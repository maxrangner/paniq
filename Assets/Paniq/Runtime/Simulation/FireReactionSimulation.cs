using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The complete deterministic fire-reaction run. It has no Unity object or
    /// presentation dependency; FireReactionRunner is the only Unity tick
    /// owner. This class builds the systems, runs the simulation contract's
    /// tick schedule in order, and hands out read-only views. The rules
    /// themselves live in the systems it owns.
    /// </summary>
    public sealed class FireReactionSimulation
    {
        public const int MillimetresPerMetre = 1000;
        public const int TicksPerSecond = 50;
        public const ulong FireHazardIdValue = 0xF1AE000000000001UL;

        private readonly SimulationContext context;
        private readonly Agent[] agents;
        private readonly FireSystem fire;
        private readonly DoorSystem doors;
        private readonly PlayerCommandSystem playerCommands;
        private readonly InfluenceSystem influence;

        /// <summary>How many people had got out as of the end of last tick, so this tick can pay for the new ones.</summary>
        private int escapedLastTick;
        private readonly FearSystem fear;
        private readonly PerceptionSystem perception;
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
            agents = CreateAgents(doorStates.Length);
            fear = new FearSystem(context, fire);
            fear.DealTemperaments(agents);

            crowd = new Crowd(agents, scenario.World.OccupancyRadiusMillimetres, geometry.FireArea);
            doors = new DoorSystem(context, doorStates, geometry);
            doors.UseCrowd(crowd);
            playerCommands = new PlayerCommandSystem(context);
            influence = new InfluenceSystem(context);
            var sound = new SoundSystem(context, crowd, fire, fear, geometry);
            perception = new PerceptionSystem(context, fire, fear, sound);
            body = new BodySystem(context, crowd, geometry, fire, sound, fear);
            collisions = new CollisionSystem(context, crowd, body, fear, sound);
            objects = new PhysicsObjectSystem(context, crowd, geometry, body, fear, sound);
            body.UseObjects(objects);
            doors.UseObjects(objects);
            GiveOutStartingPossessions();
            SeatPeopleWhoStartSeated();
            locomotion = new Locomotion(context, crowd, geometry, fire, body, collisions, objects);
            flammables = new FlammablesSystem(context, crowd, geometry, fire, objects, body, sound);
            items = new ItemBehaviour(context, geometry, objects, flammables);
            chairs = new ChairBehaviour(context, crowd, geometry, objects);
            calm = new CalmBehaviour(context, crowd, geometry, locomotion, items, chairs);
            doorBehaviour = new DoorBehaviour(context, crowd, geometry, doors, fire, sound);
            doorBehaviour.UseObjects(objects);
            help = new HelpBehaviour(context, crowd, geometry, fire, fear, body, objects, locomotion);
            help.UseDoors(doors);
            panic = new PanicBehaviour(context, crowd, geometry, fire, fear, sound, body, doorBehaviour, help, chairs, locomotion);
            burning = new BurningBehaviour(context, crowd, body, sound, locomotion);
            extinguishers = new ExtinguisherBehaviour(context, crowd, geometry, objects, fire, body, flammables, items);
            panic.UseExtinguishers(extinguishers);
            leaders = new LeaderBehaviour(context, crowd, geometry, doors, doorBehaviour, fire, sound, objects, locomotion);
            panic.UseLeaders(leaders);
            alarms = new AlarmSystem(context, sound, geometry);
            alarmBehaviour = new AlarmBehaviour(context, geometry, alarms, locomotion);
            panic.UseAlarms(alarmBehaviour);
            var barricades = new BarricadeBehaviour(context, crowd, geometry, doors, fire, objects, flammables, locomotion);
            panic.UseBarricades(barricades);
            playerCommands.Use(doors, fire, objects, crowd, influence, sound, body, geometry);
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

                crowd.MoveTo(agent, objects.PositionOf(chair));
                agent.Body.Heading = objects.HeadingOf(chair);
                agent.Body.Speed = 0;
                objects.SitOn(chair, agent);
                agent.Sitting.ChairIndex = chair;
                agent.Sitting.OnIt = true;
                agent.Intent.Activity = AgentActivityState.Sitting;
                agent.Intent.LookHeading = agent.Body.Heading;
                agent.Intent.ActivityEndTick = context.Scenario.Items.SeatedAtStartTicks;
                agent.Sitting.SitUntilTick = agent.Intent.ActivityEndTick;
            }
        }

        private Agent[] CreateAgents(int doorCount)
        {
            FireReactionScenarioData scenario = context.Scenario;
            var definitions = (FireReactionAgentDefinition[])scenario.Agents.Clone();
            Array.Sort(definitions, (left, right) => left.AgentId.CompareTo(right.AgentId));
            var created = new Agent[definitions.Length];
            for (int i = 0; i < created.Length; i++)
            {
                FireReactionAgentDefinition definition = definitions[i];
                int heading = IntegerMath.CardinalToDegrees(definition.InitialFacingDirection);
                var agent = new Agent(i, definition.AgentId, doorCount)
                {
                    Participation = AgentParticipation.Participating,
                    Outcome = AgentTerminalOutcome.Unresolved
                };
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

        /// <summary>Tests only: whether a straight walk between two points runs into a table that is still standing.</summary>
        internal bool RouteCrossesTableForTests(LogicalPosition from, LogicalPosition to) => geometry.RouteCrossesTable(from, to);

        /// <summary>Tests only: the fire system, to check its queries against a brute-force answer.</summary>
        internal FireSystem FireForTests => fire;

        internal WorldGeometry GeometryForTests => geometry;

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
        /// then objects), 8 physical objects, 9 things heating, catching,
        /// burning out and passing flames on.
        /// </summary>
        public void Step()
        {
            context.Tick = checked(context.Tick + 1);
            playerCommands.Consume();
            fire.Advance();
            ResolveCurrentFireContact();

            locomotion.BeginTick();
            collisions.BeginTick();
            objects.BeginTick();
            help.BeginTick(agents);
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
                    continue;
                }

                locomotion.RequestMove(agent);
            }

            chairs.ResolveStanding();
            leaders.CountFollowers();
            extinguishers.Spray();
            locomotion.ResolveMovement();
            help.MoveDragged(agents);
            items.FollowCarriers(agents);
            burning.SpreadFlames();
            doorBehaviour.ResolveRoomChangesAndEscapes();
            help.ResolveRescues(agents);
            collisions.Resolve();
            objects.ResolveContacts();
            objects.Advance();
            flammables.Update();
            doors.ResolveBlockages();
            CreditInfluenceForPeopleSaved();
        }

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
                doors.BlastChargesRemaining);
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
