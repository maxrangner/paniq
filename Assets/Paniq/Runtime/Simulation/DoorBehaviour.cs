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
    internal sealed class DoorBehaviour : IBindable
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly Threats threats;
        private readonly SoundSystem sound;
        private readonly ExitSignBehaviour exitSigns;
        private readonly WayfindingSystem wayfinding;
        private readonly ExitSettings settings;

        /// <summary>Set once the objects exist, for heaving whatever is wedged in a doorway.</summary>
        private PhysicsObjectSystem objects;

        public DoorBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            DoorSystem doors,
            Threats threats,
            SoundSystem sound,
            ExitSignBehaviour exitSigns,
            WayfindingSystem wayfinding,
            GroupSystem groups)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.threats = threats;
            this.sound = sound;
            this.exitSigns = exitSigns;
            this.wayfinding = wayfinding;
            this.groups = groups;
            settings = context.Scenario.Exits;
        }

        /// <summary>Who is sticking together with whom, for the door the group's anchor picks.</summary>
        private readonly GroupSystem groups;

        /// <summary>
        /// Whether this person is on their way to a way out they can see
        /// standing open. The one question eagerness turns on, and cheap to ask
        /// because the route search already wrote down which way out it was.
        /// </summary>
        public bool IsSetOnAWayOut(Agent agent)
        {
            return agent.Doors.ExitDoorIndex >= 0 &&
                   agent.Doors.WayOutDoorIndex >= 0 &&
                   geometry.IsDoorOpen(agent.Doors.WayOutDoorIndex);
        }

        /// <summary>
        /// Phase 10, once the tick has settled: a door that opened this tick is
        /// news. It is the loudest thing that happens in this game -- the player's
        /// one real move -- and until now nobody noticed until their own next
        /// decision came round, up to a second and a bit later.
        ///
        /// Two tiers, deliberately. Everybody who could still walk there stops
        /// believing that door would not open, wherever they are standing: that
        /// memory was true once and plainly is not now, and a false memory is a
        /// bug rather than a personality. But only the people in the door's own
        /// room, or the room straight through it, drop what they are doing and
        /// think again on the next tick -- the ones who would have seen or heard
        /// it go. Everybody else keeps walking their current plan, which is no
        /// longer poisoned, until their own next decision. Otherwise a whole
        /// building turns on its heel the instant a latch clicks two rooms away,
        /// which reads worse than the problem it fixes.
        ///
        /// Those same people learn the door is there, if they did not know: a
        /// door swinging open, or a wall blown out, beside you is not something
        /// anybody misses, whether or not they knew the building.
        ///
        /// Ascending door index, then ascending agent index, and no random
        /// numbers: this adds no randomness of its own.
        /// </summary>
        public void AnnounceWaysOut()
        {
            int openings = doors.OpeningsThisTick;
            if (openings == 0)
            {
                return;
            }

            Agent[] people = crowd.All;
            for (int i = 0; i < openings; i++)
            {
                int door = doors.OpeningAt(i);
                int side = geometry.DoorRoom(door);
                int beyond = geometry.RoomBeyond(door, side);
                for (int a = 0; a < people.Length; a++)
                {
                    Agent agent = people[a];
                    if (!agent.IsParticipating || agent.Burning.IsBurning)
                    {
                        continue;
                    }

                    // Somebody has opened it, so whoever shut it last has had
                    // their work undone: it is not their handiwork any more, and
                    // if it is shut again it was shut by somebody else.
                    agent.Doors.ShutByThem[door] = false;

                    int room = geometry.RoomOf(agent);
                    if (room >= 0 &&
                        geometry.TryFindRoute(room, agent.Body.Position, side, agent, out _, out _, out _))
                    {
                        agent.Doors.FoundShut[door] = false;
                        agent.Doors.AvoidUntilTick[door] = 0;
                    }

                    if (room == side || (beyond >= 0 && room == beyond))
                    {
                        context.ThinkAgainSoon(agent.Intent);
                        wayfinding.Learn(agent, door, WayLearned.SawItOpen, 0UL);
                    }
                }
            }

            doors.ClearOpenings();
        }

        /// <summary>
        /// The door to head through next, or -1 when nowhere is better than
        /// where they stand. Ways out of the building are scored by how far
        /// it is to walk there through the rooms; the first door on that walk
        /// is the one they run for. With no way out left, they pick the room
        /// furthest from the fire instead.
        /// <para>
        /// Only a way out they know of counts, walked through doors they know
        /// of. For somebody who knows the building that is all of them. A
        /// visitor who knows of no way out goes looking for one before they
        /// give up and hide (<see cref="TryChooseSearch"/>).
        /// </para>
        /// </summary>
        public int ChooseExitDoor(Agent agent)
        {
            agent.Doors.HasLookedForAWayOut = true;
            LogicalPosition position = agent.Body.Position;
            int room = geometry.RoomAt(position);
            if (room < 0)
            {
                // Halfway through a doorway: keep going.
                return agent.Doors.ExitDoorIndex;
            }

            agent.Doors.ApproachRoom = room;
            agent.Knowledge.HasSearchSpot = false;
            int groupDoor = groups.AnchorExitDoor(agent);
            int best = -1;
            int bestWayOut = -1;
            bool bestIsThroughTheHeat = false;
            long bestScore = long.MinValue;
            for (int d = 0; d < doors.Count; d++)
            {
                if (!geometry.DoorLeadsOutside(d) || !agent.Knowledge.Knows(d) ||
                    !geometry.TryFindKnownRoute(room, position, geometry.DoorRoom(d), agent, out int first, out int last, out long routeCost))
                {
                    continue;
                }

                // The door to run for now: the way out itself when it is in
                // this room, otherwise the first door along the way.
                int next = first < 0 ? d : first;
                bool open = geometry.IsDoorOpen(next);
                if (context.Tick < agent.Doors.AvoidUntilTick[next])
                {
                    // They have given up on this way for the moment. That
                    // happens for two reasons -- the door would not open, or
                    // the crush at it was not moving -- and only the first of
                    // them is about the door being shut. Checking it only for a
                    // shut door meant somebody wedged on the approach to an
                    // open one gave up on it, immediately picked it again, and
                    // stood there until the building burned down.
                    continue;
                }

                // A way out standing open is never crossed off: remembering
                // that it would not budge was true once and plainly is not
                // now, and nobody walks past an open door to the street
                // because they tried the handle five minutes ago. Only the
                // way out's own memory is forgiven, though -- a door partway
                // along that they walked at and could not shift, or shut
                // themselves, still blocks the route as surely as it ever did.
                bool wayOutIsOpen = geometry.IsDoorOpen(d);
                if (!open && (agent.Doors.FoundShut[next] || (agent.Doors.FoundShut[d] && !wayOutIsOpen)))
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
                if (wayOutIsOpen && !threats.IsInRoom(geometry.DoorRoom(d)))
                {
                    // A way out you can see standing open is worth more than
                    // any walk in this building -- but not if the room it is
                    // in is alight, or this would march people into the flames.
                    score += settings.OpenBonusMillimetres;
                }

                if (next == agent.Doors.ExitDoorIndex)
                {
                    score += settings.CurrentChoiceBonusMillimetres;
                }

                if (groupDoor >= 0 && next == groupDoor)
                {
                    // The door the rest of the group is going for: worth a
                    // walk to keep together, though not a walk through fire.
                    score += context.Scenario.Groups.ChoiceBonusMillimetres;
                }

                score -= RoutePenalties(agent, position, next);

                // Through the heat: the flames are at the door they would
                // walk at now, or in the room beyond it.
                LogicalPosition nextApproach = first < 0 ? approach : ApproachPoint(next, room);
                int into = geometry.RoomBeyond(next, room);
                bool throughTheHeat = threats.AnyCloserThan(nextApproach, TraitEffects.DangerDistance(agent, context.Scenario)) ||
                                      (into >= 0 && threats.IsInRoom(into));
                if (throughTheHeat)
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                // Nobody walks all the way to a door in a far room while that
                // room is alight.
                if (threats.IsInRoom(geometry.DoorRoom(d)))
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = next;
                    bestWayOut = d;
                    bestIsThroughTheHeat = throughTheHeat;
                }
            }

            if (best >= 0 && bestIsThroughTheHeat && !DecidesToDash(agent, room, position, best))
            {
                // Too hot for them: that door is given up for a while, and
                // what follows is the same as for somebody with no way out
                // left -- somewhere they have not looked, or somewhere to
                // hide. A shut door in a dead end buys the time the player
                // may still turn into a rescue.
                agent.Doors.AvoidUntilTick[best] = checked(context.Tick + context.Random.NextIntInclusive(
                    settings.DoorAvoidMinimumTicks, settings.DoorAvoidMaximumTicks));
                if (agent.Doors.HidFromHeatAtDoor != best)
                {
                    agent.Doors.HidFromHeatAtDoor = best;
                    context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentHidFromTheHeat, position, 0, 0,
                        agent.Fear.ScaredEventId, doors.IdOf(best));
                }

                best = -1;
            }

            if (best >= 0)
            {
                agent.Doors.WayOutDoorIndex = bestWayOut;
                agent.Knowledge.Searching = false;
                if (!bestIsThroughTheHeat)
                {
                    agent.Doors.HidFromHeatAtDoor = -1;
                }

                return best;
            }

            agent.Doors.WayOutDoorIndex = -1;
            if (!agent.Knowledge.KnowsEverything && TryChooseSearch(agent, room, position, out int search))
            {
                // Somewhere they have not looked yet: a door, or -1 with a
                // spot in this room to look round from.
                return search;
            }

            // Every way out has been tried and would not open, or is too hot
            // to go for, and there is nowhere left to look: get into whichever
            // room is furthest from the flames instead -- unless the walk to
            // it crosses burning floor, in which case they keep clear of the
            // flames where they are. They used to head for a stall through the
            // fire, bolt back from the heat, pick the stall again, and so on
            // until it reached them.
            int refuge = ChooseRefugeDoor(agent, room, position);
            if (refuge >= 0 && !FloorIsClear(position, ApproachPoint(refuge, room)))
            {
                return -1;
            }

            return refuge;
        }

        /// <summary>
        /// The way out is through the heat. Whether they run for it: the floor
        /// between here and the door has to be walkable (no burning square
        /// within the escape clearance of the straight line), and they have to
        /// be brave enough -- or standing in a room that is itself alight,
        /// where staying is the worse bet. A dash lasts a few seconds and is
        /// then decided again; while it lasts they neither bolt from the
        /// flames at their danger distance nor abandon the door for the heat
        /// at it. The owner's choice (2026-09-24): dash past or hide, by
        /// bravery.
        /// </summary>
        private bool DecidesToDash(Agent agent, int room, LogicalPosition position, int door)
        {
            int tick = context.Tick;
            if (tick < agent.Doors.DashingUntilTick)
            {
                // Already running for it: keep going while the choice stands.
                return true;
            }

            if (!FloorIsClear(position, ApproachPoint(door, room)))
            {
                // Across burning floor: nobody, however brave or desperate.
                return false;
            }

            bool nerve = agent.Traits.Bravery >= settings.DashMinimumBravery || threats.IsInRoom(room);
            if (!nerve && CoolRefugeIsReachable(agent, room, position))
            {
                // Not brave enough, and there is somewhere to hide that is not
                // through the heat: they hide.
                return false;
            }

            agent.Doors.DashingUntilTick = checked(tick + context.Jittered(settings.DashTicks));
            agent.Doors.HidFromHeatAtDoor = -1;
            context.Events.Append(tick, agent.Id, CausalEventType.AgentDashedThroughHeat, position, 0, 0,
                agent.Fear.ScaredEventId, doors.IdOf(door));
            return true;
        }

        /// <summary>Whether a straight walk from here to there keeps off burning floor: no burning square within the dash clearance of the line, or of the spot itself.</summary>
        private bool FloorIsClear(LogicalPosition from, LogicalPosition to)
        {
            int clearance = settings.DashClearanceMillimetres;
            return !threats.AnyCloserThan(to, clearance) && !threats.RoutePassesNear(from, to, clearance);
        }

        /// <summary>
        /// Whether somebody timid has somewhere to hide that is not itself
        /// through the heat: the refuge they would pick, with its approach
        /// outside their danger distance and the walk to it clear of that
        /// too. Staying where they are counts, when their own room is not
        /// alight. With no such place, hiding means shuttling at the edge of
        /// the heat until it reaches them, and running for the door is the
        /// better bet -- the desperate dash the owner asked for.
        /// </summary>
        private bool CoolRefugeIsReachable(Agent agent, int room, LogicalPosition position)
        {
            int refuge = ChooseRefugeDoor(agent, room, position);
            if (refuge < 0)
            {
                return !threats.IsInRoom(room);
            }

            LogicalPosition approach = ApproachPoint(refuge, room);
            int danger = TraitEffects.DangerDistance(agent, context.Scenario);
            return !threats.AnyCloserThan(approach, danger) && !threats.RoutePassesNear(position, approach, danger);
        }

        /// <summary>Whether they are, right now, running for a way out through the heat.</summary>
        public bool IsDashing(Agent agent) => context.Tick < agent.Doors.DashingUntilTick;

        /// <summary>
        /// A visitor who knows of no way out looks for one. Two kinds of place
        /// are worth a look: the part of this room they have not seen yet, and
        /// any room they know how to reach but have not looked round. The room
        /// they are standing in comes first: having walked in to look round
        /// it, they look round it before any other room is weighed, unless its
        /// unseen corner is by the danger or cannot be reached. Otherwise each
        /// place is scored like a way out -- the shorter walk, the way a sign
        /// they can see points, clear of the danger, and a little for sticking
        /// with what they already chose -- with a little noise. False when
        /// there is nowhere left to look.
        /// <para>
        /// On the way, a door they were looking for turns up, or a sign, or a
        /// leader; any of those makes them think again at once (see
        /// <see cref="WayfindingSystem"/>), and from then on they are running
        /// for the way out like anybody else.
        /// </para>
        /// <para>
        /// Draws one random number per place considered, in a fixed order:
        /// this room first, then the others by ascending index. Nobody who
        /// knows the building ever gets here.
        /// </para>
        /// </summary>
        private bool TryChooseSearch(Agent agent, int room, LogicalPosition position, out int door)
        {
            door = -1;
            AgentKnowledge knowledge = agent.Knowledge;
            PanicSettings panic = context.Scenario.Panic;
            bool readASign = exitSigns.TryRead(agent, out int pointing);
            int danger = TraitEffects.DangerDistance(agent, context.Scenario);
            bool found = false;
            bool lookHereFirst = false;
            long bestScore = long.MinValue;

            if (!knowledge.HasLookedOver(room))
            {
                LogicalPosition spot = UnseenCorner(knowledge, room, position, panic.EscapeWallMarginMillimetres);
                long score = context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) -
                             IntegerMath.Distance(position, spot);
                if (threats.RoutePassesNear(position, spot, panic.EscapeRouteClearanceMillimetres))
                {
                    score -= panic.EscapeRoutePenaltyMillimetres;
                }

                if (threats.AnyCloserThan(spot, danger))
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                if (readASign)
                {
                    score += exitSigns.ScoreToward(position, spot, pointing);
                }

                if (knowledge.Searching && agent.Doors.ExitDoorIndex < 0 && agent.Intent.Target.Equals(spot))
                {
                    score += settings.CurrentChoiceBonusMillimetres;
                }

                found = true;
                bestScore = score;
                knowledge.HasSearchSpot = true;
                knowledge.SearchSpot = spot;

                // Without this, a room whose far corners are further off than
                // the next room's door was left the moment it was entered, and
                // from the corridor that same room was the nearest place
                // unseen, so a stranger bounced through one doorway until the
                // building burned down. Costing routes as real walks made that
                // a certainty on the shipped floor; it had been a matter of luck.
                lookHereFirst = !threats.AnyCloserThan(spot, danger) &&
                                geometry.Routes.CanGetFromHereToThere(position, spot,
                                    context.Scenario.World.OccupancyRadiusMillimetres);
            }

            for (int r = 0; !lookHereFirst && r < geometry.RoomCount; r++)
            {
                if (r == room || knowledge.HasLookedOver(r) ||
                    !geometry.TryFindKnownRoute(room, position, r, agent, out int first, out _, out long routeCost) ||
                    first < 0)
                {
                    continue;
                }

                if (!geometry.IsDoorOpen(first) &&
                    (context.Tick < agent.Doors.AvoidUntilTick[first] || agent.Doors.FoundShut[first]))
                {
                    continue;
                }

                LogicalPosition doorway = geometry.DoorCentre(first);
                long score = context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) - routeCost -
                             RoutePenalties(agent, position, first);
                int into = geometry.RoomBeyond(first, room);
                if ((into >= 0 && threats.IsInRoom(into)) || threats.IsInRoom(r))
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                if (readASign)
                {
                    score += exitSigns.ScoreToward(position, doorway, pointing);
                }

                if (first == agent.Doors.ExitDoorIndex)
                {
                    score += settings.CurrentChoiceBonusMillimetres;
                }

                if (score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    door = first;
                    knowledge.HasSearchSpot = false;
                }
            }

            if (found && !knowledge.Searching)
            {
                knowledge.Searching = true;
                knowledge.SearchEventId = context.Events.Append(context.Tick, agent.Id,
                    CausalEventType.AgentLookedForAWayOut, position, 0, 0, agent.Fear.ScaredEventId).EventId;
            }

            return found;
        }

        /// <summary>
        /// Where to stand to see the nearest corner of this room they have not
        /// seen yet: that corner, drawn in from the walls by the margin (less
        /// in a small room). Ties go south-west, south-east, north-west,
        /// north-east, the order the corners are numbered in.
        /// </summary>
        private LogicalPosition UnseenCorner(AgentKnowledge knowledge, int room, LogicalPosition position, int margin)
        {
            LogicalBounds b = geometry.RoomBounds(room);
            margin = System.Math.Min(margin, System.Math.Min(b.MaxX - b.MinX, b.MaxZ - b.MinZ) / 2);
            byte seen = knowledge.CornersSeen[room];
            LogicalPosition best = position;
            long bestDistance = long.MaxValue;
            for (int corner = 0; corner < 4; corner++)
            {
                if ((seen & (1 << corner)) != 0)
                {
                    continue;
                }

                var spot = new LogicalPosition(
                    (corner & 1) == 0 ? b.MinX + margin : b.MaxX - margin,
                    (corner & 2) == 0 ? b.MinZ + margin : b.MaxZ - margin);
                long distance = LogicalPosition.DistanceSquared(position, spot);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best;
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
                    !geometry.TryFindKnownRoute(room, position, r, agent, out int first, out int last, out long routeCost) ||
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
                             (into >= 0 && threats.IsInRoom(into) ? settings.InFirePenaltyMillimetres : 0L);
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

            // Everybody who counts as being in the room stands inside it or in
            // one of its doorways, and a doorway to the street reaches the
            // doorway depth past the wall: so the room grown by that depth
            // holds every candidate, and the index reads only those.
            int inside = 0;
            using (Crowd.Nearby people = crowd.Gather(geometry.RoomAreaWithDoorways(room)))
            {
                for (int c = 0; c < people.Count; c++)
                {
                    Agent other = crowd.All[people[c]];
                    if (other != hopeful && other.IsParticipating && geometry.RoomOf(other) == room)
                    {
                        inside++;
                    }
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
            long fireDistanceSquared = threats.NearestDistanceSquared(middle, out _, out _);
            long score = fireDistanceSquared == long.MaxValue
                ? settings.RefugeNoFireMillimetres
                : IntegerMath.Sqrt(fireDistanceSquared);
            if (threats.IsInRoom(room))
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
            if (threats.RoutePassesNear(position, centre, context.Scenario.Panic.EscapeRouteClearanceMillimetres))
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
            if (context.Tick < agent.Doors.GiveWayUntilTick && GivesWayAt(agent, door) &&
                SomebodyElseLinedUpAt(agent, door))
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
            return agent.Doors.ExitDoorIndex >= 0 && !HasPassedThrough(agent) &&
                   geometry.IsDoorOpen(agent.Doors.ExitDoorIndex) &&
                   (IsNearExit(agent, settings.CommitDistanceMillimetres) || geometry.RoomAt(agent.Body.Position) < 0);
        }

        /// <summary>
        /// Already standing in the room on the far side of the door they were
        /// heading for: that door is done with. Without this, somebody knocked
        /// off their line just after stepping through goes back to the near side
        /// to walk through it again -- and, being close to the door, is too
        /// committed to it to think again, so they shuttle back and forth.
        /// </summary>
        public bool HasPassedThrough(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            int from = agent.Doors.ApproachRoom;
            if (door < 0 || from < 0)
            {
                return false;
            }

            int room = geometry.RoomAt(agent.Body.Position);
            return room >= 0 && room != from && room == geometry.RoomBeyond(door, from);
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
                CausalEventType.AgentTriedDoor,
                geometry.DoorCentre(door),
                0,
                0,
                agent.Fear.ScaredEventId,
                doors.IdOf(door)).EventId;
            if (doors.CanBePushedOpen(door))
            {
                agent.Intent.Activity = AgentActivityState.OpeningDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(settings.DoorOpenTicks));
            }
            else
            {
                agent.Intent.Activity = AgentActivityState.TryingDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(settings.DoorTryTicks));
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
                context.ThinkAgainSoon(agent.Intent);
                return false;
            }

            if (doors.CanBePushedOpen(door) && agent.Intent.Activity != AgentActivityState.OpeningDoor)
            {
                // Unlocked, or let go of, while they were rattling it: it opens at once.
                doors.Open(door, agent.Doors.AttemptEventId, geometry.SideOf(door, agent.Body.Position));
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            switch (agent.Intent.Activity)
            {
                case AgentActivityState.OpeningDoor:
                    if (!doors.CanBePushedOpen(door))
                    {
                        // Wedged, or taken hold of, while they were pulling at it: it will not come.
                        agent.Intent.Activity = AgentActivityState.TryingDoor;
                        agent.Intent.ActivityEndTick = checked(tick + context.Jittered(settings.DoorTryTicks));
                        return true;
                    }

                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        doors.Open(door, agent.Doors.AttemptEventId, geometry.SideOf(door, agent.Body.Position));
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
                    agent.Intent.ActivityEndTick = checked(tick + context.Jittered(settings.DoorTryTicks));
                    return true;

                case AgentActivityState.TryingDoor:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    if (doors.IsObstructed(door))
                    {
                        // Something is wedged against it. Anybody who can lift
                        // it grabs it and throws it clear -- nobody needs to be
                        // told to save their own life. Too heavy for them:
                        // somebody strong heaves it along the wall instead, and
                        // anybody else gives up as they would on a locked door.
                        int thing = doors.ObstructionIn(door);
                        if (thing >= 0 && CanReachToThrowClear(agent, thing))
                        {
                            objects.ThrowClear(agent, thing, ClearAwayHeading(agent, doorCentre), agent.Doors.AttemptEventId);
                            agent.Intent.ActivityEndTick = checked(tick + context.Jittered(settings.DoorTryTicks));
                            return true;
                        }

                        if (agent.Traits.Strength >= context.Scenario.Blockades.ShoveMinimumStrength)
                        {
                            agent.Intent.Activity = AgentActivityState.ShovingObstruction;
                            agent.Intent.ActivityEndTick = checked(tick + context.Jittered(context.Scenario.Blockades.ShoveTicks));
                        }
                        else
                        {
                            GiveUp(agent, false);
                        }

                        return true;
                    }

                    if (doors.IsHeldShut(door) && state == DoorState.Unlocked)
                    {
                        // The player is holding it shut (the owner's rule,
                        // 2026-09-25): somebody strong enough to batter a door
                        // at all gets through a held one in a single push, and
                        // it is off its hinges for good; everybody else rattles
                        // it, gives up, and comes back once it is let go of.
                        // Whether they once shut it themselves does not come
                        // into it: it is the player's hand holding it now, not
                        // their own doing. A held door somebody has also
                        // locked is a locked door, and battered as one below.
                        if (TraitEffects.DoorShoveDamage(agent, context.Scenario) > 0)
                        {
                            CausalEvent push = context.Events.Append(
                                tick,
                                agent.Id,
                                CausalEventType.AgentForcedDoor,
                                doorCentre,
                                context.Scenario.Hearing.BumpSoundRadiusMillimetres,
                                0,
                                agent.Doors.AttemptEventId,
                                doors.IdOf(door));
                            sound.Thud(agent.Id, doorCentre, push.EventId);
                            doors.Batter(door, agent, context.Scenario.Exits.DoorStrength, push.EventId);
                            agent.Intent.Activity = AgentActivityState.Fleeing;
                            return false;
                        }

                        GiveUp(agent, false);
                        return true;
                    }

                    // A door they shut themselves they never batter, however long
                    // ago it was and however badly it has trapped them: they
                    // give up on it as on any door that will not open.
                    if (!agent.Doors.ShutByThem[door] &&
                        context.Random.NextPercent(TraitEffects.DoorForceChancePercent(agent, context.Scenario)))
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
                            CausalEventType.AgentForcedDoor,
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
        /// <param name="writeItOff">
        /// True writes the door off as one they no longer count as a way out --
        /// what makes sense for a door that truly would not open. False keeps
        /// the memory of it as a way out intact, only avoided for a while: what
        /// makes sense for one merely wedged, which anybody can see is a thing
        /// to be shifted rather than a fact about the door.
        /// </param>
        private void GiveUp(Agent agent, bool writeItOff = true)
        {
            int tick = context.Tick;
            int door = agent.Doors.ExitDoorIndex;
            context.Events.Append(tick, agent.Id, CausalEventType.AgentGaveUpOnDoor, geometry.DoorCentre(door),
                0, 0, agent.Doors.AttemptEventId, doors.IdOf(door));
            agent.Doors.AvoidUntilTick[door] = checked(tick + context.Random.NextIntInclusive(
                settings.DoorAvoidMinimumTicks, settings.DoorAvoidMaximumTicks));
            agent.Doors.FoundShut[door] = writeItOff;
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
        /// <summary>
        /// Whether standing aside makes sense at this door. Normally only at an
        /// open one -- there is no point queueing politely at a door nobody is
        /// going through. But at the building's only way out, even a shut one,
        /// the press behind it is real and somebody wedged in the middle of it
        /// has to be able to ease out and come again, or the back of the queue
        /// sets solid and never moves at all.
        /// </summary>
        private bool GivesWayAt(Agent agent, int door)
        {
            return geometry.IsDoorOpen(door) || !HasAnotherWayOut(agent, door);
        }

        public bool TryGiveWay(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;

            // About a metre of the door: close enough that they are part of
            // the crush at it rather than still on their way. When this is the
            // only way out of the building the crush reaches further back, and
            // so does this: there is no other door to be sent off to, so
            // stepping aside and coming again is the only thing that keeps
            // somebody wedged in the middle of it moving at all.
            if (door < 0 || !GivesWayAt(agent, door))
            {
                return false;
            }

            int reach = HasAnotherWayOut(agent, door)
                ? settings.ApproachInsetMillimetres + context.Scenario.World.OccupancyRadiusMillimetres * 2
                : settings.CommitDistanceMillimetres;
            if (!IsNearExit(agent, reach) || !SomebodyElseLinedUpAt(agent, door))
            {
                return false;
            }

            agent.Body.BlockedTicks = 0;
            agent.Doors.GiveWayUntilTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.GiveWayMinimumTicks, settings.GiveWayMaximumTicks));
            return true;
        }

        /// <summary>
        /// Whether somebody else, on their feet, is lined up with this door's
        /// gap and close to it: somebody actually going through, and so worth
        /// standing aside for. With nobody there, standing aside helps no one.
        /// Everybody pressed round the gap would step aside at once, the gap
        /// would stand empty, and the whole knot would wait politely beside an
        /// open door for good.
        /// </summary>
        private bool SomebodyElseLinedUpAt(Agent agent, int door)
        {
            long reach = settings.ApproachInsetMillimetres + context.Scenario.World.OccupancyRadiusMillimetres * 2L;
            LogicalPosition centre = geometry.DoorCentre(door);
            using Crowd.Nearby people = crowd.Within(centre, reach);
            for (int c = 0; c < people.Count; c++)
            {
                Agent other = crowd.All[people[c]];
                if (other == agent || !other.IsParticipating || other.Body.State != AgentBodyState.Upright)
                {
                    continue;
                }

                if (LogicalPosition.DistanceSquared(other.Body.Position, centre) < reach * reach &&
                    geometry.IsLinedUpToPassThrough(door, other.Body.Position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stuck in a crush on the way out with a thing right in front of them,
        /// between them and where they are going: they grab it and throw it
        /// clear, if they can lift it. Self-preservation, so anybody does it,
        /// not only the strong and not only when told. True when they did.
        /// </summary>
        public bool TryClearTheWay(Agent agent)
        {
            LogicalPosition position = agent.Body.Position;
            LogicalPosition target = agent.Intent.Target;
            if (objects == null || agent.Body.State != AgentBodyState.Upright || agent.Carry.ItemIndex >= 0 ||
                position.Equals(target))
            {
                return false;
            }

            int radius = context.Scenario.World.OccupancyRadiusMillimetres;
            int heading = IntegerMath.HeadingBetween(position, target, agent.Body.Heading);
            LogicalPosition ahead = position + IntegerMath.Displacement(heading, radius + settings.ClearTheWayReachMillimetres);
            int thing = objects.FindBlocking(position, ahead, radius);
            if (thing < 0 || !CanReachToThrowClear(agent, thing))
            {
                return false;
            }

            objects.ThrowClear(agent, thing, ClearAwayHeading(agent, target), agent.Fear.ScaredEventId);
            agent.Body.BlockedTicks = 0;
            return true;
        }

        /// <summary>Whether this thing is within arm's reach and light enough for them to throw clear.</summary>
        private bool CanReachToThrowClear(Agent agent, int thing)
        {
            if (!objects.CanThrowClear(agent, thing))
            {
                return false;
            }

            long reach = (long)context.Scenario.World.OccupancyRadiusMillimetres + objects.RadiusOf(thing) +
                         settings.ClearTheWayReachMillimetres;
            return LogicalPosition.DistanceSquared(agent.Body.Position, objects.PositionOf(thing)) <= reach * reach;
        }

        /// <summary>
        /// Which way to throw a thing clear of the way out: back past themselves,
        /// away from <paramref name="wayOut"/>, and off to one side, the way
        /// somebody flings a chair over their shoulder. It may well land on the
        /// people behind them.
        /// </summary>
        private int ClearAwayHeading(Agent agent, LogicalPosition wayOut)
        {
            int back = IntegerMath.HeadingBetween(wayOut, agent.Body.Position, agent.Body.Heading + 180);
            int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
            return IntegerMath.NormalizeDegrees(back + side * context.Random.NextIntInclusive(
                settings.ClearTheWayMinimumAngleDegrees, settings.ClearTheWayMaximumAngleDegrees));
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
            int door = agent.Doors.ExitDoorIndex;
            if (door < 0)
            {
                return;
            }

            // Drawn either way, so this costs no change in the number of
            // random numbers a run uses.
            int until = checked(context.Tick + context.Random.NextIntInclusive(
                settings.DoorCrowdedAvoidMinimumTicks, settings.DoorCrowdedAvoidMaximumTicks));

            // Backing out of a queue means "try the other door", and marking
            // this one to avoid also hides it from the route search. When it
            // is the only way out of the building there is no other door, so
            // at full length that rule stops meaning "try elsewhere" and
            // starts meaning "give up and wander off". Only mark it when
            // another way out genuinely exists.
            if (HasAnotherWayOut(agent, door))
            {
                agent.Doors.AvoidUntilTick[door] = until;
            }
        }

        /// <summary>
        /// Whether this person could still reach a way out of the building
        /// without going through <paramref name="door"/>. Draws no random
        /// numbers: it only asks the route search, which draws none either.
        /// </summary>
        private bool HasAnotherWayOut(Agent agent, int door)
        {
            int room = geometry.RoomAt(agent.Body.Position);
            if (room < 0)
            {
                return false;
            }

            int was = agent.Doors.AvoidUntilTick[door];
            agent.Doors.AvoidUntilTick[door] = int.MaxValue;
            try
            {
                for (int d = 0; d < doors.Count; d++)
                {
                    if (d != door && geometry.DoorLeadsOutside(d) && !agent.Doors.FoundShut[d] &&
                        agent.Knowledge.Knows(d) &&
                        geometry.TryFindKnownRoute(room, agent.Body.Position, geometry.DoorRoom(d), agent,
                            out int first, out _, out _) &&
                        first != door)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                agent.Doors.AvoidUntilTick[door] = was;
            }
        }

        /// <summary>
        /// The door they have just come through: only the cruel shut it behind
        /// them, and they do it whoever is running up (only a body in the
        /// doorway stops them). The cruellest turn the key as well. Everyone
        /// else leaves it for the people behind them — unless the room they
        /// have just left is alight, which is
        /// <see cref="ConsiderShuttingAgainstFire"/>'s business, not spite.
        /// </summary>
        /// <summary>The objects are built after this behaviour, so they are handed over once everything exists.</summary>
        public void Bind(Systems systems)
        {
            objects = systems.Objects;
            people = systems.People;
        }

        /// <summary>Everybody's physical body, for hauling somebody down in a doorway through it. Bound after construction like the objects.</summary>
        private PeopleBodies people;

        /// <summary>
        /// Phase 6, before room changes: somebody down inside an open doorway
        /// with the crowd pressing on them from one side is carried on
        /// through it by the press, rather than lying in the gap as a plug
        /// that nobody can pass. Out through the way out, that is an escape
        /// on their back. Nobody decides this; it is what a crowd does to a
        /// body in its way.
        /// </summary>
        public void CarryTheFallenThroughDoorways()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                bool down = agent.Body.State == AgentBodyState.Fallen || agent.Body.State == AgentBodyState.Unconscious;
                if (!down)
                {
                    agent.Doors.CarriedThroughDoor = -1;
                    continue;
                }

                int door = OpenDoorwayLyingIn(agent.Body.Position);
                if (door < 0)
                {
                    continue;
                }

                int pressSide = SideOfThePress(agent, door);
                if (pressSide == 0)
                {
                    if (!geometry.DoorLeadsOutside(door) || geometry.SideOf(door, agent.Body.Position) <= 0)
                    {
                        continue;
                    }

                    // Already out through the wall line of a way out: the flow
                    // behind them keeps coming, and they slide on out.
                    pressSide = -1;
                }

                if (agent.Doors.CarriedThroughDoor != door)
                {
                    agent.Doors.CarriedThroughDoor = door;
                    context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentCarriedThroughDoorway, agent.Body.Position,
                        0, 0, agent.Body.EventId, doors.IdOf(door));
                }

                // Straight on through the gap, away from the press: hauled
                // toward a point beyond the wall on the far side, the way a
                // helper's drag hauls somebody, until they are out of the gap.
                LogicalPosition through = geometry.DoorPoint(door, 0, pressSide < 0 ? settings.CarryThroughRadiusMillimetres * 2
                    : -settings.CarryThroughRadiusMillimetres * 2);
                people.CarryToward(agent, through, settings.CarryThroughSpeedMillimetresPerTick);
            }
        }

        /// <summary>The open doorway this spot lies in, or -1.</summary>
        private int OpenDoorwayLyingIn(LogicalPosition position)
        {
            for (int door = 0; door < doors.Count; door++)
            {
                if (geometry.IsDoorOpen(door) && geometry.IsInDoorway(door, position))
                {
                    return door;
                }
            }

            return -1;
        }

        /// <summary>
        /// Which side of the door the crowd is pressing from: +1 for beyond
        /// the door's own room, -1 for inside it, 0 for nobody upright near
        /// enough on either side, or as many on each.
        /// </summary>
        private int SideOfThePress(Agent fallen, int door)
        {
            long radius = settings.CarryThroughRadiusMillimetres;
            int beyond = 0;
            int inside = 0;
            using Crowd.Nearby people = crowd.Within(fallen.Body.Position, radius);
            for (int c = 0; c < people.Count; c++)
            {
                Agent other = crowd.All[people[c]];
                if (other == fallen || !other.IsParticipating || other.Body.State != AgentBodyState.Upright ||
                    LogicalPosition.DistanceSquared(other.Body.Position, fallen.Body.Position) > radius * radius)
                {
                    continue;
                }

                if (geometry.SideOf(door, other.Body.Position) > 0)
                {
                    beyond++;
                }
                else
                {
                    inside++;
                }
            }

            return beyond == inside ? 0 : beyond > inside ? 1 : -1;
        }

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
                if (previousRoom >= 0 && threats.IsInRoom(previousRoom))
                {
                    ConsiderShuttingAgainstFire(agent, door, causeEventId);
                }

                return;
            }

            if (WouldCutOffTheirOwnWayOut(agent, geometry.RoomAt(agent.Body.Position), door))
            {
                // Spiteful, not stupid: they do not shut themselves in. Anybody
                // already out of the building has no room and no route left to
                // cut off, so the slam at the front door still happens.
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
            agent.Doors.ShutByThem[door] = true;
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
        /// they are shutting out and not the people. Nobody shuts it while
        /// somebody is still coming through -- except, once the flames are
        /// right at the door, somebody callous enough to weigh their own skin
        /// above the person behind them. Shutting a door on people is a
        /// selfish thing, so it takes a selfish person (the owner's rule,
        /// 2026-09-24); it used to take compassion 7 to hold a door at all.
        /// </summary>
        public void ConsiderShuttingAgainstFire(Agent agent, int door, ulong causeEventId)
        {
            if (doors.StateOf(door) != DoorState.Open)
            {
                return;
            }

            int room = geometry.RoomAt(agent.Body.Position);
            if (room < 0 || threats.IsInRoom(room))
            {
                // Their own room is alight: shutting this door saves nobody.
                return;
            }

            // Flames on either side of the door, not flames behind the wall
            // beside it.
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            int doorRoom = geometry.DoorRoom(door);
            bool flamesAtTheDoor = threats.AnyCloserThanInRooms(doorCentre, settings.FireAtDoorRadiusMillimetres,
                doorRoom, geometry.RoomBeyond(door, doorRoom));
            if (WouldCutOffTheirOwnWayOut(agent, room, door) &&
                (!flamesAtTheDoor || IsDashing(agent) || !agent.Doors.HasLookedForAWayOut))
            {
                // Getting out beats shutting the fire in. Nobody slams a door
                // they are about to run through: they used to stop on the way
                // to a door they could still reach, pull it shut, cross it off
                // as a way out, and then wander.
                //
                // Once the flames are actually at the door that route is gone
                // for anybody who is not running for it: shutting it costs them
                // nothing and may save them, and that is the case this lets
                // through. Somebody dashing for it through the heat, and
                // somebody who has not yet decided whether to, keep it.
                return;
            }

            if (SomeoneComing(agent, door, room) &&
                (!flamesAtTheDoor || agent.Traits.Compassion > settings.CallousCompassionMaximum))
            {
                // Holding it for whoever is still coming through. Only the
                // callous pull it shut on them once the flames are at it.
                return;
            }

            if (doors.TryClose(door, agent.Id, causeEventId, agent) != 0UL)
            {
                // Shut against the fire beyond it: not a way out to them now,
                // and never a door they would batter.
                RememberShutting(agent, door);
            }
        }

        /// <summary>
        /// Whether this door stands on the way to the way out this person has
        /// settled on, so shutting it would be shutting themselves in.
        /// <para>
        /// Somebody with no way out left (see <see cref="ChooseRefugeDoor"/>)
        /// has nothing to cut off, and shutting a door is the best thing left
        /// to them.
        /// </para>
        /// </summary>
        private bool WouldCutOffTheirOwnWayOut(Agent agent, int room, int door)
        {
            if (room >= 0 && agent.Fear.State != AgentFearState.Calm && !agent.Doors.HasLookedForAWayOut)
            {
                // Frightened and not yet thought about which way to run: every
                // door might be the one, so none is shut. Their first decision
                // is a few ticks away (nobody reacts on the tick), and this
                // used to be the gap in which somebody slammed the door they
                // were about to run through.
                return true;
            }

            int wayOut = agent.Doors.WayOutDoorIndex;
            if (wayOut < 0 || room < 0)
            {
                // No way out left to cut off, or they are already out of the
                // building (or standing in the doorway, where the door will not
                // shut on them anyway). Either way there is nothing to protect,
                // which is what keeps the slam at the front door working.
                return false;
            }

            // The door they are walking at right now always counts, even when
            // the route search disagrees with the choice they already made.
            return door == agent.Doors.ExitDoorIndex ||
                   door == wayOut ||
                   geometry.RouteUsesDoor(room, agent.Body.Position, geometry.DoorRoom(wayOut), agent, door,
                       knownOnly: true);
        }

        /// <summary>Anyone else still in the run near the door, on the side the closer is not.</summary>
        private bool SomeoneComing(Agent closer, int door, int closerRoom)
        {
            long radius = settings.CloseApproachRadiusMillimetres;
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            using Crowd.Nearby people = crowd.Within(doorCentre, radius);
            for (int c = 0; c < people.Count; c++)
            {
                Agent other = crowd.All[people[c]];
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
            if (threats.IsInRoom(room))
            {
                return;
            }

            long reach = settings.CloseReachMillimetres;
            int[] candidates = geometry.RoomDoors(room);
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                int beyond = geometry.RoomBeyond(door, room);

                // ConsiderShuttingAgainstFire refuses any door on their own way
                // out, this one included, so it is not checked twice here.
                if (!geometry.IsDoorOpen(door) || beyond < 0 ||
                    !threats.IsInRoom(beyond) ||
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
                ulong escaped = context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentEscaped,
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
