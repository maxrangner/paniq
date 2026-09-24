using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Errands: what a calm person does about a cue (see
    /// <see cref="CueSystem"/>). Going home to their own chair, going to the
    /// toilet, leaving the building at home time, going over to somebody for
    /// a chat. None of it is new movement: an errand is a purpose with a
    /// place, and it is carried out with what already exists -- the route
    /// fields for the walk, the door system for the doors on the way, the
    /// chair behaviour for the sit at the end, and a quiet remark through the
    /// sound system so that neighbours glance over at the talking.
    /// <para>
    /// A calm person's day used to happen inside one room, because a shut
    /// door was a wall to anybody who was not frightened. An errand opens the
    /// doors on its way, the way a frightened person does, and waits at one
    /// that will not open. That is what puts a queue at the front door at
    /// home time, and somebody behind a shut stall door when the fire starts.
    /// </para>
    /// <para>
    /// Only ever consulted by <see cref="CalmBehaviour"/>: fear takes over
    /// exactly as it takes over any calm activity, and the errand is cleared
    /// on the way. A glance at a noise interrupts an errand and
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
        private readonly DaySettings settings;
        private readonly CalmSettings calm;
        private readonly ExitSettings exits;

        public ErrandBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            PhysicsObjectSystem objects,
            DoorSystem doors,
            ChairBehaviour chairs,
            SoundSystem sound)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.doors = doors;
            this.chairs = chairs;
            this.sound = sound;
            settings = context.Scenario.Day;
            calm = context.Scenario.Calm;
            exits = context.Scenario.Exits;
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
                case ErrandPhase.Staying:
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
            if (!errand.Pending || context.Tick < errand.StartTick || !IsInterruptible(agent.Intent.Activity))
            {
                return false;
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
                return context.Tick >= errand.StartTick && Begin(agent);
            }

            if (!errand.Active)
            {
                return false;
            }

            if (errand.Phase == ErrandPhase.SittingDown)
            {
                // The chair behaviour had them and has let go: a glance at a
                // noise took them off the seat, or somebody else got to the
                // chair first. On the seat already, that is the errand done
                // and they settle back; otherwise they go for it again, and
                // if it is taken they stand about near it, as people do.
                if (agent.Sitting.OnIt)
                {
                    errand.Clear();
                    chairs.ResumeSitting(agent);
                    return true;
                }

                if (chairs.TryStartSittingOn(agent, agent.Home.Chair, false))
                {
                    return true;
                }

                errand.Clear();
                return false;
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
                    return BeginWalking(agent);
                case ErrandPhase.Walking:
                case ErrandPhase.OpeningTheDoor:
                case ErrandPhase.WaitingAtTheDoor:
                case ErrandPhase.Staying:
                    agent.Intent.Activity = AgentActivityState.RunningAnErrand;
                    agent.Body.BlockedTicks = 0;
                    return true;
                case ErrandPhase.Talking:
                    agent.Intent.Activity = AgentActivityState.Chatting;
                    return true;
                default:
                    errand.Clear();
                    return false;
            }
        }

        /// <summary>Takes up a pending errand: out of the chair first if they are in one, else straight to it.</summary>
        private bool Begin(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Kind == ErrandKind.GoHome && agent.Sitting.OnIt && agent.Sitting.ChairIndex == agent.Home.Chair)
            {
                // Already in their own chair: nothing to do.
                errand.Clear();
                return false;
            }

            if (agent.Sitting.OnIt)
            {
                chairs.StartStandingUp(agent);
                errand.Phase = ErrandPhase.GettingUp;
                return true;
            }

            return BeginWalking(agent);
        }

        /// <summary>
        /// On their feet and setting off. Somebody hailed for a chat does not
        /// set off anywhere: they turn to whoever hailed them and wait.
        /// </summary>
        private bool BeginWalking(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            int tick = context.Tick;
            errand.Phase = ErrandPhase.Walking;
            errand.Door = -1;
            errand.ApproachRoom = -1;
            if (errand.Kind != ErrandKind.ChatWith)
            {
                // A chat's end was drawn when it was called, and the walk over
                // to the other person is part of it, so it keeps that.
                errand.UntilTick = checked(tick + context.Jittered(settings.ErrandTimeoutTicks));
            }

            agent.Intent.Activity = AgentActivityState.RunningAnErrand;
            agent.Body.BlockedTicks = 0;

            switch (errand.Kind)
            {
                case ErrandKind.GoHome:
                    if (!agent.Home.Exists)
                    {
                        // Up, and nowhere in particular to go: the meeting is
                        // over for a visitor, say. They loiter as they always did.
                        errand.Clear();
                        return false;
                    }

                    errand.Place = HomePlace(agent);
                    errand.Room = geometry.RoomStoodIn(errand.Place);
                    return true;

                case ErrandKind.VisitTheToilet:
                    int stall = FindFreeStall(agent, out int stallDoor);
                    if (stall < 0)
                    {
                        errand.Clear();
                        return false;
                    }

                    errand.Room = stall;
                    errand.Object = stallDoor;
                    errand.Place = geometry.RoomBounds(stall).Centre;
                    return true;

                case ErrandKind.LeaveTheBuilding:
                    return true;

                case ErrandKind.ChatWith:
                    Agent partner = PartnerOf(agent);
                    if (partner == null)
                    {
                        errand.Clear();
                        return false;
                    }

                    if (partner.Errand.Active && partner.Errand.PartnerIndex == agent.Index && partner.Errand.Kind == ErrandKind.ChatWith)
                    {
                        // They were hailed: the other one is coming over.
                        StartTalking(agent);
                    }

                    errand.Room = geometry.RoomOf(partner);
                    return true;

                default:
                    errand.Clear();
                    return false;
            }
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
                case ErrandPhase.Staying:
                    return Stay(agent, out goalHeading);
                case ErrandPhase.Talking:
                    return Talk(agent, out goalHeading);
                default:
                    errand.Clear();
                    return false;
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

            if (tick >= errand.UntilTick)
            {
                return GiveUp(agent);
            }

            // Somebody leaving may be stuck in a queue for a long time, and
            // that is the point of them; everybody else gives up on a walk
            // that is going nowhere, with more patience than a stroll has.
            if (errand.Kind != ErrandKind.LeaveTheBuilding && agent.Body.BlockedTicks > settings.BlockedGiveUpTicks)
            {
                return GiveUp(agent);
            }

            if (errand.Kind == ErrandKind.ChatWith)
            {
                Agent partner = PartnerOf(agent);
                if (partner == null || geometry.RoomOf(partner) != geometry.RoomOf(agent))
                {
                    return End(agent);
                }

                errand.Place = partner.Body.Position;
                long gap = IntegerMath.Distance(agent.Body.Position, errand.Place);
                if (gap <= calm.SocialStopDistanceMillimetres)
                {
                    StartTalking(agent);
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, errand.Place, agent.Body.Heading);
                    return true;
                }

                goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading);
                goalSpeed = Pace(agent, gap - calm.SocialStopDistanceMillimetres);
                return true;
            }

            // A new room means a new next door: the route is worked out again
            // from wherever they have got to.
            int room = geometry.RoomAt(agent.Body.Position);
            if (room >= 0 && room != errand.ApproachRoom)
            {
                errand.ApproachRoom = room;
                if (!ChooseNextDoor(agent, room))
                {
                    return GiveUp(agent);
                }
            }

            if (errand.Door >= 0)
            {
                return StepThroughTheDoor(agent, out goalHeading, out goalSpeed);
            }

            // In the room it is in: the last leg.
            long distance = IntegerMath.Distance(agent.Body.Position, errand.Place);
            switch (errand.Kind)
            {
                case ErrandKind.GoHome:
                    if (agent.Home.Chair >= 0)
                    {
                        if (distance > context.Scenario.Items.SitSearchDistanceMillimetres)
                        {
                            break;
                        }

                        // Near enough: the chair behaviour takes them the rest
                        // of the way and seats them. If somebody else is in
                        // their chair they stand about near it, which is what
                        // people do.
                        if (chairs.TryStartSittingOn(agent, agent.Home.Chair, false))
                        {
                            errand.Phase = ErrandPhase.SittingDown;
                            return true;
                        }

                        errand.Clear();
                        return false;
                    }

                    if (distance < calm.StrollArrivalDistanceMillimetres)
                    {
                        errand.Clear();
                        return false;
                    }

                    break;

                case ErrandKind.VisitTheToilet:
                    if (room == errand.Room)
                    {
                        // In: the door shut behind them, if nobody is in it,
                        // and a while on their own.
                        doors.TryClose(errand.Object, agent.Id, errand.CauseEventId, agent);
                        errand.Phase = ErrandPhase.Staying;
                        errand.UntilTick = checked(tick + context.Random.NextIntInclusive(
                            settings.ToiletStayMinimumTicks, settings.ToiletStayMaximumTicks));
                        agent.Intent.LookHeading = agent.Body.Heading;
                        return true;
                    }

                    break;
            }

            goalHeading = geometry.Routes.HeadingToward(agent.Body.Position, errand.Place, bodyRadius, agent.Body.Heading);
            goalSpeed = Pace(agent, distance);
            return true;
        }

        /// <summary>
        /// The next door on the way from this room, or none when the place is
        /// in this room. Somebody leaving asks for the whole route to the
        /// nearest way out; everybody else for the route to the room their
        /// place is in. A calm person <em>asks</em> the way -- a visitor who
        /// does not know the floor is walked to the door like anybody else --
        /// so the route uses every door, not only the ones they know.
        /// </summary>
        private bool ChooseNextDoor(Agent agent, int room)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Kind == ErrandKind.LeaveTheBuilding)
            {
                return ChooseWayOut(agent, room);
            }

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
            if (doors.StateOf(door) == DoorState.Unlocked && !doors.IsObstructed(door))
            {
                errand.Phase = ErrandPhase.OpeningTheDoor;
                errand.UntilTick = checked(context.Tick + context.Jittered(exits.DoorOpenTicks));
                return true;
            }

            // Locked, or something wedged in it. They try the handle, which
            // is worth a line in the story, and wait for it.
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

            if (geometry.IsDoorOpen(errand.Door) ||
                doors.Open(errand.Door, errand.CauseEventId, geometry.SideOf(errand.Door, agent.Body.Position)))
            {
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

            return context.Tick >= errand.UntilTick ? GiveUp(agent) : true;
        }

        /// <summary>
        /// The door is open. Coming out of the stall, that was the last thing
        /// to do there, and they head home; otherwise the walk goes on.
        /// </summary>
        private bool CarryOnThroughTheDoor(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (errand.Done)
            {
                return GoHomeOrFinish(agent);
            }

            errand.Phase = ErrandPhase.Walking;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.ErrandTimeoutTicks));
            return true;
        }

        /// <summary>In the stall, door shut, for a while; then the door again, from the inside.</summary>
        private bool Stay(Agent agent, out int goalHeading)
        {
            AgentErrand errand = agent.Errand;
            goalHeading = agent.Intent.LookHeading;
            if (context.Tick < errand.UntilTick)
            {
                return true;
            }

            errand.Done = true;
            errand.Door = errand.Object;
            if (geometry.IsDoorOpen(errand.Door))
            {
                return GoHomeOrFinish(agent);
            }

            if (doors.StateOf(errand.Door) == DoorState.Unlocked && !doors.IsObstructed(errand.Door))
            {
                errand.Phase = ErrandPhase.OpeningTheDoor;
                errand.UntilTick = checked(context.Tick + context.Jittered(exits.DoorOpenTicks));
                return true;
            }

            // Somebody has locked them in. Worth a line, and then they wait
            // for whoever did it to think better of it.
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentTriedDoor, agent.Body.Position,
                0, 0, errand.CauseEventId, doors.IdOf(errand.Door));
            errand.Phase = ErrandPhase.WaitingAtTheDoor;
            errand.UntilTick = checked(context.Tick + context.Jittered(settings.WaitAtLockedDoorTicks));
            return true;
        }

        /// <summary>The purpose is served: back to their own chair if they have one, else the errand is over.</summary>
        private bool GoHomeOrFinish(Agent agent)
        {
            AgentErrand errand = agent.Errand;
            if (!agent.Home.Exists)
            {
                errand.Clear();
                return false;
            }

            ulong cause = errand.CauseEventId;
            errand.Clear();
            errand.Kind = ErrandKind.GoHome;
            errand.CauseEventId = cause;
            return BeginWalking(agent);
        }

        // ---------------------------------------------------------------- talking

        /// <summary>Stood facing the other one, talking. Ends when either has had enough, or the other is gone.</summary>
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

                return tick >= errand.UntilTick ? End(agent) : true;
            }

            if (tick >= errand.UntilTick)
            {
                return End(agent);
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
            agent.Intent.Activity = AgentActivityState.Chatting;
            agent.Body.BlockedTicks = 0;
            errand.NextRemarkTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.RemarkEveryMinimumTicks, settings.RemarkEveryMaximumTicks));
        }

        /// <summary>The person they are talking to, if that person is still in the chat with them.</summary>
        private Agent PartnerOf(Agent agent)
        {
            int index = agent.Errand.PartnerIndex;
            if (index < 0)
            {
                return null;
            }

            Agent partner = crowd.All[index];
            bool still = partner.IsParticipating && partner.Fear.State == AgentFearState.Calm &&
                         partner.Errand.Kind == ErrandKind.ChatWith && partner.Errand.PartnerIndex == agent.Index;
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

        /// <summary>Nobody in it, and nobody on their way to it.</summary>
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

            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Errand.Kind == ErrandKind.VisitTheToilet &&
                    agents[i].Errand.Room == stall)
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

        private static bool GiveUp(Agent agent)
        {
            agent.Errand.Clear();
            return false;
        }

        private static bool End(Agent agent)
        {
            agent.Errand.Clear();
            return false;
        }
    }
}
