using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Fire extinguishers. A brave person near a small fire, or a
    /// compassionate one who can see somebody alight, grabs the nearest
    /// extinguisher, carries it to arm's length of the flames and sprays.
    /// The spray is a cone: it puts burning floor squares out square by
    /// square, puts out burning people and things, and knocks anyone caught
    /// in it off their feet and backwards. There is only a few seconds of it
    /// in the bottle, and a weak person is shoved backwards by the recoil
    /// instead of holding their ground.
    /// </summary>
    internal sealed class ExtinguisherBehaviour : IPanicOption
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly FireSystem fire;
        private readonly BodySystem body;
        private readonly FlammablesSystem flammables;
        private readonly ItemBehaviour items;
        private readonly ExtinguisherSettings settings;

        /// <summary>Scratch space for the squares under the spray, reused every tick.</summary>
        private readonly List<int> sprayed = new List<int>();

        public ExtinguisherBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            PhysicsObjectSystem objects,
            FireSystem fire,
            BodySystem body,
            FlammablesSystem flammables,
            ItemBehaviour items)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.fire = fire;
            this.body = body;
            this.flammables = flammables;
            this.items = items;
            settings = context.Scenario.Extinguishers;
        }

        public static bool IsFighting(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.FetchingExtinguisher || activity == AgentActivityState.Spraying;
        }

        /// <summary>
        /// Whether this person would take on the fire: brave enough for the
        /// flames, or kind enough to hose down someone who is alight, and not
        /// already carrying something or helping someone.
        /// </summary>
        private bool WouldFight(Agent agent, bool someoneAlight)
        {
            if (agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Body.State != AgentBodyState.Upright)
            {
                return false;
            }

            // Somebody has just put a bottle down in front of them: for a
            // while they need less nerve than usual to be the one who takes it.
            int nerveNeeded = context.Tick < agent.Carry.SawAnExtinguisherUntilTick
                ? settings.FightMinimumBravery - settings.OfferedBraveryBonus
                : settings.FightMinimumBravery;

            return agent.Traits.Bravery >= nerveNeeded ||
                   (someoneAlight && agent.Traits.Compassion >= settings.SaveMinimumCompassion);
        }

        /// <summary>
        /// Considered in the panic decision: pick up an extinguisher and go
        /// for the fire. Returns no intent when this person is not doing that.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsFighting(agent))
            {
                return Update(agent, inDanger);
            }

            if (inDanger || !fire.Active)
            {
                // Too close to the flames to think about fighting them.
                return null;
            }

            int burningPerson = NearestBurningPerson(agent);
            if (!WouldFight(agent, burningPerson >= 0))
            {
                return null;
            }

            // Nothing to put out, or far too much of it to try. The flames used
            // to have to be in the room they were standing in, because setting
            // off for a fire anywhere else only walked them into a wall. Now
            // they can be anywhere they could walk to.
            int room = geometry.RoomOf(agent);
            if (burningPerson < 0 &&
                (fire.BurningCount == 0 || fire.BurningCount > settings.FightMaximumFireCells ||
                 room < 0 || !CanReachTheFlames(agent)))
            {
                return null;
            }

            int extinguisher = NearestFreeExtinguisher(agent);
            if (extinguisher < 0)
            {
                return null;
            }

            agent.Carry.ItemIndex = extinguisher;
            agent.Carry.Holding = false;
            agent.Intent.Activity = AgentActivityState.FetchingExtinguisher;
            agent.Intent.ActivityEndTick = checked(context.Tick + settings.FetchTimeoutTicks);
            return Update(agent, inDanger);
        }

        /// <summary>
        /// Whether there is fire they could walk to. Flames themselves are not
        /// somewhere anybody can stand, so the question is really about the
        /// floor beside them, which is where somebody fighting a fire stands.
        /// </summary>
        private bool CanReachTheFlames(Agent agent)
        {
            if (fire.NearestDistanceSquared(agent.Body.Position, out LogicalPosition flames) == long.MaxValue)
            {
                return false;
            }

            return geometry.Routes.CanGetFromHereToThere(agent.Body.Position, NextToTheFlames(flames), bodyRadius);
        }

        /// <summary>How far around a burning spot to look for floor somebody could fight it from.</summary>
        private const int StandingRoomMillimetres = 2500;

        /// <summary>
        /// Floor beside a burning spot. Flames on a desk sit on a square nobody
        /// can stand on, so asking whether the burning square itself can be
        /// walked to always answers no -- and everybody gives up on fighting a
        /// fire that is perfectly easy to walk up to.
        ///
        /// This used to clamp the point into the bounds of the room it was
        /// already inside, which does nothing at all.
        /// </summary>
        private LogicalPosition NextToTheFlames(LogicalPosition flames)
        {
            return geometry.Navigation.NearestStandableTo(flames, bodyRadius, StandingRoomMillimetres);
        }

        /// <summary>Fetching it, carrying it to the flames, spraying, and dropping it when it runs dry.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            int tick = context.Tick;
            int item = agent.Carry.ItemIndex;

            // With the bottle in their hands they will stand closer to the
            // flames than they otherwise would, but not in them.
            long nerve = TraitEffects.DangerDistance(agent, context.Scenario) * settings.DangerTolerancePercent / 100L;
            bool tooClose = agent.Carry.Holding ? fire.AnyCloserThan(agent.Body.Position, (int)nerve) : inDanger;
            if (item < 0 || tooClose || !agent.Body.IsOnTheirFeet || agent.Burning.IsBurning ||
                tick >= agent.Intent.ActivityEndTick)
            {
                GiveUp(agent);
                return null;
            }

            if (!agent.Carry.Holding)
            {
                // On the way to it: someone else may have got there first.
                if (objects.HolderOf(item) >= 0)
                {
                    GiveUp(agent);
                    return null;
                }

                LogicalPosition where = objects.PositionOf(item);
                long gap = IntegerMath.Distance(agent.Body.Position, where);
                if (gap > settings.PickUpDistanceMillimetres)
                {
                    return Walk(agent, where, agent.Personality.PanicSpeed);
                }

                objects.PickUp(item, agent);
                agent.Carry.Holding = true;
                agent.Intent.ActivityEndTick = checked(tick + settings.FightTimeoutTicks);
                context.Events.Append(tick, agent.Id, CausalEventType.AgentTookExtinguisher,
                    agent.Body.Position, 0, 0, agent.Fear.ScaredEventId, objects.IdOf(item));
                return Walk(agent, where, 0);
            }

            if (objects.FuelOf(item) <= 0)
            {
                // Empty: they drop it and run.
                context.Events.Append(tick, agent.Id, CausalEventType.ExtinguisherEmptied,
                    agent.Body.Position, 0, 0, agent.Doors.AttemptEventId, objects.IdOf(item));
                items.PutDownWhereTheyStand(agent, agent.Fear.ScaredEventId);
                GiveUp(agent);
                return null;
            }

            // Where to point it: the nearest person alight, or the nearest flames.
            LogicalPosition target;
            int burningPerson = NearestBurningPerson(agent);
            if (burningPerson >= 0)
            {
                target = crowd.All[burningPerson].Body.Position;
            }
            else if (fire.NearestDistanceSquared(agent.Body.Position, out LogicalPosition flames) < long.MaxValue &&
                     geometry.Routes.CanGetFromHereToThere(agent.Body.Position, NextToTheFlames(flames), bodyRadius))
            {
                target = flames;
            }
            else
            {
                // Nothing left they can get to. They used to give up on
                // anything outside the room they stood in; now it is anything
                // there is no way to at all.
                items.PutDownWhereTheyStand(agent, agent.Fear.ScaredEventId);
                GiveUp(agent);
                return null;
            }

            long distance = IntegerMath.Distance(agent.Body.Position, target);
            int heading = geometry.Routes.HeadingToward(agent.Body.Position, target, bodyRadius, agent.Body.Heading);

            // Once the trigger is down they keep it down while the jet still
            // reaches; otherwise they close to arm's length first, at a run
            // if they are chasing somebody who is alight.
            bool spraying = agent.Intent.Activity == AgentActivityState.Spraying;
            long closeEnough = spraying ? settings.SprayRangeMillimetres : settings.StandOffMillimetres;
            if (distance > closeEnough)
            {
                agent.Intent.Activity = AgentActivityState.FetchingExtinguisher;
                return Walk(agent, target,
                    burningPerson >= 0 ? agent.Personality.PanicSpeed : agent.Personality.CalmSpeed);
            }

            if (!spraying)
            {
                // The trigger goes down: fix the phase of the sweep now, once,
                // so two people fighting the same fire do not wave in unison.
                agent.Carry.SprayPhase = context.Random.NextIntInclusive(0, SweepPeriodTicks - 1);
            }

            agent.Intent.Activity = AgentActivityState.Spraying;
            agent.Intent.Target = target;
            return new MotorIntent(
                IntegerMath.NormalizeDegrees(heading + Sweep(agent)),
                0,
                agent.Personality.PanicTurnRate,
                context.Scenario.Panic.Acceleration);
        }

        /// <summary>
        /// How long a full sweep takes, there and back. Two and a half seconds
        /// reads as somebody working a jet across a fire rather than shaking it.
        /// </summary>
        private const int SweepPeriodTicks = 125;

        /// <summary>
        /// The jet is not a laser. Somebody holding the trigger down works it
        /// back and forth across what they are fighting: a triangle wave, so the
        /// arc is even and turns at the ends rather than snapping round. How wide
        /// it swings is strength -- a strong pair of hands keeps a narrow, steady
        /// arc while the weak are wrestled about by the hose, which is the same
        /// rule the recoil already follows. Arithmetic on the tick and one draw
        /// per trigger-pull, so it adds no randomness of its own.
        /// </summary>
        private int Sweep(Agent agent)
        {
            int width = TraitEffects.SpraySweepDegrees(agent, context.Scenario);
            if (width <= 0)
            {
                return 0;
            }

            int step = (context.Tick + agent.Carry.SprayPhase) % SweepPeriodTicks;
            int half = SweepPeriodTicks / 2;

            // 0 .. half .. 0 again, scaled to the full swing either side of centre.
            int up = step < half ? step : SweepPeriodTicks - step;
            return up * (2 * width) / half - width;
        }

        /// <summary>Phase 4½: everyone holding a trigger down sprays, before anybody moves.</summary>
        public void Spray()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (agent.Intent.Activity != AgentActivityState.Spraying || !agent.IsParticipating ||
                    agent.Carry.ItemIndex < 0 || !agent.Carry.Holding)
                {
                    continue;
                }

                SprayOnce(agent, agent.Carry.ItemIndex);
            }
        }

        /// <summary>One tick of spray: fuel down, fire out, people blasted, and the recoil.</summary>
        private void SprayOnce(Agent agent, int item)
        {
            int tick = context.Tick;
            objects.UseFuel(item, 1);
            ulong spray = context.Events.Append(tick, agent.Id, CausalEventType.ExtinguisherSprayed,
                agent.Body.Position, settings.SprayRangeMillimetres, agent.Body.Heading,
                agent.Fear.ScaredEventId, objects.IdOf(item)).EventId;

            // The floor squares under the cone go out, nearest first.
            fire.CollectBurningWithin(agent.Body.Position, settings.SprayRangeMillimetres, sprayed);
            int put = 0;
            for (int i = 0; i < sprayed.Count && put < settings.CellsPerTick; i++)
            {
                int cell = sprayed[i];
                if (InTheCone(agent, fire.CellClosestPoint(cell, agent.Body.Position)) && fire.Douse(cell, agent.Id, spray))
                {
                    put++;
                }
            }

            // Anything burning in the cone is put out, and anyone in it is
            // blasted off their feet — including whoever was alight.
            flammables.DouseWithin(agent.Body.Position, settings.SprayRangeMillimetres, spray, agent.Body.Heading, Cone(agent));

            using (Crowd.Nearby near = crowd.Within(agent.Body.Position, settings.SprayRangeMillimetres))
            {
                for (int c = 0; c < near.Count; c++)
                {
                    Agent other = crowd.All[near[c]];
                    if (other == agent || !other.IsParticipating || !InTheCone(agent, other.Body.Position))
                    {
                        continue;
                    }

                    if (other.Burning.IsBurning)
                    {
                        body.PutOutPerson(other, spray);
                    }

                    Blast(agent, other, spray);
                }
            }

            Recoil(agent, spray);
        }

        /// <summary>
        /// The jet shoves people over: knocked backwards away from the
        /// sprayer and onto the floor. Only once, not every tick of the spray.
        /// </summary>
        private void Blast(Agent sprayer, Agent hit, ulong sprayEventId)
        {
            // Someone already on the floor is left where they are.
            if (context.Tick < hit.Body.BlastedUntilTick || hit.Body.State != AgentBodyState.Upright)
            {
                return;
            }

            int away = IntegerMath.HeadingBetween(sprayer.Body.Position, hit.Body.Position, hit.Body.Heading);
            hit.Body.BlastedUntilTick = checked(context.Tick + settings.BlastRecoveryTicks);
            context.Events.Append(context.Tick, sprayer.Id, CausalEventType.AgentBlasted,
                hit.Body.Position, 0, away, sprayEventId, hit.Id);
            body.ShoveBack(hit, away, settings.BlastPushMillimetres, sprayEventId);
        }

        /// <summary>
        /// The bottle pushes back. A strong person holds it steady; a weak
        /// one is walked backwards by it, and the weakest sit down hard.
        /// </summary>
        private void Recoil(Agent agent, ulong sprayEventId)
        {
            int push = settings.RecoilPushMillimetres - agent.Traits.Strength * settings.RecoilPushPerStrength;
            if (push <= 0)
            {
                return;
            }

            int away = IntegerMath.NormalizeDegrees(agent.Body.Heading + 180);
            if (agent.Traits.Strength <= settings.RecoilFloorsMaximumStrength)
            {
                body.ShoveBack(agent, away, push, sprayEventId);
                return;
            }

            body.Slide(agent, away, push);
        }

        private MotorIntent Walk(Agent agent, LogicalPosition where, int speed)
        {
            agent.Intent.Target = where;
            int heading = IntegerMath.HeadingBetween(agent.Body.Position, where, agent.Body.Heading);
            return new MotorIntent(heading, speed, agent.Personality.PanicTurnRate, context.Scenario.Panic.Acceleration);
        }

        /// <summary>Half the width of the spray cone, in degrees.</summary>
        private int Cone(Agent agent) => settings.SprayConeDegrees / 2;

        private bool InTheCone(Agent agent, LogicalPosition point)
        {
            if (LogicalPosition.DistanceSquared(agent.Body.Position, point) >
                (long)settings.SprayRangeMillimetres * settings.SprayRangeMillimetres)
            {
                return false;
            }

            int heading = IntegerMath.HeadingBetween(agent.Body.Position, point, agent.Body.Heading);
            return System.Math.Abs(IntegerMath.SignedAngleDifference(agent.Body.Heading, heading)) <= Cone(agent);
        }

        /// <summary>The nearest person who is alight and close enough to reach, or -1.</summary>
        private int NearestBurningPerson(Agent agent)
        {
            int room = geometry.RoomOf(agent);
            long reach = settings.SaveRangeMillimetres;
            long bestDistance = reach;
            int best = -1;
            FlowField walking = geometry.Routes.ReachFrom(agent.Body.Position, bodyRadius);
            using Crowd.Nearby candidates = crowd.Within(agent.Body.Position, reach);
            for (int c = 0; c < candidates.Count; c++)
            {
                Agent other = crowd.All[candidates[c]];
                if (other == agent || !other.IsParticipating || !other.Burning.IsBurning)
                {
                    continue;
                }

                if (walking == null)
                {
                    // No routing to spare this tick: the room they stand in,
                    // which is as far as anybody could see before.
                    if (geometry.RoomOf(other) != room)
                    {
                        continue;
                    }
                }

                long distance = walking == null
                    ? IntegerMath.Distance(agent.Body.Position, other.Body.Position)
                    : geometry.Routes.DistanceIn(walking, other.Body.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidates[c];
                }
            }

            return best;
        }

        /// <summary>The nearest extinguisher with fuel left that nobody is holding, or -1.</summary>
        private int NearestFreeExtinguisher(Agent agent)
        {
            long reach = settings.FetchRangeMillimetres;
            long bestDistance = reach * reach;
            int best = -1;
            IReadOnlyList<int> bottles = objects.Equipment;
            for (int b = 0; b < bottles.Count; b++)
            {
                int i = bottles[b];
                if (objects.HolderOf(i) >= 0 || objects.FuelOf(i) <= 0)
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

            return best;
        }

        /// <summary>Back to running: they stop fighting the fire (whatever they are holding stays in their arms).</summary>
        private void GiveUp(Agent agent)
        {
            if (IsFighting(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Intent.NextPanicDecisionTick = context.Tick;
            }
        }
    }
}
