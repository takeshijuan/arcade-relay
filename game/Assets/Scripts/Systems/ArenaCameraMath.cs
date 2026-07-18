// ArenaCameraMath — pure trigonometry deriving the fixed overhead camera pose (gdd 固定俯瞰カメラ
// P-01; S-04) from CAMERA_PITCH_DEG/CAMERA_HEIGHT. Engine-independent: no MonoBehaviour/scene API
// (rules/unity-code.md #3). Vector3/Quaternion/Mathf are permitted value types.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class ArenaCameraMath
    {
        /// <summary>
        /// Computes a camera position/rotation that (a) sits <paramref name="height"/> units
        /// above <paramref name="lookTarget"/>'s plane and (b) looks down at
        /// <paramref name="pitchDeg"/> below horizontal, directly at <paramref name="lookTarget"/>
        /// (gdd CAMERA_PITCH_DEG/CAMERA_HEIGHT). No follow — call once at Game start (S-04
        /// acceptance: カメラは...固定位置に設定され追従しない). Camera sits on the -Z side with no
        /// yaw/roll offset.
        /// </summary>
        public static void ComputeFixedPose(
            float pitchDeg, float height, Vector3 lookTarget,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(pitchDeg, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            float pitchRad = pitchDeg * Mathf.Deg2Rad;
            float distance = height / Mathf.Sin(pitchRad);
            position = lookTarget - forward * distance;
        }

        /// <summary>
        /// Computes how far south (-Z, relative to the look target's Z) the fixed camera's vertical
        /// field of view still reaches the ground plane (gdd「南側可視性の再点検」節の幾何: the camera
        /// sits <c>H/tan(P)</c> units south of the look target (see <see cref="ComputeFixedPose"/>) and
        /// looks north with a downward pitch, so the near edge of the frustum — the steepest ray, at
        /// <c>pitchDeg + fovDeg/2</c> below horizontal — determines the closest-to-camera (southernmost)
        /// ground point still inside view: <c>-H/tan(P) + H/tan(P + F/2)</c>). The returned value is
        /// negative (south of the look target); a more negative value means visibility extends farther
        /// south (S-22 acceptance: 南側可視限界が拡大したことをこの幾何計算で検証する).
        ///
        /// Valid domain: <paramref name="pitchDeg"/> &gt; 0 and <c>pitchDeg + fovDeg / 2</c> &lt; 180
        /// (both terms are tangents, which have poles at 90°/270°). <paramref name="pitchDeg"/> &lt;= 0
        /// throws <see cref="System.ArgumentOutOfRangeException"/> instead of silently returning
        /// ±Infinity (asymmetric with <see cref="ComputeFixedPose"/>'s sin-based degenerate-pose guard,
        /// which lives in the Components layer per rules/unity-code.md #3 — this guard is safe to keep
        /// here because it is a plain exception, not engine/logging API). Note:
        /// <c>pitchDeg + fovDeg / 2 == 90°</c> exactly (e.g. the pre-S-22 65° + 25° tested by
        /// <c>ComputeSouthVisibilityLimitZ_OldGdd値_MatchesHandComputedMinus6_5m</c>) sits on the
        /// mathematical pole of the second tan() term, but IEEE 754 float evaluates
        /// <c>Mathf.Tan(90°)</c> as a large finite value rather than exactly Infinity, so that term
        /// collapses to ~0 — matching the true limit — without needing an explicit guard here.
        /// </summary>
        public static float ComputeSouthVisibilityLimitZ(float pitchDeg, float height, float fovDeg)
        {
            if (pitchDeg <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(pitchDeg), pitchDeg,
                    "ComputeSouthVisibilityLimitZ requires pitchDeg > 0 (tan(pitchRad) would be ~0, " +
                    "producing an unbounded/degenerate south visibility limit).");
            }

            float pitchRad = pitchDeg * Mathf.Deg2Rad;
            float nearEdgeRad = (pitchDeg + fovDeg * 0.5f) * Mathf.Deg2Rad;
            return -height / Mathf.Tan(pitchRad) + height / Mathf.Tan(nearEdgeRad);
        }
    }
}
