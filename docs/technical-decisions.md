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

## Prototype 3: gameplay, second batch (2026-09-26)

From a brainstorm about the game loop and two rounds of questions. **The
owner's decisions:** the Director climbs a ladder -- bin, socket, fuse box --
and the tower waits for a fire that has got out of its room; the Director
lights the bin after a while of ordinary day and the trigger button skips it;
a socket crackles before it pops; it pops in the busiest calm room; an
all-clear silences the bells; calming down is the personality system, not a
script; the bin fire is slow; people sense danger, never "floor on fire"; only
people pull alarms; influence is 0-20 steps, one a click, fading slowly, never
cancelled, unlimited, fading with distance, never into another room for now,
felt by calm people too, drawn as a sparkling aura and sparkling lines; the
nudge pushes away from the click and the annoyed shake and ignore it; a click
on a door is influence, holding keeps it shut, a right click turns the key;
the purse is suspended but kept; everything in one commit. Every default below
was chosen on the owner's behalf and is theirs to overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Where the ladder is switched on** | `DirectorSettings.ClimbsTheLadder`, off in the code defaults, on for the office through `LevelDefinition.directorClimbsTheLadder` | The purse's precedent: the sixty tests that use the plain scenario keep their fire starting where and when it always did, and only the level the owner plays builds up. A fingerprint case (`TheLadder_...`) covers the ladder itself | A second level wants a different ladder: then a list of incidents on the level |
| **The bin** | Three waste bins in the meeting room (3205 by the door, 3206 in the north-west corner, 3207 under the north wall); one drawn per run at start-up, only when there is more than one | Keeps the first batch's rule that the fire starts somewhere different in the meeting room each seed, with the fire starting in a thing | Too many bins in one room |
| **When it catches** | A seeded 1500-4500 ticks (30-90 s), drawn at start-up; the trigger button brings it forward | **The owner's choice.** Long enough to read the office and place influence, short enough that nobody waits | Playtesters wait too long |
| **How it smoulders** | A waste bin burns 1000-1500 ticks (was 250-450) and sets its floor square alight after resting about 500 ticks, jittered when it catches (`ObjectKindSettings.FloorIgniteRestTicks`) | It used to burn out before the floor caught. Ten seconds is long enough for somebody brave nearby to reach it with the new bottle | Nobody ever puts it out, or everybody always does |
| **A young fire** | While a Director-started fire has fewer than 12 burning squares, each square waits three times as long to spread (`YoungFireSquares`, `YoungFireSpreadPercent` 300), with the same single draw | **The owner:** "slow, and spread slow, so people have a chance to fight it if the right personality is there". Twelve is about a meeting table | As above |
| **The meeting room's extinguisher** | 3303 at (-3300, 9250), on the south wall beside the door | Without one in the room, the nearest bottle was the office's, and the bin was a real fire before anybody got back | As above |
| **"Put out"** | Nothing burning anywhere -- floor, things, people -- while it never got out of its start room | The owner's "too quickly so it can't spread": a fire still in its room is still an incident | Never |
| **"Got loose"** | A burning square or a burning thing in a room the incident does not own (a thing in a doorway, in no room, does not count). A bin's incident owns its own room; a socket's or the fuse box's also owns every room its own bang and spark set alight in the first 250 ticks after it went (`DirectorSettings.BangSettlesTicks`) | Simple and visible; a person on fire running out does not count by themselves, only what they set alight. The window was added after the code review of 2026-09-26: the fuse box lights every socket in the building by design, so without it the last rung always "got loose" and armed the tower at once. Five seconds is twice the spark's run down the cable | Playtest: the fuse box's fires should feel like one incident |
| **The wait between rungs** | 1000-2000 ticks (20-40 s), drawn at each put-out | Long enough for the office to settle, short enough not to go slack | Playtest |
| **The all-clear** | `AlarmSystem.Silence`, 500 ticks jittered after a put-out. It stands while nothing burns: a bell pulled again by somebody still frightened falls silent another 500 ticks jittered later, however often | **The owner's choice.** A pulled alarm otherwise kept everybody in earshot frightened for the rest of the round -- and, before the code review of 2026-09-26, one re-pull after the all-clear still did | Never |
| **Which socket** | Whole sockets outside the last incident's room; the room with the most calm participating people wins; a tie is drawn; no socket left means the fuse box | **The owner's choice** ("busiest calm room"). The bang lands on an audience | The owner wants the Director to aim at the way out, or at the player's influence |
| **The crackle** | 250 ticks (5 s): a `SocketCrackling` event, a crash heard 5 m that frightens nobody (the curious go and look), sparks drawn faster as it nears | **The owner's choice** ("crackle first"). The go-and-look cue already exists | Playtest |
| **The last rung** | The fuse box crackles and goes; the ladder ends whether or not that fire is put out | **The owner's choice:** bin, socket, fuse box | Never |
| **The tower** | Armed by `FireEscapedItsRoom` on the office; by the fire being lit where there is no ladder | **The owner's choice** | Never |
| **The round's stall clock** | Held open while a rung is on its way or a socket crackles (`DirectorSystem.HasSomethingComing`) | An office settled back to work is not a round that is over | Never |
| **The cable** | One way: each node's distance from the fuse box along the cable is worked out once; a spark only travels to something further away; a socket going off lights nothing; a spark passes through a socket already gone. Speed 45 -> 400 mm a tick; `FuseBoxExtraDelayTicks` removed | **The owner's rule.** Twenty metres a second takes the office's three sockets in about 2.5 s: "quickly cascade down" | Never |
| **A bang stays in its room** | `FireSystem.IgniteAround` lights only squares in the blast's room or a room open to it | A socket used to light the corridor through the office wall, so every socket fire "got loose" at once | Never |
| **Danger is danger** | Two more threats after the fire: `BurningThingsThreat` (seen, heard at the fire's base reach, nearest point its edge, in a room, near a route; harm stays with the flammables) and `BurningPeopleThreat` (seen and fled from; never in a room, on a route or "right beside me"; a person is never a danger to themselves) | **The owner's rule:** agents sense danger, not "floor in fire"; fire, zombies and whatever comes after must combine. A person alight is not "danger right beside me" so that the kind can still reach them with a bottle | A hunter arrives: it is a third threat of the same kind |
| **Extinguishers and things** | The extinguisher fights when only things burn, aiming at the nearer of a burning square or thing; "too close" counts things too | Otherwise nobody could ever put out a bin | Never |
| **Calming down** | `CalmingSettings`: a 250-tick quiet spell (jittered, drawn when they take fright), then drain `40 + 8 x bravery - 6 x nervousness` per mille a second (at least 10) to a floor of `120 x (nervousness - 5)`; below 400 they settle a reaction lag later, never two on one tick, only while running, dithering, frozen or standing. A danger in their room, in sight or in earshot (half through a shut door), a bell or a bang, or being alight refreshes the fright; each person looks every 10 ticks on their own beat | **The owner:** the personality system, not a script. That gives a hero about 7 s, an ordinary person 12, a worrier 27, a coward 60, and nervousness 9 and up never | Playtesters find people calm down during a big fire |
| **Rattled** | 3000 ticks if they saw the danger, 1000 if they only heard, each jittered per person; a noise within half its hearing reach alarms them; the brave need 3 more bravery to look first | **The owner:** "the ones that saw the fire stay rattled for a while" | Playtest |
| **Shouting stops** | A frightened person shouts only until their quiet spell is over | A crowd otherwise kept itself frightened for good by shouting about a fire that was out | Never |
| **Back to their desk** | The existing GoHome cue, unless their desk's room is alight | Already there | "Back to work" cues |
| **Only people pull alarms** | `AlarmSettings.PlayerMayPull`, on in the code defaults, off for the office through `LevelDefinition.playerPullsAlarms`; a click on the pull station puts influence beside it | **The owner's rule** | Never |
| **Influence: the numbers** | `InfluenceSettings`: 20 steps, one a click, a step lost every 100 ticks since the last click, felt to 12 m fading linearly, a click within 1 m stacks, 6000 mm for a full close pull (an exit sign's worth), susceptibility `100 + 12 x (nervousness - 5) - 12 x max(0, leadership - 5) - 12 x max(0, evil - 5)` +50 for a visitor, 10-200 %, a safety cap of 64 places that drops the faintest | **The owner's shape** (0-20, slow fade, unlimited, reach fading with distance); the numbers are defaults. One click at the edge of the room is below the choice noise, twenty close by beat an open door | Painting the whole floor, or influence too weak to notice |
| **Influence and the frightened** | Added to every door and spot choice except through the heat, into a room alight, or straight back into the room just left (`AgentDoorMemory.PreviousRoom`); the ways out through every influenced door on this room's wall are scored too, with the noise fixed at half its range; a choice it changed writes `AgentDrawnByInfluence` | With one way out, an influenced side door otherwise never got a look in. Fixed noise so an influence nobody feels draws no random numbers | Never |
| **Influence and the calm** | A calm person choosing what to do next wanders to the strongest pull with a chance of the pull per mille; the easily led (nervousness 7+ or a visitor) settled in a seat, or walking or standing about on an errand nobody else is part of, weigh it once a second at a tenth of that and get up. Never halfway into or out of a chair, out of a stall, a door they wait at, a chat, or a meeting-up somebody is waiting on | **The owner's choice** ("personality decides") | Playtest |
| **Influence on a thing** | Pinned to where the thing stood when clicked; a thing is picked by screen distance (30 px), as a person is | Things have no colliders; a moving pull would drag people after whoever carries it | A thing that should carry influence with it |
| **What a click is** | People first, unless the click is within 1 m of a floor or thing already influenced in the same room (then it stacks), then a thing, then the floor. A click the other side of a wall starts a place of its own | Frantic clicking on a busy corridor must build a pull, not nudge whoever walks under the pointer | Playtest |
| **The look of influence** | A pale gold ring that breathes and flickers, 0.3-0.8 m, brighter with the level, throwing off sparks; a thin gold line from each pulled person's chest to the place, shimmering toward it, brighter with the pull; pooled line renderers | **The owner's look.** Gold: not fire's orange, not a held door's blue | Visual pass |
| **Door controls** | Left click: influence (sent on release inside 0.3 s). Hold: keep it shut. Right click, no card, no drag: the key. No double click. `ClickDoor` stays in the simulation for recorded runs | **The owner's choice** | Never |
| **The nudge** | `NudgePersonFrom`: 300 mm away from the point (was 200 backwards), the point taken where the pointer meets the chest, pulled 0.3 m toward the camera; annoyed for 1000 ticks jittered, when a nudge only writes itself down; a side-to-side shake | **The owner's rule** | Never |
| **The heap is seen** | Anybody choosing a door treats a doorway heaped with fallen boxes, on a wall of their room and within sight, as found shut; the strong (7+) get 400 ticks jittered (about eight seconds) at it first (`BlockadeSettings.StrongGiveUpOnAHeapTicks`), counted from when they come within 3 m of it (`StrongTryTheHeapWithinMillimetres`); another heap seen on the way does not restart it | Found by the wall-starer test: a strong visitor pushed at the heap for two minutes without ever reaching the spot where they could work it. The first batch's rule -- the strong heave, everybody else goes round -- used to depend on reaching it. Eight seconds, not twenty: under the ten seconds of standing still that test allows | Never |
| **The press watch's limit** | A test's allowance for two things pressed into each other goes from 12 ticks to 15 | Seed 43: a box dropped by somebody knocked out while carrying it to wedge a door lay on them at 104 mm for thirteen ticks. Still under a third of a second, and a thing coming off a body rather than passing into it -- the same kind of case the earlier raises were for | Something genuinely passing through something else |
| **The physics engine and threads** | Nothing changed yet; recorded as open | Found 2026-09-26 re-recording the fingerprints after the code review: seed 41 with the way out opened and nobody a visitor gave one of two answers, about one run in five. Every body was identical to the last bit until one step where a person walked into two boxes of the fallen heap at once; with the engine's work spread over several processor threads it came out 0.08 mm apart, with one thread 57 runs in 57 agreed. That is the engine (PhysX) itself, not Paniq's code, and very likely what the seed 46 flip and the kicked-boxes case below were too | A decision for the owner: replays that always agree (one thread for the physics) against speed with big crowds (several) |
| **Held and let go of on the grid** | When a body is pinned or let go of (`PhysicsWorld.ApplyKinematic`), its pose is first snapped to a hundredth of a millimetre and a millionth of a turn (`SnapToTheGrid`) | Found by re-running the fingerprints: seed 46 with the way out opened gave one of two answers from run to run. The first difference was a box thrown off the fallen tower's heap for the second time landing a millimetre apart: crumbs of floating point below a millimetre, left in the engine when the box was pinned into the heap and let go again, invisible to everything the simulation reads until the throw magnified them. Snapped, five back-to-back runs agree tick for tick and the fingerprints hold run after run | A replay that drifts again: then look for another place the engine's own state leaks between steps |
| **The purse's name** | `InfluenceSystem` -> `PurseSystem`, `InfluenceSettings` -> `PurseSettings`, `influenceEnabled` -> `purseEnabled`; `PokeSystem` -> `NudgeSystem` and the poke names -> nudge (enum numbers unchanged). Proven by a full run with every fingerprint holding before anything else changed | **The owner's words:** influence and nudge mean the new things | Never |
| **Tests that are not about calming** | `GroupsEditModeTests` and `WayfindingRunEditModeTests` switch calming off, with a comment: each frightens people with one square of fire far away, and they would settle half way | The plan's rule: a test about something else keeps its subject | Never |
| Versions | `SimulationCompatibilityVersion` 68 -> 69; `ContentRevision` 80 -> 81. The thirteen fingerprints re-recorded and two new ones recorded for the ladder | Nearly everything above moves a run | Never |
| Tests | New: `DirectorLadderEditModeTests`, `CalmingDownEditModeTests`, `DangerIsDangerEditModeTests`, `InfluenceEditModeTests`. Changed: `PowerSystemEditModeTests` (a socket lights no cable; sparks pass a wrecked socket; all sockets within 3 s), `DoorClicksEditModeTests` (rewritten), `NudgeEditModeTests` (away from the point; the annoyed ignore it), `ReplayFingerprint` (the new commands) | -- | -- |

## Prototype 3: playtest fixes (2026-09-27)

The owner's twelve notes from seed 42, and their answers to nine questions:
the ladder is bin, boxes, outlet, fuse box; the boxes fall for the first
frightened runner in the corridor; a real physics fall with no guaranteed
block; "too quickly" is the carpet never caught; the socket comes twenty to
forty seconds after the boxes fall; every influenced thing has its natural
use, a door is opened or shut and the cruel wedge it; three quick pokes wake
anybody frozen; the spin on seed 42 is parked; one commit for the batch.
Every default below was chosen on the owner's behalf and is theirs to
overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The tower's trigger** | `TrapSystem.Watch`: the lowest-index frightened person on their feet, in the trap door's own room (the corridor), moving at `TrapSettings.TriggerSpeedMillimetresPerTick` 40 (2 m/s, the bolt pace) or more, whichever way. The trap is armed by `fire.Active`: the bin, with the ladder. `TriggerRadiusMillimetres` and the trap definition's own radius are gone | **The owner's rule**: "once people start running down the corridor". Any direction, because running is the rule, not heading for the T | A runner heading away from the T should not count |
| **Another bin** | `FireSystem.CellsEverLit` unchanged since the bin was lit and a bin unused: `LadderPhase.Relighting`, the next bin drawn among the unused, lit at `ReactionTick()`; its cause is the put-out; `DirectorStartedIncident.Strength` counts the bins. A bin that burnt out by itself with the carpet never caught counts the same | **The owner's rule**; "at once" is a beat later because nothing happens on the tick it was caused | The owner wants a limit other than the room's bins |
| **The socket's wait** | `NextRungDue`: due at `max(putOut + ReactionLag, boxesFell + draw(1000..2000))` when the tower has fallen (`TrapSystem.LatestFallTick`), `putOut + draw` otherwise; re-anchored with one more draw if the tower falls while the socket is on its way. The fuse box ignores boxes | **The owner's order**: bin, boxes, outlet; never before the put-out, so a fire still burning is not answered with a second | Playtest |
| **The bottle** | 3303 deleted from `PrototypeBuilding.DefaultPhysicsObjects` | **The owner's rule** | Never |
| **The fall** | `PhysicsObjectSystem.Topple`: unpin (which snaps to the grid), velocity toward the slot with `ToppleLiftPercent` 40 of it upward, spin as a thrown thing (`ToppleTumbles`), `Thrown` so it hits whoever is in the way, cause the fall. Each box's speed is the ballistic one for its own height and distance (`SpeedToReach`, whole numbers), scaled by `ToppleSpeedPercent` 100. No pins after the fall; `Pin(mayBeTaken)` is gone | **The owner's rule**: normal physics objects. Measured over ten seeds: lift 40 and 100 % land three or four of eight in the heap strip every run; 30 lands three in eight runs of ten, 50 scatters them, and spin on is better than off | A tower elsewhere, or a different box |
| **The heap** | A fact about resting boxes: `PileHoldsAtBoxes` 3 lying still in the archway's strip (`HeapGapMillimetres` 300 past the line either side, twice the door's 150), with no person in the gap, for `HeapSettleTicks` 25 in a row shuts it (`BoxHeapSettled`, `PileInto`); fewer for 25 in a row opens it (`BoxPileCleared`, cause the settling). The people-only check is `DoorSystem.NobodyInTheDoorway` (`PhysicsWorld.IsAnyBodyInDoorway` learnt to skip loose things: a box lying against the plug used to count) | Half a second either way, so a rocking or kicked box does not flicker the plug; 300 mm so a box that stops a hand short still blocks, and no wider, because a throw clear moves a box about a third of a metre | Playtesters clear it too easily or never |
| **Whoever is in the way** | Hit by the boxes as by any thrown thing (`ThingMetPerson`); the scripted shift-and-knock-down is gone | Normal physics objects | Never |
| **Small things** | `BlockadeSettings.BlockMinimumRadiusMillimetres` 160: `ResolveBlockages` skips anything smaller (laptop 150, bin 150, bottle 125; bag 175, chair 250, box 300 still jam) | Doors jammed "for no reason" on the small things a crowd shoved about | A small thing should jam |
| **Put-downs** | `IsClearForItem(allowDoorways: false)` refuses any doorway's strip; `TrySetDownAt` (wedging) still allows it | A tidier held up at a door used to set the box down in the gap | Never |
| **Stand spots** | `CalmBehaviour.StandSpotInFrontOf`: 1.0, 1.4 or 1.8 m back and 300 mm to either side or none, by the person's index, no draw | One spot a metre from the door used to gather everybody on it | Playtest |
| **Unlodging** | Calm: `ErrandPhase.ClearingTheDoor`, once per errand (`AgentErrand.ClearedADoor`): walk to it (`DaySettings.ClearTheDoorWalkTicks` 500), pick up (`Items.PickUpTicks`), set down on a clear spot (`Items.SetDownTicks`), `AgentClearedDoorway`; or, strength 7+ and too heavy, `DoorSystem.HeaveObstructionClear` (moved out of `DoorBehaviour`). Frightened: `UpdateAttempt(agent, inDanger, flamesNear)` and `TryClearTheWay(agent, flamesNear)` give up when the flames are inside their danger distance (the raw danger, not the dash-adjusted one) or they are alight | **The owner's rule**, with "not too panicked" read as the flames inside their danger distance | A weaker person should fetch help |
| **Influence on a door** | `CueKind.FollowTheInfluence` (`GoTo(TheInfluence)`, `UseTheDoor`), a person's own idea; the errand's `Object` is the door. Open → `TryClose`; shut → the usual `TryTheDoor`; opened for its own sake they do not go through. `InfluenceSystem.Spend` removes the place and writes `InfluenceSpent`. Locked, held or jammed: the wait, the clearing, the give-up; the pull stands | **The owner's rule**: open if shut, shut if open, spent on use | Never |
| **The cruel** | Evil ≥ `BarricadeEvilMinimum` 7 at a shut, clear influenced door: `ErrandPhase.WedgingTheDoor` with the barricade's own item choice, spots and set-down time, `AgentBarricadedDoor`, the pull spent | **The owner's choice** when asked | Never |
| **Influence on a thing** | `CalmBehaviour.TryUse`: a free chair → `ChairBehaviour.TryStartSittingOn` (anybody, owner or not); anything they could lift, a bottle included → `ItemBehaviour.FetchForTheInfluence`, a bottle kept (`AgentCarry.KeepIt` → `OwnsIt`, `AgentTookExtinguisher`) and let go of when frightened like a bag; whoever uses a thing spends its pull (after the pick-up, on settling into the chair, on the alarm pulled beside a spot: `SpendNear`). No use: stroll to it as before | **The owner's choice**: each thing its natural use | A taken bottle should be used, not held |
| **Annoyed** | `NudgeSystem.Nudge`: the lurch and stagger land before the annoyed check; annoyed, nothing is counted and nobody looks round | **The owner's rule** | Never |
| **Poked awake** | At the reaction tick, three in a row and frozen: `AgentPokedAwake` then `FearSystem.Unfreeze`, both temperaments, no annoyance | **The owner's choice**: anybody frozen | Never |
| **Off the chair** | The third quick poke at somebody seated: `AgentKnockedOffChair` and `BlowOver` by `NudgeSettings.KnockOffChairMillimetres` 500, at once; the chair behaviour takes them off it that tick | **The owner's rule**; at once, like the jab | Never |
| **The shake** | Presentation: while annoyed and under 2 s since the `AgentAnnoyed` event the icons already time (`AgentIconViews.AnnoyedAge`), at 56 rad/s (about nine a second). No snapshot field | **The owner's rule**: faster, and over in a few seconds | Never |
| Versions | `SimulationCompatibilityVersion` 69 → 70; `ContentRevision` 81 → 82. All fifteen fingerprints re-recorded | Nearly everything above moves a run | Never |
| Tests | Changed: `DirectorLadderEditModeTests` (the tower's trigger, the relit bins, the socket after the fall), `BoxTowerEditModeTests` (rewritten for the physics fall and a laid heap), `BarricadeEditModeTests` (unlodging, small things, the danger gate), `InfluenceEditModeTests` (using doors and things), `NudgeEditModeTests` (still shoved, poked awake, off the chair), `DoorsEditModeTests` (the new door event) | -- | -- |

## Prototype 3: playtest fixes, second round (2026-09-27)

The owner's four notes after the first round, and their answers: boxes
heavier "to suit the gameplay, not realism"; the stockroom "a winding lane
the Director can topple"; the socket pops where the crowd is; a sign when
another bin is lit; the socket five seconds after the boxes topple; amend
the first round's commit. Every default below was chosen on the owner's
behalf and is theirs to overturn.

**The one place the work departs from the owner's answer.** The owner asked
for heavier boxes only. That was tried first and measured: a box on the floor
against people who are driven by velocity every tick (`PeopleBodies.Stride`)
does not stop them whatever it weighs, because the engine's answer to two
bodies overlapping is to push them apart, and a person pushed back is driven
forward again next tick. One calm walker against a resting box, five seconds:

| 600 mm box, 5 s | 13 kg | 25 kg | 40 kg | 60 kg | 80 kg | 110 kg | 150 kg |
| --- | --- | --- | --- | --- | --- | --- | --- |
| One walker slid it | 1.6 m | 1.5 m | 1.4 m | -- | -- | 1.5 m | 0.2 m |
| One runner slid it | 6.5 m | 10.0 m | 9.7 m | 8.8 m | 6.8 m | 5.5 m | 1.1 m |
| Three runners slid it | 3.8 m | 3.7 m | 3.7 m | 6.2 m | 6.2 m | 6.3 m | 6.1 m |

(An 800 mm box slid 6.6 m for every mass under 150 kg, and 0.9 m at 150 kg
for one runner, 6.6 m for three.) Only the 150 kg box, near the 200 kg cap,
stopped a single runner, and three runners still shoved it six metres. So the
boxes are heavier (the owner's rule) *and* a heavy box that has lain still is
held where it lies against people, which is what makes it an obstacle. The
owner should know this is why a fallen box does not slide when walked into.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Box masses** | `PrototypeBuilding.BoxMass(size)`: 300 mm and under 3 kg, 400 mm 6 kg, 500 mm 10 kg, 600 mm 40 kg, 700 mm 55 kg, larger 70 kg. Every box (office, stockroom, tower, stack) uses it; the 200 kg validation cap stands | **The owner's rule**: heavier, to suit the gameplay. Under 32 kg a box is still clutter people kick and carry; from 40 kg up it is on the map | A box should be carried by two |
| **On the map** | `WorldSettings.OnTheMapFromGrams` 32000 ("more than the strongest can carry"): a loose thing on the floor (not held, dormant, resting on something, wrecked) that weighs this or more, or is pinned, is an obstacle on the navigation grid like a table (`WorldGeometry.Obstacles()` = tables and heavy things; `ObstacleAt`, `RouteCrossesObstacle`), re-marked when it has moved 150 mm from where it was last marked (`TableMovedMillimetres`), lifted the tick it is picked up, wrecked or burnt out. `NavigationGrid.MarkDoorways` no longer floors a square under an obstacle beside a doorway (the standing tower half a metre from the archway used to read as floor) | People steer by the map; a maze of loose boxes would have them walking into it | The map is too slow with many heavy things (profile first) |
| **Held still** | `PhysicsBody.HeldStill`: a thing on the map that has lain still for `WorldSettings.OnTheMapAfterTicks` 25 in a row is pinned (kinematic) where it lies and marked on the map; a thing that has lain still since the start is held the first tick it is looked at (`PhysicsObjectSystem.FollowTheHeavyThings`, in `AfterStep`). `IsOffLimits` is pinned-and-not-held, so a held thing may still be taken, heaved or flung; `Unpin` and `PlaceAt` start the count again | The measurement above: nothing else makes a crate an obstacle to a person in this engine | A held crate should rock when walked into |
| **The heave on open floor** | `DoorBehaviour.TryClearTheWay`: a thing within reach that cannot be thrown clear but `CanHeaveAside` (on the map, loose, nobody's, not fixed, strength ≥ `ShoveMinimumStrength` 7) is heaved (`HeaveAside`, the doorway pile's speed `ShoveSpeedBase + ShoveSpeedPerStrength × strength`) square across their way, to the side with more floor before the room's wall (`HeaveHeading`, `FloorBefore`), the crate's own lean breaking a tie | So a crate against one wall is heaved towards the other, and a row shoved up to one wall is shoved back the other way next time, which opens a gap | Never |
| **A struck crate** | `HitObject`: a held crate struck by a shoved thing is unpinned and takes the shove on at the hitter's velocity scaled by the masses, capped at the hitter's | A row of crates slides as a row instead of the first stopping dead against the second, which is kinematic | Never |
| **Cut off** | `DoorBehaviour.ChooseExitDoor` and `ChooseRefugeDoor` skip a door whose approach point the map cannot reach from here (`Routes.CanGetFromHereToThere`; yes when the map has no opinion this tick) unless the person is strong enough to heave (`IsCutOff`). The strong go for it and heave; everybody else picks another door, or a spot | On seed 5 the fallen tower, held still across a door, had somebody push at a crate for the rest of the round | A weaker person should call for help |
| **Blasts** | `FlingFrom` flings a held crate too (unpinned first); only off-limits pins (the standing tower, the crate walls) stay put | A 45 kg set of shelves should still go over in a blast; a bang that scatters the heap is the Director's | Never |
| **The stockroom lane** | Room 5014 unchanged. Wall A (ids 3501–3512) at x 9500 from the south wall north, four columns of 700 mm crates three high, pinned; wall B (3513–3527) at x 12000 from the north wall south, five columns; the east column 3530–3532 stays; six light loose boxes (3541–3546) in the lanes; south-wall dressing pairs outside the middle lane. The sounder moves to (13200, −650). Exit signs (8500, −2200) E, (10700, −2200) S, (10700, −5000) E, (14500, −3000) N added; `PhysicsObjectDefinition.StartsPinned` is serialized and refused for a dormant, resting or carried thing | **The owner's choice**: a winding lane the Director can topple. Three high because a person cannot see or climb over; pinned because a crate wall is a wall | The lane should be lit or dark |
| **The stack** | Trap 7002: crates 3581–3584 (700 mm, 55 kg, four high, not authored pinned: the trap holds them) at (9500, −900) against the north wall at the first bend; a **lane trap** (`TrapDefinition(trapId, boxIds, triggerRoomId, landingCentre, heading, width)`, `IsDoorTrap` false) watched in the stockroom, sprung by the first frightened runner there as the tower is, toppled along the landing line (centre (9500, −1825), heading 180, width 2600, `SpeedToReach` per slot). `BoxTowerFell` without a target reads "the crates came down across the lane". No plug: the crates block because they are held still and cut the route because they are on the map. Validation: the landing inside the trigger room, the width at least the widest crate | **The owner's choice** | The stack should fall for a runner in the office too |
| **Four crates** | Four, because three across a 2.6 m gap leave a gap a person squeezes through. Four do not fit flat, so they scatter, and the lane is cut by whatever they leave; the test lays three in a row and the fourth beside the row's end | Honest physics, as with the tower | Playtesters see the lane never cut |
| **The frightened walk** | `FrightenedWalk` (`TryStep(agent, target, speed, cause, out intent)` / `Forget`), built in `Run` before `ExtinguisherBehaviour` and `AlarmBehaviour`, which walk through it: same room, the flow field; another room, `TryFindRoute` to the next door; open, through it; shut and unlocked, the approach point, face it, `Exits.DoorOpenTicks` jittered, `doors.Open`; locked, held or jammed, `AgentTriedDoor` once and the way avoided. `ExtinguisherBehaviour.GiveUp` forgets the walk (`BlockedTicks` too); `NearestFreeExtinguisher` skips a bottle in a room no route reaches | **The owner's note**: fighters walked into shut doors for ever | The calm errand's door walk and this one should be one |
| **The socket's wait** | `DirectorSettings.SocketAfterFallTicks` 250 jittered from `TrapSystem.LatestFallTick` (any trap): once the tower has fallen and the socket has not crackled, it crackles then whatever the bin is doing (`socketFallDue`); `AfterPutOutMinimumTicks`/`MaximumTicks` 250/500 after a put-out with no fall, and for the fuse box after the socket's fire is put out. `NextRungMinimumTicks`/`MaximumTicks` are gone. A put-out with the socket already come goes to the fuse box | **The owner's rule**: 5 s after the topple; the next stage sooner after a put-out | The fuse box should also hurry after the stack falls |
| **Where it pops** | `BusiestRoomsSocket`: every participating person whatever their fear, rooms of the incident so far skipped, a tie drawn; `Pop` keeps the old rooms while something still burns | **The owner's choice**: where the crowd is | Never |
| **Another one!** | `EventSigns.TryCaption`: `DirectorStartedIncident` with `Strength > 1` captions "another one!"; `EventStory` says "another bin: … caught fire" | **The owner's choice** | Never |
| **Signs along a lane** | `WayfindingSystem.WorkOutWhatSignsTeach` measures a sign's agreement against the first step of the *walk* to the door (`WorldGeometry.TryWalkStepToward`: the cheapest neighbouring square in the door's walk table), not the straight line to the door's centre; the straight line only when the squares cannot say. A sign pointing away from every way out still teaches nothing | The middle lane's sign points south, down the lane, while its door lies north-east as the crow flies | Never |
| Versions | `SimulationCompatibilityVersion` 70 → 71; `ContentRevision` 82 → 83. All fifteen fingerprints re-recorded | Nearly everything above moves a run | Never |
| Tests | New: `HeavyThingsEditModeTests` (on the map, held still, the heave, the cut-off door), `StockroomTrapEditModeTests` (the stack falls, cuts the lane, a landing outside its room refused), `FrightenedWalksEditModeTests` (doors opened for a bottle and for the flames, a locked one given up). Changed: `BoxTowerEditModeTests` (nobody carries a fallen crate, the strong heave), `DirectorLadderEditModeTests` (5 s after the fall, 5–10 s after a put-out, the busiest room), `NavigationGridEditModeTests` (heavy things in the clearance oracle), `StockroomEditModeTests` (the crossing test runs with no traps: the stack would fall for its runner), `NewPropsEditModeTests` (61 boxes), `DoorsEditModeTests` (a fall with no doorway), `TheBuilding` (the stack, the bend) | -- | -- |

## Prototype 3: the keycard (2026-09-27)

The owner's answers, after a step back to look at the game loop: left alone
about a quarter should live; the way out needs a keycard, in a member of
staff's pocket or on a desk, "random B or C"; once swiped the door stays
unlocked for good; the card is fireproof; the end card says what would have
happened left alone, in this same batch; one commit. Every default below was
chosen on the owner's behalf and is theirs to overturn.

**The measurement that started it.** Ten rounds of the office with nobody
at the controls, seeds 40 to 49, before this batch:

| Left alone, before | |
| --- | --- |
| Cleared the 75% bar | 7 of 10 |
| Saved on average | 16.2 of 20 |
| Worst and best | 8 and 20 |
| Somebody put the bin out | 1 of 10 |
| Somebody pulled the alarm | 9 of 10 |
| The tower fell | 10 of 10, five to seven seconds after the bin caught |

After this batch, same seeds: **3 of 10 cleared, 5.9 of 20 saved on
average**; the three seeds where a member of staff started with the card
saved 16, 18 and 20, one desk seed saved 5 (the hero fetched it), the other
six saved nobody. That is after a tuning pass on the owner's word ("retune"),
below; at the first values it was 8.6 of 20 and 5 of 10 (6.6 and 3 of 10
before the code review's fixes, when a fetcher who tripped handed the card on
to somebody across the building).

**The tuning pass.** Measured over the same ten seeds, nobody at the
controls:

| Bravery to go for it | Grab in passing | Saved on average | Cleared |
| --- | --- | --- | --- |
| 4 (first value) | 6 m (first value) | 8.6 | 5 |
| 6 | 6 m | 7.7 | 4 |
| 8 | 6 m | 6.7 | 3 |
| 6 | 4 m | 7.2 | 3 |
| 6 | 2 m | 6.3 | 3 |
| 7 | 3 m | 6.6 | 3 |
| **8** | **3 m** | **5.9** | **3** |
| 4 or 6 | none | 5.4 | 3 |

About 5.4 is the floor for these knobs: the seeds where somebody starts with
the card in their pocket open the door whatever they are set to. No grabbing
in passing at all reaches it, but then a card on a desk is never fetched and
every desk seed is a total loss; bravery 8 and three metres keeps a chance
there (the hero). How often the card starts on a desk was not touched: it is
the owner's half-and-half. Three things were measured on the way and are now
rules, each recorded in its row: a card in a pocket was lost to every trip in
the crush, so only being out cold or dead drops it; a card on a desk was
never fetched, because by the time anybody had found the door shut the
fallen boxes had cut the office off from the crossbar, so staff grab it in
passing; the host fought the bin for forty seconds with the card in his
pocket and died in the corridor, so a holder makes straight for the door and
swipes it from a couple of metres.

**The code review of the batch (same day), and what changed.** Eight
findings, all fixed in the same commit: a fetch for the player that came to
nothing left a flag behind that stopped that person ever tidying again; the
hands-off round never got the player's Trigger event press, so on a level
with no Director it never burnt and reported everybody alive; a fetcher who
died mid-fetch held the card's claim for ever; a card door with no keycard
in the building (or two keycards) was accepted; the hands-off round ran
twenty unbudgeted ticks a frame and was built on the first frame of play;
fetchers were steered by where the card really was rather than where they
believed it was; the meeting-room test excused too much; the test helper
that hands somebody the card did not tell them so. Two things were measured
while fixing them and became rules: releasing the claim on any fall sent a
second person 25 metres for a card the first, who had tripped beside the
desk, was standing next to; and a fetcher arriving where they believed the
card lay gave up when it had only been nudged 80 mm.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The kind** | `PhysicsObjectKind.Keycard`, appended; 150 mm, 50 g, friction 120, `IgniteTicks` 0 (never burns), `Pocketable`; authored as 3950 on desk 4001 beside its laptop, resting. Never on the map, never jams a door (under the 160 mm radius), never tidied, wedged, thrown clear or hurled (`CanLift` refuses a pocketable thing) | **The owner's rule**: fireproof. A pocketable thing is a new flag on the kind table, as equipment was | A second pocketable thing |
| **A pocket, not the arms** | `Agent.Keycard` (`AgentKeycard`: `Held`, what they believe, `MayFetchFromTick`, `PocketingUntilTick`) beside `Agent.Carry`, never in it; the world object is held (`PickUp`, non-solid, `FollowPocket` 150 mm behind the carrier); `Release(..., onTheFloor: true)` sets it down where they lie | A holder keeps free hands for a bottle or a box, is not slowed, and trips none of the `Carry.ItemIndex` guards that would have made them ignore influence, alarms and help | Never |
| **Where it starts** | `KeycardSystem.PlaceAtTheStart`, in the `Run` constructor after everything is built, drawing only from `Pcg32(seed, 56)`: `OnADeskPercent` 50 → one of the tables in the card's authored room, uniform, near the east end (`PlaceOnATable`); else a uniform member of staff (`Knowledge.KnowsEverything`, the host included). `KeycardStarted` at tick 0 (source the holder, or the card itself for a desk). `Keycard.Enabled` false puts the card away (dormant) and no door needs it | **The owner's choice**: random B or C. Its own stream, on the deck's terms (see the simulation contract), so the start-up draws and every level without a card replay as before | A card authored on a person |
| **Who knows** | Staff believe where it started; visitors nothing. `Notice(agent)` after perception: a sighting (vision range, the 45° cone, `CanSeeBetween`) that disagrees with what they believe, and with any sighting still pending, starts a pending belief committed at `ReactionTick()`; a holder in sight means "with that person" | The owner's rule that nobody reacts on the tick a thing happens; one draw per change seen, not per tick | Belief that goes stale (a card kicked out of sight) |
| **Going for it** | An `IPanicOption`, **first** (before the leaders: a follower was never asked otherwise). Conditions: upright, not in danger, not helping, bravery ≥ `FetchBraveryMinimum` 8 (4 before the tuning pass), believes it lies free, nobody holds it, unclaimed (one fetcher at a time; a claim lapses when its fetcher is out cold, dead or doing something else, not when they trip), within `FetchRangeMillimetres` 30000 as the crow flies, a route to its room, and where they *believe* it lies not within `FlamesKeepAwayMillimetres` 1500 of any threat; and either found a card door shut (`DoorBehaviour.GiveUp` sets `MayFetchFromTick = ReactionTick()`, once) or staff with the card within `GrabOnTheWayRangeMillimetres` 3000 (6000 before the tuning pass). Walks with `FrightenedWalk` to floor beside it (`NearestStandableTo`), pockets it within `PickUpDistanceMillimetres` 700 after `PocketTicks` 25 jittered; gives up on danger, fire, `FetchTimeoutTicks` 1500, `BlockedGiveUpTicks` 50, the claim gone to somebody else, or seeing it taken or in the fire. Arrived where they believed it lay, they look about them 1.5 m (`LooksAroundMillimetres`) and go to it; further, or in a pocket, they were wrong and no longer believe they know. The truth only counts once they are over it (then flames at the card itself still stop them) | Measured: with 12 m range nobody ever went (the desks are 24 m from the door); with 400 mm nobody could reach a card on a desk from the floor; with "found the door shut" alone a desk card was never fetched | A weaker person asking a stronger to go |
| **The holder** | `PanicBehaviour` skips every other option for somebody with the card: no fire-fighting, following, helping, alarm or wedging. `SwipeIfInReach`: a frightened holder within `SwipeReachMillimetres` 2000 of a card door, in its room, swipes it; `StartAttempt` at the handle swipes too, and a calm errand (`TryTheDoor`, `WaitAtTheDoor`) | Measured: the host fought the bin with the card and died; holders were knocked down in the crush a metre from the handle | A holder should hold the door for the rest |
| **The swipe** | `DoorSystem.SwipeKeycard`: `NeedsKeycard` off, `Unlocked`, `DoorUnlockedWithKeycard` (source the holder, target the door, cause their fright or their try); whoever is rattling it opens it next tick, and `AnnounceWaysOut` wakes everybody as the player's unlock did | **The owner's choice**: unlocked for good | Never |
| **Losing it** | `DropIfOutCold` (Unconscious) and `DropFromLost` (death): `KeycardDropped`, set on the floor beside them. A trip, a knock-down, a fright, being alight and upright: kept | **The owner's words**: "knocked out", "dies". Measured otherwise, every holder lost it to a stumble | A cruel person taking it off somebody |
| **The card door** | `DoorDefinition.needsKeycard` (appended; valid only on a locked door to the street, and only with exactly one keycard in the building while `Keycard.Enabled`: `ValidateTheKeycard` refuses none, and refuses two anywhere); `DoorRuntime.NeedsKeycard` while `Keycard.Enabled`; `Batter` does nothing, the force roll is skipped, the leader does not send anyone, `ScorchInTheFire` skips it, `ClickDoor` and `ToggleLock` refuse it (no charge); in `DoorSignature` and `DoorSnapshot`. TNT in the outer wall still works; an evil escapee may still slam and lock it afterwards, and then the key or a shoulder works as before | **The owner's rule**: nobody batters it | A card door inside the building |
| **Influence on the card** | `ItemBehaviour.FetchForTheInfluence` with `Carry.Pocket`: fetched like a bottle, pocketed on pick-up (`AgentTookKeycard`, cause `InfluenceSpent`) | The owner's rule that what is pointed at is used | Never |
| **Uproar** | `DoorUnlockedWithKeycard` middling; `KeycardStarted`, `AgentTookKeycard`, `KeycardDropped` nothing | As a door forced | Never |
| **Left alone** | `LeftAloneRunner` (gameplay): the same `ScenarioData` (`RunDriver.BuildScenarioData`, feel included) on the same seed, built in `RunDriver.Awake` with the scene (a whole second run would be a hitch on a frame of play). Until the real round's disaster has started it goes no further than the real round's tick, so the player's Trigger event press is copied onto the same tick (`MirrorTrigger`): when the disaster starts is the round both are compared on, not help. Then up to 20 ticks a `FixedUpdate`, paused or not, and never more than 3 ms of work, until `RoundPhase.Over` or 12,000 ticks, where everybody still alive counts as having lived; disposed with the runner; skipped while tuning live. `RoundScreens.DrawEndCard` prints the line, or "still working it out". The physics engine on several threads can replay a busy seed to one of two answers about one run in five, so the line may differ from a truly hands-off play by the odd person | **The owner's choice**, in this batch. Deterministic runs make it an answer, not a guess | Playtesters read it as a spoiler |
| **The measurement** | `HandsOffBaselineMeasurements` (Explicit, `Measure`): `TheOffice.asset` as the level plays it, seeds 40–49, prints saved/escaped/survived/lost, the end, the card's story, and why nobody went | The number the owner tunes by | Never |
| Versions | `SimulationCompatibilityVersion` 71 → 72; `ContentRevision` 83 → 84. All fifteen fingerprints re-recorded, and held over two runs | Everything moves: the way out is opened by a person or not at all | Never |
| Tests | New: `KeycardEditModeTests` (21: where it starts, a level without it, only a door to the street, the strong give it up, the key does not fit, the swipe, the fetch from a desk, the tidier leaves it, the pull pockets it, kept when frightened and alight and knocked off a chair, picked up off the floor, never burns, left alone equals a hands-off run; and from the code review: left alone copies the trigger and keeps in step until then, the budget, no card or two cards refused, a fetcher knocked out lets somebody else go, a fetcher who trips keeps the claim, a tidy-up after a failed fetch for the player, a visitor handed the card knows it). The fetch test now uses two brave staff at the way out rather than the whole cast, where a card knocked off its desk unseen made it one seed's luck. Changed: `TheBuilding.WithThePlayerAbleToAct` puts the card away (`WithAnOrdinaryWayOut`); `ReplayFingerprint.Of` likewise for the opened runs; `CuesEditModeTests`/`ErrandsEditModeTests` calm days, `EconomyEditModeTests` and three `SimulationEditModeTests` that read the log by position put it away; `PossessionsEditModeTests` counts things in arms only; `MeetingRoomEditModeTests` excuses only the tick somebody knocked down stands up, and holds it to the stand-up reach; the 27 places in fourteen test classes that build a building of their own with no keycard in it now say so (`Keycard.Enabled = false`), which the new validation requires | -- | -- |

## Prototype 3: the Director caps the round (2026-09-28)

The owner's rule: left alone, about 25% live on average and never more than
50% on any seed, dynamic and random-feeling. The owner's decisions, not
reopened here: a reactive Director over fixed rules about the card and over
curated seeds; it reads only the crowd and the card, never the player; the
building's tricks first, measured and shown, then the crowd's. Asked, the
owner chose the wide temper (two to eight of twenty) and a breather for a
massacre, and one commit for the batch. Every default below was chosen on
the owner's behalf and is theirs to overturn.

**The measurements, in the order they were taken.** Fifty seeds, 40 to 89,
the office as the level plays it, nobody at the controls; the ten-seed
rows are seeds 40 to 49.

| Left alone | Saved on average | Cleared | Over half |
| --- | --- | --- | --- |
| Yesterday's office (before this batch, ten seeds) | 5.9 of 20 | 3 of 10 | 3 of 10 (16, 18, 20) |
| The gliding boxes fixed, cap off (ten seeds) | 4.4 of 20 | 2 of 10 | 2 of 10 (seed 47: 20, seed 48: 15) |
| Cap on, first form: everybody with a route counted, the fuse box freely, 10-20 s between pushes | 1.6 of 20 | 1 of 50 | 2 of 50 (48: 15, 56: 11) |
| Cap on, tightened: only the frightened counted, a trap only with somebody in its room, the fuse box only after a socket, 30-60 s between pushes | 1.8 of 20 | 2 of 50 | 2 of 50 (57: 17, 80: 20) |
| Cap on, and only once the way out is open, fetch knobs 8 / 3 m, before the wall-starer's door rules below | 3.8 of 20 (19%) | 4 of 50 | 7 of 50 (47: 20, 48: 15, 52: 19, 59: 12, 69: 11, 77: 14, 89: 17) |
| The same, fetch knobs back at 4 / 6 m | 3.8 of 20 (19%) | 5 of 50 | 7 of 50 (44: 18, 47: 20, 48: 15, 52: 17, 59: 12, 69: 11, 85: 18) |
| **As committed**: the above at 8 / 3 m, with the door rules (people go round a held box instead of pressing at it) | **4.5 of 20 (23%)** | **8 of 50** | **10 of 50** (41: 15, 44: 18, 45: 16, 50: 12, 62: 16, 63: 15, 67: 13, 68: 15, 83: 20, 88: 17) |

Seed 83 in the last row never ended: the fire went out, nobody got out, and
twenty frightened people kept the round's stall clock going for the whole
four minutes, so the measurement now calls it as the end card's left-alone
line would, everybody alive having lived. A round that cannot end is noted
under *found on the way* in the roadmap.

What the table taught, each now a rule in the code:

- **The gliding boxes were letting people through the tower.** Fixed
  (below), the fallen tower holds, and the office fell from 5.9 to 4.4 with
  the Director doing nothing new. The holder of a pocket card now reaches
  the door in about one round in three; before, nearly always.
- **A Director that pushes while the holder is walking the card to the
  door kills the holder and everybody behind them.** Both early forms
  emptied their menu within a minute of the holder taking fright, and
  pocket seeds that used to save sixteen saved nobody. Hence: ahead only
  once a way out stands open.
- **Counting the calm made the reading a lie.** Twenty on course the moment
  the holder took fright, with fourteen sitting at their desks. Hence: the
  frightened, on their feet, with a route.
- **A Director with nothing to push must draw nothing.** A reaction lag
  drawn for a push that then found nothing turned seed 42 from six saved
  into none, by moving every later draw. Hence: the availability check
  first, without drawing.
- **The knobs make no difference to the average.** At 4 / 6 m a desk card
  is fetched in most desk seeds, and the fetcher dies with it in a fire that
  has grown by then. Yesterday's values are kept: no measured gain, no
  reason to move what the owner tuned.

**What the building half cannot do, and why it is still worth having.** The
seeds over half are rounds in which the holder swipes and the crowd
streams through the crossbar behind them within half a minute. The
Director's only tricks there are a socket in the office or cafeteria and
the fuse box, none of which reaches the crossbar; the traps have long
fallen. So the building half trims (seed 42: seven out, the cafeteria's
socket at the swipe; seed 80: four) and leaks (seed 47: twenty). It is a
reactive Director in the sense the owner asked for, and it is the seam the
crowd's tricks plug into: the holder freezing and a cruel person taking the
card act on the door itself, which is what a hard cap needs. The fifty-seed
check stays red until then, on purpose: "any seed above ten is a bug" is
the owner's rule, and a red measurement is more honest than a loosened one.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The switch** | `DirectorSettings.CapsTheRound`, off in the code defaults, on for the office through `LevelDefinition.directorCapsTheRound`; needs `ClimbsTheLadder` (nothing to push with otherwise) | The purse's and the ladder's precedent: sixty tests keep their day | A second level |
| **The allowance** | `AllowanceMinimumPercent` 10, `AllowanceMaximumPercent` 40 of the crowd, rounded: 2 to 8 of 20, drawn once from `Pcg32(seed, 57)` with the reading's beat | **The owner's choice**: wide. Averages 25%; the top leaves ten points under 50% for leakage. Its own stream, as the card's is, so the fingerprints with the cap off held to the bit | The owner wants a narrower or a fixed temper: two numbers |
| **The reading** | Every `ReadEveryTicks` 25, on a beat drawn with the allowance. On course: escaped, plus participating, `Scared`, on their feet, with `TryFindRoute` to any door that `DoorLeadsOutside` (or in its room), counted only while such a door is not `Locked` and not `NeedsKeycard`, or somebody frightened and on their feet with `Keycard.Held` can reach one. Draws nothing | Measured: counting the calm read twenty on course at the first fright | A bigger level where a route is not a forecast |
| **Ahead** | On course > allowance **and** a way out stands open | Measured: pushing before the door opened killed the holder (1.6 and 1.8 of 20) | The crowd half may push before the door opens: that is what its tricks are for |
| **A massacre** | Alive + out <= allowance: no socket after the fall, no rung after a put-out, no relit bin, `traps.Advance(armed: false)`; `HasSomethingComing` false for the things held off, so the round may end | **The owner's choice** and the game vision's "a massacre, and the pressure lets up" | The owner wants easing sooner |
| **The push list** | In order: a standing trap with the most on-course people **in its room** (`TrapSystem.Spring`: the same fall, `TrapTriggered` with no runner); else `BusiestRoomsSocket` over the on-course counts, at least one; else the fuse box, only once `socketCame`; else, with nothing burning, another bin. `DirectorPushed` (Strength: on course, Duration: the allowance, target: what it reached for) is the cause of each | Cuts first, fire second; the fuse box takes every socket and was the first thing reached for in the early forms, which is what killed everybody | A trick that should come earlier |
| **The rest** | `PushMinimumTicks` 1500, `PushMaximumTicks` 3000, drawn when a push lands (10-20 s in the first form) | One trick felt before the next | Playtest |
| **A push draws only when it can push** | `HasSomethingToPush()` without draws, before the reaction lag is drawn | Measured on seed 42 (six saved to none by a lag drawn for nothing) | Never |
| **The push and the ladder** | A pushed socket or fuse box goes through the ladder's own crackle and pop (`CrackleAt`, `phaseAfterPop`), so its room joins the incident and the ladder does not read its own fire as loose; after the fire has got loose the ladder returns to `Over` | One crackle, one pop, one story | Never |
| **The pull on the card, frightened** | `KeycardSystem.Decide`: the strongest pull this person feels is the card's place (`InfluenceSystem.PlaceOfThing`) at `KeycardSettings.PulledToTheCardPerMille` 250 or more; waives the bravery gate and "found the door shut", tells them where to go; every other guard stands; `AgentDrawnByInfluence` when they set off, the pull spent on pocketing and named as the cause (`AgentKeycard.PulledEventId`); `KeycardSystem` is `IBindable` for the influence | The player's one lever on a desk card did nothing once the panic started | The price of the pull |
| **The gliding boxes** | `PhysicsBody.Heaved`, set in `ShoveAside`, cleared once still (after the contacts are judged); `HitObject` un-holds a held thing only for a heaved hitter | **The owner's report.** Proven: with the old rule put back alone, the light-box guard failed (the crate moved 52 mm) and every fingerprint held again; with the fix, fourteen of fifteen moved, so every run had held boxes un-holding each other | A held crate should rock when walked into |
| **Pressed against a held box** | Three rules the wall-starer test forced once the boxes held (seed 41: the other bully, a bottle in his arms and the flames close, stood nose first against a fallen box at the archway for the rest of the round). (1) `DoorBehaviour.TryHeaveHeldThingInTheWay`: at a panic decision, the strong with free hands heave a held thing right in their way (`FindBlocking(..., heldOnly)`: a laptop in the archway must not hide the crate behind it); sliding along a held box never counted as blocked, so the heave that waits for the blocked count never came. (2) `MayHeaveNow`: strength 7+, hands free, flames not inside their danger distance; otherwise the map's cut-off applies to them as to the weak, and the cut-off asks for the *far* approach of an open door (`MustReachToUse`), and an open doorway with no lane a body and a map square wide clear of held things is walled (`DoorwayIsWalled`). (3) In the blocked branch, somebody blocked by a pinned thing they may not heave (a held box, a wall of crates: `FindBlocking(..., pinnedOnly)`) gives that doorway up for a while (`GiveUpTheDoorwayForAWhile`, the crowded door's rest) and chooses again, from the room they came from if they are standing in the doorway's strip, where every choice is otherwise "keep going" (seed 8: the chancer in the archway for five seconds). (4) A barricade carrier pressed against a pinned thing for a moment gives the barricade up (`BarricadeBehaviour.PressedAgainstAHeldThing`; seed 42: the coward against the stockroom's crate wall with a chair in his arms for the whole ten-second timeout) | The owner's rule that the panicked push past or go round, made to mean go round. Without these a strong person in danger, or with a bottle in hand, pressed at the boxes until the fire came | The map should answer "walled" itself (its far-side reachability finds the long way round through the stockroom, so it cannot) |
| **The fetch knobs** | `FetchBraveryMinimum` 8 and `GrabOnTheWayRangeMillimetres` 3000, unchanged | Measured at 4 / 6 m: the same 3.8 of 20 and seven over half | The crowd half caps the top: then measure the knobs again |
| **The fifty-seed check** | `HandsOffBaselineMeasurements.FiftySeeds_LeftAlone_NeverMoreThanHalfLive`, seeds 40-89, `Explicit`, `Category("Measure")`, about 4.5 minutes; asserts no seed above half, prints the average. Red today, on purpose | The owner's rule as a check, not a hope | It goes green |
| Versions | `SimulationCompatibilityVersion` 72 -> 73; `ContentRevision` 84 -> 85. Fourteen fingerprints re-recorded, all for the gliding boxes; seed 46 opened held | -- | -- |
| Tests | New: `DirectorCapEditModeTests` (the allowance from its own stream; a standing trap sprung by the push a beat later; the socket in the on-course room, then the rest; the fuse box once a socket has gone; nothing before the door opens; nobody on course at a shut card door; a massacre gets nothing more). `KeycardEditModeTests` (a frightened ordinary person pulled to the card pockets it; not without the pull; not into the flames; two pulled, one goes). `HeavyThingsEditModeTests` (a light box kicked into a held crate leaves it). `BoxTowerEditModeTests` (once held, a fallen box never moves again; a guard, since seed 42 did not glide on its own). `SimulationEditModeTests` (the versions) | -- | -- |

## Prototype 3: the hand (2026-09-29)

The gameplay loop, from a design conversation in which the owner rejected
band-aids and cards and chose hold-only influence, a tug on people, a reach
through open doors, no hand on the tower, the round judged against left
alone, the hand working before the fire, the door hand on the right button
with the camera drag removed and eight snaps, and a banner for the
building's turn. Every default below was chosen on the owner's behalf and is
theirs to overturn; the owner's own rules are marked.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **A hold, not clicks** | `InfluenceSystem` keeps one `Place`; a press (`InfluenceDoor` / `InfluenceThing` / `InfluenceSpot`) replaces it at `MaximumLevel` 20, `ReleaseInfluence` clears it, nothing fades. `TicksPerStepLost` and `MaximumPlaces` are gone; `StackRadiusMillimetres` stays only as "a pull beside a thing counts as on it" (the pull station) | **The owner's rule.** A hand that is either on or off has no numbers to tune and nothing to take back | Never |
| **Reach through open doors** | `FeltBy` measures a walk: straight in the place's room, else the shortest way through one open doorway (`IsDoorOpen`, door-centre to place added), capped at `ReachMillimetres` 12000; a wall or a shut door is nothing | **The owner's rule**: "through open doors, but limit range to be around a room's length". One doorway deep is that length and costs one loop over the doors per question; the routes' distance fields would cost a route per person per decision | A pull is wanted round two corners |
| **The use is spent, the pull stays** | `Place.Spent`: `Spend` marks it, `PlaceOfDoor` / `PlaceOfThing` skip a spent place, `FeltBy` / `DoorBonus` / `SpotBonus` do not | Under a hold, removing the place on use would let the next person drawn to a door shut it again a beat later; keeping the gather and spending the use is what "hold to draw people to it" means | Never |
| **The easily led get up sooner** | `LeaveTaskChancePerMille` 100 → 300 | A full pull is instant now; at one in ten a second a meeting took ten seconds to stir, which reads as the hand doing nothing | Playtesters find meetings empty at a touch |
| **The tug** | `TugSystem`, `TugSettings`, commands `TugPerson` / `ReleaseTug`, `AgentTug` on the person, `AgentSnapshot.IsTugged` / `IsShakingFree`, `RunSnapshot.TuggedAgentIndex`. The brake replaces the behaviour's intent with goal speed 0 at acceleration 1 (`ApplyBody` brakes at twice that: 2 mm/tick a tick, five metres a second to nothing in about a second). Nobody alight or down; a hand on somebody who catches fire, goes out cold, gets out or dies comes off | **The owner's rule**, "not 100% instant, more like tugging someone's shirt". Braking the intent rather than pinning the body keeps the physics honest: the crowd can still shove them, and letting go resumes their own plan | A hand on a seated person is wanted to do something |
| **Who tears free** | `TearsFreeFromStrength` 6, `TearFreeTicksAtThreshold` 400 jittered and halved per point above (4 s at 7, 2 at 8, 1 at 9, half a second at 10), never sooner than a reaction lag; `AgentShookFree`, shown for `ShookFreeShownTicks` 100 | **The owner's rule**, "sliding scale ... visibly shaking you off". An ordinary person held for ever keeps the tug a real tool; the brute gone in a moment keeps it a joke as well | The cast's strengths change |
| **The tugged look round** | `NudgeSystem.Startle`: the nudge's reaction (`AgentNudged`, a look round, a re-decision for the frightened) at their reaction tick, without the lurch and without counting toward annoyance | A hand on a shirt is noticed; it is not a poke | Never |
| **Moves that finish** | `ExtinguisherBehaviour.WouldKeepTheBottle` (startled or frightened, upright, not alight, bravery ≥ `FightMinimumBravery`, something burning) is asked by `ItemBehaviour.LetGoIfNeeded` before the fling and by `Decide` with the bottle in hand; `AlarmSettings.PulledBraveryBonus` 2 and `AlarmSystem.StationTheHandIsOn` (within `StackRadiusMillimetres`) stand in for the eight-metre walk; `KeycardSettings.PulledAfterTicks` 100 replaces "about five clicks" | A held lever that ends in sparkles and nothing else is what "vague" felt like; both were on the roadmap's left-out list | Never |
| **The creak** | `TrapSettings.CreakTicks` 150 jittered, `CreakHearingMillimetres` 6000; `TrapSystem.Creak` for a runner's spring and the Director's alike; `TrapCreaked` (strength: ticks to the fall) heard as a `Crash`; the sway in `BoxViews.Creak` from the event, the boxes looked up in the level's `TrapDefinition` | The building plays in the open: the socket already crackled for five seconds, the tower fell a beat after being sprung with nothing to see. Three seconds is enough to move people and not enough to clear a room | Playtesters never notice it, or always escape it |
| **No hand on the tower** | Nothing: a held tower box is a place like any other (people are drawn beside it) | **The owner's choice**: "pull people clear, that is all" | The owner wants to steady or topple it |
| **Right is the building** | `HoldDoor` / `ReleaseDoor` from the right button held (`DoorClicks` on the right button: click = `ToggleLock`, hold = hand); the card is put down on the right press; `CameraRig.ReadDrag`, `Turn`, `IsTurningTheView` and the two drag constants deleted; `StepDegrees` 45 | **The owner's rule** ("remove the camera control. Only use the q, e, but add double the amount of steps"). Left is the crowd, right is the building, and nothing is told from a drag any more | A lean round a wall is missed |
| **The hand before the fire** | Nothing gates a press on the round's phase | **The owner's choice**: yes, fully. "Left alone" starts its fire when the player's does, so the comparison stays fair | Never |
| **The banner** | `RunSnapshot.DirectorPushTick` from `DirectorSystem.LastPushTick`; `PrototypeHud` draws THE BUILDING TURNS ON THE CROWD for `PushBannerTicks` 200 in the alarm's band, over the bells | **The owner's choice**: a banner for the turn, signs at the place for the rest. The allowance stays hidden | Never |
| **The par and the margin** | `PrototypeHud.Draw` takes `LeftAloneSavedCount` for the strip; `RoundScreens.DrawEndCard` shows saved − left-alone in green or red, `LevelSession.RecordMargin` / `BestMarginFor` (PlayerPrefs `paniq.bestmargin.`) beside the best share; the 75% bar stays | **The owner's choice**: beat the building | The owner wants the bar to move |
| **Three or four lines of why** | `EventStory.Retell`: the hand (presses, tugs, tears free, seconds held from the release events' strengths), the card (`KeycardStarted`, `AgentTookKeycard`, `KeycardDropped`, `DoorUnlockedWithKeycard`), the corridor (`BoxTowerFell` with a target, escapes after it), the fire (`FireEscapedItsRoom`, `IncidentPutOut`, the lost) | The smallest form of the vision's retelling that changes what a player does next round | The full retelling is built |
| **An escape spot they can reach** | `PanicBehaviour.ChooseEscapeTarget` takes one reach field a decision (`Navigation.ReachFrom`, rounded to a couple of metres so neighbours share it) and skips any candidate `DistanceIn` cannot reach; with no field to spare this tick the candidates stand as they did | Found by the wall-starer test on seed 41 once the stack creaked: cut off in the stockroom's west lane with every door written off, people picked spots across the crate walls and ran at the crates for the rest of the round, because the flight falls back to a straight line when there is no way through | Never |
| **Creeping is being blocked** | `AgentIntent.PressedTicks`: running at `PanicSettings.PressedSpeedMinimum` 20 mm/tick or more while moving less than `PressedStepMillimetres` 5 a tick, counted every tick; after `BlockedGiveUpTicks` (12) of it, somebody pressed against a held or pinned thing they cannot heave gives the doorway up for a while and chooses again, as the blocked already did | The blocked count only sees a step the engine refused; a body sliding along a crate is accepted a few millimetres at a time and never counted. The 2026-09-28 rule ("go round") could not fire for anybody still creeping | A crush counts as pressed too often (it only re-decides when a held thing is in the way) |
| **Versions** | Rules 73 → 74, building 85 → 86, all fifteen fingerprints re-recorded; the creak moves every run in which the tower falls, and seven moved again for the two flight rules above | The rule for a change meant to alter behaviour | Never |

## Prototype 3: the hand, second pass (2026-09-30)

The owner played the hand on seeds 42 and 3 ("a HUGE step forward. I actually
had glimpses of gameplay. But far from tweaked") and sent nine notes, a new
control and a change. Asked, the owner chose: the right button is **pure
push-away** (the key and the hand holding a door shut leave the mouse); under
the hand the card door is **battered but holds**, and the hand there sends
somebody for the card; **left alone stays as it is** (3.0 of 20, zero
allowed; not to be retuned); **most answer, strong wills refuse, and against
their will should be often** ("again -- we need clear influence"); and the
tower falls **where the runner was** when it began to creak. Every default
below was chosen on the owner's behalf and is theirs to overturn; the owner's
own rules are marked.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Strong wills refuse** | `InfluenceSettings.RefusesFromLeadership` 9, `RefusesFromEvil` 9: `Susceptibility` 0. On the office: the host and the bully | **The owner's rule**: "most, strong wills refuse". Nine picks exactly two of twenty | The cast changes |
| **Everybody else feels it** | `MinimumPercent` 10 → 60 | A leader at 40% used to be all but deaf to it; now they drag their feet | Leaders read as puppets |
| **Full near the hand** | `FullWithinPercent` 50: full within six metres, fading to nothing at twelve (`FeltBy`) | "Holding next to a group barely made them come closer": at three metres the old linear fade gave three quarters, times their susceptibility | A hand across a room pulls too much |
| **The calm answer in a second or two** | `MaybeLeaveForTheInfluence` for anybody who does not refuse, seated, on an errand or idling (standing, glancing, strolling); `LeaveTaskCheckTicks` 50 → 10, `LeaveTaskChancePerMille` 300 → 400 times what they feel; `EasilyLedNervousness` no longer asked. A person answering one press is not asked again by it (`Intent.ForTheHandPress`) | The old gate (nervousness 7+ or a visitor, once a second) left a desk of steady staff sitting. Seen while building it: at a half-second check a steady person standing idle took two seconds to stir on seed 42, and in the test run more than four | Rooms empty at a touch |
| **The startled turn to it** | `FearSystem.AlertIntent`: felt at half or more, face a pull and edge toward it at half a calm walk, or face away from a push | The startled ignored it entirely | Never |
| **The frightened weigh it at twenty metres** | `FullPullBonusMillimetres` 6000 → 20000 | Six metres, "an exit sign", lost to most differences between a room's ways out | A pull drags people through a longer way than looks sane |
| **A click is a beacon** | `PlayerCommandType.LeaveInfluence`, sent when the button comes up inside the click window (`PlaceHold`); `Place.EndsAtTick` = press + `BeaconTicks` 150; `InfluenceSystem.Advance` releases it (a `PowerReleasedInfluence`, as a release) | **The owner's rule**: "a single click should place an influence beacon for 3 seconds". In the run, so it replays | Never |
| **Push away** | `RepelDoor`, `RepelThing`, `RepelSpot`, `Place.Repels`, `PowerRepelled`, `AgentPushedAwayByInfluence`. The calm walk off to floor of their own out of its full strength, in their room (`TryMoveAwayFromThePush`); the frightened take its bonus negated; a door pushed from, felt at a quarter or more, is not a way out (`DoorBehaviour.HandPushesFrom`). One hand: a push replaces a pull. On a person, a push spot at their feet | **The owner's rule**: "works same as the left mouse button, but in reverse". Measured: a door pushed from lost only to a scored minus twenty metres when the office's other way is longer by more, so a strong push rules the door out | A push should be weighable, not absolute |
| **No key, no hand on a door** | The right button's `ToggleLock` and `HoldDoor` bindings removed; both commands stay in the run for levels and tests | **The owner's choice**, "pure push-away" | A level needs the player to lock a door |
| **Against their nature** | `ActsAgainstNatureFromPerMille` 250: felt at a quarter or more. The bottle: `ExtinguisherBehaviour.BottleTheHandIsOn` sends the frightened for it whatever their nerve; a bottle taken for the hand (`AgentCarry.ForTheHand`) is kept and fought with, standing as close as bravery 7 would. The door: `DoorBehaviour.HandIsOn` forces a try whatever the chance and whatever the strength, keeps them at it while the hand stays, comes back to a door given up or found shut, and each blow does at least `WeakBlowDamage` 1 (forty blows break an ordinary door). The crate: `HandHeaveBehaviour` (calm and frightened) for a loose thing on the map, straining `HeaveStrainTicksAtNoStrength` 250 × (7 − strength) / 7 ticks, halved with a second at it, then `PhysicsObjectSystem.HeaveForTheHand`. The alarm: anybody pulls it. The card: anybody fetches it. `AgentActedForTheHand` (strength: `AgainstTheirNature`) when somebody without the nerve or strength does it | **The owner's rules**: "a cowardly agent should pick up fire extinguisher, an agent with low strength will bash on door", and "against their will should be often" | It reads as puppetry rather than people |
| **The card door under the hand** | Pounded like any door (`AgentForcedDoor`, thud); `DoorSystem.Batter` still refuses it; the hand on it sends somebody who believes the card lies free (`KeycardSystem.PullOnTheCardDoor`) after `PulledAfterTicks` 100 → 50 | **The owner's rule**: "they batter it, it holds", and the hand "sends someone who knows where the card is to fetch it" | Never |
| **The tug** | `BrakeMillimetresPerTickPerTick` 2 → 8: a sprinter stopped in a quarter of a second. The struggle is drawn all the while they are held (`AgentViews`), from their fear, nervousness (fast, small) and strength (slow, big); tearing free unchanged | **The owner's rule**: "a tug must stop agents quicker. They visibly try to shake away depending on personality" | Never |
| **The keycard click** | `PlayerInput.NearestThing` measures to where a thing is drawn (`DrawnMiddle`: its pose, or the desk for a resting thing), `PickSmallThingPixels` 42 for things under 200 mm, and the nearer on screen of person and thing wins. The calm fetch of the card walks round to floor beside it (`ItemBehaviour`, `NearestStandableTo`) with `KeycardSettings.PickUpDistanceMillimetres` reach | "Hit box too small": it was measured 7.5 cm off the floor under a card drawn 74 cm up, and anybody within 45 pixels won the click | Never |
| **The tower falls where the runner was** | Door traps: sprung by the first frightened runner (lowest index) within `TrapSettings.TriggerReachMillimetres` 3500 of the tower's foot, **in its own room** and in sight; their spot and heading kept; the boxes thrown into rows of `HeapWidthMillimetres` 1800 across their heading on that spot (`ToppleOntoTheRunner`). The Director's push with no runner: the nearest frightened person in the crossbar near it, else the archway as before. The creak is heard from the tower itself. The archway still shuts on three boxes lying in it. The stockroom's stack unchanged | **The owner's rule**: "fall next to the first person running past, not in the corridor"; "where they were". Measured: runners in the corridor, through the wall, left the boxes against the wall (two of eight within two metres), hence the crossbar only | The owner wants the corridor cut again |
| **Showing it** | A gold hand over anybody `ActingForTheHand` (`AgentIconViews`); their line thick and bright; a tremble while doing something against their nature; a gold "for you..." sign (`EventSigns`); the push in a cool blue with lines running away; a beacon's ring throbs; the end card's hand line counts the times somebody did for the player what they never would have | "Agents doesn't SHOW the influence in behavior very well" | Never |
| **The review's fixes** | Nobody reacts to the hand on the tick it lands: `InfluenceSystem.HasNoticed` draws each person's own reaction tick the first time a press reaches them (the startled turn, the heave, the bottle, the alarm, the card), and draws nothing for anybody who does not feel it. Somebody who sets off for a crate and gives up is not sent again by the same press (`Intent.HeaveGaveUpOnPress`). A push on a door counts once in a door choice, not as a door and a spot (`SpotBonus`'s `exceptDoor`). Somebody pushed to a wall stands there instead of pacing, and the push is logged once. Doing something for the hand ends when they are done, give up, choose something else or change fear (`InfluenceSystem.StopActing`), so the gold hand does not follow somebody running the other way; the tremble is the run's own judgement (`ActingAgainstTheirNature`), not a guess in the drawing. A right click that puts a card down is not also a push. The hand's options look at what the hand is on before working out how strongly it is felt. Found by a code review of the batch | The owner's rule on reaction ticks, and the review | Never |
| **Left alone** | Nothing tuned. Measured after the batch: 3.9 of 20 (19%), seven seeds of fifty clearing, nine over half (44, 58, 59, 60, 65, 68, 76, 77, 80); before it, 3.0, four and six. The fifty-seed check stays red | **The owner's choice**: leave it as it is. The rise is most likely the tower no longer walling the corridor | The owner says |
| Versions | `SimulationCompatibilityVersion` 74 → 75; `ContentRevision` 86 → 87; all fifteen fingerprints re-recorded (the tower's new fall moves every run it falls in) | -- | -- |
| Tests | New: `InfluenceTheHandEditModeTests` (the steady answer within seconds and the strong-willed never; a click's three seconds; the push sends the calm away and turns the frightened off a door; a coward fights with the bottle; three weak people break a locked door together; the card door pounded and never broken, and somebody sent for the card; a weak person heaves a crate after straining). `BoxTowerEditModeTests.TheTower_ComesDownWhereTheRunnerStoodWhenItBeganToCreak`. `DoorClicksEditModeTests` (the beacon click and the hold; the card aimed at up on the desk). Changed: the tug stops a runner within twenty ticks; the cruel wedge at evil 8 (9 refuses) | -- | -- |

## Prototype 3: eyes and the Tab panel (2026-09-30)

The owner asked for eyes "so we can see the direction they are facing" and
for Tab to open "debug settings" with switches for the vision cones, the
numbers and the icons. Asked, the owner chose **white eyes with black
pupils** and **everything showing at Play, as before**. The rest was chosen
on the owner's behalf and is theirs to overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The eyes** | Two white spheres with a black pupil each, children of the body's capsule (`AgentViews.CreateEyes`), high on the rounded top about 20 degrees above its widest point, 17 cm across on a half-metre person. The whites are unlit, the pupils lit; two shared materials for the whole crowd (`PresentationMaterials.EyeWhite`, `Pupil`). Not recoloured by fear; not drawn through walls | **The owner's choice** of look. Seen in pictures from the play camera: at 11 cm they were lost even close up, and lit whites went grey on a face turned from the light. High on the head, so the camera above still finds them on somebody side-on; from behind they are hidden, which says "facing away" | Bigger crowds make four spheres a person show in the profile; or the model pipeline gives people real faces |
| **The Tab panel** | Tab opens a small panel top right (`DebugView`) with five switches: vision cones, numbers over heads, marks over heads, everyone's stats (the table Tab used to open alone) and the walkable floor (G still flips it too). The stats table sits under the panel | The table stays one key away, and G keeps working | The panel grows past a handful of switches |
| **What starts on** | Cones, numbers and marks on; stats and the walkable floor off | **The owner's choice**: as it always looked | -- |
| **How long a switch lasts** | While the game runs, Reset included; nothing saved to disk, so every press of Play starts from the same picture | A hidden setting carried over from yesterday could look like a bug in a playtest, or change what a test sees | The owner tires of setting them again each time |
| **What "marks" covers** | Everything over a head except the number: `!`, `)))`, `?`, `#!`, `...`, the snowflake, the stars, the leader's star, the gold hand. Not the group band at the ankles or the influence lines, which are on the body and the floor | The owner's word was "icons", the things floating over a head | The owner wants one of those hidden too |
| Tests | `RoundPresentationPlayModeTests`: everybody has two eyes, up on the head and on the side they face; the panel's switches hide and bring back the cones, numbers and marks | -- | -- |

## Prototype 3: the hand, third pass (2026-09-30)

The owner played the second pass and asked for four things: more influence
over the frightened, who "still run around too much"; a debug slider for
"general attraction" with a number, to find the value to keep; people
switching to the task in front of the hand, "like clearing boxes for a path,
or opening a door"; and the hand following the pointer while held, left and
right, "so agents can be guided". And a thorough look at the logic, for
"more clear influence, without losing the aspect of the agents character
traits". The cause of the first was found in the code: to somebody
frightened a hand on the floor was a compass, never a place -- it tilted
which door they chose (`SpotBonus`, a cosine with no distance in it) or which
of eight random spots in their room they sprinted to, and on arriving they
picked eight more. Nothing brought them to the hand. Every default below was
chosen on the owner's behalf and is theirs to overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Is influence one number?** | No: reach, the fade, each person's susceptibility, what a pull is worth in a frightened choice (20 m), the calm's chance to get up, the against-their-nature threshold (250) and more. They all start from `InfluenceSystem.FeltBy`, so **one dial scales that**: `InfluenceSettings.StrengthPercent` (100), multiplying `FeltBy`, uncapped (the chances cap at 1000) | The owner asked for one slider to tune; scaling the shared reading moves everything together and keeps who refuses and who comes first | The owner wants a second dial |
| **The slider** | Tab panel, "Hand strength N% answering now: M", 0-300 in tens (`DebugView.HandStrengthPercent`). Sent as `PlayerCommandType.SetHandStrength` (percent in the point's X), so the run's own clone of the settings changes, a replay replays it, and the left-alone round (which has no hand) is not marked live-tuned. Sent when it moves and again to each new round; nothing sent at the level's own 100. Lasts through Reset, 100 at every Play, never saved | "A slider in debug with a print out number so I can find the sweetspot and later hardcode it" | The sweet spot is found: it becomes the default and the slider may go |
| **The frightened come to the hand** | `HandGatherBehaviour`, a panic option after the keycard and the heave, before the leaders. Takes a pull on the floor or on a thing with no panic use (not the bottle, the card or its door, crates, or beside a pull station; a door works through the door choice). Answer: felt ≥ `ActsAgainstNatureFromPerMille` (250), noticed (`HasNoticed`), every `LeaveTaskCheckTicks` (10) on their own beat, chance `FrightenedAnswerChancePerMille` 400 × felt / 1000. They walk (`FrightenedWalk`, through doors) to `GatherSpotFor` and stand facing the hand; a run beyond 2.5 m, slowing to a walk | The owner: "when panicked, the agents still run around too much". The same answer rate as the calm, so both halves of the round feel the same hand | The frightened read as puppets |
| **Where they stand** | `GatherSpotFor`: a ring by person number, six to a ring at 0.9, 1.4 and 1.9 m, 60° apart with a 30° twist per ring, snapped to standable floor; worked out afresh each tick from where the hand is | People answering a floor hand all used to walk to its very spot and shove; a ring reads as a crowd gathered | Crowds of more than 18 at one hand |
| **Character kept** | Once a second (on their own beat) somebody standing at the hand may break away: chance `BreakAwayPerMille` 300 × (1000 − felt) / 1000; a person feeling it fully never does. Breaking away, having no way there, or the hand moving onto something with its own answer ends it, and the same press does not ask them again (`Intent.HandGaveUpOnPress`). The flames at their danger distance always end it | "Without losing the aspect of the agents character traits": the nervous and visitors stay, leaders drift off in seconds, the strongest wills never come | Everybody or nobody breaks away |
| **Not turned back from the way out** | Somebody within 2.5 m of an open door out of the building (`IsLeaving` on a door that `DoorLeadsOutside`) is not taken by a pull. Between rooms they are: the first version of this rule used any door, and a test showed a person running straight past the hand through the stockroom door the hand itself had made them choose | Pulling somebody back from the way out would read as the game fighting the player | The owner wants to pull people back in |
| **Push, frightened** | Felt at 250 and noticed, inside the push's full strength (+ 0.75 m): they walk away (`InfluenceSystem.AwayFromThePush`, the calm rule moved to be shared), then run on as they would. A dragged push herds | "Same with right click hold" | -- |
| **The hand beats a leader** | Answering calls `LeaderBehaviour.StopFollowing`; the option comes before the leaders. The host still refuses the hand and still rallies anybody who has not answered | A follower of a leader never felt the hand; the heave already sat before the leaders for the same reason | -- |
| **A door hand rethinks at once** | The first tick somebody frightened has taken in a hand on a door (felt ≥ 250), their next door choice is brought forward (`ThinkAgainSoon`, once a press, `Intent.RethoughtForPress`) | They went on toward the old door for up to 1.8 s before the hand was weighed | -- |
| **Drag** | `PlayerCommandType.MoveInfluence` (a point): the held place becomes a floor place there, keeping its press (`EventId`, `PressTick`, `Repels`), so whoever answers goes on answering; ignored for a beacon or off the floor; not written in the story (the command history keeps it). `HandDrag` in the pointer code: a floor press follows at once, a door or a thing only after 800 mm; a move is sent after 250 mm and at most ten a second. The calm strolling to the hand aim at their moving spot, and somebody standing at it follows when it is a metre away | "When left click is held, if then dragged the influence point should move with the pointer ... Same with right click hold" | Moves flood the run on a long drag |
| **Clearing, not one crate** | `HandHeaveBehaviour`: a pull on the floor, a crate, or a piled or wedged doorway, with a crate (`CanHeaveForTheHand`) within `ClearReachMillimetres` 1500, is a clearing hand. Whoever answers heaves the nearest crate nobody else is at (ten metres' worth of preference), then the next, until none is left; no longer spent per heave; written once a press | "Switch to that specific task, like clearing boxes for a path" | Helpers clear things the player did not mean |
| **Heaved away from the hand** | Away from the hand's middle; a crate on the middle itself, away from the heaver. The heaver stands on their own side of the crate. It used to be pushed from wherever the grid's first free spot was, sometimes deeper into the heap | Found in the audit | -- |
| **A task begun is finished** | A crate set off for, a door errand begun, a thing being fetched, a chair being sat on: finished even if the hand comes off (a click's beacon is three seconds). Gathering and pushes still end at once | The owner's "as soon as you let go the agents are on their own" is about where they go, and a click was too short to send anybody | Let go should cancel everything |
| **The calm door job** | Decided at the press (`Place.DoorWasOpen`, `AgentErrand.HandWantsItOpen`): a door already the way the hand wanted is left alone. Walked up to on the side they come from (`SideToUseTheDoorFrom`), not opened from a metre back, nor from the wrong side for somebody drawn through the next room. A wedge is cleared by anybody sent, the weak straining (`HandHeaveBehaviour.StrainTicks`), again if it comes back. An open door that will not shut is not tried again by the same press (`Intent.DoorGaveUpOnPress`). The cruel do not wedge a piled doorway. The errand names the press as its cause | Six faults found in the audit | -- |
| **The calm, answering** | Take the hand in a beat late (`HasNoticed`, the owner's rule). Come with their own bag in hand (four of twenty start with one) but use nothing with it. Come with an errand still to come (only an errand under way keeps them); a due errand waits while they answer; leaving a seat or an errand ends the errand properly (`ErrandBehaviour.LeaveForTheHand`), keeping a cue that came in meanwhile and no longer sitting them straight back down. No side-to-side wander on the way | Found in the audit: people with a meeting later got up for the hand and never went; somebody seated with an errand sat back down | -- |
| **Smaller fixes** | Somebody in a doorway feels the hand (`FeltBy` asks `RoomOf`). The startled edge toward a pull along the route, at the same 250 threshold (it was a straight line at 500). A push that changes a door choice is logged as a push naming its press (it was "drawn", naming nothing). A noise that interrupts a calm person drops a thing they were on their way to fetch and a crate they were heaving (both stuck for good before) | Found in the audit | -- |
| **Left as is** | The `FeltBy(agent, 0)` calls that assume one place: correct while there is one hand. A hand on any thing within a metre of a pull station counts as on the station; dragging past one sends somebody to pull it. "Is the hand clearing?" walks every object each time it is asked (a few times a tick per person while a hand is down): cheap at twenty people | -- | A second hand; a crowd of hundreds with a hand down |
| **Left alone** | Nothing tuned (the owner's rule). Measured after the batch: 4.2 of 20 (21%), nine of fifty clearing, eleven over half (44, 48, 53, 58, 60, 65, 68, 76, 77, 80, 89); before it, 3.9, seven and nine. The hand is not in that round, so the change is the noise fix's. The fifty-seed check stays red | **The owner's rule**: leave it as it is | The owner says |
| Versions | `SimulationCompatibilityVersion` 75 → 76; `ContentRevision` 87 → 88 (four new settings). Four fingerprints re-recorded: seed 42 and 40 cards played and seed 42 cards played with no visitors (the hand is in their commands, which now also cover `SetHandStrength`, `MoveInfluence`, `RepelSpot` and `LeaveInfluence`), and seed 42 doors opened, which moved only because of the noise fix (checked by switching the fix off: the old value came back) | -- | -- |
| Tests | New in `InfluenceTheHandEditModeTests`: the frightened come to the hand and stay while a strong will runs on; a dragged hand takes them with it on one press; one press clears three crates; a crate set off for is heaved after a click's beacon; the calm stand on spots of their own; the hand strength scales how strongly it is felt (0: nobody comes); somebody with an errand still to come answers. `DoorClicksEditModeTests`: the drag follows the floor a step at a time and leaves a door only when pulled clearly off it | -- | -- |

## Prototype 3: tells, and the hand's tally (2026-09-30)

The owner asked for "the visible agent tells", the slider kept, and "the stat
at the end about how many clicks/influence you used this round". Reading the
code showed every dangerous commitment decided and started on one tick:
`DecidesToDash`, `MakeScared`'s freeze, and the keycard, alarm, help and
extinguisher options. Every default below was chosen on the owner's behalf
and is theirs to overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Three tells** | Going stiff (before a freeze), gathering nerve (before a dash through the heat), turning back (before heading back toward the flames). `TellSystem`; state on `AgentIntent`; events `AgentBeganATell`, `AgentCaughtInTime` (neither pays the uproar) | The three commitments that most often end in a death the player could have stopped, and each already has a place where it is decided | A playtest names a death nobody could see coming |
| **Lengths** | `TellSettings`: going stiff 60 ticks, gathering nerve 60, turning back 50, all `Jittered` (the owner's rule on fixed times) | Long enough to see a ring and move the pointer, short enough that a crowd does not visibly wait | Rings close before anybody can reach them, or people stand about waiting |
| **What catches** | A poke (`NudgeSystem.Nudge`), a tug (`TugSystem.Tug`), or the hand felt at `ActsAgainstNatureFromPerMille` and noticed, pull or push -- except a pull on the very door or thing the tell is about. It takes hold at `ReactionTick` | Every move the player has saves somebody, and "nobody reacts on the tick" | -- |
| **What a catch does** | Going stiff: `Unfreeze`, they run. Gathering nerve: the door avoided for `DoorAvoidMinimum/MaximumTicks` (as by somebody who hides). Turning back: no heading back toward the flames for `Jittered(RefusesToGoBackTicks)` (500, ten seconds) | Each undoes exactly the dangerous thing and leaves the rest of their panic alone | -- |
| **When turning back applies** | The walk passes, or ends, within `DangerousWalkClearanceMillimetres` (1500) of a threat (`RoutePassesNear`, `AnyCloserThan`); a bottle carried to the fire always. Never for somebody the hand sent (the card pulled, the bottle pointed at, the station held) | A safe errand earning a tell would be noise; a player who sent somebody has chosen | -- |
| **Going stiff keeps the freeze** | The person is frozen (still, staring) during the tell and the freeze length is unchanged; only the snowflake and the ice colour wait for the ring to close | The freeze's own timing and the three-pokes rule stay as they were | -- |
| **The ring** | A red-orange ring at the feet, 0.85 m shrinking to 0.12 m with `TellProgress`, pulsing faster; drawn in `InfluenceView` beside the tug's ring; never hidden by the Tab panel's Marks switch. Body: a growing shiver, a bounce, a look over the shoulder (`AgentViews`). Signs "going stiff...", "here goes...", "I have to go back!", and "caught!" in green | One shape for every tell, like the creak for every trap | -- |
| **The tally** | `HandTally` from the log and the command history: presses (`PowerInfluenced` + `PowerRepelled`), clicks (`LeaveInfluence`), pushes, pokes (`PowerNudged`, not `AgentNudged`, which a tug also writes), tugs, seconds held (a press to its release or the next press, a tug to its release, shake-free or the next -- the old count missed a press replaced by another), metres dragged (between `MoveInfluence` points of one hold), answered (`AgentDrawnByInfluence`), against their nature, tells begun and caught; actions a minute over the round. Two lines on the end card, worked out once when the round ends; the hand strength added when it is not 100 | "How many clicks/influence you used", and the number that says whether a round was frantic | The owner wants it during the round |
| **The slider** | Unchanged from the third pass (Tab panel, 0-300 %, `SetHandStrength`) | Already built | -- |
| **Left alone** | Nothing tuned (the owner's rule). Measured after the batch: 4.5 of 20 (22%), seven of fifty clearing, seven over half (42, 43, 55, 56, 71, 76, 83); before it, 4.2, nine and eleven. The fifty-seed check stays red | **The owner's rule** | The owner says |
| **Found on the way: the strip outside the way out** | `WorldGeometry.EscapedThrough`: straight out of the gap, out once 800 mm past the wall, as before; off to one side of it, up to 1.5 m beyond the gap's edge, out once the whole body is clear of the wall | The whole-run tests caught people outside the building but never counted: carried out on their backs and getting up to one side, or walking out at a slant after a leader, then wandering along the outside wall. Latent before; the tells' new timing showed it | A level with an outside space people should stay in |
| **Found on the way: the dash's last tick** | When gathering nerve runs out the dash begins at once (`DoorBehaviour.DashNow`), not at the next choice of door | Waiting a tick left the person, not yet dashing, shutting the door against the fire and trapping themselves (the closet test) | -- |
| **Tests that are not about tells** | Tells switched off in two: the put-out square staying out (at its fast spread the second of turning back lets the fire outgrow one bottle) and the two reaches of a yell (a wind-up's draw moved the next yell a few ticks). The pinned-runner check does not count somebody winding up. The three-pokes test waits out going stiff first | Each was measuring something else that the new second moved | -- |
| Versions | `SimulationCompatibilityVersion` 76 → 77; `ContentRevision` 88 → 89 (`TellSettings`). All fifteen fingerprints re-recorded: freezes and dashes happen in every recorded run, and each now comes a second later | -- | -- |
| Tests | `TellsEditModeTests`: going stiff comes first and then the freeze; one poke in it and they run, the log naming the poke, a beat later; the brave gather their nerve before the dash; a tug in it calls the dash off; a walk to a station past the flames turns back first and a safe one does not; tells off, the freeze comes at once; the tally counts presses, a click, a drag, a poke and a tug | -- | -- |

## Prototype 3: the hand's fourth pass (2026-09-30)

The owner's notes after the tells build: "Agents still feel like they don't
really listen ... sometimes no reaction at all, sometimes smallish influence
but often lose attention fast. If getting them to notice or sway their
focus, the focus should mostly stay ... think magnets and fish/bird
clusters"; "influence points and cooldown: using influence depletes a bar
that is automatically refilled continuously"; "I ran a seed 42 and got most
of the agents to the final corridor, but even though I only influenced the
exit, none survived"; the top of the screen cut to saved, lost, still inside
and the seed; the cards removed; the scene renamed; "no band-aid solutions,
good clean systems", for the whole code. Two measurements were taken before
anything was designed (`HandOnTheWayOutMeasurements`):

- **Seed 42, fire at 20 s**, the owner's round: the host (leadership 9, who
  refuses the hand) holds the keycard, the fallen tower cuts him off, he
  wanders the bathroom end for fifty seconds and burns with the card while
  four people pound the way out -- and under the hand they could not give it
  up, and the door could not give. Left alone 3 lived; with the hand 0. The
  screen never said who had the card.
- **Seed 41, fire at 6 s**: left alone 5 lived (the hero fetched the card);
  with the hand on the way out 0: the crowd it gathered blocked the one
  fetcher, who gave up after one second of being blocked, was picked again
  next tick, and fourteen people pounded a door that never gives for eighty
  seconds.

Reading the code found why the hand "does not listen": three mechanisms
stacked, each able to say no -- a chance roll every ten ticks scaled by what
was felt (a tenth at the edge of the reach), every "am I doing this for the
hand" being a comparison with the live press so that a release *or a press
anywhere else* dropped everybody at once (and the calm forgot the hand at
every new activity), and never-again markers plus a once-a-second break-away
roll. Two designs were made independently and merged; the owner decided the
card door gives and that the wider refactor (one task model for calm and
frightened people) is the next stone. Every default below is the owner's to
overturn.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The goal is the person's** | `AgentHand` on every agent: the press whose ask is their goal, a copy of the place (`Goal`, refreshed each tick the live press is felt, so a drag carries them), `Committed`, `Conviction` (per mille), `Acting`, and the once-per-press markers. The scattered press ids on `AgentIntent` are gone | The live place is replaced on every press, so a person who keeps a task must hold their own copy; per-person state lives on `Agent`, where the tug and the card already are | The next stone: one task model for calm and frightened people |
| **Conviction, one number** | Grows every tick the live press is felt by `felt × ConvictionGainPerTickAtFullPull (5) / 1000` (nothing to full in four seconds beside the hand, ten seconds to the commit line at a quarter felt); they set about the task at `AnswerFromPerMille` (100); the hand coming off keeps the goal at `CommitFromPerMille` (500, two seconds of a full pull) and drops it below; a kept goal fades `CommittedDecayPerSecondPerMille` (20) × 100 / susceptibility once a second on their own beat (a leader at six tenths in thirty seconds, an ordinary person fifty, a nervous visitor a hundred); a failed attempt costs `GiveUpCostPerMille` (300) and `RetryAfterTicks` (150, jittered). Removed: `LeaveTaskChancePerMille`, `FrightenedAnswerChancePerMille`, `BreakAwayPerMille`, `WanderToItPerMille`, `EasilyLedNervousness` | Replaces the three stacked mechanisms with one that grows and fades; character shows in how fast, not in a coin flip. Nobody breaks away *while* the hand is on them | The owner names a feel: "they stick too long" moves the decay, "too easily" the commit line |
| **What ends a goal, one rule** | `InfluenceSystem.Advance`, for everybody in index order: out cold, alight or in the player's tug drops it; a push felt and taken in drops it; a fresh press felt and taken in *replaces* it, conviction kept; a hand off or elsewhere commits at the line or drops; a kept goal fades to nothing. Plus one line in `PanicBehaviour.Decide`: flames inside their danger distance drop it unless they are dashing on purpose. Behaviours never decide this; they call `Answer`, `Done`, `GiveUp`, `Interrupted` | The owner: "if not another external action or big personal choice". Every behaviour applying the same rule is what makes it a system rather than a set of special cases | -- |
| **Two questions** | `TryGetLivePull` / `TryGetLivePush` are the hand *now on*, felt and noticed: what a tell is caught by, what the startled edge toward, what a push does. `TryGetPull` is the person's goal, live or kept, with a drive that is the greater of their conviction and what they feel now. Everything a person *does for* the hand asks the second; nothing reads `influence[0]` any more | A kept goal must not silently catch every tell for a minute; a committed pounder eight metres from the door must still count as driven hard | -- |
| **The charge** | `HandChargeSystem`, its own system: `HandChargeSettings` capacity 3000, drain 2 a tick while the hand is on a place (a beacon included) or a person, refill 1 a tick always, a press needs 150. A full bar is a minute of holding and refills in a minute; run dry, the hand comes off the place and the person (`PowerHandSpent`), and no press is taken until it has rested three seconds; the hand does not come back on by itself. `PlayerCommandSystem` asks one gate; `TheBuilding.WithThePlayerAbleToAct` switches it off for the tests that hold as long as they like | Its own system because it governs the tug as much as the place; a minute so one hold can gather a crowd by dragging and then pound the card door open | The owner's play: the bar should run dry about once a round |
| **The card door gives** | **The owner's decision.** Counted in pounding *time*: each tick up to `CardDoorPoundersCounted` (3) people shouldering it for the hand add one to `DoorRuntime.HandPound`, and at `CardDoorPoundTicks` (6000) it bursts (`DoorSystem.SettlePounding`, once a tick after the blockages): three or more people forty seconds, two a minute, one two minutes; the damage tint shows it weakening. Anybody `ForcingDoor` with the goal on it counts, live or kept. `Batter` still refuses card doors, and without the hand nobody pounds one | Blows come every 20-30 random ticks, so a count would be a noisy timer; strength would make the outcome depend on who is there, where the owner asked for about forty seconds; a doorway fits three shoulders, so fourteen people do not turn forty seconds into nine | The owner wants the kept goal not to count |
| **The fetcher's patience** | A fetcher the hand sent is given up on when blocked for `BlockedGiveUpTicks × PulledPatienceTimes` (5, five seconds) and after `FetchTimeoutTicks × PulledTimeoutTimes` (2, sixty seconds); giving up costs conviction and a beat, so the claim passes to somebody else. `PulledAfterTicks` is gone: conviction cannot reach the line before the notice beat, so a glancing press turns nobody back | Seed 41 | -- |
| **The card is shown** | A small yellow card over whoever has the keycard (`AgentIconViews`, a mark like the others, so the Tab panel's switch hides it with them); the way out's hover line says who has it or that it lies free | Seed 42: the player had no way to know | -- |
| **The top strip** | `PrototypeHud.Draw`: one strip, `Saved / Lost / Still inside / Seed`, bold 15 pt; the tick, the fire, the calm and scared counts, "Need N of M" and "Left alone" are gone from it (the last two in the Tab stats panel's footer, and on the end card); the hover line at the bottom left above the bar | **The owner's rule**: "remove all but saved lost still inside and seed. Make it easier to read at a glance without making them huge" | -- |
| **Cards gone** | `PurseSettings.CardsFromTheDead` (false): the deck deals nothing and draws no opening card; `StartingHand` still honoured for tests and a level that hands cards out. `PrototypeHud.DrawCards`, `PlayerInput`'s card picking, `CardAimRing` and its test, `RunDriver.QueueCard` deleted; the commands stay in the simulation | **The owner's rule**: "remove cards" | A level wants cards: switch it on |
| **The rename** | The scene is `prototype_fire_1_fl_small` (`Bootstrapper.PrototypeSceneName`), the level's id and name likewise ("Prototype fire 1 (one floor, small)"), the scenario id `prototype-fire-1-fl-small`. The asset files (`TheOffice.asset`, `FireReactionScenario.asset`) keep their names: four hard-coded paths and a menu, never seen by the player | **The owner's rule** | -- |
| **Found on the way: the heap unpiled itself** | `WorldGeometry.IsDoorwayPlugged` gives a doorway heaped with fallen boxes no slab: the heap is the wall there (people are stopped by the boxes; the map and the fire treat it as shut). The walls commit's 200 mm slab, appearing in the archway the moment it counted as piled, shoved the heaped boxes out of the gap within seconds, so the heap cleared itself and the corridor stayed open | Seeds 48, 64 and 67 left alone saved eighteen of twenty after the walls commit; the archway went "piled" and "not piled" inside five seconds every time | -- |
| **Left out** | Somebody mid-chat leaving for the hand (the errand's partner would be left waiting); a hand on the host (he refuses, as the owner chose); the fetcher passing through a crowd that makes way; the wider task model (the next stone) | Each is its own change | The next stone |
| Versions | `SimulationCompatibilityVersion` 78 → 79; `ContentRevision` 90 → 91 (`HandChargeSettings`, the conviction settings, `CardsFromTheDead`, the card door's pounding, the scenario id). All fifteen fingerprints re-recorded: the hand runs answer differently, and every run somebody dies in has lost its deal line | -- | -- |
| Left alone | Nothing tuned (the owner's rule). Measured after the batch: **3.2 of 20 on average (16%)**, three seeds clearing the 75% bar and five saving more than half (58, 62, 69, 80, 88); the batch before was 4.5, seven and seven. Nobody plays the hand in that round, so the change comes from the walls (a harder doorway crush; the heap now holds, where for a few minutes of this batch's building it shoved itself clear) and nothing else here. The fifty-seed check stays red, as it was. | **The owner's rule** | The owner says |
| Tests | `HandChargeEditModeTests` (holding drains and resting refills; an empty bar takes the hand off, is refused and rests; a tug drains it too; a beacon costs its three seconds; left alone it never moves); `InfluenceTheHandEditModeTests` (a hand held two seconds and let go finishes its ask; a flick commits nobody; a leader loses a kept goal before a nervous visitor; flames drop it; a tug drops it; a moved hand takes them with it, conviction kept; the card door gives in about forty seconds under the hand; the hand on the card door still sends for the card); the calm drift test keeps its goal then fades; the two fetchers test starts sooner; the economy tests ask for cards; `TheBuilding.WithThePlayerAbleToAct` switches the bar off. All fifteen fingerprints re-recorded, and again after the heap fix, both proven twice | -- | -- |

## Prototype 3: walls as thick to the feet as to the eye (2026-09-30)

The owner: "Objects (like chairs) often clip inside walls, maybe more."
Measured first (a wall-penetration probe, `Run.WallPenetrationForTests`, over
ten seeds left alone): the physics engine sinks a loose thing more than 20 mm
into a wall on about ten of 120,000 sampled ticks -- the world was not the
problem. The picture was: `RoomView` drew every wall 400 mm thick over a slab
the engine built 40 mm thick, so a chair shoved against a real wall was drawn
180 mm inside the drawn one. A repair found on the way, in a commit of its own.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **One thickness** | `WorldSettings.WallThicknessMillimetres` = 200, read by `PhysicsWorld` (wall pieces and door plugs), `RoomView` (the drawn wall) and `WorldGeometry.PieceOfWall` (the map's wall segments) | The drawing and the world must agree, and 200 mm is an ordinary interior wall; 400 mm to the physics would have cost every room 20 cm a side and moved every prop against a wall | A level wants thick outer walls and thin inner ones: a thickness per wall side |
| **Clearance to the face** | `NavigationGrid.Wall` carries a half-thickness; `DistanceFrom` is the distance to the slab itself -- half a thickness each side of the line, square at its ends -- never negative; a table's edge has none, being its face | The walkable map measured to the wall *line*; with a thicker slab and no change here, everybody standing 250 mm from a line would have been 150 mm inside the engine's wall and shoved out every tick | -- |
| **Doorways keep their width** | `WorldGeometry.AddWallWithItsDoorwaysRemoved` runs a wall piece half a thickness past the room's corners (so two walls meeting there leave no crack) and ends it square at every doorway; `PhysicsWorld.SetWalls` builds exactly the piece it is given, where it used to overrun both ends of every piece | Overrunning every end would have narrowed every doorway by a whole thickness: the 800 mm stall doors left 202 mm for a 250 mm body and the floor plan was refused | -- |
| **A door's face** | `WorldGeometry.IsObjectInDoorway` and `DoorBehaviour.HasReachedClosedExit` measure from the door's face, half a thickness inside the wall line the door's centre sits on | A chair shoved against a shut door came to rest 100 mm short of the line and no longer counted as wedged, so the door opened and the runner shoved the chair into the gap, where it jammed with nobody trying the door (`AChairWedgedInTheWayOut_IsThrownClearByAnOrdinaryPerson`) | -- |
| **Props** | The stockroom's two crate walls and the trap's stack move 50 mm off their walls (`PrototypeBuilding`: wall A from z -5600 to -5550 and so on up, wall B and the stack from -900 to -950 and on down); nothing else. The new check `PhysicsObjectsEditModeTests.NothingAuthored_StartsInsideAWall_OrIsShovedIntoOne_OnTenSeeds` (ten seeds, five seconds each, nothing more than 20 mm into a wall) found them 50 mm inside the new face | Everything else in `PrototypeBuilding` stands at least 100 mm off a wall line | The check fails on a new floor plan |
| Versions | `SimulationCompatibilityVersion` 77 → 78; `ContentRevision` 89 → 90. All fifteen fingerprints re-recorded: every wall-side square lost 100 mm of clearance, so every spot chosen beside a wall moved | -- | -- |

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
