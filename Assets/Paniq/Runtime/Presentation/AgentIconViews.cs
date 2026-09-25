using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The icons floating over one person's head: a red "!" that pops up
    /// when they notice something, three sound-wave arcs when they yell, a
    /// snowflake while they are frozen with fear, little yellow stars
    /// circling while they are knocked out cold, a "?" while they turn to
    /// see what a noise was, "..." while idling, and the person's number
    /// (matching the Tab stats panel). Icons live on their own
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

        /// <summary>The star over somebody other people are following.</summary>
        private static readonly Color LeaderGreen = new Color(0.45f, 0.95f, 0.5f);
        private static readonly Color IdleGrey = new Color(0.72f, 0.82f, 0.95f);
        private static readonly Color StarYellow = new Color(1f, 0.9f, 0.2f);
        private static readonly Color NumberWhite = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>The scribble over somebody annoyed at being poked (prototype 3).</summary>
        private static readonly Color AnnoyedOrange = new Color(1f, 0.5f, 0.15f);
        private const float AnnoyedDuration = 1.6f;

        private readonly Transform root;
        private readonly Transform notice;
        private readonly LineRenderer[] noticeStrokes;
        private readonly Transform yell;
        private readonly LineRenderer[] yellArcs;
        private readonly Transform snowflake;
        private readonly LineRenderer[] snowflakeStrokes;
        private readonly Transform[] stars;
        private readonly LineRenderer[] starStrokes;
        private readonly TextMesh question;
        private readonly TextMesh annoyed;
        private readonly TextMesh idle;
        private readonly TextMesh number;
        private readonly float spinOffset;

        private float noticeTime = float.NegativeInfinity;
        private float yellTime = float.NegativeInfinity;
        private float annoyedTime = float.NegativeInfinity;
        private float frozenSince = float.NegativeInfinity;
        private bool wasFrozen;

        public AgentIconViews(string name, string numberLabel, Material lineMaterial, float spinOffset, Transform parent)
        {
            this.spinOffset = spinOffset;
            root = new GameObject($"{name} icons (presentation)").transform;
            root.SetParent(parent, false);

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

            // Three little five-pointed stars that circle the head while knocked out.
            stars = new Transform[3];
            starStrokes = new LineRenderer[3];
            for (int k = 0; k < stars.Length; k++)
            {
                stars[k] = CreateGroup($"Star {k + 1}", Vector3.zero);
                starStrokes[k] = CreateStroke(stars[k], lineMaterial, 0.022f, 0.022f, 0, StarPoints(0.075f, 0.032f));
                starStrokes[k].loop = true;
            }

            SetColor(starStrokes, StarYellow);

            question = CreateText("Investigating ?", "?", 0.2f, 64, QuestionYellow, new Vector3(0f, 0.2f, 0f));
            annoyed = CreateText("Annoyed #!", "#!", 0.16f, 64, AnnoyedOrange, new Vector3(0f, 0.2f, 0f));
            idle = CreateText("Idle ...", "...", 0.13f, 48, IdleGrey, new Vector3(0f, -0.05f, 0f));
            number = CreateText("Number", numberLabel, 0.07f, 64, NumberWhite, new Vector3(0.32f, -0.28f, 0f));
            // A green star over whoever is being followed. It used to be an
            // arrow, and the people following them wore the same arrow a size
            // smaller, so at a glance a leader and their followers looked
            // exactly alike. Only the leader is marked now.
            leading = CreateGroup("Leading", new Vector3(0f, 0.32f, 0f));
            leadingStroke = CreateStroke(leading, lineMaterial, 0.03f, 0.03f, 0, StarPoints(0.11f, 0.046f));
            leadingStroke.loop = true;
            SetColor(new[] { leadingStroke }, LeaderGreen);

            SetColor(noticeStrokes, NoticeRed);
            SetColor(snowflakeStrokes, IceBlue);
            HideAll();
        }

        /// <summary>The person just noticed something: pop the red "!".</summary>
        public void Notice(float time) => noticeTime = time;

        /// <summary>The person just yelled: play the sound-wave arcs.</summary>
        public void Yell(float time) => yellTime = time;

        /// <summary>The person is annoyed at being poked: an orange scribble, shaking.</summary>
        public void Annoyed(float time) => annoyedTime = time;

        public void HideAll()
        {
            notice.gameObject.SetActive(false);
            yell.gameObject.SetActive(false);
            snowflake.gameObject.SetActive(false);
            SetActive(stars, false);
            question.gameObject.SetActive(false);
            annoyed.gameObject.SetActive(false);
            leading.gameObject.SetActive(false);
            idle.gameObject.SetActive(false);
            number.gameObject.SetActive(false);
        }

        /// <param name="facingSide">+1 when the person faces screen-right, -1 for screen-left.</param>
        public void Update(
            Vector3 anchor,
            Quaternion cameraRotation,
            float facingSide,
            bool frozen,
            bool knockedOut,
            bool investigating,
            bool idling,
            bool leadingOthers,
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

            number.gameObject.SetActive(true);
            // Stars chase each other round a flattened circle, as if orbiting the head.
            SetActive(stars, knockedOut);
            if (knockedOut)
            {
                for (int k = 0; k < stars.Length; k++)
                {
                    float angle = time * 3.2f + spinOffset + k * (Mathf.PI * 2f / stars.Length);
                    float depth = Mathf.Sin(angle);
                    stars[k].localPosition = new Vector3(Mathf.Cos(angle) * 0.3f, 0.02f + depth * 0.07f, 0f);
                    stars[k].localScale = Vector3.one * (0.85f + 0.25f * depth);
                    stars[k].localRotation = Quaternion.Euler(0f, 0f, time * 140f + k * 40f);
                }
            }

            // Annoyed: an orange scribble that shakes and fades, over
            // everything but the "!".
            float annoyedAge = time - annoyedTime;
            bool showAnnoyed = annoyedAge >= 0f && annoyedAge < AnnoyedDuration && !showNotice;
            annoyed.gameObject.SetActive(showAnnoyed);
            if (showAnnoyed)
            {
                float scale = annoyedAge < NoticePopTime ? EaseOutBack(annoyedAge / NoticePopTime) : 1f;
                annoyed.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                annoyed.transform.localPosition = new Vector3(0.03f * Mathf.Sin(annoyedAge * 40f), 0.2f, 0f);
                annoyed.color = WithAlpha(AnnoyedOrange, Fade(annoyedAge, AnnoyedDuration, NoticeFadeTime));
            }

            question.gameObject.SetActive(investigating && !showNotice && !showAnnoyed);

            // A leader's call: a star over the head, bobbing as they shout.
            // Whoever is trailing after them wears nothing at all, so the one
            // mark in a knot of people is the one worth looking at.
            leading.gameObject.SetActive(leadingOthers);
            if (leadingOthers)
            {
                leading.localPosition = new Vector3(0f, 0.32f + 0.03f * Mathf.Sin(time * 7f), 0f);
                leading.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.2f + spinOffset) * 8f);
            }

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

        private readonly Transform leading;
        private readonly LineRenderer leadingStroke;

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

        /// <summary>The ten corners of a five-pointed star, pointing up.</summary>
        private static Vector3[] StarPoints(float outer, float inner)
        {
            var points = new Vector3[10];
            for (int i = 0; i < points.Length; i++)
            {
                float radius = i % 2 == 0 ? outer : inner;
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            return points;
        }

        private static void SetActive(Transform[] groups, bool active)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].gameObject.SetActive(active);
            }
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
