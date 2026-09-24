namespace Paniq.Simulation
{
    /// <summary>
    /// Cues: the small things that happen in the building's day and change
    /// what some people want to do. The meeting ends, it is home time,
    /// somebody starts a chat, somebody goes to the toilet. A cue is called
    /// by the Director from the level's timetable, by a person as their own
    /// idea, or by the player; whoever calls it, it ends here.
    /// <para>
    /// A cue is delivered the way a leader's shout is, never by reaching into
    /// heads: it is written into the log once, and every calm person in its
    /// audience is handed a pending <see cref="AgentErrand"/> that they take
    /// up at their own reaction tick, each their own few ticks late and each
    /// with their own seeded share of the cue's spread, so a room never gets
    /// up in unison. Nobody frightened is in any audience: fear has its own
    /// rules, and nothing here switches on an event's name.
    /// </para>
    /// <para>
    /// The leader's part: a meeting is ended by its <em>host</em>, the seated
    /// person in the room with the most leadership, who is the cue's source
    /// and the first on their feet. Later cues -- calling a meeting, walking
    /// the visitors out, a fire drill -- use the same host rule.
    /// </para>
    /// </summary>
    internal sealed class CueSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DaySettings settings;

        public CueSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            settings = context.Scenario.Day;
        }

        /// <summary>
        /// The meeting in this room is over. The host says so and is up first;
        /// everybody else calm in the room follows, each their own while
        /// later, spread over <paramref name="spreadTicks"/>.
        /// </summary>
        public ulong EndMeeting(int room, int spreadTicks, ulong causeEventId)
        {
            Agent host = HostOf(room);
            LogicalPosition where = host != null ? host.Body.Position : geometry.RoomBounds(room).Centre;
            ulong cue = context.Events.Append(context.Tick, host != null ? host.Id : default, CausalEventType.CueCalled,
                where, (int)CueKind.MeetingEnds, 0, causeEventId, geometry.RoomId(room)).EventId;

            using Crowd.Nearby inRoom = crowd.Gather(geometry.RoomBounds(room));
            for (int c = 0; c < inRoom.Count; c++)
            {
                Agent person = crowd.All[inRoom[c]];
                if (!CanTakeUpACue(person) || geometry.RoomOf(person) != room)
                {
                    continue;
                }

                int offset = person == host ? 0 : context.Random.NextIntInclusive(0, spreadTicks);
                Hand(person, ErrandKind.GoHome, checked(context.ReactionTick() + offset), cue);
            }

            return cue;
        }

        /// <summary>
        /// The end of the working day. Everybody calm in the building packs up
        /// and heads for the way out, each their own while later, spread over
        /// <paramref name="spreadTicks"/>. Nobody announces it: people look at
        /// the clock for themselves.
        /// </summary>
        public ulong CallHomeTime(int spreadTicks, ulong causeEventId)
        {
            ulong cue = context.Events.Append(context.Tick, default, CausalEventType.CueCalled,
                geometry.FireArea.Centre, (int)CueKind.HomeTime, 0, causeEventId).EventId;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (!CanTakeUpACue(agents[i]))
                {
                    continue;
                }

                int offset = context.Random.NextIntInclusive(0, spreadTicks);
                Hand(agents[i], ErrandKind.LeaveTheBuilding, checked(context.ReactionTick() + offset), cue);
            }

            return cue;
        }

        /// <summary>
        /// Somebody's own idea: they go over to somebody and talk to them. The
        /// other person is hailed and turns to face them, a few ticks late like
        /// every reaction; the two of them talk until one of them has had
        /// enough or something frightens either. False when the other person
        /// is not free to be talked to.
        /// </summary>
        public bool StartChat(Agent initiator, Agent partner)
        {
            if (!CanTakeUpACue(partner) || partner.Errand.Kind != ErrandKind.None || initiator.Errand.Kind != ErrandKind.None)
            {
                return false;
            }

            ulong cue = context.Events.Append(context.Tick, initiator.Id, CausalEventType.CueCalled,
                initiator.Body.Position, (int)CueKind.Chat, 0, 0UL, partner.Id).EventId;
            int until = checked(context.Tick + context.Random.NextIntInclusive(settings.ChatMinimumTicks, settings.ChatMaximumTicks));

            // Their own idea starts now; being hailed is a reaction and starts late.
            Hand(initiator, ErrandKind.ChatWith, context.Tick, cue);
            initiator.Errand.PartnerIndex = partner.Index;
            initiator.Errand.UntilTick = until;
            Hand(partner, ErrandKind.ChatWith, context.ReactionTick(), cue);
            partner.Errand.PartnerIndex = initiator.Index;
            partner.Errand.UntilTick = until;
            return true;
        }

        /// <summary>
        /// Somebody's own idea: they go to the toilet. Starts now, because
        /// nobody waits for their own idea. Not while a cue is waiting on
        /// them: what the building asks beats what they thought of.
        /// </summary>
        public bool StartToiletTrip(Agent person)
        {
            if (person.Errand.Kind != ErrandKind.None)
            {
                return false;
            }

            ulong cue = context.Events.Append(context.Tick, person.Id, CausalEventType.CueCalled,
                person.Body.Position, (int)CueKind.ToiletTrip).EventId;
            Hand(person, ErrandKind.VisitTheToilet, context.Tick, cue);
            return true;
        }

        /// <summary>
        /// Somebody's own idea: back to their desk. Not a cue anybody else
        /// notices, so it is not logged; and not while a cue is waiting on them.
        /// </summary>
        public bool SendHome(Agent person)
        {
            if (person.Errand.Kind != ErrandKind.None)
            {
                return false;
            }

            Hand(person, ErrandKind.GoHome, context.Tick, 0UL);
            return true;
        }

        /// <summary>
        /// The host of a room: the seated person in it with the most
        /// leadership, the lower ID on a tie; failing anybody seated, anybody
        /// calm in it. Null for an empty room.
        /// </summary>
        public Agent HostOf(int room)
        {
            Agent host = null;
            bool hostSeated = false;
            using Crowd.Nearby inRoom = crowd.Gather(geometry.RoomBounds(room));
            for (int c = 0; c < inRoom.Count; c++)
            {
                Agent person = crowd.All[inRoom[c]];
                if (!CanTakeUpACue(person) || geometry.RoomOf(person) != room)
                {
                    continue;
                }

                bool seated = person.Sitting.OnIt;
                if (host == null ||
                    (seated && !hostSeated) ||
                    (seated == hostSeated && person.Traits.Leadership > host.Traits.Leadership))
                {
                    host = person;
                    hostSeated = seated;
                }
            }

            return host;
        }

        /// <summary>Calm, on their feet or in a chair, and still in the run: the only people a cue reaches.</summary>
        public static bool CanTakeUpACue(Agent person)
        {
            return person.IsParticipating && person.Fear.State == AgentFearState.Calm &&
                   !person.Burning.IsBurning && person.Body.IsOnTheirFeet;
        }

        /// <summary>
        /// Hands somebody a pending errand, replacing whatever they had in mind:
        /// a cue from outside beats a person's own plans, the way home time
        /// beats a chat.
        /// </summary>
        private static void Hand(Agent person, ErrandKind kind, int startTick, ulong causeEventId)
        {
            AgentErrand errand = person.Errand;
            errand.Clear();
            errand.Kind = kind;
            errand.Phase = ErrandPhase.NotStarted;
            errand.StartTick = startTick;
            errand.CauseEventId = causeEventId;
        }
    }
}
