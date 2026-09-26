using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The Director: the background system that decides what the building's
    /// day holds. It keeps the level's <em>timetable</em>
    /// (<see cref="ScenarioData.Timetable"/>) -- the meeting ends at the
    /// minute mark, say -- and calls each cue on it once, on its tick. It
    /// reads simulation state and the seed only, and never anything the
    /// player's screen knows.
    /// <para>
    /// On a level that asks for it (<see cref="DirectorSettings.ClimbsTheLadder"/>,
    /// prototype 3, 2026-09-26) it also paces the round, as a ladder of small
    /// incidents rather than one big one:
    /// </para>
    /// <list type="number">
    /// <item>After half a minute to a minute and a half of ordinary day -- or
    /// at once, when the player presses the trigger -- a waste bin in the
    /// meeting room catches fire. It smoulders, then spreads slowly while it
    /// is young, so somebody brave nearby has a real chance to put it out.</item>
    /// <item>If it is put out -- nothing burning anywhere, and it never got out
    /// of the room it started in -- the bells fall silent a little later (the
    /// all-clear), and twenty to forty seconds on a wall socket in the busiest
    /// calm room crackles for a few seconds and pops: a bang and a small
    /// fire.</item>
    /// <item>If that is put out too, the fuse box crackles and goes, and the
    /// spark takes every socket in the building with it. That is the last
    /// rung.</item>
    /// </list>
    /// <para>
    /// A fire that gets out of the room it started in is the real fire: the
    /// Director adds nothing more, and the traps are armed by it. If everybody
    /// simply runs out, that is fine too. Without the ladder the traps are
    /// armed by the fire being lit, as they were in the first batch.
    /// </para>
    /// <para>
    /// Phase 1½ of the tick, after the player's commands and before the
    /// hazards advance: "the building's day". A cue called here reaches
    /// people at their own reaction tick inside phase 4, so nothing moves on
    /// the tick a cue is called.
    /// </para>
    /// <para>
    /// Random draws: with the ladder, two at start-up (which bin, if there is
    /// more than one, and when it catches), one per put-out (when the next
    /// rung comes, and one for the all-clear's own moment), and one per rung
    /// only when two sockets tie for the busiest room.
    /// </para>
    /// </summary>
    internal sealed class DirectorSystem : IBindable
    {
        private enum Rung
        {
            None,
            Bin,
            Socket,
            FuseBox
        }

        private enum LadderPhase
        {
            /// <summary>The ordinary day: the bin has not caught yet.</summary>
            Waiting,

            /// <summary>An incident is burning, and has not got out of its room.</summary>
            Burning,

            /// <summary>It was put out; the next rung, if any, is on its way.</summary>
            Out,

            /// <summary>A socket or the fuse box is crackling, about to go.</summary>
            Crackling,

            /// <summary>A fire got out of its room: the real fire. Nothing more is added.</summary>
            Over
        }

        private readonly SimulationContext context;
        private readonly CueSystem cues;
        private readonly WorldGeometry geometry;
        private readonly TrapSystem traps;
        private readonly FireSystem fire;
        private readonly FlammablesSystem flammables;
        private readonly PowerSystem power;
        private readonly PhysicsObjectSystem objects;
        private readonly Crowd crowd;
        private readonly SoundSystem sound;
        private readonly DirectorSettings settings;
        private readonly ScheduledCue[] timetable;
        private readonly bool[] called;

        private AlarmSystem alarms;
        private RoundSystem round;

        /// <summary>Whether this run's Director climbs the ladder: the level asks for it and one of its bins is in the building.</summary>
        private readonly bool climbs;

        /// <summary>The bin the first incident starts in, or -1.</summary>
        private readonly int binIndex = -1;

        private LadderPhase phase = LadderPhase.Waiting;
        private Rung rung = Rung.None;
        private Rung nextRung = Rung.None;
        private int dueTick;
        private int startRoom = -1;
        private int previousRoom = -1;

        /// <summary>
        /// The rooms the incident owns: the one it started in, and any its own
        /// bang or cascade set alight while it was settling. Fire anywhere else
        /// is fire that got loose.
        /// </summary>
        private bool[] incidentRooms;

        /// <summary>Until this tick, anything that catches belongs to the incident: the bang, the spark running down the cable.</summary>
        private int settlesAtTick;
        private LogicalPosition incidentPosition;
        private ulong incidentEventId;
        private ulong putOutEventId;
        private ulong crackleEventId;
        private int crackleNode = -1;
        private int allClearTick = -1;

        /// <summary>The event that said the fire got loose, or 0 while it has not.</summary>
        public ulong EscapedEventId { get; private set; }

        public DirectorSystem(SimulationContext context, CueSystem cues, WorldGeometry geometry, TrapSystem traps,
            FireSystem fire, FlammablesSystem flammables, PowerSystem power, PhysicsObjectSystem objects, Crowd crowd,
            SoundSystem sound)
        {
            this.context = context;
            this.cues = cues;
            this.geometry = geometry;
            this.traps = traps;
            this.fire = fire;
            this.flammables = flammables;
            this.power = power;
            this.objects = objects;
            this.crowd = crowd;
            this.sound = sound;
            settings = context.Scenario.Director;
            timetable = context.Scenario.Timetable ?? System.Array.Empty<ScheduledCue>();
            called = new bool[timetable.Length];

            if (!settings.ClimbsTheLadder)
            {
                return;
            }

            // Only the bins that are really in this building: a test floor
            // with the loose things cleared away has no ladder to climb.
            var present = new List<int>();
            for (int i = 0; i < settings.FirstIncidentThings.Length; i++)
            {
                int index = objects.IndexOf(settings.FirstIncidentThings[i]);
                if (index >= 0)
                {
                    present.Add(index);
                }
            }

            if (present.Count == 0)
            {
                return;
            }

            climbs = true;
            fire.LeaveTheStartToTheDirector();
            binIndex = present.Count == 1 ? present[0] : present[context.Random.NextIntInclusive(0, present.Count - 1)];
            dueTick = context.Random.NextIntInclusive(settings.FirstIncidentMinimumTicks, settings.FirstIncidentMaximumTicks);
        }

        /// <summary>Both are built after this system.</summary>
        public void Bind(Systems systems)
        {
            alarms = systems.Alarms;
            round = systems.Round;
        }

        /// <summary>
        /// Whether the Director has something still to come: a rung on its
        /// way, or a socket crackling. The round's stall clock must not end a
        /// round that has gone quiet only because the office settled back to
        /// work after a fire was put out.
        /// </summary>
        public bool HasSomethingComing =>
            climbs && (phase == LadderPhase.Crackling || (phase == LadderPhase.Out && nextRung != Rung.None));

        /// <summary>
        /// Every cue whose tick has come and that has not been called yet, in
        /// timetable order; then the ladder, if this level climbs one; then
        /// the traps.
        /// </summary>
        public void Advance()
        {
            CallTheTimetable();
            if (!climbs)
            {
                traps.Advance(fire.Active, fire.ActivationEventId);
                return;
            }

            Climb();
            traps.Advance(EscapedEventId != 0UL, EscapedEventId);
        }

        private void Climb()
        {
            int tick = context.Tick;
            switch (phase)
            {
                case LadderPhase.Waiting:
                    // The day runs its course, unless the player presses the
                    // button first.
                    if (tick >= dueTick || fire.StartRequested)
                    {
                        StartTheBin();
                    }

                    break;

                case LadderPhase.Burning:
                    Watch();
                    break;

                case LadderPhase.Out:
                    // The all-clear stands while nothing burns: a bell pulled
                    // again by somebody still frightened falls silent a little
                    // later, as the first did, however often it is pulled.
                    if (alarms != null && alarms.Ringing && allClearTick < 0)
                    {
                        allClearTick = checked(tick + context.Jittered(settings.AllClearAfterTicks));
                    }

                    if (allClearTick >= 0 && tick >= allClearTick)
                    {
                        alarms?.Silence(putOutEventId);
                        allClearTick = -1;
                    }

                    if (SomethingIsBurning())
                    {
                        // It caught again: in its own rooms it is the same
                        // incident back; anywhere else it has got loose.
                        allClearTick = -1;
                        startRoom = previousRoom;
                        phase = LadderPhase.Burning;
                        Watch();
                        break;
                    }

                    if (nextRung != Rung.None && tick >= dueTick)
                    {
                        StartCrackling();
                    }

                    break;

                case LadderPhase.Crackling:
                    if (tick >= dueTick)
                    {
                        Pop();
                    }

                    break;
            }
        }

        /// <summary>The first incident: the fire exists from now on, and the bin is what is burning.</summary>
        private void StartTheBin()
        {
            ulong cause = round != null && round.TriggerEventId != 0UL ? round.TriggerEventId : 0UL;
            incidentPosition = objects.PositionOf(binIndex);
            ulong activated = fire.StartWithoutFlames(incidentPosition, cause);
            incidentEventId = context.Events.Append(context.Tick, objects.IdOf(binIndex),
                CausalEventType.DirectorStartedIncident, incidentPosition, 0, 0, activated).EventId;
            flammables.IgniteObject(binIndex, incidentEventId);
            BeginIncident(geometry.RoomAtPoint(incidentPosition), 0);
            rung = Rung.Bin;
            phase = LadderPhase.Burning;
        }

        /// <summary>
        /// A new incident in this room. For <paramref name="settleTicks"/>
        /// more, any room set alight joins it: a bin has no bang, so none; a
        /// socket's bang and the fuse box's spark running down the cable light
        /// rooms of their own on purpose, and that is the incident, not the
        /// fire getting loose.
        /// </summary>
        private void BeginIncident(int room, int settleTicks)
        {
            incidentRooms ??= new bool[geometry.RoomCount];
            System.Array.Clear(incidentRooms, 0, incidentRooms.Length);
            startRoom = room;
            if (room >= 0)
            {
                incidentRooms[room] = true;
            }

            settlesAtTick = checked(context.Tick + settleTicks);
        }

        /// <summary>An incident burning: has it got loose, or gone out?</summary>
        private void Watch()
        {
            if (context.Tick <= settlesAtTick)
            {
                for (int r = 0; r < incidentRooms.Length; r++)
                {
                    incidentRooms[r] |= fire.IsBurningInRoom(r) || flammables.AnythingBurningInRoom(r);
                }
            }

            if (fire.BurningOutside(incidentRooms) || flammables.AnythingBurningOutside(incidentRooms))
            {
                EscapedEventId = context.Events.Append(context.Tick, default, CausalEventType.FireEscapedItsRoom,
                    incidentPosition, startRoom, 0, incidentEventId).EventId;
                phase = LadderPhase.Over;
                return;
            }

            if (SomethingIsBurning())
            {
                return;
            }

            int tick = context.Tick;
            putOutEventId = context.Events.Append(tick, default, CausalEventType.IncidentPutOut,
                incidentPosition, startRoom, 0, incidentEventId).EventId;
            previousRoom = startRoom;
            phase = LadderPhase.Out;
            allClearTick = checked(tick + context.Jittered(settings.AllClearAfterTicks));
            nextRung = rung == Rung.Bin ? Rung.Socket : rung == Rung.Socket ? Rung.FuseBox : Rung.None;
            dueTick = nextRung == Rung.None
                ? int.MaxValue
                : checked(tick + context.Random.NextIntInclusive(settings.NextRungMinimumTicks, settings.NextRungMaximumTicks));
        }

        /// <summary>Floor, things or people: anything at all alight.</summary>
        private bool SomethingIsBurning()
        {
            if (fire.BurningCount > 0 || flammables.BurningCount > 0)
            {
                return true;
            }

            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Burning.IsBurning)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The next rung begins: whatever is to go off crackles and smokes for
        /// a few seconds first -- the owner's "crackle first", so a player who
        /// is watching can pull people away -- and the curious go and look.
        /// </summary>
        private void StartCrackling()
        {
            int node = -1;
            if (nextRung == Rung.Socket)
            {
                node = BusiestCalmRoomsSocket();
                if (node < 0)
                {
                    // Every socket is gone, or the only ones left are where
                    // the last fire was: straight to the big one.
                    nextRung = Rung.FuseBox;
                }
            }

            if (nextRung == Rung.FuseBox)
            {
                node = power.FuseBoxNodeIndex;
                if (node < 0 || !power.IsFuseBoxStillWhole(node))
                {
                    nextRung = Rung.None;
                    dueTick = int.MaxValue;
                    return;
                }
            }

            int tick = context.Tick;
            crackleNode = node;
            LogicalPosition at = power.NodePosition(node);
            crackleEventId = context.Events.Append(tick, power.NodeId(node), CausalEventType.SocketCrackling,
                at, settings.CrackleTicks, 0, putOutEventId).EventId;
            sound.Crash(power.NodeId(node), at, settings.CrackleHearingMillimetres, crackleEventId);
            phase = LadderPhase.Crackling;
            dueTick = checked(tick + settings.CrackleTicks);
        }

        /// <summary>It goes: the next incident begins where it went off.</summary>
        private void Pop()
        {
            ulong bang = nextRung == Rung.FuseBox
                ? power.PopTheFuseBox(crackleEventId)
                : power.PopSocket(crackleNode, crackleEventId);
            rung = nextRung;
            nextRung = Rung.None;
            incidentPosition = power.NodePosition(crackleNode);
            BeginIncident(geometry.RoomAtPoint(incidentPosition), settings.BangSettlesTicks);
            incidentEventId = bang != 0UL ? bang : crackleEventId;
            phase = LadderPhase.Burning;
        }

        /// <summary>
        /// The socket for the second rung: in the room with the most calm
        /// people still in the run, never the room the last fire was in. The
        /// bang lands on an audience. Sockets that tie -- two rooms as busy,
        /// or two sockets in the busiest -- are drawn between; nothing is drawn
        /// when there is no tie. -1 when there is no socket to choose.
        /// </summary>
        private int BusiestCalmRoomsSocket()
        {
            var calmIn = new int[geometry.RoomCount];
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || agent.Fear.State != AgentFearState.Calm)
                {
                    continue;
                }

                int room = geometry.RoomAt(agent.Body.Position);
                if (room >= 0)
                {
                    calmIn[room]++;
                }
            }

            var best = new List<int>();
            int bestCount = -1;
            for (int node = 0; node < power.NodeCount; node++)
            {
                if (!power.IsSocketStillWhole(node))
                {
                    continue;
                }

                int room = geometry.RoomAtPoint(power.NodePosition(node));
                if (room < 0 || room == previousRoom)
                {
                    continue;
                }

                if (calmIn[room] > bestCount)
                {
                    bestCount = calmIn[room];
                    best.Clear();
                }

                if (calmIn[room] == bestCount)
                {
                    best.Add(node);
                }
            }

            if (best.Count == 0)
            {
                return -1;
            }

            return best.Count == 1 ? best[0] : best[context.Random.NextIntInclusive(0, best.Count - 1)];
        }

        private void CallTheTimetable()
        {
            for (int i = 0; i < timetable.Length; i++)
            {
                if (called[i] || context.Tick < timetable[i].AtTick)
                {
                    continue;
                }

                called[i] = true;
                Call(timetable[i]);
            }
        }

        /// <summary>
        /// A timetable entry is a cue that reaches a room or the whole
        /// building; the scenario refuses any other kind, so which it is
        /// comes from the cue's own definition rather than a list kept here.
        /// </summary>
        private void Call(ScheduledCue cue)
        {
            int room = cues.DefinitionOf(cue.Kind).Audience == CueAudience.Room ? RoomIndexOf(cue.RoomId) : -1;
            cues.Call(cue.Kind, null, room, null, cue.SpreadTicks, 0UL);
        }

        private int RoomIndexOf(SimulationId roomId)
        {
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (geometry.RoomId(r) == roomId)
                {
                    return r;
                }
            }

            throw new System.InvalidOperationException($"The timetable names room {roomId}, which the building does not have.");
        }
    }
}
