// CameraShakeSystemTests — S-23: ダッシュ紙一重回避のカメラシェイク演出 (gdd P-01「紙一重回避」).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the near-miss trigger decision
// (contact+invulnerable+not-already-latched) and the linear decay-to-exact-zero offset math.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class CameraShakeSystemTests
    {
        [Test]
        public void ShouldTriggerNearMissShake_ContactingAndInvulnerable_NotYetLatched_ReturnsTrue()
        {
            Assert.IsTrue(CameraShakeSystem.ShouldTriggerNearMissShake(
                isContacting: true, isInvulnerable: true, alreadyTriggeredThisWindow: false));
        }

        [Test]
        public void ShouldTriggerNearMissShake_AlreadyLatched_ReturnsFalse_EvenIfStillContactingAndInvulnerable()
        {
            Assert.IsFalse(CameraShakeSystem.ShouldTriggerNearMissShake(
                isContacting: true, isInvulnerable: true, alreadyTriggeredThisWindow: true),
                "同一ダッシュの無敵窓内で複数回接触判定が発生しても多重加算せず単発に丸める");
        }

        [Test]
        public void ShouldTriggerNearMissShake_NotInvulnerable_ReturnsFalse_EvenIfContacting()
        {
            Assert.IsFalse(CameraShakeSystem.ShouldTriggerNearMissShake(
                isContacting: true, isInvulnerable: false, alreadyTriggeredThisWindow: false),
                "通常被弾(無敵でない接触)はシェイクを発火しない");
        }

        [Test]
        public void ShouldTriggerNearMissShake_NotContacting_ReturnsFalse_EvenIfInvulnerable()
        {
            Assert.IsFalse(CameraShakeSystem.ShouldTriggerNearMissShake(
                isContacting: false, isInvulnerable: true, alreadyTriggeredThisWindow: false));
        }

        [Test]
        public void TickTimer_ClampsAtZero_NeverNegative()
        {
            Assert.AreEqual(0f, CameraShakeSystem.TickTimer(0.05f, 0.5f));
        }

        [Test]
        public void TickTimer_TicksDownByDeltaTime()
        {
            Assert.AreEqual(0.1f, CameraShakeSystem.TickTimer(0.15f, 0.05f), 1e-5f);
        }

        [Test]
        public void ComputeOffset_AtRemainingZero_ReturnsExactlyZero()
        {
            Vector3 offset = CameraShakeSystem.ComputeOffset(
                remaining: 0f, duration: GameConfig.Fx.DashNearMissShakeDuration,
                magnitude: GameConfig.Fx.DashNearMissShakeMagnitude, cameraRight: Vector3.right, cameraUp: Vector3.up);

            Assert.AreEqual(Vector3.zero, offset, "shake must strictly revert to the base position once the duration has elapsed");
        }

        [Test]
        public void ComputeOffset_AtTriggerInstant_HasMagnitudeEqualToConfiguredPeak()
        {
            Vector3 offset = CameraShakeSystem.ComputeOffset(
                remaining: GameConfig.Fx.DashNearMissShakeDuration, duration: GameConfig.Fx.DashNearMissShakeDuration,
                magnitude: GameConfig.Fx.DashNearMissShakeMagnitude, cameraRight: Vector3.right, cameraUp: Vector3.up);

            Assert.AreEqual(GameConfig.Fx.DashNearMissShakeMagnitude, offset.magnitude, 1e-4f,
                "offset magnitude at trigger instant (remaining == duration) must equal the configured peak magnitude");
        }

        [Test]
        public void ComputeOffset_DecaysLinearly_HalfwayThroughIsHalfMagnitude()
        {
            float duration = GameConfig.Fx.DashNearMissShakeDuration;
            float magnitude = GameConfig.Fx.DashNearMissShakeMagnitude;

            Vector3 offset = CameraShakeSystem.ComputeOffset(
                remaining: duration * 0.5f, duration: duration, magnitude: magnitude,
                cameraRight: Vector3.right, cameraUp: Vector3.up);

            Assert.AreEqual(magnitude * 0.5f, offset.magnitude, 1e-4f);
        }

        [Test]
        public void ComputeOffset_NeverExceedsConfiguredPeakMagnitude()
        {
            float duration = GameConfig.Fx.DashNearMissShakeDuration;
            float magnitude = GameConfig.Fx.DashNearMissShakeMagnitude;

            for (float t = 0f; t <= duration; t += duration / 20f)
            {
                Vector3 offset = CameraShakeSystem.ComputeOffset(t, duration, magnitude, Vector3.right, Vector3.up);
                Assert.LessOrEqual(offset.magnitude, magnitude + 1e-4f);
            }
        }

        [Test]
        public void ComputeOffset_ZeroDuration_ReturnsZero_NoDivideByZero()
        {
            Vector3 offset = CameraShakeSystem.ComputeOffset(
                remaining: 0.1f, duration: 0f, magnitude: GameConfig.Fx.DashNearMissShakeMagnitude,
                cameraRight: Vector3.right, cameraUp: Vector3.up);

            Assert.AreEqual(Vector3.zero, offset);
        }
    }
}
