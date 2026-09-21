using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// People running into people. The bodies meet in the physics engine,
    /// which does the pushing; this decides what each meeting means. A
    /// panicking runner who hits somebody hard enough puts both on the floor,
    /// a lighter hit makes both stagger. Running into someone lying on the
    /// floor trips you over them. A cruel person held up by somebody in their
    /// way takes hold and heaves them aside, which is the one deliberate shove
    /// in here. Judged from the engine's list of contacts after each step, in
    /// the sorted order it gives them.
    /// </summary>
    internal sealed class CollisionSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly BodySystem body;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly PeopleBodies people;

        public CollisionSystem(SimulationContext context, Crowd crowd, BodySystem body, FearSystem fear, SoundSystem sound,
            PeopleBodies people)
        {
            this.context = context;
            this.crowd = crowd;
            this.body = body;
            this.fear = fear;
            this.sound = sound;
            this.people = people;
        }

        /// <summary>
        /// Phase 8, after the engine has stepped: every pair of people who
        /// touched. A pair who have only just met are a collision, judged by how
        /// fast they came together; a pair who were already pressed together are
        /// where a cruel person may shove.
        /// </summary>
        public void Resolve(IReadOnlyList<PhysicsWorld.Contact> contacts)
        {
            for (int c = 0; c < contacts.Count; c++)
            {
                PhysicsWorld.Contact contact = contacts[c];
                if (contact.BodyB < 0)
                {
                    continue;
                }

                Agent first = people.PersonAt(contact.BodyA);
                Agent second = people.PersonAt(contact.BodyB);
                if (first == null || second == null || !first.IsParticipating || !second.IsParticipating)
                {
                    continue;
                }

                // Who ran into whom: the one coming on faster along the line
                // between them.
                long alongFirst = Along(first, second);
                long alongSecond = Along(second, first);
                Agent mover = alongFirst >= alongSecond ? first : second;
                Agent other = mover == first ? second : first;
                int closing = (int)((alongFirst + alongSecond) / PhysicsWorld.SubMillimetre);
                if (contact.Began && Collide(mover, other, closing))
                {
                    continue;
                }

                // Not fast enough to barge through them by accident: the cruel
                // take hold and shove instead of going round.
                if (!TryShove(mover, other))
                {
                    TryShove(other, mover);
                }
            }
        }

        /// <summary>
        /// How fast <paramref name="self"/> was coming toward <paramref name="toward"/>
        /// going into the step, in hundredths of a millimetre per tick.
        /// </summary>
        private long Along(Agent self, Agent toward)
        {
            long nx = (long)toward.Body.Position.X - self.Body.Position.X;
            long nz = (long)toward.Body.Position.Z - self.Body.Position.Z;
            long distance = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (distance == 0L)
            {
                return 0L;
            }

            (long x, long z) = people.VelocityOf(self);
            return (x * nx + z * nz) / distance;
        }

        /// <summary>
        /// Only a frightened runner fleeing (or burning) counts as running into
        /// somebody; anybody else meeting a person is just the jostle of a crowd,
        /// which the engine has already dealt with. True when it counted.
        /// </summary>
        private bool Collide(Agent mover, Agent other, int closing)
        {
            if (mover.Fear.State != AgentFearState.Scared ||
                (mover.Intent.Activity != AgentActivityState.Fleeing && mover.Intent.Activity != AgentActivityState.Burning) ||
                mover.Body.State != AgentBodyState.Upright || closing <= 0)
            {
                return false;
            }

            FallSettings settings = context.Scenario.Falls;
            if (other.IsDown)
            {
                // Running into somebody on the floor: over they go.
                if (closing < settings.TripMinimumSpeed)
                {
                    return false;
                }

                people.FallTowards(mover, mover.Body.Heading);
                body.Trip(mover, other.Body.EventId);
                return true;
            }

            if (other.Body.State != AgentBodyState.Upright || closing < TraitEffects.BumpMinimumSpeed(mover, context.Scenario))
            {
                return false;
            }

            var midpoint = new LogicalPosition(
                (int)(((long)mover.Body.Position.X + other.Body.Position.X) / 2),
                (int)(((long)mover.Body.Position.Z + other.Body.Position.Z) / 2));
            CausalEvent collision = context.Events.Append(
                context.Tick,
                mover.Id,
                FireReactionEventType.AgentsCollided,
                midpoint,
                closing,
                0,
                mover.Fear.ScaredEventId,
                other.Id);

            int moverFalls = IntegerMath.HeadingBetween(other.Body.Position, mover.Body.Position, mover.Body.Heading);
            int otherFalls = IntegerMath.HeadingBetween(mover.Body.Position, other.Body.Position, other.Body.Heading);
            if (closing >= settings.KnockdownClosingSpeed)
            {
                // A much stronger person only reels from a hit that floors the other.
                KnockDownOrStagger(mover, other, collision.EventId, closing, moverFalls);
                KnockDownOrStagger(other, mover, collision.EventId, closing, otherFalls);
            }
            else
            {
                body.Stagger(mover, collision.EventId);
                body.Stagger(other, collision.EventId);
            }

            // Crashing into someone on fire, or while on fire, spreads the flames.
            if (mover.Burning.IsBurning)
            {
                body.CatchFire(other, mover.Burning.EventId);
            }
            else if (other.Burning.IsBurning)
            {
                body.CatchFire(mover, other.Burning.EventId);
            }

            if (other.Fear.State == AgentFearState.Calm)
            {
                fear.Alarm(other, collision.EventId, AgentAlertSource.Bumped, mover.Body.Position);
            }

            sound.Thud(mover.Id, midpoint, collision.EventId);
            return true;
        }

        /// <summary>
        /// A cruel, frightened person who comes up against somebody in the way
        /// of where they want to go takes hold and heaves them aside rather than
        /// edging round them. It needs no speed at all, so it is what happens in
        /// the queue at a doorway, and there is a pause before they do it again.
        /// Frozen with fear or not, whoever is in front of them is fair game.
        /// </summary>
        private bool TryShove(Agent shover, Agent victim)
        {
            FallSettings settings = context.Scenario.Falls;
            if (shover.Fear.State != AgentFearState.Scared || shover.Body.State != AgentBodyState.Upright ||
                victim.Body.State != AgentBodyState.Upright || shover.Traits.Evil < settings.ShoveMinimumEvil ||
                context.Tick < shover.Intent.NextShoveTick || shover.Body.Speed <= 0)
            {
                return false;
            }

            // Only somebody ahead of them, within sixty degrees of the way they
            // are trying to go.
            LogicalPosition ahead = IntegerMath.Direction(shover.Body.Heading);
            long dx = (long)victim.Body.Position.X - shover.Body.Position.X;
            long dz = (long)victim.Body.Position.Z - shover.Body.Position.Z;
            long distance = IntegerMath.Sqrt(dx * dx + dz * dz);
            if ((ahead.X * dx + ahead.Z * dz) * 2L <= distance * IntegerMath.TrigScale)
            {
                return false;
            }

            shover.Intent.NextShoveTick = checked(context.Tick + settings.ShoveIntervalTicks);
            int away = IntegerMath.HeadingBetween(shover.Body.Position, victim.Body.Position, shover.Body.Heading);
            CausalEvent shoved = context.Events.Append(
                context.Tick,
                shover.Id,
                FireReactionEventType.AgentShoved,
                victim.Body.Position,
                settings.ShovePushMillimetres,
                0,
                shover.Fear.ScaredEventId,
                victim.Id);

            if (shover.Traits.Strength - victim.Traits.Strength >= settings.ShoveKnockDownStrengthGap)
            {
                body.ShoveBack(victim, away, settings.ShovePushMillimetres, shoved.EventId);
            }
            else
            {
                body.Slide(victim, away, settings.ShovePushMillimetres);
                body.Stagger(victim, shoved.EventId);
            }

            if (victim.Fear.State == AgentFearState.Calm)
            {
                fear.Alarm(victim, shoved.EventId, AgentAlertSource.Bumped, shover.Body.Position);
            }

            sound.Thud(shover.Id, victim.Body.Position, shoved.EventId);
            return true;
        }

        private void KnockDownOrStagger(Agent agent, Agent hitBy, ulong collisionEventId, int closingSpeed, int fallHeading)
        {
            if (TraitEffects.ShrugsOff(agent, hitBy, context.Scenario))
            {
                body.Stagger(agent, collisionEventId);
            }
            else
            {
                people.FallTowards(agent, fallHeading);
                body.KnockDown(agent, collisionEventId, closingSpeed);
            }
        }
    }
}
