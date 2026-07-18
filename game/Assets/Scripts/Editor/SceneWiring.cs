// SceneWiring — idempotent per-scene editor tool that attaches each story's runtime bootstrap
// components into the scene file ForgeScaffold created empty (contract §11 scene wiring). Safe to
// re-run: looks for an existing named root GameObject before adding one. One static method per
// wired scene; call the one you need via -executeMethod (rule 11: failures escalate to exit 1).
// Invoke: -executeMethod ForgeGame.EditorTools.SceneWiring.WireTitle
using System;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Ui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ForgeGame.EditorTools
{
    public static class SceneWiring
    {
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const string TitleRootName = "TitleController";
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string MenuRootName = "MenuController";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string ResultScenePath = "Assets/Scenes/Result.unity";
        private const string ResultRootName = "ResultController";
        private const string PlayerRootName = "Player";
        private const string MainCameraName = "Main Camera";
        private const string WaveSpawnerRootName = "WaveSpawner";
        private const string RunStatsTrackerRootName = "RunStatsTracker";
        private const string GameHudRootName = "GameHud";
        private const string ArenaEnvironmentRootName = "ArenaEnvironment";
        private const string ArenaBackdropRootName = "ArenaBackdrop";
        private const string DirectionalLightRootName = "Directional Light";
        private const string PostProcessVolumeRootName = "PostProcessVolume";

        public static void WireTitle()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

                GameObject root = GameObject.Find(TitleRootName);
                if (root == null)
                {
                    root = new GameObject(TitleRootName);
                }
                if (root.GetComponent<TitleScreen>() == null)
                {
                    root.AddComponent<TitleScreen>();
                }
                if (root.GetComponent<TitleController>() == null)
                {
                    root.AddComponent<TitleController>();
                }

                WireTitleUiFrameKit(root);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    Fail("Failed to save Title scene after wiring.");
                    return;
                }
                Debug.Log("SceneWiring: Title scene wired (TitleController + TitleScreen + IMG-05 decoration).");
            }
            catch (Exception e)
            {
                Fail("Exception during Title scene wiring: " + e);
            }
        }

        public static void WireMenu()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

                GameObject root = GameObject.Find(MenuRootName);
                if (root == null)
                {
                    root = new GameObject(MenuRootName);
                }
                if (root.GetComponent<MenuScreen>() == null)
                {
                    root.AddComponent<MenuScreen>();
                }
                if (root.GetComponent<MenuController>() == null)
                {
                    root.AddComponent<MenuController>();
                }

                WireMenuAudioMixer(root);
                WireMenuCrystalIcon(root);
                WireMenuUiFrameKit(root);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    Fail("Failed to save Menu scene after wiring.");
                    return;
                }
                Debug.Log("SceneWiring: Menu scene wired (MenuController + MenuScreen + AudioMixer + crystal icon + IMG-05 decoration).");
            }
            catch (Exception e)
            {
                Fail("Exception during Menu scene wiring: " + e);
            }
        }

        /// <summary>
        /// S-13: creates/loads the shared BGM/SFX AudioMixer (Editor/AudioMixerSetup — best-effort, see
        /// its header) and bakes the reference into MenuController's private `_mixer` SerializeField via
        /// SerializedObject (mirrors AssetIntegration.PatchWaveSpawnerPrefab's SerializedObject-assign
        /// pattern). A creation failure degrades to a logged error + the field left unassigned
        /// (Components/MenuController.ApplyMixerVolumes already documents/handles a null _mixer) rather
        /// than failing the whole Menu wiring pass.
        /// </summary>
        private static void WireMenuAudioMixer(GameObject root)
        {
            MenuController controller = root.GetComponent<MenuController>();
            if (controller == null)
            {
                Fail("WireMenuAudioMixer: MenuController missing on '" + root.name + "'.");
                return;
            }

            AudioMixer mixer = AudioMixerSetup.EnsureMixer(
                GameConfig.Audio.MixerAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);
            if (mixer == null)
            {
                Debug.LogError("[Wiring] WireMenuAudioMixer: AudioMixer creation/load failed (see AudioMixerSetup log above) — " +
                    "Settings-tab volume changes will still save to SaveData but won't drive a live mixer bus this session.");
                return;
            }

            var so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("_mixer");
            if (prop == null)
            {
                Fail("WireMenuAudioMixer: MenuController._mixer serialized field not found (rename?).");
                return;
            }
            prop.objectReferenceValue = mixer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>S-13: bakes IMG-03 (crystal icon) into MenuScreen's private `_crystalIconSprite`
        /// SerializeField for the 統計タブ crystal-count rows. Missing sprite (not yet generated) degrades
        /// to a logged warning + unassigned field — MenuScreen already builds a transparent icon Image in
        /// that case rather than erroring.</summary>
        private static void WireMenuCrystalIcon(GameObject root)
        {
            MenuScreen screen = root.GetComponent<MenuScreen>();
            if (screen == null)
            {
                Fail("WireMenuCrystalIcon: MenuScreen missing on '" + root.name + "'.");
                return;
            }

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(GameConfig.AssetKeys.CrystalIconSprite);
            if (icon == null)
            {
                Debug.LogWarning("[SceneWiring] WireMenuCrystalIcon: crystal icon sprite not found at " +
                    GameConfig.AssetKeys.CrystalIconSprite + " — Stats tab will show crystal rows without an icon.");
                return;
            }

            var so = new SerializedObject(screen);
            SerializedProperty prop = so.FindProperty("_crystalIconSprite");
            if (prop == null)
            {
                Fail("WireMenuCrystalIcon: MenuScreen._crystalIconSprite serialized field not found (rename?).");
                return;
            }
            prop.objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>S-30: bakes the 3 IMG-05 sprites TitleScreen consumes (_panelSprite/_ribbonSprite/
        /// _cornerSprite) — mirrors WireMenuCrystalIcon's per-sprite SerializedObject-assign pattern via
        /// the shared TryBakeSprite helper below.</summary>
        private static void WireTitleUiFrameKit(GameObject root)
        {
            TitleScreen screen = root.GetComponent<TitleScreen>();
            if (screen == null)
            {
                Fail("WireTitleUiFrameKit: TitleScreen missing on '" + root.name + "'.");
                return;
            }
            var so = new SerializedObject(screen);
            TryBakeSprite(so, "_panelSprite", GameConfig.AssetKeys.UiPanelSprite, "TitleScreen");
            TryBakeSprite(so, "_ribbonSprite", GameConfig.AssetKeys.UiRibbonSprite, "TitleScreen");
            TryBakeSprite(so, "_cornerSprite", GameConfig.AssetKeys.UiCornerSprite, "TitleScreen");
        }

        /// <summary>S-30: bakes the 5 IMG-05 sprites MenuScreen consumes (panel/tabSelected/tabUnselected/
        /// ribbon/corner) — mirrors WireMenuCrystalIcon's pattern for the other 4 fields this screen needs.</summary>
        private static void WireMenuUiFrameKit(GameObject root)
        {
            MenuScreen screen = root.GetComponent<MenuScreen>();
            if (screen == null)
            {
                Fail("WireMenuUiFrameKit: MenuScreen missing on '" + root.name + "'.");
                return;
            }
            var so = new SerializedObject(screen);
            TryBakeSprite(so, "_panelSprite", GameConfig.AssetKeys.UiPanelSprite, "MenuScreen");
            TryBakeSprite(so, "_tabSelectedSprite", GameConfig.AssetKeys.UiTabSelectedSprite, "MenuScreen");
            TryBakeSprite(so, "_tabUnselectedSprite", GameConfig.AssetKeys.UiTabUnselectedSprite, "MenuScreen");
            TryBakeSprite(so, "_ribbonSprite", GameConfig.AssetKeys.UiRibbonSprite, "MenuScreen");
            TryBakeSprite(so, "_cornerSprite", GameConfig.AssetKeys.UiCornerSprite, "MenuScreen");
        }

        /// <summary>S-30: bakes the 1 IMG-05 sprite GameHud consumes (panel background, reused for all 4
        /// stat-group panels).</summary>
        private static void WireGameUiFrameKit(GameObject gameHudRoot)
        {
            GameHud hud = gameHudRoot.GetComponent<GameHud>();
            if (hud == null)
            {
                Fail("WireGameUiFrameKit: GameHud missing on '" + gameHudRoot.name + "'.");
                return;
            }
            var so = new SerializedObject(hud);
            TryBakeSprite(so, "_panelSprite", GameConfig.AssetKeys.UiPanelSprite, "GameHud");
        }

        /// <summary>S-30: bakes the 3 IMG-05 sprites ResultScreen consumes — mirrors WireTitleUiFrameKit.</summary>
        private static void WireResultUiFrameKit(GameObject root)
        {
            ResultScreen screen = root.GetComponent<ResultScreen>();
            if (screen == null)
            {
                Fail("WireResultUiFrameKit: ResultScreen missing on '" + root.name + "'.");
                return;
            }
            var so = new SerializedObject(screen);
            TryBakeSprite(so, "_panelSprite", GameConfig.AssetKeys.UiPanelSprite, "ResultScreen");
            TryBakeSprite(so, "_ribbonSprite", GameConfig.AssetKeys.UiRibbonSprite, "ResultScreen");
            TryBakeSprite(so, "_cornerSprite", GameConfig.AssetKeys.UiCornerSprite, "ResultScreen");
        }

        /// <summary>Loads the Sprite at <paramref name="assetPath"/> (produced by
        /// Editor/AssetIntegration.ConfigureUiFrameKitSprites) and assigns it to
        /// <paramref name="serializedObject"/>'s <paramref name="fieldName"/> field. A missing sprite (IMG-05
        /// not integrated yet this session) degrades to a logged warning + the field left unassigned —
        /// mirrors WireMenuCrystalIcon's handling of a not-yet-generated IMG-03. Returns whether the bake
        /// succeeded (callers here don't currently need the result, but this keeps the same
        /// success/failure vocabulary the audio AssignClipResult helpers use elsewhere in this file).</summary>
        private static bool TryBakeSprite(SerializedObject serializedObject, string fieldName, string assetPath, string ownerLabel)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[SceneWiring] TryBakeSprite: sprite not found at {assetPath} — {ownerLabel}.{fieldName} left unassigned (IMG-05 not integrated this session).");
                return false;
            }
            SerializedProperty prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
            {
                Fail($"TryBakeSprite: {ownerLabel}.{fieldName} serialized field not found (rename?).");
                return false;
            }
            prop.objectReferenceValue = sprite;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static void WireGame()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

                GameObject player = GameObject.Find(PlayerRootName);
                if (player == null)
                {
                    player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    player.name = PlayerRootName;
                    player.transform.position = Vector3.zero;
                    ApplyPlaceholderColor(player, GameConfig.Ui.ColorTextSecondary);
                }
                if (player.GetComponent<PlayerController>() == null)
                {
                    player.AddComponent<PlayerController>();
                }
                if (player.GetComponent<AutoAttackDriver>() == null)
                {
                    player.AddComponent<AutoAttackDriver>();
                }
                if (player.GetComponent<HealthComponent>() == null)
                {
                    player.AddComponent<HealthComponent>();
                }
                if (player.GetComponent<HeroFxController>() == null)
                {
                    player.AddComponent<HeroFxController>();
                }
                if (player.GetComponent<DashTrailSpawner>() == null)
                {
                    player.AddComponent<DashTrailSpawner>();
                }

                GameObject mainCamera = GameObject.Find(MainCameraName);
                if (mainCamera == null)
                {
                    Fail("Game scene missing 'Main Camera' (expected from ForgeScaffold DefaultGameObjects).");
                    return;
                }
                if (mainCamera.GetComponent<ArenaCameraRig>() == null)
                {
                    mainCamera.AddComponent<ArenaCameraRig>();
                }

                GameObject arenaEnvironment = GameObject.Find(ArenaEnvironmentRootName);
                if (arenaEnvironment == null)
                {
                    arenaEnvironment = new GameObject(ArenaEnvironmentRootName);
                }
                if (arenaEnvironment.GetComponent<ArenaEnvironment>() == null)
                {
                    arenaEnvironment.AddComponent<ArenaEnvironment>();
                }

                GameObject arenaBackdrop = GameObject.Find(ArenaBackdropRootName);
                if (arenaBackdrop == null)
                {
                    arenaBackdrop = new GameObject(ArenaBackdropRootName);
                }
                if (arenaBackdrop.GetComponent<ArenaBackdrop>() == null)
                {
                    arenaBackdrop.AddComponent<ArenaBackdrop>();
                }

                // S-27: art-bible style_block を key image の高発色トゥーンへ寄せる URP グローバル
                // Volume + Directional Light 調整。
                GameObject directionalLight = GameObject.Find(DirectionalLightRootName);
                if (directionalLight == null)
                {
                    Fail("Game scene missing 'Directional Light' (expected from ForgeScaffold DefaultGameObjects).");
                    return;
                }
                if (directionalLight.GetComponent<KeyLightRig>() == null)
                {
                    directionalLight.AddComponent<KeyLightRig>();
                }

                GameObject postProcessVolume = GameObject.Find(PostProcessVolumeRootName);
                if (postProcessVolume == null)
                {
                    postProcessVolume = new GameObject(PostProcessVolumeRootName);
                }
                if (postProcessVolume.GetComponent<PostProcessRig>() == null)
                {
                    postProcessVolume.AddComponent<PostProcessRig>();
                }

                GameObject waveSpawner = GameObject.Find(WaveSpawnerRootName);
                if (waveSpawner == null)
                {
                    waveSpawner = new GameObject(WaveSpawnerRootName);
                }
                if (waveSpawner.GetComponent<WaveSpawner>() == null)
                {
                    waveSpawner.AddComponent<WaveSpawner>();
                }

                GameObject runStatsTracker = GameObject.Find(RunStatsTrackerRootName);
                if (runStatsTracker == null)
                {
                    runStatsTracker = new GameObject(RunStatsTrackerRootName);
                }
                if (runStatsTracker.GetComponent<RunStatsTracker>() == null)
                {
                    runStatsTracker.AddComponent<RunStatsTracker>();
                }

                GameObject gameHud = GameObject.Find(GameHudRootName);
                if (gameHud == null)
                {
                    gameHud = new GameObject(GameHudRootName);
                }
                if (gameHud.GetComponent<GameHud>() == null)
                {
                    gameHud.AddComponent<GameHud>();
                }
                if (gameHud.GetComponent<GameHudController>() == null)
                {
                    gameHud.AddComponent<GameHudController>();
                }

                WireGameUiFrameKit(gameHud);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    Fail("Failed to save Game scene after wiring.");
                    return;
                }
                Debug.Log("SceneWiring: Game scene wired (Player/PlayerController+AutoAttackDriver+HealthComponent+HeroFxController+DashTrailSpawner + Main Camera/ArenaCameraRig + ArenaEnvironment + ArenaBackdrop + Directional Light/KeyLightRig + PostProcessVolume/PostProcessRig + WaveSpawner + RunStatsTracker + GameHud/GameHudController + IMG-05 decoration).");
            }
            catch (Exception e)
            {
                Fail("Exception during Game scene wiring: " + e);
            }
        }

        public static void WireResult()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Single);

                GameObject root = GameObject.Find(ResultRootName);
                if (root == null)
                {
                    root = new GameObject(ResultRootName);
                }
                if (root.GetComponent<ResultScreen>() == null)
                {
                    root.AddComponent<ResultScreen>();
                }
                if (root.GetComponent<ResultController>() == null)
                {
                    root.AddComponent<ResultController>();
                }

                WireResultUiFrameKit(root);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    Fail("Failed to save Result scene after wiring.");
                    return;
                }
                Debug.Log("SceneWiring: Result scene wired (ResultController + ResultScreen + IMG-05 decoration).");
            }
            catch (Exception e)
            {
                Fail("Exception during Result scene wiring: " + e);
            }
        }

        // Placeholder-only visual (contract §11: 3D 資産統合は S-18/S-20。今は単色マテリアルで代用し、
        // MDL-01 hero FBX 差し替え時に置き換える). Explicitly picks a URP-compatible shader so the
        // primitive never falls back to the pink InternalErrorShader under the project's URP pipeline.
        private static void ApplyPlaceholderColor(GameObject go, string hexColor)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[SceneWiring] placeholder color skipped: '" + go.name + "' has no Renderer.");
                return;
            }
            if (!ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                Debug.LogWarning("[SceneWiring] placeholder color skipped: GameConfig hex '" + hexColor + "' failed to parse.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[SceneWiring] URP/Lit shader not found; falling back to Standard (pink InternalErrorShader risk under URP).");
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                Debug.LogWarning("[SceneWiring] placeholder color skipped: neither URP/Lit nor Standard shader found.");
                return;
            }
            renderer.sharedMaterial = new Material(shader) { color = color };
        }

        private static void Fail(string message)
        {
            Debug.LogError("[SceneWiring] " + message);
            EditorApplication.Exit(1);
        }
    }
}
