using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Influence (prototype 3, second batch, 2026-09-26): the player clicks a
    /// door, a thing or a patch of floor, and people are drawn toward it. It
    /// is never an order. Every person weighs it against their own fear,
    /// habits and character, and it only tips the ones who were undecided.
    /// The owner's words: "clicking a door once just increases the chances of
    /// an agent using the door; clicking it a few more times increases it
    /// more; clicking an empty hallway a few times acts like an attractor
    /// influencing the agents' own decision making."
    /// <para>
    /// Each click adds one step, up to <see cref="InfluenceSettings.MaximumLevel"/>;
    /// a strong pull takes frantic clicking. It ticks down a step at a time
    /// on its own and cannot be cancelled. It sticks to its place: anybody who
    /// comes near it later feels it too, more the nearer they are, and not at
    /// all from another room. There is no limit to how many places there are.
    /// </para>
    /// <para>
    /// How much a person feels it is arithmetic on where they stand and who
    /// they are (<see cref="FeltBy"/>), so asking draws no random numbers and
    /// an influence nobody is near changes no run. Places are kept in the
    /// order they were first clicked, so a replay agrees.
    /// </para>
    /// </summary>
    internal sealed class InfluenceSystem
    {
        /// <summary>One influenced place.</summary>
        internal struct Place
        {
            /// <summary>A door's index, or -1.</summary>
            public int Door;

            /// <summary>A thing's index, or -1.</summary>
            public int Thing;

            /// <summary>What was clicked, for the log and the display: the door or the thing, or none for floor.</summary>
            public SimulationId Target;

            /// <summary>Where the pull comes from: the doorway's middle, the thing where it stood, or the spot.</summary>
            public LogicalPosition At;

            /// <summary>The rooms it is felt in: its own, and for a door the room on the other side too.</summary>
            public int RoomA;
            public int RoomB;

            /// <summary>Its level at the last click, and when that was; it has lost a step for every stretch since.</summary>
            public int LevelAtClick;
            public int ClickTick;

            /// <summary>The last click's event: what somebody drawn by it names as the cause.</summary>
            public ulong EventId;
        }

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly InfluenceSettings settings;
        private readonly List<Place> places = new List<Place>();

        public InfluenceSystem(SimulationContext context, WorldGeometry geometry)
        {
            this.context = context;
            this.geometry = geometry;
            settings = context.Scenario.Influence;
        }

        /// <summary>How many places are influenced right now.</summary>
        public int Count => places.Count;

        /// <summary>The <paramref name="i"/>-th place.</summary>
        public Place this[int i] => places[i];

        /// <summary>A place's level now: its level at the last click, less a step for each stretch since.</summary>
        public int LevelOf(int i) => LevelNow(places[i]);

        private int LevelNow(Place place)
        {
            int lost = (context.Tick - place.ClickTick) / settings.TicksPerStepLost;
            return System.Math.Max(0, place.LevelAtClick - lost);
        }

        /// <summary>
        /// One click on a door. Returns the level it now has. The door must be
        /// one the building has; the command system has checked.
        /// </summary>
        public int OnDoor(int door, SimulationId doorId)
        {
            int roomA = geometry.DoorRoom(door);
            int roomB = geometry.RoomBeyond(door, roomA);
            return Click(door, -1, doorId, geometry.DoorCentre(door), roomA, roomB);
        }

        /// <summary>One click on a thing: the pull comes from where it stands now, and stays there.</summary>
        public int OnThing(int thing, SimulationId thingId, LogicalPosition at)
        {
            int room = geometry.RoomAtPoint(at);
            return Click(-1, thing, thingId, at, room, room);
        }

        /// <summary>One click on the floor. False, and nothing written, when the spot is not floor in any room.</summary>
        public bool TryOnSpot(LogicalPosition at, out int level)
        {
            level = 0;
            int room = geometry.RoomAtPoint(at);
            if (room < 0)
            {
                return false;
            }

            level = Click(-1, -1, default, at, room, room);
            return true;
        }

        /// <summary>
        /// A click adds a step to whatever it lands on: the same door, the same
        /// thing, or a place on the floor (or a thing) within
        /// <see cref="InfluenceSettings.StackRadiusMillimetres"/> in the same
        /// room -- so frantic clicking on a patch of corridor builds one strong
        /// pull rather than twenty weak ones, and a click just the other side
        /// of a wall starts a place of its own there.
        /// </summary>
        private int Click(int door, int thing, SimulationId target, LogicalPosition at, int roomA, int roomB)
        {
            int tick = context.Tick;
            int found = Find(door, thing, at, roomA);
            int level;
            if (found >= 0)
            {
                Place place = places[found];
                level = System.Math.Min(settings.MaximumLevel, LevelNow(place) + 1);
                place.LevelAtClick = level;
                place.ClickTick = tick;
                place.EventId = Log(place.Target, place.At, level);
                places[found] = place;
                return level;
            }

            if (places.Count >= settings.MaximumPlaces)
            {
                DropTheWeakest();
            }

            level = 1;
            places.Add(new Place
            {
                Door = door,
                Thing = thing,
                Target = target,
                At = at,
                RoomA = roomA,
                RoomB = roomB,
                LevelAtClick = level,
                ClickTick = tick,
                EventId = Log(target, at, level)
            });
            return level;
        }

        private ulong Log(SimulationId target, LogicalPosition at, int level)
        {
            return context.Events.Append(context.Tick, default, CausalEventType.PowerInfluenced, at, level, 0, 0UL, target)
                .EventId;
        }

        private int Find(int door, int thing, LogicalPosition at, int room)
        {
            long stack = settings.StackRadiusMillimetres;
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                if (door >= 0)
                {
                    if (place.Door == door)
                    {
                        return i;
                    }

                    continue;
                }

                if (thing >= 0 && place.Thing == thing)
                {
                    return i;
                }

                if (place.Door < 0 && place.RoomA == room &&
                    LogicalPosition.DistanceSquared(place.At, at) <= stack * stack)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>A safety net, never the player's limit: the faintest place goes, the oldest of equals.</summary>
        private void DropTheWeakest()
        {
            int weakest = 0;
            for (int i = 1; i < places.Count; i++)
            {
                if (LevelNow(places[i]) < LevelNow(places[weakest]))
                {
                    weakest = i;
                }
            }

            places.RemoveAt(weakest);
        }

        /// <summary>Phase 1's tail: places that have faded to nothing are gone, in order.</summary>
        public void Advance()
        {
            for (int i = places.Count - 1; i >= 0; i--)
            {
                if (LevelNow(places[i]) <= 0)
                {
                    places.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// How strongly this person feels this place, per mille of a full pull
        /// felt by an ordinary person standing on it: the place's level, less
        /// the further off they are, times how easily led they are
        /// (<see cref="Susceptibility"/>). Nothing from another room, and
        /// nothing from beyond <see cref="InfluenceSettings.ReachMillimetres"/>.
        /// </summary>
        public int FeltBy(Agent agent, int i)
        {
            Place place = places[i];
            int level = LevelNow(place);
            if (level <= 0)
            {
                return 0;
            }

            int room = geometry.RoomAt(agent.Body.Position);
            if (room < 0 || (room != place.RoomA && room != place.RoomB))
            {
                return 0;
            }

            long reach = settings.ReachMillimetres;
            long distance = IntegerMath.Distance(agent.Body.Position, place.At);
            if (distance >= reach)
            {
                return 0;
            }

            long felt = 1000L * level / settings.MaximumLevel * (reach - distance) / reach;
            return (int)(felt * Susceptibility(agent) / 100L);
        }

        /// <summary>
        /// How easily led somebody is, in percent of an ordinary person: the
        /// nervous and strangers to the building more, leaders and the cruel
        /// much less. Nobody feels nothing at all, and nobody more than twice.
        /// </summary>
        public int Susceptibility(Agent agent)
        {
            AgentTraitValues traits = agent.Traits;
            int percent = 100 + settings.PercentPerNervousness * (traits.Nervousness - 5) -
                          settings.PercentPerLeadership * System.Math.Max(0, traits.Leadership - 5) -
                          settings.PercentPerEvil * System.Math.Max(0, traits.Evil - 5) +
                          (agent.Knowledge.KnowsEverything ? 0 : settings.VisitorPercent);
            return System.Math.Max(settings.MinimumPercent, System.Math.Min(settings.MaximumPercent, percent));
        }

        /// <summary>
        /// For the display: every place, and everybody still in the building
        /// who feels one, with the strongest pull they feel. Fills the buffers
        /// it is given, so a tick allocates nothing.
        /// </summary>
        public void FillSnapshot(List<InfluencePlaceSnapshot> intoPlaces, List<InfluencePullSnapshot> intoPulls, Agent[] agents)
        {
            intoPlaces.Clear();
            intoPulls.Clear();
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                intoPlaces.Add(new InfluencePlaceSnapshot(place.Target, place.Door >= 0, place.At, LevelNow(place),
                    settings.MaximumLevel));
            }

            if (places.Count == 0)
            {
                return;
            }

            for (int a = 0; a < agents.Length; a++)
            {
                if (!agents[a].IsParticipating)
                {
                    continue;
                }

                int felt = StrongestFeltBy(agents[a], out int strongest);
                if (felt > 0)
                {
                    intoPulls.Add(new InfluencePullSnapshot(agents[a].Id, a, strongest, felt));
                }
            }
        }

        /// <summary>The strongest pull this person feels, and from which place; 0 and -1 when none.</summary>
        public int StrongestFeltBy(Agent agent, out int strongest)
        {
            strongest = -1;
            int best = 0;
            for (int i = 0; i < places.Count; i++)
            {
                int felt = FeltBy(agent, i);
                if (felt > best)
                {
                    best = felt;
                    strongest = i;
                }
            }

            return best;
        }

        /// <summary>
        /// What running for this door is worth to somebody, in millimetres, for
        /// the door choice to add: the full bonus for a strong pull felt close
        /// to it, nothing when the door is not influenced or they cannot feel it.
        /// </summary>
        public long DoorBonus(Agent agent, int door)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door == door)
                {
                    return (long)FeltBy(agent, i) * settings.FullPullBonusMillimetres / 1000L;
                }
            }

            return 0L;
        }

        /// <summary>
        /// What heading for a spot is worth to somebody, in millimetres: for
        /// every place on the floor or on a thing they can feel, its pull times
        /// how far the way to the candidate agrees with the way to the place
        /// (the full pull for straight toward it, nothing square to it, the
        /// same off for straight away). Doors are left to <see cref="DoorBonus"/>.
        /// </summary>
        public long SpotBonus(Agent agent, LogicalPosition candidate)
        {
            if (places.Count == 0)
            {
                return 0L;
            }

            long total = 0L;
            LogicalPosition from = agent.Body.Position;
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                if (place.Door >= 0)
                {
                    continue;
                }

                int felt = FeltBy(agent, i);
                if (felt <= 0)
                {
                    continue;
                }

                int towardIt = IntegerMath.HeadingBetween(from, place.At, agent.Body.Heading);
                long agreement = ExitSignBehaviour.Agreement(from, candidate, towardIt);
                total += (long)felt * settings.FullPullBonusMillimetres / 1000L * agreement / IntegerMath.TrigScale;
            }

            return total;
        }
    }
}
