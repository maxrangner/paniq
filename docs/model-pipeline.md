# Making models: from a description to a thing in the game

You describe a thing ("a vending machine, ordinary office size, with a flap
at the bottom that swings") or drop a sketch into a folder, and back come a
picture of it from two angles and a model file the game can use. You ask for
changes in words. Nothing here needs Blender or Unity open.

This page is written for the owner. The scripts it describes live under
`tools/models/`; why they are built this way is in the
[decision log](technical-decisions.md#tooling-decision-models-are-code-2026-09-25).
Nothing has been made with it yet: the pipeline landed before its first
model, and the game still draws people as capsules and props as stacked
blocks.

## What a model is here

A **model** is a shape the game can draw: a **mesh**, which is a skin of
small flat triangles stretched over the shape, the way a paper lantern is
paper over a frame. The game today has no meshes of its own; everything on
screen is built from Unity's built-in cube, cylinder and capsule. A model
made here is a real mesh with corners, bevels and recesses of its own.

Models in Paniq are **crude on purpose**. The look settled in
[look and controls](look-and-controls.md) is a doll's house: a vending
machine is a box with a recessed front and a bump for the coin slot, a person
is a rounded column with no arms. That is not a limit of the tools; it is the
style, and it is also exactly what description-to-model does well.

## How to ask

Say, in a message, what the thing is and anything you care about. A good
request has most of these; anything you leave out, the assistant decides and
tells you what it chose.

- **What it is.** "A photocopier." "A tall fridge." "A person."
- **How big, roughly.** "Ordinary office size" is a fine answer; the
  assistant looks up real sizes. Or give a size: "about waist high".
- **Which parts move**, if any. "The lid opens." "The drawer slides out."
- **What it should look like**, in words: "boxy", "rounded", "on four thin
  legs", "with a screen on the front".
- **A picture**, if you have one. Put it under `docs/reference/` (any PNG or
  JPG; a photo, a sketch on paper, a screenshot of something similar) or
  paste it into the chat. The assistant can look at pictures. A front view
  and a side view help most; something for scale (a person, a door) helps
  with size.
- **Colour** is not part of the model. The game colours things itself, the
  same way it turns a frightened person red today, so "a red chair" is a
  request for the game, not for the model. Say it anyway; it goes on the
  list for the stone that puts the model on screen.

Expect two or three rounds per model: the first version comes back, you say
"taller" or "the top should be rounder", the next one comes back a minute
later.

## What comes back

1. **A picture**, `docs/models/previews/<Name>.png`. The left half is the
   thing as the game's camera would see it (from a corner, looking down at
   the game's angle); the right half is a straight-on front view. Open it
   in VS Code by clicking it. It is grey and untextured on purpose.
2. **The model file**, `Assets/Paniq/Content/Models/<Name>.fbx`. FBX is a
   common file format for 3D shapes that Unity reads without any extra
   software. This is the file the game will use.
3. **A short report**, `docs/models/previews/<Name>.json`, with the model's
   size, how many triangles it spends, which parts move, and a fingerprint
   of the shape so the next build knows whether anything changed.
4. **The script that made it**, `tools/models/models/<Name>.py`. This is the
   model's source. You never need to open it; the assistant edits it when
   you ask for a change.

**To see a model in Unity**, click by click:

1. In the Unity window, find the **Project** panel (usually at the bottom;
   if it is missing, **Window > General > Project**).
2. Open the folders **Assets > Paniq > Content > Models** and click the
   model's name.
3. The **Inspector** panel (usually on the right) shows the file's settings;
   at its bottom is a small 3D preview you can drag round with the mouse.
   Expect a grey shape at real size, standing on its base.
4. To see it in a scene, drag the file from the Project panel into the
   **Scene** view. It appears where you drop it, a metre being a metre, its
   front facing the same way people face. Delete it again afterwards
   (select it, press **Delete**) so the scene stays as it was; the stone
   that draws models in the game will place them itself.

If a build says **"unchanged, nothing copied"**, the model's shape is exactly
what it was last time, so no file was rewritten and there is nothing new to
look at.

## Asking for changes

Say what should differ: "make it 20 cm taller", "rounder corners", "add a
second drawer", "the lid should open". The assistant changes the script,
rebuilds, and shows you the new picture. Every earlier version is in git, so
"go back to the one before" is always possible.

Two things to know:

- **The script is the truth, not the Blender file.** Blender is the 3D
  program that does the building, and a copy of its file can be saved to
  look round in (`Temp\PaniqModels\<Name>\<Name>.blend`). Changes made there
  by hand are lost on the next build. If you want to sculpt something by
  hand one day, say so, and that model switches to a hand-made file with the
  script retired; it is a choice per model, not a problem.
- **Changing a model does not change the game** until a stone puts models on
  screen. Until then the picture is the way to judge it.

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
  towards detail the style does not want.
- **No textures, no materials.** Colour and shine come from the game's own
  materials, so state colouring (frightened red, charred black) works on
  models the same as on capsules today.
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

- **No model has been made.** The first one starts with a request like the
  ones above. A prop is the right first model, not a person: a person mesh
  arrives with a different kind of renderer (because of shape keys) and one
  line of the see-through-walls code has to learn about it first. That is
  recorded in the decision log as a prerequisite.
- **Models are not shown in the game.** A later stone replaces the capsule
  and the block-built props kind by kind, in `AgentViews`,
  `BoxViews.CreateOfficeThing` and `RoomView.CreateTable`. Until then the
  preview picture is where a model lives.
- **Only one moving-part style and one deformation style.** Sliding parts
  (a drawer) are a hinge part moved instead of turned; both are code.

## If Blender is not found

Blender 5.2 LTS is installed on this computer under
`C:\Program Files\Blender Foundation\`. If it is ever moved or a different
version is installed, the build says "Cannot find Blender 5" and how to point
it at the right one (an environment variable called `BLENDER_PATH`). The
project pins Blender 5.2; a build with another version warns, and a warning
there means "check the preview more carefully", not "stop".

## For the assistant: how it works

The owner can stop reading here.

**Build.** `tools\BuildModel.ps1 -Name <Name>` runs
`blender --background --factory-startup --python-exit-code 1 --python tools/models/build.py -- --model <Name> --repo <repo> --scratch Temp\PaniqModels`.
`-All` builds every script under `tools/models/models/`; `-Example <Name>`
builds a fixture from `tools/models/examples/` into
`Assets/Paniq/Tests/Fixtures/Models/`; `-KeepBlend` saves a `.blend` beside
the raw export; `-Force` redraws the picture of an unchanged model. Lines
starting `PANIQ ` are the human-facing output; everything else is Blender's
chatter, shown only on failure.

**A model script** defines `build()` and returns a `Model`:

```python
from paniq_models import Model

def build():
    m = Model("VendingMachine", footprint_mm=(900, 800), height_mm=1800, budget_tris=500)
    m.box("Cabinet", size=(0.9, 0.8, 1.8), bevel=0.02).inset("front", 0.06, -0.03)
    m.box("Base", size=(0.8, 0.7, 0.1))
    flap = m.part("Flap", hinge_at=(0.0, -0.4, 0.3))
    m.box("FlapPanel", size=(0.5, 0.02, 0.25), at=(0.0, -0.41, 0.05), parent=flap)
    return m
```

The authoring frame is Blender's: +Z up, -Y the front, +X the model's right,
metres, and every piece's `at` is its bottom centre. Pieces: `box(name, size,
at, bevel, bevel_segments, parent)` and `cylinder(name, radius, height, at,
segments, axis, bevel, bevel_segments, parent)`; each returns a `Piece` with
chainable `bevel(width, segments)`, `inset(face, thickness, depth)` and
`extrude(face, distance, scale)`, where `face` is one of `up`, `down`,
`front`, `back`, `left`, `right`. `part(name, hinge_at)` makes a named child
object with its origin at the hinge; pieces join it with `parent=`.
`shape_key(name, move)` stores a deformation of the body, `move` mapping a
vertex position to where it goes at full strength. Taper, mirror and join
are not in the kit yet; add them to `paniq_models/__init__.py` when the
first model needs them.

**What `build.py` does**, in order: realise the objects (one body named
after the model plus one child per part, in a `Model` collection); validate
(`validate.py`: floor, footprint, height, budget, names, hinge inside the
model); hash the geometry plus the export recipe (`report.py`); if the hash
matches the last report and the FBX exists, stop with "unchanged"; render
the preview (`preview.py`: EEVEE, two orthographic views, composed with
numpy; a render failure is a warning, never a stop); export
(`export.py`: bake the yaw in `axes.py` into the mesh data, then FBX with
forward -Z, up Y, apply transform, FBX All scaling, triangles, no
animation); copy the FBX into place; write the report.

**Unity's side.** `Assets/Paniq/Editor/ModelImportSettings.cs` applies the
import settings to every FBX under `Content/Models` and the fixtures on
import: file units and scale, axis conversion baked, no materials, no
collider, no animation, blend shapes on with calculated normals, not
readable. A hand change in the Inspector does not survive a reimport; raise
`GetVersion()` to force one after changing the settings.

**The ruler.** `tools/models/examples/CalibrationBox.py` is a 1 m box with a
bump on top, a bump on the front, a bump on its right side, a flap hinged
along the back top edge and one shape key. `ModelsEditModeTests` (filter
word `Models`) loads its FBX and asserts the bounds (up on +Y, front on +X,
right on +Z), that every transform is identity, the flap's pivot and
extent, the blend shape by name, no collider, no material, that the triangle
count matches the report, and the importer settings. Rebuild it with
`tools\BuildModel.ps1 -Example CalibrationBox` after any change to the kit
or the recipe, then run the test.

**When the ruler fails after an upgrade.** A wrong bound on X or Z means the
front or the right landed elsewhere: change `EXPORT_YAW_DEGREES` in
`axes.py` (a Blender point (x, y, z) reaches Unity as (x, z, y) today). A stray
rotation on every transform means the exporter's apply-transform no longer
bakes it: switch to a Z-up export (`axis_forward='Y', axis_up='Z'`, no
apply-transform) and let Unity's `bakeAxisConversion` do the work. A scale
of 100 means the unit scaling moved: `apply_scale_options`. Record whatever
the fix was in the decision log.
