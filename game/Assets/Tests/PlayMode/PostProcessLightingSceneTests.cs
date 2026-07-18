// PostProcessLightingSceneTests — S-27: URP ポストプロセス/ライティングで key image 発色へ接近.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame -> PostProcessVolume/PostProcessRig +
// Directional Light/KeyLightRig) and machine-verifies the S-27 acceptance: a global URP Volume exists
// with Bloom/ColorAdjustments/Tonemapping overrides sourced from GameConfig.PostProcess, the Directional
// Light is adjusted per GameConfig.Lighting (art-bible upper-front-left key light, soft shadows only),
// and a RenderTexture capture shows hero/enemy/crystal remain distinguishable and not washed-out/crushed
// after the post-process + lighting change (no readability regression), with the crystal's emissive glow
// reading brighter than the non-emissive enemy material and the plain arena floor.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class PostProcessLightingSceneTests
    {
        private const string PostProcessVolumeRootName = "PostProcessVolume";
        private const string DirectionalLightRootName = "Directional Light";

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

        [UnityTest]
        public IEnumerator PostProcessVolume_IsGlobal_WithBloomColorAdjustmentsTonemapping_SourcedFromGameConfig()
        {
            yield return LoadGameScene();

            GameObject root = GameObject.Find(PostProcessVolumeRootName);
            Assert.IsNotNull(root, "Game scene must be wired with a PostProcessVolume root (Editor/SceneWiring.WireGame)");
            Assert.IsNotNull(root.GetComponent<PostProcessRig>(), "PostProcessVolume root must carry the PostProcessRig component");

            var volume = root.GetComponent<Volume>();
            Assert.IsNotNull(volume, "PostProcessRig must RequireComponent<Volume> so a Volume is always present");
            Assert.IsTrue(volume.isGlobal,
                "post-process Volume must be global (applies everywhere in the arena under the fixed overhead camera)");

            VolumeProfile profile = volume.sharedProfile;
            Assert.IsNotNull(profile, "PostProcessRig must build/assign a VolumeProfile at runtime");
            Assert.AreSame(profile, PostProcessRig.LastAppliedProfileForTests,
                "PostProcessRig.Start() must be the one that built/applied this session's VolumeProfile");

            Assert.IsTrue(profile.TryGet(out Bloom bloom), "VolumeProfile must contain a Bloom override");
            Assert.IsTrue(bloom.active, "Bloom override must be active");
            Assert.AreEqual(GameConfig.PostProcess.BloomThreshold, bloom.threshold.value, 1e-4f,
                "Bloom.threshold must come from GameConfig.PostProcess.BloomThreshold");
            Assert.AreEqual(GameConfig.PostProcess.BloomIntensity, bloom.intensity.value, 1e-4f,
                "Bloom.intensity must come from GameConfig.PostProcess.BloomIntensity");
            Assert.AreEqual(GameConfig.PostProcess.BloomScatter, bloom.scatter.value, 1e-4f,
                "Bloom.scatter must come from GameConfig.PostProcess.BloomScatter");

            Assert.IsTrue(profile.TryGet(out ColorAdjustments colorAdjustments), "VolumeProfile must contain a ColorAdjustments override");
            Assert.IsTrue(colorAdjustments.active, "ColorAdjustments override must be active");
            Assert.AreEqual(GameConfig.PostProcess.ColorPostExposure, colorAdjustments.postExposure.value, 1e-4f,
                "ColorAdjustments.postExposure must come from GameConfig.PostProcess.ColorPostExposure");
            Assert.AreEqual(GameConfig.PostProcess.ColorContrast, colorAdjustments.contrast.value, 1e-4f,
                "ColorAdjustments.contrast must come from GameConfig.PostProcess.ColorContrast");
            Assert.AreEqual(GameConfig.PostProcess.ColorSaturation, colorAdjustments.saturation.value, 1e-4f,
                "ColorAdjustments.saturation must come from GameConfig.PostProcess.ColorSaturation");

            Assert.IsTrue(profile.TryGet(out Tonemapping tonemapping), "VolumeProfile must contain a Tonemapping override");
            Assert.IsTrue(tonemapping.active, "Tonemapping override must be active");
            TonemappingMode expectedMode = GameConfig.PostProcess.TonemappingUseNeutral ? TonemappingMode.Neutral : TonemappingMode.ACES;
            Assert.AreEqual(expectedMode, tonemapping.mode.value,
                "Tonemapping.mode must come from GameConfig.PostProcess.TonemappingUseNeutral");

            var camData = Camera.main.GetUniversalAdditionalCameraData();
            Assert.IsTrue(camData.renderPostProcessing,
                "PostProcessRig must enable UniversalAdditionalCameraData.renderPostProcessing on Camera.main, otherwise the Volume has no visible effect");
        }

        [UnityTest]
        public IEnumerator DirectionalLight_KeyLightRig_AppliesGameConfigRotationColorIntensityAndSoftShadows()
        {
            yield return LoadGameScene();

            GameObject root = GameObject.Find(DirectionalLightRootName);
            Assert.IsNotNull(root, "Game scene must have a 'Directional Light' (ForgeScaffold DefaultGameObjects)");
            Assert.IsNotNull(root.GetComponent<KeyLightRig>(), "Directional Light must carry the KeyLightRig component (Editor/SceneWiring.WireGame)");

            var light = root.GetComponent<Light>();
            Assert.IsNotNull(light);
            Assert.AreEqual(LightType.Directional, light.type);

            Quaternion expectedRotation = Quaternion.Euler(GameConfig.Lighting.KeyLightPitchDeg, GameConfig.Lighting.KeyLightYawDeg, 0f);
            Assert.Less(Quaternion.Angle(expectedRotation, root.transform.rotation), 0.5f,
                "Directional Light rotation must match GameConfig.Lighting key light pitch/yaw (art-bible upper-front-left)");

            Assert.AreEqual(GameConfig.Lighting.KeyLightIntensity, light.intensity, 1e-4f,
                "Light.intensity must come from GameConfig.Lighting.KeyLightIntensity");
            Assert.AreEqual(LightShadows.Soft, light.shadows,
                "style_block prohibits hard cast shadows — must be Soft, not Hard/None");
            Assert.AreEqual(GameConfig.Lighting.KeyLightShadowStrength, light.shadowStrength, 1e-4f,
                "Light.shadowStrength must come from GameConfig.Lighting.KeyLightShadowStrength");

            Assert.IsTrue(ColorUtility.TryParseHtmlString(GameConfig.Lighting.KeyLightColor, out Color expectedColor));
            Assert.Less(ColorDistance(expectedColor, light.color), 0.01f,
                "Light.color must match GameConfig.Lighting.KeyLightColor");
        }

        [UnityTest]
        public IEnumerator Capture_HeroEnemyCrystalReadability_Evidence_NotWashedOutOrCrushed_AndDistinguishable()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            player.transform.position = Vector3.zero;

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner);
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab,
                "WaveSpawner.enemyPrefab must be assigned to the Swarmer prefab once Editor/AssetIntegration has run");

            Vector3 enemyPos = new Vector3(4.5f, 0f, 2.5f);
            GameObject enemyInstance = Object.Instantiate(swarmerPrefab, enemyPos, Quaternion.identity);
            enemyInstance.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);

            Vector3 crystalPos = new Vector3(-4.5f, 0f, -2.5f);
            CrystalPickup.SpawnDrop(crystalPos, 1);
            CrystalPickup crystal = Object.FindFirstObjectByType<CrystalPickup>();
            Assert.IsNotNull(crystal);

            yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Camera.main must exist in the wired Game scene to capture evidence.");

            Renderer crystalRenderer = crystal.GetComponent<Renderer>();
            Renderer enemyRenderer = enemyInstance.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(crystalRenderer, "crystal must have a Renderer");
            Assert.IsNotNull(enemyRenderer, "enemy must have a Renderer");

            const int width = 960;
            const int height = 540;
            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;

            // Crystal/enemy spawn/instantiate directly at their world position with no animator/bone
            // hierarchy repositioning involved, so Renderer.bounds.center projected through
            // WorldToScreenPoint is pixel-accurate for them (verified against the rendered frame within
            // ~1-2px). The hero FBX rig's rendered on-screen silhouette was found NOT to line up with
            // its own Renderer.bounds.center projected the same way under this steeply-pitched fixed
            // camera (a pre-existing rig/animator characteristic unrelated to this story's Volume/
            // lighting change — see state/active.md), so hero is instead located by a generous best-
            // match search below, anchored at the world origin's screen projection and constrained
            // narrow in X (so it can't wander ~100px sideways onto the enemy) but tall in Y.
            Vector3 crystalScreen = cam.WorldToScreenPoint(crystalRenderer.bounds.center);
            Vector3 enemyScreen = cam.WorldToScreenPoint(enemyRenderer.bounds.center);
            Vector3 heroAnchorScreen = cam.WorldToScreenPoint(Vector3.zero);

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
            File.WriteAllBytes(Path.Combine(dir, "postprocess-lighting-readability.png"), tex.EncodeToPNG());

            // Floor reference: a point on the near (south) side of the arena, away from every spawned
            // object above, so its color is guaranteed to be plain graded floor.
            Color floorReference = SampleAverage(tex, cam.WorldToScreenPoint(new Vector3(0f, 0f, -9f)), width, height, 3);

            Color crystalColor = SampleAverage(tex, crystalScreen, width, height, 5);
            Color enemyColor = SampleAverage(tex, enemyScreen, width, height, 5);
            Color heroColor = FindMostDistinctFromReference(
                tex, heroAnchorScreen, width, height, floorReference, radiusX: 45, radiusY: 130, step: 2);

            Object.DestroyImmediate(tex);
            Object.Destroy(enemyInstance);

            foreach (var pair in new (string name, Color color)[]
                     { ("hero", heroColor), ("enemy", enemyColor), ("crystal", crystalColor) })
            {
                Assert.IsFalse(IsNearWhite(pair.color), $"{pair.name} pixel {pair.color} must not be blown out to near-white (白飛び)");
                Assert.IsFalse(IsNearBlack(pair.color), $"{pair.name} pixel {pair.color} must not be crushed to near-black (黒潰れ)");
            }

            Assert.Greater(ColorDistance(heroColor, enemyColor), 0.05f,
                "hero and enemy must remain distinguishable after post-process/lighting adjustment");
            Assert.Greater(ColorDistance(heroColor, crystalColor), 0.05f,
                "hero and crystal must remain distinguishable after post-process/lighting adjustment");
            Assert.Greater(ColorDistance(enemyColor, crystalColor), 0.05f,
                "enemy and crystal must remain distinguishable after post-process/lighting adjustment");

            // CR-CODE s-27 iter2 major指摘#4 fix: the pairwise distinctness asserts above are all
            // satisfiable even if hero/enemy failed to render entirely (a total rendering regression),
            // because the sampled/searched pixel would then just be floorReference-colored, and the floor
            // color already differs enough from the crystal's Bloom-boosted emissive glow (and from
            // itself vs itself is 0, but crystal vs floor-colored "hero"/"enemy" would still pass). Assert
            // each foreground sample is itself meaningfully distinct from the plain floor, so a missing
            // hero/enemy render (silent regression) is caught instead of degenerating into a floor-vs-floor
            // comparison that the pairwise asserts above cannot detect.
            Assert.Greater(ColorDistance(heroColor, floorReference), 0.05f,
                "hero pixel must be meaningfully distinct from the plain arena floor (catches a hero-not-rendered regression)");
            Assert.Greater(ColorDistance(enemyColor, floorReference), 0.05f,
                "enemy pixel must be meaningfully distinct from the plain arena floor (catches an enemy-not-rendered regression)");

            Assert.Greater(crystalColor.maxColorComponent, enemyColor.maxColorComponent,
                "crystal's Bloom-boosted emissive glow must read brighter than the non-emissive enemy material");
            Assert.Greater(crystalColor.maxColorComponent, floorReference.maxColorComponent,
                "crystal's Bloom-boosted emissive glow must read brighter than the plain arena floor it sits on");
        }

        private static Color SampleAverage(Texture2D tex, Vector3 screenPoint, int width, int height, int radius)
        {
            int cx = Mathf.Clamp(Mathf.RoundToInt(screenPoint.x), 0, width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(screenPoint.y), 0, height - 1);
            Color sum = Color.black;
            int count = 0;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = Mathf.Clamp(cx + dx, 0, width - 1);
                    int y = Mathf.Clamp(cy + dy, 0, height - 1);
                    sum += tex.GetPixel(x, y);
                    count++;
                }
            }
            return sum / count;
        }

        /// <summary>Scans a (2*radius+1) square (stepping by <paramref name="step"/> pixels) around
        /// <paramref name="screenPoint"/> for the pixel with the largest color distance from
        /// <paramref name="reference"/> (the floor's graded color) — i.e. the most likely foreground-object
        /// pixel in that neighborhood, without depending on an exact per-pixel projection.</summary>
        private static Color FindMostDistinctFromReference(
            Texture2D tex, Vector3 screenPoint, int width, int height, Color reference, int radiusX, int radiusY, int step)
        {
            int cx = Mathf.Clamp(Mathf.RoundToInt(screenPoint.x), 0, width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(screenPoint.y), 0, height - 1);
            Color best = tex.GetPixel(cx, cy);
            float bestDist = ColorDistance(best, reference);
            for (int dx = -radiusX; dx <= radiusX; dx += step)
            {
                for (int dy = -radiusY; dy <= radiusY; dy += step)
                {
                    int x = Mathf.Clamp(cx + dx, 0, width - 1);
                    int y = Mathf.Clamp(cy + dy, 0, height - 1);
                    Color c = tex.GetPixel(x, y);
                    float d = ColorDistance(c, reference);
                    if (d > bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
            }
            return best;
        }

        private static bool IsNearWhite(Color c)
        {
            const float whiteThreshold = 0.97f;
            return c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold;
        }

        private static bool IsNearBlack(Color c)
        {
            const float blackThreshold = 0.03f;
            return c.r < blackThreshold && c.g < blackThreshold && c.b < blackThreshold;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
