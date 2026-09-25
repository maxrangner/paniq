using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Where people want to go. People never snap: each tick they turn a
    /// limited number of degrees toward where they want to face and change the
    /// speed they are trying for by a limited amount, like a person rather than
    /// a chess piece. Behaviours only say what they want
    /// (<see cref="MotorIntent"/>); this is the one place that turns that into
    /// a heading and a speed. The body itself is moved by the physics engine,
    /// pushed by the person's own feet toward that heading and speed (see
    /// <see cref="PeopleBodies"/>). All values are integers so replays stay
    /// exact.
    /// </summary>
    internal sealed class Locomotion
    {
        private const int SharpTurnDegrees = 75;

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;

        public Locomotion(SimulationContext context, Crowd crowd, WorldGeometry geometry, PhysicsObjectSystem objects)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
        }

        /// <summary>Turns and accelerates the body toward what the behaviour wants.</summary>
        public static void ApplyBody(Agent agent, MotorIntent intent)
        {
            AgentBody body = agent.Body;
            int goalSpeed = intent.GoalSpeed;
            int remainingTurn = Math.Abs(IntegerMath.SignedAngleDifference(body.Heading, intent.GoalHeading));
            body.Heading = IntegerMath.TurnToward(body.Heading, intent.GoalHeading, intent.TurnRate);

            // People slow down to make a sharp turn, and brake faster than
            // they speed up.
            if (remainingTurn > SharpTurnDegrees)
            {
                goalSpeed /= 2;
            }

            body.Speed = body.Speed < goalSpeed
                ? Math.Min(goalSpeed, body.Speed + intent.Acceleration)
                : Math.Max(goalSpeed, body.Speed - intent.Acceleration * 2);
        }

        /// <summary>
        /// Blends the goal direction with a push away from people inside
        /// personal space, a push away from loose objects, a push away from
        /// nearby walls (except the wall of the door the person is lined up
        /// with) and table edges, and an optional extra direction (panic
        /// following). Weights are percentages of a unit goal vector. Tables
        /// push as hard as walls unless <paramref name="tableAvoidPercent"/>
        /// says otherwise.
        /// </summary>
        public int Steer(
            Agent agent,
            int goalHeading,
            int peopleAvoidPercent,
            int wallAvoidPercent,
            int objectAvoidPercent,
            long extraX = 0L,
            long extraZ = 0L,
            int tableAvoidPercent = -1)
        {
            SteeringSettings settings = context.Scenario.Steering;
            LogicalPosition position = agent.Body.Position;
            LogicalPosition goal = IntegerMath.Direction(goalHeading);
            long steerX = goal.X + extraX;
            long steerZ = goal.Z + extraZ;

            long space = settings.PersonalSpaceMillimetres;
            if (space > 0L && peopleAvoidPercent > 0)
            {
                // Only the people whose cell this reaches: anybody further off
                // than personal space contributes nothing to the sum anyway.
                using Crowd.Nearby neighbours = crowd.Within(position, space);
                for (int i = 0; i < neighbours.Count; i++)
                {
                    Agent other = crowd.All[neighbours[i]];
                    if (other == agent || !other.IsParticipating)
                    {
                        continue;
                    }

                    long dx = (long)position.X - other.Body.Position.X;
                    long dz = (long)position.Z - other.Body.Position.Z;
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

            if (objectAvoidPercent > 0)
            {
                objects.AddAvoidance(position, objectAvoidPercent, ref steerX, ref steerZ);
            }

            geometry.AddWallRepulsion(position, agent.DoorwayInUse, settings.WallAvoidDistanceMillimetres,
                wallAvoidPercent, tableAvoidPercent < 0 ? wallAvoidPercent : tableAvoidPercent, ref steerX, ref steerZ);

            return IntegerMath.HeadingOf(steerX, steerZ, goalHeading);
        }
    }
}
