using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Things catching fire: boxes, chairs and tables. Anything close to
    /// flames (a burning square or another burning thing) heats up, and once
    /// it has been hot for long enough it bursts into flames. A burning thing
    /// sets the floor square under it alight once it has rested there for a
    /// moment, so a burning box kicked across the room spreads the fire where
    /// it lands. It sets alight anyone who touches it, and anyone on fire who
    /// touches it sets it alight. After a while it burns out and is left
    /// charred: still solid, never burning again. This system owns every
    /// thing's burn state; the physics and the world geometry still own
    /// where things are.
    /// </summary>
    internal sealed class FlammablesSystem
    {
        private sealed class Flammable
        {
            public SimulationId Id;
            public bool IsTable;
            public int Index;

            /// <summary>Its place in the list of things, which is also its place in the burning list.</summary>
            public int Slot;
            public int IgniteTicks;
            public int BurnMinimumTicks;
            public int BurnMaximumTicks;
            public ObjectBurnState State;
            public int Heat;
            public int BurnEndTick;
            public ulong EventId;
            public int RestCell = -1;
            public int RestTicks;
        }

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly PhysicsObjectSystem objects;
        private readonly BodySystem body;
        private readonly SoundSystem sound;
        private readonly FlammableSettings settings;
        private readonly int personRadius;

        /// <summary>Loose objects first (ascending ID), then tables (ascending ID): the order every pass uses.</summary>
        private readonly Flammable[] things;

        /// <summary>
        /// The slots of everything currently alight, ascending, kept up to date
        /// as things catch and go out. Looking for what is heating a thing means
        /// reading this rather than every object and table in the building, and
        /// because it stays in the same order as <see cref="things"/> the answer
        /// -- which fire gets the blame -- is the one a full scan would give.
        /// </summary>
        private readonly List<int> alight = new List<int>();

        public FlammablesSystem(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            FireSystem fire,
            PhysicsObjectSystem objects,
            BodySystem body,
            SoundSystem sound)
        {
            this.sound = sound;
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.fire = fire;
            this.objects = objects;
            this.body = body;
            settings = context.Scenario.Flammables;
            personRadius = context.Scenario.World.OccupancyRadiusMillimetres;

            things = new Flammable[objects.Count + geometry.TableCount];
            for (int i = 0; i < objects.Count; i++)
            {
                ObjectKindSettings kind = settings.Of(objects.KindOf(i));
                things[i] = new Flammable
                {
                    Id = objects.IdOf(i),
                    Index = i,

                    // Nothing that takes no time to catch: 0 means it never does.
                    IgniteTicks = kind.IgniteTicks,
                    BurnMinimumTicks = kind.BurnMinimumTicks,
                    BurnMaximumTicks = kind.BurnMaximumTicks
                };

                things[i].Slot = i;
            }

            for (int t = 0; t < geometry.TableCount; t++)
            {
                things[objects.Count + t] = new Flammable
                {
                    Id = geometry.TableId(t),
                    IsTable = true,
                    Index = t,
                    IgniteTicks = settings.TableIgniteTicks,
                    BurnMinimumTicks = settings.TableBurnMinimumTicks,
                    BurnMaximumTicks = settings.TableBurnMaximumTicks
                };

                things[objects.Count + t].Slot = objects.Count + t;
            }
        }

        /// <summary>
        /// Phase 9, after the physical objects have moved: heat and ignite,
        /// burn out, light the floor, and pass flames between things and
        /// people. Each pass goes through things in their fixed order.
        /// </summary>
        public void Update()
        {
            if (!fire.Active)
            {
                return;
            }

            int tick = context.Tick;

            // Burning people set alight whatever they touch.
            Agent[] agents = crowd.All;
            for (int a = 0; a < agents.Length; a++)
            {
                Agent agent = agents[a];
                if (!agent.IsParticipating || !agent.Burning.IsBurning)
                {
                    continue;
                }

                for (int i = 0; i < things.Length; i++)
                {
                    if (things[i].State == ObjectBurnState.Intact && Touches(things[i], agent))
                    {
                        Ignite(things[i], agent.Burning.EventId);
                    }
                }
            }

            // Heat builds up in anything close to flames; enough heat and it catches.
            for (int i = 0; i < things.Length; i++)
            {
                Flammable thing = things[i];

                // Ignite time 0 means this thing never catches at all (a potted plant).
                if (thing.State != ObjectBurnState.Intact || thing.IgniteTicks <= 0)
                {
                    continue;
                }

                ulong source = HeatSource(thing);
                if (source == 0UL)
                {
                    continue;
                }

                thing.Heat++;
                if (thing.Heat >= thing.IgniteTicks)
                {
                    Ignite(thing, source);
                }
            }

            for (int i = 0; i < things.Length; i++)
            {
                Flammable thing = things[i];
                if (thing.State != ObjectBurnState.Burning)
                {
                    continue;
                }

                if (tick >= thing.BurnEndTick)
                {
                    StopBurning(thing);
                    context.Events.Append(tick, thing.Id, FireReactionEventType.ObjectBurntOut, PositionOf(thing), 0, 0,
                        thing.EventId);
                    continue;
                }

                LightTheFloor(thing);

                // Anyone touching it catches fire.
                for (int a = 0; a < agents.Length; a++)
                {
                    if (agents[a].IsParticipating && !agents[a].Burning.IsBurning && Touches(thing, agents[a]))
                    {
                        body.CatchFire(agents[a], thing.EventId);
                    }
                }
            }
        }

        /// <summary>
        /// A jet of water over an area: everything burning inside the cone
        /// from <paramref name="from"/> goes out, and is left charred.
        /// </summary>
        public void DouseWithin(LogicalPosition from, int reach, ulong causeEventId, int heading, int coneDegrees)
        {
            long reachSquared = (long)reach * reach;
            for (int i = 0; i < things.Length; i++)
            {
                Flammable thing = things[i];
                if (thing.State != ObjectBurnState.Burning)
                {
                    continue;
                }

                LogicalPosition where = PositionOf(thing);
                if (LogicalPosition.DistanceSquared(from, where) > reachSquared)
                {
                    continue;
                }

                int toIt = IntegerMath.HeadingBetween(from, where, heading);
                if (Math.Abs(IntegerMath.SignedAngleDifference(heading, toIt)) > coneDegrees)
                {
                    continue;
                }

                StopBurning(thing);
                context.Events.Append(context.Tick, thing.Id, FireReactionEventType.ObjectBurntOut, where, 0, 0, causeEventId);
            }
        }

        /// <summary>The event of the flames heating this thing (the earliest-lit square, or a burning thing), or 0 when nothing is close.</summary>
        private ulong HeatSource(Flammable thing)
        {
            int reach = settings.HeatDistanceMillimetres;
            ulong cell = thing.IsTable
                ? fire.FindTouchingBounds(Grow(geometry.TableBounds(thing.Index), reach))
                : fire.FindTouchingCircle(objects.PositionOf(thing.Index), objects.RadiusOf(thing.Index) + reach);
            if (cell != 0UL)
            {
                return cell;
            }

            for (int i = 0; i < alight.Count; i++)
            {
                Flammable other = things[alight[i]];
                if (other != thing && Gap(thing, other) <= reach)
                {
                    return other.EventId;
                }
            }

            return 0UL;
        }

        /// <summary>Marks a thing alight and adds it to the burning list, keeping that list ascending.</summary>
        private void StartBurning(Flammable thing)
        {
            thing.State = ObjectBurnState.Burning;
            int at = alight.Count;
            while (at > 0 && alight[at - 1] > thing.Slot)
            {
                at--;
            }

            alight.Insert(at, thing.Slot);
        }

        /// <summary>Marks a thing burnt out and takes it off the burning list.</summary>
        private void StopBurning(Flammable thing)
        {
            thing.State = ObjectBurnState.Burnt;
            alight.Remove(thing.Slot);
        }

        private void Ignite(Flammable thing, ulong causeEventId)
        {
            int tick = context.Tick;
            int duration = context.Random.NextIntInclusive(thing.BurnMinimumTicks, thing.BurnMaximumTicks);
            StartBurning(thing);
            thing.BurnEndTick = checked(tick + duration);
            thing.EventId = context.Events.Append(tick, thing.Id, FireReactionEventType.ObjectCaughtFire, PositionOf(thing),
                0, duration, causeEventId).EventId;
            thing.RestCell = -1;
            thing.RestTicks = 0;

            // Something electrical does not sit and burn: it goes off.
            Pop(thing);
        }

        /// <summary>
        /// An electrical thing goes off the moment the flames reach it: a bang
        /// everybody hears, whatever is loose nearby flung away from it, anybody
        /// close knocked off their feet, and a scatter of new fire on the floor
        /// around it. Then it is wreckage, and it burns out quickly.
        /// <para>
        /// Everything here is somebody else's existing rule, in a fixed order so
        /// the run stays repeatable: the objects in ascending ID order, then the
        /// people in ascending ID order, then the floor squares row by row.
        /// </para>
        /// </summary>
        private void Pop(Flammable thing)
        {
            if (thing.IsTable)
            {
                return;
            }

            ObjectKindSettings kind = settings.Of(objects.KindOf(thing.Index));
            if (kind.PopRadiusMillimetres <= 0)
            {
                return;
            }

            LogicalPosition centre = objects.PositionOf(thing.Index);
            long radius = kind.PopRadiusMillimetres;
            ulong bang = context.Events.Append(context.Tick, thing.Id, FireReactionEventType.ObjectExploded, centre,
                kind.PopRadiusMillimetres, 0, thing.EventId).EventId;

            // Heard well beyond the blast itself, which is how the far side of
            // the building learns something has happened.
            sound.Bang(thing.Id, centre, kind.PopRadiusMillimetres * 6, kind.PopRadiusMillimetres * 3, bang);

            objects.FlingFrom(centre, kind.PopRadiusMillimetres, kind.PopSpeed, thing.Index, bang);

            Agent[] people = crowd.All;
            for (int i = 0; i < people.Length; i++)
            {
                Agent agent = people[i];
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

            fire.IgniteAround(centre, kind.PopRadiusMillimetres, kind.PopIgniteCells, bang);
            objects.Wreck(thing.Index, thing.Id, bang);
        }

        /// <summary>A burning thing that stays in one square for a moment sets that square alight.</summary>
        private void LightTheFloor(Flammable thing)
        {
            if (!thing.IsTable && objects.IsMoving(thing.Index))
            {
                thing.RestCell = -1;
                thing.RestTicks = 0;
                return;
            }

            int cell = fire.CellIndexAt(PositionOf(thing));
            if (cell != thing.RestCell)
            {
                thing.RestCell = cell;
                thing.RestTicks = 0;
            }

            thing.RestTicks++;
            if (thing.RestTicks == settings.FloorIgniteRestTicks)
            {
                fire.IgniteCell(cell, thing.EventId);
            }
        }

        private LogicalPosition PositionOf(Flammable thing)
        {
            return thing.IsTable ? geometry.TableBounds(thing.Index).Centre : objects.PositionOf(thing.Index);
        }

        /// <summary>Whether a person's body is within a touch of this thing.</summary>
        private bool Touches(Flammable thing, Agent agent)
        {
            int touch = personRadius + settings.TouchGapMillimetres;
            if (thing.IsTable)
            {
                return geometry.TableBounds(thing.Index).DistanceSquaredTo(agent.Body.Position) <= (long)touch * touch;
            }

            long reach = objects.RadiusOf(thing.Index) + (long)touch;
            return LogicalPosition.DistanceSquared(objects.PositionOf(thing.Index), agent.Body.Position) <= reach * reach;
        }

        /// <summary>The gap between two things' edges, in millimetres (tables are rectangles, objects circles).</summary>
        private long Gap(Flammable thing, Flammable other)
        {
            if (thing.IsTable && other.IsTable)
            {
                LogicalBounds a = geometry.TableBounds(thing.Index);
                LogicalBounds b = geometry.TableBounds(other.Index);
                long dx = Math.Max(0L, Math.Max((long)a.MinX - b.MaxX, (long)b.MinX - a.MaxX));
                long dz = Math.Max(0L, Math.Max((long)a.MinZ - b.MaxZ, (long)b.MinZ - a.MaxZ));
                return IntegerMath.Sqrt(dx * dx + dz * dz);
            }

            if (thing.IsTable || other.IsTable)
            {
                Flammable table = thing.IsTable ? thing : other;
                Flammable item = thing.IsTable ? other : thing;
                long toTable = IntegerMath.Sqrt(geometry.TableBounds(table.Index).DistanceSquaredTo(objects.PositionOf(item.Index)));
                return toTable - objects.RadiusOf(item.Index);
            }

            long centres = IntegerMath.Sqrt(LogicalPosition.DistanceSquared(objects.PositionOf(thing.Index), objects.PositionOf(other.Index)));
            return centres - objects.RadiusOf(thing.Index) - objects.RadiusOf(other.Index);
        }

        private static LogicalBounds Grow(LogicalBounds bounds, int by)
        {
            return new LogicalBounds(bounds.MinX - by, bounds.MaxX + by, bounds.MinZ - by, bounds.MaxZ + by);
        }

        // ---------------------------------------------------------------- snapshots

        public ObjectBurnState ObjectState(int objectIndex) => things[objectIndex].State;

        public int ObjectHeatPercent(int objectIndex) => HeatPercent(things[objectIndex]);

        public FireReactionTableSnapshot[] GetTableSnapshots()
        {
            var tables = new FireReactionTableSnapshot[geometry.TableCount];
            for (int t = 0; t < tables.Length; t++)
            {
                Flammable thing = things[objects.Count + t];
                tables[t] = new FireReactionTableSnapshot(thing.Id, geometry.TableBounds(t), thing.State, HeatPercent(thing),
                    geometry.TablePose(t), geometry.IsTableBroken(t));
            }

            return tables;
        }

        private static int HeatPercent(Flammable thing)
        {
            if (thing.State != ObjectBurnState.Intact)
            {
                return 100;
            }

            return thing.IgniteTicks <= 0 ? 0 : Math.Min(100, thing.Heat * 100 / thing.IgniteTicks);
        }
    }
}
