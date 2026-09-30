using System;
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
    /// everywhere at once"). A press puts a full pull on the place at once
    /// and replaces whatever was held before; a release takes it away at
    /// once. One place at a time, nothing fades, nothing stacks.
    /// </para>
    /// <para>
    /// A place is felt within <see cref="InfluenceSettings.ReachMillimetres"/>
    /// of it as a walk: straight across the room it is in, or through one open
    /// doorway into the room next door, never through a wall or a shut door.
    /// How much a person feels it is arithmetic on where they stand and who
    /// they are (<see cref="FeltBy"/>), so asking draws no random numbers and
    /// a hand nobody is near changes no run.
    /// </para>
    /// <para>
    /// <b>The fourth pass (2026-09-30): the hand holds.</b> The owner: "you
    /// should feel the influence and see them guided, but still see their
    /// personality ... think magnets and fish/bird clusters ... if getting them
    /// to notice or sway their focus, the focus should mostly stay." Every
    /// person now carries the hand's ask as their own goal
    /// (<see cref="AgentHand"/>): a copy of the place, and a <em>conviction</em>
    /// that builds every tick they feel the hand -- fast beside it, slowly at
    /// the edge of its reach, faster the more easily led they are -- and that
    /// is the one number everything asks. They set about the task once it
    /// passes <see cref="InfluenceSettings.AnswerFromPerMille"/>; they keep the
    /// task after the hand comes off once it has passed
    /// <see cref="InfluenceSettings.CommitFromPerMille"/> ("after some influence
    /// points spent they should stick to that choice"), and a kept goal fades
    /// on their own beat, the strong-willed first, until it is gone. There are
    /// no chance gates and no never-again markers any more: a failed attempt
    /// costs conviction and a beat, then they try again. What ends a goal is
    /// one rule for everybody, in <see cref="Advance"/> and one line of
    /// <c>PanicBehaviour</c>: out cold, alight, the player's tug, a felt push,
    /// flames inside their danger distance, or a fresh press they feel, which
    /// replaces the goal and keeps their attention.
    /// </para>
    /// <para>
    /// Two kinds of question, on purpose. <see cref="TryGetLivePull"/> and
    /// <see cref="TryGetLivePush"/> are the hand <em>now on</em>, felt and
    /// noticed -- what a tell is caught by, what the startled edge toward,
    /// what a push does. <see cref="TryGetPull"/> is what this person is
    /// <em>doing for</em> the hand, live or kept, with a drive that is the
    /// greater of their conviction and what they feel now. Everything a person
    /// does for the hand asks the second.
    /// </para>
    /// <para>
    /// Somebody who does what the pull asked (opens the door, sits on the
    /// chair, pockets the card) spends its <em>use</em>: the place goes on
    /// gathering people while it is held, but nobody uses it again until it
    /// is pressed afresh.
    /// </para>
    /// </summary>
    internal sealed class InfluenceSystem : IBindable
    {
        /// <summary>One influenced place: the hand's press, and a person's copy of it.</summary>
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
            /// pushes, a door that pushes is never chosen, and nobody keeps a
            /// push as a goal.
            /// </summary>
            public bool Repels;

            /// <summary>
            /// The tick a click's beacon comes off by itself, or 0 while the
            /// button is still held (2026-09-30, the owner: "a single click
            /// should place an influence beacon for 3 seconds").
            /// </summary>
            public int EndsAtTick;

            /// <summary>
            /// For a door: whether it stood open when the hand went on it
            /// (2026-09-30). What the hand asks is the opposite -- shut it if it
            /// was open, open it if it was shut -- decided at the press, so a
            /// door somebody else opened meanwhile is not shut again.
            /// </summary>
            public bool DoorWasOpen;

            /// <summary>Whether this place draws people to it: the left button's hand.</summary>
            public bool Pulls => !Repels;
        }

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly InfluenceSettings settings;

        /// <summary>The held place, if any: a list of at most one, so the callers that walk places need not change.</summary>
        private readonly List<Place> places = new List<Place>(1);

        /// <summary>Everybody, for the conviction bookkeeping each tick. Bound after construction.</summary>
        private Crowd crowd;

        public InfluenceSystem(SimulationContext context, WorldGeometry geometry)
        {
            this.context = context;
            this.geometry = geometry;
            settings = context.Scenario.Influence;
        }

        public void Bind(Systems systems)
        {
            crowd = systems.Crowd;
        }

        /// <summary>How many places are influenced right now: one while the button is down, else none.</summary>
        public int Count => places.Count;

        /// <summary>The <paramref name="i"/>-th place.</summary>
        public Place this[int i] => places[i];

        /// <summary>A held place's level: always the full one. Kept for the display and the tests.</summary>
        public int LevelOf(int i) => settings.MaximumLevel;

        /// <summary>The hand's press, while one is on: what somebody answering it remembers. 0 when the hand is off.</summary>
        public ulong CurrentPress => places.Count > 0 ? places[0].EventId : 0UL;

        // ---------------------------------------------------------------- the hand itself

        /// <summary>The player's hand goes on a door. The door must be one the building has; the command system has checked.</summary>
        public void OnDoor(int door, SimulationId doorId, bool repels = false)
        {
            int roomA = geometry.DoorRoom(door);
            int roomB = geometry.RoomBeyond(door, roomA);
            Press(door, -1, doorId, geometry.DoorCentre(door), roomA, roomB, repels);
            Place place = places[0];
            place.DoorWasOpen = geometry.IsDoorOpen(door);
            places[0] = place;
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
            place.EndsAtTick = Math.Max(context.Tick + 1, place.PressTick + settings.BeaconTicks);
            places[0] = place;
        }

        /// <summary>
        /// The held hand slides to a spot (2026-09-30, the owner: "when left
        /// click is held, if then dragged the influence point should move with
        /// the pointer. So agents can be guided with this"). Wherever it was,
        /// it is a hand on the floor now; the press stays the same press, so
        /// whoever was answering it goes on answering it and follows it
        /// (their copy of the place is refreshed each tick they feel it).
        /// Nothing for a beacon, for no hand, or for a spot off the floor.
        /// Nothing is written: the command is in the run.
        /// </summary>
        public void Move(LogicalPosition at)
        {
            if (places.Count == 0 || places[0].EndsAtTick > 0)
            {
                return;
            }

            int room = geometry.RoomAtPoint(at);
            if (room < 0)
            {
                return;
            }

            Place place = places[0];
            place.Door = -1;
            place.Thing = -1;
            place.Target = default;
            place.At = at;
            place.RoomA = room;
            place.RoomB = room;
            places[0] = place;
        }

        /// <summary>
        /// The Tab panel's dial (2026-09-30): how strongly everybody feels the
        /// hand from the next tick on, in this run's own settings.
        /// </summary>
        public void SetStrength(int percent)
        {
            settings.StrengthPercent = Math.Max(0, Math.Min(InfluenceSettings.MaximumStrengthPercent, percent));
        }

        /// <summary>
        /// The Tab panel's second dial (2026-09-30): how far the hand is felt
        /// from the next tick on, in this run's own settings. Nothing is
        /// cached from the reach, so a person at the old edge simply feels
        /// more or nothing next tick.
        /// </summary>
        public void SetReach(int millimetres)
        {
            settings.ReachMillimetres = Math.Max(InfluenceSettings.MinimumReachMillimetres,
                Math.Min(InfluenceSettings.MaximumReachMillimetres, millimetres));
        }

        /// <summary>
        /// The player lets go: the place is gone at once. Whoever was acting
        /// for it keeps the task if their conviction has reached the commit
        /// line, and drops it otherwise (<see cref="Advance"/>, this tick).
        /// Nothing written when nothing was held.
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
        /// not used again until pressed afresh. The event's id, or 0 when
        /// nothing unspent was on it, in which case nothing is written.
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

        /// <summary>Whether this pull, live or a person's copy, is on the floor beside a spot (a pull station) and not yet used.</summary>
        public bool IsPullingNear(in Place place, LogicalPosition at)
        {
            long stack = settings.StackRadiusMillimetres;
            return place.Door < 0 && !place.Spent && place.Pulls &&
                   LogicalPosition.DistanceSquared(place.At, at) <= stack * stack;
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

        // ---------------------------------------------------------------- feeling it

        /// <summary>
        /// How strongly this person feels this place, per mille of a full pull
        /// felt by an ordinary person standing on it, times how easily led
        /// they are (<see cref="Susceptibility"/>). Full within half the reach,
        /// then fading to nothing at <see cref="InfluenceSettings.ReachMillimetres"/>.
        /// Distance is a walk: across the room, or through one open doorway
        /// from the room next door; nothing through a wall or a shut door.
        /// The same for a pull and a push: which way it acts is the caller's.
        /// </summary>
        public int FeltBy(Agent agent, int i) => FeltBy(agent, places[i]);

        /// <summary>The same, for any place: the live one or a person's copy of it.</summary>
        public int FeltBy(Agent agent, in Place place)
        {
            // The room they count as in: in a doorway, the one they were last
            // in (2026-09-30; it used to be only a room holding their whole
            // body, so anybody in a doorway felt nothing).
            int room = geometry.RoomOf(agent);
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
            long felt = distance <= full ? 1000L : 1000L * (reach - distance) / Math.Max(1L, reach - full);

            // Everybody's reading of the hand, scaled by the one dial
            // (2026-09-30, the Tab panel's hand strength).
            return (int)(felt * Susceptibility(agent) / 100L * settings.StrengthPercent / 100L);
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
                          settings.PercentPerLeadership * Math.Max(0, traits.Leadership - 5) -
                          settings.PercentPerEvil * Math.Max(0, traits.Evil - 5) +
                          (agent.Knowledge.KnowsEverything ? 0 : settings.VisitorPercent);
            return Math.Max(settings.MinimumPercent, Math.Min(settings.MaximumPercent, percent));
        }

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

            AgentHand hand = agent.Hand;
            if (hand.NoticedPress != press)
            {
                hand.NoticedPress = press;
                hand.NoticedAtTick = context.ReactionTick();
            }

            return context.Tick >= hand.NoticedAtTick;
        }

        /// <summary>
        /// The hand on <em>now</em>, if it pulls and this person feels it and
        /// has taken it in: what a tell is caught by and the startled edge
        /// toward, whether or not they have made it their goal. Nothing for the
        /// strongest wills.
        /// </summary>
        public bool TryGetLivePull(Agent agent, out Place place, out int felt)
        {
            place = default;
            felt = 0;
            if (places.Count == 0 || places[0].Repels || Refuses(agent))
            {
                return false;
            }

            felt = FeltBy(agent, 0);
            if (felt <= 0 || !HasNoticed(agent))
            {
                felt = 0;
                return false;
            }

            place = places[0];
            return true;
        }

        /// <summary>The hand on now, if it pushes and this person feels it and has taken it in. A push is never kept as a goal.</summary>
        public bool TryGetLivePush(Agent agent, out Place place, out int felt)
        {
            place = default;
            felt = 0;
            if (places.Count == 0 || !places[0].Repels || Refuses(agent))
            {
                return false;
            }

            felt = FeltBy(agent, 0);
            if (felt <= 0 || !HasNoticed(agent))
            {
                felt = 0;
                return false;
            }

            place = places[0];
            return true;
        }

        /// <summary>
        /// What this person is doing for the hand, or could: their goal -- the
        /// live press while it is on and felt, or the copy they kept when it
        /// came off -- and how hard it drives them, per mille: the greater of
        /// their conviction and what they feel right now. False for nobody's
        /// goal. Everything a person does for the hand asks this.
        /// </summary>
        public bool TryGetPull(Agent agent, out Place place, out int drive)
        {
            AgentHand hand = agent.Hand;
            place = hand.Goal;
            drive = 0;
            if (hand.Press == 0UL)
            {
                return false;
            }

            drive = hand.Conviction;
            if (places.Count > 0 && places[0].EventId == hand.Press)
            {
                drive = Math.Max(drive, FeltBy(agent, 0));
            }

            return true;
        }

        /// <summary>
        /// Whether this person may set about their goal now: conviction past
        /// the answer line, not the strongest of wills, and not inside the beat
        /// after a give-up. Draws nothing.
        /// </summary>
        public bool MayAnswer(Agent agent)
        {
            AgentHand hand = agent.Hand;
            return hand.Press != 0UL && !Refuses(agent) && hand.Conviction >= settings.AnswerFromPerMille &&
                   context.Tick >= hand.RetryFromTick;
        }

        /// <summary>
        /// Whether this person is doing what the hand asked (the gold hand
        /// over them): they have set about their goal, or they hold a bottle
        /// they took for it. Asks nothing random.
        /// </summary>
        public bool IsActingFor(Agent agent)
        {
            if (agent.Carry.ForTheHand && agent.Carry.Holding)
            {
                return true;
            }

            return agent.Hand.Acting && agent.Hand.Press != 0UL;
        }

        /// <summary>Whether this person keeps a goal the hand has come off (the gold hand, still).</summary>
        public bool IsCommitted(Agent agent) => agent.Hand.Press != 0UL && agent.Hand.Committed;

        /// <summary>Whether this person's goal is this press.</summary>
        public static bool IsAnswering(Agent agent, ulong press) => press != 0UL && agent.Hand.Press == press;

        /// <summary>
        /// They set about their goal: the gold hand goes up, and the log says
        /// once per press that they were drawn by it. <paramref name="drive"/>
        /// is what the log records as the strength.
        /// </summary>
        public void Answer(Agent agent, in Place place, int drive)
        {
            AgentHand hand = agent.Hand;
            hand.Acting = true;
            if (hand.AnsweredPress != place.EventId)
            {
                hand.AnsweredPress = place.EventId;
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentDrawnByInfluence, agent.Body.Position,
                    drive, 0, place.EventId, place.Target);
            }
        }

        /// <summary>
        /// The task is done (the door used, the chair sat on, the card
        /// pocketed, the heap cleared): the goal is over, and their conviction
        /// stays, so a hand still on asks them again for the next thing.
        /// </summary>
        public static void Done(Agent agent)
        {
            AgentHand hand = agent.Hand;
            hand.Acting = false;
            hand.AgainstTheirNature = false;
            hand.Committed = false;
            hand.GotUpForIt = false;
            hand.Press = 0UL;
            hand.Goal = default;
        }

        /// <summary>
        /// The task failed (no way there, the door would not shut, hemmed in
        /// too long): it costs them <see cref="InfluenceSettings.GiveUpCostPerMille"/>
        /// of their conviction and a beat before they try again; below the
        /// answer line they drop it altogether. The one draw here is the
        /// beat's jitter, for somebody who was acting.
        /// </summary>
        public void GiveUp(Agent agent)
        {
            AgentHand hand = agent.Hand;
            hand.Acting = false;
            hand.AgainstTheirNature = false;
            hand.GotUpForIt = false;
            hand.Conviction -= settings.GiveUpCostPerMille;
            hand.RetryFromTick = checked(context.Tick + Math.Max(context.ReactionLag(), context.Jittered(settings.RetryAfterTicks)));
            if (hand.Conviction < settings.AnswerFromPerMille)
            {
                Drop(agent);
            }
        }

        /// <summary>On their own again entirely: no goal, no conviction. The once-per-press markers stay, so nothing is written twice.</summary>
        public static void Drop(Agent agent)
        {
            AgentHand hand = agent.Hand;
            hand.Press = 0UL;
            hand.Goal = default;
            hand.Committed = false;
            hand.Conviction = 0;
            hand.Acting = false;
            hand.AgainstTheirNature = false;
            hand.GotUpForIt = false;
        }

        /// <summary>
        /// What they were doing for the hand is cut short by something else
        /// (taking fright, calming down): the task stops, and the goal and the
        /// conviction stay, so the other side of them answers it afresh.
        /// </summary>
        public static void Interrupted(Agent agent)
        {
            AgentHand hand = agent.Hand;
            hand.Acting = false;
            hand.AgainstTheirNature = false;
            hand.GotUpForIt = false;
        }

        /// <summary>The flames inside their danger distance: the hand is out of their head.</summary>
        public static void Endangered(Agent agent) => Drop(agent);

        /// <summary>
        /// Somebody does for the hand what they never would of their own
        /// accord: written once, for the sign and the story, and remembered so
        /// the drawing trembles them while they do it.
        /// </summary>
        public void ActedAgainstNature(Agent agent, AgainstTheirNature what, ulong press, SimulationId target,
            LogicalPosition at)
        {
            agent.Hand.AgainstTheirNature = true;
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentActedForTheHand, at, (int)what, 0,
                press, target);
        }

        /// <summary>Doing for the hand, right now, what is against their nature: the drawing trembles them.</summary>
        public bool IsActingAgainstNature(Agent agent) => agent.Hand.AgainstTheirNature && IsActingFor(agent);

        // ---------------------------------------------------------------- each tick

        /// <summary>
        /// Phase 1's tail. A click's beacon whose time is up comes off by
        /// itself, exactly as a release would. Then everybody's conviction, in
        /// index order, by the one rule (2026-09-30):
        /// <list type="bullet">
        /// <item>out cold, alight, or in the player's hand: the goal is dropped;</item>
        /// <item>a push felt and taken in drops a goal;</item>
        /// <item>a pull felt and taken in becomes the goal (a fresh press
        /// replaces the old goal and keeps the conviction), the copy follows
        /// the hand, and conviction grows by what is felt;</item>
        /// <item>a goal whose hand is off or elsewhere is kept once conviction
        /// has passed the commit line, else dropped; a kept goal fades once a
        /// second on their own beat, faster the less easily led they are,
        /// and is dropped at nothing.</item>
        /// </list>
        /// Draws: <see cref="HasNoticed"/> draws one reaction tick per person
        /// per press, only for somebody who feels it; nothing else here draws.
        /// </summary>
        public void Advance()
        {
            if (places.Count > 0 && places[0].EndsAtTick > 0 && context.Tick >= places[0].EndsAtTick)
            {
                Release();
            }

            if (crowd == null)
            {
                return;
            }

            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                AgentHand hand = agent.Hand;
                if (!agent.IsParticipating || agent.Body.State == AgentBodyState.Unconscious || agent.Burning.IsBurning ||
                    agent.Tug.Held)
                {
                    if (hand.Press != 0UL)
                    {
                        Drop(agent);
                    }

                    continue;
                }

                if (places.Count > 0 && !Refuses(agent))
                {
                    Place live = places[0];
                    int felt = FeltBy(agent, 0);
                    if (felt > 0 && HasNoticed(agent))
                    {
                        if (live.Repels)
                        {
                            if (hand.Press != 0UL)
                            {
                                Drop(agent);
                            }

                            continue;
                        }

                        if (hand.Press != live.EventId)
                        {
                            // A fresh press: the old goal is over, their
                            // attention to the player is not.
                            hand.Press = live.EventId;
                            hand.Committed = false;
                            hand.Acting = false;
                            hand.AgainstTheirNature = false;
                            hand.GotUpForIt = false;
                        }

                        hand.Goal = live;
                        hand.Conviction = Math.Min(1000, hand.Conviction + felt * settings.ConvictionGainPerTickAtFullPull / 1000);
                        continue;
                    }
                }

                if (hand.Press == 0UL)
                {
                    continue;
                }

                if (places.Count > 0 && places[0].EventId == hand.Press)
                {
                    // Still on, only not felt from here: they keep at it.
                    continue;
                }

                if (!hand.Committed)
                {
                    if (hand.Conviction >= settings.CommitFromPerMille)
                    {
                        hand.Committed = true;
                    }
                    else
                    {
                        Drop(agent);
                    }

                    continue;
                }

                if ((context.Tick + agent.Index) % Run.TicksPerSecond == 0)
                {
                    int susceptibility = Math.Max(1, Susceptibility(agent));
                    hand.Conviction -= settings.CommittedDecayPerSecondPerMille * 100 / susceptibility;
                    if (hand.Conviction <= 0)
                    {
                        Drop(agent);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- where and which way

        /// <summary>
        /// Where somebody answering a hand on the floor stands (2026-09-30): a
        /// spot of their own in a loose ring round it -- nine tenths of a metre
        /// out, then one and four tenths, then one and nine, six to a ring,
        /// by their number -- on floor they can stand on. Draws nothing;
        /// worked out afresh from where the place is now, so a hand dragged
        /// along carries them with it.
        /// </summary>
        public LogicalPosition GatherSpotFor(Agent agent, in Place place)
        {
            LogicalPosition at = place.At;
            int ring = agent.Index / GatherRingSize % GatherRingRadii.Length;
            int heading = IntegerMath.NormalizeDegrees(agent.Index % GatherRingSize * (360 / GatherRingSize) + ring * 30);
            LogicalPosition spot = at + IntegerMath.Displacement(heading, GatherRingRadii[ring]);
            if (geometry.RoomAtPoint(spot) < 0)
            {
                // Past a wall: the same side, closer in.
                spot = at + IntegerMath.Displacement(heading, GatherRingRadii[0] / 2);
                if (geometry.RoomAtPoint(spot) < 0)
                {
                    spot = at;
                }
            }

            return geometry.Navigation.NearestStandableTo(spot, context.Scenario.World.OccupancyRadiusMillimetres,
                GatherStandingRoomMillimetres);
        }

        /// <summary>The ring round a hand people answering it stand in: how many to a ring, and each ring's distance out.</summary>
        private const int GatherRingSize = 6;
        private static readonly int[] GatherRingRadii = { 900, 1400, 1900 };
        private const int GatherStandingRoomMillimetres = 1200;

        /// <summary>
        /// Where somebody pushed by the hand walks to (2026-09-30; the calm
        /// behaviour's own rule, shared now with the frightened): straight
        /// away from the push, out past its full strength, in the room they
        /// are in -- halved up to three times to stay in it. Draws nothing.
        /// </summary>
        public LogicalPosition AwayFromThePush(Agent agent, in Place push)
        {
            LogicalPosition from = agent.Body.Position;
            long distance = IntegerMath.Distance(from, push.At);
            int away = distance > 0
                ? IntegerMath.HeadingBetween(push.At, from, agent.Body.Heading)
                : IntegerMath.NormalizeDegrees(agent.Body.Heading + 180);
            long fullRadius = (long)settings.ReachMillimetres * settings.FullWithinPercent / 100L;
            int walk = (int)Math.Max(MinimumPushWalkMillimetres,
                Math.Min(MaximumPushWalkMillimetres, fullRadius + PushClearanceMillimetres - distance));

            int room = geometry.RoomOf(agent);
            LogicalPosition target = from + IntegerMath.Displacement(away, walk);
            for (int attempt = 0; attempt < 3 && geometry.RoomAtPoint(target) != room; attempt++)
            {
                walk /= 2;
                target = from + IntegerMath.Displacement(away, walk);
            }

            return geometry.ClampIntoRoom(from, target);
        }

        /// <summary>Whether they stand inside a push's full strength and a little more: the ground it clears.</summary>
        public bool InsideThePush(Agent agent, in Place push)
        {
            long clear = (long)settings.ReachMillimetres * settings.FullWithinPercent / 100L + PushClearanceMillimetres / 2;
            return LogicalPosition.DistanceSquared(agent.Body.Position, push.At) < clear * clear;
        }

        /// <summary>How far somebody pushed walks, at the least and the most, and how far past the push's full strength they aim.</summary>
        internal const int MinimumPushWalkMillimetres = 2000;
        internal const int MaximumPushWalkMillimetres = 8000;
        internal const int PushClearanceMillimetres = 1500;

        /// <summary>
        /// How far it is to the place from here: straight, in one of the
        /// place's rooms; else through the nearest open doorway that joins
        /// this room to one of them, doorway to place added on; else out of
        /// reach. One doorway deep, which is "about a room's length".
        /// </summary>
        private long WalkTo(int room, LogicalPosition from, in Place place)
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
        /// What running for this door is worth to somebody, in millimetres, for
        /// the door choice to add: the full bonus for their goal on it, driven
        /// hard, nothing when it is not their goal -- and as much taken off for
        /// a hand pushing people away from it, felt and taken in.
        /// </summary>
        public long DoorBonus(Agent agent, int door)
        {
            if (TryGetPull(agent, out Place goal, out int drive) && goal.Door == door)
            {
                return (long)drive * settings.FullPullBonusMillimetres / 1000L;
            }

            if (TryGetLivePush(agent, out Place push, out int felt) && push.Door == door)
            {
                return -((long)felt * settings.FullPullBonusMillimetres / 1000L);
            }

            return 0L;
        }

        /// <summary>
        /// What heading for a spot is worth to somebody, in millimetres: for a
        /// goal on the floor or on a thing, its drive times how far the way to
        /// the candidate agrees with the way to the place (the full pull for
        /// straight toward it, nothing square to it, the same off for straight
        /// away), and the other way round for a felt push -- a door that
        /// pushes included, so a frightened crowd steers off it. A door that
        /// pulls is left to <see cref="DoorBonus"/>, and so is
        /// <paramref name="exceptDoor"/>, the door a choice is scoring already,
        /// so a push on it is not counted twice.
        /// </summary>
        public long SpotBonus(Agent agent, LogicalPosition candidate, int exceptDoor = -1)
        {
            long total = 0L;
            LogicalPosition from = agent.Body.Position;
            if (TryGetPull(agent, out Place goal, out int drive) && goal.Door < 0 && drive > 0)
            {
                int towardIt = IntegerMath.HeadingBetween(from, goal.At, agent.Body.Heading);
                long agreement = ExitSignBehaviour.Agreement(from, candidate, towardIt);
                total += (long)drive * settings.FullPullBonusMillimetres / 1000L * agreement / IntegerMath.TrigScale;
            }

            if (TryGetLivePush(agent, out Place push, out int felt) && !(push.Door >= 0 && push.Door == exceptDoor))
            {
                int towardIt = IntegerMath.HeadingBetween(from, push.At, agent.Body.Heading);
                long agreement = ExitSignBehaviour.Agreement(from, candidate, towardIt);
                total -= (long)felt * settings.FullPullBonusMillimetres / 1000L * agreement / IntegerMath.TrigScale;
            }

            return total;
        }

        // ---------------------------------------------------------------- the display

        /// <summary>
        /// For the display: the held place, and everybody still in the building
        /// who feels it or keeps a goal from it, with how strongly and whether
        /// they are acting on it. Fills the buffers it is given, so a tick
        /// allocates nothing.
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

            for (int a = 0; a < agents.Length; a++)
            {
                Agent agent = agents[a];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                if (TryGetPull(agent, out _, out int drive) && drive > 0)
                {
                    intoPulls.Add(new InfluencePullSnapshot(agent.Id, a, 0, drive, IsActingFor(agent), IsCommitted(agent)));
                }
                else if (places.Count > 0)
                {
                    int felt = FeltBy(agent, 0);
                    if (felt > 0)
                    {
                        intoPulls.Add(new InfluencePullSnapshot(agent.Id, a, 0, felt, IsActingFor(agent)));
                    }
                }
            }
        }
    }
}
