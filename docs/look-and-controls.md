# Look and controls

**Status:** the controls below are **built** as of prototype 2. The camera
moves, turns a quarter at a time and zooms with a coupled tilt, exactly as
this note describes. The look is partly built: the building now reads as one
floor of an office tower, and the two *Future goals* at the bottom — a slight
perspective with depth of field, and the cutaway walls — are still parked.

The decisions themselves were settled with the owner; the ones with technical
consequences are recorded in [technical decisions](technical-decisions.md).

## The view

The player looks at the building **from a corner**. Two walls recede away to
the left and two to the right, the way a model on a table is photographed. That
corner-on framing is what "45 degrees" means here: it is the compass direction
the camera looks from, not how high above the floor it sits.

- **Height:** the camera sits at the classic isometric angle when zoomed out.
  The prototype uses 35.264 degrees above the ground, which is the angle that
  makes a step north and a step east cover the same distance on screen.
- **Projection:** orthographic — no perspective. Parallel lines stay parallel,
  and a desk in the far corner is drawn exactly the same size as one at the
  front. A slight perspective is a **future goal**; see *Future goals* below.
- **Zoom** pulls the camera in and out, and **tilts it as it goes**: the closer
  the view, the more the camera looks along the floor; the further out, the
  more it looks down on the building.

One honest consequence: once the tilt moves off 35.264 degrees, the picture is
no longer isometric in the strict sense. That is accepted. Isometric here
describes the look the game settles at, not a rule the projection obeys at
every zoom level.

## The controls

| Input | What it does |
| --- | --- |
| **W A S D** | move the camera forward, back, left and right across the building |
| **Q / E** | rotate the view a quarter turn, snapped — so there are four views, one per corner |
| **Mouse wheel** | zoom in and out, tilting the camera as described above |
| **Left mouse button** | interact with the world under the pointer |

Chosen on the owner's behalf:

- **W always moves the camera up the screen**, whichever corner the view is
  currently from. Moving relative to the world instead would mean W changed
  direction every time the player pressed Q, which is disorienting.
- Rotation **snaps** rather than sweeping freely, so the view always returns to
  a corner and the look stays consistent.
- The angles, zoom limits and how far the tilt travels are presentation values,
  chosen when the camera was built and listed below.

Keys the prototype already uses, which these live alongside: **1–4** pick a
card, **Tab** shows the table of everyone's traits, **G** paints the floor
people can walk on, and **Space** pauses.

The values chosen when the camera was built, recorded in
[technical decisions](technical-decisions.md): the view pans at 14 metres a
second and more slowly the closer it is zoomed, the wheel zooms in twelve
notches from the whole building down to about a sixth of it, and the tilt
travels from 35.264 degrees at full zoom-out to 18 degrees fully in. The view
can be pushed about eight metres past the building's edge and no further.

## The look

**A doll's house.** The building should read as a small, crafted model of a
place rather than a real one — the diorama feeling, where the whole scene is
something you could pick up.

- **Models are crude and simple.** No detailed characters, no realistic
  proportions.
- **Depth of field** is wanted to strengthen the miniature feeling: blurring
  the very front and very back of the scene is what makes a photograph of a
  real street look like a toy. This is blocked for now by the orthographic
  camera and is part of the future goal below.
- **Animation is minimal.** Walking is a bob rather than a stride. This means
  movement is animated procedurally — the transform is moved and bounced in
  code, as the fire cubes already are — with **no character rigs, no Animator
  controllers, and no animation packages**.

Concept art is to be added under `docs/reference/` as it is produced. Nothing
is there yet.

## Walls and what they hide

**Today:** people and loose objects behind a wall show through it as pale blue
silhouettes, and the silhouette paints only where a wall or door actually hides
something.

**Future goal — the cutaway doll's house.** The walls between the camera and
the room simply are not drawn. As the player presses Q or E, whichever walls
are now in the way drop out and the rooms behind them stand open, exactly as a
doll's house has one side missing.

Things mounted on a dropped wall **stay**: windows, fire extinguishers, fire
alarms and wall sockets remain where they were, so the room keeps the objects
that matter to play even when its wall is gone.

One thing that will need doing first: doors already record which wall they sit
in, but alarms — and the other wall-mounted objects — carry only a position.
They will need to know their wall before the cutaway can decide what to hide
and what to keep.

## Future goals

Parked deliberately, in the order they are likely to matter:

1. **A slight perspective, and depth of field with it.** A camera placed far
   back with a long lens looks almost identical to the orthographic view at a
   glance, but distant parts converge slightly and the miniature blur works
   properly. This is what would deliver the doll's-house feeling in full.
2. **The cutaway walls** described above.

Both are small changes to presentation only. Neither touches the simulation,
so neither is urgent and neither is risky.
