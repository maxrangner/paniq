using UnityEditor;
using UnityEngine;

namespace Paniq.Editor
{
    /// <summary>
    /// Keeps the project-wide physics settings the simulation depends on, put
    /// back whenever the editor loads rather than trusted to stay ticked.
    /// Unity has no scripting call for either, so they are set on the saved
    /// settings file directly.
    /// <list type="bullet">
    /// <item>Enhanced Determinism: the engine's promise that a scene plays out
    /// the same way whatever else exists alongside it, provided bodies are
    /// added in the same order. Replays rest on it.</item>
    /// <item>Improved Patch Friction: without it the engine's friction can come
    /// out up to twice as strong as asked for, so a box given a floor grip of
    /// 0.27 slows as if it were 0.54, and every slide distance tuned in the
    /// scenario is wrong.</item>
    /// <item>The standard (Projected Gauss-Seidel) solver. The Temporal one,
    /// for all Unity's documentation says of its handling of very different
    /// masses, let a person pushing into an office chair sink half a metre
    /// into it and left a laptop hanging off a desk. The difference in mass
    /// between people and the things they shove is dealt with instead by a
    /// floor on how light a loose thing is to the engine (see PhysicsWorld).</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectPhysicsSettings
    {
        private const string SettingsPath = "ProjectSettings/DynamicsManager.asset";

        static ProjectPhysicsSettings()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Paniq/Project/Apply Physics Settings", priority = 1)]
        private static void Apply()
        {
            Object[] loaded = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning($"Paniq: could not open {SettingsPath} to check Enhanced Determinism.");
                return;
            }

            var settings = new SerializedObject(loaded[0]);
            bool changed = TurnOn(settings, "m_EnableEnhancedDeterminism", "Enhanced Determinism, which replays depend on");
            changed |= TurnOn(settings, "m_ImprovedPatchFriction", "Improved Patch Friction, which slide distances depend on");
            changed |= UseStandardSolver(settings);
            if (!changed)
            {
                return;
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static bool UseStandardSolver(SerializedObject settings)
        {
            const int ProjectedGaussSeidel = 0;
            SerializedProperty solver = settings.FindProperty("m_SolverType");
            if (solver == null || solver.intValue == ProjectedGaussSeidel)
            {
                return false;
            }

            solver.intValue = ProjectedGaussSeidel;
            Debug.Log("Paniq: put the physics solver back to the standard one.");
            return true;
        }

        private static bool TurnOn(SerializedObject settings, string property, string description)
        {
            SerializedProperty setting = settings.FindProperty(property);
            if (setting == null)
            {
                Debug.LogWarning($"Paniq: this version of Unity has no {property}; {description} cannot be checked.");
                return false;
            }

            if (setting.boolValue)
            {
                return false;
            }

            setting.boolValue = true;
            Debug.Log($"Paniq: turned on {description}.");
            return true;
        }
    }
}
