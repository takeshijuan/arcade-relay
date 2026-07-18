// MetaSchema — migration chain + validation (tech-stack-unity セーブ/永続化). Pure C#.
// Migration functions are append-only: v(n)→v(n+1). A save newer than the current schema is
// treated as corruption (no implicit downgrade). Callers (Persistence) act on the result.
namespace ForgeGame.Systems.Meta
{
    public enum SaveLoadStatus
    {
        Ok = 0,
        Migrated = 1,
        Corrupt = 2, // parse fail / missing save_version / future version / schema-invalid
    }

    public readonly struct SaveLoadOutcome
    {
        public readonly SaveData Data;
        public readonly SaveLoadStatus Status;
        public readonly string Reason;

        public SaveLoadOutcome(SaveData data, SaveLoadStatus status, string reason)
        {
            Data = data;
            Status = status;
            Reason = reason;
        }
    }

    public static class MetaSchema
    {
        /// <summary>
        /// Validate and migrate a just-deserialized SaveData (may be null on parse failure).
        /// Never throws; returns a defaulted SaveData with Corrupt status when unusable so the
        /// I/O layer can perform the .bak + [SaveCorruption] protocol.
        /// </summary>
        public static SaveLoadOutcome Normalize(SaveData parsed)
        {
            if (parsed == null)
            {
                return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Corrupt, "parse-null");
            }

            if (parsed.save_version <= 0)
            {
                return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Corrupt, "missing-or-invalid-save_version");
            }

            if (parsed.save_version > GameConfig.Save.CurrentSchemaVersion)
            {
                return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Corrupt, "future-version");
            }

            bool migrated = false;
            SaveData data = parsed;
            // Apply v(n)→v(n+1) migrations in order. (No migrations yet at schema v1.)
            // while (data.save_version < GameConfig.Save.CurrentSchemaVersion) { ...; migrated = true; }

            if (!IsSchemaValid(data))
            {
                return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Corrupt, "schema-invalid");
            }

            return new SaveLoadOutcome(
                data, migrated ? SaveLoadStatus.Migrated : SaveLoadStatus.Ok, null);
        }

        /// <summary>Range/consistency checks that JsonUtility cannot enforce.</summary>
        private static bool IsSchemaValid(SaveData d)
        {
            if (d.highScore < 0 || d.crystalBalance < 0) return false;
            if (d.totalRunsPlayed < 0 || d.totalKillCount < 0 || d.totalCrystalsEarned < 0) return false;
            if (d.upgradeAttackLevel < 0 || d.upgradeAttackLevel > GameConfig.Upgrade.AttackLevelMax) return false;
            if (d.upgradeMoveSpeedLevel < 0 || d.upgradeMoveSpeedLevel > GameConfig.Upgrade.MoveSpeedLevelMax) return false;
            if (d.upgradeMaxHpLevel < 0 || d.upgradeMaxHpLevel > GameConfig.Upgrade.MaxHpLevelMax) return false;
            return true;
        }
    }
}
