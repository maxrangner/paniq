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

        public WorldSettings Clone() => (WorldSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(OccupancyRadiusMillimetres > 0 && OccupancyRadiusMillimetres <= 2000, "occupancy radius");
            Settings.Require(MaximumStepDistanceMillimetres >= 0 && MaximumStepDistanceMillimetres <= 1000, "maximum step");
        }
    }

    /// <summary>Seeing the fire and how long it takes to react.</summary>
    [Serializable]
    public sealed class PerceptionSettings
    {
        public int VisionRangeMillimetres = 3000;
        public int MaximumReactionDelayTicks = 20;

        public PerceptionSettings Clone() => (PerceptionSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(VisionRangeMillimetres > 0 && MaximumReactionDelayTicks >= 0, "perception");
        }
    }

    /// <summary>Where and when the fire starts, and how fast it spreads square by square.</summary>
    [Serializable]
    public sealed class FireSettings
    {
        public LogicalBounds SpawnBounds = new LogicalBounds(-2000, 2000, -2000, 2000);
        public int ActivationTick = 250;
        public int CellSizeMillimetres = 500;
        public int SpreadMinimumTicks = 40;
        public int SpreadMaximumTicks = 120;

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

        /// <summary>
        /// The chance, each time somebody alight would lurch off in a new
        /// direction, that they throw themselves down and roll instead. About
        /// one lurch in twenty-five. Over the ten or so lurches somebody has
        /// before they collapse that is about two people in five who try it, so
        /// most of them still just run, and going down stays a thing you notice.
        /// </summary>
        public int DropAndRollChancePercent = 4;

        /// <summary>How long they roll before getting up (or being put out).</summary>
        public int RollMinimumTicks = 60;
        public int RollMaximumTicks = 120;

        /// <summary>
        /// The chance that a roll, once it has run its course, smothers the
        /// flames. One draw at the end rather than one a tick, so this number is
        /// the odds as written: a third of rolls save the person, which makes
        /// going down plainly worth doing and plainly not a rescue.
        /// </summary>
        public int RollPutsOutChancePercent = 35;

        /// <summary>How long a square that has been put out stays too wet to catch again.</summary>
        public int DousedWetTicks = 1000;

        /// <summary>How many ticks of spray one burning square takes to put out.</summary>
        public int DouseTicksPerCell = 30;

        /// <summary>A gap between two bodies at most this wide lets the flames jump across.</summary>
        public int BurningSpreadGapMillimetres = 100;

        /// <summary>Chance per tick that the flames jump to someone that close. Running into someone always does it.</summary>
        public int BurningSpreadChancePercent = 20;

        public FireSettings Clone() => (FireSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(SpawnBounds.MinX <= SpawnBounds.MaxX && SpawnBounds.MinZ <= SpawnBounds.MaxZ, "fire spawn bounds");
            Settings.Require(ActivationTick >= 0 && CellSizeMillimetres >= 100, "fire timing and cell size");
            Settings.Require(SpreadMinimumTicks > 0 && SpreadMaximumTicks >= SpreadMinimumTicks, "fire spread interval");
            Settings.Require(Settings.Range(BurnMinimumTicks, BurnMaximumTicks, 1) &&
                             Settings.Range(BurningTurnMinimumTicks, BurningTurnMaximumTicks, 1) &&
                             BurningBlockedTurnTicks >= 1 &&
                             Settings.Range(BurningScreamMinimumTicks, BurningScreamMaximumTicks, 1) &&
                             BurningSpreadGapMillimetres >= 0 && Settings.Percent(BurningSpreadChancePercent) &&
                             DousedWetTicks >= 0 && DouseTicksPerCell >= 1, "burning people");
            Settings.Require(Settings.Percent(DropAndRollChancePercent) &&
                             Settings.Percent(RollPutsOutChancePercent) &&
                             Settings.Range(RollMinimumTicks, RollMaximumTicks, 1), "stop, drop and roll");
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

        public CalmSettings Clone() => (CalmSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(Settings.Range(SpeedMinimum, SpeedMaximum, 0), "calm speed");
            Settings.Require(Settings.Range(TurnRateMinimum, TurnRateMaximum, 1), "calm turn rates");
            Settings.Require(Acceleration > 0, "calm acceleration");
            Settings.Require(Settings.Range(DecisionMinimumTicks, DecisionMaximumTicks, 1), "calm decision interval");
            Settings.Require(BlockedGiveUpTicks >= 1 && StrollTimeoutTicks >= 1 && SocialTimeoutTicks >= 1, "calm timeouts");
            Settings.Require(StrollWallMarginMillimetres >= 0 && StrollMinimumDistanceMillimetres >= 0 &&
                             StrollArrivalDistanceMillimetres >= 0 && StrollSlowdownDistanceMillimetres > 0 &&
                             WanderMaximumDegrees >= 0 && WanderMaximumDegrees <= 180, "strolling");
            Settings.Require(SocialStopDistanceMillimetres >= 0 &&
                             Settings.Range(SocialMinimumDistanceMillimetres, SocialMaximumDistanceMillimetres, 0), "socialising");
            Settings.Require(PeopleAvoidPercent >= 0 && WallAvoidPercent >= 0 && ObjectAvoidPercent >= 0, "calm steering weights");
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

        /// <summary>Steering weights, as percentages of the pull toward the goal.</summary>
        public int PeopleAvoidPercent = 50;
        public int WallAvoidPercent = 200;
        public int ObjectAvoidPercent = 20;

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
            Settings.Require(PeopleAvoidPercent >= 0 && WallAvoidPercent >= 0 && ObjectAvoidPercent >= 0, "panic steering weights");
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
        /// <summary>Calm people this close to a yell understand it and are alarmed.</summary>
        public int YellAlarmRadiusMillimetres = 2500;

        /// <summary>Calm people this close to a yell only hear it and turn to look.</summary>
        public int YellHearingRadiusMillimetres = 6000;

        public int FireHearingRadiusMillimetres = 3500;
        public int BumpSoundRadiusMillimetres = 3000;
        public int InvestigateMinimumTicks = 50;
        public int InvestigateMaximumTicks = 125;

        /// <summary>After this long looking toward a noise, a person may edge toward it.</summary>
        public int InvestigateCreepDelayTicks = 25;

        /// <summary>They only edge closer while the noise is further away than this.</summary>
        public int InvestigateCreepDistanceMillimetres = 2000;

        /// <summary>They only edge closer while facing the noise within this many degrees.</summary>
        public int InvestigateCreepMaximumTurn = 30;

        public HearingSettings Clone() => (HearingSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(YellAlarmRadiusMillimetres > 0 && YellHearingRadiusMillimetres >= YellAlarmRadiusMillimetres &&
                             FireHearingRadiusMillimetres >= 0 && BumpSoundRadiusMillimetres >= 0 &&
                             Settings.Range(InvestigateMinimumTicks, InvestigateMaximumTicks, 1), "hearing");
            Settings.Require(InvestigateCreepDelayTicks >= 0 && InvestigateCreepDistanceMillimetres >= 0 &&
                             InvestigateCreepMaximumTurn >= 0 && InvestigateCreepMaximumTurn <= 180, "investigating");
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

        /// <summary>
        /// How long somebody eases out of the crush when the door they are
        /// queueing at is the building's only way out. A second: long enough to
        /// let the press shuffle forward without them, short enough that they
        /// are plainly still trying to get out rather than wandering off.
        /// </summary>
        public int OnlyWayOutCrowdedAvoidTicks = 50;
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

        /// <summary>This evil: shut the door behind them, even in the face of someone coming.</summary>
        public int EvilCloseMinimum = 7;

        /// <summary>This evil: turn the key as well, so nobody can follow.</summary>
        public int EvilLockMinimum = 9;

        /// <summary>This compassionate: hold a door open for someone coming, even with fire in the room beyond.</summary>
        public int CompassionHoldMinimum = 7;

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
            Settings.Require(ApproachInsetMillimetres >= 0 && OutsideTargetMillimetres >= 0 && ArrivalDistanceMillimetres > 0 &&
                             CommitDistanceMillimetres >= 0 && NoSwerveDistanceMillimetres >= 0, "door approach");
            Settings.Require(OpenBonusMillimetres >= 0 && CurrentChoiceBonusMillimetres >= 0 &&
                             ChoiceNoiseMillimetres >= 0 && InFirePenaltyMillimetres >= 0 &&
                             CurrentRoomBonusMillimetres >= 0 && RefugeNoFireMillimetres >= 0 &&
                             RefugeClearRoomMillimetres >= 0 && RefugeSpacePerPersonMillimetres > 0, "door scoring");
            Settings.Require(DoorStrength >= 1, "door strength");
            Settings.Require(CloseReachMillimetres >= 0 && CloseApproachRadiusMillimetres >= 0 &&
                             FireAtDoorRadiusMillimetres >= 0 && EvilCloseMinimum >= 0 &&
                             CompassionHoldMinimum >= 0 && EvilLockMinimum >= EvilCloseMinimum,
                "closing doors");
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

        public ObjectPhysicsSettings Clone() => (ObjectPhysicsSettings)MemberwiseClone();

        internal void Validate()
        {
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
        /// <summary>
        /// How far either side of what they are aiming at somebody with no
        /// strength at all waves the jet, and how much each point of strength
        /// steadies it. At 24 less 3 a point, an ordinary person (5) swings 9
        /// degrees either side and anybody 8 or stronger holds it dead straight.
        /// </summary>
        public int SweepDegrees = 24;
        public int SweepDegreesPerStrengthPoint = 3;

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

        /// <summary>Give up fetching after this long, and stop fighting after this long.</summary>
        public int FetchTimeoutTicks = 500;
        public int FightTimeoutTicks = 1500;

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
            Settings.Require(FetchTimeoutTicks > 0 && FightTimeoutTicks > 0, "extinguisher timeouts");
            Settings.Require(SprayRangeMillimetres > 0 && SprayConeDegrees > 0 && SprayConeDegrees <= 180 && CellsPerTick > 0 &&
                StandOffMillimetres > 0 && StandOffMillimetres <= SprayRangeMillimetres, "the spray");
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
        public const int KindCount = 12;

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

        public ObjectKindSettings Clone() => (ObjectKindSettings)MemberwiseClone();

        /// <summary>The office's things, in enum order.</summary>
        public static ObjectKindSettings[] Defaults()
        {
            return new[]
            {
                Entry(PhysicsObjectKind.Box, 100, 75, 400, 750),

                // Wooden: a hard enough knock breaks it up.
                Breakable(Entry(PhysicsObjectKind.Chair, 120, 150, 600, 900), 450),

                // Castors: it rolls away across the floor, and its frame bends.
                Breakable(Entry(PhysicsObjectKind.OfficeChair, 35, 175, 600, 900), 400),
                Entry(PhysicsObjectKind.WasteBin, 80, 50, 250, 450),

                // Earth and green leaves: it never catches.
                Entry(PhysicsObjectKind.PottedPlant, 200, 0, 0, 0),
                Entry(PhysicsObjectKind.Bag, 90, 100, 300, 500),

                // Hard plastic on hard floor: it skitters. Its battery goes
                // off when the flames reach it: a sharp crack rather than a
                // proper bang, enough to make everybody nearby jump and to
                // throw burning plastic onto the desk it was sitting on.
                Popping(Entry(PhysicsObjectKind.Laptop, 55, 200, 200, 400), 900, 45, 1),

                // Steel: it never catches.
                Entry(PhysicsObjectKind.Extinguisher, 90, 0, 0, 0),

                // Stiff leather: it slides less than a soft bag and burns slowly.
                Entry(PhysicsObjectKind.Briefcase, 110, 175, 350, 600),

                // Electrical. Neither burns for long: the flames reach them and
                // they go off, which is the point of them. A microwave clears a
                // 2.2 m circle, a socket 1.4 m, a laptop only 0.9 m.
                Popping(Entry(PhysicsObjectKind.Microwave, 150, 120, 60, 90), 2200, 70, 3),

                // Bolted to the wall, so it never slides anywhere.
                Popping(Entry(PhysicsObjectKind.WallSocket, 1000, 90, 40, 60), 1400, 55, 2),

                // What a collapsed table becomes: a heap of boards. It shifts
                // when somebody puts their shoulder into it but slides nowhere
                // on its own, and it never catches by itself — the table it came
                // from keeps its own burning, so a heap that could also catch
                // would burn the same wood twice.
                Entry(PhysicsObjectKind.TableWreck, 250, 0, 0, 0)
            };
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
        }
    }

    [Serializable]
    public sealed class FlammableSettings
    {
        /// <summary>Flames (a burning square or burning thing) this close to a thing's edge heat it.</summary>
        public int HeatDistanceMillimetres = 500;

        /// <summary>How each kind of loose object slides and burns; one entry per kind.</summary>
        public ObjectKindSettings[] Kinds = ObjectKindSettings.Defaults();

        /// <summary>
        /// How hard a blow collapses a table, as momentum in kilograms times
        /// millimetres per tick. Higher than a chair's, because a table is the
        /// sturdiest thing in the room.
        /// </summary>
        public int TableBreakMomentum = 600;

        /// <summary>
        /// What the heap of boards a collapsed table becomes weighs. A desk is
        /// 40 kg, so somebody strong can shoulder it out of a doorway but
        /// nobody kicks it across the room.
        /// </summary>
        public int TableWreckMassGrams = 40000;

        /// <summary>
        /// How wide the heap is: half the table's short side, kept between these.
        /// A desk leaves something the size of a person to walk round; the
        /// meeting table leaves a little more, without walling the room in two.
        /// </summary>
        public int TableWreckMinimumSizeMillimetres = 350;
        public int TableWreckMaximumSizeMillimetres = 500;

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
            Settings.Require(TableBreakMomentum >= 0 && TableWreckMassGrams > 0 &&
                             TableWreckMinimumSizeMillimetres > 0 &&
                             TableWreckMaximumSizeMillimetres >= TableWreckMinimumSizeMillimetres, "table strength");
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

        /// <summary>
        /// How far somebody settling into a chair scoots it in under the table.
        /// Matched to the shove back they give it getting out, so the chair ends
        /// a sit roughly where it started one.
        /// </summary>
        public int SitScootMillimetres = 120;

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

        /// <summary>
        /// How long somebody who starts the run already seated stays put before
        /// they would get up of their own accord. A minute of ticks: longer
        /// than any recorded run, so a meeting that is under way when the fire
        /// starts breaks up because of the fire and nothing else.
        /// </summary>
        public int SeatedAtStartTicks = 3000;

        /// <summary>Getting out of a chair: this long, less a little for the nervous.</summary>
        public int StandUpTicks = 40;
        public int StandUpTicksPerNervousness = 2;

        /// <summary>How hard the chair is shoved back as they stand, in millimetres per tick.</summary>
        public int StandUpShoveSpeed = 8;
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
            Settings.Require(SeatedAtStartTicks > 0, "how long people who start seated stay seated");
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
            Settings.Require(ShoveMinimumStrength >= 0 && ShoveTicks >= 1 && ShoveSpeedBase >= 0 &&
                             ShoveSpeedPerStrength >= 0, "heaving an obstruction clear");
            Settings.Require(BarricadeNervousMinimum >= 0 && BarricadeEvilMinimum >= 0 &&
                             BarricadeFetchRangeMillimetres >= 0 && BarricadeTimeoutTicks >= 1 &&
                             BarricadeSetDownTicks >= 1 && BarricadeBlockedGiveUpTicks >= 1, "barricading");
        }
    }

    /// <summary>
    /// Fire alarms. Somebody who has taken in that there is a fire and who
    /// thinks of other people walks over to the nearest alarm and hits it; every
    /// alarm in the building then rings, and everybody who hears one knows there
    /// is a fire. What they do about it depends who they are: the brave and
    /// level-headed walk briskly out, while the nervous stampede.
    /// </summary>
    [Serializable]
    public sealed class AlarmSettings
    {
        /// <summary>Turn the alarms off altogether, to see the building without them.</summary>
        public bool Enabled = true;

        /// <summary>How far somebody will divert to hit an alarm.</summary>
        public int ReachMillimetres = 4000;

        /// <summary>How close they must get to hit it.</summary>
        public int ArrivalMillimetres = 500;

        /// <summary>How long hitting it takes.</summary>
        public int PressTicks = 20;

        /// <summary>If they cannot get to it in this long, they give up and run.</summary>
        public int FetchTimeoutTicks = 400;

        /// <summary>How far a ringing bell is heard, and how far it is alarming. A bell fills its own room.</summary>
        public int BellHearingRadiusMillimetres = 14000;
        public int BellAlarmRadiusMillimetres = 14000;

        /// <summary>Who thinks to raise the alarm: a leader, or somebody who thinks of others.</summary>
        public int PullMinimumLeadership = 6;
        public int PullMinimumCompassion = 6;

        /// <summary>
        /// Bravery minus nervousness at least this much, and the bell makes them
        /// leave briskly rather than panic. Everyone else stampedes.
        /// </summary>
        public int ComposureGap = 2;

        public AlarmSettings Clone() => (AlarmSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(ReachMillimetres >= 0 && ArrivalMillimetres > 0 && PressTicks >= 1 &&
                             FetchTimeoutTicks >= 1, "fire alarms");
            Settings.Require(BellHearingRadiusMillimetres >= 0 &&
                             BellAlarmRadiusMillimetres <= BellHearingRadiusMillimetres, "alarm bells");
            Settings.Require(PullMinimumLeadership >= 0 && PullMinimumCompassion >= 0, "who raises the alarm");
        }
    }

    /// <summary>
    /// The player's influence, and what each card costs. Influence starts at
    /// <see cref="Starting"/>, every card spends some, and every person who gets
    /// out alive pays some back. Nothing else refills it, so a run where nobody
    /// is saved runs the player dry.
    /// </summary>
    [Serializable]
    public sealed class InfluenceSettings
    {
        /// <summary>What the player starts the run with.</summary>
        public int Starting = 100;

        /// <summary>Earned for each person who gets out alive, rescued or under their own steam.</summary>
        public int PerPersonSaved = 15;

        /// <summary>The most influence the player can bank, so saving everybody does not leave a meaningless pile.</summary>
        public int Maximum = 300;

        public int BeefcakeCost = 20;
        public int SpawnFireCost = 10;
        public int SpawnExtinguisherCost = 25;
        public int BlastWallCost = 40;

        public InfluenceSettings Clone() => (InfluenceSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(Starting >= 0 && PerPersonSaved >= 0 && Maximum >= Starting, "influence");
            Settings.Require(BeefcakeCost >= 0 && SpawnFireCost >= 0 && SpawnExtinguisherCost >= 0 &&
                             BlastWallCost >= 0, "card costs");
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
