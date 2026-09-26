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

## Prototype 3: gameplay, first batch (2026-09-25)

The owner's seven notes for prototype 3: the level as an obstacle. Every
default below was chosen on the owner's behalf and is theirs to overturn; the
owner's own rules are marked. The decisions the owner made when asked: the
work goes on a new branch `feat/prototype-3-gameplay`, and a strong person
gets through a held door in one push.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Where the fire starts** | `FireSettings.SpawnAreas` is one area, the meeting room less a 500 mm wall margin: `(-5500, 1500, 9500, 16500)` | **The owner's rule:** always in the meeting room, anywhere in it. Every seed is a different day from here on, so all thirteen fingerprints were re-recorded | The owner wants another room |
| **What the tower is** | Eight ordinary boxes (ids 3701-3708, 600 mm, 13 kg) authored stacked four high in two columns at (13600, 6350) and (14200, 6350), in the crossbar's south-west corner half a metre east of the archway's wall line; a `TrapDefinition` (id 7001) names the archway (2016) and the boxes | **The owner asked** that the boxes stay physics objects people can carry off. Half a metre off the wall line so the standing tower is not already "wedged in" the archway; stacking any height needed `RestingHeight` to count the things already stacked, not just the one on the floor | The owner wants it elsewhere: eight coordinates |
| **Pinning** | `PhysicsBody.Pinned`: the engine holds it (kinematic), `SetMotion` ignores every push, a blast leaves it be. The tower is pinned while it stands and each box is pinned while it lies in the heap. A standing tower box may not be taken at all (`PhysicsObjectSystem.CanLift` refuses it, and every way of taking a thing -- tidying, wedging, throwing clear, hurling aside at a run -- asks `CanLift`); a heap box is pinned with `mayBeTaken`, and lifting, throwing or shoving it unpins it | The crowd walking past to the toilet all day must not be what brings the tower down, and a running crowd must not kick the heap apart -- "most important is that it blocks the passage" (the owner). The code review (2026-09-26) found a strong runner barging past could fling a standing box aside and a tidy person carry one off; the heap's shove used to move nothing | A tower the crowd can topple is wanted |
| **What the fall is** | The archway becomes a shut doorway (`DoorRuntime.Piled`, state `Unlocked`, still a hole) and the boxes are placed by the simulation in a row along the wall line 350 mm inside the corridor, a second row on top; the presentation draws any box that moved more than a metre in one tick as a half-second tumble | A guaranteed block, and everything a doorway already does for free: its plug keeps bodies out, `FireCanCross` refuses it, routes go round it, and at it people do what they do at any wedged door. Trusting the physics engine to land eight boxes across a 2.4 m gap is not a guarantee | The placed fall looks fake in play: then a real physical tumble with the plug as the guarantee |
| **Which side the boxes lie** | The corridor's side of the wall line, not the crossbar's | Flames only heat what is in their room (`FireSystem.FindTouchingSweep`): laid beyond the line the boxes were a wall the corridor fire could never burn, and the trap held the fire for ever | Never |
| **The trigger** | Fire lit (`FireSystem.Active`) and any participating person within `TrapSettings.TriggerRadiusMillimetres` 2000 of the tower's centre; lowest index wins; the fall lands at `ReactionTick()`, 2-8 ticks later | **The owner's rule** for arming and springing; the reaction-lag rule for the beat between. Two metres reaches a runner passing through the archway, not somebody on the far side of the junction | Numbers |
| **What holds the heap** | `PileHoldsAtBoxes` 3: while three or more unburnt boxes lie still in the doorway's own wedge strip (the same `IsObjectInDoorway` test the door uses, so "jammed" and "the heap" are one thing), the archway stays shut. A box in somebody's arms, in the air, out of the strip or burnt out does not count; a loose box that comes to rest in the strip counts again | **The owner:** "if enough people choose to pick up one at a time they could clear it, or maybe a strong person might ram it". Both are the existing wedged-door behaviour: `ThrowClear` for whoever can lift 13 kg, giving up for the rest | Playtesters clear it too easily or never |
| **The heap and the fire** | Nothing new: the shut doorway holds the fire, the boxes catch from the corridor's flames (cardboard, 1.5 s) and burn 8-15 s each, box lighting box, and when fewer than three are left the archway opens and the fire crosses | "Also slowing the fire" (the owner): a shut door buys eighteen seconds, a heap of eight boxes about the same again, and it burns in plain sight | Never |
| **Whoever stands where it lands** | Anybody astride the wall line, or under the boxes on the corridor side, is shifted clear (`PeopleBodies.ShiftTo`, a metre at most) and knocked down away from the line (`AgentKnockedDown`, cause `BoxTowerFell`), `KnockClearMillimetres` 800 | A body cannot be left inside a pinned box; shifted first, because the engine pushes an overlapping body wherever it likes | Never |
| **What else the crash does** | A `Crash` sound of 12 m (calm people look, nobody is frightened by the noise alone); everybody frightened within it thinks again at their reaction tick | The way they were running for may just have shut: the reverse of "a way out that has just opened" | Never |
| **The events** | `TrapTriggered` (background), `BoxTowerFell` (middling uproar, a sign "the boxes came down!"), `BoxPileCleared` (good news, "the way is clear!") | The story has to be able to say why the corridor is shut | Never |
| **The pull station** | One, id 6001, at (-5700, 8700): the corridor's north wall at its west end, beside the maintenance door and past the meeting room's door. `AlarmSettings.ReachMillimetres` 4000 -> 8000 | **The owner's rule:** one alarm, close to the fuse box room, out of the way so only the brave use it. At four metres nobody in the office or the corridor would ever have considered it; the bravery filter (6+) already exists | Playtesters never see it pulled |
| **No purse** | `InfluenceSettings.Enabled`: off, every price is nought, nothing is paid in, `RunSnapshot.InfluenceEnabled` hides the bar, the prices and the "not enough" lines. Off on the office level through a new `LevelDefinition.influenceEnabled` (false), while the code default stays on | **The owner's rule:** remove influence for now, keep the system intact. A level flag rather than a scenario default so the forty tests of the purse still test it and the fingerprints still pay for their scripted cards | The owner wants the purse back: one tick box on the level asset |
| **Holding a door** | `PlayerCommandType.HoldDoor` / `ReleaseDoor` (free). The button down on a door for longer than `DoorClicks.WindowSeconds` 0.3 is a hold, and the click that began it is not sent; the second click of a double click never becomes a hold; the button up ends it; pausing releases it. `DoorRuntime.HeldShut`: an open door is pulled shut when its doorway is clear (retried each tick), `Open` refuses, `ToggleLock` refuses. A locked door, a swing door, a gap or a broken door takes no hand: `HoldShut` refuses it and the input never sends it; a door that breaks lets go of the hand on it. The HUD says a door is held only when the run's snapshot does | Reuses the one window the player already knows; a press that outlasts it was never a click. Released on pause because nothing pressed while the world is stopped reaches the run. A locked door needs no hand, and a hand on one let the strong through a lock in one push | A playtester wants to hold and lock |
| **The strong and a held door** | Somebody whose shove would damage a door at all (strength >= `DoorBreakMinimumStrength` 7) bursts a held, unlocked door with one push: `Batter` with the door's whole strength, whoever shut it last (the rule that people never batter a door they shut themselves does not apply: the hand on it is the player's). A held door somebody has also locked is battered as a locked door. Everybody else gives up as on a wedge (`writeItOff: false`) and comes back once it is let go of | **The owner's choice** when asked: "yes, strong one gets it in one push" | The owner |
| **Every person's door check** | `DoorSystem.CanBePushedOpen(door)`: shut, unlocked, nothing wedged, nobody holding, no heap. Replaces five copies of "unlocked and not obstructed" in `DoorBehaviour` and `ErrandBehaviour` | One question, asked in one place, so a held or piled door cannot be forgotten by one of them | Never |
| **Poking** | `PlayerCommandType.PokePerson` (free; refused for an unknown person, nothing for the dead or escaped). At once, for somebody on their feet and not sitting: `Slide` 200 mm backwards from their facing and `Stagger`. At `ReactionTick()`: `AgentPoked` (the `!`) and, for a calm loiterer, a look round of two glances (`CalmBehaviour.LookRound`, the same look round a calm person does anyway) before the calm behaviour picks something else; the third poke inside `AnnoyedWindowTicks` 500 is `AgentAnnoyed` (an orange `#!`, "leave me alone!", one glance). A poke while a reaction is already due folds into it, as `ThinkAgainSoon` does for decisions: one look round, at the first poke's time, naming the first poke. Never a fright | The jab is the player's act and lands at once; the reaction obeys the lag rule. A poke is a nuisance, and the crowd's fear is for threats | Playtesters want a poke to frighten, or to hurt |
| **Picking whom to poke** | `PlayerInput.NearestPerson`, the screen-space pick built for the cards, after the ray has found no alarm and no door | Already there, already tested | Never |
| **The scene baker** | Not extended: a trap is code, not a scene component | No level is baked from a scene today | The next level is |
| **The purse's switch** | `LevelDefinition.influenceEnabled` defaults to on, so a new level has a purse unless it is switched off on purpose; `TheOffice.asset` switches it off. Every price passes through `InfluenceSystem.Priced` and every payment through `Credit`, the two places the switch is read | The switch used to default to off, so any new level asset would have played with everything free | Never |
| Versions | `SimulationCompatibilityVersion` 66 -> 67; `ContentRevision` 79 -> 80. All thirteen fingerprints re-recorded: the fire moved to the meeting room, so every seed is a different day. Then 67 -> 68 for the code review's fixes (2026-09-26): ten of the thirteen re-recorded, because in those runs somebody used to take or knock a box off the standing tower | New commands, new rules and a new floor | Never |
| Tests | `BoxTowerEditModeTests`, `HeldDoorsEditModeTests`, `PokeEditModeTests` new; `DoorClicksEditModeTests` for the hold; `EconomyEditModeTests` for the purse switched off; `AlarmsEditModeTests` moved to the corridor's west end (the office's station is gone); `FireStartAreaEditModeTests` for one room. Two scenes repaired for the new day rather than the new rules: the hard-knocks knock-out now stands still and never trips (the seed's later draws had moved and a random trip added a second knock-down), and the press watch no longer counts somebody lying down inside a bathroom stall, whose body is longer than the stall is wide (seed 45), up to 150 mm into its wall (`PressWatch.DeepestLyingInAStallMillimetres`), so a body going through a stall's wall is still caught. The code review's fixes (2026-09-26) are proven in `BoxTowerEditModeTests` (nobody takes from the standing tower, anybody from the heap), `HeldDoorsEditModeTests` (a locked door takes no hand, a burst door lets go, the strong burst a door they once shut), `DoorClicksEditModeTests` (a double click is never a hold), `PokeEditModeTests` (two quick pokes are one look round) and `LevelSessionEditModeTests` (a new level has a purse) | -- | -- |

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
