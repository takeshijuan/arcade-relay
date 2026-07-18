// GameHudController — Game シーンの HUD ライフサイクル配線 (ui-engineer, S-10). Every frame, reads the
// current HP/dash-cooldown/wave/score from the authoritative Components singletons (HealthComponent on
// the Player, PlayerController.DashCooldownRemaining, WaveSpawner.Instance.CurrentWave,
// RunStatsTracker.Instance) and pushes the values into the sibling Ui/GameHud (display). Thin by design
// (rule: Components はライフサイクルと配線のみ) — no HP/cooldown/wave/score arithmetic here beyond the
// single live-score formula call, which reuses Systems/ScoreSystem.ComputeFinalScore verbatim (式の
// 再実装禁止 pattern already used by MetaProgression.Effective* callers) rather than reimplementing gdd's
// score formula a second time. This also keeps the HUD's live score numerically identical to the value
// Components/HealthComponent.CompleteRun folds into RunResult at Result transition (no visual jump).
using ForgeGame.Systems;
using ForgeGame.Ui;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class GameHudController : MonoBehaviour
    {
        private GameHud _hud;
        private HealthComponent _health;

        private bool _healthMissingLogged;
        private bool _playerMissingLogged;
        private bool _waveSpawnerMissingLogged;
        private bool _statsMissingLogged;

        // S-16 (死亡演出): full-screen dissolve overlay progress. Own elapsed-time accumulator (started
        // the frame HealthComponent.IsDeathSequenceActive first flips true), mirroring
        // Components/HeroFxController's identical approach for the hero material fade/tilt — both race
        // the same GameConfig.Fx.DeathSequenceDuration constant instead of one reading the other's timer,
        // so the two never need cross-component synchronization (only correlated by the shared constant).
        private bool _deathDissolveActive;
        private float _deathDissolveElapsed;

        // S-15 (ウェーブ切替フィードバック): -1 sentinel means "no wave observed yet" — the very first
        // Update() must not treat WaveSpawner's initial CurrentWave(=1) as an "increase" from the C#
        // default int(0) and fire a spurious pulse/SFX on scene load. Mirrors HeroFxController/
        // GameHudController's own death-dissolve elapsed-time-accumulator pattern (own timer, driven by
        // Time.deltaTime, racing a shared GameConfig.Fx constant) rather than reading another component's
        // timer.
        private int _previousWaveForPulse = -1;
        private bool _wavePulseActive;
        private float _wavePulseElapsed;

        /// <summary>Test-observability accessor (mirrors Components/HealthComponent.
        /// SaveInvocationCountForTests): number of times this controller has triggered the wave-start SFX
        /// since it started running. S-15's PlayMode test uses this instead of AudioSource.isPlaying to
        /// assert the trigger fired exactly once per wave increase (a short one-shot clip's isPlaying flag
        /// can flip back to false before the assertion runs).</summary>
        public int WaveStartSfxTriggerCountForTests { get; private set; }

        private void Start()
        {
            _hud = GetComponent<GameHud>();
            if (_hud == null)
            {
                Debug.LogError("[Wiring] GameHudController requires a sibling GameHud component; HUD display is disabled");
                enabled = false;
                return;
            }

            ResolveHealth();
        }

        private void ResolveHealth()
        {
            if (PlayerController.Instance == null)
            {
                return;
            }
            _health = PlayerController.Instance.GetComponent<HealthComponent>();
        }

        private void Update()
        {
            if (_health == null)
            {
                ResolveHealth();
            }
            UpdateHp();
            UpdateDash();
            UpdateWave();
            UpdateWavePulse();
            UpdateScore();
            UpdateDeathDissolve();
        }

        private void UpdateHp()
        {
            if (_health == null)
            {
                LogMissingOnce(ref _healthMissingLogged, "Player/HealthComponent");
                return;
            }
            _hud.SetHp(_health.CurrentHp, _health.EffectiveMaxHp);
        }

        private void UpdateDash()
        {
            if (PlayerController.Instance == null)
            {
                LogMissingOnce(ref _playerMissingLogged, "PlayerController");
                return;
            }
            _hud.SetDashCooldown(PlayerController.Instance.DashCooldownRemaining, GameConfig.Player.DashCooldown);
        }

        private void UpdateWave()
        {
            if (WaveSpawner.Instance == null)
            {
                LogMissingOnce(ref _waveSpawnerMissingLogged, "WaveSpawner");
                return;
            }
            int wave = WaveSpawner.Instance.CurrentWave;
            _hud.SetWave(wave);
            ObserveWaveForPulse(wave);
        }

        /// <summary>S-15: detects a currentWave increase (gdd: 「currentWave が直前フレームから増加した瞬間」)
        /// and, on the transition frame only, starts the HUD pulse timer and fires the wave-start SFX once
        /// (via SfxLibrary.Play — a null Instance/clip degrades silently, same as every other
        /// SfxLibrary.Play call site in this codebase; not a wiring error worth logging, since
        /// Editor/AssetIntegration failing to assign an already-generated clip is SfxLibrary.Awake's own
        /// wiring-guard responsibility to surface, not this call site's).</summary>
        private void ObserveWaveForPulse(int wave)
        {
            if (_previousWaveForPulse < 0)
            {
                _previousWaveForPulse = wave; // first observation this scene — establish baseline only
                return;
            }
            if (!WavePulseSystem.HasWaveIncreased(_previousWaveForPulse, wave))
            {
                _previousWaveForPulse = wave;
                return;
            }
            _previousWaveForPulse = wave;

            _wavePulseActive = true;
            _wavePulseElapsed = 0f;
            if (SfxLibrary.Instance != null)
            {
                SfxLibrary.Instance.Play(SfxLibrary.Instance.WaveStart);
            }
            WaveStartSfxTriggerCountForTests++;
        }

        /// <summary>Drives Ui/GameHud.SetWaveScale from Systems/WavePulseSystem.ComputeScale while a pulse
        /// is active (S-15: gdd 決定「HUDのウェーブ数値表示を0.3秒かけて1.0→1.3→1.0倍にスケールさせる」).
        /// Snaps back to exactly 1.0 (not just whatever ComputeScale returns at t=1, which is already 1.0
        /// but this makes the terminal state explicit and stops the per-frame call once done). Called
        /// directly from Update() (not nested inside UpdateWave()'s WaveSpawner-presence guard) so an
        /// already-started pulse keeps decaying via its own elapsed-time accumulator even if WaveSpawner
        /// were to disappear mid-pulse — decay must not depend on the same data source that triggered it
        /// (CR-CODE S-15 iteration 1, finding on WaveSpawner-guard coupling).</summary>
        private void UpdateWavePulse()
        {
            if (!_wavePulseActive)
            {
                return;
            }
            _wavePulseElapsed += Time.deltaTime;
            float scale = WavePulseSystem.ComputeScale(_wavePulseElapsed, GameConfig.Fx.WavePulseDuration, GameConfig.Fx.WavePulseScale);
            _hud.SetWaveScale(scale);
            if (_wavePulseElapsed >= GameConfig.Fx.WavePulseDuration)
            {
                _wavePulseActive = false;
                _hud.SetWaveScale(1f);
            }
        }

        private void UpdateScore()
        {
            if (RunStatsTracker.Instance == null || _health == null)
            {
                LogMissingOnce(ref _statsMissingLogged, "RunStatsTracker/HealthComponent (for live score)");
                return;
            }
            int score = ScoreSystem.ComputeFinalScore(
                _health.SurvivalTimeSec, RunStatsTracker.Instance.NormalKillCount,
                RunStatsTracker.Instance.HeavyKillCount, RunStatsTracker.Instance.CrystalsCollected);
            _hud.SetScore(score);
        }

        /// <summary>Drives Ui/GameHud.DeathDissolveOverlay from Components/HealthComponent.IsDeathSequenceActive
        /// (S-16: gdd 決定「画面全体のディゾルブ/フェードVFX」). Silently no-ops while _health is unresolved
        /// (UpdateHp already logs the missing-HealthComponent wiring error once — no need to double-log
        /// the same underlying cause here).</summary>
        private void UpdateDeathDissolve()
        {
            if (_health == null)
            {
                return;
            }
            if (!_health.IsDeathSequenceActive)
            {
                if (_deathDissolveActive)
                {
                    // Defensive reset for a hypothetical scene reuse without a full reload — not
                    // currently reachable (death always transitions to Result, which loads fresh), but
                    // keeps this component's state machine self-consistent rather than assuming so.
                    _deathDissolveActive = false;
                    _deathDissolveElapsed = 0f;
                    _hud.SetDeathDissolve(0f);
                }
                return;
            }

            if (!_deathDissolveActive)
            {
                _deathDissolveActive = true;
                _deathDissolveElapsed = 0f;
            }
            _deathDissolveElapsed += Time.deltaTime;
            float progress = HeroFxSystem.ComputeProgress(_deathDissolveElapsed, GameConfig.Fx.DeathSequenceDuration);
            _hud.SetDeathDissolve(HeroFxSystem.ComputeDissolveAlpha(progress));
        }

        private static void LogMissingOnce(ref bool alreadyLogged, string what)
        {
            if (alreadyLogged)
            {
                return;
            }
            alreadyLogged = true;
            Debug.LogError($"[Wiring] GameHudController: {what} missing at Game — corresponding HUD element(s) will not update");
        }
    }
}
