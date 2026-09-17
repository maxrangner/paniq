# Development workflow

## Git

- Commit Unity scenes, prefabs, ScriptableObjects, `.meta` files, package
  manifest, and generated package lockfile once Unity creates it.
- Do not commit `Library`, build output, logs, IDE files, or user settings.
- Do not use Git LFS until large binary source media exists and its patterns
  have been agreed.

## Assisted development

- Begin each task by restating its game-facing outcome in plain language.
- Research current official Unity documentation before recommending unfamiliar
  technology, and provide one recommended path rather than an unexplained list
  of options.
- Establish broad game identity before deciding slice-specific mechanics. Work
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
- Explain new terms when first used and state what the owner should see or be
  able to do after each Unity editor step.
- End each task with changed files, validation actually run, and any remaining
  manual action or limitation. Never present an unrun Unity check as passed.

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

## Profiling checkpoint

After each vertical-slice milestone, make a standalone Windows build and record
the date, hardware, scene, frame rate, frame-time hotspots, active agent count,
and active event count in the milestone's notes. Use that evidence, not an
assumed future scale requirement, to justify optimization work.
