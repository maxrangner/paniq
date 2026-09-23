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

        /// <summary>Startled: stop, draw a seeded reaction delay, and log what caused it.</summary>
        public ulong StartAlert(Agent agent, ulong causalParentEventId, AgentAlertSource alertSource)
        {
            int tick = context.Tick;
            agent.Fear.State = AgentFearState.Alert;
            agent.Fear.AlertSource = alertSource;
            agent.Intent.Activity = AgentActivityState.Reacting;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Hearing.HasSoundPoint = false;
            agent.Fear.ReactionDelayTicks = context.Random.NextIntInclusive(0,
                TraitEffects.MaximumReactionDelayTicks(agent, context.Scenario));
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
            if (alertSource == AgentAlertSource.Alarm)
            {
                // A bell tells you there is a fire without showing you one, so
                // the level-headed simply leave.
                agent.Fear.Composed = TraitEffects.StaysComposed(agent.Traits, context.Scenario);
            }

            StartAlert(agent, causalParentEventId, alertSource);
            agent.Hearing.SoundPoint = from;
            agent.Hearing.HasSoundPoint = true;
        }

        /// <summary>
        /// Composure goes the moment the fire stops being an abstraction: it
        /// comes at them, somebody knocks them over, they catch light, or they
        /// see the flames for themselves. From then on they panic like anybody
        /// else.
        /// </summary>
        public void BreakComposure(Agent agent)
        {
            if (!agent.Fear.Composed)
            {
                return;
            }

            agent.Fear.Composed = false;

            // Whatever they were doing calmly, they are now running.
            if (agent.Fear.State == AgentFearState.Scared && agent.Intent.Activity != AgentActivityState.Frozen)
            {
                agent.Intent.NextPanicDecisionTick = context.Tick;
            }
        }

        /// <summary>An alerted person now sees the danger for themselves; <paramref name="rootEventId"/> is what started the threat they saw.</summary>
        public void PromoteAlertToVisual(Agent agent, ulong rootEventId)
        {
            agent.Fear.AlertSource = AgentAlertSource.Visual;
            BreakComposure(agent);
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
            CausalEvent scared = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentScared,
                agent.Body.Position,
                threats.Count,
                0,
                agent.Fear.AlertEventId != 0UL ? agent.Fear.AlertEventId : threats.RootEventId);
            agent.Fear.ScaredEventId = scared.EventId;

            if (agent.Personality.Temperament == AgentPanicTemperament.Runner || agent.Fear.Composed)
            {
                // Nobody who is keeping their head freezes; they head for a way out.
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
        /// Snapping out of a freeze: log it, start running, and shout straight
        /// away. The cause is their own freeze running out, or someone shaking them.
        /// </summary>
        public void Unfreeze(Agent agent, ulong causalParentEventId = 0UL)
        {
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentUnfroze, agent.Body.Position, 0, 0,
                causalParentEventId != 0UL ? causalParentEventId : agent.Fear.FrozeEventId);
            StartFleeing(agent);
            agent.Fear.NextShoutTick = context.Tick;
        }

        private void StartFleeing(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Fleeing;
            agent.Intent.NextPanicDecisionTick = context.Tick;
            agent.Fear.NextShoutTick = checked(context.Tick + TraitEffects.ShoutInterval(agent, context.Scenario, ref context.Random));
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
