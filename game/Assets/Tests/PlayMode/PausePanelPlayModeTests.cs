// PausePanelPlayModeTests.cs — S-17 acceptance: Game シーン中の Esc で一時停止オーバーレイを開閉し、
// 開いている間 Time.timeScale=0 でタワー攻撃・敵移動・タイマーが全停止する（Title/Menu/Result 中の
// Esc は無反応）。オーバーレイから再開・設定・タイトルへ戻るを選べる。
//
// シーンロードとデバイス追加/入力擬似発行を同一コルーチン内に収める（GameHudPlayModeTests/
// TitleScreenPlayModeTests と同じ batchmode 実測の既知の落とし穴への対応 — docs/conventions.md）。
//
// 敵移動停止の検証: WAVE_PREP_SEC(15s) の実待機を避けるため、WaveSpawnController.StepForTest
// （規約9のテスト用シーム）を小刻みに呼び、準備フェーズ終了直後の1体だけをゴール到達前の中間地点に
// 決定論的に出現させる（同一 Tick 呼び出し内で AdvanceSpawning と AdvanceMovement が同じ deltaTime を
// 共有するため、1回で大きな deltaTime を渡すと出現直後にゴール到達してしまう — 複数回に分割する理由）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class PausePanelPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            // Time.timeScale はシーン/テストを跨いで持続するグローバル値のため、テスト内で 0 にしたまま
            // 後続テストへ漏らさない（S-17 の核心的な副作用のため明示的にリセットする）。
            Time.timeScale = GameConfig.Presentation.NormalTimeScale;
            // S-17 CR-CODE iter1 #3 fix: ClickNearRightEdgeOfBgmVolumeSlider が PersistAudioSettings 経由で
            // プロセスグローバル static GameFlow.CurrentAudioSettings を書き換えたまま残すと、後続フィクスチャ
            // （CurrentAudioSettings ?? CreateDefault() で初期値を解決する画面のテスト）へ順序依存でキャリー
            // する。S-16 の MenuScreenPlayModeTests.TearDown と同じ明示リセットを行う。
            GameFlow.SetCurrentAudioSettings(null);
            // EscInResultScene_DoesNothing が GameFlow.GoToResult で書き込む RunResult キャリーを後続
            // テストへ漏らさない（ResultScreenPlayModeTests.TearDown と同じ明示リセット）。
            GameFlow.ClearRunResult();
            yield break;
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>準備フェーズ(15s)を安全に消化しつつ、出現直後の1体をゴール手前の中間地点で止める
        /// （5秒刻み×3回 = 合計15秒ちょうどで準備フェーズが終わり、最終呼び出しのdeltaTimeだけが移動距離
        /// に効くため 3.5m/s×5s=17.5m<40m でゴール到達しない）。</summary>
        private static int SpawnOneEnemyMidPath(WaveSpawnController waveController)
        {
            waveController.StepForTest(5f);
            waveController.StepForTest(5f);
            waveController.StepForTest(5f);
            Assert.AreEqual(1, waveController.WaveSystem.Enemies.Count, "テスト前提: 中間地点に敵が1体出現していること");
            return waveController.WaveSystem.Enemies[0].Id;
        }

        [UnityTest]
        public IEnumerator Esc_OpensOverlay_SetsTimeScaleZero_AndStopsEnemyMovement()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            Assert.IsNotNull(pausePanel, "PausePanel コンポーネントが Game シーンに存在しない");
            var waveController = Object.FindFirstObjectByType<WaveSpawnController>();

            int enemyId = SpawnOneEnemyMidPath(waveController);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            Assert.IsTrue(pausePanel.IsOpen, "Esc で一時停止オーバーレイが開いていない");
            Assert.AreEqual(GameConfig.Presentation.PausedTimeScale, Time.timeScale, "一時停止中に Time.timeScale が 0 になっていない");

            // batch-verify(Build) 2026-07-22 対応: 基準位置は「一時停止が確定した直後」に採る。
            // Press〜Release の2フレームは Esc 入力を処理して timeScale を 0 へ切替える途中であり、
            // その間は timeScale がまだ 1 のフレームが存在し得るため、Press 前に採った基準位置と
            // 比較すると（timeScale 切替が反映されるまでの1フレーム分の通常速度移動により）0.01m 規模の
            // 誤差で false-fail していた。「一時停止中は動かない」という acceptance の意図どおり、
            // 一時停止確定後を基準にする。
            Assert.IsTrue(waveController.TryGetEnemyPosition(enemyId, out Vector3 posBeforePause));

            // 一時停止中は実フレームが経過しても Time.deltaTime==0 のため WaveSpawnController.Update() は
            // 進行しない（docs/architecture.md §5・conventions.md 6節: delta-time 駆動の自動停止）。
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsTrue(waveController.TryGetEnemyPosition(enemyId, out Vector3 posDuringPause));
            Assert.AreEqual(posBeforePause, posDuringPause, "一時停止中に敵の位置が変化してしまっている");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EscTwice_ResumesOverlay_RestoresTimeScale_AndEnemyMovementContinues()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            var waveController = Object.FindFirstObjectByType<WaveSpawnController>();

            int enemyId = SpawnOneEnemyMidPath(waveController);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(pausePanel.IsOpen, "前提: 一時停止オーバーレイが開いていること");

            Assert.IsTrue(waveController.TryGetEnemyPosition(enemyId, out Vector3 posDuringPause));

            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            Assert.IsFalse(pausePanel.IsOpen, "再度の Esc で一時停止オーバーレイが閉じていない");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "再開後に Time.timeScale が 1 に戻っていない");

            for (int i = 0; i < 10; i++) yield return null;

            Assert.IsTrue(waveController.TryGetEnemyPosition(enemyId, out Vector3 posAfterResume));
            Assert.AreNotEqual(posDuringPause, posAfterResume, "再開後に敵の移動が再開していない");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClickResumeButton_ClosesOverlay_AndRestoresTimeScale()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            var hudPanel = Object.FindFirstObjectByType<HudPanel>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(pausePanel.IsOpen, "前提: 一時停止オーバーレイが開いていること");

            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, pausePanel.ResumeButtonRect.position);
            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.IsFalse(pausePanel.IsOpen, "「再開」ボタンでオーバーレイが閉じていない");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "「再開」ボタンで Time.timeScale が 1 に戻っていない");

            // S-17 CR-CODE iter2 #6(minor) fix: iter1 #1(major) で塞いだ「同一フレーム内で PausePanel.Resume()
            // 実行後、同フレームの HudPanel.Update() が同じクリックをビルドスポット選択として再処理してしまう」
            // race のリグレッション検知。ResumeButtonRect は画面中央付近に置かれビルドスポットと重なり得るため、
            // 再開直後のフレームで TowerSelectPanel が誤って開いていないことを確認する（Update 実行順が
            // PausePanel→HudPanel の場合の片側を検知。逆順のケースは IsOpen 単体ガードで別途カバー済み）。
            Assert.IsFalse(hudPanel.TowerSelect.IsOpen,
                "「再開」クリックの同一フレームでビルドスポット選択パネルが誤って開いてしまっている（LastResumeFrame ガードのリグレッション）");
        }

        [UnityTest]
        public IEnumerator ClickSettingsButton_ShowsSettingsView_BackButtonReturnsToMain()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 settingsPoint = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, pausePanel.SettingsButtonRect.position);
            Move(mouse.position, settingsPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.IsTrue(pausePanel.IsSettingsViewOpen, "「設定」ボタンで設定サブビューが開いていない");
            Assert.IsTrue(pausePanel.IsOpen, "設定サブビュー中も一時停止状態(IsOpen)は維持されるべき");
            Assert.AreEqual(GameConfig.Presentation.PausedTimeScale, Time.timeScale, "設定サブビュー中も timeScale=0 のままであるべき");
            Assert.IsNotNull(pausePanel.BgmVolumeSlider, "設定サブビューにBGM音量スライダーが存在しない");
            Assert.IsNotNull(pausePanel.SfxVolumeSlider, "設定サブビューにSFX音量スライダーが存在しない");

            Vector2 backPoint = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, pausePanel.BackButtonRect.position);
            Move(mouse.position, backPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.IsFalse(pausePanel.IsSettingsViewOpen, "「戻る」ボタンで設定サブビューから戻っていない");
            Assert.IsTrue(pausePanel.IsOpen, "「戻る」後も一時停止オーバーレイ自体は開いたままであるべき");
        }

        [UnityTest]
        public IEnumerator ClickTitleButton_ExitsPause_RestoresTimeScale_AndTransitionsToTitle()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, pausePanel.TitleButtonRect.position);
            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name, "「タイトルへ戻る」で Title シーンへ遷移していない");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "Title 遷移後も Time.timeScale が 0 のまま持ち越されている");
        }

        [UnityTest]
        public IEnumerator EscInTitleScene_DoesNothing()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            // Title シーンには PausePanel が存在しないため Esc は構造的に無反応（シーン遷移もタイムスケール変化も無い）。
            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name, "Title シーン中の Esc でシーンが変化してしまっている");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "Title シーン中の Esc で Time.timeScale が変化してしまっている");
        }

        [UnityTest]
        public IEnumerator EscInMenuScene_DoesNothing()
        {
            // S-17 CR-CODE iter1 #5 fix: Menu への PausePanel 誤配置は「構造的保証（配置しない）」のみで
            // リグレッション検知テストが無かった。EscInTitleScene_DoesNothing と同型のテストを追加する。
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name, "Menu シーン中の Esc でシーンが変化してしまっている");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "Menu シーン中の Esc で Time.timeScale が変化してしまっている");
        }

        [UnityTest]
        public IEnumerator EscInResultScene_DoesNothing()
        {
            // S-17 CR-CODE iter1 #5 fix: Result への PausePanel 誤配置についても同様にリグレッション検知テストを追加する。
            // ResultScreenPlayModeTests（S-09）と同じ「GameFlow.GoToResult を直接呼んで Result シーンへ入る」
            // パターンを踏襲する（RunOutcomeController 経由でコアを実際に破壊する経路は gameplay-engineer 領域の
            // 演出待機ステップ機構に不要に依存するため避ける）。
            var result = new RunResult
            {
                IsWin = true,
                CoreHpRemaining = GameConfig.Core.HpMax,
                KillCount = 0,
                AoeKillCount = 0,
                UsedBuildSpots = 0,
                ClearTimeSec = 1f,
            };
            GameFlow.GoToResult(result);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name, "テスト前提: GameFlow.GoToResult で Result シーンへ遷移していること");

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name, "Result シーン中の Esc でシーンが変化してしまっている");
            Assert.AreEqual(GameConfig.Presentation.NormalTimeScale, Time.timeScale, "Result シーン中の Esc で Time.timeScale が変化してしまっている");
        }

        [UnityTest]
        public IEnumerator ClickNearRightEdgeOfBgmVolumeSlider_ViaPointerInput_RaisesVolumeAndPersists()
        {
            yield return LoadGame();

            var pausePanel = Object.FindFirstObjectByType<PausePanel>();
            var store = new InMemoryAudioSettingsStore();
            pausePanel.SetAudioSettingsStoreForTest(store);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 settingsPoint = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, pausePanel.SettingsButtonRect.position);
            Move(mouse.position, settingsPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            var sliderRect = (RectTransform)pausePanel.BgmVolumeSlider.transform;
            Vector3 nearRightEdgeLocal = new Vector3(sliderRect.rect.xMax * 0.9f, 0f, 0f);
            Vector3 nearRightEdgeWorld = sliderRect.TransformPoint(nearRightEdgeLocal);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pausePanel.UiCamera, nearRightEdgeWorld);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.Greater(pausePanel.BgmVolumeSlider.value, 0.8f,
                "スライダー右端付近を実マウス入力でクリックしても音量が変化しない");
            Assert.Greater(pausePanel.BgmAudioSource.volume, 0.8f,
                "実マウス入力でのスライダー操作がAudioSource.volumeへ反映されていない");

            AudioSettingsData persisted = store.Load();
            Assert.Greater(persisted.BgmVolume, 0.8f, "実マウス入力でのスライダー操作がPersistence経由で保存されていない");
        }
    }
}
