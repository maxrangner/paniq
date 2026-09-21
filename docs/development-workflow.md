# Development workflow

## Git

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

### Running the edit-mode tests without closing Unity

Unity's batch-mode test runner cannot open the project while the editor has it
open, which is the normal state while working. The simulation is plain C# --
it touches one Unity type, and only as a serialization marker -- so it can be
compiled and run on its own:

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

Two checks that cannot run outside the editor are covered another way by the
script -- the fixed timestep is read from `ProjectSettings/TimeManager.asset`,
and the saved scenario asset is compared with the code defaults by reading its
YAML. Everything it genuinely cannot check is listed as skipped at the end of
every run, never as passed.

**This is a fast check, not a substitute for Unity's own runners.** Run the
EditMode and PlayMode runners in the editor before calling a change verified in
the engine.

## Profiling checkpoint

Before adopting any scale tooling, and whenever a prototype stone noticeably
raises the number of people or visual objects on screen, make a standalone
Windows build. Record the date, hardware, scene, frame rate, frame-time
hotspots, active agent count, and active event count in that prototype's note. Use that evidence, not an
assumed future scale requirement, to justify optimization work.
