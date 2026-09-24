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
    internal sealed class PanicBehaviour : IBindable
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly Threats threats;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly BodySystem body;
        private readonly DoorBehaviour doorBehaviour;
        private readonly HelpBehaviour help;
        private readonly ChairBehaviour chairs;
        private readonly ExitSignBehaviour exitSigns;

        /// <summary>
        /// The things somebody might do instead of running, in the order they
        /// are offered: the first that answers wins. Set once the whole cast of
        /// behaviours exists, because each needs the others around it.
        /// </summary>
        private IPanicOption[] options = new IPanicOption[0];
        private readonly Locomotion locomotion;
        private readonly PanicSettings settings;

        public PanicBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            Threats threats,
            FearSystem fear,
            SoundSystem sound,
            BodySystem body,
            DoorBehaviour doorBehaviour,
            HelpBehaviour help,
            ChairBehaviour chairs,
            ExitSignBehaviour exitSigns,
            Locomotion locomotion)
        {
            this.help = help;
            this.chairs = chairs;
            this.exitSigns = exitSigns;
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.threats = threats;
            this.fear = fear;
            this.sound = sound;
            this.body = body;
            this.doorBehaviour = doorBehaviour;
            this.locomotion = locomotion;
            settings = context.Scenario.Panic;
        }

        /// <summary>
        /// What a frightened person might do instead of running, in the order
        /// they consider it. The first that answers wins, so this list is the
        /// priority order, and it is the only place it is written down.
        /// Raising the alarm comes after helping so that somebody with an
        /// unconscious person in front of them sees to them rather than
        /// walking off to the bell; plenty of other people are free to hit it.
        /// Bound once everything exists, because each needs the others.
        /// </summary>
        public void Bind(Systems systems)
        {
            objects = systems.Objects;
            options = new IPanicOption[]
            {
                systems.Leaders, systems.Extinguishers, systems.Help, systems.AlarmBehaviour, systems.Barricades
            };
        }

        /// <summary>The loose things and the tables, for heaving a table out of the way.</summary>
        private PhysicsObjectSystem objects;

        /// <summary>
        /// Stuck with a table between them and where they are going: they
        /// heave it out of the way, which sends a light one over and shifts a
        /// heavy one. Self-preservation, so anybody does it, and it costs
        /// them a moment before they can do it again. True when they did.
        /// </summary>
        private bool TryHeaveTable(Agent agent)
        {
            LogicalPosition position = agent.Body.Position;
            if (objects == null || agent.Body.State != AgentBodyState.Upright ||
                context.Tick < agent.Intent.NextTableHeaveTick)
            {
                return false;
            }

            // The way they are facing, which is the way they have been
            // pushing: somebody bolting from flames faces away from them,
            // whatever spot they last decided to make for.
            int heading = agent.Body.Heading;
            LogicalPosition ahead = position + IntegerMath.Displacement(heading, bodyRadius + ArmsReachMillimetres);
            int table = geometry.TableAt(ahead, bodyRadius);
            if (table < 0)
            {
                return false;
            }

            objects.HeaveTable(agent, table, heading, agent.Fear.ScaredEventId);
            agent.Intent.NextTableHeaveTick = checked(context.Tick + settings.TableHeaveRestTicks);
            agent.Body.BlockedTicks = 0;
            return true;
        }

        /// <summary>How far in front of their body somebody's hands reach when they heave at a table.</summary>
        private const int ArmsReachMillimetres = 300;

        /// <summary>
        /// This tick's panicked decision. Returns no intent when the person
        /// tripped while deciding and is already on the floor.
        /// </summary>
        public MotorIntent? Decide(Agent agent)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            long fireDistanceSquared = threats.NearestDistanceSquared(agent.Body.Position, out LogicalPosition firePoint, out _);
            long danger = TraitEffects.DangerDistance(agent, context.Scenario);
            bool inDanger = fireDistanceSquared < danger * danger;
            if (inDanger)
            {
                // The fire is on them now: whatever composure the bell left them
                // with is gone.
                fear.BreakComposure(agent);
            }

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

            // Set on a way out they can see standing open, right now, and not
            // otherwise occupied. While this is true they stop dithering.
            // Worked out after the fire and the frozen have had their say, so
            // neither is overruled by it.
            bool eager = doorBehaviour.IsSetOnAWayOut(agent) && !inDanger && !agent.Burning.IsBurning &&
                         agent.Body.State == AgentBodyState.Upright;
            intent.SetOnAWayOut = eager;

            if (tick >= agent.Fear.NextShoutTick)
            {
                sound.Yell(agent, agent.Fear.ScaredEventId);
                agent.Fear.NextShoutTick = checked(tick + TraitEffects.ShoutInterval(agent, context.Scenario, ref context.Random));
            }

            if (agent.Sitting.OnIt || agent.Sitting.Phase == SitPhase.LeapingUp)
            {
                // Still in a chair: they have to get out of it first. They come
                // up on the spot and the chair goes over behind them, so nobody
                // is shoved backwards out of their seat before they run.
                chairs.StartLeapingUp(agent);
                if (chairs.UpdateLeapingUp(agent))
                {
                    return PanicIntent.StandAndFace(agent, agent.Body.Heading, settings);
                }

                intent.Activity = AgentActivityState.Fleeing;
            }

            // Anything they would rather be doing than running, in order.
            for (int i = 0; i < options.Length; i++)
            {
                MotorIntent? instead = options[i].Decide(agent, inDanger, eager);
                if (instead.HasValue)
                {
                    return instead.Value;
                }
            }

            int room = geometry.RoomAt(agent.Body.Position);
            if (room >= 0 && !DoorBehaviour.IsAtDoor(agent))
            {
                // Fire coming through the door of the room they are in.
                doorBehaviour.ConsiderClosingAgainstFire(agent, room);
            }

            // Running for a way out through the heat: the flames at their
            // danger distance neither turn them back nor make them abandon
            // the door.
            bool dashing = doorBehaviour.IsDashing(agent);

            if (DoorBehaviour.IsAtDoor(agent))
            {
                // Worked out first: giving up forgets which door this was.
                MotorIntent faceDoor = doorBehaviour.FaceDoor(agent);
                if (doorBehaviour.UpdateAttempt(agent, inDanger && !dashing))
                {
                    return faceDoor;
                }
            }

            if (doorBehaviour.HasPassedThrough(agent))
            {
                // Through the door they were running for: the next leg of the
                // way out is worked out from the room they are standing in now.
                context.ThinkAgainSoon(intent);
            }

            bool leaving = doorBehaviour.IsLeaving(agent);
            if ((leaving || eager) && intent.Activity == AgentActivityState.Hesitating)
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
                     !(leaving && geometry.RoomAt(agent.Body.Position) < 0 &&
                       agent.Body.BlockedTicks < settings.BlockedGiveUpTicks * 2))
            {
                // A thing in the way: grab it and throw it clear.
                // A table in the way: heave it over or along.
                // Wedged beside an open door: stand aside for whoever is lined up with it.
                // Otherwise stuck in the crowd: if it was on the way to a door, try another one for a while.
                if (!doorBehaviour.TryClearTheWay(agent) && !TryHeaveTable(agent) && !doorBehaviour.TryGiveWay(agent))
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
            int swerve = tick < intent.SwerveEndTick && !nearExit && !eager ? intent.SwerveOffset : 0;
            if (inDanger && fireDistanceSquared > 0L && !leaving && !dashing)
            {
                // Too close: run directly away from the nearest flames.
                goalHeading = IntegerMath.HeadingBetween(firePoint, agent.Body.Position, agent.Body.Heading) + swerve / 2;
            }
            else
            {
                // Round whatever is in the way rather than straight at it.
                goalHeading = geometry.Routes.HeadingToward(
                    agent.Body.Position, intent.Target, bodyRadius, agent.Body.Heading) + swerve;
            }

            long followX = 0L;
            long followZ = 0L;
            if (!nearExit && !eager)
            {
                FollowNearbyRunners(agent, out followX, out followZ);
            }

            goalHeading = locomotion.Steer(agent, goalHeading, TraitEffects.PanicPeopleAvoidPercent(agent, context.Scenario),
                settings.WallAvoidPercent, settings.ObjectAvoidPercent, followX, followZ, settings.TableAvoidPercent);
            return PanicIntent.WalkTowards(agent, goalHeading, settings);
        }

        private MotorIntent LookIntent(Agent agent)
        {
            return PanicIntent.StandAndFace(agent, agent.Intent.LookHeading, settings);
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

            // A door; or, for a visitor looking for a way out, the spot to look
            // round this room from; or, with nothing better, a spot to run to.
            intent.Target = agent.Doors.ExitDoorIndex >= 0 ? doorBehaviour.DoorTarget(agent)
                : agent.Knowledge.HasSearchSpot ? agent.Knowledge.SearchSpot
                : ChooseEscapeTarget(agent);
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
        /// fire is good, the way a sign they can see points is good, a route
        /// that brushes past the fire is bad, a U-turn is a little bad, and
        /// random noise keeps the choice human and imperfect.
        /// </summary>
        private LogicalPosition ChooseEscapeTarget(Agent agent)
        {
            LogicalPosition position = agent.Body.Position;
            LogicalPosition best = position;
            long bestScore = long.MinValue;
            int room = geometry.RoomOf(agent);

            // Read the signs once, before the samples, rather than once per
            // sample: what they can see does not change between one candidate
            // spot and the next.
            bool readASign = exitSigns.TryRead(agent, out int signPointing);
            for (int sample = 0; sample < settings.EscapeSampleCount; sample++)
            {
                LogicalPosition candidate = geometry.RandomInteriorPoint(room, settings.EscapeWallMarginMillimetres);
                long score = context.Random.NextIntInclusive(0, settings.EscapeNoiseMillimetres);

                long fireDistanceSquared = threats.NearestDistanceSquared(candidate, out _, out _);
                score += fireDistanceSquared == long.MaxValue ? 20000L : IntegerMath.Sqrt(fireDistanceSquared);

                if (threats.RoutePassesNear(position, candidate, settings.EscapeRouteClearanceMillimetres))
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

                if (readASign)
                {
                    // They have been told which way the door is, and they
                    // believe it: a spot the sign's way is worth crossing the
                    // room for, and one the other way is worth less than where
                    // they already stand.
                    score += exitSigns.ScoreToward(position, candidate, signPointing);
                }

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
            using Crowd.Nearby neighbours = crowd.Within(agent.Body.Position, radius);
            for (int i = 0; i < neighbours.Count; i++)
            {
                Agent other = crowd.All[neighbours[i]];
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
