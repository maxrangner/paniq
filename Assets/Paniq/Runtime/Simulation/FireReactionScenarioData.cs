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

        public FireReactionAgentDefinition[] Agents { get; set; }

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

            var ids = new HashSet<StableAgentId>();
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
