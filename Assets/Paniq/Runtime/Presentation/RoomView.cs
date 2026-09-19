using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The floor and walls from the scenario's room, each wall split around
    /// its doors, a door leaf in every gap and a strip of ground outside.
    /// Door leaves swing open, judder when shoved, and keep a collider only
    /// so a click can find which door was hit.
    /// </summary>
    internal sealed class RoomView
    {
        private const float WallHeight = 1.5f;
        private const float WallThickness = 0.4f;
        private const float DoorHeight = 1.3f;
        private const float DoorSwingSeconds = 0.3f;

        private static readonly Color UnlockedDoorColor = new Color(0.18f, 0.8f, 0.3f);

        private sealed class DoorView
        {
            public SimulationId DoorId;
            public Transform Hinge;
            public Renderer Leaf;
            public float ClosedYaw;
            public float OpenYaw;
            public float Swing;
            public float ShakeStart = -10f;
            public DoorState State;
        }

        private readonly PresentationMaterials materials;
        private readonly Transform parent;
        private readonly FireReactionScenarioData scenario;
        private readonly Dictionary<SimulationId, DoorView> doors = new Dictionary<SimulationId, DoorView>();
        private readonly Dictionary<Collider, SimulationId> doorByCollider = new Dictionary<Collider, SimulationId>();

        public RoomView(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            this.scenario = scenario;
            this.materials = materials;
            this.parent = parent;
            Build();
        }

        private void Build()
        {
            LogicalBounds room = scenario.World.RoomBounds;
            float minX = Metres(room.MinX);
            float maxX = Metres(room.MaxX);
            float minZ = Metres(room.MinZ);
            float maxZ = Metres(room.MaxZ);
            CreatePrimitive("Room Floor", PrimitiveType.Cube, parent, new Vector3((minX + maxX) * 0.5f, -0.05f, (minZ + maxZ) * 0.5f),
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
                    if (door.Side == side)
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
                    CreateWallPiece(side, piece++, alongX, wallLine, cursor, gapStart);
                    cursor = Metres(door.CentreAlongWallMillimetres + door.WidthMillimetres / 2);
                    CreateDoor(door, alongX, wallLine);
                }

                CreateWallPiece(side, piece, alongX, wallLine, cursor, end);
            }
        }

        private void CreateWallPiece(WallSide side, int piece, bool alongX, float wallLine, float from, float to)
        {
            if (to - from <= 0.001f)
            {
                return;
            }

            float middle = (from + to) * 0.5f;
            Vector3 position = alongX ? new Vector3(middle, WallHeight * 0.5f, wallLine) : new Vector3(wallLine, WallHeight * 0.5f, middle);
            Vector3 scale = alongX ? new Vector3(to - from, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, to - from);
            CreatePrimitive($"Room Wall {side} {piece}", PrimitiveType.Cube, parent, position, scale, materials.Wall);
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

            // A strip of ground outside, as far as the doorway reaches.
            float depth = Metres(scenario.Exits.DoorwayDepthMillimetres);
            CreatePrimitive($"Door {door.DoorId.Value} outside ground", PrimitiveType.Cube, parent,
                gapCentre + outward * (depth * 0.5f + WallThickness * 0.25f) + Vector3.down * 0.05f,
                alongX ? new Vector3(width + 0.4f, 0.1f, depth) : new Vector3(depth, 0.1f, width + 0.4f),
                materials.Outside);

            var view = new DoorView
            {
                DoorId = door.DoorId,
                Hinge = hinge,
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
                }
            }

            foreach (DoorView view in doors.Values)
            {
                float target = view.State == DoorState.Open ? 1f : 0f;
                view.Swing = Mathf.MoveTowards(view.Swing, target, deltaTime / DoorSwingSeconds);
                float yaw = Mathf.LerpAngle(view.ClosedYaw, view.OpenYaw, Mathf.SmoothStep(0f, 1f, view.Swing));

                float shakeAge = time - view.ShakeStart;
                if (shakeAge < 0.3f && view.State != DoorState.Open)
                {
                    yaw += Mathf.Sin(shakeAge * 70f) * 4f * (1f - shakeAge / 0.3f);
                }

                view.Hinge.rotation = Quaternion.Euler(0f, yaw, 0f);
                Color color = view.State == DoorState.Locked ? PresentationMaterials.LockedDoorColor : UnlockedDoorColor;
                if (hoveredDoor.HasValue && hoveredDoor.Value == view.DoorId)
                {
                    color = Color.Lerp(color, Color.white, 0.3f);
                }

                materials.SetColors(view.Leaf, color, color * 0.35f);
            }
        }
    }
}
