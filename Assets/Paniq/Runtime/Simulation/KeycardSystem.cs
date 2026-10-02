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
    /// sees who has it. Somebody frightened who has found the card door shut,
    /// knows where the card lies and is brave enough goes back for it, one
    /// fetcher at a time, and never into the flames: the card does not burn
    /// (the owner's rule), so a card lying in the fire waits until the fire
    /// has passed. Whoever has it swipes the door open for good; whoever is
    /// out cold or dead drops it where they lie, for anybody to pick up (a
    /// trip or a knock-down they get up from keeps it). What people go for is
    /// what they believe about the card, not where it really is: the truth
    /// only counts once they are standing over it. The player's pull on the
    /// card makes somebody calm pocket it too -- and, since 2026-09-28,
    /// somebody frightened: a pull felt at a quarter of full or more sends
    /// whoever feels it strongest for the card, brave or not, because before
    /// that the one lever the player had on a desk card stopped working the
    /// moment the panic started. Everything that keeps a fetcher alive
    /// still applies, and the pull is spent on pocketing it.
    /// </para>
    /// <para>
    /// The card goes in a pocket, not the arms (<see cref="AgentKeycard"/>
    /// rather than <see cref="AgentCarry"/>): its holder's hands stay free
    /// for a bottle or a box, it slows nobody, and nobody drops it merely
    /// because they are frightened.
    /// </para>
    /// </summary>
    internal sealed class KeycardSystem : IPanicOption, IBindable
    {
        /// <summary>
        /// The card's own random stream (see <see cref="DeckSystem"/> for the
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

        /// <summary>Who is on their way to it, so two people do not set off for one card.</summary>
        private int claimedBy = -1;

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

        public static bool IsFetching(Agent agent) => agent.Intent.Activity == AgentActivityState.FetchingKeycard;

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
            agent.Keycard.PocketingUntilTick = 0;
            if (claimedBy == agent.Index)
            {
                claimedBy = -1;
            }

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
            // Dead on the way to it: the card is anybody's to go for again.
            if (claimedBy == agent.Index)
            {
                claimedBy = -1;
            }

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

        // ---------------------------------------------------------------- going for it

        /// <summary>
        /// They found the card door shut: from a beat later they may go for
        /// the card. Once per person; somebody who has given the door up
        /// before already wants it.
        /// </summary>
        public void NoteTheDoorNeedsTheCard(Agent agent)
        {
            if (agent.Keycard.Held >= 0 || agent.Keycard.MayFetchFromTick >= 0)
            {
                return;
            }

            agent.Keycard.MayFetchFromTick = context.ReactionTick();
        }

        /// <summary>
        /// Considered in the panic decision, before the bottle: go back for
        /// the card. Returns no intent when this person is not doing that.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsFetching(agent))
            {
                return Update(agent, inDanger);
            }

            AgentKeycard belief = agent.Keycard;
            if (Card < 0 || belief.Held >= 0 || inDanger || agent.Body.State != AgentBodyState.Upright ||
                agent.Help.TargetIndex >= 0)
            {
                return null;
            }

            // The player's pull on the card (2026-09-28): whoever feels it
            // strongest goes for it, brave or not, and where the pull is is
            // where they go. Otherwise it takes nerve and a reason: either
            // they found the card door shut and, a beat later, want the card;
            // or they work here, the card lies free close by, and they grab
            // it on the way out rather than run to a door they know needs it.
            bool pulled = PulledToTheCard(agent, out InfluenceSystem.Place pull, out int felt);
            bool viaTheDoor = pulled && pull.Door >= 0;
            LogicalPosition where;
            if (viaTheDoor)
            {
                // The hand on the card door (2026-09-30, the owner: "the
                // hand there also sends someone who knows where the card is
                // to fetch it"): only somebody who believes it lies free
                // somewhere, and they go where they believe it lies.
                if (!belief.Knows || belief.WithSomebody)
                {
                    return null;
                }

                where = belief.Place;
            }
            else if (pulled)
            {
                where = pull.At;
            }
            else
            {
                if (!belief.Knows || belief.WithSomebody || agent.Traits.Bravery < settings.FetchBraveryMinimum)
                {
                    return null;
                }

                bool wantsIt = belief.MayFetchFromTick >= 0 && context.Tick >= belief.MayFetchFromTick && WantsACardDoor(agent);
                bool grabsItOnTheWay = agent.Knowledge.KnowsEverything && HasACardDoor() && WithinGrabRange(agent, belief.Place);
                if (!wantsIt && !grabsItOnTheWay)
                {
                    return null;
                }

                where = belief.Place;
            }

            if (claimedBy >= 0 && claimedBy != agent.Index)
            {
                if (IsStillOnTheirWay(crowd.All[claimedBy]))
                {
                    // Somebody is already on their way to it.
                    return null;
                }

                claimedBy = -1;
            }

            // What they believe, not what is so: a card they think lies on a
            // desk is worth going for even if it has since been dropped in the
            // fire, and one they think lies in the flames is not, wherever it
            // really is. The truth only counts once they are over it.
            long reach = settings.FetchRangeMillimetres;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, where) > reach * reach || FlamesNear(where))
            {
                return null;
            }

            int room = geometry.RoomOf(agent);
            int cardRoom = geometry.RoomAtPoint(where);
            if (room < 0 || cardRoom < 0 ||
                (room != cardRoom && !geometry.TryFindRoute(room, agent.Body.Position, cardRoom, agent, out _, out _, out _)))
            {
                return null;
            }

            // Turning back (2026-09-30): a walk for the card past the flames
            // is wound up to first, unless the player's hand sent them.
            if (tells != null)
            {
                TellSystem.GoingBack going = tells.BeforeGoingBack(agent, where, card, agent.Fear.ScaredEventId, pulled);
                if (going == TellSystem.GoingBack.Wait)
                {
                    return tells.StandIntent(agent);
                }

                if (going == TellSystem.GoingBack.Refuse)
                {
                    return null;
                }
            }

            if (pulled)
            {
                // Where the player pointed is where they believe it lies,
                // until they are standing over the spot.
                if (!viaTheDoor)
                {
                    Believe(belief, -1, where);
                }

                belief.PulledEventId = pull.EventId;
                influence.Answer(agent, pull, felt);
                if (agent.Traits.Bravery < settings.FetchBraveryMinimum)
                {
                    // Without the nerve to go back into the building for it
                    // of their own accord (2026-09-30).
                    influence.ActedAgainstNature(agent, AgainstTheirNature.WentForTheCard, pull.EventId, objects.IdOf(card),
                        agent.Body.Position);
                }
            }
            else
            {
                belief.PulledEventId = 0UL;
            }

            claimedBy = agent.Index;
            belief.PocketingUntilTick = 0;
            agent.Intent.Activity = AgentActivityState.FetchingKeycard;

            // A fetcher the hand sent gets twice the time (2026-09-30): the
            // crowd the same hand gathers at the door is in their way.
            agent.Intent.ActivityEndTick = checked(context.Tick +
                context.Jittered(settings.FetchTimeoutTicks * (pulled ? settings.PulledTimeoutTimes : 1)));
            return Update(agent, inDanger);
        }

        /// <summary>
        /// Whether this person's goal is the card, or the door it opens
        /// (2026-09-30), unused, driving them at
        /// <see cref="KeycardSettings.PulledToTheCardPerMille"/> or more, and
        /// they may set about it. A glancing press convinces nobody, so it
        /// turns nobody back into the building. Draws nothing.
        /// </summary>
        private bool PulledToTheCard(Agent agent, out InfluenceSystem.Place pull, out int drive)
        {
            pull = default;
            drive = 0;
            if (influence == null || !influence.TryGetPull(agent, out pull, out drive) || !pull.Pulls || pull.Spent)
            {
                return false;
            }

            bool onTheCard = pull.Thing >= 0 && pull.Thing == card;
            bool onItsDoor = pull.Door >= 0 && doors.NeedsKeycard(pull.Door);
            return (onTheCard || onItsDoor) && drive >= settings.PulledToTheCardPerMille && influence.MayAnswer(agent);
        }

        /// <summary>Whether any door still wants the card.</summary>
        private bool HasACardDoor()
        {
            for (int d = 0; d < doors.Count; d++)
            {
                if (doors.NeedsKeycard(d))
                {
                    return true;
                }
            }

            return false;
        }

        private bool WithinGrabRange(Agent agent, LogicalPosition place)
        {
            long range = settings.GrabOnTheWayRangeMillimetres;
            return LogicalPosition.DistanceSquared(agent.Body.Position, place) <= range * range;
        }

        /// <summary>
        /// Whether whoever claimed the card is still really on their way to it:
        /// alive, not out cold, and fetching. Somebody who died or was knocked
        /// out mid-fetch keeps the activity but not the claim, or nobody else
        /// would ever go for the card. A trip or a knock-down keeps it: they
        /// are up again in a moment, usually right beside the card, and
        /// handing it on then sent somebody across the building for a card
        /// the one who tripped was standing next to (measured 2026-09-27).
        /// </summary>
        private static bool IsStillOnTheirWay(Agent claimant) =>
            claimant.IsParticipating && claimant.Body.State != AgentBodyState.Unconscious && IsFetching(claimant);

        /// <summary>A card door they have found shut themselves.</summary>
        private bool WantsACardDoor(Agent agent)
        {
            for (int d = 0; d < doors.Count; d++)
            {
                if (doors.NeedsKeycard(d) && agent.Doors.FoundShut[d])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The card lies too near the flames to reach for: it does not burn, so it waits.</summary>
        private bool FlamesNear(LogicalPosition spot)
        {
            long keepAway = settings.FlamesKeepAwayMillimetres;
            return threats.NearestDistanceSquared(spot, out _, out _) < keepAway * keepAway;
        }

        /// <summary>Walking to where they believe the card is, and pocketing it once they are over it.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            int tick = context.Tick;
            AgentKeycard belief = agent.Keycard;
            // Hemmed in: a fetcher the hand sent keeps at it longer
            // (2026-09-30), the crowd the same hand gathered being what is
            // in their way.
            bool gettingNowhere = agent.Body.BlockedTicks >=
                                  settings.BlockedGiveUpTicks * (belief.PulledEventId != 0UL ? settings.PulledPatienceTimes : 1);
            if (Card < 0 || inDanger || !agent.Body.IsOnTheirFeet || agent.Burning.IsBurning ||
                tick >= agent.Intent.ActivityEndTick || gettingNowhere || claimedBy != agent.Index ||
                !belief.Knows || belief.WithSomebody || FlamesNear(belief.Place))
            {
                // Also when the claim went to somebody else while they were
                // down, or they have since seen the card taken or in the fire.
                GiveUp(agent);
                return null;
            }

            LogicalPosition actual = objects.PositionOf(card);
            long pickUp = settings.PickUpDistanceMillimetres;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, belief.Place) <= pickUp * pickUp)
            {
                // Arrived where they thought it lay. Kicked a little way off,
                // it is plainly still there and they go to it; gone, or in
                // somebody's pocket after all, they were wrong and know it.
                long nearby = LooksAroundMillimetres;
                if (objects.HolderOf(card) >= 0 || LogicalPosition.DistanceSquared(belief.Place, actual) > nearby * nearby)
                {
                    belief.Knows = false;
                    GiveUp(agent);
                    return null;
                }

                belief.Place = actual;
            }

            if (LogicalPosition.DistanceSquared(agent.Body.Position, belief.Place) > pickUp * pickUp)
            {
                // Still on the way, to floor beside where they think it lies.
                LogicalPosition beside = geometry.Navigation.NearestStandableTo(belief.Place, bodyRadius, StandingRoomMillimetres);
                if (walk.TryStep(agent, beside, agent.Personality.PanicSpeed, agent.Fear.ScaredEventId, out MotorIntent step))
                {
                    return step;
                }

                GiveUp(agent);
                return null;
            }

            if (FlamesNear(actual))
            {
                // There, but in the flames: it waits until they have passed.
                GiveUp(agent);
                return null;
            }

            if (belief.PocketingUntilTick == 0)
            {
                belief.PocketingUntilTick = checked(tick + context.Jittered(settings.PocketTicks));
            }

            if (tick < belief.PocketingUntilTick)
            {
                // Stood over it, reaching for it.
                return new MotorIntent(
                    IntegerMath.HeadingBetween(agent.Body.Position, actual, agent.Body.Heading),
                    0,
                    agent.Personality.PanicTurnRate,
                    context.Scenario.Panic.Acceleration);
            }

            walk.Forget(agent);
            ulong cause = agent.Fear.ScaredEventId;
            if (belief.PulledEventId != 0UL)
            {
                // Fetched because the player asked: the pull on it is spent,
                // and that is what pocketing it names.
                ulong spent = influence != null ? influence.Spend(agent, -1, card) : 0UL;
                cause = spent != 0UL ? spent : belief.PulledEventId;
                belief.PulledEventId = 0UL;
            }

            Pocket(agent, card, cause);
            agent.Intent.Activity = AgentActivityState.Fleeing;
            InfluenceSystem.Done(agent);
            return null;
        }

        /// <summary>Back to running, with no card.</summary>
        private void GiveUp(Agent agent)
        {
            walk.Forget(agent);
            bool pulled = agent.Keycard.PulledEventId != 0UL;
            agent.Keycard.PulledEventId = 0UL;
            if (pulled && influence != null)
            {
                // Sent by the hand and got nowhere: it costs them, and the
                // beat lets the claim pass to somebody else.
                influence.GiveUp(agent);
            }
            else
            {
                InfluenceSystem.Interrupted(agent);
            }
            if (claimedBy == agent.Index)
            {
                claimedBy = -1;
            }

            if (IsFetching(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                context.ThinkAgainSoon(agent.Intent);
            }
        }

        // ---------------------------------------------------------------- tests

        /// <summary>Tests and measurements: whether the card lies too near the flames for anybody to go for it.</summary>
        internal bool CardNearFlamesForTests => Card >= 0 && FlamesNear(objects.PositionOf(card));

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
