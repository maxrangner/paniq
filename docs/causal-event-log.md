# Causal event log and debugging view

**Status:** decided foundation. This note defines how a run retains, queries,
and presents the causal events required by the simulation contract. It does not
define event types, event mechanics, storage optimization, or player-facing UI.

## Purpose

Paniq's surprising outcomes must remain explainable. The causal event log is
the run's ordered history of what the simulation says happened and why. It lets
a later debugging tool follow an outcome back through its direct causes without
asking the presentation layer to reconstruct or guess simulation history.

This note builds on the [simulation contract](simulation-contract.md),
[scenario data and runtime state](scenario-runtime-state.md), and [agent state
model](agent-state-model.md). The simulation contract remains the authority for
the event envelope, event allocation, processing order, and presentation
boundary.

## Event-log lifetime

Each run owns one append-only `CausalEventLog` as part of its mutable runtime
state. The log records every immutable causal event that the simulation emits
for that run, exactly once. It contains the contract envelope unchanged:
Event ID, tick, source ID, event type, logical position, optional strength,
optional duration, causal parent, and optional target ID. Each event type
defines the meaning of its populated optional fields; absent values do not imply
a magic numeric value.

Snapshots do not copy the log. Because entries are only ever added, a snapshot
holds a read-only view of the entries that existed at its tick, and later
entries stay invisible to it.

Events are retained in ascending tick, then ascending Event ID order. The log
retains the complete history while its run exists; it does not prune, reorder,
or replace entries. A future checkpoint must retain the log prefix available at
its capture tick so that resuming it does not lose causal history.

An event with no causal parent is a root event. An event with a causal parent
may be appended only when that parent has already been recorded in the same run
and has a lower Event ID. A missing, foreign-run, or later parent is an internal
invariant failure: the emitting system validates it before related simulation
mutation, and the log rejects it at append time. Failure stops the run with a
diagnostic; the simulation never silently drops the event or stores an
incomplete chain.

Each event has one direct causal parent. Multiple contributing causes are
deferred until a concrete social or panic mechanic needs them; that mechanic
must then define replay-relevant contributor data rather than overloading the
direct-parent field.

Recording an event preserves an outcome the simulation has already decided. It
does not itself decide an outcome, invoke gameplay logic, alter event delivery,
or determine the order in which systems process events.

## Read-only queries

The log exposes immutable event records and read-only query results. Queries
never mutate the log, run state, or simulation processing order.

| Query | Result |
| --- | --- |
| Event ID lookup | The matching event, or no result when that ID is absent from this run. |
| Timeline filter | Events in an inclusive tick range, optionally narrowed by source ID and/or event type. When several filters are present, an event must satisfy all of them. |
| Causal chain | The selected event and its ancestors, ordered from the root event to the selected event. |
| Direct children | Events whose causal parent is the selected Event ID, in contract order. |

All collection results use ascending tick, then ascending Event ID order.
Queries identify entities only through stable IDs and do not expose Unity
scene-object references.

## Debugging-view boundary

The simulation provides presentation with a read-only snapshot of the event
log. A snapshot may be recreated or observed after the simulation advances, but
presentation cannot append, remove, alter, or reorder logged events.

A future developer-facing debugging view may present:

- a chronological event list; and
- the selected event's contract fields and its root-to-event causal chain.

The view may select events, filter results, and format labels for people to
read. Those actions are presentation-only: they cannot create events, mutate
run state, consume simulation randomness, or infer a missing cause. The exact
screen layout, visual treatment, controls, and player-facing explanation remain
undecided.

## Replay relevance and deferred work

The event envelope, optional-field meanings, direct-parent rule, and log
ordering are replay-relevant. Changes follow the [simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

This foundation deliberately does not set an event-count limit, retention cap,
archive/export format, indexing strategy, or player UI. Keep the complete
in-memory run history until profiling demonstrates a concrete need to revisit
that choice. New event types and their effects belong to the systems that
introduce them.
