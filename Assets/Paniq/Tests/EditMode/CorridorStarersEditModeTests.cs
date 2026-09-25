using System;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Nobody frightened stands about staring at a wall. On seed 41 the owner
    /// watched people come out of the meeting room into the corridor and
    /// stand there, still, nose to the wall, doing nothing. Whatever the
    /// cause, that is what the player sees, so that is what this watches
    /// for: anybody frightened and on their feet who has not moved for ten
    /// seconds together while doing nothing that keeps a person still on
    /// purpose -- trying a door, pulling an alarm, shaking somebody awake,
    /// or standing frozen, which is a fright of its own and is drawn as one.
    /// </summary>
    public sealed class CorridorStarersEditModeTests
    {
        /// <summary>How long somebody may stand still before it is a bug rather than a moment.</summary>
        private const int StillSeconds = 10;

        /// <summary>Less than this in a second is standing still, in millimetres; a crowd's jostling moves people more.</summary>
        private const int StandingStillMillimetres = 60;

        /// <summary>How near a wall counts as nose to it, in millimetres.</summary>
        private const int NoseToTheWallMillimetres = 400;

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

        /// <summary>
        /// The shipped floor as the owner plays it: the round waits for the
        /// trigger, which comes early or after the meeting has broken up on
        /// its own, and the way out is either left locked or clicked open
        /// twelve seconds after the trigger, the way a player would.
        /// </summary>
        [TestCase(41UL, 300, false)]
        [TestCase(41UL, 300, true)]
        [TestCase(41UL, 3300, false)]
        [TestCase(41UL, 3300, true)]
        [TestCase(42UL, 300, false)]
        [TestCase(42UL, 300, true)]
        [TestCase(42UL, 3300, true)]
        public void NobodyFrightened_StandsStaringAtAWall(ulong seed, int triggerTick, bool wayOutOpened)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Round.HazardWaitsForTrigger = true;
            LogicalBounds corridor = Array.Find(data.Rooms, r => r.RoomId == PrototypeBuilding.Corridor).Bounds;
            using (var simulation = new Run(data, seed))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), triggerTick);
                if (wayOutOpened)
                {
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, DoorsEditModeTests.TheWayOut, triggerTick + 600);
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, DoorsEditModeTests.TheWayOut, triggerTick + 601);
                }

                var wasAt = new LogicalPosition[simulation.AgentCount];
                var stillSeconds = new int[simulation.AgentCount];
                var report = new StringBuilder();
                int until = triggerTick + 150 * Run.TicksPerSecond;
                for (int t = 0; t < until; t++)
                {
                    simulation.Step();
                    if (simulation.Tick % Run.TicksPerSecond != 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot person = simulation.GetAgent(i);
                        bool moved = IntegerMath.Distance(wasAt[i], person.Position) >= StandingStillMillimetres;
                        wasAt[i] = person.Position;
                        bool frightenedAndIdle = person.Participation == AgentParticipation.Participating &&
                                                 person.FearState != AgentFearState.Calm && !person.IsDown &&
                                                 !StillOnPurpose(person.ActivityState);
                        stillSeconds[i] = frightenedAndIdle && !moved ? stillSeconds[i] + 1 : 0;
                        if (stillSeconds[i] == StillSeconds)
                        {
                            string where = StrictlyInside(corridor, person.Position)
                                ? FacingAWall(corridor, person) ? "in the corridor, nose to the wall" : "in the corridor"
                                : "elsewhere";
                            report.AppendLine($"(still for ten seconds {where}, tick {simulation.Tick}) {simulation.DescribeForTests(i)}");
                        }
                    }
                }

                Assert.That(report.Length, Is.Zero,
                    $"Seed {seed}, trigger at {triggerTick}, way out {(wayOutOpened ? "opened" : "locked")}: somebody frightened stood doing nothing:\n{report}");
            }
        }

        /// <summary>The things that keep a frightened person still on purpose.</summary>
        private static bool StillOnPurpose(AgentActivityState activity)
        {
            switch (activity)
            {
                case AgentActivityState.Frozen:
                case AgentActivityState.OpeningDoor:
                case AgentActivityState.TryingDoor:
                case AgentActivityState.ForcingDoor:
                case AgentActivityState.Sitting:
                case AgentActivityState.StandingUp:
                case AgentActivityState.PullingAlarm:
                case AgentActivityState.ShakingAwake:
                case AgentActivityState.Grabbing:
                case AgentActivityState.PickingUp:
                case AgentActivityState.SettingDown:
                case AgentActivityState.Spraying:
                    return true;
                default:
                    return false;
            }
        }

        private static bool StrictlyInside(LogicalBounds b, LogicalPosition p) =>
            p.X > b.MinX && p.X < b.MaxX && p.Z > b.MinZ && p.Z < b.MaxZ;

        /// <summary>Within arm's reach of one of the room's walls and looking at it.</summary>
        private static bool FacingAWall(LogicalBounds room, AgentSnapshot person)
        {
            LogicalPosition looking = IntegerMath.Direction(person.HeadingDegrees);
            long squarely = IntegerMath.TrigScale * 7 / 10;
            LogicalPosition at = person.Position;
            return (room.MaxZ - at.Z <= NoseToTheWallMillimetres && looking.Z > squarely) ||
                   (at.Z - room.MinZ <= NoseToTheWallMillimetres && looking.Z < -squarely) ||
                   (room.MaxX - at.X <= NoseToTheWallMillimetres && looking.X > squarely) ||
                   (at.X - room.MinX <= NoseToTheWallMillimetres && looking.X < -squarely);
        }
    }
}
