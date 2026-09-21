namespace Paniq.Simulation
{
    /// <summary>
    /// Taking charge. Someone with leadership who is not in immediate danger
    /// looks around every second or so and forms a plan, in this order:
    /// <list type="number">
    /// <item>the door out of here will not open, and somebody strong is
    /// within earshot, so they send that person to break it down;</item>
    /// <item>the fire is still small and there is an extinguisher about, so
    /// they send the bravest person within earshot to fetch it;</item>
    /// <item>otherwise they simply call the people near them along behind
    /// them.</item>
    /// </list>
    /// Either way they shout, which gathers the people nearby: anyone with
    /// less leadership of their own, who is not cruel, follows them until the
    /// leader is out, down, alight, or no longer worth following. Orders are
    /// events naming the person ordered, never a hold on them: whoever is
    /// ordered may still decide otherwise.
    /// </summary>
    internal sealed class LeaderBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly DoorBehaviour doorBehaviour;
        private readonly FireSystem fire;
        private readonly SoundSystem sound;
        private readonly PhysicsObjectSystem objects;
        private readonly Locomotion locomotion;
        private readonly LeadershipSettings settings;

        public LeaderBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            DoorSystem doors,
            DoorBehaviour doorBehaviour,
            FireSystem fire,
            SoundSystem sound,
            PhysicsObjectSystem objects,
            Locomotion locomotion)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.doorBehaviour = doorBehaviour;
            this.fire = fire;
            this.sound = sound;
            this.objects = objects;
            this.locomotion = locomotion;
            settings = context.Scenario.Leadership;
        }

        /// <summary>
        /// The panic decision asks this first. A leader takes charge (and
        /// keeps running themselves); a follower goes where their leader
        /// goes. Returns no intent for anyone doing neither.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger)
        {
            if (agent.Leading.FollowingIndex >= 0)
            {
                return Follow(agent, inDanger);
            }

            if (inDanger || agent.Traits.Leadership < settings.LeaderMinimum ||
                context.Tick < agent.Leading.NextPlanTick)
            {
                return null;
            }

            agent.Leading.NextPlanTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.PlanMinimumTicks, settings.PlanMaximumTicks));

            if (!TryOrderADoorBrokenDown(agent) && !TryOrderTheFireFought(agent))
            {
                Rally(agent, FireReactionEventType.LeaderCalledPeopleOn, default);
            }

            // Leaders lead by going: their own running is decided as usual.
            return null;
        }

        /// <summary>
        /// A way out this person has found shut, with somebody strong enough
        /// to break it standing within earshot: they send them at it.
        /// </summary>
        private bool TryOrderADoorBrokenDown(Agent leader)
        {
            int room = geometry.RoomOf(leader);
            int door = -1;
            long bestDistance = long.MaxValue;
            for (int d = 0; d < doors.Count; d++)
            {
                if (!geometry.DoorLeadsOutside(d) || geometry.IsDoorOpen(d) || !leader.Doors.FoundShut[d] ||
                    !geometry.DoorTouchesRoom(d, room))
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(leader.Body.Position, geometry.DoorCentre(d));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    door = d;
                }
            }

            if (door < 0)
            {
                return false;
            }

            Agent breaker = NearbyBest(leader, settings.OrderRangeMillimetres, out _, IsStrongEnoughToBreakDoors);
            if (breaker == null)
            {
                return false;
            }

            if (!Obeys(breaker, leader))
            {
                // They shout anyway; whoever it was simply does not take it on.
                Rally(leader, FireReactionEventType.LeaderCalledPeopleOn, default);
                return true;
            }

            ulong order = Rally(leader, FireReactionEventType.LeaderOrderedDoorBroken, doors.IdOf(door), breaker.Id);

            // Sent at that door: they stop trailing after the leader, or the
            // next thing they decide would be to follow them again and the
            // door would never get touched.
            StopFollowing(breaker);

            // Sent at that door, and they will not give up on it while it holds.
            breaker.Doors.ExitDoorIndex = door;
            breaker.Doors.ApproachRoom = geometry.RoomOf(breaker);
            breaker.Doors.FoundShut[door] = false;
            breaker.Doors.AvoidUntilTick[door] = 0;
            breaker.Leading.OrderedDoor = door;
            breaker.Leading.OrderedUntilTick = checked(context.Tick + settings.OrderLastsTicks);
            breaker.Leading.OrderEventId = order;
            breaker.Intent.Activity = AgentActivityState.Fleeing;
            breaker.Intent.NextPanicDecisionTick = context.Tick;
            return true;
        }

        /// <summary>The fire is still small and a bottle is free: they send the bravest person nearby for it.</summary>
        private bool TryOrderTheFireFought(Agent leader)
        {
            if (fire.BurningCount == 0 || fire.BurningCount > context.Scenario.Extinguishers.FightMaximumFireCells)
            {
                return false;
            }

            int bottle = -1;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects.KindOf(i) == PhysicsObjectKind.Extinguisher && objects.HolderOf(i) < 0 && objects.FuelOf(i) > 0 &&
                    geometry.RoomAtPoint(objects.PositionOf(i)) == geometry.RoomOf(leader))
                {
                    bottle = i;
                    break;
                }
            }

            if (bottle < 0)
            {
                return false;
            }

            Agent fighter = NearbyBest(leader, settings.OrderRangeMillimetres, out int bravery,
                other => other.Carry.ItemIndex < 0 && other.Traits.Bravery >= settings.OrderedFightMinimumBravery);
            if (fighter == null || bravery <= 0)
            {
                return false;
            }

            if (!Obeys(fighter, leader))
            {
                Rally(leader, FireReactionEventType.LeaderCalledPeopleOn, default);
                return true;
            }

            ulong order = Rally(leader, FireReactionEventType.LeaderOrderedFireFought, objects.IdOf(bottle), fighter.Id);

            // Sent for the bottle: they stop following the leader first, or the
            // next thing they decide would be to fall in behind them again.
            StopFollowing(fighter);

            // Told to grab it: that is now their idea too.
            fighter.Carry.ItemIndex = bottle;
            fighter.Carry.Holding = false;
            fighter.Intent.Activity = AgentActivityState.FetchingExtinguisher;
            fighter.Intent.ActivityEndTick = checked(context.Tick + context.Scenario.Extinguishers.FetchTimeoutTicks);
            fighter.Leading.OrderedUntilTick = checked(context.Tick + settings.OrderLastsTicks);
            fighter.Leading.OrderEventId = order;
            return true;
        }

        /// <summary>
        /// A shout that gathers whoever is near: they follow this leader
        /// until they are out, down, or the leader stops being one.
        /// </summary>
        private ulong Rally(Agent leader, FireReactionEventType eventType, SimulationId target, SimulationId ordered = default)
        {
            ulong order = context.Events.Append(context.Tick, leader.Id, eventType, leader.Body.Position,
                settings.RallyRangeMillimetres, 0, leader.Fear.ScaredEventId,
                ordered.Value != 0UL ? ordered : target).EventId;
            sound.Yell(leader, order);

            long range = settings.RallyRangeMillimetres;
            int room = geometry.RoomOf(leader);
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                // Somebody frozen with fear does not hear a shout; they
                // have to be shaken (see HelpBehaviour).
                if (other == leader || !other.IsParticipating || other.Leading.FollowingIndex == leader.Index ||
                    other.Intent.Activity == AgentActivityState.Frozen || other.Burning.IsBurning ||
                    geometry.RoomOf(other) != room ||
                    LogicalPosition.DistanceSquared(other.Body.Position, leader.Body.Position) > range * range)
                {
                    continue;
                }

                if (!Obeys(other, leader))
                {
                    continue;
                }

                other.Leading.FollowingIndex = leader.Index;
                other.Leading.FollowUntilTick = checked(context.Tick + settings.FollowLastsTicks);
                other.Leading.OrderEventId = order;
            }

            return order;
        }

        /// <summary>
        /// Whether this person does as they are told: not the cruel, not
        /// someone who leads more than the leader does, and the more nervous
        /// they are the more likely they are to fall in behind.
        /// </summary>
        private bool Obeys(Agent follower, Agent leader)
        {
            if (follower.Traits.Evil >= settings.DefiantMinimumEvil ||
                follower.Traits.Leadership >= leader.Traits.Leadership)
            {
                return false;
            }

            int chance = settings.ObeyBasePercent + follower.Traits.Nervousness * settings.ObeyPercentPerNervousness -
                         follower.Traits.Bravery * settings.ObeyPercentPerBravery;
            return context.Random.NextPercent(chance);
        }

        /// <summary>Going where the leader goes, a stride behind them.</summary>
        private MotorIntent? Follow(Agent agent, bool inDanger)
        {
            // Fear that roots someone to the spot beats any shout.
            if (agent.Intent.Activity == AgentActivityState.Frozen || agent.Burning.IsBurning)
            {
                agent.Leading.FollowingIndex = -1;
                return null;
            }

            Agent leader = crowd.All[agent.Leading.FollowingIndex];
            bool worthFollowing = leader.IsParticipating && !leader.Burning.IsBurning &&
                                  leader.Body.State == AgentBodyState.Upright &&
                                  geometry.RoomOf(leader) == geometry.RoomOf(agent);
            if (inDanger || context.Tick >= agent.Leading.FollowUntilTick || !worthFollowing)
            {
                StopFollowing(agent);
                return null;
            }

            long gap = IntegerMath.Distance(agent.Body.Position, leader.Body.Position);
            if (gap <= settings.FollowGapMillimetres)
            {
                // Close enough: they run their own way from here.
                return null;
            }

            agent.Intent.Activity = AgentActivityState.Following;
            agent.Intent.Target = leader.Body.Position;
            int heading = locomotion.Steer(agent,
                IntegerMath.HeadingBetween(agent.Body.Position, leader.Body.Position, agent.Body.Heading),
                TraitEffects.PanicPeopleAvoidPercent(agent, context.Scenario),
                context.Scenario.Panic.WallAvoidPercent,
                context.Scenario.Panic.ObjectAvoidPercent);
            return new MotorIntent(heading, agent.Personality.PanicSpeed, agent.Personality.PanicTurnRate,
                context.Scenario.Panic.Acceleration);
        }

        /// <summary>Phase 4½: who is being followed, for the icons over their heads.</summary>
        public void CountFollowers()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                agents[i].Leading.LedCount = 0;
            }

            for (int i = 0; i < agents.Length; i++)
            {
                int leader = agents[i].Leading.FollowingIndex;
                if (leader >= 0 && agents[i].IsParticipating)
                {
                    agents[leader].Leading.LedCount++;
                }
            }
        }

        public static void StopFollowing(Agent agent)
        {
            agent.Leading.FollowingIndex = -1;
            if (agent.Intent.Activity == AgentActivityState.Following)
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
            }
        }

        /// <summary>Whether someone sent at a door is still under orders to keep at it.</summary>
        public static bool IsUnderOrdersAtThisDoor(Agent agent, int door, int tick)
        {
            return agent.Leading.OrderedDoor == door && tick < agent.Leading.OrderedUntilTick;
        }

        private bool IsStrongEnoughToBreakDoors(Agent agent)
        {
            return agent.Traits.Strength >= context.Scenario.Traits.DoorBreakMinimumStrength;
        }

        /// <summary>
        /// The nearest person in the same room within reach who fits, with
        /// their bravery, or null. Nobody already under orders is picked again.
        /// </summary>
        private Agent NearbyBest(Agent leader, int reach, out int bravery, System.Func<Agent, bool> fits)
        {
            bravery = 0;
            int room = geometry.RoomOf(leader);
            long best = (long)reach * reach;
            Agent found = null;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                // Nobody frozen with fear, alight, off their feet, or
                // already under somebody's orders.
                if (other == leader || !other.IsParticipating || other.Burning.IsBurning ||
                    other.Intent.Activity == AgentActivityState.Frozen ||
                    other.Body.State != AgentBodyState.Upright || context.Tick < other.Leading.OrderedUntilTick ||
                    geometry.RoomOf(other) != room || !fits(other))
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(leader.Body.Position, other.Body.Position);
                if (distance < best)
                {
                    best = distance;
                    found = other;
                    bravery = other.Traits.Bravery;
                }
            }

            return found;
        }
    }
}
