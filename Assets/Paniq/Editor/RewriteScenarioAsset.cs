using Paniq.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Paniq.EditorTools
{
    /// <summary>
    /// Puts the scenario asset back in step with the numbers in the code.
    /// <para>
    /// Every tunable value has its default written once, in
    /// <c>FireReactionScenarioData</c> and <c>ScenarioSettings.cs</c>. The asset
    /// in <c>Assets/Paniq/Content</c> is a saved copy of those, so the owner can
    /// change them in the Inspector without touching code. Whenever a default
    /// changes, or a new group of settings or a new kind of thing is added, the
    /// saved copy no longer matches and the edit-mode test
    /// <c>ScenarioAsset_MatchesTheCodeDefaults</c> fails.
    /// </para>
    /// <para>
    /// This command throws the saved copy away and lets Unity write a fresh one
    /// from the code. Any values changed by hand in the Inspector are lost, which
    /// is the point: it is how a scenario is reset to the defaults.
    /// </para>
    /// </summary>
    internal static class RewriteScenarioAsset
    {
        private const string AssetPath = "Assets/Paniq/Content/FireReactionScenario.asset";

        [MenuItem("Paniq/Rewrite Scenario Asset From Code Defaults")]
        private static void Rewrite()
        {
            var asset = AssetDatabase.LoadAssetAtPath<FireReactionScenario>(AssetPath);
            if (asset == null)
            {
                Debug.LogError($"Paniq: no scenario asset at {AssetPath}.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Rewrite scenario asset?",
                    "This replaces every value in the scenario asset with the defaults written in the code. " +
                    "Anything changed by hand in the Inspector will be lost.",
                    "Rewrite it",
                    "Cancel"))
            {
                return;
            }

            // Overwriting the serialized data with a fresh instance is enough:
            // Unity writes the new values out when the asset is saved.
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            FireReactionScenario fresh = FireReactionScenario.CreateDefault();
            EditorUtility.CopySerialized(fresh, asset);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            if (asset.IsValid(out string error))
            {
                Debug.Log($"Paniq: {AssetPath} rewritten from the code defaults.");
            }
            else
            {
                Debug.LogError($"Paniq: {AssetPath} was rewritten but is not valid: {error}");
            }
        }
    }
}
