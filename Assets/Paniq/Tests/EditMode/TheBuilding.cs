using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Places in the default building, by name rather than by coordinate.
    /// <para>
    /// Tests used to write the coordinates out longhand -- a person at
    /// (15000, 0) meaning "somebody in the meeting room, far from the fire".
    /// That reads as a magic number, and when the floor plan changed, seventy
    /// tests failed at once with "agent 2 starts outside every room" and no
    /// hint of which room they had meant. Naming the place says what the test
    /// is actually about, and the next time a room moves this file moves with
    /// it instead of all of them.
    /// </para>
    /// <para>
    /// The open office is deliberately unchanged from prototype 1, and the
    /// storage closet keeps its door and its old floor (it grew north on
    /// 2026-09-24 and south on 2026-09-25), so tests that only ever used the
    /// office need nothing from here.
    /// </para>
    /// </summary>
    internal static class TheBuilding
    {
        /// <summary>The middle of the open-plan office, where the fire usually starts.</summary>
        public static readonly LogicalPosition Office = new LogicalPosition(0, 0);

        /// <summary>The office's south-west corner, about as far from the corridor as the room goes.</summary>
        public static readonly LogicalPosition OfficeFarCorner = new LogicalPosition(-4500, -4500);

        /// <summary>Just inside the office, below its door onto the corridor.</summary>
        public static readonly LogicalPosition OfficeByItsDoor = new LogicalPosition(0, 5000);

        /// <summary>The storage closet off the office's east wall, on the floor it has had since prototype 1.</summary>
        public static readonly LogicalPosition Closet = new LogicalPosition(7000, 2500);

        /// <summary>The middle of the long corridor every room opens onto.</summary>
        public static readonly LogicalPosition Corridor = new LogicalPosition(3500, 7500);

        /// <summary>The corridor's west end, outside the maintenance room's door.</summary>
        public static readonly LogicalPosition CorridorWestEnd = new LogicalPosition(-5000, 7500);

        /// <summary>
        /// Clear floor in the meeting room, across the corridor from the
        /// office. Deliberately not the middle of the room: the middle of the
        /// meeting room is the middle of the meeting table, which is not floor
        /// and which nothing can walk to.
        /// </summary>
        public static readonly LogicalPosition MeetingRoom = new LogicalPosition(-4000, 15500);

        /// <summary>The middle of the cafeteria, at the exit end of the corridor.</summary>
        public static readonly LogicalPosition Cafeteria = new LogicalPosition(7000, 13000);

        /// <summary>The bathroom, across the corridor from the cafeteria.</summary>
        public static readonly LogicalPosition Bathroom = new LogicalPosition(10500, 4000);

        /// <summary>The maintenance room at the dead west end of the floor.</summary>
        public static readonly LogicalPosition Maintenance = new LogicalPosition(-7500, 7500);

        /// <summary>The crossbar of the T, between the way out and the arm down to the stockroom.</summary>
        public static readonly LogicalPosition Crossbar = new LogicalPosition(14500, 9000);

        /// <summary>The stockroom behind the bathroom, on the lane between its two doors.</summary>
        public static readonly LogicalPosition Stockroom = new LogicalPosition(10000, -3000);

        /// <summary>The office's south-east corner, nearer the stockroom door than the corridor door.</summary>
        public static readonly LogicalPosition OfficeSouthEastCorner = new LogicalPosition(4500, -4500);

        /// <summary>Just inside the building's one way out.</summary>
        public static readonly LogicalPosition InsideTheWayOut = new LogicalPosition(14500, 16000);

        /// <summary>Outside the building, through the way out.</summary>
        public static readonly LogicalPosition OutsideTheWayOut = new LogicalPosition(14500, 18500);

        /// <summary>The building's one way out. It starts locked: it is the player's to open.</summary>
        public static readonly SimulationId TheWayOut = new SimulationId(2008UL);

        /// <summary>The office's door onto the storage closet. Unchanged from prototype 1.</summary>
        public static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>The office's door onto the corridor.</summary>
        public static readonly SimulationId OfficeDoor = new SimulationId(2005UL);

        /// <summary>The meeting room's door onto the corridor.</summary>
        public static readonly SimulationId MeetingRoomDoor = new SimulationId(2006UL);

        /// <summary>The meeting room's second door, straight into the cafeteria.</summary>
        public static readonly SimulationId MeetingRoomToCafeteria = new SimulationId(2017UL);

        /// <summary>The cafeteria's door onto the corridor.</summary>
        public static readonly SimulationId CafeteriaDoor = new SimulationId(2011UL);

        /// <summary>The bathroom's door onto the corridor.</summary>
        public static readonly SimulationId BathroomDoor = new SimulationId(2012UL);

        /// <summary>The maintenance room's door onto the corridor's west end.</summary>
        public static readonly SimulationId MaintenanceDoor = new SimulationId(2010UL);

        /// <summary>The archway where the corridor Ts. It is an opening, not a door: nothing shuts it.</summary>
        public static readonly SimulationId Archway = new SimulationId(2016UL);

        /// <summary>The stockroom's door in the office's east wall, at the office's south end.</summary>
        public static readonly SimulationId StockroomDoor = new SimulationId(2018UL);

        /// <summary>The stockroom's door into the crossbar's south end, under the way out.</summary>
        public static readonly SimulationId StockroomToCrossbar = new SimulationId(2019UL);

        /// <summary>
        /// The tower of boxes (prototype 3): the middle of its footprint,
        /// against the corridor's north wall just short of the archway
        /// (since 2026-10-02; it stood in the junction's south-west corner).
        /// </summary>
        public static readonly LogicalPosition TheTower = new LogicalPosition(12000, 8550);

        /// <summary>The tower of boxes, as a stack that comes down when somebody runs into it.</summary>
        public static readonly SimulationId TheTrap = new SimulationId(7001UL);

        /// <summary>The second stack (2026-09-27): four crates standing free at the north end of the stockroom's first crate wall.</summary>
        public static readonly SimulationId TheStockroomTrap = new SimulationId(7002UL);

        /// <summary>The middle of the gap at the stockroom lane's first bend, between the stack and the north wall.</summary>
        public static readonly LogicalPosition StockroomBend = new LogicalPosition(9500, -1450);

        /// <summary>The cubicle landscape east of the crossbar (2026-10-02): the middle of its wide west aisle.</summary>
        public static readonly LogicalPosition CubicleWestAisle = new LogicalPosition(17000, 7000);

        /// <summary>The cubicle landscape's door onto the crossbar beside the way out.</summary>
        public static readonly SimulationId CubicleDoorByTheWayOut = new SimulationId(2020UL);

        /// <summary>The cubicle landscape's door onto the crossbar's south end, opposite the stockroom's.</summary>
        public static readonly SimulationId CubicleSouthDoor = new SimulationId(2021UL);

        /// <summary>The cubicle landscape's door into the stockroom's east lane.</summary>
        public static readonly SimulationId CubicleToStockroom = new SimulationId(2022UL);

        /// <summary>The corridor's east end, just short of the archway into the crossbar.</summary>
        public static readonly LogicalPosition CorridorEastEnd = new LogicalPosition(12000, 7500);

        /// <summary>The building's one pull station, at the corridor's west end beside the maintenance room (prototype 3).</summary>
        public static readonly SimulationId TheAlarm = new SimulationId(6001UL);

        /// <summary>
        /// The shipped building, with the fire pinned to the open office.
        /// <para>
        /// A played round starts the fire in the meeting room (prototype 3;
        /// it used to be one of four rooms), which is covered by its own
        /// tests -- but a test about what a frightened crowd does needs the
        /// fire where the crowd is, every time, or it is really a test of
        /// which square came up.
        /// </para>
        /// </summary>
        public static ScenarioData WithTheFireInTheOffice(ScenarioData data)
        {
            data.Fire.SpawnBounds = new LogicalBounds(-4500, 4500, -4500, 4500);
            return data;
        }

        /// <summary>
        /// The building with the economy taken out of the way: a deep purse, a
        /// deep hand, and an uproar that pays nothing.
        /// <para>
        /// A played round opens with nothing -- no purse and no cards --
        /// and fills the purse from the uproar while the dead deal the cards.
        /// That is the game, and it has its own tests. But a test about what a
        /// crowd does once a door is open is not a test of the economy: it
        /// opens the door as a way of setting the scene. Rather than have forty
        /// such tests quietly fail because the player could not afford the
        /// setup, they say here that the economy is not what they are about.
        /// </para>
        /// <para>
        /// The uproar is silenced as well as the purse filled, because a test
        /// that checks what a card cost counts the purse before and after: with
        /// the building paying for its own commotion in between, the sum came
        /// out a few short and the test was really measuring how much shouting
        /// happened to have gone on.
        /// </para>
        /// </summary>
        public static ScenarioData WithThePlayerAbleToAct(ScenarioData data)
        {

            // The hand's charge is off (2026-09-30): a test holds the hand as
            // long as it likes. HandChargeEditModeTests has the bar.
            data.HandCharge.Enabled = false;

            // A player who can act can open the way out, as they could before
            // the keycard (2026-09-27): the tests here are about doors,
            // cards and the crowd, not about the card. KeycardEditModeTests
            // has the card.
            return WithAnOrdinaryWayOut(data);
        }

        /// <summary>
        /// The way out as a plain locked door (2026-09-27): the keycard put
        /// away before the round starts, so the player's key opens it and
        /// the strong batter it, as before the card existed.
        /// </summary>
        public static ScenarioData WithAnOrdinaryWayOut(ScenarioData data)
        {
            data.Keycard.Enabled = false;
            return data;
        }

        /// <summary>The keycard, authored on the first office desk.</summary>
        public static readonly SimulationId TheKeycard = new SimulationId(3950UL);

        /// <summary>A fire in one square, at a named place, that never spreads on its own.</summary>
        public static void FireAt(ScenarioData data, LogicalPosition where)
        {
            data.Fire.SpawnBounds = new LogicalBounds(where.X, where.X, where.Z, where.Z);
        }

        /// <summary>The building's day with a stall visit that lasts this long: the toilet trip's standing step, re-ranged.</summary>
        public static ScenarioData WithToiletStay(ScenarioData data, int minimumTicks, int maximumTicks)
        {
            return WithStepRange(data, CueKind.ToiletTrip, ErrandStepKind.StandFor, minimumTicks, maximumTicks);
        }

        /// <summary>The building's day with chats that last this long: the chat's talking step, re-ranged.</summary>
        public static ScenarioData WithChatLength(ScenarioData data, int minimumTicks, int maximumTicks)
        {
            return WithStepRange(data, CueKind.Chat, ErrandStepKind.Talk, minimumTicks, maximumTicks);
        }

        private static ScenarioData WithStepRange(ScenarioData data, CueKind kind, ErrandStepKind step, int minimumTicks, int maximumTicks)
        {
            CueDefinition cue = data.CueOf(kind);
            var steps = (ErrandStep[])cue.Script.Clone();
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Kind == step)
                {
                    steps[i] = steps[i].WithTicks(minimumTicks, maximumTicks);
                }
            }

            data.ReplaceCue(cue.WithScript(steps));
            return data;
        }
    }
}
