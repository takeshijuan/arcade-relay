// QaVisualEvidencePlayModeTests.cs — QA-PLAY 用の視覚証跡テスト（qa-lead が追加。gates.md QA-PLAY 観点
// 「視覚証跡の機械検知＋目視」「必須シーン遷移 Title→Menu→Game→Result→Menu」「視覚サニティテスト」を満たす。
// 本テストは実装コードではなく検証専用（tech-stack-unity.md「QA-PLAY の実行方法」節に従う）。
// -nographics 不使用が前提。ScreenCapture.CaptureScreenshot が batchmode で機能しないため、
// Camera を RenderTexture にレンダリングして Texture2D.ReadPixels → EncodeToPNG で保存する。
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    public class QaVisualEvidencePlayModeTests
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        private static string EvidenceDir
        {
            get
            {
                // Application.dataPath == <repo>/game/Assets → 2階層上が repo root。
                string gameDir = Directory.GetParent(Application.dataPath).FullName;
                string repoRoot = Directory.GetParent(gameDir).FullName;
                string dir = Path.Combine(repoRoot, "qa", "evidence");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void CaptureSceneScreenshot(string fileName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"Camera.main が見つからない（シーン: {SceneManager.GetActiveScene().name}）");

            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                string path = Path.Combine(EvidenceDir, fileName);
                File.WriteAllBytes(path, png);
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        /// <summary>視覚サニティテスト: 全 Renderer に null/InternalErrorShader(ピンク)が無いこと。</summary>
        private static void AssertNoMissingOrPinkMaterials()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    Assert.IsNotNull(mat, $"Renderer '{renderer.name}' の materials[{i}] が null");
                    Assert.IsNotNull(mat.shader, $"Renderer '{renderer.name}' の material '{mat.name}' に shader が無い");
                    Assert.AreNotEqual(
                        "Hidden/InternalErrorShader", mat.shader.name,
                        $"Renderer '{renderer.name}' の material '{mat.name}' がピンク(InternalErrorShader)＝マテリアル欠落");
                }
            }
        }

        [UnityTest]
        public IEnumerator Title_Screenshot_And_MaterialSanity()
        {
            GameFlow.SetRecovered(false);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            AssertNoMissingOrPinkMaterials();
            CaptureSceneScreenshot("qa-visual-title.png");
        }

        [UnityTest]
        public IEnumerator Menu_Screenshot_And_MaterialSanity()
        {
            GameFlow.SetCurrentSaveData(ForgeGame.Systems.Meta.SaveData.CreateDefault());
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            AssertNoMissingOrPinkMaterials();
            CaptureSceneScreenshot("qa-visual-menu.png");

            GameFlow.SetCurrentSaveData(null);
        }

        [UnityTest]
        public IEnumerator Game_Screenshot_MaterialSanity_NaNCheck_AndCameraFacing()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
            // ウェーブスポーンや配置の初期化・敵の移動が数フレーム進むのを待つ（Animator/位置の固着検知のため）。
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            // QA-PLAY 視覚証跡: Game のスクリーンショットは「開始直後の空盤面」不可
            // （コアループの主要オブジェクトが写るフレームで撮る必要がある — gates.md QA-PLAY 視覚証跡）。
            // 実際のプレイ操作と同じ入口（BuildSpotController.TryPlaceTower）でタワー2種を設置してから撮影する。
            var build = UnityEngine.Object.FindFirstObjectByType<ForgeGame.Components.BuildSpotController>();
            Assert.IsNotNull(build, "Game シーンに BuildSpotController が見つからない");
            var placeBastion = build.TryPlaceTower(0, ForgeGame.TowerType.BastionCannon);
            Assert.IsTrue(placeBastion.Success, $"Bastion Cannon 設置に失敗: {placeBastion.FailureReason}");
            var placeArc = build.TryPlaceTower(1, ForgeGame.TowerType.ArcEmitter);
            Assert.IsTrue(placeArc.Success, $"Arc Emitter 設置に失敗: {placeArc.FailureReason}");
            yield return null;
            yield return null;

            AssertNoMissingOrPinkMaterials();

            // NaN 座標検査: シーン内の全 Transform（敵・タワー・コア含む）。
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in transforms)
            {
                Vector3 p = t.position;
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z),
                    $"Transform '{t.name}' の座標に NaN が含まれる: {p}");
            }

            // カメラ向き検査: メインカメラがシーン中心（原点近傍＝コア/経路の想定位置）を向いているか。
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");
            Vector3 approxSceneCenter = Vector3.zero;
            Vector3 toCenter = (approxSceneCenter - cam.transform.position);
            if (toCenter.sqrMagnitude > 0.0001f)
            {
                float dot = Vector3.Dot(cam.transform.forward, toCenter.normalized);
                Assert.Greater(dot, 0.2f,
                    $"Game シーンのメインカメラが主要被写体（シーン中心付近）を向いていない可能性 (dot={dot})");
            }

            CaptureSceneScreenshot("qa-visual-game.png");
        }

        [UnityTest]
        public IEnumerator Result_Screenshot_And_MaterialSanity()
        {
            var winResult = new RunResult
            {
                IsWin = true,
                CoreHpRemaining = GameConfig.Core.HpMax,
                KillCount = 17,
                AoeKillCount = 3,
                UsedBuildSpots = 4,
                ClearTimeSec = 123.4f,
            };
            GameFlow.GoToResult(winResult);
            yield return null;
            yield return null;
            yield return null;

            AssertNoMissingOrPinkMaterials();
            CaptureSceneScreenshot("qa-visual-result.png");

            GameFlow.ClearRunResult();
        }
    }
}
