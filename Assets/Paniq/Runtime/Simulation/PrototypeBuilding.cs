namespace Paniq.Simulation
{
    /// <summary>
    /// The prototype's building, cast and clutter, written out longhand.
    ///
    /// This is content, not rules. It used to live inside
    /// <see cref="ScenarioData"/>, which meant the type that
    /// describes what a scenario can say was also the one place saying what
    /// this particular scenario says -- so the shape of the data and one
    /// building's worth of coordinates could not be told apart. Kept separate,
    /// a second building is a second file rather than an edit to the schema.
    ///
    /// Anything laid out in a scene and baked by Paniq &gt; Bake Scenario From
    /// Scene replaces all of this; it stays as the starting point, and as
    /// something the tests can lean on.
    ///
    /// <para>
    /// <b>The floor plan.</b> A corridor runs the length of the building. The
    /// meeting room and the cafeteria sit along its north side and the open
    /// office and the bathroom along its south, each with a door onto it. At
    /// the east end the corridor Ts: the building's one way out is up the
    /// north arm. The south arm runs down past the bathroom into the
    /// stockroom, a long room full of cardboard boxes behind the bathroom and
    /// the closet, which also has a door into the office's east wall -- so
    /// from the office there are two ways to the way out, north through the
    /// corridor or south-east through the boxes (2026-09-25; the owner asked
    /// for every room but the bathroom to have two ways out). The cafeteria
    /// used to have a second door straight onto the north arm beside the way
    /// out; the owner had it taken out (2026-09-25), so the cafeteria empties
    /// through its swing doors or through the meeting room like everybody
    /// else. At the far west end, past everything, is the maintenance room. The bathroom is one rectangle with three stalls
    /// across the full width of its south wall, each its own little room with
    /// its own door, exactly as the storage closet hangs off the office.
    /// </para>
    /// <para>
    /// <b>Held still on purpose.</b> The open office keeps its old rectangle
    /// and stays the first room, and the storage closet keeps its door and
    /// its old floor. Dozens of tests name places inside them by coordinate,
    /// and moving them would have meant rewriting tests that have nothing to
    /// do with the shape of the building. The closet grew north to the
    /// corridor wall on 2026-09-24 (the owner asked for a bigger one) and
    /// south to the stalls' line on 2026-09-25; every place a test names in
    /// it is still inside it.
    /// </para>
    /// </summary>
    internal static class PrototypeBuilding
    {
        /// <summary>The open-plan office. The first room, and unchanged since prototype 1.</summary>
        public static readonly SimulationId Office = new SimulationId(5001UL);

        /// <summary>The storage closet off the office's east wall: 2 m wide and, since 2026-09-25, 6.5 m long, from the corridor wall down to the stalls' line.</summary>
        public static readonly SimulationId Closet = new SimulationId(5002UL);

        /// <summary>
        /// The long corridor every room opens onto. Three metres wide: two
        /// people meeting head on beside a doorway in a narrow corridor wedge,
        /// and this one carries the whole floor.
        /// </summary>
        public static readonly SimulationId Corridor = new SimulationId(5003UL);

        /// <summary>The cafeteria, north of the corridor at the exit end.</summary>
        public static readonly SimulationId Cafeteria = new SimulationId(5004UL);

        /// <summary>The meeting room, north of the corridor at the far end from the exit.</summary>
        public static readonly SimulationId MeetingRoom = new SimulationId(5005UL);

        /// <summary>The bathroom, south of the corridor, with three stalls off it.</summary>
        public static readonly SimulationId Bathroom = new SimulationId(5006UL);

        /// <summary>The maintenance room at the dead west end, as far from the way out as the floor goes.</summary>
        public static readonly SimulationId Maintenance = new SimulationId(5007UL);

        /// <summary>
        /// The crossbar of the T at the east end. The way out is at the north
        /// end of it; the south end used to be a dead end, and since 2026-09-25
        /// runs on down past the bathroom to the stockroom's door.
        /// </summary>
        public static readonly SimulationId Crossbar = new SimulationId(5009UL);

        public static readonly SimulationId StallOne = new SimulationId(5011UL);
        public static readonly SimulationId StallTwo = new SimulationId(5012UL);
        public static readonly SimulationId StallThree = new SimulationId(5013UL);

        /// <summary>
        /// The stockroom behind the bathroom and the closet: 10 m by 5.5 m of
        /// cardboard boxes, with a door into the office's east wall and one
        /// into the crossbar's south end. It is the office's second way to
        /// the way out -- and a fire that reaches it turns it into a furnace.
        /// </summary>
        public static readonly SimulationId Stockroom = new SimulationId(5014UL);

        /// <summary>
        /// The building. Every edge is a multiple of 250 mm, the size of a
        /// navigation square, so no square is ever half in one room and half
        /// in another.
        /// </summary>
        public static RoomDefinition[] DefaultRooms()
        {
            return new[]
            {
                // The office is unchanged from prototype 1, and deliberately
                // so. The closet keeps its door and its old floor (z 1500 to
                // 3500), runs north to the corridor wall and south to the
                // line of the stalls, so the whole east side of the office
                // shares one straight edge with the stockroom below.
                new RoomDefinition(Office, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new RoomDefinition(Closet, new LogicalBounds(6000, 8000, -500, 6000)),

                new RoomDefinition(Corridor, new LogicalBounds(-6000, 13000, 6000, 9000)),
                new RoomDefinition(Cafeteria, new LogicalBounds(2000, 13000, 9000, 17000)),
                new RoomDefinition(MeetingRoom, new LogicalBounds(-6000, 2000, 9000, 17000)),
                new RoomDefinition(Bathroom, new LogicalBounds(8000, 13000, 1000, 6000)),
                new RoomDefinition(Maintenance, new LogicalBounds(-9000, -6000, 6000, 9000)),
                new RoomDefinition(Crossbar, new LogicalBounds(13000, 16000, -500, 17000)),

                // The stalls, across the full width of the bathroom's south
                // wall so the bathroom is one clean rectangle: two of 1.5 m
                // and a wide one of 2 m in the middle. Small, but a person is
                // half a metre across and the navigation squares are a
                // quarter of one, so there is real room to stand and turn
                // round inside each. (They used to stop 250 mm short of the
                // bathroom's side walls, and those walls sat on fire-square
                // centres, so two columns of floor could never burn.)
                new RoomDefinition(StallOne, new LogicalBounds(8000, 9500, -500, 1000), RoomUse.Stall),
                new RoomDefinition(StallTwo, new LogicalBounds(9500, 11500, -500, 1000), RoomUse.Stall),
                new RoomDefinition(StallThree, new LogicalBounds(11500, 13000, -500, 1000), RoomUse.Stall),

                // The stockroom, last so every room above keeps its index.
                new RoomDefinition(Stockroom, new LogicalBounds(6000, 16000, -6000, -500))
            };
        }

        /// <summary>
        /// What this building's day holds: the meeting breaks up at the minute
        /// mark, over about eight seconds, one person at a time. A minute is
        /// longer than any recorded run, so a meeting under way when the fire
        /// starts breaks up because of the fire and nothing else. There is no
        /// home time on this floor: the way out starts locked and the round
        /// opens calm with no clock, so the whole office queueing at the front
        /// door before the player has pressed anything would change the level
        /// rather than furnish it.
        /// </summary>
        public static ScheduledCue[] DefaultTimetable()
        {
            return new[]
            {
                new ScheduledCue(CueKind.MeetingEnds, 3000, 400, MeetingRoom)
            };
        }

        /// <summary>
        /// What each kind of cue is, as a script of steps (see
        /// <see cref="ErrandStepKind"/>). These are content: a level may say
        /// its meeting ends with a speech, or its toilet trips take longer.
        /// </summary>
        public static CueDefinition[] DefaultCues()
        {
            ErrandStep[] goHome =
            {
                new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.Home),
                new ErrandStep(ErrandStepKind.SitOn, ErrandTarget.Home)
            };

            return new[]
            {
                // The host says so and is up first; everybody goes back to
                // their desk, or, having none on this floor, gets up and
                // loiters.
                new CueDefinition(CueKind.MeetingEnds, CueAudience.Room, CueHostRule.SeatedWithMostLeadership, true, goHome,
                    new[]
                    {
                        new ErrandStep(ErrandStepKind.Say),
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.Home),
                        new ErrandStep(ErrandStepKind.SitOn, ErrandTarget.Home)
                    }),

                // Everybody packs up and leaves.
                new CueDefinition(CueKind.HomeTime, CueAudience.Building, CueHostRule.Nobody, true,
                    new[] { new ErrandStep(ErrandStepKind.Leave) }),

                // Over to the other person and a talk, six to eighteen seconds.
                new CueDefinition(CueKind.Chat, CueAudience.Pair, CueHostRule.Nobody, true,
                    new[]
                    {
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.Partner),
                        new ErrandStep(ErrandStepKind.Talk, ErrandTarget.Partner, 300, 900)
                    }),

                // Into a free stall, door shut, ten to thirty seconds, door
                // open, and back: to their desk, or, having none on this
                // floor, to where they were standing.
                new CueDefinition(CueKind.ToiletTrip, CueAudience.Self, CueHostRule.Nobody, true,
                    new[]
                    {
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.FreeStall),
                        new ErrandStep(ErrandStepKind.ShutTheDoor),
                        new ErrandStep(ErrandStepKind.StandFor, ErrandTarget.None, 500, 1500),
                        new ErrandStep(ErrandStepKind.OpenTheDoor),
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.HomeOrWhereTheyStood),
                        new ErrandStep(ErrandStepKind.SitOn, ErrandTarget.Home)
                    }),

                // Back to their own desk: their own idea, and nobody else's business.
                new CueDefinition(CueKind.GoHome, CueAudience.Self, CueHostRule.Nobody, false, goHome),

                // Off to see what that noise was: toward it, through whatever
                // doors are in the way, a moment's look, and back to the day
                // -- unless what they see frightens them, which ends it.
                new CueDefinition(CueKind.GoAndLook, CueAudience.Self, CueHostRule.Nobody, true,
                    new[]
                    {
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.TheNoise),
                        new ErrandStep(ErrandStepKind.StandFor, ErrandTarget.None, 50, 150)
                    })
            };
        }

        /// <summary>
        /// The signs pointing the way out: three down the corridor pointing
        /// east toward the T, one in each arm of the T pointing north at the
        /// door, and two in the stockroom -- one at its west end pointing
        /// east along the lane, one under its door into the crossbar pointing
        /// north. The south arm keeps its sign: somebody who has come out of
        /// the stockroom needs telling which way the door is.
        /// </summary>
        public static ExitSignDefinition[] DefaultExitSigns()
        {
            const int North = 0;
            const int East = 90;
            return new[]
            {
                new ExitSignDefinition(new LogicalPosition(-3000, 8600), East),
                new ExitSignDefinition(new LogicalPosition(3000, 8600), East),
                new ExitSignDefinition(new LogicalPosition(9000, 8600), East),
                new ExitSignDefinition(new LogicalPosition(14500, 11000), North),
                new ExitSignDefinition(new LogicalPosition(14500, 4000), North),
                new ExitSignDefinition(new LogicalPosition(7500, -3200), East),
                new ExitSignDefinition(new LogicalPosition(14500, -1200), North)
            };
        }

        /// <summary>The floor's main fuse box, on the maintenance room wall.</summary>
        public static readonly SimulationId FuseBox = new SimulationId(3281UL);

        /// <summary>The meeting room's three waste bins: the Director's first fire starts in one of them.</summary>
        public static SimulationId[] MeetingRoomBins() => new[]
        {
            new SimulationId(3205UL), new SimulationId(3206UL), new SimulationId(3207UL)
        };

        /// <summary>
        /// The cable, run as a chain: each socket back to the one before it,
        /// and the first of them back to the fuse box in the maintenance room.
        /// Every leg follows a wall, because that is where cable goes.
        /// <para>
        /// The cable runs one way (2026-09-26): when the fuse box goes, the
        /// spark races out of the maintenance room and down the chain,
        /// setting off each socket in turn. A socket popping by itself lights
        /// nothing, so it never climbs back up to the fuse box.
        /// </para>
        /// </summary>
        public static PowerLineDefinition[] DefaultPowerLines()
        {
            return new[]
            {
                // The fuse box to the office socket: out of the maintenance
                // room, along the corridor's south wall, and down the office's
                // west wall.
                new PowerLineDefinition(FuseBox, new SimulationId(3271UL),
                    new LogicalPosition(-7500, 6500),
                    new LogicalPosition(-5800, 6500),
                    new LogicalPosition(-5800, -4000)),

                // Across the office's south end to the socket on its east side.
                new PowerLineDefinition(new SimulationId(3271UL), new SimulationId(3272UL),
                    new LogicalPosition(-5800, -4000),
                    new LogicalPosition(-5800, -5800),
                    new LogicalPosition(5800, -5800),
                    new LogicalPosition(5800, 4000)),

                // And on to the cafeteria, up the corridor and along its north
                // wall to the bank of microwaves.
                new PowerLineDefinition(new SimulationId(3272UL), new SimulationId(3273UL),
                    new LogicalPosition(5800, 4000),
                    new LogicalPosition(5800, 6200),
                    new LogicalPosition(12800, 6200),
                    new LogicalPosition(12800, 10000))
            };
        }

        /// <summary>
        /// Where the fire is allowed to start: a patch well inside each of the
        /// four rooms people use. One is drawn, then a spot inside it, so one
        /// run begins among the desks, the next behind a bathroom stall door
        /// and the next by the cafeteria counter.
        /// <para>
        /// The corridor is deliberately not on the list. It is the one route
        /// the whole floor shares, and a fire starting in it would cut the
        /// building in half before the player had touched anything.
        /// </para>
        /// </summary>
        public static LogicalBounds[] DefaultFireAreas()
        {
            // Prototype 3 (2026-09-25, the owner's rule): the fire always
            // starts in the meeting room, anywhere in it -- the whole room
            // less half a metre of wall margin, so a seed can put it behind
            // the door, under the table or in a far corner. The office, the
            // cafeteria and the bathroom used to be on this list too; the
            // level is built around the meeting room burning first, with the
            // tower of boxes waiting at the junction for the crowd that runs
            // from it.
            return new[]
            {
                new LogicalBounds(-5500, 1500, 9500, 16500)    // the meeting room
            };
        }

        /// <summary>
        /// The Director's traps (prototype 3, 2026-09-25): the tower of boxes
        /// in the junction's south-west corner, just past the bathroom door
        /// and to the right, which comes down across the archway (2016)
        /// between the corridor and the crossbar. Its boxes are authored in
        /// <see cref="DefaultPhysicsObjects"/>.
        /// </summary>
        public static TrapDefinition[] DefaultTraps()
        {
            return new[]
            {
                new TrapDefinition(new SimulationId(7001UL), new SimulationId(2016UL), new[]
                {
                    new SimulationId(3701UL), new SimulationId(3702UL), new SimulationId(3703UL), new SimulationId(3704UL),
                    new SimulationId(3705UL), new SimulationId(3706UL), new SimulationId(3707UL), new SimulationId(3708UL)
                })
            };
        }

        /// <summary>
        /// The doors. There is exactly one way out of the building, at the end
        /// of the corridor's north arm, and it starts locked: it is the
        /// player's to open. Every room opens onto the long stretch of corridor
        /// that runs past them all, the meeting room and the cafeteria share a
        /// door, and the office has a second door into the stockroom and out
        /// through the crossbar's south end. Everything inside starts shut but
        /// unlocked, so people can work the doors themselves.
        /// </summary>
        public static DoorDefinition[] DefaultDoors()
        {
            return new[]
            {
                // The office's storage closet. Unchanged from prototype 1.
                new DoorDefinition(new SimulationId(2002UL), Office, WallSide.East, 2500, 1000, false),

                // The four rooms onto the corridor.
                new DoorDefinition(new SimulationId(2005UL), Office, WallSide.North, 0, 1000, false),
                new DoorDefinition(new SimulationId(2006UL), MeetingRoom, WallSide.South, -2000, 1000, false),

                // The meeting room's second door, straight into the cafeteria
                // (the owner asked for it, 2026-09-24).
                new DoorDefinition(new SimulationId(2017UL), MeetingRoom, WallSide.East, 14000, 1000, false),
                // The cafeteria's corridor door, the far one from the way out,
                // is a pair of swing doors 2 m wide (the owner asked,
                // 2026-09-25): people push straight through, nobody works
                // them, and the fire burns through them in half the time.
                new DoorDefinition(new SimulationId(2011UL), Cafeteria, WallSide.South, 6000, 2000, false, swings: true),
                new DoorDefinition(new SimulationId(2012UL), Bathroom, WallSide.North, 10500, 1000, false),

                // The cafeteria's door onto the arm nearer the exit (2009) was
                // taken out on 2026-09-25 at the owner's request: the
                // cafeteria goes out through its swing doors or the meeting
                // room, and nobody has a private door beside the way out.

                // The maintenance room at the dead west end.
                new DoorDefinition(new SimulationId(2010UL), Corridor, WallSide.West, 7500, 1000, false),

                // The bathroom stalls, each door in the middle of its stall.
                new DoorDefinition(new SimulationId(2013UL), StallOne, WallSide.North, 8750, 800, false),
                new DoorDefinition(new SimulationId(2014UL), StallTwo, WallSide.North, 10500, 800, false),
                new DoorDefinition(new SimulationId(2015UL), StallThree, WallSide.North, 12250, 800, false),

                // The stockroom (2026-09-25): into the office's east wall at
                // its south end, as far from the office's corridor door as
                // the wall allows, so a fire at one door leaves the other;
                // and into the crossbar's south end, straight under the way
                // out. The room's own doors, in its own walls.
                new DoorDefinition(new SimulationId(2018UL), Stockroom, WallSide.West, -4000, 1000, false),
                new DoorDefinition(new SimulationId(2019UL), Stockroom, WallSide.North, 14500, 1000, false),

                // The T itself: an archway rather than a door, because a
                // corridor that turns a corner is two rectangles and there is
                // nothing in a corridor junction to shut. It is 2.4 m of the
                // corridor's 3 m, which is as wide as an opening may be and
                // still leave the stub of wall either side that a doorway
                // needs to be a doorway.
                new DoorDefinition(new SimulationId(2016UL), Corridor, WallSide.East, 7500, 2400,
                    startsLocked: false, isOpening: true),

                // The building's one way out, at the end of the north arm, as
                // far from the maintenance room as the floor goes.
                new DoorDefinition(new SimulationId(2008UL), Crossbar, WallSide.North, 14500, 1000)
            };
        }

        /// <summary>
        /// Twenty people: eight around the open-plan office, six in a meeting,
        /// four in the cafeteria and two in the bathroom. Each has an authored
        /// personality so every trait shows up in play: Str, Spd, Brv, Cmp,
        /// Evl, Nrv, Ldr.
        /// <para>
        /// The meeting is a client visit. Five of the six are visitors, who
        /// came up in the lift -- which is no way out in a fire -- and have no
        /// idea where the stairs are. Their host works here, and is the
        /// strongest leader in the building: turned up, they can walk the lot of
        /// them out; left alone, they have the signs, their eyes and each
        /// other. Everybody else works on this floor and knows it.
        /// </para>
        /// <para>
        /// The eight in the office each have a desk chair that is theirs, and
        /// the two sitting in the cafeteria have their cafeteria chairs: over
        /// the day they drift back to them (<see cref="ErrandBehaviour"/>).
        /// The visitors and their host have no home on this floor and loiter
        /// once the meeting is over.
        /// </para>
        /// </summary>
        public static AgentDefinition[] DefaultAgents()
        {
            return new[]
            {
                // The open-plan office, on their feet, each beside their own desk chair.
                Agent(1001UL, -5000, -5000, CardinalDirection.North, 5, 5, 5, 5, 2, 5, 4).WithHome(new SimulationId(3101UL)), // ordinary
                Agent(1002UL, 0, -5000, CardinalDirection.East, 9, 6, 6, 3, 6, 3, 3, 3251UL).WithHome(new SimulationId(3102UL)), // the brute, briefcase in hand
                Agent(1003UL, 5000, -5000, CardinalDirection.West, 8, 6, 8, 8, 1, 3, 8).WithHome(new SimulationId(3103UL)), // the hero
                Agent(1004UL, -5000, 0, CardinalDirection.East, 4, 4, 7, 9, 0, 4, 5, 3221UL).WithHome(new SimulationId(3106UL)), // the saint, bag over her shoulder
                Agent(1005UL, 900, 0, CardinalDirection.South, 6, 6, 5, 1, 8, 4, 6).WithHome(new SimulationId(3108UL)), // the villain
                Agent(1006UL, 5000, 0, CardinalDirection.North, 3, 5, 1, 5, 2, 10, 1).WithHome(new SimulationId(3104UL)), // the nervous wreck
                Agent(1007UL, -5000, 5000, CardinalDirection.South, 5, 10, 5, 5, 3, 6, 4).WithHome(new SimulationId(3107UL)), // the sprinter
                Agent(1008UL, 0, 5000, CardinalDirection.West, 7, 5, 4, 2, 9, 5, 5).WithHome(new SimulationId(3105UL)), // the bully

                // The meeting room: a meeting already under way, six of them
                // round the long table. None can see the fire wherever it
                // starts; they learn about it from the shouting and the alarm.
                // Five are clients visiting; the other hero is their host.
                Seated(1009UL, -3600, 13900, South, 5, 5, 6, 5, 3, 4, 4, 3241UL).WithFamiliarity(AgentFamiliarity.Visitor), // ordinary
                Seated(1010UL, -2000, 13900, South, 9, 4, 7, 6, 2, 3, 5, 3242UL).WithFamiliarity(AgentFamiliarity.Visitor), // the strong one
                Seated(1011UL, -400, 13900, South, 4, 7, 3, 7, 1, 7, 2, 3243UL, 3223UL).WithFamiliarity(AgentFamiliarity.Visitor), // the worrier, bag by her chair
                Seated(1012UL, -3600, 12100, North, 3, 6, 2, 8, 0, 9, 1, 3244UL, 3252UL).WithFamiliarity(AgentFamiliarity.Visitor), // the timid carer, briefcase by her chair
                Seated(1013UL, -2000, 12100, North, 7, 8, 8, 4, 7, 2, 6, 3245UL).WithFamiliarity(AgentFamiliarity.Visitor), // the chancer
                Seated(1014UL, -400, 12100, North, 6, 5, 7, 9, 1, 3, 9, 3246UL), // the other hero, and the host

                // The cafeteria: two at a table, whose chairs those are, two on their feet.
                Seated(1015UL, 5000, 13000, South, 5, 5, 4, 5, 4, 6, 4, 3247UL).WithHome(new SimulationId(3247UL)), // ordinary
                Seated(1016UL, 5000, 11000, North, 8, 6, 6, 2, 8, 4, 6, 3248UL).WithHome(new SimulationId(3248UL)), // the other bully
                Agent(1017UL, 9500, 15000, CardinalDirection.West, 4, 9, 5, 6, 2, 6, 3), // the runner
                Agent(1018UL, 11000, 10500, CardinalDirection.North, 3, 4, 2, 4, 3, 8, 2), // the coward

                // The bathroom, furthest from everything.
                Agent(1019UL, 9000, 4500, CardinalDirection.South, 5, 5, 5, 6, 3, 5, 5), // ordinary
                Agent(1020UL, 11500, 4500, CardinalDirection.South, 6, 7, 6, 4, 5, 4, 6) // ordinary
            };
        }

        /// <summary>
        /// Three desks around the office, the meeting room's long table, and
        /// two cafeteria tables.
        /// </summary>
        public static TableDefinition[] DefaultTables()
        {
            return new[]
            {
                new TableDefinition(new SimulationId(4001UL), new LogicalPosition(-2500, -1500), 1200, 700),
                new TableDefinition(new SimulationId(4002UL), new LogicalPosition(2500, 2800), 1200, 700),
                new TableDefinition(new SimulationId(4003UL), new LogicalPosition(-1500, 2000), 1200, 700),

                // The meeting room's long table: one 5.4 x 1 m slab down the
                // middle of the room.
                //
                // It used to be authored as two touching halves. The seam was
                // not a thing in the room, but it was a thing in the rules: one
                // half could collapse while the other stood, and anything the
                // collapsing half had been holding up was left standing inside
                // the half that had not, with nothing able to correct it,
                // because keeping things out of furniture works by asking which
                // side they came from and the answer was "neither".
                new TableDefinition(new SimulationId(4004UL), new LogicalPosition(-2000, 13000), 5400, 1000),

                // The cafeteria's two tables.
                new TableDefinition(new SimulationId(4005UL), new LogicalPosition(5000, 12000), 1200, 1200),
                new TableDefinition(new SimulationId(4006UL), new LogicalPosition(9000, 12000), 1200, 1200)
            };
        }

        /// <summary>
        /// Everything loose in the building: cardboard boxes against the office
        /// walls with three of them stacked in pairs; wooden chairs pulled up to
        /// the office desks; waste bins, potted plants (heavy, and they never
        /// catch), bags and briefcases; laptops on desks; office chairs on
        /// castors around the meeting and cafeteria tables; extinguishers; and
        /// the electrical things, which go off when the flames reach them.
        /// </summary>
        public static PhysicsObjectDefinition[] DefaultPhysicsObjects()
        {
            return new[]
            {
                // Cardboard boxes: knee-high, out of the middle of the floor
                // and against the office walls, three of them stacked in pairs.
                // The upper box of a pair rests on the lower one, so it is in
                // nobody's way until something knocks the stack over. Every
                // stack stands well clear of a door, so a pile that is knocked
                // over lands on open floor, not in somebody's way out.
                Box(3001UL, -5500, 3500, 400, 6000),
                Box(3002UL, -5500, 3500, 300, 3000, restsOnTheOneBelow: true),
                Box(3003UL, 2000, 5500, 400, 6000),
                Box(3004UL, 2000, 5500, 250, 2000, restsOnTheOneBelow: true),
                Box(3005UL, -2500, -5500, 350, 4000),
                Box(3006UL, -2500, -5500, 250, 2000, restsOnTheOneBelow: true),
                Box(3007UL, 3500, -2000, 300, 3000),
                Box(3008UL, -3800, 3500, 250, 2000),

                // The office's wooden chairs, each pulled up to a desk and
                // facing it.
                Chair(3101UL, -2800, -2125, North),
                Chair(3102UL, -2200, -875, South),
                Chair(3103UL, -3375, -1500, East),
                Chair(3104UL, 2200, 2175, North),
                Chair(3105UL, 2800, 3425, South),
                Chair(3106UL, -1800, 1375, North),
                Chair(3107UL, -1200, 2625, South),
                Chair(3108UL, -625, 2000, West),

                // Bins, plants and bags, spread around the floor.
                Bin(3201UL, -5400, 200),
                Bin(3202UL, 5400, -3200),
                Bin(3203UL, 11000, 15000),
                Bin(3204UL, 8600, 2000),

                // The meeting room's three bins (2026-09-26): one of them is
                // where the Director's first fire starts, drawn per round --
                // beside the door, in the far corner, or under the north wall.
                Bin(3205UL, -600, 9400),
                Bin(3206UL, -5400, 16400),
                Bin(3207UL, 0, 16500),
                Plant(3211UL, -5400, -3400),
                Plant(3212UL, 5400, 5400),
                Plant(3213UL, 2600, 16400),
                Plant(3214UL, -5600, 9600),
                Bag(3221UL, -5000, 0),
                Bag(3222UL, 1200, -2600),

                // Two briefcases and two of the bags start in somebody's hand,
                // so they are parked where their owner stands, or on the floor
                // just behind the chair of an owner who is sitting down. Where
                // they are authored only matters if nobody is holding them.
                Bag(3223UL, -400, 14600),
                Bag(3224UL, 10000, 14000),
                Briefcase(3251UL, 0, -5000),
                Briefcase(3252UL, -3600, 11400),

                // Laptops live on desks. One on each office desk, and six down
                // the meeting table in front of the people sitting at it. They
                // only reach the floor when somebody picks one up and drops or
                // throws it, or a blast sweeps the table.
                Laptop(3231UL, -2500, -1500),
                Laptop(3232UL, 2500, 2800),
                Laptop(3233UL, -1500, 2000),
                Laptop(3234UL, -3600, 13200),
                Laptop(3235UL, -2000, 13200),
                Laptop(3236UL, -400, 13200),
                Laptop(3237UL, -3600, 12800),
                Laptop(3238UL, -2000, 12800),
                Laptop(3239UL, -400, 12800),

                // The meeting room's six chairs on castors, and the
                // cafeteria's two, every one of them facing its table.
                OfficeChair(3241UL, -3600, 13900, South),
                OfficeChair(3242UL, -2000, 13900, South),
                OfficeChair(3243UL, -400, 13900, South),
                OfficeChair(3244UL, -3600, 12100, North),
                OfficeChair(3245UL, -2000, 12100, North),
                OfficeChair(3246UL, -400, 12100, North),
                OfficeChair(3247UL, 5000, 13000, South),
                OfficeChair(3248UL, 5000, 11000, North),

                // One extinguisher in the office, one in the cafeteria, and
                // (2026-09-26) one on the meeting room's wall beside its door,
                // so a small fire in there can be put out by somebody brave
                // before it is a big one.
                Extinguisher(3301UL, -1000, -5700),
                Extinguisher(3302UL, 4000, 16300),
                Extinguisher(3303UL, -3300, 9250),

                // Electrical things, which go off when the flames reach them.
                // The microwaves are a bank of them along the cafeteria's far
                // wall, which is what a cafeteria has.
                Microwave(3261UL, 12600, 9600, East),
                Microwave(3262UL, 12600, 10400, East),
                WallSocket(3271UL, -5800, -4000, West),
                WallSocket(3272UL, 5800, 4000, East),
                WallSocket(3273UL, 12800, 10000, East),

                // The floor's main fuse box, on the maintenance room wall, as
                // far from the way out as the building goes.
                MainFuseBox(3281UL, -7500, 6500, South),

                // Four spares the player can stand anywhere with a card. They
                // are nowhere at all until then.
                SpareExtinguisher(3391UL),
                SpareExtinguisher(3392UL),
                SpareExtinguisher(3393UL),
                SpareExtinguisher(3394UL),

                // The rest of the office (2026-09-24). Everything below is
                // knocked about by the physics like the rest: the tall things
                // go over, the things on castors roll, and it all burns.
                // Every prop against a wall faces the room: its facing is the
                // wall at its back (they all used to face north, so the ones
                // on the east and west walls stood side-on to them).

                // A vending machine against the cafeteria's east wall, past
                // the microwaves.
                VendingMachine(3401UL, 12600, 15500, East),

                // Filing cabinets against the office walls, and one in the
                // maintenance room beside the fuse box.
                // Never beside where somebody starts: a cabinet going over on
                // top of a person standing between it and the wall pushed them
                // through the wall (seed 40).
                Cabinet(3411UL, -5700, 4700, West),
                Cabinet(3412UL, 5700, -1200, East),
                Cabinet(3413UL, -8700, 8600, West),

                // Shelves of files on the office's north wall, clear of its
                // door, of books on the meeting room's east wall, and two of
                // stores along the closet's east wall.
                Shelves(3421UL, -1500, 5550),
                Shelves(3422UL, 1550, 10500, East),
                Shelves(3423UL, 7550, 4200, East),
                Shelves(3424UL, 7550, 5300, East),

                // The copier against the office's west wall, the one wall of
                // the office with no door in it, and another parked in the
                // corridor by the cafeteria door, which a crowd will shove
                // along in front of it. The office's copier stood by the east
                // wall until 2026-09-25, when the stockroom door went into
                // that wall: a crowd rolled the copier along the wall into the
                // doorway's approach, and two people pushed it from opposite
                // sides for the rest of the round (seed 42). A thing on
                // castors belongs against a wall it can never block a door in.
                CopyMachine(3431UL, -5550, -2000, West),
                CopyMachine(3432UL, 8000, 8550),

                // Whiteboards on wheels: one at the head of the meeting table,
                // one in the office.
                Whiteboard(3441UL, -5400, 13000, West),
                Whiteboard(3442UL, 2500, -5400, South),

                // Standing lamps in a meeting room corner and a cafeteria
                // corner, each with its shade, which is nowhere until the lamp
                // goes over.
                // The cafeteria's lamp stands in the corner furthest from its
                // doors: kicked over into a doorway, its pole lay across the
                // feet of whoever was opening the door (seed 40).
                StandingLamp(3451UL, 1600, 16600),
                StandingLamp(3452UL, 12600, 16600),
                LampShade(3461UL, 3451UL, 1600, 16600),
                LampShade(3462UL, 3452UL, 12600, 16600),

                // Robot vacuums trundling about the office and the cafeteria.
                RobotVacuum(3471UL, 0, -3000),
                RobotVacuum(3472UL, 8000, 15500),

                // The stockroom's stores (2026-09-25): boxes of every size,
                // many stacked in pairs. Loose things are not on the map
                // people steer by -- they only dodge them when they get there
                // -- so the straight line from the office door (6000, -4000)
                // to the crossbar door (14500, -500) is kept clear of all of
                // them by a metre either side, and nothing stands within
                // 1.5 m of either doorway, where a box at rest would jam the
                // door.
                //
                // A row of stacks along the south wall.
                Box(3501UL, 8000, -5650, 600, 13000),
                Box(3502UL, 8000, -5650, 400, 6000, restsOnTheOneBelow: true),
                Box(3503UL, 9000, -5550, 800, 24000),
                Box(3504UL, 9000, -5550, 500, 9000, restsOnTheOneBelow: true),
                Box(3505UL, 10000, -5700, 500, 9000),
                Box(3506UL, 10000, -5700, 300, 3000, restsOnTheOneBelow: true),
                Box(3507UL, 11000, -5600, 700, 18000),
                Box(3508UL, 11000, -5600, 400, 6000, restsOnTheOneBelow: true),
                Box(3509UL, 12000, -5650, 600, 13000),
                Box(3510UL, 12000, -5650, 350, 4000, restsOnTheOneBelow: true),
                Box(3511UL, 13000, -5550, 800, 24000),
                Box(3512UL, 13000, -5550, 450, 7500, restsOnTheOneBelow: true),
                Box(3513UL, 14000, -5700, 500, 9000),
                Box(3514UL, 14000, -5700, 300, 3000, restsOnTheOneBelow: true),
                Box(3515UL, 15000, -5600, 700, 18000),
                Box(3516UL, 15000, -5600, 400, 6000, restsOnTheOneBelow: true),

                // Crates along the north wall, under the closet and the
                // bathroom, stopping where the lane comes up to the wall.
                Box(3517UL, 6700, -1000, 700, 18000),
                Box(3518UL, 7600, -1000, 600, 13000),
                Box(3519UL, 8500, -1000, 500, 9000),
                Box(3520UL, 9300, -1000, 700, 18000),
                Box(3521UL, 10200, -1000, 600, 13000),
                Box(3522UL, 10200, -1000, 400, 6000, restsOnTheOneBelow: true),
                Box(3523UL, 6600, -2000, 500, 9000),

                // An island south of the lane.
                Box(3524UL, 10000, -4100, 600, 13000),
                Box(3525UL, 10000, -4100, 400, 6000, restsOnTheOneBelow: true),
                Box(3526UL, 10800, -4200, 500, 9000),
                Box(3527UL, 9300, -4300, 450, 7500),
                Box(3528UL, 12500, -4600, 350, 4000),
                Box(3529UL, 13500, -4500, 400, 6000),

                // A column against the east wall, clear of the crossbar door.
                Box(3530UL, 15600, -4000, 600, 13000),
                Box(3531UL, 15600, -3000, 700, 18000),
                Box(3532UL, 15600, -2000, 500, 9000),

                // The fire alarm bells (2026-09-25), one high on a wall of
                // every room people use, including the stockroom and the
                // crossbar. They ring when any pull station below is hit, and
                // the flames reaching one set it off and silence it.
                Sounder(3601UL, -5850, -1500, West),
                Sounder(3602UL, 0, 8850, North),
                Sounder(3603UL, 8000, 16850, North),
                Sounder(3604UL, -5850, 11000, West),
                Sounder(3605UL, 12850, 3000, East),
                Sounder(3606UL, 12000, -650, North),
                Sounder(3607UL, 15850, 8000, East),

                // The tower of boxes (prototype 3, 2026-09-25): two stacks of
                // four, 1.8 m tall, in the junction's south-west corner --
                // out of the bathroom door and to the right, where the
                // corridor meets the crossbar. It stands half a metre off the
                // archway's wall line so it is not "wedged in" the archway
                // while it stands, and clear of the crossbar's south arm.
                // Pinned while it stands (the Director's TrapSystem holds
                // it), it comes down across the archway once the fire is
                // lit and somebody comes near. Each box is a plain 600 mm
                // box of 13 kg: the strong can throw one clear and most
                // people can carry one.
                Box(3701UL, 13600, 6350, 600, 13000),
                Box(3702UL, 13600, 6350, 600, 13000, restsOnTheOneBelow: true),
                Box(3703UL, 13600, 6350, 600, 13000, restsOnTheOneBelow: true),
                Box(3704UL, 13600, 6350, 600, 13000, restsOnTheOneBelow: true),
                Box(3705UL, 14200, 6350, 600, 13000),
                Box(3706UL, 14200, 6350, 600, 13000, restsOnTheOneBelow: true),
                Box(3707UL, 14200, 6350, 600, 13000, restsOnTheOneBelow: true),
                Box(3708UL, 14200, 6350, 600, 13000, restsOnTheOneBelow: true)
            };
        }

        /// <summary>
        /// The pull stations: one on a wall of each big room, just inside it so
        /// somebody can stand at it, and well clear of the doorways -- an alarm
        /// beside a door turns into a queue. The closet, the stalls and the
        /// maintenance room have none: they are cupboards. The crossbar has
        /// none either: it had one on its east wall below the way out (6006)
        /// until the owner had it taken out (2026-09-25), so nobody stops
        /// beside the exit to hit a switch. The bells that ring are things on
        /// the walls (see <see cref="Sounder"/>).
        /// </summary>
        public static AlarmDefinition[] DefaultAlarms()
        {
            // Prototype 3 (2026-09-25, the owner's rule): one pull station
            // in the whole building, at the far west end of the corridor on
            // its north wall, beside the maintenance room (the fuse box
            // room) and past the meeting room's door. Pulling it means
            // walking toward the fire, so only the brave do. The stations in
            // the office (6001's old spot), the corridor's middle (6002), the
            // cafeteria (6003), the meeting room (6004) and the stockroom
            // (6005) are gone; the bells on the walls are untouched.
            return new[]
            {
                new AlarmDefinition(new SimulationId(6001UL), new LogicalPosition(-5700, 8700))
            };
        }

        /// <summary>
        /// Which way a thing faces: for a chair, the way whoever sits on it
        /// looks; for anything that stands against a wall, the wall at its
        /// back (every prop's front is drawn on its -Z side, so it faces the
        /// room when its facing is the wall).
        /// </summary>
        private const int North = 0;
        private const int East = 90;
        private const int South = 180;
        private const int West = 270;

        private static PhysicsObjectDefinition Chair(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Chair, new LogicalPosition(x, z), 450, 5000,
                initialFacingDegrees: facing);
        }

        /// <summary>An office chair: the same size as a wooden one, but on castors (see the kind's friction).</summary>
        private static PhysicsObjectDefinition OfficeChair(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.OfficeChair, new LogicalPosition(x, z), 500, 9000,
                initialFacingDegrees: facing);
        }

        private static PhysicsObjectDefinition Bin(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WasteBin, new LogicalPosition(x, z), 300, 2000);
        }

        private static PhysicsObjectDefinition Plant(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.PottedPlant, new LogicalPosition(x, z), 450, 25000);
        }

        private static PhysicsObjectDefinition Bag(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Bag, new LogicalPosition(x, z), 350, 4000);
        }

        /// <summary>A fire extinguisher: small, heavy for its size, and it never burns.</summary>
        private static PhysicsObjectDefinition Extinguisher(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(x, z), 250, 7000);
        }

        /// <summary>
        /// A laptop, open on a desk. It rests on the table it stands on, so it
        /// is in nobody's way until somebody lifts it off.
        /// </summary>
        private static PhysicsObjectDefinition Laptop(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Laptop, new LogicalPosition(x, z), 300, 1500,
                startsResting: true);
        }

        private static AgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership));
        }

        /// <summary>
        /// Somebody who begins the run already sitting on a named chair, and
        /// who may have something with them. They are authored standing where
        /// the chair is; the run settles them onto it at tick zero, facing the
        /// way the chair faces.
        /// </summary>
        private static AgentDefinition Seated(
            ulong id, int x, int z, int facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership,
            ulong chair, ulong carrying = 0UL)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z),
                DirectionOf(facing),
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership),
                new SimulationId(carrying), new SimulationId(chair));
        }

        /// <summary>The cardinal direction for one of the four chair facings above.</summary>
        private static CardinalDirection DirectionOf(int facingDegrees)
        {
            return (CardinalDirection)(IntegerMath.NormalizeDegrees(facingDegrees) / 90);
        }

        /// <summary>The same person, but walking in with something in their hand.</summary>
        private static AgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership,
            ulong carrying)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership),
                new SimulationId(carrying));
        }

        /// <summary>
        /// One of the spare extinguishers the player's card puts down. It is not
        /// in the world until then, so its position is never used.
        /// </summary>
        private static PhysicsObjectDefinition SpareExtinguisher(ulong id)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(0, 0), 220, 9000, true);
        }

        /// <summary>A microwave on a counter: heavy, and it goes off with a bang.</summary>
        private static PhysicsObjectDefinition Microwave(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Microwave, new LogicalPosition(x, z), 450, 14000,
                initialFacingDegrees: facing);
        }

        /// <summary>
        /// The main fuse box: bolted to the wall like a socket, and the biggest
        /// bang in the building when the spark reaches it.
        /// </summary>
        private static PhysicsObjectDefinition MainFuseBox(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.FuseBox, new LogicalPosition(x, z), 600, 90000,
                initialFacingDegrees: facing);
        }

        /// <summary>A wall socket: it never shifts, but it pops.</summary>
        private static PhysicsObjectDefinition WallSocket(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WallSocket, new LogicalPosition(x, z), 160, 60000,
                initialFacingDegrees: facing);
        }

        /// <summary>A fire alarm bell: bolted high on the wall like a socket, and it pops when the flames reach it.</summary>
        private static PhysicsObjectDefinition Sounder(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.AlarmSounder, new LogicalPosition(x, z), 200, 60000,
                initialFacingDegrees: facing);
        }

        private static PhysicsObjectDefinition Briefcase(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Briefcase, new LogicalPosition(x, z), 400, 6000);
        }

        /// <summary>A vending machine: 0.7 m square, 1.8 m tall and 160 kg; only a blast tips it.</summary>
        private static PhysicsObjectDefinition VendingMachine(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.VendingMachine, new LogicalPosition(x, z), 700, 160000,
                initialFacingDegrees: facing);
        }

        /// <summary>A filing cabinet: half a metre square, chest high, 60 kg of steel and paper.</summary>
        private static PhysicsObjectDefinition Cabinet(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Cabinet, new LogicalPosition(x, z), 500, 60000,
                initialFacingDegrees: facing);
        }

        /// <summary>Shelves: 0.9 m wide, 1.8 m tall, shallow, 45 kg with the books on them. Their back is at +Z, so the facing is the wall they stand against.</summary>
        private static PhysicsObjectDefinition Shelves(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Shelves, new LogicalPosition(x, z), 900, 45000,
                initialFacingDegrees: facing);
        }

        /// <summary>The copier: 0.8 m across, waist high, 100 kg, on castors (see the kind's friction).</summary>
        private static PhysicsObjectDefinition CopyMachine(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.CopyMachine, new LogicalPosition(x, z), 800, 100000,
                initialFacingDegrees: facing);
        }

        /// <summary>A whiteboard on wheels: a metre wide (the widest a thing may be), 15 kg; its board lies across the facing.</summary>
        private static PhysicsObjectDefinition Whiteboard(ulong id, int x, int z, int facing = North)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Whiteboard, new LogicalPosition(x, z), 1000, 15000,
                initialFacingDegrees: facing);
        }

        /// <summary>A standing lamp: a 0.3 m base, 6 kg, and it goes over at a touch.</summary>
        private static PhysicsObjectDefinition StandingLamp(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.StandingLamp, new LogicalPosition(x, z), 300, 6000);
        }

        /// <summary>
        /// A lamp's shade: part of the lamp, so it is nowhere until the lamp
        /// goes over, when it comes off at the lamp's top and drops to the
        /// floor. Authored at the lamp's spot, which is never used.
        /// </summary>
        private static PhysicsObjectDefinition LampShade(ulong id, ulong lamp, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.LampShade, new LogicalPosition(x, z), 350, 1000,
                startsDormant: true, partOfObjectId: new SimulationId(lamp));
        }

        /// <summary>A robot vacuum: a 0.33 m disc, 4 kg, that drives itself about (see the kind's table row).</summary>
        private static PhysicsObjectDefinition RobotVacuum(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.RobotVacuum, new LogicalPosition(x, z), 330, 4000);
        }

        /// <summary>
        /// A cardboard box. With <paramref name="restsOnTheOneBelow"/> it is the
        /// upper box of a stacked pair, authored at the same spot as the one it
        /// stands on.
        /// </summary>
        private static PhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams,
            bool restsOnTheOneBelow = false)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams,
                startsResting: restsOnTheOneBelow);
        }
    }
}
