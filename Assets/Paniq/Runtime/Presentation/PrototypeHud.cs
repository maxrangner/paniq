using System;
using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The text at the top left: tick, fire, the head count, and what a door
    /// click will do and cost. Along the bottom, the purse the player has
    /// left and the cards they can spend it on. Tab toggles a plain table of
    /// everyone's traits and state.
    /// <para>
    /// What is on screen while the round runs is what the player is reading:
    /// the numbers, the buttons and the purse. Everything that only explains
    /// how to play -- which keys do what, what the marks over people's heads
    /// mean -- lives in <see cref="DrawPauseHelp"/> and appears only when the
    /// world is stopped, which is when somebody actually wants to read it.
    /// </para>
    /// </summary>
    internal static class PrototypeHud
    {
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFill = new Color(0.3f, 0.75f, 1f, 0.9f);
        private static readonly Color CardPicked = new Color(0.25f, 0.55f, 0.85f, 0.95f);
        private static readonly Color CardAffordable = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color CardTooDear = new Color(0.25f, 0.1f, 0.1f, 0.7f);
        private static readonly Color CardEdge = new Color(0.85f, 0.82f, 0.7f, 0.9f);
        private static readonly Color CardTooDearEdge = new Color(0.6f, 0.35f, 0.3f, 0.9f);
        private static readonly Color CardFace = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        private static readonly Color CardTooDearFace = new Color(0.2f, 0.1f, 0.1f, 0.9f);
        private static readonly Color Badge = new Color(0.95f, 0.9f, 0.7f, 1f);

        /// <summary>
        /// The hand's card size, in pixels: a portrait card, a shade under a
        /// 2:3 playing card, small enough that six of them sit under the
        /// strip on a laptop screen. They were 210 × 34 bars of text, which
        /// the owner asked to look like cards and take less room.
        /// </summary>
        private const float CardWidth = 96f;
        private const float CardHeight = 132f;
        private const float CardGap = 8f;
        private const float CardLift = 10f;

        // IMGUI styles are made from the skin, which only exists while a GUI
        // event is being handled, so they are built on first use and kept.
        private static GUIStyle cardNameStyle;
        private static GUIStyle cardBlurbStyle;
        private static GUIStyle cardCostStyle;
        private static GUIStyle badgeStyle;

        private static GUIStyle CardNameStyle => cardNameStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 13
        };

        private static GUIStyle CardBlurbStyle => cardBlurbStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter, wordWrap = true, fontSize = 10
        };

        private static GUIStyle CardCostStyle => cardCostStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = 14
        };

        private static GUIStyle BadgeStyle => badgeStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = 12
        };

        /// <summary>One line under the card's name saying what it does, for a hand read at a glance.</summary>
        private static string BlurbOf(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return "strength 10 for everyone caught";
                case PlayerCommandType.PlayCourage: return "fearless, everyone caught";
                case PlayerCommandType.PlayTerror: return "the fear of God, everyone caught";
                case PlayerCommandType.PlayBastard: return "turned nasty, everyone caught";
                case PlayerCommandType.PlayColdHeart: return "cares for nobody, everyone caught";
                case PlayerCommandType.SpawnFire: return "a fire where you click";
                case PlayerCommandType.SpawnExtinguisher: return "a bottle where you click";
                case PlayerCommandType.BlastWall: return "a hole through a wall";
                case PlayerCommandType.PopFuseBox: return "the fuse box goes off";
                case PlayerCommandType.StickTogether: return "everyone caught keeps together";
                default: return string.Empty;
            }
        }

        /// <summary>The red band across the very top while the bells ring.</summary>
        private static readonly Color BannerRed = new Color(0.8f, 0.08f, 0.06f);
        private const float BannerHeight = 28f;

        private static GUIStyle bannerStyle;

        private static GUIStyle BannerStyle => bannerStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = 16
        };

        /// <summary>
        /// The top of the screen (packed toward it since 2026-09-25, the owner
        /// asked): a red FIRE ALARM band across the very top while the bells
        /// ring, then four lines at the top left -- the tick and the fire, the
        /// head count, the round's score, and what a click on the door or the
        /// pull station under the pointer would do and cost. The band's 28
        /// pixels are always kept, so nothing jumps when the bells start.
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
            if (snapshot.AlarmsRinging)
            {
                // Two beats a second, like the bells.
                float pulse = Mathf.Repeat(Time.unscaledTime * 4f, 2f) < 1f ? 1f : 0.75f;
                var band = new Rect(0f, 0f, Screen.width, BannerHeight);
                GUI.color = new Color(BannerRed.r, BannerRed.g, BannerRed.b, pulse);
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(band, "FIRE ALARM", BannerStyle);
            }

            string fireText;
            if (snapshot.FireActive)
            {
                fireText = $"FIRE  {snapshot.FireCells.Count} squares burning";
            }
            else if (scenario.Round.HazardWaitsForTrigger)
            {
                // Nothing is counting down: it waits for the player.
                fireText = snapshot.EventTriggered ? "FIRE STARTING" : "NO FIRE YET";
            }
            else
            {
                fireText = $"FIRE IN {Mathf.Max(0f, (scenario.Fire.ActivationTick - snapshot.Tick) / (float)Run.TicksPerSecond):0.00} s";
            }

            GUI.Label(new Rect(20f, 36f, 700f, 22f), $"Fire-reaction prototype  |  tick {snapshot.Tick}  |  {fireText}");
            GUI.Label(new Rect(20f, 58f, 900f, 22f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount}, on fire {snapshot.BurningCount})   " +
                $"Down {snapshot.DownCount} (out cold {snapshot.UnconsciousCount})   Lost {snapshot.LostCount}   " +
                $"Escaped {snapshot.EscapedCount}   In a room with no fire {snapshot.ClearOfFireCount}");

            // The round's score, on its dark backing.
            var strip = new Rect(20f, 80f, 720f, 22f);
            GUI.color = StripBack;
            GUI.DrawTexture(strip, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(strip.x + 8f, strip.y, strip.width - 16f, strip.height),
                $"Saved {snapshot.SavedCount}   Lost {snapshot.LostCount}   Still inside {snapshot.RemainingCount}" +
                $"      Need {snapshot.TargetSavedCount} of {snapshot.CrowdSize} to clear      Seed {seed}");

            if (hoveredDoor.HasValue)
            {
                // A door with something wedged in it will not move, so say so
                // rather than letting a click look as though it did nothing.
                // Since 2026-09-26 a click draws people to the door (a step of
                // influence), holding keeps it shut, and the key is a right click.
                DoorSnapshot door = hoveredDoor.Value;
                int key = snapshot.CostOfLockToggle(door.State, door.LeadsOutside);
                bool locked = door.State == DoorState.Locked;
                bool affordable = !locked || snapshot.Purse >= key;
                string pull = $"click to draw people to it ({InfluenceLevel(snapshot, door.DoorId, true)}/{MaximumInfluence(snapshot)})";
                string action = door.Swings ? $"Swing doors: people push straight through. {Capital(pull)}"
                    : door.IsPiled ? "THE BOXES ARE LYING ACROSS IT - nobody gets through until enough of them are gone"
                    : door.State == DoorState.Broken ? $"Broken down. {Capital(pull)}"
                    : door.IsJammed ? "SOMETHING IS WEDGED IN IT - it will not open until that is shifted" +
                                      (door.IsHeld ? ", and you are holding it as well" : "")
                    : door.IsHeld ? "You are holding it shut. Let go of the button to let go of the door"
                    : !affordable ? $"Locked. NOT ENOUGH IN THE PURSE to unlock it - it costs {key}, and you have {snapshot.Purse}"
                    : locked ? $"Locked. Right-click to unlock{Price(snapshot, key)}; {pull}"
                    : $"{Capital(pull)}; hold to keep it shut; right-click to lock{Price(snapshot, key)}";
                GUI.color = door.IsJammed || door.IsPiled || !affordable ? new Color(1f, 0.7f, 0.6f)
                    : door.IsHeld ? new Color(0.6f, 0.8f, 1f) : Color.white;
                GUI.Label(new Rect(20f, 104f, 900f, 22f), $"Door {door.DoorId.Value}: {action}");
                GUI.color = Color.white;
            }
            else if (hoveredAlarm.HasValue)
            {
                // A fire alarm is priced like a card and refused for nothing
                // once the bells are ringing; say which before the click. On a
                // level where only people pull them (the office, 2026-09-26),
                // a click draws people to it instead.
                int price = snapshot.CostOf(PlayerCommandType.PullAlarm);
                bool affordable = !snapshot.PlayerMayPullAlarms || snapshot.Purse >= price;
                string action = snapshot.AlarmsRinging ? "already ringing"
                    : !snapshot.PlayerMayPullAlarms ? "only the people in the building pull it. Click to draw people to it"
                    : !affordable ? $"NOT ENOUGH IN THE PURSE - it costs {price}, and you have {snapshot.Purse}"
                    : $"Click to pull it{Price(snapshot, price)}: every bell in the building rings";
                GUI.color = affordable || snapshot.AlarmsRinging ? Color.white : new Color(1f, 0.7f, 0.6f);
                GUI.Label(new Rect(20f, 104f, 900f, 22f), $"Fire alarm {hoveredAlarm.Value.Value}: {action}");
                GUI.color = Color.white;
            }
            else if (input.HeldDoor.HasValue && IsHeldInTheRun(snapshot, input.HeldDoor.Value))
            {
                GUI.color = new Color(0.6f, 0.8f, 1f);
                GUI.Label(new Rect(20f, 104f, 900f, 22f),
                    $"Holding door {input.HeldDoor.Value.Value} shut. Let go of the button to let go of the door");
                GUI.color = Color.white;
            }
            else if (input.HoveredPerson.HasValue)
            {
                bool annoyed = IsAnnoyed(snapshot, input.HoveredPerson.Value);
                GUI.Label(new Rect(20f, 104f, 900f, 22f), annoyed
                    ? $"Person {input.HoveredPerson.Value.Value}: annoyed with you - nudging them does nothing for a while"
                    : $"Person {input.HoveredPerson.Value.Value}: click to nudge them away from the click");
            }
            else if (input.HoveredThing.HasValue)
            {
                GUI.Label(new Rect(20f, 104f, 900f, 22f),
                    $"Click to draw people to it ({InfluenceLevel(snapshot, input.HoveredThing.Value, false)}/{MaximumInfluence(snapshot)}): click again for a stronger pull");
            }
            else if (input.HoveredFloor.HasValue)
            {
                GUI.Label(new Rect(20f, 104f, 900f, 22f), "Click to draw people here: click again, and again, for a stronger pull");
            }
        }

        /// <summary>How many steps of influence a door or a thing has now, for the hover line.</summary>
        private static int InfluenceLevel(RunSnapshot snapshot, SimulationId target, bool isDoor)
        {
            for (int i = 0; i < snapshot.InfluencePlaces.Count; i++)
            {
                InfluencePlaceSnapshot place = snapshot.InfluencePlaces[i];
                if (place.IsDoor == isDoor && place.Target == target)
                {
                    return place.Level;
                }
            }

            return 0;
        }

        private static int MaximumInfluence(RunSnapshot snapshot) =>
            snapshot.InfluencePlaces.Count > 0 ? snapshot.InfluencePlaces[0].MaximumLevel : 20;

        private static bool IsAnnoyed(RunSnapshot snapshot, SimulationId person)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                if (snapshot.Agents[i].AgentId == person)
                {
                    return snapshot.Agents[i].IsAnnoyed;
                }
            }

            return false;
        }

        private static string Capital(string words) =>
            string.IsNullOrEmpty(words) ? words : char.ToUpperInvariant(words[0]) + words.Substring(1);

        /// <summary>
        /// Whether the run itself has this door held: the line says so only
        /// once the hold has landed, and stops the moment it ends -- let go,
        /// or burst off its hinges by somebody strong -- rather than trusting
        /// the button.
        /// </summary>
        private static bool IsHeldInTheRun(RunSnapshot snapshot, SimulationId door)
        {
            for (int i = 0; i < snapshot.Doors.Count; i++)
            {
                if (snapshot.Doors[i].DoorId == door)
                {
                    return snapshot.Doors[i].IsHeld;
                }
            }

            return false;
        }

        /// <summary>A price in brackets, or nothing at all on a level with no purse (prototype 3).</summary>
        private static string Price(RunSnapshot snapshot, int price) => snapshot.PurseEnabled ? $" ({price})" : "";

        private static readonly Color StripBack = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>Which cards are thrown at a patch of crowd rather than at a place in the building.</summary>
        private static bool IsAThrownCard(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake:
                case PlayerCommandType.PlayCourage:
                case PlayerCommandType.PlayTerror:
                case PlayerCommandType.PlayBastard:
                case PlayerCommandType.PlayColdHeart:
                case PlayerCommandType.StickTogether:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The player's purse and their cards, along the bottom: portrait
        /// cards, each with its name, a line on what it does and its price.
        /// Two of a kind sit as one card with the count in its corner
        /// (2026-09-25). A card is a button: click it to pick it up, click it
        /// again to put it down. A card they cannot afford is dimmed red; the
        /// one in their hand lifts and turns blue, and the line above says
        /// what a click will do. They used to be wide bars of text picked up
        /// with the number keys.
        /// </summary>
        public static void DrawCards(
            RunSnapshot snapshot, PlayerCommandType? selected, PlayerInput input, int peopleInTheCircle)
        {
            float bottom = Screen.height - 20f;
            int stacks = CountStacks(snapshot.Hand);
            float handWidth = Mathf.Max(300f, stacks * (CardWidth + CardGap) - CardGap);
            float cardsTop = bottom - CardHeight - CardLift;

            // The purse, above the hand and as wide as it. On a level with no
            // purse (the office since prototype 3) the space is kept and
            // nothing is drawn in it, so the hint line above it stays put.
            var barArea = new Rect(20f, cardsTop - CardGap - 16f, handWidth, 16f);
            if (snapshot.PurseEnabled)
            {
                GUI.color = BarBack;
                GUI.DrawTexture(barArea, Texture2D.whiteTexture);
                GUI.color = BarFill;
                float fraction = snapshot.PurseMaximum <= 0
                    ? 0f
                    : Mathf.Clamp01(snapshot.Purse / (float)snapshot.PurseMaximum);
                GUI.DrawTexture(new Rect(barArea.x, barArea.y, barArea.width * fraction, barArea.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(barArea.x + barArea.width + 10f, barArea.y - 3f, 400f, 22f),
                    $"Purse {snapshot.Purse} of {snapshot.PurseMaximum}   (spent {snapshot.PurseSpent}, taken in {snapshot.PurseEarned})");
            }

            // The hand. One card at the start of a round and then only what
            // the dead deal, so an empty bar is the game saying "you have
            // played what you had and nobody has died since" rather than a
            // display that has not loaded.
            if (stacks == 0)
            {
                GUI.color = new Color(0.75f, 0.75f, 0.75f);
                GUI.Label(new Rect(20f, bottom - 22f, 700f, 22f), "No cards left. The dead deal them.");
                GUI.color = Color.white;
                return;
            }

            for (int i = 0; i < stacks; i++)
            {
                PlayerCommandType card = stackKinds[i];
                int count = stackCounts[i];
                int cost = snapshot.CostOf(card);
                bool affordable = snapshot.Purse >= cost;
                bool picked = selected == card;
                var area = new Rect(20f + i * (CardWidth + CardGap), bottom - CardHeight - (picked ? CardLift : 0f),
                    CardWidth, CardHeight);

                // The card is a button. It claims its patch of screen so the
                // click that picks it up never also lands on the floor behind.
                HudHitTest.Claim(area);
                if (GUI.Button(area, GUIContent.none, GUIStyle.none))
                {
                    input.Toggle(card);
                }

                // Edge and face.
                GUI.color = picked ? CardPicked : affordable ? CardEdge : CardTooDearEdge;
                GUI.DrawTexture(area, Texture2D.whiteTexture);
                GUI.color = affordable ? CardFace : CardTooDearFace;
                GUI.DrawTexture(new Rect(area.x + 2f, area.y + 2f, area.width - 4f, area.height - 4f), Texture2D.whiteTexture);

                // Two or more of a kind: the count, in a badge top left.
                if (count > 1)
                {
                    var badge = new Rect(area.x + 6f, area.y + 6f, 30f, 22f);
                    GUI.color = picked ? CardPicked : Badge;
                    GUI.DrawTexture(badge, Texture2D.whiteTexture);
                    GUI.color = picked ? Color.white : Color.black;
                    GUI.Label(badge, $"×{count}", BadgeStyle);
                }

                // Name, what it does, and the price.
                Color ink = affordable ? Color.white : new Color(1f, 0.7f, 0.7f, 0.9f);
                GUI.color = ink;
                GUI.Label(new Rect(area.x + 6f, area.y + 32f, area.width - 12f, 44f), PlayerInput.NameOf(card), CardNameStyle);
                GUI.color = affordable ? new Color(0.85f, 0.85f, 0.85f) : ink;
                GUI.Label(new Rect(area.x + 6f, area.y + 76f, area.width - 12f, 32f), BlurbOf(card), CardBlurbStyle);
                GUI.color = ink;
                if (snapshot.PurseEnabled)
                {
                    GUI.Label(new Rect(area.x, area.yMax - 24f, area.width, 20f), affordable ? cost.ToString() : $"{cost} (you have {snapshot.Purse})",
                        affordable ? CardCostStyle : CardBlurbStyle);
                }
            }

            // Only while a card is actually in hand: what it is waiting to be
            // aimed at, and what it is pointing at right now. With nothing
            // picked up there is nothing to say, and the line that used to sit
            // here explaining the number keys has moved to the pause screen.
            GUI.color = Color.white;
            if (selected == null)
            {
                return;
            }

            // A trait card is thrown at a patch and catches whoever is inside
            // it, so what the player needs to know is how many that is right
            // now. The circle on the floor says the same thing; this says it in
            // words, and says nought out loud, because a throw that catches
            // nobody is the one mistake that is free.
            string hint;
            if (IsAThrownCard(selected.Value))
            {
                int caught = peopleInTheCircle;
                hint = $"{PlayerInput.NameOf(selected.Value)}: " + (caught == 0
                    ? "nobody in the circle -- a throw that catches nobody is free"
                    : caught == 1 ? "1 person in the circle" : $"{caught} people in the circle");
            }
            else if (selected.Value == PlayerCommandType.BlastWall)
            {
                hint = $"TNT: click a wall  ({snapshot.BlastChargesRemaining} left)";
            }
            else
            {
                hint = $"{PlayerInput.NameOf(selected.Value)}: click a spot on the floor";
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(20f, barArea.y - 26f, 900f, 22f), hint + "   (right click or Escape puts it down)");
        }

        /// <summary>The kinds in hand in the order they were first dealt, and how many of each: the stacks the hand is drawn as.</summary>
        private static readonly List<PlayerCommandType> stackKinds = new List<PlayerCommandType>();
        private static readonly List<int> stackCounts = new List<int>();

        private static int CountStacks(IReadOnlyList<PlayerCommandType> hand)
        {
            stackKinds.Clear();
            stackCounts.Clear();
            for (int i = 0; i < hand.Count; i++)
            {
                int at = stackKinds.IndexOf(hand[i]);
                if (at < 0)
                {
                    stackKinds.Add(hand[i]);
                    stackCounts.Add(1);
                }
                else
                {
                    stackCounts[at]++;
                }
            }

            return stackKinds.Count;
        }

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
                (Colour: new Color(0.85f, 0.6f, 1f), Mark: "band", Means: "at the ankles: keeping together with the others wearing it"),
                (Colour: new Color(1f, 0.55f, 0.15f), Mark: "[]", Means: "on fire"),
                (Colour: new Color(0.55f, 0.15f, 0.15f), Mark: "[]", Means: "lost")
            };

            string doorHelp = snapshot.PurseEnabled
                ? $"draw people to use it; right-click to lock or unlock it for {snapshot.CostOfLockToggle(DoorState.Locked, false)}. " +
                  $"Red is locked; the way out costs {snapshot.CostOfLockToggle(DoorState.Locked, true)} to unlock"
                : "draw people to use it, one step a click; right-click to lock or unlock it. Red is locked";
            var keys = new[]
            {
                ("Click a card", "pick it up, then click the floor to throw it. Two of a kind sit as one card"),
                ("Cards", "dealt by the dead, one each. Nobody dies, nobody deals"),
                ("Purse", snapshot.PurseEnabled ? "paid by the uproar, and by everyone who gets out" : "none on this level: everything is free"),
                ("Escape", "put the card back down (or right click)"),
                ("Click a door", doorHelp),
                ("Hold a door", "keep the button down on it and nobody can open it; the strong burst it in one push. Let go and it is a door again"),
                ("Click a person", "nudge them away from the click. Three quick ones and they are annoyed, and shake"),
                ("Click the floor", "draw people there, one step a click, up to twenty; it fades on its own. Things too"),
                ("W A S D", "move the camera"),
                ("Q E", "turn a quarter"),
                ("Wheel", "zoom"),
                ("Tab", "everyone's stats"),
                ("G", "the floor people can walk on"),
                ("Space", "start and stop the world (or the Pause button, top right)"),
                ("Reset", "the button top right: back to the start card, keeping the seed"),
                ("Trigger event", "the red button bottom centre starts the fire, once, and goes")
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
        /// <paramref name="footer"/>: which physics feel is in use, and so on.
        /// </summary>
        public static void DrawStats(RunSnapshot snapshot, string footer)
        {
            const float rowHeight = 20f;
            float width = 640f;
            float height = rowHeight * (snapshot.Agents.Count + 3) + 12f;
            // Below Reset and Pause, which sit in the top-right corner.
            var area = new Rect(Screen.width - width - 20f, 108f, width, height);
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
                DrawRow(x, y, rowHeight, new[]
                {
                    (i + 1).ToString(), t.Strength.ToString(), t.Speed.ToString(), t.Bravery.ToString(),
                    t.Compassion.ToString(), t.Evil.ToString(), t.Nervousness.ToString(), t.Leadership.ToString(),
                    TemperamentText(agent.Temperament), StateText(agent)
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
                case AgentActivityState.Fleeing: return "running";
                default: return agent.ActivityState.ToString().ToLowerInvariant();
            }
        }
    }
}
