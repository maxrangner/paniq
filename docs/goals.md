# Goals and milestones

Paniq is built in phases. Each phase has a different purpose, so the words
below are used strictly.

## 1. Foundation — complete

The Unity project, bootstrap and development scenes, runtime assembly,
placeholder content path, source-control rules, smoke tests, and the design
notes every system must obey: the [simulation contract](simulation-contract.md),
[scenario data versus runtime state](scenario-runtime-state.md), the
[agent state model](agent-state-model.md), the [causal event
log](causal-event-log.md), and the [spatial-world rules](spatial-world-rules.md).

## 2. Prototype — current phase

**What a prototype is here:** a playable test bed built stone by stone. Each
stone is one layer added on top of what already works: a system (for example
fire that spreads), a behaviour (for example people who panic), or a style (for
example how the fire looks). After every stone the prototype still runs, and
the owner plays it and decides what the next stone is.

**How prototypes are numbered.** A prototype here is a run of stones that is
worked on and then closed, not a separate game. Prototype 1 is the
fire-reaction office: finished on 2026-09-22 and merged into `main`.
Prototype 2 is next and is being built in that same scene, so nothing from
prototype 1 is thrown away by starting it.

A prototype is **not** a small version of the finished game. It has no fixed
end state, no required feature list, and no promise that any layer survives.
A layer that does not feel right is reworked or thrown away. Its job is to
find out, by playing, what Paniq should feel like.

### The question this prototype exists to answer

> **Can a player watch a disaster, understand why it went wrong, change one
> thing, and do better?**

The [game vision](game-vision.md) already commits to this: outcomes must be
surprising but explainable, so a player can learn from a chaotic run. Nothing
built so far tests it, because the loop that would test it — a round that
*ends*, is *scored*, can be *understood*, and can be *played again* — does not
exist yet. A prototype that can only be watched cannot say whether the game is
any good.

Everything else is in service of that question.

### What this prototype is deliberately not for

Naming these keeps later stones from drifting:

- **Not more disasters.** One is enough to answer the question above.
- **Not art, menus, saving, or platform work.**
- **Not new crowd behaviour for its own sake.** The forty-sixth behaviour stone
  teaches less right now than the first stone of *game* does.

### When the prototype has done its job

Not a feature checklist — a moment. The prototype has taught enough when a
build can be handed to somebody who has never seen it, with nothing explained,
and they play three rounds in a row and understand more each time. At that
point phase 3 can be described.

### Rules for prototype work

- Add one stone at a time and keep the prototype playable after each.
- Stones may be built in batches with a playtest between them, but only in the
  same scene and code, adding up to **one combined level** that exercises
  everything (the owner, 2026-09-19: "Final goal is still one combined level
  trying everything").
- Every stone still obeys the foundation notes (replayable seeded
  simulation, stable IDs, cause-and-effect events, visuals that only observe).
- Each stone updates the level's page ([the office level](the-office-level.md)), the
  [prototype roadmap](roadmap.md), and any defaults in
  [technical decisions](technical-decisions.md).
- Plain GameObjects and C# only. Scale tooling waits for profiling evidence.

## 3. Vertical slice — not yet defined

**What a vertical slice is:** a short, finished-quality piece of the real game
that shows every core part working together at the intended quality.

It will be described later, once the prototype has answered the question above.
Until then, no work or document targets it.
