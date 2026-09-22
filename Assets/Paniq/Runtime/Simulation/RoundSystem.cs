using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// When a round starts, when it is finished, and how it scored.
    /// <para>
    /// A round begins calm. Nothing is wrong until the player triggers the
    /// event, and until they do the round can never end. Once it has started,
    /// the round is finished when nobody is left to resolve: everybody is out,
    /// dead, or alive somewhere the hazard cannot reach. That last case is why
    /// "nobody is moving" is not the rule -- somebody safe in a far room would
    /// leave the round hanging for ever.
    /// </para>
    /// <para>
    /// This lives in the simulation, not the display, because it decides an
    /// outcome: it is what turns the last survivors into people who were
    /// saved. The display only reads the result off the snapshot.
    /// </para>
    /// </summary>
    internal sealed class RoundSystem
    {
        private readonly SimulationContext context;
        private readonly Agent[] agents;
        private readonly WorldGeometry geometry;
        private readonly FireSystem fire;
        private readonly RoundSettings settings;

        /// <summary>Wired up after construction: it is built after this system is.</summary>
        private FlammablesSystem flammables;

        /// <summary>Scratch for the room flood fill, kept so a tick allocates nothing.</summary>
        private readonly bool[] hazardCanReachRoom;
        private readonly List<int> roomsToVisit = new List<int>();

        /// <summary>The first tick on which everybody left was out of reach, or -1 while somebody is not.</summary>
        private int settledSinceTick = -1;

        public RoundSystem(SimulationContext context, Agent[] agents, WorldGeometry geometry, FireSystem fire)
        {
            this.context = context;
            this.agents = agents;
            this.geometry = geometry;
            this.fire = fire;
            settings = context.Scenario.Round;
            hazardCanReachRoom = new bool[geometry.RoomCount];
        }

        /// <summary>Wired up after construction, because the flammables are built later.</summary>
        public void Use(FlammablesSystem burningThings) => flammables = burningThings;

        /// <summary>Where the round has got to.</summary>
        public RoundPhase Phase { get; private set; } = RoundPhase.BeforeEvent;

        /// <summary>The event that set the disaster going, for anything that wants to name its cause.</summary>
        public ulong TriggerEventId { get; private set; }

        /// <summary>The tick the round finished on, or -1 while it is still going.</summary>
        public int EndedOnTick { get; private set; } = -1;

        /// <summary>
        /// The player's "trigger event". The first one starts the round and
        /// asks the hazard to begin; any later one is ignored, so a player
        /// leaning on the button cannot light two fires.
        /// </summary>
        public void TriggerEvent()
        {
            if (Phase != RoundPhase.BeforeEvent)
            {
                return;
            }

            Phase = RoundPhase.Running;
            CausalEvent triggered = context.Events.Append(
                context.Tick,
                default,
                FireReactionEventType.RoundEventTriggered,
                geometry.FireArea.Centre,
                context.Tick);
            TriggerEventId = triggered.EventId;
            fire.RequestStart();
        }

        /// <summary>
        /// Last phase of the tick: has the round finished? Called after
        /// everything else has settled, so it judges the tick as it ended
        /// rather than as it was half way through.
        /// </summary>
        public void Update()
        {
            if (Phase == RoundPhase.BeforeEvent)
            {
                if (!fire.StartRequested)
                {
                    return;
                }

                // The hazard started itself on its own tick count rather than
                // being triggered. Same round, no trigger to blame it on.
                Phase = RoundPhase.Running;
            }

            if (Phase != RoundPhase.Running)
            {
                return;
            }

            if (NobodyLeftUnresolved())
            {
                // Everyone is already out or dead. There is nothing left that
                // could change, so there is nothing to wait for.
                Finish();
                return;
            }

            if (!EverybodyLeftIsOutOfReach())
            {
                settledSinceTick = -1;
                return;
            }

            if (settledSinceTick < 0)
            {
                settledSinceTick = context.Tick;
            }

            if (context.Tick - settledSinceTick < settings.SettleTicks)
            {
                return;
            }

            Finish();
        }

        /// <summary>Whether every person has already reached a final outcome.</summary>
        private bool NobodyLeftUnresolved()
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether every person still unresolved is somewhere the fire cannot
        /// get to. Somebody on fire is never out of reach -- they are carrying
        /// it with them.
        /// </summary>
        private bool EverybodyLeftIsOutOfReach()
        {
            MarkRoomsTheHazardCanReach();
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                if (agent.Burning.IsBurning)
                {
                    return false;
                }

                int room = geometry.RoomAt(agent.Body.Position);
                if (room < 0 || hazardCanReachRoom[room])
                {
                    // Out of any room means in a doorway or mid-escape: still
                    // going somewhere, so the round is not over.
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Every room the fire is in, plus every room reachable from one of
        /// those through a door that is open, broken or blasted. A shut door
        /// stops it, which is the whole point of shutting one.
        /// </summary>
        private void MarkRoomsTheHazardCanReach()
        {
            roomsToVisit.Clear();
            for (int room = 0; room < hazardCanReachRoom.Length; room++)
            {
                bool burning = fire.IsBurningInRoom(room) ||
                               (flammables != null && flammables.AnythingBurningInRoom(room));
                hazardCanReachRoom[room] = burning;
                if (burning)
                {
                    roomsToVisit.Add(room);
                }
            }

            for (int i = 0; i < roomsToVisit.Count; i++)
            {
                int room = roomsToVisit[i];
                int[] doors = geometry.RoomDoors(room);
                for (int d = 0; d < doors.Length; d++)
                {
                    int door = doors[d];
                    if (!geometry.IsDoorOpen(door))
                    {
                        continue;
                    }

                    int beyond = geometry.RoomBeyond(door, room);
                    if (beyond >= 0 && !hazardCanReachRoom[beyond])
                    {
                        hazardCanReachRoom[beyond] = true;
                        roomsToVisit.Add(beyond);
                    }
                }
            }
        }

        /// <summary>
        /// Ends the round: everybody still alive inside lived through it, and
        /// is written down as having survived. In ascending person order, as
        /// every other loop that could affect an outcome is.
        /// </summary>
        private void Finish()
        {
            int saved = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (agent.IsParticipating)
                {
                    agent.Participation = AgentParticipation.NoLongerParticipating;
                    agent.Outcome = AgentTerminalOutcome.Survived;
                    agent.Body.Speed = 0;
                    context.Events.Append(
                        context.Tick,
                        agent.Id,
                        FireReactionEventType.AgentSurvived,
                        agent.Body.Position,
                        0,
                        0,
                        TriggerEventId);
                }

                saved += IsSaved(agent.Outcome) ? 1 : 0;
            }

            Phase = RoundPhase.Over;
            EndedOnTick = context.Tick;
            context.Events.Append(
                context.Tick,
                default,
                FireReactionEventType.RoundEnded,
                geometry.FireArea.Centre,
                saved,
                0,
                TriggerEventId);
        }

        /// <summary>Out of the building alive, or alive inside it at the end: both count.</summary>
        public static bool IsSaved(AgentTerminalOutcome outcome) =>
            outcome == AgentTerminalOutcome.Escaped || outcome == AgentTerminalOutcome.Survived;
    }
}
