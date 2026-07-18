// EnemyVisualMotionSystemTests — S-21: coded forward-lean tilt + up/down bounce for the swarmer's
// "Visual" child (MDL-02 rig-degraded — no ANM-04/Avatar/AnimatorController). Conventions.md §9: new
// pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class EnemyVisualMotionSystemTests
    {
        [Test]
        public void AdvanceBouncePhase_ScalesWithFrequencyAndDeltaTime()
        {
            float phase = EnemyVisualMotionSystem.AdvanceBouncePhase(phase: 0f, frequencyHz: 1f, deltaTime: 0.25f);

            // 1 Hz over 0.25s = a quarter cycle = pi/2 radians.
            Assert.AreEqual(Mathf.PI / 2f, phase, 1e-4f);
        }

        [Test]
        public void AdvanceBouncePhase_WrapsAtTwoPi_StaysNumericallyStable()
        {
            // 1 Hz over 1s = a full 2*pi cycle added on top of an already near-2*pi phase; without
            // wrapping this would keep growing unbounded across an enemy's lifetime.
            float phase = EnemyVisualMotionSystem.AdvanceBouncePhase(
                phase: Mathf.PI * 2f - 0.1f, frequencyHz: 1f, deltaTime: 1f);

            Assert.Less(phase, Mathf.PI * 2f);
            Assert.GreaterOrEqual(phase, 0f);
            Assert.AreEqual(2f * Mathf.PI - 0.1f, phase, 1e-3f); // wraps back to (2*pi - 0.1) mod 2*pi
        }

        [Test]
        public void AdvanceBouncePhase_MultiCycleHitch_WrapsFullyInsteadOfGrowingUnbounded()
        {
            // CR-CODE S-21 iteration 1 minor finding: a single `next - TwoPi` subtraction only wraps
            // once per call, so a hitch spanning several bounce periods (e.g. a multi-second frame drop
            // at 3Hz) used to leave the phase >= TwoPi and grow unbounded across sustained low-fps
            // stretches. Mathf.Repeat must fully wrap regardless of how many cycles elapsed in one call.
            float phase = EnemyVisualMotionSystem.AdvanceBouncePhase(
                phase: 0f, frequencyHz: 3f, deltaTime: 10f); // 3Hz * 10s = 30 full cycles = phase 0 mod 2*pi

            Assert.Less(phase, Mathf.PI * 2f);
            Assert.GreaterOrEqual(phase, 0f);
            Assert.AreEqual(0f, phase, 1e-2f);
        }

        [Test]
        public void ComputeBounceOffsetY_IsSineWaveScaledByAmplitude()
        {
            Assert.AreEqual(0f, EnemyVisualMotionSystem.ComputeBounceOffsetY(0f, 0.08f), 1e-4f);
            Assert.AreEqual(0.08f, EnemyVisualMotionSystem.ComputeBounceOffsetY(Mathf.PI / 2f, 0.08f), 1e-4f);
            Assert.AreEqual(0f, EnemyVisualMotionSystem.ComputeBounceOffsetY(Mathf.PI, 0.08f), 1e-4f);
            Assert.AreEqual(-0.08f, EnemyVisualMotionSystem.ComputeBounceOffsetY(3f * Mathf.PI / 2f, 0.08f), 1e-4f);
        }

        [Test]
        public void ComputeApproachTilt_DegenerateDirection_ReturnsIdentityInsteadOfNaN()
        {
            Quaternion rotation = EnemyVisualMotionSystem.ComputeApproachTilt(Vector3.zero, 12f);

            Assert.AreEqual(Quaternion.identity, rotation);
        }

        [Test]
        public void ComputeApproachTilt_FacesApproachDirectionOnYAxis()
        {
            Quaternion rotation = EnemyVisualMotionSystem.ComputeApproachTilt(Vector3.forward, 0f);

            // With zero tilt, the yaw-only rotation's forward axis must match the approach direction.
            Vector3 forward = rotation * Vector3.forward;
            Assert.AreEqual(Vector3.forward.x, forward.x, 1e-3f);
            Assert.AreEqual(Vector3.forward.z, forward.z, 1e-3f);
        }

        [Test]
        public void ComputeApproachTilt_AppliesForwardLeanPitch()
        {
            Quaternion levelRotation = EnemyVisualMotionSystem.ComputeApproachTilt(Vector3.forward, 0f);
            Quaternion tiltedRotation = EnemyVisualMotionSystem.ComputeApproachTilt(Vector3.forward, 12f);

            Assert.AreNotEqual(levelRotation, tiltedRotation, "a nonzero tilt angle must change the resulting rotation");
        }
    }
}
