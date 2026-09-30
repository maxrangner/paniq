using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Influence (prototype 3, second batch, 2026-09-26): the player's hand on
    /// a door, a thing or a patch of floor, and people are drawn toward it. It
    /// is never an order. Every person weighs it against their own fear,
    /// habits and character, and it only tips the ones who were undecided.
    /// <para>
    /// Since 2026-09-29 it is a hold, not clicks (the owner's rule: "hold
    /// only ... it also makes all decisions a priority. You can't be
    /// everywhere at once. When you interact the influence is clear and
    /// instant, but as soon as you let go the agents are on their own"). A
    /// press puts a full pull on the place at once and replaces whatever was
    /// held before; a release takes it away at once. One place at a time,
    /// nothing fades, nothing stacks. It used to be one step a click, twenty
    /// to fill and forty seconds to fade.
    /// </para>
    /// <para>
    /// A place is felt within <see cref="InfluenceSettings.ReachMillimetres"/>
    /// of it as a walk: straight across the room it is in, or through one open
    /// doorway into the room next door (2026-09-29, the owner: "through open
    /// doors, but limit range to be around a room's length"), never through a
    /// wall or a shut door. How much a person feels it is arithmetic on where
    /// they stand and who they are (<see cref="FeltBy"/>), so asking draws no
    /// random numbers and a hand nobody is near changes no run.
    /// </para>
    /// <para>
    /// Somebody who does what the pull asked (opens the door, sits on the
    /// chair, pockets the card) spends its <em>use</em>: the place goes on
    /// gathering people while it is held, but nobody uses it again until it
    /// is pressed afresh, so a door opened for the player is not shut for
    /// them a moment later by the next person drawn to it.
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

            /// <summary>What was pressed, for the log and the display: the door or the thing, or none for floor.</summary>
            public SimulationId Target;

            /// <summary>Where the pull comes from: the doorway's middle, the thing where it stood, or the spot.</summary>
            public LogicalPosition At;

            /// <summary>The rooms it is in: its own, and for a door the room on the other side too.</summary>
            public int RoomA;
            public int RoomB;

            /// <summary>The tick the hand went on it.</summary>
            public int PressTick;

            /// <summary>The press's event: what somebody drawn by it names as the cause.</summary>
            public ulong EventId;

            /// <summary>Somebody has done what it asked: it gathers still, but is not used again until pressed afresh.</summary>
            public bool Spent;

            /// <summary>
            /// The right button's hand (2026-09-30): it pushes people away
            /// rather than drawing them. Nothing is used at a place that
            /// pushes, and a door that pushes is never chosen.
            /// </summary>
            public bool Repels;

            /// <summary>
            /// The tick a click's beacon comes off by itself, or 0 while the
            /// button is still held (2026-09-30, the owner: "a single click
            /// should place an influence beacon for 3 seconds").
            /// </summary>
            public int EndsAtTick;

            /// <summary>Whether this place draws people to it: the left button's hand.</summary>
            public bool Pulls => !Repels;
        }

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly InfluenceSettings settings;

        /// <summary>The held place, if any: a list of at most one, so the callers that walk places need not change.</summary>
        private readonly List<Place> places = new List<Place>(1);

        public InfluenceSystem(SimulationContext context, WorldGeometry geometry)
        {
            this.context = context;
            this.geometry = geometry;
            settings = context.Scenario.Influence;
        }

        /// <summary>How many places are influenced right now: one while the button is down, else none.</summary>
        public int Count => places.Count;

        /// <summary>The <paramref name="i"/>-th place.</summary>
        public Place this[int i] => places[i];

        /// <summary>A held place's level: always the full one. Kept for the display and the tests.</summary>
        public int LevelOf(int i) => settings.MaximumLevel;

        /// <summary>How many ticks the hand has been on the <paramref name="i"/>-th place.</summary>
        public int HeldFor(int i) => context.Tick - places[i].PressTick;

        /// <summary>The player's hand goes on a door. The door must be one the building has; the command system has checked.</summary>
        public void OnDoor(int door, SimulationId doorId, bool repels = false)
        {
            int roomA = geometry.DoorRoom(door);
            int roomB = geometry.RoomBeyond(door, roomA);
            Press(door, -1, doorId, geometry.DoorCentre(door), roomA, roomB, repels);
        }

        /// <summary>The player's hand goes on a thing: the pull comes from where it stands now, and stays there.</summary>
        public void OnThing(int thing, SimulationId thingId, LogicalPosition at, bool repels = false)
        {
            int room = geometry.RoomAtPoint(at);
            Press(-1, thing, thingId, at, room, room, repels);
        }

        /// <summary>The player's hand goes on the floor. False, and nothing written, when the spot is not floor in any room.</summary>
        public bool TryOnSpot(LogicalPosition at, bool repels = false)
        {
            int room = geometry.RoomAtPoint(at);
            if (room < 0)
            {
                return false;
            }

            Press(-1, -1, default, at, room, room, repels);
            return true;
        }

        /// <summary>
        /// The button came up inside a click (2026-09-30): the place pressed a
        /// moment ago stays under the hand until
        /// <see cref="InfluenceSettings.BeaconTicks"/> after its press, then
        /// comes off by itself. Nothing when nothing is held, or when it is a
        /// beacon already.
        /// </summary>
        public void Leave()
        {
            if (places.Count == 0 || places[0].EndsAtTick > 0)
            {
                return;
            }

            Place place = places[0];
            place.EndsAtTick = System.Math.Max(context.Tick + 1, place.PressTick + settings.BeaconTicks);
            places[0] = place;
        }

        /// <summary>
        /// The player lets go: the place is gone at once, whoever was on their
        /// way to it left to their own devices (they re-decide on their own
        /// beat, as they do for everything). Nothing written when nothing was
        /// held.
        /// </summary>
        public void Release()
        {
            if (places.Count == 0)
            {
                return;
            }

            Place place = places[0];
            context.Events.Append(context.Tick, default, CausalEventType.PowerReleasedInfluence, place.At,
                context.Tick - place.PressTick, 0, place.EventId, place.Target);
            places.Clear();
        }

        /// <summary>
        /// A press: the one place the hand is on, replacing whatever it was
        /// on before. Pressing the place already held presses it afresh, which
        /// is how a door used once is asked for the opposite.
        /// </summary>
        private void Press(int door, int thing, SimulationId target, LogicalPosition at, int roomA, int roomB, bool repels)
        {
            places.Clear();
            places.Add(new Place
            {
                Door = door,
                Thing = thing,
                Target = target,
                At = at,
                RoomA = roomA,
                RoomB = roomB,
                PressTick = context.Tick,
                EventId = context.Events.Append(context.Tick, default,
                    repels ? CausalEventType.PowerRepelled : CausalEventType.PowerInfluenced, at,
                    settings.MaximumLevel, 0, 0UL, target).EventId,
                Spent = false,
                Repels = repels
            });
        }

        /// <summary>The unspent pull on this door, or -1: the door is there to be used.</summary>
        public int PlaceOfDoor(int door)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door == door && !places[i].Spent && places[i].Pulls)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The pull on this door, spent or not, or -1: the hand is drawing people to it.</summary>
        public int PullOnDoor(int door)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door == door && places[i].Pulls)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Whether the hand is pushing people away from this door.</summary>
        public bool IsRepelling(int door)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door == door && places[i].Repels)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The unspent pull on this thing, or -1: the thing is there to be used.</summary>
        public int PlaceOfThing(int thing)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Thing == thing && !places[i].Spent && places[i].Pulls)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Somebody did what the pull asked (the owner's rule, 2026-09-27):
        /// opened or shut the door, sat on the chair, picked the thing up.
        /// Its use is spent: it goes on gathering people while held, but is
        /// not used again until pressed afresh -- a door opened for the
        /// player is shut for them at the next press. The event's id, or 0
        /// when nothing unspent was on it, in which case nothing is written.
        /// </summary>
        public ulong Spend(Agent by, int door, int thing)
        {
            int i = door >= 0 ? PlaceOfDoor(door) : thing >= 0 ? PlaceOfThing(thing) : -1;
            return i < 0 ? 0UL : SpendAt(by, i);
        }

        /// <summary>The pull on the floor, or on a thing, within a stacking distance of here: the spot beside a pull station somebody has just pulled.</summary>
        public ulong SpendNear(Agent by, LogicalPosition at)
        {
            long stack = settings.StackRadiusMillimetres;
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door < 0 && !places[i].Spent && places[i].Pulls &&
                    LogicalPosition.DistanceSquared(places[i].At, at) <= stack * stack)
                {
                    return SpendAt(by, i);
                }
            }

            return 0UL;
        }

        /// <summary>Whether an unspent pull lies within a stacking distance of here: the hand is on this pull station.</summary>
        public bool IsPullingNear(LogicalPosition at)
        {
            long stack = settings.StackRadiusMillimetres;
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door < 0 && !places[i].Spent && places[i].Pulls &&
                    LogicalPosition.DistanceSquared(places[i].At, at) <= stack * stack)
                {
                    return true;
                }
            }

            return false;
        }

        private ulong SpendAt(Agent by, int i)
        {
            Place place = places[i];
            ulong spent = context.Events.Append(context.Tick, by.Id, CausalEventType.InfluenceSpent, place.At,
                settings.MaximumLevel, 0, place.EventId, place.Target).EventId;
            place.Spent = true;
            places[i] = place;
            return spent;
        }

        /// <summary>
        /// Phase 1's tail: a click's beacon whose time is up comes off by
        /// itself, exactly as a release would (2026-09-30). Nothing fades.
        /// </summary>
        public void Advance()
        {
            if (places.Count > 0 && places[0].EndsAtTick > 0 && context.Tick >= places[0].EndsAtTick)
            {
                Release();
            }
        }

        /// <summary>
        /// How strongly this person feels this place, per mille of a full pull
        /// felt by an ordinary person standing on it, times how easily led
        /// they are (<see cref="Susceptibility"/>). Full within half the reach
        /// -- "next to a group" is a full pull (the owner, 2026-09-30: "holding
        /// next to a group barely made them come closer") -- then fading to
        /// nothing at <see cref="InfluenceSettings.ReachMillimetres"/>.
        /// Distance is a walk: across the room, or through one open doorway
        /// from the room next door; nothing through a wall or a shut door.
        /// The same for a pull and a push: which way it acts is the caller's.
        /// </summary>
        public int FeltBy(Agent agent, int i)
        {
            Place place = places[i];
            int room = geometry.RoomAt(agent.Body.Position);
            if (room < 0)
            {
                return 0;
            }

            long reach = settings.ReachMillimetres;
            long distance = WalkTo(room, agent.Body.Position, place);
            if (distance >= reach)
            {
                return 0;
            }

            long full = reach * settings.FullWithinPercent / 100L;
            long felt = distance <= full ? 1000L : 1000L * (reach - distance) / System.Math.Max(1L, reach - full);
            return (int)(felt * Susceptibility(agent) / 100L);
        }

        /// <summary>
        /// How far it is to the place from here: straight, in one of the
        /// place's rooms; else through the nearest open doorway that joins
        /// this room to one of them, doorway to place added on; else out of
        /// reach. One doorway deep, which is "about a room's length".
        /// </summary>
        private long WalkTo(int room, LogicalPosition from, Place place)
        {
            if (room == place.RoomA || room == place.RoomB)
            {
                return IntegerMath.Distance(from, place.At);
            }

            long best = long.MaxValue;
            for (int d = 0; d < geometry.DoorCount; d++)
            {
                if (!geometry.IsDoorOpen(d))
                {
                    continue;
                }

                int side = geometry.DoorRoom(d);
                int beyond = geometry.RoomBeyond(d, side);
                bool joins = (side == room && (beyond == place.RoomA || beyond == place.RoomB)) ||
                             (beyond == room && (side == place.RoomA || side == place.RoomB));
                if (!joins)
                {
                    continue;
                }

                LogicalPosition through = geometry.DoorCentre(d);
                long via = IntegerMath.Distance(from, through) + IntegerMath.Distance(through, place.At);
                if (via < best)
                {
                    best = via;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether this person will have none of the player's hand: the
        /// strongest wills in the building (the owner, 2026-09-30: "most,
        /// strong wills refuse"). On the office, the bully and the host.
        /// </summary>
        public bool Refuses(Agent agent) =>
            agent.Traits.Leadership >= settings.RefusesFromLeadership || agent.Traits.Evil >= settings.RefusesFromEvil;

        /// <summary>
        /// How easily led somebody is, in percent of an ordinary person: the
        /// nervous and strangers to the building more, leaders and the cruel
        /// less. The strongest wills feel nothing at all (<see cref="Refuses"/>);
        /// everybody else feels at least <see cref="InfluenceSettings.MinimumPercent"/>,
        /// and nobody more than <see cref="InfluenceSettings.MaximumPercent"/>.
        /// </summary>
        public int Susceptibility(Agent agent)
        {
            if (Refuses(agent))
            {
                return 0;
            }

            AgentTraitValues traits = agent.Traits;
            int percent = 100 + settings.PercentPerNervousness * (traits.Nervousness - 5) -
                          settings.PercentPerLeadership * System.Math.Max(0, traits.Leadership - 5) -
                          settings.PercentPerEvil * System.Math.Max(0, traits.Evil - 5) +
                          (agent.Knowledge.KnowsEverything ? 0 : settings.VisitorPercent);
            return System.Math.Max(settings.MinimumPercent, System.Math.Min(settings.MaximumPercent, percent));
        }

        /// <summary>
        /// For the display: the held place, and everybody still in the building
        /// who feels it, with how strongly. Fills the buffers it is given, so a
        /// tick allocates nothing.
        /// </summary>
        public void FillSnapshot(List<InfluencePlaceSnapshot> intoPlaces, List<InfluencePullSnapshot> intoPulls, Agent[] agents)
        {
            intoPlaces.Clear();
            intoPulls.Clear();
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                intoPlaces.Add(new InfluencePlaceSnapshot(place.Target, place.Door >= 0, place.At, settings.MaximumLevel,
                    settings.MaximumLevel, place.Repels, place.EndsAtTick > 0));
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

                int felt = FeltBy(agents[a], 0);
                if (felt > 0)
                {
                    intoPulls.Add(new InfluencePullSnapshot(agents[a].Id, a, 0, felt, IsActingFor(agents[a])));
                }
            }
        }

        /// <summary>
        /// Whether this person is doing what the hand asked, whatever their
        /// nature says (2026-09-30): they answered the press the hand is still
        /// on, or they hold a bottle they took for it. Asks nothing random.
        /// </summary>
        public bool IsActingFor(Agent agent)
        {
            if (agent.Carry.ForTheHand && agent.Carry.Holding)
            {
                return true;
            }

            ulong press = agent.Intent.ForTheHandPress;
            return press != 0UL && places.Count > 0 && places[0].EventId == press;
        }

        /// <summary>The hand's press, while one is on: what somebody answering it remembers. 0 when the hand is off.</summary>
        public ulong CurrentPress => places.Count > 0 ? places[0].EventId : 0UL;

        /// <summary>
        /// Whether this person has taken in the hand now on (2026-09-30, the
        /// owner's rule: nobody reacts on the tick a thing happens, and no two
        /// on the same tick). The first time they are asked about a press they
        /// draw their own reaction tick; from then on it is simply whether that
        /// tick has come. Ask it only of somebody who feels the hand, so a hand
        /// nobody is near draws nothing.
        /// </summary>
        public bool HasNoticed(Agent agent)
        {
            ulong press = CurrentPress;
            if (press == 0UL)
            {
                return false;
            }

            if (agent.Intent.NoticedHandPress != press)
            {
                agent.Intent.NoticedHandPress = press;
                agent.Intent.NoticedHandAtTick = context.ReactionTick();
            }

            return context.Tick >= agent.Intent.NoticedHandAtTick;
        }

        /// <summary>They are no longer doing anything for the hand: done, given up, or on to something else.</summary>
        public static void StopActing(Agent agent)
        {
            agent.Intent.ForTheHandPress = 0UL;
            agent.Intent.AgainstTheirNature = false;
        }

        /// <summary>
        /// Somebody does for the hand what they never would of their own
        /// accord: written once, for the sign and the story, and remembered so
        /// the drawing trembles them while they do it.
        /// </summary>
        public void ActedAgainstNature(Agent agent, AgainstTheirNature what, ulong press, SimulationId target,
            LogicalPosition at)
        {
            agent.Intent.AgainstTheirNature = true;
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentActedForTheHand, at, (int)what, 0,
                press, target);
        }

        /// <summary>Doing for the hand, right now, what is against their nature: the drawing trembles them.</summary>
        public bool IsActingAgainstNature(Agent agent) => agent.Intent.AgainstTheirNature && IsActingFor(agent);

        /// <summary>
        /// The strongest pull this person feels, and from which place; 0 and
        /// -1 when none. A place that pushes is no pull (<see cref="StrongestPushFeltBy"/>).
        /// </summary>
        public int StrongestFeltBy(Agent agent, out int strongest)
        {
            strongest = -1;
            int best = 0;
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Repels)
                {
                    continue;
                }

                int felt = FeltBy(agent, i);
                if (felt > best)
                {
                    best = felt;
                    strongest = i;
                }
            }

            return best;
        }

        /// <summary>The strongest push this person feels, and from which place; 0 and -1 when none.</summary>
        public int StrongestPushFeltBy(Agent agent, out int strongest)
        {
            strongest = -1;
            int best = 0;
            for (int i = 0; i < places.Count; i++)
            {
                if (!places[i].Repels)
                {
                    continue;
                }

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
        /// the door choice to add: the full bonus for the hand on it felt close
        /// to it, nothing when the door is not held or they cannot feel it --
        /// and as much taken off for a hand pushing people away from it.
        /// </summary>
        public long DoorBonus(Agent agent, int door)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Door == door)
                {
                    long bonus = (long)FeltBy(agent, i) * settings.FullPullBonusMillimetres / 1000L;
                    return places[i].Repels ? -bonus : bonus;
                }
            }

            return 0L;
        }

        /// <summary>
        /// What heading for a spot is worth to somebody, in millimetres: for
        /// a held place on the floor or on a thing they can feel, its pull times
        /// how far the way to the candidate agrees with the way to the place
        /// (the full pull for straight toward it, nothing square to it, the
        /// same off for straight away), and the other way round for a place
        /// that pushes -- a door that pushes included, so a frightened crowd
        /// steers off it. A door that pulls is left to <see cref="DoorBonus"/>,
        /// and so is <paramref name="exceptDoor"/>, the door a choice is
        /// scoring already, so a push on it is not counted twice.
        /// </summary>
        public long SpotBonus(Agent agent, LogicalPosition candidate, int exceptDoor = -1)
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
                if (place.Door >= 0 && (place.Pulls || place.Door == exceptDoor))
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
                long bonus = (long)felt * settings.FullPullBonusMillimetres / 1000L * agreement / IntegerMath.TrigScale;
                total += place.Repels ? -bonus : bonus;
            }

            return total;
        }
    }
}
