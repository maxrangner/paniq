using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Collisions and falls. A panicking runner whose way is blocked by a
    /// person and who is going too fast to dodge runs into them. A hard
    /// enough hit puts both on the floor; a lighter one makes both stagger.
    /// Running into someone lying on the floor trips you over them. Nobody
    /// ever overlaps: a collision is a move that did not happen.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int StaggerJoltMinimumDegrees = 25;
        private const int StaggerJoltMaximumDegrees = 50;

        private readonly List<BumpIntent> bumps = new List<BumpIntent>();

        /// <summary>
        /// Records a collision when a fleeing, upright runner's straight step
        /// is blocked by a person. Someone upright is hit before any dodge is
        /// tried if the runner is too fast; someone on the floor is only
        /// tripped over (<paramref name="overFallen"/>) once every side-step
        /// has failed.
        /// </summary>
        private bool TryRecordBump(int agentIndex, AgentRuntime agent, LogicalPosition straight, bool overFallen)
        {
            if (agent.FearState != AgentFearState.Scared ||
                agent.Activity != AgentActivityState.Fleeing ||
                agent.BodyState != AgentBodyState.Upright)
            {
                return false;
            }

            int otherIndex = FindBlockingAgent(agentIndex, agent.Position, agent.Position + straight);
            if (otherIndex < 0)
            {
                return false;
            }

            AgentRuntime other = agents[otherIndex];
            if (IsDown(other) != overFallen)
            {
                return false;
            }

            if (overFallen)
            {
                if (agent.Speed < scenario.TripMinimumSpeed)
                {
                    return false;
                }

                bumps.Add(new BumpIntent(agentIndex, otherIndex, 0, true));
                return true;
            }

            int closing = ClosingSpeed(agent, other);
            if (closing < scenario.BumpMinimumSpeed)
            {
                return false;
            }

            bumps.Add(new BumpIntent(agentIndex, otherIndex, closing, false));
            return true;
        }

        /// <summary>
        /// How fast two people approach each other along the line between
        /// them, in millimetres per tick. Head-on runners add their speeds;
        /// one catching another from behind only counts the difference.
        /// </summary>
        private static int ClosingSpeed(AgentRuntime mover, AgentRuntime other)
        {
            long nx = (long)other.Position.X - mover.Position.X;
            long nz = (long)other.Position.Z - mover.Position.Z;
            long distance = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (distance == 0L)
            {
                return mover.Speed;
            }

            LogicalPosition moverDirection = IntegerMath.Direction(mover.Heading);
            LogicalPosition otherDirection = IntegerMath.Direction(other.Heading);
            long moverAlong = mover.Speed * (moverDirection.X * nx + moverDirection.Z * nz);
            long otherAlong = other.Speed * (otherDirection.X * nx + otherDirection.Z * nz);
            return (int)((moverAlong - otherAlong) / (IntegerMath.TrigScale * distance));
        }

        /// <summary>
        /// Collisions resolve in the order they were recorded (ascending mover
        /// ID): people into people first, then people into objects.
        /// </summary>
        private void ResolveCollisions()
        {
            ResolvePersonCollisions();
            ResolveObjectContacts();
        }

        private void ResolvePersonCollisions()
        {
            for (int b = 0; b < bumps.Count; b++)
            {
                BumpIntent bump = bumps[b];
                AgentRuntime mover = agents[bump.MoverIndex];
                AgentRuntime other = agents[bump.OtherIndex];
                if (mover.Participation != AgentParticipation.Participating ||
                    other.Participation != AgentParticipation.Participating ||
                    mover.BodyState != AgentBodyState.Upright)
                {
                    continue;
                }

                if (bump.TripOver)
                {
                    if (IsDown(other))
                    {
                        Trip(mover, other.BodyEventId);
                    }

                    continue;
                }

                if (other.BodyState != AgentBodyState.Upright)
                {
                    continue;
                }

                var midpoint = new LogicalPosition(
                    (int)(((long)mover.Position.X + other.Position.X) / 2),
                    (int)(((long)mover.Position.Z + other.Position.Z) / 2));
                CausalEvent collision = eventLog.Append(
                    tick,
                    mover.Id,
                    FireReactionEventType.AgentsCollided,
                    midpoint,
                    bump.ClosingSpeed,
                    0,
                    mover.ScaredEventId);

                if (bump.ClosingSpeed >= scenario.KnockdownClosingSpeed)
                {
                    KnockDown(mover, collision.EventId);
                    KnockDown(other, collision.EventId);
                }
                else
                {
                    Stagger(mover, collision.EventId);
                    Stagger(other, collision.EventId);
                }

                if (other.FearState == AgentFearState.Calm)
                {
                    StartAlert(other, collision.EventId, AgentAlertSource.Bumped);
                    other.SoundPoint = mover.Position;
                    other.HasSoundPoint = true;
                }

                EmitSound(mover.Id, midpoint, scenario.BumpSoundRadiusMillimetres, 0, collision.EventId);
            }
        }

        private void KnockDown(AgentRuntime agent, ulong collisionEventId)
        {
            int duration = random.NextIntInclusive(scenario.KnockdownMinimumTicks, scenario.KnockdownMaximumTicks);
            CausalEvent down = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentKnockedDown,
                agent.Position,
                0,
                duration,
                collisionEventId);
            PutDown(agent, AgentBodyState.Fallen, duration, down.EventId);
        }

        private void Stagger(AgentRuntime agent, ulong collisionEventId)
        {
            int duration = random.NextIntInclusive(scenario.StaggerMinimumTicks, scenario.StaggerMaximumTicks);
            int side = random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
            agent.Heading = IntegerMath.NormalizeDegrees(agent.Heading +
                side * random.NextIntInclusive(StaggerJoltMinimumDegrees, StaggerJoltMaximumDegrees));
            PutDown(agent, AgentBodyState.Staggering, duration, collisionEventId);
        }

        /// <summary>A stumble: on your own, or over someone already on the floor. It makes a thud.</summary>
        private void Trip(AgentRuntime agent, ulong causalParentEventId)
        {
            int duration = random.NextIntInclusive(scenario.TripMinimumTicks, scenario.TripMaximumTicks);
            CausalEvent trip = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentTripped,
                agent.Position,
                scenario.BumpSoundRadiusMillimetres,
                duration,
                causalParentEventId);
            PutDown(agent, AgentBodyState.Fallen, duration, trip.EventId);
            EmitSound(agent.Id, agent.Position, scenario.BumpSoundRadiusMillimetres, 0, trip.EventId);
        }

        private void PutDown(AgentRuntime agent, AgentBodyState state, int duration, ulong eventId)
        {
            agent.BodyState = state;
            agent.BodyEndTick = checked(tick + duration);
            agent.BodyEventId = eventId;
            agent.Speed = 0;
            agent.BlockedTicks = 0;
        }

        /// <summary>
        /// Advances staggering, lying down and getting up. Returns true while
        /// the body is not under the person's control this tick.
        /// </summary>
        private bool UpdateBody(AgentRuntime agent)
        {
            if (agent.BodyState == AgentBodyState.Upright)
            {
                return false;
            }

            agent.Speed = 0;
            if (tick < agent.BodyEndTick)
            {
                return true;
            }

            if (agent.BodyState == AgentBodyState.Fallen)
            {
                agent.BodyState = AgentBodyState.GettingUp;
                agent.BodyEndTick = checked(tick + scenario.GetUpTicks);
                return true;
            }

            if (agent.BodyState == AgentBodyState.GettingUp)
            {
                eventLog.Append(tick, agent.Id, FireReactionEventType.AgentGotUp, agent.Position, 0, 0, agent.BodyEventId);
            }

            agent.BodyState = AgentBodyState.Upright;
            if (agent.FearState == AgentFearState.Scared && agent.Activity != AgentActivityState.Frozen)
            {
                // Back on your feet: look for a way out afresh.
                agent.Activity = AgentActivityState.Fleeing;
                agent.NextPanicDecisionTick = tick;
            }

            return false;
        }

        private static bool IsDown(AgentRuntime agent)
        {
            return agent.BodyState == AgentBodyState.Fallen || agent.BodyState == AgentBodyState.GettingUp;
        }

        private readonly struct BumpIntent
        {
            public BumpIntent(int moverIndex, int otherIndex, int closingSpeed, bool tripOver)
            {
                MoverIndex = moverIndex;
                OtherIndex = otherIndex;
                ClosingSpeed = closingSpeed;
                TripOver = tripOver;
            }

            public int MoverIndex { get; }
            public int OtherIndex { get; }
            public int ClosingSpeed { get; }
            public bool TripOver { get; }
        }
    }
}
