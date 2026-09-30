using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The hand's charge (2026-09-30, the owner: "Influence points and
    /// cooldown. Using influence depletes a bar that is automatically refilled
    /// continuously"). One bar for the one hand: it drains every tick the
    /// hand is on a place -- a hold, a drag, a click's beacon -- or on a
    /// person by the shirt, and refills every tick whatever the hand is
    /// doing. Empty, the hand comes off by itself and no press is taken until
    /// the bar is back above <see cref="HandChargeSettings.PressNeeds"/>; the
    /// hand does not come back on by itself when it is, the player presses
    /// again.
    /// <para>
    /// A system of its own rather than part of the influence: it governs the
    /// tug as much as the place, so neither of those need know the other, and
    /// the command system asks one gate. It is integer arithmetic on the
    /// command stream and draws nothing, so a replay replays it; the round
    /// left alone gives no commands and never touches it. Off
    /// (<see cref="HandChargeSettings.Enabled"/>), the hand is free, as it was.
    /// </para>
    /// </summary>
    internal sealed class HandChargeSystem
    {
        private readonly SimulationContext context;
        private readonly HandChargeSettings settings;
        private readonly InfluenceSystem influence;
        private readonly TugSystem tugs;
        private int charge;

        public HandChargeSystem(SimulationContext context, InfluenceSystem influence, TugSystem tugs)
        {
            this.context = context;
            this.influence = influence;
            this.tugs = tugs;
            settings = context.Scenario.HandCharge;
            charge = settings.Capacity;
        }

        /// <summary>What is in the bar, 0 to the capacity.</summary>
        public int Charge => charge;

        /// <summary>The bar as a share of full, per mille, for the display.</summary>
        public int PerMille => settings.Capacity <= 0 ? 1000 : charge * 1000 / settings.Capacity;

        /// <summary>Whether a press is taken now: always with the charge off; else once the bar holds what a press needs.</summary>
        public bool MayPress => !settings.Enabled || charge >= settings.PressNeeds;

        /// <summary>Whether the bar is resting: too low for a press, and shown so.</summary>
        public bool IsResting => settings.Enabled && charge < settings.PressNeeds;

        /// <summary>
        /// Phase 1's tail, before the influence weighs anybody: the refill,
        /// then the drain for a hand that is on something. Run dry, the hand
        /// comes off the place and the person at once, and the log says the
        /// hand gave out.
        /// </summary>
        public void Advance()
        {
            if (!settings.Enabled)
            {
                return;
            }

            charge = Math.Min(settings.Capacity, charge + settings.RefillPerTick);
            bool held = influence.Count > 0 || tugs.HeldIndex >= 0;
            if (!held)
            {
                return;
            }

            charge -= settings.DrainPerTick;
            if (charge > 0)
            {
                return;
            }

            charge = 0;
            LogicalPosition at = influence.Count > 0 ? influence[0].At : default;
            context.Events.Append(context.Tick, default, CausalEventType.PowerHandSpent, at, 0, 0, 0UL, default);
            influence.Release();
            tugs.ReleaseHeld();
        }
    }
}
