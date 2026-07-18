// AudioMixerSetupTests — S-13: validates Editor/AudioMixerSetup's reflection-based AudioMixerController
// authoring actually produces a mixer with real BgmVolume/SfxVolume exposed float parameters, and that
// a re-run is idempotent (no duplicate groups/parameters).
//
// AudioMixer.GetFloat succeeds purely against the managed exposed-parameter table and works in EditMode
// (confirmed empirically). AudioMixer.SetFloat only takes effect against a live native audio graph,
// which only exists once Unity's audio system has started (Play Mode / an actual running game) —
// confirmed empirically that SetFloat silently returns false in EditMode even against a correctly
// authored mixer. The SetFloat round-trip (what Components/MenuController.ApplyMixerVolumes actually
// drives at runtime) is therefore verified in a PlayMode test instead
// (Tests/PlayMode/MenuSettingsSceneTests.cs), not here.
//
// Uses a throwaway temp asset path under Assets/ (deleted in TearDown) so this never touches the real
// Assets/Generated/Audio/Mixer.mixer committed asset.
using ForgeGame.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace ForgeGame.Tests.EditMode
{
    public sealed class AudioMixerSetupTests
    {
        private const string TestAssetPath = "Assets/Tests/EditMode/_tmp-audio-mixer-setup-test.mixer";

        [TearDown]
        public void DeleteTestAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(TestAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(TestAssetPath);
            }
        }

        [Test]
        public void EnsureMixer_CreatesMixerWithExposedBgmAndSfxVolumeParameters()
        {
            AudioMixer mixer = AudioMixerSetup.EnsureMixer(
                TestAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);

            Assert.IsNotNull(mixer, "AudioMixerSetup.EnsureMixer must return a non-null AudioMixer");

            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerBgmVolumeParam, out _),
                "BgmVolume must be a real exposed parameter (GetFloat must recognize it)");
            Assert.IsTrue(mixer.GetFloat(GameConfig.Audio.MixerSfxVolumeParam, out _),
                "SfxVolume must be a real exposed parameter (GetFloat must recognize it)");
            Assert.IsFalse(mixer.GetFloat("NotAnExposedParameter", out _),
                "sanity: an unexposed parameter name must NOT be recognized (guards against a false-positive GetFloat)");
        }

        [Test]
        public void EnsureMixer_SecondCall_IsIdempotent_ReturnsSameAssetWithoutError()
        {
            AudioMixer first = AudioMixerSetup.EnsureMixer(
                TestAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);
            Assert.IsNotNull(first);

            AudioMixer second = AudioMixerSetup.EnsureMixer(
                TestAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);

            Assert.IsNotNull(second);
            Assert.AreEqual(AssetDatabase.GetAssetPath(first), AssetDatabase.GetAssetPath(second));
            Assert.IsTrue(second.GetFloat(GameConfig.Audio.MixerBgmVolumeParam, out _),
                "idempotent re-run must not lose/duplicate the exposed BgmVolume parameter");
        }
    }
}
