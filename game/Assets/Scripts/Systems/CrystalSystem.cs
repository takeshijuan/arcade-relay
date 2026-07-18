// CrystalSystem — pure C# crystal pickup-radius + lifetime math (gdd クリスタル ドロップ＆回収; P-04;
// S-09). Engine-independent: no MonoBehaviour, no scene API (rules/unity-code.md #3) — Vector3/Mathf are
// value types, not scene API. Components/CrystalPickup drives this every frame with Time.deltaTime and
// owns the Instantiate/Destroy/RunStatsTracker side effects (mirrors HealthSystem.IsContacting's
// XZ-radius pattern and WaveSpawner/AutoAttackDriver's timer-accumulation pattern).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class CrystalSystem
    {
        /// <summary>True when the player's position is within pickupRadius of the crystal (XZ plane
        /// only — Y ignored, mirrors HealthSystem.IsContacting/AutoAttackSystem.FindNearestIndex's flat-
        /// distance convention. アリーナは XZ 平面, conventions.md §3). No pickup input required (gdd
        /// 操作仕様: 拾得操作なし自動回収 — P-02 照準ゼロ思想を回収にも適用).</summary>
        public static bool IsWithinPickupRadius(Vector3 playerPosition, Vector3 crystalPosition, float pickupRadius)
        {
            float dx = playerPosition.x - crystalPosition.x;
            float dz = playerPosition.z - crystalPosition.z;
            float distanceSq = dx * dx + dz * dz;
            return distanceSq <= pickupRadius * pickupRadius;
        }

        /// <summary>Result of ticking one frame of a crystal's CRYSTAL_LIFETIME countdown.</summary>
        public readonly struct LifetimeEvaluation
        {
            public readonly float Remaining;
            public readonly bool Expired;

            public LifetimeEvaluation(float remaining, bool expired)
            {
                Remaining = remaining;
                Expired = expired;
            }
        }

        /// <summary>Ticks the CRYSTAL_LIFETIME countdown by deltaTime (rule 2: delta-time 必須), floored
        /// at 0. Expired becomes true on the frame the countdown reaches 0 (gdd: CRYSTAL_LIFETIME 経過で
        /// 未回収クリスタルは消滅する).</summary>
        public static LifetimeEvaluation TickLifetime(float remaining, float deltaTime)
        {
            float ticked = Mathf.Max(0f, remaining - deltaTime);
            return new LifetimeEvaluation(ticked, ticked <= 0f);
        }
    }
}
