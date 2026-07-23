// GameFlowPlayModeTests.cs — S-01 acceptance:
//  (1) Boot 起動時に GameBootstrap がセーブをロードしエラー0で Title へ自動遷移する
//  (2) recovered フラグが GameFlow 経由で伝播する（実際の破損検知プロトコルは S-07 の責務。ここは配線のみ検証）
//  (3) GameFlow.GoToResult が RunResult をキャリーし Result シーンへ遷移する
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.PlayMode
{
    public class GameFlowPlayModeTests
    {
        // recovered=true を常に返すテスト用ダブル。破損検知の中身（.bak退避・[SaveCorruption]ログ）は
        // S-07（FileSaveStore）の責務のため、ここでは GameBootstrap→GameFlow の伝播配線のみを検証する。
        private sealed class RecoveredSaveStoreStub : ISaveStore
        {
            public LoadResult Load() => new LoadResult(SaveData.CreateDefault(), true);
            public void Save(SaveData data) { }
        }

        [TearDown]
        public void ResetGameFlowState()
        {
            GameFlow.ClearRunResult();
            GameFlow.SetRecovered(false);
        }

        // S-07 以降、GameBootstrap の既定ストアは SaveStores.CreateDefault()（=FileSaveStore）のため、
        // 実 Boot シーンをそのまま起動すると（シーン内 GameObject は Awake/Start がテストの介入前に
        // 走ってしまい SetSaveStoreForTest の注入余地が無い）既定ストアが実行環境の実
        // Application.persistentDataPath/save.json を Load() してしまう。実環境のセーブが破損している
        // 場合は実ユーザー領域へ .bak を書き込み・[SaveCorruption] LogError を出し、
        // 「テストは実ユーザーのセーブ先を汚さない一時パスを使う」規約に違反し環境依存で false-fail しうる
        // （CR-CODE 指摘）。下の GameBootstrap_PropagatesRecoveredFlag... テストと同じパターンで、
        // 実 Boot シーンは使わず注入済み GameBootstrap を単体で駆動する（InMemorySaveStore = ファイル I/O 無し）。
        [UnityTest]
        public IEnumerator Boot_LoadsSave_AndAutoTransitionsToTitle_WithNoErrors()
        {
            var go = new GameObject("TestBootstrapNoErrors");
            go.SetActive(false);
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.SetSaveStoreForTest(new InMemorySaveStore());
            go.SetActive(true);

            yield return null; // Start() 実行を1フレーム待つ
            yield return null; // GameFlow.GoToTitle() が要求した遅延シーンロードの完了を待つ
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GameBootstrap_PropagatesRecoveredFlag_ToGameFlow_AndStillReachesTitle()
        {
            // 実シーンの GameBootstrap（InMemorySaveStore・recovered=false 固定）との競合を避けるため
            // Boot シーンは使わず、注入済み GameBootstrap を単体で駆動する。
            var go = new GameObject("TestBootstrapWithRecoveredStub");
            go.SetActive(false);
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.SetSaveStoreForTest(new RecoveredSaveStoreStub());
            go.SetActive(true);

            yield return null; // Start() 実行を1フレーム待つ

            Assert.IsTrue(GameFlow.Recovered);

            yield return null; // GameFlow.GoToTitle() が要求した遅延シーンロードの完了を待つ
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator GameFlow_GoToResult_CarriesRunResult_AndLoadsResultScene()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;

            var result = new RunResult
            {
                IsWin = true,
                CoreHpRemaining = 42,
                KillCount = 17,
                AoeKillCount = 3,
                UsedBuildSpots = 4,
                ClearTimeSec = 123.4f,
            };
            GameFlow.GoToResult(result);

            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name);
            Assert.IsTrue(GameFlow.TryGetLastRunResult(out RunResult carried));
            Assert.AreEqual(result.IsWin, carried.IsWin);
            Assert.AreEqual(result.CoreHpRemaining, carried.CoreHpRemaining);
            Assert.AreEqual(result.KillCount, carried.KillCount);
            Assert.AreEqual(result.AoeKillCount, carried.AoeKillCount);
            Assert.AreEqual(result.UsedBuildSpots, carried.UsedBuildSpots);
            Assert.AreEqual(result.ClearTimeSec, carried.ClearTimeSec);
        }
    }
}
