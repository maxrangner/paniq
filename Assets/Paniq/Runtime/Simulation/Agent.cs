namespace Paniq.Simulation
{
    /// <summary>
    /// One person's runtime state, split by concern so it is clear which
    /// system changes what:
    /// <list type="bullet">
    /// <item><see cref="Body"/>: where they are and how they move. Only
    /// <see cref="Locomotion"/> turns and accelerates it; the body and
    /// collision systems may stop it, knock it down or jolt it.</item>
    /// <item><see cref="Traits"/>: strength, speed, bravery, compassion, evil
    /// and nervousness (0–10). Set at the start; only the simulation may
    /// change them (a later player power could).</item>
    /// <item><see cref="Personality"/>: seeded once at the start, never changed.</item>
    /// <item><see cref="Fear"/>: calm, alert or scared, and the timers and
    /// events that go with it (<see cref="FearSystem"/>).</item>
    /// <item><see cref="Intent"/>: what they are trying to do right now (the behaviours).</item>
    /// <item><see cref="Hearing"/>: the last noise worth turning toward.</item>
    /// <item><see cref="Doors"/>: the door they are running for and doors that failed them.</item>
    /// <item><see cref="Burning"/>: whether they are on fire, and until when.</item>
    /// <item><see cref="Carry"/>: the item they are going for or carrying.</item>
    /// <item><see cref="Help"/>: the person they are helping, if any.</item>
    /// <item><see cref="Sitting"/>: the chair they are on, if any.</item>
    /// <item><see cref="Leading"/>: who they are following, and what they were told to do.</item>
    /// <item><see cref="Alarm"/>: the fire alarm they are going to hit, if any.</item>
    /// <item><see cref="Barricade"/>: the door they are wedging something against, if any.</item>
    /// </list>
    /// </summary>
    internal sealed class Agent
    {
        public Agent(int index, SimulationId id, int doorCount)
        {
            Index = index;
            Id = id;
            Doors = new AgentDoorMemory(doorCount);
        }

        /// <summary>Position in ascending-ID order; the processing order of every per-person loop.</summary>
        public readonly int Index;

        public readonly SimulationId Id;
        public AgentParticipation Participation;
        public AgentTerminalOutcome Outcome;

        public AgentTraitValues Traits;

        public readonly AgentBody Body = new AgentBody();
        public readonly AgentPersonality Personality = new AgentPersonality();
        public readonly AgentFear Fear = new AgentFear();
        public readonly AgentIntent Intent = new AgentIntent();
        public readonly AgentHearing Hearing = new AgentHearing();
        public readonly AgentDoorMemory Doors;
        public readonly AgentBurning Burning = new AgentBurning();
        public readonly AgentCarry Carry = new AgentCarry();
        public readonly AgentHelp Help = new AgentHelp();
        public readonly AgentSitting Sitting = new AgentSitting();
        public readonly AgentLeading Leading = new AgentLeading();
        public readonly AgentAlarm Alarm = new AgentAlarm();
        public readonly AgentBarricade Barricade = new AgentBarricade();

        public bool IsParticipating => Participation == AgentParticipation.Participating;

        /// <summary>Lying on the floor (awake or knocked out) or getting up.</summary>
        public bool IsDown => Body.State == AgentBodyState.Fallen || Body.State == AgentBodyState.GettingUp ||
                              Body.State == AgentBodyState.Unconscious;

        /// <summary>
        /// The doorway this person may step into at the moment.
        ///
        /// A way out they are running for comes first, then a doorway they
        /// picked to stroll through, and then -- for anybody on an errand --
        /// any open doorway at all.
        ///
        /// That last case is the point. A doorway used to be walkable only for
        /// somebody running for a way out, so anyone carrying an extinguisher
        /// to a fire in the next room walked up to the doorway and slid along
        /// the wall beside it. The rule was written when nothing could cross a
        /// room and the only reason to be in a doorway was to escape; now
        /// people have errands that take them through the building. Somebody
        /// standing about, or wandering inside one room, is still walled in, so
        /// nobody drifts through a door for no reason.
        /// </summary>
        public int DoorwayInUse
        {
            get
            {
                if (Doors.ExitDoorIndex >= 0)
                {
                    return Doors.ExitDoorIndex;
                }

                if (Doors.StrollDoorIndex >= 0)
                {
                    return Doors.StrollDoorIndex;
                }

                return IsOnAnErrand ? AgentDoorMemory.AnyDoorway : -1;
            }
        }

        /// <summary>On their way to something in particular, rather than standing about or milling around.</summary>
        public bool IsOnAnErrand
        {
            get
            {
                switch (Intent.Activity)
                {
                    case AgentActivityState.FetchingExtinguisher:
                    case AgentActivityState.Spraying:
                    case AgentActivityState.GoingToSit:
                    case AgentActivityState.FetchingItem:
                    case AgentActivityState.CarryingItem:
                    case AgentActivityState.ShakingAwake:
                    case AgentActivityState.Grabbing:
                    case AgentActivityState.Dragging:
                    case AgentActivityState.GoingToAlarm:
                    case AgentActivityState.FetchingBarricade:
                    case AgentActivityState.CarryingBarricade:
                    case AgentActivityState.Following:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public FireReactionAgentSnapshot ToSnapshot()
        {
            return new FireReactionAgentSnapshot(
                Id,
                Body.Position,
                Participation,
                Fear.State,
                Fear.AlertSource,
                Outcome,
                Intent.Activity,
                Body.Heading,
                Body.Speed,
                Personality.CalmSpeed,
                Personality.PanicSpeed,
                Fear.ReactionDelayTicks,
                Personality.Temperament,
                Body.State,
                Traits,
                Burning.IsBurning,
                Leading.LedCount > 0,
                Fear.Composed);
        }
    }

    internal sealed class AgentBody
    {
        /// <summary>
        /// Where the body stands. Read freely; to move it, call
        /// <see cref="Crowd.MoveTo"/> rather than assigning here. The crowd
        /// keeps an index of who is standing where, and a move that did not go
        /// through it would leave that index describing the last tick. The
        /// setter is private so that a new way of moving somebody cannot be
        /// written without noticing this.
        /// </summary>
        public LogicalPosition Position { get; private set; }

        /// <summary>Moves the body. Call <see cref="Crowd.MoveTo"/>, which is what keeps the index true.</summary>
        internal void MoveWithoutTellingTheCrowd(LogicalPosition position)
        {
            Position = position;
        }

        /// <summary>Whole degrees clockwise from north.</summary>
        public int Heading;

        /// <summary>Millimetres per tick.</summary>
        public int Speed;

        /// <summary>Ticks in a row this person wanted to move and could not.</summary>
        public int BlockedTicks;

        /// <summary>Until this tick, the same jet of water cannot knock them over again.</summary>
        public int BlastedUntilTick;

        public AgentBodyState State;

        /// <summary>
        /// Still standing, even if they have just been jolted.
        ///
        /// Anything already under way -- carrying a bottle to a fire, going to
        /// hit an alarm, wedging a door -- asks this rather than "perfectly
        /// steady". A stagger is two tenths of a second after somebody clips
        /// you in a doorway, and treating it as being off your feet meant a
        /// single brush from a passer-by made somebody drop what they were
        /// doing and run. Being knocked down is still being knocked down.
        /// </summary>
        public bool IsOnTheirFeet => State == AgentBodyState.Upright || State == AgentBodyState.Staggering;

        public int EndTick;

        /// <summary>The event that put the body in its current state, for the later AgentGotUp.</summary>
        public ulong EventId;
    }

    internal sealed class AgentPersonality
    {
        public int CalmSpeed;
        public int PanicSpeed;
        public int CalmTurnRate;
        public int PanicTurnRate;
        public AgentPanicTemperament Temperament;
    }

    internal sealed class AgentFear
    {
        public AgentFearState State;
        public AgentAlertSource AlertSource;
        public int ReactionDelayTicks;
        public int ReactionEndTick;
        public ulong AlertEventId;
        public ulong ScaredEventId;
        public int FreezeEndTick;
        public ulong FrozeEventId;
        public int NextShoutTick;

        /// <summary>
        /// Told about the fire by an alarm bell rather than by seeing it, and
        /// level-headed enough to walk out instead of panicking: no sprinting,
        /// no zig-zagging, no dithering and no freezing. It lasts until the fire
        /// actually comes at them (<see cref="FearSystem.BreakComposure"/>).
        /// </summary>
        public bool Composed;
    }

    internal sealed class AgentIntent
    {
        public AgentActivityState Activity;
        public int ActivityEndTick;
        public LogicalPosition Target;
        public int LookHeading;
        public int LooksRemaining;
        public int WanderOffset;
        public int NextWanderTick;
        public int SocialPartnerIndex = -1;
        public int SwerveOffset;
        public int SwerveEndTick;
        public int NextPanicDecisionTick;

        /// <summary>The soonest a cruel person will heave another person out of their way (not the door shoving in <see cref="AgentDoorMemory"/>).</summary>
        public int NextShoveTick;
    }

    internal sealed class AgentHearing
    {
        public LogicalPosition SoundPoint;
        public bool HasSoundPoint;
        public int InvestigateStartTick;
    }

    internal sealed class AgentDoorMemory
    {
        public AgentDoorMemory(int doorCount)
        {
            AvoidUntilTick = new int[doorCount];
            FoundShut = new bool[doorCount];
        }

        /// <summary>The door being run for, or -1.</summary>
        public int ExitDoorIndex = -1;

        /// <summary>The room they are heading at that door from, so approach and target points work from either side.</summary>
        public int ApproachRoom = -1;

        /// <summary>The room they were in last tick, or -1; a change is the moment to think about the door behind them.</summary>
        public int CurrentRoom = -1;

        /// <summary>
        /// A doorway this person may walk through that is not a way out they
        /// are running for: a calm person strolling into the next room, say.
        ///
        /// These used to be the same field, and that is why a calm person could
        /// never leave the room they started in. Only somebody heading for a
        /// way out was allowed into a doorway, and a calm person is not heading
        /// for one, so every door was a wall to them and the building read as
        /// four sealed boxes rather than one place.
        /// </summary>
        public int StrollDoorIndex = -1;

        /// <summary>Any open doorway will do, because they are on their way somewhere.</summary>
        public const int AnyDoorway = -2;

        /// <summary>Per door: the tick until which this person will not try it again.</summary>
        public readonly int[] AvoidUntilTick;

        /// <summary>Per door: they have stood at it and it would not open, so they stop counting on it.</summary>
        public readonly bool[] FoundShut;

        /// <summary>Until this tick they stand aside beside their open door, letting whoever is lined up with it through first.</summary>
        public int GiveWayUntilTick;

        public ulong AttemptEventId;
        public int NextShoveTick;

        /// <summary>Their AgentEscaped event, once they are out: anyone they drag out is rescued because of it.</summary>
        public ulong EscapedEventId;
    }

    internal sealed class AgentLeading
    {
        /// <summary>The leader (agent index) they are following, or -1.</summary>
        public int FollowingIndex = -1;

        /// <summary>They stop following at this tick unless called on again.</summary>
        public int FollowUntilTick;

        /// <summary>When they may next look around and form a plan.</summary>
        public int NextPlanTick;

        /// <summary>A door they were sent to break down, or -1, and until when.</summary>
        public int OrderedDoor = -1;
        public int OrderedUntilTick;

        /// <summary>The shout that set them on, so what follows can name its cause.</summary>
        public ulong OrderEventId;

        /// <summary>How many people are following this person at the moment (presentation only).</summary>
        public int LedCount;
    }

    internal sealed class AgentBarricade
    {
        /// <summary>The door they are wedging something against, or -1.</summary>
        public int DoorIndex = -1;

        /// <summary>If it is not done by then, they abandon it and run.</summary>
        public int GiveUpTick;
    }

    internal sealed class AgentAlarm
    {
        /// <summary>The fire alarm they are walking over to hit, or -1.</summary>
        public int AlarmIndex = -1;
    }

    internal sealed class AgentSitting
    {
        /// <summary>The chair they are on, or walking to, or -1.</summary>
        public int ChairIndex = -1;

        /// <summary>True once they are actually on it.</summary>
        public bool OnIt;

        /// <summary>
        /// When they would get up of their own accord. Kept apart from the
        /// activity timer, so turning in the seat to look at a noise does not
        /// cut short how long they meant to sit.
        /// </summary>
        public int SitUntilTick;
    }

    internal sealed class AgentHelp
    {
        /// <summary>The person (agent index) being helped, or -1.</summary>
        public int TargetIndex = -1;

        /// <summary>When the shaking or grabbing is done; 0 while still on the way.</summary>
        public int WorkEndTick;

        /// <summary>If they have not reached the person by then, they give up.</summary>
        public int GiveUpTick;

        /// <summary>Someone frozen for good they could not shake awake, and will not try again.</summary>
        public int GaveUpOnIndex = -1;

        public ulong GrabEventId;

        /// <summary>The door they are dragging someone toward, or -1.</summary>
        public int DragDoor = -1;

        /// <summary>Where they stood before this tick's move, so a blocked drag can undo it.</summary>
        public LogicalPosition PositionBeforeMove;

        /// <summary>
        /// How many ticks running they have hauled somebody and got nowhere.
        /// Counted here rather than on the body, because a dragger takes a step
        /// that is accepted and then undone when the person behind them will
        /// not fit; the body's own blocked count is cleared by that accepted
        /// step, so it never climbs and they would strain for ever.
        /// </summary>
        public int StuckTicks;
    }

    internal sealed class AgentCarry
    {
        /// <summary>The item (physical-object index) being fetched or carried, or -1.</summary>
        public int ItemIndex = -1;

        /// <summary>The item is in their arms, not just being walked to.</summary>
        public bool Holding;

        /// <summary>
        /// It is their own — a bag or a briefcase they walked in with, not
        /// something they are tidying away. They keep hold of it while calm and
        /// only let go of it when something frightens them.
        /// </summary>
        public bool OwnsIt;

        /// <summary>
        /// Until this tick, they have an extinguisher in mind: somebody put one
        /// down in front of them and they have seen it. While it lasts they
        /// need less nerve than usual to go and take it, which is what makes
        /// the player's card feel like an offer rather than scenery.
        /// </summary>
        public int SawAnExtinguisherUntilTick;
    }

    internal sealed class AgentBurning
    {
        public bool IsBurning;

        /// <summary>The tick they collapse and are lost.</summary>
        public int EndTick;

        /// <summary>The AgentCaughtFire event: the cause of their end, and of anyone they set alight.</summary>
        public ulong EventId;

        public int NextTurnTick;
        public int NextScreamTick;
    }

    /// <summary>
    /// What a behaviour wants the body to do this tick. Behaviours return
    /// one; <see cref="Locomotion.ApplyBody"/> carries it out.
    /// </summary>
    internal readonly struct MotorIntent
    {
        public MotorIntent(int goalHeading, int goalSpeed, int turnRate, int acceleration)
        {
            GoalHeading = goalHeading;
            GoalSpeed = goalSpeed;
            TurnRate = turnRate;
            Acceleration = acceleration;
        }

        public int GoalHeading { get; }
        public int GoalSpeed { get; }
        public int TurnRate { get; }
        public int Acceleration { get; }
    }
}
