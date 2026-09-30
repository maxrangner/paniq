# The test levels

Three blank levels for watching the crowd rather than playing a round
(2026-09-30, the owner's ask: "blank levels to test panicked crowds ... a
large square room with walls, a maze to test following, an interaction test
level"). They sit beside the office on the start card and share every rule
and number the office plays by; only the building, the people and the things
in it are different. Nothing about them is a game: no Director, no keycard,
no timetable, no fire until asked for.

## Picking a level

The start card (the card that covers the screen before anything moves) has a
row of buttons under the level's name: **The Office**, **The Square Room**,
**The Maze**, **The Interaction Room**. Press one and the scene reloads into
that level, behind its own start card, on its own seed. **Reset** and **Play
again** keep the level you are in; the seed box belongs to the level on
screen. Each level keeps its own best score.

The row is drawn from the runner's list of level assets
(`Assets/Paniq/Content/Levels/`), so a new level is a new asset added to that
list, not new code (see [adding a level](development-workflow.md#adding-a-level)).

## The crowd switch

On the three test levels a button sits bottom centre, where the red Trigger
event button sits on the office: **Crowd: calm** or **Crowd: panicked**,
reading what the crowd is being held at and flicking it the other way.

- **Crowd: panicked.** Everybody in the building takes fright, each a few
  ticks after the next and never two on one tick (the same startle a fire
  alarm bell gives, with nothing to see and nothing to hear, so nobody turns
  toward anything). The runners run for a way out, the freezers freeze, the
  brave go for the pull station, and so on, exactly as they would for a
  fire. The switch *holds*: nobody settles while it stands at panicked, and
  anybody found calm again (up off the floor after a fall, say) is startled
  again on a lag of their own. Pressing it also begins the round, so a room
  that empties ends with a score card.
- **Crowd: calm.** Everybody frightened settles, one at a time on a tick of
  their own, as soon as they are free to (somebody forcing a door, on the
  floor or alight finishes that first). The bells fall silent. From then on
  the ordinary rules decide who takes fright: a fire lit afterwards frightens
  people as it always did. Calm is not a hold.

Both are player commands, recorded like every click, so a run with the switch
in it replays. The story on the end card says "you set the whole crowd
panicking" and "you calmed the whole crowd down".

## The Square Room

One room, 24 m by 24 m, with a shut, unlocked door in the middle of every
wall, a bell on every wall and a pull station beside the north door. Forty
people stand in a block in the middle, a metre apart, and the seed deals
their personalities, so a new seed is a new crowd. No furniture and no fire:
there is no Trigger event button here. Panicked, they have four ways out to
choose between; right-click a door to lock it (the purse opens full) and
watch the crush at the ones left.

## The Maze

Thirty-six cells of 4 m by 4 m, joined by 2 m archways into a maze with one
1 m way out on its south-east edge, drawn from a picture in the code
(`TestBuildings.MazePicture`) so it can be redrawn without arithmetic. The
walk from the far corner to the way out is sixteen cells, past seven dead
ends. One person works here and knows every turn, and is the strongest
leader there is, so a rally gathers the room; ten are visitors who know only
the cell they stand in. Two green signs in the last two cells point at the
way out; four bells are spread through the maze so the alarm reaches every
cell. Watch the star over the staff member and who trails after it, and
watch the strangers search. No Trigger event button.

## The Interaction Room

A 16 m by 12 m room with one of everything to bump into, pick up, sit on,
open or set alight: light and heavy boxes, a wooden chair and an office chair
at a desk with a laptop on it, a bag, a bin, a plant that never burns, a
standing lamp, a fire extinguisher, a pull station and two bells. Off it: a
lobby to the north through an archway, with the unlocked way out in its far
wall; a side room to the east through a pair of swing doors; and a closet in
the east wall behind an ordinary shut door. A second way out in the south
wall starts locked. Eight people with one dial each turned up (the brute, the
saint, the villain, the leader, the nervous wreck, the hero, the sprinter) and
one visitor who does not know the way. **Trigger event** lights a fire in the
middle of the floor, between the people and the desk; the crowd switch works
too, and a fire lit after the crowd was calmed frightens them afresh.

## What is deliberately not here

- No "left alone" line on the end card: a level nothing sets off has no
  disaster to leave alone. The Interaction Room keeps it, because its trigger
  lights a fire.
- The maze is fixed, not drawn from the seed: the same maze every time, so a
  note about a corner means the same corner next week.
- No level made with the scene baker is on the row yet; the baker still writes
  only the office's scenario asset.
