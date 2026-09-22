# Design research

This document preserves concise, evidence-informed guidance for Paniq's future
design work. These sources are lenses for making and testing decisions, not a
universal formula for fun or a substitute for playtesting.

## Current guidance

| Source | Lesson | Paniq implication |
| --- | --- | --- |
| [MDA: A Formal Approach to Game Design and Game Research](https://users.cs.northwestern.edu/~hunicke/MDA.pdf) (Hunicke, LeBlanc, and Zubek, 2004) | Designers can reason from intended aesthetic experience to system dynamics and then mechanics. | Start each prototype stone with the experience Paniq should create, then choose the smallest crowd rules and interventions that can create it. |
| [GameFlow: A Model for Evaluating Player Enjoyment in Games](https://doi.org/10.1145/1077246.1077253) (Sweetser and Wyeth, 2005) | Enjoyment evaluation should consider clear goals, feedback, control, skills, challenge, and immersion. | Each prototype needs an understandable rescue goal, visible state changes, useful feedback after an intervention, and challenge that does not overwhelm a new player. |
| [The Open, the Closed and the Emergent](https://gamestudies.org/1902/articles/soleradillon) (Soler-Adillon, 2019) | Emergent play comes from interacting rules and player decisions; meaningful play requires players to perceive the relationship between actions and outcomes. | Create varied crowd cascades from a small, visible rule set. Make cause and effect observable so surprise becomes learning instead of arbitrary randomness. |
| [Methods for Game User Research](https://ieeexplore.ieee.org/document/6562711/) (Nacke, Drachen, and Göbel, 2013) | Game user research can combine observation with methods such as think-aloud sessions and heuristics to improve design. | When Paniq has a playable prototype, observe players and ask them to explain what they think happened. Use that evidence to find unclear rules and misleading feedback. |
| [The AI Systems of Left 4 Dead](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf) (Booth, Valve, AIIDE 2009) | A background "AI Director" tailors each playthrough by pacing pressure rather than scripting it, so no two runs of the same level play alike. | Paniq's Director is the intended cure for contagion that either fizzles out or runs away. It is accepted that a reactive Director makes runs incomparable between attempts; see [game vision](game-vision.md). Not yet read in full from this environment — confirm the pacing details against the source before tuning. |

## How to use this research

Before a material design recommendation:

1. Describe the player experience or problem in plain language.
2. Read the relevant source material and record a short Paniq-specific lesson
   here when it will guide future work.
3. State whether the result is a decided direction, a hypothesis, or a prototype
   question.
4. Validate hypotheses with the smallest playable test and player observation.

The current core direction is documented in [game-vision.md](game-vision.md).
