// AssetIntegrationSceneTests — engine 取込後検証 (gates.md AR-ASSET ※節 / tech-stack-unity.md「QA-PLAY の
// 実行方法」視覚サニティテスト観点). Loads the wired Game.unity scene (Editor/AssetIntegration.IntegrateAll
// has already swapped the Player's placeholder capsule for the Hero prefab and WaveSpawner's placeholder
// cube fallback for the Swarmer prefab) and machine-verifies the two things gates.md explicitly assigns
// to the Integrate phase (not AR-ASSET, which can't launch Unity): Humanoid Avatar validity via the
// live Animator, and no Renderer left with a null/InternalErrorShader material after
// Editor/AssetIntegration.FixMaterialsForUrp.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class AssetIntegrationSceneTests
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
        public IEnumerator HeroVisual_HasValidHumanoidAnimatorAndNoMaterialErrors()
        {
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");

            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player must carry a HeroVisual child with an Animator once Editor/AssetIntegration has run");
            Assert.IsTrue(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                "Hero Animator's Avatar must be a valid Humanoid Avatar (tech-stack-unity.md「資産の取り扱い」— failure degrades to Generic + MANIFEST note, not silently)");
            Assert.IsNotNull(animator.runtimeAnimatorController, "Hero Animator must have the generated AnimatorController assigned");

            // Animator progression check (tech-stack-unity.md §5): not stuck on __preview__, and
            // normalizedTime advances after a short wait.
            AnimatorStateInfo before = animator.GetCurrentAnimatorStateInfo(0);
            Assert.IsFalse(before.IsName("__preview__"), "Animator must not be on Unity's auto-generated __preview__ clip");
            yield return new WaitForSecondsRealtime(0.2f);
            AnimatorStateInfo after = animator.GetCurrentAnimatorStateInfo(0);
            Assert.Greater(after.normalizedTime, before.normalizedTime, "Animator must progress (not frozen) over 0.2s");

            AssertNoMaterialErrors(player.gameObject);

            CaptureEvidenceScreenshot("asset-integration-hero-visual.png");
        }

        // RenderTexture capture (tech-stack-unity.md「QA-PLAY の実行方法」— ScreenCapture.CaptureScreenshot
        // doesn't work in batchmode; render Main Camera to a RenderTexture instead). Not itself an
        // acceptance assertion — a human-reviewable visual evidence artifact alongside the machine
        // checks above (gates.md AR-ASSET ※節: engine 取込後検証の証跡).
        private static void CaptureEvidenceScreenshot(string fileName)
        {
            Camera cam = Camera.main;
            // CR-CODE s-06 iter1 指摘#3: a wired Game scene always has a Main Camera — treating its
            // absence as a silent no-op let scene breakage pass this test while silently dropping the
            // gates.md-required visual evidence artifact. Fail loudly instead (this is not itself an
            // acceptance check for S-06 gameplay, but a broken/missing Main Camera is a wiring defect).
            Assert.IsNotNull(cam, $"Camera.main must exist in the wired Game scene to capture '{fileName}' evidence — scene wiring is broken.");
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

        [UnityTest]
        public IEnumerator HeroVisual_BoundingBoxWithinArtBibleHeightRange()
        {
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, "HeroVisual must contribute at least one Renderer");

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Assert.GreaterOrEqual(bounds.size.y, GameConfig.ModelScale.HeroHeightRangeMinM);
            Assert.LessOrEqual(bounds.size.y, GameConfig.ModelScale.HeroHeightRangeMaxM);
        }

        // gates.md AR-ASSET ※節: SFX-01..05 のクリップ配線検証（CR-CODE s-06 iter1 指摘#1）— Editor/
        // AssetIntegration.PatchSfxLibrary/AssignClip が生成済みクリップの割当に失敗しても
        // SfxLibrary.Play(null) の意図的サイレント no-op（未生成 SFX-06 用）と区別なく無音化していた穴を塞ぐ。
        // SFX-05 (S-15) は本バッチで新規に統合された枠 — 他4枠と同じ検証に加える。
        [UnityTest]
        public IEnumerator SfxLibrary_HasAllGeneratedClipsAssigned()
        {
            yield return LoadGameScene();

            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.AttackHit, "SfxLibrary.AttackHit (SFX-01) must be assigned by Editor/AssetIntegration");
            Assert.IsNotNull(SfxLibrary.Instance.Dash, "SfxLibrary.Dash (SFX-02) must be assigned by Editor/AssetIntegration");
            Assert.IsNotNull(SfxLibrary.Instance.PlayerHit, "SfxLibrary.PlayerHit (SFX-03) must be assigned by Editor/AssetIntegration");
            Assert.IsNotNull(SfxLibrary.Instance.CrystalPickup, "SfxLibrary.CrystalPickup (SFX-04) must be assigned by Editor/AssetIntegration");
            Assert.IsNotNull(SfxLibrary.Instance.WaveStart, "SfxLibrary.WaveStart (SFX-05) must be assigned by Editor/AssetIntegration");
        }

        // S-19 音声統合: SfxLibrary's shared AudioSource must be routed to the mixer's Sfx group (not left
        // at unity-gain-only default output) so every SFX-01..05 trigger reflects the settings-tab
        // sfxVolume bus (mirrors Tests/PlayMode/AudioIntegrationSceneTests.cs's identical Bgm-group check
        // for BgmPlayer).
        [UnityTest]
        public IEnumerator SfxLibrary_AudioSourceIsRoutedToSfxMixerGroup()
        {
            yield return LoadGameScene();

            Assert.IsNotNull(SfxLibrary.Instance);
            var source = SfxLibrary.Instance.GetComponent<AudioSource>();
            Assert.IsNotNull(source);
            Assert.IsNotNull(source.outputAudioMixerGroup, "SfxLibrary's AudioSource must be routed to the Sfx mixer bus (Editor/AssetIntegration.PatchSfxLibrary)");
            Assert.AreEqual(GameConfig.Audio.MixerSfxGroupName, source.outputAudioMixerGroup.name);
        }

        [UnityTest]
        public IEnumerator SwarmerPrefab_HasNoMaterialErrorsWhenSpawned()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab,
                "WaveSpawner.enemyPrefab must be assigned to the Swarmer prefab once Editor/AssetIntegration has run (falls back to a primitive-cube placeholder otherwise)");

            GameObject instance = Object.Instantiate(swarmerPrefab);
            try
            {
                AssertNoMaterialErrors(instance);
                Assert.IsNotNull(instance.GetComponent<EnemyAgent>(), "Swarmer prefab must carry EnemyAgent (WaveSpawner.SpawnOne relies on it already being present)");
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static void AssertNoMaterialErrors(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
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
