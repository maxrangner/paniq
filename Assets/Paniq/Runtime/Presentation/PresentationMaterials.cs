using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// Every material the prototype display uses, created once and shared.
    /// Per-object colours go through one <see cref="MaterialPropertyBlock"/>
    /// instead of material copies. <see cref="Destroy"/> releases them all
    /// when the display goes away, so reloading the scene does not pile up
    /// materials.
    /// </summary>
    internal sealed class PresentationMaterials
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

        public static readonly Color LockedDoorColor = new Color(0.86f, 0.14f, 0.1f);
        public static readonly Color BoxColor = new Color(0.62f, 0.45f, 0.26f);
        public static readonly Color WoodColor = new Color(0.45f, 0.29f, 0.17f);
        public static readonly Color FlameRed = new Color(1f, 0.16f, 0.02f);

        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        public PresentationMaterials()
        {
            Room = CreateLit(new Color(0.12f, 0.14f, 0.18f));
            Wall = CreateLit(new Color(0.22f, 0.24f, 0.3f));
            Vision = CreateLit(new Color(0.25f, 0.7f, 1f));
            Agent = CreateLit(new Color(0.78f, 0.84f, 0.9f));

            // Unlit and coloured per vertex, so icons stay bright and can fade.
            Shader iconShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Icon = new Material(iconShader);
            Door = CreateLit(LockedDoorColor);
            Door.EnableKeyword("_EMISSION");
            Box = CreateLit(BoxColor);
            Outside = CreateLit(new Color(0.2f, 0.22f, 0.2f));
            Fire = CreateLit(FlameRed);
            Fire.EnableKeyword("_EMISSION");
            Fire.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Fire.SetColor(EmissionColorId, FlameRed);

            // The pale silhouette drawn wherever a wall is in the way. Its
            // shader lives in Content/Rendering; without it, things behind a
            // wall are simply hidden.
            Shader seeThrough = Shader.Find("Paniq/See-Through");
            if (seeThrough != null)
            {
                SeeThrough = new Material(seeThrough);
                SeeThrough.SetColor(GhostColorId, new Color(0.62f, 0.78f, 0.98f, 0.32f));

                // Fire behind a wall shows as an orange glow rather than the
                // pale blue of people and furniture, so a blaze in the next
                // room is unmistakable.
                FireSeeThrough = new Material(seeThrough);
                FireSeeThrough.SetColor(GhostColorId, new Color(1f, 0.45f, 0.1f, 0.45f));
            }
            else
            {
                Debug.LogWarning("Paniq: the See-Through shader is missing, so nothing will show through walls.");
            }

            // The invisible mark walls and doors leave, which tells the
            // silhouette where it may draw. Without it, nothing shows through.
            Shader wallMark = Shader.Find("Paniq/Wall Mark");
            if (wallMark != null)
            {
                WallMark = new Material(wallMark);
            }
            else
            {
                Debug.LogWarning("Paniq: the Wall Mark shader is missing, so nothing will show through walls.");
            }
        }

        /// <summary>The silhouette material, or null when its shader is missing.</summary>
        public Material SeeThrough { get; }

        /// <summary>The invisible mark on walls and doors that the silhouette draws through, or null when its shader is missing.</summary>
        public Material WallMark { get; }

        /// <summary>The orange silhouette of flames behind a wall, or null when its shader is missing.</summary>
        public Material FireSeeThrough { get; }

        public Material Room { get; }
        public Material Wall { get; }
        public Material Vision { get; }
        public Material Agent { get; }
        public Material Icon { get; }
        public Material Door { get; }
        public Material Box { get; }
        public Material Outside { get; }
        public Material Fire { get; }

        /// <summary>Recolours one renderer without copying its material.</summary>
        public void SetColor(Renderer target, Color color)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            target.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Recolours one emissive renderer without copying its material.</summary>
        public void SetColors(Renderer target, Color baseColor, Color emission)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(ColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            target.SetPropertyBlock(propertyBlock);
        }

        public void Destroy()
        {
            foreach (Material material in new[] { Room, Wall, Vision, Agent, Icon, Door, Box, Outside, Fire, SeeThrough, WallMark, FireSeeThrough })
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }
        }

        private static Material CreateLit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }
    }
}
