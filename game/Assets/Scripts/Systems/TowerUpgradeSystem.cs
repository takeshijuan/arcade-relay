// TowerUpgradeSystem.cs — タワーアップグレード（純粋 C#・エンジン非依存。S-10）。
// gdd「タワーアップグレード」節（P-01, P-02）+「タワー仕様」節: 役割（種別・射程・発射/tick間隔）は変えず
// ダメージのみ増加させる（Lv1→Lv2→Lv3。Lv3で打止め）。射程・発射間隔・ターゲティングは
// TowerCombatSystem がタワー種別のみで決めるため、本システムは TowerInstance.Level を進めるだけで
// 役割が自動的に不変であることを保証する（種別を変えるコードパスを持たない）。
// 実効コスト = 基礎アップグレードコスト×(1-discountRate)（EconomySystem.ComputeEffectiveCost と同じ式）。
// UPG-02 割引率の実際の反映（SaveData の upgTowerDiscountLv から discountRate を算出して呼び出す配線）は
// S-14 で実装済み: Components/BuildSpotController.Awake が Systems/Meta/MetaProgression.ComputeTowerDiscountRate
// で算出した値を保持し、TryUpgradeTower（本メソッドの呼び出し元）が渡す。discountRate 引数の既定値 0f は
// 未購入（Lv0）時の実効値と一致するため、単体テスト等で discountRate を省略した場合も既存挙動と等価。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。
using System;
using System.Collections.Generic;

namespace ForgeGame.Systems
{
    public enum TowerUpgradeFailureReason
    {
        None = 0,
        TowerNotFound,
        AlreadyMaxLevel,
        InsufficientGold,
    }

    /// <summary>TryUpgrade の結果（成功時は Tower.Level が更新済み・失敗時は FailureReason を見る）。</summary>
    public readonly struct TowerUpgradeResult
    {
        public readonly bool Success;
        public readonly TowerUpgradeFailureReason FailureReason;
        public readonly TowerInstance Tower; // 成功時: 更新後の状態。失敗時: 現状（TowerNotFound は既定値）
        public readonly int EffectiveCost;

        public TowerUpgradeResult(bool success, TowerUpgradeFailureReason failureReason, TowerInstance tower, int effectiveCost)
        {
            Success = success;
            FailureReason = failureReason;
            Tower = tower;
            EffectiveCost = effectiveCost;
        }
    }

    /// <summary>
    /// 設置済みタワーの Lv 強化（Lv1→2→3）。BuildSpotSystem.Towers から towerId で対象を探し、
    /// 資金充足時のみ Level をインクリメントして BuildSpotSystem.UpdateTower で反映する。
    /// ダメージ量自体は TowerCombatSystem.DamageForLevel が Level を参照して決めるため、
    /// ここでは Level を進めるだけで攻撃側の挙動更新は自動的に反映される。
    /// </summary>
    public static class TowerUpgradeSystem
    {
        public static TowerUpgradeResult TryUpgrade(BuildSpotSystem buildSpots, int towerId, EconomySystem economy, float discountRate = 0f)
        {
            if (buildSpots == null) throw new ArgumentNullException(nameof(buildSpots));
            if (economy == null) throw new ArgumentNullException(nameof(economy));

            IReadOnlyList<TowerInstance> towers = buildSpots.Towers;
            int index = -1;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].Id == towerId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return new TowerUpgradeResult(false, TowerUpgradeFailureReason.TowerNotFound, default, 0);

            TowerInstance tower = towers[index];
            if (tower.Level >= GameConfig.Tower.MaxLevel)
                return new TowerUpgradeResult(false, TowerUpgradeFailureReason.AlreadyMaxLevel, tower, 0);

            int baseCost = BaseUpgradeCost(tower.Type, tower.Level);
            int effectiveCost = EconomySystem.ComputeEffectiveCost(baseCost, discountRate);

            if (!economy.TrySpend(effectiveCost))
                return new TowerUpgradeResult(false, TowerUpgradeFailureReason.InsufficientGold, tower, effectiveCost);

            tower.Level += 1;
            tower.InvestedGold += effectiveCost; // S-11 売却返還額の算出基礎（設置+強化投入額の累計）
            buildSpots.UpdateTower(index, tower);
            return new TowerUpgradeResult(true, TowerUpgradeFailureReason.None, tower, effectiveCost);
        }

        /// <summary>現在Lv→次Lvへ上げるための基礎コスト（gdd 数値表 UpgradeLv2Cost/UpgradeLv3Cost）。</summary>
        private static int BaseUpgradeCost(TowerType type, int currentLevel)
        {
            switch (currentLevel)
            {
                case 1:
                    return type == TowerType.BastionCannon
                        ? GameConfig.BastionCannon.UpgradeLv2Cost
                        : GameConfig.ArcEmitter.UpgradeLv2Cost;
                case 2:
                    return type == TowerType.BastionCannon
                        ? GameConfig.BastionCannon.UpgradeLv3Cost
                        : GameConfig.ArcEmitter.UpgradeLv3Cost;
                default:
                    // TryUpgrade は Level>=MaxLevel(3) を事前に AlreadyMaxLevel で弾くため、
                    // currentLevel は常に 1〜2 のはず。到達したら TowerInstance.Level の不整合。
                    throw new ArgumentOutOfRangeException(nameof(currentLevel), currentLevel,
                        "currentLevel は 1〜2 の範囲である必要がある（Lv3 は打止め）。");
            }
        }
    }
}
