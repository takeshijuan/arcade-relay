// MetaProgressionTests — S-01/S-13: メタ進行 pure reducer (Systems/Meta/MetaProgression).
// Conventions.md §9: pure Systems get EditMode coverage. Locks in the gdd upgradeCost 擬似コード's
// literal cost curve (including Math.Round's banker's-rounding midpoint behavior at level 3), the
// TryPurchase exact-balance boundary, and the UPG-01/UPG-02 effective-stat formulas that
// PlayerController/AutoAttackDriver apply at run start.
using ForgeGame.Systems.Meta;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class MetaProgressionTests
    {
        [Test]
        public void UpgradeCost_Levels1Through5_FollowTheGddCostCurve()
        {
            // gdd 擬似コード: cost(n) = round(UPGRADE_COST_BASE * UPGRADE_COST_GROWTH_PER_LEVEL^(n-1))
            // — CostBase=50, CostGrowthPerLevel=1.5 → raw 50 / 75 / 112.5 / 168.75 / 253.125.
            Assert.AreEqual(50, MetaProgression.UpgradeCost(1));
            Assert.AreEqual(75, MetaProgression.UpgradeCost(2));
            Assert.AreEqual(112, MetaProgression.UpgradeCost(3),
                "level 3's raw cost 112.5 is a midpoint — MetaProgression.UpgradeCost uses Math.Round's default banker's rounding (MidpointRounding.ToEven), so it yields the even 112, not 113");
            Assert.AreEqual(169, MetaProgression.UpgradeCost(4));
            Assert.AreEqual(253, MetaProgression.UpgradeCost(5));
        }

        [Test]
        public void TryPurchase_BalanceExactlyEqualToCost_PurchasesAndLeavesZeroBalance()
        {
            SaveData save = SaveData.CreateDefault();
            save.crystalBalance = MetaProgression.UpgradeCost(1);

            MetaProgression.PurchaseResult result =
                MetaProgression.TryPurchase(save, MetaProgression.UpgradeKind.Attack);

            Assert.IsTrue(result.Purchased,
                "balance exactly == cost must pass the `crystalBalance < cost` insufficiency check (boundary case)");
            Assert.AreEqual(0, result.Data.crystalBalance, "the full balance must be spent");
            Assert.AreEqual(1, result.Data.upgradeAttackLevel);
        }

        [Test]
        public void EffectiveAttackDamage_Level0_IsExactlyTheBaseDamage()
        {
            Assert.AreEqual((float)GameConfig.Player.AutoAttackDamageBase,
                MetaProgression.EffectiveAttackDamage(0), 1e-5f,
                "level 0 must apply no bonus (Lv0 baseline = AUTO_ATTACK_DAMAGE_BASE)");
        }

        [Test]
        public void EffectiveAttackDamage_Level2_AppliesTwoBonusIncrementsMultiplicatively()
        {
            float expected = GameConfig.Player.AutoAttackDamageBase *
                (1f + GameConfig.Upgrade.AttackBonusPerLevel * 2f);

            Assert.AreEqual(expected, MetaProgression.EffectiveAttackDamage(2), 1e-4f,
                "gdd 実行時の補正式: base * (1 + UPG_ATTACK_BONUS_PER_LEVEL * level)");
        }

        [Test]
        public void EffectiveMoveSpeed_Level0_IsExactlyTheBaseMoveSpeed()
        {
            Assert.AreEqual(GameConfig.Player.MoveSpeed,
                MetaProgression.EffectiveMoveSpeed(0), 1e-5f,
                "level 0 must apply no bonus (Lv0 baseline = PLAYER_MOVE_SPEED)");
        }

        [Test]
        public void EffectiveMoveSpeed_Level2_AppliesTwoBonusIncrementsMultiplicatively()
        {
            float expected = GameConfig.Player.MoveSpeed *
                (1f + GameConfig.Upgrade.MoveSpeedBonusPerLevel * 2f);

            Assert.AreEqual(expected, MetaProgression.EffectiveMoveSpeed(2), 1e-4f,
                "gdd 実行時の補正式: base * (1 + UPG_MOVE_SPEED_BONUS_PER_LEVEL * level)");
        }
    }
}
