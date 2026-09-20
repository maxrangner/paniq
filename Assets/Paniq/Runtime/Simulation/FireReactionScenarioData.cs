using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    [Serializable]
    public struct FireReactionAgentDefinition
    {
        [UnityEngine.SerializeField] private SimulationId agentId;
        [UnityEngine.SerializeField] private LogicalPosition initialPosition;
        [UnityEngine.SerializeField] private CardinalDirection initialFacingDirection;
        [UnityEngine.SerializeField] private bool hasAuthoredTraits;
        [UnityEngine.SerializeField] private AgentTraitValues traits;

        public FireReactionAgentDefinition(SimulationId agentId, LogicalPosition initialPosition)
            : this(agentId, initialPosition, CardinalDirection.North)
        {
        }

        /// <summary>A person whose traits are drawn from the seed at the start of the run.</summary>
        public FireReactionAgentDefinition(
            SimulationId agentId,
            LogicalPosition initialPosition,
            CardinalDirection initialFacingDirection)
        {
            this.agentId = agentId;
            this.initialPosition = initialPosition;
            this.initialFacingDirection = initialFacingDirection;
            hasAuthoredTraits = false;
            traits = default;
        }

        /// <summary>A person with an authored personality.</summary>
        public FireReactionAgentDefinition(
            SimulationId agentId,
            LogicalPosition initialPosition,
            CardinalDirection initialFacingDirection,
            AgentTraitValues traits)
        {
            this.agentId = agentId;
            this.initialPosition = initialPosition;
            this.initialFacingDirection = initialFacingDirection;
            hasAuthoredTraits = true;
            this.traits = traits;
        }

        public SimulationId AgentId => agentId;
        public LogicalPosition InitialPosition => initialPosition;
        public CardinalDirection InitialFacingDirection => initialFacingDirection;

        /// <summary>False when the traits are drawn from the seed instead.</summary>
        public bool HasAuthoredTraits => hasAuthoredTraits;

        public AgentTraitValues Traits => traits;
    }

    /// <summary>
    /// A door in one of the room's walls. Its position is the centre of the
    /// gap, measured along the wall (X for north and south walls, Z for east
    /// and west walls). Every door starts locked.
    /// </summary>
    [Serializable]
    public struct FireReactionDoorDefinition
    {
        [UnityEngine.SerializeField] private SimulationId doorId;
        [UnityEngine.SerializeField] private SimulationId roomId;
        [UnityEngine.SerializeField] private WallSide side;
        [UnityEngine.SerializeField] private int centreAlongWallMillimetres;
        [UnityEngine.SerializeField] private int widthMillimetres;
        [UnityEngine.SerializeField] private bool startsLocked;

        public FireReactionDoorDefinition(
            SimulationId doorId,
            SimulationId roomId,
            WallSide side,
            int centreAlongWallMillimetres,
            int widthMillimetres,
            bool startsLocked = true)
        {
            this.doorId = doorId;
            this.roomId = roomId;
            this.side = side;
            this.centreAlongWallMillimetres = centreAlongWallMillimetres;
            this.widthMillimetres = widthMillimetres;
            this.startsLocked = startsLocked;
        }

        public SimulationId DoorId => doorId;

        /// <summary>The room whose wall holds this door; what lies beyond is worked out from the rooms.</summary>
        public SimulationId RoomId => roomId;

        public WallSide Side => side;
        public int CentreAlongWallMillimetres => centreAlongWallMillimetres;
        public int WidthMillimetres => widthMillimetres;

        /// <summary>Locked at the start (the player's doors); otherwise it starts shut but openable.</summary>
        public bool StartsLocked => startsLocked;
    }

    /// <summary>
    /// A loose object on the floor that people can bump and send sliding.
    /// Its footprint is a circle of diameter <see cref="SizeMillimetres"/>.
    /// </summary>
    [Serializable]
    public struct FireReactionPhysicsObjectDefinition
    {
        [UnityEngine.SerializeField] private SimulationId objectId;
        [UnityEngine.SerializeField] private PhysicsObjectKind kind;
        [UnityEngine.SerializeField] private LogicalPosition initialPosition;
        [UnityEngine.SerializeField] private int sizeMillimetres;
        [UnityEngine.SerializeField] private int massGrams;

        public FireReactionPhysicsObjectDefinition(
            SimulationId objectId,
            PhysicsObjectKind kind,
            LogicalPosition initialPosition,
            int sizeMillimetres,
            int massGrams)
        {
            this.objectId = objectId;
            this.kind = kind;
            this.initialPosition = initialPosition;
            this.sizeMillimetres = sizeMillimetres;
            this.massGrams = massGrams;
        }

        public SimulationId ObjectId => objectId;
        public PhysicsObjectKind Kind => kind;
        public LogicalPosition InitialPosition => initialPosition;
        public int SizeMillimetres => sizeMillimetres;
        public int MassGrams => massGrams;
        public int RadiusMillimetres => sizeMillimetres / 2;
    }

    /// <summary>
    /// One rectangular room of the building. Rooms never overlap, but they
    /// may share a wall line; a door in that shared wall joins them. The
    /// first room is where the fire starts. A door with no room beyond it
    /// leads outside, which is how people escape.
    /// </summary>
    [Serializable]
    public struct FireReactionRoomDefinition
    {
        [UnityEngine.SerializeField] private SimulationId roomId;
        [UnityEngine.SerializeField] private LogicalBounds bounds;

        public FireReactionRoomDefinition(SimulationId roomId, LogicalBounds bounds)
        {
            this.roomId = roomId;
            this.bounds = bounds;
        }

        public SimulationId RoomId => roomId;
        public LogicalBounds Bounds => bounds;
    }

    /// <summary>
    /// A table: a fixed rectangle on the floor that people walk around and
    /// loose objects bounce off. <see cref="WidthMillimetres"/> runs along X,
    /// <see cref="DepthMillimetres"/> along Z.
    /// </summary>
    [Serializable]
    public struct FireReactionTableDefinition
    {
        [UnityEngine.SerializeField] private SimulationId tableId;
        [UnityEngine.SerializeField] private LogicalPosition centre;
        [UnityEngine.SerializeField] private int widthMillimetres;
        [UnityEngine.SerializeField] private int depthMillimetres;

        public FireReactionTableDefinition(SimulationId tableId, LogicalPosition centre, int widthMillimetres, int depthMillimetres)
        {
            this.tableId = tableId;
            this.centre = centre;
            this.widthMillimetres = widthMillimetres;
            this.depthMillimetres = depthMillimetres;
        }

        public SimulationId TableId => tableId;
        public LogicalPosition Centre => centre;
        public int WidthMillimetres => widthMillimetres;
        public int DepthMillimetres => depthMillimetres;

        public LogicalBounds Bounds => new LogicalBounds(
            centre.X - widthMillimetres / 2, centre.X + widthMillimetres / 2,
            centre.Z - depthMillimetres / 2, centre.Z + depthMillimetres / 2);
    }

    /// <summary>
    /// Everything a fire-reaction run starts from: its replay identity, every
    /// tunable number (grouped by topic in ScenarioSettings.cs), and the
    /// people, doors and boxes in the room. The scenario asset keeps one of
    /// these; a run works on its own clone and never writes it back.
    /// </summary>
    [Serializable]
    public sealed class FireReactionScenarioData
    {
        public string ScenarioId = "fire-reaction-prototype";
        public string ContentRevision = "27";
        public ulong DefaultSeed = 42UL;
        public int SimulationCompatibilityVersion = 19;

        public WorldSettings World = new WorldSettings();
        public PerceptionSettings Perception = new PerceptionSettings();
        public FireSettings Fire = new FireSettings();
        public SteeringSettings Steering = new SteeringSettings();
        public CalmSettings Calm = new CalmSettings();
        public PanicSettings Panic = new PanicSettings();
        public TemperamentSettings Temperament = new TemperamentSettings();
        public HearingSettings Hearing = new HearingSettings();
        public FallSettings Falls = new FallSettings();
        public ExitSettings Exits = new ExitSettings();
        public ObjectPhysicsSettings ObjectPhysics = new ObjectPhysicsSettings();
        public TraitSettings Traits = new TraitSettings();
        public FlammableSettings Flammables = new FlammableSettings();
        public ItemSettings Items = new ItemSettings();
        public HelpSettings Help = new HelpSettings();

        public FireReactionAgentDefinition[] Agents = DefaultAgents();
        public FireReactionDoorDefinition[] Doors = DefaultDoors();
        public FireReactionPhysicsObjectDefinition[] PhysicsObjects = DefaultPhysicsObjects();
        public FireReactionTableDefinition[] Tables = DefaultTables();
        public FireReactionRoomDefinition[] Rooms = DefaultRooms();

        /// <summary>A deep copy: changing the copy never changes this one.</summary>
        public FireReactionScenarioData Clone()
        {
            var copy = (FireReactionScenarioData)MemberwiseClone();
            copy.World = World?.Clone();
            copy.Perception = Perception?.Clone();
            copy.Fire = Fire?.Clone();
            copy.Steering = Steering?.Clone();
            copy.Calm = Calm?.Clone();
            copy.Panic = Panic?.Clone();
            copy.Temperament = Temperament?.Clone();
            copy.Hearing = Hearing?.Clone();
            copy.Falls = Falls?.Clone();
            copy.Exits = Exits?.Clone();
            copy.ObjectPhysics = ObjectPhysics?.Clone();
            copy.Traits = Traits?.Clone();
            copy.Flammables = Flammables?.Clone();
            copy.Items = Items?.Clone();
            copy.Help = Help?.Clone();
            copy.Agents = (FireReactionAgentDefinition[])Agents?.Clone();
            copy.Doors = (FireReactionDoorDefinition[])Doors?.Clone();
            copy.PhysicsObjects = (FireReactionPhysicsObjectDefinition[])PhysicsObjects?.Clone();
            copy.Tables = (FireReactionTableDefinition[])Tables?.Clone();
            copy.Rooms = (FireReactionRoomDefinition[])Rooms?.Clone();
            return copy;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(ScenarioId) || string.IsNullOrEmpty(ContentRevision))
            {
                throw new InvalidOperationException("A fire-reaction scenario needs an ID and content revision.");
            }

            if (SimulationCompatibilityVersion <= 0 || DefaultSeed == 0UL)
            {
                throw new InvalidOperationException("A fire-reaction scenario needs a compatibility version and explicit seed.");
            }

            if (World == null || Perception == null || Fire == null || Steering == null || Calm == null ||
                Panic == null || Temperament == null || Hearing == null || Falls == null || Exits == null ||
                ObjectPhysics == null || Traits == null || Flammables == null || Items == null || Help == null)
            {
                throw new InvalidOperationException("A fire-reaction scenario is missing a settings group.");
            }

            World.Validate();
            Perception.Validate();
            Fire.Validate();
            Steering.Validate();
            Calm.Validate();
            Panic.Validate();
            Temperament.Validate();
            Hearing.Validate();
            Falls.Validate();
            Exits.Validate(World);
            ObjectPhysics.Validate();
            Traits.Validate();
            Flammables.Validate();
            Items.Validate();
            Help.Validate();
            Settings.Require(Calm.SpeedMaximum + Traits.CalmSpeedJitter <= World.MaximumStepDistanceMillimetres &&
                             Panic.SpeedMaximum + Traits.PanicSpeedJitter <= World.MaximumStepDistanceMillimetres,
                "speeds within the maximum step");

            var roomIds = new HashSet<SimulationId>();
            ValidateRooms(roomIds);

            // The fire starts in the first room.
            LogicalBounds room = Rooms[0].Bounds;
            int spawnMargin = Fire.CellSizeMillimetres / 2;
            if (!room.ContainsCircle(new LogicalPosition(Fire.SpawnBounds.MinX, Fire.SpawnBounds.MinZ), spawnMargin) ||
                !room.ContainsCircle(new LogicalPosition(Fire.SpawnBounds.MaxX, Fire.SpawnBounds.MaxZ), spawnMargin))
            {
                throw new InvalidOperationException("The fire spawn rectangle must remain inside the first room.");
            }

            if (Agents == null || Agents.Length == 0)
            {
                throw new InvalidOperationException("A fire-reaction scenario needs at least one agent.");
            }

            var ids = new HashSet<SimulationId> { new SimulationId(FireReactionSimulation.FireHazardIdValue) };
            int radius = World.OccupancyRadiusMillimetres;
            long touchingDistance = (long)radius * 2L;
            for (int i = 0; i < Agents.Length; i++)
            {
                FireReactionAgentDefinition agent = Agents[i];
                if (agent.AgentId.Value == 0UL || !ids.Add(agent.AgentId))
                {
                    throw new InvalidOperationException("Agent IDs must be unique and non-zero.");
                }

                if (agent.HasAuthoredTraits && !agent.Traits.IsValid)
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} has a trait outside 0–10.");
                }

                if (RoomHolding(agent.InitialPosition, radius) < 0)
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} starts outside every room.");
                }

                for (int previous = 0; previous < i; previous++)
                {
                    long distanceSquared = LogicalPosition.DistanceSquared(agent.InitialPosition, Agents[previous].InitialPosition);
                    if (distanceSquared < touchingDistance * touchingDistance)
                    {
                        throw new InvalidOperationException("Initial agents cannot overlap.");
                    }
                }
            }

            foreach (FireReactionRoomDefinition definition in Rooms)
            {
                ids.Add(definition.RoomId);
            }

            ValidateDoors(ids);
            ValidateTables(ids);
            ValidatePhysicsObjects(ids);
        }

        /// <summary>The room whose walls hold a body of this radius, or -1.</summary>
        private int RoomHolding(LogicalPosition position, int radius)
        {
            for (int r = 0; r < Rooms.Length; r++)
            {
                if (Rooms[r].Bounds.ContainsCircle(position, radius))
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>
        /// Rooms: at least one, unique IDs, big enough for people to pass
        /// each other, and never overlapping (sharing a wall line is how they
        /// are joined, so touching is fine).
        /// </summary>
        private void ValidateRooms(HashSet<SimulationId> roomIds)
        {
            if (Rooms == null || Rooms.Length == 0)
            {
                throw new InvalidOperationException("A fire-reaction scenario needs at least one room.");
            }

            int radius = World.OccupancyRadiusMillimetres;
            for (int r = 0; r < Rooms.Length; r++)
            {
                FireReactionRoomDefinition definition = Rooms[r];
                if (definition.RoomId.Value == 0UL || !roomIds.Add(definition.RoomId))
                {
                    throw new InvalidOperationException("Room IDs must be unique and non-zero.");
                }

                LogicalBounds b = definition.Bounds;
                if (b.MaxX - b.MinX < radius * 4 || b.MaxZ - b.MinZ < radius * 4)
                {
                    throw new InvalidOperationException($"Room {definition.RoomId} is too small to walk in.");
                }

                if (b.MinX < -100000 || b.MaxX > 100000 || b.MinZ < -100000 || b.MaxZ > 100000)
                {
                    throw new InvalidOperationException($"Room {definition.RoomId} leaves the 200 m coordinate span.");
                }

                for (int previous = 0; previous < r; previous++)
                {
                    LogicalBounds other = Rooms[previous].Bounds;
                    if (b.MinX < other.MaxX && b.MaxX > other.MinX && b.MinZ < other.MaxZ && b.MaxZ > other.MinZ)
                    {
                        throw new InvalidOperationException($"Rooms {definition.RoomId} and {Rooms[previous].RoomId} overlap.");
                    }
                }
            }
        }

        private void ValidateTables(HashSet<SimulationId> ids)
        {
            Tables ??= Array.Empty<FireReactionTableDefinition>();
            int radius = World.OccupancyRadiusMillimetres;
            for (int i = 0; i < Tables.Length; i++)
            {
                FireReactionTableDefinition table = Tables[i];
                if (table.TableId.Value == 0UL || !ids.Add(table.TableId))
                {
                    throw new InvalidOperationException("Table IDs must be unique and non-zero.");
                }

                LogicalBounds bounds = table.Bounds;
                if (table.WidthMillimetres < 200 || table.DepthMillimetres < 200 ||
                    RoomHolding(new LogicalPosition(bounds.MinX, bounds.MinZ), 0) < 0 ||
                    RoomHolding(new LogicalPosition(bounds.MaxX, bounds.MaxZ), 0) < 0)
                {
                    throw new InvalidOperationException($"Table {table.TableId} is too small or outside every room.");
                }

                for (int a = 0; a < Agents.Length; a++)
                {
                    if (bounds.DistanceSquaredTo(Agents[a].InitialPosition) < (long)radius * radius)
                    {
                        throw new InvalidOperationException($"Agent {Agents[a].AgentId} starts inside table {table.TableId}.");
                    }
                }
            }
        }

        private void ValidateDoors(HashSet<SimulationId> ids)
        {
            Doors ??= Array.Empty<FireReactionDoorDefinition>();
            int radius = World.OccupancyRadiusMillimetres;
            for (int i = 0; i < Doors.Length; i++)
            {
                FireReactionDoorDefinition door = Doors[i];
                if (door.DoorId.Value == 0UL || !ids.Add(door.DoorId))
                {
                    throw new InvalidOperationException("Door IDs must be unique and non-zero.");
                }

                int roomIndex = Array.FindIndex(Rooms, r => r.RoomId == door.RoomId);
                if (roomIndex < 0)
                {
                    throw new InvalidOperationException($"Door {door.DoorId} names an unknown room.");
                }

                // Wide enough for one person, with a solid bit of wall either side.
                LogicalBounds room = Rooms[roomIndex].Bounds;
                bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
                int wallMin = alongX ? room.MinX : room.MinZ;
                int wallMax = alongX ? room.MaxX : room.MaxZ;
                int half = door.WidthMillimetres / 2;
                if (door.WidthMillimetres < radius * 2 + 100 ||
                    door.CentreAlongWallMillimetres - half < wallMin + radius ||
                    door.CentreAlongWallMillimetres + half > wallMax - radius)
                {
                    throw new InvalidOperationException($"Door {door.DoorId} does not fit in its wall.");
                }

                ValidateDoorNeighbour(door, roomIndex, half);

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionDoorDefinition other = Doors[previous];
                    if (other.Side == door.Side && other.RoomId == door.RoomId &&
                        Math.Abs((long)other.CentreAlongWallMillimetres - door.CentreAlongWallMillimetres) <
                        (other.WidthMillimetres + door.WidthMillimetres) / 2 + radius * 2)
                    {
                        throw new InvalidOperationException("Doors in the same wall cannot overlap.");
                    }
                }
            }
        }

        /// <summary>
        /// What lies beyond a door: either nothing (it leads outside) or one
        /// room flush against the far side of that wall, whose wall covers
        /// the whole gap. A room that only half covers the gap would leave a
        /// doorway opening into a wall, so it is rejected.
        /// </summary>
        private void ValidateDoorNeighbour(FireReactionDoorDefinition door, int roomIndex, int half)
        {
            LogicalBounds room = Rooms[roomIndex].Bounds;
            bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
            int gapMin = door.CentreAlongWallMillimetres - half;
            int gapMax = door.CentreAlongWallMillimetres + half;
            for (int r = 0; r < Rooms.Length; r++)
            {
                if (r == roomIndex)
                {
                    continue;
                }

                LogicalBounds other = Rooms[r].Bounds;
                bool flush;
                switch (door.Side)
                {
                    case WallSide.North:
                        flush = other.MinZ == room.MaxZ;
                        break;
                    case WallSide.South:
                        flush = other.MaxZ == room.MinZ;
                        break;
                    case WallSide.East:
                        flush = other.MinX == room.MaxX;
                        break;
                    default:
                        flush = other.MaxX == room.MinX;
                        break;
                }

                int otherMin = alongX ? other.MinX : other.MinZ;
                int otherMax = alongX ? other.MaxX : other.MaxZ;
                if (!flush || otherMax <= gapMin || otherMin >= gapMax)
                {
                    continue;
                }

                if (otherMin > gapMin || otherMax < gapMax)
                {
                    throw new InvalidOperationException(
                        $"Door {door.DoorId} opens partly into room {Rooms[r].RoomId} and partly into a wall.");
                }

                return;
            }
        }

        private void ValidatePhysicsObjects(HashSet<SimulationId> ids)
        {
            PhysicsObjects ??= Array.Empty<FireReactionPhysicsObjectDefinition>();
            int radius = World.OccupancyRadiusMillimetres;
            for (int i = 0; i < PhysicsObjects.Length; i++)
            {
                FireReactionPhysicsObjectDefinition body = PhysicsObjects[i];
                if (body.ObjectId.Value == 0UL || !ids.Add(body.ObjectId))
                {
                    throw new InvalidOperationException("Physical object IDs must be unique and non-zero.");
                }

                if (body.SizeMillimetres < 100 || body.SizeMillimetres > 1000 ||
                    body.MassGrams <= 0 || body.MassGrams > 200000)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} has an invalid size or mass.");
                }

                if (RoomHolding(body.InitialPosition, body.RadiusMillimetres) < 0)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} starts outside every room.");
                }

                for (int t = 0; t < Tables.Length; t++)
                {
                    if (Tables[t].Bounds.DistanceSquaredTo(body.InitialPosition) < (long)body.RadiusMillimetres * body.RadiusMillimetres)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts inside table {Tables[t].TableId}.");
                    }
                }

                long agentReach = radius + (long)body.RadiusMillimetres;
                for (int a = 0; a < Agents.Length; a++)
                {
                    if (LogicalPosition.DistanceSquared(body.InitialPosition, Agents[a].InitialPosition) < agentReach * agentReach)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts on top of a person.");
                    }
                }

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionPhysicsObjectDefinition other = PhysicsObjects[previous];
                    long reach = (long)other.RadiusMillimetres + body.RadiusMillimetres;
                    if (LogicalPosition.DistanceSquared(body.InitialPosition, other.InitialPosition) < reach * reach)
                    {
                        throw new InvalidOperationException("Initial objects cannot overlap.");
                    }
                }
            }
        }

        /// <summary>
        /// Twenty people, ten in the office and ten in the meeting room,
        /// facing different ways, each with an authored personality so every
        /// trait shows up in play: Str, Spd, Brv, Cmp, Evl, Nrv.
        /// </summary>
        public static FireReactionAgentDefinition[] DefaultAgents()
        {
            return new[]
            {
                Agent(1001UL, -5000, -5000, CardinalDirection.North, 5, 5, 5, 5, 2, 5), // ordinary
                Agent(1002UL, 0, -5000, CardinalDirection.East, 9, 6, 6, 3, 6, 3), // the brute
                Agent(1003UL, 5000, -5000, CardinalDirection.West, 8, 6, 8, 8, 1, 3), // the hero
                Agent(1004UL, -5000, 0, CardinalDirection.East, 4, 4, 7, 9, 0, 4), // the saint
                Agent(1005UL, 900, 0, CardinalDirection.South, 6, 6, 5, 1, 8, 4), // the villain
                Agent(1006UL, 5000, 0, CardinalDirection.North, 3, 5, 1, 5, 2, 10), // the nervous wreck
                Agent(1007UL, -5000, 5000, CardinalDirection.South, 5, 10, 5, 5, 3, 6), // the sprinter
                Agent(1008UL, 0, 5000, CardinalDirection.West, 7, 5, 4, 2, 9, 5), // the bully
                Agent(1009UL, 5000, 5000, CardinalDirection.South, 3, 4, 2, 4, 3, 8), // the coward
                Agent(1010UL, 0, -1800, CardinalDirection.North, 5, 5, 5, 6, 3, 5), // ordinary

                // The meeting room: ten more people who cannot see the fire
                // when it starts and only learn about it through the shouting.
                Agent(1011UL, 10500, -4500, CardinalDirection.North, 5, 5, 6, 5, 3, 4), // ordinary
                Agent(1012UL, 13500, -4500, CardinalDirection.West, 9, 4, 7, 6, 2, 3), // the strong one
                Agent(1013UL, 16500, -4500, CardinalDirection.North, 4, 7, 3, 7, 1, 7), // the worrier
                Agent(1014UL, 19500, -4500, CardinalDirection.West, 6, 5, 5, 5, 5, 5), // ordinary
                Agent(1015UL, 10500, 0, CardinalDirection.East, 3, 6, 2, 8, 0, 9), // the timid carer
                Agent(1016UL, 13500, 1200, CardinalDirection.South, 7, 8, 8, 4, 7, 2), // the chancer
                Agent(1017UL, 17200, -1800, CardinalDirection.West, 5, 5, 4, 5, 4, 6), // ordinary
                Agent(1018UL, 19500, 1200, CardinalDirection.North, 8, 6, 6, 2, 8, 4), // the other bully
                Agent(1019UL, 12000, 4500, CardinalDirection.South, 4, 9, 5, 6, 2, 6), // the runner
                Agent(1020UL, 18000, 4500, CardinalDirection.South, 6, 5, 7, 9, 1, 3) // the other hero
            };
        }

        /// <summary>
        /// The doors. The five in outside walls are the player's, set
        /// off-centre so different corners have different nearest exits, and
        /// they start locked. The three inside doors (the storage closet, and
        /// the corridor at each end) start shut but not locked, so people can
        /// open them themselves.
        /// </summary>
        public static FireReactionDoorDefinition[] DefaultDoors()
        {
            return new[]
            {
                new FireReactionDoorDefinition(new SimulationId(2001UL), Office, WallSide.North, -2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2002UL), Office, WallSide.East, 2500, 1000, false),
                new FireReactionDoorDefinition(new SimulationId(2003UL), Office, WallSide.South, 2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2004UL), Office, WallSide.West, -2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2005UL), Office, WallSide.East, 0, 1000, false),
                new FireReactionDoorDefinition(new SimulationId(2006UL), Corridor, WallSide.East, 0, 1000, false),
                new FireReactionDoorDefinition(new SimulationId(2007UL), MeetingRoom, WallSide.North, 15000, 1000),
                new FireReactionDoorDefinition(new SimulationId(2008UL), MeetingRoom, WallSide.East, 2500, 1000)
            };
        }

        /// <summary>
        /// Eight cardboard boxes, 0.3–0.6 m wide and 3–20 kg, set between
        /// where people stand; eight 5 kg wooden chairs pulled up to the
        /// tables; and the rest of the office: waste bins, potted plants
        /// (heavy, and they never catch), bags and laptops that skitter, and
        /// eight office chairs on castors around the meeting room.
        /// </summary>
        public static FireReactionPhysicsObjectDefinition[] DefaultPhysicsObjects()
        {
            return new[]
            {
                Box(3001UL, -2000, -3750, 400, 6000),
                Box(3002UL, 2000, -3750, 300, 3000),
                Box(3003UL, 4000, -1250, 500, 12000),
                Box(3004UL, -4000, 1250, 600, 20000),
                Box(3005UL, -2000, 3750, 350, 4000),
                Box(3006UL, 2000, 1250, 450, 9000),
                Box(3007UL, 4000, 3750, 300, 3000),
                Box(3008UL, -4000, -1250, 400, 6000),
                Chair(3101UL, -2800, -2125),
                Chair(3102UL, -2200, -875),
                Chair(3103UL, -3375, -1500),
                Chair(3104UL, 2200, 2175),
                Chair(3105UL, 2800, 3425),
                Chair(3106UL, -1800, 1375),
                Chair(3107UL, -1200, 2625),
                Chair(3108UL, -625, 2000),

                // The office's clutter: bins, plants, bags and laptops, plus
                // the meeting room's chairs on castors.
                Bin(3201UL, -5400, 200),
                Bin(3202UL, 5400, -3200),
                Bin(3203UL, 10200, -1500),
                Bin(3204UL, 20200, 3200),
                Plant(3211UL, -5400, -3400),
                Plant(3212UL, 5400, 5400),
                Plant(3213UL, 9800, 5200),
                Plant(3214UL, 20200, -5200),
                Bag(3221UL, -3400, 3400),
                Bag(3222UL, 1200, -2600),
                Bag(3223UL, 14500, -2400),
                Bag(3224UL, 17500, 3400),
                Laptop(3231UL, -1100, -700),
                Laptop(3232UL, 3400, 1800),
                Laptop(3233UL, 15800, 2400),
                Laptop(3234UL, 12200, -3200),
                OfficeChair(3241UL, 11500, 2400),
                OfficeChair(3242UL, 13000, 3000),
                OfficeChair(3243UL, 14500, 2400),
                OfficeChair(3244UL, 16000, 3000),
                OfficeChair(3245UL, 17500, 2400),
                OfficeChair(3246UL, 19000, -2400),
                OfficeChair(3247UL, 11500, -1200),
                OfficeChair(3248UL, 16800, -3600)
            };
        }

        /// <summary>The open-plan office where the fire starts.</summary>
        public static readonly SimulationId Office = new SimulationId(5001UL);

        /// <summary>The storage closet off the office's east wall.</summary>
        public static readonly SimulationId Closet = new SimulationId(5002UL);

        /// <summary>The short corridor from the office to the meeting room.</summary>
        public static readonly SimulationId Corridor = new SimulationId(5003UL);

        /// <summary>The meeting room at the far end of the corridor.</summary>
        public static readonly SimulationId MeetingRoom = new SimulationId(5004UL);

        /// <summary>
        /// The building: a 12 × 12 m open-plan office where the fire starts, a
        /// 2 × 2 m storage closet against its east wall, a 3 m corridor east
        /// out of the office, and a second 12 × 12 m room, the meeting room,
        /// at the end of it. None of them is a refuge; they are simply rooms.
        /// </summary>
        public static FireReactionRoomDefinition[] DefaultRooms()
        {
            return new[]
            {
                new FireReactionRoomDefinition(Office, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new FireReactionRoomDefinition(Closet, new LogicalBounds(6000, 8000, 1500, 3500)),
                new FireReactionRoomDefinition(Corridor, new LogicalBounds(6000, 9000, -1000, 1000)),
                new FireReactionRoomDefinition(MeetingRoom, new LogicalBounds(9000, 21000, -6000, 6000))
            };
        }

        /// <summary>Three 1.2 × 0.7 m tables around the office, and the meeting room's long table in two halves.</summary>
        public static FireReactionTableDefinition[] DefaultTables()
        {
            return new[]
            {
                new FireReactionTableDefinition(new SimulationId(4001UL), new LogicalPosition(-2500, -1500), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4002UL), new LogicalPosition(2500, 2800), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4003UL), new LogicalPosition(-1500, 2000), 1200, 700),

                // The meeting room's long table, in two halves.
                new FireReactionTableDefinition(new SimulationId(4004UL), new LogicalPosition(13500, 0), 2400, 900),
                new FireReactionTableDefinition(new SimulationId(4005UL), new LogicalPosition(16500, 0), 2400, 900)
            };
        }

        private static FireReactionPhysicsObjectDefinition Chair(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Chair, new LogicalPosition(x, z), 450, 5000);
        }

        /// <summary>An office chair: the same size as a wooden one, but on castors (see the kind's friction).</summary>
        private static FireReactionPhysicsObjectDefinition OfficeChair(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.OfficeChair, new LogicalPosition(x, z), 500, 9000);
        }

        private static FireReactionPhysicsObjectDefinition Bin(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WasteBin, new LogicalPosition(x, z), 300, 2000);
        }

        private static FireReactionPhysicsObjectDefinition Plant(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.PottedPlant, new LogicalPosition(x, z), 450, 25000);
        }

        private static FireReactionPhysicsObjectDefinition Bag(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Bag, new LogicalPosition(x, z), 350, 4000);
        }

        private static FireReactionPhysicsObjectDefinition Laptop(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Laptop, new LogicalPosition(x, z), 300, 1500);
        }

        private static FireReactionAgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness));
        }

        private static FireReactionPhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams);
        }
    }
}
