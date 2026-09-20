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

    /// <summary>
    /// A stable, opaque identifier for any simulation entity: a person, a
    /// door, a box or a hazard. Compare and store it; never read meaning into it.
    /// </summary>
    [Serializable]
    public struct SimulationId : IEquatable<SimulationId>, IComparable<SimulationId>
    {
        [UnityEngine.SerializeField] private ulong value;

        public SimulationId(ulong value)
        {
            this.value = value;
        }

        public ulong Value => value;
        public bool Equals(SimulationId other) => value == other.value;
        public override bool Equals(object obj) => obj is SimulationId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public int CompareTo(SimulationId other) => value.CompareTo(other.value);
        public override string ToString() => value.ToString();
        public static bool operator ==(SimulationId left, SimulationId right) => left.Equals(right);
        public static bool operator !=(SimulationId left, SimulationId right) => !left.Equals(right);
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

    /// <summary>
    /// A person's personality: six traits from 0 to 10, where 5 is an
    /// ordinary person. Traits shape how they move and react (see
    /// <c>TraitEffects</c>). Authored per person in the scenario, or drawn
    /// from the seed.
    /// </summary>
    [Serializable]
    public struct AgentTraitValues : IEquatable<AgentTraitValues>
    {
        public const int Minimum = 0;
        public const int Maximum = 10;
        public const int Ordinary = 5;

        [UnityEngine.SerializeField] private int strength;
        [UnityEngine.SerializeField] private int speed;
        [UnityEngine.SerializeField] private int bravery;
        [UnityEngine.SerializeField] private int compassion;
        [UnityEngine.SerializeField] private int evil;
        [UnityEngine.SerializeField] private int nervousness;

        public AgentTraitValues(int strength, int speed, int bravery, int compassion, int evil, int nervousness)
        {
            this.strength = strength;
            this.speed = speed;
            this.bravery = bravery;
            this.compassion = compassion;
            this.evil = evil;
            this.nervousness = nervousness;
        }

        public static AgentTraitValues AllOrdinary => new AgentTraitValues(Ordinary, Ordinary, Ordinary, Ordinary, Ordinary, Ordinary);

        public int Strength => strength;
        public int Speed => speed;
        public int Bravery => bravery;
        public int Compassion => compassion;
        public int Evil => evil;
        public int Nervousness => nervousness;

        public bool IsValid =>
            InRange(strength) && InRange(speed) && InRange(bravery) &&
            InRange(compassion) && InRange(evil) && InRange(nervousness);

        private static bool InRange(int value) => value >= Minimum && value <= Maximum;

        public bool Equals(AgentTraitValues other) =>
            strength == other.strength && speed == other.speed && bravery == other.bravery &&
            compassion == other.compassion && evil == other.evil && nervousness == other.nervousness;

        public override bool Equals(object obj) => obj is AgentTraitValues other && Equals(other);

        public override int GetHashCode() =>
            ((((strength * 11 + speed) * 11 + bravery) * 11 + compassion) * 11 + evil) * 11 + nervousness;

        public override string ToString() =>
            $"Str {strength} Spd {speed} Brv {bravery} Cmp {compassion} Evl {evil} Nrv {nervousness}";
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
        ForcingDoor,

        /// <summary>On fire: running around wildly until they collapse.</summary>
        Burning,

        /// <summary>Going to sit down, sitting on a chair, and getting back up off it.</summary>
        GoingToSit,
        Sitting,
        StandingUp,

        /// <summary>Tidying up: walking over to an item, picking it up, carrying it off and setting it down.</summary>
        FetchingItem,
        PickingUp,
        CarryingItem,
        SettingDown,

        /// <summary>Helping: shaking someone frozen with fear, getting a grip on someone knocked out, or dragging them.</summary>
        ShakingAwake,
        Grabbing,
        Dragging
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
        GettingUp,

        /// <summary>Knocked out cold: lying still for several seconds before coming to.</summary>
        Unconscious
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
        BoxesCollided,
        AgentPassedOut,
        AgentCameTo,
        DoorBrokenDown,
        AgentCaughtFire,
        ObjectCaughtFire,
        ObjectBurntOut,
        ItemThrown,
        ItemDropped,
        DoorClosed,
        DoorLocked,
        AgentShookAwake,
        AgentGrabbed,
        AgentDropped,
        AgentRescued
    }

    /// <summary>A box, chair or table: untouched (maybe heating up), in flames, or burnt out and charred.</summary>
    public enum ObjectBurnState
    {
        Intact,
        Burning,
        Burnt
    }

    /// <summary>Which wall of the room a door sits in. North is +Z, east is +X.</summary>
    public enum WallSide
    {
        North,
        East,
        South,
        West
    }

    /// <summary>
    /// Locked and unlocked doors are both shut; people can only tell them
    /// apart by trying. A broken door has been smashed open and can never
    /// shut again.
    /// </summary>
    public enum DoorState
    {
        Locked,
        Unlocked,
        Open,
        Broken
    }

    /// <summary>
    /// The kinds of loose object the physical-object system knows. How far
    /// each slides and how it burns comes from the scenario's table of kinds.
    /// </summary>
    public enum PhysicsObjectKind
    {
        Box,
        Chair,

        /// <summary>An office chair on castors: it rolls a long way when kicked.</summary>
        OfficeChair,

        /// <summary>A waste-paper basket: light, and it catches quickly.</summary>
        WasteBin,

        /// <summary>A potted plant: heavy, and too green to burn.</summary>
        PottedPlant,

        /// <summary>A bag left on the floor: light, and easy to trip over.</summary>
        Bag,

        /// <summary>A laptop: small, hard, and it skitters across the floor.</summary>
        Laptop
    }

    public enum PlayerCommandType
    {
        /// <summary>Locked becomes unlocked; unlocked becomes open; open closes (unless someone is in the doorway). Broken stays broken.</summary>
        ClickDoor
    }

    /// <summary>
    /// One player action, already turned into simulation data. It is consumed
    /// at the start of its target tick; commands sharing a tick run in
    /// sequence order.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public PlayerCommand(int targetTick, long sequence, PlayerCommandType commandType, SimulationId targetId)
        {
            TargetTick = targetTick;
            Sequence = sequence;
            CommandType = commandType;
            TargetId = targetId;
        }

        public int TargetTick { get; }
        public long Sequence { get; }
        public PlayerCommandType CommandType { get; }
        public SimulationId TargetId { get; }
    }
}
