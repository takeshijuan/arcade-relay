// ForgeBuild — batchmode build entry (tech-stack-unity 検証コマンド "build 相当").
// Invoke fully-qualified: -executeMethod ForgeGame.EditorTools.ForgeBuild.BuildMac
// Rule 11: batchmode failures MUST escalate to a non-zero exit code (never LogError+return).
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForgeGame.EditorTools
{
    public static class ForgeBuild
    {
        private const string OutputDir = "Build";
        private const string AppName = "ForgeGame.app";

        public static void BuildMac()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in EditorBuildSettings. Run ForgeScaffold.SetupScenes first.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = System.IO.Path.Combine(OutputDir, AppName),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            // Ensure the active target is StandaloneOSX (architecture defaults to the editor host,
            // i.e. Apple silicon arm64; left at default to stay API-stable across Unity 6 minors).
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {summary.outputPath} ({summary.totalSize} bytes)");
                return;
            }

            Fail($"Build failed: result={summary.result} errors={summary.totalErrors}");
        }

        private static void Fail(string message)
        {
            Debug.LogError("[ForgeBuild] " + message);
            EditorApplication.Exit(1);
        }
    }
}
