namespace Paniq.Simulation
{
    /// <summary>
    /// What a person notices each tick before deciding anything: fire in
    /// their vision cone, fire crackling nearby, and their own reaction
    /// timer running out.
    /// </summary>
    internal sealed class PerceptionSystem
    {
        private readonly SimulationContext context;
        private readonly FireSystem fire;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;

        public PerceptionSystem(SimulationContext context, FireSystem fire, FearSystem fear, SoundSystem sound)
        {
            this.context = context;
            this.fire = fire;
            this.fear = fear;
            this.sound = sound;
        }

        public void Update(Agent agent)
        {
            if (!fire.Active)
            {
                return;
            }

            bool enteredAlert = false;
            if (agent.Fear.State == AgentFearState.Calm)
            {
                if (SeesFire(agent))
                {
                    // Seeing fire: startled, and yelling about it.
                    ulong alertEventId = fear.StartAlert(agent, fire.ActivationEventId, AgentAlertSource.Visual);
                    sound.Yell(agent, alertEventId);
                    enteredAlert = true;
                }
                else if (agent.Intent.Activity != AgentActivityState.Investigating)
                {
                    sound.HearFire(agent);
                }
            }

            if (agent.Fear.State != AgentFearState.Alert)
            {
                return;
            }

            if (!enteredAlert && agent.Fear.AlertSource != AgentAlertSource.Visual && SeesFire(agent))
            {
                fear.PromoteAlertToVisual(agent);
            }

            // The alert stays visible for at least the detection tick, even
            // when the seeded reaction delay is zero.
            if (!enteredAlert && context.Tick >= agent.Fear.ReactionEndTick)
            {
                fear.MakeScared(agent);
            }
        }

        private bool SeesFire(Agent agent)
        {
            return fire.IsVisibleFrom(agent.Body.Position, agent.Body.Heading, context.Scenario.Perception.VisionRangeMillimetres);
        }
    }
}
