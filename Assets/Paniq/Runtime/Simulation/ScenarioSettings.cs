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

    /// <summary>The room, the size of a person, and the longest single step.</summary>
    [Serializable]
    public sealed class WorldSettings
    {
        public LogicalBounds RoomBounds = new LogicalBounds(-6000, 6000, -6000, 6000);
        public int OccupancyRadiusMillimetres = 250;
        public int MaximumStepDistanceMillimetres = 120;

        public WorldSettings Clone() => (WorldSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(RoomBounds.MinX < RoomBounds.MaxX && RoomBounds.MinZ < RoomBounds.MaxZ, "room bounds");
            Settings.Require(RoomBounds.MinX >= -100000 && RoomBounds.MaxX <= 100000 &&
                             RoomBounds.MinZ >= -100000 && RoomBounds.MaxZ <= 100000, "room within the 200 m coordinate span");
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
                             BurningSpreadGapMillimetres >= 0 && Settings.Percent(BurningSpreadChancePercent), "burning people");
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

        /// <summary>Runners aim this far inside a shut door.</summary>
        public int ApproachInsetMillimetres = 600;

        /// <summary>Runners aim this far outside an open door once lined up with it.</summary>
        public int OutsideTargetMillimetres = 1500;

        public int ArrivalDistanceMillimetres = 400;

        /// <summary>Within this distance of an open exit, a runner commits to leaving.</summary>
        public int CommitDistanceMillimetres = 2500;

        /// <summary>Within this distance of their exit, runners stop swerving and following.</summary>
        public int NoSwerveDistanceMillimetres = 2000;

        // Scoring doors: distance, plus these bonuses and penalties.
        public int OpenBonusMillimetres = 4000;
        public int CurrentChoiceBonusMillimetres = 1500;
        public int ChoiceNoiseMillimetres = 1500;
        public int InFirePenaltyMillimetres = 8000;

        /// <summary>How much shoving damage a door takes before it breaks. Damage stays between attempts.</summary>
        public int DoorStrength = 40;

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
                             Settings.Range(GiveUpGlanceMinimumTicks, GiveUpGlanceMaximumTicks, 1), "door timings");
            Settings.Require(ApproachInsetMillimetres >= 0 && OutsideTargetMillimetres >= 0 && ArrivalDistanceMillimetres > 0 &&
                             CommitDistanceMillimetres >= 0 && NoSwerveDistanceMillimetres >= 0, "door approach");
            Settings.Require(OpenBonusMillimetres >= 0 && CurrentChoiceBonusMillimetres >= 0 &&
                             ChoiceNoiseMillimetres >= 0 && InFirePenaltyMillimetres >= 0, "door scoring");
            Settings.Require(DoorStrength >= 1, "door strength");
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

        /// <summary>Fastest a kicked box spins, in degrees per tick.</summary>
        public int SpinMaximum = 20;

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

    /// <summary>
    /// Boxes, chairs and tables catching fire. Things heat up while flames
    /// are close and catch once hot for long enough; cardboard catches
    /// sooner than wood, and wood burns longer.
    /// </summary>
    [Serializable]
    public sealed class FlammableSettings
    {
        /// <summary>Flames (a burning square or burning thing) this close to a thing's edge heat it.</summary>
        public int HeatDistanceMillimetres = 500;

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

        public FlammableSettings Clone() => (FlammableSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(HeatDistanceMillimetres >= 0 && BoxIgniteTicks >= 1 && ChairIgniteTicks >= 1 &&
                             TableIgniteTicks >= 1, "heating up");
            Settings.Require(Settings.Range(BoxBurnMinimumTicks, BoxBurnMaximumTicks, 1) &&
                             Settings.Range(ChairBurnMinimumTicks, ChairBurnMaximumTicks, 1) &&
                             Settings.Range(TableBurnMinimumTicks, TableBurnMaximumTicks, 1), "burn times");
            Settings.Require(FloorIgniteRestTicks >= 1 && TouchGapMillimetres >= 0, "burning things");
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

        public ItemSettings Clone() => (ItemSettings)MemberwiseClone();

        internal void Validate()
        {
            Settings.Require(CarryBaseGrams >= 0 && CarryGramsPerStrength >= 0 && Settings.Percent(CarrySlowdownPercent), "carrying");
            Settings.Require(Settings.Percent(TidyChancePercent) && FetchRangeMillimetres >= 0 &&
                             CarryMinimumDistanceMillimetres >= 0 && ReachMillimetres >= 0 && PickUpTicks >= 1 &&
                             SetDownTicks >= 1 && HoldGapMillimetres >= 0, "tidying up");
            Settings.Require(DropNervousness >= 0 && HurlMinimumStrength >= 0 && EvilAimMinimum >= 0 && AimRangeMillimetres >= 0 &&
                             ThrowImpulse > 0 && ThrowMinimumSpeed >= 1 && ThrowHitMultiplier >= 1, "throwing");
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
