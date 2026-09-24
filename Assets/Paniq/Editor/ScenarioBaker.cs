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
            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioAsset>(ScenarioAssetPath);
            if (scenario == null)
            {
                EditorUtility.DisplayDialog("Paniq", "There is no scenario at " + ScenarioAssetPath + ".", "Right");
                return;
            }

            // Start from the current settings, so the tuning numbers survive and
            // only the shape of the building is taken from the scene.
            ScenarioData data = scenario.ToRuntimeData();
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
            BakeCues(Find<PaniqCue>(), data, problems);

            if (problems.Count == 0)
            {
                try
                {
                    // The same check a run makes when it loads, so a floor plan
                    // that would fail at play time fails here instead, with the
                    // scene still in front of you.
                    data.Clone().Validate();
                    new Run(data);
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

        private static RoomDefinition[] BakeRooms(PaniqRoom[] rooms, List<string> problems)
        {
            var baked = new RoomDefinition[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                RectInt floor = rooms[i].Floor;
                baked[i] = new RoomDefinition(
                    new SimulationId((ulong)rooms[i].RoomId),
                    new LogicalBounds(floor.xMin, floor.xMax, floor.yMin, floor.yMax),
                    rooms[i].Use);

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
        private static DoorDefinition[] BakeDoors(PaniqDoor[] doors, PaniqRoom[] rooms, List<string> problems)
        {
            var baked = new List<DoorDefinition>();
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
                baked.Add(new DoorDefinition(
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

        private static TableDefinition[] BakeTables(PaniqTable[] tables, List<string> problems)
        {
            var baked = new TableDefinition[tables.Length];
            for (int i = 0; i < tables.Length; i++)
            {
                Vector2Int centre = PaniqAuthoring.Millimetres(tables[i].transform.position);
                Vector2Int size = PaniqAuthoring.SizeMillimetres(tables[i].transform);
                baked[i] = new TableDefinition(
                    new SimulationId((ulong)tables[i].TableId),
                    new LogicalPosition(centre.x, centre.y),
                    size.x,
                    size.y);
            }

            return baked;
        }

        private static PhysicsObjectDefinition[] BakeProps(PaniqProp[] props, List<string> problems)
        {
            var baked = new PhysicsObjectDefinition[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                PaniqProp prop = props[i];
                Vector2Int at = PaniqAuthoring.Millimetres(prop.transform.position);
                baked[i] = new PhysicsObjectDefinition(
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

        private static AgentDefinition[] BakePeople(PaniqPerson[] people, List<string> problems)
        {
            var baked = new AgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                PaniqPerson person = people[i];
                Vector2Int at = PaniqAuthoring.Millimetres(person.transform.position);
                var where = new LogicalPosition(at.x, at.y);
                CardinalDirection facing = Facing(person.transform.eulerAngles.y);

                baked[i] = person.AuthorTheirPersonality
                    ? new AgentDefinition(
                        new SimulationId((ulong)person.AgentId), where, facing,
                        new AgentTraitValues(person.Strength, person.Speed, person.Bravery,
                            person.Compassion, person.Evil, person.Nervousness, person.Leadership))
                    : new AgentDefinition(new SimulationId((ulong)person.AgentId), where, facing);
                baked[i] = baked[i].WithFamiliarity(
                    person.Visitor ? AgentFamiliarity.Visitor : AgentFamiliarity.KnowsTheBuilding);

                // Where they belong: a chair of theirs, or where they stand.
                // The run refuses a home that is not a chair, so a prop that
                // is not one is reported here, in the scene's own terms.
                if (person.Home != null)
                {
                    if (person.Home.Kind != PhysicsObjectKind.Chair && person.Home.Kind != PhysicsObjectKind.OfficeChair)
                    {
                        problems.Add("Person " + person.name + "'s home is " + person.Home.name + ", which is not a chair.");
                    }

                    baked[i] = baked[i].WithHome(new SimulationId((ulong)person.Home.ObjectId));
                }
                else if (person.HomeIsWhereTheyStand)
                {
                    baked[i] = baked[i].WithHome(where);
                }
            }

            return baked;
        }

        /// <summary>
        /// The timetable, from the cues placed in the scene, in the order they
        /// happen. A scene with none keeps the timetable the level already
        /// has, the way it keeps its exit signs.
        /// </summary>
        private static void BakeCues(PaniqCue[] cues, ScenarioData data, List<string> problems)
        {
            if (cues.Length == 0)
            {
                return;
            }

            var baked = new List<ScheduledCue>();
            foreach (PaniqCue cue in cues)
            {
                // What may be scheduled comes from the cue's own definition,
                // the same rule the run checks: one that reaches a room or the
                // whole building, and a room named only for the former.
                CueDefinition definition = data.CueOf(cue.Kind);
                if (!definition.IsSchedulable)
                {
                    problems.Add("Cue " + cue.name + " is a " + cue.Kind + ", which is somebody's own idea and cannot be scheduled.");
                    continue;
                }

                bool inARoom = definition.Audience == CueAudience.Room;
                if (inARoom && cue.Room == null)
                {
                    problems.Add("Cue " + cue.name + " happens in a room but does not say which.");
                    continue;
                }

                baked.Add(new ScheduledCue(
                    cue.Kind,
                    Mathf.Max(1, Mathf.RoundToInt(cue.AtSeconds * Run.TicksPerSecond)),
                    Mathf.Max(0, Mathf.RoundToInt(cue.SpreadSeconds * Run.TicksPerSecond)),
                    inARoom ? new SimulationId((ulong)cue.Room.RoomId) : default));
            }

            baked.Sort((left, right) => left.AtTick.CompareTo(right.AtTick));
            data.Timetable = baked.ToArray();
        }

        private static AlarmDefinition[] BakeAlarms(PaniqAlarm[] alarms, List<string> problems)
        {
            var baked = new AlarmDefinition[alarms.Length];
            for (int i = 0; i < alarms.Length; i++)
            {
                Vector2Int at = PaniqAuthoring.Millimetres(alarms[i].transform.position);
                baked[i] = new AlarmDefinition(
                    new SimulationId((ulong)alarms[i].AlarmId),
                    new LogicalPosition(at.x, at.y));
            }

            return baked;
        }

        private static void BakeFire(PaniqFireStart[] starts, ScenarioData data, PaniqRoom[] rooms,
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
                Mathf.RoundToInt(start.StartsAfterSeconds * Run.TicksPerSecond));

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
                RoomDefinition first = data.Rooms[0];
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
