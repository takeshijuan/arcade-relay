// EnemyVisualMotionSystem — pure C# code motion for the swarmer's "Visual" child (gdd/assets.md 決定:
// MDL-02 はリグ未完了・must-replace・ANM-04(approach_loop)未生成のため、接近表現はスケルタルアニメ/
// Avatar/AnimatorController に依存せず、コード側の前傾チルト + 上下バウンスで代替する — Checkpoint B
// 追認 2026-07-13; S-21). Engine-independent: no MonoBehaviour/scene API (rules/unity-code.md #3;
// Vector3/Quaternion/Mathf 等の値型・数学型のみ使用). Components/EnemyAgent drives this every frame with
// Time.deltaTime and applies the result to a "Visual" child transform's localPosition/localRotation —
// deliberately NOT the EnemyAgent root's own transform, which Systems/EnemyApproachSystem already owns
// exclusively for world-space approach movement (renderer/motion separation also doubles as the
// drop-in replacement seam for a future rigged model once Tripo credits are topped up).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class EnemyVisualMotionSystem
    {
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>Advances a bounce phase accumulator by <paramref name="frequencyHz"/> over
        /// <paramref name="deltaTime"/> (delta-time 必須), wrapped to [0, 2π) so the phase stays
        /// numerically stable for arbitrarily long-lived enemies. Uses Mathf.Repeat (not a single
        /// conditional subtraction — CR-CODE S-21 iteration 1 minor finding: a single `next - TwoPi`
        /// only wraps once per call, so a hitch longer than one bounce period, e.g. deltaTime >
        /// 1/frequencyHz, would leave `next` >= TwoPi and let the phase grow unbounded across
        /// sustained low-fps stretches) so the wrap holds for any deltaTime, not just deltaTime <
        /// one period.</summary>
        public static float AdvanceBouncePhase(float phase, float frequencyHz, float deltaTime)
        {
            float next = phase + frequencyHz * TwoPi * deltaTime;
            return Mathf.Repeat(next, TwoPi);
        }

        /// <summary>Vertical bounce offset (m) for the current phase — a sine wave, mirroring
        /// Components/CrystalPickup's existing bob-motion pattern (same coded-motion technique reused for
        /// a different visual — gdd「クリスタル・アリーナ環境の視覚表現方針」と同種のコードモーション).</summary>
        public static float ComputeBounceOffsetY(float phase, float amplitude)
        {
            return Mathf.Sin(phase) * amplitude;
        }

        /// <summary>Local rotation combining a yaw toward <paramref name="approachDirectionXZ"/> (so the
        /// lean reads as "forward" relative to the direction Systems/EnemyApproachSystem is actually
        /// stepping the enemy toward) with a fixed forward-lean pitch (前傾チルト). Returns
        /// Quaternion.identity when the direction is degenerate (already at the target — mirrors
        /// EnemyApproachSystem.ComputeDirection's own zero-vector guard, avoiding a NaN LookRotation).</summary>
        public static Quaternion ComputeApproachTilt(Vector3 approachDirectionXZ, float tiltDeg)
        {
            if (approachDirectionXZ.sqrMagnitude < 1e-8f)
            {
                return Quaternion.identity;
            }
            Quaternion yaw = Quaternion.LookRotation(approachDirectionXZ.normalized, Vector3.up);
            Quaternion forwardLean = Quaternion.AngleAxis(tiltDeg, Vector3.right);
            return yaw * forwardLean;
        }
    }
}
