// MetaTypes.cs — メタ進行のバージョン別プレーン型（純粋 C#・contract §11 / tech-stack-unity.md セーブ節）。
// JsonUtility でのフラット表現前提: 全フィールドはプリミティブ(int/float/bool)・ネストなし。
namespace ForgeGame.Systems.Meta
{
    /// <summary>
    /// 永続セーブデータ（gdd「セーブデータ方針」節の正本）。
    /// 注意: JSON 先頭キーは contract §6 規定どおり `save_version`。JsonUtility はキー名を
    /// フィールド名からそのまま生成し remap 不可のため、この1フィールドのみ C# 命名規約を外れて
    /// snake_case とする（docs/conventions.md 参照）。他フィールドは gdd 表の名称に一致。
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int save_version;          // 先頭フィールド必須（contract §6）
        public int highScore;             // 最高 finalScore
        public float bestClearTimeSec;    // 勝利ラン最短。未記録は -1（Menu/Result で「--」表示）
        public int totalRunsPlayed;       // 総プレイ回数
        public int totalWins;             // 総勝利数
        public int totalKills;            // 総撃破数（累計・ACH-03 判定）
        public int essence;               // 所持 essence（UPG 購入原資）
        public int upgStartingGoldLv;     // UPG-01 Lv
        public int upgTowerDiscountLv;    // UPG-02 Lv
        public int upgEssenceRateLv;      // UPG-03 Lv
        public bool achFirstVictory;      // ACH-01
        public bool achPerfectDefense;    // ACH-02
        public bool achCenturySlayer;     // ACH-03
        public bool achAoeSpecialist;     // ACH-04
        public bool achFrugalArchitect;   // ACH-05

        /// <summary>初回起動時（セーブ無し）の既定値。gdd「セーブデータ方針」表の初期値と一致。</summary>
        public static SaveData CreateDefault() => new SaveData
        {
            save_version = GameConfig.Save.CurrentVersion,
            highScore = 0,
            bestClearTimeSec = -1f,
            totalRunsPlayed = 0,
            totalWins = 0,
            totalKills = 0,
            essence = 0,
            upgStartingGoldLv = 0,
            upgTowerDiscountLv = 0,
            upgEssenceRateLv = 0,
            achFirstVictory = false,
            achPerfectDefense = false,
            achCenturySlayer = false,
            achAoeSpecialist = false,
            achFrugalArchitect = false,
        };

        /// <summary>浅いコピー（純粋 reducer が破壊的更新を避けるため）。</summary>
        public SaveData Clone() => (SaveData)MemberwiseClone();
    }

    /// <summary>
    /// セーブロードの結果。破損復旧の有無を UI 層（Title/Menu）へ伝える recovered フラグを同伴する。
    /// recovered は SaveData に永続化されず、ロード時に都度生成される（gdd「セーブデータ方針」注記）。
    /// </summary>
    public readonly struct LoadResult
    {
        public readonly SaveData Data;
        public readonly bool Recovered;

        public LoadResult(SaveData data, bool recovered)
        {
            Data = data;
            Recovered = recovered;
        }
    }
}
