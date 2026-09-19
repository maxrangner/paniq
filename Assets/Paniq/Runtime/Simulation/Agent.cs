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

        public bool IsParticipating => Participation == AgentParticipation.Participating;

        /// <summary>Lying on the floor or getting up.</summary>
        public bool IsDown => Body.State == AgentBodyState.Fallen || Body.State == AgentBodyState.GettingUp;

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
                Traits);
        }
    }

    internal sealed class AgentBody
    {
        public LogicalPosition Position;

        /// <summary>Whole degrees clockwise from north.</summary>
        public int Heading;

        /// <summary>Millimetres per tick.</summary>
        public int Speed;

        /// <summary>Ticks in a row this person wanted to move and could not.</summary>
        public int BlockedTicks;

        public AgentBodyState State;
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
        }

        /// <summary>The door being run for, or -1.</summary>
        public int ExitDoorIndex = -1;

        /// <summary>Per door: the tick until which this person will not try it again.</summary>
        public readonly int[] AvoidUntilTick;

        public ulong AttemptEventId;
        public int NextShoveTick;
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
