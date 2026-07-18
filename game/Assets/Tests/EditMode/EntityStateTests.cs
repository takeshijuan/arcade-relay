// EntityStateTests — S-06: EntityState.ApplyDamage pure reducer (Types.cs), shared by
// Components/EnemyAgent (敵HP・撃破) and a later story's HealthSystem (player HP). Conventions.md §9:
// pure logic gets EditMode coverage.
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class EntityStateTests
    {
        [Test]
        public void ApplyDamage_SubtractsDamageFromHp()
        {
            var state = new EntityState(hp: 40, maxHp: 40);

            EntityState next = state.ApplyDamage(20);

            Assert.AreEqual(20, next.Hp);
            Assert.AreEqual(40, next.MaxHp, "MaxHp must be unaffected by damage");
        }

        [Test]
        public void ApplyDamage_FloorsAtZero_NeverGoesNegative()
        {
            var state = new EntityState(hp: 10, maxHp: 40);

            EntityState next = state.ApplyDamage(999);

            Assert.AreEqual(0, next.Hp);
        }

        [Test]
        public void ApplyDamage_AtExactlyZero_IsDeadTrue()
        {
            var state = new EntityState(hp: 20, maxHp: 40);

            EntityState next = state.ApplyDamage(20);

            Assert.IsTrue(next.IsDead);
        }

        [Test]
        public void ApplyDamage_IsPure_OriginalStateUnchanged()
        {
            var state = new EntityState(hp: 40, maxHp: 40);

            state.ApplyDamage(20);

            Assert.AreEqual(40, state.Hp, "ApplyDamage must not mutate the receiver (pure function)");
        }

        [Test]
        public void ApplyDamage_EnemyHpBase_DiesAfterExactlyTwoAutoAttackHits()
        {
            // S-06 acceptance: ENEMY_HP_BASE (40) を AUTO_ATTACK_DAMAGE_BASE (20) 刻みで2発撃破 (TTK≈1.2s).
            var state = new EntityState(GameConfig.Enemy.HpBase, GameConfig.Enemy.HpBase);

            state = state.ApplyDamage(GameConfig.Player.AutoAttackDamageBase);
            Assert.IsFalse(state.IsDead, "must survive the first hit");
            Assert.AreEqual(GameConfig.Enemy.HpBase - GameConfig.Player.AutoAttackDamageBase, state.Hp);

            state = state.ApplyDamage(GameConfig.Player.AutoAttackDamageBase);
            Assert.IsTrue(state.IsDead, "must die on exactly the second hit");
            Assert.AreEqual(0, state.Hp);
        }
    }
}
