namespace Paniq.Simulation
{
    /// <summary>
    /// Something the crowd is afraid of. The fire is one; the game vision names
    /// four families of danger (spreading, rising, contagious, hunting), and
    /// this is what they all look like to a frightened person.
    /// <para>
    /// The crowd only ever asks a threat these questions: where is the nearest
    /// bit of you, are you in this room, are you closer than this, can I see
    /// you from here, does this route brush past you, am I touching you, and
    /// what happens to me if I am. Everything that merely needs to be
    /// <em>afraid</em> -- fear, perception, panic, doors, helping, barricading,
    /// the round clock -- asks through <see cref="Threats"/> and never names
    /// the fire. Everything that is genuinely about fire (dousing squares,
    /// things catching, doors scorching, the player's fire card) keeps talking
    /// to <see cref="FireSystem"/> directly.
    /// </para>
    /// <para>
    /// Before this seam existed, seventeen classes talked to the fire by name,
    /// and the roadmap's next stone -- a hunter that walks and chooses -- would
    /// have meant editing every one of them, and editing them again for the
    /// disaster after that.
    /// </para>
    /// </summary>
    internal interface IThreat
    {
        /// <summary>Whether it is in the world doing harm yet.</summary>
        bool Active { get; }

        /// <summary>Whether it has been asked to begin, whether or not it has yet.</summary>
        bool StartRequested { get; }

        /// <summary>The player's "trigger event": set the disaster going.</summary>
        void RequestStart();

        /// <summary>The event that started it, or 0 before it has: the root cause of everything it does.</summary>
        ulong RootEventId { get; }

        /// <summary>How much of it there is: burning squares, hunters. Written into fear events as a measure of how bad things are.</summary>
        int Count { get; }

        /// <summary>
        /// A number that changes whenever the threat changes, so the round can
        /// tell a building where something is still happening from one that
        /// has settled.
        /// </summary>
        long Signature { get; }

        /// <summary>How far off a calm person hears it and turns to look, in millimetres; 0 for a silent threat.</summary>
        int HeardWithinMillimetres { get; }

        /// <summary>Phase 2 of the tick: the threat advances on its own clock.</summary>
        void Advance();

        /// <summary>
        /// Squared distance to the nearest point of the threat, or long.MaxValue
        /// when there is none; also that point and the event that put it there.
        /// </summary>
        long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId);

        /// <summary>True when some part of the threat is strictly closer than this.</summary>
        bool AnyCloserThan(LogicalPosition position, int distance);

        /// <summary>
        /// True when some part of the threat that is in one of these two rooms
        /// is strictly closer than this. What a door asks about the rooms it
        /// joins, so that a fire behind the wall next to it does not count.
        /// </summary>
        bool AnyCloserThanInRooms(LogicalPosition position, int distance, int roomA, int roomB);

        /// <summary>True when the threat is in this room.</summary>
        bool IsInRoom(int room);

        /// <summary>True when somebody at this spot, facing this way, can see it within this range.</summary>
        bool IsVisibleFrom(LogicalPosition eye, int heading, int range);

        /// <summary>True when a straight walk from one point to the other passes within this clearance of it.</summary>
        bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance);

        /// <summary>The event to blame when a person standing here is touching the threat, or 0.</summary>
        ulong Touching(LogicalPosition position);

        /// <summary>The same for a person who moved from one spot to the other this tick.</summary>
        ulong TouchingAlong(LogicalPosition from, LogicalPosition to);

        /// <summary>What touching it does to somebody: the fire sets them alight.</summary>
        void Harm(Agent agent, ulong causeEventId, BodySystem body);
    }
}
