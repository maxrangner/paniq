using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// One cause-and-effect record. <see cref="SourceId"/> is who or what caused
    /// it; <see cref="TargetId"/>, when set, is who or what it affected (the
    /// person run into, the box kicked, the door tried). <see cref="Strength"/>
    /// and <see cref="DurationTicks"/> mean what each event type says.
    /// </summary>
    public readonly struct CausalEvent
    {
        public CausalEvent(
            ulong eventId,
            int tick,
            SimulationId sourceId,
            CausalEventType eventType,
            LogicalPosition position,
            int strength,
            int durationTicks,
            ulong causalParentEventId,
            SimulationId targetId = default)
        {
            EventId = eventId;
            Tick = tick;
            SourceId = sourceId;
            EventType = eventType;
            Position = position;
            Strength = strength;
            DurationTicks = durationTicks;
            CausalParentEventId = causalParentEventId;
            TargetId = targetId;
        }

        public ulong EventId { get; }
        public int Tick { get; }
        public SimulationId SourceId { get; }
        public CausalEventType EventType { get; }
        public LogicalPosition Position { get; }
        public int Strength { get; }
        public int DurationTicks { get; }
        public ulong CausalParentEventId { get; }
        public bool HasCausalParent => CausalParentEventId != 0UL;

        /// <summary>Who or what this event affected; value 0 when it names nothing.</summary>
        public SimulationId TargetId { get; }

        public bool HasTarget => TargetId.Value != 0UL;
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
            SimulationId sourceId,
            CausalEventType eventType,
            LogicalPosition position,
            int strength = 0,
            int durationTicks = 0,
            ulong causalParentEventId = 0UL,
            SimulationId targetId = default)
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
                strength,
                durationTicks,
                causalParentEventId,
                targetId);
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

        /// <summary>The events so far, as a view that later events do not change. Nothing is copied.</summary>
        public AppendOnlyView<CausalEvent> View() => new AppendOnlyView<CausalEvent>(events);
    }
}
