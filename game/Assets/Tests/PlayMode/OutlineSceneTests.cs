// OutlineSceneTests — S-28: URP Outline 輪郭線 + hero/swarmer マテリアル増強 (テクスチャ再生成なし).
// Loads the wired Game.unity scene (Editor/AssetIntegration.IntegrateAll has already appended the
// "ForgeGame/Outline" material — Editor/AssetIntegration.BuildOutlineMaterial/ApplyOutlineToRenderers —
// as an extra sharedMaterials slot on every hero/swarmer Renderer) and machine-verifies the S-28
// acceptance: hero/swarmer Renderers carry the outline material with no null/InternalErrorShader slots,
// the outline material's color/width come from GameConfig.Outline (not a hardcoded/authoring-time
// default), and a RenderTexture capture of two densely-packed swarmers shows a visible dark-navy seam
// between their silhouettes.
using System.Collections;
using System.IO;
using System.Linq;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class OutlineSceneTests
    {
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";

        // CR-CODE S-28 iter2 minor finding fix: this was a third local copy of the "ForgeGame/Outline"
        // literal (alongside Editor/AssetIntegration and Components/HeroFxController, both of which already
        // read GameConfig.Outline.ShaderName) — point it at the same GameConfig constant to complete the
        // centralization instead of re-duplicating the string here.
        private static readonly string OutlineShaderName = GameConfig.Outline.ShaderName;

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

        private static void DisableWaveSpawner()
        {
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator HeroVisual_HasOutlineMaterial_SourcedFromGameConfig_NoMaterialErrors()
        {
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");

            AssertOutlineAppliedWithNoMaterialErrors(player.gameObject, "hero");
        }

        [UnityTest]
        public IEnumerator SwarmerPrefab_HasOutlineMaterial_SourcedFromGameConfig_NoMaterialErrors()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab,
                "WaveSpawner.enemyPrefab must be assigned to the Swarmer prefab once Editor/AssetIntegration has run");

            GameObject instance = Object.Instantiate(swarmerPrefab);
            try
            {
                AssertOutlineAppliedWithNoMaterialErrors(instance, "swarmer");
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static void AssertOutlineAppliedWithNoMaterialErrors(GameObject root, string label)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(GameConfig.Outline.Color, out Color expectedColor),
                "GameConfig.Outline.Color must be a valid hex color");

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, $"{label} must contribute at least one Renderer");

            bool anyOutlineFound = false;
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (Material mat in materials)
                {
                    Assert.IsNotNull(mat, $"{label} Renderer '{renderer.name}' has a null material slot");
                    Assert.AreNotEqual(InternalErrorShaderName, mat.shader.name,
                        $"{label} Renderer '{renderer.name}' material '{mat.name}' fell back to InternalErrorShader (pink) — not URP-compatible");
                }

                Material outlineMat = materials.FirstOrDefault(m => m.shader != null && m.shader.name == OutlineShaderName);
                if (outlineMat == null)
                {
                    continue;
                }
                anyOutlineFound = true;

                Assert.IsTrue(outlineMat.HasProperty("_OutlineColor"), $"{label} outline material must expose _OutlineColor");
                Color actualColor = outlineMat.GetColor("_OutlineColor");
                Assert.Less(ColorDistance(expectedColor, actualColor), 0.02f,
                    $"{label} outline material's _OutlineColor must come from GameConfig.Outline.Color (#{ColorUtility.ToHtmlStringRGB(expectedColor)}), was #{ColorUtility.ToHtmlStringRGB(actualColor)}");

                Assert.IsTrue(outlineMat.HasProperty("_OutlineWidth"), $"{label} outline material must expose _OutlineWidth");
                Assert.AreEqual(GameConfig.Outline.WidthMeters, outlineMat.GetFloat("_OutlineWidth"), 1e-4f,
                    $"{label} outline material's _OutlineWidth must come from GameConfig.Outline.WidthMeters");
            }

            Assert.IsTrue(anyOutlineFound,
                $"{label} must carry the '{OutlineShaderName}' outline material on at least one Renderer (Editor/AssetIntegration.ApplyOutlineToRenderers)");
        }

        [UnityTest]
        public IEnumerator Capture_DenseSwarmerCluster_OutlineSeparatesSilhouettes_Evidence()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner);
            GameObject swarmerPrefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(swarmerPrefab);

            // Measure one swarmer's actual half-width first (rather than assuming a hardcoded body size)
            // so the pair below can be placed with a small deliberate gap — just wide enough that each
            // creature's own outline rim is not swallowed by the other's overlapping fill (a fully
            // overlapping pair merges into one silhouette with no visible interior seam, which is not what
            // this test is trying to observe), but still close enough to read as "densely packed" in a
            // 24m-diameter arena (the acceptance scenario: "密集時に敵同士...のシルエットが輪郭線で分離して
            // 視認できる").
            GameObject probe = Object.Instantiate(swarmerPrefab, Vector3.zero, Quaternion.identity);
            probe.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            yield return null;
            Renderer probeRenderer = probe.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(probeRenderer);
            float halfWidth = probeRenderer.bounds.extents.x;
            Object.DestroyImmediate(probe);

            const float gapMeters = 0.12f;
            float offset = halfWidth + gapMeters / 2f;
            Vector3 posA = new Vector3(-offset, 0f, 0f);
            Vector3 posB = new Vector3(offset, 0f, 0f);
            GameObject a = Object.Instantiate(swarmerPrefab, posA, Quaternion.identity);
            a.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            GameObject b = Object.Instantiate(swarmerPrefab, posB, Quaternion.identity);
            b.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);

            yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Camera.main must exist in the wired Game scene to capture evidence.");

            Renderer rendererA = a.GetComponentInChildren<Renderer>();
            Renderer rendererB = b.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(rendererA);
            Assert.IsNotNull(rendererB);

            // Higher resolution than the other evidence captures in this suite: the gap between the two
            // silhouettes is intentionally thin (gapMeters above), so more pixel density is needed to
            // reliably resolve it.
            const int width = 1920;
            const int height = 1080;
            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Camera.WorldToScreenPoint below must run while cam.targetTexture is still this 1920x1080 RT
            // (it projects using the camera's CURRENT render-target pixel dimensions) — resetting
            // cam.targetTexture back to `previous` first would make it project into whatever tiny default
            // Game View resolution batchmode uses instead, silently producing screen coordinates that
            // don't correspond to any pixel in `tex` at all (observed during S-28 QA: rects came back in
            // the ~300x250 range against a 1920x1080 texture).
            Bounds screenSpaceRendererABounds = rendererA.bounds;
            Bounds screenSpaceRendererBBounds = rendererB.bounds;
            (float minX, float maxX, float minY, float maxY) rectA = ScreenRect(cam, screenSpaceRendererABounds);
            (float minX, float maxX, float minY, float maxY) rectB = ScreenRect(cam, screenSpaceRendererBBounds);

            cam.targetTexture = previous;
            RenderTexture.active = null;
            rt.Release();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "qa", "evidence"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "outline-dense-swarmer-cluster.png"), tex.EncodeToPNG());

            // Classify pixels by luminance rather than by exact color match against the raw (ungraded)
            // GameConfig hex values: S-27's URP Volume (Bloom/ColorAdjustments/Tonemapping) measurably
            // shifts rendered pixel colors away from their authored hex (verified during S-28 QA — e.g. the
            // floor's rendered color came back nowhere near GameConfig.Environment.FloorColor's raw RGB).
            // Luminance ordering survives that grading: the dark-navy outline (~0.22 luma) reads
            // meaningfully darker than both the enemy body (~0.37) and the floor (~0.64), so a fixed
            // threshold between those two bands reliably isolates outline pixels regardless of grading.
            //
            // CR-CODE S-28 iter1 minor finding: a pure luminance threshold is not enough on its own —
            // GameConfig.Environment.FloorColor (#7FE850, a bright green) under
            // GameConfig.Lighting.KeyLightShadowStrength (0.55, i.e. shadowed floor retains ~45% of its lit
            // luminance) lands close enough to darkLuminanceThreshold (~0.29 by this test's own ~0.64 lit-
            // floor observation) that a shadow patch straddled by lit floor on both sides could register as
            // an "isolated dark run" with no outline present at all, silently passing this test even if the
            // outline material broke. GameConfig.Outline.Color (#164583) is blue-dominant (B > G > R) while
            // the floor is green-dominant (G > R > B) in both its lit and shadowed form — channel dominance
            // is far more robust to tonemapping/grading shifts than absolute luminance, since grading curves
            // are applied per-channel and essentially never swap which channel is largest. CountIsolatedDarkRuns
            // below additionally requires a "dark" pixel's blue channel to be the strict maximum of its three
            // channels, so shadowed-floor pixels (green-dominant) can no longer masquerade as outline pixels
            // (blue-dominant) regardless of how close their luminance gets to the threshold.
            const float darkLuminanceThreshold = 0.30f;

            // The two creatures' irregular (multi-segment, non-convex) serpentine silhouettes don't overlap
            // or separate along a single predictable straight line, so instead of trying to geometrically
            // predict exactly where a gap/seam will land on screen, scan the whole screen-space union of
            // both bodies (rendererA.bounds + rendererB.bounds projected to screen, all 8 corners each —
            // more accurate than a single ground-plane point under this steeply-pitched camera). A single,
            // fully-merged silhouette crossed left-to-right always produces exactly 2 "isolated dark runs"
            // per scanline (entering its left outline edge, exiting its right outline edge) regardless of
            // how many creatures are actually inside it — that alone can't distinguish "one blob" from "two
            // separate, outline-divided creatures". Two creatures whose own individual outlines are both
            // still visible on a given scanline instead produce 4 isolated dark runs on that row (each
            // creature contributing its own left+right outline edge, with a lighter — enemy-body or floor —
            // pixel run between every pair) — see qa/evidence/outline-dense-swarmer-cluster.png for a
            // directly reviewable crop showing exactly this 4-run pattern at the pair's seam.
            const int scanMarginPx = 6;
            int xMin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(rectA.minX, rectB.minX)) - scanMarginPx, 0, width - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(rectA.maxX, rectB.maxX)) + scanMarginPx, 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(rectA.minY, rectB.minY)) - scanMarginPx, 0, height - 1);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(rectA.maxY, rectB.maxY)) + scanMarginPx, 0, height - 1);

            const int isolatedDarkRunsForTwoSilhouettes = 4;
            bool foundOutlineSeam = false;
            int bestRunsSeen = 0;
            for (int y = yMin; y <= yMax && !foundOutlineSeam; y++)
            {
                int runs = CountIsolatedDarkRuns(tex, y, xMin, xMax, darkLuminanceThreshold);
                bestRunsSeen = Mathf.Max(bestRunsSeen, runs);
                if (runs >= isolatedDarkRunsForTwoSilhouettes)
                {
                    foundOutlineSeam = true;
                }
            }

            Object.DestroyImmediate(tex);
            Object.Destroy(a);
            Object.Destroy(b);

            Assert.IsTrue(foundOutlineSeam,
                $"expected at least one scanline with {isolatedDarkRunsForTwoSilhouettes} isolated dark (outline) " +
                "runs — the two-separate-silhouettes signature — between the two densely-packed swarmers " +
                $"(best single-row count observed: {bestRunsSeen})");
        }

        /// <summary>Counts maximal runs of "dark" (luminance below <paramref name="darkLuminanceThreshold"/>
        /// AND blue-dominant — see this test's darkLuminanceThreshold comment for why the blue-dominance
        /// check exists: it excludes shadowed floor pixels, which are green-dominant and can otherwise
        /// approach the same luminance as the dark-navy outline) pixels on row <paramref name="y"/>,
        /// restricted to bounds [<paramref name="xMin"/>, <paramref name="xMax"/>], that have a non-dark
        /// pixel immediately before AND after them within that range (a dark run touching either scan edge
        /// is not "isolated" — it may be a silhouette that extends past the scan window, not a genuine seam
        /// bounded on both sides).</summary>
        private static int CountIsolatedDarkRuns(Texture2D tex, int y, int xMin, int xMax, float darkLuminanceThreshold)
        {
            int isolatedRuns = 0;
            bool sawLightBeforeRun = false;
            bool inDarkRun = false;
            for (int x = xMin; x <= xMax; x++)
            {
                Color c = tex.GetPixel(x, y);
                float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                bool isBlueDominant = c.b > c.r && c.b > c.g;
                bool isDark = luminance < darkLuminanceThreshold && isBlueDominant;

                if (isDark)
                {
                    inDarkRun = true;
                }
                else
                {
                    if (inDarkRun && sawLightBeforeRun)
                    {
                        isolatedRuns++;
                    }
                    inDarkRun = false;
                    sawLightBeforeRun = true;
                }
            }
            return isolatedRuns;
        }

        /// <summary>Projects all 8 corners of a world-space Bounds to screen space and returns the
        /// resulting axis-aligned screen rect (min/max X/Y) — more accurate than projecting a single
        /// ground-plane point under a steeply-pitched camera, where a body's elevated height shifts its
        /// on-screen X away from what a y=0 projection of the same world X would suggest.</summary>
        private static (float minX, float maxX, float minY, float maxY) ScreenRect(Camera cam, Bounds bounds)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 screen = cam.WorldToScreenPoint(corner);
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }
            return (minX, maxX, minY, maxY);
        }

        private static float ColorDistance(Color x, Color y)
        {
            float dr = x.r - y.r;
            float dg = x.g - y.g;
            float db = x.b - y.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
