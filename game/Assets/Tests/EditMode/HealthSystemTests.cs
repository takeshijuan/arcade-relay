// HealthSystemTests — S-08: プレイヤーHP・被弾・死亡判定 → Result 遷移 (gdd HP・被弾・死亡判定 P-01,P-03).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the XZ-plane contact-radius test
// and the per-enemy ENEMY_CONTACT_COOLDOWN evaluation (damage gating by contact/invulnerability/timer).
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class HealthSystemTests
    {
        [Test]
        public void IsContacting_WithinRadiusSum_ReturnsTrue()
        {
            float radiusSum = GameConfig.Player.CollisionRadius + GameConfig.Enemy.CollisionRadius;
            Vector3 player = Vector3.zero;
            Vector3 enemy = new Vector3(radiusSum * 0.5f, 0f, 0f);

            Assert.IsTrue(HealthSystem.IsContacting(player, enemy, radiusSum));
        }

        [Test]
        public void IsContacting_ExactlyAtRadiusSum_ReturnsTrue()
        {
            float radiusSum = GameConfig.Player.CollisionRadius + GameConfig.Enemy.CollisionRadius;
            Vector3 player = Vector3.zero;
            Vector3 enemy = new Vector3(radiusSum, 0f, 0f);

            Assert.IsTrue(HealthSystem.IsContacting(player, enemy, radiusSum), "boundary distance must count as contacting");
        }

        [Test]
        public void IsContacting_BeyondRadiusSum_ReturnsFalse()
        {
            float radiusSum = GameConfig.Player.CollisionRadius + GameConfig.Enemy.CollisionRadius;
            Vector3 player = Vector3.zero;
            Vector3 enemy = new Vector3(radiusSum * 3f, 0f, 0f);

            Assert.IsFalse(HealthSystem.IsContacting(player, enemy, radiusSum));
        }

        [Test]
        public void IsContacting_IgnoresYDifference_StaysOnXzPlane()
        {
            float radiusSum = GameConfig.Player.CollisionRadius + GameConfig.Enemy.CollisionRadius;
            Vector3 player = Vector3.zero;
            // Same XZ position, wildly different Y — must still count as contacting (アリーナは XZ 平面).
            Vector3 enemy = new Vector3(0f, 50f, 0f);

            Assert.IsTrue(HealthSystem.IsContacting(player, enemy, radiusSum));
        }

        [Test]
        public void EvaluateContact_ContactingAndCooldownElapsed_AppliesDamageAndResetsCooldown()
        {
            HealthSystem.ContactEvaluation result = HealthSystem.EvaluateContact(
                isContacting: true, cooldownRemaining: 0f, deltaTime: 0.1f, isInvulnerable: false);

            Assert.IsTrue(result.ShouldApplyDamage);
            Assert.AreEqual(GameConfig.Enemy.ContactCooldown, result.CooldownRemaining,
                "a landed hit must re-arm the full ENEMY_CONTACT_COOLDOWN window");
        }

        [Test]
        public void EvaluateContact_CooldownStillActive_NoDamage_ButTicksDown()
        {
            HealthSystem.ContactEvaluation result = HealthSystem.EvaluateContact(
                isContacting: true, cooldownRemaining: 0.5f, deltaTime: 0.1f, isInvulnerable: false);

            Assert.IsFalse(result.ShouldApplyDamage);
            Assert.AreEqual(0.4f, result.CooldownRemaining, 1e-5f);
        }

        [Test]
        public void EvaluateContact_NotContacting_NeverDamages_ButStillTicksDown()
        {
            HealthSystem.ContactEvaluation result = HealthSystem.EvaluateContact(
                isContacting: false, cooldownRemaining: 0.3f, deltaTime: 0.1f, isInvulnerable: false);

            Assert.IsFalse(result.ShouldApplyDamage);
            Assert.AreEqual(0.2f, result.CooldownRemaining, 1e-5f,
                "cooldown must keep ticking down even without contact (so a re-approach isn't unfairly instant)");
        }

        [Test]
        public void EvaluateContact_Invulnerable_NoDamage_EvenWhenContactingAndCooldownElapsed()
        {
            HealthSystem.ContactEvaluation result = HealthSystem.EvaluateContact(
                isContacting: true, cooldownRemaining: 0f, deltaTime: 0.1f, isInvulnerable: true);

            Assert.IsFalse(result.ShouldApplyDamage, "dash無敵窓中は被弾しない — gdd 決定");
        }

        [Test]
        public void EvaluateContact_CooldownNeverGoesNegative()
        {
            HealthSystem.ContactEvaluation result = HealthSystem.EvaluateContact(
                isContacting: false, cooldownRemaining: 0.05f, deltaTime: 0.5f, isInvulnerable: false);

            Assert.AreEqual(0f, result.CooldownRemaining, "delta-time overshoot on a slow frame must floor at 0, never negative");
        }
    }
}
