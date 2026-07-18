// EnemyKillPopSystem — pure C# scale math for the enemy-kill "pop" visual (gdd P-02「照準ゼロの自動攻撃」/
// P-03「群れ密度の圧力」juice ブラッシュアップ; S-32). Engine-independent (rules/unity-code.md #3): Mathf is
// a value-type utility, not scene API. Components/EnemyAgent drives this every frame once a killing hit
// is confirmed (ApplyAutoAttackDamage → BeginKillPop), applying the returned scale to the enemy's visual
// transform (localScale) until IsComplete, at which point it actually Destroys the GameObject — the
// enemy is already removed from ActiveEnemies (no longer counted for contact/targeting/spawn-cap) the
// instant the kill is confirmed, so this timer only delays the visible disappearance, never the
// score/crystal-drop bookkeeping (Components/AutoAttackDriver.RegisterKillAndDropCrystals already ran
// synchronously before this timer starts).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class EnemyKillPopSystem
    {
        /// <summary>
        /// Linear scale factor for the current instant of an active kill-pop, growing from 1.0 at
        /// trigger (elapsed=0) to <paramref name="scaleMultiplier"/> once <paramref name="elapsed"/>
        /// reaches <paramref name="duration"/> (S-32 acceptance: 「消滅前に一瞬スケールアップしてから消える」
        /// — the enemy is destroyed the instant this reaches the multiplier, so the pop reads as a single
        /// outward "puff" immediately preceding disappearance, not an oscillation). Clamped so a caller
        /// ticking past duration (e.g. one extra frame before the Destroy() call takes effect) never
        /// overshoots the multiplier.
        /// </summary>
        public static float ComputeScale(float elapsed, float duration, float scaleMultiplier)
        {
            if (duration <= 0f)
            {
                return scaleMultiplier;
            }
            float t = Mathf.Clamp01(elapsed / duration);
            return Mathf.Lerp(1f, scaleMultiplier, t);
        }

        /// <summary>True once the pop's visible duration has fully elapsed — the caller should destroy
        /// the enemy's GameObject on the same frame this first returns true.</summary>
        public static bool IsComplete(float elapsed, float duration) => elapsed >= duration;
    }
}
