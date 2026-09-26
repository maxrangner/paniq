# Glossary

The words this project uses in its own way, each in a line or two of plain
language, with where to read more. If a document uses a word that is not here
and not everyday English, add it.

## Making the game

| Word | Meaning |
| --- | --- |
| **Stone** | One layer added to the prototype on top of what already works: a system, a behaviour, or a style. After each stone the game still runs and the owner plays it. See [goals](goals.md). |
| **Prototype 1, 2, 3** | Groups of stones with one purpose each: 1 made the fire-reaction office, 2 made it a round you can play, 3 makes the level push back. See the [roadmap](roadmap.md). |
| **Batch** | A set of notes or requests the owner hands over at once, usually after a playtest. A batch normally lands as one commit. See [`AGENTS.md`](../AGENTS.md). |
| **Playtest** | The owner playing the current build and writing down what felt wrong or right. It decides the next stone. |
| **Level** | One building with its people, things and timetable. The game has one so far: [the office level](the-office-level.md). |
| **Scenario** | The code's word for a level's starting situation: the building, the cast and every setting, before anything has happened. See [scenario data and runtime state](scenario-runtime-state.md). |
| **Bake** | Turning a floor plan laid out by dragging objects in a Unity scene into scenario data (**Paniq > Bake Scenario From Scene**). See [development workflow](development-workflow.md#building-a-floor-plan). |
| **Live page / history** | The docs keep only what is current on the live pages; the records of finished stones sit unchanged in `docs/history/`. |

## How the simulation works

| Word | Meaning |
| --- | --- |
| **Simulation** | The part of the game that decides what happens: who moves where, who panics, what burns. It knows nothing about how things look. |
| **Presentation** | The part that draws, plays sounds and shows the interface. It watches the simulation and never changes it. |
| **Tick** | One step of the simulation: 20 milliseconds, so 50 a second. Everything happens on some tick. See the [simulation contract](simulation-contract.md). |
| **Seed** | The number that decides every random choice in a run. The same level, seed and player clicks always give the same run. "Seed 42" is one particular version of the day. |
| **Run** | One attempt at a level, from the first tick to the last. Also the code's name for it (`Run`). |
| **Round** | A run as the player experiences it: a calm opening, the disaster, the end card with the score. |
| **Replay** | Playing a run again from its seed and the player's clicks and getting exactly the same result. The simulation is built so this always works on the same build and computer. |
| **Fingerprint** | A single number that sums up a whole run, used by tests to catch any change in behaviour. If a change should not alter the game, every fingerprint must stay the same; if it should, they are re-recorded. |
| **Rules version / content revision** | Two numbers stored with a level (`SimulationCompatibilityVersion`, `ContentRevision`). One goes up when the rules change, the other when the building changes, so an old replay is never played against new rules. See the [version history](history/version-history.md). |
| **Causal event** | A record of one thing that happened (a shout, a door forced, someone catching fire) and what caused it, so every outcome can be traced back. See the [causal event log](causal-event-log.md). |
| **Snapshot** | A copy of the simulation's state at one tick, which the presentation draws from. |
| **Threat** | Anything a person can be afraid of: the burning floor, a thing on fire, a person on fire; a future hunter would be another (`IThreat`). People react to danger and feelings, never to a named event or to "floor on fire" as such. |
| **Navigation square / flow field** | The floor is cut into 250 mm squares. A flow field is a map over those squares that tells everyone heading for the same place which way to step, so a crowd finds its way round furniture cheaply. |

## People

| Word | Meaning |
| --- | --- |
| **Agent / person** | One simulated human. The code says agent, the docs mostly say person. See the [agent state model](agent-state-model.md). |
| **Traits** | Seven numbers from 0 to 10 per person (strength, speed, bravery, compassion, evil, nervousness, leadership), 5 being ordinary. |
| **Temperament** | How a person panics: runs, freezes for a while, or freezes for good. Dealt like a deck at the start, most fearful first. |
| **Fear states** | Calm, alert, scared: how far along being frightened a person is. Freezing is a temperament, not a fear state. |
| **Calming down** | A frightened person who sees and hears nothing frightening for a while settles, at a pace set by their personality: the brave in seconds, the very nervous never. See [the agent state model](agent-state-model.md). |
| **Rattled** | Calm again after a fright, but jumpy for a while (a minute if they saw the danger): a thud or a bang frightens them outright. |
| **Reaction lag** | The few ticks between something happening and anyone reacting to it. The owner's rule: nobody reacts on the tick a thing happens, and no group acts on the same tick. See [`AGENTS.md`](../AGENTS.md). |
| **Visitor / host** | People who do not know the building (the meeting's clients) and the person who does and can lead them out. |
| **Home** | The chair or spot a person belongs to, which they drift back to during the day. |

## The building's day and the player

| Word | Meaning |
| --- | --- |
| **Cue** | Something on the level's timetable or in the world that gives people a reason to act: a meeting ending, a noise to go and look at. See [the cue system](cue-system.md). |
| **Errand** | What a person does about a cue, as a list of steps: walk there, open the door, sit, come back. |
| **Director** | The unseen part of the game that paces a run, like a stage director. It calls the timetable's cues (the meeting ending), climbs a ladder of small incidents, and springs the trap. See [the cue system](cue-system.md). |
| **Ladder / rung** | The Director's escalation on the office: a waste bin catches; put out, a socket crackles and pops in the busiest calm room; put out, the fuse box goes. Each step is a rung. A fire that gets out of its room ends the ladder. |
| **All-clear** | The bells falling silent about ten seconds after a fire is put out, so people can calm down. |
| **Trap** | Something in the level the Director can set off, such as the tower of boxes that falls across the archway. |
| **Card** | A move the player throws into the crowd or at the building (Beefcake, TNT, the extinguisher, Stick together). See [the office level](the-office-level.md). |
| **Purse** | The currency cards and doors used to cost (called influence until 2026-09-26). The office level has it switched off; the rules are still in the code for later levels (`PurseSystem`). |
| **Influence** | The player clicking a door, a thing or a patch of floor to draw people toward it: one step a click, up to twenty, fading on its own, felt within about twelve metres in the same room, and weighed by each person's character. Never an order. (`InfluenceSystem`.) |
| **Nudge** | The player clicking a person: they step away from the click. Three quick nudges and they are annoyed, shake, and ignore nudges for a while. (`NudgeSystem`.) |
| **Trigger event** | The red button that starts the disaster, so the round opens calm and the player looks around first. |
| **The way out** | The one door in an outside wall. Walking out through it is escaping. |

## Checking work

| Word | Meaning |
| --- | --- |
| **Edit-mode / play-mode tests** | Automatic checks. Edit-mode tests run pieces of the game without pressing Play; play-mode tests press Play and look at the scene. |
| **Test bridge** | A small helper inside the open Unity editor that lets a script outside it run the tests (`tools/RunUnityTests.ps1`). See [development workflow](development-workflow.md). |
| **The two gears** | Quick targeted tests while working, the full suite (about three minutes) before every commit. |
| **Compile check** | `tools/CompileAgainstUnity.ps1`: checks the code builds, in seconds, without Unity open. |
