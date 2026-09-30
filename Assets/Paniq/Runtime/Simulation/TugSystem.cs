using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The player's hand on a person (2026-09-29, the owner's rule): "It
    /// holds them in place. Should not be 100% instant, more like tugging
    /// someone's shirt. So you can save someone running into fire." The tug
    /// lasts as long as the button is held; the person is braked to a stop
    /// over about a second and held there. "But the strongest can break
    /// free. Sliding scale. A slightly not too strong can eventually break
    /// free by visibly shaking you off."
    /// <para>
    /// The one direct thing the player does to a person, and the stated
    /// exception to "suggest, never command": even so, the strong tear free,
    /// and nobody alight or already down can be held. The tug is the
    /// player's act and lands at once, as a nudge does; the person's own
    /// reaction -- looking round for whoever has hold of them -- comes at
    /// their reaction tick through the nudge's rules, so nothing they decide
    /// is decided on the tick of the press. Tearing free is drawn a jittered
    /// while ahead, never on the tick the hand went on.
    /// </para>
    /// Phase 1½, with the nudges: whoever tears free this tick does so before
    /// anybody decides anything; the brake itself is applied to every held
    /// person's intent in phase 4.
    /// </summary>
    internal sealed class TugSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly NudgeSystem nudges;
        private readonly TugSettings settings;

        /// <summary>The person (agent index) the hand is on, or -1.</summary>
        private int held = -1;

        public TugSystem(SimulationContext context, Crowd crowd, NudgeSystem nudges)
        {
            this.context = context;
            this.crowd = crowd;
            this.nudges = nudges;
            settings = context.Scenario.Tug;
        }

        /// <summary>Who the hand is on, as an agent index, or -1.</summary>
        public int HeldIndex => held;

        /// <summary>
        /// The player takes hold of somebody. One hand: whoever was held
        /// before is let go of first. Somebody alight, down, out of the
        /// building or dead cannot be held, and nothing is written.
        /// </summary>
        public void Tug(Agent agent)
        {
            if (held == agent.Index)
            {
                return;
            }

            if (held >= 0)
            {
                Release(crowd.All[held]);
            }

            if (!agent.IsParticipating || agent.Burning.IsBurning || agent.IsDown)
            {
                return;
            }

            AgentTug tug = agent.Tug;
            tug.Held = true;
            tug.HeldSinceTick = context.Tick;
            tug.TugEventId = context.Events.Append(context.Tick, default, CausalEventType.PowerTugged, agent.Body.Position,
                0, 0, 0UL, agent.Id).EventId;
            tug.TearsFreeAtTick = TearFreeTick(agent);
            held = agent.Index;

            // Winding up to something dangerous (2026-09-30): the tug catches it.
            TellSystem.Catch(context, agent, tug.TugEventId);

            // They look round for whoever has hold of them, a beat later, as
            // the nudged do; a tug frightens nobody and annoys nobody.
            nudges.Startle(agent, tug.TugEventId);
        }

        /// <summary>
        /// When they tear free: never, below the strength that can; at it,
        /// after the threshold's time, jittered; and halved for every point
        /// above it, so the brute is gone in a moment.
        /// </summary>
        private int TearFreeTick(Agent agent)
        {
            int strength = agent.Traits.Strength;
            if (strength < settings.TearsFreeFromStrength)
            {
                return 0;
            }

            int ticks = settings.TearFreeTicksAtThreshold >> Math.Min(30, strength - settings.TearsFreeFromStrength);
            return checked(context.Tick + Math.Max(context.ReactionLag(), context.Jittered(Math.Max(1, ticks))));
        }

        /// <summary>The player lets go. Nothing written when the hand was not on them.</summary>
        public void Release(Agent agent)
        {
            if (!agent.Tug.Held)
            {
                return;
            }

            LetGo(agent);
            context.Events.Append(context.Tick, default, CausalEventType.PowerReleasedTug, agent.Body.Position,
                context.Tick - agent.Tug.HeldSinceTick, 0, agent.Tug.TugEventId, agent.Id);
        }

        private void LetGo(Agent agent)
        {
            agent.Tug.Held = false;
            agent.Tug.TearsFreeAtTick = 0;
            if (held == agent.Index)
            {
                held = -1;
            }
        }

        /// <summary>
        /// Phase 1½: whoever is due to tear free does, visibly; and a hand on
        /// somebody who has since caught fire, gone out cold, got out or died
        /// is off them, because there is nothing left to hold.
        /// </summary>
        public void Advance()
        {
            if (held < 0)
            {
                return;
            }

            Agent agent = crowd.All[held];
            AgentTug tug = agent.Tug;
            if (!agent.IsParticipating || agent.Burning.IsBurning || agent.Body.State == AgentBodyState.Unconscious)
            {
                LetGo(agent);
                return;
            }

            if (tug.TearsFreeAtTick > 0 && context.Tick >= tug.TearsFreeAtTick)
            {
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentShookFree, agent.Body.Position,
                    context.Tick - tug.HeldSinceTick, 0, tug.TugEventId);
                tug.ShookFreeShownUntilTick = checked(context.Tick + settings.ShookFreeShownTicks);
                LetGo(agent);
            }
        }

        /// <summary>
        /// The brake: what a held person's feet are allowed to do this tick.
        /// They keep turning toward whatever they meant to do, and their
        /// speed comes down by the brake each tick to nothing, and stays
        /// there. Applied to the behaviour's own intent, so letting go
        /// leaves them exactly where their plan was.
        /// </summary>
        public MotorIntent Restrain(Agent agent, MotorIntent intent)
        {
            // ApplyBody brakes at twice the acceleration it is given.
            int acceleration = Math.Max(1, settings.BrakeMillimetresPerTickPerTick / 2);
            return new MotorIntent(intent.GoalHeading, 0, intent.TurnRate, acceleration);
        }
    }
}
