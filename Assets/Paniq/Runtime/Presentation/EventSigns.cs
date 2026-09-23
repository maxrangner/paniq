using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Little cardboard signs that pop up beside somebody the moment something
    /// happens to them -- "knocked out!", "on fire!", "no way through!" -- with
    /// a short arrow pointing at whatever caused it.
    /// <para>
    /// The round already keeps a full account of itself, and the end card can
    /// read the whole thing back, but that is afterwards. By the time the
    /// player reads that somebody went down in the doorway they have long since
    /// stopped being able to do anything about it. A sign says it while it is
    /// happening, at the place it is happening, and gets out of the way again.
    /// </para>
    /// <para>
    /// Deliberately not the log's wording. The log writes sentences for reading
    /// at rest; a sign is two or three words at a glance while the room is on
    /// fire. What the two do share is the number over a person's head, so the
    /// "3" on a sign and the "person 3" in the log are the same person.
    /// </para>
    /// <para>
    /// Presentation only: it reads the log and decides nothing.
    /// </para>
    /// </summary>
    internal sealed class EventSigns
    {
        /// <summary>How long a sign lasts, from popping up to gone.</summary>
        private const float Lifetime = 1.7f;

        private const float PopTime = 0.16f;
        private const float FadeTime = 0.4f;

        /// <summary>
        /// How many signs may be up at once. Twenty people going down in a
        /// crush would otherwise paper over the whole screen, and a screenful
        /// of signs says less than four do.
        /// </summary>
        private const int AtMostOnScreen = 4;

        /// <summary>
        /// How long before the same person may have another sign. Without it
        /// somebody repeatedly failing at a door stutters a sign every tick.
        /// </summary>
        private const float PerPersonRest = 1.4f;

        /// <summary>How high above the floor a sign floats.</summary>
        private const float SignHeight = 1.55f;

        /// <summary>How long the arrow is allowed to get, in metres.</summary>
        private const float ArrowLength = 0.55f;

        private static readonly Color Card = new Color(0.97f, 0.95f, 0.86f);
        private static readonly Color Edge = new Color(0.16f, 0.14f, 0.12f);
        private static readonly Color BadInk = new Color(0.78f, 0.12f, 0.08f);
        private static readonly Color GoodInk = new Color(0.10f, 0.52f, 0.18f);

        private sealed class Sign
        {
            public Transform Root;
            public Transform Plate;
            public TextMesh Label;
            public LineRenderer Border;
            public LineRenderer Arrow;
            public MeshRenderer Backing;

            public float Born = float.NegativeInfinity;
            public ulong Person;

            /// <summary>Where in the world the sign sits, and what it points at.</summary>
            public Vector3 Anchor;
            public Vector3 PointAt;
            public bool HasArrow;
            public Color Ink;
        }

        private readonly Sign[] signs = new Sign[AtMostOnScreen];

        /// <summary>When each person last had a sign, so they do not stack up.</summary>
        private readonly Dictionary<ulong, float> lastShown = new Dictionary<ulong, float>();

        /// <summary>Every event so far by its ID, so a sign can find its cause.</summary>
        private readonly Dictionary<ulong, CausalEvent> byId = new Dictionary<ulong, CausalEvent>();

        private readonly Material lineMaterial;

        public EventSigns(PresentationMaterials materials, Transform parent)
        {
            lineMaterial = materials.Icon;
            for (int i = 0; i < signs.Length; i++)
            {
                signs[i] = Build(i, materials, parent);
                signs[i].Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Remembers an event so a later one can name it as its cause. Called
        /// for every event, including the ones that never get a sign, because
        /// the chatter is often exactly what caused the thing worth showing.
        /// </summary>
        public void Remember(CausalEvent record) => byId[record.EventId] = record;

        /// <summary>
        /// Shows a sign for this event, if it is one worth a sign and the
        /// person is not already wearing one.
        /// </summary>
        public void Offer(CausalEvent record, EventStory story, float time)
        {
            if (!TryCaption(record, story, out string caption, out bool good))
            {
                return;
            }

            // Who the sign belongs to. Most of these happen *to* somebody, so
            // the target is the subject; where there is no target the source is.
            SimulationId subject = record.HasTarget ? record.TargetId : record.SourceId;
            if (lastShown.TryGetValue(subject.Value, out float when) && time - when < PerPersonRest)
            {
                return;
            }

            lastShown[subject.Value] = time;

            Sign sign = Oldest(subject.Value);
            sign.Person = subject.Value;
            sign.Born = time;
            sign.Ink = good ? GoodInk : BadInk;
            sign.Anchor = ToUnityPosition(record.Position) + Vector3.up * SignHeight;
            sign.HasArrow = TryFindCause(record, out sign.PointAt);
            sign.Label.text = caption;
            sign.Root.gameObject.SetActive(true);
        }

        /// <summary>
        /// One frame of every sign that is up: the pop, the drift upward, the
        /// fade, and the arrow swinging round to keep pointing at the cause as
        /// the player turns the camera.
        /// </summary>
        public void Update(float time, Quaternion cameraRotation)
        {
            for (int i = 0; i < signs.Length; i++)
            {
                Sign sign = signs[i];
                float age = time - sign.Born;
                if (age < 0f || age >= Lifetime)
                {
                    if (sign.Root.gameObject.activeSelf)
                    {
                        sign.Root.gameObject.SetActive(false);
                    }

                    continue;
                }

                // Drifts up a little as it ages, the way a speech bubble does.
                sign.Root.SetPositionAndRotation(
                    sign.Anchor + Vector3.up * (age * 0.12f), cameraRotation);

                float pop = age < PopTime ? AgentIconViews.EaseOutBack(age / PopTime) : 1f;
                sign.Plate.localScale = Vector3.one * Mathf.Max(0.01f, pop);

                float fade = Mathf.Clamp01((Lifetime - age) / FadeTime);
                sign.Label.color = WithAlpha(sign.Ink, fade);
                SetColour(sign.Border, WithAlpha(Edge, fade));
                if (sign.Backing != null)
                {
                    sign.Backing.enabled = true;
                }

                sign.Arrow.enabled = sign.HasArrow;
                if (sign.HasArrow)
                {
                    PointTheArrow(sign, cameraRotation, fade);
                }
            }
        }

        /// <summary>
        /// Swings the arrow to point from the sign toward the cause. Worked out
        /// on the screen rather than in the world, because the sign itself
        /// always faces the camera: an arrow aimed in world space would point
        /// off into the air as soon as the view turned.
        /// </summary>
        private static void PointTheArrow(Sign sign, Quaternion cameraRotation, float fade)
        {
            Vector3 toCause = Quaternion.Inverse(cameraRotation) * (sign.PointAt - sign.Root.position);
            var flat = new Vector2(toCause.x, toCause.y);
            if (flat.sqrMagnitude < 0.0001f)
            {
                sign.Arrow.enabled = false;
                return;
            }

            float angle = Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg;
            sign.Arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            SetColour(sign.Arrow, WithAlpha(sign.Ink, fade));
        }

        /// <summary>
        /// Where the thing that caused this came from. The log threads every
        /// event back to whatever set it off, so the arrow is simply that
        /// event's position. A cause at the same spot as the sign is no use to
        /// anybody, so it gets no arrow at all.
        /// </summary>
        private bool TryFindCause(CausalEvent record, out Vector3 point)
        {
            point = default;
            if (!record.HasCausalParent || !byId.TryGetValue(record.CausalParentEventId, out CausalEvent cause))
            {
                return false;
            }

            point = ToUnityPosition(cause.Position) + Vector3.up * 0.6f;
            return (point - (ToUnityPosition(record.Position) + Vector3.up * SignHeight)).sqrMagnitude > 0.35f;
        }

        /// <summary>
        /// The sign to use next: a free one, or the one this person already
        /// has, or failing both the oldest on screen. Reusing the person's own
        /// sign is what stops one unlucky soul stacking four of them.
        /// </summary>
        private Sign Oldest(ulong person)
        {
            Sign oldest = signs[0];
            for (int i = 0; i < signs.Length; i++)
            {
                if (signs[i].Person == person && signs[i].Root.gameObject.activeSelf)
                {
                    return signs[i];
                }

                if (signs[i].Born < oldest.Born)
                {
                    oldest = signs[i];
                }
            }

            return oldest;
        }

        /// <summary>
        /// Whether this kind of event is worth interrupting the player for.
        /// The rule: something that changes what a person can do, and that the
        /// player cannot work out by looking. A collision is visible; being
        /// knocked out by one is not. Everything the log already treats as
        /// chatter fails that rule, and is excluded before anything else is
        /// asked.
        /// </summary>
        internal static bool EarnsASign(FireReactionEventType type)
        {
            return !EventStory.IsBackground(type) && WordsFor(type) != null;
        }

        /// <summary>
        /// What a sign says, in two or three words. Deliberately not the log's
        /// wording: the log writes sentences for reading at rest, and this is
        /// read at a glance while the room is on fire. Null means this event
        /// gets no sign.
        /// </summary>
        private static string WordsFor(FireReactionEventType type)
        {
            switch (type)
            {
                case FireReactionEventType.AgentCaughtFire: return "on fire!";
                case FireReactionEventType.AgentPassedOut: return "knocked out!";
                case FireReactionEventType.AgentCrushed: return "crushed!";
                case FireReactionEventType.AgentGaveUpOnDoor: return "no way through!";
                case FireReactionEventType.AgentShoved: return "shoved!";
                case FireReactionEventType.AgentFroze: return "frozen!";
                case FireReactionEventType.AgentLost: return "lost";
                case FireReactionEventType.AgentRescued: return "dragged clear!";
                case FireReactionEventType.AgentShookAwake: return "woken up!";
                case FireReactionEventType.AgentDoused: return "put out!";
                case FireReactionEventType.DoorBlocked: return "jammed!";
                case FireReactionEventType.DoorBurntThrough: return "burnt through!";
                case FireReactionEventType.ObjectExploded: return "bang!";
                case FireReactionEventType.PowerSparkStarted: return "the wire is lit!";
                case FireReactionEventType.AgentLookedForAWayOut: return "which way?";
                case FireReactionEventType.AgentFoundADeadEnd: return "dead end!";
                case FireReactionEventType.AgentFoundTheWayOut: return "this way!";
                default: return null;
            }
        }

        /// <summary>
        /// Whether this is something going right. Good news is written in green
        /// and bad in red, so the colour alone carries at a glance even when
        /// there is no time to read the words.
        /// </summary>
        internal static bool IsGoodNews(FireReactionEventType type)
        {
            switch (type)
            {
                case FireReactionEventType.AgentRescued:
                case FireReactionEventType.AgentShookAwake:
                case FireReactionEventType.AgentDoused:
                case FireReactionEventType.AgentFoundTheWayOut:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The words for one event, with the person's number on the front so
        /// the sign names somebody the player has been watching.
        /// </summary>
        private static bool TryCaption(CausalEvent record, EventStory story, out string caption, out bool good)
        {
            caption = null;
            good = false;
            if (!EarnsASign(record.EventType))
            {
                return false;
            }

            SimulationId subject = record.HasTarget ? record.TargetId : record.SourceId;
            string who = story.NumberOf(subject) is int number ? number + ": " : string.Empty;
            caption = who + WordsFor(record.EventType);
            good = IsGoodNews(record.EventType);
            return true;
        }

        private Sign Build(int index, PresentationMaterials materials, Transform parent)
        {
            var sign = new Sign();
            sign.Root = new GameObject($"Sign {index + 1} (presentation)").transform;
            sign.Root.SetParent(parent, false);

            sign.Plate = new GameObject("Plate").transform;
            sign.Plate.SetParent(sign.Root, false);

            // The card itself: a quad behind the words so they are readable
            // against a burning room rather than lost in it.
            GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backing.name = "Card";
            Object.Destroy(backing.GetComponent<Collider>());
            backing.transform.SetParent(sign.Plate, false);
            backing.transform.localScale = new Vector3(0.95f, 0.3f, 1f);
            backing.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            sign.Backing = backing.GetComponent<MeshRenderer>();
            sign.Backing.sharedMaterial = materials.Icon;
            sign.Backing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sign.Backing.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", Card);
            sign.Backing.SetPropertyBlock(block);

            sign.Border = Stroke(sign.Plate, "Border", 0.018f);
            sign.Border.loop = true;
            sign.Border.positionCount = 4;
            sign.Border.SetPositions(new[]
            {
                new Vector3(-0.475f, -0.15f, 0f), new Vector3(0.475f, -0.15f, 0f),
                new Vector3(0.475f, 0.15f, 0f), new Vector3(-0.475f, 0.15f, 0f)
            });

            var labelObject = new GameObject("Words");
            labelObject.transform.SetParent(sign.Plate, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            sign.Label = labelObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                sign.Label.font = font;
                labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            sign.Label.anchor = TextAnchor.MiddleCenter;
            sign.Label.alignment = TextAlignment.Center;
            sign.Label.fontStyle = FontStyle.Bold;
            sign.Label.characterSize = 0.055f;
            sign.Label.fontSize = 64;

            // The arrow hangs off the sign and is spun to point at the cause.
            var arrowPivot = new GameObject("Arrow").transform;
            arrowPivot.SetParent(sign.Root, false);
            sign.Arrow = Stroke(arrowPivot, "Shaft", 0.03f);
            sign.Arrow.positionCount = 5;
            sign.Arrow.SetPositions(new[]
            {
                new Vector3(0.18f, 0f, 0f),
                new Vector3(ArrowLength, 0f, 0f),
                new Vector3(ArrowLength - 0.11f, 0.08f, 0f),
                new Vector3(ArrowLength, 0f, 0f),
                new Vector3(ArrowLength - 0.11f, -0.08f, 0f)
            });

            return sign;
        }

        private LineRenderer Stroke(Transform parent, string name, float width)
        {
            var strokeObject = new GameObject(name);
            strokeObject.transform.SetParent(parent, false);
            LineRenderer line = strokeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.sharedMaterial = lineMaterial;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void SetColour(LineRenderer line, Color colour)
        {
            line.startColor = colour;
            line.endColor = colour;
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
