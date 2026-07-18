// HeroFxSystemTests — S-16: 死亡演出（コード合成）+ 被弾マテリアルフラッシュ. Conventions.md §9: new pure
// Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class HeroFxSystemTests
    {
        [Test]
        public void ComputeProgress_ClampsToZeroOneRange()
        {
            Assert.AreEqual(0f, HeroFxSystem.ComputeProgress(0f, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, HeroFxSystem.ComputeProgress(0.25f, 0.5f), 1e-4f);
            Assert.AreEqual(1f, HeroFxSystem.ComputeProgress(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(1f, HeroFxSystem.ComputeProgress(999f, 0.5f), 1e-4f, "elapsed beyond duration must clamp to 1, not overshoot");
        }

        [Test]
        public void ComputeProgress_NonPositiveDuration_ReturnsCompleteInsteadOfDividingByZero()
        {
            Assert.AreEqual(1f, HeroFxSystem.ComputeProgress(0.1f, 0f));
            Assert.AreEqual(1f, HeroFxSystem.ComputeProgress(0.1f, -1f));
        }

        [Test]
        public void ComputeDeathFadeAlpha_StartsOpaque_EndsFullyTransparent()
        {
            Assert.AreEqual(1f, HeroFxSystem.ComputeDeathFadeAlpha(0f), 1e-4f);
            Assert.AreEqual(0.5f, HeroFxSystem.ComputeDeathFadeAlpha(0.5f), 1e-4f);
            Assert.AreEqual(0f, HeroFxSystem.ComputeDeathFadeAlpha(1f), 1e-4f);
        }

        [Test]
        public void ComputeDeathTiltDeg_ScalesLinearlyWithProgress()
        {
            Assert.AreEqual(0f, HeroFxSystem.ComputeDeathTiltDeg(0f, 80f), 1e-4f);
            Assert.AreEqual(40f, HeroFxSystem.ComputeDeathTiltDeg(0.5f, 80f), 1e-4f);
            Assert.AreEqual(80f, HeroFxSystem.ComputeDeathTiltDeg(1f, 80f), 1e-4f);
        }

        [Test]
        public void ComputeDissolveAlpha_StartsTransparent_EndsFullyOpaque()
        {
            Assert.AreEqual(0f, HeroFxSystem.ComputeDissolveAlpha(0f), 1e-4f);
            Assert.AreEqual(0.5f, HeroFxSystem.ComputeDissolveAlpha(0.5f), 1e-4f);
            Assert.AreEqual(1f, HeroFxSystem.ComputeDissolveAlpha(1f), 1e-4f);
        }

        [Test]
        public void ComputeHitFlashIntensity_DecaysLinearlyToZero()
        {
            Assert.AreEqual(1f, HeroFxSystem.ComputeHitFlashIntensity(0f, 0.15f), 1e-4f);
            Assert.AreEqual(0.5f, HeroFxSystem.ComputeHitFlashIntensity(0.075f, 0.15f), 1e-4f);
            Assert.AreEqual(0f, HeroFxSystem.ComputeHitFlashIntensity(0.15f, 0.15f), 1e-4f);
            Assert.AreEqual(0f, HeroFxSystem.ComputeHitFlashIntensity(999f, 0.15f), 1e-4f, "elapsed beyond duration must clamp to 0, not go negative");
        }

        [Test]
        public void ComputeHitFlashIntensity_NonPositiveDuration_ReturnsNoFlashInsteadOfDividingByZero()
        {
            Assert.AreEqual(0f, HeroFxSystem.ComputeHitFlashIntensity(0f, 0f));
            Assert.AreEqual(0f, HeroFxSystem.ComputeHitFlashIntensity(0f, -1f));
        }

        [Test]
        public void ComputeHitFlashColor_ZeroIntensity_ReturnsBaseColor()
        {
            Color baseColor = new Color(0.2f, 0.3f, 0.4f, 0.8f);
            Color flashColor = Color.red;

            Color result = HeroFxSystem.ComputeHitFlashColor(baseColor, flashColor, 0f);

            Assert.AreEqual(baseColor, result);
        }

        [Test]
        public void ComputeHitFlashColor_FullIntensity_ReturnsFlashColorRgb_ButPreservesBaseAlpha()
        {
            Color baseColor = new Color(0.2f, 0.3f, 0.4f, 0.8f);
            Color flashColor = new Color(1f, 0f, 0f, 1f);

            Color result = HeroFxSystem.ComputeHitFlashColor(baseColor, flashColor, 1f);

            Assert.AreEqual(flashColor.r, result.r, 1e-4f);
            Assert.AreEqual(flashColor.g, result.g, 1e-4f);
            Assert.AreEqual(flashColor.b, result.b, 1e-4f);
            Assert.AreEqual(baseColor.a, result.a, 1e-4f, "flash blend must not overwrite the base color's alpha (death-fade owns alpha separately)");
        }

        [Test]
        public void ComputeHitFlashColor_HalfIntensity_LerpsBetweenBaseAndFlash()
        {
            Color baseColor = new Color(0f, 0f, 0f, 1f);
            Color flashColor = new Color(1f, 1f, 1f, 1f);

            Color result = HeroFxSystem.ComputeHitFlashColor(baseColor, flashColor, 0.5f);

            Assert.AreEqual(0.5f, result.r, 1e-4f);
            Assert.AreEqual(0.5f, result.g, 1e-4f);
            Assert.AreEqual(0.5f, result.b, 1e-4f);
        }
    }
}
