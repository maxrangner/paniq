# The cue system: the building's day

**Status:** decided foundation (2026-09-24). This note defines how the calm
half of a level gets things to do: the small events that happen in a
building's day, who calls them, how a person takes one up, and what a future
event editor edits. It builds on the [simulation contract](simulation-contract.md)
and the [agent state model](agent-state-model.md), which stay the authority for
ticks, randomness, identity, events and the boundary with presentation.

## What a player sees

Before this, the office before the fire was a room of people killing time:
every few seconds each person rolled a dice and strolled, stood, glanced,
tidied a box or sat in a chair. Nothing had a purpose, nobody ever opened a
door, and the one thing that looked like an event, the meeting breaking up at
the minute mark, was a timer on six chairs.

Now the building has a **day**. The meeting ends because the level's
timetable says so, and the host is the first on their feet. Office workers
drift back to their own desk chairs between strolls. Somebody walks to the
bathroom, goes into a stall, shuts the door and comes out a while later. Two
people stop and talk face to face, and the people beside them glance up as it
starts. Call it a day and the whole floor packs up and heads for the way out,
one person at a time, opening every door on the way, and queues at the front
door if it is locked. All of it goes into the story on the end card.

## Three words

- A **cue** is one of these small events: *the meeting ends*, *home time*, *a
  chat*, *a toilet trip*, *back to my desk*. The word is chosen because
  "event" already means a line in the causal log. A cue is written into the
  log once (`CausalEventType.CueCalled`, with the kind as its strength) and
  then reaches people. What a cue *is* -- who it reaches, who speaks for it,
  and what they do about it -- is data on the scenario (`CueDefinition`, one
  per `CueKind`), not code.
- An **errand** is what one person does about a cue: the cue's **script**,
  a short list of **steps** from a fixed vocabulary, carried out one at a
  time. The steps are: go to (their own chair or spot, the nearest free
  stall, the person the cue is about, or home or where they stood), sit on
  (their own chair), stand for a while, say something, talk, shut the door
  of the small room they are in, open it, and leave the building. The toilet
  trip, for instance, is *go to a free stall, shut the door, stand for ten to
  thirty seconds, open the door, go home, sit on your chair*. Every step is
  carried out with the behaviours that already exist: the route fields for
  the walk, the door system for the doors on the way, the chair behaviour
  for the sit, the sound system for a remark. A new cue is a new list, not
  new code, and the list is what a future event editor edits.
- The **Director** is the background system that calls cues from the level's
  **timetable**. Today the timetable is all it does; the reactive Director of
  the [game vision](game-vision.md), which adds and eases pressure by watching
  the run, grows from this seam.

## Who calls a cue

| Caller | How | Today |
| --- | --- | --- |
| The Director | `DirectorSystem.Advance` walks `ScenarioData.Timetable` and calls each entry once, on its tick | The office's timetable holds one thing: the meeting ends at the minute mark, spread over eight seconds |
| A person | a band of the same dice roll every calm person makes when choosing what to do next (`CalmBehaviour.ChooseActivity`); or, for going to look, the end of a glance toward a threat's noise from another room (`CalmBehaviour.TryGoAndLook`) | a toilet trip, a chat, going back to their desk, going to see what that noise was |
| The player | `PlayerCommandType.CallHomeTime`, free like the trigger, consumed before the hand is consulted; logged as `PowerCalledHomeTime`, the root cause of the cue it calls | home time for the whole building; nothing on the screen is wired to it yet |

Whoever calls it, a cue ends in `CueSystem`, which reads the cue's
definition for its audience (the caller alone, the caller and one other
person, a room, the whole building) and hands every calm, upright person in
it a pending errand. Only a cue that reaches a room or the whole building may
be on the timetable; the rule is the definition's, checked once, in the
scenario, the Director and the bake tool alike.

## The rules a cue obeys

- **Nobody moves on the tick a cue is called.** Every person in the audience
  takes it up at their own reaction tick (`SimulationContext.ReactionTick`),
  plus their own seeded share of the cue's *spread*, on a tick nobody else
  takes one up on (`CueSystem.Staggered` reserves them, as fear reserves
  reaction ticks). A meeting breaks up one person at a time even with no
  spread; the building empties over half a minute. The owner's rule from the
  playtest fixes holds: nothing happens to a whole group on one tick.
- **A cue never changes what somebody is doing on the tick it is called.**
  Somebody in the middle of an errand -- talking, in the stall, walking to
  their desk -- finishes it, and takes the new cue up when it ends
  (`AgentErrand.Next`). Home time called mid-chat waits for the chat.
- **Home time stands.** It is a state of the day (`CueSystem.IsHomeTime`),
  not a moment: whoever gave up on a locked way out, or was busy, is handed
  it again after a pause of their own (`AgentHome.NextHomeTryTick`), and
  until they are out nobody has ideas of their own. A front door unlocked a
  minute late still empties the building.
- **A reaction is a feeling, never a response to a named event.** People take
  up an errand because they were in a cue's audience, exactly as somebody
  ordered by a leader takes up the order: a record on them, a cause, and their
  own decisions from there. No rule in the crowd switches on an event type.
- **Fear beats everything.** Errands exist only while a person is calm.
  `ErrandBehaviour` is consulted by `CalmBehaviour` alone, and the errand is
  cleared on the way into being alert and on the way into being scared.
  Nobody frightened is ever in a cue's audience.
- **A cue from outside beats a person's own idea.** Somebody with home time
  waiting on them has no ideas of their own until it is done; a chat, a
  toilet trip, a wander back to their desk are not offered to them. A
  person's own idea, on the other hand, is never handed to somebody who
  already has something waiting on them.
- **The cruel are contrary.** Somebody cruel enough to defy a leader defies
  a cue with a person behind it half the time (`DaySettings.CruelIgnoreCuePercent`):
  sits on for a while when the host ends the meeting, will not talk to
  whoever came over. A cue from the clock (home time) is nobody's to defy.
  Every refusal is a line in the story (`CausalEventType.AgentIgnoredCue`).
- **A walk with a purpose still wanders.** An errand's last leg carries the
  same wander a stroll does, fading out as the place gets close; doorways
  are approached straight.
- **One random stream.** The Director and the cues draw from the run's
  generator in processing order, like every behaviour. Their draws depend on
  simulation state alone, so a replay of a seed reproduces the day.
- **The purse pays for people saved, not for people who left.** Somebody who
  strolls out at home time before anything is wrong is written down as out
  of the building, and counts as saved on the card, but the influence meter
  credits an escape only while the round is running.

## The leader's part

A meeting is ended by its **host**: the seated person in the room with the
most leadership, the lower ID on a tie. The host is the source of the
`CueCalled` line ("person 14 ended the meeting") and takes the cue up first,
with no spread; everyone else follows. `CueSystem.HostOf` is the seam later
cues use: calling a meeting, walking the visitors out, a fire drill.

## How an errand is carried out

`AgentErrand` is a fixed record on each person: which cue, which step of its
script, the phase of that step, when it starts, when the current phase gives
up, the room and door it is about, the partner, the place, the cause. Nothing
is allocated. `ErrandBehaviour` begins each step in turn; a step that does
not apply to this person (sit on your chair, for somebody with none) is
skipped, and an errand aimed at something that is not there (a free stall,
a partner who has gone) ends.

| Cue | Script, as shipped on the office level |
| --- | --- |
| The meeting ends | go to your own chair or spot, sit on it. The host's own script is *say something, go home, sit*: the remark is made from the chair, the table glances at the host, and the host is the first up. Somebody with no home (a visitor) gets up and loiters. Somebody already in their own chair (the two at the cafeteria table, when the cue reaches their room) sits on for a while of their own and then goes about their day. Somebody cruel may sit on for ten to thirty seconds first, which is a line in the story |
| Back to my desk | the same script, as a person's own idea |
| Toilet trip | go to the nearest free stall (a room whose `Use` is `Stall`), opening doors on the way; shut its door; stand for ten to thirty seconds; open the door; go home, or, having no home, back to where they stood; sit |
| Chat | go to the partner; talk for six to eighteen seconds. Both walk and meet in the middle, the one hailed a few ticks late. The first thing each says is heard nearby (`SoundSystem.Say`) and neighbours glance over; the rest is neither heard nor written down. It ends when the one whose idea it was has had enough, and for the other a moment later, or when either is gone or knocked down. Nobody starts a chat from a doorway or with somebody stood in one, and somebody cruel may refuse one outright ("person 8 would not talk to person 5") |
| Home time | leave: the way out that is the shortest walk, worked out again in every new room; each door on the route opened if shut, waited at if locked or wedged; through the way out and gone |
| Go and look | go toward where the noise came from (`ErrandTarget.TheNoise`), opening doors on the way, and stop 2.5 m short of it; stand for one to three seconds. Somebody's own idea, after they have looked toward a threat's noise (fire crackling) from another room and seen nothing; not the very nervous (nervousness 8 or more), not somebody with a cue waiting on them, and not again for about eighteen seconds. What they usually see when the door opens is the fire, which frightens them and ends the errand, and the shout that follows tells the room. Without it a bathroom ablaze was heard by eighteen people who sat on at their desks until it reached them (seeds 40 and 42, 2026-09-24) |

Every "go to" walks room to room, opening the doors on the way, and every
step gives up if it is going nowhere: stuck for `DaySettings.BlockedGiveUpTicks`
(three seconds; somebody leaving never gives up for being stuck, a queue is
the point of them) or past `DaySettings.ErrandTimeoutTicks`. A door somebody
opened themselves is shut behind them once they are through, unless somebody
else is near it (`DaySettings.DoorHoldMillimetres`), so the floor does not
end up all open doors. A door stood at that would not open is remembered for
about a minute (`DaySettings.LockedDoorMemoryTicks`) and routed round.

The sit at the end of an errand is in their own chair, and only in it:
somebody with a desk chair never sits anywhere else, nobody sits in somebody
else's, and a sit at one's own desk lasts half a minute to a minute and a
half (`DaySettings.DeskSitMinimumTicks` / `DeskSitMaximumTicks`) rather than
the five to twenty seconds of a chair that is not theirs.

Two things make this different from the calm behaviour before it:

- **Calm people open doors.** A shut door used to be a wall to anybody who was
  not frightened, so a calm person's whole day happened inside one room. An
  errand opens the doors on its way the way a frightened person does, and
  waits at one that will not open. That is what puts a queue at the front
  door at home time, and somebody behind a shut stall door when the fire
  starts.
- **A glance does not lose the errand.** A noise interrupts an errand as it
  interrupts any calm activity (the person turns to look, and does not edge
  toward it from a stall, a door or a conversation), and afterwards
  `ErrandBehaviour.TryResume` carries on from the phase it was in. Somebody
  walking to their chair who looks up at a thud still ends up on their chair.

A calm person **asks the way**: the route for an errand uses every door, not
only the ones the person knows, so a visitor leaves at home time like anybody
else. Only the frightened have to find the way out for themselves.

## Where it sits in the tick

Phase 1½, between the player's commands and the hazards advancing: the
Director calls the cues whose tick has come. Their effect lands in phase 4,
at each person's own reaction tick. The [simulation contract](simulation-contract.md)
lists it.

## What the future event editor edits

All of it is plain data on the scenario, editable in Unity's Inspector today:

- `ScenarioData.Cues`: one `CueDefinition` per kind -- audience, host rule,
  whether it is written down, and the script of steps (`ErrandStep`: kind,
  target, a range of ticks). Change the toilet trip's stay, give the meeting
  end a speech, or write a new cue's steps here.
- `ScenarioData.Timetable`: a list of `ScheduledCue` (kind, tick, spread,
  room). A scene can place `Paniq > Cue` components instead, and the bake
  tool writes them into the timetable; a scene with none keeps the timetable
  the level already has.
- `AgentDefinition.HomeObjectId` / `HomeSpot`: where each person belongs.
  `Paniq > Person` has a *Home* chair field and a "home is where they stand"
  tick box.
- `RoomDefinition.Use`: what a room is for. `Paniq > Room` has a *Use* field;
  the three bathroom stalls are `Stall`.
- `DaySettings`: how often a person needs the toilet (a clock per person,
  about six minutes between trips, not a dice roll: a calm person decides
  something every few seconds, and a chance per decision sent the whole
  office to the bathroom; stretched on a floor whose stalls could not keep
  up with the crowd at that rate, so they are on average at most half full),
  how often somebody drifts back to their desk, how long a desk sit lasts,
  how far a remark carries, how long a leaver waits at a locked door and
  how long before they try home time again, how long a locked door is
  remembered, and how near somebody must be for a door to be left open for
  them.

## What comes next

The cue system was reviewed the day it was built (2026-09-24), against the
three requirements for the finished game and with the goal of dynamic,
random interactions that feel alive and human. The three commits the review
asked for are in (an errand is a list of steps; the day keeps its own rules;
people, not clockwork). What follows is the agreed direction from here, in
the order it pays off, with a rough size and the reason for the order. None
of it is started; each is a stone of its own.

1. **Ambient object cues** (about half a day). A phone rings on a desk and
   three heads turn; a tray drops in the cafeteria. A *sound at a point*
   step on a seeded schedule, using the thud the sound system already has.
   The office reacts to things, not only to each other: the cheapest big
   win for "alive", and the first cue whose source is a thing rather than a
   person, which the editor will want.
2. **Social texture from the step vocabulary** (days). A greeting as two
   people pass (a head turn without stopping, which needs a look heading
   the body does not follow); a third person joining a chat (a small pooled
   chat record instead of one partner index); two people walking together
   (a calm *follow* step, built from the leader's follow); the host walking
   the visitors out at home time. Each is a new list of steps plus at most
   one new step kind.
3. **Return to calm** (one to two weeks; the big one). Fear is one way today:
   nobody who has been frightened ever comes back. Somebody alarmed by a
   distant shout who sees nothing should settle back, wary; after a small
   fire is put out the office should drift back to its desks and talk about
   it; the day should resume. Needed for a large level where one wing
   panics and another works on, for "back to work" and "fire drill" cues,
   and for everything below. Touches `FearSystem`, `PanicBehaviour` and the
   round clock, and is the wall behind every later cue.
4. **The reactive Director** (three to five days, after 3). Reads the count
   of the frightened, the rooms cut off, the burning squares; picks from a
   menu of cues with cooldowns, seeded. A quiet run gets a second problem in
   a far wing; a massacre gets a breather. `DirectorSystem` is the seam; it
   reads simulation state and the seed only, never the player's screen.
   *Started 2026-09-25 (prototype 3):* its first reactive rule is in, a
   trap (`TrapSystem`, called from the Director every tick) that watches the
   fire and the crowd and brings the tower of boxes down across the archway
   once the fire is lit and somebody comes near. One rule, one trap, no
   menu and no cooldowns yet; the traps are data on the scenario
   (`ScenarioData.TrapDefinitions`), so a second is a line, not code.
5. **Visuals for the day** (a visuals commit). A pip over whoever speaks and
   heads turning to them; a home-time button, so the player's path to it
   can be seen; chats that are not two statues. Nothing here decides
   anything in the simulation.
6. **Meetings as gatherings.** A meeting is a record (room, host, members),
   so *call a meeting* exists as a cue and the timetable can hold a whole
   day: meeting at nine, lunch at twelve, home at five.

Smaller things noted along the way, none started:

- **Same-tick glances.** Everybody who hears a remark or a thud glances on
  the same tick; the owner's rule that nothing happens to a whole group on
  one tick is not yet applied to glances at a noise.
- **A chair just left.** A visitor risen from the meeting may sit straight
  back down in the nearest free chair a few seconds later.
- **The day at scale.** The stress building has no timetable and no homes,
  so the day has never been timed at two or five hundred people; the first
  home time in a big building builds a route field per door side outside
  the eight-per-tick budget, a one-off spike, unmeasured. Give the stress
  building homes and a timetable and time the first home-time tick.
- **Storeys.** Nothing in the day mentions a storey; the radius queries it
  added (remarks, chat candidates, the stall fallback) will each need a
  storey filter when a level asks, like everything else.
- **A home-time card and button.** The command exists and is tested; the
  card in the deck and the button on the screen are a small later change.
- **"Lunch ends" on the timetable.** The two at the cafeteria table sit
  through the calm half unless the meeting-ending cue reaches their room;
  it is one timetable line if they read as statues.
- **Cues after the disaster starts.** Waits for return to calm (3): a "fire
  drill" or "carry on working" cue during a round needs calm to be
  something a person can come back to.
