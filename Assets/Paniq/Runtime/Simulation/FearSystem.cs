namespace Paniq.Simulation
{
    /// <summary>
    /// Fear state changes: calm to alert, alert to scared, freezing and
    /// snapping out of it. Each change is logged with its cause. This system
    /// decides nothing on its own; perception, sound and collisions call it
    /// when something frightens a person.
    /// </summary>
    internal sealed class FearSystem
    {
        private readonly SimulationContext context;
        private readonly Threats threats;

        public FearSystem(SimulationContext context, Threats threats)
        {
            this.context = context;
            this.threats = threats;
        }

        /// <summary>
        /// Temperaments are dealt like a deck rather than rolled one by one,
        /// so every room gets the authored mix (10 people: 2 freeze for good,
        /// 3 freeze for a while, 5 run). The most fearful people (nervousness
        /// minus bravery) get the freezing cards first; the seed only decides
        /// between people who are equally fearful.
        /// </summary>
        public void DealTemperaments(Agent[] agents)
        {
            TemperamentSettings settings = context.Scenario.Temperament;
            int count = agents.Length;
            int freezeForever = (count * settings.FreezeForeverPercent + 50) / 100;
            int freezeThenRun = System.Math.Min(count - freezeForever, (count * settings.FreezeThenRunPercent + 50) / 100);

            // A seeded shuffle breaks ties, then a stable sort puts the most fearful first.
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = context.Random.NextIntInclusive(0, i);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var shuffledPosition = new int[count];
            for (int i = 0; i < count; i++)
            {
                shuffledPosition[order[i]] = i;
            }

            System.Array.Sort(order, (left, right) =>
            {
                int byFear = TraitEffects.Fearfulness(agents[right].Traits).CompareTo(TraitEffects.Fearfulness(agents[left].Traits));
                return byFear != 0 ? byFear : shuffledPosition[left].CompareTo(shuffledPosition[right]);
            });

            for (int i = 0; i < count; i++)
            {
                agents[order[i]].Personality.Temperament = i < freezeForever ? AgentPanicTemperament.FreezeForever
                    : i < freezeForever + freezeThenRun ? AgentPanicTemperament.FreezeThenRun
                    : AgentPanicTemperament.Runner;
            }
        }

        /// <summary>The ticks on which somebody is due to finish being startled, so nobody else is given one of them.</summary>
        private readonly System.Collections.Generic.HashSet<int> reactionEndsTaken = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// A reaction delay nobody else has: if somebody is already due to
        /// finish being startled on that tick, this one is put off by the
        /// startle stagger, and again until the tick is theirs alone. Six
        /// people a bell reaches on the same tick come up out of their chairs
        /// one after another, a few ticks apart, never all at once -- the
        /// owner's rule that nothing happens to a whole group on one tick.
        /// Processing order decides who waits, so a replay agrees, and no
        /// random number is drawn.
        /// </summary>
        private int Staggered(int tick, int delay)
        {
            reactionEndsTaken.RemoveWhere(taken => taken < tick);
            int stagger = context.Scenario.Perception.StartleStaggerTicks;
            int end = checked(tick + delay);
            while (!reactionEndsTaken.Add(end))
            {
                end = checked(end + stagger);
            }

            return end - tick;
        }

        /// <summary>Startled: stop, draw a seeded reaction delay, and log what caused it.</summary>
        public ulong StartAlert(Agent agent, ulong causalParentEventId, AgentAlertSource alertSource)
        {
            int tick = context.Tick;
            agent.Fear.State = AgentFearState.Alert;
            agent.Fear.AlertSource = alertSource;
            agent.Intent.Activity = AgentActivityState.Reacting;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Hearing.HasSoundPoint = false;
            agent.Hearing.ClearPending();

            // Whatever the day had them doing is over: fear has its own rules.
            agent.Errand.Clear();
            // Their own lag before anything at all, then their own seeded
            // reaction delay on top, then a tick nobody else finishes on.
            agent.Fear.ReactionDelayTicks = Staggered(tick, context.ReactionLag() + context.Random.NextIntInclusive(0,
                TraitEffects.MaximumReactionDelayTicks(agent, context.Scenario)));
            agent.Fear.ReactionEndTick = checked(tick + agent.Fear.ReactionDelayTicks);
            CausalEvent alert = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentAlerted,
                agent.Body.Position,
                threats.Count,
                agent.Fear.ReactionDelayTicks,
                causalParentEventId);
            agent.Fear.AlertEventId = alert.EventId;
            return alert.EventId;
        }

        /// <summary>Startled by something they heard or felt at <paramref name="from"/>; they will turn toward it.</summary>
        public void Alarm(Agent agent, ulong causalParentEventId, AgentAlertSource alertSource, LogicalPosition from)
        {
            // A bell tells you there is a fire without showing you one, and
            // it frightens you just the same: the owner's rule (2026-09-25),
            // "pull it, and everybody panics". The level-headed used to walk
            // out at a stroll instead.
            StartAlert(agent, causalParentEventId, alertSource);
            agent.Hearing.SoundPoint = from;
            agent.Hearing.HasSoundPoint = true;
        }

        /// <summary>An alerted person now sees the danger for themselves; <paramref name="rootEventId"/> is what started the threat they saw.</summary>
        public void PromoteAlertToVisual(Agent agent, ulong rootEventId)
        {
            agent.Fear.AlertSource = AgentAlertSource.Visual;
            CausalEvent alert = context.Events.Append(
                context.Tick,
                agent.Id,
                CausalEventType.AgentAlerted,
                agent.Body.Position,
                threats.Count,
                agent.Fear.ReactionDelayTicks,
                rootEventId);
            agent.Fear.AlertEventId = alert.EventId;
        }

        /// <summary>
        /// Panic takes one of three shapes, fixed per person: run, freeze and
        /// then run, or freeze for good.
        /// </summary>
        public void MakeScared(Agent agent)
        {
            if (agent.Fear.State == AgentFearState.Scared)
            {
                return;
            }

            int tick = context.Tick;
            agent.Fear.State = AgentFearState.Scared;

            // Whatever the day had them doing is over: fear has its own rules.
            // Cleared here as well as on the way into being alert, because a
            // test frightens somebody straight to scared.
            agent.Errand.Clear();
            CausalEvent scared = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentScared,
                agent.Body.Position,
                threats.Count,
                0,
                agent.Fear.AlertEventId != 0UL ? agent.Fear.AlertEventId : threats.RootEventId);
            agent.Fear.ScaredEventId = scared.EventId;

            if (agent.Personality.Temperament == AgentPanicTemperament.Runner)
            {
                StartFleeing(agent);
                return;
            }

            TemperamentSettings settings = context.Scenario.Temperament;
            agent.Intent.Activity = AgentActivityState.Frozen;
            agent.Fear.FreezeEndTick = agent.Personality.Temperament == AgentPanicTemperament.FreezeForever
                ? int.MaxValue
                : checked(tick + context.Random.NextIntInclusive(settings.FreezeMinimumTicks, settings.FreezeMaximumTicks));
            CausalEvent froze = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentFroze,
                agent.Body.Position,
                0,
                agent.Fear.FreezeEndTick == int.MaxValue ? 0 : agent.Fear.FreezeEndTick - tick,
                scared.EventId);
            agent.Fear.FrozeEventId = froze.EventId;
        }

        /// <summary>
        /// Snapping out of a freeze: log it, start running, and shout as soon
        /// as anybody does. The cause is their own freeze running out, or someone shaking them.
        /// </summary>
        public void Unfreeze(Agent agent, ulong causalParentEventId = 0UL)
        {
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentUnfroze, agent.Body.Position, 0, 0,
                causalParentEventId != 0UL ? causalParentEventId : agent.Fear.FrozeEventId);
            StartFleeing(agent);
        }

        /// <summary>
        /// Running. The first shout comes with the first stride, a few ticks
        /// late like every reaction, and the next ones at their own interval:
        /// it used to come two to five seconds in, which spread a fright
        /// round a meeting table one seat at a time.
        /// </summary>
        private void StartFleeing(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Fleeing;
            agent.Doors.HasLookedForAWayOut = false;
            context.ThinkAgainSoon(agent.Intent);
            agent.Fear.NextShoutTick = context.ReactionTick();
        }

        /// <summary>
        /// A startled person stops. If they saw the danger they turn to face
        /// it; if they were yelled at or bumped, they turn toward where that
        /// came from.
        /// </summary>
        public MotorIntent AlertIntent(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Reacting;
            int goalHeading = agent.Body.Heading;
            if (agent.Fear.AlertSource == AgentAlertSource.Visual &&
                threats.NearestDistanceSquared(agent.Body.Position, out LogicalPosition dangerPoint, out _) < long.MaxValue)
            {
                goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, dangerPoint, agent.Body.Heading);
            }
            else if (agent.Hearing.HasSoundPoint)
            {
                goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, agent.Hearing.SoundPoint, agent.Body.Heading);
            }

            return new MotorIntent(goalHeading, 0, agent.Personality.PanicTurnRate, context.Scenario.Panic.Acceleration);
        }
    }
}
