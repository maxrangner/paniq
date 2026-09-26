using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// What a person notices each tick before deciding anything: a threat in
    /// their vision cone, somebody bolting in their vision cone, a threat
    /// they can hear nearby, and their own reaction timer running out. It
    /// asks <see cref="Threats"/>, never the fire by name, so a person is
    /// frightened of whatever the level holds; and fear spreads by sight as
    /// well as by voice, because the person opposite leaping up is a fright
    /// of its own before you know what they saw.
    /// </summary>
    internal sealed class PerceptionSystem
    {
        private readonly SimulationContext context;
        private readonly Threats threats;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PerceptionSettings settings;

        public PerceptionSystem(SimulationContext context, Threats threats, FearSystem fear, SoundSystem sound, Crowd crowd,
            WorldGeometry geometry)
        {
            this.context = context;
            this.threats = threats;
            this.fear = fear;
            this.sound = sound;
            this.crowd = crowd;
            this.geometry = geometry;
            settings = context.Scenario.Perception;
        }

        public void Update(Agent agent)
        {
            // Seeing the danger needs a danger to see; being startled by
            // somebody bolting, and a startle turning into fright, do not.
            // Before 2026-09-25 nothing here ran until the fire existed, so
            // an alarm bell pulled before it left everybody standing
            // startled, facing the bell, until the flames came.
            bool anyDanger = threats.AnyActive;
            bool enteredAlert = false;
            if (agent.Fear.State == AgentFearState.Calm)
            {
                if (anyDanger && SeesDanger(agent, out ulong seenRoot))
                {
                    // Seeing danger: startled, and yelling about it.
                    ulong alertEventId = fear.StartAlert(agent, seenRoot, AgentAlertSource.Visual);
                    sound.Yell(agent, alertEventId);
                    enteredAlert = true;
                }
                else if (SeesSomeoneBolting(agent, out Agent runner))
                {
                    // Rattled, it takes more nerve to look before running.
                    int nerve = settings.BraveLookFirstMinimum +
                                (agent.Fear.IsRattledAt(context.Tick) ? context.Scenario.Calming.RattledBraveryPenalty : 0);
                    if (agent.Traits.Bravery >= nerve)
                    {
                        // The brave look up first. What they see next -- the
                        // flames, or the shout -- decides whether they run.
                        if (agent.Intent.Activity != AgentActivityState.Investigating)
                        {
                            sound.LookToward(agent, runner.Body.Position, runner.Fear.ScaredEventId);
                        }
                    }
                    else
                    {
                        fear.Alarm(agent, runner.Fear.ScaredEventId, AgentAlertSource.SawSomeoneRun, runner.Body.Position);
                        enteredAlert = true;
                    }
                }

                if (agent.Fear.State == AgentFearState.Calm && anyDanger)
                {
                    // Also while looking toward some other noise: a fire
                    // crackling is not drowned out by a thud in the next room.
                    sound.HearThreats(agent);
                }
            }

            if (agent.Fear.State != AgentFearState.Alert)
            {
                return;
            }

            if (!enteredAlert && anyDanger && agent.Fear.AlertSource != AgentAlertSource.Visual &&
                SeesDanger(agent, out ulong root))
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

        /// <summary>
        /// The nearest frightened person in sight who is plainly bolting:
        /// running, or leaping up out of a chair, inside the watcher's vision
        /// cone and along a line of sight that runs through open doorways
        /// only.
        /// </summary>
        private bool SeesSomeoneBolting(Agent agent, out Agent runner)
        {
            runner = null;
            long range = settings.BoltSightRangeMillimetres;
            if (range <= 0L)
            {
                return false;
            }

            LogicalPosition eye = agent.Body.Position;
            int eyeRoom = geometry.RoomAtPoint(eye);
            LogicalPosition facing = IntegerMath.Direction(agent.Body.Heading);
            long rangeSquared = range * range;
            long nearest = long.MaxValue;
            using Crowd.Nearby people = crowd.Within(eye, range);
            for (int i = 0; i < people.Count; i++)
            {
                Agent other = crowd.All[people[i]];
                if (other == agent || !other.IsParticipating || other.Fear.State != AgentFearState.Scared ||
                    other.Body.State != AgentBodyState.Upright)
                {
                    continue;
                }

                bool bolting = other.Sitting.Phase == SitPhase.LeapingUp ||
                               (other.Intent.Activity == AgentActivityState.Fleeing && other.Body.Speed >= settings.BoltSpeedMinimum);
                if (!bolting)
                {
                    continue;
                }

                long distanceSquared = LogicalPosition.DistanceSquared(eye, other.Body.Position);
                if (distanceSquared > rangeSquared || distanceSquared >= nearest ||
                    !InVisionCone(eye, facing, other.Body.Position) ||
                    !geometry.CanSeeBetween(eyeRoom, eye, geometry.RoomAtPoint(other.Body.Position), other.Body.Position))
                {
                    continue;
                }

                nearest = distanceSquared;
                runner = other;
            }

            return runner != null;
        }

        /// <summary>A 45-degree half-angle either side of the way they face: |sideways| is at most forward.</summary>
        private static bool InVisionCone(LogicalPosition eye, LogicalPosition direction, LogicalPosition point)
        {
            long offsetX = (long)point.X - eye.X;
            long offsetZ = (long)point.Z - eye.Z;
            long forward = checked(offsetX * direction.X + offsetZ * direction.Z);
            long lateral = checked(offsetX * direction.Z - offsetZ * direction.X);
            return forward >= 0L && Math.Abs(lateral) <= forward;
        }
    }
}
