using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The cable running socket to socket and back to the fuse box, and the
    /// spark crawling along it once something sets it off.
    /// <para>
    /// The cable is drawn through the same corners the run measures its length
    /// from, and the spark is put at the same fraction along it that the run
    /// says it has reached. Neither can drift from the other, because there is
    /// one route and one number.
    /// </para>
    /// <para>
    /// Presentation only: it reads the cable and decides nothing.
    /// </para>
    /// </summary>
    internal sealed class PowerCableView
    {
        /// <summary>How high up the wall the cable runs, in metres.</summary>
        private const float CableHeight = 1.15f;

        /// <summary>How many sparks may be drawn at once. The building has three runs of cable.</summary>
        private const int MostSparksAtOnce = 6;

        private static readonly Color CableColour = new Color(0.16f, 0.15f, 0.17f);
        private static readonly Color SparkColour = new Color(1f, 0.92f, 0.45f);
        private static readonly Color TailColour = new Color(1f, 0.45f, 0.08f);

        private readonly FireReactionPowerLineDefinition[] lines;
        private readonly Transform[] sparks;
        private readonly LineRenderer[] tails;

        public PowerCableView(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            lines = scenario.PowerLines ?? System.Array.Empty<FireReactionPowerLineDefinition>();

            var root = new GameObject("Power cable (presentation)").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < lines.Length; i++)
            {
                LogicalPosition[] corners = lines[i].Corners;
                var runObject = new GameObject($"Cable {i + 1}");
                runObject.transform.SetParent(root, false);
                LineRenderer cable = runObject.AddComponent<LineRenderer>();
                cable.useWorldSpace = true;
                cable.sharedMaterial = materials.Icon;
                cable.widthMultiplier = 0.035f;
                cable.numCornerVertices = 2;
                cable.numCapVertices = 2;
                cable.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cable.receiveShadows = false;
                cable.startColor = CableColour;
                cable.endColor = CableColour;
                cable.positionCount = corners.Length;
                for (int c = 0; c < corners.Length; c++)
                {
                    cable.SetPosition(c, ToUnityPosition(corners[c]) + Vector3.up * CableHeight);
                }
            }

            // A small pool of sparks, made once and moved about, so a cascade
            // never allocates in the middle of a round.
            sparks = new Transform[MostSparksAtOnce];
            tails = new LineRenderer[MostSparksAtOnce];
            for (int i = 0; i < sparks.Length; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = $"Spark {i + 1}";
                Object.Destroy(spark.GetComponent<Collider>());
                spark.transform.SetParent(root, false);
                spark.transform.localScale = Vector3.one * 0.14f;
                Renderer renderer = spark.GetComponent<Renderer>();
                renderer.sharedMaterial = materials.Fire;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", SparkColour);
                block.SetColor("_EmissionColor", SparkColour * 3f);
                renderer.SetPropertyBlock(block);
                sparks[i] = spark.transform;

                var tailObject = new GameObject($"Spark tail {i + 1}");
                tailObject.transform.SetParent(root, false);
                LineRenderer tail = tailObject.AddComponent<LineRenderer>();
                tail.useWorldSpace = true;
                tail.sharedMaterial = materials.Icon;
                tail.widthMultiplier = 0.07f;
                tail.numCapVertices = 2;
                tail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tail.receiveShadows = false;
                tail.startColor = SparkColour;
                tail.endColor = new Color(TailColour.r, TailColour.g, TailColour.b, 0f);
                tail.positionCount = 2;
                tails[i] = tail;

                spark.SetActive(false);
                tailObject.SetActive(false);
            }
        }

        /// <summary>One frame: every live spark put where the run says it has got to.</summary>
        public void Update(FireReactionSnapshot snapshot, float time)
        {
            int shown = 0;
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.PowerSparks.Count && shown < sparks.Length; i++)
                {
                    FireReactionPowerSparkSnapshot spark = snapshot.PowerSparks[i];
                    if (spark.LineIndex < 0 || spark.LineIndex >= lines.Length)
                    {
                        continue;
                    }

                    Vector3 at = PointAlong(lines[spark.LineIndex], spark.TravelledMillimetres, spark.RunsForward);
                    Vector3 behind = PointAlong(lines[spark.LineIndex],
                        Mathf.Max(0, spark.TravelledMillimetres - 450), spark.RunsForward);

                    sparks[shown].gameObject.SetActive(true);
                    sparks[shown].position = at;

                    // A flicker, because a fuse does not burn evenly.
                    sparks[shown].localScale = Vector3.one * (0.12f + 0.04f * Mathf.Sin(time * 37f + shown));

                    tails[shown].gameObject.SetActive(true);
                    tails[shown].SetPosition(0, behind);
                    tails[shown].SetPosition(1, at);
                    shown++;
                }
            }

            for (int i = shown; i < sparks.Length; i++)
            {
                if (sparks[i].gameObject.activeSelf)
                {
                    sparks[i].gameObject.SetActive(false);
                    tails[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Where along a run of cable a spark that has crawled this far is.
        /// Walked leg by leg, the same way the run's length is added up, so the
        /// drawn spark sits exactly where the rules put it.
        /// </summary>
        private static Vector3 PointAlong(FireReactionPowerLineDefinition line, int travelled, bool forward)
        {
            LogicalPosition[] corners = line.Corners;
            int count = corners.Length;
            int left = Mathf.Max(0, travelled);
            for (int leg = 1; leg < count; leg++)
            {
                LogicalPosition from = forward ? corners[leg - 1] : corners[count - leg];
                LogicalPosition to = forward ? corners[leg] : corners[count - leg - 1];
                int length = Mathf.Abs(to.X - from.X) + Mathf.Abs(to.Z - from.Z);
                if (left > length && leg < count - 1)
                {
                    left -= length;
                    continue;
                }

                float part = length <= 0 ? 1f : Mathf.Clamp01(left / (float)length);
                Vector3 start = ToUnityPosition(from) + Vector3.up * CableHeight;
                Vector3 end = ToUnityPosition(to) + Vector3.up * CableHeight;
                return Vector3.Lerp(start, end, part);
            }

            return ToUnityPosition(corners[forward ? count - 1 : 0]) + Vector3.up * CableHeight;
        }
    }
}
