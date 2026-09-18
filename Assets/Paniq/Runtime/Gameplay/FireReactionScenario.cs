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
        [SerializeField] private string contentRevision = "10";
        [SerializeField] private ulong defaultSeed = 42UL;
        [SerializeField] private int simulationCompatibilityVersion = 2;

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

        [Header("People")]
        [SerializeField] private FireReactionAgentDefinition[] agents = Array.Empty<FireReactionAgentDefinition>();

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
        public IReadOnlyList<FireReactionAgentDefinition> Agents => agents;

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
                Agents = copy
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
    }
}
