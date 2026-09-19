using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>One burning grid square. Cells are listed in the order they ignited.</summary>
    public readonly struct FireCellSnapshot
    {
        public FireCellSnapshot(int cellX, int cellZ, LogicalBounds bounds, int ignitionTick, ulong eventId)
        {
            CellX = cellX;
            CellZ = cellZ;
            Bounds = bounds;
            IgnitionTick = ignitionTick;
            EventId = eventId;
        }

        public int CellX { get; }
        public int CellZ { get; }
        public LogicalBounds Bounds { get; }
        public LogicalPosition Centre => Bounds.Centre;
        public int IgnitionTick { get; }

        /// <summary>The FireActivated or FireSpread event that lit this cell.</summary>
        public ulong EventId { get; }
    }

    public readonly struct FireReactionAgentSnapshot
    {
        public FireReactionAgentSnapshot(
            SimulationId agentId,
            LogicalPosition position,
            AgentParticipation participation,
            AgentFearState fearState,
            AgentAlertSource alertSource,
            AgentTerminalOutcome outcome,
            AgentActivityState activityState,
            int headingDegrees,
            int speedMillimetresPerTick,
            int calmSpeedMillimetresPerTick,
            int panicSpeedMillimetresPerTick,
            int reactionDelayTicks,
            AgentPanicTemperament temperament,
            AgentBodyState bodyState,
            AgentTraitValues traits)
        {
            Traits = traits;
            Temperament = temperament;
            BodyState = bodyState;
            AgentId = agentId;
            Position = position;
            Participation = participation;
            FearState = fearState;
            AlertSource = alertSource;
            Outcome = outcome;
            ActivityState = activityState;
            HeadingDegrees = headingDegrees;
            SpeedMillimetresPerTick = speedMillimetresPerTick;
            CalmSpeedMillimetresPerTick = calmSpeedMillimetresPerTick;
            PanicSpeedMillimetresPerTick = panicSpeedMillimetresPerTick;
            ReactionDelayTicks = reactionDelayTicks;
        }

        public SimulationId AgentId { get; }
        public LogicalPosition Position { get; }
        public AgentParticipation Participation { get; }
        public AgentFearState FearState { get; }
        public AgentAlertSource AlertSource { get; }
        public AgentTerminalOutcome Outcome { get; }
        public AgentActivityState ActivityState { get; }

        /// <summary>Whole degrees clockwise from north (+Z); 90 is east (+X).</summary>
        public int HeadingDegrees { get; }

        /// <summary>Current speed; the distance moved this tick if the move was accepted.</summary>
        public int SpeedMillimetresPerTick { get; }

        /// <summary>This agent's seeded walking pace.</summary>
        public int CalmSpeedMillimetresPerTick { get; }

        /// <summary>This agent's seeded sprinting pace.</summary>
        public int PanicSpeedMillimetresPerTick { get; }

        public int ReactionDelayTicks { get; }

        /// <summary>This agent's seeded way of panicking.</summary>
        public AgentPanicTemperament Temperament { get; }

        /// <summary>Upright, or staggering, lying on the floor, or getting up.</summary>
        public AgentBodyState BodyState { get; }

        /// <summary>Strength, speed, bravery, compassion, evil and nervousness, 0–10.</summary>
        public AgentTraitValues Traits { get; }

        public bool IsDown => BodyState == AgentBodyState.Fallen || BodyState == AgentBodyState.GettingUp;
    }

    /// <summary>A door as the player sees it: where its gap is and whether it is locked, unlocked or open.</summary>
    public readonly struct FireReactionDoorSnapshot
    {
        public FireReactionDoorSnapshot(SimulationId doorId, WallSide side, LogicalPosition centre, int widthMillimetres, DoorState state)
        {
            DoorId = doorId;
            Side = side;
            Centre = centre;
            WidthMillimetres = widthMillimetres;
            State = state;
        }

        public SimulationId DoorId { get; }
        public WallSide Side { get; }

        /// <summary>The middle of the door gap, on the wall line.</summary>
        public LogicalPosition Centre { get; }

        public int WidthMillimetres { get; }
        public DoorState State { get; }
    }

    /// <summary>A loose object on the floor, such as a box.</summary>
    public readonly struct FireReactionPhysicsObjectSnapshot
    {
        public FireReactionPhysicsObjectSnapshot(
            SimulationId objectId,
            PhysicsObjectKind kind,
            LogicalPosition position,
            int sizeMillimetres,
            int headingDegrees,
            int speedMillimetresPerTick)
        {
            ObjectId = objectId;
            Kind = kind;
            Position = position;
            SizeMillimetres = sizeMillimetres;
            HeadingDegrees = headingDegrees;
            SpeedMillimetresPerTick = speedMillimetresPerTick;
        }

        public SimulationId ObjectId { get; }
        public PhysicsObjectKind Kind { get; }
        public LogicalPosition Position { get; }
        public int SizeMillimetres { get; }

        /// <summary>Which way the object is turned; it spins when hit off-centre.</summary>
        public int HeadingDegrees { get; }

        public int SpeedMillimetresPerTick { get; }
    }

    /// <summary>
    /// A read-only view of simulation state after one tick, for presentation
    /// and tests. People, doors and boxes are copied; the event log and the
    /// burning cells only ever grow, so the snapshot holds a view of them as
    /// they were at this tick instead of a copy.
    /// </summary>
    public sealed class FireReactionSnapshot
    {
        private readonly FireReactionAgentSnapshot[] agents;
        private readonly IReadOnlyList<FireCellSnapshot> fireCells;
        private readonly FireReactionDoorSnapshot[] doors;
        private readonly FireReactionPhysicsObjectSnapshot[] physicsObjects;
        private readonly IReadOnlyList<CausalEvent> events;

        internal FireReactionSnapshot(
            int tick,
            bool fireActive,
            LogicalPosition fireOrigin,
            int fireCellSizeMillimetres,
            IReadOnlyList<FireCellSnapshot> fireCells,
            FireReactionAgentSnapshot[] agents,
            FireReactionDoorSnapshot[] doors,
            FireReactionPhysicsObjectSnapshot[] physicsObjects,
            IReadOnlyList<CausalEvent> events)
        {
            this.doors = doors;
            this.physicsObjects = physicsObjects;
            Tick = tick;
            FireActive = fireActive;
            FireOrigin = fireOrigin;
            FireCellSizeMillimetres = fireCellSizeMillimetres;
            this.fireCells = fireCells;
            this.agents = agents;
            this.events = events;
        }

        public int Tick { get; }
        public bool FireActive { get; }
        public LogicalPosition FireOrigin { get; }
        public int FireCellSizeMillimetres { get; }
        public IReadOnlyList<FireCellSnapshot> FireCells => fireCells;
        public IReadOnlyList<FireReactionAgentSnapshot> Agents => agents;
        public IReadOnlyList<CausalEvent> Events => events;
        public IReadOnlyList<FireReactionDoorSnapshot> Doors => doors;
        public IReadOnlyList<FireReactionPhysicsObjectSnapshot> PhysicsObjects => physicsObjects;

        public int CalmCount => Count(AgentFearState.Calm);
        public int ScaredCount => Count(AgentFearState.Scared);

        public int FrozenCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating &&
                        agents[i].ActivityState == AgentActivityState.Frozen)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int DownCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating && agents[i].IsDown)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int LostCount => CountOutcome(AgentTerminalOutcome.Lost);
        public int EscapedCount => CountOutcome(AgentTerminalOutcome.Escaped);

        private int CountOutcome(AgentTerminalOutcome outcome)
        {
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Outcome == outcome)
                {
                    count++;
                }
            }

            return count;
        }

        private int Count(AgentFearState fearState)
        {
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Participation == AgentParticipation.Participating && agents[i].FearState == fearState)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
