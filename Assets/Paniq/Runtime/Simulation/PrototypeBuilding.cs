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
    /// north arm, and the south arm is a dead end. The cafeteria has a second
    /// door onto that north arm, so from the cafeteria there is a short way
    /// out and a long one. At the far west end, past everything, is the
    /// maintenance room. The bathroom has three stalls, each its own little
    /// room with its own door, exactly as the storage closet hangs off the
    /// office.
    /// </para>
    /// <para>
    /// <b>Two things are held still on purpose.</b> The open office keeps its
    /// old rectangle and stays the first room, and the storage closet keeps
    /// its old rectangle and its door. Dozens of tests name places inside
    /// them by coordinate, and moving them would have meant rewriting tests
    /// that have nothing to do with the shape of the building.
    /// </para>
    /// </summary>
    internal static class PrototypeBuilding
    {
        /// <summary>The open-plan office. The first room, and unchanged since prototype 1.</summary>
        public static readonly SimulationId Office = new SimulationId(5001UL);

        /// <summary>The storage closet off the office's east wall. Also unchanged.</summary>
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
        /// end of it; the south end is a dead end, which is somewhere wrong for
        /// a frightened person to run.
        /// </summary>
        public static readonly SimulationId Crossbar = new SimulationId(5009UL);

        public static readonly SimulationId StallOne = new SimulationId(5011UL);
        public static readonly SimulationId StallTwo = new SimulationId(5012UL);
        public static readonly SimulationId StallThree = new SimulationId(5013UL);

        /// <summary>
        /// The building. Every edge is a multiple of 250 mm, the size of a
        /// navigation square, so no square is ever half in one room and half
        /// in another.
        /// </summary>
        public static RoomDefinition[] DefaultRooms()
        {
            return new[]
            {
                // Unchanged from prototype 1, and deliberately so.
                new RoomDefinition(Office, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new RoomDefinition(Closet, new LogicalBounds(6000, 8000, 1500, 3500)),

                new RoomDefinition(Corridor, new LogicalBounds(-6000, 13000, 6000, 9000)),
                new RoomDefinition(Cafeteria, new LogicalBounds(2000, 13000, 9000, 17000)),
                new RoomDefinition(MeetingRoom, new LogicalBounds(-6000, 2000, 9000, 17000)),
                new RoomDefinition(Bathroom, new LogicalBounds(8000, 13000, 1000, 6000)),
                new RoomDefinition(Maintenance, new LogicalBounds(-9000, -6000, 6000, 9000)),
                new RoomDefinition(Crossbar, new LogicalBounds(13000, 16000, 2000, 17000)),

                // The stalls: 1.5 m square apiece, hung off the bathroom's
                // south wall. Small, but a person is half a metre across and
                // the navigation squares are a quarter of one, so there is
                // real room to stand and turn round inside each.
                new RoomDefinition(StallOne, new LogicalBounds(8250, 9750, -500, 1000), RoomUse.Stall),
                new RoomDefinition(StallTwo, new LogicalBounds(9750, 11250, -500, 1000), RoomUse.Stall),
                new RoomDefinition(StallThree, new LogicalBounds(11250, 12750, -500, 1000), RoomUse.Stall)
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
                // The host is up first; everybody goes back to their desk, or,
                // having none on this floor, gets up and loiters.
                new CueDefinition(CueKind.MeetingEnds, CueAudience.Room, CueHostRule.SeatedWithMostLeadership, true, goHome),

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

                // Into a free stall, door shut, ten to thirty seconds, door open, and back.
                new CueDefinition(CueKind.ToiletTrip, CueAudience.Self, CueHostRule.Nobody, true,
                    new[]
                    {
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.FreeStall),
                        new ErrandStep(ErrandStepKind.ShutTheDoor),
                        new ErrandStep(ErrandStepKind.StandFor, ErrandTarget.None, 500, 1500),
                        new ErrandStep(ErrandStepKind.OpenTheDoor),
                        new ErrandStep(ErrandStepKind.GoTo, ErrandTarget.Home),
                        new ErrandStep(ErrandStepKind.SitOn, ErrandTarget.Home)
                    }),

                // Back to their own desk: their own idea, and nobody else's business.
                new CueDefinition(CueKind.GoHome, CueAudience.Self, CueHostRule.Nobody, false, goHome)
            };
        }

        /// <summary>
        /// The signs pointing the way out: three down the corridor pointing
        /// east toward the T, and one in each arm of the T pointing north at
        /// the door. The south arm gets one too, because somebody who has run
        /// down the dead end needs telling they have.
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
                new ExitSignDefinition(new LogicalPosition(14500, 4000), North)
            };
        }

        /// <summary>The floor's main fuse box, on the maintenance room wall.</summary>
        public static readonly SimulationId FuseBox = new SimulationId(3281UL);

        /// <summary>
        /// The cable, run as a chain: each socket back to the one before it,
        /// and the first of them back to the fuse box in the maintenance room.
        /// Every leg follows a wall, because that is where cable goes.
        /// <para>
        /// A socket popping lights the cable at both its ends, so the spark
        /// travels outward along the chain whichever link it starts on -- and
        /// the card that pops the fuse box sends it the other way, out of the
        /// maintenance room and along the line of sockets.
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
            return new[]
            {
                new LogicalBounds(-4500, 4500, -4500, 4500),   // the open office
                new LogicalBounds(-4500, 500, 10500, 15500),   // the meeting room
                new LogicalBounds(3500, 11500, 10500, 15500),  // the cafeteria
                new LogicalBounds(9500, 11500, 2500, 4500)     // the bathroom
            };
        }

        /// <summary>
        /// The doors. There is exactly one way out of the building, at the end
        /// of the corridor's north arm, and it starts locked: it is the
        /// player's to open. Every room opens onto the long stretch of corridor
        /// that runs past them all, and the cafeteria has a second door onto
        /// the arm nearer the exit, so from there the choice is a short route
        /// or a long one. Everything inside starts shut but unlocked, so people
        /// can work the doors themselves.
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
                new DoorDefinition(new SimulationId(2011UL), Cafeteria, WallSide.South, 6000, 1000, false),
                new DoorDefinition(new SimulationId(2012UL), Bathroom, WallSide.North, 10500, 1000, false),

                // The cafeteria's second door, onto the arm nearer the exit.
                new DoorDefinition(new SimulationId(2009UL), Cafeteria, WallSide.East, 12000, 1000, false),

                // The maintenance room at the dead west end.
                new DoorDefinition(new SimulationId(2010UL), Corridor, WallSide.West, 7500, 1000, false),

                // The bathroom stalls.
                new DoorDefinition(new SimulationId(2013UL), StallOne, WallSide.North, 9000, 800, false),
                new DoorDefinition(new SimulationId(2014UL), StallTwo, WallSide.North, 10500, 800, false),
                new DoorDefinition(new SimulationId(2015UL), StallThree, WallSide.North, 12000, 800, false),

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

                // One extinguisher in the office and one in the cafeteria.
                Extinguisher(3301UL, -1000, -5700),
                Extinguisher(3302UL, 4000, 16300),

                // Electrical things, which go off when the flames reach them.
                // The microwaves are a bank of them along the cafeteria's far
                // wall, which is what a cafeteria has.
                Microwave(3261UL, 12600, 9600),
                Microwave(3262UL, 12600, 10400),
                WallSocket(3271UL, -5800, -4000),
                WallSocket(3272UL, 5800, 4000),
                WallSocket(3273UL, 12800, 10000),

                // The floor's main fuse box, on the maintenance room wall, as
                // far from the way out as the building goes.
                MainFuseBox(3281UL, -7500, 6500),

                // Four spares the player can stand anywhere with a card. They
                // are nowhere at all until then.
                SpareExtinguisher(3391UL),
                SpareExtinguisher(3392UL),
                SpareExtinguisher(3393UL),
                SpareExtinguisher(3394UL)
            };
        }

        /// <summary>
        /// One alarm on a wall of each big room, just inside it so somebody can
        /// stand at it, and well clear of the doorways -- an alarm beside a
        /// door turns into a queue. The closet, the stalls and the maintenance
        /// room have none: they are cupboards.
        /// </summary>
        public static AlarmDefinition[] DefaultAlarms()
        {
            return new[]
            {
                new AlarmDefinition(new SimulationId(6001UL), new LogicalPosition(-5700, 2000)),
                new AlarmDefinition(new SimulationId(6002UL), new LogicalPosition(3000, 8700)),
                new AlarmDefinition(new SimulationId(6003UL), new LogicalPosition(7000, 16700)),
                new AlarmDefinition(new SimulationId(6004UL), new LogicalPosition(-5700, 16700))
            };
        }

        /// <summary>Which way a chair faces, which is the way whoever sits on it looks.</summary>
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
        private static PhysicsObjectDefinition Microwave(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Microwave, new LogicalPosition(x, z), 450, 14000);
        }

        /// <summary>
        /// The main fuse box: bolted to the wall like a socket, and the biggest
        /// bang in the building when the spark reaches it.
        /// </summary>
        private static PhysicsObjectDefinition MainFuseBox(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.FuseBox, new LogicalPosition(x, z), 600, 90000);
        }

        /// <summary>A wall socket: it never shifts, but it pops.</summary>
        private static PhysicsObjectDefinition WallSocket(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WallSocket, new LogicalPosition(x, z), 160, 60000);
        }

        private static PhysicsObjectDefinition Briefcase(ulong id, int x, int z)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Briefcase, new LogicalPosition(x, z), 400, 6000);
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
