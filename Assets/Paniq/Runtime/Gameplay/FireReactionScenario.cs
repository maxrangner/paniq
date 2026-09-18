using System;
using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Read-only authored input for the fire-reaction prototype. A run copies
    /// these values into FireReactionScenarioData and never writes this asset.
    /// Speeds are millimetres per tick, turn rates degrees per tick, and
    /// durations ticks; the simulation runs 50 ticks per second.
    /// </summary>
    [CreateAssetMenu(fileName = "FireReactionScenario", menuName = "Paniq/Fire Reaction Scenario")]
    public sealed class FireReactionScenario : ScriptableObject
    {
        [Header("Replay identity")]
        [SerializeField] private string scenarioId = "fire-reaction-prototype";
        [SerializeField] private string contentRevision = "12";
        [SerializeField] private ulong defaultSeed = 42UL;
        [SerializeField] private int simulationCompatibilityVersion = 4;

        [Header("World")]
        [SerializeField] private LogicalBounds roomBounds = new LogicalBounds(-6000, 6000, -6000, 6000);
        [SerializeField] private int occupancyRadiusMillimetres = 250;
        [SerializeField] private int maximumStepDistanceMillimetres = 120;

        [Header("Perception")]
        [SerializeField] private int visionRangeMillimetres = 3000;
        [SerializeField] private int yellRadiusMillimetres = 2500;
        [SerializeField] private int maximumReactionDelayTicks = 20;

        [Header("Fire")]
        [SerializeField] private LogicalBounds fireSpawnBounds = new LogicalBounds(-2000, 2000, -2000, 2000);
        [SerializeField] private int fireActivationTick = 250;
        [SerializeField] private int fireCellSizeMillimetres = 500;
        [SerializeField] private int fireSpreadMinimumTicks = 40;
        [SerializeField] private int fireSpreadMaximumTicks = 120;

        [Header("Steering")]
        [SerializeField] private int personalSpaceMillimetres = 800;
        [SerializeField] private int wallAvoidDistanceMillimetres = 600;

        [Header("Calm")]
        [SerializeField] private int calmSpeedMinimum = 22;
        [SerializeField] private int calmSpeedMaximum = 30;
        [SerializeField] private int calmTurnRateMinimum = 4;
        [SerializeField] private int calmTurnRateMaximum = 7;
        [SerializeField] private int calmAcceleration = 2;
        [SerializeField] private int calmDecisionMinimumTicks = 60;
        [SerializeField] private int calmDecisionMaximumTicks = 200;

        [Header("Panic")]
        [SerializeField] private int panicSpeedMinimum = 70;
        [SerializeField] private int panicSpeedMaximum = 100;
        [SerializeField] private int panicTurnRateMinimum = 10;
        [SerializeField] private int panicTurnRateMaximum = 16;
        [SerializeField] private int panicAcceleration = 8;
        [SerializeField] private int panicDecisionMinimumTicks = 20;
        [SerializeField] private int panicDecisionMaximumTicks = 60;
        [SerializeField] private int swerveChancePercent = 35;
        [SerializeField] private int swerveAngleMinimum = 30;
        [SerializeField] private int swerveAngleMaximum = 70;
        [SerializeField] private int swerveMinimumTicks = 10;
        [SerializeField] private int swerveMaximumTicks = 25;
        [SerializeField] private int hesitateChancePercent = 12;
        [SerializeField] private int hesitateMinimumTicks = 5;
        [SerializeField] private int hesitateMaximumTicks = 15;
        [SerializeField] private int escapeSampleCount = 8;
        [SerializeField] private int dangerDistanceMillimetres = 1500;
        [SerializeField] private int followRadiusMillimetres = 2500;
        [SerializeField] private int panicShoutMinimumTicks = 100;
        [SerializeField] private int panicShoutMaximumTicks = 250;

        [Header("Temperament")]
        [SerializeField] private int freezeThenRunPercent = 30;
        [SerializeField] private int freezeForeverPercent = 15;
        [SerializeField] private int freezeMinimumTicks = 100;
        [SerializeField] private int freezeMaximumTicks = 300;

        [Header("Sound")]
        [SerializeField] private int yellHearingRadiusMillimetres = 6000;
        [SerializeField] private int fireHearingRadiusMillimetres = 3500;
        [SerializeField] private int bumpSoundRadiusMillimetres = 3000;
        [SerializeField] private int investigateMinimumTicks = 50;
        [SerializeField] private int investigateMaximumTicks = 125;

        [Header("Collisions and falls")]
        [SerializeField] private int bumpMinimumSpeed = 50;
        [SerializeField] private int knockdownClosingSpeed = 100;
        [SerializeField] private int staggerMinimumTicks = 10;
        [SerializeField] private int staggerMaximumTicks = 20;
        [SerializeField] private int knockdownMinimumTicks = 60;
        [SerializeField] private int knockdownMaximumTicks = 140;
        [SerializeField] private int getUpTicks = 25;
        [SerializeField] private int tripChancePercent = 4;
        [SerializeField] private int tripMinimumSpeed = 60;
        [SerializeField] private int tripMinimumTicks = 40;
        [SerializeField] private int tripMaximumTicks = 100;

        [Header("Doors")]
        [SerializeField] private int doorwayDepthMillimetres = 2000;
        [SerializeField] private int escapeDepthMillimetres = 800;
        [SerializeField] private int doorOpenTicks = 20;
        [SerializeField] private int doorTryTicks = 25;
        [SerializeField] private int doorForceChancePercent = 60;
        [SerializeField] private int doorForceMinimumTicks = 75;
        [SerializeField] private int doorForceMaximumTicks = 200;
        [SerializeField] private int doorShoveMinimumTicks = 20;
        [SerializeField] private int doorShoveMaximumTicks = 30;
        [SerializeField] private int doorAvoidMinimumTicks = 300;
        [SerializeField] private int doorAvoidMaximumTicks = 600;
        [SerializeField] private int doorCrowdedAvoidMinimumTicks = 100;
        [SerializeField] private int doorCrowdedAvoidMaximumTicks = 200;

        [Header("Physical objects")]
        [SerializeField] private int agentMassGrams = 70000;
        [SerializeField] private int objectFriction = 157;
        [SerializeField] private int objectAgentRestitutionPercent = 30;
        [SerializeField] private int objectObjectRestitutionPercent = 40;
        [SerializeField] private int objectWallRestitutionPercent = 30;
        [SerializeField] private int objectTripMinimumSpeed = 60;
        [SerializeField] private int objectTripScale = 500;
        [SerializeField] private int objectTripMaximumChancePercent = 75;
        [SerializeField] private int objectStaggerMomentum = 800;
        [SerializeField] private int objectKnockdownMomentum = 1600;

        [Header("People")]
        [SerializeField] private FireReactionAgentDefinition[] agents = Array.Empty<FireReactionAgentDefinition>();

        [Header("Room contents")]
        [SerializeField] private FireReactionDoorDefinition[] doors = DefaultDoors();
        [SerializeField] private FireReactionPhysicsObjectDefinition[] physicsObjects = DefaultPhysicsObjects();

        public string ScenarioId => scenarioId;
        public string ContentRevision => contentRevision;
        public ulong DefaultSeed => defaultSeed;
        public int SimulationCompatibilityVersion => simulationCompatibilityVersion;
        public LogicalBounds RoomBounds => roomBounds;
        public int OccupancyRadiusMillimetres => occupancyRadiusMillimetres;
        public int MaximumStepDistanceMillimetres => maximumStepDistanceMillimetres;
        public int VisionRangeMillimetres => visionRangeMillimetres;
        public int YellRadiusMillimetres => yellRadiusMillimetres;
        public int MaximumReactionDelayTicks => maximumReactionDelayTicks;
        public int FireActivationTick => fireActivationTick;
        public int FireCellSizeMillimetres => fireCellSizeMillimetres;
        public int BumpSoundRadiusMillimetres => bumpSoundRadiusMillimetres;
        public int GetUpTicks => getUpTicks;
        public int BumpMinimumSpeed => bumpMinimumSpeed;
        public int DoorwayDepthMillimetres => doorwayDepthMillimetres;
        public IReadOnlyList<FireReactionAgentDefinition> Agents => agents;
        public IReadOnlyList<FireReactionDoorDefinition> Doors => doors;
        public IReadOnlyList<FireReactionPhysicsObjectDefinition> PhysicsObjects => physicsObjects;

        public FireReactionScenarioData ToRuntimeData()
        {
            var copy = agents == null ? Array.Empty<FireReactionAgentDefinition>() : (FireReactionAgentDefinition[])agents.Clone();
            Array.Sort(copy, (left, right) => left.AgentId.CompareTo(right.AgentId));
            return new FireReactionScenarioData
            {
                ScenarioId = scenarioId,
                ContentRevision = contentRevision,
                DefaultSeed = defaultSeed,
                SimulationCompatibilityVersion = simulationCompatibilityVersion,
                RoomBounds = roomBounds,
                OccupancyRadiusMillimetres = occupancyRadiusMillimetres,
                MaximumStepDistanceMillimetres = maximumStepDistanceMillimetres,
                VisionRangeMillimetres = visionRangeMillimetres,
                YellRadiusMillimetres = yellRadiusMillimetres,
                MaximumReactionDelayTicks = maximumReactionDelayTicks,
                FireSpawnBounds = fireSpawnBounds,
                FireActivationTick = fireActivationTick,
                FireCellSizeMillimetres = fireCellSizeMillimetres,
                FireSpreadMinimumTicks = fireSpreadMinimumTicks,
                FireSpreadMaximumTicks = fireSpreadMaximumTicks,
                PersonalSpaceMillimetres = personalSpaceMillimetres,
                WallAvoidDistanceMillimetres = wallAvoidDistanceMillimetres,
                CalmSpeedMinimum = calmSpeedMinimum,
                CalmSpeedMaximum = calmSpeedMaximum,
                CalmTurnRateMinimum = calmTurnRateMinimum,
                CalmTurnRateMaximum = calmTurnRateMaximum,
                CalmAcceleration = calmAcceleration,
                CalmDecisionMinimumTicks = calmDecisionMinimumTicks,
                CalmDecisionMaximumTicks = calmDecisionMaximumTicks,
                PanicSpeedMinimum = panicSpeedMinimum,
                PanicSpeedMaximum = panicSpeedMaximum,
                PanicTurnRateMinimum = panicTurnRateMinimum,
                PanicTurnRateMaximum = panicTurnRateMaximum,
                PanicAcceleration = panicAcceleration,
                PanicDecisionMinimumTicks = panicDecisionMinimumTicks,
                PanicDecisionMaximumTicks = panicDecisionMaximumTicks,
                SwerveChancePercent = swerveChancePercent,
                SwerveAngleMinimum = swerveAngleMinimum,
                SwerveAngleMaximum = swerveAngleMaximum,
                SwerveMinimumTicks = swerveMinimumTicks,
                SwerveMaximumTicks = swerveMaximumTicks,
                HesitateChancePercent = hesitateChancePercent,
                HesitateMinimumTicks = hesitateMinimumTicks,
                HesitateMaximumTicks = hesitateMaximumTicks,
                EscapeSampleCount = escapeSampleCount,
                DangerDistanceMillimetres = dangerDistanceMillimetres,
                FollowRadiusMillimetres = followRadiusMillimetres,
                PanicShoutMinimumTicks = panicShoutMinimumTicks,
                PanicShoutMaximumTicks = panicShoutMaximumTicks,
                FreezeThenRunPercent = freezeThenRunPercent,
                FreezeForeverPercent = freezeForeverPercent,
                FreezeMinimumTicks = freezeMinimumTicks,
                FreezeMaximumTicks = freezeMaximumTicks,
                YellHearingRadiusMillimetres = yellHearingRadiusMillimetres,
                FireHearingRadiusMillimetres = fireHearingRadiusMillimetres,
                BumpSoundRadiusMillimetres = bumpSoundRadiusMillimetres,
                InvestigateMinimumTicks = investigateMinimumTicks,
                InvestigateMaximumTicks = investigateMaximumTicks,
                BumpMinimumSpeed = bumpMinimumSpeed,
                KnockdownClosingSpeed = knockdownClosingSpeed,
                StaggerMinimumTicks = staggerMinimumTicks,
                StaggerMaximumTicks = staggerMaximumTicks,
                KnockdownMinimumTicks = knockdownMinimumTicks,
                KnockdownMaximumTicks = knockdownMaximumTicks,
                GetUpTicks = getUpTicks,
                TripChancePercent = tripChancePercent,
                TripMinimumSpeed = tripMinimumSpeed,
                TripMinimumTicks = tripMinimumTicks,
                TripMaximumTicks = tripMaximumTicks,
                DoorwayDepthMillimetres = doorwayDepthMillimetres,
                EscapeDepthMillimetres = escapeDepthMillimetres,
                DoorOpenTicks = doorOpenTicks,
                DoorTryTicks = doorTryTicks,
                DoorForceChancePercent = doorForceChancePercent,
                DoorForceMinimumTicks = doorForceMinimumTicks,
                DoorForceMaximumTicks = doorForceMaximumTicks,
                DoorShoveMinimumTicks = doorShoveMinimumTicks,
                DoorShoveMaximumTicks = doorShoveMaximumTicks,
                DoorAvoidMinimumTicks = doorAvoidMinimumTicks,
                DoorAvoidMaximumTicks = doorAvoidMaximumTicks,
                DoorCrowdedAvoidMinimumTicks = doorCrowdedAvoidMinimumTicks,
                DoorCrowdedAvoidMaximumTicks = doorCrowdedAvoidMaximumTicks,
                AgentMassGrams = agentMassGrams,
                ObjectFriction = objectFriction,
                ObjectAgentRestitutionPercent = objectAgentRestitutionPercent,
                ObjectObjectRestitutionPercent = objectObjectRestitutionPercent,
                ObjectWallRestitutionPercent = objectWallRestitutionPercent,
                ObjectTripMinimumSpeed = objectTripMinimumSpeed,
                ObjectTripScale = objectTripScale,
                ObjectTripMaximumChancePercent = objectTripMaximumChancePercent,
                ObjectStaggerMomentum = objectStaggerMomentum,
                ObjectKnockdownMomentum = objectKnockdownMomentum,
                Agents = copy,
                Doors = doors == null ? Array.Empty<FireReactionDoorDefinition>() : (FireReactionDoorDefinition[])doors.Clone(),
                PhysicsObjects = physicsObjects == null
                    ? Array.Empty<FireReactionPhysicsObjectDefinition>()
                    : (FireReactionPhysicsObjectDefinition[])physicsObjects.Clone()
            };
        }

        public bool IsValid(out string error)
        {
            try
            {
                ToRuntimeData().Validate();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static FireReactionScenario CreateDefault()
        {
            var scenario = CreateInstance<FireReactionScenario>();
            scenario.agents = DefaultAgents();
            return scenario;
        }

        /// <summary>Ten people spread around the room, facing different ways.</summary>
        public static FireReactionAgentDefinition[] DefaultAgents()
        {
            return new[]
            {
                new FireReactionAgentDefinition(new StableAgentId(1001UL), new LogicalPosition(-5000, -5000), CardinalDirection.North),
                new FireReactionAgentDefinition(new StableAgentId(1002UL), new LogicalPosition(0, -5000), CardinalDirection.East),
                new FireReactionAgentDefinition(new StableAgentId(1003UL), new LogicalPosition(5000, -5000), CardinalDirection.West),
                new FireReactionAgentDefinition(new StableAgentId(1004UL), new LogicalPosition(-5000, 0), CardinalDirection.East),
                new FireReactionAgentDefinition(new StableAgentId(1005UL), new LogicalPosition(900, 0), CardinalDirection.South),
                new FireReactionAgentDefinition(new StableAgentId(1006UL), new LogicalPosition(5000, 0), CardinalDirection.North),
                new FireReactionAgentDefinition(new StableAgentId(1007UL), new LogicalPosition(-5000, 5000), CardinalDirection.South),
                new FireReactionAgentDefinition(new StableAgentId(1008UL), new LogicalPosition(0, 5000), CardinalDirection.West),
                new FireReactionAgentDefinition(new StableAgentId(1009UL), new LogicalPosition(5000, 5000), CardinalDirection.South),
                new FireReactionAgentDefinition(new StableAgentId(1010UL), new LogicalPosition(0, -1800), CardinalDirection.North)
            };
        }

        /// <summary>One 1 m door per wall, set off-centre in a pinwheel so each corner has a different nearest exit.</summary>
        public static FireReactionDoorDefinition[] DefaultDoors()
        {
            return new[]
            {
                new FireReactionDoorDefinition(new StableAgentId(2001UL), WallSide.North, -2500, 1000),
                new FireReactionDoorDefinition(new StableAgentId(2002UL), WallSide.East, 2500, 1000),
                new FireReactionDoorDefinition(new StableAgentId(2003UL), WallSide.South, 2500, 1000),
                new FireReactionDoorDefinition(new StableAgentId(2004UL), WallSide.West, -2500, 1000)
            };
        }

        /// <summary>Eight cardboard boxes, 0.3–0.6 m wide and 3–20 kg, set between where people stand.</summary>
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
                Box(3008UL, -4000, -1250, 400, 6000)
            };
        }

        private static FireReactionPhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams)
        {
            return new FireReactionPhysicsObjectDefinition(
                new StableAgentId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams);
        }
    }
}
