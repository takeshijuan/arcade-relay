// ForgeScaffold — one-shot scaffolder that materializes the 5 required scenes (contract §11:
// Boot/Title/Menu/Game/Result) and registers them in EditorBuildSettings in flow order.
// Invoke: -executeMethod ForgeGame.EditorTools.ForgeScaffold.SetupScenes
// Rule 11: any failure escalates to a non-zero exit.
using System;
using System.Collections.Generic;
using System.IO;
using ForgeGame;
using ForgeGame.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeGame.EditorTools
{
    public static class ForgeScaffold
    {
        private const string ScenesDir = "Assets/Scenes";

        // Flow order (gdd ゲームフロー). Boot must be index 0 (first loaded scene).
        private static readonly string[] SceneNames =
        {
            GameConfig.Scenes.Boot,
            GameConfig.Scenes.Title,
            GameConfig.Scenes.Menu,
            GameConfig.Scenes.Game,
            GameConfig.Scenes.Result,
        };

        public static void SetupScenes()
        {
            try
            {
                Directory.CreateDirectory(ScenesDir);
                var buildScenes = new List<EditorBuildSettingsScene>();

                foreach (string name in SceneNames)
                {
                    string path = $"{ScenesDir}/{name}.unity";
                    var scene = EditorSceneManager.NewScene(
                        NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                    if (name == GameConfig.Scenes.Boot)
                    {
                        var go = new GameObject("BootLoader");
                        go.AddComponent<BootLoader>();
                    }

                    if (!EditorSceneManager.SaveScene(scene, path))
                    {
                        Fail($"Failed to save scene: {path}");
                        return;
                    }
                    buildScenes.Add(new EditorBuildSettingsScene(path, true));
                }

                EditorBuildSettings.scenes = buildScenes.ToArray();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"ForgeScaffold: created {SceneNames.Length} scenes and set EditorBuildSettings.");
            }
            catch (Exception e)
            {
                Fail("Exception during scaffold: " + e);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[ForgeScaffold] " + message);
            EditorApplication.Exit(1);
        }
    }
}
