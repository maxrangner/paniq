using System;

namespace Paniq.Simulation
{
    // Every tunable number in a fire-reaction scenario, grouped by the part of
    // the game it shapes. The defaults below are the prototype's current
    // values; the scenario asset stores its own copy and the simulation runs
    // on a clone, so a run can never write back into the asset.
    //
    // Units: distances are millimetres, speeds millimetres per tick, turn
    // rates degrees per tick, durations ticks (50 ticks = 1 second), chances
    // whole percentages. Object velocities are noted where they differ.

    /// <summary>The size of a person and the longest single step; the rooms themselves are in the scenario.</summary>
    [Serializable]
    public sealed class WorldSettings
    {
        public int OccupancyRadiusMillimetres = 250;
        public int MaximumStepDistanceMillimetres = 120;

        /// <summary>
        /// How far, either way, a fixed length of time for a person is
        /// stretched or squeezed when it starts, as a percentage of it. Every
        /// moment somebody spends -- getting up, trying a door, pressing an
        /// alarm, picking something up -- takes a slightly different time for
        /// each person and each occasion, so two people who started the same
        /// thing on the same tick do not finish it on the same tick either.
        /// The owner's rule (2026-09-24): nothing in the game happens to a
        /// whole group on exactly the same tick.
        /// </summary>
        public int TimingJitterPercent = 20;

        public WorldSettings Clone() => (WorldSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(OccupancyRadiusMillimetres > 0 && OccupancyRadiusMillimetres <= 2000, "occupancy radius");
            Settings.Require(MaximumStepDistanceMillimetres >= 0 && MaximumStepDistanceMillimetres <= 1000, "maximum step");
            Settings.Require(Settings.Percent(TimingJitterPercent), "timing jitter");
        }
    }

    /// <summary>Seeing the fire and how long it takes to react.</summary>
    [Serializable]
    public sealed class PerceptionSettings
    {
        /// <summary>
        /// How far somebody sees a threat, in their own room or through an
        /// open doorway along their line of sight. Twelve metres is the long
        /// side of the meeting room and most of the corridor: a fire in the
        /// room you are in is seen, and a fire beyond an open door is seen
        /// when the door is between you and it. It was three metres, which
        /// let a fire burn at the far end of the same room unseen
        /// (2026-09-24, the owner's second playtest).
        /// </summary>
        public int VisionRangeMillimetres = 12000;
        public int MaximumReactionDelayTicks = 20;

        /// <summary>
        /// How far off somebody sees a person bolt: a frightened person on
        /// their feet and running, or leaping up out of a chair, in the
        /// watcher's own room or through an open doorway. Fear spreads by
        /// sight as well as by voice: in a meeting the person opposite
        /// leaping up is what makes you look, before you know why.
        /// </summary>
        public int BoltSightRangeMillimetres = 8000;

        /// <summary>A frightened person moving at least this fast (millimetres a tick) is plainly running, not edging away; a calm walk is 20 to 32.</summary>
        public int BoltSpeedMinimum = 40;

        /// <summary>This brave or braver: seeing somebody bolt makes you look, not run. Everybody else is startled by it.</summary>
        public int BraveLookFirstMinimum = 7;

        /// <summary>
        /// No two people finish being startled on the same tick. Whoever
        /// would have, the later one in ID order is put off by this many
        /// ticks, and again until the tick is theirs alone: a bell that
        /// reaches six people at a table has them come up out of their chairs
        /// one after another, a few ticks apart, never all at once. The
        /// owner's rule (2026-09-24).
        /// </summary>
        public int StartleStaggerTicks = 3;

        /// <summary>
        /// Nobody reacts on the tick a thing happens. Every reaction to the
        /// world -- a bell, a door swinging open, a shout, a noise, a leader's
        /// call, being knocked down, giving something up and thinking again --
        /// begins this many ticks late, drawn per person and per occasion from
        /// the seed, so a crowd answers the world raggedly the way people do
        /// rather than all at once the way clockwork does. The owner's rule
        /// (2026-09-24): all behaviour, never a reaction on the same tick.
        /// </summary>
        public int ReactionLagMinimumTicks = 2;
        public int ReactionLagMaximumTicks = 8;

        /// <summary>
        /// How far away somebody who does not know the building notices a door
        /// or a corner of the room they are in. Eight metres is how far off a
        /// green sign can be read, so a door and the sign beside it are seen
        /// together. On this floor it is also what makes the T at the end of
        /// the corridor a real choice: stood in its archway, the dead end is
        /// five and a half metres off and in sight, and the way out is nine and
        /// a half metres off and not.
        /// </summary>
        public int DoorSightRangeMillimetres = 8000;

        public PerceptionSettings Clone() => (PerceptionSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(StartleStaggerTicks >= 1, "startle stagger");
            Settings.Require(Settings.Range(ReactionLagMinimumTicks, ReactionLagMaximumTicks, 1), "reaction lag");
            Settings.Require(VisionRangeMillimetres > 0 && MaximumReactionDelayTicks >= 0 && DoorSightRangeMillimetres >= 0,
                "perception");
            Settings.Require(BoltSightRangeMillimetres >= 0 && BoltSpeedMinimum >= 0 && BraveLookFirstMinimum >= 0, "seeing somebody bolt");
        }
    }

    /// <summary>
    /// How a round begins, how it is judged to be finished, and what it takes
    /// to clear it.
    /// </summary>
    [Serializable]
    public sealed class RoundSettings
    {
        /// <summary>
        /// The hazard waits for the player's "trigger event" rather than
        /// starting itself on <see cref="FireSettings.ActivationTick"/>. The
        /// level then opens calm and stays calm until the player sets it off.
        /// <para>
        /// Off by default, because a bare scenario on its own should behave the
        /// way it always has: the fire starts on its own tick count. A playable
        /// level turns it on (see the level asset), so the calm opening belongs
        /// to the level rather than to the scenario data.
        /// </para>
        /// </summary>
        public bool HazardWaitsForTrigger;

        /// <summary>
        /// The share of the crowd that has to be saved to clear the level, out
        /// of a hundred. Saved means escaped or alive inside at the end.
        /// Chosen with the owner at 75: fifteen of twenty.
        /// </summary>
        public int TargetSavedPercent = 75;

        /// <summary>
        /// How long the whole building has to be doing nothing at all before
        /// the round is called finished.
        /// <para>
        /// The round used to end as soon as everybody left was in a room the
        /// fire could not reach, which stopped it while people were still
        /// walking towards the door. It waits for them now, and this is the
        /// only thing that stops a run going on for ever when the last person
        /// left is frozen in a corner.
        /// </para>
        /// </summary>
        public int StallTicks = 1500;

        /// <summary>
        /// How far somebody has to get from where they were standing when the
        /// stall clock started for the building to count as still moving.
        /// </summary>
        public int StallMoveMillimetres = 400;

        public RoundSettings Clone() => (RoundSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(TargetSavedPercent >= 0 && TargetSavedPercent <= 100, "round clear target");
            Settings.Require(StallTicks >= 1 && StallMoveMillimetres >= 0, "round stall time");
        }
    }

    /// <summary>
    /// The cable from the fuse box down to the sockets, and how fast a spark
    /// runs along it once the fuse box goes.
    /// </summary>
    [Serializable]
    public sealed class PowerSettings
    {
        /// <summary>
        /// How fast the spark runs, in millimetres a tick. 400 is twenty
        /// metres a second: the office's three sockets go within about two
        /// and a half seconds of the fuse box.
        /// <para>
        /// It was 45 (a crawl slower than somebody running) while a socket
        /// popping lit the cable in both directions and a spark crept up to
        /// the fuse box. Since 2026-09-26 the cable runs one way only -- the
        /// owner's rule: "only if the fusebox goes, it should quickly cascade
        /// down to all outlets, but not the other way around" -- so the fuse
        /// box going is the big moment, and it has to be quick.
        /// </para>
        /// </summary>
        public int SparkSpeedMillimetresPerTick = 400;

        /// <summary>How near the player has to click the fuse box for the card to find it.</summary>
        public int CardReachMillimetres = 2500;

        public PowerSettings Clone() => (PowerSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(SparkSpeedMillimetresPerTick >= 1, "spark speed");
            Settings.Require(CardReachMillimetres >= 0, "fuse box card reach");
        }
    }

    /// <summary>Where and when the fire starts, and how fast it spreads square by square.</summary>
    [Serializable]
    public sealed class FireSettings
    {
        /// <summary>
        /// The preset areas the danger may begin in. One of them is drawn, and
        /// then a spot inside it, so a run's fire is somewhere different every
        /// seed without ever being somewhere silly -- inside a wall, in the
        /// corridor everybody has to use, or out in the street.
        /// <para>
        /// A list rather than one rectangle because a floor has more than one
        /// room worth burning, and because a finished level wants to say where
        /// its disaster is allowed to start rather than leaving it to chance.
        /// </para>
        /// </summary>
        public LogicalBounds[] SpawnAreas = { new LogicalBounds(-2000, 2000, -2000, 2000) };

        /// <summary>
        /// The one area, for a scenario that wants the fire in exactly one
        /// place -- which nearly every test does. Reading it gives the first
        /// area; setting it replaces the lot with that one.
        /// </summary>
        public LogicalBounds SpawnBounds
        {
            get => SpawnAreas != null && SpawnAreas.Length > 0 ? SpawnAreas[0] : default;
            set => SpawnAreas = new[] { value };
        }

        public int ActivationTick = 250;
        public int CellSizeMillimetres = 500;
        public int SpreadMinimumTicks = 40;
        public int SpreadMaximumTicks = 120;

        /// <summary>
        /// A fire the Director starts in a thing (a waste bin) counts as young
        /// while fewer squares than this are alight: a patch about the size of
        /// a meeting table. Only such a fire is ever young.
        /// </summary>
        public int YoungFireSquares = 12;

        /// <summary>
        /// How much longer a young fire's squares wait before spreading, as a
        /// share of the usual wait: three times as long, so it takes about
        /// half a minute to grow from the bin to the size of a table. Enough
        /// time for somebody brave to fetch the bottle off the wall, not so
        /// much that nobody ever needs to (the owner, 2026-09-26: slow, so
        /// people have a chance to fight it if the right personality is there).
        /// </summary>
        public int YoungFireSpreadPercent = 300;

        /// <summary>How long someone on fire runs around before collapsing.</summary>
        public int BurnMinimumTicks = 150;
        public int BurnMaximumTicks = 300;

        /// <summary>How often a burning person lurches off in a new direction.</summary>
        public int BurningTurnMinimumTicks = 10;
        public int BurningTurnMaximumTicks = 25;

        /// <summary>Blocked this long, a burning person lurches another way at once.</summary>
        public int BurningBlockedTurnTicks = 5;

        /// <summary>How often a burning person screams (a yell others hear).</summary>
        public int BurningScreamMinimumTicks = 25;
        public int BurningScreamMaximumTicks = 50;

        /// <summary>How long a square that has been put out stays too wet to catch again.</summary>
        public int DousedWetTicks = 1000;

        /// <summary>How many ticks of spray one burning square takes to put out.</summary>
        public int DouseTicksPerCell = 30;

        /// <summary>A gap between two bodies at most this wide lets the flames jump across.</summary>
        public int BurningSpreadGapMillimetres = 100;

        /// <summary>Chance per tick that the flames jump to someone that close. Running into someone always does it.</summary>
        public int BurningSpreadChancePercent = 20;

        /// <summary>Chance per tick that someone alight drops and rolls instead of running blind.</summary>
        public int DropAndRollChancePercent = 4;

        /// <summary>How long a drop-and-roll lasts.</summary>
        public int RollMinimumTicks = 60;
        public int RollMaximumTicks = 120;

        /// <summary>Chance a roll puts the flames out for good.</summary>
        public int RollPutsOutChancePercent = 35;

        public FireSettings Clone()
        {
            var copy = (FireSettings)MemberwiseClone();

            // The shallow copy would hand both runs the same array, and a run
            // must never be able to reach into another one's scenario.
            copy.SpawnAreas = (LogicalBounds[])SpawnAreas?.Clone();
            return copy;
        }

        internal void Validate()
        {
            Settings.Require(SpawnAreas != null && SpawnAreas.Length > 0, "at least one fire spawn area");
            for (int i = 0; i < SpawnAreas.Length; i++)
            {
                Settings.Require(SpawnAreas[i].MinX <= SpawnAreas[i].MaxX && SpawnAreas[i].MinZ <= SpawnAreas[i].MaxZ,
                    "fire spawn bounds");
            }
            Settings.Require(ActivationTick >= 0 && CellSizeMillimetres >= 100, "fire timing and cell size");
            Settings.Require(SpreadMinimumTicks > 0 && SpreadMaximumTicks >= SpreadMinimumTicks, "fire spread interval");
            Settings.Require(YoungFireSquares >= 0 && YoungFireSpreadPercent >= 1, "a young fire's spread");
            Settings.Require(Settings.Range(BurnMinimumTicks, BurnMaximumTicks, 1) &&
                             Settings.Range(BurningTurnMinimumTicks, BurningTurnMaximumTicks, 1) &&
                             BurningBlockedTurnTicks >= 1 &&
                             Settings.Range(BurningScreamMinimumTicks, BurningScreamMaximumTicks, 1) &&
                             BurningSpreadGapMillimetres >= 0 && Settings.Percent(BurningSpreadChancePercent) &&
                             DousedWetTicks >= 0 && DouseTicksPerCell >= 1, "burning people");
            Settings.Require(Settings.Percent(DropAndRollChancePercent) &&
                             Settings.Range(RollMinimumTicks, RollMaximumTicks, 1) &&
                             Settings.Percent(RollPutsOutChancePercent), "drop and roll");
        }
    }

    /// <summary>Personal space and keeping off walls and boxes, shared by calm and panicked people.</summary>
    [Serializable]
    public sealed class SteeringSettings
    {
        public int PersonalSpaceMillimetres = 800;
        public int WallAvoidDistanceMillimetres = 600;

        /// <summary>How far beyond touching a box people start steering around it.</summary>
        public int ObjectAvoidMarginMillimetres = 400;

        public SteeringSettings Clone() => (SteeringSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(PersonalSpaceMillimetres >= 0 && WallAvoidDistanceMillimetres >= 0 &&
                             ObjectAvoidMarginMillimetres >= 0, "steering distances");
        }
    }

    /// <summary>Loitering before anyone is frightened: strolling, standing, glancing, chatting.</summary>
    [Serializable]
    public sealed class CalmSettings
    {
        /// <summary>Walking pace of someone with Speed 0 and Speed 10; everyone else is in between (plus a little jitter).</summary>
        public int SpeedMinimum = 20;
        public int SpeedMaximum = 32;
        public int TurnRateMinimum = 4;
        public int TurnRateMaximum = 7;
        public int Acceleration = 2;
        public int DecisionMinimumTicks = 60;
        public int DecisionMaximumTicks = 200;

        /// <summary>A person blocked this long gives up on what they were doing.</summary>
        public int BlockedGiveUpTicks = 20;

        public int StrollWallMarginMillimetres = 1000;

        /// <summary>
        /// How often a calm person wandering off picks somewhere through an
        /// open doorway rather than in the room they are in. Low, so rooms keep
        /// the people in them and the movement reads as somebody popping next
        /// door rather than the building shuffling itself.
        /// </summary>
        public int StrollNextDoorPercent = 15;
        public int StrollMinimumDistanceMillimetres = 1500;
        public int StrollArrivalDistanceMillimetres = 300;
        public int StrollSlowdownDistanceMillimetres = 700;
        public int StrollTimeoutTicks = 600;
        public int WanderMaximumDegrees = 25;

        public int SocialStopDistanceMillimetres = 1100;
        public int SocialMinimumDistanceMillimetres = 1500;
        public int SocialMaximumDistanceMillimetres = 6000;
        public int SocialTimeoutTicks = 400;

        /// <summary>Steering weights, as percentages of the pull toward the goal.</summary>
        public int PeopleAvoidPercent = 100;
        public int WallAvoidPercent = 150;
        public int ObjectAvoidPercent = 100;

        /// <summary>
        /// How hard a calm person keeps off the edge of a table: as hard as
        /// off a wall. Somebody strolling round the office walks round the
        /// desks.
        /// </summary>
        public int TableAvoidPercent = 150;

        public CalmSettings Clone() => (CalmSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(Settings.Range(SpeedMinimum, SpeedMaximum, 0), "calm speed");
            Settings.Require(Settings.Range(TurnRateMinimum, TurnRateMaximum, 1), "calm turn rates");
            Settings.Require(Acceleration > 0, "calm acceleration");
            Settings.Require(Settings.Range(DecisionMinimumTicks, DecisionMaximumTicks, 1), "calm decision interval");
            Settings.Require(BlockedGiveUpTicks >= 1 && StrollTimeoutTicks >= 1 && SocialTimeoutTicks >= 1, "calm timeouts");
            Settings.Require(StrollNextDoorPercent >= 0 && StrollNextDoorPercent <= 100, "stroll next door chance");
            Settings.Require(StrollWallMarginMillimetres >= 0 && StrollMinimumDistanceMillimetres >= 0 &&
                             StrollArrivalDistanceMillimetres >= 0 && StrollSlowdownDistanceMillimetres > 0 &&
                             WanderMaximumDegrees >= 0 && WanderMaximumDegrees <= 180, "strolling");
            Settings.Require(SocialStopDistanceMillimetres >= 0 &&
                             Settings.Range(SocialMinimumDistanceMillimetres, SocialMaximumDistanceMillimetres, 0), "socialising");
            Settings.Require(PeopleAvoidPercent >= 0 && WallAvoidPercent >= 0 && ObjectAvoidPercent >= 0 &&
                             TableAvoidPercent >= 0, "calm steering weights");
        }
    }

    /// <summary>Running in fear: sprinting, changing their mind, swerving, hesitating, following.</summary>
    [Serializable]
    public sealed class PanicSettings
    {
        /// <summary>Sprinting pace of someone with Speed 0 and Speed 10; everyone else is in between (plus a little jitter).</summary>
        public int SpeedMinimum = 60;
        public int SpeedMaximum = 110;
        public int TurnRateMinimum = 10;
        public int TurnRateMaximum = 16;
        public int Acceleration = 8;
        public int DecisionMinimumTicks = 20;
        public int DecisionMaximumTicks = 60;
        public int SwerveChancePercent = 35;
        public int SwerveAngleMinimum = 30;
        public int SwerveAngleMaximum = 70;
        public int SwerveMinimumTicks = 10;
        public int SwerveMaximumTicks = 25;
        public int HesitateChancePercent = 12;
        public int HesitateMinimumTicks = 5;
        public int HesitateMaximumTicks = 15;

        /// <summary>Fire closer than this makes a runner bolt straight away from it.</summary>
        public int DangerDistanceMillimetres = 1500;

        public int FollowRadiusMillimetres = 2500;

        /// <summary>How strongly runners drift with nearby runners, as a percentage of their own goal.</summary>
        public int FollowPercent = 35;

        public int ShoutMinimumTicks = 100;
        public int ShoutMaximumTicks = 250;

        /// <summary>A runner blocked this long makes a fresh decision.</summary>
        public int BlockedGiveUpTicks = 12;

        public int ArrivalDistanceMillimetres = 700;

        // Choosing where to run when there is no door to run for.
        public int EscapeSampleCount = 8;
        public int EscapeWallMarginMillimetres = 700;
        public int EscapeRouteClearanceMillimetres = 1000;
        public int EscapeRoutePenaltyMillimetres = 5000;
        public int EscapeShortHopDistanceMillimetres = 1500;
        public int EscapeShortHopPenaltyMillimetres = 2000;
        public int EscapeTurnPenaltyPerDegree = 10;
        public int EscapeNoiseMillimetres = 1500;

        /// <summary>A spot or door whose straight route runs into a table scores this much worse.</summary>
        public int TableRoutePenaltyMillimetres = 3000;

        // Reading the exit signs. Somebody running who catches sight of one
        // takes its word for which way the way out is.

        /// <summary>
        /// How far off a sign can still be read, in millimetres. A sign is a
        /// big lit thing you pick out down a corridor, not something you have
        /// to be standing under: the fire's 3 m vision range is the distance at
        /// which flames are upon you, and would be far too short for this.
        /// </summary>
        public int SignReadRangeMillimetres = 8000;

        /// <summary>
        /// How much better a spot scores for lying the way a visible sign
        /// points, in millimetres, falling to that much worse for lying the
        /// opposite way. Big enough to beat the noise in the scoring and to
        /// stand alongside the penalty for a route past the fire, so a sign
        /// changes where somebody decides to go rather than only how they
        /// drift once they are going.
        /// </summary>
        public int SignEscapeBonusMillimetres = 6000;

        /// <summary>Steering weights, as percentages of the pull toward the goal.</summary>
        public int PeopleAvoidPercent = 50;
        public int WallAvoidPercent = 200;
        public int ObjectAvoidPercent = 20;

        /// <summary>
        /// How hard a running person keeps off the edge of a table. Much less
        /// than off a wall: a table is furniture, not brickwork, and a crowd
        /// in a panic brushes past desks, bumps them and shoves them along.
        /// Tables used to be kept off exactly like walls, so nobody ever
        /// touched one and no table was ever seen to move.
        /// </summary>
        public int TableAvoidPercent = 60;

        /// <summary>
        /// The change of speed somebody stuck behind a table gives it when
        /// they heave it out of their way, in millimetres per tick, for a
        /// table as light as a blast's reference thing (20 kg). Applied at
        /// the table's top edge, so a light desk goes over away from them and
        /// a heavy one slides. Heavier tables get less, down to
        /// <see cref="TableHeaveLeastPercent"/> of it.
        /// </summary>
        public int TableHeaveSpeedMillimetresPerTick = 80;

        /// <summary>The least share of the heave the heaviest table gets, so even the meeting table shifts.</summary>
        public int TableHeaveLeastPercent = 25;

        /// <summary>How long after heaving a table somebody waits before heaving one again.</summary>
        public int TableHeaveRestTicks = 40;

        public PanicSettings Clone() => (PanicSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(Settings.Range(SpeedMinimum, SpeedMaximum, 0), "panic speed");
            Settings.Require(Settings.Range(TurnRateMinimum, TurnRateMaximum, 1), "panic turn rates");
            Settings.Require(Acceleration > 0, "panic acceleration");
            Settings.Require(Settings.Range(DecisionMinimumTicks, DecisionMaximumTicks, 1), "panic decision interval");
            Settings.Require(Settings.Percent(SwerveChancePercent) && Settings.Percent(HesitateChancePercent), "chances");
            Settings.Require(Settings.Range(SwerveAngleMinimum, SwerveAngleMaximum, 0) && SwerveAngleMaximum <= 180 &&
                             Settings.Range(SwerveMinimumTicks, SwerveMaximumTicks, 1) &&
                             Settings.Range(HesitateMinimumTicks, HesitateMaximumTicks, 1), "swerve and hesitation");
            Settings.Require(EscapeSampleCount >= 1 && DangerDistanceMillimetres >= 0 && FollowRadiusMillimetres >= 0 &&
                             FollowPercent >= 0, "panic choices");
            Settings.Require(Settings.Range(ShoutMinimumTicks, ShoutMaximumTicks, 1), "panic shouts");
            Settings.Require(BlockedGiveUpTicks >= 1 && ArrivalDistanceMillimetres >= 0, "panic arrival");
            Settings.Require(EscapeWallMarginMillimetres >= 0 && EscapeRouteClearanceMillimetres >= 0 &&
                             EscapeRoutePenaltyMillimetres >= 0 && EscapeShortHopDistanceMillimetres >= 0 &&
                             EscapeShortHopPenaltyMillimetres >= 0 && EscapeTurnPenaltyPerDegree >= 0 &&
                             EscapeNoiseMillimetres >= 0 && TableRoutePenaltyMillimetres >= 0, "escape scoring");
            Settings.Require(PeopleAvoidPercent >= 0 && WallAvoidPercent >= 0 && ObjectAvoidPercent >= 0 &&
                             TableAvoidPercent >= 0, "panic steering weights");
            Settings.Require(TableHeaveSpeedMillimetresPerTick >= 0 && Settings.Percent(TableHeaveLeastPercent) &&
                             TableHeaveRestTicks >= 1, "heaving tables");
            Settings.Require(SignReadRangeMillimetres >= 0 && SignEscapeBonusMillimetres >= 0,
                "exit sign reading");
        }
    }

    /// <summary>Who runs and who freezes. Dealt like a shuffled deck, so every room gets this mix.</summary>
    [Serializable]
    public sealed class TemperamentSettings
    {
        public int FreezeThenRunPercent = 30;
        public int FreezeForeverPercent = 15;
        public int FreezeMinimumTicks = 100;
        public int FreezeMaximumTicks = 300;

        public TemperamentSettings Clone() => (TemperamentSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(FreezeThenRunPercent >= 0 && FreezeForeverPercent >= 0 &&
                             FreezeThenRunPercent + FreezeForeverPercent <= 100 &&
                             Settings.Range(FreezeMinimumTicks, FreezeMaximumTicks, 1), "temperaments");
        }
    }

    /// <summary>
    /// Noises as simulation data: how far yells, fire and thuds carry, and
    /// how a calm person investigates one.
    /// </summary>
    [Serializable]
    public sealed class HearingSettings
    {
        /// <summary>
        /// Calm people this close to a yell understand it and are alarmed.
        /// Every yell is a frightened person's (calm people talk, they do not
        /// yell), so this is the reach of a panic shout: six metres, the
        /// width of a room, halved by a shut door. It was two and a half,
        /// which spread a fright round a meeting table one seat at a time.
        /// </summary>
        public int YellAlarmRadiusMillimetres = 6000;

        /// <summary>Calm people this close to a yell only hear it and turn to look.</summary>
        public int YellHearingRadiusMillimetres = 12000;

        /// <summary>
        /// How far a fire is heard when it is one square of floor. It is heard
        /// further as it grows, by <see cref="FireHearingPerCellMillimetres"/>
        /// a burning square up to <see cref="FireHearingMaximumMillimetres"/>:
        /// a bathroom ablaze roars, and a roar carries. Through a wall or a
        /// shut door it carries half as far, like every noise.
        /// </summary>
        public int FireHearingRadiusMillimetres = 3500;
        public int FireHearingPerCellMillimetres = 50;
        public int FireHearingMaximumMillimetres = 15000;

        public int BumpSoundRadiusMillimetres = 3000;
        public int InvestigateMinimumTicks = 50;
        public int InvestigateMaximumTicks = 125;

        /// <summary>After this long looking toward a noise, a person may edge toward it.</summary>
        public int InvestigateCreepDelayTicks = 25;

        /// <summary>They only edge closer while the noise is further away than this.</summary>
        public int InvestigateCreepDistanceMillimetres = 2000;

        /// <summary>They only edge closer while facing the noise within this many degrees.</summary>
        public int InvestigateCreepMaximumTurn = 30;

        /// <summary>
        /// A person keeps a few noises in their head at once. While they are
        /// looking toward one, a threat's own noise (fire crackling) or a
        /// louder noise nearer to them takes over; anything else waits, and
        /// is looked at next if it is still this fresh when they finish.
        /// </summary>
        public int PendingNoiseFreshTicks = 300;

        /// <summary>
        /// Somebody who has heard a threat's noise from another room and seen
        /// nothing goes to look: they walk toward the noise, opening doors on
        /// the way, and stop this far short of it -- by then they have seen
        /// what it was, or they have not and go back to their day.
        /// </summary>
        public int GoAndLookStopMillimetres = 2500;

        /// <summary>The very nervous do not go and look; they stay where they are and keep glancing.</summary>
        public int GoAndLookNervousnessMaximum = 7;

        /// <summary>Having gone to look once, how long before the same person would go again.</summary>
        public int GoAndLookAgainTicks = 900;

        public HearingSettings Clone() => (HearingSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(YellAlarmRadiusMillimetres > 0 && YellHearingRadiusMillimetres >= YellAlarmRadiusMillimetres &&
                             FireHearingRadiusMillimetres >= 0 && BumpSoundRadiusMillimetres >= 0 &&
                             Settings.Range(InvestigateMinimumTicks, InvestigateMaximumTicks, 1), "hearing");
            Settings.Require(InvestigateCreepDelayTicks >= 0 && InvestigateCreepDistanceMillimetres >= 0 &&
                             InvestigateCreepMaximumTurn >= 0 && InvestigateCreepMaximumTurn <= 180, "investigating");
            Settings.Require(FireHearingPerCellMillimetres >= 0 && FireHearingMaximumMillimetres >= FireHearingRadiusMillimetres,
                "a fire heard as it grows");
            Settings.Require(PendingNoiseFreshTicks >= 0 && GoAndLookStopMillimetres >= 0 && GoAndLookNervousnessMaximum >= 0 &&
                             GoAndLookAgainTicks >= 1, "going to look");
        }
    }

    /// <summary>Running into people, staggering, being knocked down, tripping and getting up.</summary>
    [Serializable]
    public sealed class FallSettings
    {
        public int BumpMinimumSpeed = 50;
        public int KnockdownClosingSpeed = 100;
        public int StaggerMinimumTicks = 10;
        public int StaggerMaximumTicks = 20;
        public int StaggerJoltMinimumDegrees = 25;
        public int StaggerJoltMaximumDegrees = 50;
        public int KnockdownMinimumTicks = 60;
        public int KnockdownMaximumTicks = 140;
        public int GetUpTicks = 25;
        public int TripChancePercent = 4;
        public int TripMinimumSpeed = 60;
        public int TripMinimumTicks = 40;
        public int TripMaximumTicks = 100;

        /// <summary>Chance that a knock-down leaves someone out cold, at exactly the knock-down closing speed.</summary>
        public int PassOutChancePercent = 10;

        /// <summary>Extra pass-out chance for each mm/tick of closing speed above the knock-down speed.</summary>
        public int PassOutPercentPerSpeed = 1;

        /// <summary>For a box hit: extra pass-out chance per this much momentum (kg·mm/tick) above the knock-down momentum.</summary>
        public int PassOutMomentumPerPercent = 100;

        public int PassOutMaximumPercent = 60;
        public int UnconsciousMinimumTicks = 300;
        public int UnconsciousMaximumTicks = 600;

        /// <summary>Getting up groggily after coming to takes longer than after a plain fall.</summary>
        public int ComeToGetUpTicks = 50;

        // Shoving people out of the way. Running into somebody is an accident
        // that needs speed; taking hold of them and heaving them aside is
        // deliberate, works at a walk, and only the cruel do it.

        /// <summary>This evil: shove whoever is in the way aside instead of going round them.</summary>
        public int ShoveMinimumEvil = 7;

        /// <summary>The soonest they will shove somebody again.</summary>
        public int ShoveIntervalTicks = 40;

        /// <summary>How far a shove sends somebody, as far as the room allows.</summary>
        public int ShovePushMillimetres = 350;

        /// <summary>This much stronger than the person shoved, and they go down rather than just reeling.</summary>
        public int ShoveKnockDownStrengthGap = 2;

        public FallSettings Clone() => (FallSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(BumpMinimumSpeed > 0 && KnockdownClosingSpeed >= BumpMinimumSpeed &&
                             Settings.Range(StaggerMinimumTicks, StaggerMaximumTicks, 1) &&
                             Settings.Range(StaggerJoltMinimumDegrees, StaggerJoltMaximumDegrees, 0) &&
                             Settings.Range(KnockdownMinimumTicks, KnockdownMaximumTicks, 1) && GetUpTicks >= 1, "collisions");
            Settings.Require(TripChancePercent >= 0 && TripChancePercent <= 50 && TripMinimumSpeed >= 0 &&
                             Settings.Range(TripMinimumTicks, TripMaximumTicks, 1), "tripping");
            Settings.Require(Settings.Percent(PassOutChancePercent) && PassOutPercentPerSpeed >= 0 &&
                             PassOutMomentumPerPercent > 0 && Settings.Percent(PassOutMaximumPercent) &&
                             Settings.Range(UnconsciousMinimumTicks, UnconsciousMaximumTicks, 1) &&
                             ComeToGetUpTicks >= 1, "passing out");
            Settings.Require(ShoveMinimumEvil >= 0 && ShoveIntervalTicks >= 1 && ShovePushMillimetres >= 0 &&
                             ShoveKnockDownStrengthGap >= 0, "shoving people aside");
        }
    }

    /// <summary>Doorways, escaping, and how panicked people choose, open, rattle and force doors.</summary>
    [Serializable]
    public sealed class ExitSettings
    {
        /// <summary>How far the walkable strip through an open door reaches outside.</summary>
        public int DoorwayDepthMillimetres = 2000;

        /// <summary>How far inside the room the walkable strip through an open door begins.</summary>
        public int DoorwayInsetMillimetres = 1000;

        /// <summary>How far past the wall someone must walk to count as escaped.</summary>
        public int EscapeDepthMillimetres = 800;

        public int DoorOpenTicks = 20;
        public int DoorTryTicks = 25;
        public int DoorForceChancePercent = 60;
        public int DoorForceMinimumTicks = 75;
        public int DoorForceMaximumTicks = 200;
        public int DoorShoveMinimumTicks = 20;
        public int DoorShoveMaximumTicks = 30;
        public int DoorAvoidMinimumTicks = 300;
        public int DoorAvoidMaximumTicks = 600;
        public int DoorCrowdedAvoidMinimumTicks = 100;
        public int DoorCrowdedAvoidMaximumTicks = 200;
        public int GiveUpGlanceMinimumTicks = 15;
        public int GiveUpGlanceMaximumTicks = 30;

        /// <summary>Wedged beside an open door and stuck, a runner stands aside this long for whoever is lined up with it.</summary>
        public int GiveWayMinimumTicks = 25;
        public int GiveWayMaximumTicks = 50;

        /// <summary>Standing aside: this far along the wall past the door's edge, and this far in from the wall.</summary>
        public int GiveWayAsideMillimetres = 100;
        public int GiveWayInsetMillimetres = 400;

        /// <summary>Runners aim this far inside a shut door.</summary>
        public int ApproachInsetMillimetres = 600;

        /// <summary>Runners aim this far outside an open door once lined up with it.</summary>
        public int OutsideTargetMillimetres = 1500;

        public int ArrivalDistanceMillimetres = 400;

        /// <summary>Within this distance of an open exit, a runner commits to leaving.</summary>
        public int CommitDistanceMillimetres = 2500;

        /// <summary>Within this distance of their exit, runners stop swerving and following.</summary>
        public int NoSwerveDistanceMillimetres = 2000;

        /// <summary>
        /// Clearing the way: somebody stuck can grab a thing this far beyond the
        /// edge of their own body (and the thing's), and throws it back past
        /// themselves, off to one side by between these two angles.
        /// </summary>
        public int ClearTheWayReachMillimetres = 400;
        public int ClearTheWayMinimumAngleDegrees = 30;
        public int ClearTheWayMaximumAngleDegrees = 70;

        /// <summary>With every way out given up on, how much better the room they already stand in has to look.</summary>
        public int CurrentRoomBonusMillimetres = 2000;

        /// <summary>What a room with nothing burning in it is worth when picking somewhere to get away from the fire.</summary>
        public int RefugeClearRoomMillimetres = 8000;

        /// <summary>What any room is worth while nothing anywhere is alight.</summary>
        public int RefugeNoFireMillimetres = 20000;

        /// <summary>How much floor one person needs before a room looks full to someone hoping to get in.</summary>
        public int RefugeSpacePerPersonMillimetres = 1000;

        // Scoring doors: distance, plus these bonuses and penalties.
        public int OpenBonusMillimetres = 4000;
        public int CurrentChoiceBonusMillimetres = 1500;
        public int ChoiceNoiseMillimetres = 1500;
        public int InFirePenaltyMillimetres = 8000;

        /// <summary>How much shoving damage a door takes before it breaks. Damage stays between attempts.</summary>
        public int DoorStrength = 40;

        /// <summary>
        /// How long a shut door stands with flames against it before it burns
        /// through and the fire comes on. Eighteen seconds: longer than a chair
        /// (three) or a table (five), because a door is a slab in a frame, and
        /// long enough that shutting one buys real time without ever being a
        /// way to win. Flames within <see cref="FireAtDoorRadiusMillimetres"/>
        /// of the doorway count, from either side.
        /// </summary>
        public int DoorBurnThroughTicks = 900;

        /// <summary>
        /// How long a pair of swing doors stands in the flames before it
        /// goes: half a shut door's time (the owner's rule, 2026-09-25 --
        /// "swinging doors stop fire half as good as normal ones"). Two thin
        /// leaves that meet in the middle rather than a slab in a frame.
        /// </summary>
        public int SwingDoorBurnThroughTicks = 450;

        // Closing doors. Only the cruel shut the door behind them as they
        // leave a room or the building, and only the cruellest lock it.
        // Shutting a door with fire beyond it is a different act and open to
        // anyone.

        /// <summary>How close to a door someone sheltering must be to pull it shut.</summary>
        public int CloseReachMillimetres = 2000;

        /// <summary>Anyone else this close to the door counts as "someone coming".</summary>
        public int CloseApproachRadiusMillimetres = 3000;

        /// <summary>Fire this close to a door makes anyone shut it, kind or not, and whoever is still coming.</summary>
        public int FireAtDoorRadiusMillimetres = 2000;

        /// <summary>
        /// This evil: shut the door behind them, even in the face of someone
        /// coming. Raised from 7, which let four people in twenty do it, so the
        /// building read as full of door-slammers rather than as having one.
        /// </summary>
        public int EvilCloseMinimum = 8;

        /// <summary>This evil: turn the key as well, so nobody can follow.</summary>
        public int EvilLockMinimum = 9;

        /// <summary>
        /// This callous or worse: with the flames already at the door, they
        /// pull it shut on somebody still coming through. Everybody else
        /// holds a door for whoever is coming, flames or no flames; shutting
        /// one on people is a selfish thing, so it takes a selfish person
        /// (the owner's rule, 2026-09-24). Compassion used to have to be 7 or
        /// more to hold a door at all, which left most of the office shutting
        /// doors in each other's faces.
        /// </summary>
        public int CallousCompassionMaximum = 3;

        /// <summary>
        /// The way out is through the heat -- its approach is inside their
        /// danger distance, or the room beyond it is alight -- and the floor
        /// there is still walkable. This brave or braver runs for it; the
        /// rest, and anyone whose route crosses burning floor, give that door
        /// up and hide in the nearest dead end. Somebody whose own room is
        /// alight runs for it whatever their nerve, because staying is worse.
        /// The owner's choice (2026-09-24): "dash past or hide, by bravery".
        /// </summary>
        public int DashMinimumBravery = 5;

        /// <summary>How long a dash lasts before the choice is made again: three seconds, jittered.</summary>
        public int DashTicks = 150;

        /// <summary>
        /// "Across burning floor" means a straight walk that passes within
        /// this of a burning square, or ends within it. Half a metre: a
        /// square is half a metre across and a person a quarter, so this is
        /// the line past which the walk is through the flames rather than
        /// past them. Nobody dashes across burning floor, however brave.
        /// </summary>
        public int DashClearanceMillimetres = 500;

        /// <summary>
        /// Somebody down inside an open doorway with the crowd pressing on
        /// them from one side is carried on through it by the press rather
        /// than lying in the gap as a plug: anyone upright within this of
        /// them on one side counts as pressing, and while it lasts they are
        /// hauled toward the other side at this speed (millimetres a tick;
        /// 40 is two metres a second, the pace of a body shoved along a
        /// floor). Out through the way out, that is an escape on their back.
        /// On seed 41 the opened exit stood blocked for fifteen seconds by
        /// the people the cruel had shoved to the floor in it (2026-09-24).
        /// </summary>
        public int CarryThroughRadiusMillimetres = 1000;
        public int CarryThroughSpeedMillimetresPerTick = 40;

        public ExitSettings Clone() => (ExitSettings)MemberwiseClone();

        internal void Validate(WorldSettings world)
        {
            int radius = world.OccupancyRadiusMillimetres;
            Settings.Require(DoorwayDepthMillimetres >= radius * 2 && DoorwayDepthMillimetres <= 5000 &&
                             EscapeDepthMillimetres > radius &&
                             EscapeDepthMillimetres <= DoorwayDepthMillimetres - radius, "doorway depth");
            Settings.Require(DoorwayInsetMillimetres >= radius * 2, "doorway inset");
            Settings.Require(DoorOpenTicks >= 1 && DoorTryTicks >= 1 && Settings.Percent(DoorForceChancePercent) &&
                             Settings.Range(DoorForceMinimumTicks, DoorForceMaximumTicks, 1) &&
                             Settings.Range(DoorShoveMinimumTicks, DoorShoveMaximumTicks, 1) &&
                             Settings.Range(DoorAvoidMinimumTicks, DoorAvoidMaximumTicks, 1) &&
                             Settings.Range(DoorCrowdedAvoidMinimumTicks, DoorCrowdedAvoidMaximumTicks, 1) &&
                             Settings.Range(GiveUpGlanceMinimumTicks, GiveUpGlanceMaximumTicks, 1) &&
                             Settings.Range(GiveWayMinimumTicks, GiveWayMaximumTicks, 1), "door timings");
            Settings.Require(GiveWayAsideMillimetres >= 0 && GiveWayInsetMillimetres >= 0, "giving way");
            Settings.Require(ClearTheWayReachMillimetres >= 0 &&
                             Settings.Range(ClearTheWayMinimumAngleDegrees, ClearTheWayMaximumAngleDegrees, 0) &&
                             ClearTheWayMaximumAngleDegrees <= 180, "clearing the way");
            Settings.Require(ApproachInsetMillimetres >= 0 && OutsideTargetMillimetres >= 0 && ArrivalDistanceMillimetres > 0 &&
                             CommitDistanceMillimetres >= 0 && NoSwerveDistanceMillimetres >= 0, "door approach");
            Settings.Require(OpenBonusMillimetres >= 0 && CurrentChoiceBonusMillimetres >= 0 &&
                             ChoiceNoiseMillimetres >= 0 && InFirePenaltyMillimetres >= 0 &&
                             CurrentRoomBonusMillimetres >= 0 && RefugeNoFireMillimetres >= 0 &&
                             RefugeClearRoomMillimetres >= 0 && RefugeSpacePerPersonMillimetres > 0, "door scoring");
            Settings.Require(DoorStrength >= 1 && DoorBurnThroughTicks >= 1 && SwingDoorBurnThroughTicks >= 1, "door strength");
            Settings.Require(CloseReachMillimetres >= 0 && CloseApproachRadiusMillimetres >= 0 &&
                             FireAtDoorRadiusMillimetres >= 0 && EvilCloseMinimum >= 0 &&
                             CallousCompassionMaximum >= 0 && EvilLockMinimum >= EvilCloseMinimum,
                "closing doors");
            Settings.Require(DashMinimumBravery >= 0 && DashTicks >= 1 && DashClearanceMillimetres >= 0, "dashing through the heat");
            Settings.Require(CarryThroughRadiusMillimetres >= 0 && CarryThroughSpeedMillimetresPerTick >= 0, "carried through a doorway");
        }
    }

    /// <summary>
    /// The dials for how physics feels, as opposed to how it is built. The
    /// 3D physics engine does the sums; these decide whether the result
    /// reads as cartoon or as heavy, and they are chosen for fun rather than
    /// realism. 100 percent is the plain physical answer in every case.
    /// </summary>
    [Serializable]
    public sealed class PhysicsFeelSettings
    {
        /// <summary>
        /// Gravity as a percentage of Earth's. Higher makes flung things arc
        /// fast and land with a snap, which reads well from a distant camera;
        /// lower makes everything float.
        /// </summary>
        public int GravityPercent = 150;

        /// <summary>How hard blasts (TNT, popping microwaves) throw things and people, as a percentage.</summary>
        public int BlastStrengthPercent = 100;

        /// <summary>
        /// How much of a blast's push goes upward, as a percentage of its
        /// sideways push. 0 slides things along the floor; 100 sends them up
        /// as steeply as out.
        /// </summary>
        public int BlastLiftPercent = 60;

        /// <summary>How hard people throw things, as a percentage.</summary>
        public int ThrowStrengthPercent = 100;

        /// <summary>How far above level a thrown thing leaves the hand, in degrees.</summary>
        public int ThrowArcDegrees = 20;

        /// <summary>
        /// Extra spin given to anything knocked flying, as a percentage: at 0
        /// flung things only turn as their collisions make them; above 100
        /// they cartwheel for comedy.
        /// </summary>
        public int TumblePercent = 100;

        /// <summary>How grippy the floor is under loose things, as a percentage of the object physics friction.</summary>
        public int FloorGripPercent = 100;

        /// <summary>
        /// The fastest anything may travel, in millimetres per tick (1000 is
        /// 50 metres a second). A safety net: the engine can fling things
        /// absurdly fast when many pile together, and this keeps it funny
        /// rather than broken.
        /// </summary>
        public int MaximumSpeedMillimetresPerTick = 400;

        /// <summary>How tall the walls are for the physics, in millimetres. Taller than they are drawn, so nothing is lobbed over one.</summary>
        public int WallHeightMillimetres = 3000;

        /// <summary>How high the table tops are, in millimetres. Things resting on a table sit here.</summary>
        public int TableHeightMillimetres = 740;

        /// <summary>
        /// How hard a person can push with their own feet: the most their speed
        /// can change in one tick, in millimetres per tick. It is also how hard
        /// they can hold their ground. 3 is a brisk shove: somebody can shoulder
        /// through a loose crowd, but a crowd leaning the other way carries
        /// them with it.
        /// </summary>
        public int PersonPushMillimetresPerTickPerTick = 3;

        /// <summary>How grippy somebody is while they are sliding along the floor off their feet, as friction times 100.</summary>
        public int PersonFloorGripPercent = 60;

        /// <summary>
        /// How hard the people and things pressing on somebody from every side
        /// may squeeze before it hurts, in kilogram-millimetres per tick each
        /// tick. One person leaning on another is about a tenth of this; four
        /// or five pushing into a jammed doorway get there.
        /// </summary>
        public int CrushPressure = 700;

        /// <summary>How long somebody can stand that squeeze before they go down, in ticks.</summary>
        public int CrushTicks = 25;

        /// <summary>
        /// How far behind where they should be a person being dragged may trail
        /// before the one dragging them is held up, in millimetres.
        /// </summary>
        public int DragSlackMillimetres = 600;

        public PhysicsFeelSettings Clone() => (PhysicsFeelSettings)MemberwiseClone();

        /// <summary>
        /// Copies every dial that can change while a run is going, for live
        /// tuning in the editor. The rest are built into the run's physics
        /// world when it starts (floor grip, top speed, wall and table height)
        /// and are left alone. A run tuned this way can no longer be replayed.
        /// </summary>
        public void TakeLiveValuesFrom(PhysicsFeelSettings other)
        {
            GravityPercent = other.GravityPercent;
            BlastStrengthPercent = other.BlastStrengthPercent;
            BlastLiftPercent = other.BlastLiftPercent;
            ThrowStrengthPercent = other.ThrowStrengthPercent;
            ThrowArcDegrees = other.ThrowArcDegrees;
            TumblePercent = other.TumblePercent;
            PersonPushMillimetresPerTickPerTick = other.PersonPushMillimetresPerTickPerTick;
            PersonFloorGripPercent = other.PersonFloorGripPercent;
            CrushPressure = other.CrushPressure;
            CrushTicks = other.CrushTicks;
            DragSlackMillimetres = other.DragSlackMillimetres;
        }

        /// <summary>Whether these values would be accepted, and if not, why.</summary>
        public bool IsValid(out string error)
        {
            try
            {
                Validate();
                error = null;
                return true;
            }
            catch (InvalidOperationException invalid)
            {
                error = invalid.Message;
                return false;
            }
        }

        internal void Validate()
        {
            Settings.Require(GravityPercent > 0 && BlastStrengthPercent >= 0 && BlastLiftPercent >= 0 &&
                             ThrowStrengthPercent >= 0 && TumblePercent >= 0 && FloorGripPercent >= 0,
                "physics feel");
            Settings.Require(ThrowArcDegrees >= 0 && ThrowArcDegrees < 90, "throw arc");
            Settings.Require(MaximumSpeedMillimetresPerTick > 0 && WallHeightMillimetres > 0 &&
                             TableHeightMillimetres > 0 && TableHeightMillimetres < WallHeightMillimetres,
                "physics sizes");
            Settings.Require(PersonPushMillimetresPerTickPerTick > 0 && PersonFloorGripPercent >= 0 &&
                             CrushPressure > 0 && CrushTicks > 0 &&
                             DragSlackMillimetres >= 0,
                "people's physics");
        }
    }

    /// <summary>
    /// Loose objects such as boxes. Object velocities inside the simulation
    /// are hundredths of a millimetre per tick, so friction is in those
    /// units; momentum is kilograms times millimetres per tick.
    /// </summary>
    [Serializable]
    public sealed class ObjectPhysicsSettings
    {
        public int AgentMassGrams = 70000;
        public int Friction = 157;
        public int AgentRestitutionPercent = 30;
        public int ObjectRestitutionPercent = 40;
        public int WallRestitutionPercent = 30;
        public int TripMinimumSpeed = 60;
        public int TripScale = 500;
        public int TripMaximumChancePercent = 75;
        public int StaggerMomentum = 800;
        public int KnockdownMomentum = 1600;

        /// <summary>Box-on-box hits at least this fast (mm per tick) are logged.</summary>
        public int LoggedBoxHitSpeed = 20;

        /// <summary>
        /// Fastest anything turns as it slides, in degrees per tick. 8 is 400
        /// degrees a second, about one turn and a bit: enough to read as a
        /// tumble, slow enough that a kicked box never looks like a top. Only
        /// the smallest things reach it, and only at full speed.
        /// </summary>
        public int SpinMaximum = 8;

        /// <summary>
        /// A thing that drives itself (a robot vacuum), stopped by a wall, a
        /// desk or somebody's foot, waits this long before setting off on its
        /// new heading, and turns by between this many degrees and that many,
        /// either way, drawn from the seed.
        /// </summary>
        public int RoverPauseTicks = 15;
        public int RoverTurnMinimumDegrees = 60;
        public int RoverTurnMaximumDegrees = 150;

        /// <summary>How fast it turns on the spot, in degrees a tick: a quarter turn in about a fifth of a second.</summary>
        public int RoverTurnDegreesPerTick = 10;

        public ObjectPhysicsSettings Clone() => (ObjectPhysicsSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(RoverPauseTicks >= 0 && Settings.Range(RoverTurnMinimumDegrees, RoverTurnMaximumDegrees, 1) &&
                             RoverTurnMaximumDegrees <= 180 && RoverTurnDegreesPerTick >= 1, "robot vacuums");
            Settings.Require(AgentMassGrams > 0 && Friction >= 0 && Settings.Percent(AgentRestitutionPercent) &&
                             Settings.Percent(ObjectRestitutionPercent) && Settings.Percent(WallRestitutionPercent), "object physics");
            Settings.Require(TripMinimumSpeed >= 0 && TripScale > 0 && Settings.Percent(TripMaximumChancePercent) &&
                             StaggerMomentum > 0 && KnockdownMomentum >= StaggerMomentum, "object hits");
            Settings.Require(LoggedBoxHitSpeed >= 0 && SpinMaximum >= 0, "object spin and logging");
        }
    }

    /// <summary>
    /// How much each personality trait changes behaviour. Traits run from 0
    /// to 10 and 5 is an ordinary person, who behaves exactly as the other
    /// settings say. Most effects are "percent per point": with 10 percent
    /// per point, a trait of 8 means 30 percent more and a trait of 2 means
    /// 30 percent less.
    /// </summary>
    [Serializable]
    public sealed class TraitSettings
    {
        /// <summary>Seeded wobble added to each person's walking and sprinting pace, so equal Speed traits still differ a little.</summary>
        public int CalmSpeedJitter = 2;
        public int PanicSpeedJitter = 5;

        /// <summary>Strength: how hard a person shoves a box, as their effective body weight.</summary>
        public int StrengthMassPercentPerPoint = 10;

        /// <summary>Strength: in a knock-down collision, someone this many points stronger only staggers.</summary>
        public int StrengthShrugOffGap = 4;

        /// <summary>Bravery: shorter reaction delay when startled.</summary>
        public int BraveryReactionDelayPercentPerPoint = 10;

        /// <summary>Bravery: how close fire may get before they bolt straight away from it.</summary>
        public int BraveryDangerDistancePercentPerPoint = 5;

        /// <summary>Nervousness: shouting more often and changing their mind more often while running.</summary>
        public int NervousShoutIntervalPercentPerPoint = 10;
        public int NervousDecisionIntervalPercentPerPoint = 10;

        /// <summary>Nervousness: extra chance (percentage points) to zig-zag and to hesitate at each panic decision.</summary>
        public int NervousSwerveChancePerPoint = 5;
        public int NervousHesitateChancePerPoint = 2;

        /// <summary>Nervousness: tripping over their own feet.</summary>
        public int NervousTripPercentPerPoint = 10;

        /// <summary>Compassion and evil: how hard a runner steers around other people.</summary>
        public int CompassionAvoidPercentPerPoint = 15;
        public int EvilAvoidPercentPerPoint = 15;

        /// <summary>Compassion and evil: the closing speed (mm per tick) at which a runner rams someone rather than dodging.</summary>
        public int CompassionBumpSpeedPerPoint = 5;
        public int EvilBumpSpeedPerPoint = 5;
        public int MinimumBumpSpeed = 20;

        /// <summary>Strength: less chance of being knocked out cold (percentage points per point).</summary>
        public int StrengthPassOutPercentPerPoint = 3;

        /// <summary>Strength: more likely to throw a shoulder at a locked door rather than give up (percentage points per point).</summary>
        public int StrengthForceChancePerPoint = 5;

        /// <summary>Strength: people at least this strong damage a locked door when they shove it.</summary>
        public int DoorBreakMinimumStrength = 7;

        /// <summary>Strength: damage per shove for each point of strength from the minimum up (Str 7 does 1, Str 9 does 3).</summary>
        public int DoorDamagePerPoint = 1;

        public TraitSettings Clone() => (TraitSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(CalmSpeedJitter >= 0 && PanicSpeedJitter >= 0, "trait speed jitter");
            Settings.Require(StrengthMassPercentPerPoint >= 0 && StrengthMassPercentPerPoint <= 20 &&
                             StrengthShrugOffGap >= 1, "strength effects");
            Settings.Require(BraveryReactionDelayPercentPerPoint >= 0 && BraveryReactionDelayPercentPerPoint <= 20 &&
                             BraveryDangerDistancePercentPerPoint >= 0 && BraveryDangerDistancePercentPerPoint <= 20,
                "bravery effects");
            Settings.Require(NervousShoutIntervalPercentPerPoint >= 0 && NervousShoutIntervalPercentPerPoint <= 18 &&
                             NervousDecisionIntervalPercentPerPoint >= 0 && NervousDecisionIntervalPercentPerPoint <= 18 &&
                             NervousSwerveChancePerPoint >= 0 && NervousHesitateChancePerPoint >= 0 &&
                             NervousTripPercentPerPoint >= 0 && NervousTripPercentPerPoint <= 20, "nervousness effects");
            Settings.Require(CompassionAvoidPercentPerPoint >= 0 && EvilAvoidPercentPerPoint >= 0 &&
                             CompassionBumpSpeedPerPoint >= 0 && EvilBumpSpeedPerPoint >= 0 && MinimumBumpSpeed > 0,
                "compassion and evil effects");
            Settings.Require(StrengthPassOutPercentPerPoint >= 0 && StrengthForceChancePerPoint >= 0 &&
                             DoorBreakMinimumStrength >= 0 && DoorBreakMinimumStrength <= AgentTraitValues.Maximum &&
                             DoorDamagePerPoint >= 0, "strength at doors and knock-outs");
        }
    }

    /// <summary>
    /// Sticking together (the "Stick together" card, 2026-09-25): how hard
    /// the people a throw catches keep to each other once frightened, and
    /// how much the door their most leaderly member picks sways the rest.
    /// </summary>
    [Serializable]
    public sealed class GroupSettings
    {
        /// <summary>Members further apart than this have lost each other, and the pull is off.</summary>
        public int ReachMillimetres = 8000;

        /// <summary>Members this near the middle of the group are together already: no pull, no hanging back.</summary>
        public int CloseEnoughMillimetres = 1000;

        /// <summary>
        /// The pull comes on gradually over this distance beyond close enough,
        /// rather than at full strength the moment they part: a full-strength
        /// pull at running speed steered members straight into each other,
        /// and a hard collision puts both on the floor.
        /// </summary>
        public int PullRampMillimetres = 2000;

        /// <summary>No pull at all while another member is within arm's reach: they are together, whatever the middle of the group says.</summary>
        public int ElbowRoomMillimetres = 800;

        /// <summary>
        /// The pull toward the rest of the group, as a percentage of a full
        /// step: this, plus per point of nervousness, less per point of
        /// bravery, less again per point of evil. Nought to a hundred.
        /// </summary>
        public int CohesionBasePercent = 60;
        public int CohesionPercentPerNervousness = 3;
        public int CohesionPercentPerBravery = 2;
        public int CohesionPercentPerEvil = 6;

        /// <summary>Evil this high walks off: they ignore the group as they ignore a leader.</summary>
        public int IgnoreMinimumEvil = 7;

        /// <summary>
        /// How much somebody with the rest of the group behind them slows to
        /// let them catch up, as a percentage of their pull: at full pull a
        /// sprinter drops to this much less than their pace. Steering only
        /// turns a person; waiting is a matter of pace.
        /// </summary>
        public int HangBackPercent = 70;

        /// <summary>How much the door the group's anchor runs for is worth to the others, in walk-millimetres of scoring.</summary>
        public int ChoiceBonusMillimetres = 3000;

        /// <summary>How often, give or take, members compare notes on the way out.</summary>
        public int ShareEveryTicks = 100;

        public GroupSettings Clone() => (GroupSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(ReachMillimetres >= 0 && CloseEnoughMillimetres >= 0 && ChoiceBonusMillimetres >= 0 &&
                             ShareEveryTicks >= 1 && IgnoreMinimumEvil >= 0 && PullRampMillimetres >= 1 &&
                             ElbowRoomMillimetres >= 0 && HangBackPercent >= 0 && HangBackPercent <= 100, "sticking together");
        }
    }

    /// <summary>Taking charge: who leads, who follows, and what leaders tell people to do.</summary>
    [Serializable]
    public sealed class LeadershipSettings
    {
        /// <summary>Leadership needed before someone starts telling other people what to do.</summary>
        public int LeaderMinimum = 7;

        /// <summary>How often a leader looks around and forms a plan.</summary>
        public int PlanMinimumTicks = 50;
        public int PlanMaximumTicks = 100;

        /// <summary>How far a shout gathers people, and how far an order carries.</summary>
        public int RallyRangeMillimetres = 5000;
        public int OrderRangeMillimetres = 6000;

        /// <summary>How long an order and a following last before they lapse.</summary>
        public int OrderLastsTicks = 750;
        public int FollowLastsTicks = 400;

        /// <summary>A follower keeps about this far behind before running their own way.</summary>
        public int FollowGapMillimetres = 1500;

        /// <summary>Bravery needed before a leader sends someone at the fire with a bottle.</summary>
        public int OrderedFightMinimumBravery = 5;

        /// <summary>Evil this high never does as it is told.</summary>
        public int DefiantMinimumEvil = 7;

        /// <summary>The chance of doing as told: this, plus per point of nervousness, less per point of bravery.</summary>
        public int ObeyBasePercent = 55;
        public int ObeyPercentPerNervousness = 4;
        public int ObeyPercentPerBravery = 3;

        public LeadershipSettings Clone() => (LeadershipSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(LeaderMinimum >= 0 && DefiantMinimumEvil >= 0 && OrderedFightMinimumBravery >= 0, "who leads");
            Settings.Require(Settings.Range(PlanMinimumTicks, PlanMaximumTicks, 1), "leader planning");
            Settings.Require(RallyRangeMillimetres >= 0 && OrderRangeMillimetres >= 0 && FollowGapMillimetres > 0,
                "leader distances");
            Settings.Require(OrderLastsTicks > 0 && FollowLastsTicks > 0, "how long orders last");
            Settings.Require(ObeyBasePercent >= 0 && ObeyPercentPerNervousness >= 0 && ObeyPercentPerBravery >= 0,
                "doing as told");
        }
    }

    /// <summary>
    /// Fire extinguishers: who picks one up, how the spray works, and what
    /// it does to whoever is caught in it.
    /// </summary>
    [Serializable]
    public sealed class ExtinguisherSettings
    {
        /// <summary>How many ticks of spray one bottle holds.</summary>
        public int FuelTicks = 300;

        /// <summary>Bravery needed to take on the flames, and compassion needed to hose down a burning person.</summary>
        public int FightMinimumBravery = 7;
        public int SaveMinimumCompassion = 7;

        /// <summary>
        /// Somebody stood an extinguisher down in front of them. For the next
        /// stretch of ticks they need this much less nerve to pick it up: a
        /// bottle at your feet is a far easier thing to reach for than one
        /// across the room. How long they keep it in mind, and how far away
        /// they notice one being put down, are below.
        /// </summary>
        public int OfferedBraveryBonus = 3;
        public int OfferedTicks = 400;
        public int OfferedNoticeRangeMillimetres = 6000;

        /// <summary>Nobody takes on a fire bigger than this many burning squares (saving someone is always worth it).</summary>
        public int FightMaximumFireCells = 24;

        /// <summary>They will cross this much floor for an extinguisher, and hold it once this close.</summary>
        public int FetchRangeMillimetres = 9000;
        public int PickUpDistanceMillimetres = 400;

        /// <summary>How far they will go to hose down someone who is alight.</summary>
        public int SaveRangeMillimetres = 8000;

        /// <summary>
        /// Give up fetching after this long, and stop fighting after this long.
        ///
        /// Fetching was ten seconds while the bottle and the fire both had to
        /// be in the room somebody was standing in. Now that they can walk to a
        /// fire anywhere in the building, ten seconds is not enough to cross
        /// it: they would set off with the bottle, get as far as the doorway,
        /// and give up on the way. Somebody genuinely getting nowhere is still
        /// caught, by the blocked counter rather than by the clock.
        /// </summary>
        public int FetchTimeoutTicks = 1500;
        public int FightTimeoutTicks = 1500;

        /// <summary>
        /// Getting nowhere for this long on the errand -- pressed against a
        /// wall, a table or a crowd -- and they give it up. The comment above
        /// promised this and nothing did it: somebody wedged stood there for
        /// the whole thirty seconds.
        /// </summary>
        public int BlockedGiveUpTicks = 50;

        /// <summary>
        /// How much of their usual keep-away distance from the flames someone
        /// with an extinguisher in their hands still keeps: the bottle makes
        /// them braver, up to a point.
        /// </summary>
        public int DangerTolerancePercent = 50;

        /// <summary>How close they get to what they are hosing down before they stop walking.</summary>
        public int StandOffMillimetres = 2000;

        /// <summary>The jet: this far, this wide, and this many squares put out per tick.</summary>
        public int SprayRangeMillimetres = 3000;
        public int SprayConeDegrees = 30;
        public int CellsPerTick = 1;

        /// <summary>How wide the jet sweeps side to side: this much, less this much per strength point.</summary>
        public int SweepDegrees = 24;
        public int SweepDegreesPerStrengthPoint = 3;

        /// <summary>How far the jet shoves someone, and how long before it can knock them over again.</summary>
        public int BlastPushMillimetres = 400;
        public int BlastRecoveryTicks = 100;

        /// <summary>
        /// The recoil: this far back per tick, less this much per point of
        /// strength, so anyone strong holds it steady. At or below the last
        /// value, the recoil puts them on the floor.
        /// </summary>
        public int RecoilPushMillimetres = 60;
        public int RecoilPushPerStrength = 12;
        public int RecoilFloorsMaximumStrength = 0;

        public ExtinguisherSettings Clone() => (ExtinguisherSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(FuelTicks > 0 && FightMaximumFireCells >= 0, "extinguisher fuel");
            Settings.Require(FightMinimumBravery >= 0 && SaveMinimumCompassion >= 0, "who fights a fire");
            Settings.Require(OfferedBraveryBonus >= 0 && OfferedTicks > 0 && OfferedNoticeRangeMillimetres >= 0,
                "noticing an extinguisher somebody put down");
            Settings.Require(FetchRangeMillimetres >= 0 && PickUpDistanceMillimetres > 0 && SaveRangeMillimetres >= 0,
                "extinguisher distances");
            Settings.Require(FetchTimeoutTicks > 0 && FightTimeoutTicks > 0 && BlockedGiveUpTicks > 0, "extinguisher timeouts");
            Settings.Require(SprayRangeMillimetres > 0 && SprayConeDegrees > 0 && SprayConeDegrees <= 180 && CellsPerTick > 0 &&
                StandOffMillimetres > 0 && StandOffMillimetres <= SprayRangeMillimetres, "the spray");
            Settings.Require(SweepDegrees >= 0 && SweepDegreesPerStrengthPoint >= 0, "the sweep");
            Settings.Require(BlastPushMillimetres >= 0 && BlastRecoveryTicks >= 0, "the blast");
            Settings.Require(Settings.Percent(DangerTolerancePercent), "extinguisher nerve");
            Settings.Require(RecoilPushMillimetres >= 0 && RecoilPushPerStrength >= 0 && RecoilFloorsMaximumStrength >= 0,
                "the recoil");
        }
    }

    /// <summary>
    /// Boxes, chairs and tables catching fire. Things heat up while flames
    /// are close and catch once hot for long enough; cardboard catches
    /// sooner than wood, and wood burns longer.
    /// </summary>
    /// <summary>
    /// One kind of loose object: how far it slides when kicked, and how
    /// readily it burns. Friction is a percentage of the shared floor
    /// friction, so 50 slides twice as far and 200 half as far; an object
    /// that never burns has <see cref="IgniteTicks"/> 0.
    /// </summary>
    [Serializable]
    public sealed class ObjectKindSettings
    {
        public const int KindCount = 22;

        public PhysicsObjectKind Kind;
        public int FrictionPercent = 100;
        public int IgniteTicks = 75;
        public int BurnMinimumTicks = 400;
        public int BurnMaximumTicks = 750;

        /// <summary>
        /// How hard a blow this thing survives, as momentum in kilograms times
        /// millimetres per tick. A hit above it smashes the thing. Zero means it
        /// never breaks, however hard it is hit.
        /// </summary>
        public int BreakMomentum;

        /// <summary>
        /// How far an electrical thing throws things about when it goes off.
        /// Zero means it does not go off at all.
        /// </summary>
        public int PopRadiusMillimetres;

        /// <summary>How fast the blast sends loose things, in millimetres per tick.</summary>
        public int PopSpeed;

        /// <summary>How many floor squares around it the blast can set alight.</summary>
        public int PopIgniteCells;

        /// <summary>
        /// It goes off at the end of its burn rather than the moment the
        /// flames reach it: a robot vacuum rides about alight for a good
        /// while, and then its battery goes. The pop is the same pop, from
        /// the three values above; only when it happens differs.
        /// </summary>
        public bool PopsWhenBurntOut;

        /// <summary>
        /// When it goes off it empties itself over everything around it: a
        /// fire extinguisher bursting in the flames puts out every burning
        /// square, thing and person within its pop radius, the way a spray
        /// would, and is spent (2026-09-25).
        /// </summary>
        public bool PopDouses;

        /// <summary>
        /// What this kind of thing is for, rather than what it is made of.
        ///
        /// These used to be decided by naming the kind in the rules -- "is it a
        /// chair or an office chair", "is it an extinguisher" -- in nine
        /// different places. Written down here instead, a new kind of thing
        /// that people can sit on, or a new piece of equipment, is a row in
        /// this table rather than an edit to every rule that might care.
        /// </summary>
        public bool CanBeSatOn;

        /// <summary>
        /// Equipment: something kept where it is until somebody needs it, like
        /// an extinguisher on its bracket. Nobody tidies it away, wedges a door
        /// with it, or drops it the moment they are frightened.
        /// </summary>
        public bool IsEquipment;

        /// <summary>
        /// It pops the first time it goes over: a standing lamp's bulb bursting
        /// as it hits the floor. A small crack rather than a bang (see
        /// <see cref="PopRadiusMillimetres"/> for the things that go off).
        /// </summary>
        public bool PopsWhenTipped;

        /// <summary>
        /// Whatever was authored as part of it comes loose when it goes over:
        /// a lamp's shade. Parts are their own kind of thing, kept out of the
        /// world until then (see <c>PhysicsObjectDefinition.PartOfObjectId</c>).
        /// </summary>
        public bool ShedsPartsWhenTipped;

        /// <summary>
        /// It moves about the floor by itself, like a robot vacuum: trundling
        /// at <see cref="CruiseSpeedMillimetresPerTick"/> and turning when it
        /// meets a wall, a table or anything else that stops it.
        /// </summary>
        public bool DrivesItself;

        /// <summary>How fast a thing that drives itself goes, in millimetres per tick.</summary>
        public int CruiseSpeedMillimetresPerTick;

        /// <summary>
        /// How long this kind burns in one place before the floor under it
        /// catches, stretched or squeezed a little each time it catches. Zero
        /// means the shared <see cref="FlammableSettings.FloorIgniteRestTicks"/>.
        /// A waste bin smoulders about ten seconds first (2026-09-26): long
        /// enough for somebody brave to reach it with a bottle.
        /// </summary>
        public int FloorIgniteRestTicks;

        public ObjectKindSettings Clone() => (ObjectKindSettings)MemberwiseClone();

        /// <summary>The office's things, in enum order.</summary>
        public static ObjectKindSettings[] Defaults()
        {
            return new[]
            {
                Entry(PhysicsObjectKind.Box, 100, 75, 400, 750),

                // Wooden. It is shoved, tipped and rolled like anything else,
                // but it never smashes: a room left full of broken stumps read
                // as a demolition rather than as a fire.
                SatOn(Entry(PhysicsObjectKind.Chair, 120, 150, 600, 900)),

                // Castors: it rolls away across the floor.
                SatOn(Entry(PhysicsObjectKind.OfficeChair, 35, 175, 600, 900)),
                // Paper in a plastic tub: it catches quickly, then smoulders
                // a good while before the carpet under it goes (2026-09-26,
                // the owner's slow bin fire, so somebody brave has time to
                // reach it). It used to burn out in five to nine seconds,
                // before the floor ever caught.
                SmouldersFor(Entry(PhysicsObjectKind.WasteBin, 80, 50, 1000, 1500), 500),

                // Earth and green leaves: it never catches.
                Entry(PhysicsObjectKind.PottedPlant, 200, 0, 0, 0),
                Entry(PhysicsObjectKind.Bag, 90, 100, 300, 500),

                // Hard plastic on hard floor: it skitters. Its battery goes
                // off when the flames reach it: a sharp crack rather than a
                // proper bang, enough to make everybody nearby jump and to
                // throw burning plastic onto the desk it was sitting on.
                Popping(Entry(PhysicsObjectKind.Laptop, 55, 200, 200, 400), 900, 45, 1),

                // Steel, and full of pressure: equipment rather than clutter,
                // and it takes four seconds in the flames to heat through,
                // then bursts (the owner asked, 2026-09-25): a bang the size
                // of a socket's, everyone within 1.6 m off their feet, no
                // fresh fire, and its contents over every flame in that
                // circle. After that it is a spent bottle nobody fetches.
                Dousing(Equipment(Popping(Entry(PhysicsObjectKind.Extinguisher, 90, 200, 1, 1), 1600, 60, 0))),

                // Stiff leather: it slides less than a soft bag and burns slowly.
                Entry(PhysicsObjectKind.Briefcase, 110, 175, 350, 600),

                // Electrical. Neither burns for long: the flames reach them and
                // they go off, which is the point of them. A microwave clears a
                // 2.2 m circle, a socket 1.4 m, a laptop only 0.9 m.
                Popping(Entry(PhysicsObjectKind.Microwave, 150, 120, 60, 90), 2200, 70, 3),

                // Bolted to the wall, so it never slides anywhere.
                Popping(Entry(PhysicsObjectKind.WallSocket, 1000, 90, 40, 60), 1400, 55, 2),

                // A pre-authored dormant heap, claimed and placed when a table
                // is smashed: already wreckage, so it never catches again.
                Entry(PhysicsObjectKind.TableWreck, 250, 0, 0, 0),

                // The main fuse box. Bolted to the wall like a socket, and the
                // biggest bang in the building by a long way: a 3.2 m circle
                // against the microwave's 2.2, and it sets five squares of
                // floor alight rather than three.
                Popping(Entry(PhysicsObjectKind.FuseBox, 1000, 110, 50, 80), 3200, 95, 5),

                // The rest of the office, added 2026-09-24. Tall things tip
                // because their shape is tall and their feet grip; nothing
                // per kind is written for that.

                // Steel and glass, and heavy: a blast tips it, a crowd does not.
                Entry(PhysicsObjectKind.VendingMachine, 150, 220, 500, 800),

                // Steel drawers full of paper: slow to catch, burns a long while.
                Entry(PhysicsObjectKind.Cabinet, 140, 180, 500, 800),

                // Books and files on thin shelves: quick to catch, easy to tip.
                Entry(PhysicsObjectKind.Shelves, 130, 110, 500, 800),

                // On castors like an office chair, so a shove sends it rolling
                // across the room. Its toner goes off in the flames: a bang
                // bigger than a laptop's, smaller than a socket's.
                Popping(Entry(PhysicsObjectKind.CopyMachine, 30, 150, 300, 500), 1200, 50, 2),

                // Light, tall and on wheels: it goes over at a shove.
                Entry(PhysicsObjectKind.Whiteboard, 30, 200, 300, 500),

                // It goes over at a touch, and when it does its bulb pops and
                // its shade comes off.
                TipsAndPops(Entry(PhysicsObjectKind.StandingLamp, 100, 150, 300, 500), sheds: true),

                // The shade: paper on a wire frame. Part of the lamp until the
                // lamp goes over, then a loose thing on the floor.
                Entry(PhysicsObjectKind.LampShade, 90, 100, 200, 350),

                // A robot vacuum: it drives itself about at a Roomba's pace
                // (16 mm a tick is 0.8 m/s), turns at walls and desks, and
                // once alight it rides about burning for half a minute to a
                // minute -- a lot of health, the owner asked -- before its
                // battery goes off with a laptop-sized bang.
                SelfDriving(PoppingAtTheEnd(Entry(PhysicsObjectKind.RobotVacuum, 60, 120, 1500, 3000), 1000, 45, 2), 16),

                // A fire alarm bell: bolted to the wall like a socket, and the
                // flames reaching it set it off with a laptop-sized crack.
                // After that it is silent.
                Popping(Entry(PhysicsObjectKind.AlarmSounder, 1000, 80, 20, 40), 800, 40, 1)
            };
        }

        /// <summary>The same kind, but one that goes off when its burn ends rather than when the flames reach it.</summary>
        private static ObjectKindSettings PoppingAtTheEnd(ObjectKindSettings kind, int radius, int speed, int igniteCells)
        {
            Popping(kind, radius, speed, igniteCells);
            kind.PopsWhenBurntOut = true;
            return kind;
        }

        /// <summary>The same kind, but one that burns this long in one place before the floor under it catches.</summary>
        private static ObjectKindSettings SmouldersFor(ObjectKindSettings kind, int floorIgniteRestTicks)
        {
            kind.FloorIgniteRestTicks = floorIgniteRestTicks;
            return kind;
        }

        /// <summary>The same kind, but one that pops the first time it goes over, and may shed the parts authored onto it.</summary>
        private static ObjectKindSettings TipsAndPops(ObjectKindSettings kind, bool sheds)
        {
            kind.PopsWhenTipped = true;
            kind.ShedsPartsWhenTipped = sheds;
            return kind;
        }

        /// <summary>The same kind, but one that drives itself about at this speed.</summary>
        private static ObjectKindSettings SelfDriving(ObjectKindSettings kind, int cruiseSpeedMillimetresPerTick)
        {
            kind.DrivesItself = true;
            kind.CruiseSpeedMillimetresPerTick = cruiseSpeedMillimetresPerTick;
            return kind;
        }

        /// <summary>The same kind, but one somebody can sit on.</summary>
        private static ObjectKindSettings SatOn(ObjectKindSettings kind)
        {
            kind.CanBeSatOn = true;
            return kind;
        }

        /// <summary>The same kind, but equipment rather than clutter.</summary>
        private static ObjectKindSettings Equipment(ObjectKindSettings kind)
        {
            kind.IsEquipment = true;
            return kind;
        }

        /// <summary>The same kind, but one whose pop puts the fire out around it rather than spreading it.</summary>
        private static ObjectKindSettings Dousing(ObjectKindSettings kind)
        {
            kind.PopDouses = true;
            return kind;
        }

        /// <summary>The same kind, but one a hard enough blow smashes.</summary>
        private static ObjectKindSettings Breakable(ObjectKindSettings kind, int breakMomentum)
        {
            kind.BreakMomentum = breakMomentum;
            return kind;
        }

        /// <summary>The same kind, but one that goes off when the flames reach it.</summary>
        private static ObjectKindSettings Popping(ObjectKindSettings kind, int radius, int speed, int igniteCells)
        {
            kind.PopRadiusMillimetres = radius;
            kind.PopSpeed = speed;
            kind.PopIgniteCells = igniteCells;
            return kind;
        }

        private static ObjectKindSettings Entry(PhysicsObjectKind kind, int friction, int ignite, int burnMinimum, int burnMaximum)
        {
            return new ObjectKindSettings
            {
                Kind = kind,
                FrictionPercent = friction,
                IgniteTicks = ignite,
                BurnMinimumTicks = burnMinimum,
                BurnMaximumTicks = burnMaximum
            };
        }

        internal void Validate()
        {
            Settings.Require(FrictionPercent > 0 && FrictionPercent <= 1000, "object friction");
            Settings.Require(IgniteTicks >= 0, "object ignite time");
            Settings.Require(BreakMomentum >= 0 && PopRadiusMillimetres >= 0 && PopSpeed >= 0 && PopIgniteCells >= 0,
                "breaking and popping");
            Settings.Require(IgniteTicks == 0 || Settings.Range(BurnMinimumTicks, BurnMaximumTicks, 1), "object burn time");
            Settings.Require(FloorIgniteRestTicks >= 0, "how long a thing smoulders before the floor catches");
            Settings.Require(CruiseSpeedMillimetresPerTick >= 0 && (!DrivesItself || CruiseSpeedMillimetresPerTick > 0),
                "a thing that drives itself needs a speed");
        }
    }

    [Serializable]
    public sealed class FlammableSettings
    {
        /// <summary>Flames (a burning square or burning thing) this close to a thing's edge heat it.</summary>
        public int HeatDistanceMillimetres = 500;

        /// <summary>How each kind of loose object slides and burns; one entry per kind.</summary>
        public ObjectKindSettings[] Kinds = ObjectKindSettings.Defaults();

        /// <summary>Ticks of heat before each kind catches fire.</summary>
        public int BoxIgniteTicks = 75;
        public int ChairIgniteTicks = 150;
        public int TableIgniteTicks = 250;

        /// <summary>How long each kind burns before it is charred.</summary>
        public int BoxBurnMinimumTicks = 400;
        public int BoxBurnMaximumTicks = 750;
        public int ChairBurnMinimumTicks = 600;
        public int ChairBurnMaximumTicks = 900;
        public int TableBurnMinimumTicks = 1000;
        public int TableBurnMaximumTicks = 1500;

        /// <summary>A burning thing resting this long in one floor square sets it alight.</summary>
        public int FloorIgniteRestTicks = 50;

        /// <summary>A person this close to a burning thing's edge touches it (and catches fire).</summary>
        public int TouchGapMillimetres = 50;

        /// <summary>
        /// A table's weight, by the floor it covers: a 1.2 by 0.7 m desk comes
        /// out at 21 kg, the 5.4 by 1 m meeting table at 135 kg. Tables are
        /// bodies like anything else, so this is what decides how far a crowd
        /// shoves one and how hard it is to tip over.
        /// </summary>
        public int TableMassGramsPerSquareMetre = 25000;

        /// <summary>How well a table grips the floor, as the engine's friction times 100.</summary>
        public int TableFloorGripPercent = 80;

        public FlammableSettings Clone()
        {
            var copy = (FlammableSettings)MemberwiseClone();
            if (Kinds != null)
            {
                copy.Kinds = new ObjectKindSettings[Kinds.Length];
                for (int i = 0; i < Kinds.Length; i++)
                {
                    copy.Kinds[i] = Kinds[i]?.Clone();
                }
            }

            return copy;
        }

        /// <summary>How this kind of object slides and burns.</summary>
        public ObjectKindSettings Of(PhysicsObjectKind kind) => Kinds[(int)kind];

        internal void Validate()
        {
            Settings.Require(HeatDistanceMillimetres >= 0 && BoxIgniteTicks >= 1 && ChairIgniteTicks >= 1 &&
                             TableIgniteTicks >= 1, "heating up");
            Settings.Require(Settings.Range(BoxBurnMinimumTicks, BoxBurnMaximumTicks, 1) &&
                             Settings.Range(ChairBurnMinimumTicks, ChairBurnMaximumTicks, 1) &&
                             Settings.Range(TableBurnMinimumTicks, TableBurnMaximumTicks, 1), "burn times");
            Settings.Require(FloorIgniteRestTicks >= 1 && TouchGapMillimetres >= 0, "burning things");
            Settings.Require(TableMassGramsPerSquareMetre > 0 && TableFloorGripPercent >= 0, "table weight");
            Settings.Require(Kinds != null && Kinds.Length == ObjectKindSettings.KindCount, "one entry per kind of object");
            for (int i = 0; i < Kinds.Length; i++)
            {
                Settings.Require((int)Kinds[i].Kind == i, "kinds in enum order");
                Kinds[i].Validate();
            }
        }
    }

    /// <summary>Picking up, carrying, setting down, dropping and throwing boxes and chairs.</summary>
    [Serializable]
    public sealed class ItemSettings
    {
        /// <summary>The heaviest item someone can lift: this much, plus the next value per strength point.</summary>
        public int CarryBaseGrams = 5000;
        public int CarryGramsPerStrength = 2500;

        /// <summary>A load as heavy as their limit slows a carrier by this percentage (lighter loads less).</summary>
        public int CarrySlowdownPercent = 40;

        /// <summary>Chance that a calm person's fresh decision is to tidy up the nearest item they can lift.</summary>
        public int TidyChancePercent = 12;

        /// <summary>How often a calm person who is choosing what to do next goes to sit down.</summary>
        public int SitChancePercent = 14;

        /// <summary>They will cross this much floor for a free chair, and are sitting once this close to one.</summary>
        public int SitSearchDistanceMillimetres = 6000;
        public int SitArrivalDistanceMillimetres = 600;

        /// <summary>How long they stay in the chair.</summary>
        public int SitMinimumTicks = 250;
        public int SitMaximumTicks = 1000;

        /// <summary>Getting out of a chair: this long, less a little for the nervous.</summary>
        public int StandUpTicks = 40;
        public int StandUpTicksPerNervousness = 2;

        /// <summary>How hard the chair is shoved back as they stand, in millimetres per tick.</summary>
        public int StandUpShoveSpeed = 8;

        /// <summary>How far a chair scoots in to seat someone settling onto it.</summary>
        public int SitScootMillimetres = 120;

        /// <summary>
        /// Sitting down is done in parts rather than in one jump: the chair is
        /// pulled this far out from the table over <see cref="SitPullTicks"/>,
        /// the person lowers onto the seat over <see cref="SitLowerTicks"/>,
        /// and then they ride it back in. Getting up runs the same the other
        /// way round. Somebody startled skips all of it and leaps clear.
        /// </summary>
        public int SitPullOutMillimetres = 300;
        public int SitPullTicks = 12;
        // Half a second each way. It was a fifth, which had somebody cross
        // half a metre of floor onto or off the seat in ten ticks: a lunge,
        // not a sit.
        public int SitLowerTicks = 25;

        /// <summary>
        /// How hard a chair somebody leapt out of is sent over backwards, in
        /// millimetres a tick. A shove, not a throw: at twice this it clatters
        /// into the meeting table hard enough to shift it, which is not what
        /// standing up quickly should do. What tips it over is the lift and
        /// the spin, not the speed.
        /// </summary>
        public int JumpUpKnockOverSpeed = 30;

        public int FetchRangeMillimetres = 4000;

        /// <summary>They carry it at least this far before setting it down.</summary>
        public int CarryMinimumDistanceMillimetres = 1500;

        /// <summary>How far past touching an item someone can reach to pick it up.</summary>
        public int ReachMillimetres = 150;

        public int PickUpTicks = 25;
        public int SetDownTicks = 20;

        /// <summary>Gap between a carrier and the item held in front of them.</summary>
        public int HoldGapMillimetres = 20;

        /// <summary>Anyone at least this nervous drops what they carry when frightened; the rest throw it.</summary>
        public int DropNervousness = 6;

        /// <summary>Runners at least this strong hurl an item in their way instead of kicking it.</summary>
        public int HurlMinimumStrength = 6;

        /// <summary>Runners at least this evil hurl it at the nearest person within the aim range.</summary>
        public int EvilAimMinimum = 7;
        public int AimRangeMillimetres = 4000;

        /// <summary>Throw speed (mm/tick) = impulse × (strength + 5) ÷ (item kg + 5), at least the minimum.</summary>
        public int ThrowImpulse = 60;
        public int ThrowMinimumSpeed = 20;

        /// <summary>A thrown item's hit counts as this many times its sliding momentum (it strikes the body, not the feet).</summary>
        public int ThrowHitMultiplier = 3;

        /// <summary>How far to either side of straight ahead a frightened person's throw can veer.</summary>
        public int PanicThrowSpreadDegrees = 60;

        public ItemSettings Clone() => (ItemSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(CarryBaseGrams >= 0 && CarryGramsPerStrength >= 0 && Settings.Percent(CarrySlowdownPercent), "carrying");
            Settings.Require(Settings.Percent(TidyChancePercent) && FetchRangeMillimetres >= 0 &&
                             CarryMinimumDistanceMillimetres >= 0 && ReachMillimetres >= 0 && PickUpTicks >= 1 &&
                             SetDownTicks >= 1 && HoldGapMillimetres >= 0, "tidying up");
            Settings.Require(DropNervousness >= 0 && HurlMinimumStrength >= 0 && EvilAimMinimum >= 0 && AimRangeMillimetres >= 0 &&
                             ThrowImpulse > 0 && ThrowMinimumSpeed >= 1 && ThrowHitMultiplier >= 1 &&
                             PanicThrowSpreadDegrees >= 0 && PanicThrowSpreadDegrees <= 180, "throwing");
            Settings.Require(SitScootMillimetres >= 0 && SitPullOutMillimetres >= 0 && SitPullTicks >= 1 &&
                             SitLowerTicks >= 1 && JumpUpKnockOverSpeed >= 0, "sitting down");
        }
    }

    /// <summary>
    /// TNT. The player picks a wall and blows a hole through it: a ragged gap
    /// wider than a door that nobody can lock, shut or open, because it is not a
    /// door. The bang throws whatever is loose nearby away from it and knocks
    /// anybody close off their feet.
    /// </summary>
    [Serializable]
    public sealed class BlastSettings
    {
        /// <summary>How wide a hole is. Half again as wide as a door, so it reads as a breach.</summary>
        public int HoleWidthMillimetres = 1500;

        /// <summary>How far from a wall a click still counts as that wall.</summary>
        public int WallReachMillimetres = 900;

        /// <summary>How much solid wall must remain between a hole and any other opening.</summary>
        public int ClearanceMillimetres = 500;

        /// <summary>Anybody this close is blown off their feet, and slid this far.</summary>
        public int KnockDownRadiusMillimetres = 1500;
        public int ShoveDistanceMillimetres = 600;

        /// <summary>Loose things this close are flung away from it, at this speed.</summary>
        public int ThrowRadiusMillimetres = 2500;
        public int ThrowSpeedMillimetresPerTick = 90;

        /// <summary>How far the bang is heard, and how far it frightens people.</summary>
        public int BangHearingRadiusMillimetres = 20000;
        public int BangAlarmRadiusMillimetres = 12000;

        public BlastSettings Clone() => (BlastSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(HoleWidthMillimetres > 0 && WallReachMillimetres > 0 && ClearanceMillimetres >= 0,
                "blast holes");
            Settings.Require(KnockDownRadiusMillimetres >= 0 && ShoveDistanceMillimetres >= 0 &&
                             ThrowRadiusMillimetres >= 0 && ThrowSpeedMillimetresPerTick >= 0, "the blast");
            Settings.Require(BangHearingRadiusMillimetres >= BangAlarmRadiusMillimetres, "the bang");
        }
    }

    /// <summary>
    /// Things wedged in doorways. Anything resting in a doorway jams the door:
    /// it cannot be opened and it cannot be shut. Frightened people with nowhere
    /// left to run do it on purpose to keep the fire out, and the cruel do it to
    /// keep other people out. Somebody strong enough heaves the obstruction
    /// clear; everybody else treats the door as shut and looks elsewhere.
    /// </summary>
    [Serializable]
    public sealed class BlockadeSettings
    {
        /// <summary>How close to the wall line a thing has to rest to be in the leaf's way.</summary>
        public int BlockGapMillimetres = 150;

        /// <summary>How far short of the wall line a barricade is set down.</summary>
        public int BarricadeSpotGapMillimetres = 80;

        /// <summary>This strong, and they heave an obstruction out of the way instead of giving up.</summary>
        public int ShoveMinimumStrength = 7;

        /// <summary>How long the heaving takes, and how fast it sends the obstruction along the wall.</summary>
        public int ShoveTicks = 40;
        public int ShoveSpeedBase = 30;
        public int ShoveSpeedPerStrength = 8;

        /// <summary>
        /// How long somebody strong keeps at a doorway heaped with fallen boxes
        /// before they too give it up and go round: eight seconds, a little
        /// different for each of them -- a real go at it, never ten seconds of
        /// standing still. Everybody else goes round the moment
        /// they see the heap (2026-09-26). Without a limit, a strong person
        /// wedged against the heap where they could not work it pushed at it
        /// for the rest of the round.
        /// </summary>
        public int StrongGiveUpOnAHeapTicks = 400;

        /// <summary>
        /// How near a strong person must be to a heaped doorway before that
        /// patience starts to run: three metres, close enough to be having a
        /// go at it. Seen from across the room, the heap is simply where they
        /// are heading; the clock starting there would have them give up
        /// before they arrived.
        /// </summary>
        public int StrongTryTheHeapWithinMillimetres = 3000;

        /// <summary>This nervous, and somebody sheltering wedges the door of the room they are in.</summary>
        public int BarricadeNervousMinimum = 7;

        /// <summary>This cruel, and they wedge it to keep other people out.</summary>
        public int BarricadeEvilMinimum = 7;

        /// <summary>How far they will cross a room for something to wedge the door with.</summary>
        public int BarricadeFetchRangeMillimetres = 5000;

        /// <summary>
        /// Fixed rather than drawn, so this feature adds no randomness of its
        /// own and the change to the recorded runs stays easy to account for.
        /// </summary>
        public int BarricadeTimeoutTicks = 500;
        public int BarricadeSetDownTicks = 25;

        /// <summary>
        /// How long they shuffle about getting nowhere before abandoning it. Long
        /// enough to manoeuvre something bulky around a small room, and still
        /// under the second and a half nothing may stand still for.
        /// </summary>
        public int BarricadeBlockedGiveUpTicks = 60;

        public BlockadeSettings Clone() => (BlockadeSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(BlockGapMillimetres >= 0 && BarricadeSpotGapMillimetres >= 0, "wedging doorways");
            Settings.Require(StrongGiveUpOnAHeapTicks >= 1 && StrongTryTheHeapWithinMillimetres >= 0,
                "how long the strong keep at a heap");
            Settings.Require(ShoveMinimumStrength >= 0 && ShoveTicks >= 1 && ShoveSpeedBase >= 0 &&
                             ShoveSpeedPerStrength >= 0, "heaving an obstruction clear");
            Settings.Require(BarricadeNervousMinimum >= 0 && BarricadeEvilMinimum >= 0 &&
                             BarricadeFetchRangeMillimetres >= 0 && BarricadeTimeoutTicks >= 1 &&
                             BarricadeSetDownTicks >= 1 && BarricadeBlockedGiveUpTicks >= 1, "barricading");
        }
    }

    /// <summary>
    /// Fire alarms. Somebody who has taken in that there is a fire and who is
    /// brave, thinks of other people or is used to being listened to walks
    /// over to the nearest pull station and hits it; every bell in the
    /// building then rings, and everybody who hears one takes fright exactly
    /// as if they had seen the flames (the owner's rule, 2026-09-25). The
    /// bells ring again every few seconds, each on its own beat.
    /// </summary>
    [Serializable]
    public sealed class AlarmSettings
    {
        /// <summary>Turn the alarms off altogether, to see the building without them.</summary>
        public bool Enabled = true;

        /// <summary>
        /// Whether the player may pull an alarm by clicking it. On in the code
        /// defaults, for a later level; the office switches it off
        /// (2026-09-26, the owner: "player can't pull alarm, only agents").
        /// </summary>
        public bool PlayerMayPull = true;

        /// <summary>
        /// How far somebody will divert to hit an alarm, as a walk. Eight
        /// metres since prototype 3 (2026-09-25): the office keeps one pull
        /// station, at the far west end of the corridor beside the fuse box
        /// room, and at four metres nobody in the office or the corridor
        /// would ever have thought of it.
        /// </summary>
        public int ReachMillimetres = 8000;

        /// <summary>How close they must get to hit it.</summary>
        public int ArrivalMillimetres = 500;

        /// <summary>How long hitting it takes.</summary>
        public int PressTicks = 20;

        /// <summary>If they cannot get to it in this long, they give up and run.</summary>
        public int FetchTimeoutTicks = 400;

        /// <summary>How far a ringing bell is heard, and how far it is alarming. A bell fills its own room.</summary>
        public int BellHearingRadiusMillimetres = 14000;
        public int BellAlarmRadiusMillimetres = 14000;

        /// <summary>Who thinks to raise the alarm: a leader, somebody who thinks of others, or somebody brave (the owner asked for the brave, 2026-09-25).</summary>
        public int PullMinimumLeadership = 6;
        public int PullMinimumCompassion = 6;
        public int PullMinimumBravery = 6;

        /// <summary>
        /// How often a ringing bell rings again, give or take: each bell draws
        /// its own beat, so no two ring on one tick. Six seconds: somebody who
        /// was out of earshot behind a shut door hears the next one once the
        /// door opens.
        /// </summary>
        public int RepeatTicks = 300;

        public AlarmSettings Clone() => (AlarmSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(ReachMillimetres >= 0 && ArrivalMillimetres > 0 && PressTicks >= 1 &&
                             FetchTimeoutTicks >= 1 && RepeatTicks >= 1, "fire alarms");
            Settings.Require(BellHearingRadiusMillimetres >= 0 &&
                             BellAlarmRadiusMillimetres <= BellHearingRadiusMillimetres, "alarm bells");
            Settings.Require(PullMinimumLeadership >= 0 && PullMinimumCompassion >= 0 && PullMinimumBravery >= 0,
                "who raises the alarm");
        }
    }

    /// <summary>
    /// The player's purse, and what each card costs. The purse starts at
    /// <see cref="Starting"/>, every card spends some, and every person who gets
    /// out alive pays some back. Nothing else refills it, so a run where nobody
    /// is saved runs the player dry.
    /// </summary>
    [Serializable]
    public sealed class PurseSettings
    {
        /// <summary>
        /// Whether there is a purse at all. Off (the office level since
        /// prototype 3, 2026-09-25, the owner's call: "remove influence
        /// points for now, keep the system intact"), every door, alarm and
        /// card is free, nothing is paid in, and the display shows no purse.
        /// The rules below still stand, and a level that wants them turns
        /// this back on. The code default stays on so the tests of the purse
        /// still test it; the level asset turns it off (see
        /// <c>LevelDefinition</c>).
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// What the player starts the run with: thirty, which is one card or
        /// one pull of a fire alarm (the owner's call, 2026-09-24: "start with
        /// one random card, and 30 activity points"). It was nothing, so that
        /// the building had to get into trouble before there was anything to
        /// spend; thirty is one move before it does -- enough to raise the
        /// alarm on a fire nobody else has seen, and nowhere near the way out
        /// (100) before anybody is in trouble.
        /// </summary>
        public int Starting = 30;

        /// <summary>Earned for each person who gets out alive, rescued or under their own steam.</summary>
        public int PerPersonSaved = 15;

        /// <summary>
        /// The most the purse can hold: a hundred (the owner's
        /// call, 2026-09-25), which is exactly what the way out costs to
        /// unlock, so a full purse is the one thing that opens it.
        /// </summary>
        public int Maximum = 100;

        /// <summary>
        /// Cards the player is holding before anybody has died. Empty in the
        /// office, where the whole point is that the round opens with nothing.
        /// A later level that wants to hand the player something to start with
        /// -- or a test that needs a particular card in hand -- sets it here.
        /// </summary>
        public PlayerCommandType[] StartingHand = new PlayerCommandType[0];

        /// <summary>
        /// How many cards are drawn from the deck at the start, on top of
        /// <see cref="StartingHand"/>: one, from the deck's own random stream,
        /// so the same seed opens with the same card. The owner's call
        /// (2026-09-24). A test that counts cards in hand sets it to nought.
        /// </summary>
        public int OpeningDrawCount = 1;

        /// <summary>
        /// What the player pays to pull a fire alarm: the price of a card, and
        /// exactly the opening purse, so raising the building is the one move
        /// always on offer from the first tick. Free (and pointless) once the
        /// bells are ringing.
        /// </summary>
        public int PullAlarmCost = 30;

        /// <summary>
        /// How wide a patch a card thrown at the floor catches. About a
        /// doorway and a half across: wide enough that a scrum wedged in a door
        /// is one throw, narrow enough that a calm room is not.
        /// <para>
        /// Cards are aimed at a place rather than at a chosen person, so this
        /// is the whole of the player's accuracy. A throw that catches nobody
        /// is a miss and costs nothing; a throw that catches the wrong person
        /// is spent.
        /// </para>
        /// </summary>
        public int CardPatchRadiusMillimetres = 1500;

        /// <summary>
        /// Every card costs the same. Which card you get is not something you
        /// choose -- the dead deal them -- so pricing them against each other
        /// would be pricing a choice nobody makes. What the player chooses is
        /// whether this moment is worth thirty.
        /// </summary>
        public int CardCost = 30;

        /// <summary>
        /// What the uproar pays. Every notable thing that happens in the
        /// building feeds the meter, sorted into three sizes: somebody
        /// shouting or tripping is small, somebody going down or a door coming
        /// off its hinges is middling, and somebody catching fire or an
        /// appliance going off is big.
        /// <para>
        /// Deaths are deliberately not in here. A death deals a card instead,
        /// so it pays once rather than twice and the two currencies keep one
        /// source each.
        /// </para>
        /// </summary>
        public int UproarSmall = 1;
        public int UproarMiddling = 3;
        public int UproarBig = 6;

        /// <summary>
        /// What working a door costs. Reaching into the building and working
        /// a door is the player's commonest move, and it used to be free, so
        /// there was never a reason not to fling every door in the place open.
        /// Each click pays for what that click does. Ten for anything done to
        /// an inside door -- opening, shutting, locking, unlocking (the owner's
        /// call, 2026-09-25) -- and the whole purse to unlock the building's
        /// way out, which is the round's one big decision.
        /// </summary>
        public int UnlockDoorCost = 10;
        public int OpenDoorCost = 10;
        public int CloseDoorCost = 10;
        public int LockDoorCost = 10;
        public int UnlockExitCost = 100;

        public PurseSettings Clone()
        {
            var copy = (PurseSettings)MemberwiseClone();

            // The shallow copy would hand both scenarios the same array, so a
            // level that dealt itself an opening card would deal it to every
            // other copy too.
            copy.StartingHand = StartingHand == null
                ? new PlayerCommandType[0]
                : (PlayerCommandType[])StartingHand.Clone();
            return copy;
        }

        internal void Validate()
        {
            Settings.Require(Starting >= 0 && PerPersonSaved >= 0 && Maximum >= Starting, "purse");
            Settings.Require(LockDoorCost >= 0 && UnlockExitCost >= 0, "the key");
            Settings.Require(CardCost >= 0 && PullAlarmCost >= 0 && OpeningDrawCount >= 0, "card costs");
            Settings.Require(CardPatchRadiusMillimetres > 0, "how wide a card's patch is");
            Settings.Require(UproarSmall >= 0 && UproarMiddling >= 0 && UproarBig >= 0, "what the uproar pays");
            Settings.Require(UnlockDoorCost >= 0 && OpenDoorCost >= 0 && CloseDoorCost >= 0, "door costs");
        }
    }

    /// <summary>People helping each other: shaking the frozen awake and dragging the knocked-out to safety.</summary>
    [Serializable]
    public sealed class HelpSettings
    {
        /// <summary>Anyone more evil than this never helps.</summary>
        public int HelpMaximumEvil = 4;

        /// <summary>Who shakes a frozen person awake, and from how far they notice them.</summary>
        public int ShakeMinimumCompassion = 6;
        public int ShakeMinimumBravery = 4;
        public int ShakeRangeMillimetres = 4000;
        public int ShakeMinimumTicks = 50;
        public int ShakeMaximumTicks = 75;

        /// <summary>Chance that someone frozen for good snaps out of it when shaken (the frozen-for-a-while always do).</summary>
        public int ShakeFreezeForeverSuccessPercent = 50;

        /// <summary>Who drags a knocked-out person, and from how far they notice them.</summary>
        public int DragMinimumStrength = 6;
        public int DragMinimumCompassion = 6;
        public int DragRangeMillimetres = 5000;
        public int GrabTicks = 50;

        /// <summary>Dragging pace (mm per tick): base plus this much per strength point.</summary>
        public int DragSpeedBase = 10;
        public int DragSpeedPerStrength = 2;

        /// <summary>Gap between a dragger and the person lying behind them.</summary>
        public int DragGapMillimetres = 50;

        /// <summary>With no open door to make for, how far away from the fire they drag someone.</summary>
        public int DragAwayDistanceMillimetres = 3000;

        /// <summary>Stuck this long while dragging, they let go.</summary>
        /// <summary>
        /// How long a dragger strains against a blockage before letting go. Kept
        /// well under the second and a half that nothing in the run is allowed
        /// to stand still for, so somebody hauling a body out never becomes a
        /// statue. The margin has to cover letting go as well as holding on:
        /// somebody wedged in a corner takes a moment more to steer out of it
        /// once their hands are free.
        /// </summary>
        public int DragGiveUpBlockedTicks = 40;

        /// <summary>How far past touching they can reach someone, and how long they try to get there.</summary>
        public int ReachMillimetres = 150;
        public int ReachTimeoutTicks = 250;

        public HelpSettings Clone() => (HelpSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(HelpMaximumEvil >= 0 && ShakeMinimumCompassion >= 0 && ShakeMinimumBravery >= 0 &&
                             ShakeRangeMillimetres >= 0 && Settings.Range(ShakeMinimumTicks, ShakeMaximumTicks, 1) &&
                             Settings.Percent(ShakeFreezeForeverSuccessPercent), "shaking awake");
            Settings.Require(DragMinimumStrength >= 0 && DragMinimumCompassion >= 0 && DragRangeMillimetres >= 0 &&
                             GrabTicks >= 1 && DragSpeedBase >= 0 && DragSpeedPerStrength >= 0 && DragGapMillimetres >= 0 &&
                             DragAwayDistanceMillimetres >= 0 && DragGiveUpBlockedTicks >= 1, "dragging");
            Settings.Require(ReachMillimetres >= 0 && ReachTimeoutTicks >= 1, "reaching someone");
        }
    }

    /// <summary>
    /// The building's day: the small things calm people do because a cue
    /// told them to or because they thought of it themselves (see
    /// <see cref="CueSystem"/> and <see cref="ErrandBehaviour"/>). What the
    /// day actually holds -- when the meeting ends, whether there is a home
    /// time -- is the level's timetable, not a setting.
    /// </summary>
    [Serializable]
    public sealed class DaySettings
    {
        /// <summary>
        /// How often one person needs the toilet: about this many ticks
        /// between trips, each person's next drawn from the seed, and their
        /// first anywhere inside the first stretch so the whole office does
        /// not go at once. Six minutes: in a twenty-person office that is a
        /// trip every twenty seconds or so somewhere on the floor, one or two
        /// people in the bathroom at a time. A chance per decision was tried
        /// first and sent people every few seconds, because a calm person
        /// decides something every few seconds. Nought means nobody ever goes,
        /// which a test about two people in one room wants.
        /// </summary>
        public int ToiletEveryTicks = 18000;

        /// <summary>
        /// How often a calm person who has a desk and is not at it decides
        /// to go back to it. This is what keeps an office reading as an
        /// office: people drift back to their own chairs between strolls
        /// and chats rather than wandering the corridor all day.
        /// </summary>
        public int GoHomeChancePercent = 10;

        /// <summary>Within this distance of their spot, or on their chair, somebody counts as at home.</summary>
        public int AtHomeMillimetres = 1500;

        /// <summary>How often somebody talking says something, drawn from this range.</summary>
        public int RemarkEveryMinimumTicks = 150;
        public int RemarkEveryMaximumTicks = 400;

        /// <summary>
        /// How far the first thing each of them says carries. Quiet: within a
        /// couple of metres people glance over as the talking starts, and
        /// nobody further off hears a thing. What follows is only written
        /// down, so a chat beside somebody's desk does not hold their head
        /// turned all afternoon. A remark alarms nobody, whatever it says.
        /// </summary>
        public int RemarkHearingRadiusMillimetres = 2500;

        /// <summary>
        /// A walk that has taken this long is given up on: a person who
        /// cannot get where they were going goes back to loitering rather
        /// than pressing at a wall all day.
        /// </summary>
        public int ErrandTimeoutTicks = 3000;

        /// <summary>
        /// Somebody on an errand who has been stuck this long gives it up.
        /// Three seconds: a stroll gives up after less than half a second,
        /// because a stroll has no purpose, and an errand walker who borrowed
        /// that patience dropped a toilet trip the first time they had to
        /// wait behind somebody in the office. Somebody leaving the building
        /// never gives up for being stuck: a queue at the front door is the
        /// point of them.
        /// </summary>
        public int BlockedGiveUpTicks = 150;

        /// <summary>
        /// How long somebody leaving stands at a locked way out before they
        /// give up and go back to their day. Long: a queue at the front door
        /// at home time is exactly the sort of thing worth watching.
        /// </summary>
        public int WaitAtLockedDoorTicks = 1500;

        /// <summary>
        /// When the player calls it a day, how far apart people take it up:
        /// the building empties over about half a minute, never all at once.
        /// A timetable's home time carries its own spread.
        /// </summary>
        public int PlayerHomeTimeSpreadTicks = 1500;

        /// <summary>
        /// How long somebody sits at their own desk before getting up for a
        /// stroll, a chat or the toilet: half a minute to a minute and a
        /// half. The ordinary sit (five to twenty seconds) is for a chair
        /// that is not theirs; at their own desk it had people popping up and
        /// down like a fairground game.
        /// </summary>
        public int DeskSitMinimumTicks = 1500;
        public int DeskSitMaximumTicks = 4500;

        /// <summary>
        /// Home time stands until everybody is out. Somebody who gave up on
        /// it -- stood at a locked way out until they tired of it, or found
        /// no route -- waits about this long, jittered, before taking it up
        /// again, so a way out unlocked a minute later still empties the
        /// building.
        /// </summary>
        public int HomeTimeRetryTicks = 1500;

        /// <summary>
        /// How long somebody remembers a door they stood at that would not
        /// open, and routes round it. A minute: long enough that the story
        /// is not one line of "tried the door" after another from somebody
        /// shut in a stall, short enough that a door unlocked is found again.
        /// </summary>
        public int LockedDoorMemoryTicks = 3000;

        /// <summary>
        /// A door somebody opened themselves is shut behind them once they
        /// are through, unless somebody else is within this distance of it
        /// and may be on their way through too.
        /// </summary>
        public int DoorHoldMillimetres = 2000;

        /// <summary>
        /// How often somebody cruel enough to defy a leader
        /// (<see cref="LeadershipSettings.DefiantMinimumEvil"/>) defies a cue
        /// that has a person behind it: sits on when the host ends the
        /// meeting, will not talk to whoever came over. Half the time: the
        /// cruel are contrary, not deaf, and a refusal is a line in the
        /// story. A cue with nobody behind it (home time, by the clock) is
        /// nobody's to defy.
        /// </summary>
        public int CruelIgnoreCuePercent = 50;

        /// <summary>
        /// How long somebody who sat on when the meeting ended sits on for,
        /// drawn from this range: ten to thirty seconds, after which they
        /// get up like everybody else. Contrary, not glued to the chair.
        /// </summary>
        public int CruelSitOnMinimumTicks = 500;
        public int CruelSitOnMaximumTicks = 1500;

        public DaySettings Clone() => (DaySettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(ToiletEveryTicks >= 0 && Settings.Percent(GoHomeChancePercent), "day chances");
            Settings.Require(Settings.Range(RemarkEveryMinimumTicks, RemarkEveryMaximumTicks, 1), "remarks");
            Settings.Require(RemarkHearingRadiusMillimetres >= 0 && AtHomeMillimetres >= 0, "remark reach and home");
            Settings.Require(ErrandTimeoutTicks >= 1 && BlockedGiveUpTicks >= 1 && WaitAtLockedDoorTicks >= 0 &&
                             PlayerHomeTimeSpreadTicks >= 0, "errand timing");
            Settings.Require(Settings.Range(DeskSitMinimumTicks, DeskSitMaximumTicks, 1) && HomeTimeRetryTicks >= 1 &&
                             LockedDoorMemoryTicks >= 0 && DoorHoldMillimetres >= 0, "desk sits, home time and doors");
            Settings.Require(Settings.Percent(CruelIgnoreCuePercent) && Settings.Range(CruelSitOnMinimumTicks, CruelSitOnMaximumTicks, 0), "ignoring cues");
        }
    }

    /// <summary>
    /// The Director's traps (prototype 3, 2026-09-25): a tower of boxes that
    /// comes down across a doorway once the fire is lit and somebody comes
    /// near it (<see cref="TrapDefinition"/>, <see cref="TrapSystem"/>).
    /// </summary>
    [Serializable]
    public sealed class TrapSettings
    {
        /// <summary>
        /// How near somebody has to come to the tower to bring it down, for
        /// a trap that names no radius of its own. Two metres: a runner
        /// passing the corner, not somebody on the far side of the junction.
        /// </summary>
        public int TriggerRadiusMillimetres = 2000;

        /// <summary>
        /// How many of the fallen boxes have to be lying unburnt in the
        /// doorway for it to stay shut. Fewer than this and the way is open
        /// again: carried off, thrown clear or burnt, one at a time.
        /// </summary>
        public int PileHoldsAtBoxes = 3;

        /// <summary>
        /// How far the crash of the tower is heard. Twelve metres, a whole
        /// corridor: calm people look, and anybody running for that doorway
        /// thinks again.
        /// </summary>
        public int CrashSoundRadiusMillimetres = 12000;

        /// <summary>How far somebody standing in the doorway is knocked clear as the boxes come down.</summary>
        public int KnockClearMillimetres = 800;

        /// <summary>
        /// How far past the wall line, into the far room, the fallen boxes
        /// are laid: clear of the doorway's own plug, and still inside the
        /// strip that counts as the doorway.
        /// </summary>
        public int PileBeyondMillimetres = 350;

        public TrapSettings Clone() => (TrapSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(TriggerRadiusMillimetres > 0 && PileHoldsAtBoxes >= 1 && CrashSoundRadiusMillimetres >= 0 &&
                             KnockClearMillimetres >= 0 && PileBeyondMillimetres >= 0, "traps");
        }
    }

    /// <summary>
    /// The Director's ladder of small incidents (prototype 3, 2026-09-26,
    /// <see cref="DirectorSystem"/>): the round starts with a waste bin
    /// catching fire; put it out and, a while later, a socket crackles and
    /// pops in the busiest room; put that out and the fuse box goes, and every
    /// socket with it. A fire that gets out of the room it started in is the
    /// real fire, and the Director stops adding to it.
    /// </summary>
    [Serializable]
    public sealed class DirectorSettings
    {
        /// <summary>
        /// Whether the Director climbs the ladder at all. Off in the code
        /// defaults, so a test building starts its fire the way it always
        /// did; the office level switches it on (<c>LevelDefinition</c>).
        /// </summary>
        public bool ClimbsTheLadder;

        /// <summary>
        /// The things the first incident may start in: one is drawn per run.
        /// The office's three waste bins in the meeting room, so the fire
        /// still starts somewhere different there each round -- by the door
        /// one seed, by the far wall the next (the owner's rule from the first
        /// batch).
        /// </summary>
        public SimulationId[] FirstIncidentThings = PrototypeBuilding.MeetingRoomBins();

        /// <summary>
        /// How long the working day runs before the bin catches, drawn per
        /// run: thirty to ninety seconds. Time for the player to read the
        /// office and set up their influence; the red button skips it.
        /// </summary>
        public int FirstIncidentMinimumTicks = 1500;
        public int FirstIncidentMaximumTicks = 4500;

        /// <summary>
        /// How long after a fire is put out the next rung comes, drawn each
        /// time: twenty to forty seconds. Long enough for the office to settle
        /// back to work, short enough that the round does not go slack.
        /// </summary>
        public int NextRungMinimumTicks = 1000;
        public int NextRungMaximumTicks = 2000;

        /// <summary>
        /// How long a socket or the fuse box crackles and smokes before it
        /// goes: five seconds of warning, the owner's choice ("crackle first"),
        /// so a player who is watching can pull people away.
        /// </summary>
        public int CrackleTicks = 250;

        /// <summary>How far the crackle is heard: the room it is in, more or less. It frightens nobody; the curious go and look.</summary>
        public int CrackleHearingMillimetres = 5000;

        /// <summary>
        /// How long after a fire is put out any bell that was pulled falls
        /// silent: the all-clear, about ten seconds later. Without it a pulled
        /// alarm kept everybody in earshot frightened for the rest of the round.
        /// </summary>
        public int AllClearAfterTicks = 500;

        /// <summary>
        /// For this long after a socket or the fuse box goes, any room it sets
        /// alight belongs to the incident: five seconds, long enough for the
        /// fuse box's spark to reach the last socket (about two and a half)
        /// with room to spare. Fire reaching a new room after that has got
        /// loose. A bin has no bang, so its incident is its own room.
        /// </summary>
        public int BangSettlesTicks = 250;

        public DirectorSettings Clone()
        {
            var copy = (DirectorSettings)MemberwiseClone();
            copy.FirstIncidentThings = (SimulationId[])FirstIncidentThings?.Clone();
            return copy;
        }

        internal void Validate()
        {
            Settings.Require(FirstIncidentThings != null, "the first incident's things");
            Settings.Require(Settings.Range(FirstIncidentMinimumTicks, FirstIncidentMaximumTicks, 1) &&
                             Settings.Range(NextRungMinimumTicks, NextRungMaximumTicks, 1) &&
                             CrackleTicks >= 1 && CrackleHearingMillimetres >= 0 && AllClearAfterTicks >= 1 &&
                             BangSettlesTicks >= 0,
                "the Director's ladder");
        }
    }

    /// <summary>
    /// Calming down (prototype 3, 2026-09-26): a frightened person who sees
    /// and hears nothing frightening for a while settles, at a pace their own
    /// personality sets -- the owner's "the ones that saw the fire stay
    /// rattled for a while, the nervous might freak out more, the calm and
    /// brave don't really care; this should be the personality system in
    /// play, not scripted". See <see cref="FearSystem.Settle"/>.
    /// <para>
    /// Fear is a level from 1000 (just frightened) down. After a quiet spell
    /// it drains by
    /// <c>DrainBase + DrainPerBravery x bravery - DrainPerNervousness x nervousness</c>
    /// a second (never less than <see cref="DrainMinimumPerMillePerSecond"/>),
    /// but never below <c>FloorPerNervousness x (nervousness - 5)</c>; below
    /// <see cref="CalmBelowPerMille"/> they calm down. So a hero (bravery 10,
    /// nervousness 1) settles about seven seconds after the quiet begins, an
    /// ordinary person (5 and 5) in about twelve, a worrier in half a minute,
    /// and anybody with nervousness 9 or more never does: their floor is above
    /// the line, and they keep heading out.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class CalmingSettings
    {
        /// <summary>Off, and frightened people stay frightened for the rest of the round, as they did before.</summary>
        public bool Enabled = true;

        /// <summary>How long nothing frightening has to go on before fear starts to drain at all: five seconds, a little different per person.</summary>
        public int QuietTicks = 250;

        public int CalmBelowPerMille = 400;
        public int DrainBasePerMillePerSecond = 40;
        public int DrainPerBravery = 8;
        public int DrainPerNervousness = 6;
        public int DrainMinimumPerMillePerSecond = 10;
        public int FloorPerNervousness = 120;

        /// <summary>How long somebody who saw the danger stays rattled once they have calmed down: a minute.</summary>
        public int RattledAfterSeeingTicks = 3000;

        /// <summary>How long somebody who only heard about it stays rattled: twenty seconds.</summary>
        public int RattledAfterHearingTicks = 1000;

        /// <summary>
        /// While rattled, a noise -- a thud, a crash, a bang -- frightens them
        /// outright within this share of the distance it is heard at, where
        /// before it only turned their head.
        /// </summary>
        public int RattledStartlePercent = 50;

        /// <summary>
        /// While rattled, the brave need this much more bravery before they
        /// look up at somebody bolting rather than bolt with them.
        /// </summary>
        public int RattledBraveryPenalty = 3;

        /// <summary>
        /// How often each frightened person checks whether anything frightening
        /// is still going on: every fifth of a second, each on their own beat,
        /// so a crowd of five hundred costs a hundred looks a tick, not five
        /// hundred.
        /// </summary>
        public int CheckEveryTicks = 10;

        public CalmingSettings Clone() => (CalmingSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(QuietTicks >= 1 && CalmBelowPerMille >= 0 && CalmBelowPerMille <= 1000 &&
                             DrainMinimumPerMillePerSecond >= 1 && FloorPerNervousness >= 0 &&
                             RattledAfterSeeingTicks >= 0 && RattledAfterHearingTicks >= 0 &&
                             Settings.Percent(RattledStartlePercent) && CheckEveryTicks >= 1, "calming down");
        }
    }

    /// <summary>
    /// Nudging people (prototype 3, 2026-09-25): a click on somebody makes
    /// them lurch, look round a beat later, and after a few nudges in a row
    /// get annoyed (<see cref="NudgeSystem"/>).
    /// </summary>
    [Serializable]
    public sealed class NudgeSettings
    {
        /// <summary>How far the nudge shoves them: a step, away from where the click landed (300 since 2026-09-26; it was 200).</summary>
        public int LurchMillimetres = 300;

        /// <summary>Nudges this close together count as "in a row".</summary>
        public int AnnoyedWindowTicks = 500;

        /// <summary>The nudge that makes them annoyed: the third in a row.</summary>
        public int AnnoyedAfterNudges = 3;

        /// <summary>
        /// How long they stay annoyed: twenty seconds, a little different each
        /// time, shaking with it, and while it lasts a nudge does nothing to
        /// them (the owner's rule, 2026-09-26).
        /// </summary>
        public int AnnoyedForTicks = 1000;


        public NudgeSettings Clone() => (NudgeSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(LurchMillimetres >= 0 && AnnoyedWindowTicks >= 0 && AnnoyedAfterNudges >= 1 && AnnoyedForTicks >= 1,
                "nudging");
        }
    }

    /// <summary>
    /// Influence (prototype 3, second batch, 2026-09-26; <see cref="InfluenceSystem"/>):
    /// the player clicks a door, a thing or a patch of floor and people are
    /// drawn toward it, each by as much as their character lets them.
    /// </summary>
    [Serializable]
    public sealed class InfluenceSettings
    {
        /// <summary>The most steps a place can have: twenty clicks' worth (the owner's "maybe steps 0-20").</summary>
        public int MaximumLevel = 20;

        /// <summary>How long a place keeps each step: two seconds, so a full twenty fades over forty.</summary>
        public int TicksPerStepLost = 100;

        /// <summary>
        /// How far a place's pull reaches, fading to nothing: twelve metres, a
        /// big room across. The owner: further than four metres, weaker the
        /// further off, and never into another room for now.
        /// </summary>
        public int ReachMillimetres = 12000;

        /// <summary>A click this near a place on the floor adds to it rather than starting another.</summary>
        public int StackRadiusMillimetres = 1000;

        /// <summary>
        /// What a full pull, felt by an ordinary person standing on it, is worth
        /// to their choices, in millimetres of walk: six metres, the same as
        /// reading an exit sign pointing that way, and more than a door
        /// standing open (four) or the rest of a group going that way (three).
        /// One click at the edge of its reach is worth next to nothing.
        /// </summary>
        public int FullPullBonusMillimetres = 6000;

        /// <summary>How easily led somebody is: percent more for each point of nervousness above five (less below).</summary>
        public int PercentPerNervousness = 12;

        /// <summary>Percent less for each point of leadership above five: leaders go their own way.</summary>
        public int PercentPerLeadership = 12;

        /// <summary>Percent less for each point of evil above five: the cruel ignore it.</summary>
        public int PercentPerEvil = 12;

        /// <summary>Percent more for a visitor, who does not know the building and takes any hint going.</summary>
        public int VisitorPercent = 50;

        public int MinimumPercent = 10;
        public int MaximumPercent = 200;

        /// <summary>A safety net against a stuck mouse button, never the player's limit: past this many places, the faintest goes.</summary>
        public int MaximumPlaces = 64;

        /// <summary>
        /// Calm people who are easily led -- this nervous, or a visitor -- may
        /// get up from a chair or leave an errand for a strong enough pull.
        /// </summary>
        public int EasilyLedNervousness = 7;

        /// <summary>How often an easily led person, sitting or busy, weighs up a pull: every second, on their own beat.</summary>
        public int LeaveTaskCheckTicks = 50;

        /// <summary>
        /// The chance, per mille, that they get up at a check, for a full pull
        /// felt: one in ten. A faint pull, proportionally less. So within ten
        /// seconds or so of frantic clicking the nervous start drifting out of
        /// a meeting, while the steady sit on.
        /// </summary>
        public int LeaveTaskChancePerMille = 100;

        /// <summary>
        /// The chance, per mille of a full pull felt, that a calm person with
        /// nothing in particular to do picks the pull as where to wander next.
        /// </summary>
        public int WanderToItPerMille = 1000;

        public InfluenceSettings Clone() => (InfluenceSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(MaximumLevel >= 1 && TicksPerStepLost >= 1 && ReachMillimetres >= 1 &&
                             StackRadiusMillimetres >= 0 && FullPullBonusMillimetres >= 0 &&
                             MinimumPercent >= 0 && MaximumPercent >= MinimumPercent && MaximumPlaces >= 1 &&
                             LeaveTaskCheckTicks >= 1 && LeaveTaskChancePerMille >= 0 && WanderToItPerMille >= 0,
                "influence");
        }
    }

    internal static class Settings
    {
        public static bool Range(int minimum, int maximum, int floor) => minimum >= floor && maximum >= minimum;

        public static bool Percent(int value) => value >= 0 && value <= 100;

        public static void Require(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Fire-reaction scenario values are invalid: {what}.");
            }
        }
    }
}
