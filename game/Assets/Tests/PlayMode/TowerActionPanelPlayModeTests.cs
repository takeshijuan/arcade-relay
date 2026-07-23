// TowerActionPanelPlayModeTests.cs — S-23 acceptance: Game HUD で設置済みタワーへの左クリックにより
// アップグレード/売却パネル（Ui/TowerActionPanel）を開き、「強化」クリックで
// Components/BuildSpotController.TryUpgradeTower（Lv進行・実効コスト分の資金減算）、「売却」クリックで
// Components/BuildSpotController.TrySellTower（スポット解放・返還額分の資金増加）へそれぞれ到達することを
// クリック擬似発行で検証する。CR-CODE S-10/S-11 で継続エスカレーションされていた [BLOCKER]
// （プレイヤー入力から両メソッドへ到達する経路が存在しない — state/reviews/s-10.md, s-11.md）を解消する。
//
// GameHudPlayModeTests.cs と同じ「シーンロードと入力擬似発行を同一コルーチン内に収める」パターンを踏襲する
// （tech-stack-unity.md「既知の落とし穴」: [UnitySetUp]/[UnityTest] 境界を跨ぐと入力デバイス状態が
// InputActionMap 側の WasPressedThisFrame() に反映されない batchmode 既知不具合の回避）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Systems;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class TowerActionPanelPlayModeTests : InputTestFixture
    {
        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Vector2 ScreenPointOfSpot(int spotIndex, Camera cam) =>
            cam.WorldToScreenPoint(GameConfig.Build.SpotPositions[spotIndex]);

        private IEnumerator ClickAt(Mouse mouse, Vector2 point)
        {
            Move(mouse.position, point);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickPlacedTower_OpensActionPanel_WithLv1Info()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            Assert.IsNotNull(hud);
            Assert.IsNotNull(build);

            PlacementResult placement = build.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, "前提: タワー設置に成功していること");

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));

            Assert.IsTrue(hud.TowerAction.IsOpen, "設置済みタワー左クリックでアップグレード/売却パネルが開いていない");
            Assert.AreEqual(placement.Tower.Id, hud.TowerAction.OpenTowerId);
            Assert.IsTrue(hud.TowerAction.CurrentInfoText.text.Contains(GameConfig.BastionCannon.DamageLv1.ToString()),
                $"Lv1現在ダメージ表示が不正: {hud.TowerAction.CurrentInfoText.text}");
            Assert.IsTrue(hud.TowerAction.IsUpgradeAvailable, "資金充足のはずが強化ボタンが非活性表示になっている");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClickUpgradeButton_UpgradesTowerToLv2_AndDeductsEffectiveCost()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            PlacementResult placement = build.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            int towerId = placement.Tower.Id;

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));
            Assert.IsTrue(hud.TowerAction.IsOpen, "前提: アップグレード/売却パネルが開いていること");

            int goldBefore = build.Economy.Gold;
            Vector2 upgradeButtonPoint = RectTransformUtility.WorldToScreenPoint(hud.UiCamera, hud.TowerAction.UpgradeButtonRect.position);
            yield return ClickAt(mouse, upgradeButtonPoint);

            Assert.IsFalse(hud.TowerAction.IsOpen, "強化クリック後にパネルが閉じていない");

            TowerInstance? upgraded = FindTower(build, towerId);
            Assert.IsTrue(upgraded.HasValue, "強化後にタワーが見つからない");
            Assert.AreEqual(2, upgraded.Value.Level, "強化クリックで Lv2 になっていない");
            // CR-CODE S-23 iter1 minor fix: 期待減算額は基礎コスト直書きではなく acceptance の「実効コスト
            // （基礎コスト×(1-UPG-02割引率)）」の導出そのもの（EconomySystem.ComputeEffectiveCost）で求める。
            // 既定セーブ（UPG-02 Lv0=割引0）では数値上は基礎コストと一致するが、導出経路を売却テスト
            // （下記 ComputeSellRefund 経由）と対称にし、既定割引が変わっても false-fail しないようにする。
            int expectedUpgradeCost = EconomySystem.ComputeEffectiveCost(GameConfig.BastionCannon.UpgradeLv2Cost, build.TowerDiscountRate);
            Assert.AreEqual(goldBefore - expectedUpgradeCost, build.Economy.Gold,
                "資金が実効コスト分減っていない");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClickSellButton_SellsTower_ReopensSpot_AndRefundsGold()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            PlacementResult placement = build.TryPlaceTower(1, TowerType.ArcEmitter);
            Assert.IsTrue(placement.Success);
            int investedGold = placement.EffectiveCost;
            int expectedRefund = EconomySystem.ComputeSellRefund(investedGold, GameConfig.Build.SellRefundRate);

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(1, hud.UiCamera));
            Assert.IsTrue(hud.TowerAction.IsOpen, "前提: アップグレード/売却パネルが開いていること");

            int goldBefore = build.Economy.Gold;
            Vector2 sellButtonPoint = RectTransformUtility.WorldToScreenPoint(hud.UiCamera, hud.TowerAction.SellButtonRect.position);
            yield return ClickAt(mouse, sellButtonPoint);

            Assert.IsFalse(hud.TowerAction.IsOpen, "売却クリック後にパネルが閉じていない");
            Assert.IsFalse(build.BuildSpots.IsOccupied(1), "売却後にスポットが空きに戻っていない");
            Assert.AreEqual(goldBefore + expectedRefund, build.Economy.Gold, "資金が返還額分増えていない");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RightClick_ClosesActionPanel()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            Assert.IsTrue(build.TryPlaceTower(0, TowerType.BastionCannon).Success);

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));
            Assert.IsTrue(hud.TowerAction.IsOpen);

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;

            Assert.IsFalse(hud.TowerAction.IsOpen, "右クリックでアップグレード/売却パネルが閉じていない");
        }

        [UnityTest]
        public IEnumerator OutsideClick_ClosesActionPanel()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            Assert.IsTrue(build.TryPlaceTower(0, TowerType.BastionCannon).Success);

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));
            Assert.IsTrue(hud.TowerAction.IsOpen);

            yield return ClickAt(mouse, new Vector2(5f, 5f));

            Assert.IsFalse(hud.TowerAction.IsOpen, "パネル外クリックでアップグレード/売却パネルが閉じていない");
        }

        [UnityTest]
        public IEnumerator MaxLevelTower_UpgradeButtonDisabled_ShowsMaxedText()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            PlacementResult placement = build.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            int towerId = placement.Tower.Id;
            Assert.IsTrue(build.TryUpgradeTower(towerId).Success, "前提: Lv2 化に成功していること");
            // StartingGold(100) - 設置(50) - Lv2強化(40) = 残10 では Lv3強化(70)に届かないため補充する
            // （TowerCombatPlayModeTests の同種前提構築パターンと同じ理由 — state/reviews/s-10.md 参照）。
            build.Economy.Add(GameConfig.BastionCannon.UpgradeLv3Cost);
            Assert.IsTrue(build.TryUpgradeTower(towerId).Success, "前提: Lv3 化に成功していること");

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));

            Assert.IsTrue(hud.TowerAction.IsOpen);
            Assert.IsFalse(hud.TowerAction.IsUpgradeAvailable, "Lv3到達済みなのに強化ボタンが活性表示になっている");
            Assert.IsTrue(hud.TowerAction.NextInfoText.text.Contains("最大強化済み"),
                $"Lv3到達済みの表示が不正: {hud.TowerAction.NextInfoText.text}");

            LogAssert.NoUnexpectedReceived();
        }

        // CR-CODE S-23 iter2 minor fix: acceptance「資金不足時はボタンを事前に非活性表示する」の資金不足
        // ケース直接テストが無く、非活性検証は MaxLevelTower ケース（Lv3到達）のみだった。
        // GameHudPlayModeTests.InsufficientFundsSpot_BothButtonsDisabled_ClickDoesNotPlace と対称に、
        // 「IsUpgradeAvailable==false のとき非活性ボタンのクリックが TryUpgradeTower に到達しない
        // （TryHandleClick の upgradeAvailable ガードで action=None になり、パネル内クリックのため
        // Close() もされない）」ことを検証する。
        [UnityTest]
        public IEnumerator InsufficientFundsTower_UpgradeButtonDisabled_ClickDoesNotUpgrade()
        {
            yield return LoadGame();

            var hud = Object.FindFirstObjectByType<HudPanel>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();

            PlacementResult placement = build.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, "前提: タワー設置に成功していること");
            int towerId = placement.Tower.Id;

            // StartingGold(100) - 設置(50) = 残50 では Lv2強化(40)に届いてしまうため、
            // UpgradeLv2Cost 未満まで資金を追加で減らして資金不足前提を作る
            // （MaxLevelTower ケースの Economy.Add と対称の「直接 API で前提を作る」パターン）。
            Assert.IsTrue(build.Economy.TrySpend(GameConfig.BastionCannon.Cost / 2),
                "前提: 資金を Lv2 強化費未満まで減らせること");
            Assert.Less(build.Economy.Gold, GameConfig.BastionCannon.UpgradeLv2Cost,
                "資金不足前提が成立していない（残高が強化費以上）");

            var mouse = InputSystem.AddDevice<Mouse>();
            yield return ClickAt(mouse, ScreenPointOfSpot(0, hud.UiCamera));
            Assert.IsTrue(hud.TowerAction.IsOpen, "前提: アップグレード/売却パネルが開いていること");
            Assert.IsFalse(hud.TowerAction.IsUpgradeAvailable, "資金不足のはずが強化ボタンが活性表示になっている");
            Assert.IsTrue(hud.TowerAction.UpgradeLabel.text.Contains("資金不足"),
                $"資金不足の表示が不正: {hud.TowerAction.UpgradeLabel.text}");

            int goldBeforeClick = build.Economy.Gold;
            Vector2 upgradeButtonPoint = RectTransformUtility.WorldToScreenPoint(hud.UiCamera, hud.TowerAction.UpgradeButtonRect.position);
            yield return ClickAt(mouse, upgradeButtonPoint);

            Assert.IsTrue(hud.TowerAction.IsOpen, "資金不足クリックでパネルが閉じてしまった（非活性ボタンはクリック不可のはず）");
            TowerInstance? afterClick = FindTower(build, towerId);
            Assert.IsTrue(afterClick.HasValue);
            Assert.AreEqual(1, afterClick.Value.Level, "資金不足クリックでタワーが強化されてしまった");
            Assert.AreEqual(goldBeforeClick, build.Economy.Gold, "資金不足クリックで資金が変化した");

            LogAssert.NoUnexpectedReceived();
        }

        private static TowerInstance? FindTower(BuildSpotController build, int towerId)
        {
            System.Collections.Generic.IReadOnlyList<TowerInstance> towers = build.BuildSpots.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].Id == towerId) return towers[i];
            }
            return null;
        }
    }
}
