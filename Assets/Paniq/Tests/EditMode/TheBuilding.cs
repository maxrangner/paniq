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
    /// 2026-09-24), so tests that only ever used the office need nothing from
    /// here.
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

        /// <summary>The crossbar of the T, between the way out and the dead end.</summary>
        public static readonly LogicalPosition Crossbar = new LogicalPosition(14500, 9000);

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

        /// <summary>The cafeteria's second door, onto the arm nearer the way out.</summary>
        public static readonly SimulationId CafeteriaShortcut = new SimulationId(2009UL);

        /// <summary>The bathroom's door onto the corridor.</summary>
        public static readonly SimulationId BathroomDoor = new SimulationId(2012UL);

        /// <summary>The maintenance room's door onto the corridor's west end.</summary>
        public static readonly SimulationId MaintenanceDoor = new SimulationId(2010UL);

        /// <summary>The archway where the corridor Ts. It is an opening, not a door: nothing shuts it.</summary>
        public static readonly SimulationId Archway = new SimulationId(2016UL);

        /// <summary>
        /// The shipped building, with the fire pinned to the open office.
        /// <para>
        /// A played round draws the fire from one of four preset areas, so
        /// three runs in four start it somewhere other than the office. That is
        /// the point of it, and it is covered by its own tests -- but a test
        /// about what a frightened crowd does needs the fire where the crowd
        /// is, every time, or it is really a test of which room came up.
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
        /// A played round opens with nothing -- no influence and no cards --
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
            data.Influence.Starting = 100000;
            data.Influence.Maximum = 100000;
            data.Influence.StartingHand = EveryCard();
            data.Influence.OpeningDrawCount = 0;
            data.Influence.UproarSmall = 0;
            data.Influence.UproarMiddling = 0;
            data.Influence.UproarBig = 0;
            return data;
        }

        /// <summary>
        /// A deep hand: a dozen of every card there is. Playing one takes it
        /// out of the hand, so a test that plays the same card five times over
        /// -- running TNT out of charges, say -- needs more than one of it.
        /// </summary>
        public static PlayerCommandType[] EveryCard()
        {
            var kinds = new[]
            {
                PlayerCommandType.PlayBeefcake,
                PlayerCommandType.PlayCourage,
                PlayerCommandType.PlayTerror,
                PlayerCommandType.PlayBastard,
                PlayerCommandType.PlayColdHeart,
                PlayerCommandType.SpawnFire,
                PlayerCommandType.SpawnExtinguisher,
                PlayerCommandType.BlastWall,
                PlayerCommandType.PopFuseBox
            };

            const int spare = 12;
            var hand = new PlayerCommandType[kinds.Length * spare];
            for (int i = 0; i < hand.Length; i++)
            {
                hand[i] = kinds[i % kinds.Length];
            }

            return hand;
        }

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
