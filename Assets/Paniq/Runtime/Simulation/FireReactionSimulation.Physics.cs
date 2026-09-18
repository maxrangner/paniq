using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Physical objects: loose things on the floor that slide when pushed.
    /// This is the simulation's own whole-number physics, not Unity's, so a
    /// box that trips someone does so identically in every replay. Objects
    /// have round footprints; boxes are the only kind so far.
    ///
    /// People bump objects during their decisions (resolved with the other
    /// collisions), then every moving object slides along its velocity,
    /// stops at the first thing it meets and bounces off it, and slows with
    /// floor friction. Nothing ever overlaps.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        /// <summary>Object positions and velocities are kept in hundredths of a millimetre.</summary>
        private const int SubMillimetre = 100;

        private const int ObjectAvoidMargin = 400;
        private const int ObjectSpinMaximum = 20;
        private const int ObjectBumpLoggedSpeed = 20;
        private const int ContactSearchSteps = 1024;

        private sealed class PhysicsBody
        {
            public StableAgentId Id;
            public PhysicsObjectKind Kind;
            public long X;
            public long Z;
            public long VelocityX;
            public long VelocityZ;
            public int Radius;
            public int Size;
            public int MassGrams;
            public int Heading;
            public int Spin;

            /// <summary>The event that last set this object moving, so its later hits can name their cause.</summary>
            public ulong LastPushEventId;

            public LogicalPosition Position => new LogicalPosition(
                (int)FloorDivide(X, SubMillimetre),
                (int)FloorDivide(Z, SubMillimetre));
        }

        private readonly List<ObjectContact> objectContacts = new List<ObjectContact>();
        private PhysicsBody[] bodies;

        public int PhysicsObjectCount => bodies.Length;

        public FireReactionPhysicsObjectSnapshot GetPhysicsObject(int index) => ToSnapshot(bodies[index]);

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        internal void LaunchObjectForTests(int index, int velocityX, int velocityZ)
        {
            bodies[index].VelocityX = (long)velocityX * SubMillimetre;
            bodies[index].VelocityZ = (long)velocityZ * SubMillimetre;
        }

        private void InitializePhysicsObjects()
        {
            var definitions = (FireReactionPhysicsObjectDefinition[])scenario.PhysicsObjects.Clone();
            Array.Sort(definitions, (left, right) => left.ObjectId.CompareTo(right.ObjectId));
            bodies = new PhysicsBody[definitions.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                FireReactionPhysicsObjectDefinition definition = definitions[i];
                bodies[i] = new PhysicsBody
                {
                    Id = definition.ObjectId,
                    Kind = definition.Kind,
                    X = (long)definition.InitialPosition.X * SubMillimetre,
                    Z = (long)definition.InitialPosition.Z * SubMillimetre,
                    Radius = definition.RadiusMillimetres,
                    Size = definition.SizeMillimetres,
                    MassGrams = definition.MassGrams
                };
            }
        }

        /// <summary>The lowest-index object a circle of this radius would pass through on this move, or -1.</summary>
        private int FindBlockingObject(LogicalPosition start, LogicalPosition destination, int radius, int ignoreIndex = -1)
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                if (b == ignoreIndex)
                {
                    continue;
                }

                LogicalPosition centre = bodies[b].Position;
                long reach = (long)radius + bodies[b].Radius;
                if (IntegerMath.SegmentPassesWithin(start, destination, centre, reach * reach))
                {
                    return b;
                }
            }

            return -1;
        }

        /// <summary>A push away from nearby objects, so people walk around boxes rather than into them.</summary>
        private void AddObjectAvoidance(AgentRuntime agent, int percent, ref long steerX, ref long steerZ)
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                LogicalPosition centre = bodies[b].Position;
                long range = (long)scenario.OccupancyRadiusMillimetres + bodies[b].Radius + ObjectAvoidMargin;
                long dx = (long)agent.Position.X - centre.X;
                long dz = (long)agent.Position.Z - centre.Z;
                long distanceSquared = dx * dx + dz * dz;
                if (distanceSquared == 0L || distanceSquared >= range * range)
                {
                    continue;
                }

                long distance = IntegerMath.Sqrt(distanceSquared);
                long strength = IntegerMath.TrigScale * (range - distance) / range * percent / 100L;
                steerX += dx * strength / distance;
                steerZ += dz * strength / distance;
            }
        }

        /// <summary>
        /// Records a person walking or running into an object. With
        /// <paramref name="fastOnly"/> only a runner too fast to dodge counts;
        /// otherwise it is someone with no side-step left who pushes it.
        /// </summary>
        private bool TryRecordObjectContact(int agentIndex, AgentRuntime agent, LogicalPosition straight, bool fastOnly)
        {
            if (IsZero(straight) || agent.BodyState != AgentBodyState.Upright)
            {
                return false;
            }

            LogicalPosition destination = agent.Position + straight;
            if (FindBlockingAgent(agentIndex, agent.Position, destination) >= 0)
            {
                return false;
            }

            int bodyIndex = FindBlockingObject(agent.Position, destination, scenario.OccupancyRadiusMillimetres);
            if (bodyIndex < 0)
            {
                return false;
            }

            int closing = ClosingSpeedToObject(agent, bodies[bodyIndex]);
            if (closing <= 0 || (fastOnly && closing < scenario.BumpMinimumSpeed))
            {
                return false;
            }

            objectContacts.Add(new ObjectContact(agentIndex, bodyIndex, closing));
            return true;
        }

        /// <summary>How fast a person and an object approach along the line between them, in mm per tick.</summary>
        private static int ClosingSpeedToObject(AgentRuntime agent, PhysicsBody body)
        {
            LogicalPosition centre = body.Position;
            long nx = (long)centre.X - agent.Position.X;
            long nz = (long)centre.Z - agent.Position.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return agent.Speed;
            }

            LogicalPosition direction = IntegerMath.Direction(agent.Heading);
            long agentAlong = agent.Speed * (direction.X * nx + direction.Z * nz) / (IntegerMath.TrigScale * length);
            long bodyAlong = (body.VelocityX * nx + body.VelocityZ * nz) / (SubMillimetre * length);
            return (int)(agentAlong - bodyAlong);
        }

        /// <summary>
        /// People into objects, in the order recorded. The object is shoved
        /// along the line between them, as a 70 kg person running into it
        /// would; a runner may trip over it instead, the more likely the
        /// faster they are and the bigger it is.
        /// </summary>
        private void ResolveObjectContacts()
        {
            for (int c = 0; c < objectContacts.Count; c++)
            {
                ObjectContact contact = objectContacts[c];
                AgentRuntime agent = agents[contact.AgentIndex];
                PhysicsBody body = bodies[contact.BodyIndex];
                if (agent.Participation != AgentParticipation.Participating ||
                    agent.BodyState != AgentBodyState.Upright)
                {
                    continue;
                }

                LogicalPosition centre = body.Position;
                long nx = (long)centre.X - agent.Position.X;
                long nz = (long)centre.Z - agent.Position.Z;
                long length = IntegerMath.Sqrt(nx * nx + nz * nz);
                if (length == 0L)
                {
                    continue;
                }

                int closing = contact.ClosingSpeed;
                var point = new LogicalPosition(
                    (int)(agent.Position.X + nx * scenario.OccupancyRadiusMillimetres / length),
                    (int)(agent.Position.Z + nz * scenario.OccupancyRadiusMillimetres / length));

                ulong bumpEventId = 0UL;
                if (agent.FearState == AgentFearState.Scared)
                {
                    bumpEventId = eventLog.Append(tick, agent.Id, FireReactionEventType.BoxBumped, point,
                        closing, 0, agent.ScaredEventId).EventId;
                }

                bool trip = false;
                if (bumpEventId != 0UL && closing >= scenario.ObjectTripMinimumSpeed)
                {
                    long chance = (long)(closing - scenario.ObjectTripMinimumSpeed + 10) * body.Size / scenario.ObjectTripScale;
                    trip = random.NextPercent((int)Math.Min(scenario.ObjectTripMaximumChancePercent, chance));
                }

                long agentMass = scenario.AgentMassGrams;
                long total = agentMass + body.MassGrams;
                long restitution = 100L + scenario.ObjectAgentRestitutionPercent;
                long push = restitution * agentMass * closing / total;
                if (trip)
                {
                    push /= 2;
                }

                body.VelocityX += push * nx / length;
                body.VelocityZ += push * nz / length;
                body.LastPushEventId = bumpEventId;

                LogicalPosition direction = IntegerMath.Direction(agent.Heading);
                long sideways = agent.Speed * (direction.X * nz - direction.Z * nx) / (IntegerMath.TrigScale * length);
                body.Spin = (int)Math.Max(-ObjectSpinMaximum, Math.Min(ObjectSpinMaximum, body.Spin + sideways / 3));

                agent.Speed = (int)Math.Max(0L, agent.Speed - restitution * body.MassGrams * closing / (100L * total));

                if (trip)
                {
                    Trip(agent, bumpEventId);
                }
                else if (bumpEventId != 0UL && closing >= scenario.BumpMinimumSpeed)
                {
                    EmitSound(agent.Id, point, scenario.BumpSoundRadiusMillimetres, 0, bumpEventId);
                }
            }
        }

        /// <summary>
        /// Phase 8: every moving object, in ascending ID order, slides along
        /// its velocity, stops at the first person or object in the way and
        /// bounces off it (or the walls), then slows with floor friction.
        /// </summary>
        private void AdvancePhysicsObjects()
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody body = bodies[b];
                if (body.Spin != 0)
                {
                    body.Heading = IntegerMath.NormalizeDegrees(body.Heading + body.Spin);
                    body.Spin -= Math.Sign(body.Spin);
                }

                if (body.VelocityX == 0L && body.VelocityZ == 0L)
                {
                    continue;
                }

                LimitSpeed(body, (long)scenario.MaximumStepDistanceMillimetres * SubMillimetre);
                long nextX = body.X + body.VelocityX;
                long nextZ = body.Z + body.VelocityZ;
                BounceOffWalls(body, ref nextX, ref nextZ);

                LogicalPosition from = body.Position;
                var to = new LogicalPosition((int)FloorDivide(nextX, SubMillimetre), (int)FloorDivide(nextZ, SubMillimetre));
                if (!IsObjectPathBlocked(b, from, to, out _, out _))
                {
                    body.X = nextX;
                    body.Z = nextZ;
                }
                else
                {
                    // Slide up to the last clear point, then bounce off what is in the way.
                    int clear = 0;
                    int blocked = ContactSearchSteps;
                    while (blocked - clear > 1)
                    {
                        int middle = (clear + blocked) / 2;
                        if (IsObjectPathBlocked(b, from, Lerp(from, to, middle), out _, out _))
                        {
                            blocked = middle;
                        }
                        else
                        {
                            clear = middle;
                        }
                    }

                    LogicalPosition stop = Lerp(from, to, clear);
                    body.X = (long)stop.X * SubMillimetre;
                    body.Z = (long)stop.Z * SubMillimetre;
                    IsObjectPathBlocked(b, from, Lerp(from, to, blocked), out int agentIndex, out int otherIndex);
                    if (agentIndex >= 0)
                    {
                        HitAgent(body, agents[agentIndex]);
                    }
                    else if (otherIndex >= 0)
                    {
                        HitObject(body, bodies[otherIndex]);
                    }
                }

                ApplyFriction(body);
            }
        }

        private void BounceOffWalls(PhysicsBody body, ref long nextX, ref long nextZ)
        {
            LogicalBounds room = scenario.RoomBounds;
            long minX = (long)(room.MinX + body.Radius) * SubMillimetre;
            long maxX = (long)(room.MaxX - body.Radius) * SubMillimetre;
            long minZ = (long)(room.MinZ + body.Radius) * SubMillimetre;
            long maxZ = (long)(room.MaxZ - body.Radius) * SubMillimetre;
            int restitution = scenario.ObjectWallRestitutionPercent;
            if (nextX < minX || nextX > maxX)
            {
                nextX = Math.Max(minX, Math.Min(maxX, nextX));
                body.VelocityX = -body.VelocityX * restitution / 100L;
            }

            if (nextZ < minZ || nextZ > maxZ)
            {
                nextZ = Math.Max(minZ, Math.Min(maxZ, nextZ));
                body.VelocityZ = -body.VelocityZ * restitution / 100L;
            }
        }

        /// <summary>The first participating person, or else object, this object's move would pass through.</summary>
        private bool IsObjectPathBlocked(int bodyIndex, LogicalPosition from, LogicalPosition to, out int agentIndex, out int otherIndex)
        {
            PhysicsBody body = bodies[bodyIndex];
            agentIndex = -1;
            otherIndex = -1;
            long agentReach = (long)body.Radius + scenario.OccupancyRadiusMillimetres;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].Participation == AgentParticipation.Participating &&
                    IntegerMath.SegmentPassesWithin(from, to, agents[i].Position, agentReach * agentReach))
                {
                    agentIndex = i;
                    return true;
                }
            }

            otherIndex = FindBlockingObject(from, to, body.Radius, bodyIndex);
            return otherIndex >= 0;
        }

        /// <summary>
        /// An object running into a person bounces back. A heavy, fast one
        /// knocks them off balance or trips them; a light or slow one does
        /// nothing to them.
        /// </summary>
        private void HitAgent(PhysicsBody body, AgentRuntime agent)
        {
            LogicalPosition centre = body.Position;
            long nx = (long)agent.Position.X - centre.X;
            long nz = (long)agent.Position.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            LogicalPosition direction = IntegerMath.Direction(agent.Heading);
            long agentVelocityX = (long)direction.X * agent.Speed * SubMillimetre / IntegerMath.TrigScale;
            long agentVelocityZ = (long)direction.Z * agent.Speed * SubMillimetre / IntegerMath.TrigScale;
            long closing = ((body.VelocityX - agentVelocityX) * nx + (body.VelocityZ - agentVelocityZ) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            long agentMass = scenario.AgentMassGrams;
            long bounce = (100L + scenario.ObjectAgentRestitutionPercent) * agentMass * closing /
                          (100L * (agentMass + body.MassGrams));
            body.VelocityX -= bounce * nx / length;
            body.VelocityZ -= bounce * nz / length;

            // Kilograms times millimetres per tick.
            long momentum = body.MassGrams * closing / (1000L * SubMillimetre);
            if (agent.BodyState != AgentBodyState.Upright || momentum < scenario.ObjectStaggerMomentum)
            {
                return;
            }

            var point = new LogicalPosition(
                (int)(centre.X + nx * body.Radius / length),
                (int)(centre.Z + nz * body.Radius / length));
            CausalEvent hit = eventLog.Append(tick, body.Id, FireReactionEventType.BoxHitAgent, point,
                (int)(closing / SubMillimetre), 0, body.LastPushEventId);
            bool wasCalm = agent.FearState == AgentFearState.Calm;
            if (momentum >= scenario.ObjectKnockdownMomentum)
            {
                Trip(agent, hit.EventId);
            }
            else
            {
                Stagger(agent, hit.EventId);
                EmitSound(body.Id, point, scenario.BumpSoundRadiusMillimetres, 0, hit.EventId);
            }

            if (wasCalm && agent.FearState == AgentFearState.Calm)
            {
                StartAlert(agent, hit.EventId, AgentAlertSource.Bumped);
                agent.SoundPoint = point;
                agent.HasSoundPoint = true;
            }
        }

        /// <summary>Two objects meet: they push each other apart by their masses, a little bouncily.</summary>
        private void HitObject(PhysicsBody body, PhysicsBody other)
        {
            LogicalPosition centre = body.Position;
            LogicalPosition otherCentre = other.Position;
            long nx = (long)otherCentre.X - centre.X;
            long nz = (long)otherCentre.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            long closing = ((body.VelocityX - other.VelocityX) * nx + (body.VelocityZ - other.VelocityZ) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            long total = (long)body.MassGrams + other.MassGrams;
            long restitution = 100L + scenario.ObjectObjectRestitutionPercent;
            long slowed = restitution * other.MassGrams * closing / (100L * total);
            long shoved = restitution * body.MassGrams * closing / (100L * total);
            body.VelocityX -= slowed * nx / length;
            body.VelocityZ -= slowed * nz / length;
            other.VelocityX += shoved * nx / length;
            other.VelocityZ += shoved * nz / length;

            if (body.LastPushEventId != 0UL && closing >= (long)ObjectBumpLoggedSpeed * SubMillimetre)
            {
                var point = new LogicalPosition(
                    (int)(centre.X + nx * body.Radius / length),
                    (int)(centre.Z + nz * body.Radius / length));
                other.LastPushEventId = eventLog.Append(tick, body.Id, FireReactionEventType.BoxesCollided, point,
                    (int)(closing / SubMillimetre), 0, body.LastPushEventId).EventId;
            }
            else if (body.LastPushEventId != 0UL)
            {
                other.LastPushEventId = body.LastPushEventId;
            }
        }

        private void ApplyFriction(PhysicsBody body)
        {
            long speed = IntegerMath.Sqrt(body.VelocityX * body.VelocityX + body.VelocityZ * body.VelocityZ);
            if (speed <= scenario.ObjectFriction)
            {
                body.VelocityX = 0L;
                body.VelocityZ = 0L;
                return;
            }

            body.VelocityX = body.VelocityX * (speed - scenario.ObjectFriction) / speed;
            body.VelocityZ = body.VelocityZ * (speed - scenario.ObjectFriction) / speed;
        }

        private static void LimitSpeed(PhysicsBody body, long maximum)
        {
            long speed = IntegerMath.Sqrt(body.VelocityX * body.VelocityX + body.VelocityZ * body.VelocityZ);
            if (speed > maximum)
            {
                body.VelocityX = body.VelocityX * maximum / speed;
                body.VelocityZ = body.VelocityZ * maximum / speed;
            }
        }

        private static LogicalPosition Lerp(LogicalPosition from, LogicalPosition to, int step)
        {
            return new LogicalPosition(
                (int)(from.X + ((long)to.X - from.X) * step / ContactSearchSteps),
                (int)(from.Z + ((long)to.Z - from.Z) * step / ContactSearchSteps));
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && (value < 0L) != (divisor < 0L) ? quotient - 1L : quotient;
        }

        private static FireReactionPhysicsObjectSnapshot ToSnapshot(PhysicsBody body)
        {
            long speed = IntegerMath.Sqrt(body.VelocityX * body.VelocityX + body.VelocityZ * body.VelocityZ);
            return new FireReactionPhysicsObjectSnapshot(
                body.Id,
                body.Kind,
                body.Position,
                body.Size,
                body.Heading,
                (int)(speed / SubMillimetre));
        }

        private FireReactionPhysicsObjectSnapshot[] GetPhysicsObjectSnapshots()
        {
            var snapshots = new FireReactionPhysicsObjectSnapshot[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                snapshots[i] = ToSnapshot(bodies[i]);
            }

            return snapshots;
        }

        private readonly struct ObjectContact
        {
            public ObjectContact(int agentIndex, int bodyIndex, int closingSpeed)
            {
                AgentIndex = agentIndex;
                BodyIndex = bodyIndex;
                ClosingSpeed = closingSpeed;
            }

            public int AgentIndex { get; }
            public int BodyIndex { get; }
            public int ClosingSpeed { get; }
        }
    }
}
