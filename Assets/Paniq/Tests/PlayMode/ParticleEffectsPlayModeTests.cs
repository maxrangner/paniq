using System.Collections;
using NUnit.Framework;
using Paniq.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paniq.Tests.PlayMode
{
    /// <summary>
    /// The particle effects: a bang looks the same every time its event plays,
    /// nothing is built mid-game, and the budget holds under the worst pile-up.
    /// </summary>
    public sealed class ParticleEffectsPlayModeTests
    {
        private GameObject holder;

        [SetUp]
        public void CreateHolder()
        {
            holder = new GameObject("Particle effects test");
        }

        [TearDown]
        public void DestroyHolder()
        {
            Object.Destroy(holder);
        }

        [Test]
        public void TheSameBangThrowsTheSameSparks()
        {
            var first = new ParticleEffects(null, holder.transform);
            var second = new ParticleEffects(null, holder.transform);
            try
            {
                first.BeginFrame();
                second.BeginFrame();
                first.Bang(new Vector3(2f, 0.5f, 3f), 1f, 424242UL);
                second.Bang(new Vector3(2f, 0.5f, 3f), 1f, 424242UL);

                ParticleSystem.Particle[] a = Sparks(first);
                ParticleSystem.Particle[] b = Sparks(second);
                Assert.That(a.Length, Is.GreaterThan(0), "A bang threw no sparks.");
                Assert.That(b.Length, Is.EqualTo(a.Length));
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.That(b[i].position, Is.EqualTo(a[i].position), $"Spark {i} started somewhere else.");
                    Assert.That(b[i].velocity, Is.EqualTo(a[i].velocity), $"Spark {i} flew another way.");
                    Assert.That(b[i].startSize, Is.EqualTo(a[i].startSize), $"Spark {i} was another size.");
                }

                // Another event's bang scatters differently.
                var third = new ParticleEffects(null, holder.transform);
                try
                {
                    third.BeginFrame();
                    third.Bang(new Vector3(2f, 0.5f, 3f), 1f, 777UL);
                    ParticleSystem.Particle[] c = Sparks(third);
                    Assert.That(c[1].velocity, Is.Not.EqualTo(a[1].velocity));
                }
                finally
                {
                    third.Dispose();
                }
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }

        [Test]
        public void EffectsReuseWhatWasBuiltAtTheStart()
        {
            var effects = new ParticleEffects(null, holder.transform);
            try
            {
                int built = holder.GetComponentsInChildren<Transform>(true).Length;
                effects.BeginFrame();
                effects.Bang(Vector3.up, 2f, 1UL);
                effects.Break(Vector3.up, 0.5f, true, 2UL);
                effects.Knock(Vector3.zero, 1f, 3UL);
                float carry = 0f;
                effects.Flames(Vector3.zero, 0.3f, 1f, 0.1f, 0.5f, ref carry);
                effects.Spray(Vector3.up, Vector3.forward, 0.5f, ref carry);
                effects.BurningFloor(Vector3.zero, 0.2f, 1f, ref carry);

                Assert.That(effects.LiveParticles, Is.GreaterThan(0));
                Assert.That(holder.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(built),
                    "Playing effects built new objects instead of reusing the particle systems.");
            }
            finally
            {
                effects.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator TheBudgetHoldsUnderAStormOfBangs()
        {
            ParticleEffectSettings settings = ParticleEffectSettings.CreateDefaults();
            settings.LiveParticleBudget = 1000;
            var effects = new ParticleEffects(settings, holder.transform);
            try
            {
                for (int frame = 0; frame < 20; frame++)
                {
                    effects.BeginFrame();
                    for (int bang = 0; bang < 10; bang++)
                    {
                        effects.Bang(new Vector3(bang, 1f, frame), 4f, (ulong)(frame * 100 + bang));
                    }

                    float carry = 0f;
                    effects.Spray(Vector3.up, Vector3.forward, 1f, ref carry);
                    Assert.That(effects.LiveParticles, Is.LessThanOrEqualTo(settings.LiveParticleBudget),
                        $"Frame {frame}: more particles alive than the budget allows.");
                    yield return null;
                }
            }
            finally
            {
                effects.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ThePrototypeSceneUsesTheParticleSettingsAsset()
        {
            yield return SceneManager.LoadSceneAsync(Paniq.App.Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            var presentation = Object.FindFirstObjectByType<FireReactionPrototypePresentation>();
            Assert.That(presentation, Is.Not.Null);
            var settings = (ParticleEffectSettings)typeof(FireReactionPrototypePresentation)
                .GetField("particleEffects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(presentation);
            Assert.That(settings, Is.Not.Null, "The scene lost its link to Content/ParticleEffects.asset.");
            Assert.That(settings.LiveParticleBudget, Is.GreaterThan(0));
            Assert.That(settings.Sparks.AmountPercent, Is.GreaterThan(0));
        }

        private static ParticleSystem.Particle[] Sparks(ParticleEffects effects)
        {
            ParticleSystem system = effects.SystemFor(ParticleEffects.Kind.Sparks);
            var particles = new ParticleSystem.Particle[system.particleCount];
            system.GetParticles(particles);
            return particles;
        }
    }
}
