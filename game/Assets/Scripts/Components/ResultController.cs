// ResultController — Result シーンのライフサイクル配線 (ui-engineer, S-11 / S-33). Enables the UI input map,
// reads Components/SessionHolder.LastRunResult/.LastRunHighScoreUpdated (populated once by
// Components/HealthComponent.CompleteRun — architecture.md §2) into the presenter (Ui/ResultScreen),
// and on Submit/Cancel drives the scene transition (gdd ゲームフロー: Result→Game 決定=即リスタート /
// Result→Menu Esc または「メニューへ」). Thin by design (rule: Components はライフサイクルと配線のみ) — no
// score/highscore arithmetic here; HealthComponent already computed RunResult/highScoreUpdated, this
// component only forwards them to the display and drives two display-only timers (S-33's score count-up
// and high-score notice pulse) using Systems/ResultCountUpSystem + Systems/WavePulseSystem's pure math
// (mirrors Components/GameHudController's own-elapsed-time-accumulator pattern for S-15/S-16).
//
// Degradation note (mirrors Components/TitleController's and Components/MenuController's documented
// rationale): if SessionHolder.Instance/.LastRunResult is null (Result loaded without going through a
// completed run — e.g. wiring break, or a PlayMode test that loads Result standalone), this is treated
// as a legitimate default (zeroed RunResult, no high-score notice) rather than a hard wiring failure.
// Start() logs a warning once so a genuine Game→Result wiring break (where CompleteRun ran but the
// RunResult failed to propagate) is still observable.
using ForgeGame.InputLayer;
using ForgeGame.Systems;
using ForgeGame.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeGame.Components
{
    public sealed class ResultController : MonoBehaviour
    {
        private GameInput _input;
        private ResultScreen _screen;
        private bool _screenReady;

        private RunResult _run;
        private bool _highScoreUpdated;

        // S-33: own elapsed-time accumulators (Time.deltaTime-driven), started once in Start() after the
        // run/highScoreUpdated values are resolved — mirrors GameHudController's death-dissolve/wave-pulse
        // timers rather than reading another component's clock.
        private bool _scoreCountUpActive;
        private float _scoreCountUpElapsedS;
        private bool _highScoreNoticePulseActive;
        private float _highScoreNoticePulseElapsedS;

        private void Awake()
        {
            _input = new GameInput();
        }

        private void Start()
        {
            _input.EnableUi();

            _screen = GetComponent<ResultScreen>();
            if (_screen == null)
            {
                Debug.LogError("[Wiring] ResultController requires a sibling ResultScreen component; run summary will not display (Submit->Game and Cancel->Menu remain functional as escape routes)");
                return;
            }

            (_run, _highScoreUpdated) = ResolveLastRun();
            _screen.SetRunResult(_run, _highScoreUpdated);

            _scoreCountUpActive = true;
            _scoreCountUpElapsedS = 0f;
            _highScoreNoticePulseActive = _highScoreUpdated;
            _highScoreNoticePulseElapsedS = 0f;
            _screenReady = true;
        }

        private static (RunResult, bool) ResolveLastRun()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.LastRunResult.HasValue)
            {
                return (SessionHolder.Instance.LastRunResult.Value, SessionHolder.Instance.LastRunHighScoreUpdated);
            }
            Debug.LogWarning("[Wiring] SessionHolder/LastRunResult missing at Result (not reached via a completed Game run, or session state lost); showing a zeroed run summary");
            return (default, false);
        }

        private void Update()
        {
            // Submit/Cancel scene transitions do not depend on _screen (no rendering/display state is
            // read here) — they must stay reachable even if ResultScreen failed to wire, otherwise a
            // Result-load wiring break would strand the player with zero exits (mirrors
            // Components/MenuController's Submit/Cancel-independent-of-_screen pattern). This also
            // satisfies S-33's acceptance that the count-up/pulse animations never block or delay Submit/
            // Cancel — the checks run unconditionally, before (and independent of) the animation updates
            // below, and both `return` immediately on a scene-load trigger.
            if (_input.Submit.WasPressedThisFrame())
            {
                SceneManager.LoadScene(GameConfig.Scenes.Game);
                return;
            }
            if (_input.Cancel.WasPressedThisFrame())
            {
                SceneManager.LoadScene(GameConfig.Scenes.Menu);
                return;
            }

            if (!_screenReady)
            {
                return;
            }
            UpdateScoreCountUp();
            UpdateHighScoreNoticePulse();
        }

        /// <summary>S-33: drives Ui/ResultScreen.SetFinalScoreValue from Systems/ResultCountUpSystem while
        /// the count-up is active (gdd 決定「最終スコアを0から最終値まで一定時間かけてカウントアップ表示し
        /// （イーズアウトで終盤ほど刻みが小さくなる）」). Snaps to the exact final score once the duration
        /// elapses (ResultCountUpSystem already converges there, but this makes the terminal state
        /// explicit and stops the per-frame call once done — mirrors GameHudController.UpdateWavePulse's
        /// identical snap-then-stop pattern).</summary>
        private void UpdateScoreCountUp()
        {
            if (!_scoreCountUpActive)
            {
                return;
            }
            _scoreCountUpElapsedS += Time.deltaTime;
            int displayed = ResultCountUpSystem.ComputeDisplayedScore(
                _scoreCountUpElapsedS, GameConfig.Fx.ResultScoreCountUpDurationS, _run.FinalScore, GameConfig.Fx.ResultCountUpEaseExponent);
            _screen.SetFinalScoreValue(displayed);
            if (_scoreCountUpElapsedS >= GameConfig.Fx.ResultScoreCountUpDurationS)
            {
                _scoreCountUpActive = false;
                _screen.SetFinalScoreValue(_run.FinalScore);
            }
        }

        /// <summary>S-33: drives Ui/ResultScreen.SetHighScoreNoticeScale from Systems/WavePulseSystem.
        /// ComputeScale (S-15と同一の三角波パルス関数を再利用) while a new-high-score run's notice pulse is
        /// active. Never starts (stays inactive for the whole scene) when highScoreUpdated was false, so a
        /// run that did not beat the high score never pulses the (inactive) notice object.</summary>
        private void UpdateHighScoreNoticePulse()
        {
            if (!_highScoreNoticePulseActive)
            {
                return;
            }
            _highScoreNoticePulseElapsedS += Time.deltaTime;
            float scale = WavePulseSystem.ComputeScale(
                _highScoreNoticePulseElapsedS, GameConfig.Fx.ResultHighScoreNoticePulseDurationS, GameConfig.Fx.ResultHighScoreNoticeScalePulse);
            _screen.SetHighScoreNoticeScale(scale);
            if (_highScoreNoticePulseElapsedS >= GameConfig.Fx.ResultHighScoreNoticePulseDurationS)
            {
                _highScoreNoticePulseActive = false;
                _screen.SetHighScoreNoticeScale(1f);
            }
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }
    }
}
