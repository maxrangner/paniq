using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Each burning cell as a dim glowing tile plus two or three small cubes
    /// that bob, spin, flicker and fade to embers. Variation comes from a hash
    /// of the cell's grid position, never from the simulation's generator.
    /// </summary>
    internal sealed class FireView
    {
        private static readonly Color FlameYellow = new Color(1f, 0.82f, 0.2f);
        private static readonly Color EmberRed = new Color(0.42f, 0.04f, 0.02f);

        private sealed class CellView
        {
            public Renderer Tile;
            public Transform[] Cubes;
            public Renderer[] Renderers;
            public Vector3[] Offsets;
            public float[] Sizes;
            public float[] Seeds;
            public float SpawnTime;

            /// <summary>When this square was hosed down, or -1 while it burns.</summary>
            public float OutSince = -1f;
        }

        private readonly PresentationMaterials materials;
        private readonly Transform parent;
        private readonly List<CellView> cells = new List<CellView>();

        public FireView(PresentationMaterials materials, Transform parent)
        {
            this.materials = materials;
            this.parent = parent;
        }

        public void Update(FireReactionSnapshot snapshot, float time)
        {
            // Burning cells only ever get added, in ignition order.
            while (cells.Count < snapshot.FireCells.Count)
            {
                cells.Add(CreateCell(snapshot.FireCells[cells.Count], cells.Count + 1, time));
            }

            for (int i = 0; i < cells.Count; i++)
            {
                // A square that has been put out: the flames drop away and a
                // dark wet patch is left behind.
                if (snapshot.FireCells[i].IsOut)
                {
                    PutOut(cells[i], time);
                    continue;
                }

                Animate(cells[i], time);
            }
        }

        /// <summary>A square someone has hosed down: cubes gone, a damp scorch mark left.</summary>
        private void PutOut(CellView view, float time)
        {
            if (view.OutSince < 0f)
            {
                view.OutSince = time;
            }

            float age = Mathf.Clamp01((time - view.OutSince) / 0.6f);
            materials.SetColors(view.Tile, Color.Lerp(new Color(0.55f, 0.1f, 0.02f), new Color(0.12f, 0.13f, 0.16f), age),
                Color.black);
            for (int k = 0; k < view.Cubes.Length; k++)
            {
                view.Cubes[k].localScale = Vector3.one * Mathf.Max(0f, 0.12f * (1f - age));
            }
        }

        private CellView CreateCell(FireCellSnapshot cell, int number, float time)
        {
            var root = new GameObject($"Fire cell {number} (read-only presentation)").transform;
            root.SetParent(parent, false);
            root.position = ToUnityPosition(cell.Centre);
            float cellSize = Metres(cell.Bounds.MaxX - cell.Bounds.MinX);

            // A dim glowing floor tile marks the exact square that burns.
            GameObject tile = CreatePrimitive("Scorch", PrimitiveType.Cube, root, Vector3.zero,
                new Vector3(cellSize * 0.96f, 0.02f, cellSize * 0.96f), materials.Fire);
            tile.transform.localPosition = new Vector3(0f, 0.01f, 0f);

            int cubeCount = Hash01(cell.CellX, cell.CellZ, 0) < 0.5f ? 2 : 3;
            var view = new CellView
            {
                Tile = tile.GetComponent<Renderer>(),
                Cubes = new Transform[cubeCount],
                Renderers = new Renderer[cubeCount],
                Offsets = new Vector3[cubeCount],
                Sizes = new float[cubeCount],
                Seeds = new float[cubeCount],
                SpawnTime = time
            };

            float spread = cellSize * 0.28f;
            for (int k = 0; k < cubeCount; k++)
            {
                GameObject cube = CreatePrimitive($"Flame {k + 1}", PrimitiveType.Cube, root, Vector3.zero, Vector3.one, materials.Fire);

                // A fire in the next room glows through the wall, so nobody has
                // to guess why a person with an extinguisher is heading that way.
                ShowFireThroughWalls(cube, materials);
                cube.transform.localPosition = Vector3.zero;
                view.Cubes[k] = cube.transform;
                view.Renderers[k] = cube.GetComponent<Renderer>();
                view.Seeds[k] = Hash01(cell.CellX, cell.CellZ, k + 1);
                view.Sizes[k] = Mathf.Lerp(0.15f, 0.35f, Hash01(cell.CellX, cell.CellZ, k + 11));
                view.Offsets[k] = new Vector3(
                    (Hash01(cell.CellX, cell.CellZ, k + 21) * 2f - 1f) * spread,
                    0f,
                    (Hash01(cell.CellX, cell.CellZ, k + 31) * 2f - 1f) * spread);
            }

            return view;
        }

        private void Animate(CellView view, float time)
        {
            float age = time - view.SpawnTime;
            float pop = age < 0.4f ? EaseOutBack(age / 0.4f) : 1f;
            float ember = Mathf.Clamp01((age - 10f) / 20f);

            Color tileColor = Color.Lerp(new Color(0.55f, 0.1f, 0.02f), EmberRed * 0.6f, ember);
            materials.SetColors(view.Tile, tileColor, tileColor * (0.7f + 0.15f * Mathf.Sin(time * 4f + view.Seeds[0] * 20f)));

            for (int k = 0; k < view.Cubes.Length; k++)
            {
                float seed = view.Seeds[k];
                float flicker = 1f + 0.18f * Mathf.Sin(time * (7f + seed * 6f) + seed * 20f) +
                                0.08f * Mathf.Sin(time * (13f + seed * 9f));
                float size = view.Sizes[k] * pop * flicker * Mathf.Lerp(1f, 0.65f, ember);
                float hover = (0.06f + 0.06f * seed) * (0.5f + 0.5f * Mathf.Sin(time * (3f + seed * 3f) + seed * 10f)) *
                              (1f - 0.7f * ember);

                Transform cube = view.Cubes[k];
                cube.localPosition = view.Offsets[k] + Vector3.up * (size * 0.5f + 0.03f + hover);
                cube.localRotation = Quaternion.Euler(
                    12f * Mathf.Sin(time * 2f + seed * 7f),
                    time * (40f + seed * 80f) + seed * 360f,
                    12f * Mathf.Cos(time * 2.3f + seed * 5f));
                cube.localScale = Vector3.one * Mathf.Max(0.001f, size);

                float heat = 0.5f + 0.5f * Mathf.Sin(time * (5f + seed * 5f) + seed * 30f);
                Color color = Color.Lerp(Color.Lerp(PresentationMaterials.FlameRed, FlameYellow, heat), EmberRed, ember);
                materials.SetColors(view.Renderers[k], color, color * Mathf.Lerp(2.2f, 0.6f, ember));
            }
        }
    }
}
