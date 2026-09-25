using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// A debugging view, off by default: the floor painted square by square
    /// wherever a person could stand. It shows the shape of the walkable world
    /// the way the simulation sees it -- so furniture reads as holes, doorways
    /// read as gaps in the walls, and anywhere too tight to squeeze through
    /// simply is not painted.
    ///
    /// The whole thing is one mesh built once, because the building does not
    /// change shape while it is being looked at, and ten thousand separate
    /// objects would cost more to draw than the game does.
    ///
    /// Presentation only: it reads the grid and never tells it anything.
    /// </summary>
    internal sealed class NavigationGridView
    {
        /// <summary>Squares are drawn a little small, so the gaps between them read as a grid.</summary>
        private const float InsetMetres = 0.02f;

        /// <summary>Just above the floor, so it paints over it rather than fighting with it.</summary>
        private const float HeightMetres = 0.012f;

        private readonly GameObject root;

        public NavigationGridView(Run simulation, Transform parent, int bodyRadiusMillimetres)
        {
            root = new GameObject("NavigationGrid");
            root.transform.SetParent(parent, false);
            root.SetActive(false);

            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            filter.sharedMesh = BuildMesh(simulation, bodyRadiusMillimetres);
            renderer.sharedMaterial = WalkableMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Whether the overlay is being shown.</summary>
        public bool Shown => root != null && root.activeSelf;

        public void Toggle()
        {
            if (root != null)
            {
                root.SetActive(!root.activeSelf);
            }
        }

        /// <summary>One flat square for every patch of floor a person of this size could stand on.</summary>
        private static Mesh BuildMesh(Run simulation, int bodyRadiusMillimetres)
        {
            NavigationGridReading grid = simulation.ReadNavigationGrid();
            float half = Metres(NavigationGrid.CellSizeMillimetres) / 2f - InsetMetres;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < grid.Count; i++)
            {
                if (!grid.FitsABody(i, bodyRadiusMillimetres))
                {
                    continue;
                }

                LogicalPosition centre = grid.CentreOf(i);
                float x = Metres(centre.X);
                float z = Metres(centre.Z);
                int corner = vertices.Count;
                vertices.Add(new Vector3(x - half, HeightMetres, z - half));
                vertices.Add(new Vector3(x - half, HeightMetres, z + half));
                vertices.Add(new Vector3(x + half, HeightMetres, z + half));
                vertices.Add(new Vector3(x + half, HeightMetres, z - half));
                triangles.Add(corner);
                triangles.Add(corner + 1);
                triangles.Add(corner + 2);
                triangles.Add(corner);
                triangles.Add(corner + 2);
                triangles.Add(corner + 3);
            }

            var mesh = new Mesh { name = "NavigationGrid" };

            // A big building is easily past the 65k vertices a small index
            // buffer can address.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material WalkableMaterial()
        {
            // A plain unlit colour. If neither shader is in the build this is
            // a debugging overlay nobody can see rather than a broken scene, so
            // it falls back rather than throwing.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            return new Material(shader) { color = new Color(0.2f, 0.9f, 0.7f, 1f) };
        }
    }
}
