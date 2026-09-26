# Making models: from a description to a thing in the game

**Status: implemented and in use (2026-09-26).** The pipeline is merged into
`main` and has made its first model, the wet-floor sign. Models are not yet
drawn in the game; see [what is not done yet](#what-is-not-done-yet).

You describe a thing ("a vending machine, ordinary office size, with a flap
at the bottom that swings") or show a sketch, and back come a picture of it
from two angles and a model file the game can use. You ask for changes in
words. **You never run a command or open Blender**; the assistant does all
of that.

This page is written for the owner. Why it is built this way is in the
[decision log](technical-decisions.md#tooling-decision-models-are-code-2026-09-25);
how it works underneath is in
[development workflow](development-workflow.md#building-a-model). The first
model made with it is the yellow wet-floor sign
([picture](models/previews/WetFloorSign.png)); the game itself still draws
people as capsules and props as stacked blocks.

## How it works for you

1. **You ask.** Write a message: "A vending machine, ordinary office size,
   glass front, the flap at the bottom swings." Add a picture if you have one.
2. **The assistant builds it.** It writes a short script for the model, has
   Blender build it, checks it against the rules below, and looks at the
   picture itself to catch anything obviously wrong.
3. **You see the picture.** The assistant shows it to you and says where it
   is, with anything it had to decide on your behalf.
4. **You say what to change,** in plain words: "taller", "rounder top", "the
   glass should cover more of the front". Steps 2 to 4 repeat, usually two or
   three times per model.
5. **When you are happy, it is committed.** The model is saved in the
   project. A later stone of the game puts models on screen in place of
   today's capsules and blocks.

## What a model is here

A **model** is a shape the game can draw: a **mesh**, which is a skin of
small flat triangles stretched over the shape, the way a paper lantern is
paper over a frame. The game today has no meshes of its own; everything on
screen is built from Unity's built-in cube, cylinder and capsule. A model
made here is a real mesh with corners, bevels and recesses of its own.

Models in Paniq are **crude on purpose** in shape. The look settled in
[look and controls](look-and-controls.md) is a doll's house: a vending
machine is a box with a recessed front and a bump for the coin slot, a person
is a rounded column with no arms. That is not a limit of the tools; it is the
style, and it is also exactly what description-to-model does well.

Crude in shape does not mean plain on the surface. Every model is built
ready for textures and materials; see
[textures and materials](#textures-and-materials-ready-not-yet-chosen).

## How to ask

A good request has most of these; anything you leave out, the assistant
decides and tells you what it chose.

- **What it is.** "A photocopier." "A tall fridge." "A person."
- **How big, roughly.** "Ordinary office size" is a fine answer; the
  assistant looks up real sizes. Or give a size: "about waist high".
- **Which parts move**, if any. "The lid opens." "The drawer slides out."
- **What it should look like**, in words: "boxy", "rounded", "on four thin
  legs", "with a screen on the front".
- **Which parts are made of different stuff**: "glass front", "metal legs",
  "fabric seat". Each becomes its own **surface** (see below), so it can get
  its own material later. Colours are welcome too; they are noted for when
  materials arrive.
- **A picture**, if you have one. Put it under `docs/reference/` (any PNG or
  JPG; a photo, a sketch on paper, a screenshot of something similar) or
  paste it into the chat. The assistant can look at pictures. A front view
  and a side view help most; something for scale (a person, a door) helps
  with size.

## What comes back

1. **A picture**, `docs/models/previews/<Name>.png`. The left half is the
   thing as the game's camera would see it (from a corner, looking down at
   the game's angle); the right half is a straight-on front view. Each
   surface is drawn in its own pale shade, the main body palest, so you can
   see which parts will be able to take different materials. The shades are
   only for the picture; they are not the model's colours.
2. **The model file**, `Assets/Paniq/Content/Models/<Name>.fbx`. FBX is a
   common file format for 3D shapes that Unity reads without any extra
   software. This is the file the game will use.
3. **A short report**, `docs/models/previews/<Name>.json`, with the model's
   size, how many triangles it spends, which parts move, its surfaces, and a
   fingerprint of the shape so the next build knows whether anything changed.
4. **The script that made it**, `tools/models/models/<Name>.py`. This is the
   model's source. You never need to open it; the assistant edits it when
   you ask for a change.

**To look at a model in Unity yourself** (optional), click by click:

1. In the Unity window, find the **Project** panel (usually at the bottom;
   if it is missing, **Window > General > Project**).
2. Open the folders **Assets > Paniq > Content > Models** and click the
   model's name.
3. The **Inspector** panel (usually on the right) shows the file's settings;
   at its bottom is a small 3D preview you can drag round with the mouse.
   Expect a plain grey shape at real size, standing on its base.
4. To see it in a scene, drag the file from the Project panel into the
   **Scene** view. It appears where you drop it, a metre being a metre, its
   front facing the same way people face. Delete it again afterwards
   (select it, press **Delete**) so the scene stays as it was; the stone
   that draws models in the game will place them itself.

## Asking for changes

Say what should differ: "make it 20 cm taller", "rounder corners", "add a
second drawer", "the lid should open". The assistant changes the script,
rebuilds, and shows you the new picture. Every earlier version is in git, so
"go back to the one before" is always possible.

Two things to know:

- **The script is the truth, not a Blender file.** Blender is the 3D
  program that does the building. Changes made in it by hand would be lost
  on the next build. If you want a model sculpted by hand one day, say so,
  and that one model switches to a hand-made file with its script retired;
  it is a choice per model, not a problem.
- **Changing a model does not change the game** until a stone puts models on
  screen. Until then the picture is the way to judge it.

## Textures and materials: ready, not yet chosen

**Decided (2026-09-26):** later prototypes and the game give every item
textures and materials that react to light. Today's crude prototype draws
everything in flat colours, and which look the textures take is chosen later,
once concept art settles it. What was decided now is that no model may be a
dead end for that.

Three words first:

- A **material** says how a surface meets light: its colour, how shiny or
  rough it is, whether it looks like metal, glass or cloth.
- A **texture** is a picture painted onto a surface: wood grain, a label, a
  face.
- A **texture map** is the hidden instruction that says which part of the
  picture lands on which face, like the flattened-out cardboard of a box
  before it is folded.

Every model is built with all three in mind:

- **Surfaces.** A model is divided into named surfaces ("Body", "Glass",
  "Screen", "Fabric"), each of which arrives in the game as a separate part
  of the mesh. Later, the glass of a vending machine can be shiny and
  see-through while its body is painted metal.
- **A texture map on every part.** Every model carries one, laid out so no
  two faces share a spot on the picture. A painted picture per model, or a
  small shared set of colour swatches, can be added without rebuilding the
  model by hand.
- **Ready for bumpy materials.** Unity is told to work out the extra
  direction data (tangents) that materials with painted-on bumps and dents
  need.

When the look is chosen, the work is: make the materials and pictures, and
teach the game which material goes on which surface. If the kit itself has
to change (a different texture-map layout, say), every model is rebuilt from
its script with one command. No model is ever redone by hand. The game's own
tints, frightened red and charred black, can still be laid on top of a
textured surface.

## The rules every model follows

The build checks these and refuses a model that breaks one, saying which.

- **Metres.** A Blender metre is a Unity metre. A 1.8 m vending machine is
  1.8 m tall in the game.
- **It stands on the floor.** The model's origin (the point Unity holds it
  by) is at the centre of its footprint, on the floor, so putting it at a
  spot puts it on the ground there.
- **It faces the way people face.** The game's people face along +X, and so
  does every model's front. A model script never thinks about this; the
  build turns the model the right way as it exports.
- **It fits its footprint.** Each model declares how wide, how deep and how
  tall it is, and the build rejects a shape that spills more than 5 % over.
  The simulation gives every prop one size number, and the stone that draws
  models scales the model uniformly to match it, so a model is authored at
  the thing's real size.
- **A triangle budget.** A prop may spend 500 triangles and a person 300.
  This is a style rule, not a performance one: five hundred people at 300
  triangles each is nothing to a graphics card. It stops a model drifting
  towards detail the style does not want. Textures, not triangles, are where
  detail will live.
- **Named surfaces and a texture map.** Every part has at least one named
  surface and exactly one texture map, inside the picture's square.
- **Moving parts are named.** A lid, a flap or a door is its own named part
  with its pivot on the hinge. The game turns it in code.
- **Deformations are shape keys.** See the next section.
- **One file per thing, named after it.** A prop's model is named after its
  kind in the simulation (`VendingMachine`, `Cabinet`); people share one
  model, `Person`.

## What "simple animation" means here

The style decision stands: no character rigs (a skeleton of bones inside a
model), no Animator controllers (Unity's system for playing recorded
motions), no animation packages. All motion is done in code, the way people
already bob and waddle today. Models support that in two ways:

- **Hinge parts.** A copier's lid is a separate named part whose pivot sits
  on the hinge. When the simulation says the lid is open, the game turns
  that part; the model only has to be built so the turn looks right.
- **Shape keys.** A shape key (Unity calls it a **blend shape**) is a second
  stored position for every point of the mesh: "this is the person, and this
  is the same person leaning forward". The game dials it between 0 and 1 in
  code, so a frightened person can lean, flinch or crouch a little without a
  skeleton. A model may carry a few of these, each with a name.

What this does not cover: walk cycles, arms and legs that swing, anything
that needs bones. If a stone ever needs that, the decision log says what to
revisit.

## What is not done yet

- **One model so far: `WetFloorSign`,** a waist-high yellow A-frame
  "Caution: floor slippery when wet" sign. The game has no wet-floor sign
  among its props yet, so the model waits for one. No person model yet: a
  person mesh arrives with a different kind of renderer (because of shape
  keys) and one line of the see-through-walls code has to learn about it
  first. That is recorded in the decision log as a prerequisite.
- **Models are not shown in the game.** A later stone replaces the capsule
  and the block-built props kind by kind. Until then the preview picture is
  where a model lives.
- **No materials or textures exist yet.** Models are ready for them; the
  look is chosen later.
