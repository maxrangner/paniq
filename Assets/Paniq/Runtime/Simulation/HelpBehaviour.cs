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
    internal sealed class HelpBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly FearSystem fear;
        private readonly BodySystem body;
        private readonly PhysicsObjectSystem objects;
        private readonly Locomotion locomotion;
        private readonly HelpSettings settings;
        private readonly int radius;

        public HelpBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            FireSystem fire,
            FearSystem fear,
            BodySystem body,
            PhysicsObjectSystem objects,
            Locomotion locomotion)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.fire = fire;
            this.fear = fear;
            this.body = body;
            this.objects = objects;
            this.locomotion = locomotion;
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
        public MotorIntent? Decide(Agent agent, bool inDanger)
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
            Agent[] agents = crowd.All;
            int best = -1;
            long bestDistance = long.MaxValue;
            bool bestIsShake = false;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                if (other == agent || !other.IsParticipating || other.Burning.IsBurning || i == agent.Help.GaveUpOnIndex ||
                    IsTargeted(i) || fire.AnyCloserThan(other.Body.Position, danger))
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
            agent.Help.WorkEndTick = 0;
            agent.Help.GiveUpTick = checked(context.Tick + settings.ReachTimeoutTicks);
            agent.Intent.Activity = bestIsShake ? AgentActivityState.ShakingAwake : AgentActivityState.Grabbing;
            agent.Doors.ExitDoorIndex = -1;
            return true;
        }

        private bool IsTargeted(int index)
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && IsHelping(agents[i]) && agents[i].Help.TargetIndex == index)
                {
                    return true;
                }
            }

            return false;
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

            int toTarget = IntegerMath.HeadingBetween(agent.Body.Position, target.Body.Position, agent.Body.Heading);
            long reach = radius * 2L + settings.ReachMillimetres;
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

        // ---------------------------------------------------------------- dragging

        /// <summary>Toward the nearest open door (they will take the person out with them), or else away from the fire.</summary>
        private void ChooseDragTarget(Agent agent)
        {
            int best = -1;
            long bestDistance = long.MaxValue;
            for (int d = 0; d < geometry.DoorCount; d++)
            {
                if (!geometry.IsDoorOpen(d) || !geometry.DoorLeadsOutside(d))
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(d));
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
            if (fire.NearestDistanceSquared(agent.Body.Position, out LogicalPosition flames) == long.MaxValue)
            {
                agent.Intent.Target = agent.Body.Position;
                return;
            }

            int away = IntegerMath.HeadingBetween(flames, agent.Body.Position, agent.Body.Heading);
            agent.Intent.Target = geometry.ClampIntoWalkable(agent.Body.Position, -1,
                agent.Body.Position + IntegerMath.Displacement(away, settings.DragAwayDistanceMillimetres));
        }

        private MotorIntent? Drag(Agent agent)
        {
            Agent target = crowd.All[agent.Help.TargetIndex];
            if (!target.IsParticipating || target.Body.State != AgentBodyState.Unconscious ||
                agent.Body.BlockedTicks > settings.DragGiveUpBlockedTicks)
            {
                StopHelping(agent, true);
                return null;
            }

            // A door they were making for was shut: pick again.
            if (agent.Help.DragDoor >= 0 && !geometry.IsDoorOpen(agent.Help.DragDoor))
            {
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

            int heading = IntegerMath.HeadingBetween(agent.Body.Position, agent.Intent.Target, agent.Body.Heading);
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

            agent.Help.TargetIndex = -1;
            agent.Help.DragDoor = -1;
            if (IsHelping(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Intent.NextPanicDecisionTick = context.Tick;
            }
        }

        // ---------------------------------------------------------------- moving the dragged

        /// <summary>Before movement: remember where every dragger stands, in case the person behind them cannot follow.</summary>
        public void BeginTick(Agent[] agents)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                agents[i].Help.PositionBeforeMove = agents[i].Body.Position;
            }
        }

        /// <summary>
        /// After movement: each dragged person is pulled along behind their
        /// helper. If there is no room for them there, the helper's step is
        /// undone. Then a helper who is no longer able to drag lets go.
        /// </summary>
        public void MoveDragged(Agent[] agents)
        {
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
                if (helper.Body.Position.Equals(helper.Help.PositionBeforeMove))
                {
                    continue;
                }

                LogicalPosition spot = helper.Body.Position +
                                       IntegerMath.Displacement(helper.Body.Heading + 180, radius * 2 + settings.DragGapMillimetres);
                if (!CanLieAt(dragged, helper, spot))
                {
                    if (IsClearFor(helper, dragged, helper.Help.PositionBeforeMove))
                    {
                        // No room behind them: the step is undone and they try again.
                        helper.Body.Position = helper.Help.PositionBeforeMove;
                        helper.Body.Speed = 0;
                        helper.Body.BlockedTicks++;
                    }
                    else
                    {
                        // Someone has already stepped where they stood: they lose their grip.
                        StopHelping(helper, true);
                    }

                    continue;
                }

                dragged.Body.Position = spot;
                dragged.Body.Heading = helper.Body.Heading;
                if (fire.Active)
                {
                    ulong cell = fire.FindTouching(spot);
                    if (cell != 0UL)
                    {
                        // Dragged into the flames.
                        body.CatchFire(dragged, cell);
                    }
                }
            }
        }

        private bool CanLieAt(Agent dragged, Agent helper, LogicalPosition spot)
        {
            if (!geometry.IsWalkable(dragged.Body.Position, helper.Doors.ExitDoorIndex, spot) &&
                !geometry.IsWalkable(helper.Body.Position, helper.Doors.ExitDoorIndex, spot))
            {
                return false;
            }

            return IsClearFor(dragged, helper, spot) && objects.FindBlocking(spot, spot, radius) < 0;
        }

        /// <summary>Nobody but <paramref name="self"/> and <paramref name="partner"/> within touching distance of the spot.</summary>
        private bool IsClearFor(Agent self, Agent partner, LogicalPosition spot)
        {
            long touching = radius * 2L;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent other = agents[i];
                if (other != self && other != partner && other.IsParticipating &&
                    LogicalPosition.DistanceSquared(other.Body.Position, spot) < touching * touching)
                {
                    return false;
                }
            }

            return true;
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
