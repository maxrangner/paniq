using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Sticking together (the owner's card, 2026-09-25). Everybody a
    /// "Stick together" throw catches becomes one group: once frightened,
    /// each of them is pulled toward the others, so the ones ahead hang back
    /// for the ones behind and the knot crosses the building as a knot rather
    /// than a string; they go for the door the most leaderly of them goes for;
    /// and they tell each other what they know of the way out, so a visitor
    /// bound to somebody who works here learns it.
    /// <para>
    /// It is not a leash. The pull is by personality: the nervous feel it
    /// most, the brave least, and the cruel not at all, as they ignore a
    /// leader. Nobody waits with flames at their back (the panic decision
    /// skips the pull in danger), the frozen and the burning are out of it,
    /// and nobody answers on the tick the card lands: each member's pull
    /// begins a few ticks late, like every reaction.
    /// </para>
    /// </summary>
    internal sealed class GroupSystem
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WayfindingSystem wayfinding;
        private readonly GroupSettings settings;
        private int groupCount;

        public GroupSystem(SimulationContext context, Crowd crowd, WayfindingSystem wayfinding)
        {
            this.context = context;
            this.crowd = crowd;
            this.wayfinding = wayfinding;
            settings = context.Scenario.Groups;
        }

        /// <summary>How many groups have been formed this run.</summary>
        public int Count => groupCount;

        /// <summary>
        /// Everybody in <paramref name="members"/> becomes one group, each
        /// with their own event as the cause of whatever they learn from the
        /// others. Their pull begins at their own reaction tick.
        /// </summary>
        public int Form(List<int> members, List<ulong> causes)
        {
            int group = groupCount++;
            Agent[] all = crowd.All;
            for (int i = 0; i < members.Count; i++)
            {
                Agent agent = all[members[i]];
                agent.Group.GroupId = group;
                agent.Group.CauseEventId = causes[i];
                agent.Group.FromTick = context.ReactionTick();
                agent.Group.NextShareTick = agent.Group.FromTick;
            }

            return group;
        }

        /// <summary>Bound to a group, and not too cruel to care.</summary>
        public bool IsBound(Agent agent) => agent.Group.GroupId >= 0 && agent.Traits.Evil < settings.IgnoreMinimumEvil;

        /// <summary>How hard this person keeps to the group, nought to a hundred: the nervous most, the brave least, the cruel less again.</summary>
        public int CohesionPercent(Agent agent)
        {
            int percent = settings.CohesionBasePercent +
                          settings.CohesionPercentPerNervousness * agent.Traits.Nervousness -
                          settings.CohesionPercentPerBravery * agent.Traits.Bravery -
                          settings.CohesionPercentPerEvil * agent.Traits.Evil;
            return percent < 0 ? 0 : percent > 100 ? 100 : percent;
        }

        /// <summary>
        /// The door the group's anchor -- the member with the most leadership
        /// -- is running for, or -1 for the anchor themselves and for anybody
        /// in no group. Scored as a bonus in the choice of door, so the knot
        /// picks one way unless fire or personality says otherwise.
        /// </summary>
        public int AnchorExitDoor(Agent agent)
        {
            if (!IsBound(agent))
            {
                return -1;
            }

            Agent anchor = AnchorOf(agent.Group.GroupId);
            return anchor == null || anchor == agent ? -1 : anchor.Doors.ExitDoorIndex;
        }

        private Agent AnchorOf(int group)
        {
            Agent best = null;
            Agent[] all = crowd.All;
            for (int i = 0; i < all.Length; i++)
            {
                Agent agent = all[i];
                if (agent.Group.GroupId != group || !agent.IsParticipating || !IsBound(agent))
                {
                    continue;
                }

                if (best == null || agent.Traits.Leadership > best.Traits.Leadership)
                {
                    best = agent;
                }
            }

            return best;
        }

        /// <summary>
        /// The pull toward the rest of the group, added to a frightened
        /// person's steering: a unit vector toward the middle of the other
        /// members within reach, scaled by how much they care. Nothing when
        /// they are already close, when the others are too far off to be
        /// found, or before their own reaction tick. The same call is the
        /// moment they compare notes on the way out, every so often.
        /// <para>
        /// Returns the pace to run at, as a percentage: a hundred, or less
        /// for somebody with the rest of the group behind them, who slows to
        /// let them catch up. Steering only turns a person, and waiting is a
        /// matter of pace.
        /// </para>
        /// </summary>
        public int PullToward(Agent agent, int goalHeading, ref long extraX, ref long extraZ)
        {
            if (!IsBound(agent) || context.Tick < agent.Group.FromTick || !agent.Body.IsOnTheirFeet)
            {
                return 100;
            }

            int cohesion = CohesionPercent(agent);
            if (cohesion <= 0)
            {
                return 100;
            }

            bool share = context.Tick >= agent.Group.NextShareTick;
            long reach = settings.ReachMillimetres;
            long sumX = 0L;
            long sumZ = 0L;
            long nearestSquared = long.MaxValue;
            int count = 0;
            Agent[] all = crowd.All;
            for (int i = 0; i < all.Length; i++)
            {
                Agent other = all[i];
                long apartSquared = LogicalPosition.DistanceSquared(agent.Body.Position, other.Body.Position);
                if (other == agent || other.Group.GroupId != agent.Group.GroupId || !other.IsParticipating ||
                    apartSquared > reach * reach)
                {
                    continue;
                }

                if (share)
                {
                    // Whatever they know of the way out, this one knows now.
                    wayfinding.Share(other, agent, agent.Group.CauseEventId);
                }

                sumX += other.Body.Position.X;
                sumZ += other.Body.Position.Z;
                nearestSquared = apartSquared < nearestSquared ? apartSquared : nearestSquared;
                count++;
            }

            if (share)
            {
                agent.Group.NextShareTick = checked(context.Tick + context.Jittered(settings.ShareEveryTicks));
            }

            if (count == 0)
            {
                return 100;
            }

            long dx = sumX / count - agent.Body.Position.X;
            long dz = sumZ / count - agent.Body.Position.Z;
            long distance = IntegerMath.Sqrt(dx * dx + dz * dz);
            long close = settings.CloseEnoughMillimetres;
            if (distance <= close)
            {
                return 100;
            }

            // The pull comes on gradually as they part, and not at all while
            // somebody is at their elbow: a full pull at running speed steered
            // members straight into one another, and a hard collision puts
            // both on the floor.
            long elbow = settings.ElbowRoomMillimetres;
            if (nearestSquared > elbow * elbow)
            {
                long strength = System.Math.Min(IntegerMath.TrigScale, (distance - close) * IntegerMath.TrigScale / settings.PullRampMillimetres);
                extraX += dx * strength / distance * cohesion / 100L;
                extraZ += dz * strength / distance * cohesion / 100L;
            }

            // The rest of them behind: hang back for them.
            LogicalPosition ahead = IntegerMath.Direction(goalHeading);
            bool behind = dx * ahead.X + dz * ahead.Z < 0L;
            return behind ? 100 - cohesion * settings.HangBackPercent / 100 : 100;
        }
    }
}
