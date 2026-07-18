// WaveSpawnSystem — pure C# wave/difficulty-curve derivation (gdd 難易度曲線 + 数値表「ウェーブ・
// 難度カーブ」; P-03; S-05). Given elapsed run time, derives the current wave number and that wave's
// spawn interval / simultaneous spawn count / enemy speed / enemy HP multiplier. Engine-independent:
// no MonoBehaviour, no scene API, no RNG (rules/unity-code.md #3) — Components/WaveSpawner drives this
// every frame with Time.deltaTime and owns the spawn-timer/RNG-angle/Instantiate side effects.
//
// Formulas verified against design/gdd.md "ウェーブ・難度カーブ" table (Wave 1/3/5/8/12 rows):
//   SpawnInterval(1)=1.50s SpawnInterval(3)=1.34s SpawnInterval(5)=1.18s SpawnInterval(8)=0.94s SpawnInterval(12)=0.62s
//   SpawnCount(1)=1        SpawnCount(3)=1        SpawnCount(5)=2        SpawnCount(8)=3        SpawnCount(12)=4
//   EnemySpeed(1)=2.50     EnemySpeed(3)=2.70     EnemySpeed(5)=2.92     EnemySpeed(8)=3.29     EnemySpeed(12)=3.85
//   EnemyHp(1)=40 (baseline, no growth before wave 4) EnemyHp(5)~=45 EnemyHp(8)~=54 EnemyHp(12)~=68
using System;
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class WaveSpawnSystem
    {
        /// <summary>Snapshot of a wave's derived spawn/enemy tuning. EnemyHp is the rounded absolute
        /// HP (gdd table's "敵HP" column) for convenience; EnemyHpMultiplier is the raw multiplier a
        /// later story (heavy-variant stacking) may want directly.</summary>
        public readonly struct WaveParameters
        {
            public readonly int Wave;
            public readonly float SpawnInterval;
            public readonly int SpawnCount;
            public readonly float EnemySpeed;
            public readonly float EnemyHpMultiplier;
            public readonly int EnemyHp;

            public WaveParameters(
                int wave, float spawnInterval, int spawnCount, float enemySpeed, float enemyHpMultiplier, int enemyHp)
            {
                Wave = wave;
                SpawnInterval = spawnInterval;
                SpawnCount = spawnCount;
                EnemySpeed = enemySpeed;
                EnemyHpMultiplier = enemyHpMultiplier;
                EnemyHp = enemyHp;
            }
        }

        /// <summary>Current wave number from elapsed seconds (gdd セッション内進行の単位:
        /// currentWave = 1 + floor(elapsedSec / WAVE_DURATION)). Mirrors ScoreSystem.CurrentWave —
        /// kept here too so WaveSpawnSystem's own derivation chain has no cross-file dependency.</summary>
        public static int CurrentWave(float elapsedSec)
        {
            return 1 + (int)Math.Floor(elapsedSec / GameConfig.Wave.WaveDuration);
        }

        /// <summary>Spawn interval for <paramref name="wave"/> (1-based). gdd: SPAWN_INTERVAL_BASE +
        /// SPAWN_INTERVAL_DECAY_PER_WAVE * (wave - 1), clamped to SPAWN_INTERVAL_MIN. Wave 1 uses the
        /// base interval unmodified (decay applies from wave 2 onward).</summary>
        public static float SpawnInterval(int wave)
        {
            float raw = GameConfig.Wave.SpawnIntervalBase + GameConfig.Wave.SpawnIntervalDecayPerWave * (wave - 1);
            return Mathf.Max(GameConfig.Wave.SpawnIntervalMin, raw);
        }

        /// <summary>Simultaneous spawn count for <paramref name="wave"/> (gdd: SPAWN_COUNT_PER_TICK_BASE
        /// plus 1 for every SPAWN_COUNT_GROWTH_INTERVAL waves elapsed since wave 1).</summary>
        public static int SpawnCount(int wave)
        {
            int growthSteps = (wave - 1) / GameConfig.Wave.SpawnCountGrowthInterval;
            return GameConfig.Wave.SpawnCountPerTickBase + growthSteps;
        }

        /// <summary>Enemy move speed for <paramref name="wave"/> (gdd: ENEMY_MOVE_SPEED_BASE compounded
        /// by ENEMY_SPEED_GROWTH_PER_WAVE every wave, starting from wave 1).</summary>
        public static float EnemySpeed(int wave)
        {
            return GameConfig.Enemy.MoveSpeedBase * Mathf.Pow(1f + GameConfig.Wave.EnemySpeedGrowthPerWave, wave - 1);
        }

        /// <summary>Enemy HP multiplier for <paramref name="wave"/> (gdd: HP成長はWave4から。
        /// ENEMY_HP_GROWTH_PER_WAVE を (wave - ENEMY_HP_GROWTH_START_WAVE + 1) 回複利適用). Waves before
        /// the start wave return a multiplier of 1 (baseline ENEMY_HP_BASE, no growth yet).</summary>
        public static float EnemyHpMultiplier(int wave)
        {
            int growthSteps = Mathf.Max(0, wave - GameConfig.Wave.EnemyHpGrowthStartWave + 1);
            return Mathf.Pow(1f + GameConfig.Wave.EnemyHpGrowthPerWave, growthSteps);
        }

        /// <summary>Full derived parameter set for <paramref name="elapsedSec"/> of run time.</summary>
        public static WaveParameters Compute(float elapsedSec)
        {
            int wave = CurrentWave(elapsedSec);
            float hpMultiplier = EnemyHpMultiplier(wave);
            int hp = Mathf.RoundToInt(GameConfig.Enemy.HpBase * hpMultiplier);
            return new WaveParameters(
                wave,
                SpawnInterval(wave),
                SpawnCount(wave),
                EnemySpeed(wave),
                hpMultiplier,
                hp);
        }

        /// <summary>Point on the ENEMY_SPAWN_RADIUS ring at <paramref name="angleRad"/> (gdd: 敵スポーン
        /// はスポーンリング上のランダム点). Pure trig only — WaveSpawner (Components) supplies the random
        /// angle so RNG stays out of the engine-independent Systems layer (mirrors ArenaCameraMath's
        /// pure-trig approach for the fixed camera pose). Y is always 0 (アリーナは XZ 平面).</summary>
        public static Vector3 SpawnPointOnRadius(float angleRad, float radius)
        {
            return new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
        }
    }
}
