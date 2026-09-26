# Development workflow

## Git

**Branches and commit size are settled in [`AGENTS.md`](../AGENTS.md) under
*Git workflow*, and that is the binding copy.** In short: ask the owner before
making a branch, even for a one-line fix, and land a batch of work as a few
commits, usually one — rules, input, drawing, tests and documentation
together — adding a commit only when it can be defended as a change worth
reading or reverting on its own, such as a tooling repair found on the way.

What goes into a commit at all:

- Commit Unity scenes, prefabs, ScriptableObjects, `.meta` files, package
  manifest, and generated package lockfile once Unity creates it.
- Do not commit `Library`, build output, logs, IDE files, or user settings.
- Do not use Git LFS until large binary source media exists and its patterns
  have been agreed.

## Assisted development

Communication rules live in [`../AGENTS.md`](../AGENTS.md) and are binding for
every assistant. In short: plain language, player-facing framing first, a
concrete example for anything abstract, and no questions that require
game-development knowledge to answer.

- Begin each task by restating its game-facing outcome in plain language.
- Research current official Unity documentation before recommending unfamiliar
  technology, and provide one recommended path rather than an unexplained list
  of options.
- Establish broad game identity before deciding detailed mechanics. Work
  from the largest product decision toward the smallest implementation detail.
- Label design statements as **decided**, **hypothesis**, or **prototype
  question**. Do not promote a hypothesis to a commitment without evidence from
  playtesting or an explicit owner decision.
- Research material game-design questions before recommending a direction, and
  record the source, lesson, and Paniq-specific implication in
  [`design-research.md`](design-research.md). Research guides decisions; it does
  not replace playtesting or the owner's creative direction.
- Build and test the smallest version of a proposed system before committing to
  additional layers or dependent systems.
- Explain each new term the first time it appears in a response, and state what
  the owner should see on screen after each Unity editor step.
- End each task with the report shape defined in `AGENTS.md`: what is different
  in the game, how to see it, what was verified, and what was not. Never
  present an unrun Unity check as passed.

## Scenes and content

- `Bootstrap.unity` owns startup only.
- `Development.unity` is a safe place for foundation checks and temporary
  placeholder geometry.
- Future scenario scenes own their authored layout and configuration.
- Runtime code must not silently write authored content while the editor is
  open.

## Tests

- Put logic-focused tests in `Assets/Paniq/Tests/EditMode`.
- Put scene, object-lifecycle, and integration checks in
  `Assets/Paniq/Tests/PlayMode`.
- Checking work runs in two gears: targeted tests while iterating, both test
  groups before committing. The binding rule is in
  [`AGENTS.md`](../AGENTS.md) under *Quality checks*; the commands and the
  coverage table are below.
- The replay fingerprint tests (`ReplayFingerprintEditModeTests`) squash whole
  runs into single numbers. A change meant to be invisible to players, such as
  a restructure, must keep every number. A change meant to alter behaviour
  bumps the scenario's `SimulationCompatibilityVersion` and `ContentRevision`
  and re-records the numbers in the same commit, saying why.

### Running the tests inside the open editor

Since the move to Unity's 3D physics, most simulation tests need the physics
engine, and that exists only inside Unity. Unity's batch-mode runner cannot open
the project while the editor has it open, so a small editor script
(`Assets/Paniq/Editor/TestBridge`) takes requests from a file and runs the tests
in the editor that is already open:

```powershell
.\tools\CompileAgainstUnity.ps1                      # after every edit: compiles, no editor needed
.\tools\RunUnityTests.ps1 -Filter Doors,ClosingDoors # while iterating: the areas a step touched
.\tools\RunUnityTests.ps1 -Filter ReplayFingerprint  # simulation code changed: the canary
.\tools\RunUnityTests.ps1 -All                       # before committing: both halves
.\tools\RunUnityTests.ps1 -Slowest 10 -LastRun       # what the last run spent its time on
.\tools\RunUnityTests.ps1                            # every edit-mode test
.\tools\RunUnityTests.ps1 -PlayMode                  # the play-mode tests
.\tools\RunUnityTests.ps1 -Category UnityPhysics     # the physics-foundation checks, the one category in use
.\tools\RunUnityTests.ps1 -Reset                     # the bridge is stuck on a run Unity dropped
.\tools\BuildModel.ps1 -Name WetFloorSign            # build one model from its script with Blender (see model-pipeline.md)
```

The editor must be open on the project and not in play mode. If nothing
happens, click into the editor once so it notices changed files, or press
Ctrl+R there to refresh. A dialog in the editor (for example "the open scene
was modified externally") pauses everything until it is answered; answer it,
then use `-Reset` if the run never reports back.

`tools\CompileAgainstUnity.ps1` compiles every Paniq assembly against Unity's
libraries without Unity running: a quick check that a change builds before
handing it to the editor.

### Two gears

The full suite is 425 tests and about three minutes, because every test that
builds a run needs the physics engine inside the editor. Run after every step
of a six-step task, that is fifteen minutes spent re-proving what the step
could not have touched. So checking work has two gears:

1. **While iterating.** After every edit, the compile check above. Once a
   step has a claim worth checking (a behaviour is in, not a file saved), the
   tests for the areas the step touched, in one request:
   `-Filter Doors,ClosingDoors`. Several names run every test matching any of
   them. Whenever simulation code changed, add `ReplayFingerprint`: those
   tests squash whole runs into numbers and catch a change of behaviour in
   code that has no tests of its own.
2. **Before committing.** `-All`, on the tree that will be committed, and
   again after any change to shared simulation code (the run itself, the
   systems, the physics world, navigation). This is the run that "validation
   passed" refers to. A targeted run is reported as a targeted check, naming
   what ran.

The full run happens once per task whatever its size, so a larger task per
prompt waits less in total than the same work split into small prompts.

`-Slowest 10` after any run, or `-Slowest 10 -LastRun` afterwards with no
editor, lists the tests the suite spends its time on. Trim on that evidence,
not by feel.

### Which tests cover what

Most test files are named after the code they check (`DoorsEditModeTests`
for the door code, `WayfindingEditModeTests` for wayfinding), so the filter
word is the feature's name. These are the ones that are not:

| Code changed | Filter words |
| --- | --- |
| `InfluenceSystem`, `DeckSystem`, `PlayerCommandSystem` (the player's purse, cards and clicks) | `Powers,Economy,UproarTable,TraitCards` |
| `Run` (the tick itself) | `Simulation,ReplayFingerprint` |
| `UniformGridIndex` (who is near here) | `SpatialIndex` |
| `IThreat`, `Threats` (what a danger is) | `ThreatSeam,ReplayFingerprint` |
| `CollisionSystem`, `BodySystem`, `PhysicsWorld` | `HardKnocks,Shoving,PhysicsFoundation,PhysicsObjects` |
| `PrototypeBuilding`, `WorldGeometry`, `Navigation`, `FlowField` | `Rooms,FarRooms,CrossRoom,MeetingRoom,BigBuilding,NavigationRoutes,Wayfinding,Stockroom,SwingDoors` |
| `ItemBehaviour`, `ChairBehaviour`, `PhysicsObjectSystem` | `Blast,Breakables,Items,OfficeItems,Furniture,Possessions,Sitting` |
| `TraitEffects` | `Traits,TraitCards` |
| `DoorBehaviour`, `DoorSystem` | `Doors,ClosingDoors,DoorBurn,Barricade,Cornered` |
| `LeaderBehaviour`, `HelpBehaviour` | `Leadership,Helping` |
| `GroupSystem` (sticking together) | `Groups,TraitCards` |
| `PlayerInput`, `DoorClicks`, `HudHitTest` (the pointer) | `DoorClicks,PlayerInputPicking` |
| `AlarmSystem`, `AlarmBehaviour`, `FlammablesSystem` (bells that pop, bottles that burst) | `Alarms,NewProps,Extinguishers` |
| `PerceptionSystem`, `SoundSystem` (what a person sees and hears) | `Perception,Hearing,Simulation` |
| `ModelImportSettings`, `tools/models` (a model's way into Unity; rebuild the ruler first with `BuildModel.ps1 -Example CalibrationBox`) | `Models` |

`FearSystem`, `PanicBehaviour`, `CalmBehaviour`, `Locomotion`, `Crowd` and
the causal event log have no tests of their own; they are checked only
through whole runs. A change there means `ReplayFingerprint` in the small
gear and the full run before the commit, without exception. When a test file
is added or renamed, this table is updated in the same commit.

### The runner that needed no editor is gone

Until 2026-09-23 `tools/RunEditModeTests.ps1` compiled the simulation without
Unity and ran the edit-mode tests in about twenty seconds. Once people and
things became physical bodies, every test that builds a run needed the
editor's physics engine, so nearly the whole suite was skipped under it and it
was retired along with its stand-ins and its runner. The two checks it made
its own way live in the editor's suite: the fixed timestep in
`SimulationContractEditModeTests`, and the saved scenario asset matching the
code defaults in `SimulationEditModeTests`.

## Building a model

The owner's side (how to ask, what comes back, the rules) is in
[model-pipeline.md](model-pipeline.md). The owner never runs any of this;
the assistant does. This section is how it works underneath.

**Build.** A model is a script under `tools/models/models/<Name>.py`.
`tools\BuildModel.ps1 -Name <Name>` runs
`blender --background --factory-startup --python-exit-code 1 --python tools/models/build.py -- --model <Name> --repo <repo> --scratch Temp\PaniqModels`.
`-All` builds every script; `-Example <Name>` builds a fixture from
`tools/models/examples/` into `Assets/Paniq/Tests/Fixtures/Models/`;
`-KeepBlend` saves a `.blend` beside the raw export to look round in;
`-Force` redraws the picture of an unchanged model. Lines starting `PANIQ `
are the human-facing output; everything else is Blender's chatter, shown
only on failure. Nothing needs the Unity editor; it picks the file up next
time it looks.

**A model script** defines `build()` and returns a `Model`:

```python
from paniq_models import Model

def build():
    m = Model("VendingMachine", footprint_mm=(900, 800), height_mm=1800, budget_tris=500)
    m.box("Cabinet", size=(0.9, 0.8, 1.8), bevel=0.02).inset("front", 0.06, -0.03)
    m.box("Window", size=(0.6, 0.02, 1.0), at=(0.0, -0.40, 0.7), surface="Glass")
    m.box("Base", size=(0.8, 0.7, 0.1))
    flap = m.part("Flap", hinge_at=(0.0, -0.4, 0.3))
    m.box("FlapPanel", size=(0.5, 0.02, 0.25), at=(0.0, -0.41, 0.05), parent=flap)
    return m
```

The authoring frame is Blender's: +Z up, −Y the front, +X the model's
right, metres, and every piece's `at` is its bottom centre. Pieces:
`box(name, size, at, bevel, bevel_segments, parent, surface)` and
`cylinder(name, radius, height, at, segments, axis, bevel, bevel_segments,
parent, surface)`; each returns a `Piece` with chainable
`bevel(width, segments)`, `inset(face, thickness, depth)`,
`extrude(face, distance, scale)` and `rotate(degrees, axis, about)`, where
`face` is one of `up`, `down`, `front`, `back`, `left`, `right`. `rotate`
turns a piece the right-hand way round a line along `X`, `Y` or `Z` through
`about` (a leaning panel); face names pick faces by where they look after
the turn, so inset and extrude first. `surface` defaults to `Body`; a surface
name is one capitalised word. `part(name, hinge_at)` makes a named child
object with its origin at the hinge; pieces join it with `parent=`.
`shape_key(name, move)` stores a deformation of the body, `move` mapping a
vertex position to where it goes at full strength. Taper, mirror and join
are not in the kit yet; add them to `paniq_models/__init__.py` when a
model needs them.

**What `build.py` does**, in order: realise the objects (one body named
after the model plus one child per part, in a `Model` collection; one
material slot per surface in first-appearance order; a Smart UV Project
texture map, before any shape key); validate (`validate.py`: floor,
footprint, height, budget, one texture map inside the unit square, at least
one surface, names, hinge inside the model); hash the geometry, surfaces,
texture map and export recipe (`report.py`); if the hash matches the last
report and the FBX exists, stop with "unchanged"; render the preview
(`preview.py`: EEVEE, two orthographic views, a pale shade per surface,
composed with numpy; a render failure is a warning, never a stop); export
(`export.py`: bake the yaw in `axes.py` into the mesh data, then FBX with
forward −Z, up Y, apply transform, FBX All scaling, triangles, no
animation); copy the FBX into place; write the report, which lists each
mesh's surfaces in sub-mesh order.

**Unity's side.** `Assets/Paniq/Editor/ModelImportSettings.cs` applies the
import settings to every FBX under `Content/Models` and the fixtures on
import: file units and scale, axis conversion baked, no imported materials
(each surface still arrives as its own sub-mesh, in the report's order), no
collider, no animation, blend shapes on with calculated normals, tangents
calculated (MikkTSpace) for normal-mapped materials, not readable. A hand
change in the Inspector does not survive a reimport; raise `GetVersion()` to
force one after changing the settings.

**The ruler.** `tools/models/examples/CalibrationBox.py` is a 1 m box with
a bump on top, a bump on the front (a second surface, `Accent`), a bump on
its right side, a flap hinged along the back top edge and one shape key.
`ModelsEditModeTests` (filter word `Models`) asserts the bounds (up on +Y,
front on +X, right on +Z), identity transforms, the flap's pivot and
extent, the blend shape by name, the surfaces as sub-meshes in report
order, a texture map and tangents on every mesh, no collider, no material,
the triangle count against the report, and the importer settings. After any
change to the kit or the recipe, rebuild it with
`tools\BuildModel.ps1 -Example CalibrationBox` and run `-Filter Models`.

**When the ruler fails after an upgrade.** A wrong bound on X or Z means the
front or the right landed elsewhere: change `EXPORT_YAW_DEGREES` in
`axes.py` (a Blender point (x, y, z) reaches Unity as (x, z, y) today). A
stray rotation on every transform means the exporter's apply-transform no
longer bakes it: switch to a Z-up export (`axis_forward='Y', axis_up='Z'`,
no apply-transform) and let Unity's `bakeAxisConversion` do the work. A
scale of 100 means the unit scaling moved: `apply_scale_options`. Record
whatever the fix was in the decision log.

**When materials arrive.** A stone that gives surfaces their materials
either assigns them by sub-mesh index, reading the order from the report,
or switches `materialImportMode` on so each sub-mesh arrives with a
material named after its surface, then remaps those names to the project's
materials. Either is a change to the import settings and the presentation
code, not to any model.

## Building a floor plan

A building used to be four typed coordinates per room in a C# file, with no
picture of it until you pressed play. That is why there were four rooms. You
can now lay one out by dragging things around in the scene.

1. Open a scene and make a cube (**GameObject > 3D Object > Cube**).
2. Add **Paniq > Room** to it and scale it to the shape of the room. A blue
   outline shows the floor the simulation will actually use.
3. Give each room a number nothing else in the building uses.
4. Put **Paniq > Door** objects on the walls. The baker works out which room's
   wall each one is in and where along it, so sliding a door along a wall, or
   onto a different wall, is all there is to do. Red means locked to start
   with, green means unlocked.
5. Add **Paniq > Table** for solid furniture, **Paniq > Prop** for loose things
   (a box, a chair, an extinguisher), **Paniq > Person** for people,
   **Paniq > Alarm** for alarms, and exactly one **Paniq > Fire Start** for
   where the fire begins.
6. Give the building a day, if you want one (see
   [the cue system](cue-system.md)): on a **Person**, drag the chair that is
   theirs into *Home*, or tick *home is where they stand*; on a **Room**, set
   *Use* to *Stall* for a toilet stall; and add a **Paniq > Cue** for each
   thing on the timetable -- the meeting that ends (inside its room, with the
   room dragged in) or home time (anywhere). A scene with no cue keeps the
   timetable the level already has.
7. Run **Paniq > Bake Scenario From Scene**.

The baker rounds everything to whole millimetres, and rooms to the size of a
navigation square, because a run only repeats exactly if every number in it is
a whole one. It refuses to write a floor plan the simulation would reject and
says what is wrong in terms of the object in the scene, so a doorway nobody
could fit through, or two rooms overlapping, is caught with the scene still in
front of you rather than at play time.

Press **G** while playing to paint the floor square by square wherever a
person could stand. That is the quickest way to see whether a doorway really
is a way through and whether furniture leaves a route around it.

Tuning numbers -- speeds, tempers, how fire spreads -- are not touched by the
baker. It only takes the shape of the building from the scene.

## Profiling checkpoint

Before adopting any scale tooling, and whenever a prototype stone noticeably
raises the number of people or visual objects on screen, make a standalone
Windows build.

For the simulation and particle budgets there is a ready-made one: menu
**Paniq > Profiling > Build Stress Profile Player** (or
`.\tools\RunUnityTests.ps1 -Menu "Paniq/Profiling/Build Stress Profile Player"`)
builds `Builds\StressProfile\StressProfile.exe`. Run it; a window opens for a
minute or two, then closes, leaving `stress-profile.txt` beside it with ticks
timed at 100, 200 and 500 people and a frame timed under a storm of particle
effects. Copy the numbers into [technical decisions](technical-decisions.md). Record the date, hardware, scene, frame rate, frame-time
hotspots, active agent count, and active event count in that prototype's note. Use that evidence, not an
assumed future scale requirement, to justify optimization work.
