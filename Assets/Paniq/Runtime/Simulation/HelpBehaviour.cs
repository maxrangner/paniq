using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// People helping each other. A compassionate, not-too-timid runner who
    /// sees someone frozen with fear nearby goes over and shakes them until
    /// they snap out of it. A strong, compassionate runner who sees someone
    /// knocked out cold grabs them and drags them toward an open door (or
    /// just away from the fire); if the helper gets out, so does the person
    /// they are dragging. The cruel never help. Helping stops when the helper
    /// is in danger, loses their footing, catches fire or gives up.
    /// </summary>
    internal sealed class HelpBehaviour : IPanicOption, IBindable
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly Threats threats;
        private readonly FearSystem fear;
        private readonly BodySystem body;
        private readonly PhysicsObjectSystem objects;
        private readonly Locomotion locomotion;
        private readonly HelpSettings settings;
        private readonly int radius;

        /// <summary>
        /// Per person: who last set out to help them (their index), or -1.
        /// Read through <see cref="IsAlreadyBeingHelped"/>, which checks the
        /// helper is still at it, so a stale entry is harmless. This used to
        /// be a table of everybody rebuilt from the whole crowd every time
        /// anybody kind thought about helping, which was every tick.
        /// </summary>
        private readonly int[] helpedBy;

        /// <summary>Everybody's physical body: somebody being dragged is hauled along the floor as one.</summary>
        private readonly PeopleBodies people;

        /// <summary>Set once the doors exist, so a dragger can tell a jammed doorway from a clear one.</summary>
        private DoorSystem doors;

        public HelpBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            Threats threats,
            FearSystem fear,
            BodySystem body,
            PhysicsObjectSystem objects,
            Locomotion locomotion,
            PeopleBodies people)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.threats = threats;
            this.fear = fear;
            this.body = body;
            this.objects = objects;
            this.locomotion = locomotion;
            this.people = people;
            helpedBy = new int[crowd.All.Length];
            for (int i = 0; i < helpedBy.Length; i++)
            {
                helpedBy[i] = -1;
            }

            settings = context.Scenario.Help;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;
        }

        public static bool IsHelping(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.ShakingAwake || activity == AgentActivityState.Grabbing ||
                   activity == AgentActivityState.Dragging;
        }

        // ---------------------------------------------------------------- deciding

        /// <summary>
        /// This tick's helping, for a panicking person: carry on helping, or
        /// maybe start. Returns no intent when they are not helping, and the
        /// panic behaviour decides instead.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (!IsHelping(agent))
            {
                if (inDanger || agent.Intent.Activity != AgentActivityState.Fleeing || agent.Carry.Holding ||
                    !TryStart(agent))
                {
                    return null;
                }
            }

            if (inDanger)
            {
                // Too close to the flames to stay: let go and run.
                StopHelping(agent, true);
                return null;
            }

            return agent.Intent.Activity == AgentActivityState.Dragging ? Drag(agent) : GoToOrWorkOn(agent);
        }

        private bool TryStart(Agent agent)
        {
            AgentTraitValues traits = agent.Traits;
            if (traits.Evil > settings.HelpMaximumEvil || traits.Compassion < settings.ShakeMinimumCompassion)
            {
                return false;
            }

            bool canShake = traits.Bravery >= settings.ShakeMinimumBravery;
            bool canDrag = traits.Strength >= settings.DragMinimumStrength && traits.Compassion >= settings.DragMinimumCompassion;
            if (!canShake && !canDrag)
            {
                return false;
            }

            int danger = TraitEffects.DangerDistance(agent, context.Scenario);

            // Nobody outside the longer of the two reaches can be chosen, so
            // only the people near enough are worth looking at.
            long furthest = Math.Max(settings.ShakeRangeMillimetres, settings.DragRangeMillimetres);
            int best = -1;
            long bestDistance = long.MaxValue;
            bool bestIsShake = false;
            using Crowd.Nearby candidates = crowd.Within(agent.Body.Position, furthest);
            for (int c = 0; c < candidates.Count; c++)
            {
                int i = candidates[c];
                Agent other = crowd.All[i];
                if (other == agent || !other.IsParticipating || other.Burning.IsBurning || i == agent.Help.GaveUpOnIndex ||
                    IsAlreadyBeingHelped(i) || threats.AnyCloserThan(other.Body.Position, danger))
                {
                    continue;
                }

                bool frozen = canShake && other.Intent.Activity == AgentActivityState.Frozen &&
                              other.Body.State == AgentBodyState.Upright;
                bool knockedOut = canDrag && other.Body.State == AgentBodyState.Unconscious;
                long range = frozen ? settings.ShakeRangeMillimetres : settings.DragRangeMillimetres;
                long distance = LogicalPosition.DistanceSquared(agent.Body.Position, other.Body.Position);
                if ((!frozen && !knockedOut) || distance > range * range || distance >= bestDistance)
                {
                    continue;
                }

                best = i;
                bestDistance = distance;
                bestIsShake = frozen;
            }

            if (best < 0)
            {
                return false;
            }

            agent.Help.TargetIndex = best;
            helpedBy[best] = agent.Index;
            agent.Help.WorkEndTick = 0;
            agent.Help.GiveUpTick = checked(context.Tick + settings.ReachTimeoutTicks);
            agent.Intent.Activity = bestIsShake ? AgentActivityState.ShakingAwake : AgentActivityState.Grabbing;
            agent.Doors.ExitDoorIndex = -1;
            return true;
        }

        /// <summary>
        /// Whether somebody is already seeing to this person, so that two
        /// people do not both set off for the same casualty. The last helper
        /// to set out for them is remembered; they count only while they are
        /// still in the run, still helping, and still helping this person,
        /// which is exactly what a walk of the whole crowd used to establish.
        /// </summary>
        private bool IsAlreadyBeingHelped(int person)
        {
            int helper = helpedBy[person];
            if (helper < 0)
            {
                return false;
            }

            Agent by = crowd.All[helper];
            return by.IsParticipating && IsHelping(by) && by.Help.TargetIndex == person;
        }

        /// <summary>Running to the person in need, then shaking them, or getting a grip on them.</summary>
        private MotorIntent? GoToOrWorkOn(Agent agent)
        {
            int tick = context.Tick;
            Agent target = crowd.All[agent.Help.TargetIndex];
            bool shaking = agent.Intent.Activity == AgentActivityState.ShakingAwake;
            bool stillInNeed = target.IsParticipating && !target.Burning.IsBurning &&
                               (shaking
                                   ? target.Intent.Activity == AgentActivityState.Frozen && target.Body.State == AgentBodyState.Upright
                                   : target.Body.State == AgentBodyState.Unconscious);
            if (!stillInNeed || (agent.Help.WorkEndTick == 0 && tick >= agent.Help.GiveUpTick))
            {
                StopHelping(agent, false);
                return null;
            }

            int toTarget = geometry.Routes.HeadingToward(
                agent.Body.Position, target.Body.Position, radius, agent.Body.Heading);
            // Somebody lying on the floor is a body's length long, and a helper
            // takes hold of the nearest part of them, not their middle: coming
            // at them end on, the middle is half a body further away.
            long reach = radius * 2L + settings.ReachMillimetres +
                         (target.IsDown ? PeopleBodies.HeightMillimetres / 2 - radius : 0);
            if (LogicalPosition.DistanceSquared(agent.Body.Position, target.Body.Position) > reach * reach)
            {
                // Still on the way.
                agent.Help.WorkEndTick = 0;
                int heading = locomotion.Steer(agent, toTarget, 0, context.Scenario.Panic.WallAvoidPercent,
                    context.Scenario.Panic.ObjectAvoidPercent);
                return new MotorIntent(heading, agent.Personality.PanicSpeed, agent.Personality.PanicTurnRate,
                    context.Scenario.Panic.Acceleration);
            }

            if (agent.Help.WorkEndTick == 0)
            {
                agent.Help.WorkEndTick = checked(tick + (shaking
                    ? context.Random.NextIntInclusive(settings.ShakeMinimumTicks, settings.ShakeMaximumTicks)
                    : settings.GrabTicks));
            }

            if (tick < agent.Help.WorkEndTick)
            {
                return new MotorIntent(toTarget, 0, agent.Personality.PanicTurnRate, context.Scenario.Panic.Acceleration);
            }

            if (shaking)
            {
                FinishShaking(agent, target);
                return null;
            }

            // Got a grip: start dragging.
            agent.Help.GrabEventId = context.Events.Append(tick, agent.Id, FireReactionEventType.AgentGrabbed,
                target.Body.Position, 0, 0, agent.Fear.ScaredEventId, target.Id).EventId;
            agent.Intent.Activity = AgentActivityState.Dragging;
            agent.Body.BlockedTicks = 0;
            agent.Help.StuckTicks = 0;
            ChooseDragTarget(agent);
            return Drag(agent);
        }

        private void FinishShaking(Agent agent, Agent target)
        {
            bool forGood = target.Personality.Temperament == AgentPanicTemperament.FreezeForever;
            if (!forGood || context.Random.NextPercent(settings.ShakeFreezeForeverSuccessPercent))
            {
                CausalEvent shook = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentShookAwake,
                    target.Body.Position, 0, 0, agent.Fear.ScaredEventId, target.Id);
                fear.Unfreeze(target, shook.EventId);
            }
            else
            {
                // Too far gone: they leave them and do not try again.
                agent.Help.GaveUpOnIndex = agent.Help.TargetIndex;
            }

            StopHelping(agent, false);
        }

        /// <summary>The doors are built after this behaviour, so they are handed over once everything exists.</summary>
        public void Bind(Systems systems) => doors = systems.Doors;

        // ---------------------------------------------------------------- dragging

        /// <summary>
        /// Toward the way out that is the shortest walk hauling somebody, or
        /// else away from the fire.
        ///
        /// It used to be the nearest way out as the crow flies, walls and all.
        /// In a building of four rooms that happened to be right; in any bigger
        /// one it sends somebody dragging an unconscious body at a blank wall,
        /// because the door on the other side of it was nearest.
        /// </summary>
        private void ChooseDragTarget(Agent agent)
        {
            int best = -1;
            long bestDistance = long.MaxValue;
            FlowField walking = geometry.Routes.ReachFrom(agent.Body.Position, radius);
            for (int d = 0; d < geometry.DoorCount; d++)
            {
                if (!geometry.IsDoorOpen(d) || !geometry.DoorLeadsOutside(d) || doors.IsObstructed(d) ||
                    !agent.Knowledge.Knows(d))
                {
                    // Something wedged in the gap: they would never get through.
                    // Or a way out they do not know is there, which to them is
                    // no way out at all.
                    continue;
                }

                long distance = walking == null
                    ? IntegerMath.Distance(agent.Body.Position, geometry.DoorCentre(d))
                    : geometry.Routes.DistanceIn(walking, geometry.DoorCentre(d));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = d;
                }
            }

            agent.Help.DragDoor = best;
            if (best >= 0)
            {
                agent.Doors.ExitDoorIndex = best;
                agent.Intent.Target = geometry.DoorPoint(best, 0, context.Scenario.Exits.OutsideTargetMillimetres);
                return;
            }

            agent.Doors.ExitDoorIndex = -1;
            if (threats.NearestDistanceSquared(agent.Body.Position, out LogicalPosition flames, out _) == long.MaxValue)
            {
                agent.Intent.Target = agent.Body.Position;
                return;
            }

            int away = IntegerMath.HeadingBetween(flames, agent.Body.Position, agent.Body.Heading);
            agent.Intent.Target = geometry.ClampIntoRoom(agent.Body.Position,
                agent.Body.Position + IntegerMath.Displacement(away, settings.DragAwayDistanceMillimetres));
        }

        private MotorIntent? Drag(Agent agent)
        {
            Agent target = crowd.All[agent.Help.TargetIndex];
            if (!target.IsParticipating || target.Body.State != AgentBodyState.Unconscious ||
                agent.Help.StuckTicks > settings.DragGiveUpBlockedTicks)
            {
                StopHelping(agent, true);
                return null;
            }

            // A door they were making for was shut: pick again.
            if (agent.Help.DragDoor >= 0 &&
                (!geometry.IsDoorOpen(agent.Help.DragDoor) || doors.IsObstructed(agent.Help.DragDoor)))
            {
                // Shut in their face, or something wedged in the gap: they need
                // somewhere else to drag them.
                ChooseDragTarget(agent);
            }

            if (agent.Help.DragDoor >= 0)
            {
                int from = geometry.RoomAt(agent.Body.Position);
                agent.Intent.Target = from >= 0 &&
                                      !geometry.IsLinedUpToPassThrough(agent.Help.DragDoor, agent.Body.Position)
                    ? geometry.DoorPointFrom(agent.Help.DragDoor, from, 0, -context.Scenario.Exits.ApproachInsetMillimetres)
                    : geometry.DoorPointFrom(agent.Help.DragDoor, geometry.RoomOf(agent), 0,
                        context.Scenario.Exits.OutsideTargetMillimetres);
            }

            int heading = geometry.Routes.HeadingToward(
                agent.Body.Position, agent.Intent.Target, radius, agent.Body.Heading);
            heading = locomotion.Steer(agent, heading, 0, context.Scenario.Panic.WallAvoidPercent, 0);
            int speed = settings.DragSpeedBase + settings.DragSpeedPerStrength * agent.Traits.Strength;
            return new MotorIntent(heading, speed, agent.Personality.CalmTurnRate, context.Scenario.Panic.Acceleration);
        }

        /// <summary>Let go of whoever they were helping; a dragged person is dropped where they lie.</summary>
        public void StopHelping(Agent agent, bool logDrop)
        {
            if (agent.Intent.Activity == AgentActivityState.Dragging && logDrop && agent.Help.TargetIndex >= 0)
            {
                Agent target = crowd.All[agent.Help.TargetIndex];
                context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentDropped, target.Body.Position, 0, 0,
                    agent.Help.GrabEventId, target.Id);
            }

            if (logDrop && agent.Help.TargetIndex >= 0)
            {
                // They tried and could not manage it, so they do not keep
                // grabbing the same person and straining in the same corner.
                agent.Help.GaveUpOnIndex = agent.Help.TargetIndex;
            }

            agent.Help.TargetIndex = -1;
            agent.Help.DragDoor = -1;
            agent.Help.StuckTicks = 0;
            if (IsHelping(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Intent.NextPanicDecisionTick = context.Tick;

                // Whatever had them stuck, they start counting again from here.
                agent.Body.BlockedTicks = 0;
            }
        }

        // ---------------------------------------------------------------- moving the dragged

        /// <summary>
        /// Before the engine steps: every helper hauls the person they are
        /// dragging toward the spot just behind them. The one being dragged is
        /// a loose body on the floor, so they snag on furniture, doorframes and
        /// other people. Trailing too far behind, they hold the helper up; far
        /// too far, the helper loses their grip. Getting nowhere counts as
        /// stuck, so the give-up rule eventually lets them go instead of
        /// leaving them standing there for the rest of the run.
        /// </summary>
        public void PullDragged(Agent[] agents)
        {
            // From the helper's middle to the dragged person's: the helper, a
            // gap, then half a body lying in line behind them.
            int behind = radius + settings.DragGapMillimetres + PeopleBodies.HeightMillimetres / 2;
            long slack = behind + (long)context.Scenario.PhysicsFeel.DragSlackMillimetres;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent helper = agents[i];
                if (!helper.IsParticipating || helper.Intent.Activity != AgentActivityState.Dragging || helper.Help.TargetIndex < 0)
                {
                    continue;
                }

                if (helper.Body.State != AgentBodyState.Upright || helper.Burning.IsBurning)
                {
                    StopHelping(helper, true);
                    continue;
                }

                Agent dragged = agents[helper.Help.TargetIndex];
                long apart = IntegerMath.Sqrt(LogicalPosition.DistanceSquared(helper.Body.Position, dragged.Body.Position));
                if (apart > slack * 2L)
                {
                    // Torn out of their hands.
                    StopHelping(helper, true);
                    continue;
                }

                if (apart > slack)
                {
                    // Snagged: they lean on the dead weight and get nowhere.
                    helper.Body.Speed = 0;
                    helper.Body.BlockedTicks++;
                }

                helper.Help.StuckTicks = helper.Body.BlockedTicks > 0 ? helper.Help.StuckTicks + 1 : 0;

                // The body trails an arm's length from the helper, on whichever
                // side of them it already lies, like a weight on a rope: never
                // hauled through the helper to a spot behind their back.
                int trailing = IntegerMath.HeadingBetween(helper.Body.Position, dragged.Body.Position, helper.Body.Heading + 180);
                LogicalPosition spot = helper.Body.Position + IntegerMath.Displacement(trailing, behind);
                people.PullToward(dragged, spot, Math.Max(helper.Body.Speed, settings.DragSpeedBase));
                dragged.Body.Heading = helper.Body.Heading;
            }
        }

        /// <summary>After escapes: a helper who got out takes the person they were dragging out with them.</summary>
        public void ResolveRescues(Agent[] agents)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                Agent helper = agents[i];
                if (helper.Outcome != AgentTerminalOutcome.Escaped || helper.Intent.Activity != AgentActivityState.Dragging ||
                    helper.Help.TargetIndex < 0)
                {
                    continue;
                }

                Agent dragged = agents[helper.Help.TargetIndex];
                helper.Help.TargetIndex = -1;
                if (!dragged.IsParticipating)
                {
                    continue;
                }

                dragged.Participation = AgentParticipation.NoLongerParticipating;
                dragged.Outcome = AgentTerminalOutcome.Escaped;
                context.Events.Append(context.Tick, helper.Id, FireReactionEventType.AgentRescued, dragged.Body.Position, 0, 0,
                    helper.Doors.EscapedEventId, dragged.Id);
            }
        }
    }
}
