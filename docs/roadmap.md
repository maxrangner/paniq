# Prototype roadmap

The prototype is built stone by stone (see [goals](goals.md) for what
"prototype" means here). This page records the stones laid so far and how the
next one is chosen. It is not a schedule and not a feature list for the
finished game.

## Foundation (complete)

| Note | What it settles |
| --- | --- |
| [Simulation contract](simulation-contract.md) | Ticks, seeded randomness, stable IDs, events, processing order, and the simulation/visuals boundary |
| [Scenario data versus runtime state](scenario-runtime-state.md) | What is authored versus what changes during a run, including replay data |
| [Agent state model](agent-state-model.md) | The minimum record each person needs |
| [Causal event log](causal-event-log.md) | How cause-and-effect events are kept and inspected |
| [Movement and spatial-world rules](spatial-world-rules.md) | Positions, room bounds, occupancy, and movement resolution |

## Prototype stones laid

All stones so far live in the [fire-reaction prototype](fire-reaction-prototype.md)
scene.

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

## Choosing the next stone

The owner picks the next stone after playing the current prototype. A stone
should be small enough to build and play in one step. Its plan names:

1. what the player will see differently;
2. which layer it is (system, behaviour, or style);
3. what it deliberately leaves out; and
4. how it will be checked (tests, and what to look for on screen).

Everything obeys the foundation notes above and the evidence gates in
[technical decisions](technical-decisions.md).
