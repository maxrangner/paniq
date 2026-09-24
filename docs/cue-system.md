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
  chat*, *a toilet trip*. The word is chosen because "event" already means a
  line in the causal log. A cue is written into the log once
  (`CausalEventType.CueCalled`, with the kind as its strength) and then
  reaches people.
- An **errand** is what one person does about a cue: a purpose with a place.
  Go home, leave the building, visit the toilet, talk to somebody. An errand
  is carried out with the behaviours that already exist: the route fields for
  the walk, the door system for the doors on the way, the chair behaviour for
  the sit at the end, the sound system for a remark.
- The **Director** is the background system that calls cues from the level's
  **timetable**. Today the timetable is all it does; the reactive Director of
  the [game vision](game-vision.md), which adds and eases pressure by watching
  the run, grows from this seam.

## Who calls a cue

| Caller | How | Today |
| --- | --- | --- |
| The Director | `DirectorSystem.Advance` walks `ScenarioData.Timetable` and calls each entry once, on its tick | The office's timetable holds one thing: the meeting ends at the minute mark, spread over eight seconds |
| A person | a band of the same dice roll every calm person makes when choosing what to do next (`CalmBehaviour.ChooseActivity`) | a toilet trip, a chat, going back to their desk |
| The player | `PlayerCommandType.CallHomeTime`, free like the trigger, consumed before the hand is consulted; logged as `PowerCalledHomeTime`, the root cause of the cue it calls | home time for the whole building; nothing on the screen is wired to it yet |

Whoever calls it, a cue ends in `CueSystem`, which decides its audience (a
room, the whole building, one person, a pair) and hands every calm, upright
person in it a pending errand.

## The rules a cue obeys

- **Nobody moves on the tick a cue is called.** Every person in the audience
  takes it up at their own reaction tick (`SimulationContext.ReactionTick`),
  plus their own seeded share of the cue's *spread*. A meeting breaks up one
  person at a time; the building empties over half a minute. The owner's rule
  from the playtest fixes holds: nothing happens to a whole group on one tick.
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
  toilet trip, a wander back to their desk are not offered to them.
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

`AgentErrand` is a fixed record on each person: the kind, the phase, when it
starts, when the current phase gives up, the room and door it is about, the
partner, the place, the cause. Nothing is allocated.

| Errand | Steps |
| --- | --- |
| Go home | out of whatever chair they are in; walk to their own chair or spot, room to room; near it, the chair behaviour takes over and seats them. Somebody with no home (a visitor) gets up and loiters |
| Visit the toilet | the nearest free stall by walking distance (a room whose `Use` is `Stall`); walk there, opening doors on the way; shut the stall door; stay a drawn while; open it; then home, or loiter |
| Chat | the one whose idea it was walks over; the other is hailed and turns to face them, a few ticks late; both stand talking; the first thing each says is heard nearby (`SoundSystem.Say`) and neighbours glance over; the rest is only written down; it ends when either has had enough or the other is gone |
| Leave the building | the way out that is the shortest walk, worked out again in every new room; walk to each door on the route, open it if it is shut, wait beside it if it is locked or wedged; through the way out and gone |

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
  office to the bathroom), how often somebody drifts back to their desk, how
  long a chat lasts, how far a remark carries, how long a leaver waits at a
  locked door.

## Deliberately left for later

- **The reactive Director.** Reading how the run is going and adding or
  easing pressure. The seam is `DirectorSystem`; it reads simulation state
  and the seed only.
- **Cues after the disaster starts.** Nobody returns to calm today, so an
  errand never outlives a fright. A "fire drill" or "carry on working" cue
  during a round waits for calm to be something a person can come back to.
- **A third person joining a chat**, and refusing a cue by trait (the
  bastard who keeps the meeting going).
- **The host walking the visitors out** at home time, with the leader's rally.
- **A home-time card and button.** The command exists and is tested; the card
  in the deck and the button on the screen are a small later change.
- **Sitting until told, for everybody who starts seated.** The two people at
  the cafeteria table start seated and nothing on this level's timetable gets
  them up, so they sit through the calm half until something frightens them.
  A "lunch ends" cue is one timetable line if they read as statues.
