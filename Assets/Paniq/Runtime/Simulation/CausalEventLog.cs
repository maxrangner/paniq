using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    public readonly struct CausalEvent
    {
        public CausalEvent(
            ulong eventId,
            int tick,
            StableAgentId sourceId,
            FireReactionEventType eventType,
            LogicalPosition position,
            int strengthMillimetres,
            int durationTicks,
            ulong causalParentEventId)
        {
            EventId = eventId;
            Tick = tick;
            SourceId = sourceId;
            EventType = eventType;
            Position = position;
            StrengthMillimetres = strengthMillimetres;
            DurationTicks = durationTicks;
            CausalParentEventId = causalParentEventId;
        }

        public ulong EventId { get; }
        public int Tick { get; }
        public StableAgentId SourceId { get; }
        public FireReactionEventType EventType { get; }
        public LogicalPosition Position { get; }
        public int StrengthMillimetres { get; }
        public int DurationTicks { get; }
        public ulong CausalParentEventId { get; }
        public bool HasCausalParent => CausalParentEventId != 0UL;
    }

    /// <summary>Append-only event history owned by one simulation run.</summary>
    public sealed class CausalEventLog
    {
        private readonly List<CausalEvent> events = new List<CausalEvent>();
        private ulong nextEventId = 1UL;

        public int Count => events.Count;
        public IReadOnlyList<CausalEvent> Events => events;

        public CausalEvent Append(
            int tick,
            StableAgentId sourceId,
            FireReactionEventType eventType,
            LogicalPosition position,
            int strengthMillimetres = 0,
            int durationTicks = 0,
            ulong causalParentEventId = 0UL)
        {
            if (causalParentEventId != 0UL && !Contains(causalParentEventId))
            {
                throw new InvalidOperationException($"Causal parent {causalParentEventId} is not in this run.");
            }

            ulong eventId = nextEventId++;
            if (causalParentEventId >= eventId)
            {
                throw new InvalidOperationException("A causal parent must have a lower event ID.");
            }

            var record = new CausalEvent(
                eventId,
                tick,
                sourceId,
                eventType,
                position,
                strengthMillimetres,
                durationTicks,
                causalParentEventId);
            events.Add(record);
            return record;
        }

        public bool Contains(ulong eventId)
        {
            return eventId != 0UL && eventId < nextEventId && eventId <= (ulong)events.Count;
        }

        /// <summary>Event IDs are allocated densely from 1, so lookup is by index.</summary>
        public CausalEvent Get(ulong eventId)
        {
            if (!Contains(eventId))
            {
                throw new KeyNotFoundException($"Unknown event ID {eventId}.");
            }

            return events[(int)(eventId - 1UL)];
        }

        public CausalEvent[] ToArray() => events.ToArray();
    }
}
