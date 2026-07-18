// MetaProgression — pure reducer over SaveData (gdd メタ進行). Takes values, returns a new
// SaveData; performs NO I/O (Persistence layer saves the result). Engine-independent.
using System;

namespace ForgeGame.Systems.Meta
{
    public static class MetaProgression
    {
        /// <summary>Effective attack damage after UPG-01 (gdd 実行時の補正式).</summary>
        public static float EffectiveAttackDamage(int upgradeAttackLevel) =>
            GameConfig.Player.AutoAttackDamageBase *
            (1f + GameConfig.Upgrade.AttackBonusPerLevel * upgradeAttackLevel);

        /// <summary>Effective move speed after UPG-02.</summary>
        public static float EffectiveMoveSpeed(int upgradeMoveSpeedLevel) =>
            GameConfig.Player.MoveSpeed *
            (1f + GameConfig.Upgrade.MoveSpeedBonusPerLevel * upgradeMoveSpeedLevel);

        /// <summary>Effective max HP after UPG-03.</summary>
        public static int EffectiveMaxHp(int upgradeMaxHpLevel) =>
            GameConfig.Player.MaxHpBase + GameConfig.Upgrade.MaxHpBonusPerLevel * upgradeMaxHpLevel;

        /// <summary>Crystal cost of the next level (gdd upgradeCost 擬似コード). level は 1 始まり。</summary>
        public static int UpgradeCost(int nextLevel) =>
            (int)Math.Round(GameConfig.Upgrade.CostBase *
                Math.Pow(GameConfig.Upgrade.CostGrowthPerLevel, nextLevel - 1));

        /// <summary>
        /// Fold a finished run into a new SaveData (gdd 勝敗条件: ApplyRunResult).
        /// Updates high score / bests / cumulative stats / crystal balance. Does not save.
        /// </summary>
        public static SaveData ApplyRunResult(SaveData current, RunResult run)
        {
            SaveData next = current.Clone();
            next.totalRunsPlayed += 1;
            next.totalKillCount += run.NormalKillCount + run.HeavyKillCount;
            next.totalCrystalsEarned += run.CrystalsCollected;
            next.crystalBalance += run.CrystalsCollected;
            if (run.FinalScore > next.highScore) next.highScore = run.FinalScore;
            if (run.SurvivalTimeSec > next.bestSurvivalTimeSec) next.bestSurvivalTimeSec = run.SurvivalTimeSec;
            if (run.WaveReached > next.bestWaveReached) next.bestWaveReached = run.WaveReached;
            return next;
        }

        /// <summary>True if run.FinalScore exceeds current.highScore (gdd Result 画面: ハイスコア更新の
        /// 有無表示). MUST be queried on the pre-run SaveData, before ApplyRunResult mutates highScore —
        /// callers pass ApplyRunResult's `current` argument, not its returned value.</summary>
        public static bool IsNewHighScore(SaveData current, RunResult run) => run.FinalScore > current.highScore;

        public enum UpgradeKind { Attack, MoveSpeed, MaxHp }

        public readonly struct PurchaseResult
        {
            public readonly SaveData Data;
            public readonly bool Purchased; // false = insufficient balance or at max level
            public PurchaseResult(SaveData data, bool purchased) { Data = data; Purchased = purchased; }
        }

        /// <summary>
        /// Attempt to buy one level of an upgrade (gdd Menu アップグレード購入). Pure: returns a
        /// new SaveData and whether the purchase happened. Insufficient balance / max level → no-op.
        /// </summary>
        public static PurchaseResult TryPurchase(SaveData current, UpgradeKind kind)
        {
            int level = LevelOf(current, kind);
            int max = MaxLevelOf(kind);
            if (level >= max) return new PurchaseResult(current, false);

            int cost = UpgradeCost(level + 1);
            if (current.crystalBalance < cost) return new PurchaseResult(current, false);

            SaveData next = current.Clone();
            next.crystalBalance -= cost;
            switch (kind)
            {
                case UpgradeKind.Attack: next.upgradeAttackLevel += 1; break;
                case UpgradeKind.MoveSpeed: next.upgradeMoveSpeedLevel += 1; break;
                case UpgradeKind.MaxHp: next.upgradeMaxHpLevel += 1; break;
            }
            return new PurchaseResult(next, true);
        }

        private static int LevelOf(SaveData d, UpgradeKind kind) => kind switch
        {
            UpgradeKind.Attack => d.upgradeAttackLevel,
            UpgradeKind.MoveSpeed => d.upgradeMoveSpeedLevel,
            UpgradeKind.MaxHp => d.upgradeMaxHpLevel,
            _ => 0,
        };

        private static int MaxLevelOf(UpgradeKind kind) => kind switch
        {
            UpgradeKind.Attack => GameConfig.Upgrade.AttackLevelMax,
            UpgradeKind.MoveSpeed => GameConfig.Upgrade.MoveSpeedLevelMax,
            UpgradeKind.MaxHp => GameConfig.Upgrade.MaxHpLevelMax,
            _ => 0,
        };
    }
}
