// FileSaveAdapter — the ONLY place that touches the filesystem for saves (tech-stack-unity
// セーブ/永続化 + contract §11 層分離). Systems/Meta stays pure; this layer serializes,
// writes atomically, and runs the corruption protocol (.bak + [SaveCorruption]).
using System;
using System.IO;
using ForgeGame.Systems.Meta;
using UnityEngine;

namespace ForgeGame.Persistence
{
    public sealed class FileSaveAdapter
    {
        private readonly string _saveDir;

        /// <summary>Default ctor uses persistentDataPath. Tests pass a temp dir (never the real save).</summary>
        public FileSaveAdapter() : this(Application.persistentDataPath) { }

        public FileSaveAdapter(string saveDir)
        {
            _saveDir = saveDir;
        }

        public string SavePath => Path.Combine(_saveDir, GameConfig.Save.FileName);

        /// <summary>
        /// Load + normalize. Missing file → clean default (Ok). Unusable data → .bak retire +
        /// single [SaveCorruption] error + defaults (recovered=true via Corrupt status).
        /// </summary>
        public SaveLoadOutcome Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                // Save() のリネーム窓（旧実装の Delete→Move 間や電源断）で本体が消え .tmp だけが
                // 残るケースの回収路。ただし .tmp は WriteAllText 途中のクラッシュで半書きの可能性も
                // あるため、昇格前にパース+スキーマ検証する（adversarial F-1）: 有効なら正データとして
                // 昇格、無効なら「正セーブは一度も存在しなかった」ので破損扱いにせず no-file（既定値・
                // [SaveCorruption] エラー無し）。無効 .tmp はリネーム退避して再トリガーを防ぐ
                string orphanTmp = path + ".tmp";
                if (File.Exists(orphanTmp) && IsUsableSaveJson(orphanTmp))
                {
                    try
                    {
                        File.Move(orphanTmp, path);
                        Debug.LogWarning("[SaveCorruption] orphaned .tmp recovered as save (previous save rename was interrupted)");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[SaveCorruption] orphaned .tmp recovery failed: " + e.Message);
                        return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Ok, "no-file");
                    }
                }
                else
                {
                    if (File.Exists(orphanTmp))
                    {
                        try
                        {
                            File.Move(orphanTmp, orphanTmp + ".bak");
                            Debug.LogWarning("[SaveCorruption] orphaned .tmp was half-written (crash during first save) — retired as .tmp.bak, starting fresh");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[SaveCorruption] half-written .tmp retire failed: " + e.Message);
                        }
                    }
                    return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Ok, "no-file");
                }
            }

            string raw;
            try
            {
                raw = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                return HandleCorruption(path, null, "read-failed:" + e.Message);
            }

            SaveData parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(raw);
            }
            catch (Exception e)
            {
                return HandleCorruption(path, raw, "json-parse:" + e.Message);
            }

            SaveLoadOutcome outcome = MetaSchema.Normalize(parsed);
            if (outcome.Status == SaveLoadStatus.Corrupt)
            {
                return HandleCorruption(path, raw, outcome.Reason);
            }
            return outcome;
        }

        /// <summary>Atomic write: serialize → .tmp → replace. Never leaves a half-written save.</summary>
        public void Save(SaveData data)
        {
            Directory.CreateDirectory(_saveDir);
            // Stamp the current schema version on every write — MetaTypes.cs's save_version field
            // defaults to the sentinel 0 (not CurrentSchemaVersion) so genuine key-omission is
            // detectable as corruption on load; callers that build a SaveData without explicitly
            // setting save_version (e.g. object initializers) must not persist that sentinel.
            data.save_version = GameConfig.Save.CurrentSchemaVersion;
            string path = SavePath;
            string tmp = path + ".tmp";
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(tmp, json);
            // Delete→Move はその間にクラッシュすると本体無し+.tmp 孤児になり、次回起動が
            // 「初回起動」と誤認して全進行を無症状で失う（Load 側の孤児回収は二重の保険）。
            // File.Replace は同一ボリューム上で rename(2) 相当 = window なし（3引数の
            // File.Move(…, overwrite:true) は .NET Core 3.0+ 専用で Unity の API プロファイルには
            // 存在しない — CS1501）。初回保存のみ本体が無く Replace が使えないため plain Move —
            // その時点では上書きすべき既存進行が存在せず、失うものが無い。
            if (File.Exists(path))
            {
                File.Replace(tmp, path, null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        /// <summary>Validation probe for orphan-.tmp promotion (Load). Any read/parse/schema failure
        /// means "not a usable save" — the caller then treats it as no-file, so the broad catch is
        /// the answer, not a swallowed error (the caller logs the retire path).</summary>
        private static bool IsUsableSaveJson(string tmpPath)
        {
            try
            {
                SaveData parsed = JsonUtility.FromJson<SaveData>(File.ReadAllText(tmpPath));
                return MetaSchema.Normalize(parsed).Status != SaveLoadStatus.Corrupt;
            }
            catch
            {
                return false;
            }
        }

        private SaveLoadOutcome HandleCorruption(string path, string raw, string reason)
        {
            string backup = path + ".bak." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            try
            {
                if (raw != null) File.WriteAllText(backup, raw);
                else if (File.Exists(path)) File.Copy(path, backup, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveCorruption] backup write failed: " + e.Message);
            }
            Debug.LogError($"[SaveCorruption] reason={reason} backup={backup}");
            return new SaveLoadOutcome(SaveData.CreateDefault(), SaveLoadStatus.Corrupt, reason);
        }
    }
}
