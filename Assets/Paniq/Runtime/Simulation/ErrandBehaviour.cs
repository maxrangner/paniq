using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Errands: what a calm person does about a cue (see
    /// <see cref="CueSystem"/>), carried out step by step from the cue's
    /// script (<see cref="CueDefinition"/>, <see cref="ErrandStepKind"/>).
    /// Going home to their own chair, going to the toilet, leaving the
    /// building at home time, going over to somebody for a chat. None of it
    /// is new movement: a step is a purpose with a place, and it is carried
    /// out with what already exists -- the route fields for the walk, the
    /// door system for the doors on the way, the chair behaviour for the sit
    /// at the end, and a quiet remark through the sound system so that
    /// neighbours glance over at the talking.
    /// <para>
    /// A calm person's day used to happen inside one room, because a shut
    /// door was a wall to anybody who was not frightened. A walk opens the
    /// doors on its way, the way a frightened person does, and waits at one
    /// that will not open. That is what puts a queue at the front door at
    /// home time, and somebody behind a shut stall door when the fire starts.
    /// </para>
    /// <para>
    /// Only ever consulted by <see cref="CalmBehaviour"/>: fear takes over
    /// exactly as it takes over any calm activity, and the errand is cleared
    /// on the way. A glance at a noise interrupts a step and
    /// <see cref="TryResume"/> carries it on where it left off.
    /// </para>
    /// </summary>
    internal sealed class ErrandBehaviour
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly DoorSystem doors;
        private readonly ChairBehaviour chairs;
        private readonly SoundSystem sound;
        private readonly CueSystem cues;
        private readonly DaySettings settings;
        private readonly CalmSettings calm;
        private readonly ExitSettings exits;

        /// <summary>
        /// Per room: who (agent index) last set off for it as a stall, or -1.
        /// A claim holds while that person's errand is still about the
        /// stall, so asking is one look rather than a walk of the crowd.
        /// </summary>
        private readonly int[] stallClaim;

        /// <summary>
        /// How often one person needs the toilet on this floor: the day's
        /// figure, stretched when that many people at that rate would keep
        /// the floor's stalls more than half full, so a big crowd with three
        /// stalls does not queue for them all afternoon.
        /// </summary>
        public int ToiletEveryTicks { get; }

        public ErrandBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            PhysicsObjectSystem objects,
            DoorSystem doors,
            ChairBehaviour chairs,
            SoundSystem sound,
            CueSystem cues)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.doors = doors;
            this.chairs = chairs;
            this.sound = sound;
            this.cues = cues;
            settings = context.Scenario.Day;
            calm = context.Scenario.Calm;
            exits = context.Scenario.Exits;
            stallClaim = new int[geometry.RoomCount];
            for (int r = 0; r < stallClaim.Length; r++)
            {
                stallClaim[r] = -1;
            }

            ToiletEveryTicks = ToiletRate();
        }

        /// <summary>
        /// The toilet rate the stalls can keep up with: at most half of them
        /// in use on average, with a stay at its longest, or the day's own
        /// figure when that is slower already. Nought (nobody goes) stays
        /// nought; a floor with no stall never sends anybody.
        /// </summary>
        private int ToiletRate()
        {
            int every = settings.ToiletEveryTicks;
            if (every <= 0)
            {
                return 0;
            }

            int stalls = 0;
            RoomDefinition[] rooms = context.Scenario.Rooms;
            for (int r = 0; r < rooms.Length; r++)
            {
                stalls += rooms[r].Use == RoomUse.Stall ? 1 : 0;
            }

            if (stalls == 0)
            {
                return 0;
            }

            int stay = 0;
            ErrandStep[] script = cues.DefinitionOf(CueKind.ToiletTrip).Script;
            for (int s = 0; s < script.Length; s++)
            {
                stay += script[s].Kind == ErrandStepKind.StandFor ? script[s].MaximumTicks : 0;
            }

            long needed = 2L * context.Scenario.Agents.Length * stay / stalls;
            return (int)Math.Min(int.MaxValue, Math.Max(every, needed));
        }

        // ---------------------------------------------------------------- taking up

        /// <summary>A calm activity an errand may cut short: loitering of any kind, and sitting on purpose.</summary>
        public static bool IsInterruptible(AgentActivityState activity)
        {
            switch (activity)
            {
                case AgentActivityState.Standing:
                case AgentActivityState.LookingAround:
                case AgentActivityState.Strolling:
                case AgentActivityState.Socialising:
                case AgentActivityState.Sitting:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Handed an errand whose time has come, or already on one.</summary>
        public static bool IsDue(Agent agent, int tick)
        {
            AgentErrand errand = agent.Errand;
            return errand.Active || (errand.Pending && tick >= errand.StartTick);
        }

        /// <summary>
        /// Somebody stood still on purpose in the middle of an errand -- in the
        /// stall, at a door, talking -- who turns to a noise without edging
        /// toward it.
        /// </summary>
        public static bool IsStayingPut(Agent agent)
        {
            if (!agent.Errand.Active)
            {
                return false;
            }

            switch (agent.Errand.Phase)
            {
                case ErrandPhase.Standing:
                case ErrandPhase.OpeningTheDoor:
                case ErrandPhase.WaitingAtTheDoor:
                case ErrandPhase.Talking:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The top of every calm decision: a pending errand whose tick has
        /// come is taken up now, if what they are doing can be dropped for it.
        /// True when it was.
        /// </summary>
        public bool StartIfDue(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            int tick = context.Tick;
            if (!errand.Pending || tick < errand.StartTick || !IsInterruptible(agent.Intent.Activity))
            {
                return false;
            }

            if (tick > errand.StartTick)
            {
                // Late -- a glance at a noise held them past their own tick,
                // which the whole room shares when the host speaks -- so they
                // take a tick nobody else is taking, rather than all rising
                // together the moment the glance ends.
                int start = cues.ReserveStart(tick);
                if (start > tick)
                {
                    errand.StartTick = start;
                    return false;
                }
            }

            return Begin(agent);
        }

        /// <summary>
        /// After whatever interrupted an errand has ended -- a glance at a
        /// noise, getting out of a chair -- carries it on where it left off.
        /// False when there is nothing to carry on with, and the person
        /// should choose something of their own.
        /// </summary>
        public bool TryResume(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Pending)
            {
                // Not begun yet: the same door as every start, so a late one
                // takes a tick of its own.
                return StartIfDue(agent);
            }

            if (!errand.Active)
            {
                return false;
            }

            if (errand.Phase == ErrandPhase.SittingDown)
            {
                // The chair behaviour had them and has let go: a glance at a
                // noise took them off the seat, or somebody else got to the
                // chair first. On the seat already, that is the step done and
                // they settle back; otherwise they go for it again, and if it
                // is taken they stand about near it, as people do.
                if (agent.Sitting.OnIt)
                {
                    Advance(agent);
                    chairs.ResumeSitting(agent);
                    return true;
                }

                if (agent.Sitting.ChairIndex == agent.Home.Chair && agent.Sitting.ChairIndex >= 0)
                {
                    // Still theirs -- they had hold of it, or were on their
                    // way -- so the chair behaviour simply carries on.
                    agent.Intent.Activity = AgentActivityState.GoingToSit;
                    return true;
                }

                return BeginStep(agent, errand.Step);
            }

            if (agent.Sitting.OnIt)
            {
                // Still in a chair (they turned in it to look at a noise, say):
                // out of it first, and the rest follows once they are up.
                chairs.StartStandingUp(agent);
                errand.Phase = ErrandPhase.GettingUp;
                return true;
            }

            switch (errand.Phase)
            {
                case ErrandPhase.GettingUp:
                    return BeginStep(agent, errand.Step);
                case ErrandPhase.Walking:
                case ErrandPhase.OpeningTheDoor:
                case ErrandPhase.WaitingAtTheDoor:
                case ErrandPhase.Standing:
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    agent.Body.BlockedTicks = 0;
                    return true;
                case ErrandPhase.Talking:
                    agent.Intent.Activity = AgentActivityState.Chatting;
                    return true;
                default:
                    return Finish(agent, "phase " + errand.Phase + " in TryResume");
            }
        }

        /// <summary>Takes up a pending errand: out of the chair first if they are in one, else straight to the first step.</summary>
        private bool Begin(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (agent.Sitting.OnIt && agent.Sitting.ChairIndex == agent.Home.Chair && SendsThemHome(errand))
            {
                // Already in their own chair: nowhere to walk to, but whatever
                // held them in it (a meeting, a lunch) is over, so they sit on
                // for a while of their own and then go about their day.
                chairs.SitForAWhile(agent);
                return Finish(agent, "already home");
            }

            // Anything the script has them say first is said from where they
            // are, seated or not: "that's all for today" comes before the
            // chair goes back, and the room hears it before anybody rises.
            ErrandStep[] script = ScriptOf(errand);
            int first = 0;
            while (first < script.Length && script[first].Kind == ErrandStepKind.Say)
            {
                sound.Say(agent, errand.CauseEventId, true);
                first++;
            }

            if (agent.Sitting.OnIt)
            {
                chairs.StartStandingUp(agent);
                errand.Step = first;
                errand.Phase = ErrandPhase.GettingUp;
                return true;
            }

            return BeginStep(agent, first);
        }

        /// <summary>Whether the script does nothing but send them home.</summary>
        private bool SendsThemHome(AgentErrand errand)
        {
            ErrandStep[] script = ScriptOf(errand);
            return script.Length > 0 && script[0].Kind == ErrandStepKind.GoTo && script[0].Target == ErrandTarget.Home;
        }

        private ErrandStep[] ScriptOf(AgentErrand errand)
        {
            CueDefinition cue = cues.DefinitionOf(errand.Cue);
            return errand.IsHost && cue.HostScript.Length > 0 ? cue.HostScript : cue.Script;
        }

        // ---------------------------------------------------------------- the steps

        /// <summary>Starts a step of the script. A step that takes no time, or that does not apply to this person, goes straight on to the next.</summary>
        private bool BeginStep(Agent agent, int index)
        {
            AgentErrand errand = agent.Errand;
            ErrandStep[] script = ScriptOf(errand);
            errand.ClearStep();
            errand.Step = index;
            if (index >= script.Length)
            {
                return Finish(agent, "done");
            }

            int tick = context.Tick;
            ErrandStep step = script[index];
            switch (step.Kind)
            {
                case ErrandStepKind.GoTo:
                    return BeginGoTo(agent, step);

                case ErrandStepKind.SitOn:
                    return BeginSitOn(agent);

                case ErrandStepKind.StandFor:
                    errand.Phase = ErrandPhase.Standing;
                    errand.UntilTick = checked(tick + context.Random.NextIntInclusive(step.MinimumTicks, step.MaximumTicks));
                    agent.Intent.LookHeading = agent.Body.Heading;
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    return true;

                case ErrandStepKind.Say:
                    sound.Say(agent, errand.CauseEventId, true);
                    return BeginStep(agent, index + 1);

                case ErrandStepKind.Talk:
                    StartTalking(agent);
                    return true;

                case ErrandStepKind.ShutTheDoor:
                    if (errand.Object >= 0)
                    {
                        doors.TryClose(errand.Object, agent.Id, errand.CauseEventId, agent);
                    }

                    return BeginStep(agent, index + 1);

                case ErrandStepKind.OpenTheDoor:
                    if (errand.Object < 0 || geometry.IsDoorOpen(errand.Object))
                    {
                        return BeginStep(agent, index + 1);
                    }

                    errand.Door = errand.Object;
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    return TryTheDoor(agent);

                case ErrandStepKind.Leave:
                    errand.Phase = ErrandPhase.Walking;
                    errand.UntilTick = checked(tick + context.Jittered(settings.ErrandTimeoutTicks));
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    agent.Body.BlockedTicks = 0;
                    return true;

                default:
                    return Finish(agent, "unknown step");
            }
        }

        /// <summary>
        /// Where a walk is aimed, worked out as it starts. Somebody with no
        /// home skips a step aimed at one; an errand aimed at a stall or a
        /// person that is not there is over.
        /// </summary>
        private bool BeginGoTo(Agent agent, ErrandStep step)
        {
            AgentErrand errand = agent.Errand;
            switch (step.Target)
            {
                case ErrandTarget.Home:
                    if (!agent.Home.Exists)
                    {
                        return BeginStep(agent, errand.Step + 1);
                    }

                    AimAtHome(agent);
                    break;

                case ErrandTarget.HomeOrWhereTheyStood:
                    if (agent.Home.Exists)
                    {
                        AimAtHome(agent);
                    }
                    else
                    {
                        errand.Destination = errand.Origin;
                        errand.Room = geometry.RoomStoodIn(errand.Destination);
                        errand.ArriveWithin = calm.StrollArrivalDistanceMillimetres;
                    }

                    break;

                case ErrandTarget.TheNoise:
                    // Toward where they heard it, stopping well short: by then
                    // they have seen what it was, or there was nothing to see.
                    errand.Destination = agent.Hearing.NoiseToLookAt;
                    errand.Room = geometry.RoomStoodIn(errand.Destination);
                    errand.ArriveWithin = context.Scenario.Hearing.GoAndLookStopMillimetres;
                    if (errand.Room < 0)
                    {
                        return Finish(agent, "noise from nowhere");
                    }

                    break;

                case ErrandTarget.FreeStall:
                    int stall = FindFreeStall(agent, out int stallDoor);
                    if (stall < 0)
                    {
                        return Finish(agent, "no free stall");
                    }

                    stallClaim[stall] = agent.Index;
                    errand.Room = stall;
                    errand.Object = stallDoor;
                    errand.Destination = geometry.RoomBounds(stall).Centre;
                    errand.ArriveWithin = 0;
                    break;

                case ErrandTarget.Partner:
                    Agent partner = PartnerOf(agent);
                    if (partner == null)
                    {
                        return Finish(agent, "partner gone");
                    }

                    // Both walk, and meet in the middle: the one hailed used
                    // to stand and wait to be walked up to from six metres
                    // off, which read as a summons rather than a chat.
                    errand.Room = geometry.RoomOf(partner);
                    errand.Place = partner.Body.Position;
                    errand.ArriveWithin = calm.SocialStopDistanceMillimetres;

                    // The walk over is part of the chat and ends when it does:
                    // no timeout of its own, and no draw for one.
                    errand.Phase = ErrandPhase.Walking;
                    errand.UntilTick = errand.ChatEndTick;
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    agent.Body.BlockedTicks = 0;
                    return true;

                default:
                    return Finish(agent, "unknown target");
            }

            errand.Phase = ErrandPhase.Walking;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.ErrandTimeoutTicks));
            agent.Intent.Activity = AgentActivityState.RunningAnErrand;
            agent.Body.BlockedTicks = 0;
            StartWandering(agent);
            return true;
        }

        /// <summary>
        /// A walk with a purpose still wanders a little, as a stroll does:
        /// dead straight lines read as clockwork. The offset is redrawn every
        /// second or so and fades out as the place gets close, so arriving
        /// looks deliberate; doorways are approached straight.
        /// </summary>
        private void StartWandering(Agent agent)
        {
            agent.Intent.WanderOffset = context.Random.NextIntInclusive(-calm.WanderMaximumDegrees, calm.WanderMaximumDegrees);
            agent.Intent.NextWanderTick = checked(context.Tick + context.Random.NextIntInclusive(25, 60));
        }

        private int Wander(Agent agent, long distance)
        {
            AgentIntent intent = agent.Intent;
            if (context.Tick >= intent.NextWanderTick)
            {
                StartWandering(agent);
            }

            return distance < 1200 ? intent.WanderOffset / 2 : intent.WanderOffset;
        }

        /// <summary>Their chair or spot: near enough a chair for the chair behaviour to take them the rest of the way, or on a spot.</summary>
        private void AimAtHome(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            errand.Destination = HomePlace(agent);
            errand.Room = geometry.RoomStoodIn(errand.Destination);
            errand.ArriveWithin = agent.Home.Chair >= 0
                ? context.Scenario.Items.SitSearchDistanceMillimetres
                : calm.StrollArrivalDistanceMillimetres;
        }

        /// <summary>
        /// Their own chair, if they have one and are near it: the chair
        /// behaviour takes them the rest of the way and seats them. If
        /// somebody else is in it they stand about near it, which is what
        /// people do; either way the errand ends with the sit.
        /// </summary>
        private bool BeginSitOn(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            int chair = agent.Home.Chair;

            // Only somebody nowhere near it skips the sit: the walk before
            // this step brings them within the chair behaviour's own reach,
            // and a chair nudged a hand's width since the walk was aimed must
            // not lose them the sit on the very tick they arrive.
            if (chair < 0 ||
                IntegerMath.Distance(agent.Body.Position, objects.PositionOf(chair)) > 2L * context.Scenario.Items.SitSearchDistanceMillimetres)
            {
                return BeginStep(agent, errand.Step + 1);
            }

            if (chairs.TryStartSittingOn(agent, chair, false))
            {
                errand.Phase = ErrandPhase.SittingDown;
                return true;
            }

            if (objects.OccupantOf(chair) < 0 && errand.Tries < 3)
            {
                // Nobody is on it, but it is not to be sat on right now -- still
                // sliding from somebody's knock, or on its back: a moment, and
                // another look.
                errand.Tries++;
                errand.RetryStep = true;
                errand.Phase = ErrandPhase.Standing;
                errand.UntilTick = checked(context.Tick + context.Jittered(context.Scenario.Items.SitPullTicks * 2));
                agent.Intent.LookHeading = agent.Body.Heading;
                agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                return true;
            }

            return BeginStep(agent, errand.Step + 1);
        }

        /// <summary>The step is done: on to the next, or the errand is over.</summary>
        private bool Advance(Agent agent)
        {
            return BeginStep(agent, agent.Errand.Step + 1);
        }

        // ---------------------------------------------------------------- carrying out

        /// <summary>
        /// One tick of an errand, for somebody whose activity is
        /// <see cref="AgentActivityState.RunningAnErrand"/> or
        /// <see cref="AgentActivityState.Chatting"/>. False when the errand is
        /// over, done or given up, and they should choose for themselves.
        /// </summary>
        public bool Update(Agent agent, out int goalHeading, out int goalSpeed)
        {
            goalHeading = agent.Body.Heading;
            goalSpeed = 0;
            AgentErrand errand = agent.Errand;
            if (!errand.Active)
            {
                return false;
            }

            switch (errand.Phase)
            {
                case ErrandPhase.Walking:
                    return Walk(agent, out goalHeading, out goalSpeed);
                case ErrandPhase.OpeningTheDoor:
                    return OpenTheDoor(agent, out goalHeading);
                case ErrandPhase.WaitingAtTheDoor:
                    return WaitAtTheDoor(agent, out goalHeading);
                case ErrandPhase.Standing:
                    goalHeading = agent.Intent.LookHeading;
                    if (context.Tick < errand.UntilTick)
                    {
                        return true;
                    }

                    return errand.RetryStep ? BeginStep(agent, errand.Step) : Advance(agent);
                case ErrandPhase.Talking:
                    return Talk(agent, out goalHeading);
                default:
                    return Finish(agent, "phase " + errand.Phase + " in Update");
            }
        }

        /// <summary>
        /// On the way: room to room through the doors on the route, opening
        /// any that are shut, and then to the place itself.
        /// </summary>
        private bool Walk(Agent agent, out int goalHeading, out int goalSpeed)
        {
            AgentErrand errand = agent.Errand;
            int tick = context.Tick;
            goalHeading = agent.Body.Heading;
            goalSpeed = 0;
            ErrandStep step = ScriptOf(errand)[errand.Step];
            bool leaving = step.Kind == ErrandStepKind.Leave;

            if (tick >= errand.UntilTick)
            {
                return GiveUp(agent, "walk timed out");
            }

            // Somebody leaving may be stuck in a queue for a long time, and
            // that is the point of them; everybody else gives up on a walk
            // that is going nowhere, with more patience than a stroll has.
            if (!leaving && agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
            {
                return GiveUp(agent, "stuck");
            }

            if (step.Target == ErrandTarget.Partner)
            {
                Agent partner = PartnerOf(agent);
                if (partner == null || geometry.RoomOf(partner) != geometry.RoomOf(agent))
                {
                    return Finish(agent, "partner gone or left the room");
                }

                errand.Place = partner.Body.Position;
                long gap = IntegerMath.Distance(agent.Body.Position, errand.Place);
                if (gap <= errand.ArriveWithin)
                {
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, errand.Place, agent.Body.Heading);
                    return Advance(agent);
                }

                goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading);
                goalSpeed = Pace(agent, gap - errand.ArriveWithin);
                return true;
            }

            if (errand.OpenedDoor >= 0)
            {
                ShutTheDoorBehindThem(agent);
            }

            // A new room means a new next door: the route is worked out again
            // from wherever they have got to.
            int room = geometry.RoomAt(agent.Body.Position);
            if (room >= 0 && room != errand.ApproachRoom)
            {
                errand.ApproachRoom = room;
                if (!(leaving ? ChooseWayOut(agent, room) : ChooseNextDoor(agent, room)))
                {
                    return GiveUp(agent, "no route");
                }
            }

            if (errand.Door >= 0)
            {
                return StepThroughTheDoor(agent, out goalHeading, out goalSpeed);
            }

            // In the room it is in: the last leg, to the destination itself
            // rather than to the door they came in by. A chair is aimed at
            // where it stands now; it may have been nudged since they set off.
            bool aChair = agent.Home.Chair >= 0 &&
                          (step.Target == ErrandTarget.Home || step.Target == ErrandTarget.HomeOrWhereTheyStood);
            errand.Place = aChair ? objects.PositionOf(agent.Home.Chair) : errand.Destination;
            long distance = IntegerMath.Distance(agent.Body.Position, errand.Place);
            bool arrived = step.Target == ErrandTarget.FreeStall ? room == errand.Room : distance <= errand.ArriveWithin;
            if (arrived)
            {
                agent.Intent.LookHeading = agent.Body.Heading;
                return Advance(agent);
            }

            goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading) +
                          Wander(agent, distance);
            goalSpeed = Pace(agent, distance);
            return true;
        }

        /// <summary>
        /// A door they opened themselves, once they are a stride through it:
        /// shut behind them, unless somebody else is near it and may be on
        /// their way through, in which case it is left, as people do. Doors
        /// used to drift open one by one until every door on the floor stood
        /// open, which changes how a fire and a noise travel.
        /// </summary>
        private void ShutTheDoorBehindThem(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            int door = errand.OpenedDoor;
            if (geometry.SideOf(door, agent.Body.Position) == errand.OpenedFromSide ||
                IntegerMath.Distance(agent.Body.Position, geometry.DoorCentre(door)) < bodyRadius * 3)
            {
                return;
            }

            errand.OpenedDoor = -1;
            errand.OpenedFromSide = 0;
            if (!geometry.IsDoorOpen(door))
            {
                return;
            }

            using Crowd.Nearby near = crowd.Within(geometry.DoorCentre(door), settings.DoorHoldMillimetres);
            for (int c = 0; c < near.Count; c++)
            {
                Agent other = crowd.All[near[c]];
                if (other != agent && other.IsParticipating)
                {
                    return;
                }
            }

            doors.TryClose(door, agent.Id, errand.CauseEventId, agent);
        }

        /// <summary>
        /// The next door on the way from this room, or none when the place is
        /// in this room. A calm person <em>asks</em> the way -- a visitor who
        /// does not know the floor is walked to the door like anybody else --
        /// so the route uses every door, not only the ones they know.
        /// </summary>
        private bool ChooseNextDoor(Agent agent, int room)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Room < 0 || errand.Room == room)
            {
                errand.Door = -1;
                return true;
            }

            if (!geometry.TryFindRoute(room, agent.Body.Position, errand.Room, agent, out int first, out _, out _))
            {
                return false;
            }

            errand.Door = first;
            return true;
        }

        /// <summary>
        /// The way out that is the shortest walk from here, counting the walk
        /// to the first door and from the last one, and the first door toward
        /// it. Lowest door index on a tie; no random numbers.
        /// </summary>
        private bool ChooseWayOut(Agent agent, int room)
        {
            AgentErrand errand = agent.Errand;
            int best = -1;
            int bestFirst = -1;
            long bestCost = long.MaxValue;
            for (int d = 0; d < geometry.DoorCount; d++)
            {
                if (!geometry.DoorLeadsOutside(d))
                {
                    continue;
                }

                int doorRoom = geometry.DoorRoom(d);
                if (!geometry.TryFindRoute(room, agent.Body.Position, doorRoom, agent, out int first, out int last, out long cost))
                {
                    continue;
                }

                LogicalPosition approach = geometry.DoorPointFrom(d, doorRoom, 0, -exits.ApproachInsetMillimetres);
                long total = cost + (first < 0
                    ? IntegerMath.Distance(agent.Body.Position, approach)
                    : IntegerMath.Distance(geometry.DoorCentre(last), approach));
                if (total < bestCost)
                {
                    bestCost = total;
                    best = d;
                    bestFirst = first;
                }
            }

            if (best < 0)
            {
                return false;
            }

            errand.WayOutDoor = best;
            errand.Door = bestFirst < 0 ? best : bestFirst;
            return true;
        }

        /// <summary>
        /// Getting through the next door: through it if it is open, to the
        /// spot in front of it if it is shut, and then a push at it or a wait
        /// beside it.
        /// </summary>
        private bool StepThroughTheDoor(Agent agent, out int goalHeading, out int goalSpeed)
        {
            AgentErrand errand = agent.Errand;
            int door = errand.Door;
            goalHeading = agent.Body.Heading;
            goalSpeed = 0;

            if (geometry.IsDoorOpen(door))
            {
                // Aimed a stride past it, from the side they are on: for a
                // door between rooms that is the next room, and for the way out
                // it is the street, and far enough out to count as gone.
                errand.Place = geometry.DoorPointFrom(door, errand.ApproachRoom, 0, exits.OutsideTargetMillimetres);
                goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading);
                goalSpeed = agent.Personality.CalmSpeed;
                return true;
            }

            errand.Place = geometry.DoorPointFrom(door, errand.ApproachRoom, 0, -exits.ApproachInsetMillimetres);
            long gap = IntegerMath.Distance(agent.Body.Position, errand.Place);
            if (gap > exits.ArrivalDistanceMillimetres)
            {
                goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading);
                goalSpeed = Pace(agent, gap);
                return true;
            }

            goalHeading = FaceTheDoor(agent, door);
            return TryTheDoor(agent);
        }

        /// <summary>
        /// At a shut door: a push at it if it is merely shut, which takes a
        /// moment; otherwise (locked, or something wedged in it) they try the
        /// handle, which is worth a line in the story, and wait for it.
        /// </summary>
        private bool TryTheDoor(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            int door = errand.Door;
            if (doors.StateOf(door) == DoorState.Unlocked && !doors.IsObstructed(door))
            {
                errand.Phase = ErrandPhase.OpeningTheDoor;
                errand.UntilTick = checked(context.Tick + context.Jittered(exits.DoorOpenTicks));
                return true;
            }

            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentTriedDoor, agent.Body.Position,
                0, 0, errand.CauseEventId, doors.IdOf(door));
            errand.Phase = ErrandPhase.WaitingAtTheDoor;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.WaitAtLockedDoorTicks));
            return true;
        }

        /// <summary>A moment with a hand on the door, then it is open and they carry on.</summary>
        private bool OpenTheDoor(Agent agent, out int goalHeading)
        {
            AgentErrand errand = agent.Errand;
            goalHeading = FaceTheDoor(agent, errand.Door);
            if (context.Tick < errand.UntilTick)
            {
                return true;
            }

            if (geometry.IsDoorOpen(errand.Door))
            {
                return CarryOnThroughTheDoor(agent);
            }

            int side = geometry.SideOf(errand.Door, agent.Body.Position);
            if (doors.Open(errand.Door, errand.CauseEventId, side))
            {
                // Theirs to shut behind them once they are through.
                errand.OpenedDoor = errand.Door;
                errand.OpenedFromSide = side;
                return CarryOnThroughTheDoor(agent);
            }

            // Something got wedged in it in the meantime.
            errand.Phase = ErrandPhase.WaitingAtTheDoor;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.WaitAtLockedDoorTicks));
            return true;
        }

        /// <summary>
        /// Stood at a door that would not open, waiting for it to. The moment
        /// it does -- the player turns the key, somebody shoves the wedge
        /// clear -- they go on; after long enough they give it up.
        /// </summary>
        private bool WaitAtTheDoor(Agent agent, out int goalHeading)
        {
            AgentErrand errand = agent.Errand;
            goalHeading = FaceTheDoor(agent, errand.Door);
            if (geometry.IsDoorOpen(errand.Door))
            {
                return CarryOnThroughTheDoor(agent);
            }

            if (doors.StateOf(errand.Door) == DoorState.Unlocked && !doors.IsObstructed(errand.Door))
            {
                errand.Phase = ErrandPhase.OpeningTheDoor;
                errand.UntilTick = checked(context.Tick + context.Jittered(exits.DoorOpenTicks));
                return true;
            }

            if (context.Tick < errand.UntilTick)
            {
                return true;
            }

            // Found locked: they stop counting on it for a while, so the next
            // errand is not routed through it a moment later and the story
            // does not fill with them trying the same handle.
            agent.Doors.AvoidUntilTick[errand.Door] = checked(context.Tick + context.Jittered(settings.LockedDoorMemoryTicks));
            return GiveUp(agent, "door never opened");
        }

        /// <summary>
        /// The door is open. If opening it was the step (the stall door, from
        /// the inside) that step is done; otherwise it was a door on the way,
        /// and the walk goes on.
        /// </summary>
        private bool CarryOnThroughTheDoor(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (ScriptOf(errand)[errand.Step].Kind == ErrandStepKind.OpenTheDoor)
            {
                return Advance(agent);
            }

            errand.Phase = ErrandPhase.Walking;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.ErrandTimeoutTicks));
            return true;
        }

        // ---------------------------------------------------------------- talking

        /// <summary>Stood facing the other one, talking. Ends when the one whose idea it was has had enough, or the other is gone.</summary>
        private bool Talk(Agent agent, out int goalHeading)
        {
            AgentErrand errand = agent.Errand;
            int tick = context.Tick;
            goalHeading = agent.Body.Heading;
            Agent partner = PartnerOf(agent);
            if (partner == null)
            {
                if (errand.PartnerIndex >= 0)
                {
                    // The other one has gone -- frightened, called away. It
                    // takes a moment to notice, like anything else.
                    errand.PartnerIndex = -1;
                    errand.UntilTick = Math.Min(errand.UntilTick, context.ReactionTick());
                }

                return tick >= errand.UntilTick ? Finish(agent, "partner gone") : true;
            }

            if (tick >= errand.UntilTick)
            {
                return Finish(agent, "chat over");
            }

            goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, partner.Body.Position, agent.Body.Heading);
            long gap = IntegerMath.Distance(agent.Body.Position, partner.Body.Position);
            bool together = gap <= calm.SocialStopDistanceMillimetres + calm.StrollSlowdownDistanceMillimetres;
            if (together && tick >= errand.NextRemarkTick)
            {
                // The first thing each of them says is what the neighbours
                // look up at; the rest is only written down.
                sound.Say(agent, errand.CauseEventId, errand.Remarks == 0);
                errand.Remarks++;
                errand.NextRemarkTick = checked(tick + context.Random.NextIntInclusive(
                    settings.RemarkEveryMinimumTicks, settings.RemarkEveryMaximumTicks));
            }

            return true;
        }

        private void StartTalking(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            errand.Phase = ErrandPhase.Talking;

            // The one whose idea it was has had enough at the drawn end; the
            // other notices a moment later, like anything else, so the two
            // never turn away on the same tick. The long stop is a backstop.
            errand.UntilTick = errand.IsHost ? errand.ChatEndTick : checked(errand.ChatEndTick + settings.ErrandTimeoutTicks);
            agent.Intent.Activity = AgentActivityState.Chatting;
            agent.Body.BlockedTicks = 0;
            errand.NextRemarkTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.RemarkEveryMinimumTicks, settings.RemarkEveryMaximumTicks));
        }

        /// <summary>The person the cue is about, if that person is still in it with them.</summary>
        private Agent PartnerOf(Agent agent)
        {
            int index = agent.Errand.PartnerIndex;
            if (index < 0)
            {
                return null;
            }

            Agent partner = crowd.All[index];
            bool still = partner.IsParticipating && partner.Fear.State == AgentFearState.Calm && partner.Body.IsOnTheirFeet &&
                         partner.Errand.Has && partner.Errand.PartnerIndex == agent.Index;
            return still ? partner : null;
        }

        // ---------------------------------------------------------------- places

        /// <summary>Where home is: the chair, or the spot.</summary>
        private LogicalPosition HomePlace(Agent agent)
        {
            return agent.Home.Chair >= 0 ? objects.PositionOf(agent.Home.Chair) : agent.Home.Spot;
        }

        /// <summary>On their own chair, or near enough their spot to count.</summary>
        public bool IsAtHome(Agent agent)
        {
            if (!agent.Home.Exists)
            {
                return false;
            }

            if (agent.Home.Chair >= 0)
            {
                return agent.Sitting.OnIt && agent.Sitting.ChairIndex == agent.Home.Chair;
            }

            return IntegerMath.Distance(agent.Body.Position, agent.Home.Spot) <= settings.AtHomeMillimetres;
        }

        /// <summary>
        /// The nearest stall nobody is in or on their way to, by how far it is
        /// to walk there, and its door. -1 when there is none. Straight-line
        /// distance when there is no routing to spare this tick.
        /// </summary>
        public int FindFreeStall(Agent agent, out int door)
        {
            door = -1;
            int best = -1;
            long bestDistance = long.MaxValue;
            FlowField walking = geometry.Routes.ReachFrom(agent.Body.Position, bodyRadius);
            RoomDefinition[] rooms = context.Scenario.Rooms;
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (rooms[r].Use != RoomUse.Stall || geometry.RoomDoors(r).Length == 0 || !IsStallFree(r))
                {
                    continue;
                }

                LogicalPosition centre = geometry.RoomBounds(r).Centre;
                long distance = walking == null
                    ? IntegerMath.Distance(agent.Body.Position, centre)
                    : geometry.Routes.DistanceIn(walking, centre);
                if (distance == long.MaxValue)
                {
                    // Unreachable by the fields, which do not go through shut
                    // doors; the walk is worked out room by room and will.
                    distance = IntegerMath.Distance(agent.Body.Position, centre) * 4;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = r;
                }
            }

            if (best >= 0)
            {
                door = geometry.RoomDoors(best)[0];
            }

            return best;
        }

        /// <summary>Nobody in it, and nobody on their way to it: the last claim on it, if it still holds.</summary>
        private bool IsStallFree(int stall)
        {
            using Crowd.Nearby inside = crowd.Gather(geometry.RoomBounds(stall));
            for (int c = 0; c < inside.Count; c++)
            {
                Agent person = crowd.All[inside[c]];
                if (person.IsParticipating && geometry.RoomAt(person.Body.Position) == stall)
                {
                    return false;
                }
            }

            int claim = stallClaim[stall];
            if (claim >= 0)
            {
                Agent claimant = crowd.All[claim];
                if (claimant.IsParticipating && claimant.Errand.Has && claimant.Errand.Room == stall)
                {
                    return false;
                }
            }

            return true;
        }

        private int FaceTheDoor(Agent agent, int door)
        {
            return IntegerMath.HeadingBetween(agent.Body.Position, geometry.DoorCentre(door), agent.Body.Heading);
        }

        /// <summary>Walking pace, easing off over the last stretch so arrival looks deliberate, as a stroll does.</summary>
        private int Pace(Agent agent, long distanceLeft)
        {
            int speed = agent.Personality.CalmSpeed;
            if (distanceLeft < calm.StrollSlowdownDistanceMillimetres)
            {
                return Math.Max(speed / 3, (int)(speed * Math.Max(0L, distanceLeft) / calm.StrollSlowdownDistanceMillimetres));
            }

            return speed;
        }

        /// <summary>
        /// The errand is over, done or given up: they choose for themselves
        /// from here, unless a cue arrived while they were busy, which they
        /// take up next. Home time given up on (a locked way out, no route)
        /// stands: they try again in a while. The reason is kept for the
        /// debug line.
        /// </summary>
        private bool Finish(Agent agent, string because)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Has && errand.Cue == CueKind.HomeTime)
            {
                agent.Home.NextHomeTryTick = checked(context.Tick + context.Jittered(settings.HomeTimeRetryTicks));
            }

            PendingCue next = errand.Next;
            errand.Clear();
            errand.EndedBecause = because;
            if (next.Has)
            {
                errand.Has = true;
                errand.Cue = next.Cue;
                errand.IsHost = next.IsHost;
                errand.StartTick = next.StartTick;
                errand.CauseEventId = next.CauseEventId;
                errand.Origin = agent.Body.Position;
            }

            return false;
        }

        private bool GiveUp(Agent agent, string because)
        {
            return Finish(agent, because);
        }
    }
}
