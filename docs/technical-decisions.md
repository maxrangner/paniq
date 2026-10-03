# Technical decisions

## Chosen foundation

| Area | Decision | Reason |
| --- | --- | --- |
| Engine | Unity 6.3 LTS (`6000.3.24f1`) | Supported Windows workflow, C#, mature 3D tools, and a future ECS path. |
| Rendering | URP | A scalable Unity rendering path suitable for desktop and later mobile quality profiles. |
| Initial runtime model | GameObjects and C# | Keeps the prototype approachable and fast to iterate. |
| Input | Unity Input System | Keeps device input separate from game intent. |
| Tests | Unity Test Framework | Supports edit-mode logic tests and play-mode scene-flow tests. |
| Version control | Git | Text Unity assets are tracked; LFS waits for large source media. |

## Level mode, and the order of the audit (2026-10-03)

The owner: "if every change is several hours I can't progress ... get one
level working and THEN work the fingerprints etc when the level is done."
Measured: the hours went into proving numbers about a level still changing
shape (machine-played rounds before the owner played, the office kept
byte-identical behind switches, documents and versions per batch), not into
the 6.5-minute suite. The owner's decisions when asked: rebuild how people
decide (the audit's one task model) **before anything else**, and **set
aside** the behaviours the loop level does not use.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Level mode** | Replaces *Sketch and keep* (binding text in `AGENTS.md`): compile check, smoke check, local save point, three-line report per change; proving once, in a hardening pass | **The owner's rule** | The level is done |
| **The smoke check** | `SmokeEditModeTests`, category `Smoke`: the five levels, seed 41, once each; a fire round must end within three minutes (the old office excepted, known not to); about twenty seconds | The quickest test that a broken build never reaches the owner | It grows past a minute |
| **Other levels may change** | No new "old way" switches; the office and the test levels take the new rules too | A switch per rule doubled the code for a level nobody plays now | The office is played again |
| **Measurements on request** | `LoopMeasurements` / `LoopPlayers` stay, `[Explicit]`, run only when the owner asks a question they answer | Machine players are not people; their numbers gated every hand-over | Never as a gate |
| **Cards and purse deleted** | Removed from the code, the levels and the screens; git history keeps them | The owner removed cards on 2026-09-30; the purse still ran every tick and three test levels quietly switched it back on | A level wants cards |
| **Set aside** | Keycard fetching, the cruel wedging doors, toilet trips, the brave fighting fire unasked: not carried into the rebuilt decisions | **The owner's choice**; saves two to three days of the rebuild | A level needs one back (about a day each) |
| **Test tools** | A comma filter from Bash runs every name; each half keeps its own results file; an inconclusive test fails; the compile check says "open the editor once" instead of hundreds of errors, compiles `Authoring` as the player does, and reads the version define from the installed editor | Audit G4-G7: each cost a test cycle | Never |
| **Also set aside** | Leaders no longer send anybody at the fire with a bottle (`TryOrderTheFireFought`, `LeadersLeaveTheFireAlone`, `OrderedFightMinimumBravery` gone); nobody locks the building's way out behind them (`PeopleLockTheWayOut` gone; inside doors still) | The same as the owner's set-asides: fighting the fire is the hand's to ask, and one bully locking the way out made a whole round a total loss. The loop level already had both off | A level wants them |
| **The one chooser** | `Tasks` (one table of what each activity is), `TaskChooser` and `ITaskOption` (carry on every tick; take up only at a decision moment, `AgentIntent.NextChoiceTick`), `Tasks.End` (done / gave up / interrupted, the same for every task) | Audit A1 and D: options were asked to take themselves up every tick with routes inside; the hand's give-up rules differed by task | Every option scores itself instead of keeping a fixed order |
| **What brings a decision forward** | The hand reaching somebody, or their conviction crossing the answer line; a wind-up that passed; an order shouted at them; anything that already called `ThinkAgainSoon` | Without these, a person would answer the hand up to a second late | Never |
| **Orders and noises as offers** | A leader's "break that door" is taken up by the person in their own turn a reaction later (`LeaderBehaviour.TakeUpAnOrder`); a noise is turned to in the hearer's own turn (`SoundSystem.TakeUpTheLook`), and waits while they are getting into or out of a chair | **The owner's rule**: nobody reacts on the tick a thing happens (audit E1, E2) | Never |
| **Everybody's rules, from the loop level** | Their own dice, choices about the heat that stick, heat judged three metres from a door, the long way round weighed, dead ends weighed, 6 % freeze for good, one click wakes the frozen, a held door walked through, the hand let go strands nobody, no fire through walls | Level mode: rules about how people behave are no level's own | A playtest says one of them is wrong |
| **Dragging a body** | Swung into line by a turn (`PhysicsWorld.SwingToward`), never hauled into a wall; getting up only onto room floor or a doorway | Audit E5 (a rotation written every tick), and a body dragged into a wall got up outside the building | Never |
| **The calm's choosing** | The same chooser (`CalmBehaviour.BuildIdeas`): the hand's push, its pull, a stop after a walk, then one roll in bands as before (tidy, sit, home time, desk, chat, look round), a stroll by default | One way of choosing for everybody, without changing how the calm day feels | Playtesters find the calm day dull or busy |
| **A poke in a wind-up** | A poke while somebody is going stiff catches the wind-up ("caught in time") rather than waking them as if frozen | One click now wakes the frozen; without this the poke beat the tell to it and the end card counted it wrongly | Never |
| **Timers given a person's own length** | The gap between one person's shoves, the rest after heaving a table, the steps of sitting down, a socket's crackle (audit E3). A person's own idea (going home, a chat) still starts on their own tick: it is a deed, not a reaction | **The owner's rule**: nothing happens to a group on one tick | Never |
| **Play-mode wiring tests** | Ask for the office by name (`LevelSession.RequestLevel`) | The loop level opens first since 2026-10-03; these check the office's doors, bin and start card | Never |
| **Saving in level mode** | Save points folded into one ordinary commit at the end of each working session; nothing pushed unless the owner asks | **The owner**: the level will take weeks and many commits; a history of sessions reads, one giant commit at the end does not | The owner wants it otherwise |

## Alignment with the three requirements for the finished game (2026-09-24)

The owner stated three requirements for the finished game (recorded in the
[game vision](game-vision.md#decided-three-things-the-finished-game-must-be))
and asked that the documents and the work line up with them. This is the check
of the foundation against each, and the two decisions the check produced.

### 1. Very optimised for large crowds, with a lot of emergent behaviour and events

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| One deterministic tick; a spatial index behind every per-person question; flow fields shared by everybody heading the same way, with a fixed per-tick budget; a display that allocates nothing a tick; fire drawn in batches; a causal event log where every event names its cause, which is what makes the chains readable and what the economy pays on. Measured at 500 people panicking in the editor: about 12 ms a tick, 5 of them physics | People are about 25 scene objects each (fine at 200, unmeasured at 500); steering and wayfinding have never been profiled on a large floor; every number is an editor number | -- | Large is the stated goal: 200 to 500 people on 30 to 50 rooms. The evidence gates for scale tooling (below, under "Deferred technology") stay: profile first, adopt only when the current approach blocks the intended scenario |

### 2. Flexible for different dangers; a reaction is a feeling about a situation, never a response to a named event

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| Danger is generic: `IThreat` answers what a frightened person may ask of any danger and `Threats` answers across all of them. Fear changes (calm, alert, scared, freezing) come from perception (sight of a threat), sound (a noise with a position and a reach: a yell close enough to be understood alarms, a thud only turns heads, a bang frightens) and contact. None of these read an event's name: a bang frightens because of its reach, not because it is called "MicrowaveExploded". The event log records causes; it does not drive reactions. The economy (`InfluenceSystem.UproarTierOf`) keys on event types, which is right: it is the score, not the crowd | The inner life is one axis, fear, shaded by seven traits and a temperament. The panic options (flee, fight the fire, help, lead, sound the alarm, barricade) are a fixed list, and some are tools for one danger (extinguishers, flammables) and belong to it. Feelings spread one way only today: a yell alarms | Anger, trust in a leader, curiosity: none exists | **Feelings are named now and built as dangers need them.** The vision names them; each arrives as per-person state with the first danger or card that needs it, as its own stone. The rule is a design constraint above: no rule in the crowd may switch on an event type. Revisit if a danger cannot be expressed as answers to the threat questions plus a feeling; that is the signal to extend the interface, not to special-case the danger |

### 3. Handcrafted levels with dynamic scenery and props, possibly on more than one floor

| Agrees | Partly | Not yet | Decided |
| --- | --- | --- | --- |
| Levels are placed by hand (`PrototypeBuilding` in code, and the scene bake tool that reads placed objects into scenario data); the level asset and session exist. Props are physical bodies: shoved, thrown, tipped, broken, burnt. Tables are bodies too, and the walkable floor is redone when one is shoved. Walls are blown through; doors break | Rooms are rectangles, joined into L and T shapes by archways; authoring in C# will be the slow part of a large floor | Everything is one storey: a position is X and Z (`LogicalPosition`), rooms are flat rectangles, the navigation squares, the fire squares and the index are one layer, and the physics scene has one floor | **Storeys are prepared for now and built later.** From the next level onward, rooms and positions carry a storey number and a stair is a kind of door between storeys, while every level is still one storey. Stairs, lifts and falls between floors stay unbuilt until a level asks. Why now: a few days once, before a big level exists; a month afterwards, because it touches positions, rooms, both grids, physics and the display in one go. Revisit never; the trigger for building stairs is the first level that wants a second floor |

## Older decisions

Decisions from finished stones are kept, unchanged, in `docs/history/`, so this
page stays short enough to read in full. Search there when you need the reason
behind older code:

- [Prototype 1](history/decisions-prototype-1.md) -- the fire-reaction office
  bring-up, and rounds, scoring and the design spine.
- [Prototype 2](history/decisions-prototype-2.md) -- the round, the playtest
  rounds, the cue system, the office floor, the cards and the alarm.
- [Prototype 3](history/decisions-prototype-3.md) -- gameplay: the tower and
  the Director, the keycard, the hand and its four passes, tells, the test
  levels, the office re-dressed and the measurements behind each.
- [The review refactor](history/decisions-review-refactor.md) -- the six phases
  of the 2026-09-23 review and where the foundation stood afterwards.
- [Version history](history/version-history.md) -- every bump of the rules
  version and the building version.

New decisions are added to this page, under the current stone. When a stone is
finished, move its sections to a new `docs/history/decisions-<stone>.md`.

## Tooling decision: models are code (2026-09-25)

**Status: implemented and in use.** Merged into `main` on 2026-09-26 with
its first model, the wet-floor sign (below). Models are not drawn in the
game yet; that is a later stone.

**What the owner asked for.** A way to describe a thing in words, or show
concept art, and get a real mesh for the game out of it; to change a model
afterwards by asking again; real meshes rather than stacked primitives; crude
models with few or no limbs; some props with simple motion and people with a
little deformation. Blender is installed. Only the pipeline and its
documentation were to be built, no model.

**What was built.** The [model pipeline](model-pipeline.md): every model is a
short script under `tools/models/models/` that `tools\BuildModel.ps1` runs
through Blender in the background, which checks it, exports an FBX into
`Assets/Paniq/Content/Models/`, draws a preview into `docs/models/previews/`
and writes a report. Unity applies fixed import settings
(`ModelImportSettings`), and a calibration box under the test fixtures is the
ruler that `ModelsEditModeTests` measures. It is tooling beside the
prototype, not a stone of it, built on a branch from `main`
(`chore/model-pipeline`, merged 2026-09-26) because it shares no code with
the prototype. Everything below was chosen on
the owner's behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Models are scripts | One Python file per model, written by the assistant from the owner's words and pictures, built into a mesh by Blender's own scripting (`bpy`, `bmesh`). The script is the source; the `.blend` is a disposable copy for looking | The alternatives fit worse. **Image-to-3D services** make dense organic meshes that fight the crude doll's-house style, cost money, need an account, are not reproducible and are a service the working agreements forbid without a documented need. **Hand-modelling in Blender's window** is not what was asked for: the owner wants to describe, not sculpt. A script is text: the assistant can write it from a picture, diff it, change "20 cm taller" in one line and rebuild in seconds, and the assistant can judge the result from a rendered picture, which works for blocky shapes | A model wants organic detail no script reads well (a face, folds of cloth); then that one model becomes a hand-made `.blend` with its script retired, a choice per model |
| Blender 5.2 LTS, found by path | `BuildModel.ps1` looks for `BLENDER_PATH`, then the newest `Blender 5.*` under Program Files, the same shape as `CompileAgainstUnity.ps1` finding Unity. Blender's bundled Python 3.13 and numpy are used; no separate Python is installed. A build with another version warns | Blender is free, already installed, and is where the FBX exporter and the mesh operations live. Pinning the LTS keeps bevel topology, and therefore the geometry hash, stable | Blender 5.2 LTS stops being maintained, or a needed operation only exists in a newer version; then bump the pin and rebuild every model with `-All` |
| FBX, not glTF, not `.blend` | Blender exports FBX with its built-in add-on; Unity 6.3 reads FBX natively. glTF would need a Unity package, and Unity's manual says `.blend` files "should not be used in production" and need Blender installed on every machine that opens the project. [Support for proprietary model file formats](https://docs.unity3d.com/6000.3/Documentation/Manual/HOWTO-ImportObjectsFrom3DApps.html) | No package, no service, nothing to install on another machine to open the project | A package is wanted anyway for something else (runtime glTF loading), at which point the recipe below is the only thing to redo |
| The export and import recipe | Blender side: the model is turned **+90° about up** in its mesh data, then exported forward −Z, up Y, *apply transform* on, scaling *FBX All*, triangles, no animation, one material slot per surface (`export.py`). Unity side: file units and file scale on, `bakeAxisConversion` on, material import off, colliders off, animation off, blend shapes on with calculated normals, tangents calculated, not readable, no compression (`ModelImportSettings`). [Model import settings](https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Model.html), [ModelImporter](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ModelImporter.html) | Blender is Z-up and right-handed, Unity Y-up and left-handed, and the classic result of getting this wrong is a model 100 times too big or lying on its side. The ruler measured it: a Blender point (x, y, z) arrives in Unity as (x, z, y). The first guess (a −90° turn, on the belief that Unity mirrors X) put the front on −X; the ruler caught it before any real model existed, which is exactly what it is for | `ModelsEditModeTests` fails after a Blender or Unity upgrade. The doc's last section says which setting each symptom points at |
| The ruler | `tools/models/examples/CalibrationBox.py`: a 1 m box with a bump on top, a bump on the front (a second surface), a bump on its right side, a flap hinged along the back top edge and one shape key, committed as a test fixture (never as content). `ModelsEditModeTests` asserts size, floor, up, front, right, identity transforms, the hinge, the blend shape, the surfaces as sub-meshes in report order, a texture map and tangents on every mesh, no collider, no material, the triangle count against the report and the importer settings | It is the one mesh made at this step, so the first real model is not the first test of the whole path. A fixture is not a model: it draws nothing in the game | Never; extend it when the kit gains a new kind of thing (a sliding part, a second deformation style) |
| Moving parts are hinge parts, not clips | A part is a named child object with its pivot on the hinge; the game turns or slides it in code from simulation state. Blender's exporter can bake keyframe animation, and Unity can import clips, but both are switched off | It keeps the standing rule of procedural motion and the simulation/presentation boundary: the simulation decides that a lid is open, the display draws it. A clip would be a second source of truth for the same motion | A motion is wanted that no code-driven turn expresses, such as a mechanism with several linked parts; then import a clip for that prop and sample it in code without an Animator |
| Shape keys are the one deformation | A model may carry named shape keys (blend shapes in Unity); the game sets their weights in code. This amends the "Character animation: procedural only" row above: still no rigs, no Animator controllers, no animation package. **Prerequisite for the first person model:** a mesh with blend shapes imports with a `SkinnedMeshRenderer`, and `PresentationUtility.AddMaterial` walks `MeshRenderer` only, so the see-through silhouette would be lost until that one word becomes `Renderer` | The owner asked for a little deformation on people (a lean, a flinch). A shape key is a stored second position for every vertex with no skeleton, which fits crude limbless bodies and the same code-driven style as the bob and waddle | Profiling a standalone build shows skinned rendering of hundreds of people costing frame time (unlikely at 300 triangles each); then move the deformation into a vertex shader driven by the same weights |
| Triangle budgets | A prop may spend 500 triangles, a person 300; the build refuses more | A style cap, not a performance one: 500 people × 300 triangles is trivial for any graphics card. It stops a model drifting towards detail the doll's house does not want | A kind of prop genuinely needs more (a staircase, a vehicle); raise its script's budget with a note there |
| Authored at true size, scaled by the view | A model is built at the thing's real size and declares its footprint and height; the build rejects a shape more than 5 % over. The simulation gives every prop one size number (`SizeMillimetres`), and the stone that draws models scales a model uniformly by that number over the model's footprint, as `BoxViews` scales primitives today | One rule for how a 400 mm box and a 900 mm vending machine relate to the simulation's idea of their size, decided before the first model rather than per model | A prop needs a footprint that is not square in the simulation; that is a simulation change (a rectangular footprint), not a model one |
| Ready for textures and materials, look not yet chosen (revised 2026-09-26) | **The owner decided:** later prototypes and the game give every item textures and materials that react to light; the crude prototype's flat colours are not the destination. Which look (painted-toy colour swatches, a painted picture per model, or both) waits for concept art, but no model may be a dead end. So every model is divided into named **surfaces** (`Body` by default; `Glass`, `Screen`, `Fabric` as the script says), each exported as its own material slot and imported as its own sub-mesh in the order the report lists; every object carries one **texture map** (Blender's Smart UV Project, 66°, islands at true relative size, packed into the unit square with a 0.02 gap, so no two faces share a spot on a picture); Unity calculates **tangents** (MikkTSpace) so normal-mapped materials work. Unity still imports no materials: the game assigns them | Replaces the first version's "colour comes from the game, no materials or textures", which was chosen on the owner's behalf and read the crude prototype as the final look; the owner corrected it. Named surfaces serve both looks (a swatch colour per surface, or a region of a painted picture); a unique texture map serves painted pictures and baked detail; tangents serve bumpy materials. Because models are scripts, a change of layout later is a kit change plus `BuildModel.ps1 -All`, never a model redone by hand. State tinting (`_BaseColor` in a property block) multiplies over a textured URP Lit material, so frightened red and charred black keep working | The look is chosen: a swatch palette may want swatch-snapped texture maps, painted pictures may want a fixed pixel density; a stone that assigns materials decides between assigning by sub-mesh index and turning on material import with a name remap. Also revisit if many surfaces per person show up as draw-call cost in a standalone profile at 500 people |
| One file per thing, named after it | `<PhysicsObjectKind>.fbx` for a prop, `Person.fbx` for people; the script's name and the model's name must match, and the build refuses otherwise | The stone that puts models on screen can look a mesh up by its kind's name without a table | Several looks for one kind (three different chairs); then a suffix and a small table |
| Previews and reports are committed; copies are gated by a hash | `docs/models/previews/<Name>.png` (the game's view and a front view) and `<Name>.json` are committed. The FBX and the picture are only rewritten when the geometry hash or the export recipe changed, because an FBX carries a timestamp and never matches byte for byte | The owner sees every model in the repository without opening anything, and `-All` does not rewrite every file | Previews get large (many models); then keep them out of git and render on demand |
| No Git LFS yet | Crude FBX files are tens of kilobytes and previews under half a megabyte, so the foundation's rule stands. Reference pictures are asked to stay under 5 MB each | Nothing large has entered the repository | `docs/reference` passes about 50 MB in total or one file passes 5 MB; then LFS for `*.png`, `*.jpg` and `*.blend` |

### The first model: a wet-floor sign (2026-09-26)

**The owner asked for** a "slippery when wet" floor sign, about waist high,
from a photo of a yellow folding A-frame sign (`docs/reference/WetFloorSign.jpg`).
It is `tools/models/models/WetFloorSign.py`: two panels leaning from a
rounded top bar, each with a handle hole, a recessed field with a raised
rim, a printed label and two feet, 228 triangles. Chosen on the owner's
behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Height | 900 mm to the top of the bar, 420 mm wide, each panel leaning 13°, so the feet stand about 440 mm apart | "About waist high" on an adult is 0.9–1.0 m; models are built at real size and the game scales them, so the game's 1 m-tall capsule people do not set the number. Real signs of this kind are 600–650 mm; the owner's waist-high wins | The sign reads too big or too small once it is on screen beside people; one number |
| Name | `WetFloorSign`, although `PhysicsObjectKind` has no such kind yet | The model is named for the kind it would be, so the stone that adds the prop finds it without a table | The kind is added under another name; rename the script |
| Two surfaces | `Body` (the yellow plastic) and `Label` (the printed warning), a label on both faces | The photo's warning is printing on a plain panel; a separate surface lets it become a picture later without touching the plastic. Real signs are printed on both sides | Never, unless the look wants the ribbed chevrons at the foot as a third surface |
| It does not fold | No hinge part; the sign is one rigid mesh | Nobody asked for it to fold, and a knocked-over sign tumbles whole under physics | Somebody is meant to fold it or kick it flat |
| `rotate` added to the kit | `Piece.rotate(degrees, axis, about)` turns a piece round a line, the right-hand way | An A-frame is two tilted panels, and every kit piece was upright. The docs already said to add kit operations when the first model needed them | Never; it is a general operation |

Blender's side is documented in the Blender 5.2 manual under
[FBX](https://docs.blender.org/manual/en/latest/files/import_export/fbx.html)
and [command line arguments](https://docs.blender.org/manual/en/latest/advanced/command_line/arguments.html);
`--background` with `--python` and `--python-exit-code` is how the build runs
without a window. Headless rendering was checked on the owner's machine:
EEVEE renders in the background there; the viewport-style render does not.

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
- A room or a position may carry a storey number; nothing new may assume the
  building is one storey (decided 2026-09-24, see the alignment section).
- A person reacts to a situation through a feeling; no rule in the crowd may
  switch on an event type (decided 2026-09-24, see the alignment section).
- Nobody reacts on the tick a thing happens, and nothing happens to a whole
  group on exactly the same tick: every reaction begins a few ticks late
  (`SimulationContext.ReactionLag`), no two people finish being startled on
  one tick, every fixed length of time a person spends goes through
  `SimulationContext.Jittered`, and any schedule several people share draws a
  seeded offset per person (the owner's rule, 2026-09-24, see the playtest
  fixes in [prototype 2's decisions](history/decisions-prototype-2.md)).

## Deferred technology

| Technology | Do not add it until |
| --- | --- |
| ECS, Burst, Entities Graphics | A profiled representative prototype scene cannot meet its frame-rate target because of crowd update or rendering cost. |
| AI Navigation | A prototype stone needs pathfinding that simple steering and obstacle avoidance cannot provide. |
| Cinemachine | The hand-authored camera prevents a required player experience. |
| Addressables | Content size, loading needs, or platform packaging makes direct references difficult to manage. |
| Mobile settings | The Windows prototype is stable and a target device is available for measurement. |
| Unity Physics (DOTS) | Standalone profiling shows the PhysX step over 3 ms a tick at 500 people and 1,000 things after the cheaper fixes, or replays must match across different kinds of computer. |
| VFX Graph | The built-in Particle System costs more than 2 ms a frame in the worst blast, or an effect needs tens of thousands of particles, such as smoke filling a building. |

Any addition above requires a short dated note here recording the measured or
feature-driven reason and the expected benefit.
