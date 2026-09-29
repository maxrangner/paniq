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
    /// is young, so somebody brave nearby has a real chance to put it out.
    /// Put out before the carpet under it ever caught, it was no fire at all:
    /// another of the room's bins catches a beat later, until the bins run
    /// out (the owner's rule, 2026-09-27).</item>
    /// <item>The tower of boxes is armed from the moment the bin is lit: the
    /// first frightened person to run along the corridor brings it down
    /// (<see cref="TrapSystem"/>), whatever the fire is doing.</item>
    /// <item>Five seconds after the boxes fall, whatever the bin fire is
    /// doing, a wall socket in the busiest room crackles for a few seconds
    /// and pops: a bang and a small fire (the owner's order, 2026-09-27:
    /// bin, boxes, outlet). If the fire is put out first -- nothing burning
    /// anywhere, and it never got out of the room it started in -- the bells
    /// fall silent a little later (the all-clear) and the socket comes five
    /// to ten seconds after the put-out instead.</item>
    /// <item>If the socket's fire is put out too, five to ten seconds later
    /// the fuse box crackles and goes, and the spark takes every socket in
    /// the building with it. That is the last rung.</item>
    /// </list>
    /// <para>
    /// A fire that gets out of the room it started in is the real fire: the
    /// Director adds nothing more. If everybody simply runs out, that is
    /// fine too. With or without the ladder the traps are armed by the fire
    /// being lit.
    /// </para>
    /// <para>
    /// Phase 1½ of the tick, after the player's commands and before the
    /// hazards advance: "the building's day". A cue called here reaches
    /// people at their own reaction tick inside phase 4, so nothing moves on
    /// the tick a cue is called.
    /// </para>
    /// <para>
    /// On a level that also asks for it (<see cref="DirectorSettings.CapsTheRound"/>,
    /// 2026-09-28) it <em>caps</em> the round, the owner's rule being that
    /// the office left alone should save about a quarter and never more
    /// than half. Like a stage manager in the wings who cannot see the
    /// audience: it watches the actors only. Before the curtain it draws an
    /// <em>allowance</em>, how many the building lets out today, from a
    /// stream of its own. Every half second, on its own beat, it reads the
    /// round -- how many are out, whether the way out is open or the card is
    /// in the pocket of somebody frightened who can reach it, and how many
    /// alive could walk there on the map -- and is in one of three states:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Ahead</b>: more are on course than the allowance. It pushes,
    /// a reaction lag after deciding to and then one trick at a time with a
    /// rest between: a trap still standing on the crowd's way is sprung
    /// without waiting for a runner; else the socket in the room with the
    /// most on-course people crackles and pops; else the fuse box; else,
    /// with nothing burning, another bin.</item>
    /// <item><b>A massacre</b>: only the allowance's worth, or fewer, are
    /// still alive or out. Nothing more is added: no socket after the fall,
    /// no fuse box after a put-out, no relit bin, and a standing trap stays
    /// unarmed. The rest may live without breaking the rule.</item>
    /// <item><b>Ordinary</b>: the ladder above, exactly as it is.</item>
    /// </list>
    /// <para>
    /// It reads the crowd and the card, never the player's clicks; the
    /// hands-off run behind the end card has the same Director reading its
    /// own round, so "left alone" stays an honest number.
    /// </para>
    /// <para>
    /// Random draws: with the ladder, two at start-up (which bin, if there is
    /// more than one, and when it catches), one per put-out (when the next
    /// rung comes, and one for the all-clear's own moment; a relit bin draws
    /// which bin, when more than one is left, and its beat), one when the
    /// tower falls (the socket's five seconds, jittered), and one per rung
    /// only when two sockets tie for the busiest room. The cap draws its
    /// allowance and its beat from its own stream, so a level without it
    /// replays as before; a push draws a reaction lag when decided, the
    /// rest before the next when it lands, and a tie-break as a rung does.
    /// </para>
    /// </summary>
    internal sealed class DirectorSystem : IBindable
    {
        /// <summary>The cap's own stream selector: 54 is the crowd, 55 the deck, 56 the keycard.</summary>
        private const ulong CapSequence = 57UL;

        private enum CapState
        {
            /// <summary>The ladder as it is.</summary>
            Ordinary,

            /// <summary>More people on course to get out than the allowance: pushing.</summary>
            Ahead,

            /// <summary>Only the allowance's worth or fewer left alive or out: nothing more is added.</summary>
            Massacre
        }
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

            /// <summary>A bin was put out before the carpet caught: another bin catches at <c>dueTick</c>.</summary>
            Relighting,

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
        private readonly DoorSystem doors;
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

        /// <summary>The bins the first incident may start in, that are in the building; which have been used.</summary>
        private readonly List<int> bins = new List<int>();
        private readonly bool[] binUsed;

        /// <summary>The bin the current (or next) bin incident starts in, or -1.</summary>
        private int binIndex = -1;

        /// <summary>How many bins have been lit so far, for the story.</summary>
        private int binsLit;

        /// <summary>How many floor squares had ever caught when the current bin was lit: none more, and the carpet never caught.</summary>
        private int cellsLitAtIncidentStart;

        /// <summary>When the socket crackles because the tower fell, or 0 while it has not fallen.</summary>
        private int socketFallDue;

        /// <summary>Whether the socket rung has begun, by either road.</summary>
        private bool socketCame;

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

        /// <summary>What the crackle names as its cause: the put-out for the ladder, the push for the cap.</summary>
        private ulong crackleCauseEventId;

        private int crackleNode = -1;
        private int allClearTick = -1;

        /// <summary>Where the ladder goes once what is crackling has popped: burning, or, for a push after the fire got loose, back to over.</summary>
        private LadderPhase phaseAfterPop = LadderPhase.Burning;

        /// <summary>The event that said the fire got loose, or 0 while it has not.</summary>
        public ulong EscapedEventId { get; private set; }

        // ---------------------------------------------------------------- the cap (2026-09-28)

        /// <summary>Whether this run's Director caps the round: the level asks for it, and there is a ladder to push with.</summary>
        private readonly bool caps;

        /// <summary>How many the building lets out this round, in people, drawn once from the cap's own stream.</summary>
        private readonly int allowance;

        /// <summary>Which tick of every <see cref="DirectorSettings.ReadEveryTicks"/> the reading is taken on: the Director's own beat.</summary>
        private readonly int readingBeat;

        /// <summary>The last reading: on-course people per room, and per trap those whose way out runs through its room.</summary>
        private readonly int[] onCourseInRoom;
        private readonly int[] onCourseBehindTrap;
        private readonly int[] peopleInRoom;

        private int onCourse;
        private int outOfTheBuilding;
        private int alive;
        private CapState capState = CapState.Ordinary;

        /// <summary>A push decided, landing at this tick (its reaction lag), or -1.</summary>
        private int pushAtTick = -1;

        /// <summary>No push before this tick: the rest after the last.</summary>
        private int nextPushAllowedTick;

        public DirectorSystem(SimulationContext context, CueSystem cues, WorldGeometry geometry, TrapSystem traps,
            DoorSystem doors, FireSystem fire, FlammablesSystem flammables, PowerSystem power, PhysicsObjectSystem objects,
            Crowd crowd, SoundSystem sound)
        {
            this.context = context;
            this.cues = cues;
            this.geometry = geometry;
            this.traps = traps;
            this.doors = doors;
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
            for (int i = 0; i < settings.FirstIncidentThings.Length; i++)
            {
                int index = objects.IndexOf(settings.FirstIncidentThings[i]);
                if (index >= 0)
                {
                    bins.Add(index);
                }
            }

            if (bins.Count == 0)
            {
                return;
            }

            climbs = true;
            binUsed = new bool[bins.Count];
            fire.LeaveTheStartToTheDirector();
            TryDrawAnotherBin();
            dueTick = context.Random.NextIntInclusive(settings.FirstIncidentMinimumTicks, settings.FirstIncidentMaximumTicks);
            peopleInRoom = new int[geometry.RoomCount];

            if (!settings.CapsTheRound)
            {
                return;
            }

            // The cap: its allowance and its beat from a stream of its own,
            // so the run's other draws are exactly what they would be without
            // it. Two to eight of twenty at the office's ten to forty percent.
            caps = true;
            var own = new Pcg32(context.Seed, CapSequence);
            int percent = own.NextIntInclusive(settings.AllowanceMinimumPercent, settings.AllowanceMaximumPercent);
            allowance = (crowd.All.Length * percent + 50) / 100;
            readingBeat = own.NextIntInclusive(0, settings.ReadEveryTicks - 1);
            onCourseInRoom = new int[geometry.RoomCount];
            onCourseBehindTrap = new int[traps.Count];
        }

        /// <summary>
        /// Picks the next bin from the ones not yet used: the only one left,
        /// or one drawn among them. False, and nothing drawn, when every bin
        /// has been lit.
        /// </summary>
        private bool TryDrawAnotherBin()
        {
            int unused = 0;
            for (int i = 0; i < bins.Count; i++)
            {
                if (!binUsed[i])
                {
                    unused++;
                }
            }

            if (unused == 0)
            {
                return false;
            }

            int pick = unused == 1 ? 0 : context.Random.NextIntInclusive(0, unused - 1);
            for (int i = 0; i < bins.Count; i++)
            {
                if (binUsed[i])
                {
                    continue;
                }

                if (pick == 0)
                {
                    binUsed[i] = true;
                    binIndex = bins[i];
                    return true;
                }

                pick--;
            }

            return false;
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
            climbs && (phase == LadderPhase.Crackling || pushAtTick >= 0 ||
                       (capState != CapState.Massacre &&
                        (phase == LadderPhase.Relighting ||
                         (socketFallDue > 0 && !socketCame) ||
                         (phase == LadderPhase.Out && nextRung != Rung.None))));

        /// <summary>
        /// Every cue whose tick has come and that has not been called yet, in
        /// timetable order; then the cap's reading and any push that lands;
        /// then the ladder, if this level climbs one; then the traps, armed
        /// by the fire being lit -- the bin, with the ladder -- and left
        /// unarmed while the round is a massacre.
        /// </summary>
        public void Advance()
        {
            CallTheTimetable();
            if (!climbs)
            {
                traps.Advance(fire.Active, fire.ActivationEventId);
                return;
            }

            Cap();
            Climb();
            traps.Advance(fire.Active && capState != CapState.Massacre,
                incidentEventId != 0UL ? incidentEventId : fire.ActivationEventId);
        }

        private void Climb()
        {
            int tick = context.Tick;
            bool holdingOff = capState == CapState.Massacre;

            // The socket after the boxes (the owner's order, 2026-09-27):
            // once the tower has come down, the socket crackles five seconds
            // later, whatever the bin fire is doing -- unless it has come
            // already because the fire was put out first.
            if (phase != LadderPhase.Waiting && !socketCame && socketFallDue == 0 && traps.LatestFallTick >= 0)
            {
                socketFallDue = checked(traps.LatestFallTick + context.Jittered(settings.SocketAfterFallTicks));
            }

            if (!holdingOff && !socketCame && socketFallDue > 0 && tick >= socketFallDue && phase != LadderPhase.Crackling)
            {
                nextRung = Rung.Socket;
                StartCrackling();
                return;
            }

            switch (phase)
            {
                case LadderPhase.Waiting:
                    // The day runs its course, unless the player presses the
                    // button first.
                    if (tick >= dueTick || fire.StartRequested)
                    {
                        StartTheBin(round != null && round.TriggerEventId != 0UL ? round.TriggerEventId : 0UL);
                    }

                    break;

                case LadderPhase.Burning:
                    Watch();
                    break;

                case LadderPhase.Relighting:
                    // The next bin, a beat after the last was doused -- unless
                    // the round is already a massacre.
                    if (!holdingOff && tick >= dueTick)
                    {
                        StartTheBin(putOutEventId);
                    }

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

                    if (!holdingOff && nextRung != Rung.None && tick >= dueTick)
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

        /// <summary>
        /// A bin incident: the fire exists from now on, and the bin is what is
        /// burning. The first time, <paramref name="cause"/> is the trigger
        /// (or nothing); a relit bin's cause is the put-out that was too
        /// quick. The event's strength counts the bins lit so far.
        /// </summary>
        private void StartTheBin(ulong cause)
        {
            incidentPosition = objects.PositionOf(binIndex);
            ulong activated = fire.StartWithoutFlames(incidentPosition, cause);
            binsLit++;
            cellsLitAtIncidentStart = fire.CellsEverLit;
            incidentEventId = context.Events.Append(context.Tick, objects.IdOf(binIndex),
                CausalEventType.DirectorStartedIncident, incidentPosition, binsLit, 0,
                binsLit > 1 ? cause : activated).EventId;
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
        private void BeginIncident(int room, int settleTicks, bool keepTheOldRooms = false)
        {
            incidentRooms ??= new bool[geometry.RoomCount];
            if (!keepTheOldRooms)
            {
                System.Array.Clear(incidentRooms, 0, incidentRooms.Length);
            }

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
            if (rung == Rung.Bin && fire.CellsEverLit == cellsLitAtIncidentStart && TryDrawAnotherBin())
            {
                // Doused before the carpet under it ever caught: that was no
                // fire, and another bin catches a beat later -- never on the
                // tick it was put out, so the story reads put out, then lit.
                phase = LadderPhase.Relighting;
                dueTick = context.ReactionTick();
                return;
            }

            previousRoom = startRoom;
            phase = LadderPhase.Out;
            allClearTick = checked(tick + context.Jittered(settings.AllClearAfterTicks));
            nextRung = rung == Rung.Bin && !socketCame ? Rung.Socket : rung == Rung.FuseBox ? Rung.None : Rung.FuseBox;
            dueTick = NextRungDue(tick);
        }

        /// <summary>
        /// When the next rung comes: five to ten seconds after the put-out
        /// (the owner, 2026-09-27). The socket may come sooner than that,
        /// five seconds after the tower falls, which <see cref="Climb"/>
        /// watches for on its own.
        /// </summary>
        private int NextRungDue(int tick)
        {
            if (nextRung == Rung.None)
            {
                return int.MaxValue;
            }

            int wait = context.Random.NextIntInclusive(settings.AfterPutOutMinimumTicks, settings.AfterPutOutMaximumTicks);
            return checked(tick + wait);
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
                socketCame = true;
                node = BusiestRoomsSocket(PeopleInRooms());
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

            crackleCauseEventId = putOutEventId;
            phaseAfterPop = LadderPhase.Burning;
            CrackleAt(node);
        }

        /// <summary>This socket, or the fuse box, crackles from now: it goes <see cref="DirectorSettings.CrackleTicks"/> later.</summary>
        private void CrackleAt(int node)
        {
            int tick = context.Tick;
            crackleNode = node;
            LogicalPosition at = power.NodePosition(node);
            crackleEventId = context.Events.Append(tick, power.NodeId(node), CausalEventType.SocketCrackling,
                at, settings.CrackleTicks, 0, crackleCauseEventId).EventId;
            sound.Crash(power.NodeId(node), at, settings.CrackleHearingMillimetres, crackleEventId);
            phase = LadderPhase.Crackling;
            dueTick = checked(tick + settings.CrackleTicks);
        }

        /// <summary>It goes: the next incident begins where it went off.</summary>
        private void Pop()
        {
            // A socket that pops while the bin still burns joins the bin's
            // incident rather than starting afresh: the bin's room is still
            // its own, not fire that got loose.
            bool stillBurning = SomethingIsBurning();
            ulong bang = nextRung == Rung.FuseBox
                ? power.PopTheFuseBox(crackleEventId)
                : power.PopSocket(crackleNode, crackleEventId);
            rung = nextRung;
            nextRung = Rung.None;
            incidentPosition = power.NodePosition(crackleNode);
            BeginIncident(geometry.RoomAtPoint(incidentPosition), settings.BangSettlesTicks, stillBurning);
            incidentEventId = bang != 0UL ? bang : crackleEventId;
            phase = phaseAfterPop;
            phaseAfterPop = LadderPhase.Burning;
        }

        /// <summary>Everybody still in the run, calm or frightened, counted by room.</summary>
        private int[] PeopleInRooms()
        {
            System.Array.Clear(peopleInRoom, 0, peopleInRoom.Length);
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                int room = geometry.RoomAt(agent.Body.Position);
                if (room >= 0)
                {
                    peopleInRoom[room]++;
                }
            }

            return peopleInRoom;
        }

        /// <summary>
        /// The socket for the second rung: in the room with the most people
        /// still in the run, calm or frightened (the owner's choice,
        /// 2026-09-27; it used to count the calm alone, and after the boxes
        /// fell the calm rooms were often empty), never a room the last fire
        /// was in. The bang lands on an audience. Sockets that tie -- two
        /// rooms as busy, or two sockets in the busiest -- are drawn between;
        /// nothing is drawn when there is no tie. -1 when there is no socket
        /// to choose. The cap asks with the on-course people counted instead
        /// (<paramref name="calmIn"/> is whichever count the caller means)
        /// and wants a room with somebody in it (<paramref name="atLeast"/>):
        /// a bang on an empty room cuts nobody off.
        /// </summary>
        private int BusiestRoomsSocket(int[] calmIn, int atLeast = 0)
        {
            var best = new List<int>();
            int bestCount = -1;
            for (int node = 0; node < power.NodeCount; node++)
            {
                if (!SocketQualifies(node, calmIn, atLeast, out int room))
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

        /// <summary>A whole socket, in a room, not one the incident has, with at least this many of the counted people in it.</summary>
        private bool SocketQualifies(int node, int[] countIn, int atLeast, out int room)
        {
            room = -1;
            if (!power.IsSocketStillWhole(node))
            {
                return false;
            }

            room = geometry.RoomAtPoint(power.NodePosition(node));
            return room >= 0 && room != previousRoom && (incidentRooms == null || !incidentRooms[room]) && countIn[room] >= atLeast;
        }

        // ---------------------------------------------------------------- the cap (2026-09-28)

        /// <summary>
        /// The cap's turn, once the round is under way: a push that has come
        /// due lands (the reading taken afresh first, and only if the round
        /// is still ahead), else on the Director's beat the reading is taken
        /// and, ahead and rested, a push is decided for a reaction lag later.
        /// </summary>
        private void Cap()
        {
            if (!caps || round == null || round.Phase != RoundPhase.Running)
            {
                return;
            }

            int tick = context.Tick;
            if (pushAtTick >= 0)
            {
                if (tick < pushAtTick)
                {
                    return;
                }

                pushAtTick = -1;
                TakeTheReading();
                if (capState == CapState.Ahead)
                {
                    Push();
                }

                nextPushAllowedTick = checked(tick + context.Random.NextIntInclusive(settings.PushMinimumTicks, settings.PushMaximumTicks));
                return;
            }

            if (tick % settings.ReadEveryTicks != readingBeat)
            {
                return;
            }

            TakeTheReading();
            if (capState == CapState.Ahead && tick >= nextPushAllowedTick && phase != LadderPhase.Waiting && HasSomethingToPush())
            {
                // Decided: it lands a reaction lag later. Only when there is
                // something to push with, so a Director with nothing to do
                // draws nothing and the run is exactly what it would be
                // without it (measured 2026-09-28: a lag drawn for a push
                // that then found nothing turned a round that saved six into
                // one that saved nobody).
                pushAtTick = context.ReactionTick();
            }
        }

        /// <summary>
        /// Whether a push would reach for anything right now, asked without
        /// drawing: a trap still standing with somebody on course in its
        /// room, a whole socket in a room with somebody on course, the fuse
        /// box once a socket has gone, or an unused bin with nothing burning.
        /// </summary>
        private bool HasSomethingToPush()
        {
            for (int t = 0; t < traps.Count; t++)
            {
                if (traps.IsStanding(t) && onCourseBehindTrap[t] > 0)
                {
                    return true;
                }
            }

            if (phase == LadderPhase.Crackling)
            {
                return false;
            }

            for (int node = 0; node < power.NodeCount; node++)
            {
                if (SocketQualifies(node, onCourseInRoom, 1, out _))
                {
                    return true;
                }
            }

            int fuseBox = power.FuseBoxNodeIndex;
            if (socketCame && fuseBox >= 0 && power.IsFuseBoxStillWhole(fuseBox))
            {
                return true;
            }

            if (SomethingIsBurning())
            {
                return false;
            }

            for (int i = 0; i < bins.Count; i++)
            {
                if (!binUsed[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How the round is going, from the crowd and the card alone. Out:
        /// escaped. Alive: still in the run. On course: out, plus everybody
        /// who has set out -- frightened, on their feet -- and could walk to
        /// a door to the street on the map people steer by; but only while a
        /// way out stands unlocked or the card is in the pocket of somebody
        /// frightened, on their feet, who can walk there too. A queue at a
        /// shut card door with the card on a desk two rooms away is nobody
        /// on course, and neither is somebody calm at their desk (measured
        /// 2026-09-28: counting them, the Director read twenty on course the
        /// moment the holder took fright and emptied its menu). Draws nothing.
        /// </summary>
        private void TakeTheReading()
        {
            System.Array.Clear(onCourseInRoom, 0, onCourseInRoom.Length);
            System.Array.Clear(onCourseBehindTrap, 0, onCourseBehindTrap.Length);
            outOfTheBuilding = 0;
            alive = 0;

            bool wayOutOpen = false;
            for (int d = 0; d < doors.Count; d++)
            {
                if (geometry.DoorLeadsOutside(d) && !doors.NeedsKeycard(d) && doors.StateOf(d) != DoorState.Locked)
                {
                    wayOutOpen = true;
                    break;
                }
            }

            Agent[] agents = crowd.All;
            bool holderOnTheirWay = false;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    outOfTheBuilding += agent.Outcome == AgentTerminalOutcome.Escaped ? 1 : 0;
                    continue;
                }

                alive++;
                if (!wayOutOpen && !holderOnTheirWay && agent.Keycard.Held >= 0 && agent.Fear.State == AgentFearState.Scared &&
                    agent.Body.IsOnTheirFeet && CanReachAWayOut(agent, out _))
                {
                    holderOnTheirWay = true;
                }
            }

            onCourse = outOfTheBuilding;
            if (wayOutOpen || holderOnTheirWay)
            {
                for (int i = 0; i < agents.Length; i++)
                {
                    Agent agent = agents[i];
                    if (!agent.IsParticipating || agent.Fear.State != AgentFearState.Scared || !agent.Body.IsOnTheirFeet ||
                        !CanReachAWayOut(agent, out _))
                    {
                        continue;
                    }

                    onCourse++;
                    int room = geometry.RoomOf(agent);
                    if (room < 0)
                    {
                        continue;
                    }

                    onCourseInRoom[room]++;
                    for (int t = 0; t < traps.Count; t++)
                    {
                        // In the trap's own room: the boxes come down on the
                        // crowd as it passes, cutting off whoever is behind,
                        // rather than walling a route off before anybody
                        // has reached it.
                        if (traps.TriggerRoom(t) == room)
                        {
                            onCourseBehindTrap[t]++;
                        }
                    }
                }
            }

            // Ahead only once a way out stands open. While the holder is
            // still walking the card to the door, the round's own suspense is
            // whether they get there; pushing then (measured 2026-09-28)
            // killed the holder and everybody behind them, and the office
            // saved 1.8 of 20. Once the door is open and more are streaming
            // out than the round allows, the building turns on the crowd.
            capState = alive + outOfTheBuilding <= allowance ? CapState.Massacre
                : wayOutOpen && onCourse > allowance ? CapState.Ahead
                : CapState.Ordinary;
        }

        /// <summary>
        /// Whether this person could walk to some door to the street on the
        /// map: their own room holds one, or a route of rooms and doors
        /// reaches one. <paramref name="firstDoor"/> is the first door of
        /// that route, or -1 from the door's own room.
        /// </summary>
        private bool CanReachAWayOut(Agent agent, out int firstDoor)
        {
            firstDoor = -1;
            int room = geometry.RoomOf(agent);
            if (room < 0)
            {
                return false;
            }

            for (int d = 0; d < doors.Count; d++)
            {
                if (!geometry.DoorLeadsOutside(d))
                {
                    continue;
                }

                int target = geometry.DoorRoom(d);
                if (target == room)
                {
                    firstDoor = -1;
                    return true;
                }

                if (geometry.TryFindRoute(room, agent.Body.Position, target, agent, out firstDoor, out _, out _))
                {
                    return true;
                }
            }

            firstDoor = -1;
            return false;
        }

        /// <summary>
        /// One push, the round being ahead: what cuts the most people who are
        /// on course, in a fixed order, skipping what is spent. A trap still
        /// standing with the most on-course people in its room is sprung;
        /// else the socket in the room with the most of them crackles; else,
        /// once a socket has already gone (the ladder's or a push's: the
        /// fuse box takes every socket left and is the last resort, not the
        /// first), the fuse box; else, with nothing at all burning and a bin
        /// unused, another bin. Nothing at all when none of these fits --
        /// the on-course crowd is in rooms with no socket -- and the
        /// Director rests and reads again. Each names the push as its cause,
        /// and the push names what set the round going.
        /// </summary>
        private void Push()
        {
            ulong cause = incidentEventId != 0UL ? incidentEventId : fire.ActivationEventId;

            int best = -1;
            for (int t = 0; t < traps.Count; t++)
            {
                if (traps.IsStanding(t) && onCourseBehindTrap[t] > 0 &&
                    (best < 0 || onCourseBehindTrap[t] > onCourseBehindTrap[best]))
                {
                    best = t;
                }
            }

            if (best >= 0)
            {
                traps.Spring(best, Pushed(traps.IdOf(best), traps.LandingOf(best), cause));
                return;
            }

            if (phase == LadderPhase.Crackling)
            {
                // Something is already about to go: let it.
                return;
            }

            int node = BusiestRoomsSocket(onCourseInRoom, 1);
            if (node >= 0)
            {
                nextRung = Rung.Socket;
                socketCame = true;
                crackleCauseEventId = Pushed(power.NodeId(node), power.NodePosition(node), cause);
                phaseAfterPop = phase == LadderPhase.Over ? LadderPhase.Over : LadderPhase.Burning;
                CrackleAt(node);
                return;
            }

            int fuseBox = power.FuseBoxNodeIndex;
            if (socketCame && fuseBox >= 0 && power.IsFuseBoxStillWhole(fuseBox))
            {
                nextRung = Rung.FuseBox;
                crackleCauseEventId = Pushed(power.NodeId(fuseBox), power.NodePosition(fuseBox), cause);
                phaseAfterPop = phase == LadderPhase.Over ? LadderPhase.Over : LadderPhase.Burning;
                CrackleAt(fuseBox);
                return;
            }

            if (!SomethingIsBurning() && TryDrawAnotherBin())
            {
                StartTheBin(Pushed(objects.IdOf(binIndex), objects.PositionOf(binIndex), cause));
            }
        }

        /// <summary>The push written down: how many were on course against the allowance, and what it reached for.</summary>
        private ulong Pushed(SimulationId target, LogicalPosition at, ulong cause)
        {
            LastPushTick = context.Tick;
            return context.Events.Append(context.Tick, default, CausalEventType.DirectorPushed, at, onCourse, allowance,
                cause, target).EventId;
        }

        /// <summary>The tick the building last turned on the crowd, or -1 while it never has: what the banner is timed from (2026-09-29).</summary>
        public int LastPushTick { get; private set; } = -1;

        /// <summary>Tests and measurements: whether this run's Director caps the round, and with what allowance.</summary>
        internal bool CapsForTests => caps;
        internal int AllowanceForTests => allowance;

        /// <summary>Tests and measurements: the last reading.</summary>
        internal int OnCourseForTests => onCourse;
        internal bool IsAheadForTests => capState == CapState.Ahead;
        internal bool IsMassacreForTests => capState == CapState.Massacre;

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
