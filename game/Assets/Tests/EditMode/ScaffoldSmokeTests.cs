// ScaffoldSmokeTests.cs — scaffold のコンパイル検証を兼ねる最小 EditMode テスト（規約7）。
// 純粋ロジック（GameConfig 定数の健全性・MetaProgression reducer・ScoreSystem）を検証する。
// story 実装が進むにつれ各 System の EditMode テストがここに追加される。
using NUnit.Framework;
using ForgeGame;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.EditMode
{
    public class ScaffoldSmokeTests
    {
        [Test]
        public void GameConfig_CoreValues_AreWithinDesignRanges()
        {
            Assert.AreEqual(8, GameConfig.Wave.WaveCount);        // brief 確定値
            Assert.AreEqual(100, GameConfig.Core.HpMax);
            Assert.AreEqual(100, GameConfig.Economy.StartingGold);
            Assert.AreEqual(6, GameConfig.Build.NumBuildSpots);
            Assert.AreEqual(3, GameConfig.Tower.MaxLevel);
            Assert.AreEqual(1, GameConfig.Save.CurrentVersion);
        }

        [Test]
        public void SaveData_CreateDefault_MatchesGddInitialValues()
        {
            SaveData d = SaveData.CreateDefault();
            Assert.AreEqual(GameConfig.Save.CurrentVersion, d.save_version);
            Assert.AreEqual(0, d.highScore);
            Assert.AreEqual(-1f, d.bestClearTimeSec);
            Assert.AreEqual(0, d.essence);
            Assert.IsFalse(d.achFirstVictory);
        }

        [Test]
        public void MetaSchema_Validate_DefaultIsValid_AndFutureVersionIsCorruption()
        {
            Assert.AreEqual(SaveValidationResult.Valid, MetaSchema.Validate(SaveData.CreateDefault()));

            SaveData future = SaveData.CreateDefault();
            future.save_version = GameConfig.Save.CurrentVersion + 1;
            Assert.AreEqual(SaveValidationResult.FutureVersion, MetaSchema.Validate(future));

            SaveData missing = SaveData.CreateDefault();
            missing.save_version = 0;
            Assert.AreEqual(SaveValidationResult.VersionMissing, MetaSchema.Validate(missing));
        }

        [Test]
        public void MetaProgression_ApplyRunResult_AccumulatesStatsIndependentOfOutcome()
        {
            // P-04: 敗北でも統計・essence は確定加算される。
            SaveData prev = SaveData.CreateDefault();
            var loss = new RunResult
            {
                IsWin = false,
                CoreHpRemaining = 0,
                KillCount = 25,
                AoeKillCount = 5,
                UsedBuildSpots = 6,
                ClearTimeSec = 120f,
            };

            SaveData next = MetaProgression.ApplyRunResult(prev, loss);

            Assert.AreEqual(1, next.totalRunsPlayed);
            Assert.AreEqual(0, next.totalWins);
            Assert.AreEqual(25, next.totalKills);
            // essence = round((5 + floor(25/5) + 0) * (1 + 0)) = 10
            Assert.AreEqual(10, next.essence);
            Assert.IsFalse(next.achFirstVictory);       // 未勝利
            Assert.AreEqual(-1f, next.bestClearTimeSec); // 敗北はベストタイム対象外
            // 元データは不変（純粋 reducer）
            Assert.AreEqual(0, prev.totalRunsPlayed);
        }

        [Test]
        public void MetaProgression_ApplyRunResult_WinUnlocksAchievementsAndBestTime()
        {
            SaveData prev = SaveData.CreateDefault();
            var win = new RunResult
            {
                IsWin = true,
                CoreHpRemaining = GameConfig.Core.HpMax, // 完全防衛（ACH-02）
                KillCount = 40,
                AoeKillCount = 20,                        // ACH-04
                UsedBuildSpots = 4,                       // ACH-05
                ClearTimeSec = 200f,
            };

            SaveData next = MetaProgression.ApplyRunResult(prev, win);

            Assert.AreEqual(1, next.totalWins);
            Assert.IsTrue(next.achFirstVictory);
            Assert.IsTrue(next.achPerfectDefense);
            Assert.IsTrue(next.achAoeSpecialist);
            Assert.IsTrue(next.achFrugalArchitect);
            Assert.AreEqual(200f, next.bestClearTimeSec);
        }

        [Test]
        public void ScoreSystem_ComputeFinalScore_MatchesFormula()
        {
            var r = new RunResult
            {
                IsWin = true,
                CoreHpRemaining = 100,
                KillCount = 50,
                ClearTimeSec = 100f,
            };
            // core=(100/100)*500=500, kill=50*8=400, time=max(0,300-100)*2=400 => 1300
            Assert.AreEqual(1300, ScoreSystem.ComputeFinalScore(r));
        }

        // ───────────────── S-14: ラン間アップグレード効果反映と購入ロジック（UPG-01/02/03） ─────────────────

        [Test]
        public void MetaProgression_ComputeStartingGold_AppliesUpg01PerLevel()
        {
            Assert.AreEqual(GameConfig.Economy.StartingGold, MetaProgression.ComputeStartingGold(0));
            // acceptance: UPG-01 Lv2 購入後のラン初期資金が STARTING_GOLD+30（15/Lv×2）
            Assert.AreEqual(
                GameConfig.Economy.StartingGold + 2 * GameConfig.Meta.Upg01GoldPerLevel,
                MetaProgression.ComputeStartingGold(2));
            Assert.AreEqual(GameConfig.Economy.StartingGold + 30, MetaProgression.ComputeStartingGold(2));
        }

        [Test]
        public void MetaProgression_ComputeTowerDiscountRate_AppliesUpg02PerLevel()
        {
            Assert.AreEqual(0f, MetaProgression.ComputeTowerDiscountRate(0));
            Assert.AreEqual(
                3 * GameConfig.Meta.Upg02DiscountPerLevel,
                MetaProgression.ComputeTowerDiscountRate(3),
                1e-6f);
        }

        [Test]
        public void MetaProgression_TryPurchaseUpgrade_Succeeds_DeductsEssence_AndIncrementsLevel()
        {
            SaveData prev = SaveData.CreateDefault();
            prev.essence = GameConfig.Meta.UpgradePurchaseCostPerLevel * 3; // Lv1〜3まで購入できる資金

            PurchaseUpgradeResult r1 = MetaProgression.TryPurchaseUpgrade(prev, UpgradeKind.Upg01StartingGold);
            Assert.IsTrue(r1.Success);
            Assert.AreEqual(PurchaseFailureReason.None, r1.FailureReason);
            SaveData afterLv1 = r1.Data;
            Assert.AreEqual(1, afterLv1.upgStartingGoldLv);
            Assert.AreEqual(prev.essence - GameConfig.Meta.UpgradePurchaseCostPerLevel, afterLv1.essence);
            Assert.AreEqual(0, prev.upgStartingGoldLv, "元データは不変（純粋 reducer）");

            PurchaseUpgradeResult r2 = MetaProgression.TryPurchaseUpgrade(afterLv1, UpgradeKind.Upg01StartingGold);
            Assert.IsTrue(r2.Success);
            SaveData afterLv2 = r2.Data;
            Assert.AreEqual(2, afterLv2.upgStartingGoldLv);

            // acceptance: UPG-01 Lv2 購入後のラン初期資金が STARTING_GOLD+30
            Assert.AreEqual(
                GameConfig.Economy.StartingGold + 30,
                MetaProgression.ComputeStartingGold(afterLv2.upgStartingGoldLv));

            // CR-CODE S-14 iter2 minor指摘: acceptance「Lv 上限3まで購入できる」の Lv2→Lv3 成功境界が
            // 未検証だった（このテストが用意する essence は3回分だが購入を Lv2 で停止していた）。3回目の
            // 購入成功と Lv3 到達を検証する（この直後の AtLevelCap テストは Lv3 状態での「拒否」のみを
            // 検証するため、Lv2→Lv3 の「成功」遷移はこのテストでのみカバーされる）。
            PurchaseUpgradeResult r3 = MetaProgression.TryPurchaseUpgrade(afterLv2, UpgradeKind.Upg01StartingGold);
            Assert.IsTrue(r3.Success, "Lv3 到達までは購入できること（上限3）");
            SaveData afterLv3 = r3.Data;
            Assert.AreEqual(3, afterLv3.upgStartingGoldLv);
            Assert.AreEqual(afterLv2.essence - GameConfig.Meta.UpgradePurchaseCostPerLevel, afterLv3.essence);
        }

        [Test]
        public void MetaProgression_TryPurchaseUpgrade_AtLevelCap_Fails_AndDoesNotChangeEssence()
        {
            SaveData prev = SaveData.CreateDefault();
            prev.essence = 9999;
            prev.upgEssenceRateLv = GameConfig.Meta.UpgradeMaxLevel;

            PurchaseUpgradeResult result = MetaProgression.TryPurchaseUpgrade(prev, UpgradeKind.Upg03EssenceRate);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PurchaseFailureReason.AlreadyMaxLevel, result.FailureReason);
            Assert.AreEqual(GameConfig.Meta.UpgradeMaxLevel, result.Data.upgEssenceRateLv);
            Assert.AreEqual(9999, result.Data.essence);
        }

        [Test]
        public void MetaProgression_TryPurchaseUpgrade_InsufficientEssence_Fails_AndDoesNotChangeLevel()
        {
            SaveData prev = SaveData.CreateDefault();
            prev.essence = GameConfig.Meta.UpgradePurchaseCostPerLevel - 1;
            int essenceBefore = prev.essence; // CR-CODE S-14 iter1 minor指摘: next==prev(同一参照)のため
            // prev.essence を直接比較先に使うと実装側の破壊的変更を検出できない（同語反復）。呼び出し前に
            // 値をコピーしてから比較する（PlayMode 側の essenceBeforePurchases と同じパターン）。

            PurchaseUpgradeResult result = MetaProgression.TryPurchaseUpgrade(prev, UpgradeKind.Upg02TowerDiscount);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PurchaseFailureReason.InsufficientEssence, result.FailureReason);
            Assert.AreEqual(0, result.Data.upgTowerDiscountLv);
            Assert.AreEqual(essenceBefore, result.Data.essence);
        }
    }
}
