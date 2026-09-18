using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Panic: sprint toward an escape spot chosen imperfectly, keep changing
    /// your mind, swerve, hesitate, drift with the people running near you,
    /// and bolt straight away if the fire gets close. Nobody stays pinned
    /// against a wall: being blocked forces a new decision.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int PanicBlockedGiveUpTicks = 12;
        private const int PanicArrivalDistance = 700;
        private const int EscapeWallMargin = 700;
        private const int EscapeRouteClearance = 1000;
        private const int EscapeRoutePenalty = 5000;
        private const int EscapeShortHopDistance = 1500;
        private const int EscapeShortHopPenalty = 2000;
        private const int EscapeTurnPenaltyPerDegree = 10;
        private const int EscapeNoise = 1500;
        private const int FollowPercent = 35;

        private void UpdatePanic(int agentIndex, AgentRuntime agent)
        {
            long fireDistanceSquared = NearestFireDistanceSquared(agent.Position, out LogicalPosition firePoint);
            long danger = scenario.DangerDistanceMillimetres;
            bool inDanger = fireDistanceSquared < danger * danger;

            if (agent.Activity == AgentActivityState.Hesitating)
            {
                if (tick < agent.ActivityEndTick && !inDanger)
                {
                    // Frozen for a split second, looking for a way out.
                    ApplyBody(agent, agent.LookHeading, 0, agent.PanicTurnRate, scenario.PanicAcceleration);
                    return;
                }

                agent.Activity = AgentActivityState.Fleeing;
                DecidePanicMove(agent, false);
            }
            else if (tick >= agent.NextPanicDecisionTick ||
                     agent.BlockedTicks >= PanicBlockedGiveUpTicks ||
                     LogicalPosition.DistanceSquared(agent.Position, agent.Target) <
                     (long)PanicArrivalDistance * PanicArrivalDistance)
            {
                DecidePanicMove(agent, !inDanger && agent.BlockedTicks < PanicBlockedGiveUpTicks);
            }

            if (agent.Activity == AgentActivityState.Hesitating)
            {
                ApplyBody(agent, agent.LookHeading, 0, agent.PanicTurnRate, scenario.PanicAcceleration);
                return;
            }

            int goalHeading;
            int swerve = tick < agent.SwerveEndTick ? agent.SwerveOffset : 0;
            if (inDanger && fireDistanceSquared > 0L)
            {
                // Too close: run directly away from the nearest flames.
                goalHeading = HeadingBetween(firePoint, agent.Position, agent.Heading) + swerve / 2;
            }
            else
            {
                goalHeading = HeadingBetween(agent.Position, agent.Target, agent.Heading) + swerve;
            }

            FollowNearbyRunners(agentIndex, agent, out long followX, out long followZ);
            goalHeading = SteerHeading(agentIndex, agent, goalHeading, 50, 200, followX, followZ);
            ApplyBody(agent, goalHeading, agent.PanicSpeed, agent.PanicTurnRate, scenario.PanicAcceleration);
        }

        /// <summary>A fresh panicked decision: maybe freeze, otherwise pick a new escape spot and maybe swerve.</summary>
        private void DecidePanicMove(AgentRuntime agent, bool mayHesitate)
        {
            agent.BlockedTicks = 0;
            agent.NextPanicDecisionTick = checked(tick + random.NextIntInclusive(
                scenario.PanicDecisionMinimumTicks,
                scenario.PanicDecisionMaximumTicks));

            if (mayHesitate && random.NextPercent(scenario.HesitateChancePercent))
            {
                agent.Activity = AgentActivityState.Hesitating;
                agent.ActivityEndTick = checked(tick + random.NextIntInclusive(
                    scenario.HesitateMinimumTicks,
                    scenario.HesitateMaximumTicks));
                int side = random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                agent.LookHeading = IntegerMath.NormalizeDegrees(agent.Heading + side * random.NextIntInclusive(40, 150));
                return;
            }

            agent.Activity = AgentActivityState.Fleeing;
            agent.Target = ChooseEscapeTarget(agent);
            if (random.NextPercent(scenario.SwerveChancePercent))
            {
                int side = random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                agent.SwerveOffset = side * random.NextIntInclusive(scenario.SwerveAngleMinimum, scenario.SwerveAngleMaximum);
                agent.SwerveEndTick = checked(tick + random.NextIntInclusive(
                    scenario.SwerveMinimumTicks,
                    scenario.SwerveMaximumTicks));
            }
        }

        /// <summary>
        /// Samples spots in the room and scores them: far from fire is good,
        /// a route that brushes past the fire is bad, a U-turn is a little
        /// bad, and random noise keeps the choice human and imperfect.
        /// </summary>
        private LogicalPosition ChooseEscapeTarget(AgentRuntime agent)
        {
            LogicalBounds room = scenario.RoomBounds;
            int margin = Math.Min(EscapeWallMargin, Math.Min(room.MaxX - room.MinX, room.MaxZ - room.MinZ) / 2);
            LogicalPosition best = agent.Position;
            long bestScore = long.MinValue;
            for (int sample = 0; sample < scenario.EscapeSampleCount; sample++)
            {
                var candidate = new LogicalPosition(
                    random.NextIntInclusive(room.MinX + margin, room.MaxX - margin),
                    random.NextIntInclusive(room.MinZ + margin, room.MaxZ - margin));
                long score = random.NextIntInclusive(0, EscapeNoise);

                long fireDistanceSquared = NearestFireDistanceSquared(candidate);
                score += fireDistanceSquared == long.MaxValue ? 20000L : IntegerMath.Sqrt(fireDistanceSquared);

                if (RoutePassesNearFire(agent.Position, candidate))
                {
                    score -= EscapeRoutePenalty;
                }

                long hopSquared = LogicalPosition.DistanceSquared(agent.Position, candidate);
                if (hopSquared < (long)EscapeShortHopDistance * EscapeShortHopDistance)
                {
                    score -= EscapeShortHopPenalty;
                }

                int turn = Math.Abs(IntegerMath.SignedAngleDifference(
                    agent.Heading,
                    HeadingBetween(agent.Position, candidate, agent.Heading)));
                score -= turn * EscapeTurnPenaltyPerDegree;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private bool RoutePassesNearFire(LogicalPosition from, LogicalPosition to)
        {
            long clearanceSquared = (long)EscapeRouteClearance * EscapeRouteClearance;
            for (int quarter = 1; quarter <= 3; quarter++)
            {
                var point = new LogicalPosition(
                    (int)(from.X + ((long)to.X - from.X) * quarter / 4),
                    (int)(from.Z + ((long)to.Z - from.Z) * quarter / 4));
                if (NearestFireDistanceSquared(point) < clearanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Average running direction of nearby panicking people, as a weighted pull.</summary>
        private void FollowNearbyRunners(int agentIndex, AgentRuntime agent, out long followX, out long followZ)
        {
            followX = 0L;
            followZ = 0L;
            long radius = scenario.FollowRadiusMillimetres;
            if (radius <= 0L)
            {
                return;
            }

            long radiusSquared = radius * radius;
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime other = agents[i];
                if (i == agentIndex ||
                    other.Participation != AgentParticipation.Participating ||
                    other.FearState != AgentFearState.Scared ||
                    other.Speed <= 0 ||
                    LogicalPosition.DistanceSquared(agent.Position, other.Position) > radiusSquared)
                {
                    continue;
                }

                LogicalPosition direction = IntegerMath.Direction(other.Heading);
                followX += direction.X;
                followZ += direction.Z;
                count++;
            }

            if (count > 0)
            {
                followX = followX / count * FollowPercent / 100L;
                followZ = followZ / count * FollowPercent / 100L;
            }
        }
    }
}
