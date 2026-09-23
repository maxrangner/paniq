using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The circle on the floor under the pointer while a card is in hand: the
    /// patch that card will catch if it is thrown here.
    /// <para>
    /// Cards are aimed at a place rather than at a chosen person, and a throw
    /// that catches the wrong person is spent. That is only a fair rule if the
    /// player could see who was standing there, which is what this is for. It
    /// brightens when somebody is inside it, so "this will catch nobody" and
    /// "this will catch somebody" are told apart at a glance rather than by
    /// counting capsules.
    /// </para>
    /// </summary>
    internal sealed class CardAimRing
    {
        private const int Segments = 56;

        private static readonly Color Empty = new Color(0.75f, 0.78f, 0.85f, 0.35f);
        private static readonly Color Catching = new Color(0.45f, 0.9f, 1f, 0.95f);

        private readonly LineRenderer line;

        public CardAimRing(Material material, Transform parent)
        {
            var ringObject = new GameObject("Card aim ring (presentation)");
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = Segments;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }

        /// <summary>How many people the throw would catch, for the HUD to say so.</summary>
        public int PeopleInside { get; private set; }

        public void Hide()
        {
            line.enabled = false;
            PeopleInside = 0;
        }

        /// <summary>
        /// Draws the patch at <paramref name="centre"/> and counts who is
        /// standing in it. The count is worked out here, from the snapshot,
        /// purely to draw and describe the aim -- the run decides for itself
        /// who was actually caught when the card lands.
        /// </summary>
        public void Show(LogicalPosition centre, int radiusMillimetres, FireReactionSnapshot snapshot, float time)
        {
            float radius = Metres(radiusMillimetres);
            Vector3 middle = ToUnityPosition(centre) + Vector3.up * 0.05f;

            PeopleInside = 0;
            if (snapshot != null)
            {
                long radiusSquared = (long)radiusMillimetres * radiusMillimetres;
                for (int i = 0; i < snapshot.Agents.Count; i++)
                {
                    FireReactionAgentSnapshot person = snapshot.Agents[i];
                    if (person.Outcome == AgentTerminalOutcome.Unresolved &&
                        LogicalPosition.DistanceSquared(person.Position, centre) <= radiusSquared)
                    {
                        PeopleInside++;
                    }
                }
            }

            // A slow breath, so an empty circle still reads as something the
            // game is drawing for you rather than a mark on the screen.
            float breath = 1f + 0.02f * Mathf.Sin(time * 3f);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                line.SetPosition(i, middle + new Vector3(
                    Mathf.Cos(angle) * radius * breath, 0f, Mathf.Sin(angle) * radius * breath));
            }

            Color colour = PeopleInside > 0 ? Catching : Empty;
            line.startColor = colour;
            line.endColor = colour;
            line.widthMultiplier = PeopleInside > 0 ? 0.055f : 0.03f;
            line.enabled = true;
        }
    }
}
