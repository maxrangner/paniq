# Agent state model

**Status:** decided foundation, checked against the code on 2026-09-26. This note defines the minimum stable,
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

Each autonomous agent has one simulation-owned record: the `Agent` class in
`Assets/Paniq/Runtime/Simulation/Agent.cs` (this note called it `AgentState`
before the code existed). Its foundation part is two fields:

| Field | Meaning |
| --- | --- |
| Agent ID | The agent's opaque, stable ID. It identifies the record, commands, events, and presentation mapping for the life of the run. |
| Participation status | Either `Participating` or `NoLongerParticipating`. It determines whether the agent is eligible for future autonomous simulation updates. |

The record is mutable run state, not authored scenario data. The state record
is keyed and processed by Agent ID. It contains no `GameObject`, `Component`,
`Transform`, or other Unity scene-object reference.

The record is retained after its participation status becomes
`NoLongerParticipating`. Retention preserves the agent's stable identity and
final participation status for causal events, replay inspection, and later
debugging. It does not keep the agent eligible for autonomous updates.

## Lifecycle

At logical tick zero, each authored initial-agent record creates an
`Agent` record with the same stable Agent ID and a `Participating` status.
The scenario remains read-only; the new record belongs only to that run.

When a future system creates an agent during a run, it first receives an Agent
ID from the deterministic creation path required by the simulation contract.
It then receives a `Participating` `Agent` record.

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
  agent. It cannot create, remove, or modify an `Agent`, nor can destroying a
  displayed object change an agent's participation status.
- A later system may attach additional simulation-owned state to an Agent ID,
  but it must define that state in its own design note and obey this model's
  identity, ownership, and deterministic-order rules.

Everything else a person has -- their body, personality, fear, intent,
hearing, what they know of the doors and rooms, whether they are burning,
carrying, helping, sitting, leading, pulling an alarm, in a group,
barricading, where they belong, their errand, and whether they have been
poked -- is prototype state attached to the same record as named parts
(`Body`, `Personality`, `Fear`, `Intent` and so on; the list is at the top of
`Agent.cs`). None of it is part of the neutral foundation, and each part obeys
this model's identity, ownership and ordering rules. The
[spatial-world rules](spatial-world-rules.md) own logical position.

`NoLongerParticipating` is intentionally neutral. A later design may use it
for death, evacuation, rescue, or another terminal outcome without changing
this foundation model.

## Prototype terminal outcome extension

The [office level](the-office-level.md) adds a simulation-owned
`AgentTerminalOutcome` (the record's `Outcome`). Its values are `Unresolved`,
`Lost`, `Escaped` (walked out of the building) and `Survived` (alive at the end
of the round, still inside, somewhere the hazard could not reach; it counts as
saved exactly as escaping does). Every initial agent starts `Unresolved`. New
values are only ever appended, because each value's number is part of the
replay fingerprint. This is prototype-owned runtime state, not a new status
value in the neutral foundation.

When the prototype resolves an agent as lost, it changes that outcome from
`Unresolved`, changes the agent's participation status to
`NoLongerParticipating`, releases occupancy under the spatial rules, and emits
the matching immutable causal event as one transition. Counts shown to the
player are read from `AgentTerminalOutcome`, never inferred from event labels
or presentation state.

## Prototype temperament and body extension

The office level also keeps two more simulation-owned parts per
person:

- `AgentPanicTemperament` (`Runner`, `FreezeThenRun`, `FreezeForever`) is
  dealt once, at tick zero, from the scenario seed. It decides how the person
  panics and never changes during a run.
- `AgentBodyState` (`Upright`, `Staggering`, `Fallen`, `GettingUp`,
  `Unconscious`) says
  whether the body is under the person's control. It is separate from what
  they intend to do, so someone knocked over resumes their intention when
  they are back up. A person who is not upright requests no movement but
  still occupies space and can still be caught by fire.

Like the terminal outcome, these are prototype runtime state, not part of the
neutral foundation.

`AgentBurning` (whether the person is on fire, when they will collapse, and
the `AgentCaughtFire` event that started it) is a third such record. A person
on fire is still participating until the burn ends; the transition to `Lost`
then happens as described above, with the catch as its cause.

## Prototype personality extension

`AgentTraitValues` (strength, speed, bravery, compassion, evil, nervousness
and leadership, each 0–10, 5 being ordinary) is simulation-owned runtime state
keyed by Agent ID. It is set at tick zero from the scenario's authored traits,
or drawn from the seed when a person has none. Only the simulation may change
it, and only in answer to a player command (the Beefcake card, below). Traits are read through `TraitEffects` whenever they are used, so such a
change would take effect at once, except for walking and sprinting pace, which
`TraitEffects.ApplyPace` sets and must be called again.

## Replay relevance

The meaning and allowed values of the foundation fields, plus the prototype's terminal
outcome record and lifecycle, are replay-relevant. Changes follow the
[simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

## Deferred implementation

This note defines the record, not decision algorithms, navigation, hazards,
save files, or presentation. The [causal event log](causal-event-log.md)
defines the event retention this model needs, and the [spatial-world rules](spatial-world-rules.md)
define its logical-world data.

## Prototype additions

The office level added these to the record above.

**Feelings, not event names (decided 2026-09-24).** What a person does follows
from their fear state, what alerted them, their temperament and their traits,
applied to what they see, hear and touch. No rule in the crowd switches on an
event type; the event log records why something happened, it does not tell
anybody what to do. Further feelings the game vision names (anger, trust in a
leader, curiosity) arrive as additional per-person state on this record, each
with the first danger or card that needs it, never as a switch on a named
event.

**What they are holding.** A person may be authored already holding something
(`CarriedObjectId` on the agent definition), and a thing held that way is marked
as *theirs*: they keep hold of it while calm rather than tidying it away, and let
go only when something frightens them. This is what makes a bag or a briefcase
different from a box somebody picked up to tidy.

**Sticking together (2026-09-25).** `AgentGroup` is the group a "Stick
together" throw bound them to, or none: the group's id, the throw's event as
the cause of whatever they learn from the others, the tick their pull begins
(their own reaction tick, never the throw's), and when they next compare notes
on the way out. It is read where a frightened person's steering and pace are
worked out, and where they choose a door. (Composure -- walking out calmly
after a bell -- was a flag here until 2026-09-25, when the owner ruled that a
bell frightens everybody.)

**Where they belong, and what they are doing about the day (2026-09-24).**
`AgentHome` is where somebody belongs: the chair that is theirs, or a spot,
set once from the authored definition and never changed. `AgentErrand` is the
purpose a cue has given them and how far along it they are (kind, phase, when
it starts, the room and door and partner it is about, its cause), written by
`CueSystem` and `ErrandBehaviour`, and cleared the moment fear takes over. Both
are prototype runtime state on this record, not fields of the neutral
foundation; see [the cue system](cue-system.md).

**Traits can be changed by the player.** The player's Beefcake card sets a
person's strength to its maximum. Nothing else about them changes, and because
traits are read through `TraitEffects` whenever they are used and never cached,
every rule that depends on strength picks it up on the next tick. Pace is the
one exception, as noted above, and Beefcake deliberately leaves speed alone so
no pace has to be re-drawn and the run's random stream is undisturbed.

**Calming down, and being rattled (2026-09-26).** Fear is no longer one way.
`AgentFear` gains when anything frightening last went on around them
(`LastFrightTick`), their own quiet spell before it starts to drain
(`QuietTicks`, drawn when they take fright), the tick they settle on once it
has drained below the line (`CalmsAtTick`), until when they stay rattled
(`RattledUntilTick`), and whether they saw the danger this time rather than
only heard about it (`SawTheThreat`, which decides how long they stay
rattled). How frightened they are right now is not stored: `FearSystem.Settle`
works it out each tick from those and their traits. See
[the cue system](cue-system.md) and `CalmingSettings`.

**Nudged and annoyed, drawn and remembered (2026-09-26).** `AgentNudge`
gains `AnnoyedUntilTick`: while it lasts they shake, and a nudge does
nothing to them. `AgentIntent.GoingToTheInfluence` marks somebody who got up
or left an errand because the player's influence drew them, so what they
choose next is to go to it. `AgentDoorMemory.PreviousRoom` is the room they
were in before this one, so influence never pulls them straight back through
the door they came in by; `HeapDoor` and `GiveUpOnTheHeapTick` are the
heaped doorway somebody strong is having a go at, and when they give it up.
