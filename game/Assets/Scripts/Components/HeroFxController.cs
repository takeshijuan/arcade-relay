// HeroFxController — hero 3D visual feedback for damage/death (ui-engineer, S-16). Reads
// Components/HealthComponent's already-computed CurrentHp/IsDeathSequenceActive (never duplicates
// HP/damage state — role「UI は表示専任・状態は game state が正」) and drives two presentation-only
// effects via the pure Systems/HeroFxSystem math:
//   (1) death sequence — hero material fade-out + toppling tilt over GameConfig.Fx.DeathSequenceDuration.
//       This component runs its OWN elapsed-time accumulator (started the frame IsDeathSequenceActive
//       first flips true) rather than reading HealthComponent's private countdown, so it stays strictly
//       on HealthComponent's public surface (Components/HealthComponent.cs is S-08/gameplay-engineer
//       territory) while still finishing in lockstep — both timers race the same named constant.
//   (2) hit flash — a short material color pulse to GameConfig.Ui.ColorHitFlash whenever CurrentHp
//       decreases while the death sequence is not active (gdd 決定: 被弾表現はマテリアルフラッシュ＋既存
//       run/idle アニメ継続、専用クリップなし).
// No new AnimationClip/Animator parameter is introduced — run/idle/attack keep playing exactly as
// PlayerController/AutoAttackDriver already drive them; the only Animator call this file makes is
// SetFloat(GameConfig.Animation.SpeedParam, 0f) once at death start, to settle the existing hero
// AnimatorController into its already-defined Idle state for the fade (gdd 決定: 「使用アニメクリップは
// 既存の ANM-02(idle)...のみとし、新規 ANM 追加は不要」) — same SetFloat-only pattern rule 13 requires of
// PlayerController's own Speed updates, just parked at 0 instead of magnitude.
// Thin by design (rule: Components は薄くロジックは Systems/) — all progress/blend math lives in
// Systems/HeroFxSystem; this file only resolves Unity objects (Renderer/Material/Transform/Animator) and
// applies already-computed values to them.
//
// CR-CODE S-28 iter1 major finding fix (fixed here — Components/ is gameplay-engineer territory per the
// S-24 story allocation note in state/stories.yaml, "レンダリング設定は gameplay-engineer 領分" — even
// though this file's original S-16 authorship note above is ui-engineer): Editor/AssetIntegration.
// ApplyOutlineToRenderers (S-28) appends the shared "ForgeGame/Outline" inverse-hull material as an EXTRA
// sharedMaterials slot on every hero Renderer, baked once into Hero.prefab. That material is Opaque/
// ZWrite On/Cull Front and has no _Surface/_Mode/blending support at all — it always draws its expanded
// silhouette hull fully opaque regardless of the base material's alpha. ResolveVisual/EnableTransparency/
// ApplyAlpha below only ever touched `_renderers[i].material` (slot 0, the base PBR material), so the
// death fade previously left that outline slot rendering as a solid dark-navy hull the whole hero mesh
// shrank into as alpha reached 0, instead of the hero visually disappearing. ResolveVisual now also
// records which renderers carry that slot (by shader name — GameConfig.Outline.ShaderName — so this file
// has no compile-time dependency on Editor/AssetIntegration), and SetOutlineVisible strips/restores it in
// lockstep with BeginDeathFx/the defensive revert below.
//
// CR-CODE S-28 iter2 minor findings fix: SetOutlineVisible previously rebuilt each renderer's materials
// array as a hardcoded 2-element pair `[_materials[i], outline]` (or `[_materials[i]]` when hidden),
// which silently assumed every hero/swarmer renderer carries exactly one base submesh material —
// `_materials[i]` only ever captured slot 0 (via `renderer.material`). On a renderer with >1 base
// material slot this would drop base slots 1..N-1 on every outline toggle (BeginDeathFx / the defensive
// revert). ResolveVisual now captures the FULL leading base slice (every sharedMaterials slot before the
// detected outline slot, each instantiated individually so per-renderer color animation is unaffected)
// into `_baseMaterialSlots`, and SetOutlineVisible reconstructs from that slice instead of from slot 0
// alone. This also resolves the ResolveVisual/SetOutlineVisible asymmetry flagged in the same review round
// (ResolveVisual already accepted any sharedMaterials.Length > 1; SetOutlineVisible no longer assumes
// exactly 2) — no separate "unexpected slot count" LogError is needed since the general N-slot handling
// has no unexpected-count case left to guard against.
using System;
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class HeroFxController : MonoBehaviour
    {
        /// <summary>Last alpha applied to the hero material(s) during the death sequence (1=opaque,
        /// 0=fully faded). 1 before death starts. Read-only exposure for PlayMode tests — mirrors the
        /// SurvivalTimeSec/DashCooldownRemaining accessor pattern already used across this codebase.</summary>
        public float DeathFadeAlpha { get; private set; } = 1f;

        /// <summary>Last toppling tilt angle (deg) applied to the hero visual during the death sequence.
        /// 0 before death starts.</summary>
        public float DeathTiltDeg { get; private set; }

        /// <summary>True from the frame the death sequence's fade/tilt begins driving the hero visual.</summary>
        public bool IsDeathFxActive => _deathActive;

        /// <summary>Last hit-flash intensity applied to the hero material(s) [0,1]. 0 when no flash is
        /// active/pending.</summary>
        public float HitFlashIntensity { get; private set; }

        /// <summary>Current color of the first resolved hero Renderer's material instance — read-only
        /// test/inspection accessor (does not drive any gameplay logic).</summary>
        public Color CurrentMaterialColor => _materials != null && _materials.Length > 0 ? _materials[0].color : Color.white;

        private HealthComponent _health;
        private Animator _animator;
        private Transform _visualRoot;
        private Renderer[] _renderers;
        private Material[] _materials;
        private Color[] _baseColors;

        // CR-CODE S-28 iter1 major finding fix: index-aligned with _renderers/_materials. Null where that
        // renderer carries no S-28 outline slot (e.g. BuildOutlineMaterial degraded — no shader/color
        // resolved this run — Editor/AssetIntegration.ApplyOutlineToRenderers's own [DEGRADED] no-op).
        private Material[] _outlineSlotMaterial;

        // CR-CODE S-28 iter2 minor finding fix: index-aligned with _renderers. Every base (non-outline)
        // sharedMaterials slot for that renderer, each already instantiated (per-renderer clone) — the
        // full leading slice SetOutlineVisible restores instead of reconstructing from _materials[i]
        // (slot 0) alone. Length 1 for the common single-base-material renderer.
        private Material[][] _baseMaterialSlots;

        // CR-CODE S-16 iter1 minor finding: parsed once here instead of every ApplyFlash() call — the
        // hex string is an immutable GameConfig constant, so re-parsing per-frame during a flash was
        // wasted work and (on a parse failure) would have spammed ParseColorOrFallback's LogError every
        // frame instead of once. Mirrors Ui/GameHud.ParseColor's parse-once-at-construction pattern.
        private Color _flashColor;

        private int _previousHp;
        private bool _hpTracked;

        private bool _deathActive;
        private float _deathElapsed;

        // CR-CODE S-16 iter2 minor finding: one-shot guard so the defensive "IsDeathSequenceActive
        // reverted to false" reset below logs at most once per instance instead of every frame it would
        // (hypothetically) stay in that state.
        private bool _deathReverseWarningLogged;

        private float _hitFlashElapsed = float.PositiveInfinity; // PositiveInfinity = no flash pending/active

        private void Start()
        {
            _health = GetComponent<HealthComponent>();
            if (_health == null)
            {
                Debug.LogError("[Wiring] HeroFxController requires a sibling HealthComponent; hero death/hit fx disabled");
                enabled = false;
                return;
            }
            ResolveVisual();
        }

        /// <summary>Finds the hero visual (rule 10 pattern, mirrors PlayerController._animator resolution):
        /// prefers the "HeroVisual" child Editor/AssetIntegration attaches once the real hero prefab is
        /// integrated; falls back to this GameObject itself (the placeholder capsule carries its own
        /// Renderer directly, matching Editor/SceneWiring.WireGame's pre-integration state).</summary>
        private void ResolveVisual()
        {
            _flashColor = ParseColorOrFallback(GameConfig.Ui.ColorHitFlash, Color.red);

            _visualRoot = transform.Find(GameConfig.Player.HeroVisualChildName);
            Transform root = _visualRoot != null ? _visualRoot : transform;
            _renderers = root.GetComponentsInChildren<Renderer>();
            _animator = root.GetComponentInChildren<Animator>();

            if (_renderers.Length == 0)
            {
                Debug.LogError("[Wiring] HeroFxController: no Renderer found under hero root/visual — death fade and hit flash will not display");
                return;
            }
            _materials = new Material[_renderers.Length];
            _baseColors = new Color[_renderers.Length];
            _outlineSlotMaterial = new Material[_renderers.Length];
            _baseMaterialSlots = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                // CR-CODE S-28 iter1 major finding fix: detect (by shader name, not object identity) an
                // appended outline slot on this renderer so BeginDeathFx/the defensive revert can strip/
                // restore it — see this file's header comment for the full failure mode this fixes.
                Material[] shared = _renderers[i].sharedMaterials;
                int outlineIndex = -1;
                if (shared.Length > 1)
                {
                    Material last = shared[shared.Length - 1];
                    if (last != null && last.shader != null && last.shader.name == GameConfig.Outline.ShaderName)
                    {
                        outlineIndex = shared.Length - 1;
                    }
                }

                // renderer.materials (plural) instances a per-object copy of EVERY slot automatically —
                // mirrors EnemyAgent.ApplyHeavyTint's explicit `new Material(sharedMaterial)` clone, just
                // via the Unity-provided shortcut, so recoloring the hero never recolors a shared asset.
                // CR-CODE S-28 iter2 minor finding fix: this now captures the FULL leading base slice (not
                // just slot 0) into _baseMaterialSlots so SetOutlineVisible can restore every base slot on
                // a multi-material renderer instead of silently dropping slots 1..N-1 — see this file's
                // header comment for the full failure mode this fixes.
                Material[] instanced = _renderers[i].materials;
                int baseCount = outlineIndex >= 0 ? outlineIndex : instanced.Length;
                Material[] baseSlots = new Material[baseCount];
                Array.Copy(instanced, baseSlots, baseCount);
                _baseMaterialSlots[i] = baseSlots;

                // _materials[i]/_baseColors[i] remain the single "primary" instance the existing
                // fade/flash math (ApplyAlpha/ApplyFlash/ApplyBaseColors/EnableTransparency/
                // DisableTransparency below) drives — slot 0, unchanged from before this fix. Only the
                // outline-slot restore logic (SetOutlineVisible) now uses the full baseSlots slice.
                Material material = baseSlots.Length > 0 ? baseSlots[0] : _renderers[i].material;
                _materials[i] = material;
                _baseColors[i] = material.color;

                if (outlineIndex >= 0)
                {
                    _outlineSlotMaterial[i] = instanced[outlineIndex];
                }
            }
        }

        private void Update()
        {
            if (_health == null)
            {
                return;
            }

            if (_health.IsDeathSequenceActive)
            {
                if (!_deathActive)
                {
                    BeginDeathFx();
                }
                TickDeath(Time.deltaTime);
                return;
            }

            if (_deathActive)
            {
                // CR-CODE S-16 iter1 minor finding: defensive reset mirroring
                // Components/GameHudController.UpdateDeathDissolve's identical guard for the same
                // hypothetical "IsDeathSequenceActive flips back to false without a full scene reload"
                // case. Not currently reachable (death always transitions to Result, which loads a
                // fresh scene/Player), but without this the hero would stay faded/tilted and a second
                // death in the same component lifetime would skip BeginDeathFx's reset entirely (stale
                // _deathElapsed jumps TickDeath straight to its terminal values, no visible fade).
                //
                // CR-CODE S-16 iter2 minor finding: this reset was incomplete — it never undid
                // EnableTransparency's material-mode switch (so a hero revived via this path would stay
                // rendered through the Transparent queue at alpha=1, risking sort-order artifacts) and
                // never re-baselined the hit-flash HP tracker (so reviving at HP lower than the pre-death
                // reading would spuriously flash on the very next frame). It also normalized an
                // officially-unreachable state transition in total silence. Fixed here: undo the surface
                // switch via DisableTransparency, re-baseline _hpTracked, and log once (not every frame)
                // since reaching this branch at all signals a wiring anomaly worth investigating.
                if (!_deathReverseWarningLogged)
                {
                    _deathReverseWarningLogged = true;
                    Debug.LogWarning("[Wiring] HeroFxController: IsDeathSequenceActive reverted to false " +
                        "while a death fx sequence was active — this path is not expected to be reachable " +
                        "(death always transitions to Result, which loads a fresh scene/Player); resetting " +
                        "fade/tilt/material state defensively.");
                }
                _deathActive = false;
                _deathElapsed = 0f;
                DeathFadeAlpha = 1f;
                DeathTiltDeg = 0f;
                ApplyBaseColors();
                ApplyTilt(0f);
                DisableTransparency();
                // CR-CODE S-28 iter1 major finding fix: restore the outline slot SetOutlineVisible(false)
                // stripped in BeginDeathFx — mirrors DisableTransparency undoing EnableTransparency right
                // above.
                SetOutlineVisible(true);
                _hpTracked = false; // re-baseline against the next frame's HP instead of the pre-death value
            }

            TrackHitFlashTrigger();
            TickHitFlash(Time.deltaTime);
        }

        private void BeginDeathFx()
        {
            _deathActive = true;
            _deathElapsed = 0f;
            _hitFlashElapsed = float.PositiveInfinity; // death visuals take over — cancel any pending flash
            HitFlashIntensity = 0f;

            if (_animator != null)
            {
                // Park the existing Hero.controller on its Idle state (Speed below
                // GameConfig.Animation.RunSpeedThreshold) for the fade — gdd 決定: フェード中は ANM-02(idle)
                // のみを流用する。No new parameter/trigger, same SetFloat call PlayerController already
                // makes every frame while alive.
                _animator.SetFloat(GameConfig.Animation.SpeedParam, 0f);

                // CR-CODE S-16 iter2 minor finding: AutoAttackDriver.Update's PlayerController.IsLocked
                // gate (iter1 major #2's fix) closes the "attacks keep firing during the fade" hole for
                // every frame from here on, but leaves one narrow same-frame race: if AutoAttackDriver.
                // Update ran before HealthComponent's this frame, an Attack trigger could already be
                // latched-but-not-yet-consumed by the Animator when BeginDeathFx runs. Clear it so that
                // race can't still commit an AnyState->Attack transition on top of the Idle pose we just
                // set — cheap and has no effect on the (already-legitimate, pre-death) case where no
                // trigger is pending.
                _animator.ResetTrigger(GameConfig.Animation.AttackTrigger);
            }

            EnableTransparency();
            // CR-CODE S-28 iter1 major finding fix: strip the opaque outline slot the same frame the fade
            // starts — without this it would keep rendering a solid dark-navy hull while the base material
            // fades to transparent (see this file's header comment for the full failure mode).
            SetOutlineVisible(false);
        }

        private void TickDeath(float deltaTime)
        {
            _deathElapsed += deltaTime;
            float progress = HeroFxSystem.ComputeProgress(_deathElapsed, GameConfig.Fx.DeathSequenceDuration);

            DeathFadeAlpha = HeroFxSystem.ComputeDeathFadeAlpha(progress);
            ApplyAlpha(DeathFadeAlpha);

            DeathTiltDeg = HeroFxSystem.ComputeDeathTiltDeg(progress, GameConfig.Fx.DeathTiltMaxDeg);
            ApplyTilt(DeathTiltDeg);
        }

        /// <summary>Detects an HP decrease since the last frame (display-only observation of
        /// Components/HealthComponent.CurrentHp — this never applies damage itself) and (re)starts the
        /// hit-flash timer. The very first frame only records a baseline (no flash on scene load).</summary>
        private void TrackHitFlashTrigger()
        {
            int currentHp = _health.CurrentHp;
            if (!_hpTracked)
            {
                _previousHp = currentHp;
                _hpTracked = true;
                return;
            }
            if (currentHp < _previousHp)
            {
                _hitFlashElapsed = 0f;
            }
            _previousHp = currentHp;
        }

        private void TickHitFlash(float deltaTime)
        {
            if (float.IsPositiveInfinity(_hitFlashElapsed))
            {
                return;
            }
            HitFlashIntensity = HeroFxSystem.ComputeHitFlashIntensity(_hitFlashElapsed, GameConfig.Fx.HitFlashDuration);
            ApplyFlash(HitFlashIntensity);
            _hitFlashElapsed += deltaTime;

            if (HitFlashIntensity <= 0f)
            {
                _hitFlashElapsed = float.PositiveInfinity;
                ApplyBaseColors(); // exact revert — avoids a faint float-precision-drift tint lingering
            }
        }

        private void ApplyFlash(float intensity)
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i].color = HeroFxSystem.ComputeHitFlashColor(_baseColors[i], _flashColor, intensity);
            }
        }

        private void ApplyBaseColors()
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i].color = _baseColors[i];
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
                Color c = _baseColors[i];
                c.a = alpha;
                _materials[i].color = c;
            }
        }

        private void ApplyTilt(float tiltDeg)
        {
            Transform root = _visualRoot != null ? _visualRoot : transform;
            root.localRotation = Quaternion.AngleAxis(tiltDeg, Vector3.right);
        }

        /// <summary>Best-effort switch to a transparent surface so the death fade's alpha actually
        /// renders instead of being silently ignored by an opaque-surface shader. Guarded with
        /// HasProperty checks (URP/Lit vs. legacy Standard vs. an unknown Meshy-exported shader with
        /// neither) so this degrades to "alpha value changes but the mesh stays visually opaque" rather
        /// than throwing on unsupported shaders — the fade timing/transition itself (this file's actual
        /// acceptance-tested behavior) does not depend on this succeeding.</summary>
        private void EnableTransparency()
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                Material mat = _materials[i];
                if (mat.HasProperty("_Surface"))
                {
                    // URP Lit/SimpleLit: 0=Opaque, 1=Transparent (Shader Graph master stack convention).
                    mat.SetFloat("_Surface", 1f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else if (mat.HasProperty("_Mode"))
                {
                    // Legacy Standard shader fallback (CreateFallbackMaterial's own fallback path).
                    mat.SetFloat("_Mode", 2f); // Fade
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else
                {
                    // CR-CODE S-16 iter1 major finding: unknown Meshy-exported shader with neither
                    // property — matches the exact "both shaders missing" defect class Components/
                    // ArenaEnvironment.CreateUrpSafeMaterial and Components/EnemyAgent.CreateFallbackMaterial
                    // already LogError on (S-14 iter1 / S-20 iter1 CR-CODE — state/active.md). The alpha
                    // value still updates (testable/acceptance-relevant), but the death fade will render
                    // fully opaque on-screen, so this must not fail silently.
                    Debug.LogError("[Wiring] HeroFxController: hero material '" + mat.name +
                        "' has neither URP/Lit (_Surface) nor legacy Standard (_Mode) shader properties — " +
                        "death fade alpha will not be visually transparent.");
                }
            }
        }

        /// <summary>Reverses EnableTransparency's surface-mode switch back to Opaque (CR-CODE S-16 iter2
        /// minor finding). Only used by Update's defensive "IsDeathSequenceActive reverted to false"
        /// reset — a path not currently reachable in production (death always transitions to Result,
        /// which loads a fresh scene/Player and a fresh HeroFxController instance), but if it is ever hit
        /// this keeps the hero material's render queue/blend state from staying stuck in the Transparent
        /// path once the fade fully reverts alpha to 1. The unknown-shader case has nothing to undo (its
        /// EnableTransparency branch never touched a surface property in the first place; it already
        /// logged its own LogError there).</summary>
        private void DisableTransparency()
        {
            if (_materials == null)
            {
                return;
            }
            for (int i = 0; i < _materials.Length; i++)
            {
                Material mat = _materials[i];
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 0f);
                    mat.SetOverrideTag("RenderType", "");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }
                else if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 0f); // Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }
            }
        }

        /// <summary>Appends/removes the S-28 outline material slot (recorded by ResolveVisual) on every
        /// renderer that carries one. Uses Renderer.materials with an array whose elements are already
        /// per-instance Materials (as _baseMaterialSlots[i]/_outlineSlotMaterial[i] already are, from
        /// ResolveVisual's `.materials` access) so this never re-instantiates or disturbs the exact
        /// instances the death fade/hit flash are already animating. No-op for any renderer with no
        /// recorded outline slot (CR-CODE S-28 iter1 major finding fix).
        /// CR-CODE S-28 iter2 minor finding fix: restores the FULL leading base slice
        /// (_baseMaterialSlots[i], every sharedMaterials slot before the outline slot) rather than
        /// reconstructing a hardcoded `[_materials[i]]` (slot 0 only) — a multi-base-material renderer no
        /// longer loses slots 1..N-1 on every outline toggle.</summary>
        private void SetOutlineVisible(bool visible)
        {
            if (_renderers == null || _outlineSlotMaterial == null)
            {
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_outlineSlotMaterial[i] == null)
                {
                    continue; // this renderer never carried an outline slot (degraded/no outline built)
                }
                Material[] baseSlots = _baseMaterialSlots[i];
                if (!visible)
                {
                    _renderers[i].materials = baseSlots;
                    continue;
                }
                Material[] withOutline = new Material[baseSlots.Length + 1];
                Array.Copy(baseSlots, withOutline, baseSlots.Length);
                withOutline[withOutline.Length - 1] = _outlineSlotMaterial[i];
                _renderers[i].materials = withOutline;
            }
        }

        private static Color ParseColorOrFallback(string hex, Color fallback)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
            Debug.LogError($"[Wiring] HeroFxController failed to parse GameConfig color '{hex}'; using fallback color");
            return fallback;
        }

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
