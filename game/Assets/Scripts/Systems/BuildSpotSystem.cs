// BuildSpotSystem.cs — ビルドスポット選択・設置・売却（純粋 C#・エンジン非依存。S-05 + S-11）。
// gdd「ビルドスポット選択・設置」「売却」節（P-01）。空き判定・実効コスト計算・資金比較・設置イベント発行、
// および設置済みタワーの売却（投入額×TOWER_SELL_REFUND_RATE の返還・スポット解放）を担う。
// 設置後は移設不可・売却のみ可（gdd 出力欄）。売却は撤去のみで再設置前提（移設ではない — gdd「売却」節）。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。Vector3 は値型として使用可。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Systems
{
    /// <summary>設置済みタワー1基の状態（純粋値）。</summary>
    public struct TowerInstance
    {
        public int Id;
        public TowerType Type;
        public int Level;            // Lv1〜3（GameConfig.Tower.MaxLevel）。S-05 時点では常に1（アップグレードは S-10）。
        public int SpotIndex;
        public Vector3 Position;
        public float CooldownTimer;  // TowerCombatSystem が delta-time で進める発射/tick間隔タイマー
        public int InvestedGold;     // 設置実効コスト+アップグレード実効コストの累計（S-11 売却返還額の算出基礎）
    }

    public enum PlacementFailureReason
    {
        None = 0,
        InvalidSpot,
        SpotOccupied,
        InsufficientGold,
    }

    /// <summary>TryPlace の結果（成功時は Tower/EffectiveCost、失敗時は FailureReason を見る）。</summary>
    public readonly struct PlacementResult
    {
        public readonly bool Success;
        public readonly PlacementFailureReason FailureReason;
        public readonly TowerInstance Tower;
        public readonly int EffectiveCost;

        public PlacementResult(bool success, PlacementFailureReason failureReason, TowerInstance tower, int effectiveCost)
        {
            Success = success;
            FailureReason = failureReason;
            Tower = tower;
            EffectiveCost = effectiveCost;
        }
    }

    public sealed class BuildSpotSystem
    {
        private readonly bool[] occupied;
        private readonly List<TowerInstance> towers = new List<TowerInstance>();
        private int nextTowerId;

        public BuildSpotSystem()
        {
            occupied = new bool[GameConfig.Build.NumBuildSpots];
        }

        public int SpotCount => occupied.Length;

        /// <summary>設置済みタワー一覧（読み取り専用）。TowerCombatSystem はこの実体を UpdateTower 経由で更新する。</summary>
        public IReadOnlyList<TowerInstance> Towers => towers;

        public bool IsOccupied(int spotIndex) => occupied[spotIndex];

        /// <summary>
        /// 空きスポットへタワーを設置する。実効コスト = 基礎コスト×(1-discountRate)（EconomySystem.ComputeEffectiveCost）。
        /// スポート不正・占有済み・資金不足のいずれかで設置不可（economy は変更されない）。
        /// </summary>
        public PlacementResult TryPlace(int spotIndex, TowerType type, EconomySystem economy, float discountRate = 0f)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (spotIndex < 0 || spotIndex >= occupied.Length)
                return new PlacementResult(false, PlacementFailureReason.InvalidSpot, default, 0);
            if (occupied[spotIndex])
                return new PlacementResult(false, PlacementFailureReason.SpotOccupied, default, 0);

            int baseCost = type == TowerType.BastionCannon ? GameConfig.BastionCannon.Cost : GameConfig.ArcEmitter.Cost;
            int effectiveCost = EconomySystem.ComputeEffectiveCost(baseCost, discountRate);

            if (!economy.TrySpend(effectiveCost))
                return new PlacementResult(false, PlacementFailureReason.InsufficientGold, default, effectiveCost);

            var tower = new TowerInstance
            {
                Id = nextTowerId++,
                Type = type,
                Level = 1,
                SpotIndex = spotIndex,
                Position = GameConfig.Build.SpotPositions[spotIndex],
                CooldownTimer = 0f,
                InvestedGold = effectiveCost,
            };
            towers.Add(tower);
            occupied[spotIndex] = true;
            return new PlacementResult(true, PlacementFailureReason.None, tower, effectiveCost);
        }

        /// <summary>
        /// TowerCombatSystem が発射/tickクールダウンタイマーを進めるための更新口。
        /// index は Towers（=towers）のインデックスに一致させること（TowerCombatSystem.Tick が保証）。
        /// </summary>
        public void UpdateTower(int index, TowerInstance updated) => towers[index] = updated;

        /// <summary>
        /// 設置済みタワーを売却する（S-11）。gdd「売却」節: 設置・強化投入額(TowerInstance.InvestedGold)の
        /// TOWER_SELL_REFUND_RATE ぶんを資金へ返還し、スポットを空きに戻す（移設ではなく撤去。再設置は
        /// 通常の TryPlace で別途行う想定 — 移設無償化で P-01「一手必中」を緩めない）。towerId 不明時は
        /// economy/occupied のいずれも変更しない。
        /// </summary>
        public TowerSellResult TrySell(int towerId, EconomySystem economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));

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
                return new TowerSellResult(false, TowerSellFailureReason.TowerNotFound, default, 0);

            TowerInstance tower = towers[index];
            int refund = EconomySystem.ComputeSellRefund(tower.InvestedGold, GameConfig.Build.SellRefundRate);

            towers.RemoveAt(index);
            occupied[tower.SpotIndex] = false;
            economy.Add(refund);

            return new TowerSellResult(true, TowerSellFailureReason.None, tower, refund);
        }
    }

    public enum TowerSellFailureReason
    {
        None = 0,
        TowerNotFound,
    }

    /// <summary>TrySell の結果（成功時は Tower に撤去直前のスナップショット・RefundAmount に返還額）。</summary>
    public readonly struct TowerSellResult
    {
        public readonly bool Success;
        public readonly TowerSellFailureReason FailureReason;
        public readonly TowerInstance Tower;
        public readonly int RefundAmount;

        public TowerSellResult(bool success, TowerSellFailureReason failureReason, TowerInstance tower, int refundAmount)
        {
            Success = success;
            FailureReason = failureReason;
            Tower = tower;
            RefundAmount = refundAmount;
        }
    }
}
