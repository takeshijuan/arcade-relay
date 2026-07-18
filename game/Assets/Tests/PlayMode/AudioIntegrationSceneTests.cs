// AudioIntegrationSceneTests — S-19: 音声統合 (BGM ループ + 全 SFX 配線 + 音量バス).
// Loads Boot.unity (Editor/AssetIntegration.PatchBgmPlayer-wired Components/BgmPlayer) end-to-end through
// BootLoader.Start and verifies: (1) BGM-01 starts looping on the mixer's Bgm bus, (2) the same
// DontDestroyOnLoad BgmPlayer instance survives scene transitions without restarting/stopping (gdd
// 「全シーン共通でシームレスループ再生」), and (3) the loaded SaveData's bgmVolume/sfxVolume are applied to
// the AudioMixer bus at Boot — before the Menu 設定タブ is ever opened (Components/MenuController already
// covers the "opening Settings reflects the current value" half; this file covers the "Boot itself pushes
// the saved value" half that only BgmPlayer.ApplySavedVolumes provides).
// Persistence assertions point Components/BootLoader.SaveDirectoryOverrideForTests at
// Application.temporaryCachePath (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない),
// mirroring MenuController/HealthComponent's identical seam.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class AudioIntegrationSceneTests
    {
        // CR-CODE s-19 iteration 2 minor finding: AssertPlayheadAdvancedWithoutRestart's detection window
        // used to be only the couple of frames BootThroughToTitle already yields (playhead ≈ 0.1-0.3s at
        // capture time), which is the same order of magnitude as a scene load's wall-clock duration — a
        // silent Stop()+Play() restart during the load could coincidentally re-advance the playhead past
        // the pre-transition capture value before the post-transition check runs, making detection a
        // coin toss (worst on the first Title->Menu transition). Waiting this much *real* time
        // immediately before each capture pushes the pre-transition value comfortably above any plausible
        // scene-load duration, so a restart-to-near-zero can no longer catch back up to it.
        private const float PlayheadCaptureMarginSeconds = 0.5f;

        // Threshold for "the pre-transition time was already within one frame of the clip's end" (the one
        // legitimate case where the playhead is expected to be lower after a transition — a normal loop
        // wraparound, not a silent restart). One frame at a conservative 30fps floor.
        private const float NearClipEndToleranceSeconds = 1f / 30f;

        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s19-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            BootLoader.SaveDirectoryOverrideForTests = _tempSaveDir;
        }

        [TearDown]
        public void TearDownSession()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            if (BgmPlayer.Instance != null)
            {
                Object.DestroyImmediate(BgmPlayer.Instance.gameObject);
            }
            BootLoader.SaveDirectoryOverrideForTests = null;
            if (_tempSaveDir != null && Directory.Exists(_tempSaveDir))
            {
                Directory.Delete(_tempSaveDir, true);
            }
        }

        /// <summary>Loads Boot.unity and waits out its own internal Boot->Title transition
        /// (BootLoader.Start loads Title synchronously once SessionHolder/BgmPlayer are wired).</summary>
        private static IEnumerator BootThroughToTitle()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Boot, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boot_StartsBgmLoopingOnBgmMixerGroup()
        {
            yield return BootThroughToTitle();

            Assert.IsNotNull(BgmPlayer.Instance, "Boot must wire a DontDestroyOnLoad BgmPlayer (Editor/AssetIntegration.PatchBgmPlayer)");
            Assert.IsNotNull(BgmPlayer.Instance.Clip, "BgmPlayer must have BGM-01 assigned (GameConfig.AssetKeys.BgmMainLoop)");

            AudioSource source = BgmPlayer.Instance.Source;
            Assert.IsNotNull(source);
            Assert.IsTrue(source.loop, "BGM-01 must loop seamlessly for the whole session (gdd 音声統合)");
            Assert.IsTrue(source.isPlaying, "BGM must already be playing once Boot completes");
            Assert.IsNotNull(source.outputAudioMixerGroup, "BGM AudioSource must be routed to the Bgm mixer bus");
            Assert.AreEqual(GameConfig.Audio.MixerBgmGroupName, source.outputAudioMixerGroup.name);
        }

        [UnityTest]
        public IEnumerator Bgm_SurvivesSceneTransitions_WithoutRestarting()
        {
            yield return BootThroughToTitle();
            BgmPlayer bgm = BgmPlayer.Instance;
            Assert.IsNotNull(bgm);
            Assert.IsTrue(bgm.Source.isPlaying);

            // CR-CODE s-19 iteration 3 minor finding: isPlaying/AreSame/clip-identity alone cannot
            // detect a Stop()+Play() (or clip re-assign) restart that resets playback to the head —
            // such a restart would leave every prior assertion green. Capture the playhead position
            // immediately before each transition and assert it did not snap back to (near) zero after,
            // which is what a silent restart would produce. A legitimate loop wraparound is allowed
            // for only the (rare) case where the pre-transition time was already within one frame's
            // worth of the clip's end.
            // CR-CODE s-19 iteration 2 minor finding: wait PlayheadCaptureMarginSeconds of real time
            // before capturing so the pre-transition value sits comfortably above any plausible scene-load
            // duration (see PlayheadCaptureMarginSeconds' doc comment above).
            yield return new WaitForSecondsRealtime(PlayheadCaptureMarginSeconds);
            float timeBeforeMenuTransition = bgm.Source.time;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreSame(bgm, BgmPlayer.Instance, "BgmPlayer must be the same DontDestroyOnLoad instance across scene loads, not recreated");
            Assert.IsTrue(BgmPlayer.Instance.Source.isPlaying, "BGM must keep playing uninterrupted through a Title->Menu transition");
            Assert.AreEqual(bgm.Clip, BgmPlayer.Instance.Clip, "the same BGM-01 clip must still be assigned (no re-wiring/duplicate playback)");
            AssertPlayheadAdvancedWithoutRestart(timeBeforeMenuTransition, BgmPlayer.Instance.Source, "Title->Menu");

            yield return new WaitForSecondsRealtime(PlayheadCaptureMarginSeconds);
            float timeBeforeGameTransition = BgmPlayer.Instance.Source.time;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreSame(bgm, BgmPlayer.Instance, "BgmPlayer must persist through a second transition too (Menu->Game)");
            Assert.IsTrue(BgmPlayer.Instance.Source.isPlaying, "BGM must keep playing uninterrupted through a Menu->Game transition");
            AssertPlayheadAdvancedWithoutRestart(timeBeforeGameTransition, BgmPlayer.Instance.Source, "Menu->Game");
        }

        /// <summary>
        /// Asserts the AudioSource's playhead moved forward (not snapped back to the head) across a
        /// scene transition, distinguishing "kept playing" from "silently restarted". A drop is only
        /// tolerated when the pre-transition time was already within one frame's worth of the clip's
        /// end (legitimate loop wraparound) — CR-CODE s-19 iteration 3 minor finding.
        /// </summary>
        private static void AssertPlayheadAdvancedWithoutRestart(float timeBeforeTransition, AudioSource source, string transitionLabel)
        {
            float clipLength = source.clip != null ? source.clip.length : 0f;
            bool nearClipEnd = clipLength > 0f && timeBeforeTransition >= clipLength - NearClipEndToleranceSeconds;
            bool restartedToHead = source.time < timeBeforeTransition && !nearClipEnd;
            Assert.IsFalse(restartedToHead,
                $"S-19: BGM playhead must not snap back to the head across a {transitionLabel} transition " +
                $"(was {timeBeforeTransition:F3}s, now {source.time:F3}s) — that would indicate a silent Stop()+Play() restart, " +
                "not an uninterrupted loop.");
        }

        [UnityTest]
        public IEnumerator Boot_AppliesSavedVolumesToMixerBus_WithoutOpeningMenuSettings()
        {
            var save = SaveData.CreateDefault();
            save.bgmVolume = 0.3f;
            save.sfxVolume = 0.65f;
            var writer = new FileSaveAdapter(_tempSaveDir);
            writer.Save(save);

            yield return BootThroughToTitle();

            Assert.IsNotNull(BgmPlayer.Instance);
            AudioMixer mixer = BgmPlayer.Instance.Source.outputAudioMixerGroup?.audioMixer;
            Assert.IsNotNull(mixer, "BGM AudioSource must resolve an AudioMixerGroup with a valid parent AudioMixer");

            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerBgmVolumeParam, out float bgmDb));
            Assert.AreEqual(VolumeControl.LinearToDecibel(0.3f), bgmDb, 0.01f,
                "S-19: bgmVolume must be applied to the mixer bus at Boot, before Menu Settings is ever opened");

            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerSfxVolumeParam, out float sfxDb));
            Assert.AreEqual(VolumeControl.LinearToDecibel(0.65f), sfxDb, 0.01f,
                "S-19: sfxVolume must likewise be applied to the mixer bus at Boot");
        }

        [UnityTest]
        public IEnumerator Boot_CorruptSave_StillWiresBgmPlayer_WithDefaultVolumes()
        {
            // Corrupt save (missing save_version) — exercises the recovery path (S-01) alongside BgmPlayer
            // wiring, confirming the Boot audio setup does not depend on a clean load.
            File.WriteAllText(Path.Combine(_tempSaveDir, GameConfig.Save.FileName), "{\"highScore\":999}");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[SaveCorruption\]"));

            yield return BootThroughToTitle();

            Assert.IsNotNull(BgmPlayer.Instance, "BgmPlayer must still be wired even when the save load recovers from corruption");
            Assert.IsTrue(BgmPlayer.Instance.Source.isPlaying);

            AudioMixer mixer = BgmPlayer.Instance.Source.outputAudioMixerGroup?.audioMixer;
            Assert.IsNotNull(mixer);
            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerBgmVolumeParam, out float bgmDb));
            Assert.AreEqual(VolumeControl.LinearToDecibel(GameConfig.Audio.DefaultBgmVolume), bgmDb, 0.01f,
                "a recovered/default SaveData must apply GameConfig.Audio.DefaultBgmVolume to the mixer bus");
        }
    }
}
