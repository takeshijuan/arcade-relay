// DashTrailSystem — pure C# spawn-cadence + fade math for the dash afterimage trail (gdd P-01「紙一重
//回避」juice ブラッシュアップ; S-31). Engine-independent (rules/unity-code.md #3): Mathf is a value-type
// helper, not scene API — no MonoBehaviour, no scene lookups, no Instantiate/Destroy. This file never
// touches Systems/DashSystem's direction/timer/displacement logic (acceptance: 「Systems/DashSystem の
// 判定ロジックには変更を加えない」) — it is a display-only sibling concern driven independently by
// Components/DashTrailSpawner from PlayerController.IsDashing (already public, read-only surfacing of
// dash state — mirrors Components/HeroFxController reading HealthComponent.IsDeathSequenceActive).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class DashTrailSystem
    {
        /// <summary>
        /// True once <paramref name="spawnTimer"/> (accumulated with Time.deltaTime by the caller — rule
        /// 2: delta-time 必須) has reached <paramref name="spawnIntervalSeconds"/>
        /// (GameConfig.Fx.DashTrailSpawnIntervalS). Callers spawn one ghost and subtract the interval
        /// (not reset to 0) so sub-frame remainder carries forward — the same accumulator shape
        /// Components/WaveSpawner already uses for its own periodic spawn timer.
        /// <paramref name="spawnIntervalSeconds"/>&lt;=0 is a wiring-error guard (mirrors
        /// Systems/HeroFxSystem.ComputeProgress's identical non-positive-duration guard; every current
        /// caller passes a positive compile-time GameConfig constant) that returns false rather than true:
        /// the caller's `while (ShouldSpawnGhost(...)) { timer -= interval; ... }` catch-up loop
        /// (Components/DashTrailSpawner.Update) would otherwise spin forever the instant the interval is
        /// misconfigured to &lt;=0, since subtracting a non-positive interval never shrinks the
        /// accumulated timer back below itself — CR-CODE S-31 iteration 1 minor finding.
        /// </summary>
        public static bool ShouldSpawnGhost(float spawnTimer, float spawnIntervalSeconds)
        {
            if (spawnIntervalSeconds <= 0f)
            {
                return false;
            }
            return spawnTimer >= spawnIntervalSeconds;
        }

        /// <summary>
        /// Ghost alpha at <paramref name="elapsedSeconds"/> into its
        /// GameConfig.Fx.DashTrailGhostLifetimeS lifetime: starts at <paramref name="initialAlpha"/>
        /// (GameConfig.Fx.DashTrailGhostAlpha) and fades linearly to exactly 0 the instant elapsed
        /// reaches the lifetime (S-31 acceptance: 「フェードアウトしながら短時間で消滅する」). A
        /// non-positive lifetime is a wiring-error guard (every current caller passes a positive
        /// compile-time GameConfig constant) that returns 0 rather than dividing by zero — mirrors
        /// Systems/HeroFxSystem.ComputeProgress's identical guard shape.
        /// </summary>
        public static float ComputeGhostAlpha(float elapsedSeconds, float lifetimeSeconds, float initialAlpha)
        {
            if (lifetimeSeconds <= 0f)
            {
                return 0f;
            }
            float progress = Mathf.Clamp01(elapsedSeconds / lifetimeSeconds);
            return initialAlpha * (1f - progress);
        }

        /// <summary>True once a ghost has lived at least its full GameConfig.Fx.DashTrailGhostLifetimeS —
        /// the caller (Components/DashTrailGhost) destroys the GameObject the frame this flips true.</summary>
        public static bool IsGhostExpired(float elapsedSeconds, float lifetimeSeconds) =>
            elapsedSeconds >= lifetimeSeconds;
    }
}
