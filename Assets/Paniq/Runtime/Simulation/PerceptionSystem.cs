namespace Paniq.Simulation
{
    /// <summary>
    /// What a person notices each tick before deciding anything: a threat in
    /// their vision cone, a threat they can hear nearby, and their own
    /// reaction timer running out. It asks <see cref="Threats"/>, never the
    /// fire by name, so a person is frightened of whatever the level holds.
    /// </summary>
    internal sealed class PerceptionSystem
    {
        private readonly SimulationContext context;
        private readonly Threats threats;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;

        public PerceptionSystem(SimulationContext context, Threats threats, FearSystem fear, SoundSystem sound)
        {
            this.context = context;
            this.threats = threats;
            this.fear = fear;
            this.sound = sound;
        }

        public void Update(Agent agent)
        {
            if (!threats.AnyActive)
            {
                return;
            }

            bool enteredAlert = false;
            if (agent.Fear.State == AgentFearState.Calm)
            {
                if (SeesDanger(agent, out ulong seenRoot))
                {
                    // Seeing danger: startled, and yelling about it.
                    ulong alertEventId = fear.StartAlert(agent, seenRoot, AgentAlertSource.Visual);
                    sound.Yell(agent, alertEventId);
                    enteredAlert = true;
                }
                else if (agent.Intent.Activity != AgentActivityState.Investigating)
                {
                    sound.HearThreats(agent);
                }
            }

            if (agent.Fear.State != AgentFearState.Alert)
            {
                return;
            }

            if (!enteredAlert && agent.Fear.AlertSource != AgentAlertSource.Visual && SeesDanger(agent, out ulong root))
            {
                fear.PromoteAlertToVisual(agent, root);
            }

            // The alert stays visible for at least the detection tick, even
            // when the seeded reaction delay is zero.
            if (!enteredAlert && context.Tick >= agent.Fear.ReactionEndTick)
            {
                fear.MakeScared(agent);
            }
        }

        private bool SeesDanger(Agent agent, out ulong rootEventId)
        {
            return threats.IsVisibleFrom(agent.Body.Position, agent.Body.Heading,
                context.Scenario.Perception.VisionRangeMillimetres, out rootEventId);
        }
    }
}
