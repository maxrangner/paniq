using System;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// What is on screen while the round runs. At the top, one strip the
    /// player reads at a glance: saved, lost, still inside, and the seed
    /// (2026-09-30, the owner: "remove all but saved lost still inside and
    /// seed"). At the bottom left, the hand: what a press under the pointer
    /// would do, and the bar the hand's charge drains and refills. Tab
    /// toggles a plain table of everyone's traits and state.
    /// <para>
    /// Everything that only explains how to play -- which keys do what, what
    /// the marks over people's heads mean -- lives in <see cref="DrawPauseHelp"/>
    /// and appears only when the world is stopped, which is when somebody
    /// actually wants to read it. The cards along the bottom are gone with
    /// the cards (2026-09-30).
    /// </para>
    /// </summary>
    internal static class PrototypeHud
    {
        /// <summary>The red band across the very top while the bells ring.</summary>
        private static readonly Color BannerRed = new Color(0.8f, 0.08f, 0.06f);

        /// <summary>The darker band when the building turns on the crowd (2026-09-29): the Director's push, shown for a few seconds.</summary>
        private static readonly Color BannerPush = new Color(0.45f, 0.05f, 0.35f);
        private const int PushBannerTicks = 200;
        private const float BannerHeight = 28f;

        private static readonly Color StripBack = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>The hand's bar: gold while it has charge, dull red while it rests.</summary>
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarCharged = new Color(1f, 0.82f, 0.3f, 0.95f);
        private static readonly Color BarResting = new Color(0.75f, 0.25f, 0.2f, 0.95f);
        private const float BarWidth = 260f;
        private const float BarHeight = 12f;

        // IMGUI styles are made from the skin, which only exists while a GUI
        // event is being handled, so they are built on first use and kept.
        private static GUIStyle bannerStyle;
        private static GUIStyle stripStyle;

        private static GUIStyle BannerStyle => bannerStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = 16
        };

        /// <summary>The strip's numbers: bold and a size up from the labels, readable at a glance without being huge.</summary>
        private static GUIStyle StripStyle => stripStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, fontSize = 15
        };

        /// <summary>Where the line about what is under the pointer sits: bottom left, above the hand's bar.</summary>
        private static Rect HintLine => new Rect(20f, Screen.height - 66f, 900f, 22f);

        /// <summary>
        /// The top of the screen: a red FIRE ALARM band across the very top
        /// while the bells ring (or the Director's band over it), then one
        /// strip: saved, lost, still inside, the seed. The band's 28 pixels
        /// are always kept, so nothing jumps when the bells start. What a
        /// press under the pointer would do is written at the bottom left,
        /// beside the hand it belongs to.
        /// </summary>
        public static void Draw(
            RunSnapshot snapshot,
            ScenarioData scenario,
            ulong seed,
            DoorSnapshot? hoveredDoor,
            SimulationId? hoveredAlarm,
            PlayerInput input)
        {
            GUI.color = Color.white;
            bool pushing = snapshot.DirectorPushTick >= 0 && snapshot.Tick - snapshot.DirectorPushTick < PushBannerTicks;
            if (pushing)
            {
                // The building turns on the crowd (2026-09-29): the Director's
                // push is announced like the alarm, so a socket popping right
                // after the door opens reads as the building's move and not
                // as bad luck. It takes the band over the bells for its few
                // seconds.
                var band = new Rect(0f, 0f, Screen.width, BannerHeight);
                GUI.color = BannerPush;
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(band, "THE BUILDING TURNS ON THE CROWD", BannerStyle);
            }
            else if (snapshot.AlarmsRinging)
            {
                // Two beats a second, like the bells.
                float pulse = Mathf.Repeat(Time.unscaledTime * 4f, 2f) < 1f ? 1f : 0.75f;
                var band = new Rect(0f, 0f, Screen.width, BannerHeight);
                GUI.color = new Color(BannerRed.r, BannerRed.g, BannerRed.b, pulse);
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(band, "FIRE ALARM", BannerStyle);
            }

            // The round's numbers, on their dark backing (2026-09-30: only
            // these four; the tick, the fire, the calm and scared counts, the
            // bar to clear and the left-alone line are gone from here -- the
            // end card and the Tab panel keep the last two).
            var strip = new Rect(20f, 36f, 620f, 28f);
            GUI.color = StripBack;
            GUI.DrawTexture(strip, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(strip.x + 10f, strip.y, strip.width - 20f, strip.height),
                $"Saved {snapshot.SavedCount}     Lost {snapshot.LostCount}     Still inside {snapshot.RemainingCount}          Seed {seed}",
                StripStyle);

            if (hoveredDoor.HasValue)
            {
                // A door with something wedged in it will not move, so say so
                // rather than letting a press look as though it did nothing.
                // Since 2026-09-30 the left button on a door draws people to
                // use it and the right pushes them away from it.
                DoorSnapshot door = hoveredDoor.Value;
                string pull = IsTheHandOn(snapshot, door.DoorId, true)
                    ? "your hand is on it"
                    : "hold to draw people to it (they open it if shut, shut it if open), right button to push them away; click for three seconds";
                string action = door.Swings ? $"Swing doors: people push straight through. {Capital(pull)}"
                    : door.IsPiled ? "THE BOXES ARE LYING ACROSS IT - nobody gets through until enough of them are gone; hold it and they clear the boxes"
                    : door.State == DoorState.Broken ? $"Broken down. {Capital(pull)}"
                    : door.IsJammed ? "SOMETHING IS WEDGED IN IT - it will not open until that is shifted"
                    : door.NeedsKeycard ? $"NEEDS THE KEYCARD - {WhereTheKeycardIs(snapshot)}. Hold it and they pound on it until it gives, and somebody who knows where the card is goes for it. {Capital(pull)}"
                    : door.State == DoorState.Locked ? $"Locked. Hold it and they throw themselves at it, weak or strong. {Capital(pull)}"
                    : Capital(pull);
                GUI.color = door.IsJammed || door.IsPiled ? new Color(1f, 0.7f, 0.6f)
                    : door.NeedsKeycard ? new Color(1f, 0.9f, 0.5f) : Color.white;
                GUI.Label(HintLine, $"Door {door.DoorId.Value}: {action}");
                GUI.color = Color.white;
            }
            else if (hoveredAlarm.HasValue)
            {
                // A fire alarm is refused for nothing once the bells are
                // ringing; say which before the click. On a level where only
                // people pull them (the office, 2026-09-26), the hand on it
                // draws people to it -- or, with the right button, away.
                int price = snapshot.CostOf(PlayerCommandType.PullAlarm);
                bool affordable = !snapshot.PlayerMayPullAlarms || snapshot.Purse >= price;
                string action = snapshot.AlarmsRinging ? "already ringing"
                    : !snapshot.PlayerMayPullAlarms ? "only the people in the building pull it. Hold to draw people to it: whoever comes pulls it"
                    : !affordable ? $"NOT ENOUGH IN THE PURSE - it costs {price}, and you have {snapshot.Purse}"
                    : $"Click to pull it{Price(snapshot, price)}: every bell in the building rings";
                GUI.color = affordable || snapshot.AlarmsRinging ? Color.white : new Color(1f, 0.7f, 0.6f);
                GUI.Label(HintLine, $"Fire alarm {hoveredAlarm.Value.Value}: {action}");
                GUI.color = Color.white;
            }
            else if (input.TuggedPerson.HasValue && IsTuggedInTheRun(snapshot, input.TuggedPerson.Value))
            {
                GUI.color = new Color(1f, 0.93f, 0.62f);
                GUI.Label(HintLine,
                    $"You have person {input.TuggedPerson.Value.Value} by the shirt. Let go of the button to let go of them");
                GUI.color = Color.white;
            }
            else if (input.HoveredPerson.HasValue)
            {
                GUI.Label(HintLine, PersonLine(snapshot, input.HoveredPerson.Value));
            }
            else if (input.HoveredThing.HasValue)
            {
                GUI.Label(HintLine, IsTheHandOn(snapshot, input.HoveredThing.Value, false)
                    ? "Your hand is on it"
                    : "Hold to draw people to it: a chair is sat on, a box carried off, fallen boxes cleared, the bottle taken and used, the card pocketed. Right button pushes them away; drag to move the hand");
            }
            else if (input.HoveredFloor.HasValue)
            {
                GUI.Label(HintLine, snapshot.InfluencePlaces.Count > 0
                    ? snapshot.InfluencePlaces[0].Repels
                        ? "Your hand is pushing people away from here: drag it to herd them. Let go and they are on their own"
                        : "Your hand is on the floor here: people nearby come to it, and the sure keep at it after you let go. Drag it to lead them"
                    : snapshot.HandResting
                        ? "Your hand is resting: the bar has to fill a little before it takes another press"
                        : "Hold to draw people here (drag to lead them), right button to push them away; a click leaves it for three seconds");
            }
        }

        /// <summary>
        /// The hand's charge (2026-09-30): a bar at the bottom left that
        /// drains while the hand is on a place or a person and refills by
        /// itself, gold while there is charge and dull red while it rests. It
        /// claims its patch of screen so a press on it stays off the world.
        /// </summary>
        public static void DrawHand(RunSnapshot snapshot)
        {
            var bar = new Rect(20f, Screen.height - 40f, BarWidth, BarHeight);
            HudHitTest.Claim(new Rect(bar.x, bar.y - 4f, bar.width + 120f, bar.height + 8f));
            GUI.color = BarBack;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float fraction = Mathf.Clamp01(snapshot.HandChargePerMille / 1000f);
            GUI.color = snapshot.HandResting ? BarResting : BarCharged;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x + bar.width + 10f, bar.y - 5f, 200f, 22f),
                snapshot.HandResting ? "your hand, resting" : "your hand");
        }

        /// <summary>
        /// What the hand does on this person (2026-09-29): a poke, a tug, or
        /// what a poke does to somebody frozen, and what would help somebody
        /// out cold. Says it in words; nothing points at them.
        /// </summary>
        private static string PersonLine(RunSnapshot snapshot, SimulationId person)
        {
            string card = HasTheKeycard(snapshot, person) ? " - HAS THE KEYCARD" : "";
            string who = $"Person {person.Value}{card}";
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                if (agent.AgentId != person)
                {
                    continue;
                }

                if (agent.IsBurning)
                {
                    return $"{who}: on fire - nothing you can hold";
                }

                if (agent.BodyState == AgentBodyState.Unconscious)
                {
                    return $"{who}: out cold - hold the floor beside them to draw somebody who could drag them";
                }

                if (agent.ActivityState == AgentActivityState.Frozen)
                {
                    return $"{who}: frozen with fear - three quick pokes wake them";
                }

                string strength = agent.Traits.Strength >= 9 ? " (too strong to hold for long)"
                    : agent.Traits.Strength >= 6 ? " (strong: they will tear free in a while)" : "";
                string doing = agent.CommittedToTheHand ? " Keeping at what your hand asked."
                    : agent.ActingForTheHand ? " Doing what your hand asks." : "";
                return agent.IsAnnoyed
                    ? $"{who}: annoyed with you - a poke does nothing for a while; hold to hold them here{strength}; right button pushes the people round them away.{doing}"
                    : $"{who}: click to poke them away from the click; hold to hold them here{strength}; right button pushes the people round them away.{doing}";
            }

            return who;
        }

        /// <summary>Whether the player's hand is on this door or thing right now, for the hover line.</summary>
        private static bool IsTheHandOn(RunSnapshot snapshot, SimulationId target, bool isDoor)
        {
            for (int i = 0; i < snapshot.InfluencePlaces.Count; i++)
            {
                InfluencePlaceSnapshot place = snapshot.InfluencePlaces[i];
                if (place.IsDoor == isDoor && place.Target == target)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the run itself has the hand on this person: the line says so only once the tug has landed, and stops when they tear free.</summary>
        private static bool IsTuggedInTheRun(RunSnapshot snapshot, SimulationId person)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                if (snapshot.Agents[i].AgentId == person)
                {
                    return snapshot.Agents[i].IsTugged;
                }
            }

            return false;
        }

        /// <summary>Whether the keycard is in this person's pocket: the card is a thing held by them.</summary>
        private static bool HasTheKeycard(RunSnapshot snapshot, SimulationId person)
        {
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                if (thing.Kind == PhysicsObjectKind.Keycard && thing.HeldBy == person)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Who has the keycard, or that it lies free, for the way out's hover
        /// line (2026-09-30: on seed 42 the host burned with the card in his
        /// pocket while four people pounded the door, and nothing on screen
        /// said who had it).
        /// </summary>
        private static string WhereTheKeycardIs(RunSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                if (thing.Kind != PhysicsObjectKind.Keycard || thing.Dormant)
                {
                    continue;
                }

                return thing.IsHeld ? $"person {thing.HeldBy.Value} has the card" : "the card lies free somewhere";
            }

            return "there is no card";
        }

        private static string Capital(string words) =>
            string.IsNullOrEmpty(words) ? words : char.ToUpperInvariant(words[0]) + words.Substring(1);

        /// <summary>A price in brackets, or nothing at all on a level with no purse (prototype 3).</summary>
        private static string Price(RunSnapshot snapshot, int price) => snapshot.PurseEnabled ? $" ({price})" : "";

        /// <summary>
        /// Everything that explains how to play, shown only while the world is
        /// stopped: what the marks over people's heads mean, and what every key
        /// and click does.
        /// <para>
        /// All of this used to be on screen the whole time -- two rows of key
        /// reminders across the top, a line under the cards, and the marks
        /// panel down the left -- which left very little of the office to look
        /// at. Pause is the moment somebody is reading rather than playing, so
        /// this is where it belongs.
        /// </para>
        /// </summary>
        public static void DrawPauseHelp(RunSnapshot snapshot)
        {
            const float rowHeight = 18f;
            const float width = 620f;
            var marks = new[]
            {
                (Colour: new Color(1f, 0.25f, 0.2f), Mark: "!", Means: "just noticed something"),
                (Colour: new Color(0.45f, 0.9f, 1f), Mark: ")))", Means: "shouting"),
                (Colour: new Color(1f, 0.85f, 0.3f), Mark: "?", Means: "what was that noise?"),
                (Colour: new Color(1f, 0.5f, 0.15f), Mark: "#!", Means: "annoyed at being nudged"),
                (Colour: new Color(0.8f, 0.8f, 0.8f), Mark: "...", Means: "idling"),
                (Colour: new Color(0.7f, 0.85f, 1f), Mark: "*", Means: "frozen with fear"),
                (Colour: new Color(1f, 0.9f, 0.35f), Mark: "o o o", Means: "out cold"),
                (Colour: new Color(0.4f, 0.95f, 0.5f), Mark: "star", Means: "somebody is following them"),
                (Colour: new Color(1f, 0.82f, 0.3f), Mark: "hand", Means: "doing what your hand asked; still, keeping at it after you let go"),
                (Colour: new Color(1f, 0.9f, 0.2f), Mark: "card", Means: "has the keycard"),
                (Colour: new Color(0.85f, 0.6f, 1f), Mark: "band", Means: "at the ankles: keeping together with the others wearing it"),
                (Colour: new Color(1f, 0.55f, 0.15f), Mark: "[]", Means: "on fire"),
                (Colour: new Color(0.55f, 0.15f, 0.15f), Mark: "[]", Means: "lost")
            };

            // Since 2026-09-30 the mouse has no key and no hand holding a door
            // shut: the right button pushes people away from it.
            const string doorHelp =
                "hold the left button to draw people to use it (a locked one they pound on), the right button to push them away from it. " +
                "Red is locked. The way out needs the keycard: whoever has it swipes it open; under your hand it gives to a long pounding";
            var keys = new[]
            {
                ("Hold the floor", "your hand on a place: people nearby come to it while you hold, the frightened too, one place at a time. Things too"),
                ("Let go", "whoever is sure of it keeps at it -- the nervous longest, leaders least -- and the rest are on their own at once"),
                ("Hold and drag", "the hand moves with the pointer and the people answering it follow; on fallen boxes, they clear them"),
                ("Click the floor", "the same, left there for three seconds"),
                ("Right button", "the same the other way round: people are pushed away from the place"),
                ("The bar", "bottom left: the hand's charge. Holding drains it, it refills by itself; empty, the hand comes off until it has rested"),
                ("A door", doorHelp),
                ("Click a person", "poke them away from the click. Three quick ones and they are annoyed, and shake"),
                ("Hold a person", "a tug on their shirt: they stop at once and stay while you hold, struggling. The strong tear free, sooner the stronger"),
                ("W A S D", "move the camera"),
                ("Q E", "turn an eighth: corner, side, corner"),
                ("Wheel", "zoom"),
                ("Tab", "what to show: vision cones, numbers, marks, everyone's stats, the walkable floor; and the hand's strength and reach sliders"),
                ("G", "the floor people can walk on"),
                ("Space", "start and stop the world (or the Pause button, top right)"),
                ("Reset", "the button top right: back to the start card, keeping the seed"),
                ("Trigger event", "the red button bottom centre starts the fire, once, and goes"),
                ("Crowd", "on a test level, the button beside it: sets the whole crowd panicking, or calms it down again")
            };

            int rows = Math.Max(marks.Length, keys.Length);
            float height = rowHeight * (rows + 2) + 16f;
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float left = area.x + 12f;
            float right = area.x + 270f;
            float top = area.y + 8f;
            GUI.Label(new Rect(left, top, 240f, rowHeight), "WHAT THE MARKS MEAN");
            GUI.Label(new Rect(right, top, 340f, rowHeight), "WHAT THE KEYS DO");

            float y = top + rowHeight * 1.5f;
            for (int i = 0; i < rows; i++)
            {
                if (i < marks.Length)
                {
                    GUI.color = marks[i].Colour;
                    GUI.Label(new Rect(left, y, 46f, rowHeight), marks[i].Mark);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(left + 50f, y, 200f, rowHeight), marks[i].Means);
                }

                if (i < keys.Length)
                {
                    GUI.Label(new Rect(right, y, 90f, rowHeight), keys[i].Item1);
                    GUI.Label(new Rect(right + 96f, y, width - 108f - 270f + 260f, rowHeight), keys[i].Item2);
                }

                y += rowHeight;
            }
        }

        /// <summary>
        /// One row per person, numbered like the labels over their heads, then
        /// <paramref name="footer"/>: which physics feel is in use, what the
        /// round needs, what it comes to left alone, and so on. Its top edge
        /// is <paramref name="top"/>, so it can sit under the Tab panel when
        /// that is open.
        /// </summary>
        public static void DrawStats(RunSnapshot snapshot, string footer, float top)
        {
            const float rowHeight = 20f;
            float width = 640f;
            float height = rowHeight * (snapshot.Agents.Count + 3) + 12f;
            var area = new Rect(Screen.width - width - 20f, top, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = area.x + 10f;
            float y = area.y + 6f;
            string[] headings = { "#", "Str", "Spd", "Brv", "Cmp", "Evl", "Nrv", "Ldr", "Panics by", "Now" };
            DrawRow(x, y, rowHeight, headings);
            y += rowHeight;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                AgentTraitValues t = agent.Traits;
                string card = HasTheKeycard(snapshot, agent.AgentId) ? " (has the keycard)" : "";
                DrawRow(x, y, rowHeight, new[]
                {
                    (i + 1).ToString(), t.Strength.ToString(), t.Speed.ToString(), t.Bravery.ToString(),
                    t.Compassion.ToString(), t.Evil.ToString(), t.Nervousness.ToString(), t.Leadership.ToString(),
                    TemperamentText(agent.Temperament), StateText(agent) + card
                });
                y += rowHeight;
            }

            GUI.Label(new Rect(x, y, width, rowHeight), "Traits run 0-10; 5 is an ordinary person.");
            GUI.Label(new Rect(x, y + rowHeight, width - 20f, rowHeight), footer);
        }

        private static readonly float[] ColumnX = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 330f, 440f };

        private static void DrawRow(float x, float y, float height, string[] cells)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                float right = c + 1 < ColumnX.Length ? ColumnX[c + 1] : ColumnX[c] + 180f;
                GUI.Label(new Rect(x + ColumnX[c], y, right - ColumnX[c], height), cells[c]);
            }
        }

        private static string TemperamentText(AgentPanicTemperament temperament)
        {
            switch (temperament)
            {
                case AgentPanicTemperament.FreezeForever: return "freezing";
                case AgentPanicTemperament.FreezeThenRun: return "freeze, run";
                default: return "running";
            }
        }

        private static string StateText(AgentSnapshot agent)
        {
            if (agent.Outcome == AgentTerminalOutcome.Lost)
            {
                return "lost";
            }

            if (agent.Outcome == AgentTerminalOutcome.Escaped)
            {
                return "escaped";
            }

            if (agent.IsBurning)
            {
                return agent.BodyState == AgentBodyState.Upright ? "on fire!" : "burning on the floor";
            }

            if (agent.BodyState == AgentBodyState.Unconscious)
            {
                return "out cold";
            }

            if (agent.BodyState != AgentBodyState.Upright)
            {
                return agent.BodyState == AgentBodyState.Staggering ? "staggering" : "down";
            }

            if (agent.FearState == AgentFearState.Calm)
            {
                return "calm";
            }

            if (agent.FearState == AgentFearState.Alert)
            {
                return "startled";
            }

            switch (agent.ActivityState)
            {
                case AgentActivityState.FetchingItem: return "going for an item";
                case AgentActivityState.PickingUp: return "picking it up";
                case AgentActivityState.CarryingItem: return "carrying";
                case AgentActivityState.SettingDown: return "putting it down";
                case AgentActivityState.TryingDoor: return "trying a door";
                case AgentActivityState.ForcingDoor: return "shoving a door";
                case AgentActivityState.OpeningDoor: return "opening a door";
                case AgentActivityState.ShakingAwake: return "shaking someone awake";
                case AgentActivityState.Grabbing: return "grabbing someone";
                case AgentActivityState.Dragging: return "dragging someone";
                case AgentActivityState.Following: return "following someone";
                case AgentActivityState.FetchingExtinguisher: return "going for an extinguisher";
                case AgentActivityState.Spraying: return "spraying the fire";
                case AgentActivityState.GoingToSit: return "going to sit down";
                case AgentActivityState.Sitting: return "sitting";
                case AgentActivityState.StandingUp: return "getting up";
                case AgentActivityState.GoingToAlarm: return "going for the alarm";
                case AgentActivityState.PullingAlarm: return "hitting the alarm";
                case AgentActivityState.FetchingKeycard: return "going for the keycard";
                case AgentActivityState.HeavingForTheHand: return "heaving a box for you";
                case AgentActivityState.AnsweringTheHand: return "answering your hand";
                case AgentActivityState.Fleeing: return "running";
                default: return agent.ActivityState.ToString().ToLowerInvariant();
            }
        }
    }
}
