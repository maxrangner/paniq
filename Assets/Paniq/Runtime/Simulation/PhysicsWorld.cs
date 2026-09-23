using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.Simulation
{
    /// <summary>
    /// The run's 3D physics: Unity's built-in engine (PhysX), in a physics
    /// scene of its own that nothing else in the game can touch, stepped by
    /// the simulation once per tick at the fixed 0.02 seconds. This is the
    /// only file in the simulation that talks to Unity's physics; every other
    /// system asks it for things in the simulation's own units (hundredths
    /// of a millimetre, ticks, grams, whole degrees) and gets whole numbers
    /// back.
    ///
    /// Replays stay exact the way the engine promises: the same build on the
    /// same kind of machine, bodies created in the same order, the same
    /// pushes applied in the same order before every step. Everything here is
    /// done in handle order, which is creation order, which the systems that
    /// create bodies keep in ascending simulation ID order. What the engine
    /// reports back is rounded to whole numbers before any rule reads it, and
    /// the collisions it reports are sorted, so no rule depends on the order
    /// the engine happened to list them in.
    ///
    /// The world has a floor, walls with doorways in them, a plug in each
    /// closed doorway, tables, and a fence round the lot well outside the
    /// building. Bodies are the loose things and the people.
    /// </summary>
    internal sealed class PhysicsWorld : IDisposable
    {
        /// <summary>Positions and velocities cross this boundary in hundredths of a millimetre, like the object system's.</summary>
        public const int SubMillimetre = 100;

        /// <summary>Rotations cross it as quaternion components scaled by this.</summary>
        public const int RotationScale = 10000;

        private const float MetresPerUnit = 1f / (1000f * SubMillimetre);
        private const float StepSeconds = 1f / Run.TicksPerSecond;

        /// <summary>
        /// How hard the engine works on each step. Fixed here rather than
        /// offered as a setting: they trade exactness against time, and
        /// changing them changes every replay. Sixteen position passes hold a
        /// 70 kg person off a 2 kg waste bin they have pinned against a wall
        /// (eight let them sink a hand's width into it); four velocity passes
        /// keep bounces honest.
        /// </summary>
        private const int SolverIterations = 16;
        private const int SolverVelocityIterations = 4;

        /// <summary>
        /// Walls stand on the wall line, half their thickness on each side, so
        /// they eat this much less than half into each room. Thin, so the rooms
        /// keep the size they were drawn at; the engine's look-ahead for fast
        /// bodies stops anything passing through.
        /// </summary>
        private const int WallThicknessMillimetres = 40;
        private const int OutsideMarginMillimetres = 8000;

        internal enum StaticKind
        {
            None,
            Floor,
            Wall,
            Table,
            Door,
            Fence
        }

        /// <summary>What the engine reported about one body after the last step, in whole numbers.</summary>
        internal struct Reading
        {
            /// <summary>Where its bottom centre is, in hundredths of a millimetre; Y is height above the floor.</summary>
            public long X, Y, Z;

            /// <summary>
            /// Where its middle is across the floor, in hundredths of a
            /// millimetre. For a thing standing up this is over its bottom
            /// centre; for somebody lying down it is their waist, not their feet.
            /// </summary>
            public long CentreX, CentreZ;

            /// <summary>Hundredths of a millimetre per tick.</summary>
            public long VelocityX, VelocityY, VelocityZ;

            /// <summary>Its turn, as a quaternion scaled by <see cref="RotationScale"/>.</summary>
            public int RotationX, RotationY, RotationZ, RotationW;

            /// <summary>Which way its front (+Z) faces across the floor, in whole degrees.</summary>
            public int Heading;

            /// <summary>How upright it stands: 100 on its feet, 0 on its side, -100 upside down.</summary>
            public int UprightPercent;

            /// <summary>How fast it is turning, in whole degrees per tick.</summary>
            public int SpinDegreesPerTick;

            /// <summary>How high its lowest point is above the floor, in millimetres.</summary>
            public int BottomMillimetres;

            public bool Sleeping;

            public LogicalPosition Position => new LogicalPosition(
                (int)FloorDivide(X, SubMillimetre), (int)FloorDivide(Z, SubMillimetre));

            public LogicalPosition Centre => new LogicalPosition(
                (int)FloorDivide(CentreX, SubMillimetre), (int)FloorDivide(CentreZ, SubMillimetre));

            public long HorizontalSpeed => IntegerMath.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);
        }

        /// <summary>
        /// Two things touched during the last step. <see cref="BodyA"/> is
        /// always a body; <see cref="BodyB"/> is another body, or -1 when it
        /// is part of the building (see <see cref="Static"/>). Bodies are in
        /// ascending handle order when both are bodies.
        /// </summary>
        internal readonly struct Contact
        {
            public Contact(int bodyA, int bodyB, StaticKind staticKind, int staticIndex, long impulse,
                LogicalPosition point, int pointHeight, bool began)
            {
                BodyA = bodyA;
                BodyB = bodyB;
                Static = staticKind;
                StaticIndex = staticIndex;
                Impulse = impulse;
                Point = point;
                PointHeight = pointHeight;
                Began = began;
            }

            public int BodyA { get; }
            public int BodyB { get; }
            public StaticKind Static { get; }

            /// <summary>Which table or door, when <see cref="Static"/> is one.</summary>
            public int StaticIndex { get; }

            /// <summary>How hard the engine pushed them apart, in kilogram-millimetres per tick.</summary>
            public long Impulse { get; }

            public LogicalPosition Point { get; }
            public int PointHeight { get; }

            /// <summary>They touched this step and not the one before: a hit rather than a lean.</summary>
            public bool Began { get; }
        }

        private sealed class Body
        {
            public Rigidbody Rigidbody;
            public Collider[] Colliders;
            public ObjectShapes.Part[] Parts;
            public bool Solid = true;
            public bool Kinematic;
            public Reading Reading;
        }

        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly Transform root;
        private readonly PhysicsFeelSettings feel;
        private readonly List<Body> bodies = new List<Body>();
        private readonly Dictionary<int, int> bodyByCollider = new Dictionary<int, int>();
        private readonly Dictionary<int, (StaticKind Kind, int Index)> staticByCollider =
            new Dictionary<int, (StaticKind, int)>();
        private readonly Dictionary<(int, int), PhysicsMaterial> materials = new Dictionary<(int, int), PhysicsMaterial>();
        private readonly List<GameObject> walls = new List<GameObject>();
        private readonly List<GameObject> tables = new List<GameObject>();

        /// <summary>Each table's own body, in table order: tables move, tip and are shoved like anything else.</summary>
        private readonly List<Rigidbody> tableBodies = new List<Rigidbody>();

        /// <summary>Each table's width, height and depth in metres, for working out the floor it covers.</summary>
        private readonly List<Vector3> tableSizes = new List<Vector3>();
        private readonly List<GameObject> doors = new List<GameObject>();
        private readonly List<Contact> contacts = new List<Contact>();
        private readonly List<Contact> incoming = new List<Contact>();
        private readonly PhysicsMaterial buildingMaterial;
        private bool disposed;

        /// <summary>
        /// Every world not yet disposed. A test that forgets to dispose its run
        /// would otherwise leave a physics scene behind in the editor for each
        /// test; the test assembly sweeps this after every test instead.
        /// </summary>
        private static readonly List<PhysicsWorld> Live = new List<PhysicsWorld>();

        /// <summary>Disposes every world still open. For tests, after each one.</summary>
        internal static void DisposeEveryWorld()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Live[i].Dispose();
            }
        }

        /// <summary>
        /// The most worlds kept open at once in the editor outside play mode.
        /// Unity allows only so many preview scenes, and a test that runs
        /// seven seeds one after another drops each run the moment it has its
        /// answer, so the oldest open world is let go to make room. No test
        /// steps more than a few runs side by side.
        /// </summary>
        private const int MostWorldsOpenInTheEditor = 8;

#if UNITY_EDITOR
        static PhysicsWorld()
        {
            // Reloading scripts forgets the list of open worlds but not their
            // scenes, and Unity has only so many to give out: close them first.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeEveryWorld;
        }
#endif

        public PhysicsWorld(PhysicsFeelSettings feel, LogicalBounds building, int wallRestitutionPercent)
        {
            if (!Application.isPlaying && Live.Count >= MostWorldsOpenInTheEditor)
            {
                Live[0].Dispose();
            }

            Live.Add(this);
            this.feel = feel;
            scene = CreateScene();
            physics = scene.GetPhysicsScene();
            root = new GameObject("Paniq physics").transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);

            // The building grips as hard as anything can, so how far a thing
            // slides is decided by the thing's own grip (see Material).
            buildingMaterial = new PhysicsMaterial("Building")
            {
                bounciness = wallRestitutionPercent / 100f,
                dynamicFriction = 1f,
                staticFriction = 1f,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average,
                hideFlags = HideFlags.HideAndDontSave
            };

            BuildFloorAndFence(building);
            Physics.ContactEvent += OnContacts;
        }

        // ---------------------------------------------------------------- scene

        /// <summary>
        /// A scene with its own physics, so a run's bodies never meet anything
        /// else's: the display's click targets, or another run in a test. In
        /// the editor outside play mode Unity only allows preview scenes, which
        /// also keep their physics to themselves.
        /// </summary>
        private static Scene CreateScene()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            }
#endif
            return SceneManager.CreateScene("Paniq physics " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Live.Remove(this);
            Physics.ContactEvent -= OnContacts;
            foreach (PhysicsMaterial material in materials.Values)
            {
                Destroy(material);
            }

            Destroy(buildingMaterial);
            Destroy(personColumn);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                return;
            }
#endif
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static void Destroy(UnityEngine.Object thing)
        {
            if (thing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(thing);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(thing);
            }
        }

        // ---------------------------------------------------------------- the building

        private static float Metres(long hundredthsOfMillimetre) => hundredthsOfMillimetre * MetresPerUnit;

        private static float MetresFromMillimetres(int millimetres) => millimetres * 0.001f;

        private GameObject Solid(string name, StaticKind kind, int index, Vector3 centre, Vector3 size)
        {
            var solid = new GameObject(name);
            solid.transform.SetParent(root, false);
            solid.transform.localPosition = centre;
            var box = solid.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = buildingMaterial;
            staticByCollider[box.GetInstanceID()] = (kind, index);
            return solid;
        }

        private void BuildFloorAndFence(LogicalBounds building)
        {
            float minX = MetresFromMillimetres(building.MinX - OutsideMarginMillimetres);
            float maxX = MetresFromMillimetres(building.MaxX + OutsideMarginMillimetres);
            float minZ = MetresFromMillimetres(building.MinZ - OutsideMarginMillimetres);
            float maxZ = MetresFromMillimetres(building.MaxZ + OutsideMarginMillimetres);
            float height = MetresFromMillimetres(feel.WallHeightMillimetres);
            const float thick = 1f;
            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;
            float spanX = maxX - minX;
            float spanZ = maxZ - minZ;

            Solid("Floor", StaticKind.Floor, 0, new Vector3(midX, -thick * 0.5f, midZ), new Vector3(spanX, thick, spanZ));
            Solid("Ceiling", StaticKind.Fence, 0, new Vector3(midX, height + thick * 0.5f, midZ), new Vector3(spanX, thick, spanZ));
            Solid("Fence west", StaticKind.Fence, 0, new Vector3(minX - thick * 0.5f, height * 0.5f, midZ), new Vector3(thick, height, spanZ));
            Solid("Fence east", StaticKind.Fence, 0, new Vector3(maxX + thick * 0.5f, height * 0.5f, midZ), new Vector3(thick, height, spanZ));
            Solid("Fence south", StaticKind.Fence, 0, new Vector3(midX, height * 0.5f, minZ - thick * 0.5f), new Vector3(spanX, height, thick));
            Solid("Fence north", StaticKind.Fence, 0, new Vector3(midX, height * 0.5f, maxZ + thick * 0.5f), new Vector3(spanX, height, thick));
        }

        /// <summary>
        /// Replaces every wall with these stretches (doorways already cut
        /// out). Called at the start, and again when a blast opens a new hole.
        /// </summary>
        public void SetWalls(IReadOnlyList<NavigationGrid.Wall> stretches)
        {
            foreach (GameObject wall in walls)
            {
                foreach (Collider collider in wall.GetComponents<Collider>())
                {
                    staticByCollider.Remove(collider.GetInstanceID());
                }

                Destroy(wall);
            }

            walls.Clear();
            float height = MetresFromMillimetres(feel.WallHeightMillimetres);
            float thick = MetresFromMillimetres(WallThicknessMillimetres);
            for (int i = 0; i < stretches.Count; i++)
            {
                NavigationGrid.Wall stretch = stretches[i];
                float fromX = MetresFromMillimetres(stretch.From.X);
                float fromZ = MetresFromMillimetres(stretch.From.Z);
                float toX = MetresFromMillimetres(stretch.To.X);
                float toZ = MetresFromMillimetres(stretch.To.Z);
                bool alongX = stretch.From.Z == stretch.To.Z;

                // Each piece runs half a thickness past its ends, so two
                // pieces meeting at a corner leave no crack to slip through.
                var size = alongX
                    ? new Vector3(Mathf.Abs(toX - fromX) + thick, height, thick)
                    : new Vector3(thick, height, Mathf.Abs(toZ - fromZ) + thick);
                var centre = new Vector3((fromX + toX) * 0.5f, height * 0.5f, (fromZ + toZ) * 0.5f);
                walls.Add(Solid("Wall", StaticKind.Wall, i, centre, size));
            }
        }

        /// <summary>
        /// A table: a solid block from the floor to its top, so things stand on
        /// it and nothing goes under, but a real body like any loose thing. It
        /// is shoved, tipped and flipped by whatever hits it, as heavy as its
        /// weight makes it. To everything else it still reports as a table (a
        /// thing that meets it meets <see cref="StaticKind.Table"/>), so the
        /// rules about tables -- what smashes one, what bounces off -- are the
        /// same whether it stands still or not. Its origin is the middle of its
        /// underside.
        /// </summary>
        public int AddTable(LogicalBounds bounds, int massGrams, int frictionPercent)
        {
            int index = tables.Count;
            float height = MetresFromMillimetres(feel.TableHeightMillimetres);
            var size = new Vector3(
                MetresFromMillimetres(bounds.MaxX - bounds.MinX), height,
                MetresFromMillimetres(bounds.MaxZ - bounds.MinZ));
            var table = new GameObject($"Table {index}");
            table.transform.SetParent(root, false);
            table.transform.SetPositionAndRotation(
                new Vector3(MetresFromMillimetres(bounds.MinX + bounds.MaxX) * 0.5f, 0f,
                    MetresFromMillimetres(bounds.MinZ + bounds.MaxZ) * 0.5f),
                Quaternion.identity);
            var box = table.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = size;
            box.sharedMaterial = Material(frictionPercent, 0);
            box.providesContacts = true;
            staticByCollider[box.GetInstanceID()] = (StaticKind.Table, index);
            var rigidbody = table.AddComponent<Rigidbody>();
            Configure(rigidbody, massGrams);
            tables.Add(table);
            tableBodies.Add(rigidbody);
            tableSizes.Add(size);
            return index;
        }

        /// <summary>Whether the engine moved this table in the last step (a table at rest sleeps).</summary>
        public bool IsTableAwake(int index)
        {
            Rigidbody rigidbody = tableBodies[index];
            return !rigidbody.isKinematic && !rigidbody.IsSleeping();
        }

        /// <summary>
        /// Where the table is and which way it is turned, in the snapshot's
        /// terms: its origin (the middle of its underside, which is the top of
        /// a table flipped onto its back) and its rotation.
        /// </summary>
        public BodyPose TablePose(int index)
        {
            Rigidbody rigidbody = tableBodies[index];
            Vector3 position = rigidbody.position;
            Quaternion rotation = rigidbody.rotation;
            return new BodyPose((int)Math.Round(position.y * 1000f),
                (int)Math.Round(rotation.x * RotationScale), (int)Math.Round(rotation.y * RotationScale),
                (int)Math.Round(rotation.z * RotationScale), (int)Math.Round(rotation.w * RotationScale),
                new LogicalPosition((int)Math.Round(position.x * 1000f), (int)Math.Round(position.z * 1000f)));
        }

        /// <summary>
        /// The rectangle of floor the table covers now, however it is turned:
        /// every corner of its block, dropped straight down onto the floor. A
        /// table on its side covers a strip as long as it is and as wide as it
        /// is tall.
        /// </summary>
        public LogicalBounds TableFootprint(int index)
        {
            Rigidbody rigidbody = tableBodies[index];
            Vector3 size = tableSizes[index];
            Vector3 position = rigidbody.position;
            Quaternion rotation = rigidbody.rotation;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? -size.x * 0.5f : size.x * 0.5f,
                    (corner & 2) == 0 ? 0f : size.y,
                    (corner & 4) == 0 ? -size.z * 0.5f : size.z * 0.5f);
                Vector3 world = position + rotation * local;
                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minZ = Mathf.Min(minZ, world.z);
                maxZ = Mathf.Max(maxZ, world.z);
            }

            return new LogicalBounds((int)Math.Round(minX * 1000f), (int)Math.Round(maxX * 1000f),
                (int)Math.Round(minZ * 1000f), (int)Math.Round(maxZ * 1000f));
        }

        /// <summary>
        /// Gives a table a sudden change of speed (hundredths of a millimetre
        /// per tick), the way a blast does. Heavy tables are given less by the
        /// caller; the engine takes the change as it is.
        /// </summary>
        public void ShoveTable(int index, long vx, long vy, long vz)
        {
            Rigidbody rigidbody = tableBodies[index];
            if (rigidbody.isKinematic)
            {
                return;
            }

            rigidbody.AddForce(VelocityInMetres(vx, vy, vz), ForceMode.VelocityChange);
        }

        /// <summary>
        /// A plug filling a doorway, there while the door is shut. The door
        /// leaf itself is drawn by the display; to the physics a shut door is
        /// simply more wall.
        /// </summary>
        public int AddDoor(LogicalPosition centre, bool alongX, int width, bool shut)
        {
            int index = doors.Count;
            float height = MetresFromMillimetres(feel.WallHeightMillimetres);
            float thick = MetresFromMillimetres(WallThicknessMillimetres);
            float span = MetresFromMillimetres(width);
            var size = alongX ? new Vector3(span, height, thick) : new Vector3(thick, height, span);
            var middle = new Vector3(MetresFromMillimetres(centre.X), height * 0.5f, MetresFromMillimetres(centre.Z));
            GameObject door = Solid("Door", StaticKind.Door, index, middle, size);
            doors.Add(door);
            doorSpaces.Add((middle, size));
            SetSolid(door, shut);
            return index;
        }

        /// <summary>Where each doorway's plug goes, so a doorway can be checked for bodies before it is shut.</summary>
        private readonly List<(Vector3 Middle, Vector3 Size)> doorSpaces = new List<(Vector3, Vector3)>();

        /// <summary>
        /// Whether any part of any body, a person or a loose thing, is in this
        /// doorway's gap, other than the one given (whoever is shutting it).
        /// </summary>
        public bool IsAnyBodyInDoorway(int door, int ignoreHandle)
        {
            (Vector3 middle, Vector3 size) = doorSpaces[door];

            // The gap itself, a hair thinner so a body leaning on the frame
            // beside it does not count.
            Vector3 half = size * 0.5f - new Vector3(0.005f, 0.005f, 0.005f);
            int count = OverlapBoxAll(middle, half);
            for (int i = 0; i < count; i++)
            {
                if (bodyByCollider.TryGetValue(overlapping[i].GetInstanceID(), out int handle) && handle != ignoreHandle)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetDoorShut(int index, bool shut)
        {
            SetSolid(doors[index], shut);
        }

        private static void SetSolid(GameObject solid, bool on)
        {
            foreach (Collider collider in solid.GetComponents<Collider>())
            {
                collider.enabled = on;
            }
        }

        // ---------------------------------------------------------------- bodies

        public int BodyCount => bodies.Count;

        /// <summary>
        /// Adds a loose thing made of these pieces, standing with its bottom
        /// centre at this point (hundredths of a millimetre), turned to this
        /// heading. Returns its handle; handles count up from zero in the
        /// order bodies are added.
        /// </summary>
        public int AddBody(string name, ObjectShapes.Part[] parts, long x, long y, long z, int heading, int massGrams,
            int frictionPercent, int bouncinessPercent)
        {
            var thing = new GameObject(name);
            thing.transform.SetParent(root, false);
            thing.transform.SetPositionAndRotation(
                new Vector3(Metres(x), Metres(y), Metres(z)), Quaternion.Euler(0f, heading, 0f));
            var rigidbody = thing.AddComponent<Rigidbody>();
            var body = new Body { Rigidbody = rigidbody, Parts = parts };
            body.Colliders = BuildColliders(thing, parts, Material(frictionPercent, bouncinessPercent));
            Configure(rigidbody, massGrams);
            rigidbody.mass = EngineMass(massGrams, true);
            return Register(body);
        }

        /// <summary>
        /// Adds a person: a capsule of this radius and height standing at this
        /// point (whole millimetres). It is a real body: it is pushed, jostled
        /// and knocked over by everything else. While upright it cannot tip
        /// (see <see cref="SetUpright"/>) and its feet are frictionless; the
        /// simulation's motor does the gripping instead.
        /// </summary>
        public int AddPerson(string name, LogicalPosition position, int heading, int radius, int height, int massGrams)
        {
            var person = new GameObject(name);
            person.transform.SetParent(root, false);
            person.transform.SetPositionAndRotation(
                new Vector3(MetresFromMillimetres(position.X), 0f, MetresFromMillimetres(position.Z)),
                Quaternion.Euler(0f, heading, 0f));
            var column = person.AddComponent<MeshCollider>();
            column.sharedMesh = PersonColumn(radius, height);
            column.convex = true;
            column.providesContacts = true;
            column.sharedMaterial = Material(0, 10);
            var rigidbody = person.AddComponent<Rigidbody>();
            Configure(rigidbody, massGrams);
            rigidbody.constraints = StandingConstraints;
            var body = new Body
            {
                Rigidbody = rigidbody,
                Colliders = new Collider[] { column },
                Parts = Array.Empty<ObjectShapes.Part>()
            };
            return Register(body);
        }

        /// <summary>How many flat sides a person's body has: enough that it reads as round.</summary>
        private const int PersonSides = 12;

        private Mesh personColumn;

        /// <summary>
        /// A person's solid shape: an upright twelve-sided column, straight up
        /// and down from the floor to the top of their head. Straight sides
        /// matter. A capsule, rounded at the bottom like a pill, wedged a waste
        /// bin or a box up under itself when somebody ran into one, and a
        /// person standing on the floor could not then be pushed off it; a flat
        /// side just shoves it along. One mesh, shared by everybody in the run.
        /// </summary>
        private Mesh PersonColumn(int radius, int height)
        {
            if (personColumn != null)
            {
                return personColumn;
            }

            float r = MetresFromMillimetres(radius);
            float top = MetresFromMillimetres(height);
            var corners = new Vector3[PersonSides * 2];
            for (int i = 0; i < PersonSides; i++)
            {
                // Whole-degree steps from the integer sine table, so the shape
                // is the same on every machine.
                int degrees = i * 360 / PersonSides;
                float x = IntegerMath.Sin(degrees) * r / IntegerMath.TrigScale;
                float z = IntegerMath.Cos(degrees) * r / IntegerMath.TrigScale;
                corners[i] = new Vector3(x, 0f, z);
                corners[i + PersonSides] = new Vector3(x, top, z);
            }

            // Only the corners matter: a convex collider is the hull round them.
            var triangles = new int[(PersonSides - 2) * 3];
            for (int i = 0; i < PersonSides - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            personColumn = new Mesh { name = "Person column", vertices = corners, triangles = triangles, hideFlags = HideFlags.HideAndDontSave };
            return personColumn;
        }

        /// <summary>
        /// Standing, or not. Standing, the body cannot tip over and faces this
        /// heading; not standing, it is free to topple, roll and lie wherever
        /// it ends up.
        /// </summary>
        public void SetUpright(int handle, bool upright, int heading)
        {
            Rigidbody rigidbody = bodies[handle].Rigidbody;
            if (!upright)
            {
                // Free to topple, but a body lying on its side is a smooth
                // cylinder and would roll across the room like a log: it is
                // stiffened against turning, so it goes over and stays put.
                rigidbody.constraints = RigidbodyConstraints.None;
                rigidbody.angularDamping = LyingAngularDamping;
                rigidbody.WakeUp();
                return;
            }

            rigidbody.constraints = StandingConstraints;
            rigidbody.angularDamping = StandingAngularDamping;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.rotation = Quaternion.Euler(0f, heading, 0f);
        }

        /// <summary>
        /// Somebody on their feet neither tips over nor leaves the floor. Their
        /// feet are on the ground: a box they walk into is shoved or knocked
        /// over rather than climbed like a step, and nothing slides under them.
        /// Knocked off their feet, both holds come off.
        /// </summary>
        private const RigidbodyConstraints StandingConstraints =
            RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        /// <summary>How quickly anything stops turning by itself; a thing flung across a room keeps tumbling a while.</summary>
        private const float StandingAngularDamping = 0.3f;

        /// <summary>How quickly somebody lying on the floor stops turning: they topple, but do not roll.</summary>
        private const float LyingAngularDamping = 4f;

        /// <summary>
        /// Lays somebody flat on the floor along a heading, their middle where
        /// it was across the floor and their side on the ground, keeping how
        /// fast they were sliding. Going down is done this way, at once, rather
        /// than by letting 1.7 m of body swing down like a falling plank: the
        /// engine's look-ahead cannot follow a swing that fast, and let the
        /// body sink a hand's width into the floor. The display draws the fall.
        /// </summary>
        public void LayDown(int handle, int heading, int radius)
        {
            Rigidbody rigidbody = bodies[handle].Rigidbody;
            Vector3 middle = rigidbody.worldCenterOfMass;
            middle.y = MetresFromMillimetres(radius) + 0.005f;
            Quaternion lying = Quaternion.Euler(90f, heading, 0f);
            Vector3 origin = middle - lying * rigidbody.centerOfMass;
            Vector3 velocity = rigidbody.linearVelocity;
            rigidbody.position = origin;
            rigidbody.rotation = lying;
            rigidbody.transform.SetPositionAndRotation(origin, lying);
            rigidbody.linearVelocity = new Vector3(velocity.x, Mathf.Min(0f, velocity.y), velocity.z);
            rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Lays a body flat along a heading, keeping its middle where it is:
        /// somebody being dragged by the arms trails in a line behind whoever
        /// is pulling, rather than broadside, and so fits through a doorway.
        /// </summary>
        public void LayAlong(int handle, int heading)
        {
            Rigidbody rigidbody = bodies[handle].Rigidbody;
            Vector3 middle = rigidbody.worldCenterOfMass;

            // Tipped forward a quarter turn, the body's length lies along the
            // way it faces; then it is turned to face the heading.
            Quaternion lying = Quaternion.Euler(90f, heading, 0f);
            Vector3 origin = middle - lying * rigidbody.centerOfMass;
            origin.y = Mathf.Max(origin.y, 0f);
            rigidbody.position = origin;
            rigidbody.rotation = lying;
            rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>Turns a standing body to face this heading, without moving it.</summary>
        public void Face(int handle, int heading)
        {
            Rigidbody rigidbody = bodies[handle].Rigidbody;
            if (rigidbody.isKinematic)
            {
                rigidbody.MoveRotation(Quaternion.Euler(0f, heading, 0f));
                return;
            }

            rigidbody.rotation = Quaternion.Euler(0f, heading, 0f);
        }

        /// <summary>
        /// How grippy a body's surface is, as the engine's friction coefficient
        /// times 100: a person on their feet is 0 (their own feet do the
        /// work), somebody sliding along the floor on their back is not.
        /// </summary>
        public void SetGrip(int handle, int frictionPercent, int bouncinessPercent)
        {
            PhysicsMaterial material = Material(frictionPercent, bouncinessPercent);
            foreach (Collider collider in bodies[handle].Colliders)
            {
                collider.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Room for what a look-up finds. It grows: a look-up that fills it
        /// is asked again with twice the room, because a full buffer is not
        /// an answer. With a fixed thirty-two, a doorway packed with chairs
        /// (three colliders each) could hide a body, and somebody standing up
        /// in a crush could be told the spot was clear.
        /// </summary>
        private Collider[] overlapping = new Collider[64];

        private int OverlapCapsuleAll(Vector3 a, Vector3 b, float radius)
        {
            while (true)
            {
                int count = physics.OverlapCapsule(a, b, radius, overlapping, ~0, QueryTriggerInteraction.Ignore);
                if (count < overlapping.Length)
                {
                    return count;
                }

                overlapping = new Collider[overlapping.Length * 2];
            }
        }

        private int OverlapBoxAll(Vector3 middle, Vector3 half)
        {
            while (true)
            {
                int count = physics.OverlapBox(middle, half, overlapping, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (count < overlapping.Length)
                {
                    return count;
                }

                overlapping = new Collider[overlapping.Length * 2];
            }
        }

        /// <summary>
        /// Whether somebody of this radius and height could stand upright here
        /// (whole millimetres) without being inside anything: a wall, a table,
        /// a loose thing or another person. Their own body does not count.
        /// </summary>
        public bool IsClearToStand(LogicalPosition spot, int radius, int height, int ownHandle)
        {
            // The capsule is lifted a hair off the floor and made a hair thinner,
            // so standing on the floor or just touching a wall is not "inside".
            float r = MetresFromMillimetres(radius - 5);
            var bottom = new Vector3(MetresFromMillimetres(spot.X), MetresFromMillimetres(radius + 10), MetresFromMillimetres(spot.Z));
            var top = new Vector3(bottom.x, MetresFromMillimetres(height - radius), bottom.z);
            int count = OverlapCapsuleAll(bottom, top, r);
            for (int i = 0; i < count; i++)
            {
                if (!bodyByCollider.TryGetValue(overlapping[i].GetInstanceID(), out int handle) || handle != ownHandle)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether somebody of this radius and length could lie flat here along
        /// this heading, their middle at this spot, without being inside
        /// anything but themselves.
        /// </summary>
        public bool IsClearToLie(LogicalPosition middle, int heading, int radius, int length, int ownHandle)
        {
            LogicalPosition along = IntegerMath.Displacement(heading, length / 2 - radius);
            float r = MetresFromMillimetres(radius - 5);
            float height = MetresFromMillimetres(radius + 10);
            var head = new Vector3(MetresFromMillimetres(middle.X + along.X), height, MetresFromMillimetres(middle.Z + along.Z));
            var feet = new Vector3(MetresFromMillimetres(middle.X - along.X), height, MetresFromMillimetres(middle.Z - along.Z));
            int count = OverlapCapsuleAll(head, feet, r);
            for (int i = 0; i < count; i++)
            {
                if (!bodyByCollider.TryGetValue(overlapping[i].GetInstanceID(), out int handle) || handle != ownHandle)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Room for what a line of sight crosses; grows the same way, for the same reason.</summary>
        private RaycastHit[] sightHits = new RaycastHit[64];

        private int RaycastAll(Vector3 start, Vector3 direction, float length)
        {
            while (true)
            {
                int count = physics.Raycast(start, direction, sightHits, length, ~0, QueryTriggerInteraction.Ignore);
                if (count < sightHits.Length)
                {
                    return count;
                }

                sightHits = new RaycastHit[sightHits.Length * 2];
            }
        }

        /// <summary>
        /// Whether a straight line from one spot to the other, at knee height,
        /// meets any part of the building (a wall, a shut door, a table). Loose
        /// things and people do not count: this is whether the two spots are on
        /// the same side of everything fixed.
        /// </summary>
        public bool IsBuildingBetween(LogicalPosition from, LogicalPosition to)
        {
            float knee = 0.4f;
            var start = new Vector3(MetresFromMillimetres(from.X), knee, MetresFromMillimetres(from.Z));
            var end = new Vector3(MetresFromMillimetres(to.X), knee, MetresFromMillimetres(to.Z));
            Vector3 along = end - start;
            float length = along.magnitude;
            if (length <= 0f)
            {
                return false;
            }

            int count = RaycastAll(start, along / length, length);
            for (int i = 0; i < count; i++)
            {
                if (staticByCollider.ContainsKey(sightHits[i].collider.GetInstanceID()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether these two bodies pass through each other: somebody sitting in
        /// a chair and the chair they are in.
        /// </summary>
        public void IgnoreEachOther(int first, int second, bool ignore)
        {
            foreach (Collider a in bodies[first].Colliders)
            {
                foreach (Collider b in bodies[second].Colliders)
                {
                    Physics.IgnoreCollision(a, b, ignore);
                }
            }
        }

        private int Register(Body body)
        {
            int handle = bodies.Count;
            bodies.Add(body);
            foreach (Collider collider in body.Colliders)
            {
                bodyByCollider[collider.GetInstanceID()] = handle;
            }

            ReadBack(handle);
            return handle;
        }

        /// <summary>
        /// No loose thing counts as lighter than this to the engine, in
        /// kilograms. A 70 kg person leaning on a 3 kg box pinned against a wall
        /// is a contest of masses the engine cannot settle in one step: it lets
        /// them sink a hand's width into the box and keeps them there. Seven to
        /// one it settles. The game's own rules (how hard a thrown laptop hits,
        /// what breaks, how far a kicked box slides) still use each thing's
        /// real weight; only the engine's sums see the floor.
        /// </summary>
        private const float LightestLooseThingKilograms = 10f;

        /// <summary>The mass the engine is given for a body: its own, but never less than the floor above for a loose thing.</summary>
        private static float EngineMass(int massGrams, bool looseThing) =>
            Math.Max(looseThing ? LightestLooseThingKilograms : 0.05f, massGrams / 1000f);

        private void Configure(Rigidbody rigidbody, int massGrams)
        {
            rigidbody.mass = Math.Max(0.05f, massGrams / 1000f);

            // Gravity is added by Step, at the run's own strength.
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbody.solverIterations = SolverIterations;
            rigidbody.solverVelocityIterations = SolverVelocityIterations;
            rigidbody.maxLinearVelocity = feel.MaximumSpeedMillimetresPerTick * 0.001f * Run.TicksPerSecond;
            rigidbody.maxAngularVelocity = 30f;
            rigidbody.linearDamping = 0.05f;
            rigidbody.angularDamping = StandingAngularDamping;

            // Two bodies found overlapping (somebody getting up where there
            // was no room, a crowd squeezed into a doorway) are eased apart at
            // no more than two metres a second: quick enough to be clear of
            // each other in a tick or two, slow enough not to be fired apart.
            rigidbody.maxDepenetrationVelocity = 2f;
        }

        private static Collider[] BuildColliders(GameObject thing, ObjectShapes.Part[] parts, PhysicsMaterial material)
        {
            var colliders = new Collider[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                colliders[i] = AddCollider(thing, parts[i]);
                colliders[i].sharedMaterial = material;
                colliders[i].providesContacts = true;
            }

            return colliders;
        }

        private static Collider AddCollider(GameObject thing, ObjectShapes.Part part)
        {
            var centre = new Vector3(
                MetresFromMillimetres(part.CentreX), MetresFromMillimetres(part.CentreY), MetresFromMillimetres(part.CentreZ));
            if (part.Shape == ObjectShapes.PartShape.Sphere)
            {
                var sphere = thing.AddComponent<SphereCollider>();
                sphere.center = centre;
                sphere.radius = MetresFromMillimetres(part.SizeX) * 0.5f;
                return sphere;
            }

            var box = thing.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = new Vector3(
                MetresFromMillimetres(part.SizeX), MetresFromMillimetres(part.SizeY), MetresFromMillimetres(part.SizeZ));
            return box;
        }

        /// <summary>
        /// One shared material per grip and bounce, so a thousand boxes share
        /// one. Grip is the engine's friction coefficient times 100. Where two
        /// things meet the slipperier of the two decides, so a thing on the
        /// floor slides exactly as its own grip says.
        /// </summary>
        private PhysicsMaterial Material(int frictionPercent, int bouncinessPercent)
        {
            if (materials.TryGetValue((frictionPercent, bouncinessPercent), out PhysicsMaterial material))
            {
                return material;
            }

            float friction = frictionPercent / 100f;
            material = new PhysicsMaterial("Paniq " + frictionPercent + "/" + bouncinessPercent)
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = Mathf.Clamp01(bouncinessPercent / 100f),
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average,
                hideFlags = HideFlags.HideAndDontSave
            };
            materials.Add((frictionPercent, bouncinessPercent), material);
            return material;
        }

        public Reading Read(int handle) => bodies[handle].Reading;

        /// <summary>
        /// Whether it takes part in the world at all. A thing somebody is
        /// carrying, or a spare the player has not put down yet, touches
        /// nothing and is held still where the simulation puts it.
        /// </summary>
        public void SetSolid(int handle, bool solid)
        {
            Body body = bodies[handle];
            if (body.Solid == solid)
            {
                return;
            }

            body.Solid = solid;
            foreach (Collider collider in body.Colliders)
            {
                collider.enabled = solid;
            }

            ApplyKinematic(body);
        }

        /// <summary>
        /// Pinned in place by the simulation rather than moved by the engine:
        /// a chair somebody is sitting on. It still gets in everyone's way.
        /// </summary>
        public void SetPinned(int handle, bool pinned)
        {
            Body body = bodies[handle];
            body.Kinematic = pinned;
            ApplyKinematic(body);
        }

        private static void ApplyKinematic(Body body)
        {
            bool kinematic = body.Kinematic || !body.Solid;
            if (body.Rigidbody.isKinematic == kinematic)
            {
                return;
            }

            if (!kinematic)
            {
                body.Rigidbody.isKinematic = false;
                body.Rigidbody.linearVelocity = Vector3.zero;
                body.Rigidbody.angularVelocity = Vector3.zero;
                return;
            }

            body.Rigidbody.linearVelocity = Vector3.zero;
            body.Rigidbody.angularVelocity = Vector3.zero;
            body.Rigidbody.isKinematic = true;
        }

        /// <summary>Puts a body straight at a spot (hundredths of a millimetre), stopped, standing upright at this heading.</summary>
        public void Place(int handle, long x, long y, long z, int heading)
        {
            Body body = bodies[handle];
            var position = new Vector3(Metres(x), Metres(y), Metres(z));
            Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
            body.Rigidbody.position = position;
            body.Rigidbody.rotation = rotation;
            body.Rigidbody.transform.SetPositionAndRotation(position, rotation);
            if (!body.Rigidbody.isKinematic)
            {
                body.Rigidbody.linearVelocity = Vector3.zero;
                body.Rigidbody.angularVelocity = Vector3.zero;
                body.Rigidbody.WakeUp();
            }

            ReadBack(handle);
        }

        /// <summary>
        /// Moves a body the simulation drives (a person, a pinned chair) to
        /// this spot during the next step. Anything loose in the way is shoved
        /// aside by it, as it would be by somebody walking through.
        /// </summary>
        public void Drive(int handle, LogicalPosition position, int heading)
        {
            Body body = bodies[handle];

            // A body that has settled is asleep, and a sleeping body ignores
            // being driven: a chair somebody is tucking in under themselves
            // has been still for a while, so it has to be woken first.
            body.Rigidbody.WakeUp();
            body.Rigidbody.MovePosition(new Vector3(
                MetresFromMillimetres(position.X), body.Rigidbody.position.y, MetresFromMillimetres(position.Z)));
            body.Rigidbody.MoveRotation(Quaternion.Euler(0f, heading, 0f));
        }

        /// <summary>Sets a loose body moving at this velocity (hundredths of a millimetre per tick), waking it.</summary>
        public void SetVelocity(int handle, long vx, long vy, long vz)
        {
            Body body = bodies[handle];
            if (body.Rigidbody.isKinematic)
            {
                return;
            }

            body.Rigidbody.linearVelocity = VelocityInMetres(vx, vy, vz);
            body.Rigidbody.WakeUp();
        }

        /// <summary>Adds this much to a loose body's velocity (hundredths of a millimetre per tick), waking it.</summary>
        public void AddVelocity(int handle, long vx, long vy, long vz)
        {
            Body body = bodies[handle];
            if (body.Rigidbody.isKinematic)
            {
                return;
            }

            body.Rigidbody.AddForce(VelocityInMetres(vx, vy, vz), ForceMode.VelocityChange);
            body.Rigidbody.WakeUp();
        }

        /// <summary>
        /// Sets a loose body turning, in whole degrees per tick about each
        /// axis: X tips it forward, Y turns it on the spot, Z rolls it sideways.
        /// </summary>
        public void SetSpin(int handle, int aboutX, int aboutY, int aboutZ)
        {
            Body body = bodies[handle];
            if (body.Rigidbody.isKinematic)
            {
                return;
            }

            const float radiansPerDegreePerTick = Mathf.Deg2Rad * Run.TicksPerSecond;
            body.Rigidbody.angularVelocity = new Vector3(aboutX, aboutY, aboutZ) * radiansPerDegreePerTick;
            body.Rigidbody.WakeUp();
        }

        private static Vector3 VelocityInMetres(long vx, long vy, long vz)
        {
            float scale = MetresPerUnit * Run.TicksPerSecond;
            return new Vector3(vx * scale, vy * scale, vz * scale);
        }

        /// <summary>Changes a body's mass (a smashed thing is lighter).</summary>
        public void SetMass(int handle, int massGrams)
        {
            bodies[handle].Rigidbody.mass = EngineMass(massGrams, bodies[handle].Parts.Length > 0);
        }

        /// <summary>Shrinks or grows every piece of a body to this percentage of its original size.</summary>
        public void Resize(int handle, int percent)
        {
            Body body = bodies[handle];
            for (int i = 0; i < body.Colliders.Length; i++)
            {
                ObjectShapes.Part part = body.Parts[i].Scaled(percent);
                var centre = new Vector3(
                    MetresFromMillimetres(part.CentreX), MetresFromMillimetres(part.CentreY), MetresFromMillimetres(part.CentreZ));
                switch (body.Colliders[i])
                {
                    case BoxCollider box:
                        box.center = centre;
                        box.size = new Vector3(MetresFromMillimetres(part.SizeX), MetresFromMillimetres(part.SizeY),
                            MetresFromMillimetres(part.SizeZ));
                        break;
                    case SphereCollider sphere:
                        sphere.center = centre;
                        sphere.radius = MetresFromMillimetres(part.SizeX) * 0.5f;
                        break;
                }
            }

            body.Rigidbody.WakeUp();
        }

        // ---------------------------------------------------------------- stepping

        /// <summary>The collisions of the last step, sorted by the bodies involved.</summary>
        public IReadOnlyList<Contact> Contacts => contacts;

        private int deepestPress;

        /// <summary>
        /// How far, in millimetres, the two things pressed deepest into each
        /// other during the last step: nothing solid should ever be more than a
        /// few millimetres inside anything else. For tests and the stats panel.
        /// </summary>
        public int DeepestPressMillimetres => deepestPress;

        private (int, int, StaticKind) deepestPair;

        /// <summary>Which two were pressed deepest: two bodies, or a body and part of the building. For tracking a fault down.</summary>
        public (int BodyA, int BodyB, StaticKind Static) DeepestPressPair => deepestPair;

        /// <summary>
        /// Runs the engine for one tick, then reads every body that moved back
        /// into whole numbers and sorts what touched what.
        /// </summary>
        public void Step()
        {
            // Gravity is pushed onto each body rather than set for the whole
            // engine, so every run can have its own and no run leans on a
            // project setting. A sleeping body is resting on something, and
            // pushing it would only wake it for nothing.
            var gravity = new Vector3(0f, -9.81f * feel.GravityPercent / 100f, 0f);
            for (int handle = 0; handle < bodies.Count; handle++)
            {
                Rigidbody rigidbody = bodies[handle].Rigidbody;
                if (!rigidbody.isKinematic && !rigidbody.IsSleeping())
                {
                    rigidbody.AddForce(gravity, ForceMode.Acceleration);
                }
            }

            for (int table = 0; table < tableBodies.Count; table++)
            {
                if (IsTableAwake(table))
                {
                    tableBodies[table].AddForce(gravity, ForceMode.Acceleration);
                }
            }

            incoming.Clear();
            deepestPress = 0;
            physics.Simulate(StepSeconds);

            for (int handle = 0; handle < bodies.Count; handle++)
            {
                Body body = bodies[handle];
                if (body.Solid && (body.Kinematic || !body.Rigidbody.IsSleeping()))
                {
                    ReadBack(handle);
                }
                else
                {
                    body.Reading.Sleeping = !body.Kinematic && body.Solid;
                    body.Reading.VelocityX = 0L;
                    body.Reading.VelocityY = 0L;
                    body.Reading.VelocityZ = 0L;
                    body.Reading.SpinDegreesPerTick = 0;
                }
            }

            MergeIncoming();
        }

        /// <summary>
        /// One contact per pair of things touching. A chair is three pieces,
        /// so the engine can report it meeting a person three times over, and
        /// with its work spread across threads it lists them in no fixed
        /// order. Folding each pair into one (impulses added up, the hardest
        /// piece's point kept) and sorting the pairs makes the list the same
        /// however the engine happened to write it, and a chair hits somebody
        /// once, not three times.
        /// </summary>
        private void MergeIncoming()
        {
            incoming.Sort(CompareContacts);
            contacts.Clear();
            for (int i = 0; i < incoming.Count; i++)
            {
                Contact next = incoming[i];
                int last = contacts.Count - 1;
                if (last >= 0 && SamePair(contacts[last], next))
                {
                    Contact kept = contacts[last];
                    Contact harder = next.Impulse > kept.Impulse ? next : kept;
                    contacts[last] = new Contact(kept.BodyA, kept.BodyB, kept.Static, kept.StaticIndex,
                        kept.Impulse + next.Impulse, harder.Point, harder.PointHeight, false);
                    continue;
                }

                contacts.Add(next);
            }

            // Whether a pair has only just met is worked out here, from which
            // pairs were touching last step, rather than taken from the
            // engine. The engine's own flag can say a pair met again when all
            // that happened is one of them changed shape (a chair smashed into
            // wreckage), and it does not always say so the same way twice.
            wasTouching.Clear();
            (wasTouching, touching) = (touching, wasTouching);
            for (int i = 0; i < contacts.Count; i++)
            {
                Contact contact = contacts[i];
                long key = PairKey(contact);
                touching.Add(key);
                contacts[i] = new Contact(contact.BodyA, contact.BodyB, contact.Static, contact.StaticIndex,
                    contact.Impulse, contact.Point, contact.PointHeight, !wasTouching.Contains(key));
            }
        }

        /// <summary>The pairs touching during the last step, and the step before, as <see cref="PairKey"/>s.</summary>
        private HashSet<long> touching = new HashSet<long>();
        private HashSet<long> wasTouching = new HashSet<long>();

        /// <summary>One number for a pair of things touching: the two bodies, or the body and which piece of the building.</summary>
        private static long PairKey(Contact contact)
        {
            long other = contact.BodyB >= 0 ? contact.BodyB : -1 - ((int)contact.Static * 100000L + contact.StaticIndex);
            return ((long)contact.BodyA << 32) ^ (other & 0xFFFFFFFFL);
        }

        private static bool SamePair(Contact left, Contact right) =>
            left.BodyA == right.BodyA && left.BodyB == right.BodyB && left.Static == right.Static &&
            left.StaticIndex == right.StaticIndex;

        /// <summary>
        /// A total order: by the pair, then by everything else, so that pieces
        /// of the same pair land in the same order whatever order they came in.
        /// </summary>
        private static int CompareContacts(Contact left, Contact right)
        {
            int order = left.BodyA.CompareTo(right.BodyA);
            if (order == 0) order = left.BodyB.CompareTo(right.BodyB);
            if (order == 0) order = ((int)left.Static).CompareTo((int)right.Static);
            if (order == 0) order = left.StaticIndex.CompareTo(right.StaticIndex);
            if (order == 0) order = right.Impulse.CompareTo(left.Impulse);
            if (order == 0) order = left.Point.X.CompareTo(right.Point.X);
            if (order == 0) order = left.Point.Z.CompareTo(right.Point.Z);
            if (order == 0) order = left.PointHeight.CompareTo(right.PointHeight);
            if (order == 0) order = left.Began.CompareTo(right.Began);
            return order;
        }

        private void ReadBack(int handle)
        {
            Body body = bodies[handle];
            Rigidbody rigidbody = body.Rigidbody;
            Vector3 position = rigidbody.position;
            Quaternion rotation = rigidbody.rotation;
            Vector3 velocity = rigidbody.isKinematic ? Vector3.zero : rigidbody.linearVelocity;
            Vector3 spin = rigidbody.isKinematic ? Vector3.zero : rigidbody.angularVelocity;
            float perTick = 1f / (MetresPerUnit * Run.TicksPerSecond);

            ref Reading reading = ref body.Reading;
            reading.X = Units(position.x);
            reading.Y = Units(position.y);
            reading.Z = Units(position.z);
            Vector3 centre = rigidbody.worldCenterOfMass;
            reading.CentreX = Units(centre.x);
            reading.CentreZ = Units(centre.z);
            reading.VelocityX = (long)Math.Round(velocity.x * perTick);
            reading.VelocityY = (long)Math.Round(velocity.y * perTick);
            reading.VelocityZ = (long)Math.Round(velocity.z * perTick);
            reading.RotationX = (int)Math.Round(rotation.x * RotationScale);
            reading.RotationY = (int)Math.Round(rotation.y * RotationScale);
            reading.RotationZ = (int)Math.Round(rotation.z * RotationScale);
            reading.RotationW = (int)Math.Round(rotation.w * RotationScale);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;

            // A thing standing on its head still faces somewhere; a thing
            // lying flat on its back faces straight up, and its top is the way
            // it is pointing across the floor.
            Vector3 across = Mathf.Abs(forward.y) < 0.9f ? forward : up * -Mathf.Sign(forward.y);
            reading.Heading = IntegerMath.HeadingOf(
                (long)Math.Round(across.x * RotationScale), (long)Math.Round(across.z * RotationScale), reading.Heading);
            reading.UprightPercent = (int)Math.Round(up.y * 100f);
            reading.SpinDegreesPerTick = (int)Math.Round(spin.magnitude * Mathf.Rad2Deg / Run.TicksPerSecond);
            reading.Sleeping = !rigidbody.isKinematic && rigidbody.IsSleeping();

            float bottom = float.MaxValue;
            foreach (Collider collider in body.Colliders)
            {
                if (collider.enabled)
                {
                    bottom = Mathf.Min(bottom, collider.bounds.min.y);
                }
            }

            reading.BottomMillimetres = bottom == float.MaxValue ? 0 : (int)Math.Round(bottom * 1000f);
        }

        private static long Units(float metres) => (long)Math.Round(metres / MetresPerUnit);

        /// <summary>
        /// The engine's list of what touched what, turned into the
        /// simulation's terms as it arrives. Only contacts in this run's own
        /// scene are kept.
        /// </summary>
        private void OnContacts(PhysicsScene reported, NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            if (reported != physics)
            {
                return;
            }

            // Kilogram-metres per second to kilogram-millimetres per tick.
            const float impulseScale = 1000f / Run.TicksPerSecond;
            for (int h = 0; h < headers.Length; h++)
            {
                ContactPairHeader header = headers[h];
                for (int p = 0; p < header.pairCount; p++)
                {
                    ref readonly ContactPair pair = ref header.GetContactPair(p);
                    if (pair.isCollisionExit)
                    {
                        continue;
                    }

                    int a = WhoseCollider(pair.colliderInstanceID, out (StaticKind Kind, int Index) staticA);
                    int b = WhoseCollider(pair.otherColliderInstanceID, out (StaticKind Kind, int Index) staticB);
                    if (a < 0 && b < 0)
                    {
                        continue;
                    }

                    (StaticKind Kind, int Index) building = default;
                    if (a < 0)
                    {
                        a = b;
                        b = -1;
                        building = staticA;
                    }
                    else if (b < 0)
                    {
                        building = staticB;
                    }
                    else if (b < a)
                    {
                        (a, b) = (b, a);
                    }

                    Vector3 point = pair.contactCount > 0 ? pair.GetContactPoint(0).position : Vector3.zero;
                    for (int c = 0; c < pair.contactCount; c++)
                    {
                        // Separation below zero is how far the two are inside each other.
                        int press = (int)Math.Round(-pair.GetContactPoint(c).separation * 1000f);
                        if (press > deepestPress)
                        {
                            deepestPress = press;
                            deepestPair = (a, b, b < 0 ? building.Kind : StaticKind.None);
                        }
                    }

                    long impulse = (long)Math.Round(pair.impulseSum.magnitude * impulseScale);
                    incoming.Add(new Contact(a, b, b < 0 ? building.Kind : StaticKind.None, b < 0 ? building.Index : 0,
                        impulse,
                        new LogicalPosition((int)Math.Round(point.x * 1000f), (int)Math.Round(point.z * 1000f)),
                        (int)Math.Round(point.y * 1000f), pair.isCollisionEnter));
                }
            }
        }

        private int WhoseCollider(int colliderId, out (StaticKind Kind, int Index) building)
        {
            if (bodyByCollider.TryGetValue(colliderId, out int handle))
            {
                building = default;
                return handle;
            }

            building = staticByCollider.TryGetValue(colliderId, out (StaticKind, int) found) ? found : (StaticKind.None, 0);
            return -1;
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && (value < 0L) != (divisor < 0L) ? quotient - 1L : quotient;
        }
    }
}
