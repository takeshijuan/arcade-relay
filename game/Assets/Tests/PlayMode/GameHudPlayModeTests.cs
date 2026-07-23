// GameHudPlayModeTests.cs — S-08 acceptance: Game シーンの HUD が現在資金・コアHP・現在ウェーブ番号を
// リアルタイム表示し、ビルドスポット左クリックでタワー種別選択メニュー（2種）を開く。資金不足の種別は
// 不足表示で選択不可、パネル外クリック/右クリックで選択解除する。設置により資金表示が減り、コア被弾で
// コアHP表示が減ること、種別選択メニューの開閉をクリック擬似発行で検証する（Canvas は ScreenSpaceCamera）。
//
// batch-verify 修正メモ（phase:prototype バッチ検証区間）: 以前は共有 [UnitySetUp] コルーチンでシーンロードを
// 行い、各 [UnityTest] 側で AddDevice→Press するパターンだったが、MenuScreenPlayModeTests/
// TitleScreenPlayModeTests で既知の batchmode 実測不具合（[UnitySetUp] と [UnityTest] の境界を跨ぐと
// テスト側で追加した Mouse デバイスの状態が InputActionMap 側の WasPressedThisFrame() に反映されない）と
// 同一原因でクリック系5テストが全滅していた。シーンロードとデバイス追加/入力擬似発行を同一コルーチン内に
// 収める（インライン化する）方式へ統一して解消（原因 story: S-08 実装時の並走レーン規律により Unity 未起動の
// 静的確認のみで review 化されたため、この不具合はバッチ検証区間まで顕在化しなかった）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class GameHudPlayModeTests : InputTestFixture
    {
        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Vector2 ScreenPointOfSpot(int spotIndex, Camera cam) =>
            cam.WorldToScreenPoint(GameConfig.Build.SpotPositions[spotIndex]);

        private static int FindEmptySpot(BuildSpotController build)
        {
            for (int i = 0; i < GameConfig.Build.SpotPositions.Length; i++)
            {
                if (!build.BuildSpots.IsOccupied(i)) return i;
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator HudCanvas_IsScreenSpaceCamera()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            Assert.IsNotNull(hud, "HudPanel コンポーネントが Game シーンに存在しない");

            var canvas = hud.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "HUD の Canvas が見つからない");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.IsNotNull(canvas.worldCamera, "worldCamera が未割当");
        }

        [UnityTest]
        public IEnumerator HudTexts_ShowInitialGoldCoreHpAndWave()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            yield return null; // Update() を1回通す

            Assert.IsTrue(hud.GoldText.text.Contains(GameConfig.Economy.StartingGold.ToString()), $"初期資金表示が不正: {hud.GoldText.text}");
            Assert.IsTrue(hud.CoreHpText.text.Contains(GameConfig.Core.HpMax.ToString()), $"初期コアHP表示が不正: {hud.CoreHpText.text}");
            Assert.IsTrue(hud.WaveText.text.Contains("1") && hud.WaveText.text.Contains(GameConfig.WaveComposition.Waves.Length.ToString()),
                $"初期ウェーブ表示が不正: {hud.WaveText.text}");
        }

        [UnityTest]
        public IEnumerator ClickEmptyBuildSpot_OpensTowerSelectPanel()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            var mouse = InputSystem.AddDevice<Mouse>();

            int emptySpot = FindEmptySpot(build);
            Vector2 point = ScreenPointOfSpot(emptySpot, hud.UiCamera);

            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(hud.TowerSelect.IsOpen, "ビルドスポット左クリックでメニューが開いていない");
            Assert.AreEqual(emptySpot, hud.TowerSelect.OpenSpotIndex);
        }

        [UnityTest]
        public IEnumerator SelectAffordableTower_PlacesTower_ClosesPanel_AndGoldDisplayDecreases()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            var mouse = InputSystem.AddDevice<Mouse>();

            int emptySpot = FindEmptySpot(build);
            Move(mouse.position, ScreenPointOfSpot(emptySpot, hud.UiCamera));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(hud.TowerSelect.IsOpen, "前提: メニューが開いていること");
            int goldBefore = build.Economy.Gold;

            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(hud.UiCamera, hud.TowerSelect.BastionCannonButtonRect.position);
            Move(mouse.position, buttonPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsFalse(hud.TowerSelect.IsOpen, "タワー選択後にメニューが閉じていない");
            Assert.IsTrue(build.BuildSpots.IsOccupied(emptySpot), "タワーが設置されていない");
            Assert.AreEqual(goldBefore - GameConfig.BastionCannon.Cost, build.Economy.Gold, "資金が実効コスト分減っていない");

            yield return null;
            Assert.IsTrue(hud.GoldText.text.Contains(build.Economy.Gold.ToString()), $"資金表示が減少後の値へ更新されていない: {hud.GoldText.text}");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InsufficientFundsSpot_BothButtonsDisabled_ClickDoesNotPlace()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            // 資金が両種コスト未満になるまで直接設置してテスト前提を成立させる
            // （TowerCombatPlayModeTests と同じ「直接 API 呼び出しで前提を作る」パターン）。
            int placed = 0;
            while (build.Economy.Gold >= GameConfig.BastionCannon.Cost && placed < GameConfig.Build.SpotPositions.Length - 1)
            {
                Assert.IsTrue(build.TryPlaceTower(placed, TowerType.BastionCannon).Success);
                placed++;
            }
            Assert.Less(build.Economy.Gold, GameConfig.ArcEmitter.Cost, "資金が両種コスト未満になるまで枯渇させる前提が成立していない");

            int emptySpot = FindEmptySpot(build);
            Assert.GreaterOrEqual(emptySpot, 0, "空きスポットが残っていない（テスト前提が壊れている）");

            var mouse = InputSystem.AddDevice<Mouse>();
            Move(mouse.position, ScreenPointOfSpot(emptySpot, hud.UiCamera));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsTrue(hud.TowerSelect.IsOpen);
            Assert.IsFalse(hud.TowerSelect.IsBastionCannonAffordable, "資金不足のはずが Bastion Cannon が選択可能表示になっている");
            Assert.IsFalse(hud.TowerSelect.IsArcEmitterAffordable, "資金不足のはずが Arc Emitter が選択可能表示になっている");
            Assert.IsTrue(hud.TowerSelect.BastionCannonLabel.text.Contains("資金不足"));
            Assert.IsTrue(hud.TowerSelect.ArcEmitterLabel.text.Contains("資金不足"));

            int goldBeforeClick = build.Economy.Gold;
            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(hud.UiCamera, hud.TowerSelect.BastionCannonButtonRect.position);
            Move(mouse.position, buttonPoint);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsFalse(build.BuildSpots.IsOccupied(emptySpot), "資金不足の種別がクリックで設置されてしまった");
            Assert.AreEqual(goldBeforeClick, build.Economy.Gold, "資金不足クリックで資金が変化した");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RightClick_ClosesPanel()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            var mouse = InputSystem.AddDevice<Mouse>();

            int emptySpot = FindEmptySpot(build);
            Move(mouse.position, ScreenPointOfSpot(emptySpot, hud.UiCamera));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.TowerSelect.IsOpen);

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;

            Assert.IsFalse(hud.TowerSelect.IsOpen, "右クリックでメニューが閉じていない");
        }

        [UnityTest]
        public IEnumerator OutsideClick_ClosesPanel()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            var mouse = InputSystem.AddDevice<Mouse>();

            int emptySpot = FindEmptySpot(build);
            Move(mouse.position, ScreenPointOfSpot(emptySpot, hud.UiCamera));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.TowerSelect.IsOpen);

            // パネル矩形から明確に外れる画面隅をクリックする。
            Move(mouse.position, new Vector2(5f, 5f));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.IsFalse(hud.TowerSelect.IsOpen, "パネル外クリックでメニューが閉じていない");
        }

        [UnityTest]
        public IEnumerator CoreHit_UpdatesCoreHpDisplay()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var coreView = Object.FindFirstObjectByType<CoreView>();
            int hpBefore = coreView.CurrentHp;

            coreView.ApplyDamage(EnemyType.Marauder);
            yield return null;
            yield return null;

            Assert.Less(coreView.CurrentHp, hpBefore, "コア被弾でコアHPが減っていない");
            Assert.IsTrue(hud.CoreHpText.text.Contains(coreView.CurrentHp.ToString()), $"コアHP表示が更新されていない: {hud.CoreHpText.text}");
        }
    }
}
