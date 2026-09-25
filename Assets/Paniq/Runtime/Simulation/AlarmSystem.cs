namespace Paniq.Simulation
{
    /// <summary>
    /// The fire alarms: pull stations on the walls, and the bells that make
    /// the noise. Somebody hits a station and every bell in the building rings
    /// at once, as a real one would, so each bell fills its own room with noise
    /// and nobody is left wondering because a door was shut between them and
    /// it. The bells ring again every few seconds, each on its own beat, so a
    /// door opened later lets the news through to whoever was behind it.
    /// <para>
    /// A bell is a thing on the wall (<see cref="PhysicsObjectKind.AlarmSounder"/>)
    /// and the flames can reach it: it goes off with a bang and falls silent,
    /// and the others carry on. A building with no bells authored rings from
    /// its pull stations, so a bare test floor still sounds.
    /// </para>
    /// <para>
    /// A bell tells people there is a fire without showing them one, and it
    /// frightens them exactly as the sight of flames would (the owner's rule,
    /// 2026-09-25: "pull it, and everybody panics"). Nobody walks out calmly
    /// any more.
    /// </para>
    /// </summary>
    internal sealed class AlarmSystem
    {
        private readonly SimulationContext context;
        private readonly SoundSystem sound;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly FlammablesSystem flammables;
        private readonly AlarmSettings settings;

        // The pull stations.
        private readonly SimulationId[] ids;
        private readonly LogicalPosition[] positions;
        private readonly int[] rooms;

        // The bells: the sounders' object indices, ascending, or -1 for a pull
        // station standing in for one on a floor with no sounders.
        private readonly int[] bellObjects;
        private readonly SimulationId[] bellIds;
        private readonly LogicalPosition[] bellPositions;
        private readonly int[] nextRingTick;
        private ulong pulledEventId;

        public AlarmSystem(SimulationContext context, SoundSystem sound, WorldGeometry geometry,
            PhysicsObjectSystem objects, FlammablesSystem flammables)
        {
            this.context = context;
            this.sound = sound;
            this.geometry = geometry;
            this.objects = objects;
            this.flammables = flammables;
            settings = context.Scenario.Alarm;

            AlarmDefinition[] definitions = context.Scenario.Alarms;
            ids = new SimulationId[definitions.Length];
            positions = new LogicalPosition[definitions.Length];
            rooms = new int[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                ids[i] = definitions[i].AlarmId;
                positions[i] = definitions[i].Position;
                rooms[i] = geometry.RoomAtPoint(definitions[i].Position);
            }

            int sounders = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                sounders += objects.KindOf(i) == PhysicsObjectKind.AlarmSounder ? 1 : 0;
            }

            if (sounders > 0)
            {
                bellObjects = new int[sounders];
                bellIds = new SimulationId[sounders];
                bellPositions = new LogicalPosition[sounders];
                int b = 0;
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects.KindOf(i) == PhysicsObjectKind.AlarmSounder)
                    {
                        bellObjects[b] = i;
                        bellIds[b] = objects.IdOf(i);
                        bellPositions[b] = objects.PositionOf(i);
                        b++;
                    }
                }
            }
            else
            {
                // No bells on this floor: the pull stations ring themselves.
                bellObjects = new int[ids.Length];
                bellIds = ids;
                bellPositions = positions;
                for (int i = 0; i < bellObjects.Length; i++)
                {
                    bellObjects[i] = -1;
                }
            }

            nextRingTick = new int[bellObjects.Length];
        }

        /// <summary>How many pull stations there are.</summary>
        public int Count => ids.Length;

        public SimulationId IdOf(int alarm) => ids[alarm];

        public LogicalPosition PositionOf(int alarm) => positions[alarm];

        public int RoomOf(int alarm) => rooms[alarm];

        /// <summary>How many bells there are to ring (the pull stations, on a floor with no sounders).</summary>
        public int BellCount => bellObjects.Length;

        /// <summary>Whether this bell can still ring: a sounder the flames have reached has gone off and is silent.</summary>
        public bool IsBellLive(int bell)
        {
            int index = bellObjects[bell];
            return index < 0 || flammables.ObjectState(index) == ObjectBurnState.Intact;
        }

        /// <summary>Whether the alarms are ringing. They never stop once they start, though a bell the fire reaches does.</summary>
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
        /// bell in the building rings, in bell order, each heard by the people
        /// in ascending ID order as any other noise is.
        /// </summary>
        public void Pull(int alarm, Agent puller, ulong causeEventId)
        {
            if (Ringing || !settings.Enabled)
            {
                return;
            }

            ulong pulled = context.Events.Append(context.Tick, puller.Id, CausalEventType.AlarmPulled,
                positions[alarm], 0, 0, causeEventId, ids[alarm]).EventId;
            Ring(pulled);
        }

        /// <summary>
        /// The player pulls an alarm: a root event of the player's own, and
        /// then every bell rings exactly as when a person pulls it. False when
        /// the bells are already ringing or the alarms are off, in which case
        /// nothing is written and nothing should be paid.
        /// </summary>
        public bool PullByPlayer(int alarm)
        {
            if (Ringing || !settings.Enabled)
            {
                return false;
            }

            ulong pulled = context.Events.Append(context.Tick, default, CausalEventType.PowerPulledAlarm,
                positions[alarm], 0, 0, 0UL, ids[alarm]).EventId;
            Ring(pulled);
            return true;
        }

        /// <summary>Which alarm has this ID, or -1.</summary>
        public int IndexOf(SimulationId id)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Every live bell in the building rings, in bell order, each heard by
        /// the people in ascending ID order as any other noise is. Each bell
        /// then draws its own beat for ringing again, so no two ring again on
        /// the same tick by design.
        /// </summary>
        private void Ring(ulong pulled)
        {
            Ringing = true;
            pulledEventId = pulled;
            for (int bell = 0; bell < bellObjects.Length; bell++)
            {
                if (!IsBellLive(bell))
                {
                    continue;
                }

                RingOne(bell);
                nextRingTick[bell] = checked(context.Tick + settings.RepeatTicks +
                                             context.Random.NextIntInclusive(0, settings.RepeatTicks - 1));
            }
        }

        /// <summary>
        /// Phase 1's tail: the bells that are due ring again. A person who was
        /// out of earshot -- behind two shut doors, say -- hears the next one
        /// once a door between them opens. Only the calm are ever reached, so
        /// this costs next to nothing once the building is up.
        /// </summary>
        public void Update()
        {
            if (!Ringing)
            {
                return;
            }

            for (int bell = 0; bell < bellObjects.Length; bell++)
            {
                if (context.Tick < nextRingTick[bell] || !IsBellLive(bell))
                {
                    continue;
                }

                RingOne(bell);
                nextRingTick[bell] = checked(context.Tick + context.Jittered(settings.RepeatTicks));
            }
        }

        private void RingOne(int bell)
        {
            ulong rang = context.Events.Append(context.Tick, bellIds[bell], CausalEventType.AlarmRang,
                bellPositions[bell], settings.BellHearingRadiusMillimetres, 0, pulledEventId).EventId;
            sound.Bell(bellIds[bell], bellPositions[bell], rang);
        }
    }
}
