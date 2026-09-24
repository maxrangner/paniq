using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Sound is a simulation idea, not audio: a noise has a position and a
    /// reach. Calm people who hear one turn to look ("what was that?"), and
    /// a yell close enough to be understood alarms them outright. Alarmed
    /// and panicking people ignore noises. Noises are delivered the moment
    /// they are made, to listeners in ascending ID order. A closed door
    /// between rooms muffles a noise to half its reach.
    /// </summary>
    internal sealed class SoundSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly Threats threats;
        private readonly FearSystem fear;
        private readonly WorldGeometry geometry;
        private readonly HearingSettings settings;

        public SoundSystem(SimulationContext context, Crowd crowd, Threats threats, FearSystem fear, WorldGeometry geometry)
        {
            this.context = context;
            this.crowd = crowd;
            this.threats = threats;
            this.fear = fear;
            this.geometry = geometry;
            settings = context.Scenario.Hearing;
        }

        /// <summary>A yell: understood (alarming) close by, merely heard further away.</summary>
        public void Yell(Agent agent, ulong causalParentEventId)
        {
            CausalEvent yell = context.Events.Append(
                context.Tick,
                agent.Id,
                CausalEventType.AgentYelled,
                agent.Body.Position,
                settings.YellHearingRadiusMillimetres,
                0,
                causalParentEventId);
            Emit(agent.Id, agent.Body.Position, settings.YellHearingRadiusMillimetres,
                settings.YellAlarmRadiusMillimetres, yell.EventId);
        }

        /// <summary>
        /// A remark in a conversation: quiet, heard a little way off, alarming
        /// nobody. Neighbours glance over at the talking when it starts (a
        /// remark that is <paramref name="heard"/>); the rest of it is only
        /// written down, or the whole office would spend the day staring at
        /// the two people talking beside them. The person being talked to
        /// never turns to wonder what the voice was, because it is them being
        /// talked to.
        /// </summary>
        public void Say(Agent speaker, ulong causalParentEventId, bool heard)
        {
            int reach = heard ? context.Scenario.Day.RemarkHearingRadiusMillimetres : 0;
            CausalEvent said = context.Events.Append(
                context.Tick,
                speaker.Id,
                CausalEventType.AgentSaid,
                speaker.Body.Position,
                reach,
                0,
                causalParentEventId);
            if (heard)
            {
                Emit(speaker.Id, speaker.Body.Position, reach, 0, said.EventId, AgentAlertSource.Yell, speaker);
            }
        }

        /// <summary>A collision, trip, shove or box hit: heard up to the thud reach, alarming no one.</summary>
        public void Thud(SimulationId sourceId, LogicalPosition position, ulong soundEventId)
        {
            Emit(sourceId, position, settings.BumpSoundRadiusMillimetres, 0, soundEventId);
        }

        /// <summary>
        /// A bang: something going off. It carries a long way and frightens
        /// people near it outright, but it shows them nothing, so it works on
        /// them like a yell rather than like a bell.
        /// </summary>
        public void Bang(SimulationId sourceId, LogicalPosition position, int hearingRadius, int alarmRadius, ulong soundEventId)
        {
            Emit(sourceId, position, hearingRadius, alarmRadius, soundEventId);
        }

        /// <summary>
        /// An alarm bell. Unlike a yell it tells everybody who hears it that
        /// there is a fire, without showing them one, so the level-headed among
        /// them leave briskly instead of panicking.
        /// </summary>
        public void Bell(SimulationId alarmId, LogicalPosition position, ulong soundEventId)
        {
            AlarmSettings alarm = context.Scenario.Alarm;
            Emit(alarmId, position, alarm.BellHearingRadiusMillimetres, alarm.BellAlarmRadiusMillimetres, soundEventId,
                AgentAlertSource.Alarm);
        }

        /// <summary>
        /// Delivers one noise to every other calm participating person, in
        /// ascending ID order. Inside <paramref name="alarmRadius"/> the noise
        /// alarms; inside <paramref name="hearingRadius"/> it only draws attention.
        /// </summary>
        private void Emit(
            SimulationId sourceId,
            LogicalPosition position,
            int hearingRadius,
            int alarmRadius,
            ulong soundEventId,
            AgentAlertSource alertSource = AgentAlertSource.Yell,
            Agent speaker = null)
        {
            int sourceRoom = geometry.RoomAtPoint(position);

            // The furthest a noise could possibly carry. A closed door halves
            // it per listener, so asking for the undivided reach can only
            // gather people the tests below then discard.
            long furthest = Math.Max(hearingRadius, alarmRadius);
            using Crowd.Nearby listeners = crowd.Within(position, furthest);
            for (int i = 0; i < listeners.Count; i++)
            {
                Agent listener = crowd.All[listeners[i]];
                if (listener.Id == sourceId ||
                    !listener.IsParticipating ||
                    listener.Fear.State != AgentFearState.Calm ||
                    (speaker != null && InTheSameChat(speaker, listener)))
                {
                    continue;
                }

                // Through a closed door a noise carries half as far.
                int divisor = geometry.RoomsOpenToEachOther(sourceRoom, geometry.RoomAtPoint(listener.Body.Position)) ? 1 : 2;
                long hearing = hearingRadius / divisor;
                long alarm = alarmRadius / divisor;
                long distanceSquared = LogicalPosition.DistanceSquared(listener.Body.Position, position);
                if (alarm > 0 && distanceSquared <= alarm * alarm)
                {
                    fear.Alarm(listener, soundEventId, alertSource, position);
                }
                else if (distanceSquared <= hearing * hearing)
                {
                    Notice(listener, position, soundEventId);
                }
            }
        }

        /// <summary>
        /// A threat that makes a noise (fire crackles): a calm person near it
        /// but not looking at it turns to see what it is. How far each threat
        /// can be heard is the threat's own to say.
        /// </summary>
        public void HearThreats(Agent agent)
        {
            if (threats.HeardNearby(agent.Body.Position, out LogicalPosition point, out ulong causeEventId))
            {
                Notice(agent, point, causeEventId);
            }
        }

        /// <summary>Talking to each other, from either side: neither turns to wonder what the other's voice was.</summary>
        private static bool InTheSameChat(Agent a, Agent b)
        {
            return (a.Errand.Kind == ErrandKind.ChatWith && a.Errand.PartnerIndex == b.Index) ||
                   (b.Errand.Kind == ErrandKind.ChatWith && b.Errand.PartnerIndex == a.Index);
        }

        /// <summary>A calm person drops what they were doing to look toward a noise.</summary>
        private void Notice(Agent agent, LogicalPosition point, ulong soundEventId)
        {
            agent.Intent.Activity = AgentActivityState.Investigating;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Body.BlockedTicks = 0;
            agent.Hearing.SoundPoint = point;
            agent.Hearing.HasSoundPoint = true;

            // A few ticks late, like every reaction: they finish the step
            // they were taking before their head comes round.
            agent.Hearing.InvestigateStartTick = context.ReactionTick();
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.InvestigateMinimumTicks,
                settings.InvestigateMaximumTicks));
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentNoticedSound, point, 0, 0, soundEventId);
        }
    }
}
