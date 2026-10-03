using System;
using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>What a machine player does with its one hand. Combined freely.</summary>
    [Flags]
    internal enum LoopSkill
    {
        None = 0,

        /// <summary>A hand on the pull station furthest from the flames, from a second after the fire until the bells ring.</summary>
        Alarm = 1,

        /// <summary>A click on whoever is winding up to freeze, dash or go back, half a second after it shows.</summary>
        Tells = 2,

        /// <summary>A click on somebody standing frozen, a second after they froze.</summary>
        WakeFrozen = 4,

        /// <summary>A hand on the door that leads the most threatened room away from the fire, the safe way round.</summary>
        Shepherd = 8,

        /// <summary>The right button on a crackling socket while it crackles.</summary>
        PushOffTheSocket = 16,

        /// <summary>The lazy player: the hand held on the way out from the first flame, and nothing else.</summary>
        HoldTheWayOut = 32,

        /// <summary>The control: one poke at one calm person two seconds after the fire, and nothing else.</summary>
        OnePoke = 64,

        /// <summary>A fixed plan of doors held one after another from the fire's start (see <see cref="LoopHand.Plan"/>).</summary>
        Scripted = 128,

        Careful = Alarm | Tells | WakeFrozen | Shepherd | PushOffTheSocket
    }

    /// <summary>
    /// A machine player for measuring a level (2026-10-02, reworked
    /// 2026-10-03): one hand, as the player has, looking five times a second
    /// and pressing the tick after, with a human's half second between seeing
    /// something and clicking it. A rough stand-in, not a person: what it
    /// shows is whether a hand used sensibly moves a round by degrees, on
    /// every seed, which is the thing a readable loop needs.
    /// </summary>
    internal sealed class LoopHand
    {
        private const int ReactionTicks = 25;
        private const int FireClearanceMillimetres = 1200;

        private readonly Run run;
        private readonly LoopSkill skills;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly int doorCount;
        private readonly LogicalPosition[] doorCentre;
        private readonly int[] doorRoomA;
        private readonly int[] doorRoomB;
        private readonly int wayOut = -1;

        private int cursor;
        private int fireTick = -1;
        private bool bellsRinging;
        private int crackleUntilTick = -1;
        private LogicalPosition crackleAt;
        private bool poked;
        private string holding = "";
        private int lastPressTick = -1000;
        private int heldSinceTick;
        private int noPressBeforeTick;
        private int kept = -1;
        private readonly Dictionary<ulong, int> tellSeen = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> frozenSeen = new Dictionary<ulong, int>();

        // Scratch, kept between decisions.
        private readonly long[] toExit;
        private readonly bool[] usable;
        private int[] people = Array.Empty<int>();
        private int[,] votes = new int[0, 0];
        private long[] nearestFlames = Array.Empty<long>();

        public int Clicks { get; private set; }
        public int Presses { get; private set; }

        /// <summary>For <see cref="LoopSkill.Scripted"/>: seconds after the fire, the door's id, and seconds held.</summary>
        public (int At, ulong Door, int For)[] Plan = Array.Empty<(int, ulong, int)>();

        public LoopHand(Run run, LoopSkill skills)
        {
            this.run = run;
            this.skills = skills;
            geometry = run.GeometryForTests;
            fire = run.FireForTests;
            doorCount = run.DoorCount;
            doorCentre = new LogicalPosition[doorCount];
            doorRoomA = new int[doorCount];
            doorRoomB = new int[doorCount];
            toExit = new long[doorCount];
            usable = new bool[doorCount];
            for (int d = 0; d < doorCount; d++)
            {
                doorCentre[d] = geometry.DoorCentre(d);
                doorRoomA[d] = geometry.DoorRoom(d);
                doorRoomB[d] = geometry.DoorLeadsOutside(d) ? -1 : geometry.RoomBeyond(d, doorRoomA[d]);
                if (doorRoomB[d] < 0)
                {
                    wayOut = d;
                }
            }
        }

        public void Think()
        {
            if (skills == LoopSkill.None)
            {
                return;
            }

            ReadTheNews();
            int tick = run.Tick;
            if (run.Phase != RoundPhase.Running)
            {
                return;
            }

            if ((skills & LoopSkill.OnePoke) != 0)
            {
                PokeOnce(tick);
                return;
            }

            if ((skills & (LoopSkill.Tells | LoopSkill.WakeFrozen)) != 0)
            {
                CatchTells(tick);
            }

            if (tick % 10 != 0 || tick < noPressBeforeTick)
            {
                return;
            }

            (string Key, PlayerCommandType Command, SimulationId Target, LogicalPosition Point) want = Decide(tick);
            bool handIsOff = run.InfluenceForTests.Count == 0;
            if (want.Key == holding && !(want.Key.Length > 0 && handIsOff && tick - lastPressTick >= 50))
            {
                return;
            }

            if (want.Key.Length == 0)
            {
                run.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), tick + 1);
            }
            else if (want.Target.Value != 0UL)
            {
                run.QueueCommand(want.Command, want.Target, tick + 1);
                Presses++;
            }
            else
            {
                run.QueueCommand(want.Command, want.Point, tick + 1);
                Presses++;
            }

            if (want.Key != holding)
            {
                heldSinceTick = tick;
            }

            holding = want.Key;
            lastPressTick = tick;
        }

        private void ReadTheNews()
        {
            IReadOnlyList<CausalEvent> events = run.EventLog.Events;
            for (; cursor < events.Count; cursor++)
            {
                CausalEvent record = events[cursor];
                switch (record.EventType)
                {
                    case CausalEventType.FireActivated:
                        fireTick = fireTick < 0 ? record.Tick : fireTick;
                        break;
                    case CausalEventType.AlarmPulled:
                    case CausalEventType.AlarmRang:
                        bellsRinging = true;
                        break;
                    case CausalEventType.SocketCrackling:
                        crackleUntilTick = record.Tick + record.Strength;
                        crackleAt = record.Position;
                        break;
                }
            }
        }

        /// <summary>The control: two seconds after the fire, one poke at the calm person with the lowest number, and nothing more.</summary>
        private void PokeOnce(int tick)
        {
            if (poked || fireTick < 0 || tick < fireTick + 100)
            {
                return;
            }

            poked = true;
            for (int i = 0; i < run.AgentCount; i++)
            {
                AgentSnapshot agent = run.GetAgent(i);
                if (agent.Participation == AgentParticipation.Participating && agent.FearState == AgentFearState.Calm)
                {
                    run.QueueCommand(PlayerCommandType.NudgePerson, agent.AgentId, tick + 1);
                    Clicks++;
                    return;
                }
            }
        }

        private void CatchTells(int tick)
        {
            for (int i = 0; i < run.AgentCount; i++)
            {
                AgentSnapshot agent = run.GetAgent(i);
                ulong id = agent.AgentId.Value;
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                if ((skills & LoopSkill.Tells) != 0)
                {
                    if (agent.Tell == AgentTell.None)
                    {
                        tellSeen.Remove(id);
                    }
                    else if (!tellSeen.TryGetValue(id, out int seen))
                    {
                        tellSeen[id] = tick;
                    }
                    else if (tick - seen == ReactionTicks)
                    {
                        Click(agent.AgentId, tick);
                    }
                }

                if ((skills & LoopSkill.WakeFrozen) != 0)
                {
                    if (agent.ActivityState != AgentActivityState.Frozen || agent.Tell != AgentTell.None)
                    {
                        frozenSeen.Remove(id);
                    }
                    else if (!frozenSeen.TryGetValue(id, out int since))
                    {
                        frozenSeen[id] = tick;
                    }
                    else
                    {
                        int waited = tick - since;
                        if (waited == 50 || waited == 65 || waited == 80)
                        {
                            Click(agent.AgentId, tick);
                        }
                    }
                }
            }
        }

        /// <summary>A click needs the hand: whatever it was holding is let go, and pressed again a moment later.</summary>
        private void Click(SimulationId person, int tick)
        {
            if (holding.Length > 0)
            {
                run.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), tick + 1);
                holding = "";
            }

            run.QueueCommand(PlayerCommandType.NudgePerson, person, tick + 1);
            noPressBeforeTick = tick + 15;
            Clicks++;
        }

        private (string, PlayerCommandType, SimulationId, LogicalPosition) Decide(int tick)
        {
            var nothing = ("", PlayerCommandType.ReleaseInfluence, default(SimulationId), default(LogicalPosition));
            if (fireTick < 0 || tick - fireTick < 50)
            {
                return nothing;
            }

            if ((skills & LoopSkill.Scripted) != 0)
            {
                int second = (tick - fireTick) / Run.TicksPerSecond;
                foreach ((int At, ulong Door, int For) step in Plan)
                {
                    if (second >= step.At && second < step.At + step.For)
                    {
                        return ("door " + step.Door, PlayerCommandType.InfluenceDoor, new SimulationId(step.Door), default(LogicalPosition));
                    }
                }

                return nothing;
            }

            if ((skills & LoopSkill.HoldTheWayOut) != 0)
            {
                return wayOut >= 0
                    ? ("door " + wayOut, PlayerCommandType.InfluenceDoor, run.GetDoor(wayOut).DoorId, default(LogicalPosition))
                    : nothing;
            }

            if ((skills & LoopSkill.PushOffTheSocket) != 0 && tick < crackleUntilTick)
            {
                return ("push " + crackleAt, PlayerCommandType.RepelSpot, default(SimulationId), crackleAt);
            }

            if ((skills & LoopSkill.Alarm) != 0 && !bellsRinging && tick - fireTick < 1000)
            {
                int station = TheCoolestStation();
                if (station >= 0)
                {
                    return ("alarm " + station, PlayerCommandType.InfluenceSpot, default(SimulationId), run.AlarmPosition(station));
                }
            }

            if ((skills & LoopSkill.Shepherd) == 0)
            {
                return nothing;
            }

            int door = tick % 25 == 0 || kept < 0 ? TheDoorToHold(tick) : kept;
            kept = door;
            return door < 0
                ? nothing
                : ("door " + door, PlayerCommandType.InfluenceDoor, run.GetDoor(door).DoorId, default(LogicalPosition));
        }

        /// <summary>The pull station furthest from the flames, if it is further than six metres from them; -1 otherwise.</summary>
        private int TheCoolestStation()
        {
            int best = -1;
            long bestDistance = 6000L * 6000L;
            for (int i = 0; i < run.AlarmCount; i++)
            {
                long distance = fire.NearestDistanceSquared(run.AlarmPosition(i));
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// The door a careful player would put their hand on now: of the
        /// rooms with people in them who are not already making for the safe
        /// door, the one with the most of them for how near the flames are;
        /// and of that room's doors, the one most of them would best leave by,
        /// walking to the way out without passing the fire. A choice is kept
        /// for three seconds so the hand does not flit. -1 when nobody needs
        /// leading.
        /// </summary>
        private int TheDoorToHold(int tick)
        {
            for (int d = 0; d < doorCount; d++)
            {
                DoorSnapshot door = run.GetDoor(d);
                usable[d] = door.State != DoorState.Locked && !fire.AnyCloserThan(doorCentre[d], FireClearanceMillimetres);
                toExit[d] = usable[d] && doorRoomB[d] < 0 ? 0L : long.MaxValue;
            }

            for (bool changed = true; changed;)
            {
                changed = false;
                for (int a = 0; a < doorCount; a++)
                {
                    if (!usable[a])
                    {
                        continue;
                    }

                    for (int b = 0; b < doorCount; b++)
                    {
                        if (a == b || !usable[b] || toExit[b] == long.MaxValue || !ShareARoom(a, b))
                        {
                            continue;
                        }

                        long through = toExit[b] + IntegerMath.Distance(doorCentre[a], doorCentre[b]);
                        if (through < toExit[a] && !fire.RoutePassesNear(doorCentre[a], doorCentre[b], FireClearanceMillimetres))
                        {
                            toExit[a] = through;
                            changed = true;
                        }
                    }
                }
            }

            int rooms = geometry.RoomCount;
            if (people.Length != rooms)
            {
                people = new int[rooms];
                votes = new int[rooms, doorCount];
                nearestFlames = new long[rooms];
            }

            Array.Clear(people, 0, rooms);
            Array.Clear(votes, 0, votes.Length);
            for (int r = 0; r < rooms; r++)
            {
                nearestFlames[r] = long.MaxValue;
            }

            for (int i = 0; i < run.AgentCount; i++)
            {
                AgentSnapshot agent = run.GetAgent(i);
                if (agent.Participation != AgentParticipation.Participating || agent.IsBurning)
                {
                    continue;
                }

                int room = geometry.RoomAtPoint(agent.Position);
                if (room < 0)
                {
                    continue;
                }

                int best = -1;
                long bestWalk = long.MaxValue;
                for (int d = 0; d < doorCount; d++)
                {
                    if (!usable[d] || toExit[d] == long.MaxValue || (doorRoomA[d] != room && doorRoomB[d] != room) ||
                        fire.RoutePassesNear(agent.Position, doorCentre[d], FireClearanceMillimetres))
                    {
                        continue;
                    }

                    long walk = toExit[d] + IntegerMath.Distance(agent.Position, doorCentre[d]);
                    if (walk < bestWalk)
                    {
                        bestWalk = walk;
                        best = d;
                    }
                }

                // Nobody to lead: no safe door, a few steps from an open way
                // out, already making for the safe door, or whose own way out
                // -- the shortest walk they know -- is the safe way anyway.
                if (best < 0 || (doorRoomB[best] < 0 && IntegerMath.Distance(agent.Position, doorCentre[best]) < 6000) ||
                    (run.ExitDoorIndexForTests(i) == best && agent.FearState == AgentFearState.Scared) ||
                    TheirOwnWayIs(i, room, agent.Position, best))
                {
                    continue;
                }

                people[room]++;
                votes[room, best]++;
                long flames = fire.NearestDistanceSquared(agent.Position);
                long distance = flames == long.MaxValue ? 60000L : IntegerMath.Sqrt(flames);
                nearestFlames[room] = Math.Min(nearestFlames[room], distance);
            }

            if (holding.StartsWith("door ") && tick - heldSinceTick < 150)
            {
                int held = int.Parse(holding.Substring(5));
                if (usable[held] && (Voted(doorRoomA[held], held) > 0 || Voted(doorRoomB[held], held) > 0))
                {
                    return held;
                }
            }

            int bestRoom = -1;
            long bestScore = 0L;
            for (int r = 0; r < rooms; r++)
            {
                if (people[r] == 0)
                {
                    continue;
                }

                long score = people[r] * 100000L / (5000L + nearestFlames[r]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = r;
                }
            }

            if (bestRoom < 0)
            {
                return -1;
            }

            int chosen = -1;
            int most = 0;
            for (int d = 0; d < doorCount; d++)
            {
                if (votes[bestRoom, d] > most)
                {
                    most = votes[bestRoom, d];
                    chosen = d;
                }
            }

            return chosen;
        }

        private int Voted(int room, int door) => room < 0 ? 0 : votes[room, door];

        /// <summary>Whether the first door of the shortest walk this person knows to the way out is this one.</summary>
        private bool TheirOwnWayIs(int index, int room, LogicalPosition position, int door)
        {
            if (wayOut < 0)
            {
                return false;
            }

            Agent person = run.AgentForTests(index);
            if (!geometry.TryFindKnownRoute(room, position, geometry.DoorRoom(wayOut), person, out int first, out _, out _))
            {
                return false;
            }

            return (first < 0 ? wayOut : first) == door;
        }

        private bool ShareARoom(int a, int b)
        {
            return doorRoomA[a] == doorRoomA[b] || (doorRoomB[b] >= 0 && doorRoomA[a] == doorRoomB[b]) ||
                   (doorRoomB[a] >= 0 && (doorRoomB[a] == doorRoomA[b] || doorRoomB[a] == doorRoomB[b]));
        }
    }
}
