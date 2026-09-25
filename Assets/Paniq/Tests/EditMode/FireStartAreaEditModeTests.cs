using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Where the danger begins. The fire used to be drawn from one four-metre
    /// patch in the middle of the office, so the seed changed who panicked but
    /// never changed the problem. It is now drawn from one of several preset
    /// areas, one inside each room worth burning.
    /// </summary>
    public sealed class FireStartAreaEditModeTests
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

        /// <summary>Which room the fire starts in on this seed, or -1 if it is nowhere.</summary>
        private int RoomTheFireStartsIn(ulong seed, out LogicalPosition origin)
        {
            ScenarioData data = scenario.ToRuntimeData();
            var simulation = new Run(data, seed);
            origin = simulation.FireOriginForTests;
            for (int r = 0; r < data.Rooms.Length; r++)
            {
                LogicalBounds b = data.Rooms[r].Bounds;
                if (origin.X >= b.MinX && origin.X <= b.MaxX && origin.Z >= b.MinZ && origin.Z <= b.MaxZ)
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>
        /// The point of the change: play twenty different seeds and the fire
        /// does not keep starting in the same room.
        /// </summary>
        [Test]
        public void AcrossManySeeds_TheFireStartsInMoreThanOneRoom()
        {
            var rooms = new HashSet<int>();
            for (ulong seed = 1UL; seed <= 20UL; seed++)
            {
                rooms.Add(RoomTheFireStartsIn(seed, out _));
            }

            Assert.That(rooms, Has.Count.GreaterThan(1),
                "Twenty seeds and the fire started in the same room every time.");
        }

        /// <summary>It always starts somewhere indoors, never in a wall or the street.</summary>
        [Test]
        public void TheFire_AlwaysStartsInsideSomeRoom()
        {
            for (ulong seed = 1UL; seed <= 40UL; seed++)
            {
                int room = RoomTheFireStartsIn(seed, out LogicalPosition origin);
                Assert.That(room, Is.GreaterThanOrEqualTo(0),
                    $"Seed {seed}: the fire started at ({origin.X}, {origin.Z}), which is in no room at all.");
            }
        }

        /// <summary>
        /// Never in the corridor. It is the one route the whole floor shares,
        /// and a fire starting in it would cut the building in half before the
        /// player had touched anything.
        /// </summary>
        [Test]
        public void TheFire_NeverStartsInTheCorridor()
        {
            ScenarioData data = scenario.ToRuntimeData();
            for (ulong seed = 1UL; seed <= 40UL; seed++)
            {
                int room = RoomTheFireStartsIn(seed, out LogicalPosition origin);
                SimulationId id = data.Rooms[room].RoomId;
                Assert.That(id.Value, Is.Not.EqualTo(5003UL).And.Not.EqualTo(5009UL),
                    $"Seed {seed}: the fire started in the corridor at ({origin.X}, {origin.Z}).");
            }
        }

        /// <summary>
        /// A scenario that names one area still draws exactly the two numbers
        /// it always did, so pinning the fire to one spot keeps working and
        /// every test that does so behaves as it did.
        /// </summary>
        [Test]
        public void OneNamedArea_PutsTheFireExactlyThere()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.SpawnBounds = new LogicalBounds(1000, 1000, 1000, 1000);
            var simulation = new Run(data, 7UL);

            // Snapped to the middle of the half-metre square it landed in.
            LogicalPosition origin = simulation.FireOriginForTests;
            Assert.That(origin.X, Is.InRange(750, 1250));
            Assert.That(origin.Z, Is.InRange(750, 1250));
        }

        /// <summary>Setting the one area replaces the list rather than adding to it.</summary>
        [Test]
        public void SettingTheOneArea_ReplacesTheWholeList()
        {
            ScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.Fire.SpawnAreas, Has.Length.GreaterThan(1), "The shipped building has several.");

            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            Assert.That(data.Fire.SpawnAreas, Has.Length.EqualTo(1));
            Assert.That(data.Fire.SpawnBounds.MinX, Is.EqualTo(0));
        }

        /// <summary>
        /// A run works on its own copy: changing one scenario's areas must not
        /// reach into another run that was cloned from the same data.
        /// </summary>
        [Test]
        public void ChangingOneRunsAreas_LeavesAnotherRunsAlone()
        {
            ScenarioData one = scenario.ToRuntimeData();
            ScenarioData other = scenario.ToRuntimeData();
            int before = other.Fire.SpawnAreas.Length;

            one.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);

            Assert.That(other.Fire.SpawnAreas, Has.Length.EqualTo(before),
                "Two runs were handed the same array of spawn areas.");
        }
    }
}
