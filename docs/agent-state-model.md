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

## Prototype terminal outcome extension

The [fire-reaction prototype](fire-reaction-prototype.md) adds a separate,
simulation-owned `AgentTerminalOutcome` record keyed by Agent ID. Its allowed
values are currently `Unresolved` and `Lost`; every initial agent starts
`Unresolved`. This is prototype-owned runtime state, not a new field or status
value in the neutral `AgentState` foundation. A later prototype stone that adds
a way to be rescued may add a `Saved` value.

When the prototype resolves an agent as lost, it changes that outcome from
`Unresolved`, changes the agent's participation status to
`NoLongerParticipating`, releases occupancy under the spatial rules, and emits
the matching immutable causal event as one transition. Counts shown to the
player are read from `AgentTerminalOutcome`, never inferred from event labels
or presentation state.

## Prototype temperament and body extension

The fire-reaction prototype also keeps two more simulation-owned records per
Agent ID:

- `AgentPanicTemperament` (`Runner`, `FreezeThenRun`, `FreezeForever`) is
  dealt once, at tick zero, from the scenario seed. It decides how the person
  panics and never changes during a run.
- `AgentBodyState` (`Upright`, `Staggering`, `Fallen`, `GettingUp`,
  `Unconscious`) says
  whether the body is under the person's control. It is separate from what
  they intend to do, so someone knocked over resumes their intention when
  they are back up. A person who is not upright requests no movement but
  still occupies space and can still be caught by fire.

Like the terminal outcome, these are prototype runtime state, not fields of
the neutral `AgentState` foundation.

`AgentBurning` (whether the person is on fire, when they will collapse, and
the `AgentCaughtFire` event that started it) is a third such record. A person
on fire is still participating until the burn ends; the transition to `Lost`
then happens as described above, with the catch as its cause.

## Prototype personality extension

`AgentTraitValues` (strength, speed, bravery, compassion, evil, nervousness,
each 0–10) is simulation-owned runtime state keyed by Agent ID. It is set at
tick zero from the scenario's authored traits, or drawn from the seed when a
person has none. Only the simulation may change it; nothing does yet, but a
later player power (such as "super strength") would be a player command that
does. Traits are read through `TraitEffects` whenever they are used, so such a
change would take effect at once, except for walking and sprinting pace, which
`TraitEffects.ApplyPace` sets and must be called again.

## Replay relevance

The meaning and allowed values of `AgentState`, plus the prototype's terminal
outcome record and lifecycle, are replay-relevant. Changes follow the
[simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

## Deferred implementation

This note does not add runtime classes, decision algorithms, navigation,
hazards, save files, or presentation. The [causal event log](causal-event-log.md)
defines the event retention this model needs, and the [spatial-world rules](spatial-world-rules.md)
define its logical-world data.

## Prototype additions

The fire-reaction prototype added these to the record above.

**What they are holding.** A person may be authored already holding something
(`CarriedObjectId` on the agent definition), and a thing held that way is marked
as *theirs*: they keep hold of it while calm rather than tidying it away, and let
go only when something frightens them. This is what makes a bag or a briefcase
different from a box somebody picked up to tidy.

**Composure.** A person told about a hazard by an alarm bell, rather than by
seeing it, may keep their head: they still head for a way out, but at walking
pace, without swerving, dithering or freezing. It is one flag on their fear,
consulted where pace, swerving and hesitation are worked out, rather than a
fourth fear state — which would have touched every check for "scared" in the
simulation. It is cleared the moment the hazard stops being an abstraction: it
reaches them, they are knocked about, or they see it for themselves.

**Traits can be changed by the player.** The player's Beefcake card sets a
person's strength to its maximum. Nothing else about them changes, and because
traits are read through `TraitEffects` whenever they are used and never cached,
every rule that depends on strength picks it up on the next tick. Pace is the
one exception, as noted above, and Beefcake deliberately leaves speed alone so
no pace has to be re-drawn and the run's random stream is undisturbed.
