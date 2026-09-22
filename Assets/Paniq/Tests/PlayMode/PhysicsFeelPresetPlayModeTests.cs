using System.Collections;
using NUnit.Framework;
using Paniq.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paniq.Tests.PlayMode
{
    /// <summary>
    /// The physics feel presets: both load and are usable, they really differ,
    /// and the prototype level starts with the Cartoon feel.
    /// </summary>
    public sealed class PhysicsFeelPresetPlayModeTests
    {
#if UNITY_EDITOR
        [Test]
        public void BothPresets_AreUsableAndFeelDifferent()
        {
            var cartoon = UnityEditor.AssetDatabase.LoadAssetAtPath<PhysicsFeelPreset>("Assets/Paniq/Content/PhysicsFeel-Cartoon.asset");
            var heavy = UnityEditor.AssetDatabase.LoadAssetAtPath<PhysicsFeelPreset>("Assets/Paniq/Content/PhysicsFeel-Heavy.asset");
            Assert.That(cartoon, Is.Not.Null, "Missing Content/PhysicsFeel-Cartoon.asset.");
            Assert.That(heavy, Is.Not.Null, "Missing Content/PhysicsFeel-Heavy.asset.");
            Assert.That(cartoon.Feel.IsValid(out string cartoonError), Is.True, cartoonError);
            Assert.That(heavy.Feel.IsValid(out string heavyError), Is.True, heavyError);
            Assert.That(heavy.Feel.GravityPercent, Is.LessThan(cartoon.Feel.GravityPercent),
                "Heavy should fall at real gravity, below Cartoon's snappy fall.");
            Assert.That(heavy.Feel.BlastStrengthPercent, Is.LessThan(cartoon.Feel.BlastStrengthPercent));
        }
#endif

        [UnityTest]
        public IEnumerator ThePrototypeLevel_StartsWithTheCartoonFeel()
        {
            yield return SceneManager.LoadSceneAsync(Paniq.App.Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            var runner = Object.FindFirstObjectByType<FireReactionRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.PhysicsFeelName, Is.EqualTo("PhysicsFeel-Cartoon"));
            Assert.That(runner.IsLiveTuned, Is.False, "Live tuning should start switched off.");
            Assert.That(runner.Simulation.Scenario.PhysicsFeel.GravityPercent, Is.EqualTo(150));
        }
    }
}
