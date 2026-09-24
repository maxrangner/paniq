using System;

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
                    // A chair they already have hold of is theirs, moving or
                    // not; only one they have not reached yet can be taken by
                    // somebody else first.
                    bool theirs = agent.Sitting.OnIt || agent.Sitting.Phase != SitPhase.None;
                    if (chair < 0 || (!theirs && !objects.IsFreeChair(chair)) ||
                        tick >= agent.Intent.ActivityEndTick ||
                        (!theirs && agent.Body.BlockedTicks > context.Scenario.Calm.BlockedGiveUpTicks))
                    {
                        // Somebody else got there first, or the way is blocked.
                        Forget(agent);
                        return false;
                    }

                    if (agent.Sitting.Phase != SitPhase.None)
                    {
                        // Pulling the chair out, lowering onto it, or riding it in.
                        return UpdateSittingDown(agent, chair, out goalHeading);
                    }

                    LogicalPosition standBy = PullOutSpot(agent, chair);
                    agent.Intent.Target = standBy;
                    long gap = IntegerMath.Distance(agent.Body.Position, standBy);

                    // Round what is in the way: a chair is chosen by how far it
                    // is to walk to it, so it may be through a doorway or on
                    // the other side of a desk.
                    goalHeading = geometry.Routes.HeadingToward(
                        agent.Body.Position, standBy, bodyRadius, agent.Body.Heading);
                    if (gap > settings.SitArrivalDistanceMillimetres)
                    {
                        goalSpeed = agent.Personality.CalmSpeed;
                        return true;
                    }

                    // Beside it: they take hold of it and pull it out, unless
                    // somebody is in the way of where they would end up.
                    if (crowd.FindBlocking(agent, agent.Body.Position, objects.PositionOf(chair)) != null)
                    {
                        Forget(agent);
                        return false;
                    }

                    agent.Sitting.Phase = SitPhase.PullingOut;
                    agent.Sitting.PulledOutMillimetres = 0;
                    agent.Sitting.ChairStart = objects.PositionOf(chair);
                    agent.Sitting.PhaseStartTick = tick;
                    agent.Intent.ActivityEndTick = checked(tick + settings.SitPullTicks * 3);
                    objects.PullAlong(chair, IntegerMath.NormalizeDegrees(objects.HeadingOf(chair) + 180), PullStep);
                    goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, objects.PositionOf(chair),
                        agent.Body.Heading);
                    return true;

                case AgentActivityState.Sitting:
                    goalHeading = agent.Intent.LookHeading;
                    if (tick >= agent.Intent.ActivityEndTick)
                    {
                        StartStandingUp(agent);
                    }

                    return true;

                default:
                    if (agent.Sitting.Phase == SitPhase.ScootingOut || agent.Sitting.Phase == SitPhase.Rising)
                    {
                        return UpdateStandingUp(agent, chair, out goalHeading);
                    }

                    if (tick < agent.Intent.ActivityEndTick)
                    {
                        return true;
                    }

                    Forget(agent);
                    return false;
            }
        }

        /// <summary>
        /// Where somebody stands to pull a chair out: off to one side of the
        /// way it faces, behind it, so the chair comes past them rather than
        /// into them. They stand on whichever side they are already nearer.
        /// </summary>
        private LogicalPosition PullOutSpot(Agent agent, int chair)
        {
            LogicalPosition seat = objects.PositionOf(chair);
            int facing = objects.HeadingOf(chair);
            int toThem = IntegerMath.HeadingBetween(seat, agent.Body.Position, facing);
            int side = IntegerMath.NormalizeDegrees(toThem - facing) < 180 ? -1 : 1;
            // Well round to the side and clear of it: the chair comes back
            // past them without knocking into them, which would only startle
            // them into wondering what the noise was.
            int away = IntegerMath.NormalizeDegrees(facing + 180 + side * 75);
            return seat + IntegerMath.Displacement(away, objects.RadiusOf(chair) + bodyRadius + 150);
        }

        /// <summary>How far the chair moves in one tick while being pulled out or tucked in.</summary>
        private int PullStep => Math.Max(1, settings.SitPullOutMillimetres / settings.SitPullTicks);

        /// <summary>
        /// Pulling the chair out, lowering onto the seat, and riding the chair
        /// back in: the three parts of sitting down, one tick at a time.
        /// </summary>
        private bool UpdateSittingDown(Agent agent, int chair, out int goalHeading)
        {
            int tick = context.Tick;
            LogicalPosition seat = objects.PositionOf(chair);
            int facing = objects.HeadingOf(chair);
            goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, seat, agent.Body.Heading);
            switch (agent.Sitting.Phase)
            {
                case SitPhase.PullingOut:
                    // Followed rather than counted: the chair is being pulled by
                    // the engine, and may come up short against something behind
                    // it. How far it actually came is how far it goes back in.
                    int pulled = (int)IntegerMath.Distance(agent.Sitting.ChairStart, seat);
                    if (pulled < settings.SitPullOutMillimetres && tick < agent.Intent.ActivityEndTick)
                    {
                        agent.Sitting.PulledOutMillimetres = pulled;
                        objects.PullAlong(chair, IntegerMath.NormalizeDegrees(facing + 180), PullStep);
                        return true;
                    }

                    agent.Sitting.PulledOutMillimetres = Math.Min(pulled, settings.SitPullOutMillimetres);
                    objects.StopPulling(chair);

                    // Out far enough, or it will not come any further: they
                    // lower themselves onto it from where they stand.
                    agent.Sitting.MoveFrom = agent.Body.Position;
                    agent.Sitting.PhaseStartTick = tick;
                    agent.Sitting.Phase = SitPhase.Lowering;
                    agent.Sitting.OnIt = true;

                    // The first step of the way down, so the display's lift
                    // onto the seat is spread as evenly as the move itself.
                    agent.Sitting.SeatedPercent = Percent(1, settings.SitLowerTicks);
                    agent.Intent.LookHeading = facing;
                    agent.Intent.ActivityEndTick = checked(tick + settings.SitLowerTicks + settings.SitPullTicks * 2);
                    agent.Body.Speed = 0;
                    people.SitIn(agent, chair, agent.Body.Position, agent.Body.Heading);
                    return true;

                default:
                    // Lowering onto the seat while the chair slides back in
                    // under the table: they ride it down and in, in one move.
                    // The chair is still loose, so it stops against whatever is
                    // behind the table and takes them with it.
                    int lowered = tick - agent.Sitting.PhaseStartTick;
                    int backIn = (int)IntegerMath.Distance(agent.Sitting.ChairStart, seat);
                    if (lowered < settings.SitLowerTicks || backIn > PullStep)
                    {
                        // Tucked in until their knees are at the table, not until
                        // the chair is against it: the person on it is wider than
                        // the chair, and nobody sits inside a desk.
                        LogicalPosition ahead = seat + IntegerMath.Displacement(facing, PullStep);
                        bool roomToTuck = geometry.TableAt(ahead, bodyRadius) < 0;
                        if (backIn > PullStep && roomToTuck && tick < agent.Intent.ActivityEndTick)
                        {
                            objects.PullAlong(chair, facing, PullStep);
                        }
                        else
                        {
                            objects.StopPulling(chair);
                        }

                        people.MoveSeated(agent, Part(agent.Sitting.MoveFrom, seat, lowered, settings.SitLowerTicks),
                            agent.Body.Heading);
                        agent.Sitting.SeatedPercent = Percent(lowered + 1, settings.SitLowerTicks);
                        return tick < agent.Intent.ActivityEndTick || SettleIntoTheChair(agent, chair);
                    }

                    // Down on the seat and back at the table: now it is theirs.
                    objects.StopPulling(chair);
                    return SettleIntoTheChair(agent, chair);
            }
        }

        /// <summary>
        /// Backing the chair out and rising from it: getting up, one tick at a
        /// time, for somebody doing it on purpose rather than leaping clear.
        /// </summary>
        private bool UpdateStandingUp(Agent agent, int chair, out int goalHeading)
        {
            int tick = context.Tick;
            goalHeading = agent.Intent.LookHeading;
            if (chair < 0 || !agent.Sitting.OnIt)
            {
                Forget(agent);
                return false;
            }

            int facing = objects.HeadingOf(chair);
            if (agent.Sitting.Phase == SitPhase.ScootingOut)
            {
                // Letting go of it is the first thing they do: it has to be
                // loose to roll back with them on it.
                objects.StandUp(chair, 0, 0);
                int backedOut = (int)IntegerMath.Distance(agent.Sitting.ChairStart, objects.PositionOf(chair));
                if (backedOut < settings.SitPullOutMillimetres && tick < agent.Intent.ActivityEndTick)
                {
                    objects.PullAlong(chair, IntegerMath.NormalizeDegrees(facing + 180), PullStep);
                    people.MoveSeated(agent, objects.PositionOf(chair), agent.Body.Heading);
                    return true;
                }

                objects.StopPulling(chair);
                agent.Sitting.MoveFrom = agent.Body.Position;

                // Chosen once, here, from the seat. It used to be worked out
                // afresh every tick from wherever they had got to, a step
                // further on each time, so the spot ran away from them: they
                // slid the better part of two metres backwards, faster and
                // faster, and were then put half a metre further on in the
                // tick they stood -- six people at a meeting table, all at
                // once, straight into the walls.
                agent.Sitting.StepTo = ChooseStepOutSpot(agent, chair);
                agent.Sitting.PhaseStartTick = tick;
                agent.Sitting.Phase = SitPhase.Rising;
                agent.Sitting.SeatedPercent = 100 - Percent(1, settings.SitLowerTicks);
                return true;
            }

            int risen = tick - agent.Sitting.PhaseStartTick;
            LogicalPosition stepTo = agent.Sitting.StepTo;
            if (risen < settings.SitLowerTicks)
            {
                people.MoveSeated(agent, Part(agent.Sitting.MoveFrom, stepTo, risen, settings.SitLowerTicks),
                    agent.Body.Heading);
                agent.Sitting.SeatedPercent = 100 - Percent(risen + 1, settings.SitLowerTicks);
                return true;
            }

            // The last part of the move took them to the spot already, so
            // standing up leaves them exactly where they are.
            objects.StandUp(chair, 0, 0);
            people.LeaveChair(agent, stepTo);
            agent.Sitting.ChairIndex = -1;
            agent.Sitting.OnIt = false;
            agent.Sitting.Phase = SitPhase.None;
            agent.Sitting.PulledOutMillimetres = 0;
            agent.Sitting.SeatedPercent = 0;
            return false;
        }

        /// <summary>
        /// The whole of a move that is <paramref name="step"/> of <paramref name="steps"/> along, as a percentage.
        /// </summary>
        private static int Percent(int step, int steps)
        {
            return Math.Min(100, Math.Max(0, step * 100 / Math.Max(1, steps)));
        }

        /// <summary>
        /// Where somebody rising from a chair steps to, chosen once as they
        /// start to rise: beside the chair, the way they stood to pull it out,
        /// left first and then right; failing that, anywhere a step clear of
        /// it; failing that, where they sit.
        /// </summary>
        private LogicalPosition ChooseStepOutSpot(Agent agent, int chair)
        {
            LogicalPosition seat = objects.PositionOf(chair);
            int facing = objects.HeadingOf(chair);
            int away = IntegerMath.NormalizeDegrees(facing + 180);
            int beside = objects.RadiusOf(chair) + bodyRadius + 150;
            for (int side = -1; side <= 1; side += 2)
            {
                LogicalPosition step = seat + IntegerMath.Displacement(IntegerMath.NormalizeDegrees(away + side * 75), beside);
                if (IsClearToStepTo(agent, chair, step))
                {
                    return step;
                }
            }

            return StepOutSpot(agent, chair, away);
        }

        /// <summary>Floor, not table, and nobody and nothing (bar their own chair) in the way of it.</summary>
        private bool IsClearToStepTo(Agent agent, int chair, LogicalPosition step)
        {
            return geometry.RoomAt(step) >= 0 &&
                   geometry.TableAt(step, bodyRadius) < 0 &&
                   crowd.FindBlocking(agent, agent.Body.Position, step) == null &&
                   objects.FindBlocking(agent.Body.Position, step, bodyRadius, chair) < 0;
        }

        /// <summary>Part of the way from one spot to another: <paramref name="step"/> of <paramref name="steps"/>.</summary>
        private static LogicalPosition Part(LogicalPosition from, LogicalPosition to, int step, int steps)
        {
            int taken = Math.Min(steps, step + 1);
            return new LogicalPosition(
                from.X + (to.X - from.X) * taken / steps,
                from.Z + (to.Z - from.Z) * taken / steps);
        }

        /// <summary>
        /// Settled: they are on the seat, the chair is back where it was, and
        /// they are facing whatever the chair faces.
        /// </summary>
        private bool SettleIntoTheChair(Agent agent, int chair)
        {
            objects.SitOn(chair, agent);
            people.MoveSeated(agent, objects.PositionOf(chair), agent.Body.Heading);
            agent.Body.Speed = 0;
            agent.Sitting.Phase = SitPhase.None;
            agent.Sitting.PulledOutMillimetres = 0;
            agent.Sitting.SeatedPercent = 100;
            agent.Intent.Activity = AgentActivityState.Sitting;

            // They turn to face the way the chair faces, not the way they
            // happened to walk up to it, so a chair pulled up to a table seats
            // somebody looking at the table. They swivel round at their usual
            // turning pace while sitting; nobody snaps round in one go.
            agent.Intent.LookHeading = objects.HeadingOf(chair);
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.SitMinimumTicks, settings.SitMaximumTicks));
            agent.Sitting.SitUntilTick = agent.Intent.ActivityEndTick;
            return true;
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

        /// <summary>
        /// Frightened out of a chair. They come up out of it <em>where they
        /// sat</em>: the old path shoved them the better part of a metre
        /// straight backwards in a single tick, which drew as everyone at the
        /// meeting table gliding backwards into the walls without ever turning
        /// round. Getting up still costs them the moment it always did, and the
        /// chair still goes over behind them when they finally leave it.
        /// </summary>
        public void StartLeapingUp(Agent agent)
        {
            if (agent.Sitting.Phase == SitPhase.LeapingUp)
            {
                return;
            }

            if (agent.Sitting.ChairIndex >= 0 && !agent.Sitting.OnIt && agent.Sitting.Phase != SitPhase.None)
            {
                // Caught halfway into the seat: they let go of the chair.
                objects.StopPulling(agent.Sitting.ChairIndex);
            }

            agent.Intent.Activity = AgentActivityState.StandingUp;
            agent.Intent.ActivityEndTick = checked(context.Tick + TraitEffects.StandUpTicks(agent, context.Scenario));
            agent.Sitting.Phase = SitPhase.LeapingUp;
            agent.Sitting.PhaseStartTick = context.Tick;
            agent.Sitting.MoveFrom = agent.Body.Position;
            agent.Sitting.PulledOutMillimetres = 0;
            agent.Sitting.RisingFromPercent = agent.Sitting.SeatedPercent;
        }

        /// <summary>
        /// Coming up out of the seat in a fright, one tick at a time. They are
        /// held on the spot the whole way up, and the chair is theirs until the
        /// moment they are on their feet -- getting out of it is still a moment
        /// spent rather than something that happens between two ticks. False
        /// once they are up and free to run.
        /// </summary>
        public bool UpdateLeapingUp(Agent agent)
        {
            if (context.Tick < agent.Intent.ActivityEndTick)
            {
                // Coming up out of the seat over the whole of the moment it
                // costs them, from however far down they were when it began.
                int whole = agent.Intent.ActivityEndTick - agent.Sitting.PhaseStartTick;
                int gone = context.Tick - agent.Sitting.PhaseStartTick + 1;
                agent.Sitting.SeatedPercent = agent.Sitting.RisingFromPercent * Math.Max(0, whole - gone) / Math.Max(1, whole);
                return true;
            }

            // Up: the chair goes over behind them and they are left standing
            // where they sat.
            Forget(agent);
            return false;
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
            if (agent.Sitting.OnIt)
            {
                // Back the chair out first, then rise from it: nobody rises
                // through a table.
                agent.Sitting.Phase = SitPhase.ScootingOut;
                agent.Sitting.PulledOutMillimetres = 0;
                agent.Sitting.PhaseStartTick = context.Tick;
                agent.Sitting.ChairStart = objects.PositionOf(agent.Sitting.ChairIndex);
            }
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

                                    // Lowering themselves onto the seat: held by
                                    // the chair already, though not on it yet.
                                    agent.Intent.Activity == AgentActivityState.GoingToSit ||

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
        /// Off the chair, because something knocked them out of it or set them
        /// alight. The chair goes over behind them and they are left standing
        /// where they sat: they used to be shoved the better part of a metre
        /// backwards in a single tick, which drew as a body gliding backwards
        /// without ever turning round.
        /// </summary>
        private void Forget(Agent agent)
        {
            int chair = agent.Sitting.ChairIndex;
            if (chair >= 0 && !agent.Sitting.OnIt && agent.Sitting.Phase != SitPhase.None)
            {
                // Interrupted while pulling it out: they let go of it.
                objects.StopPulling(chair);
            }

            if (chair >= 0 && agent.Sitting.OnIt)
            {
                int away = IntegerMath.NormalizeDegrees(objects.HeadingOf(chair) + 180);

                // Out of it in a hurry, because something frightened them or
                // knocked them out of it: the chair goes over backwards behind
                // them rather than being tucked politely away. They stay on the
                // spot, and the two pass through each other until the chair has
                // slid clear.
                objects.StandUp(chair, 0, 0);
                objects.KnockOver(chair, away, settings.JumpUpKnockOverSpeed, agent.Fear.ScaredEventId);
                people.LeaveChair(agent, null);
            }

            agent.Sitting.ChairIndex = -1;
            agent.Sitting.OnIt = false;
            agent.Sitting.Phase = SitPhase.None;
            agent.Sitting.PulledOutMillimetres = 0;
            agent.Sitting.SeatedPercent = 0;
        }

        /// <summary>
        /// The spot one step clear of where they sit, looked for in a fixed
        /// order so a replay picks the same one: straight back first, then
        /// further and further round. Their own spot when nowhere is clear.
        /// Asked once, as they start to rise; never while they are moving.
        /// </summary>
        private LogicalPosition StepOutSpot(Agent agent, int chair, int away)
        {
            int clearance = bodyRadius + objects.RadiusOf(chair) + 50;
            for (int turn = 0; turn <= 180; turn += 45)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int heading = IntegerMath.NormalizeDegrees(away + side * turn);
                    LogicalPosition step = agent.Body.Position + IntegerMath.Displacement(heading, clearance);
                    if (IsClearToStepTo(agent, chair, step))
                    {
                        return step;
                    }
                }
            }

            return agent.Body.Position;
        }
    }
}
