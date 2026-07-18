// MetaTypes — versioned save payload (gdd セーブデータ方針). Plain class with public fields
// so Unity's JsonUtility can round-trip it (Dictionary unsupported → flat fields only).
// Pure C#: no MonoBehaviour, no File I/O (that lives in Persistence/).
using System;

namespace ForgeGame.Systems.Meta
{
    [Serializable]
    public class SaveData
    {
        // Schema version MUST be first field (tech-stack-unity セーブ/永続化). Other fields camelCase.
        // Sentinel default 0 (NOT CurrentSchemaVersion) — this is load-bearing for corruption
        // detection: JsonUtility.FromJson fills any field absent from the JSON text with the
        // TYPE's field initializer value, so if this defaulted to CurrentSchemaVersion, a save.json
        // with the "save_version" key genuinely omitted would silently deserialize as a *valid*
        // current-version save instead of tripping MetaSchema.Normalize's `save_version <= 0`
        // corruption check (CR-CODE S-01 iteration finding). CreateDefault() and
        // Persistence/FileSaveAdapter.Save() are responsible for stamping the real current
        // version; this field-level default must stay a sentinel.
        public int save_version;

        public int highScore = 0;
        public float bestSurvivalTimeSec = 0f;
        public int bestWaveReached = 0;

        public int totalRunsPlayed = 0;
        public int totalKillCount = 0;
        public int totalCrystalsEarned = 0;

        public int crystalBalance = 0;

        public int upgradeAttackLevel = 0;    // UPG-01
        public int upgradeMoveSpeedLevel = 0; // UPG-02
        public int upgradeMaxHpLevel = 0;     // UPG-03

        public float bgmVolume = GameConfig.Audio.DefaultBgmVolume;
        public float sfxVolume = GameConfig.Audio.DefaultSfxVolume;

        /// <summary>Deep-ish copy for pure-reducer style (fields are all value types).</summary>
        public SaveData Clone()
        {
            return (SaveData)MemberwiseClone();
        }

        public static SaveData CreateDefault()
        {
            return new SaveData { save_version = GameConfig.Save.CurrentSchemaVersion };
        }
    }
}
