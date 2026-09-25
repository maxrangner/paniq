# Game vision

## Decided: core premise

Paniq is a darkly comedic, real-time crowd-survival game inspired by the
indirect-control lineage of *Lemmings*. The player influences an autonomous
crowd during a changing disaster and tries to save as many people as possible.
The scored result is the percentage of the crowd saved.

The player never selects a person and orders them to walk or perform a task.
They change **what a person is capable of** — how strong, how brave, how
frightened they are — and that person's own decision-making does the rest,
including the parts the player did not want.

Death is a central failure outcome. It is presented as exaggerated, dark comic
consequence rather than as realistic disaster drama.

## Decided: the game is half game, half toy

Paniq is intended to be about half a game with a score and half a toy worth
watching. A player who never intervenes at all should still enjoy watching the
crowd try to solve the disaster by itself.

What follows from that:

- **Levels are large.** Big enough that when something goes wrong in one wing,
  dozens of people elsewhere are still at their desks, chatting, sitting down
  and tidying up. Calm behaviour is always on screen somewhere.
- **Interaction is not only for score.** Causing trouble for its own amusement
  is legitimate play, in the way that ignoring the missions in a
  *Grand Theft Auto* game is legitimate play.
- **One mode.** There is no separate sandbox mode; the scored run is also the
  toy, because a large level supplies the calm half for free.

## Decided: the crowd is the hazard

The recurring spine of every Paniq scenario is that a disaster starts the
trouble and then the **crowd carries it**. People who are on fire set alight
everyone they touch. Whatever maddens or converts a person turns them on their
neighbours. The pool of people the player is trying to save is also the thing
killing them.

This is what makes the percentage saved dramatic rather than arithmetic: a
person the player fails to save does not merely reduce the score, they may
join the other side.

## Decided: three things the finished game must be

Stated by the owner on 2026-09-24, in these words, as the yardstick every
foundation decision is measured against. Where the foundation stands against
each is checked in [technical decisions](technical-decisions.md#alignment-with-the-three-requirements-for-the-finished-game-2026-09-24).

1. **Very optimised for large crowds, each person with a lot of emergent
   behaviour and events.** Large is a stated goal, not an open question: the
   working budget is two hundred to five hundred people on a floor of thirty
   to fifty rooms, and every system is measured against a panicking crowd of
   that size before it is trusted. Emergence is not decoration: every person
   decides for themselves from what they see, hear and touch, and every event
   names its cause, so a chain of consequences can be read back.
2. **Systems flexible for different dangers; a reaction is a feeling about a
   situation, never a response to a named event.** A danger is content that
   answers the crowd's questions (where is it, is it in my room, can I see it,
   is it on my way, is it touching me, what does touching do). People react to
   what those answers make them feel. Nothing in the crowd switches on an
   event's name. Fear is the feeling that exists today; the others that drive
   behaviour -- anger, trust in a leader, curiosity -- are named here so they
   are built as feelings when a danger or a card first needs them, not as
   special cases.
3. **Handcrafted levels, with dynamic scenery and props, possibly on more than
   one floor.** Levels are placed by hand and read into the run by a bake tool,
   never generated. Scenery is not a backdrop: props are physical things that
   are shoved, thrown, tipped, broken and burnt; tables move and the walkable
   floor follows them; walls are blown through; doors break. Storeys are
   prepared for now (every room and position may carry a storey number; a stair
   is a kind of door between storeys) and built when a level asks.

## Decided: how a disaster is described

Paniq does not commit to any particular disaster. Instead, every disaster is
built as a mix of four families:

| Family | What it does to the level | Example |
| --- | --- | --- |
| **Spreading** | grows outward from a place and makes ground unusable | fire, gas |
| **Rising** | shrinks the usable level and herds everyone into the same shrinking space | flood water |
| **Contagious** | turns the crowd itself into the threat | people on fire, the converted, the possessed |
| **Hunting** | one or a few intelligences that choose and chase targets | a monster, an intruder |

The fire prototype is *spreading* plus *contagious*. A flood is *rising* only.

**The test:** a proposed scenario that cannot be expressed as a mix of these is
the signal to extend the threat system, not to special-case the scenario.
Particular fictions are content; the four families are architecture.

## Decided: how the player acts

**Capability, never direction.** The player cannot tell anyone where to go, and
there is no rally point, no marker to walk to, no order to obey. What the
player does is turn a person's traits up or down, and then watch what that
person decides to do about it.

Every trait is both a tool and a joke, because the crowd's own rules supply the
consequence:

| Dial | What the player gets | What it also causes |
| --- | --- | --- |
| **Strength** | he batters the jammed door off its hinges and the room pours through | he floors three of them getting there |
| **Speed** | she reaches the stairwell before the crowd does | she hits the man in the doorway at full sprint and both go down |
| **Bravery** | he stops dithering, takes the extinguisher, fights the fire | he now walks *toward* the thing that is killing people |
| **Compassion** | she holds the door and drags the unconscious man out | she stops in a burning room to wake a frozen colleague, and two are lost instead of one |
| **Evil** | he heaves the wedged scrum apart and the queue unjams | by throwing somebody into a wall |
| **Nervousness** | he bolts at the first shout, which is how an evacuation starts early | and eleven people follow him into a dead end |
| **Leadership** | she gathers the room and moves them as a group | she leads all nine to the exit nobody knew was blocked |

Dials go **both ways**. Turning a man's compassion down saves him from running
back into a burning room for somebody already lost.

Alongside the dials, a small number of blunt **direct instruments** exist —
blowing a hole in a wall, starting a fire, putting an item down. These are
deliberately expensive, in the way that the most powerful weapon in *Worms* is
rarely the right answer. They are where mischief lives.

**Cards may break the capability rule**, rarely and at a price. A rule that is
never broken is a constraint; a rule broken twice in a level is drama.

## Decided: people can fight back, and there is combat

Individuals resist. A strong person who is grabbed sometimes throws the
attacker off; a brave one with a fire extinguisher knocks it over backwards.
Leaders can organise a group to hold a doorway. A crowd that only ever flees
gets monotonous, and resistance is what makes the trait dials matter.

**Combat has no health bars.** Damage is a ladder of visible states:

> upright → staggered → knocked down → out cold → dead

with *on fire* alongside it. Items do not deal damage numbers; they push people
down the ladder, and how far depends on the item's mass, the thrower's
strength and the impact. This stays readable when two hundred people are on
screen, which a bar over every head would not, and it keeps the tone
knockabout rather than statistical.

## Decided: what the player attends to

There are **no helper markers**. No arrows at the edge of the screen, no
minimap, no alerts pointing at trouble. Following events is the player's job,
and missing something is a real outcome.

The intended replacement is **sound**: yells, thuds, alarms, breaking glass and
screaming, carried from where they happen. A scream off to the left is what
turns the camera. This makes audio a navigation system rather than decoration,
and it is the single largest gap between what the simulation already knows and
what the player currently receives.

How the player drives the camera, and how the game is meant to look while they
do it, are in [look and controls](look-and-controls.md).

Because things will be missed, the **end screen carries the weight**: it
freezes the scene and can be clicked for the facts about any person. A
plain-language retelling of what happened out of sight is a later feature, and
an important one — it is how a player learns where to look next time.

## Decided: how a round ends and is scored

- A round ends when nobody is left to resolve: everyone is out, dead, or
  settled somewhere they are not going to leave.
- **Surviving inside counts as saved.** Barricading yourself into a storeroom
  is a legitimate way to live through a disaster, not an exploit.
- The score is the **percentage saved**, shown during the run and on the end
  screen.

## Decided: how a run is paced — the Director

A background system watches the run and adds or eases pressure, in the manner
of *Left 4 Dead*'s AI Director (the invisible system that decides when to send
a wave at the player). Too calm, and something else goes wrong in a far wing.
A massacre, and the pressure lets up.

The Director is **reactive**: it responds to how the run is actually going.

The accepted consequence is that two attempts at the same level are **not
directly comparable**, because the player's own competence changed what the
Director did. A score is a match result, not a personal best — nobody compares
Saturday's 3–1 with Tuesday's 2–0. Randomness is a core ingredient here, as it
is in a sport.

This raises the stakes on explainability. If runs cannot be compared, the only
way a player improves is by **understanding what happened**, which is why the
end screen and eventually the written retelling are the real progression
system of the game.

**Constraint:** the Director decides simulation outcomes, so it lives in the
simulation layer. It draws every choice from the scenario seed, reads only
simulation state, and never reads the camera or anything else the player's
screen knows. See [simulation contract](simulation-contract.md).

## Decided: the building has a day

Decided with the owner on 2026-09-24. The calm half of a level is not people
killing time: it is a working day, with small things happening in it. A
meeting ends and the host gets up first. Somebody goes to the toilet. Two
people stop and chat. At the end of the day everybody packs up and heads for
the door. These small events are **cues**, called by the Director from the
level's timetable, by the people themselves, or by the player, and every one
of them is carried out with the same walking, doors, chairs and sounds the
disaster uses, so that the two halves of the game are one simulation. The
Director's timetable is the first thing a future event editor edits. The
foundation is [the cue system](cue-system.md).

## Decided: structure — a shelf of dioramas

- Each level is a small, self-contained scene — a diorama. Played together they
  tell a larger story.
- **The crowd does not carry between levels.** Each diorama has its own cast.
  What carries forward is the story and the player's own skill.
- Beaten levels stay available to replay.
- **A level is built in stages**, not one room with one exit. No single solution
  should be enough to get everybody out.
- Most runs are a **delaying action**. The player never fully wins; the question
  is how badly it goes, and 60% saved can be a triumph. Some levels may be
  survivable outright.

## Decided: time control

The player can **pause to look, but not to act**. Time stops and the camera can
move so the scene can be read, but no Influence is spent and no card is played
while paused. The player gets to *understand* what is happening; they do not
get to optimise it.

This matters more in Paniq than in most games, because there are no helper
markers and a large level can easily have two things going wrong at once.
Pausing is how a player catches up — it is not a tactical layer, and the round
stays a real-time panic rather than becoming turn-based.

## Design pillars

1. **Indirect control:** change what people are capable of, never where they go.
2. **Autonomous crowd:** the crowd reacts to hazards, the environment, and each
   other; it is not a collection of identical particles. A reaction is a
   feeling about a situation -- fear today; anger, trust and curiosity as they
   are needed -- never a rule keyed to an event's name.
3. **The crowd carries the disaster:** the people being rescued are also how the
   danger spreads.
4. **Readable emergence:** simple, visible rules produce surprising chains with
   understandable causes.
5. **Dynamic survival:** disasters and crowd conditions change during real-time
   play, creating urgency without removing the player's ability to respond.
6. **Dark comic consequence:** saving people matters; disastrous failures are
   explicit, exaggerated, and part of the game's black-comedy appeal.
7. **A toy as well as a game:** the crowd is worth watching with no intervention
   at all.

## Known risks being watched

These are accepted, not solved. Each needs playtest evidence before it is
treated as settled.

- ~~**The Influence economy can spiral.**~~ **Settled on 2026-09-23: panic
  pays.** The alternative that was kept on the shelf came off it. A round now
  opens with an empty purse and an empty hand; the meter fills from the
  building in uproar, and every person the disaster kills deals the player one
  card at random. So a disastrous opening arms the player rather than leaving
  them broke and spectating, and the game and the toy are the same thing. The
  cost is that letting people die is genuinely tempting, which the owner chose
  deliberately: it hurts the percentage and buys options, so it is a hard
  choice rather than a free lunch. What is now unproven is the *wait* at the
  start of a round — see the measurement in
  [technical decisions](technical-decisions.md).
- **The cascade curve.** Contagion tends to either fizzle out or run away, and
  the interesting middle is narrow. The Director is the intended cure and is
  unproven.
- **Attention at scale.** Refusing helper markers is a deliberate choice that
  currently has no replacement built, because there is no audio yet.
- **Legibility at scale.** Two hundred people make a percentage hard to
  interpret. The clickable end screen and later the written retelling are the
  planned answer.

## Deliberately open

- The player's in-world identity.
- Specific disasters, locations, and the order levels are played in.
- Whether the player knows individuals by name, or the crowd stays anonymous.
- Audio design beyond the decision that sound replaces helper markers.
- The models themselves, and the concept art that will pin them down; the
  style they sit in is settled in [look and controls](look-and-controls.md),
  and the way one is made in the [model pipeline](model-pipeline.md).
- Which feelings beyond fear arrive first, and with which danger or card.
  (Crowd size and level size are decided above: large. Influence numbers and
  the card list are settled for now in [technical decisions](technical-decisions.md).)
- Relationships between people, and which information is hidden from the player.

Locations and situations will vary; no single narrative setting is committed.

## Not in the foundation

Networking, open-world simulation, procedural cities, monetization, purchased
assets, and mobile deployment are not foundation requirements.
