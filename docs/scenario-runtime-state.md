# Scenario data and runtime state

**Status:** decided foundation. This note defines the boundary between the
scenario a designer authors and the state created by one playthrough. It does
not define agent decisions, hazards, movement, interactable behaviour, or
player powers.

## Purpose

A scenario is the reusable starting situation that a player can attempt more
than once. A run is one changing attempt at that situation. Keeping them
separate lets the game replay a run, explain its result, and start a fresh run
without carrying over hidden changes from an earlier attempt.

This note applies the [simulation contract](simulation-contract.md) to
scenario authoring and future replay/save data. The simulation contract remains
the authority for ticks, randomness, stable identity, processing order, events,
and the presentation boundary.

## Authored scenario data

Scenario data is read-only input created before play. It lives in the scenario
scene and the ScriptableObject assets that scene uses; it is never a store for
changes made during a run. Unity saves ScriptableObjects as project assets and
uses them as data containers independent of scene objects, which makes them a
suitable place for reusable authored configuration.
[Unity ScriptableObject manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html)

Every authored scenario must provide the following replay-relevant data:

| Field | Meaning |
| --- | --- |
| Scenario ID | An opaque, stable identifier for this scenario. |
| Content revision | A human-managed revision label for this authored starting situation. Increment it when an edit could change a replay outcome. |
| Default seed | The explicit unsigned 64-bit seed used when a run does not deliberately supply another seed. |
| Simulation compatibility version | The version of the simulation rules and data interpretation this scenario expects. |
| Scene/layout reference | The authored scene and its layout/configuration that recreate the starting situation. |
| Initial entity records | Data records for the entities present at tick zero, each with its stable ID and any future system-specific authored values. |

Initial entity records and all future scenario data use stable IDs and plain
data. They must not contain `GameObject`, `Component`, `Transform`, or another
scene-object reference as simulation identity or state. A presentation mapping
may connect an ID to a temporary Unity object after a run starts, but that
mapping is not scenario data.

The scenario scene and ScriptableObject assets may be edited in Unity while
authoring. Runtime code must not modify them in the editor or a deployed build.
Any runtime value needed by a system belongs in run state instead.

## Runtime run state

Starting a run resolves its authored scenario once and creates new,
simulation-owned state at logical tick zero. The runtime state may read the
resolved scenario configuration but must never write changes back to it.

Run state contains the mutable data required to continue the same attempt,
including:

- the current logical tick;
- each simulated entity's current state, keyed by stable ID;
- events created during the run and their stable event IDs, retained through
  the [causal event log](causal-event-log.md);
- the deterministic random-number generator's current state; and
- ordered [`PlayerCommand`](simulation-contract.md#player-commands) records
  assigned to logical ticks.

The [agent state model](agent-state-model.md) defines an agent's minimum
identity and participation state. The [spatial-world rules](spatial-world-rules.md)
define logical positions, occupancy, and basic movement. The remaining fields
for agents, hazards, and player powers remain undefined until their own
foundation notes. Those systems must add mutable values to run state rather than
to scenario data.

The simulation is the only authority that changes run state. Visuals, audio,
and UI observe simulation snapshots and events through stable IDs; they cannot
change run state, change authored data, or advance simulation randomness.

## Run provenance and compatibility

Each run records a provenance record when it starts. It contains:

| Field | Why it is recorded |
| --- | --- |
| Scenario ID and content revision | Identifies the authored starting situation. |
| Chosen seed | Records the default unsigned 64-bit seed or a deliberate run-specific override. |
| Simulation compatibility version | Identifies the rules used to interpret the scenario and replay inputs. |
| Fixed step | Identifies the logical tick duration used by the run. |
| Paniq build version | Identifies the executable that produced the run. |
| Platform | Identifies the platform that produced the run. |

Normally, a run uses the scenario's default seed. A future launcher may offer a
deliberate override to create a different variation; it must record that chosen
seed in provenance before tick zero. No system may silently generate or replace
a seed.

Scenario ID and revision, chosen seed, compatibility version, fixed step,
build, platform, generator state, and ordered commands are replay-relevant.
Loading and migration follow the [simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

## Lifecycle example

1. A designer authors a scenario scene and configuration assets, assigns stable
   entity IDs, selects a default seed, and increments the content revision for
   replay-relevant changes.
2. Starting a run resolves that authored data, chooses the default seed unless
   a deliberate override is supplied, records provenance, and creates fresh
   run state at tick zero.
3. The simulation advances mutable run state, records ordered player commands,
   and emits immutable causal events. Presentation only observes those results.
4. A replay uses the recorded scenario identity, revision, seed, compatibility
   data, and inputs. It starts only when compatibility validation succeeds.

## Deferred implementation

This is a design boundary, not a request to add runtime classes, save files,
replay UI, or automatic content hashing. The first implementation should be
planned after the agent-state, event-log, and spatial-world foundation notes
define the state those systems require.
