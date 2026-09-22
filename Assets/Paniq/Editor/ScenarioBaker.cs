using System;
using System.Collections.Generic;
using System.Linq;
using Paniq.Authoring;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEditor;
using UnityEngine;

namespace Paniq.EditorTools
{
    /// <summary>
    /// Turns a building laid out in the scene into the numbers a run reads.
    ///
    /// Until now a floor plan was four typed coordinates per room in a C# file,
    /// with no picture of it until you pressed play. That is why there were
    /// four rooms. Here you drag cubes about, and this reads them.
    ///
    /// Everything it writes is whole millimetres, and rooms are rounded to the
    /// size of a navigation square. That matters more than it sounds: a run
    /// repeats exactly because every number in it is an integer, so the
    /// rounding has to happen once, here, and never again while the game is
    /// running.
    ///
    /// It refuses to write a floor plan the simulation would reject, and says
    /// what is wrong in terms of the thing in the scene rather than the number
    /// behind it.
    /// </summary>
    public static class ScenarioBaker
    {
        private const string ScenarioAssetPath = "Assets/Paniq/Content/FireReactionScenario.asset";

        [MenuItem("Paniq/Bake Scenario From Scene")]
        public static void Bake()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<FireReactionScenario>(ScenarioAssetPath);
            if (scenario == null)
            {
                EditorUtility.DisplayDialog("Paniq", "There is no scenario at " + ScenarioAssetPath + ".", "Right");
                return;
            }

            // Start from the current settings, so the tuning numbers survive and
            // only the shape of the building is taken from the scene.
            FireReactionScenarioData data = scenario.ToRuntimeData();
            var problems = new List<string>();

            PaniqRoom[] rooms = Find<PaniqRoom>();
            if (rooms.Length == 0)
            {
                EditorUtility.DisplayDialog("Paniq",
                    "There are no rooms in this scene. Add a cube, scale it to the shape of a room, " +
                    "and put a Paniq > Room component on it.", "Right");
                return;
            }

            data.Rooms = BakeRooms(rooms, problems);
            data.Doors = BakeDoors(Find<PaniqDoor>(), rooms, problems);
            data.Tables = BakeTables(Find<PaniqTable>(), problems);
            data.PhysicsObjects = BakeProps(Find<PaniqProp>(), problems);
            data.Agents = BakePeople(Find<PaniqPerson>(), problems);
            data.Alarms = BakeAlarms(Find<PaniqAlarm>(), problems);
            BakeFire(Find<PaniqFireStart>(), data, rooms, problems);

            if (problems.Count == 0)
            {
                try
                {
                    // The same check a run makes when it loads, so a floor plan
                    // that would fail at play time fails here instead, with the
                    // scene still in front of you.
                    data.Clone().Validate();
                    new FireReactionSimulation(data);
                }
                catch (Exception refused)
                {
                    problems.Add(refused.Message);
                }
            }

            if (problems.Count > 0)
            {
                Debug.LogError("Paniq: the scene was not baked.\n  " + string.Join("\n  ", problems));
                EditorUtility.DisplayDialog("Paniq",
                    "The building has " + problems.Count + " problem" + (problems.Count == 1 ? "" : "s") +
                    ". Nothing was written; the details are in the Console.", "Right");
                return;
            }

            // Marked as having come from a scene, once however many times it
            // is baked, rather than growing a tail of "+scene+scene+scene".
            const string FromAScene = "+scene";
            if (!data.ContentRevision.EndsWith(FromAScene, StringComparison.Ordinal))
            {
                data.ContentRevision += FromAScene;
            }
            scenario.OverwriteWith(data);
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();

            Debug.Log("Paniq: baked " + data.Rooms.Length + " rooms, " + data.Doors.Length + " doors, " +
                      data.Tables.Length + " tables, " + data.PhysicsObjects.Length + " props, " +
                      data.Agents.Length + " people and " + data.Alarms.Length + " alarms into " + ScenarioAssetPath + ".");
        }

        private static T[] Find<T>() where T : MonoBehaviour
        {
            // Sorted by name and then by where it stands, so the same scene
            // always bakes to the same order and therefore to the same run.
            // Name alone is not enough: Unity hands these back in no
            // particular order, and two cubes both called "Cube" would then
            // swap places between bakes and change every room's number.
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None)
                .OrderBy(found => found.name, StringComparer.Ordinal)
                .ThenBy(found => PaniqAuthoring.Millimetres(found.transform.position).x)
                .ThenBy(found => PaniqAuthoring.Millimetres(found.transform.position).y)
                .ThenBy(found => found.GetInstanceID())
                .ToArray();
        }

        // ---------------------------------------------------------------- rooms

        private static FireReactionRoomDefinition[] BakeRooms(PaniqRoom[] rooms, List<string> problems)
        {
            var baked = new FireReactionRoomDefinition[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                RectInt floor = rooms[i].Floor;
                baked[i] = new FireReactionRoomDefinition(
                    new SimulationId((ulong)rooms[i].RoomId),
                    new LogicalBounds(floor.xMin, floor.xMax, floor.yMin, floor.yMax));

                for (int other = 0; other < i; other++)
                {
                    RectInt already = rooms[other].Floor;
                    if (floor.xMin < already.xMax && already.xMin < floor.xMax &&
                        floor.yMin < already.yMax && already.yMin < floor.yMax)
                    {
                        problems.Add("Rooms " + rooms[i].name + " and " + rooms[other].name + " overlap.");
                    }
                }
            }

            return baked;
        }

        // ---------------------------------------------------------------- doors

        /// <summary>
        /// Each doorway is matched to the room wall it is nearest, which is why
        /// you only have to slide it along a wall rather than say which wall it
        /// is in.
        /// </summary>
        private static FireReactionDoorDefinition[] BakeDoors(PaniqDoor[] doors, PaniqRoom[] rooms, List<string> problems)
        {
            var baked = new List<FireReactionDoorDefinition>();
            foreach (PaniqDoor door in doors)
            {
                Vector2Int at = PaniqAuthoring.Millimetres(door.transform.position);
                PaniqRoom inWallOf = null;
                WallSide side = WallSide.North;
                int nearest = int.MaxValue;

                foreach (PaniqRoom room in rooms)
                {
                    RectInt floor = room.Floor;
                    Consider(floor.yMax - at.y, WallSide.North, at.x, floor.xMin, floor.xMax, room, ref nearest, ref inWallOf, ref side);
                    Consider(at.y - floor.yMin, WallSide.South, at.x, floor.xMin, floor.xMax, room, ref nearest, ref inWallOf, ref side);
                    Consider(floor.xMax - at.x, WallSide.East, at.y, floor.yMin, floor.yMax, room, ref nearest, ref inWallOf, ref side);
                    Consider(at.x - floor.xMin, WallSide.West, at.y, floor.yMin, floor.yMax, room, ref nearest, ref inWallOf, ref side);
                }

                if (inWallOf == null)
                {
                    problems.Add("Door " + door.name + " is not on the wall of any room. Move it onto one.");
                    continue;
                }

                bool alongX = side == WallSide.North || side == WallSide.South;
                int centre = PaniqAuthoring.Snapped(alongX ? at.x : at.y);
                baked.Add(new FireReactionDoorDefinition(
                    new SimulationId((ulong)door.DoorId),
                    new SimulationId((ulong)inWallOf.RoomId),
                    side,
                    centre,
                    door.WidthMillimetres,
                    door.StartsLocked));
            }

            return baked.ToArray();
        }

        /// <summary>
        /// How far from a wall a door may sit and still count as being in it.
        ///
        /// Without a limit, a door dragged just outside a room was still
        /// matched to that room's nearest wall -- and since the far wall of a
        /// small room can be nearer than the near wall of a big one, a way out
        /// dropped slightly outside the building could silently become a second
        /// hole in an inside wall, with the bake reporting success.
        /// </summary>
        private const int OnTheWallMillimetres = 600;

        /// <summary>Keeps the nearest wall the door actually sits in, ignoring walls it is nowhere near.</summary>
        private static void Consider(int distance, WallSide side, int along, int from, int to, PaniqRoom room,
            ref int nearest, ref PaniqRoom inWallOf, ref WallSide chosen)
        {
            if (distance < 0 || distance > OnTheWallMillimetres || distance >= nearest ||
                along < from || along > to)
            {
                return;
            }

            nearest = distance;
            inWallOf = room;
            chosen = side;
        }

        // ---------------------------------------------------------------- the rest

        private static FireReactionTableDefinition[] BakeTables(PaniqTable[] tables, List<string> problems)
        {
            var baked = new FireReactionTableDefinition[tables.Length];
            for (int i = 0; i < tables.Length; i++)
            {
                Vector2Int centre = PaniqAuthoring.Millimetres(tables[i].transform.position);
                Vector2Int size = PaniqAuthoring.SizeMillimetres(tables[i].transform);
                baked[i] = new FireReactionTableDefinition(
                    new SimulationId((ulong)tables[i].TableId),
                    new LogicalPosition(centre.x, centre.y),
                    size.x,
                    size.y);
            }

            return baked;
        }

        private static FireReactionPhysicsObjectDefinition[] BakeProps(PaniqProp[] props, List<string> problems)
        {
            var baked = new FireReactionPhysicsObjectDefinition[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                PaniqProp prop = props[i];
                Vector2Int at = PaniqAuthoring.Millimetres(prop.transform.position);
                baked[i] = new FireReactionPhysicsObjectDefinition(
                    new SimulationId((ulong)prop.ObjectId),
                    prop.Kind,
                    new LogicalPosition(at.x, at.y),
                    prop.SizeMillimetres,
                    prop.MassGrams,
                    initialFacingDegrees: Mathf.RoundToInt(prop.transform.eulerAngles.y),
                    startsResting: prop.StartsResting);
            }

            return baked;
        }

        private static FireReactionAgentDefinition[] BakePeople(PaniqPerson[] people, List<string> problems)
        {
            var baked = new FireReactionAgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                PaniqPerson person = people[i];
                Vector2Int at = PaniqAuthoring.Millimetres(person.transform.position);
                var where = new LogicalPosition(at.x, at.y);
                CardinalDirection facing = Facing(person.transform.eulerAngles.y);

                baked[i] = person.AuthorTheirPersonality
                    ? new FireReactionAgentDefinition(
                        new SimulationId((ulong)person.AgentId), where, facing,
                        new AgentTraitValues(person.Strength, person.Speed, person.Bravery,
                            person.Compassion, person.Evil, person.Nervousness, person.Leadership))
                    : new FireReactionAgentDefinition(new SimulationId((ulong)person.AgentId), where, facing);
            }

            return baked;
        }

        private static FireReactionAlarmDefinition[] BakeAlarms(PaniqAlarm[] alarms, List<string> problems)
        {
            var baked = new FireReactionAlarmDefinition[alarms.Length];
            for (int i = 0; i < alarms.Length; i++)
            {
                Vector2Int at = PaniqAuthoring.Millimetres(alarms[i].transform.position);
                baked[i] = new FireReactionAlarmDefinition(
                    new SimulationId((ulong)alarms[i].AlarmId),
                    new LogicalPosition(at.x, at.y));
            }

            return baked;
        }

        private static void BakeFire(PaniqFireStart[] starts, FireReactionScenarioData data, PaniqRoom[] rooms,
            List<string> problems)
        {
            if (starts.Length == 0)
            {
                problems.Add("There is nowhere for the fire to start. Add a Paniq > Fire Start to the room it begins in.");
                return;
            }

            if (starts.Length > 1)
            {
                problems.Add("There are " + starts.Length + " places for the fire to start; there can only be one.");
                return;
            }

            PaniqFireStart start = starts[0];
            Vector2Int at = PaniqAuthoring.Millimetres(start.transform.position);
            Vector2Int size = PaniqAuthoring.SizeMillimetres(start.transform);
            data.Fire.SpawnBounds = new LogicalBounds(
                at.x - size.x / 2, at.x + size.x / 2, at.y - size.y / 2, at.y + size.y / 2);
            data.Fire.ActivationTick = Mathf.Max(1,
                Mathf.RoundToInt(start.StartsAfterSeconds * FireReactionSimulation.TicksPerSecond));

            // The run insists the fire starts in the first room, so the room it
            // is standing in has to be the one written first.
            int inside = Array.FindIndex(rooms, room => room.Floor.xMin <= at.x && at.x <= room.Floor.xMax &&
                                                        room.Floor.yMin <= at.y && at.y <= room.Floor.yMax);
            if (inside < 0)
            {
                problems.Add("The fire starts outside every room. Move " + start.name + " into one.");
                return;
            }

            if (inside != 0)
            {
                FireReactionRoomDefinition first = data.Rooms[0];
                data.Rooms[0] = data.Rooms[inside];
                data.Rooms[inside] = first;
            }
        }

        private static CardinalDirection Facing(float degrees)
        {
            int quarter = Mathf.RoundToInt(degrees / 90f) & 3;
            switch (quarter)
            {
                case 1: return CardinalDirection.East;
                case 2: return CardinalDirection.South;
                case 3: return CardinalDirection.West;
                default: return CardinalDirection.North;
            }
        }
    }
}
