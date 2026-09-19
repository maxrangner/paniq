using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>Brown cubes, 0.75 as tall as they are wide, that slide smoothly and hop and tip a little when hit.</summary>
    internal sealed class BoxViews
    {
        private sealed class BoxView
        {
            public Transform Transform;
            public float Height;
            public float HopStart = -10f;
            public float HopStrength;
        }

        private readonly Dictionary<SimulationId, BoxView> boxes = new Dictionary<SimulationId, BoxView>();

        public BoxViews(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            foreach (FireReactionPhysicsObjectDefinition definition in scenario.PhysicsObjects)
            {
                float size = Metres(definition.SizeMillimetres);
                float height = size * 0.75f;
                GameObject box = CreatePrimitive($"Box {definition.ObjectId.Value} (presentation)", PrimitiveType.Cube, parent,
                    ToUnityPosition(definition.InitialPosition) + Vector3.up * (height * 0.5f),
                    new Vector3(size, height, size), materials.Box);

                // Slightly different cardboard for each box; presentation-only variation.
                Color shade = PresentationMaterials.BoxColor * (0.85f + 0.3f * Hash01((int)definition.ObjectId.Value, 7, 3));
                materials.SetColor(box.GetComponent<Renderer>(), shade);
                boxes.Add(definition.ObjectId, new BoxView { Transform = box.transform, Height = height });
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
                view.Transform.SetPositionAndRotation(
                    planar + Vector3.up * (view.Height * 0.5f + hop * 0.12f),
                    Quaternion.Euler(hop * 18f, yaw, 0f));
            }
        }
    }
}
