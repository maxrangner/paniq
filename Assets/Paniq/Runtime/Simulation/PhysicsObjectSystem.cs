using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Physical objects: loose things that slide, tumble, stack, fall off
    /// tables and fly when flung. The movement itself is the 3D physics
    /// engine's (see <see cref="PhysicsWorld"/>); this system keeps the
    /// rules of the game on top of it. It decides who is holding what, who
    /// sits where, what breaks, who is knocked down by what, and which event
    /// caused which, reading every collision the engine reports and deciding
    /// in whole numbers what it means.
    ///
    /// People bump objects during their decisions (resolved with the other
    /// collisions) and each bump becomes a push on the engine's body. Then
    /// the engine steps, and every hit it reports is judged here: a heavy
    /// thing flying into somebody staggers or floors them, a hard knock
    /// smashes what was hit. Furniture (tables, chairs) is shoved and tipped
    /// but never smashes.
    ///
    /// Objects are also items: a person can pick one up (it then touches
    /// nothing and is carried in front of them), set it down, drop it or
    /// throw it. A strong runner who meets an item hurls it out of the way
    /// instead of kicking it. A thrown item hits people harder than a sliding
    /// one, because it strikes the body, not the feet.
    /// </summary>
    internal sealed class PhysicsObjectSystem : IBindable
    {
        /// <summary>Object positions and velocities are kept in hundredths of a millimetre.</summary>
        public const int SubMillimetre = PhysicsWorld.SubMillimetre;

        /// <summary>
        /// The size of thing that turns at the full spin rate when it is sent
        /// off at full speed. Anything bigger turns more slowly in proportion,
        /// anything smaller faster, up to the cap.
        /// </summary>
        private const int SpinReferenceRadiusMillimetres = 200;

        /// <summary>How wide a patch of floor one cell of the index covers.</summary>
        private const int CellSizeMillimetres = 1000;

        /// <summary>How far past the rooms the index reaches, for things flung out of the building.</summary>
        private const int OutsideMarginMillimetres = 8000;

        /// <summary>
        /// A thing whose lowest point is this high or higher is off the floor:
        /// on a desk, on another thing, or in the air. It is not in anybody's
        /// way while it is up there.
        /// </summary>
        private const int OffTheFloorMillimetres = 100;

        /// <summary>
        /// Slower than this, in hundredths of a millimetre per tick (half a
        /// millimetre, two and a half centimetres a second), a thing counts as
        /// still. The engine settles things with the odd hair of movement, and
        /// nobody should wait for a box to be perfectly still.
        /// </summary>
        private const long StillSpeed = 50L;

        /// <summary>How high things are carried, and so how high a dropped or thrown one starts, in millimetres.</summary>
        private const int HandHeightMillimetres = 1000;

        /// <summary>A blast sends things cartwheeling this many times harder than a kick turns them.</summary>
        private const int BlastTumbleMultiplier = 3;

        private sealed class PhysicsBody
        {
            public SimulationId Id;
            public PhysicsObjectKind Kind;

            /// <summary>
            /// Where it stands, in hundredths of a millimetre. Read freely; to
            /// move it, call <see cref="PhysicsObjectSystem.MoveBody"/>, which
            /// also tells the index of what is lying where. The setters are
            /// private so a new way of moving something cannot be written
            /// without noticing that.
            /// </summary>
            public long X { get; private set; }

            public long Z { get; private set; }

            /// <summary>
            /// Its velocity, in hundredths of a millimetre per tick: as the
            /// engine last reported it, plus anything this tick has given it
            /// since. So it is what the thing is carrying into the next step,
            /// which is what a hit during that step is judged by.
            /// </summary>
            public long VelocityX;
            public long VelocityY;
            public long VelocityZ;

            public int Radius;
            public int Size;
            public int MassGrams;
            public int Heading;
            public int Spin;

            /// <summary>What the engine last said about where and how it is.</summary>
            public PhysicsWorld.Reading Reading;

            /// <summary>The index of the person carrying it, or -1 when it is on the floor.</summary>
            public int HeldBy = -1;

            /// <summary>The person sitting on this chair, or -1. A chair with someone on it does not budge.</summary>
            public int OccupiedBy = -1;

            /// <summary>Ticks of spray left, for an extinguisher.</summary>
            public int Fuel;

            /// <summary>Thrown and still flying: it hits harder until it stops or hits someone.</summary>
            public bool Thrown;

            /// <summary>
            /// A spare the run keeps aside until the player puts it down with a
            /// card. It is not in the world: nothing can touch it, reach it,
            /// burn it or see it.
            /// </summary>
            public bool Dormant;

            /// <summary>
            /// Smashed. It is wreckage now: smaller, lighter, still something to
            /// trip over, but no longer a chair anybody can sit on.
            /// </summary>
            public bool Wrecked;

            /// <summary>The event that last set this object moving, so its later hits can name their cause.</summary>
            public ulong LastPushEventId;

            /// <summary>Whether it stood upright the last time it was read: how a thing is seen to go over.</summary>
            public bool WasUpright = true;

            /// <summary>It has gone over and popped once already; a lamp's bulb goes only once.</summary>
            public bool Popped;

            /// <summary>The index of the thing this is a part of (a shade's lamp), or -1 for a thing of its own.</summary>
            public int PartOf = -1;

            /// <summary>A thing that drives itself: when it may set off again after being stopped, and whether it was trying to go.</summary>
            public int RoverPauseUntilTick;

            public bool RoverWantedToMove;

            /// <summary>A thing that drives itself, turning on the spot toward this heading before it sets off again.</summary>
            public bool RoverTurning;

            public int RoverGoalHeading;

            /// <summary>The <c>DoorBlocked</c> event while this thing is jamming a door, so clearing it names the same door.</summary>
            public ulong BlockedEventId;

            /// <summary>
            /// Held in place by the simulation (a tower of boxes waiting to
            /// fall, or a box lying in the heap it fell into): nothing
            /// pushes it, and it goes nowhere until it is unpinned -- by
            /// being picked up, thrown clear, or let go of by whatever
            /// pinned it (see <see cref="TrapSystem"/>).
            /// </summary>
            public bool Pinned;

            public LogicalPosition Position => new LogicalPosition(
                (int)FloorDivide(X, SubMillimetre),
                (int)FloorDivide(Z, SubMillimetre));

            /// <summary>Up on a desk, on another thing, or in the air, rather than on the floor.</summary>
            public bool OffTheFloor => HeldBy < 0 && !Dormant && Reading.BottomMillimetres >= OffTheFloorMillimetres;

            /// <summary>Moves it. Go through <see cref="PhysicsObjectSystem.MoveBody"/>, which keeps the index true.</summary>
            public void MoveWithoutTellingTheIndex(long x, long z)
            {
                X = x;
                Z = z;
            }
        }

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly BodySystem body;
        private readonly FearSystem fear;
        private readonly SoundSystem sound;
        private readonly FireSystem fire;
        private PowerSystem power;
        private readonly PhysicsWorld world;
        private readonly ObjectPhysicsSettings settings;
        private readonly PhysicsFeelSettings feel;

        /// <summary>What each kind of thing is made of and what it is for.</summary>
        private readonly FlammableSettings kinds;
        private readonly int personRadius;
        private readonly ItemSettings items;
        private readonly PhysicsBody[] bodies;

        /// <summary>Which patch of floor each thing is lying on.</summary>
        private readonly UniformGridIndex whereThingsAre;

        /// <summary>The largest thing in the scenario, so a question can be widened enough to catch it.</summary>
        private readonly int widestRadius;

        /// <summary>Everybody's physical body, for who ran into what. Set once it exists.</summary>
        private PeopleBodies people;

        /// <summary>
        /// A person running into a thing at least this fast, in millimetres per
        /// tick, is a bump worth the log: slower is just brushing past.
        /// </summary>
        private const int LoggedBumpSpeed = 10;

        /// <summary>
        /// Per thing: the body of whoever just let go of it, which it passes
        /// through until it is clear of them, or -1.
        /// </summary>
        private readonly int[] leftHandsOf;

        /// <summary>
        /// How far apart, middle to middle beyond both their widths, a thing and
        /// the person who let go of it must be before they are solid to each
        /// other again, in millimetres.
        /// </summary>
        private const int ClearOfTheHandsMillimetres = 100;

        /// <summary>Each thing's velocity going into the engine's step, reused every tick.</summary>
        private readonly (long X, long Y, long Z)[] velocityBefore;

        /// <summary>Reused by <see cref="Gather"/>, one per level of nesting.</summary>
        private readonly int[][] gathered;
        private int gatherDepth;

        public PhysicsObjectSystem(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            BodySystem body,
            FearSystem fear,
            SoundSystem sound, FireSystem fire,
            PhysicsWorld world)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.body = body;
            this.fear = fear;
            this.sound = sound;
            this.fire = fire;
            this.world = world;
            settings = context.Scenario.ObjectPhysics;
            feel = context.Scenario.PhysicsFeel;
            kinds = context.Scenario.Flammables;
            items = context.Scenario.Items;
            personRadius = context.Scenario.World.OccupancyRadiusMillimetres;

            var definitions = (PhysicsObjectDefinition[])context.Scenario.PhysicsObjects.Clone();
            Array.Sort(definitions, (left, right) => left.ObjectId.CompareTo(right.ObjectId));
            bodies = new PhysicsBody[definitions.Length];
            restingBottom = new int[definitions.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                PhysicsObjectDefinition definition = definitions[i];
                bodies[i] = new PhysicsBody
                {
                    Id = definition.ObjectId,
                    Kind = definition.Kind,
                    Radius = definition.RadiusMillimetres,
                    Size = definition.SizeMillimetres,
                    MassGrams = definition.MassGrams,
                    Dormant = definition.StartsDormant,
                    Heading = IntegerMath.NormalizeDegrees(definition.InitialFacingDegrees),

                    // A spare has no spray in it until a card puts it down, which
                    // is also why nobody ever goes to fetch one.
                    Fuel = kinds.Of(definition.Kind).IsEquipment && !definition.StartsDormant
                        ? context.Scenario.Extinguishers.FuelTicks
                        : 0
                };

                bodies[i].MoveWithoutTellingTheIndex(
                    (long)definition.InitialPosition.X * SubMillimetre,
                    (long)definition.InitialPosition.Z * SubMillimetre);
                widestRadius = Math.Max(widestRadius, definition.RadiusMillimetres);
                indexById[definition.ObjectId] = i;
                if (kinds.Of(definition.Kind).IsEquipment)
                {
                    equipment.Add(i);
                }
            }

            // Parts know the thing they belong to once every thing has a place.
            for (int i = 0; i < bodies.Length; i++)
            {
                if (definitions[i].IsPartOfSomething && indexById.TryGetValue(definitions[i].PartOfObjectId, out int whole))
                {
                    bodies[i].PartOf = whole;
                }
            }

            // Things are flung about, so the grid reaches well past the rooms.
            LogicalBounds area = geometry.FireArea;
            whereThingsAre = new UniformGridIndex(
                new LogicalBounds(
                    area.MinX - OutsideMarginMillimetres, area.MaxX + OutsideMarginMillimetres,
                    area.MinZ - OutsideMarginMillimetres, area.MaxZ + OutsideMarginMillimetres),
                CellSizeMillimetres,
                bodies.Length);

            velocityBefore = new (long, long, long)[bodies.Length];
            leftHandsOf = new int[bodies.Length];
            for (int i = 0; i < leftHandsOf.Length; i++)
            {
                leftHandsOf[i] = -1;
            }
            gathered = new int[4][];
            for (int i = 0; i < gathered.Length; i++)
            {
                gathered[i] = new int[bodies.Length];
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                whereThingsAre.Place(i, bodies[i].Position);
            }

            AddToTheWorld(definitions);
        }

        /// <summary>
        /// Gives every thing its solid body in the physics world, in ascending
        /// ID order, so each one's handle is its index here. Something authored
        /// as resting starts on top of whatever holds it up: the table under
        /// it, or else the lowest-numbered floor object at its spot, so a
        /// replay always stacks the same way.
        /// </summary>
        private void AddToTheWorld(PhysicsObjectDefinition[] definitions)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                PhysicsBody thing = bodies[i];
                int bottom = definitions[i].StartsResting ? RestingHeight(i) : 0;
                ObjectKindSettings kind = kinds.Of(thing.Kind);
                int grip = (int)Math.Max(1L, (long)FloorGrip() * kind.FrictionPercent / 100L);
                int handle = world.AddBody($"{thing.Kind} {thing.Id.Value}", ObjectShapes.For(thing.Kind, thing.Size),
                    thing.X, (long)bottom * SubMillimetre, thing.Z, thing.Heading, thing.MassGrams, grip,
                    settings.ObjectRestitutionPercent);
                if (handle != i)
                {
                    throw new InvalidOperationException("Loose things must be the first bodies in the physics world.");
                }

                if (thing.Dormant)
                {
                    world.SetSolid(i, false);
                }

                if (IsFixedInPlace(i))
                {
                    // Set into the wall: it sparks and bangs, but there is
                    // nothing of it standing out for anything to bump into.
                    world.SetPinned(i, true);
                    world.SetSolid(i, false);
                }

                thing.Reading = world.Read(i);
            }
        }

        /// <summary>
        /// How high a thing authored as resting starts: on the table under
        /// it, or on top of the lowest-numbered floor thing at its spot -- or,
        /// if other resting things with lower IDs are already stacked at that
        /// spot, on top of the highest of those. So a stack of any height
        /// works (the tower of boxes is four high): each resting thing is
        /// placed after the ones with lower IDs, so the height of everything
        /// under it is already known, and a pair stacks exactly as it did
        /// before towers existed.
        /// </summary>
        private int RestingHeight(int index)
        {
            PhysicsBody top = bodies[index];
            if (geometry.TableAt(top.Position, 0) >= 0)
            {
                return feel.TableHeightMillimetres;
            }

            int height = 0;
            bool baseFound = false;
            for (int u = 0; u < bodies.Length; u++)
            {
                PhysicsBody under = bodies[u];
                if (u == index || under.Dormant)
                {
                    continue;
                }

                long reach = under.Radius;
                if (LogicalPosition.DistanceSquared(top.Position, under.Position) > reach * reach)
                {
                    continue;
                }

                bool resting = context.Scenario.PhysicsObjects[DefinitionOf(under.Id)].StartsResting;
                if (!resting)
                {
                    if (!baseFound)
                    {
                        // The lowest-numbered floor thing at the spot is the base,
                        // as it always was.
                        baseFound = true;
                        height = Math.Max(height, ObjectShapes.TopHeight(under.Kind, under.Size));
                    }

                    continue;
                }

                if (u < index)
                {
                    // Already stacked here: this one goes on top of it.
                    height = Math.Max(height, restingBottom[u] + ObjectShapes.TopHeight(under.Kind, under.Size));
                }
            }

            restingBottom[index] = height;
            return height;
        }

        /// <summary>How high off the floor each resting thing's underside started, for the things stacked on it.</summary>
        private readonly int[] restingBottom;

        private int DefinitionOf(SimulationId id)
        {
            PhysicsObjectDefinition[] definitions = context.Scenario.PhysicsObjects;
            for (int d = 0; d < definitions.Length; d++)
            {
                if (definitions[d].ObjectId == id)
                {
                    return d;
                }
            }

            return -1;
        }

        /// <summary>
        /// The floor's grip under a thing, as the engine's friction coefficient
        /// times 100: the scenario's friction (a slowing, in hundredths of a
        /// millimetre per tick every tick) turned into the coefficient that
        /// slows a sliding thing by exactly that much, times the grip dial.
        /// Worked out against the run's own gravity, so turning gravity up for
        /// snappier arcs does not make things stop shorter on the floor.
        /// </summary>
        private int FloorGrip()
        {
            // A slowing of f hundredths of a millimetre per tick per tick is
            // f * 50 * 50 / 100000 metres per second per second; divided by
            // gravity that is the friction coefficient.
            double slowing = settings.Friction * 2500.0 / 100000.0;
            double coefficient = slowing / (9.81 * feel.GravityPercent / 100.0);
            return (int)Math.Round(coefficient * 100.0 * feel.FloorGripPercent / 100.0);
        }

        public int Count => bodies.Length;

        /// <summary>
        /// People's bodies are built after the things', and the cable is built
        /// from the things, so both are handed over once everything exists.
        /// </summary>
        public void Bind(Systems systems)
        {
            people = systems.People;
            power = systems.Power;
            flammables = systems.Flammables;
        }

        /// <summary>What is burning and what has burnt out, for a robot vacuum that has burnt to a stop.</summary>
        private FlammablesSystem flammables;

        // ---------------------------------------------------------------- things that drive themselves

        /// <summary>
        /// Before the engine steps: every thing that drives itself (a robot
        /// vacuum) pushes on the way it is facing at its cruising speed.
        /// Stopped by a wall, a desk, a foot or a box -- it wanted to go and
        /// barely moved, or the floor ahead is not floor -- it waits a moment,
        /// turns by a seeded amount and sets off again. Off its wheels (kicked,
        /// thrown, on its side), held, or burnt out, it does not drive at all.
        /// Ascending index order and the seed for the turns, so a replay agrees.
        /// It stays a loose thing like any other: kicked, thrown, tidied away
        /// and burnt like a box, and a burning one keeps trundling until it
        /// burns out, heating whatever it passes.
        /// </summary>
        public void DriveTheRovers()
        {
            int tick = context.Tick;
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody thing = bodies[b];
                ObjectKindSettings kind = kinds.Of(thing.Kind);
                if (!kind.DrivesItself || thing.HeldBy >= 0 || thing.Dormant || thing.Wrecked || thing.OccupiedBy >= 0 ||
                    (flammables != null && flammables.ObjectState(b) == ObjectBurnState.Burnt))
                {
                    thing.RoverWantedToMove = false;
                    continue;
                }

                bool onItsWheels = thing.Reading.UprightPercent >= 70 && thing.Reading.BottomMillimetres < OffTheFloorMillimetres &&
                                   !thing.Thrown;
                if (!onItsWheels)
                {
                    // Tumbling, flying or lying on its side: it settles first.
                    thing.RoverPauseUntilTick = checked(tick + settings.RoverPauseTicks);
                    thing.RoverWantedToMove = false;
                    continue;
                }

                if (thing.RoverTurning)
                {
                    // Turning on the spot, through the engine: a spin toward
                    // the heading it chose, so the body really turns and the
                    // read-back agrees. Writing its rotation straight in was
                    // tried first and made replays disagree from one run to
                    // the next; a spin, as a tumbling chair is given, does not.
                    int remaining = IntegerMath.SignedAngleDifference(thing.Heading, thing.RoverGoalHeading);
                    if (System.Math.Abs(remaining) <= settings.RoverTurnDegreesPerTick)
                    {
                        world.SetSpin(b, 0, 0, 0);
                        thing.RoverTurning = false;
                    }
                    else
                    {
                        int rate = settings.RoverTurnDegreesPerTick;
                        world.SetSpin(b, 0, remaining > 0 ? rate : -rate, 0);
                        continue;
                    }
                }

                if (tick < thing.RoverPauseUntilTick)
                {
                    continue;
                }

                // A body's length ahead: not floor, or a table, and it turns
                // before it gets there. Stopped short by something in the way
                // -- it wanted to go and hardly moved -- it turns as well.
                LogicalPosition ahead = thing.Position + IntegerMath.Displacement(thing.Heading,
                    thing.Radius + kind.CruiseSpeedMillimetresPerTick * 5);
                bool blockedAhead = geometry.RoomAt(ahead) < 0 || geometry.TableAt(ahead, thing.Radius) >= 0;
                bool stopped = thing.RoverWantedToMove &&
                               thing.Reading.HorizontalSpeed < (long)kind.CruiseSpeedMillimetresPerTick * SubMillimetre / 3L;
                if (blockedAhead || stopped)
                {
                    int turn = context.Random.NextIntInclusive(settings.RoverTurnMinimumDegrees, settings.RoverTurnMaximumDegrees);
                    int side = context.Random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                    thing.RoverGoalHeading = IntegerMath.NormalizeDegrees(thing.Heading + side * turn);
                    thing.RoverTurning = true;
                    SetMotion(b, 0L, 0L, 0L);
                    thing.RoverPauseUntilTick = checked(tick + settings.RoverPauseTicks);
                    thing.RoverWantedToMove = false;
                    continue;
                }

                LogicalPosition cruise = IntegerMath.Displacement(thing.Heading, kind.CruiseSpeedMillimetresPerTick);
                SetMotion(b, (long)cruise.X * SubMillimetre, 0L, (long)cruise.Z * SubMillimetre);
                thing.RoverWantedToMove = true;
            }
        }

        // ---------------------------------------------------------------- things that go over

        /// <summary>
        /// After the engine has stepped: anything that pops when it goes over
        /// (a standing lamp) and was upright last time but is not now, pops
        /// once -- a small crack, logged and heard, nothing thrown -- and
        /// whatever was authored as part of it comes loose where its top has
        /// come to lie. Ascending index order, so a replay agrees.
        /// </summary>
        private void PopWhateverWentOver()
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody thing = bodies[b];
                if (thing.HeldBy >= 0 || thing.Dormant || thing.PartOf >= 0)
                {
                    continue;
                }

                ObjectKindSettings kind = kinds.Of(thing.Kind);
                if (!kind.PopsWhenTipped)
                {
                    continue;
                }

                bool upright = thing.Reading.UprightPercent >= 50;
                if (thing.WasUpright && !upright && !thing.Popped)
                {
                    Pop(b, kind);
                }

                thing.WasUpright = upright;
            }
        }

        /// <summary>The small crack of a thing going over, and its parts coming loose.</summary>
        private void Pop(int index, ObjectKindSettings kind)
        {
            PhysicsBody thing = bodies[index];
            thing.Popped = true;
            LogicalPosition centre = thing.Position;
            ulong pop = context.Events.Append(context.Tick, thing.Id, CausalEventType.ObjectPopped, centre,
                thing.Size, 0, thing.LastPushEventId).EventId;
            sound.Thud(thing.Id, centre, pop);
            if (kind.ShedsPartsWhenTipped)
            {
                ShedParts(index, pop);
            }
        }

        /// <summary>
        /// Every dormant part of this thing comes loose where the thing's top
        /// now lies, and is nudged a little further the way the thing fell, so
        /// a lamp's shade drops to the floor beside it rather than inside it.
        /// </summary>
        private void ShedParts(int index, ulong causeEventId)
        {
            PhysicsBody thing = bodies[index];
            int top = ObjectShapes.TopHeight(thing.Kind, thing.Size);
            for (int p = 0; p < bodies.Length; p++)
            {
                PhysicsBody part = bodies[p];
                if (part.PartOf != index || !part.Dormant)
                {
                    continue;
                }

                // Where the top of the thing is, less the part's own height, so
                // the part starts where it was drawn and falls from there.
                (long x, long y, long z) = world.PointOn(index, System.Math.Max(0, top - ObjectShapes.TopHeight(part.Kind, part.Size)));
                part.Dormant = false;
                part.PartOf = -1;
                MoveBody(p, x, z);
                world.SetSolid(p, true);
                world.Place(p, x, y, z, part.Heading);
                part.Reading = world.Read(p);

                // Away from the thing's middle, the way it went over.
                int away = IntegerMath.HeadingOf(part.Position.X - thing.Position.X, part.Position.Z - thing.Position.Z, part.Heading);
                LogicalPosition push = IntegerMath.Displacement(away, ShedSpeedMillimetresPerTick);
                SetMotion(p, (long)push.X * SubMillimetre, 0L, (long)push.Z * SubMillimetre);
                Tumble(p, away, ShedSpeedMillimetresPerTick, 2);
                part.Thrown = false;
                part.LastPushEventId = causeEventId;
            }
        }

        /// <summary>How fast a part that has come loose is sent on its way, in millimetres per tick.</summary>
        private const int ShedSpeedMillimetresPerTick = 15;

        /// <summary>
        /// Something electrical goes off: the bang, the fling, the people
        /// knocked down and the floor set alight, in one place.
        /// <para>
        /// Everything here is somebody else's existing rule, in a fixed order
        /// so the run stays repeatable: the objects in ascending ID order, then
        /// the people in ascending ID order, then the floor squares row by row.
        /// </para>
        /// <para>
        /// It lives here rather than with the flammable things because the
        /// flames are only one of the reasons a thing goes off. Returns the
        /// bang's event ID, or zero for something that does not go off at all.
        /// </para>
        /// </summary>
        public ulong Detonate(int index, SimulationId id, ulong causeEventId)
        {
            ObjectKindSettings kind = kinds.Of(KindOf(index));
            if (kind.PopRadiusMillimetres <= 0)
            {
                return 0UL;
            }

            LogicalPosition centre = PositionOf(index);
            long radius = kind.PopRadiusMillimetres;
            ulong bang = context.Events.Append(context.Tick, id, CausalEventType.ObjectExploded, centre,
                kind.PopRadiusMillimetres, 0, causeEventId).EventId;

            // Heard well beyond the blast itself, which is how the far side of
            // the building learns something has happened.
            sound.Bang(id, centre, kind.PopRadiusMillimetres * 6, kind.PopRadiusMillimetres * 3, bang);

            FlingFrom(centre, kind.PopRadiusMillimetres, kind.PopSpeed, index, bang);

            using (Crowd.Nearby people = crowd.Within(centre, radius))
            {
                for (int c = 0; c < people.Count; c++)
                {
                    Agent agent = crowd.All[people[c]];
                    if (!agent.IsParticipating ||
                        LogicalPosition.DistanceSquared(agent.Body.Position, centre) > radius * radius)
                    {
                        continue;
                    }

                    int away = IntegerMath.HeadingBetween(centre, agent.Body.Position, agent.Body.Heading);
                    body.BlowOver(agent, away,
                        kind.PopRadiusMillimetres / 3 * context.Scenario.PhysicsFeel.BlastStrengthPercent / 100,
                        context.Scenario.PhysicsFeel.BlastLiftPercent, bang);
                }
            }

            fire.IgniteAround(centre, kind.PopRadiusMillimetres, kind.PopIgniteCells, bang);
            Wreck(index, id, bang);

            // Anything on the cable lights the cable. The power system has
            // already marked whatever it set off itself, so a spark that caused
            // this bang cannot come straight back round and cause it again.
            power?.SomethingPopped(id, bang);
            return bang;
        }

        /// <summary>
        /// Built into the building rather than standing in it: a wall socket.
        /// It never moves, whatever hits it or goes off beside it.
        /// </summary>
        /// <summary>
        /// Bolted to the wall: nothing shifts it, nobody picks it up, and a
        /// blast throws everything else around it instead.
        /// </summary>
        private bool IsFixedInPlace(int index) =>
            bodies[index].Kind == PhysicsObjectKind.WallSocket ||
            bodies[index].Kind == PhysicsObjectKind.FuseBox ||
            bodies[index].Kind == PhysicsObjectKind.AlarmSounder;

        public SimulationId IdOf(int index) => bodies[index].Id;

        /// <summary>The thing with this ID, or -1 if the run has no such thing.</summary>
        public int IndexOf(SimulationId id) => indexById.TryGetValue(id, out int index) ? index : -1;

        /// <summary>Every thing by ID, built once.</summary>
        private readonly Dictionary<SimulationId, int> indexById = new Dictionary<SimulationId, int>();

        /// <summary>
        /// The things that are equipment (extinguishers), in ascending order.
        /// Everybody looking for a bottle used to walk every loose thing in the
        /// building to find the few that are bottles.
        /// </summary>
        public IReadOnlyList<int> Equipment => equipment;

        private readonly List<int> equipment = new List<int>();

        public PhysicsObjectKind KindOf(int index) => bodies[index].Kind;

        public LogicalPosition PositionOf(int index) => bodies[index].Position;

        public int RadiusOf(int index) => bodies[index].Radius;

        /// <summary>
        /// Equipment rather than clutter: kept where it is until somebody needs
        /// it. Nobody tidies it away, wedges a door with it, or drops it the
        /// moment they are frightened.
        /// </summary>
        public bool IsEquipment(int index) => kinds.Of(bodies[index].Kind).IsEquipment;

        /// <summary>Furniture somebody sits on, whichever way up it happens to be right now.</summary>
        public bool CanBeSatOn(int index) => kinds.Of(bodies[index].Kind).CanBeSatOn;

        /// <summary>The largest thing in the building, for widening a question enough to catch it.</summary>
        public int WidestRadius => widestRadius;

        /// <summary>
        /// Moves a thing, and tells the index of what is lying where at the
        /// same moment. The next question about that patch of floor has to see
        /// where it got to, so this is kept up to date as things move rather
        /// than rebuilt once a tick.
        /// </summary>
        private void MoveBody(int index, long x, long z)
        {
            bodies[index].MoveWithoutTellingTheIndex(x, z);
            whereThingsAre.Place(index, bodies[index].Position);
        }

        /// <summary>
        /// The things that might be inside <paramref name="area"/>, in
        /// ascending order, written into a buffer belonging to this system. The
        /// list is a superset: every caller still applies its own exact test.
        /// </summary>
        public Nearby Gather(LogicalBounds area)
        {
            if (gatherDepth == gathered.Length)
            {
                throw new InvalidOperationException(
                    "Too many questions about what is lying where are open at once.");
            }

            int[] buffer = gathered[gatherDepth++];
            return new Nearby(this, buffer, whereThingsAre.Gather(area, buffer));
        }

        /// <summary>
        /// Whether the index still describes where everything actually is.
        /// Nothing in a run calls this; it is how a test proves no new way of
        /// moving something has been written that forgets to say so.
        /// </summary>
        internal bool IndexMatchesPositions()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                using Nearby here = Gather(UniformGridIndex.Around(bodies[i].Position, 0L));
                bool found = false;
                for (int n = 0; n < here.Count && !found; n++)
                {
                    found = here[n] == i;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleaseGatherBuffer()
        {
            gatherDepth--;
        }

        /// <summary>A borrowed list of things that might be in some area, in ascending order.</summary>
        internal readonly struct Nearby : IDisposable
        {
            private readonly PhysicsObjectSystem owner;
            private readonly int[] items;

            public Nearby(PhysicsObjectSystem owner, int[] items, int count)
            {
                this.owner = owner;
                this.items = items;
                Count = count;
            }

            public int Count { get; }

            public int this[int i] => items[i];

            public void Dispose()
            {
                owner.ReleaseGatherBuffer();
            }
        }

        /// <summary>Which way this thing faces, in whole degrees. A chair faces the way somebody sitting on it looks.</summary>
        public int HeadingOf(int index) => bodies[index].Heading;

        public bool IsMoving(int index)
        {
            PhysicsBody thing = bodies[index];
            return thing.VelocityX * thing.VelocityX + thing.VelocityY * thing.VelocityY +
                   thing.VelocityZ * thing.VelocityZ > StillSpeed * StillSpeed;
        }

        public int MassOf(int index) => bodies[index].MassGrams;

        /// <summary>The index of the person carrying it, or -1.</summary>
        public int HolderOf(int index) => bodies[index].HeldBy;

        /// <summary>A spare the player has not put down yet.</summary>
        public bool IsDormant(int index) => bodies[index].Dormant;

        /// <summary>Its authored width, in millimetres.</summary>
        public int SizeOf(int index) => bodies[index].Size;

        /// <summary>Held where it is by the simulation (see <see cref="PhysicsBody.Pinned"/>).</summary>
        public bool IsPinned(int index) => bodies[index].Pinned;

        /// <summary>
        /// Holds a thing where it is: the engine stops moving it and nothing
        /// here pushes it. It still stands in everybody's way, still heats,
        /// burns and can be picked up; picking it up or throwing it clear
        /// unpins it.
        /// </summary>
        public void Pin(int index)
        {
            PhysicsBody thing = bodies[index];
            if (thing.Pinned)
            {
                return;
            }

            thing.Pinned = true;
            thing.VelocityX = 0L;
            thing.VelocityY = 0L;
            thing.VelocityZ = 0L;
            thing.Spin = 0;
            thing.Thrown = false;
            world.SetPinned(index, true);
        }

        /// <summary>Lets a pinned thing go: it is a loose thing again.</summary>
        public void Unpin(int index)
        {
            PhysicsBody thing = bodies[index];
            if (!thing.Pinned)
            {
                return;
            }

            thing.Pinned = false;
            world.SetPinned(index, false);
        }

        /// <summary>
        /// Puts a thing straight down at a spot, its underside this high off
        /// the floor, stopped, turned to this heading: how the fallen tower's
        /// boxes are laid across the doorway (see <see cref="TrapSystem"/>).
        /// Not a throw and not a push: it is where it is put.
        /// </summary>
        public void PlaceAt(int index, LogicalPosition spot, int bottomMillimetres, int heading, ulong causeEventId)
        {
            PhysicsBody thing = bodies[index];
            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            thing.Heading = heading;
            thing.LastPushEventId = causeEventId;
            thing.VelocityX = 0L;
            thing.VelocityY = 0L;
            thing.VelocityZ = 0L;
            thing.Spin = 0;
            thing.Thrown = false;
            world.Place(index, thing.X, (long)bottomMillimetres * SubMillimetre, thing.Z, heading);
            thing.Reading = world.Read(index);
        }

        /// <summary>Smashed into wreckage on the floor.</summary>
        public bool IsWrecked(int index) => bodies[index].Wrecked;

        /// <summary>
        /// This thing has come to rest in a doorway. Its cause is whatever last
        /// set it moving, so the log can say who jammed the door; a thing that
        /// has never been touched is a root cause of its own.
        /// </summary>
        public void RecordBlockage(int index, SimulationId doorId, LogicalPosition doorCentre)
        {
            bodies[index].BlockedEventId = context.Events.Append(context.Tick, bodies[index].Id,
                CausalEventType.DoorBlocked, doorCentre, 0, 0, bodies[index].LastPushEventId, doorId).EventId;
        }

        /// <summary>Whatever was jamming that door is clear of it again.</summary>
        public void RecordUnblocking(int index, SimulationId doorId, LogicalPosition doorCentre)
        {
            if (index < 0)
            {
                return;
            }

            context.Events.Append(context.Tick, bodies[index].Id, CausalEventType.DoorUnblocked, doorCentre,
                0, 0, bodies[index].BlockedEventId, doorId);
            bodies[index].BlockedEventId = 0UL;
        }

        /// <summary>Heaves a thing out of a doorway, along the wall rather than through the door.</summary>
        public void ShoveAside(int index, Agent shover, int heading, int speed, ulong causeEventId)
        {
            PhysicsBody thing = bodies[index];
            LogicalPosition velocity = IntegerMath.Displacement(heading, speed);
            SetMotion(index, (long)velocity.X * SubMillimetre, 0L, (long)velocity.Z * SubMillimetre);
            thing.Thrown = false;
            thing.LastPushEventId = context.Events.Append(context.Tick, shover.Id,
                CausalEventType.AgentShovedObstruction, thing.Position, speed, 0, causeEventId, thing.Id).EventId;
            sound.Thud(shover.Id, thing.Position, thing.LastPushEventId);
        }

        /// <summary>Sets a carried thing down on an exact spot, if that spot is clear.</summary>
        public bool TrySetDownAt(int index, LogicalPosition spot, ulong causeEventId)
        {
            if (!IsClearForItem(index, spot))
            {
                return false;
            }

            Release(index, spot, 0, 0, causeEventId);
            return true;
        }

        /// <summary>
        /// Off the floor as far as every other system is concerned: in somebody's
        /// arms, a spare that is not in the world yet, or up on a desk, on top
        /// of another thing or in the air. All are skipped by the questions
        /// about what is in a person's way.
        /// </summary>
        private bool IsOutOfPlay(int index) =>
            bodies[index].HeldBy >= 0 || bodies[index].Dormant || bodies[index].OffTheFloor;

        /// <summary>Whether this thing is up on a table or on another object, and still, rather than on the floor.</summary>
        public bool IsResting(int index) => bodies[index].OffTheFloor && !IsMoving(index);

        /// <summary>The person sitting on this chair, or -1.</summary>
        public int OccupantOf(int index) => bodies[index].OccupiedBy;

        /// <summary>Ticks of spray left in an extinguisher.</summary>
        public int FuelOf(int index) => bodies[index].Fuel;

        /// <summary>Uses up a tick of spray.</summary>
        public void UseFuel(int index, int ticks)
        {
            bodies[index].Fuel = Math.Max(0, bodies[index].Fuel - ticks);
        }

        /// <summary>
        /// Whether this is a chair nobody is on, nobody is carrying, standing
        /// still on its feet: a chair knocked onto its back is not one anybody
        /// sits on.
        /// </summary>
        public bool IsFreeChair(int index)
        {
            PhysicsBody chair = bodies[index];
            return kinds.Of(chair.Kind).CanBeSatOn &&
                   chair.OccupiedBy < 0 && chair.HeldBy < 0 && !chair.Dormant && !chair.Wrecked && !IsMoving(index) &&
                   chair.Reading.UprightPercent >= 90 && !chair.OffTheFloor;
        }

        /// <summary>
        /// Scoots the chair in a little as someone settles onto it, so sitting
        /// down reads as riding the chair to its resting spot rather than
        /// snapping onto it wherever it happened to stop. Halves the distance
        /// and tries again until a clear spot is found, or gives up and leaves
        /// it where it stands.
        /// </summary>
        public LogicalPosition ScootIn(int index, int distance, Agent sitter)
        {
            PhysicsBody chair = bodies[index];
            for (int part = distance; part > 0; part /= 2)
            {
                LogicalPosition spot = chair.Position + IntegerMath.Displacement(chair.Heading, part);
                if (IsClearForItem(index, spot))
                {
                    MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
                    world.Place(index, chair.X, 0L, chair.Z, chair.Heading);
                    chair.Reading = world.Read(index);
                    return spot;
                }
            }

            return chair.Position;
        }

        /// <summary>
        /// Sets a thing sliding gently along a heading, the way a hand pulls a
        /// chair out: the engine carries it, so it stops against whatever is
        /// behind it. Nothing is logged; a hand on a chair is not an event.
        /// </summary>
        public void PullAlong(int index, int heading, int speedMillimetresPerTick)
        {
            LogicalPosition velocity = IntegerMath.Displacement(heading, speedMillimetresPerTick);
            SetMotion(index, (long)velocity.X * SubMillimetre, 0L, (long)velocity.Z * SubMillimetre);
        }

        /// <summary>Lets go of a thing that was being pulled: it stops where it is.</summary>
        public void StopPulling(int index)
        {
            SetMotion(index, 0L, 0L, 0L);
            bodies[index].Spin = 0;
        }

        /// <summary>
        /// Slides a thing this far along a heading, if the floor there is clear
        /// of everything but the person moving it. True when it went. Used for
        /// tucking a chair in under somebody who is already sitting on it: the
        /// chair is held, so it is moved rather than pushed.
        /// </summary>
        public bool TryNudge(int index, int heading, int distance, Agent mover)
        {
            PhysicsBody thing = bodies[index];
            LogicalPosition spot = thing.Position + IntegerMath.Displacement(heading, distance);
            if (!IsClearForItem(index, spot, mover.Index) ||
                crowd.FindBlocking(mover, thing.Position, spot) != null)
            {
                return false;
            }

            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            if (thing.OccupiedBy >= 0)
            {
                // Held for somebody sitting on it: driven to the spot through
                // the step, the way a person is, rather than put there behind
                // the engine's back, which it would undo.
                world.Drive(index, spot, thing.Heading);
            }
            else
            {
                world.Place(index, thing.X, 0L, thing.Z, thing.Heading);
            }

            thing.Reading = world.Read(index);
            return true;
        }

        /// <summary>
        /// Knocks a thing over backwards: shoved away along a heading and set
        /// turning end over end, so a chair somebody leaps out of goes over
        /// rather than sliding neatly back. The engine decides where it lands.
        /// </summary>
        public void KnockOver(int index, int heading, int speed, ulong causeEventId)
        {
            PhysicsBody thing = bodies[index];
            LogicalPosition velocity = IntegerMath.Displacement(heading, speed);

            // Shoved backwards and lifted a little, so its legs come off the
            // floor and it can go over instead of skating away upright.
            SetMotion(index,
                (long)velocity.X * SubMillimetre,
                (long)speed * SubMillimetre * KnockOverLiftPercent / 100L,
                (long)velocity.Z * SubMillimetre);

            // Tipping backwards about the line across the way it is going: the
            // seat goes up and over the back legs.
            LogicalPosition direction = IntegerMath.Direction(heading);
            long turn = (long)KnockOverSpinDegreesPerTick * feel.TumblePercent / 100L;
            thing.Spin = (int)turn;
            world.SetSpin(index,
                (int)(direction.Z * turn / IntegerMath.TrigScale),
                0,
                (int)(-direction.X * turn / IntegerMath.TrigScale));
            thing.Thrown = false;
            thing.LastPushEventId = causeEventId;
        }

        /// <summary>How much of the shove goes into lifting a chair somebody leapt out of, as a percentage.</summary>
        private const int KnockOverLiftPercent = 40;

        /// <summary>
        /// How fast a chair somebody leapt out of turns end over end, in degrees
        /// a tick: enough to go over in about half a second. Faster than this
        /// and it whirls through whoever is standing behind it before the engine
        /// can push the two apart.
        /// </summary>
        private const int KnockOverSpinDegreesPerTick = 12;

        /// <summary>Someone sits down on a chair: it stops dead and stays put until they get up.</summary>
        public void SitOn(int index, Agent sitter)
        {
            PhysicsBody chair = bodies[index];
            chair.OccupiedBy = sitter.Index;
            SetMotion(index, 0L, 0L, 0L);
            chair.Spin = 0;
            chair.Thrown = false;
            world.SetPinned(index, true);
        }

        /// <summary>They get up: the chair is loose again, and shoved back a little as they stand.</summary>
        public void StandUp(int index, int shoveX, int shoveZ)
        {
            PhysicsBody chair = bodies[index];
            chair.OccupiedBy = -1;
            world.SetPinned(index, false);
            SetMotion(index, shoveX, 0L, shoveZ);
        }

        /// <summary>
        /// Sets a thing moving, both here and in the engine, so a question later
        /// this tick sees the new velocity and the next step carries it out.
        /// </summary>
        private void SetMotion(int index, long velocityX, long velocityY, long velocityZ)
        {
            PhysicsBody thing = bodies[index];
            if (thing.Pinned)
            {
                // Held where it is: a kick, a shove or a blast moves it not at all.
                return;
            }

            thing.VelocityX = velocityX;
            thing.VelocityY = velocityY;
            thing.VelocityZ = velocityZ;
            world.SetVelocity(index, velocityX, velocityY, velocityZ);
        }

        /// <summary>Adds to a thing's velocity, both here and in the engine.</summary>
        private void AddMotion(int index, long velocityX, long velocityZ)
        {
            PhysicsBody thing = bodies[index];
            thing.VelocityX += velocityX;
            thing.VelocityZ += velocityZ;
            world.AddVelocity(index, velocityX, 0L, velocityZ);
        }

        // ---------------------------------------------------------------- items

        /// <summary>Whether this person could lift this item at all (items are boxes and chairs; the limit grows with strength).</summary>
        public bool CanLift(Agent agent, int index)
        {
            return bodies[index].OccupiedBy < 0 &&
                   bodies[index].MassGrams <= TraitEffects.CarryLimitGrams(agent, context.Scenario);
        }

        /// <summary>Takes an item into someone's arms. It stops moving and touches nothing while held.</summary>
        public void PickUp(int index, Agent carrier)
        {
            PhysicsBody item = bodies[index];
            Unpin(index);
            item.HeldBy = carrier.Index;
            item.VelocityX = 0L;
            item.VelocityY = 0L;
            item.VelocityZ = 0L;
            item.Spin = 0;
            item.Thrown = false;
            world.SetSolid(index, false);
            FollowCarrier(index, carrier);
        }

        /// <summary>Keeps a held item just in front of whoever carries it.</summary>
        public void FollowCarrier(int index, Agent carrier)
        {
            PhysicsBody item = bodies[index];
            LogicalPosition spot = carrier.Body.Position +
                                   IntegerMath.Displacement(carrier.Body.Heading, personRadius + item.Radius + items.HoldGapMillimetres);
            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            item.Heading = carrier.Body.Heading;
        }

        /// <summary>
        /// A clear spot on the floor right beside the carrier for this item:
        /// straight ahead first, then further and further round to either
        /// side. False when every direction is blocked.
        /// </summary>
        public bool FindSpotToPutDown(int index, Agent carrier, out LogicalPosition spot)
        {
            PhysicsBody item = bodies[index];
            int reach = personRadius + item.Radius + items.HoldGapMillimetres;
            int[] turns = { 0, 45, -45, 90, -90, 135, -135, 180 };
            for (int i = 0; i < turns.Length; i++)
            {
                spot = carrier.Body.Position + IntegerMath.Displacement(carrier.Body.Heading + turns[i], reach);
                if (IsClearForItem(index, spot))
                {
                    return true;
                }
            }

            spot = default;
            return false;
        }

        private bool IsClearForItem(int index, LogicalPosition spot, int ignoreAgentIndex = -1)
        {
            PhysicsBody item = bodies[index];
            if (geometry.RoomAtPoint(spot) < 0 || !geometry.RoomBounds(geometry.RoomAtPoint(spot)).ContainsCircle(spot, item.Radius) ||
                geometry.TableAt(spot, item.Radius) >= 0)
            {
                return false;
            }

            long agentReach = (long)personRadius + item.Radius;
            using (Crowd.Nearby people = crowd.Within(spot, agentReach))
            {
                for (int c = 0; c < people.Count; c++)
                {
                    // Whoever is holding it is never in its way: they are
                    // setting it down at arm's length.
                    // Whoever is riding it -- sitting on the chair being
                    // tucked in -- is not in its way either.
                    Agent other = crowd.All[people[c]];
                    if (other.IsParticipating && other.Index != item.HeldBy && other.Index != ignoreAgentIndex &&
                        LogicalPosition.DistanceSquared(other.Body.Position, spot) < agentReach * agentReach)
                    {
                        return false;
                    }
                }
            }

            using (Nearby things = Gather(UniformGridIndex.Around(spot, (long)item.Radius + widestRadius)))
            {
                for (int c = 0; c < things.Count; c++)
                {
                    int b = things[c];
                    if (b == index || IsOutOfPlay(b))
                    {
                        continue;
                    }

                    long reach = (long)item.Radius + bodies[b].Radius;
                    if (LogicalPosition.DistanceSquared(bodies[b].Position, spot) < reach * reach)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Lets go of a held item over <paramref name="spot"/> (found with
        /// <see cref="FindSpotToPutDown"/>). Put down calmly (no velocity and
        /// no cause) it is set on the floor; dropped, it falls from the
        /// carrier's hands; thrown, it leaves their hands at a velocity (mm per
        /// tick across the floor) and rises in an arc.
        /// </summary>
        public void Release(int index, LogicalPosition spot, int velocityX, int velocityZ, ulong causeEventId)
        {
            PhysicsBody item = bodies[index];
            int carrier = item.HeldBy;
            item.HeldBy = -1;
            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            bool thrown = velocityX != 0 || velocityZ != 0;
            bool setDown = !thrown && causeEventId == 0UL;
            long height = setDown ? 0L : (long)HandHeightMillimetres * SubMillimetre;
            world.SetSolid(index, true);
            world.Place(index, item.X, height, item.Z, item.Heading);

            // It leaves their hands cleanly: the corners of a box let go at
            // arm's length can still be inside the one letting go, so the two
            // pass through each other until it is clear of them.
            if (carrier >= 0)
            {
                int carrierHandle = people.HandleOf(crowd.All[carrier]);
                world.IgnoreEachOther(index, carrierHandle, true);
                leftHandsOf[index] = carrierHandle;
            }

            long vx = (long)velocityX * SubMillimetre * feel.ThrowStrengthPercent / 100L;
            long vz = (long)velocityZ * SubMillimetre * feel.ThrowStrengthPercent / 100L;
            long across = IntegerMath.Sqrt(vx * vx + vz * vz);
            long vy = across * IntegerMath.Sin(feel.ThrowArcDegrees) / IntegerMath.Cos(feel.ThrowArcDegrees);
            SetMotion(index, vx, thrown ? vy : 0L, vz);
            if (thrown)
            {
                // Which way it turns comes from the throw: end over end, away from the thrower.
                Tumble(index, IntegerMath.HeadingOf(vx, vz, item.Heading), (int)(across / SubMillimetre), 1);
            }

            item.Thrown = thrown;
            item.LastPushEventId = causeEventId;
        }

        /// <summary>How fast (mm per tick) this person can throw this item: stronger people and lighter items fly faster.</summary>
        public int ThrowSpeed(Agent agent, int index)
        {
            long speed = (long)items.ThrowImpulse * (agent.Traits.Strength + 5) * 1000L / (bodies[index].MassGrams + 5000L);
            return (int)Math.Max(items.ThrowMinimumSpeed,
                Math.Min(context.Scenario.World.MaximumStepDistanceMillimetres, speed));
        }

        /// <summary>
        /// Tests only: sets an object sliding at a velocity in millimetres per
        /// tick. It goes in the log as flung by nobody in particular, so what it
        /// then hits has a cause, as it would if a person had kicked it.
        /// </summary>
        public void Launch(int index, int velocityX, int velocityZ, int velocityY = 0)
        {
            PhysicsBody thing = bodies[index];
            long speed = IntegerMath.Sqrt((long)velocityX * velocityX + (long)velocityZ * velocityZ);
            thing.LastPushEventId = context.Events.Append(context.Tick, thing.Id, CausalEventType.ItemThrown,
                thing.Position, (int)speed, 0, 0UL, thing.Id).EventId;
            SetMotion(index, (long)velocityX * SubMillimetre, (long)velocityY * SubMillimetre, (long)velocityZ * SubMillimetre);
        }

        /// <summary>
        /// Whether a thing on the floor stands in the way of a body being slid
        /// across it (a shove, or the jet from an extinguisher). Only what is
        /// actually in their arms is ignored, because that travels with them;
        /// something they were merely walking toward is still on the floor and
        /// still in the way.
        /// </summary>
        public bool BlocksBody(Agent agent, LogicalPosition start, LogicalPosition destination)
        {
            int held = agent.Carry.Holding ? agent.Carry.ItemIndex : -1;
            return FindBlocking(start, destination, personRadius, held) >= 0;
        }

        /// <summary>The lowest-ID object on the floor a circle of this radius would pass through on this move, or -1.</summary>
        public int FindBlocking(LogicalPosition start, LogicalPosition destination, int radius, int ignoreIndex = -1)
        {
            using Nearby candidates = Gather(
                UniformGridIndex.Sweeping(start, destination, (long)radius + widestRadius));
            for (int c = 0; c < candidates.Count; c++)
            {
                int b = candidates[c];
                if (b == ignoreIndex || IsOutOfPlay(b))
                {
                    continue;
                }

                LogicalPosition centre = bodies[b].Position;
                long reach = (long)radius + bodies[b].Radius;
                if (IntegerMath.SegmentPassesWithin(start, destination, centre, reach * reach))
                {
                    // Ascending order, so the first match is the lowest ID.
                    return b;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether there is a spare bottle left to put down. The deck asks, so
        /// a death never deals a card that has nothing behind it.
        /// </summary>
        public bool HasSpareExtinguisher
        {
            get
            {
                for (int b = 0; b < bodies.Length; b++)
                {
                    if (bodies[b].Dormant && kinds.Of(bodies[b].Kind).IsEquipment)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Stands one of the spare extinguishers on the floor where the player
        /// pointed, full of spray. Refuses if there is no spare left or the spot
        /// is not clear floor, and takes the lowest-numbered spare so a replay
        /// always picks the same bottle.
        /// </summary>
        public bool TryPlaceSpareExtinguisher(LogicalPosition spot, out int index)
        {
            index = -1;
            for (int b = 0; b < bodies.Length; b++)
            {
                if (bodies[b].Dormant && kinds.Of(bodies[b].Kind).IsEquipment)
                {
                    index = b;
                    break;
                }
            }

            if (index < 0 || !IsClearForItem(index, spot))
            {
                index = -1;
                return false;
            }

            PhysicsBody bottle = bodies[index];
            bottle.Dormant = false;
            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            world.SetSolid(index, true);
            world.Place(index, bottle.X, 0L, bottle.Z, bottle.Heading);
            bottle.Reading = world.Read(index);
            SetMotion(index, 0L, 0L, 0L);
            bottle.Spin = 0;
            bottle.Thrown = false;
            bottle.Fuel = context.Scenario.Extinguishers.FuelTicks;
            return true;
        }

        /// <summary>
        /// Sends every loose thing within <paramref name="radius"/> of a blast
        /// flying away from it, up as well as out, cartwheeling, in ascending
        /// ID order so a replay agrees. What somebody is holding or sitting on
        /// stays put, and so does the thing that went off.
        /// </summary>
        public void FlingFrom(LogicalPosition centre, int radius, int speed, int exceptIndex, ulong causeEventId)
        {
            long reach = radius;
            long strength = (long)speed * feel.BlastStrengthPercent / 100L;
            using (Nearby near = Gather(UniformGridIndex.Around(centre, reach)))
            {
                for (int c = 0; c < near.Count; c++)
                {
                    int b = near[c];
                    PhysicsBody thing = bodies[b];
                    if (b == exceptIndex || thing.Dormant || thing.HeldBy >= 0 || thing.OccupiedBy >= 0 || IsFixedInPlace(b) ||
                        thing.Pinned)
                    {
                        continue;
                    }

                    if (LogicalPosition.DistanceSquared(thing.Position, centre) > reach * reach)
                    {
                        continue;
                    }

                    // Shared out by weight past the reference thing, as a
                    // table's shove is: a bin flies at the blast's full speed,
                    // a 45 kg set of shelves at under half of it, and a 160 kg
                    // vending machine rocks and stays put. Nothing lighter than
                    // the reference is slowed, so every box, chair and bag
                    // flies exactly as it did.
                    long share = Math.Min(strength, strength * BlastReferenceMassGrams / Math.Max(1, thing.MassGrams));
                    int away = IntegerMath.HeadingBetween(centre, thing.Position, thing.Heading);
                    LogicalPosition velocity = IntegerMath.Displacement(away, (int)share);
                    SetMotion(b,
                        (long)velocity.X * SubMillimetre,
                        share * SubMillimetre * feel.BlastLiftPercent / 100L,
                        (long)velocity.Z * SubMillimetre);
                    Tumble(b, away, (int)share, BlastTumbleMultiplier);
                    thing.Thrown = true;
                    thing.LastPushEventId = causeEventId;
                    context.Events.Append(context.Tick, thing.Id, CausalEventType.ItemThrown, thing.Position,
                        speed, 0, causeEventId, thing.Id);
                }
            }

            ShoveTablesFrom(centre, reach, strength, causeEventId);
        }

        /// <summary>
        /// The same blast against the tables. They are bodies like anything
        /// else, but heavy ones: a blast that sends a bin flying shifts a desk
        /// and barely rocks the meeting table, so what it gives each one is
        /// shared out by weight against a <see cref="BlastReferenceMassGrams"/>
        /// thing. Ascending table order, so a replay agrees.
        /// </summary>
        private void ShoveTablesFrom(LogicalPosition centre, long reach, long strength, ulong causeEventId)
        {
            for (int t = 0; t < geometry.TableCount; t++)
            {
                LogicalBounds bounds = geometry.TableBounds(t);
                LogicalPosition middle = bounds.Centre;
                if (LogicalPosition.DistanceSquared(middle, centre) > reach * reach)
                {
                    continue;
                }

                long massGrams = TableMassGrams(bounds);
                long share = Math.Min(strength, strength * BlastReferenceMassGrams / massGrams);
                int away = IntegerMath.HeadingBetween(centre, middle, 0);
                LogicalPosition velocity = IntegerMath.Displacement(away, (int)share);
                // No event: the log is about loose things and people, and a
                // shoved table hurts nobody by itself (what a table does to
                // whatever it meets is the building's business, as before).
                world.ShoveTable(t,
                    (long)velocity.X * SubMillimetre,
                    share * SubMillimetre * feel.BlastLiftPercent / 100L,
                    (long)velocity.Z * SubMillimetre);
            }
        }

        /// <summary>
        /// What a blast is measured against when it shoves a table: a thing of
        /// this weight is thrown at the blast's full speed, and a table twice
        /// as heavy gets half of it.
        /// </summary>
        private const long BlastReferenceMassGrams = 20000L;

        /// <summary>What a table weighs, as the physics was told: its top's area at the flammables' weight per square metre.</summary>
        private long TableMassGrams(LogicalBounds bounds)
        {
            return Math.Max(1000L,
                (long)(bounds.MaxX - bounds.MinX) * (bounds.MaxZ - bounds.MinZ) *
                context.Scenario.Flammables.TableMassGramsPerSquareMetre / 1000000L);
        }

        /// <summary>
        /// Somebody heaves a table out of their way: a change of speed the way
        /// they are going, delivered at the table's top edge nearest them, so a
        /// light desk goes over away from them and a heavy one slides. Shared
        /// out by weight as a blast's shove is, but never below the panic
        /// settings' least share, so even the meeting table shifts. Logged,
        /// because a table going over is a commotion the round should pay for
        /// and the story should tell.
        /// </summary>
        public void HeaveTable(Agent agent, int table, int heading, ulong causeEventId)
        {
            LogicalBounds bounds = geometry.TableBounds(table);
            PanicSettings panic = context.Scenario.Panic;
            long strength = (long)panic.TableHeaveSpeedMillimetresPerTick * feel.ThrowStrengthPercent / 100L;
            long share = Math.Max(strength * panic.TableHeaveLeastPercent / 100L,
                Math.Min(strength, strength * BlastReferenceMassGrams / TableMassGrams(bounds)));
            LogicalPosition velocity = IntegerMath.Displacement(heading, (int)share);
            LogicalPosition hands = bounds.ClosestPoint(agent.Body.Position);
            world.HeaveTable(table, hands, (long)velocity.X * SubMillimetre, (long)velocity.Z * SubMillimetre);
            context.Events.Append(context.Tick, agent.Id, CausalEventType.TableHeaved, hands,
                (int)share, 0, causeEventId, geometry.TableId(table));
        }

        /// <summary>
        /// Sets a flung thing turning end over end, away from where it was
        /// sent from: how fast comes from how hard it was sent and how small
        /// it is, times the tumble dial.
        /// </summary>
        private void Tumble(int index, int heading, int speed, int multiplier)
        {
            PhysicsBody thing = bodies[index];
            long turn = (long)SpinFromImpact(thing, speed) * multiplier * feel.TumblePercent / 100L;
            thing.Spin = (int)turn;
            if (turn == 0L)
            {
                return;
            }

            // Tipping forward along the direction of travel turns it about the
            // axis lying across that direction: up crossed with the way it goes.
            LogicalPosition direction = IntegerMath.Direction(heading);
            world.SetSpin(index,
                (int)(direction.Z * turn / IntegerMath.TrigScale),
                0,
                (int)(-direction.X * turn / IntegerMath.TrigScale));
        }

        /// <summary>Smashes a thing outright, whatever hit it: used by a blast.</summary>
        public void Wreck(int index, SimulationId brokenBy, ulong causeEventId)
        {
            TryWreck(index, long.MaxValue, brokenBy, causeEventId);
        }

        /// <summary>A push away from nearby objects, so people walk around boxes rather than into them.</summary>
        public void AddAvoidance(LogicalPosition position, int percent, ref long steerX, ref long steerZ)
        {
            int margin = context.Scenario.Steering.ObjectAvoidMarginMillimetres;
            long furthest = (long)personRadius + widestRadius + margin;
            using Nearby candidates = Gather(UniformGridIndex.Around(position, furthest));
            for (int c = 0; c < candidates.Count; c++)
            {
                int b = candidates[c];
                if (IsOutOfPlay(b))
                {
                    continue;
                }

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
        /// A person ran into a thing. The engine has already done the pushing,
        /// the person's weight and speed against the thing's; what is left is
        /// what it means. A frightened person's bump goes in the log, and from
        /// then on the thing's own hits are theirs to answer for. They may trip
        /// over it, the more likely the faster they are and the bigger it is.
        /// A strong frightened runner grabs it and hurls it out of the way
        /// instead. Never the thing they are on their way to pick up: they
        /// reach for that rather than punting it across the room.
        /// </summary>
        private void PersonRanInto(Agent agent, int index, int closing, LogicalPosition point)
        {
            PhysicsBody thing = bodies[index];
            if (agent.Body.State != AgentBodyState.Upright || thing.HeldBy >= 0 || index == agent.Carry.ItemIndex ||
                closing < LoggedBumpSpeed || agent.Fear.State != AgentFearState.Scared)
            {
                return;
            }

            FallSettings falls = context.Scenario.Falls;
            if (!agent.Burning.IsBurning && closing >= falls.BumpMinimumSpeed &&
                agent.Traits.Strength >= items.HurlMinimumStrength && CanLift(agent, index) && !IsFixedInPlace(index))
            {
                Hurl(agent, index);
                return;
            }

            ulong bumpEventId = context.Events.Append(context.Tick, agent.Id, CausalEventType.BoxBumped, point,
                closing, 0, agent.Fear.ScaredEventId, thing.Id).EventId;
            thing.LastPushEventId = bumpEventId;

            bool trip = false;
            if (closing >= settings.TripMinimumSpeed)
            {
                long chance = (long)(closing - settings.TripMinimumSpeed + 10) * thing.Size / settings.TripScale;
                trip = context.Random.NextPercent((int)Math.Min(settings.TripMaximumChancePercent, chance));
            }

            if (trip)
            {
                people.FallTowards(agent, agent.Body.Heading);
                body.Trip(agent, bumpEventId);
            }
            else if (closing >= falls.BumpMinimumSpeed)
            {
                sound.Thud(agent.Id, point, bumpEventId);
            }
        }

        /// <summary>
        /// Which way a frightened person flings whatever they were holding:
        /// roughly where they are already facing, veering to one side or the
        /// other, because it is a reflex and not a plan. The cruel are the
        /// exception: they aim it at the nearest person.
        /// </summary>
        public int PanicThrowHeading(Agent thrower, LogicalPosition from)
        {
            int spread = items.PanicThrowSpreadDegrees;
            int heading = thrower.Body.Heading + context.Random.NextIntInclusive(-spread, spread);
            if (thrower.Traits.Evil < items.EvilAimMinimum)
            {
                return heading;
            }

            Agent victim = NearestOtherPerson(thrower, from, items.AimRangeMillimetres);
            return victim == null ? heading : IntegerMath.HeadingBetween(from, victim.Body.Position, heading);
        }

        /// <summary>
        /// A strong runner flings an item in their way: aside, or, if they
        /// are cruel enough, straight at the nearest other person. They lose
        /// half their speed doing it.
        /// </summary>
        private void Hurl(Agent agent, int index)
        {
            PhysicsBody item = bodies[index];
            int heading = context.Random.NextIntInclusive(0, 1) == 0 ? agent.Body.Heading + 90 : agent.Body.Heading - 90;
            if (agent.Traits.Evil >= items.EvilAimMinimum)
            {
                Agent victim = NearestOtherPerson(agent, item.Position, items.AimRangeMillimetres);
                if (victim != null)
                {
                    heading = IntegerMath.HeadingBetween(item.Position, victim.Body.Position, heading);
                }
            }

            Fling(agent, index, heading, agent.Fear.ScaredEventId);
        }

        /// <summary>
        /// Whether somebody can grab this thing and throw it clear of their way
        /// out: loose on the floor, not fixed to the wall, not held, nobody
        /// sitting on it, and light enough for them.
        /// </summary>
        public bool CanThrowClear(Agent agent, int index)
        {
            PhysicsBody thing = bodies[index];
            return !IsOutOfPlay(index) && !IsFixedInPlace(index) && thing.HeldBy < 0 && CanLift(agent, index);
        }

        /// <summary>
        /// Self-preservation: somebody whose way out is blocked by a thing grabs
        /// it and throws it clear, towards <paramref name="heading"/>. Anybody
        /// who can lift it does this, not only the strong, and nobody has to be
        /// told to. <paramref name="causeEventId"/> is what drove them to it.
        /// </summary>
        public void ThrowClear(Agent agent, int index, int heading, ulong causeEventId)
        {
            Fling(agent, index, heading, causeEventId);
        }

        /// <summary>
        /// Snatches a thing off the floor and throws it from the hip, towards
        /// <paramref name="heading"/>: up and over rather than skimming along
        /// the ground. They lose half their speed doing it.
        /// </summary>
        private void Fling(Agent agent, int index, int heading, ulong causeEventId)
        {
            PhysicsBody item = bodies[index];
            Unpin(index);
            int speed = ThrowSpeed(agent, index);
            CausalEvent thrown = context.Events.Append(context.Tick, agent.Id, CausalEventType.ItemThrown, item.Position,
                speed, 0, causeEventId, item.Id);

            // Snatched off the floor and flung from the hip: it goes up and
            // over rather than skimming along the ground.
            long strength = (long)speed * SubMillimetre * feel.ThrowStrengthPercent / 100L;
            LogicalPosition direction = IntegerMath.Direction(heading);
            SetMotion(index,
                direction.X * strength / IntegerMath.TrigScale,
                strength * IntegerMath.Sin(feel.ThrowArcDegrees) / IntegerMath.Cos(feel.ThrowArcDegrees),
                direction.Z * strength / IntegerMath.TrigScale);
            item.Thrown = true;
            item.LastPushEventId = thrown.EventId;

            // Which way it turns is a coin toss, but how fast comes from the
            // throw: a lobbed thing turns lazily, a hurled one whips round.
            int side = context.Random.NextIntInclusive(0, 1) == 0 ? 1 : -1;
            Tumble(index, IntegerMath.NormalizeDegrees(heading + (side > 0 ? 0 : 180)), speed, 1);
            agent.Body.Speed /= 2;
        }

        private Agent NearestOtherPerson(Agent thrower, LogicalPosition from, int range)
        {
            Agent nearest = null;
            long best = (long)range * range;
            using Crowd.Nearby candidates = crowd.Within(from, range);
            for (int c = 0; c < candidates.Count; c++)
            {
                Agent other = crowd.All[candidates[c]];
                if (other == thrower || !other.IsParticipating)
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(from, other.Body.Position);
                if (distance < best)
                {
                    best = distance;
                    nearest = other;
                }
            }

            return nearest;
        }

        // ---------------------------------------------------------------- after the engine's step

        /// <summary>
        /// Phase 8, once the engine has stepped: every thing's new place is
        /// read back in ascending ID order, then every collision the engine
        /// reported is judged, in the sorted order it gives them. A thing
        /// flying into a person may stagger or floor them; two things meeting
        /// hard enough smash what was hit (furniture never smashes). The
        /// bouncing itself has already happened.
        /// </summary>
        public void AfterStep()
        {
            // What each thing carried into the step, before this step's
            // bounces: a hit is judged by how it came in, not how it left.
            (long X, long Y, long Z)[] before = velocityBefore;
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody thing = bodies[b];
                before[b] = (thing.VelocityX, thing.VelocityY, thing.VelocityZ);
                if (thing.HeldBy >= 0 || thing.Dormant)
                {
                    continue;
                }

                PhysicsWorld.Reading reading = world.Read(b);
                thing.Reading = reading;
                // Where it is across the floor is its middle, not its base: a
                // chair knocked onto its back is where its bulk now lies.
                if (reading.CentreX != thing.X || reading.CentreZ != thing.Z)
                {
                    MoveBody(b, reading.CentreX, reading.CentreZ);
                }

                thing.VelocityX = reading.VelocityX;
                thing.VelocityY = reading.VelocityY;
                thing.VelocityZ = reading.VelocityZ;
                thing.Heading = reading.Heading;
                thing.Spin = reading.SpinDegreesPerTick;
                if (thing.Thrown && !IsMoving(b))
                {
                    thing.Thrown = false;
                }
            }

            PopWhateverWentOver();
            ClearOfTheirHands();

            IReadOnlyList<PhysicsWorld.Contact> touched = world.Contacts;
            for (int c = 0; c < touched.Count; c++)
            {
                PhysicsWorld.Contact contact = touched[c];
                if (!contact.Began || contact.BodyA >= bodies.Length)
                {
                    continue;
                }

                Agent agent = contact.BodyB >= 0 ? people.PersonAt(contact.BodyB) : null;
                if (agent != null)
                {
                    if (agent.IsParticipating)
                    {
                        ThingMetPerson(contact.BodyA, before[contact.BodyA], agent, contact);
                    }
                }
                else if (contact.BodyB >= 0)
                {
                    HitObject(contact.BodyA, before[contact.BodyA], contact.BodyB, before[contact.BodyB], contact);
                }
            }
        }

        /// <summary>Anything now clear of whoever let go of it is solid to them again.</summary>
        private void ClearOfTheirHands()
        {
            for (int b = 0; b < leftHandsOf.Length; b++)
            {
                int handle = leftHandsOf[b];
                if (handle < 0)
                {
                    continue;
                }

                Agent person = people.PersonAt(handle);
                long apart = (long)personRadius + bodies[b].Radius + ClearOfTheHandsMillimetres;
                if (bodies[b].HeldBy >= 0 ||
                    LogicalPosition.DistanceSquared(person.Body.Position, bodies[b].Position) >= apart * apart)
                {
                    world.IgnoreEachOther(b, handle, false);
                    leftHandsOf[b] = -1;
                }
            }
        }

        /// <summary>How fast a velocity is, in hundredths of a millimetre per tick.</summary>
        private static long SpeedOf((long X, long Y, long Z) velocity) =>
            IntegerMath.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z);

        /// <summary>
        /// A thing and a person met. Whichever was coming on faster along the
        /// line between them did the running into: a thing flying at somebody
        /// may knock them over, somebody running into a thing may trip on it.
        /// </summary>
        private void ThingMetPerson(int index, (long X, long Y, long Z) velocity, Agent agent, PhysicsWorld.Contact contact)
        {
            LogicalPosition centre = bodies[index].Position;
            long nx = (long)agent.Body.Position.X - centre.X;
            long nz = (long)agent.Body.Position.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            (long personX, long personZ) = people.VelocityOf(agent);
            long thingComing = (velocity.X * nx + velocity.Z * nz) / length;
            long personComing = -(personX * nx + personZ * nz) / length;
            if (thingComing > personComing)
            {
                HitAgent(index, velocity, agent, contact);
                return;
            }

            PersonRanInto(agent, index, (int)((thingComing + personComing) / SubMillimetre), contact.Point);
        }

        /// <summary>
        /// A thing met a person. Only a thing coming at them counts: one they
        /// walked into themselves is their bump, judged when they made it. A
        /// heavy, fast one knocks them off balance or trips them; a light or
        /// slow one does nothing to them.
        /// </summary>
        private void HitAgent(int index, (long X, long Y, long Z) velocity, Agent agent, PhysicsWorld.Contact contact)
        {
            PhysicsBody physicsBody = bodies[index];
            LogicalPosition centre = physicsBody.Position;
            long nx = (long)agent.Body.Position.X - centre.X;
            long nz = (long)agent.Body.Position.Z - centre.Z;
            long length = IntegerMath.Sqrt(nx * nx + nz * nz);
            if (length == 0L)
            {
                return;
            }

            (long agentVelocityX, long agentVelocityZ) = people.VelocityOf(agent);
            long closing = ((velocity.X - agentVelocityX) * nx + (velocity.Z - agentVelocityZ) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            // Only a thing something set moving can hurt anybody: a box a
            // passer-by's shins nudged along has no cause, and it is too slow
            // to matter anyway.
            if (physicsBody.LastPushEventId == 0UL)
            {
                return;
            }

            // Kilograms times millimetres per tick. A thrown item strikes the body, not the feet.
            long momentum = physicsBody.MassGrams * closing / (1000L * SubMillimetre);
            if (physicsBody.Thrown)
            {
                momentum *= items.ThrowHitMultiplier;
                physicsBody.Thrown = false;
            }

            if (agent.Body.State != AgentBodyState.Upright || momentum < settings.StaggerMomentum)
            {
                return;
            }

            LogicalPosition point = contact.Point;
            CausalEvent hit = context.Events.Append(context.Tick, physicsBody.Id, CausalEventType.BoxHitAgent, point,
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

        /// <summary>
        /// Two things met. The faster one is the one that did the hitting: it
        /// passes on its cause, is logged if it came in fast, and smashes what
        /// it hit if it came in hard enough.
        /// </summary>
        private void HitObject(int first, (long X, long Y, long Z) firstVelocity, int second,
            (long X, long Y, long Z) secondVelocity, PhysicsWorld.Contact contact)
        {
            bool firstHits = SpeedOf(firstVelocity) >= SpeedOf(secondVelocity);
            int hitter = firstHits ? first : second;
            int struck = firstHits ? second : first;
            (long X, long Y, long Z) hitterVelocity = firstHits ? firstVelocity : secondVelocity;
            (long X, long Y, long Z) struckVelocity = firstHits ? secondVelocity : firstVelocity;
            PhysicsBody physicsBody = bodies[hitter];
            PhysicsBody other = bodies[struck];

            // As with people: only a thing something set moving smashes what it
            // meets, and only it passes a cause on.
            if (physicsBody.LastPushEventId == 0UL)
            {
                return;
            }

            long nx = other.X - physicsBody.X;
            long ny = other.Reading.Y - physicsBody.Reading.Y;
            long nz = other.Z - physicsBody.Z;
            long length = IntegerMath.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length == 0L)
            {
                return;
            }

            long closing = ((hitterVelocity.X - struckVelocity.X) * nx + (hitterVelocity.Y - struckVelocity.Y) * ny +
                            (hitterVelocity.Z - struckVelocity.Z) * nz) / length;
            if (closing <= 0L)
            {
                return;
            }

            if (closing >= (long)settings.LoggedBoxHitSpeed * SubMillimetre)
            {
                other.LastPushEventId = context.Events.Append(context.Tick, physicsBody.Id, CausalEventType.BoxesCollided,
                    contact.Point, (int)(closing / SubMillimetre), 0, physicsBody.LastPushEventId, other.Id).EventId;
            }
            else
            {
                other.LastPushEventId = physicsBody.LastPushEventId;
            }

            // A hard enough knock smashes what was hit.
            long momentum = physicsBody.MassGrams * closing / (1000L * SubMillimetre);
            TryWreck(struck, momentum, physicsBody.Id, other.LastPushEventId);
        }

        /// <summary>
        /// Smashes a thing if the blow was hard enough for its kind: it
        /// collapses into smaller, lighter wreckage that people still trip
        /// over but nobody can sit on. Wreckage cannot be smashed twice.
        /// </summary>
        private void TryWreck(int index, long momentum, SimulationId brokenBy, ulong causeEventId)
        {
            PhysicsBody target = bodies[index];
            int breakMomentum = context.Scenario.Flammables.Of(target.Kind).BreakMomentum;
            if (target.Wrecked || breakMomentum <= 0 || momentum < breakMomentum || target.OccupiedBy >= 0)
            {
                return;
            }

            target.Wrecked = true;
            target.Size = Math.Max(100, target.Size * 3 / 4);
            target.Radius = target.Size / 2;
            target.MassGrams = Math.Max(1000, target.MassGrams / 2);
            world.Resize(index, 75);
            world.SetMass(index, target.MassGrams);
            context.Events.Append(context.Tick, target.Id, CausalEventType.ObjectBroke, target.Position,
                (int)Math.Min(int.MaxValue, momentum), 0, causeEventId, brokenBy);
            sound.Thud(target.Id, target.Position, target.LastPushEventId);
        }

        /// <summary>
        /// How fast a thing turns after being struck, in whole degrees per tick.
        /// It comes from the speed it was given and its size: a light laptop
        /// skimmed across a desk whips round, a heavy potted plant barely turns
        /// at all. The result is never more than <c>SpinMaximum</c>.
        /// </summary>
        private int SpinFromImpact(PhysicsBody physicsBody, int speed)
        {
            if (speed <= 0)
            {
                return 0;
            }

            long step = context.Scenario.World.MaximumStepDistanceMillimetres;
            long radius = Math.Max(1, physicsBody.Radius);
            long spin = (long)settings.SpinMaximum * speed * SpinReferenceRadiusMillimetres / (step * radius);
            return (int)Math.Max(0L, Math.Min(settings.SpinMaximum, spin));
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && (value < 0L) != (divisor < 0L) ? quotient - 1L : quotient;
        }

        public PhysicsObjectSnapshot GetSnapshot(int index)
        {
            PhysicsBody physicsBody = bodies[index];
            long speed = IntegerMath.Sqrt(physicsBody.VelocityX * physicsBody.VelocityX + physicsBody.VelocityZ * physicsBody.VelocityZ);
            PhysicsWorld.Reading reading = physicsBody.Reading;
            return new PhysicsObjectSnapshot(
                physicsBody.Id,
                physicsBody.Kind,
                physicsBody.Position,
                physicsBody.Size,
                physicsBody.Heading,
                (int)(speed / SubMillimetre),
                heldBy: physicsBody.HeldBy >= 0 ? crowd.All[physicsBody.HeldBy].Id : default,
                thrown: physicsBody.Thrown,
                occupiedBy: physicsBody.OccupiedBy >= 0 ? crowd.All[physicsBody.OccupiedBy].Id : default,
                dormant: physicsBody.Dormant,
                wrecked: physicsBody.Wrecked,
                resting: IsResting(index),
                pose: physicsBody.HeldBy >= 0 || physicsBody.Dormant
                    ? default
                    : new BodyPose((int)(reading.Y / SubMillimetre),
                        reading.RotationX, reading.RotationY, reading.RotationZ, reading.RotationW, reading.Position));
        }

        public PhysicsObjectSnapshot[] GetSnapshots()
        {
            var snapshots = new PhysicsObjectSnapshot[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                snapshots[i] = GetSnapshot(i);
            }

            return snapshots;
        }
    }
}
