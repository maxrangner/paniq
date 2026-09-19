# Movement and spatial-world rules

**Status:** decided foundation. This note defines the logical ground plane,
authored world constraints, occupancy, and basic movement resolution. It does
not define navigation, hazards, player intervention, or autonomous decisions.

## Purpose

The simulation needs one authoritative answer to where a participating agent
is, whether it has room to move, and which move wins when two agents compete
for space. Keeping these answers in logical simulation data makes movement
replayable even when the displayed Unity scene is delayed, recreated, or styled
differently.

This note builds on the [simulation contract](simulation-contract.md),
[scenario data and runtime state](scenario-runtime-state.md), [agent state
model](agent-state-model.md), and [causal event log](causal-event-log.md).
Those notes remain the authority for ticks, stable IDs, event records, and the
boundary between simulation and presentation.

## Logical world and authored constraints

The first spatial world is a flat, ground-bound XZ plane. A logical position is
an ordered pair of integer millimetres: `X` and `Z`. There are **1,000 logical
units per Unity metre**. Every authored coordinate, including boundary and
obstacle edges, must be within **-100,000 to 100,000 mm** on each axis.
Logical positions, displacements, boundaries, and obstacle bounds use these
integer units; simulation movement must not read or write a Unity `Transform`.

Each authored scenario supplies these read-only spatial values:

| Value | Meaning |
| --- | --- |
| World boundary | One finite, axis-aligned rectangle containing all valid agent occupancy. |
| World constraints | Zero or more axis-aligned rectangular obstacles. Each has an opaque, stable constraint ID. |
| Occupancy radius | One positive circular radius, shared by every participating agent, no greater than 2,000 mm. |
| Maximum step distance | One non-negative maximum distance a participating agent may request in a logical tick, no greater than 1,000 mm. |
| Initial positions | One logical position for every initially participating agent. |

The boundary and obstacles are authoring data only. Runtime code cannot alter
them. The boundary is inclusive of an agent's full circular footprint; an
agent's footprint must not extend beyond it. An obstacle blocks an agent when
the circular footprint intersects its interior. Exact touching is permitted.

## Spatial runtime state and occupancy

Every participating agent has one mutable logical position in run state, keyed
by its stable Agent ID. Its occupancy is the shared-radius circle centred on
that position. The run's occupancy is derived from those participating-agent
positions; it is not an authored list and does not use Unity object references.

The simulation creates the initial positions at tick zero from the authored
scenario. A newly created participating agent receives a valid logical position
through the deterministic creation path before it can submit movement. When an
agent becomes `NoLongerParticipating`, its retained `AgentState` remains
available for replay and debugging, but its circle is immediately removed from
occupancy.

Two participating-agent circles must not overlap. A scenario is invalid if its
initial positions violate the boundary, obstacle, or overlap rules.

| Choice | Rationale |
| --- | --- |
| 20 ms logical tick | Matches the contract's 50 ticks per second while keeping a small, inspectable simulation step. |
| Integer millimetres | Converts cleanly to Unity metres while avoiding floating-point state for this local-world foundation. |
| 200 m coordinate span | Accommodates compact prototype rooms while bounding integer collision calculations. |
| Shared footprint and authored step | Keeps early occupancy and movement rules understandable; varied sizes and speeds remain later work. |

Spatial implementations must calculate coordinate differences and every
collision or sweep intermediate in checked `long` arithmetic. They must use
integer comparisons throughout: floats, Unity physics, and `Transform` values
cannot decide a logical collision. The authored limits above make the required
squared-distance and sweep comparisons representable in `long`; invalid input
fails scenario validation rather than wrapping.

## Movement requests and resolution

A future autonomous system may submit at most one `MovementRequest` for each
participating agent in a logical tick. A request contains only the stable Agent
ID and an integer XZ displacement. Submitting a request is not a movement
decision system; later agent work defines why and how a displacement is chosen.

At the start of movement resolution, the spatial system sorts the participating
Agent IDs in ascending order. With `N` participating agents, it starts at index
`tick mod N` and visits that circular order once; `N = 0` has no requests to
resolve. This rotates the first conflict opportunity without storing extra
state. It applies only to movement resolution; agent decisions remain in
ascending Agent ID order.

The spatial system resolves requests in that circular order:

1. Reject a request from a non-participating or unknown agent, a second request
   from the same agent in the tick, or a displacement whose squared length is
   greater than the shared maximum step distance squared.
2. Sweep that agent's circle from its current position to the requested
   destination. Accept the request only if the full sweep and destination stay
   inside the boundary and avoid obstacle interiors and all currently occupied
   participating-agent circles.
3. On acceptance, update the agent's logical position immediately. On
   rejection, leave it at its prior position for this tick.

Because accepted moves update occupancy before the next request is considered,
the earlier request in the circular order wins a conflict for the same free
space. Agents cannot pass through an occupied agent, trade positions, or
receive a second attempt later in the same tick. The system does not slide
along obstacles, choose an alternate route, interpolate logical movement, keep
velocity, apply acceleration, or teleport agents.

## Events and presentation

When a future movement system emits a causal event, the event's position uses
the same integer-millimetre logical position defined here. This note defines no
movement event types or effects.

Presentation may convert a logical position to Unity metres, interpolate a
displayed object between authoritative positions, and add visual style. It
cannot change logical positions, occupancy, requests, or collision outcomes.

Coordinate units and limits, constraint semantics, occupancy radius, movement
step limit, integer collision rules, and circular resolution order are
replay-relevant. Changes follow the [simulation compatibility policy](simulation-contract.md#simulation-compatibility-policy).

## Deferred work

Pathfinding, navigation meshes, steering/avoidance, slopes, vertical positions,
multi-level spaces, variable footprints, agent-specific speeds, hazards, and
player interventions remain deferred. Revisit the shared-radius and
maximum-step defaults only when a tested scenario demonstrates that they block
the intended experience.

When spatial runtime code is introduced, its edit-mode tests must cover swept
obstacle contact, boundary contact, circle touching semantics, agent occupancy,
and numeric limits. The current prototype is obstacle-free; those focused
tests validate obstacle semantics without introducing navigation requirements.

## Fire-reaction prototype notes

The [fire-reaction prototype](fire-reaction-prototype.md) follows these rules
with two documented extensions. First, each agent has its own seeded speed,
always within the shared maximum step. Second, the agent's own steering picks
a valid displacement before submitting it: it keeps the along-wall part of a
step at the boundary, or tries a small side-step around a person. The resolver
itself still never slides, reroutes, or retries. The swept-circle test uses the
exact point-to-segment distance (`IntegerMath.SegmentPassesWithin`), which is
correct for moves in any direction, not only along the axes.

**Doorways.** Each wall may have doors. A closed door is wall. An open door adds
a walkable strip as wide as the door, from 1 m inside the wall to 2 m outside
it. Only a person heading for that door, or already outside the room, may use
the strip, so calm people still treat every door as wall. A destination is
valid when the whole footprint fits in the room or in a strip the person may
use, and the sweep never passes within one body radius of either door-frame
corner. A person 0.8 m or more outside the wall, lined up with an open door,
has escaped and leaves occupancy at once.

**Physical objects.** Boxes are round footprints (diameter = box width) that
also occupy space: a person's sweep may not pass through one, and a box's sweep
may not pass through a person or another box. Box positions keep hundredths of
a millimetre so slow slides do not round away, but every overlap test uses
whole millimetres. Objects stay inside the room and treat doorways as wall.

## Resolution examples

- A participating agent requests a within-limit displacement whose swept circle
  stays clear: its logical position changes at the tick.
- A request that crosses the boundary or an obstacle is rejected: the agent
  remains at its previous logical position.
- Two agents request the same currently free location: the earlier request in
  that tick's circular order is accepted, and the other is rejected because the
  location is occupied.
- When an agent becomes `NoLongerParticipating`, its logical position remains
  available as historical run state, but another participating agent may later
  occupy that released space.

## Prototype extension: tables

The fire-reaction prototype adds fixed tables to the room. A table is an
axis-aligned rectangle in scenario data. A person's footprint may not overlap
the rectangle grown by the person's radius; the movement rules above apply
unchanged, with tables treated as extra walls when choosing and resolving a
step. `WorldGeometry` is the only code that knows where tables are.
