// EnemyApproachSystem — pure C# straight-line enemy approach math (gdd 敵・障害物: 常にプレイヤー方向
// への直線移動、回避・迂回ロジックなし; P-03; S-05). Engine-independent: no MonoBehaviour/scene API
// (rules/unity-code.md #3). Components/EnemyAgent drives this every frame with Time.deltaTime and
// applies the result to its Transform.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class EnemyApproachSystem
    {
        /// <summary>Normalized XZ direction from <paramref name="currentPosition"/> toward
        /// <paramref name="targetPosition"/> (Y ignored — アリーナは XZ 平面, conventions.md §3). Returns
        /// Vector3.zero when already at the target (avoids normalizing a zero-length vector into NaN).</summary>
        public static Vector3 ComputeDirection(Vector3 currentPosition, Vector3 targetPosition)
        {
            Vector3 flat = new Vector3(
                targetPosition.x - currentPosition.x, 0f, targetPosition.z - currentPosition.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                return Vector3.zero;
            }
            return flat.normalized;
        }

        /// <summary>One frame's XZ displacement toward <paramref name="targetPosition"/> at
        /// <paramref name="speed"/> m/s, scaled by <paramref name="deltaTime"/> (delta-time 必須).
        /// Never overshoots the target — the step is clamped to the remaining flat distance.</summary>
        public static Vector3 ComputeStep(Vector3 currentPosition, Vector3 targetPosition, float speed, float deltaTime)
        {
            Vector3 direction = ComputeDirection(currentPosition, targetPosition);
            if (direction == Vector3.zero)
            {
                return currentPosition;
            }
            float remaining = Vector3.Distance(
                new Vector3(currentPosition.x, 0f, currentPosition.z),
                new Vector3(targetPosition.x, 0f, targetPosition.z));
            float step = Mathf.Min(speed * deltaTime, remaining);
            return currentPosition + direction * step;
        }
    }
}
