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
        [UnityEngine.SerializeField] private int leadership;

        public AgentTraitValues(
            int strength,
            int speed,
            int bravery,
            int compassion,
            int evil,
            int nervousness,
            int leadership = Ordinary)
        {
            this.strength = strength;
            this.speed = speed;
            this.bravery = bravery;
            this.compassion = compassion;
            this.evil = evil;
            this.nervousness = nervousness;
            this.leadership = leadership;
        }

        /// <summary>
        /// The same person with a different strength. Used by the player's
        /// Beefcake power; everything that reads strength picks it up on the
        /// next tick, because traits are read when used and never cached.
        /// </summary>
        public AgentTraitValues WithStrength(int newStrength) =>
            new AgentTraitValues(newStrength, speed, bravery, compassion, evil, nervousness, leadership);

        public static AgentTraitValues AllOrdinary =>
            new AgentTraitValues(Ordinary, Ordinary, Ordinary, Ordinary, Ordinary, Ordinary, Ordinary);

        public int Strength => strength;
        public int Speed => speed;
        public int Bravery => bravery;
        public int Compassion => compassion;
        public int Evil => evil;
        public int Nervousness => nervousness;

        /// <summary>How readily other people do what this person says.</summary>
        public int Leadership => leadership;

        public bool IsValid =>
            InRange(strength) && InRange(speed) && InRange(bravery) &&
            InRange(compassion) && InRange(evil) && InRange(nervousness) && InRange(leadership);

        private static bool InRange(int value) => value >= Minimum && value <= Maximum;

        public bool Equals(AgentTraitValues other) =>
            strength == other.strength && speed == other.speed && bravery == other.bravery &&
            compassion == other.compassion && evil == other.evil && nervousness == other.nervousness &&
            leadership == other.leadership;

        public override bool Equals(object obj) => obj is AgentTraitValues other && Equals(other);

        public override int GetHashCode() =>
            (((((strength * 11 + speed) * 11 + bravery) * 11 + compassion) * 11 + evil) * 11 + nervousness) * 11 + leadership;

        public override string ToString() =>
            $"Str {strength} Spd {speed} Brv {bravery} Cmp {compassion} Evl {evil} Nrv {nervousness} Ldr {leadership}";
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
        Bumped,

        /// <summary>A fire alarm went off. Appended only.</summary>
        Alarm
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

        /// <summary>Running along behind whoever is leading them.</summary>
        Following,

        /// <summary>Going for an extinguisher, and spraying it at the fire.</summary>
        FetchingExtinguisher,
        Spraying,

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
        Dragging,

        // Appended only: an activity's number is part of the replay fingerprint.

        /// <summary>On the way to a fire alarm, and hitting it.</summary>
        GoingToAlarm,
        PullingAlarm,

        /// <summary>Fetching something to wedge a door with, carrying it there, and wedging it.</summary>
        FetchingBarricade,
        CarryingBarricade,
        Barricading,

        /// <summary>Heaving whatever is wedged in a doorway out of the way.</summary>
        ShovingObstruction
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

        /// <summary>A burning square put out by an extinguisher.</summary>
        FireDoused,

        /// <summary>Picking up an extinguisher, spraying it, and running it dry (target: the extinguisher).</summary>
        AgentTookExtinguisher,
        ExtinguisherSprayed,
        ExtinguisherEmptied,

        /// <summary>Someone knocked off their feet by the jet (target: the person hit).</summary>
        AgentBlasted,

        /// <summary>Someone on fire hosed down (target: the person put out).</summary>
        AgentDoused,

        /// <summary>
        /// Taking charge: calling people on (no target), sending someone at a
        /// door, or sending someone for an extinguisher (target: the person
        /// told). The shout carries as far as its strength.
        /// </summary>
        LeaderCalledPeopleOn,
        LeaderOrderedDoorBroken,
        LeaderOrderedFireFought,

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
        AgentRescued,

        // Appended only, never inserted: an event type's number is part of the
        // replay fingerprint, so renumbering would silently invalidate every
        // recorded run.

        /// <summary>Somebody cruel took hold of the person in their way and heaved them aside (target: the person shoved).</summary>
        AgentShoved,

        /// <summary>Somebody hit a fire alarm (target: the alarm).</summary>
        AlarmPulled,

        /// <summary>An alarm ringing, which is the noise everybody hears (source: the alarm).</summary>
        AlarmRang,

        /// <summary>A chair or table smashed by something hitting it hard (source: the thing that broke, target: what hit it).</summary>
        ObjectBroke,

        /// <summary>
        /// Something electrical going off: it flings whatever is near it about,
        /// knocks people down, and sets the floor alight (source: the thing).
        /// </summary>
        ObjectExploded,

        /// <summary>Something came to rest in a doorway and jammed the door (source: the thing, target: the door).</summary>
        DoorBlocked,

        /// <summary>Whatever was jamming a door is out of the way again (source: the thing, target: the door).</summary>
        DoorUnblocked,

        /// <summary>Somebody wedged something against a door on purpose (source: the person, target: the door).</summary>
        AgentBarricadedDoor,

        /// <summary>Somebody strong heaved an obstruction out of a doorway (source: the person, target: the thing).</summary>
        AgentShovedObstruction,

        // The player's cards. Each is a root event, because the player is the
        // cause, and its strength is the influence it cost.

        /// <summary>Beefcake played on somebody (target: the person made strong).</summary>
        PowerBeefcake,

        /// <summary>A fire started by the player, at the place they pointed at.</summary>
        PowerSpawnedFire,

        /// <summary>An extinguisher put on the floor by the player (target: the bottle).</summary>
        PowerSpawnedExtinguisher,

        /// <summary>A wall blown open by the player (source and target: the hole itself).</summary>
        PowerBlastedWall
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
        Laptop,

        /// <summary>A fire extinguisher: a few seconds of spray in the bottle.</summary>
        Extinguisher,

        // Appended only: a kind's number indexes the scenario's table of kinds.

        /// <summary>A briefcase: heavier and harder than a bag, so it hurts more when it hits somebody.</summary>
        Briefcase,

        /// <summary>A microwave oven on a counter: when the flames reach it, it goes off with a bang.</summary>
        Microwave,

        /// <summary>A wall socket: it never moves and never burns, but it spits sparks and pops.</summary>
        WallSocket
    }

    /// <summary>
    /// What the player did. Append new values only: a command type's number is
    /// part of the replay fingerprint.
    /// </summary>
    public enum PlayerCommandType
    {
        /// <summary>Locked becomes unlocked; unlocked becomes open; open closes (unless someone is in the doorway). Broken stays broken.</summary>
        ClickDoor,

        // The cards. Each one spends influence, and each names either a person
        // or a place. Appended only.

        /// <summary>Beefcake: the named person becomes as strong as anyone can be, for good.</summary>
        PlayBeefcake,

        /// <summary>Start a fire on the floor square under the named place.</summary>
        SpawnFire,

        /// <summary>Stand a full fire extinguisher on the floor at the named place.</summary>
        SpawnExtinguisher,

        /// <summary>TNT: blow a hole through the wall nearest the named place.</summary>
        BlastWall
    }

    /// <summary>
    /// One player action, already turned into simulation data. It is consumed
    /// at the start of its target tick; commands sharing a tick run in
    /// sequence order.
    /// <para>
    /// A command names either a thing (<see cref="TargetId"/>, as a door click
    /// does) or a place (<see cref="Point"/>, in whole millimetres, as a power
    /// aimed at the floor or a wall does). Screen positions, rays and colliders
    /// are presentation's business: they are turned into one of these two
    /// before the command is queued, so replaying the queue never depends on a
    /// camera.
    /// </para>
    /// </summary>
    public readonly struct PlayerCommand
    {
        public PlayerCommand(
            int targetTick,
            long sequence,
            PlayerCommandType commandType,
            SimulationId targetId,
            LogicalPosition point = default)
        {
            TargetTick = targetTick;
            Sequence = sequence;
            CommandType = commandType;
            TargetId = targetId;
            Point = point;
        }

        public int TargetTick { get; }
        public long Sequence { get; }
        public PlayerCommandType CommandType { get; }

        /// <summary>The thing this command is aimed at, or the default ID for a command aimed at a place.</summary>
        public SimulationId TargetId { get; }

        /// <summary>The place this command is aimed at, in whole millimetres, or the origin for a command aimed at a thing.</summary>
        public LogicalPosition Point { get; }
    }
}
