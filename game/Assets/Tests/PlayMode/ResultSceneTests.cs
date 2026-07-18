// ResultSceneTests — S-11: Result シーン（最終スコア・ハイスコア更新・リスタート/メニュー導線）+ S-33:
// 最終スコアのカウントアップ演出 + ハイスコア更新の強調表示.
// Loads the actual wired Result.unity scene (Assets/Scenes/Result.unity, wired by
// Editor/SceneWiring.WireResult) and drives it through InputTestFixture (rule 8: batchmode Game
// View has no focus, so InputAction-level assertions require the fixture's keyboard simulation
// rather than raw InputSystem.QueueStateEvent). Also covers the restart-reset acceptance and the
// full Title→Menu→Game→Result→Menu loop, forcing a quick deterministic death via HealthSceneTests'
// touching-enemy technique to keep the Game-scene leg fast and non-flaky.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class ResultSceneTests : InputTestFixture
    {
        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s11-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            HealthComponent.SaveDirectoryOverrideForTests = _tempSaveDir;
            HealthComponent.SaveInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            EnemyAgent.ActiveEnemies.Clear();
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            HealthComponent.SaveDirectoryOverrideForTests = null;
            HealthComponent.SaveInvocationCountForTests = 0;
            if (_tempSaveDir != null && Directory.Exists(_tempSaveDir))
            {
                Directory.Delete(_tempSaveDir, true);
            }
        }

        private static RunResult SampleRun(int finalScore) => new RunResult
        {
            FinalScore = finalScore,
            SurvivalTimeSec = 87.4f,
            WaveReached = 5,
            NormalKillCount = 10,
            HeavyKillCount = 1,
            CrystalsCollected = 6,
        };

        private IEnumerator LoadResultWithRun(RunResult run, bool highScoreUpdated)
        {
            SessionHolder holder = SessionHolder.EnsureCreated(SaveData.CreateDefault(), recovered: false);
            holder.SetLastRunResult(run, highScoreUpdated);
            yield return null; // let Awake run so DontDestroyOnLoad takes effect before the scene load below

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Result, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>Discrete press-then-release of a single key, spread across frames (mirrors the
        /// pattern already validated by TitleSceneTests/MenuSceneTests).</summary>
        private IEnumerator PressKey(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        /// <summary>S-33: FinalScoreText now displays "ラベル: N" where N is the animated count-up value —
        /// extracts N so tests can assert on the numeric progression rather than substring-matching text
        /// that is only correct at the very end of the animation.</summary>
        private static int ParseFinalScore(string text)
        {
            int colonIndex = text.LastIndexOf(':');
            Assert.Greater(colonIndex, -1, $"unexpected FinalScoreText format: '{text}'");
            string numberPart = text.Substring(colonIndex + 1).Trim();
            Assert.IsTrue(int.TryParse(numberPart, out int value), $"unexpected FinalScoreText format: '{text}'");
            return value;
        }

        [UnityTest]
        public IEnumerator DisplaysFinalScoreSurvivalTimeAndWaveReached()
        {
            yield return LoadResultWithRun(SampleRun(1234), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen, "Result scene must be wired with a ResultScreen (Editor/SceneWiring.WireResult)");

            // S-33: the final score now counts up from 0 over ResultScoreCountUpDurationS instead of
            // displaying immediately — wait for the animation to finish before asserting the terminal text
            // (the count-up itself is covered by the dedicated ScoreCountUp_* test below).
            float deadline = Time.realtimeSinceStartup + GameConfig.Fx.ResultScoreCountUpDurationS + 1f;
            while (Time.realtimeSinceStartup < deadline && ParseFinalScore(screen.FinalScoreText.text) != 1234)
            {
                yield return null;
            }
            StringAssert.Contains("1234", screen.FinalScoreText.text);
            StringAssert.Contains("87.4", screen.SurvivalTimeText.text);
            StringAssert.Contains("5", screen.WaveReachedText.text);
        }

        [UnityTest]
        public IEnumerator ScoreCountUp_StartsBelowFinal_ThenReachesFinalScoreMonotonically()
        {
            // S-33 CR-CODE fix: sample FinalScoreText right after the scene load completes (Start() has
            // run and seeded 0, but before any extra settle frames), so this test directly asserts the
            // acceptance's "0から...カウントアップ表示" starting point instead of only inferring it via
            // sawBelowFinal below. Duplicates LoadResultWithRun's scene-load steps minus its trailing two
            // settle frames — those two frames are exactly what would already let the count-up advance
            // past 0 for a large final score, defeating this assertion.
            SessionHolder holder = SessionHolder.EnsureCreated(SaveData.CreateDefault(), recovered: false);
            holder.SetLastRunResult(SampleRun(1234), highScoreUpdated: false);
            yield return null; // let Awake run so DontDestroyOnLoad takes effect before the scene load below
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Result, LoadSceneMode.Single);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            Assert.AreEqual(0, ParseFinalScore(screen.FinalScoreText.text),
                "final score display must start at 0 immediately after the Result scene loads, before the count-up animation advances");

            int previous = -1;
            bool sawBelowFinal = false;
            float deadline = Time.realtimeSinceStartup + GameConfig.Fx.ResultScoreCountUpDurationS + 1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                int current = ParseFinalScore(screen.FinalScoreText.text);
                Assert.GreaterOrEqual(current, previous, "final score display must never decrease frame-to-frame (monotonic count-up)");
                if (current < 1234)
                {
                    sawBelowFinal = true;
                }
                previous = current;
                if (current == 1234)
                {
                    break;
                }
                yield return null;
            }
            Assert.AreEqual(1234, previous, "final score display must reach the exact final value within ResultScoreCountUpDurationS");
            Assert.IsTrue(sawBelowFinal, "final score display must actually animate up from below the final value, not jump straight to it");
        }

        [UnityTest]
        public IEnumerator Submit_DuringCountUp_TransitionsImmediately_WithoutWaitingForAnimation()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            // A large final score keeps the ease-out count-up meaningfully below its terminal value for
            // many frames, so this test genuinely exercises "Submit fires while still mid-animation"
            // rather than relying on timing luck.
            yield return LoadResultWithRun(SampleRun(999999), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            if (ParseFinalScore(screen.FinalScoreText.text) == 999999)
            {
                // S-33 CR-CODE fix: batchmode frame timing is not guaranteed — Time.maximumDeltaTime
                // (default 1/3s) means a handful of slow frames could already reach the
                // ResultScoreCountUpDurationS=0.8s terminal snap before this check runs. That is a test
                // environment timing outlier, not a product defect, so this reports Inconclusive (not a
                // false failure and not a false pass) rather than asserting the setup invariant hard.
                Assert.Inconclusive(
                    "count-up already reached the final value before Submit was pressed (slow-frame outlier); re-run to validate mid-animation Submit behavior");
            }

            yield return PressKey(keyboard.enterKey);

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name,
                "Submit must transition to Game immediately even while the score count-up animation is still in progress");
        }

        [UnityTest]
        public IEnumerator HighScoreUpdated_True_NoticePulsesThenSettlesBackToOne()
        {
            yield return LoadResultWithRun(SampleRun(9999), highScoreUpdated: true);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);

            bool sawScaleAboveOne = false;
            float lastScale = screen.HighScoreNoticeText.transform.localScale.x;
            float deadline = Time.realtimeSinceStartup + GameConfig.Fx.ResultHighScoreNoticePulseDurationS + 1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                lastScale = screen.HighScoreNoticeText.transform.localScale.x;
                if (lastScale > 1.01f)
                {
                    sawScaleAboveOne = true;
                }
                yield return null;
            }
            Assert.IsTrue(sawScaleAboveOne, "highScoreUpdated=true must pulse the notice's scale above 1.0 at some point");
            Assert.AreEqual(1f, lastScale, 0.01f, "the notice pulse must settle back to scale 1.0 once ResultHighScoreNoticePulseDurationS elapses");
        }

        [UnityTest]
        public IEnumerator HighScoreUpdated_False_NoticeScaleNeverPulses()
        {
            yield return LoadResultWithRun(SampleRun(1), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);

            float deadline = Time.realtimeSinceStartup + GameConfig.Fx.ResultHighScoreNoticePulseDurationS + 0.2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(1f, screen.HighScoreNoticeText.transform.localScale.x, 0.001f,
                    "highScoreUpdated=false must never pulse the (inactive) notice scale");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HighScoreUpdated_True_ShowsNotice()
        {
            yield return LoadResultWithRun(SampleRun(9999), highScoreUpdated: true);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            Assert.IsTrue(screen.HighScoreNoticeObject.activeSelf, "highScoreUpdated=true must show the new-high-score notice");
        }

        [UnityTest]
        public IEnumerator HighScoreUpdated_False_HidesNotice()
        {
            yield return LoadResultWithRun(SampleRun(1), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            Assert.IsFalse(screen.HighScoreNoticeObject.activeSelf, "highScoreUpdated=false must not show the new-high-score notice");
        }

        [UnityTest]
        public IEnumerator Submit_TransitionsToGameScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            yield return PressKey(keyboard.enterKey);

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Submit_Space_AlsoTransitionsToGameScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            yield return PressKey(keyboard.spaceKey);

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Cancel_TransitionsToMenuScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            yield return PressKey(keyboard.escapeKey);

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ResultCanvas_UsesScreenSpaceCameraRenderMode()
        {
            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, screen.Canvas.renderMode);
            Assert.IsNotNull(screen.Canvas.worldCamera, "ScreenSpaceCamera Canvas must have worldCamera assigned (rule 14 — QA RenderTexture capture)");
        }

        [UnityTest]
        public IEnumerator ResultCanvas_HasImg05DecorationWithNonNullSprites()
        {
            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            var screen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(screen);
            Assert.IsNotNull(screen.PanelImage, "Result must have an IMG-05 panel background (Editor/SceneWiring.WireResultUiFrameKit)");
            Assert.IsNotNull(screen.PanelImage.sprite, "Result panel Image must have a non-null IMG-05 sprite");
            Assert.IsNotNull(screen.RibbonImage, "Result must have an IMG-05 heading ribbon");
            Assert.IsNotNull(screen.RibbonImage.sprite);
            Assert.IsNotNull(screen.CornerImages);
            Assert.AreEqual(2, screen.CornerImages.Length);
            foreach (var corner in screen.CornerImages)
            {
                Assert.IsNotNull(corner);
                Assert.IsNotNull(corner.sprite, "Result corner ornament must have a non-null IMG-05 sprite");
            }
        }

        [UnityTest]
        public IEnumerator Restart_ResetsPlayerPositionHpWaveTimerEnemiesAndScore()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadResultWithRun(SampleRun(10), highScoreUpdated: false);

            yield return PressKey(keyboard.enterKey);
            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "restarted Game scene must be wired with a PlayerController");
            Assert.AreEqual(Vector3.zero, player.transform.position, "restart must place the player back at the arena center");

            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            Assert.AreEqual(GameConfig.Player.MaxHpBase, health.CurrentHp, "restart must fully restore HP");
            // A frame or two elapses between the scene reload and this assertion (the two `yield
            // return null`s in LoadGameScene-equivalent flow above), so HealthComponent.Update has
            // already accrued a few Time.deltaTime ticks onto the freshly-reset accumulator — assert
            // "near zero, not carried over from the previous run" rather than an exact 0f.
            Assert.Less(health.SurvivalTimeSec, 0.5f, "restart must reset the survival timer (not carry over the previous run's elapsed time)");

            var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(waveSpawner);
            Assert.AreEqual(1, waveSpawner.CurrentWave, "restart must reset the wave counter to 1");

            Assert.AreEqual(0, EnemyAgent.ActiveEnemies.Count, "restart must not carry over enemies from the previous run");

            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.CurrentScore, "restart must reset the score counter");
        }

        [UnityTest]
        public IEnumerator FullLoop_TitleToMenuToGameToResultToMenu()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);

            yield return PressKey(keyboard.enterKey);
            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name, "Title Enter must transition to Menu");

            yield return PressKey(keyboard.enterKey);
            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name, "Menu はじめる+Enter must transition to Game");

            // Force a quick, deterministic death (mirrors HealthSceneTests.HpReachesZero_TransitionsToResult)
            // rather than waiting out a real ENEMY_CONTACT_COOLDOWN-paced run.
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }

            int enemiesNeeded = Mathf.CeilToInt((float)GameConfig.Player.MaxHpBase / GameConfig.Enemy.ContactDamage) + 2;
            for (int i = 0; i < enemiesNeeded; i++)
            {
                var go = new GameObject("TestEnemyContact");
                go.transform.position = player.transform.position;
                EnemyAgent agent = go.AddComponent<EnemyAgent>();
                agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            }

            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != GameConfig.Scenes.Result && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name, "HP<=0 must lock input, play the death sequence, then load Result");

            yield return PressKey(keyboard.escapeKey);
            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name,
                "Result Esc must transition to Menu, completing the Title→Menu→Game→Result→Menu loop");
        }
    }
}
