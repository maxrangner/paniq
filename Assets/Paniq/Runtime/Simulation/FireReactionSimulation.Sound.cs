namespace Paniq.Simulation
{
    /// <summary>
    /// Sound is a simulation idea, not audio: a noise has a position and a
    /// reach. Calm people who hear one turn to look ("what was that?"), and
    /// a yell close enough to be understood alarms them outright. Alarmed
    /// and panicking people ignore noises.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int InvestigateCreepDelayTicks = 25;
        private const int InvestigateCreepDistance = 2000;
        private const int InvestigateCreepMaximumTurn = 30;

        /// <summary>
        /// Delivers one noise to every other calm participating person, in
        /// ascending ID order. Inside <paramref name="alarmRadius"/> the noise
        /// alarms; inside <paramref name="hearingRadius"/> it only draws attention.
        /// </summary>
        private void EmitSound(
            StableAgentId sourceId,
            LogicalPosition position,
            int hearingRadius,
            int alarmRadius,
            ulong soundEventId)
        {
            long hearingSquared = (long)hearingRadius * hearingRadius;
            long alarmSquared = (long)alarmRadius * alarmRadius;
            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime listener = agents[i];
                if (listener.Id == sourceId ||
                    listener.Participation != AgentParticipation.Participating ||
                    listener.FearState != AgentFearState.Calm)
                {
                    continue;
                }

                long distanceSquared = LogicalPosition.DistanceSquared(listener.Position, position);
                if (alarmRadius > 0 && distanceSquared <= alarmSquared)
                {
                    StartAlert(listener, soundEventId, AgentAlertSource.Yell);
                    listener.SoundPoint = position;
                    listener.HasSoundPoint = true;
                }
                else if (distanceSquared <= hearingSquared)
                {
                    NoticeSound(listener, position, soundEventId);
                }
            }
        }

        /// <summary>A calm person drops what they were doing to look toward a noise.</summary>
        private void NoticeSound(AgentRuntime agent, LogicalPosition point, ulong soundEventId)
        {
            agent.Activity = AgentActivityState.Investigating;
            agent.SocialPartnerIndex = -1;
            agent.BlockedTicks = 0;
            agent.SoundPoint = point;
            agent.HasSoundPoint = true;
            agent.InvestigateStartTick = tick;
            agent.ActivityEndTick = checked(tick + random.NextIntInclusive(
                scenario.InvestigateMinimumTicks,
                scenario.InvestigateMaximumTicks));
            eventLog.Append(tick, agent.Id, FireReactionEventType.AgentNoticedSound, point, 0, 0, soundEventId);
        }

        /// <summary>Fire crackles: a calm person near it but not looking at it turns to see what it is.</summary>
        private void TryHearFire(AgentRuntime agent)
        {
            long radius = scenario.FireHearingRadiusMillimetres;
            if (radius <= 0L)
            {
                return;
            }

            long distanceSquared = NearestFireDistanceSquared(agent.Position, out LogicalPosition point, out int cell);
            if (distanceSquared <= radius * radius)
            {
                NoticeSound(agent, point, cellEventIds[cell]);
            }
        }

        /// <summary>
        /// Investigating: turn quickly toward the noise, then, if it is still
        /// some way off, edge toward it at half walking pace.
        /// </summary>
        private void UpdateInvestigating(AgentRuntime agent, out int goalHeading, out int goalSpeed)
        {
            goalHeading = HeadingBetween(agent.Position, agent.SoundPoint, agent.Heading);
            goalSpeed = 0;
            int facingError = System.Math.Abs(IntegerMath.SignedAngleDifference(agent.Heading, goalHeading));
            if (tick - agent.InvestigateStartTick >= InvestigateCreepDelayTicks &&
                facingError <= InvestigateCreepMaximumTurn &&
                Distance(agent.Position, agent.SoundPoint) > InvestigateCreepDistance)
            {
                goalSpeed = agent.CalmSpeed / 2;
            }
        }
    }
}
