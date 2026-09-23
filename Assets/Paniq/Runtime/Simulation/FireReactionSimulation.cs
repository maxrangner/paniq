using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The complete deterministic fire-reaction run. It has no presentation
    /// dependency; FireReactionRunner is the only Unity tick owner. Its one
    /// use of Unity is the physics engine, kept in a physics scene of the
    /// run's own (see <see cref="PhysicsWorld"/>), which is why a finished
    /// run should be disposed. This class builds the systems, runs the
    /// simulation contract's tick schedule in order, and hands out read-only
    /// views. The rules themselves live in the systems it owns.
    /// </summary>
    public sealed class FireReactionSimulation : IDisposable
    {
        public const int MillimetresPerMetre = 1000;
        public const int TicksPerSecond = 50;
        public const ulong FireHazardIdValue = 0xF1AE000000000001UL;

        private readonly SimulationContext context;
        private readonly Agent[] agents;
        private readonly FireSystem fire;
        private readonly PowerSystem power;
        private readonly DoorSystem doors;
        private readonly PlayerCommandSystem playerCommands;
        private readonly InfluenceSystem influence;
        private readonly RoundSystem round;

        /// <summary>How many people had got out as of the end of last tick, so this tick can pay for the new ones.</summary>
        private int escapedLastTick;
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
        private readonly WorldGeometry geometry;
        private readonly Crowd crowd;
        private readonly PhysicsWorld physics;
        private readonly PeopleBodies people;

        /// <summary>How many doorways the physics' walls were last built with, so a new blast hole rebuilds them.</summary>
        private int wallsBuiltForDoorways;

        /// <summary>Whether each door slot's plug is in, as the physics last had it.</summary>
        private readonly bool[] doorPlugged;

        /// <summary>Which tables the engine was moving last tick, so a table coming to rest can be noticed.</summary>
        private readonly bool[] tableWasMoving;

        public FireReactionSimulation(FireReactionScenarioData scenarioData, ulong? seedOverride = null)
        {
            // A private copy: whoever handed us the data can change it later
            // without reaching into this run.
            FireReactionScenarioData scenario = (scenarioData ?? throw new ArgumentNullException(nameof(scenarioData))).Clone();
            scenario.Validate();
            context = new SimulationContext(scenario, seedOverride ?? scenario.DefaultSeed);

            // Random draws at start-up, in this order: the fire's ignition
            // point, each person's traits (if not authored), pace, turn rates
            // and first decision (ascending ID), then the temperament deck.
            // Nothing else draws before the first tick.
            DoorRuntime[] doorStates = DoorSystem.CreateDoors(scenario);
            geometry = new WorldGeometry(context, doorStates);
            fire = new FireSystem(context, geometry);
            agents = CreateAgents(doorStates.Length, geometry);
            fear = new FearSystem(context, fire);
            fear.DealTemperaments(agents);

            crowd = new Crowd(agents, scenario.World.OccupancyRadiusMillimetres, geometry.FireArea);
            physics = new PhysicsWorld(scenario.PhysicsFeel, geometry.FireArea, scenario.ObjectPhysics.WallRestitutionPercent);
            doorPlugged = new bool[geometry.DoorSlotCount];
            tableWasMoving = new bool[geometry.TableCount];
            try
            {
                BuildTheBuildingInThePhysics();
                doors = new DoorSystem(context, doorStates, geometry);
                doors.UseCrowd(crowd);
                playerCommands = new PlayerCommandSystem(context);
                influence = new InfluenceSystem(context);
                round = new RoundSystem(context, agents, geometry, fire);
                var sound = new SoundSystem(context, crowd, fire, fear, geometry);
                perception = new PerceptionSystem(context, fire, fear, sound);
                body = new BodySystem(context, fire, sound, fear);
                objects = new PhysicsObjectSystem(context, crowd, geometry, body, fear, sound, fire, physics);
                people = new PeopleBodies(context, crowd, physics, fire, objects.Count);
                people.UseBody(body);
                body.UsePeople(people);
                objects.UsePeople(people);
                power = new PowerSystem(context, objects);
                objects.UsePower(power);
                collisions = new CollisionSystem(context, crowd, body, fear, sound, people);
                doors.UseObjects(objects);
                doors.UsePhysics(physics, people);
                GiveOutStartingPossessions();
                people.AddEveryone();
                SeatPeopleWhoStartSeated();
                locomotion = new Locomotion(context, crowd, geometry, objects);
                flammables = new FlammablesSystem(context, crowd, geometry, fire, objects, body);
                items = new ItemBehaviour(context, geometry, objects, flammables);
                chairs = new ChairBehaviour(context, crowd, geometry, objects, people);
                calm = new CalmBehaviour(context, crowd, geometry, locomotion, items, chairs);
                var exitSigns = new ExitSignBehaviour(context, geometry);
                wayfinding = new WayfindingSystem(context, geometry, exitSigns);
                doorBehaviour = new DoorBehaviour(context, crowd, geometry, doors, fire, sound, exitSigns, wayfinding);
                doorBehaviour.UseObjects(objects);
                help = new HelpBehaviour(context, crowd, geometry, fire, fear, body, objects, locomotion, people);
                help.UseDoors(doors);
                panic = new PanicBehaviour(context, crowd, geometry, fire, fear, sound, body, doorBehaviour, help, chairs,
                    exitSigns, locomotion);
                burning = new BurningBehaviour(context, crowd, body, sound, locomotion);
                extinguishers = new ExtinguisherBehaviour(context, crowd, geometry, objects, fire, body, flammables, items);
                leaders = new LeaderBehaviour(context, crowd, geometry, doors, doorBehaviour, fire, sound, objects, locomotion,
                    wayfinding);
                alarms = new AlarmSystem(context, sound, geometry);
                alarmBehaviour = new AlarmBehaviour(context, geometry, alarms, locomotion);
                var barricades = new BarricadeBehaviour(context, crowd, geometry, doors, fire, objects, flammables, locomotion);

                // What a frightened person might do instead of running, in the
                // order they consider it. The first that answers wins, so this list
                // is the priority order, and it is the only place it is written
                // down. Raising the alarm comes after helping so that somebody with
                // an unconscious person in front of them sees to them rather than
                // walking off to the bell; plenty of other people are free to hit it.
                panic.Offer(leaders, extinguishers, help, alarmBehaviour, barricades);
                round.Use(flammables, doors);
                playerCommands.Use(doors, fire, objects, crowd, influence, sound, body, geometry, round, power);
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
            FireReactionAgentDefinition[] definitions = context.Scenario.Agents;
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
        /// untouched. How long they stay seated is a fixed stretch rather than
        /// a drawn one, long enough that the meeting is still going when the
        /// first shout goes up.
        /// </summary>
        private void SeatPeopleWhoStartSeated()
        {
            FireReactionAgentDefinition[] definitions = context.Scenario.Agents;
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
                agent.Intent.Activity = AgentActivityState.Sitting;
                agent.Intent.LookHeading = agent.Body.Heading;
                agent.Intent.ActivityEndTick = context.Scenario.Items.SeatedAtStartTicks;
                agent.Sitting.SitUntilTick = agent.Intent.ActivityEndTick;
            }
        }

        private Agent[] CreateAgents(int doorCount, WorldGeometry building)
        {
            FireReactionScenarioData scenario = context.Scenario;
            var definitions = (FireReactionAgentDefinition[])scenario.Agents.Clone();
            Array.Sort(definitions, (left, right) => left.AgentId.CompareTo(right.AgentId));
            var created = new Agent[definitions.Length];
            for (int i = 0; i < created.Length; i++)
            {
                FireReactionAgentDefinition definition = definitions[i];
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
        public FireReactionScenarioData Scenario => context.Scenario;

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

        public FireReactionAgentSnapshot GetAgent(int index) => agents[index].ToSnapshot();

        /// <summary>
        /// Whether this person is on their way to the given way out: it is the
        /// one at the end of the route they picked, and they have a door to head
        /// through next. Being stuck in a queue still counts — this asks what
        /// they are trying to do, not whether they are managing it.
        /// </summary>
        public bool IsHeadingForWayOut(SimulationId id, int wayOutDoorIndex)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Id == id)
                {
                    return agents[i].Doors.WayOutDoorIndex == wayOutDoorIndex &&
                           agents[i].Doors.ExitDoorIndex >= 0;
                }
            }

            return false;
        }

        public FireReactionAgentSnapshot GetAgent(SimulationId id)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Id == id)
                {
                    return agents[i].ToSnapshot();
                }
            }

            throw new KeyNotFoundException($"Unknown agent ID {id}.");
        }

        public FireReactionDoorSnapshot GetDoor(int index) => doors.GetSnapshot(index);

        /// <summary>Tests only: how much spray is left in an extinguisher.</summary>
        internal int ExtinguisherFuel(int index) => objects.FuelOf(index);

        public FireReactionPhysicsObjectSnapshot GetPhysicsObject(int index) =>
            objects.GetSnapshot(index).WithBurn(flammables.ObjectState(index), flammables.ObjectHeatPercent(index));

        public int TableCount => context.Scenario.Tables.Length;

        public FireReactionTableSnapshot GetTable(int index) => flammables.GetTableSnapshots()[index];

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        internal void LaunchObjectForTests(int index, int velocityX, int velocityZ) => objects.Launch(index, velocityX, velocityZ);

        /// <summary>Throws a thing straight up at this many millimetres per tick.</summary>
        internal void TossObjectUpForTests(int index, int velocityY) => objects.Launch(index, 0, 0, velocityY);

        /// <summary>Tests only: whether a straight walk between two points runs into a table that is still standing.</summary>
        internal bool RouteCrossesTableForTests(LogicalPosition from, LogicalPosition to) => geometry.RouteCrossesTable(from, to);

        /// <summary>Tests only: the fire system, to check its queries against a brute-force answer.</summary>
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

        /// <summary>Tests only: what touched what in the last physics step.</summary>
        internal IReadOnlyList<PhysicsWorld.Contact> ContactsForTests => physics.Contacts;

        /// <summary>How far any two solid things were pressed into each other during the last tick, in millimetres.</summary>
        public int DeepestPressMillimetres => physics.DeepestPressMillimetres;

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

            FireReactionPhysicsObjectSnapshot thing = GetPhysicsObject(handle);
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
        /// 1 player commands, 2 hazard, 3 hazard contact, 4 decisions
        /// (ascending ID), 5 movement, 6 danger contact along accepted moves,
        /// flames jumping between people, and exits, 7 collisions (people,
        /// then objects), 8 the physics engine's step and every hit it reports,
        /// 9 things heating, catching, burning out and passing flames on.
        /// </summary>
        public void Step()
        {
            context.Tick = checked(context.Tick + 1);
            playerCommands.Consume();
            fire.Advance();

            // Phase 2 as well: a fuse burning along a wall toward a socket is
            // hazard advancing on its own clock, exactly as the fire is. It
            // goes after the fire so the fire's draws stay where they were.
            power.Advance();
            ResolveCurrentFireContact();

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
            CreditInfluenceForPeopleSaved();

            // Very last, once everything about this tick has settled: is the
            // round over? Judged on the tick as it ended rather than as it was
            // half way through.
            round.Update();
        }

        /// <summary>Where the round has got to: before the event, during it, or finished.</summary>
        public RoundPhase Phase => round.Phase;

        /// <summary>
        /// Everybody who got out this tick pays the player back. Counted rather
        /// than reported by the behaviours, so nothing in the simulation has to
        /// know the player's purse exists.
        /// </summary>
        private void CreditInfluenceForPeopleSaved()
        {
            int escaped = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                escaped += agents[i].Outcome == AgentTerminalOutcome.Escaped ? 1 : 0;
            }

            for (int saved = escapedLastTick; saved < escaped; saved++)
            {
                influence.CreditPersonSaved();
            }

            escapedLastTick = escaped;
        }

        /// <summary>Phase 3: anyone standing in fire catches fire.</summary>
        private void ResolveCurrentFireContact()
        {
            if (!fire.Active)
            {
                return;
            }

            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                ulong cellEventId = fire.FindTouching(agent.Body.Position);
                if (cellEventId != 0UL)
                {
                    body.CatchFire(agent, cellEventId);
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
                count += room >= 0 && !fire.IsBurningInRoom(room) ? 1 : 0;
            }

            return count;
        }

        public FireReactionSnapshot GetSnapshot()
        {
            var agentSnapshots = new FireReactionAgentSnapshot[agents.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                agentSnapshots[i] = agents[i].ToSnapshot();
            }

            return new FireReactionSnapshot(
                context.Tick,
                fire.Active,
                fire.Origin,
                context.Scenario.Fire.CellSizeMillimetres,
                fire.GetCells(),
                agentSnapshots,
                doors.GetSnapshots(),
                PhysicsObjectSnapshots(),
                flammables.GetTableSnapshots(),
                context.Events.View(),
                CountClearOfFire(),
                alarms.Ringing,
                influence.Influence,
                context.Scenario.Influence.Maximum,
                influence.Spent,
                influence.Earned,
                CardCosts(),
                DoorClickCosts(),
                doors.BlastChargesRemaining,
                power.Sparks(),
                round.Phase,
                context.Scenario.Round.TargetSavedPercent);
        }

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

        private FireReactionPhysicsObjectSnapshot[] PhysicsObjectSnapshots()
        {
            var snapshots = new FireReactionPhysicsObjectSnapshot[objects.Count];
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i] = GetPhysicsObject(i);
            }

            return snapshots;
        }
    }
}
