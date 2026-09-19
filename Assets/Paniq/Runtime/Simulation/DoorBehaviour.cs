namespace Paniq.Simulation
{
    /// <summary>
    /// Panicked people and doors. Runners head for a door: an unlocked one
    /// they open, a locked one they rattle and maybe try to force (it never
    /// gives) before looking for another way out. People can see an open
    /// doorway, but a shut door looks the same to them locked or not; they
    /// remember a door that would not open for a while. Walking far enough
    /// out through an open door is an escape.
    /// </summary>
    internal sealed class DoorBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly FireSystem fire;
        private readonly SoundSystem sound;
        private readonly ExitSettings settings;

        public DoorBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            DoorSystem doors,
            FireSystem fire,
            SoundSystem sound)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.fire = fire;
            this.sound = sound;
            settings = context.Scenario.Exits;
        }

        /// <summary>
        /// Picks the door to run for, or -1 when every door has failed this
        /// person recently.
        /// </summary>
        public int ChooseExitDoor(Agent agent)
        {
            int best = -1;
            long bestScore = long.MinValue;
            for (int d = 0; d < doors.Count; d++)
            {
                bool open = geometry.IsDoorOpen(d);
                if (!open && context.Tick < agent.Doors.AvoidUntilTick[d])
                {
                    continue;
                }

                LogicalPosition approach = geometry.DoorPoint(d, 0, -settings.ApproachInsetMillimetres);
                long score = context.Random.NextIntInclusive(0, settings.ChoiceNoiseMillimetres) -
                             IntegerMath.Distance(agent.Body.Position, approach);
                if (open)
                {
                    score += settings.OpenBonusMillimetres;
                }

                if (d == agent.Doors.ExitDoorIndex)
                {
                    score += settings.CurrentChoiceBonusMillimetres;
                }

                if (fire.RoutePassesNear(agent.Body.Position, approach, context.Scenario.Panic.EscapeRouteClearanceMillimetres))
                {
                    score -= context.Scenario.Panic.EscapeRoutePenaltyMillimetres;
                }

                if (fire.AnyCloserThan(approach, context.Scenario.Panic.DangerDistanceMillimetres))
                {
                    score -= settings.InFirePenaltyMillimetres;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }

            return best;
        }

        /// <summary>
        /// Where to run for the chosen door: just inside it, or, once it is
        /// open and the person is lined up with the gap, out through it.
        /// </summary>
        public LogicalPosition DoorTarget(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            if (geometry.IsDoorOpen(door) &&
                (!geometry.IsInsideRoom(agent.Body.Position) || geometry.IsLinedUpToPassThrough(door, agent.Body.Position)))
            {
                return geometry.DoorPoint(door, 0, settings.OutsideTargetMillimetres);
            }

            return geometry.DoorPoint(door, 0, -settings.ApproachInsetMillimetres);
        }

        /// <summary>True while the person is close to the door they are running for.</summary>
        public bool IsNearExit(Agent agent, int distance)
        {
            if (agent.Doors.ExitDoorIndex < 0)
            {
                return false;
            }

            long limit = distance;
            return LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(agent.Doors.ExitDoorIndex)) <
                   limit * limit;
        }

        /// <summary>
        /// Heading out through an open door: close to it (or already outside),
        /// so swerves, hesitation and fresh decisions no longer matter.
        /// </summary>
        public bool IsLeaving(Agent agent)
        {
            return agent.Doors.ExitDoorIndex >= 0 &&
                   geometry.IsDoorOpen(agent.Doors.ExitDoorIndex) &&
                   (IsNearExit(agent, settings.CommitDistanceMillimetres) || !geometry.IsInsideRoom(agent.Body.Position));
        }

        /// <summary>Reached a shut door: time to try the handle.</summary>
        public bool HasReachedClosedExit(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            if (door < 0 || geometry.IsDoorOpen(door))
            {
                return false;
            }

            long arrival = settings.ArrivalDistanceMillimetres;
            long reach = settings.ApproachInsetMillimetres + settings.ArrivalDistanceMillimetres / 2;
            return LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorPoint(door, 0, -settings.ApproachInsetMillimetres)) <
                   arrival * arrival ||
                   LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door)) < reach * reach;
        }

        /// <summary>Turn to face the door being tried.</summary>
        public MotorIntent FaceDoor(Agent agent)
        {
            return new MotorIntent(
                IntegerMath.HeadingBetween(agent.Body.Position, geometry.DoorCentre(agent.Doors.ExitDoorIndex), agent.Body.Heading),
                0,
                agent.Personality.PanicTurnRate,
                context.Scenario.Panic.Acceleration);
        }

        public void StartAttempt(Agent agent)
        {
            int door = agent.Doors.ExitDoorIndex;
            agent.Body.BlockedTicks = 0;
            agent.Doors.AttemptEventId = context.Events.Append(
                context.Tick,
                agent.Id,
                FireReactionEventType.AgentTriedDoor,
                geometry.DoorCentre(door),
                0,
                0,
                agent.Fear.ScaredEventId,
                doors.IdOf(door)).EventId;
            if (doors.StateOf(door) == DoorState.Unlocked)
            {
                agent.Intent.Activity = AgentActivityState.OpeningDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + settings.DoorOpenTicks);
            }
            else
            {
                agent.Intent.Activity = AgentActivityState.TryingDoor;
                agent.Intent.ActivityEndTick = checked(context.Tick + settings.DoorTryTicks);
            }
        }

        public static bool IsAtDoor(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.OpeningDoor ||
                   activity == AgentActivityState.TryingDoor ||
                   activity == AgentActivityState.ForcingDoor;
        }

        /// <summary>
        /// Opening, rattling or forcing a door. Returns true while the person
        /// is still busy at the door this tick, in which case they keep facing
        /// it; otherwise the running behaviour decides how they move.
        /// </summary>
        public bool UpdateAttempt(Agent agent, bool inDanger)
        {
            int tick = context.Tick;
            int door = agent.Doors.ExitDoorIndex;
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            DoorState state = doors.StateOf(door);
            if (state == DoorState.Open)
            {
                // Someone else got it open: go.
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            if (inDanger)
            {
                // The fire is too close to stand here: run for it.
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Doors.AvoidUntilTick[door] = checked(tick + context.Random.NextIntInclusive(
                    settings.DoorCrowdedAvoidMinimumTicks, settings.DoorCrowdedAvoidMaximumTicks));
                agent.Doors.ExitDoorIndex = -1;
                agent.Intent.NextPanicDecisionTick = tick;
                return false;
            }

            if (state == DoorState.Unlocked && agent.Intent.Activity != AgentActivityState.OpeningDoor)
            {
                // Unlocked while they were rattling it: it opens at once.
                doors.Open(door, agent.Doors.AttemptEventId);
                agent.Intent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            switch (agent.Intent.Activity)
            {
                case AgentActivityState.OpeningDoor:
                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        doors.Open(door, agent.Doors.AttemptEventId);
                        agent.Intent.Activity = AgentActivityState.Fleeing;
                        return false;
                    }

                    return true;

                case AgentActivityState.TryingDoor:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    if (context.Random.NextPercent(settings.DoorForceChancePercent))
                    {
                        agent.Intent.Activity = AgentActivityState.ForcingDoor;
                        agent.Intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(
                            settings.DoorForceMinimumTicks, settings.DoorForceMaximumTicks));
                        agent.Doors.NextShoveTick = tick + 1;
                    }
                    else
                    {
                        GiveUp(agent);
                    }

                    return true;

                default:
                    if (tick >= agent.Doors.NextShoveTick)
                    {
                        // A shoulder into the door: a thud, and nothing gives.
                        CausalEvent shove = context.Events.Append(
                            tick,
                            agent.Id,
                            FireReactionEventType.AgentForcedDoor,
                            doorCentre,
                            context.Scenario.Hearing.BumpSoundRadiusMillimetres,
                            0,
                            agent.Doors.AttemptEventId,
                            doors.IdOf(door));
                        sound.Thud(agent.Id, doorCentre, shove.EventId);
                        agent.Doors.NextShoveTick = checked(tick + context.Random.NextIntInclusive(
                            settings.DoorShoveMinimumTicks, settings.DoorShoveMaximumTicks));
                    }

                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        GiveUp(agent);
                    }

                    return true;
            }
        }

        /// <summary>
        /// This door will not open: remember that for a while, glance toward
        /// the next way out, then run for it.
        /// </summary>
        private void GiveUp(Agent agent)
        {
            int tick = context.Tick;
            int door = agent.Doors.ExitDoorIndex;
            context.Events.Append(tick, agent.Id, FireReactionEventType.AgentGaveUpOnDoor, geometry.DoorCentre(door),
                0, 0, agent.Doors.AttemptEventId, doors.IdOf(door));
            agent.Doors.AvoidUntilTick[door] = checked(tick + context.Random.NextIntInclusive(
                settings.DoorAvoidMinimumTicks, settings.DoorAvoidMaximumTicks));
            agent.Doors.ExitDoorIndex = -1;

            int next = ChooseExitDoor(agent);
            agent.Intent.Activity = AgentActivityState.Hesitating;
            agent.Intent.ActivityEndTick = checked(tick + context.Random.NextIntInclusive(
                settings.GiveUpGlanceMinimumTicks, settings.GiveUpGlanceMaximumTicks));
            if (next >= 0)
            {
                agent.Intent.LookHeading = IntegerMath.HeadingBetween(agent.Body.Position,
                    geometry.DoorPoint(next, 0, -settings.ApproachInsetMillimetres), agent.Body.Heading);
            }
            else
            {
                int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                agent.Intent.LookHeading = IntegerMath.NormalizeDegrees(agent.Body.Heading + side * context.Random.NextIntInclusive(90, 150));
            }
        }

        /// <summary>Stuck in the crowd on the way to a door: try another for a little while.</summary>
        public void AvoidCrowdedExit(Agent agent)
        {
            if (agent.Doors.ExitDoorIndex < 0)
            {
                return;
            }

            agent.Doors.AvoidUntilTick[agent.Doors.ExitDoorIndex] = checked(context.Tick + context.Random.NextIntInclusive(
                settings.DoorCrowdedAvoidMinimumTicks, settings.DoorCrowdedAvoidMaximumTicks));
        }

        /// <summary>Phase 6: anyone far enough out through an open door has escaped and leaves the run.</summary>
        public void ResolveEscapes()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                int door = geometry.EscapedThrough(agent.Body.Position);
                if (door < 0)
                {
                    continue;
                }

                agent.Participation = AgentParticipation.NoLongerParticipating;
                agent.Outcome = AgentTerminalOutcome.Escaped;
                context.Events.Append(context.Tick, agent.Id, FireReactionEventType.AgentEscaped, agent.Body.Position,
                    0, 0, doors.OpenedEventIdOf(door), doors.IdOf(door));
            }
        }
    }
}
