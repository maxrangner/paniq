using System.IO;
using Paniq.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.EditorTools
{
    /// <summary>
    /// Makes the level asset for the office and hands it to the runner in the
    /// open scene.
    /// <para>
    /// A level is the small asset that says which building to run, how a round
    /// in it begins and what it takes to clear it. There is one today; a second
    /// one is a duplicate of this asset with a different scenario, which is why
    /// it is worth having at all. The scene plays correctly without it, so this
    /// is a tidying step rather than a requirement.
    /// </para>
    /// </summary>
    internal static class CreateOfficeLevel
    {
        private const string FolderPath = "Assets/Paniq/Content/Levels";
        private const string AssetPath = FolderPath + "/TheOffice.asset";
        private const string ScenarioPath = "Assets/Paniq/Content/FireReactionScenario.asset";
        private const string FeelPath = "Assets/Paniq/Content/PhysicsFeel-Cartoon.asset";
        private const string ScenePath = "Assets/Paniq/Scenes/FireReactionPrototype.unity";

        [MenuItem("Paniq/Create Or Update The Office Level")]
        public static void CreateOrUpdate()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
                AssetDatabase.Refresh();
            }

            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, AssetPath);
            }

            var serialized = new SerializedObject(level);
            serialized.FindProperty("levelId").stringValue = "the-office";
            serialized.FindProperty("displayName").stringValue = "The Office";
            serialized.FindProperty("hazardWaitsForTrigger").boolValue = true;
            serialized.FindProperty("targetSavedPercent").intValue = 75;
            serialized.FindProperty("influenceEnabled").boolValue = false;
            serialized.FindProperty("scenario").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ScenarioAsset>(ScenarioPath);
            serialized.FindProperty("physicsFeel").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PhysicsFeelPreset>(FeelPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();

            int wired = GiveItToTheRunnerInThePrototypeScene(level);
            AssetDatabase.Refresh();
            Debug.Log($"Paniq: the office level is at {AssetPath}, and {wired} runner(s) in " +
                      $"{ScenePath} now use it.", level);
        }

        /// <summary>
        /// Opens the prototype scene, hands the level to every runner in it,
        /// and saves it. The scene is the only place a runner lives, so this
        /// is the whole wiring job.
        /// </summary>
        private static int GiveItToTheRunnerInThePrototypeScene(LevelDefinition level)
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
                        Debug.LogWarning("Paniq: the level asset was written, but the scene was not wired up " +
                                         "because the open scene has unsaved changes.");
                        return 0;
                    }
                }
                else if (scene.isDirty)
                {
                    Debug.LogWarning("Paniq: the level asset was written, but the open scene has unsaved changes, " +
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
                    serialized.FindProperty("level").objectReferenceValue = level;
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
