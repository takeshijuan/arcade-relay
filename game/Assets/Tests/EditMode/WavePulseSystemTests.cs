// WavePulseSystemTests — S-15: ウェーブ切替フィードバック（SFX + HUD パルス）. Conventions.md §9: new pure
// Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class WavePulseSystemTests
    {
        [Test]
        public void HasWaveIncreased_TrueOnlyWhenCurrentIsGreater()
        {
            Assert.IsTrue(WavePulseSystem.HasWaveIncreased(1, 2));
            Assert.IsFalse(WavePulseSystem.HasWaveIncreased(2, 2), "equal wave must not be treated as an increase");
            Assert.IsFalse(WavePulseSystem.HasWaveIncreased(3, 2), "a decrease must not be treated as an increase");
        }

        [Test]
        public void ComputeScale_StartsAndEndsAtOne_PeaksAtHalfway()
        {
            Assert.AreEqual(1f, WavePulseSystem.ComputeScale(0f, 0.3f, 1.3f), 1e-4f);
            Assert.AreEqual(1.3f, WavePulseSystem.ComputeScale(0.15f, 0.3f, 1.3f), 1e-4f);
            Assert.AreEqual(1f, WavePulseSystem.ComputeScale(0.3f, 0.3f, 1.3f), 1e-4f);
        }

        [Test]
        public void ComputeScale_RisesOnFirstHalf_FallsOnSecondHalf()
        {
            float quarter = WavePulseSystem.ComputeScale(0.075f, 0.3f, 1.3f);
            float threeQuarter = WavePulseSystem.ComputeScale(0.225f, 0.3f, 1.3f);

            Assert.Greater(quarter, 1f, "scale must have started rising by the first quarter of the duration");
            Assert.Less(quarter, 1.3f, "scale must not yet have reached the peak at the first quarter");
            Assert.Greater(threeQuarter, 1f, "scale must still be above 1.0 on the falling leg");
            Assert.Less(threeQuarter, 1.3f, "scale must have started falling back down by the third quarter");
        }

        [Test]
        public void ComputeScale_ClampsBeyondDuration_InsteadOfOvershooting()
        {
            Assert.AreEqual(1f, WavePulseSystem.ComputeScale(999f, 0.3f, 1.3f), 1e-4f,
                "elapsed far beyond duration must clamp to the terminal 1.0, not overshoot/oscillate");
        }

        [Test]
        public void ComputeScale_NonPositiveDuration_ReturnsNoPulseInsteadOfDividingByZero()
        {
            Assert.AreEqual(1f, WavePulseSystem.ComputeScale(0.1f, 0f, 1.3f));
            Assert.AreEqual(1f, WavePulseSystem.ComputeScale(0.1f, -1f, 1.3f));
        }
    }
}
