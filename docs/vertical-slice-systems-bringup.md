# Vertical-slice systems bring-up

**Status:** prototype question. This is an unthemed design brief for the first
integrated playable test. It proves that Paniq's core systems can work together;
it does not establish a setting, story, final art direction, or final mechanics.

## Player outcome

The player sees a small group of autonomous agents face an expanding danger
field. They place one indirect guidance marker to influence a subset of the
group, then see how many agents reach safety. The point of the test is to make
the following chain understandable:

`guidance marker placed` -> `agents adopt guidance` -> `agents change route` ->
`agents reach safety or danger` -> `saved percentage`.

This applies the project's existing research: a prototype needs a clear goal,
visible state changes, and useful feedback, while its emergent outcomes must
remain understandable. [Design research](design-research.md)

## Neutral scenario

The scenario is intentionally abstract. It uses simple geometry and colour,
not a fictional location or disaster:

| Element | Prototype representation | Rule |
| --- | --- | --- |
| Agents | 12 neutral capsules | Start active and act autonomously. |
| Danger | One expanding red circular field | Starts at a fixed location and grows every logical tick. |
| Safety | Two green exit regions | An agent reaching one is saved. |
| Guidance | One blue marker | The player may place it once at a valid logical location. It is a temporary plumbing test for indirect influence, not a commitment to the final player power. |
| World | The existing bounded, flat logical XZ plane | Uses the spatial-world rules; no navigation obstacles are required. |

The scenario has an explicit persisted seed and records its scenario ID,
content revision, and compatibility data as required by [scenario data and
runtime state](scenario-runtime-state.md).

## Simulation rules

Each scenario authors a positive cardinal step distance within the spatial
maximum, two axis-aligned logical exit regions, and the danger and guidance
values below. Danger, influence, and arrival radii must remain within 200,000
mm for the complete run. The scenario plan chooses their concrete values before
runtime implementation; this brief defines their meaning.

- Each active agent normally targets the nearer exit. It requests one cardinal
  displacement of the authored step distance toward its target each tick. It
  chooses the axis with the greater absolute remaining distance, resolving an
  equal distance as X before Z. If that one requested axis is blocked, movement
  rejects the request and the agent does not try the other axis that tick.
- A valid one-use guidance command creates the marker at its fully quantized
  logical location. At placement, only agents whose footprints contact the
  authored influence circle adopt guidance; the recipient set does not grow
  later. An adopted agent has a slice-owned guidance state of `Guiding` and
  targets the marker's authored circular arrival region. On contact with that
  region it becomes `Guided`, then targets the exit farther from the danger
  source. Guidance acceptance emits an event whose parent is the placement
  event. The player never selects or commands an individual agent.
- The danger field has a fixed logical centre, an authored non-negative initial
  radius, and an authored non-negative integer growth in millimetres per tick.
  It is a logical circle over the whole XZ plane; presentation may clip its
  display at the world boundary but simulation does not. Contact occurs when an
  agent's circular footprint touches or intersects it, using
  `distanceSquared <= (dangerRadius + occupancyRadius)^2` with integer math.
  An accepted movement sweep that contacts danger also counts as contact.
- At each tick, danger grows before pre-movement contact is resolved. Surviving
  agents decide and move, then accepted sweeps are checked for danger before
  their destination is checked against an exit. Danger therefore wins an
  otherwise same-tick exit tie.
- Danger contact resolves the agent as **lost**. Reaching an exit resolves it
  as **saved**. Both update the slice's `AgentTerminalOutcome`, change the
  `AgentState` to `NoLongerParticipating`, and release world occupancy.
- The run ends when every agent is resolved or after 3,000 logical ticks (60
  seconds at the current fixed step), whichever comes first.
- The result shows `saved / initial agents` and a saved percentage rounded to
  the nearest whole number.

The simulation uses only stable IDs, ordered `PlayerCommand` records, and
logical data. Mouse position, camera state, and displayed scene objects may
derive a live command payload but never enter simulation state or replay data.
Movement and collisions follow [movement and spatial-world rules](spatial-world-rules.md).

## Cause, effect, and presentation

The prototype records immutable causal events for danger activation, guidance
placement, guidance acceptance, an agent being saved, and an agent being lost.
Guidance-derived outcomes name that agent's guidance-acceptance event as their
causal parent; danger losses name the danger-activation event as their causal
parent.
The event log remains inspectable through the [causal event log and debugging
view](causal-event-log.md).

Presentation is observational. It shows the red danger field, green exits,
blue marker, agent colours, active/saved/lost counts, and the final result. It
may interpolate displayed movement but cannot alter positions, occupancy,
commands, hazard growth, or outcomes.

## Acceptance evidence

The prototype is ready for implementation only when its intended evidence is
clear:

1. The implementation plan commits one canonical regression case before runtime
   work begins: scenario ID and revision, seed, guidance command target tick,
   command sequence and logical position, expected terminal outcome for every
   agent, saved count, and ordered causal-event sequence.
2. That canonical case and a no-guidance control reproduce identical outcomes
   and event sequences on repeated runs. The guided case changes at least one
   named agent outcome from the control.
3. The final saved percentage agrees with `AgentTerminalOutcome.Saved` and the
   initial-agent count.
4. The event-log chain explains a guided save and a danger loss without Unity
   scene-object references.
5. Runtime tests cover the PCG reference vector, command ordering, spatial
   numeric/collision limits, terminal-outcome scoring, and the canonical
   regression case.
6. A standalone Windows build is profiled after implementation. The record
   must include date, hardware, scenario, agent/event counts, frame rate, and
   frame-time hotspots as required by [development workflow](development-workflow.md).

## Deferred decisions

This brief deliberately defers names and setting, polished art/audio, social
behaviour, multiple hazards, variable agent traits, navigation, obstacle
layouts, player economy, campaign structure, and scale tooling. A future
implementation plan must add runtime code, scene assets, input, UI, tests, and
profiling evidence before this roadmap stage becomes complete.
