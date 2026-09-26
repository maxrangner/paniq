using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Grid fire. The floor is split into square cells. The fire starts in
    /// one seeded cell; each burning cell periodically lights one random
    /// unburnt north/east/south/west neighbour, so it only ever grows outward
    /// from itself. Burning cells stay burning. The grid covers every room;
    /// cells outside the rooms never burn, and fire only jumps between rooms
    /// through an open door. Fire in another room can be neither touched nor
    /// seen through a wall. This system owns the fire's state and answers
    /// every question about where the fire is.
    /// </summary>
    internal sealed class FireSystem : IThreat
    {
        private static readonly int[] NeighbourOffsetX = { 0, 1, 0, -1 };
        private static readonly int[] NeighbourOffsetZ = { 1, 0, -1, 0 };

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly FireSettings settings;
        private readonly LogicalBounds floor;
        private readonly int radius;
        private readonly List<int> burningCells = new List<int>();
        private readonly List<FireCellSnapshot> cellRecords = new List<FireCellSnapshot>();
        private readonly List<int> neighbourScratch = new List<int>(4);
        private readonly int gridColumns;
        private readonly int gridRows;
        private readonly ulong[] cellEventIds;
        private readonly int[] cellNextSpreadTicks;

        /// <summary>Per cell: the tick until which it is too wet to catch again, after being put out.</summary>
        private readonly int[] cellWetUntilTicks;

        /// <summary>Per cell: ticks of spray it has taken so far.</summary>
        private readonly int[] cellSprayedTicks;

        /// <summary>Per cell: where its record sits in <see cref="cellRecords"/>, or -1.</summary>
        private readonly int[] cellRecordIndex;

        /// <summary>Per cell: the room it is in (0 main, 1 + n side room n), or -1 for no room.</summary>
        private readonly int[] cellRooms;

        /// <summary>Per room: how many of its cells are burning.</summary>
        private readonly int[] burningPerRoom;
        private readonly int originCell;
        private bool active;
        private ulong activationEventId;

        /// <summary>Lays out the grid and draws the seeded ignition point (two random draws: X then Z).</summary>
        public FireSystem(SimulationContext context, WorldGeometry geometry)
        {
            this.context = context;
            this.geometry = geometry;
            settings = context.Scenario.Fire;
            floor = geometry.FireArea;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;

            int cellSize = settings.CellSizeMillimetres;
            gridColumns = (floor.MaxX - floor.MinX + cellSize - 1) / cellSize;
            gridRows = (floor.MaxZ - floor.MinZ + cellSize - 1) / cellSize;
            int cellCount = checked(gridColumns * gridRows);
            cellEventIds = new ulong[cellCount];
            cellNextSpreadTicks = new int[cellCount];
            cellWetUntilTicks = new int[cellCount];
            cellSprayedTicks = new int[cellCount];
            cellRecordIndex = new int[cellCount];
            cellRooms = new int[cellCount];
            for (int cell = 0; cell < cellCount; cell++)
            {
                cellRooms[cell] = geometry.RoomAtPoint(CellBounds(cell).Centre);
                cellRecordIndex[cell] = -1;
                FloorCellCount += cellRooms[cell] >= 0 ? 1 : 0;
            }

            burningPerRoom = new int[geometry.RoomCount];

            // Which preset area the danger begins in, and then whereabouts in
            // it. A scenario with one area draws nothing for the choice: the
            // draw is skipped rather than made and thrown away, so every
            // single-area run -- which is nearly every test -- asks the
            // generator for exactly the two numbers it always did.
            LogicalBounds[] areas = settings.SpawnAreas;
            LogicalBounds area = areas.Length == 1
                ? areas[0]
                : areas[context.Random.NextIntInclusive(0, areas.Length - 1)];
            var origin = new LogicalPosition(
                context.Random.NextIntInclusive(area.MinX, area.MaxX),
                context.Random.NextIntInclusive(area.MinZ, area.MaxZ));
            originCell = NearestSquareThatCanBurn(CellAt(origin));
        }

        public bool Active => active;
        public int BurningCount => burningCells.Count;

        // ---------------------------------------------------------------- as a threat

        /// <summary>The fire that lit it all: what every fright traces back to.</summary>
        ulong IThreat.RootEventId => activationEventId;

        /// <summary>How much fire there is: burning squares.</summary>
        int IThreat.Count => burningCells.Count;

        /// <summary>
        /// The number of burning squares, which is what the round clock watched
        /// before the fire was a threat like any other. Kept as exactly that so
        /// no recorded run moved.
        /// </summary>
        long IThreat.Signature => burningCells.Count;

        /// <summary>
        /// Fire crackles: a calm person this close turns to see what it is.
        /// A bigger fire is heard further, up to a ceiling: one square of
        /// floor crackles, a room ablaze roars.
        /// </summary>
        int IThreat.HeardWithinMillimetres
        {
            get
            {
                HearingSettings hearing = context.Scenario.Hearing;
                long reach = hearing.FireHearingRadiusMillimetres + (long)burningCells.Count * hearing.FireHearingPerCellMillimetres;
                return (int)Math.Min(reach, hearing.FireHearingMaximumMillimetres);
            }
        }

        bool IThreat.IsInRoom(int room) => IsBurningInRoom(room);

        ulong IThreat.Touching(LogicalPosition position) => FindTouching(position);

        ulong IThreat.TouchingAlong(LogicalPosition from, LogicalPosition to) => FindTouchingSweep(from, to);

        /// <summary>Touching the fire sets you alight.</summary>
        void IThreat.Harm(Agent agent, ulong causeEventId, BodySystem body) => body.CatchFire(agent, causeEventId);

        /// <summary>The nearest burning point, and the ignition event of the square it is on.</summary>
        public long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId)
        {
            long distance = NearestCellDistanceSquared(from, out point, out int cell);
            causeEventId = cell >= 0 ? cellEventIds[cell] : 0UL;
            return distance;
        }
        public int GridColumns => gridColumns;
        public int GridRows => gridRows;
        public LogicalPosition Origin => CellBounds(originCell).Centre;

        /// <summary>How many grid cells are floor in some room (the rest can never burn).</summary>
        public int FloorCellCount { get; }

        /// <summary>Whether any floor square of this room (0 main, 1 + n side room n) is burning.</summary>
        public bool IsBurningInRoom(int room) => room >= 0 && burningPerRoom[room] > 0;
        public ulong ActivationEventId => activationEventId;

        /// <summary>Whether somebody has asked for the fire to start but it has not lit yet.</summary>
        private bool startRequested;

        /// <summary>
        /// The player's "trigger event", asking for the fire to start. Nothing
        /// lights here: phase 2 of this tick does the lighting, exactly as it
        /// does when the fire starts itself on a tick count. Asking twice is
        /// the same as asking once.
        /// </summary>
        public void RequestStart() => startRequested = true;

        /// <summary>Whether the fire has been asked to start, whether or not it has lit yet.</summary>
        public bool StartRequested => startRequested || active;

        /// <summary>
        /// Set by a Director that climbs a ladder of small incidents
        /// (prototype 3, 2026-09-26): the fire no longer lights its own square
        /// when it is due, because the Director starts it in a thing -- a
        /// waste bin -- instead; and while it is young it spreads slowly, so
        /// somebody brave has a real chance to put it out.
        /// </summary>
        private bool theDirectorStartsIt;

        /// <summary>See <see cref="theDirectorStartsIt"/>. Called once, before the first tick.</summary>
        public void LeaveTheStartToTheDirector() => theDirectorStartsIt = true;

        /// <summary>
        /// The Director's start: the fire exists from now on -- things near
        /// flames heat, bangs light the floor -- but not one square of floor is
        /// alight. Whatever the Director sets burning next is where the flames
        /// come from. Returns the activation event; asking twice changes
        /// nothing and returns the first.
        /// </summary>
        public ulong StartWithoutFlames(LogicalPosition where, ulong causeEventId)
        {
            if (active)
            {
                return activationEventId;
            }

            active = true;
            startRequested = true;
            activationEventId = context.Events.Append(context.Tick, new SimulationId(Run.FireHazardIdValue),
                CausalEventType.FireActivated, where, 0, 0, causeEventId).EventId;
            return activationEventId;
        }

        /// <summary>Whether any floor square is burning in a room not marked in <paramref name="rooms"/>.</summary>
        public bool BurningOutside(bool[] rooms)
        {
            for (int r = 0; r < burningPerRoom.Length; r++)
            {
                if (!rooms[r] && burningPerRoom[r] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Phase 2: the fire starts when it is due, then spreads.</summary>
        public void Advance()
        {
            int tick = context.Tick;
            if (!active)
            {
                // Either the player sets it off, or it sets itself off on its
                // own tick count -- never both, so a level cannot surprise a
                // player who was told nothing would happen until they pressed.
                // A Director that climbs a ladder starts it itself, in a bin.
                bool due = !theDirectorStartsIt && (context.Scenario.Round.HazardWaitsForTrigger
                    ? startRequested
                    : tick >= settings.ActivationTick);
                if (due)
                {
                    active = true;
                    activationEventId = Ignite(originCell, CausalEventType.FireActivated, 0UL);
                }

                return;
            }

            // Only cells that were burning at the start of this tick spread,
            // in the order they ignited.
            int burningAtStart = burningCells.Count;
            for (int i = 0; i < burningAtStart; i++)
            {
                int cell = burningCells[i];
                if (cellNextSpreadTicks[cell] > tick)
                {
                    continue;
                }

                CollectUnburntNeighbours(cell);
                if (neighbourScratch.Count == 0)
                {
                    // Surrounded by fire; it can never spread again.
                    cellNextSpreadTicks[cell] = int.MaxValue;
                    continue;
                }

                int chosen = neighbourScratch[context.Random.NextIntInclusive(0, neighbourScratch.Count - 1)];
                Ignite(chosen, CausalEventType.FireSpread, cellEventIds[cell]);
                cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            }
        }

        private ulong Ignite(int cell, CausalEventType eventType, ulong parentEventId)
        {
            int tick = context.Tick;
            CausalEvent ignition = context.Events.Append(
                tick,
                new SimulationId(Run.FireHazardIdValue),
                eventType,
                CellBounds(cell).Centre,
                settings.CellSizeMillimetres,
                0,
                parentEventId);
            cellEventIds[cell] = ignition.EventId;
            cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            burningCells.Add(cell);
            burningPerRoom[cellRooms[cell]]++;
            cellRecordIndex[cell] = cellRecords.Count;
            cellRecords.Add(new FireCellSnapshot(cell % gridColumns, cell / gridColumns, CellBounds(cell), tick, ignition.EventId));
            return ignition.EventId;
        }

        /// <summary>
        /// A door into this room became a way through, so fire that had run out
        /// of places to go may have somewhere new after all.
        /// <para>
        /// A burning square is retired for good once every square around it is
        /// alight or walled off -- see <c>cellNextSpreadTicks[cell] =
        /// int.MaxValue</c> above. That was safe while a shut door stopped fire
        /// for ever: a fire pressed against one was genuinely finished. It is
        /// not safe now. A door can be opened by the player, walked open or
        /// shouldered down by somebody, blown off by TNT, or burnt through, and
        /// any of those hands the fire on the other side of it somewhere to go.
        /// Without this, a fire that filled a closed room stayed in it for the
        /// rest of the run however wide the door was afterwards thrown.
        /// </para>
        /// Ascending cell order, so a replay agrees.
        /// </summary>
        public void WakeRoom(int room)
        {
            if (room < 0)
            {
                return;
            }

            for (int i = 0; i < burningCells.Count; i++)
            {
                int cell = burningCells[i];
                if (cellRooms[cell] == room && cellNextSpreadTicks[cell] == int.MaxValue)
                {
                    cellNextSpreadTicks[cell] = checked(context.Tick + NextSpreadDelay());
                }
            }
        }

        /// <summary>
        /// How long a square waits before it spreads again. A fire the
        /// Director started in a thing spreads slowly while it is young --
        /// fewer than <see cref="FireSettings.YoungFireSquares"/> squares
        /// alight -- which is the owner's "a trashcan fire should be slow and
        /// spread slow, so people have a chance to fight it if the right
        /// personality is there". The same one draw either way.
        /// </summary>
        private int NextSpreadDelay()
        {
            int delay = context.Random.NextIntInclusive(settings.SpreadMinimumTicks, settings.SpreadMaximumTicks);
            if (theDirectorStartsIt && burningCells.Count < settings.YoungFireSquares)
            {
                delay = (int)((long)delay * settings.YoungFireSpreadPercent / 100L);
            }

            return delay;
        }

        private void CollectUnburntNeighbours(int cell)
        {
            neighbourScratch.Clear();
            int cellX = cell % gridColumns;
            int cellZ = cell / gridColumns;
            for (int direction = 0; direction < 4; direction++)
            {
                int x = cellX + NeighbourOffsetX[direction];
                int z = cellZ + NeighbourOffsetZ[direction];
                if (x < 0 || z < 0 || x >= gridColumns || z >= gridRows)
                {
                    continue;
                }

                int neighbour = z * gridColumns + x;
                if (cellEventIds[neighbour] != 0UL || cellRooms[neighbour] < 0 || IsWet(neighbour))
                {
                    continue;
                }

                // Into another room only through its open door.
                if (cellRooms[neighbour] != cellRooms[cell] &&
                    !geometry.FireCanCross(cellRooms[cell], CellBounds(cell), cellRooms[neighbour], CellBounds(neighbour)))
                {
                    continue;
                }

                neighbourScratch.Add(neighbour);
            }
        }

        /// <summary>
        /// The drawn square, or the nearest one to it that is floor in some
        /// room. A spot drawn inside a spawn area can still land on a square
        /// whose middle falls in a wall, and a fire lit there would sit in the
        /// brickwork doing nothing at all.
        /// <para>
        /// The search walks outward ring by ring and takes the first square it
        /// finds, in a fixed order every time. It draws no random numbers, so
        /// it cannot shift a replay by itself.
        /// </para>
        /// </summary>
        private int NearestSquareThatCanBurn(int cell)
        {
            if (cellRooms[cell] >= 0)
            {
                return cell;
            }

            int fromColumn = cell % gridColumns;
            int fromRow = cell / gridColumns;
            int furthest = Math.Max(gridColumns, gridRows);
            for (int ring = 1; ring <= furthest; ring++)
            {
                for (int row = fromRow - ring; row <= fromRow + ring; row++)
                {
                    if (row < 0 || row >= gridRows)
                    {
                        continue;
                    }

                    bool edgeRow = row == fromRow - ring || row == fromRow + ring;
                    for (int column = fromColumn - ring; column <= fromColumn + ring; column++)
                    {
                        if (column < 0 || column >= gridColumns)
                        {
                            continue;
                        }

                        // Only the ring itself: everything inside it was looked
                        // at on an earlier, smaller ring.
                        if (!edgeRow && column != fromColumn - ring && column != fromColumn + ring)
                        {
                            continue;
                        }

                        int candidate = row * gridColumns + column;
                        if (cellRooms[candidate] >= 0)
                        {
                            return candidate;
                        }
                    }
                }
            }

            // No square anywhere is floor, which a scenario with a room cannot
            // manage; the drawn square is as good an answer as there is.
            return cell;
        }

        private int CellAt(LogicalPosition position)
        {
            int cellSize = settings.CellSizeMillimetres;
            int x = Math.Max(0, Math.Min(gridColumns - 1, (position.X - floor.MinX) / cellSize));
            int z = Math.Max(0, Math.Min(gridRows - 1, (position.Z - floor.MinZ) / cellSize));
            return z * gridColumns + x;
        }

        private LogicalBounds CellBounds(int cell)
        {
            int cellSize = settings.CellSizeMillimetres;
            int minX = floor.MinX + cell % gridColumns * cellSize;
            int minZ = floor.MinZ + cell / gridColumns * cellSize;
            return new LogicalBounds(minX, Math.Min(floor.MaxX, minX + cellSize), minZ, Math.Min(floor.MaxZ, minZ + cellSize));
        }

        /// <summary>The ignition event of a burning cell.</summary>
        public ulong CellEventId(int cell) => cellEventIds[cell];

        /// <summary>Too wet to catch again, after being put out.</summary>
        public bool IsWet(int cell) => context.Tick < cellWetUntilTicks[cell];

        /// <summary>
        /// Puts a burning square out: it stops burning, cannot spread, and
        /// stays too wet to catch again for a while. Returns false when it
        /// was not alight in the first place.
        /// </summary>
        public bool Douse(int cell, SimulationId source, ulong causeEventId)
        {
            if (!active || cellEventIds[cell] == 0UL)
            {
                return false;
            }

            // It takes a few seconds of spray on one square to put it out.
            if (++cellSprayedTicks[cell] < settings.DouseTicksPerCell)
            {
                return false;
            }

            cellSprayedTicks[cell] = 0;

            burningCells.Remove(cell);
            burningPerRoom[cellRooms[cell]]--;
            cellEventIds[cell] = 0UL;
            cellWetUntilTicks[cell] = checked(context.Tick + settings.DousedWetTicks);

            int record = cellRecordIndex[cell];
            if (record >= 0)
            {
                cellRecords[record] = cellRecords[record].PutOut(context.Tick);
                cellRecordIndex[cell] = -1;
            }

            context.Events.Append(context.Tick, source, CausalEventType.FireDoused,
                CellBounds(cell).Centre, settings.CellSizeMillimetres, 0, causeEventId);
            return true;
        }

        /// <summary>Every square out at once, as though sprayed until it went: for a test that needs a fire put out.</summary>
        internal void PutOutEverythingForTests()
        {
            while (burningCells.Count > 0)
            {
                int cell = burningCells[burningCells.Count - 1];
                cellSprayedTicks[cell] = settings.DouseTicksPerCell;
                Douse(cell, default, 0UL);
            }
        }

        /// <summary>Every burning square with any part of it inside this circle, nearest first.</summary>
        public void CollectBurningWithin(LogicalPosition centre, int reach, List<int> into)
        {
            into.Clear();
            long reachSquared = (long)reach * reach;
            for (int i = 0; i < burningCells.Count; i++)
            {
                int cell = burningCells[i];
                if (LogicalPosition.DistanceSquared(CellBounds(cell).ClosestPoint(centre), centre) <= reachSquared)
                {
                    into.Add(cell);
                }
            }
        }

        /// <summary>The nearest point of a square to somewhere.</summary>
        public LogicalPosition CellClosestPoint(int cell, LogicalPosition from) => CellBounds(cell).ClosestPoint(from);

        /// <summary>The grid cell under a point (clamped to the grid).</summary>
        public int CellIndexAt(LogicalPosition position) => CellAt(position);

        /// <summary>
        /// The square covering a point, or -1 when the point is off the grid
        /// altogether. <see cref="CellIndexAt"/> clamps to the nearest edge
        /// square, which is what the simulation's own callers want because they
        /// only ever ask about places inside the building; a player can click
        /// anywhere, so their clicks come through here instead.
        /// </summary>
        public int CellCovering(LogicalPosition position)
        {
            int cellSize = settings.CellSizeMillimetres;
            int x = (position.X - floor.MinX) / cellSize;
            int z = (position.Z - floor.MinZ) / cellSize;
            if (position.X < floor.MinX || position.Z < floor.MinZ || x >= gridColumns || z >= gridRows)
            {
                return -1;
            }

            return z * gridColumns + x;
        }

        /// <summary>The middle of a grid cell.</summary>
        public LogicalPosition CellCentre(int cell) => CellBounds(cell).Centre;

        /// <summary>
        /// Something burning (not the spreading fire itself) sets a cell
        /// alight: a <c>FireSpread</c> caused by <paramref name="causeEventId"/>.
        /// Nothing happens if it is already burning.
        /// </summary>
        public void IgniteCell(int cell, ulong causeEventId)
        {
            if (!active || cellEventIds[cell] != 0UL || cellRooms[cell] < 0 || IsWet(cell))
            {
                return;
            }

            Ignite(cell, CausalEventType.FireSpread, causeEventId);
        }

        /// <summary>
        /// The player starts a fire where they point. Unlike
        /// <see cref="IgniteCell"/> this works before the scenario's own fire is
        /// due, because starting one is the whole purpose: the first square the
        /// player lights becomes the run's fire. Refuses a square that is not
        /// floor, already alight, or still wet from an extinguisher, and says
        /// whether it caught.
        /// </summary>
        public bool TryIgniteForPlayer(int cell, ulong causeEventId, out ulong eventId)
        {
            eventId = 0UL;
            if (!CanIgniteForPlayer(cell))
            {
                return false;
            }

            if (!active)
            {
                // The player has beaten the scenario to it, so their card is
                // where this run's fire came from.
                active = true;
                activationEventId = Ignite(cell, CausalEventType.FireActivated, causeEventId);
                eventId = activationEventId;
                return true;
            }

            eventId = Ignite(cell, CausalEventType.FireSpread, causeEventId);
            return true;
        }

        /// <summary>
        /// Whether the player could start a fire on this square: it has to be
        /// floor in some room, not already alight, and not still wet from an
        /// extinguisher. Asked before the card is charged for.
        /// </summary>
        public bool CanIgniteForPlayer(int cell)
        {
            return cell >= 0 && cellEventIds[cell] == 0UL && cellRooms[cell] >= 0 && !IsWet(cell);
        }

        /// <summary>
        /// Lights up to <paramref name="cells"/> floor squares within
        /// <paramref name="radius"/> of a blast, row by row so a replay agrees.
        /// Squares that are not floor, already alight or still wet are skipped
        /// and do not count against the limit, and so are squares behind a
        /// wall: only the blast's own room, or a room open to it through a
        /// doorway, catches (2026-09-26). A socket used to light the corridor
        /// through the office wall, which made every socket fire "escape its
        /// room" the moment it started.
        /// </summary>
        public void IgniteAround(LogicalPosition centre, int radius, int cells, ulong causeEventId)
        {
            if (!active || cells <= 0)
            {
                // Before the run's fire has started, a bang is just a bang.
                return;
            }

            int blastRoom = geometry.RoomAtPoint(centre);
            int lit = 0;
            CellRange range = CellsWithin(centre, radius);
            for (int row = range.FirstRow; row <= range.LastRow && lit < cells; row++)
            {
                for (int column = range.FirstColumn; column <= range.LastColumn && lit < cells; column++)
                {
                    int cell = row * gridColumns + column;
                    if (!CanIgniteForPlayer(cell) || !Reaches(blastRoom, cell) ||
                        CellBounds(cell).DistanceSquaredTo(centre) > (long)radius * radius)
                    {
                        continue;
                    }

                    Ignite(cell, CausalEventType.FireSpread, causeEventId);
                    lit++;
                }
            }
        }

        /// <summary>Burning cells in ignition order, as a view that later ignitions do not change. Nothing is copied.</summary>
        public AppendOnlyView<FireCellSnapshot> GetCells() => new AppendOnlyView<FireCellSnapshot>(cellRecords);

        // ---------------------------------------------------------------- queries

        /// <summary>The event ID of the earliest-lit cell overlapping a person's footprint, or 0.</summary>
        public ulong FindTouching(LogicalPosition position)
        {
            return FindTouchingSweep(position, position);
        }

        /// <summary>The event ID of the earliest-lit cell a person's swept footprint overlaps, or 0.</summary>
        public ulong FindTouchingSweep(LogicalPosition start, LogicalPosition end)
        {
            return FindTouchingSweep(start, end, radius);
        }

        /// <summary>The event ID of the earliest-lit cell within <paramref name="reach"/> of a point, or 0.</summary>
        public ulong FindTouchingCircle(LogicalPosition centre, int reach)
        {
            return FindTouchingSweep(centre, centre, reach);
        }

        /// <summary>The event ID of the earliest-lit cell overlapping a rectangle, or 0.</summary>
        public ulong FindTouchingBounds(LogicalBounds bounds)
        {
            int firstX = ColumnOf(bounds.MinX);
            int lastX = ColumnOf(bounds.MaxX);
            int firstZ = RowOf(bounds.MinZ);
            int lastZ = RowOf(bounds.MaxZ);
            ulong earliest = 0UL;
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    ulong eventId = cellEventIds[z * gridColumns + x];
                    if (eventId != 0UL && (earliest == 0UL || eventId < earliest))
                    {
                        earliest = eventId;
                    }
                }
            }

            return earliest;
        }

        private ulong FindTouchingSweep(LogicalPosition start, LogicalPosition end, int radius)
        {
            // Fire on the far side of a wall cannot reach you.
            int bodyRoom = geometry.RoomAtPoint(start);
            int cellSize = settings.CellSizeMillimetres;
            int firstX = Math.Max(0, (Math.Min(start.X, end.X) - radius - floor.MinX) / cellSize);
            int lastX = Math.Min(gridColumns - 1, (Math.Max(start.X, end.X) + radius - floor.MinX) / cellSize);
            int firstZ = Math.Max(0, (Math.Min(start.Z, end.Z) - radius - floor.MinZ) / cellSize);
            int lastZ = Math.Min(gridRows - 1, (Math.Max(start.Z, end.Z) + radius - floor.MinZ) / cellSize);

            ulong earliest = 0UL;
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    int cell = z * gridColumns + x;
                    ulong eventId = cellEventIds[cell];
                    if (eventId == 0UL || (earliest != 0UL && eventId >= earliest) || !Reaches(bodyRoom, cell))
                    {
                        continue;
                    }

                    if (IntegerMath.SweptCircleOverlapsBounds(start, end, radius, CellBounds(cell)))
                    {
                        earliest = eventId;
                    }
                }
            }

            return earliest;
        }

        // Every query below gives exactly the answer of checking every burning
        // cell, ties included (the earliest-lit cell wins), but only looks at
        // grid cells that could matter. When fewer cells burn than it would
        // have to look at, it checks the burning cells directly instead.

        /// <summary>
        /// Squared distance to the nearest burning point, or long.MaxValue
        /// when nothing burns. Also names that point and its cell (-1 when
        /// nothing burns). Ties go to the earliest-lit cell. Searches outward
        /// from the position in square rings of cells and stops once no
        /// closer cell is possible.
        /// </summary>
        public long NearestCellDistanceSquared(LogicalPosition position, out LogicalPosition nearestPoint, out int nearestCell)
        {
            nearestPoint = position;
            nearestCell = -1;
            if (burningCells.Count == 0)
            {
                return long.MaxValue;
            }

            int cellSize = settings.CellSizeMillimetres;
            int centreColumn = ColumnOf(position.X);
            int centreRow = RowOf(position.Z);
            int lastRing = Math.Max(gridColumns, gridRows);
            long nearest = long.MaxValue;
            ulong nearestEventId = 0UL;
            int visited = 0;
            for (int ring = 0; ring <= lastRing; ring++)
            {
                // Every cell in this ring is at least (ring - 1) cells away.
                long gap = (long)(ring - 1) * cellSize;
                if (nearestCell >= 0 && gap > 0L && gap * gap > nearest)
                {
                    break;
                }

                int minRow = centreRow - ring;
                int maxRow = centreRow + ring;
                for (int row = Math.Max(0, minRow); row <= Math.Min(gridRows - 1, maxRow); row++)
                {
                    bool edgeRow = row == minRow || row == maxRow;
                    int step = edgeRow ? 1 : Math.Max(1, ring * 2);
                    for (int column = centreColumn - ring; column <= centreColumn + ring; column += step)
                    {
                        if (column < 0 || column >= gridColumns)
                        {
                            continue;
                        }

                        if (++visited > burningCells.Count)
                        {
                            // Cheaper to check every burning cell after all.
                            return NearestByCheckingEveryCell(position, out nearestPoint, out nearestCell);
                        }

                        int cell = row * gridColumns + column;
                        ulong eventId = cellEventIds[cell];
                        if (eventId == 0UL)
                        {
                            continue;
                        }

                        LogicalPosition point = CellBounds(cell).ClosestPoint(position);
                        long distance = LogicalPosition.DistanceSquared(position, point);
                        if (distance < nearest || (distance == nearest && eventId < nearestEventId))
                        {
                            nearest = distance;
                            nearestEventId = eventId;
                            nearestPoint = point;
                            nearestCell = cell;
                        }
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Squared distance to the nearest burning point that is in one of
        /// two rooms, or long.MaxValue when nothing burns there. What a shut
        /// door asks: the flames that can eat it are the ones on either side
        /// of it, not the ones in the room next door behind a wall. Ties go
        /// to the earliest-lit cell.
        /// </summary>
        public long NearestCellDistanceSquaredInRooms(LogicalPosition position, int roomA, int roomB,
            out LogicalPosition nearestPoint, out int nearestCell)
        {
            long nearest = long.MaxValue;
            ulong nearestEventId = 0UL;
            nearestPoint = position;
            nearestCell = -1;
            for (int i = 0; i < burningCells.Count; i++)
            {
                int cell = burningCells[i];
                int room = cellRooms[cell];
                if (room != roomA && room != roomB)
                {
                    continue;
                }

                LogicalPosition point = CellBounds(cell).ClosestPoint(position);
                long distance = LogicalPosition.DistanceSquared(position, point);
                ulong eventId = cellEventIds[cell];
                if (distance < nearest || (distance == nearest && eventId < nearestEventId))
                {
                    nearest = distance;
                    nearestEventId = eventId;
                    nearestPoint = point;
                    nearestCell = cell;
                }
            }

            return nearest;
        }

        /// <summary>True when some burning point in one of these two rooms is strictly closer than <paramref name="distance"/>.</summary>
        public bool AnyCloserThanInRooms(LogicalPosition position, int distance, int roomA, int roomB)
        {
            long reachSquared = (long)distance * distance;
            for (int i = 0; i < burningCells.Count; i++)
            {
                int cell = burningCells[i];
                int room = cellRooms[cell];
                if ((room == roomA || room == roomB) && CellBounds(cell).DistanceSquaredTo(position) < reachSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private long NearestByCheckingEveryCell(LogicalPosition position, out LogicalPosition nearestPoint, out int nearestCell)
        {
            long nearest = long.MaxValue;
            nearestPoint = position;
            nearestCell = -1;
            for (int i = 0; i < burningCells.Count; i++)
            {
                LogicalBounds bounds = CellBounds(burningCells[i]);
                LogicalPosition point = bounds.ClosestPoint(position);
                long distance = LogicalPosition.DistanceSquared(position, point);
                if (distance < nearest)
                {
                    nearest = distance;
                    nearestPoint = point;
                    nearestCell = burningCells[i];
                }
            }

            return nearest;
        }

        public long NearestDistanceSquared(LogicalPosition position, out LogicalPosition nearestPoint)
        {
            return NearestCellDistanceSquared(position, out nearestPoint, out _);
        }

        public long NearestDistanceSquared(LogicalPosition position)
        {
            return NearestCellDistanceSquared(position, out _, out _);
        }

        /// <summary>True when the straight line between two points passes within <paramref name="clearance"/> of fire (checked at its quarter points).</summary>
        public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance)
        {
            long clearanceSquared = (long)clearance * clearance;
            for (int quarter = 1; quarter <= 3; quarter++)
            {
                var point = new LogicalPosition(
                    (int)(from.X + ((long)to.X - from.X) * quarter / 4),
                    (int)(from.Z + ((long)to.Z - from.Z) * quarter / 4));
                if (AnyWithin(point, clearance, clearanceSquared))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when some burning point is strictly closer than <paramref name="distance"/>.</summary>
        public bool AnyCloserThan(LogicalPosition position, int distance)
        {
            return AnyWithin(position, distance, (long)distance * distance);
        }

        private bool AnyWithin(LogicalPosition position, int reach, long reachSquared)
        {
            CellRange range = CellsWithin(position, reach);
            if (burningCells.Count <= range.Count)
            {
                for (int i = 0; i < burningCells.Count; i++)
                {
                    if (CellBounds(burningCells[i]).DistanceSquaredTo(position) < reachSquared)
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int row = range.FirstRow; row <= range.LastRow; row++)
            {
                for (int column = range.FirstColumn; column <= range.LastColumn; column++)
                {
                    int cell = row * gridColumns + column;
                    if (cellEventIds[cell] != 0UL && CellBounds(cell).DistanceSquaredTo(position) < reachSquared)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Integer test of every burning cell against a forward 90-degree
        /// vision cone. Presentation geometry never decides fear.
        /// </summary>
        public bool IsVisibleFrom(LogicalPosition eye, int heading, int range)
        {
            long rangeSquared = (long)range * range;
            LogicalPosition direction = IntegerMath.Direction(heading);
            int eyeRoom = geometry.RoomAtPoint(eye);
            CellRange cells = CellsWithin(eye, range);
            if (burningCells.Count <= cells.Count)
            {
                for (int i = 0; i < burningCells.Count; i++)
                {
                    if (CellIsVisible(burningCells[i], eyeRoom, eye, direction, rangeSquared))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int row = cells.FirstRow; row <= cells.LastRow; row++)
            {
                for (int column = cells.FirstColumn; column <= cells.LastColumn; column++)
                {
                    int cell = row * gridColumns + column;
                    if (cellEventIds[cell] != 0UL && CellIsVisible(cell, eyeRoom, eye, direction, rangeSquared))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Fire in this cell can touch or be seen from a room: the same room, or one joined to it by an open door.</summary>
        private bool Reaches(int room, int cell)
        {
            return room < 0 || cellRooms[cell] == room || geometry.RoomsOpenToEachOther(room, cellRooms[cell]);
        }

        /// <summary>
        /// The nearest point, centre or a corner of the cell lies inside the
        /// vision cone, and the line of sight to it runs through open
        /// doorways only: the same room, or through the gap of an open door
        /// between the rooms (see <see cref="WorldGeometry.CanSeeBetween"/>).
        /// It used to be "the same room, or any room joined to it by an open
        /// door", which saw through the wall beside the door as readily as
        /// through the door.
        /// </summary>
        private bool CellIsVisible(int cell, int eyeRoom, LogicalPosition eye, LogicalPosition direction, long rangeSquared)
        {
            LogicalBounds bounds = CellBounds(cell);
            LogicalPosition closest = bounds.ClosestPoint(eye);
            if (LogicalPosition.DistanceSquared(eye, closest) > rangeSquared)
            {
                return false;
            }

            bool inCone = InVisionCone(eye, direction, closest, rangeSquared) ||
                          InVisionCone(eye, direction, bounds.Centre, rangeSquared) ||
                          InVisionCone(eye, direction, new LogicalPosition(bounds.MinX, bounds.MinZ), rangeSquared) ||
                          InVisionCone(eye, direction, new LogicalPosition(bounds.MaxX, bounds.MinZ), rangeSquared) ||
                          InVisionCone(eye, direction, new LogicalPosition(bounds.MinX, bounds.MaxZ), rangeSquared) ||
                          InVisionCone(eye, direction, new LogicalPosition(bounds.MaxX, bounds.MaxZ), rangeSquared);
            return inCone && geometry.CanSeeBetween(eyeRoom, eye, cellRooms[cell], closest);
        }

        /// <summary>The grid cells that could hold a point within <paramref name="reach"/> of a position (a few extra are fine).</summary>
        private CellRange CellsWithin(LogicalPosition position, int reach)
        {
            // One millimetre extra on the low side, because a cell whose far
            // edge is exactly <paramref name="reach"/> away belongs to the
            // previous column or row.
            return new CellRange(
                ColumnOf((long)position.X - reach - 1),
                ColumnOf((long)position.X + reach),
                RowOf((long)position.Z - reach - 1),
                RowOf((long)position.Z + reach));
        }

        private int ColumnOf(long x)
        {
            return (int)Math.Max(0L, Math.Min(gridColumns - 1L, FloorDivide(x - floor.MinX, settings.CellSizeMillimetres)));
        }

        private int RowOf(long z)
        {
            return (int)Math.Max(0L, Math.Min(gridRows - 1L, FloorDivide(z - floor.MinZ, settings.CellSizeMillimetres)));
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && value < 0L ? quotient - 1L : quotient;
        }

        private readonly struct CellRange
        {
            public CellRange(int firstColumn, int lastColumn, int firstRow, int lastRow)
            {
                FirstColumn = firstColumn;
                LastColumn = lastColumn;
                FirstRow = firstRow;
                LastRow = lastRow;
            }

            public int FirstColumn { get; }
            public int LastColumn { get; }
            public int FirstRow { get; }
            public int LastRow { get; }
            public int Count => (LastColumn - FirstColumn + 1) * (LastRow - FirstRow + 1);
        }

        private static bool InVisionCone(LogicalPosition eye, LogicalPosition direction, LogicalPosition point, long rangeSquared)
        {
            long offsetX = (long)point.X - eye.X;
            long offsetZ = (long)point.Z - eye.Z;
            if (checked(offsetX * offsetX + offsetZ * offsetZ) > rangeSquared)
            {
                return false;
            }

            long forward = checked(offsetX * direction.X + offsetZ * direction.Z);
            long lateral = checked(offsetX * direction.Z - offsetZ * direction.X);

            // A 45-degree half-angle means |sideways| <= forward.
            return forward >= 0L && Math.Abs(lateral) <= forward;
        }
    }
}
