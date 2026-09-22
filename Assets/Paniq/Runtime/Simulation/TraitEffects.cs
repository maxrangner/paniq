using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The one place that turns a person's traits into numbers the systems
    /// use. A trait of 5 always gives the plain scenario value, so an
    /// ordinary person behaves exactly as the settings describe; each point
    /// above or below 5 moves the value by the percentage in
    /// <see cref="TraitSettings"/>. Traits are read when used, never cached,
    /// so a later power that changes someone's traits takes effect at once
    /// (except pace, which <see cref="ApplyPace"/> sets).
    /// </summary>
    internal static class TraitEffects
    {
        /// <summary>Draws a personality: each trait is the rounded average of two 0–10 draws, so 5 is common and 0 or 10 rare.</summary>
        public static AgentTraitValues Draw(ref Pcg32 random)
        {
            return new AgentTraitValues(DrawOne(ref random), DrawOne(ref random), DrawOne(ref random),
                DrawOne(ref random), DrawOne(ref random), DrawOne(ref random), DrawOne(ref random));
        }

        private static int DrawOne(ref Pcg32 random)
        {
            int sum = random.NextIntInclusive(AgentTraitValues.Minimum, AgentTraitValues.Maximum) +
                      random.NextIntInclusive(AgentTraitValues.Minimum, AgentTraitValues.Maximum);
            return (sum + 1) / 2;
        }

        /// <summary>
        /// Walking and sprinting pace from the Speed trait, spread evenly
        /// between the Speed 0 and Speed 10 values, plus a seeded jitter (two
        /// random draws: walking, then sprinting).
        /// </summary>
        public static void ApplyPace(Agent agent, FireReactionScenarioData scenario, ref Pcg32 random)
        {
            int speed = agent.Traits.Speed;
            TraitSettings settings = scenario.Traits;
            agent.Personality.CalmSpeed = Math.Max(1,
                Between(scenario.Calm.SpeedMinimum, scenario.Calm.SpeedMaximum, speed) +
                random.NextIntInclusive(-settings.CalmSpeedJitter, settings.CalmSpeedJitter));
            agent.Personality.PanicSpeed = Math.Max(1,
                Between(scenario.Panic.SpeedMinimum, scenario.Panic.SpeedMaximum, speed) +
                random.NextIntInclusive(-settings.PanicSpeedJitter, settings.PanicSpeedJitter));
        }

        /// <summary>How nervous minus how brave: the most fearful are dealt the freezing temperaments first.</summary>
        public static int Fearfulness(AgentTraitValues traits) => traits.Nervousness - traits.Bravery;

        // ---------------------------------------------------------------- strength

        /// <summary>Effective body weight when shoving a box.</summary>
        public static long PushMassGrams(Agent agent, FireReactionScenarioData scenario)
        {
            return Scale(scenario.ObjectPhysics.AgentMassGrams, scenario.Traits.StrengthMassPercentPerPoint, agent.Traits.Strength);
        }

        /// <summary>True when <paramref name="agent"/> is so much stronger than <paramref name="other"/> that a knock-down hit only staggers them.</summary>
        public static bool ShrugsOff(Agent agent, Agent other, FireReactionScenarioData scenario)
        {
            return agent.Traits.Strength - other.Traits.Strength >= scenario.Traits.StrengthShrugOffGap;
        }

        /// <summary>
        /// Chance that a knock-down leaves this person out cold:
        /// <paramref name="basePercent"/> (already raised for a harder hit),
        /// less for the strong, never above the scenario's maximum.
        /// </summary>
        public static int PassOutChancePercent(Agent agent, FireReactionScenarioData scenario, int basePercent)
        {
            int chance = basePercent - scenario.Traits.StrengthPassOutPercentPerPoint * FromOrdinary(agent.Traits.Strength);
            return Math.Max(0, Math.Min(scenario.Falls.PassOutMaximumPercent, chance));
        }

        /// <summary>Chance to start shoving a locked door rather than give up at once.</summary>
        public static int DoorForceChancePercent(Agent agent, FireReactionScenarioData scenario)
        {
            return Percent(scenario.Exits.DoorForceChancePercent +
                           scenario.Traits.StrengthForceChancePerPoint * FromOrdinary(agent.Traits.Strength));
        }

        /// <summary>
        /// Getting out of a chair: the nervous are out of it fastest, the
        /// placid take their time, and nobody takes less than a fifth of a second.
        /// </summary>
        public static int StandUpTicks(Agent agent, FireReactionScenarioData scenario)
        {
            ItemSettings items = scenario.Items;
            return Math.Max(10, items.StandUpTicks - agent.Traits.Nervousness * items.StandUpTicksPerNervousness);
        }

        /// <summary>Damage one shove does to a locked door; zero below the minimum strength.</summary>
        public static int DoorShoveDamage(Agent agent, FireReactionScenarioData scenario)
        {
            TraitSettings settings = scenario.Traits;
            int points = agent.Traits.Strength - settings.DoorBreakMinimumStrength + 1;
            return points <= 0 ? 0 : points * settings.DoorDamagePerPoint;
        }

        /// <summary>The heaviest item this person can lift and carry.</summary>
        public static long CarryLimitGrams(Agent agent, FireReactionScenarioData scenario)
        {
            ItemSettings items = scenario.Items;
            return items.CarryBaseGrams + (long)items.CarryGramsPerStrength * agent.Traits.Strength;
        }

        // ---------------------------------------------------------------- bravery

        public static int MaximumReactionDelayTicks(Agent agent, FireReactionScenarioData scenario)
        {
            return (int)Scale(scenario.Perception.MaximumReactionDelayTicks,
                -scenario.Traits.BraveryReactionDelayPercentPerPoint, agent.Traits.Bravery);
        }

        public static int DangerDistance(Agent agent, FireReactionScenarioData scenario)
        {
            return (int)Scale(scenario.Panic.DangerDistanceMillimetres,
                -scenario.Traits.BraveryDangerDistancePercentPerPoint, agent.Traits.Bravery);
        }

        // ---------------------------------------------------------------- nervousness

        public static int ShoutInterval(Agent agent, FireReactionScenarioData scenario, ref Pcg32 random)
        {
            PanicSettings panic = scenario.Panic;
            int percent = -scenario.Traits.NervousShoutIntervalPercentPerPoint;
            int nervousness = agent.Traits.Nervousness;
            return random.NextIntInclusive(
                Math.Max(1, (int)Scale(panic.ShoutMinimumTicks, percent, nervousness)),
                Math.Max(1, (int)Scale(panic.ShoutMaximumTicks, percent, nervousness)));
        }

        public static int PanicDecisionInterval(Agent agent, FireReactionScenarioData scenario, ref Pcg32 random)
        {
            PanicSettings panic = scenario.Panic;
            int percent = -scenario.Traits.NervousDecisionIntervalPercentPerPoint;
            int nervousness = agent.Traits.Nervousness;
            return random.NextIntInclusive(
                Math.Max(1, (int)Scale(panic.DecisionMinimumTicks, percent, nervousness)),
                Math.Max(1, (int)Scale(panic.DecisionMaximumTicks, percent, nervousness)));
        }

        public static int SwerveChancePercent(Agent agent, FireReactionScenarioData scenario)
        {
            if (agent.Fear.Composed || agent.Intent.SetOnAWayOut)
            {
                // Somebody walking out because a bell rang does not zig-zag, and
                // nor does anybody with a way out in front of them standing open.
                return 0;
            }

            return Percent(scenario.Panic.SwerveChancePercent +
                           scenario.Traits.NervousSwerveChancePerPoint * FromOrdinary(agent.Traits.Nervousness));
        }

        public static int HesitateChancePercent(Agent agent, FireReactionScenarioData scenario)
        {
            if (agent.Fear.Composed || agent.Intent.SetOnAWayOut)
            {
                // Nor do they stop and dither.
                return 0;
            }

            return Percent(scenario.Panic.HesitateChancePercent +
                           scenario.Traits.NervousHesitateChancePerPoint * FromOrdinary(agent.Traits.Nervousness));
        }

        /// <summary>
        /// How fast somebody heads for the way out: a sprint, or a brisk walk
        /// for whoever is keeping their head after an alarm.
        /// </summary>
        public static int FleeSpeed(Agent agent)
        {
            // A level head is a brisk walk while the fire is somebody else's
            // problem, but a door to the street standing open is worth running
            // for whoever you are.
            return agent.Fear.Composed && !agent.Intent.SetOnAWayOut
                ? agent.Personality.CalmSpeed
                : agent.Personality.PanicSpeed;
        }

        /// <summary>
        /// How far either side of the target somebody's jet wanders while they
        /// hold the trigger down. Strong hands hold it nearly straight; the weak
        /// are wrestled about by the hose, the same way the recoil walks them
        /// backwards. Never negative.
        /// </summary>
        public static int SpraySweepDegrees(Agent agent, FireReactionScenarioData scenario)
        {
            ExtinguisherSettings settings = scenario.Extinguishers;
            return Math.Max(0,
                settings.SweepDegrees - settings.SweepDegreesPerStrengthPoint * agent.Traits.Strength);
        }

        public static int TripChancePercent(Agent agent, FireReactionScenarioData scenario)
        {
            return Percent((int)Scale(scenario.Falls.TripChancePercent, scenario.Traits.NervousTripPercentPerPoint,
                agent.Traits.Nervousness));
        }

        /// <summary>
        /// Whether a bell is enough to make this person leave briskly rather
        /// than panic: brave enough, and not too nervous.
        /// </summary>
        public static bool StaysComposed(AgentTraitValues traits, FireReactionScenarioData scenario)
        {
            return traits.Bravery - traits.Nervousness >= scenario.Alarm.ComposureGap;
        }

        // ---------------------------------------------------------------- compassion and evil

        /// <summary>How hard a runner steers around people: compassion adds, evil takes away.</summary>
        public static int PanicPeopleAvoidPercent(Agent agent, FireReactionScenarioData scenario)
        {
            TraitSettings settings = scenario.Traits;
            long percent = 100L +
                           settings.CompassionAvoidPercentPerPoint * FromOrdinary(agent.Traits.Compassion) -
                           settings.EvilAvoidPercentPerPoint * FromOrdinary(agent.Traits.Evil);
            return (int)Math.Max(0L, scenario.Panic.PeopleAvoidPercent * percent / 100L);
        }

        /// <summary>The closing speed at which a runner rams someone instead of trying to dodge.</summary>
        public static int BumpMinimumSpeed(Agent agent, FireReactionScenarioData scenario)
        {
            TraitSettings settings = scenario.Traits;
            return Math.Max(settings.MinimumBumpSpeed,
                scenario.Falls.BumpMinimumSpeed +
                settings.CompassionBumpSpeedPerPoint * FromOrdinary(agent.Traits.Compassion) -
                settings.EvilBumpSpeedPerPoint * FromOrdinary(agent.Traits.Evil));
        }

        // ---------------------------------------------------------------- arithmetic

        private static int FromOrdinary(int trait) => trait - AgentTraitValues.Ordinary;

        /// <summary><paramref name="value"/> changed by <paramref name="percentPerPoint"/> for each point the trait is above 5 (never below zero).</summary>
        private static long Scale(long value, int percentPerPoint, int trait)
        {
            long percent = Math.Max(0L, 100L + (long)percentPerPoint * FromOrdinary(trait));
            return value * percent / 100L;
        }

        /// <summary>Evenly spaced between the trait-0 and trait-10 values.</summary>
        private static int Between(int atZero, int atTen, int trait)
        {
            return atZero + (atTen - atZero) * trait / AgentTraitValues.Maximum;
        }

        private static int Percent(int value) => Math.Max(0, Math.Min(100, value));
    }
}
