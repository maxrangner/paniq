using System;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Every particle in the game, drawn by a handful of Unity particle
    /// systems built once when the display starts: one each for sparks, bang
    /// smoke, debris, dust, splinters, shards, flames, fire smoke, embers,
    /// extinguisher foam and mist. Nothing is created mid-game. Each particle
    /// is a small cube, in the chunky style of the rest of the prototype.
    ///
    /// The code places every particle itself: where it starts, how it flies,
    /// how big it is, how it spins. For a one-off effect such as a bang, all
    /// of that comes from the event's ID, so the same bang in a replay throws
    /// the same sparks. The particle systems then only move, fade and bounce
    /// them off the floor.
    ///
    /// A shared budget keeps the worst moment affordable: once half of
    /// <see cref="ParticleEffectSettings.LiveParticleBudget"/> particles are
    /// alive, every new effect makes proportionally fewer, down to none at the
    /// budget itself.
    ///
    /// Presentation only: it lives in the display's own scene, apart from the
    /// simulation's physics, so no particle can ever push anything in a run.
    /// </summary>
    internal sealed class ParticleEffects : IDisposable
    {
        internal enum Kind
        {
            Sparks,
            BangSmoke,
            Debris,
            Dust,
            Splinters,
            Shards,
            Flames,
            FireSmoke,
            Embers,
            Foam,
            Mist
        }

        /// <summary>How one kind of particle behaves once it is thrown: fixed here; its look is in the settings.</summary>
        private readonly struct Behaviour
        {
            public Behaviour(float lifetime, float size, float growth, float gravity, float drag, float bounce,
                float keep, int cap)
            {
                Lifetime = lifetime;
                Size = size;
                Growth = growth;
                Gravity = gravity;
                Drag = drag;
                Bounce = bounce;
                Keep = keep;
                Cap = cap;
            }

            /// <summary>Seconds a particle lives, before the settings' percentage.</summary>
            public float Lifetime { get; }

            /// <summary>Metres across, before the settings' percentage.</summary>
            public float Size { get; }

            /// <summary>How many times bigger it is at the end of its life than at the start (below 1 shrinks).</summary>
            public float Growth { get; }

            /// <summary>How hard it falls, as a share of real gravity; below 0 it rises like smoke.</summary>
            public float Gravity { get; }

            /// <summary>How quickly the air slows it down.</summary>
            public float Drag { get; }

            /// <summary>How much it bounces off the floor, 0..1; below 0 it passes through.</summary>
            public float Bounce { get; }

            /// <summary>How much of its speed it keeps along the floor when it lands, 0..1.</summary>
            public float Keep { get; }

            /// <summary>The most of this kind alive at once.</summary>
            public int Cap { get; }
        }

        private sealed class Stream
        {
            public ParticleSystem System;
            public Behaviour Behaviour;
            public ParticleLook Look;
            public Color ShownStart;
            public Color ShownEnd;
        }

        private readonly ParticleEffectSettings settings;
        private readonly Stream[] streams;
        private readonly Material material;
        private readonly Transform root;
        private ParticleSystem.EmitParams emit;

        /// <summary>Alive now, counting those emitted since the start of this frame.</summary>
        private int live;

        /// <summary>Numbers continuous effects such as flames, so their particles differ from one another.</summary>
        private int sequence;

        public ParticleEffects(ParticleEffectSettings settings, Transform parent)
        {
            this.settings = settings != null ? settings : ParticleEffectSettings.CreateDefaults();
            root = new GameObject("Particle effects (presentation)").transform;
            root.SetParent(parent, false);

            // An unlit, see-through material that takes each particle's own
            // colour; the shader is on the project's always-included list, so
            // it is there in a built game too.
            material = new Material(Shader.Find("Sprites/Default")) { name = "Particles (presentation)" };

            // The floor they bounce on. The display's walls are not solid, so
            // a spark can fly through one; they are small and short-lived.
            var floor = new GameObject("Particle floor").transform;
            floor.SetParent(root, false);

            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            streams = new Stream[Enum.GetValues(typeof(Kind)).Length];
            Add(Kind.Sparks, this.settings.Sparks, new Behaviour(0.7f, 0.06f, 0.4f, 1f, 0.6f, 0.45f, 0.5f, 1500));
            Add(Kind.BangSmoke, this.settings.BangSmoke, new Behaviour(1.4f, 0.45f, 2.4f, -0.05f, 2.5f, -1f, 0f, 600));
            Add(Kind.Debris, this.settings.Debris, new Behaviour(1.8f, 0.08f, 1f, 1f, 0.3f, 0.25f, 0.4f, 800));
            Add(Kind.Dust, this.settings.Dust, new Behaviour(0.8f, 0.16f, 2.2f, -0.02f, 3.5f, -1f, 0f, 800));
            Add(Kind.Splinters, this.settings.Splinters, new Behaviour(1.8f, 0.07f, 1f, 1f, 0.3f, 0.3f, 0.35f, 600));
            Add(Kind.Shards, this.settings.Shards, new Behaviour(1.5f, 0.05f, 1f, 1f, 0.3f, 0.5f, 0.5f, 600));
            Add(Kind.Flames, this.settings.Flames, new Behaviour(0.55f, 0.14f, 0.25f, -0.3f, 1.5f, -1f, 0f, 2500));
            Add(Kind.FireSmoke, this.settings.FireSmoke, new Behaviour(2.4f, 0.3f, 2.8f, -0.08f, 1.2f, -1f, 0f, 2000));
            Add(Kind.Embers, this.settings.Embers, new Behaviour(1.3f, 0.03f, 0.5f, -0.12f, 0.8f, -1f, 0f, 500));
            Add(Kind.Foam, this.settings.Foam, new Behaviour(0.9f, 0.09f, 2f, 0.7f, 0.5f, 0.05f, 0.2f, 1500));
            Add(Kind.Mist, this.settings.Mist, new Behaviour(0.8f, 0.22f, 2.2f, -0.02f, 2.5f, -1f, 0f, 800));

            void Add(Kind kind, ParticleLook look, Behaviour behaviour)
            {
                streams[(int)kind] = new Stream
                {
                    System = Build(kind, behaviour, cube, material, root, floor),
                    Behaviour = behaviour,
                    Look = look
                };
                Recolour(streams[(int)kind]);
            }
        }

        /// <summary>The settings being drawn from, for the Inspector and tests.</summary>
        public ParticleEffectSettings Settings => settings;

        /// <summary>Every particle alive right now, across all effects.</summary>
        public int LiveParticles
        {
            get
            {
                int count = 0;
                foreach (Stream stream in streams)
                {
                    count += stream.System.particleCount;
                }

                return count;
            }
        }

        /// <summary>The particle system that draws one kind, for tests.</summary>
        internal ParticleSystem SystemFor(Kind kind) => streams[(int)kind].System;

        /// <summary>
        /// Call once a frame before any effect: counts what is alive for the
        /// budget, and picks up colours changed in the Inspector.
        /// </summary>
        public void BeginFrame()
        {
            live = LiveParticles;
            foreach (Stream stream in streams)
            {
                if (stream.ShownStart != stream.Look.Start || stream.ShownEnd != stream.Look.End)
                {
                    Recolour(stream);
                }
            }
        }

        /// <summary>
        /// Something going off, <paramref name="size"/> times the size of a
        /// wall socket popping: hot sparks thrown all round that fall and
        /// bounce, dark chips of debris, and a grey puff that swells and
        /// drifts up.
        /// </summary>
        public void Bang(Vector3 origin, float size, ulong seed)
        {
            int salt = Salt(seed);
            float scale = Mathf.Sqrt(Mathf.Max(0.1f, size));

            Stream sparks = streams[(int)Kind.Sparks];
            int count = Share(sparks, 30f * scale);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Hash01(salt, i, 3)) / count * Mathf.PI * 2f;
                float speed = Mathf.Lerp(2f, 4.5f, Hash01(salt, i, 5)) * scale;
                float rise = Mathf.Lerp(1.5f, 4f, Hash01(salt, i, 7)) * scale;
                Emit(sparks, origin, new Vector3(Mathf.Cos(angle) * speed, rise, Mathf.Sin(angle) * speed), salt, i,
                    Mathf.Lerp(0.7f, 1.3f, Hash01(salt, i, 9)), 1f);
            }

            Stream debris = streams[(int)Kind.Debris];
            count = Share(debris, 14f * scale);
            for (int i = 0; i < count; i++)
            {
                float angle = Hash01(salt, i, 13) * Mathf.PI * 2f;
                float speed = Mathf.Lerp(1f, 3f, Hash01(salt, i, 15)) * scale;
                Emit(debris, origin, new Vector3(Mathf.Cos(angle) * speed, Mathf.Lerp(1f, 3f, Hash01(salt, i, 17)),
                        Mathf.Sin(angle) * speed), salt, i + 100,
                    Mathf.Lerp(0.6f, 1.6f, Hash01(salt, i, 19)), Mathf.Lerp(0.8f, 1.1f, Hash01(salt, i, 21)));
            }

            Stream smoke = streams[(int)Kind.BangSmoke];
            count = Share(smoke, 7f * scale);
            for (int i = 0; i < count; i++)
            {
                Vector3 drift = new Vector3(Hash01(salt, i, 23) - 0.5f, Hash01(salt, i, 25) * 0.6f,
                    Hash01(salt, i, 27) - 0.5f) * (1.6f * scale);
                Emit(smoke, origin + drift * 0.15f, drift, salt, i + 200,
                    Mathf.Lerp(0.8f, 1.3f, Hash01(salt, i, 29)) * scale, Mathf.Lerp(0.85f, 1.15f, Hash01(salt, i, 31)));
            }
        }

        /// <summary>
        /// A hard knock: a thing hitting someone or something, or a person
        /// hitting the floor. A low ring of dust, bigger for a harder knock
        /// (<paramref name="strength"/> 0..1).
        /// </summary>
        public void Knock(Vector3 at, float strength, ulong seed)
        {
            int salt = Salt(seed);
            strength = Mathf.Clamp01(strength);
            Stream dust = streams[(int)Kind.Dust];
            int count = Share(dust, Mathf.Lerp(4f, 14f, strength));
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Hash01(salt, i, 3)) / Mathf.Max(1, count) * Mathf.PI * 2f;
                float speed = Mathf.Lerp(0.4f, 1.4f, Hash01(salt, i, 5)) * (0.6f + strength);
                Emit(dust, new Vector3(at.x, Mathf.Max(0.05f, at.y), at.z),
                    new Vector3(Mathf.Cos(angle) * speed, Hash01(salt, i, 7) * 0.4f, Mathf.Sin(angle) * speed),
                    salt, i, Mathf.Lerp(0.7f, 1.3f, Hash01(salt, i, 9)) * (0.7f + 0.6f * strength),
                    Mathf.Lerp(0.85f, 1.1f, Hash01(salt, i, 11)));
            }
        }

        /// <summary>
        /// Something smashed: splinters for furniture, bright shards for an
        /// appliance, and a puff of dust. <paramref name="size"/> is the
        /// thing's width in metres.
        /// </summary>
        public void Break(Vector3 at, float size, bool electrical, ulong seed)
        {
            int salt = Salt(seed);
            Stream pieces = streams[(int)(electrical ? Kind.Shards : Kind.Splinters)];
            float scale = Mathf.Clamp(size / 0.5f, 0.4f, 3f);
            int count = Share(pieces, 16f * scale);
            for (int i = 0; i < count; i++)
            {
                float angle = Hash01(salt, i, 3) * Mathf.PI * 2f;
                float speed = Mathf.Lerp(0.8f, 2.6f, Hash01(salt, i, 5));
                Vector3 start = at + new Vector3(Hash01(salt, i, 7) - 0.5f, Hash01(salt, i, 9) * 0.6f,
                    Hash01(salt, i, 11) - 0.5f) * size;
                Emit(pieces, start, new Vector3(Mathf.Cos(angle) * speed, Mathf.Lerp(1f, 3f, Hash01(salt, i, 13)),
                        Mathf.Sin(angle) * speed), salt, i,
                    Mathf.Lerp(0.6f, 1.5f, Hash01(salt, i, 15)), Mathf.Lerp(0.8f, 1.15f, Hash01(salt, i, 17)));
            }

            Knock(at, 0.7f, seed ^ 0x5bd1e995UL);
        }

        /// <summary>
        /// Flames licking up from a burning thing for one frame: they start
        /// anywhere across <paramref name="halfWidth"/> of <paramref name="bottom"/>
        /// and rise about <paramref name="height"/> metres, straight up
        /// whichever way the thing lies. Now and then a wisp of smoke or an
        /// ember comes off too. <paramref name="carry"/> keeps the fraction of a
        /// particle left over, so a low rate still shows at a high frame rate.
        /// </summary>
        public void Flames(Vector3 bottom, float halfWidth, float height, float size, float deltaTime, ref float carry)
        {
            Stream flames = streams[(int)Kind.Flames];
            float rate = 30f * Mathf.Clamp(halfWidth / 0.2f, 0.5f, 3f);
            int count = Continuous(flames, rate * deltaTime, ref carry);
            float rise = height / flames.Behaviour.Lifetime;
            for (int i = 0; i < count; i++)
            {
                int n = ++sequence;
                Vector3 start = bottom + new Vector3((Hash01(n, 1, 3) * 2f - 1f) * halfWidth, 0f,
                    (Hash01(n, 1, 5) * 2f - 1f) * halfWidth);
                Emit(flames, start, new Vector3(0f, rise * Mathf.Lerp(0.6f, 1.2f, Hash01(n, 1, 7)), 0f), n, 1,
                    size / flames.Behaviour.Size * Mathf.Lerp(0.7f, 1.2f, Hash01(n, 1, 9)), 1f);

                if (Hash01(n, 1, 11) < 0.12f)
                {
                    Stream smoke = streams[(int)Kind.FireSmoke];
                    if (Share(smoke, 1f) > 0)
                    {
                        Emit(smoke, start + Vector3.up * (height * 0.8f), new Vector3(0f, 0.6f, 0f), n, 2,
                            Mathf.Lerp(0.6f, 1f, Hash01(n, 1, 13)), Mathf.Lerp(0.8f, 1.1f, Hash01(n, 1, 15)));
                    }
                }
                else if (Hash01(n, 1, 11) < 0.18f)
                {
                    Stream embers = streams[(int)Kind.Embers];
                    if (Share(embers, 1f) > 0)
                    {
                        Emit(embers, start, new Vector3((Hash01(n, 1, 17) - 0.5f) * 0.8f, Mathf.Lerp(0.8f, 1.8f,
                            Hash01(n, 1, 19)), (Hash01(n, 1, 21) - 0.5f) * 0.8f), n, 3, 1f, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// A burning square of floor, for one frame: a column of dark smoke
        /// rising from anywhere across it, and a few embers.
        /// </summary>
        public void BurningFloor(Vector3 centre, float halfWidth, float deltaTime, ref float carry)
        {
            Stream smoke = streams[(int)Kind.FireSmoke];
            int count = Continuous(smoke, 3f * deltaTime, ref carry);
            for (int i = 0; i < count; i++)
            {
                int n = ++sequence;
                Vector3 start = centre + new Vector3((Hash01(n, 2, 3) * 2f - 1f) * halfWidth, 0.3f,
                    (Hash01(n, 2, 5) * 2f - 1f) * halfWidth);
                Emit(smoke, start, new Vector3((Hash01(n, 2, 7) - 0.5f) * 0.2f, Mathf.Lerp(0.5f, 0.9f, Hash01(n, 2, 9)),
                    (Hash01(n, 2, 11) - 0.5f) * 0.2f), n, 2, Mathf.Lerp(0.8f, 1.3f, Hash01(n, 2, 13)),
                    Mathf.Lerp(0.8f, 1.1f, Hash01(n, 2, 15)));

                Stream embers = streams[(int)Kind.Embers];
                if (Hash01(n, 2, 17) < 0.5f && Share(embers, 1f) > 0)
                {
                    Emit(embers, start, new Vector3((Hash01(n, 2, 19) - 0.5f) * 0.8f, Mathf.Lerp(1f, 2f,
                        Hash01(n, 2, 21)), (Hash01(n, 2, 23) - 0.5f) * 0.8f), n, 3, 1f, 1f);
                }
            }
        }

        /// <summary>
        /// An extinguisher firing for one frame: a jet of foam from the nozzle
        /// along <paramref name="forward"/>, spreading as it goes, falling to
        /// the floor and settling there, wrapped in a thin mist.
        /// </summary>
        public void Spray(Vector3 nozzle, Vector3 forward, float deltaTime, ref float carry)
        {
            Stream foam = streams[(int)Kind.Foam];
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            int count = Continuous(foam, 90f * deltaTime, ref carry);
            for (int i = 0; i < count; i++)
            {
                int n = ++sequence;
                float speed = Mathf.Lerp(4f, 6f, Hash01(n, 4, 3));
                Vector3 velocity = forward * speed + side * ((Hash01(n, 4, 5) - 0.5f) * 1.8f) +
                                   Vector3.up * ((Hash01(n, 4, 7) - 0.3f) * 1.2f);
                Emit(foam, nozzle, velocity, n, 4, Mathf.Lerp(0.7f, 1.3f, Hash01(n, 4, 9)),
                    Mathf.Lerp(0.9f, 1.05f, Hash01(n, 4, 11)));

                Stream mist = streams[(int)Kind.Mist];
                if (Hash01(n, 4, 13) < 0.25f && Share(mist, 1f) > 0)
                {
                    Emit(mist, nozzle + forward * 0.3f, velocity * 0.5f, n, 5, Mathf.Lerp(0.7f, 1.2f, Hash01(n, 4, 15)),
                        1f);
                }
            }
        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }

            UnityEngine.Object.Destroy(material);
            if (settings != null && settings.hideFlags == HideFlags.DontSave)
            {
                UnityEngine.Object.Destroy(settings);
            }
        }

        /// <summary>
        /// How many particles of this kind to make when the effect wants
        /// <paramref name="wanted"/>: scaled by the settings, then cut back
        /// once the whole budget is more than half used.
        /// </summary>
        private int Share(Stream stream, float wanted)
        {
            int count = Mathf.RoundToInt(wanted * stream.Look.Amount * BudgetShare());
            return Mathf.Clamp(count, 0, Mathf.Max(0, settings.LiveParticleBudget - live));
        }

        /// <summary>The same for an effect that runs every frame, keeping the leftover fraction.</summary>
        private int Continuous(Stream stream, float wanted, ref float carry)
        {
            carry += wanted * stream.Look.Amount * BudgetShare();
            int count = Mathf.FloorToInt(carry);
            carry -= count;
            return Mathf.Clamp(count, 0, Mathf.Max(0, settings.LiveParticleBudget - live));
        }

        /// <summary>1 while at most half the budget is alive, falling to 0 at the full budget.</summary>
        private float BudgetShare()
        {
            float half = settings.LiveParticleBudget * 0.5f;
            return live <= half ? 1f : Mathf.Clamp01((settings.LiveParticleBudget - live) / half);
        }

        private void Emit(Stream stream, Vector3 position, Vector3 velocity, int salt, int index, float size, float shade)
        {
            ParticleLook look = stream.Look;
            Behaviour behaviour = stream.Behaviour;
            float lifetime = behaviour.Lifetime * look.Lifetime * Mathf.Lerp(0.8f, 1.2f, Hash01(salt, index, 101));
            emit.position = position;
            emit.velocity = velocity * look.Speed;
            emit.startSize = behaviour.Size * look.Size * size;
            emit.startLifetime = lifetime;
            emit.startColor = new Color(shade, shade, shade, 1f);
            emit.rotation3D = new Vector3(Hash01(salt, index, 103), Hash01(salt, index, 105), Hash01(salt, index, 107)) * 360f;
            emit.angularVelocity3D = new Vector3(Hash01(salt, index, 109) - 0.5f, Hash01(salt, index, 111) - 0.5f,
                Hash01(salt, index, 113) - 0.5f) * 720f;
            emit.randomSeed = (uint)(salt * 31 + index);
            stream.System.Emit(emit, 1);
            live++;
        }

        private static int Salt(ulong seed) => (int)(seed % 1000003UL);

        private static ParticleSystem Build(Kind kind, Behaviour behaviour, Mesh cube, Material material, Transform parent,
            Transform floor)
        {
            var holder = new GameObject($"{kind} particles");
            holder.transform.SetParent(parent, false);
            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Placed by hand from event IDs, never by the system's own dice.
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)kind + 1u;

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = behaviour.Cap;
            main.startSpeed = 0f;
            main.startRotation3D = true;
            main.gravityModifier = behaviour.Gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = behaviour.Growth >= 1f
                ? new ParticleSystem.MinMaxCurve(behaviour.Growth, AnimationCurve.EaseInOut(0f, 1f / behaviour.Growth, 1f, 1f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, behaviour.Growth));

            if (behaviour.Drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 100f;
                limit.drag = behaviour.Drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }

            if (behaviour.Bounce >= 0f)
            {
                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, floor);
                collision.bounce = behaviour.Bounce;
                collision.dampen = 1f - behaviour.Keep;
                collision.lifetimeLoss = 0f;
                collision.radiusScale = 0.5f;
            }

            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;

            system.Play();
            return system;
        }

        /// <summary>Fades a kind from its start colour to its end colour, and to nothing, over its life.</summary>
        private static void Recolour(Stream stream)
        {
            Color start = stream.Look.Start;
            Color end = stream.Look.End;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a * 0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colour = stream.System.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            stream.ShownStart = start;
            stream.ShownEnd = end;
        }
    }
}
