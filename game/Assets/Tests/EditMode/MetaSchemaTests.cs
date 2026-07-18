// MetaSchemaTests — S-01: セーブスキーマ検証 (Systems/Meta/MetaSchema.Normalize). Conventions.md §9:
// pure Systems get EditMode coverage. Covers the IsSchemaValid range checks that JsonUtility cannot
// enforce — a range-invalid field on an otherwise current-version save must normalize to a defaulted
// SaveData with Corrupt status and the "schema-invalid" reason (the I/O layer maps that to the
// .bak + [SaveCorruption] protocol — Tests/PlayMode/PersistenceStoryTests covers that end-to-end).
using ForgeGame.Systems.Meta;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class MetaSchemaTests
    {
        [Test]
        public void Normalize_NegativeHighScore_ReturnsDefaultedCorruptWithSchemaInvalidReason()
        {
            SaveData data = SaveData.CreateDefault();
            data.highScore = -1;

            SaveLoadOutcome outcome = MetaSchema.Normalize(data);

            Assert.AreEqual(SaveLoadStatus.Corrupt, outcome.Status,
                "a range-invalid field must be treated as corruption, not silently clamped/accepted");
            Assert.AreEqual("schema-invalid", outcome.Reason);
            Assert.AreEqual(0, outcome.Data.highScore,
                "the corrupted field must not leak into the returned (defaulted) SaveData");
            Assert.AreEqual(GameConfig.Save.CurrentSchemaVersion, outcome.Data.save_version);
        }

        [Test]
        public void Normalize_UpgradeAttackLevelAboveMax_ReturnsDefaultedCorruptWithSchemaInvalidReason()
        {
            SaveData data = SaveData.CreateDefault();
            data.upgradeAttackLevel = GameConfig.Upgrade.AttackLevelMax + 1;

            SaveLoadOutcome outcome = MetaSchema.Normalize(data);

            Assert.AreEqual(SaveLoadStatus.Corrupt, outcome.Status,
                "an upgrade level above its GameConfig max must be treated as corruption, not silently clamped/accepted");
            Assert.AreEqual("schema-invalid", outcome.Reason);
            Assert.AreEqual(0, outcome.Data.upgradeAttackLevel,
                "the corrupted field must not leak into the returned (defaulted) SaveData");
            Assert.AreEqual(GameConfig.Save.CurrentSchemaVersion, outcome.Data.save_version);
        }
    }
}
