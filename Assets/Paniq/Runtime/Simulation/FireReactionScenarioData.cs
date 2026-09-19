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
        [UnityEngine.SerializeField] private WallSide side;
        [UnityEngine.SerializeField] private int centreAlongWallMillimetres;
        [UnityEngine.SerializeField] private int widthMillimetres;

        public FireReactionDoorDefinition(SimulationId doorId, WallSide side, int centreAlongWallMillimetres, int widthMillimetres)
        {
            this.doorId = doorId;
            this.side = side;
            this.centreAlongWallMillimetres = centreAlongWallMillimetres;
            this.widthMillimetres = widthMillimetres;
        }

        public SimulationId DoorId => doorId;
        public WallSide Side => side;
        public int CentreAlongWallMillimetres => centreAlongWallMillimetres;
        public int WidthMillimetres => widthMillimetres;
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
    /// A small room behind one of the main room's doors. Its rectangle must
    /// sit flush against that door's wall, on the outside, with the whole
    /// door gap along it. People who reach it shelter there; fire only gets
    /// in through the door while it is open.
    /// </summary>
    [Serializable]
    public struct FireReactionSideRoomDefinition
    {
        [UnityEngine.SerializeField] private SimulationId roomId;
        [UnityEngine.SerializeField] private SimulationId doorId;
        [UnityEngine.SerializeField] private LogicalBounds bounds;

        public FireReactionSideRoomDefinition(SimulationId roomId, SimulationId doorId, LogicalBounds bounds)
        {
            this.roomId = roomId;
            this.doorId = doorId;
            this.bounds = bounds;
        }

        public SimulationId RoomId => roomId;
        public SimulationId DoorId => doorId;
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
        public string ContentRevision = "22";
        public ulong DefaultSeed = 42UL;
        public int SimulationCompatibilityVersion = 14;

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
        public FireReactionSideRoomDefinition[] SideRooms = DefaultSideRooms();

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
            copy.SideRooms = (FireReactionSideRoomDefinition[])SideRooms?.Clone();
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

            LogicalBounds room = World.RoomBounds;
            int spawnMargin = Fire.CellSizeMillimetres / 2;
            if (!room.ContainsCircle(new LogicalPosition(Fire.SpawnBounds.MinX, Fire.SpawnBounds.MinZ), spawnMargin) ||
                !room.ContainsCircle(new LogicalPosition(Fire.SpawnBounds.MaxX, Fire.SpawnBounds.MaxZ), spawnMargin))
            {
                throw new InvalidOperationException("The fire spawn rectangle must remain inside the room.");
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

                if (!room.ContainsCircle(agent.InitialPosition, radius) && !InAnySideRoom(agent.InitialPosition, radius))
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} starts outside the room.");
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

            ValidateDoors(ids);
            ValidateSideRooms(ids);
            ValidateTables(ids);
            ValidatePhysicsObjects(ids);
        }

        private bool InAnySideRoom(LogicalPosition position, int radius)
        {
            if (SideRooms == null)
            {
                return false;
            }

            foreach (FireReactionSideRoomDefinition side in SideRooms)
            {
                if (side.Bounds.ContainsCircle(position, radius))
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidateSideRooms(HashSet<SimulationId> ids)
        {
            SideRooms ??= Array.Empty<FireReactionSideRoomDefinition>();
            LogicalBounds room = World.RoomBounds;
            var usedDoors = new HashSet<SimulationId>();
            foreach (FireReactionSideRoomDefinition side in SideRooms)
            {
                if (side.RoomId.Value == 0UL || !ids.Add(side.RoomId) || !usedDoors.Add(side.DoorId))
                {
                    throw new InvalidOperationException("Side room IDs must be unique and non-zero, one room per door.");
                }

                int door = Array.FindIndex(Doors, d => d.DoorId == side.DoorId);
                if (door < 0)
                {
                    throw new InvalidOperationException($"Side room {side.RoomId} names an unknown door.");
                }

                FireReactionDoorDefinition d = Doors[door];
                LogicalBounds b = side.Bounds;
                int half = d.WidthMillimetres / 2;
                bool flush;
                bool coversGap;
                switch (d.Side)
                {
                    case WallSide.North:
                        flush = b.MinZ == room.MaxZ;
                        coversGap = b.MinX <= d.CentreAlongWallMillimetres - half && b.MaxX >= d.CentreAlongWallMillimetres + half;
                        break;
                    case WallSide.South:
                        flush = b.MaxZ == room.MinZ;
                        coversGap = b.MinX <= d.CentreAlongWallMillimetres - half && b.MaxX >= d.CentreAlongWallMillimetres + half;
                        break;
                    case WallSide.East:
                        flush = b.MinX == room.MaxX;
                        coversGap = b.MinZ <= d.CentreAlongWallMillimetres - half && b.MaxZ >= d.CentreAlongWallMillimetres + half;
                        break;
                    default:
                        flush = b.MaxX == room.MinX;
                        coversGap = b.MinZ <= d.CentreAlongWallMillimetres - half && b.MaxZ >= d.CentreAlongWallMillimetres + half;
                        break;
                }

                if (!flush || !coversGap || b.MaxX - b.MinX < World.OccupancyRadiusMillimetres * 4 ||
                    b.MaxZ - b.MinZ < World.OccupancyRadiusMillimetres * 4)
                {
                    throw new InvalidOperationException($"Side room {side.RoomId} must sit flush outside its door's wall, around the door.");
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
                    bounds.MinX < World.RoomBounds.MinX || bounds.MaxX > World.RoomBounds.MaxX ||
                    bounds.MinZ < World.RoomBounds.MinZ || bounds.MaxZ > World.RoomBounds.MaxZ)
                {
                    throw new InvalidOperationException($"Table {table.TableId} is too small or outside the room.");
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
            LogicalBounds room = World.RoomBounds;
            int radius = World.OccupancyRadiusMillimetres;
            for (int i = 0; i < Doors.Length; i++)
            {
                FireReactionDoorDefinition door = Doors[i];
                if (door.DoorId.Value == 0UL || !ids.Add(door.DoorId))
                {
                    throw new InvalidOperationException("Door IDs must be unique and non-zero.");
                }

                // Wide enough for one person, with a solid bit of wall either side.
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

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionDoorDefinition other = Doors[previous];
                    if (other.Side == door.Side &&
                        Math.Abs((long)other.CentreAlongWallMillimetres - door.CentreAlongWallMillimetres) <
                        (other.WidthMillimetres + door.WidthMillimetres) / 2 + radius * 2)
                    {
                        throw new InvalidOperationException("Doors in the same wall cannot overlap.");
                    }
                }
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

                if (!World.RoomBounds.ContainsCircle(body.InitialPosition, body.RadiusMillimetres))
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} starts outside the room.");
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
        /// Ten people spread around the room, facing different ways, each
        /// with an authored personality so every trait shows up in play:
        /// Str, Spd, Brv, Cmp, Evl, Nrv.
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
                Agent(1010UL, 0, -1800, CardinalDirection.North, 5, 5, 5, 6, 3, 5) // ordinary
            };
        }

        /// <summary>One 1 m door per wall, set off-centre in a pinwheel so each corner has a different nearest exit.</summary>
        public static FireReactionDoorDefinition[] DefaultDoors()
        {
            return new[]
            {
                new FireReactionDoorDefinition(new SimulationId(2001UL), WallSide.North, -2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2002UL), WallSide.East, 2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2003UL), WallSide.South, 2500, 1000),
                new FireReactionDoorDefinition(new SimulationId(2004UL), WallSide.West, -2500, 1000)
            };
        }

        /// <summary>
        /// Eight cardboard boxes, 0.3–0.6 m wide and 3–20 kg, set between
        /// where people stand, and eight 5 kg chairs pulled up to the tables.
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
                Chair(3108UL, -625, 2000)
            };
        }

        /// <summary>A 2 × 2 m room behind the east door: room for two or three people.</summary>
        public static FireReactionSideRoomDefinition[] DefaultSideRooms()
        {
            return new[]
            {
                new FireReactionSideRoomDefinition(new SimulationId(5001UL), new SimulationId(2002UL),
                    new LogicalBounds(6000, 8000, 1500, 3500))
            };
        }

        /// <summary>Three 1.2 × 0.7 m tables around the room.</summary>
        public static FireReactionTableDefinition[] DefaultTables()
        {
            return new[]
            {
                new FireReactionTableDefinition(new SimulationId(4001UL), new LogicalPosition(-2500, -1500), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4002UL), new LogicalPosition(2500, 2800), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4003UL), new LogicalPosition(-1500, 2000), 1200, 700)
            };
        }

        private static FireReactionPhysicsObjectDefinition Chair(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Chair, new LogicalPosition(x, z), 450, 5000);
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
