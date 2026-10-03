using System.Collections.Generic;
using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// What the office's rooms are called, for the end card and the log
    /// (2026-10-03): "the meeting room", not "room 5005". Display only; the
    /// simulation knows rooms by number and never reads this. A room not on
    /// the list is "a room", so a test level or a new floor still reads.
    /// </summary>
    internal static class RoomNames
    {
        /// <summary>The name of a room by its number, or null when it has none.</summary>
        public static string Of(ulong roomId)
        {
            switch (roomId)
            {
                case 5001UL: return "the open-plan office";
                case 5002UL: return "the storage closet";
                case 5003UL: return "the corridor";
                case 5004UL: return "the cafeteria";
                case 5005UL: return "the meeting room";
                case 5006UL:
                case 5011UL:
                case 5012UL:
                case 5013UL:
                    return "the bathroom";
                case 5007UL: return "the maintenance room";
                case 5009UL: return "the corridor to the way out";
                case 5014UL: return "the stockroom";
                case 5015UL: return "the cubicles";
                default: return null;
            }
        }

        /// <summary>The room a point is in, by number, or 0 when it is in none.</summary>
        public static ulong RoomIdAt(ScenarioData scenario, LogicalPosition at)
        {
            if (scenario?.Rooms == null)
            {
                return 0UL;
            }

            foreach (RoomDefinition room in scenario.Rooms)
            {
                LogicalBounds floor = room.Bounds;
                if (at.X >= floor.MinX && at.X <= floor.MaxX && at.Z >= floor.MinZ && at.Z <= floor.MaxZ)
                {
                    return room.RoomId.Value;
                }
            }

            return 0UL;
        }

        /// <summary>The name of the room a point is in: "the cafeteria", or "a room".</summary>
        public static string At(ScenarioData scenario, LogicalPosition at) => Of(RoomIdAt(scenario, at)) ?? "a room";

        /// <summary>
        /// Up to <paramref name="most"/> lines for the end card, by the room
        /// people began in (2026-10-03): how many of them lived with the player
        /// playing, and -- once the same seed played with nobody at the
        /// controls has its answer -- how many would have lived left alone.
        /// The rooms where the player made the most difference come first,
        /// then those that lost the most; rooms of fewer than three are left
        /// out. Reads the scenario and the outcomes; decides nothing.
        /// </summary>
        public static List<string> ByRoom(ScenarioData scenario, RunSnapshot snapshot,
            IReadOnlyList<AgentTerminalOutcome> leftAlone, int most = 3)
        {
            var lines = new List<string>(most);
            if (scenario?.Agents == null || snapshot == null)
            {
                return lines;
            }

            var outcomeById = new Dictionary<ulong, AgentTerminalOutcome>();
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                outcomeById[snapshot.Agents[i].AgentId.Value] = snapshot.Agents[i].Outcome;
            }

            var rooms = new List<(string Name, int People, int Lived, int LivedAlone)>();
            var index = new Dictionary<string, int>();
            for (int i = 0; i < scenario.Agents.Length; i++)
            {
                AgentDefinition person = scenario.Agents[i];
                string name = At(scenario, person.InitialPosition);
                if (!index.TryGetValue(name, out int at))
                {
                    at = rooms.Count;
                    index[name] = at;
                    rooms.Add((name, 0, 0, 0));
                }

                (string Name, int People, int Lived, int LivedAlone) room = rooms[at];
                room.People++;
                if (outcomeById.TryGetValue(person.AgentId.Value, out AgentTerminalOutcome outcome) &&
                    outcome != AgentTerminalOutcome.Lost)
                {
                    room.Lived++;
                }

                if (leftAlone != null && i < leftAlone.Count && leftAlone[i] != AgentTerminalOutcome.Lost)
                {
                    room.LivedAlone++;
                }

                rooms[at] = room;
            }

            rooms.RemoveAll(room => room.People < 3);
            rooms.Sort((a, b) =>
            {
                int byDifference = System.Math.Abs(b.Lived - b.LivedAlone).CompareTo(System.Math.Abs(a.Lived - a.LivedAlone));
                return byDifference != 0 ? byDifference : (b.People - b.Lived).CompareTo(a.People - a.Lived);
            });

            for (int i = 0; i < rooms.Count && lines.Count < most; i++)
            {
                (string Name, int People, int Lived, int LivedAlone) room = rooms[i];
                lines.Add(leftAlone != null
                    ? $"From {room.Name} ({room.People}): {room.Lived} lived with you, {room.LivedAlone} left alone."
                    : $"From {room.Name} ({room.People}): {room.Lived} lived.");
            }

            return lines;
        }
    }
}
