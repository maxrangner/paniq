namespace Paniq.Simulation
{
    /// <summary>
    /// The frightened answer the player's hand (2026-09-30, the owner: "when
    /// panicked, the agents still run around too much"). Somebody frightened
    /// whose goal is a hand on the floor, or on a thing with no use to them,
    /// goes to a spot of their own in a ring round it, through the doors on
    /// the way, and stands there facing it. The hand beats a leader's call,
    /// swerving and following other runners; the flames at their danger
    /// distance still send them off.
    /// <para>
    /// Since the fourth pass (2026-09-30) the goal is theirs
    /// (<see cref="AgentHand"/>): they set about it once their conviction
    /// passes the answer line, keep it when the hand comes off once it has
    /// passed the commit line, and drift off it only as that conviction
    /// fades on their own beat, the strong-willed first. Nobody breaks away
    /// while the hand is on them; a hand dragged along carries them with it
    /// (the owner: "so agents can be guided with this"); somebody who can
    /// find no way there gives up for a beat and tries again.
    /// </para>
    /// <para>
    /// A push (the right button) sends them walking away from it, out past its
    /// full strength, and then they run on as they would; a push dragged along
    /// herds them. A push is never kept.
    /// </para>
    /// <para>
    /// Only the hand's places nothing else answers: the floor, or a thing with
    /// no use to somebody frightened. The bottle is the fighters', the card and
    /// its door the card's, fallen crates the heave's, a pull station the
    /// alarm's; a door is weighed in the choice of door, and a hand landing on
    /// one brings that choice forward. Draws nothing of its own.
    /// </para>
    /// </summary>
    internal sealed class HandGatherBehaviour : ITaskOption, IBindable
    {
        /// <summary>Within this of their spot they stand; beyond it they walk.</summary>
        private const int AtTheSpotMillimetres = 450;

        /// <summary>Further off than this they run; nearer, they walk up.</summary>
        private const int RunUntilMillimetres = 2500;

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly FrightenedWalk walk;
        private InfluenceSystem influence;
        private DoorBehaviour doorBehaviour;
        private HandHeaveBehaviour handHeave;
        private AlarmSystem alarms;
        private PhysicsObjectSystem objects;
        private KeycardSystem keycards;
        private DoorSystem doors;
        private Threats threats;

        public HandGatherBehaviour(SimulationContext context, WorldGeometry geometry, FrightenedWalk walk)
        {
            this.context = context;
            this.geometry = geometry;
            this.walk = walk;
        }

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
            doorBehaviour = systems.DoorBehaviour;
            handHeave = systems.HandHeave;
            alarms = systems.Alarms;
            objects = systems.Objects;
            keycards = systems.Keycards;
            doors = systems.Doors;
            threats = systems.Threats;
        }

        public static bool IsAnswering(Agent agent) => agent.Intent.Activity == AgentActivityState.AnsweringTheHand;

        public bool IsDoing(Agent agent) => IsAnswering(agent);

        public MotorIntent? Continue(Agent agent, in Situation situation) => Update(agent, situation.InDanger);

        /// <summary>A goal for the hand, or a push on now; out of the flames, on their feet, hands and care free.</summary>
        public bool Wants(Agent agent, in Situation situation) =>
            !situation.InDanger && influence != null && agent.Body.State == AgentBodyState.Upright &&
            agent.Carry.ItemIndex < 0 && agent.Help.TargetIndex < 0 && !agent.Burning.IsBurning &&
            (agent.Hand.Press != 0UL || (influence.Count > 0 && influence[0].Repels));

        /// <summary>
        /// They come to the hand and stand there, or are sent off by a push --
        /// or, for a goal on a door, think again about which door, a beat
        /// later. Nothing for a goal that is not theirs to answer here.
        /// </summary>
        public bool TryBegin(Agent agent, in Situation situation, out MotorIntent? first)
        {
            first = Begin(agent, situation);
            return first.HasValue;
        }

        private MotorIntent? Begin(Agent agent, in Situation situation)
        {
            bool inDanger = situation.InDanger;
            if (inDanger || influence == null || agent.Body.State != AgentBodyState.Upright ||
                agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Burning.IsBurning)
            {
                return null;
            }

            // A push first: it is never a goal, and it sends them off once.
            if (influence.TryGetLivePush(agent, out InfluenceSystem.Place push, out int pushed))
            {
                return TryStartPush(agent, push, pushed, inDanger);
            }

            if (!influence.TryGetPull(agent, out InfluenceSystem.Place place, out int drive))
            {
                return null;
            }

            if (agent.Hand.DoneWithPress == place.EventId)
            {
                // Done with this one: through the door it was on.
                return null;
            }

            if (!IsTheGatherersOwn(place) && !IsADoorToGoThrough(agent, place))
            {
                RethinkForADoor(agent, place, drive);
                return null;
            }

            // Already going out of the building: a pull does not turn them
            // back from the way out. Between rooms it does.
            InfluenceSettings rules = context.Scenario.Influence;
            if ((doorBehaviour.IsLeaving(agent) && geometry.DoorLeadsOutside(agent.Doors.ExitDoorIndex)) ||
                drive < rules.ActsAgainstNatureFromPerMille || !influence.MayAnswer(agent))
            {
                return null;
            }

            // They answer it: the gold hand goes up over them, and nobody's
            // call takes them off it.
            LeaderBehaviour.StopFollowing(agent);
            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            agent.Intent.SwerveEndTick = 0;
            influence.Answer(agent, place, drive);
            return Update(agent, inDanger);
        }

        /// <summary>
        /// Whether a pull is this behaviour's to answer: on the floor (not
        /// beside a pull station, not over fallen crates), or on a thing with
        /// no use to somebody frightened. Draws nothing.
        /// </summary>
        private bool IsTheGatherersOwn(in InfluenceSystem.Place place)
        {
            if (place.Door >= 0 || (handHeave != null && handHeave.IsClearing(place)) ||
                (alarms != null && alarms.StationAt(influence, place) >= 0))
            {
                return false;
            }

            if (place.Thing < 0)
            {
                return true;
            }

            return !objects.IsEquipment(place.Thing) && (keycards == null || place.Thing != keycards.Card);
        }

        /// <summary>
        /// A hand on a door the frightened walk through (2026-10-03, a level
        /// that says so): an inside door, not locked, not heaped, not a push,
        /// and not one whose far side is inside their danger distance of the
        /// flames -- nobody follows the hand into fire.
        /// </summary>
        private bool IsADoorToGoThrough(Agent agent, in InfluenceSystem.Place place)
        {
            if (!context.Scenario.Influence.FrightenedGoThroughAHeldDoor || place.Door < 0 || place.Repels ||
                geometry.DoorLeadsOutside(place.Door) || doors == null || doors.StateOf(place.Door) == DoorState.Locked ||
                doors.IsPiled(place.Door) || (handHeave != null && handHeave.IsClearing(place)))
            {
                return false;
            }

            int far = FarSide(agent, place, out _);
            if (far < 0)
            {
                return false;
            }

            // Through, away from the fire: the far side has to be further
            // from the flames than this side, so a hand on a door is never a
            // pull back toward them for whoever is already past it.
            int through = context.Scenario.Influence.ThroughTheDoorMillimetres;
            LogicalPosition beyond = geometry.DoorPointFrom(place.Door, far, 0, -through);
            LogicalPosition behind = geometry.DoorPointFrom(place.Door, far, 0, through);
            if (threats == null)
            {
                return false;
            }

            long ahead = threats.NearestDistanceSquared(beyond, out _, out _);
            long back = threats.NearestDistanceSquared(behind, out _, out _);
            return ahead > back && !threats.AnyCloserThan(beyond, TraitEffects.DangerDistance(agent, context.Scenario));
        }

        /// <summary>
        /// Which of the door's two rooms is the far side for them: the other
        /// one from the room they are in, or, felt through another doorway,
        /// from the one of the two that is open to where they stand. -1 when
        /// neither.
        /// </summary>
        private int FarSide(Agent agent, in InfluenceSystem.Place place, out int near)
        {
            int room = geometry.RoomOf(agent);
            near = -1;
            if (room == place.RoomA || (room != place.RoomB && place.RoomA >= 0 && geometry.RoomsOpenToEachOther(room, place.RoomA)))
            {
                near = place.RoomA;
                return place.RoomB;
            }

            if (room == place.RoomB || (place.RoomB >= 0 && geometry.RoomsOpenToEachOther(room, place.RoomB)))
            {
                near = place.RoomB;
                return place.RoomA;
            }

            return -1;
        }

        /// <summary>Where they walk to go through a held door: a few steps into the far side, on standing room.</summary>
        private LogicalPosition BeyondTheDoor(Agent agent, in InfluenceSystem.Place place)
        {
            int far = FarSide(agent, place, out _);
            LogicalPosition beyond = geometry.DoorPointFrom(place.Door, far, 0, -context.Scenario.Influence.ThroughTheDoorMillimetres);
            return geometry.Navigation.NearestStandableTo(beyond, context.Scenario.World.OccupancyRadiusMillimetres, 600);
        }

        /// <summary>
        /// A goal on a door, driven hard: their next choice of door is brought
        /// forward, once a press (2026-09-30; they used to go on toward the
        /// door they had chosen for up to a second and a half before the hand
        /// was weighed at all).
        /// </summary>
        private void RethinkForADoor(Agent agent, in InfluenceSystem.Place place, int drive)
        {
            if (place.Door < 0 || agent.Hand.RethoughtForPress == place.EventId ||
                drive < context.Scenario.Influence.ActsAgainstNatureFromPerMille || !influence.MayAnswer(agent))
            {
                return;
            }

            agent.Hand.RethoughtForPress = place.EventId;
            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// A push, felt strongly and taken in, by somebody inside its full
        /// strength: they set off away from it. Written once a press.
        /// </summary>
        private MotorIntent? TryStartPush(Agent agent, in InfluenceSystem.Place push, int felt, bool inDanger)
        {
            if (!influence.InsideThePush(agent, push) || felt < context.Scenario.Influence.ActsAgainstNatureFromPerMille)
            {
                return null;
            }

            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            if (agent.Hand.PushedByPress != push.EventId)
            {
                agent.Hand.PushedByPress = push.EventId;
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentPushedAwayByInfluence,
                    agent.Body.Position, felt, 0, push.EventId, push.Target);
            }

            return Update(agent, inDanger);
        }

        /// <summary>Going to the hand and standing at it, or walking away from a push.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            if (influence == null || inDanger || agent.Body.State != AgentBodyState.Upright || agent.Burning.IsBurning ||
                agent.Carry.ItemIndex >= 0)
            {
                // The flames came near; they went down or caught. On their
                // own again at once.
                Stop(agent);
                return null;
            }

            PanicSettings panic = context.Scenario.Panic;
            if (influence.TryGetLivePush(agent, out InfluenceSystem.Place push, out int pushed) &&
                agent.Hand.PushedByPress == push.EventId)
            {
                if (!influence.InsideThePush(agent, push) || pushed <= 0)
                {
                    // Out of it: they run on as they would.
                    Stop(agent);
                    return null;
                }

                LogicalPosition away = influence.AwayFromThePush(agent, push);
                if (walk.TryStep(agent, away, agent.Personality.PanicSpeed, push.EventId, out MotorIntent flee))
                {
                    return flee;
                }

                Stop(agent);
                return null;
            }

            if (!influence.TryGetPull(agent, out InfluenceSystem.Place place, out int drive) || !agent.Hand.Acting ||
                drive <= 0)
            {
                // The goal is over: the hand came off before they were sure
                // of it, their conviction faded, or something else took them.
                Stop(agent);
                return null;
            }

            bool throughADoor = IsADoorToGoThrough(agent, place);
            if (!IsTheGatherersOwn(place) && !throughADoor)
            {
                // Dragged onto something with its own answer (a pull station,
                // fallen crates): that answer takes over, the goal kept.
                StopStanding(agent);
                return null;
            }

            if (agent.Hand.DoneWithPress == place.EventId)
            {
                Stop(agent);
                return null;
            }

            LogicalPosition spot = throughADoor ? BeyondTheDoor(agent, place) : influence.GatherSpotFor(agent, place);
            if (!throughADoor && context.Scenario.Influence.FrightenedGoOnWhenLetGo &&
                influence.CurrentPress != place.EventId &&
                IntegerMath.Distance(agent.Body.Position, spot) <= AtTheSpotMillimetres)
            {
                // Let go of, and they had got there (2026-10-03): done, and on
                // their own again, running on from where the hand left them.
                InfluenceSystem.Done(agent);
                agent.Hand.DoneWithPress = place.EventId;
                StopStanding(agent);
                return null;
            }

            long distance = IntegerMath.Distance(agent.Body.Position, spot);
            if (distance <= AtTheSpotMillimetres)
            {
                // At their spot: facing the hand, feet planted, frightened still.
                int facing = IntegerMath.HeadingBetween(agent.Body.Position, place.At, agent.Body.Heading);
                return PanicIntent.StandAndFace(agent, facing, panic);
            }

            int speed = distance > RunUntilMillimetres
                ? agent.Personality.PanicSpeed
                : System.Math.Max(agent.Personality.CalmSpeed,
                    (int)(agent.Personality.PanicSpeed * distance / RunUntilMillimetres));
            if (walk.TryStep(agent, spot, speed, place.EventId, out MotorIntent step))
            {
                return step;
            }

            // No way there (a door on the way that will not open): a beat,
            // and less sure of it, then they try again.
            influence.GiveUp(agent);
            StopStanding(agent);
            return null;
        }

        /// <summary>Back to running, thinking again a beat later; whatever they hold for the hand is over.</summary>
        private void Stop(Agent agent)
        {
            InfluenceSystem.Interrupted(agent);
            StopStanding(agent);
        }

        /// <summary>Back to running, thinking again a beat later, the goal untouched.</summary>
        private void StopStanding(Agent agent)
        {
            walk.Forget(agent);
            if (IsAnswering(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
            }

            context.ThinkAgainSoon(agent.Intent);
        }
    }
}
