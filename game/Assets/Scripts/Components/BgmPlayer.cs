// BgmPlayer — persists across every scene (Boot→Title→Menu→Game→Result) and loops BGM-01 for the whole
// session (S-19 音声統合: 全シーン共通シームレスループ). DontDestroyOnLoad singleton created once in the
// Boot scene (mirrors Components/SessionHolder's DDOL pattern exactly) so BGM never restarts/stutters on
// scene transitions. Thin by design: `AudioSource.loop = true` handles the actual seamless loop (BGM-01
// was edited with a verified crossfade loop point — design/assets.md BGM-01 `loop_verification`); this
// component only owns clip storage, AudioSource lifecycle, and AudioMixer bus routing (rule: Components
// はライフサイクルと配線のみ).
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using UnityEngine;
using UnityEngine.Audio;

namespace ForgeGame.Components
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        [Tooltip("BGM-01 (全シーン共通ループ). Assigned by Editor/AssetIntegration.PatchBgmPlayer from GameConfig.AssetKeys.BgmMainLoop.")]
        [SerializeField] private AudioClip _bgmClip;

        [Tooltip("Assigned by Editor/AssetIntegration.PatchBgmPlayer (AudioMixerSetup.EnsureMixer — same shared mixer asset as Components/MenuController._mixer / SfxLibrary).")]
        [SerializeField] private AudioMixer _mixer;

        /// <summary>Test seam — exposes the wired clip/source so PlayMode tests can assert loop/routing
        /// state without reflection (mirrors SfxLibrary's public AttackHit/Dash/... accessors).</summary>
        public AudioClip Clip => _bgmClip;
        public AudioSource Source => _source;

        private AudioSource _source;

        // CR-CODE s-19 iteration 1 minor finding: a single shared _mixerErrorLogged flag guarded both
        // RouteToMixerGroup's two failure modes (missing _mixer / missing Bgm group — Awake, runs once)
        // and ApplySavedVolumes' two failure modes (missing _mixer / SetFloat returning false — can run
        // later than Awake, from BootLoader). Once RouteToMixerGroup logged first, an independent
        // ApplySavedVolumes failure (e.g. "the saved volume never reached the mixer bus" — different,
        // actionable information from "routing itself failed") was silently swallowed. Split by failure
        // domain (mirrors Components/MenuController's already-reviewed _mixerErrorLogged/
        // _sfxMixerErrorLogged split for the same reason).
        private bool _routingErrorLogged;
        private bool _volumeApplyErrorLogged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate BgmPlayer destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
            {
                // DontDestroyOnLoad only works on root GameObjects (mirrors SessionHolder.Awake's identical guard).
                Debug.LogError("[Wiring] BgmPlayer must be a root GameObject");
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;

            RouteToMixerGroup();

            if (_bgmClip == null)
            {
                Debug.LogError("[Wiring] BgmPlayer._bgmClip is unassigned despite GameConfig.AssetKeys.BgmMainLoop ('" +
                    GameConfig.AssetKeys.BgmMainLoop + "') expecting a generated clip — Editor/AssetIntegration.PatchBgmPlayer wiring is broken.");
                return;
            }
            _source.clip = _bgmClip;
            _source.Play();
        }

        private void RouteToMixerGroup()
        {
            if (_mixer == null)
            {
                LogRoutingErrorOnce("[Wiring] BgmPlayer.RouteToMixerGroup: AudioMixer not assigned (Editor/AssetIntegration.PatchBgmPlayer should have wired it) — BGM will play at unity gain, ignoring the bgmVolume bus this session");
                return;
            }
            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(GameConfig.Audio.MixerBgmGroupName);
            if (groups == null || groups.Length == 0)
            {
                LogRoutingErrorOnce("[Wiring] BgmPlayer.RouteToMixerGroup: no AudioMixerGroup named '" + GameConfig.Audio.MixerBgmGroupName + "' found in mixer '" + _mixer.name + "'");
                return;
            }
            _source.outputAudioMixerGroup = groups[0];
        }

        /// <summary>
        /// Boot calls this once, right after SessionHolder is populated with the loaded SaveData, so
        /// bgmVolume/sfxVolume apply to the mixer bus for the whole session even if the player never opens
        /// the Menu 設定タブ this run (gdd 音声統合「音量バスが SaveData の音量値を反映する」). Relies on
        /// Unity's documented Awake-before-Start ordering across every object in the same scene load — this
        /// component's Awake has already run by the time BootLoader.Start executes, so Instance is
        /// guaranteed non-null there. Duplicated from Components/MenuController.ApplyMixerVolumes rather
        /// than shared — MenuController.cs is UI-domain/ui-engineer-owned (S-13); this is the equivalent
        /// gameplay-engineer-owned entry point for the same mixer bus (S-19).
        /// </summary>
        public void ApplySavedVolumes(SaveData save)
        {
            if (_mixer == null)
            {
                LogVolumeApplyErrorOnce("[Wiring] BgmPlayer.ApplySavedVolumes: AudioMixer not assigned — bgmVolume/sfxVolume were not applied to the mixer bus this session");
                return;
            }
            if (save == null)
            {
                Debug.LogError("[Wiring] BgmPlayer.ApplySavedVolumes called with null SaveData; skipping");
                return;
            }

            bool bgmOk = _mixer.SetFloat(GameConfig.Audio.MixerBgmVolumeParam, VolumeControl.LinearToDecibel(save.bgmVolume));
            bool sfxOk = _mixer.SetFloat(GameConfig.Audio.MixerSfxVolumeParam, VolumeControl.LinearToDecibel(save.sfxVolume));
            if (!bgmOk || !sfxOk)
            {
                LogVolumeApplyErrorOnce($"[Wiring] BgmPlayer.ApplySavedVolumes: AudioMixer.SetFloat returned false (bgmOk={bgmOk}, sfxOk={sfxOk}) — exposed parameter '{GameConfig.Audio.MixerBgmVolumeParam}'/'{GameConfig.Audio.MixerSfxVolumeParam}' missing or audio system not live");
            }
        }

        private void LogRoutingErrorOnce(string message)
        {
            if (_routingErrorLogged)
            {
                return;
            }
            _routingErrorLogged = true;
            Debug.LogError(message);
        }

        private void LogVolumeApplyErrorOnce(string message)
        {
            if (_volumeApplyErrorLogged)
            {
                return;
            }
            _volumeApplyErrorLogged = true;
            Debug.LogError(message);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
