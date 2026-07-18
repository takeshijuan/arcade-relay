// CrystalVfxLibrary — Game シーンのクリスタル発光/juice VFX 集約ポイント (gameplay-engineer, S-29).
// Components/CrystalPickup instances are created dynamically via GameObject.CreatePrimitive at runtime
// (Components/CrystalPickup.SpawnDrop) — they are not saved-scene prefab instances, so
// Editor/AssetIntegration cannot SerializedObject-patch each one directly the way
// PatchAutoAttackDriverVfx patches the single Player.AutoAttackDriver._hitVfxPrefab field (S-17). Mirrors
// Components/SfxLibrary's shape instead: a scene-resident singleton that Editor/AssetIntegration wires
// once (PatchCrystalVfxLibrary) with the two ParticleSystem prefabs it built
// (BuildCrystalGlowVfxPrefab/BuildCrystalCollectVfxPrefab — both reuse the IMG-04 particle texture/
// material S-17's BuildHitVfxPrefab already built, no new image asset), and every dynamically spawned
// CrystalPickup reaches them via CrystalVfxLibrary.Instance (same lookup pattern as
// SfxLibrary.Instance/RunStatsTracker.Instance already used in CrystalPickup). Also owns the actual
// Instantiate call (mirrors SfxLibrary.Play(clip) being the single fire point for SFX, not just exposing
// raw AudioClip getters) so callers stay thin (rule: Components はライフサイクルと配線のみ) and the
// spawn-count test hooks live in one place.
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class CrystalVfxLibrary : MonoBehaviour
    {
        /// <summary>Convenience lookup (mirrors SfxLibrary.Instance / PlayerController.Instance).</summary>
        public static CrystalVfxLibrary Instance { get; private set; }

        [Tooltip("S-29 発光ハロー用の常時ループ ParticleSystem prefab (IMG-04 相当テクスチャ再利用). " +
                 "Assigned by Editor/AssetIntegration.PatchCrystalVfxLibrary from GameConfig.AssetKeys.CrystalGlowVfxPrefab.")]
        [SerializeField] private GameObject _glowVfxPrefab;

        [Tooltip("S-29 回収 juice（パーティクル/フラッシュ）用の単発 ParticleSystem prefab. " +
                 "Assigned by Editor/AssetIntegration.PatchCrystalVfxLibrary from GameConfig.AssetKeys.CrystalCollectVfxPrefab.")]
        [SerializeField] private GameObject _collectVfxPrefab;

        public GameObject GlowVfxPrefab => _glowVfxPrefab;
        public GameObject CollectVfxPrefab => _collectVfxPrefab;

        /// <summary>Test-observability accessors (mirrors SfxLibrary.CrystalPickupTriggerCountForTests) —
        /// PlayMode tests assert the actual Instantiate call happened, not just that the prefab reference
        /// is non-null.</summary>
        public int GlowVfxSpawnCountForTests { get; private set; }
        public int CollectVfxSpawnCountForTests { get; private set; }

        // Ship-review perf fix: free-list pool (Components/GameObjectPool) for the collect-flash
        // instances — previously one Instantiate + stopAction=Destroy per pickup. Instance field on
        // this scene-resident singleton, NOT static (GameObjectPool's header contract), so the pool
        // and its retained instances die with the scene. The glow halo needs no pool of its own: it
        // is parented onto the crystal cube (SpawnGlow) and rides the crystal's own pooling in
        // Components/CrystalPickup — a re-rented crystal reuses its existing glow child.
        private readonly GameObjectPool _collectVfxPool = new GameObjectPool();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate CrystalVfxLibrary destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Wiring guard (mirrors SfxLibrary.LogMissingClipIfExpected): both prefabs are always
            // generated (GameConfig.AssetKeys.CrystalGlowVfxPrefab/CrystalCollectVfxPrefab non-empty), so a
            // null slot here means Editor/AssetIntegration.BuildCrystal*VfxPrefab/PatchCrystalVfxLibrary
            // degraded this run, not "not yet generated".
            LogMissingPrefabIfExpected(_glowVfxPrefab, GameConfig.AssetKeys.CrystalGlowVfxPrefab, nameof(_glowVfxPrefab));
            LogMissingPrefabIfExpected(_collectVfxPrefab, GameConfig.AssetKeys.CrystalCollectVfxPrefab, nameof(_collectVfxPrefab));
        }

        /// <summary>Instantiates the glow-halo prefab as a child of <paramref name="parent"/> (Components/
        /// CrystalPickup.SpawnDrop — parented so the halo's *emission point* moves with the crystal's coded
        /// bob/spin; BuildCrystalGlowVfxPrefab sets the ParticleSystem to World simulation space so already
        /// -emitted particles still drift independently once spawned, instead of rigidly spinning with the
        /// placeholder cube). No-op (mirrors SfxLibrary.Play(null)) when the prefab wasn't built this run —
        /// a documented, silent degradation, not an error, since Awake above already surfaces the actual
        /// wiring failure once.</summary>
        public void SpawnGlow(Transform parent)
        {
            if (_glowVfxPrefab == null)
            {
                return;
            }
            GameObject instance = Instantiate(_glowVfxPrefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            GlowVfxSpawnCountForTests++;
        }

        /// <summary>Spawns the collect-flash prefab at <paramref name="position"/> (Components/
        /// CrystalPickup.TryPickup, same frame as SFX-04 — mirrors AutoAttackDriver's S-17 hit-VFX
        /// call site). Ship-review perf fix: rents a finished instance from _collectVfxPool before
        /// falling back to Instantiate — the first Instantiate reroutes the prefab-baked
        /// stopAction=Destroy (Editor/AssetIntegration.BuildCrystalCollectVfxPrefab) to a pool Return
        /// via Components/PooledVfxReturner.Attach, and re-rents replay the burst through the prefab's
        /// playOnAwake on SetActive(true). Either way this method still owns no VFX lifetime timer.
        /// No-op when the prefab wasn't built this run (same degraded-silently rationale as SpawnGlow).</summary>
        public void SpawnCollect(Vector3 position)
        {
            if (_collectVfxPrefab == null)
            {
                return;
            }
            GameObject instance = _collectVfxPool.Rent();
            if (instance != null)
            {
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                instance.SetActive(true);
            }
            else
            {
                instance = Instantiate(_collectVfxPrefab, position, Quaternion.identity);
                PooledVfxReturner.Attach(instance, _collectVfxPool);
            }
            CollectVfxSpawnCountForTests++;
        }

        private static void LogMissingPrefabIfExpected(GameObject prefab, string assetKey, string fieldName)
        {
            if (prefab == null && !string.IsNullOrEmpty(assetKey))
            {
                Debug.LogError($"[Wiring] CrystalVfxLibrary.{fieldName} is unassigned despite GameConfig.AssetKeys expecting a generated prefab at '{assetKey}' — Editor/AssetIntegration wiring is broken.");
            }
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
