using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Calm "loitering": each agent makes its own small decisions every few
    /// seconds - stroll somewhere, stand, look around, or wander over to
    /// stand near someone - using the seeded generator and its own seeded
    /// pace and turn rate.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int StrollWallMargin = 1000;
        private const int StrollMinimumDistance = 1500;
        private const int StrollArrivalDistance = 300;
        private const int StrollSlowdownDistance = 700;
        private const int StrollTimeoutTicks = 600;
        private const int WanderMaximumDegrees = 25;
        private const int SocialStopDistance = 1100;
        private const int SocialMinimumDistance = 1500;
        private const int SocialMaximumDistance = 6000;
        private const int SocialTimeoutTicks = 400;
        private const int CalmBlockedGiveUpTicks = 20;

        private void UpdateCalm(int agentIndex, AgentRuntime agent)
        {
            int goalHeading = agent.Heading;
            int goalSpeed = 0;
            bool steer = false;

            switch (agent.Activity)
            {
                case AgentActivityState.Standing:
                    if (TryGetPartner(agent, out AgentRuntime standingPartner))
                    {
                        goalHeading = HeadingBetween(agent.Position, standingPartner.Position, agent.Heading);
                    }

                    if (tick >= agent.ActivityEndTick)
                    {
                        ChooseCalmActivity(agentIndex, agent, false);
                    }

                    break;

                case AgentActivityState.LookingAround:
                    goalHeading = agent.LookHeading;
                    if (tick >= agent.ActivityEndTick)
                    {
                        if (agent.LooksRemaining > 0)
                        {
                            NextGlance(agent);
                        }
                        else
                        {
                            ChooseCalmActivity(agentIndex, agent, false);
                        }
                    }

                    break;

                case AgentActivityState.Strolling:
                {
                    long distance = Distance(agent.Position, agent.Target);
                    if (distance < StrollArrivalDistance || tick >= agent.ActivityEndTick ||
                        agent.BlockedTicks > CalmBlockedGiveUpTicks)
                    {
                        ChooseCalmActivity(agentIndex, agent, true);
                        break;
                    }

                    if (tick >= agent.NextWanderTick)
                    {
                        agent.WanderOffset = random.NextIntInclusive(-WanderMaximumDegrees, WanderMaximumDegrees);
                        agent.NextWanderTick = checked(tick + random.NextIntInclusive(25, 60));
                    }

                    // Wander less as the destination gets close, so arrival
                    // looks deliberate.
                    int wander = distance < 1200 ? agent.WanderOffset / 2 : agent.WanderOffset;
                    goalHeading = HeadingBetween(agent.Position, agent.Target, agent.Heading) + wander;
                    goalSpeed = distance < StrollSlowdownDistance
                        ? Math.Max(agent.CalmSpeed / 3, (int)(agent.CalmSpeed * distance / StrollSlowdownDistance))
                        : agent.CalmSpeed;
                    steer = true;
                    break;
                }

                case AgentActivityState.Socialising:
                {
                    if (!TryGetPartner(agent, out AgentRuntime partner) ||
                        tick >= agent.ActivityEndTick || agent.BlockedTicks > CalmBlockedGiveUpTicks)
                    {
                        agent.SocialPartnerIndex = -1;
                        ChooseCalmActivity(agentIndex, agent, true);
                        break;
                    }

                    long distance = Distance(agent.Position, partner.Position);
                    if (distance <= SocialStopDistance)
                    {
                        // Arrived: stand and face them for a while.
                        agent.Activity = AgentActivityState.Standing;
                        agent.ActivityEndTick = checked(tick + random.NextIntInclusive(150, 400));
                        break;
                    }

                    goalHeading = HeadingBetween(agent.Position, partner.Position, agent.Heading);
                    goalSpeed = distance < SocialStopDistance + StrollSlowdownDistance
                        ? Math.Max(agent.CalmSpeed / 3, agent.CalmSpeed / 2)
                        : agent.CalmSpeed;
                    steer = true;
                    break;
                }

                default:
                    // Coming back to calm from another state is not possible
                    // in this prototype, but choose afresh if it ever happens.
                    ChooseCalmActivity(agentIndex, agent, false);
                    break;
            }

            if (steer)
            {
                goalHeading = SteerHeading(agentIndex, agent, goalHeading, 100, 150);
            }

            ApplyBody(agent, goalHeading, goalSpeed, agent.CalmTurnRate, scenario.CalmAcceleration);
        }

        private void ChooseCalmActivity(int agentIndex, AgentRuntime agent, bool justMoved)
        {
            agent.BlockedTicks = 0;
            int roll = random.NextIntInclusive(0, 99);
            if (justMoved)
            {
                // After walking somewhere, people usually stop for a moment.
                if (roll < 55)
                {
                    StartStanding(agent);
                }
                else
                {
                    StartLookingAround(agent);
                }

                return;
            }

            agent.SocialPartnerIndex = -1;
            if (roll < 50)
            {
                StartStroll(agent);
            }
            else if (roll < 75)
            {
                if (!TryStartSocialising(agentIndex, agent))
                {
                    StartStroll(agent);
                }
            }
            else if (agent.Activity != AgentActivityState.LookingAround)
            {
                StartLookingAround(agent);
            }
            else
            {
                StartStroll(agent);
            }
        }

        private void StartStanding(AgentRuntime agent)
        {
            agent.Activity = AgentActivityState.Standing;
            agent.ActivityEndTick = checked(tick + random.NextIntInclusive(
                scenario.CalmDecisionMinimumTicks,
                scenario.CalmDecisionMaximumTicks));
        }

        private void StartLookingAround(AgentRuntime agent)
        {
            agent.Activity = AgentActivityState.LookingAround;
            agent.LooksRemaining = random.NextIntInclusive(1, 3);
            NextGlance(agent);
        }

        private void NextGlance(AgentRuntime agent)
        {
            agent.LooksRemaining--;
            int side = random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
            agent.LookHeading = IntegerMath.NormalizeDegrees(agent.Heading + side * random.NextIntInclusive(35, 120));
            agent.ActivityEndTick = checked(tick + random.NextIntInclusive(30, 70));
        }

        private void StartStroll(AgentRuntime agent)
        {
            agent.Activity = AgentActivityState.Strolling;
            agent.ActivityEndTick = checked(tick + StrollTimeoutTicks);
            agent.WanderOffset = random.NextIntInclusive(-WanderMaximumDegrees, WanderMaximumDegrees);
            agent.NextWanderTick = checked(tick + random.NextIntInclusive(25, 60));

            LogicalBounds room = scenario.RoomBounds;
            long minimumSquared = (long)StrollMinimumDistance * StrollMinimumDistance;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                agent.Target = new LogicalPosition(
                    random.NextIntInclusive(room.MinX + StrollWallMargin, room.MaxX - StrollWallMargin),
                    random.NextIntInclusive(room.MinZ + StrollWallMargin, room.MaxZ - StrollWallMargin));
                if (LogicalPosition.DistanceSquared(agent.Position, agent.Target) >= minimumSquared)
                {
                    return;
                }
            }
        }

        private bool TryStartSocialising(int agentIndex, AgentRuntime agent)
        {
            long minimumSquared = (long)SocialMinimumDistance * SocialMinimumDistance;
            long maximumSquared = (long)SocialMaximumDistance * SocialMaximumDistance;
            int candidateCount = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (IsSocialCandidate(agentIndex, agent, i, minimumSquared, maximumSquared))
                {
                    candidateCount++;
                }
            }

            if (candidateCount == 0)
            {
                return false;
            }

            int pick = random.NextIntInclusive(0, candidateCount - 1);
            for (int i = 0; i < agents.Length; i++)
            {
                if (!IsSocialCandidate(agentIndex, agent, i, minimumSquared, maximumSquared))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    agent.SocialPartnerIndex = i;
                    agent.Activity = AgentActivityState.Socialising;
                    agent.ActivityEndTick = checked(tick + SocialTimeoutTicks);
                    return true;
                }
            }

            return false;
        }

        private bool IsSocialCandidate(
            int agentIndex,
            AgentRuntime agent,
            int otherIndex,
            long minimumSquared,
            long maximumSquared)
        {
            AgentRuntime other = agents[otherIndex];
            if (otherIndex == agentIndex ||
                other.Participation != AgentParticipation.Participating ||
                other.FearState != AgentFearState.Calm)
            {
                return false;
            }

            long distanceSquared = LogicalPosition.DistanceSquared(agent.Position, other.Position);
            return distanceSquared >= minimumSquared && distanceSquared <= maximumSquared;
        }

        private bool TryGetPartner(AgentRuntime agent, out AgentRuntime partner)
        {
            partner = null;
            if (agent.SocialPartnerIndex < 0)
            {
                return false;
            }

            AgentRuntime candidate = agents[agent.SocialPartnerIndex];
            if (candidate.Participation != AgentParticipation.Participating ||
                candidate.FearState != AgentFearState.Calm)
            {
                agent.SocialPartnerIndex = -1;
                return false;
            }

            partner = candidate;
            return true;
        }
    }
}
