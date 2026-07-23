// FeedbackCueSystemTests.cs — S-13 acceptance の純粋ロジック部分（Unity 起動なしで検証）。
// 1) FeedbackCueSystem: 発射/撃破/コア被弾/ウェーブ開始/勝敗確定の各イベントに対応するキュー選択。
// 2) final-hit 方式での撃破帰属（Arc Emitter の最終ダメージのみ AoE撃破数へ計上され、Bastion Cannon
//    帰属は計上されない）を、TowerCombatSystem/WaveSpawnSystem/EnemyHealthSystem の実際のデータフロー
//    （Components/BuildSpotController.StepSimulation と同じ「ApplyDamage→Defeatedのみ RecordKill」の順序）
//    を模して統合的に検証する（conventions.md「撃破帰属ルールの実装契約」）。
// 3) WaveSpawnSystem の新設ウェーブ開始通知（SFX-05 トリガー）。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ForgeGame;
using ForgeGame.Systems;

namespace ForgeGame.Tests.EditMode
{
    public class FeedbackCueSystemTests
    {
        [Test]
        public void SelectTowerFiredCue_BastionCannon_UsesSharedFireSfx_WithConfiguredPitch()
        {
            FeedbackCue cue = FeedbackCueSystem.SelectTowerFiredCue(TowerType.BastionCannon);
            Assert.AreEqual(GameConfig.AssetKeys.SfxTowerFire, cue.AssetKey);
            Assert.AreEqual(GameConfig.Presentation.BastionCannonFirePitch, cue.PitchMultiplier);
        }

        [Test]
        public void SelectTowerFiredCue_ArcEmitter_UsesSharedFireSfx_WithDistinctPitch()
        {
            FeedbackCue cue = FeedbackCueSystem.SelectTowerFiredCue(TowerType.ArcEmitter);
            Assert.AreEqual(GameConfig.AssetKeys.SfxTowerFire, cue.AssetKey);
            Assert.AreEqual(GameConfig.Presentation.ArcEmitterFirePitch, cue.PitchMultiplier);
            Assert.AreNotEqual(
                GameConfig.Presentation.BastionCannonFirePitch, cue.PitchMultiplier,
                "SFX-02 は共通音のためピッチで2種タワーの役割差を演出する（P-02）");
        }

        [Test]
        public void SelectEnemyDefeatedCue_UsesEnemyDefeatSfx()
        {
            Assert.AreEqual(GameConfig.AssetKeys.SfxEnemyDefeat, FeedbackCueSystem.SelectEnemyDefeatedCue().AssetKey);
        }

        [Test]
        public void SelectCoreHitCue_UsesCoreHitSfx()
        {
            Assert.AreEqual(GameConfig.AssetKeys.SfxCoreHit, FeedbackCueSystem.SelectCoreHitCue().AssetKey);
        }

        [Test]
        public void SelectWaveStartCue_UsesWaveStartSfx()
        {
            Assert.AreEqual(GameConfig.AssetKeys.SfxWaveStart, FeedbackCueSystem.SelectWaveStartCue().AssetKey);
        }

        [Test]
        public void SelectRunOutcomeCue_Win_UsesVictoryJingle()
        {
            Assert.AreEqual(GameConfig.AssetKeys.SfxVictoryJingle, FeedbackCueSystem.SelectRunOutcomeCue(true).AssetKey);
        }

        [Test]
        public void SelectRunOutcomeCue_Loss_ReusesCoreHitSfx_AsCollapseSound()
        {
            // design/assets.md SFX-04: 予算制約により敗北成立時のコア崩壊SFXもコア被弾音を流用する。
            Assert.AreEqual(GameConfig.AssetKeys.SfxCoreHit, FeedbackCueSystem.SelectRunOutcomeCue(false).AssetKey);
        }

        [Test]
        public void FeedbackCue_EmptyOrNullAssetKey_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new FeedbackCue(""));
            Assert.Throws<System.ArgumentException>(() => new FeedbackCue(null));
        }

        [Test]
        public void FeedbackCue_NonPositivePitch_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FeedbackCue("key", 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FeedbackCue("key", -1f));
        }
    }

    /// <summary>
    /// gdd「撃破の帰属ルール」節（final-hit 方式）を、Components/BuildSpotController.StepSimulation と
    /// 同じ処理順（ダメージ適用イベントを順に ApplyDamage し、Defeated のイベントのみ RecordKill する）で
    /// 検証する（S-13 acceptance の中心）。
    /// </summary>
    public class FinalHitAttributionIntegrationTests
    {
        private static int SpawnFirstEnemyAndGetId(WaveSpawnSystem waveSystem)
        {
            var goalEvents = new List<EnemyGoalReachedEvent>();
            while (waveSystem.Enemies.Count == 0)
            {
                waveSystem.Tick(0.1f, goalEvents);
                goalEvents.Clear();
            }
            return waveSystem.Enemies[0].Id;
        }

        /// <summary>BuildSpotController.StepSimulation と同じ順序で damageEvents を適用し、撃破確定分のみ RecordKill する。</summary>
        private static void ApplyDamageEventsAndRecordKills(
            WaveSpawnSystem waveSystem, EnemyHealthSystem healthSystem, List<DamageEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                DamageEvent evt = events[i];
                EnemyDamageResult result = waveSystem.ApplyDamage(evt.EnemyId, evt.Damage);
                if (result.Found && result.Defeated)
                {
                    healthSystem.RecordKill(evt.SourceTowerType);
                }
            }
        }

        [Test]
        public void MixedSourceDamageEvents_ArcEmitterDeliversFinalHit_CountsAsAoeKill()
        {
            var waveSystem = new WaveSpawnSystem();
            int enemyId = SpawnFirstEnemyAndGetId(waveSystem);
            int hp = waveSystem.Enemies[0].Hp;
            var healthSystem = new EnemyHealthSystem();

            // Bastion Cannon が非致死ダメージ、続けて Arc Emitter が final-hit（gdd 撃破の帰属ルール）。
            var events = new List<DamageEvent>
            {
                new DamageEvent(enemyId, hp - 1, TowerType.BastionCannon),
                new DamageEvent(enemyId, 1, TowerType.ArcEmitter),
            };

            ApplyDamageEventsAndRecordKills(waveSystem, healthSystem, events);

            Assert.AreEqual(1, healthSystem.KillCount);
            Assert.AreEqual(1, healthSystem.AoeKillCount, "final-hit(Arc Emitter)のみがAoE撃破数に計上される");
        }

        [Test]
        public void MixedSourceDamageEvents_BastionCannonDeliversFinalHit_DoesNotCountAsAoeKill()
        {
            var waveSystem = new WaveSpawnSystem();
            int enemyId = SpawnFirstEnemyAndGetId(waveSystem);
            int hp = waveSystem.Enemies[0].Hp;
            var healthSystem = new EnemyHealthSystem();

            // Arc Emitter が非致死ダメージ、続けて Bastion Cannon が final-hit。
            var events = new List<DamageEvent>
            {
                new DamageEvent(enemyId, hp - 1, TowerType.ArcEmitter),
                new DamageEvent(enemyId, 1, TowerType.BastionCannon),
            };

            ApplyDamageEventsAndRecordKills(waveSystem, healthSystem, events);

            Assert.AreEqual(1, healthSystem.KillCount);
            Assert.AreEqual(0, healthSystem.AoeKillCount, "final-hit(Bastion Cannon)のためAoE撃破数に計上されない");
        }

        [Test]
        public void MixedSourceDamageEvents_SameFrameSecondHitAfterDefeat_DoesNotDoubleCount()
        {
            // 同フレーム内で最初に評価されたイベントが既に撃破に至った場合、後続イベントは
            // WaveSpawnSystem.ApplyDamage の Active ガードにより Found=false となり RecordKill は呼ばれない
            // （conventions.md「同フレーム複数ヒットの決定は登録順」＋2重計上防止）。
            var waveSystem = new WaveSpawnSystem();
            int enemyId = SpawnFirstEnemyAndGetId(waveSystem);
            int hp = waveSystem.Enemies[0].Hp;
            var healthSystem = new EnemyHealthSystem();

            var events = new List<DamageEvent>
            {
                new DamageEvent(enemyId, hp, TowerType.ArcEmitter),     // これで撃破確定（final-hit=ArcEmitter）
                new DamageEvent(enemyId, hp, TowerType.BastionCannon),  // 同フレーム2発目は無効化される
            };

            ApplyDamageEventsAndRecordKills(waveSystem, healthSystem, events);

            Assert.AreEqual(1, healthSystem.KillCount, "2重計上されない");
            Assert.AreEqual(1, healthSystem.AoeKillCount, "最初に評価されたArcEmitterのみが帰属先");
        }

        [Test]
        public void MultipleEnemies_EachAttributedIndependently_TotalsAccumulateCorrectly()
        {
            var waveSystem = new WaveSpawnSystem();
            var goalEvents = new List<EnemyGoalReachedEvent>();
            while (waveSystem.Enemies.Count < 2)
            {
                waveSystem.Tick(0.1f, goalEvents);
                goalEvents.Clear();
            }
            int enemyA = waveSystem.Enemies[0].Id;
            int enemyB = waveSystem.Enemies[1].Id;
            int hp = waveSystem.Enemies[0].Hp;
            var healthSystem = new EnemyHealthSystem();

            // enemyA は Arc Emitter の final-hit、enemyB は Bastion Cannon 単独撃破。
            var events = new List<DamageEvent>
            {
                new DamageEvent(enemyA, hp, TowerType.ArcEmitter),
                new DamageEvent(enemyB, hp, TowerType.BastionCannon),
            };

            ApplyDamageEventsAndRecordKills(waveSystem, healthSystem, events);

            Assert.AreEqual(2, healthSystem.KillCount);
            Assert.AreEqual(1, healthSystem.AoeKillCount, "Arc Emitter帰属の1体のみAoE撃破数に計上される");
        }
    }

    public class WaveSpawnSystemWaveStartEventTests
    {
        [Test]
        public void Tick_FirstCall_EmitsWaveStartEvent_ForWaveOne()
        {
            var system = new WaveSpawnSystem();
            var goalEvents = new List<EnemyGoalReachedEvent>();
            var waveStartEvents = new List<int>();

            system.Tick(0.1f, goalEvents, waveStartEvents);

            Assert.AreEqual(1, waveStartEvents.Count);
            Assert.AreEqual(1, waveStartEvents[0], "WAVE1の準備フェーズ開始を1-baseで通知する");
        }

        [Test]
        public void Tick_WithinSamePrepPhase_DoesNotReemitWaveStartEvent()
        {
            var system = new WaveSpawnSystem();
            var goalEvents = new List<EnemyGoalReachedEvent>();
            var waveStartEvents = new List<int>();

            system.Tick(0.1f, goalEvents, waveStartEvents);
            waveStartEvents.Clear();
            system.Tick(0.1f, goalEvents, waveStartEvents);

            Assert.AreEqual(0, waveStartEvents.Count, "同一ウェーブの準備フェーズ中は再通知しない");
        }

        [Test]
        public void Tick_WaveTransition_EmitsWaveStartEvent_ForNextWave()
        {
            var system = new WaveSpawnSystem();
            var goalEvents = new List<EnemyGoalReachedEvent>();
            var collectedWaveStarts = new List<int>();
            var stepBuffer = new List<int>();

            // WAVE1の全スポーンを消化しきるまで進める（準備フェーズ + 出現間隔×総スポーン数 + 余裕バッファ）。
            // SpawnIntervalMultiplier（WAVE1固有の出現間隔係数）を掛けないと GameConfig のチューニングで
            // WAVE1 の係数が変わった際にこの見積もりが崩れる（S-13 CR-CODE iter1 #3）。
            // WaveSpawnSystem.AdvanceSpawning は Marauder+Warbeast を同一 intervalSec で順次消化するため、
            // 乗数は総スポーン数（MarauderCount + WarbeastCount）でなければ WarbeastCount>=1 のウェーブ構成で
            // 見積もり不足になる（S-13 CR-CODE iter2 #1）。
            int totalSpawnCount =
                GameConfig.WaveComposition.Waves[0].MarauderCount + GameConfig.WaveComposition.Waves[0].WarbeastCount;
            float totalTime = GameConfig.Wave.WavePrepSec
                + GameConfig.Wave.SpawnIntervalBase * GameConfig.WaveComposition.Waves[0].SpawnIntervalMultiplier
                    * totalSpawnCount
                + 0.5f;
            float remaining = totalTime;
            const float stepSeconds = 0.1f;
            while (remaining > 0f)
            {
                float dt = Mathf.Min(stepSeconds, remaining);
                stepBuffer.Clear();
                goalEvents.Clear();
                system.Tick(dt, goalEvents, stepBuffer);
                collectedWaveStarts.AddRange(stepBuffer);
                remaining -= dt;
            }

            CollectionAssert.Contains(collectedWaveStarts, 1, "WAVE1開始が通知される");
            CollectionAssert.Contains(collectedWaveStarts, 2, "WAVE1消化後、WAVE2の準備フェーズ開始が通知される");
        }

        [Test]
        public void Tick_NullWaveStartEventsOut_DoesNotThrow()
        {
            var system = new WaveSpawnSystem();
            var goalEvents = new List<EnemyGoalReachedEvent>();
            Assert.DoesNotThrow(() => system.Tick(0.1f, goalEvents));
        }
    }
}
