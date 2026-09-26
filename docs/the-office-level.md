# The office level

**Status:** the game's one level, and the scene every stone so far has been
built in: prototype 1 (the fire-reaction office, finished and merged into
`main` on 2026-09-22), prototype 2 (the round) and prototype 3 (gameplay, under
way). This page says how the level plays *today*; how it got here is in the
[prototype roadmap](roadmap.md) and its history. The scene is still called
`FireReactionPrototype` and the scenario `fire-reaction-prototype`; only this
page was renamed (it was `fire-reaction-prototype.md`).

The building, the cast, the doors and the furniture below were checked against
the code on 2026-09-26. The behaviour rules have been kept up to date stone by
stone and were not re-audited then. Where this page and the code disagree, the
code is right and this page is the bug.

## Experience

The `FireReactionPrototype` scene shows one office floor. A 3 m wide corridor
runs the length of the building. Along its north side sit the **meeting room**
and the **cafeteria**; along its south side the **open-plan office** (12 × 12 m,
unchanged since prototype 1) and the **bathroom**, whose three stalls are each
a little room with its own door. At the east end the corridor meets a
crossbar, making a T: the building's **one way out** is at the top of the
T's north arm, and it starts locked. The south arm runs down past the bathroom
to the **stockroom**, a 10 × 5.5 m room full of cardboard boxes with a lane
through them, which also has a door into the office's east wall. So from the
office there are two ways to the way out: along the corridor, or through the
stockroom. The **storage closet** hangs off the office's east wall, and the
**maintenance room**, with the fuse box, is at the far west end, past
everything. Every room but the bathroom has two ways out (the owner's rule,
2026-09-25).

Twenty people (capsules) work there or are visiting: eight in the office, six
in a meeting, four in the cafeteria and two in the bathroom. The office holds
three wooden desks with a chair and a laptop each, cardboard boxes against the
walls with some stacked in pairs, waste bins, potted plants, a microwave, wall
sockets and a fire extinguisher. Since prototype 3 a **tower of boxes**, two
stacks four high, stands in the corner where the corridor meets the T; see the
[roadmap](roadmap.md) for the trap it is part of. The building has **one
fire-alarm pull station**, at the far west end of the corridor, and alarm bells
high on the walls.

The meeting is a client visit already under way: six people sit round one long
table, five of them visitors who came up in the lift and do not know the way
out, and their host, the strongest leader in the building. Since prototype 3
the fire always starts somewhere in the meeting room.

**It is one floor of a tower.** The floor plan is not a plan floating in the
dark: it sits on a concrete slab that overhangs the outside walls by about half
a metre, and below that the building carries on down into a band of dark
windows with pale uprights between them, a spandrel, and the lip of the storey
below, before it all goes dark. That is drawing only — nothing about it is in
the simulation, and nobody can walk on it.

**Everyone has a personality.** Each person has seven traits from 0 to 10:
strength, speed, bravery, compassion, evil, nervousness and leadership. 5 is an
ordinary person. A number floats beside each head; press **Tab** for a table of
everyone's traits, how they will panic, and what they are doing now. The people
are authored as a cast (a scenario can also leave traits out and let the seed
draw them), in `PrototypeBuilding.DefaultAgents`:

| # | Where | Who | Str | Spd | Brv | Cmp | Evl | Nrv | Ldr |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | office | ordinary | 5 | 5 | 5 | 5 | 2 | 5 | 4 |
| 2 | office | the brute (briefcase) | 9 | 6 | 6 | 3 | 6 | 3 | 3 |
| 3 | office | the hero | 8 | 6 | 8 | 8 | 1 | 3 | 8 |
| 4 | office | the saint (bag) | 4 | 4 | 7 | 9 | 0 | 4 | 5 |
| 5 | office | the villain | 6 | 6 | 5 | 1 | 8 | 4 | 6 |
| 6 | office | the nervous wreck | 3 | 5 | 1 | 5 | 2 | 10 | 1 |
| 7 | office | the sprinter | 5 | 10 | 5 | 5 | 3 | 6 | 4 |
| 8 | office | the bully | 7 | 5 | 4 | 2 | 9 | 5 | 5 |
| 9 | meeting, visitor | ordinary | 5 | 5 | 6 | 5 | 3 | 4 | 4 |
| 10 | meeting, visitor | the strong one | 9 | 4 | 7 | 6 | 2 | 3 | 5 |
| 11 | meeting, visitor | the worrier (bag) | 4 | 7 | 3 | 7 | 1 | 7 | 2 |
| 12 | meeting, visitor | the timid carer (briefcase) | 3 | 6 | 2 | 8 | 0 | 9 | 1 |
| 13 | meeting, visitor | the chancer | 7 | 8 | 8 | 4 | 7 | 2 | 6 |
| 14 | meeting | the other hero, the host | 6 | 5 | 7 | 9 | 1 | 3 | 9 |
| 15 | cafeteria, seated | ordinary | 5 | 5 | 4 | 5 | 4 | 6 | 4 |
| 16 | cafeteria, seated | the other bully | 8 | 6 | 6 | 2 | 8 | 4 | 6 |
| 17 | cafeteria | the runner | 4 | 9 | 5 | 6 | 2 | 6 | 3 |
| 18 | cafeteria | the coward | 3 | 4 | 2 | 4 | 3 | 8 | 2 |
| 19 | bathroom | ordinary | 5 | 5 | 5 | 6 | 3 | 5 | 5 |
| 20 | bathroom | ordinary | 6 | 7 | 6 | 4 | 5 | 4 | 6 |

The eight in the office each have a desk chair that is theirs, and the two
seated in the cafeteria have theirs; over the day people drift back to them.
The visitors and their host have no home on this floor.

What the traits do:
- **Speed** sets walking pace (1.0–1.6 m/s) and sprinting pace (3–5.5 m/s).
- **Strength** makes a person shove boxes harder, and someone 4 or more points
  stronger than the person they collide with only staggers where the other is
  floored.
- **Bravery** shortens the pause before reacting and lets a runner pass closer
  to the fire before bolting away from it. Fearful people (nervous and not
  brave) are the ones who freeze.
- **Nervousness** makes a runner shout more often, change their mind more
  often, zig-zag, hesitate and trip more.
- **Compassion** makes a runner weave around people and dodge rather than ram
  them; **evil** does the opposite and barges straight through. The cruel also
  take hold of whoever is in the way and heave them aside, slam and lock doors
  behind them, and wedge doors shut to keep other people out.
- **Leadership** makes somebody take charge: sending the strong at a door,
  sending the brave for an extinguisher, and calling people on.

**Before the fire, people loiter.** Each person makes their own small decisions
every few seconds. They stroll to a spot on a gently curving path, stop, glance
around, or wander over to stand about a metre from someone as if chatting.
They turn and speed up gradually, keep personal space, and steer away from
walls before reaching them. Each person has their own seeded walking pace
(set by their speed trait) and turning speed, so no two move alike.

**After five seconds a fire starts** at a seeded spot (since prototype 3,
2026-09-25, always somewhere in the meeting room; in a played round, when the
player presses Trigger event). The
floor is a grid of 0.5 m squares. Each burning square shows a dim glowing tile
and a cluster of two or three small cubes that bob, spin, flicker and cycle
between red, orange and yellow. New squares pop in with a small overshoot, and
old squares slowly fade to deep-red embers. The fire only spreads from squares
that are already burning, into an irregular blob, and fills the room in about
40–45 seconds.

**People notice things.** A big red `!` pops up over anyone who notices
something. Someone who sees the fire stops, turns to face it and yells: three
sound-wave arcs appear beside their head, and a faint ring spreads across the
floor showing how far the yell carries. Calm people within 6 m understand
the yell and are alarmed; calm people up to 12 m away only hear it and turn to
look, with a yellow `?`. A shut door halves both. Fire crackles too: someone
within 3.5 m of one burning square with their back to it turns around, and a
fire is heard further as it grows (50 mm more a square, up to 15 m; half that
through a wall or a shut door). If it is still out of sight they edge toward
it until they see it -- and if it is in another room, they go and look: to the
door, open it, and there it is (the go-and-look cue, since 2026-09-24). Fear
spreads by sight as well: anyone who sees a frightened person leap up or run,
within 8 m and along a line of sight, is startled by that alone; the brave
look up first. Since 2026-09-24 the first shout comes with the first stride
rather than two to five seconds in, and people see 12 m rather than 3.

**People panic in different ways.** Panics are dealt like a deck across the
whole building rather than rolled one by one, and the most fearful (nervous
and not brave) get the freezing ones first. Of the twenty:
- eleven are **runners**, who sprint at 3.5–5 m/s and shout every 2–5 s;
- six **freeze** with a snowflake over their head, trembling, for 2–6 s,
  then snap out of it and run (at once if the fire gets within 1.5 m); and
- three **freeze for good** and never move again.

(The level sets the shares: 30 in 100 freeze for a while, 15 in 100 for good.)

While running, people:
- change their mind about where to run every 0.4–1.2 s;
- sometimes swerve sharply to one side for a split second (zig-zag);
- sometimes freeze for a split second as if asking "which way?!";
- drift with the direction nearby panicking people are running;
- run straight away from the fire if it is within 1.5 m;
- never stay pinned against a wall, because being blocked forces a new
  decision;
- **run into people** they are too fast to dodge. A hard hit (5 m/s closing
  speed or more, such as two runners head-on) knocks both to the floor for
  1–3 s, then they take half a second to get up. A lighter hit makes both
  stagger off course for a moment. A calm person who gets bumped is alarmed.
  Someone 4 or more points stronger than the person they hit only staggers.
- are sometimes **knocked out cold** by a hard knock-down or a heavy flying
  box: they lie still with three little yellow stars circling their head for
  6–12 s, then come round and get up slowly (1 s). About 1 in 10 knock-downs
  at the knock-down speed does it, more for harder hits and weaker people, never
  more than 6 in 10. The strongest almost never pass out.
- **trip**, now and then on their own (more often while zig-zagging), or over
  someone lying on the floor when there is no way around them.

Collisions and trips make a thud that calm people within 3 m turn toward. The
counter at the top left shows calm, scared (and frozen), down and lost people.

**Doors.** Doors are 1 m wide. The one way out starts locked; every door
inside the building starts shut but unlocked, so people work them
themselves. Since 2026-09-26 the player does not open or shut doors: a click
on a door draws people to use it (influence, below), holding the button down
on it keeps it shut (since prototype 3), and a right click turns its key --
which is how the locked way out is opened. The full controls are in
[look and controls](look-and-controls.md). On this level none of it costs
anything.

Runners head for a door. People can see that a door is open, but a shut door
looks the same to them whether it is locked or not; red and green are only for
the player. At a shut door a runner:
- opens it, if it is unlocked (0.4 s);
- otherwise rattles the handle for half a second, then either shoulders the
  door every half second for 1.5–4 s (each shove a thud people nearby hear,
  and the door judders), or gives up straight away. Stronger people shove
  more often rather than give up. An ordinary shoulder never moves the door,
  but a **strong person (strength 7+) damages it** with every shove: 1 damage
  at strength 7, 3 at strength 9. The damage stays, and the door visibly
  darkens as it weakens. At 40 damage it **bursts off its hinges** and falls
  flat outside, and stays open for good (clicking it does nothing). The brute
  needs about 14 shoves, several seconds of battering, often over more than
  one attempt;
- after giving up, glances toward another door and runs for it, avoiding the
  one that would not open for 6–12 s. If every door has failed them recently,
  they run somewhere away from the fire instead;
- opens the door at once if the player unlocks it while they are rattling or
  shoving it.

**A way out opening is news.** The moment a door becomes a way through —
opened by anybody, battered off its hinges, or blown open with TNT — the people
in the room it is in, and in the room straight through it, look up and think
again on the next tick rather than carrying on until their own next thought.
Everybody further off keeps walking their current plan, but stops believing
that door would not open. And a way out you can see standing open is never
crossed off: having tried the handle five minutes ago is not a reason to walk
past an open door to the street.

**Backing out of a crush is not giving up.** Somebody wedged in the press at a
doorway steps aside for a moment and comes again, keeping the way out as their
plan. They are only sent off to try a *different* door when there is another
one to try; with one way out of the building, being sent away would just mean
wandering off, and the queue behind them would set solid.

**People with a clear way out are in a hurry.** Anybody on their way to a way
out they can see open stops dithering: no hesitating on the spot, no zig-zag,
no drifting with whoever is running past. The exceptions are the ones who were
never going to: people frozen stiff with fear, people alight, anybody with the
flames already inside their danger distance, and anybody in the middle of
shaking somebody awake or dragging them out. The cruel still stop to shove.

Someone who walks 0.8 m out through an open door has **escaped**: they keep
walking for a moment and shrink out of view. People stuck in a crowd on the
way to a door try a different one for a few seconds. Someone wedged right
beside an open door, not lined up with the gap, steps aside against the wall
for half a second to a second so whoever is lined up can go first, instead of
two people jamming the doorway shoulder to shoulder.

**The building is rooms joined by doors.** Every room opens onto the long
corridor; the meeting room and the cafeteria also share a door, and the office
has a second door into the stockroom. The fire starts in the meeting room, so
the people elsewhere cannot see it and learn of it from the shouting, the
noise and the alarm. There is one door in an outside wall and it is the
player's: at the top of the T's north arm, locked at the start. The whole
building funnels towards it, down the corridor or round through the
stockroom, and the queue at each doorway is the thing to watch.

**Getting round what is in the way.** Somebody whose way is blocked tries a
step 30°, then 60°, then 90° to either side before giving up for the tick. The
sideways step is what lets a person pressed against a wall beside a doorway
slide along it instead of standing there until the crowd in front moves.

**A meeting is under way.** The meeting room's six begin sitting at its long
table, each in a chair that faces it. Somebody sitting who hears something turns in the seat to look, and stays
in the chair; if what they see frightens them, getting out of it costs them a
moment, and the nervous are quicker out than the placid. Laptops stand on the
desks and the meeting table, and boxes stand in stacked pairs: while a thing
rests on another it is in nobody's way, and it drops to the floor beside its
support the moment anything lifts, throws or smashes what holds it up. The
meeting ends when the level's timetable says (the minute mark), the host
first; that, and the rest of the calm day -- desks, toilet trips, chats -- is
[the cue system](cue-system.md).

People try to save themselves wherever they can. They pick a way **out of the
building** — scored by the whole walk there, including crossing the last room —
and head for the first door on that walk, room by room. Nobody walks into a
room that is alight, or across one to reach a door on its far side. Only
once every way out has been tried and would not open do they make for whichever
room is furthest from the flames instead — but not into a room that already
holds as many people as there is floor for (about one person per square
metre, so nine in the closet). The counter shows how many are in a room with
no fire in it. Being in a room is not escaping.

The fire can only get from one room to the next through an open (or broken)
door; walls stop it, and nobody sees fire through a wall. A closed door
muffles noises to half their reach.

**Slamming the door behind them is a villain's move.** Shutting the door you
have just come through, as you leave a room or the building, is something only
the cruel do: evil 8+ shut it whoever is running up behind, and only a body in
the doorway stops them. Evil 9+ turn the key as well, so nobody can follow.
Everybody else leaves it open for the people behind them.

Shutting a door because there is **fire on the other side of it** is a
different act, and anybody does it — it is the flames they are shutting out,
not the people. Somebody standing in a room that is not alight, within 2 m of
an open door with fire in the room beyond, pulls it shut -- but never while
somebody is still coming through it. Shutting a door on a person is a selfish
thing, so it takes a selfish person: once the flames are within 2 m of the
door itself, only the callous (compassion 3 or less) pull it shut on whoever
is coming; everybody else holds it (the owner's rule, 2026-09-24; it used to
take compassion 7 to hold a door at all). Nobody shuts the door they are
about to run through, nor one they have not yet decided about, nor one they
are dashing for through the heat. A closed door can be opened again by anyone
who reaches it (unless it was locked) or by the player.

**Dash past or hide.** When the only way out is through the heat -- the
flames within a person's danger distance of the door's approach, or the room
beyond the door alight -- they choose once, and again every few seconds. If
the floor between them and the door is walkable (no burning square within
half a metre of the straight walk, or of the spot itself), the brave (bravery
5+) run for it, and so does anyone whose own room is alight or who has
nowhere cooler to hide; while they run they neither bolt from the heat at
their danger distance nor abandon the door for it (`AgentDashedThroughHeat`,
sign "going for it!"). Everybody else gives that door up for a while and
makes for the room furthest from the flames that they can reach without
crossing burning floor -- a stall, the closet -- and, once inside, shuts its
door (`AgentHidFromTheHeat`, sign "too hot!"). With nowhere like that, they
keep clear of the flames where they are. People used to shuttle at the edge
of the heat, running back from it and picking the same door again, until it
reached them: the bathroom pair on seed 42 died like that with the door
three metres away. The owner's choice, 2026-09-24. Whoever carries an
extinguisher toward the flames runs, too; they used to stroll.

**Carried through the doorway.** Somebody down inside an open doorway --
knocked down, crushed, out cold -- with the crowd pressing on them from one
side is hauled on through it by the press at about two metres a second, and
once out through the wall line of a way out slides on to the escape depth:
an escape on their back (`AgentCarriedThroughDoorway`). They used to lie in
the gap as a plug nobody could pass, which is what held the seed 41 exit
shut for fifteen seconds after the player opened it.

**Helping each other.** A runner who is compassionate (6+), not too timid
(bravery 4+) and not cruel (evil 4 or less) and who is within 4 m of someone
frozen with fear runs over and shakes them by the shoulders for 1–1.5 s. The
frozen-for-a-while always snap out of it and run; the frozen-for-good do half
the time (otherwise the helper leaves them and does not try again). A runner
who is strong and compassionate (both 6+, and not cruel) and within 5 m of
someone knocked out cold runs over, gets a grip (1 s) and drags them along
behind at 1.1–1.5 m/s (faster the stronger), toward the nearest open door to
outside, or 3 m away from the fire if there is none. If the helper gets out,
the person they drag is rescued with them. Helpers let go if the fire comes
within their danger distance, if they fall, catch fire or are stuck for 2 s,
if the person wakes up, or if they cannot reach them within 5 s. Nobody
helps someone already close to the fire. In the default cast the saint, the
hero and one ordinary person shake people awake; only the hero drags.

**Tables and chairs.** Three 1.2 × 0.7 m desks stand in the office, one
5.4 × 1 m table runs down the middle of the meeting room, and two 1.2 m square
tables stand in the cafeteria. A table is
a thing like any other, not part of the building: it weighs 25 kg for each
square metre of floor it covers (so a desk is 21 kg and the meeting table
135 kg), and a crowd pressed against one shoves it, a blast turns it over, and
a hard enough knock tips it up on its edge. Nobody and nothing passes through
one: people slide along its edge as they would along a wall and steer away from
it, loose objects bounce off it, and random spots people pick to stroll to or
run for stay clear of tables. Runners also avoid spots and doors whose straight
route runs into a table. When a table has shifted 15 cm the walkable floor
around it is worked out again, so routes follow the furniture.
Eight chairs (0.45 m, 5 kg) behave like light boxes: runners kick them
skidding across the floor and trip over them.

**Things catch fire.** Boxes, chairs and tables within half a metre of
flames (a burning square or another burning thing) heat up, darkening as they
do. Cardboard boxes catch after 1.5 s of heat, chairs after 3 s, tables after
5 s. A burning thing glows with a crown of flame cubes for a while (boxes
8–15 s, chairs 12–18 s, tables 20–30 s) and is then left charcoal-black,
still solid but never burning again. While burning, a thing that rests in
one floor square for a second sets that square alight, so a burning box
kicked across the room starts a new fire where it stops. Anyone touching a
burning thing catches fire, and anyone on fire who touches a thing sets it
alight.

**Picking things up.** Boxes and chairs are items. Anyone can lift an item
up to 5 kg plus 2.5 kg per strength point (an ordinary person 17.5 kg, the
brute 27.5 kg). Now and then (12% of fresh decisions) a calm person tidies up:
they walk to the nearest item within 4 m they can lift, pick it up (0.5 s),
carry it in front of them to a spot at least 1.5 m away, and set it down
(0.4 s) on clear floor. A load slows them, by up to 40% for a load as heavy
as they can manage. Someone carrying something who is startled or scared
lets go at once: the nervous (6+) drop it, everyone else throws it ahead of
them. Knocked off their feet or set alight, they drop it; merely distracted
by a noise, they put it down. A strong runner (6+) who meets a box or chair
they can lift in their way hurls it aside instead of kicking it, and a cruel
one (evil 7+) hurls it at the nearest person within 4 m. A thrown item flies
faster the stronger the thrower and the lighter the item, and because it
strikes the body rather than the feet it hits three times as hard as a
sliding one, enough for a chair to knock someone off balance.

**Boxes.** Cardboard boxes, 0.3–0.6 m wide and 3–20 kg, stand against the
office walls, fill the stockroom and make up the tower at the T. The tower's
boxes are the exception to everything below until it falls: nobody kicks,
lifts, carries or hurls a box from the standing tower, however strong. Once
it has fallen into the archway its boxes are there for the taking, which is
how the heap is cleared.
Calm people walk around them. Runners barely look: they kick a box sliding
across the floor, and at running speed they may trip over it instead, more
often the faster they go and the bigger the box. A kicked box slides about a
metre, spins if it was hit off-centre, and bounces off walls, other boxes and
people. A heavy, fast box can knock someone off balance or trip them.

**People catch fire.** A person touched by the fire does not drop dead: they
burst into flames (an orange, flickering, flailing capsule with little flame
cubes licking up it), scream every half second or so, and either **throw
themselves down and roll** — about two people in five do, and about a third of
those rolls smother the flames and they get back up — or run around wildly
at full sprint, lurching in a new direction every 0.2–0.5 s and paying no
attention to doors or other people, for 3–6 s. Then they collapse, lost, as
a dark red capsule. Anyone they run into catches fire too, and anyone within a
hand's breadth (10 cm) of them has a 1-in-5 chance each tick. Someone on the
floor when they catch fire burns where they lie, and people lying on the floor
can be caught by the fire too. Nobody on fire can escape through a door. With
every door locked, everyone who does not get out is eventually caught (about
40 seconds after the fire starts with the default seed), and the round then
ends and is scored.

**Shoving people out of the way.** Running into somebody is an accident that
needs speed. Taking hold of them and heaving them aside is deliberate, works at
a walk, and only the cruel do it — which is why you see it in the queue at a
doorway, where nothing used to happen. The person shoved is sent about a third
of a metre clear and goes down rather than just reeling if the shover is much
stronger; a calm person shoved is alarmed, and it makes a thud. Somebody frozen
with fear can be heaved aside too: they never move of their own accord, but this
is somebody else's doing. One shove per person every 0.8 s, so a bully clears a
doorway over several seconds rather than at a stroke.

**Bags and briefcases.** Four people are holding something: the brute and
the timid carer with briefcases, the saint and the worrier with bags. It is
theirs, so they keep hold of it while they are calm rather than tidying it
away. The moment something frightens them they let go — the very nervous fumble
it onto the floor, everybody else flings it away from them in whatever
direction they happen to be facing, and the cruel aim it at the nearest person.
It is a reflex rather than a plan, so a briefcase sails off at an angle and
clatters into a table.

**Fire alarms.** Two kinds of red thing on the walls (2026-09-25): a small
pull station at hand height, and bell units high on a wall of every room
people use. Since prototype 3 (2026-09-25, the owner's rule) the building
has **one** pull station, at the far west end of the corridor beside the
maintenance room and past the meeting room's door, so pulling it means
walking toward the fire and only the brave do. Anyone who has taken in that
there is a fire, is not in immediate danger, has their hands free, and is
brave (6+), leads (6+) or thinks of others (compassion 6+) breaks off to hit
it if it is within eight metres' walk -- half a second to press it -- and
then runs.
Hitting one rings **every** bell in the building at once, so a shut door
cannot leave a room in the dark, and the bells ring again every six seconds
or so, each on its own beat, so a door opened later lets the noise through.
A bell the flames reach goes off with a crack and falls silent; the others
ring on.

A bell frightens whoever hears it exactly as the sight of flames would -- the
runners run, the freezers freeze -- fire or no fire: the owner's rule, "pull
it, and everybody panics". Until 2026-09-25 the brave and steady walked out
at a stroll instead, and an alarm pulled before the fire existed left
everybody standing startled, facing the bell, until the flames came. Nobody
wedges a door in a building with nothing burning in it, bell or no bell. The
whole thing can be switched off in the scenario.

**Furniture breaks.** A hurled box or chair that slams into a chair hard enough
smashes it: it collapses into flatter, lighter wreckage that people still trip
over but nobody can sit on. A hard enough hit **smashes a table**: it collapses into a heap
of boards about half its short side across, whatever stood on it slides off onto
clear floor beside it, and the heap takes a share of whatever knocked it over,
so it skids rather than simply appearing. (A table merely shoved or turned over
is still a table, lying where the physics put it.) From then on it is an ordinary thing on the floor — it blocks, it
slides when somebody kicks it, somebody strong can heave it out of a doorway,
and people trip over it — but the ground the table stood on is walkable again,
so the shape of the room changes while you watch.

**Electrical things pop.** The microwave and the wall sockets do not sit and
burn: the moment the flames reach them they go off. A bang the whole building
hears, everything loose nearby flung away from it, anybody standing close
knocked off their feet, a scatter of fresh fire on the floor around it, and the
thing itself left as wreckage. One pop can start a second fire across the room
from the first -- but never through a wall: since 2026-09-26 a bang lights
floor only in its own room, or in a room open to it through a doorway.

**The cable runs one way** (the owner's rule, 2026-09-26: "only if the fusebox
goes, it should quickly cascade down to all outlets, but not the other way
around"). A socket going off, in the flames or because the Director chose it,
is a bang and nothing more. When the **fuse box** goes -- the flames reach it,
the Director sets it off, or the card -- a spark races out along the cable at
twenty metres a second and every socket down the line pops in turn: the
office's three are all gone within about two and a half seconds. A socket
already wrecked does not stop the spark; it carries on past to the next.

**Wedged doorways.** Anything left resting in a doorway jams that door, from
either side, and both ways: it cannot be opened and it cannot be shut, and no
number of clicks will move it. It happens by accident all the time — a kicked
bin comes to rest in a gap, an office chair rolls into the one way out of the
building — and people discover it exactly as they discover a locked door: they
walk up, try it, and go looking elsewhere. Somebody strong (7+) instead heaves
the obstruction out along the wall and then goes through, and somebody taking
charge sends them at it, because a chair in a doorway is there for anybody to
see and needs no memory of having tried the door.

**You can see why a door will not open.** Point at a wedged door and the line
at the top left says so, rather than letting the click look as though it did
nothing.

People also wedge doors **on purpose**. Somebody frightened (nervousness 7+),
sheltering in a room, fetches the nearest thing they can lift, carries it to a
shut door and sets it down in the gap to keep the fire out. The cruel (evil 7+)
do the same thing to keep other people out. The kind never seal a door with
somebody still coming through it, and nobody seals the door they are counting
on themselves — unless the room beyond it is already alight, at which point it
has stopped being a way out.

## The round

**It builds up** (prototype 3, second batch, 2026-09-26). A card covers the
screen before anything moves: the level's name, how many of the twenty have to
live to clear it, your best ever, and a box holding the seed with a **Random**
button beside it. Press **Play** and the office comes to life — people walking
about, the meeting under way — with nothing wrong at all. A strip along the top
counts *saved*, *lost* and *still inside* against the target, and under it sits
**Trigger event**. Half a minute to a minute and a half in (the seed decides),
or the moment you press the button, **a waste bin in the meeting room catches
fire** -- by the door one seed, in the far corner or under the north wall the
next. It smoulders for about ten seconds before the carpet under it catches,
and while the fire is young it spreads slowly, so somebody brave has a real
chance to take the extinguisher off the meeting room's wall and put it out.
Pressing the button twice does not light two fires.

**If the bin is put out** -- nothing burning anywhere, and it never got out of
the meeting room -- any bell that was pulled falls silent about ten seconds
later (the all-clear; pulled again by somebody still frightened, it falls
silent again), people calm down at their own pace, and twenty to forty
seconds after the put-out **a wall socket crackles**: in the room with the most
people still calmly at work, never the room that just had the fire. It spits
sparks and smokes for five seconds (the curious may wander over to look), then
goes off: a bang and a small fire. **If that is put out too, the fuse box
crackles and goes**, and every socket after it. That is the last rung.

**If a fire gets out of the room it started in, it is the real fire.** The
Director adds nothing more, and only now is the tower of boxes armed: the first
person near it brings it down. If everybody simply runs out, that is fine too.
The round does not end as "nothing is happening" while the Director has
something still to come.

**Pause looks, it does not act.** **Space** stops everything: people mid-stride,
flames mid-flicker, smoke mid-drift. The camera still answers you so you can go
and read what is happening in the far room. No card can be picked up, no door
can be clicked, and a card already in your hand is put back down. Space again
and it all carries on.

**It ends when nobody is left to resolve.** That means every person is out of
the building or dead — or the whole building has been doing nothing at all
for long enough that there is plainly nothing left to wait for: nobody has got
anywhere, nothing new has caught, no door has moved. A queue wedged in a
doorway counts as something still happening, so a round never ends on top of
a crush that has not cleared. It used to end the moment everybody left was in
a room the fire could not reach; that stopped rounds while people were still
walking to the door, and a shut door does not make a room permanently safe
anyway. Everyone still alive at the end is written down as having
**survived**, which counts as saved exactly as escaping does: barricading
yourself into the storeroom is a way of living through a disaster, not an
exploit.

Then the scene freezes and a card gives the result — how many of the twenty
were saved and what share that is, whether it cleared the 75% needed, how the
saved split between those who got out and those who sat it out, and your best
ever. Two buttons: the same seed again, or whatever is in the seed box.

**What the round deliberately does not do yet.** You cannot click a person on
the frozen scene for the facts about them, and there is no written retelling of
what happened out of sight. Both are planned and both are what would make a run
*understandable* rather than merely scored.

## The camera

**W A S D** slide the view across the building, and W always moves it up the
screen whichever way you are looking. **Hold the right mouse button and drag**
to swing the view to any angle at all; it stays where you let go. **Q** and
**E** snap a quarter turn to the next corner view from wherever the view is
now, so the four corners remain somewhere tidy to land. The **mouse wheel**
zooms, and tilts as it goes: pulled out you look down on the building at the
isometric angle, pushed in you look along the floor. A right *click* without a
drag puts down the card in your hand, or, with none in hand, turns the key of
the door under the pointer. The camera keeps working while the game
is paused. The full description is in [look and controls](look-and-controls.md).

## What the player can do

**Doors.** Working a door costs 10 whatever you do to an inside one --
open, shut, lock, unlock -- and unlocking the building's way out costs the
whole purse, 100 (the owner's rules, 2026-09-25). The key is yours to turn
both ways: a locked door is yours to unlock and a shut one yours to lock,
and an open one shut and locked in one go if nobody is in the doorway. Once
the way out is unlocked the people open it themselves.

**The office has no purse (prototype 3, 2026-09-25).** The owner had
influence switched off for now: every door, alarm and card is free, nothing
is paid in, and the bar and the prices are gone from the screen. The cards
are still dealt by the dead and still played. Everything below about the
purse is how the rules stand in the code, switched off on the office level
(`LevelDefinition`), for a level that turns them back on.

**Influence and cards: the dead deal, the uproar pays.** A round opens with
**thirty** and **one card**, drawn at random from the deck (the owner's call,
2026-09-24; it used to open with nothing at all). Thirty is one move: a card,
or a pull of a fire alarm. The purse holds a hundred at most, and unlocking
the way out costs exactly that, so a full purse is the one thing that opens it.

The meter fills from the building being in uproar: somebody shouting, tripping,
freezing or running into somebody else pays a little; a knockdown, a shove, a
door forced or burnt through, something broken, an alarm pays more; somebody
catching fire, going out cold, being crushed, an appliance going off or a door
coming off its hinges pays most. Everybody who gets out alive pays 15. A quiet
building pays nothing, so you cannot act until things are going wrong.

**Cards are not bought — they are dealt by the dead.** Every person the disaster
kills puts one card, drawn at random, on your bar. A round nobody dies in leaves
you with a full purse, the one card you opened with, and doors as your other
move. Deaths pay in cards and not in influence, so the two currencies have one
source each. **The deck is four cards** (the owner's calls, 2026-09-24 and
2026-09-25): **Beefcake**, **TNT**, the **fire extinguisher** and **Stick
together**. The four trait cards, "start a fire" and "pop the fuse box" still
exist as commands and a level may put them in its starting hand, but the
office never deals them.

**Stick together** is thrown at a patch of crowd like a trait card, and
everybody caught becomes a group: once frightened, each is pulled toward the
others, the ones ahead slow for the ones behind, they go for the door the
most leaderly of them goes for, and a visitor bound to somebody who works
here is told the way out. It is not a leash: the cruel (evil 7+) walk off,
the brave feel it least and the nervous most, anyone with flames at their
back runs regardless, and a throw that catches fewer than two people makes
no group and is free.

**On the office, only people pull fire alarms** (the owner, 2026-09-26). A
click on the red pull station puts influence beside it instead, and the brave
among the people drawn there may pull it. A level that lets the player pull
alarms (`LevelDefinition.playerPullsAlarms`) prices it like a card, 30: every
bell in the building rings, exactly as when a person hits one, and pulling one
that is already ringing does nothing and costs nothing. Either way the bells
stop about ten seconds after the Director judges a fire put out.

**Influence: draw people to a place** (2026-09-26, the owner's idea). Click a
door, a thing or a patch of floor and people near it are drawn toward it --
never ordered there. The owner: "clicking a door once just increases the
chances of an agent using the door; clicking it a few more times increases it
more; clicking an empty hallway a few times acts like an attractor influencing
the agents' own decision making."

- **Each click is one step**, up to twenty: a strong pull takes frantic
  clicking. It loses a step every two seconds, so a full one fades over about
  forty, and it cannot be taken back. There is no limit to how many places
  there are.
- **It sticks to its place and fades with distance**: anybody within about
  twelve metres feels it, more the nearer they are, including somebody who
  wanders in later -- but never from another room (a door's is felt in both
  rooms it joins).
- **Everybody weighs it by who they are.** The nervous and visitors follow
  readily (up to twice an ordinary person); leaders and the cruel mostly ignore
  it (a tenth, at the extreme). Nobody is drawn into a room that is alight, or
  through the heat, or straight back through the door they just came in by.
- **Frightened people** choose their door and where to run with it added in: a
  full, close pull is worth as much as an exit sign pointing that way, more
  than a door standing open. An influenced door on the wall of their room is
  considered as a way round even when it is not on the shortest walk -- the
  office's stockroom door can beat the corridor.
- **Calm people feel it too.** With nothing in particular to do, they wander
  over to it, the likelier the stronger the pull. The easily led -- nervousness
  7 or more, or a visitor -- may get up from their seat or leave an errand for
  a strong one, but only between the moving parts of it: never halfway into a
  chair, mid-conversation, or while somebody is waiting to meet them. The
  steady carry on. So the player can thin out a meeting
  before anything happens, but not empty it.
- **You can see it work.** A sparkling aura on the place, faint at one click and
  intense at twenty, and a sparkling line from everybody feeling a pull to it,
  faint for a gentle pull and bright for a strong one. When influence actually
  changes somebody's mind, the log says so.

**Nudge a person.** Click somebody and they step away from where the click
landed, stagger, and look round a beat later for whoever did it. Three nudges
in ten seconds and they are annoyed: "leave me alone!", they shake with it,
and for about twenty seconds nudging them does nothing at all. A nudge
frightens nobody.

Every card costs **30**. A card you are not holding does nothing however rich
you are; a card you cannot pay for does nothing either. **A card that catches
nobody is a miss: it costs neither the influence nor the card**, and stays on
your bar. A card that catches the wrong person is spent, and that is your own
fault — which is why a circle is drawn on the floor under the pointer showing
exactly the patch it will catch, brightening when somebody is standing in it.

Click a card to pick it up, then click the floor to throw it (2026-09-25;
the number keys are gone). Escape or a right click puts it back down. Two of
a kind sit as one card with the count in its corner. A click on a door draws
people to it; a right click turns its key. Point at a door, a red pull station,
a person, a thing or the floor and the line under the score says what a click
will do. **Reset**, top right, goes straight back to the start card with the
seed kept; **Pause** sits under it; the red **Trigger event** button sits
bottom centre and goes the moment it is pressed.

The five **trait cards** are thrown at a patch of floor about a doorway and a
half across — not at a chosen person — and slam one dial to the end of its scale
for everybody standing inside, for the rest of the round:

1. **Beefcake** — strength to the top. They shoulder a locked door off its
   hinges in a few swings where before they gave up on it, shrug off hits that
   used to floor them, heave obstructions out of doorways, and carry the
   heaviest thing in the room — flooring anybody in the way as they go.
2. **Courage** — bravery to the top. The frozen unfreeze, somebody fetches an
   extinguisher and goes at the fire, and then keeps walking toward the thing
   that is killing everybody.
3. **Terror** — nervousness to the top. Whoever is caught bolts. Thrown into a
   calm room it starts the evacuation early; thrown into a doorway it causes a
   crush.
4. **Bastard** — evil to the top. They shove people aside and lock doors behind
   them. Thrown at a jammed scrum the queue unjams, because somebody threw two
   people into a wall.
5. **Cold heart** — compassion to the bottom. They stop running back in for
   people who are not getting up. The card that saves a life by making somebody
   worse.

The other four are aimed at the building:

6. **Start a fire** — click the floor and a fire starts on that square. If the
   scenario's own fire has not begun yet, yours is the one the run gets.
7. **Put down an extinguisher** — click clear floor and a full red bottle
   appears there for somebody brave to pick up. There are four spares, and the
   card stops being dealt once they are gone.
8. **TNT** — click a wall and it blows open into a ragged gap half again as wide
   as a door. A hole is not a door: nobody can shut it, lock it or batter it,
   and it stays open for the rest of the run. A hole in an outside wall is a new
   way out that nobody can take away. The bang is heard across the building,
   flings loose things away from it, and knocks anybody within a stride and a
   half off their feet. There are four sticks.
9. **Pop the fuse box** — the biggest bang in the building, and it takes the
   whole chain of sockets with it.

## Deterministic rules

- The simulation runs at 50 logical ticks per second through one
  `RunDriver.FixedUpdate` entry point. It keeps the contract's order:
  fire advances, fire contact at current positions, agent decisions in
  ascending Agent ID order, movement resolution, then fire contact along
  accepted moves.
- Positions are integer millimetres. **Headings are whole degrees** clockwise
  from north (+Z), so 90 is east. Sine and cosine come from a fixed 91-entry
  table (`IntegerMath`), and headings of vectors from an integer binary search,
  so no floating-point value decides an outcome.
- **Steering.** Each tick an agent turns toward its goal heading by at most its
  seeded turn rate (calm 4–7°, panic 10–16° per tick). It changes speed by at
  most its acceleration (calm 2, panic 8 mm/tick per tick), and brakes twice as
  fast. Speed is halved while the remaining turn exceeds 75°. The goal heading
  is blended with a push away from people inside 0.8 m and from walls within
  0.6 m. The agent picks its own valid step: at a wall it keeps the along-wall
  part of the step, and if a person is in the way it tries ±30° and ±60°
  side-steps. The resolver still accepts or rejects the request exactly as
  [spatial-world-rules.md](spatial-world-rules.md) defines. A rejected move sets
  speed to zero and counts as a blocked tick. Each tick a person's behaviour
  (calm, alert, panic, or busy at a door) states one goal, and the body turns
  and accelerates toward it exactly once.
- **Calm decisions** happen when the current activity ends or the agent has
  been blocked for 20 ticks. After moving, the agent stands (55%) or looks
  around (45%). Otherwise it strolls (50%), goes to stand near another calm
  person 1.5–6 m away (25%), or looks around (25%). A stroll picks a random
  spot at least 1 m from the walls, wanders by up to ±25° that changes every
  0.5–1.2 s, and slows down within 0.7 m of arrival. Looking around is one to
  three glances of 35–120° to a random side.
- **Panic decisions** happen every 20–60 ticks, when blocked for 12 ticks, or on
  arrival. A decision may start a 5–15 tick hesitation (12%). Otherwise it
  scores eight random spots at least 0.7 m from the walls:
  - plus the spot's distance from the fire;
  - minus 5 m if the straight route passes within 1 m of fire;
  - minus 2 m if the spot is closer than 1.5 m;
  - minus 10 mm per degree of turning needed;
  - plus up to 1.5 m of random noise.

  The best-scoring spot wins. There is also a 35% chance of a 30–70° swerve
  for 10–25 ticks. People following others add 35% of the average heading of
  panicking agents within 2.5 m.
- **Fire.** The room is a 24 × 24 grid of 500 mm cells. At tick 250 the cell
  under a seeded point in the ±2 m spawn square ignites (`FireActivated`).
  Each burning cell, in ignition order, waits a seeded 40–120 ticks. It then
  lights one random unburnt north/east/south/west neighbour (`FireSpread`,
  whose causal parent is the igniting cell's event) and waits again. Cells
  never go out. A cell with no unburnt neighbours stops spreading.
- **Contact.** An agent catches fire when its 250 mm footprint overlaps a
  burning cell, either where it stands or along an accepted move:
  `AgentCaughtFire` (parent: the earliest-lit cell it touched; duration: a
  seeded 150–300 ticks). It becomes scared, its activity is `Burning`, and it
  forgets any door. Each tick a burning upright person screams (an
  `AgentYelled`, parent: the catch) every 25–50 ticks, picks a random heading
  every 10–25 ticks (or after 5 blocked ticks), steers off walls only, and
  sprints; its bumps count like a runner's. After movement, each person already
  burning, in ascending ID order, sets alight anyone whose body is within
  100 mm of theirs with 20% per tick; a collision involving a burning person
  always does. When the burn time ends the agent is lost: `AgentLost` (parent:
  its `AgentCaughtFire`). So the log traces every death through a chain of
  burning people and squares back to the first spark.
- **Vision.** Agents do not use a proximity fear radius. They have a forward
  90-degree vision cone with a 12 m range around their current heading. A calm
  agent becomes alert when the nearest point, centre, or a corner of any
  burning cell lies inside that cone and the line of sight to it runs through
  open doorways only: the same room, or through the gap of an open door
  between the two rooms, or through two such gaps with one room between
  (`WorldGeometry.CanSeeBetween`); the wall beside an open door hides as much
  as any other wall. It yells and receives a seeded 0–20 tick reaction delay
  before panicking. Agents alerted by a yell, a bump or the sight of somebody
  bolting (`AgentAlertSource.SawSomeoneRun`: a frightened person running at
  40 mm a tick or more, or leaping out of a chair, within 8 m along a line of
  sight) receive their own delay without pretending they have seen the fire,
  and turn toward where it came from; they can promote to a visual alert when
  the fire enters their cone. Somebody with bravery 7 or more who sees a
  person bolt turns to look instead of taking fright. The `AgentScared`
  event's parent is the agent's own alert.
- **Sound.** A sound is data: a position, a hearing reach, an optional alarm
  reach, and the event that made it. It is delivered at once to every other
  calm participating agent in ascending ID order. Inside the alarm reach the
  listener becomes alert (`Yell`); inside the hearing reach it starts
  `Investigating` and logs `AgentNoticedSound`, whose parent is the sound's
  event. Yells reach 12 m (alarm 6 m); collision and trip thuds reach 3 m
  (no alarm). Fire is a continuous sound: each tick a calm agent that cannot
  see the fire but is within its hearing reach of a burning cell (3.5 m for
  one square, 50 mm more a square, at most 15 m, and half that through a wall
  or a shut door) notices it, with that cell's ignition event as parent. A
  person keeps up to three noises in mind (`AgentHearing.Pending`): while
  looking toward one, a threat's noise or a louder noise nearer to them takes
  over and the other waits; the rest are looked at in turn while they are
  fresher than 300 ticks; the same noise again within 300 ticks of looking at
  it turns no head. Somebody who has looked toward a threat's noise from
  another room and seen nothing starts the go-and-look cue (see
  [the cue system](cue-system.md)). Investigating lasts a seeded
  50–125 ticks: turn toward the point at the panic turn rate and, after 25
  ticks, if facing it within 30° and more than 2 m away, walk toward it at
  half calm pace.
- **Traits.** Each person has six traits, 0–10 (`AgentTraitValues`), authored
  in the scenario or drawn at tick zero as the rounded average of two 0–10
  draws. `TraitEffects` is the only code that turns traits into numbers; a
  trait of 5 gives exactly the scenario value and each point away from 5
  changes it by the percentages in the scenario's `Traits` settings. Pace is
  spread evenly between the Speed 0 and Speed 10 values plus a seeded jitter
  (±2 walking, ±5 sprinting, mm/tick).
- **Temperament.** At tick zero, after the per-agent draws, a deck of
  `round(n × 15%)` `FreezeForever`, `round(n × 30%)` `FreezeThenRun` and the
  rest `Runner` is dealt, freezing cards first, to people in order of
  fearfulness (nervousness minus bravery), with a seeded Fisher–Yates shuffle
  breaking ties. On becoming scared, runners flee; the others log
  `AgentFroze`, stand still and turn to stare at the nearest fire.
  `FreezeThenRun` agents log `AgentUnfroze` and start fleeing after a seeded
  100–300 ticks, or at once when fire is within 1.5 m. Fleeing agents log
  another `AgentYelled` every seeded 100–250 ticks.
- **Body state.** `Upright`, `Staggering`, `Fallen`, `GettingUp` or
  `Unconscious`. A body
  that is not upright makes no move request but still occupies space and can
  be caught by fire. `Fallen` ends in 25 ticks of `GettingUp`, then
  `AgentGotUp` (parent: the event that put it down). A scared agent back on
  its feet makes a fresh panic decision; a frozen one stays frozen.
- **Collisions.** When a fleeing, upright agent's straight step is blocked by
  an upright person, its closing speed is the difference of the two
  velocities along the line between them (integer, mm/tick). At 50 or more it
  records a bump instead of side-stepping. After movement, bumps are resolved
  in the order recorded: `AgentsCollided` (source: the runner; strength: the
  closing speed; parent: the runner's `AgentScared`), then at 100 or more an
  `AgentKnockedDown` for each person (60–140 ticks on the floor), otherwise
  both stagger for 10–20 ticks with a 25–50° heading jolt. A calm person who
  is bumped becomes alert (`Bumped`).
- **Trips.** At each panic decision, a runner at 60 mm/tick or faster trips
  with a 4% chance (8% during a swerve) and is `Fallen` for 40–100 ticks
  (`AgentTripped`, parent: its `AgentScared`). A runner at that speed whose
  way is blocked by someone on the floor, with no side-step available, trips
  over them (parent: the fallen person's down event).
- **Knocked out.** After each `AgentKnockedDown`, one roll: 10% plus 1% per
  mm/tick of closing speed above 100, minus 3% per strength point above 5,
  clamped to 0–60%. A flying box that trips someone rolls the same way with
  1% per 100 kg·mm/tick of momentum above 1,600. A pass-out logs
  `AgentPassedOut` (parent: the fall; duration 300–600 ticks) and the body is
  `Unconscious`: it occupies space, can be tripped over and caught by fire.
  Then `AgentCameTo` (parent: the pass-out), 50 ticks of `GettingUp`, and
  `AgentGotUp` (parent: `AgentCameTo`).
- Lost agents keep their state and position but leave occupancy and make no
  later decisions.
- **Player commands.** Every command is queued for a tick that has not started,
  and consumed at the start of that tick in the order it was queued, by
  `PlayerCommandSystem` — which holds the queue but decides nothing: each command
  is carried out by the system that owns those rules. A command names either a
  thing (a stable ID) or a place (whole millimetres). Screen positions, rays and
  colliders stay in the presentation and are turned into one of those two before
  anything is queued. A door click is a `ClickDoor` command. Locked → unlocked logs
  `DoorUnlocked` (a root event: the player is the cause); unlocked → open logs
  `DoorOpened` with the unlock as its parent; open → unlocked logs
  `DoorClosed` (a root event), but only if nobody is in the doorway: a body
  within the door's width (plus a radius) and no more than a radius + 0.1 m
  inside the wall, and either within a radius + 0.1 m outside it or, for a
  door to outside, anywhere in the outside doorway.
- **Closing by people.** When someone walks into another room (the door
  behind them, within 2 m), after an `AgentEscaped` (parent of the close),
  and each tick for someone within 2 m of an open door with fire in the room
  beyond it (parent: their `AgentScared`), the rules above decide; a close logs
  `DoorClosed` (source: the person, target: the door) and an evil person's
  lock logs `DoorLocked` (parent: that close).
- **Doors and escape.** See [spatial-world-rules.md](spatial-world-rules.md)
  for the doorway strip. A panic decision first scores the doors (see
  [prototype 1's decisions](history/decisions-prototype-1.md)) and targets a point 0.6 m
  inside the chosen door, or 1.5 m outside once it is open and the runner is
  lined up. Near the door, swerves and following are switched off and the
  runner is not pushed away from that wall. A runner blocked for 12 ticks
  within 0.85 m of their open door, inside the room and not lined up, gives
  way instead of making a new decision: for 25–50 ticks they target a point
  0.4 m inside the wall and 0.85 m along it from the door's
  centre, on their side (clear of anyone passing through). Reaching a shut door logs
  `AgentTriedDoor` (parent: `AgentScared`); each shove logs `AgentForcedDoor`
  and giving up logs `AgentGaveUpOnDoor` (both parent: the attempt). A runner
  who opens a door logs `DoorOpened` with their attempt as parent. After
  movement, anyone 0.8 m out through an open door logs `AgentEscaped` (parent:
  that door's `DoorOpened`, or `DoorBrokenDown`).
- **Taking charge.** Everyone has a seventh trait, leadership. Someone with
  7+ who is not in immediate danger looks around every second or so and takes
  charge. First choice: a way out they have tried themselves and found shut,
  with somebody strong enough to break it (strength 7+) within 6 m — they send
  that person at it, and the breaker keeps at it for 15 s instead of giving up.
  Second: the fire is still small and a bottle is free — they send the bravest
  person nearby (bravery 5+) for it. Otherwise they simply shout, and anyone
  within 5 m in the same room falls in behind them for 8 s, running where they
  run until they are a stride and a half away.
  Whether somebody does as they are told is personality: evil 7+ never does,
  nor does anyone whose own leadership is as high as the leader's, and
  otherwise the chance is 55% plus 4% per point of nervousness less 3% per
  point of bravery. Nobody frozen with fear hears any of it: they have to be
  shaken. A green arrow marks whoever is being followed, a small one whoever
  is following.
- **Fire extinguishers.** One red bottle stands by a wall in each big room,
  with six seconds of spray in it. Someone brave (7+) near a fire of no more
  than 24 squares, or someone kind (7+) who can see a person alight within
  8 m, fetches the nearest free bottle, carries it to about 2 m from what they
  are fighting, and holds the trigger down. The jet works back and forth across what they are fighting rather than holding
one line: the aim swings either side of the target over two and a half seconds,
24° less 3° per point of strength, so an ordinary person wavers 9° either way and
anybody strong holds it straight. The jet is a 3 m, 30° cone: it
  puts a burning square out after 0.6 s on it (one square at a time), puts out
  burning things and people it covers, and knocks anyone standing in it
  backwards and onto the floor. A square that has been put out stays too wet
  to catch again for 20 s. Holding the bottle makes them brave enough to stand
  at half their usual keep-away distance from the flames, but no closer. The
  recoil shoves them back 60 mm a tick, less 12 mm per point of strength, so
  anyone with strength 5 or more holds it steady and the weak are walked
  backwards; strength 0 is put on the floor by their own extinguisher. When
  the bottle runs dry they drop it and run.
- **Sitting.** A calm person choosing what to do next may walk to the
  nearest free chair in their room within 6 m and sit on it for 5–20 s. Nine
  people also start the run already seated, for 60 s. They do not appear on the
  seat: they stand beside the chair, pull it 30 cm out (it stops against
  whatever is behind it), and lower themselves onto it over a fifth of a second
  while it slides back in under the table, stopping a person's width short of
  it. A chair faces a direction, and whoever sits on it turns to face the same
  way at their usual turning pace, so a chair pulled up to a table seats
  somebody looking at the table. A chair with someone on it does not slide,
  cannot be picked up and cannot be tidied away. Somebody sitting who hears a
  noise turns in the seat to look and stays in it; if there is nothing to see
  they turn back to the table. Getting up on purpose runs the same way round:
  they let go of the chair, back out on it, and rise onto a clear spot. Anyone
  startled, knocked over or set alight in a chair leaps out instead, which takes
  0.8 s less 0.04 s per point of nervousness and never less than 0.2 s, so the
  nervous are out first — and the chair goes over backwards behind them.
- **Resting on something.** An object may stand on a table or on another
  object. While it does it is in nobody's way, does not slide, and is drawn at
  the height of whatever holds it up, but it still heats, burns and can be
  picked up. Lifting, throwing, shoving or blasting it, or smashing the table
  under it, brings it loose, and it lands on the nearest clear floor rather
  than inside the table or on top of the box below. A stacked box topples the
  moment the one under it is kicked, lifted or smashed.
- **Loose things.** Every kind of loose object has an entry in the
  scenario's table of kinds: friction as a percentage of the floor's, and how
  long it takes to catch fire and how long it burns. An ignite time of 0 means
  it never catches and never even heats up. Boxes and wooden chairs behave as
  before; office chairs roll on castors (friction 35%), laptops skitter (55%),
  bags and bins slide about as boxes do, and potted plants (200%) barely shift
  and never burn.
- **Breaking doors.** The chance to start shoving rather than give up is
  60% plus 5% per strength point above 5. Each shove adds
  `(strength − 6) × 1` damage to the door (nothing below strength 7); damage
  is kept per door. When it reaches the door's strength (40) the door becomes
  `Broken`: it logs `DoorBrokenDown` (source: the shover; target: the door;
  parent: the shove) and counts as open for walking, choosing and escaping.
- **Rooms.** Rooms are rectangles that never overlap. Two rooms that share
  a wall line are joined by a door in it; a door with no room beyond leads
  outside. A footprint wholly inside any room is walkable, and an open door's
  strip joins the rooms either side of it. Only a door leading outside can be
  escaped through. The fire grid covers the rectangle around all rooms; each
  cell belongs to the room its centre is in (or none, and never burns). Fire
  spreads between neighbouring cells of different rooms only when the
  connecting door is open and the edge they share overlaps the door gap. Fire
  in another room is neither touched nor seen unless the rooms are joined by
  an open door. A noise's hearing and alarm reaches halve between rooms not
  joined by an open door.
- **Choosing a way out.** Every door leading outside is scored by the length
  of the walk to it through the rooms (doors are joined door-centre to
  door-centre; a shut door still counts as a way through, a door they gave up
  on does not), plus the usual open-door bonus, current-choice bonus, random
  noise, fire and table penalties. The person then runs for the first door on
  that walk. A door someone has stood at and failed to open stops counting as
  a way out for them. With none left, they score rooms instead: distance from
  the flames, minus a quarter of the walk there, rejecting rooms already full
  (one person per square metre of floor). With fire in the room: out through the door to 1.5 m inside the
  main room if it is open, else directly away from the nearest fire.
- **Helping.** Considered in the panic decision each tick by a fleeing,
  upright, empty-handed person not in danger: the nearest person in need
  within range (frozen and upright for shaking, unconscious for dragging),
  not already someone else's target and not within the helper's danger
  distance of fire. `AgentShookAwake` (source: helper, target: the frozen
  person, parent: helper's `AgentScared`) is the parent of their
  `AgentUnfroze`. `AgentGrabbed` starts a drag; after each tick's movement the
  dragged body is placed 0.55 m behind the helper; if there is no room there
  the helper's step is undone, or, if someone has taken the helper's old
  spot, the helper lets go (`AgentDropped`, parent: the grab). A helper who
  escapes while dragging logs `AgentRescued` (target: the dragged person,
  parent: the helper's `AgentEscaped`) and the dragged person's outcome is
  `Escaped`.
- **Tables.** A table is a fixed rectangle. A body of radius r overlaps it
  when its centre is strictly inside the rectangle grown by r on every side
  (square corners). A step into a table is moved onto the grown edge facing
  where the body came from, so it slides along; a sliding object that hits it
  is stopped there and bounces on that axis like a wall. Random spots are
  redrawn (up to 8 draws) until they are 0.3 m clear of every grown table. A
  candidate escape spot or door whose straight route (swept by a person's
  radius) meets a table scores 3 m worse. Tables push people away like walls.
- **Burning things.** Phase 9, after the objects move (`FlammablesSystem`;
  boxes and chairs in ascending ID order, then tables). A burning person
  within 50 mm of an intact thing sets it alight. Each intact thing whose edge
  is within 500 mm of a burning cell (earliest-lit wins) or a burning thing
  gains one tick of heat; at 75 (box), 150 (chair) or 250 (table) it logs
  `ObjectCaughtFire` (parent: that cell or thing; duration drawn: box 400–750,
  chair 600–900, table 1,000–1,500 ticks). Heat never cools. Each burning
  thing: at its end tick logs `ObjectBurntOut` and is `Burnt`; otherwise,
  if it is not moving and has stayed in one grid cell for 50 ticks, it lights
  that cell (`FireSpread`, parent: the thing's catch), and it sets alight
  every person within 50 mm of its edge (`AgentCaughtFire`, parent: the
  thing's catch).
- **Items.** A held item leaves the floor: it follows 20 mm in front of its
  carrier after movement and takes part in no collisions. A calm person's
  pick-up and set-down are not logged (like a calm push). Letting go while
  startled, scared, down or burning logs `ItemDropped` or `ItemThrown`
  (source: the person; target: the item; parent: their burning, fall, scare
  or alert), placing the item on the first clear spot around them (ahead,
  then ±45°, ±90°, ±135°, behind); with no clear spot they hold on for now. A
  runner's contact with an item they can lift, at strength 6+, is resolved as
  a hurl: `ItemThrown` (parent: their `AgentScared`), velocity sideways (a
  seeded side) or at the nearest person within 4 m for evil 7+, and the
  runner's speed halves. Throw speed is `60 × (strength + 5) ÷ (kg + 5)`
  mm/tick, 20 at least and 120 at most. A thrown item's momentum counts ×3
  on its first hit on a person.
- **Physical objects.** After collisions, each moving box in ascending ID order
  slides by its velocity, stops touching the first person or box in its way
  (found by an integer halving search along its path), bounces, then loses
  speed to friction. A runner blocked by a box at 50 mm/tick or more logs
  `BoxBumped` (parent: `AgentScared`), pushes the box and may trip over it
  (`AgentTripped`, parent: the bump). Calm pushes are not logged. A box that
  hits someone hard enough logs `BoxHitAgent` (parent: the bump that set it
  moving) and staggers or trips them; a hard box-on-box hit logs
  `BoxesCollided`.

- **Shoving.** In the same place a collision is recorded, when a fleeing,
  upright runner's straight step is blocked by an upright person and the closing
  speed is *below* their bump speed: if their evil is 7+ and `Intent.NextShoveTick`
  has passed, a shove is recorded instead of a side-step, and the cooldown is set
  40 ticks ahead. Resolved with the collisions, in the order recorded:
  `AgentShoved` (source the shover, target the person shoved, parent the shover's
  `AgentScared`, strength the push distance), then the victim is slid 350 mm along
  the line away from the shover — through the same clamp a jet of water uses, so
  they cannot be pushed into a wall, a table, a person or a thing — and is knocked
  down if the shover's strength exceeds theirs by 2 or more, and staggered
  otherwise. A calm victim is alarmed (`Bumped`); it makes a thud. A shove needs
  no speed, so it is what happens in a doorway queue.
- **Starting possessions.** `AgentDefinition.CarriedObjectId` names a
  thing somebody walks in holding. Bound after the objects exist and before the
  first tick, drawing no random numbers, so the start-up draw order is unchanged.
  The thing is marked `Carry.OwnsIt`, which exempts it from the calm put-down and
  from being tidied; a scenario is refused if the thing does not exist, is too
  heavy for its owner, is an extinguisher, or is given to two people.
- **The primal throw.** The direction a frightened person flings what they were
  holding is their own heading plus a seeded ±60°, except that evil 7+ aim at the
  nearest person within the existing aim range. Nothing else about letting go
  changed: the very nervous still drop rather than throw.
- **Fire alarms.** One authored alarm per room (`AlarmDefinition`).
  `AlarmBehaviour.Decide` sits in the panic chain after helping and before the
  door work, so somebody with an unconscious person in front of them sees to them
  rather than walking off to the bell. A scared, upright, empty-handed person not
  in danger, with bravery 6+, leadership 6+ or compassion 6+, and an unpulled
  station in their own room within 4 m, walks to it (giving up if blocked for
  12 ticks, if somebody else rings first, or after 8 s) and presses it for 20
  ticks. `AlarmPulled` (source the person, target the station, parent their
  `AgentScared`) is followed by one `AlarmRang` per live bell, in bell order
  (source the sounder), each emitting its own noise through the ordinary sound
  path with a 14 m hearing and alarm reach -- so everybody calm in the building
  is alerted, with `AgentAlertSource.Alarm` -- and each bell rings again every
  `AlarmSettings.RepeatTicks` give or take, on its own beat. A sounder is a
  `PhysicsObjectKind.AlarmSounder`, and one that is no longer `Intact` is
  silent. A floor with no sounders rings from its pull stations.
- **A bell frightens.** Being alerted by a bell becomes being scared when the
  reaction delay is up, whether or not any threat is active
  (`PerceptionSystem.Update` gates only seeing and hearing the threat on there
  being one). There is no composure any more: everybody flees at their panic
  pace with their own swerve and hesitation, and the temperament they were
  dealt decides whether they freeze.
- **Swing doors.** A `DoorDefinition` with `Swings` starts `Open` and is never
  shut, locked, battered or clicked (a click does nothing and costs nothing).
  `WorldGeometry.FireCanCross` refuses it until it is `Broken` or
  `DoorRuntime.Obstructed`; `DoorSystem.ScorchInTheFire` burns it through in
  `ExitSettings.SwingDoorBurnThroughTicks`, and skips it while it is propped.
  Something wedged into it counts as an opening for the fire on both sides.
- **The key.** `PlayerCommandType.ToggleLock`, a root event: `DoorLocked` with
  the door as source and target (the story says "you locked door N"), or
  `DoorUnlocked` as a click's unlock. Priced by
  `InfluenceSystem.CostOfLockToggle`: the exit's unlock price when the door
  leads outside, else the inside price; an open door pays close plus lock and
  is refused for nothing if the doorway is not clear.
- **Groups.** `PowerStickTogether`, one per person caught (target the person,
  root), written only once two or more are known to be caught. `GroupSystem`
  keeps a group id per person, a pull toward the others' middle scaled by
  cohesion and faded near them, a pace below a hundred percent for whoever has
  the group behind, the anchor's door as a bonus in `DoorBehaviour.ChooseExitDoor`,
  and `WayfindingSystem.Share` between members every ~100 ticks. Each member's
  pull begins at their own reaction tick. Nobody in danger is pulled.
- **Breaking.** Each kind has a `BreakMomentum` in kilograms times millimetres
  per tick; 0 never breaks. An object-on-object hit computes the striker's
  momentum and smashes the thing struck if it is over that kind's figure:
  `ObjectBroke` (source the thing that broke, target what hit it), and the thing
  becomes three-quarters its size, half its mass, and `Wrecked` — still solid,
  still trippable, no longer a chair anybody can sit on. A moving thing that is
  stopped by a table tests its momentum against `TableBreakMomentum` along the
  path it *intended* to take, not the clamped one, and on breaking the table is
  flagged in `WorldGeometry`: `TableAt`, `RouteCrossesTable`, `PushOutOfTables`,
  `AddWallRepulsion` and the random-spot redraw all skip it from then on.
- **Popping.** A kind with a `PopRadiusMillimetres` above zero goes off the
  instant it catches fire, in place of burning: `ObjectExploded` (parent its
  `ObjectCaughtFire`), then, in a fixed order, a bang through the sound system
  (six times the radius heard, three times alarming), every loose thing inside the
  radius flung away from it in ascending ID order, every upright person inside it
  shoved back and floored in ascending ID order, up to `PopIgniteCells` floor
  squares inside it lit row by row, and the thing itself wrecked. Nothing is lit
  before the run's fire has started.
- **Wedged doorways.** `WorldGeometry.IsObjectInDoorway` is true for a thing in
  front of a door's gap whose distance from the wall line is within its own radius
  plus 150 mm, measured on either side. `DoorSystem.ResolveBlockages` runs at the
  end of the tick, after the objects have finished moving: doors in ascending
  index, and within a door the lowest-numbered thing wins. Decisions in the next
  tick therefore read last tick's answer. A blockage appearing logs `DoorBlocked`
  (source the thing, target the door, parent whatever last set the thing moving)
  and clearing logs `DoorUnblocked`. An obstructed door does not open and does not
  shut: `IsDoorwayClear` and `Open` both refuse. A runner at one rattles it as
  they would a locked door; strength 7+ instead spends 40 ticks on
  `ShovingObstruction` and then heaves the thing along the wall, to the side they
  are standing, at 30 + 8 per strength point mm/tick (`AgentShovedObstruction`).
- **Barricading.** `BarricadeBehaviour.Decide` sits in the panic chain after the
  alarm step. Somebody upright, empty-handed, not in danger, in a room that is not
  alight, with nervousness 7+ or evil 7+, picks a shut door of that room that
  nothing is wedged in and nobody else is wedging — preferring one with fire
  beyond it — then the nearest liftable, intact, still thing in the room within
  5 m. They walk straight at it (no steering, as all fetching does), pick it up,
  carry it to a standing spot back from the gap by their own width plus the
  thing's, and set it down dead centre of the gap 80 mm short of the wall line:
  `AgentBarricadedDoor` (target the door). They abandon it if the fire closes in,
  if they are knocked about, if the door opens or is already wedged, if their room
  catches, after 60 ticks of getting nowhere, or after 10 s. `LetGoIfNeeded`
  exempts a barricade carrier, or they would fling the thing away the tick after
  picking it up. All the timings are fixed rather than seeded, so this adds no
  randomness of its own. The kind never seal a door with somebody beyond it, and
  nobody seals their own chosen way out unless the room beyond it is alight.
- **Influence.** Starts at 30. A card that actually does something spends 30.
  Everything in the tick's new causal events is priced by how much of a commotion
  it is and credited to the meter, capped at 100; deaths are excluded, and so are
  the player's own cards and fire spreading square by square. Every person whose
  outcome becomes `Escaped` credits 15. All of it is counted at the end of the
  tick rather than reported by the behaviours, so nothing in the simulation has
  to know the purse exists. Influence is not an event, for the same reason
  sitting is not; each card's event records its cost in `Strength`.
- **The hand.** One card at the start, drawn from the deck. Each person whose outcome becomes `Lost`
  deals one card, drawn from `DeckSystem`'s own PCG32 stream (`initseq` 55, from
  the scenario seed) so that dealing never shifts the crowd's randomness. The
  dead are walked in ascending crowd order, so a replay deals the same cards in
  the same order. Cards backed by a finite supply leave the deck once it is gone.
  `CardDealt` names the death as its causal parent and carries the card in
  `Strength`. A card must be in hand to play, and leaves the hand only when it is
  paid for.
- **The cards.** All are root events, because the player is the cause. The five
  trait cards are aimed at a place: everybody participating within 1500 mm — an
  exact round test, because the spatial index gathers a box — has one trait set
  to the end of its scale, in ascending crowd order so the log is replay-stable.
  Traits are read when used and never cached, so every rule picks the change up
  on the next tick. A throw counts as played, and so is paid for and discarded,
  only if it moved somebody's dial: one that catches nobody, or only people
  already at that end, does nothing and is free.
  `SpawnFire` needs a floor square that is in a room, unlit and not wet, asked
  before anything is written down because the log is append-only; the square it
  lights names the card as its cause, and if the scenario's fire has not started
  the card's square becomes `FireActivated`. `SpawnExtinguisher` takes the
  lowest-numbered spare bottle and stands it on clear floor with full fuel.
  `BlastWall` is below.
- **Blast holes.** The scenario reserves four spare openings, appended after the
  authored doors and flagged `IsHole` and not `Placed`. Everything that loops over
  doors stops at the placed count, including four loops inside `WorldGeometry`
  that would otherwise read an unplaced slot as a door leading outside — and
  therefore score it and draw a random number for it, silently changing every
  recorded run. `TryPlaceHole` picks the nearest wall line within 0.9 m of the
  clicked point, in integers, with ties going to the lowest room index and then
  the wall order; clamps the gap into the wall with a body's width of wall at each
  end; refuses a gap that comes within 0.5 m of any opening in the same wall line,
  whichever room owns it, or that would open half into the next room and half into
  solid wall. Placing one writes the slot's room, side, centre and width, sets it
  `Broken`, and rebuilds the only two things cached per door — `doorNeighbour` and
  the per-room door lists — through the same method the constructor uses, because
  the order doors appear in per room is part of the replay contract. Nothing else
  is rebuilt: the fire grid is derived from rooms, not doors. `PowerBlastedWall`
  becomes the hole's `OpenedEventId`, so an escape through it names the blast as
  its cause. Then the bang (20 m heard, 12 m alarming), the loose things within
  2.5 m flung at 90 mm/tick, and the people within 1.5 m shoved back 600 mm and
  floored. No fire.

### Prototype 3, second batch (2026-09-26)

- **The Director's ladder** (`DirectorSystem`, `DirectorSettings`; on only when
  the level says so -- the office does -- so a test building starts its fire
  the way it always did). At start-up it draws which of the meeting room's bins
  (only if there is more than one) and when it catches, 1500–4500 ticks; the
  fire is told not to light its own square (`LeaveTheStartToTheDirector`). On
  that tick, or on the player's trigger, `FireSystem.StartWithoutFlames` makes
  the fire exist without a square alight (`FireActivated` at the bin),
  `DirectorStartedIncident` is written and the bin is set alight
  (`FlammablesSystem.IgniteObject`). Each tick it watches: anything burning
  in a room the incident does not own -- floor or thing -- is
  `FireEscapedItsRoom` and ends the ladder. A bin owns its own room; a socket
  or the fuse box also owns every room its own bang and spark set alight in
  the first `BangSettlesTicks` (250) after it went, so the fuse box taking
  every socket is one incident, not a fire got loose. Nothing burning at all
  (floor, things or people) is `IncidentPutOut`, followed by the all-clear 500
  ticks jittered later (`AlarmSystem.Silence`, `AllClear`) and the next rung
  1000–2000 ticks later. The all-clear stands while nothing burns: a bell
  pulled again is silenced again, 500 ticks jittered after it started.
  The socket is chosen among whole sockets outside the last incident's room by
  the most calm participating people in its room, drawing only on a tie; with
  none left it is the fuse box. It crackles for 250 ticks (`SocketCrackling`,
  a crash heard 5 m that frightens nobody) and is popped
  (`PowerSystem.PopSocket` / `PopTheFuseBox`); its room becomes the new start
  room. `HasSomethingComing` holds the round's stall clock open. The traps are
  armed by `EscapedEventId` rather than by the fire being lit.
- **A young fire spreads slowly** when the Director started it: while fewer
  than 12 squares burn, each spread wait is three times as long (the same one
  draw). A waste bin burns 1000–1500 ticks and sets its floor square alight
  after resting about 500 ticks (`ObjectKindSettings.FloorIgniteRestTicks`,
  jittered when it catches).
- **Danger is danger.** `Threats` holds the fire, then `BurningThingsThreat`
  (everything alight in `FlammablesSystem`: seen, heard at the fire's base
  reach, nearest point its edge, in a room, near a route) and then
  `BurningPeopleThreat` (whoever is alight, read once a tick in phase 2: seen
  and fled from, but never "in a room", "on a route" or "danger right beside
  me", so the kind can still reach them with a bottle, and never a danger to
  themselves). Touching and harm stay with the flammables and the burning
  person. The extinguisher aims at the nearer of a burning square or a burning
  thing, and fights when only things burn.
- **Calming down** (`FearSystem.Settle`, after perception, for the frightened
  only). Every `CheckEveryTicks` on each person's own beat, a danger in their
  room, in sight or in earshot (half as far through a shut door) refreshes
  their fear, as do a bell or a bang reaching them (`SoundSystem.Emit`) and
  being alight. After their own quiet spell (250 ticks jittered, drawn when they
  took fright) fear drains at `40 + 8 × bravery − 6 × nervousness` per mille a
  second, at least 10, down to `120 × (nervousness − 5)`; below 400 they settle
  a reaction lag later on a tick nobody else settles on, once they are only
  running, dithering, frozen or standing. Settling writes `AgentCalmedDown`,
  clears the escape, makes them rattled (3000 ticks if they saw the danger,
  1000 if not, jittered) and hands them the GoHome cue unless their desk's room is
  alight. Rattled, a noise within half its hearing reach alarms them outright,
  and the brave need three more bravery to look before they run. Frightened
  people stop shouting once their quiet spell is over.
- **Influence** (`InfluenceSystem`, `InfluenceSettings`; commands
  `InfluenceDoor`, `InfluenceThing`, `InfluenceSpot`, free). A place is a door,
  a thing's spot or a floor point; a click within 1 m of a floor or thing place
  (or on the same door or thing) adds a step up to 20, and a place loses a step
  every 100 ticks since its last click. Felt per mille: `level/20 × (1 −
  distance/12 m) × susceptibility`, only in the place's room (both rooms for a
  door), susceptibility `100 + 12 × (nervousness − 5) − 12 × max(0, leadership
  − 5) − 12 × max(0, evil − 5)`, +50 for a visitor, clamped to 10–200 %. Worth
  `felt × 6000 / 1000` mm to a door choice (the door itself) and, through the
  sign's agreement arithmetic, to spots its way. `DoorBehaviour.ChooseExitDoor`
  adds it except through the heat, into a burning room or back into
  `PreviousRoom`, and also scores the ways out through every influenced door on
  this room's wall with the noise fixed at half its range; a choice it changed
  writes `AgentDrawnByInfluence`. Calm people's `ChooseActivity` wanders to the
  strongest pull felt with a chance of the pull per mille (drawn only when one
  is felt); the easily led seated or on an errand weigh it once a second at a
  tenth of that. Weighing a pull nobody feels draws no random numbers.
- **Nudge from a point** (`NudgePersonFrom`): the lurch, 300 mm, is away from
  the point; annoyed (`AgentAnnoyed`), they stay so for 1000 ticks jittered,
  during which a nudge is written down and does nothing else.
- **The heap is seen.** A frightened person choosing a door treats a doorway
  heaped with fallen boxes on a wall of their room, within sight, as found shut
  -- unless they are strong enough to heave it, in which case they get
  `StrongGiveUpOnAHeapTicks` (400 jittered) at it first, counted from when
  they come within `StrongTryTheHeapWithinMillimetres` (3 m) of it.

## Causal events and presentation

The simulation keeps `FireActivated`, `FireSpread`, `AgentAlerted`,
`AgentYelled`, `AgentScared`, `AgentLost`, `AgentNoticedSound`,
`AgentsCollided`, `AgentKnockedDown`, `AgentTripped`, `AgentGotUp`,
`AgentFroze`, `AgentUnfroze`, `DoorUnlocked`, `DoorOpened`, `AgentTriedDoor`,
`AgentForcedDoor`, `AgentGaveUpOnDoor`, `AgentEscaped`, `BoxBumped`,
`BoxHitAgent`, `BoxesCollided`, `AgentPassedOut`, `AgentCameTo`,
`DoorBrokenDown`, `AgentCaughtFire`, `ObjectCaughtFire`, `ObjectBurntOut`,
`ItemThrown`, `ItemDropped`, `DoorClosed`, `DoorLocked`, `AgentShookAwake`,
`AgentGrabbed`, `AgentDropped`, `AgentRescued`, `AgentShoved`, `AlarmPulled`,
`AlarmRang`, `ObjectBroke`, `ObjectExploded`, `DoorBlocked`, `DoorUnblocked`,
`AgentBarricadedDoor`, `AgentShovedObstruction`, `PowerBeefcake`,
`PowerSpawnedFire`, `PowerSpawnedExtinguisher` and `PowerBlastedWall` events.
Event types are only ever appended, never inserted, because a type's number is
part of the replay fingerprint. Events that affect someone or
something name it as their target: `AgentsCollided` the person run into,
`BoxBumped` the box, `BoxHitAgent` the person hit, `BoxesCollided` the other box,
and `AgentTriedDoor`, `AgentForcedDoor`, `AgentGaveUpOnDoor`, `DoorBrokenDown`
and `AgentEscaped` the door; `AgentShoved` the person shoved, `AlarmPulled` the
alarm, `ObjectBroke` what hit the thing that broke, `DoorBlocked` and
`DoorUnblocked` the door jammed, `AgentBarricadedDoor` the door wedged,
`AgentShovedObstruction` the thing heaved aside, `PowerBeefcake` the person made
strong, and `PowerSpawnedExtinguisher` and `PowerBlastedWall` the bottle and the
hole. Every event except `FireActivated`, the player's `DoorUnlocked` and the
four cards (the player is the cause of those) has a causal parent (a box set moving by a calm
person's unlogged push is the one rare exception). The room, the building's
outside, the camera the player drives, capsules, fire cubes, vision-cone
outlines, icons, floor ripples, the counter and the round's own cards and
buttons are observational presentation. They map logical millimetres to Unity
metres and never write simulation state. The round itself is the exception
that proves the rule: deciding that a round is over turns the last survivors
into people who were saved, which is an outcome, so it lives in the simulation
(`RoundSystem`) and the screens only read the result off the snapshot.

A small panel at the bottom left **names every mark**, so the crowd can be read
without being told what the symbols are.

**Icons** sit on a per-person anchor that always faces the camera, so they
never spin with the body or tip over when it falls:
- a red `!` that pops in and fades over 1.3 s on every `AgentAlerted` and
  `AgentNoticedSound`;
- three cyan sound-wave arcs, beside the head on the side the person faces,
  appearing from the inside out on every `AgentYelled`;
- an ice-blue snowflake that turns slowly while the person is `Frozen`;
- three little yellow stars chasing each other round the head while
  `Unconscious`;
- a yellow `?` while `Investigating`, and `...` while standing or glancing
  around.

A ring grows across the floor to the sound's reach and fades over half a
second for every yell, collision, trip, door shove and hard box hit.

Walls are built from the scenario's room and split around each door. A door
leaf is hinged at one side of its gap and swings 90° over 0.3 s when the door
opens, away from whoever pushed it — outward for the player, who is not standing
anywhere. Each door also carries a setting for which ways its leaf may swing,
both ways for now, so a later scene can author a one-way fire door; a strip of darker ground outside shows the doorway. A person
shoving a door lunges at it on each shove. Boxes are brown cubes, 0.75 as tall
as they are wide, that hop and tip a little when hit. With physics, everything
is drawn at the size of its real physical shape. (Before physics, square things
were drawn smaller to fit inside the round footprint the simulation kept clear,
because their corners stuck out past it.) The door leaves are the
only objects with colliders, used only to work out which door was clicked.

The runner keeps the latest and previous snapshots. The display blends between
them by how far the current frame is between ticks, so movement is smooth at
any frame rate. Capsules bob with each stride (more when sprinting), lean
forward with speed, and lie flat when lost. Knocked-down or tripped people
tip forward onto the floor in their normal colour and tilt back up while
getting up; staggering people wobble; frozen people are tinted pale blue and
tremble on the spot. Fire-cube variation comes from a
hash of the cell's grid position, never from the simulation's random
generator. The cubes (and people, doors and boxes) share materials, recoloured per object through
a `MaterialPropertyBlock`. The camera is 45 degrees around the room and
35.264 degrees above the ground, which gives a standard isometric view.

**Seeing through smoke.** The puff of smoke a bang throws carries the same mark
a wall does, so anybody behind it shows through it as a pale silhouette instead
of disappearing into the cloud; the smoke and the sparks also show through walls
the way the flames do. Today the only smoke is the puff from an explosion, so
this is a small thing on screen until there is more of it.

**Seeing through walls.** People and the objects they knock about are drawn
twice: normally, and again as a pale blue silhouette wherever a wall or a door
stands between them and the camera, so the crowd in the meeting room, the
corridor and the closet can be watched without moving the camera. Fire gets the
same treatment in orange, so a blaze in the next room is visible through the
wall. The second drawing uses `Content/Rendering/SeeThrough.shader`, added as an
extra material on each renderer. Walls and door leaves carry
`Content/Rendering/WallMark.shader`, which paints nothing and only marks where
they are the nearest thing to the camera; the silhouette draws on that mark
alone, so nothing ghosts through a table, and nothing ghosts through itself.
Neither needs project setup. This is rendering only; the simulation neither
knows nor cares.

**Bangs you can see.** A laptop battery, a wall socket, a microwave or a stick
of TNT going off throws a flash that lights the room, sparks that arc out and
fall to the floor, a puff of smoke that swells and thins, and a jolt of the
camera — all sized to how big the blast is, so a laptop cracks and TNT booms.
Display only: the scatter of the sparks comes from the event, never from the
dice.

**What the new things look like.** A pull station is a small red box at hand
height with a white bar across it; a fire alarm bell is a red plate with a dome
high on the wall, which flashes twice a second once the bells are ringing and
goes dark once the flames have popped it, and a red FIRE ALARM band runs across
the very top of the screen on the same beat (2026-09-25). Swing doors are two
plain wooden leaves that meet in the middle, fly open ahead of whoever runs
through, flap back past shut and settle. Everybody a "Stick together" throw
bound wears a thin coloured band round the ankles, one colour per group. A
briefcase is a
flat slab on its edge with a handle; a microwave is a boxy appliance with a dark
door; a wall socket is a small pale plate that never moves. A smashed thing
squashes to a third of its height and tilts; a collapsed table drops to a flat
heap. A spare extinguisher the player has not put down is not drawn at all. A
blast hole cuts back the wall pieces it crosses — splitting one in two where the
hole is in the middle of it — and leaves five lumps of rubble in the gap, laid out
from the hole's own ID so it looks the same every run without touching the
simulation's randomness; a hole leading outside gets the same strip of outside
ground a door does. Explosions and blasts throw a large floor ring, and a shove
makes the shover lunge. The office's later things (2026-09-24): a vending machine is a tall dark red cabinet with a pale glass front; a filing cabinet a grey-blue box with drawer lines; shelves a wooden frame with boards and blocks of books; the copier a big pale box on a castor strip; a whiteboard a white slab on a post over a wheeled base; a standing lamp a dark base, a thin post and a cream shade, and the shade that drops off it is a loose cream drum of its own; a robot vacuum a squat dark disc with a little light on top, which is drawn wherever the physics has it as it trundles about.

**The player's own controls** are the hand along the bottom, the influence
bar above it, and the line above that saying what a click will do. Since
2026-09-24 the hand is drawn as portrait cards, 96 × 132 pixels, each with
its name, a line on what it does and its price; a card the player cannot
afford is dimmed red with the shortfall written on it, and the one in hand
lifts and turns blue. Since 2026-09-25 each card is a button and two of a kind
are one card with the count in a badge. A click on a door is told from a hold
by whether the button comes back up inside a third of a second (`DoorClicks`,
plain arithmetic with no scene in it); since 2026-09-26 there is no double
click to wait for. Every card and button
claims its patch of screen as it is drawn (`HudHitTest`), so a click on one
never also reaches the floor behind it. Clicking a place is worked out against
the mathematical ground plane, so no collider is needed for it; door leaves
and the pull stations are the only colliders in the scene.

This prototype deliberately remains ordinary GameObjects and C# code. The next
stone is chosen by the owner after playing it. Profile a standalone build
before adding scale tooling.
