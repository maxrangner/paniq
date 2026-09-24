namespace Paniq.Simulation
{
    /// <summary>
    /// The Director: the background system that decides what the building's
    /// day holds. Today it keeps the level's <em>timetable</em>
    /// (<see cref="ScenarioData.Timetable"/>) -- the meeting ends at the
    /// minute mark, say -- and calls each cue on it once, on its tick. This
    /// is the seam the reactive Director the game vision describes will grow
    /// from: something that watches how the run is going and adds or eases
    /// pressure. When it does, it lives here, reads simulation state and the
    /// seed only, and never anything the player's screen knows.
    /// <para>
    /// Phase 1½ of the tick, after the player's commands and before the
    /// hazards advance: "the building's day". A cue called here reaches
    /// people at their own reaction tick inside phase 4, so nothing moves on
    /// the tick a cue is called.
    /// </para>
    /// </summary>
    internal sealed class DirectorSystem
    {
        private readonly SimulationContext context;
        private readonly CueSystem cues;
        private readonly WorldGeometry geometry;
        private readonly ScheduledCue[] timetable;
        private readonly bool[] called;

        public DirectorSystem(SimulationContext context, CueSystem cues, WorldGeometry geometry)
        {
            this.context = context;
            this.cues = cues;
            this.geometry = geometry;
            timetable = context.Scenario.Timetable ?? System.Array.Empty<ScheduledCue>();
            called = new bool[timetable.Length];
        }

        /// <summary>Every cue whose tick has come and that has not been called yet, in timetable order.</summary>
        public void Advance()
        {
            for (int i = 0; i < timetable.Length; i++)
            {
                if (called[i] || context.Tick < timetable[i].AtTick)
                {
                    continue;
                }

                called[i] = true;
                Call(timetable[i]);
            }
        }

        /// <summary>
        /// A timetable entry is a cue that reaches a room or the whole
        /// building; the scenario refuses any other kind, so which it is
        /// comes from the cue's own definition rather than a list kept here.
        /// </summary>
        private void Call(ScheduledCue cue)
        {
            int room = cues.DefinitionOf(cue.Kind).Audience == CueAudience.Room ? RoomIndexOf(cue.RoomId) : -1;
            cues.Call(cue.Kind, null, room, null, cue.SpreadTicks, 0UL);
        }

        private int RoomIndexOf(SimulationId roomId)
        {
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (geometry.RoomId(r) == roomId)
                {
                    return r;
                }
            }

            throw new System.InvalidOperationException($"The timetable names room {roomId}, which the building does not have.");
        }
    }
}
