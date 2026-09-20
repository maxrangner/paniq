# Fire-reaction prototype

**Status:** current prototype. This scene is where prototype stones are laid
one at a time (see the [prototype roadmap](roadmap.md)). So far it shows that
agents can move deterministically in a life-like way, react to a spreading
hazard, and leave an explainable causal event trail.

## Experience

The `FireReactionPrototype` scene shows one abstract 12 m by 12 m room with a
door in each wall, three wooden tables with eight chairs pulled up to them,
eight cardboard boxes on the floor, and ten people (capsules).

**Everyone has a personality.** Each person has six traits from 0 to 10:
strength, speed, bravery, compassion, evil and nervousness. 5 is an ordinary
person. A number floats beside each head; press **Tab** for a table of
everyone's traits, how they will panic, and what they are doing now. The ten
people are authored as a cast (a scenario can also leave traits out and let the
seed draw them):

| # | Who | Str | Spd | Brv | Cmp | Evl | Nrv |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | ordinary | 5 | 5 | 5 | 5 | 2 | 5 |
| 2 | the brute | 9 | 6 | 6 | 3 | 6 | 3 |
| 3 | the hero | 8 | 6 | 8 | 8 | 1 | 3 |
| 4 | the saint | 4 | 4 | 7 | 9 | 0 | 4 |
| 5 | the villain | 6 | 6 | 5 | 1 | 8 | 4 |
| 6 | the nervous wreck | 3 | 5 | 1 | 5 | 2 | 10 |
| 7 | the sprinter | 5 | 10 | 5 | 5 | 3 | 6 |
| 8 | the bully | 7 | 5 | 4 | 2 | 9 | 5 |
| 9 | the coward | 3 | 4 | 2 | 4 | 3 | 8 |
| 10 | ordinary | 5 | 5 | 5 | 6 | 3 | 5 |

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
  them; **evil** does the opposite and barges straight through.

**Before the fire, people loiter.** Each person makes their own small decisions
every few seconds. They stroll to a spot on a gently curving path, stop, glance
around, or wander over to stand about a metre from someone as if chatting.
They turn and speed up gradually, keep personal space, and steer away from
walls before reaching them. Each person has their own seeded walking pace
(set by their speed trait) and turning speed, so no two move alike.

**After five seconds a fire starts** at a seeded spot near the middle. The
floor is a grid of 0.5 m squares. Each burning square shows a dim glowing tile
and a cluster of two or three small cubes that bob, spin, flicker and cycle
between red, orange and yellow. New squares pop in with a small overshoot, and
old squares slowly fade to deep-red embers. The fire only spreads from squares
that are already burning, into an irregular blob, and fills the room in about
40–45 seconds.

**People notice things.** A big red `!` pops up over anyone who notices
something. Someone who sees the fire stops, turns to face it and yells: three
sound-wave arcs appear beside their head, and a faint ring spreads across the
floor showing how far the yell carries. Calm people within 2.5 m understand
the yell and are alarmed; calm people up to 6 m away only hear it and turn to
look, with a yellow `?`. Fire crackles too: someone within 3.5 m with their
back to it turns around, and if it is still out of sight they edge toward it
until they see it.

**People panic in different ways.** In a room of ten, the most fearful are dealt:
- five **runners**, who sprint at 3.5–5 m/s and shout every 2–5 s;
- three who **freeze** with a snowflake over their head, trembling, for 2–6 s,
  then snap out of it and run (at once if the fire gets within 1.5 m); and
- two who **freeze for good** and never move again.

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

**Doors.** Each wall has a 1 m door, set off-centre. Every door starts locked
and is drawn red. The player clicks a door once to unlock it (it turns green),
again to open it (it swings outward), and again to close it (it stays
unlocked); a door with someone standing in the doorway cannot be closed.
Clicking a broken door does nothing.
Hovering over a door brightens it and the top-left text says what a click will
do.

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

Someone who walks 0.8 m out through an open door has **escaped**: they keep
walking for a moment and shrink out of view. People stuck in a crowd on the
way to a door try a different one for a few seconds. Someone wedged right
beside an open door, not lined up with the gap, steps aside against the wall
for half a second to a second so whoever is lined up can go first, instead of
two people jamming the doorway shoulder to shoulder.

**The building is rooms joined by doors.** The 12 × 12 m open-plan office is
where the fire starts. Behind its east wall are a 2 × 2 m storage closet and a
3 m corridor; the corridor leads to a second 12 × 12 m room, the meeting room.
Ten people start in the office and ten in the meeting room, where they cannot
see the fire and only learn of it from the shouting. The five doors in the
outside walls are the player's: they start locked. The three inside doors (the
closet, and the corridor at each end) start shut but unlocked, so people open
them themselves.

People try to save themselves wherever they can. They pick a way **out of the
building** — scored by the whole walk there, including crossing the last room —
and head for the first door on that walk, room by room. Nobody walks into a
room that is alight, or across one to reach a door on its far side. Only
once every way out has been tried and would not open do they make for whichever
room is furthest from the flames instead — but not into a room that already
holds as many people as there is floor for (about one person per square
metre, so four in the closet). The counter shows how many are in a room with
no fire in it. Being in a room is not escaping.

The fire can only get from one room to the next through an open (or broken)
door; walls stop it, and nobody sees fire through a wall. A closed door
muffles noises to half their reach.

**Closing doors behind them.** People close doors too, by personality, at
two moments: the door they just walked through (on the way out of the
building, or from one room into the next), and a door within reach of them
with fire in the room beyond it. The evil (7+) shut it and lock it even with someone
running up behind; only a body in the doorway stops them. The compassionate
(7+) never shut it on someone within 3 m, and while the fire is still more
than 5 m from the door they leave it open for stragglers. Otherwise, with
nobody within 3 m, the nervous (8+) shut it, and so do the brave and kind
(bravery + compassion 12+) once fire is within 5 m of the door. Anyone
in a room with no fire in it shuts a door with flames within 2 m beyond it. A closed door can be
opened again by anyone who reaches it (unless it was locked) or by the player.

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

**Tables and chairs.** Three 1.2 × 0.7 m tables stand in the room. Nobody
and nothing can pass through a table: people slide along its edge as they
would along a wall and steer away from it, loose objects bounce off it, and
random spots people pick to stroll to or run for stay clear of tables.
Runners also avoid spots and doors whose straight route runs into a table.
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

**Boxes.** Eight cardboard boxes, 0.3–0.6 m wide and 3–20 kg, sit on the floor.
Calm people walk around them. Runners barely look: they kick a box sliding
across the floor, and at running speed they may trip over it instead, more
often the faster they go and the bigger the box. A kicked box slides about a
metre, spins if it was hit off-centre, and bounces off walls, other boxes and
people. A heavy, fast box can knock someone off balance or trip them.

**People catch fire.** A person touched by the fire does not drop dead: they
burst into flames (an orange, flickering, flailing capsule with little flame
cubes licking up it), scream every half second or so, and run around wildly
at full sprint, lurching in a new direction every 0.2–0.5 s and paying no
attention to doors or other people, for 3–6 s. Then they collapse, lost, as
a dark red capsule. Anyone they run into catches fire too, and anyone within a
hand's breadth (10 cm) of them has a 1-in-5 chance each tick. Someone on the
floor when they catch fire burns where they lie, and people lying on the floor
can be caught by the fire too. Nobody on fire can escape through a door. With
every door locked, everyone who does not get out is eventually caught (about
40 seconds after the fire starts with the default seed). There is no score,
restart control, or end screen in this checkpoint.

## Deterministic rules

- The simulation runs at 50 logical ticks per second through one
  `FireReactionRunner.FixedUpdate` entry point. It keeps the contract's order:
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
  90-degree vision cone with a 3 m range around their current heading. A calm
  agent becomes alert when the nearest point, centre, or a corner of any
  burning cell lies inside that cone. It yells and receives a seeded 0–20 tick
  reaction delay before panicking. Agents alerted by a yell or a bump receive
  their own delay without pretending they have seen the fire, and turn toward
  where the noise came from; they can promote to a visual alert when the fire
  enters their cone. The `AgentScared` event's parent is the agent's own alert.
- **Sound.** A sound is data: a position, a hearing reach, an optional alarm
  reach, and the event that made it. It is delivered at once to every other
  calm participating agent in ascending ID order. Inside the alarm reach the
  listener becomes alert (`Yell`); inside the hearing reach it starts
  `Investigating` and logs `AgentNoticedSound`, whose parent is the sound's
  event. Yells reach 6 m (alarm 2.5 m); collision and trip thuds reach 3 m
  (no alarm). Fire is a continuous sound: each tick a calm, non-investigating
  agent that cannot see the fire but is within 3.5 m of a burning cell notices
  it, with that cell's ignition event as parent. Investigating lasts a seeded
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
- **Player commands.** A door click is a `ClickDoor` command for the next tick,
  consumed at the start of that tick in queue order. Locked → unlocked logs
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
  [technical decisions](technical-decisions.md)) and targets a point 0.6 m
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
  are fighting, and holds the trigger down. The jet is a 3 m, 30° cone: it
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
  nearest free chair in their room within 6 m and sit on it for 5–20 s. A
  chair with someone on it does not slide, cannot be picked up and cannot be
  tidied away. Anyone startled, knocked over or set alight in a chair has to
  get out of it first: 0.8 s less 0.04 s per point of nervousness, and never
  less than 0.2 s, so the nervous are out of it first. Standing up steps them
  clear of the seat and shoves the chair the other way.
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

## Causal events and presentation

The simulation keeps `FireActivated`, `FireSpread`, `AgentAlerted`,
`AgentYelled`, `AgentScared`, `AgentLost`, `AgentNoticedSound`,
`AgentsCollided`, `AgentKnockedDown`, `AgentTripped`, `AgentGotUp`,
`AgentFroze`, `AgentUnfroze`, `DoorUnlocked`, `DoorOpened`, `AgentTriedDoor`,
`AgentForcedDoor`, `AgentGaveUpOnDoor`, `AgentEscaped`, `BoxBumped`,
`BoxHitAgent`, `BoxesCollided`, `AgentPassedOut`, `AgentCameTo`,
`DoorBrokenDown`, `AgentCaughtFire`, `ObjectCaughtFire`, `ObjectBurntOut`,
`ItemThrown`, `ItemDropped`, `DoorClosed`, `DoorLocked`, `AgentShookAwake`,
`AgentGrabbed`, `AgentDropped` and `AgentRescued` events. Events that affect someone or
something name it as their target: `AgentsCollided` the person run into,
`BoxBumped` the box, `BoxHitAgent` the person hit, `BoxesCollided` the other box,
and `AgentTriedDoor`, `AgentForcedDoor`, `AgentGaveUpOnDoor`, `DoorBrokenDown`
and `AgentEscaped` the door. Every event except `FireActivated`
and the player's `DoorUnlocked` has a causal parent (a box set moving by a calm
person's unlogged push is the one rare exception). The room, isometric camera, capsules, fire cubes, vision-cone
outlines, icons, floor ripples and the counter are observational
presentation. They map logical millimetres to Unity metres and never write
simulation state.

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
leaf is hinged at one side of its gap and swings 90° outward over 0.3 s when
the door opens; a strip of darker ground outside shows the doorway. A person
shoving a door lunges at it on each shove. Boxes are brown cubes, 0.75 as tall
as they are wide, that hop and tip a little when hit. The door leaves are the
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

**Seeing through walls.** People and the objects they knock about are drawn
twice: normally, and again as a pale blue silhouette wherever a wall stands
between them and the camera, so the crowd in the meeting room, the corridor
and the closet can be watched without moving the camera. The second drawing
uses `Content/Rendering/SeeThrough.shader`, added as an extra material on each
renderer; it needs no project setup. This is rendering only; the simulation
neither knows nor cares.

This prototype deliberately remains ordinary GameObjects and C# code. The next
stone is chosen by the owner after playing it. Profile a standalone build
before adding scale tooling.
