using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Loose objects: boxes are brown cubes 0.75 as tall as they are wide,
    /// chairs a seat, a back and four legs. They slide smoothly and hop and
    /// tip a little when hit. Near flames they darken as they heat up; then
    /// they burn with a crown of flame cubes, and are left charcoal-black.
    /// A carried item is held up at chest height; a thrown one flies in a
    /// low arc (its height is display only; the simulation is flat).
    /// </summary>
    internal sealed class BoxViews
    {
        private sealed class BoxView
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public Color Colour;
            public FlameCubes Flames;
            public float Size;
            public float Height;
            public float HopStart = -10f;
            public float HopStrength;
            public float Lift;
            public float Arc;
        }

        private readonly Dictionary<SimulationId, BoxView> boxes = new Dictionary<SimulationId, BoxView>();
        private readonly PresentationMaterials materials;

        public BoxViews(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            this.materials = materials;
            foreach (FireReactionPhysicsObjectDefinition definition in scenario.PhysicsObjects)
            {
                if (definition.Kind == PhysicsObjectKind.Chair || definition.Kind == PhysicsObjectKind.OfficeChair)
                {
                    boxes.Add(definition.ObjectId, CreateChair(definition, materials, parent));
                    continue;
                }

                if (definition.Kind != PhysicsObjectKind.Box)
                {
                    boxes.Add(definition.ObjectId, CreateOfficeThing(definition, materials, parent));
                    continue;
                }

                float size = Metres(definition.SizeMillimetres);
                float height = size * 0.75f;
                GameObject box = CreatePrimitive($"Box {definition.ObjectId.Value} (presentation)", PrimitiveType.Cube, parent,
                    ToUnityPosition(definition.InitialPosition) + Vector3.up * (height * 0.5f),
                    new Vector3(size, height, size), materials.Box);

                ShowThroughWalls(box);

                // Slightly different cardboard for each box; presentation-only variation.
                Color shade = PresentationMaterials.BoxColor * (0.85f + 0.3f * Hash01((int)definition.ObjectId.Value, 7, 3));
                materials.SetColor(box.GetComponent<Renderer>(), shade);
                boxes.Add(definition.ObjectId, new BoxView
                {
                    Transform = box.transform,
                    Renderers = new[] { box.GetComponent<Renderer>() },
                    Colour = shade,
                    Flames = new FlameCubes(box.transform, 4, materials, definition.ObjectId.Value % 89UL),
                    Size = size,
                    Height = height
                });
            }
        }

        /// <summary>
        /// The rest of the office's things, each a couple of primitives on a
        /// root at floor level: a waste bin, a potted plant, a soft bag, or a
        /// laptop lying open.
        /// </summary>
        private static BoxView CreateOfficeThing(FireReactionPhysicsObjectDefinition definition,
            PresentationMaterials materials, Transform parent)
        {
            float size = Metres(definition.SizeMillimetres);
            var root = new GameObject($"{definition.Kind} {definition.ObjectId.Value} (presentation)").transform;
            root.SetParent(parent, false);
            root.position = ToUnityPosition(definition.InitialPosition);

            var renderers = new List<Renderer>();
            Color colour;
            float height;
            void Part(string name, PrimitiveType shape, Vector3 localPosition, Vector3 scale)
            {
                GameObject part = CreatePrimitive(name, shape, root, root.position + localPosition, scale, materials.Box);
                ShowThroughWalls(part);
                renderers.Add(part.GetComponent<Renderer>());
            }

            switch (definition.Kind)
            {
                case PhysicsObjectKind.WasteBin:
                    height = size * 1.1f;
                    colour = new Color(0.35f, 0.38f, 0.42f);
                    Part("Bin", PrimitiveType.Cylinder, Vector3.up * (height * 0.5f), new Vector3(size, height * 0.5f, size));
                    break;

                case PhysicsObjectKind.PottedPlant:
                    height = size * 1.6f;
                    colour = new Color(0.30f, 0.52f, 0.28f);
                    Part("Pot", PrimitiveType.Cylinder, Vector3.up * (size * 0.25f), new Vector3(size, size * 0.25f, size));
                    Part("Leaves", PrimitiveType.Sphere, Vector3.up * (height * 0.7f), new Vector3(size * 1.1f, size * 1.2f, size * 1.1f));
                    break;

                case PhysicsObjectKind.Extinguisher:
                    height = size * 2f;
                    colour = new Color(0.72f, 0.12f, 0.10f);
                    Part("Bottle", PrimitiveType.Cylinder, Vector3.up * (height * 0.5f), new Vector3(size, height * 0.5f, size));
                    Part("Nozzle", PrimitiveType.Cube, Vector3.up * (height + 0.03f), new Vector3(size * 0.5f, 0.06f, size * 0.5f));
                    break;

                case PhysicsObjectKind.Bag:
                    height = size * 0.7f;
                    colour = new Color(0.42f, 0.30f, 0.45f);
                    Part("Bag", PrimitiveType.Sphere, Vector3.up * (height * 0.5f), new Vector3(size, height, size * 0.75f));
                    break;

                default:
                    height = size * 0.5f;
                    colour = new Color(0.55f, 0.57f, 0.60f);
                    Part("Base", PrimitiveType.Cube, Vector3.up * 0.015f, new Vector3(size, 0.03f, size * 0.7f));
                    Part("Screen", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, -size * 0.3f),
                        new Vector3(size, height, 0.03f));
                    break;
            }

            foreach (Renderer part in renderers)
            {
                materials.SetColor(part, colour);
            }

            return new BoxView
            {
                Transform = root,
                Renderers = renderers.ToArray(),
                Colour = colour,
                Flames = new FlameCubes(root, 4, materials, definition.ObjectId.Value % 89UL),
                Size = size,
                Height = 0f
            };
        }

        /// <summary>A simple wooden chair built on a root at floor level, facing its back toward -Z.</summary>
        private static BoxView CreateChair(FireReactionPhysicsObjectDefinition definition, PresentationMaterials materials,
            Transform parent)
        {
            float size = Metres(definition.SizeMillimetres) * 0.9f;
            const float seatHeight = 0.45f;
            const float leg = 0.035f;
            var root = new GameObject($"Chair {definition.ObjectId.Value} (presentation)").transform;
            root.SetParent(parent, false);
            root.position = ToUnityPosition(definition.InitialPosition);

            var renderers = new List<Renderer>();
            void Part(string name, Vector3 localPosition, Vector3 scale)
            {
                GameObject part = CreatePrimitive(name, PrimitiveType.Cube, root, root.position + localPosition, scale, materials.Box);
                ShowThroughWalls(part);
                renderers.Add(part.GetComponent<Renderer>());
            }

            Part("Seat", new Vector3(0f, seatHeight, 0f), new Vector3(size, 0.05f, size));
            Part("Back", new Vector3(0f, seatHeight + 0.22f, -size * 0.5f + 0.025f), new Vector3(size, 0.42f, 0.05f));
            if (definition.Kind != PhysicsObjectKind.OfficeChair)
            {
                float corner = size * 0.5f - leg;
                foreach (Vector2 c in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                {
                    Part("Leg", new Vector3(c.x * corner, seatHeight * 0.5f, c.y * corner), new Vector3(leg, seatHeight, leg));
                }
            }

            // An office chair is grey plastic on a castor base; a wooden one is wood.
            bool office = definition.Kind == PhysicsObjectKind.OfficeChair;
            if (office)
            {
                Part("Castors", new Vector3(0f, 0.04f, 0f), new Vector3(size * 0.9f, 0.08f, size * 0.9f));
                Part("Post", new Vector3(0f, seatHeight * 0.5f, 0f), new Vector3(0.06f, seatHeight, 0.06f));
            }

            Color wood = office
                ? new Color(0.30f, 0.32f, 0.36f)
                : PresentationMaterials.WoodColor * (0.9f + 0.25f * Hash01((int)definition.ObjectId.Value, 11, 5));
            foreach (Renderer part in renderers)
            {
                materials.SetColor(part, wood);
            }

            return new BoxView
            {
                Transform = root,
                Renderers = renderers.ToArray(),
                Colour = wood,
                Flames = new FlameCubes(root, 4, materials, definition.ObjectId.Value % 89UL),
                Size = size,
                Height = 0f
            };
        }

        private void ShowFire(BoxView view, ObjectBurnState state, int heatPercent, float time)
        {
            Color colour = BurnColour(view.Colour, state, heatPercent);
            foreach (Renderer part in view.Renderers)
            {
                materials.SetColor(part, colour);
            }

            // A box's transform is scaled to the box (so its flames use unit sizes);
            // a chair's root is not (so they use metres).
            bool box = view.Height > 0f;
            Vector3 bottom = box ? new Vector3(0f, 0.3f, 0f) : new Vector3(0f, 0.45f, 0f);
            Vector3 spread = box ? new Vector3(0.35f, 1.2f, 0.35f) : new Vector3(view.Size * 0.35f, 0.6f, view.Size * 0.35f);
            view.Flames.Update(state == ObjectBurnState.Burning, time, bottom, spread, box ? 0.45f : 0.16f);
        }

        /// <summary>Darkening as it heats, glowing while it burns, charcoal once burnt out.</summary>
        internal static Color BurnColour(Color normal, ObjectBurnState state, int heatPercent)
        {
            switch (state)
            {
                case ObjectBurnState.Burning:
                    return Color.Lerp(normal, new Color(0.3f, 0.08f, 0.02f), 0.7f);
                case ObjectBurnState.Burnt:
                    return new Color(0.09f, 0.08f, 0.08f);
                default:
                    return Color.Lerp(normal, new Color(0.25f, 0.15f, 0.1f), heatPercent / 100f * 0.6f);
            }
        }

        /// <summary>A hop and tip; <paramref name="strength"/> 0..1 scales it.</summary>
        public void Hop(SimulationId boxId, float strength, float time)
        {
            if (boxes.TryGetValue(boxId, out BoxView view))
            {
                view.HopStart = time;
                view.HopStrength = strength;
            }
        }

        public void Update(FireReactionSnapshot snapshot, FireReactionSnapshot previousSnapshot, float blend, float time)
        {
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                FireReactionPhysicsObjectSnapshot box = snapshot.PhysicsObjects[i];
                if (!boxes.TryGetValue(box.ObjectId, out BoxView view))
                {
                    continue;
                }

                FireReactionPhysicsObjectSnapshot previous = previousSnapshot != null && i < previousSnapshot.PhysicsObjects.Count
                    ? previousSnapshot.PhysicsObjects[i]
                    : box;
                Vector3 planar = Vector3.Lerp(ToUnityPosition(previous.Position), ToUnityPosition(box.Position), blend);
                float yaw = Mathf.LerpAngle(previous.HeadingDegrees, box.HeadingDegrees, blend);

                float hopAge = (time - view.HopStart) / 0.3f;
                float hop = hopAge < 1f ? Mathf.Sin(hopAge * Mathf.PI) * view.HopStrength : 0f;

                // Lifted smoothly into someone's arms; a thrown item rides high and sinks as it slows.
                float delta = Time.deltaTime;
                view.Lift = Mathf.MoveTowards(view.Lift, box.IsHeld ? 1f : 0f, delta * 4f);
                float arcTarget = box.Thrown ? Mathf.Clamp01(box.SpeedMillimetresPerTick / 80f) : 0f;
                view.Arc = Mathf.MoveTowards(view.Arc, arcTarget, delta * 3f);
                float raised = view.Lift * 0.75f + view.Arc * 0.6f;
                float tumble = view.Arc * time * 540f;
                view.Transform.SetPositionAndRotation(
                    planar + Vector3.up * (view.Height * 0.5f + hop * 0.12f + raised),
                    Quaternion.Euler(hop * 18f + tumble, yaw, 0f));

                ShowFire(view, box.BurnState, box.HeatPercent, time);
            }
        }
    }
}
