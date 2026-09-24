namespace Paniq.Simulation
{
    /// <summary>
    /// Every system in one run, by name, so that a system built before another
    /// can be handed it afterwards in one place.
    /// <para>
    /// Systems are built in a fixed order because the order decides the run's
    /// random draws at start-up and the order bodies enter the physics world.
    /// That order cannot give every system everything it needs at construction:
    /// the body system is built before people's bodies and needs them, the
    /// doors are built before the loose things and need those, and so on. This
    /// used to be eleven differently named setters (<c>UseCrowd</c>,
    /// <c>UsePeople</c>, <c>UseDoors</c>, ...) called one by one from the run's
    /// constructor, each a special case to remember. Now a system that needs
    /// something built after it implements <see cref="IBindable"/> and is
    /// handed this once everything exists.
    /// </para>
    /// <para>
    /// Binding only stores references. It draws no random numbers and moves
    /// nothing, so the order systems are bound in cannot change a run.
    /// </para>
    /// </summary>
    internal sealed class Systems
    {
        public SimulationContext Context;
        public WorldGeometry Geometry;
        public Crowd Crowd;
        public PhysicsWorld Physics;
        public FireSystem Fire;
        public Threats Threats;
        public PowerSystem Power;
        public DoorSystem Doors;
        public PlayerCommandSystem PlayerCommands;
        public InfluenceSystem Influence;
        public DeckSystem Deck;
        public RoundSystem Round;
        public SoundSystem Sound;
        public FearSystem Fear;
        public PerceptionSystem Perception;
        public BodySystem Body;
        public PhysicsObjectSystem Objects;
        public PeopleBodies People;
        public CollisionSystem Collisions;
        public Locomotion Locomotion;
        public FlammablesSystem Flammables;
        public ItemBehaviour Items;
        public ChairBehaviour Chairs;
        public CalmBehaviour Calm;
        public ExitSignBehaviour ExitSigns;
        public WayfindingSystem Wayfinding;
        public DoorBehaviour DoorBehaviour;
        public HelpBehaviour Help;
        public PanicBehaviour Panic;
        public BurningBehaviour Burning;
        public ExtinguisherBehaviour Extinguishers;
        public LeaderBehaviour Leaders;
        public AlarmSystem Alarms;
        public AlarmBehaviour AlarmBehaviour;
        public BarricadeBehaviour Barricades;
        public CueSystem Cues;
        public DirectorSystem Director;
        public ErrandBehaviour Errands;

        /// <summary>Hands every system that asked for it the finished set, in a fixed order.</summary>
        public void BindAll()
        {
            IBindable[] bindable =
            {
                Doors, Body, Objects, People, DoorBehaviour, Help, Panic, Round, PlayerCommands
            };

            for (int i = 0; i < bindable.Length; i++)
            {
                bindable[i].Bind(this);
            }
        }
    }

    /// <summary>A system that needs something built after it. See <see cref="Systems"/>.</summary>
    internal interface IBindable
    {
        void Bind(Systems systems);
    }
}
