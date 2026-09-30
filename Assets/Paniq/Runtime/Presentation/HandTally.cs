using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// How much the player used their hand in a round (2026-09-30, the owner:
    /// "the stat at the end about how many clicks/influence you used this
    /// round"): presses, clicks, pushes, pokes, tugs, how long the hand was
    /// on something, how far it was dragged, how often people answered it,
    /// and how many tells were caught in time. The number that says whether a
    /// round was frantic.
    /// <para>
    /// Worked out from the run's log and the commands the player gave, up to
    /// the round's last tick. Reads and decides nothing; no number here
    /// reaches the run.
    /// </para>
    /// </summary>
    internal readonly struct HandTally
    {
        public HandTally(int presses, int clicks, int pushes, int pokes, int tugs, int heldTicks, long draggedMillimetres,
            int answered, int againstTheirNature, int tells, int caught, int roundTicks)
        {
            Presses = presses;
            Clicks = clicks;
            Pushes = pushes;
            Pokes = pokes;
            Tugs = tugs;
            HeldTicks = heldTicks;
            DraggedMillimetres = draggedMillimetres;
            Answered = answered;
            AgainstTheirNature = againstTheirNature;
            Tells = tells;
            Caught = caught;
            RoundTicks = roundTicks;
        }

        /// <summary>Every time the hand went on a place, pulls and pushes both.</summary>
        public int Presses { get; }

        /// <summary>Of those, the ones let go of at once and left for three seconds.</summary>
        public int Clicks { get; }

        /// <summary>Of the presses, the right button's.</summary>
        public int Pushes { get; }

        public int Pokes { get; }
        public int Tugs { get; }

        /// <summary>How long the hand was on a place or a person, in ticks.</summary>
        public int HeldTicks { get; }

        /// <summary>How far a held hand was dragged, in millimetres.</summary>
        public long DraggedMillimetres { get; }

        /// <summary>Times somebody set about answering the hand.</summary>
        public int Answered { get; }

        /// <summary>Times somebody did for the hand what they never would have.</summary>
        public int AgainstTheirNature { get; }

        /// <summary>Tells begun, and of those, caught in time.</summary>
        public int Tells { get; }
        public int Caught { get; }

        /// <summary>How long the round ran, in ticks.</summary>
        public int RoundTicks { get; }

        /// <summary>Everything the player did with a click: presses, pokes and tugs.</summary>
        public int Actions => Presses + Pokes + Tugs;

        /// <summary>Actions a minute over the round.</summary>
        public int PerMinute => RoundTicks <= 0 ? 0 : (int)((long)Actions * 60 * Run.TicksPerSecond / RoundTicks);

        /// <summary>
        /// Counts the round. A press is on until its release or the next press,
        /// a tug until it is let go of, shaken off or replaced; anything still
        /// on at <paramref name="endTick"/> counts up to it. Commands after
        /// <paramref name="endTick"/> never happened.
        /// </summary>
        public static HandTally From(IReadOnlyList<CausalEvent> events, IReadOnlyList<PlayerCommand> commands, int endTick)
        {
            int presses = 0, pushes = 0, pokes = 0, tugs = 0, answered = 0, against = 0, tells = 0, caught = 0;
            long held = 0L;
            int placeSince = -1, personSince = -1;
            for (int i = 0; i < events.Count; i++)
            {
                CausalEvent record = events[i];
                switch (record.EventType)
                {
                    case CausalEventType.PowerInfluenced:
                    case CausalEventType.PowerRepelled:
                        presses++;
                        if (record.EventType == CausalEventType.PowerRepelled)
                        {
                            pushes++;
                        }

                        if (placeSince >= 0)
                        {
                            held += record.Tick - placeSince;
                        }

                        placeSince = record.Tick;
                        break;
                    case CausalEventType.PowerReleasedInfluence:
                        if (placeSince >= 0)
                        {
                            held += record.Tick - placeSince;
                            placeSince = -1;
                        }

                        break;
                    case CausalEventType.PowerTugged:
                        tugs++;
                        if (personSince >= 0)
                        {
                            held += record.Tick - personSince;
                        }

                        personSince = record.Tick;
                        break;
                    case CausalEventType.PowerReleasedTug:
                    case CausalEventType.AgentShookFree:
                        if (personSince >= 0)
                        {
                            held += record.Tick - personSince;
                            personSince = -1;
                        }

                        break;
                    case CausalEventType.PowerNudged: pokes++; break;
                    case CausalEventType.AgentDrawnByInfluence: answered++; break;
                    case CausalEventType.AgentActedForTheHand: against++; break;
                    case CausalEventType.AgentBeganATell: tells++; break;
                    case CausalEventType.AgentCaughtInTime: caught++; break;
                }
            }

            if (placeSince >= 0)
            {
                held += endTick - placeSince;
            }

            if (personSince >= 0)
            {
                held += endTick - personSince;
            }

            int clicks = 0;
            long dragged = 0L;
            bool haveLast = false;
            LogicalPosition last = default;
            for (int i = 0; i < commands.Count; i++)
            {
                PlayerCommand command = commands[i];
                if (command.TargetTick > endTick)
                {
                    continue;
                }

                switch (command.CommandType)
                {
                    case PlayerCommandType.LeaveInfluence:
                        clicks++;
                        haveLast = false;
                        break;
                    case PlayerCommandType.InfluenceSpot:
                    case PlayerCommandType.RepelSpot:
                        last = command.Point;
                        haveLast = true;
                        break;
                    case PlayerCommandType.InfluenceDoor:
                    case PlayerCommandType.InfluenceThing:
                    case PlayerCommandType.RepelDoor:
                    case PlayerCommandType.RepelThing:
                    case PlayerCommandType.ReleaseInfluence:
                        haveLast = false;
                        break;
                    case PlayerCommandType.MoveInfluence:
                        if (haveLast)
                        {
                            dragged += IntegerMath.Distance(last, command.Point);
                        }

                        last = command.Point;
                        haveLast = true;
                        break;
                }
            }

            return new HandTally(presses, clicks, pushes, pokes, tugs, (int)System.Math.Min(int.MaxValue, held), dragged,
                answered, against, tells, caught, endTick);
        }
    }
}
