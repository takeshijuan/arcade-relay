// AutoAttackSystem — pure C# nearest-enemy search for auto-attack targeting (gdd 自動攻撃（照準ゼロ）;
// P-02, P-03; S-06). Given the attacker's XZ position and a list of candidate positions, finds the
// index of the nearest candidate within AUTO_ATTACK_RANGE (or NoTarget if none qualify). Engine-
// independent: no MonoBehaviour/scene API (rules/unity-code.md #3) — Vector3/Mathf are value types,
// not scene API. Components/AutoAttackDriver drives this every AUTO_ATTACK_INTERVAL tick, owns the
// timer accumulation (mirrors WaveSpawner's timer pattern) and the actual damage application
// (EnemyAgent.ApplyAutoAttackDamage → Types.cs EntityState.ApplyDamage, the shared pure reducer).
// Also holds ComputeAttackAnimSpeedScale (S-17: attack-clip/AUTO_ATTACK_INTERVAL speed-scaling math —
// Editor/AssetIntegration.BuildHeroController is the sole caller, at AnimatorController-build time).
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class AutoAttackSystem
    {
        /// <summary>Sentinel returned by FindNearestIndex when no candidate is within range.</summary>
        public const int NoTarget = -1;

        /// <summary>Index into <paramref name="candidatePositions"/> of the nearest position to
        /// <paramref name="origin"/> that is within <paramref name="maxRange"/> (XZ plane only — Y
        /// ignored, mirrors EnemyApproachSystem's flat-distance convention. アリーナは XZ 平面,
        /// conventions.md §3). Returns <see cref="NoTarget"/> when the list is empty or every
        /// candidate is out of range. Ties (equal distance) resolve to the earliest index
        /// (deterministic — no reliance on iteration/collection order guarantees beyond the input list).</summary>
        public static int FindNearestIndex(Vector3 origin, IReadOnlyList<Vector3> candidatePositions, float maxRange)
        {
            int nearestIndex = NoTarget;
            float nearestSqrDistance = float.MaxValue;
            float maxRangeSqr = maxRange * maxRange;

            for (int i = 0; i < candidatePositions.Count; i++)
            {
                Vector3 candidate = candidatePositions[i];
                float dx = candidate.x - origin.x;
                float dz = candidate.z - origin.z;
                float sqrDistance = dx * dx + dz * dz;
                if (sqrDistance > maxRangeSqr)
                {
                    continue;
                }
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestIndex = i;
                }
            }
            return nearestIndex;
        }

        /// <summary>Computes the AnimatorState.speed multiplier so a single playthrough of the attack clip
        /// fits within one AUTO_ATTACK_INTERVAL tick (gdd 自動攻撃の当たり表現方式・S-17: 「同一クリップで
        /// 連続発動しても不自然にならないよう、アニメ長は AUTO_ATTACK_INTERVAL 初期値0.6sを超えない（超える
        /// 場合は再生速度を発動間隔に合わせてスケーリングする）」). Editor/AssetIntegration.BuildHeroController
        /// bakes the result directly into the generated Hero.controller's Attack AnimatorState (rule 13:
        /// アニメ切替はコードでなく AnimatorController 資産 — no runtime Animator.speed manipulation needed).
        /// Returns 1 (no scaling) when the clip already fits within the interval, or when either input is
        /// non-positive (defensive — a 0/negative clip length or interval is a wiring-error guard, mirrors
        /// HeroFxSystem.ComputeProgress's durationSeconds&lt;=0 guard, not a real gameplay scenario).</summary>
        public static float ComputeAttackAnimSpeedScale(float clipLengthSeconds, float intervalSeconds)
        {
            if (intervalSeconds <= 0f || clipLengthSeconds <= 0f || clipLengthSeconds <= intervalSeconds)
            {
                return 1f;
            }
            return clipLengthSeconds / intervalSeconds;
        }
    }
}
