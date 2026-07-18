// SfxLibrary — Game シーンの効果音再生の集約ポイント (gameplay-engineer, 3D/音声資産統合 — S-18/S-19 と
// 重なる範囲の統合作業で追加). Holds AudioClip references assigned at edit time by
// Editor/AssetIntegration.IntegrateAll (SerializedObject 直接代入 — Components/WaveSpawner.enemyPrefab と
// 同じ「動的ロードのパス/アドレスは GameConfig.AssetKeys 経由、インスペクタ直参照は可」パターン。rule 5).
// Exposes a single shared AudioSource (PlayOneShot) so every trigger site (AutoAttackDriver/
// PlayerController/HealthComponent/CrystalPickup) just calls Play(clip) — no per-site AudioSource
// bookkeeping. Thin by design (rule: Components はライフサイクルと配線のみ) — whether/when to play is
// decided entirely by the caller; this component only owns clip storage + the shared AudioSource.
// clip == null is a valid, silent no-op — callers don't need to null-check before calling Play.
// S-19: the shared AudioSource is routed to the AudioMixer's Sfx group (same shared mixer asset as
// Components/MenuController._mixer / BgmPlayer) so every SFX-01..06 trigger reflects the settings-tab
// sfxVolume bus. PlayOneShot's volumeScale is now a fixed 1f (unity gain) — actual attenuation comes
// entirely from the mixer bus, not a hardcoded GameConfig.Audio.DefaultSfxVolume placeholder.
using UnityEngine;
using UnityEngine.Audio;

namespace ForgeGame.Components
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxLibrary : MonoBehaviour
    {
        /// <summary>Convenience lookup (mirrors PlayerController.Instance / WaveSpawner.Instance).</summary>
        public static SfxLibrary Instance { get; private set; }

        [Tooltip("SFX-01 (自動攻撃ヒット). Assigned by Editor/AssetIntegration from GameConfig.AssetKeys.SfxAttackHit.")]
        [SerializeField] private AudioClip _attackHit;
        [Tooltip("SFX-02 (ダッシュ発動). Assigned by Editor/AssetIntegration from GameConfig.AssetKeys.SfxDash.")]
        [SerializeField] private AudioClip _dash;
        [Tooltip("SFX-03 (プレイヤー被弾). Assigned by Editor/AssetIntegration from GameConfig.AssetKeys.SfxPlayerHit.")]
        [SerializeField] private AudioClip _playerHit;
        [Tooltip("SFX-04 (クリスタル自動回収). Assigned by Editor/AssetIntegration from GameConfig.AssetKeys.SfxCrystalPickup.")]
        [SerializeField] private AudioClip _crystalPickup;
        [Tooltip("SFX-05 (ウェーブ開始). Assigned by Editor/AssetIntegration from GameConfig.AssetKeys.SfxWaveStart (S-15).")]
        [SerializeField] private AudioClip _waveStart;

        [Tooltip("Assigned by Editor/AssetIntegration.PatchSfxLibrary (AudioMixerSetup.EnsureMixer — S-19).")]
        [SerializeField] private AudioMixer _mixer;

        public AudioClip AttackHit => _attackHit;
        public AudioClip Dash => _dash;
        public AudioClip PlayerHit => _playerHit;
        public AudioClip CrystalPickup => _crystalPickup;
        public AudioClip WaveStart => _waveStart;

        // CR-CODE S-19 iteration (major, HealthSceneTests.cs/DashSceneTests.cs/CrystalSceneTests.cs):
        // the shared AudioSource's isPlaying flag gives illusory regression protection when multiple SFX
        // can be in flight on the same source in the same test (e.g. HealthSceneTests spawns the contact
        // enemy at the player's exact position, inside AutoAttackRange, so auto-attack's SFX-01 keeps the
        // shared source.isPlaying == true regardless of whether SFX-03 actually played). These per-clip
        // counters (mirrors GameHudController.WaveStartSfxTriggerCountForTests' identical pattern for
        // SFX-05) let tests assert the *specific* clip fired instead of "something is playing".
        public int AttackHitTriggerCountForTests { get; private set; }
        public int DashTriggerCountForTests { get; private set; }
        public int PlayerHitTriggerCountForTests { get; private set; }
        public int CrystalPickupTriggerCountForTests { get; private set; }

        private AudioSource _source;
        private bool _mixerErrorLogged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate SfxLibrary destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;

            RouteToMixerGroup();

            // Wiring guard (mirrors WaveSpawner's _prefabMissingAgentLogged pattern): SFX-01..05 are
            // all generated (GameConfig.AssetKeys.Sfx* non-empty), so a null slot here means
            // Editor/AssetIntegration.AssignClip failed to load/assign the clip, not "not yet
            // generated". Awake runs once per instance so no extra one-shot bool flag is needed.
            LogMissingClipIfExpected(_attackHit, GameConfig.AssetKeys.SfxAttackHit, nameof(_attackHit));
            LogMissingClipIfExpected(_dash, GameConfig.AssetKeys.SfxDash, nameof(_dash));
            LogMissingClipIfExpected(_playerHit, GameConfig.AssetKeys.SfxPlayerHit, nameof(_playerHit));
            LogMissingClipIfExpected(_crystalPickup, GameConfig.AssetKeys.SfxCrystalPickup, nameof(_crystalPickup));
            LogMissingClipIfExpected(_waveStart, GameConfig.AssetKeys.SfxWaveStart, nameof(_waveStart));
        }

        /// <summary>S-19: routes the shared AudioSource's output to the mixer's Sfx group (mirrors
        /// BgmPlayer.RouteToMixerGroup exactly — same shared mixer asset, different exposed bus) so
        /// PlayOneShot output reflects the settings-tab sfxVolume live. _mixer == null is a documented
        /// degraded state (asset not yet generated this session / Editor/AssetIntegration failure) — SFX
        /// still plays at unity gain, just without live bus attenuation.</summary>
        private void RouteToMixerGroup()
        {
            if (_mixer == null)
            {
                LogMixerErrorOnce("[Wiring] SfxLibrary.RouteToMixerGroup: AudioMixer not assigned (Editor/AssetIntegration.PatchSfxLibrary should have wired it) — SFX will play at unity gain, ignoring the sfxVolume bus this session");
                return;
            }
            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(GameConfig.Audio.MixerSfxGroupName);
            if (groups == null || groups.Length == 0)
            {
                LogMixerErrorOnce("[Wiring] SfxLibrary.RouteToMixerGroup: no AudioMixerGroup named '" + GameConfig.Audio.MixerSfxGroupName + "' found in mixer '" + _mixer.name + "'");
                return;
            }
            _source.outputAudioMixerGroup = groups[0];
        }

        private void LogMixerErrorOnce(string message)
        {
            if (_mixerErrorLogged)
            {
                return;
            }
            _mixerErrorLogged = true;
            Debug.LogError(message);
        }

        private static void LogMissingClipIfExpected(AudioClip clip, string assetKey, string fieldName)
        {
            if (clip == null && !string.IsNullOrEmpty(assetKey))
            {
                Debug.LogError($"[Wiring] SfxLibrary.{fieldName} is unassigned despite GameConfig.AssetKeys expecting a generated clip at '{assetKey}' — Editor/AssetIntegration wiring is broken.");
            }
        }

        /// <summary>Plays <paramref name="clip"/> once via the shared AudioSource at unity gain (1f) — the
        /// actual sfxVolume attenuation happens on the AudioMixer's Sfx bus (RouteToMixerGroup above), not
        /// as a per-call volumeScale (S-19; previously used the fixed GameConfig.Audio.DefaultSfxVolume as
        /// a placeholder before the mixer bus was wired end-to-end). No-op when <paramref name="clip"/> is
        /// null (not-yet-generated SFX degrade silently, not with an error).</summary>
        public void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }
            _source.PlayOneShot(clip);
            IncrementTriggerCountForTests(clip);
        }

        /// <summary>Identifies which named slot <paramref name="clip"/> came from by reference (clips are
        /// assigned once at edit time by Editor/AssetIntegration and never swapped at runtime) and bumps
        /// its test-observability counter. A clip that matches none of the known slots (e.g. null, already
        /// filtered above) increments nothing — this is test plumbing only, never surfaced as a wiring
        /// error.</summary>
        private void IncrementTriggerCountForTests(AudioClip clip)
        {
            if (clip == _attackHit)
            {
                AttackHitTriggerCountForTests++;
            }
            else if (clip == _dash)
            {
                DashTriggerCountForTests++;
            }
            else if (clip == _playerHit)
            {
                PlayerHitTriggerCountForTests++;
            }
            else if (clip == _crystalPickup)
            {
                CrystalPickupTriggerCountForTests++;
            }
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
