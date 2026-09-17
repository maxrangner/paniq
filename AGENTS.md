# Paniq working agreements

## Scope and structure

- Inspect existing code and documents before changing them.
- Keep each change tied to the current milestone; avoid speculative systems.
- Keep runtime code under `Assets/Paniq/Runtime` and content under
  `Assets/Paniq/Content`.
- Use one runtime assembly until real subsystem boundaries require splitting it.
- Do not add packages, plugins, paid assets, services, or build targets without
  a documented current need.

## Beginner-friendly collaboration and research

- Assume the project owner is new to game development unless they demonstrate
  otherwise. Explain engine terms the first time they appear and connect each
  technical change to its practical game-design purpose.
- Before recommending an engine feature, package, workflow, or performance
  technique, research its current official documentation. Give one recommended
  path in plain language, state why it fits Paniq now, and name the condition
  that would justify revisiting it later.
- Do not make the owner choose project-specific technical values they could not
  reasonably know. Investigate them, recommend a safe default, and ask only for
  genuine design or business preferences.
- Keep technical decisions documented with the problem, recommendation,
  alternatives considered when material, source links, and a plain-language
  explanation of the trade-off.
- For each implemented task, report what changed, how to use or verify it, and
  what was not validated. Provide copyable next steps when a local tool or
  editor action is required.

## Simulation rules

- Agent-to-agent interaction must use stable identifiers or explicit event
  data, never direct scene-object references.
- Random simulation behavior must originate from a scenario seed and be
  replayable on the same build and platform.
- Visual, audio, and UI code may observe simulation state but must not decide
  simulation outcomes.
- Add ECS, Burst, navigation, or other scale tooling only after profiling shows
  that the current approach blocks the intended scenario.

## Quality checks

- Add or update relevant edit-mode and play-mode tests with behavior changes.
- Run the available Unity tests before claiming validation passed.
- Record standalone-build profiling results after each vertical-slice milestone.
- Do not modify Git history or add Git LFS rules unless explicitly requested.
