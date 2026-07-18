// PostProcessRig — Game シーンのグローバル URP Volume 配線 (gameplay-engineer, S-27). Builds a runtime
// VolumeProfile (Bloom + ColorAdjustments + Tonemapping, all sourced from GameConfig.PostProcess) and
// attaches it to this GameObject's Volume component as isGlobal=true (mirrors ArenaEnvironment/
// ArenaBackdrop's "build/assign at runtime from GameConfig, not a hand-authored asset" approach — S-20/
// S-26). Also enables post-processing on the Main Camera (UniversalAdditionalCameraData.
// renderPostProcessing), which URP leaves off by default for cameras that didn't opt in via the
// inspector — without this the Volume would have zero visible effect despite being correctly configured.
// Thin by design (rule: Components はライフサイクルと配線のみ) — there is no meaningful pure computation
// here worth extracting to Systems/ (direct GameConfig-value -> URP-API assignment only), mirroring
// ArenaBackdrop's precedent of skipping a dedicated Systems/ file for a similarly parameter-assignment-
// only integration (S-26 has no Systems/ file either).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ForgeGame.Components
{
    [RequireComponent(typeof(Volume))]
    public sealed class PostProcessRig : MonoBehaviour
    {
        /// <summary>Test-observability: the VolumeProfile this component actually built and applied this
        /// session (mirrors ArenaBackdrop.LastAppliedSkyboxForTests). Reset at the start of every
        /// Start() so a stale reference from a previous scene load can't leak into an assertion.</summary>
        public static VolumeProfile LastAppliedProfileForTests { get; private set; }

        private void Start()
        {
            LastAppliedProfileForTests = null;

            var volume = GetComponent<Volume>();
            volume.isGlobal = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PostProcessRig-RuntimeProfile";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(GameConfig.PostProcess.BloomThreshold);
            bloom.intensity.Override(GameConfig.PostProcess.BloomIntensity);
            bloom.scatter.Override(GameConfig.PostProcess.BloomScatter);

            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(GameConfig.PostProcess.ColorPostExposure);
            colorAdjustments.contrast.Override(GameConfig.PostProcess.ColorContrast);
            colorAdjustments.saturation.Override(GameConfig.PostProcess.ColorSaturation);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(GameConfig.PostProcess.TonemappingUseNeutral
                ? TonemappingMode.Neutral
                : TonemappingMode.ACES);

            volume.sharedProfile = profile;
            LastAppliedProfileForTests = profile;

            EnableMainCameraPostProcessing();
        }

        private static void EnableMainCameraPostProcessing()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Wiring] PostProcessRig: Camera.main not found — cannot enable URP " +
                    "post-processing (Bloom/ColorAdjustments/Tonemapping will not render).");
                return;
            }
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
        }
    }
}
