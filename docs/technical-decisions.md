# Technical decisions

## Chosen foundation

| Area | Decision | Reason |
| --- | --- | --- |
| Engine | Unity 6.3 LTS (`6000.3.24f1`) | Supported Windows workflow, C#, mature 3D tools, and a future ECS path. |
| Rendering | URP | A scalable Unity rendering path suitable for desktop and later mobile quality profiles. |
| Initial runtime model | GameObjects and C# | Keeps the first vertical slice approachable and fast to iterate. |
| Input | Unity Input System | Keeps device input separate from game intent. |
| Tests | Unity Test Framework | Supports edit-mode logic tests and play-mode scene-flow tests. |
| Version control | Git | Text Unity assets are tracked; LFS waits for large source media. |

## Prototype decision: fire-reaction bring-up

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Scenario data | One `FireReactionScenario` ScriptableObject with an explicit seed, stable agent IDs, and fixed spatial values | It gives the first prototype repeatable starting data without putting mutable run state in the Unity scene. [Unity ScriptableObject manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html) | The game needs multiple scenario variants or a designer-facing authoring workflow. |
| Simulation cadence | One `FixedUpdate` runner at the existing 0.02-second fixed step | The fire and agent rules advance in a predictable order even when the game renders at a different frame rate. [Unity fixed updates](https://docs.unity3d.com/6000.3/Documentation/Manual/fixed-updates.html) | A replay-compatibility review approves a different fixed step. |
| Crowd movement (revised 2026-09-18) | Integer steering with whole-degree headings: agents turn and accelerate gradually toward a goal, blended with pushes away from people within 0.8 m and walls within 0.6 m. Occupancy checks and the rotating resolver are unchanged; the swept-circle test is now exact for moves in any direction. No Unity physics or navigation. | Owner playtest: four-direction movement read as robotic and panicked agents ran into walls and stayed there. Steering gives curved, eased motion while staying replayable. | An approved scenario needs obstacles or routes that local steering cannot handle; then consider AI Navigation. |
| Integer trigonometry | Fixed 91-entry sine table (scaled by 10,000), an integer binary-search heading function, and integer square root in `IntegerMath` | Free headings need sine and cosine, but floating-point results must not decide outcomes. A table is small, exact, and tested against `Math.Sin`. | Headings need finer than one-degree resolution. |
| Fire reaction and facing | Integer-only forward 90-degree, 3 m vision cone around the agent's current heading, tested against every burning cell | Seeing danger in front of an agent reads more clearly than an invisible radius. | The approved slice needs occlusion, smoke, or sound direction. |
| Calm behaviour defaults | Seeded per-agent walk pace 22–30 mm/tick (1.1–1.5 m/s), turn rate 4–7°/tick, acceleration 2 mm/tick²; decisions every 60–200 ticks between strolling, standing, glancing around, and standing near someone | Small independent decisions read as people rather than random motion; normal human walking pace is about 1.3 m/s. Chosen on the owner's behalf. | Playtesters say the crowd looks too busy, too idle, or too uniform. |
| Panic behaviour defaults | Seeded sprint 70–100 mm/tick (3.5–5 m/s), turn rate 10–16°/tick, acceleration 8 mm/tick²; re-decide every 20–60 ticks by scoring 8 random escape spots; 35% chance of a 30–70° swerve; 12% chance of a 5–15 tick hesitation; follow nearby runners at 35% weight; flee directly within 1.5 m of fire; give up a blocked direction after 12 ticks | The owner asked for visible panic: faster running, zig-zags, and changing decisions mid-run. Measured in a headless run of the default seed: about 3.3 m/s average and about two sharp (>30°) direction changes per second in the first 20 s. Chosen on the owner's behalf. | Playtesters find panic unreadable or too twitchy; lower the swerve chance or lengthen the decision interval first. |
| Maximum step distance | Raised from 60 to 120 mm per tick | The panic sprint needs up to 100 mm per tick. It is still well under the 500 mm agent diameter, so swept contact remains meaningful. | Faster movement is needed. |
| Alert propagation | A visual detection emits one deterministic `AgentYelled` event across a 2.5 m radius; each calm listener becomes alert with its own seeded reaction delay | Nearby agents react as a group without scene physics or object references, and the causal chain explains whether fear came from sight or a yell. | The slice needs walls that block sound, a larger hearing model, or audio-driven gameplay. |
| Alert expression | Track whether alert came from direct vision or a yell; only direct visual alerts turn to face the nearest burning point | `!` communicates a direct sighting, `))` communicates a yell, and a heard warning does not falsely imply the listener can see the fire. | The prototype needs richer memory or sound direction. |
| Prototype fire shape (revised 2026-09-18) | A 24 × 24 grid of 500 mm cells. The fire starts in one seeded cell; each burning cell lights one random unburnt 4-neighbour every 40–120 ticks and never goes out. Each ignition is a `FireSpread` event caused by the cell that lit it. Shown as a glowing tile plus 2–3 small animated cubes per cell. | The owner asked for fire made of small animated cubes that spreads from itself. A grid makes "spreads from itself" literal and keeps contact checks cheap and exact. The room fills in about 40–45 s. Chosen on the owner's behalf. | The slice needs fire that burns out, spreads by material, or is blocked by walls. |
| Fire cube presentation | GameObject cubes with one shared emissive URP Lit material, recoloured per cube through a `MaterialPropertyBlock`; variation from a hash of the cell position | Up to about 1,700 cubes is fine as plain GameObjects for a prototype, and a property block avoids one material copy per cube. [MaterialPropertyBlock](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MaterialPropertyBlock.html) | Profiling a standalone build shows the fire display costs noticeable frame time; then move to GPU instancing (`Graphics.RenderMeshInstanced`). |
| Replay compatibility | `simulationCompatibilityVersion` 1 → 2 and `contentRevision` 9 → 10 | Movement, fire, and event rules changed, which the [compatibility policy](simulation-contract.md#simulation-compatibility-policy) requires to be versioned. | Any further change to a replay-relevant rule. |
| Prototype presentation camera | Orthographic camera at a 45-degree horizontal angle and 35.264-degree elevation | This is the familiar isometric game view and keeps the whole 12 m room readable. | Playtesting shows that depth or agent readability requires a different camera. |

Run **Paniq > Project > Configure URP** after Unity resolves packages. The
command creates and assigns Unity-version-matched URP assets; commit its
generated assets and project-settings changes as part of project initialization.

## How decisions are made

Paniq's owner is learning game development, so technical decisions must remain
teachable and reversible. Before adding a package, workflow, or major system:

1. Research the current official documentation and inspect the existing project.
2. State the problem in game terms, then give one plain-language recommendation.
3. Record why it is needed now, what simpler option was retained or rejected,
   relevant source links, and the condition that would trigger a later review.
4. Explain the implementation result and exact local Unity steps needed to
   verify it.

Ask the owner only to choose genuine product direction, such as the intended
player experience or visual tone - not engine settings they have no reason to
know yet.

## Design constraints for future expansion

The authoritative rules for replayable simulation are in
[Simulation contract](simulation-contract.md). They apply before any gameplay
system is introduced.

- Keep interaction data explicit: source ID, event type, world position,
  strength, duration, and causal parent.
- Keep configuration in scenes and ScriptableObjects rather than hidden static
  state.
- Keep random choices tied to an explicit scenario seed.
- Keep presentation separate from future gameplay/simulation decisions.
- Prefer simple steering and authored obstacles before adding navigation.

## Deferred technology

| Technology | Do not add it until |
| --- | --- |
| ECS, Burst, Entities Graphics | A profiled representative slice cannot meet its frame-rate target because of crowd update or rendering cost. |
| AI Navigation | The approved slice needs pathfinding that simple steering and obstacle avoidance cannot provide. |
| Cinemachine | The hand-authored camera prevents a required player experience. |
| Addressables | Content size, loading needs, or platform packaging makes direct references difficult to manage. |
| Mobile settings | The Windows slice is stable and a target device is available for measurement. |

Any addition above requires a short dated note here recording the measured or
feature-driven reason and the expected benefit.
