using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// The run's causal log, said out loud. Every event the simulation records
    /// already knows who did it, to whom, when and why; this turns one of them
    /// into a sentence somebody can read.
    /// <para>
    /// People are named by the number drawn over their head and shown in the
    /// stats table, so a line in the log points at somebody the player watched.
    /// Doors are named by the number the hover hint uses. Things are named by
    /// what they are, because "the microwave" is worth more than an ID.
    /// </para>
    /// <para>
    /// This reads the log and nothing else. It decides no outcome and changes
    /// nothing, which is the rule for anything on this side of the line.
    /// </para>
    /// </summary>
    internal sealed class EventStory
    {
        /// <summary>Person ID to the number over their head, which starts at one.</summary>
        private readonly Dictionary<ulong, int> personNumbers = new Dictionary<ulong, int>();

        /// <summary>Thing ID to what it is, so a line can say "a chair" rather than "3104".</summary>
        private readonly Dictionary<ulong, string> thingNames = new Dictionary<ulong, string>();

        private readonly HashSet<ulong> doorIds = new HashSet<ulong>();

        /// <summary>The player's commands, for the tally of the hand on the end card (2026-09-30); empty when not given.</summary>
        private readonly IReadOnlyList<PlayerCommand> commands;

        public EventStory(RunSnapshot snapshot, IReadOnlyList<PlayerCommand> commands = null)
        {
            this.commands = commands ?? System.Array.Empty<PlayerCommand>();
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                personNumbers[snapshot.Agents[i].AgentId.Value] = i + 1;
            }

            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                thingNames[thing.ObjectId.Value] = NameOfKind(thing.Kind);
            }

            for (int i = 0; i < snapshot.Doors.Count; i++)
            {
                doorIds.Add(snapshot.Doors[i].DoorId.Value);
            }
        }

        /// <summary>A card's name, for the log of a level that still deals them (the office does not, 2026-09-30).</summary>
        private static string NameOfCard(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return "Beefcake";
                case PlayerCommandType.PlayCourage: return "Courage";
                case PlayerCommandType.PlayTerror: return "Terror";
                case PlayerCommandType.PlayBastard: return "Bastard";
                case PlayerCommandType.PlayColdHeart: return "Cold heart";
                case PlayerCommandType.SpawnFire: return "Start a fire";
                case PlayerCommandType.SpawnExtinguisher: return "Fire extinguisher";
                case PlayerCommandType.BlastWall: return "TNT";
                case PlayerCommandType.PopFuseBox: return "Pop the fuse box";
                case PlayerCommandType.PullAlarm: return "Pull a fire alarm";
                case PlayerCommandType.StickTogether: return "Stick together";
                default: return card.ToString();
            }
        }

        /// <summary>
        /// The chatter. These happen dozens or hundreds of times in a round --
        /// the fire creeping one square, an extinguisher hissing, people
        /// bumping shoulders -- and printed one to a line they bury everything
        /// worth reading. The log folds runs of them into a single line.
        /// </summary>
        public static bool IsBackground(CausalEventType type)
        {
            switch (type)
            {
                case CausalEventType.FireSpread:
                case CausalEventType.FireDoused:
                case CausalEventType.ExtinguisherSprayed:
                case CausalEventType.AgentYelled:
                case CausalEventType.AgentNoticedSound:
                case CausalEventType.AgentsCollided:
                case CausalEventType.BoxBumped:
                case CausalEventType.BoxesCollided:
                case CausalEventType.AlarmRang:
                case CausalEventType.ObjectBurntOut:
                case CausalEventType.PowerSparkArrived:
                case CausalEventType.TrapTriggered:
                case CausalEventType.DirectorPushed:
                case CausalEventType.AgentSaid:
                case CausalEventType.PowerInfluenced:
                case CausalEventType.AgentDrawnByInfluence:
                case CausalEventType.PowerReleasedInfluence:
                case CausalEventType.PowerHandSpent:
                case CausalEventType.PowerTugged:
                case CausalEventType.PowerReleasedTug:
                case CausalEventType.TrapCreaked:
                case CausalEventType.PowerRepelled:
                case CausalEventType.AgentPushedAwayByInfluence:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The round in three or four lines (2026-09-29): the smallest form of
        /// the plain-language retelling the game vision asks for, for the end
        /// card. What the player's hand did, the keycard, the corridor, and
        /// the fire, each from the events that decided it. Reads the log and
        /// decides nothing. The hand's dials are named on the hand's line when
        /// they were off the level's own (the strength's own is 100; the
        /// reach's is whatever the caller says it is, nought meaning "do not
        /// say").
        /// </summary>
        public List<string> Retell(RunSnapshot snapshot, int handStrengthPercent = 100, int handReachMillimetres = 0,
            int levelReachMillimetres = 0)
        {
            var lines = new List<string>(5);
            IReadOnlyList<CausalEvent> events = snapshot.Events;

            int tornFree = 0;
            CausalEvent? cardStarted = null, cardTaken = null, cardSwiped = null, cardDropped = null;
            CausalEvent? towerFell = null, fireLoose = null, putOut = null;
            int outAfterTheFall = 0;
            for (int i = 0; i < events.Count; i++)
            {
                CausalEvent record = events[i];
                switch (record.EventType)
                {
                    case CausalEventType.AgentShookFree: tornFree++; break;
                    case CausalEventType.KeycardStarted: cardStarted ??= record; break;
                    case CausalEventType.AgentTookKeycard: cardTaken = record; break;
                    case CausalEventType.KeycardDropped: cardDropped = record; break;
                    case CausalEventType.DoorUnlockedWithKeycard: cardSwiped ??= record; break;
                    case CausalEventType.BoxTowerFell:
                        if (record.HasTarget)
                        {
                            towerFell ??= record;
                        }

                        break;
                    case CausalEventType.AgentEscaped:
                        if (towerFell.HasValue)
                        {
                            outAfterTheFall++;
                        }

                        break;
                    case CausalEventType.FireEscapedItsRoom: fireLoose ??= record; break;
                    case CausalEventType.IncidentPutOut: putOut = record; break;
                }
            }

            // Your hand (2026-09-30, the owner: "the stat at the end about how
            // many clicks/influence you used this round"): what you did, how
            // often, and what came of it.
            HandTally tally = HandTally.From(events, commands, snapshot.Tick);
            var dials = new List<string>(2);
            if (handStrengthPercent != 100)
            {
                dials.Add($"hand strength {handStrengthPercent}%");
            }

            if (levelReachMillimetres > 0 && handReachMillimetres != levelReachMillimetres)
            {
                dials.Add($"reach {DebugView.Metres(handReachMillimetres)} m");
            }

            string strength = dials.Count > 0 ? $" ({string.Join(", ", dials)})" : "";
            if (tally.Actions == 0)
            {
                lines.Add($"Your hand: never on anything. The building played itself.{strength}");
            }
            else
            {
                string tore = tornFree > 0 ? $" ({tornFree} tore free)" : "";
                string dragged = tally.DraggedMillimetres >= 1000 ? $", dragged {tally.DraggedMillimetres / 1000} m" : "";
                lines.Add($"Your hand: {Count(tally.Actions, "action")}, {tally.PerMinute} a minute -- " +
                          $"{Count(tally.Presses, "press", "presses")} ({Count(tally.Clicks, "click")}, " +
                          $"{Count(tally.Pushes, "push", "pushes")}), {Count(tally.Pokes, "poke")}, " +
                          $"{Count(tally.Tugs, "tug")}{tore}; held {tally.HeldTicks / Run.TicksPerSecond} s{dragged}.{strength}");
            }

            var came = new List<string>(3);
            if (tally.Answered > 0)
            {
                came.Add($"{Count(tally.Answered, "time")} somebody answered it");
            }

            if (tally.AgainstTheirNature > 0)
            {
                came.Add($"{tally.AgainstTheirNature} did for you what they never would");
            }

            if (tally.Tells > 0)
            {
                came.Add($"you caught {tally.Caught} of {tally.Tells} in time");
            }

            if (came.Count > 0)
            {
                string joined = string.Join("; ", came);
                lines.Add(char.ToUpperInvariant(joined[0]) + joined.Substring(1) + ".");
            }

            // The card.
            if (cardStarted.HasValue)
            {
                CausalEvent started = cardStarted.Value;
                string began = started.SourceId == started.TargetId ? "lay on a desk" : $"was in {Name(started.SourceId)}'s pocket";
                if (cardSwiped.HasValue)
                {
                    lines.Add($"The card {began}; {Name(cardSwiped.Value.SourceId)} swiped the door open at {TimeOf(cardSwiped.Value.Tick)}.");
                }
                else if (cardTaken.HasValue)
                {
                    bool lost = cardDropped.HasValue && cardDropped.Value.Tick > cardTaken.Value.Tick;
                    lines.Add(lost
                        ? $"The card {began}; {Name(cardTaken.Value.SourceId)} had it and dropped it at {TimeOf(cardDropped.Value.Tick)}. The door never opened."
                        : $"The card {began}; {Name(cardTaken.Value.SourceId)} had it and never reached the door.");
                }
                else if (started.SourceId != started.TargetId)
                {
                    lines.Add($"The card {began}, and they never reached the door.");
                }
                else
                {
                    lines.Add("The card lay on a desk and nobody ever picked it up.");
                }
            }

            // The corridor.
            lines.Add(towerFell.HasValue
                ? $"The boxes came down across the corridor at {TimeOf(towerFell.Value.Tick)}; {outAfterTheFall} got out after that."
                : "The tower of boxes never came down.");

            // The fire.
            if (fireLoose.HasValue)
            {
                lines.Add($"The fire got out of the room it started in at {TimeOf(fireLoose.Value.Tick)}; {snapshot.LostCount} did not make it.");
            }
            else if (putOut.HasValue)
            {
                lines.Add($"The fire was put out at {TimeOf(putOut.Value.Tick)} and never left its room.");
            }
            else
            {
                lines.Add("The fire never left the room it started in.");
            }

            return lines;
        }

        private static string Count(int n, string one, string many = null) =>
            n == 1 ? $"1 {one}" : $"{n} {many ?? one + "s"}";

        /// <summary>
        /// The number drawn over this person's head, or nothing if the ID is
        /// not a person's. Shared with anything else that names people, so the
        /// "3" on a pop-up sign and the "person 3" in the log are the same
        /// person the player has been watching.
        /// </summary>
        public int? NumberOf(SimulationId id)
        {
            return personNumbers.TryGetValue(id.Value, out int number) ? number : (int?)null;
        }

        /// <summary>The tick as a clock reading, counted from the start of the run.</summary>
        /// <remarks>Kept beside the other formatting helpers.</remarks>
        public static string TimeOf(int tick)
        {
            int seconds = tick / Run.TicksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>What happened, in one sentence.</summary>
        public string Describe(CausalEvent record)
        {
            string who = Name(record.SourceId);
            string whom = Name(record.TargetId);
            switch (record.EventType)
            {
                case CausalEventType.RoundEventTriggered: return "you set it off";
                case CausalEventType.FireActivated: return "a fire took hold";
                case CausalEventType.FireSpread: return "the fire spread";
                case CausalEventType.FireDoused: return "a burning patch went out";

                case CausalEventType.AgentAlerted: return $"{who} noticed something was wrong";
                case CausalEventType.AgentYelled: return $"{who} shouted";
                case CausalEventType.AgentNoticedSound: return $"{who} heard something";
                case CausalEventType.AgentScared: return $"{who} panicked";
                case CausalEventType.AgentFroze: return $"{who} froze on the spot";
                case CausalEventType.AgentUnfroze: return $"{who} came out of it and moved";
                case CausalEventType.AgentCaughtFire: return $"{who} caught fire";
                case CausalEventType.AgentRolled: return $"{who} threw themselves down and rolled";
                case CausalEventType.AgentLost: return $"{who} did not make it";
                case CausalEventType.AgentEscaped: return $"{who} got out of the building";
                case CausalEventType.AgentSurvived: return $"{who} was still alive inside at the end";

                case CausalEventType.AgentsCollided: return $"{who} ran into {whom}";
                case CausalEventType.AgentKnockedDown: return $"{who} was knocked off their feet";
                case CausalEventType.AgentTripped: return $"{who} tripped";
                case CausalEventType.AgentGotUp: return $"{who} picked themselves up";
                case CausalEventType.AgentPassedOut: return $"{who} was knocked out cold";
                case CausalEventType.AgentCameTo: return $"{who} came round";
                case CausalEventType.AgentCrushed: return $"{who} was squeezed off their feet by the crush";
                case CausalEventType.AgentShoved: return $"{who} heaved {whom} out of the way";

                case CausalEventType.AgentLookedForAWayOut: return $"{who} did not know the way out and went looking";
                case CausalEventType.AgentFoundADeadEnd: return $"{who} found only a dead end";
                case CausalEventType.AgentFoundTheWayOut:
                    switch ((WayLearned)record.Strength)
                    {
                        case WayLearned.Sign: return $"{who} read a sign and knew the way out";
                        case WayLearned.SawItOpen: return $"{who} saw a way out open up";
                        case WayLearned.Told: return $"{who} was shown the way out";
                        default: return $"{who} spotted the way out";
                    }

                case CausalEventType.DoorUnlocked: return $"{who} was unlocked";
                case CausalEventType.DoorOpened: return $"{who} was opened";
                case CausalEventType.DoorClosed: return $"{who} shut {whom}";
                case CausalEventType.DoorLocked:
                    // The player's own turn of the key names the door as its
                    // source and its target both.
                    return record.SourceId == record.TargetId ? $"you locked {whom}" : $"{who} locked {whom}";
                case CausalEventType.DoorBrokenDown: return $"{who} shouldered {whom} off its hinges";
                case CausalEventType.DoorBurntThrough: return $"{who} burnt through and the fire came on";
                case CausalEventType.DoorBlocked: return $"{who} came to rest in {whom} and jammed it";
                case CausalEventType.DoorUnblocked: return $"{whom} was clear again";
                case CausalEventType.AgentTriedDoor: return $"{who} tried a door and it would not open";
                case CausalEventType.AgentForcedDoor: return $"{who} threw a shoulder at a door";
                case CausalEventType.AgentGaveUpOnDoor: return $"{who} gave up on a door";
                case CausalEventType.AgentDashedThroughHeat: return $"{who} ran for it through the heat";
                case CausalEventType.AgentHidFromTheHeat: return $"{who} would not go through the heat and looked for somewhere to hide";
                case CausalEventType.AgentCarriedThroughDoorway: return $"{who}, down in the doorway, was carried through it by the crush";
                case CausalEventType.AgentBarricadedDoor: return $"{who} wedged something against {whom}";
                case CausalEventType.AgentShovedObstruction: return $"{who} heaved {whom} out of a doorway";
                case CausalEventType.AgentClearedDoorway: return $"{who} lifted {whom} out of a doorway";

                case CausalEventType.AgentTookExtinguisher: return $"{who} picked up {whom}";

                case CausalEventType.KeycardStarted:
                    return record.SourceId == record.TargetId
                        ? "the keycard lay on a desk"
                        : $"the keycard was in {who}'s pocket";
                case CausalEventType.AgentTookKeycard: return $"{who} pocketed the keycard";
                case CausalEventType.KeycardDropped: return $"{who} dropped the keycard where they fell";
                case CausalEventType.DoorUnlockedWithKeycard: return $"{who} swiped the keycard and {whom} was unlocked for good";
                case CausalEventType.ExtinguisherSprayed: return "an extinguisher was sprayed at the fire";
                case CausalEventType.ExtinguisherEmptied: return $"{who} ran dry";
                case CausalEventType.AgentBlasted: return $"{whom} was knocked over by the jet";
                case CausalEventType.AgentDoused: return $"{whom} was hosed down and put out";

                case CausalEventType.LeaderCalledPeopleOn: return $"{who} called everybody on";
                case CausalEventType.LeaderOrderedDoorBroken: return $"{who} sent {whom} at a door";
                case CausalEventType.LeaderOrderedFireFought: return $"{who} sent {whom} for an extinguisher";

                case CausalEventType.AgentShookAwake: return $"{who} shook {whom} awake";
                case CausalEventType.AgentGrabbed: return $"{who} took hold of {whom}";
                case CausalEventType.AgentDropped: return $"{who} let go of {whom}";
                case CausalEventType.AgentRescued: return $"{who} dragged {whom} clear";

                case CausalEventType.ItemThrown: return $"{who} flung {whom} away from them";
                case CausalEventType.TableHeaved: return $"{who} heaved a table out of the way";
                case CausalEventType.ObjectPopped: return $"{who} went over and its bulb popped";
                case CausalEventType.ItemDropped: return $"{who} dropped {whom}";
                case CausalEventType.BoxBumped: return $"{whom} was knocked about";
                case CausalEventType.BoxHitAgent: return $"{whom} was hit by something flying";
                case CausalEventType.BoxesCollided: return $"{who} knocked into {whom}";
                case CausalEventType.ObjectCaughtFire: return $"{who} caught fire";
                case CausalEventType.ObjectBurntOut: return $"{who} burnt out";
                case CausalEventType.ObjectBroke: return $"{who} broke";
                case CausalEventType.ObjectExploded: return $"{who} went off with a bang";

                case CausalEventType.AlarmPulled: return $"{who} hit a fire alarm";
                case CausalEventType.AlarmRang: return "the alarms rang out";

                case CausalEventType.PowerBeefcake: return $"you made {whom} as strong as anyone can be";
                case CausalEventType.PowerCourage: return $"you made {whom} fearless";
                case CausalEventType.PowerTerror: return $"you put the fear of God into {whom}";
                case CausalEventType.PowerBastard: return $"you turned {whom} nasty";
                case CausalEventType.PowerColdHeart: return $"you stopped {whom} caring what happened to anybody";
                case CausalEventType.PowerSpawnedFire: return "you started a fire of your own";
                case CausalEventType.PowerSpawnedExtinguisher: return "you stood an extinguisher on the floor";
                case CausalEventType.PowerBlastedWall: return "you blew a hole through a wall";
                case CausalEventType.PowerPoppedFuseBox: return "you popped the fuse box";
                case CausalEventType.PowerPulledAlarm: return "you pulled a fire alarm";
                case CausalEventType.PowerPanickedCrowd: return "you set the whole crowd panicking";
                case CausalEventType.PowerCalmedCrowd: return "you calmed the whole crowd down";
                case CausalEventType.PowerHeldDoor: return $"you held {who} shut";
                case CausalEventType.PowerReleasedDoor: return $"you let go of {who}";
                case CausalEventType.PowerNudged: return $"you nudged {whom}";
                case CausalEventType.AgentNudged: return $"{who} looked round for whoever nudged them";
                case CausalEventType.AgentAnnoyed: return $"{who} got annoyed at being nudged";
                case CausalEventType.TrapTriggered:
                    return record.HasTarget ? $"{whom} ran past the tower of boxes" : "the boxes gave way";
                case CausalEventType.DirectorPushed:
                    return $"the building turned on the crowd: {record.Strength} were on course to get out, {record.DurationTicks} allowed";
                case CausalEventType.BoxTowerFell:
                    return record.HasTarget ? $"the tower of boxes came down toward {whom}" : "the crates came down across the lane";
                case CausalEventType.BoxHeapSettled: return $"the fallen boxes blocked {whom}";
                case CausalEventType.BoxPileCleared: return $"the way through the boxes at {whom} was clear";
                case CausalEventType.PowerStickTogether: return $"you told {whom} to stick together";
                case CausalEventType.DirectorStartedIncident:
                    return record.Strength > 1 ? $"another bin: {who} caught fire" : $"{who} caught fire";
                case CausalEventType.IncidentPutOut: return "the fire was put out";
                case CausalEventType.FireEscapedItsRoom: return "the fire got out of the room it started in";
                case CausalEventType.SocketCrackling: return $"{who} began to crackle and smoke";
                case CausalEventType.AllClear: return "the alarms fell silent: all clear";
                case CausalEventType.AgentCalmedDown: return $"{who} calmed down";
                case CausalEventType.PowerInfluenced:
                    return record.HasTarget ? $"you put your hand on {whom}" : "you put your hand on a spot on the floor";
                case CausalEventType.PowerReleasedInfluence:
                    return record.HasTarget ? $"you took your hand off {whom}" : "you took your hand off the floor";
                case CausalEventType.PowerHandSpent: return "your hand gave out";
                case CausalEventType.AgentDrawnByInfluence: return $"{who} went where your hand was";
                case CausalEventType.InfluenceSpent: return $"{who} did what your hand asked";
                case CausalEventType.PowerTugged: return $"you took {whom} by the shirt";
                case CausalEventType.PowerReleasedTug: return $"you let go of {whom}";
                case CausalEventType.AgentShookFree: return $"{who} tore free of your hand";
                case CausalEventType.TrapCreaked: return $"{who} creaked and swayed";
                case CausalEventType.PowerRepelled:
                    return record.HasTarget ? $"you pushed people away from {whom}" : "you pushed people away from a spot on the floor";
                case CausalEventType.AgentPushedAwayByInfluence: return $"{who} moved away from your hand";
                case CausalEventType.AgentActedForTheHand:
                    switch ((AgainstTheirNature)record.Strength)
                    {
                        case AgainstTheirNature.FoughtTheFire: return $"{who} had no nerve for it, and fought the fire for you";
                        case AgainstTheirNature.BatteredTheDoor: return $"{who} had no strength for it, and threw themselves at {whom} for you";
                        case AgainstTheirNature.HeavedTheBox: return $"{who} had no strength for it, and heaved {whom} aside for you";
                        case AgainstTheirNature.PulledTheAlarm: return $"{who} had no nerve for it, and pulled the alarm for you";
                        case AgainstTheirNature.WentForTheCard: return $"{who} had no nerve for it, and went back for the keycard for you";
                        default: return $"{who} did something for you";
                    }
                case CausalEventType.AgentPokedAwake: return $"{who} was poked awake";
                case CausalEventType.AgentKnockedOffChair: return $"{who} was poked off their chair";
                case CausalEventType.AgentBeganATell:
                    switch ((AgentTell)record.Strength)
                    {
                        case AgentTell.GoingStiff: return $"{who} began to go stiff with fear";
                        case AgentTell.GatheringNerve: return $"{who} gathered their nerve to run through the heat";
                        default: return $"{who} turned back toward the danger";
                    }
                case CausalEventType.AgentCaughtInTime:
                    switch ((AgentTell)record.Strength)
                    {
                        case AgentTell.GoingStiff: return $"you caught {who} before they froze";
                        case AgentTell.GatheringNerve: return $"you caught {who} before they ran through the heat";
                        default: return $"you caught {who} before they went back";
                    }

                case CausalEventType.PowerSparkStarted:
                    return $"a spark set off along the cable from {Name(record.SourceId)} " +
                           $"toward {Name(record.TargetId)}";
                case CausalEventType.PowerSparkArrived:
                    return $"the spark reached {Name(record.TargetId)}";

                case CausalEventType.CardDealt:
                    return record.SourceId.Value == 0UL
                        ? $"you were dealt {NameOfCard((PlayerCommandType)record.Strength)} to start"
                        : $"{who} died, and dealt you {NameOfCard((PlayerCommandType)record.Strength)}";

                case CausalEventType.RoundEnded: return $"the round ended with {record.Strength} saved";

                case CausalEventType.CueCalled:
                    switch ((CueKind)record.Strength)
                    {
                        case CueKind.MeetingEnds:
                            return record.SourceId.Value == 0UL ? "the meeting ended" : $"{who} ended the meeting";
                        case CueKind.HomeTime: return "it was home time";
                        case CueKind.Chat: return $"{who} and {whom} had a chat";
                        case CueKind.ToiletTrip: return $"{who} went to the toilet";
                        case CueKind.GoHome: return $"{who} went back to their desk";
                        case CueKind.GoAndLook: return $"{who} went to see what the noise was";
                        default: return $"{who} called {(CueKind)record.Strength}";
                    }

                case CausalEventType.AgentSaid: return $"{who} said something";
                case CausalEventType.PowerCalledHomeTime: return "you called it a day";
                case CausalEventType.AgentIgnoredCue:
                    switch ((CueKind)record.Strength)
                    {
                        case CueKind.MeetingEnds: return $"{who} sat on when the meeting ended";
                        case CueKind.HomeTime: return $"{who} ignored home time";
                        case CueKind.Chat: return $"{who} would not talk to {whom}";
                        default: return $"{who} ignored {(CueKind)record.Strength}";
                    }

                default: return record.EventType.ToString();
            }
        }

        /// <summary>
        /// Whatever this ID belongs to, said the way the player already sees it:
        /// a person by their number, a door by the number the hover hint uses,
        /// a thing by what it is.
        /// </summary>
        private string Name(SimulationId id)
        {
            if (id.Value == 0UL)
            {
                return "somebody";
            }

            if (personNumbers.TryGetValue(id.Value, out int number))
            {
                return $"person {number}";
            }

            if (doorIds.Contains(id.Value))
            {
                return $"door {id.Value}";
            }

            return thingNames.TryGetValue(id.Value, out string thing) ? thing : "something";
        }

        private static string NameOfKind(PhysicsObjectKind kind)
        {
            switch (kind)
            {
                case PhysicsObjectKind.Box: return "a cardboard box";
                case PhysicsObjectKind.Chair: return "a chair";
                case PhysicsObjectKind.OfficeChair: return "an office chair";
                case PhysicsObjectKind.WasteBin: return "a waste bin";
                case PhysicsObjectKind.PottedPlant: return "a potted plant";
                case PhysicsObjectKind.Bag: return "a bag";
                case PhysicsObjectKind.Laptop: return "a laptop";
                case PhysicsObjectKind.Extinguisher: return "an extinguisher";
                case PhysicsObjectKind.Briefcase: return "a briefcase";
                case PhysicsObjectKind.Microwave: return "the microwave";
                case PhysicsObjectKind.WallSocket: return "a wall socket";
                case PhysicsObjectKind.FuseBox: return "the fuse box";
                case PhysicsObjectKind.TableWreck: return "a heap of broken boards";
                case PhysicsObjectKind.VendingMachine: return "the vending machine";
                case PhysicsObjectKind.Cabinet: return "a filing cabinet";
                case PhysicsObjectKind.Shelves: return "a set of shelves";
                case PhysicsObjectKind.CopyMachine: return "the copier";
                case PhysicsObjectKind.Whiteboard: return "a whiteboard";
                case PhysicsObjectKind.StandingLamp: return "a standing lamp";
                case PhysicsObjectKind.LampShade: return "a lamp shade";
                case PhysicsObjectKind.RobotVacuum: return "the robot vacuum";
                case PhysicsObjectKind.AlarmSounder: return "a fire alarm bell";
                case PhysicsObjectKind.Keycard: return "the keycard";
                default: return "something";
            }
        }
    }
}
