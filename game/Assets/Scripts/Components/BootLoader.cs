// BootLoader — Boot scene entry point (gdd ゲームフロー: Boot→Title). Thin MonoBehaviour:
// loads the save via Persistence, hands it to the DontDestroyOnLoad SessionHolder (S-01), applies the
// loaded bgmVolume/sfxVolume to the AudioMixer bus via BgmPlayer (S-19 音声統合 — so the saved volume is
// live for the whole session even if the player never opens the Menu 設定タブ), then transitions to
// Title. Save-load logic stays in Persistence/Systems; this component only wires lifecycle → scene
// transition.
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeGame.Components
{
    public sealed class BootLoader : MonoBehaviour
    {
        /// <summary>Test seam (conventions.md §9 / mirrors Components/MenuController.
        /// SaveDirectoryOverrideForTests): PlayMode tests that load Boot.unity point this at an
        /// Application.temporaryCachePath directory so Boot's FileSaveAdapter.Load() never touches the
        /// real Application.persistentDataPath save. Null (default — production never sets it) resolves
        /// to FileSaveAdapter's default ctor.</summary>
        public static string SaveDirectoryOverrideForTests;

        private void Start()
        {
            var adapter = SaveDirectoryOverrideForTests != null
                ? new FileSaveAdapter(SaveDirectoryOverrideForTests)
                : new FileSaveAdapter();
            SaveLoadOutcome outcome = adapter.Load();
            bool recovered = outcome.Status == SaveLoadStatus.Corrupt;
            SessionHolder session = SessionHolder.EnsureCreated(outcome.Data, recovered);

            // S-19: relies on Unity's documented Awake-before-Start ordering across every object in the
            // same scene load — BgmPlayer.Awake (which sets Instance) has already run by the time this
            // Start executes, so a null Instance here means Boot.unity's BgmPlayer GameObject itself is
            // missing (a wiring bug — Editor/AssetIntegration.PatchBgmPlayer should have created it), not
            // an ordering race.
            if (BgmPlayer.Instance != null)
            {
                BgmPlayer.Instance.ApplySavedVolumes(session.Save);
            }
            else
            {
                Debug.LogError("[Wiring] BootLoader: BgmPlayer.Instance is null at Start (Boot scene missing BgmPlayer — Editor/AssetIntegration.PatchBgmPlayer) — bgmVolume/sfxVolume won't be applied to the mixer bus until Menu Settings tab is opened, and BGM won't play this session");
            }

            SceneManager.LoadScene(GameConfig.Scenes.Title);
        }
    }
}
