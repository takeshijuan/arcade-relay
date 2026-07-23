// BuildSpotCombatSystemTests.cs — S-05 acceptance の純粋ロジック部分（Unity 起動なしで検証）。
// EconomySystem（残高・実効コスト計算）、BuildSpotSystem（空き判定・設置）、
// TowerCombatSystem（発射間隔・ターゲティング・ダメージ適用イベント）、
// EnemyHealthSystem（撃破判定・報酬・総撃破数/AoE撃破数の分離集計）を検証する。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ForgeGame;
using ForgeGame.Systems;

namespace ForgeGame.Tests.EditMode
{
    public class EconomySystemTests
    {
        [Test]
        public void Constructor_NegativeStartingGold_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new EconomySystem(-1));
        }

        [Test]
        public void TrySpend_SufficientGold_DeductsAndReturnsTrue()
        {
            var economy = new EconomySystem(100);
            bool ok = economy.TrySpend(40);
            Assert.IsTrue(ok);
            Assert.AreEqual(60, economy.Gold);
        }

        [Test]
        public void TrySpend_InsufficientGold_ReturnsFalse_AndDoesNotChangeGold()
        {
            var economy = new EconomySystem(30);
            bool ok = economy.TrySpend(40);
            Assert.IsFalse(ok);
            Assert.AreEqual(30, economy.Gold);
        }

        [Test]
        public void Add_IncreasesGold()
        {
            var economy = new EconomySystem(10);
            economy.Add(5);
            Assert.AreEqual(15, economy.Gold);
        }

        [Test]
        public void Add_NegativeAmount_Throws()
        {
            var economy = new EconomySystem(10);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.Add(-1));
        }

        [Test]
        public void ComputeEffectiveCost_ZeroDiscount_ReturnsBaseCost()
        {
            Assert.AreEqual(50, EconomySystem.ComputeEffectiveCost(50, 0f));
        }

        [Test]
        public void ComputeEffectiveCost_WithDiscount_RoundsToNearestInt()
        {
            // 50 * (1 - 0.03) = 48.5 → round-to-even/nearest = 48 or 49 のどちらでも「四捨五入された整数」であることを確認
            int result = EconomySystem.ComputeEffectiveCost(50, 0.03f);
            Assert.AreEqual(Mathf.RoundToInt(48.5f), result);
        }

        // S-11 acceptance: 「投入額 N のタワーを売却すると資金が round(N×0.5) 増える」の丸め方式を検証する。
        [Test]
        public void ComputeSellRefund_ZeroInvested_ReturnsZero()
        {
            Assert.AreEqual(0, EconomySystem.ComputeSellRefund(0, GameConfig.Build.SellRefundRate));
        }

        [Test]
        public void ComputeSellRefund_RoundsToNearestInt_UsingConfiguredRefundRate()
        {
            int expected = Mathf.RoundToInt(90 * GameConfig.Build.SellRefundRate);
            Assert.AreEqual(expected, EconomySystem.ComputeSellRefund(90, GameConfig.Build.SellRefundRate));
        }

        [Test]
        public void ComputeSellRefund_NegativeInvestedGold_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EconomySystem.ComputeSellRefund(-1, 0.5f));
        }
    }

    public class BuildSpotSystemTests
    {
        [Test]
        public void TryPlace_EmptySpot_SufficientGold_Succeeds_DeductsGold_MarksOccupied()
        {
            var system = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);

            PlacementResult result = system.TryPlace(0, TowerType.BastionCannon, economy);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PlacementFailureReason.None, result.FailureReason);
            Assert.AreEqual(GameConfig.BastionCannon.Cost, result.EffectiveCost);
            Assert.AreEqual(0, economy.Gold);
            Assert.IsTrue(system.IsOccupied(0));
            Assert.AreEqual(1, system.Towers.Count);
            Assert.AreEqual(TowerType.BastionCannon, system.Towers[0].Type);
            Assert.AreEqual(GameConfig.Build.SpotPositions[0], system.Towers[0].Position);
        }

        [Test]
        public void TryPlace_InsufficientGold_Fails_NoGoldChange_NoOccupy()
        {
            var system = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost - 1);

            PlacementResult result = system.TryPlace(0, TowerType.BastionCannon, economy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlacementFailureReason.InsufficientGold, result.FailureReason);
            Assert.AreEqual(GameConfig.BastionCannon.Cost - 1, economy.Gold);
            Assert.IsFalse(system.IsOccupied(0));
            Assert.AreEqual(0, system.Towers.Count);
        }

        [Test]
        public void TryPlace_OccupiedSpot_Fails()
        {
            var system = new BuildSpotSystem();
            var economy = new EconomySystem(1000);
            system.TryPlace(0, TowerType.ArcEmitter, economy);

            PlacementResult result = system.TryPlace(0, TowerType.BastionCannon, economy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlacementFailureReason.SpotOccupied, result.FailureReason);
            Assert.AreEqual(1, system.Towers.Count);
        }

        [Test]
        public void TryPlace_InvalidSpotIndex_Fails()
        {
            var system = new BuildSpotSystem();
            var economy = new EconomySystem(1000);

            PlacementResult below = system.TryPlace(-1, TowerType.BastionCannon, economy);
            PlacementResult above = system.TryPlace(GameConfig.Build.NumBuildSpots, TowerType.BastionCannon, economy);

            Assert.IsFalse(below.Success);
            Assert.AreEqual(PlacementFailureReason.InvalidSpot, below.FailureReason);
            Assert.IsFalse(above.Success);
            Assert.AreEqual(PlacementFailureReason.InvalidSpot, above.FailureReason);
            Assert.AreEqual(1000, economy.Gold);
        }

        [Test]
        public void TryPlace_NullEconomy_Throws()
        {
            var system = new BuildSpotSystem();
            Assert.Throws<System.ArgumentNullException>(() => system.TryPlace(0, TowerType.BastionCannon, null));
        }

        [Test]
        public void SpotPositions_CountMatchesNumBuildSpots_AndAreNaNFree()
        {
            Assert.AreEqual(GameConfig.Build.NumBuildSpots, GameConfig.Build.SpotPositions.Length);
            foreach (Vector3 p in GameConfig.Build.SpotPositions)
            {
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z));
            }
        }
    }

    // S-11 acceptance: 「売却操作で設置+強化投入額×TOWER_SELL_REFUND_RATE を資金へ返還しスポットを空きに戻す
    // （移設ではなく撤去）。投入額 N のタワーを売却すると資金が round(N×0.5) 増え、同スポットが再び設置可能に
    // なる」を BuildSpotSystem.TrySell（純粋ロジック部分）で検証する。
    public class TowerSellSystemTests
    {
        [Test]
        public void TrySell_PlacedTower_RefundsRoundedRate_AndFreesSpot()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Assert.IsTrue(placed.Success);
            Assert.AreEqual(0, economy.Gold, "設置で資金を使い切った状態を前提にする");

            TowerSellResult result = buildSpots.TrySell(placed.Tower.Id, economy);

            Assert.IsTrue(result.Success, $"売却に失敗: {result.FailureReason}");
            Assert.AreEqual(TowerSellFailureReason.None, result.FailureReason);
            int expectedRefund = Mathf.RoundToInt(GameConfig.BastionCannon.Cost * GameConfig.Build.SellRefundRate);
            Assert.AreEqual(expectedRefund, result.RefundAmount);
            Assert.AreEqual(expectedRefund, economy.Gold);
            Assert.IsFalse(buildSpots.IsOccupied(0), "売却後はスポットが空きに戻ること");
            Assert.AreEqual(0, buildSpots.Towers.Count, "移設ではなく撤去のため Towers から除去されること");
        }

        [Test]
        public void TrySell_AfterUpgrade_RefundIsBasedOnTotalInvestedGold_NotJustPlacementCost()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost + GameConfig.BastionCannon.UpgradeLv2Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            TowerUpgradeResult upgraded = TowerUpgradeSystem.TryUpgrade(buildSpots, placed.Tower.Id, economy);
            Assert.IsTrue(upgraded.Success);
            Assert.AreEqual(0, economy.Gold);

            int investedGold = GameConfig.BastionCannon.Cost + GameConfig.BastionCannon.UpgradeLv2Cost;
            TowerSellResult result = buildSpots.TrySell(placed.Tower.Id, economy);

            int expectedRefund = Mathf.RoundToInt(investedGold * GameConfig.Build.SellRefundRate);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(expectedRefund, result.RefundAmount);
            Assert.AreEqual(expectedRefund, economy.Gold);
        }

        [Test]
        public void TrySell_SpotIsPlaceableAgain_WithDifferentTowerType()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost + GameConfig.ArcEmitter.Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Assert.IsTrue(placed.Success);

            buildSpots.TrySell(placed.Tower.Id, economy);
            PlacementResult replaced = buildSpots.TryPlace(0, TowerType.ArcEmitter, economy);

            Assert.IsTrue(replaced.Success, $"売却後は同スポットへ再設置可能であること: {replaced.FailureReason}");
            Assert.AreEqual(1, buildSpots.Towers.Count);
            Assert.AreEqual(TowerType.ArcEmitter, buildSpots.Towers[0].Type);
        }

        [Test]
        public void TrySell_UnknownTowerId_ReturnsTowerNotFound_AndDoesNotChangeGoldOrOccupancy()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            int goldBefore = economy.Gold;

            TowerSellResult result = buildSpots.TrySell(999, economy);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TowerSellFailureReason.TowerNotFound, result.FailureReason);
            Assert.AreEqual(goldBefore, economy.Gold);
            Assert.IsTrue(buildSpots.IsOccupied(0), "不明IDでの売却は既存タワーへ影響しないこと");
            Assert.AreEqual(1, buildSpots.Towers.Count);
        }

        [Test]
        public void TrySell_NullEconomy_Throws()
        {
            var buildSpots = new BuildSpotSystem();
            Assert.Throws<System.ArgumentNullException>(() => buildSpots.TrySell(0, null));
        }
    }

    public class TowerCombatSystemTests
    {
        private static EnemyInstance MakeEnemy(int id, float distanceTraveledM, int hp, bool active = true, EnemyType type = EnemyType.Marauder) =>
            new EnemyInstance { Id = id, Type = type, DistanceTraveledM = distanceTraveledM, Active = active, Hp = hp };

        [Test]
        public void Tick_BastionCannon_NoEnemyInRange_NoEvents()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            buildSpots.TryPlace(0, TowerType.BastionCannon, economy); // spot0 位置は GameConfig.Build.SpotPositions[0]

            // 経路の反対側の端（射程外）に敵を1体置く
            var enemies = new List<EnemyInstance> { MakeEnemy(1, GameConfig.Wave.PathLengthM, GameConfig.Marauder.Hp) };
            var events = new List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, enemies, events);

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_BastionCannon_TargetsHighestProgressEnemyInRange_AfterFireInterval()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Vector3 spotPos = GameConfig.Build.SpotPositions[0];
            float spotDistanceM = spotPos.x - GameConfig.Path.StartPoint.x; // 経路上でスポートに最も近い進行度

            // 射程内に2体（進行度違い）、射程外に1体
            var enemies = new List<EnemyInstance>
            {
                MakeEnemy(1, Mathf.Max(0f, spotDistanceM - 1f), GameConfig.Marauder.Hp),
                MakeEnemy(2, Mathf.Min(GameConfig.Wave.PathLengthM, spotDistanceM + 1f), GameConfig.Marauder.Hp), // より進行度が高い
                MakeEnemy(3, GameConfig.Wave.PathLengthM, GameConfig.Marauder.Hp), // 射程外
            };
            var events = new List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, enemies, events);

            Assert.AreEqual(1, events.Count, "発射間隔到達時は1発のみ発射する");
            Assert.AreEqual(2, events[0].EnemyId, "射程内で最も進行度が高い敵を狙う");
            Assert.AreEqual(GameConfig.BastionCannon.DamageLv1, events[0].Damage);
            Assert.AreEqual(TowerType.BastionCannon, events[0].SourceTowerType);
        }

        [Test]
        public void Tick_BastionCannon_BeforeFireInterval_DoesNotFire()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Vector3 spotPos = GameConfig.Build.SpotPositions[0];
            float spotDistanceM = spotPos.x - GameConfig.Path.StartPoint.x;

            var enemies = new List<EnemyInstance> { MakeEnemy(1, spotDistanceM, GameConfig.Marauder.Hp) };
            var events = new List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval * 0.5f, buildSpots, enemies, events);

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_ArcEmitter_HitsAllEnemiesWithinRadius_WhenAnyEnemyWithinSearchRange()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.ArcEmitter.Cost);
            buildSpots.TryPlace(0, TowerType.ArcEmitter, economy);
            Vector3 spotPos = GameConfig.Build.SpotPositions[0];
            float spotDistanceM = spotPos.x - GameConfig.Path.StartPoint.x;

            var enemies = new List<EnemyInstance>
            {
                MakeEnemy(1, spotDistanceM, GameConfig.Marauder.Hp),                 // 半径内
                MakeEnemy(2, GameConfig.Wave.PathLengthM, GameConfig.Marauder.Hp),   // 索敵射程外
            };
            var events = new List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.ArcEmitter.TickInterval, buildSpots, enemies, events);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, events[0].EnemyId);
            Assert.AreEqual(GameConfig.ArcEmitter.DamageLv1, events[0].Damage);
            Assert.AreEqual(TowerType.ArcEmitter, events[0].SourceTowerType);
        }

        // S-22 回帰テスト: ARC_EMITTER_RADIUS_M が Build.SpotOffsetZ と同値(3f)だった当時は、
        // タワー中心から経路までの最短距離が常に SpotOffsetZ と一致するため
        // (x差)^2 <= RadiusM^2 - SpotOffsetZ^2 = 0 に縮退し、経路上のAoE命中区間の幅が数学的に0m
        // （円が経路に接するのみ）になっていた（P-02「範囲低火力」の役割が機能不全）。
        // RadiusM=4f への是正後は 2*sqrt(RadiusM^2-SpotOffsetZ^2) の正の幅が生まれ、索敵射程を
        // 通過する Marauder へ最低1回はダメージ適用イベントが発行されることを、経路移動を細かい
        // フレーム刻みでシミュレーションして検証する。
        [Test]
        public void Tick_ArcEmitter_MarauderTraversingSearchRange_DamagesAtLeastOnce_RegressionForZeroWidthCoverage()
        {
            // 現在の RadiusM が SpotOffsetZ と一致（＝命中区間幅が0）してしまっていないことを、
            // 実際のシミュレーションより先に数式でも確認しておく（旧バグの再発防止）。
            float coverageHalfWidthSq = (GameConfig.ArcEmitter.RadiusM * GameConfig.ArcEmitter.RadiusM)
                - (GameConfig.Build.SpotOffsetZ * GameConfig.Build.SpotOffsetZ);
            Assert.Greater(coverageHalfWidthSq, 0f, "ARC_EMITTER_RADIUS_M が SpotOffsetZ 以下では命中区間の幅が0以下になる（S-22 と同じバグ）");

            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.ArcEmitter.Cost);
            PlacementResult placed = buildSpots.TryPlace(0, TowerType.ArcEmitter, economy);
            Assert.IsTrue(placed.Success);
            Vector3 spotPos = GameConfig.Build.SpotPositions[0];
            float spotDistanceM = spotPos.x - GameConfig.Path.StartPoint.x;

            // 索敵射程(ARC_EMITTER_RANGE_M)の手前から出発し、通り過ぎるまで経路上を等速前進させる。
            float startDistanceM = Mathf.Max(0f, spotDistanceM - GameConfig.ArcEmitter.RangeM);
            float endDistanceM = Mathf.Min(GameConfig.Wave.PathLengthM, spotDistanceM + GameConfig.ArcEmitter.RangeM);
            var enemies = new List<EnemyInstance>
            {
                MakeEnemy(1, startDistanceM, GameConfig.Marauder.Hp),
            };
            var events = new List<DamageEvent>();

            const float frameDt = 0.05f; // 十分に細かいシミュレーション刻み（20Hz相当）
            float traveled = startDistanceM;
            int hitCount = 0;
            while (traveled < endDistanceM)
            {
                traveled = Mathf.Min(endDistanceM, traveled + GameConfig.Marauder.SpeedMps * frameDt);
                EnemyInstance e = enemies[0];
                e.DistanceTraveledM = traveled;
                enemies[0] = e;

                events.Clear();
                TowerCombatSystem.Tick(frameDt, buildSpots, enemies, events);
                hitCount += events.Count;
            }

            Assert.Greater(hitCount, 0, "Marauder が Arc Emitter の索敵射程を通過する間に最低1回はダメージ適用イベントが発行されること（現状ゼロ回であることの回帰確認）");
        }

        [Test]
        public void Tick_IgnoresInactiveEnemies()
        {
            var buildSpots = new BuildSpotSystem();
            var economy = new EconomySystem(GameConfig.BastionCannon.Cost);
            buildSpots.TryPlace(0, TowerType.BastionCannon, economy);
            Vector3 spotPos = GameConfig.Build.SpotPositions[0];
            float spotDistanceM = spotPos.x - GameConfig.Path.StartPoint.x;

            var enemies = new List<EnemyInstance> { MakeEnemy(1, spotDistanceM, GameConfig.Marauder.Hp, active: false) };
            var events = new List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildSpots, enemies, events);

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_NegativeDeltaTime_Throws()
        {
            var buildSpots = new BuildSpotSystem();
            var events = new List<DamageEvent>();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => TowerCombatSystem.Tick(-0.1f, buildSpots, new List<EnemyInstance>(), events));
        }

        [Test]
        public void Tick_NullArgs_Throw()
        {
            var buildSpots = new BuildSpotSystem();
            var enemies = new List<EnemyInstance>();
            var events = new List<DamageEvent>();
            Assert.Throws<System.ArgumentNullException>(() => TowerCombatSystem.Tick(0.1f, null, enemies, events));
            Assert.Throws<System.ArgumentNullException>(() => TowerCombatSystem.Tick(0.1f, buildSpots, null, events));
            Assert.Throws<System.ArgumentNullException>(() => TowerCombatSystem.Tick(0.1f, buildSpots, enemies, null));
        }
    }

    public class EnemyHealthSystemTests
    {
        [Test]
        public void ApplyDamage_NeverGoesBelowZero()
        {
            Assert.AreEqual(0, EnemyHealthSystem.ApplyDamage(10, 999));
        }

        [Test]
        public void ApplyDamage_SubtractsDamage()
        {
            Assert.AreEqual(5, EnemyHealthSystem.ApplyDamage(30, 25));
        }

        [Test]
        public void IsDefeated_TrueOnlyAtOrBelowZero()
        {
            Assert.IsFalse(EnemyHealthSystem.IsDefeated(1));
            Assert.IsTrue(EnemyHealthSystem.IsDefeated(0));
            Assert.IsTrue(EnemyHealthSystem.IsDefeated(-5));
        }

        [Test]
        public void GoldReward_MatchesConfigPerType()
        {
            Assert.AreEqual(GameConfig.Marauder.GoldReward, EnemyHealthSystem.GoldReward(EnemyType.Marauder));
            Assert.AreEqual(GameConfig.Warbeast.GoldReward, EnemyHealthSystem.GoldReward(EnemyType.Warbeast));
        }

        [Test]
        public void RecordKill_BastionCannon_IncrementsKillCountOnly()
        {
            var system = new EnemyHealthSystem();
            system.RecordKill(TowerType.BastionCannon);
            Assert.AreEqual(1, system.KillCount);
            Assert.AreEqual(0, system.AoeKillCount);
        }

        [Test]
        public void RecordKill_ArcEmitter_IncrementsBothKillCountAndAoeKillCount()
        {
            var system = new EnemyHealthSystem();
            system.RecordKill(TowerType.ArcEmitter);
            Assert.AreEqual(1, system.KillCount);
            Assert.AreEqual(1, system.AoeKillCount);
        }

        [Test]
        public void RecordKill_Accumulates_AcrossMultipleCalls()
        {
            var system = new EnemyHealthSystem();
            system.RecordKill(TowerType.BastionCannon);
            system.RecordKill(TowerType.ArcEmitter);
            system.RecordKill(TowerType.ArcEmitter);
            Assert.AreEqual(3, system.KillCount);
            Assert.AreEqual(2, system.AoeKillCount);
        }
    }

    public class WaveSpawnSystemDamageTests
    {
        [Test]
        public void ApplyDamage_LethalDamage_DeactivatesEnemy_AndReportsDefeated()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            // 準備フェーズ終了直後に1体目がスポーンするまで進める。
            while (system.Enemies.Count == 0)
            {
                system.Tick(0.1f, events);
                events.Clear();
            }
            int enemyId = system.Enemies[0].Id;

            EnemyDamageResult result = system.ApplyDamage(enemyId, GameConfig.Marauder.Hp);

            Assert.IsTrue(result.Found);
            Assert.IsTrue(result.Defeated);
            Assert.AreEqual(0, result.RemainingHp);
            Assert.IsFalse(system.Enemies[0].Active);
        }

        [Test]
        public void ApplyDamage_NonLethalDamage_KeepsEnemyActive()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            while (system.Enemies.Count == 0)
            {
                system.Tick(0.1f, events);
                events.Clear();
            }
            int enemyId = system.Enemies[0].Id;

            EnemyDamageResult result = system.ApplyDamage(enemyId, 1);

            Assert.IsTrue(result.Found);
            Assert.IsFalse(result.Defeated);
            Assert.AreEqual(GameConfig.Marauder.Hp - 1, result.RemainingHp);
            Assert.IsTrue(system.Enemies[0].Active);
        }

        [Test]
        public void ApplyDamage_SecondHitAfterDefeat_ReturnsNotFound_FinalHitOnlyCountsOnce()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            while (system.Enemies.Count == 0)
            {
                system.Tick(0.1f, events);
                events.Clear();
            }
            int enemyId = system.Enemies[0].Id;

            EnemyDamageResult first = system.ApplyDamage(enemyId, GameConfig.Marauder.Hp);
            EnemyDamageResult second = system.ApplyDamage(enemyId, 5); // 同フレーム内2発目相当

            Assert.IsTrue(first.Defeated);
            Assert.IsFalse(second.Found, "撃破済みの敵への追加ダメージ適用は Found=false（final-hit のみ帰属）");
        }

        [Test]
        public void ApplyDamage_UnknownEnemyId_ReturnsNotFound()
        {
            var system = new WaveSpawnSystem();
            EnemyDamageResult result = system.ApplyDamage(999999, 10);
            Assert.IsFalse(result.Found);
        }
    }
}
