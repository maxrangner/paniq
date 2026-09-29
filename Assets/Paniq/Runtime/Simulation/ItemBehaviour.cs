using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// People and items. Calm people now and then tidy up: they walk over to
    /// a box or chair they can lift, pick it up, carry it somewhere else and
    /// set it down. Carrying slows them down, the more the heavier the load.
    /// Anyone carrying something who is startled, knocked off their feet or
    /// set alight lets go: the nervous fumble and drop it, the rest fling it
    /// away from them in whatever direction they happen to be facing, which is
    /// a reflex rather than a plan. Calm tidying is not logged (like a calm
    /// push); drops and throws are.
    /// </summary>
    internal sealed class ItemBehaviour : IBindable
    {
        private InfluenceSystem influence;
        private KeycardSystem keycards;
        private ExtinguisherBehaviour extinguishers;

        /// <summary>Built after this behaviour.</summary>
        public void Bind(Systems systems)
        {
            influence = systems.Influence;
            keycards = systems.Keycards;
            extinguishers = systems.Extinguishers;
        }

        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly FlammablesSystem flammables;
        private readonly ItemSettings settings;
        private readonly CalmSettings calm;

        public ItemBehaviour(SimulationContext context, WorldGeometry geometry, PhysicsObjectSystem objects,
            FlammablesSystem flammables)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.geometry = geometry;
            this.objects = objects;
            this.flammables = flammables;
            settings = context.Scenario.Items;
            calm = context.Scenario.Calm;
        }

        public static bool IsTidying(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.FetchingItem || activity == AgentActivityState.PickingUp ||
                   activity == AgentActivityState.CarryingItem || activity == AgentActivityState.SettingDown;
        }

        /// <summary>Maybe go and tidy something up: the nearest item within reach that this person can lift. False if there is none.</summary>
        public bool TryStartTidying(Agent agent)
        {
            if (agent.Carry.ItemIndex >= 0)
            {
                // Their hands are already full: their own bag, or something they
                // are already tidying away.
                return false;
            }

            int best = -1;
            long bestDistance = (long)settings.FetchRangeMillimetres * settings.FetchRangeMillimetres;
            using PhysicsObjectSystem.Nearby candidates =
                objects.Gather(UniformGridIndex.Around(agent.Body.Position, settings.FetchRangeMillimetres));
            for (int c = 0; c < candidates.Count; c++)
            {
                int i = candidates[c];
                if (!IsFreeToTake(agent, i))
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(agent.Body.Position, objects.PositionOf(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            if (best < 0)
            {
                return false;
            }

            agent.Carry.ItemIndex = best;

            // Clutter, to carry off: nothing left over from a fetch for the
            // player may make this one a bottle to keep or a card to pocket.
            agent.Carry.KeepIt = false;
            agent.Carry.Pocket = false;
            agent.Intent.Activity = AgentActivityState.FetchingItem;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(calm.StrollTimeoutTicks));
            return true;
        }

        /// <summary>On the floor, still, light enough, and not burning or burnt.</summary>
        private bool IsFreeToTake(Agent agent, int index)
        {
            // An extinguisher is not clutter: it is left on its wall until
            // somebody needs it (see ExtinguisherBehaviour). Neither is a
            // chair. An office full of chairs is an office full of the nearest
            // liftable thing, so nobody ever sat down in one -- they spent the
            // day carrying the furniture about instead. A frightened person
            // still wedges a chair against a door, which is a different act
            // and lives in BarricadeBehaviour.
            return !objects.IsEquipment(index) && !objects.CanBeSatOn(index) && IsTakeable(agent, index);
        }

        /// <summary>Free, still, on the floor, light enough for them, and not burning or burnt: could be picked up at all.</summary>
        private bool IsTakeable(Agent agent, int index)
        {
            return !objects.IsDormant(index) && objects.HolderOf(index) < 0 && objects.OccupantOf(index) < 0 &&
                   !objects.IsMoving(index) && objects.CanLift(agent, index) &&
                   flammables.ObjectState(index) == ObjectBurnState.Intact;
        }

        /// <summary>What they are fetching may still be taken: for a thing to keep, anything they could lift; for clutter, clutter.</summary>
        private bool MayTake(Agent agent, int index) =>
            agent.Carry.Pocket ? keycards.CanBePocketed(agent, index)
            : agent.Carry.KeepIt ? IsTakeable(agent, index)
            : IsFreeToTake(agent, index);

        /// <summary>
        /// A thing the player has pointed at that this person could pick up
        /// (2026-09-27): free, light enough, and not a chair -- a bottle on
        /// its wall included, which tidying never takes, and the keycard,
        /// which goes in a pocket.
        /// </summary>
        public bool CanFetchForTheInfluence(Agent agent, int index) =>
            agent.Carry.ItemIndex < 0 && !objects.CanBeSatOn(index) &&
            (IsTakeable(agent, index) || keycards.CanBePocketed(agent, index));

        /// <summary>
        /// Sets off to fetch a thing the player pointed at (2026-09-27): a
        /// bottle to hold on to, as if it were their own bag; anything else
        /// to carry off and set down somewhere else, as tidying does. False
        /// when it cannot be fetched after all.
        /// </summary>
        public bool FetchForTheInfluence(Agent agent, int index)
        {
            if (!CanFetchForTheInfluence(agent, index))
            {
                return false;
            }

            agent.Carry.ItemIndex = index;
            agent.Carry.KeepIt = objects.IsEquipment(index);
            agent.Carry.Pocket = objects.IsPocketable(index);
            agent.Intent.Activity = AgentActivityState.FetchingItem;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(calm.StrollTimeoutTicks));
            return true;
        }

        /// <summary>
        /// One tick of tidying up. Returns false once it is over (set down,
        /// or given up), and the calm behaviour then chooses what to do next.
        /// </summary>
        public bool UpdateTidying(Agent agent, out int goalHeading, out int goalSpeed)
        {
            int tick = context.Tick;
            AgentIntent intent = agent.Intent;
            int item = agent.Carry.ItemIndex;
            goalHeading = agent.Body.Heading;
            goalSpeed = 0;
            bool timedOut = tick >= intent.ActivityEndTick;

            switch (intent.Activity)
            {
                case AgentActivityState.FetchingItem:
                {
                    if (timedOut || agent.Body.BlockedTicks > calm.BlockedGiveUpTicks || !MayTake(agent, item))
                    {
                        agent.Carry.ItemIndex = -1;
                        agent.Carry.KeepIt = false;
                        agent.Carry.Pocket = false;
                        return false;
                    }

                    LogicalPosition itemAt = objects.PositionOf(item);
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, itemAt, agent.Body.Heading);
                    if (IsWithinReach(agent, item))
                    {
                        intent.Activity = AgentActivityState.PickingUp;
                        intent.ActivityEndTick = checked(tick + context.Jittered(settings.PickUpTicks));
                        return true;
                    }

                    goalSpeed = agent.Personality.CalmSpeed;
                    return true;
                }

                case AgentActivityState.PickingUp:
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, objects.PositionOf(item), agent.Body.Heading);
                    if (!timedOut)
                    {
                        return true;
                    }

                    if (!MayTake(agent, item) || !IsWithinReach(agent, item))
                    {
                        agent.Carry.ItemIndex = -1;
                        agent.Carry.KeepIt = false;
                        agent.Carry.Pocket = false;
                        return false;
                    }

                    if (agent.Carry.Pocket)
                    {
                        // The keycard, fetched because the player pointed at
                        // it: into the pocket, the pull spent, the arms free.
                        agent.Carry.ItemIndex = -1;
                        agent.Carry.Pocket = false;
                        ulong pull = influence != null ? influence.Spend(agent, -1, item) : 0UL;
                        keycards.Pocket(agent, item, pull);
                        return false;
                    }

                    objects.PickUp(item, agent);
                    agent.Carry.Holding = true;

                    // Picked up because the player pointed at it: the pull on
                    // it is spent. Nothing on it, nothing written.
                    ulong spent = influence != null ? influence.Spend(agent, -1, item) : 0UL;
                    if (agent.Carry.KeepIt)
                    {
                        // A bottle taken off its wall for the player: theirs
                        // to hold on to, as their own bag is, until something
                        // frightens them and they let go of it like anything
                        // else -- and then it is a free bottle for the brave.
                        agent.Carry.KeepIt = false;
                        agent.Carry.OwnsIt = true;
                        context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentTookExtinguisher,
                            agent.Body.Position, 0, 0, spent, objects.IdOf(item));
                        return false;
                    }

                    StartCarrying(agent);
                    return true;

                case AgentActivityState.CarryingItem:
                {
                    long distance = IntegerMath.Distance(agent.Body.Position, intent.Target);
                    if (distance < calm.StrollArrivalDistanceMillimetres * 2L || timedOut ||
                        agent.Body.BlockedTicks > calm.BlockedGiveUpTicks)
                    {
                        intent.Activity = AgentActivityState.SettingDown;
                        intent.ActivityEndTick = checked(tick + context.Jittered(settings.SetDownTicks));
                        return true;
                    }

                    goalHeading = geometry.Routes.HeadingToward(
                        agent.Body.Position, intent.Target, bodyRadius, agent.Body.Heading);
                    goalSpeed = agent.Personality.CalmSpeed;
                    return true;
                }

                case AgentActivityState.SettingDown:
                    if (!timedOut)
                    {
                        return true;
                    }

                    if (!objects.FindSpotToPutDown(item, agent, out LogicalPosition spot))
                    {
                        // Nowhere clear here: carry it somewhere else.
                        StartCarrying(agent);
                        return true;
                    }

                    objects.Release(item, spot, 0, 0, 0UL);
                    agent.Carry.ItemIndex = -1;
                    agent.Carry.Holding = false;
                    return false;

                default:
                    return false;
            }
        }

        private void StartCarrying(Agent agent)
        {
            AgentIntent intent = agent.Intent;
            intent.Activity = AgentActivityState.CarryingItem;
            intent.ActivityEndTick = checked(context.Tick + context.Jittered(calm.StrollTimeoutTicks));
            long minimumSquared = (long)settings.CarryMinimumDistanceMillimetres * settings.CarryMinimumDistanceMillimetres;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                intent.Target = geometry.RandomInteriorPoint(geometry.RoomOf(agent), calm.StrollWallMarginMillimetres);
                if (LogicalPosition.DistanceSquared(agent.Body.Position, intent.Target) >= minimumSquared)
                {
                    return;
                }
            }
        }

        private bool IsWithinReach(Agent agent, int item)
        {
            long reach = context.Scenario.World.OccupancyRadiusMillimetres + (long)objects.RadiusOf(item) + settings.ReachMillimetres;
            return LogicalPosition.DistanceSquared(agent.Body.Position, objects.PositionOf(item)) <= reach * reach;
        }

        /// <summary>A load slows the carrier: up to the scenario's percentage, in proportion to how much of their limit it is.</summary>
        public MotorIntent Burdened(Agent agent, MotorIntent intent)
        {
            if (!agent.Carry.Holding)
            {
                return intent;
            }

            long share = Math.Min(100L, objects.MassOf(agent.Carry.ItemIndex) * 100L / TraitEffects.CarryLimitGrams(agent, context.Scenario));
            int speed = (int)(intent.GoalSpeed * (100L - settings.CarrySlowdownPercent * share / 100L) / 100L);
            return new MotorIntent(intent.GoalHeading, speed, intent.TurnRate, intent.Acceleration);
        }

        /// <summary>After movement: every held item stays in front of its carrier.</summary>
        public void FollowCarriers(Agent[] agents)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Carry.Holding)
                {
                    objects.FollowCarrier(agents[i].Carry.ItemIndex, agents[i]);
                }
            }
        }

        /// <summary>Someone who collapsed with an item in their arms leaves it on the floor where they fell.</summary>
        /// <summary>Sets whatever they hold down where they stand (an empty extinguisher).</summary>
        public void PutDownWhereTheyStand(Agent agent, ulong causeEventId)
        {
            if (!agent.Carry.Holding)
            {
                agent.Carry.ItemIndex = -1;
                return;
            }

            int item = agent.Carry.ItemIndex;
            LogicalPosition spot = objects.FindSpotToPutDown(item, agent, out LogicalPosition clear) ? clear : agent.Body.Position;
            ulong dropped = context.Events.Append(context.Tick, agent.Id, CausalEventType.ItemDropped, spot, 0, 0,
                causeEventId, objects.IdOf(item)).EventId;
            objects.Release(item, spot, 0, 0, dropped);
            agent.Carry.ItemIndex = -1;
            agent.Carry.Holding = false;
        }

        public void DropFromLost(Agent agent)
        {
            if (!agent.Carry.Holding)
            {
                return;
            }

            int item = agent.Carry.ItemIndex;
            LogicalPosition spot = objects.FindSpotToPutDown(item, agent, out LogicalPosition clear) ? clear : agent.Body.Position;
            ulong dropped = context.Events.Append(context.Tick, agent.Id, CausalEventType.ItemDropped, spot, 0, 0,
                agent.Burning.EventId, objects.IdOf(item)).EventId;
            objects.Release(item, spot, 0, 0, dropped);
            agent.Carry.ItemIndex = -1;
            agent.Carry.Holding = false;
        }

        /// <summary>
        /// Lets go of a held item when the carrier is no longer calmly
        /// carrying it: on fire or off their feet they drop it; startled or
        /// scared, the nervous drop it and the rest throw it ahead of them;
        /// merely distracted, they put it down.
        /// If there is no clear spot beside them they hold on for now.
        /// </summary>
        public void LetGoIfNeeded(Agent agent)
        {
            if (!agent.Carry.Holding)
            {
                return;
            }

            // Somebody fighting the fire is holding that extinguisher on purpose.
            if (ExtinguisherBehaviour.IsFighting(agent) &&
                objects.IsEquipment(agent.Carry.ItemIndex) &&
                agent.Body.State == AgentBodyState.Upright && !agent.Burning.IsBurning)
            {
                return;
            }

            // And somebody carrying something to wedge a door with means to keep
            // hold of it, frightened as they are.
            if (BarricadeBehaviour.IsBarricading(agent) &&
                agent.Body.State == AgentBodyState.Upright && !agent.Burning.IsBurning)
            {
                return;
            }

            // Somebody brave who took the bottle for the player keeps it when
            // the fright comes and takes the fire on with it (2026-09-29),
            // instead of flinging it away and going back for it.
            if (agent.Carry.OwnsIt && extinguishers != null && objects.IsEquipment(agent.Carry.ItemIndex) &&
                extinguishers.WouldKeepTheBottle(agent))
            {
                return;
            }

            bool calmAndUpright = agent.Fear.State == AgentFearState.Calm && agent.Body.State == AgentBodyState.Upright &&
                                  !agent.Burning.IsBurning;
            if (calmAndUpright && (IsTidying(agent) || agent.Carry.OwnsIt))
            {
                // Either mid-tidy, or it is their own bag and they simply carry
                // it about with them.
                return;
            }

            int item = agent.Carry.ItemIndex;
            if (!objects.FindSpotToPutDown(item, agent, out LogicalPosition spot))
            {
                return;
            }

            if (calmAndUpright)
            {
                // Distracted (by a noise, say): they just put it down, unhurried and unlogged.
                objects.Release(item, spot, 0, 0, 0UL);
                agent.Carry.ItemIndex = -1;
                agent.Carry.Holding = false;
                agent.Carry.OwnsIt = false;
                return;
            }

            ulong cause = agent.Burning.IsBurning ? agent.Burning.EventId
                : agent.Body.State != AgentBodyState.Upright ? agent.Body.EventId
                : agent.Fear.ScaredEventId != 0UL ? agent.Fear.ScaredEventId
                : agent.Fear.AlertEventId;
            bool drop = agent.Burning.IsBurning || agent.Body.State != AgentBodyState.Upright ||
                        agent.Traits.Nervousness >= settings.DropNervousness;
            if (drop)
            {
                ulong dropped = context.Events.Append(context.Tick, agent.Id, CausalEventType.ItemDropped, spot, 0, 0,
                    cause, objects.IdOf(item)).EventId;
                objects.Release(item, spot, 0, 0, dropped);
            }
            else
            {
                int speed = objects.ThrowSpeed(agent, item);
                int heading = objects.PanicThrowHeading(agent, spot);
                LogicalPosition velocity = IntegerMath.Displacement(heading, speed);
                ulong thrown = context.Events.Append(context.Tick, agent.Id, CausalEventType.ItemThrown, spot, speed, 0,
                    cause, objects.IdOf(item)).EventId;
                objects.Release(item, spot, velocity.X, velocity.Z, thrown);
            }

            agent.Carry.ItemIndex = -1;
            agent.Carry.Holding = false;
            agent.Carry.OwnsIt = false;
        }
    }
}
