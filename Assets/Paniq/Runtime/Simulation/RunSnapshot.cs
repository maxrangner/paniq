using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>One burning grid square. Cells are listed in the order they ignited.</summary>
    public readonly struct FireCellSnapshot
    {
        public FireCellSnapshot(int cellX, int cellZ, LogicalBounds bounds, int ignitionTick, ulong eventId, int outTick = 0)
        {
            CellX = cellX;
            CellZ = cellZ;
            Bounds = bounds;
            IgnitionTick = ignitionTick;
            EventId = eventId;
            OutTick = outTick;
        }

        public int CellX { get; }
        public int CellZ { get; }
        public LogicalBounds Bounds { get; }
        public LogicalPosition Centre => Bounds.Centre;
        public int IgnitionTick { get; }

        /// <summary>The tick this square was put out, or 0 while it still burns.</summary>
        public int OutTick { get; }

        public bool IsOut => OutTick != 0;

        /// <summary>The same square, put out at this tick.</summary>
        internal FireCellSnapshot PutOut(int tick)
        {
            return new FireCellSnapshot(CellX, CellZ, Bounds, IgnitionTick, EventId, tick);
        }

        /// <summary>The FireActivated or FireSpread event that lit this cell.</summary>
        public ulong EventId { get; }
    }

    public readonly struct AgentSnapshot
    {
        public AgentSnapshot(
            SimulationId agentId,
            LogicalPosition position,
            AgentParticipation participation,
            AgentFearState fearState,
            AgentAlertSource alertSource,
            AgentTerminalOutcome outcome,
            AgentActivityState activityState,
            int headingDegrees,
            int speedMillimetresPerTick,
            int calmSpeedMillimetresPerTick,
            int panicSpeedMillimetresPerTick,
            int reactionDelayTicks,
            AgentPanicTemperament temperament,
            AgentBodyState bodyState,
            AgentTraitValues traits,
            bool isBurning,
            bool isLeading = false,
            BodyPose pose = default,
            int seatedPercent = 0,
            int groupId = -1)
        {
            GroupId = groupId;
            Pose = pose;
            SeatedPercent = seatedPercent;
            Traits = traits;
            IsBurning = isBurning;
            IsLeading = isLeading;
            Temperament = temperament;
            BodyState = bodyState;
            AgentId = agentId;
            Position = position;
            Participation = participation;
            FearState = fearState;
            AlertSource = alertSource;
            Outcome = outcome;
            ActivityState = activityState;
            HeadingDegrees = headingDegrees;
            SpeedMillimetresPerTick = speedMillimetresPerTick;
            CalmSpeedMillimetresPerTick = calmSpeedMillimetresPerTick;
            PanicSpeedMillimetresPerTick = panicSpeedMillimetresPerTick;
            ReactionDelayTicks = reactionDelayTicks;
        }

        public SimulationId AgentId { get; }

        /// <summary>
        /// How high their feet are off the floor and how their body is turned,
        /// as the physics engine left it: standing, sprawled on their back,
        /// flying through the air from a blast.
        /// </summary>
        public BodyPose Pose { get; }
        public LogicalPosition Position { get; }
        public AgentParticipation Participation { get; }
        public AgentFearState FearState { get; }
        public AgentAlertSource AlertSource { get; }
        public AgentTerminalOutcome Outcome { get; }
        public AgentActivityState ActivityState { get; }

        /// <summary>Whole degrees clockwise from north (+Z); 90 is east (+X).</summary>
        public int HeadingDegrees { get; }

        /// <summary>Current speed; the distance moved this tick if the move was accepted.</summary>
        public int SpeedMillimetresPerTick { get; }

        /// <summary>This agent's seeded walking pace.</summary>
        public int CalmSpeedMillimetresPerTick { get; }

        /// <summary>This agent's seeded sprinting pace.</summary>
        public int PanicSpeedMillimetresPerTick { get; }

        public int ReactionDelayTicks { get; }

        /// <summary>This agent's seeded way of panicking.</summary>
        public AgentPanicTemperament Temperament { get; }

        /// <summary>Upright, or staggering, lying on the floor, or getting up.</summary>
        public AgentBodyState BodyState { get; }

        /// <summary>Strength, speed, bravery, compassion, evil and nervousness, 0–10.</summary>
        public AgentTraitValues Traits { get; }

        /// <summary>On fire and running around wildly until they collapse.</summary>
        public bool IsBurning { get; }

        /// <summary>Somebody is following this person right now.</summary>
        public bool IsLeading { get; }

        /// <summary>The group a "Stick together" throw bound them to, or -1.</summary>
        public int GroupId { get; }

        public bool IsDown => BodyState == AgentBodyState.Fallen || BodyState == AgentBodyState.GettingUp ||
                              BodyState == AgentBodyState.Unconscious;

        /// <summary>
        /// How far into a chair they are, nought to a hundred: nought on their
        /// feet, a hundred sat down, in between while lowering onto the seat
        /// or rising from it. The display lifts the body onto the seat by this
        /// much, so sitting down and getting up read as a movement.
        /// </summary>
        public int SeatedPercent { get; }
    }

    /// <summary>A door as the player sees it: where its gap is and whether it is locked, unlocked or open.</summary>
    public readonly struct DoorSnapshot
    {
        public DoorSnapshot(SimulationId doorId, WallSide side, LogicalPosition centre, int widthMillimetres, DoorState state,
            int damagePercent, int scorchPercent = 0, bool isHole = false, bool isBlocked = false,
            bool leadsOutside = false,
            int openSide = 0, bool isJammed = false, bool swings = false, bool isHeld = false, bool isPiled = false)
        {
            IsHeld = isHeld;
            IsPiled = isPiled;
            Swings = swings;
            IsHole = isHole;
            IsBlocked = isBlocked;
            OpenSide = openSide;
            IsJammed = isJammed;
            LeadsOutside = leadsOutside;
            DamagePercent = damagePercent;
            ScorchPercent = scorchPercent;
            DoorId = doorId;
            Side = side;
            Centre = centre;
            WidthMillimetres = widthMillimetres;
            State = state;
        }

        public SimulationId DoorId { get; }
        public WallSide Side { get; }

        /// <summary>The middle of the door gap, on the wall line.</summary>
        public LogicalPosition Centre { get; }

        public int WidthMillimetres { get; }
        public DoorState State { get; }

        /// <summary>How close a battered door is to breaking, 0–100.</summary>
        /// <summary>How far shoving has got toward breaking it, 0-100.</summary>
        public int DamagePercent { get; }

        /// <summary>How far standing in the flames has got toward burning it through, 0-100.</summary>
        public int ScorchPercent { get; }

        /// <summary>Whichever is further along, for drawing a failing door.</summary>
        public int FailingPercent => DamagePercent > ScorchPercent ? DamagePercent : ScorchPercent;

        /// <summary>
        /// A hole blasted through the wall rather than a door in a frame: drawn as
        /// a ragged gap, with no leaf to swing and nothing to click.
        /// </summary>
        public bool IsHole { get; }

        /// <summary>Something is wedged in the gap, on one side of it or both.</summary>
        public bool IsBlocked { get; }

        /// <summary>
        /// Wedged so that the leaf cannot swing either way: this door really will
        /// not open, and a click on it does nothing until the obstruction shifts.
        /// </summary>
        public bool IsJammed { get; }

        /// <summary>
        /// Which way the leaf stands open: +1 out of the room whose wall holds
        /// it, -1 into that room, 0 while it is shut.
        /// </summary>
        public int OpenSide { get; }

        /// <summary>It leads out of the building rather than into the next room.</summary>
        public bool LeadsOutside { get; }

        /// <summary>
        /// A pair of swing doors: two leaves that push open ahead of whoever
        /// walks through and swing shut behind them. Always open as far as
        /// the rules are concerned, nothing to click, and only the fire has
        /// to burn its way through.
        /// </summary>
        public bool Swings { get; }

        /// <summary>The player has a hand on it, holding it shut (prototype 3): nobody opens it until they let go.</summary>
        public bool IsHeld { get; }

        /// <summary>
        /// The tower of boxes is lying across this archway (prototype 3):
        /// shut for people and fire, with no leaf, until enough of the boxes
        /// are carried off, thrown clear or burnt.
        /// </summary>
        public bool IsPiled { get; }
    }

    /// <summary>A table: where it stands and whether it is heating up, burning or burnt out.</summary>
    public readonly struct TableSnapshot
    {
        public TableSnapshot(SimulationId tableId, LogicalBounds bounds, ObjectBurnState burnState,
            int heatPercent, BodyPose pose)
        {
            TableId = tableId;
            Bounds = bounds;
            BurnState = burnState;
            HeatPercent = heatPercent;
            Pose = pose;
        }

        public SimulationId TableId { get; }
        public LogicalBounds Bounds { get; }

        /// <summary>Where it stands and how it is turned: a table is shoved, tipped and flipped like anything else.</summary>
        public BodyPose Pose { get; }
        public ObjectBurnState BurnState { get; }

        /// <summary>How close to catching fire it is, 0–100.</summary>
        public int HeatPercent { get; }
    }

    /// <summary>A loose object on the floor, such as a box.</summary>
    public readonly struct PhysicsObjectSnapshot
    {
        public PhysicsObjectSnapshot(
            SimulationId objectId,
            PhysicsObjectKind kind,
            LogicalPosition position,
            int sizeMillimetres,
            int headingDegrees,
            int speedMillimetresPerTick,
            ObjectBurnState burnState = ObjectBurnState.Intact,
            int heatPercent = 0,
            SimulationId heldBy = default,
            bool thrown = false,
            SimulationId occupiedBy = default,
            bool dormant = false,
            bool wrecked = false,
            bool resting = false,
            BodyPose pose = default)
        {
            Pose = pose;
            Resting = resting;
            Wrecked = wrecked;
            Dormant = dormant;
            OccupiedBy = occupiedBy;
            HeldBy = heldBy;
            Thrown = thrown;
            BurnState = burnState;
            HeatPercent = heatPercent;
            ObjectId = objectId;
            Kind = kind;
            Position = position;
            SizeMillimetres = sizeMillimetres;
            HeadingDegrees = headingDegrees;
            SpeedMillimetresPerTick = speedMillimetresPerTick;
        }

        public SimulationId ObjectId { get; }
        public PhysicsObjectKind Kind { get; }
        public LogicalPosition Position { get; }
        public int SizeMillimetres { get; }

        /// <summary>Which way the object is turned; it spins when hit off-centre.</summary>
        public int HeadingDegrees { get; }

        public int SpeedMillimetresPerTick { get; }

        public ObjectBurnState BurnState { get; }

        /// <summary>How close to catching fire it is, 0–100.</summary>
        public int HeatPercent { get; }

        /// <summary>
        /// Not in the world yet: a spare kept aside for the player to put down
        /// with a card. It has no position anybody can reach, touches nothing,
        /// and must not be drawn.
        /// </summary>
        public bool Dormant { get; }

        /// <summary>Smashed: wreckage on the floor rather than a thing in one piece.</summary>
        public bool Wrecked { get; }

        /// <summary>
        /// Standing on a table or on another object rather than on the floor: a
        /// laptop on a desk, the upper box of a stacked pair. The display draws
        /// it at the height of whatever holds it up.
        /// </summary>
        public bool Resting { get; }

        /// <summary>Who is carrying it (a zero ID when it is on the floor).</summary>
        public SimulationId HeldBy { get; }

        public bool IsHeld => HeldBy.Value != 0UL;

        /// <summary>Who is sitting on it (a zero ID when nobody is).</summary>
        public SimulationId OccupiedBy { get; }

        public bool IsSatOn => OccupiedBy.Value != 0UL;

        /// <summary>Thrown and still flying.</summary>
        public bool Thrown { get; }

        /// <summary>
        /// How high it is and how it is turned in all three dimensions, as the
        /// physics engine left it: lying on its side, upside down, in mid-air.
        /// Empty for a thing being carried or not yet in the world.
        /// </summary>
        public BodyPose Pose { get; }

        /// <summary>The same object with its fire state filled in.</summary>
        internal PhysicsObjectSnapshot WithBurn(ObjectBurnState burnState, int heatPercent)
        {
            return new PhysicsObjectSnapshot(ObjectId, Kind, Position, SizeMillimetres, HeadingDegrees,
                SpeedMillimetresPerTick, burnState, heatPercent, HeldBy, Thrown, OccupiedBy, Dormant, Wrecked, Resting, Pose);
        }
    }

    /// <summary>
    /// Where a body is in the air and how it is turned, in whole numbers: its
    /// bottom's height above the floor in millimetres, and its rotation as a
    /// quaternion (the usual four-number way of writing a 3D turn) with each
    /// part scaled by 10000. The height and the base point are the body's own
    /// origin: the spot on its underside it was built up from, which is where
    /// its feet are for a person and the middle of its base for an object.
    /// </summary>
    /// <summary>
    /// A spark on its way along one run of cable: which run, and how far along
    /// it has got in millimetres from the end it started at.
    /// <para>
    /// How far it has travelled is a rule -- it decides when the next socket
    /// pops -- so the view reads it rather than timing its own animation, and
    /// the drawn spark is always exactly where the run has it.
    /// </para>
    /// </summary>
    public readonly struct PowerSparkSnapshot
    {
        public PowerSparkSnapshot(int lineIndex, int travelledMillimetres, bool runsForward)
        {
            LineIndex = lineIndex;
            TravelledMillimetres = travelledMillimetres;
            RunsForward = runsForward;
        }

        /// <summary>Which run of cable, as an index into the scenario's power lines.</summary>
        public int LineIndex { get; }

        /// <summary>How far along that run the spark has crawled, from the end it started at.</summary>
        public int TravelledMillimetres { get; }

        /// <summary>Whether it is travelling from the run's first corner toward its last.</summary>
        public bool RunsForward { get; }
    }

    public readonly struct BodyPose
    {
        public const int RotationScale = 10000;

        public BodyPose(int heightMillimetres, int rotationX, int rotationY, int rotationZ, int rotationW,
            LogicalPosition origin = default)
        {
            Origin = origin;
            HeightMillimetres = heightMillimetres;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            RotationW = rotationW;
        }

        public int HeightMillimetres { get; }

        /// <summary>
        /// Where the body's origin is across the floor. Its position elsewhere
        /// in the snapshot is its middle, which is not the same spot once it
        /// tips: a person lying on the floor has their middle a metre from
        /// their feet.
        /// </summary>
        public LogicalPosition Origin { get; }

        public int RotationX { get; }
        public int RotationY { get; }
        public int RotationZ { get; }
        public int RotationW { get; }

        /// <summary>False for the empty pose: nothing was measured, so draw it the old way.</summary>
        public bool IsKnown => RotationX != 0 || RotationY != 0 || RotationZ != 0 || RotationW != 0;
    }

    /// <summary>
    /// A read-only view of simulation state after one tick, for presentation
    /// and tests. People, doors and boxes are copied; the event log and the
    /// burning cells only ever grow, so the snapshot holds a view of them as
    /// they were at this tick instead of a copy.
    /// </summary>
    /// <summary>
    /// The first <see cref="Count"/> items of an array, read-only, so a
    /// snapshot can hand out "the doors that are placed" or "the cards in
    /// hand" from a buffer sized for the most there could be, without
    /// copying or boxing anything each time it is read.
    /// </summary>
    internal sealed class Prefix<T> : IReadOnlyList<T>
    {
        private T[] items;

        public Prefix(int capacity)
        {
            items = new T[capacity];
        }

        public int Count { get; private set; }

        public T[] Items => items;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(index));
                }

                return items[index];
            }
        }

        /// <summary>Makes room for this many and says that many are in use; what is in them is the caller's to write.</summary>
        public void Resize(int count)
        {
            if (count > items.Length)
            {
                items = new T[System.Math.Max(count, items.Length * 2)];
            }

            Count = count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return items[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Everything the display needs to draw one tick, and nothing the rules
    /// need. A snapshot is a set of buffers sized once for the run and
    /// filled by <see cref="Run.FillSnapshot"/>; the
    /// display keeps two and fills them turn about, so drawing a tick
    /// allocates nothing. <see cref="Run.GetSnapshot"/>
    /// makes and fills a fresh one for anybody who wants to keep it.
    /// </summary>
    public sealed class RunSnapshot
    {
        private readonly AgentSnapshot[] agents;
        private readonly Prefix<DoorSnapshot> doors;
        private readonly PhysicsObjectSnapshot[] physicsObjects;
        private readonly TableSnapshot[] tables;
        private readonly Prefix<PlayerCommandType> hand;
        private IReadOnlyList<FireCellSnapshot> fireCells = System.Array.Empty<FireCellSnapshot>();
        private IReadOnlyList<CausalEvent> events = System.Array.Empty<CausalEvent>();

        /// <summary>What each card costs, indexed by <see cref="PlayerCommandType"/>. Shared with the run; never written.</summary>
        private readonly int[] cardCosts;

        /// <summary>
        /// What a door click and a turn of the key cost, indexed by
        /// <see cref="DoorState"/>, for an inside door and for the way out.
        /// Shared with the run; never written.
        /// </summary>
        private readonly int[] doorClickCosts;
        private readonly int[] exitClickCosts;
        private readonly int[] lockToggleCosts;
        private readonly int[] exitLockToggleCosts;

        internal RunSnapshot(int agentCount, int doorSlotCount, int objectCount, int tableCount,
            int[] cardCosts, int[] doorClickCosts, int[] exitClickCosts, int[] lockToggleCosts, int[] exitLockToggleCosts)
        {
            agents = new AgentSnapshot[agentCount];
            doors = new Prefix<DoorSnapshot>(doorSlotCount);
            physicsObjects = new PhysicsObjectSnapshot[objectCount];
            tables = new TableSnapshot[tableCount];
            hand = new Prefix<PlayerCommandType>(16);
            this.cardCosts = cardCosts;
            this.doorClickCosts = doorClickCosts;
            this.exitClickCosts = exitClickCosts;
            this.lockToggleCosts = lockToggleCosts;
            this.exitLockToggleCosts = exitLockToggleCosts;
            PowerSparks = System.Array.Empty<PowerSparkSnapshot>();
        }

        // The buffers the run writes into. Internal: the display only reads.
        internal AgentSnapshot[] AgentBuffer => agents;
        internal PhysicsObjectSnapshot[] PhysicsObjectBuffer => physicsObjects;
        internal TableSnapshot[] TableBuffer => tables;
        internal Prefix<DoorSnapshot> DoorBuffer => doors;
        internal Prefix<PlayerCommandType> HandBuffer => hand;

        /// <summary>The scalars and the views, written after the buffers are.</summary>
        internal void Fill(
            int tick,
            bool fireActive,
            LogicalPosition fireOrigin,
            int fireCellSizeMillimetres,
            IReadOnlyList<FireCellSnapshot> fireCells,
            IReadOnlyList<CausalEvent> events,
            int clearOfFireCount,
            bool alarmsRinging,
            bool influenceEnabled,
            int influence,
            int influenceMaximum,
            int influenceSpent,
            int influenceEarned,
            int blastChargesRemaining,
            IReadOnlyList<PowerSparkSnapshot> powerSparks,
            RoundPhase roundPhase,
            int targetSavedPercent)
        {
            Tick = tick;
            FireActive = fireActive;
            FireOrigin = fireOrigin;
            FireCellSizeMillimetres = fireCellSizeMillimetres;
            this.fireCells = fireCells;
            this.events = events;
            ClearOfFireCount = clearOfFireCount;
            AlarmsRinging = alarmsRinging;
            InfluenceEnabled = influenceEnabled;
            Influence = influence;
            InfluenceMaximum = influenceMaximum;
            InfluenceSpent = influenceSpent;
            InfluenceEarned = influenceEarned;
            BlastChargesRemaining = blastChargesRemaining;
            PowerSparks = powerSparks;
            RoundPhase = roundPhase;
            TargetSavedPercent = targetSavedPercent;
        }

        public int Tick { get; private set; }

        /// <summary>People still in the building, but in a room with nothing burning in it.</summary>
        public int ClearOfFireCount { get; private set; }

        /// <summary>Whether the fire alarms are ringing.</summary>
        public bool AlarmsRinging { get; private set; }

        /// <summary>
        /// Whether this level has a purse at all (prototype 3, 2026-09-25:
        /// the office does not). Off, everything is free and the display
        /// draws no purse and no prices.
        /// </summary>
        public bool InfluenceEnabled { get; private set; } = true;

        /// <summary>What the player has left to spend, and what they have spent and earned.</summary>
        public int Influence { get; private set; }
        public int InfluenceMaximum { get; private set; }
        public int InfluenceSpent { get; private set; }
        public int InfluenceEarned { get; private set; }

        /// <summary>
        /// The cards the player is holding, in the order the dead dealt them.
        /// Empty at the start of every round: nothing is bought, everything is
        /// dealt.
        /// </summary>
        public IReadOnlyList<PlayerCommandType> Hand => hand;

        /// <summary>How many sticks of TNT the player has left.</summary>
        public int BlastChargesRemaining { get; private set; }

        /// <summary>What a card costs, so the display can grey out what is out of reach.</summary>
        public int CostOf(PlayerCommandType card)
        {
            int index = (int)card;
            return cardCosts != null && index >= 0 && index < cardCosts.Length ? cardCosts[index] : 0;
        }

        /// <summary>
        /// What one click on a door in this state would cost, so the hover
        /// hint can put a price on it before the player commits to it. The
        /// building's way out has its own price for the key.
        /// </summary>
        public int CostOfDoorClick(DoorState state, bool leadsOutside)
        {
            return CostFrom(leadsOutside ? exitClickCosts : doorClickCosts, state);
        }

        /// <summary>What turning the key on a door in this state would cost.</summary>
        public int CostOfLockToggle(DoorState state, bool leadsOutside)
        {
            return CostFrom(leadsOutside ? exitLockToggleCosts : lockToggleCosts, state);
        }

        private static int CostFrom(int[] table, DoorState state)
        {
            int index = (int)state;
            return table != null && index >= 0 && index < table.Length ? table[index] : 0;
        }

        public bool FireActive { get; private set; }
        public LogicalPosition FireOrigin { get; private set; }
        public int FireCellSizeMillimetres { get; private set; }
        public IReadOnlyList<FireCellSnapshot> FireCells => fireCells;

        /// <summary>
        /// Every spark crawling along the cable right now, so the view can
        /// draw it in the same place the rules have it. Empty when nothing is
        /// lit, which is most of a round.
        /// </summary>
        public IReadOnlyList<PowerSparkSnapshot> PowerSparks { get; private set; }
        public IReadOnlyList<AgentSnapshot> Agents => agents;
        public IReadOnlyList<CausalEvent> Events => events;
        public IReadOnlyList<DoorSnapshot> Doors => doors;
        public IReadOnlyList<PhysicsObjectSnapshot> PhysicsObjects => physicsObjects;
        public IReadOnlyList<TableSnapshot> Tables => tables;

        public int CalmCount => Count(AgentFearState.Calm);
        public int ScaredCount => Count(AgentFearState.Scared);

        public int FrozenCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating &&
                        agents[i].ActivityState == AgentActivityState.Frozen)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int DownCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating && agents[i].IsDown)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int UnconsciousCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating &&
                        agents[i].BodyState == AgentBodyState.Unconscious)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int BurningCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    if (agents[i].Participation == AgentParticipation.Participating && agents[i].IsBurning)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int LostCount => CountOutcome(AgentTerminalOutcome.Lost);
        public int EscapedCount => CountOutcome(AgentTerminalOutcome.Escaped);

        /// <summary>Alive inside at the end, somewhere the hazard could not reach.</summary>
        public int SurvivedCount => CountOutcome(AgentTerminalOutcome.Survived);

        /// <summary>Where the round has got to: before the event, during it, or finished.</summary>
        public RoundPhase RoundPhase { get; private set; }

        /// <summary>Whether the player has set the disaster going yet.</summary>
        public bool EventTriggered => RoundPhase != RoundPhase.BeforeEvent;

        /// <summary>Whether the round is finished and the score final.</summary>
        public bool RoundIsOver => RoundPhase == RoundPhase.Over;

        /// <summary>Everybody in the level, whatever became of them.</summary>
        public int CrowdSize => agents.Length;

        /// <summary>
        /// Saved: out of the building alive, or alive inside at the end
        /// somewhere the hazard could not reach. The game counts both, because
        /// barricading yourself somewhere safe is a way of living through a
        /// disaster rather than an exploit.
        /// </summary>
        public int SavedCount => EscapedCount + SurvivedCount;

        /// <summary>Still in the building with their fate undecided.</summary>
        public int RemainingCount => CrowdSize - SavedCount - LostCount;

        /// <summary>The share of the crowd that has to be saved to clear the level.</summary>
        public int TargetSavedPercent { get; private set; }

        /// <summary>How many people that target works out to, rounded up.</summary>
        public int TargetSavedCount => (CrowdSize * TargetSavedPercent + 99) / 100;

        /// <summary>The share of the crowd saved so far, rounded to the nearest whole percent.</summary>
        public int SavedPercent => CrowdSize == 0 ? 0 : (SavedCount * 100 + CrowdSize / 2) / CrowdSize;

        /// <summary>Whether enough people have been saved to clear the level.</summary>
        public bool Cleared => SavedCount >= TargetSavedCount;

        private int CountOutcome(AgentTerminalOutcome outcome)
        {
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Outcome == outcome)
                {
                    count++;
                }
            }

            return count;
        }

        private int Count(AgentFearState fearState)
        {
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Participation == AgentParticipation.Participating && agents[i].FearState == fearState)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
