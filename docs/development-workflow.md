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
- In **level mode** neither gear runs, only the compile check and the smoke
  check: the level is still changing shape, and the proving waits for the
  hardening pass. The binding rule is in [`AGENTS.md`](../AGENTS.md) under
  *Level mode*, and the short form is below.
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
.\tools\RunUnityTests.ps1 -Category UnityPhysics     # the physics-foundation checks
.\tools\RunUnityTests.ps1 -Filter SeedsFortyToFortyNine -ShowPassed  # the office left alone, ten seeds: the tuning table (Category Measure), skipped by normal runs, about a minute
.\tools\RunUnityTests.ps1 -Filter New_Nobody_Pressed -ShowPassed     # the office played by machine: one layout, one scripted player, thirty seeds (LevelTuningMeasurements), about five minutes
.\tools\RunUnityTests.ps1 -Filter FiftySeeds -ShowPassed  # fifty seeds left alone: fails on any seed that saves more than half (the owner's rule); about four and a half minutes; run before the commit of any batch that touches the Director, the card or the traps
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

### Level mode

One level first, the proving once (2026-10-03, the owner's decision; it
replaced *Sketch and keep*, and the binding text is in `AGENTS.md`):

- **Every change**: change the game code; `CompileAgainstUnity.ps1`; then
  `RunUnityTests.ps1 -Filter Smoke` (`SmokeEditModeTests`: every level on the
  start card played once, failing only on an error or a fire round that never
  ends; about twenty seconds); a local save point, `wip(level): ...`; a
  three-line report.
- **End of each working session**: the session's save points are folded
  into one ordinary commit that says what the session changed (the owner,
  2026-10-03: the level will take weeks and many commits). Nothing is pushed
  unless the owner asks.
- **Not done**: fingerprints, versions, documents, measurements, the full
  run, switches that keep other levels as they were.
- **When the owner says the level is done**: one hardening pass -- tests,
  fingerprints, versions, documents, the full run. What it has to cover is
  listed on the roadmap under *Left open*.

Why: the old way spent hours per change proving numbers about a level that
changed again the next day (machine-played rounds before the owner played,
the office kept byte-identical behind switches, documents per batch). The
test suite itself was never the cost.

### The test levels

Three levels drawn by code (2026-10-01, the owner: "blank levels to test
panicked crowds ... large square room with walls, maze to test following,
interaction test level"), picked from the row on the start card. Each takes
the office scenario's tuning and swaps the building, the people and the
clutter (`TestBuildings`); each has the **Crowd** button, which panics the
whole crowd at a press and calms it at the next, so a behaviour can be
watched without waiting for a fire.

| Level | What it is for |
| --- | --- |
| The square room (`Square.asset`) | One 24 m room, a shut door in the middle of each wall, forty people whose personalities the seed deals. Watch a crowd: the rush, the doorway crushes, who leads and who follows. No fire. |
| The maze (`Maze.asset`) | Thirty-odd 4 m cells joined by archways, one way out, a staff member who knows it and ten visitors who do not. Watch following and finding the way. No fire. |
| The interaction room (`Interaction.asset`) | One of everything to bump, carry, sit on, open, pound or set alight, a lobby, a side room through swing doors, a closet, a locked second exit, eight people with one dial each turned up. Trigger event lights a fire in the middle. |

To add one: append a name to `BuiltInBuilding`, write its method in
`TestBuildings` (ids in the 40001+ ranges, so a test may mix it with office
ids), make a level asset under `Assets/Paniq/Content/Levels` naming it, and
add the asset to the scene's `levels` list on the runner. A level with the
Crowd button plays no hands-off copy for the end card: the switch is never
copied into the copy, so the comparison would be meaningless.

### Two gears

The full suite is about 690 edit-mode tests and 19 play-mode ones: six and a
half minutes end to end with the compile check (measured 2026-10-01; the
"three minutes" this page used to say dated from a suite of 425), because
every test that builds a run needs the physics engine inside the editor. Run
after every step of a six-step task, that is forty minutes spent re-proving
what the step could not have touched. So checking work has two gears:

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

**Tooling is the exception.** A commit confined to the model pipeline, other
scripts under `tools/`, or documentation is guarded by the checks that can
see it instead of `-All`: for models, the build and its rule checks, a look
at the picture, and `-Filter Models` whenever the change reaches Unity's
side (import settings, export recipe, the calibration fixture). The rule is
in [`AGENTS.md`](../AGENTS.md). When the editor has another copy of the
project open (a worktree, say), Unity's own batch runner can run those tests
on this copy with no window:
`Unity.exe -batchmode -projectPath <this copy> -runTests -testPlatform EditMode -testFilter Models -testResults <file> -logFile <file>`.
It only works while no editor has this copy open. Tests that build a run
work too: on 2026-10-03 the whole edit-mode suite (678 tests) ran this way
in about ten minutes, after a first import of about five (see *Unity batch
mode* under the traps, below).

`-Slowest 10` after any run, or `-Slowest 10 -LastRun` afterwards with no
editor, lists the tests the suite spends its time on. Trim on that evidence,
not by feel -- and take it from a *full* run: a filtered run's list names
only what ran (the 2026-10-01 trim started from a filtered list and named the
wrong tests). Each half keeps its own last results
(`result-EditMode.txt`, `result-PlayMode.txt`), so after `-All` the list
covers both. The tests at
the top are whole-building runs over several seeds; the cost is the stepping,
not the checks. A "does it ever happen" test stops at the first seed that
says yes; an every-tick invariant keeps its seeds.

### Which tests cover what

Most test files are named after the code they check (`DoorsEditModeTests`
for the door code, `WayfindingEditModeTests` for wayfinding), so the filter
word is the feature's name. These are the ones that are not:

| Code changed | Filter words |
| --- | --- |
| `PlayerCommandSystem`, `RunDriver.Queue` (the player's clicks, and what is mirrored into the left-alone round) | `Alarms,Nudge,Tug,Influence,LevelSession,HandTally` |
| `Tasks`, `TaskChooser` (the one table of what each activity is; the one chooser; how a task ends) | `Smoke,Influence,Helping,Alarms,Leadership,Barricade,Extinguisher,Tells,Cues,Errands,Sitting` |
| `InfluenceSystem`, `HandHeaveBehaviour`, `HandGatherBehaviour`, `AgentHand` (the hand on a place, the push, the drag, the goal and its conviction, what people do for it) | `Influence,Keycard,Alarms,Extinguisher,Errands,Doors,ReplayFingerprint` |
| `HandChargeSystem` (the hand's bar) | `HandCharge,ReplayFingerprint` |
| `HandOnTheWayOutMeasurements` (the seed 41-43 diagnostics and the walls sweep, run on purpose) | `-Filter HandOnTheWayOut -ShowPassed` |
| `LevelTuningMeasurements` (the office played by machine: a layout candidate, a scripted player, thirty seeds; run on purpose, one case by name, about five minutes a case) | `-Filter New_Nobody_Pressed -ShowPassed` |
| `TugSystem` (the hand on a person) | `Tug,ReplayFingerprint` |
| `TellSystem` (the wind-up before a freeze, a dash or going back), `HandTally` (the end card's count of the hand) | `Tells,Extinguisher,Alarms,Keycard,Helping,Cornered,Nudge,Tug,ReplayFingerprint` |
| `Run` (the tick itself) | `Simulation,ReplayFingerprint` |
| `UniformGridIndex` (who is near here) | `SpatialIndex` |
| `IThreat`, `Threats` (what a danger is) | `ThreatSeam,ReplayFingerprint` |
| `CollisionSystem`, `BodySystem`, `PhysicsWorld` | `HardKnocks,Shoving,PhysicsFoundation,PhysicsObjects` |
| `PrototypeBuilding`, `WorldGeometry`, `Navigation`, `FlowField` | `Rooms,FarRooms,CrossRoom,MeetingRoom,BigBuilding,NavigationRoutes,Wayfinding,Stockroom,SwingDoors,HeavyThings,Influence,CubicleLandscape,BoxTower,StockroomTrap,PowerSystem,Furniture,NewProps,Alarms` |
| `TestBuildings` (the square room, the maze, the interaction room) | `TestBuildings` |
| `LevelDefinition`, `LevelSession`, `LevelLoader`, `RunDriver` (the level row, the seed, the best) | `LevelSession,TestBuildings` |
| `FearSystem.PanicEveryone`, `CalmEveryone` (the Crowd button) | `CrowdSwitch,CalmingDown,ReplayFingerprint` |
| `ItemBehaviour`, `ChairBehaviour`, `PhysicsObjectSystem` | `Blast,Breakables,Items,OfficeItems,Furniture,Possessions,Sitting,HeavyThings` |
| `TraitEffects` | `Traits` |
| `DoorBehaviour`, `DoorSystem` | `Doors,ClosingDoors,DoorBurn,Barricade,Cornered,BoxTower` |
| `LeaderBehaviour`, `HelpBehaviour` | `Leadership,Helping` |
| `PlayerInput`, `DoorClicks`, `PlaceHold`, `HudHitTest` (the pointer) | `DoorClicks,PlayerInputPicking,Nudge,Tug` |
| `DoorSystem.SettlePounding` (the card door giving under the hand) | `Influence,Doors` |
| `CameraRig` (Q, E and the wheel) | `CameraRig` |
| `EventStory`, `RoundScreens` (the read-back and the end card) | `EventLogScreen,EventSigns` |
| `AlarmSystem`, `AlarmBehaviour`, `FlammablesSystem` (bells that pop, bottles that burst) | `Alarms,NewProps,Extinguishers` |
| `TrapSystem`, `DirectorSystem` (the tower of boxes, the stockroom's stack, the Director's ladder and its cap) | `BoxTower,StockroomTrap,DirectorLadder,DirectorCap,Cues,Doors,Stockroom,CubicleLandscape` |
| `KeycardSystem` (the card, where it starts, who has it, the swipe) | `Keycard,DirectorCap,Influence` |
| `ErrandBehaviour`, `CueSystem`, `CalmBehaviour` (the calm day: errands, home time, chats) | `Errands,Cues,Sitting,MeetingRoom,Simulation,ReplayFingerprint` |
| `FrightenedWalk`, `ExtinguisherBehaviour` (the frightened walk through doors) | `FrightenedWalks,Extinguisher,Alarms,CrossRoom` |
| `NudgeSystem` (nudging people) | `Nudge` |
| `FearSystem.Settle` (calming down) | `CalmingDown,CorridorStarers,ReplayFingerprint` |
| `BurningThingsThreat`, `BurningPeopleThreat` (danger is danger) | `DangerIsDanger,ThreatSeam,Extinguisher,ReplayFingerprint` |
| `PowerSystem` (the cable) | `PowerSystem,DirectorLadder` |
| `PerceptionSystem`, `SoundSystem` (what a person sees and hears) | `Perception,Hearing,Simulation` |
| `ModelImportSettings`, `tools/models` (a model's way into Unity; rebuild the ruler first with `BuildModel.ps1 -Example CalibrationBox`) | `Models` |

`PanicBehaviour`, `CalmBehaviour`, `Locomotion`, `Crowd`, the rest of
`FearSystem` and the causal event log have no tests of their own; they are checked only
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

### Traps that cost a test cycle

Each of these was found the slow way. They look like your own breakage and
are not.

**Measuring a level**
- Ten seeds are noise on the office. A round ends with nobody, about half or
  everybody saved, so a ten-seed average is good to about four people either
  way, and one prop moved changes every draw after it: the first ten-seed
  round of 2026-10-02 said 9.6 of 34 saved left alone, and thirty seeds said
  16 to 20 for the same floor. Compare layouts on thirty
  (`LevelTuningMeasurements`), and trust only differences bigger than four.
- A measurement that plays thirty seeds runs longer than NUnit's default
  three minutes for one test and is reported as failed for it, with its
  table printed all the same. `LevelTuningMeasurements` carries a
  `[Timeout]` for that reason.
- The level reads the baked scenario asset. After moving anything in
  `PrototypeBuilding`, rewrite the asset before measuring, or the machine
  plays the old floor.

**The test bridge**
- `-Filter` matches plain text, and a comma separates names
  (`-Filter Doors,ClosingDoors`), from PowerShell and from Bash alike (until
  2026-10-03 a comma list started from Bash ran nothing). `A|B` is not a
  pattern here and matches nothing.
- An inconclusive test fails the run: its `Assume.That` premise no longer
  holds, so it has stopped proving anything.
- `-All -Filter X` passes when only one half has a test matching `X`.
- The bridge compiles `Assets` at the start of every request. Do not edit a
  `.cs` file under `Assets` while a run is in flight.
- `Temp/PaniqTestBridge/result.txt` holds only the latest run;
  `result-EditMode.txt` and `result-PlayMode.txt` keep each half's last. The
  editor console is drowned in physics warnings, so read the files and look
  for lines starting `FAILED`, `passed=` and `failed=`.
- From Bash, run it as
  `powershell -NoProfile -ExecutionPolicy Bypass -File tools/RunUnityTests.ps1 ...`.
  Without the bypass the script is refused.
- A play-mode run can leave `Assets/InitTestScene*.unity` behind, and a
  dialog offering to save it stops the bridge. Delete the file, never commit
  it.

**Settings, content and replays**
- The scenario asset (`Assets/Paniq/Content/FireReactionScenario.asset`) holds a
  baked copy of every setting and of the two version numbers. After changing
  any settings default or authored content, run
  `.\tools\RunUnityTests.ps1 -Menu "Paniq/Rewrite Scenario Asset From Code Defaults"`,
  or `ScenarioAsset_MatchesTheCodeDefaults` fails.
- An editor command that asks for confirmation before doing something
  destructive must check `SessionState.GetBool("Paniq.NobodyIsHereToAsk")`
  and take the yes as given, as `RewriteScenarioAsset` does. Otherwise it
  freezes the editor when the bridge runs it.
- To re-record the replay fingerprints, run `-Filter ReplayFingerprint`: each
  failing case prints `fingerprint is 0x...UL`, ready to paste into its
  `[TestCase]`. There are fifteen cases (older notes say ten or thirteen). Run the filter
  a second time after pasting: a number that moves between two identical runs
  is a determinism bug, not a new recording. To find where two runs part,
  play the seed many times in one test and compare every body's position,
  turn and speed bit for bit each tick (`BitConverter.SingleToInt32Bits` on
  the engine's own floats: the simulation's readings round away the first
  crumbs). If it only parts with the engine on several threads (set
  `JobsUtility.JobWorkerCount = 0` in the test and it stops), it is the
  physics engine, not Paniq's code; see "The physics engine and threads" in
  the technical decisions.
- Never set a loose physical body's rotation between physics steps: it made
  the busiest runs differ from one run to the next. Turn a loose body by giving
  it spin (`SetSpin`) towards the heading you want.
- A test helper called `Run(...)` hides the type `Run` inside its class; call
  helpers `Advance`.

**Pressing Play without the owner**
- `.\tools\RunUnityTests.ps1 -Menu "Edit/Play Mode/Play"` presses Play in the
  open editor (`Edit/Play` is the pre-Unity 6.3 name and no longer exists).
  The result is in `%LOCALAPPDATA%\Unity\Editor\Editor.log`.
- `EditorApplication.delayCall` does not fire while the Unity window is
  minimised. Editor automation uses a one-shot `EditorApplication.update`
  handler instead, as the bridge does.
- The editor window's title names the open scene
  (`Get-Process Unity | % MainWindowTitle`): the quickest way to see what the
  editor has open without touching it.

**Unity batch mode** (only on a *second* checkout: batch mode cannot open the
folder the editor has open)
- `Unity.exe -batchmode -nographics -projectPath <worktree> -runTests -testPlatform EditMode -testResults <file> -logFile <file>`.
  Here `-testFilter` *is* a regular expression. Put the results file outside
  `Temp/`, which Unity empties on exit. The first import takes about five
  minutes.
- Every batch run rewrites `ProjectSettings/TagManager.asset` without its
  byte-order mark; `git checkout --` it before committing.
- Play mode runs too (leave out `-nographics`), but
  `RoundPresentationPlayModeTests.TheOpeningView_ActuallyDrawsSomething`
  always fails there: batch mode never reaches the end of a frame it waits
  for. Only the editor's run can say whether the opening view draws.

**Editing files**
- About half the files under `Runtime/Simulation` begin with a byte-order mark
  (an invisible marker at the start of a text file). Keep it when rewriting a
  file whole; the line endings are plain LF throughout.
- Long shell heredocs containing quotes or `\n` get mangled. Write the script
  to a file first, then run it.
- Never read `PRIVATE_TODO_NO_LMM_KEEP-OUT.md`. It is the owner's, is usually
  modified in the working tree, and differs between branches, so
  `git checkout <branch>` can refuse; use a temporary `git worktree` for work
  on another branch.

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
