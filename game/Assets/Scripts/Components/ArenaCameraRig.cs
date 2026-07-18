// ArenaCameraRig — Game シーンの固定俯瞰カメラ配線 (gameplay-engineer, S-04). Sets the camera to a
// fixed pose derived from Systems/ArenaCameraMath (pure trig) using CAMERA_PITCH_DEG/CAMERA_HEIGHT
// and applies CAMERA_FOV, once at Start — never updated again (no player-follow; gdd 固定俯瞰カメラ:
// 四方から迫る敵を同時視認できる画角を常時維持する). Thin by design (rule: Components はライフサイクル
// と配線のみ).
//
// S-23 (gdd P-01「紙一重回避」ダッシュ紙一重回避のカメラシェイク演出): also owns the near-miss shake
// timer and applies its position-only offset (Systems/CameraShakeSystem) on top of the fixed base
// pose captured at Start — rotation/look direction is never touched by the shake (only Update()'s
// transform.localPosition write below does; the base pose set in Start() still owns rotation once).
// Components/HealthComponent (the contact-detection owner) calls TriggerNearMissShake() through the
// Instance singleton, mirroring the PlayerController.Instance/WaveSpawner.Instance pattern already
// used in this codebase.
//
// S-32 (gdd P-02「照準ゼロの自動攻撃」/P-03「群れ密度の圧力」撃破インパクト演出): owns a SECOND,
// independent decaying-offset timer for the enemy-kill camera nudge, reusing the same
// Systems/CameraShakeSystem math (TickTimer/ComputeOffset) with its own smaller GameConfig.Fx.
// EnemyKillCameraNudge* constants (deliberately distinct from DashNearMissShake* so the two cues never
// read as the same intensity — acceptance: 「ニアミス回避の合図と撃破の合図を振幅で混同させない」). The two
// offsets are summed on top of the fixed base pose so a kill nudge during an active near-miss shake (rare
// but possible — a dash invuln window overlapping a kill) composes rather than one silently overwriting
// the other. Components/EnemyAgent (the kill-confirmation owner) calls TriggerKillNudge() through the
// Instance singleton, mirroring TriggerNearMissShake()'s own call pattern.
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        /// <summary>Convenience lookup for Components/HealthComponent to trigger a near-miss shake
        /// (mirrors PlayerController.Instance/WaveSpawner.Instance). Set in Awake, cleared in
        /// OnDestroy.</summary>
        public static ArenaCameraRig Instance { get; private set; }

        /// <summary>Test-observability counter: counts TriggerNearMissShake() invocations (mirrors
        /// HealthComponent.SaveInvocationCountForTests). S-23 acceptance requires verifying "多重加算
        /// されず単発" — this has no other externally-observable return value to assert on. PlayMode
        /// tests reset this to 0 in SetUp/TearDown.</summary>
        public static int TriggerInvocationCountForTests;

        /// <summary>S-32 counterpart to <see cref="TriggerInvocationCountForTests"/>: counts
        /// TriggerKillNudge() invocations. PlayMode tests reset this to 0 in SetUp/TearDown.</summary>
        public static int KillNudgeInvocationCountForTests;

        /// <summary>Arena center the camera looks at (gdd: 単一円形アリーナ, origin-centered).</summary>
        private static readonly Vector3 ArenaCenter = Vector3.zero;

        private Vector3 _basePosition;
        private float _shakeRemaining;
        private float _killNudgeRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate ArenaCameraRig destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var cam = GetComponent<Camera>();
            cam.fieldOfView = GameConfig.Camera.Fov;

            // Fallback base position if the degenerate-pitch guard below skips ComputeFixedPose —
            // Update()'s shake offset must still apply on top of SOME stable base, never Vector3.zero.
            _basePosition = transform.localPosition;

            // ArenaCameraMath.ComputeFixedPose divides by Mathf.Sin(pitchRad); pitch values at/near
            // 0 or 180 degrees make that denominator vanish (or near-vanish) and would produce a
            // degenerate (near-Infinity magnitude) camera position. Guard here (Components layer)
            // rather than in the pure Systems layer, which must stay free of Debug/logging
            // (rules/unity-code.md #3). GameConfig.Camera.MinAbsSinPitch is a meaningful tolerance
            // (not Mathf.Epsilon, which only catches an exact-0 sine and lets near-0/near-180
            // degenerate poses through silently — CR-CODE S-04 iteration 2 finding).
            float pitchRad = GameConfig.Camera.PitchDeg * Mathf.Deg2Rad;
            if (Mathf.Abs(Mathf.Sin(pitchRad)) < GameConfig.Camera.MinAbsSinPitch)
            {
                Debug.LogError("[Wiring] invalid CAMERA_PITCH_DEG (" + GameConfig.Camera.PitchDeg +
                    "): Mathf.Sin(pitchRad) is ~0, camera pose would be degenerate (Infinity). " +
                    "Keeping the scene-authored Transform instead of applying the degenerate pose.");
                return;
            }

            ArenaCameraMath.ComputeFixedPose(
                GameConfig.Camera.PitchDeg, GameConfig.Camera.Height, ArenaCenter,
                out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            _basePosition = transform.localPosition;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _shakeRemaining = CameraShakeSystem.TickTimer(_shakeRemaining, deltaTime);
            _killNudgeRemaining = CameraShakeSystem.TickTimer(_killNudgeRemaining, deltaTime);

            Vector3 nearMissOffset = CameraShakeSystem.ComputeOffset(
                _shakeRemaining, GameConfig.Fx.DashNearMissShakeDuration, GameConfig.Fx.DashNearMissShakeMagnitude,
                transform.right, transform.up);
            // S-32: summed with the near-miss offset (not exclusive-or'd) — see class header for why the
            // two independent timers compose instead of one overwriting the other.
            Vector3 killNudgeOffset = CameraShakeSystem.ComputeOffset(
                _killNudgeRemaining, GameConfig.Fx.EnemyKillCameraNudgeDurationS, GameConfig.Fx.EnemyKillCameraNudgeMagnitudeM,
                transform.right, transform.up);
            transform.localPosition = _basePosition + nearMissOffset + killNudgeOffset;
        }

        /// <summary>Starts (or restarts) the near-miss shake window. Components/HealthComponent calls
        /// this at most once per dash invuln window (its own latch — S-23 acceptance: 単発シェイクに
        /// 丸める). Rotation is untouched; only Update()'s localPosition write reacts to this.</summary>
        public void TriggerNearMissShake()
        {
            _shakeRemaining = GameConfig.Fx.DashNearMissShakeDuration;
            TriggerInvocationCountForTests++;
        }

        /// <summary>Starts (or restarts) the enemy-kill camera nudge window (S-32). Components/EnemyAgent
        /// calls this once per confirmed kill (BeginKillPop), independently of any near-miss shake state.
        /// Rotation is untouched; only Update()'s localPosition write reacts to this.</summary>
        public void TriggerKillNudge()
        {
            _killNudgeRemaining = GameConfig.Fx.EnemyKillCameraNudgeDurationS;
            KillNudgeInvocationCountForTests++;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
