using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Moving bodies. People never snap: each tick they turn a limited
    /// number of degrees toward where they want to face and change speed by
    /// a limited amount, like a person rather than a chess piece. Behaviours
    /// only say what they want (<see cref="MotorIntent"/>); this is the one
    /// place that turns, accelerates and moves a body. All values are
    /// integers so replays stay exact.
    /// </summary>
    internal sealed class Locomotion
    {
        private const int SharpTurnDegrees = 75;
        /// <summary>
        /// The ways round something in the way, tried in turn. The last pair is a
        /// step straight sideways: somebody pressed against a wall beside a
        /// doorway, with the crowd in front of them, can only get out of it by
        /// sliding along the wall, and without this they stand there until
        /// whatever is in front of them happens to move.
        /// </summary>
        private static readonly int[] SideStepOffsets = { 30, -30, 60, -60, 90, -90 };

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly BodySystem body;
        private readonly CollisionSystem collisions;
        private readonly PhysicsObjectSystem objects;
        private readonly List<MovementRequest> requests = new List<MovementRequest>();

        public Locomotion(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            FireSystem fire,
            BodySystem body,
            CollisionSystem collisions,
            PhysicsObjectSystem objects)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.fire = fire;
            this.body = body;
            this.collisions = collisions;
            this.objects = objects;
        }

        public void BeginTick()
        {
            requests.Clear();
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
        /// with), and an optional extra direction (panic following). Weights
        /// are percentages of a unit goal vector.
        /// </summary>
        public int Steer(
            Agent agent,
            int goalHeading,
            int peopleAvoidPercent,
            int wallAvoidPercent,
            int objectAvoidPercent,
            long extraX = 0L,
            long extraZ = 0L)
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
                wallAvoidPercent, ref steerX, ref steerZ);

            return IntegerMath.HeadingOf(steerX, steerZ, goalHeading);
        }

        /// <summary>
        /// Phase 4, after the decision: turns heading and speed into this
        /// tick's movement request. The person chooses a valid step
        /// themselves: along a wall they keep the along-wall part of their
        /// step, and if a person or box blocks the way they try a small
        /// side-step. A runner too fast to dodge runs into a person or kicks a
        /// box instead; a walker with no way around a box pushes it. The
        /// movement resolver still has the final say.
        /// </summary>
        public void RequestMove(Agent agent)
        {
            LogicalPosition displacement = ChooseDisplacement(agent);
            if (!IsZero(displacement))
            {
                requests.Add(new MovementRequest(agent, displacement));
            }
        }

        private LogicalPosition ChooseDisplacement(Agent agent)
        {
            if (agent.Body.Speed <= 0)
            {
                return new LogicalPosition(0, 0);
            }

            LogicalPosition straight = StepAlong(agent, agent.Body.Heading);
            if (!IsZero(straight))
            {
                if (IsMovementValid(agent, agent.Body.Position, agent.Body.Position + straight))
                {
                    return straight;
                }

                // Too fast to dodge someone upright: run into them (resolved after movement).
                if (collisions.TryRecordBump(agent, straight, false) ||
                    objects.TryRecordContact(agent, straight, true))
                {
                    return new LogicalPosition(0, 0);
                }
            }

            // Alternate which side is tried first so a crowd does not all
            // dodge the same way.
            int flip = (agent.Index + context.Tick / FireReactionSimulation.TicksPerSecond) % 2 == 0 ? 1 : -1;
            for (int i = 0; i < SideStepOffsets.Length; i++)
            {
                LogicalPosition sideStep = StepAlong(agent, agent.Body.Heading + SideStepOffsets[i] * flip);
                if (!IsZero(sideStep) && IsMovementValid(agent, agent.Body.Position, agent.Body.Position + sideStep))
                {
                    return sideStep;
                }
            }

            if (IsZero(straight))
            {
                // Walking straight into a wall: no move to request, but the
                // person is stuck all the same and their decisions should know.
                agent.Body.Speed = 0;
                agent.Body.BlockedTicks++;
            }
            else if (collisions.TryRecordBump(agent, straight, true))
            {
                // No way around someone lying on the floor: trip over them.
                return new LogicalPosition(0, 0);
            }
            else if (objects.TryRecordContact(agent, straight, false))
            {
                // No way around a box: push it.
                return new LogicalPosition(0, 0);
            }

            return straight;
        }

        private LogicalPosition StepAlong(Agent agent, int heading)
        {
            LogicalPosition position = agent.Body.Position;
            LogicalPosition destination = geometry.ClampIntoWalkable(position, agent.DoorwayInUse,
                position + IntegerMath.Displacement(heading, agent.Body.Speed));
            return destination - position;
        }

        private static bool IsZero(LogicalPosition displacement) => displacement.X == 0 && displacement.Z == 0;

        /// <summary>
        /// Phase 5. Requests arrive in ascending ID order. Resolution starts
        /// at index tick mod N and visits that circular order once, so the
        /// first claim on contested space rotates between people. Phase 6's
        /// fire contact along each accepted move happens as it is accepted.
        /// </summary>
        public void ResolveMovement()
        {
            int count = requests.Count;
            if (count == 0)
            {
                return;
            }

            int start = context.Tick % count;
            for (int offset = 0; offset < count; offset++)
            {
                MovementRequest request = requests[(start + offset) % count];
                Agent agent = request.Agent;
                LogicalPosition startPosition = agent.Body.Position;
                LogicalPosition destination = startPosition + request.Displacement;
                if (!IsMovementValid(agent, startPosition, destination))
                {
                    agent.Body.Speed = 0;
                    agent.Body.BlockedTicks++;
                    continue;
                }

                crowd.MoveTo(agent, destination);
                agent.Body.BlockedTicks = 0;
                if (fire.Active)
                {
                    ulong cellEventId = fire.FindTouchingSweep(startPosition, destination);
                    if (cellEventId != 0UL)
                    {
                        body.CatchFire(agent, cellEventId);
                    }
                }
            }
        }

        private bool IsMovementValid(Agent agent, LogicalPosition start, LogicalPosition destination)
        {
            long maximumStep = context.Scenario.World.MaximumStepDistanceMillimetres;
            if (LogicalPosition.DistanceSquared(start, destination) > maximumStep * maximumStep ||
                !geometry.IsWalkable(agent.Body.Position, agent.DoorwayInUse, destination) ||
                geometry.ClipsDoorFrame(start, destination))
            {
                return false;
            }

            return crowd.FindBlocking(agent, start, destination) == null &&
                   objects.FindBlocking(start, destination, context.Scenario.World.OccupancyRadiusMillimetres,
                       agent.Sitting.OnIt ? agent.Sitting.ChairIndex : -1) < 0;
        }

        private readonly struct MovementRequest
        {
            public MovementRequest(Agent agent, LogicalPosition displacement)
            {
                Agent = agent;
                Displacement = displacement;
            }

            public Agent Agent { get; }
            public LogicalPosition Displacement { get; }
        }
    }
}
