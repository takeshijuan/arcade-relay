// CrystalPickup — Game シーンのクリスタル ドロップ配線 (gameplay-engineer, S-09). Spawned by
// Components/AutoAttackDriver at the killed enemy's position (gdd: 撃破位置にCRYSTAL_DROP_PER_KILL_*個の
// クリスタルを生成). Every frame, checks CRYSTAL_PICKUP_RADIUS against Components/PlayerController.Instance
// via the pure Systems/CrystalSystem.IsWithinPickupRadius — no pickup input (gdd 操作仕様: 拾得操作なし
//自動回収, P-02 照準ゼロ思想の回収版) — and otherwise ticks its CRYSTAL_LIFETIME countdown via
// Systems/CrystalSystem.TickLifetime. On pickup, reports to Components/RunStatsTracker (counter+score)
// and destroys itself; on lifetime expiry, destroys itself without reporting (gdd: 未回収クリスタルは
// CRYSTAL_LIFETIME経過で消滅). Thin by design (rule: Components はライフサイクルと配線のみ) —
// radius/lifetime math lives in Systems/CrystalSystem; the visual placeholder (primitive cube + palette
// color, self-rotation + bob) is coded motion per gdd/art-bible「クリスタル・アリーナ環境の視覚表現方針」
// (生成MDL不要 — a real crystal MDL/prop slot is deliberately left unused, per art-bible.md 決定).
// S-29: also attaches an ambient glow-halo ParticleSystem (Components/CrystalVfxLibrary.SpawnGlow) on
// spawn and fires a short-lived collect-flash ParticleSystem (Components/CrystalVfxLibrary.SpawnCollect)
// on pickup, same frame as SFX-04 — mirrors AutoAttackDriver's S-17 VFX/SFX-same-frame pattern.
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class CrystalPickup : MonoBehaviour
    {
        // CR-CODE S-29 iter1 minor #2: CrystalVfxLibrary.Instance is looked up per-crystal (both at
        // SpawnDrop and at TryPickup), and CrystalVfxLibrary.Awake already LogErrors once when the
        // singleton exists but its prefab fields are unassigned. But if the singleton itself is missing
        // from the scene/inactive (deleted, disabled, or a scene that never ran Editor/AssetIntegration's
        // PatchCrystalVfxLibrary), Awake never runs and nothing surfaces the loss — unlike the
        // RunStatsTracker null-guard just below, which already LogErrors every time. One-shot flag so a
        // run with many crystals doesn't spam the log once the condition is known. Unlike
        // Components/WaveSpawner._prefabMissingAgentLogged / Components/SfxLibrary._mixerErrorLogged
        // (instance fields on a scene-resident singleton whose lifetime already resets the flag on
        // reload/scene-load), CrystalPickup instances are transient per-crystal (AddComponent per
        // SpawnDrop call, destroyed on pickup/expiry) — an instance field would re-log on every single
        // crystal instead of once per session, so this must be static/session-scoped instead. CR-CODE
        // S-29 iter2 minor #1 fix: a bare `static bool` has no reset path across Enter Play Mode Options
        // (domain reload disabled) editor sessions or repeated PlayMode test runs in the same process, so
        // it silently returns to "already logged" (i.e. silent again) after the first occurrence anywhere
        // in that process's lifetime. RuntimeInitializeOnLoadMethod(SubsystemRegistration) below resets it
        // on every entry into Play Mode (Unity's documented mechanism for statics when domain reload is
        // skipped — see ResetStaticStateForDomainReloadDisabled), so each session gets its own one-shot.
        private static bool _vfxLibraryMissingLogged;

        // Ship-review AUTO-FIX: ApplyPlaceholderColor used to build a brand-new `new Material(shader)`
        // per spawned crystal — every crystal is visually identical by construction (same GameConfig
        // color/emission), so the per-instance materials only accumulated (renderer.sharedMaterial
        // assignment does not tie the material's lifetime to the crystal GameObject, so pickup/expiry
        // Destroy never reclaimed them). Build the material once per session and share the cached
        // instance across all crystals. Static + SubsystemRegistration reset mirrors
        // _vfxLibraryMissingLogged above (lazily rebuilt on first spawn of each Play Mode entry).
        private static Material _placeholderMaterial;

        // CR-CODE S-29 iter2 minor #1 fix: see _vfxLibraryMissingLogged comment above. SubsystemRegistration
        // is the earliest RuntimeInitializeOnLoadMethod point, before any scene loads — fires once per
        // entry into Play Mode regardless of Enter Play Mode Options' domain-reload setting, which is what
        // makes CrystalVfxLibrary-missing PlayMode tests (Tests/PlayMode/CrystalVfxSceneTests) reliable
        // regardless of test execution order/count within the same Editor process.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForDomainReloadDisabled()
        {
            _vfxLibraryMissingLogged = false;
            _placeholderMaterial = null;
        }

        private float _remainingLifetime;
        private float _bobPhase;
        private Vector3 _basePosition;

        /// <summary>Ship-review perf fix: the free-list pool this crystal despawns into (pickup/expiry),
        /// set at creation time by the pooled SpawnDrop overload. Null for crystals spawned without a
        /// pool (the 2-arg SpawnDrop overload — PlayMode tests' direct calls), which keep the original
        /// Destroy lifetime so `crystal == null` assertions are untouched.</summary>
        private GameObjectPool _pool;

        /// <summary>Spawns <paramref name="count"/> crystal placeholders at <paramref name="position"/>
        /// (gdd: 撃破位置にドロップ). Called by Components/AutoAttackDriver right after a kill. count is
        /// always GameConfig.Crystal.DropPerKillNormal today (the optional Heavy variant isn't wired by
        /// WaveSpawner/EnemyAgent yet — see AutoAttackDriver's kill-handling comment); this loop still
        /// handles count>1 correctly for when Heavy drops (CRYSTAL_DROP_PER_KILL_HEAVY=3) are wired later.
        /// Overload kept for existing (pre-pooling) call sites/tests — equivalent to
        /// SpawnDrop(position, count, null), i.e. plain Instantiate/Destroy lifetimes.</summary>
        public static void SpawnDrop(Vector3 position, int count)
        {
            SpawnDrop(position, count, null);
        }

        /// <summary>Ship-review perf fix: pooled variant — Components/AutoAttackDriver passes its own
        /// per-scene crystal pool (Components/GameObjectPool) so pickup/expiry return the cube (with its
        /// S-29 glow child intact) to the free list instead of destroying it. Re-rented crystals skip
        /// the CreatePrimitive/material/glow build entirely (the pooled unit already carries all of it —
        /// the glow halo's playOnAwake replays on SetActive(true)) and only reseed position/rotation/
        /// lifetime/bob phase (ResetRuntimeState).</summary>
        public static void SpawnDrop(Vector3 position, int count, GameObjectPool pool)
        {
            for (int i = 0; i < count; i++)
            {
                if (pool != null && TrySpawnFromPool(pool, position))
                {
                    continue;
                }
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // Ship-review AUTO-FIX: CreatePrimitive attaches a BoxCollider, but this game has zero
                // physics — contact/pickup checks are all pure XZ-distance math in Systems/
                // (Editor/AssetIntegration.PatchPlayerVisual strips the player's CapsuleCollider for
                // the same reason). Destroy it so crystals don't accumulate dead physics components.
                Object.Destroy(go.GetComponent<BoxCollider>());
                go.name = "Crystal";
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(
                    GameConfig.Crystal.PlaceholderTiltDegX, 0f, GameConfig.Crystal.PlaceholderTiltDegZ);
                go.transform.localScale = Vector3.one * GameConfig.Crystal.VisualScale;
                ApplyPlaceholderColor(go);
                // S-29: ambient glow-halo particles, parented so the emission point follows the crystal's
                // coded bob/spin (Components/CrystalVfxLibrary.SpawnGlow — no-op if the library/prefab
                // wasn't wired this run, see that method's comment).
                if (CrystalVfxLibrary.Instance != null)
                {
                    CrystalVfxLibrary.Instance.SpawnGlow(go.transform);
                }
                else
                {
                    LogVfxLibraryMissingOnce();
                }
                CrystalPickup pickup = go.AddComponent<CrystalPickup>();
                pickup._pool = pool;
            }
        }

        /// <summary>Ship-review perf fix: reuses a pooled crystal for one drop. Reposition/reseed happen
        /// BEFORE SetActive(true) (GameObjectPool's Rent contract) so the first visible frame is already
        /// at the new drop position with a fresh lifetime/bob phase. Returns false (fall back to the
        /// fresh CreatePrimitive path) when the pool is empty — or, defensively, if a pooled instance
        /// somehow lost its CrystalPickup (wiring guard, rule 12: only this class ever returns objects
        /// into the crystal pool, so that would be a pooling regression, not a valid degraded state).</summary>
        private static bool TrySpawnFromPool(GameObjectPool pool, Vector3 position)
        {
            GameObject go = pool.Rent();
            if (go == null)
            {
                return false;
            }
            CrystalPickup pickup = go.GetComponent<CrystalPickup>();
            if (pickup == null)
            {
                Debug.LogError("[Wiring] CrystalPickup: pooled crystal '" + go.name +
                    "' has no CrystalPickup component — destroying it and spawning a fresh crystal instead.");
                Object.Destroy(go);
                return false;
            }
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(
                GameConfig.Crystal.PlaceholderTiltDegX, 0f, GameConfig.Crystal.PlaceholderTiltDegZ));
            // Re-bind to the pool this rent came from: a crystal first created via a different (or
            // null) pool argument would otherwise return itself to a stale pool — or Destroy — on
            // this life's pickup/expiry, silently draining the active pool (adversarial P-3).
            pickup._pool = pool;
            pickup.ResetRuntimeState();
            go.SetActive(true);
            return true;
        }

        // Placeholder-only visual (contract §11: 3D 資産統合は S-18/S-20 相当。gdd/art-bible 決定によりク
        // リスタルは生成MDLを使わずプリミティブ+エミッシブ寄りマテリアルで恒久的に表現する). Mirrors
        // WaveSpawner/SceneWiring's URP-safe shader lookup + emission enable.
        private static void ApplyPlaceholderColor(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[Wiring] crystal placeholder color skipped: '" + go.name + "' has no Renderer.");
                return;
            }
            if (_placeholderMaterial == null)
            {
                // Ship-review AUTO-FIX: built once per session and shared across all crystals — see
                // _placeholderMaterial field comment. On build failure the warnings re-log per spawn,
                // matching the pre-cache behavior (the cache stays null, so each spawn retries).
                _placeholderMaterial = BuildPlaceholderMaterial();
                if (_placeholderMaterial == null)
                {
                    return;
                }
            }
            renderer.sharedMaterial = _placeholderMaterial;
        }

        private static Material BuildPlaceholderMaterial()
        {
            if (!ColorUtility.TryParseHtmlString(GameConfig.Ui.ColorCrystalCyan, out Color color))
            {
                Debug.LogWarning("[Wiring] crystal placeholder color skipped: GameConfig hex parse failed.");
                return null;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Wiring] URP/Lit shader not found; falling back to Standard (pink InternalErrorShader risk under URP).");
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                Debug.LogWarning("[Wiring] crystal placeholder color skipped: neither URP/Lit nor Standard shader found.");
                return null;
            }
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                // S-20: HDR-boosted emission (art-bible.md「敵とクリスタル・マゼンタの識別根拠」— クリスタ
                // ルは常時エミッシブ発光val≈0.90で敵の非発光マット面val≈0.65と輝度差で区別できる設計。
                // baseColorそのまま(HDR強度1.0相当)では輝度差が弱いためGameConfig.Crystal.EmissionIntensity
                // 倍率を掛ける). S-29: さらに GlowHaloEmissionBoost を重ねがけし、S-27 Bloom
                // (BloomThreshold=1.3)がより明瞭なハローを生む閾値超過幅を広げる（「エミッシブ発光を強化」
                // acceptance）。
                material.SetColor("_EmissionColor",
                    color * GameConfig.Crystal.EmissionIntensity * GameConfig.Crystal.GlowHaloEmissionBoost);
            }
            return material;
        }

        private void Start()
        {
            ResetRuntimeState();
        }

        /// <summary>Per-life runtime seed, shared by Start() (fresh crystal — Start only ever runs once
        /// per component, so pooled reuse cannot rely on it) and TrySpawnFromPool (re-rented crystal —
        /// ship-review perf fix). Must run after the transform has its final drop position, since the
        /// bob motion re-bases on it.</summary>
        private void ResetRuntimeState()
        {
            _remainingLifetime = GameConfig.Crystal.Lifetime;
            _basePosition = transform.position;
            // Random initial bob phase so simultaneously-dropped crystals don't visually bob in lockstep
            // (cosmetic only — mirrors WaveSpawner's Random.Range(0, 2*PI) use for the spawn angle;
            // 2*PI here is the structural full-circle bound, not a tunable gdd parameter).
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>Ship-review perf fix: pickup/expiry despawn point — returns pool-managed crystals to
        /// the free list (SetActive(false), glow child and all), keeps the original Destroy for pool-less
        /// crystals (2-arg SpawnDrop — see _pool's doc comment).</summary>
        private void Despawn()
        {
            if (_pool != null)
            {
                _pool.Return(gameObject);
                return;
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (TryPickup())
            {
                return;
            }

            CrystalSystem.LifetimeEvaluation lifetime = CrystalSystem.TickLifetime(_remainingLifetime, deltaTime);
            _remainingLifetime = lifetime.Remaining;
            if (lifetime.Expired)
            {
                Despawn();
                return;
            }

            ApplyCodedMotion(deltaTime);
        }

        private bool TryPickup()
        {
            if (PlayerController.Instance == null)
            {
                return false;
            }
            if (!CrystalSystem.IsWithinPickupRadius(
                    PlayerController.Instance.transform.position, transform.position, GameConfig.Crystal.PickupRadius))
            {
                return false;
            }
            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.RegisterCrystalPickup();
            }
            else
            {
                Debug.LogError("[Wiring] CrystalPickup: RunStatsTracker.Instance is null at pickup — crystal/score not recorded");
            }
            // SFX-04 (クリスタル自動回収).
            if (SfxLibrary.Instance != null)
            {
                SfxLibrary.Instance.Play(SfxLibrary.Instance.CrystalPickup);
            }
            // S-29: 回収 juice（パーティクル/フラッシュ）— SFX-04 と同一フレームで発火(gdd決定: VFX/SFX/
            // animation同期。S-17がAutoAttackDriverのヒットVFXで確立した方針をクリスタル回収にも適用).
            if (CrystalVfxLibrary.Instance != null)
            {
                CrystalVfxLibrary.Instance.SpawnCollect(transform.position);
            }
            else
            {
                LogVfxLibraryMissingOnce();
            }
            Despawn();
            return true;
        }

        // CR-CODE S-29 iter1 minor #2 fix — see _vfxLibraryMissingLogged field comment above.
        private static void LogVfxLibraryMissingOnce()
        {
            if (_vfxLibraryMissingLogged)
            {
                return;
            }
            _vfxLibraryMissingLogged = true;
            Debug.LogError("[Wiring] CrystalVfxLibrary.Instance is null — S-29 crystal glow/collect VFX disabled this session");
        }

        private void ApplyCodedMotion(float deltaTime)
        {
            transform.Rotate(Vector3.up, GameConfig.Crystal.RotationSpeedDegPerSec * deltaTime, Space.World);
            _bobPhase += GameConfig.Crystal.BobFrequency * Mathf.PI * 2f * deltaTime;
            float bobOffset = Mathf.Sin(_bobPhase) * GameConfig.Crystal.BobAmplitude;
            transform.position = _basePosition + new Vector3(0f, bobOffset, 0f);
        }
    }
}
