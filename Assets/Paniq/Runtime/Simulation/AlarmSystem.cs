namespace Paniq.Simulation
{
    /// <summary>
    /// The fire alarms on the walls, one to a room. Somebody hits one and every
    /// alarm in the building rings at once, as a real one would, so each bell
    /// fills its own room with noise and nobody is left wondering because a door
    /// was shut between them and it.
    /// <para>
    /// The bell tells people there is a fire without showing them one, which is
    /// why it does not simply panic everybody: see
    /// <see cref="FearSystem.BreakComposure"/>.
    /// </para>
    /// </summary>
    internal sealed class AlarmSystem
    {
        private readonly SimulationContext context;
        private readonly SoundSystem sound;
        private readonly WorldGeometry geometry;
        private readonly AlarmSettings settings;
        private readonly SimulationId[] ids;
        private readonly LogicalPosition[] positions;
        private readonly int[] rooms;

        public AlarmSystem(SimulationContext context, SoundSystem sound, WorldGeometry geometry)
        {
            this.context = context;
            this.sound = sound;
            this.geometry = geometry;
            settings = context.Scenario.Alarm;

            FireReactionAlarmDefinition[] definitions = context.Scenario.Alarms;
            ids = new SimulationId[definitions.Length];
            positions = new LogicalPosition[definitions.Length];
            rooms = new int[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                ids[i] = definitions[i].AlarmId;
                positions[i] = definitions[i].Position;
                rooms[i] = geometry.RoomAtPoint(definitions[i].Position);
            }
        }

        public int Count => ids.Length;

        public SimulationId IdOf(int alarm) => ids[alarm];

        public LogicalPosition PositionOf(int alarm) => positions[alarm];

        public int RoomOf(int alarm) => rooms[alarm];

        /// <summary>Whether the alarms are ringing. They never stop once they start.</summary>
        public bool Ringing { get; private set; }

        /// <summary>Turned off in the scenario, so nobody bothers going for one.</summary>
        public bool Enabled => settings.Enabled;

        /// <summary>
        /// The nearest alarm nobody has hit yet that this person could walk to,
        /// or -1.
        ///
        /// It is still a short walk rather than a journey -- hitting the bell
        /// is close to a reflex -- but the limit is now how far they would have
        /// to walk rather than which room they happen to be standing in. A bell
        /// three metres away through a doorway was previously invisible to them
        /// while one right across a large room was not.
        /// </summary>
        public int NearestUnpulledWithin(LogicalPosition from, int room, FlowField walking, Navigation routes)
        {
            if (!settings.Enabled || Ringing || room < 0)
            {
                return -1;
            }

            int best = -1;
            long bestDistance = settings.ReachMillimetres;
            for (int i = 0; i < ids.Length; i++)
            {
                if (walking == null)
                {
                    // No routing to spare this tick: their own room, which is
                    // all anybody could manage before.
                    if (rooms[i] != room)
                    {
                        continue;
                    }
                }

                long distance = walking == null
                    ? IntegerMath.Distance(from, positions[i])
                    : routes.DistanceIn(walking, positions[i]);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Somebody hits an alarm: it is logged against them, and then every
        /// bell in the building rings, in alarm order, each heard by the people
        /// in ascending ID order as any other noise is.
        /// </summary>
        public void Pull(int alarm, Agent puller, ulong causeEventId)
        {
            if (Ringing || !settings.Enabled)
            {
                return;
            }

            Ringing = true;
            ulong pulled = context.Events.Append(context.Tick, puller.Id, FireReactionEventType.AlarmPulled,
                positions[alarm], 0, 0, causeEventId, ids[alarm]).EventId;

            for (int i = 0; i < ids.Length; i++)
            {
                ulong rang = context.Events.Append(context.Tick, ids[i], FireReactionEventType.AlarmRang,
                    positions[i], settings.BellHearingRadiusMillimetres, 0, pulled).EventId;
                sound.Bell(ids[i], positions[i], rang);
            }
        }
    }
}
