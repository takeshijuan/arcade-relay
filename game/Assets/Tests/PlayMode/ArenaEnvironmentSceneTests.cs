// ArenaEnvironmentSceneTests — S-20: アリーナ環境ビジュアル + クリスタル発光 + 全体調整.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → ArenaEnvironment root) and
// machine-verifies the acceptance from state/stories.yaml S-20: floor/boundary/spawn-ring/crystal exist
// with no Renderer material errors, the crystal's material is emissive and rotates, the player's world
// position never goes NaN, and a RenderTexture capture (for qa-lead's mandatory human visual review of
// four-direction readability, P-01) is produced with enemies placed at all four cardinal directions.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class ArenaEnvironmentSceneTests
    {
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";
        private const string ArenaEnvironmentRootName = "ArenaEnvironment";

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
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

        private static void DisableWaveSpawner()
        {
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }
        }

        private static EnemyAgent CreateEnemy(Vector3 position)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            EnemyAgent agent = go.AddComponent<EnemyAgent>();
            agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            return agent;
        }

        [UnityTest]
        public IEnumerator ArenaEnvironment_FloorBoundaryAndSpawnRing_ExistWithNoMaterialErrors()
        {
            yield return LoadGameScene();

            GameObject root = GameObject.Find(ArenaEnvironmentRootName);
            Assert.IsNotNull(root, "Game scene must be wired with an ArenaEnvironment root (Editor/SceneWiring.WireGame)");
            Assert.IsNotNull(root.GetComponent<ArenaEnvironment>(), "ArenaEnvironment root must carry the ArenaEnvironment component");

            Transform floor = root.transform.Find("ArenaFloor");
            Transform boundary = root.transform.Find("ArenaBoundary");
            Transform spawnRing = root.transform.Find("SpawnRing");
            Assert.IsNotNull(floor, "ArenaEnvironment must build an ArenaFloor child (円柱/平面プリミティブ+マットグリーン)");
            Assert.IsNotNull(boundary, "ArenaEnvironment must build an ArenaBoundary child (境界)");
            Assert.IsNotNull(spawnRing, "ArenaEnvironment must build a SpawnRing child (スポーンリング表示)");

            AssertNoMaterialErrors(floor.gameObject);
            AssertNoMaterialErrors(boundary.gameObject);
            AssertNoMaterialErrors(spawnRing.gameObject);
        }

        [UnityTest]
        public IEnumerator ArenaFloor_IsSizedToArenaRadius_AndBoundaryMatchesArenaRadius_SpawnRingMatchesSpawnRadius()
        {
            yield return LoadGameScene();

            GameObject root = GameObject.Find(ArenaEnvironmentRootName);
            Assert.IsNotNull(root);

            Transform floor = root.transform.Find("ArenaFloor");
            Assert.IsNotNull(floor);
            // Unity's Cylinder primitive default diameter is 1 at scale 1; ArenaEnvironment scales it to
            // ARENA_RADIUS*2 so the disc's world-space radius matches the gameplay boundary exactly.
            float floorWorldRadius = floor.localScale.x * 0.5f;
            Assert.AreEqual(GameConfig.Player.ArenaRadius, floorWorldRadius, 1e-3f,
                "arena floor's world-space radius must match ARENA_RADIUS so the visual boundary and the gameplay clamp agree");

            Transform boundary = root.transform.Find("ArenaBoundary");
            MeshFilter boundaryFilter = boundary.GetComponent<MeshFilter>();
            Assert.IsNotNull(boundaryFilter.sharedMesh);
            AssertRingRadiusMatches(boundaryFilter.sharedMesh, GameConfig.Player.ArenaRadius, GameConfig.Environment.BoundaryRingWidth);

            Transform spawnRing = root.transform.Find("SpawnRing");
            MeshFilter spawnFilter = spawnRing.GetComponent<MeshFilter>();
            Assert.IsNotNull(spawnFilter.sharedMesh);
            AssertRingRadiusMatches(spawnFilter.sharedMesh, GameConfig.Enemy.SpawnRadius, GameConfig.Environment.SpawnRingWidth);
        }

        private static void AssertRingRadiusMatches(Mesh mesh, float expectedRadius, float expectedWidth)
        {
            float innerExpected = expectedRadius - expectedWidth * 0.5f;
            float outerExpected = expectedRadius + expectedWidth * 0.5f;
            foreach (Vector3 v in mesh.vertices)
            {
                float radial = new Vector2(v.x, v.z).magnitude;
                Assert.GreaterOrEqual(radial, innerExpected - 1e-3f);
                Assert.LessOrEqual(radial, outerExpected + 1e-3f);
            }
        }

        [UnityTest]
        public IEnumerator Crystal_HasEmissiveGlowingMaterial_AndRotatesOverTime()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            // Far enough that auto-pickup doesn't destroy the crystal before the rotation window elapses.
            Vector3 farPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 5f, 0f, 0f);
            CrystalPickup.SpawnDrop(farPosition, 1);
            CrystalPickup[] spawned = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(1, spawned.Length);
            CrystalPickup crystal = spawned[0];

            Renderer renderer = crystal.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "crystal must carry a Renderer");
            Material material = renderer.sharedMaterial;
            Assert.IsNotNull(material, "crystal Renderer must not have a null material slot");
            Assert.AreNotEqual(InternalErrorShaderName, material.shader.name,
                "crystal material fell back to InternalErrorShader (pink) — not URP-compatible");
            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), "crystal material must have emission enabled (発光マテリアル)");
            Color emission = material.GetColor("_EmissionColor");
            Color baseColor = material.color;
            Assert.Greater(emission.maxColorComponent, baseColor.maxColorComponent,
                "crystal emission must be HDR-boosted above the base color so it visibly glows against the non-emissive arena/enemies");

            Quaternion startRotation = crystal.transform.rotation;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreNotEqual(startRotation, crystal.transform.rotation,
                "crystal must rotate over time (緩やかな回転コードモーション)");
        }

        [UnityTest]
        public IEnumerator PlayerPosition_NeverGoesNaN_WhileArenaEnvironmentIsPresent()
        {
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Vector3 p = player.transform.position;
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z),
                    "player position must never contain NaN");
            }
        }

        [UnityTest]
        public IEnumerator Capture_FourDirectionReadability_Evidence()
        {
            // P-01 (紙一重回避): the fixed top-down camera must let the player see enemies converging from
            // all four cardinal directions simultaneously. Places visually-rendered swarmer instances
            // (WaveSpawner.EnemyPrefabForTests — CreateEnemy()'s bare GameObject+EnemyAgent used by the
            // logic-only tests above carries no Renderer at all, so it wouldn't show up here) at N/E/S/W
            // plus a couple of crystals, so a human reviewer can visually confirm the arena floor/
            // boundary/spawn-ring/crystal-glow composition reads clearly (gates.md QA-PLAY 観点2/6c).
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner);
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab,
                "WaveSpawner.enemyPrefab must be assigned to the Swarmer prefab once Editor/AssetIntegration has run");

            Vector3 center = player.transform.position;
            // CR-CODE S-20 iteration 1 major指摘#4対応 / iteration 2 minor指摘#2で記述精度を再修正:
            // ringRadius は SpawnRadius(=13.5m) そのものではなく SpawnRadius*0.6(=8.1m)、すなわちスポーン
            // リング(半径13.5m)より内側の位置に配置する。
            // S-22（Phase 3 Polish revise）追記: game-designer が design/gdd.md「カメラ」節をレンジ内で
            // 南側可視限界が最も深くなる境界値(CAMERA_HEIGHT=18/PITCH=60°/FOV=55°)へ更新し、S-22 で
            // GameConfig.Camera へ反映した。ArenaCameraMath.ComputeSouthVisibilityLimitZ の幾何計算に
            // よれば南側可視限界は旧値(H=14/P=65°/F=50°→z≈-6.5m)から z≈-9.6m まで拡大し、8.1m地点に
            // 配置する南側の敵はこの新値では画角内に収まる（ArenaCameraMathTests の
            // ComputeSouthVisibilityLimitZ_CurrentGameConfig値_CoversFourDirectionEvidenceRing...
            // で回帰検証済み）。ただし ARENA_RADIUS(12m)南端やSpawnRadius(13.5m)そのものの南端は
            // このレンジ内では依然カバーしきれない既知の制約として残る（四方完全均等カバーにはレンジ
            // 自体の拡張が必要で art-director協議を要しPhase 3 Polishのスコープ外。gdd「南側可視性の
            // 再点検」節・state/reviews/s-20.md finding #1・state/active.md 参照。Checkpoint C への
            // 申し送り事項として引き続き明記する）。
            float ringRadius = GameConfig.Enemy.SpawnRadius * 0.6f;
            Vector3[] cardinalOffsets =
            {
                new Vector3(0f, 0f, ringRadius),   // north
                new Vector3(ringRadius, 0f, 0f),   // east
                new Vector3(0f, 0f, -ringRadius),  // south
                new Vector3(-ringRadius, 0f, 0f),  // west
            };

            var spawned = new System.Collections.Generic.List<GameObject>();
            try
            {
                foreach (Vector3 offset in cardinalOffsets)
                {
                    GameObject instance = Object.Instantiate(swarmerPrefab, center + offset, Quaternion.identity);
                    instance.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
                    spawned.Add(instance);
                }

                CrystalPickup.SpawnDrop(center + new Vector3(ringRadius * 0.4f, 0f, ringRadius * 0.4f), 1);
                CrystalPickup.SpawnDrop(center + new Vector3(-ringRadius * 0.4f, 0f, -ringRadius * 0.4f), 1);

                yield return null;
                CaptureEvidenceScreenshot("arena-four-direction.png");
            }
            finally
            {
                foreach (GameObject instance in spawned)
                {
                    Object.Destroy(instance);
                }
            }
        }

        private static void AssertNoMaterialErrors(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, $"'{root.name}' must contribute at least one Renderer");
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

        // Mirrors AssetIntegrationSceneTests/QaPlayEvidenceTests.CaptureEvidenceScreenshot
        // (tech-stack-unity.md「QA-PLAY の実行方法」— ScreenCapture.CaptureScreenshot doesn't work in
        // batchmode; render Main Camera to a RenderTexture instead).
        private static void CaptureEvidenceScreenshot(string fileName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"Camera.main must exist in the wired Game scene to capture '{fileName}' evidence.");
            const int width = 960;
            const int height = 540;
            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = previous;
            RenderTexture.active = null;
            rt.Release();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "qa", "evidence"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
