// RunOutcomePlayModeTests.cs — S-06 acceptance:
// 「全 WAVE_COUNT 消化かつ coreHp>0 で勝利、coreHp<=0 で敗北が成立し、いずれも RunResult
//  （IsWin/CoreHpRemaining/KillCount/AoeKillCount/UsedBuildSpots/ClearTimeSec）を確定して
//  ScoreSystem.ComputeFinalScore→MetaProgression.ApplyRunResult→Persistence.Save を1回だけ実行し
//  （リスタート連打で二重保存しない）Result シーンへ遷移する。PlayMode テストで勝利/敗北の両経路が
//  RunResult を確定し Result へ遷移することを検証できる」を検証する。
// CoreDefensePlayModeTests.cs と同じ規約9パターン（非アクティブ生成→注入→アクティブ化・
// Update() 自動駆動を止めて StepForTest だけで決定論的に進める）で RunOutcomeController を単体駆動する。
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.PlayMode
{
    public class RunOutcomePlayModeTests
    {
        // Persistence.Save の呼び出し回数を数えるだけのテストダブル（実 I/O は S-07 の責務）。
        private sealed class SaveCountingStore : ISaveStore
        {
            public int SaveCallCount { get; private set; }
            public SaveData LastSaved { get; private set; }

            public LoadResult Load() => new LoadResult(SaveData.CreateDefault(), false);

            public void Save(SaveData data)
            {
                SaveCallCount++;
                LastSaved = data;
            }
        }

        // Persistence.Save が必ず例外を投げるテストダブル（CR-CODE iter1 #2: Save 失敗経路の未検証を埋める）。
        private sealed class ThrowingSaveStore : ISaveStore
        {
            public LoadResult Load() => new LoadResult(SaveData.CreateDefault(), false);

            public void Save(SaveData data) => throw new System.IO.IOException("simulated persistence failure");
        }

        private GameObject coreGo;
        private GameObject waveGo;
        private GameObject buildGo;
        private GameObject outcomeGo;

        private CoreView coreView;
        private WaveSpawnController waveController;
        private BuildSpotController buildController;
        private RunOutcomeController outcome;
        private SaveCountingStore saveStore;

        [SetUp]
        public void SetUp()
        {
            coreGo = new GameObject("TestCore");
            coreGo.SetActive(false);
            coreView = coreGo.AddComponent<CoreView>();
            coreGo.SetActive(true); // Awake() でプレースホルダ生成 + CurrentHp=HpMax

            waveGo = new GameObject("TestWaveSpawnController");
            waveGo.SetActive(false);
            waveController = waveGo.AddComponent<WaveSpawnController>();
            waveController.SetCoreViewForTest(coreView);
            waveGo.SetActive(true);
            waveController.enabled = false; // StepForTest のみで進める

            buildGo = new GameObject("TestBuildSpotController");
            buildGo.SetActive(false);
            buildController = buildGo.AddComponent<BuildSpotController>();
            buildController.SetWaveSpawnControllerForTest(waveController);
            buildGo.SetActive(true);
            buildController.enabled = false;

            saveStore = new SaveCountingStore();
            outcomeGo = new GameObject("TestRunOutcomeController");
            outcomeGo.SetActive(false);
            outcome = outcomeGo.AddComponent<RunOutcomeController>();
            outcome.SetWaveSpawnControllerForTest(waveController);
            outcome.SetCoreViewForTest(coreView);
            outcome.SetBuildSpotControllerForTest(buildController);
            outcome.SetSaveStoreForTest(saveStore);
            outcomeGo.SetActive(true);
            outcome.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (outcomeGo != null) Object.Destroy(outcomeGo);
            if (buildGo != null) Object.Destroy(buildGo);
            if (waveGo != null) Object.Destroy(waveGo);
            if (coreGo != null) Object.Destroy(coreGo);
            GameFlow.ClearRunResult();
            GameFlow.SetCurrentSaveData(null);
            // CR-CODE iter1 #2: GameFlow.SaveFailed は static のためテスト間で生存する。
            // ThrowingSaveStore を使うテストが true を残したまま次テストへ漏れないようにリセットする。
            GameFlow.SetSaveFailed(false);
        }

        /// <summary>演出待機(WinResultDelaySec)を消化して Result 遷移させるまでステップする共通処理。</summary>
        private IEnumerator DrainPostFinalizeDelay()
        {
            const float delayStep = 0.5f;
            float waited = 0f;
            float ceiling = GameConfig.Presentation.WinResultDelaySec + 5f;
            while (!outcome.HasTransitioned && waited < ceiling)
            {
                outcome.StepForTest(delayStep);
                waited += delayStep;
                yield return null;
            }
            Assert.Less(waited, ceiling, "演出待機後も Result へ遷移しなかった");
        }

        [UnityTest]
        public IEnumerator AllWavesCleared_WithCoreAlive_ConfirmsWin_SavesOnce_AndTransitionsToResult()
        {
            Assert.IsTrue(buildController.TryPlaceTower(0, TowerType.BastionCannon).Success);
            Assert.IsTrue(buildController.TryPlaceTower(1, TowerType.ArcEmitter).Success);

            const float step = 0.5f;
            const float maxSimulatedSeconds = 400f;
            float simulated = 0f;

            // 迎撃シミュレーション: 各ティック後に生存中の敵を即座に撃破し、ゴール到達を防ぎながら
            // 全ウェーブを消化する（S-05 の戦闘解決自体は別ストーリーで検証済みのため、ここでは
            // 「全ウェーブ消化かつコア生存」→勝利確定の配線のみを対象にする）。
            while (!(waveController.WaveSystem.AllWavesSpawned && waveController.WaveSystem.ActiveEnemyCount == 0)
                   && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(step);

                foreach (EnemyInstance enemy in new List<EnemyInstance>(waveController.WaveSystem.Enemies))
                {
                    if (!enemy.Active) continue;
                    EnemyDamageResult dr = waveController.WaveSystem.ApplyDamage(enemy.Id, 9999);
                    if (dr.Defeated)
                    {
                        TowerType attributedTo = enemy.Type == EnemyType.Marauder
                            ? TowerType.BastionCannon
                            : TowerType.ArcEmitter;
                        buildController.EnemyHealth.RecordKill(attributedTo);
                    }
                }

                outcome.StepForTest(step);
                simulated += step;
                Assert.IsFalse(coreView.IsDefeated, "想定外の敗北: 敵をゴール到達前に撃破できていない（テスト前提が壊れている）");
                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に全ウェーブを消化できなかった");
            Assert.IsTrue(outcome.IsFinalized, "全ウェーブ消化かつコア生存後も RunResult が確定していない");
            Assert.IsFalse(outcome.HasTransitioned, "演出待機前に Result へ遷移してしまっている");

            yield return DrainPostFinalizeDelay();
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name);
            Assert.IsTrue(GameFlow.TryGetLastRunResult(out RunResult carried));
            Assert.IsTrue(carried.IsWin);
            Assert.AreEqual(GameConfig.Core.HpMax, carried.CoreHpRemaining, "コアが無傷のはずが残HPが一致しない");
            Assert.Greater(carried.KillCount, 0, "撃破数が記録されていない");
            Assert.AreEqual(2, carried.UsedBuildSpots);

            Assert.AreEqual(1, saveStore.SaveCallCount, "Persistence.Save が1回だけ実行されていない");
            Assert.IsNotNull(saveStore.LastSaved);
            Assert.AreEqual(1, saveStore.LastSaved.totalRunsPlayed);
            Assert.AreEqual(1, saveStore.LastSaved.totalWins);
        }

        [UnityTest]
        public IEnumerator CoreDepleted_ConfirmsLoss_SavesOnce_AndTransitionsToResult()
        {
            const float step = 0.5f;
            const float maxSimulatedSeconds = 400f;
            float simulated = 0f;

            while (!outcome.IsFinalized && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(step);
                outcome.StepForTest(step);
                simulated += step;
                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に敗北判定が確定しなかった（迎撃なしのはずがコアHPが減っていない可能性）");
            Assert.IsTrue(coreView.IsDefeated);
            Assert.IsTrue(outcome.IsFinalized);
            Assert.IsFalse(outcome.HasTransitioned, "演出待機前に Result へ遷移してしまっている");

            yield return DrainPostFinalizeDelay();
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name);
            Assert.IsTrue(GameFlow.TryGetLastRunResult(out RunResult carried));
            Assert.IsFalse(carried.IsWin);
            Assert.AreEqual(0, carried.CoreHpRemaining);

            Assert.AreEqual(1, saveStore.SaveCallCount, "Persistence.Save が1回だけ実行されていない（連打/複数フレームでの二重保存の疑い）");
            Assert.IsNotNull(saveStore.LastSaved);
            Assert.AreEqual(1, saveStore.LastSaved.totalRunsPlayed);
            Assert.AreEqual(0, saveStore.LastSaved.totalWins);
        }

        // CR-CODE iter1 #2: Save 失敗経路（[SaveFailure] LogError 1回→GameFlow.SaveFailed=true→
        // Result 遷移は継続）が SaveCountingStore（常に成功）のみのテストでは未検証だったため追加。
        // SetSaveStoreForTest は Start() 前しか呼べない契約のため、SetUp が Start 済みの outcome を
        // 一旦破棄し、ThrowingSaveStore を注入した新しい RunOutcomeController に差し替える
        // （outcome/outcomeGo フィールドを上書きすることで TearDown の破棄対象にも含める）。
        [UnityTest]
        public IEnumerator SaveThrows_LogsSaveFailureOnce_SetsGameFlowSaveFailed_ButStillTransitionsToResult()
        {
            Object.Destroy(outcomeGo);
            yield return null;

            var throwingStore = new ThrowingSaveStore();
            outcomeGo = new GameObject("TestRunOutcomeController_Throwing");
            outcomeGo.SetActive(false);
            outcome = outcomeGo.AddComponent<RunOutcomeController>();
            outcome.SetWaveSpawnControllerForTest(waveController);
            outcome.SetCoreViewForTest(coreView);
            outcome.SetBuildSpotControllerForTest(buildController);
            outcome.SetSaveStoreForTest(throwingStore);
            outcomeGo.SetActive(true);
            outcome.enabled = false;

            const float step = 0.5f;
            const float maxSimulatedSeconds = 400f;
            float simulated = 0f;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveFailure\] RunOutcomeController failed to persist SaveData"));

            while (!outcome.IsFinalized && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(step);
                outcome.StepForTest(step);
                simulated += step;
                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に敗北判定が確定しなかった");
            Assert.IsTrue(outcome.IsFinalized);
            Assert.IsTrue(GameFlow.SaveFailed, "Save が例外を投げたのに GameFlow.SaveFailed が true にならない");

            yield return DrainPostFinalizeDelay();
            yield return null;
            yield return null;

            Assert.IsTrue(outcome.HasTransitioned, "Save 失敗時も Result への遷移は継続するはず（失敗しても遷移を止めない設計）");
            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name);
        }
    }
}
