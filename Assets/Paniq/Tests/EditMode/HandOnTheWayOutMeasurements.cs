using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Diagnostics for the owner's playtest notes of 2026-09-30 (the fourth
    /// pass of the hand). Not checks: they play the office as the level
    /// defines it and print what happened, for the numbers and reasons in
    /// docs/technical-decisions.md. Run on purpose:
    /// <c>.\tools\RunUnityTests.ps1 -Filter HandOnTheWayOut -ShowPassed</c>.
    /// Normal runs skip them.
    /// </summary>
    [Explicit, Category("Measure")]
    public sealed class HandOnTheWayOutMeasurements
    {
        private const string TheOfficeAsset = "Assets/Paniq/Content/Levels/TheOffice.asset";
        private const int CapTicks = 12000;

        /// <summary>The owner presses Trigger event early: six seconds in.</summary>
        private const int TriggerTick = 300;

        private static LevelDefinition TheLevel()
        {
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(TheOfficeAsset);
            Assert.That(level, Is.Not.Null, $"The office level is not at {TheOfficeAsset}.");
            return level;
        }

        private static ScenarioData Data(LevelDefinition level)
        {
            ScenarioData data = level.ToRuntimeData();
            if (level.PhysicsFeel != null && level.PhysicsFeel.Feel != null)
            {
                data.PhysicsFeel = level.PhysicsFeel.Feel.Clone();
            }

            return data;
        }

        /// <summary>
        /// The owner: "I ran a seed 42 and got most of the agents to the
        /// final corridor, but even though I only influenced the exit, none
        /// survived." Seed 42, the fire triggered early, played left alone
        /// and then with the hand held on the way out from the moment the
        /// first frightened person reaches the crossbar. Prints the story of
        /// the card and the door every five seconds.
        /// </summary>
        [TestCase(42UL, TriggerTick)]
        [TestCase(42UL, 1000)]
        [TestCase(42UL, 2000)]
        [TestCase(42UL, 3000)]
        [TestCase(41UL, TriggerTick)]
        [TestCase(43UL, TriggerTick)]
        public void ASeed_Triggered_LeftAlone_AndWithTheHandOnTheWayOut(ulong seed, int triggerTick)
        {
            LevelDefinition level = TheLevel();
            var report = new StringBuilder();
            report.AppendLine(Play(Data(level), seed, triggerTick, holdTheWayOut: false));
            report.AppendLine(Play(Data(level), seed, triggerTick, holdTheWayOut: true));
            TestContext.WriteLine(report.ToString());
        }

        private static string Play(ScenarioData data, ulong seed, int triggerTick, bool holdTheWayOut)
        {
            var report = new StringBuilder();
            report.AppendLine($"seed {seed}, triggered at {triggerTick / Run.TicksPerSecond} s, " +
                              (holdTheWayOut ? "the hand held on the way out once the crossbar fills:" : "left alone:"));
            using (var simulation = new Run(data, seed))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), triggerTick);
                bool holding = false;
                int heldFrom = -1;
                int ended = -1;
                for (int tick = 0; tick < CapTicks; tick++)
                {
                    simulation.Step();
                    if (holdTheWayOut && !holding && InTheCrossbar(simulation, frightenedOnly: true) > 0)
                    {
                        simulation.QueueCommand(PlayerCommandType.InfluenceDoor, TheBuilding.TheWayOut, simulation.Tick + 1);
                        holding = true;
                        heldFrom = simulation.Tick + 1;
                    }

                    if (tick % (5 * Run.TicksPerSecond) == 0 && tick >= triggerTick - 5 * Run.TicksPerSecond)
                    {
                        report.AppendLine("  " + Line(simulation, tick));
                    }

                    if (simulation.Phase == RoundPhase.Over)
                    {
                        ended = tick;
                        break;
                    }
                }

                RunSnapshot snapshot = simulation.NewSnapshotBuffer();
                simulation.FillSnapshot(snapshot);
                report.AppendLine($"  ended {(ended < 0 ? "at the cap" : $"at {ended / Run.TicksPerSecond} s")}: saved {snapshot.SavedCount} " +
                                  $"(out {snapshot.EscapedCount}, sat it out {snapshot.SurvivedCount}), lost {snapshot.LostCount}, " +
                                  $"still inside {snapshot.RemainingCount}; hand from {(heldFrom < 0 ? "never" : $"{heldFrom / Run.TicksPerSecond} s")}");
                report.AppendLine("  " + Counts(simulation));
                report.AppendLine("  " + WhoDiedWhere(simulation));
            }

            return report.ToString();
        }

        private static int InTheCrossbar(Run simulation, bool frightenedOnly)
        {
            int count = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Participation != AgentParticipation.Participating ||
                    (frightenedOnly && agent.FearState != AgentFearState.Scared))
                {
                    continue;
                }

                LogicalPosition at = agent.Position;
                if (at.X >= 13000 && at.X <= 16000 && at.Z >= -500 && at.Z <= 17000)
                {
                    count++;
                }
            }

            return count;
        }

        private static string Line(Run simulation, int tick)
        {
            int second = tick / Run.TicksPerSecond;
            string card = "no card";
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                if (thing.Kind != PhysicsObjectKind.Keycard)
                {
                    continue;
                }

                if (!thing.IsHeld)
                {
                    card = $"card free at {thing.Position}";
                }
                else
                {
                    AgentSnapshot holder = simulation.GetAgent(thing.HeldBy);
                    card = $"card with {thing.HeldBy.Value} ({holder.ActivityState}, {holder.FearState}, {holder.BodyState}) at {holder.Position}";
                }
            }

            int answering = 0;
            int forcing = 0;
            int scared = 0;
            int down = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                answering += agent.ActingForTheHand ? 1 : 0;
                forcing += agent.ActivityState == AgentActivityState.ForcingDoor || agent.ActivityState == AgentActivityState.TryingDoor ? 1 : 0;
                scared += agent.FearState == AgentFearState.Scared ? 1 : 0;
                down += agent.BodyState != AgentBodyState.Upright ? 1 : 0;
            }

            DoorSnapshot wayOut = default;
            for (int d = 0; d < simulation.DoorCount; d++)
            {
                if (simulation.GetDoor(d).DoorId == TheBuilding.TheWayOut)
                {
                    wayOut = simulation.GetDoor(d);
                }
            }

            return $"{second,3}s crossbar {InTheCrossbar(simulation, false),2} | scared {scared,2} down {down,2} | " +
                   $"answering {answering,2} at doors {forcing,2} | way out {wayOut.State}{(wayOut.NeedsKeycard ? " (card)" : "")} | {card}";
        }

        private static string Counts(Run simulation)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                switch (record.EventType)
                {
                    case CausalEventType.AgentDrawnByInfluence:
                    case CausalEventType.AgentActedForTheHand:
                    case CausalEventType.AgentForcedDoor:
                    case CausalEventType.AgentGaveUpOnDoor:
                    case CausalEventType.AgentTookKeycard:
                    case CausalEventType.KeycardDropped:
                    case CausalEventType.DoorUnlockedWithKeycard:
                    case CausalEventType.DoorOpened:
                    case CausalEventType.AgentEscaped:
                    case CausalEventType.PowerInfluenced:
                    case CausalEventType.PowerReleasedInfluence:
                        string key = record.EventType.ToString();
                        counts.TryGetValue(key, out int n);
                        counts[key] = n + 1;
                        break;
                }
            }

            var line = new StringBuilder("events:");
            foreach (KeyValuePair<string, int> pair in counts)
            {
                line.Append($" {pair.Key}={pair.Value}");
            }

            return line.ToString();
        }

        private static string WhoDiedWhere(Run simulation)
        {
            var line = new StringBuilder("lost:");
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Outcome == AgentTerminalOutcome.Lost)
                {
                    line.Append($" #{i + 1}@{agent.Position}");
                }
            }

            return line.ToString();
        }

        /// <summary>
        /// The seeds that save most left alone: when the tower falls, whether
        /// the archway is piled and stays piled, how many boxes lie in its
        /// strip, when the way out opens, and how many get out.
        /// </summary>
        [TestCase(64UL)]
        [TestCase(48UL)]
        [TestCase(67UL)]
        [TestCase(42UL)]
        public void ASeed_LeftAlone_TheTowerAndTheArchway(ulong seed)
        {
            LevelDefinition level = TheLevel();
            ScenarioData data = Data(level);
            var report = new StringBuilder();
            report.AppendLine($"seed {seed} left alone (the Director's own timing):");
            using (var simulation = new Run(data, seed))
            {
                int archway = -1;
                int wayOut = -1;
                for (int d = 0; d < simulation.DoorCount; d++)
                {
                    if (simulation.GetDoor(d).DoorId == TheBuilding.Archway) archway = d;
                    if (simulation.GetDoor(d).DoorId == TheBuilding.TheWayOut) wayOut = d;
                }

                bool wasPiled = false;
                for (int tick = 0; tick < CapTicks && simulation.Phase != RoundPhase.Over; tick++)
                {
                    simulation.Step();
                    DoorSnapshot arch = simulation.GetDoor(archway);
                    if (arch.IsPiled != wasPiled)
                    {
                        report.AppendLine($"  {tick / Run.TicksPerSecond,3}s archway piled -> {arch.IsPiled}");
                        wasPiled = arch.IsPiled;
                    }

                    if (tick % (10 * Run.TicksPerSecond) == 0)
                    {
                        int inStrip = 0;
                        int fallen = 0;
                        for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                        {
                            PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                            if (thing.ObjectId.Value < 3701UL || thing.ObjectId.Value > 3708UL) continue;
                            if (Math.Abs(thing.Position.X - 13000) <= 700 && thing.Position.Z > 6000 && thing.Position.Z < 9000) inStrip++;
                            if (IntegerMath.Distance(thing.Position, TheBuilding.TheTower) > 800) fallen++;
                        }

                        RunSnapshot snap = simulation.NewSnapshotBuffer();
                        simulation.FillSnapshot(snap);
                        report.AppendLine($"  {tick / Run.TicksPerSecond,3}s fire {(snap.FireActive ? "on" : "off")} squares {snap.FireCells.Count,3} | tower boxes fallen {fallen} in strip {inStrip} | archway {arch.State} piled {arch.IsPiled} | way out {simulation.GetDoor(wayOut).State} | saved {snap.SavedCount} lost {snap.LostCount} inside {snap.RemainingCount}");
                    }
                }

                RunSnapshot end = simulation.NewSnapshotBuffer();
                simulation.FillSnapshot(end);
                report.AppendLine($"  ended: saved {end.SavedCount} lost {end.LostCount}; allowed {(simulation.DirectorForTests.CapsForTests ? simulation.DirectorForTests.AllowanceForTests.ToString() : "-")}");
                report.AppendLine("  " + Counts(simulation));
            }

            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// The owner: "Objects (like chairs) often clip inside walls, maybe
        /// more." Ten seeds left alone, every loose thing checked every
        /// tenth of a second for how far it is sunk into a wall. Prints the
        /// worst by kind, and how many ticks any thing spent more than a
        /// finger's width inside one.
        /// </summary>
        [Test]
        public void TenSeeds_LeftAlone_HowOftenThingsSinkIntoWalls()
        {
            LevelDefinition level = TheLevel();
            var report = new StringBuilder();
            report.AppendLine("seed | ticks with a thing > 20 mm into a wall | worst (mm, kind, id, at)");
            for (ulong seed = 40UL; seed <= 49UL; seed++)
            {
                ScenarioData data = Data(level);
                using (var simulation = new Run(data, seed))
                {
                    simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), TriggerTick);
                    int badTicks = 0;
                    int worst = 0;
                    string worstWhat = "-";
                    var byKind = new SortedDictionary<string, int>();
                    for (int tick = 0; tick < CapTicks && simulation.Phase != RoundPhase.Over; tick++)
                    {
                        simulation.Step();
                        if (tick % 5 != 0)
                        {
                            continue;
                        }

                        bool bad = false;
                        for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                        {
                            PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                            if (thing.Dormant || thing.IsHeld)
                            {
                                continue;
                            }

                            int depth = simulation.WallPenetrationForTests(i);
                            if (depth > 20)
                            {
                                bad = true;
                                string kind = thing.Kind.ToString();
                                byKind.TryGetValue(kind, out int n);
                                byKind[kind] = n + 1;
                            }

                            if (depth > worst)
                            {
                                worst = depth;
                                worstWhat = $"{thing.Kind} {thing.ObjectId.Value} at {thing.Position} ({tick / Run.TicksPerSecond} s)";
                            }
                        }

                        badTicks += bad ? 5 : 0;
                    }

                    var kinds = new StringBuilder();
                    foreach (KeyValuePair<string, int> pair in byKind)
                    {
                        kinds.Append($" {pair.Key}={pair.Value * 5}");
                    }

                    report.AppendLine($"{seed} | {badTicks} | {worst} {worstWhat} |{kinds}");
                }
            }

            TestContext.WriteLine(report.ToString());
        }
    }
}
