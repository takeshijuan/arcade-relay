// BuildSpotControllerUpgradePlayModeTests.cs — S-14 acceptance:
// 「Menu で essence を UPG_PURCHASE_COST_PER_LV 消費して UPG-01/02/03 を Lv 上限3まで購入でき、購入は SaveData に
//  永続化される。ラン開始時に UPG-01（初期資金+15/Lv）・UPG-02（設置/強化コスト割引-3%/Lv）・
//  UPG-03（essence獲得率+8%/Lv）が初期条件へ反映される（敵側パラメータは不変）。EditMode/PlayMode テストで、
//  UPG-01 Lv2 購入後のラン初期資金が STARTING_GOLD+30、essence が購入コスト分減ることを検証できる。」
// 購入トランザクション自体（essence消費+Lv上限）の純粋ロジック検証は Assets/Tests/EditMode/ScaffoldSmokeTests.cs
// （MetaProgression.TryPurchaseUpgrade 系）が担う。本ファイルは「購入済み SaveData → GameFlow.CurrentSaveData
// （Menu→Game 遷移相当のプロセス内キャリー）→ Game シーン開始（BuildSpotController.Awake）」という実際の
// シーン跨ぎ経路を通して、ラン開始条件（EconomySystem 初期資金・タワー実効コスト割引）へ反映されることを検証する
// （docs/architecture.md データフロー節「EconomySystem 初期資金 = STARTING_GOLD + UPG-01 効果」）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.PlayMode
{
    public class BuildSpotControllerUpgradePlayModeTests
    {
        private GameObject buildGo;
        private GameObject waveGo;
        private GameObject coreGo;

        [TearDown]
        public void TearDown()
        {
            if (buildGo != null) Object.Destroy(buildGo);
            if (waveGo != null) Object.Destroy(waveGo);
            if (coreGo != null) Object.Destroy(coreGo);
            // GameFlow は static のためテスト間で生存する（RunOutcomePlayModeTests 等と同じ既存パターン）。
            GameFlow.SetCurrentSaveData(null);
        }

        /// <summary>TowerCombatPlayModeTests と同じ規約9パターン（非アクティブ生成→注入→アクティブ化）。
        /// SetActive(true) の時点で BuildSpotController.Awake() が実行され、その時点の
        /// GameFlow.CurrentSaveData が初期資金/割引率へ反映される。</summary>
        private BuildSpotController CreateBuildSpotController()
        {
            coreGo = new GameObject("TestCore");
            coreGo.SetActive(false);
            CoreView coreView = coreGo.AddComponent<CoreView>();
            coreGo.SetActive(true);

            waveGo = new GameObject("TestWaveSpawnController");
            waveGo.SetActive(false);
            WaveSpawnController waveController = waveGo.AddComponent<WaveSpawnController>();
            waveController.SetCoreViewForTest(coreView);
            waveGo.SetActive(true);
            waveController.enabled = false;

            buildGo = new GameObject("TestBuildSpotController");
            buildGo.SetActive(false);
            BuildSpotController controller = buildGo.AddComponent<BuildSpotController>();
            controller.SetWaveSpawnControllerForTest(waveController);
            buildGo.SetActive(true); // Awake() 実行 = ラン開始条件の確定タイミング
            controller.enabled = false;
            return controller;
        }

        [UnityTest]
        public IEnumerator NoUpgrades_StartingGold_MatchesBaseConfig_AndZeroDiscount()
        {
            GameFlow.SetCurrentSaveData(null); // 未購入（Boot 非経由の単体テスト相当）は Lv0 効果無し
            BuildSpotController controller = CreateBuildSpotController();

            Assert.AreEqual(GameConfig.Economy.StartingGold, controller.Economy.Gold);
            Assert.AreEqual(0f, controller.TowerDiscountRate);
            // CR-CODE S-14 iter2 major対応: GameFlow.CurrentSaveData 未設定時の Boot 非経由フォールバックは
            // Debug.Log ではなく非ログの観測用フラグ（UsedDefaultSaveFallback）で表面化する
            // （Debug.Log 版は LogAssert.NoUnexpectedReceived() を汚し他フィクスチャを破壊するため撤回済み — 上記コメント参照）。
            Assert.IsTrue(controller.UsedDefaultSaveFallback);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PurchasedUpg01Lv2_RunStart_StartingGoldIsBaseConfigPlus30_AndEssenceSpent()
        {
            // Menu 購入操作の正本 reducer（Systems/Meta/MetaProgression.TryPurchaseUpgrade）を2回適用し、
            // UPG-01 を Lv0→Lv2 まで購入する。
            SaveData data = SaveData.CreateDefault();
            data.essence = GameConfig.Meta.UpgradePurchaseCostPerLevel * 2;
            int essenceBeforePurchases = data.essence;

            PurchaseUpgradeResult r1 = MetaProgression.TryPurchaseUpgrade(data, UpgradeKind.Upg01StartingGold);
            Assert.IsTrue(r1.Success, "1回目の購入が成功すること");
            data = r1.Data;
            PurchaseUpgradeResult r2 = MetaProgression.TryPurchaseUpgrade(data, UpgradeKind.Upg01StartingGold);
            Assert.IsTrue(r2.Success, "2回目の購入が成功すること");
            data = r2.Data;

            Assert.AreEqual(2, data.upgStartingGoldLv);
            // acceptance: essence が購入コスト分減ること
            Assert.AreEqual(essenceBeforePurchases - GameConfig.Meta.UpgradePurchaseCostPerLevel * 2, data.essence);

            // 購入済み SaveData を Menu→Game 遷移相当のキャリー（GameFlow.CurrentSaveData）へ乗せてラン開始。
            GameFlow.SetCurrentSaveData(data);
            BuildSpotController controller = CreateBuildSpotController();

            // acceptance: UPG-01 Lv2 購入後のラン初期資金が STARTING_GOLD+30
            Assert.AreEqual(GameConfig.Economy.StartingGold + 30, controller.Economy.Gold);
            Assert.AreEqual(
                GameConfig.Economy.StartingGold + 2 * GameConfig.Meta.Upg01GoldPerLevel,
                controller.Economy.Gold);
            // GameFlow.CurrentSaveData が設定済み（Boot 経由相当）のためフォールバックは発生しない。
            Assert.IsFalse(controller.UsedDefaultSaveFallback);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PurchasedUpg02Lv1_RunStart_TowerPlacementEffectiveCostIsDiscounted()
        {
            SaveData data = SaveData.CreateDefault();
            data.essence = GameConfig.Meta.UpgradePurchaseCostPerLevel;
            PurchaseUpgradeResult result = MetaProgression.TryPurchaseUpgrade(data, UpgradeKind.Upg02TowerDiscount);
            Assert.IsTrue(result.Success);
            data = result.Data;
            Assert.AreEqual(1, data.upgTowerDiscountLv);

            GameFlow.SetCurrentSaveData(data);
            BuildSpotController controller = CreateBuildSpotController();

            float expectedDiscount = 1 * GameConfig.Meta.Upg02DiscountPerLevel;
            Assert.AreEqual(expectedDiscount, controller.TowerDiscountRate, 1e-6f);

            int expectedCost = EconomySystem.ComputeEffectiveCost(GameConfig.BastionCannon.Cost, expectedDiscount);
            PlacementResult placement = controller.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, $"設置に失敗: {placement.FailureReason}");
            Assert.AreEqual(expectedCost, placement.EffectiveCost);
            Assert.Less(expectedCost, GameConfig.BastionCannon.Cost, "UPG-02 割引が実効コストへ反映されていること");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoUpgrades_TowerPlacementEffectiveCost_EqualsBaseCost()
        {
            // acceptance: UPG-01/02 は初期条件のみを変え、敵側パラメータ（HP・速度・出現数）は不変
            // （本 story は Wave/Marauder/Warbeast の GameConfig 定数を一切参照・変更しないため、
            // 未購入時の基準値が変わっていないことをタワー実効コストの回帰確認で代表させる）。
            GameFlow.SetCurrentSaveData(null);
            BuildSpotController controller = CreateBuildSpotController();

            PlacementResult placement = controller.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            Assert.AreEqual(GameConfig.BastionCannon.Cost, placement.EffectiveCost, "未購入時は割引なし＝基礎コストのまま");
            yield return null;
        }
    }
}
