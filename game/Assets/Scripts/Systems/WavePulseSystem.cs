// WavePulseSystem — pure C# math for the wave-switch HUD feedback (gdd 難易度曲線「ウェーブ切替時の
// フィードバック」; P-03; S-15). Engine-independent (rules/unity-code.md #3): Mathf is a value-type/math
// helper, not scene API — no MonoBehaviour, no scene lookups, no File I/O. Components/GameHudController
// accumulates elapsed time with Time.deltaTime and calls into this file every frame, applying the result
// to Ui/GameHud's WaveText.transform.localScale (mirrors Systems/HeroFxSystem's ComputeProgress pattern
// for S-16's death sequence — one pure progress/shape function, driven by a Components-owned timer).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class WavePulseSystem
    {
        /// <summary>True the instant currentWave has increased relative to the previously observed value
        /// (gdd: 「currentWave が直前フレームから増加した瞬間」). Equal or decreasing values (should never
        /// decrease in this game, but the check stays symmetric/defensive rather than assuming) are not a
        /// trigger.</summary>
        public static bool HasWaveIncreased(int previousWave, int currentWave)
        {
            return currentWave > previousWave;
        }

        /// <summary>Triangular pulse scale over [0, durationSeconds]: starts at 1.0, ramps linearly up to
        /// <paramref name="peakScale"/> at the halfway point, then ramps back down to 1.0 by the end (gdd:
        /// 「1.0→1.3→1.0倍」). Clamped at both ends so a caller that keeps calling past the duration (or
        /// with a stale/negative elapsed value) still gets a well-defined 1.0 rather than overshooting.
        /// <paramref name="durationSeconds"/>&lt;=0 is a wiring-error guard (mirrors HeroFxSystem.
        /// ComputeProgress) — every current caller passes a positive compile-time GameConfig.Fx constant,
        /// so this should never happen in practice; returns 1.0 (no pulse) rather than dividing by
        /// zero/producing NaN.</summary>
        public static float ComputeScale(float elapsedSeconds, float durationSeconds, float peakScale)
        {
            if (durationSeconds <= 0f)
            {
                return 1f;
            }
            float t = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            // 0..0.5 -> 0..1 (rising leg), 0.5..1 -> 1..0 (falling leg).
            float shape = t < 0.5f ? t * 2f : (1f - t) * 2f;
            return Mathf.Lerp(1f, peakScale, shape);
        }
    }
}
