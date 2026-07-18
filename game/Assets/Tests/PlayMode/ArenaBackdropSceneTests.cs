// ArenaBackdropSceneTests — S-26: 背景/スカイボックス統合（アリーナ外周の void を key image 背景へ置換）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → ArenaBackdrop root, Editor/
// AssetIntegration.PatchArenaBackdrop assigns IMG-06) and machine-verifies the S-26 acceptance:
// RenderSettings.skybox is set (Skybox/6 Sided, all six faces non-null, not InternalErrorShader), the
// texture was sourced through GameConfig.AssetKeys (not a hardcoded path), and a RenderTexture capture
// shows a foreground object (the player) distinguishable from the background — not washed out to white.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class ArenaBackdropSceneTests
    {
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";
        private const string ArenaBackdropRootName = "ArenaBackdrop";

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

        [UnityTest]
        public IEnumerator ArenaBackdrop_RootExists_AndWiresRenderSettingsSkybox()
        {
            yield return LoadGameScene();

            GameObject root = GameObject.Find(ArenaBackdropRootName);
            Assert.IsNotNull(root, "Game scene must be wired with an ArenaBackdrop root (Editor/SceneWiring.WireGame)");
            Assert.IsNotNull(root.GetComponent<ArenaBackdrop>(), "ArenaBackdrop root must carry the ArenaBackdrop component");

            Assert.IsNotNull(RenderSettings.skybox, "RenderSettings.skybox must be set (IMG-06 backdrop — S-26)");
            Assert.AreNotEqual(InternalErrorShaderName, RenderSettings.skybox.shader.name,
                "Skybox material fell back to InternalErrorShader (pink) — not a valid shader");

            Assert.AreSame(RenderSettings.skybox, ArenaBackdrop.LastAppliedSkyboxForTests,
                "ArenaBackdrop.Start() must be the one that applied RenderSettings.skybox this session");

            foreach (string faceProperty in new[] { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" })
            {
                Assert.IsNotNull(RenderSettings.skybox.GetTexture(faceProperty),
                    $"Skybox face '{faceProperty}' must not be null (四方に背景が見える — no gap in any direction)");
            }
        }

        [UnityTest]
        public IEnumerator ArenaBackdrop_TextureIsWiredViaGameConfigAssetKey_NotHardcodedPath()
        {
            yield return LoadGameScene();

            // Runtime PlayMode tests avoid UnityEditor.AssetDatabase (this asmdef targets all platforms,
            // not Editor-only — mirrors every other file in Tests/PlayMode/, none of which reference
            // UnityEditor). Instead, verify the wiring end-to-end at runtime: GameConfig.AssetKeys is
            // non-empty (the single source of truth Editor/AssetIntegration.PatchArenaBackdrop reads from
            // — rule 5) and the texture Editor/AssetIntegration actually baked onto ArenaBackdrop matches
            // the filename that path constant points at (import-time Texture2D.name defaults to the
            // asset's filename without extension).
            Assert.IsFalse(string.IsNullOrEmpty(GameConfig.AssetKeys.ArenaBackdropTexture),
                "GameConfig.AssetKeys.ArenaBackdropTexture must be a non-empty path constant (IMG-06)");
            string expectedName = Path.GetFileNameWithoutExtension(GameConfig.AssetKeys.ArenaBackdropTexture);

            Assert.IsNotNull(ArenaBackdrop.LastAppliedSkyboxForTests,
                "ArenaBackdrop.Start() must have applied a Skybox material this session");
            Texture appliedFrontFace = ArenaBackdrop.LastAppliedSkyboxForTests.GetTexture("_FrontTex");
            Assert.IsNotNull(appliedFrontFace, "Skybox '_FrontTex' must be assigned");
            Assert.AreEqual(expectedName, appliedFrontFace.name,
                "the Skybox face texture must be the asset GameConfig.AssetKeys.ArenaBackdropTexture points at (no separate hardcoded path)");

            // CR-CODE s-26 iteration 2 minor指摘#3 fix: the assertions above only covered the texture key
            // (GameConfig.AssetKeys.ArenaBackdropTexture) — the acceptance text explicitly says "背景テクスチャ
            // /マテリアルキー" (both), so the baked *material* key (GameConfig.AssetKeys.
            // ArenaBackdropSkyboxMaterial, added by Editor/AssetIntegration.BuildArenaBackdropSkyboxMaterial)
            // must be verified end-to-end too, mirroring the texture check above.
            Assert.IsFalse(string.IsNullOrEmpty(GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial),
                "GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial must be a non-empty path constant");
            string expectedMaterialName = Path.GetFileNameWithoutExtension(GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial);
            Assert.AreEqual(expectedMaterialName, ArenaBackdrop.LastAppliedSkyboxForTests.name,
                "the applied Skybox material must be the asset GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial points at (no separate hardcoded material path)");
        }

        [UnityTest]
        public IEnumerator Capture_BackdropVsForeground_Evidence_ForegroundNotWashedOutAndDistinguishableFromBackground()
        {
            // P-01: the background must read as a backdrop, not compete with/erase the foreground. Places
            // the player at the arena center (roughly frame-center under the fixed top-down camera — S-04
            // ArenaCameraMath.ComputeFixedPose looks at Vector3.zero) and samples that screen pixel against
            // the frame's four corners (reliably beyond the circular arena's silhouette, i.e. background-
            // only pixels — mirrors ArenaEnvironmentSceneTests.Capture_FourDirectionReadability_Evidence's
            // "place at a known world point, sample the rendered frame" technique).
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            player.transform.position = Vector3.zero;

            yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Camera.main must exist in the wired Game scene to capture backdrop evidence.");

            const int width = 960;
            const int height = 540;
            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;

            Vector3 playerScreenPoint = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 0.9f);

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
            File.WriteAllBytes(Path.Combine(dir, "arena-backdrop.png"), tex.EncodeToPNG());

            int px = Mathf.Clamp(Mathf.RoundToInt(playerScreenPoint.x), 0, width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(playerScreenPoint.y), 0, height - 1);
            Color foreground = SampleAverage(tex, px, py, 4);

            const int inset = 6;
            Color topLeft = SampleAverage(tex, inset, height - 1 - inset, 4);
            Color topRight = SampleAverage(tex, width - 1 - inset, height - 1 - inset, 4);
            Color bottomLeft = SampleAverage(tex, inset, inset, 4);
            Color bottomRight = SampleAverage(tex, width - 1 - inset, inset, 4);
            Color[] backgroundCorners = { topLeft, topRight, bottomLeft, bottomRight };

            Object.DestroyImmediate(tex);

            foreach (Color corner in backgroundCorners)
            {
                Assert.IsFalse(IsNearWhite(corner),
                    $"background corner pixel {corner} must not be washed out to near-white (背景輝度で前景が白飛びしない前提としてまず背景自体が白飛びしていないこと)");
            }
            Assert.IsFalse(IsNearWhite(foreground),
                $"foreground (player) pixel {foreground} must not be washed out to near-white by the background (背景輝度で前景が白飛びしない)");

            float maxCornerDistance = 0f;
            foreach (Color corner in backgroundCorners)
            {
                maxCornerDistance = Mathf.Max(maxCornerDistance, ColorDistance(foreground, corner));
            }
            Assert.Greater(maxCornerDistance, 0.05f,
                "foreground (player) pixel must be distinguishable from at least one background corner pixel (前景オブジェクトが背景に埋もれず判別可能)");

            // CR-CODE s-26 iteration 1 major指摘#5 fix: the corner assertions above only prove the
            // background isn't washed-out white and isn't identical to the foreground — a near-black void
            // (the pre-S-26 regression this story exists to fix) satisfies both just as easily as a real
            // IMG-06 skybox render would. IMG-06 is a vertical gradient (design/assets.md: dark horizon ->
            // purple/cyan -> magenta), so top-corner and bottom-corner pixels must differ from each other
            // when the skybox is actually rendering; a flat void (or a solid-color URP fallback clear)
            // would leave top and bottom corners equal and fail this assertion — unlike the corner-vs-
            // foreground checks above, which a uniform void passes.
            Color topAvg = Average(topLeft, topRight);
            Color bottomAvg = Average(bottomLeft, bottomRight);
            Assert.Greater(ColorDistance(topAvg, bottomAvg), 0.05f,
                $"top-corner average {topAvg} and bottom-corner average {bottomAvg} must differ — proves IMG-06's vertical gradient skybox is actually rendering (a void/flat-fallback background would leave top and bottom equal)");
        }

        private static Color Average(Color a, Color b) => (a + b) / 2f;

        private static Color SampleAverage(Texture2D tex, int centerX, int centerY, int radius)
        {
            Color sum = Color.black;
            int count = 0;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = Mathf.Clamp(centerX + dx, 0, tex.width - 1);
                    int y = Mathf.Clamp(centerY + dy, 0, tex.height - 1);
                    sum += tex.GetPixel(x, y);
                    count++;
                }
            }
            return sum / count;
        }

        private static bool IsNearWhite(Color c)
        {
            const float whiteThreshold = 0.97f;
            return c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold;
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
