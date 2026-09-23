using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The index of what is standing where has to give the same answers as
    /// reading everything would, because the rules built on top of it were all
    /// written against a plain scan. These tests compare it with that scan
    /// directly, so a fault shows up here, named, rather than as a replay
    /// fingerprint that drifted somewhere in three thousand ticks.
    ///
    /// Three things are checked: the answer holds the same items, it holds them
    /// in the same order, and it keeps describing the truth as things move.
    /// </summary>
    public sealed class SpatialIndexEditModeTests
    {
        private static readonly LogicalBounds Area = new LogicalBounds(-6000, 19000, -6000, 9000);
        private const int CellSize = 1000;

        /// <summary>
        /// Placements chosen to sit exactly where a grid is usually wrong:
        /// on cell boundaries, on the area's own edges, either side of zero,
        /// outside the area altogether, and two things in the same spot.
        /// </summary>
        private static LogicalPosition[] AwkwardPlacements()
        {
            var places = new List<LogicalPosition>();
            for (int x = -7000; x <= 20000; x += 500)
            {
                places.Add(new LogicalPosition(x, 0));
                places.Add(new LogicalPosition(x, x / 3));
            }

            places.Add(new LogicalPosition(Area.MinX, Area.MinZ));
            places.Add(new LogicalPosition(Area.MaxX, Area.MaxZ));
            places.Add(new LogicalPosition(Area.MinX, Area.MaxZ));
            places.Add(new LogicalPosition(Area.MaxX, Area.MinZ));
            places.Add(new LogicalPosition(0, 0));
            places.Add(new LogicalPosition(0, 0));
            places.Add(new LogicalPosition(-1, -1));
            places.Add(new LogicalPosition(1, 1));
            places.Add(new LogicalPosition(-CellSize, -CellSize));
            places.Add(new LogicalPosition(-999, -999));
            places.Add(new LogicalPosition(-200000, -200000));
            places.Add(new LogicalPosition(200000, 200000));
            return places.ToArray();
        }

        [Test]
        public void EveryGather_HoldsEverythingInRangeAndNothingIsMissed()
        {
            LogicalPosition[] places = AwkwardPlacements();
            var index = new UniformGridIndex(Area, CellSize, places.Length);
            for (int i = 0; i < places.Length; i++)
            {
                index.Place(i, places[i]);
            }

            var gathered = new int[places.Length];
            foreach (long radius in new[] { 0L, 1L, 250L, 800L, 1000L, 2500L, 8000L })
            {
                for (int x = -8000; x <= 21000; x += 700)
                {
                    for (int z = -8000; z <= 11000; z += 700)
                    {
                        var at = new LogicalPosition(x, z);
                        LogicalBounds asked = UniformGridIndex.Around(at, radius);
                        int count = index.Gather(asked, gathered);

                        // Everything the index offered must be a real item, and
                        // everything genuinely inside the area must be offered.
                        var offered = new HashSet<int>();
                        for (int i = 0; i < count; i++)
                        {
                            offered.Add(gathered[i]);
                        }

                        for (int i = 0; i < places.Length; i++)
                        {
                            bool inside = places[i].X >= asked.MinX && places[i].X <= asked.MaxX &&
                                          places[i].Z >= asked.MinZ && places[i].Z <= asked.MaxZ;
                            if (inside)
                            {
                                Assert.That(offered, Contains.Item(i),
                                    $"Item {i} at ({places[i].X}, {places[i].Z}) is inside the area asked about " +
                                    $"at ({x}, {z}) radius {radius}, but the index did not offer it.");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryGather_IsInAscendingOrder()
        {
            // Ascending order is not a convenience. The run's randomness is one
            // shared stream, so a rule that draws a number per person would
            // change every later draw in the run if people arrived in another
            // order.
            LogicalPosition[] places = AwkwardPlacements();
            var index = new UniformGridIndex(Area, CellSize, places.Length);
            for (int i = 0; i < places.Length; i++)
            {
                index.Place(i, places[i]);
            }

            var gathered = new int[places.Length];
            for (int x = -8000; x <= 21000; x += 300)
            {
                for (int z = -8000; z <= 11000; z += 300)
                {
                    int count = index.Gather(UniformGridIndex.Around(new LogicalPosition(x, z), 1500L), gathered);
                    for (int i = 1; i < count; i++)
                    {
                        Assert.That(gathered[i], Is.GreaterThan(gathered[i - 1]),
                            $"The answer at ({x}, {z}) was out of order at position {i}.");
                    }
                }
            }
        }

        [Test]
        public void ThingsThatMoveAround_AreStillFoundWhereTheyEndUp()
        {
            // The same generator every run: this walks items through cell
            // boundaries and back out of the area, which is where a stale entry
            // would survive.
            var random = new Pcg32(1234UL, 99UL);
            const int items = 64;
            var index = new UniformGridIndex(Area, CellSize, items);
            var places = new LogicalPosition[items];
            for (int i = 0; i < items; i++)
            {
                places[i] = new LogicalPosition(0, 0);
                index.Place(i, places[i]);
            }

            var gathered = new int[items];
            for (int step = 0; step < 2000; step++)
            {
                int item = random.NextIntInclusive(0, items - 1);
                places[item] = new LogicalPosition(
                    random.NextIntInclusive(-9000, 22000),
                    random.NextIntInclusive(-9000, 12000));
                index.Place(item, places[item]);

                // Everything must be exactly where it was last put, and nowhere else.
                for (int i = 0; i < items; i++)
                {
                    int count = index.Gather(UniformGridIndex.Around(places[i], 0L), gathered);
                    var offered = new HashSet<int>();
                    for (int g = 0; g < count; g++)
                    {
                        offered.Add(gathered[g]);
                    }

                    Assert.That(offered, Contains.Item(i), $"Item {i} went missing after step {step}.");
                }

                // One sweep of the whole grid, so nothing is counted twice:
                // a question reaching past the edge is folded onto the edge
                // cells, which is where things outside the building are kept.
                int total = index.Gather(Area, gathered);
                Assert.That(total, Is.EqualTo(items),
                    $"After step {step} the index held {total} entries for {items} things: " +
                    "something was left behind in the cell it moved out of.");
            }
        }

        [Test]
        public void ARemovedThing_IsNotOfferedAndCanBePutBack()
        {
            var index = new UniformGridIndex(Area, CellSize, 3);
            var where = new LogicalPosition(1500, 1500);
            index.Place(0, where);
            index.Place(1, where);
            index.Place(2, where);

            var gathered = new int[3];
            Assert.That(index.Gather(UniformGridIndex.Around(where, 0L), gathered), Is.EqualTo(3));

            index.Remove(1);
            int count = index.Gather(UniformGridIndex.Around(where, 0L), gathered);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(gathered[0], Is.EqualTo(0));
            Assert.That(gathered[1], Is.EqualTo(2));

            index.Place(1, where);
            count = index.Gather(UniformGridIndex.Around(where, 0L), gathered);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(gathered[1], Is.EqualTo(1), "Putting it back must restore the order, not append it.");
        }

        [Test]
        public void ThroughAWholeRun_TheIndexesKeepDescribingWhereEverythingIs()
        {
            // The guard against somebody later writing a new way to move a
            // person or a box that forgets to tell the index. A stale entry
            // would quietly wrong every question about that patch of floor.
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                var simulation = new Run(scenario.ToRuntimeData());
                Assert.That(simulation.SpatialIndexesAreConsistentForTests, Is.True,
                    "The indexes were wrong before the run even started.");

                for (int tick = 1; tick <= 1500; tick++)
                {
                    simulation.Step();
                    if (tick % 25 == 0)
                    {
                        Assert.That(simulation.SpatialIndexesAreConsistentForTests, Is.True,
                            $"At tick {tick} something had moved without telling the index.");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }
    }
}
