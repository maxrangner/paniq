using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The jet from an extinguisher: a spreading fan of small pale cubes
    /// thrown out in front of whoever is spraying. Read-only presentation,
    /// driven entirely by who is in the spraying state this frame.
    /// </summary>
    internal sealed class SprayView
    {
        private const int PuffsPerSpray = 14;

        private sealed class Jet
        {
            public Transform Root;
            public Transform[] Puffs;
            public Renderer[] Renderers;
            public float[] Seeds;
        }

        private readonly PresentationMaterials materials;
        private readonly Transform parent;
        private readonly List<Jet> jets = new List<Jet>();

        public SprayView(PresentationMaterials materials, Transform parent)
        {
            this.materials = materials;
            this.parent = parent;
        }

        public void Update(FireReactionSnapshot snapshot, float time)
        {
            int used = 0;
            foreach (FireReactionAgentSnapshot agent in snapshot.Agents)
            {
                if (agent.ActivityState != AgentActivityState.Spraying ||
                    agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                while (jets.Count <= used)
                {
                    jets.Add(CreateJet(jets.Count + 1));
                }

                Show(jets[used++], agent, time);
            }

            // Every jet nobody is firing this frame is hidden.
            for (int i = used; i < jets.Count; i++)
            {
                jets[i].Root.gameObject.SetActive(false);
            }
        }

        private Jet CreateJet(int number)
        {
            var root = new GameObject($"Extinguisher spray {number} (read-only presentation)").transform;
            root.SetParent(parent, false);
            var jet = new Jet
            {
                Root = root,
                Puffs = new Transform[PuffsPerSpray],
                Renderers = new Renderer[PuffsPerSpray],
                Seeds = new float[PuffsPerSpray]
            };

            for (int i = 0; i < PuffsPerSpray; i++)
            {
                GameObject puff = CreatePrimitive($"Puff {i + 1}", PrimitiveType.Cube, root, Vector3.zero,
                    Vector3.one * 0.12f, materials.Fire);
                ShowThroughWalls(puff);
                jet.Puffs[i] = puff.transform;
                jet.Renderers[i] = puff.GetComponent<Renderer>();
                jet.Seeds[i] = Hash01(number, i, 5);
            }

            return jet;
        }

        /// <summary>
        /// Lays the puffs out along the cone: further out they are bigger,
        /// fainter and further off the centre line, and they churn over time.
        /// </summary>
        private void Show(Jet jet, FireReactionAgentSnapshot agent, float time)
        {
            jet.Root.gameObject.SetActive(true);
            Vector3 nozzle = ToUnityPosition(agent.Position) + Vector3.up * 0.55f;
            float yaw = agent.HeadingDegrees;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 side = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;

            for (int i = 0; i < jet.Puffs.Length; i++)
            {
                float seed = jet.Seeds[i];

                // Each puff runs out along the jet and restarts, at its own pace.
                float along = Mathf.Repeat(time * (1.6f + seed * 0.8f) + seed, 1f);
                float spread = along * (0.45f + seed * 0.3f);
                float wobble = Mathf.Sin(time * 9f + seed * 20f) * spread;
                jet.Puffs[i].position = nozzle + forward * (along * 2.8f) + side * wobble +
                                        Vector3.up * (Mathf.Sin(time * 6f + seed * 12f) * 0.05f - along * 0.15f);
                jet.Puffs[i].localScale = Vector3.one * (0.08f + along * 0.16f);
                jet.Puffs[i].Rotate(Vector3.up, time * 40f * (0.5f + seed), Space.Self);

                Color puff = new Color(0.86f, 0.94f, 1f, 1f) * (1f - along * 0.4f);
                materials.SetColors(jet.Renderers[i], puff, puff * 0.35f);
            }
        }
    }
}
