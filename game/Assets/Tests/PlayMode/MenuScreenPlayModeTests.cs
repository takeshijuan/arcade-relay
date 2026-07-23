// MenuScreenPlayModeTests.cs — S-03 acceptance: Menu シーンに contract §11 の必須4要素が実在し
// 操作できる: (1) 「プレイ開始」→Game (2) アウトゲーム表示(実績/統計/essence+UPG Lv)
// (3) 設定(音量スライダー+操作説明) (4) 「タイトルへ戻る」→Title。
// InputTestFixture でクリック擬似発行して2つの遷移を検証し、全 Canvas が ScreenSpaceCamera であることを確認する。
//
// QA-PLAY 再現バグ修正メモ（S-03。TitleScreenPlayModeTests と同一原因）: 以前は共有 [UnitySetUp]
// コルーチンでシーンロードを行い、各 [UnityTest] 側で AddDevice→Press するパターンだったが、
// batchmode 実行下では [UnitySetUp]（コルーチン）と [UnityTest]（別コルーチン）の境界を跨ぐと、
// テスト側で追加した Mouse デバイスの状態が InputActionMap 側の WasPressedThisFrame() に反映されない
// ことが実測で確認された（Mouse.current.leftButton.isPressed は true になるのに
// click.WasPressedThisFrame() は常に false のまま）。ResultScreenPlayModeTests（シーンロード/状態設定を
// 各 [UnityTest] 内にインライン化しているパターン）は同一の InputReader/Update ポーリング機構を使いながら
// 安定して pass するため、シーンロードとデバイス追加/入力擬似発行を同一コルーチン内に収める
// （インライン化する）方式へ統一した。
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class MenuScreenPlayModeTests : InputTestFixture
    {
        private static SaveData SampleSaveData()
        {
            SaveData data = SaveData.CreateDefault();
            data.essence = 42;
            data.upgStartingGoldLv = 1;
            data.totalRunsPlayed = 3;
            data.totalWins = 1;
            data.totalKills = 7;
            data.achFirstVictory = true;
            return data;
        }

        // S-20: recovered パラメータ追加（既定 false・既存呼び出し元は無変更）。TitleScreenPlayModeTests の
        // LoadTitle(bool recovered=false) と同型。
        private static IEnumerator LoadMenu(bool recovered = false)
        {
            GameFlow.SetCurrentSaveData(SampleSaveData());
            GameFlow.SetRecovered(recovered);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            GameFlow.SetCurrentSaveData(null);
            GameFlow.SetCurrentAudioSettings(null); // S-16: テスト間の音量キャリー漏れを防ぐ
            GameFlow.SetRecovered(false); // S-20: テスト間の recovered フラグ漏れを防ぐ
            GameFlow.SetSaveFailed(false); // CR-CODE iter1 #1: SaveFailed テスト間の漏れを防ぐ
            yield break;
        }

        /// <summary>CR-CODE iter1 #1 対応テスト用: Save() が常に例外を投げるスタブ（RunOutcomePlayModeTests の
        /// ThrowingSaveStore と同型だが、Ui 層のテストは Components 層のテストファイルへ依存しないよう
        /// 本ファイル内に別途定義する）。</summary>
        private sealed class ThrowingSaveStore : ISaveStore
        {
            public LoadResult Load() => new LoadResult(SaveData.CreateDefault(), false);
            public void Save(SaveData data) => throw new System.IO.IOException("simulated persistence failure");
        }

        private static Vector2 ScreenPointOf(RectTransform rect, Camera cam) =>
            RectTransformUtility.WorldToScreenPoint(cam, rect.position);

        [UnityTest]
        public IEnumerator MenuCanvas_IsScreenSpaceCamera()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen, "MenuScreen コンポーネントが Menu シーンに存在しない");

            var canvas = menuScreen.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Menu の Canvas が見つからない");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.IsNotNull(canvas.worldCamera, "worldCamera が未割当");
        }

        [UnityTest]
        public IEnumerator RequiredFourElements_Exist()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);

            // (1) プレイ開始
            Assert.IsNotNull(menuScreen.PlayButtonRect, "「プレイ開始」ボタンが存在しない");

            // (2) アウトゲーム表示: 実績一覧・統計・essence/UPG Lv
            Assert.IsNotNull(menuScreen.OutgamePanel, "アウトゲーム表示パネルが存在しない");
            Assert.AreEqual(5, menuScreen.AchievementCount, "実績一覧が ACH-01〜05 の5件揃っていない");
            Assert.IsNotNull(menuScreen.OutgamePanel.Find("StatsText"), "統計表示が存在しない");
            Assert.IsNotNull(menuScreen.OutgamePanel.Find("EssenceUpgText"), "essence/UPG Lv 表示が存在しない");

            // (3) 設定: 音量スライダー(BGM/SFX) + 操作説明
            Assert.IsNotNull(menuScreen.SettingsPanel, "設定パネルが存在しない");
            Assert.IsNotNull(menuScreen.BgmVolumeSlider, "BGM音量スライダーが存在しない");
            Assert.IsNotNull(menuScreen.SfxVolumeSlider, "SFX音量スライダーが存在しない");
            Assert.IsNotNull(menuScreen.SettingsPanel.Find("OperationInstructionsText"), "操作説明が存在しない");

            // (4) タイトルへ戻る
            Assert.IsNotNull(menuScreen.BackToTitleButtonRect, "「タイトルへ戻る」ボタンが存在しない");
        }

        [UnityTest]
        public IEnumerator OutgameDisplay_ReflectsLoadedSaveData()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            var essenceText = menuScreen.OutgamePanel.Find("EssenceUpgText").GetComponent<Text>();
            Assert.IsTrue(essenceText.text.Contains("essence: 42"), $"essence 表示が反映されていない: {essenceText.text}");
            Assert.IsTrue(essenceText.text.Contains("UPG-01 Lv1"), $"UPG-01 Lv 表示が反映されていない: {essenceText.text}");

            var statsText = menuScreen.OutgamePanel.Find("StatsText").GetComponent<Text>();
            Assert.IsTrue(statsText.text.Contains("総ラン数: 3"), $"統計表示が反映されていない: {statsText.text}");

            var achievement = menuScreen.OutgamePanel.Find("Achievement_ACH-01").GetComponent<Text>();
            Assert.IsTrue(achievement.text.Contains("[獲得済]"), $"獲得済み実績のマーカーが反映されていない: {achievement.text}");

            // CR-CODE iter1 #2: IMG-04(IconEssence)は design/assets.md status=generated かつ
            // Assets/Resources/Generated/textures/icon-essence.png に取込済みのため、実機同様に
            // 実 Resources.Load でロード成功しアイコンが実在することを検証する（欠落=退行を検知）。
            var essenceIcon = menuScreen.OutgamePanel.Find("EssenceIcon");
            Assert.IsNotNull(essenceIcon, "IMG-04(essenceアイコン)が生成されていない（Resources.Load 退行の疑い）");
            // CR-CODE iter2 #2: UnityEngine.Object への `?.` はエンジンのオーバーロード済み null 判定を
            // 迂回し、Image 欠落時に fake-null が素通りして MissingComponentException で無説明に落ちる。
            // GetComponent の結果を明示 null チェックしてから .sprite にアクセスする。
            var essenceIconImage = essenceIcon.GetComponent<Image>();
            Assert.IsNotNull(essenceIconImage, "essenceアイコンに Image コンポーネントが無い");
            Assert.IsNotNull(essenceIconImage.sprite, "essenceアイコンの Image.sprite が未設定");
        }

        [UnityTest]
        public IEnumerator ClickPlayButton_TransitionsToGame()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(menuScreen.PlayButtonRect, menuScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ClickBackToTitleButton_TransitionsToTitle()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(menuScreen.BackToTitleButtonRect, menuScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);
        }

        // S-15 acceptance: 実績/統計/essence+UPG購入UI。ACH-03/04 の進捗バー実在と、UPG購入ボタン押下で
        // Lv表示+1・essence表示が減少することをクリック擬似発行で検証する。

        [UnityTest]
        public IEnumerator OutgamePanel_ShowsAchievementProgressBarsForAch03AndAch04()
        {
            yield return LoadMenu(); // SampleSaveData: totalKills=7, achAoeSpecialist=false（ACH-03/04 未獲得）

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            // 進捗バーは背景(Bg)を OutgamePanel 直下の子として持ち、Fill はその子（Find は直下探索のため Bg 側で検証）。
            Assert.IsNotNull(menuScreen.OutgamePanel.Find("Achievement_ACH-03_ProgressBg"),
                "ACH-03 の進捗バーが存在しない");
            Assert.IsNotNull(menuScreen.OutgamePanel.Find("Achievement_ACH-04_ProgressBg"),
                "ACH-04 の進捗バーが存在しない");

            var ach03Text = menuScreen.OutgamePanel.Find("Achievement_ACH-03").GetComponent<Text>();
            Assert.IsTrue(ach03Text.text.Contains("7/"), $"ACH-03 現在値が反映されていない: {ach03Text.text}");
        }

        [UnityTest]
        public IEnumerator ClickUpg01BuyButton_IncreasesLevelAndDecreasesEssence()
        {
            var data = SaveData.CreateDefault();
            data.essence = 100;
            data.upgStartingGoldLv = 0;
            GameFlow.SetCurrentSaveData(data);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            // ファイル I/O を避けるためインメモリストアを注入する（Awake 後でも安全 — MenuScreen.cs 参照）。
            // CR-CODE iter1 #5: 変数に保持して購入後に Load() を呼び、PersistPurchase の Save 呼び出しが
            // 実際に発生していることを検証する（削除しても本テストが緑になる退行を防ぐ）。
            var saveStore = new InMemorySaveStore();
            menuScreen.SetSaveStoreForTest(saveStore);

            var levelText = menuScreen.OutgamePanel.Find("UPG-01LevelText").GetComponent<Text>();
            Assert.IsTrue(levelText.text.Contains("Lv0/3"), $"購入前のLv表示が想定と異なる: {levelText.text}");

            var buyButtonRect = menuScreen.OutgamePanel.Find("UPG-01BuyButton").GetComponent<RectTransform>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(buyButtonRect, menuScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(levelText.text.Contains("Lv1/3"), $"購入後にLv表示が+1されていない: {levelText.text}");

            var essenceText = menuScreen.OutgamePanel.Find("EssenceUpgText").GetComponent<Text>();
            Assert.IsTrue(essenceText.text.Contains("essence: 70"),
                $"購入後にessence表示が減少していない(100-30=70想定): {essenceText.text}");

            SaveData persisted = saveStore.Load().Data;
            Assert.AreEqual(70, persisted.essence, "購入結果が ISaveStore へ永続化されていない(essence)");
            Assert.AreEqual(1, persisted.upgStartingGoldLv, "購入結果が ISaveStore へ永続化されていない(UPG-01 Lv)");
        }

        [UnityTest]
        public IEnumerator ClickUpg01BuyButton_WithInsufficientEssence_DoesNotChangeLevel()
        {
            var data = SaveData.CreateDefault();
            data.essence = 5; // UPG_PURCHASE_COST_PER_LV(30) 未満
            data.upgStartingGoldLv = 0;
            GameFlow.SetCurrentSaveData(data);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            // CR-CODE iter1 #5: essence不足時に表示・永続化のいずれも変化しないことを検証する。
            var saveStore = new InMemorySaveStore(data.Clone());
            menuScreen.SetSaveStoreForTest(saveStore);

            var levelText = menuScreen.OutgamePanel.Find("UPG-01LevelText").GetComponent<Text>();
            var essenceText = menuScreen.OutgamePanel.Find("EssenceUpgText").GetComponent<Text>();
            var buyButtonRect = menuScreen.OutgamePanel.Find("UPG-01BuyButton").GetComponent<RectTransform>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(buyButtonRect, menuScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(levelText.text.Contains("Lv0/3"),
                $"essence不足時に購入が成立してしまっている: {levelText.text}");
            Assert.IsTrue(essenceText.text.Contains("essence: 5"),
                $"essence不足時にessence表示が変化してしまっている: {essenceText.text}");

            SaveData persisted = saveStore.Load().Data;
            Assert.AreEqual(5, persisted.essence, "essence不足時に保存が発生してしまっている(essence)");
            Assert.AreEqual(0, persisted.upgStartingGoldLv, "essence不足時に保存が発生してしまっている(UPG-01 Lv)");
        }

        // S-16 acceptance: 設定パネルの BGM/SFX 音量スライダーが実際の AudioSource 音量へ反映され、
        // 値は Persistence 経由で保存・復元される。スライダー値変更→対応する音量が変化し、再ロードで
        // 復元されることを検証する。

        [UnityTest]
        public IEnumerator ChangeBgmVolumeSlider_UpdatesAudioSourceVolume_AndPersistsAcrossReload()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen.BgmAudioSource, "BGM用AudioSourceが生成されていない");
            var store = new InMemoryAudioSettingsStore();
            menuScreen.SetAudioSettingsStoreForTest(store);

            menuScreen.BgmVolumeSlider.value = 0.35f;
            yield return null;

            Assert.AreEqual(0.35f, menuScreen.BgmAudioSource.volume, 0.001f,
                "BGM音量スライダー変更が実際のAudioSource.volumeへ反映されていない");

            AudioSettingsData persisted = store.Load();
            Assert.AreEqual(0.35f, persisted.BgmVolume, 0.001f, "BGM音量がPersistence経由で保存されていない");

            // 再ロード検証: 直近保存値を GameFlow キャリーへ反映した上で Menu を再ロードし、
            // 新しい MenuScreen インスタンスがスライダー初期値/AudioSource.volume を復元することを確認する。
            GameFlow.SetCurrentAudioSettings(store.Load());
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var reloaded = Object.FindFirstObjectByType<MenuScreen>();
            Assert.AreEqual(0.35f, reloaded.BgmVolumeSlider.value, 0.001f, "再ロード後にBGM音量スライダーが復元されていない");
            Assert.AreEqual(0.35f, reloaded.BgmAudioSource.volume, 0.001f, "再ロード後にBGM用AudioSource.volumeが復元されていない");
        }

        [UnityTest]
        public IEnumerator ChangeSfxVolumeSlider_UpdatesAudioSourceVolume_AndPersistsAcrossReload()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen.SfxAudioSource, "SFX用AudioSourceが生成されていない");
            var store = new InMemoryAudioSettingsStore();
            menuScreen.SetAudioSettingsStoreForTest(store);

            menuScreen.SfxVolumeSlider.value = 0.6f;
            yield return null;

            Assert.AreEqual(0.6f, menuScreen.SfxAudioSource.volume, 0.001f,
                "SFX音量スライダー変更が実際のAudioSource.volumeへ反映されていない");

            AudioSettingsData persisted = store.Load();
            Assert.AreEqual(0.6f, persisted.SfxVolume, 0.001f, "SFX音量がPersistence経由で保存されていない");

            GameFlow.SetCurrentAudioSettings(store.Load());
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var reloaded = Object.FindFirstObjectByType<MenuScreen>();
            Assert.AreEqual(0.6f, reloaded.SfxVolumeSlider.value, 0.001f, "再ロード後にSFX音量スライダーが復元されていない");
            Assert.AreEqual(0.6f, reloaded.SfxAudioSource.volume, 0.001f, "再ロード後にSFX用AudioSource.volumeが復元されていない");
        }

        // CR-CODE iter2 #1（major）再帰防止テスト: 上記2テストはスライダー値をプログラム的に直接設定
        // （menuScreen.BgmVolumeSlider.value = x）しているだけで、実プレイヤーのマウス操作でスライダーを
        // 操作できることを検証していなかった（本プロジェクトは EventSystem 未導入で uGUI Slider が素の
        // 状態ではポインタイベントを一切受け取れず、デッドUIになっていた）。実マウス入力擬似発行
        // （Move+Press+Release）でスライダー右端付近をクリックし、InputReader.ClickHeld +
        // RectTransformUtility 経路で実際に値が変化することを検証する。
        [UnityTest]
        public IEnumerator ClickNearRightEdgeOfBgmVolumeSlider_ViaPointerInput_RaisesVolumeAndPersists()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            var store = new InMemoryAudioSettingsStore();
            menuScreen.SetAudioSettingsStoreForTest(store);

            var sliderRect = (RectTransform)menuScreen.BgmVolumeSlider.transform;
            Vector3 nearRightEdgeLocal = new Vector3(sliderRect.rect.xMax * 0.9f, 0f, 0f);
            Vector3 nearRightEdgeWorld = sliderRect.TransformPoint(nearRightEdgeLocal);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(menuScreen.UiCamera, nearRightEdgeWorld);

            var mouse = InputSystem.AddDevice<Mouse>();
            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.Greater(menuScreen.BgmVolumeSlider.value, 0.8f,
                "スライダー右端付近を実マウス入力でクリックしても音量が変化しない（デッドUIの再帰防止）");
            Assert.Greater(menuScreen.BgmAudioSource.volume, 0.8f,
                "実マウス入力でのスライダー操作がAudioSource.volumeへ反映されていない");

            AudioSettingsData persisted = store.Load();
            Assert.Greater(persisted.BgmVolume, 0.8f, "実マウス入力でのスライダー操作がPersistence経由で保存されていない");
        }

        // S-20 acceptance: recovered=true のとき Menu にセーブ復旧トーストを表示し、
        // ベストクリアタイム未記録（-1）は「--」表示する。

        [UnityTest]
        public IEnumerator RecoveredFlag_ShowsRecoveryToast()
        {
            yield return LoadMenu(recovered: true);

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            Assert.IsTrue(menuScreen.RecoveryNoticeVisible, "recovered=true なのに Menu の復旧トーストが表示されていない");
        }

        [UnityTest]
        public IEnumerator NotRecovered_HidesRecoveryToast()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            // TitleScreenPlayModeTests.NotRecovered_HidesRecoveryNotice と同じ理由（CR-CODE iter1 #7）:
            // 「UI 未構築で失敗」と「意図的に非表示」を区別するため実在も別途アサートする。
            Assert.IsTrue(menuScreen.RecoveryNoticeTextExists, "RecoveryNoticeText が構築されていない（UI 未構築の疑い）");
            Assert.IsFalse(menuScreen.RecoveryNoticeVisible, "recovered=false なのに Menu の復旧トーストが表示されている");
        }

        // CR-CODE iter1 #1(major)対応: GameFlow.SaveFailed（直近 Persistence.Save 失敗）が Menu 画面上で
        // 通知されることを検証する（従来は購読者がゼロでプレイヤーから不可視だった）。

        [UnityTest]
        public IEnumerator SaveFailedFlag_ShowsSaveFailedNotice_OnLoad()
        {
            GameFlow.SetSaveFailed(true);
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            Assert.IsTrue(menuScreen.SaveFailedNoticeVisible,
                "GameFlow.SaveFailed=true なのに Menu の保存失敗通知が表示されていない");
        }

        [UnityTest]
        public IEnumerator NotSaveFailed_HidesSaveFailedNotice_OnLoad()
        {
            yield return LoadMenu();

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            Assert.IsTrue(menuScreen.SaveFailedNoticeTextExists, "SaveFailedNoticeText が構築されていない（UI 未構築の疑い）");
            Assert.IsFalse(menuScreen.SaveFailedNoticeVisible,
                "GameFlow.SaveFailed=false なのに Menu の保存失敗通知が表示されている");
        }

        [UnityTest]
        public IEnumerator ClickUpg01BuyButton_WhenSaveThrows_ShowsSaveFailedNotice()
        {
            var data = SaveData.CreateDefault();
            data.essence = 100;
            data.upgStartingGoldLv = 0;
            GameFlow.SetCurrentSaveData(data);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen);
            Assert.IsFalse(menuScreen.SaveFailedNoticeVisible, "購入前から保存失敗通知が表示されている");
            menuScreen.SetSaveStoreForTest(new ThrowingSaveStore());

            // CR-CODE iter2 #3（minor）対応: Find/GetComponent の各段を null 確認してから deref する
            // （既存の RunOutcomePlayModeTests / 本ファイル他テストと同じハードニングパターン）。
            var buyButtonTransform = menuScreen.OutgamePanel.Find("UPG-01BuyButton");
            Assert.IsNotNull(buyButtonTransform, "UPG-01BuyButton が構築されていない（UI 構築退行の疑い）");
            var buyButtonRect = buyButtonTransform.GetComponent<RectTransform>();
            Assert.IsNotNull(buyButtonRect, "UPG-01BuyButton に RectTransform が無い");

            // CR-CODE iter2 #2（major）対応: PersistPurchase() の catch 節が必ず
            // Debug.LogError("[SaveFailure] MenuScreen failed to persist SaveData ...") を発火させるため、
            // Unity Test Framework の既定（予期しない Error ログ＝テスト失敗）でこの acceptance 検証テスト
            // 自体が失敗するのを防ぐ（RunOutcomePlayModeTests.ThrowingSaveStore 系テストと同じパターン）。
            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveFailure\] MenuScreen failed to persist SaveData"));

            var mouse = InputSystem.AddDevice<Mouse>();
            Vector2 point = ScreenPointOf(buyButtonRect, menuScreen.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(menuScreen.SaveFailedNoticeVisible,
                "Save() が例外を投げても Menu の保存失敗通知が表示されない（黙殺されている）");
            Assert.IsTrue(GameFlow.SaveFailed, "Save() 例外後に GameFlow.SaveFailed が true になっていない");
        }

        [UnityTest]
        public IEnumerator UnrecordedBestClearTime_ShowsDoubleDash()
        {
            yield return LoadMenu(); // SampleSaveData は SaveData.CreateDefault() 由来で bestClearTimeSec=-1（未記録）

            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menuScreen, "MenuScreen コンポーネントが Menu シーンに存在しない");
            var highScoreTextTransform = menuScreen.OutgamePanel.Find("HighScoreText");
            Assert.IsNotNull(highScoreTextTransform, "HighScoreText が存在しない（UI 構築退行の疑い）");
            var highScoreText = highScoreTextTransform.GetComponent<Text>();
            Assert.IsNotNull(highScoreText, "HighScoreText に Text コンポーネントが無い");
            Assert.IsTrue(highScoreText.text.Contains("--"),
                $"未記録のベストクリアタイムが「--」表示になっていない: {highScoreText.text}");
        }

        // CR-CODE iter2 #5（minor）対応: BuildUi() 内で GameFlow.Recovered を消費する挙動（CR-CODE iter1 #2）
        // に回帰テストが無かった指摘への対応。「recovered=true で Menu を2回ロードすると2回目はトーストが
        // 出ない」「Title→Menu の順では双方で最低1回ずつ表示される」の2挙動を固定する。

        [UnityTest]
        public IEnumerator RecoveredFlag_ConsumedAfterFirstMenuLoad_NotShownOnSecondLoad()
        {
            // LoadMenu() ヘルパは呼ぶたびに明示的に GameFlow.SetRecovered(recovered) を再設定してしまうため、
            // 「Menu の BuildUi() 自身が消費する」挙動の検証にはヘルパを使わず、1回目のロード後は
            // SetRecovered を挟まずに素の SceneManager.LoadSceneAsync で再ロードする。
            GameFlow.SetRecovered(true);
            GameFlow.SetCurrentSaveData(SampleSaveData());
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var first = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(first, "MenuScreen コンポーネントが Menu シーンに存在しない（1回目）");
            Assert.IsTrue(first.RecoveryNoticeVisible, "初回 Menu ロードで recovered=true のトーストが表示されていない");
            Assert.IsFalse(GameFlow.Recovered, "Menu の BuildUi() が GameFlow.Recovered を消費していない（1回目表示後）");

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var second = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(second, "MenuScreen コンポーネントが Menu シーンに存在しない（2回目）");
            Assert.IsTrue(second.RecoveryNoticeTextExists, "RecoveryNoticeText が構築されていない（UI 未構築の疑い・2回目）");
            Assert.IsFalse(second.RecoveryNoticeVisible,
                "recovered 消費後の2回目 Menu ロードでトーストが再表示されている（消費されていない＝退行）");
        }

        [UnityTest]
        public IEnumerator RecoveredFlag_ShownOnceEachInTitleThenMenu_TitleToMenuOrder()
        {
            // 正準フロー Boot→Title→Menu では Title・Menu の両方で最低1回はトーストが表示される契約
            // （本ファイル冒頭コメント / TitleScreen.cs 冒頭コメント）。Title は消費せず Menu 側でのみ
            // 消費する設計（CR-CODE iter1 #2）を、実際の Title→Menu 遷移順で固定する。
            GameFlow.SetRecovered(true);
            GameFlow.SetCurrentSaveData(SampleSaveData());
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(title, "TitleScreen コンポーネントが Title シーンに存在しない");
            Assert.IsTrue(title.RecoveryNoticeVisible, "Title 初回表示で recovered=true のトーストが表示されていない");
            Assert.IsTrue(GameFlow.Recovered, "TitleScreen が Recovered を消費してしまっている（Title は消費しない設計のはず）");

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(menu, "MenuScreen コンポーネントが Menu シーンに存在しない");
            Assert.IsTrue(menu.RecoveryNoticeVisible, "Title→Menu の順で Menu 側トーストが表示されていない");
            Assert.IsFalse(GameFlow.Recovered, "Menu 側で GameFlow.Recovered が消費されていない");
        }
    }
}
