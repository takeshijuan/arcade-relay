// HeroFxSystem — pure C# progress/blend math for the hero's coded death sequence and hit-flash
// feedback (gdd 勝敗条件「死亡演出の実現手段」/「被弾時の表現」; P-01; S-16). Engine-independent
// (rules/unity-code.md #3): Vector3/Quaternion/Color/Mathf are value types, not scene API — no
// MonoBehaviour, no scene lookups, no File I/O. Components/HeroFxController (hero material fade/tilt +
// hit flash, on the Player root) and Components/GameHudController (screen dissolve overlay) each
// accumulate their own elapsed time with Time.deltaTime and call into this file every frame, applying
// the results to their own Unity objects (Material/Transform/Image) — both effects share
// GameConfig.Fx.DeathSequenceDuration so they stay numerically in lockstep without either one owning the
// other's timer.
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class HeroFxSystem
    {
        /// <summary>Normalized progress [0,1] through a duration-bounded effect (delta-time 必須 —
        /// callers accumulate elapsed with Time.deltaTime; this function only clamps/divides).
        /// <paramref name="durationSeconds"/>&lt;=0 is a wiring-error guard (every current caller passes a
        /// positive compile-time GameConfig.Fx constant, so this should never happen) that returns 1
        /// (effect already complete) rather than dividing by zero / producing NaN.</summary>
        public static float ComputeProgress(float elapsedSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01(elapsedSeconds / durationSeconds);
        }

        /// <summary>Hero material opacity during the death sequence: starts fully opaque (1) and fades to
        /// fully transparent (0) as <paramref name="progress"/> reaches 1 (gdd 決定: 「hero メッシュの
        /// マテリアルフェードアウト」).</summary>
        public static float ComputeDeathFadeAlpha(float progress)
        {
            return 1f - Mathf.Clamp01(progress);
        }

        /// <summary>Toppling/knockback tilt angle (deg), growing from 0 to <paramref name="maxTiltDeg"/>
        /// over the death sequence (gdd 決定: 「hero モデルの...転倒風の簡易回転チルト」). Applied by the
        /// caller around the hero visual's local right axis.</summary>
        public static float ComputeDeathTiltDeg(float progress, float maxTiltDeg)
        {
            return Mathf.Clamp01(progress) * maxTiltDeg;
        }

        /// <summary>Full-screen dissolve overlay opacity: starts fully transparent (0) and fades to fully
        /// opaque (1) in lockstep with the death sequence (gdd 決定: 「画面全体のディゾルブ/フェードVFX」).</summary>
        public static float ComputeDissolveAlpha(float progress)
        {
            return Mathf.Clamp01(progress);
        }

        /// <summary>Hit-flash intensity [0,1]: 1 immediately on the hit, linearly decaying back to 0 over
        /// <paramref name="flashDurationSeconds"/> (gdd 決定: 「hero マテリアルの短時間フラッシュ」— retriggered
        /// on every non-lethal hit, no dedicated animation clip involved).
        /// <paramref name="flashDurationSeconds"/>&lt;=0 guard mirrors ComputeProgress (returns 0 = no
        /// flash, rather than dividing by zero).</summary>
        public static float ComputeHitFlashIntensity(float elapsedSinceHitSeconds, float flashDurationSeconds)
        {
            if (flashDurationSeconds <= 0f)
            {
                return 0f;
            }
            return Mathf.Clamp01(1f - elapsedSinceHitSeconds / flashDurationSeconds);
        }

        /// <summary>Blends <paramref name="baseColor"/> toward <paramref name="flashColor"/> by
        /// <paramref name="intensity"/> (0 = base, 1 = full flash color). RGB channels only —
        /// <paramref name="baseColor"/>'s own alpha passes through unchanged, so callers can compose this
        /// with a separately-tracked death-fade alpha without the two effects fighting over the same
        /// channel (Components/HeroFxController keeps hit-flash and death-fade mutually exclusive in
        /// time, but this function stays effect-agnostic either way).</summary>
        public static Color ComputeHitFlashColor(Color baseColor, Color flashColor, float intensity)
        {
            float t = Mathf.Clamp01(intensity);
            return new Color(
                Mathf.Lerp(baseColor.r, flashColor.r, t),
                Mathf.Lerp(baseColor.g, flashColor.g, t),
                Mathf.Lerp(baseColor.b, flashColor.b, t),
                baseColor.a);
        }
    }
}
