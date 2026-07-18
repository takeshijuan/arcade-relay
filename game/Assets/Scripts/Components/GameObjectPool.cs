// GameObjectPool — generic free-list pool for the highest-churn Instantiate/Destroy surfaces
// (ship-review perf finding: enemies via Components/WaveSpawner+EnemyAgent, crystal cubes via
// Components/CrystalPickup, hit/collect particle VFX via Components/AutoAttackDriver+
// CrystalVfxLibrary). Deliberately not a MonoBehaviour and deliberately never static: each owning
// component holds its own instance field, so pooled objects stay plain scene objects and scene
// unload reclaims both the pool and its retained GameObjects naturally (no cross-scene leakage, no
// [RuntimeInitializeOnLoadMethod] reset needed — the pool's lifetime IS its owner's lifetime).
//
// Contract (kept intentionally dumb so per-type reset logic stays with the type that owns it):
// - Rent() pops a retained instance WITHOUT activating it, or returns null when the free list is
//   empty. The caller repositions/reseeds the instance and then calls SetActive(true) itself — this
//   ordering guarantees OnEnable-time registration (e.g. EnemyAgent.ActiveEnemies) never observes a
//   stale position from the instance's previous life.
// - Return() deactivates the instance and retains it, or Destroys it once the retained count would
//   exceed maxRetained (GameConfig.Perf.PoolMaxRetained by default — beyond the cap the lifetime is
//   exactly the pre-pooling Destroy behavior).
// - Entries Destroyed externally while retained (e.g. a PlayMode test's cleanup, scene teardown
//   races) are skipped on Rent via the Unity fake-null check rather than handed back as corpses.
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class GameObjectPool
    {
        private readonly Stack<GameObject> _free = new Stack<GameObject>();
        private readonly int _maxRetained;

        public GameObjectPool(int maxRetained = GameConfig.Perf.PoolMaxRetained)
        {
            _maxRetained = maxRetained;
        }

        /// <summary>Pops a retained (inactive) instance, or null when the free list is empty (the
        /// caller then falls back to its own Instantiate/CreatePrimitive factory path). The returned
        /// instance is still inactive — reposition/reseed it, then SetActive(true) (see class header
        /// for why activation is the caller's last step, not this method's).</summary>
        public GameObject Rent()
        {
            while (_free.Count > 0)
            {
                GameObject instance = _free.Pop();
                if (instance != null) // Unity fake-null: skip entries Destroyed while retained
                {
                    return instance;
                }
            }
            return null;
        }

        /// <summary>Deactivates <paramref name="instance"/> and retains it for a later Rent, or
        /// Destroys it when the free list is already at the retention cap (identical lifetime to the
        /// pre-pooling code path beyond that point). Null-tolerant no-op (mirrors Rent's fake-null
        /// tolerance) so a caller racing an external Destroy never NREs here.</summary>
        public void Return(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }
            // Double-return guard (adversarial P-4): the same instance pushed twice would later be
            // Rented twice concurrently — two callers mutating one GameObject. That's always a caller
            // bug (rule 12: loud wiring error, not silent tolerance), so log and drop the duplicate.
            // O(n) scan is fine: n is capped at _maxRetained (64 by default).
            if (_free.Contains(instance))
            {
                Debug.LogError("[Wiring] GameObjectPool: '" + instance.name +
                    "' returned twice without an intervening Rent — ignoring the duplicate return.");
                return;
            }
            instance.SetActive(false);
            if (_free.Count >= _maxRetained)
            {
                Object.Destroy(instance);
                return;
            }
            _free.Push(instance);
        }
    }
}
