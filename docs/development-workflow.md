# Development workflow

## Git

**Branches and commit size are settled in [`AGENTS.md`](../AGENTS.md) under
*Git workflow*, and that is the binding copy.** In short: ask the owner before
making a branch, even for a one-line fix, and split work into one commit per
layer — `-controls` for input and camera, `-game` for rules and scoring,
`-visuals` for how it is drawn — with each commit carrying its own tests and
documentation.

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
- Run both test groups after changes to foundation code or scene flow.
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
.\tools\RunUnityTests.ps1                        # every edit-mode test
.\tools\RunUnityTests.ps1 -PlayMode              # the play-mode tests
.\tools\RunUnityTests.ps1 -Category UnityPhysics # one NUnit category
.\tools\RunUnityTests.ps1 -Filter ReplayFingerprint
.\tools\RunUnityTests.ps1 -Reset                 # the bridge is stuck on a run Unity dropped
```

The editor must be open on the project and not in play mode. If nothing
happens, click into the editor once so it notices changed files, or press
Ctrl+R there to refresh. A dialog in the editor (for example "the open scene
was modified externally") pauses everything until it is answered; answer it,
then use `-Reset` if the run never reports back.

`tools\CompileAgainstUnity.ps1` compiles every Paniq assembly against Unity's
libraries without Unity running: a quick check that a change builds before
handing it to the editor.

### Running the edit-mode tests without Unity

The plain-.NET runner below still compiles and runs the simulation on its own.
Every test that needs Unity's physics stops at once and is listed as
**skipped**, never as passed, so today it mainly checks the scenario asset and
the tests that need no physics:

```powershell
.\tools\RunEditModeTests.ps1                  # every edit-mode test, about 20 seconds
.\tools\RunEditModeTests.ps1 -FingerprintsOnly # just the ten replay fingerprints
.\tools\RunEditModeTests.ps1 -Filter Doors     # tests whose name contains "Doors"
.\tools\RunEditModeTests.ps1 -Record           # re-record fingerprints, ready to paste
```

The script compiles the simulation, the edit-mode tests, a few small Unity
stand-ins (`tools/Stubs`) and a reflection-driven runner (`tools/TestRunner`)
with Unity's own bundled Roslyn compiler, and runs them on the installed .NET
runtime. It needs the editor installed, not running.

Use `-Record` only for a deliberate behaviour change: it prints the ten
fingerprints as `[TestCase]` lines to paste into
`ReplayFingerprintEditModeTests.cs`, which still has to be accompanied by the
version bumps above.

The fingerprints now need Unity's physics, so under this runner they are
skipped and `-Record` has nothing to print. Re-record them in the editor
instead: `tools\RunUnityTests.ps1 -Filter ReplayFingerprint` fails each changed
case with its new number (`fingerprint is 0x...UL`), ready to paste.

Two checks that cannot run outside the editor are covered another way by the
script -- the fixed timestep is read from `ProjectSettings/TimeManager.asset`,
and the saved scenario asset is compared with the code defaults by reading its
YAML. Everything it genuinely cannot check is listed as skipped at the end of
every run, never as passed.

**This is a fast check, not a substitute for Unity's own runners.** Run the
EditMode and PlayMode tests in the editor (`tools\RunUnityTests.ps1`) before
calling a change verified in the engine.

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
6. Run **Paniq > Bake Scenario From Scene**.

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
