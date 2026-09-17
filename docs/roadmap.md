# Roadmap: foundation to vertical slice

This is Paniq's implementation order, not a gameplay-design document or a
schedule. Each row is small enough to use as the brief for one subsequent
planning task.

| Status | Stage | Required output | Excludes |
| --- | --- | --- | --- |
| Complete | Lean project foundation | Unity project, scenes, runtime assembly, placeholder content path, and smoke tests | Playable mechanics |
| Complete | Simulation contract | Rules for deterministic ticks, randomness, identity, events, processing order, and presentation separation | Agent, hazard, movement, and intervention behaviour |
| Complete | [Scenario data versus runtime state](scenario-runtime-state.md) | A design note that separates authored scenario configuration from mutable run state, including replay-relevant seed and compatibility data | Agent decisions, hazards, movement, and player powers |
| Complete | [Agent state model](agent-state-model.md) | A design note for the minimum stable, simulation-owned state an autonomous agent needs | Agent decision logic, crowd behaviour, or content-specific reactions |
| Complete | [Causal event log and debugging view](causal-event-log.md) | A design note for retaining, querying, and presenting the contract's causal events | New event mechanics or player-facing UI design |
| Complete | [Movement and spatial-world rules](spatial-world-rules.md) | A design note for logical position, world constraints, occupancy, and simple movement rules | Navigation technology, hazards, and player intervention |
| Next | [Vertical-slice integration: systems bring-up](vertical-slice-systems-bringup.md) | One approved compact scenario combining autonomous agents, an evolving disaster, indirect intervention, understandable cause and effect, and a percentage-saved result | Additional scenarios, campaign structure, or scale tooling |
| Later | Post-slice refinement | Seeded replay checks, event inspection, runtime diagnostics, automated tests, and a standalone Windows profiling record | Further content and premature optimization |

## How to use this roadmap

Plan and complete each row in order. A planning prompt should name the row,
its required output, its exclusions, and the simulation contract as a binding
constraint. Do not start vertical-slice integration until the four preceding
foundation design notes are accepted.

After the post-slice refinement checkpoint, follow the broader milestones in
[Goals and milestones](goals.md) and the evidence gates in
[Technical decisions](technical-decisions.md). Further content, campaigns or
sandbox structure, and profiling-led scale tooling are intentionally outside
this roadmap.
