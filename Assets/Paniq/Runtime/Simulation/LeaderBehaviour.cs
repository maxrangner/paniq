using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Taking charge. Someone with leadership who is not in immediate danger
    /// looks around every second or so and forms a plan, in this order:
    /// <list type="number">
    /// <item>the door out of here will not open, and somebody strong is
    /// within earshot, so they send that person to break it down;</item>
    /// <item>otherwise they simply call the people near them along behind
    /// them.</item>
    /// </list>
    /// (Sending somebody at a small fire with a bottle was set aside on
    /// 2026-10-03, with the brave fighting it unasked.)
    /// Either way they shout, which gathers the people nearby: anyone with
    /// less leadership of their own, who is not cruel, follows them until the
    /// leader is out, down, alight, or no longer worth following. Orders are
    /// events naming the person ordered, never a hold on them: whoever is
    /// ordered takes it up in their own turn, a beat later
    /// (<see cref="TakeUpAnOrder"/>), and may still decide otherwise.
    /// <para>
    /// Whoever falls in behind a leader, or is already following them when
    /// they shout again, is told everything the leader knows about the
    /// building (see <see cref="WayfindingSystem.Share"/>). A host who works
    /// here can walk a room of lost visitors out; a leader who is lost
    /// themselves can only lead them round in the same circles.
    /// </para>
    /// </summary>
    internal sealed class LeaderBehaviour : ITaskOption
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly DoorBehaviour doorBehaviour;
        private readonly FireSystem fire;
        private readonly SoundSystem sound;
        private readonly PhysicsObjectSystem objects;
        private readonly Locomotion locomotion;
        private readonly WayfindingSystem wayfinding;
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
            Locomotion locomotion,
            WayfindingSystem wayfinding)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.doorBehaviour = doorBehaviour;
            this.fire = fire;
            this.sound = sound;
            this.objects = objects;
            this.locomotion = locomotion;
            this.wayfinding = wayfinding;
            settings = context.Scenario.Leadership;
        }

        public bool IsDoing(Agent agent) => agent.Leading.FollowingIndex >= 0;

        public MotorIntent? Continue(Agent agent, in Situation situation) => Follow(agent, situation.InDanger);

        /// <summary>A leader, out of the flames, whose time to look round has come.</summary>
        public bool Wants(Agent agent, in Situation situation) =>
            !situation.InDanger && agent.Traits.Leadership >= settings.LeaderMinimum &&
            context.Tick >= agent.Leading.NextPlanTick;

        /// <summary>
        /// A leader takes charge: a door sent at, or a shout that gathers
        /// whoever is near. Leaders lead by going, so their own running is
        /// decided as usual and this gives no intent.
        /// </summary>
        public bool TryBegin(Agent agent, in Situation situation, out MotorIntent? first)
        {
            first = Begin(agent, situation);
            return first.HasValue;
        }

        private MotorIntent? Begin(Agent agent, in Situation situation)
        {
            agent.Leading.NextPlanTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.PlanMinimumTicks, settings.PlanMaximumTicks));

            if (!TryOrderADoorBrokenDown(agent))
            {
                Rally(agent, CausalEventType.LeaderCalledPeopleOn, default);
            }

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
                // Any way out they have found shut themselves, or that is
                // wedged, not only one in the room they happen to be standing
                // in: the person they send can walk to it now.
                if (!geometry.DoorLeadsOutside(d) || geometry.IsDoorOpen(d) || !leader.Knowledge.Knows(d) ||
                    (!leader.Doors.FoundShut[d] && !doors.IsObstructed(d)) || doors.NeedsKeycard(d))
                {
                    // A card door is nobody's to break down (2026-09-27).
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

            // A wedged door just needs shifting -- a much lower bar than
            // breaking a locked one down.
            bool wedged = doors.IsObstructed(door);
            Agent breaker = NearbyBest(leader, settings.OrderRangeMillimetres, out _,
                wedged ? (System.Func<Agent, bool>)IsStrongEnoughToShiftAnObstruction : IsStrongEnoughToBreakDoors);
            if (breaker == null)
            {
                return false;
            }

            if (!Obeys(breaker, leader))
            {
                // They shout anyway; whoever it was simply does not take it on.
                Rally(leader, CausalEventType.LeaderCalledPeopleOn, default);
                return true;
            }

            ulong order = Rally(leader, CausalEventType.LeaderOrderedDoorBroken, doors.IdOf(door), breaker.Id);

            // An offer, not a hold (2026-10-03, the owner's rule: nobody
            // reacts on the tick a thing happens; the audit's E1): the order
            // is theirs to take up in their own turn, a beat later.
            breaker.Leading.OfferedDoor = door;
            breaker.Leading.OfferedFromTick = context.ReactionTick();
            breaker.Leading.OfferEventId = order;
            SimulationContext.ChooseNoLaterThan(breaker.Intent, breaker.Leading.OfferedFromTick);
            return true;
        }

        /// <summary>
        /// In their own turn: an order to break a door down, from a beat after
        /// it was shouted. Sent at that door, they stop trailing after the
        /// leader (or the next thing they decided would be to follow them
        /// again and the door would never get touched), and they will not
        /// give up on it while it holds. Somebody frozen, down or alight by
        /// then lets it go.
        /// </summary>
        public void TakeUpAnOrder(Agent agent)
        {
            AgentLeading leading = agent.Leading;
            int door = leading.OfferedDoor;
            if (door < 0 || context.Tick < leading.OfferedFromTick)
            {
                return;
            }

            leading.OfferedDoor = -1;
            if (agent.Intent.Activity == AgentActivityState.Frozen || agent.Burning.IsBurning ||
                agent.Body.State != AgentBodyState.Upright || geometry.IsDoorOpen(door))
            {
                return;
            }

            ulong order = leading.OfferEventId;
            StopFollowing(agent);
            wayfinding.Learn(agent, door, WayLearned.Told, order);
            agent.Doors.ExitDoorIndex = door;
            agent.Doors.ApproachRoom = geometry.RoomOf(agent);
            agent.Doors.FoundShut[door] = false;
            agent.Doors.AvoidUntilTick[door] = 0;
            leading.OrderedDoor = door;
            leading.OrderedUntilTick = checked(context.Tick + context.Jittered(settings.OrderLastsTicks));
            leading.OrderEventId = order;
            agent.Intent.Activity = AgentActivityState.Fleeing;
            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// A shout that gathers whoever is near: they follow this leader
        /// until they are out, down, or the leader stops being one.
        /// </summary>
        private ulong Rally(Agent leader, CausalEventType eventType, SimulationId target, SimulationId ordered = default)
        {
            ulong order = context.Events.Append(context.Tick, leader.Id, eventType, leader.Body.Position,
                settings.RallyRangeMillimetres, 0, leader.Fear.ScaredEventId,
                ordered.Value != 0UL ? ordered : target).EventId;
            sound.Yell(leader, order);

            long range = settings.RallyRangeMillimetres;
            int room = geometry.RoomOf(leader);
            using Crowd.Nearby near = crowd.Within(leader.Body.Position, range);
            for (int c = 0; c < near.Count; c++)
            {
                Agent other = crowd.All[near[c]];
                // Somebody frozen with fear does not hear a shout; they
                // have to be shaken (see HelpBehaviour).
                if (other == leader || !other.IsParticipating ||
                    other.Intent.Activity == AgentActivityState.Frozen || other.Burning.IsBurning ||
                    !geometry.RoomsOpenToEachOther(room, geometry.RoomOf(other)) ||
                    LogicalPosition.DistanceSquared(other.Body.Position, leader.Body.Position) > range * range)
                {
                    continue;
                }

                if (other.Leading.FollowingIndex == leader.Index)
                {
                    // Already behind them: nothing new to decide, but whatever
                    // the leader has found out since, they hear now.
                    wayfinding.Share(leader, other, order);
                    continue;
                }

                if (!Obeys(other, leader))
                {
                    continue;
                }

                wayfinding.Share(leader, other, order);
                other.Leading.FollowingIndex = leader.Index;
                other.Leading.FollowFromTick = context.ReactionTick();
                other.Leading.FollowUntilTick = checked(other.Leading.FollowFromTick + context.Jittered(settings.FollowLastsTicks));
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

            if (context.Tick < agent.Leading.FollowFromTick)
            {
                // Called, but not yet turned to follow: a few ticks late, like
                // every reaction. Until then they carry on as they were.
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

        private bool IsStrongEnoughToShiftAnObstruction(Agent agent)
        {
            return agent.Traits.Strength >= context.Scenario.Blockades.ShoveMinimumStrength;
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
            using Crowd.Nearby near = crowd.Within(leader.Body.Position, reach);
            for (int c = 0; c < near.Count; c++)
            {
                Agent other = crowd.All[near[c]];
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
