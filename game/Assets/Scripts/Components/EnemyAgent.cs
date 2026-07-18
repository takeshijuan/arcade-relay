// EnemyAgent — runtime state + per-frame approach driver for a single spawned enemy (gdd 敵・障害物:
// スウォーマー; S-05). Thin by design (rule: Components はライフサイクルと配線のみ) — all approach math
// lives in Systems/EnemyApproachSystem, and HP arithmetic lives in Types.cs EntityState.ApplyDamage
// (pure reducer). WaveSpawner calls Initialize() with the wave-derived speed/HP
// (Systems/WaveSpawnSystem.WaveParameters) right after Instantiate. Registers itself in
// ActiveEnemies so WaveSpawner can enforce MAX_CONCURRENT_ENEMIES without a scene-wide
// FindObjectsByType scan every frame. Components/AutoAttackDriver (S-06) reads ActiveEnemies each
// tick to find/apply an instant-hit auto-attack via ApplyAutoAttackDamage below.
//
// S-21 (MDL-02 静的メッシュ統合 + 接近コードモーション): this root transform is exclusively owned by
// Systems/EnemyApproachSystem for world-space position. The optional "Visual" child (built by
// Editor/AssetIntegration.BuildSwarmerPrefab — GameConfig.Enemy.VisualChildName) instead gets the coded
// forward-lean tilt + up/down bounce from Systems/EnemyVisualMotionSystem, so the two never fight over
// the same transform. Enemy prefabs without a "Visual" child (e.g. WaveSpawner's primitive-cube
// placeholder fallback before Editor/AssetIntegration has run) simply skip the coded motion.
//
// S-14 (ヘヴィスウォーマー変種): no new AI/geometry (conventions.md §7: 「MDL-02 プレハブ複製＋マテリアル
// 差し替えのみ」) — Kind is a plain tag set by Initialize(), and the only heavy-specific behavior owned by
// this class is ApplyHeavyTint (material swap). Stat multipliers (speed/HP/contact damage/drop/score) are
// derived by Systems/HeavyEnemySystem and applied by the callers that already own that arithmetic
// (Components/WaveSpawner for speed/HP at spawn time, Components/HealthComponent for contact damage,
// Components/AutoAttackDriver+RunStatsTracker+CrystalPickup for kill score/drop count).
//
// S-32 (gdd P-02「照準ゼロの自動攻撃」/P-03「群れ密度の圧力」撃破インパクト演出): a confirmed killing hit no
// longer Destroys the GameObject synchronously. ApplyAutoAttackDamage instead starts a short "pop"
// sequence (BeginKillPop) — the enemy is immediately removed from ActiveEnemies (so it stops counting
// toward MAX_CONCURRENT_ENEMIES, stops being targetable by AutoAttackDriver, and stops dealing contact
// damage in HealthComponent) and immediately fires the ArenaCameraRig kill-nudge, but the GameObject and
// its Renderer keep existing (scaling up via Systems/EnemyKillPopSystem) until GameConfig.Fx.
// EnemyKillPopDurationS elapses, at which point Update() actually calls Destroy(gameObject). Score/crystal
// drop bookkeeping is untouched by this — Components/AutoAttackDriver.RegisterKillAndDropCrystals already
// runs synchronously the instant ApplyAutoAttackDamage returns true, before this timer even starts
// (acceptance: 「スコア加点・クリスタルドロップは撃破確定と同時に即座に実行し、この演出による遅延の影響を受け
// ない」). No kind-specific branch: HeavySwarmer reuses this exact same path (acceptance: 「ヘヴィ変種も同一
// ロジックを再利用し新規分岐を追加しない」).
using System.Collections.Generic;
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class EnemyAgent : MonoBehaviour
    {
        /// <summary>Live enemies, most-recently-spawned last. WaveSpawner reads Count to cap
        /// MAX_CONCURRENT_ENEMIES; a later story (S-06/S-08) will use this list for hit/contact
        /// queries (nearest-enemy search, HP application).</summary>
        public static readonly List<EnemyAgent> ActiveEnemies = new List<EnemyAgent>();

        /// <summary>Wave-derived move speed (m/s) this enemy was spawned with (gdd
        /// ENEMY_MOVE_SPEED_BASE × per-wave growth — see WaveSpawnSystem.EnemySpeed).</summary>
        public float MoveSpeed { get; private set; }

        /// <summary>Wave-derived max HP this enemy was spawned with (gdd ENEMY_HP_BASE × per-wave
        /// multiplier — see WaveSpawnSystem.EnemyHpMultiplier).</summary>
        public int MaxHp { get; private set; }

        /// <summary>Current HP snapshot (S-06 自動攻撃・敵HP・撃破), exposed for HUD/tests. Not
        /// authoritative arithmetic — Types.cs EntityState.ApplyDamage is the pure reducer; this
        /// field just holds the latest value between hits.</summary>
        public int CurrentHp => _health.Hp;

        /// <summary>Variant tag set by Initialize() (S-14 ヘヴィスウォーマー). Defaults to Swarmer for
        /// callers using the 2-arg Initialize overload (all pre-S-14 call sites — see that overload's
        /// comment). Read by Components/HealthComponent (contact damage multiplier) and
        /// Components/AutoAttackDriver (kill score/drop count multiplier via RunStatsTracker/CrystalPickup).</summary>
        public EnemyKind Kind { get; private set; }

        private EntityState _health;
        private bool _initialized;
        private bool _wiringErrorLogged;
        private bool _playerMissingErrorLogged;

        /// <summary>Optional visual child (GameConfig.Enemy.VisualChildName — S-21 renderer/motion
        /// separation). Resolved once in Awake(); null on prefabs that don't carry one (see class header).</summary>
        private Transform _visual;
        private float _bouncePhase;

        /// <summary>The visual child's own baked local rotation at spawn time (e.g. a Blender/Meshy
        /// FBX-export axis correction — Editor/AssetIntegration.BuildSwarmerPrefab deliberately does NOT
        /// reset this, see that method's comment). Captured once so ApplyVisualMotion can layer the
        /// approach-facing tilt on TOP of it (approachTilt * base) instead of overwriting it outright,
        /// which would silently un-correct the model's orientation.</summary>
        private Quaternion _visualBaseLocalRotation = Quaternion.identity;

        /// <summary>The visual child's own baked local position at spawn time (e.g. a pivot offset baked
        /// into the FBX root by the authoring tool). Captured for the same reason as
        /// <see cref="_visualBaseLocalRotation"/> — CR-CODE S-21 iteration 1 minor finding: ApplyVisualMotion
        /// used to overwrite localPosition outright (`new Vector3(0, bounceOffsetY, 0)`), which is fine for
        /// today's MDL-02 (baked local position is (0,0,0)) but would silently snap a future rigged
        /// replacement model to the origin on its first frame if that model's FBX root carries a non-zero
        /// pivot offset — exactly the drop-in-replacement seam this class's header comment promises. Layering
        /// `base + up * bounceOffsetY` (mirroring the rotation composition) preserves that seam.</summary>
        private Vector3 _visualBaseLocalPosition = Vector3.zero;

        /// <summary>Set by ApplyHeavyTint when this instance is a HeavySwarmer; null for normal
        /// Swarmers (ApplyHeavyTint never runs — see Initialize). Released in OnDestroy() — CR-CODE
        /// S-14 iter1 minor finding, see ApplyHeavyTint's comment — and, for pooled instances, in
        /// RestoreHeavyTintMaterial at pool-return time so a heavy life's tint never bleeds into a
        /// later normal-Swarmer life of the same GameObject (ship-review perf fix).</summary>
        private Material _heavyTintMaterialInstance;

        /// <summary>Ship-review perf fix (free-list pooling): the renderer ApplyHeavyTint recolored and
        /// the sharedMaterial it displaced, captured so RestoreHeavyTintMaterial can undo the swap when
        /// this instance is returned to the pool. Both stay null for lives that never ran ApplyHeavyTint.</summary>
        private Renderer _heavyTintRenderer;
        private Material _heavyTintOriginalSharedMaterial;

        /// <summary>Ship-review perf fix: set by Components/WaveSpawner.SpawnOne (SetPool) for
        /// pool-managed enemies. Null for manually-created enemies (PlayMode tests' AddComponent path),
        /// which keep the original deferred-Destroy lifetime — see TickKillPop.</summary>
        private GameObjectPool _pool;

        /// <summary>S-32 (撃破インパクト演出): true from the confirmed killing hit (BeginKillPop) until
        /// the deferred Destroy(gameObject) at the end of the pop, exposed for tests/HUD-adjacent
        /// observers. Note this enemy is already removed from ActiveEnemies the instant this becomes
        /// true (see class header) — it is a "dead but still visually popping" state, not a live one.</summary>
        public bool IsPopping => _popping;

        /// <summary>S-32 test-observability accessor: the pop visual's current localScale (the transform
        /// Systems/EnemyKillPopSystem's scale is actually applied to — the "Visual" child if present,
        /// otherwise this GameObject's own transform, mirroring ApplyVisualMotion's same _visual-or-root
        /// choice). Undefined (returns the un-popped identity-relative scale) before a kill.</summary>
        public Vector3 PopVisualLocalScaleForTests => (_visual != null ? _visual : transform).localScale;

        private bool _popping;
        private float _popElapsed;
        private Vector3 _popBaseLocalScale;

        private void Awake()
        {
            _visual = transform.Find(GameConfig.Enemy.VisualChildName);
            if (_visual != null)
            {
                _visualBaseLocalRotation = _visual.localRotation;
                _visualBaseLocalPosition = _visual.localPosition;
            }
            else
            {
                // CR-CODE S-21 iter2 minor指摘#6: documented degradation (class header — prefabs without a
                // "Visual" child, e.g. WaveSpawner's primitive-cube placeholder fallback before
                // Editor/AssetIntegration has run, legitimately skip the coded motion), but a real
                // swarmer/hero-style prefab losing its "Visual" child to a future AssetIntegration
                // regression would otherwise drop the approach tilt/bounce with zero log trace. Awake()
                // runs exactly once per instance, so this is already one-shot without a separate logged
                // flag (unlike the per-frame Update() wiring guards below).
                Debug.Log("[AssetIntegration] EnemyAgent '" + name + "': no \"" + GameConfig.Enemy.VisualChildName +
                    "\" child found — coded approach motion (ApplyVisualMotion) will no-op for this enemy.");
            }
        }

        /// <summary>Set by WaveSpawner immediately after Instantiate (before this object's first
        /// Update runs). Not done in Awake because the wave parameters aren't known until spawn time.
        /// Overload kept for existing (pre-S-14) call sites/tests that only deal with the normal
        /// Swarmer — equivalent to Initialize(moveSpeed, maxHp, EnemyKind.Swarmer).</summary>
        public void Initialize(float moveSpeed, int maxHp)
        {
            Initialize(moveSpeed, maxHp, EnemyKind.Swarmer);
        }

        /// <summary>S-14 ヘヴィスウォーマー変種: same as the 2-arg overload, plus tags this instance's
        /// <see cref="Kind"/> and (for HeavySwarmer) swaps its Renderer's material to the heavy variant's
        /// tint color (gdd/conventions.md §7: MDL-02 プレハブ複製＋マテリアル差し替えのみ、新規ジオメトリ
        /// 生成禁止). WaveSpawner supplies moveSpeed/maxHp already multiplied by
        /// Systems/HeavyEnemySystem.AdjustedSpeed/AdjustedHp when kind is HeavySwarmer.</summary>
        public void Initialize(float moveSpeed, int maxHp, EnemyKind kind)
        {
            MoveSpeed = moveSpeed;
            MaxHp = maxHp;
            Kind = kind;
            _health = new EntityState(maxHp, maxHp);
            _initialized = true;
            // Ship-review perf fix: pooled re-rents reuse this exact reseed point, so the coded-motion
            // phase must restart with the life it belongs to (0 → 0 no-op on a fresh instance).
            _bouncePhase = 0f;
            if (kind == EnemyKind.HeavySwarmer)
            {
                ApplyHeavyTint();
            }
        }

        /// <summary>Ship-review perf fix: called by Components/WaveSpawner.SpawnOne right after
        /// spawn/rent. A pooled enemy returns itself to <paramref name="pool"/> at kill-pop end
        /// (ReturnToPool) instead of the original Destroy(gameObject); enemies that never get a pool
        /// (manually-created test enemies) keep the Destroy path unchanged.</summary>
        public void SetPool(GameObjectPool pool)
        {
            _pool = pool;
        }

        /// <summary>Clones (does not mutate) the Renderer's material so recoloring this instance doesn't
        /// also recolor every other enemy sharing the same sharedMaterial asset (both the placeholder-cube
        /// path and the real MDL-02 prefab share one material asset across all spawned instances). Mirrors
        /// WaveSpawner.ApplyPlaceholderColor/CrystalPickup.ApplyPlaceholderColor's URP-safe shader fallback
        /// for the case where no sharedMaterial exists yet.</summary>
        private void ApplyHeavyTint()
        {
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                // Wiring guard (rule 12): every current spawn path (WaveSpawner's CreatePrimitive(Cube)
                // placeholder and the MDL-02 prefab AssetIntegration guarantees a Renderer on) always
                // carries a Renderer, so reaching here means the spawn wiring regressed, not a valid
                // degraded state (mirrors WaveSpawner.SpawnOne's enemyPrefab-missing-EnemyAgent guard
                // above) — CR-CODE S-14 iter1 major finding: this used to be LogWarning, which QA-PLAY's
                // LogAssert.NoUnexpectedReceived() doesn't fail on, letting a heavy variant silently ship
                // looking identical to a normal one.
                Debug.LogError("[Wiring] EnemyAgent '" + name + "': heavy variant material swap skipped — no Renderer found.");
                return;
            }
            if (!ColorUtility.TryParseHtmlString(GameConfig.Ui.ColorHeavyEnemyTint, out Color tint))
            {
                // Wiring guard (rule 12): ColorHeavyEnemyTint is a compile-time constant ("#6B1030" —
                // GameConfig.Ui), so a parse failure can only mean that constant itself was edited into
                // an invalid hex string — a wiring/config regression, not runtime-recoverable input.
                // CR-CODE S-14 iter1 major finding: escalated from LogWarning for the same
                // LogAssert.NoUnexpectedReceived()-blind-spot reason as the Renderer-missing guard above.
                Debug.LogError("[Wiring] EnemyAgent: heavy variant material swap skipped — GameConfig hex parse failed.");
                return;
            }
            // Ship-review perf fix: remember what this swap displaces so a pool return can undo it
            // (RestoreHeavyTintMaterial) — captured before the branch below so the original is the
            // pre-tint sharedMaterial (possibly null on the CreateFallbackMaterial path, restored
            // as-is), never a previous heavy life's instance (that one was already restored/released
            // at its own return).
            Renderer tintRenderer = renderer;
            Material originalSharedMaterial = renderer.sharedMaterial;
            Material material = renderer.sharedMaterial != null
                ? new Material(renderer.sharedMaterial)
                : CreateFallbackMaterial();
            if (material == null)
            {
                return;
            }
            material.color = tint;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", tint);
            }
            renderer.material = material;
            // CR-CODE S-14 iter1 minor finding: track the instanced Material so OnDestroy() can release
            // it — renderer.material (unlike sharedMaterial) always hands back/assigns a per-instance
            // copy that Destroy(gameObject) does NOT free on its own, so without this every heavy
            // spawn+kill leaks one Material for the run's lifetime.
            _heavyTintMaterialInstance = material;
            _heavyTintRenderer = tintRenderer;
            _heavyTintOriginalSharedMaterial = originalSharedMaterial;
        }

        private static Material CreateFallbackMaterial()
        {
            // Ship-review dedup: shared URP/Lit→Standard resolution (Components/UrpShaderUtil). The
            // fallback-attempt LogWarning lives in the helper (legitimate degraded-but-working path,
            // rule 12's allowance — URP/Lit absent doesn't by itself mean the project is broken;
            // Standard may still resolve fine); only the terminal neither-shader error stays here.
            Shader shader = UrpShaderUtil.FindLitOrStandard(
                "[Wiring] URP/Lit shader not found; falling back to Standard (pink InternalErrorShader risk under URP).");
            if (shader == null)
            {
                // CR-CODE S-14 iter2 minor finding: unlike the URP/Lit-only miss above, neither shader
                // resolving means material creation cannot succeed at all — every current call site
                // (renderer.sharedMaterial == null) is itself already a documented-fallback branch, so
                // reaching here on top of that is a real asset/config regression (e.g. shader stripping
                // in a player build), not a recoverable degraded state. Same LogAssert.NoUnexpectedReceived()
                // blind-spot reasoning as ApplyHeavyTint's own two LogError guards above.
                Debug.LogError("[Wiring] EnemyAgent: heavy variant material swap skipped — neither URP/Lit nor Standard shader found.");
                return null;
            }
            return new Material(shader);
        }

        /// <summary>Applies an instant-hit auto-attack (gdd 自動攻撃: 瞬間ヒット、着弾までの飛翔時間なし)
        /// via the pure EntityState.ApplyDamage reducer. Once HP reaches 0 (撃破 — gdd 敵・障害物:
        /// 「自身はプレイヤーの自動攻撃でHPが0になるまで消滅しない」), starts the S-32 kill-pop sequence
        /// (BeginKillPop) instead of destroying immediately — the GameObject itself is Destroy()'d once
        /// GameConfig.Fx.EnemyKillPopDurationS elapses (TickKillPop), but the enemy is already excluded
        /// from ActiveEnemies (no longer live/targetable/damaging) the instant this returns true. Returns
        /// true when this hit killed the enemy. Components/AutoAttackDriver calls this after
        /// Systems/AutoAttackSystem.FindNearestIndex picks this enemy as the nearest in-range target.</summary>
        public bool ApplyAutoAttackDamage(int damage)
        {
            if (!_initialized)
            {
                // Wiring guard (rule 12): the current call graph (WaveSpawner calls Initialize()
                // synchronously right after Instantiate, before AutoAttackDriver can observe this
                // enemy via ActiveEnemies) makes this unreachable today, but without this guard a
                // future regression would hit ApplyDamage against default(EntityState) (Hp=0),
                // report a phantom kill via `return true`, and Destroy a never-initialized enemy —
                // silently inflating kill counts. Surface it instead of pretending success.
                Debug.LogError("[Wiring] EnemyAgent.ApplyAutoAttackDamage called before Initialize().");
                return false;
            }
            if (_health.IsDead)
            {
                // Already dead (e.g. re-entrant call within the same frame before Destroy takes
                // effect): report no-kill rather than a duplicate true, which would double-count
                // kills for callers that increment a score/kill counter per `true` return.
                return false;
            }
            _health = _health.ApplyDamage(damage);
            if (!_health.IsDead)
            {
                return false;
            }
            BeginKillPop();
            return true;
        }

        /// <summary>S-32: starts the kill-pop visual sequence (gdd P-02/P-03 撃破インパクト演出) in place of
        /// the immediate Destroy() this used to call. Removes this enemy from ActiveEnemies right away
        /// (rule: the kill is already confirmed — it must stop counting toward MAX_CONCURRENT_ENEMIES,
        /// stop being targetable by AutoAttackDriver, and stop dealing contact damage via HealthComponent
        /// for the remainder of the pop) and fires the camera nudge, but defers the actual
        /// Destroy(gameObject) to TickKillPop once GameConfig.Fx.EnemyKillPopDurationS elapses.</summary>
        private void BeginKillPop()
        {
            _popping = true;
            _popElapsed = 0f;
            Transform popTarget = _visual != null ? _visual : transform;
            _popBaseLocalScale = popTarget.localScale;
            ActiveEnemies.Remove(this);

            if (ArenaCameraRig.Instance != null)
            {
                ArenaCameraRig.Instance.TriggerKillNudge();
            }
            // Wiring guard intentionally omitted here (unlike HealthComponent's Start-time check for the
            // same singleton): EnemyAgent instances are spawned/killed continuously throughout a run, so a
            // per-kill LogError would spam the console under LogAssert.NoUnexpectedReceived() long before a
            // human could act on it. Components/AutoAttackDriver.Start() (the single-instance driver that
            // ultimately triggers kills) owns the one-time wiring-break log for this dependency instead.
        }

        /// <summary>Ticks the kill-pop scale (Systems/EnemyKillPopSystem, delta-time 必須) and performs the
        /// deferred Destroy(gameObject) once the pop's visible duration has elapsed.</summary>
        private void TickKillPop(float deltaTime)
        {
            _popElapsed += deltaTime;
            float scale = EnemyKillPopSystem.ComputeScale(
                _popElapsed, GameConfig.Fx.EnemyKillPopDurationS, GameConfig.Fx.EnemyKillPopScaleMultiplier);
            Transform popTarget = _visual != null ? _visual : transform;
            popTarget.localScale = _popBaseLocalScale * scale;

            if (EnemyKillPopSystem.IsComplete(_popElapsed, GameConfig.Fx.EnemyKillPopDurationS))
            {
                // Ship-review perf fix: pool-managed enemies (WaveSpawner spawns) go back to the
                // free list instead of being destroyed; enemies without a pool (manually-created
                // test enemies) keep the original deferred Destroy so `enemy == null` assertions
                // and their one-off lifetimes are untouched.
                if (_pool != null)
                {
                    ReturnToPool();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>Ship-review perf fix: undoes every visual/lifecycle mutation this life applied
        /// before handing the GameObject back to the pool, so a later Rent + Initialize starts from
        /// the same state a fresh Instantiate would have: pop scale/visual offsets restored to their
        /// Awake-captured bases, heavy tint reverted (RestoreHeavyTintMaterial), pop/initialized flags
        /// cleared. Ordering matters: _popping must be false before the pool's SetActive(false) runs
        /// so the next SetActive(true)'s OnEnable re-adds this enemy to ActiveEnemies (OnEnable's
        /// mid-pop guard would otherwise skip it forever).</summary>
        private void ReturnToPool()
        {
            Transform popTarget = _visual != null ? _visual : transform;
            popTarget.localScale = _popBaseLocalScale;
            if (_visual != null)
            {
                _visual.localPosition = _visualBaseLocalPosition;
                _visual.localRotation = _visualBaseLocalRotation;
            }
            RestoreHeavyTintMaterial();
            _popping = false;
            _popElapsed = 0f;
            _initialized = false;
            // warn-once フラグも次の life へ持ち越さない: fresh Instantiate なら life ごとに1回は
            // 配線エラーが出る。pooled 再利用で握り潰すと2体目以降の欠落が無症状になる（adversarial P-2）
            _wiringErrorLogged = false;
            _playerMissingErrorLogged = false;
            _pool.Return(gameObject);
        }

        /// <summary>Ship-review perf fix: reverts ApplyHeavyTint's material swap (restores the
        /// displaced sharedMaterial and releases the per-instance tinted clone — same release
        /// OnDestroy performs for non-pooled lifetimes) so a pooled GameObject re-rented as a normal
        /// Swarmer doesn't keep the heavy tint. No-op for lives that never ran ApplyHeavyTint.</summary>
        private void RestoreHeavyTintMaterial()
        {
            if (_heavyTintMaterialInstance == null)
            {
                return;
            }
            if (_heavyTintRenderer != null)
            {
                _heavyTintRenderer.sharedMaterial = _heavyTintOriginalSharedMaterial;
            }
            Destroy(_heavyTintMaterialInstance);
            _heavyTintMaterialInstance = null;
            _heavyTintRenderer = null;
            _heavyTintOriginalSharedMaterial = null;
        }

        private void OnEnable()
        {
            // CR-CODE S-32 iteration 1 minor finding: BeginKillPop's ActiveEnemies.Remove(this) is meant
            // to be irreversible for the remainder of this instance's life (see BeginKillPop's comment —
            // "already dead but still visually popping" must never re-enter live gameplay). No current
            // spawn/despawn path disables/re-enables an EnemyAgent GameObject mid-pop, but without this
            // guard a future one silently re-adds a dead-and-popping enemy back into ActiveEnemies for up
            // to EnemyKillPopDurationS, resurrecting it as a targetable/contact-damaging/MAX_CONCURRENT_
            // ENEMIES-counted entity with zero log trace. Close the invariant here rather than leaving it
            // to only hold by the absence of such a caller today.
            if (_popping)
            {
                return;
            }
            ActiveEnemies.Add(this);
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
        }

        /// <summary>CR-CODE S-14 iter1 minor finding: releases the per-instance Material ApplyHeavyTint
        /// created (renderer.material clones are not freed by Destroy(gameObject) on their own).
        /// No-op for normal Swarmers (field stays null — ApplyHeavyTint never ran).</summary>
        private void OnDestroy()
        {
            if (_heavyTintMaterialInstance != null)
            {
                Destroy(_heavyTintMaterialInstance);
            }
        }

        private void Update()
        {
            // S-32: a popping enemy is already dead/confirmed-killed and removed from ActiveEnemies
            // (BeginKillPop) — it must not run approach movement or the wiring guards below (which assume
            // a still-live enemy), only tick its own visual scale-up toward the deferred Destroy().
            if (_popping)
            {
                TickKillPop(Time.deltaTime);
                return;
            }
            if (!_initialized)
            {
                // Wiring guard (rule 12): WaveSpawner always calls Initialize() right after
                // Instantiate, so an uninitialized agent means a wiring bug, not a valid degraded
                // state — surface it once (not every frame) rather than silently defaulting
                // MoveSpeed to 0 forever.
                if (!_wiringErrorLogged)
                {
                    Debug.LogError("[Wiring] EnemyAgent '" + name + "' updated before Initialize() was called.");
                    _wiringErrorLogged = true;
                }
                return;
            }
            if (PlayerController.Instance == null)
            {
                // Wiring guard (rule 12): PlayerController.Instance should always be set once the
                // Game scene has loaded (SceneWiring.WireGame runs before any enemy can spawn), so a
                // null instance here means the Player root is missing / a duplicate-instance guard
                // destroyed it / SceneWiring regressed — not a state this game currently allows (no
                // player-death flow yet). Surface it once rather than freezing every enemy silently
                // forever. If a future story adds player death, relax this to LogWarning and document
                // the null-during-death window here.
                if (!_playerMissingErrorLogged)
                {
                    Debug.LogError("[Wiring] EnemyAgent: PlayerController.Instance is null — Game scene wiring broken?");
                    _playerMissingErrorLogged = true;
                }
                return;
            }
            float deltaTime = Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = PlayerController.Instance.transform.position;
            Vector3 approachDirection = EnemyApproachSystem.ComputeDirection(currentPosition, targetPosition);
            transform.position = EnemyApproachSystem.ComputeStep(currentPosition, targetPosition, MoveSpeed, deltaTime);

            ApplyVisualMotion(approachDirection, deltaTime);
        }

        /// <summary>Drives the "Visual" child's coded approach motion (前傾チルト + 上下バウンス — S-21).
        /// No-op (documented degradation, rule 12 — not every enemy prefab carries a "Visual" child) when
        /// <see cref="_visual"/> wasn't found in Awake().</summary>
        private void ApplyVisualMotion(Vector3 approachDirection, float deltaTime)
        {
            if (_visual == null)
            {
                return;
            }
            _bouncePhase = EnemyVisualMotionSystem.AdvanceBouncePhase(
                _bouncePhase, GameConfig.Enemy.VisualBounceFrequencyHz, deltaTime);
            float bounceOffsetY = EnemyVisualMotionSystem.ComputeBounceOffsetY(
                _bouncePhase, GameConfig.Enemy.VisualBounceAmplitude);
            _visual.localPosition = _visualBaseLocalPosition + Vector3.up * bounceOffsetY;
            Quaternion approachTilt = EnemyVisualMotionSystem.ComputeApproachTilt(
                approachDirection, GameConfig.Enemy.VisualApproachTiltDeg);
            _visual.localRotation = approachTilt * _visualBaseLocalRotation;
        }
    }
}
