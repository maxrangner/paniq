using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Calm "loitering": each person makes their own small decisions every
    /// few seconds - stroll somewhere, stand, look around, or go over and
    /// talk to someone - using the seeded generator and their own seeded
    /// pace and turn rate. Also turning toward, and edging toward, a noise,
    /// now and then tidying an item away (see <see cref="ItemBehaviour"/>),
    /// now and then sitting down on a chair (see <see cref="ChairBehaviour"/>),
    /// and the errands the building's day hands out -- back to their desk,
    /// to the toilet, home at the end of the day (see <see cref="ErrandBehaviour"/>
    /// and <see cref="CueSystem"/>).
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
        private readonly ErrandBehaviour errands;
        private readonly CueSystem cues;
        private readonly CalmSettings settings;

        public CalmBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            Locomotion locomotion,
            ItemBehaviour items,
            ChairBehaviour chairs,
            ErrandBehaviour errands,
            CueSystem cues)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.locomotion = locomotion;
            this.items = items;
            this.chairs = chairs;
            this.errands = errands;
            this.cues = cues;
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

            // An errand whose time has come cuts short whatever loitering
            // they were doing; what it leaves them doing is decided below.
            errands.StartIfDue(agent);

            switch (intent.Activity)
            {
                case AgentActivityState.Investigating:
                    if (tick >= intent.ActivityEndTick || agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
                    {
                        agent.Hearing.HasSoundPoint = false;
                        if (agent.Sitting.OnIt && !ErrandBehaviour.IsDue(agent, tick))
                        {
                            // Looked round from the chair and saw nothing: back to the table.
                            chairs.ResumeSitting(agent);
                            goalHeading = agent.Intent.LookHeading;
                            break;
                        }

                        ChooseNext(agent, true);
                        break;
                    }

                    // A startled head turns fast.
                    Investigate(agent, out goalHeading, out goalSpeed);
                    turnRate = agent.Personality.PanicTurnRate;
                    steer = goalSpeed > 0;
                    break;

                case AgentActivityState.Standing:
                    if (tick >= intent.ActivityEndTick)
                    {
                        ChooseNext(agent, false);
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
                            ChooseNext(agent, false);
                        }
                    }

                    break;

                case AgentActivityState.Strolling:
                {
                    long distance = IntegerMath.Distance(agent.Body.Position, intent.Target);
                    if (distance < settings.StrollArrivalDistanceMillimetres || tick >= intent.ActivityEndTick ||
                        agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
                    {
                        ChooseNext(agent, true);
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

                case AgentActivityState.RunningAnErrand:
                case AgentActivityState.Chatting:
                    if (errands.Update(agent, out goalHeading, out goalSpeed))
                    {
                        steer = goalSpeed > 0;
                        break;
                    }

                    ChooseNext(agent, true);
                    break;

                case AgentActivityState.GoingToSit:
                case AgentActivityState.Sitting:
                case AgentActivityState.StandingUp:
                    if (chairs.UpdateSitting(agent, out goalHeading, out goalSpeed))
                    {
                        steer = goalSpeed > 0;
                        break;
                    }

                    ChooseNext(agent, true);
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

                    ChooseNext(agent, true);
                    break;

                default:
                    // Coming back to calm from another state is not possible
                    // in this prototype, but choose afresh if it ever happens.
                    ChooseNext(agent, false);
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
            goalSpeed = 0;
            if (context.Tick < agent.Hearing.InvestigateStartTick)
            {
                // Not yet: nobody's head comes round on the tick the noise is
                // made. They finish the step they were taking first.
                goalHeading = agent.Body.Heading;
                return;
            }

            goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, agent.Hearing.SoundPoint, agent.Body.Heading);

            // From a chair they only turn to look; nobody edges off across the
            // room while still sitting in it. Nor from a stall, a door they
            // are waiting at, or a conversation.
            if (agent.Sitting.OnIt || ErrandBehaviour.IsStayingPut(agent))
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

        /// <summary>
        /// Whatever they were doing is over. An errand they are on, or one
        /// whose time has come, takes precedence; otherwise they choose for
        /// themselves.
        /// </summary>
        private void ChooseNext(Agent agent, bool justMoved)
        {
            if (errands.TryResume(agent))
            {
                return;
            }

            ChooseActivity(agent, justMoved);
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

            // One roll decides everything, in bands: tidying takes the lowest,
            // sitting the one above it, then a toilet trip, then going back
            // to their own desk. Each band falls through to the next when
            // there is nothing to do it with -- no chair free, no stall free.
            int band = context.Scenario.Items.TidyChancePercent;
            if (roll < band && items.TryStartTidying(agent))
            {
                return;
            }

            band += context.Scenario.Items.SitChancePercent;
            if (roll < band && chairs.TryStartSitting(agent))
            {
                return;
            }

            // Somebody with a cue waiting on them -- home time in a moment --
            // has no ideas of their own until it is done: what the building
            // asks beats what they thought of.
            bool free = agent.Errand.Kind == ErrandKind.None;

            // Not a band of the roll: a person needs the toilet when their own
            // clock says, however often they happen to be deciding things.
            if (free && TryStartToiletTrip(agent))
            {
                return;
            }

            band += context.Scenario.Day.GoHomeChancePercent;
            if (roll < band && free && TryGoHome(agent))
            {
                return;
            }

            if (roll < 50)
            {
                StartStroll(agent);
            }
            else if (roll < 75)
            {
                if (!free || !TryStartChat(agent))
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

        /// <summary>
        /// Their own idea: to the toilet, when their own clock says and given
        /// a free stall to go to. The first time is drawn anywhere inside the
        /// first stretch, so the office does not all go at once; a trip that
        /// cannot happen yet (every stall taken, hands full) waits a little
        /// and is tried again.
        /// </summary>
        private bool TryStartToiletTrip(Agent agent)
        {
            int every = context.Scenario.Day.ToiletEveryTicks;
            if (every <= 0)
            {
                return false;
            }

            int tick = context.Tick;
            if (agent.Home.NextToiletTick == 0)
            {
                agent.Home.NextToiletTick = checked(tick + 1 + context.Random.NextIntInclusive(0, every));
                return false;
            }

            if (tick < agent.Home.NextToiletTick)
            {
                return false;
            }

            if (agent.Carry.ItemIndex >= 0 || errands.FindFreeStall(agent, out _) < 0 || !cues.StartToiletTrip(agent))
            {
                // In a little while, then.
                agent.Home.NextToiletTick = checked(tick + context.Jittered(context.Scenario.Calm.StrollTimeoutTicks));
                return false;
            }

            agent.Home.NextToiletTick = checked(tick + context.Jittered(every));
            return errands.StartIfDue(agent);
        }

        /// <summary>Their own idea: back to their desk, if they have one and are not at it.</summary>
        private bool TryGoHome(Agent agent)
        {
            if (!agent.Home.Exists || errands.IsAtHome(agent))
            {
                return false;
            }

            return cues.SendHome(agent) && errands.StartIfDue(agent);
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
            intent.ActivityEndTick = checked(tick + context.Jittered(settings.StrollTimeoutTicks));
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

        /// <summary>
        /// Their own idea: over to somebody in the same room who is free to
        /// be talked to, for a chat. This used to be one-sided -- walk over
        /// and stand near somebody who never knew -- and is now a cue that
        /// both of them take up (<see cref="CueSystem.StartChat"/>).
        /// </summary>
        private bool TryStartChat(Agent agent)
        {
            long minimumSquared = (long)settings.SocialMinimumDistanceMillimetres * settings.SocialMinimumDistanceMillimetres;
            long maximumSquared = (long)settings.SocialMaximumDistanceMillimetres * settings.SocialMaximumDistanceMillimetres;
            Agent[] agents = crowd.All;
            int room = geometry.RoomOf(agent);

            // Only the people near enough to be worth walking over to, in the
            // same ascending order a walk of everybody would visit them in.
            using Crowd.Nearby near = crowd.Within(agent.Body.Position, settings.SocialMaximumDistanceMillimetres);
            int candidateCount = 0;
            for (int c = 0; c < near.Count; c++)
            {
                if (IsChatCandidate(agent, agents[near[c]], room, minimumSquared, maximumSquared))
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
                if (!IsChatCandidate(agent, agents[i], room, minimumSquared, maximumSquared))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    return cues.StartChat(agent, agents[i]) && errands.StartIfDue(agent);
                }
            }

            return false;
        }

        /// <summary>
        /// Somebody worth going over to: calm, in the same room, neither on an
        /// errand nor in a chair, and neither on top of them nor across the floor.
        /// </summary>
        private bool IsChatCandidate(Agent agent, Agent other, int room, long minimumSquared, long maximumSquared)
        {
            if (other == agent || !CueSystem.CanTakeUpACue(other) || other.Errand.Kind != ErrandKind.None ||
                other.Sitting.OnIt || !ErrandBehaviour.IsInterruptible(other.Intent.Activity) ||
                geometry.RoomOf(other) != room)
            {
                return false;
            }

            long distanceSquared = LogicalPosition.DistanceSquared(agent.Body.Position, other.Body.Position);
            return distanceSquared >= minimumSquared && distanceSquared <= maximumSquared;
        }
    }
}
