// DashTrailGhost — owns a single dash-trail ghost's fade-then-destroy lifecycle (gdd P-01「紙一重回避」
// juice ブラッシュアップ; S-31). Spawned and fully configured by Components/DashTrailSpawner.SpawnGhost via
// Initialize — this component never creates materials or resolves the hero visual itself; it only ticks
// elapsed time (Time.deltaTime — rule 2) against the pure Systems/DashTrailSystem math, applies the
// resulting alpha every frame, and destroys itself once GameConfig.Fx.DashTrailGhostLifetimeS has
// elapsed. Purely visual/presentation-only object: never touches hitboxes/colliders/invuln flags or any
// other gameplay state (acceptance: 「当たり判定・無敵フラグ等の既存ロジックに一切関与しない」) — this file has
// no reference to HealthSystem/DashSystem/PlayerController at all.
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class DashTrailGhost : MonoBehaviour
    {
        /// <summary>Last alpha applied to the ghost's materials — read-only test accessor (mirrors
        /// Components/HeroFxController.DeathFadeAlpha).</summary>
        public float CurrentAlpha { get; private set; }

        /// <summary>True once this ghost's GameConfig.Fx.DashTrailGhostLifetimeS has elapsed and it has
        /// destroyed itself — read-only test accessor for PlayMode tests that need to observe expiry
        /// without racing Destroy()'s actual frame-end teardown.</summary>
        public bool IsExpired { get; private set; }

        private Material[] _materials;
        private float _initialAlpha;
        private float _lifetimeSeconds;
        private float _elapsedSeconds;

        /// <summary>Called once by Components/DashTrailSpawner.SpawnGhost immediately after this
        /// component is added — <paramref name="materials"/> are the per-instance materials
        /// DashTrailSpawner already created fresh for this specific ghost clone (never shared with the
        /// live hero's own materials).</summary>
        public void Initialize(Material[] materials, float initialAlpha, float lifetimeSeconds)
        {
            _materials = materials;
            _initialAlpha = initialAlpha;
            _lifetimeSeconds = lifetimeSeconds;
            _elapsedSeconds = 0f;
            CurrentAlpha = initialAlpha;
            ApplyAlpha(CurrentAlpha);
        }

        private void Update()
        {
            _elapsedSeconds += Time.deltaTime;
            CurrentAlpha = DashTrailSystem.ComputeGhostAlpha(_elapsedSeconds, _lifetimeSeconds, _initialAlpha);
            ApplyAlpha(CurrentAlpha);

            if (DashTrailSystem.IsGhostExpired(_elapsedSeconds, _lifetimeSeconds))
            {
                IsExpired = true;
                Destroy(gameObject);
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null)
                {
                    continue;
                }
                Color color = _materials[i].color;
                color.a = alpha;
                _materials[i].color = color;
            }
        }

        // Ghost materials are per-instance clones created solely for this ghost (Components/
        // DashTrailSpawner.CreateGhostMaterial) — nothing else references them, so this GameObject owns
        // their lifetime and must release them itself (mirrors Components/HeroFxController.OnDestroy).
        private void OnDestroy()
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }
        }
    }
}
