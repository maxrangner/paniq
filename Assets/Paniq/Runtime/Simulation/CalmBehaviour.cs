using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Calm "loitering": each person makes their own small decisions every
    /// few seconds - stroll somewhere, stand, look around, or wander over to
    /// stand near someone - using the seeded generator and their own seeded
    /// pace and turn rate. Also turning toward, and edging toward, a noise,
    /// now and then tidying an item away (see <see cref="ItemBehaviour"/>),
    /// and now and then sitting down on a chair (see <see cref="ChairBehaviour"/>).
    /// </summary>
    internal sealed class CalmBehaviour
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly Locomotion locomotion;
        private readonly ItemBehaviour items;
        private readonly ChairBehaviour chairs;
        private readonly CalmSettings settings;

        public CalmBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            Locomotion locomotion,
            ItemBehaviour items,
            ChairBehaviour chairs)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.locomotion = locomotion;
            this.items = items;
            this.chairs = chairs;
            settings = context.Scenario.Calm;
        }

        public MotorIntent Decide(Agent agent)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            int goalHeading = agent.Body.Heading;
            int goalSpeed = 0;
            int turnRate = agent.Personality.CalmTurnRate;
            bool steer = false;

            switch (intent.Activity)
            {
                case AgentActivityState.Investigating:
                    if (tick >= intent.ActivityEndTick || agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
                    {
                        agent.Hearing.HasSoundPoint = false;
                        if (agent.Sitting.OnIt)
                        {
                            // Looked round from the chair and saw nothing: back to the table.
                            chairs.ResumeSitting(agent);
                            goalHeading = agent.Intent.LookHeading;
                            break;
                        }

                        ChooseActivity(agent, true);
                        break;
                    }

                    // A startled head turns fast.
                    Investigate(agent, out goalHeading, out goalSpeed);
                    turnRate = agent.Personality.PanicTurnRate;
                    steer = goalSpeed > 0;
                    break;

                case AgentActivityState.Standing:
                    if (TryGetPartner(agent, out Agent standingPartner))
                    {
                        goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, standingPartner.Body.Position, agent.Body.Heading);
                    }

                    if (tick >= intent.ActivityEndTick)
                    {
                        ChooseActivity(agent, false);
                    }

                    break;

                case AgentActivityState.LookingAround:
                    goalHeading = intent.LookHeading;
                    if (tick >= intent.ActivityEndTick)
                    {
                        if (intent.LooksRemaining > 0)
                        {
                            NextGlance(agent);
                        }
                        else
                        {
                            ChooseActivity(agent, false);
                        }
                    }

                    break;

                case AgentActivityState.Strolling:
                {
                    long distance = IntegerMath.Distance(agent.Body.Position, intent.Target);
                    if (distance < settings.StrollArrivalDistanceMillimetres || tick >= intent.ActivityEndTick ||
                        agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
                    {
                        ChooseActivity(agent, true);
                        break;
                    }

                    if (tick >= intent.NextWanderTick)
                    {
                        intent.WanderOffset = context.Random.NextIntInclusive(-settings.WanderMaximumDegrees, settings.WanderMaximumDegrees);
                        intent.NextWanderTick = checked(tick + context.Random.NextIntInclusive(25, 60));
                    }

                    // Wander less as the destination gets close, so arrival
                    // looks deliberate.
                    int wander = distance < 1200 ? intent.WanderOffset / 2 : intent.WanderOffset;
                    goalHeading = geometry.Routes.HeadingToward(
                        agent.Body.Position, intent.Target, bodyRadius, agent.Body.Heading) + wander;
                    int calmSpeed = agent.Personality.CalmSpeed;
                    goalSpeed = distance < settings.StrollSlowdownDistanceMillimetres
                        ? Math.Max(calmSpeed / 3, (int)(calmSpeed * distance / settings.StrollSlowdownDistanceMillimetres))
                        : calmSpeed;
                    steer = true;
                    break;
                }

                case AgentActivityState.Socialising:
                {
                    if (!TryGetPartner(agent, out Agent partner) ||
                        tick >= intent.ActivityEndTick || agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
                    {
                        intent.SocialPartnerIndex = -1;
                        ChooseActivity(agent, true);
                        break;
                    }

                    long distance = IntegerMath.Distance(agent.Body.Position, partner.Body.Position);
                    if (distance <= settings.SocialStopDistanceMillimetres)
                    {
                        // Arrived: stand and face them for a while.
                        intent.Activity = AgentActivityState.Standing;
                        intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(150, 400));
                        break;
                    }

                    int calmSpeed = agent.Personality.CalmSpeed;
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, partner.Body.Position, agent.Body.Heading);
                    goalSpeed = distance < settings.SocialStopDistanceMillimetres + settings.StrollSlowdownDistanceMillimetres
                        ? Math.Max(calmSpeed / 3, calmSpeed / 2)
                        : calmSpeed;
                    steer = true;
                    break;
                }

                case AgentActivityState.GoingToSit:
                case AgentActivityState.Sitting:
                case AgentActivityState.StandingUp:
                    if (chairs.UpdateSitting(agent, out goalHeading, out goalSpeed))
                    {
                        steer = goalSpeed > 0;
                        break;
                    }

                    ChooseActivity(agent, true);
                    break;

                case AgentActivityState.FetchingItem:
                case AgentActivityState.PickingUp:
                case AgentActivityState.CarryingItem:
                case AgentActivityState.SettingDown:
                    if (items.UpdateTidying(agent, out goalHeading, out goalSpeed))
                    {
                        steer = goalSpeed > 0;
                        break;
                    }

                    ChooseActivity(agent, true);
                    break;

                default:
                    // Coming back to calm from another state is not possible
                    // in this prototype, but choose afresh if it ever happens.
                    ChooseActivity(agent, false);
                    break;
            }

            if (steer)
            {
                goalHeading = locomotion.Steer(agent, goalHeading,
                    settings.PeopleAvoidPercent, settings.WallAvoidPercent, settings.ObjectAvoidPercent,
                    tableAvoidPercent: settings.TableAvoidPercent);
            }

            return new MotorIntent(goalHeading, goalSpeed, turnRate, settings.Acceleration);
        }

        /// <summary>
        /// Investigating a noise: turn quickly toward it, then, if it is still
        /// some way off, edge toward it at half walking pace.
        /// </summary>
        private void Investigate(Agent agent, out int goalHeading, out int goalSpeed)
        {
            HearingSettings hearing = context.Scenario.Hearing;
            goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, agent.Hearing.SoundPoint, agent.Body.Heading);
            goalSpeed = 0;

            // From a chair they only turn to look; nobody edges off across the
            // room while still sitting in it.
            if (agent.Sitting.OnIt)
            {
                return;
            }

            int facingError = Math.Abs(IntegerMath.SignedAngleDifference(agent.Body.Heading, goalHeading));
            if (context.Tick - agent.Hearing.InvestigateStartTick >= hearing.InvestigateCreepDelayTicks &&
                facingError <= hearing.InvestigateCreepMaximumTurn &&
                IntegerMath.Distance(agent.Body.Position, agent.Hearing.SoundPoint) > hearing.InvestigateCreepDistanceMillimetres)
            {
                goalSpeed = agent.Personality.CalmSpeed / 2;
            }
        }

        private void ChooseActivity(Agent agent, bool justMoved)
        {
            agent.Body.BlockedTicks = 0;
            int roll = context.Random.NextIntInclusive(0, 99);
            if (justMoved)
            {
                // After walking somewhere, people usually stop for a moment.
                if (roll < 55)
                {
                    StartStanding(agent);
                }
                else
                {
                    StartLookingAround(agent);
                }

                return;
            }

            agent.Intent.SocialPartnerIndex = -1;
            if (roll < context.Scenario.Items.TidyChancePercent && items.TryStartTidying(agent))
            {
                return;
            }

            // The same roll decides both, so sitting takes the band just above tidying.
            if (roll < context.Scenario.Items.TidyChancePercent + context.Scenario.Items.SitChancePercent &&
                chairs.TryStartSitting(agent))
            {
                return;
            }

            if (roll < 50)
            {
                StartStroll(agent);
            }
            else if (roll < 75)
            {
                if (!TryStartSocialising(agent))
                {
                    StartStroll(agent);
                }
            }
            else if (agent.Intent.Activity != AgentActivityState.LookingAround)
            {
                StartLookingAround(agent);
            }
            else
            {
                StartStroll(agent);
            }
        }

        private void StartStanding(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Standing;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.DecisionMinimumTicks,
                settings.DecisionMaximumTicks));
        }

        private void StartLookingAround(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.LookingAround;
            agent.Intent.LooksRemaining = context.Random.NextIntInclusive(1, 3);
            NextGlance(agent);
        }

        private void NextGlance(Agent agent)
        {
            agent.Intent.LooksRemaining--;
            int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
            agent.Intent.LookHeading = IntegerMath.NormalizeDegrees(agent.Body.Heading + side * context.Random.NextIntInclusive(35, 120));
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(30, 70));
        }

        private void StartStroll(Agent agent)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            intent.Activity = AgentActivityState.Strolling;
            intent.ActivityEndTick = checked(tick + settings.StrollTimeoutTicks);
            intent.WanderOffset = context.Random.NextIntInclusive(-settings.WanderMaximumDegrees, settings.WanderMaximumDegrees);
            intent.NextWanderTick = checked(tick + context.Random.NextIntInclusive(25, 60));

            int here = geometry.RoomOf(agent);
            int strollTo = ChooseRoomToStrollTo(agent, here);
            long minimumSquared = (long)settings.StrollMinimumDistanceMillimetres * settings.StrollMinimumDistanceMillimetres;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                intent.Target = geometry.RandomInteriorPoint(strollTo, settings.StrollWallMarginMillimetres);
                if (strollTo != here || LogicalPosition.DistanceSquared(agent.Body.Position, intent.Target) >= minimumSquared)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Where to wander: this room, or through an open door into the next
        /// one. Somewhere else only now and then, so a room does not empty
        /// itself; and the door they would walk through is remembered, because
        /// a doorway is a wall to anybody with no reason to be in it.
        ///
        /// Before this, calm people could not leave the room they started in
        /// at all, and the building read as a set of sealed boxes rather than
        /// one place people lived in.
        /// </summary>
        private int ChooseRoomToStrollTo(Agent agent, int here)
        {
            agent.Doors.StrollDoorIndex = -1;
            if (here < 0)
            {
                return System.Math.Max(0, here);
            }

            int[] doorsHere = geometry.RoomDoors(here);
            if (settings.StrollNextDoorPercent <= 0 || doorsHere.Length == 0)
            {
                // Checked before drawing, so that turning this off leaves the
                // run's random numbers exactly as they were without it. A test
                // about something else can then switch it off and be sure it
                // has changed nothing but this.
                return here;
            }

            int roll = context.Random.NextIntInclusive(0, 99);
            if (roll >= settings.StrollNextDoorPercent)
            {
                return here;
            }

            // One of the open doorways out of this room, chosen by the same roll.
            int chosen = -1;
            int seen = 0;
            for (int i = 0; i < doorsHere.Length; i++)
            {
                int door = doorsHere[i];
                if (!geometry.IsDoorOpen(door) || geometry.RoomBeyond(door, here) < 0)
                {
                    continue;
                }

                seen++;
                if (roll % seen == 0)
                {
                    chosen = door;
                }
            }

            if (chosen < 0)
            {
                return here;
            }

            agent.Doors.StrollDoorIndex = chosen;
            return geometry.RoomBeyond(chosen, here);
        }

        private bool TryStartSocialising(Agent agent)
        {
            long minimumSquared = (long)settings.SocialMinimumDistanceMillimetres * settings.SocialMinimumDistanceMillimetres;
            long maximumSquared = (long)settings.SocialMaximumDistanceMillimetres * settings.SocialMaximumDistanceMillimetres;
            Agent[] agents = crowd.All;

            // Only the people near enough to be worth walking over to, in the
            // same ascending order a walk of everybody would visit them in.
            using Crowd.Nearby near = crowd.Within(agent.Body.Position, settings.SocialMaximumDistanceMillimetres);
            int candidateCount = 0;
            for (int c = 0; c < near.Count; c++)
            {
                if (IsSocialCandidate(agent, agents[near[c]], minimumSquared, maximumSquared))
                {
                    candidateCount++;
                }
            }

            if (candidateCount == 0)
            {
                return false;
            }

            int pick = context.Random.NextIntInclusive(0, candidateCount - 1);
            for (int c = 0; c < near.Count; c++)
            {
                int i = near[c];
                if (!IsSocialCandidate(agent, agents[i], minimumSquared, maximumSquared))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    agent.Intent.SocialPartnerIndex = i;
                    agent.Intent.Activity = AgentActivityState.Socialising;
                    agent.Intent.ActivityEndTick = checked(context.Tick + settings.SocialTimeoutTicks);
                    return true;
                }
            }

            return false;
        }

        private static bool IsSocialCandidate(Agent agent, Agent other, long minimumSquared, long maximumSquared)
        {
            if (other == agent || !other.IsParticipating || other.Fear.State != AgentFearState.Calm)
            {
                return false;
            }

            long distanceSquared = LogicalPosition.DistanceSquared(agent.Body.Position, other.Body.Position);
            return distanceSquared >= minimumSquared && distanceSquared <= maximumSquared;
        }

        private bool TryGetPartner(Agent agent, out Agent partner)
        {
            partner = null;
            if (agent.Intent.SocialPartnerIndex < 0)
            {
                return false;
            }

            Agent candidate = crowd.All[agent.Intent.SocialPartnerIndex];
            if (!candidate.IsParticipating || candidate.Fear.State != AgentFearState.Calm)
            {
                agent.Intent.SocialPartnerIndex = -1;
                return false;
            }

            partner = candidate;
            return true;
        }
    }
}
