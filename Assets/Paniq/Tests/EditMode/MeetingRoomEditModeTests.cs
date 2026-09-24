using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The meeting that is already under way when the fire starts: people who
    /// begin the run in a chair, chairs that face the table they are pulled up
    /// to, and the things standing on that table.
    /// </summary>
    public sealed class MeetingRoomEditModeTests
    {
        private ScenarioAsset scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = ScenarioAsset.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private ScenarioData DefaultData() => scenario.ToRuntimeData();

        /// <summary>Whether a point is inside any table top.</summary>
        private static bool OnATable(RunSnapshot snapshot, LogicalPosition point, int radius)
        {
            foreach (TableSnapshot table in snapshot.Tables)
            {
                if (table.Bounds.ContainsCircle(point, radius))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>North of the corridor: the meeting room and the cafeteria.</summary>
        private static bool AcrossTheCorridor(LogicalPosition where) => where.Z > 9000;

        [Test]
        public void EightPeople_StartTheRunSeated_SixOfThemInTheMeeting()
        {
            var simulation = new Run(DefaultData());
            RunSnapshot snapshot = simulation.GetSnapshot();

            int seated = 0;
            int seatedInTheMeetingRoom = 0;
            foreach (AgentSnapshot person in snapshot.Agents)
            {
                if (person.ActivityState != AgentActivityState.Sitting)
                {
                    continue;
                }

                seated++;
                seatedInTheMeetingRoom += AcrossTheCorridor(person.Position) && person.Position.X < 2000 ? 1 : 0;
            }

            Assert.That(seated, Is.EqualTo(8),
                "Six round the meeting table and two at a cafeteria table, before anything happens.");
            Assert.That(seatedInTheMeetingRoom, Is.EqualTo(6), "Six of them are in the meeting.");
        }

        [Test]
        public void EverySeatedPerson_FacesTheTableTheyAreSittingAt()
        {
            var simulation = new Run(DefaultData());
            RunSnapshot snapshot = simulation.GetSnapshot();

            foreach (AgentSnapshot person in snapshot.Agents)
            {
                if (person.ActivityState != AgentActivityState.Sitting)
                {
                    continue;
                }

                // Half a metre in front of them is table: a desk is only 0.7 m
                // deep, so a longer reach would overshoot it altogether.
                LogicalPosition ahead = person.Position + IntegerMath.Displacement(person.HeadingDegrees, 500);
                Assert.That(OnATable(snapshot, ahead, 0), Is.True,
                    $"Person {person.AgentId} is sitting but not looking at a table.");
            }
        }

        /// <summary>
        /// The whole meeting gets up without anybody gliding backwards. Each of
        /// the six used to be shoved 800 mm straight back from the way they
        /// faced in the single tick they stopped being seated, and since three
        /// of them face north and three face south, the table emptied outwards
        /// into both walls at once.
        /// </summary>
        [Test]
        public void WhenTheMeetingIsStartled_NobodyGlidesBackwardsOutOfTheirChair()
        {
            // The fire breaks out in the meeting room itself, in the corner away
            // from the table, so the six are startled for certain. On the
            // shipped seed it starts in the bathroom, and in half a minute the
            // news never reached the meeting: this test used to watch six
            // people sit still and pass.
            ScenarioData data = DefaultData();
            data.Round.HazardWaitsForTrigger = false;
            data.Fire.ActivationTick = 50;
            data.Fire.SpawnBounds = new LogicalBounds(-5000, -5000, 16000, 16000);
            var simulation = new Run(data);
            var wasAt = new LogicalPosition[simulation.AgentCount];
            var seated = new bool[simulation.AgentCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot person = simulation.GetAgent(i);
                wasAt[i] = person.Position;
                seated[i] = person.ActivityState == AgentActivityState.Sitting &&
                            AcrossTheCorridor(person.Position) && person.Position.X < 2000;
            }

            Assert.That(System.Array.FindAll(seated, s => s).Length, Is.EqualTo(6), "Six are in the meeting.");

            // Long enough for the fire to break out, the bell to go and every
            // one of them to be up and running.
            var startedRisingAt = new int[simulation.AgentCount];
            for (int t = 0; t < 30 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    if (!seated[i])
                    {
                        continue;
                    }

                    AgentSnapshot person = simulation.GetAgent(i);
                    if (startedRisingAt[i] == 0 && person.SeatedPercent < 100)
                    {
                        startedRisingAt[i] = simulation.Tick;
                    }

                    LogicalPosition now = person.Position;
                    long step = IntegerMath.Distance(wasAt[i], now);
                    Assert.That(step, Is.LessThan(200L),
                        $"Person {person.AgentId} crossed {step} mm in one tick: that is a teleport, not a step.");
                    wasAt[i] = now;
                }
            }

            // Startled by the same bell, they come up out of their chairs one
            // after another, a few ticks apart, never all on one tick: the
            // owner's rule that nothing happens to a whole group on the same
            // tick. It is what the owner asked for first, watching this room.
            var risingTicks = new System.Collections.Generic.HashSet<int>();
            int rose = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (!seated[i] || startedRisingAt[i] == 0)
                {
                    // Not everybody is startled inside half a minute -- the
                    // bell may not have been rung, and somebody who freezes
                    // for good may freeze in their chair and stay rooted.
                    continue;
                }

                Assert.That(risingTicks.Add(startedRisingAt[i]), Is.True,
                    $"Two people started up out of their chairs on tick {startedRisingAt[i]}: startled people should come up a few ticks apart.");
                rose++;
            }

            Assert.That(rose, Is.GreaterThanOrEqualTo(4), "Most of the meeting gets up and runs.");
        }

        /// <summary>
        /// The meeting ends by the timetable at the minute mark, with nothing
        /// having happened, and everybody gets up. This is the case the fright
        /// fix above never covered: getting up on purpose worked its step-out
        /// spot out afresh every tick from wherever the body had got to, so
        /// the spot ran away from the body and all six slid backwards into
        /// the walls, faster and faster, and were then put half a metre
        /// further on in the tick they stood. Seed 42, about tick 3000.
        /// </summary>
        [Test]
        public void WhenTheMeetingEndsOnItsOwn_EverybodyStepsBesideTheirChair()
        {
            ScenarioData data = DefaultData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = true;
            Assert.That(data.Timetable.Length, Is.EqualTo(1), "The office's day holds one thing: the meeting ends.");
            ScheduledCue meetingEnds = data.Timetable[0];
            Assert.That(meetingEnds.Kind, Is.EqualTo(CueKind.MeetingEnds));
            var simulation = new Run(data);
            var wasAt = new LogicalPosition[simulation.AgentCount];
            var seat = new LogicalPosition[simulation.AgentCount];
            var seated = new bool[simulation.AgentCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot person = simulation.GetAgent(i);
                wasAt[i] = person.Position;
                seat[i] = person.Position;
                seated[i] = person.ActivityState == AgentActivityState.Sitting &&
                            AcrossTheCorridor(person.Position) && person.Position.X < 2000;
            }

            Assert.That(System.Array.FindAll(seated, s => s).Length, Is.EqualTo(6), "Six are in the meeting.");

            // Through the end of the meeting, everybody's own lag and their
            // own share of the spread beyond it, and the moment it takes them
            // to get up, then a little longer.
            int until = meetingEnds.AtTick + meetingEnds.SpreadTicks + data.Perception.ReactionLagMaximumTicks +
                        data.Items.SitPullTicks + data.Items.SitLowerTicks + 25;
            var roseAt = new int[simulation.AgentCount];
            for (int t = 0; t < until; t++)
            {
                simulation.Step();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    if (!seated[i])
                    {
                        continue;
                    }

                    AgentSnapshot person = simulation.GetAgent(i);
                    if (roseAt[i] == 0 && person.ActivityState != AgentActivityState.Sitting)
                    {
                        roseAt[i] = simulation.Tick;
                    }

                    // Once they are on their feet, and before they have wandered
                    // off: a step beside the chair, not a slide across the room.
                    if (roseAt[i] > 0 && simulation.Tick == roseAt[i] + data.Items.SitPullTicks + data.Items.SitLowerTicks + 5)
                    {
                        Assert.That(IntegerMath.Distance(seat[i], person.Position), Is.LessThan(1200L),
                            $"Person {person.AgentId} got up and ended {IntegerMath.Distance(seat[i], person.Position)} mm from their seat: a step beside the chair, not a slide across the room.");
                    }

                    LogicalPosition now = person.Position;
                    long step = IntegerMath.Distance(wasAt[i], now);
                    Assert.That(step, Is.LessThan(200L),
                        $"Tick {simulation.Tick}: person {person.AgentId} crossed {step} mm in one tick: that is a teleport, not a step.");
                    wasAt[i] = now;
                }
            }

            // The meeting breaks up one person at a time. All six used to rise
            // on tick 3000 exactly, in unison, which reads as clockwork rather
            // than people; the owner's rule is that nothing happens to a whole
            // group on the same tick.
            var risingTicks = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (seated[i])
                {
                    Assert.That(roseAt[i], Is.GreaterThan(0), $"Person {simulation.GetAgent(i).AgentId} never got up.");
                    Assert.That(risingTicks.Add(roseAt[i]), Is.True,
                        $"Two people got up on tick {roseAt[i]}: the meeting should break up one person at a time.");
                }
            }

            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (!seated[i])
                {
                    continue;
                }

                AgentSnapshot person = simulation.GetAgent(i);
                Assert.That(person.ActivityState, Is.Not.EqualTo(AgentActivityState.Sitting),
                    $"Person {person.AgentId} is still sitting after the meeting ended.");
            }

            // The host ends it: the person at the table with the most
            // leadership is the first on their feet, and the log says so.
            int host = -1;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (seated[i] && (host < 0 || simulation.GetAgent(i).Traits.Leadership > simulation.GetAgent(host).Traits.Leadership))
                {
                    host = i;
                }
            }

            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (seated[i] && i != host)
                {
                    Assert.That(roseAt[host], Is.LessThan(roseAt[i]),
                        $"The host (person {simulation.GetAgent(host).AgentId}) should be up before person {simulation.GetAgent(i).AgentId}.");
                }
            }

            bool ended = false;
            foreach (CausalEvent record in simulation.GetSnapshot().Events)
            {
                if (record.EventType == CausalEventType.CueCalled && (CueKind)record.Strength == CueKind.MeetingEnds)
                {
                    Assert.That(record.SourceId, Is.EqualTo(simulation.GetAgent(host).AgentId), "The host is written down as ending it.");
                    Assert.That(record.Tick, Is.EqualTo(meetingEnds.AtTick), "On the tick the timetable says.");
                    ended = true;
                }
            }

            Assert.That(ended, Is.True, "The meeting ending is a line in the story.");
        }

        [Test]
        public void EveryChair_FacesATableAndEveryLaptop_StandsOnOne()
        {
            var simulation = new Run(DefaultData());
            RunSnapshot snapshot = simulation.GetSnapshot();

            int chairs = 0;
            int laptops = 0;
            foreach (PhysicsObjectSnapshot thing in snapshot.PhysicsObjects)
            {
                if (thing.Kind == PhysicsObjectKind.Chair || thing.Kind == PhysicsObjectKind.OfficeChair)
                {
                    chairs++;
                    LogicalPosition ahead = thing.Position + IntegerMath.Displacement(thing.HeadingDegrees, 500);
                    Assert.That(OnATable(snapshot, ahead, 0), Is.True,
                        $"Chair {thing.ObjectId} is not pulled up to a table.");
                }

                if (thing.Kind == PhysicsObjectKind.Laptop)
                {
                    laptops++;
                    Assert.That(thing.Resting, Is.True, $"Laptop {thing.ObjectId} is not standing on anything.");
                    Assert.That(OnATable(snapshot, thing.Position, thing.SizeMillimetres / 2), Is.True,
                        $"Laptop {thing.ObjectId} is not on a table.");
                }
            }

            Assert.That(chairs, Is.EqualTo(16),
                "Eight at the office desks, six at the meeting table and two in the cafeteria.");
            Assert.That(laptops, Is.GreaterThan(0));
        }

        [Test]
        public void ALaptopOnADesk_IsInNobodysWayButBlocksOnceItIsOnTheFloor()
        {
            ScenarioData data = DefaultData();
            data.Fire.ActivationTick = int.MaxValue;
            var simulation = new Run(data);

            int laptop = -1;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).Kind == PhysicsObjectKind.Laptop)
                {
                    laptop = i;
                    break;
                }
            }

            Assert.That(laptop, Is.GreaterThanOrEqualTo(0));
            PhysicsObjectSnapshot before = simulation.GetPhysicsObject(laptop);
            Assert.That(before.Resting, Is.True, "It starts on a desk.");
            Assert.That(before.Pose.HeightMillimetres, Is.GreaterThan(700), "Up at desk height.");

            // Knocked hard across the desk: it skids off the edge, drops and
            // lands on the floor clear of the table it stood on.
            simulation.LaunchObjectForTests(laptop, 0, 60);
            simulation.Step();
            Assert.That(simulation.GetPhysicsObject(laptop).Resting, Is.False, "Sent sliding, it is no longer at rest on the desk.");
            for (int t = 0; t < 2 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            PhysicsObjectSnapshot after = simulation.GetPhysicsObject(laptop);
            // Down on the floor, perhaps on its side with the screen propping it
            // up a little, but nowhere near desk height.
            Assert.That(after.Pose.HeightMillimetres, Is.LessThan(300), "It fell to the floor.");
            Assert.That(OnATable(simulation.GetSnapshot(), after.Position, after.SizeMillimetres / 2 - 20), Is.False,
                "A laptop on the floor is never left inside the table it fell off.");

        }
    }
}
