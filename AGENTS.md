# Paniq working agreements

These agreements apply to every AI assistant working in this repository
(Claude Code, Codex, and any other). They are binding, not advisory. When a
rule here conflicts with an assistant's default style, this file wins.

## How to talk to the project owner (highest priority)

The owner is new to game development. Treat that as a permanent fact of this
project, not a phase to grow out of. These rules override any instinct to be
concise, technical, or efficient.

- **Write for someone who has never built a game.** No unexplained jargon, no
  Unity vocabulary without a gloss, no acronyms on first use. If a sentence
  would only make sense to someone who has shipped a game before, rewrite it.
- **Gloss every technical term the first time it appears in a response** — not
  once per project, once per response. The owner reads these across days and
  sessions and will not remember a definition from last week. Keep the gloss
  to one short clause in plain words, e.g. "a prefab (a saved template of a
  game object you can stamp into a scene many times)".
- **Lead with the game, not the code.** Every explanation starts with what a
  player would see, feel, or do differently. The technical detail comes after,
  and only as much as the owner needs to make the decision in front of them.
- **Use a concrete example for anything abstract.** Prefer "when 40 people run
  for the same exit, the ones at the back currently walk through each other"
  over "the avoidance system lacks density handling". If a concept resists an
  example, it is not yet explained well enough to present.
- **Analogies to ordinary life are encouraged.** Crowds, traffic, queues,
  recipes, and stage plays are all fair game. Say when an analogy breaks down.

## Asking the owner questions

Most questions should never reach the owner. Before asking anything:

1. Try to answer it by reading the repo, the docs, or official documentation.
2. If it is a technical value the owner could not reasonably know, pick a safe
   default, state the default and its reasoning, and move on. Do not ask.
3. Only ask when the answer is a genuine **creative, product, or business**
   preference — something only the owner can decide about the game they want.

When a question does reach the owner it must follow this shape:

- **Ask at most two or three questions at a time.** More than that is a
  handoff of work, not a request for direction.
- **State what is being decided in player-facing terms**, in one sentence.
- **Give named options, each with a concrete example** of what the player
  would experience under that option.
- **Recommend one option and say why it fits Paniq now.**
- **Say how reversible the choice is** — whether picking wrong costs an
  afternoon or a month.
- **Never ask the owner to supply a number, engine setting, package name, or
  API choice.** Research it, choose it, and explain the choice instead.

### Worked example

Not acceptable:

> Should agents use NavMeshAgent with obstacle avoidance quality set to High,
> or a custom steering solution with spatial hashing? What avoidance radius do
> you want?

Acceptable:

> **Decision: how realistic should crowd movement look in the prototype?**
>
> - **Option A — simple crowds (recommended).** People walk to exits and slide
>   around each other. In a 40-person room it reads as a believable rush, but
>   in a tight doorway you will sometimes see two people overlap for a moment.
>   This is the path Unity gives us for free, so we can have it working this
>   week and spend our time on panic behaviour instead.
> - **Option B — precise crowds.** Nobody ever overlaps, and you get realistic
>   shoulder-to-shoulder crushes at doorways. This needs custom movement code
>   and would take roughly two to three weeks before anything is playable.
>
> Recommendation: Option A now. Switching to B later means rewriting movement
> but nothing else, so this is a reversible choice — roughly a week of rework,
> not a restart. The signal to revisit is playtesters saying doorway crushes
> look fake.
>
> I only need your answer on the feel you want; I will pick the settings.

## Reporting finished work

Every completed task ends with a report in this shape:

- **What is different in the game now**, in plain language and player terms.
- **How to see it for yourself** — exact, copyable, click-by-click steps for
  anything the owner must do in the Unity editor, including what they should
  expect to see on screen when it works.
- **What was actually verified**, naming the checks that were run.
- **What was not verified**, stated plainly. Never present an unrun check as
  passed.
- **Anything now possible or blocked** as a result of the change.

## Git workflow

### Branches

- **Ask the owner before creating a branch, every time.** Name the branch you
  would make and what would go on it, then wait. This applies to a large piece
  of work and to a one-line fix alike: a small fix does not get a branch of its
  own unless the owner says so. Default to working on the branch that is
  already checked out.
- The one exception is `main`: never commit to it directly. If `main` is
  checked out and there is work to do, that is the moment to ask, not to branch
  quietly.
- **Branch names**: `type/short-description` only — `feat/`, `fix/`,
  `chore/`, `docs/`, `refactor/`, `test/` — in plain kebab-case words. Never
  prefix a branch with an assistant's name (`claude/`, `codex/`, or similar),
  and never add a random-looking letters/numbers suffix. If a tool
  auto-generates a branch name that violates this, rename it (or open the PR
  from a correctly named branch) before it's pushed.
- **Merges**: always use `--no-ff`, so every integration leaves a visible
  merge commit in the log, even when the merge could fast-forward.

### How much goes in one commit

A commit is **one layer of change**: the smallest thing that can be described in
one sentence, read on its own, and reverted on its own without dragging
unrelated work out with it. Neither one commit per branch nor one per file save.

The usual split, and the order it is usually made in:

| Suffix | What belongs in it |
| --- | --- |
| `-controls` | Input and camera: what the player presses, and where they look |
| `-game` | Rules, state, scoring, level and round structure: what the simulation decides |
| `-visuals` | How any of it is drawn |

Use the stone's name as the scope, with the layer as a suffix — for example
`feat(prototype-2-controls)`, `feat(prototype-2-game)`,
`feat(prototype-2-visuals)`. A layer that a piece of work does not touch simply
has no commit.

- **Tests and documentation travel with the change they describe**, never in a
  commit of their own. A `-game` commit carries its own tests and its own
  updates to the roadmap and the decision log.
- **A fix found while reviewing your own work is its own commit**, in whichever
  layer it belongs to, rather than being folded back into the commit that
  introduced the problem.
- Still avoid a trail of `wip`-style commits; squash those before pushing. The
  test is never how many commits there are, it is whether each one is a change
  somebody would want to read by itself.

**Worked example.** Prototype 2 added a scored round that ends, seeds, replay
and a high score, a camera the player drives, pause, and an office-tower look.
That is three commits — controls, game, visuals — not one, and not five.

## Scope and structure

- Inspect existing code and documents before changing them.
- Keep each change tied to the current prototype stone (see
  [`docs/roadmap.md`](docs/roadmap.md)); avoid speculative systems.
- Keep runtime code under `Assets/Paniq/Runtime` and content under
  `Assets/Paniq/Content`.
- Use one runtime assembly until real subsystem boundaries require splitting it.
- Do not add packages, plugins, paid assets, services, or build targets without
  a documented current need.

## Research and documentation

- Before recommending an engine feature, package, workflow, or performance
  technique, research its current official documentation. Give one recommended
  path in plain language, state why it fits Paniq now, and name the condition
  that would justify revisiting it later.
- Keep technical decisions documented with the problem, recommendation,
  alternatives considered when material, source links, and a plain-language
  explanation of the trade-off.
- Record every default chosen on the owner's behalf in
  [`docs/technical-decisions.md`](docs/technical-decisions.md), so a decision
  made silently is still a decision the owner can find and overturn.

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
- Record standalone-build profiling results before adopting any scale tooling,
  and whenever a prototype stone noticeably raises the number of people or
  visual objects on screen.
- Do not modify Git history or add Git LFS rules unless explicitly requested.
