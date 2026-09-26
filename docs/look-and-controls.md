# Look and controls

**Status:** the controls below are **built** as of prototype 2. The camera
moves, swings freely under a right-button drag, snaps a quarter turn on Q and
E, and zooms with a coupled tilt, exactly as this note describes. The look is partly built: the building now reads as one
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
  more it looks down on the building. The tilt does not start straight away —
  the first half of the wheel's travel comes **straight in** at the isometric
  angle, and only the second half swoops down toward the floor. Tilting from
  the very first notch made a small zoom feel like a lurch rather than a step
  closer.

One honest consequence: once the tilt moves off 35.264 degrees, the picture is
no longer isometric in the strict sense. That is accepted. Isometric here
describes the look the game settles at, not a rule the projection obeys at
every zoom level.

## The controls

| Input | What it does |
| --- | --- |
| **W A S D** | move the camera forward, back, left and right across the building |
| **Hold right mouse button and drag** | swing the view to any angle at all; it stays where you let go |
| **Q / E** | snap a quarter turn to the next corner view, from wherever the view is now |
| **Mouse wheel** | zoom in and out, tilting the camera as described above |
| **Left mouse button** | on a card along the bottom: pick it up (or put it down again); on the floor with a card in hand: throw it there |
| **Click a door, a thing or the floor** (nothing in hand) | influence (2026-09-26): one step of pull toward it, drawing people near it -- in the same room, within about twelve metres, more the nearer they are -- to go there or use it. Each click adds a step, up to twenty, so click frantically for a strong pull; it loses a step every two seconds and cannot be taken back. A sparkling aura shows it, and a sparkling line runs to everybody feeling it, brighter the harder they are pulled |
| **Click a person** (nothing in hand) | nudge them: they step away from where the click landed. Three nudges in ten seconds and they are annoyed -- they shake with it, and for about twenty seconds more nudges do nothing |
| **Click a red pull station** | on the office, only people pull alarms (2026-09-26): a click draws people to it instead, and the brave among them may pull it. A level that lets the player pull alarms pulls it |
| **Hold the button down on a door** | a hand on it (prototype 3): an open door pulls shut as soon as the doorway is clear, and nobody opens it while you hold it. Somebody strong enough bursts through in one push, and then there is nothing left to hold. A locked door needs no hand, and swing doors and gaps take none. Let go of the button and it is a door again |
| **Right click a door** (without dragging) | turn its key: unlock a locked one (this is how the way out is opened), lock a shut one, or shut and lock an open one |
| **Right click** elsewhere (without dragging) | put down the card in hand |
| **Reset** (the button top right) | back to the start card at once, keeping the seed, from anywhere in the round or from the end card |
| **Pause** (the button under Reset) | stop and start the world, as Space does |
| **Trigger event** (the red button bottom centre) | start the fire. It goes the moment it is pressed |

Chosen on the owner's behalf:

- **W always moves the camera up the screen**, whichever corner the view is
  currently from. Moving relative to the world instead would mean W changed
  direction every time the player pressed Q, which is disorienting.
- Rotation is **free under a drag and snapped under Q and E**. It used to be
  snapped only, which kept the look consistent but meant a thing hidden behind
  a wall could not be leaned around — the nearest corner view was as close as
  the player could get. Dragging does **not** spring back to a corner when it
  is let go: springing back would undo the one thing the drag is for. The four
  corners remain as somewhere tidy to land, and Q and E measure from wherever
  the view is pointing, so a quarter turn always arrives on one however far a
  drag has wandered.
- **A right click puts a card down; a right drag turns the view.** The two are
  told apart by how far the pointer travelled before the button came back up,
  which is why the card is dropped on the button's *release* rather than on its
  press — at the moment of pressing, nobody yet knows which one it is.
- **A click on a door is sent when the button comes back up** (2026-09-26).
  There is no double click any more -- the key moved to the right button so
  that frantic clicking stacks up influence rather than turning the key -- so
  nothing has to wait for a second click. A press let go of inside a third of
  a second is a click; one still down when the third of a second is up is a
  hold. Until 2026-09-26 a click opened or shut the door and a double click
  turned its key; people open and shut doors themselves now.
- **A click on a card or a button never reaches the world.** Every card and
  button claims its patch of screen as it is drawn, and the next frame's
  click checks those patches first; until 2026-09-25 a click on "Trigger
  event" with a door under it clicked the door too.
- **The number keys are gone** (2026-09-25). Cards are clicked, and two of a
  kind sit as one card with the count on it, so there is nothing for a
  number to name.
- **A press that outlasts the window is a hold** (prototype 3,
  2026-09-25). The same third of a second decides both: a button up again
  inside it is a click, a button still down when it closes is a hand on the
  door, and the press that began the hold is never a click. The line under
  the score says a door is held only once the run has
  taken the hold, so a door that cannot be held never claims to be. Pausing
  lets go of a held door, because nothing pressed while the world is stopped
  reaches it. On this level nothing costs anything: the office has no purse (the
  owner's call), so the prices the lines above used to quote are gone from
  the screen.
- **A nudge is a click, never a hold**, and it goes to whoever is drawn
  nearest the pointer on the screen, the way a card used to be aimed. A
  door or a pull station under the pointer wins over a person behind it. It
  comes from where the pointer meets their body at chest height, pulled back
  a little toward the camera, so a click on someone's left side sends them
  right and one in the middle sends them away from the camera.
- **A click near a patch already influenced adds to it**, whoever is walking
  under the pointer (2026-09-26): within a metre of a patch of floor or a
  thing with influence on it, the click is influence and not a nudge.
  Otherwise the order is: a person, then a thing drawn nearest the pointer,
  then the floor itself.
- The angles, zoom limits and how far the tilt travels are presentation values,
  chosen when the camera was built and listed below.

Keys the prototype already uses, which these live alongside: **Tab** shows
the table of everyone's traits, **G** paints the floor people can walk on,
and **Space** pauses.

The values chosen when the camera was built, recorded in
[technical decisions](history/decisions-prototype-2.md#prototype-2-decision-building-the-round): the view pans at 14 metres a
second and more slowly the closer it is zoomed, the wheel zooms in twelve
notches from the whole building down to about a sixth of it, and the tilt
travels from 35.264 degrees at full zoom-out to 18 degrees fully in — held flat
for the first half of the wheel and eased the whole way down over the second.
A drag swings the view a quarter of a degree per pixel, so a full turn is about
a screen and a half of travel, and five pixels of travel is the line between a
click and a drag. The view can be pushed about eight metres past the building's
edge and no further.

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
- **Animation is minimal.** Walking is a waddle rather than a stride: the body
  hops on each footfall and rocks and twists onto alternating feet, so a
  capsule with no legs still reads as somebody taking steps. The look wanted is
  a person play-walking a doll across a table, not a gait, so it is deliberately
  larger than a real walk. It fades out with speed, and anybody staggering,
  alight, frozen or sitting keeps their own movement instead. This means
  movement is animated procedurally — the transform is moved, bounced and
  rocked in code, as the fire cubes already are — with **no character rigs, no
  Animator controllers, and no animation packages**.

Standing rules from the owner, not to be undone by a later change:

- **A seated person looks exactly like a standing one, only higher.** In the
  owner's words (2026-09-24): "Seated person looks exactly the same as
  standing. No leaning, no squashing, no deforming. They should be higher off
  the ground than a standing person, representing them sitting on the flat part
  of the chair." A seated head about half a metre above a standing one is
  accepted.
- **Signs stand upright**, as signs on a wall do (the owner's request; they
  used to lie flat to face the camera).
- **The play view stays clean** (the owner, 2026-09-19: "Let's try to keep
  gameplay clean"). Over a head go the person's number and the marks that say
  what they are doing (the `!`, the snowflake, the star); traits and state go
  in the panel **Tab** opens. No always-on trait bars.

What prototype 3's second batch added to the picture (2026-09-26):

- **Influence glows.** Every influenced door, thing or patch of floor has a
  pale gold ring that breathes and flickers and throws off little sparks --
  faint at one click, wide and bright at twenty -- and everybody feeling a
  pull has a thin gold line from their chest to it, shimmering toward the
  place, faint for a gentle pull and bright for a strong one. The owner asked
  for "a glowing sparkling aura" and "a glowing sparkling line between object
  and agent, faint first, more intense the more influence". Gold rather than
  the orange of fire or the blue of a held door, so none of the three is
  mistaken for another. The lines are the answer to "is my influence doing
  anything?": without them, "more likely to go there" is invisible.
- **The annoyed shake.** Somebody nudged three times quickly huffs from side
  to side on the spot, bigger than the tremble of the frozen, for as long as
  they are annoyed.
- **A socket about to go crackles.** For the five seconds before the Director
  pops a socket or the fuse box, it spits little showers of bright sparks,
  faster as the moment nears, with a ring on the floor for the crackle.

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
