using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Squashes a whole run into one number: every person's position,
    /// heading and speed on every tick, every event, the final boxes, and the
    /// random generator's final state. Two runs with the same fingerprint
    /// played out identically. A code change that is meant to be invisible
    /// (a restructure) must keep the recorded fingerprints; a deliberate
    /// behaviour change re-records them and bumps the compatibility version.
    /// </summary>
    internal static class ReplayFingerprint
    {
        public const int Ticks = 3000;

        /// <summary>Door clicks for the "doors opened" runs: north and south, each unlocked then opened.</summary>
        public static readonly (ulong DoorId, int Tick)[] OpeningClicks =
        {
            (2001UL, 300), (2001UL, 301), (2003UL, 900), (2003UL, 901)
        };

        /// <param name="kickBoxes">
        /// Start every box sliding, so box-on-box and box-on-person hits
        /// happen often enough to be covered; the default scenario only
        /// produces a few.
        /// </param>
        public static ulong Run(FireReactionScenarioData data, ulong seed, bool openDoors, bool kickBoxes = false)
        {
            var simulation = new FireReactionSimulation(data, seed);
            if (openDoors)
            {
                foreach ((ulong doorId, int tick) in OpeningClicks)
                {
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(doorId), tick);
                }
            }

            if (kickBoxes)
            {
                // Every box slides toward the middle of the room, so they meet
                // each other and whoever stands in between.
                LogicalPosition middle = data.Rooms[0].Bounds.Centre;
                for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                {
                    LogicalPosition from = simulation.GetPhysicsObject(i).Position;
                    LogicalPosition velocity = IntegerMath.Displacement(
                        IntegerMath.HeadingOf((long)middle.X - from.X, (long)middle.Z - from.Z, 0), 110);
                    simulation.LaunchObjectForTests(i, velocity.X, velocity.Z);
                }
            }

            var hash = new Fnv1a();
            for (int tick = 0; tick < Ticks; tick++)
            {
                simulation.Step();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                    hash.Add(agent.Position.X);
                    hash.Add(agent.Position.Z);
                    hash.Add(agent.HeadingDegrees);
                    hash.Add(agent.SpeedMillimetresPerTick);
                    hash.Add((int)agent.FearState);
                    hash.Add((int)agent.ActivityState);
                    hash.Add((int)agent.BodyState);
                    hash.Add((int)agent.Outcome);
                }
            }

            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                FireReactionPhysicsObjectSnapshot box = simulation.GetPhysicsObject(i);
                hash.Add(box.Position.X);
                hash.Add(box.Position.Z);
                hash.Add(box.HeadingDegrees);
                hash.Add(box.SpeedMillimetresPerTick);
            }

            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                hash.Add(record.EventId);
                hash.Add(record.Tick);
                hash.Add(record.SourceId.Value);
                hash.Add((int)record.EventType);
                hash.Add(record.Position.X);
                hash.Add(record.Position.Z);
                hash.Add(record.Strength);
                hash.Add(record.DurationTicks);
                hash.Add(record.CausalParentEventId);
                hash.Add(record.TargetId.Value);
            }

            hash.Add(simulation.Random.State);
            return hash.Value;
        }

        /// <summary>64-bit FNV-1a over little-endian bytes; fixed and platform-independent.</summary>
        private struct Fnv1a
        {
            private ulong value;
            private bool started;

            public ulong Value => started ? value : 14695981039346656037UL;

            public void Add(int number) => Add(unchecked((ulong)(uint)number));

            public void Add(ulong number)
            {
                if (!started)
                {
                    value = 14695981039346656037UL;
                    started = true;
                }

                for (int b = 0; b < 8; b++)
                {
                    value ^= (number >> (8 * b)) & 0xFFUL;
                    value = unchecked(value * 1099511628211UL);
                }
            }
        }
    }
}
