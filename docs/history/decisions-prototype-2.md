# Technical decisions: prototype 2

The decisions and fixes taken while building prototype 2, the round.
Moved here from [technical decisions](../technical-decisions.md) unchanged, in
their original order.

## Prototype 2 decision: building the round

Settled or chosen while building prototype 2's first five stones on
2026-09-22. The three marked **owner** were the owner's own choice; the rest
were chosen on their behalf and are recorded here so they can be overturned.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| How a round begins (**owner**) | The level opens calm and nothing happens until the player presses "Trigger event". No timer | The owner wanted a deliberate beginning and a chance to look around the office before setting it off. It also makes the button a part of play rather than a debug toy | A level wants a disaster already under way when the player arrives. It is one tick box on the level asset |
| What clears a level (**owner**) | 75% saved: fifteen of the twenty people. The owner asked that it stay easy to tweak | Just above what a do-nothing run manages, so intervening is worth it, while leaving room for "60% saved was a triumph" | Playtests say it is reliably out of reach or reliably trivial. It is one number on the level asset |
| What the high score remembers (**owner**) | One number per level: the best share of the crowd ever saved, in `PlayerPrefs` | The simplest thing to understand with one level, and the docs already accept that two runs are not strictly comparable | Runs become comparable, or the owner wants a per-seed record |
| Where the round lives | `RoundSystem`, in the simulation | Ending a round decides an outcome -- it is what turns the last survivors into people who were saved -- so the [simulation contract](../simulation-contract.md) applies to it in full | Never, while the contract stands |
| "Settled somewhere safe" | Alive, not on fire, in a room with no fire in it, and no path from any burning room to theirs through a door that is open, broken or blasted -- held for five seconds | It is the case the design names and the only one that can end a round that would otherwise hang for ever. The five seconds stop a round ending in the lull before somebody shoulders a door open | A hazard exists that can reach anywhere given time, or that does not travel through doors |
| A new outcome value | `Survived`, appended to `AgentTerminalOutcome` | The [agent state model](../agent-state-model.md) anticipated this. Appended, never inserted: a value's number is part of the replay fingerprint | Never; the rule is append-only |
| Trigger as a player command | `TriggerEvent`, appended to `PlayerCommandType`, costing nothing | Everything the player does has to reach the run as a recorded command or a replay cannot reproduce it. Free because it is not a card | Never, while replay matters |
| What a level is | A small `LevelDefinition` asset: an id, a name, which scenario, which physics feel, whether the hazard waits, and the clear target | The beginnings of a level system without building a level system. A second level is a duplicate of this asset, not new code | A level needs anything a scenario cannot express, such as its own cast or its own cards |
| Playing again | The scene is reloaded and the run rebuilt from scratch, with the chosen seed left in a static `LevelSession` that is not in the scene | The run owns a private physics world and a sceneful of built geometry. Throwing it all away is the only way to be certain nothing carries over, and it takes a fraction of a second | The reload becomes slow enough to notice, which would mean resetting in place instead |
| Pause | `Time.timeScale = 0`, with the camera reading the clock that ignores it | Everything freezes -- people, fire, smoke, sparks -- which is what "the scene freezes" should look like. The run steps its own physics world by hand, so this changes nothing about the size of a tick; it only stops ticks happening | A system needs to keep running while paused |
| Screens and buttons | Unity's immediate-mode GUI, the same drawing the existing counter and cards already use | No new package, and this prototype is explicitly "not art, menus, saving, or platform work". A real menu system is a vertical-slice job | The screens need layout, fonts or animation that immediate mode makes painful |
| Compatibility version 38 | Bumped from 37, content revision 45 to 46, and all ten replay fingerprints re-recorded | Marking survivors at the end of a round genuinely changes what a run produces, so the recorded numbers had to change. Done in the same commit, as [the workflow](../development-workflow.md) requires | Every behaviour change; the procedure is the point, not this instance |
| The building's outside | Drawn as a slab, a window band and the top of the storey below, then dark | The doll's-house look wants a model of a place, and a floor plan on a black background reads as a diagram. Presentation only: no simulation change, so no fingerprint moved | The cutaway-walls stone, which will want the outside walls to behave differently as the view turns |
| Camera numbers | Pan 14 m/s (slower when zoomed in), zoom in twelve notches from the full-building framing down to a sixth of it, tilt from 35.264 degrees out to 18 degrees in, and panning allowed 8 m past the building's edge | [Look and controls](../look-and-controls.md) says these are presentation values to be chosen when the camera is built. Chosen to feel unhurried at full zoom-out and still readable up close | Playtests say the view is sluggish, or that the close view is hard to read |

## Prototype 2 decision: the first playtest's changes

Settled or chosen on 2026-09-23, after the owner played the round. The rows
marked **owner** are the owner's own choice; the rest were chosen on their
behalf and are recorded here so they can be overturned.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Doors cost influence (**owner**) | Turning the key **50**, walking a door open **30**, pulling one shut **10**. Each click pays for what that click does, so unlocking a far-off door and leaving the people inside to open it themselves is cheaper than opening it yourself. A click nobody can pay for, or one the door refuses because something is wedged in it or somebody is standing in it, does nothing and costs nothing | Working a door was the player's commonest move and it was free, so there was never a reason not to fling every door in the building open. The owner set all three prices | The first playtest with them. The building's one way out starts locked, so opening it is **80 of the 100** the player starts with, before anybody has been saved: `InfluenceSettings.Starting` is the one number to raise if that reads as unfair rather than tense |
| Trigger stays free | Pressing "Trigger event" still costs nothing | It is the button that starts the round, not something the player spends influence on | The owner wants setting the disaster going to be a priced decision |
| Fire burns through a shut door | A shut door with flames within 2 m of the doorway takes one tick of scorch a tick, and at **900** (eighteen seconds) it is gone for good, falling away from the fire. It darkens as it goes, using the same leaf colour a battered door already used, so the failure is visible before it happens | A shut door stopped fire outright and for ever, so a building with its doors closed had rooms nothing could ever reach and shutting yourself in was a way to win. Eighteen seconds is longer than a chair (three) or a table (five), because a door is a slab in a frame, and short enough that it only ever buys time. Only floor fire counts, not a burning chair leaning against the door | A door beside a bonfire reads wrong because the chair against it is ignored, or playtests say the reprieve is too long or too short |
| When a round ends | Everybody out or dead, **or** the whole building doing nothing at all for **1500 ticks** (thirty seconds). "Nothing at all" means nobody moved 400 mm, nobody is on their feet and moving, **nobody is pressing forward and getting nowhere**, nobody is working at a door or dragging somebody or fighting the fire, nobody is alight, and the fire, the burning things and every door are unchanged | The round used to stop as soon as everybody left was out of the fire's reach, which ended it on top of people still walking to the door — and with doors that now burn through, "out of reach" is temporary anyway. **The owner's instruction was that a queue must never end a round**, which is why blocked-but-trying is one of the signals: a crowd jammed in a doorway covers almost no ground, and distance alone would have read that as a settled building | Playtests find a case where the building is genuinely finished but something keeps the clock alive, or a round that should have ended is still running |
| Fire remembers a door can open | A burning square is retired for good once everything around it is alight or walled off. That was safe while a shut door stopped fire for ever; it is not now. Every door that becomes a way through -- opened, forced, blasted or burnt through -- wakes the retired squares in the rooms on both sides of it | Found by the burn-through test: fire that had filled the storage closet stayed in it for the rest of the run however wide the door was afterwards thrown. The bug was always reachable (somebody opening a door onto a fire pressed against it) and burning doors only made it common | Never, while squares are retired at all |
| Alive inside at the end (**owner**) | Still counts as saved, unchanged | The owner chose it. It keeps the score meaning "how many were alive at the end", which is easy to explain and forgiving while the prototype is rough | The owner wants getting people *out* to be the only thing that scores |
| Nobody shuts a door on their own way out | A person never pulls shut a door that stands on the route to the way out they have settled on — **unless the flames have already reached it**, in which case that route was never going to work and shutting it may save them. Somebody with no way out left shuts doors freely | People stopped on their way out to pull doors shut and then dithered, because shutting a door also crosses it off their own list of ways out. The exception is deliberate: shutting the fire out of the room you are cornered in is the mechanic **the owner said he likes and wants kept** | A playtest shows people failing to contain a fire they plainly should have |
| Who slams a door behind them (**owner**) | Evil **8**, raised from 7. Locking it still needs Evil 9 | Four of the twenty authored people cleared the old bar, so the building read as full of door-slammers rather than as containing one. The owner chose 8. No authored personality was changed | The owner wants it rarer still, which is one number, or wants the trait spread itself retuned |
| Furniture never breaks | Chairs and tables are shoved, tipped, rolled and flipped like any other body, and are still furniture when they stop. The table-smashing path and the reserved wreck heaps are gone; chairs simply have no breaking point. The generic wrecking machinery **stays**, because the electrical things that go off still mangle themselves and nobody asked for that to change | The owner asked for table and chair destruction removed. Tables were already real rigid bodies that shove, tip and flip, so this is the removal of a path rather than the building of one | A stone wants breakable scenery on purpose, which would author it rather than making all furniture breakable |
| A chair is furniture, not clutter | Anything people sit on is excluded from calm tidying, by the `CanBeSatOn` row the settings table already had. A frightened person can still wedge one against a door | Tidying is offered before sitting, and in an office the nearest liftable thing is almost always a chair, so the room spent its day carrying its own chairs about and nobody ever sat down | A scenario wants furniture moved on purpose, such as a room being cleared |
| Aiming a card at a person | The pointer is matched against where each body is **drawn on the screen**, within 45 pixels of the middle of the capsule, rather than against a spot on the floor | Cards were aimed by intersecting the ground plane and looking for somebody within half a metre of that point. A body is drawn a metre in the air, so with the camera tilted low the floor under somebody's chest is two or three metres behind their feet: the player clicked a person and the game searched empty carpet. Place-aimed cards still read the floor, which for them is correct | The view stops being a single camera, or people stop being drawn as one capsule |
| Seated bodies | The same body, unsquashed, with its middle dropped to 0.30 m and leaning 12 degrees back. What goes below the floor line is inside the chair | A seated body was flattened to two-thirds height at full width, which turns a capsule into a hunched blob, and then stood on the 0.45 m seat as well — so a sitting head ended up *higher* than a standing one | People stop being capsules |
| Leader and follower marks | A green star over somebody other people are following. Whoever is following them wears nothing | Both wore the same green arrow at different sizes, so a leader and their followers looked alike at a glance. One mark in a knot of people is the one worth looking at | Following needs to be readable on its own, rather than by watching who the star is moving toward |
| Where the instructions live | Every key reminder and the guide to the marks moved off the running screen and into a panel shown only while paused | Attention aids are deliberately thin in this game (see the row above on helper markers), and three rows of instructions left very little of the office to look at. Pause is the moment somebody is reading rather than playing | The pause panel grows past one screenful |
| Reading the round back | A scrollable list on the end card, built from the causal log the run already keeps. Background chatter — the fire creeping a square, an extinguisher hissing, people bumping shoulders — is folded into one line apiece with a count; a button unfolds everything. People are named by the number over their head, doors by the number the hover hint uses | **The owner chose** the story over the raw list. Unfolded, a round runs to thousands of lines and the fire's own spreading buries everything a person did. Presentation only: it reads the log and decides nothing | The retelling stone, which will want to say what a stretch of the log *meant* rather than list it |
| Compatibility version 39 | Bumped from 38, content revision 46 to 47, and all ten replay fingerprints re-recorded | Six of the rows above change what a run produces. Done in the same commit, as [the workflow](../development-workflow.md) requires | Every behaviour change; the procedure is the point, not this instance |


## Prototype 2 decision: a camera you can swing, and the log that never opened

Chosen on 2026-09-23 on the owner's behalf, after the owner asked for a camera
that turns under the mouse. None of these is a rule the simulation obeys, so
none of them touches a replay: this is all presentation.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Free rotation, kept | Holding the **right mouse button** and dragging swings the view to any angle. Letting go does **not** spring back to a corner | The view could only sit at one of four corners, so anything behind a wall could not be leaned around — the nearest corner was as close as the player could get. Springing back on release would undo the one thing the drag is for | The owner finds the view drifting off-square annoying in ordinary play, in which case a gentle pull back toward the nearest corner after a pause is the smaller fix |
| A drag is a quarter of a degree per pixel | `CameraRig.DegreesPerDragPixel = 0.25f` — a full turn is about a screen and a half of pointer travel | Fine enough to aim at one desk, coarse enough that turning right round is not a chore. Chosen by feel, not measured | Playtesters overshoot constantly, or say turning round takes too long |
| Q and E measure from where the view is **going** | Each press lands on the next corner view round — `45 + 90n` — measured from the angle the view is easing toward rather than the angle it has reached | Measuring from the shown angle loses the second of two quick taps to the first one's travel, so a double tap turned one corner and a bit instead of two | Never, while the view eases at all |
| Five pixels tells a click from a drag | A right button that travelled under `CameraRig.DragPixels = 5f` before coming back up is a click and puts the card down; more than that is a turn and leaves the card in hand. The card is dropped on **release**, not on press | The right button already meant "put the card down", and a drag that also threw the card away would be unusable. At the moment of pressing nobody yet knows which one it is, so the decision has to wait for the button to come back up | A shaky hand drops cards it meant to keep, or a small deliberate nudge of the view throws one away |
| The zoom tilt waits until halfway | The camera holds the isometric 35.264 degrees for the first half of the wheel's travel and eases the whole way to 18 degrees over the second half, smoothed at both ends (`CameraRig.SwoopAt`) | The tilt used to start at the first notch, so a single click of the wheel both moved and tipped the view and read as a lurch rather than a step closer. Coming straight in first makes a small zoom feel like a small zoom | Playtests say the swoop is too sudden when it does arrive, which would move the halfway point rather than change the shape |
| The round read-back is owned by the presentation | `RoundScreens` sets `WantsTheLog`; `RunPresentation` takes it, clears it, opens `EventLogScreen` and draws it last | The flag was set and never read, so the end card's **What happened** button did nothing at all. Drawing it last is what puts the story over the end card rather than under it | A second screen wants the same treatment, at which point the request flag becomes a small stack |
| The log is for looking, not acting | While the log is up the pointer reaches nothing in the run, and Escape closes it | The same rule pause already follows, and a click meant for the list should never also unlock a door behind it | Never |

## Prototype 2 decision: the new floor plan

Chosen on 2026-09-23. The owner chose the shape of the floor and where the fuse
box sits relative to the way out; everything below was chosen on their behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| The floor plan (**owner**) | A corridor past four rooms, Ting at one end: the way out up one arm, a dead end down the other, and the maintenance room at the far end of the building | The owner asked for four rooms and a corridor, and chose the fuse box far from the way out so that reaching it is a deliberate trip to the back of the floor | Playtests say the single corridor reads as a queue rather than as tension |
| The office and the closet do not move | Room 5001 keeps `(-6000..6000, -6000..6000)` and stays `Rooms[0]`; room 5002 keeps its rectangle and door `2002` | `Rooms[0]` is load-bearing in three places: the fire's spawn area is measured against it, the geometry treats the first room as where the fire starts, and the replay fingerprint kicks boxes at its centre. Beyond that, twenty-seven test files write coordinates inside the office longhand | A room genuinely has to move, at which point the `TheBuilding` test helper is the thing to widen first |
| Door `2008` keeps its number | The one locked way out is still door `2008`, moved from the meeting room's east wall to the north end of the T | The replay fingerprint helper opens `2008` by name, so holding the ID steady is what lets recorded runs stay comparable across the move | Never, while `2008` means "the way out" |
| Archways | A new `IsOpening` flag on a door definition makes it a permanent opening: `IsHole`, `Placed`, and `Broken` from tick zero. It reuses the machinery a blown-open wall already had | A T-shaped corridor is two rectangles, and joining two rooms required a door. A swinging door in the middle of a hallway is wrong, and a wall there would have made the T impossible | A level wants an opening that can be closed, which is a different thing and should stay a different thing |
| The archway is 2.4 m of a 3 m corridor | Not the full width | A doorway has to leave a body's width of wall at each side or the validator refuses it, so an opening can never be the entire wall | The rule about stubs of wall changes |
| Stalls are 1.5 m square with 800 mm doors | Each stall is its own room off the bathroom's south wall | The engine's stated minimum door is 600 mm, but a 600 mm gap fails the navigation check that actually decides whether a body fits, because clearance is measured from square centres. 800 mm clears it in the worst alignment by about 50 mm, so it survives the building being moved | Navigation squares change size |
| The fire's spawn area may be any room | `Validate` used to require the rectangle inside `Rooms[0]`; it now requires it inside *some* one room, clear of the walls | The danger is about to be allowed to start anywhere on the floor, and the first room is no longer special | Never |
| Content revision, not compatibility version | `ContentRevision` 47 -> 48; `SimulationCompatibilityVersion` stays 39 | `docs/simulation-contract.md` reserves the compatibility version for changes to the *rules* -- the step, the command envelope, the generator, the tick schedule, the meaning of an event. A building is content, which is what the content revision is for. The ten fingerprints were re-recorded either way, because they are recorded against this scenario's content | A change to the floor plan ever changes a rule, which it should not |
| Where the danger may start (**owner**) | Any of the four rooms, never the corridor. `FireSettings.SpawnBounds` (one rectangle) became `SpawnAreas` (a list), with one area well inside each room | The owner asked for the fire to start in more randomised places and said a finished level would have a preset area for it; a list of preset areas is that idea, and a level that wants one spot still names one | The owner wants a room excluded, or the corridor included after all |
| One area draws nothing for the choice | `FireSystem` skips the "which area" draw when a scenario names exactly one, rather than making it and throwing it away | Nearly every test pins the fire to one spot. Drawing and discarding would have shifted every one of their runs for no reason, because the generator advances whether or not the answer could vary | Never; a draw whose answer is known is not a draw |
| The drawn spot is nudged onto floor | A point that lands on a square whose middle is in a wall walks outward, ring by ring in a fixed order, to the nearest square that is floor in some room. No random numbers | A spawn area is a rectangle, and a rectangle inside a room can still contain squares whose middles fall in the brickwork. A fire lit there sits in a wall doing nothing | Never |
| Exit signs are drawn flat, not upright | A green plate with a white chevron, lying face-up a little above head height | The camera looks down on the building and can now be swung to any angle. A sign mounted upright on a wall is edge-on and unreadable from half of them; a flat one reads from all of them | The view ever stops looking down on the building |
| Exit signs mean nothing to the run | They are authored content the display reads and the simulation never looks at | People route by the navigation grid. A sign that changed where anybody walked would be a second, quieter pathfinder disagreeing with the first | A level wants signs that mislead on purpose, which would be a rule and would belong in the run |
| The cable is authored, not worked out | Each run is a list of corners in whole millimetres on `PowerLineDefinition`. The run measures its own length from them; the view draws the spark at the same fraction along the same corners | How long the spark takes is an outcome of the round -- it decides when the next socket pops -- and where the spark is drawn is a picture. With one route and one number they cannot drift apart | A level wants cable it did not draw by hand, at which point routing it becomes a tool rather than a rule |
| Cable belongs to the things it joins | A run whose ends are not in this building is simply not there, rather than being refused | Dozens of tests replace the clutter with two boxes and a chair. Making each of them also delete the wiring would be churn for no safety: cable to a socket that does not exist is not a mistake, it is an absence | A building wants wiring to something that is not a socket or a fuse box, which would be a mistake |
| The spark crawls at about two metres a second | `PowerSettings.SparkSpeedMillimetresPerTick = 45`, down from 120 | At six metres a second the spark outran everybody in the building: the whole chain went off within seconds and a test round with every door open lost nineteen of twenty people and saved none. At the slower speed the same round saves nine. A fuse that outruns the people watching it is a delayed explosion, not a fuse | Playtests say the wait is boring rather than tense |
| A new phase in the tick, and a new compatibility version | `power.Advance()` runs in phase 2, immediately after `fire.Advance()`. `SimulationCompatibilityVersion` 39 -> 40 | `docs/simulation-contract.md` names the tick schedule as one of the things that forces the version. Going after the fire leaves the fire's random draws exactly where they were, so the only replay movement comes from the cable actually doing something | Never; a phase's place in the tick is part of the contract |
| The fuse box card aims at a place | `PlayerCommandType.PopFuseBox` carries a point, and the card finds a fuse box within 2.5 m of it | The pointer already has a place path for TNT and the extinguisher, and a floor has one fuse box. Aiming at a thing would have meant a third way of picking targets for no gain | A building has more than one fuse box |
| A refused card writes nothing | The reach is checked before the event is appended, not after | The log only ever grows, so it cannot record something and then take it back. A card that does nothing must leave no trace, the way every other refused card does | Never |
| A menu command the bridge drives never asks | `RewriteScenarioAsset` checks `SessionState` for the flag the test bridge sets, and takes the yes as given | Driving that command from a script put a modal dialog up in an editor nobody was watching, which froze Unity -- not just the command, but every later request and recompiling with it -- until somebody noticed | Never; asking somebody who is not there is worse than not asking |

## Prototype 2 decision: getting out of the chair, and signs that mean something

Chosen on 2026-09-23, after a playtest of seed 42. The owner chose how a
sitting person should look and asked for the signs to steer people; everything
else below was chosen on their behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Nobody is shoved backwards out of a seat | A frightened person rises where they sat. `ChairBehaviour.Forget` used to place them 800 mm along `Heading + 180` in one tick, and the panic path is now its own `StartLeapingUp`/`UpdateLeapingUp` pair instead of a timer that ended in that teleport | Six people round the meeting table all faced their table, so "backwards" was "at the wall" for every one of them, and they went on the same tick. The view slides a body between one tick's position and the next without turning it, so 800 mm in one tick drew as a smooth backwards glide. The owner reported it as the meeting room floating into the walls | Somebody genuinely needs to be thrown out of a chair, which is a shove and should read as one |
| The chair still goes over | `KnockOver` is unchanged and still fires when they leave the seat | It is the tested, deliberate difference between getting up and being frightened out of it. Only the *person* stopped teleporting | Never |
| The graceful stand-up stays calm-only | `UpdateStandingUp` is still reached only from `CalmBehaviour` | It rides the chair back out from under the table over about twenty ticks, which is what somebody who is not frightened does. Wiring it to the panic path would have kept the backwards travel and merely spread it over half a second | The calm rise is seen to slide too, at which point it is the same fix again |
| A seated body rests on the seat | Drawn from the 0.475 m seat surface upward, 0.62 m from end to end, upright | It used to sit with its middle at 0.30 m, which put 200 mm of a standing-height body below the floor, and tilt 12 degrees -- forward into the table, though the comment claimed it leaned back into the chair | The people and the furniture are ever drawn at one scale |
| A sitting head ends up above a standing one (**owner**) | About 10 cm above | The chairs and desks are drawn life size while the people are drawn at about 60% of it, so a seat comes up to a standing person's waist. Folding the body far enough to keep heads level would make it wider than it is tall, which was tried before and read as a blob. The owner chose the perch over the blob, knowing the trade | The owner wants everything drawn at one scale, which is a change to every visual and the camera framing, not to chairs |
| The signs steer people (**owner**) | Reverses the row above, "Exit signs mean nothing to the run". Somebody choosing where to run next, with no way out in mind, now weighs that choice by the nearest sign they can see | That row named its own revisit condition: a level wanting signs that matter would be a rule and would belong in the run. The owner asked for exactly that | Signs are ever meant to mislead, which is the same machinery pointed somewhere false |
| A sign changes where they decide to go, not how they steer | The only thing a sign touches is the scoring in `ChooseEscapeTarget`. A steering pull was built first and then taken out again | The pull was applied every tick, on top of whatever they were already doing, and it dragged people off errands: a strong person sent at a door wedged with a box sailed past it and off down the corridor instead, and two tests said so. Where somebody *decides* to go is the honest place for a sign to speak, and it is also the only place that cannot fight a route already worked out | Never; a sign that overrules a route is the second, quieter pathfinder the original decision was protecting against |
| A sign is read at 8 m, in the 45-degree cone, and not through a wall | `SignReadRangeMillimetres = 8000`; same room or a room open to theirs | A sign is a big lit thing you pick out down a corridor. The fire's 3 m vision range is the distance at which flames are upon you and is far too short for signage. The room test is the one `FireSystem` already uses for seeing fire, so the two agree about what a wall is | The corridor stops being the longest sightline on the floor |
| A sign is worth 6 m in the escape scoring | `SignEscapeBonusMillimetres = 6000`, scaled by how well a candidate spot agrees with the sign, so the wrong way costs the same again | It beats the 1.5 m of noise in the scoring and stands alongside the 5 m penalty for a route past the fire, so it decides the choice rather than colouring it | Playtests say people ignore the signs, or follow them too slavishly to look human |
| **On this floor the signs rarely change anything** | Kept anyway | Everybody runs a route search across the whole building (`DoorBehaviour.ChooseExitDoor` -> `WorldGeometry.TryFindRoute`) and always finds its one way out, so they nearly always *have* a way out in mind and the sign stays quiet -- and when it does speak it usually agrees, because it points at that same door. Three of the ten replay fingerprints moved, so it is not nothing, but it is not the feature the owner pictured either. Making signs matter the way they do in life means taking the perfect map away from people, which is a change to how everyone navigates and is the owner's call | The floor gets a second way out, or people stop being given a map of the whole building |
| Reading a sign draws no random numbers | Pure integer geometry, ties broken by the order the signs are written | The simulation contract requires replayable behaviour from the seed alone. A sign that drew would move every run that has one | Never |
| Content revision, not compatibility version | `ContentRevision` 51 -> 52; `SimulationCompatibilityVersion` unchanged | The tick schedule, the command envelope, the generator and the meaning of an event are all untouched. The ten fingerprints were re-recorded, because both changes move what people do | A change here ever changes a rule, which it should not |

## Prototype 2 decision: people find their own way out

Chosen on 2026-09-23. The owner asked whether people could look for a way out
the way real people do, rather than being handed a perfect map, and chose three
things: a **mixed crowd** (some people know the building and some do not),
built **before the hunter**, and built so it works for **any level and any
danger**. Everything else below was chosen on their behalf. It meets the revisit
condition of the row above, "On this floor the signs rarely change anything":
people are no longer all given a map of the whole building.

**What the player sees.** The five clients at the meeting do not know the
floor. When they panic, a "which way?" sign pops up; they check the doors they
can see, back out of a cupboard with "dead end!", and get a green "this way!"
the moment a sign, an open door or their host shows them. Everybody who works
here runs exactly as before.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Who knows the building (**owner**: mixed crowd, any level) | Each person is authored `KnowsTheBuilding` or `Visitor` (`AgentDefinition.Familiarity`, a *Visitor* tick box on `PaniqPerson`). Zero means "knows it" | A person authored before this existed, in any level, reads back as zero and keeps the old perfect map, so nothing anywhere changes unless somebody is made a visitor | A level wants shades in between, such as knowing one wing |
| The default cast (chosen for the owner) | The meeting is a client visit: 1009-1013 are visitors, their host 1014 (leadership 9) works here. They came up in the lift, which is no way out in a fire | Real evacuations split this way: in Sime's fire study, staff left by the fire exit and members of the public went back the way they came in. Five lost people and one leader who can walk them out is a showcase for the leadership dial the game vision describes ("she leads all nine to the exit nobody knew was blocked") | Playtests find the round too easy or too hard with five lost |
| Knowledge is per door, not per room | A visitor knows a door (and where it leads) or does not. They know every door of the room they start in | Seeing into a room must not reveal its far door: stood at the arch of the T, you know the T is there, not that the stairs are at the end of it | Rooms stop being rectangles, so a door can hide round a corner of its own room |
| Seeing a door | Within `DoorSightRangeMillimetres = 8000`, in their room, or in the next room through an open doorway. No vision cone | 8 m is how far a sign can be read, so a door and its sign are seen together. On this floor the arch of the T leaves the dead end 5.5 m off and in sight, and the way out 9.7 m off and not, which is exactly the wrong turn the floor was built to offer. A frightened person glances all round; a cone would make learning a door depend on which way they faced for one tick. The wall rule is the one the fire and the signs already use | Smoke should cut sight, or an occluding floor plan arrives |
| Signs teach the whole way | Reading a sign teaches every door on the walk from it to the way out it points at, worked out once from the building. Only frightened people read them | A sign that taught only the last door would teach nothing usable, because routes only cross known doors. "The exit is this way" is the promise a chain of signs makes. Nobody strolling to the coffee machine reads exit signs | Signs are ever meant to mislead |
| A door opening beside you is news | People in its room, or the room through it, learn it: the player's click, a door forced or burnt through, a blown hole | Nobody misses a door flying open beside them, however little they know the building. Uses the two-tier rule `AnnounceWaysOut` already has | Sound carries the news further (the Sound stone) |
| Leaders share everything they know | Falling in behind a leader, or being called on again while following, teaches every door the leader knows and which rooms are not worth looking in | The leadership dial should decide whether lost people get out. A leader who is lost themselves teaches nothing new | Playtests want followers to learn only the leader's route |
| Looking for a way out | A visitor with no known way out searches before hiding: the unseen corners of their own room, or any room they know how to reach and have not looked round, scored like a way out (walk, sign direction, danger, a bonus for sticking with a choice, a little noise). With nowhere left, they hide as before | The same shape as choosing a way out, so it reads as the same person thinking. Searching comes before hiding because a real stranger looks before they give up | Searchers are seen checking every office before the corridor, which would call for preferring corridors |
| A room is looked round when all four corners have been in sight from inside it | Four bits per room. A dead end is a looked-round room with nothing onward except the way they came in | Cheap, exact and works on any rectangle. It makes "they went in, looked, and came out" a thing that happens and can be told | Rooms stop being rectangles |
| Errands keep the whole map | Alarms, extinguishers, helping somebody and strolling still route through every door | These are trips toward something the person has already noticed, and they already walk on the shared flow fields. Limiting them needs knowledge of objects as well as doors, which is a different feature | Visitors are seen striding to an alarm in a room they have never been in |
| Staff are provably unchanged | Every new rule is a no-op for somebody who knows the building and draws no random number. A new test (`WithNoVisitors_TheFloorReplaysExactlyAsItDidBefore`) keeps the ten old fingerprints with the visitors made staff | That is the promise "everybody who works here runs exactly as before", checked bit for bit | Never |
| Random numbers | Only a visitor's search draws: one number per place considered, in a fixed order, inside the existing decision | The simulation contract requires replay from the seed alone | Never |
| Versions | `ContentRevision` 52 -> 53, `SimulationCompatibilityVersion` 40 -> 41 | Where people run is a rule of the simulation, not only content, so an old replay cannot be played against it | Never |
| **The ten fingerprints are not yet re-recorded** | They still hold version 40's values and fail until they are re-recorded in the Unity editor | This change was written in a cloud machine without Unity. The fingerprint runs need Unity's physics, so no value could be produced there, and guessing one would be worse than a known failure | As soon as the editor next opens: run `ReplayFingerprintEditModeTests`, paste each printed value, delete the note in that file |

Sources: J. D. Sime, [Movement toward the Familiar](https://journals.sagepub.com/doi/10.1177/0013916585176003),
*Environment and Behavior* 17(6), 1985; M. Kobes et al.,
[Building safety and human behaviour in fire: a literature review](https://research.tue.nl/en/publications/building-safety-and-human-behaviour-in-fire-a-literature-review),
*Fire Safety Journal* 45(1), 2010, which names familiarity with the building
layout among the things that decide how well people get out.

## Prototype 2 decision: the dead deal, the uproar pays

Chosen on 2026-09-23, after the owner said the round had nothing to do in it
and that they could not read anybody. The four rules of the economy are the
owner's; everything else below was chosen on their behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| A round opens with nothing (**owner**) | `InfluenceSettings.Starting` 100 -> 0, and an empty hand | The old opening was the same every time: pay 80 of your 100 for the one way out, then sit on the remaining 20, which bought exactly one card. Starting at nothing makes the first move a decision about *when*, not *what*, and it means the building has to get into trouble before the player has earned the right to save anybody | The measurement below says the wait is longer than a player will sit through |
| The uproar fills the meter (**owner**) | Every notable thing in the causal log pays, in three sizes: 1 for shouting, tripping, freezing, a collision, something catching; 3 for a knockdown, a shove, a door forced or burnt through, something broken, an alarm; 6 for somebody catching fire, going out cold, being crushed, an appliance going off, a door coming off its hinges | `game-vision.md` listed "panic itself pays" as the intriguing alternative kept on the shelf. It comes off the shelf: it fixes the death spiral (a disastrous opening no longer leaves the player broke and spectating) and it makes the game and the toy the same thing | Two sizes turn out to be enough, or the meter fills so fast that influence stops being a constraint at all |
| Deaths deal cards, and pay nothing else (**owner**) | One card per death, drawn uniformly from what is still available. A death credits no influence | The owner's reasoning is the best argument in the design: it hurts your percentage but buys you options, so it is a genuinely hard choice rather than a free lunch. Keeping deaths out of the meter gives the two currencies one source each, which is a rule that fits in one sentence apiece | Deaths paying twice is ever wanted, which would make letting people die the best move on both axes at once |
| Every card costs 30 (**owner**) | One `CardCost`, replacing Beefcake 20, fire 10, extinguisher 25, TNT 40, fuse box 45 | Which card you hold is not something you choose any more, so pricing them against each other would be pricing a choice nobody makes. What the player chooses is whether this moment is worth thirty | A card is ever bought rather than dealt |
| **TNT and the fuse box got cheaper** | 40 -> 30 and 45 -> 30 | Follows from the flat price. It is softened by their now being rare draws rather than things to save up for, and both are still limited to four charges | A single card is seen to decide a round on its own |
| Doors are unchanged (**owner**) | Unlock 50, walk open 30, pull shut 10 | The owner kept them deliberately. The one way out therefore costs 80, which is now something to be earned rather than something you begin with | — |
| Saving somebody still pays 15 | `PerPersonSaved` unchanged | The owner's rules did not remove it, and it is the only force in the economy pulling against "let it burn". It is also what makes opening the exit start to fund the rest of the round | The pull toward letting people die is judged too weak or too strong in play |
| Fire spreading pays nothing | Excluded from the uproar | It fires dozens of times a second in a room nobody is standing in, and would have swamped every other signal. The fire pays through what it does to people and things instead | The uproar is ever wanted to track the hazard rather than the crowd |
| The player's own cards pay nothing | `Power*` events excluded | Otherwise a card would partly refund itself | Never |
| The deck has its own random stream | A second PCG32 from the same scenario seed on `initseq` 55, owned by `DeckSystem`; the crowd keeps 54 | Sharing the one generator made the deck retune the entire game: a death drew a number, and from then on everybody panicked, tripped and froze differently. Sixty tests failed for no reason but that. [Simulation contract](../simulation-contract.md) allows a derived stream on stated terms, which this is the first use of | A third stream is ever wanted, at which point the derivation deserves a scheme rather than a second constant |
| A dealt card names the death that dealt it | `CardDealt` carries the `AgentLost` event as its causal parent, and the card in its strength field | Every event in the log traces back to a cause; a test caught this one not doing so. It also makes the round read back properly: "Ana died, and dealt you TNT" | Never |
| Cards can be dealt before a level starts | `InfluenceSettings.StartingHand`, empty in the office | A level may later want to hand the player something, and forty tests need a particular card in hand without being tests of the economy. A real setting rather than a test backdoor | — |
| Content revision and compatibility version both move | `ContentRevision` 52 -> 53; `SimulationCompatibilityVersion` 40 -> 41 | A new command outcome, a new event type and a new random stream all change what a recorded run means | — |

### What the measurement said

`HeadlessMeasurements.HowLongBeforeThePlayerCanOpenTheWayOut`, seeds 40-46,
first minute of each run. The way out costs 80; a card costs 30.

| Seed | Way out affordable | First card | Purse at 30 s | Purse at 60 s |
| --- | --- | --- | --- | --- |
| 40 | 56.9 s | 25.3 s | 37 | 109 |
| 41 | 16.9 s | 25.0 s | 300 | 300 |
| 42 | 57.8 s | 19.7 s | 43 | 93 |
| 43 | not within a minute | 19.2 s | 41 | 69 |
| 44 | 46.8 s | 24.8 s | 47 | 300 |
| 45 | 18.0 s | 19.1 s | 291 | 300 |
| 46 | 28.7 s | 21.9 s | 98 | 300 |

Re-measured on the same day after the wayfinding work was joined to this,
which made five of the fourteen people visitors who do not know the way out.
Every figure held except seed 45's purse at thirty seconds, which rose from 267
to 291, and a card or two more dealt on seeds 41 and 44. Strangers dying more
was expected to move these numbers and barely did.

Two things to watch, both for the owner to judge in play rather than for
anybody to tune blind:

- **The spread is enormous.** Seventeen seconds on one seed, not within a
  minute on another. A seed where the fire starts among people pays quickly; a
  seed where it starts in an empty corner pays almost nothing until it reaches
  somebody. The lever is `InfluenceSettings.UproarSmall/Middling/Big`.
- **The first card always arrives before the door can be opened**, at about
  twenty seconds on every seed. So the player's first move is a card, not the
  exit, on every seed measured. That may be exactly right -- something to do
  while the meter fills -- or it may mean the exit is priced out of the opening.

## Prototype 2 decision: cards you throw into the crowd

Chosen on 2026-09-23, immediately after the economy above. The owner settled how
blunt a card is, what it hits, when it can be played and how a person is read;
everything else was chosen on their behalf.

The owner rejected a first design built around pausing, an inspector panel and
seven dials to push, in these words: *"the game should not steer into a paused
and tactical game style. Point of the game is panic. It should be chaos."* That
rejection is the reason for most of the rows below.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| A card slams a dial to the end (**owner**) | One throw takes the dial to 10, or to 0 for Cold heart. No nudging | You see what you did: the man who was dithering picks up an extinguisher and goes at the fire. A nudge from 2 to 4 often changes nothing visible, which is a bad feeling to pay for | Everybody the player touches reads as a caricature |
| It lasts the round (**owner**) | No decay, no undo | The consequence plays out over minutes, which is where the joke is: the one you made fearless is also the one who walks toward the thing killing everybody. A timed version invites spamming and micro-timing, which is what pause was rejected for | — |
| Thrown at a patch, not a person (**owner**) | Everybody within 1500 mm of the point, for the rest of the round | Picking one capsule out of a running crowd is inherently a precision game, and precision is what pulls toward slow motion and pausing. A patch means you pick the right *moment* and live with who was standing there | Playtests say throws feel arbitrary rather than chaotic |
| Full speed only (**owner**) | No acting while paused, no slow motion. Space still pauses for looking | `game-vision.md` already said pause is for looking, not acting; the first design would have overturned it and the owner put it back. The round stays a real-time panic | — |
| A miss is free (**owner**) | A throw that moves nobody's dial costs neither the influence nor the card | The owner's rule: *"if you miss and don't hit anyone the card aren't spent, but hitting the wrong agent is your own fault."* It also matches the rule Beefcake already had | — |
| A throw that catches only people already at that end is also free | Same rule | It moved nobody, so it did nothing, and the established rule is that a card which does nothing is free. It is the difference between a wasted throw and a throw that never happened | The player is seen to farm this by aiming at a group they have already changed |
| Nothing displays a person's traits (**owner**) | No panel, no inspector, no marks over heads | You read character from conduct: the man standing still in the smoke *is* the coward. It is what you would do watching a real panic, and it costs nothing to build because the behaviour is already on screen | Playtesters say their cards feel random |
| A circle on the floor under the pointer | `CardAimRing`, sized to the patch, dim when empty and bright when somebody is inside; the HUD says how many | "You hit the wrong person and that is your fault" is only a fair rule if you could see who was standing there. The count is worked out in the presentation purely to draw and describe the aim; the run decides for itself who was caught | — |
| Everybody caught flashes | One `Power…` event per person, each driving the existing `agents.Notice` blink | You can see what you actually got, which is how a player learns to aim. One event per person also makes the round read back as "you made these four fearless" rather than as one line naming a patch of carpet | — |
| The patch is round, not square | Exact distance test on top of the index's box gather | The spatial index gathers a bounding box, so without it the corners would catch somebody 2.1 m away on the diagonal — outside the circle the player was shown. A test covers it | Never |
| The four cards chosen (**owner**) | Courage, Terror, Bastard, Cold heart, alongside the existing Beefcake | The owner picked these over one card per trait and over a smaller pair. Each has a large amount of prototype 1 behaviour already behind it, and each is a joke as well as a tool | — |
| Speed and leadership get no card | Left out | Nine cards is already the width of the bar, and these two have the least visible consequence behind them. Leadership especially needs people nearby to lead | The bar becomes a hand that scrolls, or a level wants a leader made on purpose |
| Beefcake converted rather than kept | It is now thrown at a patch like the rest, and no longer names a person | Two rules for the same kind of card would be one to learn for nothing. It also removes the last thing aimed at a chosen person, which is what `TargetsAPerson` now returns false for everywhere | The end screen's "click somebody for their facts" arrives, which reuses the person-picking that was deliberately left in place |
| One dial each, no trade-offs | Courage does not cost compassion | A third option the owner did not take. It would need the panel that was rejected, to show what you broke | The cards are judged too strong |
| Content revision and compatibility version both move again | `ContentRevision` 53 -> 54; `SimulationCompatibilityVersion` 41 -> 42 | Four new commands, four new event types, and a deck that now deals nine cards rather than five | — |

## Prototype 2 decision: joining the two lines of work

Chosen on 2026-09-23. Two sessions wrote to
`feat/prototype-2-round-and-controls` at the same time: one added wayfinding
and visitors and pushed it, the other added the economy and the cards and
committed locally. Neither is at fault and no work was lost. The branch was
rebased into one straight line, wayfinding first, and these are the things that
only showed up once the two were together.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| One straight line, not a merge (**owner**) | The economy and the cards replayed on top of wayfinding | A deliberate exception to the `--no-ff` rule, which is about integrating one branch into another; this was a single branch that forked because two sessions wrote to it. The two local commits had never been shared, so rewriting them cost nothing | Two sessions are ever deliberately kept on separate branches, where the rule applies as written |
| Both groups of events kept their order | Wayfinding's three appended first, the economy's five after | The event list is append-only because its ordinals are part of every recorded run. Theirs were pushed first, so theirs keep the lower numbers | Never |
| **`AgentFoundTheWayOut` now names a cause** | Falls back to the fright that set them looking, `agent.Fear.ScaredEventId`, when nothing more specific is passed | Only a leader telling somebody handed over a cause; seeing a door or reading a sign did not, so in a plain run those two were the one thing nothing could be traced back through, and a long-standing test that walks the whole log said so the first time it was ever run against wayfinding. Its sibling `AgentLookedForAWayOut` already names the same fright, so this is the arrangement that side had chosen anyway | Somebody wants a way out learned with no fright behind it, which today cannot happen: the event is only written to a frightened person |
| A test that needs a death now arranges one | `LostAgents_TraceBackThroughTheFlamesToABurningSquare` and the three card-dealing tests pin the fire to a named square and stand somebody on it | They used to put a person at the middle of the fire's spawn *area* and trust the seed to light it under them. That is not a property of a seed, it is a property of where a random draw lands, so the day wayfinding shifted the run's randomness the fire moved off them and four tests lost the death they were about. A death a test needs is a death it arranges | Never |
| Wayfinding's own run tests can afford their setup | `WayfindingRunEditModeTests.Floor` opens with a full purse | They open the way out in their first two ticks, and a round now opens with nothing, so every one of them had the exit stay locked and nobody get out of the building. The file predates the economy and is a test of where people walk | Never |
| Their invariant fingerprints re-recorded once | `WithNoVisitors_TheFloorReplaysExactlyAsItDidBefore` | Its note said never to re-record it. It had to be, exactly once: the dead now deal a card, which writes a line into the log of every run somebody dies in, so the numbers moved for a reason that has nothing to do with wayfinding. From here it means what it was written to mean | Never again |
| **That invariant is weaker than it reads** | Left as it is, with the fact written into its own comment | On seeds 40, 42 and 46 the meeting room is never frightened inside the minute, so the visitors never look for anything and nine of its ten cases come out identical to the runs that *have* visitors. Only cards-played on seed 42 tells a floor of staff from a floor with strangers on it. Worth knowing before trusting it | Somebody picks seeds for it that frighten the meeting room, which would make all ten cases mean something |
| The new events pay nothing into the purse | `AgentLookedForAWayOut`, `AgentFoundADeadEnd`, `AgentFoundTheWayOut` are left out of the uproar | They are somebody thinking, not a commotion, and they fire often. Confirmed deliberately rather than left to the default | The purse is ever meant to reward a crowd that is lost rather than a crowd in uproar |

**What the whole branch had never done before this:** pass. The wayfinding
commit was written without a Unity editor, so it shipped ten knowingly-stale
fingerprints and two test files -- 690 lines -- that had never been run once.
Both are now recorded and run: 391 passing, 0 failing.

## Prototype 2 decision: the branch history condensed (2026-09-25)

Chosen by the owner on 2026-09-25. `feat/prototype-2-round-and-controls` had
grown to 74 commits, too many to read as a story. Its main line was rewritten
as 15 commits, one per stage of the work, in the order the work happened. The
three side strands, the review refactor, the building's day
(`feat/cue-system`) and the test gear (`chore/test-routine`), keep every
commit they had, fork from the same points and merge back at the same points,
so the graph keeps its shape: 36 commits in all, four of them merges.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| One commit per stage on the main line, not per layer (**owner**) | Each new main-line commit gathers everything one stage of prototype 2 added, across game, controls and visuals, and its message lists the original commits it absorbs | The owner asked for few commits with good grouping, and explicitly not one. From here new work lands as few commits per batch, usually one (see *How much goes in one commit* in `AGENTS.md`) | Never for this branch |
| The graph keeps its shape (**owner**) | The side strands were rebuilt commit for commit, same files, messages, authors and dates, on top of the new main line, and the branch names `feat/cue-system` and `chore/test-routine` were moved to the rebuilt tips | The owner reads the graph as much as the log. A first attempt folded the merges into a straight line, which made the side branches vanish from the picture, and was redone | Never |
| Snapshots, not replays | Every new commit reuses, byte for byte, the file tree of an old commit; condensed commits take the tree of the last old commit in their group | Nothing was merged, rebased or re-typed, so nothing could conflict and nothing could break; every new commit is a state the branch really was in and was tested in. The new tip's files are identical to the old tip's, apart from this section | Never |
| The old history is kept | Local tags `backup/prototype-2-before-condense-2026-09-25` (old tip), `backup/cue-system-before-regraft-2026-09-25` and `backup/test-routine-before-regraft-2026-09-25` (old side tips); never pushed | The full messages of the 74 commits remain readable under the tags, and each condensed message names the commits it came from | Delete the tags once the branch is merged into `main` |

## Prototype 2 decision: the test suite reviewed

Chosen on 2026-09-23, after a play-mode test sat broken for a day because every
check that day had been an edit-mode one. The owner settled two things and the
rest was chosen on their behalf.

**Settled by the owner:** the liveliness tests stay as real tests -- furniture
still burns, somebody still gets knocked out, people still throw things -- and
the suite should stay quick enough to run constantly, cutting duplication hard
in exchange.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **One command runs both halves** | `tools/RunUnityTests.ps1 -All`, which calls the script once per half so the two paths cannot drift | The suite has always had two halves and the command ran one. That is not a thinking error to be more careful about next time, it is a tool that quietly did half its job, and it put a broken test on GitHub | Never; the plain form stays for when one half is wanted |
| Four of six "same seed, same run" tests cut | Kept `TheBusiestRun_PlaysOutTheSameWayThreeTimes` and `FixedSeedRuns_ProduceIdenticalStateAndEvents` | Six tests asked one question. The busiest replays a seed with doors, boxes and cards all going, three times over; the other keeps two runs in lockstep and so names the *tick* they diverged, which is the one thing a fingerprint cannot tell you. The four cut were narrower versions that would have to get past both | A third way of being non-deterministic turns up that neither notices |
| `PanickedCrowds_BumpKnockDownTripAndGetBackUp` 20 seeds -> 8 | ~15 s saved | It checks invariants on every tick of every run rather than asking whether something happens sometimes, so eight runs is still tens of thousands of checks. Seed count is the right lever for an invariant sweep and the wrong one for a liveliness test, where fewer runs only makes the answer wobblier | It ever starts failing on one seed and passing on another, which would mean it had become a liveliness test |
| `NobodyStandsStillInFrontOfAnOpenDoor` down to one case | ~9 s saved | The two cases differed only in whether the door was unlocked at second six or second twelve, and nothing about standing clear of a doorway depends on the clock | — |
| `WithNoVisitors` from ten cases to three, on different seeds | ~7 s saved, and the test now means something | Measured: on seeds 40, 42 and 46 the meeting room is never frightened inside the minute, so the visitors never looked for anything and nine of the ten cases ran a simulation identical to the test above them -- proving only that a run equals itself. It now runs seed 41, where five people find the way out and four go looking, and cards-played on 42 | The shipped floor changes enough that these seeds stop frightening the meeting room |
| **The aim circle is tested against the card** | New `CardAimRingEditModeTests` | The circle drawn under the pointer and the patch the card catches are two separately written pieces of code -- one counts from the snapshot in the presentation, the other gathers from the crowd index in the run. A card that catches the wrong person is spent, and that is only fair because the player was shown the patch first. Nothing checked they agreed. Proved able to fail by widening the card's patch by 700 mm and watching it report "the circle promised 3 and the card caught 4" | Never |
| No test for the card bar reading the hand | Left out deliberately | It would need simulated keyboard input in play mode to set up two lines that mirror `snapshot.Hand`, and the economy tests already check that at the source. A heavy test for a thin seam is the kind this review exists to remove | The bar ever holds state of its own rather than mirroring the run |
| **Five tests were quietly working shut doors** | `FurnitureEditModeTests`, `HardKnocksEditModeTests`, `RoomsEditModeTests`, `RoundEditModeTests` (then still prefixed `FireReaction`) and `HeadlessMeasurements` now put a purse behind the player | Found while reviewing, not while failing. A round opens with an empty purse, so every door click in those files was being refused and they went on passing while checking a building nobody had opened. `NobodyStandsStillInFrontOfAnOpenDoor` unlocks every door as its whole premise, and none of them were unlocking; `CountEventsForSeeds40To46` printed its "doors all opened" and "doors locked" halves as the same run for both. Repaired, the opened half now reports 13 doors opened against 2 | Never -- and it is the third time this week that an empty purse silently hollowed out a test, so the helper is the first thing to reach for when a test works a door |

**Before and after.** 391 edit-mode tests in 203 s, with play mode run by a
separate command nobody was running. Now **381 edit-mode tests in 160 s, plus
16 play-mode tests, both halves in one command in 172 s of wall clock.**
Fourteen cases removed, four added, five files repaired. Two minutes was the
target and it missed by about fifty seconds; closing that would mean cutting
into the liveliness tests, which the owner chose to keep and which are worth
more than the time.

## Prototype 2 fix: a stranger looks round the room they walked into

Chosen on 2026-09-23, found while the review refactor's route costs were being
tested and fixed on its own before them.

**What a player saw.** A visitor who knew of no way out could walk into the
cafeteria, turn straight round, walk back into the corridor, turn round again,
and go on bouncing through that one doorway until the building burned down.
From the corridor, the cafeteria was the nearest room they had not looked
round; from just inside the cafeteria, its far corners were further off than
the bathroom's door back out through the corridor, and the door they had just
come through earned the "stick with what you chose" bonus in both directions.
Whether a given run fell into the loop was a matter of a few hundred
millimetres of noise, which is why no recorded run and no test had caught it;
costing routes as real walks made it a certainty on the shipped floor.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The room they are in comes first** | In `DoorBehaviour.TryChooseSearch`, somebody standing in a room they have not looked round looks round it -- walks to where its unseen corners come into sight -- before any other room is weighed. The rule stands down when that corner is by the danger, or when the squares say it cannot be reached; then the old weighing of every room runs as before | The loop needs somebody to leave a room before looking round it, so once a room entered is always a room looked round, the number of rooms left to look round falls with every doorway and the search must end. It is also what a person does: you walk into a room to look, so you look | A room too big to look round from a few spots (a warehouse floor); then "looked round" needs a finer notion than four corners |
| Versions | `SimulationCompatibilityVersion` 42 -> 43; `ContentRevision` 54 -> 55. All thirteen recorded fingerprints happen not to change: no recorded run has a stranger searching a room whose far corners are further than the next door | Where a searcher walks is a rule of the run, so the version moves even though the recordings did not | Never |
| Test | `WayfindingRunEditModeTests.AStrangerOnTheirOwn_WithNoSigns_LooksForTheWayOutAndFindsIt` is the check, and now passes for a reason rather than by luck | -- | -- |

## Prototype 2: playtest fixes (2026-09-24)

The owner played seeds 41 and 42 and listed seven things that looked or
behaved wrong. Two were one bug, and the fix the owner had been told about on
2026-09-23 had repaired only half of it. The seated look is the owner's call
("exactly the same as standing, only higher"); everything else below was chosen
on their behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| The step out of a chair is chosen once | `ChairBehaviour` picks where somebody rising from a chair will step -- beside it, the way they stood to pull it out, left before right, then anywhere a step clear -- once, as they start to rise, and keeps it in `AgentSitting.StepTo`. Rising interpolates to that spot and standing up leaves them on it | It used to be worked out afresh every tick from wherever the body had got to, a step further on each time, so the spot ran away from the body: over ten ticks a person slid the better part of two metres backwards, faster and faster, and was then placed half a metre further on in the tick they stood. Everybody who starts the round seated stands up at tick 3000, so six people at the meeting table did it at once, straight into the walls -- the "old bug" the owner saw again on seed 42. The 2026-09-23 fix ("Nobody is shoved backwards out of a seat") had repaired only the frightened way out of a chair; this is the calm one, and that row's own revisit condition named exactly this | Never; a target that moves with the thing chasing it is a bug in any behaviour |
| Sitting down and getting up take half a second | `SitLowerTicks` 10 → 25, used for lowering onto the seat and for rising off it | A fifth of a second to cross half a metre of floor onto or off a seat is a lunge, not a sit. Half a second is the slowest pace that still reads as one movement at the game's speed; a full second had people hanging in mid-air | Playtesters read it as slow motion |
| How far into the seat somebody is, in the snapshot | `AgentSnapshot.SeatedPercent`, 0 on their feet, 100 sat down, in between while lowering or rising (including a frightened leap up), kept by `ChairBehaviour` and read by the display | The display used to read sitting off the activity state alone, which is 0, half or 1 with no in-between, so a body popped onto the seat and popped off it; and people held in a chair while turning to look at a noise were drawn standing. It is an observation the display follows and decides nothing, like the body pose | Never |
| Doors burn only from fire in a room they open onto | `DoorSystem.ScorchInTheFire` asks `FireSystem.NearestCellDistanceSquaredInRooms` for the door's own room and the room beyond it. The same filter, as `IThreat.AnyCloserThanInRooms`, decides whether flames are "at the door" when somebody weighs slamming it | A shut door charred from any burning square within two metres in a straight line, wall or no wall. The storage closet is two metres deep and its only door is in the wall opposite the bathroom, exactly two metres from it, so a bathroom fire against that wall burnt the closet door open from a room it has nothing to do with (seed 42) | A hazard that goes through walls, which would want its own answer to the question rather than the fire's |
| Tables are furniture to a running crowd, not walls | New `TableAvoidPercent`: a panicking person keeps off a table's edge at 60 % of the pull toward their goal, a calm one at 150 % (the same as off a wall, as before). `Locomotion.Steer` takes the table weight apart from the wall weight | Table edges were steered away from exactly like walls, at 200 % in a panic, so nobody running ever touched one, and the physics that would have shoved a desk along under a 70 kg body never got the chance. Calm people still walk politely round the desks | A level wants a table people may not lean on, which is a kind of table rather than a steering weight |
| Somebody stuck behind a table heaves it | `PanicBehaviour.TryHeaveTable`: blocked for `BlockedGiveUpTicks` with a table within arm's reach ahead of them, they give it a change of speed of `TableHeaveSpeedMillimetresPerTick` (80, times the feel's throw strength) their way, shared out by weight as a blast's shove is but never below `TableHeaveLeastPercent` (25 %), and cannot heave again for `TableHeaveRestTicks` (40). Tried after throwing a loose thing clear and before standing aside at a door | The roadmap promised tables shoved, tipped and flipped, and only a blast ever moved one. The heave is what a person actually does with a desk in their way. A 21 kg desk goes over; a 36 kg cafeteria table slides; the 135 kg meeting table shifts a little and stays a table you can hide behind | Playtesters see desks fly rather than tip, which is the speed dial; or a crowd of six fails to move the meeting table between them, which is the least share |
| A heave lands on the top edge, not the middle | `PhysicsWorld.HeaveTable` applies the push as an impulse at the point on the table's top over the hands, so the engine turns it as a real shove there would | A blast's shove goes through the middle and can only slide a table, which is why nothing ever tipped. A push high on a table tips it about its far feet; that is physics, so the engine is left to do it rather than a "flip" rule guessing at it | Never |
| A heaved table is written down | `CausalEventType.TableHeaved`, naming the table, a middling commotion for the uproar meter, and a line in the story | A table going over in a crowd is exactly the kind of thing the meter is meant to pay for and the read-back is meant to tell | Never |
| Fetching an extinguisher goes round the walls | `ExtinguisherBehaviour.Walk` asks the route fields for its heading, as every other errand does | The bottle is chosen by how far it is to walk to it, which may be through two doorways, and the walk to it headed straight at it. On seed 41 three people from the meeting stood nose to the wall between them and the cafeteria's extinguisher -- one in the corridor facing north, two in the meeting room facing east -- for the whole thirty seconds the errand lasts. This is the "staring at the wall" the owner saw. Found by a harness that watches every frightened person for ten seconds without moving (`CorridorStarersEditModeTests`), which is kept as the test | Never; walking straight at a thing through a wall is never right |
| An errand somebody is stuck on is given up | `ExtinguisherSettings.BlockedGiveUpTicks` (50): a second of getting nowhere on the way to the bottle or the fire and they drop it and go back to running | The settings already said "somebody genuinely getting nowhere is still caught, by the blocked counter rather than by the clock", and nothing did it. Every other errand has one | A crowd so thick that everybody with a bottle gives up in it, which is a bigger number |
| A seated person is the standing person, higher up (**owner**) | The same capsule, never scaled, tilted or folded, lifted so its bottom rests on the seat: middle at 0.975 m, head at about 1.5 m. Replaces the rows "A seated body rests on the seat" and "A sitting head ends up above a standing one" | The 2026-09-23 fix drew a seated body 0.62 m tall at its full 0.5 m width, and a capsule scaled unevenly flattens its rounded ends into a blob: that was the squash the owner reported. The owner's instruction, in their words: "exactly the same as standing. No leaning, no squashing, no deforming. They should be higher off the ground than a standing person, representing them sitting on the flat part of the chair." A seated head is now about half a metre above a standing one | The owner asks for people and furniture at one scale, which is a change to every visual, not to chairs |
| The body rides the seat by the snapshot's seated fraction | `AgentViews` lifts the body by `SeatedPercent`, blended between the last two ticks like the position, and lets the walking bounce, waddle and the alert hop fade out with it | Sitting was read off the activity state alone, which is nought, half or one, so the body popped onto the seat and popped off it, somebody held in a chair while turning to look at a noise was drawn standing, and an alert person bobbed 0.2-0.4 m off their seat | Never |
| Exit signs stand upright (**owner**) | A thin green slab standing on edge along the way it points, like a sign on the wall beside a corridor, with the white arrow on both faces. Reverses "Exit signs are drawn flat, not upright" | The flat sign faced the sky, which read to the owner as a sign for the camera and not for the people in the building. The owner asked for them upright and accepts that from some camera angles a sign is edge-on; the slab has thickness so that edge-on it is still a sliver of green. The simulation never had a sign face -- a person reads a sign by where it is -- so nothing in the run changes | The signs are ever meant to be readable from one side only, at which point the run needs a sign face too |
| Nothing happens to a whole group on the same tick (**owner**) | Every fixed length of time a person spends on something -- getting up, coming round, trying or opening a door, pressing an alarm, picking up or setting down, standing up from a chair, grabbing somebody, an order or a following lasting, a fetch or a stroll timing out -- goes through `SimulationContext.Jittered`, which stretches or squeezes it by a seeded amount of up to `WorldSettings.TimingJitterPercent` (20 %) either way. People who start the round seated each sit on for their own while beyond `SeatedAtStartTicks`, up to `SeatedAtStartSpreadTicks` (400) more. Written into the working agreements and the simulation contract as a rule | The six at the meeting all rose on tick 3000 exactly, and the owner asked that nothing in the game happen to a whole group on the same tick: it reads as clockwork rather than people. Lengths that were already drawn from a range (freezing, burning, sitting, hesitating) are left as they were. Measured with every door open over forty seeds: 437 escapes and 191 deaths before, 377 and 212 after; twelve of those seeds run again with the draws kept but the jitter set to nothing scored the same way, so it is the dice reshuffled rather than the timing, and the worst new seed is a thrown chair wedging the way out, which the game already does on purpose. The open-doors test now asks for at least one escape a seed and a quarter of a hundred people safe over five seeds, instead of three a seed, because three a seed was hostage to which chair landed where | A length of time that must be exact for the rules to work, which would be an exception named here; or forty more seeds showing escapes still down once the dice have been reshuffled by something else |
| Nobody reacts on the tick a thing happens (**owner**) | `SimulationContext.ReactionLag`: every reaction to the world begins a few ticks late, drawn per person and per occasion from the seed (`PerceptionSettings.ReactionLagMinimumTicks` 2 to `ReactionLagMaximumTicks` 8). It is applied at every "think again now" -- a door swinging open, a leader's order, getting up, being shoved, giving something up, learning a way out -- at the start of being startled (before the seeded reaction delay), at a calm person's turn toward a noise, and at falling in behind a leader. On top of that no two people finish being startled on the same tick: `FearSystem.Staggered` puts the later one in ID order off by `StartleStaggerTicks` (3) until the tick is theirs alone | The owner's words: "All behavior in the game should never be a reaction of a tick. Always add a small tick offset to make the reactions more human." Reaction delays already ranged from nought upward, which let two of six draw nought and rise together the tick the bell rang; a door opening had everybody in the room think again on that very tick; a call had followers turn on the tick it was made. Two to eight ticks is four to sixteen hundredths of a second, which reads as people and not as a delay | A reaction that must be instant for the rules to work, which would be an exception named here |
| The test bridge stops a playtest | `TestBridge` leaves play mode itself when a request arrives, keeping the request until the editor is back, instead of refusing | A playtest left running blocked every test, and the only way on was to close and reopen the editor. Nobody is at the keyboard when a script asks | Never |

## Prototype 2: the rest of the office (2026-09-24)

The owner asked for seven more props: a vending machine, cabinets, shelves, a
big copy machine on wheels, a whiteboard on wheels, a standing lamp that tips
and pops with a shade that falls off, and a robot vacuum that moves along and
can catch fire. "Only add the objects and implement their behaviours like the
other props." Built on the prototype 2 branch in a second checkout while
another agent worked on the building's day in the first; the props pipeline
they touch is nothing that agent's work touches.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Seven kinds, each a row in the table | `VendingMachine`, `Cabinet`, `Shelves`, `CopyMachine`, `Whiteboard`, `StandingLamp` (+ `LampShade`), `RobotVacuum`: a row in `ObjectKindSettings.Defaults`, a shape in `ObjectShapes`, a drawing in `BoxViews`, a name in the story, a factory and places in `PrototypeBuilding`. Tipping, rolling, burning and popping all come from the row: a vending machine is 160 kg at friction 150, a copier is 100 kg at the office chair's 30, a whiteboard 15 kg at 30, shelves 45 kg that catch at 110 ticks, a copier whose toner pops at 1.2 m | Exactly the pattern the 2026-09-21 decision "what a thing is for, written down once" was made for. Nothing per kind is written for going over: a tall shape with gripping feet goes over because the engine says so | A prop needs a rule the table cannot say, at which point it is a column, as the two below are |
| A blast shares itself out by weight to loose things too | `FlingFrom` gives a thing heavier than the 20 kg reference a smaller share of a blast, as `ShoveTablesFrom` already did for tables. Nothing lighter is slowed | Before, a 160 kg vending machine flew from a blast exactly as a bin does. Now a bin flies at full speed, shelves at under half, and the vending machine rocks and stays put; a whiteboard and a set of shelves go over, which is the test | Never |
| A thing that pops when it goes over, and sheds its parts | Two columns, `PopsWhenTipped` and `ShedsPartsWhenTipped`; one definition field, `PartOfObjectId`, for a part that starts dormant on the thing it belongs to. `PhysicsObjectSystem.PopWhateverWentOver` runs after the engine's step: upright last tick and not now, it pops once (`CausalEventType.ObjectPopped`, a small uproar, a thud, "pop!", a quarter-size flash) and every dormant part of it is placed at its top and nudged the way it fell | The lamp. A part is an authored dormant thing rather than one made at run time because the list of things is index-stable: the snapshot buffers, the physics handles and the index of what lies where all assume it. Dormant spares already work exactly this way for the extinguisher card | A prop with parts that come off for another reason than going over |
| A thing that drives itself | Two columns, `DrivesItself` and `CruiseSpeedMillimetresPerTick` (16, a Roomba's 0.8 m/s), and three dials in `ObjectPhysicsSettings` (`RoverPauseTicks` 15, `RoverTurnMinimum/MaximumDegrees` 60-150). `PhysicsObjectSystem.DriveTheRovers` runs before the engine's step: on its wheels and not held, thrown, wrecked or burnt out, it pushes toward its heading at cruise speed; with no floor a body-length ahead, a table there, or having wanted to go and hardly moved, it stops, turns by a seeded amount and waits a moment | The robot vacuum. It is a loose thing like any other -- kicked, thrown, tidied away (only when stopped: tidying refuses anything moving), burnt -- and a burning one keeps trundling until it burns out, heating what it passes, which is the fun of it. It is not a threat and not a person: it decides nothing about anybody | A second self-moving thing wants a different way of choosing where to go, at which point the turn rule is a kind's, not the system's |
| A loose body is turned by a spin, never by writing its rotation | The robot vacuum turns on the spot by being given an angular velocity through the engine (`PhysicsWorld.SetSpin`, as a tumbling chair is) toward the heading it chose, at `RoverTurnDegreesPerTick` (10), and drives off once it is facing it | The first version wrote the body's rotation straight in between steps (`PhysicsWorld.Face` on a loose body), and with that in place the same seed gave different fingerprints from one process to the next in the busiest runs -- the first replay break since the physics engine came in. Batch-mode runs of the committed code, and of every other new prop, agreed exactly; only a driven rotation write disagreed. Found by bisecting the new work feature by feature; `Face` stays for the kinematic bodies it was written for | Never; a loose body's pose belongs to the engine |
| The vacuum rides about alight, then goes off (**owner**) | A new column, `PopsWhenBurntOut`: the pop (the same three values) happens when the burn ends rather than the moment the flames reach it. The robot vacuum burns for 1,500-3,000 ticks (30-60 s) and then its battery goes off with a laptop-sized bang (1 m, two squares lit) | The owner: "a lot of health. I want it to ride for a long time on fire before it goes pop." Before, it burnt for six to ten seconds and never popped | Playtesters want the bang bigger, which is the pop's three values |
| The round's end has a cause even without a trigger | `RoundSystem` traces `RoundEnded` and `AgentSurvived` to the player's trigger, or, in a run where the hazard began on its own clock, to the hazard's own first event | With the new props a test run ended inside its window for the first time and the log had an event with no cause. Every event but the first has one; this one was missing it whenever the round had no trigger | Never |
| Heavy props stand where nobody starts | The office's second cabinet and the cafeteria's lamp were moved once each: a cabinet going over on a person standing between it and the wall pushed them through the wall (seed 40), and a lamp kicked into a doorway lay across the feet of somebody opening the door for a quarter of a second | Placement is content, and content is the cheap fix. The wall push is a real hazard of tall heavy props and 40 mm walls that the movement test guards; if it recurs somewhere a prop cannot be moved from, the fix is thicker walls or a lower depenetration speed for people, and that is a physics decision to make deliberately | A person is seen outside the building |
| Batch-mode testing from a second checkout | `Unity.exe -batchmode -nographics -runTests -testPlatform EditMode -testResults <path outside Temp>` and `-executeMethod Paniq.EditorTools.RewriteScenarioAsset.Rewrite`, run from a git worktree while the editor stays open on the main folder. Results must go outside `Temp`, which Unity empties on exit | Two agents, one repository, one editor: the second checkout keeps the other agent's uncommitted files out of reach. The first import of the second folder takes minutes; every later run about two | Never |

## Prototype 2 decision: the building has a day (2026-09-24)

The owner asked for the groundwork of an event system -- small events set off
by a director, by the people themselves or by the player, dynamic, built out
of what exists, tying in the leader, prepared for a future event editor -- and
accepted the plan in full ("I accept it all"), on a new branch off prototype 2
with as few commits as possible. The design is the foundation note
[the cue system](../cue-system.md). Everything below was chosen on the owner's
behalf.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| The word is *cue* | A small event in the building's day is a `CueKind` and is logged as `CausalEventType.CueCalled`; what one person does about it is an `AgentErrand`; the timetable walker is `DirectorSystem` | "Event" already means a line in the causal log, and the owner's request used it for both. A stage play gives cues; the cast acts on them | Never; a rename is cheap if a better word turns up |
| Cues are delivered like a leader's shout | `CueSystem` writes the cue down once and hands every calm, upright person in its audience a pending errand that starts at their own reaction tick plus their own seeded share of the cue's spread | It is the shape leaders already use (an event naming the person, a record on them, their own decisions from there), so the feelings rule holds: no rule in the crowd switches on an event type, and nobody moves on the tick a cue is called | A cue that must reach somebody frightened, which would be a feeling (trust) rather than a cue |
| The host ends the meeting | `CueSystem.HostOf`: the seated person in the room with the most leadership, lowest ID on a tie, is the cue's source and takes it up with no spread | The owner asked that the leader tie in. A meeting is ended by whoever runs it, and the read-back can say who | A later cue wants a different host rule (the loudest, the most senior); it is one function |
| Errands open doors | A calm person on an errand walks room to room, opens a shut door on the way after `Exits.DoorOpenTicks`, and waits at one that is locked or wedged for `DaySettings.WaitAtLockedDoorTicks` (1500) before giving up | A shut door was a wall to anybody not frightened, so the whole calm day happened inside one room and no errand could cross the building. The wait is what makes a queue at a locked front door | Playtesters see people give up at a locked door too soon or too late; it is one number |
| An errand is more patient than a stroll | `DaySettings.BlockedGiveUpTicks` (150): stuck for three seconds, an errand walker gives up; a stroll gives up after `Calm.BlockedGiveUpTicks` (20) | Found by a trace: a toilet trip borrowed the stroll's patience and was dropped the first time its walker had to wait behind somebody in the office | Errand walkers seen pressing at each other for seconds; it is one number |
| A calm person asks the way | Errand routes use `TryFindRoute` (every door), not the known-doors route, so a visitor leaves at home time like anybody else | A visitor who does not know the floor is walked to the door by the people who do, in life; only a frightened person has to find it alone | A cue is wanted where not knowing the way matters (a drill) |
| Home time is not on the office's timetable | `PrototypeBuilding.DefaultTimetable` holds only the meeting ending at tick 3000, spread 400 | The level opens calm with no clock and the way out locked; home time would have the whole office queueing at a locked door before the player has pressed anything, which changes the level rather than furnishing it | The owner wants it; it is one timetable line, and the round rules around it (people who left before the fire count as saved) are tested |
| The player path is plumbing, not a card | `PlayerCommandType.CallHomeTime`, free like the trigger, logged as `PowerCalledHomeTime` (the root cause of the cue); no card in the deck, nothing on the screen | The owner asked for the player as a caller; the command proves the path. A card is a small later change to the deck and the HUD, and a product decision about mischief | The owner wants a "Home time" card |
| The purse pays only during the round | `Run.SettleThePurseAndTheHand` credits an escape only while `RoundPhase.Running` | Somebody who strolled out at home time before anything was wrong was never in danger; paying for them would let a player fill the purse by emptying the building first. They still count as saved on the card: they are out | A level where leaving before the disaster should score differently |
| A cruel person going home may slam the door | `ConsiderSlammingBehind` has no fear check and is left alone | It is the villain's move the roadmap already promises. On a level with home time, one bastard out first can lock everybody in, which is the game | Playtesters find it unfair rather than funny |
| Only the first remark is heard | `SoundSystem.Say` emits a sound (`DaySettings.RemarkHearingRadiusMillimetres`, 2500, alarming nobody) for the first thing each person in a chat says; the rest are logged as chatter and heard by nobody | Every remark heard had the whole office turn to stare at the two people talking beside them, all afternoon, and errand walkers stopping every few seconds. People look up when a conversation starts and then ignore it | Audio arrives and wants every remark as a sound; the log already has them |
| A toilet trip is a clock, not a dice roll | `DaySettings.ToiletEveryTicks` (18000, six minutes): each person's next trip is drawn from the seed, the first anywhere in the first stretch, and nought means nobody goes | A chance per decision was tried first at 3 % and sent people every few seconds, because a calm person decides something every few seconds: twenty people, three stalls, a constant procession, and the one strong person walked out of a leadership test's room. How often somebody needs the toilet is a rate, not a share of their decisions | Playtesters never see a trip in a round (raise the rate) or see the bathroom queue (lower it); one number |
| Chats are in one room, between free people | `IsChatCandidate`: calm, same room, not seated, not on an errand, between 1.5 and 6 m | Somebody would otherwise open a door to go and chat to a person in the corridor, and the old one-sided version walked at walls | A chat across a doorway is wanted |
| Own ideas wait for a cue | A person with a cue waiting on them is offered no toilet trip, chat or wander home until it is done | Found by a test: four people missed home time because their own dice handed them a chat or a toilet trip in the seconds before they took it up | Never |
| Everybody who starts seated sits until told | `AgentSitting.SitUntilTold`; the sit-length draw is skipped; the two at the cafeteria table sit through the calm half | The meeting must break up by the timetable and nothing else, and the start-up draw order stays untouched. The cafeteria pair used to rise at the minute mark with the meeting | They read as statues; a "lunch ends" is one timetable line |
| Tidying is not interruptible; sitting on purpose is | `ErrandBehaviour.IsInterruptible` | An interrupted carry drops the box unlogged; a person in a chair can be got up | A cue should interrupt a carry (a fire drill) |
| A glance resumes the errand | `CalmBehaviour.ChooseNext` asks `ErrandBehaviour.TryResume` before choosing afresh; a chair handed over to the chair behaviour is kept as `ErrandPhase.SittingDown` | Found by a trace: a thud beside somebody walking home dropped the errand, and a remark beside somebody walking to their chair dropped the chair | Never; a glance forgetting the errand is a bug |
| Homes are chairs, or spots | `AgentDefinition.HomeObjectId` (a chair, one person each) or `HomeSpot` (inside a room), zero meaning none; `DaySettings.GoHomeChancePercent` (10) sends somebody with a home back to it | The office's eight desk chairs are assigned by hand in `PrototypeBuilding`; visitors and the host have none and loiter | A level wants a home that is a desk without a chair, a counter, a post |
| Rooms have a use | `RoomDefinition.Use` (`Ordinary`, `Stall`), zero meaning ordinary; the three bathroom stalls are stalls | A toilet trip needs somewhere to go; the bake tool and the Inspector carry it | A level wants a kitchen, a lift lobby, a stage |
| The timetable is data | `ScenarioData.Timetable` of `ScheduledCue` (kind, tick, spread, room), validated; `Paniq > Cue` in a scene, baked in tick order; a scene with none keeps the level's | This is the whole of the tie-in to a future event editor: it edits this list. It is editable in the Inspector today | The editor wants conditions ("when the fire reaches the corridor"), which is the reactive Director |
| Phase 1½ | `DirectorSystem.Advance` runs after the player's commands and before the hazards | A cue's effect lands at each person's reaction tick in phase 4, so nothing moves on the tick a cue is called, and the fire's draws stay where they were | Never |
| Measured, not just asserted | `CrowdScaleMeasurements.HowLongASecondOfSimulationTakes`, editor numbers, ms per tick, base branch → this branch: 20 people 0.371 → 0.342; 50 people 0.594 → 0.542; 100 people 0.986 → 0.834; 200 people 1.534 → 1.224 | The quality checks ask for a measurement whenever a stone changes what the crowd does. The day costs nothing measurable; it is slightly cheaper, most likely because two people stood talking cost less to steer than two people walking. The measurement's crowd has no homes and no timetable, so only chats and toilet trips show in it | The next stone that raises the crowd |
| Versions 50 → 51, content 62 → 63 | All thirteen fingerprints re-recorded, including the three "no visitors" cases, whose comment says why | Every calm decision draws differently and the seated-at-start draw is gone | -- |
| The press watch ignores the floor | `PressWatch` (a test helper) no longer counts a thing pressed into the floor; `Run.DeepestPressIsIntoTheFloorForTests` says which pair was deepest | On seed 45 a burning office chair, bumped by two people fleeing the meeting, wedged itself tilted in the meeting room's doorway with a leg 81 mm into the floor for the rest of the run. The watch is for things passing into tables, walls, doors and each other; nobody sees a leg 81 mm into the carpet, and a chair wedged in a doorway is what doorways are for. A physics creak surfaced by the dice, not caused by the day; noted here so it is not lost | A thing seen resting visibly sunk into the floor in play |

**What was verified.** The cue and errand suites (14 tests: the Director
calls a timetable entry once on its tick; the meeting ending reaches the six
in the room and nobody on the tick it is called; the player's home time is a
root event and the cue names it; a bad timetable and a bad home are refused;
every new event type reads back as words; somebody sent home across the
building opens the doors on the way and sits on their own chair; a toilet trip
shuts the stall door, stays, opens it and comes back to the desk; a fright in
the stall drops the errand; a chat has both facing, a neighbour glancing, the
partners not glancing at each other, and ends a moment after one is
frightened; home time through an open way out gets everybody out calmly, each
in their own time, with the purse unpaid and no round begun, and the trigger
then ends the round on the spot; home time at a locked way out is a queue and
nobody panics; a glance at a noise interrupts an errand which then carries
on), the meeting room, sitting and calm-behaviour suites, and the thirteen
fingerprints re-recorded. The full suites at the commit: 423 edit-mode tests
and 16 play-mode tests pass, the 14 explicit measurements skipped as always.
Six older tests had their premises brought up to date rather than their
rules: buildings of their own clear the office's timetable, a door tried by a
calm errand has the cue as its cause, a cue may name a room or a person, a
person's own idea is a root cause, the leader test tries several seeds as its
sibling does, and the walking-about test keeps chats as short as the old
"walk over and stand" was and strips homes so the cafeteria pair walk too.

**What was not verified.** A watched round in the editor: nobody has yet seen
a toilet trip or a chat on screen. The scene bake with a `Paniq > Cue` placed:
the code compiles and mirrors the other bake steps, but no scene has one.

## Prototype 2 fix: a chair that will not come all the way out is sat on anyway (2026-09-24)

Found while the cue system was being reworked into steps, and fixed on its
own before it.

**What a player saw.** Somebody walking back to their desk took hold of their
chair, pulled at it, and if it would not come all the way out from the desk --
because the chair next to it was being pulled out at the same moment, or the
desk behind stood too close -- let go of it, stood about, and tried again.
When the pull did come out but the chair would not slide all the way back in,
they got up off the seat and kicked the chair over behind them as though
frightened, on a calm afternoon. Person 1019, sent home from the bathroom to
the office, spent 34 seconds at their desk without managing to sit, while a
neighbour pulled out the next chair along.

The code always meant a chair that will not come all the way out to be sat on
where it stopped, and one that will not slide all the way in to be settled
where it is; both fallbacks were written. Neither could ever run: the timeout
for the walk *to* the chair was tested first, on the same tick either fallback
would have made do, and it dropped the chair instead. Being already on the
seat when it fired is what made the chair go over.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The walk's timeout is the walk's** | In `ChairBehaviour.UpdateSitting`, once somebody has hold of the chair (pulling it out, lowering onto it, or riding it in), each part of the sit keeps its own time and the walk-to-it timeout, chair-taken and blocked checks no longer apply. Each part already had a way of making do at its own timeout, and now reaches it | Sitting down is the calm half's commonest move, and the day about to be built on top has everybody going back to their desk several times an hour. A sit that fails one time in ten at a crowded desk cluster is a sit that fails on screen every minute | A chair that stops short leaves somebody sitting visibly away from the desk. Then shift them the last bit as the chair scoots in, which the seat-scoot code already knows how to do |
| Versions | `SimulationCompatibilityVersion` 51 -> 52; `ContentRevision` 63 -> 64. Five of the thirteen recorded fingerprints re-recorded (seed 42 locked and opened, seed 46 opened, and both box runs); eight happen not to change | Whether a sit completes changes where people are for the rest of the run | Never |
| Test | `ErrandsEditModeTests.SentHomeAcrossTheBuilding_SomebodyOpensTheDoorsOnTheWay_AndSitsOnTheirOwnChair` is the check that reached this path, and the trace that found it (somebody sent home from the bathroom, with their neighbour sitting down at the same time) is what it exercises | -- | -- |
## Prototype 2 fix: being shoved about is not getting anywhere (2026-09-24)

Found while the cue system was being reworked into steps, and fixed on its
own before it.

**What a player saw.** On seed 41, with the fire set off at six seconds and
the way out opened, person 1011 fled the meeting straight at the long table
between them and the door, and stood pressed against its edge by the crowd
for the rest of the round, feet going, getting nowhere. Frightened people who
are stuck are meant to heave a table over, throw a thing clear, or think
again after a quarter of a second of getting nowhere. They never did, because
a single tick of getting somewhere wiped the whole count: a crowd that shoves
somebody thirty millimetres sideways and back every few ticks kept resetting
it just short of the line.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **A free tick forgives one stuck tick** | `PeopleBodies` still counts a tick stuck when somebody got less than a third of the way they wanted; a tick that got somewhere now takes one off the count instead of wiping it. Measuring progress *along the push* was tried first and dropped: the body's momentum lags a sharp turn by a dozen ticks, so every turn read as being stuck and a lone runner dithered between doors | Every "stuck" rule in the game (heaving a table, clearing a wedged door, giving way, trying another door, giving up a stroll or an errand) reads this one count, and a jostling crush is exactly when they are needed | Somebody in a slow-moving queue, stuck two ticks in three, now reaches the "think again" line where before they never did. Then the queue needs an order of its own rather than a stuck count |
| **A self-started round has a cause** | When the hazard starts on its own tick count rather than the player's trigger, `RoundSystem` takes the hazard's own start event as the round's trigger, so the survivors and the end of the round are blamed on it. Before, both were written with no cause at all, which broke the rule that every line of the read-back traces back to the fire, whenever a run happened to end inside a test's window | Found because the fix above emptied the building of a test run faster than it used to | Never |
| Test | `FurnitureEditModeTests.NobodyAndNothing_EverEndsUpInsideATable` skips a table more than about ten degrees off flat, not only one on its side: a desk mid-tip after a heave lifts and shifts its body's origin, and the authored rectangle drawn round that origin flagged somebody running past its edge | The check approximates a rotated table by its authored rectangle, which is a floor rectangle only while the table is flat | -- |
| Versions | `SimulationCompatibilityVersion` 52 -> 53; `ContentRevision` 64 -> 65. Eleven of the thirteen recorded fingerprints re-recorded; two (seed 42 locked, and the seed 40 box run) happen not to change | When somebody counts as stuck changes what they do next for the rest of a run | Never |
| Test | `CorridorStarersEditModeTests.NobodyFrightened_StandsStaringAtAWall` (seed 41, trigger 300, way out opened) is the check that reached this path | -- | -- |

## Prototype 2 decision: the cue system reviewed, and an errand is a list of steps (2026-09-24)

The owner asked, the same day the cue system was built, whether it expands
within the project's rules, what should change now, and what the next
simulation steps are, the goal being dynamic, random interactions that feel
alive and human. The review (mine and an independent reviewer's, in
`C:\Users\max\.claude\plans\prepp-for-event-system-squishy-rossum.md`)
found the delivery half right and the execution half hand-written per kind:
adding a cue touched eight files and three drifting lists of "what may be
scheduled", and of the ten cues likely next, two fit, three half fit and five
fought the shape. The owner approved the "now" part: three commits, of which
this is the first.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| An errand is a list of steps | `ErrandStepKind` (go to, sit on, stand for, say, talk, shut the door, open the door, leave) with `ErrandTarget` (home, a free stall, the partner, home or where they stood); `ErrandBehaviour` begins each step in turn, skips one that does not apply, and ends an errand aimed at something that is not there | The walker (room to room, doors on the way, waiting at a locked one, resuming after a glance) was the reusable four fifths of every errand; the per-kind fifth was where every new cue would be hand-written. At four kinds and fourteen tests it was a refactor; at ten it would have been a rewrite | A cue needs a step the vocabulary lacks (follow a person, pick something up, linger in a room): add the step, not a kind |
| A cue's behaviour is data | `CueDefinition` (kind, audience, host rule, written down, script, host script), one per kind in `ScenarioData.Cues`; validated: every kind once, every step known, ranges the right way round, a partner only for a pair, a host only for a room | This is what a future event editor edits: what a cue does, not only when. `TheBuilding.WithToiletStay` and `WithChatLength` show a test changing a script | The editor wants to add kinds without code, which wants a name instead of the `CueKind` enum as identity |
| One rule for what the timetable may call | `CueDefinition.IsSchedulable` (a cue that reaches a room or the building); the scenario, the Director and the bake tool all ask it | Three copies of the list would have drifted | Never |
| A chair is kept through a glance, and a sliding one is waited for | `TryResume` hands a person whose own chair is still theirs back to the chair behaviour; `BeginSitOn` waits a moment (up to three times) for a chair nobody is on that is still sliding or on its back | Found by a trace: a thud beside somebody walking to their chair dropped the chair, and a chair still sliding from their own pull was "not free" | Never |
| The host is up first, whatever the spread | `CueSystem.CallInRoom` starts everybody else in the room no earlier than a tick after the host, on top of their own reaction lag and drawn spread. Before, the host had a spread of nought but the same lag draw as everybody else, so somebody else with a shorter lag and a spread of one or two rose first, which the meeting-room test caught once the draws moved | "The host says so and is up first" is what the cue promises; the test asserted it and the code only made it likely | A cue with no host, or one where the host is meant to be last (the host walking the visitors out) |
| Six of thirteen fingerprints re-recorded; versions 53 → 54, content 65 → 66 | The port keeps the draws in order (seven fingerprints held, including two of the three "no visitors" cases); the six that moved are the two chair improvements and the host rule above | An honest refactor says which behaviour it changed | -- |

**Deliberately left for the next two commits** (approved together with this
one): the fixes the review found (a cue must not change what somebody is
doing on the tick it is called; a cue can get up somebody seated in their own
chair; home time persists so an unlocked door empties the building; cue
take-up reserves ticks; a chat partner must be upright; a locked stall is
remembered; home time is not positioned on the fire; doors are shut behind;
own-chair-only sitting; visitors walk back where they came from; longer desk
sits; only heard remarks logged; stall claims in one step) and the changes
that make people read as people (the host says something at the meeting end;
the hailed partner walks too; chats end one person at a time; doorway hails
excluded; wander on errand walks; traits gate what somebody takes up).

## Prototype 2 fix: the day keeps its own rules (2026-09-24)

The second of the three commits the owner approved from the cue system's
review, after the refactor that made an errand a list of steps. Every row is
a rule the day already claimed to keep and did not, or a thing that read as
wrong on screen, found by reading the code the day it was written.

**What a player sees.** Home time called while two people are talking no
longer snaps them apart on that tick: they finish talking and then go. The
meeting breaks up one person at a time even when its spread is nought. Home
time stands until everybody is out, so a front door unlocked a minute late
still empties the building, and nobody wanders off to the toilet while it
is home time. The two at the cafeteria table get up when the meeting-ending
cue reaches their room instead of sitting until something frightens them.
People shut the doors they opened behind them, so the floor is not all open
doors five minutes in. Nobody sits in somebody else's desk chair, and
somebody at their own desk stays there for a minute or so rather than
popping up after ten seconds. A visitor walks back from the toilet to where
they were standing instead of loitering in the stall. The story no longer
reads "person 3 said something" forty times a chat.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **A cue never changes what somebody is doing on the tick it is called** | `CueSystem.Hand` no longer wipes an errand under way. It goes into `AgentErrand.Next`, and `ErrandBehaviour.Finish` takes it up when the current errand ends. A pending errand (handed, not yet begun) is still replaced, since nothing has begun | It broke the owner's rule twice over: home time called mid-chat turned one talker away that very tick, and their partner ended the same tick for want of them | A cue that must interrupt (a fire drill): then a flag on the definition |
| **No two people take a cue up on the same tick** | `CueSystem.Staggered` reserves start ticks the way `FearSystem.Staggered` reserves reaction ticks: the first free tick at or after the drawn one, stepped by `PerceptionSettings.StartleStaggerTicks`, forgotten as the ticks pass, no random number drawn | The spread was statistical: with a spread of nought, six people at the meeting rose within the same eight-tick lag, two or three of them on one tick, most runs | Never |
| **Home time stands** | `CueSystem.HomeTimeTick` remembers it. Somebody free with nothing waiting on them, after `AgentHome.NextHomeTryTick`, is handed it again (`RemindOfHomeTime`, the same line in the story as its cause); giving up on it -- a locked way out stood at until they tired of it, no route -- sets that tick `DaySettings.HomeTimeRetryTicks` (**1500**, jittered) ahead. While home time stands nobody has ideas of their own: no toilet trip, no chat, no drifting back to their desk | The front of the queue gave up on the locked door after thirty seconds, the rest timed out at sixty, and nothing ever told them again. Unlock the door at thirty-five seconds and the building stayed. It is a state of the day, not a moment | Home time that can be called off (the disaster starts, the Director says work on) |
| **Home time is written down in the middle of the floor** | `CueSystem` works out the middle of all the rooms once and writes a building-wide cue there | It was positioned at the fire's origin, so the end card put "it was home time" on the fire | A level whose "middle" is nowhere sensible (an L-shaped floor); then a named point on the level |
| **Somebody seated in their own chair sits on for a while** | `ChairBehaviour.SitForAWhile`: when a cue that sends them home reaches somebody already in their own chair, the sit stops being "until told" and gets a drawn length of its own; the errand ends there | The two at the cafeteria table were exactly that, so the "lunch ends" line the last commit offered would have done nothing | Never |
| **A chat partner must be on their feet** | `ErrandBehaviour.PartnerOf` checks `Body.IsOnTheirFeet` | Knocked flat, they were talked to on the floor | Never |
| **A door found locked is remembered** | Giving up at a door that would not open sets `AgentDoors.AvoidUntilTick` for `DaySettings.LockedDoorMemoryTicks` (**3000**, jittered), which the route finder already honours | Somebody shut in a locked stall retried the handle every thirty seconds for ever, and every try was a line in the story | A player unlocking a door expects people to notice at once. Then a door swinging open clears the memory, as it already teaches a way out |
| **A door they opened is shut behind them** | `ErrandBehaviour.ShutTheDoorBehindThem`: the door somebody opened themselves on the way (`AgentErrand.OpenedDoor`), once they are a stride through it on the far side, is shut unless anybody else is within `DaySettings.DoorHoldMillimetres` (**2000**) of it, in which case it is left for them | Errands opened doors and never shut them, so five minutes into a calm half every door on the floor stood open, which changes how a fire and a noise travel and reads as a draughty office | The cruel slamming doors (which exists for strolls) should apply here too, or a trait should hold a door open longer |
| **Somebody with a desk chair sits only in it** | `ChairBehaviour.TryStartSitting`: a person with a home chair sits in it if it is free and within reach, and nowhere else; everybody else skips chairs that are somebody's own (learnt once from everybody's homes) | Musical chairs: anybody sat in the nearest free chair, including somebody else's desk chair, and the owner came back to find it taken and stood about | Communal chairs in a room with desks (a spare chair at a desk cluster) |
| **A visitor walks back from the toilet** | The toilet trip's last walk is *home or where they stood* rather than *home* | With no home to go to, a visitor's errand ended inside the stall and they strolled there | Never |
| **Desk sits are longer** | `DaySettings.DeskSitMinimumTicks` / `DeskSitMaximumTicks` (**1500** to **4500**: half a minute to a minute and a half) for a sit in one's own chair; the ordinary five to twenty seconds is for any other chair | Office workers popped up and down at their desks like a fairground game | Playtesters say the office looks dead; then shorter, or fidgets in the chair |
| **Only a heard remark is written down** | `SoundSystem.Say` returns without a line when the remark is not heard; a chat's first remark from each person is heard and logged, the rest are neither | "Person 3 said something" forty times in the read-back | The read-back wants a "they talked for a while" summary line |
| **A stall claim is one look** | `ErrandBehaviour` keeps, per room, who last set off for it as a stall; a claim holds while that person's errand is still about the stall. Asking whether a stall is free is that one look plus the people physically in it | It walked the whole crowd once per stall per toilet decision: three stalls times five hundred people, several times a second | Never |
| **The toilet rate allows for the stalls** | `ErrandBehaviour.ToiletEveryTicks`: the day's figure, stretched to `2 × people × longest stay / stalls` when that is slower, so the stalls are on average at most half full. Twenty people, three stalls, a thirty-second stay: every 400 seconds instead of 360 | Demand was a rate per person, so at five hundred people three stalls would have saturated for good and "try the toilet again" become everybody's idea | A level with a bathroom per wing; then per-wing rather than per-floor |
| **The host of a room cue: the lower ID on a tie** | `CueSystem.HostOf` breaks a leadership tie by ID, as the doc always said | It broke ties by index, which is equal to ID order today but not on a baked level whose IDs are out of order | Never |
| Versions | `SimulationCompatibilityVersion` 54 -> 55; `ContentRevision` 66 -> 67. Ten of the thirteen recorded fingerprints re-recorded; three (seed 40 cards, and the two seed 41 "no visitors" runs) happen not to change | Nearly every row changes where somebody is a minute in | Never |
| Tests | `CuesEditModeTests`: home time called mid-chat changes nothing on that tick and is taken up when the chat is over; a cue with no spread reaches no two people on the same tick; the meeting ending gets up somebody seated in their own chair. `ErrandsEditModeTests`: home time with the way out unlocked late still empties the building, without trying the handle all day; somebody sent home shuts the office door behind them. The meeting-room test now allows a visitor to sit down again some seconds after rising | -- | -- |

## Prototype 2: people, not clockwork (2026-09-24)

The third of the three commits the owner approved from the cue system's
review: the things that read as clockwork on screen, each a small change on
top of the step vocabulary, and most of the visible payoff of the review.

**What a player sees.** The meeting ends with the host saying something;
heads turn to the host, and then people rise one at a time. Two people who
are going to talk both walk and meet in the middle, instead of one standing
and waiting to be walked up to from six metres off, and when the chat is
over one turns away and the other a moment later. Nobody starts a chat in a
doorway. A walk to the bathroom or back to a desk wanders a little, like a
stroll, instead of running dead straight. And the cruel are contrary: half
the time the villain sits on when the host ends the meeting, or will not
talk to whoever came over, and the story says so.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The host says something as the meeting ends** | The meeting-ending cue's host script is *say, go home, sit*; `ErrandBehaviour.Begin` says anything a script begins with from where they are, seated or not, before the chair goes back. The remark is heard within the usual radius, so the table glances at the host, and the rest rise after | The meeting rose in silence with nothing to tell the watcher why. This is the "feeling, not event" form of an announcement: the room hears the host, then people get up | The remark wants words on screen |
| **A late start still takes a tick of its own** | `ErrandBehaviour.StartIfDue`, when somebody is past their start tick (a glance at the host held them), asks `CueSystem.ReserveStart` for the first free tick from now; the resume after a glance goes through the same door | With the whole table glancing at the host on one tick, everybody's reserved ticks had passed by the time the glances ended, and they rose together the moment they did | Never |
| **Both walk to a chat** | The one hailed walks too (`ErrandBehaviour.BeginGoTo`, partner target); each stops within the social stop distance of the other | The hailed one stood and waited to be walked up to from up to six metres, which read as a summons | A chat where one is busy at a desk and the other comes to them (a knock on the door): then a flag on the step |
| **A chat ends one person at a time** | `StartTalking`: only the one whose idea it was ends at the drawn tick; the other notices a moment later (their own reaction lag), with a long backstop | Both turned away on the same tick | Never |
| **No chats in doorways** | `CalmBehaviour.TryStartChat` starts none from a doorway, and `IsChatCandidate` skips anybody stood in one (`RoomAt` is -1 there) | Somebody hailed in a doorway stopped in it for the chat and blocked it for everybody | Never |
| **Errand walks wander** | The last leg of an errand walk (inside the room, not the approach to a door) carries the same wander offset a stroll does, redrawn every second or so and halved near the place (`ErrandBehaviour.Wander`) | Errand walks were dead straight; strolls wander, and the difference read as clockwork | A walk that must be straight (a leader's follow) |
| **The cruel are contrary** | `CueSystem.Refuses`: somebody cruel enough to defy a leader (`LeadershipSettings.DefiantMinimumEvil`) defies a cue with a person behind it `DaySettings.CruelIgnoreCuePercent` (**50**) of the time. Sat on when the host ends the meeting: they take it up `CruelSitOnMinimumTicks`–`CruelSitOnMaximumTicks` (**500**–**1500**) later. Will not talk: the chat never happens and the initiator thinks of something else. A cue from the clock (home time) is nobody's to defy. Only the cruel draw, so nobody else's numbers move. Each refusal is `CausalEventType.AgentIgnoredCue`, a line in the story ("person 13 sat on when the meeting ended", "person 8 would not talk to person 5"), blamed on the cue's line; a refused chat that was never written down is a root of its own | Cues bypassed traits: the cruel obeyed home time as readily as anyone, nobody ever refused a chat. `LeaderBehaviour.Obeys` was the shape to reuse, and a refusal is a story line | A refusal that changes the other person (the host trying again, the initiator offended) |
| Versions | `SimulationCompatibilityVersion` 55 -> 56; `ContentRevision` 67 -> 68. All thirteen recorded fingerprints re-recorded | Every row changes where people are and what the log says | Never |
| Tests | `CuesEditModeTests`: the host says something as the meeting ends, before anybody rises; a chat ends one person at a time; the cruel may ignore a cue and the story says so (and the line reads back as words). `ErrandsEditModeTests`: the hailed partner walks over too. The meeting tests count only standing up as rising (turning in the chair to look at the host is not), and pin the contrary chance to nought where they are about everybody getting up. Not covered by a test of its own: no chats in doorways, and the wander on errand walks (both are exercised by the suite, neither asserted) | -- | -- |

**Found on the way, left for later.** Everybody who hears a remark glances
on the same tick (`SoundSystem.Notice` is immediate for every listener),
which is the owner's same-tick rule broken for glances at a noise; a
visitor risen from the meeting may sit straight back down in the nearest
chair a few seconds later, since a chair just left is not remembered.

## Prototype 2 fix: pressed against a table is not cut off (2026-09-24)

Found merging the day into the rest of the office: the merged content's new
random draws sent a different person into a corner both branches had passed
by on their own.

**What a player saw.** On seed 41, with the fire set off at six seconds and
the way out left locked, person 1009 fled the meeting straight at the long
table between them and the door and stood pressed against its edge for half
a minute, heaving at it every second, feet going, getting nowhere. The heave
worked as designed (the table crept a hand's width each time); what they
never once did was step round it.

**Why.** The floor is walked as squares, and a square with less clear space
round it than a body's radius is one no route reaches. A body shoved right up
against a table's edge stands on such a square. Asked for the way to the
door, the route-finder found no route from there at all and fell back to
pointing straight at the door, through the table.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The nearest square a route reaches** | `Navigation` follows a route from the person's own square, or, when no route reaches it, from the nearest square within two (half a metre) that one does: the same room only, the cheapest of the first ring that has any, walked in a fixed order so a replay picks the same one. The first step is onto that square and the route takes over from there. "Can I get there" and "how far is it" recover the same way, so nobody pressed against a table reads as cut off from the building | Half a metre covers a body pressed into an edge, or a table shoved into somebody; further than that is a body inside something, which is the physics' business to ease out | A person is seen stepping through a wall to reach the floor beyond it (the same-room rule is what prevents it) |
| Versions | `SimulationCompatibilityVersion` 57 -> 58; `ContentRevision` 69 -> 70. Eleven of the thirteen fingerprints re-recorded; two (seed 42 opened, and the seed 42 box run) happen not to change. | Which way somebody pressed against a table turns changes the rest of a run | Never |
| Test | `NavigationRoutesEditModeTests.SomebodyPressedAgainstATable_IsPointedRoundIt_NotStraightIntoIt`; `CorridorStarersEditModeTests.NobodyFrightened_StandsStaringAtAWall` (seed 41, trigger 300, way out locked) is the check that reached this path | -- | -- |

## Prototype 2 fix: a door strolled through is forgotten (2026-09-24)

Found merging the day into the rest of the office, the same way as the fix
above.

**What a player saw.** Home time called, the way out unlocked late, and
everybody out but one: person 1018 reached the open front door with nobody
near and walked in a tight circle in front of it for half a minute, wandered
off, came back the long way round, and the round ran out before they were
out.

**Why.** Walls push people away from themselves so nobody scrapes along them,
except the wall holding the doorway a person is lined up with, or nobody
could walk through a door. Which doorway that is was answered in a fixed
order: the way out they are fleeing to, else the door they are strolling
through, else, on an errand, any open doorway. The door strolled through was
remembered after the stroll ended and answered ahead of the errand, so
somebody who had wandered through the cafeteria's shortcut earlier was, for
the rest of the day, lined up with that door and no other, and the wall
beside the way out pushed them back from it every time they came near.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **An errand may use any open doorway, whatever was strolled through** | `Agent.DoorwayInUse` answers the way out first, then, on an errand, any open doorway, and only then the door a stroll is going through; and `CalmBehaviour` forgets the stroll's door the moment the stroll ends | The errand's answer is the wider one and the right one whenever both apply, and a remembered stroll door was never meant to outlive the stroll | Somebody standing about is seen drifting out through a door for no reason |
| Versions | `SimulationCompatibilityVersion` 58 -> 59; `ContentRevision` 70 -> 71. None of the thirteen recorded fingerprints happens to change: no recorded run has somebody on an errand through a doorway with a stroll's door still remembered. | Which wall pushes whom changes the rest of a run | Never |
| Test | `ErrandsEditModeTests.AnErrandThroughTheWayOut_IsNotWalledOffByADoorStrolledThroughEarlier`; `ErrandsEditModeTests.HomeTime_WithTheWayOutUnlockedLate_StillEmptiesTheBuilding_WithoutTryingTheHandleAllDay` is the check that reached this path | -- | -- |

## Prototype 2 fix: pressing Play with no scene open plays the game (2026-09-24)

**What the owner saw.** Play pressed, a moment's loading, the Play button
blue, and nothing on screen. Unity had started with no scene to reopen (its
note of the last open scene was empty, and the title bar read `Untitled`),
so Play faithfully played a blank scene. The recent commits had nothing to
do with it; the editor log had no error at all. Best guess at the cause: the
previous session closed straight after a play-mode test run, which swaps
scenes in and out, and left the "reopen this" note blank.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Play on the blank scene plays the game** | `Assets/Paniq/Editor/PlayFromABlankScene.cs`: when Play is pressed and the only open scene has never been saved and never been touched, cancel the Play, open `Bootstrap.unity`, press Play again, and say so in the Console. Any scene somebody has changed, and the saved scene the test runner builds for play-mode tests, are left alone | To someone new to Unity an empty Game tab reads as a broken game, and the first thing suspected was the day's commits. A silent no is worse than a loud yes | The project has several playable scenes and the right one to open is no longer obvious |
| **"Blank" means never saved and never touched, not empty** | The scene Unity makes when it has nothing to reopen holds a camera and a light, so "no objects" never matched it (the first version of the guard did nothing, tried on the very scene the owner had). `Scene.isDirty` false and no path is the mark | Found by pressing Play on that scene through the bridge | -- |
| **The follow-up runs on `EditorApplication.update`, not `delayCall`** | `delayCall` waits for the editor's panels to refresh, which they do not while the window is minimised (as it is whenever a script drives the editor), and the scene never opened. A one-shot `update` handler, which the test bridge already relies on, does | The second try on the same scene | -- |
| **Not `EditorSceneManager.playModeStartScene`** (rejected) | Unity's own switch for "always start Play from this scene" | It starts every Play from that scene, including the play-mode test runner's, and nothing in the test framework package guards against it: the play-mode tests would break | Never, at that cost |
| Tests | None automated: the edit-mode test assembly cannot reference the loose editor assembly the script lives in, and the behaviour exists only on a button press. Verified on the blank scene Unity had actually opened, pressing Play through the bridge (`tools/RunUnityTests.ps1 -Menu "Edit/Play Mode/Play"`, the menu's name in Unity 6.3): the Console line, `Bootstrap.unity` opened, play mode entered, `FireReactionPrototype` loaded. `-PlayMode` passed with the guard in place, which proves the test runner's scene is untouched | -- | -- |

## Prototype 2 fix: a seated body is not told to stop spinning (2026-09-24)

Found on the way to the fix above: the previous editor session's log was 9 GB,
all one warning, *"Setting angular velocity of a kinematic body is not
supported."*, once per seated person per tick.

**Why.** Somebody sitting is pinned to the chair: their body is kinematic (it
goes where it is put and nothing pushes it). Every tick of the sit-down,
`PeopleBodies.MoveSeated` called `PhysicsWorld.SetUpright`, which zeroed the
body's spin, and the engine refuses that for a pinned body and says so. The
game was unaffected, since the order was ignored anyway, but the Console
filled with yellow warnings whenever people sat and every test run paid for
writing them to disk.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The spin is zeroed only on a body that is free to spin** | `PhysicsWorld.SetUpright` skips `angularVelocity` when the body is kinematic, the way the same file's other velocity writes already do. Constraints, damping and rotation are set as before | A warning a tick per seated person, and a 9 GB log | Never |
| Versions | Unchanged. The write was a no-op, so no run changes and every recorded fingerprint stays | -- | -- |
| Test | `SittingEditModeTests.SomebodySittingDown_DrawsNoComplaintFromThePhysicsEngine` listens on `Application.logMessageReceived` through a sit-down, because Unity's runner fails a test on an error but not on a warning. It failed before the fix and passes after | -- | -- |

## Prototype 2: the office floor, second pass (2026-09-24)

The owner's second playtest of seeds 41 and 42 listed nineteen things; the
building ones were props all facing the same way, a door between the meeting
room and the cafeteria, a bigger closet, and "no doors locked apart from the
exit". Asked how literal the last one was, the owner said they meant none
locked from the start, and that if a locked door is the bully's doing it
stays. Only the way out starts locked, so nothing changed there. The rest was
chosen on the owner's behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Which way a prop faces | A prop's facing is the wall at its back: its front (drawers, glass, shelves) is drawn on its -Z side and turns to face the room. Every helper that places a wall prop takes a facing; the corridor copier and the office's north-wall shelves keep north | The owner saw shelves standing side-on to their wall. One rule, written once, beats a note per prop | A prop that has no front, or one meant to stand free in a room |
| Where the new door goes | Door 2017, in the meeting room's east wall at z 14000, 1 m wide, unlocked: the stretch of that wall clear of the meeting table, the bookshelves and the cafeteria's table. ID 2017 keeps every existing door's index | The owner asked for the door; the place is the only clear metre of that wall | The meeting room or the cafeteria is refurnished |
| How the closet grows | North to the corridor wall (2 × 4.5 m), keeping its door and its old floor, with two shelves of stores along its east wall | "Expand" left the size open. North is the only side with empty floor behind it that costs no other room anything, and keeping the old floor inside keeps the dozens of tests that name spots in it | A floor plan that wants the closet to open onto the corridor as well |
| A test made honest | The closet test for "nobody shuts a door they are about to run through" frightens its person by hand and names the closet door. It used to pass because a calm stroll in a two-metre closet ended in the office | Growing the closet exposed it; the fix it then found is its own commit | Never |

## Prototype 2: fear and attention spread (2026-09-24)

Four of the owner's second-playtest notes were about noticing: the meeting
rose one seat at a time, shouts did not carry, nobody saw past three metres
or through a doorway, and the bathroom pair looked at the wrong noise.
Reproducing them showed why it mattered: whenever the fire starts outside the
open office, everybody in the other rooms stays calm until the flames reach
them, and on seeds 40 and 42 all twenty die with the way out standing open.
Asked how fright should spread, the owner chose *seeing someone bolt startles
you*. The rest was chosen on their behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Seeing somebody bolt | A frightened person running (40 mm a tick or more) or leaping out of a chair, within 8 m and along a line of sight, startles whoever sees them (`AgentAlertSource.SawSomeoneRun`), each after their own reaction lag and staggered; bravery 7 or more turns to look instead | The owner's choice; the cheapest true thing about a room full of people | A curiosity or trust feeling replaces bravery as the gate |
| The first shout | Comes with the first stride, at the reaction lag (2 to 8 ticks), then at the old interval | The old two to five seconds was the whole reason a table rose seat by seat | Never |
| Shout reach | Understood 6 m off, heard 12 m, halved by a shut door (was 2.5 / 6) | Six metres is a room; the owner asked for shouts that travel | A level with rooms much bigger than these |
| Sight | 12 m (was 3), and through open doorways only along the line of sight, one or two hops (`WorldGeometry.CanSeeBetween`); the wall beside an open door hides | The owner asked to see the room and through doors; the old rule saw through walls next to a door | A room bigger than 12 m across, or a threat that hides |
| A fire is heard as it grows | 3.5 m for one square plus 50 mm a square, at most 15 m; half that through a wall or a shut door (`IThreat.HeardWithinMillimetres`, `SoundSystem.HearThreats`) | One square crackles, a room roars; a whole floor hearing a bathroom fire through the walls at full reach would be too much | A smoke model, which is what people really notice through a door |
| Several noises | Up to three pending per person; a threat's noise or a louder-and-nearer one takes over, the rest wait 300 ticks; the same noise within 1 m and 300 ticks turns no head twice | The owner asked for awareness of several sources; the twitch of re-noticing the same fire every second cut short every errand | Never |
| Going to look | A person's own cue (`CueKind.GoAndLook`, `ErrandTarget.TheNoise`): toward the noise, doors opened, stop 2.5 m short, a moment's look; not nervousness 8+, not with a cue pending, not again for 900 ticks | Not on the list, but without it nothing in the list reaches a room whose door is shut, and that is every room on seeds 40 and 42 | A smoke model; or a level where curiosity should get people killed less often |
| Cost | At 500 panicking people in the stress building: 14.98 ms a tick before, 15.58 after (editor); calm 200 people 1.46 before, 1.43 after. Within run-to-run noise, so no cache | Measured with `CrowdScaleMeasurements` on both trees | A measurement past about 2 ms for perception alone |

## Prototype 2: doors and the cornered (2026-09-24)

The owner's notes: doors shut on people behind them ("closing a door should
be rare unless no one is behind, or a calculated risk to save themselves --
selfish or evil, so it should match personality"), workers who would not go
into a dead end even with the danger blocking the only right way ("they
should still want to save themselves"), the bathroom pair on seed 42 who
rarely got out, and an extinguisher carrier strolling toward the fire. Asked
what a worker should do with the flames between them and the only door, the
owner chose *dash past or hide, by bravery*. Defaults chosen on their behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Who shuts a door on somebody coming | Nobody, except the callous (`CallousCompassionMaximum` 3) once the flames are at the door; the same gate for wedging a door shut. The cruel (evil 8+) slam and 9+ lock as before | The owner's rule: shutting a door on people is selfish, so it takes a selfish person; compassion 7 as the bar for holding one left most people shutting doors in faces | A trust or anger feeling that changes who holds a door |
| When to dash | The way out is through the heat (approach inside the danger distance, or the next room alight) and the floor to it is clear (`DashClearanceMillimetres` 500: no burning square within half a metre of the straight walk or the spot), and either bravery is 5 or more (`DashMinimumBravery`), or their own room is alight, or no refuge is reachable outside their danger distance | The owner's choice; the desperation clauses came from seed 42, where a timid person's only refuge was also past the flames and hiding meant shuttling to death | A route search that walks round a fire rather than a straight-line test |
| How long a dash holds | `DashTicks` 150, jittered, then decided again; while it holds, the danger distance neither turns them back nor makes them abandon the door, and the door they dash for is not shut against the fire | Three seconds is a doorway and a stride; deciding again keeps a dash from becoming a walk into a fire that has grown | Never |
| Where to hide | The room furthest from the flames they can reach (as before), but only if the walk to it does not cross burning floor; otherwise they keep clear of the flames where they are | Heading for a stall through the fire, bolting back, and heading for it again was the shuttle the owner watched | A route search round the fire |
| A door not yet decided about | Somebody frightened shuts no door before their first thought about a way out, and none they are dashing for, even with flames at it | The flames-at-the-door rule used to slam the closet door on the tick of the fright, before dash or hide was ever chosen | Never |
| The carrier's pace | Panic speed to the flames, not walking pace | Seed 41, the owner's note | Never |
| What it did to the recorded seeds (door opened 12 s after the trigger, 100 s watched) | 41: 18 saved / 2 lost (was 14 / 6 before this and the fear stone; 15 / 5 at the start of the day). 42: 12 / 5 (was 1 / 16; 0 / 20 at the start). 40: 5 / 15 (was 3 / 17; 0 / 20). 46: 3 / 17 (was 3 / 17; 2 / 18) | The bathroom fire on 40 and the cafeteria fire on 46 still cut the corridor before most people know; they are the seeds the player's alarm is for | The alarm the player can pull (next stone) |

## Prototype 2: the seed 41 exit, diagnosed (2026-09-24)

Two of the owner's seed 41 notes were "why stuck?" questions: a crowd stood
at a closed corridor door, and people with a very hard time getting through
the opened exit, one of them apparently stuck in the door. Both were
reproduced with scratch tests before anything was changed (the pattern is
`CorridorStarersEditModeTests`: a whole run watched every second, and
`Run.DescribeForTests` for whoever misbehaves).

| Item | Finding | What was done |
| --- | --- | --- |
| The crowd at a shut corridor door | Not reproduced. Four seeds × three trigger times × way out locked or opened, watching for anybody frightened within 2.5 m of a shut, unlocked inner door for ten seconds: nobody. The waits found were at the locked way out (correct: the player's door) and at inner doors the bully had locked (the owner keeps him) | Nothing; reported. The corridor changed under the fear and door stones anyway |
| The jam in the opened exit | With the crowd gathered before the door opens: nobody out for the first fifteen seconds; 86 shoves, 20 knock-downs, 5 crushes and 4 knocked out cold within 2.5 m of the exit in 80 s. Four cruel people shove the weaker to the floor in the gap, and whoever is down inside the 1 m doorway plugs it. On the early-open case the bully locks the front door from outside with sixteen still inside; the strong break it down forty seconds later | A body down inside an open doorway with the crowd pressing on one side is hauled through it by the press (`DoorBehaviour.CarryTheFallenThroughDoorways`, `PeopleBodies.CarryToward`: the helpers' drag, but it moves a crumpled body too), at `CarryThroughSpeedMillimetresPerTick` 40 while anyone upright is within `CarryThroughRadiusMillimetres` 1000 on one side, and on out to the escape depth once past an exit's wall line. Seed 41, door opened at tick 1500: 2, 7, 15 out at 5, 10, 15 s (was 0, 0, 2); at 2400: 3, 9, 15 (was 8, 16, 18 -- that run had the door broken down early). The shoving itself is left as it is: it is the crush the game is about, and the owner's note was about getting through |
| The bully locking the front door | Kept, by the owner's answer on locking. The story and the pop-up sign say who did it | Nothing |

## Prototype 2 decision: three cards, thirty to start, and the alarm (2026-09-24)

The owner asked for exactly this: "keep only cards: beefcake, tnt, add fire
extinguisher", "have player start with one random card, and 30 activity
points", and "fire alarms should be able to be pulled (clicked) for 30
points". The extinguisher card already existed as `SpawnExtinguisher` (four
spares stood anywhere the player clicks), so "add" meant keep it. Chosen on the
owner's behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| What happens to the other six cards | They stay as commands (`PlayerCommandType` is append-only and part of the fingerprint) with their handlers and tests; only `DeckSystem.Deck` shrinks. A level puts them back through `InfluenceSettings.StartingHand` | Deleting them would be a refactor for its own sake, and a coward or saint card is still on the direction list | A card is wanted back, or the list is final |
| The opening card | `InfluenceSettings.OpeningDrawCount` 1, drawn in the deck's constructor from the deck's own stream (sequence 55), logged as `CardDealt` at tick 0 with no source, so the story says "you were dealt X to start". Both finite cards are in supply at the start, so nothing is excluded from the draw | The owner said "random"; the deck's stream means the same seed opens the same and nothing else in the run moves | A level wants a chosen opening hand (it has `StartingHand` for that) |
| "Activity points" | The influence meter as it is; nothing renamed | The owner's word for the meter in the note, not a request to rename it | The owner asks |
| The alarm's price and rules | `PullAlarmCost` 30, paid only when the bells actually start; a pull on ringing bells is refused for free; a pull the player cannot afford does nothing. `PowerPulledAlarm` is a root event of the player's (uproar tier: nothing, like every Power event) and the bells name it as their cause | Priced like a card, it is the one move the opening thirty always buys | The alarm proves too strong an opening move (the seeds' hands-off numbers are in the decision above) |
| Tests that count cards | `TheBuilding.WithThePlayerAbleToAct` sets `OpeningDrawCount` to nought; the economy tests that count a hand set it themselves | A test about playing two cards is not about the opening draw | Never |

## Prototype 2: the Menu button and the alarm click (2026-09-24)

The owner asked for a replay button top right "for quick reset to menu", and
for alarms to be pullable by clicking. Chosen on their behalf:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| What "menu" is | The start card: `LevelLoader.BackToTheStart(runner.Seed)`, which reloads the scene with the seed kept and the card up. There is no other menu | The start card already holds the level, the seed box and PLAY; it is what the owner returns to | A level select exists |
| Where and when the button shows | A 110 × 30 IMGUI button at 20 px from the top-right corner, drawn by `RoundScreens.DrawStrip` whenever the round is not waiting to start (so on the end card too, not on the start card). The Tab stats table moved from y 20 to y 60 to sit under it | The corner the owner named; the start card is the menu, so the button would be pointless on it | Any HUD redesign |
| How an alarm is clicked | `RoomView.CreateAlarm` gives the box a `BoxCollider` about 0.45 m across (2.5 × 2 × 2.5 in the box's own units), registered in `alarmByCollider`; `PlayerInput.UpdateDoors` tests for an alarm before a door and queues `RunDriver.QueueAlarmPull`. The hover line under the score says the price, "already ringing", or that the player cannot afford it | The drawn box is a hand's width, and a click on a hand's width from across the room is a miss | Alarms drawn bigger, or a general "click anything" picking pass |
| No version bump | Nothing in the simulation moved: the command and its price were the previous commit | Presentation only | -- |

## Prototype 2: cards that look like cards (2026-09-24)

The owner asked for the playable cards to "look more like cards and more
compact". Chosen on their behalf, all in `PrototypeHud.DrawCards`:

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| Shape and size | Portrait, 96 × 132 px with an 8 px gap, drawn with IMGUI rectangles and the white texture as before: a light edge, a dark face, a badge with the key top left, the name in bold, a one-line blurb, the price along the bottom | A shade under a playing card's 2:3, and six of them sit under the score strip at 1366 wide. No textures and no UI package, as the rest of the HUD | A real UI pass, or art for the cards |
| What a picked-up card does | Lifts 10 px and turns blue (edge and badge) | The old row used colour alone; a lift reads as "in hand" at a glance | Never |
| What an unaffordable card does | Dimmed red edge and face, pink ink, and the price line reads "30 (you have 12)" | The old row only tinted it; saying the shortfall answers the question the player has | Never |
| The purse bar | Above the hand, as wide as it (at least 300 px), the hint line above that | It used to be a fixed 420 px beside a row that could be any width | Never |
| Styles | Four `GUIStyle`s built from the skin on first use and kept | The skin only exists during a GUI event, and a style per frame would allocate | Never |

## Prototype 2: playtest fixes, third round, the game layer (2026-09-25)

The owner's seventeen notes, the game half. Every default below was chosen on
the owner's behalf and is theirs to overturn; the owner's own rules are marked.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Where the stockroom is** | `(6000..16000, -6000..-500)`: 10 m by 5.5 m, the building's south edge straight | "Behind the bathroom", reaching the arm of the corridor away from the exit, leaves one shape; a straight south edge suits the tower look | A different footprint: four numbers and the box layout, an afternoon |
| **Which corridor it joins** | The crossbar's south arm, no longer a dead end | The only corridor a room behind the bathroom can touch; "corridor opposite the exit" read as the arm of the T away from the exit arm | The owner meant something else |
| **Stall widths** | 1500 / 2000 / 1500 (the middle stall wide) | 5 m does not split into three on the 250 mm grid; a wide accessible stall is what real bathrooms have | Never |
| **The stockroom's doors** | In the office's east wall at z -4000, and in the crossbar's south wall at x 14500 | As far from the office's corridor door as the wall allows, so a fire at one door leaves the other; the crossbar door straight under the way out | Never |
| **Fire start areas** | Unchanged: no stockroom fire | Adding an area changes which room every seed starts in, and the owner plays seeds 41 and 42 as reference | The owner wants stockroom fires |
| **The boxes** | Thirty-two, ids 3501 onward, sizes 250-800 mm, along the walls and in an island south of the lane; the straight line from door to door kept a metre clear; the first row 1.5 m from either doorway | Loose things are not on the map people steer by (`NavigationGrid` takes rooms, tables, walls and doorways), so people are steered along the door-to-door line and only dodge a box when they reach it; a box at rest in a doorway jams the door | Somebody is seen stuck in the stores |
| **Swing doors and sight, sound, routing** | Open for everything but the fire: `DoorState.Open` from the start, never shut, locked or battered | The simplest model that is coherent everywhere `IsDoorOpen` is asked; cafeteria swing doors have windows | Sound through them should be muffled, or a lock wanted |
| **Swing door fire time** | `ExitSettings.SwingDoorBurnThroughTicks` 450, half a shut door's 900 | **The owner's rule:** "swinging doors stop fire half as good as normal ones" | One number |
| **Propped swing doors** | Something wedged in the gap lets the fire through: `DoorRuntime.Obstructed`, written by `DoorSystem.ResolveBlockages`, read by `WorldGeometry.FireCanCross`; the fire beside a door just propped is woken as beside a door just opened | The geometry holds no door system to ask, and a burning square with nowhere to go is retired for good until woken | Never |
| **Sounders** | A new kind of thing, `PhysicsObjectKind.AlarmSounder`: bolted to the wall like a socket, a plate 2.1 m up above every head, ignites after 80 ticks and pops like a laptop; seven, one per room people use | Reuses ignition, the bang, the fling, the story and the signs for free; a bell that pops is a thing on the wall the flames reached | Never |
| **Which bells ring** | Every sounder still `Intact`; a floor authored with no sounders rings from its pull stations | So every test building still sounds; `Wreck` never marks a kind without `BreakMomentum`, so burn state is the only test | Never |
| **Re-ringing** | `AlarmSettings.RepeatTicks` 300, each bell drawing its own beat (a seeded offset at the pull, then jittered) | A door opened later lets the news through; the owner's rule that nothing happens to a whole group on one tick | One number |
| **The composed walk-out** | Deleted outright: `Composed`, `BreakComposure`, `StaysComposed`, `ComposureGap`, `IsComposed`, the calm flee pace | **The owner's rule:** "pull it, and everybody panics". Dead code that described the old rule would mislead the next reader | Restore from git if a fire-drill cue ever wants it |
| **A bell before any fire** | `PerceptionSystem` gates only seeing and hearing the threat on there being one; a startle turning into fright runs regardless | An alarm pulled before the fire left everybody startled and staring at the bell until the flames came, which is what the owner saw | Never |
| **Who pulls the alarm** | Bravery 6+, or leadership 6+, or compassion 6+ (`AlarmSettings.PullMinimumBravery`) | **The owner asked** for the brave; the other two stay because they are people who think of others | Numbers |
| **Barricading with no fire** | Off until a threat is active (`BarricadeBehaviour.Decide`) | A bell alone should send people to the doors, not to blocking them | Never |
| **Extinguisher burst** | Ignites after 200 ticks of heat, pops at once with radius 1600, speed 60, no fresh fire; `ObjectKindSettings.PopDouses` empties it over the circle (squares, then things, then people, the spray's order) from `FlammablesSystem.Pop`; a burst bottle has no fuel, which is all "spent" needs | **The owner asked** for the explosion; a full bottle bursting sprays its contents, so an unfetched card becomes a small rescue rather than a bomb | The dousing reads as a free extinguisher |
| **Dormant things and heat** | `FlammablesSystem` skips a dormant loose thing in the heat pass and the touch pass | The spares sit at (0, 0) in the office until placed; and the dormant lamp shade at its lamp's spot could already be lit and light the floor from nowhere | Never |
| **Prices** | `Maximum` 100; `OpenDoorCost` 10, `CloseDoorCost` 10, `UnlockDoorCost` 10, new `LockDoorCost` 10, new `UnlockExitCost` 100 | **The owner's rules:** open and lock cost 10, the purse holds 100, the way out costs 100 to unlock; unlocking an inside door was not named, so it costs what any inside-door move costs | Numbers |
| **The key** | `PlayerCommandType.ToggleLock` (appended): locked to unlocked, shut to locked, open to shut-and-locked when the doorway is clear, refused for nothing otherwise; `ClickDoor` keeps its old meaning so forty tests that unlock the way out with two clicks stand | The input layer decides which click sends which command; the commands stay general | Never |
| **Locking the way out** | Allowed, at the inside lock price | Unspecified; symmetrical with the bully | The owner objects |
| **Stick together** | `GroupSystem`: a pull toward the middle of the other members within 8 m, scaled by cohesion `60 + 3 × nervousness - 2 × bravery - 6 × evil` (evil 7+ ignores it), fading in over 2 m past the first metre and off while another member is within 0.8 m; whoever has the group behind them drops to 70 % of their cohesion off their pace; the door the most leaderly member runs for scores +3000; members share wayfinding every ~100 ticks; all round long | **The owner asked** for a card whose people "stay as a group, not a 100 % hard rule, personality should still play in". Steering only turns a person, so waiting had to be a matter of pace; a full-strength pull at running speed steered members into each other and a hard collision put both on the floor | Playtesters say the knot looks tethered, or falls apart |
| **A group of one** | Two or more make a group; a throw that catches one or none is a miss, free, and writes nothing | A group of one is no group; the log is append-only, so who was caught is settled before the first event goes in | Never |
| **The deck** | Beefcake, TNT, the fire extinguisher, Stick together | The owner said "add card" | The owner |
| **The cafeteria's shortcut** | Door 2009, the cafeteria's door onto the crossbar's north arm, removed; the cafeteria keeps its swing doors and the meeting room's door | **The owner asked** (2026-09-25): "remove the door from the cafeteria closest to the exit" | The owner |
| **The switch beside the way out** | Pull station 6006, on the crossbar's east wall in the north arm below the exit, removed; five stations remain, one in each big room, and the seven bells are untouched. `ContentRevision` 78 -> 79, `SimulationCompatibilityVersion` stays 66 (content, not a rule); the seed 42 fingerprints were re-recorded because somebody who used to run for that station now runs for another, and seeds 40, 41 and 46 did not move | **The owner asked** (2026-09-25): "remove the fire alarm switch in the corridor next to the exit" | The owner |
| **The office's copier** | Moved from the east wall (5500, -2000) to the west wall (-5550, -2000), the one office wall with no door in it | With the stockroom door in the east wall, the crowd rolled the copier along that wall into the doorway's approach and two people pushed it from opposite sides for the rest of the round (seed 42, found by the seed 41/42 harness). "Blocked" resets whenever a body creeps a millimetre, so neither ever gave up; that is the deeper fault, left for a stone of its own | Somebody is seen pushing furniture against somebody else |
| Versions | `SimulationCompatibilityVersion` 64 -> 65 -> 66; `ContentRevision` 76 -> 77 -> 78. All thirteen recorded fingerprints re-recorded twice: the floor plan alone moves every run | Where the walls are, what a bell does and what a door costs are rules of the run | Never |
| Tests | `StockroomEditModeTests`, `SwingDoorsEditModeTests`, `GroupsEditModeTests` new; `AlarmsEditModeTests` rewritten around "everybody panics", the re-ring and the popping bell; `PowersEditModeTests` for the key and the purse; the frozen-person tolerance in `SimulationEditModeTests` is now "never a step of their own, and never shoved more than four metres" | -- | -- |

## Prototype 2: playtest fixes, third round, the controls (2026-09-25)

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **The click gate** | `HudHitTest`: every card and button claims its rectangle as it is drawn in `OnGUI`, and the next frame's `Update` treats a pointer over any of them as the HUD's, with no hover and no world click. The rectangles are a frame old | The world is read in `Update` and the HUD drawn later in the same frame, with nothing between them: a click on "Trigger event" with a door under it clicked the door too, and a click on a card with another in hand would have thrown it at the floor behind | A layout that moves under the pointer |
| **The double click** | `DoorClicks`, plain arithmetic: a single click waits `WindowSeconds` 0.3 for a second one on the same door, then is sent; a second click inside the window turns the key (`ToggleLock`) instead; a click on another door settles the first as the single click it was; pausing or a card going up forgets it | Acting on the first click at once would open the door, and charge for it, before the double click was known; 0.3 s is about fifteen ticks, less than the lag everybody already reacts with | A playtester feels the wait |
| **A single click on a locked door** | Sends nothing; the hover line says "double-click to unlock" and the price | **The owner's rule:** single click open/close, double click lock/unlock | The owner wants one click to unlock |
| **Number keys** | Removed, with `Pick` and the key badge | **The owner said** "clickable instead of using numbers", and a stack of two has no number to name | A playtester misses them |
| **Stacks** | The hand is drawn grouped by kind in first-dealt order, with "×N" in the badge; `SelectedCard` is a kind and `DeckSystem.Discard` already removes one of a kind, so the run needed nothing | **The owner's rule:** cards of the same type stack | Never |
| **Buttons** | Reset (the old Menu) at the top right, y 36; Pause under it, y 72; Trigger event bottom centre, 220 × 36, gone once pressed (no grey "Event triggered" placeholder), kept right of the hand on a narrow screen; the "PAUSED" label dropped, the pause panel says it | **The owner asked** for each of these | Never |
| Tests | `DoorClicksEditModeTests` for the pure decision class and the hit test. The locked-door rule and the card buttons live in `PlayerInput` and IMGUI, which the play-mode tests do not drive; checked in the editor instead | -- | -- |

## Prototype 2: playtest fixes, third round, the drawing (2026-09-25)

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Swing leaves** | Two hinges at the gap's ends, each with a leaf half the gap wide and its own click collider; a spring on an "open" value (stiffness 120, damping 8, underdamped) whose target is +1 or -1 while a body is in the doorway strip and 0 otherwise; the side is set by where the first body came from and held until the gap clears | **The owner asked** for leaves that swing open and closed with some bounce, pushed through dynamically; an underdamped spring flaps past shut and settles, which is the bounce; holding the side stops the leaves swinging back through somebody halfway | The flap looks wrong |
| **Which way a pusher pushes** | Somebody inside the door's room pushes the leaves outward; somebody outside pushes them in | Away from the pusher, as a real door gives | Never |
| **The bell and the station** | `BoxViews` draws an `AlarmSounder` as a red plate with a dome at 2.1 m, flashing twice a second while ringing and only while intact; `RoomView` draws a pull station as a smaller red box with a white bar at 1.1 m, steady, with the same big click collider | **The owner asked** for the noise-makers and the triggers to be different things; the flash moved from the station to the bell because the bell is the thing making the noise | Never |
| **The banner** | A 28-pixel band across the very top, deep red, "FIRE ALARM" in bold, alpha pulsing on the bells' beat; the band's height is kept whether or not it shows | **The owner asked** for a red banner; keeping the space stops the lines under it jumping when the bells start | Never |
| **The packed top** | Four lines at y 36, 58, 80 and 104: tick and fire together, the head count, the round's score on its dark backing (moved out of `RoundScreens`), the hover line; the stats panel under the buttons at y 108 | **The owner asked** for the screen packed toward the top; the round's score belongs with the other numbers | Never |
| **The group band** | A thin cylinder child of the capsule at ankle height, coloured from a four-colour palette by group number | **The owner's card** needs its people telling apart; a child of the capsule runs and falls with them for free | Groups outnumber the palette |
| Tests | None of this reaches the run; `RoundPresentationPlayModeTests` and `BootstrapSceneFlowTests` still draw and still find the door leaf by name | -- | -- |

## Prototype 2 decision: a small gear for the tests (2026-09-24)

The owner noticed that most of the time spent waiting on an assistant is the
assistant running the whole test suite, and asked whether that routine was
necessary, best, or most effective. It was necessary once (a play-mode test
sat broken for a day when nobody ran that half) and it is not effective now:
425 tests take about 172 s inside the editor, and a task of six steps was
paying that after every step, re-proving what the step could not have
touched. The rule in `AGENTS.md` said "run the available Unity tests before
claiming validation passed", which every assistant read as "everything, every
time".

Nothing here makes a test cheaper or a commit less guarded. It makes the
in-between runs smaller and the full run once per task. **Every default below
was chosen on the owner's behalf**; the owner decided the scope (the rule and
the runner, not the rule alone) and the branch.

| Item | Decision | Why now | Revisit when |
| --- | --- | --- | --- |
| **Two gears** | After every edit, `tools/CompileAgainstUnity.ps1` (seconds, no editor). When a step has a claim worth checking, `RunUnityTests.ps1 -Filter` with the areas touched, plus `ReplayFingerprint` when simulation code changed. Before the commits of a task, and after any change to shared simulation code, `-All`. "Validation passed" means `-All` passed; a targeted run is reported by name | Each turn of the edit-and-check loop cost the same as the final check. The loop is right; the gear was wrong | The full run drops under a minute (one gear would do), or a targeted run lets something through that `-All` then catches before the commit more than a couple of times (the small gear is too small) |
| **Test when a step has a claim, not when a file is saved** | Batch the edits of one behaviour, then run its tests | The compile check already catches what a single save can break, and the bridge's refresh and poll make even a tiny run cost tens of seconds | — |
| **Several names in one filter** | `-Filter Doors,ClosingDoors`. The script writes one `filter=` line per name; the bridge's `Values` reads them all (its `Parse` keeps only the last of a repeated key, which is right for `id` and `mode`) and a test matching any of them runs | One request per area meant one editor round trip per area | Never |
| **A coverage table in the workflow doc, not a `-Changed` switch** | `docs/development-workflow.md`, *Which tests cover what*, lists the ten cases where the test is not named after the code, and the files that only whole runs check. **Designed and rejected**: a switch reading `git diff` and mapping files to tests through a 25-row table inside the script | The table would have had to grow with every new file, and a tool that names the tests for a change implies it knows which matter, when only the full run does. Assistants know what they changed; a table they read costs nothing to maintain but the row | The table is found wrong more often than it is read, or a third area gets a name nobody can guess |
| **`-Slowest N` and `-LastRun`** | The bridge always wrote each test's seconds to `result.txt`; the script now sorts and prints the top N after a run, or from the last results with `-LastRun` and no editor | The 2026-09-23 trims were made by feel with the durations in the file all along | — |
| **The canary's own cost is unmeasured** | `ReplayFingerprintEditModeTests` is 13 full 3,000-tick runs and is the small gear's safety net for simulation changes. Nobody has timed it alone. **First thing to do once this lands**: `.\tools\RunUnityTests.ps1 -Filter ReplayFingerprint -Slowest 13` and record the number here | Recorded rather than guessed: if it is near a minute, the small gear for simulation work is not small | That measurement. Slimming the canary (fewer recorded runs, or shorter ones) is then its own decision, not this one |
| **`-All` stays two requests** | One per half, as decided on 2026-09-23 | Merging them saves one asset refresh (seconds of 172) and needs the bridge to carry a two-phase run across play mode's domain reload | Never, at this cost |
| **No continuous integration** | No service runs the tests on push | Unity in CI needs a licensed editor on a hosted runner; nothing in the agreements documents a need, and the `-All` rule guards the same commit | A second regular contributor, or a commit reaches the branch twice without its `-All` |
| **`UnityPhysics` left as is** | The category stays on `PhysicsFoundationEditModeTests`; only the wording of the examples changes, since every test needs physics now and the name no longer says who | Renaming is scope creep in a tooling change | A second class earns the category |

**Verified here:** nothing that runs. This change was made on a machine
without Unity or PowerShell, so the script was re-read and diffed, not
executed, and the bridge was not compiled. **The owner's first run** of
`.\tools\CompileAgainstUnity.ps1`, then `-Filter Doors,ClosingDoors -Slowest 5`
with the editor open, then `-All`, is the verification.

