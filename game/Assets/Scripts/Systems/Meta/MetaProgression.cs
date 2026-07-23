// MetaProgression.cs — RunResult を受けて新 SaveData を返す純粋 reducer（I/O なし・contract §11）。
// 保存・読込は Persistence 層が仲介する。この層は値を受けて値を返すのみ。
// 数値の正本は GameConfig.Meta / GameConfig.Score。gdd「メタ進行（アウトゲーム）」節に対応。
using System;
using ForgeGame.Systems;

namespace ForgeGame.Systems.Meta
{
    /// <summary>TryPurchaseUpgrade の失敗理由（BuildSpotSystem.PlacementFailureReason 等と同じ命名慣例）。</summary>
    public enum PurchaseFailureReason
    {
        None = 0,
        AlreadyMaxLevel,
        InsufficientEssence,
    }

    /// <summary>TryPurchaseUpgrade の結果（成功時は Data.essence/Lv が更新済み・失敗時は FailureReason を見る）。</summary>
    public readonly struct PurchaseUpgradeResult
    {
        public readonly bool Success;
        public readonly PurchaseFailureReason FailureReason;
        public readonly SaveData Data; // 成功時: 購入後の新 SaveData。失敗時: prev（変更なし）

        public PurchaseUpgradeResult(bool success, PurchaseFailureReason failureReason, SaveData data)
        {
            Success = success;
            FailureReason = failureReason;
            Data = data;
        }
    }

    public static class MetaProgression
    {
        /// <summary>
        /// ラン結果を既存セーブへ確定適用し、新しい SaveData を返す（元データは不変）。
        /// P-04: 勝敗に関わらず統計・essence・実績の進捗は確定で積み上がる。
        /// </summary>
        public static SaveData ApplyRunResult(SaveData prev, RunResult r)
        {
            SaveData next = prev.Clone();

            // 統計（gdd 統計節）
            next.totalRunsPlayed = prev.totalRunsPlayed + 1;
            if (r.IsWin) next.totalWins = prev.totalWins + 1;
            next.totalKills = prev.totalKills + r.KillCount;

            // ハイスコア（勝敗どちらの結果でも算出・比較）
            int finalScore = ScoreSystem.ComputeFinalScore(r);
            if (finalScore > prev.highScore) next.highScore = finalScore;

            // ベストクリアタイム（勝利ランのみ・最短更新。未記録は -1）
            if (r.IsWin && (prev.bestClearTimeSec < 0f || r.ClearTimeSec < prev.bestClearTimeSec))
                next.bestClearTimeSec = r.ClearTimeSec;

            // essence 加算（gdd 通貨節の算出式）
            next.essence = prev.essence + ComputeEssenceEarned(r, prev.upgEssenceRateLv);

            // 実績判定（gdd 実績節。解放済みは維持）
            next.achFirstVictory = prev.achFirstVictory || next.totalWins >= 1;                      // ACH-01
            next.achPerfectDefense = prev.achPerfectDefense
                || (r.IsWin && r.CoreHpRemaining >= GameConfig.Core.HpMax);                          // ACH-02
            next.achCenturySlayer = prev.achCenturySlayer
                || next.totalKills >= GameConfig.Meta.CenturySlayerKills;                            // ACH-03
            next.achAoeSpecialist = prev.achAoeSpecialist
                || r.AoeKillCount >= GameConfig.Meta.AoeSpecialistKills;                             // ACH-04
            next.achFrugalArchitect = prev.achFrugalArchitect
                || (r.IsWin && r.UsedBuildSpots <= GameConfig.Meta.FrugalMaxSpots);                  // ACH-05

            return next;
        }

        /// <summary>
        /// essenceEarned = round(
        ///   (ESSENCE_BASE_LOSS + floor(killCount / ESSENCE_KILL_DIVISOR) + (isWin ? ESSENCE_WIN_BONUS : 0))
        /// * (1 + upgEssenceRateLv * UPG03_ESSENCE_RATE_PER_LV) )
        /// </summary>
        public static int ComputeEssenceEarned(RunResult r, int upgEssenceRateLv)
        {
            int baseAmount = GameConfig.Meta.EssenceBaseLoss
                + (r.KillCount / GameConfig.Meta.EssenceKillDivisor)
                + (r.IsWin ? GameConfig.Meta.EssenceWinBonus : 0);
            float multiplier = 1f + upgEssenceRateLv * GameConfig.Meta.Upg03EssenceRatePerLevel;
            return (int)Math.Round(baseAmount * multiplier, MidpointRounding.AwayFromZero);
        }

        // ───────────────── S-14: ラン間アップグレード効果反映と購入ロジック（UPG-01/02/03） ─────────────────
        // UPG-03（essenceEarned への乗算）は上記 ComputeEssenceEarned が既に prev.upgEssenceRateLv を
        // 受け取り適用済み（S-06 実装分）。本節で追加するのは UPG-01（初期資金）・UPG-02（割引率）を
        // ラン開始条件へ変換する純粋関数と、essence を消費して Lv を進める購入 reducer（正本は本 story）。

        /// <summary>
        /// UPG-01 適用後のラン開始資金（gdd「ゲームフロー」節・docs/architecture.md データフロー節:
        /// 「EconomySystem 初期資金 = STARTING_GOLD + UPG-01 効果」）。呼び出し側（Components/BuildSpotController）
        /// が Awake 時に GameFlow.CurrentSaveData の upgStartingGoldLv を渡して EconomySystem を初期化する。
        /// </summary>
        public static int ComputeStartingGold(int upgStartingGoldLv) =>
            GameConfig.Economy.StartingGold + upgStartingGoldLv * GameConfig.Meta.Upg01GoldPerLevel;

        /// <summary>
        /// UPG-02 適用後のタワー設置/強化コスト割引率（EconomySystem.ComputeEffectiveCost の discountRate 引数へ
        /// 渡す値。gdd「ビルドスポット選択・設置」「タワーアップグレード」節の実効コスト式に一致）。
        /// </summary>
        public static float ComputeTowerDiscountRate(int upgTowerDiscountLv) =>
            upgTowerDiscountLv * GameConfig.Meta.Upg02DiscountPerLevel;

        /// <summary>
        /// UPG-xx を essence で購入する（Menu アウトゲーム表示からの確定操作の正本 reducer）。
        /// 購入コストは Lv によらず一律 UPG_PURCHASE_COST_PER_LV（gdd「持ち越しアップグレード」節）。
        /// Lv 上限（UpgradeMaxLevel=3）到達 or essence 不足のいずれかで失敗し、Data には prev をそのまま返す
        /// （元データは変更しない・純粋 reducer）。失敗理由は PurchaseUpgradeResult.FailureReason で区別できる
        /// （CR-CODE S-14 iter1 minor指摘: bool 単独では「Lv上限」と「essence不足」を呼び出し側が再導出する
        /// 必要があり、PlacementResult/TowerUpgradeResult/TowerSellResult の FailureReason enum 付き結果構造体の
        /// 既存慣例から逸脱していたため、同じ形へ揃えた）。
        /// </summary>
        public static PurchaseUpgradeResult TryPurchaseUpgrade(SaveData prev, UpgradeKind kind)
        {
            if (prev == null) throw new ArgumentNullException(nameof(prev));

            int currentLv = GetUpgradeLevel(prev, kind);
            int cost = GameConfig.Meta.UpgradePurchaseCostPerLevel;

            if (currentLv >= GameConfig.Meta.UpgradeMaxLevel)
                return new PurchaseUpgradeResult(false, PurchaseFailureReason.AlreadyMaxLevel, prev);
            if (prev.essence < cost)
                return new PurchaseUpgradeResult(false, PurchaseFailureReason.InsufficientEssence, prev);

            SaveData purchased = prev.Clone();
            purchased.essence = prev.essence - cost;
            SetUpgradeLevel(purchased, kind, currentLv + 1);
            return new PurchaseUpgradeResult(true, PurchaseFailureReason.None, purchased);
        }

        /// <summary>現在の UPG-xx Lv を SaveData から読む（TryPurchaseUpgrade / 呼び出し側の表示共用ヘルパ）。</summary>
        public static int GetUpgradeLevel(SaveData data, UpgradeKind kind)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            switch (kind)
            {
                case UpgradeKind.Upg01StartingGold: return data.upgStartingGoldLv;
                case UpgradeKind.Upg02TowerDiscount: return data.upgTowerDiscountLv;
                case UpgradeKind.Upg03EssenceRate: return data.upgEssenceRateLv;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知の UpgradeKind");
            }
        }

        private static void SetUpgradeLevel(SaveData data, UpgradeKind kind, int level)
        {
            switch (kind)
            {
                case UpgradeKind.Upg01StartingGold: data.upgStartingGoldLv = level; break;
                case UpgradeKind.Upg02TowerDiscount: data.upgTowerDiscountLv = level; break;
                case UpgradeKind.Upg03EssenceRate: data.upgEssenceRateLv = level; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知の UpgradeKind");
            }
        }
    }
}
