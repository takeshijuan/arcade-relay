// ResultCountUpSystem — pure C# math for the Result final-score count-up animation (gdd P-04「積み上がる
// 再挑戦」; S-33). Engine-independent (rules/unity-code.md #3): Mathf is a value-type/math helper, not
// scene API — no MonoBehaviour, no scene lookups, no File I/O. Components/ResultController accumulates
// elapsed time with Time.deltaTime and calls into this file every frame, applying the result to
// Ui/ResultScreen's FinalScoreText (mirrors Systems/WavePulseSystem's split between pure progress/shape
// math and a Components-owned timer). The high-score notice pulse reuses Systems/WavePulseSystem.
// ComputeScale directly rather than duplicating a second triangular-pulse function.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class ResultCountUpSystem
    {
        /// <summary>Displayed score at <paramref name="elapsedSeconds"/> into a
        /// <paramref name="durationSeconds"/>-long ease-out count-up from 0 to
        /// <paramref name="finalScore"/> (gdd: 「0から最終値まで一定時間かけてカウントアップ表示し（イーズ
        /// アウトで終盤ほど刻みが小さくなる）」). Uses a standard ease-out-power curve
        /// (progress = 1-(1-t)^easeExponent): monotonically increasing in t, so the returned int is
        /// monotonically non-decreasing as elapsedSeconds grows, and reaches exactly
        /// <paramref name="finalScore"/> once elapsedSeconds&gt;=durationSeconds (t clamps to 1, so
        /// progress=1). Larger <paramref name="easeExponent"/> means the tail values move less per
        /// second (per the acceptance's "終盤ほど刻みが小さくなる"). durationSeconds&lt;=0 is a
        /// wiring-error guard (mirrors WavePulseSystem.ComputeScale) — every current caller passes a
        /// positive compile-time GameConfig.Fx constant, so this should never happen in practice; returns
        /// finalScore immediately (no animation) rather than dividing by zero/producing NaN.</summary>
        public static int ComputeDisplayedScore(float elapsedSeconds, float durationSeconds, int finalScore, float easeExponent)
        {
            if (durationSeconds <= 0f)
            {
                return finalScore;
            }
            float t = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            float exponent = Mathf.Max(easeExponent, 0.0001f); // guard against a non-positive exponent producing NaN via Pow
            float progress = 1f - Mathf.Pow(1f - t, exponent);
            return Mathf.FloorToInt(finalScore * progress);
        }
    }
}
