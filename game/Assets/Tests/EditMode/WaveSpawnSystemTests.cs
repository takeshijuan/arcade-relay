// WaveSpawnSystemTests — S-05: derivation values must match design/gdd.md「ウェーブ・難度カーブ」表.
// Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class WaveSpawnSystemTests
    {
        // --- Wave number from elapsed time (gdd: 1 + floor(elapsedSec / WAVE_DURATION), WAVE_DURATION=30s) ---

        [Test]
        public void CurrentWave_AtZeroElapsed_IsWaveOne()
        {
            Assert.AreEqual(1, WaveSpawnSystem.CurrentWave(0f));
        }

        [Test]
        public void CurrentWave_At150Seconds_IsWaveFive()
        {
            // Wave 5 spans 120-150s per gdd table.
            Assert.AreEqual(5, WaveSpawnSystem.CurrentWave(120f));
            Assert.AreEqual(5, WaveSpawnSystem.CurrentWave(149.9f));
            Assert.AreEqual(6, WaveSpawnSystem.CurrentWave(150f));
        }

        // --- gdd 数値表「ウェーブ・難度カーブ」table row: Wave 1 (0-30s) ---
        // スポーン間隔 / 同時スポーン数 / 敵速度 / 敵HP: 1.5s / 1体 / 2.5 m/s / 40（基準）/ 未出現

        [Test]
        public void Wave1_MatchesGddTable()
        {
            WaveSpawnSystem.WaveParameters p = WaveSpawnSystem.Compute(0f);

            Assert.AreEqual(1, p.Wave);
            Assert.AreEqual(1.5f, p.SpawnInterval, 1e-3f);
            Assert.AreEqual(1, p.SpawnCount);
            Assert.AreEqual(2.5f, p.EnemySpeed, 1e-3f);
            Assert.AreEqual(40, p.EnemyHp);
            Assert.AreEqual(1f, p.EnemyHpMultiplier, 1e-3f, "HP成長はWave4から — Wave1はベースライン(倍率1)");
        }

        // --- gdd table row: Wave 5 (120-150s) ---
        // スポーン間隔 / 同時スポーン数 / 敵速度 / 敵HP: 1.18s / 2体 / 2.92 m/s / 約45 / 混入率15%

        [Test]
        public void Wave5_MatchesGddTable()
        {
            WaveSpawnSystem.WaveParameters p = WaveSpawnSystem.Compute(130f); // within Wave5's 120-150s window

            Assert.AreEqual(5, p.Wave);
            Assert.AreEqual(1.18f, p.SpawnInterval, 1e-2f);
            Assert.AreEqual(2, p.SpawnCount);
            Assert.AreEqual(2.92f, p.EnemySpeed, 1e-2f);
            Assert.AreEqual(45, p.EnemyHp);
        }

        // --- Additional table rows (Wave 3/8/12) sanity-checked for the same formulas ---

        [Test]
        public void Wave3_MatchesGddTable()
        {
            WaveSpawnSystem.WaveParameters p = WaveSpawnSystem.Compute(65f); // 60-90s window

            Assert.AreEqual(3, p.Wave);
            Assert.AreEqual(1.34f, p.SpawnInterval, 1e-2f);
            Assert.AreEqual(1, p.SpawnCount);
            Assert.AreEqual(2.70f, p.EnemySpeed, 1e-2f);
            Assert.AreEqual(40, p.EnemyHp, "HP成長はWave4から — Wave3はまだベースライン");
        }

        [Test]
        public void Wave8_MatchesGddTable()
        {
            WaveSpawnSystem.WaveParameters p = WaveSpawnSystem.Compute(215f); // 210-240s window

            Assert.AreEqual(8, p.Wave);
            Assert.AreEqual(0.94f, p.SpawnInterval, 1e-2f);
            Assert.AreEqual(3, p.SpawnCount);
            Assert.AreEqual(3.29f, p.EnemySpeed, 1e-2f);
            Assert.AreEqual(54, p.EnemyHp);
        }

        [Test]
        public void Wave12_MatchesGddTable()
        {
            WaveSpawnSystem.WaveParameters p = WaveSpawnSystem.Compute(335f); // 330-360s window

            Assert.AreEqual(12, p.Wave);
            Assert.AreEqual(0.62f, p.SpawnInterval, 1e-2f);
            Assert.AreEqual(4, p.SpawnCount);
            Assert.AreEqual(3.85f, p.EnemySpeed, 1e-2f);
            Assert.AreEqual(68, p.EnemyHp);
        }

        // --- Wave16+: spawn interval floors at SPAWN_INTERVAL_MIN (gdd) ---

        [Test]
        public void Wave16AndBeyond_SpawnIntervalFloorsAtMinimum()
        {
            float interval16 = WaveSpawnSystem.SpawnInterval(16);
            float interval25 = WaveSpawnSystem.SpawnInterval(25);

            Assert.AreEqual(GameConfig.Wave.SpawnIntervalMin, interval16, 1e-3f);
            Assert.AreEqual(GameConfig.Wave.SpawnIntervalMin, interval25, 1e-3f, "must not keep decreasing past the min clamp");
        }

        // --- Spawn point placement (pure trig, no RNG in Systems layer) ---

        [Test]
        public void SpawnPointOnRadius_IsAtExactlyRadiusDistanceFromOrigin_OnTheXzPlane()
        {
            Vector3 point = WaveSpawnSystem.SpawnPointOnRadius(Mathf.PI / 3f, GameConfig.Enemy.SpawnRadius);

            Assert.AreEqual(0f, point.y, 1e-6f);
            float flatDistance = new Vector2(point.x, point.z).magnitude;
            Assert.AreEqual(GameConfig.Enemy.SpawnRadius, flatDistance, 1e-3f);
        }
    }
}
