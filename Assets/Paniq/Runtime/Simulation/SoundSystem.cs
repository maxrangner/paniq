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
    /// <para>
    /// A person keeps more than one noise in their head. While they look
    /// toward one, a threat's own noise (fire crackling) or a louder noise
    /// nearer to them takes over; the rest wait, and are looked at in turn
    /// while they are still fresh (<see cref="AgentHearing"/>).
    /// </para>
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
        /// remark that is <paramref name="heard"/>); the rest of it is
        /// neither heard nor written down, or the whole office would spend
        /// the day staring at the two people talking beside them and the
        /// story would read "person 3 said something" forty times. The person
        /// being talked to never turns to wonder what the voice was, because
        /// it is them being talked to.
        /// </summary>
        public void Say(Agent speaker, ulong causalParentEventId, bool heard)
        {
            if (!heard)
            {
                return;
            }

            int reach = context.Scenario.Day.RemarkHearingRadiusMillimetres;
            CausalEvent said = context.Events.Append(
                context.Tick,
                speaker.Id,
                CausalEventType.AgentSaid,
                speaker.Body.Position,
                reach,
                0,
                causalParentEventId);
            Emit(speaker.Id, speaker.Body.Position, reach, 0, said.EventId, AgentAlertSource.Yell, speaker);
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
                    Notice(listener, position, soundEventId, hearing, false, sourceRoom);
                }
            }
        }

        /// <summary>
        /// A threat that makes a noise (fire crackles): a calm person near it
        /// but not looking at it turns to see what it is. How far each threat
        /// can be heard is the threat's own to say; through a wall or a shut
        /// door it carries half as far, like every other noise.
        /// </summary>
        public void HearThreats(Agent agent)
        {
            if (!threats.HeardNearby(agent.Body.Position, out LogicalPosition point, out ulong causeEventId, out long reach))
            {
                return;
            }

            int sourceRoom = geometry.RoomAtPoint(point);
            if (!geometry.RoomsOpenToEachOther(sourceRoom, geometry.RoomAtPoint(agent.Body.Position)))
            {
                long muffled = reach / 2;
                if (LogicalPosition.DistanceSquared(agent.Body.Position, point) > muffled * muffled)
                {
                    return;
                }
            }

            Notice(agent, point, causeEventId, reach, true, sourceRoom);
        }

        /// <summary>
        /// Something seen rather than heard that turns a calm head the same
        /// way: somebody bolting across the room. The brave look before they
        /// run.
        /// </summary>
        public void LookToward(Agent agent, LogicalPosition point, ulong causeEventId)
        {
            Notice(agent, point, causeEventId, settings.YellHearingRadiusMillimetres, false, geometry.RoomAtPoint(point));
        }

        /// <summary>
        /// Done looking toward one noise: the freshest of the ones that
        /// waited, a threat's first, is looked at next, a few ticks late like
        /// every reaction. False when nothing fresh is waiting.
        /// </summary>
        public bool TakeUpAPendingNoise(Agent agent)
        {
            AgentHearing hearing = agent.Hearing;
            int tick = context.Tick;
            int pick = -1;
            for (int i = 0; i < hearing.PendingCount; i++)
            {
                HeardNoise noise = hearing.Pending[i];
                if (tick - noise.Tick > settings.PendingNoiseFreshTicks)
                {
                    continue;
                }

                if (pick < 0 || (noise.IsAThreat && !hearing.Pending[pick].IsAThreat) ||
                    (noise.IsAThreat == hearing.Pending[pick].IsAThreat && noise.Tick > hearing.Pending[pick].Tick))
                {
                    pick = i;
                }
            }

            if (pick < 0)
            {
                hearing.ClearPending();
                return false;
            }

            HeardNoise next = hearing.Pending[pick];
            hearing.Pending[pick] = hearing.Pending[hearing.PendingCount - 1];
            hearing.PendingCount--;
            Begin(agent, next.Point, next.EventId, next.Loudness, next.IsAThreat, next.Room);
            return true;
        }

        /// <summary>Talking to each other, from either side: neither turns to wonder what the other's voice was.</summary>
        private static bool InTheSameChat(Agent a, Agent b)
        {
            return (a.Errand.Has && a.Errand.PartnerIndex == b.Index) ||
                   (b.Errand.Has && b.Errand.PartnerIndex == a.Index);
        }

        /// <summary>How close two noises have to be to count as the same one, in millimetres.</summary>
        private const long SameNoiseMillimetres = 1000L;

        /// <summary>
        /// A calm person hears something. Doing nothing in particular, they
        /// drop it to look toward the noise. Already looking toward another:
        /// the same noise again changes nothing; a threat's noise, or a
        /// louder one nearer to them, takes over and the other waits; any
        /// other noise waits its turn.
        /// </summary>
        private void Notice(Agent agent, LogicalPosition point, ulong soundEventId, long loudness, bool isAThreat, int sourceRoom)
        {
            AgentHearing hearing = agent.Hearing;
            if (isAThreat && agent.Errand.Active && agent.Errand.Cue == CueKind.GoAndLook)
            {
                // Already on their way to see what it is.
                return;
            }

            if (agent.Intent.Activity != AgentActivityState.Investigating && hearing.HasLookedAtAnything &&
                context.Tick - hearing.LastLookedTick <= settings.PendingNoiseFreshTicks &&
                LogicalPosition.DistanceSquared(point, hearing.LastLookedPoint) <= SameNoiseMillimetres * SameNoiseMillimetres)
            {
                // They looked at that a moment ago. A fire crackling in the
                // next room used to turn the same head every couple of
                // seconds for as long as it burned, which is not attention,
                // it is a twitch -- and it cut short every errand they began.
                return;
            }

            if (agent.Intent.Activity == AgentActivityState.Investigating && hearing.HasSoundPoint)
            {
                if (LogicalPosition.DistanceSquared(point, hearing.SoundPoint) <= SameNoiseMillimetres * SameNoiseMillimetres)
                {
                    hearing.SoundIsAThreat |= isAThreat;
                    return;
                }

                bool takesOver = (isAThreat && !hearing.SoundIsAThreat) ||
                                 (isAThreat == hearing.SoundIsAThreat && loudness > hearing.SoundLoudness &&
                                  LogicalPosition.DistanceSquared(agent.Body.Position, point) <
                                  LogicalPosition.DistanceSquared(agent.Body.Position, hearing.SoundPoint));
                if (!takesOver)
                {
                    Remember(hearing, point, soundEventId, loudness, isAThreat, sourceRoom, context.Tick);
                    return;
                }

                // The one they were looking at is not forgotten: it waits.
                Remember(hearing, hearing.SoundPoint, hearing.SoundEventId, hearing.SoundLoudness, hearing.SoundIsAThreat,
                    hearing.SoundRoom, context.Tick);
            }

            Begin(agent, point, soundEventId, loudness, isAThreat, sourceRoom);
        }

        /// <summary>Starts looking toward a noise, a few ticks late like every reaction: they finish the step they were taking before their head comes round.</summary>
        private void Begin(Agent agent, LogicalPosition point, ulong soundEventId, long loudness, bool isAThreat, int sourceRoom)
        {
            AgentHearing hearing = agent.Hearing;
            agent.Intent.Activity = AgentActivityState.Investigating;
            agent.Intent.SocialPartnerIndex = -1;
            agent.Body.BlockedTicks = 0;
            hearing.SoundPoint = point;
            hearing.HasSoundPoint = true;
            hearing.SoundEventId = soundEventId;
            hearing.SoundIsAThreat = isAThreat;
            hearing.SoundRoom = sourceRoom;
            hearing.SoundLoudness = loudness;
            hearing.LastLookedPoint = point;
            hearing.LastLookedTick = context.Tick;
            hearing.HasLookedAtAnything = true;
            hearing.InvestigateStartTick = context.ReactionTick();
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Random.NextIntInclusive(
                settings.InvestigateMinimumTicks,
                settings.InvestigateMaximumTicks));
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentNoticedSound, point, 0, 0, soundEventId);
        }

        /// <summary>Puts a noise aside to be looked at later. One already waiting nearby stands for it; when there is no room, the oldest goes.</summary>
        private static void Remember(AgentHearing hearing, LogicalPosition point, ulong eventId, long loudness, bool isAThreat, int room, int tick)
        {
            int oldest = -1;
            for (int i = 0; i < hearing.PendingCount; i++)
            {
                if (LogicalPosition.DistanceSquared(point, hearing.Pending[i].Point) <= SameNoiseMillimetres * SameNoiseMillimetres)
                {
                    hearing.Pending[i].Tick = tick;
                    hearing.Pending[i].IsAThreat |= isAThreat;
                    return;
                }

                if (oldest < 0 || hearing.Pending[i].Tick < hearing.Pending[oldest].Tick)
                {
                    oldest = i;
                }
            }

            int slot = hearing.PendingCount < AgentHearing.PendingCapacity ? hearing.PendingCount++ : oldest;
            hearing.Pending[slot] = new HeardNoise
            {
                Point = point,
                EventId = eventId,
                Tick = tick,
                Loudness = loudness,
                IsAThreat = isAThreat,
                Room = room
            };
        }
    }
}
