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
            influence = systems.Influence;
            tells = systems.Tells;
        }

        /// <summary>The wind-up before somebody freezes (2026-09-30).</summary>
        private TellSystem tells;

        /// <summary>The player's hand, built after this (2026-09-30): the startled turn to it.</summary>
        private InfluenceSystem influence;

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

            // Whatever they were doing for the hand, calm, is over (2026-09-30).
            InfluenceSystem.Interrupted(agent);
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
            InfluenceSystem.Interrupted(agent);
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

            // Going stiff (2026-09-30, the owner: "the visible agent tells"):
            // a second or so of shivering, harder and harder, before they lock
            // up -- and one poke, a tug or the hand in it, and they run instead.
            if (tells != null && tells.Enabled)
            {
                tells.Start(agent, AgentTell.GoingStiff, agent.Body.Heading, -1, froze.EventId);
            }
        }

        /// <summary>
        /// Snapping out of a freeze: log it, start running, and shout as soon
        /// as anybody does. The cause is their own freeze running out, or someone shaking them.
        /// </summary>
        public void Unfreeze(Agent agent, ulong causalParentEventId = 0UL)
        {
            TellSystem.Forget(agent);
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
        /// came from. Since 2026-09-30 the player's hand beats both: felt at
        /// half strength or more, they turn to a pull and edge toward it, or
        /// turn from a push and edge away (the owner: "we need clear
        /// influence"; the startled used to ignore it altogether).
        /// </summary>
        public MotorIntent AlertIntent(Agent agent)
        {
            agent.Intent.Activity = AgentActivityState.Reacting;
            int goalHeading = agent.Body.Heading;
            if (TryTurnToTheHand(agent, out int toTheHand, out int edge))
            {
                return new MotorIntent(toTheHand, edge, agent.Personality.PanicTurnRate, context.Scenario.Panic.Acceleration);
            }

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

        /// <summary>
        /// The hand felt at half strength or more, by somebody startled: which
        /// way to face (toward a pull, away from a push) and how fast to edge
        /// that way (half a calm walk). Draws nothing.
        /// </summary>
        private bool TryTurnToTheHand(Agent agent, out int heading, out int speed)
        {
            heading = agent.Body.Heading;
            speed = 0;
            if (influence == null)
            {
                return false;
            }

            // The hand on now, felt and taken in, pull or push: the startled
            // are not answering anything yet.
            bool pushes = influence.TryGetLivePush(agent, out InfluenceSystem.Place place, out int felt);
            if (!pushes && !influence.TryGetLivePull(agent, out place, out felt))
            {
                return false;
            }

            if (felt < context.Scenario.Influence.ActsAgainstNatureFromPerMille)
            {
                // Not felt enough. The same threshold as everything else the
                // hand asks (2026-09-30; it was half a full pull here alone).
                return false;
            }

            // Toward a pull along the way there, round walls and through the
            // doorway it is felt through (2026-09-30: it was a straight line,
            // into the wall when the hand was next door); away from a push.
            heading = place.Repels
                ? IntegerMath.HeadingBetween(place.At, agent.Body.Position, agent.Body.Heading)
                : geometry != null
                    ? geometry.Routes.HeadingToward(agent.Body.Position, place.At,
                        context.Scenario.World.OccupancyRadiusMillimetres, agent.Body.Heading)
                    : IntegerMath.HeadingBetween(agent.Body.Position, place.At, agent.Body.Heading);
            speed = agent.Personality.CalmSpeed / 2;
            return true;
        }

        // ---------------------------------------------------------------- calming down

        /// <summary>The ticks on which somebody is due to settle, so no two settle on the same one.</summary>
        private readonly System.Collections.Generic.HashSet<int> calmEndsTaken = new System.Collections.Generic.HashSet<int>();

        // The crowd switch (2026-10-01), a button on the test levels: the
        // owner's rule, "toggle button in UI, calm or panicked, toggling
        // should set their states". It is two player commands, so a replay
        // carries it, and it goes through the same startle and settle as
        // anything else, so nobody moves on the tick it is pressed and no
        // two people on one tick.

        /// <summary>
        /// Whether the switch stands at "panicked": everybody is kept
        /// frightened, and anybody found calm is startled again.
        /// </summary>
        public bool HoldsPanicked { get; private set; }

        /// <summary>The switch's last press, for the log lines it causes.</summary>
        private ulong switchEventId;

        /// <summary>
        /// The switch flicked to "panicked": everybody calm is startled, each
        /// with their own lag and reaction delay and a tick of their own to
        /// finish on, exactly as a bell startles them; everybody already
        /// frightened has their fear filled again. Held until
        /// <see cref="CalmEveryone"/>: nobody settles, and whoever is found
        /// calm again (up off the floor, say) is startled again in
        /// <see cref="Settle"/>.
        /// </summary>
        public void PanicEveryone(ulong causeEventId, Agent[] agents)
        {
            HoldsPanicked = true;
            switchEventId = causeEventId;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                agent.Fear.CalmOrdered = false;
                if (!agent.IsParticipating || agent.Burning.IsBurning)
                {
                    continue;
                }

                if (agent.Fear.State == AgentFearState.Calm)
                {
                    if (agent.Body.State == AgentBodyState.Upright)
                    {
                        StartAlert(agent, causeEventId, AgentAlertSource.CrowdSwitch);
                    }
                }
                else
                {
                    Refresh(agent);
                }
            }
        }

        /// <summary>
        /// The switch flicked to "calm": the hold is off, and everybody
        /// startled or frightened is given a tick of their own, a few ticks
        /// out, to settle on -- whatever the calming rules would have said,
        /// but never before they are free to (<see cref="CalmDown"/> waits
        /// for someone mid-way through a door or on the floor). From then on
        /// the ordinary rules decide who takes fright: a fire lit afterwards
        /// frightens them as it always did.
        /// </summary>
        public void CalmEveryone(ulong causeEventId, Agent[] agents)
        {
            HoldsPanicked = false;
            switchEventId = causeEventId;
            int tick = context.Tick;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || agent.Burning.IsBurning || agent.Fear.State == AgentFearState.Calm)
                {
                    continue;
                }

                agent.Fear.CalmOrdered = true;
                agent.Fear.CalmsAtTick = checked(tick + Staggered(calmEndsTaken, tick, context.ReactionLag()));
            }
        }

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
            int tick = context.Tick;
            AgentFear fear = agent.Fear;
            if (!agent.IsParticipating)
            {
                return;
            }

            // The crowd switch's orders come before the calming rules
            // (2026-10-01). Told to calm down, they settle on their tick
            // whatever is going on; held panicked, nobody settles and anybody
            // found calm is startled again, on a lag of their own.
            if (fear.CalmOrdered)
            {
                if (fear.State == AgentFearState.Calm)
                {
                    fear.CalmOrdered = false;
                }
                else if (tick >= fear.CalmsAtTick && !agent.Burning.IsBurning)
                {
                    CalmDown(agent);
                }

                return;
            }

            if (HoldsPanicked)
            {
                if (fear.State == AgentFearState.Calm)
                {
                    if (!agent.Burning.IsBurning && agent.Body.State == AgentBodyState.Upright)
                    {
                        StartAlert(agent, switchEventId, AgentAlertSource.CrowdSwitch);
                    }
                }
                else
                {
                    Refresh(agent);
                }

                return;
            }

            if (!calming.Enabled || fear.State != AgentFearState.Scared)
            {
                return;
            }

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
            // Somebody only startled, not yet frightened, has done nothing
            // yet and can be stood down where they are (the crowd switch is
            // the one thing that asks, 2026-10-01).
            bool free = activity == AgentActivityState.Fleeing || activity == AgentActivityState.Hesitating ||
                        activity == AgentActivityState.Frozen || activity == AgentActivityState.Standing ||
                        agent.Fear.State == AgentFearState.Alert;
            if (!free || agent.Body.State != AgentBodyState.Upright || agent.Help.TargetIndex >= 0 ||
                agent.Sitting.Phase == SitPhase.LeapingUp || TellSystem.IsTelling(agent))
            {
                return;
            }

            int tick = context.Tick;
            AgentFear fear = agent.Fear;
            CalmingSettings calming = context.Scenario.Calming;
            int rattled = fear.SawTheThreat ? calming.RattledAfterSeeingTicks : calming.RattledAfterHearingTicks;
            ulong cause = fear.CalmOrdered ? switchEventId
                : fear.ScaredEventId != 0UL ? fear.ScaredEventId : fear.AlertEventId;
            fear.CalmOrdered = false;
            fear.State = AgentFearState.Calm;
            fear.AlertSource = AgentAlertSource.None;
            InfluenceSystem.Interrupted(agent);
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
                cause);

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
