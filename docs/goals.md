# Goals and milestones

## 1. Lean project foundation

Create the documented Unity project, bootstrap scene, development scene,
placeholder content path, source-control rules, and smoke tests.

## 2. Vertical slice

Prove one compact, playable crowd situation using standard GameObjects and C#.
The slice's exact scenario and player objective are design decisions to make
before its implementation begins.

## 3. Refinement and debugging

Make cause and effect understandable through seeded runs, event inspection,
basic runtime diagnostics, repeatable tests, and standalone-build profiling.

## 4. Small authored levels and sandbox

Turn the proven interaction model into concise puzzle levels, then expand the
same systems into a controlled free-play mode.

## 5. Profiling-led scale upgrade

Only move high-volume simulation or rendering to ECS/Burst/Entities Graphics
when profiling a representative scenario shows the GameObject implementation
cannot sustain the selected frame-rate target.
