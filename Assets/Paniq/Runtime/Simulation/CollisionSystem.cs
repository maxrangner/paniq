using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// People running into people. A panicking runner whose way is blocked
    /// by a person and who is going too fast to dodge runs into them; a hard
    /// enough hit puts both on the floor, a lighter one makes both stagger.
    /// Running into someone lying on the floor trips you over them. Nobody
    /// ever overlaps: a collision is a move that did not happen, recorded
    /// during decisions and resolved after movement in the order recorded.
    /// </summary>
    internal sealed class CollisionSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly BodySystem body;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly List<BumpIntent> bumps = new List<BumpIntent>();

        public CollisionSystem(SimulationContext context, Crowd crowd, BodySystem body, FearSystem fear, SoundSystem sound)
        {
            this.context = context;
            this.crowd = crowd;
            this.body = body;
            this.fear = fear;
            this.sound = sound;
        }

        public void BeginTick()
        {
            bumps.Clear();
        }

        /// <summary>
        /// Records a collision when a fleeing, upright runner's straight step
        /// is blocked by a person. Someone upright is hit before any dodge is
        /// tried if the runner is too fast; someone on the floor is only
        /// tripped over (<paramref name="overFallen"/>) once every side-step
        /// has failed.
        /// </summary>
        public bool TryRecordBump(Agent mover, LogicalPosition straight, bool overFallen)
        {
            if (mover.Fear.State != AgentFearState.Scared ||
                mover.Intent.Activity != AgentActivityState.Fleeing ||
                mover.Body.State != AgentBodyState.Upright)
            {
                return false;
            }

            Agent other = crowd.FindBlocking(mover, mover.Body.Position, mover.Body.Position + straight);
            if (other == null || other.IsDown != overFallen)
            {
                return false;
            }

            FallSettings settings = context.Scenario.Falls;
            if (overFallen)
            {
                if (mover.Body.Speed < settings.TripMinimumSpeed)
                {
                    return false;
                }

                bumps.Add(new BumpIntent(mover, other, 0, true));
                return true;
            }

            int closing = ClosingSpeed(mover, other);
            if (closing < TraitEffects.BumpMinimumSpeed(mover, context.Scenario))
            {
                return false;
            }

            bumps.Add(new BumpIntent(mover, other, closing, false));
            return true;
        }

        /// <summary>
        /// How fast two people approach each other along the line between
        /// them, in millimetres per tick. Head-on runners add their speeds;
        /// one catching another from behind only counts the difference.
        /// </summary>
        private static int ClosingSpeed(Agent mover, Agent other)
        {
            long nx = (long)other.Body.Position.X - mover.Body.Position.X;
            long nz = (long)other.Body.Position.Z - mover.Body.Position.Z;
            long distance = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (distance == 0L)
            {
                return mover.Body.Speed;
            }

            LogicalPosition moverDirection = IntegerMath.Direction(mover.Body.Heading);
            LogicalPosition otherDirection = IntegerMath.Direction(other.Body.Heading);
            long moverAlong = mover.Body.Speed * (moverDirection.X * nx + moverDirection.Z * nz);
            long otherAlong = other.Body.Speed * (otherDirection.X * nx + otherDirection.Z * nz);
            return (int)((moverAlong - otherAlong) / (IntegerMath.TrigScale * distance));
        }

        /// <summary>Phase 7, people into people, in the order the bumps were recorded (ascending mover ID).</summary>
        public void Resolve()
        {
            FallSettings settings = context.Scenario.Falls;
            for (int b = 0; b < bumps.Count; b++)
            {
                BumpIntent bump = bumps[b];
                Agent mover = bump.Mover;
                Agent other = bump.Other;
                if (!mover.IsParticipating || !other.IsParticipating || mover.Body.State != AgentBodyState.Upright)
                {
                    continue;
                }

                if (bump.TripOver)
                {
                    if (other.IsDown)
                    {
                        body.Trip(mover, other.Body.EventId);
                    }

                    continue;
                }

                if (other.Body.State != AgentBodyState.Upright)
                {
                    continue;
                }

                var midpoint = new LogicalPosition(
                    (int)(((long)mover.Body.Position.X + other.Body.Position.X) / 2),
                    (int)(((long)mover.Body.Position.Z + other.Body.Position.Z) / 2));
                CausalEvent collision = context.Events.Append(
                    context.Tick,
                    mover.Id,
                    FireReactionEventType.AgentsCollided,
                    midpoint,
                    bump.ClosingSpeed,
                    0,
                    mover.Fear.ScaredEventId,
                    other.Id);

                if (bump.ClosingSpeed >= settings.KnockdownClosingSpeed)
                {
                    // A much stronger person only reels from a hit that floors the other.
                    KnockDownOrStagger(mover, other, collision.EventId, bump.ClosingSpeed);
                    KnockDownOrStagger(other, mover, collision.EventId, bump.ClosingSpeed);
                }
                else
                {
                    body.Stagger(mover, collision.EventId);
                    body.Stagger(other, collision.EventId);
                }

                if (other.Fear.State == AgentFearState.Calm)
                {
                    fear.Alarm(other, collision.EventId, AgentAlertSource.Bumped, mover.Body.Position);
                }

                sound.Thud(mover.Id, midpoint, collision.EventId);
            }
        }

        private void KnockDownOrStagger(Agent agent, Agent hitBy, ulong collisionEventId, int closingSpeed)
        {
            if (TraitEffects.ShrugsOff(agent, hitBy, context.Scenario))
            {
                body.Stagger(agent, collisionEventId);
            }
            else
            {
                body.KnockDown(agent, collisionEventId, closingSpeed);
            }
        }

        private readonly struct BumpIntent
        {
            public BumpIntent(Agent mover, Agent other, int closingSpeed, bool tripOver)
            {
                Mover = mover;
                Other = other;
                ClosingSpeed = closingSpeed;
                TripOver = tripOver;
            }

            public Agent Mover { get; }
            public Agent Other { get; }
            public int ClosingSpeed { get; }
            public bool TripOver { get; }
        }
    }
}
