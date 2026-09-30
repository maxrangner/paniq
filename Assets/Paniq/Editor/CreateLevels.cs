using System.Collections.Generic;
using System.IO;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.EditorTools
{
    /// <summary>
    /// Makes the level assets and hands them to the runner in the prototype
    /// scene: the office, and since 2026-09-30 the three blank test levels
    /// (the square room, the maze and the interaction room).
    /// <para>
    /// A level is the small asset that says which building to run, how a
    /// round in it begins and what it takes to clear it. The assets are
    /// checked in, so this is a repair path rather than a requirement: run it
    /// if a level asset has gone missing or the runner's level list is empty,
    /// and it writes every level again and lists them all on the runner.
    /// </para>
    /// </summary>
    internal static class CreateLevels
    {
        private const string FolderPath = "Assets/Paniq/Content/Levels";
        private const string ScenarioPath = "Assets/Paniq/Content/FireReactionScenario.asset";
        private const string FeelPath = "Assets/Paniq/Content/PhysicsFeel-Cartoon.asset";
        private const string ScenePath = "Assets/Paniq/Scenes/FireReactionPrototype.unity";

        /// <summary>One row per level: the file, and every field the asset holds.</summary>
        private readonly struct Row
        {
            public Row(string file, string levelId, string displayName, BuiltInBuilding building,
                bool purse, bool ladder, bool cap, bool playerPullsAlarms, bool crowdSwitch, bool triggerStartsAHazard)
            {
                File = file;
                LevelId = levelId;
                DisplayName = displayName;
                Building = building;
                Purse = purse;
                Ladder = ladder;
                Cap = cap;
                PlayerPullsAlarms = playerPullsAlarms;
                CrowdSwitch = crowdSwitch;
                TriggerStartsAHazard = triggerStartsAHazard;
            }

            public string File { get; }
            public string LevelId { get; }
            public string DisplayName { get; }
            public BuiltInBuilding Building { get; }
            public bool Purse { get; }
            public bool Ladder { get; }
            public bool Cap { get; }
            public bool PlayerPullsAlarms { get; }
            public bool CrowdSwitch { get; }
            public bool TriggerStartsAHazard { get; }
        }

        /// <summary>The office first: it is the level the runner is wired to, and the first on the start card.</summary>
        private static readonly Row[] Rows =
        {
            new Row("TheOffice", "the-office", "The Office", BuiltInBuilding.None,
                purse: false, ladder: true, cap: true, playerPullsAlarms: false, crowdSwitch: false, triggerStartsAHazard: true),
            new Row("Square", "square", "The Square Room", BuiltInBuilding.SquareRoom,
                purse: true, ladder: false, cap: false, playerPullsAlarms: true, crowdSwitch: true, triggerStartsAHazard: false),
            new Row("Maze", "maze", "The Maze", BuiltInBuilding.Maze,
                purse: true, ladder: false, cap: false, playerPullsAlarms: true, crowdSwitch: true, triggerStartsAHazard: false),
            new Row("Interaction", "interaction", "The Interaction Room", BuiltInBuilding.InteractionRoom,
                purse: true, ladder: false, cap: false, playerPullsAlarms: true, crowdSwitch: true, triggerStartsAHazard: true)
        };

        [MenuItem("Paniq/Create Or Update The Levels")]
        public static void CreateOrUpdate()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
                AssetDatabase.Refresh();
            }

            var written = new List<LevelDefinition>();
            foreach (Row row in Rows)
            {
                written.Add(Write(row));
            }

            AssetDatabase.SaveAssets();
            int wired = GiveThemToTheRunnerInThePrototypeScene(written);
            AssetDatabase.Refresh();
            Debug.Log($"Paniq: {written.Count} levels are in {FolderPath}, and {wired} runner(s) in " +
                      $"{ScenePath} now offer them.", written[0]);
        }

        private static LevelDefinition Write(Row row)
        {
            string path = $"{FolderPath}/{row.File}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, path);
            }

            var serialized = new SerializedObject(level);
            serialized.FindProperty("levelId").stringValue = row.LevelId;
            serialized.FindProperty("displayName").stringValue = row.DisplayName;
            serialized.FindProperty("hazardWaitsForTrigger").boolValue = true;
            serialized.FindProperty("targetSavedPercent").intValue = 75;
            serialized.FindProperty("purseEnabled").boolValue = row.Purse;
            serialized.FindProperty("directorClimbsTheLadder").boolValue = row.Ladder;
            serialized.FindProperty("directorCapsTheRound").boolValue = row.Cap;
            serialized.FindProperty("playerPullsAlarms").boolValue = row.PlayerPullsAlarms;
            serialized.FindProperty("builtInBuilding").enumValueIndex = (int)row.Building;
            serialized.FindProperty("offersCrowdSwitch").boolValue = row.CrowdSwitch;
            serialized.FindProperty("triggerStartsAHazard").boolValue = row.TriggerStartsAHazard;
            serialized.FindProperty("scenario").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ScenarioAsset>(ScenarioPath);
            serialized.FindProperty("physicsFeel").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PhysicsFeelPreset>(FeelPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        /// <summary>
        /// Opens the prototype scene, hands the first level to every runner
        /// in it as the one it is wired to and the whole list as what the
        /// start card offers, and saves it. The scene is the only place a
        /// runner lives, so this is the whole wiring job.
        /// </summary>
        private static int GiveThemToTheRunnerInThePrototypeScene(IReadOnlyList<LevelDefinition> levels)
        {
            Scene scene = SceneManager.GetActiveScene();
            bool alreadyOpen = scene.path == ScenePath;
            if (!alreadyOpen)
            {
                // Never throw away work somebody has open. In batch mode there
                // is nobody to ask, so an unsaved scene stops the command.
                if (!Application.isBatchMode)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        Debug.LogWarning("Paniq: the level assets were written, but the scene was not wired up " +
                                         "because the open scene has unsaved changes.");
                        return 0;
                    }
                }
                else if (scene.isDirty)
                {
                    Debug.LogWarning("Paniq: the level assets were written, but the open scene has unsaved changes, " +
                                     "so it was left alone.");
                    return 0;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            int wired = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (RunDriver runner in rootObject.GetComponentsInChildren<RunDriver>(true))
                {
                    var serialized = new SerializedObject(runner);
                    serialized.FindProperty("level").objectReferenceValue = levels[0];
                    SerializedProperty list = serialized.FindProperty("levels");
                    list.arraySize = levels.Count;
                    for (int i = 0; i < levels.Count; i++)
                    {
                        list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(runner);
                    wired++;
                }
            }

            if (wired > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return wired;
        }
    }
}
