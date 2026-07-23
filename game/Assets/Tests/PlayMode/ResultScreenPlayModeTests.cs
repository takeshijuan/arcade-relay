// ResultScreenPlayModeTests.cs — S-09 acceptance: Result シーンが勝敗・今回 finalScore・
// ハイスコア（更新時強調）・今回解放した新規実績を表示し、「もう一度」→Game、「メニューへ」→Menu の
// 両遷移をクリック擬似発行で検証できる。加えて Title→Menu→Game→Result→Menu の必須シーン遷移1周が
// 成立することを検証する（contract §11 正準フロー）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class ResultScreenPlayModeTests : InputTestFixture
    {
        private static RunResult WinResult() => new RunResult
        {
            IsWin = true,
            CoreHpRemaining = GameConfig.Core.HpMax,
            KillCount = 17,
            AoeKillCount = 3,
            UsedBuildSpots = 4,
            ClearTimeSec = 123.4f,
        };

        private static Vector2 ScreenPointOf(RectTransform rect, Camera cam) =>
            RectTransformUtility.WorldToScreenPoint(cam, rect.position);

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            GameFlow.ClearRunResult();
            GameFlow.SetCurrentSaveData(null);
            GameFlow.SetRecovered(false);
            GameFlow.SetSaveFailed(false); // CR-CODE iter1 #1: SaveFailed テスト間の漏れを防ぐ
            yield break;
        }

        [UnityTest]
        public IEnumerator ResultCanvas_IsScreenSpaceCamera()
        {
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen, "ResultScreen コンポーネントが Result シーンに存在しない");

            var canvas = resultScreen.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Result の Canvas が見つからない");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.IsNotNull(canvas.worldCamera, "worldCamera が未割当");
        }

        [UnityTest]
        public IEnumerator DisplaysOutcome_Score_And_HighlightsNewHighScore()
        {
            RunResult result = WinResult();
            int expectedScore = ScoreSystem.ComputeFinalScore(result);

            SaveData prev = SaveData.CreateDefault();
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen);

            var outcomeText = resultScreen.InfoPanel.Find("OutcomeText").GetComponent<Text>();
            Assert.AreEqual("勝利", outcomeText.text);

            var scoreText = resultScreen.InfoPanel.Find("ScoreText").GetComponent<Text>();
            Assert.IsTrue(scoreText.text.Contains(expectedScore.ToString()), $"finalScore が表示されていない: {scoreText.text}");

            var highScoreText = resultScreen.InfoPanel.Find("HighScoreText").GetComponent<Text>();
            Assert.IsTrue(highScoreText.text.Contains("更新"), $"新規ハイスコアの強調表示が無い: {highScoreText.text}");
            Assert.AreEqual(expectedScore, post.highScore);
        }

        // S-20 acceptance: ベストクリアタイム未記録（-1）は「--」表示、記録済みは秒数表示する。

        [UnityTest]
        public IEnumerator UnrecordedBestClearTime_ShowsDoubleDash()
        {
            var result = new RunResult
            {
                IsWin = false, // 敗北ランでは MetaProgression.ApplyRunResult が bestClearTimeSec を更新しない
                CoreHpRemaining = 0,
                KillCount = 1,
                AoeKillCount = 0,
                UsedBuildSpots = 6,
                ClearTimeSec = 40f,
            };
            SaveData prev = SaveData.CreateDefault(); // bestClearTimeSec = -1（未記録）
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen, "ResultScreen コンポーネントが Result シーンに存在しない");
            var bestClearTimeTextTransform = resultScreen.InfoPanel.Find("BestClearTimeText");
            Assert.IsNotNull(bestClearTimeTextTransform, "BestClearTimeText が存在しない（UI 構築退行の疑い）");
            var bestClearTimeText = bestClearTimeTextTransform.GetComponent<Text>();
            Assert.IsNotNull(bestClearTimeText, "BestClearTimeText に Text コンポーネントが無い");
            Assert.IsTrue(bestClearTimeText.text.Contains("--"),
                $"未記録のベストクリアタイムが「--」表示になっていない: {bestClearTimeText.text}");
        }

        [UnityTest]
        public IEnumerator RecordedBestClearTime_ShowsFormattedSeconds()
        {
            RunResult result = WinResult(); // ClearTimeSec=123.4f・勝利ランのため bestClearTimeSec が確定する
            SaveData prev = SaveData.CreateDefault();
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen, "ResultScreen コンポーネントが Result シーンに存在しない");
            var bestClearTimeTextTransform = resultScreen.InfoPanel.Find("BestClearTimeText");
            Assert.IsNotNull(bestClearTimeTextTransform, "BestClearTimeText が存在しない（UI 構築退行の疑い）");
            var bestClearTimeText = bestClearTimeTextTransform.GetComponent<Text>();
            Assert.IsNotNull(bestClearTimeText, "BestClearTimeText に Text コンポーネントが無い");
            Assert.IsTrue(bestClearTimeText.text.Contains("123.4"),
                $"ベストクリアタイムが反映されていない: {bestClearTimeText.text}");
        }

        // CR-CODE iter1 #1(major)対応: GameFlow.SaveFailed（直近 Persistence.Save 失敗）が Result 画面上で
        // 通知されることを検証する（従来は購読者がゼロでプレイヤーから不可視だった）。

        [UnityTest]
        public IEnumerator SaveFailedFlag_ShowsSaveFailedNotice()
        {
            GameFlow.SetSaveFailed(true);
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen);
            Assert.IsTrue(resultScreen.SaveFailedNoticeVisible,
                "GameFlow.SaveFailed=true なのに Result の保存失敗通知が表示されていない");
        }

        [UnityTest]
        public IEnumerator NotSaveFailed_HidesSaveFailedNotice()
        {
            GameFlow.SetSaveFailed(false);
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen);
            Assert.IsTrue(resultScreen.SaveFailedNoticeTextExists, "SaveFailedNoticeText が構築されていない（UI 未構築の疑い）");
            Assert.IsFalse(resultScreen.SaveFailedNoticeVisible,
                "GameFlow.SaveFailed=false なのに Result の保存失敗通知が表示されている");
        }

        [UnityTest]
        public IEnumerator LossResult_ShowsLossOutcome_NoHighlight()
        {
            var result = new RunResult
            {
                IsWin = false,
                CoreHpRemaining = 0,
                KillCount = 2,
                AoeKillCount = 0,
                UsedBuildSpots = 6,
                ClearTimeSec = 40f,
            };

            SaveData prev = SaveData.CreateDefault();
            prev.highScore = 999999; // 今回スコアより十分大きいので更新されない
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var outcomeText = resultScreen.InfoPanel.Find("OutcomeText").GetComponent<Text>();
            Assert.AreEqual("敗北", outcomeText.text);

            var highScoreText = resultScreen.InfoPanel.Find("HighScoreText").GetComponent<Text>();
            Assert.IsFalse(highScoreText.text.Contains("更新"), $"更新されていないのに強調表示されている: {highScoreText.text}");
        }

        [UnityTest]
        public IEnumerator DisplaysNewlyUnlockedAchievement_FirstVictory()
        {
            RunResult result = WinResult();
            SaveData prev = SaveData.CreateDefault(); // totalWins=0 → このランで ACH-01 が新規解放される
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var newAchText = resultScreen.InfoPanel.Find("NewAchievementsText").GetComponent<Text>();
            Assert.IsTrue(newAchText.text.Contains("ACH-01"), $"新規実績(ACH-01)が表示されていない: {newAchText.text}");
        }

        [UnityTest]
        public IEnumerator NoNewAchievements_ShowsNoneLabel()
        {
            var result = new RunResult
            {
                IsWin = false,
                CoreHpRemaining = 0,
                KillCount = 1,
                AoeKillCount = 0,
                UsedBuildSpots = 6,
                ClearTimeSec = 40f,
            };
            SaveData prev = SaveData.CreateDefault();
            SaveData post = MetaProgression.ApplyRunResult(prev, result);
            GameFlow.SetCurrentSaveData(post);
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var newAchText = resultScreen.InfoPanel.Find("NewAchievementsText").GetComponent<Text>();
            Assert.IsTrue(newAchText.text.Contains("なし"), $"新規実績なしの表示になっていない: {newAchText.text}");
        }

        [UnityTest]
        public IEnumerator ClickPlayAgain_TransitionsToGame_AndClearsRunResult()
        {
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(resultScreen.PlayAgainButtonRect, resultScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);
            Assert.IsFalse(GameFlow.TryGetLastRunResult(out _), "もう一度→Game 後も RunResult が残っている");
        }

        [UnityTest]
        public IEnumerator ClickBackToMenu_TransitionsToMenu_AndClearsRunResult()
        {
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;

            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(resultScreen.BackToMenuButtonRect, resultScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
            Assert.IsFalse(GameFlow.TryGetLastRunResult(out _), "メニューへ→Menu 後も RunResult が残っている");
        }

        [UnityTest]
        public IEnumerator FullLoop_TitleMenuGameResultMenu_CompletesRequiredSceneCycle()
        {
            // Title → Menu（クリック）
            GameFlow.SetRecovered(false);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);

            // Menu → Game（「プレイ開始」クリック）
            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            Vector2 playPoint = ScreenPointOf(menuScreen.PlayButtonRect, menuScreen.UiCamera);
            Move(mouse.position, playPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);

            // Game → Result（ラン確定は gameplay-engineer 領域〔S-06〕のため GameFlow.GoToResult で代表させる。
            // GameFlowPlayModeTests.cs の既存パターンと同じ扱い）
            GameFlow.GoToResult(WinResult());
            yield return null;
            yield return null;
            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name);

            // Result → Menu（「メニューへ」クリック）
            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            Assert.IsNotNull(resultScreen);
            Vector2 menuPoint = ScreenPointOf(resultScreen.BackToMenuButtonRect, resultScreen.UiCamera);
            Move(mouse.position, menuPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }
    }
}
