using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The little green signs along the corridor that point the way out.
    /// <para>
    /// They are for the player's eye and nobody else's: people in the building
    /// find their own way out by the navigation grid, exactly as they always
    /// have, and would do so if every sign were taken down. What the signs fix
    /// is that the player, looking at a corridor that runs the length of the
    /// building and Ts at one end, has no way of telling which arm the door is
    /// up until they have driven the camera down there to look.
    /// </para>
    /// <para>
    /// Drawn standing upright, a little above head height, the way a sign hangs
    /// on a wall, with the arrow on both faces. They were laid flat for a while
    /// so the camera, which looks down on the building, could read them from
    /// any angle; the owner asked for them upright (2026-09-24), so that they
    /// read as signs the people in the building are looking at, and accepted
    /// that from some camera angles a sign is seen edge-on.
    /// </para>
    /// </summary>
    internal sealed class ExitSignView
    {
        /// <summary>
        /// How high the signs float, in metres: above head height, and clear
        /// enough of the 1.5 m wall tops to read as signs hanging in the room
        /// rather than as panels set into the ceiling.
        /// </summary>
        private const float Height = 1.2f;

        private static readonly Color SignGreen = new Color(0.10f, 0.52f, 0.20f);
        private static readonly Color ArrowWhite = new Color(0.95f, 0.98f, 0.95f);

        public ExitSignView(ExitSignDefinition[] signs, PresentationMaterials materials, Transform parent)
        {
            if (signs == null || signs.Length == 0)
            {
                return;
            }

            var root = new GameObject("Exit signs (presentation)").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < signs.Length; i++)
            {
                Build(signs[i], i, materials, root);
            }
        }

        private static void Build(ExitSignDefinition sign, int index, PresentationMaterials materials,
            Transform root)
        {
            var at = new GameObject($"Exit sign {index + 1}").transform;
            at.SetParent(root, false);

            // Standing upright, turned so its local +X points the way out: the
            // plate then lies along the way it points, like a sign on the wall
            // beside a corridor. The heading is a compass bearing clockwise
            // from north, which is how every other direction in the run is
            // written, and the quarter turn is what lines +X up with it.
            at.SetPositionAndRotation(
                ToUnityPosition(sign.At) + Vector3.up * Height,
                Quaternion.Euler(0f, sign.PointingDegrees - 90f, 0f));

            // A thin slab rather than a flat quad, so that seen edge-on it is
            // still a sliver of green rather than nothing at all.
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            Object.Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(at, false);
            plate.transform.localScale = new Vector3(0.9f, 0.34f, PlateThickness);
            Renderer plateRenderer = plate.GetComponent<Renderer>();
            plateRenderer.sharedMaterial = materials.Icon;
            plateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plateRenderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", SignGreen);
            plateRenderer.SetPropertyBlock(block);

            // A chevron and a shaft, pointing along the sign's local +X --
            // which the rotation above has aimed at the way out -- on both
            // faces of the plate, since the sign can be looked at from
            // either side and an arrow pointing east points east from both.
            BuildArrow(at, PlateThickness * 0.5f + ArrowStandOff, materials);
            BuildArrow(at, -(PlateThickness * 0.5f + ArrowStandOff), materials);
        }

        /// <summary>How thick the plate is, in metres.</summary>
        private const float PlateThickness = 0.02f;

        /// <summary>How far off each face the arrow floats, in metres, so the plate does not draw over it.</summary>
        private const float ArrowStandOff = 0.005f;

        private static void BuildArrow(Transform at, float offset, PresentationMaterials materials)
        {
            var arrow = new GameObject(offset > 0f ? "Arrow" : "Arrow (back)");
            arrow.transform.SetParent(at, false);
            arrow.transform.localPosition = new Vector3(0f, 0f, offset);
            LineRenderer line = arrow.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.sharedMaterial = materials.Icon;
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = ArrowWhite;
            line.endColor = ArrowWhite;
            line.positionCount = 5;
            line.SetPositions(new[]
            {
                new Vector3(-0.34f, 0f, 0f),
                new Vector3(0.32f, 0f, 0f),
                new Vector3(0.16f, 0.11f, 0f),
                new Vector3(0.32f, 0f, 0f),
                new Vector3(0.16f, -0.11f, 0f)
            });
        }
    }
}
