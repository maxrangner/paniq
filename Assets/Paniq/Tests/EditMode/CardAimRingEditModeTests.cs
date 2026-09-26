using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The circle drawn on the floor under the pointer, and the promise it
    /// makes. A card is thrown at a patch and catches whoever is standing in
    /// it, and a throw that catches the wrong person is spent -- which is only
    /// a fair rule because the player was shown the patch first.
    /// <para>
    /// So the number the circle reports and the people the card actually
    /// changes are two separately written pieces of code that must agree:
    /// <see cref="CardAimRing.Show"/> counts from the snapshot in the
    /// presentation, and <c>PlayerCommandSystem.PlayTraitCard</c> gathers from
    /// the crowd index in the run. If they ever drift, the game lies about the
    /// one thing it tells the player is their own fault, and nothing else in
    /// the suite would notice.
    /// </para>
    /// </summary>
    public sealed class CardAimRingEditModeTests
    {
        private ScenarioAsset scenario;
        private GameObject parent;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            scenario = ScenarioAsset.CreateDefault();
            parent = new GameObject("Test aim ring parent");
            material = new Material(Shader.Find("Sprites/Default"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(material);
        }

        /// <summary>A quiet office with people exactly where the test puts them, and every card in hand.</summary>
        private ScenarioData QuietRoomWith(params LogicalPosition[] people)
        {
            ScenarioData data =
                TheBuilding.WithThePlayerAbleToAct(TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData()));

            var crowd = new AgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                crowd[i] = new AgentDefinition(new SimulationId((ulong)(1 + i)), people[i],
                    CardinalDirection.North, AgentTraitValues.AllOrdinary);
            }

            data.Agents = crowd;
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        private static List<CausalEvent> EventsOfType(Run simulation, CausalEventType type)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        private int Counted(Run simulation, LogicalPosition at, int radius)
        {
            var ring = new CardAimRing(material, parent.transform);
            ring.Show(at, radius, simulation.GetSnapshot(), 0f);
            return ring.PeopleInside;
        }

        /// <summary>
        /// The whole point of this file: whatever the circle says it will
        /// catch is what the card goes on to catch. Checked over a spread of
        /// aiming points, including ones that cut the crowd in half and ones
        /// that clip its corners, rather than at a single convenient spot.
        /// </summary>
        [Test]
        public void WhatTheCircleSaysItWillCatch_IsWhatTheCardCatches()
        {
            ScenarioData data = QuietRoomWith(
                new LogicalPosition(0, 0),
                new LogicalPosition(1000, 0),
                new LogicalPosition(-1200, 900),
                new LogicalPosition(1200, 1200),
                new LogicalPosition(0, 2600),
                new LogicalPosition(4000, -3000));

            int radius = data.Purse.CardPatchRadiusMillimetres;
            var aimedAt = new[]
            {
                new LogicalPosition(0, 0),
                new LogicalPosition(1200, 1200),
                new LogicalPosition(-1200, 900),
                new LogicalPosition(600, 1500),
                new LogicalPosition(0, 2600),
                new LogicalPosition(4000, -3000),
                new LogicalPosition(2500, 2500),
                new LogicalPosition(-5000, -5000)
            };

            int everCaught = 0;
            foreach (LogicalPosition spot in aimedAt)
            {
                using (var simulation = new Run(data))
                {
                    simulation.Step();
                    int promised = Counted(simulation, spot, radius);

                    simulation.QueueCommand(PlayerCommandType.PlayCourage, spot, simulation.Tick + 1);
                    simulation.Step();
                    simulation.Step();
                    int caught = EventsOfType(simulation, CausalEventType.PowerCourage).Count;

                    Assert.That(caught, Is.EqualTo(promised),
                        $"Aimed at {spot.X},{spot.Z}: the circle promised {promised} and the card caught {caught}.");
                    everCaught += caught;
                }
            }

            Assert.That(everCaught, Is.GreaterThan(0), "These throws are meant to catch somebody somewhere.");
        }

        /// <summary>
        /// Nobody in the circle really means nobody, because that is the one
        /// mistake the game gives back for free and the HUD says so out loud.
        /// </summary>
        [Test]
        public void AnEmptyPatch_CountsNobody()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            using (var simulation = new Run(data))
            {
                simulation.Step();
                Assert.That(Counted(simulation, new LogicalPosition(5000, 5000),
                    data.Purse.CardPatchRadiusMillimetres), Is.Zero);
            }
        }

        /// <summary>
        /// The circle is round. The run turns the corners down by hand because
        /// its spatial index gathers a square, so the drawing has to agree:
        /// 1200 mm on both axes is 1697 mm away, outside a 1500 mm patch but
        /// inside the box around it.
        /// </summary>
        [Test]
        public void ThePatch_IsRoundOnScreenAsWellAsInTheRun()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(1200, 1200));
            using (var simulation = new Run(data))
            {
                simulation.Step();
                Assert.That(Counted(simulation, new LogicalPosition(0, 0), 1500), Is.Zero,
                    "A corner of the square is not inside the circle.");
                Assert.That(Counted(simulation, new LogicalPosition(1200, 1200), 1500), Is.EqualTo(1),
                    "And the same person is caught when the circle is over them.");
            }
        }

        /// <summary>
        /// The dead are not counted, because a card cannot reach them. Without
        /// this the circle would promise somebody it could not deliver, and the
        /// throw would read as a miss the player could not account for.
        /// </summary>
        [Test]
        public void TheDead_AreNotInTheCircle()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, new LogicalPosition(0, 0));
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;

            using (var simulation = new Run(data))
            {
                for (int tick = 0; tick < 2000 && simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Lost; tick++)
                {
                    simulation.Step();
                }

                Assume.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Lost),
                    "The floor is arranged so this person burns.");
                Assert.That(Counted(simulation, simulation.GetAgent(0).Position,
                    data.Purse.CardPatchRadiusMillimetres), Is.Zero);
            }
        }
    }
}
