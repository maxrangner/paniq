using System;
using UnityEditor;

namespace Paniq.EditorTools
{
    /// <summary>
    /// How Unity reads a model built by tools/BuildModel.ps1. Every FBX under
    /// Content/Models and under the test fixtures gets the same settings on
    /// import, the first time and every time after, so a built model is never
    /// set up by hand in the Inspector, and a hand change there does not
    /// survive a reimport. What each setting is for is in docs/model-pipeline.md.
    /// </summary>
    public sealed class ModelImportSettings : AssetPostprocessor
    {
        public const string ContentFolder = "Assets/Paniq/Content/Models/";
        public const string FixtureFolder = "Assets/Paniq/Tests/Fixtures/Models/";

        /// <summary>Raise this to make Unity reimport every covered model with changed settings.</summary>
        public override uint GetVersion() => 1;

        public static bool Covers(string path)
        {
            return path.StartsWith(ContentFolder, StringComparison.Ordinal)
                || path.StartsWith(FixtureFolder, StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (!Covers(assetPath))
            {
                return;
            }

            Apply((ModelImporter)assetImporter);
        }

        public static void Apply(ModelImporter importer)
        {
            // Size: a Blender metre is a Unity metre, read from the file rather than guessed.
            importer.useFileUnits = true;
            importer.useFileScale = true;
            importer.globalScale = 1f;

            // Up and forward are baked into the mesh, so no object carries a stray quarter turn.
            importer.bakeAxisConversion = true;

            // Nothing but the mesh: colour comes from the game's materials, collision from the simulation.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            // Motion belongs to the game: hinge parts are turned and shape keys dialled in code.
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = true;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;

            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }
    }
}
