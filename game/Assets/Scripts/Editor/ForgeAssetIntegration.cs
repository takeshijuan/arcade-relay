// ForgeAssetIntegration.cs — Integrate（直列区間）専用の資産取込検証ツール。
// gates.md AR-ASSET ※節「エンジン取込後検証はIntegrate実施者の責務」に対応する。
// -executeMethod から呼ばれ、AssetDatabase を確定させた上で:
//   1) 画像資産（icon-*）の TextureImporter 設定を UI 用途向けに補正する（Sprite / アルファ透過保持）。
//   2) 3D モデル（GameConfig.AssetKeys.Model*）を Resources.Load してインスタンス化し、
//      Renderer の合成 bounds を実測して GameConfig.Presentation の想定高さ（MANIFEST bbox_authoring_m
//      と同じ authoring スケール）との整合を検証する（assets-config.md「取込後にエンジン内バウンディング
//      ボックスを bbox_authoring_m と突合して再検証」）。
//   3) 音声（SFX-01〜06・BGM-01）の存在有無、および残る未生成資産（タイル・アイコンシート）の欠落有無を
//      機械的に列挙する（design/assets.md status=planned の既知ギャップはプレースホルダのまま残す前提 —
//      ビルド失敗にはしない。既存資産が壊れている場合のみ Exit(1)）。
// 規約11（tech-stack-unity.md）: 回復不能エラーは Debug.LogError + return で済ませず例外/Exit(1) で
// 非0終了させる。ここでは「取込済みのはずの資産に Renderer が無い」等の構造的破損のみを回復不能とみなす。
// CR-CODE S-19 iter1 minor #4 対応: MDL-01〜05・SFX-01〜06・BGM-01 は本 story で「取込済みであるべき資産」
// になったため、report 上は PLANNED（design/assets.md status=planned の既知ギャップ）と区別できる
// "MISSING_INTEGRATED" ステータス語で出す（CheckModels/CheckAudio）。
// CR-CODE S-19 iter2 minor #3 対応: モデル（MDL）の MISSING_INTEGRATED は引き続き hardFailure（Exit(1)）へ
// 昇格しない。MDL-04 は GLTFast の ScriptedImporter インポート結果が同一 Editor セッション内で確定しない
// 既知のタイミング差異があり（stories.yaml S-19 impl note）、直後の一括検証実行では「まだインポートが
// 確定していないだけ」で「壊れている」ではない誤検知が起こり得るため。一方、音声（SFX/BGM）の import は
// 同種の非同期確定タイミング問題を持たない（AudioImporter は同期的に確定する）ため、harness 側に
// MISSING_INTEGRATED を構造化返却として消費する経路が無い現状（.claude/workflows/prototype.js・
// full-build.js に該当パース無し）を踏まえ、SFX/BGM の MISSING_INTEGRATED は hardFailure（Exit(1)）へ
// 昇格させる（CheckAudio）。構造的破損の確定シグナルは NO_RENDERER（インポート済みだが Renderer が無い＝
// 壊れている、モデルのみ）に加え、SFX/BGM の MISSING_INTEGRATED も対象とする。
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ForgeGame;

namespace ForgeGame.EditorTools
{
    public static class ForgeAssetIntegration
    {
        private struct ModelCheck
        {
            public string Key;
            public string Name;
            public float ExpectedHeightM;
            public float ToleranceM;
        }

        /// <summary>
        /// 検証コマンド: "$UNITY" -batchmode -projectPath game
        ///   -executeMethod ForgeGame.EditorTools.ForgeAssetIntegration.RunIntegrationCheck -quit
        /// </summary>
        public static void RunIntegrationCheck()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                ConfigureIconImporters();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                var report = new StringBuilder();
                bool hardFailure = CheckModels(report);
                CheckTextures(report);
                // CR-CODE S-19 iter2 minor #3: 音声の MISSING_INTEGRATED はモデルと異なり hardFailure に
                // 昇格させる（上記ヘッダコメント参照）。
                hardFailure |= CheckAudio(report);
                CheckKnownGaps(report);

                string text = report.ToString();
                Debug.Log(text);
                WriteReportFile(text);

                EditorApplication.Exit(hardFailure ? 1 : 0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ForgeAssetIntegration] unrecoverable failure during asset integration check: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureIconImporters()
        {
            string[] iconPaths =
            {
                "Assets/Resources/Generated/textures/icon-tower-select.png",
                "Assets/Resources/Generated/textures/icon-essence.png",
                "Assets/Resources/Generated/textures/icon-core-hp.png",
            };

            foreach (string path in iconPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[ForgeAssetIntegration] TextureImporter not found at \"{path}\" (asset missing or not yet imported).");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static bool CheckModels(StringBuilder report)
        {
            var checks = new[]
            {
                new ModelCheck { Key = GameConfig.AssetKeys.ModelBastionCannon, Name = "MDL-01 BastionCannon", ExpectedHeightM = GameConfig.Presentation.BastionCannonHeightM, ToleranceM = 0.5f },
                new ModelCheck { Key = GameConfig.AssetKeys.ModelArcEmitter, Name = "MDL-02 ArcEmitter", ExpectedHeightM = GameConfig.Presentation.ArcEmitterHeightM, ToleranceM = 0.5f },
                new ModelCheck { Key = GameConfig.AssetKeys.ModelMarauder, Name = "MDL-03 Marauder", ExpectedHeightM = GameConfig.Presentation.MarauderHeightM, ToleranceM = 0.3f },
                new ModelCheck { Key = GameConfig.AssetKeys.ModelWarbeast, Name = "MDL-04 Warbeast", ExpectedHeightM = GameConfig.Presentation.WarbeastHeightM, ToleranceM = 0.3f },
                new ModelCheck { Key = GameConfig.AssetKeys.ModelCoreCrystal, Name = "MDL-05 CoreCrystal", ExpectedHeightM = GameConfig.Presentation.CoreHeightM, ToleranceM = 0.5f },
            };

            report.AppendLine("[ForgeAssetIntegration] === model bounds check (bbox_authoring_m re-verification) ===");
            bool hardFailure = false;

            foreach (ModelCheck check in checks)
            {
                GameObject prefab = Resources.Load<GameObject>(check.Key);
                if (prefab == null)
                {
                    // CR-CODE S-19 iter1 minor #4: PLANNED（design/assets.md status=planned の既知ギャップ）
                    // と区別できるステータス語。取込済みであるべき資産のため人間判断（Checkpoint）向けに
                    // 明示するが、GLTFast インポート確定タイミングの既知の揺れがあるため hardFailure にはしない
                    // （上記ヘッダコメント参照）。
                    report.AppendLine($"MISSING_INTEGRATED\t{check.Name}\tkey={check.Key}");
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0)
                    {
                        report.AppendLine($"NO_RENDERER\t{check.Name}\tkey={check.Key}\t(asset present but no Renderer — import likely broken)");
                        hardFailure = true;
                        continue;
                    }

                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    float measuredHeightM = bounds.size.y;
                    float diffM = Mathf.Abs(measuredHeightM - check.ExpectedHeightM);
                    string status = diffM <= check.ToleranceM ? "PASS" : "OUT_OF_TOLERANCE";

                    report.AppendLine(
                        $"{status}\t{check.Name}\tkey={check.Key}\tmeasuredHeightM={measuredHeightM:F4}" +
                        $"\texpectedHeightM={check.ExpectedHeightM:F4}\ttoleranceM={check.ToleranceM:F2}" +
                        $"\tsizeXYZ=({bounds.size.x:F4},{bounds.size.y:F4},{bounds.size.z:F4})" +
                        "\trigType=none(no Avatar/Animator check applicable)");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            return hardFailure;
        }

        private static void CheckTextures(StringBuilder report)
        {
            report.AppendLine("[ForgeAssetIntegration] === texture presence check ===");
            CheckResource<Texture2D>(report, GameConfig.AssetKeys.IconTowerSelect, "IMG-03 IconTowerSelect");
            CheckResource<Texture2D>(report, GameConfig.AssetKeys.IconEssence, "IMG-04 IconEssence");
            CheckResource<Texture2D>(report, GameConfig.AssetKeys.IconCoreHp, "IMG-08 IconCoreHp");
            // batch-verify(Build) 2026-07-22: IMG-01/02 は AR-ASSET APPROVE 済み（state/reviews/assets-images.md
            // iteration2）のため Assets/Resources/Generated/textures/ へ取込み、CheckKnownGaps の PLANNED から
            // ここへ移動した（重複計上しない）。
            CheckResource<Texture2D>(report, GameConfig.AssetKeys.TileGrass, "IMG-01 TileGrass");
            CheckResource<Texture2D>(report, GameConfig.AssetKeys.TileDirtPath, "IMG-02 TileDirtPath");
        }

        private static bool CheckAudio(StringBuilder report)
        {
            report.AppendLine("[ForgeAssetIntegration] === audio presence check ===");
            // CR-CODE S-19 iter1 minor #4 / iter2 minor #3: SFX-01〜06・BGM-01 は本 story で
            // 「取込済みであるべき資産」になったため、PLANNED（既知ギャップ）と区別できる MISSING_INTEGRATED
            // ステータス語で出す。モデルと異なり音声インポートに既知の非同期確定タイミング問題は無いため、
            // ここでの MISSING_INTEGRATED はそのまま hardFailure に反映する（上部ヘッダコメント参照）。
            bool hardFailure = false;
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxTowerPlace, "SFX-01 TowerPlace", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxTowerFire, "SFX-02 TowerFire", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxEnemyDefeat, "SFX-03 EnemyDefeat", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxCoreHit, "SFX-04 CoreHit", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxWaveStart, "SFX-05 WaveStart", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.SfxVictoryJingle, "SFX-06 VictoryJingle", integrated: true);
            hardFailure |= CheckResource<AudioClip>(report, GameConfig.AssetKeys.BgmMainTheme, "BGM-01 MainTheme", integrated: true);
            return hardFailure;
        }

        /// <summary>design/assets.md で status=planned のまま（未生成）と既知の資産。プレースホルダ/未使用のまま
        /// 残ることを記録するだけで、欠落は失敗にしない（MANIFEST notes 済みの計画済み縮退）。
        /// S-19（資産統合）で MDL-04 Warbeast / BGM-01 MainTheme は取込済みとなり上記 CheckModels/CheckAudio へ
        /// 移動した（重複計上しない）。batch-verify(Build) 2026-07-22 で IMG-01/02 も取込済みとなり CheckTextures
        /// へ移動した。残るのは IMG-05/06/07（UI アイコン。各 UI story）。</summary>
        private static void CheckKnownGaps(StringBuilder report)
        {
            report.AppendLine("[ForgeAssetIntegration] === known gaps (status=planned in design/assets.md; not shipped this batch) ===");
            report.AppendLine($"PLANNED\tIMG-05 IconAchievements\tkey={GameConfig.AssetKeys.IconAchievements}");
            report.AppendLine($"PLANNED\tIMG-06 IconUpgrades\tkey={GameConfig.AssetKeys.IconUpgrades}");
            report.AppendLine($"PLANNED\tIMG-07 IconEnemyIndicator\tkey={GameConfig.AssetKeys.IconEnemyIndicator}");
        }

        /// <summary>資産の存在を確認して report へ1行追記する。戻り値は「integrated=true の資産が欠落
        /// していた（＝MISSING_INTEGRATED を出した）」ことを示す（CR-CODE S-19 iter2 minor #3:
        /// CheckAudio が呼び出し側で hardFailure へ反映するために使う）。integrated=false（CheckTextures 等の
        /// 既知未生成資産）では常に false を返す。</summary>
        private static bool CheckResource<T>(StringBuilder report, string key, string name, bool integrated = false) where T : UnityEngine.Object
        {
            T asset = Resources.Load<T>(key);
            if (asset != null)
            {
                report.AppendLine($"PASS\t{name}\tkey={key}");
                return false;
            }

            string missingStatus = integrated ? "MISSING_INTEGRATED" : "MISSING";
            report.AppendLine($"{missingStatus}\t{name}\tkey={key}");
            return integrated;
        }

        private static void WriteReportFile(string text)
        {
            // game/Logs/ 相対（ForgeBuild.cs の game/Logs/build.log と同じ置き場）。
            string logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "asset-integration-report.txt"), text);
        }
    }
}
