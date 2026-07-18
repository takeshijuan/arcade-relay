// VolumeControl — pure clamped-step math + linear-to-decibel conversion for the Menu 設定タブ
// BGM/SFX 音量スライダー (gdd「操作仕様」: A/Dで0.1刻み増減; S-13). Engine-independent: no
// MonoBehaviour/scene API (rules/unity-code.md #3) — Vector/Mathf are value/math types, not scene API.
// Mirrors Systems/MenuNavigation.cs's ownership pattern: a ui-engineer-authored pure Systems file for
// a UI-domain concern (Menu 設定タブ), not gameplay Systems/Meta (which stays gameplay-engineer's turf
// per the role boundary — this file never touches SaveData itself, only plain float math).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class VolumeControl
    {
        /// <summary>
        /// Steps a 0..1 volume value by <paramref name="stepSize"/> * <paramref name="direction"/>,
        /// clamped to [0,1] and rounded to the same decimal precision as stepSize (gdd: 0.1刻み) so
        /// repeated presses don't accumulate float error (e.g. 0.7999999f instead of 0.8f).
        /// </summary>
        public static float Step(float current, float stepSize, int direction)
        {
            if (stepSize <= 0f)
            {
                return Mathf.Clamp01(current);
            }
            float next = current + stepSize * direction;
            float scale = 1f / stepSize;
            next = Mathf.Round(next * scale) / scale;
            return Mathf.Clamp01(next);
        }

        /// <summary>
        /// Converts a linear 0..1 volume (SaveData.bgmVolume/sfxVolume) to the decibel value an
        /// AudioMixer exposed float parameter expects (0.0 -&gt; GameConfig.Audio.MixerMinDb "silence",
        /// 1.0 -&gt; 0dB unity gain). Values at/below MixerSilenceLinearFloor snap to MixerMinDb directly
        /// (avoids log10(0) = -Infinity).
        /// </summary>
        public static float LinearToDecibel(float linear)
        {
            float clamped = Mathf.Clamp01(linear);
            if (clamped <= GameConfig.Audio.MixerSilenceLinearFloor)
            {
                return GameConfig.Audio.MixerMinDb;
            }
            return 20f * Mathf.Log10(clamped);
        }
    }
}
