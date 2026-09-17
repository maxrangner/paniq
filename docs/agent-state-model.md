# Agent state model

**Status:** decided foundation. This note defines the minimum stable,
simulation-owned runtime state for an autonomous agent. It does not define
agent decision logic, crowd behaviour, hazards, movement, or content-specific
reactions and outcomes.

## Purpose

An agent needs a persistent simulation record so the game can identify the
same individual throughout a run and explain what happened to it later. This
record is deliberately smaller than a future AI or character system: it only
answers who the agent is and whether it still takes part in the simulation.

This note builds on the [simulation contract](simulation-contract.md) and
[scenario data and runtime state](scenario-runtime-state.md). Those notes
remain the authority for deterministic time, stable IDs, events, scenario
authoring, replay provenance, and the boundary between simulation and
presentation.

## Minimum agent record

Each autonomous agent has one simulation-owned `AgentState` record containing:

| Field | Meaning |
| --- | --- |
| Agent ID | The agent's opaque, stable ID. It identifies the record, commands, events, and presentation mapping for the life of the run. |
| Participation status | Either `Participating` or `NoLongerParticipating`. It determines whether the agent is eligible for future autonomous simulation updates. |

`AgentState` is mutable run state, not authored scenario data. The state record
is keyed and processed by Agent ID. It contains no `GameObject`, `Component`,
`Transform`, or other Unity scene-object reference.

The record is retained after its participation status becomes
`NoLongerParticipating`. Retention preserves the agent's stable identity and
final participation status for causal events, replay inspection, and later
debugging. It does not keep the agent eligible for autonomous updates.

## Lifecycle

At logical tick zero, each authored initial-agent record creates an
`AgentState` record with the same stable Agent ID and a `Participating` status.
The scenario remains read-only; the new record belongs only to that run.

When a future system creates an agent during a run, it first receives an Agent
ID from the deterministic creation path required by the simulation contract.
It then receives a `Participating` `AgentState` record.

Only the simulation may change participation status. A transition to
`NoLongerParticipating` must also emit an immutable causal event using the
contract's shared event envelope. The system that introduces the transition
defines its event type, cause, and presentation; this note only requires that
the transition is traceable.

## Invariants and boundaries

- Future autonomous systems process participating agent records in ascending
  Agent ID order whenever order could affect a simulation outcome, random draw,
  or emitted event.
- Presentation may temporarily map an Agent ID to a Unity object to display an
  agent. It cannot create, remove, or modify `AgentState`, nor can destroying a
  displayed object change an agent's participation status.
- A later system may attach additional simulation-owned state to an Agent ID,
  but it must define that state in its own design note and obey this model's
  identity, ownership, and deterministic-order rules.

The following are deliberately not fields of `AgentState` yet: movement intent,
perception, goals, decision state, panic, health, abilities, relationships, and
hazard effects. The [spatial-world rules](spatial-world-rules.md) own logical
position; later agent, hazard, and player-power work owns the remaining
concepts.

`NoLongerParticipating` is intentionally neutral. A later design may use it
for death, evacuation, rescue, or another terminal outcome without changing
this foundation model.

## Vertical-slice terminal outcome extension

The vertical slice adds a separate, simulation-owned `AgentTerminalOutcome`
record keyed by Agent ID. Its allowed values are `Unresolved`, `Saved`, and
`Lost`; every initial agent starts `Unresolved`. This is slice-owned runtime
state, not a new field or status value in the neutral `AgentState` foundation.

When the slice resolves an agent as saved or lost, it changes that outcome from
`Unresolved`, changes the agent's participation status to
`NoLongerParticipating`, releases occupancy under the spatial rules, and emits
the matching immutable causal event as one transition. The final score counts
`Saved` outcomes over the initial-agent count; it does not infer outcomes from
event labels or presentation state.

## Replay relevance

The meaning and allowed values of `AgentState`, plus the slice's terminal
outcome record and lifecycle, are replay-relevant. Changes follow the
[simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

## Deferred implementation

This note does not add runtime classes, decision algorithms, navigation,
hazards, save files, or presentation. The [causal event log](causal-event-log.md)
defines the event retention this model needs, and the [spatial-world rules](spatial-world-rules.md)
define its logical-world data.
