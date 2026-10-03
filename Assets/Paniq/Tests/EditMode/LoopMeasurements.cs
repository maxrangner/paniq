using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The loop level played by machine (2026-10-02, reworked 2026-10-03):
    /// for a layout candidate, the same thirty seeds played four ways -- left
    /// alone, by a careful machine player, by a lazy one (the hand on the way
    /// out, nothing else), and with one pointless poke -- and one block
    /// printed: how the rounds are spread, what the hand was worth and how
    /// sure that is, who lived by the room they began in, by fire spot, how
    /// the round was paced, and which of the plan's gates pass. Not checks:
    /// run on purpose, one case by name, with a long timeout from the bridge
    /// (<c>-TimeoutSeconds 2400</c>). Normal runs skip them.
    /// <para>
    /// The machine players are rough stand-ins, not people: their numbers
    /// say whether a hand can matter by degrees on this floor, and whether
    /// one trick alone wins it, not how the round feels.
    /// </para>
    /// </summary>
    [Explicit, Category("Measure"), Timeout(3600000)]
    public sealed class LoopMeasurements
    {
        private const string TheLoopAsset = "Assets/Paniq/Content/Levels/OfficeLoop.asset";
        private const int CapTicks = 12000;

        /// <summary>Nobody presses Trigger: the Director lights the fire on its own clock, the round a player who never presses gets.</summary>
        private const int DirectorsOwn = -1;

        private const ulong FirstSeed = 40UL;
        private const ulong LastSeed = 69UL;

        // ---------------------------------------------------------------- the cases

        [Test] public void SummaryA_AsAuthored() => Summary("as authored", Loop, DirectorsOwn, FirstSeed, LastSeed);
        [Test] public void SummaryB_HeldOut() => Summary("as authored, held-out seeds", Loop, DirectorsOwn, 70UL, 99UL);
        [Test] public void SummaryC_Pressed() => Summary("as authored, Trigger at 10 s", Loop, 500, FirstSeed, LastSeed);
        [Test] public void SummaryD_Lunchtime() => Summary("six of the cubicles at lunch in the cafeteria", () => Lunchtime(Loop()), DirectorsOwn, FirstSeed, LastSeed);
        [Test] public void Ablation()
        {
            LoopSkill[] each = { LoopSkill.Alarm, LoopSkill.Tells, LoopSkill.WakeFrozen, LoopSkill.Shepherd, LoopSkill.PushOffTheSocket };
            var worth = new List<int>[each.Length];
            for (int k = 0; k < each.Length; k++)
            {
                worth[k] = new List<int>();
            }

            for (ulong seed = FirstSeed; seed < FirstSeed + 20; seed++)
            {
                int alone = Play(Loop(), seed, LoopSkill.None, DirectorsOwn).Saved;
                for (int k = 0; k < each.Length; k++)
                {
                    worth[k].Add(Play(Loop(), seed, each[k], DirectorsOwn).Saved - alone);
                }
            }

            var report = new StringBuilder();
            report.AppendLine("ABLATION, twenty seeds, the worth of each move alone:");
            for (int k = 0; k < each.Length; k++)
            {
                (double mean, double margin) = MeanAndMargin(worth[k]);
                int better = 0, worse = 0;
                foreach (int w in worth[k]) { better += w > 0 ? 1 : 0; worse += w < 0 ? 1 : 0; }
                report.AppendLine($"  {each[k]}: {mean:+0.0;-0.0} ±{margin:0.0}, better {better}, worse {worse}");
            }

            TestContext.WriteLine(report.ToString());
        }

        /// <summary>The T only, left alone against fixed plans of doors held, twenty seeds.</summary>
        [Test] public void PlansAtTheT()
        {
            var plans = new (string Name, (int, ulong, int)[] Plan)[]
            {
                ("stockroom door 30 s", new[] { (0, 2018UL, 30) }),
                ("stockroom door 15 s, then into the cubicles 15 s", new[] { (0, 2018UL, 15), (15, 2022UL, 15) }),
                ("office door from the corridor 10 s, stockroom door 20 s, into the cubicles 15 s", new[] { (0, 2005UL, 10), (10, 2018UL, 20), (30, 2022UL, 15) }),
                ("into the cubicles 30 s", new[] { (0, 2022UL, 30) })
            };
            var report = new StringBuilder();
            report.AppendLine("PLANS AT THE T, twenty seeds:");
            var alone = new List<int>();
            for (ulong seed = FirstSeed; seed < FirstSeed + 20; seed++)
            {
                alone.Add(Play(OnlySpot(0), seed, LoopSkill.None, DirectorsOwn).Saved);
            }

            report.AppendLine("  alone: " + Spread(alone, 34));
            foreach ((string Name, (int, ulong, int)[] Plan) plan in plans)
            {
                var worth = new List<int>();
                for (ulong seed = FirstSeed; seed < FirstSeed + 20; seed++)
                {
                    worth.Add(PlayScripted(OnlySpot(0), seed, plan.Plan) - alone[(int)(seed - FirstSeed)]);
                }

                (double mean, double margin) = MeanAndMargin(worth);
                int better = 0, worse = 0;
                foreach (int w in worth) { better += w > 0 ? 1 : 0; worse += w < 0 ? 1 : 0; }
                report.AppendLine($"  {plan.Name}: {mean:+0.0;-0.0} ±{margin:0.0}, better {better}, worse {worse}");
            }

            TestContext.WriteLine(report.ToString());
        }

        private static ScenarioData OnlySpot(int spot)
        {
            ScenarioData data = Loop();
            data.Director.FireSpots = new[] { data.Director.FireSpots[spot] };
            return data;
        }

        private static int PlayScripted(ScenarioData data, ulong seed, (int, ulong, int)[] plan)
        {
            using (var run = new Run(data, seed))
            {
                var hand = new LoopHand(run, LoopSkill.Scripted) { Plan = plan };
                for (int tick = 0; tick < CapTicks && run.Phase != RoundPhase.Over; tick++)
                {
                    run.Step();
                    hand.Think();
                }

                int saved = 0;
                for (int i = 0; i < run.AgentCount; i++)
                {
                    saved += run.GetAgent(i).Outcome != AgentTerminalOutcome.Lost ? 1 : 0;
                }

                return saved;
            }
        }

        [Test] public void Hunt_Crash()
        {
            foreach (LoopSkill skills in new[] { LoopSkill.None, LoopSkill.Careful, LoopSkill.HoldTheWayOut, LoopSkill.OnePoke })
            {
                for (ulong seed = FirstSeed; seed <= LastSeed; seed++)
                {
                    try
                    {
                        Play(Loop(), seed, skills, DirectorsOwn);
                    }
                    catch (Exception e)
                    {
                        TestContext.WriteLine($"CRASH seed {seed} player {skills}: {e}");
                        return;
                    }
                }
            }
        }

        [Test] public void StoryA_41() => Story("as authored", Loop, 41UL, LoopSkill.None);
        [Test] public void StoryB_41_Careful() => Story("as authored", Loop, 41UL, LoopSkill.Careful);

        // ---------------------------------------------------------------- the level and its candidates

        private static ScenarioData Loop()
        {
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(TheLoopAsset);
            Assert.That(level, Is.Not.Null, $"The loop level is not at {TheLoopAsset}.");
            ScenarioData data = level.ToRuntimeData();
            if (level.PhysicsFeel != null && level.PhysicsFeel.Feel != null)
            {
                data.PhysicsFeel = level.PhysicsFeel.Feel.Clone();
            }

            return data;
        }

        /// <summary>Six of the cubicle landscape's staff begin round the cafeteria's tables and counter instead of at their desks.</summary>
        internal static ScenarioData Lunchtime(ScenarioData data)
        {
            (ulong Id, int X, int Z)[] lunch =
            {
                (1021UL, 7500, 11000), (1022UL, 7500, 13000), (1023UL, 9500, 13800),
                (1026UL, 10500, 11000), (1027UL, 10500, 12800), (1028UL, 6500, 15200)
            };
            for (int i = 0; i < data.Agents.Length; i++)
            {
                foreach ((ulong Id, int X, int Z) move in lunch)
                {
                    if (data.Agents[i].AgentId.Value == move.Id)
                    {
                        data.Agents[i] = data.Agents[i].MovedTo(new LogicalPosition(move.X, move.Z));
                    }
                }
            }

            return data;
        }

        // ---------------------------------------------------------------- one round

        private struct Round
        {
            public int Saved;
            public bool Ended;
            public int FireTick;
            public int FirstDeathTick;
            public int Spot;
            public bool[] Lived;
            public int Clicks;
            public int Presses;
        }

        private static Round Play(ScenarioData data, ulong seed, LoopSkill skills, int triggerTick)
        {
            using (var run = new Run(data, seed))
            {
                if (triggerTick > 0)
                {
                    run.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), triggerTick);
                }

                var hand = new LoopHand(run, skills);
                bool ended = false;
                for (int tick = 0; tick < CapTicks; tick++)
                {
                    run.Step();
                    hand.Think();
                    if (run.Phase == RoundPhase.Over)
                    {
                        ended = true;
                        break;
                    }
                }

                var round = new Round { Ended = ended, FireTick = -1, FirstDeathTick = -1, Spot = -1, Clicks = hand.Clicks, Presses = hand.Presses };
                foreach (CausalEvent record in run.EventLog.Events)
                {
                    if (record.EventType == CausalEventType.DirectorStartedIncident && round.FireTick < 0)
                    {
                        round.FireTick = record.Tick;
                        round.Spot = SpotOf(data, record.Position);
                    }
                    else if (record.EventType == CausalEventType.AgentLost && round.FirstDeathTick < 0)
                    {
                        round.FirstDeathTick = record.Tick;
                    }
                }

                round.Lived = new bool[run.AgentCount];
                for (int i = 0; i < run.AgentCount; i++)
                {
                    round.Lived[i] = run.GetAgent(i).Outcome != AgentTerminalOutcome.Lost;
                    round.Saved += round.Lived[i] ? 1 : 0;
                }

                return round;
            }
        }

        private static int SpotOf(ScenarioData data, LogicalPosition at)
        {
            for (int i = 0; i < data.Director.FireSpots.Length; i++)
            {
                LogicalBounds spot = data.Director.FireSpots[i];
                if (at.X >= spot.MinX && at.X <= spot.MaxX && at.Z >= spot.MinZ && at.Z <= spot.MaxZ)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---------------------------------------------------------------- the summary

        internal static void Summary(string name, Func<ScenarioData> build, int triggerTick, ulong firstSeed, ulong lastSeed)
        {
            ScenarioData sample = build();
            int crowd = sample.Agents.Length;
            int bar = (sample.Round.TargetSavedPercent * crowd + 99) / 100;
            var alone = new List<int>();
            var careful = new List<int>();
            var lazy = new List<int>();
            var carefulWorth = new List<int>();
            var lazyWorth = new List<int>();
            int pokeSteady = 0;
            int better = 0;
            int worse = 0;
            int earlyDeaths = 0;
            int neverEnded = 0;
            int presses = 0;
            int clicks = 0;
            var rooms = new SortedDictionary<ulong, int[]>();
            var spots = new SortedDictionary<int, List<(int Alone, int Careful)>>();
            var lines = new StringBuilder();

            for (ulong seed = firstSeed; seed <= lastSeed; seed++)
            {
                Round a = Play(build(), seed, LoopSkill.None, triggerTick);
                Round c = Play(build(), seed, LoopSkill.Careful, triggerTick);
                Round l = Play(build(), seed, LoopSkill.HoldTheWayOut, triggerTick);
                Round p = Play(build(), seed, LoopSkill.OnePoke, triggerTick);
                alone.Add(a.Saved);
                careful.Add(c.Saved);
                lazy.Add(l.Saved);
                carefulWorth.Add(c.Saved - a.Saved);
                lazyWorth.Add(l.Saved - a.Saved);
                pokeSteady += Math.Abs(p.Saved - a.Saved) <= 2 ? 1 : 0;
                better += c.Saved > a.Saved ? 1 : 0;
                worse += c.Saved < a.Saved ? 1 : 0;
                earlyDeaths += a.FirstDeathTick >= 0 && a.FireTick >= 0 && a.FirstDeathTick - a.FireTick < 15 * Run.TicksPerSecond ? 1 : 0;
                neverEnded += (a.Ended ? 0 : 1) + (c.Ended ? 0 : 1);
                presses += c.Presses;
                clicks += c.Clicks;

                for (int i = 0; i < crowd; i++)
                {
                    ulong room = RoomOf(sample, sample.Agents[i].InitialPosition);
                    if (!rooms.TryGetValue(room, out int[] tally))
                    {
                        rooms[room] = tally = new int[3];
                    }

                    tally[0]++;
                    tally[1] += a.Lived[i] ? 1 : 0;
                    tally[2] += c.Lived[i] ? 1 : 0;
                }

                if (!spots.TryGetValue(a.Spot, out List<(int, int)> bySpot))
                {
                    spots[a.Spot] = bySpot = new List<(int, int)>();
                }

                bySpot.Add((a.Saved, c.Saved));
                lines.AppendLine($"{seed} | spot {a.Spot} | alone {a.Saved} | careful {c.Saved} ({c.Saved - a.Saved:+0;-0;0}) | lazy {l.Saved} | one poke {p.Saved} | " +
                                 $"fire {Seconds(a.FireTick)}, first death {Seconds(a.FirstDeathTick)}{(a.Ended ? "" : " | never ended")}");
            }

            int seeds = alone.Count;
            var report = new StringBuilder();
            report.AppendLine($"CANDIDATE {name} | seeds {firstSeed}-{lastSeed} | fire: {(triggerTick > 0 ? $"Trigger at {triggerTick / Run.TicksPerSecond} s" : "the Director's own")} | bar {bar} of {crowd}");
            report.Append(lines);
            report.AppendLine("LEFT ALONE " + Spread(alone, crowd) + $" | clear the bar: {Count(alone, bar)}/{seeds}");
            report.AppendLine("CAREFUL    " + Spread(careful, crowd) + $" | clear the bar: {Count(careful, bar)}/{seeds}");
            report.AppendLine("LAZY       " + Spread(lazy, crowd));
            (double worth, double margin) = MeanAndMargin(carefulWorth);
            double lazyMean = Mean(lazyWorth);
            report.AppendLine($"WORTH      careful {worth:+0.0;-0.0} ±{margin:0.0} (95%), better {better}, worse {worse} of {seeds} | lazy {lazyMean:+0.0;-0.0} | one poke within 2 on {pokeSteady}/{seeds}");
            var byRoom = new List<string>();
            foreach (KeyValuePair<ulong, int[]> pair in rooms)
            {
                byRoom.Add($"{pair.Key}: {100 * pair.Value[1] / pair.Value[0]}->{100 * pair.Value[2] / pair.Value[0]}% of {pair.Value[0] / seeds}");
            }

            report.AppendLine("BY ROOM    lived, alone->careful: " + string.Join("; ", byRoom));
            var bySpots = new List<string>();
            foreach (KeyValuePair<int, List<(int Alone, int Careful)>> pair in spots)
            {
                double meanAlone = 0;
                double meanCareful = 0;
                foreach ((int Alone, int Careful) round in pair.Value)
                {
                    meanAlone += round.Alone;
                    meanCareful += round.Careful;
                }

                bySpots.Add($"spot {pair.Key} ({pair.Value.Count}): alone {meanAlone / pair.Value.Count:0.0} careful {meanCareful / pair.Value.Count:0.0}");
            }

            report.AppendLine("BY SPOT    " + string.Join("; ", bySpots));
            report.AppendLine($"PACING     first death under 15 s after the fire: {earlyDeaths}/{seeds} | rounds that never ended: {neverEnded} | careful: {presses / seeds} presses, {clicks / seeds} clicks a round");

            double aloneMean = Mean(alone);
            var gates = new List<string>
            {
                Gate("alone 7-10", aloneMean >= 7 && aloneMean <= 10),
                Gate("alone never 0, never over half", Min(alone) >= 1 && Max(alone) * 2 <= crowd),
                Gate("alone spread 4 or less", Deviation(alone) <= 4.0),
                Gate("careful +8, better 25/30, worse 2/30", worth >= 8 && better * 30 >= 25 * seeds && worse * 30 <= 2 * seeds),
                Gate("lazy under half of careful", lazyMean < worth / 2),
                Gate("one poke steady 27/30", pokeSteady * 30 >= 27 * seeds),
                Gate("paced and ending", (seeds - earlyDeaths) * 30 >= 27 * seeds && neverEnded == 0)
            };
            report.AppendLine("GATES      " + string.Join(" ", gates));
            TestContext.WriteLine(report.ToString());
        }

        private static string Gate(string what, bool passed) => $"[{(passed ? "x" : " ")}] {what}";

        private static string Seconds(int tick) => tick < 0 ? "-" : $"{tick / Run.TicksPerSecond}s";

        private static int Count(List<int> values, int atLeast)
        {
            int n = 0;
            foreach (int v in values) n += v >= atLeast ? 1 : 0;
            return n;
        }

        private static int Min(List<int> values)
        {
            int m = int.MaxValue;
            foreach (int v in values) m = Math.Min(m, v);
            return m;
        }

        private static int Max(List<int> values)
        {
            int m = int.MinValue;
            foreach (int v in values) m = Math.Max(m, v);
            return m;
        }

        private static double Mean(List<int> values)
        {
            double sum = 0;
            foreach (int v in values) sum += v;
            return sum / values.Count;
        }

        private static double Deviation(List<int> values)
        {
            double mean = Mean(values);
            double sum = 0;
            foreach (int v in values) sum += (v - mean) * (v - mean);
            return Math.Sqrt(sum / values.Count);
        }

        /// <summary>The mean of paired differences and the half-width of its 95% interval.</summary>
        private static (double Mean, double Margin) MeanAndMargin(List<int> values)
        {
            double mean = Mean(values);
            double sum = 0;
            foreach (int v in values) sum += (v - mean) * (v - mean);
            double sd = values.Count > 1 ? Math.Sqrt(sum / (values.Count - 1)) : 0;
            return (mean, 1.96 * sd / Math.Sqrt(values.Count));
        }

        internal static string Spread(List<int> saved, int crowd)
        {
            var sorted = new List<int>(saved);
            sorted.Sort();
            int Quartile(int percent) => sorted[Math.Min(sorted.Count - 1, sorted.Count * percent / 100)];
            var buckets = new int[5];
            foreach (int value in sorted)
            {
                buckets[Math.Min(4, value * 5 / Math.Max(1, crowd))]++;
            }

            double mean = Mean(saved);
            return $"mean {mean:0.0}/{crowd} ({100.0 * mean / crowd:0}%) dev {Deviation(saved):0.0} | low {sorted[0]} q1 {Quartile(25)} med {Quartile(50)} q3 {Quartile(75)} high {sorted[sorted.Count - 1]} | fifths {buckets[0]}/{buckets[1]}/{buckets[2]}/{buckets[3]}/{buckets[4]}";
        }

        // ---------------------------------------------------------------- one round told person by person

        /// <summary>One seed told person by person: where each began, how it ended for them, when, where, and what set them alight; and what the building and the hand did.</summary>
        internal static void Story(string name, Func<ScenarioData> build, ulong seed, LoopSkill skills)
        {
            ScenarioData data = build();
            var report = new StringBuilder();
            report.AppendLine($"{name}; seed {seed}; player {skills}; the Director's own timing:");
            using (var simulation = new Run(data, seed))
            {
                var began = new LogicalPosition[simulation.AgentCount];
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    began[i] = simulation.GetAgent(i).Position;
                }

                var hand = new LoopHand(simulation, skills);
                for (int tick = 0; tick < CapTicks; tick++)
                {
                    simulation.Step();
                    hand.Think();
                    if (simulation.Phase == RoundPhase.Over)
                    {
                        break;
                    }
                }

                var byId = new Dictionary<ulong, CausalEvent>();
                var firstFireInRoom = new HashSet<ulong>();
                var ends = new Dictionary<ulong, CausalEvent>();
                var scared = new Dictionary<ulong, int>();
                bool bells = false;
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    byId[record.EventId] = record;
                    string at = $"{record.Tick / 50.0:0.0}s";
                    switch (record.EventType)
                    {
                        case CausalEventType.DirectorStartedIncident:
                        case CausalEventType.IncidentPutOut:
                        case CausalEventType.FireEscapedItsRoom:
                        case CausalEventType.SocketCrackling:
                        case CausalEventType.ObjectExploded:
                        case CausalEventType.AlarmPulled:
                        case CausalEventType.DoorBurntThrough:
                        case CausalEventType.AgentPokedAwake:
                        case CausalEventType.RoundEnded:
                            report.AppendLine($"  {at} {record.EventType} {record.SourceId.Value} at {record.Position} in room {RoomOf(data, record.Position)}");
                            break;
                        case CausalEventType.AlarmRang:
                            if (!bells)
                            {
                                bells = true;
                                report.AppendLine($"  {at} the bells ring");
                            }

                            break;
                        case CausalEventType.FireSpread:
                            if (firstFireInRoom.Add(RoomOf(data, record.Position)))
                            {
                                report.AppendLine($"  {at} fire reaches room {RoomOf(data, record.Position)}");
                            }

                            break;
                        case CausalEventType.PowerInfluenced:
                        case CausalEventType.PowerRepelled:
                            report.AppendLine($"  {at} HAND {record.EventType} on {record.TargetId.Value} at {record.Position}");
                            break;
                        case CausalEventType.AgentHidFromTheHeat:
                        case CausalEventType.AgentDashedThroughHeat:
                        case CausalEventType.AgentFroze:
                            report.AppendLine($"  {at} {record.EventType} {record.SourceId.Value} in room {RoomOf(data, record.Position)}");
                            break;
                        case CausalEventType.AgentScared:
                            if (!scared.ContainsKey(record.SourceId.Value)) scared[record.SourceId.Value] = record.Tick / 50;
                            break;
                        case CausalEventType.AgentLost:
                        case CausalEventType.AgentEscaped:
                        case CausalEventType.AgentSurvived:
                            ends[record.SourceId.Value] = record;
                            break;
                        case CausalEventType.AgentRescued:
                            ends[record.TargetId.Value] = record;
                            break;
                    }
                }

                report.AppendLine("person | began in | frightened (s) | ended | when (s) | where | how");
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot agent = simulation.GetAgent(i);
                    ulong id = agent.AgentId.Value;
                    string fright = scared.TryGetValue(id, out int when) ? when.ToString() : "-";
                    if (!ends.TryGetValue(id, out CausalEvent end))
                    {
                        report.AppendLine($"{id} | {RoomOf(data, began[i])} | {fright} | still inside | - | {RoomOf(data, agent.Position)} | -");
                        continue;
                    }

                    string how = "";
                    if (end.EventType == CausalEventType.AgentLost)
                    {
                        var chain = new List<string>();
                        CausalEvent step = end;
                        for (int hop = 0; hop < 3 && step.HasCausalParent && byId.TryGetValue(step.CausalParentEventId, out CausalEvent parent); hop++)
                        {
                            chain.Add($"{parent.EventType}({parent.SourceId.Value})");
                            step = parent;
                        }

                        how = string.Join(" <- ", chain);
                    }

                    report.AppendLine($"{id} | {RoomOf(data, began[i])} | {fright} | {end.EventType} | {end.Tick / Run.TicksPerSecond} | {RoomOf(data, end.Position)} | {how}");
                }
            }

            TestContext.WriteLine(report.ToString());
        }

        internal static ulong RoomOf(ScenarioData data, LogicalPosition at)
        {
            foreach (RoomDefinition candidate in data.Rooms)
            {
                LogicalBounds floor = candidate.Bounds;
                if (at.X >= floor.MinX && at.X <= floor.MaxX && at.Z >= floor.MinZ && at.Z <= floor.MaxZ)
                {
                    return candidate.RoomId.Value;
                }
            }

            return 0UL;
        }
    }
}
