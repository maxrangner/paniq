# Goals and milestones

The [foundation-to-slice roadmap](roadmap.md) gives the implementation sequence
for reaching and validating the vertical slice. These milestones remain the
broader product goals.

## 1. Lean project foundation

Create the documented Unity project, bootstrap scene, development scene,
placeholder content path, source-control rules, and smoke tests.

## 2. Vertical slice

Prove one compact, playable real-time survival scenario using standard
GameObjects and C#. It must demonstrate an autonomous crowd, one evolving
disaster, indirect player intervention, readable cause and effect, and a
percentage-saved result. The exact setting, disaster, action set, and success
threshold remain design decisions to make before implementation begins.

## 3. Refinement and debugging

Make cause and effect understandable through seeded runs, event inspection,
basic runtime diagnostics, repeatable tests, and standalone-build profiling.

## 4. Further content

Use the proven interaction model to explore additional content. The eventual
structure, such as authored levels, a campaign, or a sandbox, is deliberately
deferred until the vertical slice is proven and refined.

## 5. Profiling-led scale upgrade

Only move high-volume simulation or rendering to ECS/Burst/Entities Graphics
when profiling a representative scenario shows the GameObject implementation
cannot sustain the selected frame-rate target.
