// EnemyApproachSystemTests — S-05: straight-line approach math (gdd 敵・障害物: 常にプレイヤー方向への
// 直線移動、回避・迂回ロジックなし). Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class EnemyApproachSystemTests
    {
        [Test]
        public void ComputeDirection_PointsFromCurrentTowardTarget_NormalizedOnXzPlane()
        {
            Vector3 direction = EnemyApproachSystem.ComputeDirection(
                currentPosition: new Vector3(10f, 0f, 0f), targetPosition: Vector3.zero);

            Assert.AreEqual(-1f, direction.x, 1e-4f);
            Assert.AreEqual(0f, direction.y, 1e-4f);
            Assert.AreEqual(0f, direction.z, 1e-4f);
            Assert.AreEqual(1f, direction.magnitude, 1e-4f);
        }

        [Test]
        public void ComputeDirection_AtTarget_ReturnsZeroInsteadOfNaN()
        {
            Vector3 direction = EnemyApproachSystem.ComputeDirection(Vector3.zero, Vector3.zero);

            Assert.AreEqual(Vector3.zero, direction);
            Assert.IsFalse(float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z));
        }

        [Test]
        public void ComputeDirection_IgnoresYDifference()
        {
            Vector3 direction = EnemyApproachSystem.ComputeDirection(
                currentPosition: new Vector3(0f, 5f, 10f), targetPosition: new Vector3(0f, -3f, 0f));

            Assert.AreEqual(0f, direction.y, 1e-4f, "movement must stay on the XZ plane (Y ignored)");
        }

        [Test]
        public void ComputeStep_MovesTowardTargetByExactlySpeedTimesDeltaTime()
        {
            Vector3 next = EnemyApproachSystem.ComputeStep(
                currentPosition: new Vector3(10f, 0f, 0f), targetPosition: Vector3.zero, speed: 2.5f, deltaTime: 1f);

            Assert.AreEqual(7.5f, next.x, 1e-3f);
            Assert.AreEqual(0f, next.z, 1e-4f);
        }

        [Test]
        public void ComputeStep_ScalesWithDeltaTime()
        {
            Vector3 halfStep = EnemyApproachSystem.ComputeStep(
                currentPosition: new Vector3(10f, 0f, 0f), targetPosition: Vector3.zero, speed: 2.5f, deltaTime: 0.5f);

            Assert.AreEqual(8.75f, halfStep.x, 1e-3f); // moved 1.25m toward the target
        }

        [Test]
        public void ComputeStep_NeverOvershootsTheTarget()
        {
            // Large speed*deltaTime relative to the remaining distance must clamp at the target,
            // not fly past it (would otherwise oscillate around the player every frame).
            Vector3 next = EnemyApproachSystem.ComputeStep(
                currentPosition: new Vector3(1f, 0f, 0f), targetPosition: Vector3.zero, speed: 100f, deltaTime: 1f);

            Assert.AreEqual(Vector3.zero, next);
        }

        [Test]
        public void ComputeStep_AtTarget_StaysPutWithoutNaN()
        {
            Vector3 next = EnemyApproachSystem.ComputeStep(
                currentPosition: Vector3.zero, targetPosition: Vector3.zero, speed: 2.5f, deltaTime: 1f);

            Assert.AreEqual(Vector3.zero, next);
            Assert.IsFalse(float.IsNaN(next.x) || float.IsNaN(next.y) || float.IsNaN(next.z));
        }
    }
}
