// FileSaveStore.cs — File I/O ベースの ISaveStore 実装（S-07・contract §6 / tech-stack-unity.md「セーブ / 永続化」節）。
// この層だけが persistentDataPath / File I/O を触ってよい（規約15）。パース結果の妥当性判定・
// マイグレーションは Systems/Meta（純粋ロジック）に委譲し、この層は「読む/書く/破損時に退避する」ことだけを担う。
//
// 保存: <dir>/save.json へ .tmp 経由のアトミック書込（.tmp に書き切ってから本体へ置換 — 書込中クラッシュで
// 本体を壊さない）。
// 破損時プロトコル（黙示初期化禁止 — rules/unity-code.md）: パース失敗・save_version 欠落/未来版・
// スキーマ検証失敗のいずれも (1) 生データを save.json.bak.<UTC> へ退避 → (2) Debug.LogError
// ("[SaveCorruption] ...") を1回 → (3) 既定値を再生成し recovered=true を返す。
using System;
using System.IO;
using UnityEngine;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Persistence
{
    /// <summary>
    /// directory/fileName はテスト注入用（tech-stack-unity.md テスト規約: PlayMode テストは
    /// Application.temporaryCachePath 配下の一意ディレクトリを使い、persistentDataPath を直接使わない）。
    /// 省略時は本番既定（Application.persistentDataPath / GameConfig.Save.FileName）を使う。
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string directory;
        private readonly string fileName;

        public FileSaveStore(string directory = null, string fileName = null)
        {
            // null = 「省略」→ 本番既定にフォールバックする正当な経路。空文字/空白は呼び出し側の
            // パス構築ミス（例: テストのパス連結バグ）である可能性が高く、黙って本番既定
            // （Application.persistentDataPath / GameConfig.Save.FileName）にフォールバックすると
            // 意図せず実ユーザー領域への書込みに化ける。SetSaveStoreForTest(null) の即時失敗パターン
            // (GameBootstrap/RunOutcomeController) と同じ「黙示無効化の禁止」をここにも適用する
            // （CR-CODE 指摘: 空文字が無警告で本番パスへフォールバックしていた）。
            if (directory != null && string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "[FileSaveStore] directory is empty/whitespace; pass null to use the production default " +
                    "(Application.persistentDataPath) or a real path. An empty string would silently fall back " +
                    "to the production path, masking a path-construction bug.",
                    nameof(directory));
            }
            if (fileName != null && string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "[FileSaveStore] fileName is empty/whitespace; pass null to use the production default " +
                    "(GameConfig.Save.FileName) or a real file name.",
                    nameof(fileName));
            }

            this.directory = directory ?? Application.persistentDataPath;
            this.fileName = fileName ?? GameConfig.Save.FileName;
        }

        private string SavePath => Path.Combine(directory, fileName);
        private string TempPath => SavePath + ".tmp";

        public LoadResult Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                // 初回起動（セーブ無し）。破損ではないので recovered=false（contract §6）。
                return new LoadResult(SaveData.CreateDefault(), false);
            }

            string raw;
            try
            {
                raw = File.ReadAllText(path);
            }
            catch (Exception firstEx)
            {
                // 一過性の I/O エラー（ファイルロック・権限の瞬間的競合等）を破損と誤認しないよう、
                // 即時に1回だけリトライする（CR-CODE 指摘: リトライ無しだと一過性エラーで健全な
                // セーブが破損誤検知され、直後の Result 保存で既定値ベースに上書きされ進行が失われうる）。
                // リトライも失敗した場合のみ ReadFailed として破損復旧プロトコルへ入る。
                // 初回失敗は（回復可能条件のため）LogWarning で1行記録する — 変数なし catch で情報を
                // 完全に破棄すると、恒常的な一過性 I/O 障害の前兆が2連続失敗するまで一切表面化しない
                // （CR-CODE 指摘: 回復不能側の [SaveCorruption] LogError とは別に、回復可能側の痕跡も残す）。
                Debug.LogWarning(
                    $"[SaveReadRetry] first read failed ({firstEx.GetType().Name}); retrying once path={path}");
                try
                {
                    raw = File.ReadAllText(path);
                }
                catch (Exception ex2)
                {
                    // ファイルが存在するのに読めない（権限等）場合もパース不能相当として扱う。
                    // 生データを取得できないため .bak 退避は本体コピーにフォールバックする。
                    return RecoverFromCorruption($"ReadFailed:{ex2.GetType().Name}", rawForBackup: null);
                }
            }

            // JsonUtility はかなり寛容なパーサで、構文的に壊れた入力でも例外を投げず空オブジェクト相当を
            // 返すことがある（Unity 既知挙動）。例外捕捉だけに頼ると「パース不能」を検知し損ねるため、
            // まず軽量な構造チェック（波括弧の対応・文字列内は無視）で明らかな非JSONを弾く。
            if (!LooksLikeJsonObject(raw))
            {
                return RecoverFromCorruption(SaveValidationResult.ParseFailed.ToString(), raw);
            }

            SaveData parsed;
            string parseExceptionDetail = null;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(raw);
            }
            catch (Exception ex)
            {
                // 例外型・メッセージを完全に破棄すると初期診断情報が失われる（CR-CODE 指摘）。
                // reason 自体は既存テストの正規表現 (^\[SaveCorruption\] reason=ParseFailed backup=...)
                // と衝突しないよう、backup パスの後ろに detail= として付加する（RecoverFromCorruption 側）。
                parsed = null;
                parseExceptionDetail = ex.GetType().Name;
            }

            if (parsed == null)
            {
                return RecoverFromCorruption(SaveValidationResult.ParseFailed.ToString(), raw, parseExceptionDetail);
            }

            SaveValidationResult validation = MetaSchema.Validate(parsed);
            if (validation != SaveValidationResult.Valid)
            {
                return RecoverFromCorruption(validation.ToString(), raw);
            }

            SaveData migrated = MetaSchema.Migrate(parsed);
            return new LoadResult(migrated, false);
        }

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(data);
            string tempPath = TempPath;
            string savePath = SavePath;

            File.WriteAllText(tempPath, json);
            try
            {
                if (File.Exists(savePath))
                {
                    // File.Replace はアトミックな置換（POSIX rename 相当）。
                    File.Replace(tempPath, savePath, null);
                }
                else
                {
                    File.Move(tempPath, savePath);
                }
            }
            catch (Exception)
            {
                // 置換に失敗した場合、.tmp を残したままにすると次回起動時に紛らわしい残骸になる。
                // ただし本体(savePath)はまだ書き換わっていない＝壊れていないため、.tmp を掃除した上で
                // 例外を再送出する（黙って成功したことにしない — silent-failure 禁止）。
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch (Exception cleanupEx)
                    {
                        // 掃除失敗自体は元例外(置換失敗)を隠さない（それは下の throw で呼び出し元へ伝わる）が、
                        // 空 catch のままだと .tmp 残留の調査手がかりがゼロになる（CR-CODE 指摘）。1行だけログする。
                        Debug.LogError($"[SaveTempCleanupFailed] path={tempPath} reason={cleanupEx.GetType().Name}");
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// 明らかに JSON オブジェクトでない入力（非JSONテキスト・波括弧の不整合・切り詰められたファイル等）
        /// を弾く軽量構造チェック。フルの JSON 文法検証ではないが、JsonUtility が寛容すぎて例外を
        /// 投げないケース（Unity 既知挙動）の取りこぼしを防ぐ第一防衛線として使う。
        /// </summary>
        private static bool LooksLikeJsonObject(string raw)
        {
            string trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;
            if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}') return false;

            int depth = 0;
            bool inString = false;
            bool escapeNext = false;
            foreach (char c in trimmed)
            {
                if (inString)
                {
                    if (escapeNext) escapeNext = false;
                    else if (c == '\\') escapeNext = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}') depth--;
                if (depth < 0) return false;
            }

            return depth == 0 && !inString;
        }

        /// <summary>
        /// 破損復旧の3点セット: (1) 生データを save.json.bak.<UTC> へ退避 (2) [SaveCorruption] ログ1回
        /// (3) 既定値再生成 + recovered=true。バックアップ自体の失敗は復旧を止めないが、別ログで明示する
        /// （バックアップ失敗を握り潰して「復旧成功」と見せかけない）。
        /// `detail` は任意の追加診断情報（例外型名等。CR-CODE 指摘対応 — reason だけでは失われる情報を残す）。
        /// </summary>
        private LoadResult RecoverFromCorruption(string reason, string rawForBackup, string detail = null)
        {
            string backupPath = $"{SavePath}.bak.{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}";
            bool backupWritten = false;
            try
            {
                Directory.CreateDirectory(directory);
                if (rawForBackup != null)
                {
                    File.WriteAllText(backupPath, rawForBackup);
                    backupWritten = true;
                }
                else if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, backupPath, overwrite: true);
                    backupWritten = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{GameConfig.Save.CorruptionLogPrefix} backup_failed reason={ex.GetType().Name} original_reason={reason}");
            }

            // 主ログの backup= フィールドは「実際に .bak が書けたか」を正直に反映する（CR-CODE 指摘:
            // 従来は退避が失敗しても backup=<存在しないパス> をそのまま出力し、直前の backup_failed ログを
            // 読まない限り退避成功と誤読されていた）。
            string backupField = backupWritten ? backupPath : "FAILED";
            string detailSuffix = detail != null ? $" detail={detail}" : string.Empty;
            Debug.LogError($"{GameConfig.Save.CorruptionLogPrefix} reason={reason} backup={backupField}{detailSuffix}");
            return new LoadResult(SaveData.CreateDefault(), true);
        }
    }
}
