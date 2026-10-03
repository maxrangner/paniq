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
    /// <item>If the fire is put out -- nothing burning anywhere, and it
    /// never got out of the room it started in -- the bells fall silent a
    /// little later (the all-clear), and that is the end of the ladder. Since
    /// 2026-10-02 the ladder adds no socket and no fuse box of its own (the
    /// owner: popping the socket by the fallen boxes was "an instant game
    /// over. That should only pop late in a run"; asked what late means, "only
    /// as the building's counter-move"). It used to pop a socket five seconds
    /// after the tower fell, or after a put-out, and the fuse box after
    /// that.</item>
    /// </list>
    /// <para>
    /// A fire that gets out of the room it started in is the real fire: the
    /// ladder adds nothing more. If everybody simply runs out, that is
    /// fine too. The stacks of boxes are no longer the Director's at all
    /// (2026-10-02): only somebody running into one brings it down
    /// (<see cref="TrapSystem"/>).
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
    /// rest between: the socket in the room with the most on-course people
    /// crackles and pops; else, once a socket has gone, the fuse box; else,
    /// with nothing burning, another bin. This is the only way a socket or
    /// the fuse box ever goes by the Director's doing (2026-10-02).</item>
    /// <item><b>A massacre</b>: only the allowance's worth, or fewer, are
    /// still alive or out. Nothing more is added: no push and no relit
    /// bin. The rest may live without breaking the rule.</item>
    /// <item><b>Ordinary</b>: the ladder above, exactly as it is.</item>
    /// </list>
    /// <para>
    /// It reads the crowd and the card, never the player's clicks; the
    /// hands-off run behind the end card has the same Director reading its
    /// own round, so "left alone" stays an honest number.
    /// </para>
    /// <para>
    /// Random draws: with the ladder, two at start-up (which bin, if there is
    /// more than one, and when it catches) and one per put-out (the
    /// all-clear's own moment; a relit bin draws which bin, when more than
    /// one is left, and its beat). The cap draws its
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

        /// <summary>
        /// Whether this run's Director opens with a real fire (2026-10-02,
        /// <see cref="DirectorSettings.StartsARealFire"/>): no ladder and no
        /// cap. The round's whole script -- when the first fire lights, where
        /// every fire lights, how long after a put-out the next one comes,
        /// when the bells ring, and the building's one move -- is drawn
        /// before the first tick from a stream of its own
        /// (<see cref="FireSequence"/>), so the player's clicks, which draw
        /// from the run's shared stream, can never change it: the copy of the
        /// round played with nobody at the controls faces the same building
        /// (2026-10-03; the first cut drew the spot when the fire lit, and a
        /// click in the calm could move it).
        /// </summary>
        private readonly bool burns;

        /// <summary>The Director's own stream for that script: 54 is the crowd, 55 the deck, 56 the keycard, 57 the cap.</summary>
        private const ulong FireSequence = 58UL;

        /// <summary>Where each fire of the round lights, in order: the first, then each relight.</summary>
        private readonly LogicalPosition[] firePoints;

        /// <summary>How long after a fire is put out for good the next one lights, one per relight.</summary>
        private readonly int[] relightAfter;

        /// <summary>How many fires the building has lit, for the story.</summary>
        private int firesLit;

        /// <summary>How long after the smoke reaches a detector the bells start.</summary>
        private readonly int bellsLag;

        /// <summary>The tick the smoke detectors ring the bells, once the fire is big enough, or -1; and whether this fire has rung them.</summary>
        private int bellsAtTick = -1;
        private bool bellsRung;

        /// <summary>Whether the fire burning now has got out of the room it started in.</summary>
        private bool escapedThisFire;

        /// <summary>The tick the first fire lit, or -1.</summary>
        private int firstFireTick = -1;

        /// <summary>The building's move (2026-10-03): the socket it sets crackling, as a thing's index, or -1 for none.</summary>
        private readonly int moveSocket = -1;

        /// <summary>How long after the first fire the move begins, and how long the socket crackles.</summary>
        private readonly int moveAfterTicks;
        private readonly int moveCrackleTicks;

        private int movePopsAtTick = -1;
        private bool moveBegun;
        private ulong moveEventId;

        /// <summary>
        /// The tick the round ends because the building is at peace: the last
        /// fire it will light is out for good. -1 while that is not so. The
        /// round's own stall clock never ended such a round, because a calm
        /// office goes back to work and is never still.
        /// </summary>
        public int PeaceEndsTheRoundAtTick { get; private set; } = -1;

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

        /// <summary>Whether a socket has gone by the Director's push: the fuse box may be the next push.</summary>
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

        /// <summary>The last reading: on-course people per room.</summary>
        private readonly int[] onCourseInRoom;

        private int onCourse;
        private int outOfTheBuilding;
        private int alive;
        private CapState capState = CapState.Ordinary;

        /// <summary>A push decided, landing at this tick (its reaction lag), or -1.</summary>
        private int pushAtTick = -1;

        /// <summary>No push before this tick: the rest after the last.</summary>
        private int nextPushAllowedTick;

        public DirectorSystem(SimulationContext context, CueSystem cues, WorldGeometry geometry,
            DoorSystem doors, FireSystem fire, FlammablesSystem flammables, PowerSystem power, PhysicsObjectSystem objects,
            Crowd crowd, SoundSystem sound)
        {
            this.context = context;
            this.cues = cues;
            this.geometry = geometry;
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

            if (settings.StartsARealFire && settings.FireSpots.Length > 0)
            {
                burns = true;
                fire.LeaveTheStartToTheDirector();
                var script = new Pcg32(context.Seed, FireSequence);
                dueTick = script.NextIntInclusive(settings.FirstIncidentMinimumTicks, settings.FirstIncidentMaximumTicks);

                // The spots in a drawn order, a point inside each, and the
                // beats between: as many fires as there are relights and
                // spots to spend them on.
                int spots = settings.FireSpots.Length;
                var order = new int[spots];
                for (int i = 0; i < spots; i++)
                {
                    order[i] = i;
                }

                for (int i = spots - 1; i > 0; i--)
                {
                    int j = script.NextIntInclusive(0, i);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                int fires = System.Math.Min(spots, 1 + settings.Relights);
                firePoints = new LogicalPosition[fires];
                relightAfter = new int[fires];
                for (int i = 0; i < fires; i++)
                {
                    LogicalBounds area = settings.FireSpots[order[i]];
                    firePoints[i] = new LogicalPosition(script.NextIntInclusive(area.MinX, area.MaxX),
                        script.NextIntInclusive(area.MinZ, area.MaxZ));
                    relightAfter[i] = Spread(ref script, settings.RelightAfterTicks);
                }

                PerceptionSettings perception = context.Scenario.Perception;
                bellsLag = System.Math.Max(1, script.NextIntInclusive(perception.ReactionLagMinimumTicks, perception.ReactionLagMaximumTicks));

                // The building's move: one of the level's sockets, if any is
                // in the building, a while after the first fire.
                var sockets = new List<int>();
                for (int i = 0; i < settings.MoveSockets.Length; i++)
                {
                    int index = objects.IndexOf(settings.MoveSockets[i]);
                    if (index >= 0)
                    {
                        sockets.Add(index);
                    }
                }

                if (sockets.Count > 0)
                {
                    moveSocket = sockets[sockets.Count == 1 ? 0 : script.NextIntInclusive(0, sockets.Count - 1)];
                    moveAfterTicks = script.NextIntInclusive(settings.MoveMinimumTicksAfterFire, settings.MoveMaximumTicksAfterFire);
                    moveCrackleTicks = Spread(ref script, settings.CrackleTicks);
                }

                return;
            }

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
            (burns && (phase == LadderPhase.Relighting || (moveBegun && movePopsAtTick >= 0))) ||
            (climbs && (phase == LadderPhase.Crackling || pushAtTick >= 0 ||
                        (capState != CapState.Massacre && phase == LadderPhase.Relighting)));

        /// <summary>
        /// Every cue whose tick has come and that has not been called yet, in
        /// timetable order; then the cap's reading and any push that lands;
        /// then the ladder, if this level climbs one. The stacks of boxes
        /// are not the Director's (2026-10-02): the run advances them.
        /// </summary>
        public void Advance()
        {
            CallTheTimetable();
            if (burns)
            {
                Burn();
                return;
            }

            if (!climbs)
            {
                return;
            }

            Cap();
            Climb();
        }

        // ---------------------------------------------------------------- the fire that splits the floor (2026-10-02)

        /// <summary>A length of time spread by the world's jitter, drawn from the Director's own stream.</summary>
        private int Spread(ref Pcg32 own, int ticks)
        {
            int spread = System.Math.Max(1, ticks * context.Scenario.World.TimingJitterPercent / 100);
            return System.Math.Max(1, ticks + own.NextIntInclusive(-spread, spread));
        }

        /// <summary>
        /// The round of a level that opens with a real fire: wait out the
        /// calm (or the trigger), light it, ring the bells when the smoke is
        /// thick enough, play the building's move, and watch. If every flame
        /// is put out, the next fire of the script lights a beat later while
        /// there is one; after the last, the building is at peace and the
        /// round ends a few seconds on.
        /// </summary>
        private void Burn()
        {
            int tick = context.Tick;
            TheBuildingsMove(tick);
            switch (phase)
            {
                case LadderPhase.Waiting:
                    if (tick >= dueTick || fire.StartRequested)
                    {
                        StartTheFire(round != null && round.TriggerEventId != 0UL ? round.TriggerEventId : 0UL);
                    }

                    break;

                case LadderPhase.Burning:
                    if (SomethingIsBurning())
                    {
                        WatchTheFire(tick);
                        break;
                    }

                    putOutEventId = context.Events.Append(tick, default, CausalEventType.IncidentPutOut,
                        incidentPosition, startRoom, 0, incidentEventId).EventId;
                    bellsAtTick = -1;
                    bellsRung = false;
                    if (firesLit < firePoints.Length)
                    {
                        phase = LadderPhase.Relighting;
                        dueTick = checked(tick + relightAfter[firesLit - 1]);
                    }
                    else
                    {
                        phase = LadderPhase.Out;
                        allClearTick = checked(tick + context.Jittered(settings.AllClearAfterTicks));
                        PeaceEndsTheRoundAtTick = checked(tick + context.Jittered(settings.PeaceEndsTheRoundTicks));
                    }

                    break;

                case LadderPhase.Relighting:
                    if (tick >= dueTick)
                    {
                        StartTheFire(putOutEventId);
                    }

                    break;

                case LadderPhase.Out:
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
                        // An ember somebody carried, or the move's bang: the
                        // building is not at peace after all.
                        allClearTick = -1;
                        PeaceEndsTheRoundAtTick = -1;
                        phase = LadderPhase.Burning;
                    }

                    break;
            }
        }

        /// <summary>
        /// A fire burning: the smoke reaches a detector once it is big
        /// enough, and the bells ring a beat later; and the first flame or
        /// burning thing outside the room this fire started in is written
        /// down once a fire, as the ladder writes it, for the sign and the
        /// end card.
        /// </summary>
        private void WatchTheFire(int tick)
        {
            if (settings.BellsRingAtSquares > 0 && !bellsRung && alarms != null)
            {
                if (bellsAtTick < 0 && fire.BurningCount >= settings.BellsRingAtSquares)
                {
                    bellsAtTick = checked(tick + bellsLag);
                }

                if (bellsAtTick >= 0 && tick >= bellsAtTick)
                {
                    bellsRung = true;
                    alarms.TripByTheSmoke(incidentEventId);
                }
            }

            if (!escapedThisFire && startRoom >= 0 &&
                (fire.BurningOutside(incidentRooms) || flammables.AnythingBurningOutside(incidentRooms)))
            {
                escapedThisFire = true;
                EscapedEventId = context.Events.Append(tick, default, CausalEventType.FireEscapedItsRoom,
                    incidentPosition, startRoom, 0, incidentEventId).EventId;
            }
        }

        /// <summary>
        /// The next fire of the script: every floor square within the
        /// burst's reach of its point alight at once. The first names the
        /// trigger as its cause (or nothing); a later one the put-out it
        /// answers. The event's strength counts the fires lit so far and its
        /// duration is the room it lit in, for the story.
        /// </summary>
        private void StartTheFire(ulong cause)
        {
            if (firesLit >= firePoints.Length)
            {
                phase = LadderPhase.Out;
                return;
            }

            incidentPosition = firePoints[firesLit];
            startRoom = geometry.RoomAtPoint(incidentPosition);
            ulong activated = fire.StartWithoutFlames(incidentPosition, cause);
            firesLit++;
            firstFireTick = firstFireTick < 0 ? context.Tick : firstFireTick;
            incidentEventId = context.Events.Append(context.Tick, default, CausalEventType.DirectorStartedIncident,
                incidentPosition, firesLit, 0, firesLit > 1 ? cause : activated).EventId;
            fire.IgniteAround(incidentPosition, settings.FireBurstRadiusMillimetres, settings.FireBurstSquares, incidentEventId);

            // This fire's own room, and nothing else yet.
            incidentRooms ??= new bool[geometry.RoomCount];
            System.Array.Clear(incidentRooms, 0, incidentRooms.Length);
            if (startRoom >= 0)
            {
                incidentRooms[startRoom] = true;
            }

            escapedThisFire = false;
            PeaceEndsTheRoundAtTick = -1;
            phase = LadderPhase.Burning;
        }

        /// <summary>
        /// The building's move (2026-10-03, the owner: the building follows
        /// one fixed script per seed): a socket crackles and smokes a while
        /// after the first fire -- the curious go and look, the banner says
        /// the building has turned -- and then pops, flooring whoever is
        /// beside it and lighting the floor round it. The same moment in the
        /// round played with nobody at the controls. Nothing, if the flames
        /// got to the socket first.
        /// </summary>
        private void TheBuildingsMove(int tick)
        {
            if (moveSocket < 0 || firstFireTick < 0)
            {
                return;
            }

            if (!moveBegun && tick >= firstFireTick + moveAfterTicks)
            {
                moveBegun = true;
                if (objects.IsWrecked(moveSocket) || flammables.ObjectState(moveSocket) != ObjectBurnState.Intact)
                {
                    return;
                }

                SimulationId id = objects.IdOf(moveSocket);
                LogicalPosition at = objects.PositionOf(moveSocket);
                LastPushTick = tick;
                moveEventId = context.Events.Append(tick, id, CausalEventType.SocketCrackling, at, moveCrackleTicks, 0,
                    incidentEventId).EventId;
                sound.Crash(id, at, settings.CrackleHearingMillimetres, moveEventId);
                movePopsAtTick = checked(tick + moveCrackleTicks);
                return;
            }

            if (movePopsAtTick >= 0 && tick >= movePopsAtTick)
            {
                movePopsAtTick = -1;
                if (!objects.IsWrecked(moveSocket) && flammables.ObjectState(moveSocket) == ObjectBurnState.Intact)
                {
                    objects.Detonate(moveSocket, objects.IdOf(moveSocket), moveEventId);
                }
            }
        }

        private void Climb()
        {
            int tick = context.Tick;
            bool holdingOff = capState == CapState.Massacre;
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

                    // Put out, and the ladder has nothing more to add
                    // (2026-10-02): a socket or the fuse box comes only as
                    // the cap's push, which sets it crackling itself.
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

            // Put out, and that is that (2026-10-02): the bells fall silent
            // a little later and the ladder adds nothing more. It used to
            // send a socket five to ten seconds on, and the fuse box after
            // the socket's put-out.
            previousRoom = startRoom;
            phase = LadderPhase.Out;
            allClearTick = checked(tick + context.Jittered(settings.AllClearAfterTicks));
            nextRung = Rung.None;
            dueTick = int.MaxValue;
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
        /// This socket, or the fuse box, crackles from now, and goes
        /// <see cref="DirectorSettings.CrackleTicks"/> later: it crackles and
        /// smokes for a few seconds first -- the owner's "crackle first", so
        /// a player who is watching can pull people away -- and the curious
        /// go and look. Only the cap's push asks for it (2026-10-02).
        /// </summary>
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

        /// <summary>
        /// The socket for a push: in the room with the most people counted
        /// in <paramref name="calmIn"/> (the cap counts those on course to
        /// get out), never a room the last fire was in, and with at least
        /// <paramref name="atLeast"/> of them there: a bang on an empty room
        /// cuts nobody off. Sockets that tie -- two rooms as busy, or two
        /// sockets in the busiest -- are drawn between; nothing is drawn
        /// when there is no tie. -1 when there is no socket to choose.
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
        /// drawing: a whole socket in a room with somebody on course, the
        /// fuse box once a socket has gone, or an unused bin with nothing
        /// burning. It must mirror <see cref="Push"/> exactly.
        /// </summary>
        private bool HasSomethingToPush()
        {
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
            outOfTheBuilding = 0;
            alive = 0;

            // A card door stands open once it has been swiped (it is an
            // ordinary door from then on) or once it has been pounded down
            // under the player's hand: broken, it still "needs the card" on
            // paper, and reading that as shut (until 2026-10-02) meant the
            // building never turned on a crowd that had broken its way out.
            bool wayOutOpen = false;
            for (int d = 0; d < doors.Count; d++)
            {
                if (geometry.DoorLeadsOutside(d) && doors.StateOf(d) != DoorState.Locked &&
                    (!doors.NeedsKeycard(d) || doors.StateOf(d) == DoorState.Broken))
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
        /// on course, in a fixed order, skipping what is spent. The socket
        /// in the room with the most of them crackles; else, once a socket
        /// has already gone (the fuse box takes every socket left and is
        /// the last resort, not the first), the fuse box; else, with
        /// nothing at all burning and a bin
        /// unused, another bin. Nothing at all when none of these fits --
        /// the on-course crowd is in rooms with no socket -- and the
        /// Director rests and reads again. Each names the push as its cause,
        /// and the push names what set the round going.
        /// </summary>
        private void Push()
        {
            ulong cause = incidentEventId != 0UL ? incidentEventId : fire.ActivationEventId;
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
