// PooledVfxReturner — bridges a one-shot ParticleSystem instance back into its GameObjectPool
// (ship-review perf finding). The generated VFX prefabs (Editor/AssetIntegration.BuildHitVfxPrefab /
// BuildCrystalCollectVfxPrefab) bake main.stopAction=Destroy so un-pooled uses stay self-cleaning;
// Attach() flips the *instance's* stopAction to Callback (a runtime-only override — the prefab asset
// is untouched) and returns the finished instance to the pool instead, so each burst reuses one
// GameObject per concurrent flash rather than allocating/destroying a fresh one per hit/pickup.
// Attached once per Instantiate by Components/AutoAttackDriver (hit VFX) and
// Components/CrystalVfxLibrary (collect VFX); rented replays need nothing here — playOnAwake fires
// again on SetActive(true) and the stop callback below re-returns the instance.
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class PooledVfxReturner : MonoBehaviour
    {
        private GameObjectPool _pool;

        /// <summary>Reroutes <paramref name="vfxInstance"/>'s finished-playing cleanup from the
        /// prefab-baked Destroy to a pool Return. No-op with a wiring error when the instance carries
        /// no ParticleSystem (rule 12: every generated VFX prefab is built around one by
        /// Editor/AssetIntegration, so its absence is a broken-prefab regression, not a valid degraded
        /// state — the instance then simply never self-cleans, exactly as it wouldn't have pre-pooling).</summary>
        public static void Attach(GameObject vfxInstance, GameObjectPool pool)
        {
            var ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError("[Wiring] PooledVfxReturner: '" + vfxInstance.name +
                    "' has no ParticleSystem — VFX instance cannot be pooled (broken generated prefab?).");
                return;
            }
            ParticleSystem.MainModule main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;
            PooledVfxReturner returner = vfxInstance.AddComponent<PooledVfxReturner>();
            returner._pool = pool;
        }

        /// <summary>Fired by Unity when the ParticleSystem finishes (stopAction=Callback, set in
        /// Attach). Destroy fallback mirrors the prefab's own baked stopAction=Destroy for the
        /// (unreachable-by-construction) case of a returner that never got a pool.</summary>
        private void OnParticleSystemStopped()
        {
            if (_pool == null)
            {
                Destroy(gameObject);
                return;
            }
            _pool.Return(gameObject);
        }
    }
}
