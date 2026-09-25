using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The model pipeline's ruler. The calibration box that
    /// <c>tools\BuildModel.ps1 -Example CalibrationBox</c> builds must arrive
    /// in Unity a metre across and standing on the floor, its top bump on +Y,
    /// its front bump on +X (the way people face) and its right bump on +Z,
    /// its flap pivoting on the hinge, its squash as a blend shape, with nothing carrying a stray
    /// rotation or scale, no collider and no material. If a Blender or Unity
    /// upgrade breaks any of that, this fails before the first real model
    /// does. The rules are in docs/model-pipeline.md.
    /// </summary>
    public sealed class ModelsEditModeTests
    {
        private const string FixturePath = "Assets/Paniq/Tests/Fixtures/Models/CalibrationBox.fbx";
        private const string ReportPath = "docs/models/previews/CalibrationBox.json";
        private const float Millimetre = 0.001f;

        [System.Serializable]
        private sealed class Report
        {
            public int triangles;
            public string[] parts;
            public string[] shape_keys;
        }

        private GameObject model;

        [SetUp]
        public void SetUp()
        {
            model = AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath);
            Assert.That(model, Is.Not.Null, $"{FixturePath} is missing; run tools\\BuildModel.ps1 -Example CalibrationBox.");
        }

        /// <summary>
        /// A Blender metre is a Unity metre, the floor is at zero, up is +Y,
        /// the front is +X and the model's right is +Z. Measured over the
        /// vertices rather than <see cref="Mesh.bounds"/>, which Unity widens
        /// to make room for the blend shape.
        /// </summary>
        [Test]
        public void TheBox_IsAMetreAcross_StandsOnTheFloor_AndFacesPlusX()
        {
            Bounds bounds = VertexBounds(BodyMesh());
            AssertNear(bounds.min, new Vector3(-0.5f, 0f, -0.5f), "the lowest corner");
            AssertNear(bounds.max, new Vector3(0.6f, 1.2f, 0.6f),
                "the highest corner (front bump on +X, top bump on +Y, right bump on +Z)");
        }

        /// <summary>The axis conversion is baked into the mesh: no object carries a quarter turn or a scale of 100.</summary>
        [Test]
        public void NothingCarriesAStrayRotationOrScale()
        {
            foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(Quaternion.Angle(transform.localRotation, Quaternion.identity), Is.LessThan(0.01f),
                    $"{transform.name} is turned by {transform.localRotation.eulerAngles}.");
                AssertNear(transform.localScale, Vector3.one, $"the scale of {transform.name}");
            }
        }

        /// <summary>A moving part is its own object with its pivot on the hinge, so the game can swing it.</summary>
        [Test]
        public void TheFlap_IsItsOwnObject_PivotingOnItsHinge()
        {
            Transform lid = Find("Lid");
            Assert.That(lid, Is.Not.Null, "The part 'Lid' did not arrive as an object of its own.");
            AssertNear(lid.localPosition, new Vector3(-0.5f, 1f, 0f), "the hinge (back top edge)");
            Bounds flap = VertexBounds(MeshOf(lid));
            AssertNear(flap.min, new Vector3(0f, 0f, -0.3f), "the flap's near corner, on the hinge");
            AssertNear(flap.max, new Vector3(0.3f, 0.02f, 0.3f), "the flap's far corner, towards the front");
        }

        /// <summary>A shape key in Blender is a blend shape in Unity, and it keeps its name.</summary>
        [Test]
        public void TheSquash_ArrivesAsABlendShape()
        {
            Mesh body = BodyMesh();
            Assert.That(body.blendShapeCount, Is.EqualTo(1), "one shape key was exported");
            Assert.That(body.GetBlendShapeName(0), Is.EqualTo("Squash"));
        }

        /// <summary>Colour comes from the game's materials and collision from the simulation, never from the file.</summary>
        [Test]
        public void NoColliderAndNoMaterialComeWithTheFile()
        {
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, "a collider was generated");
            Assert.That(AssetDatabase.LoadAllAssetRepresentationsAtPath(FixturePath).OfType<Material>(), Is.Empty,
                "a material was imported");
        }

        /// <summary>What Unity got is what Blender said it sent: the report beside the preview agrees.</summary>
        [Test]
        public void WhatUnityGot_MatchesTheBuildReport()
        {
            string path = Path.Combine(Application.dataPath, "..", ReportPath);
            Assert.That(File.Exists(path), Is.True, $"{ReportPath} is missing; the build writes it.");
            Report report = JsonUtility.FromJson<Report>(File.ReadAllText(path));

            int triangles = model.GetComponentsInChildren<Transform>(true)
                .Select(MeshOf).Where(mesh => mesh != null).Sum(mesh => mesh.triangles.Length / 3);
            Assert.That(triangles, Is.EqualTo(report.triangles), "triangles");
            Assert.That(report.parts, Is.EquivalentTo(new[] { "Lid" }));
            Assert.That(report.shape_keys, Is.EquivalentTo(new[] { "Squash" }));
        }

        /// <summary>The importer carries the pipeline's settings, applied on import rather than by hand.</summary>
        [Test]
        public void TheImporter_CarriesThePipelineSettings()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FixturePath);
            Assert.That(importer.useFileUnits, Is.True, "useFileUnits");
            Assert.That(importer.useFileScale, Is.True, "useFileScale");
            Assert.That(importer.globalScale, Is.EqualTo(1f), "globalScale");
            Assert.That(importer.bakeAxisConversion, Is.True, "bakeAxisConversion");
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None), "materialImportMode");
            Assert.That(importer.addCollider, Is.False, "addCollider");
            Assert.That(importer.importAnimation, Is.False, "importAnimation");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None), "animationType");
            Assert.That(importer.importBlendShapes, Is.True, "importBlendShapes");
            Assert.That(importer.isReadable, Is.False, "isReadable");
        }

        private Mesh BodyMesh()
        {
            Mesh mesh = model.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name != "Lid").Select(MeshOf).FirstOrDefault(found => found != null);
            Assert.That(mesh, Is.Not.Null, "The box's own mesh did not arrive.");
            return mesh;
        }

        private Transform Find(string name)
        {
            return model.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
        }

        private static Mesh MeshOf(Transform transform)
        {
            var skinned = transform.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                return skinned.sharedMesh;
            }

            var filter = transform.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static Bounds VertexBounds(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Assert.That(vertices, Is.Not.Empty, $"{mesh.name} has no vertices.");
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                bounds.Encapsulate(vertex);
            }

            return bounds;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Millimetre),
                $"{what}: expected {expected:F4}, got {actual:F4}.");
        }
    }
}
