using System;
using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Diagnostics
{
    /// <summary>
    /// A building made only for measuring: thirty 6 × 6 m rooms in a grid,
    /// joined by doorways, with one way out, a desk in every room, and as many
    /// people and cardboard boxes as asked for. Nobody is frightened and nothing burns;
    /// everybody picks something new to do every few ticks, so most of them
    /// are walking, bumping into each other and kicking boxes at any moment.
    /// That busy, crowded office is what a tick costs at its worst. Nothing in
    /// the game uses it.
    /// </summary>
    public static class StressBuilding
    {
        private const int Columns = 6;
        private const int Rows = 5;
        private const int RoomSize = 6000;
        private const int Margin = 400;
        private const int Spacing = 700;

        /// <summary>Rooms, doors, <paramref name="people"/> people and <paramref name="things"/> boxes, from the scenario's own tuning.</summary>
        public static ScenarioData Build(ScenarioData template, int people, int things)
        {
            ScenarioData data = template.Clone();
            data.Alarms = new AlarmDefinition[0];
            data.BlastHoles = new SimulationId[0];
            data.Fire.ActivationTick = int.MaxValue;
            data.Fire.SpawnBounds = new LogicalBounds(1000, 1000, 1000, 1000);
            data.Calm.DecisionMinimumTicks = 1;
            data.Calm.DecisionMaximumTicks = 3;

            var rooms = new List<RoomDefinition>();
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    rooms.Add(new RoomDefinition(
                        new SimulationId((ulong)(50000 + row * Columns + column)),
                        new LogicalBounds(column * RoomSize, (column + 1) * RoomSize, row * RoomSize, (row + 1) * RoomSize)));
                }
            }

            // One desk in the middle of each room. Tables are bodies like
            // everything else now, so a building being measured needs some.
            var tables = new List<TableDefinition>();
            for (int room = 0; room < rooms.Count; room++)
            {
                LogicalBounds bounds = rooms[room].Bounds;
                tables.Add(new TableDefinition(
                    new SimulationId((ulong)(51000 + room)),
                    new LogicalPosition((bounds.MinX + bounds.MaxX) / 2, (bounds.MinZ + bounds.MaxZ) / 2), 1200, 700));
            }

            data.Tables = tables.ToArray();

            var doors = new List<DoorDefinition>();
            ulong doorId = 20000UL;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    SimulationId room = rooms[row * Columns + column].RoomId;
                    if (column + 1 < Columns)
                    {
                        doors.Add(new DoorDefinition(new SimulationId(doorId++), room, WallSide.East,
                            row * RoomSize + RoomSize / 2, 1000, false));
                    }

                    if (row + 1 < Rows)
                    {
                        doors.Add(new DoorDefinition(new SimulationId(doorId++), room, WallSide.North,
                            column * RoomSize + RoomSize / 2, 1000, false));
                    }
                }
            }

            doors.Add(new DoorDefinition(new SimulationId(doorId), rooms[rooms.Count - 1].RoomId,
                WallSide.East, (Rows - 1) * RoomSize + RoomSize / 2, 1000, false));
            data.Rooms = rooms.ToArray();
            data.Doors = doors.ToArray();

            // A building of its own has a day of its own: the office's
            // timetable names a room this grid does not have.
            data.Timetable = System.Array.Empty<ScheduledCue>();

            // Spots on a lattice through every room, handed out in turn: a
            // person, then two boxes, and so on, so both are spread evenly.
            var spots = new List<LogicalPosition>();
            int perSide = (RoomSize - 2 * Margin) / Spacing + 1;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    for (int spot = 0; spot < perSide * perSide; spot++)
                    {
                        var where = new LogicalPosition(
                            column * RoomSize + Margin + spot % perSide * Spacing,
                            row * RoomSize + Margin + spot / perSide * Spacing);

                        // Not on the desk in the middle of the room.
                        LogicalBounds desk = data.Tables[row * Columns + column].Bounds;
                        int clear = data.World.OccupancyRadiusMillimetres + 50;
                        if (where.X > desk.MinX - clear && where.X < desk.MaxX + clear &&
                            where.Z > desk.MinZ - clear && where.Z < desk.MaxZ + clear)
                        {
                            continue;
                        }

                        spots.Add(where);
                    }
                }
            }

            if (people + things > spots.Count)
            {
                throw new InvalidOperationException(
                    $"The stress building has room for {spots.Count} people and things, not {people + things}.");
            }

            var agents = new AgentDefinition[people];
            var boxes = new PhysicsObjectDefinition[things];
            int placedPeople = 0;
            int placedThings = 0;
            float thingsPerPerson = people == 0 ? float.MaxValue : things / (float)people;
            for (int s = 0; s < spots.Count && (placedPeople < people || placedThings < things); s++)
            {
                bool personNext = placedPeople < people &&
                                  (placedThings >= things || placedThings >= placedPeople * thingsPerPerson);
                if (personNext)
                {
                    agents[placedPeople] = new AgentDefinition(
                        new SimulationId((ulong)(9000 + placedPeople)), spots[s], CardinalDirection.North,
                        AgentTraitValues.AllOrdinary);
                    placedPeople++;
                }
                else
                {
                    boxes[placedThings] = new PhysicsObjectDefinition(
                        new SimulationId((ulong)(100000 + placedThings)), PhysicsObjectKind.Box, spots[s], 300, 3000);
                    placedThings++;
                }
            }

            data.Agents = agents;
            data.PhysicsObjects = boxes;
            return data;
        }
    }
}
