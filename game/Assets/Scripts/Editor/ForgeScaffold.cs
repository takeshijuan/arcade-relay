// ForgeScaffold.cs — 5シーン（Boot/Title/Menu/Game/Result・contract §11 必須シーン集合）を生成し
// EditorBuildSettings を正準フロー順に構成する scaffold ユーティリティ。
// 実行: "$UNITY" -batchmode -projectPath game -executeMethod ForgeGame.EditorTools.ForgeScaffold.GenerateScenes -quit
// 各シーンには最小の Main Camera + Directional Light を置く（story が中身を肉付けする土台）。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForgeGame.Components;

namespace ForgeGame.EditorTools
{
    public static class ForgeScaffold
    {
        private const string SceneDir = "Assets/Scenes";

        // 正準フロー順（contract §11）。EditorBuildSettings のインデックス順に一致させる。
        private static readonly string[] SceneNames =
        {
            GameConfig.Scenes.Boot,
            GameConfig.Scenes.Title,
            GameConfig.Scenes.Menu,
            GameConfig.Scenes.Game,
            GameConfig.Scenes.Result,
        };

        public static void GenerateScenes()
        {
            if (!AssetDatabase.IsValidFolder(SceneDir))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var buildScenes = new List<EditorBuildSettingsScene>();

            foreach (string name in SceneNames)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Main Camera
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.10f, 0.12f);
                camGo.AddComponent<AudioListener>();
                camGo.transform.position = new Vector3(0f, 10f, -10f);
                camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

                // Directional Light
                var lightGo = new GameObject("Directional Light");
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // Boot シーンだけ入口コンポーネントを置く（正準フロー Boot→Title の起点）
                if (name == GameConfig.Scenes.Boot)
                {
                    var boot = new GameObject("GameBootstrap");
                    boot.AddComponent<GameBootstrap>();
                }

                string path = $"{SceneDir}/{name}.unity";
                bool saved = EditorSceneManager.SaveScene(scene, path);
                if (!saved)
                {
                    Debug.LogError($"[ForgeScaffold] シーン保存失敗: {path}");
                    EditorApplication.Exit(1);
                    return;
                }
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeScaffold] {SceneNames.Length} scenes generated and registered in EditorBuildSettings.");
        }
    }
}
