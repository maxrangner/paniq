using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Paniq.Editor
{
    /// <summary>
    /// Creates and assigns the project's URP asset through Unity's own menu
    /// command, so serialized rendering assets always match the installed
    /// editor, and sets up seeing people through walls: the renderer draws the
    /// see-through layer twice, once normally and once as a pale silhouette
    /// wherever a wall stands in front of it. This follows Unity's own
    /// "character behind objects" recipe for the Render Objects feature.
    /// </summary>
    public static class ConfigureUrpProject
    {
        private const string RenderingFolder = "Assets/Paniq/Content/Rendering";
        private const string CreateUrpMenuItem = "Assets/Create/Rendering/URP Asset (with Universal Renderer)";

        /// <summary>Matches PresentationUtility.SeeThroughLayer; editor code cannot reference the runtime assembly.</summary>
        private const string SeeThroughLayerName = "SeeThrough";

        private const string BehindFeatureName = "Paniq See-Through (behind walls)";
        private const string InFrontFeatureName = "Paniq See-Through (in front)";

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
                    Debug.LogError($"Unity could not execute '{CreateUrpMenuItem}'.");
                    return;
                }

                pipeline = FindUrpPipeline();
            }

            if (pipeline == null)
            {
                Debug.LogError("Unity did not create a URP pipeline asset.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            ConfigureSeeThrough();
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(pipeline);
        }

        /// <summary>
        /// Leaves the see-through layer out of the renderer's ordinary opaque
        /// pass and draws it with two Render Objects passes instead: the parts
        /// in front of everything as usual, and the parts behind something
        /// with a pale see-through material and no depth writing.
        /// </summary>
        private static void ConfigureSeeThrough()
        {
            int layer = LayerMask.NameToLayer(SeeThroughLayerName);
            if (layer < 0)
            {
                Debug.LogError($"Add a '{SeeThroughLayerName}' layer in Project Settings > Tags and Layers first.");
                return;
            }

            UniversalRendererData renderer = FindRendererData();
            if (renderer == null)
            {
                Debug.LogError("Unity did not create a Universal Renderer asset to configure.");
                return;
            }

            int mask = 1 << layer;
            renderer.opaqueLayerMask &= ~mask;
            renderer.transparentLayerMask &= ~mask;

            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures.ToArray())
            {
                if (existing != null && (existing.name == BehindFeatureName || existing.name == InFrontFeatureName))
                {
                    renderer.rendererFeatures.Remove(existing);
                    AssetDatabase.RemoveObjectFromAsset(existing);
                    UnityEngine.Object.DestroyImmediate(existing, true);
                }
            }

            AddRenderObjects(renderer, BehindFeatureName, mask, SeeThroughMaterial(), true);
            AddRenderObjects(renderer, InFrontFeatureName, mask, null, false);
            ValidateFeatures(renderer);
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>One Render Objects pass over the see-through layer.</summary>
        private static void AddRenderObjects(
            ScriptableRendererData renderer,
            string featureName,
            int layerMask,
            Material overrideMaterial,
            bool behind)
        {
            var feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = featureName;
            RenderObjectsSettings settings = feature.settings;
            settings.Event = RenderPassEvent.AfterRenderingOpaques;
            settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            settings.filterSettings.LayerMask = layerMask;
            settings.overrideMaterial = overrideMaterial;
            settings.overrideMode = overrideMaterial != null
                ? RenderObjectsSettings.OverrideMaterialMode.Material
                : RenderObjectsSettings.OverrideMaterialMode.None;

            // Behind: drawn only where something else is nearer the camera, and
            // the depth buffer is left alone so the ordinary pass still wins.
            settings.overrideDepthState = true;
            settings.depthCompareFunction = behind ? CompareFunction.Greater : CompareFunction.LessEqual;
            settings.enableWrite = !behind;

            renderer.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, renderer);
        }

        /// <summary>The pale material the silhouettes are drawn with, made once and kept beside the pipeline asset.</summary>
        private static Material SeeThroughMaterial()
        {
            string path = $"{RenderingFolder}/SeeThrough.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("The URP Unlit shader is missing; see-through silhouettes need it.");
                return null;
            }

            var material = new Material(shader) { name = "SeeThrough" };
            material.SetColor("_BaseColor", new Color(0.65f, 0.78f, 0.95f, 0.3f));

            // The transparent setup URP's own shader inspector writes.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Rebuilds the renderer's feature map. Unity keeps that method
        /// internal, so it is called by reflection; without it the two passes
        /// are stored but never run.
        /// </summary>
        private static void ValidateFeatures(ScriptableRendererData renderer)
        {
            MethodInfo validate = typeof(ScriptableRendererData).GetMethod(
                "ValidateRendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            if (validate != null)
            {
                validate.Invoke(renderer, null);
                return;
            }

            Debug.LogWarning("Could not rebuild the renderer feature map; open the renderer asset in the Inspector once.");
        }

        private static UniversalRendererData FindRendererData()
        {
            foreach (string assetGuid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { RenderingFolder }))
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(assetGuid));
                if (renderer != null)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static RenderPipelineAsset FindUrpPipeline()
        {
            var assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { RenderingFolder });
            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                if (pipeline != null)
                {
                    return pipeline;
                }
            }

            return null;
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
