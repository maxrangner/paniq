namespace Paniq.Simulation
{
    /// <summary>
    /// Wedging a door shut on purpose. Somebody who has given up on getting out
    /// of the building and is sheltering in a room fetches the nearest thing they
    /// can lift and sets it down in a doorway, which jams the door for everybody.
    /// Two sorts of people do it, for opposite reasons: the frightened, to keep
    /// the fire out, and the cruel, to keep other people out. The kind never do
    /// it while somebody is still coming through.
    /// <para>
    /// Built like <see cref="HelpBehaviour"/>: one step in the panic decision
    /// that returns what the body should do, or nothing when this person is not
    /// barricading anything.
    /// </para>
    /// </summary>
    internal sealed class BarricadeBehaviour : IPanicOption
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly Threats threats;
        private readonly PhysicsObjectSystem objects;
        private readonly FlammablesSystem flammables;
        private readonly Locomotion locomotion;
        private readonly BlockadeSettings settings;
        private readonly PanicSettings panic;

        /// <summary>
        /// Per door slot: who last set out to wedge it (their index), or -1.
        /// Read through <see cref="IsTakenByAnybodyElse"/>, which checks they
        /// are still on it. This used to be a walk of the whole crowd for
        /// every door a frightened person considered.
        /// </summary>
        private readonly int[] barricaderOf;

        public BarricadeBehaviour(
            SimulationContext context,
            Crowd crowd,
            WorldGeometry geometry,
            DoorSystem doors,
            Threats threats,
            PhysicsObjectSystem objects,
            FlammablesSystem flammables,
            Locomotion locomotion)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.threats = threats;
            this.objects = objects;
            this.flammables = flammables;
            this.locomotion = locomotion;
            settings = context.Scenario.Blockades;
            panic = context.Scenario.Panic;
            barricaderOf = new int[geometry.DoorSlotCount];
            for (int i = 0; i < barricaderOf.Length; i++)
            {
                barricaderOf[i] = -1;
            }
        }

        public static bool IsBarricading(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.FetchingBarricade ||
                   activity == AgentActivityState.CarryingBarricade ||
                   activity == AgentActivityState.Barricading;
        }

        /// <summary>
        /// Considered in the panic decision. Returns no intent when this person
        /// is not wedging a door.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsBarricading(agent))
            {
                // Already under way: they finish it even if a clear exit
                // opens up in the meantime.
                return Update(agent, inDanger);
            }

            if (eager)
            {
                // A way out stands open in front of them: nobody starts
                // wedging themselves into a room while that is true.
                return null;
            }

            if (inDanger || agent.Body.State != AgentBodyState.Upright || agent.Carry.ItemIndex >= 0 ||
                agent.Help.TargetIndex >= 0 || agent.Burning.IsBurning)
            {
                return null;
            }

            if (!WouldBarricade(agent))
            {
                return null;
            }

            int room = geometry.RoomOf(agent);
            if (room < 0 || threats.IsInRoom(room))
            {
                // Their own room is alight: wedging its doors saves nobody.
                return null;
            }

            // Either they have nowhere left to go and are digging in, or there
            // are flames on the other side of a door and they want it sealed.
            int door = ChooseDoor(agent, room, agent.Doors.ExitDoorIndex < 0);
            if (door < 0)
            {
                return null;
            }

            int item = ChooseItem(agent, room);
            if (item < 0)
            {
                return null;
            }

            agent.Barricade.DoorIndex = door;
            barricaderOf[door] = agent.Index;
            agent.Carry.ItemIndex = item;
            agent.Carry.Holding = false;
            agent.Intent.Activity = AgentActivityState.FetchingBarricade;
            agent.Barricade.GiveUpTick = checked(context.Tick + settings.BarricadeTimeoutTicks);
            return Update(agent, inDanger);
        }

        /// <summary>The frightened do it to keep the fire out; the cruel to keep people out.</summary>
        private bool WouldBarricade(Agent agent)
        {
            return agent.Traits.Nervousness >= settings.BarricadeNervousMinimum ||
                   agent.Traits.Evil >= settings.BarricadeEvilMinimum;
        }

        /// <summary>
        /// A shut door of the room they are sheltering in that nothing is wedged
        /// in yet and nobody else is already wedging: the one with fire beyond it
        /// first, then the nearest.
        /// </summary>
        private int ChooseDoor(Agent agent, int room, bool nowhereLeftToGo)
        {
            int[] candidates = geometry.RoomDoors(room);
            int best = -1;
            long bestScore = long.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                if (geometry.IsDoorOpen(door) || doors.IsObstructed(door) || IsTakenByAnybodyElse(agent, door))
                {
                    continue;
                }

                int beyondRoom = geometry.RoomBeyond(door, room);
                bool flamesBeyond = beyondRoom >= 0 && threats.IsInRoom(beyondRoom);
                if (!nowhereLeftToGo && !flamesBeyond)
                {
                    // Still hoping to walk out, and nothing is coming through
                    // this door: no reason to seal it.
                    continue;
                }

                if (door == agent.Doors.ExitDoorIndex && !flamesBeyond)
                {
                    // Never seal the door they are counting on — unless the room
                    // beyond it is alight, in which case it has stopped being a
                    // way out and become the thing coming for them.
                    continue;
                }

                // The kind will not seal a door with somebody still coming.
                if (agent.Traits.Compassion >= context.Scenario.Exits.CompassionHoldMinimum &&
                    SomebodyBeyond(agent, door, room))
                {
                    continue;
                }

                long score = LogicalPosition.DistanceSquared(agent.Body.Position, geometry.DoorCentre(door));
                if (flamesBeyond)
                {
                    // Flames on the other side: this is the one that matters.
                    score -= 100_000_000L;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = door;
                }
            }

            return best;
        }

        private bool IsTakenByAnybodyElse(Agent agent, int door)
        {
            int taker = barricaderOf[door];
            return taker >= 0 && crowd.All[taker] != agent && crowd.All[taker].Barricade.DoorIndex == door;
        }

        private bool SomebodyBeyond(Agent agent, int door, int room)
        {
            int beyond = geometry.RoomBeyond(door, room);
            if (beyond < 0)
            {
                // A door to the street: "beyond" is nobody's room, and the
                // question as always asked matches anybody standing in a
                // doorway anywhere. Kept as it was; it is a rare question.
                Agent[] people = crowd.All;
                for (int i = 0; i < people.Length; i++)
                {
                    Agent other = people[i];
                    if (other != agent && other.IsParticipating && geometry.RoomAt(other.Body.Position) < 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            using Crowd.Nearby near = crowd.Gather(geometry.RoomBounds(beyond));
            for (int c = 0; c < near.Count; c++)
            {
                Agent other = crowd.All[near[c]];
                if (other != agent && other.IsParticipating && geometry.RoomAt(other.Body.Position) == beyond)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The nearest thing in their room they can lift and nobody else is using.</summary>
        private int ChooseItem(Agent agent, int room)
        {
            int best = -1;
            long bestDistance = (long)settings.BarricadeFetchRangeMillimetres * settings.BarricadeFetchRangeMillimetres;
            using PhysicsObjectSystem.Nearby near =
                objects.Gather(UniformGridIndex.Around(agent.Body.Position, settings.BarricadeFetchRangeMillimetres));
            for (int c = 0; c < near.Count; c++)
            {
                int i = near[c];
                if (objects.IsDormant(i) || objects.HolderOf(i) >= 0 || objects.OccupantOf(i) >= 0 ||
                    objects.IsEquipment(i) || objects.IsMoving(i) ||
                    !objects.CanLift(agent, i) || flammables.ObjectState(i) != ObjectBurnState.Intact ||
                    geometry.RoomAtPoint(objects.PositionOf(i)) != room)
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(agent.Body.Position, objects.PositionOf(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Walking to the thing, carrying it to the door, and wedging it in.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            int door = agent.Barricade.DoorIndex;
            int item = agent.Carry.ItemIndex;
            int room = geometry.RoomOf(agent);
            if (door < 0 || item < 0 || inDanger || !agent.Body.IsOnTheirFeet ||
                agent.Burning.IsBurning || context.Tick >= agent.Barricade.GiveUpTick ||
                agent.Body.BlockedTicks >= settings.BarricadeBlockedGiveUpTicks ||
                geometry.IsDoorOpen(door) || doors.IsObstructed(door) ||
                room < 0 || threats.IsInRoom(room))
            {
                GiveUp(agent);
                return null;
            }

            if (agent.Intent.Activity == AgentActivityState.FetchingBarricade)
            {
                if (objects.HolderOf(item) >= 0 || objects.IsMoving(item))
                {
                    // Somebody else took it.
                    GiveUp(agent);
                    return null;
                }

                LogicalPosition thing = objects.PositionOf(item);

                // The same reach as tidying up: arm's length past both bodies.
                long reach = context.Scenario.World.OccupancyRadiusMillimetres + (long)objects.RadiusOf(item) +
                             context.Scenario.Items.ReachMillimetres;
                if (LogicalPosition.DistanceSquared(agent.Body.Position, thing) > reach * reach)
                {
                    return WalkTo(agent, thing);
                }

                objects.PickUp(item, agent);
                agent.Carry.Holding = true;
                agent.Intent.Activity = AgentActivityState.CarryingBarricade;
                return FaceTowards(agent, thing, 0);
            }

            LogicalPosition spot = WedgeSpot(door, room);
            if (agent.Intent.Activity == AgentActivityState.Barricading)
            {
                if (context.Tick < agent.Intent.ActivityEndTick)
                {
                    // Reaching in and wedging it: standing still, facing the gap.
                    return FaceTowards(agent, spot, 0);
                }

                if (objects.TrySetDownAt(item, spot, agent.Fear.ScaredEventId))
                {
                    context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentBarricadedDoor, spot,
                        0, 0, agent.Fear.ScaredEventId, doors.IdOf(door));
                    agent.Carry.ItemIndex = -1;
                    agent.Carry.Holding = false;
                }

                GiveUp(agent);
                return null;
            }

            // They stand back from the gap and reach in, rather than standing on
            // the spot, so the thing is never set down on top of them.
            LogicalPosition standing = StandingSpot(door, room, item);

            // A standing position rather than a reach, so near enough is near
            // enough: within their own width of it.
            long arrival = context.Scenario.World.OccupancyRadiusMillimetres;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, standing) > arrival * arrival)
            {
                return WalkTo(agent, standing);
            }

            agent.Intent.Activity = AgentActivityState.Barricading;
            agent.Intent.ActivityEndTick = checked(context.Tick + settings.BarricadeSetDownTicks);
            return FaceTowards(agent, spot, 0);
        }

        /// <summary>Dead centre of the gap, just short of the wall, on their side of it.</summary>
        private LogicalPosition WedgeSpot(int door, int room)
        {
            int outward = -(context.Scenario.World.OccupancyRadiusMillimetres / 2 + settings.BarricadeSpotGapMillimetres);
            return geometry.DoorPointFrom(door, room, 0, outward);
        }

        /// <summary>
        /// Where they stand to do it: back from the gap by their own width plus
        /// the thing's, so setting it down never leaves it on top of them.
        /// </summary>
        private LogicalPosition StandingSpot(int door, int room, int item)
        {
            int clear = context.Scenario.World.OccupancyRadiusMillimetres + objects.RadiusOf(item) + 40;
            int outward = -(context.Scenario.World.OccupancyRadiusMillimetres / 2 + settings.BarricadeSpotGapMillimetres + clear);
            return geometry.DoorPointFrom(door, room, 0, outward);
        }

        /// <summary>
        /// Straight at whatever they are walking to, with no steering around
        /// things: the thing they mean to pick up is itself an object, and
        /// avoiding it would have them circling it for ever. This is how fetching
        /// works everywhere else too (see <see cref="ItemBehaviour"/>).
        /// </summary>
        private MotorIntent WalkTo(Agent agent, LogicalPosition target)
        {
            agent.Intent.Target = target;
            int heading = IntegerMath.HeadingBetween(agent.Body.Position, target, agent.Body.Heading);
            return PanicIntent.WalkTowards(agent, heading, panic);
        }

        private MotorIntent FaceTowards(Agent agent, LogicalPosition target, int speed)
        {
            int heading = IntegerMath.HeadingBetween(agent.Body.Position, target, agent.Body.Heading);
            return PanicIntent.MoveAt(agent, heading, speed, panic);
        }

        /// <summary>Done, or given up. Whatever they were holding stays in their arms for the usual rules to deal with.</summary>
        private void GiveUp(Agent agent)
        {
            agent.Barricade.DoorIndex = -1;
            if (IsBarricading(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                agent.Intent.NextPanicDecisionTick = context.Tick;
                agent.Body.BlockedTicks = 0;
            }
        }
    }
}
