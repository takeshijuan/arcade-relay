// ResultScreen — Result シーンの表示専任コンポーネント (ui-engineer, S-11). Builds the ScreenSpaceCamera
// Canvas + Text UI entirely in code at Awake (engine=unity 方針: uGUI をコード中心に構築), mirroring
// Ui/TitleScreen and Ui/MenuScreen's code-first pattern. All sizes/colors/text content come from
// GameConfig.Ui (no magic numbers/hardcoded strings here).
//
// Display-only: SetRunResult only formats and applies the RunResult/highScoreUpdated values handed in
// by Components/ResultController, which reads the authoritative Components/SessionHolder.LastRunResult —
// this component never reads game state itself and holds no duplicate copy of it beyond the last values
// passed in (display cache, not a second source of truth; conventions.md role「UI は表示専任・状態は
// game state が正」).
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    public sealed class ResultScreen : MonoBehaviour
    {
        /// <summary>S-30: baked by Editor/SceneWiring.WireResultUiFrameKit — same null-degrades-gracefully
        /// pattern as Ui/TitleScreen's IMG-05 fields.</summary>
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _ribbonSprite;
        [SerializeField] private Sprite _cornerSprite;

        public Canvas Canvas { get; private set; }
        public Text TitleText { get; private set; }
        public Text FinalScoreText { get; private set; }
        public Text SurvivalTimeText { get; private set; }
        public Text WaveReachedText { get; private set; }
        public GameObject HighScoreNoticeObject { get; private set; }
        public Text HighScoreNoticeText { get; private set; }
        public Text RestartHintText { get; private set; }
        public Text MenuHintText { get; private set; }

        /// <summary>S-30: decorative Images built from IMG-05 (panel background + heading ribbon + two
        /// mirrored corner ornaments) — mirrors Ui/TitleScreen's fields of the same shape.</summary>
        public Image PanelImage { get; private set; }
        public Image RibbonImage { get; private set; }
        public Image[] CornerImages { get; private set; }

        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("ResultCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Wiring] ResultScreen: no MainCamera-tagged camera in scene; canvas will silently render as Overlay and be invisible to QA RenderTexture capture");
            }
            Canvas.worldCamera = mainCamera;
            Canvas.planeDistance = GameConfig.Ui.CanvasPlaneDistance;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);

            canvasGo.AddComponent<GraphicRaycaster>();

            UiFactory.CreateBackground(canvasGo.transform, ownerName: nameof(ResultScreen));
            BuildDecoration(canvasGo.transform);

            TitleText = UiFactory.CreateText(
                canvasGo.transform, "TitleText", GameConfig.Ui.ResultTitleText,
                GameConfig.Ui.ResultTitleFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultTitleAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultTitleSize, ownerName: nameof(ResultScreen));

            FinalScoreText = UiFactory.CreateText(
                canvasGo.transform, "FinalScoreText", $"{GameConfig.Ui.ResultFinalScoreLabel}: 0",
                GameConfig.Ui.ResultStatFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultFinalScoreAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultStatSize, ownerName: nameof(ResultScreen));

            SurvivalTimeText = UiFactory.CreateText(
                canvasGo.transform, "SurvivalTimeText", $"{GameConfig.Ui.ResultSurvivalTimeLabel}: 0.0 秒",
                GameConfig.Ui.ResultStatFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultSurvivalTimeAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultStatSize, ownerName: nameof(ResultScreen));

            WaveReachedText = UiFactory.CreateText(
                canvasGo.transform, "WaveReachedText", $"{GameConfig.Ui.ResultWaveReachedLabel}: 0",
                GameConfig.Ui.ResultStatFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultWaveReachedAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultStatSize, ownerName: nameof(ResultScreen));

            HighScoreNoticeText = UiFactory.CreateText(
                canvasGo.transform, "HighScoreNotice", GameConfig.Ui.ResultHighScoreUpdatedText,
                GameConfig.Ui.ResultHighScoreNoticeFontSize, GameConfig.Ui.ColorFocusHighlight,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultHighScoreNoticeAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultHighScoreNoticeSize, ownerName: nameof(ResultScreen));
            HighScoreNoticeObject = HighScoreNoticeText.gameObject;
            HighScoreNoticeObject.SetActive(false);

            RestartHintText = UiFactory.CreateText(
                canvasGo.transform, "RestartHintText", GameConfig.Ui.ResultRestartHintText,
                GameConfig.Ui.ResultHintFontSize, GameConfig.Ui.ColorTextSecondary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultRestartHintAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultHintSize, ownerName: nameof(ResultScreen));

            MenuHintText = UiFactory.CreateText(
                canvasGo.transform, "MenuHintText", GameConfig.Ui.ResultMenuHintText,
                GameConfig.Ui.ResultHintFontSize, GameConfig.Ui.ColorTextSecondary,
                anchor: new Vector2(0.5f, GameConfig.Ui.ResultMenuHintAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.ResultHintSize, ownerName: nameof(ResultScreen));
        }

        /// <summary>S-30: mirrors Ui/TitleScreen.BuildDecoration's panel/ribbon/corner treatment for the
        /// other "headline" screen — built right after UiFactory.CreateBackground so text draws on top.</summary>
        private void BuildDecoration(Transform parent)
        {
            PanelImage = UiFrameKitVisuals.CreateSlicedImage(
                parent, "Panel", _panelSprite,
                anchor: GameConfig.Ui.ResultPanelAnchor, anchoredPos: GameConfig.Ui.ResultPanelAnchoredPos,
                size: GameConfig.Ui.ResultPanelSize);

            RibbonImage = UiFrameKitVisuals.CreateSimpleImage(
                parent, "Ribbon", _ribbonSprite,
                anchor: GameConfig.Ui.ResultPanelAnchor, anchoredPos: GameConfig.Ui.ResultRibbonAnchoredPos,
                size: GameConfig.Ui.ResultRibbonSize);

            var cornerLeft = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerLeft", _cornerSprite,
                anchor: GameConfig.Ui.ResultPanelAnchor, anchoredPos: GameConfig.Ui.ResultCornerAnchoredPos,
                size: GameConfig.Ui.ResultCornerSize);

            Vector2 mirroredPos = new Vector2(-GameConfig.Ui.ResultCornerAnchoredPos.x, GameConfig.Ui.ResultCornerAnchoredPos.y);
            var cornerRight = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerRight", _cornerSprite,
                anchor: GameConfig.Ui.ResultPanelAnchor, anchoredPos: mirroredPos,
                size: GameConfig.Ui.ResultCornerSize);
            if (cornerRight != null)
            {
                cornerRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            CornerImages = new[] { cornerLeft, cornerRight };
        }

        /// <summary>Reflects the finished run's stats and whether it set a new high score (gdd Result
        /// 画面: 今回の最終スコア・生存時間・到達ウェーブ + ハイスコア更新の有無). highScoreUpdated toggles a
        /// dedicated notice on/off (mirrors Ui/TitleScreen.SetRecoveryNoticeVisible's show/hide pattern) —
        /// it is never shown when the run did not beat the prior high score.
        /// S-33: only the final score animates (count-up driven separately by Components/ResultController
        /// via SetFinalScoreValue), so this seeds it at 0 rather than the final value — survival
        /// time/wave-reached are not part of the count-up acceptance and stay immediate. The notice's
        /// scale is reset to 1 up front so a fresh Result load never inherits a mid-pulse scale from a
        /// stale instance.</summary>
        public void SetRunResult(RunResult run, bool highScoreUpdated)
        {
            FinalScoreText.text = $"{GameConfig.Ui.ResultFinalScoreLabel}: 0";
            SurvivalTimeText.text = $"{GameConfig.Ui.ResultSurvivalTimeLabel}: {run.SurvivalTimeSec:F1} 秒";
            WaveReachedText.text = $"{GameConfig.Ui.ResultWaveReachedLabel}: {run.WaveReached}";
            HighScoreNoticeObject.SetActive(highScoreUpdated);
            HighScoreNoticeText.transform.localScale = Vector3.one;
        }

        /// <summary>S-33: applies the current count-up frame's displayed value to FinalScoreText (driven
        /// by Components/ResultController + Systems/ResultCountUpSystem — this component holds no score
        /// state of its own beyond the last value it was told to show, per conventions.md's display-cache
        /// rule).</summary>
        public void SetFinalScoreValue(int displayedScore)
        {
            FinalScoreText.text = $"{GameConfig.Ui.ResultFinalScoreLabel}: {displayedScore}";
        }

        /// <summary>S-33: applies the current high-score notice pulse frame's uniform scale (driven by
        /// Components/ResultController + Systems/WavePulseSystem.ComputeScale, reused as-is from S-15).
        /// Unconditionally sets the scale (mirrors Ui/GameHud.SetWaveScale's unconditional-apply style) —
        /// unlike SetWaveScale's target, the notice GameObject may be inactive for a run that did not beat
        /// the high score, but that guard is the caller's responsibility: Components/ResultController only
        /// invokes this while its own _highScoreNoticePulseActive flag is true, which is only ever set when
        /// highScoreUpdated is true (i.e. exactly when the notice was activated in SetRunResult). This
        /// setter itself performs no activeSelf check.</summary>
        public void SetHighScoreNoticeScale(float scale)
        {
            HighScoreNoticeText.transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}
