using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Loose objects: boxes are brown cubes 0.75 as tall as they are wide,
    /// chairs a seat, a back and four legs. Each is drawn exactly where the
    /// physics engine has it, turned the way it is turned: a chair knocked
    /// over lies on its side, a thrown bag flies in its real arc, a box
    /// stacked on another sits on top of it. Near flames they darken as they
    /// heat up; then they burn with a crown of flame cubes, and are left
    /// charcoal-black. A carried item is held up at hand height.
    /// </summary>
    internal sealed class BoxViews
    {
        private sealed class BoxView
        {
            public Transform Transform;
            public PhysicsObjectKind Kind;
            public Renderer[] Renderers;
            public Color Colour;
            public FlameEmitter Flames;
            public float Size;
            public float Height;
            public float Lift;

            /// <summary>
            /// A thing that jumped more than a metre in one tick was put
            /// somewhere by the simulation, not pushed (the tower of boxes
            /// laid across the archway): it is drawn tumbling from where it
            /// was to where it is over half a second, rather than blinking
            /// there. When the tumble began, and where from.
            /// </summary>
            public float TumbleStart = float.NegativeInfinity;
            public Vector3 TumbleFrom;
            public Quaternion TumbleFromRotation;

            /// <summary>0 while it is in one piece, 1 once it has collapsed into wreckage.</summary>
            public float Wreck;
        }

        private readonly Dictionary<SimulationId, BoxView> boxes = new Dictionary<SimulationId, BoxView>();
        private readonly PresentationMaterials materials;

        public BoxViews(ScenarioData scenario, PresentationMaterials materials, ParticleEffects effects,
            Transform parent)
        {
            this.materials = materials;
            foreach (PhysicsObjectDefinition definition in scenario.PhysicsObjects)
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

            foreach (PhysicsObjectDefinition definition in scenario.PhysicsObjects)
            {
                boxes[definition.ObjectId].Kind = definition.Kind;
            }

            foreach (BoxView view in boxes.Values)
            {
                view.Flames = new FlameEmitter(view.Transform, 4, effects, materials);
            }
        }

        /// <summary>
        /// A cardboard box, three quarters as tall as it is wide. Like every
        /// other loose thing it is a cube on a root at floor level, so squashing
        /// or moving the root never touches the box's own size.
        /// </summary>
        private static BoxView CreateBox(PhysicsObjectDefinition definition, PresentationMaterials materials,
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
                Size = size,
                Height = height
            };
        }

        /// <summary>
        /// The rest of the office's things, each a couple of primitives on a
        /// root at floor level: a waste bin, a potted plant, a soft bag, or a
        /// laptop lying open.
        /// </summary>
        private static BoxView CreateOfficeThing(PhysicsObjectDefinition definition,
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

                case PhysicsObjectKind.TableWreck:
                    // What is left of a table that went over: three boards in a
                    // heap, lying at angles to each other. Low enough to see
                    // over, solid enough to have to go round.
                    height = 0.18f;
                    colour = PresentationMaterials.WoodColor * 0.75f;
                    for (int board = 0; board < 3; board++)
                    {
                        GameObject plank = CreatePrimitive($"Board {board}", PrimitiveType.Cube, root,
                            root.position + Vector3.up * (0.03f + board * 0.05f),
                            new Vector3(size * 1.4f, 0.05f, size * 0.8f), materials.Box);
                        plank.transform.localRotation = Quaternion.Euler(0f, board * 28f - 28f, board * 4f - 4f);
                        ShowThroughWalls(plank, materials);
                        renderers.Add(plank.GetComponent<Renderer>());
                    }

                    break;

                case PhysicsObjectKind.FuseBox:
                    // A grey steel cabinet on the wall, with a door on the
                    // front of it: bigger than anything else electrical, and it
                    // should look like it.
                    height = size;
                    colour = new Color(0.55f, 0.57f, 0.60f);
                    Part("Cabinet", PrimitiveType.Cube, Vector3.up * (height * 0.5f),
                        new Vector3(size, height, size * 0.35f));
                    Part("Door", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, -size * 0.2f),
                        new Vector3(size * 0.85f, height * 0.8f, 0.03f));
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

                case PhysicsObjectKind.VendingMachine:
                    // A tall dark red cabinet with a pale glass front and a
                    // black base strip: the shapes the physics has, coloured.
                    height = 1.8f;
                    colour = new Color(0.62f, 0.14f, 0.14f);
                    Part("Cabinet", PrimitiveType.Cube, Vector3.up * (height * 0.5f), new Vector3(size, height, size));
                    Part("Glass", PrimitiveType.Cube, new Vector3(0f, height * 0.6f, -size * 0.5f),
                        new Vector3(size * 0.7f, height * 0.55f, 0.02f));
                    Part("Base", PrimitiveType.Cube, new Vector3(0f, 0.08f, -size * 0.5f), new Vector3(size, 0.16f, 0.02f));
                    break;

                case PhysicsObjectKind.Cabinet:
                    // A grey-blue steel filing cabinet with three drawer lines.
                    height = 1.4f;
                    colour = new Color(0.42f, 0.46f, 0.52f);
                    Part("Cabinet", PrimitiveType.Cube, Vector3.up * (height * 0.5f), new Vector3(size, height, size));
                    for (int drawer = 0; drawer < 3; drawer++)
                    {
                        Part($"Drawer {drawer}", PrimitiveType.Cube,
                            new Vector3(0f, height * (0.2f + 0.3f * drawer), -size * 0.5f),
                            new Vector3(size * 0.85f, 0.015f, 0.02f));
                    }

                    break;

                case PhysicsObjectKind.Shelves:
                    // A wooden frame with boards across it and books on them,
                    // in a few colours so it reads as shelves and not a wall.
                    height = 1.8f;
                    colour = PresentationMaterials.WoodColor;
                    float depth = size * 0.4f;
                    Part("Left side", PrimitiveType.Cube, new Vector3(-size * 0.5f + 0.015f, height * 0.5f, 0f),
                        new Vector3(0.03f, height, depth));
                    Part("Right side", PrimitiveType.Cube, new Vector3(size * 0.5f - 0.015f, height * 0.5f, 0f),
                        new Vector3(0.03f, height, depth));
                    Part("Back", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, depth * 0.5f - 0.01f),
                        new Vector3(size, height, 0.02f));
                    for (int shelf = 0; shelf < 4; shelf++)
                    {
                        float y = height * (0.05f + 0.3f * shelf);
                        Part($"Shelf {shelf}", PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(size, 0.03f, depth));
                        Part($"Books {shelf}", PrimitiveType.Cube, new Vector3(0f, y + 0.14f, 0f),
                            new Vector3(size * 0.8f, 0.25f, depth * 0.7f));
                    }

                    break;

                case PhysicsObjectKind.CopyMachine:
                    // A big pale box with a darker lid and a castor strip.
                    height = 1.1f;
                    colour = new Color(0.78f, 0.78f, 0.76f);
                    Part("Body", PrimitiveType.Cube, Vector3.up * (height * 0.55f), new Vector3(size, height * 0.9f, size * 0.9f));
                    Part("Lid", PrimitiveType.Cube, Vector3.up * (height + 0.02f), new Vector3(size * 0.9f, 0.04f, size * 0.8f));
                    Part("Castors", PrimitiveType.Cube, Vector3.up * 0.05f, new Vector3(size * 0.9f, 0.1f, size * 0.8f));
                    break;

                case PhysicsObjectKind.Whiteboard:
                    // A white board on a thin post over a low base with wheels.
                    height = 1.85f;
                    colour = new Color(0.93f, 0.94f, 0.92f);
                    Part("Base", PrimitiveType.Cube, Vector3.up * 0.04f, new Vector3(size, 0.08f, size * 0.5f));
                    Part("Post", PrimitiveType.Cube, Vector3.up * 0.35f, new Vector3(0.05f, 0.6f, 0.05f));
                    Part("Board", PrimitiveType.Cube, Vector3.up * 1.2f, new Vector3(size, 1.3f, 0.04f));
                    break;

                case PhysicsObjectKind.StandingLamp:
                    // A dark base, a thin post and a cream shade on top. The
                    // shade stays drawn on the lamp: the loose shade that drops
                    // off when the lamp goes over is a thing of its own.
                    height = 1.725f;
                    colour = new Color(0.25f, 0.24f, 0.22f);
                    Part("Base", PrimitiveType.Cylinder, Vector3.up * 0.02f, new Vector3(size, 0.02f, size));
                    Part("Post", PrimitiveType.Cylinder, Vector3.up * 0.77f, new Vector3(0.03f, 0.73f, 0.03f));
                    Part("Shade", PrimitiveType.Cylinder, Vector3.up * 1.6f, new Vector3(size * 1.1f, 0.125f, size * 1.1f));
                    break;

                case PhysicsObjectKind.LampShade:
                    height = 0.25f;
                    colour = new Color(0.92f, 0.86f, 0.70f);
                    Part("Shade", PrimitiveType.Cylinder, Vector3.up * (height * 0.5f), new Vector3(size, height * 0.5f, size));
                    break;

                case PhysicsObjectKind.RobotVacuum:
                    // A squat dark disc with a little light on top.
                    height = 0.09f;
                    colour = new Color(0.22f, 0.23f, 0.25f);
                    Part("Disc", PrimitiveType.Cylinder, Vector3.up * (height * 0.5f), new Vector3(size, height * 0.5f, size));
                    Part("Light", PrimitiveType.Cube, new Vector3(0f, height + 0.01f, size * 0.25f), new Vector3(0.05f, 0.02f, 0.05f));
                    break;

                case PhysicsObjectKind.AlarmSounder:
                    // A fire alarm bell high on the wall, above every head: a
                    // red plate with a dome on its front. It flashes while the
                    // bells ring (see Update) and goes dark once it has popped.
                    height = 2.1f + size * 0.5f;
                    colour = new Color(0.78f, 0.12f, 0.12f);
                    Part("Plate", PrimitiveType.Cube, Vector3.up * 2.1f, new Vector3(size, size, 0.04f));
                    Part("Dome", PrimitiveType.Sphere, new Vector3(0f, 2.1f, -size * 0.2f), new Vector3(size * 0.6f, size * 0.6f, size * 0.4f));
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
                Size = size,
                Height = height
            };
        }

        /// <summary>A simple wooden chair built on a root at floor level, facing its back toward -Z.</summary>
        private static BoxView CreateChair(PhysicsObjectDefinition definition, PresentationMaterials materials,
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
                Size = size,

                // Flames rise from the seat, which is 0.6 of the way up this.
                Height = seatHeight / 0.6f
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
            view.Flames.Update(state == ObjectBurnState.Burning, bottom, spread,
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
                ? view.Transform.position.y + Mathf.Max(0.1f, view.Height * 0.5f)
                : 0.4f;
        }

        /// <summary>
        /// Where the middle of a thing is drawn now, how wide it is, and
        /// whether it breaks into bright shards (an appliance or an
        /// extinguisher) rather than splinters, for the effect of it smashing.
        /// </summary>
        public bool TryDescribe(SimulationId objectId, out Vector3 middle, out float size, out bool shatters)
        {
            if (!boxes.TryGetValue(objectId, out BoxView view))
            {
                middle = default;
                size = 0f;
                shatters = false;
                return false;
            }

            middle = view.Transform.TransformPoint(Vector3.up * (view.Height * 0.4f));
            size = view.Size;
            shatters = view.Kind == PhysicsObjectKind.Laptop || view.Kind == PhysicsObjectKind.Microwave ||
                       view.Kind == PhysicsObjectKind.WallSocket || view.Kind == PhysicsObjectKind.Extinguisher ||
                       view.Kind == PhysicsObjectKind.CopyMachine;
            return true;
        }

        /// <summary>Where the simulation lets go of a carried thing, in metres; it is drawn there while held.</summary>
        private const float HandHeight = 1f;

        /// <summary>A thing that moved further than this in one tick was put there, and is drawn tumbling (see <c>BoxView.TumbleStart</c>).</summary>
        private const float TumbleJumpMetres = 1f;
        private const float TumbleSeconds = 0.5f;
        private const float TumbleHeight = 0.6f;

        public void Update(RunSnapshot snapshot, RunSnapshot previousSnapshot, float blend, float time)
        {
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot box = snapshot.PhysicsObjects[i];
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

                PhysicsObjectSnapshot previous = previousSnapshot != null && i < previousSnapshot.PhysicsObjects.Count
                    ? previousSnapshot.PhysicsObjects[i]
                    : box;

                // Smashed: it collapses into a flat heap, as small as the
                // physics made it, and stays that way.
                float delta = Time.deltaTime;
                view.Wreck = Mathf.MoveTowards(view.Wreck, box.Wrecked ? 1f : 0f, delta * 6f);
                float shrink = Mathf.Lerp(1f, 0.75f, view.Wreck);
                view.Transform.localScale = new Vector3(shrink, Mathf.Lerp(1f, 0.3f, view.Wreck), shrink);

                // Lifted smoothly into someone's arms. The simulation keeps no
                // pose for a carried thing (it rides with its carrier), so it
                // is drawn in their hands, upright and facing their way.
                view.Lift = Mathf.MoveTowards(view.Lift, box.IsHeld ? 1f : 0f, delta * 4f);

                // Put somewhere in one tick (see TumbleStart): start a tumble
                // from where it was drawn last frame.
                Vector3 wasAt = ToUnityPosition(previous.Position);
                Vector3 isAt = ToUnityPosition(box.Position);
                if (!box.IsHeld && !previous.IsHeld && !previous.Dormant &&
                    (isAt - wasAt).sqrMagnitude > TumbleJumpMetres * TumbleJumpMetres &&
                    time - view.TumbleStart > TumbleSeconds)
                {
                    view.TumbleStart = time;
                    view.TumbleFrom = view.Transform.position;
                    view.TumbleFromRotation = view.Transform.rotation;
                }

                float tumbleAge = time - view.TumbleStart;
                if (tumbleAge < TumbleSeconds)
                {
                    // An arc up and over, turning end over end, to where the
                    // simulation now has it.
                    float t = tumbleAge / TumbleSeconds;
                    Vector3 landing = box.Pose.IsKnown ? PoseOrigin(box.Pose) : isAt;
                    Quaternion landed = box.Pose.IsKnown ? PoseRotation(box.Pose) : Quaternion.Euler(0f, box.HeadingDegrees, 0f);
                    Vector3 arc = Vector3.Lerp(view.TumbleFrom, landing, t) + Vector3.up * (TumbleHeight * 4f * t * (1f - t));
                    Quaternion spin = Quaternion.Slerp(view.TumbleFromRotation, landed, t) *
                                      Quaternion.Euler(360f * t * (1f - t) * 2f, 0f, 0f);
                    view.Transform.SetPositionAndRotation(arc, spin);
                }
                else if (!box.Pose.IsKnown)
                {
                    Vector3 planar = Vector3.Lerp(wasAt, isAt, blend);
                    float yaw = Mathf.LerpAngle(previous.HeadingDegrees, box.HeadingDegrees, blend);
                    view.Transform.SetPositionAndRotation(planar + Vector3.up * (view.Lift * HandHeight),
                        Quaternion.Euler(0f, yaw, 0f));
                }
                else
                {
                    BodyPose to = box.Pose;
                    BodyPose from = previous.Pose.IsKnown ? previous.Pose : to;
                    view.Transform.SetPositionAndRotation(
                        Vector3.Lerp(PoseOrigin(from), PoseOrigin(to), blend),
                        Quaternion.Slerp(PoseRotation(from), PoseRotation(to), blend));
                }

                ShowFire(view, box.BurnState, box.HeatPercent, time);

                // A bell rings by flashing, twice a second, until the flames
                // have had it; then it is a dark thing on the wall like any
                // other burnt-out thing.
                if (view.Kind == PhysicsObjectKind.AlarmSounder && snapshot.AlarmsRinging &&
                    box.BurnState == ObjectBurnState.Intact)
                {
                    float pulse = Mathf.Repeat(time * 4f, 2f) < 1f ? 1f : 0.25f;
                    Color flash = Color.Lerp(view.Colour, Color.white, pulse);
                    foreach (Renderer part in view.Renderers)
                    {
                        materials.SetColor(part, flash);
                    }
                }
            }
        }

        /// <summary>Where a pose's origin is in the world, in metres.</summary>
        internal static Vector3 PoseOrigin(BodyPose pose) =>
            ToUnityPosition(pose.Origin) + Vector3.up * Metres(pose.HeightMillimetres);

        /// <summary>A pose's turn as Unity's rotation.</summary>
        internal static Quaternion PoseRotation(BodyPose pose)
        {
            const float scale = BodyPose.RotationScale;
            var rotation = new Quaternion(pose.RotationX / scale, pose.RotationY / scale, pose.RotationZ / scale,
                pose.RotationW / scale);
            return rotation.normalized;
        }
    }
}
