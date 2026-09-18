using System;

namespace Paniq.Simulation
{
    /// <summary>Integer millimetres on the simulation's flat XZ ground plane.</summary>
    [Serializable]
    public struct LogicalPosition : IEquatable<LogicalPosition>
    {
        [UnityEngine.SerializeField] private int x;
        [UnityEngine.SerializeField] private int z;

        public LogicalPosition(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public int X => x;
        public int Z => z;

        public static LogicalPosition operator +(LogicalPosition position, LogicalPosition displacement)
        {
            return new LogicalPosition(checked(position.x + displacement.x), checked(position.z + displacement.z));
        }

        public static LogicalPosition operator -(LogicalPosition left, LogicalPosition right)
        {
            return new LogicalPosition(checked(left.x - right.x), checked(left.z - right.z));
        }

        public bool Equals(LogicalPosition other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is LogicalPosition other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ z;
        public override string ToString() => $"({x}, {z}) mm";

        public static long DistanceSquared(LogicalPosition left, LogicalPosition right)
        {
            long dx = (long)left.x - right.x;
            long dz = (long)left.z - right.z;
            return checked(dx * dx + dz * dz);
        }
    }

    /// <summary>A stable, opaque identifier for simulation entities.</summary>
    [Serializable]
    public struct StableAgentId : IEquatable<StableAgentId>, IComparable<StableAgentId>
    {
        [UnityEngine.SerializeField] private ulong value;

        public StableAgentId(ulong value)
        {
            this.value = value;
        }

        public ulong Value => value;
        public bool Equals(StableAgentId other) => value == other.value;
        public override bool Equals(object obj) => obj is StableAgentId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public int CompareTo(StableAgentId other) => value.CompareTo(other.value);
        public override string ToString() => value.ToString();
        public static bool operator ==(StableAgentId left, StableAgentId right) => left.Equals(right);
        public static bool operator !=(StableAgentId left, StableAgentId right) => !left.Equals(right);
    }

    [Serializable]
    public struct LogicalBounds
    {
        [UnityEngine.SerializeField] private int minX;
        [UnityEngine.SerializeField] private int maxX;
        [UnityEngine.SerializeField] private int minZ;
        [UnityEngine.SerializeField] private int maxZ;

        public LogicalBounds(int minX, int maxX, int minZ, int maxZ)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minZ = minZ;
            this.maxZ = maxZ;
        }

        public int MinX => minX;
        public int MaxX => maxX;
        public int MinZ => minZ;
        public int MaxZ => maxZ;
        public LogicalPosition Centre => new LogicalPosition((minX + maxX) / 2, (minZ + maxZ) / 2);

        public bool ContainsCircle(LogicalPosition position, int radiusMillimetres)
        {
            return position.X >= minX + radiusMillimetres &&
                   position.X <= maxX - radiusMillimetres &&
                   position.Z >= minZ + radiusMillimetres &&
                   position.Z <= maxZ - radiusMillimetres;
        }

        /// <summary>The point inside these bounds nearest to <paramref name="position"/>.</summary>
        public LogicalPosition ClosestPoint(LogicalPosition position)
        {
            return new LogicalPosition(
                Math.Max(minX, Math.Min(maxX, position.X)),
                Math.Max(minZ, Math.Min(maxZ, position.Z)));
        }

        public long DistanceSquaredTo(LogicalPosition position)
        {
            return LogicalPosition.DistanceSquared(position, ClosestPoint(position));
        }
    }

    /// <summary>Authoring-only starting facing; the simulation stores whole-degree headings.</summary>
    public enum CardinalDirection
    {
        North,
        East,
        South,
        West
    }

    public enum AgentParticipation
    {
        Participating,
        NoLongerParticipating
    }

    public enum AgentFearState
    {
        Calm,
        Alert,
        Scared
    }

    public enum AgentAlertSource
    {
        None,
        Visual,
        Yell,
        Bumped
    }

    /// <summary>What an agent is currently choosing to do. Calm and panic activities are separate.</summary>
    public enum AgentActivityState
    {
        Standing,
        LookingAround,
        Strolling,
        Socialising,
        Reacting,
        Fleeing,
        Hesitating,
        Investigating,
        Frozen,
        OpeningDoor,
        TryingDoor,
        ForcingDoor
    }

    /// <summary>Seeded personality: how this person reacts once scared.</summary>
    public enum AgentPanicTemperament
    {
        Runner,
        FreezeThenRun,
        FreezeForever
    }

    /// <summary>Whether the body is under the person's control. Separate from what they intend to do.</summary>
    public enum AgentBodyState
    {
        Upright,
        Staggering,
        Fallen,
        GettingUp
    }

    public enum AgentTerminalOutcome
    {
        Unresolved,
        Lost,
        Escaped
    }

    public enum FireReactionEventType
    {
        FireActivated,
        FireSpread,
        AgentAlerted,
        AgentYelled,
        AgentScared,
        AgentLost,
        AgentNoticedSound,
        AgentsCollided,
        AgentKnockedDown,
        AgentTripped,
        AgentGotUp,
        AgentFroze,
        AgentUnfroze,
        DoorUnlocked,
        DoorOpened,
        AgentTriedDoor,
        AgentForcedDoor,
        AgentGaveUpOnDoor,
        AgentEscaped,
        BoxBumped,
        BoxHitAgent,
        BoxesCollided
    }

    /// <summary>Which wall of the room a door sits in. North is +Z, east is +X.</summary>
    public enum WallSide
    {
        North,
        East,
        South,
        West
    }

    /// <summary>Locked and unlocked doors are both shut; people can only tell them apart by trying.</summary>
    public enum DoorState
    {
        Locked,
        Unlocked,
        Open
    }

    /// <summary>The shapes the physical-object system knows. Only boxes so far.</summary>
    public enum PhysicsObjectKind
    {
        Box
    }

    public enum PlayerCommandType
    {
        /// <summary>Locked becomes unlocked; unlocked becomes open; open stays open.</summary>
        ClickDoor
    }

    /// <summary>
    /// One player action, already turned into simulation data. It is consumed
    /// at the start of its target tick; commands sharing a tick run in
    /// sequence order.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public PlayerCommand(int targetTick, long sequence, PlayerCommandType commandType, StableAgentId targetId)
        {
            TargetTick = targetTick;
            Sequence = sequence;
            CommandType = commandType;
            TargetId = targetId;
        }

        public int TargetTick { get; }
        public long Sequence { get; }
        public PlayerCommandType CommandType { get; }
        public StableAgentId TargetId { get; }
    }
}
