// FileAudioSettingsStore.cs — File I/O ベースの IAudioSettingsStore 実装（S-16）。
// FileSaveStore.cs と同じ「.tmp 経由のアトミック書込」パターンを踏襲する。この層だけが
// Application.persistentDataPath / File I/O を触ってよい（tech-stack-unity.md 規約15）。
// 破損時プロトコル: contract §6 の3点セット（.bak退避・[SaveCorruption]ログ・recovered伝播）は
// 「メタ進行セーブデータ」（SaveData/save_version）専用の規約であり、UI設定専用の本ストアには
// 適用義務が無い（IAudioSettingsStore.cs 冒頭コメント参照）。読込失敗時は1回 Warning を出し既定値へ
// フォールバックする（黙って握り潰さない）。
//
// CR-CODE iter1 #1（major）対応: JsonUtility は寛容なパーサで、`{}` やフィールド欠落・切詰め等
// 「パース可能だが不正」な入力でも例外なし・非nullで BgmVolume=SfxVolume=0（デフォルト float）を
// 返してしまう（FileSaveStore.cs の同種既知挙動と同じ）。これを黙って採用すると Warning すら出さずに
// 毎起動ミュートになるため、FileSaveStore.LooksLikeJsonObject 相当の軽量構造チェックに加え、
// 必須フィールドキーの実在チェックを Load() に追加する（欠落時は Warning 1回 + 既定値）。
// CR-CODE iter1 #4（minor）対応: FileSaveStore.cs が一過性 I/O エラー対策として持つ「読込1回リトライ」
// も同型で追加する（一過性エラーを破損と誤認し、直後のスライダー操作で健全な設定が上書き消失するのを防ぐ）。
// CR-CODE iter2 #2（minor）対応: iter1 #1 の HasRequiredFields はキー実在のみを見て値の型までは見ないため、
// `{"BgmVolume":"1.0","SfxVolume":"0.5"}` のような手動改変（数値をクォートで括った型不一致）が素通りし、
// JsonUtility.FromJson が型不一致フィールドを例外無く既定値 0（ミュート）のまま残す寛容挙動の残存経路になる。
// HasNumericFieldTypes で「フィールド名の直後の値がクォート開始でない」ことまで確認するチェックを追加する
// （既存チェックを緩めない・厳格化のみの追加のため既存の正常系入力への影響は無い）。
// CR-CODE iter2 #4（minor）対応: 本コメント群の指摘番号を state/reviews/s-16.md の iteration 1 記載順
// （1=パース可能だが不正/major, 2=デバウンス/minor, 3=テスト非対称/minor, 4=リトライ/minor,
// 5=null契約/minor, 6=.meta/minor）に合わせて振り直した（旧: #3→#1, #5→#4。iter2 指摘の相互参照矛盾対応）。
using System;
using System.IO;
using UnityEngine;
using ForgeGame;

namespace ForgeGame.Persistence
{
    /// <summary>
    /// directory/fileName はテスト注入用（tech-stack-unity.md テスト規約: PlayMode テストは
    /// Application.temporaryCachePath 配下の一意ディレクトリを使い、persistentDataPath を直接使わない）。
    /// 省略時は本番既定（Application.persistentDataPath / GameConfig.Save.AudioSettingsFileName）を使う。
    /// </summary>
    public sealed class FileAudioSettingsStore : IAudioSettingsStore
    {
        private readonly string directory;
        private readonly string fileName;

        public FileAudioSettingsStore(string directory = null, string fileName = null)
        {
            // FileSaveStore.cs と同じ「空文字/空白は黙示フォールバックさせない」ガード。
            if (directory != null && string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "[FileAudioSettingsStore] directory is empty/whitespace; pass null to use the production " +
                    "default (Application.persistentDataPath) or a real path.", nameof(directory));
            }
            if (fileName != null && string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "[FileAudioSettingsStore] fileName is empty/whitespace; pass null to use the production " +
                    "default (GameConfig.Save.AudioSettingsFileName) or a real file name.", nameof(fileName));
            }

            this.directory = directory ?? Application.persistentDataPath;
            this.fileName = fileName ?? GameConfig.Save.AudioSettingsFileName;
        }

        private string SavePath => Path.Combine(directory, fileName);
        private string TempPath => SavePath + ".tmp";

        public AudioSettingsData Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                // 初回起動（設定ファイル無し）。破損ではないため Warning は出さない。
                return AudioSettingsData.CreateDefault();
            }

            string raw;
            try
            {
                raw = File.ReadAllText(path);
            }
            catch (Exception firstEx)
            {
                // FileSaveStore.cs と同じ「一過性 I/O エラーを破損と誤認しない」ための1回リトライ。
                Debug.LogWarning(
                    $"[AudioSettingsReadRetry] first read failed ({firstEx.GetType().Name}); retrying once path={path}");
                try
                {
                    raw = File.ReadAllText(path);
                }
                catch (Exception ex2)
                {
                    Debug.LogWarning($"[AudioSettingsLoad] failed to read {path} after retry ({ex2.GetType().Name}); using defaults.");
                    return AudioSettingsData.CreateDefault();
                }
            }

            // 「パース可能だが不正」（`{}`・必須フィールド欠落・切詰め等）を弾く軽量チェック
            // （FileSaveStore.LooksLikeJsonObject と同型 + 必須キー実在チェック）。これを通さないと
            // JsonUtility が例外なし・非nullで BgmVolume=SfxVolume=0 を返し、Warning すら出さずに
            // ミュートへ黙って劣化する。
            if (!LooksLikeJsonObject(raw) || !HasRequiredFields(raw) || !HasNumericFieldTypes(raw))
            {
                Debug.LogWarning($"[AudioSettingsLoad] {path} is not a valid AudioSettingsData JSON object " +
                    "(malformed / missing BgmVolume or SfxVolume field / non-numeric field value); using defaults.");
                return AudioSettingsData.CreateDefault();
            }

            AudioSettingsData parsed;
            try
            {
                parsed = JsonUtility.FromJson<AudioSettingsData>(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioSettingsLoad] failed to parse {path} ({ex.GetType().Name}); using defaults.");
                return AudioSettingsData.CreateDefault();
            }

            if (parsed == null)
            {
                Debug.LogWarning($"[AudioSettingsLoad] {path} parsed to null; using defaults.");
                return AudioSettingsData.CreateDefault();
            }

            // 値域サニティ（スライダー min/max は 0..1 だが手動改変ファイル対策として防御的にクランプする）。
            parsed.BgmVolume = Mathf.Clamp01(parsed.BgmVolume);
            parsed.SfxVolume = Mathf.Clamp01(parsed.SfxVolume);
            return parsed;
        }

        /// <summary>
        /// 明らかに JSON オブジェクトでない入力を弾く軽量構造チェック（FileSaveStore.cs の同名メソッドと
        /// 同型・意図的な複製。Persistence 層内で独立した2実装に留め、他ストーリー担当ファイル
        /// [FileSaveStore.cs] への非追記編集を避ける）。
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
        /// 必須フィールドキー（"BgmVolume"/"SfxVolume"）が生テキストに実在するかの軽量チェック。
        /// JsonUtility は欠落フィールドを黙って既定値 0 にするため、キー実在の確認が無いと
        /// `{}` や `{"foo":1}` のような入力を「有効な0.0設定」として誤って採用してしまう。
        /// </summary>
        private static bool HasRequiredFields(string raw) =>
            raw.Contains("\"BgmVolume\"") && raw.Contains("\"SfxVolume\"");

        // CR-CODE iter2 #2（minor）対応: フィールド名の直後（空白/コロン/空白を挟んで）の値がクォート
        // 開始（文字列型）になっていないかを確認する。JsonUtility は数値フィールドへの文字列値を
        // 例外無く既定値 0 のまま残す（型不一致の黙示握り潰し）ため、この形の入力だけを狙って弾く
        // 軽量チェック（既存 HasRequiredFields を緩めない・追加の厳格化のみ）。
        private static readonly System.Text.RegularExpressions.Regex BgmVolumeQuotedValue =
            new System.Text.RegularExpressions.Regex("\"BgmVolume\"\\s*:\\s*\"", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex SfxVolumeQuotedValue =
            new System.Text.RegularExpressions.Regex("\"SfxVolume\"\\s*:\\s*\"", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool HasNumericFieldTypes(string raw) =>
            !BgmVolumeQuotedValue.IsMatch(raw) && !SfxVolumeQuotedValue.IsMatch(raw);

        public void Save(AudioSettingsData data)
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
                // 置換失敗時、本体(savePath)はまだ書き換わっていない＝壊れていない。.tmp を掃除した上で
                // 例外を再送出する（黙って成功したことにしない — silent-failure 禁止）。
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch (Exception cleanupEx)
                    {
                        Debug.LogError($"[AudioSettingsTempCleanupFailed] path={tempPath} reason={cleanupEx.GetType().Name}");
                    }
                }
                throw;
            }
        }
    }
}
