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

            /// <summary>0 while it is in one piece, 1 once it has collapsed into wreckage.</summary>
            public float Wreck;

            /// <summary>How high its own top is, in metres: what a box stacked on it stands on.</summary>
            public float TopHeight;

            /// <summary>
            /// How high the thing under it holds it up while it rests there: a
            /// desk top for a laptop, the lower box for a stacked one. Zero for
            /// anything that starts on the floor.
            /// </summary>
            public float SupportHeight;

            /// <summary>Where it is drawn now; it drops smoothly to the floor when it comes loose.</summary>
            public float Rest;
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

                boxes.Add(definition.ObjectId, CreateBox(definition, materials, parent));
            }

            FindWhatHoldsUpEachRestingThing(scenario);
        }

        /// <summary>
        /// A cardboard box, three quarters as tall as it is wide. Like every
        /// other loose thing it is a cube on a root at floor level, so squashing
        /// or moving the root never touches the box's own size.
        /// </summary>
        private static BoxView CreateBox(FireReactionPhysicsObjectDefinition definition, PresentationMaterials materials,
            Transform parent)
        {
            float size = Metres(definition.SizeMillimetres);
            float height = size * 0.75f;
            var root = new GameObject($"Box {definition.ObjectId.Value} (presentation)").transform;
            root.SetParent(parent, false);
            root.position = ToUnityPosition(definition.InitialPosition);

            GameObject cube = CreatePrimitive("Cardboard", PrimitiveType.Cube, root, root.position + Vector3.up * (height * 0.5f),
                new Vector3(size, height, size), materials.Box);
            ShowThroughWalls(cube, materials);

            // Slightly different cardboard for each box; presentation-only variation.
            Color shade = PresentationMaterials.BoxColor * (0.85f + 0.3f * Hash01((int)definition.ObjectId.Value, 7, 3));
            Renderer renderer = cube.GetComponent<Renderer>();
            materials.SetColor(renderer, shade);
            return new BoxView
            {
                Transform = root,
                Renderers = new[] { renderer },
                Colour = shade,
                Flames = new FlameCubes(root, 4, materials, definition.ObjectId.Value % 89UL),
                Size = size,
                Height = height,
                TopHeight = height
            };
        }

        /// <summary>
        /// Works out, once, how high each thing that starts resting is held up:
        /// on a desk, at the desk top; on another object, at that object's top.
        /// A stacked box is authored at the same spot as the one under it, so
        /// the nearest thing on the floor beneath it is its support.
        /// </summary>
        private void FindWhatHoldsUpEachRestingThing(FireReactionScenarioData scenario)
        {
            foreach (FireReactionPhysicsObjectDefinition definition in scenario.PhysicsObjects)
            {
                if (!definition.StartsResting || !boxes.TryGetValue(definition.ObjectId, out BoxView view))
                {
                    continue;
                }

                float support = 0f;
                foreach (FireReactionTableDefinition table in scenario.Tables)
                {
                    if (table.Bounds.ContainsCircle(definition.InitialPosition, 0))
                    {
                        support = RoomView.TableHeight;
                        break;
                    }
                }

                if (support <= 0f)
                {
                    long best = long.MaxValue;
                    foreach (FireReactionPhysicsObjectDefinition under in scenario.PhysicsObjects)
                    {
                        if (under.ObjectId == definition.ObjectId || under.StartsResting ||
                            !boxes.TryGetValue(under.ObjectId, out BoxView underView))
                        {
                            continue;
                        }

                        long distance = LogicalPosition.DistanceSquared(definition.InitialPosition, under.InitialPosition);
                        if (distance < best)
                        {
                            best = distance;
                            support = underView.TopHeight;
                        }
                    }
                }

                view.SupportHeight = support;
                view.Rest = support;
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
                ShowThroughWalls(part, materials);
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

                case PhysicsObjectKind.Microwave:
                    // A boxy appliance with a dark door on the front.
                    height = size * 0.6f;
                    colour = new Color(0.62f, 0.63f, 0.66f);
                    Part("Body", PrimitiveType.Cube, Vector3.up * (height * 0.5f), new Vector3(size, height, size * 0.8f));
                    Part("Door", PrimitiveType.Cube, new Vector3(0f, height * 0.55f, -size * 0.42f),
                        new Vector3(size * 0.8f, height * 0.6f, 0.03f));
                    break;

                case PhysicsObjectKind.WallSocket:
                    // A small flat plate; it never moves, so it is barely there.
                    height = size * 0.5f;
                    colour = new Color(0.88f, 0.87f, 0.84f);
                    Part("Plate", PrimitiveType.Cube, Vector3.up * (height * 0.5f), new Vector3(size, height, 0.03f));
                    break;

                case PhysicsObjectKind.Briefcase:
                    // A flat slab on its edge, with a handle: harder and heavier
                    // than a bag, and it shows.
                    height = size * 0.8f;
                    colour = new Color(0.34f, 0.24f, 0.16f);
                    Part("Case", PrimitiveType.Cube, Vector3.up * (height * 0.5f), new Vector3(size, height, size * 0.3f));
                    Part("Handle", PrimitiveType.Cube, Vector3.up * (height + 0.02f), new Vector3(size * 0.4f, 0.03f, 0.03f));
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
                Height = height,
                TopHeight = height
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
                ShowThroughWalls(part, materials);
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

                // Flames rise from the seat, which is 0.6 of the way up this.
                Height = seatHeight / 0.6f,
                TopHeight = seatHeight
            };
        }

        private void ShowFire(BoxView view, ObjectBurnState state, int heatPercent, float time)
        {
            Color colour = BurnColour(view.Colour, state, heatPercent);
            foreach (Renderer part in view.Renderers)
            {
                materials.SetColor(part, colour);
            }

            // Every root is at floor level and unscaled, so flames are sized in
            // metres from the thing itself: a small box gets a small fire.
            float top = view.Height > 0f ? view.Height : 0.45f;
            Vector3 bottom = new Vector3(0f, top * 0.6f, 0f);
            Vector3 spread = new Vector3(view.Size * 0.35f, Mathf.Max(0.3f, top * 1.3f), view.Size * 0.35f);
            view.Flames.Update(state == ObjectBurnState.Burning, time, bottom, spread,
                Mathf.Clamp(view.Size * 0.4f, 0.08f, 0.18f));
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

        /// <summary>
        /// How high off the floor a burst from this thing should start: the
        /// middle of it, wherever it is now, so a laptop that goes off on a
        /// desk flashes at desk height rather than at the floor.
        /// </summary>
        public float BurstHeightOf(SimulationId objectId)
        {
            return boxes.TryGetValue(objectId, out BoxView view)
                ? view.Rest + Mathf.Max(0.1f, view.Height * 0.5f)
                : 0.4f;
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

                // A spare the player has not put down yet is not in the world,
                // so it is not drawn either.
                if (view.Transform.gameObject.activeSelf == box.Dormant)
                {
                    view.Transform.gameObject.SetActive(!box.Dormant);
                }

                if (box.Dormant)
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

                // In flight it tips nose-up and back as it arcs, never more than
                // a quarter turn; the turning you see comes from the simulation's
                // own heading, so the picture and the physics agree.
                float tumble = view.Arc * 25f;

                // Resting on a desk or on another box it sits on top of it; the
                // moment it comes loose it drops to the floor rather than
                // teleporting there.
                view.Rest = Mathf.MoveTowards(view.Rest, box.Resting ? view.SupportHeight : 0f, delta * 6f);

                // Smashed: it collapses to a flat heap and stays that way.
                view.Wreck = Mathf.MoveTowards(view.Wreck, box.Wrecked ? 1f : 0f, delta * 6f);
                float squash = Mathf.Lerp(1f, 0.3f, view.Wreck);
                view.Transform.localScale = new Vector3(1f, squash, 1f);
                view.Transform.SetPositionAndRotation(
                    planar + Vector3.up * (view.Rest + hop * 0.12f + raised),
                    Quaternion.Euler(hop * 18f + tumble, yaw, view.Wreck * 12f));

                ShowFire(view, box.BurnState, box.HeatPercent, time);
            }
        }
    }
}
