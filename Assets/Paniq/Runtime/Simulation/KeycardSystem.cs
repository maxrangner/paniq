namespace Paniq.Simulation
{
    /// <summary>
    /// The keycard that opens the way out (2026-09-27, the owner's idea).
    /// <para>
    /// The way out is a card door: nobody batters it, the fire does not burn
    /// through it and the player's key does not fit it. One card is in the
    /// building. Where it begins is drawn per round from a stream of its own
    /// -- in a member of staff's pocket, or lying on a desk in the room it
    /// was authored in -- so the rest of the run's draws are exactly what
    /// they would be without it. Staff know where it started; visitors know
    /// nothing; anybody who sees it learns where it is, a beat later, and
    /// sees who has it. Whoever has it swipes the door open for good;
    /// whoever is out cold or dead drops it where they lie (a trip or a
    /// knock-down they get up from keeps it). The player's pull on the card
    /// makes somebody calm pocket it. Going back for it in a fright -- the
    /// brave who had found the card door shut, or whoever the hand sent --
    /// was set aside on 2026-10-03 (the owner's choice): the loop level has
    /// no card, and it is not carried into the rebuilt way people decide.
    /// </para>
    /// <para>
    /// The card goes in a pocket, not the arms (<see cref="AgentKeycard"/>
    /// rather than <see cref="AgentCarry"/>): its holder's hands stay free
    /// for a bottle or a box, it slows nobody, and nobody drops it merely
    /// because they are frightened.
    /// </para>
    /// </summary>
    internal sealed class KeycardSystem : IBindable
    {
        /// <summary>
        /// The card's own random stream (docs/simulation-contract.md lists the
        /// terms a derived stream is allowed on): where the card starts is
        /// drawn from it, and from it only, so a level with no card replays
        /// exactly as it did before cards existed.
        /// </summary>
        private const ulong PlacementSequence = 56UL;

        /// <summary>How far around the card to look for floor somebody could reach it from.</summary>
        private const int StandingRoomMillimetres = 2500;

        /// <summary>A sighting this close to what they already believe is the same place.</summary>
        private const int SamePlaceMillimetres = 300;

        /// <summary>
        /// Arrived where they believed the card lay, somebody looks about them
        /// this far: a card kicked a little way off the spot is still found.
        /// </summary>
        private const int LooksAroundMillimetres = 1500;

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly PhysicsObjectSystem objects;
        private readonly Threats threats;
        private readonly FrightenedWalk walk;
        private readonly KeycardSettings settings;
        private readonly int bodyRadius;
        private readonly Pcg32 placement;

        /// <summary>The card (physical-object index), or -1 when the building has none.</summary>
        private int card = -1;

        /// <summary>The player's pulls, built after this system.</summary>
        private InfluenceSystem influence;

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
            tells = systems.Tells;
        }

        /// <summary>The wind-up before going back toward the flames (2026-09-30).</summary>
        private TellSystem tells;

        public KeycardSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry, DoorSystem doors,
            PhysicsObjectSystem objects, Threats threats, FrightenedWalk walk)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.objects = objects;
            this.threats = threats;
            this.walk = walk;
            settings = context.Scenario.Keycard;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            placement = new Pcg32(context.Seed, PlacementSequence);
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects.KindOf(i) == PhysicsObjectKind.Keycard)
                {
                    card = i;
                    break;
                }
            }
        }

        /// <summary>The card's physical-object index, or -1 when the building has none or it is put away.</summary>
        public int Card => card >= 0 && !objects.IsDormant(card) ? card : -1;

        /// <summary>Whether this person has the card in their pocket.</summary>
        public static bool Has(Agent agent) => agent.Keycard.Held >= 0;

        // ---------------------------------------------------------------- the start

        /// <summary>
        /// Where the card begins, drawn once before the first tick from the
        /// card's own stream: on a desk in its room, or in a member of
        /// staff's pocket. Then everybody who works here knows where.
        /// </summary>
        public void PlaceAtTheStart(Agent[] agents)
        {
            if (card < 0)
            {
                return;
            }

            if (!settings.Enabled)
            {
                objects.PutAway(card);
                return;
            }

            int holder = -1;
            if (!placement.NextPercent(settings.OnADeskPercent))
            {
                holder = DrawAMemberOfStaff(agents);
            }

            if (holder >= 0)
            {
                Agent staff = agents[holder];
                Pocket(staff, card, 0UL, CausalEventType.KeycardStarted);
            }
            else
            {
                PutOnADesk();
                context.Events.Append(0, objects.IdOf(card), CausalEventType.KeycardStarted, objects.PositionOf(card),
                    0, 0, 0UL, objects.IdOf(card));
            }

            TellTheStaff(agents);
        }

        /// <summary>
        /// A member of staff (anybody who knows the building), drawn
        /// uniformly; -1 when there is none. With
        /// <see cref="KeycardSettings.PocketStaysInTheCardsRoom"/> only the
        /// staff who start in the room the card is authored in are drawn
        /// from, so the card never begins beside the way out.
        /// </summary>
        private int DrawAMemberOfStaff(Agent[] agents)
        {
            int room = settings.PocketStaysInTheCardsRoom ? geometry.RoomAtPoint(objects.PositionOf(card)) : -1;
            int count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (MayStartWithTheCard(agents[i], room))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return -1;
            }

            int pick = placement.NextIntInclusive(0, count - 1);
            for (int i = 0; i < agents.Length; i++)
            {
                if (MayStartWithTheCard(agents[i], room) && pick-- == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Staff, and in the card's own room when one is named (<paramref name="room"/> of -1 is anywhere).</summary>
        private bool MayStartWithTheCard(Agent agent, int room) =>
            agent.Knowledge.KnowsEverything && (room < 0 || geometry.RoomOf(agent) == room);

        /// <summary>
        /// On one of the desks in the room the card was authored in, drawn
        /// uniformly, near the desk's east end where it can be reached from
        /// the floor. A room with no desk leaves it where it was authored.
        /// </summary>
        private void PutOnADesk()
        {
            int room = geometry.RoomAtPoint(objects.PositionOf(card));
            int count = 0;
            for (int t = 0; t < geometry.TableCount; t++)
            {
                // Desks only: a partition has no top to leave a card on.
                if (geometry.IsDesk(t) && geometry.RoomAtPoint(geometry.TableBounds(t).Centre) == room)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            int pick = placement.NextIntInclusive(0, count - 1);
            for (int t = 0; t < geometry.TableCount; t++)
            {
                LogicalBounds desk = geometry.TableBounds(t);
                if (!geometry.IsDesk(t) || geometry.RoomAtPoint(desk.Centre) != room || pick-- != 0)
                {
                    continue;
                }

                objects.PlaceOnATable(card, new LogicalPosition(desk.MaxX - 150, desk.Centre.Z));
                return;
            }
        }

        /// <summary>Everybody who works here knows where the card started; a visitor does not.</summary>
        private void TellTheStaff(Agent[] agents)
        {
            int holder = objects.HolderOf(card);
            for (int i = 0; i < agents.Length; i++)
            {
                if (!agents[i].Knowledge.KnowsEverything)
                {
                    continue;
                }

                Believe(agents[i].Keycard, holder, objects.PositionOf(card));
            }
        }

        private static void Believe(AgentKeycard belief, int holder, LogicalPosition place)
        {
            belief.Knows = true;
            belief.WithSomebody = holder >= 0;
            belief.Holder = holder;
            belief.Place = place;
            belief.Pending = false;
        }

        // ---------------------------------------------------------------- seeing it

        /// <summary>
        /// Phase 3, after looking and listening: whoever can see the card --
        /// lying somewhere, or in somebody's hand -- takes in where it is a
        /// beat later. One reaction lag is drawn per change seen, never per
        /// tick, so nobody learns a thing on the tick it happens and no two
        /// people learn it together.
        /// </summary>
        public void Notice(Agent agent)
        {
            if (Card < 0 || agent.Keycard.Held == card)
            {
                return;
            }

            AgentKeycard belief = agent.Keycard;
            int holder = objects.HolderOf(card);
            LogicalPosition where = holder >= 0 ? crowd.All[holder].Body.Position : objects.PositionOf(card);

            if (belief.Pending && context.Tick >= belief.PendingUntilTick)
            {
                Believe(belief, belief.PendingHolder, belief.PendingPlace);
            }

            if (belief.Knows && Agrees(belief.WithSomebody, belief.Holder, belief.Place, holder, where))
            {
                return;
            }

            if (belief.Pending && Agrees(belief.PendingWithSomebody, belief.PendingHolder, belief.PendingPlace, holder, where))
            {
                return;
            }

            if (!CanSee(agent, where))
            {
                return;
            }

            belief.Pending = true;
            belief.PendingWithSomebody = holder >= 0;
            belief.PendingHolder = holder;
            belief.PendingPlace = where;
            belief.PendingUntilTick = context.ReactionTick();
        }

        private static bool Agrees(bool withSomebody, int holder, LogicalPosition place, int actualHolder,
            LogicalPosition actualPlace)
        {
            if (actualHolder >= 0)
            {
                return withSomebody && holder == actualHolder;
            }

            return !withSomebody &&
                   LogicalPosition.DistanceSquared(place, actualPlace) <= (long)SamePlaceMillimetres * SamePlaceMillimetres;
        }

        /// <summary>In sight: within vision range, in front of them, and along a line of sight through open doorways only.</summary>
        private bool CanSee(Agent agent, LogicalPosition point)
        {
            LogicalPosition eye = agent.Body.Position;
            long range = context.Scenario.Perception.VisionRangeMillimetres;
            if (LogicalPosition.DistanceSquared(eye, point) > range * range)
            {
                return false;
            }

            return PerceptionSystem.InVisionCone(eye, IntegerMath.Direction(agent.Body.Heading), point) &&
                   geometry.CanSeeBetween(geometry.RoomAtPoint(eye), eye, geometry.RoomAtPoint(point), point);
        }

        // ---------------------------------------------------------------- having it

        /// <summary>
        /// Into their pocket, and the door they gave up on is a way out again.
        /// <paramref name="how"/> is the event written: taken, or (at the
        /// start) simply there.
        /// </summary>
        public void Pocket(Agent agent, int index, ulong causeEventId,
            CausalEventType how = CausalEventType.AgentTookKeycard)
        {
            objects.PickUp(index, agent);
            objects.FollowPocket(index, agent);
            agent.Keycard.Held = index;
            Believe(agent.Keycard, agent.Index, agent.Body.Position);
            context.Events.Append(context.Tick, agent.Id, how, agent.Body.Position, 0, 0, causeEventId,
                objects.IdOf(index));

            // A card door they had written off is worth another try now.
            for (int d = 0; d < doors.Count; d++)
            {
                if (doors.NeedsKeycard(d))
                {
                    agent.Doors.FoundShut[d] = false;
                    agent.Doors.AvoidUntilTick[d] = 0;
                }
            }

            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// Phase 3: somebody frightened with the card in their pocket, near
        /// enough a card door to reach its reader, swipes it -- from the back
        /// of the crush in the doorway as well as from the handle. The door
        /// is unlocked from then on, and whoever is rattling it opens it at
        /// once. The reach is <see cref="KeycardSettings.SwipeReachMillimetres"/>,
        /// in the door's own room only: not through the wall beside it.
        /// </summary>
        public void SwipeIfInReach(Agent agent)
        {
            if (agent.Keycard.Held < 0 || agent.Fear.State != AgentFearState.Scared || !agent.Body.IsOnTheirFeet)
            {
                return;
            }

            long reach = settings.SwipeReachMillimetres;
            int room = geometry.RoomOf(agent);
            for (int d = 0; d < doors.Count; d++)
            {
                if (!doors.NeedsKeycard(d) || geometry.DoorRoom(d) != room ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(d)) > reach * reach)
                {
                    continue;
                }

                doors.SwipeKeycard(d, agent, agent.Fear.ScaredEventId);
            }
        }

        /// <summary>Whether this thing is the card, lying free for this person to pocket.</summary>
        public bool CanBePocketed(Agent agent, int index)
        {
            return objects.IsPocketable(index) && agent.Keycard.Held < 0 && !objects.IsDormant(index) &&
                   objects.HolderOf(index) < 0 && objects.OccupantOf(index) < 0 && !objects.IsMoving(index);
        }

        /// <summary>
        /// Phase 3: whoever is out cold loses the card where they lie (the
        /// owner's rule: "someone knocked out"). A trip or a knock-down they
        /// get up from keeps it in their pocket; measured otherwise, every
        /// holder lost it to a stumble in the crush before reaching the door.
        /// </summary>
        public void DropIfOutCold(Agent agent)
        {
            if (agent.Keycard.Held < 0 || agent.Body.State != AgentBodyState.Unconscious)
            {
                return;
            }

            Drop(agent, agent.Body.EventId);
        }

        /// <summary>Somebody who died with the card leaves it where they fell.</summary>
        public void DropFromLost(Agent agent)
        {
            if (agent.Keycard.Held < 0)
            {
                return;
            }

            Drop(agent, agent.Burning.EventId != 0UL ? agent.Burning.EventId : agent.Body.EventId);
        }

        private void Drop(Agent agent, ulong causeEventId)
        {
            int index = agent.Keycard.Held;
            LogicalPosition spot = objects.FindSpotToPutDown(index, agent, out LogicalPosition clear) ? clear : agent.Body.Position;
            ulong dropped = context.Events.Append(context.Tick, agent.Id, CausalEventType.KeycardDropped, spot, 0, 0,
                causeEventId, objects.IdOf(index)).EventId;
            objects.Release(index, spot, 0, 0, dropped, onTheFloor: true);
            agent.Keycard.Held = -1;
            Believe(agent.Keycard, -1, spot);
        }

        /// <summary>After the physics step: the card stays on whoever has it.</summary>
        public void FollowHolders(Agent[] agents)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Keycard.Held >= 0)
                {
                    objects.FollowPocket(agents[i].Keycard.Held, agents[i]);
                }
            }
        }

        // ---------------------------------------------------------------- tests

        /// <summary>Tests and measurements: whether the card lies too near the flames for anybody to reach for it.</summary>
        internal bool CardNearFlamesForTests
        {
            get
            {
                long keepAway = settings.FlamesKeepAwayMillimetres;
                return Card >= 0 && threats.NearestDistanceSquared(objects.PositionOf(card), out _, out _) < keepAway * keepAway;
            }
        }

        /// <summary>Tests only: the card into this person's pocket, before the first tick, with nothing written.</summary>
        internal void GiveToForTests(Agent agent, Agent[] agents)
        {
            TakeBackForTests();
            objects.PickUp(card, agent);
            objects.FollowPocket(card, agent);
            agent.Keycard.Held = card;
            TellTheStaff(agents);

            // The holder knows they have it, visitor or not, as Pocket says.
            Believe(agent.Keycard, agent.Index, agent.Body.Position);
        }

        /// <summary>Tests only: the card down at a spot on the floor, before the first tick, with nothing written.</summary>
        internal void PutDownForTests(LogicalPosition spot, Agent[] agents)
        {
            TakeBackForTests();
            objects.PlaceAt(card, spot, 0, 0, 0UL);
            TellTheStaff(agents);
        }

        /// <summary>Tests only: the card onto this table, before the first tick, with nothing written.</summary>
        internal void PutOnATableForTests(int table, Agent[] agents)
        {
            TakeBackForTests();
            LogicalBounds desk = geometry.TableBounds(table);
            objects.PlaceOnATable(card, new LogicalPosition(desk.MaxX - 150, desk.Centre.Z));
            TellTheStaff(agents);
        }

        private void TakeBackForTests()
        {
            if (card < 0 || !settings.Enabled)
            {
                throw new System.InvalidOperationException("This building has no keycard in play.");
            }

            int holder = objects.HolderOf(card);
            if (holder >= 0)
            {
                Agent had = crowd.All[holder];
                had.Keycard.Held = -1;
                objects.Release(card, had.Body.Position, 0, 0, 0UL, onTheFloor: true);
            }
        }
    }
}
