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

        /// <summary>The chair they are already sitting on when the run starts, or the default ID for standing.</summary>
        [UnityEngine.SerializeField] private SimulationId seatedOnObjectId;

        /// <summary>
        /// How well they know the building. Declared last so it sits last in
        /// the saved asset, and zero -- what anybody authored before it existed
        /// reads back as -- means they know it.
        /// </summary>
        [UnityEngine.SerializeField] private AgentFamiliarity familiarity;

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
            seatedOnObjectId = default;
            familiarity = default;
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
            seatedOnObjectId = default;
            familiarity = default;
        }

        /// <summary>
        /// A person with an authored personality who may walk in carrying
        /// something, and who may already be sitting on a named chair when the
        /// run starts.
        /// </summary>
        public FireReactionAgentDefinition(
            SimulationId agentId,
            LogicalPosition initialPosition,
            CardinalDirection initialFacingDirection,
            AgentTraitValues traits,
            SimulationId carriedObjectId,
            SimulationId seatedOnObjectId = default)
        {
            this.agentId = agentId;
            this.initialPosition = initialPosition;
            this.initialFacingDirection = initialFacingDirection;
            hasAuthoredTraits = true;
            this.traits = traits;
            this.carriedObjectId = carriedObjectId;
            this.seatedOnObjectId = seatedOnObjectId;
            familiarity = default;
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

        /// <summary>The chair they begin the run sitting on, if any.</summary>
        public SimulationId SeatedOnObjectId => seatedOnObjectId;

        public bool StartsSeated => seatedOnObjectId.Value != 0UL;

        /// <summary>Whether they know the building or only the room they start in.</summary>
        public AgentFamiliarity Familiarity => familiarity;

        /// <summary>
        /// The same person, knowing the building this well. A copy rather than
        /// another constructor, so every way of writing a person gains it at once.
        /// </summary>
        public FireReactionAgentDefinition WithFamiliarity(AgentFamiliarity value)
        {
            FireReactionAgentDefinition copy = this;
            copy.familiarity = value;
            return copy;
        }
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
        [UnityEngine.SerializeField] private bool isOpening;

        public FireReactionDoorDefinition(
            SimulationId doorId,
            SimulationId roomId,
            WallSide side,
            int centreAlongWallMillimetres,
            int widthMillimetres,
            bool startsLocked = true,
            bool isOpening = false)
        {
            this.doorId = doorId;
            this.roomId = roomId;
            this.side = side;
            this.centreAlongWallMillimetres = centreAlongWallMillimetres;
            this.widthMillimetres = widthMillimetres;
            this.startsLocked = startsLocked;
            this.isOpening = isOpening;
        }

        public SimulationId DoorId => doorId;

        /// <summary>The room whose wall holds this door; what lies beyond is worked out from the rooms.</summary>
        public SimulationId RoomId => roomId;

        public WallSide Side => side;
        public int CentreAlongWallMillimetres => centreAlongWallMillimetres;
        public int WidthMillimetres => widthMillimetres;

        /// <summary>Locked at the start (the player's doors); otherwise it starts shut but openable.</summary>
        public bool StartsLocked => startsLocked && !isOpening;

        /// <summary>
        /// A doorway with no door in it: an archway, permanently open, which
        /// nobody can shut and the fire walks straight through. This is how two
        /// rectangles are joined into one L- or T-shaped space, because a room
        /// is always a rectangle and a corridor that turns a corner is two of
        /// them. It behaves exactly as a hole blown in a wall already does.
        /// </summary>
        public bool IsOpening => isOpening;
    }

    /// <summary>
    /// A little green sign on the way out, and the way it points.
    /// <para>
    /// For the player's eye only. People find their own way out by the
    /// navigation grid and would do so if every sign were taken down; what the
    /// signs fix is that a player looking at a corridor which Ts at one end
    /// cannot otherwise tell which arm the door is up.
    /// </para>
    /// </summary>
    [Serializable]
    public struct FireReactionExitSignDefinition
    {
        [UnityEngine.SerializeField] private LogicalPosition at;
        [UnityEngine.SerializeField] private int pointingDegrees;

        public FireReactionExitSignDefinition(LogicalPosition at, int pointingDegrees)
        {
            this.at = at;
            this.pointingDegrees = pointingDegrees;
        }

        public LogicalPosition At => at;

        /// <summary>Which way it points: a compass bearing clockwise from north, like every other heading.</summary>
        public int PointingDegrees => pointingDegrees;
    }

    /// <summary>
    /// One run of cable between two electrical things, as a line of corners in
    /// whole millimetres following the walls.
    /// <para>
    /// The route is authored rather than worked out, because both sides of the
    /// line need it and they have to agree. How long the spark takes to travel
    /// is an outcome of the run -- it decides when the next socket pops -- and
    /// where the spark is drawn is a picture. One route, measured once, and
    /// neither can drift from the other.
    /// </para>
    /// </summary>
    [Serializable]
    public struct FireReactionPowerLineDefinition
    {
        [UnityEngine.SerializeField] private SimulationId fromObjectId;
        [UnityEngine.SerializeField] private SimulationId toObjectId;
        [UnityEngine.SerializeField] private LogicalPosition[] corners;

        public FireReactionPowerLineDefinition(SimulationId fromObjectId, SimulationId toObjectId,
            params LogicalPosition[] corners)
        {
            this.fromObjectId = fromObjectId;
            this.toObjectId = toObjectId;
            this.corners = corners;
        }

        public SimulationId FromObjectId => fromObjectId;
        public SimulationId ToObjectId => toObjectId;

        /// <summary>The route, end to end. At least two points, each a corner of the run.</summary>
        public LogicalPosition[] Corners => corners;

        /// <summary>
        /// Two runs of cable are the same run when they join the same things
        /// by the same route. Spelled out because the route is an array, and
        /// without this two identical routes compare as different for having
        /// been built twice -- which is exactly what comparing the saved
        /// scenario against the code does.
        /// </summary>
        public override bool Equals(object other)
        {
            if (!(other is FireReactionPowerLineDefinition line))
            {
                return false;
            }

            if (fromObjectId != line.fromObjectId || toObjectId != line.toObjectId)
            {
                return false;
            }

            LogicalPosition[] mine = corners ?? Array.Empty<LogicalPosition>();
            LogicalPosition[] theirs = line.corners ?? Array.Empty<LogicalPosition>();
            if (mine.Length != theirs.Length)
            {
                return false;
            }

            for (int i = 0; i < mine.Length; i++)
            {
                if (mine[i].X != theirs[i].X || mine[i].Z != theirs[i].Z)
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = fromObjectId.GetHashCode() * 397 ^ toObjectId.GetHashCode();
                LogicalPosition[] route = corners ?? Array.Empty<LogicalPosition>();
                for (int i = 0; i < route.Length; i++)
                {
                    hash = hash * 397 ^ route[i].X;
                    hash = hash * 397 ^ route[i].Z;
                }

                return hash;
            }
        }

        /// <summary>How far the spark has to travel, in whole millimetres.</summary>
        public int LengthMillimetres
        {
            get
            {
                int total = 0;
                for (int i = 1; i < corners.Length; i++)
                {
                    total += System.Math.Abs(corners[i].X - corners[i - 1].X) +
                             System.Math.Abs(corners[i].Z - corners[i - 1].Z);
                }

                return total;
            }
        }
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

        /// <summary>Which way it faces at the start, in whole degrees. Chairs use this to face their table.</summary>
        [UnityEngine.SerializeField] private int initialFacingDegrees;

        /// <summary>
        /// It starts resting on a table or on another object rather than on the
        /// floor: a laptop on a desk, the upper box of a stacked pair.
        /// </summary>
        [UnityEngine.SerializeField] private bool startsResting;

        public FireReactionPhysicsObjectDefinition(
            SimulationId objectId,
            PhysicsObjectKind kind,
            LogicalPosition initialPosition,
            int sizeMillimetres,
            int massGrams,
            bool startsDormant = false,
            int initialFacingDegrees = 0,
            bool startsResting = false)
        {
            this.objectId = objectId;
            this.kind = kind;
            this.initialPosition = initialPosition;
            this.sizeMillimetres = sizeMillimetres;
            this.massGrams = massGrams;
            this.startsDormant = startsDormant;
            this.initialFacingDegrees = initialFacingDegrees;
            this.startsResting = startsResting;
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

        /// <summary>Which way it faces at the start, in whole degrees.</summary>
        public int InitialFacingDegrees => initialFacingDegrees;

        /// <summary>
        /// True when it begins on top of something instead of on the floor. It
        /// is not an obstacle while it rests there, and it comes loose the
        /// moment anybody picks it up, throws it, or breaks what holds it.
        /// </summary>
        public bool StartsResting => startsResting;
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
        public string ContentRevision = "55";
        public ulong DefaultSeed = 42UL;

        // 43: a stranger who has walked into a room to look round it looks
        // round it before weighing any other room, unless its unseen corner is
        // by the danger or cannot be reached.
        // 41: people plan their escape only through doors they know. Visitors
        // look for a way out, and seeing a door, reading a sign, a door opening
        // beside them and a leader's shout all teach them one.
        // 40: the tick schedule gained a phase. The cable between the sockets
        // and the fuse box advances its sparks beside the fire, in phase 2, so
        // a run recorded before this one cannot be replayed against it.
        // 39: doors cost influence to work, a shut door standing in the flames
        // burns through instead of holding them off for ever, the round runs
        // until everybody is out or dead rather than until they are merely out
        // of reach, nobody shuts a door they are about to run through, chairs
        // are furniture rather than clutter to be carried about, and nothing
        // made of furniture smashes any more. All of it changes what a run
        // produces, so every recorded replay fingerprint was re-recorded.
        public int SimulationCompatibilityVersion = 43;

        public WorldSettings World = new WorldSettings();
        public PerceptionSettings Perception = new PerceptionSettings();
        public FireSettings Fire = new FireSettings { SpawnAreas = PrototypeBuilding.DefaultFireAreas() };
        public PowerSettings Power = new PowerSettings();
        public RoundSettings Round = new RoundSettings();
        public SteeringSettings Steering = new SteeringSettings();
        public CalmSettings Calm = new CalmSettings();
        public PanicSettings Panic = new PanicSettings();
        public TemperamentSettings Temperament = new TemperamentSettings();
        public HearingSettings Hearing = new HearingSettings();
        public FallSettings Falls = new FallSettings();
        public ExitSettings Exits = new ExitSettings();
        public ObjectPhysicsSettings ObjectPhysics = new ObjectPhysicsSettings();
        public PhysicsFeelSettings PhysicsFeel = new PhysicsFeelSettings();
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

        public FireReactionAgentDefinition[] Agents = PrototypeBuilding.DefaultAgents();
        public FireReactionDoorDefinition[] Doors = PrototypeBuilding.DefaultDoors();
        public FireReactionPhysicsObjectDefinition[] PhysicsObjects = PrototypeBuilding.DefaultPhysicsObjects();
        public FireReactionTableDefinition[] Tables = PrototypeBuilding.DefaultTables();
        public FireReactionRoomDefinition[] Rooms = PrototypeBuilding.DefaultRooms();
        public FireReactionAlarmDefinition[] Alarms = PrototypeBuilding.DefaultAlarms();

        /// <summary>
        /// The sticks of TNT the player has: one spare opening each, kept out of
        /// the world until a charge is spent on a wall.
        /// </summary>
        public SimulationId[] BlastHoles = DefaultBlastHoles();

        /// <summary>The cable running from socket to socket and back to the fuse box.</summary>
        public FireReactionPowerLineDefinition[] PowerLines = PrototypeBuilding.DefaultPowerLines();

        /// <summary>The signs pointing the way out. Nothing in the run reads them.</summary>
        public FireReactionExitSignDefinition[] ExitSigns = PrototypeBuilding.DefaultExitSigns();

        /// <summary>A deep copy: changing the copy never changes this one.</summary>
        public FireReactionScenarioData Clone()
        {
            var copy = (FireReactionScenarioData)MemberwiseClone();
            copy.World = World?.Clone();
            copy.Perception = Perception?.Clone();
            copy.Fire = Fire?.Clone();
            copy.Power = Power?.Clone();
            copy.Round = Round?.Clone();
            copy.Steering = Steering?.Clone();
            copy.Calm = Calm?.Clone();
            copy.Panic = Panic?.Clone();
            copy.Temperament = Temperament?.Clone();
            copy.Hearing = Hearing?.Clone();
            copy.Falls = Falls?.Clone();
            copy.Exits = Exits?.Clone();
            copy.ObjectPhysics = ObjectPhysics?.Clone();
            copy.PhysicsFeel = PhysicsFeel?.Clone();
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
            copy.PowerLines = (FireReactionPowerLineDefinition[])PowerLines?.Clone();
            copy.ExitSigns = (FireReactionExitSignDefinition[])ExitSigns?.Clone();
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

            if (World == null || Perception == null || Fire == null || Round == null || Steering == null || Calm == null ||
                Panic == null || Temperament == null || Hearing == null || Falls == null || Exits == null ||
                ObjectPhysics == null || PhysicsFeel == null || Traits == null || Flammables == null || Items == null || Help == null ||
                Influence == null || Alarm == null || Blockades == null || Blast == null ||
                Extinguishers == null || Leadership == null)
            {
                throw new InvalidOperationException("A fire-reaction scenario is missing a settings group.");
            }

            World.Validate();
            Perception.Validate();
            Fire.Validate();
            Round.Validate();
            Steering.Validate();
            Calm.Validate();
            Panic.Validate();
            Temperament.Validate();
            Hearing.Validate();
            Falls.Validate();
            Exits.Validate(World);
            ObjectPhysics.Validate();
            PhysicsFeel.Validate();
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
            Power.Validate();
            Settings.Require(Calm.SpeedMaximum + Traits.CalmSpeedJitter <= World.MaximumStepDistanceMillimetres &&
                             Panic.SpeedMaximum + Traits.PanicSpeedJitter <= World.MaximumStepDistanceMillimetres,
                "speeds within the maximum step");

            var roomIds = new HashSet<SimulationId>();
            ValidateRooms(roomIds);

            // The fire starts somewhere inside a room -- any room, not just the
            // first one. It used to have to be the first, which was fine while
            // there was one room worth burning; now that the danger may begin
            // anywhere on the floor, what matters is only that every rectangle
            // it could be drawn from is a room rather than a wall or the street.
            for (int i = 0; i < Fire.SpawnAreas.Length; i++)
            {
                ValidateSpawnArea(Fire.SpawnAreas[i]);
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

                if (!Enum.IsDefined(typeof(AgentFamiliarity), agent.Familiarity))
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} has an unknown familiarity with the building.");
                }

                if (RoomHolding(StartPositionOf(agent), radius) < 0)
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} starts outside every room.");
                }

                if (agent.StartsSeated)
                {
                    ValidateStartingSeat(agent);
                }

                for (int previous = 0; previous < i; previous++)
                {
                    long distanceSquared = LogicalPosition.DistanceSquared(
                        StartPositionOf(agent), StartPositionOf(Agents[previous]));
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
            ValidatePowerLines();
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
        /// <summary>
        /// A rectangle the fire may be drawn from has to sit inside one room,
        /// clear of its walls. Spanning two rooms is refused even when both
        /// ends are indoors, because the space between them is a wall.
        /// </summary>
        private void ValidateSpawnArea(LogicalBounds area)
        {
            int margin = Fire.CellSizeMillimetres / 2;
            var lowest = new LogicalPosition(area.MinX, area.MinZ);
            var highest = new LogicalPosition(area.MaxX, area.MaxZ);
            for (int r = 0; r < Rooms.Length; r++)
            {
                LogicalBounds room = Rooms[r].Bounds;
                if (room.ContainsCircle(lowest, margin) && room.ContainsCircle(highest, margin))
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "A fire spawn rectangle must lie inside one room, clear of its walls.");
        }

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
                    if (bounds.DistanceSquaredTo(StartPositionOf(Agents[a])) < (long)radius * radius)
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

                // Not on the floor, so it is in nothing's way and may sit
                // inside a table's footprint or on top of another object: a
                // laptop on a desk, the upper box of a stacked pair.
                bool offTheFloor = inSomebodysHand || body.StartsResting;

                if (body.SizeMillimetres < 100 || body.SizeMillimetres > 1000 ||
                    body.MassGrams <= 0 || body.MassGrams > 200000)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} has an invalid size or mass.");
                }

                if (!body.StartsDormant && RoomHolding(body.InitialPosition, body.RadiusMillimetres) < 0)
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} starts outside every room.");
                }

                if (body.StartsResting && !RestsOnSomething(body))
                {
                    throw new InvalidOperationException(
                        $"Object {body.ObjectId} starts resting on nothing: put it on a table or on another object.");
                }

                for (int t = 0; t < Tables.Length && !offTheFloor; t++)
                {
                    if (Tables[t].Bounds.DistanceSquaredTo(body.InitialPosition) < (long)body.RadiusMillimetres * body.RadiusMillimetres)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts inside table {Tables[t].TableId}.");
                    }
                }

                // A chair somebody starts sitting on has that person standing
                // exactly where it is, which is the point of it.
                bool satOn = IsSatOnAtTheStart(body.ObjectId);
                long agentReach = radius + (long)body.RadiusMillimetres;
                for (int a = 0; a < Agents.Length && !offTheFloor && !satOn; a++)
                {
                    if (LogicalPosition.DistanceSquared(body.InitialPosition, StartPositionOf(Agents[a])) < agentReach * agentReach)
                    {
                        throw new InvalidOperationException($"Object {body.ObjectId} starts on top of a person.");
                    }
                }

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionPhysicsObjectDefinition other = PhysicsObjects[previous];
                    if (offTheFloor || other.StartsResting || IsCarriedAtTheStart(other.ObjectId))
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
        /// <summary>
        /// The cable has to make sense before a run starts: every end a real
        /// electrical thing, every leg square to the walls, and every socket
        /// able to reach the fuse box. A cable that goes nowhere would simply
        /// do nothing, silently, for the whole round.
        /// </summary>
        private void ValidatePowerLines()
        {
            PowerLines ??= Array.Empty<FireReactionPowerLineDefinition>();
            if (PowerLines.Length == 0)
            {
                return;
            }

            var electrical = new Dictionary<SimulationId, PhysicsObjectKind>();
            for (int i = 0; i < PhysicsObjects.Length; i++)
            {
                PhysicsObjectKind kind = PhysicsObjects[i].Kind;
                if (kind == PhysicsObjectKind.WallSocket || kind == PhysicsObjectKind.FuseBox)
                {
                    electrical[PhysicsObjects[i].ObjectId] = kind;
                }
            }

            int real = 0;
            for (int i = 0; i < PowerLines.Length; i++)
            {
                FireReactionPowerLineDefinition line = PowerLines[i];

                // A run to something this building does not have is simply not
                // there. Cable is an attribute of the things it joins, so a
                // scenario that swaps out the clutter loses the wiring with it
                // rather than having to remember to delete it as well.
                if (!electrical.ContainsKey(line.FromObjectId) || !electrical.ContainsKey(line.ToObjectId))
                {
                    continue;
                }

                real++;
                if (line.FromObjectId == line.ToObjectId)
                {
                    throw new InvalidOperationException($"Power line {i} joins {line.FromObjectId} to itself.");
                }

                LogicalPosition[] corners = line.Corners;
                if (corners == null || corners.Length < 2)
                {
                    throw new InvalidOperationException($"Power line {i} needs at least two corners.");
                }

                for (int c = 1; c < corners.Length; c++)
                {
                    bool alongX = corners[c].Z == corners[c - 1].Z;
                    bool alongZ = corners[c].X == corners[c - 1].X;
                    if (alongX == alongZ)
                    {
                        throw new InvalidOperationException(
                            $"Power line {i} has a leg that is diagonal or goes nowhere; cable follows the walls.");
                    }
                }
            }

            if (real == 0)
            {
                // No cable in this building at all.
                return;
            }

            // Everything electrical has to be reachable from the fuse box, or a
            // socket popping would light a cable that leads nowhere.
            SimulationId fuseBox = default;
            int boxes = 0;
            foreach (KeyValuePair<SimulationId, PhysicsObjectKind> thing in electrical)
            {
                if (thing.Value == PhysicsObjectKind.FuseBox)
                {
                    fuseBox = thing.Key;
                    boxes++;
                }
            }

            if (boxes != 1)
            {
                throw new InvalidOperationException(
                    $"A building with cable in it needs exactly one fuse box, and this one has {boxes}.");
            }

            var reached = new HashSet<SimulationId> { fuseBox };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < PowerLines.Length; i++)
                {
                    SimulationId from = PowerLines[i].FromObjectId;
                    SimulationId to = PowerLines[i].ToObjectId;
                    if (!electrical.ContainsKey(from) || !electrical.ContainsKey(to))
                    {
                        continue;
                    }

                    if (reached.Contains(from) && reached.Add(to))
                    {
                        grew = true;
                    }
                    else if (reached.Contains(to) && reached.Add(from))
                    {
                        grew = true;
                    }
                }
            }

            foreach (KeyValuePair<SimulationId, PhysicsObjectKind> thing in electrical)
            {
                if (!reached.Contains(thing.Key))
                {
                    throw new InvalidOperationException(
                        $"{thing.Key} has no cable back to the fuse box, so nothing could ever reach it.");
                }
            }
        }

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
        /// Somebody who starts the run seated must name a real chair, and one
        /// chair each. Where they stand is the chair's business, not theirs:
        /// the run settles them onto it at tick zero and everything here
        /// measures them from it, so an authored position that disagrees is
        /// simply ignored rather than refused.
        /// </summary>
        private void ValidateStartingSeat(FireReactionAgentDefinition agent)
        {
            int seat = Array.FindIndex(PhysicsObjects ?? Array.Empty<FireReactionPhysicsObjectDefinition>(),
                o => o.ObjectId == agent.SeatedOnObjectId);
            if (seat < 0)
            {
                throw new InvalidOperationException(
                    $"Agent {agent.AgentId} starts seated on {agent.SeatedOnObjectId}, which is not in the scenario.");
            }

            FireReactionPhysicsObjectDefinition chair = PhysicsObjects[seat];
            if (chair.Kind != PhysicsObjectKind.Chair && chair.Kind != PhysicsObjectKind.OfficeChair)
            {
                throw new InvalidOperationException(
                    $"Agent {agent.AgentId} starts seated on {chair.ObjectId}, which is not a chair.");
            }

            for (int a = 0; a < Agents.Length; a++)
            {
                if (Agents[a].AgentId != agent.AgentId && Agents[a].SeatedOnObjectId == agent.SeatedOnObjectId)
                {
                    throw new InvalidOperationException(
                        $"Agents {Agents[a].AgentId} and {agent.AgentId} both start on chair {chair.ObjectId}.");
                }
            }
        }

        /// <summary>
        /// Where somebody actually begins the run: on their chair if they start
        /// seated, otherwise where they are authored. Everything that checks a
        /// person against the room, the furniture and each other asks this, so
        /// it checks where they will really be standing.
        /// </summary>
        private LogicalPosition StartPositionOf(FireReactionAgentDefinition agent)
        {
            if (!agent.StartsSeated || PhysicsObjects == null)
            {
                return agent.InitialPosition;
            }

            for (int o = 0; o < PhysicsObjects.Length; o++)
            {
                if (PhysicsObjects[o].ObjectId == agent.SeatedOnObjectId)
                {
                    return PhysicsObjects[o].InitialPosition;
                }
            }

            return agent.InitialPosition;
        }

        /// <summary>Whether somebody begins the run sitting on this chair.</summary>
        private bool IsSatOnAtTheStart(SimulationId objectId)
        {
            for (int a = 0; a < Agents.Length; a++)
            {
                if (Agents[a].SeatedOnObjectId == objectId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether something authored as resting really has something under it:
        /// a table whose top it sits within, or another object on the floor at
        /// the same spot. Catches a laptop authored into mid-air.
        /// </summary>
        private bool RestsOnSomething(FireReactionPhysicsObjectDefinition body)
        {
            for (int t = 0; t < Tables.Length; t++)
            {
                if (Tables[t].Bounds.ContainsCircle(body.InitialPosition, body.RadiusMillimetres))
                {
                    return true;
                }
            }

            for (int o = 0; o < PhysicsObjects.Length; o++)
            {
                FireReactionPhysicsObjectDefinition under = PhysicsObjects[o];
                if (under.ObjectId == body.ObjectId || under.StartsResting || under.StartsDormant)
                {
                    continue;
                }

                long reach = under.RadiusMillimetres;
                if (LogicalPosition.DistanceSquared(body.InitialPosition, under.InitialPosition) <= reach * reach)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
