// HeavyEnemySystem — pure C# heavy-variant unlock/roll/stat-multiplier math (gdd 敵・障害物:
// ヘヴィスウォーマー; P-03; S-14). Engine-independent: no MonoBehaviour, no scene API
// (rules/unity-code.md #3) — RNG stays out of this layer (mirrors WaveSpawnSystem.SpawnPointOnRadius'
// pure-trig approach): Components/WaveSpawner supplies the random roll value, this file only decides
// whether that roll clears the gdd threshold and derives the stat multipliers. No new AI branch is
// introduced here (gdd 決定: スウォーマーと同一の直線接近ロジックを再利用) — this class is purely stat
// derivation, reused by both Components/WaveSpawner (spawn decision + speed/HP) and
// Components/HealthComponent (contact damage) and Components/AutoAttackDriver (via
// Components/RunStatsTracker.RegisterKill/Components/CrystalPickup.SpawnDrop, which already branch on
// isHeavy/kind — see those files' own gdd/SCORE_PER_KILL_*/CRYSTAL_DROP_PER_KILL_* constants).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class HeavyEnemySystem
    {
        /// <summary>True once <paramref name="wave"/> has reached HEAVY_ENEMY_UNLOCK_WAVE (gdd: 敵・障害物
        /// 「HEAVY_ENEMY_UNLOCK_WAVE以降、スポーン時に...」).</summary>
        public static bool IsUnlocked(int wave)
        {
            return wave >= GameConfig.Enemy.HeavyUnlockWave;
        }

        /// <summary>Decides whether a single spawn roll should produce the heavy variant. Takes the
        /// already-rolled uniform [0,1) value (RNG lives in Components/WaveSpawner, matching
        /// WaveSpawnSystem.SpawnPointOnRadius' random-angle convention) rather than generating it here, so
        /// this pure function stays deterministic and EditMode-testable.</summary>
        public static bool ShouldSpawnHeavy(int wave, float randomValue01)
        {
            return IsUnlocked(wave) && randomValue01 < GameConfig.Enemy.HeavySpawnChance;
        }

        /// <summary>Heavy-adjusted move speed (gdd: HEAVY_ENEMY_SPEED_MULTで減速). <paramref name="baseSpeed"/>
        /// is the wave-derived normal speed (WaveSpawnSystem.EnemySpeed) — the two multipliers stack, not
        /// replace each other (wave growth still applies, gdd table's per-wave value is the "base").</summary>
        public static float AdjustedSpeed(float baseSpeed)
        {
            return baseSpeed * GameConfig.Enemy.HeavySpeedMult;
        }

        /// <summary>Heavy-adjusted max HP (gdd: 最大HPにHEAVY_ENEMY_HP_MULTを適用), rounded to the nearest
        /// int like WaveSpawnSystem.Compute's own EnemyHp rounding.</summary>
        public static int AdjustedHp(int baseHp)
        {
            return Mathf.RoundToInt(baseHp * GameConfig.Enemy.HeavyHpMult);
        }

        /// <summary>Heavy-adjusted contact damage (gdd: 接触ダメージにHEAVY_ENEMY_CONTACT_DAMAGE_MULTを適用).</summary>
        public static int AdjustedContactDamage(int baseContactDamage)
        {
            return Mathf.RoundToInt(baseContactDamage * GameConfig.Enemy.HeavyContactDamageMult);
        }
    }
}
