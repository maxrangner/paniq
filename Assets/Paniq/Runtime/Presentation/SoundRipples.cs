using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>A ring that grows across the floor to a noise's reach and fades over half a second. Rings are reused.</summary>
    internal sealed class SoundRipples
    {
        private const float Duration = 0.55f;

        public static readonly Color YellColor = new Color(0.4f, 0.92f, 1f, 0.55f);
        public static readonly Color ThudColor = new Color(1f, 0.85f, 0.6f, 0.6f);

        private sealed class Ripple
        {
            public LineRenderer Line;
            public Vector3 Centre;
            public float Radius;
            public float StartTime;
            public Color Color;
        }

        private readonly Material material;
        private readonly Transform parent;
        private readonly List<Ripple> ripples = new List<Ripple>();

        public SoundRipples(Material material, Transform parent)
        {
            this.material = material;
            this.parent = parent;
        }

        public void Start(LogicalPosition position, int radiusMillimetres, Color color, float time)
        {
            if (radiusMillimetres <= 0)
            {
                return;
            }

            Ripple ripple = null;
            for (int i = 0; i < ripples.Count; i++)
            {
                if (!ripples[i].Line.enabled)
                {
                    ripple = ripples[i];
                    break;
                }
            }

            if (ripple == null)
            {
                var rippleObject = new GameObject($"Sound ripple {ripples.Count + 1} (presentation)");
                rippleObject.transform.SetParent(parent, false);
                rippleObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                LineRenderer line = rippleObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.alignment = LineAlignment.TransformZ;
                line.positionCount = 48;
                line.sharedMaterial = material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                ripple = new Ripple { Line = line };
                ripples.Add(ripple);
            }

            ripple.Centre = ToUnityPosition(position) + Vector3.up * 0.04f;
            ripple.Radius = Metres(radiusMillimetres);
            ripple.StartTime = time;
            ripple.Color = color;
            ripple.Line.enabled = true;
        }

        public void Update(float time)
        {
            for (int r = 0; r < ripples.Count; r++)
            {
                Ripple ripple = ripples[r];
                if (!ripple.Line.enabled)
                {
                    continue;
                }

                float t = (time - ripple.StartTime) / Duration;
                if (t >= 1f)
                {
                    ripple.Line.enabled = false;
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Max(0.05f, ripple.Radius * eased);
                LineRenderer line = ripple.Line;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i / (float)line.positionCount * Mathf.PI * 2f;
                    line.SetPosition(i, ripple.Centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }

                Color color = ripple.Color;
                color.a *= 1f - t;
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Lerp(0.06f, 0.02f, t);
            }
        }
    }
}
