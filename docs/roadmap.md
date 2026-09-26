# Prototype roadmap

The prototype is built stone by stone (see [goals](goals.md) for what
"prototype" means here). This page records the stones laid so far and how the
next one is chosen. It is not a schedule and not a feature list for the
finished game.

**Where things stand (2026-09-26).** Prototype 1 -- the fire-reaction office
-- is finished and merged into `main`. Prototype 2, the stone of *game*, made
the same scene a round you can play and is recorded in
[finished stones](history/roadmap-finished-stones.md). Prototype 3 is under way
in the same scene: it is the stone of *gameplay*, where the level itself pushes
back. Its first batch put a trap in the building; its second gives the round a
build-up (the Director's ladder) and the player an everyday move (influence).
Both are under "Prototype 3" further down.

## Foundation (complete)

| Note | What it settles |
| --- | --- |
| [Simulation contract](simulation-contract.md) | Ticks, seeded randomness, stable IDs, events, processing order, and the simulation/visuals boundary |
| [Scenario data versus runtime state](scenario-runtime-state.md) | What is authored versus what changes during a run, including replay data |
| [Agent state model](agent-state-model.md) | The minimum record each person needs |
| [Causal event log](causal-event-log.md) | How cause-and-effect events are kept and inspected |
| [Movement and spatial-world rules](spatial-world-rules.md) | Positions, room bounds, occupancy, and movement resolution |
| [The cue system](cue-system.md) | The building's day: cues, the errands people run for them, the Director and the timetable a future event editor edits |

## Physics overhaul: what is left

The four physics stones of prototype 1 (see
[finished stones](history/roadmap-finished-stones.md)) replaced the prototype's flat, home-made physics with
Unity's 3D physics engine and added particle effects. Alongside them came the
"Cartoon" and "Heavy" feel presets with live tuning, blast strength scaling
people as well as things, and a standalone stress profile. The profile
meets the budgets at 200 people (1.9 ms a tick) and for particles (0.7 ms a
frame), and misses the physics budget at 500 people by 0.4 ms (3.4 ms against 3).
Still to come:

- **Physics at 500 people.** Profile inside the step (the engine's own work,
  against reading back 1,500 bodies and their contacts each tick) before
  reaching for bigger tools. Worth doing when a level is planned with more than
  about 300 people.
- **Particles bouncing off walls.** Today they bounce off the floor only, and a
  spark can fly through a wall. Worth doing if it is noticed in play.
- **Bangs seen through walls.** Before physics, the sparks and smoke of a bang
  in the next room showed through the wall the way flames do. The new particles
  cannot yet, so a bang out of sight is heard but not seen. Worth doing if
  playtesters miss it.

## Finished stones

Prototype 1 (the fire-reaction office), the foundations rebuilt on
2026-09-21, and every pass of prototype 2 are recorded in
[finished stones](history/roadmap-finished-stones.md).

## Left open by finished stones

What finished stones deliberately left out, and what each asked the next
playtest to watch, gathered here from [finished stones](history/roadmap-finished-stones.md)
so the open list lives on this page. Strike an item out once it is settled.

*From Foundations rebuilt (2026-09-21):*

**Deliberately left for later.**

- **Rooms that are not rectangles**, and TNT cutting a real hole rather than
  installing a permanently-open door. The floor squares already handle any
  shape, so this is a change to how rooms are described rather than to how
  anybody moves. Worth doing when a floor plan actually needs an L-shaped room
  or a room inside a room. Stairs are no longer the trigger: storeys are
  prepared for by a storey number on rooms and positions, with a stair as a
  kind of door between storeys (decided 2026-09-24, see the alignment section
  of [technical decisions](technical-decisions.md)).
- **Burst and Jobs on the movement loops.** The simulation stays plain C# so
  this remains possible. Worth doing when a measurement passes about 5 ms a
  tick, a quarter of the budget, with drawing still to pay for.

*From Prototype 2: next, in the same building:*

**What these stones deliberately left out.** Clicking a person on the frozen
end screen for the facts about them, and the plain-language *retelling* -- the
log says what happened, not yet what it meant -- are both still to come. They
are the part of the [game vision](game-vision.md) that makes a run
*understandable* rather than merely scored. Also left out: any level select,
more than one level, and comparing one run against another beyond the single
best-ever number.

*From Prototype 2: next, in the same building:*

**One number to watch at the next playtest.** The way out cost 80 of the 100
influence the player started with when this stone was laid; since 2026-09-25
the purse holds 100 at most, the round opens with 30, and unlocking the way out
costs the whole hundred (`InfluenceSettings`). Whether that reads as the right
tension or as unfair is a question for play.

*From Prototype 2, second pass: feel:*

**What this pass deliberately left out.** The signs name the moment, not the
meaning -- "no way through!" rather than "the only other way out was already
alight". The plain-language *retelling* the [game vision](game-vision.md)
asks for is still ahead. Nothing here touches the simulation, so no replay
moved and no version was bumped.

*From Prototype 2, second pass: feel:*

**One thing to watch at the next playtest.** Four signs at once is the cap,
and the same person cannot have a second inside about a second and a half.
In a bad crush that will throw some of them away. Whether the ones kept are
the ones worth keeping is a question only playing it answers.

*From Prototype 2, second pass: a real office floor:*

**Two things to watch at the next playtest, for the visitors.** A stranger
checks the nearest doors first, and on this floor the nearest one from the
meeting room is the maintenance cupboard, so a lost client may well visit it
before anything useful. Whether that reads as a person looking or as a person
being stupid is the first question. The second is balance: the host usually
rallies the room, and a client facing along the corridor usually reads a sign,
so the clients may turn out rarely to be lost at all -- or, with the host
turned down, lost too long.

*From Prototype 2, second pass: a real office floor:*

**Not yet checked in the editor.** This stone was written without Unity. Its
rules have tests that run outside the editor, and they pass; the whole-run
tests and the ten replay fingerprints need Unity's physics, and the
fingerprints still hold the values from before the change. They have to be run
and re-recorded in the editor (see the decision log).

*From Prototype 2, second pass: a real office floor:*

**One thing to watch at the next playtest.** The building funnels everybody down
one corridor to one door. That is the tension it is built for, but it may simply
read as a queue. The dead-end arm of the T is the other thing to watch: it
exists to be a wrong turn, and whether anybody actually takes it is a question
only playing it answers.

*From Prototype 2: the building has a day (2026-09-24):*

**What this deliberately left out.** ~~The reactive Director that watches the
run and adds or eases pressure~~ (its first ladder built 2026-09-26; easing
pressure still to come); cues after the disaster has started (~~nobody returns
to calm yet~~ -- calming down built 2026-09-26; "back to work" cues still to
come); a third person joining a chat; refusing a cue by trait;
the host walking the visitors out; a home-time card and button; and a
"lunch ends" line on the timetable for the two who start seated in the
cafeteria. All named in the [cue system](cue-system.md) note.

*From Prototype 2: the building has a day (2026-09-24):*

**One thing to watch at the next playtest.** Whether the calm half now reads
as a day or as a fidget: people opening doors to go to the toilet, chats
starting up beside desks, the office filling its own chairs. The numbers are
all in `DaySettings`.

*From Prototype 2: playtest fixes, third round (2026-09-25):*

**What this deliberately left out.** A fire that starts in the stockroom (one
line, but it changes which room every seed starts in, so the owner's seeds
41 and 42 would move); a door from the closet into the stockroom (it would
make the closet a through-room and take away the office's refuge); the scene
baker still cannot author a swing door; swing doors neither muffle sound nor
take a lock.

*From Prototype 2: playtest fixes, third round (2026-09-25):*

**One thing to watch at the next playtest.** *Settled 2026-09-26: on the
office only people pull alarms, so the player can no longer do this.* The alarm
can be pulled before the fire is triggered, for 30 of the opening 30. Everybody then crowds the
locked way out, and the strong may batter it down before the fire exists.
Escapes before the trigger pay the purse nothing, but the round's saved count
still counts them. Whether that is a tactic or an exploit is the owner's call.

## Prototype 3: gameplay, first batch (2026-09-25)

Prototype 2 made the office a round you can play. Prototype 3 is about
getting gameplay up and running: the level has to be an obstacle, not a
floor plan. The owner's seven notes, built as one batch on
`feat/prototype-3-gameplay`. The level is the same office; what changed is
what it does to you.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The fire starts in the meeting room | System | Every round, the fire starts somewhere in the meeting room -- behind the door one seed, under the table the next. It used to be drawn from one of four rooms. The owner's rule |
| A tower of boxes | System | Two stacks of cardboard boxes, four high, stand in the corner where the corridor meets the T: out of the bathroom door and to the right. All day it is scenery; people walk past it to the toilet and nobody knocks it over |
| The Director's first trap | System | Once the fire is lit, the first person to run near the tower brings it down a beat later. The boxes crash across the archway between the corridor and the crossbar and wall it off: nobody gets through, and the fire coming down the corridor is held at the heap until the boxes have burnt. From then on the only way out is office, stockroom, up the T's south arm. This is the reactive Director's first rule: it watches the fire and the crowd, and springs a trap |
| Boxes you can clear | Behaviour | The fallen boxes are real boxes. Somebody strong enough grabs one off the heap and throws it clear; anyone who can carry one can carry it off; everybody else gives up on the archway and goes round. Once fewer than three boxes lie in the gap the archway is an archway again -- for people and for the fire |
| One pull station | System | The building keeps one fire-alarm pull station, at the far west end of the corridor beside the fuse box room, past the meeting room's door. Pulling it means walking toward the fire, so only the brave do; people will now consider an alarm up to eight metres' walk away instead of four |
| No purse | System | The office has no influence. Every door, every alarm and every card is free, and the bar and the prices are gone from the screen. The purse's rules are all still in the code, switched off on the level: a later level can turn them back on |
| Hold a door shut | System | Press and hold the left button on a door and you have a hand on it. An open door pulls shut as soon as the doorway is clear, and while you hold it nobody opens it, locked or not. Somebody strong enough bursts through it in a single push, and it is off its hinges for good (the owner's rule); everybody else rattles it, gives up and looks elsewhere, and comes back once you let go. A held door is drawn in the blue of a card in hand |
| Poke a person | Behaviour | Click somebody with nothing in hand and you poke them: they lurch backwards and stagger, and a beat later look round for whoever did it (the red `!`). Three pokes inside ten seconds and they are annoyed: an orange `#!` over their head, "leave me alone!" on a sign, and a calm person stops what they were doing and goes and does something else. Somebody sitting down keeps their seat. A poke frightens nobody |

**What this deliberately left out.** The scene baker cannot author a trap; the
fall is placed by the simulation (a guaranteed wall of cardboard) and only
drawn as a tumble, not thrown by the physics engine; there is one trap and
the Director has no menu of them; a burning heap does not yet drop embers
onto the crossbar side; and the annoyed do not yet walk *away* from the
player's pointer, only off to something else.

**One thing to watch at the next playtest.** The tower blocks the short way
out for everybody in the corridor, the cafeteria and the bathroom. Whether the
long way round through the office and the stockroom reads as a tense escape
or as a maze is the question this batch exists to ask. The second is whether
the crash is noticed at all from the far end of the building; the sign says
"the boxes came down!" beside whoever set it off, and the crash is heard
twelve metres.

## Prototype 3: gameplay, second batch (2026-09-26)

The owner was working on the game loop: what the player does over and over,
and why. The answer this batch builds: **the Director adds things people are
pushed away from, the player adds places people are pulled toward, and every
person weighs both by their personality.** One batch, on
`feat/prototype-3-gameplay`, from a brainstorm and two rounds of questions.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| A round that builds up | System | Half a minute to a minute and a half of ordinary day (or less, if you press the button), then a waste bin in the meeting room catches. It smoulders about ten seconds, then spreads slowly while young. An extinguisher hangs on the meeting room's wall |
| The Director's ladder | System | Put the bin out and, twenty to forty seconds later, a socket crackles for five seconds and pops in the busiest calm room; put that out and the fuse box goes, and every socket with it. A fire that gets out of its room is the real fire: the Director stops, and only then can the tower of boxes fall |
| The cable runs one way | System | A socket going off is a bang and a small fire, nothing more. The fuse box going sends sparks down to every socket within about two and a half seconds. A bang no longer lights floor through a wall |
| Danger is danger | Behaviour | A burning bin, a burning chair, a person on fire: people see them and are frightened, as by burning floor. The owner's rule: people sense danger, never "floor on fire", so a zombie will fit the same way |
| Calming down | Behaviour | A frightened person who sees and hears nothing frightening for a while settles, at their own pace -- the brave in seconds, the very nervous never -- and goes back to their desk, rattled for a while. The bells stop about ten seconds after a fire is put out (the all-clear) |
| Only people pull alarms | System | Clicking a pull station draws people to it instead; the brave may pull it |
| Influence | System | Click a door, a thing or the floor: one step of pull a click, up to twenty, fading over forty seconds, felt within twelve metres in the same room. The nervous and visitors follow, leaders and the cruel mostly ignore it, nobody follows it into fire; calm people drift to it and the easily led get up for it. A sparkling aura on the place, a sparkling line to everybody pulled |
| New door controls | System | Click a door: draw people to it. Hold: keep it shut. Right click: turn the key (how the way out is opened). No double click |
| The nudge | Behaviour | Click a person: they step away from the click. Three quickly and they are annoyed, shake, and ignore nudges for twenty seconds |
| The heap is seen | Behaviour | Anybody who can see the fallen tower and cannot heave it goes round; the strong give it about eight seconds first. (Found when a strong visitor pushed at the heap for two minutes in a test run) |
| The purse is "the purse" | Rename | The old currency is called the purse in code and docs, so "influence" means only the new idea |

**What this deliberately left out.** A menu of incidents (one ladder, fire
only); easing pressure after a massacre; influence reaching through open doors
into the next room; talking about the fire afterwards, and the timetable
resuming after a put-out; the scene baker authoring a ladder; any score for a
round the player contained rather than evacuated.

**Things to watch at the next playtest.**

- Whether a contained round should score better than an early evacuation. The
  score is still the share saved, and the ladder always ends in the fuse box.
- Whether unlimited influence turns into painting the whole floor, and whether
  twelve metres and two seconds a step feel right (both single numbers in
  `InfluenceSettings`).
- Whether the sparkling lines read clearly or clutter a crowded room.
- Whether anybody ever puts the bin out. That depends on somebody brave
  enough being in the meeting room, and on the extinguisher by the door.
- People calming during a big fire and panicking again when it reaches them.

**Found on the way, and open.** The physics engine does not always replay a
person walking into two boxes at once the same way when its work is spread
over several processor threads (found 2026-09-26; see "The physics engine and
threads" in [technical decisions](technical-decisions.md)). One replay
fingerprint, seed 41 with the way out opened and nobody a visitor, can fail
about one run in five because of it. Choosing between replays that always
agree and speed with big crowds is the owner's to make.

## Foundations reviewed (2026-09-23)

Not a stone. The owner asked for a full review of the code against the game
these documents describe, and accepted its plan in full: six phases of
refactoring on one branch, then merged back. The review's verdict was that the
foundations are sound and that the prototype had outgrown two of its founding
assumptions, "the hazard is the fire" and "twenty people in a small office".
Each phase is recorded here as it lands, with its decision-log entry.

| Phase | What changed | What it means for the game |
| --- | --- | --- |
| 1. Afraid of a threat | The crowd asks `Threats`, never the fire by name; the fire is one `IThreat`. One binding pass replaces eleven setters. Every event type says what it pays the meter | The hunter stone below can be built as a second threat rather than by editing seventeen files. Nothing a player sees moved: all thirteen fingerprints held |

**Where that leaves the foundation.** Assessed on 2026-09-24, in full in the
[decision log](history/decisions-review-refactor.md#review-refactor-where-the-foundation-stands-afterwards).
It is the right shape for the game these documents describe, proven by tests
and fingerprints rather than by a watched round, and measured at 500 people in
a small building rather than in a large one. The known weak points, each with
what would expose it:

- A route can name doors the feet do not walk, where the shortest walk cuts
  through the room next door; a staged level with several ways round.
- "Look round the room you walked into" means four corners in sight; a
  warehouse-sized room.
- Rooms are rectangles authored in C#; a large floor makes authoring the slow part.
- People are about 25 scene objects each, unmeasured at 500; the frame rate on
  a 500-person level.
- Every performance number is an editor number; the standalone profile has not
  been run since the panic case was added.
| 2a. One map | A route between rooms costs what it is to walk, round the furniture, instead of the straight line from door to door. The fields people steer by and the graph they choose doors by now agree | Somebody choosing between two ways out picks the shorter walk, not the shorter line. This is the one review phase that changes a run: the versions were bumped and the recorded runs that moved were re-recorded. Testing it exposed a stranger bouncing through one doorway for ever, fixed in its own commit |
| 6. The documents | The prototype note says what a round and the camera do now; the exit-sign comments say strangers read the signs; nine comments stop saying tables smash; the version list is a table | Nothing a player sees. The notes the owner reads match the game again |
| 5. The rename | `FireReactionSimulation` is `Run`, `FireReactionSnapshot` is `RunSnapshot`, `FireReactionRunner` is `RunDriver`, `FireReactionEventType` is `CausalEventType`, and every other `FireReaction*` type drops the prefix; files moved with their `.meta` files so the scene and the asset still point at them | Nothing a player sees. The code now says it is the game, not a fire demo; a hunter written into it reads right |
| 4c. A full buffer is not an answer | The physics look-ups (is this spot clear, is anybody in this doorway, is a wall between us) grow their buffers instead of answering from the first thirty-two things found | In a dense crush nobody stands up inside somebody else and no door shuts on somebody it should refuse. Versions bumped; no recorded run reaches that density, so all thirteen fingerprints held |
| 4b. The fire in batches | Every burning square's tile and flames are drawn in a few dozen batched calls instead of three or four scene objects per square, glow through walls included | A whole floor ablaze no longer drags the frame rate down while the simulation is fine. Same look; presentation only |
| 4a. Nothing allocated a tick | The display fills two reusable snapshots turn about instead of building a fresh one every tick; the cost tables are worked out once a run; the stress profile also measures the panicking building | No stutter from memory tidying as the crowd grows. Nothing a player sees today; all thirteen fingerprints held |
| 3. Dead weight out | The movement rules from before the physics engine (`KeepObjectInRoom`, `ClipsDoorFrame`, `TableHit`, doorway strips, `IsWalkable`, `ClampIntoWalkable`, `PhysicsWorld.RemoveTable`) and the test runner that needed no editor are deleted; the one live use, where a helper drags a casualty to, is a ten-line `ClampIntoRoom` | Nothing a player sees. The world code is about a tenth shorter and no longer describes two ways of moving, one of them dead. All thirteen fingerprints held |
| 2b. The index everywhere | Every question one person asks about the people or things near them reads the spatial index for that patch of floor instead of walking everybody. Rooms come from the navigation grid; IDs are looked up in one step. A panic measurement (fire lit, everybody frightened, up to 500 people) now exists next to the calm one | The cost of a panic no longer grows with the square of the crowd, which is what a larger level needs. Nothing a player sees moved: all thirteen fingerprints held and the measured runs end identically |

## Agreed direction for the next stones

Settled with the owner in a design review (see [game vision](game-vision.md)
for the decisions themselves). This is a direction, not a schedule: the owner
still picks each stone after playing the one before it.

### Done: the round ends, and it has a score

Built as prototype 2's first stone, except for clicking a person on the frozen
scene, which is still to come. The rule for when it ends was rewritten
afterwards: it now waits for everybody to be out of the building or dead,
rather than stopping as soon as the people left were out of the fire's reach.

1. **What the player sees:** the round runs until everybody is out or dead, or
   until nothing at all has happened in the building for half a minute. The
   scene freezes and a card reads "You saved 13 of 20 — 65%", with a button to
   read the whole round back. Any person on the frozen scene can be clicked for
   the facts about them: who they were, their traits, and what happened to them
   and when — still to come. One key plays the level again.
2. **Layer:** system.
3. **Deliberately left out:** the plain-language retelling of what happened out
   of sight (a later stone), comparing one run against another, and any menu
   around the round.
4. **How it is checked:** edit-mode tests for the end condition and the count,
   including the case of somebody alive and settled in a room the hazard cannot
   reach, who counts as saved. On screen: play to the end and see the freeze,
   the percentage, and a click on a body producing their facts.

### Then: one hunter

People who only know part of the building went ahead of this, by the owner's
choice, so the hunter meets a crowd where some people can run into it through
a door they have never opened.

1. **What the player sees:** something that walks toward people and turns
   whoever it catches into another one of itself. Not a horde, not a mode —
   one of them, in the existing building.
2. **Layer:** system and behaviour.
3. **Deliberately left out:** fiction, art, weapons, and any second hazard
   family. This stone is not a zombie scenario; it is a test.
4. **Why this one:** the crowd's fear used to point at a grid of burning
   floor squares. A hunter is a threat that *moves and chooses*, which is the
   hardest assumption in the current design. The seam -- "afraid of a threat":
   something at a place, with a size, that can be noticed and that hurts on
   contact -- now exists (`IThreat`, built in the review refactor above), so
   the work of this stone is the hunter itself: one that walks, picks a
   target, and converts whoever it catches.
5. **How it is checked:** the fire behaviour is already proven unchanged by
   the generalisation (all thirteen fingerprints held), and a stationary test
   threat already frightens, is fled from and hurts; this stone adds tests for
   chase and conversion. On screen: the crowd flees a walking threat the same
   way it flees fire.

### After that, in rough order

- **Sound.** Yells, thuds, alarms and screams carried from where they happen.
  The game deliberately has no helper markers, so sound is how a player knows
  to turn the camera. The simulation already emits every one of these events.
- ~~**Cards you throw into the crowd.**~~ Built, alongside the economy that
  deals them. What is left of the idea is the other direction of each dial (a
  *coward* card, a *saint* card) and cards for speed and leadership.
- **The Director.** The background system that adds and eases pressure. Its
  first reactive form is built (2026-09-26): the ladder of small incidents on
  the office, from a bin to a socket to the fuse box (see prototype 3's second
  batch above and [the cue system](cue-system.md)). Still to build: easing
  pressure after a massacre, and a menu of incidents rather than one ladder.
- **Zombies**, rather than the generic hunter below: the vision's *contagious
  plus hunting* mix. The owner chose the fiction; the work is still the seam
  between "afraid of the fire" and "afraid of a threat".
- ~~**A camera the player drives.**~~ Built: moving, rotating a quarter turn at
  a time and zooming, per [look and controls](look-and-controls.md).
- ~~**Pause to look.**~~ Built: time stops and the camera moves, but nothing
  can be spent or played while paused.
- **A staged level**, big enough that calm people are always on screen
  somewhere and no single way out saves everybody.
- **The written retelling** on the end screen.

## Choosing the next stone

The owner picks the next stone after playing the current prototype. A stone
should be small enough to build and play in one step. Its plan names:

1. what the player will see differently;
2. which layer it is (system, behaviour, or style);
3. what it deliberately leaves out; and
4. how it will be checked (tests, and what to look for on screen).

Everything obeys the foundation notes above and the evidence gates in
[technical decisions](technical-decisions.md).
