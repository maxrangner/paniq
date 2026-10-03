using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The office played as a loop (2026-10-02, the owner: "the game loop
    /// itself is lacking or vague ... small things change the outcome from 0
    /// saved to all saved ... make this prototype work"). The same floor
    /// plan, cast and furniture as <see cref="PrototypeBuilding"/>, with the
    /// round's rules changed so that a round is many small fates instead of
    /// one big one. It is written as a list of changes laid over the office
    /// scenario, the way the test levels are, so the office as it was stays
    /// playable beside it and every recorded replay of it stays as recorded.
    /// <para>
    /// <b>What was wrong, measured.</b> Thirty seeds of the office left
    /// alone: eight rounds saved nobody or next to nobody, eleven saved
    /// twenty-four or more of thirty-four, and almost none fell between.
    /// Three things did it, each a single gate the whole crowd passed at
    /// once: the one way out locked behind one keycard (the card never
    /// came: everybody died); the Director's cap, which popped the socket
    /// beside a queue of twenty-five the moment the door opened (one to
    /// twenty-six of them died, by where they happened to stand); and a bin
    /// fire somebody brave put out in three rounds of four once the door
    /// was open (everybody lived, and the round never ended). With the way
    /// out unlocked, no cap, and a real fire in the middle of the floor at
    /// half its speed, the same crowd left alone saved between nine and
    /// thirty-two, most rounds within four people of the average.
    /// </para>
    /// <para>
    /// <b>What is different here.</b>
    /// </para>
    /// <list type="bullet">
    /// <item>The way out is an ordinary door, shut and unlocked. There is no
    /// keycard on this level, and nobody locks the way out behind them.</item>
    /// <item>The Director opens with a real fire in the middle of the
    /// building (<see cref="DirectorSettings.StartsARealFire"/>), in one of
    /// <see cref="FireSpots"/>, drawn per round: it cuts the floor in two,
    /// and who is on the wrong side of it changes from seed to seed. There
    /// is no ladder and no cap: the building does not strike at whoever is
    /// getting out.</item>
    /// <item>The fire spreads at half the office's pace, so a round lasts
    /// two minutes rather than one and what people decide matters more than
    /// how far they stood from the door.</item>
    /// <item>A burning thing no longer heats what stands on the far side of
    /// a wall: the fire comes through doors, where it can be seen coming and
    /// shut out.</item>
    /// </list>
    /// </summary>
    internal static class OfficeLoopLevel
    {
        /// <summary>What the scenario is called, so its best score and its replays are its own.</summary>
        public const string ScenarioId = "prototype-fire-2-fl-small";

        /// <summary>
        /// Where the fire may start, one drawn per round, each of them between
        /// the west wing and the way out, so that the office, the meeting room
        /// and the cafeteria are cut off from the corridor's short way and the
        /// way out is the long way round, through the stockroom and the
        /// cubicles: "the T", the corridor in front of the bathroom just short
        /// of the archway; the corridor outside the cafeteria's swing doors;
        /// and inside the bathroom by its door, which reaches the corridor a
        /// little later. Measured 2026-10-03: a fire at the west end of the
        /// corridor or in the meeting room left the way out's side of it
        /// clear and saved four in five left alone; at the T, a third.
        /// </summary>
        public static LogicalBounds[] FireSpots()
        {
            return new[]
            {
                new LogicalBounds(8500, 10500, 6800, 8200),
                new LogicalBounds(6800, 8200, 6800, 8200),
                new LogicalBounds(9800, 11200, 4600, 5600)
            };
        }

        /// <summary>The second pull station (2026-10-03): on the corridor's north wall near the archway, so a hand can get the bells rung wherever the fire is.</summary>
        public static readonly SimulationId ArchwayPullStation = new SimulationId(6002UL);

        /// <summary>The socket the building's move sets crackling: in the cubicle landscape, on the far row of screens.</summary>
        public static readonly SimulationId MoveSocket = new SimulationId(3275UL);

        /// <summary>The cubicle landscape's other socket, at the foot of the west aisle where the long way round comes in: taken out.</summary>
        public static readonly SimulationId LongWaySocket = new SimulationId(3274UL);

        /// <summary>The office scenario with this level's changes written over it.</summary>
        public static ScenarioData Apply(ScenarioData template)
        {
            ScenarioData data = template.Clone();
            data.ScenarioId = ScenarioId;

            // The way out: the same door in the same wall, shut and unlocked,
            // and nobody locks it behind them.
            for (int i = 0; i < data.Doors.Length; i++)
            {
                DoorDefinition door = data.Doors[i];
                if (door.NeedsKeycard)
                {
                    data.Doors[i] = new DoorDefinition(door.DoorId, door.RoomId, door.Side,
                        door.CentreAlongWallMillimetres, door.WidthMillimetres, startsLocked: false);
                }
            }

            data.Exits.PeopleLockTheWayOut = false;

            // The fire: the Director's script, in the middle of the floor,
            // at half the office's pace, and through doors only. The bells
            // ring by themselves once the smoke is thick; a fire put out is
            // relit once, somewhere else, and then it is over.
            data.Director.StartsARealFire = true;
            data.Director.FireSpots = FireSpots();
            data.Director.FirstIncidentMinimumTicks = 1000;
            data.Director.FirstIncidentMaximumTicks = 2000;
            data.Director.BellsRingAtSquares = 45;
            data.Director.Relights = 1;
            data.Director.RelightAfterTicks = 750;
            data.Director.MoveSockets = new[] { MoveSocket };
            data.Fire.SpreadMinimumTicks = 80;
            data.Fire.SpreadMaximumTicks = 240;
            data.Fire.YoungFireSquares = 0;
            data.Flammables.BurningThingsHeatThroughWalls = false;

            // Each person and the fire throw dice of their own, so one poke
            // changes the person poked rather than the whole round.
            data.World.EachPersonHasTheirOwnDice = true;

            // How the frightened decide about the heat.
            data.Exits.HeatChoicesStick = true;
            data.Exits.HeatNearADoorMillimetres = 3000;
            data.Exits.HeatDetourMillimetres = 12000;
            data.Exits.HeatDetourPercentPerBravery = 15;
            data.Exits.RefugeDeadEndPenaltyMillimetres = 12000;
            data.Leadership.LeadersLeaveTheFireAlone = true;

            // Fewer frozen for good, and one click wakes anybody frozen.
            data.Temperament.FreezeForeverPercent = 6;
            data.Nudge.PokesToWakeTheFrozen = 1;

            // The hand leads: a held door is walked through, and a hand let
            // go of strands nobody.
            data.Influence.FrightenedGoThroughAHeldDoor = true;
            data.Influence.TurnBackPenaltyMillimetres = 15000;
            data.Influence.FrightenedGoOnWhenLetGo = true;

            // Paused here: the keycard, toilet trips, the brave fighting the
            // fire unasked (a hand on a bottle still sends somebody), and the
            // cruel wedging doors shut. The cap, the bin ladder and the purse
            // are switched off by the level asset.
            data.Keycard.Enabled = false;
            data.Day.ToiletEveryTicks = 0;
            data.Extinguishers.FightMinimumBravery = 11;
            data.Leadership.OrderedFightMinimumBravery = 11;
            data.Blockades.BarricadeEvilMinimum = 11;

            // A second pull station, by the archway.
            var alarms = new List<AlarmDefinition>(data.Alarms)
            {
                new AlarmDefinition(ArchwayPullStation, new LogicalPosition(12300, 8700))
            };
            data.Alarms = alarms.ToArray();

            // No cable, no socket in the arm that leads to the way out or on
            // the long way round, and no stacks of boxes for now: the cable
            // took every socket in the building with the fuse box, and each
            // stack was a chance gate across a main route.
            data.PowerLines = System.Array.Empty<PowerLineDefinition>();
            var stacked = new HashSet<ulong>();
            foreach (TrapDefinition trap in data.TrapDefinitions)
            {
                foreach (SimulationId box in trap.BoxIds)
                {
                    stacked.Add(box.Value);
                }
            }

            data.TrapDefinitions = System.Array.Empty<TrapDefinition>();
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects);
            things.RemoveAll(thing => thing.ObjectId == PrototypeBuilding.ExitArmSocket || thing.ObjectId == LongWaySocket ||
                                      stacked.Contains(thing.ObjectId.Value));
            data.PhysicsObjects = things.ToArray();
            return data;
        }
    }
}
