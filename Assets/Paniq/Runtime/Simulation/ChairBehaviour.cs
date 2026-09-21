namespace Paniq.Simulation
{
    /// <summary>
    /// Sitting down. A calm person now and then walks to a free chair, sits
    /// on it for a while, and gets up again. A chair with someone on it does
    /// not slide, cannot be picked up and cannot be tidied away. Anyone who
    /// is startled, knocked over or set alight while sitting has to get up
    /// first, which costs them a moment: the nervous are quicker out of the
    /// chair than the placid. Sitting causes nothing in the world, so it
    /// logs no events; who is on which chair is in the snapshot.
    /// </summary>
    internal sealed class ChairBehaviour
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly PhysicsObjectSystem objects;
        private readonly WorldGeometry geometry;
        private readonly Crowd crowd;
        private readonly ItemSettings settings;

        /// <summary>Everybody's physical body: sitting down holds it on the chair.</summary>
        private readonly PeopleBodies people;

        public ChairBehaviour(SimulationContext context, Crowd crowd, WorldGeometry geometry, PhysicsObjectSystem objects,
            PeopleBodies people)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.people = people;
            settings = context.Scenario.Items;
        }

        public static bool IsSitting(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.GoingToSit ||
                   activity == AgentActivityState.Sitting ||
                   activity == AgentActivityState.StandingUp;
        }

        /// <summary>
        /// Picks the nearest free chair in the room and walks over to it.
        /// False when there is none worth crossing the room for.
        /// </summary>
        public bool TryStartSitting(Agent agent)
        {
            if (agent.Carry.ItemIndex >= 0)
            {
                return false;
            }

            int room = geometry.RoomOf(agent);
            long reach = settings.SitSearchDistanceMillimetres;
            int best = -1;
            long bestDistance = reach;

            // The nearest chair they could walk to, which may be through a
            // doorway. It used to have to be in the room they were standing in,
            // because walking to one anywhere else meant walking at the wall
            // between.
            FlowField walking = geometry.Routes.ReachFrom(agent.Body.Position, bodyRadius);
            using PhysicsObjectSystem.Nearby candidates =
                objects.Gather(UniformGridIndex.Around(agent.Body.Position, reach));
            for (int c = 0; c < candidates.Count; c++)
            {
                int i = candidates[c];
                if (!objects.IsFreeChair(i))
                {
                    continue;
                }

                LogicalPosition chair = objects.PositionOf(i);
                if (walking == null)
                {
                    // No routing to spare this tick: keep to this room, which
                    // is all anybody could do before.
                    if (geometry.RoomAtPoint(chair) != room)
                    {
                        continue;
                    }
                }

                long distance = walking == null
                    ? IntegerMath.Distance(agent.Body.Position, chair)
                    : geometry.Routes.DistanceIn(walking, chair);
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

            agent.Sitting.ChairIndex = best;
            agent.Sitting.OnIt = false;
            agent.Intent.Activity = AgentActivityState.GoingToSit;
            agent.Intent.Target = objects.PositionOf(best);
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Scenario.Calm.StrollTimeoutTicks);
            return true;
        }

        /// <summary>
        /// Walking to the chair, sitting on it, or getting up again. Returns
        /// false when they are done (or gave up) and should choose something
        /// else to do.
        /// </summary>
        public bool UpdateSitting(Agent agent, out int goalHeading, out int goalSpeed)
        {
            int tick = context.Tick;
            goalHeading = agent.Body.Heading;
            goalSpeed = 0;
            int chair = agent.Sitting.ChairIndex;
            switch (agent.Intent.Activity)
            {
                case AgentActivityState.GoingToSit:
                    if (chair < 0 || (!agent.Sitting.OnIt && !objects.IsFreeChair(chair)) ||
                        tick >= agent.Intent.ActivityEndTick ||
                        agent.Body.BlockedTicks > context.Scenario.Calm.BlockedGiveUpTicks)
                    {
                        // Somebody else got there first, or the way is blocked.
                        Forget(agent);
                        return false;
                    }

                    LogicalPosition seat = objects.PositionOf(chair);
                    agent.Intent.Target = seat;
                    long gap = IntegerMath.Distance(agent.Body.Position, seat);

                    // Round what is in the way: a chair is chosen by how far it
                    // is to walk to it, so it may be through a doorway or on
                    // the other side of a desk.
                    goalHeading = geometry.Routes.HeadingToward(
                        agent.Body.Position, seat, bodyRadius, agent.Body.Heading);
                    if (gap > settings.SitArrivalDistanceMillimetres)
                    {
                        goalSpeed = agent.Personality.CalmSpeed;
                        return true;
                    }

                    // Right beside it: they settle onto it, unless someone is
                    // in the way of where they would end up.
                    if (crowd.FindBlocking(agent, agent.Body.Position, seat) != null)
                    {
                        Forget(agent);
                        return false;
                    }

                    SitDown(agent, chair);
                    return true;

                case AgentActivityState.Sitting:
                    goalHeading = agent.Intent.LookHeading;
                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        StartStandingUp(agent);
                    }

                    return true;

                default:
                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    Forget(agent);
                    return false;
            }
        }

        /// <summary>They settle onto the chair and face the way it faces.</summary>
        private void SitDown(Agent agent, int chair)
        {
            // Settling onto the chair puts them on it: the one place a body
            // moves outside the engine's step, and only by a stride.
            objects.SitOn(chair, agent);
            people.SitIn(agent, chair, objects.PositionOf(chair), agent.Body.Heading);
            agent.Body.Speed = 0;
            agent.Sitting.OnIt = true;
            agent.Intent.Activity = AgentActivityState.Sitting;

            // They turn to face the way the chair faces, not the way they
            // happened to walk up to it, so a chair pulled up to a table seats
            // somebody looking at the table. They swivel round at their usual
            // turning pace while sitting; nobody snaps round in one go.
            agent.Intent.LookHeading = objects.HeadingOf(chair);
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.SitMinimumTicks, settings.SitMaximumTicks));
            agent.Sitting.SitUntilTick = agent.Intent.ActivityEndTick;
        }

        /// <summary>
        /// They turned in their seat to look at a noise and saw nothing worth
        /// getting up for: they settle back, facing the way the chair faces,
        /// for however long they had meant to sit anyway.
        /// </summary>
        public void ResumeSitting(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Sitting;
            agent.Intent.LookHeading = objects.HeadingOf(agent.Sitting.ChairIndex);
            agent.Intent.ActivityEndTick = System.Math.Max(agent.Sitting.SitUntilTick, context.Tick + 1);
        }

        /// <summary>Getting out of the chair, which takes a moment longer if they are placid.</summary>
        public void StartStandingUp(Agent agent)
        {
            if (agent.Intent.Activity == AgentActivityState.StandingUp)
            {
                return;
            }

            agent.Intent.Activity = AgentActivityState.StandingUp;
            agent.Intent.ActivityEndTick = checked(context.Tick + TraitEffects.StandUpTicks(agent, context.Scenario));
        }

        /// <summary>
        /// Phase 4½: anyone who is on a chair but no longer sitting on
        /// purpose — knocked over, alight, or up and running — is off it, and
        /// the chair is shoved back as they go.
        /// </summary>
        public void ResolveStanding()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.Sitting.OnIt)
                {
                    continue;
                }

                bool stillSeated = agent.IsParticipating && !agent.Burning.IsBurning &&
                                   agent.Body.State == AgentBodyState.Upright &&
                                   (agent.Intent.Activity == AgentActivityState.Sitting ||
                                    agent.Intent.Activity == AgentActivityState.StandingUp ||

                                    // Startled, but still in the chair until they get out of it.
                                    agent.Intent.Activity == AgentActivityState.Reacting ||

                                    // Heard something and turned in the seat to look.
                                    agent.Intent.Activity == AgentActivityState.Investigating);
                if (!stillSeated)
                {
                    Forget(agent);
                }
            }
        }

        /// <summary>
        /// Off the chair: they step out of it, and it is shoved the other way
        /// as they go. If there is nowhere to step they stay put and the
        /// chair simply comes loose again.
        /// </summary>
        private void Forget(Agent agent)
        {
            int chair = agent.Sitting.ChairIndex;
            if (chair >= 0 && agent.Sitting.OnIt)
            {
                int away = IntegerMath.NormalizeDegrees(agent.Body.Heading + 180);
                LogicalPosition shove = IntegerMath.Displacement(away, settings.StandUpShoveSpeed);
                objects.StandUp(chair, -shove.X * PhysicsObjectSystem.SubMillimetre, -shove.Z * PhysicsObjectSystem.SubMillimetre);
                StepOutOfTheChair(agent, chair, away);
            }

            agent.Sitting.ChairIndex = -1;
            agent.Sitting.OnIt = false;
        }

        /// <summary>
        /// One step clear of the seat, so they are not standing in the chair.
        /// Somebody knocked off it takes no step: they lie where they fell, and
        /// the chair, loose again, is shoved out from under them instead.
        /// </summary>
        private void StepOutOfTheChair(Agent agent, int chair, int away)
        {
            if (agent.IsDown)
            {
                people.LeaveChair(agent, null);
                return;
            }

            int clearance = context.Scenario.World.OccupancyRadiusMillimetres + objects.RadiusOf(chair) + 50;
            for (int turn = 0; turn <= 180; turn += 45)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int heading = IntegerMath.NormalizeDegrees(away + side * turn);
                    LogicalPosition step = agent.Body.Position + IntegerMath.Displacement(heading, clearance);
                    if (geometry.RoomAt(step) < 0 || geometry.TableAt(step, context.Scenario.World.OccupancyRadiusMillimetres) >= 0 ||
                        crowd.FindBlocking(agent, agent.Body.Position, step) != null ||
                        objects.FindBlocking(agent.Body.Position, step, context.Scenario.World.OccupancyRadiusMillimetres, chair) >= 0)
                    {
                        continue;
                    }

                    people.LeaveChair(agent, step);
                    return;
                }
            }

            // Nowhere to step: they stand up where they are, and the chair
            // they were in is eased out from under them.
            people.LeaveChair(agent, null);
        }
    }
}
