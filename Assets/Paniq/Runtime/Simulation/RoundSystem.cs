namespace Paniq.Simulation
{
    /// <summary>
    /// When a round starts, when it is finished, and how it scored.
    /// <para>
    /// A round begins calm. Nothing is wrong until the player triggers the
    /// event, and until they do the round can never end. Once it has started,
    /// it runs until everybody is out of the building or dead -- or until the
    /// whole building has been doing nothing at all for long enough that there
    /// is plainly nothing left to wait for.
    /// </para>
    /// <para>
    /// It used to end the moment everybody left was in a room the fire could
    /// not reach. That stopped rounds while people were still walking towards
    /// the door, and a shut door no longer makes a room permanently safe
    /// anyway, so the rule has gone.
    /// </para>
    /// <para>
    /// The one thing the stall clock must never mistake for a settled building
    /// is a queue. Twelve people wedged in a doorway can cover almost no ground
    /// for a minute, so distance alone is not enough: see
    /// <see cref="SomethingIsStillHappening"/>, which also asks who is trying
    /// to move and cannot.
    /// </para>
    /// <para>
    /// This lives in the simulation, not the display, because it decides an
    /// outcome: it is what turns the last survivors into people who were
    /// saved. The display only reads the result off the snapshot.
    /// </para>
    /// </summary>
    internal sealed class RoundSystem : IBindable
    {
        private readonly SimulationContext context;
        private readonly Agent[] agents;
        private readonly WorldGeometry geometry;
        private readonly Threats threats;
        private readonly RoundSettings settings;

        /// <summary>Wired up after construction: both are built after this system is.</summary>
        private FlammablesSystem flammables;
        private DoorSystem doors;

        /// <summary>
        /// Where everybody was standing when the stall clock last started, so
        /// "has anybody got anywhere" can be asked without keeping a history.
        /// </summary>
        private readonly LogicalPosition[] stallAnchor;

        /// <summary>What the rest of the world looked like at that same moment.</summary>
        private int anchoredResolved = -1;
        private long anchoredThreats = -1L;
        private int anchoredBurningThings = -1;
        private long anchoredDoors;

        /// <summary>The first tick on which nothing at all was happening, or -1 while something is.</summary>
        private int stalledSinceTick = -1;

        public RoundSystem(SimulationContext context, Agent[] agents, WorldGeometry geometry, Threats threats)
        {
            this.context = context;
            this.agents = agents;
            this.geometry = geometry;
            this.threats = threats;
            settings = context.Scenario.Round;
            stallAnchor = new LogicalPosition[agents.Length];
        }

        /// <summary>Both are built after this system, so they are handed over once everything exists.</summary>
        public void Bind(Systems systems)
        {
            flammables = systems.Flammables;
            doors = systems.Doors;
        }

        /// <summary>Where the round has got to.</summary>
        public RoundPhase Phase { get; private set; } = RoundPhase.BeforeEvent;

        /// <summary>The event that set the disaster going, for anything that wants to name its cause.</summary>
        public ulong TriggerEventId { get; private set; }

        /// <summary>The tick the round finished on, or -1 while it is still going.</summary>
        public int EndedOnTick { get; private set; } = -1;

        /// <summary>
        /// The player's "trigger event". The first one starts the round and
        /// asks the hazard to begin; any later one is ignored, so a player
        /// leaning on the button cannot light two fires.
        /// </summary>
        public void TriggerEvent()
        {
            if (Phase != RoundPhase.BeforeEvent)
            {
                return;
            }

            Phase = RoundPhase.Running;
            CausalEvent triggered = context.Events.Append(
                context.Tick,
                default,
                CausalEventType.RoundEventTriggered,
                geometry.FireArea.Centre,
                context.Tick);
            TriggerEventId = triggered.EventId;
            threats.RequestStart();
        }

        /// <summary>
        /// Last phase of the tick: has the round finished? Called after
        /// everything else has settled, so it judges the tick as it ended
        /// rather than as it was half way through.
        /// </summary>
        public void Update()
        {
            if (Phase == RoundPhase.BeforeEvent)
            {
                if (!threats.StartRequested)
                {
                    return;
                }

                // The hazard started itself on its own tick count rather than
                // being triggered. Same round; the hazard's own start is what
                // the survivors and the end of the round are blamed on, so
                // that nothing in the log is left without a cause.
                Phase = RoundPhase.Running;
                TriggerEventId = threats.RootEventId;
            }

            if (Phase != RoundPhase.Running)
            {
                return;
            }

            if (NobodyLeftUnresolved())
            {
                // Everyone is already out or dead. There is nothing left that
                // could change, so there is nothing to wait for.
                Finish();
                return;
            }

            if (SomethingIsStillHappening())
            {
                DropAnchor();
                stalledSinceTick = -1;
                return;
            }

            if (stalledSinceTick < 0)
            {
                stalledSinceTick = context.Tick;
            }

            if (context.Tick - stalledSinceTick < settings.StallTicks)
            {
                return;
            }

            Finish();
        }

        /// <summary>
        /// Whether anybody is getting anywhere, trying to, or being done to.
        /// Any one of these restarts the clock, so the round only ends on a
        /// building that is genuinely still.
        /// <para>
        /// The blocked-ticks question is the one that matters most. A crowd
        /// jammed in a doorway barely moves, but every one of them is pressing
        /// forward and getting nowhere, which is exactly what that counter
        /// measures. Without it, a queue at the one way out of the building
        /// would read as a settled room and the round would be called over on
        /// top of them.
        /// </para>
        /// </summary>
        private bool SomethingIsStillHappening()
        {
            long moved = settings.StallMoveMillimetres;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                if (agent.Burning.IsBurning || agent.Body.Speed > 0 || agent.Body.BlockedTicks > 0 ||
                    agent.Body.State != AgentBodyState.Upright ||
                    IsBusy(agent.Intent.Activity) ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, stallAnchor[i]) > moved * moved)
                {
                    return true;
                }
            }

            return anchoredResolved != ResolvedCount() ||
                   anchoredThreats != threats.Signature ||
                   anchoredBurningThings != (flammables == null ? 0 : flammables.BurningCount) ||
                   anchoredDoors != (doors == null ? 0L : doors.DoorSignature);
        }

        /// <summary>
        /// Someone standing still on purpose, in the middle of doing something
        /// that will end: rattling a door, hauling somebody along the floor,
        /// emptying an extinguisher at the fire.
        /// </summary>
        private static bool IsBusy(AgentActivityState activity)
        {
            switch (activity)
            {
                case AgentActivityState.TryingDoor:
                case AgentActivityState.ForcingDoor:
                case AgentActivityState.OpeningDoor:
                case AgentActivityState.Grabbing:
                case AgentActivityState.Dragging:
                case AgentActivityState.ShakingAwake:
                case AgentActivityState.Spraying:
                case AgentActivityState.PullingAlarm:
                case AgentActivityState.StandingUp:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Remembers the building as it stands, to measure the next stretch of quiet against.</summary>
        private void DropAnchor()
        {
            for (int i = 0; i < agents.Length; i++)
            {
                stallAnchor[i] = agents[i].Body.Position;
            }

            anchoredResolved = ResolvedCount();
            anchoredThreats = threats.Signature;
            anchoredBurningThings = flammables == null ? 0 : flammables.BurningCount;
            anchoredDoors = doors == null ? 0L : doors.DoorSignature;
        }

        private int ResolvedCount()
        {
            int resolved = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                resolved += agents[i].IsParticipating ? 0 : 1;
            }

            return resolved;
        }

        /// <summary>Whether every person has already reached a final outcome.</summary>
        private bool NobodyLeftUnresolved()
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ends the round: everybody still alive inside lived through it, and
        /// is written down as having survived. In ascending person order, as
        /// every other loop that could affect an outcome is.
        /// </summary>
        private void Finish()
        {
            int saved = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (agent.IsParticipating)
                {
                    agent.Participation = AgentParticipation.NoLongerParticipating;
                    agent.Outcome = AgentTerminalOutcome.Survived;
                    agent.Body.Speed = 0;
                    context.Events.Append(
                        context.Tick,
                        agent.Id,
                        CausalEventType.AgentSurvived,
                        agent.Body.Position,
                        0,
                        0,
                        WhatSetItOff());
                }

                saved += IsSaved(agent.Outcome) ? 1 : 0;
            }

            Phase = RoundPhase.Over;
            EndedOnTick = context.Tick;
            context.Events.Append(
                context.Tick,
                default,
                CausalEventType.RoundEnded,
                geometry.FireArea.Centre,
                saved,
                0,
                WhatSetItOff());
        }

        /// <summary>
        /// What the round's end traces back to: the player's trigger, or, in
        /// a run where the hazard began on its own clock, the hazard's own
        /// first event. It used to be the trigger alone, which left the end of
        /// an untriggered run with no cause at all.
        /// </summary>
        private ulong WhatSetItOff() => TriggerEventId != 0UL ? TriggerEventId : threats.RootEventId;

        /// <summary>Out of the building alive, or alive inside it at the end: both count.</summary>
        public static bool IsSaved(AgentTerminalOutcome outcome) =>
            outcome == AgentTerminalOutcome.Escaped || outcome == AgentTerminalOutcome.Survived;
    }
}
