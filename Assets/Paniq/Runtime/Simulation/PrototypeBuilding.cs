namespace Paniq.Simulation
{
    /// <summary>
    /// The prototype's building, cast and clutter, written out longhand.
    ///
    /// This is content, not rules. It used to live inside
    /// <see cref="FireReactionScenarioData"/>, which meant the type that
    /// describes what a scenario can say was also the one place saying what
    /// this particular scenario says -- so the shape of the data and one
    /// building's worth of coordinates could not be told apart. Kept separate,
    /// a second building is a second file rather than an edit to the schema.
    ///
    /// Anything laid out in a scene and baked by Paniq > Bake Scenario From
    /// Scene replaces all of this; it stays as the starting point, and as
    /// something the tests can lean on.
    /// </summary>
    internal static class PrototypeBuilding
    {
        /// <summary>
        /// Twenty people: ten on their feet around the open-plan office where
        /// the fire starts, and ten in the meeting room, nine of them sitting
        /// at the table and one standing at the end of it presenting. Each has
        /// an authored personality so every trait shows up in play: Str, Spd,
        /// Brv, Cmp, Evl, Nrv, Ldr.
        /// </summary>
        public static FireReactionAgentDefinition[] DefaultAgents()
        {
            return new[]
            {
                Agent(1001UL, -5000, -5000, CardinalDirection.North, 5, 5, 5, 5, 2, 5, 4), // ordinary
                Agent(1002UL, 0, -5000, CardinalDirection.East, 9, 6, 6, 3, 6, 3, 3, 3251UL), // the brute, briefcase in hand
                Agent(1003UL, 5000, -5000, CardinalDirection.West, 8, 6, 8, 8, 1, 3, 8), // the hero
                Agent(1004UL, -5000, 0, CardinalDirection.East, 4, 4, 7, 9, 0, 4, 5, 3221UL), // the saint, bag over her shoulder
                Agent(1005UL, 900, 0, CardinalDirection.South, 6, 6, 5, 1, 8, 4, 6), // the villain
                Agent(1006UL, 5000, 0, CardinalDirection.North, 3, 5, 1, 5, 2, 10, 1), // the nervous wreck
                Agent(1007UL, -5000, 5000, CardinalDirection.South, 5, 10, 5, 5, 3, 6, 4), // the sprinter
                Agent(1008UL, 0, 5000, CardinalDirection.West, 7, 5, 4, 2, 9, 5, 5), // the bully
                Agent(1009UL, 5000, 5000, CardinalDirection.South, 3, 4, 2, 4, 3, 8, 2), // the coward
                Agent(1010UL, 0, -1800, CardinalDirection.North, 5, 5, 5, 6, 3, 5, 5), // ordinary

                // The meeting room: a meeting already under way. Nine of them
                // are sitting at the long table, each in a chair facing it,
                // and the tenth is on their feet at the near end of it talking
                // to the room. None of them can see the fire when it starts;
                // they learn about it through the shouting and the alarm.
                Seated(1011UL, 12000, 900, South, 5, 5, 6, 5, 3, 4, 4, 3241UL), // ordinary
                Seated(1012UL, 13350, 900, South, 9, 4, 7, 6, 2, 3, 5, 3242UL), // the strong one
                Seated(1013UL, 14700, 900, South, 4, 7, 3, 7, 1, 7, 2, 3243UL, 3223UL), // the worrier, bag on her lap
                Seated(1014UL, 16050, 900, South, 6, 5, 5, 5, 5, 5, 5, 3244UL), // ordinary
                Seated(1015UL, 12000, -900, North, 3, 6, 2, 8, 0, 9, 1, 3245UL), // the timid carer
                Seated(1016UL, 13350, -900, North, 7, 8, 8, 4, 7, 2, 6, 3246UL), // the chancer
                Seated(1017UL, 14700, -900, North, 5, 5, 4, 5, 4, 6, 4, 3247UL), // ordinary
                Seated(1018UL, 16050, -900, North, 8, 6, 6, 2, 8, 4, 6, 3248UL, 3252UL), // the other bully, briefcase by his chair
                Seated(1019UL, 17100, 0, West, 4, 9, 5, 6, 2, 6, 3, 3249UL), // the runner, at the head of the table

                // On her feet at the near end of the table, presenting.
                Agent(1020UL, 10700, 0, CardinalDirection.East, 6, 5, 7, 9, 1, 3, 9) // the other hero
            };
        }

        /// <summary>
        /// The doors. There is exactly one way out of the building, in the
        /// meeting room at the far end, and it starts locked: it is the
        /// player's to open. The office has no way out of its own, so everybody
        /// in it has to cross the corridor and the meeting room to escape, and
        /// the corridor is the one pinch point the whole building funnels
        /// through. The three inside doors (the storage closet, and the
        /// corridor at each end) start shut but not locked, so people can open
        /// them themselves.
        /// </summary>
        public static FireReactionDoorDefinition[] DefaultDoors()
        {
            return new[]
            {
                new FireReactionDoorDefinition(new SimulationId(2002UL), Office, WallSide.East, 2500, 1000, false),
                new FireReactionDoorDefinition(new SimulationId(2005UL), Office, WallSide.East, 0, 1000, false),
                new FireReactionDoorDefinition(new SimulationId(2006UL), Corridor, WallSide.East, 0, 1000, false),

                // The meeting room's one way out of the building, at the far
                // end from the corridor: leaving it is a choice between the
                // length of the room and going back toward the fire.
                new FireReactionDoorDefinition(new SimulationId(2008UL), MeetingRoom, WallSide.East, 2500, 1000)
            };
        }

        /// <summary>
        /// Everything loose in the building. Eight cardboard boxes, 0.25-0.4 m
        /// and 2-6 kg, against the walls with three of them stacked in pairs;
        /// eight 5 kg wooden chairs pulled up to the office desks; waste bins,
        /// potted plants (heavy, and they never catch), bags and briefcases;
        /// nine laptops, every one of them on a desk; and nine office chairs on
        /// castors around the meeting table.
        /// </summary>
        public static FireReactionPhysicsObjectDefinition[] DefaultPhysicsObjects()
        {
            return new[]
            {
                // Cardboard boxes: knee-high, out of the middle of the floor
                // and against the walls, three of them stacked in pairs. The
                // upper box of a pair rests on the lower one, so it is in
                // nobody's way until something knocks the stack over.
                // Every stack stands well clear of a door, so a pile that gets
                // knocked over lands on open floor, not in somebody's way out.
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

                // The office's clutter: bins, plants and bags.
                Bin(3201UL, -5400, 200),
                Bin(3202UL, 5400, -3200),
                Bin(3203UL, 10200, -1500),
                Bin(3204UL, 18500, 3800),
                Plant(3211UL, -5400, -3400),
                Plant(3212UL, 5400, 5400),
                Plant(3213UL, 9500, 3900),
                Plant(3214UL, 18600, -3900),
                Bag(3221UL, -5000, 0),
                Bag(3222UL, 1200, -2600),

                // Two briefcases and two of the bags start in somebody's hand,
                // so they are parked where their owner stands, or on the floor
                // just behind the chair of an owner who is sitting down. Where
                // they are authored only matters if nobody is holding them.
                Bag(3223UL, 14700, 1600),
                Bag(3224UL, 17500, 3400),
                Briefcase(3251UL, 0, -5000),
                Briefcase(3252UL, 16050, -1600),

                // Laptops live on desks. One on each office desk, and six down
                // the meeting table in front of the people sitting at it. They
                // only reach the floor when somebody picks one up and drops or
                // throws it, or a blast sweeps the table.
                Laptop(3231UL, -2500, -1500),
                Laptop(3232UL, 2500, 2800),
                Laptop(3233UL, -1500, 2000),
                Laptop(3234UL, 12000, 250),
                Laptop(3235UL, 13350, 250),
                Laptop(3236UL, 14700, 250),
                Laptop(3237UL, 16050, 250),
                Laptop(3238UL, 12000, -250),
                Laptop(3239UL, 13350, -250),

                // The meeting room's nine chairs on castors, pulled up to the
                // long table: four down each side and one at the far head,
                // every one of them facing the table.
                OfficeChair(3241UL, 12000, 900, South),
                OfficeChair(3242UL, 13350, 900, South),
                OfficeChair(3243UL, 14700, 900, South),
                OfficeChair(3244UL, 16050, 900, South),
                OfficeChair(3245UL, 12000, -900, North),
                OfficeChair(3246UL, 13350, -900, North),
                OfficeChair(3247UL, 14700, -900, North),
                OfficeChair(3248UL, 16050, -900, North),
                OfficeChair(3249UL, 17100, 0, West),

                // One extinguisher by each big room's wall.
                Extinguisher(3301UL, -1000, -5700),
                Extinguisher(3302UL, 14000, -4300),

                // Electrical things, which go off when the flames reach them.
                Microwave(3261UL, 5400, -1400),
                Microwave(3262UL, 18600, -1800),
                WallSocket(3271UL, -5800, -4000),
                WallSocket(3272UL, 5800, 4000),
                WallSocket(3273UL, 18800, -1000),

                // Four spares the player can stand anywhere with a card. They
                // are nowhere at all until then.
                SpareExtinguisher(3391UL),
                SpareExtinguisher(3392UL),
                SpareExtinguisher(3393UL),
                SpareExtinguisher(3394UL)
            };
        }

        /// <summary>The open-plan office where the fire starts.</summary>
        public static readonly SimulationId Office = new SimulationId(5001UL);

        /// <summary>The storage closet off the office's east wall.</summary>
        public static readonly SimulationId Closet = new SimulationId(5002UL);

        /// <summary>
        /// The short corridor from the office to the meeting room. Three metres
        /// wide rather than two: the meeting room has only one other way out,
        /// so most of the people in it come back down here, and two people
        /// meeting head on beside a doorway in a narrow corridor wedge.
        /// </summary>
        public static readonly SimulationId Corridor = new SimulationId(5003UL);

        /// <summary>The meeting room at the far end of the corridor.</summary>
        public static readonly SimulationId MeetingRoom = new SimulationId(5004UL);

        /// <summary>
        /// The building: a 12 × 12 m open-plan office where the fire starts, a
        /// 2 × 2 m storage closet against its east wall, a 3 m corridor east
        /// out of the office, and a 10 × 9 m meeting room at the end of it —
        /// smaller than the office, so the two rooms read as different places.
        /// None of them is a refuge; they are simply rooms.
        /// </summary>
        public static FireReactionRoomDefinition[] DefaultRooms()
        {
            return new[]
            {
                new FireReactionRoomDefinition(Office, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new FireReactionRoomDefinition(Closet, new LogicalBounds(6000, 8000, 1500, 3500)),
                new FireReactionRoomDefinition(Corridor, new LogicalBounds(6000, 9000, -1500, 1500)),
                new FireReactionRoomDefinition(MeetingRoom, new LogicalBounds(9000, 19000, -4500, 4500))
            };
        }

        /// <summary>Three 1.2 × 0.7 m desks around the office, and the meeting room's 5.4 m table in two halves.</summary>
        public static FireReactionTableDefinition[] DefaultTables()
        {
            return new[]
            {
                new FireReactionTableDefinition(new SimulationId(4001UL), new LogicalPosition(-2500, -1500), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4002UL), new LogicalPosition(2500, 2800), 1200, 700),
                new FireReactionTableDefinition(new SimulationId(4003UL), new LogicalPosition(-1500, 2000), 1200, 700),

                // The meeting room's long table: one 5.4 x 1 m table down the
                // middle of the room, from x 11300 to x 16700.
                //
                // It used to be authored as two touching halves. The seam was
                // not a thing in the room, but it was a thing in the rules: one
                // half could collapse while the other stood, and anything the
                // collapsing half had been holding up was left standing inside
                // the half that had not, with nothing able to correct it,
                // because keeping things out of furniture works by asking which
                // side they came from and the answer was "neither".
                new FireReactionTableDefinition(new SimulationId(4004UL), new LogicalPosition(14000, 0), 5400, 1000)
            };
        }

        /// <summary>Which way a chair faces, which is the way whoever sits on it looks.</summary>
        private const int North = 0;
        private const int East = 90;
        private const int South = 180;
        private const int West = 270;

        private static FireReactionPhysicsObjectDefinition Chair(ulong id, int x, int z, int facing = North)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Chair, new LogicalPosition(x, z), 450, 5000,
                initialFacingDegrees: facing);
        }

        /// <summary>An office chair: the same size as a wooden one, but on castors (see the kind's friction).</summary>
        private static FireReactionPhysicsObjectDefinition OfficeChair(ulong id, int x, int z, int facing = North)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.OfficeChair, new LogicalPosition(x, z), 500, 9000,
                initialFacingDegrees: facing);
        }

        private static FireReactionPhysicsObjectDefinition Bin(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WasteBin, new LogicalPosition(x, z), 300, 2000);
        }

        private static FireReactionPhysicsObjectDefinition Plant(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.PottedPlant, new LogicalPosition(x, z), 450, 25000);
        }

        private static FireReactionPhysicsObjectDefinition Bag(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Bag, new LogicalPosition(x, z), 350, 4000);
        }

        /// <summary>A fire extinguisher: small, heavy for its size, and it never burns.</summary>
        private static FireReactionPhysicsObjectDefinition Extinguisher(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(x, z), 250, 7000);
        }

        /// <summary>
        /// A laptop, open on a desk. It rests on the table it stands on, so it
        /// is in nobody's way until somebody lifts it off.
        /// </summary>
        private static FireReactionPhysicsObjectDefinition Laptop(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Laptop, new LogicalPosition(x, z), 300, 1500,
                startsResting: true);
        }

        private static FireReactionAgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership));
        }

        /// <summary>
        /// Somebody who begins the run already sitting on a named chair, and
        /// who may have something with them. They are authored standing where
        /// the chair is; the run settles them onto it at tick zero, facing the
        /// way the chair faces.
        /// </summary>
        private static FireReactionAgentDefinition Seated(
            ulong id, int x, int z, int facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership,
            ulong chair, ulong carrying = 0UL)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z),
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
        private static FireReactionAgentDefinition Agent(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership,
            ulong carrying)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership),
                new SimulationId(carrying));
        }

        /// <summary>
        /// One of the spare extinguishers the player's card puts down. It is not
        /// in the world until then, so its position is never used.
        /// </summary>
        private static FireReactionPhysicsObjectDefinition SpareExtinguisher(ulong id)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Extinguisher, new LogicalPosition(0, 0), 220, 9000, true);
        }

        /// <summary>A microwave on a counter: heavy, and it goes off with a bang.</summary>
        private static FireReactionPhysicsObjectDefinition Microwave(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Microwave, new LogicalPosition(x, z), 450, 14000);
        }

        /// <summary>A wall socket: it never shifts, but it pops.</summary>
        private static FireReactionPhysicsObjectDefinition WallSocket(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.WallSocket, new LogicalPosition(x, z), 160, 60000);
        }

        private static FireReactionPhysicsObjectDefinition Briefcase(ulong id, int x, int z)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Briefcase, new LogicalPosition(x, z), 400, 6000);
        }

        /// <summary>
        /// One alarm on a wall of each room, just inside it so somebody can
        /// stand at it. The closet has none: it is a cupboard.
        /// </summary>
        public static FireReactionAlarmDefinition[] DefaultAlarms()
        {
            return new[]
            {
                // The open-plan office, on the west wall.
                new FireReactionAlarmDefinition(new SimulationId(6001UL), new LogicalPosition(-5700, 2000)),

                // The corridor, on its north wall.
                new FireReactionAlarmDefinition(new SimulationId(6002UL), new LogicalPosition(7500, 700)),

                // The meeting room, on its north wall, well clear of both its
                // doors: an alarm beside a doorway turns into a queue.
                new FireReactionAlarmDefinition(new SimulationId(6003UL), new LogicalPosition(11000, 4200))
            };
        }

        /// <summary>
        /// A cardboard box. With <paramref name="restsOnTheOneBelow"/> it is the
        /// upper box of a stacked pair, authored at the same spot as the one it
        /// stands on.
        /// </summary>
        private static FireReactionPhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams,
            bool restsOnTheOneBelow = false)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams,
                startsResting: restsOnTheOneBelow);
        }
    }
}
