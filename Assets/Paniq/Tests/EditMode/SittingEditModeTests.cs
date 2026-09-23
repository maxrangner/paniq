using System;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Sitting on chairs: a calm person walks over and sits down, the chair
    /// stays put while they are on it, and a fright gets them out of it
    /// before they can run.
    /// <para>
    /// A chair is furniture, not clutter. Tidying used to be offered first and
    /// would take anything liftable, and in an office the nearest liftable
    /// thing is almost always a chair, so the room spent its day carrying its
    /// own chairs around and nobody ever sat on one. See
    /// <see cref="ACalmPersonBesideAChair_SitsOnItRatherThanCarryingItOff"/>.
    /// </para>
    /// </summary>
    public sealed class SittingEditModeTests
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

        /// <summary>Whether this person is sitting on the one chair.</summary>
        private static bool OnTheChair(Run simulation)
        {
            return simulation.GetPhysicsObject(0).OccupiedBy == simulation.GetAgent(0).AgentId;
        }

        /// <summary>One person and one chair alone in the office, with no fire yet.</summary>
        private ScenarioData OnePersonOneChair()
        {
            // A way out of the office that is not locked, so getting up and
            // running from the fire means running away from it and staying
            // away, rather than trying a locked door and turning back.
            ScenarioData data =
                DoorsEditModeTests.WithAWayOutOfTheOffice(scenario.ToRuntimeData(), startsLocked: false);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, 0), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.Tables = new TableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Chair,
                    new LogicalPosition(-1500, 0), 450, 5000)
            };
            data.Fire.ActivationTick = int.MaxValue;

            // They always choose to sit down when they choose afresh.
            data.Items.TidyChancePercent = 0;
            data.Items.SitChancePercent = 100;
            data.Calm.DecisionMinimumTicks = 10;
            data.Calm.DecisionMaximumTicks = 20;
            return data;
        }

        /// <summary>
        /// The whole office as it is really authored -- desks, chairs, boxes,
        /// bags and all -- left alone to get on with its day. Somebody has to
        /// end up in a chair, and nobody should be carting one about.
        /// </summary>
        [Test]
        public void ACalmPersonBesideAChair_SitsOnItRatherThanCarryingItOff()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = true;

            using (var simulation = new Run(data))
            {
                bool anybodySat = false;
                for (int t = 0; t < 120 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot person = simulation.GetAgent(i);
                        anybodySat |= person.ActivityState == AgentActivityState.Sitting;
                        Assert.That(IsCarryingAChair(simulation, person), Is.False,
                            $"Tick {simulation.Tick}: person {i + 1} picked a chair up and walked off with it.");
                    }
                }

                Assert.That(anybodySat, Is.True, "Two minutes in an office and nobody sat down.");
            }
        }

        /// <summary>Whether this person has a chair in their hands.</summary>
        private static bool IsCarryingAChair(Run simulation, AgentSnapshot person)
        {
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                bool isAChair = thing.Kind == PhysicsObjectKind.Chair || thing.Kind == PhysicsObjectKind.OfficeChair;
                if (isAChair && thing.HeldBy == person.AgentId)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void ACalmPerson_WalksToAChairAndSitsOnIt()
        {
            var simulation = new Run(OnePersonOneChair());
            for (int t = 0; t < 20 * Run.TicksPerSecond && !OnTheChair(simulation); t++)
            {
                simulation.Step();
            }

            Assert.That(OnTheChair(simulation), Is.True, "Nobody ever sat down.");
            Assert.That(simulation.GetPhysicsObject(0).IsSatOn, Is.True);
            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting));
            Assert.That(LogicalPosition.DistanceSquared(simulation.GetAgent(0).Position, new LogicalPosition(-1500, 0)),
                Is.LessThan(400L * 400L), "They are on the chair, not beside it.");
        }

        /// <summary>How upright a thing stands: 1 on its feet, 0 on its side, -1 upside down.</summary>
        private static float Uprightness(PhysicsObjectSnapshot thing)
        {
            var turn = new UnityEngine.Quaternion(
                thing.Pose.RotationX / (float)BodyPose.RotationScale, thing.Pose.RotationY / (float)BodyPose.RotationScale,
                thing.Pose.RotationZ / (float)BodyPose.RotationScale, thing.Pose.RotationW / (float)BodyPose.RotationScale);
            return (turn * UnityEngine.Vector3.up).y;
        }

        /// <summary>
        /// Nobody drops onto a seat out of thin air: they stand beside the
        /// chair, pull it out, and lower themselves onto it as it goes back in.
        /// </summary>
        [Test]
        public void SittingDown_PullsTheChairOutAndRidesItBackIn()
        {
            var simulation = new Run(OnePersonOneChair());
            LogicalPosition stood = simulation.GetPhysicsObject(0).Position;
            long furthest = 0L;
            for (int t = 0; t < 20 * Run.TicksPerSecond && !OnTheChair(simulation); t++)
            {
                simulation.Step();
                furthest = Math.Max(furthest,
                    LogicalPosition.DistanceSquared(stood, simulation.GetPhysicsObject(0).Position));
            }

            Assert.That(OnTheChair(simulation), Is.True, "Nobody ever sat down.");
            Assert.That(furthest, Is.GreaterThan(200L * 200L), "The chair was never pulled out.");
            Assert.That(LogicalPosition.DistanceSquared(stood, simulation.GetPhysicsObject(0).Position),
                Is.LessThan(150L * 150L), "The chair should end up back where it stood.");
        }

        /// <summary>
        /// Somebody who leaps out of a chair because the fire is on them sends
        /// it over backwards behind them, rather than tucking it in.
        /// </summary>
        [Test]
        public void LeapingOutOfAChair_SendsItOverBackwards()
        {
            ScenarioData data = OnePersonOneChair();
            data.Fire.ActivationTick = 400;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new Run(data);
            for (int t = 0; t < 8 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            Assert.That(Uprightness(simulation.GetPhysicsObject(0)), Is.GreaterThan(0.9f), "The chair starts on its feet.");
            for (int t = 0; t < 12 * Run.TicksPerSecond &&
                            Uprightness(simulation.GetPhysicsObject(0)) > 0.5f; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.Not.EqualTo(AgentActivityState.Sitting),
                "They should be out of the chair by now.");
            Assert.That(Uprightness(simulation.GetPhysicsObject(0)), Is.LessThan(0.5f),
                "The chair they leapt out of should have gone over.");
        }

        [Test]
        public void AChairWithSomeoneOnIt_StaysPutWhenKicked()
        {
            var simulation = new Run(OnePersonOneChair());
            for (int t = 0; t < 20 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            LogicalPosition before = simulation.GetPhysicsObject(0).Position;
            simulation.LaunchObjectForTests(0, 90, 0);
            for (int t = 0; t < Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(0).Position, Is.EqualTo(before),
                "A chair with someone sitting on it should not slide.");
        }

        /// <summary>
        /// Getting out of a chair in a fright leaves somebody standing where
        /// they sat. They used to be shoved 800 mm straight backwards from the
        /// way they were facing in a single tick, and because the view slides a
        /// body smoothly between one tick's position and the next without
        /// turning it, six people round a meeting table all appeared to float
        /// backwards into the walls the moment the alarm went.
        /// </summary>
        [Test]
        public void LeavingAChairInAFright_LeavesThemStandingWhereTheySat()
        {
            ScenarioData data = OnePersonOneChair();
            data.Fire.ActivationTick = 400;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new Run(data);
            for (int t = 0; t < 8 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            while (simulation.Tick < data.Fire.ActivationTick)
            {
                simulation.Step();
            }

            LogicalPosition seat = simulation.GetAgent(0).Position;
            LogicalPosition previous = seat;
            long longestStep = 0L;
            for (int t = 0; t < 5 * Run.TicksPerSecond && OnTheChair(simulation); t++)
            {
                simulation.Step();
                LogicalPosition now = simulation.GetAgent(0).Position;
                longestStep = Math.Max(longestStep, IntegerMath.Distance(previous, now));
                previous = now;
            }

            Assert.That(OnTheChair(simulation), Is.False, "They never got out of the chair.");
            Assert.That(longestStep, Is.LessThan(200L),
                "Nobody crosses 200 mm of floor in a fiftieth of a second. A jump that big is a teleport, not a step.");
            Assert.That(IntegerMath.Distance(seat, simulation.GetAgent(0).Position), Is.LessThan(400L),
                "They should come up out of the seat where they sat, not a stride behind it.");
        }

        [Test]
        public void SomeoneSittingWhenTheFireStarts_GetsUpBeforeTheyRun()
        {
            ScenarioData data = OnePersonOneChair();

            // The fire breaks out right in front of them, well after they sit.
            data.Fire.ActivationTick = 400;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new Run(data);
            for (int t = 0; t < 8 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            while (simulation.Tick < data.Fire.ActivationTick)
            {
                simulation.Step();
            }

            int satStill = 0;
            for (int t = 0; t < 5 * Run.TicksPerSecond && OnTheChair(simulation); t++)
            {
                simulation.Step();
                satStill += simulation.GetAgent(0).SpeedMillimetresPerTick == 0 ? 1 : 0;
            }

            Assert.That(OnTheChair(simulation), Is.False, "They never got out of the chair.");
            Assert.That(satStill, Is.GreaterThan(5), "Getting out of a chair should cost them a moment.");

            for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            AgentSnapshot person = simulation.GetAgent(0);
            Assert.That(person.FearState, Is.EqualTo(AgentFearState.Scared));
            Assert.That(person.Position.X, Is.LessThan(-1500), "Once up, they run away from the flames.");
            Assert.That(simulation.GetPhysicsObject(0).IsSatOn, Is.False, "The chair is free again.");
        }
    }
}
