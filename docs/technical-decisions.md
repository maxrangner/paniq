# Technical decisions

## Chosen foundation

| Area | Decision | Reason |
| --- | --- | --- |
| Engine | Unity 6.3 LTS (`6000.3.24f1`) | Supported Windows workflow, C#, mature 3D tools, and a future ECS path. |
| Rendering | URP | A scalable Unity rendering path suitable for desktop and later mobile quality profiles. |
| Initial runtime model | GameObjects and C# | Keeps the prototype approachable and fast to iterate. |
| Input | Unity Input System | Keeps device input separate from game intent. |
| Tests | Unity Test Framework | Supports edit-mode logic tests and play-mode scene-flow tests. |
| Version control | Git | Text Unity assets are tracked; LFS waits for large source media. |

## Level mode, and the order of the audit (2026-10-03)

The owner: "if every change is several hours I can't progress ... get one
level working and THEN work the fingerprints etc when the level is done."
Measured: the hours went into proving numbers about a level still changing
shape (machine-played rounds before the owner played, the office kept
byte-identical behind switches, documents and versions per batch), not into
the 6.5-minute suite. The owner's decisions when asked: rebuild how people
decide (the audit's one task model) **before anything else**, and **set
aside** the behaviours the loop level does not use.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Level mode** | Replaces *Sketch and keep* (binding text in `AGENTS.md`): compile check, smoke check, local save point, three-line report per change; proving once, in a hardening pass | **The owner's rule** | The level is done |
| **The smoke check** | `SmokeEditModeTests`, category `Smoke`: the five levels, seed 41, once each; a fire round must end within three minutes (the old office excepted, known not to); about twenty seconds | The quickest test that a broken build never reaches the owner | It grows past a minute |
| **Other levels may change** | No new "old way" switches; the office and the test levels take the new rules too | A switch per rule doubled the code for a level nobody plays now | The office is played again |
| **Measurements on request** | `LoopMeasurements` / `LoopPlayers` stay, `[Explicit]`, run only when the owner asks a question they answer | Machine players are not people; their numbers gated every hand-over | Never as a gate |
| **Cards and purse deleted** | Removed from the code, the levels and the screens; git history keeps them | The owner removed cards on 2026-09-30; the purse still ran every tick and three test levels quietly switched it back on | A level wants cards |
| **Set aside** | Keycard fetching, the cruel wedging doors, toilet trips, the brave fighting fire unasked: not carried into the rebuilt decisions | **The owner's choice**; saves two to three days of the rebuild | A level needs one back (about a day each) |
| **Test tools** | A comma filter from Bash runs every name; each half keeps its own results file; an inconclusive test fails; the compile check says "open the editor once" instead of hundreds of errors, compiles `Authoring` as the player does, and reads the version define from the installed editor | Audit G4-G7: each cost a test cycle | Never |
| **Also set aside** | Leaders no longer send anybody at the fire with a bottle (`TryOrderTheFireFought`, `LeadersLeaveTheFireAlone`, `OrderedFightMinimumBravery` gone); nobody locks the building's way out behind them (`PeopleLockTheWayOut` gone; inside doors still) | The same as the owner's set-asides: fighting the fire is the hand's to ask, and one bully locking the way out made a whole round a total loss. The loop level already had both off | A level wants them |
| **The one chooser** | `Tasks` (one table of what each activity is), `TaskChooser` and `ITaskOption` (carry on every tick; take up only at a decision moment, `AgentIntent.NextChoiceTick`), `Tasks.End` (done / gave up / interrupted, the same for every task) | Audit A1 and D: options were asked to take themselves up every tick with routes inside; the hand's give-up rules differed by task | Every option scores itself instead of keeping a fixed order |
| **What brings a decision forward** | The hand reaching somebody, or their conviction crossing the answer line; a wind-up that passed; an order shouted at them; anything that already called `ThinkAgainSoon` | Without these, a person would answer the hand up to a second late | Never |
| **Orders and noises as offers** | A leader's "break that door" is taken up by the person in their own turn a reaction later (`LeaderBehaviour.TakeUpAnOrder`); a noise is turned to in the hearer's own turn (`SoundSystem.TakeUpTheLook`), and waits while they are getting into or out of a chair | **The owner's rule**: nobody reacts on the tick a thing happens (audit E1, E2) | Never |
| **Everybody's rules, from the loop level** | Their own dice, choices about the heat that stick, heat judged three metres from a door, the long way round weighed, dead ends weighed, 6 % freeze for good, one click wakes the frozen, a held door walked through, the hand let go strands nobody, no fire through walls | Level mode: rules about how people behave are no level's own | A playtest says one of them is wrong |
| **Dragging a body** | Swung into line by a turn (`PhysicsWorld.SwingToward`), never hauled into a wall; getting up only onto room floor or a doorway | Audit E5 (a rotation written every tick), and a body dragged into a wall got up outside the building | Never |
| **The calm's choosing** | The same chooser (`CalmBehaviour.BuildIdeas`): the hand's push, its pull, a stop after a walk, then one roll in bands as before (tidy, sit, home time, desk, chat, look round), a stroll by default | One way of choosing for everybody, without changing how the calm day feels | Playtesters find the calm day dull or busy |
| **A poke in a wind-up** | A poke while somebody is going stiff catches the wind-up ("caught in time") rather than waking them as if frozen | One click now wakes the frozen; without this the poke beat the tell to it and the end card counted it wrongly | Never |
| **Timers given a person's own length** | The gap between one person's shoves, the rest after heaving a table, the steps of sitting down, a socket's crackle (audit E3). A person's own idea (going home, a chat) still starts on their own tick: it is a deed, not a reaction | **The owner's rule**: nothing happens to a group on one tick | Never |
| **Play-mode wiring tests** | Ask for the office by name (`LevelSession.RequestLevel`) | The loop level opens first since 2026-10-03; these check the office's doors, bin and start card | Never |
| **Saving in level mode** | Save points folded into one ordinary commit at the end of each working session; nothing pushed unless the owner asks | **The owner**: the level will take weeks and many commits; a history of sessions reads, one giant commit at the end does not | The owner wants it otherwise |

## Alignment with the three requirements for the finished game (2026-09-24)

The owner stated three requirements for the finished game (recorded in the
[game vision](game-vision.md#decided-three-things-the-finished-game-must-be))
and asked that the documents and the work line up with them. This is the check
of the foundation against each, and the two decisions the check produced.

### 1. Very optimised for large crowds, with a lot of emergent behaviour and events

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| One deterministic tick; a spatial index behind every per-person question; flow fields shared by everybody heading the same way, with a fixed per-tick budget; a display that allocates nothing a tick; fire drawn in batches; a causal event log where every event names its cause, which is what makes the chains readable and what the economy pays on. Measured at 500 people panicking in the editor: about 12 ms a tick, 5 of them physics | People are about 25 scene objects each (fine at 200, unmeasured at 500); steering and wayfinding have never been profiled on a large floor; every number is an editor number | -- | Large is the stated goal: 200 to 500 people on 30 to 50 rooms. The evidence gates for scale tooling (below, under "Deferred technology") stay: profile first, adopt only when the current approach blocks the intended scenario |

### 2. Flexible for different dangers; a reaction is a feeling about a situation, never a response to a named event

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| Danger is generic: `IThreat` answers what a frightened person may ask of any danger and `Threats` answers across all of them. Fear changes (calm, alert, scared, freezing) come from perception (sight of a threat), sound (a noise with a position and a reach: a yell close enough to be understood alarms, a thud only turns heads, a bang frightens) and contact. None of these read an event's name: a bang frightens because of its reach, not because it is called "MicrowaveExploded". The event log records causes; it does not drive reactions. The economy (`InfluenceSystem.UproarTierOf`) keys on event types, which is right: it is the score, not the crowd | The inner life is one axis, fear, shaded by seven traits and a temperament. The panic options (flee, fight the fire, help, lead, sound the alarm, barricade) are a fixed list, and some are tools for one danger (extinguishers, flammables) and belong to it. Feelings spread one way only today: a yell alarms | Anger, trust in a leader, curiosity: none exists | **Feelings are named now and built as dangers need them.** The vision names them; each arrives as per-person state with the first danger or card that needs it, as its own stone. The rule is a design constraint above: no rule in the crowd may switch on an event type. Revisit if a danger cannot be expressed as answers to the threat questions plus a feeling; that is the signal to extend the interface, not to special-case the danger |

### 3. Handcrafted levels with dynamic scenery and props, possibly on more than one floor

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| Levels are placed by hand (`PrototypeBuilding` in code, and the scene bake tool that reads placed objects into scenario data); the level asset and session exist. Props are physical bodies: shoved, thrown, tipped, broken, burnt. Tables are bodies too, and the walkable floor is redone when one is shoved. Walls are blown through; doors break | Rooms are rectangles, joined into L and T shapes by archways; authoring in C# will be the slow part of a large floor | Everything is one storey: a position is X and Z (`LogicalPosition`), rooms are flat rectangles, the navigation squares, the fire squares and the index are one layer, and the physics scene has one floor | **Storeys are prepared for now and built later.** From the next level onward, rooms and positions carry a storey number and a stair is a kind of door between storeys, while every level is still one storey. Stairs, lifts and falls between floors stay unbuilt until a level asks. Why now: a few days once, before a big level exists; a month afterwards, because it touches positions, rooms, both grids, physics and the display in one go. Revisit never; the trigger for building stairs is the first level that wants a second floor |

## Older decisions

Decisions from finished stones are kept, unchanged, in `docs/history/`, so this
page stays short enough to read in full. Search there when you need the reason
behind older code:

- [Prototype 1](history/decisions-prototype-1.md) -- the fire-reaction office
  bring-up, and rounds, scoring and the design spine.
- [Prototype 2](history/decisions-prototype-2.md) -- the round, the playtest
  rounds, the cue system, the office floor, the cards and the alarm.
- [Prototype 3](history/decisions-prototype-3.md) -- gameplay: the tower and
  the Director, the keycard, the hand and its four passes, tells, the test
  levels, the office re-dressed and the measurements behind each.
- [The review refactor](history/decisions-review-refactor.md) -- the six phases
  of the 2026-09-23 review and where the foundation stood afterwards.
- [Version history](history/version-history.md) -- every bump of the rules
  version and the building version.

New decisions are added to this page, under the current stone. When a stone is
finished, move its sections to a new `docs/history/decisions-<stone>.md`.

## How decisions are made

Paniq's owner is learning game development, so technical decisions must remain
teachable and reversible. Before adding a package, workflow, or major system:

1. Research the current official documentation and inspect the existing project.
2. State the problem in game terms, then give one plain-language recommendation.
3. Record why it is needed now, what simpler option was retained or rejected,
   relevant source links, and the condition that would trigger a later review.
4. Explain the implementation result and exact local Unity steps needed to
   verify it.

Ask the owner only to choose genuine product direction, such as the intended
player experience or visual tone - not engine settings they have no reason to
know yet.

## Design constraints for future expansion

The authoritative rules for replayable simulation are in
[Simulation contract](simulation-contract.md). They apply before any gameplay
system is introduced.

- Keep interaction data explicit: source ID, event type, world position,
  strength, duration, and causal parent.
- Keep configuration in scenes and ScriptableObjects rather than hidden static
  state.
- Keep random choices tied to an explicit scenario seed.
- Keep presentation separate from future gameplay/simulation decisions.
- Prefer simple steering and authored obstacles before adding navigation.
- A room or a position may carry a storey number; nothing new may assume the
  building is one storey (decided 2026-09-24, see the alignment section).
- A person reacts to a situation through a feeling; no rule in the crowd may
  switch on an event type (decided 2026-09-24, see the alignment section).
- Nobody reacts on the tick a thing happens, and nothing happens to a whole
  group on exactly the same tick: every reaction begins a few ticks late
  (`SimulationContext.ReactionLag`), no two people finish being startled on
  one tick, every fixed length of time a person spends goes through
  `SimulationContext.Jittered`, and any schedule several people share draws a
  seeded offset per person (the owner's rule, 2026-09-24, see the playtest
  fixes in [prototype 2's decisions](history/decisions-prototype-2.md)).

## Deferred technology

| Technology | Do not add it until |
| --- | --- |
| ECS, Burst, Entities Graphics | A profiled representative prototype scene cannot meet its frame-rate target because of crowd update or rendering cost. |
| AI Navigation | A prototype stone needs pathfinding that simple steering and obstacle avoidance cannot provide. |
| Cinemachine | The hand-authored camera prevents a required player experience. |
| Addressables | Content size, loading needs, or platform packaging makes direct references difficult to manage. |
| Mobile settings | The Windows prototype is stable and a target device is available for measurement. |
| Unity Physics (DOTS) | Standalone profiling shows the PhysX step over 3 ms a tick at 500 people and 1,000 things after the cheaper fixes, or replays must match across different kinds of computer. |
| VFX Graph | The built-in Particle System costs more than 2 ms a frame in the worst blast, or an effect needs tens of thousands of particles, such as smoke filling a building. |

Any addition above requires a short dated note here recording the measured or
feature-driven reason and the expected benefit.
