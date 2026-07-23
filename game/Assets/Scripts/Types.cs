// Types.cs — シーン/システム横断の共有型。エンジン非依存（純粋 C#・値型のみ）。
namespace ForgeGame
{
    /// <summary>タワー種別（P-02: 二種の役割分担）。</summary>
    public enum TowerType
    {
        BastionCannon = 0, // 単体高火力
        ArcEmitter = 1,    // 範囲低火力
    }

    /// <summary>敵種別。</summary>
    public enum EnemyType
    {
        Marauder = 0, // 速い・低HP
        Warbeast = 1, // 遅い・高HP
    }

    /// <summary>ラン終了の結果種別。</summary>
    public enum RunOutcome
    {
        Win = 0,  // 全 WAVE_COUNT 防衛かつ coreHp>0
        Loss = 1, // coreHp<=0
    }

    /// <summary>
    /// ラン間アップグレード種別（UPG-01〜03。gdd「持ち越しアップグレード」節・S-14）。
    /// Systems/Meta/MetaProgression.TryPurchaseUpgrade / ComputeStartingGold / ComputeTowerDiscountRate の
    /// 引数・SaveData の upgXxxLv フィールドとの対応に使う共有 enum。
    /// </summary>
    public enum UpgradeKind
    {
        Upg01StartingGold = 0,  // 初期資金 +15/Lv（SaveData.upgStartingGoldLv）
        Upg02TowerDiscount = 1, // 設置/強化コスト割引 -3%/Lv（SaveData.upgTowerDiscountLv）
        Upg03EssenceRate = 2,   // essence獲得率 +8%/Lv（SaveData.upgEssenceRateLv）
    }

    /// <summary>
    /// ラン終了時にメタ進行へ渡す確定結果（純粋値。I/O なし）。
    /// gdd「メタ進行」節の入力 RunResult に対応。MetaProgression.ApplyRunResult の入力。
    /// </summary>
    [System.Serializable]
    public struct RunResult
    {
        public bool IsWin;             // 勝利か
        public int CoreHpRemaining;    // 残コアHP（敗北時 0）
        public int KillCount;          // 総撃破数（種別問わず）
        public int AoeKillCount;       // Arc Emitter 帰属の撃破数（ACH-04・撃破帰属ルール）
        public int UsedBuildSpots;     // このランで使用したビルドスポット数（ACH-05）
        public float ClearTimeSec;     // ラン開始〜終了の経過秒（スコア/ベストタイム）
    }

    /// <summary>
    /// 音量設定（BGM/SFX。S-16）。Persistence/IAudioSettingsStore が保存/復元する軽量な UI 設定値。
    /// メタ進行 SaveData（save_version 管理・contract §6 の破損3点セット対象）とは別ファイルに分離保存する
    /// （Persistence/IAudioSettingsStore.cs 冒頭コメント参照）。RunResult と同様 PascalCase フィールド
    /// （SaveData の gdd 名称一致・snake_case 例外は SaveData 固有のため、本型には適用しない）。
    /// </summary>
    [System.Serializable]
    public class AudioSettingsData
    {
        public float BgmVolume;
        public float SfxVolume;

        /// <summary>初期値（未保存時）。GameConfig.Ui.MenuDefaultVolume と一致。</summary>
        public static AudioSettingsData CreateDefault() => new AudioSettingsData
        {
            BgmVolume = GameConfig.Ui.MenuDefaultVolume,
            SfxVolume = GameConfig.Ui.MenuDefaultVolume,
        };
    }
}
