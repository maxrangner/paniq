# Movement and spatial-world rules

**Status:** decided foundation, amended by the physics engine (2026-09-21) and
checked against the code on 2026-09-26. This note defines the logical ground plane,
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
agent becomes `NoLongerParticipating`, its retained `Agent` record remains
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
cannot decide a logical collision. **Amended 2026-09-21 (compatibility version
32):** Unity's 3D physics engine now decides how bodies move and touch, inside
the run's own physics world and stepped by the simulation. See "Bodies in 3D"
at the end of this note. Positions are still read back as integer millimetres,
and every rule below that reads positions (rooms, doors, fire, sight, sound)
still works in whole numbers. The authored limits above make the required
squared-distance and sweep comparisons representable in `long`; invalid input
fails scenario validation rather than wrapping.

## Movement requests and resolution

**History (2026-09-21).** The foundation moved people by a sweep-and-refuse
rule: one integer displacement per person per tick, resolved in a rotating
circular order, accepted only if the swept footprint stayed inside the walls
and clear of everybody else, never sliding or retrying. People and things are
now physical bodies that push each other (see "Bodies in 3D"); what survives
of that rule is its fixed resolution order, which the physics step keeps.

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

## What was deferred, and where it went

This note deferred pathfinding, steering, varied speeds, varied footprints,
vertical positions and several storeys. As of 2026-09-26:

- **Finding the way** is built: the floor is a grid of 250 mm squares
  (`NavigationGrid`), and people follow flow fields (`FlowField`,
  `Navigation`) round furniture and across the building; see "Several rooms"
  below and the roadmap's foundations rebuilt.
- **Steering and pushing** are the physics engine's: see "Bodies in 3D".
- **Speeds** vary per person, from the speed trait
  (see the [agent state model](agent-state-model.md)).
- **Height** is real for bodies (see "Bodies in 3D"), but the floor plan is
  still one flat storey. **Storeys are decided, not built**: a room or a
  position may carry a storey number and a stair is a kind of door between
  storeys, and nothing new may assume one storey
  ([design constraints](technical-decisions.md#design-constraints-for-future-expansion)).
- **Footprints** are still one shared radius for every person.

## Before the physics engine

Until 2026-09-23 this note also described doorway strips, a swept-circle
movement test and round box footprints. Those rules were replaced by the
physics bodies below and their code was deleted in the review refactor; the
old text is kept in [history](history/spatial-rules-before-physics.md).

## Prototype extension: tables

The office level adds fixed tables to the room. A table is an
axis-aligned rectangle in scenario data. A person's footprint may not overlap
the rectangle grown by the person's radius: the physics step keeps bodies out
of it, and the navigation squares under it are not walkable. `WorldGeometry`
is the only code that knows where tables are.

## Prototype extension: several rooms

A building is a set of axis-aligned rectangular rooms that never overlap. Two
rooms that share a wall line are joined by a door set in it; a door with no
room beyond it leads outside, and only such a door can be escaped through. A
footprint wholly inside any room is walkable, and an open door's walkable
strip joins the rooms on either side of it. `WorldGeometry` numbers the rooms
so fire, sight and sound can respect walls, and answers "how do I walk from
this room to that one" by searching the rooms as a graph, with each door
costing the distance from the door walked in through to the door walked out
of. The first room is the open-plan office; where a fire may start is the
level's to say (on the office level, the meeting room).

A scenario is refused if two rooms overlap, if a door names a room that does
not exist, or if a door would open half into a room and half into its wall.

## Prototype extensions

These are rules the office level added on top of the foundation
above. They are recorded here because they change what the shape of the world
means, not just what happens in it.

**A thing resting in a doorway jams the door.** A loose object in front of a
door's gap, within its own radius plus a small clearance of the wall line on
either side, stops that door opening *and* stops it shutting. It is worked out
once at the end of each tick, after every object has finished moving, so the
decisions in the following tick read a settled answer. Fire, sound and sight are
deliberately unaffected: a cardboard box does not stop flames or shouting, so the
geometry never needs to know about objects.

**A smashed table stops being an obstacle.** Tables are fixed rectangles that
people, objects and route choices all keep out of. A table that has been broken
is flagged, and from then on every one of those queries skips it: the floor it
stood on becomes walkable, and routes may cross it. This is the one thing in the
prototype that changes the shape of a room during a run.

**An opening may appear during a run.** A blast hole is not a new kind of thing:
it is one of a fixed number of spare door slots the scenario reserves, filled in
at the moment a charge is spent and set permanently open. Walkability, route
finding, fire spread, sound, sight and escaping all ask about doors, so they pick
a hole up with no rules of their own. Two consequences are load-bearing. A spare
slot that has not been placed must be skipped by *every* loop over doors,
because an unplaced slot reads as a door leading outside and would be routed to.
And placing one must rebuild exactly the two things the geometry caches per door —
what lies beyond it, and which doors touch which room — through the same code the
constructor uses, because the order doors appear in per room decides the order
behaviours consider them.

## Bodies in 3D (2026-09-21)

Since compatibility version 32, people and loose things are solid 3D bodies in
Unity's physics engine (PhysX), in a physics world that belongs to one run and
that nothing in the displayed scene can reach. The simulation steps it once per
tick. The [simulation contract](simulation-contract.md) gives the order.

What that changes about space:

- **Bodies can overlap a little, for a moment.** Two people squeezed in a
  doorway may be pressed a few centimetres into each other. The engine eases
  them apart at no more than 2 m/s. The tests allow up to 75 mm for up to two
  ticks and fail anything deeper or longer.
- **Height is real.** Things rest on tables and on each other, fall off, fly in
  arcs and land. A person knocked down lies flat along the floor. Where there is
  no room to lie, they stay on their feet in the physics ("crumpled") until they
  get up.
- **A person's position is the middle of their body.** For someone lying down,
  that is about a metre from their feet. Rooms, doors, fire, sight and sound all
  read that middle.
- **Walls are solid slabs 40 mm thick and 3 m high.** A shut door fills its
  gap; an open door, a blast hole or a spare slot not yet placed does not.
  Tables are fixed blocks until smashed, when they are taken out of the world.
  The world has a floor, a ceiling and a fence well outside the building, so
  nothing can fall out of it.
- **What was in the way is measured, not assumed.** A door will not close on
  any body in its doorway, standing or lying. Somebody getting up looks for a
  clear spot the engine confirms is empty, and never one through a wall.
- **Everything else still works in whole numbers.** After each step, positions,
  headings and speeds are rounded back to 1/100 mm, whole degrees and ticks, so
  navigation, perception and decisions are unchanged.
