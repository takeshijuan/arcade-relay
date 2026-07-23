// MetaSchema.cs — セーブスキーマの検証とマイグレーション（純粋 C#・追加のみ/書き換え禁止）。
// tech-stack-unity.md「セーブ / 永続化」節: v(n)→v(n+1) を順に適用。現行より新しい版は破損相当。
namespace ForgeGame.Systems.Meta
{
    /// <summary>破損判定の理由（Persistence 層が [SaveCorruption] ログの reason に埋める）。</summary>
    public enum SaveValidationResult
    {
        Valid = 0,
        ParseFailed = 1,        // JSON パース不能（Persistence 層が判定）
        VersionMissing = 2,     // save_version が既定(0)＝欠落
        FutureVersion = 3,      // 現行より新しい版（暗黙ダウングレード禁止）
        SchemaInvalid = 4,      // 必須フィールドの型/値が不正
    }

    /// <summary>スキーマ検証・マイグレーションの純粋ロジック。I/O は Persistence 層が担う。</summary>
    public static class MetaSchema
    {
        /// <summary>
        /// パース済み SaveData の妥当性を検査する。パース失敗は Persistence 層で別途 ParseFailed 判定。
        /// スキーマ検証失敗（必須フィールド欠落・型不正・値域外）も破損として扱う（contract §6）。
        /// </summary>
        public static SaveValidationResult Validate(SaveData data)
        {
            if (data == null) return SaveValidationResult.ParseFailed;
            if (data.save_version <= 0) return SaveValidationResult.VersionMissing;
            if (data.save_version > GameConfig.Save.CurrentVersion) return SaveValidationResult.FutureVersion;
            // 値域サニティ（負値禁止のカウンタ等）。将来フィールド追加時はここに検査を足す。
            if (data.totalRunsPlayed < 0 || data.totalWins < 0 || data.totalKills < 0 || data.essence < 0)
                return SaveValidationResult.SchemaInvalid;
            if (data.upgStartingGoldLv < 0 || data.upgTowerDiscountLv < 0 || data.upgEssenceRateLv < 0)
                return SaveValidationResult.SchemaInvalid;
            return SaveValidationResult.Valid;
        }

        /// <summary>
        /// 旧版データを現行版へ順次マイグレーションする。v1 が現行のため現状は恒等変換。
        /// v(n)→v(n+1) 関数は将来「追加のみ」で連ねる（既存関数の書き換え禁止）。
        /// </summary>
        public static SaveData Migrate(SaveData data)
        {
            // 例: while (data.save_version < 2) data = MigrateV1ToV2(data);
            return data;
        }
    }
}
