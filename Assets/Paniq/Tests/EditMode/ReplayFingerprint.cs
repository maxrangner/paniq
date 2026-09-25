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

        /// <summary>
        /// Door clicks for the "doors opened" runs: the building's one way out,
        /// unlocked and then opened.
        /// </summary>
        public static readonly (ulong DoorId, int Tick)[] OpeningClicks =
        {
            (2008UL, 300), (2008UL, 301)
        };

        /// <summary>
        /// Cards for the "cards played" runs: all five trait cards thrown into
        /// the office where the crowd is, a fire of the player's own, a spare
        /// extinguisher put down, and a wall blown open. Enough to cover every
        /// command type in a replay.
        /// </summary>
        public static readonly (PlayerCommandType Card, SimulationId Target, LogicalPosition Point, int Tick)[] Cards =
        {
            (PlayerCommandType.PlayBeefcake, default, new LogicalPosition(0, 0), 200),
            (PlayerCommandType.SpawnFire, default, new LogicalPosition(3000, 3000), 400),
            (PlayerCommandType.PlayCourage, default, new LogicalPosition(0, 0), 450),
            (PlayerCommandType.SpawnExtinguisher, default, new LogicalPosition(-4000, 4000), 600),
            (PlayerCommandType.PlayTerror, default, new LogicalPosition(-2000, 0), 650),
            (PlayerCommandType.BlastWall, default, new LogicalPosition(0, -5900), 800),
            (PlayerCommandType.PlayBastard, default, new LogicalPosition(2000, 0), 850),
            (PlayerCommandType.PlayColdHeart, default, new LogicalPosition(0, 2000), 900),
            (PlayerCommandType.StickTogether, default, new LogicalPosition(0, 0), 950),
            (PlayerCommandType.ToggleLock, new SimulationId(2002UL), default, 1000)
        };

        /// <summary>
        /// The tick the "cards played" run triggers the event on. The same
        /// tick the fire would have started itself on, so that run burns
        /// exactly as the others do while still covering the trigger command.
        /// </summary>
        public const int TriggerTick = 250;

        /// <param name="kickBoxes">
        /// Start every box sliding, so box-on-box and box-on-person hits
        /// happen often enough to be covered; the default scenario only
        /// produces a few.
        /// </param>
        public static ulong Of(ScenarioData data, ulong seed, bool openDoors, bool kickBoxes = false,
            bool playCards = false)
        {
            if (playCards || openDoors)
            {
                // A copy, so setting this does not leak into the caller's data.
                data = data.Clone();

                // A round now opens with an empty purse and an empty hand, so
                // without this every door click and every card in the runs
                // below would be refused for want of funds and the two would
                // fingerprint identically to the run that does nothing. What is
                // being guarded here is the simulation's response to a player
                // acting, not whether they could afford to.
                data.Influence.Starting = 100000;
                data.Influence.Maximum = 100000;
                data.Influence.StartingHand = TheBuilding.EveryCard();
            }

            if (playCards)
            {
                data.Round.HazardWaitsForTrigger = true;
            }

            var simulation = new Run(data, seed);
            if (playCards)
            {
                // The hazard is the player's to set going in this run, as it
                // is in the game.
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), TriggerTick);

                // Queued before the run starts, in card order, so a replay plays
                // exactly the same hand at exactly the same ticks.
                foreach ((PlayerCommandType card, SimulationId target, LogicalPosition point, int tick) in Cards)
                {
                    if (target.Value != 0UL)
                    {
                        simulation.QueueCommand(card, target, tick);
                    }
                    else
                    {
                        simulation.QueueCommand(card, point, tick);
                    }
                }
            }

            if (openDoors)
            {
                foreach ((ulong doorId, int tick) in OpeningClicks)
                {
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(doorId), tick);
                }
            }

            if (kickBoxes)
            {
                // Every loose thing in the office slides toward the middle of
                // the room, so they meet each other and whoever stands in
                // between. Only the office's: the stockroom's thirty-odd boxes
                // (2026-09-25) hurled through its walls into their own
                // collapsing stacks made a contact storm the physics engine
                // did not replay the same twice.
                LogicalBounds office = data.Rooms[0].Bounds;
                LogicalPosition middle = office.Centre;
                for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                {
                    LogicalPosition from = simulation.GetPhysicsObject(i).Position;
                    if (!office.ContainsCircle(from, 0))
                    {
                        continue;
                    }

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
                    AgentSnapshot agent = simulation.GetAgent(i);
                    hash.Add(agent.Position.X);
                    hash.Add(agent.Position.Z);
                    hash.Add(agent.HeadingDegrees);
                    hash.Add(agent.SpeedMillimetresPerTick);
                    hash.Add((int)agent.FearState);
                    hash.Add((int)agent.ActivityState);
                    hash.Add((int)agent.BodyState);
                    hash.Add((int)agent.Outcome);
                }

                // Every loose thing, in three dimensions, twice a second: the
                // physics engine's work is part of the run, so a replay that
                // drifts there has to show here too.
                if (tick % 25 == 0)
                {
                    for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                    {
                        AddObject(ref hash, simulation.GetPhysicsObject(i));
                    }
                }
            }

            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                AddObject(ref hash, simulation.GetPhysicsObject(i));
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
            simulation.Dispose();
            return hash.Value;
        }

        private static void AddObject(ref Fnv1a hash, PhysicsObjectSnapshot thing)
        {
            hash.Add(thing.Position.X);
            hash.Add(thing.Position.Z);
            hash.Add(thing.HeadingDegrees);
            hash.Add(thing.SpeedMillimetresPerTick);
            hash.Add(thing.Pose.HeightMillimetres);
            hash.Add(thing.Pose.RotationX);
            hash.Add(thing.Pose.RotationY);
            hash.Add(thing.Pose.RotationZ);
            hash.Add(thing.Pose.RotationW);
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
