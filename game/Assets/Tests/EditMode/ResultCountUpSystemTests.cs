// ResultCountUpSystemTests — S-33: Result 画面 最終スコアのカウントアップ演出. Conventions.md §9: new pure
// Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class ResultCountUpSystemTests
    {
        [Test]
        public void ComputeDisplayedScore_StartsAtZero()
        {
            Assert.AreEqual(0, ResultCountUpSystem.ComputeDisplayedScore(0f, 0.8f, 1234, 2f));
        }

        [Test]
        public void ComputeDisplayedScore_ReachesFinalScoreAtDuration()
        {
            Assert.AreEqual(1234, ResultCountUpSystem.ComputeDisplayedScore(0.8f, 0.8f, 1234, 2f));
        }

        [Test]
        public void ComputeDisplayedScore_ClampsBeyondDuration_InsteadOfOvershooting()
        {
            Assert.AreEqual(1234, ResultCountUpSystem.ComputeDisplayedScore(999f, 0.8f, 1234, 2f),
                "elapsed far beyond duration must clamp to the final score, not overshoot");
        }

        [Test]
        public void ComputeDisplayedScore_MonotonicallyIncreases_AcrossTheDuration()
        {
            int previous = -1;
            for (float t = 0f; t <= 0.8f; t += 0.02f)
            {
                int current = ResultCountUpSystem.ComputeDisplayedScore(t, 0.8f, 1234, 2f);
                Assert.GreaterOrEqual(current, previous, $"displayed score must never decrease as elapsed time grows (t={t})");
                previous = current;
            }
        }

        [Test]
        public void ComputeDisplayedScore_EaseOut_SlowsDownTowardTheEnd()
        {
            // Ease-out-power (progress = 1-(1-t)^exponent): the first half of the duration should cover
            // strictly more ground than the second half (gdd: 「終盤ほど刻みが小さくなる」).
            int atHalf = ResultCountUpSystem.ComputeDisplayedScore(0.4f, 0.8f, 1000, 2f);
            int atFull = ResultCountUpSystem.ComputeDisplayedScore(0.8f, 0.8f, 1000, 2f);

            int firstHalfDelta = atHalf - 0;
            int secondHalfDelta = atFull - atHalf;
            Assert.Greater(firstHalfDelta, secondHalfDelta,
                "the first half of the count-up duration must cover more score than the second half (ease-out)");
        }

        [Test]
        public void ComputeDisplayedScore_ZeroFinalScore_StaysZeroThroughout()
        {
            Assert.AreEqual(0, ResultCountUpSystem.ComputeDisplayedScore(0f, 0.8f, 0, 2f));
            Assert.AreEqual(0, ResultCountUpSystem.ComputeDisplayedScore(0.4f, 0.8f, 0, 2f));
            Assert.AreEqual(0, ResultCountUpSystem.ComputeDisplayedScore(0.8f, 0.8f, 0, 2f));
        }

        [Test]
        public void ComputeDisplayedScore_NonPositiveDuration_ReturnsFinalScoreImmediately()
        {
            Assert.AreEqual(1234, ResultCountUpSystem.ComputeDisplayedScore(0.1f, 0f, 1234, 2f));
            Assert.AreEqual(1234, ResultCountUpSystem.ComputeDisplayedScore(0.1f, -1f, 1234, 2f));
        }
    }
}
