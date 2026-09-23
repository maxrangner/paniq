using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Every threat in the run at once, answering the crowd's questions across
    /// all of them: the nearest bit of any, any in this room, any in sight.
    /// The order threats were added in is the order they are asked in and the
    /// order ties are broken in, so a replay agrees with itself.
    /// <para>
    /// With one threat in it -- the fire, which is every run today -- every
    /// answer is exactly the fire's own, so adding this changed no replay.
    /// </para>
    /// </summary>
    internal sealed class Threats
    {
        private readonly List<IThreat> all = new List<IThreat>();

        public Threats(params IThreat[] threats)
        {
            all.AddRange(threats);
        }

        /// <summary>A threat that is not part of any scenario yet, for a test that wants to frighten people with something other than fire.</summary>
        internal void AddForTests(IThreat threat) => all.Add(threat);

        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    count += all[i].Active ? all[i].Count : 0;
                }

                return count;
            }
        }

        public bool AnyActive
        {
            get
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Active)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool StartRequested
        {
            get
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].StartRequested)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The player's trigger sets every threat in the level going.</summary>
        public void RequestStart()
        {
            for (int i = 0; i < all.Count; i++)
            {
                all[i].RequestStart();
            }
        }

        /// <summary>The root cause of the disaster: the first threat's start event, or 0 before anything has started.</summary>
        public ulong RootEventId
        {
            get
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].RootEventId != 0UL)
                    {
                        return all[i].RootEventId;
                    }
                }

                return 0UL;
            }
        }

        /// <summary>Changes whenever any threat changes.</summary>
        public long Signature
        {
            get
            {
                long signature = 0L;
                for (int i = 0; i < all.Count; i++)
                {
                    signature = unchecked(signature * 1000003L + all[i].Signature);
                }

                return signature;
            }
        }

        /// <summary>Phase 2: every threat advances, in order.</summary>
        public void Advance()
        {
            for (int i = 0; i < all.Count; i++)
            {
                all[i].Advance();
            }
        }

        /// <summary>The nearest point of any threat; ties go to the threat added first.</summary>
        public long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId)
        {
            long nearest = long.MaxValue;
            point = from;
            causeEventId = 0UL;
            for (int i = 0; i < all.Count; i++)
            {
                long distance = all[i].NearestDistanceSquared(from, out LogicalPosition candidate, out ulong cause);
                if (distance < nearest)
                {
                    nearest = distance;
                    point = candidate;
                    causeEventId = cause;
                }
            }

            return nearest;
        }

        public bool AnyCloserThan(LogicalPosition position, int distance)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].AnyCloserThan(position, distance))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsInRoom(int room)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsInRoom(room))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether any threat is in sight, and which one's root event to blame for the fright.</summary>
        public bool IsVisibleFrom(LogicalPosition eye, int heading, int range, out ulong rootEventId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsVisibleFrom(eye, heading, range))
                {
                    rootEventId = all[i].RootEventId;
                    return true;
                }
            }

            rootEventId = 0UL;
            return false;
        }

        public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].RoutePassesNear(from, to, clearance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a calm person here hears a threat close by: the first that
        /// is within its own hearing reach, with the point to turn toward and
        /// the event that made the noise.
        /// </summary>
        public bool HeardNearby(LogicalPosition position, out LogicalPosition point, out ulong causeEventId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                long reach = all[i].HeardWithinMillimetres;
                if (reach <= 0L)
                {
                    continue;
                }

                long distance = all[i].NearestDistanceSquared(position, out point, out causeEventId);
                if (distance <= reach * reach)
                {
                    return true;
                }
            }

            point = position;
            causeEventId = 0UL;
            return false;
        }

        /// <summary>Phase 3: whatever this person is standing in gets them, threat by threat.</summary>
        public void ResolveContact(Agent agent, BodySystem body)
        {
            for (int i = 0; i < all.Count; i++)
            {
                IThreat threat = all[i];
                if (!threat.Active)
                {
                    continue;
                }

                ulong cause = threat.Touching(agent.Body.Position);
                if (cause != 0UL)
                {
                    threat.Harm(agent, cause, body);
                }
            }
        }

        /// <summary>After the step: whatever this person's move carried them through gets them.</summary>
        public void ResolveContactAlong(Agent agent, LogicalPosition from, LogicalPosition to, BodySystem body)
        {
            for (int i = 0; i < all.Count; i++)
            {
                IThreat threat = all[i];
                if (!threat.Active)
                {
                    continue;
                }

                ulong cause = threat.TouchingAlong(from, to);
                if (cause != 0UL)
                {
                    threat.Harm(agent, cause, body);
                }
            }
        }
    }
}
