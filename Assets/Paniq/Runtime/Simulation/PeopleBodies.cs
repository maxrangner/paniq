using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// People as physical bodies: a capsule each in the physics world, pushed
    /// about by everything else. Behaviours still decide where somebody wants
    /// to go and how fast (<see cref="Locomotion"/>); this turns that into a
    /// push from their own feet, and reads back where the engine actually put
    /// them.
    ///
    /// Before each step, everybody on their feet pushes toward the velocity
    /// they want, but only as hard as a person can push (the physics feel's
    /// person push). So somebody shoulders through a loose crowd, a crowd
    /// leaning the other way carries them along, and a jammed doorway builds
    /// up real pressure. Somebody off their feet does not push at all: they
    /// topple over as a loose body, slide on the floor, and are tripped over.
    /// After each step, where everybody ended up is read back into the crowd's
    /// index, and how hard everything around each person pressed on them is
    /// added up; squeezed too hard for too long, they go down.
    ///
    /// Everybody is handled in ascending ID order, so the engine is always
    /// given the same pushes in the same order.
    /// </summary>
    internal sealed class PeopleBodies : IBindable
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly PhysicsWorld world;
        private readonly Threats threats;
        private readonly PhysicsFeelSettings feel;
        private readonly int firstHandle;
        private readonly int radius;

        /// <summary>Set once the body system exists; it is built before this.</summary>
        private BodySystem body;

        /// <summary>How the engine has each person, so a change of state is noticed once.</summary>
        private readonly Pose[] pose;

        /// <summary>Which way each person is to fall when next they go down.</summary>
        private readonly int[] fallHeading;

        /// <summary>The chair each person is sitting in, as a body in the physics world, or -1.</summary>
        private readonly int[] seatedIn;

        /// <summary>Each person's velocity as the engine last left it, hundredths of a millimetre per tick.</summary>
        private readonly long[] velocityX;
        private readonly long[] velocityZ;

        /// <summary>
        /// Each person's velocity going into this tick's step: as the engine
        /// left it, plus their own push and anything that shoved them. What a
        /// collision during the step is judged by.
        /// </summary>
        private readonly long[] incomingX;
        private readonly long[] incomingZ;

        /// <summary>How fast each person asked to go this tick, in millimetres per tick.</summary>
        private readonly int[] wanted;

        /// <summary>How hard everything pressed on each person during the last step.</summary>
        private readonly long[] squeeze;

        /// <summary>Ticks in a row each person has been squeezed past bearing.</summary>
        private readonly int[] squeezedTicks;

        /// <summary>
        /// Somebody being dragged: where they are being pulled to this tick, and
        /// how fast, or a speed of -1 when nobody is pulling them.
        /// </summary>
        private readonly LogicalPosition[] pullTo;
        private readonly int[] pullSpeed;

        private enum Pose
        {
            Standing,
            Lying,

            /// <summary>Down, but with no room to lie flat: the body stays standing up, gripping the floor, pushing nowhere.</summary>
            Crumpled,
            Seated,
            Gone
        }

        public PeopleBodies(SimulationContext context, Crowd crowd, PhysicsWorld world, Threats threats, int firstHandle)
        {
            this.context = context;
            this.crowd = crowd;
            this.world = world;
            this.threats = threats;
            this.firstHandle = firstHandle;
            feel = context.Scenario.PhysicsFeel;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;
            int count = crowd.All.Length;
            pose = new Pose[count];
            fallHeading = new int[count];
            seatedIn = new int[count];
            velocityX = new long[count];
            velocityZ = new long[count];
            incomingX = new long[count];
            incomingZ = new long[count];
            wanted = new int[count];
            squeeze = new long[count];
            squeezedTicks = new int[count];
            pullTo = new LogicalPosition[count];
            pullSpeed = new int[count];
            leavingChair = new int[count];
            for (int i = 0; i < count; i++)
            {
                seatedIn[i] = -1;
                pullSpeed[i] = -1;
                leavingChair[i] = -1;
            }
        }

        /// <summary>The body system is built before this, so it is handed over once everything exists.</summary>
        public void Bind(Systems systems) => body = systems.Body;

        /// <summary>How tall a person is to the physics, in millimetres: what a flying chair can hit.</summary>
        public const int HeightMillimetres = 1700;

        /// <summary>A person's body in the physics world.</summary>
        public int HandleOf(Agent agent) => firstHandle + agent.Index;

        /// <summary>The person whose body this is, or null for a loose thing.</summary>
        public Agent PersonAt(int handle)
        {
            int index = handle - firstHandle;
            return handle >= firstHandle && index < crowd.All.Length ? crowd.All[index] : null;
        }

        /// <summary>A body for each person, in ascending ID order, after every loose thing.</summary>
        public void AddEveryone()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                int handle = world.AddPerson($"Person {agent.Id.Value}", agent.Body.Position, agent.Body.Heading,
                    radius, HeightMillimetres,
                    (int)Math.Min(int.MaxValue, TraitEffects.PushMassGrams(agent, context.Scenario)));
                if (handle != firstHandle + i)
                {
                    throw new InvalidOperationException("People's bodies must follow the loose things' in the physics world.");
                }
            }
        }

        // ---------------------------------------------------------------- what other systems ask for

        /// <summary>How fast somebody was moving going into this tick's step, across the floor, in hundredths of a millimetre per tick.</summary>
        public (long X, long Z) VelocityOf(Agent agent) => (incomingX[agent.Index], incomingZ[agent.Index]);

        /// <summary>The next time this person goes down, they fall this way.</summary>
        public void FallTowards(Agent agent, int heading) => fallHeading[agent.Index] = IntegerMath.NormalizeDegrees(heading);

        /// <summary>
        /// A shove, a blast, a jet of water: a push of this speed (millimetres
        /// per tick) across the floor, and optionally up off it. It is added to
        /// however they were already moving.
        /// </summary>
        public void Push(Agent agent, int heading, int speed, int lift)
        {
            // Thrown up off the floor, they are off their feet already: the
            // body is let go before the push lands, or the hold that keeps a
            // standing person on the floor would swallow the lift.
            if (lift > 0 && pose[agent.Index] == Pose.Standing)
            {
                Topple(agent, HandleOf(agent));
            }

            LogicalPosition across = IntegerMath.Direction(heading);
            long scale = (long)speed * PhysicsWorld.SubMillimetre;
            long pushX = across.X * scale / IntegerMath.TrigScale;
            long pushZ = across.Z * scale / IntegerMath.TrigScale;
            world.AddVelocity(HandleOf(agent), pushX, (long)lift * PhysicsWorld.SubMillimetre, pushZ);
            velocityX[agent.Index] += pushX;
            velocityZ[agent.Index] += pushZ;
        }

        /// <summary>
        /// The speed, in millimetres per tick, that sends somebody off their feet
        /// sliding this far along the floor before their grip stops them.
        /// </summary>
        public int SpeedToSlide(int distanceMillimetres)
        {
            // Slowing = grip * gravity, in millimetres per tick per tick; then
            // v * v = 2 * slowing * distance.
            double slowing = feel.PersonFloorGripPercent / 100.0 * 9.81 * feel.GravityPercent / 100.0 *
                             1000.0 / (FireReactionSimulation.TicksPerSecond * FireReactionSimulation.TicksPerSecond);
            return (int)Math.Round(Math.Sqrt(2.0 * Math.Max(0.0, slowing) * Math.Max(0, distanceMillimetres)));
        }

        /// <summary>
        /// Somebody helping pulls this person, lying on the floor, toward a
        /// spot this tick, no faster than the speed given.
        /// </summary>
        public void PullToward(Agent agent, LogicalPosition spot, int speed)
        {
            pullTo[agent.Index] = spot;
            pullSpeed[agent.Index] = Math.Max(0, speed);
        }

        /// <summary>
        /// Sits somebody in a chair: they are put on it, held there, and pass
        /// through the chair itself so the two do not fight.
        /// </summary>
        public void SitIn(Agent agent, int chairHandle, LogicalPosition seat, int heading)
        {
            int handle = HandleOf(agent);
            crowd.MoveTo(agent, seat);
            seatedIn[agent.Index] = chairHandle;
            world.IgnoreEachOther(handle, chairHandle, true);
            world.SetUpright(handle, true, heading);
            world.SetPinned(handle, true);
            world.Place(handle, (long)seat.X * PhysicsWorld.SubMillimetre, 0L, (long)seat.Z * PhysicsWorld.SubMillimetre, heading);
            pose[agent.Index] = Pose.Seated;
        }

        /// <summary>
        /// Moves somebody who is on a chair: lowering onto the seat, riding the
        /// chair in under the table, or backing out on it. They are held by the
        /// chair throughout, so nothing else pushes them about while they move.
        /// </summary>
        public void MoveSeated(Agent agent, LogicalPosition position, int heading)
        {
            int handle = HandleOf(agent);
            crowd.MoveTo(agent, position);
            world.SetUpright(handle, true, heading);
            world.Place(handle, (long)position.X * PhysicsWorld.SubMillimetre, 0L,
                (long)position.Z * PhysicsWorld.SubMillimetre, heading);
        }

        /// <summary>
        /// Out of the chair. If they were given a spot to step to they stand
        /// there; knocked off it, they go down where they are and are free of
        /// it from then on.
        /// </summary>
        public void LeaveChair(Agent agent, LogicalPosition? stepTo)
        {
            int index = agent.Index;
            int chair = seatedIn[index];
            if (chair < 0)
            {
                return;
            }

            int handle = HandleOf(agent);
            seatedIn[index] = -1;
            world.SetPinned(handle, false);
            pose[index] = Pose.Standing;
            if (stepTo.HasValue)
            {
                crowd.MoveTo(agent, stepTo.Value);
                world.Place(handle, (long)stepTo.Value.X * PhysicsWorld.SubMillimetre, 0L,
                    (long)stepTo.Value.Z * PhysicsWorld.SubMillimetre, agent.Body.Heading);
                world.IgnoreEachOther(handle, chair, false);
                return;
            }

            // Up out of the chair with nowhere to step: they are still standing
            // in its outline, so the two keep passing through each other until
            // the chair, shoved back as they rose, has slid clear of them.
            leavingChair[index] = chair;
        }

        /// <summary>
        /// Per person: the chair they got up out of without room to step clear
        /// of it, and are still passing through, or -1.
        /// </summary>
        private readonly int[] leavingChair;

        /// <summary>How far apart a person and the chair they got up from must be, middle to middle, before they are solid to each other again.</summary>
        private const int ClearOfTheChairMillimetres = 600;

        /// <summary>Anybody now clear of the chair they got up from is solid to it again.</summary>
        private void LetGoOfChairs()
        {
            for (int i = 0; i < leavingChair.Length; i++)
            {
                int chair = leavingChair[i];
                if (chair < 0)
                {
                    continue;
                }

                LogicalPosition person = crowd.All[i].Body.Position;
                LogicalPosition seat = world.Read(chair).Centre;
                long apart = (long)ClearOfTheChairMillimetres;
                if (LogicalPosition.DistanceSquared(person, seat) >= apart * apart)
                {
                    world.IgnoreEachOther(firstHandle + i, chair, false);
                    leavingChair[i] = -1;
                }
            }
        }

        // ---------------------------------------------------------------- before the step

        /// <summary>
        /// Phase 8, before the engine steps: everybody's body is brought into
        /// line with their state, and everybody on their feet pushes toward the
        /// way they want to go.
        /// </summary>
        public void Drive()
        {
            LetGoOfChairs();
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                int handle = firstHandle + i;
                wanted[i] = 0;
                incomingX[i] = velocityX[i];
                incomingZ[i] = velocityZ[i];
                if (!agent.IsParticipating)
                {
                    if (pose[i] != Pose.Gone)
                    {
                        world.SetSolid(handle, false);
                        pose[i] = Pose.Gone;
                    }

                    continue;
                }

                if (pose[i] == Pose.Seated)
                {
                    continue;
                }

                bool down = agent.Body.State == AgentBodyState.Fallen || agent.Body.State == AgentBodyState.Unconscious;
                if (down && pose[i] == Pose.Standing)
                {
                    Topple(agent, handle);
                }
                else if (!down && pose[i] == Pose.Lying)
                {
                    StandUp(agent, handle);
                }
                else if (!down && pose[i] == Pose.Crumpled)
                {
                    pose[i] = Pose.Standing;
                }

                if (down)
                {
                    if (pose[i] == Pose.Lying)
                    {
                        Pull(agent, handle);
                    }
                    else
                    {
                        // Crumpled where they stood: no push of their own, and
                        // anybody dragging them hauls a dead weight that will
                        // not come.
                        pullSpeed[i] = -1;
                        world.SetGrip(handle, feel.PersonFloorGripPercent, 10);
                    }

                    continue;
                }

                world.Face(handle, agent.Body.Heading);
                if (agent.Body.State != AgentBodyState.Upright)
                {
                    // Reeling or getting up: no push of their own, and their
                    // feet drag them to a stop.
                    world.SetGrip(handle, feel.PersonFloorGripPercent, 10);
                    continue;
                }

                world.SetGrip(handle, 0, 10);
                wanted[i] = agent.Body.Speed;
                Stride(agent, handle);
            }
        }

        /// <summary>
        /// The push of somebody's own feet: toward the velocity they want, by no
        /// more than a person can manage in one tick. Standing still, the same
        /// push is what holds their ground.
        /// </summary>
        private void Stride(Agent agent, int handle)
        {
            LogicalPosition direction = IntegerMath.Direction(agent.Body.Heading);
            long scale = (long)agent.Body.Speed * PhysicsWorld.SubMillimetre;
            long wantX = direction.X * scale / IntegerMath.TrigScale;
            long wantZ = direction.Z * scale / IntegerMath.TrigScale;
            long changeX = wantX - velocityX[agent.Index];
            long changeZ = wantZ - velocityZ[agent.Index];
            long change = IntegerMath.Sqrt(changeX * changeX + changeZ * changeZ);
            long most = (long)feel.PersonPushMillimetresPerTickPerTick * PhysicsWorld.SubMillimetre;
            if (change > most)
            {
                changeX = changeX * most / change;
                changeZ = changeZ * most / change;
            }

            if (changeX != 0L || changeZ != 0L)
            {
                world.AddVelocity(handle, changeX, 0L, changeZ);
                incomingX[agent.Index] += changeX;
                incomingZ[agent.Index] += changeZ;
            }
        }

        /// <summary>
        /// Off their feet: the body may tip now, and is set toppling the way
        /// they are falling, keeping whatever speed they had.
        /// </summary>
        /// <summary>How much of their speed somebody knocked flat keeps, as a percentage: the rest goes into the floor.</summary>
        private const long SkidPercent = 40L;

        private void Topple(Agent agent, int handle)
        {
            pose[agent.Index] = Pose.Lying;
            world.SetGrip(handle, feel.PersonFloorGripPercent, 10);
            int heading = fallHeading[agent.Index];
            fallHeading[agent.Index] = agent.Body.Heading;

            // Laid flat, still sliding as they were, along the way they fall
            // if there is room there, else the nearest way round that has
            // room. Packed in so tight there is no room to lie at all, they
            // crumple where they stand: down, but taking up no more floor than
            // they did on their feet. Either way the display draws the fall.
            for (int turn = 0; turn <= 180; turn += 45)
            {
                for (int side = 1; side >= -1; side -= 2)
                {
                    if (side < 0 && (turn == 0 || turn == 180))
                    {
                        continue;
                    }

                    int along = IntegerMath.NormalizeDegrees(heading + side * turn);
                    if (world.IsClearToLie(agent.Body.Position, along, radius, HeightMillimetres, handle))
                    {
                        world.SetUpright(handle, false, agent.Body.Heading);
                        world.LayDown(handle, along, radius);

                        // Most of a running person's speed goes into the floor
                        // when they hit it: they skid, they do not keep running
                        // along on their side and plough into whoever is there.
                        velocityX[agent.Index] = velocityX[agent.Index] * SkidPercent / 100L;
                        velocityZ[agent.Index] = velocityZ[agent.Index] * SkidPercent / 100L;
                        world.SetVelocity(handle, velocityX[agent.Index], 0L, velocityZ[agent.Index]);
                        return;
                    }
                }
            }

            pose[agent.Index] = Pose.Crumpled;
        }

        /// <summary>
        /// Back on their feet: stood upright where they lay, facing the way they
        /// were, and holding still. If where they lay has since been crowded
        /// (a box slid up against them, somebody else went down across their
        /// legs), they get up in the nearest clear spot instead, looked for in a
        /// fixed order so a replay picks the same one.
        /// </summary>
        private void StandUp(Agent agent, int handle)
        {
            pose[agent.Index] = Pose.Standing;
            LogicalPosition where = ClearSpotNear(agent.Body.Position, agent.Body.Heading, handle);
            if (!where.Equals(agent.Body.Position))
            {
                crowd.MoveTo(agent, where);
            }

            world.SetUpright(handle, true, agent.Body.Heading);
            world.Place(handle, (long)where.X * PhysicsWorld.SubMillimetre, 0L, (long)where.Z * PhysicsWorld.SubMillimetre,
                agent.Body.Heading);
            velocityX[agent.Index] = 0L;
            velocityZ[agent.Index] = 0L;
        }

        /// <summary>How far round somebody getting up looks for room to stand, and in what steps, in millimetres.</summary>
        private const int StandSearchStepMillimetres = 100;
        private const int StandSearchReachMillimetres = 800;

        /// <summary>
        /// The nearest spot to this one where somebody could stand without
        /// being inside anything: here first, then further and further out, in
        /// eight directions starting from the way they face. If nowhere within
        /// reach is clear they stand where they are and are eased out of
        /// whatever they overlap.
        /// </summary>
        private LogicalPosition ClearSpotNear(LogicalPosition from, int heading, int handle)
        {
            if (world.IsClearToStand(from, radius, HeightMillimetres, handle))
            {
                return from;
            }

            for (int distance = StandSearchStepMillimetres; distance <= StandSearchReachMillimetres;
                 distance += StandSearchStepMillimetres)
            {
                for (int turn = 0; turn < 360; turn += 45)
                {
                    LogicalPosition spot = from + IntegerMath.Displacement(heading + turn, distance);
                    if (world.IsClearToStand(spot, radius, HeightMillimetres, handle) && !world.IsBuildingBetween(from, spot))
                    {
                        return spot;
                    }
                }
            }

            return from;
        }

        /// <summary>Somebody being dragged is hauled toward the spot behind their helper.</summary>
        private void Pull(Agent agent, int handle)
        {
            int index = agent.Index;
            int speed = pullSpeed[index];
            pullSpeed[index] = -1;
            if (speed < 0)
            {
                return;
            }

            LogicalPosition from = agent.Body.Position;
            long dx = (long)pullTo[index].X - from.X;
            long dz = (long)pullTo[index].Z - from.Z;
            long distance = IntegerMath.Sqrt(dx * dx + dz * dz);

            // Hauled by the arms, the body trails in line behind the one
            // pulling it, and so goes through a doorway rather than across it:
            // swung round into line, where there is room to swing it.
            int inLine = IntegerMath.HeadingOf(dx, dz, agent.Body.Heading);
            if (world.IsClearToLie(from, inLine, radius, HeightMillimetres, handle))
            {
                world.LayAlong(handle, inLine);
            }
            long wantX = 0L;
            long wantZ = 0L;
            if (distance > 0L)
            {
                long go = Math.Min(distance, speed);
                wantX = dx * go * PhysicsWorld.SubMillimetre / distance;
                wantZ = dz * go * PhysicsWorld.SubMillimetre / distance;
            }

            world.SetVelocity(handle, wantX, 0L, wantZ);
            incomingX[index] = wantX;
            incomingZ[index] = wantZ;
        }


        // ---------------------------------------------------------------- after the step

        /// <summary>
        /// Phase 8, once the engine has stepped: where everybody ended up, in
        /// ascending ID order, told to the crowd's index; whether they got
        /// anywhere they wanted to; and whether the move carried them through
        /// fire.
        /// </summary>
        public void ReadBack()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || pose[i] == Pose.Gone)
                {
                    continue;
                }

                PhysicsWorld.Reading reading = world.Read(firstHandle + i);
                LogicalPosition from = agent.Body.Position;
                LogicalPosition to = reading.Centre;
                velocityX[i] = reading.VelocityX;
                velocityZ[i] = reading.VelocityZ;
                agent.Body.Pose = new BodyPose((int)(reading.Y / PhysicsWorld.SubMillimetre),
                    reading.RotationX, reading.RotationY, reading.RotationZ, reading.RotationW, reading.Position);
                if (!to.Equals(from))
                {
                    crowd.MoveTo(agent, to);
                }

                // Wanting to go somewhere and getting less than a third of the
                // way is being stuck, whatever is in the way.
                long moved = IntegerMath.Sqrt(LogicalPosition.DistanceSquared(from, to));
                if (wanted[i] > 0 && moved * 3 < wanted[i])
                {
                    agent.Body.BlockedTicks++;
                }
                else if (moved > 0L)
                {
                    agent.Body.BlockedTicks = 0;
                }

                if (moved > 0L)
                {
                    threats.ResolveContactAlong(agent, from, to, body);
                }
            }
        }

        /// <summary>
        /// How hard everything pressed on each person during the step, from the
        /// engine's list of contacts: other people, loose things, walls and
        /// doors, but not the floor they stand on. Anybody squeezed past bearing
        /// for long enough goes down.
        /// </summary>
        public void FeelTheSqueeze(IReadOnlyList<PhysicsWorld.Contact> contacts)
        {
            Array.Clear(squeeze, 0, squeeze.Length);
            for (int c = 0; c < contacts.Count; c++)
            {
                PhysicsWorld.Contact contact = contacts[c];
                if (contact.Static == PhysicsWorld.StaticKind.Floor)
                {
                    continue;
                }

                Agent first = PersonAt(contact.BodyA);
                Agent second = contact.BodyB >= 0 ? PersonAt(contact.BodyB) : null;
                if (first != null)
                {
                    squeeze[first.Index] += contact.Impulse;
                }

                if (second != null)
                {
                    squeeze[second.Index] += contact.Impulse;
                }
            }

            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || agent.Body.State != AgentBodyState.Upright || pose[i] != Pose.Standing ||
                    squeeze[i] < feel.CrushPressure)
                {
                    squeezedTicks[i] = 0;
                    continue;
                }

                squeezedTicks[i]++;
                if (squeezedTicks[i] >= feel.CrushTicks)
                {
                    squeezedTicks[i] = 0;
                    body.Crush(agent, (int)Math.Min(int.MaxValue, squeeze[i]));
                }
            }
        }

        /// <summary>How hard this person was squeezed during the last step, for tests and the stats panel.</summary>
        public long SqueezeOn(Agent agent) => squeeze[agent.Index];
    }
}
