# Simulation contract

**Status:** decided foundation. This note defines the rules shared by every
future simulation system. It deliberately does not define agent decisions,
hazards, movement, interactable behaviour, or player powers.

## Purpose

Paniq should produce surprising crowd-survival situations that players can
understand and improve on. A run must therefore be replayable and explainable:
the same starting scenario and the same player actions must lead to the same
simulation outcome within the supported determinism boundary.

This contract is the authority for simulation time, random choices, identity,
causal events, and the boundary between simulation and presentation.

## Time

Gameplay is real time, but simulation advances as an ordered sequence of
logical ticks. The initial fixed step is **0.02 seconds**, or **50 ticks per
second**.

- Unity's `FixedUpdate` supplies the runtime cadence. Each simulation tick has
  a monotonically increasing tick number and uses the fixed step, never the
  elapsed time of a rendered frame.
- Unity can execute zero, one, or several fixed updates around a rendered frame
  as it catches up. This changes when the player sees a result, but must not
  change which ticks run or the result they produce.
- A change to the fixed step is a simulation-compatibility change. It requires
  a documented reason, replay review, and tests before it can be adopted.

Unity documents `FixedUpdate` as the entry point for fixed-timestep work and
notes that multiple fixed updates can run when frame rendering is slower than
the fixed cadence. [Unity fixed updates](https://docs.unity3d.com/6000.3/Documentation/Manual/fixed-updates.html)

## Randomness

Every scenario has an explicit, persisted unsigned 64-bit seed. Starting a run
initializes one simulation-owned **PCG32 XSH-RR** deterministic random-number
generator from that seed. The generator uses the PCG reference initialization
procedure with the scenario seed as `initstate` and a fixed `initseq` of `54`.

- Simulation systems obtain all random values only from that generator.
- Derived streams are not allowed in the foundation. A later stream design must
  name its derivation algorithm, state ownership, and compatibility effect
  before it is introduced.
- Simulation code must not use Unity's global `UnityEngine.Random`, wall-clock
  time, rendering-frame count, or presentation state to choose an outcome.
- Visual, audio, and UI code may use presentation-only variation, but may not
  read, seed, restore, or advance the simulation generator.
- The generator's 64-bit state and stream selector are part of runtime
  simulation state when a future save, replay checkpoint, or debugger needs to
  resume a run.
- A future implementation must verify the PCG reference vector: initializing
  with `initstate` 42 and `initseq` 54 produces, in order,
  `A15C02B7`, `7B47F409`, `BA1D3330`, `83D2F293`, `BFA4784B`, and
  `CBED606E` as unsigned 32-bit hexadecimal outputs.

Paniq owns the algorithm rather than using `System.Random`: .NET does not
guarantee that the same seed produces the same sequence across major versions.
The PCG reference provides the small, specified PCG32 algorithm and seeding
procedure. [System.Random compatibility](https://learn.microsoft.com/en-us/dotnet/api/system.random#notes-to-callers)
and [PCG reference implementation](https://www.pcg-random.org/download.html).

## Stable identity

Every simulated agent, hazard, and interactable has a stable value ID. IDs are
opaque data: callers may compare, store, serialize, and place them in events,
but must not infer behaviour or location from their value.

- Scenario authoring assigns or records the IDs needed to recreate its initial
  entities. Runtime-created entities receive IDs through the simulation's
  deterministic creation path.
- Simulation state, commands, and events refer to an entity by its stable ID,
  never by a Unity `GameObject`, `Component`, `Transform`, or other scene-object
  reference.
- Presentation keeps its own temporary mapping from stable IDs to Unity scene
  objects. Destroying or recreating a displayed object must not change the
  identity or outcome of its simulated entity.

## Deterministic processing order

Whenever processing order can affect simulation state, random draws, emitted
events, or conflict outcomes, systems must use an explicit deterministic order.

- Process entities in ascending stable-ID order. A later system may define a
  different order only when it documents a deterministic tie-breaker.
- Process events in ascending tick, then ascending Event ID. Event IDs must be
  allocated through the same deterministic processing path.
- Dictionaries and hash sets may support lookup, but their enumeration order
  must never determine simulation processing order. Copy and sort their IDs or
  use an already ordered collection before processing.
- When two outcomes otherwise have equal priority, the lower stable ID wins
  unless the responsible system documents another deterministic rule.

One simulation runner owns tick advancement. Simulation systems must not each
advance themselves from separate `MonoBehaviour.FixedUpdate` callbacks; Unity
component execution order is not simulation order.

Within phase 4, a person's behaviour only states what it wants the body to do
this tick (a goal heading, a goal speed, a turn rate and an acceleration).
Locomotion carries that out once per person per tick. Other systems may stop,
knock down or jolt a body as the direct result of a logged event, but no
behaviour turns or accelerates a body itself.

The tick schedule is, in order:

1. Consume commands assigned to this tick.
2. Advance hazard state.
3. Resolve hazard contact at current positions.
4. Make agent decisions.
5. Resolve movement requests.
6. Resolve danger contact along accepted movement and then exit outcomes.
7. Resolve collisions, in the order they were recorded during phase 4: people
   into people first, then people into physical objects.
8. Advance physical objects (such as boxes), in ascending object ID order.

A phase that no prototype stone uses yet is simply empty. In the fire-reaction
prototype, phase 1 consumes door clicks and phase 6 marks people who have
walked out through an open door as escaped. Collisions (phase 7) never move
anyone: a collision is a move that was refused in phase 4, so they cannot
change the contact results of phase 6. A collision with an object only changes
that object's velocity, which phase 8 then applies. Phase 8 moves objects but
never people; an object that runs into a person stops against them and may
stagger or trip them.

Sounds are delivered synchronously when they are emitted, to listeners in
ascending Agent ID order, like any other event-driven transition.

Each system emits and appends its events synchronously with the transition that
caused them. A later system must document where it belongs in this schedule
before it can affect simulation state.

## Player commands

Presentation captures a player action during Unity's dynamic update, converts
it to logical simulation data, and appends one immutable `PlayerCommand` to the
run's command queue. It assigns the command to the next logical tick that has
not started. The queue allocates a monotonically increasing command sequence
number; commands for one tick are consumed in that sequence order at the first
schedule phase above.

| Field | Meaning |
| --- | --- |
| Target tick | The logical tick at whose start the command is consumed. |
| Command sequence | The run-wide monotonic ordering value for commands sharing a tick. |
| Command type | A named data-level action, such as placing a guidance marker. |
| Logical payload | Fully quantized, validated simulation data needed by that command: a thing's stable ID, a place in whole millimetres, or both. |

Raw pointer positions, camera state, screen coordinates, Unity input objects,
and scene-object references are not command data. They may help presentation
derive the logical payload for a live action, but replay consumes only the
recorded `PlayerCommand`.

The first command type is `ClickDoor`, whose payload is one door's stable ID.
The display finds which door was under the mouse pointer with a ray-cast
against a click-only collider on each door leaf, then hands the runner that
door ID. The simulation keeps every queued command in order (`Commands`), and
queuing the same commands on a fresh run with the same seed replays it exactly.
A command for a tick that has already started is rejected.

A command may name a **place** instead of a thing: a `LogicalPosition` in whole
millimetres, which is fully quantized simulation data and so a valid payload.
The display works one out by intersecting the pointer with the mathematical
ground plane and rounding; the ray and the screen position never leave the
presentation. The prototype's cards use both shapes — `PlayBeefcake` names a
person, and `SpawnFire`, `SpawnExtinguisher` and `BlastWall` name a place.

The queue belongs to `PlayerCommandSystem`, which holds it but decides nothing:
each command is carried out by the system that owns those rules. Validation
splits in two. Whether a command *names something the run has* is checked as it
is queued, and an unknown ID is refused outright. Whether a command *can do
anything where it points* is decided when it is consumed, because the world will
have moved on by then; a command that cannot is a no-op that costs the player
nothing and, because the causal log is append-only, leaves no trace in it.

Unity's Input System supports dynamic and fixed update processing. Paniq uses
dynamic capture and explicitly queues logical commands, so rendering cadence
cannot change their simulation ordering. [Unity Input System update modes](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Settings.html)

## Causal events

Simulation systems communicate outcomes with immutable event data, rather than
directly calling or holding references to scene objects. Each event record
contains:

| Field | Meaning |
| --- | --- |
| Event ID | A stable identifier for this event within the run. |
| Tick | The logical tick that produced it. |
| Source ID | The stable ID of the entity that caused it. |
| Event type | A named, data-level description of what happened. |
| Position | The logical world position at which it happened. |
| Strength | An optional numeric magnitude. An event type defines when it is present and what it means. |
| Duration | An optional logical-time duration. An event type defines when it is present and what it means. |
| Causal parent | The Event ID that directly caused it, or no parent for a root event. |
| Target ID | The stable ID of the entity it affected (the person run into, the box kicked, the door tried), or none. An event type defines when it is present. |

Receivers use these fields and their own simulation state to process the event.
The causal-parent chain is retained so a later event log and debugging view can
explain an outcome back to its origin. Defining event types and their mechanics
belongs to the systems that introduce them; this contract only fixes the shared
envelope.

## Simulation and presentation boundary

The simulation is authoritative: it owns runtime state, advances logical
ticks, consumes simulation randomness, accepts ordered player commands, and
decides outcomes. Presentation is observational: visuals, audio, and UI read
simulation snapshots and events to show those outcomes.

Presentation code must not decide simulation outcomes, mutate simulation state,
or consume simulation randomness. It can be delayed, skipped, replayed, or
recreated without changing a run's simulation result.

## Determinism boundary

Paniq guarantees a reproducible run only when all of the following match:

- scenario data and its explicit seed;
- Paniq build and platform;
- fixed-step configuration; and
- the ordered player-input data supplied to each logical tick.

Bit-identical replays across different builds or platforms are not promised by
this foundation and are explicitly deferred. Any future extension of that
guarantee must identify and control the relevant numeric, engine, and content
compatibility differences.

## Simulation compatibility policy

The simulation compatibility version identifies the interpretation of every
replay-relevant rule. Changing the fixed step, command envelope, PCG algorithm
or initialization, tick schedule, event-envelope meaning, numeric spatial
rule, or documented system-specific replay field requires a compatibility
review and a new version unless a documented migration preserves old runs.

At save, checkpoint, or replay load, the game compares the provenance record
with the available scenario and runtime environment. A mismatch is
**incompatible**: the game reports it and never silently converts,
approximates, or substitutes simulation data. Supporting design notes list
their relevant fields; this section owns the policy.

## Next layers

The foundation notes built on this contract are
[scenario data versus runtime state](scenario-runtime-state.md), the [agent
state model](agent-state-model.md), [causal event logging and debugging](causal-event-log.md),
and [movement and spatial-world rules](spatial-world-rules.md). Prototype
stones, recorded in the [prototype roadmap](roadmap.md), add their own
mechanics without weakening the replay and separation guarantees above.
