using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Loose objects: boxes are brown cubes 0.75 as tall as they are wide,
    /// chairs a seat, a back and four legs. They slide smoothly and hop and
    /// tip a little when hit.
    /// </summary>
    internal sealed class BoxViews
    {
        private sealed class BoxView
        {
            public Transform Transform;
            public Renderer[] Renderers;
            public float Height;
            public float HopStart = -10f;
            public float HopStrength;
        }

        private readonly Dictionary<SimulationId, BoxView> boxes = new Dictionary<SimulationId, BoxView>();

        public BoxViews(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            foreach (FireReactionPhysicsObjectDefinition definition in scenario.PhysicsObjects)
            {
                if (definition.Kind == PhysicsObjectKind.Chair)
                {
                    boxes.Add(definition.ObjectId, CreateChair(definition, materials, parent));
                    continue;
                }

                float size = Metres(definition.SizeMillimetres);
                float height = size * 0.75f;
                GameObject box = CreatePrimitive($"Box {definition.ObjectId.Value} (presentation)", PrimitiveType.Cube, parent,
                    ToUnityPosition(definition.InitialPosition) + Vector3.up * (height * 0.5f),
                    new Vector3(size, height, size), materials.Box);

                // Slightly different cardboard for each box; presentation-only variation.
                Color shade = PresentationMaterials.BoxColor * (0.85f + 0.3f * Hash01((int)definition.ObjectId.Value, 7, 3));
                materials.SetColor(box.GetComponent<Renderer>(), shade);
                boxes.Add(definition.ObjectId, new BoxView
                {
                    Transform = box.transform,
                    Renderers = new[] { box.GetComponent<Renderer>() },
                    Height = height
                });
            }
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
                renderers.Add(part.GetComponent<Renderer>());
            }

            Part("Seat", new Vector3(0f, seatHeight, 0f), new Vector3(size, 0.05f, size));
            Part("Back", new Vector3(0f, seatHeight + 0.22f, -size * 0.5f + 0.025f), new Vector3(size, 0.42f, 0.05f));
            float corner = size * 0.5f - leg;
            foreach (Vector2 c in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Part("Leg", new Vector3(c.x * corner, seatHeight * 0.5f, c.y * corner), new Vector3(leg, seatHeight, leg));
            }

            Color wood = PresentationMaterials.WoodColor * (0.9f + 0.25f * Hash01((int)definition.ObjectId.Value, 11, 5));
            foreach (Renderer part in renderers)
            {
                materials.SetColor(part, wood);
            }

            return new BoxView { Transform = root, Renderers = renderers.ToArray(), Height = 0f };
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
                view.Transform.SetPositionAndRotation(
                    planar + Vector3.up * (view.Height * 0.5f + hop * 0.12f),
                    Quaternion.Euler(hop * 18f, yaw, 0f));
            }
        }
    }
}
