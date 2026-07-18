// PlayerMovementTests — S-04: pure XZ movement math (diagonal normalization, arena clamp).
// Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class PlayerMovementTests
    {
        [Test]
        public void ComputeDisplacement_AxisAlignedInput_MovesAtExactlyMoveSpeed()
        {
            Vector3 displacement = PlayerMovement.ComputeDisplacement(
                inputVector: new Vector2(1f, 0f), moveSpeed: 6f, deltaTime: 1f);

            Assert.AreEqual(6f, displacement.magnitude, 1e-4f);
            Assert.AreEqual(6f, displacement.x, 1e-4f);
            Assert.AreEqual(0f, displacement.y, 1e-4f);
            Assert.AreEqual(0f, displacement.z, 1e-4f);
        }

        [Test]
        public void ComputeDisplacement_DiagonalInput_IsNormalizedAndNeverExceedsMoveSpeed()
        {
            // gdd 操作仕様: 斜め入力はベクトル合成後に正規化しPLAYER_MOVE_SPEEDを超えない.
            // Raw (1,1) has magnitude sqrt(2) > 1 — must be clamped before scaling.
            Vector3 displacement = PlayerMovement.ComputeDisplacement(
                inputVector: new Vector2(1f, 1f), moveSpeed: 6f, deltaTime: 1f);

            Assert.AreEqual(6f, displacement.magnitude, 1e-4f, "diagonal displacement must not exceed moveSpeed");
        }

        [Test]
        public void ComputeDisplacement_ScalesWithDeltaTime()
        {
            Vector3 displacement = PlayerMovement.ComputeDisplacement(
                inputVector: new Vector2(1f, 0f), moveSpeed: 6f, deltaTime: 0.5f);

            Assert.AreEqual(3f, displacement.x, 1e-4f);
        }

        [Test]
        public void ComputeDisplacement_ZeroInput_ProducesNoMovement()
        {
            Vector3 displacement = PlayerMovement.ComputeDisplacement(
                inputVector: Vector2.zero, moveSpeed: 6f, deltaTime: 1f);

            Assert.AreEqual(Vector3.zero, displacement);
        }

        [Test]
        public void ClampToArena_PositionInsideRadius_IsUnchanged()
        {
            var position = new Vector3(3f, 0f, 4f); // distance 5, well within radius 12
            Vector3 clamped = PlayerMovement.ClampToArena(position, arenaRadius: 12f);

            Assert.AreEqual(position, clamped);
        }

        [Test]
        public void ClampToArena_PositionOutsideRadius_IsClampedToRadius()
        {
            var position = new Vector3(20f, 0f, 0f); // outside ARENA_RADIUS=12
            Vector3 clamped = PlayerMovement.ClampToArena(position, arenaRadius: 12f);

            Assert.AreEqual(12f, clamped.x, 1e-4f);
            Assert.AreEqual(0f, clamped.z, 1e-4f);
            var flatDistance = new Vector2(clamped.x, clamped.z).magnitude;
            Assert.LessOrEqual(flatDistance, 12f + 1e-4f);
        }

        [Test]
        public void ClampToArena_PreservesYAxis()
        {
            var position = new Vector3(20f, 1.5f, 0f);
            Vector3 clamped = PlayerMovement.ClampToArena(position, arenaRadius: 12f);

            Assert.AreEqual(1.5f, clamped.y, 1e-4f);
        }

        [Test]
        public void ClampToArena_DiagonalOverflow_ClampsToRadiusAlongSameDirection()
        {
            var position = new Vector3(10f, 0f, 10f); // distance ≈14.14 > radius 12
            Vector3 clamped = PlayerMovement.ClampToArena(position, arenaRadius: 12f);

            var flatDistance = new Vector2(clamped.x, clamped.z).magnitude;
            Assert.AreEqual(12f, flatDistance, 1e-3f);
            // direction preserved: x and z should remain equal (45° direction)
            Assert.AreEqual(clamped.x, clamped.z, 1e-3f);
        }
    }
}
