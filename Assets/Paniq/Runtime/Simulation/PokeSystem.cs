namespace Paniq.Simulation
{
    /// <summary>
    /// Poking people (prototype 3, 2026-09-25): the player clicks somebody
    /// and they get a jab. The jab itself is the player's act and lands at
    /// once -- a lurch backwards and a stagger, if they are on their feet --
    /// but the person's own reaction obeys the rule every reaction obeys:
    /// it comes a few ticks later, at their reaction tick. Then they look
    /// round for whoever did it (the red "!"), and a calm person stops what
    /// they were doing to glare for a moment. Poked once too often in a row
    /// they are annoyed: they say so, and a calm person goes and stands
    /// somewhere else.
    /// <para>
    /// A poke never frightens anybody. It is a nuisance, not a threat, and
    /// the crowd's fear is for threats.
    /// </para>
    /// Phase 1½: the reactions due this tick are written here, before anybody
    /// decides anything, so phase 4 already sees them.
    /// </summary>
    internal sealed class PokeSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly BodySystem body;
        private readonly CalmBehaviour calm;
        private readonly PokeSettings settings;

        public PokeSystem(SimulationContext context, Crowd crowd, BodySystem body, CalmBehaviour calm)
        {
            this.context = context;
            this.crowd = crowd;
            this.body = body;
            this.calm = calm;
            settings = context.Scenario.Poke;
        }

        /// <summary>The player's poke, carried out by <see cref="PlayerCommandSystem"/> on its tick.</summary>
        public void Poke(Agent agent)
        {
            int tick = context.Tick;
            AgentPoke poke = agent.Poke;
            ulong poked = context.Events.Append(tick, default, CausalEventType.PowerPoked, agent.Body.Position,
                0, 0, 0UL, agent.Id).EventId;

            // The jab: somebody on their feet lurches back from the way they
            // face and staggers; somebody sitting, lying or out cold only
            // feels it.
            if (agent.Body.IsOnTheirFeet && !agent.Sitting.OnIt)
            {
                body.Slide(agent, IntegerMath.NormalizeDegrees(agent.Body.Heading + 180), settings.LurchMillimetres);
                body.Stagger(agent, poked);
            }

            if (tick - poke.LastPokeTick > settings.AnnoyedWindowTicks)
            {
                poke.CountInARow = 0;
            }

            poke.CountInARow++;
            poke.LastPokeTick = tick;

            // A reaction already due -- to a poke a tick or two ago -- is
            // kept, and this poke is folded into it, the way somebody jabbed
            // twice in quick succession turns round once. It is neither
            // pushed later nor handed to the newer poke, which is the rule
            // ThinkAgainSoon keeps for decisions. The folded poke still
            // counts toward annoyance, which is judged when they turn.
            if (poke.ReactAtTick <= 0)
            {
                poke.PokeEventId = poked;
                poke.ReactAtTick = context.ReactionTick();
            }
        }

        /// <summary>Everybody whose reaction is due: looking round, or getting annoyed.</summary>
        public void Advance()
        {
            int tick = context.Tick;
            Agent[] all = crowd.All;
            for (int i = 0; i < all.Length; i++)
            {
                Agent agent = all[i];
                AgentPoke poke = agent.Poke;
                if (poke.ReactAtTick <= 0 || tick < poke.ReactAtTick)
                {
                    continue;
                }

                poke.ReactAtTick = 0;
                if (!agent.IsParticipating)
                {
                    continue;
                }

                bool annoyed = poke.CountInARow >= settings.AnnoyedAfterPokes;
                context.Events.Append(tick, agent.Id, annoyed ? CausalEventType.AgentAnnoyed : CausalEventType.AgentPoked,
                    agent.Body.Position, 0, 0, poke.PokeEventId);
                if (annoyed)
                {
                    // Said their piece: the next poke starts the count again.
                    poke.CountInARow = 0;
                }

                if (agent.Fear.State != AgentFearState.Calm)
                {
                    // Busy being frightened: they decide again, and that is all.
                    context.ThinkAgainSoon(agent.Intent);
                    continue;
                }

                if (agent.Sitting.OnIt || agent.Sitting.ChairIndex >= 0 || agent.Errand.Has ||
                    !IsLoitering(agent.Intent.Activity))
                {
                    // Sitting, on an errand or in the middle of something
                    // with steps to it: the look is enough.
                    continue;
                }

                // Stop and look round for whoever did it, the way a calm
                // person looks round anyway; then the calm behaviour picks
                // something else to do. Annoyed, they give it one glance and
                // get on with it, which is how somebody walks off in a huff.
                agent.Intent.SocialPartnerIndex = -1;
                agent.Doors.StrollDoorIndex = -1;
                calm.LookRound(agent, annoyed ? 1 : 2);
            }
        }

        private static bool IsLoitering(AgentActivityState activity)
        {
            switch (activity)
            {
                case AgentActivityState.Standing:
                case AgentActivityState.LookingAround:
                case AgentActivityState.Strolling:
                case AgentActivityState.Socialising:
                case AgentActivityState.Investigating:
                    return true;
                default:
                    return false;
            }
        }
    }
}
