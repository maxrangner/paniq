using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Cues: the small things that happen in the building's day and change
    /// what some people want to do. The meeting ends, it is home time,
    /// somebody starts a chat, somebody goes to the toilet. A cue is called
    /// by the Director from the level's timetable, by a person as their own
    /// idea, or by the player; whoever calls it, it ends here.
    /// <para>
    /// What a cue <em>is</em> -- who it reaches, who speaks for it, and what
    /// they do about it step by step -- is data on the scenario
    /// (<see cref="CueDefinition"/>), one per <see cref="CueKind"/>. This
    /// system only works out the audience and hands each person the cue;
    /// <see cref="ErrandBehaviour"/> carries the script out.
    /// </para>
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
    /// The leader's part: a cue in a room may have a <em>host</em>, the seated
    /// person in the room with the most leadership, who is the cue's source,
    /// follows the host script, and is the first on their feet.
    /// </para>
    /// </summary>
    internal sealed class CueSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;

        /// <summary>Every kind's definition, by kind, looked up once.</summary>
        private readonly CueDefinition[] definitions;

        /// <summary>
        /// The ticks on which somebody already takes up a cue, so no two
        /// people take one up on the same tick: the owner's rule that nothing
        /// happens to a whole group on one tick, kept the way fear keeps it
        /// for reactions. Ticks gone by are forgotten as they pass.
        /// </summary>
        private readonly HashSet<int> startsTaken = new HashSet<int>();

        /// <summary>The middle of the floor, where a cue for the whole building is written down as happening.</summary>
        private readonly LogicalPosition buildingCentre;

        /// <summary>The line home time was written on, for everybody who takes it up late.</summary>
        private ulong homeTimeLine;

        public CueSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            definitions = new CueDefinition[System.Enum.GetValues(typeof(CueKind)).Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                definitions[i] = context.Scenario.CueOf((CueKind)i);
            }

            LogicalBounds floor = geometry.RoomBounds(0);
            for (int r = 1; r < geometry.RoomCount; r++)
            {
                LogicalBounds room = geometry.RoomBounds(r);
                floor = new LogicalBounds(Math.Min(floor.MinX, room.MinX), Math.Max(floor.MaxX, room.MaxX),
                    Math.Min(floor.MinZ, room.MinZ), Math.Max(floor.MaxZ, room.MaxZ));
            }

            buildingCentre = floor.Centre;
        }

        public CueDefinition DefinitionOf(CueKind kind) => definitions[(int)kind];

        /// <summary>The tick home time was called on, or -1: it stands from then until everybody is out.</summary>
        public int HomeTimeTick { get; private set; } = -1;

        public bool IsHomeTime => HomeTimeTick >= 0;

        /// <summary>
        /// Home time again, for somebody who was busy when it was called or
        /// gave up on the way out: the same cue, the same line in the story,
        /// taken up at their own reaction tick. False when they are not free
        /// to.
        /// </summary>
        public bool RemindOfHomeTime(Agent person)
        {
            if (!IsHomeTime || person.Errand.Has || !CanTakeUpACue(person))
            {
                return false;
            }

            Hand(person, CueKind.HomeTime, false, Staggered(context.ReactionTick()), homeTimeLine);
            return true;
        }

        /// <summary>
        /// The first free tick at or after <paramref name="start"/>: nobody
        /// takes up a cue on a tick somebody else already does. Processing
        /// order decides who waits, so a replay agrees, and no random number
        /// is drawn.
        /// </summary>
        private int Staggered(int start)
        {
            int tick = context.Tick;
            startsTaken.RemoveWhere(taken => taken < tick);
            int stagger = context.Scenario.Perception.StartleStaggerTicks;
            while (!startsTaken.Add(start))
            {
                start = checked(start + stagger);
            }

            return start;
        }

        /// <summary>
        /// Calls a cue. Who it reaches follows its definition: the caller
        /// alone, the caller and the person it is about, everybody calm in
        /// <paramref name="room"/>, or everybody calm in the building. Returns
        /// the cue's line in the story, or 0 when nothing was called (a chat
        /// with somebody who is not free, an idea somebody with a cue waiting
        /// on them may not have).
        /// </summary>
        public ulong Call(CueKind kind, Agent caller, int room, Agent partner, int spreadTicks, ulong causeEventId)
        {
            CueDefinition cue = definitions[(int)kind];
            switch (cue.Audience)
            {
                case CueAudience.Self:
                    return CallForOne(cue, caller, causeEventId);
                case CueAudience.Pair:
                    return CallForTwo(cue, caller, partner, causeEventId);
                case CueAudience.Room:
                    return CallInRoom(cue, room, spreadTicks, causeEventId);
                default:
                    return CallInBuilding(cue, spreadTicks, causeEventId);
            }
        }

        /// <summary>The meeting in this room is over: the host says so and is up first; everybody else follows, each their own while later.</summary>
        public ulong EndMeeting(int room, int spreadTicks, ulong causeEventId) =>
            Call(CueKind.MeetingEnds, null, room, null, spreadTicks, causeEventId);

        /// <summary>The end of the working day: everybody calm in the building packs up and heads for the way out, each their own while later.</summary>
        public ulong CallHomeTime(int spreadTicks, ulong causeEventId) =>
            Call(CueKind.HomeTime, null, -1, null, spreadTicks, causeEventId);

        /// <summary>Somebody's own idea: over to somebody for a talk. False when either is not free.</summary>
        public bool StartChat(Agent initiator, Agent partner) =>
            Call(CueKind.Chat, initiator, -1, partner, 0, 0UL) != 0UL;

        /// <summary>Somebody's own idea: to the toilet. False when a cue is already waiting on them.</summary>
        public bool StartToiletTrip(Agent person) => Call(CueKind.ToiletTrip, person, -1, null, 0, 0UL) != 0UL;

        /// <summary>Somebody's own idea: back to their desk. False when a cue is already waiting on them.</summary>
        public bool SendHome(Agent person) => Call(CueKind.GoHome, person, -1, null, 0, 0UL) != 0UL;

        /// <summary>
        /// A person's own idea: they alone, now, because nobody waits for
        /// their own idea. Not while a cue is waiting on them: what the
        /// building asks beats what they thought of.
        /// </summary>
        private ulong CallForOne(CueDefinition cue, Agent person, ulong causeEventId)
        {
            if (person.Errand.Has)
            {
                return 0UL;
            }

            ulong line = WriteDown(cue, person, person.Body.Position, default, causeEventId);
            Hand(person, cue.Kind, true, context.Tick, line);
            return line == 0UL ? ulong.MaxValue : line;
        }

        /// <summary>
        /// A person's own idea about another person: their own part starts
        /// now; the other is hailed and takes it up a few ticks late, like
        /// every reaction. The length of what they do together (a chat) is
        /// drawn here, before either is handed it, and held by the one whose
        /// idea it was.
        /// </summary>
        private ulong CallForTwo(CueDefinition cue, Agent initiator, Agent partner, ulong causeEventId)
        {
            if (initiator.Errand.Has || partner.Errand.Has || !CanTakeUpACue(partner))
            {
                return 0UL;
            }

            ulong line = WriteDown(cue, initiator, initiator.Body.Position, partner.Id, causeEventId);
            int endTick = checked(context.Tick + TogetherLength(cue));

            Hand(initiator, cue.Kind, true, context.Tick, line);
            initiator.Errand.PartnerIndex = partner.Index;
            initiator.Errand.ChatEndTick = endTick;
            Hand(partner, cue.Kind, false, context.ReactionTick(), line);
            partner.Errand.PartnerIndex = initiator.Index;
            partner.Errand.ChatEndTick = endTick;
            return line == 0UL ? ulong.MaxValue : line;
        }

        /// <summary>How long the two of them are together: the range of the script's talk step, drawn from the seed.</summary>
        private int TogetherLength(CueDefinition cue)
        {
            ErrandStep[] script = cue.Script;
            for (int s = 0; s < script.Length; s++)
            {
                if (script[s].Kind == ErrandStepKind.Talk)
                {
                    return context.Random.NextIntInclusive(script[s].MinimumTicks, script[s].MaximumTicks);
                }
            }

            return 0;
        }

        /// <summary>
        /// A cue in a room: the host, if the cue has one, says so and takes it
        /// up with no spread; everybody else calm in the room follows, each
        /// their own while later, spread over <paramref name="spreadTicks"/>.
        /// </summary>
        private ulong CallInRoom(CueDefinition cue, int room, int spreadTicks, ulong causeEventId)
        {
            Agent host = cue.Host == CueHostRule.SeatedWithMostLeadership ? HostOf(room) : null;
            LogicalPosition where = host != null ? host.Body.Position : geometry.RoomBounds(room).Centre;
            ulong line = WriteDown(cue, host, where, geometry.RoomId(room), causeEventId);

            // The host is up first, whatever everybody else's own lag and
            // spread come to: nobody in the room beats the person who called
            // it, by at least a tick.
            int hostStart = host != null ? context.ReactionTick() : context.Tick;
            using Crowd.Nearby inRoom = crowd.Gather(geometry.RoomBounds(room));
            for (int c = 0; c < inRoom.Count; c++)
            {
                Agent person = crowd.All[inRoom[c]];
                if (!CanTakeUpACue(person) || geometry.RoomOf(person) != room)
                {
                    continue;
                }

                if (person == host)
                {
                    Hand(person, cue.Kind, true, hostStart, line);
                    continue;
                }

                int start = Math.Max(context.ReactionTick(), checked(hostStart + 1));
                Hand(person, cue.Kind, false, Staggered(checked(start + context.Random.NextIntInclusive(0, spreadTicks))), line);
            }

            return line;
        }

        /// <summary>
        /// A cue for the whole building: everybody calm, each their own while
        /// later. Nobody announces it: people look at the clock for
        /// themselves. Home time is remembered as a state of the day, not a
        /// moment, so whoever gives up on the way out tries again.
        /// </summary>
        private ulong CallInBuilding(CueDefinition cue, int spreadTicks, ulong causeEventId)
        {
            ulong line = WriteDown(cue, null, buildingCentre, default, causeEventId);
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (!CanTakeUpACue(agents[i]))
                {
                    continue;
                }

                int offset = context.Random.NextIntInclusive(0, spreadTicks);
                Hand(agents[i], cue.Kind, false, Staggered(checked(context.ReactionTick() + offset)), line);
            }

            if (cue.Kind == CueKind.HomeTime && !IsHomeTime)
            {
                HomeTimeTick = context.Tick;
                homeTimeLine = line;
            }

            return line;
        }

        /// <summary>The cue's line in the story, if it gets one: source is whoever called or hosts it, target the room or the person it is about.</summary>
        private ulong WriteDown(CueDefinition cue, Agent source, LogicalPosition where, SimulationId target, ulong causeEventId)
        {
            if (!cue.WrittenDown)
            {
                return 0UL;
            }

            return context.Events.Append(context.Tick, source != null ? source.Id : default, CausalEventType.CueCalled,
                where, (int)cue.Kind, 0, causeEventId, target).EventId;
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
                    (seated == hostSeated && (person.Traits.Leadership > host.Traits.Leadership ||
                                              (person.Traits.Leadership == host.Traits.Leadership && person.Id.Value < host.Id.Value))))
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
        /// Hands somebody a pending errand, replacing whatever they had in
        /// mind: a cue from outside beats a person's own plans. Somebody in
        /// the middle of an errand finishes it first, and takes this one up
        /// when it ends: home time called mid-chat waits for the chat, so a
        /// cue never changes what somebody is doing on the tick it is called.
        /// </summary>
        private static void Hand(Agent person, CueKind kind, bool isHost, int startTick, ulong causeEventId)
        {
            AgentErrand errand = person.Errand;
            if (errand.Active)
            {
                errand.Next = new PendingCue
                {
                    Has = true, Cue = kind, IsHost = isHost, StartTick = startTick, CauseEventId = causeEventId
                };
                return;
            }

            errand.Clear();
            errand.Has = true;
            errand.Cue = kind;
            errand.IsHost = isHost;
            errand.StartTick = startTick;
            errand.CauseEventId = causeEventId;
            errand.Origin = person.Body.Position;
        }
    }
}
