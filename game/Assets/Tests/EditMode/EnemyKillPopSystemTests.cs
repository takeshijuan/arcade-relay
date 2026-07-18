// EnemyKillPopSystemTests — S-32: 撃破インパクト演出（敵消滅ポップ + 小型カメラノッジ）(gdd P-02/P-03).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the scale-growth math
// (ComputeScale) and the completion gate (IsComplete) that Components/EnemyAgent ticks.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class EnemyKillPopSystemTests
    {
        [Test]
        public void ComputeScale_AtElapsedZero_ReturnsOne_NoInstantJump()
        {
            Assert.AreEqual(1f, EnemyKillPopSystem.ComputeScale(
                0f, GameConfig.Fx.EnemyKillPopDurationS, GameConfig.Fx.EnemyKillPopScaleMultiplier), 1e-5f,
                "the pop must start at the enemy's normal scale (1.0), not jump straight to the peak multiplier");
        }

        [Test]
        public void ComputeScale_AtDuration_ReturnsConfiguredMultiplier()
        {
            Assert.AreEqual(GameConfig.Fx.EnemyKillPopScaleMultiplier, EnemyKillPopSystem.ComputeScale(
                GameConfig.Fx.EnemyKillPopDurationS, GameConfig.Fx.EnemyKillPopDurationS, GameConfig.Fx.EnemyKillPopScaleMultiplier),
                1e-5f, "gdd/acceptance: 消滅前に一瞬スケールアップしてから消える — scale must reach the configured peak by the end of the pop");
        }

        [Test]
        public void ComputeScale_Halfway_IsHalfwayBetweenOneAndMultiplier()
        {
            float duration = GameConfig.Fx.EnemyKillPopDurationS;
            float multiplier = GameConfig.Fx.EnemyKillPopScaleMultiplier;
            float expected = 1f + (multiplier - 1f) * 0.5f;

            Assert.AreEqual(expected, EnemyKillPopSystem.ComputeScale(duration * 0.5f, duration, multiplier), 1e-4f);
        }

        [Test]
        public void ComputeScale_ClampsAtMultiplier_NeverOvershootsPastElapsedGreaterThanDuration()
        {
            float duration = GameConfig.Fx.EnemyKillPopDurationS;
            float multiplier = GameConfig.Fx.EnemyKillPopScaleMultiplier;

            Assert.AreEqual(multiplier, EnemyKillPopSystem.ComputeScale(duration * 3f, duration, multiplier), 1e-5f,
                "a caller ticking one extra frame past duration must not overshoot the configured peak multiplier");
        }

        [Test]
        public void ComputeScale_ZeroDuration_ReturnsMultiplier_NoDivideByZero()
        {
            Assert.AreEqual(GameConfig.Fx.EnemyKillPopScaleMultiplier,
                EnemyKillPopSystem.ComputeScale(0.05f, 0f, GameConfig.Fx.EnemyKillPopScaleMultiplier), 1e-5f);
        }

        [Test]
        public void IsComplete_BeforeDuration_ReturnsFalse()
        {
            Assert.IsFalse(EnemyKillPopSystem.IsComplete(GameConfig.Fx.EnemyKillPopDurationS * 0.5f, GameConfig.Fx.EnemyKillPopDurationS));
        }

        [Test]
        public void IsComplete_AtOrPastDuration_ReturnsTrue()
        {
            Assert.IsTrue(EnemyKillPopSystem.IsComplete(GameConfig.Fx.EnemyKillPopDurationS, GameConfig.Fx.EnemyKillPopDurationS));
            Assert.IsTrue(EnemyKillPopSystem.IsComplete(GameConfig.Fx.EnemyKillPopDurationS * 1.5f, GameConfig.Fx.EnemyKillPopDurationS));
        }

        [Test]
        public void KillCameraNudgeMagnitude_IsSmallerThanDashNearMissShakeMagnitude()
        {
            // acceptance: 「DashNearMissShakeMagnitude=0.15mより明確に小さくし、ニアミス合図とは体感で区別
            // できるようにする」— locks in the relative ordering between the two GameConfig.Fx constants so
            // a future config edit can't silently make the two cues indistinguishable in amplitude.
            Assert.Less(GameConfig.Fx.EnemyKillCameraNudgeMagnitudeM, GameConfig.Fx.DashNearMissShakeMagnitude);
        }
    }
}
