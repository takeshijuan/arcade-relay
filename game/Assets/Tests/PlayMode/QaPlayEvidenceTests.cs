// QaPlayEvidenceTests — QA-PLAY visual evidence (gates.md QA-PLAY 観点2/6c, tech-stack-unity.md
// 「QA-PLAY の実行方法」節3). Captures a RenderTexture screenshot of each scene in the required
// Title→Menu→Game→Result→Menu loop for qa-lead's mandatory visual inspection (Read + magick mean
// check). This file lives under Assets/Tests/ (qa-lead's permitted write scope per
// .claude/agents/qa-lead.md — production code under Assets/Scripts/ is off-limits, but acceptance
// / evidence test code here is explicitly allowed). No production code is modified by this file.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class QaPlayEvidenceTests : InputTestFixture
    {
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

        private static SaveData BuildPopulatedSave()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 4200;
            save.bestSurvivalTimeSec = 128.5f;
            save.bestWaveReached = 7;
            save.totalRunsPlayed = 12;
            save.totalKillCount = 340;
            save.totalCrystalsEarned = 560;
            save.crystalBalance = 75;
            save.upgradeAttackLevel = 2;
            save.upgradeMoveSpeedLevel = 1;
            save.upgradeMaxHpLevel = 3;
            save.bgmVolume = 0.6f;
            save.sfxVolume = 0.4f;
            return save;
        }

        // Mirrors AssetIntegrationSceneTests.CaptureEvidenceScreenshot (tech-stack-unity.md「QA-PLAY
        // の実行方法」3: ScreenCapture.CaptureScreenshot doesn't work in batchmode — render Main
        // Camera to a RenderTexture instead. UI is captured too because rule 14 mandates
        // ScreenSpaceCamera canvases with worldCamera assigned).
        private static void CaptureEvidenceScreenshot(string fileName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"no Main Camera found while capturing '{fileName}'");
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

        private IEnumerator PressKey(UnityEngine.InputSystem.Controls.ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_Title()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(screen);
            CaptureEvidenceScreenshot("qa-title.png");
        }

        [UnityTest]
        public IEnumerator Capture_Menu_StartTab()
        {
            SessionHolder.EnsureCreated(BuildPopulatedSave(), recovered: false);
            yield return null;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(screen);
            CaptureEvidenceScreenshot("qa-menu-start.png");
        }

        [UnityTest]
        public IEnumerator Capture_Menu_StatsTab()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            SessionHolder.EnsureCreated(BuildPopulatedSave(), recovered: false);
            yield return null;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            yield return PressKey(keyboard.eKey); // Start -> Stats
            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Stats].activeSelf);
            CaptureEvidenceScreenshot("qa-menu-stats.png");
        }

        [UnityTest]
        public IEnumerator Capture_Game()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            // Let a couple of waves spawn so the screenshot shows enemies approaching (P-03 group
            // pressure), not an empty arena.
            yield return new WaitForSecondsRealtime(1.5f);
            CaptureEvidenceScreenshot("qa-game.png");
        }

        [UnityTest]
        public IEnumerator Capture_Game_MidWaveSwarmDensity()
        {
            // P-03 (群れ密度の圧力) visual evidence: fast-forward several waves so multiple enemies are
            // simultaneously visible converging on the player, not just the single early-wave enemy
            // captured by Capture_Game.
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            Time.timeScale = 20f;
            yield return new WaitForSecondsRealtime(1.5f); // ~30 simulated seconds -> into Wave 2/3
            Time.timeScale = 1f;
            yield return null;

            Assert.Greater(EnemyAgent.ActiveEnemies.Count, 0, "expected multiple enemies on screen for the swarm-density screenshot");
            CaptureEvidenceScreenshot("qa-game-swarm.png");
        }

        [UnityTest]
        public IEnumerator Capture_Swarmer_Closeup()
        {
            // MDL-02 (swarmer) degraded to rig_type=none / static prefab (Integrate report: quadruped
            // auto-rig unavailable). Close-up capture, mirroring AssetIntegrationSceneTests' hero
            // close-up, so the static pose can be visually confirmed as an intact (non-glitched)
            // model rather than a genuinely broken/inverted mesh.
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner);
            GameObject prefab = spawner.EnemyPrefabForTests;
            Assert.IsNotNull(prefab);

            GameObject instance = Object.Instantiate(prefab);
            instance.transform.position = new Vector3(0f, 0f, 2.5f);
            instance.GetComponent<EnemyAgent>().Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);

            Camera cam = Camera.main;
            Assert.IsNotNull(cam);
            Vector3 camStart = cam.transform.position;
            Quaternion rotStart = cam.transform.rotation;
            cam.transform.position = new Vector3(0f, 1.2f, -1.5f);
            cam.transform.LookAt(instance.transform.position + Vector3.up * 0.8f);

            yield return null;
            CaptureEvidenceScreenshot("qa-swarmer-closeup.png");

            cam.transform.position = camStart;
            cam.transform.rotation = rotStart;
            Object.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator Capture_Result()
        {
            var run = new RunResult
            {
                FinalScore = 3456,
                SurvivalTimeSec = 96.2f,
                WaveReached = 6,
                NormalKillCount = 22,
                HeavyKillCount = 2,
                CrystalsCollected = 14,
            };
            SessionHolder holder = SessionHolder.EnsureCreated(SaveData.CreateDefault(), recovered: false);
            holder.SetLastRunResult(run, highScoreUpdated: true);
            yield return null;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Result, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            CaptureEvidenceScreenshot("qa-result.png");
        }
    }
}
