using NUnit.Framework;
using Paniq.Gameplay;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The one thing that outlives a run: which seed to play next, and the
    /// best score ever managed on a level.
    /// </summary>
    public sealed class LevelSessionEditModeTests
    {
        private const string TestLevel = "test-level-for-session-tests";
        private const string OtherLevel = "another-test-level";

        [SetUp]
        public void SetUp()
        {
            LevelSession.ClearRequestedSeed();
            LevelSession.ForgetBest(TestLevel);
            LevelSession.ForgetBest(OtherLevel);
        }

        [TearDown]
        public void TearDown()
        {
            LevelSession.ClearRequestedSeed();
            LevelSession.ForgetBest(TestLevel);
            LevelSession.ForgetBest(OtherLevel);
            PlayerPrefs.Save();
        }

        [Test]
        public void ASeedAskedFor_IsTheSeedTheRunGets()
        {
            LevelDefinition level = LevelDefinition.CreateDefault();
            try
            {
                LevelSession.RequestSeed(1234UL);
                Assert.That(LevelSession.TakeSeedFor(level), Is.EqualTo(1234UL));
                Assert.That(LevelSession.CurrentSeed, Is.EqualTo(1234UL),
                    "The run's seed has to be remembered, or 'play again' cannot repeat it.");
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void WithNoSeedAskedFor_TheLevelsOwnIsUsed()
        {
            LevelDefinition level = LevelDefinition.CreateDefault();
            try
            {
                Assert.That(LevelSession.TakeSeedFor(level), Is.EqualTo(level.DefaultSeed));
                Assert.That(level.DefaultSeed, Is.Not.Zero, "A scenario seed is never zero.");
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void AZeroSeed_IsRefusedRatherThanUsed()
        {
            // Zero is the one value a scenario refuses, so a player typing it
            // must not be able to build a run that will not start.
            LevelSession.RequestSeed(777UL);
            LevelSession.RequestSeed(0UL);
            Assert.That(LevelSession.RequestedSeed, Is.EqualTo(777UL));
        }

        [Test]
        public void TheBestScore_OnlyEverGoesUp()
        {
            Assert.That(LevelSession.BestPercentFor(TestLevel), Is.Zero, "A level never played has no best.");

            Assert.That(LevelSession.RecordResult(TestLevel, 40), Is.True, "The first result is always a best.");
            Assert.That(LevelSession.BestPercentFor(TestLevel), Is.EqualTo(40));

            Assert.That(LevelSession.RecordResult(TestLevel, 25), Is.False, "A worse round must not lower the best.");
            Assert.That(LevelSession.BestPercentFor(TestLevel), Is.EqualTo(40));

            Assert.That(LevelSession.RecordResult(TestLevel, 40), Is.False, "Matching the best is not beating it.");

            Assert.That(LevelSession.RecordResult(TestLevel, 85), Is.True);
            Assert.That(LevelSession.BestPercentFor(TestLevel), Is.EqualTo(85));
        }

        [Test]
        public void EachLevel_KeepsItsOwnBest()
        {
            LevelSession.RecordResult(TestLevel, 80);
            LevelSession.RecordResult(OtherLevel, 30);
            Assert.That(LevelSession.BestPercentFor(TestLevel), Is.EqualTo(80));
            Assert.That(LevelSession.BestPercentFor(OtherLevel), Is.EqualTo(30));
        }

        [Test]
        public void ARandomSeed_IsNeverZeroAndIsNotAlwaysTheSame()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (int i = 0; i < 50; i++)
            {
                ulong seed = LevelSession.RandomSeed();
                Assert.That(seed, Is.Not.Zero, "Zero is the one seed a scenario refuses.");
                seen.Add(seed);
            }

            Assert.That(seen.Count, Is.GreaterThan(1), "The random button has to give different days out.");
        }

        [Test]
        public void ALevel_PutsItsOwnRulesOverTheScenario()
        {
            LevelDefinition level = LevelDefinition.CreateDefault();
            try
            {
                Simulation.ScenarioData data = level.ToRuntimeData();
                Assert.That(data.Round.HazardWaitsForTrigger, Is.True,
                    "A playable level opens calm and waits for the player.");
                Assert.That(data.Round.TargetSavedPercent, Is.EqualTo(level.TargetSavedPercent));
                Assert.That(data.Round.TargetSavedPercent, Is.EqualTo(75), "Three quarters, as agreed with the owner.");
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }
    }
}
