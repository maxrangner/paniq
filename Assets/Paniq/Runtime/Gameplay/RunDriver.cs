using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Owns the one simulation FixedUpdate entry point for the prototype. The
    /// run is created the first time anything asks for it, so other
    /// components can read it from their own Awake whatever order Unity
    /// starts them in.
    /// <para>
    /// It also owns the clock the player controls: the run is held still
    /// behind the start card, runs while the round is going, freezes when
    /// paused, and stops for good when the round is over.
    /// </para>
    /// </summary>
    public sealed class RunDriver : MonoBehaviour
    {
        [Tooltip("The level being played: which building, how a round in it begins, and what clears it.")]
        [SerializeField] private LevelDefinition level;

        [Tooltip("Ignored when a level is assigned above. The building on its own, for a scene that has no level yet.")]
        [SerializeField] private ScenarioAsset scenario;

        [Tooltip("Every level the start card offers (2026-09-30). The level above is always offered first, whether or not it is listed here.")]
        [SerializeField] private LevelDefinition[] levels;

        /// <summary>The level this run was built in: the one asked for on the start card, or the one wired above.</summary>
        private LevelDefinition chosen;

        [Tooltip("Play with this physics feel instead of the scenario's own. Leave empty for the scenario's values.")]
        [SerializeField] private PhysicsFeelPreset physicsFeel;

        [Tooltip("Editor only: copy the preset's dials into the run every tick, so changes show while playing. " +
                 "Floor grip, top speed and wall and table height only change on the next Play. A run tuned live cannot be replayed.")]
        [SerializeField] private bool livePhysicsTuning;

        private bool warnedAboutFeel;

        private Run simulation;
        private RunSnapshot current;
        private RunSnapshot previous;

        /// <summary>
        /// Two snapshots filled turn about: the one the display is blending
        /// from (last tick) and the one it is blending to (this tick). Made
        /// afresh for each run, because they are sized for it.
        /// </summary>
        private readonly RunSnapshot[] buffers = new RunSnapshot[2];
        private Run buffersFor;

        /// <summary>The run. Read its <see cref="Run.Scenario"/> for the values it actually uses.</summary>
        public Run Simulation
        {
            get
            {
                if (simulation == null)
                {
                    // The seed is settled here, before tick zero, and recorded
                    // so the player can ask for the same one again.
                    chosen = LevelSession.Choose(level, levels);
                    simulation = new Run(BuildScenarioData(), LevelSession.TakeSeedFor(chosen));
                    Seed = LevelSession.CurrentSeed;

                    // "Play again" means the player has already chosen; only a
                    // fresh arrival waits behind the start card.
                    IsWaitingToStart = !LevelSession.StartImmediately;
                    LevelSession.ClearRequestedSeed();
                }

                return simulation;
            }
        }

        /// <summary>
        /// The building and rules a run is built from: the level's, or the
        /// scenario's own played by a level's rules, with the physics feel
        /// written over it. The same data builds the round the player plays
        /// and the one played with nobody at the controls (see
        /// <see cref="LeftAloneRunner"/>), so the two differ only in the clicks.
        /// </summary>
        private ScenarioData BuildScenarioData()
        {
            ScenarioData data;
            if (Level != null)
            {
                data = Level.ToRuntimeData();
            }
            else
            {
                // No level assigned: the building on its own, played by
                // the same rules a level would impose, so the scene is
                // playable without any setup step.
                if (scenario == null)
                {
                    scenario = ScenarioAsset.CreateDefault();
                }

                data = scenario.ToRuntimeData();
                data.Round.HazardWaitsForTrigger = true;
            }

            if (EffectiveFeel() != null && FeelIsUsable())
            {
                data.PhysicsFeel = EffectiveFeel().Feel.Clone();
            }

            return data;
        }

        /// <summary>
        /// The same seed played without the player's help, for the end card's
        /// "left alone" line (2026-09-27). Built with the scene, while the
        /// scene is loading anyway, so its start costs no frame of play; kept
        /// in step with the real round until the disaster starts, then run
        /// ahead. Never on a run being tuned live, which no replay could match.
        /// </summary>
        private LeftAloneRunner leftAlone;

        /// <summary>At most this many ticks of the hands-off round a fixed step: twenty times as fast as the real one.</summary>
        private const int LeftAloneTicksPerStep = 20;

        /// <summary>
        /// And never more than this much work a fixed step, in milliseconds,
        /// however cheap or dear a tick is: a busy building takes longer to
        /// work out, it does not make the frame stutter.
        /// </summary>
        private const double LeftAloneMillisecondsPerStep = 3.0;

        /// <summary>How many would have lived with nobody at the controls, once known.</summary>
        public int? LeftAloneSavedCount => leftAlone?.SavedCount;

        /// <summary>The hands-off round exists and has not finished yet.</summary>
        public bool LeftAloneStillWorking => leftAlone != null && !leftAlone.IsDone;

        /// <summary>State after the latest tick, filled at most once per tick.</summary>
        public RunSnapshot Snapshot => current ??= FillSpareSnapshot();

        /// <summary>
        /// Fills whichever of the two buffers the display is not still
        /// reading as last tick's state. Before there were two, every tick
        /// built a fresh snapshot (a few kilobytes of arrays), and at a few
        /// hundred people that is what turns into the runtime pausing to
        /// tidy memory.
        /// </summary>
        private RunSnapshot FillSpareSnapshot()
        {
            Run run = Simulation;
            if (!ReferenceEquals(buffersFor, run))
            {
                buffers[0] = run.NewSnapshotBuffer();
                buffers[1] = run.NewSnapshotBuffer();
                buffersFor = run;
                previous = null;
            }

            RunSnapshot spare = ReferenceEquals(previous, buffers[0]) ? buffers[1] : buffers[0];
            run.FillSnapshot(spare);
            return spare;
        }

        /// <summary>State one tick earlier, so presentation can blend smoothly between ticks.</summary>
        public RunSnapshot PreviousSnapshot => previous ?? Snapshot;

        /// <summary>
        /// True once live tuning has been on for this run: its physics no
        /// longer follow from its scenario and seed alone, so it cannot be replayed.
        /// </summary>
        public bool IsLiveTuned { get; private set; }

        /// <summary>The name of the physics feel preset in use, or null for the scenario's own values.</summary>
        public string PhysicsFeelName => EffectiveFeel() != null ? EffectiveFeel().name : null;

        /// <summary>The level being played: the one chosen on the start card, or the one this runner is wired to. Never null once the run exists.</summary>
        public LevelDefinition Level => chosen != null ? chosen : level;

        /// <summary>
        /// Every level the start card offers, the wired one first and no
        /// level twice; an empty slot in the list is skipped.
        /// </summary>
        public IReadOnlyList<LevelDefinition> Levels
        {
            get
            {
                var offered = new List<LevelDefinition>();
                if (level != null)
                {
                    offered.Add(level);
                }

                if (levels != null)
                {
                    foreach (LevelDefinition candidate in levels)
                    {
                        if (candidate != null && !offered.Contains(candidate))
                        {
                            offered.Add(candidate);
                        }
                    }
                }

                return offered;
            }
        }

        /// <summary>The seed this run was built from.</summary>
        public ulong Seed { get; private set; }

        /// <summary>
        /// Held still behind the start card: the run exists and can be looked
        /// at, but no tick has happened yet.
        /// </summary>
        public bool IsWaitingToStart { get; private set; }

        /// <summary>
        /// Frozen so the scene can be read. Time stops for everything --
        /// people, fire, smoke and sparks -- while the camera keeps answering
        /// the player, and nothing they do reaches the run.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>Whether the run is actually advancing right now.</summary>
        public bool IsTicking => !IsWaitingToStart && !IsPaused && Simulation.Phase != RoundPhase.Over;

        /// <summary>The player pressing Play on the start card.</summary>
        public void BeginPlaying()
        {
            IsWaitingToStart = false;
        }

        /// <summary>
        /// Pause to look, not to act. A round that is already over cannot be
        /// paused; it is frozen already.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (Simulation.Phase == RoundPhase.Over)
            {
                paused = false;
            }

            IsPaused = paused;
            ApplyTimeScale();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>
        /// Freezing everything, not just the simulation. The run steps its own
        /// physics world by hand, so stopping Unity's clock does not change
        /// the size of a tick -- it just stops ticks happening at all, and
        /// stops the particle effects with them.
        /// </summary>
        private void ApplyTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        /// <summary>The level's physics feel, or the one set directly on this component.</summary>
        private PhysicsFeelPreset EffectiveFeel()
        {
            if (Level != null && Level.PhysicsFeel != null)
            {
                return Level.PhysicsFeel;
            }

            return physicsFeel;
        }

        private void Awake()
        {
            _ = Simulation;

            // The hands-off copy is built now, with the scene: a whole second
            // run, which would be a hitch on the first frame of play.
            // Not on a level whose trigger sets nothing off (2026-09-30):
            // with no disaster to leave alone, the comparison is empty.
            if (!(Application.isEditor && livePhysicsTuning) && (Level == null || Level.TriggerStartsAHazard))
            {
                leftAlone = new LeftAloneRunner(BuildScenarioData(), Seed);
            }

            // A previous run may have left the clock stopped, and a reloaded
            // scene inherits it.
            IsPaused = false;
            ApplyTimeScale();
        }

        private void FixedUpdate()
        {
            // Behind the start card, paused, or finished: in all three the
            // scene stands still and can be looked at, and no tick happens.
            if (IsTicking)
            {
                Advance();
            }

            AdvanceLeftAlone();
        }

        /// <summary>
        /// The hands-off round's share of a fixed step. Until the disaster has
        /// started in the real round it goes no further than the real round
        /// has, so the player's Trigger event can still be copied onto the
        /// same tick; after that it runs ahead, paused or not, a budgeted
        /// handful of ticks at a time.
        /// </summary>
        private void AdvanceLeftAlone()
        {
            if (leftAlone == null)
            {
                return;
            }

            if (IsLiveTuned)
            {
                leftAlone.Dispose();
                leftAlone = null;
                return;
            }

            int noFurtherThan = Simulation.Phase == RoundPhase.BeforeEvent ? Simulation.Tick : int.MaxValue;
            long budget = (long)(LeftAloneMillisecondsPerStep * System.Diagnostics.Stopwatch.Frequency / 1000.0);
            leftAlone.Advance(LeftAloneTicksPerStep, noFurtherThan, budget);
        }

        private void OnDestroy()
        {
            // The clock is Unity's, not this object's, so a scene reload that
            // happened while paused must not leave the next one frozen.
            Time.timeScale = 1f;

            // The run keeps a physics scene of its own; let it go with the runner.
            simulation?.Dispose();
            simulation = null;
            leftAlone?.Dispose();
            leftAlone = null;
        }

        /// <summary>
        /// A player click on a door, queued for the next tick that has not
        /// started. Locked becomes unlocked, unlocked becomes open.
        /// </summary>
        /// <summary>Copies the preset's live dials into the run, when live tuning is on in the editor.</summary>
        private void TuneLive()
        {
            PhysicsFeelPreset feel = EffectiveFeel();
            if (!Application.isEditor || !livePhysicsTuning || feel == null || !FeelIsUsable())
            {
                return;
            }

            Simulation.Scenario.PhysicsFeel.TakeLiveValuesFrom(feel.Feel);
            IsLiveTuned = true;
        }

        /// <summary>Whether the preset's values would be accepted; a bad one is ignored, with one warning.</summary>
        private bool FeelIsUsable()
        {
            PhysicsFeelPreset feel = EffectiveFeel();
            string error = "It has no values.";
            if (feel.Feel != null && feel.Feel.IsValid(out error))
            {
                warnedAboutFeel = false;
                return true;
            }

            if (!warnedAboutFeel)
            {
                warnedAboutFeel = true;
                Debug.LogWarning($"Paniq: the physics feel preset '{feel.name}' is not usable, so it is ignored. {error}",
                    feel);
            }

            return false;
        }

        public void QueueDoorClick(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ClickDoor, doorId, Simulation.Tick + 1);
        }

        /// <summary>The player turning a door's key (a right click since 2026-09-26), queued for the next tick that has not started.</summary>
        public void QueueLockToggle(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ToggleLock, doorId, Simulation.Tick + 1);
        }

        /// <summary>The player taking hold of a door to keep it shut (prototype 3), queued for the next tick that has not started.</summary>
        public void QueueHoldDoor(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.HoldDoor, doorId, Simulation.Tick + 1);
        }

        /// <summary>The player letting go of a held door, queued for the next tick that has not started.</summary>
        public void QueueReleaseDoor(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ReleaseDoor, doorId, Simulation.Tick + 1);
        }

        /// <summary>The player nudging somebody (prototype 3), queued for the next tick that has not started.</summary>
        public void QueueNudge(SimulationId personId)
        {
            Simulation.QueueCommand(PlayerCommandType.NudgePerson, personId, Simulation.Tick + 1);
        }

        /// <summary>The player nudging somebody from a point on the floor: they step away from it (2026-09-26).</summary>
        public void QueueNudge(SimulationId personId, LogicalPosition from)
        {
            Simulation.QueueCommand(PlayerCommandType.NudgePersonFrom, personId, from, Simulation.Tick + 1);
        }

        /// <summary>The player's hand going on a door (2026-09-26; a hold since 2026-09-29), queued for the next tick that has not started.</summary>
        public void QueueInfluenceDoor(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.InfluenceDoor, doorId, Simulation.Tick + 1);
        }

        /// <summary>The player's hand going on a thing.</summary>
        public void QueueInfluenceThing(SimulationId thingId)
        {
            Simulation.QueueCommand(PlayerCommandType.InfluenceThing, thingId, Simulation.Tick + 1);
        }

        /// <summary>The player's hand going on a patch of floor, in whole millimetres.</summary>
        public void QueueInfluenceSpot(LogicalPosition spot)
        {
            Simulation.QueueCommand(PlayerCommandType.InfluenceSpot, spot, Simulation.Tick + 1);
        }

        /// <summary>The player's hand coming off the place it was on (2026-09-29).</summary>
        public void QueueReleaseInfluence()
        {
            Simulation.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), Simulation.Tick + 1);
        }

        /// <summary>The player taking hold of somebody by the shirt (2026-09-29).</summary>
        public void QueueTug(SimulationId personId)
        {
            Simulation.QueueCommand(PlayerCommandType.TugPerson, personId, Simulation.Tick + 1);
        }

        /// <summary>The player letting go of the person they had hold of (2026-09-29).</summary>
        public void QueueReleaseTug(SimulationId personId)
        {
            Simulation.QueueCommand(PlayerCommandType.ReleaseTug, personId, Simulation.Tick + 1);
        }

        /// <summary>The player pulling a fire alarm, queued for the next tick that has not started.</summary>
        public void QueueAlarmPull(SimulationId alarmId)
        {
            Simulation.QueueCommand(PlayerCommandType.PullAlarm, alarmId, Simulation.Tick + 1);
        }

        /// <summary>
        /// The player setting the disaster going, queued for the next tick that
        /// has not started. Only the first one does anything.
        /// </summary>
        public void QueueTriggerEvent()
        {
            // The hands-off round starts its disaster on the same tick: when
            // the disaster starts is the round, not the player's help.
            if (Simulation.Phase == RoundPhase.BeforeEvent)
            {
                leftAlone?.MirrorTrigger(Simulation.Tick + 1);
            }

            Simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), Simulation.Tick + 1);
        }

        /// <summary>
        /// The crowd switch flicked to "panicked" (2026-09-30), queued for the
        /// next tick that has not started. Not mirrored into the hands-off
        /// round: a level with the switch has no hands-off round.
        /// </summary>
        public void QueueCrowdPanicked()
        {
            Simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), Simulation.Tick + 1);
        }

        /// <summary>The crowd switch flicked to "calm" (2026-09-30), queued for the next tick that has not started.</summary>
        public void QueueCrowdCalm()
        {
            Simulation.QueueCommand(PlayerCommandType.SetCrowdCalm, default(SimulationId), Simulation.Tick + 1);
        }

        /// <summary>
        /// A card played on somebody, queued for the next tick that has not
        /// started.
        /// </summary>
        public void QueueCard(PlayerCommandType card, SimulationId personId)
        {
            Simulation.QueueCommand(card, personId, Simulation.Tick + 1);
        }

        /// <summary>A card played on a place, in whole millimetres.</summary>
        public void QueueCard(PlayerCommandType card, LogicalPosition spot)
        {
            Simulation.QueueCommand(card, spot, Simulation.Tick + 1);
        }

        public void StepForTests()
        {
            Advance();
        }

        private void Advance()
        {
            TuneLive();
            previous = Snapshot;
            Simulation.Step();
            current = null;
        }
    }
}
