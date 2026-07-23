// TowerUpgradeSystemTests.cs — S-10 acceptance の純粋ロジック部分（Unity 起動なしで検証）。
// 「設置済みタワー左クリックでアップグレード/売却パネルを開き、資金が次Lv実効コスト
//  （基礎コスト×(1-UPG-02割引率)）以上なら Lv を上げダメージのみ増加する（役割・射程・間隔は不変、
//  Lv3 で打止め）」を、TowerUpgradeSystem（Lv進行・実効コスト・資金判定）と
// TowerCombatSystem（Lv別ダメージ/発）を組み合わせて検証する。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ForgeGame;
using ForgeGame.Systems;

namespace ForgeGame.Tests.EditMode
{
    public class TowerUpgradeSystemTests
    {
        private static EnemyInstance MakeStationaryTarget(int id, Vector3 towerPosition, int hp) =>
            new EnemyInstance
            {
                Id = id,
                Type = EnemyType.Warbeast,
                DistanceTraveledM = towerPosition.x - GameConfig.Path.StartPoint.x,
                Active = true,
                Hp = hp,
            };

        [Test]
        public void TryUpgrade_Lv1ToLv2_Succeeds_DeductsEffectiveCost_AndIncrementsLevel()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost + GameConfig.BastionCannon.UpgradeLv2Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Assert.IsTrue(placed.Success);
            int goldBefore = economy.Gold;

            TowerUpgradeResult result = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);

            Assert.IsTrue(result.Success, $"アップグレードに失敗: {result.FailureReason}");
            Assert.AreEqual(TowerUpgradeFailureReason.None, result.FailureReason);
            Assert.AreEqual(GameConfig.BastionCannon.UpgradeLv2Cost, result.EffectiveCost);
            Assert.AreEqual(2, result.Tower.Level);
            Assert.AreEqual(2, buildSpots.Towers[0].Level, "BuildSpotSystem 側の状態も更新されていること");
            Assert.AreEqual(goldBefore - GameConfig.BastionCannon.UpgradeLv2Cost, economy.Gold);
        }

        [Test]
        public void TryUpgrade_Lv2ToLv3_Succeeds_ThenLv3IsMaxLevel_FailsFurtherUpgrade()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(
                GameConfig.ArcEmitter.Cost + GameConfig.ArcEmitter.UpgradeLv2Cost + GameConfig.ArcEmitter.UpgradeLv3Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.ArcEmitter, economy);
            Assert.IsTrue(placed.Success);

            TowerUpgradeResult toLv2 = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsTrue(toLv2.Success);
            Assert.AreEqual(2, toLv2.Tower.Level);

            int goldBeforeLv3 = economy.Gold;
            TowerUpgradeResult toLv3 = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsTrue(toLv3.Success);
            Assert.AreEqual(GameConfig.ArcEmitter.UpgradeLv3Cost, toLv3.EffectiveCost);
            Assert.AreEqual(3, toLv3.Tower.Level);
            Assert.AreEqual(goldBeforeLv3 - GameConfig.ArcEmitter.UpgradeLv3Cost, economy.Gold);

            TowerUpgradeResult beyondLv3 = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsFalse(beyondLv3.Success);
            Assert.AreEqual(TowerUpgradeFailureReason.AlreadyMaxLevel, beyondLv3.FailureReason);
            Assert.AreEqual(3, buildSpots.Towers[0].Level, "打止め後もLvは変化しない");
        }

        [Test]
        public void TryUpgrade_InsufficientGold_Fails_AndDoesNotChangeLevelOrGold()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Assert.IsTrue(placed.Success);
            Assert.AreEqual(0, economy.Gold, "設置で資金を使い切り、強化に必要な資金が無い状態を前提にする");

            TowerUpgradeResult result = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TowerUpgradeFailureReason.InsufficientGold, result.FailureReason);
            Assert.AreEqual(GameConfig.BastionCannon.UpgradeLv2Cost, result.EffectiveCost);
            Assert.AreEqual(0, economy.Gold);
            Assert.AreEqual(1, buildSpots.Towers[0].Level);
        }

        [Test]
        public void TryUpgrade_UnknownTowerId_ReturnsTowerNotFound()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(1000);

            TowerUpgradeResult result = TowerUpgradeSystem.TryUpgrade(buildSpots, 999, economy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TowerUpgradeFailureReason.TowerNotFound, result.FailureReason);
        }

        [Test]
        public void TryUpgrade_WithDiscountRate_AppliesSameFormulaAsPlacement()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(1000);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            int goldBefore = economy.Gold;

            // CR-CODE S-10 iter1 minor #4: ハードコード値ではなく GameConfig.Meta.Upg02DiscountPerLevel から
            // 導出する（S-14 で調整レンジ内の値変更があっても黙って乖離しない）。
            const float discountRate = 2 * GameConfig.Meta.Upg02DiscountPerLevel; // UPG-02 Lv2 相当（-3%/Lv）
            TowerUpgradeResult result = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy, discountRate);

            int expectedCost = EconomySystem.ComputeEffectiveCost(GameConfig.BastionCannon.UpgradeLv2Cost, discountRate);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(expectedCost, result.EffectiveCost);
            Assert.AreEqual(goldBefore - expectedCost, economy.Gold);
        }

        [Test]
        public void TryUpgrade_NullArgs_Throw()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(1000);
            Assert.Throws<System.ArgumentNullException>(() => TowerUpgradeSystem.TryUpgrade(null, 0, economy));
            Assert.Throws<System.ArgumentNullException>(() => TowerUpgradeSystem.TryUpgrade(buildSpots, 0, null));
        }

        [Test]
        public void AfterUpgrade_BastionCannon_FiresLv2ThenLv3Damage_RoleUnchanged()
        {
            // acceptance: 「Lv を上げダメージのみ増加する（役割・射程・間隔は不変、Lv3 で打止め）」を
            // TowerCombatSystem の実発射ダメージで検証する（役割=単体高火力・射程/間隔は GameConfig 定数の
            // ままで Level だけが変わることを damage 値の変化で確認）。
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(
                GameConfig.BastionCannon.Cost + GameConfig.BastionCannon.UpgradeLv2Cost + GameConfig.BastionCannon.UpgradeLv3Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Vector3 towerPosition = GameConfig.Build.SpotPositions[0];

            var target = new List<EnemyInstance> { MakeStationaryTarget(1, towerPosition, GameConfig.Warbeast.Hp * 10) };

            var eventsLv1 = new List<DamageEvent>();
            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, target, eventsLv1);
            Assert.AreEqual(1, eventsLv1.Count);
            Assert.AreEqual(GameConfig.BastionCannon.DamageLv1, eventsLv1[0].Damage);
            Assert.AreEqual(TowerType.BastionCannon, eventsLv1[0].SourceTowerType, "アップグレードで役割(種別)は変わらない");

            TowerUpgradeResult toLv2 = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsTrue(toLv2.Success);
            var eventsLv2 = new List<DamageEvent>();
            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, target, eventsLv2);
            Assert.AreEqual(1, eventsLv2.Count);
            Assert.AreEqual(GameConfig.BastionCannon.DamageLv2, eventsLv2[0].Damage);

            TowerUpgradeResult toLv3 = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsTrue(toLv3.Success);
            var eventsLv3 = new List<DamageEvent>();
            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, target, eventsLv3);
            Assert.AreEqual(1, eventsLv3.Count);
            Assert.AreEqual(GameConfig.BastionCannon.DamageLv3, eventsLv3[0].Damage);
        }
    }
}
