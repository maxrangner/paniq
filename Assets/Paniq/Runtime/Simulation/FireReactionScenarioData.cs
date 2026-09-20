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

        /// <summary>The thing they walk in holding, or the default ID for empty-handed.</summary>
        [UnityEngine.SerializeField] private SimulationId carriedObjectId;

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
            carriedObjectId = default;
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
            carriedObjectId = default;
        }

        /// <summary>A person with an authored personality who walks in carrying something.</summary>
        public FireReactionAgentDefinition(
            SimulationId agentId,
            LogicalPosition initialPosition,
            CardinalDirection initialFacingDirection,
            AgentTraitValues traits,
            SimulationId carriedObjectId)
        {
            this.agentId = agentId;
            this.initialPosition = initialPosition;
            this.initialFacingDirection = initialFacingDirection;
            hasAuthoredTraits = true;
            this.traits = traits;
            this.carriedObjectId = carriedObjectId;
        }

        public SimulationId AgentId => agentId;
        public LogicalPosition InitialPosition => initialPosition;
        public CardinalDirection InitialFacingDirection => initialFacingDirection;

        /// <summary>False when the traits are drawn from the seed instead.</summary>
        public bool HasAuthoredTraits => hasAuthoredTraits;

        public AgentTraitValues Traits => traits;

        /// <summary>What they are already holding when the run starts, if anything.</summary>
        public SimulationId CarriedObjectId => carriedObjectId;

        public bool StartsCarryingSomething => carriedObjectId.Value != 0UL;
    }

    /// <summary>
    /// A fire alarm on a wall. One per room. Anybody who has taken in that
    /// there is a fire can walk over and hit it, and then every alarm in the
    /// building rings at once.
    /// </summary>
    [Serializable]
    public struct FireReactionAlarmDefinition
    {
        [UnityEngine.SerializeField] private SimulationId alarmId;
        [UnityEngine.SerializeField] private LogicalPosition position;

        public FireReactionAlarmDefinition(SimulationId alarmId, LogicalPosition position)
        {
            this.alarmId = alarmId;
            this.position = position;
        }

        public SimulationId AlarmId => alarmId;
        public LogicalPosition Position => position;
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

        /// <summary>A spare kept out of the world until the player puts it down.</summary>
        [UnityEngine.SerializeField] private bool startsDormant;

        public FireReactionPhysicsObjectDefinition(
            SimulationId objectId,
            PhysicsObjectKind kind,
            LogicalPosition initialPosition,
            int sizeMillimetres,
            int massGrams,
            bool startsDormant = false)
        {
            this.objectId = objectId;
            this.kind = kind;
            this.initialPosition = initialPosition;
            this.sizeMillimetres = sizeMillimetres;
            this.massGrams = massGrams;
            this.startsDormant = startsDormant;
        }

        public SimulationId ObjectId => objectId;
        public PhysicsObjectKind Kind => kind;
        public LogicalPosition InitialPosition => initialPosition;
        public int SizeMillimetres => sizeMillimetres;
        public int MassGrams => massGrams;
        public int RadiusMillimetres => sizeMillimetres / 2;

        /// <summary>
        /// True for one of the spares the run keeps aside for the player's
        /// cards. It is nowhere until a card puts it somewhere, so where it is
        /// authored does not matter.
        /// </summary>
        public bool StartsDormant => startsDormant;
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
        public string ContentRevision = "30";
        public ulong DefaultSeed = 42UL;
        public int SimulationCompatibilityVersion = 22;

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
        public ExtinguisherSettings Extinguishers = new ExtinguisherSettings();
        public LeadershipSettings Leadership = new LeadershipSettings();
        public ItemSettings Items = new ItemSettings();
        public HelpSettings Help = new HelpSettings();
        public InfluenceSettings Influence = new InfluenceSettings();
        public AlarmSettings Alarm = new AlarmSettings();
        public BlockadeSettings Blockades = new BlockadeSettings();
        public BlastSettings Blast = new BlastSettings();

        public FireReactionAgentDefinition[] Agents = DefaultAgents();
        public FireReactionDoorDefinition[] Doors = DefaultDoors();
        public FireReactionPhysicsObjectDefinition[] PhysicsObjects = DefaultPhysicsObjects();
        public FireReactionTableDefinition[] Tables = DefaultTables();
        public FireReactionRoomDefinition[] Rooms = DefaultRooms();
        public FireReactionAlarmDefinition[] Alarms = DefaultAlarms();

        /// <summary>
        /// The sticks of TNT the player has: one spare opening each, kept out of
        /// the world until a charge is spent on a wall.
        /// </summary>
        public SimulationId[] BlastHoles = DefaultBlastHoles();

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
            copy.Extinguishers = Extinguishers?.Clone();
            copy.Leadership = Leadership?.Clone();
            copy.Items = Items?.Clone();
            copy.Help = Help?.Clone();
            copy.Influence = Influence?.Clone();
            copy.Alarm = Alarm?.Clone();
            copy.Blockades = Blockades?.Clone();
            copy.Blast = Blast?.Clone();
            copy.Agents = (FireReactionAgentDefinition[])Agents?.Clone();
            copy.Doors = (FireReactionDoorDefinition[])Doors?.Clone();
            copy.PhysicsObjects = (FireReactionPhysicsObjectDefinition[])PhysicsObjects?.Clone();
            copy.Tables = (FireReactionTableDefinition[])Tables?.Clone();
            copy.Rooms = (FireReactionRoomDefinition[])Rooms?.Clone();
            copy.Alarms = (FireReactionAlarmDefinition[])Alarms?.Clone();
            copy.BlastHoles = (SimulationId[])BlastHoles?.Clone();
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
                ObjectPhysics == null || Traits == null || Flammables == null || Items == null || Help == null ||
                Influence == null || Alarm == null || Blockades == null || Blast == null ||
                Extinguishers == null || Leadership == null)
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
            Extinguishers.Validate();
            Leadership.Validate();
            Items.Validate();
            Help.Validate();
            Influence.Validate();
            Alarm.Validate();
            Blockades.Validate();
            Blast.Validate();
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
            ValidateStartingPossessions();
            ValidateAlarms(ids);
            ValidateBlastHoles(ids);
        }

        /// <summary>
        /// Everything somebody walks in holding must be a thing this scenario
        /// has, light enough for them to hold, and held by only one person.
        /// </summary>
        private void ValidateStartingPossessions()
        {
            var taken = new HashSet<SimulationId>();
            for (int i = 0; i < Agents.Length; i++)
            {
                FireReactionAgentDefinition agent = Agents[i];
                if (!agent.StartsCarryingSomething)
                {
                    continue;
                }

                int found = -1;
                for (int o = 0; o < PhysicsObjects.Length; o++)
                {
                    if (PhysicsObjects[o].ObjectId == agent.CarriedObjectId)
                    {
                        found = o;
                        break;
                    }
                }

                if (found < 0)
                {
                    throw new InvalidOperationException(
                        $"Agent {agent.AgentId} starts holding {agent.CarriedObjectId}, which this scenario does not have.");
                }

                if (!taken.Add(agent.CarriedObjectId))
                {
                    throw new InvalidOperationException($"Two people cannot both start holding {agent.CarriedObjectId}.");
                }

                if (PhysicsObjects[found].Kind == PhysicsObjectKind.Extinguisher)
                {
                    throw new InvalidOperationException(
                        $"Agent {agent.AgentId} cannot start holding an extinguisher; the brave fetch those themselves.");
                }

                long limit = Items.CarryBaseGrams + (long)Items.CarryGramsPerStrength *
                    (agent.HasAuthoredTraits ? agent.Traits.Strength : AgentTraitValues.Minimum);
                if (PhysicsObjects[found].MassGrams > limit)
                {
                    throw new InvalidOperationException(
                        $"Agent {agent.AgentId} is not strong enough to carry {agent.CarriedObjectId}.");
                }
            }
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

                // Something that starts in somebody's hand is not on the floor:
                // it is moved in front of its owner at tick zero, so where it is
                // authored does not matter and it cannot be in anything's way.
                bool inSomebodysHand = IsCarriedAtTheStart(body.ObjectId) || body.StartsDormant;

                if (body.SizeMillimetres < 100 || body.SizeMillimetres > 1000 ||
                    body.MassGrams <= 0 || body.MassGrams > 200000)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} has an invalid size or mass.");
                }

                if (!body.StartsDormant && RoomHolding(body.InitialPosition, body.RadiusMillimetres) < 0)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} starts outside every room.");
                }

                for (int t = 0; t < Tables.Length && !inSomebodysHand; t++)
                {
                    if (Tables[t].Bounds.DistanceSquaredTo(body.InitialPosition) < (long)body.RadiusMillimetres * body.RadiusMillimetres)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts inside table {Tables[t].TableId}.");
                    }
                }

                long agentReach = radius + (long)body.RadiusMillimetres;
                for (int a = 0; a < Agents.Length && !inSomebodysHand; a++)
                {
                    if (LogicalPosition.DistanceSquared(body.InitialPosition, Agents[a].InitialPosition) < agentReach * agentReach)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts on top of a person.");
                    }
                }

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionPhysicsObjectDefinition other = PhysicsObjects[previous];
                    if (inSomebodysHand || IsCarriedAtTheStart(other.ObjectId))
                    {
                        continue;
                    }

                    long reach = (long)other.RadiusMillimetres + body.RadiusMillimetres;
                    if (LogicalPosition.DistanceSquared(body.InitialPosition, other.InitialPosition) < reach * reach)
                    {
                        throw new InvalidOperationException("Initial objects cannot overlap.");
                    }
                }
            }
        }

        /// <summary>Four sticks of TNT, each with its own stable ID.</summary>
        public static SimulationId[] DefaultBlastHoles()
        {
            return new[]
            {
                new SimulationId(2901UL), new SimulationId(2902UL),
                new SimulationId(2903UL), new SimulationId(2904UL)
            };
        }

        /// <summary>Every stick of TNT needs its own ID, like anything else in the run.</summary>
        private void ValidateBlastHoles(HashSet<SimulationId> ids)
        {
            BlastHoles ??= Array.Empty<SimulationId>();
            for (int i = 0; i < BlastHoles.Length; i++)
            {
                if (BlastHoles[i].Value == 0UL || !ids.Add(BlastHoles[i]))
                {
                    throw new InvalidOperationException("Blast-hole IDs must be unique and non-zero.");
                }
            }
        }

        /// <summary>One alarm per room, each on that room's wall and each with its own ID.</summary>
        private void ValidateAlarms(HashSet<SimulationId> ids)
        {
            Alarms ??= Array.Empty<FireReactionAlarmDefinition>();
            for (int i = 0; i < Alarms.Length; i++)
            {
                FireReactionAlarmDefinition alarm = Alarms[i];
                if (alarm.AlarmId.Value == 0UL || !ids.Add(alarm.AlarmId))
                {
                    throw new InvalidOperationException("Fire-alarm IDs must be unique and non-zero.");
                }

                if (RoomHolding(alarm.Position, 0) < 0)
                {
                    throw new InvalidOperationException($"Fire alarm {alarm.AlarmId} is not in any room.");
                }
            }
        }

        /// <summary>Whether somebody walks in holding this thing.</summary>
        private bool IsCarriedAtTheStart(SimulationId objectId)
        {
            for (int a = 0; a < Agents.Length; a++)
            {
                if (Agents[a].CarriedObjectId == objectId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Twenty people, ten in the office and ten in the meeting room,
        /// facing different ways, each with an authored personality so every
        /// trait shows up in play: Str, Spd, Brv, Cmp, Evl, Nrv, Ldr.
        /// </summary>
        public static FireReactionAgentDefinition[] DefaultAgents()
        {
            return new[]
            {
                Agent(1001UL, -5000, -5000, CardinalDirection.North, 5, 5, 5, 5, 2, 5, 4), // ordinary
                Agent(1002UL, 0, -5000, CardinalDirection.East, 9, 6, 6, 3, 6, 3, 3, 3251UL), // the brute, briefcase in hand
                Agent(1003UL, 5000, -5000, CardinalDirection.West, 8, 6, 8, 8, 1, 3, 8), // the hero
                Agent(1004UL, -5000, 0, CardinalDirection.East, 4, 4, 7, 9, 0, 4, 5, 3221UL), // the saint, bag over her shoulder
                Agent(1005UL, 900, 0, CardinalDirection.South, 6, 6, 5, 1, 8, 4, 6), // the villain
                Agent(1006UL, 5000, 0, CardinalDirection.North, 3, 5, 1, 5, 2, 10, 1), // the nervous wreck
                Agent(1007UL, -5000, 5000, CardinalDirection.South, 5, 10, 5, 5, 3, 6, 4), // the sprinter
                Agent(1008UL, 0, 5000, CardinalDirection.West, 7, 5, 4, 2, 9, 5, 5), // the bully
                Agent(1009UL, 5000, 5000, CardinalDirection.South, 3, 4, 2, 4, 3, 8, 2), // the coward
                Agent(1010UL, 0, -1800, CardinalDirection.North, 5, 5, 5, 6, 3, 5, 5), // ordinary

                // The meeting room: ten more people who cannot see the fire
                // when it starts and only learn about it through the shouting.
                Agent(1011UL, 10500, -4500, CardinalDirection.North, 5, 5, 6, 5, 3, 4, 4), // ordinary
                Agent(1012UL, 13500, -4500, CardinalDirection.West, 9, 4, 7, 6, 2, 3, 5), // the strong one
                Agent(1013UL, 16500, -4500, CardinalDirection.North, 4, 7, 3, 7, 1, 7, 2, 3223UL), // the worrier, bag in hand
                Agent(1014UL, 19500, -4500, CardinalDirection.West, 6, 5, 5, 5, 5, 5, 5), // ordinary
                Agent(1015UL, 10500, 0, CardinalDirection.East, 3, 6, 2, 8, 0, 9, 1), // the timid carer
                Agent(1016UL, 13500, 1200, CardinalDirection.South, 7, 8, 8, 4, 7, 2, 6), // the chancer
                Agent(1017UL, 17200, -1800, CardinalDirection.West, 5, 5, 4, 5, 4, 6, 4), // ordinary
                Agent(1018UL, 19500, 1200, CardinalDirection.North, 8, 6, 6, 2, 8, 4, 6, 3252UL), // the other bully, briefcase in hand
                Agent(1019UL, 12000, 4500, CardinalDirection.South, 4, 9, 5, 6, 2, 6, 3), // the runner
                Agent(1020UL, 18000, 4500, CardinalDirection.South, 6, 5, 7, 9, 1, 3, 9) // the other hero
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
                Bag(3221UL, -5000, 0),
                Bag(3222UL, 1200, -2600),
                Bag(3223UL, 16500, -4500),
                Bag(3224UL, 17500, 3400),
                // Two briefcases and two of the bags start in somebody's hand,
                // so they are parked where their owner stands.
                Briefcase(3251UL, 0, -5000),
                Briefcase(3252UL, 19500, 1200),
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
                OfficeChair(3248UL, 16800, -3600),

                // One extinguisher by each big room's wall.
                Extinguisher(3301UL, -1000, -5700),
                Extinguisher(3302UL, 14000, -5700),

                // Electrical things, which go off when the flames reach them.
                Microwave(3261UL, 5400, -1400),
                Microwave(3262UL, 20400, -3000),
                WallSocket(3271UL, -5800, -4000),
                WallSocket(3272UL, 5800, 4000),
                WallSocket(3273UL, 20800, -1000),

                // Four spares the player can stand anywhere with a card. They
                // are nowhere at all until then.
                SpareExtinguisher(3391UL),
                SpareExtinguisher(3392UL),
                SpareExtinguisher(3393UL),
                SpareExtinguisher(3394UL)
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

        /// <summary>A fire extinguisher: small, heavy for its size, and it never burns.</summary>
        private static FireReactionPhysicsObjectDefinition Extinguisher(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(x, z), 250, 7000);
        }

        private static FireReactionPhysicsObjectDefinition Laptop(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Laptop, new LogicalPosition(x, z), 300, 1500);
        }

        private static FireReactionAgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership));
        }

        /// <summary>The same person, but walking in with something in their hand.</summary>
        private static FireReactionAgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership,
            ulong carrying)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership),
                new SimulationId(carrying));
        }

        /// <summary>
        /// One of the spare extinguishers the player's card puts down. It is not
        /// in the world until then, so its position is never used.
        /// </summary>
        private static FireReactionPhysicsObjectDefinition SpareExtinguisher(ulong id)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(0, 0), 220, 9000, true);
        }

        /// <summary>A microwave on a counter: heavy, and it goes off with a bang.</summary>
        private static FireReactionPhysicsObjectDefinition Microwave(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Microwave, new LogicalPosition(x, z), 450, 14000);
        }

        /// <summary>A wall socket: it never shifts, but it pops.</summary>
        private static FireReactionPhysicsObjectDefinition WallSocket(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WallSocket, new LogicalPosition(x, z), 160, 60000);
        }

        private static FireReactionPhysicsObjectDefinition Briefcase(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Briefcase, new LogicalPosition(x, z), 400, 6000);
        }

        /// <summary>
        /// One alarm on a wall of each room, just inside it so somebody can
        /// stand at it. The closet has none: it is a cupboard.
        /// </summary>
        public static FireReactionAlarmDefinition[] DefaultAlarms()
        {
            return new[]
            {
                // The open-plan office, on the west wall.
                new FireReactionAlarmDefinition(new SimulationId(6001UL), new LogicalPosition(-5700, 2000)),

                // The corridor, on its north wall.
                new FireReactionAlarmDefinition(new SimulationId(6002UL), new LogicalPosition(7500, 700)),

                // The meeting room, on its north wall, well clear of both its
                // doors: an alarm beside a doorway turns into a queue.
                new FireReactionAlarmDefinition(new SimulationId(6003UL), new LogicalPosition(11000, 5700))
            };
        }

        private static FireReactionPhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams);
        }
    }
}
