namespace Paniq.Simulation
{
    /// <summary>
    /// Fear state changes: calm to alert, alert to scared, freezing and
    /// snapping out of it -- and, since prototype 3's second batch
    /// (2026-09-26), calming down again (<see cref="Settle"/>). Each change is
    /// logged with its cause. Perception, sound and collisions call it when
    /// something frightens a person; calming down it watches for itself.
    /// </summary>
    internal sealed class FearSystem : IBindable
    {
        private readonly SimulationContext context;
        private readonly Threats threats;

        /// <summary>Built after this system: where people are, where their desks are, and the errand that sends them back to one.</summary>
        private WorldGeometry geometry;
        private PhysicsObjectSystem objects;
        private CueSystem cues;

        public FearSystem(SimulationContext context, Threats threats)
        {
            this.context = context;
            this.threats = threats;
        }

        public void Bind(Systems systems)
        {
            geometry = systems.Geometry;
            objects = systems.Objects;
            cues = systems.Cues;
        }

        /// <summary>
        /// Temperaments are dealt like a deck rather than rolled one by one,
        /// so every room gets the authored mix (10 people: 2 freeze for good,
        /// 3 freeze for a while, 5 run). The most fearful people (nervousness
        /// minus bravery) get the freezing cards first; the seed only decides
        /// between people who are equally fearful.
        /// </summary>
        public void DealTemperaments(Agent[] agents)
        {
            TemperamentSettings settings = context.Scenario.Temperament;
            int count = agents.Length;
            int freezeForever = (count * settings.FreezeForeverPercent + 50) / 100;
            int freezeThenRun = System.Math.Min(count - freezeForever, (count * settings.FreezeThenRunPercent + 50) / 100);

            // A seeded shuffle breaks ties, then a stable sort puts the most fearful first.
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = context.Random.NextIntInclusive(0, i);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var shuffledPosition = new int[count];
            for (int i = 0; i < count; i++)
            {
                shuffledPosition[order[i]] = i;
            }

            System.Array.Sort(order, (left, right) =>
            {
                int byFear = TraitEffects.Fearfulness(agents[right].Traits).CompareTo(TraitEffects.Fearfulness(agents[left].Traits));
                return byFear != 0 ? byFear : shuffledPosition[left].CompareTo(shuffledPosition[right]);
            });

            for (int i = 0; i < count; i++)
            {
                agents[order[i]].Personality.Temperament = i < freezeForever ? AgentPanicTemperament.FreezeForever
                    : i < freezeForever + freezeThenRun ? AgentPanicTemperament.FreezeThenRun
                    : AgentPanicTemperament.Runner;
            }
        }

        /// <summary>The ticks on which somebody is due to finish being startled, so nobody else is given one of them.</summary>
        private readonly System.Collections.Generic.HashSet<int> reactionEndsTaken = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// A reaction delay nobody else has: if somebody is already due to
        /// finish being startled on that tick, this one is put off by the
        /// startle stagger, and again until the tick is theirs alone. Six
        /// people a bell reaches on the same tick come up out of their chairs
        /// one after another, a few ticks apart, never all at once -- the
        /// owner's rule that nothing happens to a whole group on one tick.
        /// Processing order decides who waits, so a replay agrees, and no
        /// random number is drawn.
        /// </summary>
        private int Staggered(int tick, int delay) => Staggered(reactionEndsTaken, tick, delay);

        /// <summary>
        /// The same stagger against any set of ticks already taken: startles
        /// keep one set, settling down another, so a startle and a settle may
        /// share a tick but no two of either do.
        /// </summary>
        private int Staggered(System.Collections.Generic.HashSet<int> taken, int tick, int delay)
        {
            taken.RemoveWhere(end => end < tick);
            int stagger = context.Scenario.Perception.StartleStaggerTicks;
            int due = checked(tick + delay);
            while (!taken.Add(due))
            {
                due = checked(due + stagger);
            }

            return due - tick;
        }

        /// <summary>Startled: stop, draw a seeded reaction delay, and log what caused it.</summary>
        public ulong StartAlert(Agent agent, ulong causalParentEventId, AgentAlertSource alertSource)
        {
            int tick = context.Tick;
            agent.Fear.State = AgentFearState.Alert;
            agent.Fear.AlertSource = alertSource;
            agent.Fear.SawTheThreat = alertSource == AgentAlertSource.Visual;
            agent.Intent.Activity = AgentActivityState.Reacting;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Hearing.HasSoundPoint = false;
            agent.Hearing.ClearPending();

            // Whatever the day had them doing is over: fear has its own rules.
            agent.Errand.Clear();
            // Their own lag before anything at all, then their own seeded
            // reaction delay on top, then a tick nobody else finishes on.
            agent.Fear.ReactionDelayTicks = Staggered(tick, context.ReactionLag() + context.Random.NextIntInclusive(0,
                TraitEffects.MaximumReactionDelayTicks(agent, context.Scenario)));
            agent.Fear.ReactionEndTick = checked(tick + agent.Fear.ReactionDelayTicks);
            CausalEvent alert = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentAlerted,
                agent.Body.Position,
                threats.Count,
                agent.Fear.ReactionDelayTicks,
                causalParentEventId);
            agent.Fear.AlertEventId = alert.EventId;
            return alert.EventId;
        }

        /// <summary>Startled by something they heard or felt at <paramref name="from"/>; they will turn toward it.</summary>
        public void Alarm(Agent agent, ulong causalParentEventId, AgentAlertSource alertSource, LogicalPosition from)
        {
            // A bell tells you there is a fire without showing you one, and
            // it frightens you just the same: the owner's rule (2026-09-25),
            // "pull it, and everybody panics". The level-headed used to walk
            // out at a stroll instead.
            StartAlert(agent, causalParentEventId, alertSource);
            agent.Hearing.SoundPoint = from;
            agent.Hearing.HasSoundPoint = true;
        }

        /// <summary>An alerted person now sees the danger for themselves; <paramref name="rootEventId"/> is what started the threat they saw.</summary>
        public void PromoteAlertToVisual(Agent agent, ulong rootEventId)
        {
            agent.Fear.AlertSource = AgentAlertSource.Visual;
            agent.Fear.SawTheThreat = true;
            CausalEvent alert = context.Events.Append(
                context.Tick,
                agent.Id,
                CausalEventType.AgentAlerted,
                agent.Body.Position,
                threats.Count,
                agent.Fear.ReactionDelayTicks,
                rootEventId);
            agent.Fear.AlertEventId = alert.EventId;
        }

        /// <summary>
        /// Panic takes one of three shapes, fixed per person: run, freeze and
        /// then run, or freeze for good.
        /// </summary>
        public void MakeScared(Agent agent)
        {
            if (agent.Fear.State == AgentFearState.Scared)
            {
                return;
            }

            int tick = context.Tick;
            agent.Fear.State = AgentFearState.Scared;
            agent.Fear.LastFrightTick = tick;
            agent.Fear.CalmsAtTick = 0;
            if (context.Scenario.Calming.Enabled)
            {
                // Their own quiet spell, a little different from everybody
                // else's, so a room frightened together never settles together.
                agent.Fear.QuietTicks = context.Jittered(context.Scenario.Calming.QuietTicks);
            }

            // Whatever the day had them doing is over: fear has its own rules.
            // Cleared here as well as on the way into being alert, because a
            // test frightens somebody straight to scared.
            agent.Errand.Clear();
            CausalEvent scared = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentScared,
                agent.Body.Position,
                threats.Count,
                0,
                agent.Fear.AlertEventId != 0UL ? agent.Fear.AlertEventId : threats.RootEventId);
            agent.Fear.ScaredEventId = scared.EventId;

            if (agent.Personality.Temperament == AgentPanicTemperament.Runner)
            {
                StartFleeing(agent);
                return;
            }

            TemperamentSettings settings = context.Scenario.Temperament;
            agent.Intent.Activity = AgentActivityState.Frozen;
            agent.Fear.FreezeEndTick = agent.Personality.Temperament == AgentPanicTemperament.FreezeForever
                ? int.MaxValue
                : checked(tick + context.Random.NextIntInclusive(settings.FreezeMinimumTicks, settings.FreezeMaximumTicks));
            CausalEvent froze = context.Events.Append(
                tick,
                agent.Id,
                CausalEventType.AgentFroze,
                agent.Body.Position,
                0,
                agent.Fear.FreezeEndTick == int.MaxValue ? 0 : agent.Fear.FreezeEndTick - tick,
                scared.EventId);
            agent.Fear.FrozeEventId = froze.EventId;
        }

        /// <summary>
        /// Snapping out of a freeze: log it, start running, and shout as soon
        /// as anybody does. The cause is their own freeze running out, or someone shaking them.
        /// </summary>
        public void Unfreeze(Agent agent, ulong causalParentEventId = 0UL)
        {
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentUnfroze, agent.Body.Position, 0, 0,
                causalParentEventId != 0UL ? causalParentEventId : agent.Fear.FrozeEventId);
            StartFleeing(agent);
        }

        /// <summary>
        /// Running. The first shout comes with the first stride, a few ticks
        /// late like every reaction, and the next ones at their own interval:
        /// it used to come two to five seconds in, which spread a fright
        /// round a meeting table one seat at a time.
        /// </summary>
        private void StartFleeing(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Fleeing;
            agent.Doors.HasLookedForAWayOut = false;
            context.ThinkAgainSoon(agent.Intent);
            agent.Fear.NextShoutTick = context.ReactionTick();
        }

        /// <summary>
        /// A startled person stops. If they saw the danger they turn to face
        /// it; if they were yelled at or bumped, they turn toward where that
        /// came from.
        /// </summary>
        public MotorIntent AlertIntent(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Reacting;
            int goalHeading = agent.Body.Heading;
            if (agent.Fear.AlertSource == AgentAlertSource.Visual &&
                threats.NearestDistanceSquared(agent.Body.Position, out LogicalPosition dangerPoint, out _) < long.MaxValue)
            {
                goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, dangerPoint, agent.Body.Heading);
            }
            else if (agent.Hearing.HasSoundPoint)
            {
                goalHeading = IntegerMath.HeadingBetween(agent.Body.Position, agent.Hearing.SoundPoint, agent.Body.Heading);
            }

            return new MotorIntent(goalHeading, 0, agent.Personality.PanicTurnRate, context.Scenario.Panic.Acceleration);
        }

        // ---------------------------------------------------------------- calming down

        /// <summary>The ticks on which somebody is due to settle, so no two settle on the same one.</summary>
        private readonly System.Collections.Generic.HashSet<int> calmEndsTaken = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Something frightening again -- a bell, a bang, the danger back in
        /// sight: their fear is full again and the quiet spell starts over.
        /// </summary>
        public void Refresh(Agent agent)
        {
            if (agent.Fear.State != AgentFearState.Scared)
            {
                return;
            }

            agent.Fear.LastFrightTick = context.Tick;
            agent.Fear.CalmsAtTick = 0;
        }

        /// <summary>
        /// Phase 4, straight after they have looked and listened: a frightened
        /// person who has seen and heard nothing frightening for a while
        /// settles, at their own pace (prototype 3, 2026-09-26). The owner's
        /// rule: "the ones that saw the fire stay rattled for a while, the
        /// nervous might freak out more, the calm and brave don't really care
        /// -- this should be the personality system in play, not scripted."
        /// <para>
        /// Fear is full the moment it is refreshed. After their own quiet spell
        /// it drains at a pace their bravery quickens and their nervousness
        /// slows, down to a floor their nervousness sets; below the line they
        /// settle, a few ticks late like every reaction and never on the same
        /// tick as anybody else. Somebody whose floor is above the line never
        /// settles: the very nervous keep heading out. The numbers and the
        /// cast they give are in <see cref="CalmingSettings"/>.
        /// </para>
        /// <para>
        /// What keeps somebody frightened: being alight, any danger in sight
        /// or in their room, a danger's own noise within earshot (through a
        /// shut door, half as far), and -- delivered by <see cref="SoundSystem"/>
        /// -- a bell or a bang. A yell does not: a crowd that has stopped
        /// being frightened of anything would otherwise keep itself frightened
        /// by shouting about it. Each person looks every
        /// <see cref="CalmingSettings.CheckEveryTicks"/>, on their own beat.
        /// </para>
        /// It draws a number only when somebody's moment to settle is set.
        /// </summary>
        public void Settle(Agent agent)
        {
            CalmingSettings calming = context.Scenario.Calming;
            if (!calming.Enabled || agent.Fear.State != AgentFearState.Scared || !agent.IsParticipating)
            {
                return;
            }

            int tick = context.Tick;
            AgentFear fear = agent.Fear;
            if (agent.Burning.IsBurning ||
                ((tick + agent.Index) % calming.CheckEveryTicks == 0 && StillFrightening(agent)))
            {
                Refresh(agent);
                return;
            }

            if (fear.CalmsAtTick > 0)
            {
                if (tick >= fear.CalmsAtTick)
                {
                    CalmDown(agent);
                }

                return;
            }

            if (FearFloorPerMille(agent, calming) >= calming.CalmBelowPerMille)
            {
                // The very nervous never settle.
                return;
            }

            long drainTicks = (long)(1000 - calming.CalmBelowPerMille) * Run.TicksPerSecond / DrainPerMillePerSecond(agent, calming);
            if (tick < (long)fear.LastFrightTick + fear.QuietTicks + drainTicks)
            {
                return;
            }

            fear.CalmsAtTick = checked(tick + Staggered(calmEndsTaken, tick, context.ReactionLag()));
        }

        /// <summary>How fast somebody's fear drains once the quiet spell is over: the brave fast, the nervous slowly.</summary>
        private static int DrainPerMillePerSecond(Agent agent, CalmingSettings calming)
        {
            int rate = calming.DrainBasePerMillePerSecond + calming.DrainPerBravery * agent.Traits.Bravery -
                       calming.DrainPerNervousness * agent.Traits.Nervousness;
            return System.Math.Max(calming.DrainMinimumPerMillePerSecond, rate);
        }

        /// <summary>How low somebody's fear can drain at all: nothing for the steady, most of the way for the very nervous.</summary>
        private static int FearFloorPerMille(Agent agent, CalmingSettings calming)
        {
            return System.Math.Max(0, calming.FloorPerNervousness * (agent.Traits.Nervousness - 5));
        }

        /// <summary>Whether anything frightening is still in sight, in their room, or in earshot.</summary>
        private bool StillFrightening(Agent agent)
        {
            if (!threats.AnyActive)
            {
                return false;
            }

            LogicalPosition at = agent.Body.Position;
            int room = geometry != null ? geometry.RoomAt(at) : -1;
            if (room >= 0 && threats.IsInRoom(room))
            {
                return true;
            }

            if (threats.IsVisibleFrom(at, agent.Body.Heading, context.Scenario.Perception.VisionRangeMillimetres, out _))
            {
                agent.Fear.SawTheThreat = true;
                return true;
            }

            if (!threats.HeardNearby(at, out LogicalPosition noise, out _, out long reach))
            {
                return false;
            }

            // Through a wall or a shut door a noise carries half as far, as
            // every other noise does.
            if (geometry != null && !geometry.RoomsOpenToEachOther(geometry.RoomAtPoint(noise), room))
            {
                long muffled = reach / 2;
                return LogicalPosition.DistanceSquared(at, noise) <= muffled * muffled;
            }

            return true;
        }

        /// <summary>
        /// Somebody frightened settles. Only while they are running, dithering
        /// or frozen -- never mid-way through forcing a door, hosing the fire,
        /// hauling somebody out or pulling the alarm, and never while they are
        /// off their feet: they finish that first, and settle after. They are
        /// rattled for a while (longer if they saw the danger), and those with
        /// a desk head back to it -- unless the danger is in that room.
        /// </summary>
        private void CalmDown(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            bool free = activity == AgentActivityState.Fleeing || activity == AgentActivityState.Hesitating ||
                        activity == AgentActivityState.Frozen || activity == AgentActivityState.Standing;
            if (!free || agent.Body.State != AgentBodyState.Upright || agent.Help.TargetIndex >= 0 ||
                agent.Sitting.Phase == SitPhase.LeapingUp)
            {
                return;
            }

            int tick = context.Tick;
            AgentFear fear = agent.Fear;
            CalmingSettings calming = context.Scenario.Calming;
            int rattled = fear.SawTheThreat ? calming.RattledAfterSeeingTicks : calming.RattledAfterHearingTicks;
            fear.State = AgentFearState.Calm;
            fear.AlertSource = AgentAlertSource.None;
            fear.CalmsAtTick = 0;
            fear.FreezeEndTick = 0;
            fear.RattledUntilTick = checked(tick + context.Jittered(rattled));
            fear.SawTheThreat = false;

            // The escape is over: whatever they meant to run for, they no
            // longer do. What they learned about the building -- doors found
            // shut, the way out -- they keep.
            AgentIntent intent = agent.Intent;
            intent.Activity = AgentActivityState.Standing;
            intent.ActivityEndTick = tick;
            intent.SetOnAWayOut = false;
            intent.SwerveOffset = 0;
            intent.SwerveEndTick = 0;
            agent.Doors.ExitDoorIndex = -1;
            agent.Doors.WayOutDoorIndex = -1;
            agent.Doors.HasLookedForAWayOut = false;
            agent.Doors.DashingUntilTick = 0;
            agent.Leading.FollowingIndex = -1;
            agent.Hearing.HasSoundPoint = false;
            agent.Hearing.ClearPending();
            agent.Body.BlockedTicks = 0;

            context.Events.Append(tick, agent.Id, CausalEventType.AgentCalmedDown, agent.Body.Position, rattled, 0,
                fear.ScaredEventId);

            if (cues != null && agent.Home.Exists && !threats.IsInRoom(HomeRoom(agent)))
            {
                cues.SendHome(agent);
            }
        }

        private int HomeRoom(Agent agent)
        {
            if (geometry == null)
            {
                return -1;
            }

            LogicalPosition home = agent.Home.Chair >= 0 && objects != null
                ? objects.PositionOf(agent.Home.Chair)
                : agent.Home.Spot;
            return geometry.RoomAtPoint(home);
        }
    }
}
