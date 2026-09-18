using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Steering: agents never snap. Each tick they turn a limited number of
    /// degrees toward where they want to face and change speed by a limited
    /// amount, like a person rather than a chess piece. All values are
    /// integers so replays stay exact.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int SharpTurnDegrees = 75;
        private static readonly int[] SideStepOffsets = { 30, -30, 60, -60 };

        /// <summary>Turns and accelerates the body toward a goal heading and speed.</summary>
        private static void ApplyBody(AgentRuntime agent, int goalHeading, int goalSpeed, int turnRate, int acceleration)
        {
            int remainingTurn = Math.Abs(IntegerMath.SignedAngleDifference(agent.Heading, goalHeading));
            agent.Heading = IntegerMath.TurnToward(agent.Heading, goalHeading, turnRate);

            // People slow down to make a sharp turn, and brake faster than
            // they speed up.
            if (remainingTurn > SharpTurnDegrees)
            {
                goalSpeed /= 2;
            }

            agent.Speed = agent.Speed < goalSpeed
                ? Math.Min(goalSpeed, agent.Speed + acceleration)
                : Math.Max(goalSpeed, agent.Speed - acceleration * 2);
        }

        /// <summary>
        /// Blends the goal direction with a push away from people inside
        /// personal space, a push away from nearby walls, and an optional
        /// extra direction (panic following). Weights are percentages of a
        /// unit goal vector.
        /// </summary>
        private int SteerHeading(
            int agentIndex,
            AgentRuntime agent,
            int goalHeading,
            int peopleAvoidPercent,
            int wallAvoidPercent,
            long extraX = 0L,
            long extraZ = 0L)
        {
            LogicalPosition goal = IntegerMath.Direction(goalHeading);
            long steerX = goal.X + extraX;
            long steerZ = goal.Z + extraZ;

            long space = scenario.PersonalSpaceMillimetres;
            if (space > 0L && peopleAvoidPercent > 0)
            {
                for (int i = 0; i < agents.Length; i++)
                {
                    AgentRuntime other = agents[i];
                    if (i == agentIndex || other.Participation != AgentParticipation.Participating)
                    {
                        continue;
                    }

                    long dx = (long)agent.Position.X - other.Position.X;
                    long dz = (long)agent.Position.Z - other.Position.Z;
                    long distanceSquared = dx * dx + dz * dz;
                    if (distanceSquared == 0L || distanceSquared >= space * space)
                    {
                        continue;
                    }

                    long distance = IntegerMath.Sqrt(distanceSquared);
                    long strength = IntegerMath.TrigScale * (space - distance) / space * peopleAvoidPercent / 100L;
                    steerX += dx * strength / distance;
                    steerZ += dz * strength / distance;
                }
            }

            long wall = scenario.WallAvoidDistanceMillimetres;
            if (wall > 0L && wallAvoidPercent > 0)
            {
                LogicalBounds room = scenario.RoomBounds;
                int radius = scenario.OccupancyRadiusMillimetres;
                steerX += WallPush(agent.Position.X - radius - room.MinX, wall, wallAvoidPercent);
                steerX -= WallPush(room.MaxX - radius - agent.Position.X, wall, wallAvoidPercent);
                steerZ += WallPush(agent.Position.Z - radius - room.MinZ, wall, wallAvoidPercent);
                steerZ -= WallPush(room.MaxZ - radius - agent.Position.Z, wall, wallAvoidPercent);
            }

            return IntegerMath.HeadingOf(steerX, steerZ, goalHeading);
        }

        private static long WallPush(long gap, long range, int percent)
        {
            if (gap >= range)
            {
                return 0L;
            }

            return IntegerMath.TrigScale * (range - Math.Max(0L, gap)) / range * percent / 100L;
        }

        /// <summary>
        /// Converts heading and speed into this tick's requested
        /// displacement. The agent chooses a valid step itself: along a wall
        /// it keeps the along-wall part of its step, and if a person blocks
        /// the way it tries a small side-step. The movement resolver still
        /// has the final say.
        /// </summary>
        private LogicalPosition ChooseDisplacement(int agentIndex, AgentRuntime agent)
        {
            if (agent.Speed <= 0)
            {
                return new LogicalPosition(0, 0);
            }

            LogicalPosition straight = StepAlong(agent, agent.Heading);
            if (!IsZero(straight) && IsMovementValid(agentIndex, agent.Position, agent.Position + straight))
            {
                return straight;
            }

            // Alternate which side is tried first so a crowd does not all
            // dodge the same way.
            int flip = (agentIndex + tick / TicksPerSecond) % 2 == 0 ? 1 : -1;
            for (int i = 0; i < SideStepOffsets.Length; i++)
            {
                LogicalPosition sideStep = StepAlong(agent, agent.Heading + SideStepOffsets[i] * flip);
                if (!IsZero(sideStep) && IsMovementValid(agentIndex, agent.Position, agent.Position + sideStep))
                {
                    return sideStep;
                }
            }

            if (IsZero(straight))
            {
                // Walking straight into a wall: no move to request, but the
                // agent is stuck all the same and its decisions should know.
                agent.Speed = 0;
                agent.BlockedTicks++;
            }

            return straight;
        }

        private LogicalPosition StepAlong(AgentRuntime agent, int heading)
        {
            LogicalPosition destination = ClampIntoRoom(agent.Position + IntegerMath.Displacement(heading, agent.Speed));
            return destination - agent.Position;
        }

        private LogicalPosition ClampIntoRoom(LogicalPosition position)
        {
            LogicalBounds room = scenario.RoomBounds;
            int radius = scenario.OccupancyRadiusMillimetres;
            return new LogicalPosition(
                Math.Max(room.MinX + radius, Math.Min(room.MaxX - radius, position.X)),
                Math.Max(room.MinZ + radius, Math.Min(room.MaxZ - radius, position.Z)));
        }

        private static bool IsZero(LogicalPosition displacement) => displacement.X == 0 && displacement.Z == 0;

        private static int HeadingBetween(LogicalPosition from, LogicalPosition to, int fallback)
        {
            return IntegerMath.HeadingOf((long)to.X - from.X, (long)to.Z - from.Z, fallback);
        }

        private static long Distance(LogicalPosition from, LogicalPosition to)
        {
            return IntegerMath.Sqrt(LogicalPosition.DistanceSquared(from, to));
        }
    }
}
