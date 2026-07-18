// PlayerMovement — pure XZ-plane movement math (gdd プレイヤー移動 P-01,P-02; S-04). Engine-
// independent: no MonoBehaviour/scene API (rules/unity-code.md #3). Vector2/Vector3 are permitted
// value types. Diagonal input is normalized here (defense in depth) even though InputLayer/
// GameInput's 2DVector composite already normalizes digital diagonals by default, so the "never
// exceeds PLAYER_MOVE_SPEED" guarantee (gdd 操作仕様 / conventions.md §3) holds independent of the
// composite's Mode setting.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class PlayerMovement
    {
        /// <summary>
        /// World-space XZ displacement for one frame. <paramref name="inputVector"/> is the raw
        /// Move action value (x=left/right, y=forward/back); clamped to unit length before being
        /// scaled by <paramref name="moveSpeed"/> so diagonal input never exceeds moveSpeed (gdd
        /// 操作仕様). Y is always 0 — movement stays on the XZ plane (conventions.md §3).
        /// </summary>
        public static Vector3 ComputeDisplacement(Vector2 inputVector, float moveSpeed, float deltaTime)
        {
            if (inputVector.sqrMagnitude > 1f)
            {
                inputVector = inputVector.normalized;
            }
            return new Vector3(inputVector.x, 0f, inputVector.y) * (moveSpeed * deltaTime);
        }

        /// <summary>
        /// Clamps <paramref name="position"/>'s XZ distance from the arena center (origin) to
        /// <paramref name="arenaRadius"/> (gdd アリーナ境界: ARENA_RADIUS 外へ出られない). Y passes
        /// through unchanged.
        /// </summary>
        public static Vector3 ClampToArena(Vector3 position, float arenaRadius)
        {
            Vector2 flat = new Vector2(position.x, position.z);
            if (flat.sqrMagnitude > arenaRadius * arenaRadius)
            {
                flat = flat.normalized * arenaRadius;
            }
            return new Vector3(flat.x, position.y, flat.y);
        }
    }
}
