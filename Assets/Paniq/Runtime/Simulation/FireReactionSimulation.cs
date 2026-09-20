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
        private readonly WorldGeometry geometry;

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

            var crowd = new Crowd(agents, scenario.World.OccupancyRadiusMillimetres);
            doors = new DoorSystem(context, doorStates, geometry);
            doors.UseCrowd(crowd);
            var sound = new SoundSystem(context, crowd, fire, fear, geometry);
            perception = new PerceptionSystem(context, fire, fear, sound);
            body = new BodySystem(context, fire, sound);
            collisions = new CollisionSystem(context, crowd, body, fear, sound);
            objects = new PhysicsObjectSystem(context, crowd, geometry, body, fear, sound);
            locomotion = new Locomotion(context, crowd, geometry, fire, body, collisions, objects);
            flammables = new FlammablesSystem(context, crowd, geometry, fire, objects, body);
            items = new ItemBehaviour(context, geometry, objects, flammables);
            calm = new CalmBehaviour(context, crowd, geometry, locomotion, items);
            doorBehaviour = new DoorBehaviour(context, crowd, geometry, doors, fire, sound);
            help = new HelpBehaviour(context, crowd, geometry, fire, fear, body, objects, locomotion);
            panic = new PanicBehaviour(context, crowd, geometry, fire, fear, sound, body, doorBehaviour, help, locomotion);
            burning = new BurningBehaviour(context, crowd, body, sound, locomotion);
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
                agent.Body.Position = definition.InitialPosition;
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

        /// <summary>Every command queued so far, in sequence order. Replaying them gives the same run.</summary>
        public IReadOnlyList<PlayerCommand> Commands => doors.Commands;

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

        public FireReactionPhysicsObjectSnapshot GetPhysicsObject(int index) =>
            objects.GetSnapshot(index).WithBurn(flammables.ObjectState(index), flammables.ObjectHeatPercent(index));

        public int TableCount => context.Scenario.Tables.Length;

        public FireReactionTableSnapshot GetTable(int index) => flammables.GetTableSnapshots()[index];

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        internal void LaunchObjectForTests(int index, int velocityX, int velocityZ) => objects.Launch(index, velocityX, velocityZ);

        /// <summary>Tests only: the fire system, to check its queries against a brute-force answer.</summary>
        internal FireSystem FireForTests => fire;

        /// <summary>
        /// Queues a player action for a tick that has not started yet. Commands
        /// for the same tick run in the order they were queued.
        /// </summary>
        public PlayerCommand QueueCommand(PlayerCommandType commandType, SimulationId targetId, int targetTick)
        {
            return doors.QueueCommand(commandType, targetId, targetTick);
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
            doors.ConsumeCommands();
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
                CountClearOfFire());
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
