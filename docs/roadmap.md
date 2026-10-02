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
build-up (the Director's ladder) and the player an everyday move (influence);
its third (2026-09-27) is the fixes from the first playtest of both; its
fourth (2026-09-27) makes the way out a card door, after a measurement showed
the office saving itself with nobody playing; its fifth (2026-09-28) has the
Director cap the round with the building's tricks, and measures what that
can and cannot do; its sixth (2026-09-29) is the gameplay loop itself, the
hand: one hand that pulls people toward a place or holds one person back,
the building playing in the open, and the round judged against the same seed
left alone; its seventh (2026-09-30) is the hand's second pass, after the
owner's first play of it: influence everybody but the strongest wills
answers, people doing for the hand what they never would, a click that
leaves a beacon, the right button pushing people away, and the tower falling
on its runner; its eighth (2026-09-30) is the hand's third pass: the
frightened coming to the hand instead of running past it, the hand dragged
to guide people, a hand on fallen boxes clearing them all, a hand-strength
slider to find the value to keep, and twenty-odd faults found in a read of
the whole hand; its ninth (2026-09-30) is tells -- a second of visible wind-up
before somebody freezes, dashes through the heat or goes back toward the
flames, in which one click saves them -- and the end card's tally of how much
the player used their hand; its tenth (2026-09-30) is the hand's fourth pass:
every person holds the hand's ask as a goal of their own with a conviction
that builds and fades, a bar the hand drains and that refills by itself, the
card door giving to a long pounding under the hand, a card over whoever has
the keycard, the top strip cut to four numbers, the cards gone, the walls as
thick as they look, and the scene renamed `prototype_fire_1_fl_small`; its
eleventh (2026-10-01) is about how fast the prototype can be iterated rather
than about the game: the sketch-and-keep agreement (an idea is played before
it is built to keep), a second dial in the Tab panel for how far the hand
reaches, and a faster test suite; its twelfth (2026-10-02) is the first two
sketches kept: three test levels and a Crowd button for watching one thing
at a time, a hand felt through a doorway only where it can be seen, lines
only to who feels it now, and the keycard worn on the hip. All are under
"Prototype 3" further down.

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

## Prototype 3: playtest fixes (2026-09-27)

The owner played seed 42 with both batches in and came back with twelve
notes. One batch, one commit, on `feat/prototype-3-gameplay`; the owner
answered nine questions about them first. The one note parked by the owner
is a person (number 18, the coward in the cafeteria) turning on the spot
before the fire: "ignore this for now, I'll get back to it if I see it
again".

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Bin, boxes, outlet, fuse box | System | The Director's ladder has the tower of boxes as its second rung. The bin catches as before; the moment the first frightened person runs along the corridor, the tower comes down, whatever the fire is doing (it used to wait for a fire to get out of the meeting room, which is why the owner saw it fall once). Twenty to forty seconds after the boxes fall -- or after a put-out, if nobody ever ran -- the socket crackles and pops in the busiest calm room, and if that is put out the fuse box goes as before |
| Another bin | System | A bin doused before the carpet under it ever caught was no fire at all: another of the meeting room's three bins catches a beat later. The owner's rule, "if the fire in the meeting room is put out too quickly, light another trashcan straight away". Once the carpet has burnt, a put-out is a put-out |
| No bottle in the meeting room | System | The meeting room's extinguisher is gone (the owner's rule). The nearest bottles are the office's and the cafeteria's, so the bin is put out only by somebody who fetches one from another room |
| The boxes really fall | System | The tower is toppled by the physics engine: each box is shoved toward its own slot across the archway and tumbles, bounces and lands wherever it lands. While three or more lie still in the archway's strip for half a second it is shut for people and fire; kicked, carried or thrown out, it opens again; kicked back in, it shuts again. The boxes are ordinary boxes from the fall on -- kicked, carried, hurled, and they hit whoever stands in their way. Measured over ten seeds, three or four of the eight land in the strip every time; the owner chose an honest fall over a guaranteed wall |
| Fewer jams | Behaviour | Small things (a bin, a laptop, a bottle) never jam a door; somebody tidying up never sets a thing down in a doorway; a crowd drawn to a door spreads out in front of it instead of shoving the loose things between them into the gap. Doors used to jam "for no reason" |
| Unlodging a jam | Behaviour | Somebody calm on an errand who finds a thing wedged in the door they want lifts it out and sets it aside (or heaves it, if they are strong and it is too heavy), then goes through; once per errand. A frightened runner throws it clear as before, unless the flames are inside their danger distance or they are alight: too panicked, they give the door up and look elsewhere. The owner's rule |
| Influence is something to use | System | Click a door and somebody drawn to it opens it if shut, or shuts it if open; that spends the pull on it, so the next click asks for the opposite. The cruel (evil 7+) wedge a shut door with the nearest thing instead. Click a chair and somebody sits on it; a box, bin or bag and somebody carries it off; the bottle on the wall and somebody takes it and holds on to it. The floor, and things with no use, gather people as before. Frightened people weigh influence as before. A door they cannot use keeps its pull |
| Nudges, four ways | Behaviour | Somebody annoyed is still shoved by every click, they just stop looking round for it. The angry shake is a fast shake that passes in about two seconds (the annoyance lasts twenty). Three quick pokes wake somebody frozen with fear -- frozen for good included -- and they run. Three quick pokes knock somebody sitting down off the chair onto the floor |

**What this deliberately left out.** A guaranteed wall of boxes (the owner
chose the honest fall; a run where the boxes bounce wide leaves the corridor
open); the spin on seed 42, parked by the owner; influence on a pull station
still draws people beside it rather than making the brave pull it sooner;
a bottle taken for the player is held like a bag and let go of when its
holder takes fright, rather than used by them.

**Things to watch at the next playtest.**

- Whether the heap forms often enough to be the corridor's trap, and
  whether three boxes in the archway read as a wall or as a scatter.
- Whether the second bin feels like the Director cheating, and whether
  anybody ever puts a bin out now that the bottle is two rooms away.
- Whether calm people clearing jams makes doors too reliable, and whether
  the cruel wedging an influenced door is a delight or a trap for the player.
- Whether the socket's wait from the fall, rather than the put-out, reads as
  a rhythm.

## Prototype 3: playtest fixes, second round (2026-09-27)

The owner played again with the first round in and sent four notes and a
request for suggestions: the fallen tower was "easily pushed through, so it's
no real obstacle"; the stockroom should be "more of a zig-zag setup, or
labyrinth", with heavier boxes stacked higher, "a hazard"; people fetching an
extinguisher did not go through shut doors; and after a put-out the Director
should move on sooner. The owner chose: scale the boxes heavier "to suit the
gameplay, not realism"; a winding lane the Director can topple; the socket
popping where the crowd is; a sign when another bin is lit; and, at the plan,
"change socket pop to 5 sec after box topple". Amended into the first round's
commit, as asked.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Heavy boxes | System | Boxes weigh by their size: the small office boxes 3 to 10 kg, the 600 mm boxes 40 kg, the stockroom's crates 55 kg. Making them heavier alone did nothing -- in this engine a running person shoves a 110 kg box six metres in five seconds, measured -- so a box too heavy to carry, once it has lain still for half a second, is **held where it lies** against people: nobody walks through the fallen tower any more. It is on the map people steer by, like a table, so routes go round it. Somebody strong (7+) blocked by one heaves it aside, and a crate it is shoved into slides along with it; anybody weaker crosses off a door the map says they cannot reach and picks another, or a spot clear of the flames. A blast sends held crates flying like anything else |
| The stockroom lane | Level | The stockroom is a winding lane between two walls of crates stacked three high: in at the west door, north up the west lane, round the end of the first wall, south down the middle lane, round the end of the second, north up the east lane to the door by the way out. About fifteen metres of walking where it was nine. Exit signs at each bend; a few light loose boxes in the lanes |
| The stack at the bend | System | The Director's second trap: four crates stacked against the north wall at the lane's first bend. The moment the first frightened person runs through the stockroom the stack comes down across the lane, and the crates, too heavy to carry, lie where they land and cut the lane on the map: the office's short cut to the way out is gone and the office goes round by the corridor. Sprung by running, like the tower; falls with the physics engine, like the tower |
| Through shut doors | Behaviour | Somebody frightened going for an extinguisher, for the flames with one, or for a pull station opens the shut doors on the way, as a calm errand does. A locked door is tried once and the bottle behind it given up for the rest of the round. They used to walk into the shut door, push at it for a second, give up, pick the bottle again and walk into it again |
| The socket, sooner | System | The socket pops five seconds after the boxes fall, whatever the bin is doing (burning, out, or got loose): bin, boxes, outlet is the order. With no fall it pops five to ten seconds after the bin is put out; the fuse box goes five to ten seconds after the socket's fire is put out. It pops in the room with the most people, calm or frightened, never a room the incident already has |
| Another one! | Presentation | When a second bin catches, "another one!" hangs over it and the story says "another bin: the waste bin caught fire" |

**What this deliberately left out.** Boxes off the heap thrown into the
corridor (offered; the owner did not pick it); crates that block by weight
alone (measured, and they cannot in this engine; see the decisions); a
guaranteed seal of the lane by the fallen stack (four crates do not fit flat
across the gap, so they scatter, and the map is whatever they leave).

**Things to watch at the next playtest.**

- Whether a held crate reads as a crate that will not budge or as a crate
  glued to the floor, and whether the strong heaving one aside is seen.
- Whether the stockroom lane is a hazard or a nuisance to the office, and
  whether the fallen stack ever traps somebody in the west lane.
- Whether five seconds from the fall to the socket is a rhythm or a rush.
- Whether people cut off from a door by crates find another quickly enough,
  or stand about in the room until somebody strong clears a way.

## Prototype 3: the keycard (2026-09-27)

The owner stepped back to look at the game loop and asked for a measurement:
what does the office come to with nobody at the controls? Ten seeds, left
alone: seven of ten cleared the 75% bar and sixteen of twenty lived on
average, because somebody always pulled the alarm and the strong always
battered the locked way out down. The player's one decisive move, turning
the key, only made that happen sooner. The owner's answers: left alone,
about a quarter should live; the way out needs a **keycard** that the seed
puts in a member of staff's pocket or on a desk ("random B or C"); once
swiped the door stays open for good; the card does not burn; and the end
card should say what would have happened left alone. One batch, one commit,
on `feat/prototype-3-gameplay`.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| A card door | System | The way out no longer gives to a shoulder, the fire does not burn through it, and your right click does nothing on it: "NEEDS THE KEYCARD" when you point at it. Only somebody with the card opens it, and then it is an ordinary door for the rest of the round |
| The keycard | System | A small bright yellow card. Half the seeds it starts in a member of staff's pocket (never a visitor's); the other half it lies on one of the office's three desks. Point at a person and the line under the score says HAS THE KEYCARD; the Tab table says so too |
| Who knows | Behaviour | Everybody who works here knows where it started; the visitors do not. Anybody who sees the card, or sees who has it, learns where it is a beat later |
| Going for it | Behaviour | Somebody frightened and very brave (bravery 8 or more: on the office, the hero) grabs it off the desk beside them as they run, if they work here and it lies within three metres; or goes back across the building for it once they have found the way out shut and know where it lies. One person at a time (a fetcher who trips stays the one going; one knocked out or killed lets somebody else go), never into the flames, and only when they could walk there. They go where they *believe* it lies: a card knocked off its desk unseen sends them to the desk, and they find it gone |
| The swipe | Behaviour | Whoever has the card makes straight for the way out: no fire to fight, nobody to follow, and swipes the reader from a couple of metres, from the back of the crush in the doorway. The door opens at once for whoever is rattling it |
| Losing it | Behaviour | Somebody out cold or dead drops the card where they lie, for anybody to pick up. A trip, or a knock-down they get up from, keeps it in their pocket. A card dropped in the fire waits, unburnt, until the flames have passed |
| Your pull on the card | System | Click the card and somebody calm nearby goes and pockets it, the pull spent |
| Left alone | System | The end card also says "Left alone, N of 20 would have lived": the same seed played again in the background without your help. It starts its disaster when yours starts (your Trigger event press is copied onto the same moment; everything else you do is left out), then runs ahead, never more than a few milliseconds' work a frame |
| The measurement | Tooling | `HandsOffBaselineMeasurements` plays the office as the level defines it for seeds 40 to 49 with nobody at the controls and prints what each round came to and the story of the card. The number the owner tunes the level by |

**Left alone, after this batch: 5.9 of 20 on average, three seeds in ten
clearing** (the owner asked for about a quarter, five of twenty). Tuned on
the owner's word with the hands-off measurement: going for the card now
takes bravery 8 (it was 4) and grabbing it in passing reaches three metres
(it was six); at the old values the office saved 8.6 by itself. About 5.4 is
the floor for these knobs, because the seeds where a member of staff starts
with the card in their pocket nearly always open the door (16 to 20 saved);
the desk seeds now mostly need the player, one in five saving a handful. The
knobs are in `KeycardSettings`; how often the card starts on a desk is the
owner's half-and-half and was left alone.

**What this deliberately left out.** People making way for the holder in a
crush; a leader sending the holder to the door; two cards; a card that burns;
the card-holder being told where the way out is; the scene baker authoring
where the card starts (it authors the card door and the card's default spot).

**Things to watch at the next playtest.**

- Whether "who has the card?" reads at a glance, and whether the yellow card
  is seen at all at the usual zoom.
- Whether a holder fighting nothing and following nobody reads as purposeful
  or as rude.
- Whether the left-alone line lands as the point of the round, or as a
  spoiler.
- Whether six of twenty left alone -- the pocket seeds saving themselves,
  the desk seeds needing the player -- feels like the quarter the owner
  wanted, or too much like a coin toss.
- Whether the player has enough of a lever on a desk card once the panic
  has started: the pull on the card moves the calm, and nobody frightened
  goes for it unless they are the hero.

**Found on the way, and open.** Somebody knocked down where it is crowded
gets up in the nearest clear spot, up to the search reach away
(`PeopleBodies.StandUp`); with the card's new draws, seed 42 had a visitor
stand up 800 mm from where they lay in a single tick, which reads as a hop.
The chair-glide guard in `MeetingRoomEditModeTests` now ignores the tick
somebody gets up on. Worth a shorter reach, or a step rather than a shift,
if it is seen in play.

## Prototype 3: the Director caps the round (2026-09-28)

The owner's rule for the office left alone: about a quarter should live, and
never more than half on any seed, and it should feel like real people on a
bad day rather than a rule. Yesterday's office was a coin toss: a card in a
pocket saved sixteen to twenty, a card on a desk nobody. The owner chose a
reactive Director over fixed rules about who starts with the card and over
curated seeds, with two decisions not to be reopened: it reads only how the
round is going (the crowd and the card), never what the player does, so the
"left alone" line stays honest; and its menu is the building's tricks first
(the tower, the stack, the socket, the fuse box, another bin), measured and
shown, then the crowd's (the alarm rushed early, a cruel person taking the
card, the holder freezing). When asked, the owner chose a wide temper (two
to eight of twenty, differently every seed) and a breather for a massacre.
One batch, one commit, on `feat/prototype-3-gameplay`; it also carries the
owner's report of fallen boxes gliding by themselves.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The allowance | System | Before the round the Director draws how many the building lets out today, two to eight of twenty, from a stream of its own. Some days the office is kind, some days it turns on the crowd after the second person. The allowance is not shown; the end card's "left alone" line is where it is felt |
| On course | System | Every half second the Director reads the round: who is out, who has set out (frightened, on their feet) and could walk to the way out, whether the way out stands open or the card is in a frightened pocket that can reach it. A calm person at a desk is not on course; nor is a queue at a shut card door |
| The push | System | Once the way out is open and more are on course than allowed, the building turns on the crowd, a beat after the Director decides and half a minute to a minute between: a trap still standing with somebody on course in its room comes down without a runner; else the socket in the room with the most of them crackles and pops; once a socket has gone, the fuse box; with nothing burning, another bin. The log says "the building turned on the crowd: 13 were on course, 3 allowed" |
| A massacre | System | With only the allowance's worth or fewer still alive or out, nothing more is added: no socket after the boxes fall, no fuse box after a put-out, no second bin, and a standing trap stays unarmed. The round may then end on its own |
| The pull on the card, frightened | Behaviour | Click the card a few times (about five beside it) and somebody frightened near it goes and pockets it, brave or not, never into the flames, one at a time. Today only calm people did, so the player's one lever on a desk card stopped working the moment the panic started |
| The boxes stay where they fall | Fix | A box from the fallen tower that has been held where it lies stays there. It used to take off across the floor by itself a few seconds after landing (the owner's note): the rule that lets a heaved crate shove the next one along was firing for any box that slid into it |
| Nobody presses at a held box for ever | Behaviour | Now that the fallen boxes hold, somebody strong with free hands who meets one in their way heaves it aside at their next thought, not only once they have been stuck a while; somebody who cannot (too weak, the flames too close, a bottle in their arms) treats a doorway walled with held boxes as no way through and goes round, instead of standing nose first against it until the fire comes (found by the wall-starer test on seed 41) |
| Fifty seeds | Tooling | `HandsOffBaselineMeasurements` plays fifty seeds left alone and fails on any seed that saves more than half, naming it; it prints each round's allowance and every push. Four and a half minutes; run on purpose before the commit of any batch that touches the Director, the card or the traps |

**What the measurement showed.** In full in the [decisions](technical-decisions.md#prototype-3-the-director-caps-the-round-2026-09-28).
The building half, at its honest best, leaves the office at **4.5 of 20 on
average over fifty seeds (23%), eight seeds clearing and ten saving more
than half**, and the fetch knobs made no difference to that either way. Two
things the table taught on the way: the gliding boxes were letting people
through the fallen tower, so fixing them alone brought yesterday's 5.9 to
4.4; and a Director that pushes while the holder is still walking the card
to the door kills the holder and everybody behind them (1.6 of 20), so it
now waits for the door to open. The building's tricks can trim a round in
which the door is open and the crowd is still inside; they cannot stop a
crowd already past them, and they cannot put fire in the crossbar. The
crowd's tricks, which control the door itself, are where "never more than
half" becomes a rule.

**What this deliberately left out.** The crowd half of the menu (the next
batch); any tuning of the tower's heap now that it holds; the allowance shown
to the player; a Director that eases before a massacre; the scene baker
authoring the cap.

**Things to watch at the next playtest.**

- Whether a socket crackling right after the door opens reads as the
  building turning on the crowd, or as bad luck.
- Whether the fallen tower, now that it really holds, is too much: only the
  strong get through it, and the holder of a pocket card reaches the door
  in about one round in three.
- Whether five clicks on the card is the right price for turning a
  frightened person back for it.
- Whether the allowance's swing (a kind day, a cruel day) is felt at all
  from the end card.

**Found on the way, and open.** The heap forms and clears within a few
seconds in nearly every run (the strong heave a box out), yet the corridor
stays cut on the map by the boxes lying beside it; whether that is the trap
the owner wants or a wall is a question for play. And a round that cannot
end: on seed 83, left alone, the fire went out with nobody dead and nobody
out, and twenty frightened people kept the stall clock going for four
minutes; the measurement calls it as the end card would, but a player
would sit through it. The physics engine's thread flip was seen once
today on seed 42 with the way out opened (one run of four), as it was
yesterday on seed 41.

## Prototype 3: the hand (2026-09-29)

The owner stepped back: "Simulation runs fine, and is fun, but the gameplay
is very vague and hard to gauge. We need a good gameplay loop." Four rounds
of questions settled the shape. Not band-aids on their own (a status line, a
lower bar); not cards as the foundation ("I still like the concept of
influence. And main loop should still be around frantically saving the
people"); twenty people stay ("it's already frantic now ... when we get
gameplay working we will scale up with bigger level AND more agents"). The
frame: **one hand.** The building throws problems at the crowd, in the open;
the player answers them one at a time with a hand, pulling people toward a
place or holding one person back; every person weighs it by character; and
the round is judged against the same seed left alone. One batch, one commit,
on `feat/prototype-3-gameplay`.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The hand on a place | System | Influence is a hold, not clicks (the owner: "hold only ... it also makes all decisions a priority. You can't be everywhere at once. When you interact the influence is clear and instant, but as soon as you let go the agents are on their own"). Press the left button on a door, a thing or the floor and the pull is full at once; let go and it is gone at once; one place at a time. It reaches about a room's length as a walk, through an open doorway but never a wall or a shut door (the owner: "through open doors, but limit range to be around a room's length"). A place used for you goes on gathering people but is not used again until pressed afresh. The easily led get up for it within a couple of seconds |
| The tug: a hand on a person | Behaviour | Hold the button on somebody and you have them by the shirt (the owner: "it holds them in place. Should not be 100% instant, more like tugging someone's shirt. So you can save someone running into fire"). They slow to a stop over about a second and stay while you hold, and are off again the moment you let go. From strength 6 they tear free -- eight seconds at 6, half as long for each point above, so the brute is gone in a moment -- visibly, with a shake ("the strongest can break free. Sliding scale. A slightly not too strong can eventually break free by visibly shaking you off"). Nobody alight or down can be held. The one stated exception to "suggest, never command" |
| Moves that finish | Behaviour | Somebody brave who took the bottle for you keeps it when the fright comes and goes at the fire with it, instead of flinging it away. The hand on the pull station takes two points less bravery and reaches as far as the pull. A held card is gone for by the frightened after two seconds of holding |
| Left is the crowd, right is the building | Controls | The hand holding a door shut moves to the right button held; a right click is still the key. The right-button camera drag is gone and Q and E step in eighths (the owner: "remove the camera control. Only use the q, e, but add double the amount of steps it snaps to"). A click on a person is still a poke |
| The building plays in the open | System | Every trap creaks for about three seconds before it falls: the stack sways, the creak is heard in its room so calm people look up, a sign says "it's going!", and you have those seconds to get people clear -- with a hand on the floor away from it; the tower itself cannot be touched (the owner's choice). When the Director turns on the crowd a banner says so across the top, like the alarm |
| Nobody runs at a crate wall | Behaviour | Found on the way, by the wall-starer test on seed 41 once the stack creaked: cut off in the stockroom's west lane, people picked spots on the far side of the crate walls and ran at the crates for the rest of the round. A frightened person with no door now picks only a spot they could walk to, and somebody creeping against a crate they cannot heave counts as blocked after a quarter of a second and chooses again |
| Beat the building | Presentation | The round is judged against the same seed left alone (the owner's choice). The strip reads "Left alone: 4 would live" once the background round has its answer; the end card says "You made the difference for 5" in green (or the reverse in red), keeps the 75% bar as a distant target, remembers the best margin, and gives three or four lines of why: your hand, the keycard, the corridor, the fire |

**What the measurement showed.** The office left alone, fifty seeds
(`HandsOffBaselineMeasurements`), before and after the creak:

| Rules | Saved, average of 20 | Seeds over half | Seeds clearing 75% |
| --- | --- | --- | --- |
| No creak (the batch before, 2026-09-28) | 4.5 (23%) | 10 | 8 |
| Creak heard but no delay (a variant, measured once) | 3.4 (17%) | 9 | 6 |
| Creak heard and three seconds | 2.7 (13%) | 4 | 2 |
| The same, with the two flight rules (shipped) | 3.0 (15%) | 6 | 4 |

The creak costs lives with nobody playing: the crash sound turns calm heads
toward the archway, and the delay lets more of the corridor's crowd reach
the archway before the boxes land on it; the flight rules (a reachable
escape spot, creeping counted as blocked) give a little back. The owner's
standing rule is about a quarter left alone and never more than half on any
seed; the shipped numbers are under the quarter and still break the half on
six seeds, so the fifty-seed check stays red, as it was before this batch.
Nothing was tuned to compensate: which way to move it (a shorter creak, a
quieter creak, or the Director's allowance) is the owner's call, and the
hand is what this batch is for.

**What this deliberately left out.** The purse and the cards as a
foundation; more people or a bigger level (after the loop works); a second
way out; a level rebuilt as stages; the full written retelling (the four
lines are its smallest form); sound (the creak and the banner are drawn and
signed; when audio arrives they are heard); a pull felt more than one doorway
away; steadying or toppling the tower by hand. The Director still reads only
the crowd, never the player.

**Things to watch at the next playtest.**

- Whether one hand feels like a choice or like a bottleneck: two things going
  wrong in two rooms is the moment it will show.
- Whether the tug reads as a hand on a shirt or as a freeze, and whether the
  brute tearing free in a moment is a delight or a cheat.
- Whether three seconds of creak is enough warning, and whether anybody
  notices a creak on the far side of the building.
- Whether the margin line is the number the owner looks at first, and whether
  the four lines of why say enough to change the next round.
- Whether the eight camera steps lose anything the free swing gave.

## Prototype 3: the hand, second pass (2026-09-30)

The owner played the hand on seeds 42 and 3: "This last commit was a HUGE step
forward. I actually had glimpses of gameplay. But far from tweaked." The
notes: influence barely felt ("holding next to a group barely made them come
closer. Must be much more noticeable"); holding is what you do, so a single
click should leave a beacon for three seconds; a tug must stop people quicker,
and they should visibly try to shake it off by personality; the keycard could
not be clicked; a hold on the fallen boxes moved none of them; a hold on the
exit gathered a crowd that did not try to break it down; people acted upon
should do what they normally would not ("a cowardly agent should pick up fire
extinguisher, an agent with low strength will bash on door"); in sum, "agents
doesn't SHOW the influence in behavior very well"; and seeds 42 and 3 saved
nobody left alone. New: the right button an anti-influence. Changed: the
tower should fall next to the first person running past, not in the corridor.
Asked, the owner chose: most answer and **strong wills refuse**, "but against
their will should be often. Again -- we need clear influence"; the right
button is **pure push-away**; the card door is **battered but holds**, and
the hand there sends for the card; the tower falls **where the runner was**;
and **left alone stays as it is**. One batch, one commit, on
`feat/prototype-3-gameplay`.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Everybody answers | Behaviour | Hold beside a group and they come: within a second or two, the nervous first and the steady last, one after another -- from their desks, off their errands, from standing about. The pull is full within six metres. The strongest wills refuse it: on the office, the host and the bully. Somebody startled turns to the hand; somebody frightened swings to it (a close hand is worth twenty metres of walk, where it was six) |
| A click is a beacon | Controls | A quick click leaves the hand where it was for three seconds, its ring throbbing, and then it comes off by itself. Holding is as before |
| Push away | System | The right button, held or clicked, pushes people away from a door, a thing, a person or the floor: the calm walk off out of it, anybody sitting gets up to, the frightened steer away, and a door pushed from is no way out to anybody who feels it. A cool blue ring, and blue lines running away. The key and the hand holding a door shut are gone from the mouse (the owner's choice) |
| Against their nature | Behaviour | Whoever feels the hand strongly does what it asks, whoever they are: the coward drawn to the bottle takes it and sprays the fire; the weak drawn to a locked door throw themselves at it, keep at it, and a few together break it; a crate too heavy for anybody to carry is heaved aside by whoever comes, after a few seconds of straining if they are weak; anybody pulls the alarm; anybody goes back for the card |
| The card door holds | Behaviour | Hold the way out and the crowd pounds on it, and it never gives (the owner's rule); after a second somebody who knows where the card lies goes back for it |
| You can see it | Presentation | A gold hand bobs over anybody doing what the hand asked, and their line is thick and bright; somebody doing it against their nature trembles, and a gold "for you..." sign goes up as they start. The end card counts the times somebody did for you what they never would have |
| The tug bites | Behaviour | A runner is stopped in about a quarter of a second (it was a second), and struggles all the while you hold them, their own way: the frightened hardest, the nervous flailing fast and small, the strong heaving slow and big. Tearing free is as before |
| The keycard clicks | Controls | The card on a desk is aimed at where it is drawn, a small thing may be a little further off the pointer, and a thing nearer the pointer than a person wins the click. Somebody calm sent for it walks round the desk to it |
| The tower falls on the runner | System | The tower comes down when a frightened person runs past it -- within three and a half metres, in the crossbar -- and falls, after its creak, in a heap on the spot where they stood when it began to creak. The runner is usually clear; whoever follows them runs into it. The archway still shuts if three boxes happen to land in it |

**Left alone, measured after this batch** (`HandsOffBaselineMeasurements`,
fifty seeds, nothing tuned, the owner's choice): **3.9 of 20 on average
(19%)**, seven seeds clearing the 75% bar and nine saving more than half
(44, 58, 59, 60, 65, 68, 76, 77, 80). The batch before was 3.0 of 20, four
clearing, six over half. Most of the rise is likely the tower: it no longer
falls across the corridor, so the corridor's crowd is no longer walled in
by it. The fifty-seed check stays red, as it was. Seed 42 left alone saves 5
of 20 here; the owner saw none on seed 42, most likely because the
measurement lets the fire start at the Director's own time while in play
Trigger event was pressed early (the left-alone round starts its fire when
the player's does). Seed 3 is outside the measured range.

**What this deliberately left out.** Tuning left alone (the owner's choice);
a floor under it; the key anywhere on the controls; a hand that steadies or
topples the tower; a hand on the tower's standing boxes; people who refuse by
anything but leadership and evil; the stockroom's stack falling on its
runner (it falls across its lane as before); the hand reaching more than one
doorway.

**Things to watch at the next playtest.**

- Whether the hand now reads as clear, or as a puppet string: everybody but
  two coming within a second or two may be too much.
- Whether a click's three seconds is a useful tap, or too short to matter.
- Whether the push reads as the hand's opposite, and whether a door nobody
  will take while pushed is too absolute.
- Whether the coward at the fire and the weak at the door are seen as
  against their nature (the tremble, the sign), or just as people doing
  things.
- Whether the tower falling on a runner in the crossbar is a trap or a
  nuisance, now that it rarely cuts the corridor.
- Whether the struggle under the tug reads by personality.

## Prototype 3: the hand, third pass (2026-09-30)

The owner played the second pass and asked for more: "Even more influence
over agents actions. When panicked, the agents still run around too much";
a debug slider for "general attraction" with a number, "so I can find the
sweetspot and later hardcode it"; people switching to "that specific task,
like clearing boxes for a path, or opening a door"; and the hand following
the pointer while held, "so agents can be guided with this. Same with right
click hold". And: "Go over the logic thoroughly with the influence system in
mind. Find bugs, bad pathing, logic or flow. I want more clear influence,
without losing the aspect of the agents character traits." Reading the code
found why the frightened ran about: to them a hand on the floor was a
compass, never a place. It tilted which door they chose, or which random
spot in their room they sprinted to, and on arriving they chose another.
No questions went to the owner; every default is in the
[decisions](technical-decisions.md#prototype-3-the-hand-third-pass-2026-09-30).
One batch, one commit, on `feat/prototype-3-gameplay`.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The frightened come to the hand | Behaviour | Hold on the floor during the fire and the frightened who feel it come to it, the nervous first, and stand in a loose ring round it facing it, instead of sprinting past to another spot. The hand beats a leader's call, swerving and following other runners. Flames close by still send them off, and nobody is pulled back from the way out. Character stays: somebody who feels it less than fully (a leader) breaks away after a few seconds; the nervous stay; the host and the bully never come. A push sends them walking away from it. Let go and they are on their own at once |
| Drag to guide | Controls | Keep the button down and move the pointer: the hand slides with it and the people answering it follow, through doorways too. A right-button drag herds people ahead of it. Pressed on a door or a thing, the hand stays there until the pointer is clearly off it |
| The hand clears the boxes | Behaviour | A hand on the fallen boxes, on the floor beside them, or on the archway they are heaped across: whoever comes heaves the nearest box out of the way, away from the hand, then the next, until none is left -- the weak after straining, several helpers spread over the heap. It used to be one box a press, and the box could go deeper into the heap. Drag along a heap and they clear a path behind the pointer |
| Hand strength | Tooling | Tab panel: "Hand strength 100%   answering now: 3" over a slider from 0 to 300 %. It scales how strongly everybody feels the hand -- the one reading everything the hand does starts from -- so turning it up brings more people, from further off, sooner. It lasts through Reset and is back to 100 at every Play |
| The calm, clearer | Behaviour | People answering the hand stand on spots of their own round it (they used to crowd its very spot), walk straight to it, take it in a beat late like every reaction, and come with a bag in hand or a meeting still to come (both used to keep them away). A door is opened or shut as the hand asked at the press -- somebody walks right up to it, on the side they come from, and clears a wedge first whoever they are. A crate, a door or a thing somebody set off for is finished even after a click's three seconds are up |
| Fixes found on the way | Fixes | Somebody in a doorway now feels the hand; the startled edge to it along the way there rather than into a wall; a hand on a door makes the frightened rethink at once; a push that changes somebody's way out is logged as a push; somebody seated with an errand no longer sits straight back down after getting up for the hand; a noise no longer leaves a calm person forever "on the way" to fetch something |

**Left alone, measured after this batch** (`HandsOffBaselineMeasurements`,
fifty seeds, nothing tuned, the owner's rule): **4.2 of 20 on average
(21%)**, nine seeds clearing the 75% bar and eleven saving more than half
(44, 48, 53, 58, 60, 65, 68, 76, 77, 80, 89). The batch before was 3.9, seven
and nine. Nobody plays the hand in that round, so the change can only come
from the one fix that works without it: a noise no longer leaves a calm
person forever "on the way" to fetch something, so those people tidy, sit
and move about again as they used to. The fifty-seed check stays red, as it
was.

**What this deliberately left out.** More than one hand at a time; a hand
on the floor beside a shut door opening that door (only a hand on the door
does, so a hand in a corridor does not set people opening every door along
it); somebody with a bag putting it down to do a job for the hand (they come
with it, and do nothing with it); retuning left alone (the owner's rule);
the stockroom stack falling on its runner. The slider's sweet spot is for
the owner to find; the value found becomes the new default.

**Things to watch at the next playtest.**

- Where the slider ends up. Tell us the number; it becomes the level's own.
- Whether the frightened standing in a ring round the hand read as
  following it, or as a queue that has forgotten the fire. Whether leaders
  breaking away after a few seconds reads as character or as the hand
  failing.
- Whether dragging reads as leading, and whether ten moves a second is
  smooth enough.
- Whether clearing a heap box by box is quick enough to save the corridor,
  and whether people clearing boxes the player did not mean (a hand on the
  floor near fallen boxes clears them) is a surprise.
- Whether finishing a job after a click reads as "they heard you", or as the
  hand not letting go.

## Prototype 3: tells, and the hand's tally (2026-09-30)

After the third pass of the hand the owner asked whether this is a foundation
for fun. The answer given was that the fun of a game like this is spinning
plates -- many small crises at once, each one saved by a quick click if you
see it coming -- and that people had nothing like the tower's creak: every
dangerous thing they did, they decided and did on the same tick. The owner
asked for "the visible agent tells", kept "the influence strength slider"
(built with the third pass), and asked for "the stat at the end about how many
clicks/influence you used this round". No questions went to the owner; every
default is in the [decisions](technical-decisions.md#prototype-3-tells-and-the-hands-tally-2026-09-30).
One batch, one commit, on `feat/prototype-3-gameplay`.

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Going stiff | Behaviour | Somebody about to freeze shivers harder and harder for about a second, a red ring shrinking at their feet, "going stiff...". One poke, a tug or the hand before the ring closes: they run instead. Missed: frozen, as ever, and three pokes to wake |
| Gathering nerve | Behaviour | Somebody about to dash for a door through the heat stands facing it, bouncing on their toes, "here goes...". Caught: the door is given up for a while, and they hide or go another way |
| Turning back | Behaviour | Somebody about to head back toward the flames -- the card, a pull station, somebody down, the fire with a bottle -- looks back over their shoulder, "I have to go back!". Caught: they stay out of the flames for about ten seconds. Only when the walk passes near the flames, and never for somebody your hand sent |
| The tally | Presentation | The end card's hand line counts actions and actions a minute; presses (clicks, pushes), pokes, tugs; seconds held; metres dragged; the hand strength if the slider moved; and what came of it: times somebody answered, times somebody did what they never would, tells caught of tells begun |

**Left alone, measured after this batch** (`HandsOffBaselineMeasurements`,
fifty seeds, nothing tuned, the owner's rule): **4.5 of 20 on average
(22%)**, seven seeds clearing the 75% bar and seven saving more than half
(42, 43, 55, 56, 71, 76, 83); the batch before was 4.2, nine and eleven.
Nobody catches a tell in that round, so every freeze, dash and walk back
toward the flames simply comes a second later, and people off to one side of
the way out now count as out. The fifty-seed check stays red, as it was.

**Found on the way.** People carried out of the way out on their backs, or
walking out at a slant after a leader, could end up just outside the
building, off to one side of the door, never counted as out, wandering along
the outside wall. Anybody clear of the wall beside the way out now counts as
out. And a dash that had just gathered its nerve could shut its own door
against the fire on the tick in between; the dash now begins the moment the
wind-up runs out.

**What this deliberately left out.** Tells for anything else (a trip, a
bolt away from the flames, a calm person walking toward a noise); a tell for
the calm half of the day; a live counter on screen during the round (the
tally is on the end card only); the lazy-player measurement offered in the
design talk.

**Things to watch at the next playtest.**

- Whether a second is enough to see a ring and reach it with the pointer,
  and whether three rings at once across the office is frantic in the good
  way.
- Whether going stiff is now too easy to cancel, so nobody freezes, or
  still rare enough to matter.
- Whether caught people read as saved or as confused (a dash called off
  leaves somebody by the hot door).
- The tally: the actions a minute of a round that felt frantic against one
  that felt calm.
- And the slider's value: the number that felt right, to keep as the level's
  own.

## Prototype 3: the hand's fourth pass (2026-09-30)

The owner played the tells build and wrote: "Agents still feel like they
don't really listen. Hard to describe, but you should feel the influence and
see them guided, but still see their personality in the actions. Think
magnets and fish/bird clusters. Right now it's hard to control them at all.
Sometimes no reaction at all, sometimes smallish influence but often lose
attention fast. If getting them to notice or sway their focus, the focus
should mostly stay." And: influence should cost from a bar that refills by
itself; "I ran a seed 42 and got most of the agents to the final corridor,
but even though I only influenced the exit, none survived"; the top of the
screen cut to saved, lost, still inside and the seed; the cards removed; the
scene renamed; and "don't be afraid to refactor logic and systems. No
band-aid solutions" -- for the whole code. Two measurements were taken first
(seed 42 with the fire at twenty seconds reproduced the owner's round: the
host, who refuses the hand, burned with the keycard in his pocket behind the
fallen tower while four people pounded a door that could not give; on seed 41
the hand on the way out turned five saved into none, because the crowd it
gathered blocked the one person fetching the card). Two designs were made
independently and merged; the owner decided the card door gives, and that the
wider refactor -- one way of deciding what to do for calm and frightened
people alike -- is the next stone. One batch, one commit, on
`feat/prototype-3-gameplay`; the details are in the
[decisions](technical-decisions.md#prototype-3-the-hands-fourth-pass-2026-09-30).

| Stone | Kind | What the player sees |
| --- | --- | --- |
| The focus stays | Behaviour | Everybody carries the hand's ask as a goal of their own, with a *conviction* that builds every moment they feel the hand -- fast beside it, slowly at the edge of its reach, faster the more easily led they are. They set about the task once it passes a low line (about half a second beside the hand); let go, and anybody past a higher line (two seconds beside it) keeps the task -- clearing the heap, opening the door, pounding, standing where you pointed -- and drifts off it only as that conviction fades on their own beat, leaders first, the nervous last. Nobody breaks away while the hand is on them. No more coin flips: the chance rolls, the break-away roll and the "never again for this press" marks are gone. What ends a goal is one rule for everybody: flames close by, a knock-down, catching fire, your tug, a felt push, or a fresh press they feel, which replaces the goal and keeps their attention. Somebody who gave up (no way there, the door would not shut, hemmed in too long) is a little less sure and tries again after a beat |
| The hand's bar | System | A bar at the bottom left drains while your hand is on a place, a click's beacon or a person, and refills by itself all the time: a full bar is about a minute of holding, and refills in a minute. Run dry, the hand comes off by itself, "your hand gave out" goes in the story, and the bar shows *resting* for a few seconds before it takes another press. Nobody at the controls, it never moves |
| The card door gives | Behaviour | **The owner's decision**, replacing this morning's "it never gives": held under the hand, the crowd's pounding wears the way out down -- three or more people about forty seconds, two a minute, one two minutes -- and it visibly weakens before it bursts. Without the hand nobody pounds it, so it never gives. And a fetcher your hand sent for the card keeps at it five seconds when hemmed in instead of one, gets twice the time, and hands the claim on if they give up |
| Who has the card | Presentation | A small yellow card floats over whoever has the keycard (hidden with the other marks by the Tab panel); the way out's hover line says who has it, or that it lies free |
| The top strip | Presentation | One strip: `Saved 3   Lost 2   Still inside 15      Seed 42`, bold, readable at a glance. The tick, the fire, the calm and scared counts, "Need 15 of 20" and "Left alone" are gone from it; the last two are in the Tab panel's stats and on the end card. The hover line -- what a press under the pointer does -- sits at the bottom left, beside the bar |
| No cards | Presentation | No card bar, no aim circle, no "No cards left" line; the dead deal nothing. The card machinery stays in the code for a later level |
| The scene's name | Tooling | `prototype_fire_1_fl_small`: a fire, one floor, small. The start card says "Prototype fire 1 (one floor, small)" |

**Left alone, measured after this batch** (`HandsOffBaselineMeasurements`,
fifty seeds, nothing tuned, the owner's rule): **3.2 of 20 on average (16%)**, three seeds clearing the 75% bar and five saving more than half (58, 62, 69, 80, 88); the batch before was 4.5, seven and seven. Nobody plays the hand in that round, so the change comes from the walls (a harder doorway crush; the heap now holds, where for a few minutes of this batch's building it shoved itself clear) and nothing else here. The fifty-seed check stays red, as it was.

**Seeds 41 and 42 with the hand on the way out, after this batch**
(`HandOnTheWayOutMeasurements`): seed 41 (fire at 6 s): 0 left alone, **13 with the hand on the way out** (it was 0 and 0); seed 42 at 40 s: 5 left alone, **19 with the hand** (it was 0); at 6 s, 6 and 4; at 20 s (the owner's round), 4 and 4 -- the host still refuses the hand and still burns with the card behind the fallen tower, and the hand on the door cannot change that; at 60 s, 8 and 7. The exit hand is no longer a death trap; whether it helps now depends on whether the card can reach the door.

**What this deliberately left out.** Somebody mid-chat leaving for the hand
(their partner would be left waiting); a hand that reaches the host (he
refuses, as the owner chose); a crowd at the door that makes way for the
fetcher; a live counter of the bar's use on the end card; and the wider
refactor, recorded below under *Agreed direction* as the next stone.

**Where the two-system split forced a double wiring in this batch** (the
brief for the next stone): the hand's goal is answered once in
`CalmBehaviour` (leaving a chair or an errand, wandering to it, using the
thing) and once in the panic options (`HandGatherBehaviour`, `HandHeaveBehaviour`,
`KeycardSystem`, `ExtinguisherBehaviour`, `AlarmBehaviour`); a task begun calm
is cut short and re-answered when fright comes (`FearSystem` calls
`Interrupted`), and the other way round; the door errand (`ErrandBehaviour`)
and the frightened door choice (`DoorBehaviour`) each read the goal
separately; and "done" is called from six places.

**Things to watch at the next playtest.**

- Whether people now read as guided and still themselves: the nervous
  first and longest, leaders late and briefly. Whether a kept goal after you
  let go reads as "they heard me" or as puppetry.
- Whether a minute of hand is the right bar: it should run dry about once
  in a frantic round, and the three seconds' rest should sting, not stall.
- Whether the card door giving in forty seconds under the hand is a fair
  last resort or too easy; whether the yellow card over the holder's head
  changes what you do first.
- Whether the strip at the top is what you glance at, and whether anything
  that left it is missed.

## Prototype 3: walls as thick to the feet as to the eye (2026-09-30)

A repair found on the way to the hand's fourth pass, from the owner's note
"objects (like chairs) often clip inside walls". The world was never at fault:
the physics engine's walls were 4 cm thick and the drawn ones 40 cm, so a chair
shoved up against a real wall was drawn sunk a hand's depth into the picture of
it. Now every wall is 20 cm thick to the eye, to the physics and to the map
people steer by, and a thing against a wall touches its face. One commit of its
own; the decision is in
[technical decisions](technical-decisions.md#prototype-3-walls-as-thick-to-the-feet-as-to-the-eye-2026-09-30).

## Prototype 3: test levels and honest reach (2026-10-02)

The first two sketches under the sketch-and-keep agreement, played by the
owner and kept ("I can navigate the maze fine, so that is kind of fun"),
hardened as one commit on `feat/prototype-3-gameplay`. The first (2026-10-01)
answered the owner's ask for "blank levels to test panicked crowds ... large
square room with walls, maze to test following, interaction test level";
the second (2026-10-02) answered three playtest notes: the lines showed far
more than the hand's reach, the hand leaked round door frames in the office
(fine in the open test levels), and the keycard was hard to see. The
details are in the
[decisions](technical-decisions.md#prototype-3-test-levels-and-honest-reach-2026-10-02).

| Stone | Kind | What the player sees |
| --- | --- | --- |
| Three test levels | Tooling | A row on the start card: the office, the square room (one 24 m room, a door in every wall, forty people the seed invents), the maze (4 m cells, archways, one way out, one member of staff and ten visitors) and the interaction room (one of everything to bump, carry, sit on, open or set alight). Each borrows the office's tuning and swaps the building. Reset and "play again" keep the level |
| The Crowd button | Tooling | On a test level, beside Trigger event: *Crowd: panicked* startles everybody, each on a tick of their own, and holds them frightened; *Crowd: calm* settles them one by one and silences the bells. It begins the round, so an emptied level ends with a score, and Trigger event still lights a fire in a round the switch began. No "left alone" line on these levels: the switch is never copied into the background round |
| Hand by sight | Behaviour | Through an open doorway the hand is felt only where it can be seen: in full straight through the gap, fading to nothing as the line misses the frame by three quarters of a metre. A hand against the wall beside an open door is hidden by that wall, as by any wall. Somebody who took the hand in and walked out of view keeps at it, as before |
| Honest lines | Presentation | A gold line runs only to somebody who feels the hand right now, and the thick line of somebody doing what it asked is as bright as they feel it. Somebody keeping at an earlier ask from out of reach has the gold hand over their head and no line |
| The keycard on the hip | Presentation | The card is worn on its holder's right hip, drawn 1.7 times its size, and rides on the body through a lean, a seat and a fall. The small mark over the head is gone: it read as a second card |

**What this deliberately left out.** People who took up the call keeping at
it after leaving view (kept on purpose; the owner may yet call that "through
walls" too); a Crowd button on the office; a "left alone" line on a level
with the switch; any test of how the test levels are drawn, beyond that they
are valid buildings that run.

**Things to watch at the next playtest.**

- Whether three quarters of a metre of soft edge reads as a gradient or as a
  leak; 0 is a hard cut.
- Whether the lines, now honest, are enough to say who is answering, or
  whether the gold hand alone is too quiet.
- Whether the card on the hip is seen at the usual zoom.

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

### Next: one way of deciding, for calm and frightened people alike

Decided with the owner on 2026-09-30, after the hand's fourth pass. Today a
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
  batch above and [the cue system](cue-system.md)); its second (2026-09-28)
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
