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
    /// Drawn lying flat, a little above head height, rather than upright on the
    /// wall. The view looks down on the building, so a flat sign reads at a
    /// glance from any angle the camera can be swung to, and an upright one
    /// would be edge-on half the time.
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

            // Laid flat with its face upward, then turned so its local +X
            // points the way out. The heading is a compass bearing clockwise
            // from north, which is how every other direction in the run is
            // written, and the extra quarter turn is what lines +X up with it.
            at.SetPositionAndRotation(
                ToUnityPosition(sign.At) + Vector3.up * Height,
                Quaternion.Euler(-90f, sign.PointingDegrees - 90f, 0f));

            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Plate";
            Object.Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(at, false);
            plate.transform.localScale = new Vector3(0.9f, 0.34f, 1f);
            Renderer plateRenderer = plate.GetComponent<Renderer>();
            plateRenderer.sharedMaterial = materials.Icon;
            plateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plateRenderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", SignGreen);
            plateRenderer.SetPropertyBlock(block);

            // A chevron and a shaft, pointing along the sign's local +X --
            // which the rotation above has aimed at the way out. The sign is
            // laid flat, so its local +Z is world up: the arrow goes on that
            // side to sit on top of the plate. It used to be at -0.02, which
            // hung it underneath, and since plate and arrow share a
            // transparent material the plate simply drew over it -- every sign
            // read as a blank green rectangle.
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(at, false);
            arrow.transform.localPosition = new Vector3(0f, 0f, 0.02f);
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
