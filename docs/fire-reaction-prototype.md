# Fire-reaction prototype

**Status:** prototype checkpoint. This is a mechanics bring-up between the
foundation notes and the interactive vertical slice. It proves that agents can
move deterministically in a life-like way, react to a spreading hazard, and
leave an explainable causal event trail.

## Experience

The `FireReactionPrototype` scene shows one enclosed, abstract 12 m by 12 m
room and ten people (capsules).

**Before the fire, people loiter.** Each person makes their own small decisions
every few seconds. They stroll to a spot on a gently curving path, stop, glance
around, or wander over to stand about a metre from someone as if chatting.
They turn and speed up gradually, keep personal space, and steer away from
walls before reaching them. Each person has their own seeded walking pace
(1.1–1.5 m/s) and turning speed, so no two move alike.

**After five seconds a fire starts** at a seeded spot near the middle. The
floor is a grid of 0.5 m squares. Each burning square shows a dim glowing tile
and a cluster of two or three small cubes that bob, spin, flicker and cycle
between red, orange and yellow. New squares pop in with a small overshoot, and
old squares slowly fade to deep-red embers. The fire only spreads from squares
that are already burning, into an irregular blob, and fills the room in about
40–45 seconds.

**People who see the fire panic.** They freeze for a moment with a `!` and
turn to face it, yell (`))`) to people within 2.5 m, then sprint at 3.5–5 m/s.
While panicking they:
- change their mind about where to run every 0.4–1.2 s;
- sometimes swerve sharply to one side for a split second (zig-zag);
- sometimes freeze for a split second as if asking "which way?!";
- drift with the direction nearby panicking people are running;
- run straight away from the fire if it is within 1.5 m; and
- never stay pinned against a wall, because being blocked forces a new
  decision.

A person touched by the fire is lost and falls over as a dark red capsule.
There are no exits yet, so everyone is eventually caught (about 40 seconds
after the fire starts with the default seed). There are also no player
controls, score, restart control, or end screen in this checkpoint.

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
  speed to zero and counts as a blocked tick.
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
- **Contact.** An agent is lost when its 250 mm footprint overlaps a burning
  cell, either where it stands or along an accepted move. The `AgentLost`
  event's causal parent is the earliest-lit cell it touched. So the log traces
  every death back through the exact chain of squares to the first spark.
- **Vision.** Agents do not use a proximity fear radius. They have a forward
  90-degree vision cone with a 3 m range around their current heading. A calm
  agent becomes alert when the nearest point, centre, or a corner of any
  burning cell lies inside that cone. It yells across a 2.5 m radius and
  receives a seeded 0–20 tick reaction delay before panicking. Agents alerted
  by a yell receive their own delay without pretending they have seen the
  fire; they can promote to a visual alert when the fire enters their cone. The
  `AgentScared` event's parent is the agent's own alert.
- Lost agents keep their state and position but leave occupancy and make no
  later decisions.

## Causal events and presentation

The simulation keeps `FireActivated`, `FireSpread`, `AgentAlerted`,
`AgentYelled`, `AgentScared`, and `AgentLost` events. The room, isometric
camera, capsules, fire cubes, vision-cone outlines, and calm/scared/lost counter
are observational presentation. They map logical millimetres to Unity metres
and never write simulation state.

The runner keeps the latest and previous snapshots. The display blends between
them by how far the current frame is between ticks, so movement is smooth at
any frame rate. Capsules bob with each stride (more when sprinting), lean
forward with speed, and lie flat when lost. Fire-cube variation comes from a
hash of the cell's grid position, never from the simulation's random
generator. The cubes share one emissive material, recoloured per cube through
a `MaterialPropertyBlock`. The camera is 45 degrees around the room and
35.264 degrees above the ground, which gives a standard isometric view.

This checkpoint deliberately remains ordinary GameObjects and C# code. The
interactive exits/guidance/scoring slice in
[vertical-slice-systems-bringup.md](vertical-slice-systems-bringup.md) remains
the next milestone. Profile a standalone build before adding scale tooling.
