namespace Paniq.Simulation
{
    /// <summary>
    /// A frightened person's walk to somewhere in particular -- a bottle on
    /// a wall, a pull station, the flames -- through the doors on the way
    /// (2026-09-27). The route fields treat a shut doorway as floor, so a
    /// fighter used to walk into the door, push at it for a second, give
    /// up, pick the bottle again and walk into it again (the owner: "agents
    /// don't seem to go through closed doors with a fire extinguisher").
    /// Now the next door on the route is opened as a calm errand opens one:
    /// up to it, a moment with a hand on it, through. A door that will not
    /// open -- locked, held, wedged, heaped -- is remembered as one to avoid
    /// for a while, and the walk fails so whoever asked gives up.
    /// <para>
    /// The walk keeps its state on the person (<see cref="AgentDoorMemory.WalkDoor"/>
    /// and its neighbours); <see cref="Forget"/> clears it when the errand
    /// that used it ends, and clears the count of blocked ticks with it, so
    /// a give-up is not judged stuck the moment it is taken up again.
    /// </para>
    /// </summary>
    internal sealed class FrightenedWalk
    {
        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly Locomotion locomotion;
        private readonly int bodyRadius;
        private readonly ExitSettings exits;
        private readonly PanicSettings panic;

        public FrightenedWalk(SimulationContext context, WorldGeometry geometry, DoorSystem doors, Locomotion locomotion)
        {
            this.context = context;
            this.geometry = geometry;
            this.doors = doors;
            this.locomotion = locomotion;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            exits = context.Scenario.Exits;
            panic = context.Scenario.Panic;
        }

        /// <summary>
        /// One tick of the walk toward <paramref name="target"/> at
        /// <paramref name="speed"/>. False, with nothing to do, when there is
        /// no way: no route to the target's room, or a door on it that will
        /// not open. <paramref name="causeEventId"/> is what the walk is for,
        /// named by the doors it opens and tries.
        /// </summary>
        public bool TryStep(Agent agent, LogicalPosition target, int speed, ulong causeEventId, out MotorIntent intent)
        {
            AgentDoorMemory memory = agent.Doors;
            LogicalPosition position = agent.Body.Position;
            int room = geometry.RoomOf(agent);
            int targetRoom = geometry.RoomAtPoint(target);
            if (room < 0 || targetRoom < 0 || room == targetRoom)
            {
                // The same room, or stood in a doorway on the way: straight
                // along the fields, which go round the furniture.
                memory.WalkDoor = -1;
                memory.WalkUntilTick = 0;
                intent = Toward(agent, target, speed);
                return true;
            }

            int door = memory.WalkDoor;
            if (door < 0 || memory.WalkFromRoom != room)
            {
                // A new room: the next door on the way from here.
                if (!geometry.TryFindRoute(room, position, targetRoom, agent, out int first, out _, out _) || first < 0)
                {
                    intent = default;
                    return false;
                }

                door = first;
                memory.WalkDoor = door;
                memory.WalkFromRoom = room;
                memory.WalkUntilTick = 0;
            }

            if (geometry.IsDoorOpen(door))
            {
                memory.WalkUntilTick = 0;
                intent = Toward(agent, geometry.DoorPointFrom(door, room, 0, exits.OutsideTargetMillimetres), speed);
                return true;
            }

            LogicalPosition approach = geometry.DoorPointFrom(door, room, 0, -exits.ApproachInsetMillimetres);
            long arrival = exits.ArrivalDistanceMillimetres;
            if (LogicalPosition.DistanceSquared(position, approach) > arrival * arrival)
            {
                memory.WalkUntilTick = 0;
                intent = Toward(agent, approach, speed);
                return true;
            }

            int facing = IntegerMath.HeadingBetween(position, geometry.DoorCentre(door), agent.Body.Heading);
            if (memory.WalkUntilTick == 0)
            {
                if (!doors.CanBePushedOpen(door))
                {
                    // Locked, held, wedged or heaped: not this way, and not
                    // again for a while, so the next choice goes elsewhere.
                    context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentTriedDoor, position, 0, 0,
                        causeEventId, doors.IdOf(door));
                    memory.AvoidUntilTick[door] = checked(context.Tick + context.Random.NextIntInclusive(
                        exits.DoorCrowdedAvoidMinimumTicks, exits.DoorCrowdedAvoidMaximumTicks));
                    memory.WalkDoor = -1;
                    intent = default;
                    return false;
                }

                // A hand on it: a moment, as a calm person takes.
                memory.WalkUntilTick = checked(context.Tick + context.Jittered(exits.DoorOpenTicks));
                intent = PanicIntent.StandAndFace(agent, facing, panic);
                return true;
            }

            if (context.Tick < memory.WalkUntilTick)
            {
                intent = PanicIntent.StandAndFace(agent, facing, panic);
                return true;
            }

            memory.WalkUntilTick = 0;
            if (!doors.Open(door, causeEventId, geometry.SideOf(door, position)))
            {
                // Wedged or taken hold of while they had a hand on it.
                memory.WalkDoor = -1;
                intent = default;
                return false;
            }

            intent = Toward(agent, geometry.DoorPointFrom(door, room, 0, exits.OutsideTargetMillimetres), speed);
            return true;
        }

        /// <summary>The walk is over, one way or the other.</summary>
        public void Forget(Agent agent)
        {
            agent.Doors.WalkDoor = -1;
            agent.Doors.WalkFromRoom = -1;
            agent.Doors.WalkUntilTick = 0;
            agent.Body.BlockedTicks = 0;
        }

        /// <summary>Along the fields toward a spot, steering round people, walls and things.</summary>
        private MotorIntent Toward(Agent agent, LogicalPosition where, int speed)
        {
            agent.Intent.Target = where;
            int heading = geometry.Routes.HeadingToward(agent.Body.Position, where, bodyRadius, agent.Body.Heading);
            heading = locomotion.Steer(agent, heading, TraitEffects.PanicPeopleAvoidPercent(agent, context.Scenario),
                panic.WallAvoidPercent, panic.ObjectAvoidPercent, 0L, 0L);
            return new MotorIntent(heading, speed, agent.Personality.PanicTurnRate, panic.Acceleration);
        }
    }
}
