using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The complete deterministic fire-reaction run. It has no Unity object or
    /// presentation dependency; FireReactionRunner is the only Unity tick owner.
    /// Behaviour is split across partial files: Fire, Steering, Calm, Panic,
    /// Sound, Collisions, Doors, Physics.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        public const int MillimetresPerMetre = 1000;
        public const int TicksPerSecond = 50;
        public const ulong FireHazardIdValue = 0xF1AE000000000001UL;

        private sealed class AgentRuntime
        {
            public StableAgentId Id;
            public LogicalPosition Position;
            public AgentParticipation Participation;
            public AgentFearState FearState;
            public AgentAlertSource AlertSource;
            public AgentTerminalOutcome Outcome;
            public AgentActivityState Activity;
            public int ActivityEndTick;

            // Body state: facing and speed change gradually through steering.
            public int Heading;
            public int Speed;
            public int BlockedTicks;

            // Seeded personality.
            public int CalmSpeed;
            public int PanicSpeed;
            public int CalmTurnRate;
            public int PanicTurnRate;

            // Current intention.
            public LogicalPosition Target;
            public int LookHeading;
            public int LooksRemaining;
            public int WanderOffset;
            public int NextWanderTick;
            public int SocialPartnerIndex = -1;
            public int SwerveOffset;
            public int SwerveEndTick;
            public int NextPanicDecisionTick;

            // Fear.
            public int ReactionDelayTicks;
            public int ReactionEndTick;
            public ulong AlertEventId;
            public ulong ScaredEventId;
            public AgentPanicTemperament Temperament;
            public int FreezeEndTick;
            public ulong FrozeEventId;
            public int NextShoutTick;

            // Hearing: where the last noise worth turning toward came from.
            public LogicalPosition SoundPoint;
            public bool HasSoundPoint;
            public int InvestigateStartTick;

            // Body: staggering, lying on the floor, or getting up.
            public AgentBodyState BodyState;
            public int BodyEndTick;
            public ulong BodyEventId;

            // Doors: the one being run for, and ones that recently would not open.
            public int ExitDoorIndex = -1;
            public int[] DoorAvoidUntilTick;
            public ulong DoorAttemptEventId;
            public int NextShoveTick;
        }

        private readonly FireReactionScenarioData scenario;
        private readonly AgentRuntime[] agents;
        private readonly CausalEventLog eventLog = new CausalEventLog();
        private readonly List<MovementRequest> requests = new List<MovementRequest>();
        private Pcg32 random;
        private int tick;

        public FireReactionSimulation(Paniq.Gameplay.FireReactionScenario scenarioAsset, ulong? seedOverride = null)
            : this(scenarioAsset != null
                ? scenarioAsset.ToRuntimeData()
                : throw new ArgumentNullException(nameof(scenarioAsset)), seedOverride)
        {
        }

        public FireReactionSimulation(FireReactionScenarioData scenarioData, ulong? seedOverride = null)
        {
            scenario = scenarioData ?? throw new ArgumentNullException(nameof(scenarioData));
            scenario.Validate();
            random = new Pcg32(seedOverride ?? scenario.DefaultSeed);
            InitializeFire();
            InitializeDoors();
            InitializePhysicsObjects();

            var definitions = (FireReactionAgentDefinition[])scenario.Agents.Clone();
            Array.Sort(definitions, (left, right) => left.AgentId.CompareTo(right.AgentId));
            agents = new AgentRuntime[definitions.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                FireReactionAgentDefinition definition = definitions[i];
                int heading = IntegerMath.CardinalToDegrees(definition.InitialFacingDirection);
                agents[i] = new AgentRuntime
                {
                    Id = definition.AgentId,
                    Position = definition.InitialPosition,
                    Participation = AgentParticipation.Participating,
                    FearState = AgentFearState.Calm,
                    AlertSource = AgentAlertSource.None,
                    Outcome = AgentTerminalOutcome.Unresolved,
                    Activity = AgentActivityState.Standing,
                    Heading = heading,
                    LookHeading = heading,
                    CalmSpeed = random.NextIntInclusive(scenario.CalmSpeedMinimum, scenario.CalmSpeedMaximum),
                    PanicSpeed = random.NextIntInclusive(scenario.PanicSpeedMinimum, scenario.PanicSpeedMaximum),
                    CalmTurnRate = random.NextIntInclusive(scenario.CalmTurnRateMinimum, scenario.CalmTurnRateMaximum),
                    PanicTurnRate = random.NextIntInclusive(scenario.PanicTurnRateMinimum, scenario.PanicTurnRateMaximum),
                    // Stagger first decisions so the room does not start in lockstep.
                    ActivityEndTick = random.NextIntInclusive(1, scenario.CalmDecisionMaximumTicks),
                    DoorAvoidUntilTick = new int[doors.Length]
                };
            }

            DealTemperaments();
        }

        /// <summary>
        /// Temperaments are dealt like a shuffled deck rather than rolled one
        /// by one, so every room gets the authored mix (10 people: 2 freeze
        /// for good, 3 freeze for a while, 5 run) and only who-gets-which is
        /// left to the seed.
        /// </summary>
        private void DealTemperaments()
        {
            int count = agents.Length;
            int freezeForever = (count * scenario.FreezeForeverPercent + 50) / 100;
            int freezeThenRun = Math.Min(count - freezeForever, (count * scenario.FreezeThenRunPercent + 50) / 100);
            var deck = new AgentPanicTemperament[count];
            for (int i = 0; i < count; i++)
            {
                deck[i] = i < freezeForever ? AgentPanicTemperament.FreezeForever
                    : i < freezeForever + freezeThenRun ? AgentPanicTemperament.FreezeThenRun
                    : AgentPanicTemperament.Runner;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = random.NextIntInclusive(0, i);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            for (int i = 0; i < count; i++)
            {
                agents[i].Temperament = deck[i];
            }
        }

        public int Tick => tick;
        public int AgentCount => agents.Length;
        public CausalEventLog EventLog => eventLog;
        public Pcg32 Random => random;
        public FireReactionScenarioData Scenario => scenario;

        public FireReactionAgentSnapshot GetAgent(int index) => ToSnapshot(agents[index]);

        public FireReactionAgentSnapshot GetAgent(StableAgentId id)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Id == id)
                {
                    return ToSnapshot(agents[i]);
                }
            }

            throw new KeyNotFoundException($"Unknown agent ID {id}.");
        }

        /// <summary>
        /// One logical tick, in the simulation contract's order: player
        /// commands, hazard, hazard contact, agent decisions (ascending ID),
        /// movement resolution with contact along accepted moves, exits,
        /// collisions, then physical objects.
        /// </summary>
        public void Step()
        {
            tick = checked(tick + 1);
            ConsumeCommands();
            AdvanceFire();
            ResolveCurrentFireContact();

            requests.Clear();
            bumps.Clear();
            objectContacts.Clear();
            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime agent = agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                UpdateFear(agent);
                if (UpdateBody(agent))
                {
                    // Staggering, on the floor or getting up: no control, no move.
                    continue;
                }

                switch (agent.FearState)
                {
                    case AgentFearState.Calm:
                        UpdateCalm(i, agent);
                        break;
                    case AgentFearState.Alert:
                        UpdateAlert(agent);
                        break;
                    default:
                        UpdatePanic(i, agent);
                        break;
                }

                if (agent.BodyState != AgentBodyState.Upright)
                {
                    // Tripped during this decision.
                    agent.Speed = 0;
                    continue;
                }

                LogicalPosition displacement = ChooseDisplacement(i, agent);
                if (displacement.X != 0 || displacement.Z != 0)
                {
                    requests.Add(new MovementRequest(i, displacement));
                }
            }

            ResolveMovement();
            ResolveExits();
            ResolveCollisions();
            AdvancePhysicsObjects();
        }

        public FireReactionSnapshot GetSnapshot()
        {
            var agentSnapshots = new FireReactionAgentSnapshot[agents.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                agentSnapshots[i] = ToSnapshot(agents[i]);
            }

            return new FireReactionSnapshot(
                tick,
                fireActive,
                FireOrigin,
                scenario.FireCellSizeMillimetres,
                GetFireCells(),
                agentSnapshots,
                GetDoorSnapshots(),
                GetPhysicsObjectSnapshots(),
                eventLog.ToArray());
        }

        private void UpdateFear(AgentRuntime agent)
        {
            if (!fireActive)
            {
                return;
            }

            bool enteredAlert = false;
            if (agent.FearState == AgentFearState.Calm)
            {
                if (SeesFire(agent))
                {
                    StartVisualAlert(agent);
                    enteredAlert = true;
                }
                else if (agent.Activity != AgentActivityState.Investigating)
                {
                    TryHearFire(agent);
                }
            }

            if (agent.FearState != AgentFearState.Alert)
            {
                return;
            }

            if (!enteredAlert && agent.AlertSource != AgentAlertSource.Visual && SeesFire(agent))
            {
                PromoteAlertToVisual(agent);
            }

            // The alert stays visible for at least the detection tick, even
            // when the seeded reaction delay is zero.
            if (!enteredAlert && tick >= agent.ReactionEndTick)
            {
                MakeScared(agent);
            }
        }

        /// <summary>
        /// A startled agent stops. If it saw the fire it turns to face it;
        /// if it was yelled at or bumped, it turns toward where that came from.
        /// </summary>
        private void UpdateAlert(AgentRuntime agent)
        {
            agent.Activity = AgentActivityState.Reacting;
            int goalHeading = agent.Heading;
            if (agent.AlertSource == AgentAlertSource.Visual &&
                NearestFireDistanceSquared(agent.Position, out LogicalPosition firePoint) < long.MaxValue)
            {
                goalHeading = HeadingBetween(agent.Position, firePoint, agent.Heading);
            }
            else if (agent.HasSoundPoint)
            {
                goalHeading = HeadingBetween(agent.Position, agent.SoundPoint, agent.Heading);
            }

            ApplyBody(agent, goalHeading, 0, agent.PanicTurnRate, scenario.PanicAcceleration);
        }

        private void StartVisualAlert(AgentRuntime agent)
        {
            ulong alertEventId = StartAlert(agent, fireActivationEventId, AgentAlertSource.Visual);
            Yell(agent, alertEventId);
        }

        /// <summary>A yell is a sound: understood (alarming) close by, merely heard further away.</summary>
        private void Yell(AgentRuntime agent, ulong causalParentEventId)
        {
            CausalEvent yell = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentYelled,
                agent.Position,
                scenario.YellHearingRadiusMillimetres,
                0,
                causalParentEventId);
            EmitSound(agent.Id, agent.Position, scenario.YellHearingRadiusMillimetres,
                scenario.YellRadiusMillimetres, yell.EventId);
        }

        private void PromoteAlertToVisual(AgentRuntime agent)
        {
            agent.AlertSource = AgentAlertSource.Visual;
            CausalEvent alert = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentAlerted,
                agent.Position,
                burningCells.Count,
                agent.ReactionDelayTicks,
                fireActivationEventId);
            agent.AlertEventId = alert.EventId;
        }

        private ulong StartAlert(AgentRuntime agent, ulong causalParentEventId, AgentAlertSource alertSource)
        {
            agent.FearState = AgentFearState.Alert;
            agent.AlertSource = alertSource;
            agent.Activity = AgentActivityState.Reacting;
            agent.SocialPartnerIndex = -1;
            agent.HasSoundPoint = false;
            agent.ReactionDelayTicks = random.NextIntInclusive(0, scenario.MaximumReactionDelayTicks);
            agent.ReactionEndTick = checked(tick + agent.ReactionDelayTicks);
            CausalEvent alert = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentAlerted,
                agent.Position,
                burningCells.Count,
                agent.ReactionDelayTicks,
                causalParentEventId);
            agent.AlertEventId = alert.EventId;
            return alert.EventId;
        }

        /// <summary>
        /// Panic takes one of three shapes, fixed per person: run, freeze and
        /// then run, or freeze for good.
        /// </summary>
        private void MakeScared(AgentRuntime agent)
        {
            if (agent.FearState == AgentFearState.Scared)
            {
                return;
            }

            agent.FearState = AgentFearState.Scared;
            CausalEvent scared = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentScared,
                agent.Position,
                burningCells.Count,
                0,
                agent.AlertEventId != 0UL ? agent.AlertEventId : fireActivationEventId);
            agent.ScaredEventId = scared.EventId;

            if (agent.Temperament == AgentPanicTemperament.Runner)
            {
                StartFleeing(agent);
                return;
            }

            agent.Activity = AgentActivityState.Frozen;
            agent.FreezeEndTick = agent.Temperament == AgentPanicTemperament.FreezeForever
                ? int.MaxValue
                : checked(tick + random.NextIntInclusive(scenario.FreezeMinimumTicks, scenario.FreezeMaximumTicks));
            CausalEvent froze = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentFroze,
                agent.Position,
                0,
                agent.FreezeEndTick == int.MaxValue ? 0 : agent.FreezeEndTick - tick,
                scared.EventId);
            agent.FrozeEventId = froze.EventId;
        }

        private void StartFleeing(AgentRuntime agent)
        {
            agent.Activity = AgentActivityState.Fleeing;
            agent.NextPanicDecisionTick = tick;
            agent.NextShoutTick = checked(tick + random.NextIntInclusive(
                scenario.PanicShoutMinimumTicks,
                scenario.PanicShoutMaximumTicks));
        }

        private void MakeLost(AgentRuntime agent, ulong fireCellEventId)
        {
            if (agent.Participation != AgentParticipation.Participating)
            {
                return;
            }

            agent.Participation = AgentParticipation.NoLongerParticipating;
            agent.Outcome = AgentTerminalOutcome.Lost;
            agent.Speed = 0;
            eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentLost,
                agent.Position,
                burningCells.Count,
                0,
                fireCellEventId);
        }

        private void ResolveCurrentFireContact()
        {
            if (!fireActive)
            {
                return;
            }

            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime agent = agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                ulong cellEventId = FindFireTouching(agent.Position);
                if (cellEventId != 0UL)
                {
                    MakeLost(agent, cellEventId);
                }
            }
        }

        /// <summary>
        /// Requests arrive in ascending Agent ID order. Resolution starts at
        /// index tick mod N and visits that circular order once, so the first
        /// claim on contested space rotates between agents.
        /// </summary>
        private void ResolveMovement()
        {
            int count = requests.Count;
            if (count == 0)
            {
                return;
            }

            int start = tick % count;
            for (int offset = 0; offset < count; offset++)
            {
                MovementRequest request = requests[(start + offset) % count];
                AgentRuntime agent = agents[request.AgentIndex];
                LogicalPosition startPosition = agent.Position;
                LogicalPosition destination = startPosition + request.Displacement;
                if (!IsMovementValid(request.AgentIndex, startPosition, destination))
                {
                    agent.Speed = 0;
                    agent.BlockedTicks++;
                    continue;
                }

                agent.Position = destination;
                agent.BlockedTicks = 0;
                if (fireActive)
                {
                    ulong cellEventId = FindFireTouchingSweep(startPosition, destination);
                    if (cellEventId != 0UL)
                    {
                        MakeLost(agent, cellEventId);
                    }
                }
            }
        }

        private bool IsMovementValid(int agentIndex, LogicalPosition start, LogicalPosition destination)
        {
            long maximumStep = scenario.MaximumStepDistanceMillimetres;
            if (LogicalPosition.DistanceSquared(start, destination) > maximumStep * maximumStep ||
                !IsInWalkableSpace(agents[agentIndex], destination) ||
                ClipsDoorFrame(start, destination))
            {
                return false;
            }

            return FindBlockingAgent(agentIndex, start, destination) < 0 &&
                   FindBlockingObject(start, destination, scenario.OccupancyRadiusMillimetres) < 0;
        }

        /// <summary>The lowest-index participating person this move would pass through, or -1.</summary>
        private int FindBlockingAgent(int agentIndex, LogicalPosition start, LogicalPosition destination)
        {
            long touching = (long)scenario.OccupancyRadiusMillimetres * 2L;
            long touchingSquared = touching * touching;
            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime other = agents[i];
                if (i == agentIndex || other.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                if (IntegerMath.SegmentPassesWithin(start, destination, other.Position, touchingSquared))
                {
                    return i;
                }
            }

            return -1;
        }

        private static FireReactionAgentSnapshot ToSnapshot(AgentRuntime agent)
        {
            return new FireReactionAgentSnapshot(
                agent.Id,
                agent.Position,
                agent.Participation,
                agent.FearState,
                agent.AlertSource,
                agent.Outcome,
                agent.Activity,
                agent.Heading,
                agent.Speed,
                agent.CalmSpeed,
                agent.PanicSpeed,
                agent.ReactionDelayTicks,
                agent.Temperament,
                agent.BodyState);
        }

        private readonly struct MovementRequest
        {
            public MovementRequest(int agentIndex, LogicalPosition displacement)
            {
                AgentIndex = agentIndex;
                Displacement = displacement;
            }

            public int AgentIndex { get; }
            public LogicalPosition Displacement { get; }
        }
    }
}
