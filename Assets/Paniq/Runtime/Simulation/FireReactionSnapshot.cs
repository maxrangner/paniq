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
            StableAgentId agentId,
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
            int reactionDelayTicks)
        {
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

        public StableAgentId AgentId { get; }
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
    }

    /// <summary>A copied, read-only view of simulation state for presentation and tests.</summary>
    public sealed class FireReactionSnapshot
    {
        private readonly FireReactionAgentSnapshot[] agents;
        private readonly FireCellSnapshot[] fireCells;
        private readonly CausalEvent[] events;

        internal FireReactionSnapshot(
            int tick,
            bool fireActive,
            LogicalPosition fireOrigin,
            int fireCellSizeMillimetres,
            FireCellSnapshot[] fireCells,
            FireReactionAgentSnapshot[] agents,
            CausalEvent[] events)
        {
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

        public int CalmCount => Count(AgentFearState.Calm);
        public int ScaredCount => Count(AgentFearState.Scared);

        public int LostCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Outcome == AgentTerminalOutcome.Lost)
                    {
                        count++;
                    }
                }

                return count;
            }
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
