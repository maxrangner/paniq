using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The floor and walls from the scenario's room, each wall split around
    /// its doors, a door leaf in every gap, a strip of ground outside, and
    /// the tables.
    /// Door leaves swing open, judder when shoved, fall flat when broken
    /// down, and keep a collider only so a click can find which door was hit.
    /// </summary>
    internal sealed class RoomView
    {
        private const float WallHeight = 1.5f;
        private const float WallThickness = 0.4f;
        private const float DoorHeight = 1.3f;
        private const float DoorSwingSeconds = 0.3f;

        private static readonly Color UnlockedDoorColor = new Color(0.18f, 0.8f, 0.3f);
        private static readonly Color BrokenDoorColor = new Color(0.42f, 0.3f, 0.2f);
        private const float DoorFallSeconds = 0.25f;

        private sealed class DoorView
        {
            public SimulationId DoorId;
            public Transform Hinge;
            public Vector3 HingePosition;
            public float Fall;
            public Renderer Leaf;
            public float ClosedYaw;

            /// <summary>Where the leaf sits open on each side of the frame.</summary>
            public float OpenYawOut;
            public float OpenYawIn;
            public float Swing;

            /// <summary>Which way the leaf swung: +1 out of the room whose wall holds it, -1 into it.</summary>
            public int OpenSide = 1;

            /// <summary>The way out of the door's own room, in world space.</summary>
            public Vector3 OutwardDirection;
            public float ShakeStart = -10f;
            public DoorState State;
            public int DamagePercent;
        }

        private readonly PresentationMaterials materials;
        private readonly ParticleEffects effects;
        private readonly Transform parent;
        private readonly ScenarioData scenario;
        private readonly Dictionary<SimulationId, DoorView> doors = new Dictionary<SimulationId, DoorView>();
        private readonly Dictionary<Collider, SimulationId> doorByCollider = new Dictionary<Collider, SimulationId>();
        private readonly Dictionary<Collider, SimulationId> alarmByCollider = new Dictionary<Collider, SimulationId>();

        private sealed class TableView
        {
            public Renderer[] Parts;
            public FlameEmitter Flames;
            public float Width;
            public float Depth;

            /// <summary>The whole table, so one going over can be turned bodily.</summary>
            public Transform Root;

            /// <summary>Where it stands, so a table going over can drop from it.</summary>
            public Vector3 RestingPosition;

            /// <summary>Its ID, so which way it tips is the same every run.</summary>
            public SimulationId TableId;
        }

        /// <summary>Every table's parts (top and legs), recoloured as it heats, burns and chars.</summary>
        private readonly Dictionary<SimulationId, TableView> tables = new Dictionary<SimulationId, TableView>();

        /// <summary>The boxes on the walls, which all flash together.</summary>
        private readonly List<Renderer> alarms = new List<Renderer>();

        /// <summary>
        /// One built piece of wall and the span it covers, so a hole blasted
        /// through it later can cut it back.
        /// </summary>
        private sealed class WallPieceView
        {
            public bool AlongX;
            public float Line;
            public float From;
            public float To;
            public Transform Piece;
        }

        private readonly List<WallPieceView> wallPieces = new List<WallPieceView>();

        /// <summary>Holes already built, so each one is built only once.</summary>
        private readonly HashSet<SimulationId> holesDrawn = new HashSet<SimulationId>();

        private static readonly Color RubbleColor = new Color(0.45f, 0.42f, 0.40f);

        private static readonly Color AlarmRestingColor = new Color(0.75f, 0.12f, 0.12f);

        public RoomView(ScenarioData scenario, PresentationMaterials materials, ParticleEffects effects,
            Transform parent)
        {
            this.scenario = scenario;
            this.materials = materials;
            this.effects = effects;
            this.parent = parent;
            Build();
        }

        private void Build()
        {
            foreach (RoomDefinition room in scenario.Rooms)
            {
                CreateRoom(room);
            }

            foreach (TableDefinition table in scenario.Tables)
            {
                CreateTable(table);
            }

            foreach (AlarmDefinition alarm in scenario.Alarms)
            {
                CreateAlarm(alarm);
            }
        }

        /// <summary>
        /// A small red box on the wall. It sits still until somebody hits it,
        /// and then flashes for the rest of the run so the player can see at a
        /// glance that the building has been told.
        /// </summary>
        private void CreateAlarm(AlarmDefinition alarm)
        {
            const float height = 1.1f;
            Vector3 at = ToUnityPosition(alarm.Position) + Vector3.up * height;
            GameObject box = CreatePrimitive($"Fire alarm {alarm.AlarmId.Value} (presentation)", PrimitiveType.Cube,
                parent, at, new Vector3(0.18f, 0.24f, 0.18f), materials.Box);
            var view = box.GetComponent<Renderer>();
            materials.SetColor(view, AlarmRestingColor);
            alarms.Add(view);

            // The player can pull it (2026-09-24), so it needs a collider to
            // click, and a bigger one than the box: the box is a hand's width,
            // and a click on a hand's width from across the room is a miss.
            // Half a metre about the box, in the box's own scaled units.
            var handle = box.AddComponent<BoxCollider>();
            handle.size = new Vector3(2.5f, 2f, 2.5f);
            alarmByCollider.Add(handle, alarm.AlarmId);
        }

        /// <summary>Which fire alarm a clicked collider belongs to, if any.</summary>
        public bool TryGetAlarm(Collider collider, out SimulationId alarmId)
        {
            return alarmByCollider.TryGetValue(collider, out alarmId);
        }

        /// <summary>Every bell flashes while the alarms are ringing.</summary>
        public void UpdateAlarms(RunSnapshot snapshot, float time)
        {
            for (int i = 0; i < alarms.Count; i++)
            {
                Color colour = AlarmRestingColor;
                if (snapshot.AlarmsRinging)
                {
                    // Two flashes a second, bright enough to catch the eye.
                    float pulse = Mathf.Repeat(time * 4f, 2f) < 1f ? 1f : 0.25f;
                    colour = Color.Lerp(AlarmRestingColor, Color.white, pulse);
                }

                materials.SetColor(alarms[i], colour);
            }
        }

        /// <summary>
        /// One room: its floor, and its four walls with a gap cut wherever a
        /// door crosses them. A wall shared with the next room is drawn by
        /// both, so each cuts the gaps of every door along that wall line;
        /// the swinging leaf itself belongs to the room that holds the door.
        /// </summary>
        private void CreateRoom(RoomDefinition room)
        {
            float minX = Metres(room.Bounds.MinX);
            float maxX = Metres(room.Bounds.MaxX);
            float minZ = Metres(room.Bounds.MinZ);
            float maxZ = Metres(room.Bounds.MaxZ);
            string name = $"Room {room.RoomId.Value}";
            CreatePrimitive($"{name} floor", PrimitiveType.Cube, parent,
                new Vector3((minX + maxX) * 0.5f, -0.05f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.1f, maxZ - minZ), materials.Room);

            foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West })
            {
                bool alongX = side == WallSide.North || side == WallSide.South;
                float wallLine = side == WallSide.North ? maxZ : side == WallSide.South ? minZ : side == WallSide.East ? maxX : minX;
                float start = (alongX ? minX : minZ) - WallThickness * 0.5f;
                float end = (alongX ? maxX : maxZ) + WallThickness * 0.5f;

                var gaps = new List<DoorDefinition>();
                foreach (DoorDefinition door in scenario.Doors)
                {
                    if (CrossesWall(door, alongX, wallLine, start, end))
                    {
                        gaps.Add(door);
                    }
                }

                gaps.Sort((left, right) => left.CentreAlongWallMillimetres.CompareTo(right.CentreAlongWallMillimetres));
                float cursor = start;
                int piece = 1;
                foreach (DoorDefinition door in gaps)
                {
                    float gapStart = Metres(door.CentreAlongWallMillimetres - door.WidthMillimetres / 2);
                    CreateWallPiece(side, piece++, alongX, wallLine, cursor, gapStart, name);
                    cursor = Metres(door.CentreAlongWallMillimetres + door.WidthMillimetres / 2);
                    // An archway is a gap and nothing else: the wall is cut
                    // around it exactly as it is for a door, but there is no
                    // leaf to hang, nothing to swing and nothing to click.
                    if (door.RoomId == room.RoomId && !door.IsOpening)
                    {
                        CreateDoor(door, alongX, wallLine);
                    }
                }

                CreateWallPiece(side, piece, alongX, wallLine, cursor, end, name);
            }
        }

        /// <summary>Whether this door's gap lies in a wall along <paramref name="wallLine"/>, between the two ends.</summary>
        private bool CrossesWall(DoorDefinition door, bool alongX, float wallLine, float from, float to)
        {
            bool doorAlongX = door.Side == WallSide.North || door.Side == WallSide.South;
            if (doorAlongX != alongX)
            {
                return false;
            }

            LogicalBounds owner = RoomBoundsOf(door.RoomId);
            float doorLine = Metres(door.Side == WallSide.North ? owner.MaxZ
                : door.Side == WallSide.South ? owner.MinZ
                : door.Side == WallSide.East ? owner.MaxX
                : owner.MinX);
            float half = Metres(door.WidthMillimetres) * 0.5f;
            float centre = Metres(door.CentreAlongWallMillimetres);
            return Mathf.Approximately(doorLine, wallLine) && centre - half >= from && centre + half <= to;
        }

        private LogicalBounds RoomBoundsOf(SimulationId roomId)
        {
            foreach (RoomDefinition room in scenario.Rooms)
            {
                if (room.RoomId == roomId)
                {
                    return room.Bounds;
                }
            }

            return default;
        }

        /// <summary>Whether a door leads out of the building: no room lies beyond its wall.</summary>
        private bool LeadsOutside(DoorDefinition door)
        {
            LogicalBounds owner = RoomBoundsOf(door.RoomId);
            bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
            int half = door.WidthMillimetres / 2;
            foreach (RoomDefinition other in scenario.Rooms)
            {
                if (other.RoomId == door.RoomId)
                {
                    continue;
                }

                LogicalBounds b = other.Bounds;
                bool flush;
                switch (door.Side)
                {
                    case WallSide.North:
                        flush = b.MinZ == owner.MaxZ;
                        break;
                    case WallSide.South:
                        flush = b.MaxZ == owner.MinZ;
                        break;
                    case WallSide.East:
                        flush = b.MinX == owner.MaxX;
                        break;
                    default:
                        flush = b.MaxX == owner.MinX;
                        break;
                }

                int otherMin = alongX ? b.MinX : b.MinZ;
                int otherMax = alongX ? b.MaxX : b.MaxZ;
                if (flush && otherMin <= door.CentreAlongWallMillimetres - half &&
                    otherMax >= door.CentreAlongWallMillimetres + half)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A plain wooden table: a thin top on four legs.</summary>
        /// <summary>How high a table top is, in metres: where a laptop on it is drawn.</summary>
        public const float TableHeight = 0.74f;

        private void CreateTable(TableDefinition table)
        {
            const float height = TableHeight;
            const float topThickness = 0.06f;
            const float leg = 0.06f;
            float width = Metres(table.WidthMillimetres);
            float depth = Metres(table.DepthMillimetres);
            Vector3 centre = ToUnityPosition(table.Centre);
            var root = new GameObject($"Table {table.TableId.Value} (presentation)").transform;
            root.SetParent(parent, false);
            root.position = centre;

            var renderers = new List<Renderer>
            {
                CreatePrimitive("Top", PrimitiveType.Cube, root, centre + Vector3.up * (height - topThickness * 0.5f),
                    new Vector3(width, topThickness, depth), materials.Box).GetComponent<Renderer>()
            };
            float legX = width * 0.5f - leg;
            float legZ = depth * 0.5f - leg;
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                renderers.Add(CreatePrimitive("Leg", PrimitiveType.Cube, root,
                    centre + new Vector3(corner.x * legX, (height - topThickness) * 0.5f, corner.y * legZ),
                    new Vector3(leg, height - topThickness, leg), materials.Box).GetComponent<Renderer>());
            }

            Renderer[] parts = renderers.ToArray();
            foreach (Renderer part in parts)
            {
                materials.SetColor(part, PresentationMaterials.WoodColor);
            }

            tables.Add(table.TableId, new TableView
            {
                Parts = parts,
                Flames = new FlameEmitter(root, 10, effects, materials),
                Width = width,
                Depth = depth,
                Root = root,
                RestingPosition = root.localPosition,
                TableId = table.TableId
            });
        }

        private void CreateWallPiece(WallSide side, int piece, bool alongX, float wallLine, float from, float to,
            string owner = "Room")
        {
            if (to - from <= 0.001f)
            {
                return;
            }

            float middle = (from + to) * 0.5f;
            Vector3 position = alongX ? new Vector3(middle, WallHeight * 0.5f, wallLine) : new Vector3(wallLine, WallHeight * 0.5f, middle);
            Vector3 scale = alongX ? new Vector3(to - from, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, to - from);
            GameObject built = CreatePrimitive($"{owner} Wall {side} {piece}", PrimitiveType.Cube, parent, position, scale,
                materials.Wall);
            MarkAsWall(built, materials);
            wallPieces.Add(new WallPieceView
            {
                AlongX = alongX,
                Line = wallLine,
                From = from,
                To = to,
                Piece = built.transform
            });
        }

        /// <summary>
        /// Holes the player has blasted since the last frame. A door this view has
        /// never seen before is one of them.
        /// </summary>
        public void UpdateHoles(RunSnapshot snapshot)
        {
            foreach (DoorSnapshot door in snapshot.Doors)
            {
                if (door.IsHole && !holesDrawn.Contains(door.DoorId))
                {
                    CreateHole(door);
                }
            }
        }

        /// <summary>
        /// A hole blasted through a wall: every wall piece it crosses is cut back
        /// (and split in two where the hole is in the middle of one), and rubble is
        /// left in the gap. A hole has no leaf, so nothing swings and nothing can
        /// be clicked.
        /// </summary>
        private void CreateHole(DoorSnapshot hole)
        {
            holesDrawn.Add(hole.DoorId);
            bool alongX = hole.Side == WallSide.North || hole.Side == WallSide.South;
            float line = alongX ? Metres(hole.Centre.Z) : Metres(hole.Centre.X);
            float centre = alongX ? Metres(hole.Centre.X) : Metres(hole.Centre.Z);
            float half = Metres(hole.WidthMillimetres) * 0.5f;
            float gapFrom = centre - half;
            float gapTo = centre + half;

            // Both rooms either side of a shared wall drew it, so there can be
            // more than one piece to cut.
            for (int i = wallPieces.Count - 1; i >= 0; i--)
            {
                WallPieceView wall = wallPieces[i];
                if (wall.AlongX != alongX || Mathf.Abs(wall.Line - line) > 0.01f ||
                    wall.To <= gapFrom || wall.From >= gapTo)
                {
                    continue;
                }

                float leftTo = Mathf.Min(wall.To, gapFrom);
                float rightFrom = Mathf.Max(wall.From, gapTo);
                Object.Destroy(wall.Piece.gameObject);
                wallPieces.RemoveAt(i);
                CreateWallPiece(hole.Side, 90, alongX, wall.Line, wall.From, leftTo, "Blasted");
                CreateWallPiece(hole.Side, 91, alongX, wall.Line, rightFrom, wall.To, "Blasted");
            }

            // Rubble in the gap, laid out from the hole's own ID so it looks the
            // same every run without touching the simulation's randomness.
            var root = new GameObject($"Blast hole {hole.DoorId.Value} (presentation)").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < 5; i++)
            {
                float along = Mathf.Lerp(gapFrom + 0.15f, gapTo - 0.15f, Hash01((int)hole.DoorId.Value, i, 31));
                float across = (Hash01((int)hole.DoorId.Value, i, 71) - 0.5f) * 0.7f;
                float size = 0.12f + Hash01((int)hole.DoorId.Value, i, 17) * 0.16f;
                Vector3 at = alongX
                    ? new Vector3(along, size * 0.5f, line + across)
                    : new Vector3(line + across, size * 0.5f, along);
                GameObject lump = CreatePrimitive($"Rubble {i}", PrimitiveType.Cube, root, at,
                    new Vector3(size, size, size), materials.Wall);
                materials.SetColor(lump.GetComponent<Renderer>(), RubbleColor);
                lump.transform.rotation = Quaternion.Euler(0f, Hash01((int)hole.DoorId.Value, i, 53) * 90f, 0f);
            }

            if (!hole.LeadsOutside)
            {
                return;
            }

            // The ground beyond it, so a way out reads as a way out.
            float depth = Metres(scenario.Exits.DoorwayDepthMillimetres);
            float outward = hole.Side == WallSide.North || hole.Side == WallSide.East ? 1f : -1f;
            Vector3 stripAt = alongX
                ? new Vector3(centre, 0.01f, line + outward * depth * 0.5f)
                : new Vector3(line + outward * depth * 0.5f, 0.01f, centre);
            Vector3 stripSize = alongX
                ? new Vector3(half * 2f, 0.02f, depth)
                : new Vector3(depth, 0.02f, half * 2f);
            CreatePrimitive("Blasted ground", PrimitiveType.Cube, root, stripAt, stripSize, materials.Outside);
        }

        /// <summary>A door leaf hinged at one side of the gap, which swings outward when the door opens.</summary>
        private void CreateDoor(DoorDefinition door, bool alongX, float wallLine)
        {
            float width = Metres(door.WidthMillimetres);
            float centre = Metres(door.CentreAlongWallMillimetres);
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 outward = door.Side == WallSide.North ? Vector3.forward
                : door.Side == WallSide.South ? Vector3.back
                : door.Side == WallSide.East ? Vector3.right
                : Vector3.left;
            Vector3 gapCentre = alongX ? new Vector3(centre, 0f, wallLine) : new Vector3(wallLine, 0f, centre);

            var hinge = new GameObject($"Door {door.DoorId.Value} hinge (presentation)").transform;
            hinge.SetParent(parent, false);
            hinge.position = gapCentre - along * (width * 0.5f);
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = $"Door {door.DoorId.Value} (click target)";
            leaf.transform.SetParent(hinge, false);
            leaf.transform.localPosition = new Vector3(width * 0.5f, DoorHeight * 0.5f, 0f);
            leaf.transform.localScale = new Vector3(width - 0.04f, DoorHeight, 0.08f);
            Renderer leafRenderer = leaf.GetComponent<Renderer>();
            leafRenderer.sharedMaterial = materials.Door;

            // A shut door hides whoever is behind it just as a wall does.
            MarkAsWall(leaf, materials);

            // A strip of ground outside, as far as the doorway reaches (an inside door opens into the next room's floor).
            float depth = Metres(scenario.Exits.DoorwayDepthMillimetres);
            if (LeadsOutside(door))
            {
                CreatePrimitive($"Door {door.DoorId.Value} outside ground", PrimitiveType.Cube, parent,
                    gapCentre + outward * (depth * 0.5f + WallThickness * 0.25f) + Vector3.down * 0.05f,
                    alongX ? new Vector3(width + 0.4f, 0.1f, depth) : new Vector3(depth, 0.1f, width + 0.4f),
                    materials.Outside);
            }

            var view = new DoorView
            {
                DoorId = door.DoorId,
                Hinge = hinge,
                HingePosition = hinge.position,


                Leaf = leafRenderer,
                ClosedYaw = YawOf(along),

                // A door swings both ways, so the leaf has an open position on
                // each side of the frame and takes whichever one the simulation
                // says it went: away from whoever pushed it.
                OpenYawOut = YawOf(outward),
                OpenYawIn = YawOf(-outward),
                OutwardDirection = outward,
                State = DoorState.Locked
            };
            hinge.rotation = Quaternion.Euler(0f, view.ClosedYaw, 0f);
            doors.Add(door.DoorId, view);
            doorByCollider.Add(leaf.GetComponent<Collider>(), door.DoorId);
        }

        /// <summary>Which door a clicked collider belongs to, if any.</summary>
        public bool TryGetDoor(Collider collider, out SimulationId doorId)
        {
            return doorByCollider.TryGetValue(collider, out doorId);
        }

        /// <summary>The door's state as last shown.</summary>
        public DoorState StateOf(SimulationId doorId) => doors[doorId].State;

        /// <summary>A shove makes a shut door judder in its frame.</summary>
        public void Shake(SimulationId doorId, float time)
        {
            if (doors.TryGetValue(doorId, out DoorView view))
            {
                view.ShakeStart = time;
            }
        }

        public void Update(RunSnapshot snapshot, SimulationId? hoveredDoor, float time, float deltaTime)
        {
            foreach (DoorSnapshot door in snapshot.Doors)
            {
                if (doors.TryGetValue(door.DoorId, out DoorView known))
                {
                    known.State = door.State;
                    // Battered or burnt: either way the leaf darkens as it goes.
                    known.DamagePercent = door.FailingPercent;
                    if (door.OpenSide != 0)
                    {
                        known.OpenSide = door.OpenSide;
                    }
                }
            }

            foreach (TableSnapshot table in snapshot.Tables)
            {
                if (!tables.TryGetValue(table.TableId, out TableView view))
                {
                    continue;
                }

                Color colour = BoxViews.BurnColour(PresentationMaterials.WoodColor, table.BurnState, table.HeatPercent);
                foreach (Renderer part in view.Parts)
                {
                    materials.SetColor(part, colour);
                }

                // Drawn where the physics has it: shoved, tipped up on one
                // edge or flat on its back, whatever happened to it. Eased
                // towards the latest reading rather than snapped to it, so it
                // moves smoothly between ticks.
                if (table.Pose.IsKnown)
                {
                    Vector3 origin = BoxViews.PoseOrigin(table.Pose);
                    Quaternion turned = BoxViews.PoseRotation(table.Pose);
                    float follow = 1f - Mathf.Exp(-deltaTime * 20f);
                    view.Root.SetPositionAndRotation(
                        Vector3.Lerp(view.Root.position, origin, follow),
                        Quaternion.Slerp(view.Root.rotation, turned, follow));
                }

                view.Flames.Update(table.BurnState == ObjectBurnState.Burning, new Vector3(0f, 0.72f, 0f),
                    new Vector3(view.Width * 0.4f, 0.8f, view.Depth * 0.4f), 0.2f);
            }

            foreach (DoorView view in doors.Values)
            {
                float target = view.State == DoorState.Open ? 1f : 0f;
                view.Swing = Mathf.MoveTowards(view.Swing, target, deltaTime / DoorSwingSeconds);
                float openYaw = view.OpenSide >= 0 ? view.OpenYawOut : view.OpenYawIn;
                float yaw = Mathf.LerpAngle(view.ClosedYaw, openYaw, Mathf.SmoothStep(0f, 1f, view.Swing));

                float shakeAge = time - view.ShakeStart;
                if (shakeAge < 0.3f && view.State != DoorState.Open && view.State != DoorState.Broken)
                {
                    yaw += Mathf.Sin(shakeAge * 70f) * 4f * (1f - shakeAge / 0.3f);
                }

                view.Hinge.rotation = Quaternion.Euler(0f, yaw, 0f);
                view.Hinge.position = view.HingePosition;
                if (view.State == DoorState.Broken)
                {
                    // Burst off its hinges: the leaf slams down flat outside the doorway.
                    view.Fall = Mathf.MoveTowards(view.Fall, 1f, deltaTime / DoorFallSeconds);

                    // It falls flat on the side it came off, which is the side it
                    // would have swung to.
                    Vector3 fallTowards = view.OpenSide >= 0 ? view.OutwardDirection : -view.OutwardDirection;
                    float fallDirection = Vector3.Dot(
                        Quaternion.Euler(0f, view.ClosedYaw, 0f) * Vector3.forward, fallTowards) >= 0f ? 1f : -1f;
                    float tip = 90f * fallDirection * view.Fall * view.Fall;
                    view.Hinge.rotation = Quaternion.Euler(0f, view.ClosedYaw, 0f) * Quaternion.Euler(tip, 0f, 0f);
                    view.Hinge.position = view.HingePosition + Vector3.up * (0.05f * view.Fall);
                }

                Color color = view.State == DoorState.Locked ? PresentationMaterials.LockedDoorColor
                    : view.State == DoorState.Broken ? BrokenDoorColor
                    : UnlockedDoorColor;

                // A battered door darkens toward splintered wood as it weakens.
                if (view.State != DoorState.Broken && view.DamagePercent > 0)
                {
                    color = Color.Lerp(color, BrokenDoorColor, view.DamagePercent / 100f * 0.8f);
                }
                if (hoveredDoor.HasValue && hoveredDoor.Value == view.DoorId)
                {
                    color = Color.Lerp(color, Color.white, 0.3f);
                }

                materials.SetColors(view.Leaf, color, color * 0.35f);
            }
        }
    }
}
