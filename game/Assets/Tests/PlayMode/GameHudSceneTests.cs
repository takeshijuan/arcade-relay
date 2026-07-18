// GameHudSceneTests — S-10: Game HUD（HP/ダッシュCD/ウェーブ/スコア）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → GameHud/GameHudController root) and
// exercises each HUD element against the same triggers earlier stories' scene tests already use
// (touching-enemy contact for HP — mirrors HealthSceneTests; D+Space dash — mirrors DashSceneTests;
// Time.timeScale fast-forward for wave — mirrors EnemySpawnSceneTests; kill+auto-pickup for score —
// mirrors CrystalSceneTests), asserting the HUD reflects the underlying Components state rather than
// re-deriving it (HUD is 表示専任 — conventions.md).
using System.Collections;
using System.Globalization;
using ForgeGame.Components;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class GameHudSceneTests : InputTestFixture
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

        private static int ParseScore(string text) =>
            int.Parse(text.Substring(GameConfig.Ui.HudScoreLabelPrefix.Length), CultureInfo.InvariantCulture);

        private static int ParseWave(string text) =>
            int.Parse(text.Substring(GameConfig.Ui.HudWaveLabelPrefix.Length), CultureInfo.InvariantCulture);

        [UnityTest]
        public IEnumerator ContinuousContact_ReducesHpText_AndBarFill()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud, "Game scene must be wired with a GameHud (Editor/SceneWiring.WireGame)");
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);

            yield return null; // let GameHudController.Update run at least once at full HP
            Assert.AreEqual($"{GameConfig.Ui.HudHpLabel} {GameConfig.Player.MaxHpBase}/{GameConfig.Player.MaxHpBase}", hud.HpText.text);
            Assert.AreEqual(1f, hud.HpBarFill.fillAmount, 1e-3f);

            CreateEnemy(player.transform.position);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (health.CurrentHp == GameConfig.Player.MaxHpBase && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            yield return null; // one more frame so GameHudController.Update reflects the new HP

            Assert.Less(health.CurrentHp, GameConfig.Player.MaxHpBase, "test setup: contact must have reduced HP");
            Assert.AreEqual($"{GameConfig.Ui.HudHpLabel} {health.CurrentHp}/{health.EffectiveMaxHp}", hud.HpText.text,
                "HUD HP text must reflect Components/HealthComponent.CurrentHp after a hit");
            Assert.Less(hud.HpBarFill.fillAmount, 1f, "HUD HP bar fill must decrease after a hit");
        }

        [UnityTest]
        public IEnumerator DashActivation_ShowsCooldownRemaining_OnHud()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            yield return null;
            Assert.AreEqual(GameConfig.Ui.HudDashReadyText, hud.DashText.text, "HUD must show the ready state before any dash");
            Assert.AreEqual(1f, hud.DashBarFill.fillAmount, 1e-3f);

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.IsDashing && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(player.IsDashing, "test setup: dash must have activated");
            yield return null; // one more frame so GameHudController.Update reflects the fresh cooldown

            Assert.Greater(player.DashCooldownRemaining, 0f, "test setup: cooldown must be counting down right after activation");
            Assert.AreNotEqual(GameConfig.Ui.HudDashReadyText, hud.DashText.text,
                "HUD must show the counting-down state (not the ready text) while DASH_COOLDOWN is active");
            StringAssert.StartsWith(GameConfig.Ui.HudDashLabel, hud.DashText.text, "HUD counting-down text must be formatted with the DASH label prefix");
            Assert.Less(hud.DashBarFill.fillAmount, 1f, "HUD dash bar fill must be less than full while cooling down");

            Release(keyboard.spaceKey);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator OverTime_WaveNumber_OnHud_Increases()
        {
            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            yield return null;
            Assert.AreEqual(1, ParseWave(hud.WaveText.text), "HUD must start at wave 1");

            // WAVE_DURATION=30s; fast-forward well past the wave-2 boundary.
            Time.timeScale = 50f;
            yield return new WaitForSecondsRealtime(1.0f); // ~50 simulated seconds
            Time.timeScale = 1f;
            yield return null;

            Assert.Greater(ParseWave(hud.WaveText.text), 1, "HUD wave number must increase once WAVE_DURATION elapses (gdd: 1+floor(elapsedSec/WAVE_DURATION))");
        }

        [UnityTest]
        public IEnumerator KillAndAutoPickup_IncreasesScore_OnHud()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats);

            yield return null;
            int scoreBefore = ParseScore(hud.ScoreText.text); // baseline (small survival-time term may already be non-zero)

            // Close enough to be within both AUTO_ATTACK_RANGE and CRYSTAL_PICKUP_RADIUS at kill time.
            Vector3 spawnPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 0.5f, 0f, 0f);
            CreateEnemy(spawnPosition);

            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (stats.CrystalsCollected == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Time.timeScale = 1f;
            yield return null; // one more frame so GameHudController.Update reflects the fresh score

            Assert.AreEqual(1, stats.NormalKillCount, "test setup: enemy must have been killed");
            Assert.AreEqual(1, stats.CrystalsCollected, "test setup: dropped crystal must have been auto-collected");

            int scoreAfter = ParseScore(hud.ScoreText.text);
            Assert.GreaterOrEqual(scoreAfter, scoreBefore + GameConfig.Score.PerKillNormal + GameConfig.Score.PerCrystal,
                "HUD score must increase by at least SCORE_PER_KILL_NORMAL+SCORE_PER_CRYSTAL after a kill+auto-pickup (survival-time term only ever adds on top)");
        }

        [UnityTest]
        public IEnumerator GameHudCanvas_UsesScreenSpaceCameraRenderMode()
        {
            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, hud.Canvas.renderMode);
            Assert.IsNotNull(hud.Canvas.worldCamera, "ScreenSpaceCamera Canvas must have worldCamera assigned (rule 14 — QA RenderTexture capture)");
        }

        [UnityTest]
        public IEnumerator GameHudCanvas_HasImg05PanelsWithNonNullSprites()
        {
            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            Assert.IsNotNull(hud.StatPanelImages, "HUD must have IMG-05 panel backgrounds behind each stat group (Editor/SceneWiring.WireGameUiFrameKit)");
            Assert.AreEqual(4, hud.StatPanelImages.Length, "HUD must have one panel per stat group (HP/Dash/Wave/Score)");
            foreach (var panel in hud.StatPanelImages)
            {
                Assert.IsNotNull(panel, "HUD stat panel Image must be built");
                Assert.IsNotNull(panel.sprite, "HUD stat panel must have a non-null IMG-05 sprite");
            }
        }
    }
}
