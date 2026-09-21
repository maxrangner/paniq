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

        /// <summary>Set once the objects exist, for heaving whatever is wedged in a doorway.</summary>
        private PhysicsObjectSystem objects;

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
                    !geometry.TryFindRoute(room, position, geometry.DoorRoom(d), agent, out int first, out int last, out long routeCost))
                {
                    continue;
                }

                // The door to run for now: the way out itself when it is in
                // this room, otherwise the first door along the way.
                int next = first < 0 ? d : first;
                bool open = geometry.IsDoorOpen(next);
                if (!open && (context.Tick < agent.Doors.AvoidUntilTick[next] ||
                              agent.Doors.FoundShut[next] || agent.Doors.FoundShut[d]))
                {
                    // A door they have already found shut is no longer a way
                    // out to them: either the door they would walk at now (a
                    // shut door partway along blocks the route just as surely)
                    // or the way out at the end of it.
                    continue;
                }

                // The whole walk: to the first door, room to room, and
                // across the last room to the way out itself.
                LogicalPosition approach = ApproachPoint(d, geometry.DoorRoom(d));
                long walk = first < 0
                    ? IntegerMath.Distance(position, approach)
                    : routeCost + IntegerMath.Distance(geometry.DoorCentre(last), approach);
                long score = context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) - walk;
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

                // Nobody walks into the next room, or all the way to a
                // door in a far room, while that room is alight.
                int into = geometry.RoomBeyond(next, room);
                if ((into >= 0 && fire.IsBurningInRoom(into)) || fire.IsBurningInRoom(geometry.DoorRoom(d)))
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
            long bestScore = RefugeScore(room, 0L) + settings.CurrentRoomBonusMillimetres;
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (r == room ||
                    !geometry.TryFindRoute(room, position, r, agent, out int first, out int last, out long routeCost) ||
                    first < 0)
                {
                    continue;
                }

                routeCost += IntegerMath.Distance(geometry.DoorCentre(last), geometry.RoomBounds(r).Centre);

                if ((!geometry.IsDoorOpen(first) && context.Tick < agent.Doors.AvoidUntilTick[first]) || IsRoomFull(r, agent))
                {
                    continue;
                }

                int into = geometry.RoomBeyond(first, room);
                long score = RefugeScore(r, routeCost) +
                             context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) -
                             RoutePenalties(agent, position, first) -
                             (into >= 0 && fire.IsBurningInRoom(into) ? settings.InFirePenaltyMillimetres : 0L);
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

        /// <summary>
        /// How good a room looks to get away to: how far its middle is from
        /// the nearest flames, plus a bonus if nothing in it is alight, less
        /// half the walk there. A room already burning is worth nothing.
        /// </summary>
        private long RefugeScore(int room, long routeCost)
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
            else
            {
                score += settings.RefugeClearRoomMillimetres;
            }

            return score - routeCost / 2L;
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
            if (context.Tick < agent.Doors.GiveWayUntilTick && geometry.IsDoorOpen(door))
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
            if (doors.StateOf(door) == DoorState.Unlocked && !doors.IsObstructed(door))
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
                   activity == AgentActivityState.ForcingDoor ||
                   activity == AgentActivityState.ShovingObstruction;
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

            if (state == DoorState.Unlocked && !doors.IsObstructed(door) &&
                agent.Intent.Activity != AgentActivityState.OpeningDoor)
            {
                // Unlocked while they were rattling it: it opens at once.
                doors.Open(door, agent.Doors.AttemptEventId);
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            switch (agent.Intent.Activity)
            {
                case AgentActivityState.OpeningDoor:
                    if (doors.IsObstructed(door))
                    {
                        // Wedged while they were pulling at it: it will not come.
                        agent.Intent.Activity = AgentActivityState.TryingDoor;
                        agent.Intent.ActivityEndTick = checked(tick + settings.DoorTryTicks);
                        return true;
                    }

                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        doors.Open(door, agent.Doors.AttemptEventId);
                        agent.Intent.Activity = AgentActivityState.Fleeing;
                        return false;
                    }

                    return true;

                case AgentActivityState.ShovingObstruction:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    HeaveObstructionClear(agent, door);
                    agent.Intent.Activity = AgentActivityState.TryingDoor;
                    agent.Intent.ActivityEndTick = checked(tick + settings.DoorTryTicks);
                    return true;

                case AgentActivityState.TryingDoor:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    if (doors.IsObstructed(door))
                    {
                        // Something is wedged against it. Somebody strong heaves
                        // it clear; anybody else gives up as they would on a
                        // locked door.
                        if (agent.Traits.Strength >= context.Scenario.Blockades.ShoveMinimumStrength)
                        {
                            agent.Intent.Activity = AgentActivityState.ShovingObstruction;
                            agent.Intent.ActivityEndTick = checked(tick + context.Scenario.Blockades.ShoveTicks);
                        }
                        else
                        {
                            GiveUp(agent);
                        }

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

                    if (tick >= agent.Intent.ActivityEndTick &&
                        !LeaderBehaviour.IsUnderOrdersAtThisDoor(agent, door, tick))
                    {
                        // Sent at this door by somebody: they keep at it.
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
        /// Stuck at an open door, whether wedged beside the gap or nose to
        /// nose with somebody in it: step aside for a moment and try again,
        /// instead of everyone leaning on each other in the doorway. Returns
        /// false when this does not apply.
        /// </summary>
        public bool TryGiveWay(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            // About a metre of the door: close enough that they are part of
            // the crush at it rather than still on their way.
            if (door < 0 || !geometry.IsDoorOpen(door) ||
                !IsNearExit(agent, settings.ApproachInsetMillimetres + context.Scenario.World.OccupancyRadiusMillimetres * 2))
            {
                return false;
            }


            agent.Body.BlockedTicks = 0;
            agent.Doors.GiveWayUntilTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.GiveWayMinimumTicks, settings.GiveWayMaximumTicks));
            return true;
        }

        /// <summary>
        /// Heaves whatever is wedged in a doorway out along the wall, to the side
        /// the heaver is standing, because nothing ever goes through a doorway.
        /// The stronger they are, the further it goes.
        /// </summary>
        private void HeaveObstructionClear(Agent agent, int door)
        {
            int thing = doors.ObstructionIn(door);
            if (thing < 0)
            {
                return;
            }

            BlockadeSettings blockades = context.Scenario.Blockades;
            long offset = geometry.AlongOffset(door, agent.Body.Position);
            int side = offset < 0L ? -1 : 1;
            int along = geometry.AlongWallHeading(door, side);
            int speed = blockades.ShoveSpeedBase + blockades.ShoveSpeedPerStrength * agent.Traits.Strength;
            objects.ShoveAside(thing, agent, along, speed, agent.Doors.AttemptEventId);
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
        /// The door they have just come through: only the cruel shut it behind
        /// them, and they do it whoever is running up (only a body in the
        /// doorway stops them). The cruellest turn the key as well. Everyone
        /// else leaves it for the people behind them — unless the room they
        /// have just left is alight, which is
        /// <see cref="ConsiderShuttingAgainstFire"/>'s business, not spite.
        /// </summary>
        /// <summary>Wired up after construction, because the objects are built after this behaviour.</summary>
        public void UseObjects(PhysicsObjectSystem physicsObjects) => objects = physicsObjects;

        public void ConsiderSlammingBehind(Agent agent, int door, int previousRoom, ulong causeEventId)
        {
            if (doors.StateOf(door) != DoorState.Open)
            {
                return;
            }

            AgentTraitValues traits = agent.Traits;
            if (traits.Evil < settings.EvilCloseMinimum)
            {
                // Not cruel: the only reason left to shut it is the fire.
                if (previousRoom >= 0 && fire.IsBurningInRoom(previousRoom))
                {
                    ConsiderShuttingAgainstFire(agent, door, causeEventId);
                }

                return;
            }

            ulong closed = doors.TryClose(door, agent.Id, causeEventId, agent);
            if (closed == 0UL)
            {
                return;
            }

            if (traits.Evil >= settings.EvilLockMinimum)
            {
                doors.Lock(door, agent, closed);
            }

            // They know perfectly well what they just did. Without this they
            // forget at once, pick the same door on their next thought, walk
            // back and hammer on a door they shut themselves.
            RememberShutting(agent, door);
        }

        /// <summary>
        /// Marks a door this person has shut or locked themselves as one they
        /// will not head back to for a while. The same memory
        /// <see cref="GiveUp"/> writes when a door beats them, because the
        /// outcome is the same: to them, that door is not a way out.
        /// </summary>
        private void RememberShutting(Agent agent, int door)
        {
            agent.Doors.FoundShut[door] = true;
            agent.Doors.AvoidUntilTick[door] = checked(context.Tick + context.Random.NextIntInclusive(
                settings.DoorAvoidMinimumTicks, settings.DoorAvoidMaximumTicks));
            if (agent.Doors.ExitDoorIndex == door)
            {
                agent.Doors.ExitDoorIndex = -1;
            }
        }

        /// <summary>
        /// A door with fire beyond it, and they are standing in a room that is
        /// not alight: anyone shuts that, cruel or not, because it is the fire
        /// they are shutting out and not the people. The kind hold it open
        /// while somebody is still coming through — but not once the flames are
        /// right at the door.
        /// </summary>
        public void ConsiderShuttingAgainstFire(Agent agent, int door, ulong causeEventId)
        {
            if (doors.StateOf(door) != DoorState.Open)
            {
                return;
            }

            int room = geometry.RoomAt(agent.Body.Position);
            if (room < 0 || fire.IsBurningInRoom(room))
            {
                // Their own room is alight: shutting this door saves nobody.
                return;
            }

            LogicalPosition doorCentre = geometry.DoorCentre(door);
            bool flamesAtTheDoor = fire.AnyCloserThan(doorCentre, settings.FireAtDoorRadiusMillimetres);
            if (!flamesAtTheDoor &&
                agent.Traits.Compassion >= settings.CompassionHoldMinimum &&
                SomeoneComing(agent, door, room))
            {
                // Holding it for whoever is still coming through.
                return;
            }

            doors.TryClose(door, agent.Id, causeEventId, agent);
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

                // Never the door they are about to walk through themselves.
                if (door == agent.Doors.ExitDoorIndex || !geometry.IsDoorOpen(door) || beyond < 0 ||
                    !fire.IsBurningInRoom(beyond) ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door)) > reach * reach)
                {
                    continue;
                }

                ConsiderShuttingAgainstFire(agent, door, agent.Fear.ScaredEventId);
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
                        ConsiderSlammingTheDoorBehind(agent, room, previous);
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

                // Out: only the cruel shut it behind them.
                ConsiderSlammingBehind(agent, door, agent.Doors.CurrentRoom, escaped);
            }
        }

        /// <summary>Just through a door into the next room: the cruel shut it behind them, and the fire makes anyone shut it.</summary>
        private void ConsiderSlammingTheDoorBehind(Agent agent, int room, int previousRoom)
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

                ConsiderSlammingBehind(agent, door, previousRoom, agent.Fear.ScaredEventId);
            }
        }
    }
}
