// WaveSpawnSystemTests.cs — S-04 acceptance の純粋ロジック部分（Unity 起動なしで検証）。
// WaveSpawnSystem のスポーン/経路移動/ゴール到達判定と CoreDefenseSystem のコアHP減算・敗北判定を検証する。
// 実運用（Update() 毎フレームの小刻みな delta-time）を模すため、テストも小刻みな Tick を積み重ねて
// シミュレートする（1回の巨大な deltaTime を与えると同一フレーム内でスポーン直後の敵が大きく移動してしまい、
// フレームレート非依存の検証として不正確になるため）。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ForgeGame;
using ForgeGame.Systems;

namespace ForgeGame.Tests.EditMode
{
    public class WaveSpawnSystemTests
    {
        private const float StepSeconds = 0.1f;

        /// <summary>小刻みな Tick を totalSeconds ぶん積み重ね、発生したゴール到達イベントを collected に集約する。</summary>
        private static void AdvanceTime(WaveSpawnSystem system, float totalSeconds, List<EnemyGoalReachedEvent> collected)
        {
            var stepEvents = new List<EnemyGoalReachedEvent>();
            float remaining = totalSeconds;
            while (remaining > 0f)
            {
                float dt = Mathf.Min(StepSeconds, remaining);
                stepEvents.Clear();
                system.Tick(dt, stepEvents);
                collected.AddRange(stepEvents);
                remaining -= dt;
            }
        }

        [Test]
        public void GetPathPosition_IsLinearAndNaNFree_AcrossFullRange()
        {
            for (float d = -5f; d <= GameConfig.Wave.PathLengthM + 5f; d += 1f)
            {
                Vector3 p = WaveSpawnSystem.GetPathPosition(d);
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), $"NaN at distance {d}");
            }

            Vector3 start = WaveSpawnSystem.GetPathPosition(0f);
            Vector3 end = WaveSpawnSystem.GetPathPosition(GameConfig.Wave.PathLengthM);
            Assert.AreEqual(GameConfig.Path.StartPoint, start);
            Assert.AreEqual(GameConfig.Path.EndPoint, end);

            // 距離が経路長を超えても終点にクランプされる（負値も始点にクランプ）
            Assert.AreEqual(GameConfig.Path.EndPoint, WaveSpawnSystem.GetPathPosition(GameConfig.Wave.PathLengthM + 100f));
            Assert.AreEqual(GameConfig.Path.StartPoint, WaveSpawnSystem.GetPathPosition(-100f));
        }

        [Test]
        public void Tick_SpawnsWave1MarauderCount_AfterPrepAndSpawnIntervals()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();

            // WAVE1 は Marauder x6（GameConfig.WaveComposition.Waves[0]）。準備フェーズ+出現間隔6回ぶんを消化。
            float totalTime = GameConfig.Wave.WavePrepSec + GameConfig.Wave.SpawnIntervalBase * 6f + StepSeconds;
            AdvanceTime(system, totalTime, events);

            Assert.AreEqual(6, system.Enemies.Count);
            foreach (EnemyInstance e in system.Enemies)
            {
                Assert.AreEqual(EnemyType.Marauder, e.Type);
            }
        }

        [Test]
        public void Tick_MovesEnemy_ByDeltaTimeAndSpeed_NotFrameRateDependent()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();

            // 準備フェーズ終了直後に1体目がスポーンするまで進める（ちょうど1体になった時点で止める）。
            while (system.Enemies.Count == 0)
            {
                system.Tick(StepSeconds, events);
                events.Clear();
            }
            Assert.AreEqual(1, system.Enemies.Count);

            float distanceBefore = system.Enemies[0].DistanceTraveledM;
            const float dt = 0.5f;
            system.Tick(dt, events);
            float distanceAfter = system.Enemies[0].DistanceTraveledM;

            Assert.AreEqual(GameConfig.Marauder.SpeedMps * dt, distanceAfter - distanceBefore, 0.0001f);
        }

        [Test]
        public void Tick_EnemyReachingGoal_FiresGoalEvent_AndBecomesInactive()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            var collected = new List<EnemyGoalReachedEvent>();

            // WAVE1 は SpawnIntervalBase 毎に敵が出続けるため、固定の余裕バッファ付き総時間まで進めると
            // 後続の敵も同じウィンドウ内でゴール到達してしまう（SpawnIntervalBase < バッファ）。
            // 「1体目がゴールへ到達した瞬間」だけを検証するため、最初のゴール到達イベントが発生した
            // Tick で停止する（StepSeconds はゴール到達間隔 SpawnIntervalBase より十分小さく、
            // 同一 Tick 内で2体以上が同時到達することはない）。
            while (collected.Count == 0)
            {
                events.Clear();
                system.Tick(StepSeconds, events);
                collected.AddRange(events);
            }

            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(EnemyType.Marauder, collected[0].Type);
            Assert.AreEqual(system.Enemies[0].Id, collected[0].EnemyId);
            Assert.IsFalse(system.Enemies[0].Active);
            Assert.AreEqual(GameConfig.Wave.PathLengthM, system.Enemies[0].DistanceTraveledM);
        }

        [Test]
        public void WaveComposition_MatchesGddDifficultyCurveTable_ForAllEightWaves()
        {
            // gdd「難易度曲線」表（design/gdd.md）の WAVE 1〜8 の敵構成・出現間隔倍率をそのまま数値表として検証する（S-12）。
            // WAVE 4 は表に出現間隔の明記が無いため WAVE 3（90%）継承と解釈した実装判断
            // （GameConfig.WaveComposition のコメント参照）をそのままここでも数値として固定する。
            (int marauder, int warbeast, float multiplier)[] expected =
            {
                (6, 0, 1.0f),    // WAVE 1
                (8, 0, 1.0f),    // WAVE 2
                (6, 2, 0.9f),    // WAVE 3
                (10, 2, 0.9f),   // WAVE 4
                (8, 4, 0.8f),    // WAVE 5
                (10, 5, 0.75f),  // WAVE 6
                (12, 6, 0.7f),   // WAVE 7
                (14, 8, 0.65f),  // WAVE 8
            };

            Assert.AreEqual(GameConfig.Wave.WaveCount, GameConfig.WaveComposition.Waves.Length);
            Assert.AreEqual(expected.Length, GameConfig.WaveComposition.Waves.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                GameConfig.WaveComposition.WaveDef wave = GameConfig.WaveComposition.Waves[i];
                Assert.AreEqual(expected[i].marauder, wave.MarauderCount, $"WAVE {i + 1} MarauderCount");
                Assert.AreEqual(expected[i].warbeast, wave.WarbeastCount, $"WAVE {i + 1} WarbeastCount");
                Assert.AreEqual(expected[i].multiplier, wave.SpawnIntervalMultiplier, 0.0001f, $"WAVE {i + 1} SpawnIntervalMultiplier");

                if (i < 2)
                {
                    Assert.AreEqual(0, wave.WarbeastCount, $"WAVE {i + 1} は Warbeast 混成開始(WAVE3)前のため Warbeast 数0");
                }
                else
                {
                    Assert.Greater(wave.WarbeastCount, 0, $"WAVE {i + 1} は WAVE3以降のため Warbeast が混成出現している必要がある");
                }
            }
        }

        [Test]
        public void Tick_SpawnsAllEightWaves_InOrder_WithCorrectCompositionAndDeterministicSpawnOrder()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();

            for (int waveIndex = 0; waveIndex < GameConfig.WaveComposition.Waves.Length; waveIndex++)
            {
                GameConfig.WaveComposition.WaveDef wave = GameConfig.WaveComposition.Waves[waveIndex];
                int enemiesBefore = system.Enemies.Count;
                int expectedTotalThisWave = wave.MarauderCount + wave.WarbeastCount;

                // このウェーブの全スポーンを消化しきるまで進める（準備フェーズ + 出現間隔 × 体数 + 余裕バッファ）。
                float intervalSec = GameConfig.Wave.SpawnIntervalBase * wave.SpawnIntervalMultiplier;
                float totalTime = GameConfig.Wave.WavePrepSec + intervalSec * expectedTotalThisWave + StepSeconds * 2f;
                AdvanceTime(system, totalTime, events);

                int spawnedThisWave = system.Enemies.Count - enemiesBefore;
                Assert.AreEqual(expectedTotalThisWave, spawnedThisWave, $"WAVE {waveIndex + 1} 総スポーン数");

                int marauderCount = 0;
                int warbeastCount = 0;
                for (int i = enemiesBefore; i < system.Enemies.Count; i++)
                {
                    if (system.Enemies[i].Type == EnemyType.Marauder) marauderCount++;
                    else warbeastCount++;
                }
                Assert.AreEqual(wave.MarauderCount, marauderCount, $"WAVE {waveIndex + 1} Marauder 数");
                Assert.AreEqual(wave.WarbeastCount, warbeastCount, $"WAVE {waveIndex + 1} Warbeast 数");

                // 決定論的な出現順（Marauder→Warbeast）: このウェーブ内で先に生成された分は全て Marauder。
                for (int i = enemiesBefore; i < enemiesBefore + wave.MarauderCount; i++)
                {
                    Assert.AreEqual(EnemyType.Marauder, system.Enemies[i].Type, $"WAVE {waveIndex + 1} index {i - enemiesBefore} は Marauder が先");
                }
                for (int i = enemiesBefore + wave.MarauderCount; i < system.Enemies.Count; i++)
                {
                    Assert.AreEqual(EnemyType.Warbeast, system.Enemies[i].Type, $"WAVE {waveIndex + 1} index {i - enemiesBefore} は Warbeast が後");
                }
            }

            Assert.IsTrue(system.AllWavesSpawned);
            Assert.AreEqual(GameConfig.WaveComposition.Waves.Length, system.CurrentWaveNumber);
        }

        [Test]
        public void Tick_SpawnIntervalsWithinEachWave_MatchConfiguredMultiplier()
        {
            // 全ウェーブを小刻み Tick で消化しながら、敵が1体増えた瞬間の累積時刻を記録する。
            // ウェーブ内で隣接する2体（1体目=準備フェーズ終了直後を除く）の時刻差が
            // SpawnIntervalBase × 当該ウェーブの SpawnIntervalMultiplier に一致することを検証する
            // （gdd「難易度曲線」表の出現間隔◯%列そのものの検証）。
            // 加えて、波間の最終スポーン→次波先頭スポーンの間隔が WAVE_PREP_SEC に一致することも検証する
            // （acceptance「ウェーブ間に WAVE_PREP_SEC の準備フェーズを挟む」の直接検証。CR-CODE S-12 iter1 対応）。
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            var spawnTimestamps = new List<float>();
            float elapsed = 0f;
            int previousCount = 0;

            // ハング防止の安全上限（CR-CODE S-12 iter1 対応）: 理論所要時間
            // Σ(WavePrepSec + intervalSec × 体数) を超えても AllWavesSpawned が立たない場合、
            // WaveSpawnSystem の波遷移条件に回帰があるとみなし、無限ループではなく明示的な Assert.Fail にする。
            float safetyLimitSec = 0f;
            for (int w = 0; w < GameConfig.WaveComposition.Waves.Length; w++)
            {
                GameConfig.WaveComposition.WaveDef wd = GameConfig.WaveComposition.Waves[w];
                float ivl = GameConfig.Wave.SpawnIntervalBase * wd.SpawnIntervalMultiplier;
                safetyLimitSec += GameConfig.Wave.WavePrepSec + ivl * (wd.MarauderCount + wd.WarbeastCount);
            }
            safetyLimitSec += StepSeconds * 10f; // 余裕バッファ

            while (!system.AllWavesSpawned)
            {
                Assert.Less(elapsed, safetyLimitSec,
                    "AllWavesSpawned が理論所要時間を超えても立たない（波遷移条件の回帰の疑い。ハング防止の安全上限）");
                events.Clear();
                system.Tick(StepSeconds, events);
                elapsed += StepSeconds;
                int currentCount = system.Enemies.Count;
                for (int i = previousCount; i < currentCount; i++)
                {
                    spawnTimestamps.Add(elapsed);
                }
                previousCount = currentCount;
            }

            int cursor = 0;
            for (int w = 0; w < GameConfig.WaveComposition.Waves.Length; w++)
            {
                GameConfig.WaveComposition.WaveDef wave = GameConfig.WaveComposition.Waves[w];
                int total = wave.MarauderCount + wave.WarbeastCount;
                float expectedInterval = GameConfig.Wave.SpawnIntervalBase * wave.SpawnIntervalMultiplier;

                if (w > 0)
                {
                    float waveGap = spawnTimestamps[cursor] - spawnTimestamps[cursor - 1];
                    Assert.AreEqual(GameConfig.Wave.WavePrepSec, waveGap, StepSeconds * 1.5f,
                        $"WAVE {w} → WAVE {w + 1} 間の準備フェーズ（WAVE_PREP_SEC）");
                }

                for (int i = 1; i < total; i++)
                {
                    float delta = spawnTimestamps[cursor + i] - spawnTimestamps[cursor + i - 1];
                    Assert.AreEqual(expectedInterval, delta, StepSeconds * 1.5f, $"WAVE {w + 1} 出現間隔（{i}番目）");
                }
                cursor += total;
            }

            Assert.AreEqual(cursor, spawnTimestamps.Count);
        }

        [Test]
        public void Tick_NegativeDeltaTime_Throws()
        {
            var system = new WaveSpawnSystem();
            var events = new List<EnemyGoalReachedEvent>();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => system.Tick(-0.1f, events));
        }

        [Test]
        public void Tick_NullEventList_Throws()
        {
            var system = new WaveSpawnSystem();
            Assert.Throws<System.ArgumentNullException>(() => system.Tick(0.1f, null));
        }
    }

    public class CoreDefenseSystemTests
    {
        [Test]
        public void ApplyDamage_Marauder_SubtractsMarauderCoreDamage()
        {
            int next = CoreDefenseSystem.ApplyDamage(GameConfig.Core.HpMax, EnemyType.Marauder);
            Assert.AreEqual(GameConfig.Core.HpMax - GameConfig.Marauder.CoreDamage, next);
        }

        [Test]
        public void ApplyDamage_Warbeast_SubtractsWarbeastCoreDamage()
        {
            int next = CoreDefenseSystem.ApplyDamage(GameConfig.Core.HpMax, EnemyType.Warbeast);
            Assert.AreEqual(GameConfig.Core.HpMax - GameConfig.Warbeast.CoreDamage, next);
        }

        [Test]
        public void ApplyDamage_NeverGoesBelowZero()
        {
            int next = CoreDefenseSystem.ApplyDamage(1, EnemyType.Warbeast); // damage(3) > hp(1)
            Assert.AreEqual(0, next);
        }

        [Test]
        public void IsDefeated_TrueOnlyAtOrBelowZero()
        {
            Assert.IsFalse(CoreDefenseSystem.IsDefeated(1));
            Assert.IsTrue(CoreDefenseSystem.IsDefeated(0));
            Assert.IsTrue(CoreDefenseSystem.IsDefeated(-5));
        }

        [Test]
        public void RepeatedMarauderHits_FromCoreHpMax_ReachExactlyZero()
        {
            int hp = GameConfig.Core.HpMax;
            int hits = 0;
            while (!CoreDefenseSystem.IsDefeated(hp))
            {
                hp = CoreDefenseSystem.ApplyDamage(hp, EnemyType.Marauder);
                hits++;
                Assert.Less(hits, GameConfig.Core.HpMax + 1, "無限ループ防止のガード");
            }
            Assert.AreEqual(0, hp);
        }
    }
}
