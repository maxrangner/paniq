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

A prototype is **not** a small version of the finished game. It has no fixed
end state, no required feature list, and no promise that any layer survives.
A layer that does not feel right is reworked or thrown away. Its job is to
find out, by playing, what Paniq should feel like.

Rules for prototype work:

- Add one stone at a time and keep the prototype playable after each.
- Every stone still obeys the foundation notes (replayable seeded
  simulation, stable IDs, cause-and-effect events, visuals that only observe).
- Each stone updates its own prototype note, the
  [prototype roadmap](roadmap.md), and any defaults in
  [technical decisions](technical-decisions.md).
- Plain GameObjects and C# only. Scale tooling waits for profiling evidence.

## 3. Vertical slice — not yet defined

**What a vertical slice is:** a short, finished-quality piece of the real game
that shows every core part working together at the intended quality.

It will be described later, once the prototype has taught enough about what
Paniq should be. Until then, no work or document targets it.
