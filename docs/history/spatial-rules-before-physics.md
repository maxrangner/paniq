# Spatial rules before the physics engine

Moved here unchanged from [movement and spatial-world rules](../spatial-world-rules.md)
on 2026-09-26. These rules were replaced by physical bodies on 2026-09-21 and
their code was deleted in the review refactor on 2026-09-23.

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

The [fire-reaction prototype](../the-office-level.md) follows these rules
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
whole millimetres. Objects stay inside the room and treat doorways as wall (they never pass through one), but an object may come to rest *in* a doorway, against the wall line, and one that does jams that door.

