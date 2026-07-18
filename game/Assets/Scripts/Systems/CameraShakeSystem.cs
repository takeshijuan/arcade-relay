// CameraShakeSystem — pure C# trigger-detection + decay math for the dash 紙一重回避 camera shake
// (gdd P-01「紙一重回避」; S-23). Engine-independent (rules/unity-code.md #3): Vector3/Mathf are value
// types, not scene API. Components/HealthComponent drives the trigger decision every frame from its
// existing per-enemy contact loop (isContacting via Systems/HealthSystem.IsContacting + isInvulnerable
// via Components/PlayerController.IsDashInvulnerable — both already computed there for the damage
// path); Components/ArenaCameraRig owns the shake timer and applies the offset on top of its fixed
// base pose (ArenaCameraMath.ComputeFixedPose) without ever touching rotation/look direction.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class CameraShakeSystem
    {
        /// <summary>
        /// True exactly on the frame a "near miss" should trigger a shake: a contact occurred while
        /// dash-invulnerable, and no shake has already been latched for the current invulnerability
        /// window (S-23 acceptance: 「同一ダッシュの無敵窓内で複数回接触判定が発生しても多重加算せず1回分の
        /// シェイクに丸める」— ダッシュ1回につき最大1回). Deliberately independent of any per-enemy
        /// contact-damage cooldown (HealthSystem.EvaluateContact): a contact blocked purely by
        /// invulnerability is a near-miss regardless of whether that same contact would also have been
        /// blocked by ENEMY_CONTACT_COOLDOWN — the invincibility is what "saved" the player, so it is
        /// the relevant cause here. Callers own the latch bookkeeping (set true immediately after this
        /// returns true; reset to false the instant <paramref name="isInvulnerable"/> observes false,
        /// i.e. the dash's invuln window has ended) — this function is a single stateless decision
        /// point, not a state container (Systems layer must stay pure/stateless).
        /// </summary>
        public static bool ShouldTriggerNearMissShake(bool isContacting, bool isInvulnerable, bool alreadyTriggeredThisWindow) =>
            isContacting && isInvulnerable && !alreadyTriggeredThisWindow;

        /// <summary>Delta-time-scaled countdown, floored at 0 (rule 2: delta-time 必須) — same shape as
        /// Systems/DashSystem.TickTimer. Kept as its own function here (not a cross-reference into
        /// DashSystem) because a camera-shake timer is not a dash-domain concept; this is a trivial
        /// 1-line Mathf.Max clamp already duplicated identically by HealthSystem's own cooldown ticking
        /// in this codebase, not the kind of multi-term gdd formula (wave-spawn-interval,
        /// effective-stat derivations) that conventions.md §5「式の再実装禁止」targets.</summary>
        public static float TickTimer(float remaining, float deltaTime) => Mathf.Max(0f, remaining - deltaTime);

        /// <summary>
        /// World-space position offset for the current instant of an active shake, linearly decaying
        /// from <paramref name="magnitude"/> at trigger to exactly <see cref="Vector3.zero"/> the
        /// instant <paramref name="remaining"/> reaches 0 (S-23 acceptance: 「DASH_NEARMISS_SHAKE_
        /// DURATION経過後に元の固定位置へ厳密に復帰すること」— an exact-zero return at remaining&lt;=0, not
        /// an asymptotic decay, is what guarantees the strict revert the PlayMode test checks for).
        /// The offset sweeps through exactly one damped cycle along the camera's own local right/up
        /// axes (so it reads as a screen-space shake regardless of the fixed camera's world
        /// orientation, and never touches look direction/rotation — gdd 固定俯瞰カメラ: 注視方向は変え
        /// ない). The phase is derived purely from the remaining/duration ratio — no extra tunable
        /// frequency constant is introduced (rules/unity-code.md #1 exempts structural, non-tunable
        /// values like this fixed one-cycle phase mapping, the same way a bare array-index 0/1 literal
        /// is exempt).
        /// </summary>
        public static Vector3 ComputeOffset(
            float remaining, float duration, float magnitude, Vector3 cameraRight, Vector3 cameraUp)
        {
            if (remaining <= 0f || duration <= 0f)
            {
                return Vector3.zero;
            }
            float decay = remaining / duration;
            float phase = (1f - decay) * Mathf.PI * 2f;
            return (cameraRight * Mathf.Sin(phase) + cameraUp * Mathf.Cos(phase)) * (magnitude * decay);
        }
    }
}
