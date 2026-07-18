// DashSystemTests — S-07: ダッシュ回避（無敵窓・クールダウン・方向優先順位）(gdd ダッシュ回避 P-01).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the 3-tier direction priority
// (move input > last move direction > opposite of nearest enemy), the no-viable-direction edge case,
// and the timer arithmetic (CanActivate / TickTimer / ComputeDisplacement).
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class DashSystemTests
    {
        [Test]
        public void ResolveDashDirection_PrefersLiveMoveInput_OverLastDirectionAndEnemy()
        {
            // Priority (1): live move input wins even when (2) and (3) point elsewhere.
            Vector2 moveInput = new Vector2(1f, 0f);           // +X
            Vector3 lastMoveDirection = Vector3.forward;        // +Z
            Vector3 nearestEnemyDirection = Vector3.forward;    // +Z (would resolve to -Z under (3))

            Vector3 direction = DashSystem.ResolveDashDirection(
                moveInput, lastMoveDirection, hasNearestEnemy: true, nearestEnemyDirection);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), direction);
        }

        [Test]
        public void ResolveDashDirection_NormalizesDiagonalMoveInput()
        {
            Vector2 moveInput = new Vector2(1f, 1f);

            Vector3 direction = DashSystem.ResolveDashDirection(
                moveInput, Vector3.zero, hasNearestEnemy: false, Vector3.zero);

            Assert.AreEqual(1f, direction.magnitude, 1e-5f, "diagonal input must resolve to a unit-length direction");
        }

        [Test]
        public void ResolveDashDirection_NoMoveInput_FallsBackToLastMoveDirection()
        {
            // Priority (2): no live move input, but the player moved right just before.
            Vector3 lastMoveDirection = new Vector3(2f, 0f, 0f); // not yet normalized on input

            Vector3 direction = DashSystem.ResolveDashDirection(
                Vector2.zero, lastMoveDirection, hasNearestEnemy: true, nearestEnemyDirection: Vector3.forward);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), direction, "must normalize and prefer (2) over (3)");
        }

        [Test]
        public void ResolveDashDirection_NoInputOrHistory_FallsBackToOppositeOfNearestEnemy()
        {
            // Priority (3): never moved yet (ラン開始直後) — dash away from the nearest enemy.
            Vector3 nearestEnemyDirection = new Vector3(0f, 0f, 3f); // enemy is +Z of the player

            Vector3 direction = DashSystem.ResolveDashDirection(
                Vector2.zero, Vector3.zero, hasNearestEnemy: true, nearestEnemyDirection);

            Assert.AreEqual(new Vector3(0f, 0f, -1f), direction, "must dash directly away from the nearest enemy");
        }

        [Test]
        public void ResolveDashDirection_NothingAvailable_ReturnsZero()
        {
            Vector3 direction = DashSystem.ResolveDashDirection(
                Vector2.zero, Vector3.zero, hasNearestEnemy: false, Vector3.zero);

            Assert.AreEqual(Vector3.zero, direction,
                "no move input, no movement history, and no enemy reference point must yield no direction");
        }

        [Test]
        public void ResolveDashDirection_IgnoresYComponentOfInputsAndHistory_StaysOnXzPlane()
        {
            Vector3 lastMoveDirection = new Vector3(0f, 5f, 1f); // Y should never leak into the dash direction

            Vector3 direction = DashSystem.ResolveDashDirection(
                Vector2.zero, lastMoveDirection, hasNearestEnemy: false, Vector3.zero);

            Assert.AreEqual(0f, direction.y, 1e-5f);
        }

        [Test]
        public void CanActivate_TrueWhenCooldownElapsed_FalseWhileCoolingDown()
        {
            Assert.IsTrue(DashSystem.CanActivate(0f));
            Assert.IsTrue(DashSystem.CanActivate(-0.1f), "already-elapsed (negative) remainder must count as ready");
            Assert.IsFalse(DashSystem.CanActivate(0.01f));
            Assert.IsFalse(DashSystem.CanActivate(GameConfig.Player.DashCooldown));
        }

        [Test]
        public void TickTimer_SubtractsDeltaTime_AndFloorsAtZero()
        {
            float afterOneTick = DashSystem.TickTimer(1f, 0.4f);
            Assert.AreEqual(0.6f, afterOneTick, 1e-5f);

            float overshoot = DashSystem.TickTimer(0.1f, 0.5f);
            Assert.AreEqual(0f, overshoot, "timer must never go negative (delta-time overshoot on a slow frame)");
        }

        [Test]
        public void ComputeDisplacement_ScalesDirectionByDashSpeedAndDeltaTime()
        {
            Vector3 displacement = DashSystem.ComputeDisplacement(
                new Vector3(1f, 0f, 0f), GameConfig.Player.DashSpeed, GameConfig.Player.DashDuration);

            // Full-duration displacement must match the gdd 数値表 rationale: DASH_SPEED×DASH_DURATION ≈ 4m.
            Assert.AreEqual(GameConfig.Player.DashSpeed * GameConfig.Player.DashDuration, displacement.magnitude, 1e-4f);
            Assert.AreEqual(0f, displacement.y, 1e-5f);
        }
    }
}
