using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    [Serializable]
    public struct FireReactionAgentDefinition
    {
        [UnityEngine.SerializeField] private StableAgentId agentId;
        [UnityEngine.SerializeField] private LogicalPosition initialPosition;
        [UnityEngine.SerializeField] private CardinalDirection initialFacingDirection;

        public FireReactionAgentDefinition(StableAgentId agentId, LogicalPosition initialPosition)
            : this(agentId, initialPosition, CardinalDirection.North)
        {
        }

        public FireReactionAgentDefinition(
            StableAgentId agentId,
            LogicalPosition initialPosition,
            CardinalDirection initialFacingDirection)
        {
            this.agentId = agentId;
            this.initialPosition = initialPosition;
            this.initialFacingDirection = initialFacingDirection;
        }

        public StableAgentId AgentId => agentId;
        public LogicalPosition InitialPosition => initialPosition;
        public CardinalDirection InitialFacingDirection => initialFacingDirection;
    }

    /// <summary>
    /// A door in one of the room's walls. Its position is the centre of the
    /// gap, measured along the wall (X for north and south walls, Z for east
    /// and west walls). Every door starts locked.
    /// </summary>
    [Serializable]
    public struct FireReactionDoorDefinition
    {
        [UnityEngine.SerializeField] private StableAgentId doorId;
        [UnityEngine.SerializeField] private WallSide side;
        [UnityEngine.SerializeField] private int centreAlongWallMillimetres;
        [UnityEngine.SerializeField] private int widthMillimetres;

        public FireReactionDoorDefinition(StableAgentId doorId, WallSide side, int centreAlongWallMillimetres, int widthMillimetres)
        {
            this.doorId = doorId;
            this.side = side;
            this.centreAlongWallMillimetres = centreAlongWallMillimetres;
            this.widthMillimetres = widthMillimetres;
        }

        public StableAgentId DoorId => doorId;
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
        [UnityEngine.SerializeField] private StableAgentId objectId;
        [UnityEngine.SerializeField] private PhysicsObjectKind kind;
        [UnityEngine.SerializeField] private LogicalPosition initialPosition;
        [UnityEngine.SerializeField] private int sizeMillimetres;
        [UnityEngine.SerializeField] private int massGrams;

        public FireReactionPhysicsObjectDefinition(
            StableAgentId objectId,
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

        public StableAgentId ObjectId => objectId;
        public PhysicsObjectKind Kind => kind;
        public LogicalPosition InitialPosition => initialPosition;
        public int SizeMillimetres => sizeMillimetres;
        public int MassGrams => massGrams;
        public int RadiusMillimetres => sizeMillimetres / 2;
    }

    /// <summary>
    /// Runtime-only copy of scenario values; it cannot write the asset. Speeds
    /// are millimetres per tick, turn rates degrees per tick, durations ticks
    /// (50 ticks = 1 second).
    /// </summary>
    public sealed class FireReactionScenarioData
    {
        public string ScenarioId { get; set; }
        public string ContentRevision { get; set; }
        public ulong DefaultSeed { get; set; }
        public int SimulationCompatibilityVersion { get; set; }

        // World
        public LogicalBounds RoomBounds { get; set; }
        public int OccupancyRadiusMillimetres { get; set; }
        public int MaximumStepDistanceMillimetres { get; set; }

        // Perception
        public int VisionRangeMillimetres { get; set; }
        public int YellRadiusMillimetres { get; set; }
        public int MaximumReactionDelayTicks { get; set; }

        // Fire
        public LogicalBounds FireSpawnBounds { get; set; }
        public int FireActivationTick { get; set; }
        public int FireCellSizeMillimetres { get; set; }
        public int FireSpreadMinimumTicks { get; set; }
        public int FireSpreadMaximumTicks { get; set; }

        // Shared steering
        public int PersonalSpaceMillimetres { get; set; }
        public int WallAvoidDistanceMillimetres { get; set; }

        // Calm
        public int CalmSpeedMinimum { get; set; }
        public int CalmSpeedMaximum { get; set; }
        public int CalmTurnRateMinimum { get; set; }
        public int CalmTurnRateMaximum { get; set; }
        public int CalmAcceleration { get; set; }
        public int CalmDecisionMinimumTicks { get; set; }
        public int CalmDecisionMaximumTicks { get; set; }

        // Panic
        public int PanicSpeedMinimum { get; set; }
        public int PanicSpeedMaximum { get; set; }
        public int PanicTurnRateMinimum { get; set; }
        public int PanicTurnRateMaximum { get; set; }
        public int PanicAcceleration { get; set; }
        public int PanicDecisionMinimumTicks { get; set; }
        public int PanicDecisionMaximumTicks { get; set; }
        public int SwerveChancePercent { get; set; }
        public int SwerveAngleMinimum { get; set; }
        public int SwerveAngleMaximum { get; set; }
        public int SwerveMinimumTicks { get; set; }
        public int SwerveMaximumTicks { get; set; }
        public int HesitateChancePercent { get; set; }
        public int HesitateMinimumTicks { get; set; }
        public int HesitateMaximumTicks { get; set; }
        public int EscapeSampleCount { get; set; }
        public int DangerDistanceMillimetres { get; set; }
        public int FollowRadiusMillimetres { get; set; }
        public int PanicShoutMinimumTicks { get; set; }
        public int PanicShoutMaximumTicks { get; set; }

        // Temperament
        public int FreezeThenRunPercent { get; set; }
        public int FreezeForeverPercent { get; set; }
        public int FreezeMinimumTicks { get; set; }
        public int FreezeMaximumTicks { get; set; }

        // Sound
        public int YellHearingRadiusMillimetres { get; set; }
        public int FireHearingRadiusMillimetres { get; set; }
        public int BumpSoundRadiusMillimetres { get; set; }
        public int InvestigateMinimumTicks { get; set; }
        public int InvestigateMaximumTicks { get; set; }

        // Collisions and falls
        public int BumpMinimumSpeed { get; set; }
        public int KnockdownClosingSpeed { get; set; }
        public int StaggerMinimumTicks { get; set; }
        public int StaggerMaximumTicks { get; set; }
        public int KnockdownMinimumTicks { get; set; }
        public int KnockdownMaximumTicks { get; set; }
        public int GetUpTicks { get; set; }
        public int TripChancePercent { get; set; }
        public int TripMinimumSpeed { get; set; }
        public int TripMinimumTicks { get; set; }
        public int TripMaximumTicks { get; set; }

        // Doors
        public int DoorwayDepthMillimetres { get; set; }
        public int EscapeDepthMillimetres { get; set; }
        public int DoorOpenTicks { get; set; }
        public int DoorTryTicks { get; set; }
        public int DoorForceChancePercent { get; set; }
        public int DoorForceMinimumTicks { get; set; }
        public int DoorForceMaximumTicks { get; set; }
        public int DoorShoveMinimumTicks { get; set; }
        public int DoorShoveMaximumTicks { get; set; }
        public int DoorAvoidMinimumTicks { get; set; }
        public int DoorAvoidMaximumTicks { get; set; }
        public int DoorCrowdedAvoidMinimumTicks { get; set; }
        public int DoorCrowdedAvoidMaximumTicks { get; set; }

        // Physical objects. Object velocities inside the simulation are
        // hundredths of a millimetre per tick; friction is in those units per
        // tick. Momentum is kilograms times millimetres per tick.
        public int AgentMassGrams { get; set; }
        public int ObjectFriction { get; set; }
        public int ObjectAgentRestitutionPercent { get; set; }
        public int ObjectObjectRestitutionPercent { get; set; }
        public int ObjectWallRestitutionPercent { get; set; }
        public int ObjectTripMinimumSpeed { get; set; }
        public int ObjectTripScale { get; set; }
        public int ObjectTripMaximumChancePercent { get; set; }
        public int ObjectStaggerMomentum { get; set; }
        public int ObjectKnockdownMomentum { get; set; }

        public FireReactionAgentDefinition[] Agents { get; set; }
        public FireReactionDoorDefinition[] Doors { get; set; } = Array.Empty<FireReactionDoorDefinition>();
        public FireReactionPhysicsObjectDefinition[] PhysicsObjects { get; set; } = Array.Empty<FireReactionPhysicsObjectDefinition>();

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

            Require(RoomBounds.MinX < RoomBounds.MaxX && RoomBounds.MinZ < RoomBounds.MaxZ, "room bounds");
            Require(RoomBounds.MinX >= -100000 && RoomBounds.MaxX <= 100000 &&
                    RoomBounds.MinZ >= -100000 && RoomBounds.MaxZ <= 100000, "room within the 200 m coordinate span");
            Require(OccupancyRadiusMillimetres > 0 && OccupancyRadiusMillimetres <= 2000, "occupancy radius");
            Require(MaximumStepDistanceMillimetres >= 0 && MaximumStepDistanceMillimetres <= 1000, "maximum step");
            Require(VisionRangeMillimetres > 0 && YellRadiusMillimetres > 0 && MaximumReactionDelayTicks >= 0, "perception");
            Require(FireSpawnBounds.MinX <= FireSpawnBounds.MaxX && FireSpawnBounds.MinZ <= FireSpawnBounds.MaxZ, "fire spawn bounds");
            Require(FireActivationTick >= 0 && FireCellSizeMillimetres >= 100, "fire timing and cell size");
            Require(FireSpreadMinimumTicks > 0 && FireSpreadMaximumTicks >= FireSpreadMinimumTicks, "fire spread interval");
            Require(PersonalSpaceMillimetres >= 0 && WallAvoidDistanceMillimetres >= 0, "steering distances");
            Require(Range(CalmSpeedMinimum, CalmSpeedMaximum, 0), "calm speed");
            Require(Range(PanicSpeedMinimum, PanicSpeedMaximum, 0), "panic speed");
            Require(CalmSpeedMaximum <= MaximumStepDistanceMillimetres &&
                    PanicSpeedMaximum <= MaximumStepDistanceMillimetres, "speeds within the maximum step");
            Require(Range(CalmTurnRateMinimum, CalmTurnRateMaximum, 1) &&
                    Range(PanicTurnRateMinimum, PanicTurnRateMaximum, 1), "turn rates");
            Require(CalmAcceleration > 0 && PanicAcceleration > 0, "acceleration");
            Require(Range(CalmDecisionMinimumTicks, CalmDecisionMaximumTicks, 1) &&
                    Range(PanicDecisionMinimumTicks, PanicDecisionMaximumTicks, 1), "decision intervals");
            Require(SwerveChancePercent >= 0 && SwerveChancePercent <= 100 &&
                    HesitateChancePercent >= 0 && HesitateChancePercent <= 100, "chances");
            Require(Range(SwerveAngleMinimum, SwerveAngleMaximum, 0) && SwerveAngleMaximum <= 180 &&
                    Range(SwerveMinimumTicks, SwerveMaximumTicks, 1) &&
                    Range(HesitateMinimumTicks, HesitateMaximumTicks, 1), "swerve and hesitation");
            Require(EscapeSampleCount >= 1 && DangerDistanceMillimetres >= 0 && FollowRadiusMillimetres >= 0, "panic choices");
            Require(Range(PanicShoutMinimumTicks, PanicShoutMaximumTicks, 1), "panic shouts");
            Require(FreezeThenRunPercent >= 0 && FreezeForeverPercent >= 0 &&
                    FreezeThenRunPercent + FreezeForeverPercent <= 100 &&
                    Range(FreezeMinimumTicks, FreezeMaximumTicks, 1), "temperaments");
            Require(YellHearingRadiusMillimetres >= YellRadiusMillimetres && FireHearingRadiusMillimetres >= 0 &&
                    BumpSoundRadiusMillimetres >= 0 &&
                    Range(InvestigateMinimumTicks, InvestigateMaximumTicks, 1), "hearing");
            Require(BumpMinimumSpeed > 0 && KnockdownClosingSpeed >= BumpMinimumSpeed &&
                    Range(StaggerMinimumTicks, StaggerMaximumTicks, 1) &&
                    Range(KnockdownMinimumTicks, KnockdownMaximumTicks, 1) && GetUpTicks >= 1, "collisions");
            Require(TripChancePercent >= 0 && TripChancePercent <= 50 && TripMinimumSpeed >= 0 &&
                    Range(TripMinimumTicks, TripMaximumTicks, 1), "tripping");
            Require(DoorwayDepthMillimetres >= OccupancyRadiusMillimetres * 2 && DoorwayDepthMillimetres <= 5000 &&
                    EscapeDepthMillimetres > OccupancyRadiusMillimetres &&
                    EscapeDepthMillimetres <= DoorwayDepthMillimetres - OccupancyRadiusMillimetres, "doorway depth");
            Require(DoorOpenTicks >= 1 && DoorTryTicks >= 1 &&
                    DoorForceChancePercent >= 0 && DoorForceChancePercent <= 100 &&
                    Range(DoorForceMinimumTicks, DoorForceMaximumTicks, 1) &&
                    Range(DoorShoveMinimumTicks, DoorShoveMaximumTicks, 1) &&
                    Range(DoorAvoidMinimumTicks, DoorAvoidMaximumTicks, 1) &&
                    Range(DoorCrowdedAvoidMinimumTicks, DoorCrowdedAvoidMaximumTicks, 1), "door timings");
            Require(AgentMassGrams > 0 && ObjectFriction >= 0 &&
                    ObjectAgentRestitutionPercent >= 0 && ObjectAgentRestitutionPercent <= 100 &&
                    ObjectObjectRestitutionPercent >= 0 && ObjectObjectRestitutionPercent <= 100 &&
                    ObjectWallRestitutionPercent >= 0 && ObjectWallRestitutionPercent <= 100, "object physics");
            Require(ObjectTripMinimumSpeed >= 0 && ObjectTripScale > 0 &&
                    ObjectTripMaximumChancePercent >= 0 && ObjectTripMaximumChancePercent <= 100 &&
                    ObjectStaggerMomentum > 0 && ObjectKnockdownMomentum >= ObjectStaggerMomentum, "object hits");

            int spawnMargin = FireCellSizeMillimetres / 2;
            if (!RoomBounds.ContainsCircle(new LogicalPosition(FireSpawnBounds.MinX, FireSpawnBounds.MinZ), spawnMargin) ||
                !RoomBounds.ContainsCircle(new LogicalPosition(FireSpawnBounds.MaxX, FireSpawnBounds.MaxZ), spawnMargin))
            {
                throw new InvalidOperationException("The fire spawn rectangle must remain inside the room.");
            }

            if (Agents == null || Agents.Length == 0)
            {
                throw new InvalidOperationException("A fire-reaction scenario needs at least one agent.");
            }

            var ids = new HashSet<StableAgentId> { new StableAgentId(FireReactionSimulation.FireHazardIdValue) };
            long touchingDistance = (long)OccupancyRadiusMillimetres * 2L;
            for (int i = 0; i < Agents.Length; i++)
            {
                FireReactionAgentDefinition agent = Agents[i];
                if (agent.AgentId.Value == 0UL || !ids.Add(agent.AgentId))
                {
                    throw new InvalidOperationException("Agent IDs must be unique and non-zero.");
                }

                if (!RoomBounds.ContainsCircle(agent.InitialPosition, OccupancyRadiusMillimetres))
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
            ValidatePhysicsObjects(ids);
        }

        private void ValidateDoors(HashSet<StableAgentId> ids)
        {
            Doors ??= Array.Empty<FireReactionDoorDefinition>();
            for (int i = 0; i < Doors.Length; i++)
            {
                FireReactionDoorDefinition door = Doors[i];
                if (door.DoorId.Value == 0UL || !ids.Add(door.DoorId))
                {
                    throw new InvalidOperationException("Door IDs must be unique and non-zero.");
                }

                // Wide enough for one person, with a solid bit of wall either side.
                bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
                int wallMin = alongX ? RoomBounds.MinX : RoomBounds.MinZ;
                int wallMax = alongX ? RoomBounds.MaxX : RoomBounds.MaxZ;
                int half = door.WidthMillimetres / 2;
                if (door.WidthMillimetres < OccupancyRadiusMillimetres * 2 + 100 ||
                    door.CentreAlongWallMillimetres - half < wallMin + OccupancyRadiusMillimetres ||
                    door.CentreAlongWallMillimetres + half > wallMax - OccupancyRadiusMillimetres)
                {
                    throw new InvalidOperationException($"Door {door.DoorId} does not fit in its wall.");
                }

                for (int previous = 0; previous < i; previous++)
                {
                    FireReactionDoorDefinition other = Doors[previous];
                    if (other.Side == door.Side &&
                        Math.Abs((long)other.CentreAlongWallMillimetres - door.CentreAlongWallMillimetres) <
                        (other.WidthMillimetres + door.WidthMillimetres) / 2 + OccupancyRadiusMillimetres * 2)
                    {
                        throw new InvalidOperationException("Doors in the same wall cannot overlap.");
                    }
                }
            }
        }

        private void ValidatePhysicsObjects(HashSet<StableAgentId> ids)
        {
            PhysicsObjects ??= Array.Empty<FireReactionPhysicsObjectDefinition>();
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

                if (!RoomBounds.ContainsCircle(body.InitialPosition, body.RadiusMillimetres))
                {
                    throw new InvalidOperationException($"Object {body.ObjectId} starts outside the room.");
                }

                long agentReach = OccupancyRadiusMillimetres + (long)body.RadiusMillimetres;
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

        private static bool Range(int minimum, int maximum, int floor) => minimum >= floor && maximum >= minimum;

        private static void Require(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Fire-reaction scenario values are invalid: {what}.");
            }
        }
    }
}
