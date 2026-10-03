# Prototype roadmap

The prototype is built stone by stone (see [goals](goals.md) for what
"prototype" means here). This page records the stones laid so far and how the
next one is chosen. It is not a schedule and not a feature list for the
finished game.

**Where things stand (2026-10-03).** Prototypes 1, 2 and 3 are finished and
merged into `main`; their records are in
[finished stones](history/roadmap-finished-stones.md). Prototype 1 was the
fire-reaction office; prototype 2, the stone of *game*, made it a round you
can play; prototype 3, the stone of *gameplay*, gave the player a hand (hold
a place and people come to it, hold a person and they stop), made the
building push back in the open, gave people tells a click can catch, added a
second office level played as a loop, and rebuilt how people decide so that
calm and frightened people choose their next task the same way.

What comes next, in the order the owner chose on 2026-10-03:

1. **The loop level in level mode.** *Prototype fire 2* (`OfficeLoop`), which
   opens first on the start card: an ordinary way out, a real fire in the
   middle of the floor, the Director's script drawn per seed. The owner plays
   it, sends notes, and plays again minutes later (see `AGENTS.md`). Left
   alone it saves 9 to 28 of 34 on seeds 40-49 (about 56 %), against the
   owner's target of about a quarter.
2. **The hardening pass** when the owner says the level is done (see *Left
   open*, below).
3. **The audit's foundations**: big crowds (simulation, then drawing), then
   levels as data with storeys.

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
2026-09-21, and every pass of prototypes 2 and 3 are recorded in
[finished stones](history/roadmap-finished-stones.md).

## Left open by finished stones

What finished stones deliberately left out, and what each asked the next
playtest to watch, gathered here from [finished stones](history/roadmap-finished-stones.md)
so the open list lives on this page. Strike an item out once it is settled.

*For the hardening pass, from level mode (2026-10-03):*

- **Fifteen tests switched off** (`[Ignore]`, each saying why): three in
  `ClosingDoorsEditModeTests` (nobody locks the way out; re-aim at an inside
  door); five in `ExtinguisherEditModeTests`, three in
  `FrightenedWalksEditModeTests` and one in `CrossRoomEditModeTests` (the
  brave no longer fight the fire unasked; re-aim at a hand on the bottle);
  one in `InfluenceEditModeTests` (the cruel no longer wedge doors); one in
  `LeadershipEditModeTests` (leaders send nobody at the fire); one in
  `RoundEditModeTests` (the closet's six hide rather than jam; re-aim).
- **All fifteen replay fingerprints** to record again, and the versions in
  the three places (`ScenarioData.cs`, `FireReactionScenario.asset`,
  `SimulationEditModeTests.DefaultScenario_IsValidWithReplayIdentity`) bumped
  once.
- **Switches nobody needs any more**: the loop level's rules are everybody's
  defaults now (`HeatChoicesStick`, `FrightenedGoThroughAHeldDoor`,
  `FrightenedGoOnWhenLetGo`, `EachPersonHasTheirOwnDice`,
  `BurningThingsHeatThroughWalls`...); the old branches behind them can go.
- **Documents**: the pages that still describe cards, the purse and the
  set-aside behaviours as current carry a dated note at their top; their
  bodies are rewritten in the pass (`the-office-level.md` most of all).
- **The building blocks** of the 2026-10-03 rebuild of how people decide:
  "walk to a door and through it" and "fetch and carry a thing" are still
  written several times over (kept, because door handling is the most finely
  tuned code and the owner had not yet played the new crowd); and the
  audit's room route table (A2) and fair share of route-finding (A1) with
  the big crowds stone.

*From Prototype 3: gameplay (closed 2026-10-03).* Only what is still open
today; each pass's own lists, many of them about the cards, the purse and
the keycard's fetchers that are gone now, are kept in
[finished stones](history/roadmap-finished-stones.md#prototype-3-gameplay-finished-2026-10-03).

- **Left alone saves too many.** The owner's rule: with nobody playing,
  about a quarter live, and never more than half on any seed. The loop level
  saves about 56 % left alone (seeds 40-49); the office about half (thirty
  seeds). The fifty-seed check (`HandsOffBaselineMeasurements`) stays red on
  purpose until a level meets the rule. Rounds tend to be all or nothing.
- **Replays that always agree, or speed.** The physics engine can work out a
  person walking into two boxes at once a hair differently when its work is
  spread over several processor threads, so a few replay fingerprints come
  out one of two ways (about one run in two to five). One physics thread
  would end that at a cost in speed with big crowds. The owner's choice;
  worth making before the big crowds stone.
- **A fire put out is peace.** On the office, a round where the fire is put
  out and everybody settles inside never ends: people still calming hold the
  stall clock open. Should the building answer a fire that is fully put out?
  The owner's call. (Every loop-level round ends.)
- **A packed queue at a locked way out stands still** for ten seconds and
  more. Whether people in a crush should do something else is open.
- **Getting up in a crowd can hop.** Somebody knocked down where it is
  crowded gets up in the nearest clear spot, up to 800 mm away in one tick
  (`PeopleBodies.StandUp`). Worth a shorter reach, or a step, if seen in play.
- **Person 18 turning on the spot** before the fire on seed 42 of the
  office: parked by the owner ("I'll get back to it if I see it again").
- **The hand-strength slider's value.** The sweet spot the owner settles on
  becomes the default, and the slider may go.
- **Deliberately not built:** more than one hand at a time; the hand felt
  more than one doorway away; a hand on the standing tower; the Director's
  crowd tricks (the holder freezing, a cruel person taking the card, the
  alarm rushed early) and a Director that eases off before a massacre; the
  scene baker authoring traps, the Director's ladder or where the card
  starts; tells for anything but freezing, dashing and going back; sound (the
  creak and the banner are drawn, not heard); a second way out on the office.
- **To watch at the next playtest:** whether people read as guided and still
  themselves (the nervous first and longest, leaders late and briefly);
  whether a minute of hand is the right bar; whether the words at the hand
  say enough or get in the way; whether a second's tell is long enough to
  reach with the pointer.

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
*understandable* rather than merely scored. Also left out: ~~any level select,
more than one level~~ (built in prototype 3: a start card with the levels on
it), and comparing one run against another beyond the single best-ever number.

*From Prototype 2: next, in the same building:*

**One number to watch at the next playtest.** *Settled 2026-10-03: the purse
is deleted.* ~~The way out cost 80 of the 100
influence the player started with when this stone was laid; since 2026-09-25
the purse holds 100 at most, the round opens with 30, and unlocking the way out
costs the whole hundred (`InfluenceSettings`). Whether that reads as the right
tension or as unfair is a question for play.~~

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

### Done (2026-10-03): one way of deciding, for calm and frightened people alike

Built on 2026-10-03 (see *Where things stand*, at the top); what follows is
the brief as it was decided with the owner on 2026-09-30, after the hand's
fourth pass. Today a
calm person and a frightened one run two separate "what am I doing" systems
(`CalmBehaviour`'s activities and `PanicBehaviour`'s ordered options), so every
feature that reaches people -- the hand, the tells, the building's cues -- is
wired into both, and a task begun on one side is cut short and re-answered on
the other. The next stone rebuilds that into one task model a person carries
through fright and calm alike, so a feature is wired once. It is weeks, moves
every replay, and is judged by how cheap the feature after it becomes, so it
lands after the owner has played the fourth pass. The brief is the list of
double wirings recorded under that pass above.

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
  batch in [finished stones](history/roadmap-finished-stones.md) and
  [the cue system](cue-system.md)); its second (2026-09-28)
  caps the round with the building's tricks and eases in a massacre. Still
  to build: the crowd's tricks (the holder freezing, a cruel person taking
  the card, the alarm rushed early), which are what can make "never more
  than half" a rule.
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
