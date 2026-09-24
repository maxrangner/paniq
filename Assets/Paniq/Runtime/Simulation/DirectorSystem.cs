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

        private void Call(ScheduledCue cue)
        {
            switch (cue.Kind)
            {
                case CueKind.MeetingEnds:
                    cues.EndMeeting(RoomIndexOf(cue.RoomId), cue.SpreadTicks, 0UL);
                    break;
                case CueKind.HomeTime:
                    cues.CallHomeTime(cue.SpreadTicks, 0UL);
                    break;
                default:
                    // The scenario refuses a timetable that schedules anything
                    // else: a chat and a toilet trip are a person's own idea.
                    throw new System.InvalidOperationException($"The timetable cannot call {cue.Kind}.");
            }
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
