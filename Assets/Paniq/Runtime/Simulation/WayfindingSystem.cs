using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// How somebody who does not know the building finds out about it.
    /// <para>
    /// Everybody used to be handed a perfect map: the moment they were
    /// frightened they knew every door on the floor and the shortest walk out.
    /// Somebody who works here still does. A visitor starts out knowing only
    /// the room they are in (<see cref="AgentKnowledge"/>) and learns a door in
    /// one of four ways, the same four a real person has:
    /// </para>
    /// <list type="bullet">
    /// <item>seeing it -- in the room they are in, or through an open doorway
    /// into the next one -- within <see cref="PerceptionSettings.DoorSightRangeMillimetres"/>;</item>
    /// <item>reading a green sign, which tells a frightened person every door
    /// on the way to the way out it points at;</item>
    /// <item>a door opening beside them, which nobody misses (see
    /// <see cref="DoorBehaviour.AnnounceWaysOut"/>);</item>
    /// <item>a leader they fall in behind telling them everything they know
    /// (see <see cref="LeaderBehaviour"/>).</item>
    /// </list>
    /// <para>
    /// This knows nothing about fire, or about any particular floor: it asks
    /// only about rooms, doors, signs and people, so whatever the danger is and
    /// whatever the level, the same rules hold. It draws no random numbers.
    /// </para>
    /// </summary>
    internal sealed class WayfindingSystem
    {
        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly ExitSignBehaviour exitSigns;
        private readonly long sightRangeSquared;

        /// <summary>
        /// Per sign: every door on the walk from it to the way out it points
        /// at, the way out last. Empty for a sign that points at no way out.
        /// Worked out once, from the building rather than from anybody's
        /// knowledge of it, because that is what a sign on a wall promises.
        /// </summary>
        private readonly int[][] signTeaches;

        public WayfindingSystem(SimulationContext context, WorldGeometry geometry, ExitSignBehaviour exitSigns)
        {
            this.context = context;
            this.geometry = geometry;
            this.exitSigns = exitSigns;
            long range = context.Scenario.Perception.DoorSightRangeMillimetres;
            sightRangeSquared = range * range;
            signTeaches = WorkOutWhatSignsTeach();
        }

        /// <summary>
        /// What a sign teaches, for the tests: the doors on its way out, the
        /// way out last.
        /// </summary>
        internal int[] WhatSignTeaches(int sign) => signTeaches[sign];

        /// <summary>
        /// Phase 4, at the start of each person's turn: take in what they can
        /// see. Costs nothing at all for somebody who knows the building.
        /// </summary>
        public void Look(Agent agent)
        {
            AgentKnowledge knowledge = agent.Knowledge;
            if (knowledge.KnowsEverything || !agent.IsParticipating ||
                agent.Body.State == AgentBodyState.Unconscious)
            {
                return;
            }

            int room = geometry.RoomOf(agent);
            if (room < 0)
            {
                return;
            }

            if (room != knowledge.LookRoom)
            {
                knowledge.CameFrom = knowledge.LookRoom;
                knowledge.LookRoom = room;
            }

            LogicalPosition eye = agent.Body.Position;
            int[] here = geometry.RoomDoors(room);
            for (int i = 0; i < here.Length; i++)
            {
                int door = here[i];
                if (!InSight(eye, geometry.DoorCentre(door)))
                {
                    continue;
                }

                Learn(agent, door, WayLearned.Saw, 0UL);

                // Through an open doorway, the next room's doors as well --
                // the same "same room, or a room joined to it by an open door"
                // rule that seeing the fire and reading a sign already use.
                int beyond = geometry.RoomBeyond(door, room);
                if (beyond >= 0 && geometry.IsDoorOpen(door))
                {
                    int[] there = geometry.RoomDoors(beyond);
                    for (int j = 0; j < there.Length; j++)
                    {
                        if (InSight(eye, geometry.DoorCentre(there[j])))
                        {
                            Learn(agent, there[j], WayLearned.Saw, 0UL);
                        }
                    }
                }
            }

            if (agent.Fear.State != AgentFearState.Calm)
            {
                // Nobody strolling to the coffee machine reads the exit signs;
                // somebody frightened reads every one they pass.
                int sign = exitSigns.NearestReadable(agent);
                if (sign >= 0)
                {
                    int[] teaches = signTeaches[sign];
                    for (int i = 0; i < teaches.Length; i++)
                    {
                        Learn(agent, teaches[i], WayLearned.Sign, 0UL);
                    }
                }
            }

            LookRound(agent, room, eye);
        }

        /// <summary>
        /// They know this door now. Learning of a way out while frightened is
        /// news: it is logged, and they think again straight away.
        /// </summary>
        public void Learn(Agent agent, int door, WayLearned how, ulong cause)
        {
            AgentKnowledge knowledge = agent.Knowledge;
            if (knowledge.Knows(door))
            {
                return;
            }

            knowledge.KnowsDoor[door] = true;
            if (!geometry.DoorLeadsOutside(door) || agent.Fear.State != AgentFearState.Scared)
            {
                return;
            }

            // Falling back to the fright that set them looking, so this never
            // goes into the log as a root event. Only a leader telling them
            // hands over a cause of its own; seeing a door or reading a sign
            // does not, and without this those two were the one thing in a
            // plain run that nothing could be traced back through. Its sibling
            // AgentLookedForAWayOut already names the same fright.
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentFoundTheWayOut,
                agent.Body.Position, (int)how, 0, cause != 0UL ? cause : agent.Fear.ScaredEventId);
            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// A leader tells somebody who has fallen in behind them everything
        /// they know: every door, and which rooms are not worth looking in.
        /// </summary>
        public void Share(Agent leader, Agent follower, ulong order)
        {
            AgentKnowledge told = follower.Knowledge;
            if (told.KnowsEverything)
            {
                return;
            }

            AgentKnowledge teller = leader.Knowledge;
            for (int door = 0; door < geometry.DoorCount; door++)
            {
                if (teller.Knows(door))
                {
                    Learn(follower, door, WayLearned.Told, order);
                }
            }

            for (int room = 0; room < geometry.RoomCount; room++)
            {
                if (teller.HasLookedOver(room))
                {
                    told.CornersSeen[room] = AgentKnowledge.AllCorners;
                }
            }
        }

        /// <summary>
        /// Every corner of the room they are standing wholly inside that is in
        /// sight. The moment the last one is, a searcher thinks again -- and
        /// if there turns out to be nothing onward, that was a dead end.
        /// </summary>
        private void LookRound(Agent agent, int room, LogicalPosition eye)
        {
            AgentKnowledge knowledge = agent.Knowledge;
            if (knowledge.CornersSeen[room] == AgentKnowledge.AllCorners || geometry.RoomAt(eye) != room)
            {
                return;
            }

            LogicalBounds b = geometry.RoomBounds(room);
            byte seen = knowledge.CornersSeen[room];
            seen |= InSight(eye, new LogicalPosition(b.MinX, b.MinZ)) ? (byte)1 : (byte)0;
            seen |= InSight(eye, new LogicalPosition(b.MaxX, b.MinZ)) ? (byte)2 : (byte)0;
            seen |= InSight(eye, new LogicalPosition(b.MinX, b.MaxZ)) ? (byte)4 : (byte)0;
            seen |= InSight(eye, new LogicalPosition(b.MaxX, b.MaxZ)) ? (byte)8 : (byte)0;
            knowledge.CornersSeen[room] = seen;
            if (seen != AgentKnowledge.AllCorners || !knowledge.Searching)
            {
                return;
            }

            context.ThinkAgainSoon(agent.Intent);
            if (IsDeadEnd(agent, room))
            {
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentFoundADeadEnd,
                    eye, 0, 0, knowledge.SearchEventId);
            }
        }

        /// <summary>
        /// Nothing onward from this room that they know of: no door out of the
        /// building, and no door to a room they have not already looked round,
        /// except the one they came in by.
        /// </summary>
        private bool IsDeadEnd(Agent agent, int room)
        {
            AgentKnowledge knowledge = agent.Knowledge;
            int[] here = geometry.RoomDoors(room);
            for (int i = 0; i < here.Length; i++)
            {
                int door = here[i];
                if (!knowledge.Knows(door))
                {
                    continue;
                }

                int beyond = geometry.RoomBeyond(door, room);
                if (beyond < 0 || (beyond != knowledge.CameFrom && !knowledge.HasLookedOver(beyond)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool InSight(LogicalPosition eye, LogicalPosition point)
        {
            return LogicalPosition.DistanceSquared(eye, point) <= sightRangeSquared;
        }

        /// <summary>
        /// For each sign, the way out it points at: of every way out the
        /// building has, the one whose first step from the sign agrees best
        /// with its arrow, ties to the shorter walk and then the lower index.
        /// A sign pointing at no way out at all teaches nothing.
        /// </summary>
        private int[][] WorkOutWhatSignsTeach()
        {
            var teaches = new int[exitSigns.Count][];
            var route = new List<int>();
            for (int sign = 0; sign < teaches.Length; sign++)
            {
                teaches[sign] = new int[0];
                LogicalPosition at = exitSigns.At(sign);
                int room = geometry.RoomAtPoint(at);
                if (room < 0)
                {
                    continue;
                }

                int best = -1;
                long bestAgreement = 0L;
                long bestCost = long.MaxValue;
                for (int door = 0; door < geometry.DoorCount; door++)
                {
                    if (!geometry.DoorLeadsOutside(door) ||
                        !geometry.TryFindRoute(room, at, geometry.DoorRoom(door), null, out int first, out _, out long cost))
                    {
                        continue;
                    }

                    LogicalPosition step = geometry.DoorCentre(first < 0 ? door : first);
                    long agreement = ExitSignBehaviour.Agreement(at, step, exitSigns.PointingOf(sign));
                    if (agreement > bestAgreement || (agreement == bestAgreement && best >= 0 && cost < bestCost))
                    {
                        best = door;
                        bestAgreement = agreement;
                        bestCost = cost;
                    }
                }

                if (best < 0)
                {
                    continue;
                }

                route.Clear();
                geometry.RouteDoors(room, at, geometry.DoorRoom(best), null, route);
                route.Add(best);
                teaches[sign] = route.ToArray();
            }

            return teaches;
        }
    }
}
