// MenuController — Menu シーンのライフサイクル配線 (ui-engineer, S-03; アップグレード購入配線=gameplay-engineer,
// S-12). Drives Systems/MenuNavigation (pure tab-cycle/focus-move math) from GameInput and pushes the
// result to Ui/MenuScreen (display). Thin by design (rule: Components はライフサイクルと配線のみ) — purchase
// arithmetic itself lives in Systems/Meta/MetaProgression.TryPurchase (pure reducer); this component
// only reads the focused Upgrade row, calls the reducer, persists via Persistence/FileSaveAdapter, and
// refreshes SessionHolder/MenuScreen with the result (設定タブのスライダー調整=S-13。本 story はアップグレード
// タブの購入操作のみ).
//
// Degradation note (mirrors Components/TitleController's documented rationale): if
// SessionHolder.Instance/.Save is null when Menu loads, this is treated as a legitimate default
// (empty/default SaveData for display) rather than a hard wiring failure — PlayMode tests that
// load Title/Menu standalone (without going through Boot) legitimately hit this path, and normal
// gameplay always reaches Menu via Boot (which populates SessionHolder before Title/Menu run).
// Start() logs a warning once so a genuine Boot→Menu wiring break is still observable.
using ForgeGame.InputLayer;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ForgeGame.Components
{
    public sealed class MenuController : MonoBehaviour
    {
        /// <summary>Test seam (conventions.md §9 / mirrors Components/HealthComponent's identical
        /// field): PlayMode purchase tests point this at an Application.temporaryCachePath directory
        /// so purchase persistence never touches the real Application.persistentDataPath save. Null
        /// (default — SceneWiring/production never sets it) resolves to FileSaveAdapter's default ctor.</summary>
        public static string SaveDirectoryOverrideForTests;

        /// <summary>S-13: baked by Editor/SceneWiring.WireMenuAudioMixer (SerializedObject assignment —
        /// see WaveSpawner.enemyPrefab/SfxLibrary's AudioClip fields for the identical pattern). Null is
        /// a legitimate degraded state (AudioMixerSetup failed/asset not yet generated this session) —
        /// ApplyMixerVolumes logs this (and the separate SetFloat-returned-false case) once per session
        /// via _mixerErrorLogged rather than on every Start()/A-D-press call, and no-ops rather than
        /// throwing; volume changes still persist to SaveData either way (conventions.md role「UI は
        /// 表示専任・状態は game state が正」— the mixer bus is a presentation-layer side effect of the
        /// save, not the save's source of truth). CR-CODE S-13 iteration 1, finding minor-1: comment
        /// previously claimed a once-guard that did not exist in the implementation; fixed by adding one.</summary>
        [SerializeField] private AudioMixer _mixer;

        /// <summary>CR-CODE S-13 iteration 1, findings minor-1/major: guards ApplyMixerVolumes' error
        /// logging (both the null-_mixer branch and the SetFloat-returned-false branch below) to once per
        /// Menu session instead of once per Start()/A-D-adjustment call, so a genuine wiring/asset-drift
        /// failure is still surfaced without spamming the log on every keypress.</summary>
        private bool _mixerErrorLogged;

        /// <summary>S-19 音声統合: SFX-06 (アップグレード購入確定). Baked by
        /// Editor/AssetIntegration.PatchMenuPurchaseSfx (mirrors WireMenuAudioMixer's SerializedObject
        /// pattern) from GameConfig.AssetKeys.SfxUpgradePurchase. GameConfig.AssetKeys.SfxUpgradePurchase
        /// is non-empty (SFX-06 is generated — see GameConfig.cs), so a null field here means
        /// Editor/AssetIntegration.AssignClip failed to load/assign the clip, not "not yet generated" —
        /// Awake logs this via LogMissingClipIfExpected below (mirrors Components/SfxLibrary's identical
        /// guard). PlayUpgradePurchaseSfx itself still no-ops on null rather than throwing (same runtime
        /// policy as Components/SfxLibrary.Play(null)) — the Awake log is what makes a broken wiring
        /// state observable instead of silently swallowed.
        /// CR-CODE s-19 iteration 1 minor finding: this comment previously claimed null was always a
        /// legitimate "asset not yet generated this session" state — that was true when this component
        /// was first written (before audio-designer generated SFX-06) but became stale/false the moment
        /// GameConfig.AssetKeys.SfxUpgradePurchase stopped being an empty string in this same story's
        /// diff. Corrected here alongside adding the missing wiring-error log.</summary>
        [SerializeField] private AudioClip _upgradePurchaseSfx;

        /// <summary>S-19: dedicated AudioSource for _upgradePurchaseSfx (Editor/AssetIntegration.
        /// PatchMenuPurchaseSfx ensures one exists on this GameObject), routed to the mixer's Sfx group in
        /// Awake — mirrors Components/SfxLibrary.RouteToMixerGroup/BgmPlayer.RouteToMixerGroup exactly.</summary>
        private AudioSource _sfxSource;

        /// <summary>Guards RouteSfxSourceToMixerGroup's own error logging to once per session, kept as a
        /// separate flag from _mixerErrorLogged above (rather than sharing one guard across both call
        /// sites). CR-CODE S-19 iteration 2 minor finding: a prior version of this comment claimed the
        /// split exists to avoid "double-logging the same session" — backwards: in the common single-
        /// failure case (mixer never wired, MenuScreen present) both RouteSfxSourceToMixerGroup (Awake)
        /// and ApplyMixerVolumes (Start) *do* still log once each, because they describe different
        /// consequences of the same missing-_mixer root cause (SFX-06 vs. BGM/SFX volume sliders will not
        /// apply). Sharing a single guard would let whichever call site runs first (always
        /// RouteSfxSourceToMixerGroup, since Awake precedes Start) consume the flag and silently suppress
        /// ApplyMixerVolumes' own — otherwise independent — failure-mode logging (e.g. the
        /// AudioMixer.SetFloat-returned-false branch, which is unrelated to _mixer being null). Separate
        /// flags is what keeps each call site's own distinct failure mode observable.</summary>
        private bool _sfxMixerErrorLogged;

        private GameInput _input;
        private MenuScreen _screen;

        private int _activeTab;
        private int _focusIndex;
        private Vector2 _prevNavigate;
        private float _prevAdjust;

        private void Awake()
        {
            _input = new GameInput();

            _sfxSource = GetComponent<AudioSource>();
            if (_sfxSource != null)
            {
                _sfxSource.playOnAwake = false;
                RouteSfxSourceToMixerGroup();
            }
            else
            {
                Debug.LogError("[Wiring] MenuController: AudioSource missing (Editor/AssetIntegration.PatchMenuPurchaseSfx should have ensured one) — SFX-06 (アップグレード購入確定) will not play this session");
            }

            // CR-CODE s-19 iteration 1 minor finding: mirrors Components/SfxLibrary.Awake's identical
            // LogMissingClipIfExpected guard — GameConfig.AssetKeys.SfxUpgradePurchase is non-empty
            // (SFX-06 is generated), so a null _upgradePurchaseSfx here means
            // Editor/AssetIntegration.AssignClip failed, not "not yet generated"; that broken state was
            // previously completely silent (PlayUpgradePurchaseSfx's null-check just no-ops every purchase).
            LogMissingClipIfExpected(_upgradePurchaseSfx, GameConfig.AssetKeys.SfxUpgradePurchase, nameof(_upgradePurchaseSfx));
        }

        private static void LogMissingClipIfExpected(AudioClip clip, string assetKey, string fieldName)
        {
            if (clip == null && !string.IsNullOrEmpty(assetKey))
            {
                Debug.LogError($"[Wiring] MenuController.{fieldName} is unassigned despite GameConfig.AssetKeys expecting a generated clip at '{assetKey}' — Editor/AssetIntegration.PatchMenuPurchaseSfx wiring is broken.");
            }
        }

        /// <summary>S-19: mirrors Components/SfxLibrary.RouteToMixerGroup/BgmPlayer.RouteToMixerGroup —
        /// same shared mixer asset (_mixer, already baked by Editor/SceneWiring.WireMenuAudioMixer for
        /// S-13), routed to the Sfx bus instead of Bgm. _mixer == null degrades to unity-gain playback,
        /// logged once via _sfxMixerErrorLogged below.
        /// CR-CODE S-19 finding (minor): this previously no-op'd on the assumption that
        /// ApplyMixerVolumes (called from Start()) always logs the missing-_mixer case first. That is
        /// false when _screen == null — Start() early-returns before reaching ApplyMixerVolumes (see
        /// below), so a double wiring failure (MenuScreen missing + AudioMixer missing) left the
        /// missing-_mixer state completely unlogged. Awake (where this method runs) always executes
        /// regardless of _screen, so logging here independently — via the dedicated _sfxMixerErrorLogged
        /// guard (see its own field comment for why it is kept separate from ApplyMixerVolumes'
        /// _mixerErrorLogged rather than shared) — closes that gap.</summary>
        private void RouteSfxSourceToMixerGroup()
        {
            if (_mixer == null)
            {
                if (!_sfxMixerErrorLogged)
                {
                    _sfxMixerErrorLogged = true;
                    Debug.LogError("[Wiring] MenuController.RouteSfxSourceToMixerGroup: AudioMixer not assigned (Editor/SceneWiring.WireMenuAudioMixer should have wired it) — SFX-06 will play at unity gain, ignoring the sfxVolume bus this session");
                }
                return;
            }
            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(GameConfig.Audio.MixerSfxGroupName);
            if (groups == null || groups.Length == 0)
            {
                if (!_sfxMixerErrorLogged)
                {
                    _sfxMixerErrorLogged = true;
                    Debug.LogError("[Wiring] MenuController.RouteSfxSourceToMixerGroup: no AudioMixerGroup named '" + GameConfig.Audio.MixerSfxGroupName + "' found in mixer '" + _mixer.name + "'");
                }
                return;
            }
            _sfxSource.outputAudioMixerGroup = groups[0];
        }

        /// <summary>S-19: plays SFX-06 once via the dedicated Sfx-routed AudioSource. No-op when the clip
        /// or AudioSource is unassigned (mirrors Components/SfxLibrary.Play's null-safety policy).</summary>
        private void PlayUpgradePurchaseSfx()
        {
            if (_sfxSource == null || _upgradePurchaseSfx == null)
            {
                return;
            }
            _sfxSource.PlayOneShot(_upgradePurchaseSfx);
        }

        private void Start()
        {
            _input.EnableUi();

            _screen = GetComponent<MenuScreen>();
            if (_screen == null)
            {
                Debug.LogError("[Wiring] MenuController requires a sibling MenuScreen component; Menu rendering and tab/focus navigation are disabled (Submit->Game and Cancel->Title remain functional as escape routes)");
                return;
            }

            SaveData save = ResolveSaveForDisplay();
            _screen.RenderStats(save);
            ApplyMixerVolumes(save); // gdd: Menu を開いた時点で既に保存済みの音量をバスへ反映する

            _activeTab = GameConfig.Ui.MenuTabIndex.Start;
            _focusIndex = 0;
            _screen.SetActiveTab(_activeTab);
            _screen.SetFocusIndex(_activeTab, _focusIndex);
        }

        private static SaveData ResolveSaveForDisplay()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.Save != null)
            {
                return SessionHolder.Instance.Save;
            }
            Debug.LogWarning("[Wiring] SessionHolder/Save missing at Menu (not loaded via Boot, or session state lost); showing default SaveData");
            return SaveData.CreateDefault();
        }

        private void Update()
        {
            // Submit/Cancel scene transitions do not depend on _screen (no rendering/display
            // state is read here) — they must stay reachable even if MenuScreen failed to wire,
            // otherwise a Menu-load wiring break would strand the player with zero exits.
            if (_input.Submit.WasPressedThisFrame() && _activeTab == GameConfig.Ui.MenuTabIndex.Start)
            {
                SceneManager.LoadScene(GameConfig.Scenes.Game);
                return;
            }
            if (_input.Cancel.WasPressedThisFrame())
            {
                SceneManager.LoadScene(GameConfig.Scenes.Title);
                return;
            }

            if (_screen == null)
            {
                return;
            }

            HandleTabSwitch();
            HandleFocusMove();
            HandlePurchase();
            HandleVolumeAdjust();
        }

        /// <summary>
        /// gdd Menu「アップグレード」タブ: 決定キーで現在フォーカス中の UPG-01〜03 を購入する
        /// (MetaProgression.TryPurchase — 残高不足/上限Lvは購入されず留まる。式の再実装禁止). Purchases are
        /// only evaluated on the Upgrade tab so Submit on other tabs keeps its existing meaning
        /// (Start tab's transition is handled earlier in Update, before this method runs).
        /// </summary>
        private void HandlePurchase()
        {
            if (_activeTab != GameConfig.Ui.MenuTabIndex.Upgrade)
            {
                return;
            }
            if (!_input.Submit.WasPressedThisFrame())
            {
                return;
            }

            SaveData current = ResolveSaveForDisplay();
            MetaProgression.UpgradeKind? kind = UpgradeKindForFocusIndex(_focusIndex);
            if (kind == null)
            {
                // CR-CODE S-12 iteration 1, finding m-1: an out-of-range focusIndex is a wiring bug
                // (MenuNavigation.MoveFocus already clamps to 0..itemCount-1 and tab switches reset
                // focus to 0, so this should be unreachable in normal play). Abort the purchase rather
                // than silently falling back to a fixed UpgradeKind — a wrong-item fallback would spend
                // the player's currency on an upgrade they never selected, which is a worse failure
                // mode than "nothing happens" for an already-logged wiring error.
                return;
            }

            MetaProgression.PurchaseResult result = MetaProgression.TryPurchase(current, kind.Value);
            if (!result.Purchased)
            {
                return; // gdd: 残高不足/上限Lvでは購入されず留まる（画面は変えない）
            }

            PersistPurchase(result.Data);
            _screen.RenderStats(result.Data);
            PlayUpgradePurchaseSfx(); // gdd 音要件: 購入確定にSFX-06を鳴らす（S-19）
        }

        /// <summary>Derives the purchased UpgradeKind from GameConfig.Ui.MenuUpgradeRowKinds (single
        /// source of truth for row index -> kind; CR-CODE S-12 iteration 1, finding M-2). Returns null
        /// for an out-of-range focusIndex (wiring bug) after logging — see the null-check above.</summary>
        private static MetaProgression.UpgradeKind? UpgradeKindForFocusIndex(int focusIndex)
        {
            MetaProgression.UpgradeKind[] kinds = GameConfig.Ui.MenuUpgradeRowKinds;
            if (focusIndex < 0 || focusIndex >= kinds.Length)
            {
                Debug.LogError($"[Wiring] MenuController: unexpected Upgrade tab focusIndex={focusIndex} (expected 0..{kinds.Length - 1}); purchase skipped");
                return null;
            }
            return kinds[focusIndex];
        }

        /// <summary>Persists a successful purchase immediately (gdd: 「即セーブ」) and keeps the
        /// DontDestroyOnLoad SessionHolder as the in-memory source of truth so a subsequent purchase
        /// in the same Menu session, or a Game load right after, sees the updated balance/levels
        /// without requiring a re-load from disk (mirrors Components/HealthComponent.CompleteRun's
        /// save-then-UpdateSave sequencing).</summary>
        private static void PersistPurchase(SaveData data)
        {
            FileSaveAdapter adapter = SaveDirectoryOverrideForTests != null
                ? new FileSaveAdapter(SaveDirectoryOverrideForTests)
                : new FileSaveAdapter();
            try
            {
                adapter.Save(data);
            }
            catch (System.Exception ex)
            {
                // I/O failure (disk full / permissions / locked file) must not strand the purchase
                // silently — mirrors HealthComponent.CompleteRun's treatment of the same failure mode.
                // The in-memory SessionHolder is still updated below so the Menu/Game reflect the
                // purchase for the remainder of this session even though disk persistence failed.
                // CR-CODE S-12 iteration 1, finding m-3: reviewer suggested narrowing this to
                // IOException/UnauthorizedAccessException. Kept as System.Exception intentionally —
                // FileSaveAdapter.Save's call chain (Directory.CreateDirectory / File.WriteAllText /
                // File.Exists / File.Delete / File.Move) can also throw ArgumentException,
                // PathTooLongException, DirectoryNotFoundException, SecurityException, or platform-
                // specific I/O exceptions depending on the failure; this is a last-resort safety net
                // whose job is "never let a save-write fault crash or silently drop a purchase", not to
                // discriminate between I/O failure modes, and narrowing it would just reintroduce the
                // same "unhandled exception during purchase" risk this catch exists to close. Matches
                // the already-reviewed HealthComponent.CompleteRun pattern for consistency.
                Debug.LogError("[SaveCorruption] save write failed: " + ex);
            }

            if (SessionHolder.Instance != null)
            {
                SessionHolder.Instance.UpdateSave(data);
            }
            else
            {
                Debug.LogError("[Wiring] MenuController: SessionHolder.Instance missing after purchase; save persisted to disk (if reachable) but in-memory session not updated");
            }
        }

        /// <summary>
        /// gdd Menu「設定」タブ: A/Dでフォーカス中のBGM/SFX音量スライダーをVolumeStep刻みで増減し、即時
        /// AudioMixerへ反映＋即セーブする（Settings タブ以外・フォーカス外の項目では無反応。決定キーは
        /// このタブでは元々どこにも配線されていないため「フォーカス中は決定キー無効」は構造的に成立する
        /// — HandlePurchase は Upgrade タブのみ、Update 冒頭の Submit チェックは Start タブのみを扱う）。
        /// </summary>
        private void HandleVolumeAdjust()
        {
            if (_activeTab != GameConfig.Ui.MenuTabIndex.Settings)
            {
                _prevAdjust = 0f; // mirrors HandleFocusMove's _prevNavigate reset-per-frame-off-tab pattern
                return;
            }

            float adjust = _input.Adjust.ReadValue<float>();
            float threshold = GameConfig.Ui.MenuNavigateAxisThreshold; // same edge-detection threshold as Navigate
            int direction = 0;
            if (adjust > threshold && _prevAdjust <= threshold) direction = +1;
            else if (adjust < -threshold && _prevAdjust >= -threshold) direction = -1;
            _prevAdjust = adjust;

            if (direction == 0)
            {
                return;
            }

            SaveData current = ResolveSaveForDisplay();
            SaveData next = current.Clone();
            if (_focusIndex == GameConfig.Ui.MenuSettingsIndex.Bgm)
            {
                next.bgmVolume = VolumeControl.Step(current.bgmVolume, GameConfig.Audio.VolumeStep, direction);
            }
            else if (_focusIndex == GameConfig.Ui.MenuSettingsIndex.Sfx)
            {
                next.sfxVolume = VolumeControl.Step(current.sfxVolume, GameConfig.Audio.VolumeStep, direction);
            }
            else
            {
                Debug.LogError($"[Wiring] MenuController.HandleVolumeAdjust: unexpected Settings tab focusIndex={_focusIndex}");
                return;
            }

            PersistSettings(next);
            ApplyMixerVolumes(next);
            _screen.RenderStats(next);
        }

        /// <summary>Persists a settings (volume) change immediately (gdd: 「即時反映...+即時セーブ」),
        /// mirroring PersistPurchase's save/error-handling/SessionHolder-update sequencing exactly (kept
        /// as a separate method rather than a shared helper to avoid touching PersistPurchase's already
        /// CR-CODE-reviewed body — see its own comments for the rationale behind each step).</summary>
        private static void PersistSettings(SaveData data)
        {
            FileSaveAdapter adapter = SaveDirectoryOverrideForTests != null
                ? new FileSaveAdapter(SaveDirectoryOverrideForTests)
                : new FileSaveAdapter();
            try
            {
                adapter.Save(data);
            }
            catch (System.Exception ex)
            {
                // Same last-resort safety net as PersistPurchase's identical catch (see its comment for
                // the full exception-type rationale) — a save-write fault must not crash or silently
                // drop a volume change.
                Debug.LogError("[SaveCorruption] save write failed: " + ex);
            }

            if (SessionHolder.Instance != null)
            {
                SessionHolder.Instance.UpdateSave(data);
            }
            else
            {
                Debug.LogError("[Wiring] MenuController: SessionHolder.Instance missing after settings change; save persisted to disk (if reachable) but in-memory session not updated");
            }
        }

        /// <summary>Pushes bgmVolume/sfxVolume (linear 0..1) onto the AudioMixer's exposed BgmVolume/
        /// SfxVolume float parameters (dB, via VolumeControl.LinearToDecibel) — the gdd「即時反映
        /// （AudioMixerバス）」requirement. _mixer==null is a documented degraded state (see its field
        /// comment), not a hard failure: SaveData already carries the authoritative value regardless.
        /// CR-CODE S-13 iteration 1, finding major: AudioMixer.SetFloat is documented (AudioMixerSetup.cs
        /// header) to fail SILENTLY (return false, no exception/log) when the exposed-parameter name is
        /// unrecognized or the native mixer state is not live — e.g. the mixer asset regenerated with a
        /// failed rename, or the audio system not initialized yet. Both return values are now checked and
        /// logged (once per session via _mixerErrorLogged) so this degraded path is no longer silent,
        /// matching the null-_mixer branch's existing logging.</summary>
        private void ApplyMixerVolumes(SaveData data)
        {
            if (_mixer == null)
            {
                LogMixerErrorOnce("[Wiring] MenuController.ApplyMixerVolumes: AudioMixer not assigned (Editor/SceneWiring.WireMenuAudioMixer should have wired it) — volume change was saved but not applied to the mixer bus this session");
                return;
            }

            bool bgmOk = _mixer.SetFloat(GameConfig.Audio.MixerBgmVolumeParam, VolumeControl.LinearToDecibel(data.bgmVolume));
            bool sfxOk = _mixer.SetFloat(GameConfig.Audio.MixerSfxVolumeParam, VolumeControl.LinearToDecibel(data.sfxVolume));
            if (!bgmOk || !sfxOk)
            {
                LogMixerErrorOnce($"[Wiring] MenuController.ApplyMixerVolumes: AudioMixer.SetFloat returned false (bgmOk={bgmOk}, sfxOk={sfxOk}) — exposed parameter '{GameConfig.Audio.MixerBgmVolumeParam}'/'{GameConfig.Audio.MixerSfxVolumeParam}' missing or audio system not live; volume change was saved but not applied to the mixer bus this session");
            }
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

        private void HandleTabSwitch()
        {
            int direction = 0;
            if (_input.TabNext.WasPressedThisFrame()) direction = +1;
            else if (_input.TabPrev.WasPressedThisFrame()) direction = -1;
            if (direction == 0)
            {
                return;
            }

            _activeTab = MenuNavigation.CycleTab(_activeTab, GameConfig.Ui.MenuTabIndex.Count, direction);
            _focusIndex = 0; // gdd: タブ切替時はフォーカスをそのタブの先頭項目にリセット
            _screen.SetActiveTab(_activeTab);
            _screen.SetFocusIndex(_activeTab, _focusIndex);
        }

        private void HandleFocusMove()
        {
            Vector2 nav = _input.Navigate.ReadValue<Vector2>();
            float threshold = GameConfig.Ui.MenuNavigateAxisThreshold;

            int direction = 0;
            if (nav.y > threshold && _prevNavigate.y <= threshold) direction = -1;      // W = 上 = 前の項目
            else if (nav.y < -threshold && _prevNavigate.y >= -threshold) direction = +1; // S = 下 = 次の項目
            _prevNavigate = nav;

            if (direction == 0)
            {
                return;
            }

            int itemCount = ItemCountForTab(_activeTab);
            _focusIndex = MenuNavigation.MoveFocus(_focusIndex, itemCount, direction);
            _screen.SetFocusIndex(_activeTab, _focusIndex);
        }

        private static int ItemCountForTab(int tabIndex)
        {
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Start) return GameConfig.Ui.MenuItemCount.Start;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Stats) return GameConfig.Ui.MenuItemCount.Stats;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Upgrade) return GameConfig.Ui.MenuItemCount.Upgrade;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Settings) return GameConfig.Ui.MenuItemCount.Settings;
            Debug.LogError($"[Wiring] ItemCountForTab: unknown tabIndex={tabIndex}");
            return 0;
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }
    }
}
