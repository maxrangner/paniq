using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Panic: sprint toward a door or an escape spot chosen imperfectly, keep
    /// changing your mind, swerve, hesitate, drift with the people running
    /// near you, and bolt straight away if the fire gets close. Nobody stays
    /// pinned against a wall: being blocked forces a new decision. Frozen
    /// people stand and stare until (or unless) they snap out of it.
    /// </summary>
    internal sealed class PanicBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly BodySystem body;
        private readonly DoorBehaviour doorBehaviour;
        private readonly HelpBehaviour help;
        private readonly Locomotion locomotion;
        private readonly PanicSettings settings;

        public PanicBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            FireSystem fire,
            FearSystem fear,
            SoundSystem sound,
            BodySystem body,
            DoorBehaviour doorBehaviour,
            HelpBehaviour help,
            Locomotion locomotion)
        {
            this.help = help;
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.fire = fire;
            this.fear = fear;
            this.sound = sound;
            this.body = body;
            this.doorBehaviour = doorBehaviour;
            this.locomotion = locomotion;
            settings = context.Scenario.Panic;
        }

        /// <summary>
        /// This tick's panicked decision. Returns no intent when the person
        /// tripped while deciding and is already on the floor.
        /// </summary>
        public MotorIntent? Decide(Agent agent)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            long fireDistanceSquared = fire.NearestDistanceSquared(agent.Body.Position, out LogicalPosition firePoint);
            long danger = TraitEffects.DangerDistance(agent, context.Scenario);
            bool inDanger = fireDistanceSquared < danger * danger;

            if (intent.Activity == AgentActivityState.Frozen)
            {
                if (!ShouldUnfreeze(agent, inDanger))
                {
                    // Rooted to the spot, staring at the fire.
                    int stare = fireDistanceSquared < long.MaxValue
                        ? IntegerMath.HeadingBetween(agent.Body.Position, firePoint, agent.Body.Heading)
                        : agent.Body.Heading;
                    return new MotorIntent(stare, 0, agent.Personality.CalmTurnRate, settings.Acceleration);
                }

                fear.Unfreeze(agent);
            }

            if (tick >= agent.Fear.NextShoutTick)
            {
                sound.Yell(agent, agent.Fear.ScaredEventId);
                agent.Fear.NextShoutTick = checked(tick + TraitEffects.ShoutInterval(agent, context.Scenario, ref context.Random));
            }

            MotorIntent? helping = help.Decide(agent, inDanger);
            if (helping.HasValue)
            {
                return helping.Value;
            }

            int room = geometry.RoomAt(agent.Body.Position);
            if (room >= 0 && !DoorBehaviour.IsAtDoor(agent))
            {
                // Fire coming through the door of the room they are in.
                doorBehaviour.ConsiderClosingAgainstFire(agent, room);
            }

            if (DoorBehaviour.IsAtDoor(agent))
            {
                // Worked out first: giving up forgets which door this was.
                MotorIntent faceDoor = doorBehaviour.FaceDoor(agent);
                if (doorBehaviour.UpdateAttempt(agent, inDanger))
                {
                    return faceDoor;
                }
            }

            bool leaving = doorBehaviour.IsLeaving(agent);
            if (leaving && intent.Activity == AgentActivityState.Hesitating)
            {
                intent.Activity = AgentActivityState.Fleeing;
            }

            if (intent.Activity == AgentActivityState.Hesitating)
            {
                if (tick < intent.ActivityEndTick && !inDanger)
                {
                    // Frozen for a split second, looking for a way out.
                    return LookIntent(agent);
                }

                intent.Activity = AgentActivityState.Fleeing;
                DecideMove(agent, false);
            }
            else if (doorBehaviour.HasReachedClosedExit(agent))
            {
                doorBehaviour.StartAttempt(agent);
                return doorBehaviour.FaceDoor(agent);
            }
            else if (agent.Body.BlockedTicks >= settings.BlockedGiveUpTicks &&
                     !(leaving && geometry.RoomAt(agent.Body.Position) < 0))
            {
                // Wedged beside an open door: stand aside for whoever is lined up with it.
                // Otherwise stuck in the crowd: if it was on the way to a door, try another one for a while.
                if (!doorBehaviour.TryGiveWay(agent))
                {
                    doorBehaviour.AvoidCrowdedExit(agent);
                    DecideMove(agent, false);
                }
            }
            else if (!leaving &&
                     (tick >= intent.NextPanicDecisionTick ||
                      (agent.Doors.ExitDoorIndex < 0 &&
                       LogicalPosition.DistanceSquared(agent.Body.Position, intent.Target) <
                       (long)settings.ArrivalDistanceMillimetres * settings.ArrivalDistanceMillimetres)))
            {
                DecideMove(agent, !inDanger);
            }

            if (agent.Body.State != AgentBodyState.Upright)
            {
                // Tripped while deciding.
                return null;
            }

            if (intent.Activity == AgentActivityState.Hesitating)
            {
                return LookIntent(agent);
            }

            if (agent.Doors.ExitDoorIndex >= 0)
            {
                intent.Target = doorBehaviour.DoorTarget(agent);
            }

            int goalHeading;
            bool nearExit = doorBehaviour.IsNearExit(agent, context.Scenario.Exits.NoSwerveDistanceMillimetres);
            int swerve = tick < intent.SwerveEndTick && !nearExit ? intent.SwerveOffset : 0;
            if (inDanger && fireDistanceSquared > 0L && !leaving)
            {
                // Too close: run directly away from the nearest flames.
                goalHeading = IntegerMath.HeadingBetween(firePoint, agent.Body.Position, agent.Body.Heading) + swerve / 2;
            }
            else
            {
                goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, intent.Target, agent.Body.Heading) + swerve;
            }

            long followX = 0L;
            long followZ = 0L;
            if (!nearExit)
            {
                FollowNearbyRunners(agent, out followX, out followZ);
            }

            goalHeading = locomotion.Steer(agent, goalHeading, TraitEffects.PanicPeopleAvoidPercent(agent, context.Scenario),
                settings.WallAvoidPercent, settings.ObjectAvoidPercent, followX, followZ);
            return new MotorIntent(goalHeading, agent.Personality.PanicSpeed, agent.Personality.PanicTurnRate, settings.Acceleration);
        }

        private MotorIntent LookIntent(Agent agent)
        {
            return new MotorIntent(agent.Intent.LookHeading, 0, agent.Personality.PanicTurnRate, settings.Acceleration);
        }

        private bool ShouldUnfreeze(Agent agent, bool inDanger)
        {
            return agent.Personality.Temperament == AgentPanicTemperament.FreezeThenRun &&
                   (context.Tick >= agent.Fear.FreezeEndTick || inDanger);
        }

        /// <summary>
        /// A fresh panicked decision: maybe trip over your own feet, maybe
        /// freeze for a split second, otherwise pick a door or an escape spot
        /// and maybe swerve.
        /// </summary>
        private void DecideMove(Agent agent, bool mayHesitate)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            agent.Body.BlockedTicks = 0;
            intent.NextPanicDecisionTick = checked(tick + TraitEffects.PanicDecisionInterval(agent, context.Scenario, ref context.Random));

            FallSettings falls = context.Scenario.Falls;
            if (agent.Body.Speed >= falls.TripMinimumSpeed)
            {
                // Zig-zagging makes a stumble twice as likely.
                int chance = TraitEffects.TripChancePercent(agent, context.Scenario) * (tick < intent.SwerveEndTick ? 2 : 1);
                if (context.Random.NextPercent(chance))
                {
                    body.Trip(agent, agent.Fear.ScaredEventId);
                    return;
                }
            }

            if (mayHesitate && context.Random.NextPercent(TraitEffects.HesitateChancePercent(agent, context.Scenario)))
            {
                intent.Activity = AgentActivityState.Hesitating;
                intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(
                    settings.HesitateMinimumTicks,
                    settings.HesitateMaximumTicks));
                int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                intent.LookHeading = IntegerMath.NormalizeDegrees(agent.Body.Heading + side * context.Random.NextIntInclusive(40, 150));
                return;
            }

            intent.Activity = AgentActivityState.Fleeing;
            agent.Doors.ExitDoorIndex = doorBehaviour.ChooseExitDoor(agent);
            intent.Target = agent.Doors.ExitDoorIndex >= 0 ? doorBehaviour.DoorTarget(agent) : ChooseEscapeTarget(agent);
            if (context.Random.NextPercent(TraitEffects.SwerveChancePercent(agent, context.Scenario)))
            {
                int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                intent.SwerveOffset = side * context.Random.NextIntInclusive(settings.SwerveAngleMinimum, settings.SwerveAngleMaximum);
                intent.SwerveEndTick = checked(tick + context.Random.NextIntInclusive(
                    settings.SwerveMinimumTicks,
                    settings.SwerveMaximumTicks));
            }
        }

        /// <summary>
        /// Samples spots in the room they are in and scores them: far from
        /// fire is good, a route that brushes past the fire is bad, a U-turn
        /// is a little bad, and random noise keeps the choice human and
        /// imperfect.
        /// </summary>
        private LogicalPosition ChooseEscapeTarget(Agent agent)
        {
            LogicalPosition position = agent.Body.Position;
            LogicalPosition best = position;
            long bestScore = long.MinValue;
            int room = geometry.RoomOf(agent);
            for (int sample = 0; sample < settings.EscapeSampleCount; sample++)
            {
                LogicalPosition candidate = geometry.RandomInteriorPoint(room, settings.EscapeWallMarginMillimetres);
                long score = context.Random.NextIntInclusive(0, settings.EscapeNoiseMillimetres);

                long fireDistanceSquared = fire.NearestDistanceSquared(candidate);
                score += fireDistanceSquared == long.MaxValue ? 20000L : IntegerMath.Sqrt(fireDistanceSquared);

                if (fire.RoutePassesNear(position, candidate, settings.EscapeRouteClearanceMillimetres))
                {
                    score -= settings.EscapeRoutePenaltyMillimetres;
                }

                if (geometry.RouteCrossesTable(position, candidate))
                {
                    score -= settings.TableRoutePenaltyMillimetres;
                }

                long hopSquared = LogicalPosition.DistanceSquared(position, candidate);
                if (hopSquared < (long)settings.EscapeShortHopDistanceMillimetres * settings.EscapeShortHopDistanceMillimetres)
                {
                    score -= settings.EscapeShortHopPenaltyMillimetres;
                }

                int turn = Math.Abs(IntegerMath.SignedAngleDifference(
                    agent.Body.Heading,
                    IntegerMath.HeadingBetween(position, candidate, agent.Body.Heading)));
                score -= turn * settings.EscapeTurnPenaltyPerDegree;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Average running direction of nearby panicking people, as a weighted pull.</summary>
        private void FollowNearbyRunners(Agent agent, out long followX, out long followZ)
        {
            followX = 0L;
            followZ = 0L;
            long radius = settings.FollowRadiusMillimetres;
            if (radius <= 0L)
            {
                return;
            }

            long radiusSquared = radius * radius;
            int count = 0;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                if (other == agent ||
                    !other.IsParticipating ||
                    other.Fear.State != AgentFearState.Scared ||
                    other.Body.Speed <= 0 ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, other.Body.Position) > radiusSquared)
                {
                    continue;
                }

                LogicalPosition direction = IntegerMath.Direction(other.Body.Heading);
                followX += direction.X;
                followZ += direction.Z;
                count++;
            }

            if (count > 0)
            {
                followX = followX / count * settings.FollowPercent / 100L;
                followZ = followZ / count * settings.FollowPercent / 100L;
            }
        }
    }
}
