// ForgeBuild.cs — batchmode ビルドの入口（tech-stack-unity.md「検証コマンド」build 相当）。
// 検証コマンド: "$UNITY" -batchmode -projectPath game -executeMethod ForgeGame.EditorTools.ForgeBuild.BuildMac -quit
// 完全修飾名で指定すること（namespace 内のため裸名では解決されない — 規約より）。
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForgeGame.EditorTools
{
    public static class ForgeBuild
    {
        private const string OutputPath = "Build/ForgeGame.app"; // game/ 相対

        /// <summary>
        /// StandaloneOSX（Apple silicon）向けにビルドする。
        /// 失敗は必ず非0終了に昇格させる（規約11: batchmode の握り潰し禁止）。壊れた状態で保存しない。
        /// </summary>
        public static void BuildMac()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[ForgeBuild] EditorBuildSettings に有効なシーンがありません。");
                EditorApplication.Exit(1);
                return;
            }

            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneOSX;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ForgeBuild] Build succeeded: {summary.totalSize} bytes -> {OutputPath}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[ForgeBuild] Build failed: result={summary.result}, errors={summary.totalErrors}");
                EditorApplication.Exit(1);
            }
        }
    }
}
