using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The icons floating over one person's head: a red "!" that pops up
    /// when they notice something, three sound-wave arcs when they yell, a
    /// snowflake while they are frozen with fear, a "?" while they turn to
    /// see what a noise was, and "..." while idling. Icons live on their own
    /// anchor that always faces the camera, so they never spin with the body
    /// or tip over when it falls. Presentation only.
    /// </summary>
    internal sealed class AgentIconViews
    {
        private const float NoticeDuration = 1.3f;
        private const float NoticePopTime = 0.18f;
        private const float NoticeFadeTime = 0.3f;
        private const float YellDuration = 0.9f;
        private const float YellArcStagger = 0.1f;
        private const float YellFadeTime = 0.3f;
        private const float SnowflakePopTime = 0.25f;

        private static readonly Color NoticeRed = new Color(1f, 0.1f, 0.08f);
        private static readonly Color YellCyan = new Color(0.4f, 0.92f, 1f);
        private static readonly Color IceBlue = new Color(0.7f, 0.93f, 1f);
        private static readonly Color QuestionYellow = new Color(1f, 0.88f, 0.25f);
        private static readonly Color IdleGrey = new Color(0.72f, 0.82f, 0.95f);

        private readonly Transform root;
        private readonly Transform notice;
        private readonly LineRenderer[] noticeStrokes;
        private readonly Transform yell;
        private readonly LineRenderer[] yellArcs;
        private readonly Transform snowflake;
        private readonly LineRenderer[] snowflakeStrokes;
        private readonly TextMesh question;
        private readonly TextMesh idle;
        private readonly float spinOffset;

        private float noticeTime = float.NegativeInfinity;
        private float yellTime = float.NegativeInfinity;
        private float frozenSince = float.NegativeInfinity;
        private bool wasFrozen;

        public AgentIconViews(string name, Material lineMaterial, float spinOffset)
        {
            this.spinOffset = spinOffset;
            root = new GameObject($"{name} icons (presentation)").transform;

            notice = CreateGroup("Notice !", new Vector3(0f, 0.05f, 0f));
            noticeStrokes = new[]
            {
                // A tapered bar and a round dot.
                CreateStroke(notice, lineMaterial, 0.15f, 0.09f, 4, new Vector3(0f, 0.52f, 0f), new Vector3(0f, 0.14f, 0f)),
                CreateStroke(notice, lineMaterial, 0.13f, 0.13f, 8, new Vector3(0f, 0.005f, 0f), new Vector3(0f, -0.005f, 0f))
            };

            yell = CreateGroup("Yell arcs", Vector3.zero);
            yellArcs = new LineRenderer[3];
            for (int k = 0; k < yellArcs.Length; k++)
            {
                yellArcs[k] = CreateStroke(yell, lineMaterial, 0.045f, 0.045f, 3, ArcPoints(0.12f + 0.1f * k, 70f, 9));
            }

            snowflake = CreateGroup("Snowflake", new Vector3(0f, 0.2f, 0f));
            snowflakeStrokes = new LineRenderer[9];
            const float armLength = 0.24f;
            for (int k = 0; k < 3; k++)
            {
                Vector3 axis = Quaternion.Euler(0f, 0f, 90f + 60f * k) * Vector3.right;
                snowflakeStrokes[k] = CreateStroke(snowflake, lineMaterial, 0.035f, 0.035f, 2,
                    axis * armLength, -axis * armLength);
            }

            for (int arm = 0; arm < 6; arm++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, 90f + 60f * arm);
                Vector3 along = turn * Vector3.right;
                Vector3 fork = along * (armLength * 0.6f);
                Vector3 left = fork + turn * (Quaternion.Euler(0f, 0f, 45f) * Vector3.right) * 0.08f;
                Vector3 right = fork + turn * (Quaternion.Euler(0f, 0f, -45f) * Vector3.right) * 0.08f;
                snowflakeStrokes[3 + arm] = CreateStroke(snowflake, lineMaterial, 0.028f, 0.028f, 2, left, fork, right);
            }

            question = CreateText("Investigating ?", "?", 0.2f, 64, QuestionYellow, new Vector3(0f, 0.2f, 0f));
            idle = CreateText("Idle ...", "...", 0.13f, 48, IdleGrey, new Vector3(0f, -0.05f, 0f));

            SetColor(noticeStrokes, NoticeRed);
            SetColor(snowflakeStrokes, IceBlue);
            HideAll();
        }

        /// <summary>The person just noticed something: pop the red "!".</summary>
        public void Notice(float time) => noticeTime = time;

        /// <summary>The person just yelled: play the sound-wave arcs.</summary>
        public void Yell(float time) => yellTime = time;

        public void HideAll()
        {
            notice.gameObject.SetActive(false);
            yell.gameObject.SetActive(false);
            snowflake.gameObject.SetActive(false);
            question.gameObject.SetActive(false);
            idle.gameObject.SetActive(false);
        }

        /// <param name="facingSide">+1 when the person faces screen-right, -1 for screen-left.</param>
        public void Update(
            Vector3 anchor,
            Quaternion cameraRotation,
            float facingSide,
            bool frozen,
            bool investigating,
            bool idling,
            float time)
        {
            root.SetPositionAndRotation(anchor, cameraRotation);

            // "!" pops in with an overshoot, holds, then fades.
            float noticeAge = time - noticeTime;
            bool showNotice = noticeAge >= 0f && noticeAge < NoticeDuration;
            notice.gameObject.SetActive(showNotice);
            if (showNotice)
            {
                float scale = noticeAge < NoticePopTime ? EaseOutBack(noticeAge / NoticePopTime) : 1f;
                notice.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                notice.localPosition = new Vector3(0f, 0.05f + 0.04f * Mathf.Sin(noticeAge * 9f), 0f);
                SetColor(noticeStrokes, WithAlpha(NoticeRed, Fade(noticeAge, NoticeDuration, NoticeFadeTime)));
            }

            // Three arcs appear from the inside out, beside the head on the side the person faces.
            float yellAge = time - yellTime;
            bool showYell = yellAge >= 0f && yellAge < YellDuration;
            yell.gameObject.SetActive(showYell);
            if (showYell)
            {
                float side = facingSide < 0f ? -1f : 1f;
                yell.localPosition = new Vector3(0.2f * side, 0.12f, 0f);
                yell.localScale = new Vector3(side, 1f, 1f);
                float fade = Fade(yellAge, YellDuration, YellFadeTime);
                for (int k = 0; k < yellArcs.Length; k++)
                {
                    float arcAge = yellAge - k * YellArcStagger;
                    yellArcs[k].enabled = arcAge >= 0f;
                    float pulse = 1f + 0.12f * Mathf.Sin(arcAge * 20f);
                    yellArcs[k].widthMultiplier = pulse;
                    SetColor(yellArcs[k], WithAlpha(YellCyan, fade));
                }
            }

            // Snowflake: pops in when the freeze starts and turns slowly.
            if (frozen && !wasFrozen)
            {
                frozenSince = time;
            }

            wasFrozen = frozen;
            snowflake.gameObject.SetActive(frozen && !showNotice);
            if (snowflake.gameObject.activeSelf)
            {
                float age = time - frozenSince;
                float scale = age < SnowflakePopTime ? EaseOutBack(age / SnowflakePopTime) : 1f;
                snowflake.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                snowflake.localRotation = Quaternion.Euler(0f, 0f, time * 25f + spinOffset);
            }

            question.gameObject.SetActive(investigating && !showNotice);
            idle.gameObject.SetActive(idling && !showNotice && !showYell);
        }

        private GameObject CreateGroupObject(string groupName)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(root, false);
            return group;
        }

        private Transform CreateGroup(string groupName, Vector3 localPosition)
        {
            Transform group = CreateGroupObject(groupName).transform;
            group.localPosition = localPosition;
            return group;
        }

        private static LineRenderer CreateStroke(
            Transform parent,
            Material material,
            float startWidth,
            float endWidth,
            int capVertices,
            params Vector3[] points)
        {
            var strokeObject = new GameObject("Stroke");
            strokeObject.transform.SetParent(parent, false);
            LineRenderer line = strokeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.sharedMaterial = material;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = capVertices;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }

        private TextMesh CreateText(string objectName, string text, float characterSize, int fontSize, Color color,
            Vector3 localPosition)
        {
            GameObject textObject = CreateGroupObject(objectName);
            textObject.transform.localPosition = localPosition;
            TextMesh label = textObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                label.font = font;
                textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.characterSize = characterSize;
            label.fontSize = fontSize;
            label.color = color;
            return label;
        }

        /// <summary>Points of an arc opening toward +X, centred on the local origin.</summary>
        private static Vector3[] ArcPoints(float radius, float spanDegrees, int count)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-spanDegrees * 0.5f, spanDegrees * 0.5f, i / (count - 1f)) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            return points;
        }

        private static float Fade(float age, float duration, float fadeTime)
        {
            return Mathf.Clamp01((duration - age) / fadeTime);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void SetColor(LineRenderer[] lines, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                SetColor(lines[i], color);
            }
        }

        private static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        internal static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
