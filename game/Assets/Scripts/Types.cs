// Types — shared plain data types used across engine-independent Systems and the
// Unity-facing Components/Ui/Persistence layers. Keep these free of MonoBehaviour.
using System;

namespace ForgeGame
{
    /// <summary>Enemy variant tag (gdd 敵・障害物). No per-variant AI branch (P-03).</summary>
    public enum EnemyKind
    {
        Swarmer = 0,
        HeavySwarmer = 1,
    }

    /// <summary>Immutable snapshot of an in-run entity's combat state (pure-C# systems input).</summary>
    [Serializable]
    public struct EntityState
    {
        public int Hp;
        public int MaxHp;

        public EntityState(int hp, int maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
        }

        public bool IsDead => Hp <= 0;

        /// <summary>Pure reducer: returns a new state with <paramref name="damage"/> subtracted from
        /// Hp, floored at 0 (never negative — used for both HP0 撃破判定 and future HealthSystem
        /// player-damage application; S-06 自動攻撃・敵HP・撃破). No side effects.</summary>
        public EntityState ApplyDamage(int damage) => new EntityState(Math.Max(0, Hp - damage), MaxHp);
    }

    /// <summary>Per-run tallies handed to MetaProgression at Result (gdd スコア算出 / メタ進行).</summary>
    [Serializable]
    public struct RunResult
    {
        public int FinalScore;
        public float SurvivalTimeSec;
        public int WaveReached;
        public int NormalKillCount;
        public int HeavyKillCount;
        public int CrystalsCollected;
    }
}
