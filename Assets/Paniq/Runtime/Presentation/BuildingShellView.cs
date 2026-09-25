using System.Collections.Generic;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// What turns the floor plan into a building. Without this the office is a
    /// diagram floating on a dark background; with it the floor sits on a
    /// concrete slab, the outside walls carry on downward into a band of dark
    /// windows, and below that is the top of the next storey down before
    /// everything fades into the dark.
    /// <para>
    /// Purely something to look at. It is built once from the same room
    /// bounds the rooms themselves are built from, it never moves, and it
    /// knows nothing about the simulation.
    /// </para>
    /// </summary>
    internal sealed class BuildingShellView
    {
        /// <summary>How far the slab sticks out past the outside walls.</summary>
        private const float LedgeMetres = 0.45f;

        private const float SlabThickness = 0.55f;
        private const float WindowBandHeight = 1.7f;
        private const float SpandrelHeight = 0.45f;
        private const float LowerSlabThickness = 0.5f;

        /// <summary>How far apart the pale uprights between the windows sit.</summary>
        private const float MullionSpacingMetres = 1.6f;

        private const float MullionWidth = 0.12f;

        /// <summary>How far below the floor the building reaches, for the camera to frame.</summary>
        public const float DepthMetres = SlabThickness + WindowBandHeight + SpandrelHeight + LowerSlabThickness + 2.4f;

        private static readonly Color ConcreteColor = new Color(0.30f, 0.31f, 0.34f);
        private static readonly Color GlassColor = new Color(0.07f, 0.11f, 0.15f);
        private static readonly Color MullionColor = new Color(0.46f, 0.48f, 0.52f);

        private readonly List<Material> owned = new List<Material>();

        public BuildingShellView(Bounds floorPlan, Transform parent)
        {
            var root = new GameObject("Building shell").transform;
            root.SetParent(parent, false);

            Material concrete = Own(CreateLit(ConcreteColor));
            Material glass = Own(CreateLit(GlassColor));
            Material mullion = Own(CreateLit(MullionColor));

            float minX = floorPlan.min.x - LedgeMetres;
            float maxX = floorPlan.max.x + LedgeMetres;
            float minZ = floorPlan.min.z - LedgeMetres;
            float maxZ = floorPlan.max.z + LedgeMetres;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            var middle = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

            // The slab this floor stands on. Its top is just under the drawn
            // floor, so the floor keeps its own colour and the slab only shows
            // as an edge all the way round.
            float slabTop = -0.02f;
            CreatePrimitive("Floor slab", PrimitiveType.Cube, root,
                new Vector3(middle.x, slabTop - SlabThickness * 0.5f, middle.z),
                new Vector3(width, SlabThickness, depth), concrete);

            // The storey below: a band of glass set back a little from the
            // slab edge, with pale uprights across it, then a spandrel and the
            // top of the next slab down.
            float glassTop = slabTop - SlabThickness;
            float glassInset = LedgeMetres * 0.55f;
            BuildBand(root, glass, mullion, minX + glassInset, maxX - glassInset, minZ + glassInset, maxZ - glassInset,
                glassTop, WindowBandHeight);

            float spandrelTop = glassTop - WindowBandHeight;
            BuildRing(root, "Spandrel", concrete, minX + glassInset * 0.4f, maxX - glassInset * 0.4f,
                minZ + glassInset * 0.4f, maxZ - glassInset * 0.4f, spandrelTop, SpandrelHeight, LedgeMetres * 0.9f);

            // The lip of the floor below, which is what tells the eye the
            // building carries on down rather than simply stopping.
            float lowerTop = spandrelTop - SpandrelHeight;
            CreatePrimitive("Storey below", PrimitiveType.Cube, root,
                new Vector3(middle.x, lowerTop - LowerSlabThickness * 0.5f, middle.z),
                new Vector3(width, LowerSlabThickness, depth), concrete);

            // A short dark skirt under it all, so the bottom edge reads as the
            // building carrying on into shadow rather than as a cut.
            CreatePrimitive("Into the dark", PrimitiveType.Cube, root,
                new Vector3(middle.x, lowerTop - LowerSlabThickness - 1.2f, middle.z),
                new Vector3(width - LedgeMetres, 2.4f, depth - LedgeMetres), Own(CreateLit(new Color(0.05f, 0.06f, 0.08f))));
        }

        /// <summary>A ring of four walls around the building, hanging down from <paramref name="top"/>.</summary>
        private static void BuildRing(Transform root, string name, Material material,
            float minX, float maxX, float minZ, float maxZ, float top, float height, float thickness)
        {
            float centreY = top - height * 0.5f;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            CreatePrimitive($"{name} north", PrimitiveType.Cube, root,
                new Vector3((minX + maxX) * 0.5f, centreY, maxZ - thickness * 0.5f),
                new Vector3(width, height, thickness), material);
            CreatePrimitive($"{name} south", PrimitiveType.Cube, root,
                new Vector3((minX + maxX) * 0.5f, centreY, minZ + thickness * 0.5f),
                new Vector3(width, height, thickness), material);
            CreatePrimitive($"{name} east", PrimitiveType.Cube, root,
                new Vector3(maxX - thickness * 0.5f, centreY, (minZ + maxZ) * 0.5f),
                new Vector3(thickness, height, depth), material);
            CreatePrimitive($"{name} west", PrimitiveType.Cube, root,
                new Vector3(minX + thickness * 0.5f, centreY, (minZ + maxZ) * 0.5f),
                new Vector3(thickness, height, depth), material);
        }

        /// <summary>The window band: dark glass all the way round, with pale uprights across it.</summary>
        private static void BuildBand(Transform root, Material glass, Material mullion,
            float minX, float maxX, float minZ, float maxZ, float top, float height)
        {
            BuildRing(root, "Windows", glass, minX, maxX, minZ, maxZ, top, height, 0.3f);

            float centreY = top - height * 0.5f;
            AddMullions(root, mullion, minX, maxX, maxZ, true, centreY, height);
            AddMullions(root, mullion, minX, maxX, minZ, true, centreY, height);
            AddMullions(root, mullion, minZ, maxZ, maxX, false, centreY, height);
            AddMullions(root, mullion, minZ, maxZ, minX, false, centreY, height);
        }

        /// <summary>Evenly spaced uprights along one side, inset slightly so they stand proud of the glass.</summary>
        private static void AddMullions(Transform root, Material material, float from, float to, float line,
            bool alongX, float centreY, float height)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt((to - from) / MullionSpacingMetres));
            float step = (to - from) / count;
            for (int i = 0; i <= count; i++)
            {
                float along = from + step * i;
                Vector3 position = alongX
                    ? new Vector3(along, centreY, line)
                    : new Vector3(line, centreY, along);
                Vector3 scale = alongX
                    ? new Vector3(MullionWidth, height, 0.34f)
                    : new Vector3(0.34f, height, MullionWidth);
                CreatePrimitive("Mullion", PrimitiveType.Cube, root, position, scale, material);
            }
        }

        private Material Own(Material material)
        {
            owned.Add(material);
            return material;
        }

        private static Material CreateLit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        /// <summary>Releases the materials this view made, so reloading the scene does not pile them up.</summary>
        public void Destroy()
        {
            foreach (Material material in owned)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }

            owned.Clear();
        }
    }
}
