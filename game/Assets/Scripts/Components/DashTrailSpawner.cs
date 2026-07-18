// DashTrailSpawner — spawns dash afterimage ghosts while PlayerController.IsDashing is true (gdd P-01
// 「紙一重回避」juice ブラッシュアップ; S-31). Thin by design (rule: Components はライフサイクルと配線のみ) —
// spawn cadence + ghost alpha/lifetime math live in Systems/DashTrailSystem and are ticked by the spawned
// Components/DashTrailGhost; this file only resolves the hero visual (rule 10 pattern, mirrors
// HeroFxController.ResolveVisual), accumulates the spawn timer, clones the visual for each ghost, and
// applies a fresh translucent tinted material. Reads only PlayerController's public read-only IsDashing
// flag — never the private dash direction/timer/invuln fields, and never writes back into
// PlayerController/Systems/DashSystem (acceptance: 「Systems/DashSystem の判定ロジックには変更を加えない」— a
// display-only sibling, same relationship HeroFxController already has to HealthComponent).
using System.Collections.Generic;
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class DashTrailSpawner : MonoBehaviour
    {
        /// <summary>Total ghosts spawned since this instance was created — read-only test accessor
        /// (mirrors Components/WaveSpawner.SpawnCallCountForTests).</summary>
        public int GhostsSpawnedForTests { get; private set; }

        // CR-CODE S-31 iteration 1 minor finding: shader lookup is deterministic per session, but
        // CreateGhostMaterial runs once per renderer material-slot per ghost (multiple ghosts/dash, one
        // dash roughly every DashCooldown seconds) — an unguarded log would spam dozens of identical lines
        // once the condition is known. One-shot flags mirror Components/CrystalPickup._vfxLibraryMissingLogged
        // (static + SubsystemRegistration reset, not an instance field, so a session-wide "already logged"
        // holds across every ghost/dash rather than resetting each time a new ghost's material is built).
        // Ship-review dedup: the URP-missing warn-once flag and the resolved-Shader session cache moved
        // into the shared Components/UrpShaderUtil (same static + SubsystemRegistration pattern there);
        // the two flags below guard messages that remain caller-specific.
        private static bool _noGhostShaderFoundLogged;
        private static bool _unknownShaderPropertiesLogged;

        // CR-CODE S-31 iteration 2 minor finding: SpawnGhost's two silent-skip `return;` branches
        // (_visualRoot null / BuildPlaceholderGhost returning null because the placeholder mesh is
        // missing) previously degraded the whole dash-trail feature to a permanent no-op with zero
        // observability — every spawn interval, for the rest of the session. One-shot flags mirror the
        // shader-lookup flags immediately above (same static + SubsystemRegistration-reset pattern).
        private static bool _visualRootMissingWarned;
        private static bool _placeholderGhostUnavailableWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForDomainReloadDisabled()
        {
            _noGhostShaderFoundLogged = false;
            _unknownShaderPropertiesLogged = false;
            _visualRootMissingWarned = false;
            _placeholderGhostUnavailableWarned = false;
        }

        private PlayerController _player;
        private Transform _visualRoot;
        private Color _tintColor;
        private bool _tintColorValid;
        private float _spawnTimer;
        private bool _wasDashing;

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            if (_player == null)
            {
                Debug.LogError("[Wiring] DashTrailSpawner requires a sibling PlayerController; dash trail disabled");
                enabled = false;
                return;
            }

            _tintColorValid = ColorUtility.TryParseHtmlString(GameConfig.Fx.DashTrailGhostTintColor, out _tintColor);
            if (!_tintColorValid)
            {
                Debug.LogError("[Wiring] DashTrailSpawner failed to parse GameConfig.Fx.DashTrailGhostTintColor; dash trail disabled");
                enabled = false;
                return;
            }

            ResolveVisual();
        }

        /// <summary>Rule 10 pattern (mirrors Components/HeroFxController.ResolveVisual): prefers the
        /// "HeroVisual" child Editor/AssetIntegration attaches once the real hero prefab is integrated;
        /// falls back to this GameObject itself (the placeholder capsule carries its own Renderer
        /// directly, matching the pre-integration state SceneWiring.WireGame produces). SpawnGhost below
        /// treats `_visualRoot == transform` as a distinct, special-cased branch (BuildPlaceholderGhost)
        /// precisely because that fallback resolves to the Player root itself, not a side-effect-free art
        /// asset — see BuildPlaceholderGhost's doc comment (CR-CODE S-31 iteration 1 blocker).</summary>
        private void ResolveVisual()
        {
            Transform found = transform.Find(GameConfig.Player.HeroVisualChildName);
            _visualRoot = found != null ? found : transform;
        }

        private void Update()
        {
            bool isDashing = _player.IsDashing;

            if (isDashing && !_wasDashing)
            {
                // First frame of a new dash: spawn immediately (the trail starts right at the "抜けた"
                // instant, not one interval late) and begin a fresh cadence for the rest of this window.
                SpawnGhost();
                _spawnTimer = 0f;
            }
            else if (isDashing)
            {
                _spawnTimer += Time.deltaTime;
                while (DashTrailSystem.ShouldSpawnGhost(_spawnTimer, GameConfig.Fx.DashTrailSpawnIntervalS))
                {
                    _spawnTimer -= GameConfig.Fx.DashTrailSpawnIntervalS;
                    SpawnGhost();
                }
            }
            else
            {
                // Acceptance: 「ダッシュ発動外ではゴーストが生成されないこと」— reset so the next dash's
                // cadence starts clean instead of carrying over a stale partial-interval remainder from a
                // previous (unrelated) dash.
                _spawnTimer = 0f;
            }

            _wasDashing = isDashing;
        }

        private void SpawnGhost()
        {
            if (_visualRoot == null)
            {
                // CR-CODE S-31 iteration 2 minor finding: was a silent no-op return, retried every
                // DashTrailSpawnIntervalS for the rest of the session with zero observability. One-shot
                // (rule 12) — this condition is a wiring/lifecycle issue, not a per-frame transient.
                if (!_visualRootMissingWarned)
                {
                    Debug.LogWarning("[Wiring] DashTrailSpawner: visual root is null — dash trail ghost spawn skipped.");
                    _visualRootMissingWarned = true;
                }
                return;
            }

            // CR-CODE S-31 iteration 1 blocker fix: `_visualRoot == transform` means ResolveVisual fell
            // back to this GameObject itself (the Player root — see ResolveVisual's doc comment above),
            // which is NOT a side-effect-free art asset like the integrated HeroVisual child. Instantiate
            // was unconditionally cloning `_visualRoot.gameObject` here, so in the pre-integration/
            // degraded state it cloned the entire Player GameObject — CapsuleCollider,
            // PlayerController/AutoAttackDriver/HealthComponent/HeroFxController/DashTrailSpawner (itself,
            // recursively) included. PlayerController.Awake() runs synchronously inside Instantiate() and
            // unconditionally overwrites the PlayerController.Instance singleton
            // (`Instance = this`) the instant the clone exists — hijacking every other system that reads
            // it (WaveSpawner/EnemyAgent targeting, CrystalPickup, HealthComponent invuln, GameHudController)
            // for the frame, and the clone's own DashTrailSpawner would recursively spawn further full-Player
            // clones. This directly violates acceptance「ゴーストは当たり判定・無敵フラグ等の既存ロジックに一切
            // 関与しない純粋な視覚オブジェクト」. The HeroVisual branch (Instantiate) is unaffected and stays
            // exactly as before — that child is a pure art asset (mesh + Animator only, no gameplay
            // MonoBehaviours) once Editor/AssetIntegration.PatchPlayerVisual attaches it.
            GameObject ghost = _visualRoot == transform
                ? BuildPlaceholderGhost()
                : Instantiate(_visualRoot.gameObject, _visualRoot.position, _visualRoot.rotation);

            if (ghost == null)
            {
                // BuildPlaceholderGhost found nothing to show (e.g. mesh removed mid-integration this
                // frame) — skip this ghost rather than spawn an empty/broken one. CR-CODE S-31 iteration 2
                // minor finding: this previously retried silently every DashTrailSpawnIntervalS for the
                // rest of the session (dash trail permanently suppressed with no observability). One-shot
                // (rule 12), mirrors _visualRootMissingWarned above.
                if (!_placeholderGhostUnavailableWarned)
                {
                    Debug.LogWarning("[Wiring] DashTrailSpawner: placeholder visual has no MeshFilter/sharedMesh — dash trail ghosts suppressed.");
                    _placeholderGhostUnavailableWarned = true;
                }
                return;
            }

            ghost.name = "DashTrailGhost";

            // Freeze the clone's pose at this instant: disabling the cloned Animator(s) stops their bone
            // transforms from advancing further, leaving a static "snapshot" silhouette of the hero mid-
            // dash (the placeholder capsule carries no Animator, so this loop is a no-op there).
            Animator[] animators = ghost.GetComponentsInChildren<Animator>();
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }

            Material[] ghostMaterials = ApplyGhostMaterials(ghost);

            DashTrailGhost fx = ghost.AddComponent<DashTrailGhost>();
            fx.Initialize(ghostMaterials, GameConfig.Fx.DashTrailGhostAlpha, GameConfig.Fx.DashTrailGhostLifetimeS);

            GhostsSpawnedForTests++;
        }

        /// <summary>Builds the pre-integration/degraded-fallback ghost (`_visualRoot == transform` — see
        /// ResolveVisual's doc comment) WITHOUT Instantiate-ing the Player root: copies only this
        /// GameObject's own MeshFilter/sharedMesh onto a brand-new bare GameObject, leaving every gameplay
        /// component/Collider on the real Player untouched and never cloned (CR-CODE S-31 iteration 1
        /// blocker — see SpawnGhost's doc comment for the full failure mode this avoids). The primitive
        /// capsule Editor/SceneWiring.WireGame creates (`GameObject.CreatePrimitive(PrimitiveType.Capsule)`)
        /// has exactly one MeshFilter/MeshRenderer pair and no children, so a single-node copy is
        /// sufficient — this branch never runs once a real Hero prefab is integrated (that case uses the
        /// Instantiate branch in SpawnGhost instead).</summary>
        private GameObject BuildPlaceholderGhost()
        {
            MeshFilter sourceFilter = _visualRoot.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                // Nothing to show yet (e.g. the placeholder mesh was removed mid-integration this exact
                // frame) — the caller skips spawning a ghost entirely rather than show an empty one.
                return null;
            }

            var ghost = new GameObject();
            ghost.transform.SetPositionAndRotation(_visualRoot.position, _visualRoot.rotation);
            ghost.transform.localScale = _visualRoot.lossyScale;

            MeshFilter filter = ghost.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;

            // CR-CODE S-31 iteration 2 minor finding: AddComponent<MeshRenderer>() alone has zero material
            // slots (sharedMaterials.Length == 0). ApplyGhostMaterials below skips any renderer with
            // slotCount == 0 entirely, so without this the placeholder ghost was created, counted
            // (GhostsSpawnedForTests++), and ticked by DashTrailGhost, but never received a translucent
            // tint material — it rendered as nothing. Size the new renderer's material array to the source
            // placeholder's own slot count (falling back to 1 if the source itself somehow has none) so
            // ApplyGhostMaterials has at least one slot to fill with a fresh ghost material.
            MeshRenderer sourceRenderer = _visualRoot.GetComponent<MeshRenderer>();
            int slotCount = sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0
                ? sourceRenderer.sharedMaterials.Length
                : 1;
            MeshRenderer renderer = ghost.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[slotCount];

            return ghost;
        }

        /// <summary>Replaces every renderer's material slot on the ghost clone with a fresh, per-slot
        /// instanced translucent tinted material (never touches/mutates the live hero's own shared or
        /// instanced materials — the ghost is a fully separate GameObject tree) and returns the flattened
        /// instance list for DashTrailGhost.Initialize to fade every frame. Deliberately overwrites every
        /// slot uniformly (including any S-28 outline slot) rather than special-casing it — unlike
        /// HeroFxController's death fade (which must preserve the base PBR material's own texture/shading
        /// through a dissolve), the ghost has no material worth preserving: it is a single flat silhouette
        /// tint end to end, so there is no "opaque outline hull left behind" failure mode to guard
        /// against here.</summary>
        private Material[] ApplyGhostMaterials(GameObject ghost)
        {
            Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();
            var allInstances = new List<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                int slotCount = renderers[i].sharedMaterials.Length;
                if (slotCount == 0)
                {
                    continue;
                }
                Material[] instanced = new Material[slotCount];
                for (int s = 0; s < slotCount; s++)
                {
                    Material material = CreateGhostMaterial();
                    instanced[s] = material;
                    if (material != null)
                    {
                        allInstances.Add(material);
                    }
                }
                renderers[i].materials = instanced;
            }
            return allInstances.ToArray();
        }

        /// <summary>Builds one fresh translucent GameConfig.Fx.DashTrailGhostTintColor material (URP-safe
        /// shader lookup mirrors Components/EnemyAgent.CreateFallbackMaterial /
        /// Components/ArenaEnvironment.CreateUrpSafeMaterial; the Opaque→Transparent surface switch
        /// mirrors Components/HeroFxController.EnableTransparency — built transparent from the start here
        /// since every ghost material starts life translucent, rather than switching an already-opaque
        /// material at runtime).</summary>
        private Material CreateGhostMaterial()
        {
            // Ship-review dedup: shared, session-cached URP/Lit→Standard resolution
            // (Components/UrpShaderUtil — the fold-in of this file's former _ghostShaderCache /
            // _urpShaderMissingWarned pair). The fallback-attempt LogWarning (legitimate
            // degraded-but-working path, rule 10/12, warn-once) lives in the helper with this caller's
            // original wording; the terminal neither-shader error stays here with its own one-shot flag.
            Shader shader = UrpShaderUtil.FindLitOrStandard(
                "[Wiring] URP/Lit shader not found; falling back to Standard for dash trail ghost (pink InternalErrorShader risk under URP).");
            if (shader == null)
            {
                if (!_noGhostShaderFoundLogged)
                {
                    Debug.LogError("[Wiring] DashTrailSpawner: neither URP/Lit nor Standard shader found — ghost material skipped (ghost will render as the shader's own error/pink fallback).");
                    _noGhostShaderFoundLogged = true;
                }
                return null;
            }

            Material material = new Material(shader);
            Color color = _tintColor;
            color.a = GameConfig.Fx.DashTrailGhostAlpha;
            material.color = color;

            if (material.HasProperty("_Surface"))
            {
                // URP Lit/SimpleLit: 0=Opaque, 1=Transparent (Shader Graph master stack convention).
                material.SetFloat("_Surface", 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (material.HasProperty("_Mode"))
            {
                // Legacy Standard shader fallback.
                material.SetFloat("_Mode", 2f); // Fade
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (!_unknownShaderPropertiesLogged)
            {
                Debug.LogError("[Wiring] DashTrailSpawner: ghost material '" + material.name +
                    "' has neither URP/Lit (_Surface) nor legacy Standard (_Mode) shader properties — " +
                    "ghost alpha will not be visually transparent.");
                _unknownShaderPropertiesLogged = true;
            }

            return material;
        }
    }
}
