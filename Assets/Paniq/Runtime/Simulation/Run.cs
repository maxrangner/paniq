using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The complete deterministic fire-reaction run. It has no presentation
    /// dependency; RunDriver is the only Unity tick owner. Its one
    /// use of Unity is the physics engine, kept in a physics scene of the
    /// run's own (see <see cref="PhysicsWorld"/>), which is why a finished
    /// run should be disposed. This class builds the systems, runs the
    /// simulation contract's tick schedule in order, and hands out read-only
    /// views. The rules themselves live in the systems it owns.
    /// </summary>
    public sealed class Run : IDisposable
    {
        public const int MillimetresPerMetre = 1000;
        public const int TicksPerSecond = 50;
        public const ulong FireHazardIdValue = 0xF1AE000000000001UL;

        private readonly SimulationContext context;
        private readonly Agent[] agents;
        private readonly FireSystem fire;

        /// <summary>Everything the crowd is afraid of. Today that is the fire alone.</summary>
        private readonly Threats threats;
        private readonly PowerSystem power;
        private readonly DoorSystem doors;
        private readonly PlayerCommandSystem playerCommands;
        private readonly InfluenceSystem influence;
        private readonly DeckSystem deck;
        private readonly RoundSystem round;

        /// <summary>How many people had got out as of the end of last tick, so this tick can pay for the new ones.</summary>
        private int escapedLastTick;

        /// <summary>How many had been killed, so this tick can deal for the new ones.</summary>
        private int lostLastTick;
        private readonly FearSystem fear;
        private readonly PerceptionSystem perception;
        private readonly WayfindingSystem wayfinding;
        private readonly BodySystem body;
        private readonly CollisionSystem collisions;
        private readonly PhysicsObjectSystem objects;
        private readonly Locomotion locomotion;
        private readonly CalmBehaviour calm;
        private readonly PanicBehaviour panic;
        private readonly DoorBehaviour doorBehaviour;
        private readonly BurningBehaviour burning;
        private readonly FlammablesSystem flammables;
        private readonly ItemBehaviour items;
        private readonly HelpBehaviour help;
        private readonly ChairBehaviour chairs;
        private readonly ExtinguisherBehaviour extinguishers;
        private readonly LeaderBehaviour leaders;
        private readonly AlarmSystem alarms;
        private readonly AlarmBehaviour alarmBehaviour;

        /// <summary>The building's day: the cues, who carries them out, and the timetable that calls them.</summary>
        private readonly CueSystem cues;
        private readonly ErrandBehaviour errands;
        private readonly DirectorSystem director;
        private readonly WorldGeometry geometry;
        private readonly Crowd crowd;
        private readonly PhysicsWorld physics;
        private readonly PeopleBodies people;

        /// <summary>Every system by name, for binding the ones built before what they need.</summary>
        private readonly Systems systems;

        /// <summary>How many doorways the physics' walls were last built with, so a new blast hole rebuilds them.</summary>
        private int wallsBuiltForDoorways;

        /// <summary>Whether each door slot's plug is in, as the physics last had it.</summary>
        private readonly bool[] doorPlugged;

        /// <summary>Which tables the engine was moving last tick, so a table coming to rest can be noticed.</summary>
        private readonly bool[] tableWasMoving;

        public Run(ScenarioData scenarioData, ulong? seedOverride = null)
        {
            // A private copy: whoever handed us the data can change it later
            // without reaching into this run.
            ScenarioData scenario = (scenarioData ?? throw new ArgumentNullException(nameof(scenarioData))).Clone();
            scenario.Validate();
            context = new SimulationContext(scenario, seedOverride ?? scenario.DefaultSeed);

            // Random draws at start-up, in this order: the fire's ignition
            // point, each person's traits (if not authored), pace, turn rates
            // and first decision (ascending ID), then the temperament deck.
            // Nothing else draws before the first tick.
            DoorRuntime[] doorStates = DoorSystem.CreateDoors(scenario);
            geometry = new WorldGeometry(context, doorStates);
            fire = new FireSystem(context, geometry);
            threats = new Threats(fire);
            agents = CreateAgents(doorStates.Length, geometry);
            fear = new FearSystem(context, threats);
            fear.DealTemperaments(agents);

            crowd = new Crowd(agents, scenario.World.OccupancyRadiusMillimetres, geometry.FireArea);
            physics = new PhysicsWorld(scenario.PhysicsFeel, geometry.FireArea, scenario.ObjectPhysics.WallRestitutionPercent);
            doorPlugged = new bool[geometry.DoorSlotCount];
            tableWasMoving = new bool[geometry.TableCount];
            try
            {
                BuildTheBuildingInThePhysics();
                doors = new DoorSystem(context, doorStates, geometry);
                playerCommands = new PlayerCommandSystem(context);
                influence = new InfluenceSystem(context);
                deck = new DeckSystem(context);
                round = new RoundSystem(context, agents, geometry, threats);
                var sound = new SoundSystem(context, crowd, threats, fear, geometry);
                perception = new PerceptionSystem(context, threats, fear, sound);
                body = new BodySystem(context, threats, sound, fear);
                objects = new PhysicsObjectSystem(context, crowd, geometry, body, fear, sound, fire, physics);
                people = new PeopleBodies(context, crowd, physics, threats, objects.Count);
                power = new PowerSystem(context, objects);
                collisions = new CollisionSystem(context, crowd, body, fear, sound, people);

                // The bodies enter the physics world here, loose things first
                // and then the people, and the seating and possessions that
                // follow draw no random numbers: see each method's note.
                GiveOutStartingPossessions();
                people.AddEveryone();
                SeatPeopleWhoStartSeated();
                RememberHomes();
                locomotion = new Locomotion(context, crowd, geometry, objects);
                flammables = new FlammablesSystem(context, crowd, geometry, fire, objects, body);
                items = new ItemBehaviour(context, geometry, objects, flammables);
                chairs = new ChairBehaviour(context, crowd, geometry, objects, people);
                cues = new CueSystem(context, crowd, geometry);
                errands = new ErrandBehaviour(context, crowd, geometry, objects, doors, chairs, sound, cues);
                calm = new CalmBehaviour(context, crowd, geometry, locomotion, items, chairs, errands, cues);
                director = new DirectorSystem(context, cues, geometry);
                var exitSigns = new ExitSignBehaviour(context, geometry);
                wayfinding = new WayfindingSystem(context, geometry, exitSigns);
                doorBehaviour = new DoorBehaviour(context, crowd, geometry, doors, threats, sound, exitSigns, wayfinding);
                help = new HelpBehaviour(context, crowd, geometry, threats, fear, body, objects, locomotion, people);
                panic = new PanicBehaviour(context, crowd, geometry, threats, fear, sound, body, doorBehaviour, help, chairs,
                    exitSigns, locomotion);
                burning = new BurningBehaviour(context, crowd, body, sound, locomotion);
                extinguishers = new ExtinguisherBehaviour(context, crowd, geometry, objects, fire, body, flammables, items);
                leaders = new LeaderBehaviour(context, crowd, geometry, doors, doorBehaviour, fire, sound, objects, locomotion,
                    wayfinding);
                alarms = new AlarmSystem(context, sound, geometry);
                alarmBehaviour = new AlarmBehaviour(context, geometry, alarms, locomotion);
                var barricades = new BarricadeBehaviour(context, crowd, geometry, doors, threats, objects, flammables, locomotion);

                // Everything exists: hand each system the ones built after it.
                // Binding stores references only, so it cannot move a run.
                systems = new Systems
                {
                    Context = context, Geometry = geometry, Crowd = crowd, Physics = physics, Fire = fire,
                    Threats = threats, Power = power, Doors = doors, PlayerCommands = playerCommands,
                    Influence = influence, Deck = deck, Round = round, Sound = sound, Fear = fear,
                    Perception = perception, Body = body, Objects = objects, People = people,
                    Collisions = collisions, Locomotion = locomotion, Flammables = flammables, Items = items,
                    Chairs = chairs, Calm = calm, ExitSigns = exitSigns, Wayfinding = wayfinding,
                    DoorBehaviour = doorBehaviour, Help = help, Panic = panic, Burning = burning,
                    Extinguishers = extinguishers, Leaders = leaders, Alarms = alarms,
                    AlarmBehaviour = alarmBehaviour, Barricades = barricades,
                    Cues = cues, Errands = errands, Director = director
                };
                systems.BindAll();
            }
            catch
            {
                physics.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Lets go of the run's physics scene. The run cannot be stepped after
        /// this. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            physics.Dispose();
        }

        /// <summary>
        /// The walls, the doorway plugs and the tables, in the physics world.
        /// Tables go in in index order so each one's number there is its number
        /// here.
        /// </summary>
        private void BuildTheBuildingInThePhysics()
        {
            physics.SetWalls(geometry.SolidWalls());
            wallsBuiltForDoorways = geometry.DoorCount;
            for (int door = 0; door < doorPlugged.Length; door++)
            {
                geometry.DescribeDoorway(door, out LogicalPosition centre, out bool alongX, out int width);
                doorPlugged[door] = geometry.IsDoorwayPlugged(door);
                physics.AddDoor(centre, alongX, width, doorPlugged[door]);
            }

            FlammableSettings flammables = context.Scenario.Flammables;
            for (int table = 0; table < geometry.TableCount; table++)
            {
                LogicalBounds bounds = geometry.TableBounds(table);
                long squareMillimetres = (long)(bounds.MaxX - bounds.MinX) * (bounds.MaxZ - bounds.MinZ);
                int massGrams = (int)Math.Max(1000L,
                    squareMillimetres * flammables.TableMassGramsPerSquareMetre / 1000000L);
                physics.AddTable(bounds, massGrams, flammables.TableFloorGripPercent);
                geometry.MoveTable(table, physics.TableFootprint(table), physics.TablePose(table), true);
            }
        }

        /// <summary>
        /// After the engine steps: a table the engine moved covers different
        /// floor now. Only tables it actually moved are read, so a room full of
        /// tables standing still costs nothing. The walkable floor is worked out
        /// again when a table comes to rest, not while it is still sliding:
        /// people walk round where it was for the moment it takes to settle,
        /// which nobody can see, and a crowd shoving a desk about does not cost
        /// a new floor plan every tick.
        /// </summary>
        private void FollowTheTables()
        {
            for (int table = 0; table < geometry.TableCount; table++)
            {
                bool awake = physics.IsTableAwake(table);
                if (!awake && !tableWasMoving[table])
                {
                    continue;
                }

                geometry.MoveTable(table, physics.TableFootprint(table), physics.TablePose(table), !awake);
                tableWasMoving[table] = awake;
            }
        }

        /// <summary>
        /// Before the engine steps: every doorway that has opened or shut
        /// since, and a new wall shape if a blast has opened a hole.
        /// </summary>
        private void BringThePhysicsUpToDate()
        {
            if (geometry.DoorCount != wallsBuiltForDoorways)
            {
                physics.SetWalls(geometry.SolidWalls());
                wallsBuiltForDoorways = geometry.DoorCount;
            }

            for (int door = 0; door < doorPlugged.Length; door++)
            {
                bool plugged = geometry.IsDoorwayPlugged(door);
                if (plugged != doorPlugged[door])
                {
                    doorPlugged[door] = plugged;
                    physics.SetDoorShut(door, plugged);
                }
            }
        }

        /// <summary>
        /// Puts the things people walk in holding into their arms. Done once the
        /// objects exist, and it draws no random numbers, so the start-up draw
        /// order above is untouched.
        /// </summary>
        private void GiveOutStartingPossessions()
        {
            AgentDefinition[] definitions = context.Scenario.Agents;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                SimulationId carried = default;
                for (int d = 0; d < definitions.Length; d++)
                {
                    if (definitions[d].AgentId == agent.Id)
                    {
                        carried = definitions[d].CarriedObjectId;
                        break;
                    }
                }

                if (carried.Value == 0UL)
                {
                    continue;
                }

                int item = objects.IndexOf(carried);
                agent.Carry.ItemIndex = item;
                agent.Carry.Holding = true;
                agent.Carry.OwnsIt = true;
                objects.PickUp(item, agent);
            }
        }

        /// <summary>
        /// Sits down everyone the scenario says begins the run in a chair: a
        /// meeting already under way when the fire starts. Like
        /// <see cref="GiveOutStartingPossessions"/> this runs once the objects
        /// exist and draws no random numbers, so the start-up draw order is
        /// untouched. They stay seated until something gets them up -- the
        /// level's timetable ending the meeting, or the fire -- rather than
        /// for a while of their own.
        /// </summary>
        private void SeatPeopleWhoStartSeated()
        {
            AgentDefinition[] definitions = context.Scenario.Agents;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                SimulationId chairId = default;
                for (int d = 0; d < definitions.Length; d++)
                {
                    if (definitions[d].AgentId == agent.Id)
                    {
                        chairId = definitions[d].SeatedOnObjectId;
                        break;
                    }
                }

                if (chairId.Value == 0UL)
                {
                    continue;
                }

                int chair = objects.IndexOf(chairId);
                if (chair < 0)
                {
                    throw new InvalidOperationException(
                        $"Agent {agent.Id} starts seated on {chairId}, which is not in the scenario.");
                }

                if (objects.OccupantOf(chair) >= 0)
                {
                    throw new InvalidOperationException(
                        $"Agent {agent.Id} starts seated on {chairId}, which somebody else already starts on.");
                }

                agent.Body.Heading = objects.HeadingOf(chair);
                agent.Body.Speed = 0;
                objects.SitOn(chair, agent);
                people.SitIn(agent, chair, objects.PositionOf(chair), agent.Body.Heading);
                agent.Sitting.ChairIndex = chair;
                agent.Sitting.OnIt = true;
                agent.Sitting.SeatedPercent = 100;
                agent.Intent.Activity = AgentActivityState.Sitting;
                agent.Intent.LookHeading = agent.Body.Heading;

                // Until told: the meeting breaks up when the timetable says
                // (CueSystem.EndMeeting), the host first and the rest one at
                // a time, never on one tick.
                agent.Sitting.SitUntilTold = true;
                agent.Intent.ActivityEndTick = int.MaxValue;
                agent.Sitting.SitUntilTick = int.MaxValue;
            }
        }

        /// <summary>
        /// Tells everybody where they belong: the chair that is theirs, or a
        /// spot. Draws nothing. Somebody with neither loiters wherever the day
        /// leaves them, exactly as everybody did before anybody had a desk.
        /// </summary>
        private void RememberHomes()
        {
            AgentDefinition[] definitions = context.Scenario.Agents;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                for (int d = 0; d < definitions.Length; d++)
                {
                    if (definitions[d].AgentId != agent.Id)
                    {
                        continue;
                    }

                    AgentDefinition definition = definitions[d];
                    if (definition.HasHomeChair)
                    {
                        agent.Home.Chair = objects.IndexOf(definition.HomeObjectId);
                    }
                    else if (definition.HasHomeSpot)
                    {
                        agent.Home.HasSpot = true;
                        agent.Home.Spot = definition.HomeSpot;
                    }

                    break;
                }
            }
        }

        private Agent[] CreateAgents(int doorCount, WorldGeometry building)
        {
            ScenarioData scenario = context.Scenario;
            var definitions = (AgentDefinition[])scenario.Agents.Clone();
            Array.Sort(definitions, (left, right) => left.AgentId.CompareTo(right.AgentId));
            var created = new Agent[definitions.Length];
            for (int i = 0; i < created.Length; i++)
            {
                AgentDefinition definition = definitions[i];
                int heading = IntegerMath.CardinalToDegrees(definition.InitialFacingDirection);
                var agent = new Agent(i, definition.AgentId, doorCount, building.RoomCount)
                {
                    Participation = AgentParticipation.Participating,
                    Outcome = AgentTerminalOutcome.Unresolved
                };
                if (definition.Familiarity == AgentFamiliarity.Visitor)
                {
                    // Draws nothing, so the start-up order of random numbers
                    // above and below is untouched.
                    int startRoom = building.RoomStoodIn(definition.InitialPosition);
                    agent.Knowledge.StartAsVisitor(startRoom, building.RoomDoors(startRoom));
                }

                // Before the crowd exists, so there is no index to tell yet;
                // the crowd indexes everybody as it is built.
                agent.Body.MoveWithoutTellingTheCrowd(definition.InitialPosition);
                agent.Body.Heading = heading;
                agent.Fear.State = AgentFearState.Calm;
                agent.Fear.AlertSource = AgentAlertSource.None;
                agent.Intent.Activity = AgentActivityState.Standing;
                agent.Intent.LookHeading = heading;
                agent.Traits = definition.HasAuthoredTraits ? definition.Traits : TraitEffects.Draw(ref context.Random);
                TraitEffects.ApplyPace(agent, scenario, ref context.Random);
                agent.Personality.CalmTurnRate = context.Random.NextIntInclusive(scenario.Calm.TurnRateMinimum, scenario.Calm.TurnRateMaximum);
                agent.Personality.PanicTurnRate = context.Random.NextIntInclusive(scenario.Panic.TurnRateMinimum, scenario.Panic.TurnRateMaximum);

                // Stagger first decisions so the room does not start in lockstep.
                agent.Intent.ActivityEndTick = context.Random.NextIntInclusive(1, scenario.Calm.DecisionMaximumTicks);
                created[i] = agent;
            }

            return created;
        }

        public int Tick => context.Tick;
        public int AgentCount => agents.Length;
        public CausalEventLog EventLog => context.Events;
        public Pcg32 Random => context.Random;
        public ScenarioData Scenario => context.Scenario;

        /// <summary>
        /// How long the physics engine's own step has taken over this whole
        /// run, measured on the clock, for profiling. It never feeds back into
        /// the run.
        /// </summary>
        public TimeSpan PhysicsStepTime =>
            TimeSpan.FromSeconds(physicsStepTimestampTicks / (double)System.Diagnostics.Stopwatch.Frequency);

        private long physicsStepTimestampTicks;

        public bool FireActive => fire.Active;
        public int FireCellCount => fire.BurningCount;
        public int FireGridColumns => fire.GridColumns;
        public int FireGridRows => fire.GridRows;

        /// <summary>Grid cells that are floor in some room; the others never burn.</summary>
        public int FireFloorCellCount => fire.FloorCellCount;
        public LogicalPosition FireOrigin => fire.Origin;
        public ulong FireActivationEventId => fire.ActivationEventId;

        public int DoorCount => doors.Count;

        /// <summary>The fire alarms, for the display.</summary>
        public int AlarmCount => alarms.Count;

        public SimulationId AlarmId(int index) => alarms.IdOf(index);

        public LogicalPosition AlarmPosition(int index) => alarms.PositionOf(index);

        /// <summary>Whether the alarms are ringing.</summary>
        public bool AlarmsRinging => alarms.Ringing;

        /// <summary>What the player has left to spend on cards.</summary>
        public int Influence => influence.Influence;

        /// <summary>Influence earned back by getting people out, and spent on cards, for the display.</summary>
        public int InfluenceEarned => influence.Earned;
        public int InfluenceSpent => influence.Spent;

        /// <summary>How many sticks of TNT the player has left.</summary>
        public int BlastChargesRemaining => doors.BlastChargesRemaining;

        /// <summary>What a card costs, so the display can grey out what the player cannot afford.</summary>
        public int CostOf(PlayerCommandType card) => influence.CostOf(card);

        /// <summary>Every command queued so far, in sequence order. Replaying them gives the same run.</summary>
        public IReadOnlyList<PlayerCommand> Commands => playerCommands.Commands;

        public int PhysicsObjectCount => objects.Count;

        public AgentSnapshot GetAgent(int index) => agents[index].ToSnapshot();

        /// <summary>
        /// Whether this person is on their way to the given way out: it is the
        /// one at the end of the route they picked, and they have a door to head
        /// through next. Being stuck in a queue still counts — this asks what
        /// they are trying to do, not whether they are managing it.
        /// </summary>
        public bool IsHeadingForWayOut(SimulationId id, int wayOutDoorIndex)
        {
            int i = crowd.IndexOf(id);
            return i >= 0 && agents[i].Doors.WayOutDoorIndex == wayOutDoorIndex && agents[i].Doors.ExitDoorIndex >= 0;
        }

        public AgentSnapshot GetAgent(SimulationId id)
        {
            int i = crowd.IndexOf(id);
            if (i < 0)
            {
                throw new KeyNotFoundException($"Unknown agent ID {id}.");
            }

            return agents[i].ToSnapshot();
        }

        public DoorSnapshot GetDoor(int index) => doors.GetSnapshot(index);

        /// <summary>Tests only: how much spray is left in an extinguisher.</summary>
        internal int ExtinguisherFuel(int index) => objects.FuelOf(index);

        public PhysicsObjectSnapshot GetPhysicsObject(int index) =>
            objects.GetSnapshot(index).WithBurn(flammables.ObjectState(index), flammables.ObjectHeatPercent(index));

        public int TableCount => context.Scenario.Tables.Length;

        public TableSnapshot GetTable(int index) => flammables.GetTableSnapshots()[index];

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        internal void LaunchObjectForTests(int index, int velocityX, int velocityZ) => objects.Launch(index, velocityX, velocityZ);

        /// <summary>Throws a thing straight up at this many millimetres per tick.</summary>
        internal void TossObjectUpForTests(int index, int velocityY) => objects.Launch(index, 0, 0, velocityY);

        /// <summary>Tests only: everything about what one person is doing and why, in one line, for a test that has to say what went wrong.</summary>
        internal string DescribeForTests(int index)
        {
            Agent a = agents[index];
            string searchSpot = a.Knowledge.HasSearchSpot ? a.Knowledge.SearchSpot.ToString() : "-";
            return $"person {a.Id.Value}: fear={a.Fear.State} activity={a.Intent.Activity} body={a.Body.State} " +
                   $"at={a.Body.Position} heading={a.Body.Heading} speed={a.Body.Speed} blocked={a.Body.BlockedTicks} " +
                   $"target={a.Intent.Target} activityEnd={a.Intent.ActivityEndTick} nextDecision={a.Intent.NextPanicDecisionTick} " +
                   $"exitDoor={a.Doors.ExitDoorIndex} wayOut={a.Doors.WayOutDoorIndex} room={a.Doors.CurrentRoom} " +
                   $"knowsAll={a.Knowledge.KnowsEverything} searching={a.Knowledge.Searching} searchSpot={searchSpot} " +
                   $"freezeEnd={a.Fear.FreezeEndTick} reactionEnd={a.Fear.ReactionEndTick} temperament={a.Personality.Temperament} " +
                   $"errand={(a.Errand.Has ? a.Errand.Cue.ToString() : "none")}/step{a.Errand.Step}/{a.Errand.Phase} errandDoor={a.Errand.Door} errandRoom={a.Errand.Room} " +
                   $"errandPlace={a.Errand.Place} errandUntil={a.Errand.UntilTick} errandEnded={a.Errand.EndedBecause} sitting={a.Sitting.OnIt}/{a.Sitting.ChairIndex}";
        }

        /// <summary>Tests only: this person heaves this table the way they are facing, as a panicking person stuck behind it would.</summary>
        internal void HeaveTableForTests(int agentIndex, int table) =>
            objects.HeaveTable(agents[agentIndex], table, agents[agentIndex].Body.Heading, 0UL);

        /// <summary>Tests only: whether a straight walk between two points runs into a table that is still standing.</summary>
        internal bool RouteCrossesTableForTests(LogicalPosition from, LogicalPosition to) => geometry.RouteCrossesTable(from, to);

        /// <summary>Tests only: the fire system, to check its queries against a brute-force answer.</summary>
        /// <summary>Tops up the purse for a test that is not about the economy.</summary>
        public void GiveInfluenceForTests(int amount) => influence.GiveForTests(amount);

        internal FireSystem FireForTests => fire;

        internal WorldGeometry GeometryForTests => geometry;

        /// <summary>
        /// The middle of the square the fire was drawn to start in, whether or
        /// not it has been lit yet. For a test that wants to know where the
        /// danger would begin without having to set it going.
        /// </summary>
        internal LogicalPosition FireOriginForTests => fire.Origin;

        /// <summary>The cable and the sparks on it, for a test to watch one travel.</summary>
        internal PowerSystem PowerForTests => power;

        /// <summary>The cues, for a test that calls one by hand rather than waiting for the timetable or the dice.</summary>
        internal CueSystem CuesForTests => cues;

        /// <summary>Tests only: what one person's errand is and how far along it is.</summary>
        internal AgentErrand ErrandForTests(int index) => agents[index].Errand;

        /// <summary>Tests only: a thud at a spot, so calm people near it turn to look.</summary>
        internal void MakeANoiseForTests(LogicalPosition where) => systems.Sound.Thud(default, where, 0UL);

        /// <summary>The run's shared state, for a test double that needs to write into the log.</summary>
        internal SimulationContext ContextForTests => context;

        /// <summary>Puts something other than fire into the world for the crowd to be afraid of. Before the first tick only.</summary>
        internal void AddThreatForTests(IThreat threat)
        {
            if (context.Tick != 0)
            {
                throw new InvalidOperationException("A test threat has to be in the world before the first tick.");
            }

            threats.AddForTests(threat);
        }

        /// <summary>Tests only: what touched what in the last physics step.</summary>
        internal IReadOnlyList<PhysicsWorld.Contact> ContactsForTests => physics.Contacts;

        /// <summary>How far any two solid things were pressed into each other during the last tick, in millimetres.</summary>
        public int DeepestPressMillimetres => physics.DeepestPressMillimetres;

        /// <summary>Tests only: whether the deepest press of the last tick was a thing against the floor, rather than against a table, a wall, a door or another thing.</summary>
        internal bool DeepestPressIsIntoTheFloorForTests
        {
            get
            {
                (_, int b, PhysicsWorld.StaticKind building) = physics.DeepestPressPair;
                return b < 0 && building == PhysicsWorld.StaticKind.Floor;
            }
        }

        /// <summary>
        /// Tests only: the two things pressed deepest during the last tick, as
        /// words: a person, a thing, or part of the building.
        /// </summary>
        internal string DeepestPressForTests
        {
            get
            {
                (int a, int b, PhysicsWorld.StaticKind building) = physics.DeepestPressPair;
                return Describe(a) + " and " + (b < 0 ? building.ToString() : Describe(b));
            }
        }

        private string Describe(int handle)
        {
            Agent person = people.PersonAt(handle);
            if (person != null)
            {
                return $"person {person.Id} ({person.Body.State}, {person.Intent.Activity}, sitting {person.Sitting.OnIt})";
            }

            PhysicsObjectSnapshot thing = GetPhysicsObject(handle);
            return $"{thing.Kind} {thing.ObjectId} (held {thing.IsHeld}, sat on {thing.IsSatOn}, height {thing.Pose.HeightMillimetres})";
        }

        /// <summary>Tests only: a person's handle in the physics world, to find them among the contacts.</summary>
        internal int PhysicsHandleForTests(int agentIndex) => people.HandleOf(agents[agentIndex]);

        /// <summary>Tests only: how hard everything pressed on this person during the last step.</summary>
        internal long SqueezeForTests(int agentIndex) => people.SqueezeOn(agents[agentIndex]);

        /// <summary>How long this person has wanted to move and could not.</summary>
        internal int BlockedTicksForTests(int index) => agents[index].Body.BlockedTicks;

        /// <summary>Where this person is currently trying to get to.</summary>
        internal LogicalPosition TargetForTests(int index) => agents[index].Intent.Target;

        /// <summary>The way out this person is running for, or -1.</summary>
        internal int ExitDoorForTests(int index) => agents[index].Doors.ExitDoorIndex;
        internal Agent AgentForTests(int index) => agents[index];

        /// <summary>Frightens somebody at once, as if they had seen the fire: for tests of what the frightened do.</summary>
        internal void FrightenForTests(int index) => fear.MakeScared(agents[index]);


        /// <summary>
        /// The floor drawn as squares, for the debugging overlay. The building
        /// does not change shape while a run is being watched, so one reading
        /// at the start is enough.
        /// </summary>
        public NavigationGridReading ReadNavigationGrid() => geometry.Navigation.Reading();

        /// <summary>
        /// Whether the indexes of who and what is standing where still agree
        /// with the actual positions. False means something moved without
        /// saying so, which would quietly wrong every "what is near here"
        /// answer from that moment on.
        /// </summary>
        internal bool SpatialIndexesAreConsistentForTests =>
            crowd.IndexMatchesPositions() && objects.IndexMatchesPositions();

        /// <summary>
        /// Queues a player action for a tick that has not started yet. Commands
        /// for the same tick run in the order they were queued.
        /// </summary>
        public PlayerCommand QueueCommand(PlayerCommandType commandType, SimulationId targetId, int targetTick)
        {
            return playerCommands.Queue(commandType, targetId, default, targetTick);
        }

        /// <summary>
        /// Queues a player action aimed at a place rather than at a thing, in
        /// whole millimetres. Same rules as the overload above.
        /// </summary>
        public PlayerCommand QueueCommand(PlayerCommandType commandType, LogicalPosition point, int targetTick)
        {
            return playerCommands.Queue(commandType, default, point, targetTick);
        }

        /// <summary>
        /// One logical tick, in the simulation contract's order:
        /// 1 player commands, 1½ the building's day (the Director's cues),
        /// 2 hazard, 3 hazard contact, 4 decisions
        /// (ascending ID), 5 movement, 6 danger contact along accepted moves,
        /// flames jumping between people, and exits, 7 collisions (people,
        /// then objects), 8 the physics engine's step and every hit it reports,
        /// 9 things heating, catching, burning out and passing flames on.
        /// </summary>
        public void Step()
        {
            context.Tick = checked(context.Tick + 1);
            playerCommands.Consume();

            // Phase 1½: what the building's day holds. A cue called here
            // reaches people at their own reaction tick in phase 4, so nobody
            // moves on the tick it is called.
            director.Advance();
            threats.Advance();

            // Phase 2 as well: a fuse burning along a wall toward a socket is
            // hazard advancing on its own clock, exactly as a threat is. It
            // goes after the threats so the fire's draws stay where they were.
            power.Advance();
            ResolveCurrentContact();

            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                if (body.BurnOut(agent))
                {
                    items.DropFromLost(agent);
                    continue;
                }

                perception.Update(agent);

                // Whatever doors and corners they can see, before they decide
                // anything: somebody who has just come round a corner and seen
                // the way out runs for it this tick, not next time they think.
                wayfinding.Look(agent);

                // Startled, off their feet or on fire: whatever they carry is dropped or thrown.
                items.LetGoIfNeeded(agent);
                if (body.Update(agent))
                {
                    // Staggering, on the floor or getting up: no control, no move.
                    continue;
                }

                MotorIntent? intent;
                if (agent.Burning.IsBurning)
                {
                    intent = burning.Decide(agent);
                }
                else if (agent.Fear.State == AgentFearState.Calm)
                {
                    intent = calm.Decide(agent);
                }
                else if (agent.Fear.State == AgentFearState.Alert)
                {
                    intent = fear.AlertIntent(agent);
                }
                else
                {
                    intent = panic.Decide(agent);
                }

                if (intent.HasValue)
                {
                    Locomotion.ApplyBody(agent, items.Burdened(agent, intent.Value));
                }

                if (agent.Body.State != AgentBodyState.Upright)
                {
                    // Tripped during this decision.
                    agent.Body.Speed = 0;
                }
            }

            chairs.ResolveStanding();
            leaders.CountFollowers();
            extinguishers.Spray();
            help.PullDragged(agents);

            // The engine's step: everybody's feet push, everything moves,
            // bounces and topples, then what happened is read back and judged.
            BringThePhysicsUpToDate();
            people.Drive();
            long stepStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            physics.Step();
            physicsStepTimestampTicks += System.Diagnostics.Stopwatch.GetTimestamp() - stepStarted;
            people.ReadBack();
            FollowTheTables();
            objects.AfterStep();
            collisions.Resolve(physics.Contacts);
            people.FeelTheSqueeze(physics.Contacts);

            items.FollowCarriers(agents);
            burning.RollToPutItOut();
            burning.SpreadFlames();
            doorBehaviour.ResolveRoomChangesAndEscapes();
            help.ResolveRescues(agents);
            flammables.Update();
            doors.ScorchInTheFire(fire);
            doors.ResolveBlockages();

            // Before the list of doors that opened is cleared: fire that had
            // nowhere left to go may have somewhere now.
            WakeFireBesideDoorsThatOpened();

            // Last of all, once the tick has settled: anybody who could have
            // seen or heard a door open this tick thinks again on the next
            // one. Here for the same reason the blockages are worked out here
            // -- the next tick's decisions read one settled answer instead of
            // one that changes as the door swings.
            doorBehaviour.AnnounceWaysOut();
            SettleThePurseAndTheHand();

            // Very last, once everything about this tick has settled: is the
            // round over? Judged on the tick as it ended rather than as it was
            // half way through.
            round.Update();
        }

        /// <summary>Where the round has got to: before the event, during it, or finished.</summary>
        public RoundPhase Phase => round.Phase;

        /// <summary>
        /// What this tick paid the player. Everybody who got out pays the purse
        /// back, everything that happened feeds the meter, and everybody who
        /// was killed deals a card. Counted here rather than reported by the
        /// behaviours, so nothing in the simulation has to know the player's
        /// purse exists.
        /// <para>
        /// The dead are dealt for in agent order, not in the order they
        /// happened to be resolved, so a replay of the same seed draws the same
        /// cards.
        /// </para>
        /// </summary>
        private void SettleThePurseAndTheHand()
        {
            int escaped = 0;
            int lost = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                AgentTerminalOutcome outcome = agents[i].Outcome;
                escaped += outcome == AgentTerminalOutcome.Escaped ? 1 : 0;
                lost += outcome == AgentTerminalOutcome.Lost ? 1 : 0;
            }

            // Only while the round is running: somebody who strolled out at
            // home time before anything was wrong was never in danger, and
            // the purse pays for people saved, not for people who left.
            if (round.Phase == RoundPhase.Running)
            {
                for (int saved = escapedLastTick; saved < escaped; saved++)
                {
                    influence.CreditPersonSaved();
                }
            }

            escapedLastTick = escaped;

            if (lost > lostLastTick)
            {
                DealForTheNewlyDead();
                lostLastTick = lost;
            }

            influence.CreditUproar();
        }

        /// <summary>
        /// One card for each person killed since last tick, found by walking
        /// the crowd in order and dealing for anybody dead who has not been
        /// dealt for yet. In crowd order rather than in the order they happened
        /// to be resolved, so a replay of the same seed draws the same cards.
        /// </summary>
        private void DealForTheNewlyDead()
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Outcome != AgentTerminalOutcome.Lost || agents[i].DeathDealt)
                {
                    continue;
                }

                agents[i].DeathDealt = true;
                deck.DealForDeath(
                    agents[i].Id,
                    agents[i].Body.Position,
                    agents[i].DeathEventId,
                    objects.HasSpareExtinguisher,
                    doors.BlastChargesRemaining > 0);
            }
        }

        /// <summary>Phase 3: anyone standing in a threat is got by it (in fire, they catch fire).</summary>
        private void ResolveCurrentContact()
        {
            if (!threats.AnyActive)
            {
                return;
            }

            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (agent.IsParticipating)
                {
                    threats.ResolveContact(agent, body);
                }
            }
        }

        /// <summary>
        /// Every door that became a way through this tick, told to the fire on
        /// both sides of it. Ascending door order, so a replay agrees.
        /// </summary>
        private void WakeFireBesideDoorsThatOpened()
        {
            for (int i = 0; i < doors.OpeningsThisTick; i++)
            {
                int door = doors.OpeningAt(i);
                int room = geometry.DoorRoom(door);
                fire.WakeRoom(room);
                fire.WakeRoom(geometry.RoomBeyond(door, room));
            }
        }

        /// <summary>People still in the run who are in a room with nothing burning in it.</summary>
        private int CountClearOfFire()
        {
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || agent.Burning.IsBurning)
                {
                    continue;
                }

                int room = geometry.RoomAt(agent.Body.Position);
                count += room >= 0 && !threats.IsInRoom(room) ? 1 : 0;
            }

            return count;
        }

        /// <summary>
        /// A fresh copy of the run as it stands, for anybody who wants to keep
        /// it (tests, tools). The display does not: it keeps two buffers from
        /// <see cref="NewSnapshotBuffer"/> and fills them turn about with
        /// <see cref="FillSnapshot"/>, so drawing allocates nothing a tick.
        /// </summary>
        public RunSnapshot GetSnapshot()
        {
            RunSnapshot snapshot = NewSnapshotBuffer();
            FillSnapshot(snapshot);
            return snapshot;
        }

        /// <summary>An empty snapshot sized for this run, to be filled and filled again.</summary>
        public RunSnapshot NewSnapshotBuffer()
        {
            return new RunSnapshot(agents.Length, geometry.DoorSlotCount, objects.Count, geometry.TableCount,
                cardCosts ??= CardCosts(), doorClickCosts ??= DoorClickCosts());
        }

        /// <summary>Writes the run as it stands into a snapshot from <see cref="NewSnapshotBuffer"/>.</summary>
        public void FillSnapshot(RunSnapshot into)
        {
            AgentSnapshot[] people = into.AgentBuffer;
            for (int i = 0; i < agents.Length; i++)
            {
                people[i] = agents[i].ToSnapshot();
            }

            // Only the openings that are really there: a spare hole slot has no
            // position to draw and nothing to say about it.
            Prefix<DoorSnapshot> openings = into.DoorBuffer;
            openings.Resize(doors.Count);
            for (int i = 0; i < doors.Count; i++)
            {
                openings.Items[i] = doors.GetSnapshot(i);
            }

            PhysicsObjectSnapshot[] things = into.PhysicsObjectBuffer;
            for (int i = 0; i < things.Length; i++)
            {
                things[i] = GetPhysicsObject(i);
            }

            flammables.FillTableSnapshots(into.TableBuffer);

            Prefix<PlayerCommandType> held = into.HandBuffer;
            held.Resize(deck.Hand.Count);
            for (int i = 0; i < deck.Hand.Count; i++)
            {
                held.Items[i] = deck.Hand[i];
            }

            into.Fill(
                context.Tick,
                fire.Active,
                fire.Origin,
                context.Scenario.Fire.CellSizeMillimetres,
                fire.GetCells(),
                context.Events.View(),
                CountClearOfFire(),
                alarms.Ringing,
                influence.Influence,
                context.Scenario.Influence.Maximum,
                influence.Spent,
                influence.Earned,
                doors.BlastChargesRemaining,
                power.Sparks(),
                round.Phase,
                context.Scenario.Round.TargetSavedPercent);
        }

        /// <summary>The cost tables, worked out once: they are settings, and settings do not change in a run.</summary>
        private int[] cardCosts;
        private int[] doorClickCosts;

        /// <summary>What every command costs, by command type, for the display.</summary>
        private int[] CardCosts()
        {
            var costs = new int[System.Enum.GetValues(typeof(PlayerCommandType)).Length];
            for (int i = 0; i < costs.Length; i++)
            {
                costs[i] = influence.CostOf((PlayerCommandType)i);
            }

            return costs;
        }

        /// <summary>What a click costs on a door in each state, for the display.</summary>
        private int[] DoorClickCosts()
        {
            var costs = new int[System.Enum.GetValues(typeof(DoorState)).Length];
            for (int i = 0; i < costs.Length; i++)
            {
                costs[i] = influence.CostOfDoorClick((DoorState)i);
            }

            return costs;
        }
    }
}
