using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The buildings drawn by code rather than laid out by hand: none for a
    /// level that plays the scenario's own building (the office), and the
    /// three blank test levels of 2026-10-01 (see <see cref="TestBuildings"/>).
    /// A level asset names one of these; the number is what the asset
    /// stores, so the order is appended only.
    /// </summary>
    public enum BuiltInBuilding
    {
        /// <summary>The scenario's own building, whatever it holds.</summary>
        None,

        /// <summary>One big square room with a way out in every wall and forty people: for watching a crowd.</summary>
        SquareRoom,

        /// <summary>A maze of small cells with one way out, a staff member who knows it and visitors who do not: for watching following.</summary>
        Maze,

        /// <summary>A room with one of everything to bump into, pick up, sit on, open or set alight: for watching interactions.</summary>
        InteractionRoom
    }

    /// <summary>
    /// The blank test levels (2026-10-01, the owner's ask: "blank levels to
    /// test panicked crowds ... large square room with walls, maze to test
    /// following, interaction test level"). Each is drawn by code, the way
    /// the stress-profile building is, because it is a shape with numbers
    /// rather than a floor somebody dressed: forty people on a lattice, a
    /// maze from a picture. Each takes the office scenario as its template,
    /// keeps every tuning number in it, and replaces the building, the
    /// cast and the clutter.
    /// <para>
    /// What every one of them does to the template: the tables, loose
    /// things, bells, signs, cable, timetable and traps of the office go;
    /// the cue definitions stay (a scenario needs one of each kind); the
    /// keycard is switched off (there is no card to fetch); the fire never
    /// starts on its own clock, so nothing burns until the player asks; and
    /// the purse opens full, so the doors and the alarm are the owner's to
    /// work. Every number they use lives in a range nothing else in the
    /// game uses (rooms 40001+, doors 41001+, people 42001+, things 43001+,
    /// tables 44001+, pull stations 45001+, bells 45101+), so a test that
    /// mixes a test building with office IDs never collides.
    /// </para>
    /// </summary>
    internal static class TestBuildings
    {
        private const ulong FirstRoomId = 40001UL;
        private const ulong FirstDoorId = 41001UL;
        private const ulong FirstPersonId = 42001UL;
        private const ulong FirstThingId = 43001UL;
        private const ulong FirstTableId = 44001UL;
        private const ulong FirstStationId = 45001UL;
        private const ulong FirstBellId = 45101UL;

        private const int North = 0;
        private const int East = 90;
        private const int South = 180;
        private const int West = 270;

        /// <summary>The template with the building named written over it; <see cref="BuiltInBuilding.None"/> leaves it untouched.</summary>
        public static ScenarioData Apply(BuiltInBuilding building, ScenarioData template)
        {
            switch (building)
            {
                case BuiltInBuilding.None:
                    return template;
                case BuiltInBuilding.SquareRoom:
                    return SquareRoom(template);
                case BuiltInBuilding.Maze:
                    return Maze(template);
                case BuiltInBuilding.InteractionRoom:
                    return InteractionRoom(template);
                default:
                    throw new ArgumentOutOfRangeException(nameof(building), $"Unknown built-in building {building}.");
            }
        }

        /// <summary>
        /// One 24 m square room with a shut, unlocked door in the middle of
        /// every wall, a bell on every wall and a pull station on the north
        /// one, and forty people on a lattice in the middle whose
        /// personalities the seed draws. No furniture and no fire: the crowd
        /// is the whole point. Panicked, they have four ways out to choose
        /// between; the owner can lock any of them with the key.
        /// </summary>
        public static ScenarioData SquareRoom(ScenarioData template)
        {
            const int side = 24000;
            ScenarioData data = Blank(template, "square-room");
            var room = new SimulationId(FirstRoomId);
            data.Rooms = new[] { new RoomDefinition(room, new LogicalBounds(0, side, 0, side)) };
            data.Doors = new[]
            {
                new DoorDefinition(new SimulationId(FirstDoorId), room, WallSide.North, side / 2, 1000, false),
                new DoorDefinition(new SimulationId(FirstDoorId + 1), room, WallSide.East, side / 2, 1000, false),
                new DoorDefinition(new SimulationId(FirstDoorId + 2), room, WallSide.South, side / 2, 1000, false),
                new DoorDefinition(new SimulationId(FirstDoorId + 3), room, WallSide.West, side / 2, 1000, false)
            };

            // A pull station beside the north door, and a bell high on each
            // wall, off the doors' line: a bell fills fourteen metres, so
            // four of them cover the room from any corner.
            data.Alarms = new[] { new AlarmDefinition(new SimulationId(FirstStationId), new LogicalPosition(9000, side - 300)) };
            data.PhysicsObjects = new[]
            {
                Bell(FirstBellId, 6000, side - 150, North),
                Bell(FirstBellId + 1, side - 150, 6000, East),
                Bell(FirstBellId + 2, 18000, 150, South),
                Bell(FirstBellId + 3, 150, 18000, West)
            };

            // Forty people, eight by five, a metre apart, in the middle of
            // the floor and facing every way in turn. No traits authored:
            // the seed deals them, so a new seed is a new crowd.
            var people = new List<AgentDefinition>();
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    int index = row * 8 + column;
                    people.Add(new AgentDefinition(
                        new SimulationId(FirstPersonId + (ulong)index),
                        new LogicalPosition(8500 + column * 1000, 10000 + row * 1000),
                        (CardinalDirection)(index % 4)));
                }
            }

            data.Agents = people.ToArray();
            data.Fire.SpawnBounds = new LogicalBounds(11500, 12500, 11500, 12500);
            return data;
        }

        /// <summary>
        /// The maze as a picture, so it can be redrawn without arithmetic.
        /// Every other character across and down is a cell (<c>.</c>, or
        /// <c>L</c> for the cell the staff member starts in); between two
        /// cells a space is an archway and <c>|</c> or <c>-</c> a wall; on
        /// the outer edge <c>E</c> marks the cell whose outside wall carries
        /// the one way out. The top row is north. Drawn once by a seeded
        /// walk and kept as it came out: sixteen cells from L to E, seven
        /// dead ends.
        /// </summary>
        public static readonly string[] MazePicture =
        {
            "#############",
            "#L|. . . . .#",
            "# #-# # #-#-#",
            "#. .|.|. . .#",
            "#-# # #-#-# #",
            "#.|.|.|. .|.#",
            "# # #-# # # #",
            "#.|. . .|. .#",
            "# #-#-#-#-# #",
            "#. . .|. . .#",
            "# #-#-# #-#-#",
            "#. . . . . .E",
            "#############"
        };

        /// <summary>Each cell of the maze is this big, and the archways between cells this wide (a metre of wall stands either side).</summary>
        public const int MazeCellMillimetres = 4000;
        public const int MazeArchwayMillimetres = 2000;

        /// <summary>
        /// The maze: <see cref="MazePicture"/> built as 4 m cells, each a
        /// room, joined by 2 m archways, with one 1 m way out on the outer
        /// wall. One person works here and knows every turn (the strongest
        /// leader there is, so a rally gathers the room); ten are visitors
        /// who know only the cell they stand in, so they have to follow
        /// somebody or find the way for themselves. Two green signs in the
        /// last two cells point at the way out, a pull station stands in the
        /// first cell and four bells are spread through the maze so the
        /// alarm reaches every cell.
        /// </summary>
        public static ScenarioData Maze(ScenarioData template)
        {
            ScenarioData data = Blank(template, "maze");
            string[] picture = MazePicture;
            int height = (picture.Length - 1) / 2;
            int width = (picture[0].Length - 1) / 2;

            var rooms = new List<RoomDefinition>();
            var doors = new List<DoorDefinition>();
            ulong nextDoor = FirstDoorId;
            int leaderColumn = 0;
            int leaderRow = 0;
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    char mark = picture[2 * row + 1][2 * column + 1];
                    if (mark == '#')
                    {
                        continue;
                    }

                    if (mark == 'L')
                    {
                        leaderColumn = column;
                        leaderRow = row;
                    }

                    LogicalBounds floor = MazeCell(column, row, height);
                    SimulationId roomId = MazeRoomId(column, row, width);
                    rooms.Add(new RoomDefinition(roomId, floor));

                    // An archway east and one south, where the picture is open.
                    if (column + 1 < width && picture[2 * row + 1][2 * column + 2] == ' ')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.East,
                            (floor.MinZ + floor.MaxZ) / 2, MazeArchwayMillimetres, startsLocked: false, isOpening: true));
                    }

                    if (row + 1 < height && picture[2 * row + 2][2 * column + 1] == ' ')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.South,
                            (floor.MinX + floor.MaxX) / 2, MazeArchwayMillimetres, startsLocked: false, isOpening: true));
                    }

                    // The way out: an E on the outer edge beside this cell.
                    if (column == width - 1 && picture[2 * row + 1][2 * width] == 'E')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.East,
                            (floor.MinZ + floor.MaxZ) / 2, 1000, startsLocked: false));
                    }
                    else if (column == 0 && picture[2 * row + 1][0] == 'E')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.West,
                            (floor.MinZ + floor.MaxZ) / 2, 1000, startsLocked: false));
                    }
                    else if (row == 0 && picture[0][2 * column + 1] == 'E')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.North,
                            (floor.MinX + floor.MaxX) / 2, 1000, startsLocked: false));
                    }
                    else if (row == height - 1 && picture[2 * height][2 * column + 1] == 'E')
                    {
                        doors.Add(new DoorDefinition(new SimulationId(nextDoor++), roomId, WallSide.South,
                            (floor.MinX + floor.MaxX) / 2, 1000, startsLocked: false));
                    }
                }
            }

            data.Rooms = rooms.ToArray();
            data.Doors = doors.ToArray();

            // The staff member in the middle of the L cell, and the ten
            // visitors in the corners of that cell and the two cells east
            // of it along the top row, close enough to be rallied.
            LogicalBounds home = MazeCell(leaderColumn, leaderRow, height);
            var people = new List<AgentDefinition>
            {
                new AgentDefinition(new SimulationId(FirstPersonId), home.Centre, CardinalDirection.East,
                    new AgentTraitValues(5, 5, 7, 7, 0, 3, 9))
            };
            var spots = new List<LogicalPosition>();
            for (int step = 0; step < 3; step++)
            {
                LogicalBounds spread = MazeCell(leaderColumn + step, leaderRow, height);
                spots.Add(new LogicalPosition(spread.MinX + 1000, spread.MinZ + 1000));
                spots.Add(new LogicalPosition(spread.MaxX - 1000, spread.MinZ + 1000));
                spots.Add(new LogicalPosition(spread.MinX + 1000, spread.MaxZ - 1000));
                spots.Add(new LogicalPosition(spread.MaxX - 1000, spread.MaxZ - 1000));
            }

            // Visitors of every stripe short of a leader: the nervous who
            // obey, the brave who look first, the ordinary. None cruel, so
            // nobody is contrary on principle.
            var visitorTraits = new[]
            {
                new AgentTraitValues(5, 5, 3, 5, 0, 8, 2),
                new AgentTraitValues(6, 6, 5, 6, 1, 5, 3),
                new AgentTraitValues(4, 5, 2, 7, 0, 9, 1),
                new AgentTraitValues(7, 5, 7, 4, 2, 3, 4),
                new AgentTraitValues(5, 7, 4, 5, 0, 6, 2),
                new AgentTraitValues(3, 4, 3, 8, 0, 7, 1),
                new AgentTraitValues(6, 6, 6, 5, 1, 4, 3),
                new AgentTraitValues(5, 5, 5, 5, 0, 5, 2),
                new AgentTraitValues(4, 6, 2, 6, 1, 8, 1),
                new AgentTraitValues(8, 5, 6, 3, 2, 4, 5)
            };
            for (int i = 0; i < visitorTraits.Length; i++)
            {
                people.Add(new AgentDefinition(new SimulationId(FirstPersonId + 1 + (ulong)i), spots[i],
                        (CardinalDirection)(i % 4), visitorTraits[i])
                    .WithFamiliarity(AgentFamiliarity.Visitor));
            }

            data.Agents = people.ToArray();

            // A station on the west wall of the first cell; bells in four
            // cells spread through the maze, each 150 mm in from its cell's
            // north wall, so every cell is within a bell's fourteen metres.
            data.Alarms = new[] { new AlarmDefinition(new SimulationId(FirstStationId), new LogicalPosition(home.MinX + 300, home.MinZ + 1000)) };
            var things = new List<PhysicsObjectDefinition>();
            ulong nextBell = FirstBellId;
            foreach ((int column, int row) in new[] { (1, 1), (4, 1), (1, 4), (4, 4) })
            {
                LogicalBounds where = MazeCell(column, row, height);
                things.Add(Bell(nextBell++, (where.MinX + where.MaxX) / 2, where.MaxZ - 150, North));
            }

            data.PhysicsObjects = things.ToArray();

            // Two signs, in the two cells before the way out, pointing along
            // the last stretch: the picture's exit is in the south-east
            // corner, reached from the west along the bottom row.
            LogicalBounds exitCell = MazeCell(width - 1, height - 1, height);
            LogicalBounds before = MazeCell(width - 2, height - 1, height);
            data.ExitSigns = new[]
            {
                new ExitSignDefinition(new LogicalPosition(before.Centre.X, before.MaxZ - 400), East),
                new ExitSignDefinition(new LogicalPosition(exitCell.Centre.X, exitCell.MaxZ - 400), East)
            };

            data.Fire.SpawnBounds = new LogicalBounds(home.Centre.X - 500, home.Centre.X + 500, home.Centre.Z - 500, home.Centre.Z + 500);
            return data;
        }

        /// <summary>The floor of one maze cell: the picture's top row is north, so the row number counts down from the top.</summary>
        private static LogicalBounds MazeCell(int column, int row, int height)
        {
            int minX = column * MazeCellMillimetres;
            int minZ = (height - 1 - row) * MazeCellMillimetres;
            return new LogicalBounds(minX, minX + MazeCellMillimetres, minZ, minZ + MazeCellMillimetres);
        }

        private static SimulationId MazeRoomId(int column, int row, int width) =>
            new SimulationId(FirstRoomId + (ulong)(row * width + column));

        /// <summary>
        /// A 16 m by 12 m room with one of everything: light and heavy boxes,
        /// a wooden chair and an office chair at a desk with a laptop on it,
        /// a bag, a bin, a plant, a standing lamp, a fire extinguisher, a
        /// pull station and two bells. Off it: a lobby to the north through
        /// an archway, with the unlocked way out in its far wall; a side room
        /// to the east through a pair of swing doors; and a closet in the
        /// east wall behind an ordinary shut door. A second way out in the
        /// south wall starts locked. Eight people with one dial each turned
        /// up, so every kind of behaviour has somebody to show it, and a
        /// visitor who does not know the way. The fire, when the player asks
        /// for it, starts in the middle of the floor between the people and
        /// the desk.
        /// </summary>
        public static ScenarioData InteractionRoom(ScenarioData template)
        {
            ScenarioData data = Blank(template, "interaction-room");
            var main = new SimulationId(FirstRoomId);
            var lobby = new SimulationId(FirstRoomId + 1);
            var side = new SimulationId(FirstRoomId + 2);
            var closet = new SimulationId(FirstRoomId + 3);
            data.Rooms = new[]
            {
                new RoomDefinition(main, new LogicalBounds(0, 16000, 0, 12000)),
                new RoomDefinition(lobby, new LogicalBounds(4000, 12000, 12000, 16000)),
                new RoomDefinition(side, new LogicalBounds(16000, 22000, 6000, 12000)),
                new RoomDefinition(closet, new LogicalBounds(16000, 20000, 0, 4000))
            };
            data.Doors = new[]
            {
                // The archway into the lobby, and the way out beyond it.
                new DoorDefinition(new SimulationId(FirstDoorId), main, WallSide.North, 8000, 2000, startsLocked: false, isOpening: true),
                new DoorDefinition(new SimulationId(FirstDoorId + 1), lobby, WallSide.North, 8000, 1000, startsLocked: false),

                // The swing doors into the side room.
                new DoorDefinition(new SimulationId(FirstDoorId + 2), main, WallSide.East, 9000, 2000, startsLocked: false, swings: true),

                // The closet's ordinary door, shut but unlocked.
                new DoorDefinition(new SimulationId(FirstDoorId + 3), main, WallSide.East, 2000, 1000, startsLocked: false),

                // The second way out, locked: the owner's key, or the strong.
                new DoorDefinition(new SimulationId(FirstDoorId + 4), main, WallSide.South, 4000, 1000)
            };

            data.Tables = new[] { new TableDefinition(new SimulationId(FirstTableId), new LogicalPosition(8000, 9500), 1600, 800) };
            ulong lamp = FirstThingId + 10;
            data.PhysicsObjects = new[]
            {
                // Boxes: two anybody can carry, one only the strong can shift.
                Thing(FirstThingId, PhysicsObjectKind.Box, 3000, 3000, 300, PrototypeBuilding.BoxMass(300)),
                Thing(FirstThingId + 1, PhysicsObjectKind.Box, 3600, 3900, 400, PrototypeBuilding.BoxMass(400)),
                Thing(FirstThingId + 2, PhysicsObjectKind.Box, 13000, 3000, 600, PrototypeBuilding.BoxMass(600)),

                // Seats at the desk, and a laptop open on it.
                Thing(FirstThingId + 3, PhysicsObjectKind.Chair, 7200, 8300, 450, 5000),
                Thing(FirstThingId + 4, PhysicsObjectKind.OfficeChair, 8800, 8300, 500, 9000),
                new PhysicsObjectDefinition(new SimulationId(FirstThingId + 5), PhysicsObjectKind.Laptop,
                    new LogicalPosition(8000, 9500), 300, 1500, startsResting: true),

                // Odds and ends: a bag to fling, a bin, a plant that never burns.
                Thing(FirstThingId + 6, PhysicsObjectKind.Bag, 11000, 5000, 350, 4000),
                Thing(FirstThingId + 7, PhysicsObjectKind.WasteBin, 15000, 11000, 300, 2000),
                Thing(FirstThingId + 8, PhysicsObjectKind.PottedPlant, 1000, 1000, 450, 25000),

                // A bottle by the west wall, and a lamp in the north-west corner.
                Thing(FirstThingId + 9, PhysicsObjectKind.Extinguisher, 300, 8000, 250, 7000),
                Thing(lamp, PhysicsObjectKind.StandingLamp, 1000, 11000, 300, 6000),
                new PhysicsObjectDefinition(new SimulationId(lamp + 1), PhysicsObjectKind.LampShade,
                    new LogicalPosition(1000, 11000), 350, 1000, startsDormant: true, partOfObjectId: new SimulationId(lamp)),

                // The side room and the closet have a chair and boxes to find.
                Thing(FirstThingId + 12, PhysicsObjectKind.Chair, 19000, 9000, 450, 5000),
                Thing(FirstThingId + 13, PhysicsObjectKind.Box, 20500, 7000, 300, PrototypeBuilding.BoxMass(300)),
                Thing(FirstThingId + 14, PhysicsObjectKind.Box, 18500, 1000, 400, PrototypeBuilding.BoxMass(400)),
                Thing(FirstThingId + 15, PhysicsObjectKind.Box, 19000, 3000, 300, PrototypeBuilding.BoxMass(300)),

                // Bells in the main room and the side room.
                Bell(FirstBellId, 4000, 11850, North),
                Bell(FirstBellId + 1, 19000, 11850, North)
            };
            data.Alarms = new[] { new AlarmDefinition(new SimulationId(FirstStationId), new LogicalPosition(300, 6000)) };

            // Str, Spd, Brv, Cmp, Evl, Nrv, Ldr: one dial each turned up.
            data.Agents = new[]
            {
                Person(FirstPersonId, 4000, 5000, CardinalDirection.North, 10, 6, 6, 3, 5, 3, 3), // the brute
                Person(FirstPersonId + 1, 6000, 5000, CardinalDirection.North, 4, 4, 7, 10, 0, 4, 5), // the saint
                Person(FirstPersonId + 2, 8000, 5000, CardinalDirection.North, 6, 6, 5, 1, 10, 4, 6), // the villain
                Person(FirstPersonId + 3, 10000, 5000, CardinalDirection.North, 6, 5, 7, 8, 1, 3, 10), // the leader
                Person(FirstPersonId + 4, 12000, 5000, CardinalDirection.North, 3, 5, 1, 5, 2, 10, 1), // the nervous wreck
                Person(FirstPersonId + 5, 5000, 2000, CardinalDirection.East, 8, 6, 10, 8, 1, 3, 7), // the hero
                Person(FirstPersonId + 6, 9000, 2000, CardinalDirection.West, 5, 10, 5, 5, 3, 6, 4), // the sprinter
                Person(FirstPersonId + 7, 13000, 1500, CardinalDirection.North, 5, 5, 5, 5, 2, 5, 4) // an ordinary visitor
                    .WithFamiliarity(AgentFamiliarity.Visitor)
            };

            data.ExitSigns = new[] { new ExitSignDefinition(new LogicalPosition(8000, 12600), North) };
            data.Fire.SpawnBounds = new LogicalBounds(7000, 9000, 6500, 7500);
            return data;
        }

        /// <summary>
        /// The template with the office taken out of it: everything a
        /// building brings goes, every tuning number stays. See the class
        /// note for what and why.
        /// </summary>
        private static ScenarioData Blank(ScenarioData template, string scenarioId)
        {
            ScenarioData data = template.Clone();
            data.ScenarioId = scenarioId;
            data.Tables = Array.Empty<TableDefinition>();
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Alarms = Array.Empty<AlarmDefinition>();
            data.ExitSigns = Array.Empty<ExitSignDefinition>();
            data.PowerLines = Array.Empty<PowerLineDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Keycard.Enabled = false;
            data.Fire.ActivationTick = int.MaxValue;
            data.Purse.Starting = data.Purse.Maximum;
            return data;
        }

        /// <summary>A fire alarm bell high on a wall, facing the wall it hangs on, as the office's are.</summary>
        private static PhysicsObjectDefinition Bell(ulong id, int x, int z, int facing)
        {
            return new PhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.AlarmSounder, new LogicalPosition(x, z), 200, 60000,
                initialFacingDegrees: facing);
        }

        private static PhysicsObjectDefinition Thing(ulong id, PhysicsObjectKind kind, int x, int z, int size, int grams)
        {
            return new PhysicsObjectDefinition(new SimulationId(id), kind, new LogicalPosition(x, z), size, grams);
        }

        private static AgentDefinition Person(
            ulong id, int x, int z, CardinalDirection facing,
            int strength, int speed, int bravery, int compassion, int evil, int nervousness, int leadership)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(strength, speed, bravery, compassion, evil, nervousness, leadership));
        }
    }
}
