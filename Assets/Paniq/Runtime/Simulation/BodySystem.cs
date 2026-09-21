using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Whether the body is under the person's control: staggering, lying on
    /// the floor, getting up, and being caught by the fire. A body that is
    /// not upright makes no move but still takes up space.
    /// </summary>
    internal sealed class BodySystem
    {
        private readonly SimulationContext context;
        private readonly FireSystem fire;
        private readonly SoundSystem sound;
        private readonly WorldGeometry geometry;
        private readonly Crowd crowd;
        private readonly FearSystem fear;
        private readonly FallSettings settings;

        /// <summary>Set once the objects exist, so a body pushed across the floor cannot slide through the furniture.</summary>
        private PhysicsObjectSystem objects;

        public BodySystem(SimulationContext context, Crowd crowd, WorldGeometry geometry, FireSystem fire, SoundSystem sound,
            FearSystem fear)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.fire = fire;
            this.sound = sound;
            this.fear = fear;
            settings = context.Scenario.Falls;
        }

        /// <summary>Wired up after construction, because the objects are built after this system.</summary>
        public void UseObjects(PhysicsObjectSystem physicsObjects) => objects = physicsObjects;

        /// <summary>
        /// Advances staggering, lying down and getting up. Returns true while
        /// the body is not under the person's control this tick.
        /// </summary>
        public bool Update(Agent agent)
        {
            AgentBody body = agent.Body;
            if (body.State == AgentBodyState.Upright)
            {
                return false;
            }

            int tick = context.Tick;
            body.Speed = 0;
            if (tick < body.EndTick)
            {
                return true;
            }

            if (body.State == AgentBodyState.Fallen)
            {
                body.State = AgentBodyState.GettingUp;
                body.EndTick = checked(tick + settings.GetUpTicks);
                return true;
            }

            if (body.State == AgentBodyState.Unconscious)
            {
                // Coming round, then getting up slowly.
                body.EventId = context.Events.Append(tick, agent.Id, FireReactionEventType.AgentCameTo, body.Position, 0,
                    settings.ComeToGetUpTicks, body.EventId).EventId;
                body.State = AgentBodyState.GettingUp;
                body.EndTick = checked(tick + settings.ComeToGetUpTicks);
                return true;
            }

            if (body.State == AgentBodyState.GettingUp)
            {
                context.Events.Append(tick, agent.Id, FireReactionEventType.AgentGotUp, body.Position, 0, 0, body.EventId);
            }

            body.State = AgentBodyState.Upright;
            if (agent.Fear.State == AgentFearState.Scared && agent.Intent.Activity != AgentActivityState.Frozen &&
                !agent.Burning.IsBurning)
            {
                // Back on your feet: look for a way out afresh.
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Intent.NextPanicDecisionTick = tick;
            }

            return false;
        }

        /// <param name="closingSpeed">How hard the hit was; harder hits knock people out more often.</param>
        public void KnockDown(Agent agent, ulong collisionEventId, int closingSpeed)
        {
            // Being knocked about or set alight ends any composure.
            fear.BreakComposure(agent);
            int duration = context.Random.NextIntInclusive(settings.KnockdownMinimumTicks, settings.KnockdownMaximumTicks);
            CausalEvent down = context.Events.Append(
                context.Tick,
                agent.Id,
                FireReactionEventType.AgentKnockedDown,
                agent.Body.Position,
                0,
                duration,
                collisionEventId);
            PutDown(agent, AgentBodyState.Fallen, duration, down.EventId);
            int hardness = Math.Max(0, closingSpeed - settings.KnockdownClosingSpeed) * settings.PassOutPercentPerSpeed;
            MaybePassOut(agent, down.EventId, settings.PassOutChancePercent + hardness);
        }

        /// <summary>
        /// After a hard fall, maybe out cold: lying still for several
        /// seconds (stars over their head), then groggily getting up.
        /// </summary>
        public void MaybePassOut(Agent agent, ulong downEventId, int basePercent)
        {
            if (!context.Random.NextPercent(TraitEffects.PassOutChancePercent(agent, context.Scenario, basePercent)))
            {
                return;
            }

            int duration = context.Random.NextIntInclusive(settings.UnconsciousMinimumTicks, settings.UnconsciousMaximumTicks);
            CausalEvent passedOut = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentPassedOut,
                agent.Body.Position, 0, duration, downEventId);
            PutDown(agent, AgentBodyState.Unconscious, duration, passedOut.EventId);
        }

        /// <summary>
        /// Shoved bodily backwards and onto the floor: the jet from an
        /// extinguisher. They slide as far as there is room for and land
        /// facing the way they were pushed.
        /// </summary>
        public void ShoveBack(Agent agent, int away, int distance, ulong causeEventId)
        {
            if (agent.Body.State != AgentBodyState.Upright)
            {
                // Already off their feet: there is nothing left to knock down,
                // and a body that is not upright never moves.
                return;
            }

            fear.BreakComposure(agent);
            Slide(agent, away, distance);
            int duration = context.Random.NextIntInclusive(settings.KnockdownMinimumTicks, settings.KnockdownMaximumTicks);
            CausalEvent down = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentKnockedDown,
                agent.Body.Position, 0, duration, causeEventId);
            PutDown(agent, AgentBodyState.Fallen, duration, down.EventId);
        }

        /// <summary>
        /// Pushed across the floor without falling: as far as the walls, the
        /// furniture and the other people allow.
        /// </summary>
        public void Slide(Agent agent, int heading, int distance)
        {
            LogicalPosition step = agent.Body.Position + IntegerMath.Displacement(heading, distance);
            LogicalPosition destination = geometry.ClampIntoWalkable(agent.Body.Position, agent.Doors.ExitDoorIndex, step);
            if (crowd.FindBlocking(agent, agent.Body.Position, destination) != null ||
                geometry.ClipsDoorFrame(agent.Body.Position, destination) ||
                objects.BlocksBody(agent, agent.Body.Position, destination))
            {
                return;
            }

            agent.Body.Position = destination;
            agent.Body.Speed = 0;
        }

        /// <summary>The flames on someone are put out by a jet of water.</summary>
        public void PutOutPerson(Agent agent, ulong causeEventId)
        {
            if (!agent.Burning.IsBurning)
            {
                return;
            }

            agent.Burning.IsBurning = false;
            agent.Burning.EventId = 0UL;
            context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentDoused,
                agent.Body.Position, 0, 0, causeEventId, agent.Id);
        }

        /// <summary>Knocked off balance: reeling for a moment, jolted a little to one side.</summary>
        public void Stagger(Agent agent, ulong causeEventId)
        {
            // Being knocked about or set alight ends any composure.
            fear.BreakComposure(agent);
            int duration = context.Random.NextIntInclusive(settings.StaggerMinimumTicks, settings.StaggerMaximumTicks);
            int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
            agent.Body.Heading = IntegerMath.NormalizeDegrees(agent.Body.Heading +
                side * context.Random.NextIntInclusive(settings.StaggerJoltMinimumDegrees, settings.StaggerJoltMaximumDegrees));
            PutDown(agent, AgentBodyState.Staggering, duration, causeEventId);
        }

        /// <summary>A stumble: on your own, over someone on the floor, or over a box. It makes a thud.</summary>
        public void Trip(Agent agent, ulong causalParentEventId)
        {
            // Being knocked about or set alight ends any composure.
            fear.BreakComposure(agent);
            int duration = context.Random.NextIntInclusive(settings.TripMinimumTicks, settings.TripMaximumTicks);
            CausalEvent trip = context.Events.Append(
                context.Tick,
                agent.Id,
                FireReactionEventType.AgentTripped,
                agent.Body.Position,
                context.Scenario.Hearing.BumpSoundRadiusMillimetres,
                duration,
                causalParentEventId);
            PutDown(agent, AgentBodyState.Fallen, duration, trip.EventId);
            sound.Thud(agent.Id, agent.Body.Position, trip.EventId);
        }

        private void PutDown(Agent agent, AgentBodyState state, int duration, ulong eventId)
        {
            agent.Body.State = state;
            agent.Body.EndTick = checked(context.Tick + duration);
            agent.Body.EventId = eventId;
            agent.Body.Speed = 0;
            agent.Body.BlockedTicks = 0;
        }

        /// <summary>
        /// Touched by flames: on fire, and running wild until they collapse
        /// (see <see cref="BurningBehaviour"/>). The cause is the burning
        /// square, or the burning person, that set them alight. Anyone
        /// already on fire stays as they are.
        /// </summary>
        public void CatchFire(Agent agent, ulong causeEventId)
        {
            // Being knocked about or set alight ends any composure.
            fear.BreakComposure(agent);
            if (!agent.IsParticipating || agent.Burning.IsBurning)
            {
                return;
            }

            int tick = context.Tick;
            FireSettings fireSettings = context.Scenario.Fire;
            int duration = context.Random.NextIntInclusive(fireSettings.BurnMinimumTicks, fireSettings.BurnMaximumTicks);
            CausalEvent caught = context.Events.Append(tick, agent.Id, FireReactionEventType.AgentCaughtFire,
                agent.Body.Position, 0, duration, causeEventId);

            AgentBurning burning = agent.Burning;
            burning.IsBurning = true;
            burning.EndTick = checked(tick + duration);
            burning.EventId = caught.EventId;
            burning.NextTurnTick = tick;
            burning.NextScreamTick = tick;

            // Whatever they were doing, they are now in blind panic.
            agent.Fear.State = AgentFearState.Scared;
            if (agent.Fear.ScaredEventId == 0UL)
            {
                agent.Fear.ScaredEventId = caught.EventId;
            }

            agent.Intent.Activity = AgentActivityState.Burning;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Hearing.HasSoundPoint = false;
            agent.Doors.ExitDoorIndex = -1;
        }

        /// <summary>Burnt out: collapses and is lost. Returns true when it happened this tick.</summary>
        public bool BurnOut(Agent agent)
        {
            if (!agent.Burning.IsBurning || context.Tick < agent.Burning.EndTick)
            {
                return false;
            }

            MakeLost(agent, agent.Burning.EventId);
            return true;
        }

        /// <summary>Out of the run for good, named after what finished them.</summary>
        private void MakeLost(Agent agent, ulong causeEventId)
        {
            if (!agent.IsParticipating)
            {
                return;
            }

            agent.Participation = AgentParticipation.NoLongerParticipating;
            agent.Outcome = AgentTerminalOutcome.Lost;
            agent.Body.Speed = 0;
            context.Events.Append(
                context.Tick,
                agent.Id,
                FireReactionEventType.AgentLost,
                agent.Body.Position,
                fire.BurningCount,
                0,
                causeEventId);
        }
    }
}
