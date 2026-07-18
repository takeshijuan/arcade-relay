// AutoAttackDriver — Game シーンの自動攻撃配線 (gameplay-engineer, S-06/S-09). Every AUTO_ATTACK_INTERVAL
// seconds (delta-time accumulated, rule 2), searches Components/EnemyAgent.ActiveEnemies for the
// nearest live enemy within AUTO_ATTACK_RANGE (via Systems/AutoAttackSystem — pure XZ nearest-search)
// and applies an instant hit for effectiveAttackDamage (gdd 自動攻撃: 瞬間ヒット、飛翔時間なし). No input
// is read here — gdd 操作仕様: 「（自動攻撃）入力なし。発動間隔タイマーで最寄りの敵1体へ自動発動」/
// 「攻撃ボタンの割当自体が存在しない」（P-02の中核）; InputLayer/GameInput accordingly exposes no Attack
// action. Thin by design (rule: Components はライフサイクルと配線のみ) — nearest-search math lives in
// AutoAttackSystem; damage/death bookkeeping lives on EnemyAgent (EntityState.ApplyDamage in
// Types.cs) so HealthSystem reuses the same pure reducer for player HP. On a killing hit, reports the
// kill to Components/RunStatsTracker and drops crystals via Components/CrystalPickup.SpawnDrop at the
// kill position (gdd 行為→点数 / クリスタル ドロップ＆回収; P-04; S-09).
// S-17: also Instantiates the IMG-04 hit-VFX prefab (Editor/AssetIntegration.BuildHitVfxPrefab —
// ParticleSystem with stopAction=Destroy, so this component owns no VFX lifetime/cleanup logic) at the
// target's position, in the same landed-hit branch as the SFX-01 play + ANM-01 trigger (gdd 決定: VFX/
// SFX/animation all fire together the frame a hit lands). The attack clip's playback speed itself is
// baked into the generated Hero.controller's Attack state at Editor/AssetIntegration.BuildHeroController
// time (Systems/AutoAttackSystem.ComputeAttackAnimSpeedScale) — this component still only SetTriggers.
// S-32: the killed EnemyAgent itself now owns the kill-pop visual + camera-nudge trigger (BeginKillPop) —
// this component's only S-32-relevant addition is the Start-time ArenaCameraRig wiring-break log (see
// Start() below), since it's the single per-run driver instance that ultimately causes kills.
using System.Collections.Generic;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class AutoAttackDriver : MonoBehaviour
    {
        /// <summary>Test-only override for GameConfig.Player.AutoAttackInterval (CR-CODE S-16 iter2 minor
        /// finding #2's regression test). Null = use the GameConfig constant (default, production
        /// behavior). Lets a PlayMode test force many attack ticks to fall inside a short window (e.g. the
        /// death fade sequence) without needing to precisely prime the real timer's phase beforehand.
        /// Mirrors Components/HealthComponent.SaveDirectoryOverrideForTests — a static override hook, not
        /// a new gameplay path.</summary>
        public static float? AttackIntervalOverrideForTests;

        /// <summary>Test-observability accessor: total number of times TryAttackNearest has actually
        /// landed an attack (SetTrigger + SFX + damage) during this component's lifetime. Mirrors
        /// Components/WaveSpawner.SpawnCallCountForTests.</summary>
        public int AttackCallCountForTests { get; private set; }

        [Tooltip("Hit VFX prefab (IMG-04 Particle System), assigned by Editor/AssetIntegration." +
                 "BuildHitVfxPrefab once integrated (GameConfig.AssetKeys.HitVfxPrefab). Mirrors " +
                 "Components/WaveSpawner.enemyPrefab's SerializeField+Editor-patch pattern.")]
        [SerializeField] private GameObject _hitVfxPrefab;

        /// <summary>Test-observability accessor (mirrors Components/WaveSpawner.EnemyPrefabForTests) —
        /// PlayMode tests assert the hit VFX prefab was actually assigned by Editor/AssetIntegration.</summary>
        public GameObject HitVfxPrefabForTests => _hitVfxPrefab;

        private readonly List<Vector3> _candidatePositions = new List<Vector3>();
        private float _timer;
        private int _attackDamage;
        private Animator _animator;

        // Ship-review perf fix: free-list pools (Components/GameObjectPool) for the two per-kill/
        // per-hit churn surfaces this driver owns — the IMG-04 hit-VFX instances (previously one
        // Instantiate + stopAction=Destroy per landed hit) and the crystal-drop cubes (previously one
        // CreatePrimitive per drop + Destroy per pickup/expiry, spawned via CrystalPickup.SpawnDrop
        // below). Instance fields, NOT static (GameObjectPool's header contract): this driver is a
        // Game-scene resident, so both pools and their retained objects die with the scene.
        private readonly GameObjectPool _hitVfxPool = new GameObjectPool();
        private readonly GameObjectPool _crystalDropPool = new GameObjectPool();

        private void Start()
        {
            _attackDamage = ResolveAttackDamage();
            // rule 10: GetComponentInChildren, not RequireComponent — the placeholder capsule (no
            // model integrated yet) has no Animator; the real Hero prefab (Editor/AssetIntegration)
            // carries one on its visual child. Null here is a valid "prefab not integrated" state.
            _animator = GetComponentInChildren<Animator>();

            // Rule 12 (縮退が正当なケースの分岐 — CR-CODE S-17 iter1 major finding fix): unlike _animator
            // (legitimately null pre-3D-integration), a null _hitVfxPrefab here used to be asserted as
            // "wiring is broken" via LogError. That contradicted the Editor side of this exact feature:
            // Editor/AssetIntegration.BuildHitVfxPrefab and its caller PatchAutoAttackDriverVfx both treat
            // a missing IMG-04 texture, a missing URP particle shader, or a failed prefab-save as
            // [DEGRADED]-and-continue (exit 0), not Fail() — the hit VFX is a presentation-only layer on
            // top of the acceptance-critical damage/SFX/animation effects, so one missing/broken asset
            // must not block the rest of Game scene wiring. Given that documented Editor-side contract,
            // this is a *documented legitimate degradation*, not a broken-wiring assertion: stays a
            // one-time LogWarning (not LogError), and the message enumerates the actual possible causes
            // instead of asserting a single definite one.
            if (_hitVfxPrefab == null && !string.IsNullOrEmpty(GameConfig.AssetKeys.HitVfxPrefab))
            {
                Debug.LogWarning("[Wiring] AutoAttackDriver._hitVfxPrefab is unassigned even though " +
                    "GameConfig.AssetKeys.HitVfxPrefab points at '" + GameConfig.AssetKeys.HitVfxPrefab +
                    "' — attacks will land without a hit VFX. Possible causes: IMG-04 texture or the URP " +
                    "particle shader was missing at asset-integration time (Editor/AssetIntegration." +
                    "BuildHitVfxPrefab logs [DEGRADED] and skips prefab creation in that case), the prefab " +
                    "save step failed, or Editor/AssetIntegration.IntegrateAll simply hasn't been run yet " +
                    "for this scene.");
            }

            // S-32 (撃破インパクト演出): mirrors Components/HealthComponent.Start's identical check for the
            // same ArenaCameraRig.Instance dependency (near-miss shake) — this driver is the single
            // per-run instance that ultimately triggers kills (via EnemyAgent.BeginKillPop), so it owns the
            // one-time wiring-break log rather than each individually-spawned EnemyAgent logging it (which
            // would spam the console under LogAssert.NoUnexpectedReceived() — see BeginKillPop's comment).
            if (ArenaCameraRig.Instance == null)
            {
                Debug.LogError("[Wiring] AutoAttackDriver: ArenaCameraRig.Instance missing at Start; enemy-kill camera nudge will not play for this run");
            }
        }

        private void Update()
        {
            // CR-CODE S-16 iter1 major finding: once HealthComponent starts the death sequence it locks
            // PlayerController.IsLocked (same input-lock gate PlayerController.Update() itself checks),
            // but AutoAttackDriver had no equivalent gate — a dying player standing in an enemy's melee
            // range would still fire an auto-attack (trigger + SFX + a possible kill) mid-fade, fighting
            // Components/HeroFxController's Idle-only death fx (SetFloat(Speed,0) getting immediately
            // overridden by an AnyState->Attack transition). Gate here rather than in HeroFxController —
            // this is the same input-lock condition PlayerController already enforces for movement/dash,
            // not a new concept.
            if (PlayerController.Instance != null && PlayerController.Instance.IsLocked)
            {
                return;
            }

            float interval = AttackIntervalOverrideForTests ?? GameConfig.Player.AutoAttackInterval;
            _timer += Time.deltaTime;
            if (_timer < interval)
            {
                return;
            }
            // Subtract (not reset to 0) so a slightly-late frame doesn't lose the remainder —
            // mirrors WaveSpawner's spawn-timer accumulation pattern.
            _timer -= interval;
            TryAttackNearest();
        }

        private void TryAttackNearest()
        {
            List<EnemyAgent> enemies = EnemyAgent.ActiveEnemies;
            _candidatePositions.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                _candidatePositions.Add(enemies[i].transform.position);
            }

            int nearestIndex = AutoAttackSystem.FindNearestIndex(
                transform.position, _candidatePositions, GameConfig.Player.AutoAttackRange);
            if (nearestIndex == AutoAttackSystem.NoTarget)
            {
                return;
            }

            EnemyAgent target = enemies[nearestIndex];
            // Capture position + kind before the kill: a killing hit no longer Destroy()s the target's
            // GameObject synchronously/end-of-frame (S-32 kill-pop juice) — ApplyAutoAttackDamage now
            // starts EnemyAgent.BeginKillPop and the actual Destroy() only happens
            // GameConfig.Fx.EnemyKillPopDurationS later via EnemyAgent.TickKillPop. The target GameObject
            // (and transform.position/Kind) therefore stays valid well past this call either way, but
            // reads still happen up front here rather than after ApplyAutoAttackDamage to keep this site
            // explicit about ordering instead of relying on the pop's lifetime.
            Vector3 targetPosition = target.transform.position;
            EnemyKind targetKind = target.Kind;
            AttackCallCountForTests++;
            bool killed = target.ApplyAutoAttackDamage(_attackDamage);

            // VFX (IMG-04) + SFX-01 + ANM-01 (gdd 自動攻撃「瞬間ヒット」表現・S-17 VFX/SFX同期): every
            // landed hit, not just kills — the attack connected either way. rule 13: animator is driven
            // with SetTrigger only, the idle/run<->attack state graph (and the attack clip's speed
            // scaling — Systems/AutoAttackSystem.ComputeAttackAnimSpeedScale) lives in the generated
            // AnimatorController asset.
            if (_hitVfxPrefab != null)
            {
                SpawnHitVfx(targetPosition);
            }
            if (_animator != null)
            {
                _animator.SetTrigger(GameConfig.Animation.AttackTrigger);
            }
            if (SfxLibrary.Instance != null)
            {
                SfxLibrary.Instance.Play(SfxLibrary.Instance.AttackHit);
            }

            if (!killed)
            {
                return;
            }
            RegisterKillAndDropCrystals(targetPosition, targetKind);
        }

        /// <summary>Ship-review perf fix: rents a finished hit-VFX instance from _hitVfxPool before
        /// falling back to Instantiate. First-ever spawn (and any spawn while every pooled instance is
        /// still playing) Instantiates exactly as before, then reroutes the instance's prefab-baked
        /// stopAction=Destroy to a pool Return (Components/PooledVfxReturner.Attach); re-rents just
        /// reposition + SetActive(true), which replays the burst via the prefab's playOnAwake.</summary>
        private void SpawnHitVfx(Vector3 position)
        {
            GameObject vfx = _hitVfxPool.Rent();
            if (vfx != null)
            {
                vfx.transform.SetPositionAndRotation(position, Quaternion.identity);
                vfx.SetActive(true);
                return;
            }
            vfx = Instantiate(_hitVfxPrefab, position, Quaternion.identity);
            PooledVfxReturner.Attach(vfx, _hitVfxPool);
        }

        // S-09/S-14: 撃破時のクリスタルドロップ+スコア加算 (gdd 行為→点数 / クリスタル ドロップ＆回収 /
        // 敵・障害物). isHeavy is derived from the killed EnemyAgent's Kind (set by WaveSpawner via
        // Systems/HeavyEnemySystem.ShouldSpawnHeavy at spawn time) — RunStatsTracker.RegisterKill already
        // applies SCORE_PER_KILL_NORMAL/HEAVY internally, so this just picks the matching crystal drop
        // count (CRYSTAL_DROP_PER_KILL_NORMAL/HEAVY). Instance method (was static) since the ship-review
        // perf fix: the crystal drop now rides this driver's per-scene _crystalDropPool.
        private void RegisterKillAndDropCrystals(Vector3 killPosition, EnemyKind kind)
        {
            bool isHeavy = kind == EnemyKind.HeavySwarmer;
            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.RegisterKill(isHeavy);
            }
            else
            {
                Debug.LogError("[Wiring] AutoAttackDriver: RunStatsTracker.Instance is null at kill — kill/score not recorded");
            }
            int dropCount = isHeavy ? GameConfig.Crystal.DropPerKillHeavy : GameConfig.Crystal.DropPerKillNormal;
            CrystalPickup.SpawnDrop(killPosition, dropCount, _crystalDropPool);
        }

        /// <summary>Game 初期化時のアップグレード反映 (conventions.md §5): effectiveAttackDamage は
        /// MetaProgression.EffectiveAttackDamage で算出する（式の再実装禁止）。SessionHolder が無い
        /// （Boot を経由せず Game を単独ロードした場合）場合は upgradeAttackLevel=0 相当の
        /// GameConfig.Player.AutoAttackDamageBase（Lv0 基準値）へフォールバックする（PlayerController.
        /// ResolveMoveSpeed と同じフォールバック方針）。</summary>
        private static int ResolveAttackDamage()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.Save != null)
            {
                return Mathf.RoundToInt(
                    MetaProgression.EffectiveAttackDamage(SessionHolder.Instance.Save.upgradeAttackLevel));
            }
            Debug.LogWarning(
                "[Wiring] SessionHolder missing at Game (not loaded via Boot, or session state lost); using base AUTO_ATTACK_DAMAGE_BASE (Lv0)");
            return GameConfig.Player.AutoAttackDamageBase;
        }
    }
}
