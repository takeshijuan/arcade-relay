// HeavyEnemySystemTests — S-14: ヘヴィスウォーマー変種（任意・見た目/ステータス差分のみ）.
// Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class HeavyEnemySystemTests
    {
        [Test]
        public void IsUnlocked_BelowUnlockWave_ReturnsFalse()
        {
            Assert.IsFalse(HeavyEnemySystem.IsUnlocked(GameConfig.Enemy.HeavyUnlockWave - 1));
        }

        [Test]
        public void IsUnlocked_AtOrAboveUnlockWave_ReturnsTrue()
        {
            Assert.IsTrue(HeavyEnemySystem.IsUnlocked(GameConfig.Enemy.HeavyUnlockWave));
            Assert.IsTrue(HeavyEnemySystem.IsUnlocked(GameConfig.Enemy.HeavyUnlockWave + 5));
        }

        [Test]
        public void ShouldSpawnHeavy_BeforeUnlock_NeverTrue_EvenWithARollThatWouldOtherwiseQualify()
        {
            // A roll of 0 always clears any positive HeavySpawnChance threshold — this isolates the
            // unlock-wave gate from the probability gate.
            Assert.IsFalse(HeavyEnemySystem.ShouldSpawnHeavy(GameConfig.Enemy.HeavyUnlockWave - 1, randomValue01: 0f));
        }

        [Test]
        public void ShouldSpawnHeavy_AfterUnlock_RollBelowChance_ReturnsTrue()
        {
            float rollBelowChance = GameConfig.Enemy.HeavySpawnChance * 0.5f;
            Assert.IsTrue(HeavyEnemySystem.ShouldSpawnHeavy(GameConfig.Enemy.HeavyUnlockWave, rollBelowChance));
        }

        [Test]
        public void ShouldSpawnHeavy_AfterUnlock_RollAtOrAboveChance_ReturnsFalse()
        {
            Assert.IsFalse(HeavyEnemySystem.ShouldSpawnHeavy(
                GameConfig.Enemy.HeavyUnlockWave, GameConfig.Enemy.HeavySpawnChance));
            Assert.IsFalse(HeavyEnemySystem.ShouldSpawnHeavy(GameConfig.Enemy.HeavyUnlockWave, randomValue01: 0.999f));
        }

        [Test]
        public void AdjustedSpeed_AppliesHeavySpeedMult()
        {
            float baseSpeed = GameConfig.Enemy.MoveSpeedBase;
            float expected = baseSpeed * GameConfig.Enemy.HeavySpeedMult;
            Assert.AreEqual(expected, HeavyEnemySystem.AdjustedSpeed(baseSpeed), 1e-4f);
            Assert.Less(HeavyEnemySystem.AdjustedSpeed(baseSpeed), baseSpeed, "gdd: HEAVY_ENEMY_SPEED_MULTで減速 (<1.0)");
        }

        [Test]
        public void AdjustedHp_AppliesHeavyHpMult_AndRounds()
        {
            int baseHp = GameConfig.Enemy.HpBase;
            int expected = UnityEngine.Mathf.RoundToInt(baseHp * GameConfig.Enemy.HeavyHpMult);
            Assert.AreEqual(expected, HeavyEnemySystem.AdjustedHp(baseHp));
            Assert.Greater(HeavyEnemySystem.AdjustedHp(baseHp), baseHp, "gdd: 最大HPにHEAVY_ENEMY_HP_MULTを適用 (>1.0)");
        }

        [Test]
        public void AdjustedContactDamage_AppliesHeavyContactDamageMult_AndRounds()
        {
            int baseDamage = GameConfig.Enemy.ContactDamage;
            int expected = UnityEngine.Mathf.RoundToInt(baseDamage * GameConfig.Enemy.HeavyContactDamageMult);
            Assert.AreEqual(expected, HeavyEnemySystem.AdjustedContactDamage(baseDamage));
            Assert.Greater(HeavyEnemySystem.AdjustedContactDamage(baseDamage), baseDamage,
                "gdd: 接触ダメージにHEAVY_ENEMY_CONTACT_DAMAGE_MULTを適用 (>1.0)");
        }
    }
}
