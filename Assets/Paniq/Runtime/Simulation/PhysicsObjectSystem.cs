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
    ///
    /// Objects are also items: a person can pick one up (it then leaves the
    /// floor and is carried in front of them, touching nothing), set it
    /// down, drop it or throw it. A strong runner who meets an item hurls it
    /// out of the way instead of kicking it. A thrown item hits people harder
    /// than a sliding one, because it strikes the body, not the feet.
    /// </summary>
    internal sealed class PhysicsObjectSystem
    {
        /// <summary>Object positions and velocities are kept in hundredths of a millimetre.</summary>
        /// <summary>Object positions and velocities are kept in hundredths of a millimetre.</summary>
        public const int SubMillimetre = 100;

        /// <summary>Resolution of the halving search for where a sliding object first touches something.</summary>
        private const int ContactSearchSteps = 1024;

        /// <summary>
        /// The size of thing that turns at the full spin rate when it is sent
        /// off at full speed. Anything bigger turns more slowly in proportion,
        /// anything smaller faster, up to the cap.
        /// </summary>
        private const int SpinReferenceRadiusMillimetres = 200;

        /// <summary>
        /// How a thing coming off a table or a stack looks for clear floor: out
        /// from where it was in steps this long, up to this far.
        /// </summary>
        private const int FallSearchStepMillimetres = 50;
        private const int FallSearchReachMillimetres = 1500;

        /// <summary>How wide a patch of floor one cell of the index covers.</summary>
        private const int CellSizeMillimetres = 1000;

        /// <summary>How far past the rooms the index reaches, for things flung out of the building.</summary>
        private const int OutsideMarginMillimetres = 8000;

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

            public long VelocityX;
            public long VelocityZ;
            public int Radius;
            public int Size;
            public int MassGrams;
            public int Heading;
            public int Spin;

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
            /// Smashed. It is wreckage now: flatter, lighter, still something to
            /// trip over, but no longer a chair anybody can sit on.
            /// </summary>
            public bool Wrecked;

            /// <summary>
            /// Sitting on a table or on another object rather than on the floor:
            /// a laptop on a desk, the upper box of a stacked pair. It is not in
            /// anybody's way while it rests there and it does not slide, but it
            /// can still be picked up, burnt and blown off. The moment anything
            /// moves it, it comes loose and is an ordinary loose object again.
            /// </summary>
            public bool Resting;

            /// <summary>
            /// The object it is stacked on, or -1 when it rests on a table (or
            /// not at all). If that object moves, is lifted or is smashed, this
            /// one topples off it.
            /// </summary>
            public int RestsOn = -1;

            /// <summary>The event that last set this object moving, so its later hits can name their cause.</summary>
            public ulong LastPushEventId;

            /// <summary>The <c>DoorBlocked</c> event while this thing is jamming a door, so clearing it names the same door.</summary>
            public ulong BlockedEventId;

            public LogicalPosition Position => new LogicalPosition(
                (int)FloorDivide(X, SubMillimetre),
                (int)FloorDivide(Z, SubMillimetre));

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
        private readonly ObjectPhysicsSettings settings;

        /// <summary>What each kind of thing is made of and what it is for.</summary>
        private readonly FlammableSettings kinds;
        private readonly int personRadius;
        private readonly List<ObjectContact> contacts = new List<ObjectContact>();
        private readonly ItemSettings items;
        private readonly PhysicsBody[] bodies;

        /// <summary>Which patch of floor each thing is lying on.</summary>
        private readonly UniformGridIndex whereThingsAre;

        /// <summary>The largest thing in the scenario, so a question can be widened enough to catch it.</summary>
        private readonly int widestRadius;

        /// <summary>Reused by <see cref="Gather"/>, one per level of nesting.</summary>
        private readonly int[][] gathered;
        private int gatherDepth;

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
            kinds = context.Scenario.Flammables;
            items = context.Scenario.Items;
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
                    Radius = definition.RadiusMillimetres,
                    Size = definition.SizeMillimetres,
                    MassGrams = definition.MassGrams,
                    Dormant = definition.StartsDormant,
                    Heading = IntegerMath.NormalizeDegrees(definition.InitialFacingDegrees),
                    Resting = definition.StartsResting,

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
            }

            // Things are flung about, so the grid reaches well past the rooms.
            LogicalBounds area = geometry.FireArea;
            whereThingsAre = new UniformGridIndex(
                new LogicalBounds(
                    area.MinX - OutsideMarginMillimetres, area.MaxX + OutsideMarginMillimetres,
                    area.MinZ - OutsideMarginMillimetres, area.MaxZ + OutsideMarginMillimetres),
                CellSizeMillimetres,
                bodies.Length);

            gathered = new int[4][];
            for (int i = 0; i < gathered.Length; i++)
            {
                gathered[i] = new int[bodies.Length];
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                whereThingsAre.Place(i, bodies[i].Position);
            }

            FindWhatEachStackedThingStandsOn();
        }

        /// <summary>
        /// A thing authored as resting but not on a table is stacked on the
        /// floor object at its spot: the lowest-numbered one there, so a replay
        /// always agrees. Done once, at the start.
        /// </summary>
        private void FindWhatEachStackedThingStandsOn()
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody top = bodies[b];
                if (!top.Resting || geometry.TableAt(top.Position, 0) >= 0)
                {
                    continue;
                }

                for (int u = 0; u < bodies.Length; u++)
                {
                    PhysicsBody under = bodies[u];
                    if (u == b || under.Resting || under.Dormant)
                    {
                        continue;
                    }

                    long reach = under.Radius;
                    if (LogicalPosition.DistanceSquared(top.Position, under.Position) <= reach * reach)
                    {
                        top.RestsOn = u;
                        break;
                    }
                }
            }
        }

        public int Count => bodies.Length;

        public SimulationId IdOf(int index) => bodies[index].Id;

        /// <summary>The thing with this ID, or -1 if the run has no such thing.</summary>
        public int IndexOf(SimulationId id)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public PhysicsObjectKind KindOf(int index) => bodies[index].Kind;

        public LogicalPosition PositionOf(int index) => bodies[index].Position;

        public int RadiusOf(int index) => bodies[index].Radius;

        /// <summary>
        /// Equipment rather than clutter: kept where it is until somebody needs
        /// it. Nobody tidies it away, wedges a door with it, or drops it the
        /// moment they are frightened.
        /// </summary>
        public bool IsEquipment(int index) => kinds.Of(bodies[index].Kind).IsEquipment;

        /// <summary>The largest thing in the building, for widening a question enough to catch it.</summary>
        public int WidestRadius => widestRadius;

        /// <summary>
        /// Moves a thing, and tells the index of what is lying where at the
        /// same moment. Things slide during a tick and the next question about
        /// that patch of floor has to see where they got to, so this is kept up
        /// to date as they move rather than rebuilt once a tick.
        /// </summary>
        private void MoveBody(int index, long x, long z)
        {
            bodies[index].MoveWithoutTellingTheIndex(x, z);
            whereThingsAre.Place(index, bodies[index].Position);
        }

        /// <summary>
        /// Puts a loose thing beside a table if it has ended up inside one.
        ///
        /// Keeping things out of the furniture as they slide works by asking
        /// which side of the table they came from, and that cannot answer for a
        /// thing that did not slide in: one kicked while it stood on a table, or
        /// shoved a couple of millimetres over an edge by somebody's foot in a
        /// single tick. Those are rare and small, and they used to leave a
        /// laptop hanging in the middle of a desk with nothing to correct it.
        /// Checked once a tick for anything actually on the floor, so the rule
        /// "a loose thing is never inside a table" holds however it got there.
        /// </summary>
        private void KeepOutOfFurniture(int index)
        {
            PhysicsBody body = bodies[index];
            if (body.HeldBy >= 0 || body.Dormant || body.Resting)
            {
                return;
            }

            LogicalPosition where = body.Position;
            LogicalPosition beside = geometry.PushOutOfTables(where, where, body.Radius, out _, out _);

            // Only if there is really somewhere to put it. Shoving it off a
            // table edge into the box behind it would trade one thing standing
            // in the furniture for two things standing in each other, and in a
            // room with no room left the honest answer is to leave it where it
            // is and let the next kick sort it out.
            if (!beside.Equals(where) && IsClearForItem(index, beside))
            {
                MoveBody(index, (long)beside.X * SubMillimetre, (long)beside.Z * SubMillimetre);
            }
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

        public bool IsMoving(int index) => bodies[index].VelocityX != 0L || bodies[index].VelocityZ != 0L;

        public int MassOf(int index) => bodies[index].MassGrams;

        /// <summary>The index of the person carrying it, or -1.</summary>
        public int HolderOf(int index) => bodies[index].HeldBy;

        /// <summary>A spare the player has not put down yet.</summary>
        public bool IsDormant(int index) => bodies[index].Dormant;

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
                FireReactionEventType.DoorBlocked, doorCentre, 0, 0, bodies[index].LastPushEventId, doorId).EventId;
        }

        /// <summary>Whatever was jamming that door is clear of it again.</summary>
        public void RecordUnblocking(int index, SimulationId doorId, LogicalPosition doorCentre)
        {
            if (index < 0)
            {
                return;
            }

            context.Events.Append(context.Tick, bodies[index].Id, FireReactionEventType.DoorUnblocked, doorCentre,
                0, 0, bodies[index].BlockedEventId, doorId);
            bodies[index].BlockedEventId = 0UL;
        }

        /// <summary>
        /// Heaves a thing out of a doorway, along the wall rather than through
        /// the door, because a thing never passes through a doorway.
        /// </summary>
        public void ShoveAside(int index, Agent shover, int heading, int speed, ulong causeEventId)
        {
            PhysicsBody thing = bodies[index];
            FallOff(index, heading);
            LogicalPosition velocity = IntegerMath.Displacement(heading, speed);
            thing.VelocityX = (long)velocity.X * SubMillimetre;
            thing.VelocityZ = (long)velocity.Z * SubMillimetre;
            thing.Thrown = false;
            thing.LastPushEventId = context.Events.Append(context.Tick, shover.Id,
                FireReactionEventType.AgentShovedObstruction, thing.Position, speed, 0, causeEventId, thing.Id).EventId;
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
        /// arms, or a spare that is not in the world yet. Both are skipped by
        /// collisions, avoidance, movement and anything looking for something to
        /// pick up.
        /// </summary>
        private bool IsOutOfPlay(int index) =>
            bodies[index].HeldBy >= 0 || bodies[index].Dormant || bodies[index].Resting;

        /// <summary>Whether this thing is resting on a table or on another object rather than on the floor.</summary>
        public bool IsResting(int index) => bodies[index].Resting;

        /// <summary>
        /// It is lifted straight off whatever held it up: into somebody's arms,
        /// where its floor position no longer matters.
        /// </summary>
        private void LiftOff(int index)
        {
            PhysicsBody body = bodies[index];
            body.Resting = false;
            body.RestsOn = -1;

            // It stood on a table, so its position is inside one until
            // something moves it off.
            KeepOutOfFurniture(index);
        }

        /// <summary>
        /// It comes off whatever held it up and lands on clear floor beside it:
        /// off the edge of the table it stood on, or off the side of the box it
        /// was stacked on, never inside either. It looks outward from where it
        /// was, trying the way it was being sent first, then turning further
        /// and further from it; if nowhere within reach is clear it stays put.
        /// Anything that sends a resting thing moving calls this first, so a
        /// laptop is never shoved about while still on a desk. Whole numbers
        /// and a fixed search order, so a replay lands it in the same place.
        /// </summary>
        private void FallOff(int index, int preferredHeading)
        {
            PhysicsBody body = bodies[index];
            if (!body.Resting)
            {
                return;
            }

            LiftOff(index);
            LogicalPosition from = body.Position;
            if (IsClearForItem(index, from))
            {
                return;
            }

            for (int distance = FallSearchStepMillimetres; distance <= FallSearchReachMillimetres;
                 distance += FallSearchStepMillimetres)
            {
                for (int turn = 0; turn <= 180; turn += 45)
                {
                    for (int side = 1; side >= -1; side -= 2)
                    {
                        if (side < 0 && (turn == 0 || turn == 180))
                        {
                            continue;
                        }

                        LogicalPosition spot = from + IntegerMath.Displacement(preferredHeading + side * turn, distance);
                        if (IsClearForItem(index, spot))
                        {
                            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Phase 8, first: a box stacked on another topples off when the one
        /// under it is kicked away, picked up or smashed, rather than being left
        /// hanging in the air where the lower box used to be. It falls the
        /// opposite way to where the lower box went.
        /// </summary>
        private void TopplePilesWhoseBaseMoved()
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody top = bodies[b];
                if (!top.Resting || top.RestsOn < 0)
                {
                    continue;
                }

                PhysicsBody under = bodies[top.RestsOn];
                bool moving = under.VelocityX != 0L || under.VelocityZ != 0L;
                if (!moving && under.HeldBy < 0 && !under.Wrecked && !under.Dormant)
                {
                    continue;
                }

                int away = moving
                    ? IntegerMath.HeadingOf(-under.VelocityX, -under.VelocityZ, top.Heading)
                    : top.Heading;
                FallOff(b, away);

                // Whatever knocked the lower box also knocked this one down, so
                // anything it goes on to do (jam a doorway, trip somebody) can
                // name that as its cause.
                top.LastPushEventId = under.LastPushEventId;
            }
        }

        /// <summary>
        /// Everything resting inside these bounds drops where it is: what a
        /// table was holding up when it collapsed. The table is rubble now, not
        /// something solid, so there is floor for them right there.
        ///
        /// Except where there is not. A long table can be authored as two
        /// rectangles side by side, and collapsing one of them leaves anything
        /// near the seam standing inside the half that is still up. So each
        /// thing is pushed clear of whatever is still solid as it drops.
        /// </summary>
        public void LooseEverythingRestingIn(LogicalBounds bounds)
        {
            for (int b = 0; b < bodies.Length; b++)
            {
                if (!bodies[b].Resting || bodies[b].RestsOn >= 0 || !bounds.ContainsCircle(bodies[b].Position, 0))
                {
                    continue;
                }

                LiftOff(b);
            }
        }

        /// <summary>The person sitting on this chair, or -1.</summary>
        public int OccupantOf(int index) => bodies[index].OccupiedBy;

        /// <summary>Ticks of spray left in an extinguisher.</summary>
        public int FuelOf(int index) => bodies[index].Fuel;

        /// <summary>Uses up a tick of spray.</summary>
        public void UseFuel(int index, int ticks)
        {
            bodies[index].Fuel = System.Math.Max(0, bodies[index].Fuel - ticks);
        }

        /// <summary>Whether this is a chair nobody is on, nobody is carrying, and that is standing still.</summary>
        public bool IsFreeChair(int index)
        {
            PhysicsBody body = bodies[index];
            return kinds.Of(body.Kind).CanBeSatOn &&
                   body.OccupiedBy < 0 && body.HeldBy < 0 && !body.Dormant && !body.Wrecked && !IsMoving(index);
        }

        /// <summary>Someone sits down on a chair: it stops dead and stays put until they get up.</summary>
        public void SitOn(int index, Agent sitter)
        {
            PhysicsBody body = bodies[index];
            body.OccupiedBy = sitter.Index;
            body.VelocityX = 0L;
            body.VelocityZ = 0L;
            body.Spin = 0;
            body.Thrown = false;
        }

        /// <summary>They get up: the chair is loose again, and shoved back a little as they stand.</summary>
        public void StandUp(int index, int shoveX, int shoveZ)
        {
            PhysicsBody body = bodies[index];
            body.OccupiedBy = -1;
            body.VelocityX = shoveX;
            body.VelocityZ = shoveZ;
        }

        // ---------------------------------------------------------------- items

        /// <summary>Whether this person could lift this item at all (items are boxes and chairs; the limit grows with strength).</summary>
        public bool CanLift(Agent agent, int index)
        {
            return bodies[index].OccupiedBy < 0 &&
                   bodies[index].MassGrams <= TraitEffects.CarryLimitGrams(agent, context.Scenario);
        }

        /// <summary>Takes an item off the floor into someone's arms. It stops moving and touches nothing while held.</summary>
        public void PickUp(int index, Agent carrier)
        {
            PhysicsBody item = bodies[index];
            LiftOff(index);
            item.HeldBy = carrier.Index;
            item.VelocityX = 0L;
            item.VelocityZ = 0L;
            item.Spin = 0;
            item.Thrown = false;
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

        private bool IsClearForItem(int index, LogicalPosition spot)
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
                    Agent other = crowd.All[people[c]];
                    if (other.IsParticipating &&
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
        /// Puts a held item back on the floor at <paramref name="spot"/>
        /// (found with <see cref="FindSpotToPutDown"/>), still, or flying off
        /// at a velocity (mm per tick) when thrown.
        /// </summary>
        public void Release(int index, LogicalPosition spot, int velocityX, int velocityZ, ulong causeEventId)
        {
            PhysicsBody item = bodies[index];
            item.HeldBy = -1;
            MoveBody(index, (long)spot.X * SubMillimetre, (long)spot.Z * SubMillimetre);
            item.VelocityX = (long)velocityX * SubMillimetre;
            item.VelocityZ = (long)velocityZ * SubMillimetre;
            item.Thrown = velocityX != 0 || velocityZ != 0;
            item.LastPushEventId = causeEventId;
        }

        /// <summary>How fast (mm per tick) this person can throw this item: stronger people and lighter items fly faster.</summary>
        public int ThrowSpeed(Agent agent, int index)
        {
            long speed = (long)items.ThrowImpulse * (agent.Traits.Strength + 5) * 1000L / (bodies[index].MassGrams + 5000L);
            return (int)Math.Max(items.ThrowMinimumSpeed,
                Math.Min(context.Scenario.World.MaximumStepDistanceMillimetres, speed));
        }

        public void BeginTick()
        {
            contacts.Clear();
        }

        /// <summary>Tests only: sets an object sliding at a velocity in millimetres per tick.</summary>
        public void Launch(int index, int velocityX, int velocityZ)
        {
            // Sending a thing off a desk or a stack takes it off there first,
            // like every other way of setting one moving.
            FallOff(index, IntegerMath.HeadingOf(velocityX, velocityZ, bodies[index].Heading));
            bodies[index].VelocityX = (long)velocityX * SubMillimetre;
            bodies[index].VelocityZ = (long)velocityZ * SubMillimetre;
        }

        /// <summary>The lowest-ID object a circle of this radius would pass through on this move, or -1.</summary>
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
            bottle.VelocityX = 0L;
            bottle.VelocityZ = 0L;
            bottle.Spin = 0;
            bottle.Thrown = false;
            bottle.Fuel = context.Scenario.Extinguishers.FuelTicks;
            return true;
        }

        /// <summary>
        /// Sends every loose thing within <paramref name="radius"/> of a blast
        /// flying away from it, in ascending ID order so a replay agrees. What
        /// somebody is holding or sitting on stays put, and so does the thing
        /// that went off.
        /// </summary>
        public void FlingFrom(LogicalPosition centre, int radius, int speed, int exceptIndex, ulong causeEventId)
        {
            long reach = radius;
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody thing = bodies[b];
                if (b == exceptIndex || thing.Dormant || thing.HeldBy >= 0 || thing.OccupiedBy >= 0)
                {
                    continue;
                }

                if (LogicalPosition.DistanceSquared(thing.Position, centre) > reach * reach)
                {
                    continue;
                }

                int away = IntegerMath.HeadingBetween(centre, thing.Position, thing.Heading);
                LogicalPosition velocity = IntegerMath.Displacement(away, speed);
                FallOff(b, away);
                thing.VelocityX = (long)velocity.X * SubMillimetre;
                thing.VelocityZ = (long)velocity.Z * SubMillimetre;
                thing.Thrown = true;
                thing.Spin = SpinFromImpact(thing, speed);
                thing.LastPushEventId = causeEventId;
                context.Events.Append(context.Tick, thing.Id, FireReactionEventType.ItemThrown, thing.Position,
                    speed, 0, causeEventId, thing.Id);
            }
        }

        /// <summary>
        /// A table takes the momentum of whatever just bounced off it, and
        /// collapses if that was hard enough. A smashed table stops being
        /// something people walk around, so the room opens up.
        /// </summary>
        private void TrySmashTable(PhysicsBody thrown, LogicalPosition from, LogicalPosition to)
        {
            int table = geometry.TableHit(from, to, thrown.Radius);
            if (table < 0)
            {
                return;
            }

            long speed = IntegerMath.Sqrt(thrown.VelocityX * thrown.VelocityX + thrown.VelocityZ * thrown.VelocityZ);
            long momentum = thrown.MassGrams * speed / (1000L * SubMillimetre);
            if (momentum < context.Scenario.Flammables.TableBreakMomentum)
            {
                return;
            }

            geometry.BreakTable(table);

            // Whatever the table was holding up drops to the floor with it.
            LooseEverythingRestingIn(geometry.TableBounds(table));
            context.Events.Append(context.Tick, geometry.TableId(table), FireReactionEventType.ObjectBroke,
                geometry.TableBounds(table).Centre, (int)Math.Min(int.MaxValue, momentum), 0,
                thrown.LastPushEventId, thrown.Id);
            sound.Thud(geometry.TableId(table), geometry.TableBounds(table).Centre, thrown.LastPushEventId);
        }

        /// <summary>Smashes a thing outright, whatever hit it: used by a blast.</summary>
        public void Wreck(int index, SimulationId brokenBy, ulong causeEventId)
        {
            TryWreck(bodies[index], long.MaxValue, brokenBy, causeEventId);
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

            // Never the thing they are on their way to pick up: they reach
            // for it rather than punting it across the room.
            int bodyIndex = FindBlocking(agent.Body.Position, destination, personRadius, agent.Carry.ItemIndex);
            if (bodyIndex < 0)
            {
                return false;
            }

            int closing = ClosingSpeed(agent, bodies[bodyIndex]);
            if (closing <= 0 || (fastOnly && closing < context.Scenario.Falls.BumpMinimumSpeed))
            {
                return false;
            }

            // A strong runner grabs it and hurls it out of the way rather than kicking it.
            bool hurl = fastOnly && agent.Fear.State == AgentFearState.Scared && !agent.Burning.IsBurning &&
                        agent.Traits.Strength >= items.HurlMinimumStrength && CanLift(agent, bodyIndex);
            contacts.Add(new ObjectContact(agent, bodyIndex, closing, hurl));
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
                if (contact.Hurl && physicsBody.HeldBy < 0)
                {
                    Hurl(agent, contact.BodyIndex);
                    continue;
                }

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

                // A glancing kick turns it; a square-on one does not. The turn
                // is set from this knock rather than added to whatever it was
                // already doing, so brushing past a box tick after tick cannot
                // wind it up into a spinning top.
                LogicalPosition direction = IntegerMath.Direction(agent.Body.Heading);
                long sideways = agent.Body.Speed * (direction.X * nz - direction.Z * nx) / (IntegerMath.TrigScale * length);
                int turn = SpinFromImpact(physicsBody, (int)Math.Abs(sideways));
                if (turn > Math.Abs(physicsBody.Spin))
                {
                    physicsBody.Spin = sideways < 0L ? -turn : turn;
                }

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

            int speed = ThrowSpeed(agent, index);
            LogicalPosition velocity = IntegerMath.Displacement(heading, speed);
            FallOff(index, heading);
            CausalEvent thrown = context.Events.Append(context.Tick, agent.Id, FireReactionEventType.ItemThrown, item.Position,
                speed, 0, agent.Fear.ScaredEventId, item.Id);
            item.VelocityX = (long)velocity.X * SubMillimetre;
            item.VelocityZ = (long)velocity.Z * SubMillimetre;
            item.Thrown = true;
            item.LastPushEventId = thrown.EventId;

            // Which way it turns is a coin toss, but how fast comes from the
            // throw: a lobbed thing turns lazily, a hurled one whips round.
            item.Spin = (context.Random.NextIntInclusive(0, 1) == 0 ? 1 : -1) * SpinFromImpact(item, speed);
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

        /// <summary>
        /// Phase 8: every moving object, in ascending ID order, slides along
        /// its velocity, stops at the first person or object in the way and
        /// bounces off it (or the walls), then slows with floor friction.
        /// </summary>
        public void Advance()
        {
            TopplePilesWhoseBaseMoved();
            for (int b = 0; b < bodies.Length; b++)
            {
                PhysicsBody physicsBody = bodies[b];

                // Carried in someone's arms, or with someone sitting on it:
                // it goes nowhere by itself.
                if (physicsBody.HeldBy >= 0 || physicsBody.OccupiedBy >= 0 || physicsBody.Dormant ||
                    physicsBody.Resting)
                {
                    continue;
                }

                // A thing on the floor turns only while it is still sliding, so
                // nothing is ever left spinning on the spot.
                if (physicsBody.VelocityX == 0L && physicsBody.VelocityZ == 0L)
                {
                    physicsBody.Spin = 0;
                    continue;
                }

                if (physicsBody.Spin != 0)
                {
                    physicsBody.Heading = IntegerMath.NormalizeDegrees(physicsBody.Heading + physicsBody.Spin);
                }

                LimitSpeed(physicsBody, (long)context.Scenario.World.MaximumStepDistanceMillimetres * SubMillimetre);
                long nextX = physicsBody.X + physicsBody.VelocityX;
                long nextZ = physicsBody.Z + physicsBody.VelocityZ;
                LogicalPosition before = physicsBody.Position;

                // Where it was heading before the walls and tables stopped it.
                // The smash has to be tested against that, because the clamped
                // position stops just short of whatever it ran into.
                var intended = new LogicalPosition((int)FloorDivide(nextX, SubMillimetre), (int)FloorDivide(nextZ, SubMillimetre));
                geometry.KeepObjectInRoom(physicsBody.Radius, SubMillimetre, physicsBody.X, physicsBody.Z,
                    ref nextX, ref nextZ, out bool hitX, out bool hitZ);
                if (hitX || hitZ)
                {
                    // Anything that slams into a table hard enough may smash it,
                    // whether it was hurled or merely kicked very hard.
                    TrySmashTable(physicsBody, before, intended);
                }

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
                    MoveBody(b, nextX, nextZ);
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
                    MoveBody(b, (long)stop.X * SubMillimetre, (long)stop.Z * SubMillimetre);
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
                if (agents[i].IsParticipating && agents[i].Index != physicsBody.HeldBy &&
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

            // A hard enough knock smashes what was hit.
            long momentum = physicsBody.MassGrams * closing / (1000L * SubMillimetre);
            TryWreck(other, momentum, physicsBody.Id, other.LastPushEventId);
        }

        /// <summary>
        /// Smashes a thing if the blow was hard enough for its kind: it
        /// collapses where it stands into lower, lighter wreckage that people
        /// still trip over but nobody can sit on. Wreckage cannot be smashed
        /// twice.
        /// </summary>
        private void TryWreck(PhysicsBody target, long momentum, SimulationId brokenBy, ulong causeEventId)
        {
            int breakMomentum = context.Scenario.Flammables.Of(target.Kind).BreakMomentum;
            if (target.Wrecked || breakMomentum <= 0 || momentum < breakMomentum || target.OccupiedBy >= 0)
            {
                return;
            }

            target.Wrecked = true;
            target.Size = Math.Max(100, target.Size * 3 / 4);
            target.Radius = target.Size / 2;
            target.MassGrams = Math.Max(1000, target.MassGrams / 2);
            context.Events.Append(context.Tick, target.Id, FireReactionEventType.ObjectBroke, target.Position,
                (int)Math.Min(int.MaxValue, momentum), 0, causeEventId, brokenBy);
            sound.Thud(target.Id, target.Position, target.LastPushEventId);
        }

        /// <summary>This kind of object's grip on the floor, per tick.</summary>
        private long FrictionFor(PhysicsBody physicsBody)
        {
            return Math.Max(1L, (long)settings.Friction * context.Scenario.Flammables.Of(physicsBody.Kind).FrictionPercent / 100L);
        }

        private void ApplyFriction(PhysicsBody physicsBody)
        {
            long friction = FrictionFor(physicsBody);
            long speed = IntegerMath.Sqrt(physicsBody.VelocityX * physicsBody.VelocityX + physicsBody.VelocityZ * physicsBody.VelocityZ);
            if (speed <= friction)
            {
                physicsBody.VelocityX = 0L;
                physicsBody.VelocityZ = 0L;
                physicsBody.Thrown = false;
                physicsBody.Spin = 0;
                return;
            }

            physicsBody.VelocityX = physicsBody.VelocityX * (speed - friction) / speed;
            physicsBody.VelocityZ = physicsBody.VelocityZ * (speed - friction) / speed;

            // The same drag that slows it down slows its turn, so a thing
            // coasting to a halt stops turning as it settles rather than
            // snapping still.
            physicsBody.Spin = (int)(physicsBody.Spin * (speed - friction) / speed);
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
                (int)(speed / SubMillimetre),
                heldBy: physicsBody.HeldBy >= 0 ? crowd.All[physicsBody.HeldBy].Id : default,
                thrown: physicsBody.Thrown,
                occupiedBy: physicsBody.OccupiedBy >= 0 ? crowd.All[physicsBody.OccupiedBy].Id : default,
                dormant: physicsBody.Dormant,
                wrecked: physicsBody.Wrecked,
                resting: physicsBody.Resting);
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
            public ObjectContact(Agent agent, int bodyIndex, int closingSpeed, bool hurl)
            {
                Agent = agent;
                BodyIndex = bodyIndex;
                ClosingSpeed = closingSpeed;
                Hurl = hurl;
            }

            public Agent Agent { get; }
            public int BodyIndex { get; }
            public int ClosingSpeed { get; }

            /// <summary>Grabbed and flung aside rather than kicked.</summary>
            public bool Hurl { get; }
        }
    }
}
