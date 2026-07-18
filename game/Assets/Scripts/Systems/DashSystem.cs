// DashSystem — pure C# dash-direction resolution + timer arithmetic (gdd ダッシュ回避 P-01; S-07).
// Engine-independent: no MonoBehaviour/scene API (rules/unity-code.md #3) — Vector2/Vector3/Mathf are
// value types, not scene API. Components/PlayerController drives this: it owns the per-frame Dash
// input read + EnemyAgent.ActiveEnemies query (scene API, disallowed here), and calls the pure
// functions below for direction priority, cooldown/duration/invuln timer ticking, and displacement.
//
// Direction priority (gdd 操作仕様 / conventions.md §4 — must NOT reference hero facing):
//   (1) live move input direction (same-frame Move action value)
//   (2) last nonzero move direction (直前まで移動していた向き)
//   (3) opposite of the nearest enemy direction (最寄り敵の反対方向) — only when (1) and (2) are both
//       unavailable, i.e. the player has never moved yet (ラン開始直後など一度も移動していない時)
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class DashSystem
    {
        /// <summary>
        /// Resolves the XZ dash direction per the 3-tier gdd priority above. Returns
        /// <see cref="Vector3.zero"/> only when every tier is unavailable (no move input, no prior
        /// movement, no enemy reference point) — callers must not consume a dash activation in that
        /// case (there is nowhere to go). Never reads/returns anything derived from hero facing
        /// (conventions.md §4: 「facing 非連動」).
        /// </summary>
        public static Vector3 ResolveDashDirection(
            Vector2 moveInput, Vector3 lastMoveDirection, bool hasNearestEnemy, Vector3 nearestEnemyDirection)
        {
            if (moveInput.sqrMagnitude > 0f)
            {
                return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            }
            // lastMoveDirection/nearestEnemyDirection are flattened onto XZ here (defense in depth,
            // mirrors PlayerMovement's explicit Y=0 construction) so a caller passing an
            // un-flattened world-space offset (e.g. an enemy at a different Y) can never leak a
            // vertical component into the dash direction (conventions.md §3: アリーナは XZ 平面).
            Vector3 flatLastMove = new Vector3(lastMoveDirection.x, 0f, lastMoveDirection.z);
            if (flatLastMove.sqrMagnitude > 0f)
            {
                return flatLastMove.normalized;
            }
            Vector3 flatEnemyDirection = new Vector3(nearestEnemyDirection.x, 0f, nearestEnemyDirection.z);
            if (hasNearestEnemy && flatEnemyDirection.sqrMagnitude > 0f)
            {
                return -flatEnemyDirection.normalized;
            }
            return Vector3.zero;
        }

        /// <summary>True once <paramref name="cooldownRemaining"/> has ticked down to (or started
        /// at) zero — gdd 操作仕様: DASH_COOLDOWN 経過まで再発動不可、クールダウン中は無反応（入力バッファなし）。
        /// Callers must simply drop a press while this is false, never queue it.</summary>
        public static bool CanActivate(float cooldownRemaining) => cooldownRemaining <= 0f;

        /// <summary>Delta-time-scaled countdown, floored at 0 (rule 2: delta-time 必須). Shared by
        /// the cooldown / dash-duration / invuln-window timers — all three tick down identically.</summary>
        public static float TickTimer(float remaining, float deltaTime) => Mathf.Max(0f, remaining - deltaTime);

        /// <summary>World-space XZ displacement for one frame of an active dash (gdd DASH_SPEED ×
        /// deltaTime; total over DASH_DURATION ≈ DASH_SPEED×DASH_DURATION ≈ 4m). Expects
        /// <paramref name="dashDirection"/> pre-normalized by <see cref="ResolveDashDirection"/> at
        /// activation time and held fixed for the whole dash (gdd 操作仕様: 「ダッシュ中は移動入力を無視し
        /// 軌道を固定」).</summary>
        public static Vector3 ComputeDisplacement(Vector3 dashDirection, float dashSpeed, float deltaTime) =>
            dashDirection * (dashSpeed * deltaTime);
    }
}
