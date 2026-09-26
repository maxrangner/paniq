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
| `PurseSystem`, `DeckSystem`, `PlayerCommandSystem` (the player's purse, cards and clicks) | `Powers,Economy,UproarTable,TraitCards` |
| `InfluenceSystem` (drawing people to a place) | `Influence,ReplayFingerprint` |
| `Run` (the tick itself) | `Simulation,ReplayFingerprint` |
| `UniformGridIndex` (who is near here) | `SpatialIndex` |
| `IThreat`, `Threats` (what a danger is) | `ThreatSeam,ReplayFingerprint` |
| `CollisionSystem`, `BodySystem`, `PhysicsWorld` | `HardKnocks,Shoving,PhysicsFoundation,PhysicsObjects` |
| `PrototypeBuilding`, `WorldGeometry`, `Navigation`, `FlowField` | `Rooms,FarRooms,CrossRoom,MeetingRoom,BigBuilding,NavigationRoutes,Wayfinding,Stockroom,SwingDoors` |
| `ItemBehaviour`, `ChairBehaviour`, `PhysicsObjectSystem` | `Blast,Breakables,Items,OfficeItems,Furniture,Possessions,Sitting` |
| `TraitEffects` | `Traits,TraitCards` |
| `DoorBehaviour`, `DoorSystem` | `Doors,ClosingDoors,DoorBurn,Barricade,Cornered,HeldDoors,BoxTower` |
| `LeaderBehaviour`, `HelpBehaviour` | `Leadership,Helping` |
| `GroupSystem` (sticking together) | `Groups,TraitCards` |
| `PlayerInput`, `DoorClicks`, `HudHitTest` (the pointer) | `DoorClicks,PlayerInputPicking,HeldDoors,Nudge` |
| `AlarmSystem`, `AlarmBehaviour`, `FlammablesSystem` (bells that pop, bottles that burst) | `Alarms,NewProps,Extinguishers` |
| `TrapSystem`, `DirectorSystem` (the tower of boxes, the Director's ladder) | `BoxTower,DirectorLadder,Cues,Doors,Stockroom` |
| `NudgeSystem` (nudging people) | `Nudge` |
| `FearSystem.Settle` (calming down) | `CalmingDown,CorridorStarers,ReplayFingerprint` |
| `BurningThingsThreat`, `BurningPeopleThreat` (danger is danger) | `DangerIsDanger,ThreatSeam,Extinguisher,ReplayFingerprint` |
| `PowerSystem` (the cable) | `PowerSystem,DirectorLadder` |
| `PerceptionSystem`, `SoundSystem` (what a person sees and hears) | `Perception,Hearing,Simulation` |

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

**The test bridge**
- `-Filter` matches plain text, and a comma separates names
  (`-Filter Doors,ClosingDoors`). `A|B` is not a pattern here and matches
  nothing.
- The bridge compiles `Assets` at the start of every request. Do not edit a
  `.cs` file under `Assets` while a run is in flight.
- `Temp/PaniqTestBridge/result.txt` holds only the latest run: a play-mode run
  overwrites an edit-mode one. The editor console is drowned in physics
  warnings, so read the file and look for lines starting `FAILED`, `passed=`
  and `failed=`.
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
  `[TestCase]`. There are thirteen cases (older notes say ten). Run the filter
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
