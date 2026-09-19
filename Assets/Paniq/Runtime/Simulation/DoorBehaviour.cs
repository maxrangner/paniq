namespace Paniq.Simulation
{
    /// <summary>
    /// Panicked people and doors. A runner works out a way out of the
    /// building: which door out to aim for, and which door to head through
    /// first to get there. An unlocked door they open, a locked one they
    /// rattle and maybe try to force (it never gives) before looking for
    /// another way; when every way out has failed them, they make for
    /// whichever room is furthest from the fire. People can see an open
    /// doorway, but a shut door looks the same to them locked or not; they
    /// remember a door that would not open for a while. Walking far enough
    /// out through a door to the outside is an escape. People close doors
    /// behind them, or keep them open, according to their personality.
    /// </summary>
    internal sealed class DoorBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly FireSystem fire;
        private readonly SoundSystem sound;
        private readonly ExitSettings settings;

        public DoorBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            DoorSystem doors,
            FireSystem fire,
            SoundSystem sound)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.fire = fire;
            this.sound = sound;
            settings = context.Scenario.Exits;
        }

        /// <summary>
        /// The door to head through next, or -1 when nowhere is better than
        /// where they stand. Ways out of the building are scored by how far
        /// it is to walk there through the rooms; the first door on that walk
        /// is the one they run for. With no way out left, they pick the room
        /// furthest from the fire instead.
        /// </summary>
        public int ChooseExitDoor(Agent agent)
        {
            LogicalPosition position = agent.Body.Position;
            int room = geometry.RoomAt(position);
            if (room < 0)
            {
                // Halfway through a doorway: keep going.
                return agent.Doors.ExitDoorIndex;
            }

            agent.Doors.ApproachRoom = room;
            int best = -1;
            long bestScore = long.MinValue;
            for (int d = 0; d < doors.Count; d++)
            {
                if (!geometry.DoorLeadsOutside(d) ||
                    !geometry.TryFindRoute(room, position, geometry.DoorRoom(d), agent, out int first, out long routeCost))
                {
                    continue;
                }

                // The door to run for now: the way out itself when it is in
                // this room, otherwise the first door along the way.
                int next = first < 0 ? d : first;
                bool open = geometry.IsDoorOpen(next);
                if (!open && (context.Tick < agent.Doors.AvoidUntilTick[next] || agent.Doors.FoundShut[d]))
                {
                    // A door they have already found shut is no longer a way out to them.
                    continue;
                }

                LogicalPosition approach = ApproachPoint(d, geometry.DoorRoom(d));
                long score = context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) - routeCost;
                score -= first < 0 ? IntegerMath.Distance(position, approach) : 0L;
                if (geometry.IsDoorOpen(d))
                {
                    score += settings.OpenBonusMillimetres;
                }

                if (next == agent.Doors.ExitDoorIndex)
                {
                    score += settings.CurrentChoiceBonusMillimetres;
                }

                score -= RoutePenalties(agent, position, next);
                if (fire.AnyCloserThan(approach, TraitEffects.DangerDistance(agent, context.Scenario)))
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = next;
                }
            }

            // Every way out has been tried and would not open: get into
            // whichever room is furthest from the flames instead.
            return best >= 0 ? best : ChooseRefugeDoor(agent, room, position);
        }

        /// <summary>
        /// No way out of the building left: head for the room furthest from
        /// the fire that they can still reach. Returns the first door on the
        /// way, or -1 when the room they are in is already the best of them.
        /// </summary>
        private int ChooseRefugeDoor(Agent agent, int room, LogicalPosition position)
        {
            int best = -1;
            long bestScore = RefugeScore(room, position) + settings.CurrentRoomBonusMillimetres;
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (r == room || !geometry.TryFindRoute(room, position, r, agent, out int first, out long routeCost) || first < 0)
                {
                    continue;
                }

                if ((!geometry.IsDoorOpen(first) && context.Tick < agent.Doors.AvoidUntilTick[first]) || IsRoomFull(r, agent))
                {
                    continue;
                }

                long score = RefugeScore(r, geometry.DoorCentre(first)) - routeCost +
                             context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) -
                             RoutePenalties(agent, position, first);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = first;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether a room already holds as many people as there is floor for:
        /// one square metre each, so nobody makes for a cupboard with three
        /// people already crammed into it.
        /// </summary>
        private bool IsRoomFull(int room, Agent hopeful)
        {
            LogicalBounds b = geometry.RoomBounds(room);
            long space = settings.RefugeSpacePerPersonMillimetres;
            long capacity = (b.MaxX - b.MinX) / space * ((b.MaxZ - b.MinZ) / space);
            Agent[] agents = crowd.All;
            int inside = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] != hopeful && agents[i].IsParticipating && geometry.RoomOf(agents[i]) == room)
                {
                    inside++;
                }
            }

            return inside >= capacity;
        }

        /// <summary>How good a room looks to hide in: how far its middle is from the flames, and empty of fire.</summary>
        private long RefugeScore(int room, LogicalPosition from)
        {
            LogicalPosition middle = geometry.RoomBounds(room).Centre;
            long fireDistanceSquared = fire.NearestDistanceSquared(middle);
            long score = fireDistanceSquared == long.MaxValue
                ? settings.RefugeNoFireMillimetres
                : IntegerMath.Sqrt(fireDistanceSquared);
            if (fire.IsBurningInRoom(room))
            {
                score -= settings.InFirePenaltyMillimetres;
            }

            return score - IntegerMath.Distance(from, middle) / 4L;
        }

        /// <summary>What is in the way on the first leg: flames to squeeze past, or a table to go around.</summary>
        private long RoutePenalties(Agent agent, LogicalPosition position, int door)
        {
            LogicalPosition centre = geometry.DoorCentre(door);
            long penalty = 0L;
            if (fire.RoutePassesNear(position, centre, context.Scenario.Panic.EscapeRouteClearanceMillimetres))
            {
                penalty += context.Scenario.Panic.EscapeRoutePenaltyMillimetres;
            }

            if (geometry.RouteCrossesTable(position, centre))
            {
                penalty += context.Scenario.Panic.TableRoutePenaltyMillimetres;
            }

            return penalty;
        }

        /// <summary>Where someone waits to use a door: a stride back from it, on their side.</summary>
        private LogicalPosition ApproachPoint(int door, int fromRoom)
        {
            return geometry.DoorPointFrom(door, fromRoom, 0, -settings.ApproachInsetMillimetres);
        }

        /// <summary>
        /// Where to run for the chosen door: just short of it, or, once it is
        /// open and the person is lined up with the gap, through it; or,
        /// while giving way, just beside it.
        /// </summary>
        public LogicalPosition DoorTarget(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            LogicalPosition position = agent.Body.Position;
            int from = agent.Doors.ApproachRoom;
            int room = geometry.RoomAt(position);
            if (context.Tick < agent.Doors.GiveWayUntilTick && geometry.IsDoorOpen(door) && room >= 0)
            {
                // Standing aside, against the wall on their side of the gap.
                int radius = context.Scenario.World.OccupancyRadiusMillimetres;
                int aside = doors.WidthOf(door) / 2 + radius + settings.GiveWayAsideMillimetres;
                int sign = geometry.AlongOffset(door, position) < 0L ? -1 : 1;
                return geometry.DoorPointFrom(door, from, sign * aside, -settings.GiveWayInsetMillimetres);
            }

            if (geometry.IsDoorOpen(door) && (room < 0 || geometry.IsLinedUpToPassThrough(door, position)))
            {
                return geometry.DoorPointFrom(door, from, 0, settings.OutsideTargetMillimetres);
            }

            return ApproachPoint(door, from);
        }

        /// <summary>True while the person is close to the door they are running for.</summary>
        public bool IsNearExit(Agent agent, int distance)
        {
            if (agent.Doors.ExitDoorIndex < 0)
            {
                return false;
            }

            long limit = distance;
            return LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(agent.Doors.ExitDoorIndex)) <
                   limit * limit;
        }

        /// <summary>
        /// Heading through an open door: close to it (or already in the
        /// doorway), so swerves, hesitation and fresh decisions no longer matter.
        /// </summary>
        public bool IsLeaving(Agent agent)
        {
            return agent.Doors.ExitDoorIndex >= 0 &&
                   geometry.IsDoorOpen(agent.Doors.ExitDoorIndex) &&
                   (IsNearExit(agent, settings.CommitDistanceMillimetres) || geometry.RoomAt(agent.Body.Position) < 0);
        }

        /// <summary>Reached a shut door: time to try the handle.</summary>
        public bool HasReachedClosedExit(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            if (door < 0 || geometry.IsDoorOpen(door))
            {
                return false;
            }

            long arrival = settings.ArrivalDistanceMillimetres;
            long reach = settings.ApproachInsetMillimetres + settings.ArrivalDistanceMillimetres / 2;
            return LogicalPosition.DistanceSquared(agent.Body.Position, ApproachPoint(door, agent.Doors.ApproachRoom)) <
                   arrival * arrival ||
                   LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door)) < reach * reach;
        }

        /// <summary>Turn to face the door being tried.</summary>
        public MotorIntent FaceDoor(Agent agent)
        {
            return new MotorIntent(
                IntegerMath.HeadingBetween(agent.Body.Position, geometry.DoorCentre(agent.Doors.ExitDoorIndex), agent.Body.Heading),
                0,
                agent.Personality.PanicTurnRate,
                context.Scenario.Panic.Acceleration);
        }

        public void StartAttempt(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            agent.Body.BlockedTicks = 0;
            agent.Doors.AttemptEventId = context.Events.Append(
                context.Tick,
                agent.Id,
                FireReactionEventType.AgentTriedDoor,
                geometry.DoorCentre(door),
                0,
                0,
                agent.Fear.ScaredEventId,
                doors.IdOf(door)).EventId;
            if (doors.StateOf(door) == DoorState.Unlocked)
            {
                agent.Intent.Activity = AgentActivityState.OpeningDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + settings.DoorOpenTicks);
            }
            else
            {
                agent.Intent.Activity = AgentActivityState.TryingDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + settings.DoorTryTicks);
            }
        }

        public static bool IsAtDoor(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.OpeningDoor ||
                   activity == AgentActivityState.TryingDoor ||
                   activity == AgentActivityState.ForcingDoor;
        }

        /// <summary>
        /// Opening, rattling or forcing a door. Returns true while the person
        /// is still busy at the door this tick, in which case they keep facing
        /// it; otherwise the running behaviour decides how they move.
        /// </summary>
        public bool UpdateAttempt(Agent agent, bool inDanger)
        {
            int tick = context.Tick;
            int door = agent.Doors.ExitDoorIndex;
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            DoorState state = doors.StateOf(door);
            if (geometry.IsDoorOpen(door))
            {
                // Someone else got it open (or broke it down): go.
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            if (inDanger)
            {
                // The fire is too close to stand here: run for it.
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Doors.AvoidUntilTick[door] = checked(tick + context.Random.NextIntInclusive(
                    settings.DoorCrowdedAvoidMinimumTicks, settings.DoorCrowdedAvoidMaximumTicks));
                agent.Doors.ExitDoorIndex = -1;
                agent.Intent.NextPanicDecisionTick = tick;
                return false;
            }

            if (state == DoorState.Unlocked && agent.Intent.Activity != AgentActivityState.OpeningDoor)
            {
                // Unlocked while they were rattling it: it opens at once.
                doors.Open(door, agent.Doors.AttemptEventId);
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            switch (agent.Intent.Activity)
            {
                case AgentActivityState.OpeningDoor:
                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        doors.Open(door, agent.Doors.AttemptEventId);
                        agent.Intent.Activity = AgentActivityState.Fleeing;
                        return false;
                    }

                    return true;

                case AgentActivityState.TryingDoor:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    if (context.Random.NextPercent(TraitEffects.DoorForceChancePercent(agent, context.Scenario)))
                    {
                        agent.Intent.Activity = AgentActivityState.ForcingDoor;
                        agent.Intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(
                            settings.DoorForceMinimumTicks, settings.DoorForceMaximumTicks));
                        agent.Doors.NextShoveTick = tick + 1;
                    }
                    else
                    {
                        GiveUp(agent);
                    }

                    return true;

                default:
                    if (tick >= agent.Doors.NextShoveTick)
                    {
                        // A shoulder into the door: a thud, and usually nothing gives.
                        CausalEvent shove = context.Events.Append(
                            tick,
                            agent.Id,
                            FireReactionEventType.AgentForcedDoor,
                            doorCentre,
                            context.Scenario.Hearing.BumpSoundRadiusMillimetres,
                            0,
                            agent.Doors.AttemptEventId,
                            doors.IdOf(door));
                        sound.Thud(agent.Id, doorCentre, shove.EventId);
                        if (doors.Batter(door, agent, TraitEffects.DoorShoveDamage(agent, context.Scenario), shove.EventId))
                        {
                            // Battered enough: the door bursts off its hinges.
                            agent.Intent.Activity = AgentActivityState.Fleeing;
                            return false;
                        }

                        agent.Doors.NextShoveTick = checked(tick + context.Random.NextIntInclusive(
                            settings.DoorShoveMinimumTicks, settings.DoorShoveMaximumTicks));
                    }

                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        GiveUp(agent);
                    }

                    return true;
            }
        }

        /// <summary>
        /// This door will not open: remember that for a while, glance toward
        /// the next way out, then run for it.
        /// </summary>
        private void GiveUp(Agent agent)
        {
            int tick = context.Tick;
            int door = agent.Doors.ExitDoorIndex;
            context.Events.Append(tick, agent.Id, FireReactionEventType.AgentGaveUpOnDoor, geometry.DoorCentre(door),
                0, 0, agent.Doors.AttemptEventId, doors.IdOf(door));
            agent.Doors.AvoidUntilTick[door] = checked(tick + context.Random.NextIntInclusive(
                settings.DoorAvoidMinimumTicks, settings.DoorAvoidMaximumTicks));
            agent.Doors.FoundShut[door] = true;
            agent.Doors.ExitDoorIndex = -1;

            int next = ChooseExitDoor(agent);
            agent.Intent.Activity = AgentActivityState.Hesitating;
            agent.Intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(
                settings.GiveUpGlanceMinimumTicks, settings.GiveUpGlanceMaximumTicks));
            if (next >= 0)
            {
                agent.Intent.LookHeading = IntegerMath.HeadingBetween(agent.Body.Position,
                    ApproachPoint(next, agent.Doors.ApproachRoom), agent.Body.Heading);
            }
            else
            {
                int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                agent.Intent.LookHeading = IntegerMath.NormalizeDegrees(agent.Body.Heading + side * context.Random.NextIntInclusive(90, 150));
            }
        }

        /// <summary>
        /// Stuck right beside an open door without being lined up with the
        /// gap: step aside and let whoever is lined up go first, instead of
        /// everyone wedging against the frame at once. Returns false when
        /// this does not apply.
        /// </summary>
        public bool TryGiveWay(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            if (door < 0 || !geometry.IsDoorOpen(door) || geometry.RoomAt(agent.Body.Position) < 0 ||
                geometry.IsLinedUpToPassThrough(door, agent.Body.Position) ||
                !IsNearExit(agent, settings.ApproachInsetMillimetres + context.Scenario.World.OccupancyRadiusMillimetres))
            {
                return false;
            }

            agent.Body.BlockedTicks = 0;
            agent.Doors.GiveWayUntilTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.GiveWayMinimumTicks, settings.GiveWayMaximumTicks));
            return true;
        }

        /// <summary>Stuck in the crowd on the way to a door: try another for a little while.</summary>
        public void AvoidCrowdedExit(Agent agent)
        {
            if (agent.Doors.ExitDoorIndex < 0)
            {
                return;
            }

            agent.Doors.AvoidUntilTick[agent.Doors.ExitDoorIndex] = checked(context.Tick + context.Random.NextIntInclusive(
                settings.DoorCrowdedAvoidMinimumTicks, settings.DoorCrowdedAvoidMaximumTicks));
        }

        /// <summary>
        /// Whether to shut <paramref name="door"/> behind them, by personality:
        /// the evil shut it and lock it even with someone coming (only a body
        /// in the doorway stops them); the compassionate never shut it on
        /// someone coming; otherwise, with nobody coming, the nervous shut it,
        /// and so do the brave and kind if fire is getting near. Fire right
        /// outside the door of the room they are in makes anyone shut it.
        /// </summary>
        public void ConsiderClosing(Agent agent, int door, ulong causeEventId)
        {
            if (doors.StateOf(door) != DoorState.Open)
            {
                return;
            }

            AgentTraitValues traits = agent.Traits;
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            bool evil = traits.Evil >= settings.EvilCloseMinimum;
            int room = geometry.RoomAt(agent.Body.Position);
            bool fireAtDoor = room >= 0 && !fire.IsBurningInRoom(room) &&
                              fire.AnyCloserThan(doorCentre, settings.FireAtDoorRadiusMillimetres);
            bool shut;
            if (evil || fireAtDoor)
            {
                shut = true;
            }
            else if (SomeoneComing(agent, door, room))
            {
                shut = false;
            }
            else if (traits.Compassion >= settings.CompassionHoldMinimum && !fire.AnyCloserThan(doorCentre, settings.CloseFireRadiusMillimetres))
            {
                // Keeps it open for stragglers while the fire is still well away.
                shut = false;
            }
            else
            {
                shut = traits.Nervousness >= settings.NervousCloseMinimum ||
                       (traits.Bravery + traits.Compassion >= settings.BraveKindCloseSum &&
                        fire.AnyCloserThan(doorCentre, settings.CloseFireRadiusMillimetres));
            }

            if (!shut)
            {
                return;
            }

            ulong closed = doors.TryClose(door, agent.Id, causeEventId, agent);
            if (closed != 0UL && evil)
            {
                doors.Lock(door, agent, closed);
            }
        }

        /// <summary>Anyone else still in the run near the door, on the side the closer is not.</summary>
        private bool SomeoneComing(Agent closer, int door, int closerRoom)
        {
            long radius = settings.CloseApproachRadiusMillimetres;
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                if (other == closer || !other.IsParticipating ||
                    (closerRoom >= 0 && geometry.RoomAt(other.Body.Position) == closerRoom) ||
                    LogicalPosition.DistanceSquared(other.Body.Position, doorCentre) > radius * radius)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Fire next door: someone in a room with no fire in it, standing
        /// within reach of an open door with flames beyond, thinks about
        /// shutting it.
        /// </summary>
        public void ConsiderClosingAgainstFire(Agent agent, int room)
        {
            if (fire.IsBurningInRoom(room))
            {
                return;
            }

            long reach = settings.CloseReachMillimetres;
            int[] candidates = geometry.RoomDoors(room);
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                int beyond = geometry.RoomBeyond(door, room);
                if (!geometry.IsDoorOpen(door) || beyond < 0 || !fire.IsBurningInRoom(beyond) ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door)) > reach * reach)
                {
                    continue;
                }

                ConsiderClosing(agent, door, agent.Fear.ScaredEventId);
            }
        }

        /// <summary>
        /// Phase 6: who has walked into another room, and who is out of the
        /// building. Stepping into a new room is the moment people think
        /// about the door behind them; walking far enough out of an outside
        /// door is an escape, and they leave the run.
        /// </summary>
        public void ResolveRoomChangesAndEscapes()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                int room = geometry.RoomAt(agent.Body.Position);
                if (room >= 0 && room != agent.Doors.CurrentRoom)
                {
                    int previous = agent.Doors.CurrentRoom;
                    agent.Doors.CurrentRoom = room;
                    if (previous >= 0 && !agent.Burning.IsBurning)
                    {
                        ConsiderClosingBehind(agent, room, previous);
                    }
                }

                if (agent.Burning.IsBurning)
                {
                    // Someone on fire is past saving by any door.
                    continue;
                }

                int door = geometry.EscapedThrough(agent.Body.Position);
                if (door < 0)
                {
                    continue;
                }

                agent.Participation = AgentParticipation.NoLongerParticipating;
                agent.Outcome = AgentTerminalOutcome.Escaped;
                ulong escaped = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentEscaped,
                    agent.Body.Position, 0, 0, doors.OpenedEventIdOf(door), doors.IdOf(door)).EventId;
                agent.Doors.EscapedEventId = escaped;

                // Out: shut the door behind them, or leave it open for the others?
                ConsiderClosing(agent, door, escaped);
            }
        }

        /// <summary>Just through a door into the next room: shut it behind them, or leave it for the others?</summary>
        private void ConsiderClosingBehind(Agent agent, int room, int previousRoom)
        {
            long reach = settings.CloseReachMillimetres;
            int[] candidates = geometry.RoomDoors(room);
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                if (geometry.RoomBeyond(door, room) != previousRoom || !geometry.IsDoorOpen(door) ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door)) > reach * reach)
                {
                    continue;
                }

                ConsiderClosing(agent, door, agent.Fear.ScaredEventId);
            }
        }
    }
}
