// KeyLightRig — Game シーンの Directional Light を art-bible.json style_block（"Single soft key light
// from upper-front-left producing only soft-edged ambient-occlusion darkening at contact points ... no
// hard cast shadows"）に合わせて調整する (gameplay-engineer, S-27). Attached directly to the scaffold's
// existing "Directional Light" GameObject (mirrors ArenaCameraRig's [RequireComponent(typeof(Camera))]
// pattern applied to Main Camera).
//
// "upper-front-left" is read relative to the fixed overhead camera (Systems/ArenaCameraMath.
// ComputeFixedPose: the camera sits south of the arena with no yaw, looking north/+Z at all times — S-04/
// S-22). A negative Y (yaw) rotation turns the light's forward ray toward -X (west, i.e. camera-left when
// facing north), so the light source itself sits broadly on the camera's side of the arena (south-ish,
// i.e. "front") while shining down-and-toward-camera-left — matching "upper-front-left" without ever
// backlighting silhouettes seen from the fixed camera (backlighting would blow out edges and defeat the
// P-01/P-03 readability requirement this story protects).
//
// Thin by design (rule: Components はライフサイクルと配線のみ) — direct GameConfig-sourced assignment
// only, no computation worth extracting to Systems/ (mirrors PostProcessRig / ArenaBackdrop's precedent
// of skipping a dedicated Systems/ file for parameter-assignment-only integrations).
using UnityEngine;

namespace ForgeGame.Components
{
    [RequireComponent(typeof(Light))]
    public sealed class KeyLightRig : MonoBehaviour
    {
        private void Start()
        {
            var light = GetComponent<Light>();
            if (light.type != LightType.Directional)
            {
                Debug.LogError("[Wiring] KeyLightRig: attached to a non-Directional Light (type=" +
                    light.type + ") — art-bible key light adjustment skipped. Expected the scaffold's " +
                    "'Directional Light' GameObject (Editor/SceneWiring.WireGame).");
                return;
            }

            transform.rotation = Quaternion.Euler(
                GameConfig.Lighting.KeyLightPitchDeg, GameConfig.Lighting.KeyLightYawDeg, 0f);

            if (ColorUtility.TryParseHtmlString(GameConfig.Lighting.KeyLightColor, out Color color))
            {
                light.color = color;
            }
            else
            {
                // CR-CODE s-27 iter2 minor指摘#6 fix: QA-PLAY の LogAssert.NoUnexpectedReceived() は
                // error 系のみ検知するため、LogWarning のままだと定数破損（authoring ミス）が自動ゲートを
                // 素通りし誤ったライト色のまま出荷され得る。上の非 Directional 経路（[Wiring] LogError）
                // と対称に揃え、[Wiring] プレフィクスの LogError に引き上げる。フォールバック挙動
                // （light の既存色を維持）自体は変えない。
                Debug.LogError("[Wiring] KeyLightRig: GameConfig.Lighting.KeyLightColor ('" +
                    GameConfig.Lighting.KeyLightColor + "') failed to parse as a hex color; leaving the " +
                    "light's existing color unchanged.");
            }

            light.intensity = GameConfig.Lighting.KeyLightIntensity;
            // style_block: "no hard cast shadows" — Soft (not Hard, not None so contact-point AO-like
            // darkening still reads per the style_block's "soft-edged ambient-occlusion darkening").
            light.shadows = LightShadows.Soft;
            light.shadowStrength = GameConfig.Lighting.KeyLightShadowStrength;
        }
    }
}
