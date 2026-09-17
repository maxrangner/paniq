using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Paniq.Editor
{
    /// <summary>
    /// Creates and assigns the project's URP asset through Unity's own menu
    /// command, so serialized rendering assets always match the installed editor.
    /// </summary>
    public static class ConfigureUrpProject
    {
        private const string RenderingFolder = "Assets/Paniq/Content/Rendering";
        private const string CreateUrpMenuItem = "Assets/Create/Rendering/URP Asset (with Universal Renderer)";

        [MenuItem("Paniq/Project/Configure URP", priority = 0)]
        public static void Configure()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                if (GraphicsSettings.defaultRenderPipeline.GetType().Name == "UniversalRenderPipelineAsset")
                {
                    EditorGUIUtility.PingObject(GraphicsSettings.defaultRenderPipeline);
                    return;
                }

                EditorUtility.DisplayDialog(
                    "Paniq URP setup",
                    "A non-URP render pipeline is already assigned. Clear or replace it deliberately before configuring Paniq URP.",
                    "OK");
                return;
            }

            EnsureFolder(RenderingFolder);
            var pipeline = FindUrpPipeline();
            if (pipeline == null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RenderingFolder);
                if (!EditorApplication.ExecuteMenuItem(CreateUrpMenuItem))
                {
                    throw new InvalidOperationException($"Unity could not execute '{CreateUrpMenuItem}'.");
                }

                pipeline = FindUrpPipeline();
            }

            if (pipeline == null)
            {
                throw new InvalidOperationException("Unity did not create a URP pipeline asset.");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(pipeline);
        }

        private static RenderPipelineAsset FindUrpPipeline()
        {
            var assetGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { RenderingFolder });
            if (assetGuids.Length == 0)
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);
            return AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
        }

        private static void EnsureFolder(string targetFolder)
        {
            var currentFolder = "Assets";
            foreach (var folderPart in targetFolder.Substring("Assets/".Length).Split('/'))
            {
                var nextFolder = $"{currentFolder}/{folderPart}";
                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    AssetDatabase.CreateFolder(currentFolder, folderPart);
                }

                currentFolder = nextFolder;
            }
        }
    }
}
