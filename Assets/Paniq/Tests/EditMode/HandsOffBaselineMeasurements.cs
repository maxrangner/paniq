using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Plays the office exactly as the level defines it, with nobody at the
    /// controls, and prints what each round came to -- how many lived, where
    /// the keycard started, who took it, who swiped the way out and when,
    /// and what the Director allowed and did. This is the number the owner
    /// tunes the level by ("left alone, about five of twenty should live",
    /// 2026-09-27); the results go in docs/technical-decisions.md. Run it on
    /// purpose: <c>.\tools\RunUnityTests.ps1 -Filter HandsOffBaseline -ShowPassed</c>.
    /// Normal test runs skip it.
    /// <para>
    /// Two cases. The ten seeds, 40 to 49, are the tuning table. The fifty,
    /// 40 to 89, are the owner's rule (2026-09-28): left alone, never more
    /// than half on any seed. That case fails on a seed that saves more,
    /// and names it; the average is printed, not asserted, because it is an
    /// aim, not a rule. It is run before the commit of any batch that
    /// touches the Director, the card or the traps.
    /// </para>
    /// </summary>
    [Explicit, Category("Measure")]
    public sealed class HandsOffBaselineMeasurements
    {
        private const string TheOfficeAsset = "Assets/Paniq/Content/Levels/TheOffice.asset";

        /// <summary>A round that has not ended by itself in four minutes of game time is called here.</summary>
        private const int CapTicks = 12000;

        /// <summary>
        /// Print, every five seconds, who has the card and what they are
        /// doing. Off by default: it is a page a seed, and it is for finding
        /// out why a holder never reached the door.
        /// </summary>
        private const bool PrintTheTrail = false;

        [Test]
        public void SeedsFortyToFortyNine_LeftAlone()
        {
            Play(40UL, 49UL, neverMoreThanHalf: false);
        }

        [Test]
        public void FiftySeeds_LeftAlone_NeverMoreThanHalfLive()
        {
            Play(40UL, 89UL, neverMoreThanHalf: true);
        }

        private static void Play(ulong firstSeed, ulong lastSeed, bool neverMoreThanHalf)
        {
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(TheOfficeAsset);
            Assert.That(level, Is.Not.Null, $"The office level is not at {TheOfficeAsset}.");

            int seeds = (int)(lastSeed - firstSeed + 1);
            var report = new StringBuilder();
            report.AppendLine($"The office, left alone (nobody at the controls), seeds {firstSeed} to {lastSeed}:");
            report.AppendLine("seed | saved | got out | sat it out | lost | ended (s) | allowed | the keycard and the Director");
            int cleared = 0;
            int savedInAll = 0;
            var overHalf = new List<string>();
            for (ulong seed = firstSeed; seed <= lastSeed; seed++)
            {
                ScenarioData data = level.ToRuntimeData();
                if (level.PhysicsFeel != null && level.PhysicsFeel.Feel != null)
                {
                    data.PhysicsFeel = level.PhysicsFeel.Feel.Clone();
                }

                using (var simulation = new Run(data, seed))
                {
                    int ended = -1;
                    int flamesReachedTheCard = -1;
                    var trail = new StringBuilder();
                    for (int tick = 0; tick < CapTicks; tick++)
                    {
                        simulation.Step();
                        if (flamesReachedTheCard < 0 && simulation.KeycardsForTests.CardNearFlamesForTests)
                        {
                            flamesReachedTheCard = tick;
                        }

                        if (PrintTheTrail && tick % (5 * Run.TicksPerSecond) == 0)
                        {
                            trail.Append(WhereTheCardIs(simulation, tick));
                        }

                        if (simulation.Phase == RoundPhase.Over)
                        {
                            ended = tick;
                            break;
                        }
                    }

                    RunSnapshot snapshot = simulation.NewSnapshotBuffer();
                    simulation.FillSnapshot(snapshot);

                    // A round still going at the cap is called as the end
                    // card's left-alone line calls it: everybody still alive
                    // lived (seed 83, 2026-09-28: the fire went out, nobody got
                    // out, and twenty frightened people kept the stall clock
                    // going for four minutes).
                    int saved = ended < 0 ? snapshot.CrowdSize - snapshot.LostCount : snapshot.SavedCount;
                    if (saved * 100 >= data.Round.TargetSavedPercent * snapshot.CrowdSize)
                    {
                        cleared++;
                    }

                    savedInAll += saved;
                    if (saved * 2 > snapshot.CrowdSize)
                    {
                        overHalf.Add($"seed {seed} saved {saved} of {snapshot.CrowdSize}");
                    }

                    string allowed = simulation.DirectorForTests.CapsForTests ? simulation.DirectorForTests.AllowanceForTests.ToString() : "-";
                    report.AppendLine(
                        $"{seed} | {saved} of {snapshot.CrowdSize} | {snapshot.EscapedCount} | {snapshot.SurvivedCount} | " +
                        $"{snapshot.LostCount} | {(ended < 0 ? "still going at the cap" : (ended / Run.TicksPerSecond).ToString())} | {allowed} | " +
                        TheStoryOfTheCard(simulation));
                    report.AppendLine("     " + WhyNobodyWent(simulation, data, flamesReachedTheCard));
                    if (PrintTheTrail)
                    {
                        report.AppendLine("     trail: " + trail);
                    }
                }
            }

            int crowd = level.ToRuntimeData().Agents.Length;
            report.AppendLine($"{cleared} of {seeds} cleared the 75% bar; {(double)savedInAll / seeds:0.0} of {crowd} saved on average " +
                              $"({100.0 * savedInAll / (seeds * crowd):0}%; the owner asked for about 25%).");
            report.AppendLine(overHalf.Count == 0
                ? "No seed saved more than half."
                : $"Over half on {overHalf.Count} of {seeds}: {string.Join("; ", overHalf)}.");
            TestContext.WriteLine(report.ToString());
            if (neverMoreThanHalf)
            {
                Assert.That(overHalf, Is.Empty,
                    "The owner's rule: left alone, never more than half live on any seed. Any seed above it is a bug.");
            }
        }

        /// <summary>
        /// The reasons a card lies unfetched, counted over the crowd: who gave
        /// the card door up (and so wanted the card), who among them was brave
        /// enough and knew where it was, and when the flames reached it.
        /// </summary>
        private static string WhyNobodyWent(Run simulation, ScenarioData data, int flamesReachedTheCard)
        {
            int gaveUp = 0;
            int wanted = 0;
            int braveEnough = 0;
            int knew = 0;
            int died = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                AgentKeycard belief = simulation.KeycardBeliefForTests(i);
                died += agent.Outcome == AgentTerminalOutcome.Lost ? 1 : 0;
                if (belief.MayFetchFromTick < 0)
                {
                    continue;
                }

                wanted++;
                braveEnough += agent.Traits.Bravery >= data.Keycard.FetchBraveryMinimum ? 1 : 0;
                knew += belief.Knows && !belief.WithSomebody ? 1 : 0;
            }

            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == CausalEventType.AgentGaveUpOnDoor && record.TargetId == TheBuilding.TheWayOut)
                {
                    gaveUp++;
                }
            }

            string flames = flamesReachedTheCard < 0 ? "the flames never reached the card"
                : $"the flames reached the card at {flamesReachedTheCard / Run.TicksPerSecond} s";
            return $"gave the door up {gaveUp} times; {wanted} wanted the card, {braveEnough} of them brave enough, " +
                   $"{knew} of them knowing it lay free; {flames}; {died} died";
        }

        /// <summary>Every five seconds: who has the card, where, and what they are doing; or where it lies.</summary>
        private static string WhereTheCardIs(Run simulation, int tick)
        {
            int second = tick / Run.TicksPerSecond;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                if (thing.Kind != PhysicsObjectKind.Keycard)
                {
                    continue;
                }

                if (!thing.IsHeld)
                {
                    return $"{second}s free {thing.Position} | ";
                }

                for (int a = 0; a < simulation.AgentCount; a++)
                {
                    AgentSnapshot agent = simulation.GetAgent(a);
                    if (agent.AgentId == thing.HeldBy)
                    {
                        return $"{second}s {agent.AgentId.Value} {agent.Position} {agent.FearState} {agent.ActivityState} {agent.BodyState}" +
                               $"{(agent.IsBurning ? " alight" : "")} | ";
                    }
                }
            }

            return "";
        }

        /// <summary>What the Director reached for, by its id.</summary>
        private static string NameOf(SimulationId target)
        {
            switch (target.Value)
            {
                case 7001UL: return "the tower";
                case 7002UL: return "the stack";
                case 3271UL: return "the office's west socket";
                case 3272UL: return "the office's east socket";
                case 3273UL: return "the cafeteria's socket";
                case 3274UL: return "the cubicles' west socket";
                case 3275UL: return "the cubicles' east socket";
                case 3276UL: return "the socket in the arm to the way out";
                case 3281UL: return "the fuse box";
                case 3205UL:
                case 3206UL:
                case 3207UL: return "another bin";
                default: return target.Value.ToString();
            }
        }

        /// <summary>Where the card began, everybody who took or dropped it, the swipe, the traps and the Director's pushes, each with its second.</summary>
        private static string TheStoryOfTheCard(Run simulation)
        {
            var story = new List<string>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                int second = record.Tick / Run.TicksPerSecond;
                switch (record.EventType)
                {
                    case CausalEventType.KeycardStarted:
                        story.Add(record.SourceId == record.TargetId ? "on a desk" : $"in {record.SourceId.Value}'s pocket");
                        break;
                    case CausalEventType.AgentTookKeycard:
                        story.Add($"{record.SourceId.Value} took it at {second} s");
                        break;
                    case CausalEventType.KeycardDropped:
                        story.Add($"{record.SourceId.Value} dropped it at {second} s");
                        break;
                    case CausalEventType.DoorUnlockedWithKeycard:
                        story.Add($"{record.SourceId.Value} swiped at {second} s");
                        break;
                    case CausalEventType.BoxTowerFell:
                        story.Add($"{(record.HasTarget ? "boxes" : "crates")} fell at {second} s");
                        break;
                    case CausalEventType.BoxPileCleared:
                        story.Add($"the heap cleared at {second} s");
                        break;
                    case CausalEventType.DirectorPushed:
                        story.Add($"PUSHED {NameOf(record.TargetId)} at {second} s ({record.Strength} on course)");
                        break;
                    case CausalEventType.AgentPassedOut:
                    case CausalEventType.AgentLost:
                        if (HadTheCard(simulation, record))
                        {
                            story.Add($"{record.SourceId.Value} {(record.EventType == CausalEventType.AgentLost ? "died" : "passed out")} with it at {second} s");
                        }

                        break;
                    case CausalEventType.DoorOpened:
                    case CausalEventType.DoorBrokenDown:
                    case CausalEventType.DoorBurntThrough:
                        if (record.TargetId == TheBuilding.TheWayOut || record.SourceId == TheBuilding.TheWayOut)
                        {
                            story.Add($"the way out {(record.EventType == CausalEventType.DoorOpened ? "opened" : "gave way")} at {second} s");
                        }

                        break;
                }
            }

            return story.Count == 0 ? "no card" : string.Join("; ", story);
        }

        /// <summary>Whether this person's fall or death was followed, within a few ticks, by the card slipping out of their pocket.</summary>
        private static bool HadTheCard(Run simulation, CausalEvent fall)
        {
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == CausalEventType.KeycardDropped && record.SourceId == fall.SourceId &&
                    record.Tick >= fall.Tick && record.Tick <= fall.Tick + 5)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
