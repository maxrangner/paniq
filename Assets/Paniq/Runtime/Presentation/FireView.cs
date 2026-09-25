using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Each burning cell as a dim glowing tile plus two or three small cubes
    /// that bob, spin, flicker and fade to embers, under a column of smoke
    /// and a few sparks of ember. Variation comes from a hash of the cell's
    /// grid position, never from the simulation's generator.
    /// <para>
    /// Nothing here is a scene object. Every tile and every flame cube is one
    /// entry in a batch, and each batch is drawn with one call
    /// (<see cref="Graphics.RenderMeshInstanced"/>); the frame cost of a fire
    /// is then a few dozen calls however many squares burn, where it used to
    /// be three or four objects per square, each moved and recoloured every
    /// frame. Colour is shared by everything in a batch, so a flame's colour
    /// is rounded to one of a few dozen steps; the flicker hides the steps.
    /// </para>
    /// </summary>
    internal sealed class FireView
    {
        private static readonly Color FlameYellow = new Color(1f, 0.82f, 0.2f);
        private static readonly Color EmberRed = new Color(0.42f, 0.04f, 0.02f);
        private static readonly Color ScorchRed = new Color(0.55f, 0.1f, 0.02f);
        private static readonly Color WetGrey = new Color(0.12f, 0.13f, 0.16f);

        /// <summary>How finely a flame's heat and age are stepped when it is put in a batch by colour.</summary>
        private const int HeatSteps = 6;
        private const int EmberSteps = 5;

        /// <summary>How many instances one draw call may carry.</summary>
        private const int InstancesPerCall = 1000;

        private sealed class CellView
        {
            public Vector3 Centre;
            public float CellSize;
            public float HalfWidth;
            public float SpawnTime;
            public int CubeCount;
            public readonly Vector3[] Offsets = new Vector3[3];
            public readonly float[] Sizes = new float[3];
            public readonly float[] Seeds = new float[3];

            /// <summary>The leftover fraction of a smoke particle, so the column is even at any frame rate.</summary>
            public float SmokeCarry;

            /// <summary>When this square was hosed down, or -1 while it burns.</summary>
            public float OutSince = -1f;
        }

        /// <summary>What <see cref="Graphics.RenderMeshInstanced"/> wants per instance.</summary>
        private struct Instance
        {
            public Matrix4x4 objectToWorld;
        }

        /// <summary>Everything drawn in one colour this frame.</summary>
        private sealed class Batch
        {
            public Instance[] Items = new Instance[64];
            public int Count;
            public readonly MaterialPropertyBlock Colour = new MaterialPropertyBlock();

            public void Add(in Matrix4x4 transform)
            {
                if (Count == Items.Length)
                {
                    System.Array.Resize(ref Items, Items.Length * 2);
                }

                Items[Count++].objectToWorld = transform;
            }
        }

        private readonly PresentationMaterials materials;
        private readonly ParticleEffects effects;
        private readonly List<CellView> cells = new List<CellView>();
        private readonly Mesh cube;

        /// <summary>Flames by colour step: ember age, then heat.</summary>
        private readonly Batch[] flames = new Batch[EmberSteps * HeatSteps];

        /// <summary>Burning tiles by colour step: ember age, then the slow pulse.</summary>
        private readonly Batch[] tiles = new Batch[EmberSteps * 3];

        /// <summary>Tiles that have been put out, by how far they have cooled.</summary>
        private readonly Batch[] wetTiles = new Batch[4];

        /// <summary>Every flame cube once more, for the orange glow through walls.</summary>
        private readonly Batch throughWalls = new Batch();

        /// <summary>Where the fire is, grown as it spreads, so the batches are never culled by mistake.</summary>
        private Bounds extent;
        private bool hasExtent;

        public FireView(PresentationMaterials materials, ParticleEffects effects, Transform parent)
        {
            this.materials = materials;
            this.effects = effects;
            _ = parent;
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int i = 0; i < flames.Length; i++)
            {
                flames[i] = new Batch();
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = new Batch();
            }

            for (int i = 0; i < wetTiles.Length; i++)
            {
                wetTiles[i] = new Batch();
            }
        }

        /// <summary>How many squares are being drawn, and how many flame cubes: the check that a fire is on screen.</summary>
        public int DrawnCellCount { get; private set; }
        public int DrawnFlameCount { get; private set; }

        public void Update(RunSnapshot snapshot, float time)
        {
            // Burning cells only ever get added, in ignition order.
            while (cells.Count < snapshot.FireCells.Count)
            {
                cells.Add(CreateCell(snapshot.FireCells[cells.Count], time));
            }

            ClearBatches();
            int flameCount = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                // A square that has been put out: the flames drop away and a
                // dark wet patch is left behind.
                if (snapshot.FireCells[i].IsOut)
                {
                    PutOut(cells[i], time);
                    continue;
                }

                flameCount += Animate(cells[i], time);
                effects.BurningFloor(cells[i].Centre, cells[i].HalfWidth, Time.deltaTime, ref cells[i].SmokeCarry);
            }

            DrawnCellCount = cells.Count;
            DrawnFlameCount = flameCount;
            Draw();
        }

        private void ClearBatches()
        {
            for (int i = 0; i < flames.Length; i++)
            {
                flames[i].Count = 0;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i].Count = 0;
            }

            for (int i = 0; i < wetTiles.Length; i++)
            {
                wetTiles[i].Count = 0;
            }

            throughWalls.Count = 0;
        }

        private CellView CreateCell(FireCellSnapshot cell, float time)
        {
            float cellSize = Metres(cell.Bounds.MaxX - cell.Bounds.MinX);
            var view = new CellView
            {
                Centre = ToUnityPosition(cell.Centre),
                CellSize = cellSize,
                HalfWidth = cellSize * 0.4f,
                SpawnTime = time,
                CubeCount = Hash01(cell.CellX, cell.CellZ, 0) < 0.5f ? 2 : 3
            };

            float spread = cellSize * 0.28f;
            for (int k = 0; k < view.CubeCount; k++)
            {
                view.Seeds[k] = Hash01(cell.CellX, cell.CellZ, k + 1);
                view.Sizes[k] = Mathf.Lerp(0.15f, 0.35f, Hash01(cell.CellX, cell.CellZ, k + 11));
                view.Offsets[k] = new Vector3(
                    (Hash01(cell.CellX, cell.CellZ, k + 21) * 2f - 1f) * spread,
                    0f,
                    (Hash01(cell.CellX, cell.CellZ, k + 31) * 2f - 1f) * spread);
            }

            var around = new Bounds(view.Centre, new Vector3(cellSize + 2f, 3f, cellSize + 2f));
            if (hasExtent)
            {
                extent.Encapsulate(around);
            }
            else
            {
                extent = around;
                hasExtent = true;
            }

            return view;
        }

        /// <summary>A square someone has hosed down: cubes gone, a damp scorch mark left.</summary>
        private void PutOut(CellView view, float time)
        {
            if (view.OutSince < 0f)
            {
                view.OutSince = time;
            }

            float age = Mathf.Clamp01((time - view.OutSince) / 0.6f);
            int step = Mathf.Min(wetTiles.Length - 1, (int)(age * wetTiles.Length));
            wetTiles[step].Add(TileTransform(view));

            // The cubes shrink away over the same moment, in their ember colour.
            float size = 0.12f * (1f - age);
            if (size > 0.005f)
            {
                for (int k = 0; k < view.CubeCount; k++)
                {
                    Matrix4x4 transform = Matrix4x4.TRS(
                        view.Centre + view.Offsets[k] + Vector3.up * (size * 0.5f + 0.03f),
                        Quaternion.identity, Vector3.one * size);
                    flames[(EmberSteps - 1) * HeatSteps].Add(transform);
                    throughWalls.Add(transform);
                }
            }
        }

        private static Matrix4x4 TileTransform(CellView view)
        {
            return Matrix4x4.TRS(view.Centre + new Vector3(0f, 0.01f, 0f), Quaternion.identity,
                new Vector3(view.CellSize * 0.96f, 0.02f, view.CellSize * 0.96f));
        }

        /// <summary>Puts a burning square's tile and cubes into this frame's batches; how many cubes it drew.</summary>
        private int Animate(CellView view, float time)
        {
            float age = time - view.SpawnTime;
            float pop = age < 0.4f ? EaseOutBack(age / 0.4f) : 1f;
            float ember = Mathf.Clamp01((age - 10f) / 20f);
            int emberStep = Mathf.Min(EmberSteps - 1, (int)(ember * EmberSteps));

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f + view.Seeds[0] * 20f);
            int pulseStep = Mathf.Min(2, (int)(pulse * 3f));
            tiles[emberStep * 3 + pulseStep].Add(TileTransform(view));

            for (int k = 0; k < view.CubeCount; k++)
            {
                float seed = view.Seeds[k];
                float flicker = 1f + 0.18f * Mathf.Sin(time * (7f + seed * 6f) + seed * 20f) +
                                0.08f * Mathf.Sin(time * (13f + seed * 9f));
                float size = Mathf.Max(0.001f, view.Sizes[k] * pop * flicker * Mathf.Lerp(1f, 0.65f, ember));
                float hover = (0.06f + 0.06f * seed) * (0.5f + 0.5f * Mathf.Sin(time * (3f + seed * 3f) + seed * 10f)) *
                              (1f - 0.7f * ember);

                Matrix4x4 transform = Matrix4x4.TRS(
                    view.Centre + view.Offsets[k] + Vector3.up * (size * 0.5f + 0.03f + hover),
                    Quaternion.Euler(
                        12f * Mathf.Sin(time * 2f + seed * 7f),
                        time * (40f + seed * 80f) + seed * 360f,
                        12f * Mathf.Cos(time * 2.3f + seed * 5f)),
                    Vector3.one * size);

                float heat = 0.5f + 0.5f * Mathf.Sin(time * (5f + seed * 5f) + seed * 30f);
                int heatStep = Mathf.Min(HeatSteps - 1, (int)(heat * HeatSteps));
                flames[emberStep * HeatSteps + heatStep].Add(transform);
                throughWalls.Add(transform);
            }

            return view.CubeCount;
        }

        /// <summary>One draw call per colour in use, plus the glow through walls.</summary>
        private void Draw()
        {
            if (!hasExtent || cube == null || materials.Fire == null)
            {
                return;
            }

            var lit = new RenderParams(materials.Fire)
            {
                worldBounds = extent,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off
            };

            for (int e = 0; e < EmberSteps; e++)
            {
                float ember = (e + 0.5f) / EmberSteps;
                for (int h = 0; h < HeatSteps; h++)
                {
                    float heat = (h + 0.5f) / HeatSteps;
                    Color color = Color.Lerp(Color.Lerp(PresentationMaterials.FlameRed, FlameYellow, heat), EmberRed, ember);
                    DrawBatch(lit, flames[e * HeatSteps + h], color, color * Mathf.Lerp(2.2f, 0.6f, ember));
                }

                Color tileColor = Color.Lerp(ScorchRed, EmberRed * 0.6f, ember);
                for (int p = 0; p < 3; p++)
                {
                    float pulse = (p + 0.5f) / 3f;
                    DrawBatch(lit, tiles[e * 3 + p], tileColor, tileColor * (0.7f + 0.15f * (pulse * 2f - 1f)));
                }
            }

            for (int w = 0; w < wetTiles.Length; w++)
            {
                float cooled = (w + 0.5f) / wetTiles.Length;
                DrawBatch(lit, wetTiles[w], Color.Lerp(ScorchRed, WetGrey, cooled), Color.black);
            }

            // A fire in the next room glows through the wall, so nobody has
            // to guess why a person with an extinguisher is heading that way.
            if (materials.FireSeeThrough != null && throughWalls.Count > 0)
            {
                var glow = new RenderParams(materials.FireSeeThrough)
                {
                    worldBounds = extent,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off
                };
                DrawInstances(glow, throughWalls);
            }
        }

        private void DrawBatch(RenderParams lit, Batch batch, Color baseColor, Color emission)
        {
            if (batch.Count == 0)
            {
                return;
            }

            materials.FillColors(batch.Colour, baseColor, emission);
            lit.matProps = batch.Colour;
            DrawInstances(lit, batch);
        }

        private void DrawInstances(RenderParams parameters, Batch batch)
        {
            for (int start = 0; start < batch.Count; start += InstancesPerCall)
            {
                Graphics.RenderMeshInstanced(parameters, cube, 0, batch.Items,
                    Mathf.Min(InstancesPerCall, batch.Count - start), start);
            }
        }
    }
}
