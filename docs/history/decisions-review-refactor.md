# Technical decisions: the review refactor

The six phases of the 2026-09-23 code review and the assessment written
afterwards. Moved here from [technical decisions](../technical-decisions.md)
unchanged.

## Review refactor, phase 1: afraid of a threat, not of the fire

Chosen on 2026-09-23. The owner asked for a full review of the code against the
game the docs describe and accepted its plan in full; the review is in the
session's plan file and its findings are summarised in the
[roadmap](../roadmap.md). This is the first of six phases, and every phase that is
a restructure keeps all thirteen replay fingerprints.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The crowd fears a threat, not the fire** | `IThreat` (what a frightened person may ask of a danger: nearest part, in this room, closer than, in sight, on a route, touching, and what touching does) and `Threats` (every threat in the level, answered across all of them). The fire implements it. Fear, perception, panic, doors, helping, barricading, the round clock, the body's death cause and the physics read-back ask `Threats`; extinguishers, flammables, door scorching, the fire card and the fire-waking still ask `FireSystem` | Seventeen classes named the fire (129 references), and the roadmap's next stone -- a hunter -- names "afraid of the fire into afraid of a threat" as its real work. The seam is cheaper before the hunter than after. With one threat in the list every answer is the fire's own, so all thirteen fingerprints held | A threat that needs a question the interface does not ask; add it to the interface, not to the crowd |
| The fire's signature is its burning-square count | `IThreat.Signature` for fire is exactly what the round clock watched before | Anything else would have moved the stall clock and with it a fingerprint | A hazard whose change is not a count |
| Hearing is the threat's own | `IThreat.HeardWithinMillimetres`, 0 for a silent threat; the fire's is the old crackle radius | A hunter growls or is silent; that is the hunter's business | -- |
| The trigger starts every threat | `Threats.RequestStart` asks each | One button sets the disaster going, whatever it is made of | A level wants threats that start on their own clocks after the trigger; that is a per-threat delay, not a second button |
| One `Bind` pass instead of eleven setters | `Systems` names every system; a system built before something it needs implements `IBindable` and is handed the finished set once. `UseCrowd`, `UseBody`, `UsePeople`, `UsePower`, `UseObjects`, `UsePhysics`, `UseDoors`, `Use` and `Offer` are gone | Each new system was adding another setter and another ordering rule to a constructor that already has to protect the start-up random-draw order. Binding stores references only, so its order cannot move a run | A system needs something at construction that only exists later, which binding cannot give; then the construction order itself has to change |
| Every event type says what it pays | `InfluenceSystem.UproarTierOf` names all 75 event types, and an event left out throws rather than paying nothing. `UproarTableEditModeTests` walks the enum | The switch had a silent default, so a new event landed in the wrong tier by omission and no test could tell | -- |
| A test double for a threat | `ThreatSeamEditModeTests.StationaryThreat`: a silent spot that trips whoever touches it. Five tests: noticed and blamed, run from, got by, watched by the round clock, started by the trigger, all with no fire lit | This is the roadmap's own check for the hunter stone: "the existing fire behaviour is unchanged by the generalisation, plus tests for chase and conversion" -- the first half now, the second when the hunter is built | Never |
| Versions unchanged | `SimulationCompatibilityVersion` stays 42, `ContentRevision` stays 54 | Nothing a run produces moved; the fingerprints prove it | -- |

## Review refactor, phase 2b: nobody reads the whole crowd to decide for one person

Chosen on 2026-09-23. Second phase of the review refactor (the walking-distance
route costs, phase 2a, are their own entry). A restructure: all thirteen replay
fingerprints held, and the panic measurement below ends with the same number of
people scared, down, alight, out and dead before and after, tick for tick.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Every per-person question reads the index** | `SomebodyElseLinedUpAt`, `SomeoneComing`, `IsRoomFull`, `IsDoorwayClear`, `SomebodyBeyond`, `Rally`, `NearbyBest`, `TryStartSocialising`, `SprayOnce`, `SpreadFlames`, `Detonate`, `FlingFrom`, `BlastWall`, `OfferItToWhoeverCanSeeIt`, the barricade's `ChooseItem`, and the flammables' "burning people light things, burning things light people" all ask `Crowd.Within`/`Gather` or `PhysicsObjectSystem.Gather` for the patch of floor they care about and keep their own exact test. Two new areas in `WorldGeometry` say where to look: `PersonDoorwaySearchArea` (every point `IsInDoorway` could accept) and `RoomAreaWithDoorways` (a room plus its doorways) | Sixteen loops walked every person or every thing to answer a question about one person, and were asked once per person per tick in a panic: the cost grew with the square of the crowd exactly when the game is busiest. The index hands back candidates in ascending order, which is the order the old loops drew random numbers in, so the answers cannot move | A question the index cannot bound (the announcement of ways out still walks the crowd once per announcer; it is rare) |
| Who is already helping, who is already wedging | `HelpBehaviour.helpedBy[person]` and `BarricadeBehaviour.barricaderOf[door]` remember the last person who set out, and are believed only while that person is still at it | The help table was rebuilt from the whole crowd every time anyone kind thought about helping, which is every tick while fleeing. Each door and each casualty has at most one taker at a time, because the only place a taker is set is right after the check | A second way to start helping or wedging is written; it must set the same entry |
| Which room, from the grid | `RoomAt` and `RoomAtPoint` ask the navigation grid for the square's room first and trust it only when the footprint really is inside that room; otherwise the rooms are walked as before | Asked about ten times per person per tick over every room's rectangle. A square under a table is marked as no room, and a wall that does not sit on a square edge can put a point in the room next door, which is why the exact check stays | -- |
| Things by ID, in one step | `Crowd`, `PhysicsObjectSystem` and `DoorSystem` keep a dictionary from ID to index, built once; `GetAgent(id)` and `IsHeadingForWayOut` use the crowd's. `PhysicsObjectSystem.Equipment` lists the extinguishers once, so a fetch is not a walk of every box | A click on a person or a thing walked the list; harmless at twenty, not at five hundred | -- |
| No array per burning tick | `BurningBehaviour` keeps one buffer of who is alight | It made a fresh array every tick anybody burned | -- |
| **The panic measurement exists** | `CrowdScaleMeasurements.HowLongAPanickingBuildingTakes`: the stress building with the fire lit in the first room and everybody frightened on the first tick, at 100, 200 and 500 people; reports the average tick, the physics share, the slowest tick and how the crowd ended | Every measurement before this was of a calm crowd; the review found the costs that grow with the square of the crowd live in the panic, and nothing measured it | Every stone that raises the crowd or the level size, as the quality checks already require |

The numbers, in the editor, before and after this phase. The crowd ends the
same way both times, which is the proof the answers did not move. The
timings are within run-to-run noise: at these sizes the scans that were
removed were not where the time went, and the phase is justified by what it
removes (growth with the square of the crowd) rather than by a saving measured
today. What remains outside physics, about 7 ms a tick at 500 people
panicking, is steering and wayfinding, and is the next thing to profile when a
larger level is authored.

| Building | Before | After |
| --- | --- | --- |
| 100 people calm, 200 boxes | 1.59 ms a tick (physics 1.24) | 1.58 ms (physics 1.24) |
| 200 people calm, 400 boxes | 2.77 ms (physics 1.96) | 2.78 ms (physics 1.97) |
| 500 people calm, 1000 boxes | 10.37 ms (physics 4.76) | 10.29 ms (physics 4.74) |
| 100 people panicking | 3.05 ms, slowest 6.73; 11 down, 13 alight, 0 dead | 3.02 ms, slowest 6.84; 11 down, 13 alight, 0 dead |
| 200 people panicking | 7.54 ms, slowest 11.85; 28 down, 7 alight, 1 dead | 7.41 ms, slowest 9.39; 28 down, 7 alight, 1 dead |
| 500 people panicking | 12.70 ms, slowest 16.33; 45 down, 4 alight, 4 out | 12.24 ms, slowest 16.10; 45 down, 4 alight, 4 out |

At 50 ticks a second the simulation has 20 ms a tick before it cannot keep up
at all, and it shares that with drawing; 500 people panicking in the editor
keeps up, and a built game is faster than the editor.

## Review refactor, phase 2a: a route costs the walk, not the line

Chosen on 2026-09-23. The one phase of the review refactor that changes what a
run does, so it has its own commit and its own versions.

**What a player sees.** Somebody choosing between two ways out of a furnished
room now picks the one that is the shorter *walk*, where before they picked the
one that was nearer as the crow flies and then walked the long way round the
furniture to it. On the shipped floor the two mostly agree, so most recorded
runs did not change.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Route legs are walking distances** | `WorldGeometry.FindRoute` still searches the rooms as a graph, but each leg -- from where the traveller stands to the first door, then door to door across each room -- costs what the flow fields say it is to walk, round tables and through doorways. The distance to a door is measured to the spot just inside it where somebody stands to go through | The room graph and the flow fields were two maps of the same building that could disagree: the graph chose the door, the fields walked there, and the decision log had already written down that a straight line "does not know a table is there". Staged levels with several ways out are exactly when the choice matters | A floor plan needs rooms that are not rectangles; then the graph's rooms need reworking, not its costs |
| One table per door and side, kept | The walking cost of every square to each door is worked out the first time that door is asked about, kept as one array of integers per door and side, and thrown away when the floor changes shape (a hole blown, a table shoved). A leg is then one look-up | A route search runs for everybody who plans, every time they plan; working a field out per search would be the cost the navigation budget exists to stop. The tables are built outside that budget so a plan never has to wait for a busy tick | Memory: a floor of fifty doors on a large grid is a few megabytes of tables, which is fine; a hundred floors at once would not be |
| A leg is the walk the feet will take, even through the room next door | The fields are the building's, not the room's: where the shortest walk from a door to the next cuts through a neighbouring room, the leg costs that walk. In the test, a table across the cafeteria makes the walk to its shortcut door go out by the corridor door and round, and the route is costed as that | It is the same field the person then steers by, so the cost and the walk agree, which is the whole point. Confining a leg to its room would cost the route more than the feet pay | A route's named doors must be the only doors walked (for instance a door that must be *known* to pass); then the fields need to know about knowledge too |
| Where the squares cannot say, the line | Off the grid, on a square nobody could stand on (shoved into a table), or with no way at all, a leg costs the straight line from door centre to door centre as before | The old answer is a safe floor; refusing a route would strand somebody who has merely been pushed somewhere odd | -- |
| Versions | `SimulationCompatibilityVersion` 43 -> 44; `ContentRevision` 55 -> 56. Three of the thirteen fingerprints re-recorded (seed 42 with cards, seed 46 doors locked, seed 42 cards with no visitors); ten happen not to change | Which door somebody runs for is a rule of the run, so an old replay cannot be played against it | Never |
| Test | `WayfindingEditModeTests.ARoute_CostsTheWalkRoundATable_NotTheLine`: the cafeteria's shortcut costs about the line with the floor clear, and half as much again at least with a nine-metre table across the room | The behaviour change the phase exists for, pinned | -- |
| What it turned up | The honest costs made a stranger bounce through one doorway for ever; that was a weakness of the search, not of the costs, and is fixed in its own entry above ("a stranger looks round the room they walked into") | -- | -- |

## Review refactor, phase 3: the dead weight out

Chosen on 2026-09-23. Third phase of the review refactor; a restructure, and
all thirteen replay fingerprints held.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The pre-physics movement rules are deleted** | `WorldGeometry.KeepObjectInRoom`, `ClipsDoorFrame` and `TableHit` had no callers. `DoorwayStrip`, `CanUseDoorway` and `IsWalkable` existed only to serve `ClampIntoWalkable`, whose one live caller was the spot a helper drags a casualty to; that is now `ClampIntoRoom`, ten lines: a spot already on some room's floor stays, otherwise it is pulled into the room the helper stands in (or the nearest room from a doorway) and off its tables. `PhysicsWorld.RemoveTable` (for tables that smashed, which nothing does any more) is gone, and the tables are handed to the squares as they are rather than through a "still standing" filter that filtered nothing | Dead rules make the world code harder to change safely: a reader cannot tell which of two ways of moving is the live one. Since people and things became physical bodies the engine resolves movement, and these described the sweep-and-refuse world before it | -- |
| One difference, kept small | The old clamp let a helper standing in a doorway aim into the doorway's strip; the new one pulls that spot into the nearest room instead. No recorded run does this; the fingerprints say so | A doorway is nowhere to lay a casualty down | -- |
| **The runner that needed no editor is deleted** | `tools/RunEditModeTests.ps1`, `tools/Stubs/` and `tools/TestRunner/` (about 1,300 lines) are gone. `tools/CompileAgainstUnity.ps1` (compile only, no editor) and `tools/RunUnityTests.ps1` (the open editor's own runner) stay | Its own decision-log entry named the condition for retiring it -- "the simulation starts needing real engine types" -- and the physics engine met it: the stand-in threw for anything that builds a run, so nearly every test was skipped, while the workflow document still advertised "the whole suite in about twenty seconds". A harness that skips the suite is worse than none, because it looks like a check | Unity ships a test runner that tolerates an open editor; then the bridge could go instead |
| The superseded movement rules are a history note | `docs/spatial-world-rules.md` keeps two lines saying what the sweep-and-refuse rule was and when the physics step replaced it, instead of the full rule and its worked examples | A hundred lines of rules marked "superseded" are a hundred lines a reader has to check are still superseded | -- |

## Review refactor, phase 4a: the display fills two snapshots instead of building one a tick

Chosen on 2026-09-23. Fourth phase of the review refactor; a restructure, and
all thirteen replay fingerprints held.

**What a player sees.** Nothing today. What it removes is a cause of hitches
that would have arrived with a bigger crowd: every tick the run built a fresh
copy of everything the display reads (people, doors, every loose thing twice,
tables, the hand, the sparks, two cost tables), tens of kilobytes fifty times a
second at a few hundred people, and the runtime pausing to tidy that memory is
what turns into a stutter.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Two snapshots, filled turn about** | `RunSnapshot` is now a set of buffers sized once for a run, and `Run.FillSnapshot` writes into one. `RunDriver` keeps two, made by `NewSnapshotBuffer` for each run: the display blends from last tick's to this tick's, so the one it is not still reading is the one filled next. `GetSnapshot` still makes and fills a fresh one, for tests and tools that keep what they get | Drawing a tick now allocates nothing but the two small views over the append-only fire and event lists (which are prefixes of the run's own lists, not copies) | A third reader of live snapshots appears (a replay scrubber, say); it needs its own buffers, not the runner's |
| Placed doors and the hand as prefixes | `Prefix<T>`, the first *n* items of a buffer sized for the most there could be (every door slot; sixteen cards), read as a list without copying or boxing | The number of placed doors grows when a hole is blown and the hand grows and shrinks; a fresh array per tick was the old answer | -- |
| The cost tables once per run | What each card and each door click costs is worked out once, the first time a snapshot is asked for, not once a tick with a reflection call (`Enum.GetValues`) each | They are settings, and settings do not change in a run | A card's price ever changes mid-run; then the table is filled each tick again, still without reflection |
| The stress profile measures the panic too | `StressProfiler` runs the panicking building (fire lit, everybody frightened) after the calm one, at 100, 200 and 500 people, so the standalone numbers cover the case that matters | The editor measurement of phase 2b exists; the built game is the number the budget is written against | Every stone that raises the crowd, as the quality checks already require |

## Review refactor, phase 4b: the fire is drawn in batches, not as objects

Chosen on 2026-09-23. Presentation only: the rules did not change and no
fingerprint could.

**What a player sees.** The same fire: a dim scorched tile per burning square
with two or three small cubes bobbing, spinning, flickering and fading to
embers over it, and an orange glow where a blaze is behind a wall. What changes
is what happens when a large floor is fully ablaze: the frame rate no longer
falls off a cliff while the simulation is still fine.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **One draw call per colour, however many squares burn** | `FireView` keeps no scene objects. Every tile and flame cube is one entry in a batch, and each batch is drawn with `Graphics.RenderMeshInstanced` (Unity's way of drawing many copies of one mesh in one call; the built-in cube mesh, up to a thousand per call). A flame's colour is rounded to one of thirty steps (six of heat by five of age) and a tile's to one of fifteen, so a fire is at most a few dozen calls; the flicker hides the steps | The old view made a root, a tile and two or three cubes per square and moved, spun, scaled and recoloured each cube every frame. A 40 m by 30 m floor at 500 mm squares is 4,800 squares, so 15,000 to 20,000 objects when it all burns; the decision log of 2026-09-20 named "up to about 1,700 cubes is fine" and instanced drawing as the way past it | A flame needs a colour of its own (a blue chemical fire beside an orange one); then the fire material needs a shader with a per-instance colour, which the standard lit shader does not offer |
| The glow through walls is a batch too | The see-through shader gained the two lines that let it draw batches (`multi_compile_instancing` and the instance ID), and the two fire materials allow instancing. The glow is one more call over every flame cube | Without it the fire would vanish from behind walls the moment it was batched | -- |
| No shadows from flames | The batches cast and receive no shadows and take no light probes | The cubes are tiny and self-lit; a shadow from each was cost for nothing. The old cubes had Unity's defaults, so this is the one visible difference, and it is invisible | Playtesters miss a flicker on the floor around a fire; then a light, not shadows, is the answer |
| The test asks the view what it drew | `BootstrapSceneFlowTests` used to look for a scene object called "Fire cell 1" with three children; it now asks `FireView.DrawnCellCount` and `DrawnFlameCount` through `RunPresentation.FireForTests` | There are no such objects any more, and "what did you draw this frame" is the honest check | -- |

## Review refactor, phase 4c: a full buffer is not an answer

Chosen on 2026-09-23. The last of phase 4; a behaviour fix in cases that
were wrong, with its own version.

**What a player sees.** In a dense crush, once in a while somebody could stand
up inside somebody else, or a door could shut on somebody it should have
refused, or two people could be told a wall was not between them. No recorded
run reaches that density, so nothing recorded changed; a larger level would.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **A look-up that fills its buffer asks again with twice the room** | `PhysicsWorld.IsClearToStand`, `IsClearToLie`, `IsAnyBodyInDoorway` and `IsBuildingBetween` shared two fixed buffers of thirty-two and treated a full one as the whole answer. They start at sixty-four now and double whenever a look-up comes back full, so the answer is always from everything there | A chair is three colliders and a doorway crush is dozens of bodies; thirty-two was a guess from a twenty-person office. Growing on demand costs nothing until it is needed and then once | -- |
| Versions | `SimulationCompatibilityVersion` 44 -> 45; `ContentRevision` 56 -> 57. All thirteen recorded fingerprints happen not to change | Which spots count as clear is a rule of the run, even where no recording exercised the difference | Never |

## Review refactor, phase 5: the code stops calling itself a fire demo

Chosen on 2026-09-23. A rename and nothing else: one commit whose diff is
only names, so no other change is buried in it. No fingerprint could move,
because enum values, settings and rules are untouched.

**What a player sees.** Nothing. What a reader sees is code that says what
the game is: prototype 2 is "the stone of game", with rounds, an economy,
cards and levels, and a hunter stone next, and it was written in classes
called `FireReactionSimulation`, `FireReactionSnapshot` and so on, 2,355
times across 119 files.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The `FireReaction` prefix is gone from every type** | `FireReactionSimulation` is `Run`; `FireReactionSnapshot` is `RunSnapshot`; `FireReactionRunner` (the component that owns the tick) is `RunDriver`; `FireReactionPrototypePresentation` is `RunPresentation`; `FireReactionScenarioData` is `ScenarioData` and the asset class `FireReactionScenario` is `ScenarioAsset`; `FireReactionEventType` is `CausalEventType`, beside `CausalEvent` and `CausalEventLog`; every `FireReaction*Definition` and `FireReaction*Snapshot` simply drops the prefix; every `FireReaction*EditModeTests` likewise | A hunter stone written into `FireReactionSimulation` reads wrong to anyone who joins, and the longer it waits the more there is to rename | -- |
| Files renamed with their `.meta` files | Each renamed script moved with `git mv` together with its `.meta`, so Unity's IDs are unchanged and the scene and the scenario asset still point at the same scripts. The scene (`FireReactionPrototype`) and the asset file (`FireReactionScenario.asset`) keep their names: they are content, referenced by path from build settings and editor tools, and renaming content is not this commit's job | A MonoBehaviour's file must be named after its class, and a moved `.meta` is what keeps the reference | The scene is rebuilt for a real level; then it is named for the level |
| Two names were not free | `EventType` clashes with Unity's own `UnityEngine.EventType`, and `Presentation` with the `Paniq.Presentation` namespace; `Simulation` would clash with `Paniq.Simulation`. Hence `CausalEventType` and `RunPresentation`. `Run` itself clashed with three test helpers named `Run(...)` and a test enum `Run`, renamed `Advance`, `ReplayFingerprint.Of` and `RecordedRun` | C# looks a simple name up in the enclosing class and namespace before it looks at `using` directives, so a helper method called `Run` hides the type `Run` inside that class | A new test helper is called `Run` and the compiler says "'Run' is a method, which is not valid in the given context": rename the helper |

## Review refactor, phase 6: the documents say what the code does

Chosen on 2026-09-23. Documentation and comments only.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| The prototype note's round and camera | `fire-reaction-prototype.md` said a round ends when everyone is "somewhere the fire cannot get to, held for five seconds", and that Q and E snap between four views only. It now says what the code does: a round ends when everybody is out or dead, or when the whole building has been still for long enough (a queue counts as something happening); the view swings freely under a right drag and Q and E snap from wherever it is | The owner reads these notes to know the game; a note that describes a rule that was removed misleads | Each stone that changes a rule; its commit carries the note |
| The exit signs are read | `ScenarioData.ExitSigns` and `ExitSignDefinition` said "nothing in the run reads them" and "for the player's eye only"; strangers read them (`ExitSignBehaviour`, `WayfindingSystem`) | The comment was true when written and false since the wayfinding stone | -- |
| Tables do not smash | Nine comments still described tables smashing to wreckage (they are shoved and tipped, and never smash, since version 39). The `TableWreck` kind stays so an authored one still loads, with a comment saying nothing becomes it | A reader following a stale comment looks for code that is not there | -- |
| The version list is a table | The replay compatibility row of the first table held every bump in one cell of about three thousand characters; it is now a version history table at the end of this document, newest first, and the row points to it | Unreadable as a cell, useful as a table | -- |
| Thirteen fingerprints, not ten | The review refactor's own entries said "ten"; there have been thirteen recorded runs since the no-visitors cases were added. Older entries keep "ten" because that was the count when they were written | -- | -- |

## Review refactor: where the foundation stands afterwards

Written on 2026-09-24, after the six phases were merged into prototype 2, because
the owner asked whether this is a foundation to stand on. It is an assessment,
not a plan: what is solid and why, what still creaks and what would expose it,
and what was and was not verified.

**Verdict.** The foundation is now the right shape for the game the documents
describe: one deterministic tick, a causal event log, a spatial index used
everywhere, one map for "which door" and "how to walk there", a threat seam a
new hazard plugs into, a display that allocates nothing a tick, and a fire that
draws in batches. Two qualifications. It has been proven by tests, not by eyes:
411 tests pass and every replay fingerprint held through every restructure, but
no round has been watched on screen since the fire was batched and the route
costs changed. And it has been measured at 500 people in a small building, not
in a large one: steering and wayfinding, about 7 ms of the 12 ms a tick at 500
people panicking in the editor, have never been profiled on a big floor.

**What was verified.** Edit-mode (395) and play-mode (16) suites after every
commit; thirteen replay fingerprints held through every restructure and were
re-recorded once, for the walking-distance route costs; the panic measurement
before and after the index work ends with identical crowds; the editor log shows
no rendering complaints from the batched fire.

**What was not verified.** A watched round; a floor larger than the shipped one;
the standalone stress profile (every number in this document is an editor
number); the look of the batched fire, beyond the play-mode test that it draws.

### What is solid

| Item | Why it can be built on | Proven by | Revisit when |
| --- | --- | --- | --- |
| Threats, not fire | Fear, perception, panic, doors, helping, barricading, the round clock and contact all ask `Threats`; a new hazard is a second `IThreat`, not an edit to seventeen files | `ThreatSeamEditModeTests`: a stationary threat frightens, is fled from, hurts and holds the round open, with no fire lit | A hazard needs a question the interface does not ask |
| One map | Routes cost what the walk is and the feet steer by the same fields, so the door chosen and the door walked agree | `ARoute_CostsTheWalkRoundATable_NotTheLine`; three fingerprints moved and were re-recorded | Rooms stop being rectangles |
| The index everywhere | No decision for one person reads the whole crowd; the cost is per person, not per crowd squared | All thirteen fingerprints held; the panic measurement ends identically | A question the index cannot bound |
| Nothing allocated a tick, fire in batches, honest physics look-ups | The three things that would have turned a big level into a stutter are gone before the big level exists | Fingerprints held; play-mode fire test; buffers grow on demand | The people (about 25 objects each) need the same batching |
| Names say what the code is | `Run`, `RunSnapshot`, `RunDriver`, `RunPresentation`, `ScenarioData`, `ScenarioAsset`, `CausalEventType` | Both suites after the rename | -- |
| Every default is in this document | Each choice made on the owner's behalf has its reason and the condition for overturning it | This document | -- |

### What still creaks

| Item | What it is | What would expose it |
| --- | --- | --- |
| A route can name doors the feet do not walk | A leg costs the real walk even where the shortest walk cuts through the room next door. Cost and walk agree, which is the point, but a stranger's "known doors" plan can name a door they will not actually pass | A staged level with several ways round, and strangers in it |
| "Look round the room you walked into" is one test deep | Right for rooms of the present size, and it is what a person does; but "looked round" means four corners in sight | A warehouse-sized room, where four corners are not "looked round" |
| Rooms are rectangles | L and T shapes are two rectangles joined by an archway, and a floor is authored in C# (`PrototypeBuilding`) plus a scene bake | A large floor: the authoring, not the simulation, becomes the slow part |
| People are about 25 scene objects each | Fine at 200 people; unmeasured at 500. The fire got batching; the people have not | The frame rate on a 500-person level |
| The standalone stress numbers are stale | The profiler has the panic case, but the built player has not been run since; every number here is an editor number | The next stone that raises the crowd, which the quality checks already require to be profiled |
| `TableWreck` is a kind nothing produces | Kept so an authored one still loads | -- |
| A test helper called `Run(...)` hides the type `Run` | C# finds the enclosing class's method before the type; the compiler says "'Run' is a method, which is not valid in the given context" | Any new test with a helper called `Run`; call it `Advance` |

