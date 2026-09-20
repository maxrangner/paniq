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
            public float FallDirection;
            public float Fall;
            public Renderer Leaf;
            public float ClosedYaw;
            public float OpenYaw;
            public float Swing;
            public float ShakeStart = -10f;
            public DoorState State;
            public int DamagePercent;
        }

        private readonly PresentationMaterials materials;
        private readonly Transform parent;
        private readonly FireReactionScenarioData scenario;
        private readonly Dictionary<SimulationId, DoorView> doors = new Dictionary<SimulationId, DoorView>();
        private readonly Dictionary<Collider, SimulationId> doorByCollider = new Dictionary<Collider, SimulationId>();

        private sealed class TableView
        {
            public Renderer[] Parts;
            public FlameCubes Flames;
            public float Width;
            public float Depth;
        }

        /// <summary>Every table's parts (top and legs), recoloured as it heats, burns and chars.</summary>
        private readonly Dictionary<SimulationId, TableView> tables = new Dictionary<SimulationId, TableView>();

        public RoomView(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            this.scenario = scenario;
            this.materials = materials;
            this.parent = parent;
            Build();
        }

        private void Build()
        {
            foreach (FireReactionRoomDefinition room in scenario.Rooms)
            {
                CreateRoom(room);
            }

            foreach (FireReactionTableDefinition table in scenario.Tables)
            {
                CreateTable(table);
            }
        }

        /// <summary>
        /// One room: its floor, and its four walls with a gap cut wherever a
        /// door crosses them. A wall shared with the next room is drawn by
        /// both, so each cuts the gaps of every door along that wall line;
        /// the swinging leaf itself belongs to the room that holds the door.
        /// </summary>
        private void CreateRoom(FireReactionRoomDefinition room)
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

                var gaps = new List<FireReactionDoorDefinition>();
                foreach (FireReactionDoorDefinition door in scenario.Doors)
                {
                    if (CrossesWall(door, alongX, wallLine, start, end))
                    {
                        gaps.Add(door);
                    }
                }

                gaps.Sort((left, right) => left.CentreAlongWallMillimetres.CompareTo(right.CentreAlongWallMillimetres));
                float cursor = start;
                int piece = 1;
                foreach (FireReactionDoorDefinition door in gaps)
                {
                    float gapStart = Metres(door.CentreAlongWallMillimetres - door.WidthMillimetres / 2);
                    CreateWallPiece(side, piece++, alongX, wallLine, cursor, gapStart, name);
                    cursor = Metres(door.CentreAlongWallMillimetres + door.WidthMillimetres / 2);
                    if (door.RoomId == room.RoomId)
                    {
                        CreateDoor(door, alongX, wallLine);
                    }
                }

                CreateWallPiece(side, piece, alongX, wallLine, cursor, end, name);
            }
        }

        /// <summary>Whether this door's gap lies in a wall along <paramref name="wallLine"/>, between the two ends.</summary>
        private bool CrossesWall(FireReactionDoorDefinition door, bool alongX, float wallLine, float from, float to)
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
            foreach (FireReactionRoomDefinition room in scenario.Rooms)
            {
                if (room.RoomId == roomId)
                {
                    return room.Bounds;
                }
            }

            return default;
        }

        /// <summary>Whether a door leads out of the building: no room lies beyond its wall.</summary>
        private bool LeadsOutside(FireReactionDoorDefinition door)
        {
            LogicalBounds owner = RoomBoundsOf(door.RoomId);
            bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
            int half = door.WidthMillimetres / 2;
            foreach (FireReactionRoomDefinition other in scenario.Rooms)
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
        private void CreateTable(FireReactionTableDefinition table)
        {
            const float height = 0.74f;
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
                Flames = new FlameCubes(root, 10, materials, table.TableId.Value % 83UL),
                Width = width,
                Depth = depth
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
            CreatePrimitive($"{owner} Wall {side} {piece}", PrimitiveType.Cube, parent, position, scale, materials.Wall);
        }

        /// <summary>A door leaf hinged at one side of the gap, which swings outward when the door opens.</summary>
        private void CreateDoor(FireReactionDoorDefinition door, bool alongX, float wallLine)
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

                // Which way round the leaf must tip about the wall line to fall outward.
                FallDirection = Vector3.Dot(Quaternion.Euler(0f, YawOf(along), 0f) * Vector3.forward, outward) >= 0f ? 1f : -1f,
                Leaf = leafRenderer,
                ClosedYaw = YawOf(along),
                OpenYaw = YawOf(outward),
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

        public void Update(FireReactionSnapshot snapshot, SimulationId? hoveredDoor, float time, float deltaTime)
        {
            foreach (FireReactionDoorSnapshot door in snapshot.Doors)
            {
                if (doors.TryGetValue(door.DoorId, out DoorView known))
                {
                    known.State = door.State;
                    known.DamagePercent = door.DamagePercent;
                }
            }

            foreach (FireReactionTableSnapshot table in snapshot.Tables)
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

                view.Flames.Update(table.BurnState == ObjectBurnState.Burning, time, new Vector3(0f, 0.72f, 0f),
                    new Vector3(view.Width * 0.4f, 0.8f, view.Depth * 0.4f), 0.2f);
            }

            foreach (DoorView view in doors.Values)
            {
                float target = view.State == DoorState.Open ? 1f : 0f;
                view.Swing = Mathf.MoveTowards(view.Swing, target, deltaTime / DoorSwingSeconds);
                float yaw = Mathf.LerpAngle(view.ClosedYaw, view.OpenYaw, Mathf.SmoothStep(0f, 1f, view.Swing));

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
                    float tip = 90f * view.FallDirection * view.Fall * view.Fall;
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
