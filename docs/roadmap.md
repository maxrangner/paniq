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
| A way out that has just opened | Behaviour | Opening the one door is news: the people who could see or hear it go turn and head for it on the spot, and nobody is still trudging toward the fire on the strength of a door that used to be locked |
| Eager to get out | Behaviour | People with a clear way out in front of them stop dithering, stop zig-zagging, stop drifting with the crowd and run for it — even the ones an alarm had left walking out calmly. The frozen, the burning, the cruel and the ones who turn back to help are still themselves |
| You can see why a door will not open | Style | A door with a chair wedged in it says so when you point at it, instead of turning green and then doing nothing; and somebody taking charge now sends the strong at a wedged door as well as a locked one |
| Doors that give | Style | A door leaf swings away from whoever pushes it rather than always the same way, and each door carries a setting for the one-way doors a later scene may want |
| Tables that tip over | System | A table hit hard enough tips up on one edge and crashes into a heap of boards; whatever stood on it slides off beside it, and the heap is then just another thing to shove, kick and trip over |
| Sitting properly | Style | People at the meeting table sit on their chairs rather than standing in them, folded down so their heads clear the table, and they scoot the chair in as they settle and shove it back as they rise |
| Stop, drop and roll | Behaviour | Somebody alight may throw themselves down and roll instead of running blind, and about a third of the time the flames go out and they get back up |
| A jet you can watch | Style | An extinguisher is not a laser: the jet works back and forth across the fire, a narrow steady arc in strong hands and a wild wobble in weak ones |
| Reading the crowd | Style | A small panel names what every mark over a head means, and boxes and chairs are drawn to fit inside the space the simulation actually keeps clear, so nobody clips through a corner any more |

## Choosing the next stone

The owner picks the next stone after playing the current prototype. A stone
should be small enough to build and play in one step. Its plan names:

1. what the player will see differently;
2. which layer it is (system, behaviour, or style);
3. what it deliberately leaves out; and
4. how it will be checked (tests, and what to look for on screen).

Everything obeys the foundation notes above and the evidence gates in
[technical decisions](technical-decisions.md).
