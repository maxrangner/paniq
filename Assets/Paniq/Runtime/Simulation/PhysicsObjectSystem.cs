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
    internal sealed class PhysicsObjectSystem
    {
        /// <summary>Object positions and velocities are kept in hundredths of a millimetre.</summary>
        private const int SubMillimetre = 100;

        /// <summary>Resolution of the halving search for where a sliding object first touches something.</summary>
        private const int ContactSearchSteps = 1024;

        private sealed class PhysicsBody
        {
            public SimulationId Id;
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

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly BodySystem body;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly ObjectPhysicsSettings settings;
        private readonly int personRadius;
        private readonly List<ObjectContact> contacts = new List<ObjectContact>();
        private readonly PhysicsBody[] bodies;

        public PhysicsObjectSystem(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            BodySystem body,
            FearSystem fear,
            SoundSystem sound)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.body = body;
            this.fear = fear;
            this.sound = sound;
            settings = context.Scenario.ObjectPhysics;
            personRadius = context.Scenario.World.OccupancyRadiusMillimetres;

            var definitions = (FireReactionPhysicsObjectDefinition[])context.Scenario.PhysicsObjects.Clone();
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

        public int Count => bodies.Length;

        public SimulationId IdOf(int index) => bodies[index].Id;

        public PhysicsObjectKind KindOf(int index) => bodies[index].Kind;

        public LogicalPosition PositionOf(int index) => bodies[index].Position;

        public int RadiusOf(int index) => bodies[index].Radius;

        public bool IsMoving(int index) => bodies[index].VelocityX != 0L || bodies[index].VelocityZ != 0L;

        public void BeginTick()
        {
            contacts.Clear();
        }

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        public void Launch(int index, int velocityX, int velocityZ)
        {
            bodies[index].VelocityX = (long)velocityX * SubMillimetre;
            bodies[index].VelocityZ = (long)velocityZ * SubMillimetre;
        }

        /// <summary>The lowest-ID object a circle of this radius would pass through on this move, or -1.</summary>
        public int FindBlocking(LogicalPosition start, LogicalPosition destination, int radius, int ignoreIndex = -1)
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
        public void AddAvoidance(LogicalPosition position, int percent, ref long steerX, ref long steerZ)
        {
            int margin = context.Scenario.Steering.ObjectAvoidMarginMillimetres;
            for (int b = 0; b < bodies.Length; b++)
            {
                LogicalPosition centre = bodies[b].Position;
                long range = (long)personRadius + bodies[b].Radius + margin;
                long dx = (long)position.X - centre.X;
                long dz = (long)position.Z - centre.Z;
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
        public bool TryRecordContact(Agent agent, LogicalPosition straight, bool fastOnly)
        {
            if ((straight.X == 0 && straight.Z == 0) || agent.Body.State != AgentBodyState.Upright)
            {
                return false;
            }

            LogicalPosition destination = agent.Body.Position + straight;
            if (crowd.FindBlocking(agent, agent.Body.Position, destination) != null)
            {
                return false;
            }

            int bodyIndex = FindBlocking(agent.Body.Position, destination, personRadius);
            if (bodyIndex < 0)
            {
                return false;
            }

            int closing = ClosingSpeed(agent, bodies[bodyIndex]);
            if (closing <= 0 || (fastOnly && closing < context.Scenario.Falls.BumpMinimumSpeed))
            {
                return false;
            }

            contacts.Add(new ObjectContact(agent, bodyIndex, closing));
            return true;
        }

        /// <summary>How fast a person and an object approach along the line between them, in mm per tick.</summary>
        private static int ClosingSpeed(Agent agent, PhysicsBody physicsBody)
        {
            LogicalPosition centre = physicsBody.Position;
            long nx = (long)centre.X - agent.Body.Position.X;
            long nz = (long)centre.Z - agent.Body.Position.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return agent.Body.Speed;
            }

            LogicalPosition direction = IntegerMath.Direction(agent.Body.Heading);
            long agentAlong = agent.Body.Speed * (direction.X * nx + direction.Z * nz) / (IntegerMath.TrigScale * length);
            long bodyAlong = (physicsBody.VelocityX * nx + physicsBody.VelocityZ * nz) / (SubMillimetre * length);
            return (int)(agentAlong - bodyAlong);
        }

        /// <summary>
        /// Phase 7, people into objects, in the order recorded. The object is
        /// shoved along the line between them, as a person running into it
        /// would; a runner may trip over it instead, the more likely the
        /// faster they are and the bigger it is.
        /// </summary>
        public void ResolveContacts()
        {
            for (int c = 0; c < contacts.Count; c++)
            {
                ObjectContact contact = contacts[c];
                Agent agent = contact.Agent;
                PhysicsBody physicsBody = bodies[contact.BodyIndex];
                if (!agent.IsParticipating || agent.Body.State != AgentBodyState.Upright)
                {
                    continue;
                }

                LogicalPosition centre = physicsBody.Position;
                long nx = (long)centre.X - agent.Body.Position.X;
                long nz = (long)centre.Z - agent.Body.Position.Z;
                long length = IntegerMath.Sqrt(nx * nx + nz * nz);
                if (length == 0L)
                {
                    continue;
                }

                int closing = contact.ClosingSpeed;
                var point = new LogicalPosition(
                    (int)(agent.Body.Position.X + nx * personRadius / length),
                    (int)(agent.Body.Position.Z + nz * personRadius / length));

                ulong bumpEventId = 0UL;
                if (agent.Fear.State == AgentFearState.Scared)
                {
                    bumpEventId = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.BoxBumped, point,
                        closing, 0, agent.Fear.ScaredEventId, physicsBody.Id).EventId;
                }

                bool trip = false;
                if (bumpEventId != 0UL && closing >= settings.TripMinimumSpeed)
                {
                    long chance = (long)(closing - settings.TripMinimumSpeed + 10) * physicsBody.Size / settings.TripScale;
                    trip = context.Random.NextPercent((int)Math.Min(settings.TripMaximumChancePercent, chance));
                }

                long agentMass = TraitEffects.PushMassGrams(agent, context.Scenario);
                long total = agentMass + physicsBody.MassGrams;
                long restitution = 100L + settings.AgentRestitutionPercent;
                long push = restitution * agentMass * closing / total;
                if (trip)
                {
                    push /= 2;
                }

                physicsBody.VelocityX += push * nx / length;
                physicsBody.VelocityZ += push * nz / length;
                physicsBody.LastPushEventId = bumpEventId;

                LogicalPosition direction = IntegerMath.Direction(agent.Body.Heading);
                long sideways = agent.Body.Speed * (direction.X * nz - direction.Z * nx) / (IntegerMath.TrigScale * length);
                physicsBody.Spin = (int)Math.Max(-settings.SpinMaximum, Math.Min(settings.SpinMaximum, physicsBody.Spin + sideways / 3));

                agent.Body.Speed = (int)Math.Max(0L, agent.Body.Speed - restitution * physicsBody.MassGrams * closing / (100L * total));

                if (trip)
                {
                    body.Trip(agent, bumpEventId);
                }
                else if (bumpEventId != 0UL && closing >= context.Scenario.Falls.BumpMinimumSpeed)
                {
                    sound.Thud(agent.Id, point, bumpEventId);
                }
            }
        }

        /// <summary>
        /// Phase 8: every moving object, in ascending ID order, slides along
        /// its velocity, stops at the first person or object in the way and
        /// bounces off it (or the walls), then slows with floor friction.
        /// </summary>
        public void Advance()
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody physicsBody = bodies[b];
                if (physicsBody.Spin != 0)
                {
                    physicsBody.Heading = IntegerMath.NormalizeDegrees(physicsBody.Heading + physicsBody.Spin);
                    physicsBody.Spin -= Math.Sign(physicsBody.Spin);
                }

                if (physicsBody.VelocityX == 0L && physicsBody.VelocityZ == 0L)
                {
                    continue;
                }

                LimitSpeed(physicsBody, (long)context.Scenario.World.MaximumStepDistanceMillimetres * SubMillimetre);
                long nextX = physicsBody.X + physicsBody.VelocityX;
                long nextZ = physicsBody.Z + physicsBody.VelocityZ;
                geometry.KeepObjectInRoom(physicsBody.Radius, SubMillimetre, physicsBody.X, physicsBody.Z,
                    ref nextX, ref nextZ, out bool hitX, out bool hitZ);
                if (hitX)
                {
                    physicsBody.VelocityX = -physicsBody.VelocityX * settings.WallRestitutionPercent / 100L;
                }

                if (hitZ)
                {
                    physicsBody.VelocityZ = -physicsBody.VelocityZ * settings.WallRestitutionPercent / 100L;
                }

                LogicalPosition from = physicsBody.Position;
                var to = new LogicalPosition((int)FloorDivide(nextX, SubMillimetre), (int)FloorDivide(nextZ, SubMillimetre));
                if (!IsPathBlocked(b, from, to, out _, out _))
                {
                    physicsBody.X = nextX;
                    physicsBody.Z = nextZ;
                }
                else
                {
                    // Slide up to the last clear point, then bounce off what is in the way.
                    int clear = 0;
                    int blocked = ContactSearchSteps;
                    while (blocked - clear > 1)
                    {
                        int middle = (clear + blocked) / 2;
                        if (IsPathBlocked(b, from, Lerp(from, to, middle), out _, out _))
                        {
                            blocked = middle;
                        }
                        else
                        {
                            clear = middle;
                        }
                    }

                    LogicalPosition stop = Lerp(from, to, clear);
                    physicsBody.X = (long)stop.X * SubMillimetre;
                    physicsBody.Z = (long)stop.Z * SubMillimetre;
                    IsPathBlocked(b, from, Lerp(from, to, blocked), out Agent agent, out int otherIndex);
                    if (agent != null)
                    {
                        HitAgent(physicsBody, agent);
                    }
                    else if (otherIndex >= 0)
                    {
                        HitObject(physicsBody, bodies[otherIndex]);
                    }
                }

                ApplyFriction(physicsBody);
            }
        }

        /// <summary>The first participating person, or else object, this object's move would pass through.</summary>
        private bool IsPathBlocked(int bodyIndex, LogicalPosition from, LogicalPosition to, out Agent agent, out int otherIndex)
        {
            PhysicsBody physicsBody = bodies[bodyIndex];
            agent = null;
            otherIndex = -1;
            long agentReach = (long)physicsBody.Radius + personRadius;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating &&
                    IntegerMath.SegmentPassesWithin(from, to, agents[i].Body.Position, agentReach * agentReach))
                {
                    agent = agents[i];
                    return true;
                }
            }

            otherIndex = FindBlocking(from, to, physicsBody.Radius, bodyIndex);
            return otherIndex >= 0;
        }

        /// <summary>
        /// An object running into a person bounces back. A heavy, fast one
        /// knocks them off balance or trips them; a light or slow one does
        /// nothing to them.
        /// </summary>
        private void HitAgent(PhysicsBody physicsBody, Agent agent)
        {
            LogicalPosition centre = physicsBody.Position;
            long nx = (long)agent.Body.Position.X - centre.X;
            long nz = (long)agent.Body.Position.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            LogicalPosition direction = IntegerMath.Direction(agent.Body.Heading);
            long agentVelocityX = (long)direction.X * agent.Body.Speed * SubMillimetre / IntegerMath.TrigScale;
            long agentVelocityZ = (long)direction.Z * agent.Body.Speed * SubMillimetre / IntegerMath.TrigScale;
            long closing = ((physicsBody.VelocityX - agentVelocityX) * nx + (physicsBody.VelocityZ - agentVelocityZ) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            long agentMass = TraitEffects.PushMassGrams(agent, context.Scenario);
            long bounce = (100L + settings.AgentRestitutionPercent) * agentMass * closing /
                          (100L * (agentMass + physicsBody.MassGrams));
            physicsBody.VelocityX -= bounce * nx / length;
            physicsBody.VelocityZ -= bounce * nz / length;

            // Kilograms times millimetres per tick.
            long momentum = physicsBody.MassGrams * closing / (1000L * SubMillimetre);
            if (agent.Body.State != AgentBodyState.Upright || momentum < settings.StaggerMomentum)
            {
                return;
            }

            var point = new LogicalPosition(
                (int)(centre.X + nx * physicsBody.Radius / length),
                (int)(centre.Z + nz * physicsBody.Radius / length));
            CausalEvent hit = context.Events.Append(context.Tick, physicsBody.Id, FireReactionEventType.BoxHitAgent, point,
                (int)(closing / SubMillimetre), 0, physicsBody.LastPushEventId, agent.Id);
            bool wasCalm = agent.Fear.State == AgentFearState.Calm;
            if (momentum >= settings.KnockdownMomentum)
            {
                body.Trip(agent, hit.EventId);
                FallSettings falls = context.Scenario.Falls;
                body.MaybePassOut(agent, agent.Body.EventId,
                    falls.PassOutChancePercent + (int)((momentum - settings.KnockdownMomentum) / falls.PassOutMomentumPerPercent));
            }
            else
            {
                body.Stagger(agent, hit.EventId);
                sound.Thud(physicsBody.Id, point, hit.EventId);
            }

            if (wasCalm && agent.Fear.State == AgentFearState.Calm)
            {
                fear.Alarm(agent, hit.EventId, AgentAlertSource.Bumped, point);
            }
        }

        /// <summary>Two objects meet: they push each other apart by their masses, a little bouncily.</summary>
        private void HitObject(PhysicsBody physicsBody, PhysicsBody other)
        {
            LogicalPosition centre = physicsBody.Position;
            LogicalPosition otherCentre = other.Position;
            long nx = (long)otherCentre.X - centre.X;
            long nz = (long)otherCentre.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            long closing = ((physicsBody.VelocityX - other.VelocityX) * nx + (physicsBody.VelocityZ - other.VelocityZ) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            long total = (long)physicsBody.MassGrams + other.MassGrams;
            long restitution = 100L + settings.ObjectRestitutionPercent;
            long slowed = restitution * other.MassGrams * closing / (100L * total);
            long shoved = restitution * physicsBody.MassGrams * closing / (100L * total);
            physicsBody.VelocityX -= slowed * nx / length;
            physicsBody.VelocityZ -= slowed * nz / length;
            other.VelocityX += shoved * nx / length;
            other.VelocityZ += shoved * nz / length;

            if (physicsBody.LastPushEventId != 0UL && closing >= (long)settings.LoggedBoxHitSpeed * SubMillimetre)
            {
                var point = new LogicalPosition(
                    (int)(centre.X + nx * physicsBody.Radius / length),
                    (int)(centre.Z + nz * physicsBody.Radius / length));
                other.LastPushEventId = context.Events.Append(context.Tick, physicsBody.Id, FireReactionEventType.BoxesCollided,
                    point, (int)(closing / SubMillimetre), 0, physicsBody.LastPushEventId, other.Id).EventId;
            }
            else if (physicsBody.LastPushEventId != 0UL)
            {
                other.LastPushEventId = physicsBody.LastPushEventId;
            }
        }

        private void ApplyFriction(PhysicsBody physicsBody)
        {
            long speed = IntegerMath.Sqrt(physicsBody.VelocityX * physicsBody.VelocityX + physicsBody.VelocityZ * physicsBody.VelocityZ);
            if (speed <= settings.Friction)
            {
                physicsBody.VelocityX = 0L;
                physicsBody.VelocityZ = 0L;
                return;
            }

            physicsBody.VelocityX = physicsBody.VelocityX * (speed - settings.Friction) / speed;
            physicsBody.VelocityZ = physicsBody.VelocityZ * (speed - settings.Friction) / speed;
        }

        private static void LimitSpeed(PhysicsBody physicsBody, long maximum)
        {
            long speed = IntegerMath.Sqrt(physicsBody.VelocityX * physicsBody.VelocityX + physicsBody.VelocityZ * physicsBody.VelocityZ);
            if (speed > maximum)
            {
                physicsBody.VelocityX = physicsBody.VelocityX * maximum / speed;
                physicsBody.VelocityZ = physicsBody.VelocityZ * maximum / speed;
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

        public FireReactionPhysicsObjectSnapshot GetSnapshot(int index)
        {
            PhysicsBody physicsBody = bodies[index];
            long speed = IntegerMath.Sqrt(physicsBody.VelocityX * physicsBody.VelocityX + physicsBody.VelocityZ * physicsBody.VelocityZ);
            return new FireReactionPhysicsObjectSnapshot(
                physicsBody.Id,
                physicsBody.Kind,
                physicsBody.Position,
                physicsBody.Size,
                physicsBody.Heading,
                (int)(speed / SubMillimetre));
        }

        public FireReactionPhysicsObjectSnapshot[] GetSnapshots()
        {
            var snapshots = new FireReactionPhysicsObjectSnapshot[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                snapshots[i] = GetSnapshot(i);
            }

            return snapshots;
        }

        private readonly struct ObjectContact
        {
            public ObjectContact(Agent agent, int bodyIndex, int closingSpeed)
            {
                Agent = agent;
                BodyIndex = bodyIndex;
                ClosingSpeed = closingSpeed;
            }

            public Agent Agent { get; }
            public int BodyIndex { get; }
            public int ClosingSpeed { get; }
        }
    }
}
