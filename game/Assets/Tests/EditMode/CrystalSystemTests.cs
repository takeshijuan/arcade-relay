// CrystalSystemTests — S-09: クリスタル ドロップ・自動回収 + スコア算出 (gdd クリスタル ドロップ＆回収 P-04).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the XZ-plane pickup-radius test
// (mirrors HealthSystemTests' IsContacting coverage) and the CRYSTAL_LIFETIME countdown/expiry tick.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class CrystalSystemTests
    {
        [Test]
        public void IsWithinPickupRadius_WithinRadius_ReturnsTrue()
        {
            Vector3 player = Vector3.zero;
            Vector3 crystal = new Vector3(GameConfig.Crystal.PickupRadius * 0.5f, 0f, 0f);

            Assert.IsTrue(CrystalSystem.IsWithinPickupRadius(player, crystal, GameConfig.Crystal.PickupRadius));
        }

        [Test]
        public void IsWithinPickupRadius_ExactlyAtRadius_ReturnsTrue()
        {
            Vector3 player = Vector3.zero;
            Vector3 crystal = new Vector3(GameConfig.Crystal.PickupRadius, 0f, 0f);

            Assert.IsTrue(CrystalSystem.IsWithinPickupRadius(player, crystal, GameConfig.Crystal.PickupRadius),
                "boundary distance must count as within pickup radius");
        }

        [Test]
        public void IsWithinPickupRadius_BeyondRadius_ReturnsFalse()
        {
            Vector3 player = Vector3.zero;
            Vector3 crystal = new Vector3(GameConfig.Crystal.PickupRadius * 3f, 0f, 0f);

            Assert.IsFalse(CrystalSystem.IsWithinPickupRadius(player, crystal, GameConfig.Crystal.PickupRadius));
        }

        [Test]
        public void IsWithinPickupRadius_IgnoresYDifference_StaysOnXzPlane()
        {
            Vector3 player = Vector3.zero;
            // Same XZ position, wildly different Y — must still count as within radius (アリーナは XZ 平面).
            Vector3 crystal = new Vector3(0f, 50f, 0f);

            Assert.IsTrue(CrystalSystem.IsWithinPickupRadius(player, crystal, GameConfig.Crystal.PickupRadius));
        }

        [Test]
        public void TickLifetime_NotYetExpired_TicksDown_AndNotExpired()
        {
            CrystalSystem.LifetimeEvaluation result = CrystalSystem.TickLifetime(
                remaining: GameConfig.Crystal.Lifetime, deltaTime: 1f);

            Assert.AreEqual(GameConfig.Crystal.Lifetime - 1f, result.Remaining, 1e-5f);
            Assert.IsFalse(result.Expired);
        }

        [Test]
        public void TickLifetime_ExactlyReachesZero_IsExpired()
        {
            CrystalSystem.LifetimeEvaluation result = CrystalSystem.TickLifetime(remaining: 0.5f, deltaTime: 0.5f);

            Assert.AreEqual(0f, result.Remaining);
            Assert.IsTrue(result.Expired, "the frame the countdown reaches exactly 0 must count as expired");
        }

        [Test]
        public void TickLifetime_Overshoots_FloorsAtZero_AndExpired()
        {
            CrystalSystem.LifetimeEvaluation result = CrystalSystem.TickLifetime(remaining: 0.1f, deltaTime: 1f);

            Assert.AreEqual(0f, result.Remaining, "delta-time overshoot on a slow frame must floor at 0, never negative");
            Assert.IsTrue(result.Expired);
        }
    }
}
