// TitleController — Title シーンのライフサイクル配線 (ui-engineer, S-02). Enables the UI input map,
// forwards SessionHolder.Recovered to the presenter (Ui/TitleScreen), and on Submit/Cancel drives
// the scene transition / quit request (gdd ゲームフロー: Title→Menu 決定 / Title→終了 Esc).
// Thin by design (rule: Components はライフサイクルと配線のみ) — no game logic beyond wiring.
// Degradation note: if SessionHolder.Instance is null (Title loaded without going through Boot,
// e.g. wiring break or a PlayMode test that loads Title standalone), recovered is treated as
// false — there is no session state to report a corruption-recovery notice for. This is a
// legitimate default (not silent): Start() logs a warning once so a genuine Boot→Title wiring
// break (where SaveCorruption fired but the flag failed to propagate) is still observable.
using ForgeGame.InputLayer;
using ForgeGame.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeGame.Components
{
    public sealed class TitleController : MonoBehaviour
    {
        /// <summary>
        /// Count of Esc-driven quit requests this session. Real desktop builds also call
        /// Application.Quit() (guarded by !Application.isEditor — PlayMode tests always run
        /// inside the Editor process, so batchmode test runs never actually exit); tests assert
        /// on this counter as the observable proxy for "quit hook fired" (story acceptance).
        /// </summary>
        public int QuitRequestedCount { get; private set; }

        private GameInput _input;
        private TitleScreen _screen;

        private void Awake()
        {
            _input = new GameInput();
        }

        private void Start()
        {
            _input.EnableUi();

            _screen = GetComponent<TitleScreen>();
            if (_screen == null)
            {
                Debug.LogError("[Wiring] TitleController requires a sibling TitleScreen component; recovery notice will not display");
                return;
            }

            if (SessionHolder.Instance == null)
            {
                Debug.LogWarning("[Wiring] SessionHolder missing at Title (not loaded via Boot, or session state lost); treating as recovered=false");
            }
            bool recovered = SessionHolder.Instance != null && SessionHolder.Instance.Recovered;
            _screen.SetRecoveryNoticeVisible(recovered);
        }

        private void Update()
        {
            if (_input.Submit.WasPressedThisFrame())
            {
                SceneManager.LoadScene(GameConfig.Scenes.Menu);
                return;
            }
            if (_input.Cancel.WasPressedThisFrame())
            {
                RequestQuit();
            }
        }

        private void RequestQuit()
        {
            QuitRequestedCount++;
            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }
    }
}
