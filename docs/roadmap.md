# Prototype roadmap

The prototype is built stone by stone (see [goals](goals.md) for what
"prototype" means here). This page records the stones laid so far and how the
next one is chosen. It is not a schedule and not a feature list for the
finished game.

**Where things stand (2026-09-22).** Prototype 1 -- the fire-reaction office
-- is finished and merged into `main`. Prototype 2 is under way in the same
scene: it is the stone of *game*, and its first five stones are listed below.

## Foundation (complete)

| Note | What it settles |
| --- | --- |
| [Simulation contract](simulation-contract.md) | Ticks, seeded randomness, stable IDs, events, processing order, and the simulation/visuals boundary |
| [Scenario data versus runtime state](scenario-runtime-state.md) | What is authored versus what changes during a run, including replay data |
| [Agent state model](agent-state-model.md) | The minimum record each person needs |
| [Causal event log](causal-event-log.md) | How cause-and-effect events are kept and inspected |
| [Movement and spatial-world rules](spatial-world-rules.md) | Positions, room bounds, occupancy, and movement resolution |

## Prototype 1: the stones laid (finished)

Every stone below lives in the [fire-reaction prototype](fire-reaction-prototype.md)
scene, and together they are prototype 1. Prototype 2 keeps all of them and
adds to the same scene.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Room and people | System | A 12 m room with ten capsules and an isometric camera |
| Seeded fire | System | A fire that starts at a seeded spot after five seconds |
| Sight and yelling | Behaviour | People who see the fire show `!` and yell `))` to people nearby |
| Life-like movement | Behaviour | People steer on curves, ease in and out of stops, and keep personal space |
| Calm loitering | Behaviour | Small independent decisions: stroll, stand, glance around, stand near someone |
| Panic | Behaviour | Sprinting, changing their mind, zig-zag swerves, hesitations, following others |
| Spreading grid fire | System | Fire grows square by square, only from squares already burning |
| Cube fire look | Style | Small glowing cubes that bob, spin, flicker and fade to embers |
| Panic temperaments | Behaviour | Some people run, some freeze and tremble for a few seconds, some freeze for good |
| Collisions and falls | Behaviour | Runners collide; hard hits put both on the floor until they get up; runners trip, alone or over fallen people |
| Noise and curiosity | System | Yells, thuds and fire crackle carry a set distance; calm people turn to see what a noise was |
| Readable icons | Style | Red `!` pop-up on noticing, sound-wave arcs on yells, a snowflake when frozen, `?` while wondering, floor ripples for noises |
| Doors and player clicks | System | A red locked door in every wall; click once to unlock it (green), again to swing it open; people who get out count as escaped |
| Using doors | Behaviour | Runners head for a door, open it if they can, rattle and sometimes shoulder a locked one in vain, then look for another way out |
| Physical objects: boxes | System | Cardboard boxes that people kick sliding across the floor; a sprinter can trip over one, and a heavy flying box can bowl someone over |
| Personalities | Behaviour | Everyone has strength, speed, bravery, compassion, evil and nervousness (0–10) that change how they run, collide, freeze and shout; a number over each head and a Tab stats table |
| Hard knocks | Behaviour | Brutal hits can knock someone out cold (stars circle their head); strong people batter locked doors until they burst off their hinges |
| Burning people | Behaviour | Touching the fire sets people alight instead of killing them outright: they run around screaming for 3–6 s, setting alight anyone they touch, then collapse |
| Tables and chairs | System | Three tables people slide around and objects bounce off, and eight chairs that runners kick about and trip over |
| Things catch fire | System | Boxes, chairs and tables heat up near flames, burn, and char; a burning box kicked across the room starts a new fire where it stops; touching burning things sets people alight |
| Carrying | Behaviour | Calm people tidy boxes and chairs away; startled carriers drop or throw what they hold; strong runners hurl things out of their way, the cruel ones at people |
| Side room | System | The east door opens into a small room where runners shelter; fire only gets in through the open door, and walls hide fire and muffle yells |
| Closing doors | Behaviour | The player can close open doors; people shut and lock doors behind them by personality: the evil lock them in others' faces, the kind hold them open |
| Helping each other | Behaviour | The kind shake people frozen with fear awake; the strong and kind drag the knocked-out to an open door and out; the cruel never help |
| Giving way at doorways | Behaviour | Two people who reach a 1 m door together no longer wedge against the frame: whoever is not lined up steps aside, and people in a small room stand clear of the way in |
| Rooms and doors | System | The building is rooms joined by doors: the closet is an ordinary room, inside doors start shut but unlocked, and people walk room to room toward a way out, hiding in the room furthest from the fire only when every way out has failed |
| A second room | System | A corridor east out of the office leads to a meeting room with ten more people in it, who cannot see the fire and learn of it from the shouting; twenty people now share the building |
| Seeing through walls | Style | People and loose objects behind a wall show as pale blue silhouettes through it |
| Office clutter | System | Waste bins, potted plants that never burn, bags, laptops that skitter across the floor, and office chairs on castors that roll when kicked |
| Sitting down | Behaviour | Calm people walk over to a free chair and sit for a while; a fright costs them a moment getting out of it, and the chair is shoved back as they go |
| Fire extinguishers | System | The brave fetch a bottle and fight the fire square by square, the kind hose down someone who is alight; six seconds of spray, and anyone caught in the jet is knocked over backwards |
| Taking charge | Behaviour | A seventh trait, leadership: leaders send the strong at a door that will not open, send somebody for an extinguisher, and gather the people near them; the cruel never do as they are told |
| Slammed doors are a villain's move | Behaviour | Only the cruel shut the door behind them as they leave, and only the very worst turn the key; everyone else leaves it for the people behind them |
| Shoving people aside | Behaviour | The cruel take hold of whoever is in their way and heave them out of it, so a queue at a doorway becomes a scrum |
| Bags and briefcases | Behaviour | Four people walk in carrying something, and fling it away from them the moment they are frightened |
| Fire alarms | System | A red box on each room's wall: somebody who has seen the fire hits one and every bell in the building rings. The nervous stampede; the brave and steady walk briskly out until the fire actually reaches them |
| Breaking and popping | System | Hurled things smash chairs into wreckage and collapse tables, so the floor plan changes mid-run; microwaves and wall sockets go off with a bang that flings debris, floors people and scatters fresh fire |
| Wedged doorways | System | Anything resting in a doorway jams the door both ways. The strong heave it clear, everybody else looks elsewhere, and the frightened and the cruel wedge doors on purpose |
| Influence and cards | System | A meter the player spends on four cards and earns back only by getting people out alive: Beefcake, start a fire, put down an extinguisher, and TNT |
| TNT | System | A hole blown through a wall: wider than a door, permanently open, and impossible to lock — a way out nobody can take away |
| One way out | Behaviour | The office has no way out of its own: the building has a single exit, in the meeting room, so everybody crosses the corridor to reach it and the queue at a doorway becomes the thing to watch |
| A meeting in progress | Style | The second room is a smaller meeting room with one way out. Nine people start sitting at its long table, each in a chair that faces it and most with a laptop in front of them, while a tenth stands at the end of it presenting |
| Things that rest on things | System | A laptop stands on a desk and a box on another box, out of everybody's way, until somebody lifts, throws or smashes what holds it up — and then it drops to clear floor beside it |
| Bangs you can see | Style | A laptop battery, a wall socket, a microwave or a stick of TNT goes off with a flash that lights the room, sparks that fall to the floor, a puff of smoke and a jolt of the camera, sized to the blast |
| Solid furniture | Style | People are drawn their real height, so desks come to their hips; and the see-through silhouette only paints where a wall or a door hides something, so a chair no longer shows through itself |
| Things are real 3D objects | System | Chairs tip over and lie on their sides, bags fly in real arcs over tables and land, boxes pile up, slide off desks and topple when bumped |
| People are bodies | System | A crowd really pushes: a strong rush carries a person along, a knocked-down person lies where they fell and others stumble over them, a blast throws people and they land |
| Crushes | Behaviour | Pack too many people into a doorway and the ones in the middle are squeezed off their feet, with nothing scripted about doors |
| Effects you can see | Style | Bangs throw sparks, debris and smoke; burning things lick with flames and trail smoke; extinguishers fire a foam jet that settles on the floor; knocks kick up dust; smashed furniture splinters and appliances shatter |
| A way out that has just opened | Behaviour | Opening the one door is news: the people who could see or hear it go turn and head for it on the spot, and nobody is still trudging toward the fire on the strength of a door that used to be locked |
| Eager to get out | Behaviour | People with a clear way out in front of them stop dithering, stop zig-zagging, stop drifting with the crowd and run for it — even the ones an alarm had left walking out calmly. The frozen, the burning, the cruel and the ones who turn back to help are still themselves |
| Nobody is sent away from the only door | Behaviour | Backing out of a crush used to mean "go and try the other door", which with one way out meant giving up and wandering. Now they step aside for a moment and come again, and the way out never leaves their plan |
| You can see why a door will not open | Style | A door with a chair wedged in it says so when you point at it, instead of turning green and then doing nothing; and somebody taking charge now sends the strong at a wedged door as well as a locked one |
| Doors that give | Style | A door leaf swings away from whoever pushes it rather than always the same way, and each door carries a setting for the one-way doors a later scene may want |
| Tables that tip over | System | A table hit hard enough tips up on one edge and crashes into a heap of boards; whatever stood on it slides off beside it, and the heap is then just another thing to shove, kick and trip over |
| Sitting properly | Style | People at the meeting table sit on their chairs rather than standing in them, folded down so their heads clear the table, and they scoot the chair in as they settle and shove it back as they rise |
| Stop, drop and roll | Behaviour | Somebody alight may throw themselves down and roll instead of running blind, and about a third of the time the flames go out and they get back up |
| A jet you can watch | Style | An extinguisher is not a laser: the jet works back and forth across the fire, a narrow steady arc in strong hands and a wild wobble in weak ones |
| Reading the crowd | Style | A small panel names what every mark over a head means |

## Foundations rebuilt (2026-09-21)

Not a stone: the ground the stones stand on. The owner asked whether bugs were
being hidden by moving props around the prototype level. They were, and worse
-- whole behaviours had been switched off because nothing could work out how to
cross a room.

| What changed | What it means |
| --- | --- |
| Edit-mode tests ran without closing Unity (`tools/RunEditModeTests.ps1`; retired 2026-09-23 once every test needed the physics engine) | At the time, the whole suite in about twenty seconds; today `tools/RunUnityTests.ps1` asks the open editor instead |
| An index of who and what is standing where | "Who is near me" stops meaning "look at everyone"; the costs that grew with the square of the crowd are gone |
| The floor drawn as 250 mm squares, with real clearance | A doorway too narrow to walk through is refused when the floor plan loads, instead of sealing a room in silence |
| Flow fields | People find their way round furniture and across the building; a crowd of two hundred costs no more to steer than twenty |
| Errands that cross the building | Fetching an extinguisher, hitting an alarm, finding a chair, hauling somebody out and wandering next door all work anywhere they can walk to |
| No more "room zero" | Three places guessed the first room when they could not tell; in a bigger building that threw things across the floor plan |
| What a thing is for, written down once | Sitting and equipment are a row in a table rather than nine rules naming kinds |
| Lay a building out by dragging it | **Paniq > Bake Scenario From Scene** reads rooms, doors, props and people from the scene |

Two hundred people finding their way round furniture now cost about half what
twenty people walking in straight lines used to.

### Deliberately left for later

- **Rooms that are not rectangles**, and TNT cutting a real hole rather than
  installing a permanently-open door. The floor squares already handle any
  shape, so this is a change to how rooms are described rather than to how
  anybody moves. Worth doing when a floor plan actually needs an L-shaped room,
  a room inside a room, or stairs.
- **Burst and Jobs on the movement loops.** The simulation stays plain C# so
  this remains possible. Worth doing when a measurement passes about 5 ms a
  tick, a quarter of the budget, with drawing still to pay for.

## Physics overhaul: what is left

The four stones above replaced the prototype's flat, home-made physics with
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

## Prototype 2: next, in the same building

Prototype 1 is closed. Prototype 2 continues in the **same fire-reaction
office**: the building, the twenty people, the physics, the doors and the
influence cards all stay, and prototype 2 is more layers on top of them. The
owner chose this over starting a fresh scene so that a new layer is playable
the day it is written, and so a run can be judged against how that building
normally goes.

**What prototype 2 is for** was settled with the owner on 2026-09-22: it is
the stone of *game*. Prototype 1 built a disaster worth watching; prototype 2
turns watching it into playing it. A round now begins when the player sets it
off, ends when nobody is left to resolve, is scored, and can be played again
on the same seed or a different one -- which is exactly the loop
[goals](goals.md) says the prototype exists to test.

### Prototype 2: the stones laid

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The round begins, ends and is scored | System | The office opens calm and stays calm until you press **Trigger event**. A strip counts saved, lost and still inside against the fifteen of twenty needed to clear it. When nobody is left to resolve, everything freezes and a card gives the score |
| Start, seed, play again, high score | System | A card before the round with the level, the target, your best ever and a seed box; two buttons after it -- the same seed again, or a new one. The best percentage you have ever saved is remembered between sessions |
| A camera you drive | Style | W A S D slide the view across the building, Q and E swing a quarter turn to the next corner, and the wheel zooms while tilting: further out looks down on the building, closer looks along the floor |
| Pause to look | System | Space stops everything -- people, flames, smoke. The camera still answers you; nothing can be played. Press it again to carry on |
| A floor of an office building | Style | The floor sits on a concrete slab, and below it the outside walls carry on down into a band of dark windows and the top of the next storey, so the level reads as one floor of a tower rather than a plan on a black background |
| Doors cost something | System | Working a door is no longer free: 50 to turn the key, 30 to walk it open, 10 to pull it shut. The building's one way out starts locked, so opening it is 80 of the 100 you begin with, before you have saved anybody |
| A shut door only buys time | System | Fire at a shut door eats it. The leaf darkens for about eighteen seconds and then goes, and the fire comes through. Shutting yourself in is a delay, never a win, and the building no longer has corners nothing can reach |
| The round waits for everybody | System | It runs until everybody is out of the building or dead. It used to stop the moment the people left were merely out of the fire's reach, which ended rounds on top of people still walking to the door. A queue never ends it; only a building where nothing at all has happened for half a minute does |
| Running out beats shutting doors | System | Nobody stops on the way out to pull shut a door they are about to run through. Once the flames have actually reached a door that route is gone anyway, and then anybody will shut it -- which is still the best move somebody cornered has |
| Furniture is knocked about, not destroyed | Style | Chairs and tables are shoved, tipped, rolled and flipped, and are still chairs and tables when they stop. They used to shatter into heaps, which read as a demolition rather than a fire |
| Chairs are for sitting on | Style | People in the office sit down in them. They used to pick the nearest one up and carry it across the room instead, because tidying was offered before sitting and a chair is the nearest liftable thing in an office |
| A quieter screen | Style | While the round runs the screen carries the numbers, the buttons and the purse and nothing else. Every key reminder and the guide to the marks over people's heads moved to the pause screen, which is when somebody is reading rather than playing |
| You can click the person you meant | System | Cards are aimed at bodies on the screen rather than at a spot on the floor under them. Aiming at the floor meant that with the camera tilted low, clicking somebody's chest picked a patch of carpet two or three metres behind them, and the card was never played |
| A star for a leader | Style | Somebody other people are following wears a green star. The people trailing after them wear nothing. Both used to wear the same arrow at different sizes |
| Read the round back | System | **What happened** on the end card opens the whole round as a list: the fire taking hold, every shout, every door forced, shut or burnt through, everybody who caught fire and everybody who got out. The background chatter is folded into a line apiece, and one button unfolds the lot |
| The dead deal, the uproar pays | System | A round opens with an empty purse and an empty bar. The meter fills from the building in uproar -- shouting, thuds, people going down, things catching -- and every person the fire kills deals you one card at random. Cards all cost 30, doors cost what they always did, and the one way out is 80 you now have to earn |
| Cards you throw into the crowd | Behaviour | Five cards — Beefcake, Courage, Terror, Bastard and Cold heart — thrown at a patch of floor rather than at a chosen person. Everybody caught has one dial slammed to the end of its scale for the rest of the round, so the four you meant to save and the one who was doing fine all change together. A circle under the pointer shows what you are about to catch; a throw that catches nobody is free, and a throw that catches the wrong person is yours to live with |

**What these stones deliberately left out.** Clicking a person on the frozen
end screen for the facts about them, and the plain-language *retelling* -- the
log says what happened, not yet what it meant -- are both still to come. They
are the part of the [game vision](game-vision.md) that makes a run
*understandable* rather than merely scored. Also left out: any level select,
more than one level, and comparing one run against another beyond the single
best-ever number.

**One number to watch at the next playtest.** The way out costs 80 of the 100
influence the player starts with. That may be exactly the tension the owner
wants, or it may read as unfair; it is one number (`InfluenceSettings.Starting`)
either way.

**One thing to know about the score.** Two runs are not strictly comparable,
because the same building on a different seed is a different day. The best-ever
number is a personal high-water mark the owner asked for, not a league table,
and [game vision](game-vision.md) still holds that understanding a run is the
real progression.

## Prototype 2, second pass: feel

Started 2026-09-23, after the owner played the round and asked for a camera
that turns under the mouse, people who look like they are walking, and some
way of knowing *why* something happened while it was happening rather than
afterwards.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The read-back actually opens | System | **What happened** on the end card opens the round's story. It was written, and the button was wired to ask for it, but nothing ever answered -- so pressing it did nothing at all |
| A camera you can swing | Style | Hold the right mouse button and drag, and the building turns under the pointer to any angle you like, staying where you let go. Q and E still snap a quarter turn, now from wherever you are looking. The wheel comes straight in for the first half of its travel and only then swoops down to look along the carpet |
| People waddle | Style | People rock and twist from foot to foot as they walk, like somebody play-walking a doll across a table. They used to slide like chess pieces. A shuffle barely moves; a panicked run is all shoulders |
| Little signs say why | Style | The moment somebody catches fire, goes down in a crush or gives up on a door, a small cardboard sign pops up beside them saying so, with an arrow pointing at whatever caused it. It fades in under two seconds. The end card's list is still where the detail lives |

**What this pass deliberately left out.** The signs name the moment, not the
meaning -- "no way through!" rather than "the only other way out was already
alight". The plain-language *retelling* the [game vision](game-vision.md)
asks for is still ahead. Nothing here touches the simulation, so no replay
moved and no version was bumped.

**One thing to watch at the next playtest.** Four signs at once is the cap,
and the same person cannot have a second inside about a second and a half.
In a bad crush that will throw some of them away. Whether the ones kept are
the ones worth keeping is a question only playing it answers.

## Prototype 2, second pass: a real office floor

| Stone | Kind | What the player sees |
| --- | --- | --- |
| A floor plan instead of a test rig | System | A corridor runs the length of the building. The meeting room and the cafeteria open onto one side of it, the open office and the bathroom onto the other. At the east end it Ts: the one way out is up the north arm, and the south arm is a dead end that frightened people will sometimes run down. The cafeteria has a second door onto that arm, so from the cafeteria there is a short way out and a long one. Past everything at the west end is the maintenance room |
| Doorways with no door in them | System | The corridor turns a corner without a door in the middle of it. A room has always been a rectangle, so a corridor that turns is two of them, and until now two rooms could only be joined by a door -- which would have put a swinging door in the middle of a hallway. An **archway** is a doorway with nothing in it: permanently open, nothing to shut, and the fire walks straight through |
| Bathroom stalls | Style | Three stalls off the bathroom, each its own little room with its own narrow door, the way the storage closet already hangs off the office |
| The power runs through the walls | System | The sockets are joined by cable running through the walls, and every run ends at the main fuse box in the maintenance room. When the flames reach a socket it pops and the cable lights like a fuse on a stick of dynamite: a spark crawls along the wall to the next socket, which pops in turn, and so on down the line. When it reaches the fuse box, the box goes off harder than anything else in the building |
| Signs to the way out | Style | Little green signs along the corridor with an arrow pointing the way to the actual exit, and one in each arm of the T -- including the dead end, because somebody who has run down it needs telling they have. They are drawn lying flat above head height rather than upright on a wall, because the view looks down on the building and an upright sign would be edge-on half the time |
| A card for the fuse box | System | A fifth card pops the fuse box yourself. The spark then runs the *other* way, out of the maintenance room and along the line of sockets. It is the far end of the floor from the way out, so it is a deliberate trip to the back of the building |
| The fire could start anywhere | System | One run it starts among the desks, the next behind a bathroom stall door, the next by the cafeteria counter. It used to be drawn from one four-metre patch of carpet in the middle of the office, so the seed changed who panicked but never changed the problem. The corridor is deliberately left off the list: it is the one route the whole floor shares, and a fire starting in it would cut the building in half before the player had touched anything |
| Getting out of a chair | System | People frightened out of a seat come up where they sat and then turn and run. They used to be shoved the better part of a metre straight backwards from the way they were facing in a single tick, and because everyone at the meeting table faces the table, the whole meeting appeared to float backwards into the walls the moment the alarm went |
| Sitting up properly | Style | Somebody at a desk sits upright on top of the chair, with the seat visible under them. They used to be sunk 200 mm into the floor and tipped forward into the table |
| The signs point people out | System | The green signs stop being scenery. Somebody who has no way out in mind, picking a direction to run in, now weighs that choice by the nearest sign they can see, so they are less likely to commit to the dead end. On this floor that is a rarer moment than it sounds -- everybody can work out a route to the building's one way out, and the signs point at that same door -- so the signs change some runs and not others. Making them matter every time means taking away the map everybody is currently given, which is a bigger change and the owner's to make |
| People find their own way out | System | The five clients at the meeting do not know the floor; everybody else works here and runs exactly as before. When the clients panic, "which way?" pops up over them, and they go and look: the doors they can see, the rooms they have not looked round, the far end of the corridor. A cupboard earns "dead end!" and they come back out. The moment a green sign, a door flying open beside them, or their host's shout tells them the way, "this way!" goes up and they run for it. This is the change the row above said was the owner's: the signs, the leadership dial and a door being opened now decide whether a stranger gets out. Whether somebody knows the building is a tick box on each person, so it works on any floor and against any danger |

**What is held still, and why.** The open office keeps its exact old rectangle
and stays the first room, and the storage closet keeps its rectangle and its
door. Dozens of tests name places inside them by coordinate. Moving them would
have meant rewriting tests that have nothing to do with the shape of a building.

**What this cost.** Seventy tests across seventeen files, against an estimate of
about ten. Most were scenes built around "fire on one side, the way out on the
other" -- and the office's test-only way out had to move from its north wall,
which now opens onto the corridor, to its south wall, which still faces the
street. Every one of those scenes had to be turned round with it. The tests now
name places through a shared `TheBuilding` helper rather than writing
coordinates out longhand, so the next time a room moves, one file moves with it.

**A number that turned out to matter enormously.** The spark first crawled at
six metres a second, which is faster than anybody in the building can run. The
whole chain of sockets went off within a few seconds of the first one, and in a
test round with every door open **nineteen of the twenty people died and nobody
got out**. At two metres a second -- slower than a running person -- the same
round saves nine. The speed of the fuse is the difference between a hazard you
can do something about and one that simply happens to you, and it is one number:
`PowerSettings.SparkSpeedMillimetresPerTick`.

**Two things to watch at the next playtest, for the visitors.** A stranger
checks the nearest doors first, and on this floor the nearest one from the
meeting room is the maintenance cupboard, so a lost client may well visit it
before anything useful. Whether that reads as a person looking or as a person
being stupid is the first question. The second is balance: the host usually
rallies the room, and a client facing along the corridor usually reads a sign,
so the clients may turn out rarely to be lost at all -- or, with the host
turned down, lost too long.

**Not yet checked in the editor.** This stone was written without Unity. Its
rules have tests that run outside the editor, and they pass; the whole-run
tests and the ten replay fingerprints need Unity's physics, and the
fingerprints still hold the values from before the change. They have to be run
and re-recorded in the editor (see the decision log).

**One thing to watch at the next playtest.** The building funnels everybody down
one corridor to one door. That is the tension it is built for, but it may simply
read as a queue. The dead-end arm of the T is the other thing to watch: it
exists to be a wrong turn, and whether anybody actually takes it is a question
only playing it answers.

## Foundations reviewed (2026-09-23)

Not a stone. The owner asked for a full review of the code against the game
these documents describe, and accepted its plan in full: six phases of
refactoring on one branch, then merged back. The review's verdict was that the
foundations are sound and that the prototype had outgrown two of its founding
assumptions, "the hazard is the fire" and "twenty people in a small office".
Each phase is recorded here as it lands, with its decision-log entry.

| Phase | What changed | What it means for the game |
| --- | --- | --- |
| 1. Afraid of a threat | The crowd asks `Threats`, never the fire by name; the fire is one `IThreat`. One binding pass replaces eleven setters. Every event type says what it pays the meter | The hunter stone below can be built as a second threat rather than by editing seventeen files. Nothing a player sees moved: all ten fingerprints held |
| 2a. One map | A route between rooms costs what it is to walk, round the furniture, instead of the straight line from door to door. The fields people steer by and the graph they choose doors by now agree | Somebody choosing between two ways out picks the shorter walk, not the shorter line. This is the one review phase that changes a run: the versions were bumped and the recorded runs that moved were re-recorded. Testing it exposed a stranger bouncing through one doorway for ever, fixed in its own commit |
| 3. Dead weight out | The movement rules from before the physics engine (`KeepObjectInRoom`, `ClipsDoorFrame`, `TableHit`, doorway strips, `IsWalkable`, `ClampIntoWalkable`, `PhysicsWorld.RemoveTable`) and the test runner that needed no editor are deleted; the one live use, where a helper drags a casualty to, is a ten-line `ClampIntoRoom` | Nothing a player sees. The world code is about a tenth shorter and no longer describes two ways of moving, one of them dead. All thirteen fingerprints held |
| 2b. The index everywhere | Every question one person asks about the people or things near them reads the spatial index for that patch of floor instead of walking everybody. Rooms come from the navigation grid; IDs are looked up in one step. A panic measurement (fire lit, everybody frightened, up to 500 people) now exists next to the calm one | The cost of a panic no longer grows with the square of the crowd, which is what a larger level needs. Nothing a player sees moved: all ten fingerprints held and the measured runs end identically |

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
   the generalisation (all ten fingerprints held), and a stationary test
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
- **The Director.** The background system that adds and eases pressure.
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
