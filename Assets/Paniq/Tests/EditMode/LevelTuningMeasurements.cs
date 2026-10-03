using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The office played by machine, for dressing the level (2026-10-02, the
    /// owner: "use all the tools and lessons available to make a good test
    /// level play fun"). Not checks: each case plays ten seeds of the office
    /// as the level defines it, with one layout candidate and one scripted
    /// player, and prints a line a seed -- who lived, what the building did,
    /// which of the level's set pieces showed up -- and a summary against the
    /// owner's rule (left alone, about a quarter live and never more than
    /// half). Run on purpose:
    /// <c>.\tools\RunUnityTests.ps1 -Filter LevelTuning -ShowPassed</c>, or
    /// one case by its name. Normal runs skip them.
    /// <para>
    /// The scripted players are rough stand-ins, not people: their numbers
    /// say whether a hand <em>can</em> matter on this floor, and whether one
    /// trick alone wins it, not how the round feels.
    /// </para>
    /// </summary>
    [Explicit, Category("Measure"), Timeout(1800000)]
    public sealed class LevelTuningMeasurements
    {
        private const string TheOfficeAsset = "Assets/Paniq/Content/Levels/TheOffice.asset";

        /// <summary>A round that has not ended by itself in four minutes of game time is called here.</summary>
        private const int CapTicks = 12000;

        /// <summary>The owner presses Trigger event early: ten seconds in.</summary>
        private const int Pressed = 500;

        /// <summary>Nobody presses: the Director's own thirty to ninety seconds.</summary>
        private const int DirectorsOwn = -1;

        private const ulong FirstSeed = 40UL;
        private const ulong LastSeed = 69UL;

        private const ulong KeycardId = 3950UL;

        private enum Player
        {
            /// <summary>Nothing: the "left alone" line.</summary>
            Nobody,

            /// <summary>Holds the way out from the moment somebody frightened reaches its arm.</summary>
            ExitCamper,

            /// <summary>Holds the nearest free extinguisher from the first flame on, and nothing else.</summary>
            BottleOnly,

            /// <summary>The card, a bottle while the bin smoulders, the heap in the archway, a push at a crackling socket.</summary>
            Careful
        }

        // ---------------------------------------------------------------- the cases

        [Test] public void New_Nobody_Pressed() => Report("as authored", null, Player.Nobody, Pressed);
        [Test] public void New_Nobody_DirectorsOwn() => Report("as authored", null, Player.Nobody, DirectorsOwn);
        [Test] public void New_ExitCamper_Pressed() => Report("as authored", null, Player.ExitCamper, Pressed);
        [Test] public void New_BottleOnly_Pressed() => Report("as authored", null, Player.BottleOnly, Pressed);
        [Test] public void New_Careful_Pressed() => Report("as authored", null, Player.Careful, Pressed);

        [Test] public void OldTower_Nobody_Pressed() => Report("the tower back in the junction's corner", OldTower, Player.Nobody, Pressed);
        [Test] public void OldStack_Nobody_Pressed() => Report("the stack back against the north wall", OldStack, Player.Nobody, Pressed);
        [Test] public void OldBottle_BottleOnly_Pressed() => Report("the office bottle beside its corridor door", OldBottle, Player.BottleOnly, Pressed);
        [Test] public void NearBottles_BottleOnly_Pressed() => Report("the bottles within the brave's reach", NearBottles, Player.BottleOnly, Pressed);
        [Test] public void NearBottles_Nobody_Pressed() => Report("the bottles within the brave's reach", NearBottles, Player.Nobody, Pressed);
        [Test] public void NearBottles_Nobody_DirectorsOwn() => Report("the bottles within the brave's reach", NearBottles, Player.Nobody, DirectorsOwn);
        [Test] public void NearBottles_Careful_Pressed() => Report("the bottles within the brave's reach", NearBottles, Player.Careful, Pressed);
        [Test] public void NearBottles_ExitCamper_Pressed() => Report("the bottles within the brave's reach", NearBottles, Player.ExitCamper, Pressed);

        [Test] public void ArmSouth_Nobody_Pressed() => Report("the arm's socket at the arm's mouth", ArmSouth, Player.Nobody, Pressed);
        [Test] public void ArmSouth_Careful_Pressed() => Report("the arm's socket at the arm's mouth", ArmSouth, Player.Careful, Pressed);

        [Test] public void DeskOnly_Nobody_Pressed() => Report("the card always on a desk", DeskOnly, Player.Nobody, Pressed);
        [Test] public void DeskOnly_Careful_Pressed() => Report("the card always on a desk", DeskOnly, Player.Careful, Pressed);
        [Test] public void PocketAnywhere_Nobody_Pressed() => Report("the card in any pocket", PocketAnywhere, Player.Nobody, Pressed);

        [Test] public void NoArmSocket_Nobody_Pressed() => Report("no socket in the exit arm", NoArmSocket, Player.Nobody, Pressed);
        [Test] public void NoArmSocket_Careful_Pressed() => Report("no socket in the exit arm", NoArmSocket, Player.Careful, Pressed);

        // ---------------------------------------------------------------- the layout candidates

        private static void OldTower(ScenarioData data)
        {
            for (ulong id = 3701UL; id <= 3708UL; id++)
            {
                Move(data, id, id <= 3704UL ? 13600 : 14200, 6350);
            }
        }

        private static void OldStack(ScenarioData data)
        {
            for (ulong id = 3581UL; id <= 3584UL; id++)
            {
                Move(data, id, 9500, -950);
            }
        }

        private static void OldBottle(ScenarioData data) => Move(data, 3301UL, 1000, 5650);

        /// <summary>The cafeteria's and the cubicles' bottles back where the brave reach them unasked: by the meeting room's door, and by the door beside the way out.</summary>
        private static void NearBottles(ScenarioData data)
        {
            Move(data, 3302UL, 4000, 16300);
            Move(data, 3303UL, 18500, 16700);
        }

        /// <summary>The exit arm's socket at the arm's mouth, just north of the junction, behind the queue rather than in it.</summary>
        private static void ArmSouth(ScenarioData data)
        {
            ulong arm = PrototypeBuilding.ExitArmSocket.Value;
            Move(data, arm, 13200, 9700);
            for (int i = 0; i < data.PowerLines.Length; i++)
            {
                PowerLineDefinition line = data.PowerLines[i];
                if (line.ToObjectId.Value == arm)
                {
                    data.PowerLines[i] = new PowerLineDefinition(line.FromObjectId, line.ToObjectId,
                        new LogicalPosition(10500, 16800),
                        new LogicalPosition(13200, 16800),
                        new LogicalPosition(13200, 9700));
                }
                else if (line.FromObjectId.Value == arm)
                {
                    data.PowerLines[i] = new PowerLineDefinition(line.FromObjectId, line.ToObjectId,
                        new LogicalPosition(13200, 9700),
                        new LogicalPosition(13200, 10000),
                        new LogicalPosition(20200, 10000),
                        new LogicalPosition(20200, 1700),
                        new LogicalPosition(20400, 1700));
                }
            }
        }

        private static void DeskOnly(ScenarioData data) => data.Keycard.OnADeskPercent = 100;

        private static void PocketAnywhere(ScenarioData data) => data.Keycard.PocketStaysInTheCardsRoom = false;

        /// <summary>The exit arm's socket taken off the wall, and the cable run past where it was.</summary>
        private static void NoArmSocket(ScenarioData data)
        {
            ulong arm = PrototypeBuilding.ExitArmSocket.Value;
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects);
            things.RemoveAll(thing => thing.ObjectId.Value == arm);
            data.PhysicsObjects = things.ToArray();

            var lines = new List<PowerLineDefinition>();
            foreach (PowerLineDefinition line in data.PowerLines)
            {
                if (line.FromObjectId.Value != arm && line.ToObjectId.Value != arm)
                {
                    lines.Add(line);
                }
            }

            lines.Add(new PowerLineDefinition(new SimulationId(3273UL), new SimulationId(3274UL),
                new LogicalPosition(10500, 16800),
                new LogicalPosition(13200, 16800),
                new LogicalPosition(13200, 10000),
                new LogicalPosition(20200, 10000),
                new LogicalPosition(20200, 1700),
                new LogicalPosition(20400, 1700)));
            data.PowerLines = lines.ToArray();
        }

        private static void Move(ScenarioData data, ulong id, int x, int z)
        {
            for (int i = 0; i < data.PhysicsObjects.Length; i++)
            {
                PhysicsObjectDefinition thing = data.PhysicsObjects[i];
                if (thing.ObjectId.Value != id)
                {
                    continue;
                }

                data.PhysicsObjects[i] = new PhysicsObjectDefinition(thing.ObjectId, thing.Kind, new LogicalPosition(x, z),
                    thing.SizeMillimetres, thing.MassGrams, thing.StartsDormant, thing.InitialFacingDegrees, thing.StartsResting,
                    thing.PartOfObjectId, thing.StartsPinned);
                return;
            }

            Assert.Fail($"There is no thing {id} in the office to move.");
        }

        // ---------------------------------------------------------------- one case: ten seeds

        private static void Report(string layout, Action<ScenarioData> candidate, Player player, int triggerTick)
        {
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(TheOfficeAsset);
            Assert.That(level, Is.Not.Null, $"The office level is not at {TheOfficeAsset}.");

            var report = new StringBuilder();
            report.AppendLine($"The office, {layout}; player: {player}; " +
                              (triggerTick > 0 ? $"Trigger pressed at {triggerTick / Run.TicksPerSecond} s" : "the Director's own timing") +
                              $"; seeds {FirstSeed} to {LastSeed}:");
            report.AppendLine("seed | saved | out | lost | ended (s) | allowed | alarm | tower (archway shut) | stack | sprays | tells begun/caught | hand | the card and the Director | where they died | in the exit arm at the first strike");

            int seeds = 0;
            int crowd = 0;
            int savedInAll = 0;
            int cleared = 0;
            int stillGoing = 0;
            var overHalf = new List<string>();
            var seen = new Dictionary<string, int>();
            for (ulong seed = FirstSeed; seed <= LastSeed; seed++)
            {
                ScenarioData data = level.ToRuntimeData();
                if (level.PhysicsFeel != null && level.PhysicsFeel.Feel != null)
                {
                    data.PhysicsFeel = level.PhysicsFeel.Feel.Clone();
                }

                candidate?.Invoke(data);
                Row row = PlayOne(data, seed, player, triggerTick);
                report.AppendLine(row.Line);
                seeds++;
                crowd = row.Crowd;
                savedInAll += row.Saved;
                cleared += row.Saved * 100 >= data.Round.TargetSavedPercent * row.Crowd ? 1 : 0;
                stillGoing += row.StillGoing ? 1 : 0;
                if (row.Saved * 2 > row.Crowd)
                {
                    overHalf.Add($"{seed} ({row.Saved})");
                }

                foreach (string what in row.Seen)
                {
                    seen[what] = seen.TryGetValue(what, out int count) ? count + 1 : 1;
                }
            }

            report.AppendLine($"SUMMARY {layout} / {player} / {(triggerTick > 0 ? "pressed" : "Director's own")}: " +
                              $"{(double)savedInAll / seeds:0.0} of {crowd} saved on average ({100.0 * savedInAll / (seeds * crowd):0}%); " +
                              $"{cleared} of {seeds} cleared the bar; over half on {overHalf.Count}" +
                              (overHalf.Count == 0 ? "" : $": {string.Join(", ", overHalf)}") +
                              $"; still going at the cap on {stillGoing}.");
            var shown = new List<string>();
            foreach (string what in SetPieces)
            {
                shown.Add($"{what} {(seen.TryGetValue(what, out int count) ? count : 0)}");
            }

            report.AppendLine($"SEEN in how many of {seeds}: {string.Join("; ", shown)}.");
            TestContext.WriteLine(report.ToString());
        }

        private static readonly string[] SetPieces =
        {
            "alarm pulled", "tower fell", "archway shut 5 s", "stack fell", "card taken off a desk", "way out opened",
            "bottle sprayed", "fire put out", "tell begun", "building struck"
        };

        private struct Row
        {
            public string Line;
            public int Saved;
            public int Crowd;
            public bool StillGoing;
            public List<string> Seen;
        }

        // ---------------------------------------------------------------- one seed

        private static Row PlayOne(ScenarioData data, ulong seed, Player player, int triggerTick)
        {
            using (var simulation = new Run(data, seed))
            {
                if (triggerTick > 0)
                {
                    simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), triggerTick);
                }

                int archway = -1;
                int wayOut = -1;
                for (int d = 0; d < simulation.DoorCount; d++)
                {
                    if (simulation.GetDoor(d).DoorId == TheBuilding.Archway) archway = d;
                    if (simulation.GetDoor(d).DoorId == TheBuilding.TheWayOut) wayOut = d;
                }

                var hand = new ScriptedHand(simulation, player, archway, wayOut);
                int piledTicks = 0;
                int ended = -1;
                int pushesSeen = 0;
                int inTheArmAtTheStrike = -1;
                for (int tick = 0; tick < CapTicks; tick++)
                {
                    simulation.Step();
                    if (inTheArmAtTheStrike < 0)
                    {
                        IReadOnlyList<CausalEvent> log = simulation.EventLog.Events;
                        for (; pushesSeen < log.Count; pushesSeen++)
                        {
                            if (log[pushesSeen].EventType == CausalEventType.DirectorPushed)
                            {
                                inTheArmAtTheStrike = InTheExitArm(simulation);
                                break;
                            }
                        }
                    }

                    if (archway >= 0 && simulation.GetDoor(archway).IsPiled)
                    {
                        piledTicks++;
                    }

                    hand.Think();
                    if (simulation.Phase == RoundPhase.Over)
                    {
                        ended = tick;
                        break;
                    }
                }

                RunSnapshot snapshot = simulation.NewSnapshotBuffer();
                simulation.FillSnapshot(snapshot);
                int saved = ended < 0 ? snapshot.CrowdSize - snapshot.LostCount : snapshot.SavedCount;

                int alarm = -1;
                int tower = -1;
                int stack = -1;
                int sprays = 0;
                int putOut = 0;
                bool onADesk = false;
                bool takenOffADesk = false;
                bool opened = false;
                bool struck = false;
                var story = new List<string>();
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    int second = record.Tick / Run.TicksPerSecond;
                    switch (record.EventType)
                    {
                        case CausalEventType.AlarmPulled:
                            alarm = alarm < 0 ? second : alarm;
                            break;
                        case CausalEventType.BoxTowerFell:
                            if (record.HasTarget) tower = tower < 0 ? second : tower;
                            else stack = stack < 0 ? second : stack;
                            break;
                        case CausalEventType.ExtinguisherSprayed:
                            sprays++;
                            break;
                        case CausalEventType.IncidentPutOut:
                            putOut++;
                            break;
                        case CausalEventType.KeycardStarted:
                            onADesk = record.SourceId == record.TargetId;
                            story.Add(onADesk ? "desk" : $"pocket {record.SourceId.Value}");
                            break;
                        case CausalEventType.AgentTookKeycard:
                            takenOffADesk |= onADesk;
                            story.Add($"{record.SourceId.Value} took {second}s");
                            break;
                        case CausalEventType.KeycardDropped:
                            story.Add($"{record.SourceId.Value} dropped {second}s");
                            break;
                        case CausalEventType.DoorUnlockedWithKeycard:
                            opened = true;
                            story.Add($"swiped {second}s");
                            break;
                        case CausalEventType.BoxPileCleared:
                            story.Add($"heap cleared {second}s");
                            break;
                        case CausalEventType.DirectorPushed:
                            struck = true;
                            story.Add($"STRUCK {record.TargetId.Value} {second}s ({record.Strength} on course)");
                            break;
                        case CausalEventType.DoorBrokenDown:
                            if (record.TargetId == TheBuilding.TheWayOut || record.SourceId == TheBuilding.TheWayOut)
                            {
                                opened = true;
                                story.Add($"way out pounded open {second}s");
                            }

                            break;
                    }
                }

                HandTally tally = HandTally.From(simulation.EventLog.Events, simulation.Commands, simulation.Tick);
                var seen = new List<string>();
                if (alarm >= 0) seen.Add("alarm pulled");
                if (tower >= 0) seen.Add("tower fell");
                if (piledTicks >= 5 * Run.TicksPerSecond) seen.Add("archway shut 5 s");
                if (stack >= 0) seen.Add("stack fell");
                if (takenOffADesk) seen.Add("card taken off a desk");
                if (opened) seen.Add("way out opened");
                if (sprays > 0) seen.Add("bottle sprayed");
                if (putOut > 0) seen.Add("fire put out");
                if (tally.Tells > 0) seen.Add("tell begun");
                if (struck) seen.Add("building struck");

                string allowed = simulation.DirectorForTests.CapsForTests ? simulation.DirectorForTests.AllowanceForTests.ToString() : "-";
                string line =
                    $"{seed} | {saved} | {snapshot.EscapedCount} | {snapshot.LostCount} | {(ended < 0 ? "cap" : (ended / Run.TicksPerSecond).ToString())} | {allowed} | " +
                    $"{(alarm < 0 ? "-" : alarm + "s")} | {(tower < 0 ? "-" : tower + "s")} ({piledTicks / Run.TicksPerSecond}s) | {(stack < 0 ? "-" : stack + "s")} | " +
                    $"{sprays} | {tally.Tells}/{tally.Caught} | " +
                    (player == Player.Nobody ? "-" : $"{tally.Presses} presses, {tally.HeldTicks / Run.TicksPerSecond}s held, {tally.Answered} answered") +
                    $" | {string.Join("; ", story)} | {WhereTheyDied(simulation)} | {(inTheArmAtTheStrike < 0 ? "-" : inTheArmAtTheStrike.ToString())}";
                return new Row { Line = line, Saved = saved, Crowd = snapshot.CrowdSize, StillGoing = ended < 0, Seen = seen };
            }
        }

        /// <summary>Everybody still in the run standing in the crossbar north of the junction.</summary>
        private static int InTheExitArm(Run simulation)
        {
            int count = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Participation == AgentParticipation.Participating &&
                    agent.Position.X >= 13000 && agent.Position.X <= 16000 && agent.Position.Z >= 9000)
                {
                    count++;
                }
            }

            return count;
        }

        private static string WhereTheyDied(Run simulation)
        {
            var count = new SortedDictionary<string, int>();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Outcome != AgentTerminalOutcome.Lost)
                {
                    continue;
                }

                string room = RoomName(agent.Position);
                count[room] = count.TryGetValue(room, out int n) ? n + 1 : 1;
            }

            var parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in count)
            {
                parts.Add($"{pair.Key} {pair.Value}");
            }

            return parts.Count == 0 ? "nobody died" : string.Join(", ", parts);
        }

        /// <summary>The office's rooms by eye, for reading a table: the crossbar is told apart as the exit arm, the junction and the south arm.</summary>
        private static string RoomName(LogicalPosition at)
        {
            int x = at.X;
            int z = at.Z;
            if (x >= 16000) return "cubicles";
            if (x >= 13000) return z >= 9000 ? "exit arm" : z >= 6000 ? "junction" : "south arm";
            if (x < -6000) return "maintenance";
            if (z >= 9000) return x < 2000 ? "meeting room" : "cafeteria";
            if (z >= 6000) return "corridor";
            if (x < 6000) return "office";
            if (z < -500) return "stockroom";
            return x < 8000 ? "closet" : "bathroom";
        }

        // ---------------------------------------------------------------- the scripted players

        /// <summary>
        /// One hand, as the player has: a place held, or nothing. It looks
        /// every fifth of a second and presses the tick after, as a click
        /// does; when the bar runs dry and the hand comes off, it presses
        /// again once the place it wants is still the same.
        /// </summary>
        private sealed class ScriptedHand
        {
            private readonly Run simulation;
            private readonly Player player;
            private readonly int archway;
            private readonly int wayOut;
            private readonly int card = -1;
            private readonly List<int> bottles = new List<int>();

            private int eventCursor;
            private int fireStartedTick = -1;
            private LogicalPosition fireStartedAt = new LogicalPosition(-2000, 13000);
            private int crackleUntilTick = -1;
            private LogicalPosition crackleAt;
            private int bangTick = -1;
            private LogicalPosition bangAt;
            private bool camping;

            private string holding = "";
            private int lastPressTick = -1000;

            public ScriptedHand(Run simulation, Player player, int archway, int wayOut)
            {
                this.simulation = simulation;
                this.player = player;
                this.archway = archway;
                this.wayOut = wayOut;
                for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                {
                    PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                    if (thing.ObjectId.Value == KeycardId) card = i;
                    if (thing.Kind == PhysicsObjectKind.Extinguisher && !thing.Dormant) bottles.Add(i);
                }
            }

            public void Think()
            {
                if (player == Player.Nobody)
                {
                    return;
                }

                ReadTheNews();
                int tick = simulation.Tick;
                if (tick % 10 != 0 || simulation.Phase != RoundPhase.Running)
                {
                    return;
                }

                (string Key, PlayerCommandType Command, SimulationId Target, LogicalPosition Point) want = Decide(tick);
                bool handIsOff = simulation.InfluenceForTests.Count == 0;
                if (want.Key == holding && !(want.Key.Length > 0 && handIsOff && tick - lastPressTick >= 50))
                {
                    return;
                }

                if (want.Key.Length == 0)
                {
                    simulation.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), tick + 1);
                }
                else if (want.Target.Value != 0UL)
                {
                    simulation.QueueCommand(want.Command, want.Target, tick + 1);
                }
                else
                {
                    simulation.QueueCommand(want.Command, want.Point, tick + 1);
                }

                holding = want.Key;
                lastPressTick = tick;
            }

            private void ReadTheNews()
            {
                IReadOnlyList<CausalEvent> events = simulation.EventLog.Events;
                for (; eventCursor < events.Count; eventCursor++)
                {
                    CausalEvent record = events[eventCursor];
                    switch (record.EventType)
                    {
                        case CausalEventType.DirectorStartedIncident:
                        case CausalEventType.FireActivated:
                            if (fireStartedTick < 0)
                            {
                                fireStartedTick = record.Tick;
                                fireStartedAt = record.Position;
                            }

                            break;
                        case CausalEventType.SocketCrackling:
                            crackleUntilTick = record.Tick + 275;
                            crackleAt = record.Position;
                            break;
                        case CausalEventType.ObjectExploded:
                            if (crackleUntilTick >= 0 && IntegerMath.Distance(record.Position, crackleAt) < 200)
                            {
                                // What crackled has gone: no more pushing, the fire it lit is the job now.
                                crackleUntilTick = record.Tick;
                                bangTick = record.Tick;
                                bangAt = record.Position;
                            }

                            break;
                    }
                }
            }

            private (string, PlayerCommandType, SimulationId, LogicalPosition) Decide(int tick)
            {
                var nothing = ("", PlayerCommandType.ReleaseInfluence, default(SimulationId), default(LogicalPosition));
                bool wayOutOpen = wayOut >= 0 && simulation.GetDoor(wayOut).State != DoorState.Locked;
                switch (player)
                {
                    case Player.ExitCamper:
                        camping |= FrightenedInTheExitArm() > 0;
                        return camping && !wayOutOpen ? Door(TheBuilding.TheWayOut) : nothing;

                    case Player.BottleOnly:
                    {
                        int bottle = fireStartedTick >= 0 ? NearestFreeBottle(fireStartedAt, int.MaxValue) : -1;
                        return bottle >= 0 ? Thing(simulation.GetPhysicsObject(bottle).ObjectId) : nothing;
                    }

                    case Player.Careful:
                    {
                        if (tick < crackleUntilTick)
                        {
                            return ("push " + crackleAt, PlayerCommandType.RepelSpot, default(SimulationId), crackleAt);
                        }

                        if (bangTick >= 0 && tick - bangTick < 1000)
                        {
                            // The building struck: for twenty seconds, a bottle to the fire it lit, if one is within reach of it.
                            int nearBang = NearestFreeBottle(bangAt, 12000);
                            if (nearBang >= 0)
                            {
                                return Thing(simulation.GetPhysicsObject(nearBang).ObjectId);
                            }
                        }

                        bool cardIsFree = card >= 0 && !simulation.GetPhysicsObject(card).IsHeld && !simulation.GetPhysicsObject(card).Dormant;
                        if (fireStartedTick < 0)
                        {
                            // The calm opening: have somebody pocket the card.
                            return cardIsFree ? Thing(new SimulationId(KeycardId)) : nothing;
                        }

                        if (tick - fireStartedTick < 400)
                        {
                            int bottle = NearestFreeBottle(fireStartedAt, int.MaxValue);
                            if (bottle >= 0)
                            {
                                return Thing(simulation.GetPhysicsObject(bottle).ObjectId);
                            }
                        }

                        if (cardIsFree && !wayOutOpen)
                        {
                            return Thing(new SimulationId(KeycardId));
                        }

                        if (archway >= 0 && simulation.GetDoor(archway).IsPiled)
                        {
                            return Door(TheBuilding.Archway);
                        }

                        return nothing;
                    }

                    default:
                        return nothing;
                }
            }

            private static (string, PlayerCommandType, SimulationId, LogicalPosition) Door(SimulationId door) =>
                ("door " + door.Value, PlayerCommandType.InfluenceDoor, door, default(LogicalPosition));

            private static (string, PlayerCommandType, SimulationId, LogicalPosition) Thing(SimulationId thing) =>
                ("thing " + thing.Value, PlayerCommandType.InfluenceThing, thing, default(LogicalPosition));

            private int NearestFreeBottle(LogicalPosition to, int within)
            {
                int best = -1;
                long bestDistance = within;
                foreach (int index in bottles)
                {
                    PhysicsObjectSnapshot bottle = simulation.GetPhysicsObject(index);
                    if (bottle.IsHeld || bottle.Dormant || bottle.Wrecked)
                    {
                        continue;
                    }

                    long distance = IntegerMath.Distance(bottle.Position, to);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = index;
                    }
                }

                return best;
            }

            private int FrightenedInTheExitArm()
            {
                int count = 0;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot agent = simulation.GetAgent(i);
                    if (agent.Participation == AgentParticipation.Participating && agent.FearState == AgentFearState.Scared &&
                        agent.Position.X >= 13000 && agent.Position.X <= 16000 && agent.Position.Z >= 9000)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
