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
| **Level mode** | How work runs while a level is being built (2026-10-03, replacing *sketch and keep*): every change gets only the compile check and the smoke check, is saved as a local save point, and goes straight to the owner to play. Tests, fingerprints, documents and versions wait for one *hardening pass* when the owner says the level is done. See [`AGENTS.md`](../AGENTS.md). |
| **Save point** | A local commit made after each change in level mode (`wip(level): ...`), so any change can be undone in one step. Folded into one ordinary commit at the end of each working session; nothing pushed unless the owner asks. |
| **Smoke check** | `SmokeEditModeTests`: every level played once, failing only if something throws or a fire round never ends. About twenty seconds. Proves the game still runs, nothing about how it plays. |
| **Hardening pass** | The one round of tests, fingerprints, documents and versions that ends level mode, when the owner says the level is done. |
| **Task** | What a person is doing, one level above how their body moves (2026-10-03): idle, noticing a noise, sitting, tidying, an errand, escaping, frozen, following, fighting fire, helping, raising the alarm, barricading, or doing what the hand asks. One table in the code (`Tasks`) says which activity belongs to which task. |
| **The chooser** | How everybody, calm or frightened, picks their next task (2026-10-03, `TaskChooser`): a short list of options in order of preference, weighed only at a decision moment; whatever is under way carries on in between. |
| **Decision moment** | The moments a person weighs what to take up next: about once a second when frightened, when an activity ends when calm, and at once when something happens to them -- the hand reaching them, an order shouted at them, a wind-up finished. |
| **Offer** | Something from outside -- a leader's order, a noise -- that a person takes up in their own turn a beat later, never on the tick it happens (the owner's rule). |
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
| **Director** | The unseen part of the game that paces a run, like a stage director. It calls the timetable's cues (the meeting ending), lights the first bin, and turns on a crowd that is getting out too easily. Since 2026-10-02 it no longer touches the stacks of boxes. See [the cue system](cue-system.md). |
| **Ladder / rung** | The Director's opening on the office: a waste bin catches; doused before the carpet under it caught, another bin catches. Since 2026-10-02 that is all of it: put out for good, the bells fall silent and nothing more comes (it used to go on to a socket and then the fuse box). A fire that gets out of its room ends the ladder. |
| **The cap / the allowance** | The Director's other job on the office (2026-09-28): before the round it draws an *allowance*, how many the building lets out today (a tenth to two fifths of the crowd: three to fourteen of the office's thirty-four), and once the way out is open and more people than that are *on course* it pushes back with the building's tricks; when the round is already a massacre it adds nothing more. |
| **On course** | Somebody the Director counts as about to get out: out already, or frightened, on their feet, and able to walk to the way out on the map, while the way out stands open or the card is in a frightened pocket that can reach it. A calm person at their desk is not on course; nor is a queue at a shut card door. |
| **Push**, or **strike** | One move by the Director against a round that is ahead of its allowance: the socket in the room with the most people on course set crackling, the fuse box once a socket has gone, another bin lit. Half a minute to a minute between pushes. Since 2026-10-02 this is the only way a socket or the fuse box goes by the Director's doing, and a stack of boxes is never one. |
| **Exit arm** | The three-metre-wide stretch of the T's crossbar north of the junction, ending at the way out. The queue forms here, and since 2026-10-02 a socket on its wall lets the building strike it. |
| **Cubicle landscape** | The big open-plan room east of the T (2026-10-02): fifteen cubicles behind low screens, three doors in its west wall, fourteen people. The way round when the junction or the exit arm is cut. |
| **Partition**, or **screen** | A low fixed screen between cubicles: walked round like a desk, never shoved or heaved, hides nothing because people are taller than it. In the scenario it is a table marked `isPartition`. |
| **The hand's ask** | What the hand asks at the place it is on, in two or three words shown at its ring: come here, away from here, open or shut the door, pound the door, clear the boxes, carry it off, take the bottle, sit here, pull the alarm, get the card (`HandAsk`). |
| **All-clear** | The bells falling silent about ten seconds after a fire is put out, so people can calm down. |
| **Keycard** | The card that opens the way out (2026-09-27). The way out is a card door: nobody batters it and the player has no key to it. The card starts in a member of staff's pocket or on an office desk, the seed decides; staff know where, anybody who sees it learns where; somebody frightened who found the door shut goes back for it if brave enough; whoever is out cold or dead drops it; whoever has it swipes the door open for good. (`KeycardSystem`.) Since 2026-10-03 nobody goes back for it in a fright; whoever has it still swipes the door, and the loop level has no card at all. |
| **Left alone** | What the same seed comes to without the player's help: the end card's "Left alone, N of 34 would have lived", played in the background while the round runs, its disaster started when the player's was. The owner's target for the office is about a quarter and never more than half; measured 2026-10-02 it is about half. (`LeftAloneRunner`.) |
| **Trap**, or **stack** | A stack of boxes that comes down when somebody runs into it: the tower by the archway, the crates in the stockroom. The code still says trap (`TrapSystem`, `TrapDefinition`); since 2026-10-02 nothing sets one off but a body. |
| **Card** | A move the player used to throw into the crowd or at the building (Beefcake, TNT, the extinguisher, Stick together). Deleted from the code on 2026-10-03; the git history keeps them. |
| **Purse** | The currency cards and doors used to cost (called influence until 2026-09-26). Deleted from the code on 2026-10-03; everything the player does is free. |
| **The hand** | What the player has since 2026-09-29: a mouse button, held down. The left draws people, the right pushes them away (2026-09-30). On a place it is influence; on a person the left is a tug; let go, and everybody is on their own again. Held and dragged, it moves with the pointer and the people answering it follow (2026-09-30). One hand, so one place or person at a time: "you can't be everywhere at once" (the owner). |
| **Influence** | The player's hand on a door, a thing or a patch of floor, drawing people toward it for as long as the button is held: full at once, gone at once when let go, felt about a room's length away as a walk (through an open doorway, never a wall), and weighed by each person's character. The calm and, since 2026-09-30, the frightened come and stand round it; on fallen boxes it has them cleared. (`InfluenceSystem`.) |
| **Conviction** | How much the hand holds a person, 0 to 1000 (2026-09-30): it grows every moment they feel the hand, by how strongly; past a low line they set about the task, past a higher one they keep it when the hand comes off, and a kept goal fades on their own beat, faster the less easily led they are. A failed attempt costs some. The one number everything the hand asks of a person reads. (`AgentHand.Conviction`, `InfluenceSettings`.) |
| **The hand's charge** | The bar at the bottom left (2026-09-30): it drains while the hand is on a place, a beacon or a person, refills by itself all the time, and empty takes the hand off until it has rested. (`HandChargeSystem`.) |
| **Hand strength** | How strongly everybody feels the hand, as a percent of the level's own: the Tab panel's slider, for finding the value to keep (2026-09-30). Everything the hand does starts from how strongly a person feels it, so this one number turns it all up or down. (`InfluenceSettings.StrengthPercent`.) |
| **Tell** | The second of visible wind-up before somebody does something dangerous -- going stiff before a freeze, gathering nerve before a dash through the heat, turning back toward the flames -- with a red ring shrinking at their feet. (`TellSystem`.) |
| **Caught in time** | A tell stopped by a poke, a tug or the hand before its ring closed: they run instead of freezing, give up the hot door, or stay out of the flames. Counted on the end card. |
| **Answering the hand** | A person doing what the hand asks: coming to it, using what it is on, clearing the boxes under it. A gold hand bobs over them. |
| **Tug** | The player's hand on a person: a tug on their shirt that stops them within about a quarter of a second and holds them there while the button is held, struggling. The strong tear free, sooner the stronger; nobody alight or down can be held. The one direct thing the player does to a person. (`TugSystem`.) |
| **Nudge** (or poke) | The player clicking a person: they step away from the click. Three quick pokes and they are annoyed, shake, and ignore pokes for a while; three quick pokes wake somebody frozen. (`NudgeSystem`.) |
| **Creak** | Gone since 2026-10-02. The three seconds a sprung trap swayed and was heard before it fell (2026-09-29); a stack knocked by a runner comes down a beat later with no warning, because a shove gives none. |
| **Par**, or **left alone** | What the same seed comes to with nobody at the controls, played in the background during the round. Shown on the strip once it is known and on the end card as the margin: the round is judged as you against the building. (`LeftAloneRunner`.) |
| **Trigger event** | The red button that starts the disaster, so the round opens calm and the player looks around first. |
| **Test level** | A level drawn by code rather than laid out by hand, for watching one thing (2026-10-01): the square room (a crowd), the maze (following), the interaction room (one of everything). Picked from the row on the start card; each has the Crowd button. (`TestBuildings`.) |
| **Crowd switch** | The Crowd button on a test level: one press panics the whole crowd, each person on a tick of their own, and holds them so; the next calms them one by one and silences the bells. It begins the round. (`FearSystem.PanicEveryone`, `CalmEveryone`.) |
| **Line of sight (of the hand)** | Through an open doorway the hand is felt only where it can be seen (2026-10-02): in full straight through the gap, fading over three quarters of a metre past the frame, nothing beyond. The wall beside an open door hides the hand as any wall does. (`InfluenceSettings.DoorwaySightSoftEdgeMillimetres`.) |
| **The way out** | The one door in an outside wall. Walking out through it is escaping. |

## Checking work

| Word | Meaning |
| --- | --- |
| **Edit-mode / play-mode tests** | Automatic checks. Edit-mode tests run pieces of the game without pressing Play; play-mode tests press Play and look at the scene. |
| **Test bridge** | A small helper inside the open Unity editor that lets a script outside it run the tests (`tools/RunUnityTests.ps1`). See [development workflow](development-workflow.md). |
| **The two gears** | Quick targeted tests while working, the full suite (about six and a half minutes) before every commit. Level mode runs neither, only the compile check and the smoke check. |
| **Compile check** | `tools/CompileAgainstUnity.ps1`: checks the code builds, in seconds, without Unity open. |
