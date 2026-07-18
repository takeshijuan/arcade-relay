// EnemyVisualMotionSceneTests — S-21: swarmer static mesh integration + approach coded motion (rig
//縮退代替 — MDL-02 は must-replace/ANM-04 未生成のため Avatar/AnimatorController/スケルタルクリップに
// 依存しない). Loads the wired Game.unity scene (Editor/AssetIntegration.IntegrateAll has already built
// Assets/Generated/Prefabs/Swarmer.prefab with the "Visual" child wrapper — see
// Editor/AssetIntegration.BuildSwarmerPrefab) and machine-verifies the acceptance from
// state/stories.yaml S-21: no Renderer material errors, no Animator/skeletal-clip dependency, and the
// "Visual" child's localPosition.y oscillates over time (coded up/down bounce).
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class EnemyVisualMotionSceneTests
    {
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";

        [TearDown]
        public void TearDownSession()
        {
            EnemyAgent.ActiveEnemies.Clear();
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
        }

        private IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwarmerVisual_BouncesOverTimeWithoutAnimatorOrMaterialErrors()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab,
                "WaveSpawner.enemyPrefab must be assigned to the Swarmer prefab once Editor/AssetIntegration has run");

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController for EnemyAgent to approach");

            GameObject instance = Object.Instantiate(swarmerPrefab, Vector3.zero, Quaternion.identity);
            try
            {
                EnemyAgent agent = instance.GetComponent<EnemyAgent>();
                Assert.IsNotNull(agent, "Swarmer prefab root must carry EnemyAgent");
                agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);

                // No skeletal-animation dependency (rig-degraded MDL-02 — S-21 acceptance).
                Assert.IsNull(instance.GetComponentInChildren<Animator>(),
                    "Swarmer must not depend on an Animator/skeletal clip — coded motion only (MDL-02 rig-degraded, ANM-04 not generated)");

                Transform visual = instance.transform.Find(GameConfig.Enemy.VisualChildName);
                Assert.IsNotNull(visual,
                    $"Swarmer prefab must carry a '{GameConfig.Enemy.VisualChildName}' child separating renderer from the EnemyAgent root's approach motion (S-21 差し替え口)");

                AssertNoMaterialErrors(instance);

                float minY = float.MaxValue;
                float maxY = float.MinValue;
                float elapsed = 0f;
                // ~2 bounce cycles at GameConfig.Enemy.VisualBounceFrequencyHz regardless of the batchmode
                // editor's actual per-frame wall-clock time (accumulate via Time.deltaTime, not frame count).
                float minSimulatedSeconds = 2f / GameConfig.Enemy.VisualBounceFrequencyHz;
                while (elapsed < minSimulatedSeconds)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    minY = Mathf.Min(minY, visual.localPosition.y);
                    maxY = Mathf.Max(maxY, visual.localPosition.y);
                }

                Assert.Greater(maxY - minY, GameConfig.Enemy.VisualBounceAmplitude,
                    "Visual child's localPosition.y must oscillate (coded up/down bounce approach motion)");

                // The EnemyAgent root itself must still be the one whose world position moves toward the
                // player (Systems/EnemyApproachSystem) — the bounce/tilt must stay confined to the child.
                Assert.AreEqual(0f, instance.transform.position.y, 1e-3f,
                    "EnemyAgent root's world Y must stay untouched by the child's bounce motion");
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator SwarmerVisual_PreservesBakedLocalPositionOffsetUnderBounce()
        {
            // CR-CODE S-21 iteration 1 minor finding: ApplyVisualMotion used to overwrite the "Visual"
            // child's localPosition outright (`new Vector3(0, bounceOffsetY, 0)`), discarding any baked
            // local position on the FBX root. Today's MDL-02 happens to bake (0,0,0) so this was
            // invisible, but the drop-in-replacement seam this class's header comment promises (a future
            // rigged model via Tripo credit top-up) could carry a non-zero pivot offset — this test uses a
            // synthetic non-zero baked offset to prove the composition (base + bounce) instead of relying
            // on today's coincidentally-zero MDL-02 offset.
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController for EnemyAgent to approach");

            var root = new GameObject("SyntheticSwarmerWithBakedOffset");
            var visual = new GameObject(GameConfig.Enemy.VisualChildName);
            visual.transform.SetParent(root.transform, worldPositionStays: false);
            var bakedOffset = new Vector3(0.25f, 0.5f, -0.1f);
            visual.transform.localPosition = bakedOffset;

            // EnemyAgent.Awake() (which captures _visualBaseLocalPosition) runs synchronously here since
            // root is active — must happen AFTER the baked offset is set above.
            EnemyAgent agent = root.AddComponent<EnemyAgent>();
            try
            {
                agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
                yield return null; // one Update — drives ApplyVisualMotion once

                Vector3 pos = visual.transform.localPosition;
                Assert.AreEqual(bakedOffset.x, pos.x, 1e-4f,
                    "baked local X offset must survive coded motion (drop-in replacement seam)");
                Assert.AreEqual(bakedOffset.z, pos.z, 1e-4f,
                    "baked local Z offset must survive coded motion (drop-in replacement seam)");
                Assert.GreaterOrEqual(pos.y, bakedOffset.y - GameConfig.Enemy.VisualBounceAmplitude - 1e-3f,
                    "Y must be baked.y plus the bounce term, not the bounce term alone");
                Assert.LessOrEqual(pos.y, bakedOffset.y + GameConfig.Enemy.VisualBounceAmplitude + 1e-3f,
                    "Y must be baked.y plus the bounce term, not the bounce term alone");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static void AssertNoMaterialErrors(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, "Swarmer instance must contribute at least one Renderer");
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(mat, $"Renderer '{renderer.name}' has a null material slot");
                    Assert.AreNotEqual(InternalErrorShaderName, mat.shader.name,
                        $"Renderer '{renderer.name}' material '{mat.name}' fell back to InternalErrorShader (pink) — not URP-compatible");
                }
            }
        }
    }
}
