using System;

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
        private readonly FlammableSettings settings;
        private readonly int personRadius;

        /// <summary>Loose objects first (ascending ID), then tables (ascending ID): the order every pass uses.</summary>
        private readonly Flammable[] things;

        public FlammablesSystem(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            FireSystem fire,
            PhysicsObjectSystem objects,
            BodySystem body)
        {
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
                bool chair = objects.KindOf(i) == PhysicsObjectKind.Chair;
                things[i] = new Flammable
                {
                    Id = objects.IdOf(i),
                    Index = i,
                    IgniteTicks = chair ? settings.ChairIgniteTicks : settings.BoxIgniteTicks,
                    BurnMinimumTicks = chair ? settings.ChairBurnMinimumTicks : settings.BoxBurnMinimumTicks,
                    BurnMaximumTicks = chair ? settings.ChairBurnMaximumTicks : settings.BoxBurnMaximumTicks
                };
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
                if (thing.State != ObjectBurnState.Intact)
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
                    thing.State = ObjectBurnState.Burnt;
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

            for (int i = 0; i < things.Length; i++)
            {
                Flammable other = things[i];
                if (other != thing && other.State == ObjectBurnState.Burning && Gap(thing, other) <= reach)
                {
                    return other.EventId;
                }
            }

            return 0UL;
        }

        private void Ignite(Flammable thing, ulong causeEventId)
        {
            int tick = context.Tick;
            int duration = context.Random.NextIntInclusive(thing.BurnMinimumTicks, thing.BurnMaximumTicks);
            thing.State = ObjectBurnState.Burning;
            thing.BurnEndTick = checked(tick + duration);
            thing.EventId = context.Events.Append(tick, thing.Id, FireReactionEventType.ObjectCaughtFire, PositionOf(thing),
                0, duration, causeEventId).EventId;
            thing.RestCell = -1;
            thing.RestTicks = 0;
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
                tables[t] = new FireReactionTableSnapshot(thing.Id, geometry.TableBounds(t), thing.State, HeatPercent(thing));
            }

            return tables;
        }

        private static int HeatPercent(Flammable thing)
        {
            return thing.State == ObjectBurnState.Intact ? Math.Min(100, thing.Heat * 100 / thing.IgniteTicks) : 100;
        }
    }
}
