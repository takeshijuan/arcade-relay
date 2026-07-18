// MenuSettingsSceneTests — S-13: 設定タブ 音量スライダー + 操作説明 + 統計タブ完全表示.
// Loads the wired Menu.unity scene (Editor/SceneWiring.WireMenu → Components/MenuController +
// Ui/MenuScreen) and drives the Settings tab through InputTestFixture (mirrors MenuSceneTests/
// UpgradePurchaseSceneTests' identical PressKey/LoadMenuWithSave pattern). Persistence assertions point
// Components/MenuController.SaveDirectoryOverrideForTests at Application.temporaryCachePath
// (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない).
using System.Collections;
using System.IO;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class MenuSettingsSceneTests : InputTestFixture
    {
        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s13-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            MenuController.SaveDirectoryOverrideForTests = _tempSaveDir;
        }

        [TearDown]
        public void TearDownSession()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            MenuController.SaveDirectoryOverrideForTests = null;
            if (_tempSaveDir != null && Directory.Exists(_tempSaveDir))
            {
                Directory.Delete(_tempSaveDir, true);
            }
        }

        private static SaveData BuildSave(float bgmVolume = 0.8f, float sfxVolume = 0.8f)
        {
            var save = SaveData.CreateDefault();
            save.bgmVolume = bgmVolume;
            save.sfxVolume = sfxVolume;
            return save;
        }

        private IEnumerator LoadMenuWithSave(SaveData save)
        {
            SessionHolder.EnsureCreated(save, recovered: false);
            yield return null; // let Awake run so DontDestroyOnLoad takes effect before the scene load below

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>Discrete press-then-release of a single key, spread across frames (mirrors
        /// MenuSceneTests/UpgradePurchaseSceneTests.PressKey).</summary>
        private IEnumerator PressKey(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        private IEnumerator NavigateToSettingsTab(Keyboard keyboard)
        {
            // Start(0) -> Stats(1) -> Upgrade(2) -> Settings(3): 3 presses of E (gdd Q/E タブ循環).
            for (int i = 0; i < 3; i++)
            {
                yield return PressKey(keyboard.eKey);
            }
        }

        [UnityTest]
        public IEnumerator AdjustBgm_PressD_IncreasesByStep_SavesImmediately_AndDrivesMixer()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(bgmVolume: 0.5f, sfxVolume: 0.5f));
            yield return NavigateToSettingsTab(keyboard);
            // Focus starts at index 0 = BGM on tab entry (gdd: タブ切替時はフォーカスをそのタブの先頭項目にリセット).

            yield return PressKey(keyboard.dKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(0.6f, current.bgmVolume, 0.001f, "D must step bgmVolume up by VolumeStep (0.1)");
            Assert.AreEqual(0.5f, current.sfxVolume, 0.001f, "adjusting BGM must not touch sfxVolume");

            // gdd:「即時反映...+即時セーブ」— must have landed on disk in the overridden test directory immediately.
            var reader = new FileSaveAdapter(_tempSaveDir);
            SaveLoadOutcome outcome = reader.Load();
            Assert.AreEqual(SaveLoadStatus.Ok, outcome.Status);
            Assert.AreEqual(0.6f, outcome.Data.bgmVolume, 0.001f);

            var controller = Object.FindFirstObjectByType<MenuController>();
            Assert.IsNotNull(controller);
            AudioMixerField(controller, out var mixer);
            Assert.IsNotNull(mixer, "S-13: Editor/SceneWiring.WireMenuAudioMixer must have baked an AudioMixer onto MenuController");
            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerBgmVolumeParam, out float bgmDb));
            Assert.AreEqual(VolumeControl.LinearToDecibel(0.6f), bgmDb, 0.01f,
                "gdd「即時反映（AudioMixerバス）」— the exposed BgmVolume parameter must reflect the new value immediately");
        }

        [UnityTest]
        public IEnumerator AdjustSfx_PressA_DecreasesByStep_OnlyAffectsSfxVolume()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(bgmVolume: 0.5f, sfxVolume: 0.5f));
            yield return NavigateToSettingsTab(keyboard);

            // Move focus from BGM(index 0) to SFX(index 1): 1x S (gdd W/S 縦フォーカス).
            yield return PressKey(keyboard.sKey);
            yield return PressKey(keyboard.aKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(0.4f, current.sfxVolume, 0.001f, "A must step sfxVolume down by VolumeStep (0.1)");
            Assert.AreEqual(0.5f, current.bgmVolume, 0.001f, "adjusting SFX must not touch bgmVolume");
        }

        [UnityTest]
        public IEnumerator AdjustVolume_ClampsAtUpperBound_RepeatedPressesDoNotExceedOne()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(bgmVolume: 0.9f, sfxVolume: 0.5f));
            yield return NavigateToSettingsTab(keyboard);

            // 0.9 -> 1.0 -> clamp at 1.0 (must not overshoot on the second press).
            yield return PressKey(keyboard.dKey);
            yield return PressKey(keyboard.dKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(1.0f, current.bgmVolume, 0.001f, "bgmVolume must clamp at 1.0, never exceed it");
        }

        [UnityTest]
        public IEnumerator AdjustVolume_ClampsAtLowerBound_RepeatedPressesDoNotGoBelowZero()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(bgmVolume: 0.05f, sfxVolume: 0.5f));
            yield return NavigateToSettingsTab(keyboard);

            yield return PressKey(keyboard.aKey);
            yield return PressKey(keyboard.aKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(0f, current.bgmVolume, 0.001f, "bgmVolume must clamp at 0.0, never go negative");
        }

        [UnityTest]
        public IEnumerator SubmitWhileSliderFocused_DoesNotTransitionScene_OrChangeSaveData()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            SaveData save = BuildSave(bgmVolume: 0.5f, sfxVolume: 0.5f);
            yield return LoadMenuWithSave(save);
            yield return NavigateToSettingsTab(keyboard);

            yield return PressKey(keyboard.enterKey);

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name,
                "gdd: 設定タブのスライダー項目フォーカス中は決定キーが無効 — Submit must not transition scenes");
            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(0.5f, current.bgmVolume, 0.001f, "Submit on a focused slider must not itself mutate SaveData");
            Assert.AreEqual(0.5f, current.sfxVolume, 0.001f);

            // No save file should exist yet either (no purchase/adjust happened, only an inert Submit).
            var reader = new FileSaveAdapter(_tempSaveDir);
            Assert.IsFalse(File.Exists(reader.SavePath), "an inert Submit press must not write a save file");
        }

        [UnityTest]
        public IEnumerator StatsTab_CrystalRows_HaveNonNullIconImages()
        {
            yield return LoadMenuWithSave(BuildSave());

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(screen);
            Assert.IsNotNull(screen.StatTotalCrystalsIcon, "累計獲得クリスタル row must have an IMG-03 icon Image (conventions.md §8)");
            Assert.IsNotNull(screen.StatCrystalBalanceIcon, "クリスタル残高 row must have an IMG-03 icon Image (conventions.md §8)");
            Assert.IsNotNull(screen.StatTotalCrystalsIcon.sprite, "Editor/SceneWiring.WireMenuCrystalIcon must have baked IMG-03 onto MenuScreen");
            Assert.IsNotNull(screen.StatCrystalBalanceIcon.sprite);
        }

        /// <summary>Reads MenuController's private `_mixer` field via reflection — this test asserts on
        /// the runtime state Editor/SceneWiring bakes in, not a re-implementation of the wiring itself.</summary>
        private static void AudioMixerField(MenuController controller, out UnityEngine.Audio.AudioMixer mixer)
        {
            var field = typeof(MenuController).GetField("_mixer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mixer = field?.GetValue(controller) as UnityEngine.Audio.AudioMixer;
        }
    }
}
