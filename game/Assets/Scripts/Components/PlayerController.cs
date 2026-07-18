// PlayerController — Game シーンのプレイヤー移動配線 (gameplay-engineer, S-04) + ダッシュ回避配線 (S-07).
// Reads Move/Dash from InputLayer/GameInput each frame, drives Systems/PlayerMovement (pure XZ math +
// arena clamp) and Systems/DashSystem (pure direction-priority + timer arithmetic), and applies the
// result to transform.position with delta-time scaling. Thin by design (rule: Components はライフサイクル
// と配線のみ) — all movement/dash math lives in Systems/PlayerMovement and Systems/DashSystem.
using System.Collections.Generic;
using ForgeGame.InputLayer;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>
        /// Convenience lookup for later stories (enemy AI / auto-attack / health targeting the
        /// player transform) — mirrors the SessionHolder singleton pattern already used in this
        /// codebase. Set in Awake, cleared in OnDestroy.
        /// </summary>
        public static PlayerController Instance { get; private set; }

        /// <summary>True while a dash's DASH_DURATION window is active (movement input ignored —
        /// gdd 操作仕様: 「ダッシュ中は移動入力を無視し軌道を固定」).</summary>
        public bool IsDashing => _dashTimeRemaining > 0f;

        /// <summary>True while the DASH_INVULN_DURATION window is active. Components/HealthComponent
        /// (S-08) reads this to suppress contact damage — gdd 操作仕様/数値表: 「無敵フラグ中は被弾しない」.</summary>
        public bool IsDashInvulnerable => _invulnTimeRemaining > 0f;

        /// <summary>True once Components/HealthComponent has locked input following HP&lt;=0 (gdd
        /// 勝敗条件: 「判定した瞬間、入力をロックし死亡演出を再生」). Update() short-circuits entirely while
        /// true — no further movement/dash activation until the scene reloads (Result/リスタート).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Seconds remaining before Dash can activate again (0 = ready). Read-only
        /// exposure of the internal cooldown timer for Ui/GameHud (S-10 HUD ダッシュクールダウン残
        /// 表示) — mirrors the IsDashing/IsDashInvulnerable accessor pattern above; no logic here,
        /// just surfacing already-computed state (conventions.md role「UI は表示専任」).</summary>
        public float DashCooldownRemaining => _cooldownRemaining;

        private GameInput _input;
        private float _moveSpeed;
        private Animator _animator;

        private float _cooldownRemaining;
        private float _dashTimeRemaining;
        private float _invulnTimeRemaining;
        private Vector3 _dashDirection;
        private Vector3 _lastMoveDirection;
        private readonly List<Vector3> _enemyPositionsScratch = new List<Vector3>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate PlayerController destroyed (scene={gameObject.scene.name})");
                // Destroy はフレーム末まで遅延し、その間 Start()/Update() が _input=null のまま
                // 走って NRE を重ねる — enabled=false で以後のライフサイクルを止めてから破棄する
                enabled = false;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _input = new GameInput();
        }

        private void Start()
        {
            _input.EnableGameplay();
            _moveSpeed = ResolveMoveSpeed();
            // rule 10: GetComponentInChildren, not RequireComponent — the placeholder capsule (no
            // model integrated yet) has no Animator; the real Hero prefab (Editor/AssetIntegration)
            // carries one on its visual child. Null here is a valid "prefab not integrated" state.
            _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Called once by Components/HealthComponent the instant HP reaches 0 (gdd 勝敗条件:
        /// 入力ロック). Disables the gameplay input map (Move/Dash) and freezes Update() so no
        /// queued/buffered input can act during or after the death sequence.</summary>
        public void LockInput()
        {
            IsLocked = true;
            _input.DisableGameplay();
        }

        private void Update()
        {
            if (IsLocked)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector2 inputVector = _input.Move.ReadValue<Vector2>();

            // rule 13: animator gets a value only (SetFloat) — the Idle<->Run transition/threshold
            // itself lives in the generated AnimatorController asset (GameConfig.Animation.SpeedParam).
            if (_animator != null)
            {
                _animator.SetFloat(GameConfig.Animation.SpeedParam, inputVector.magnitude);
            }

            TickDashTimers(deltaTime);
            TryActivateDash(inputVector);

            Vector3 displacement;
            if (IsDashing)
            {
                displacement = DashSystem.ComputeDisplacement(_dashDirection, GameConfig.Player.DashSpeed, deltaTime);
            }
            else
            {
                displacement = PlayerMovement.ComputeDisplacement(inputVector, _moveSpeed, deltaTime);
                if (inputVector.sqrMagnitude > 0f)
                {
                    _lastMoveDirection = new Vector3(inputVector.x, 0f, inputVector.y).normalized;
                }
            }

            Vector3 next = transform.position + displacement;
            transform.position = PlayerMovement.ClampToArena(next, GameConfig.Player.ArenaRadius);
        }

        private void TickDashTimers(float deltaTime)
        {
            _cooldownRemaining = DashSystem.TickTimer(_cooldownRemaining, deltaTime);
            _dashTimeRemaining = DashSystem.TickTimer(_dashTimeRemaining, deltaTime);
            _invulnTimeRemaining = DashSystem.TickTimer(_invulnTimeRemaining, deltaTime);
        }

        /// <summary>
        /// Activates a dash on the frame Space is pressed, if the cooldown has elapsed (gdd 操作仕様:
        /// 「クールダウン中は無反応（入力バッファなし）」— a press during cooldown is simply dropped, never
        /// queued/buffered). Direction priority is delegated to Systems/DashSystem.ResolveDashDirection
        /// (conventions.md §4: dash 方向は hero の facing に連動しない — this method never reads or writes
        /// any facing/rotation state).
        /// </summary>
        private void TryActivateDash(Vector2 inputVector)
        {
            if (!_input.Dash.WasPressedThisFrame())
            {
                return;
            }
            if (!DashSystem.CanActivate(_cooldownRemaining) || IsDashing)
            {
                return;
            }

            bool hasNearestEnemy = TryGetNearestEnemyDirection(out Vector3 nearestEnemyDirection);
            Vector3 direction = DashSystem.ResolveDashDirection(
                inputVector, _lastMoveDirection, hasNearestEnemy, nearestEnemyDirection);
            if (direction.sqrMagnitude <= 0f)
            {
                // No viable direction (never moved yet and no enemy on the field to move away
                // from) — don't consume the cooldown for a dash that would go nowhere.
                return;
            }

            _dashDirection = direction;
            _dashTimeRemaining = GameConfig.Player.DashDuration;
            _invulnTimeRemaining = GameConfig.Player.DashInvulnDuration;
            _cooldownRemaining = GameConfig.Player.DashCooldown;

            // SFX-02 (ダッシュ発動) — only on an actually-activated dash (this method already returned
            // early above for CD-blocked/no-direction presses, so reaching here means the dash is real).
            if (SfxLibrary.Instance != null)
            {
                SfxLibrary.Instance.Play(SfxLibrary.Instance.Dash);
            }
        }

        /// <summary>Direction from this transform to the nearest live enemy, unbounded range (gdd
        /// 操作仕様 priority (3) is「最寄り敵（自動攻撃の対象）の反対方向」— not range-gated to
        /// AUTO_ATTACK_RANGE, since the player may need to dash away from an approaching enemy still
        /// outside that range). Reuses Systems/AutoAttackSystem's nearest-search rather than
        /// duplicating it (式の再実装禁止 pattern already used for MetaProgression.Effective*).
        /// float.PositiveInfinity as maxRange is a deliberate "unbounded" sentinel, not a magic
        /// number — squaring it stays a well-defined +Infinity, so every candidate qualifies.</summary>
        private bool TryGetNearestEnemyDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            List<EnemyAgent> enemies = EnemyAgent.ActiveEnemies;
            if (enemies.Count == 0)
            {
                return false;
            }
            _enemyPositionsScratch.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                _enemyPositionsScratch.Add(enemies[i].transform.position);
            }
            int nearestIndex = AutoAttackSystem.FindNearestIndex(
                transform.position, _enemyPositionsScratch, float.PositiveInfinity);
            if (nearestIndex == AutoAttackSystem.NoTarget)
            {
                return false;
            }
            direction = _enemyPositionsScratch[nearestIndex] - transform.position;
            return direction.sqrMagnitude > 0f;
        }

        /// <summary>
        /// Game 初期化時のアップグレード反映 (conventions.md §5): effectiveMoveSpeed は
        /// MetaProgression.EffectiveMoveSpeed で算出する（式の再実装禁止）。SessionHolder が無い
        /// （Boot を経由せず Game を単独ロードした場合。通常プレイは常に Boot 経由）場合は
        /// upgradeMoveSpeedLevel=0 相当の GameConfig.Player.MoveSpeed（Lv0 基準値）へフォールバック
        /// する（数値上 MetaProgression.EffectiveMoveSpeed(0) と等価）。
        /// </summary>
        private static float ResolveMoveSpeed()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.Save != null)
            {
                return MetaProgression.EffectiveMoveSpeed(SessionHolder.Instance.Save.upgradeMoveSpeedLevel);
            }
            Debug.LogWarning("[Wiring] SessionHolder missing at Game (not loaded via Boot, or session state lost); using base PLAYER_MOVE_SPEED (Lv0)");
            return GameConfig.Player.MoveSpeed;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            _input?.Dispose();
        }
    }
}
