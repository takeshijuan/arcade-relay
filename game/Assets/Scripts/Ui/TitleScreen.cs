// TitleScreen — Title シーンの表示専任コンポーネント (ui-engineer, S-02). Builds the ScreenSpaceCamera
// Canvas + Text UI entirely in code at Awake (engine=unity 方針: uGUI をコード中心に構築).
// All sizes/colors/text content come from GameConfig.Ui (no magic numbers/hardcoded strings here).
// Display-only: the recovery flag it renders is handed in by Components/TitleController, which
// reads the authoritative Components/SessionHolder.Recovered value — this component never reads
// game state itself and holds no duplicate copy of it beyond the last bool passed to
// SetRecoveryNoticeVisible (display cache, not a second source of truth).
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    public sealed class TitleScreen : MonoBehaviour
    {
        /// <summary>S-30: baked by Editor/SceneWiring.WireTitleUiFrameKit (SerializedObject assignment,
        /// same pattern as Ui/MenuScreen._crystalIconSprite for IMG-03). A null value degrades gracefully —
        /// Ui/UiFrameKitVisuals.CreateSlicedImage/CreateSimpleImage skip creating the Image rather than
        /// erroring, exactly like MenuScreen's crystal icon does for a not-yet-generated sprite.</summary>
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _ribbonSprite;
        [SerializeField] private Sprite _cornerSprite;

        public Canvas Canvas { get; private set; }
        public Text TitleText { get; private set; }
        public Text HintText { get; private set; }
        public GameObject RecoveryNoticeObject { get; private set; }
        public Text RecoveryNoticeText { get; private set; }

        /// <summary>S-30: decorative Images built from IMG-05 (panel background + heading ribbon + two
        /// mirrored corner ornaments). Any entry may be null if the corresponding sprite wasn't baked this
        /// session (see field docs above) — PlayMode tests should only assert non-null once the sprites are
        /// actually wired, not assume they always exist.</summary>
        public Image PanelImage { get; private set; }
        public Image RibbonImage { get; private set; }
        public Image[] CornerImages { get; private set; }

        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("TitleCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Wiring] TitleScreen: no MainCamera-tagged camera in scene; canvas will silently render as Overlay and be invisible to QA RenderTexture capture");
            }
            Canvas.worldCamera = mainCamera;
            Canvas.planeDistance = GameConfig.Ui.CanvasPlaneDistance;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);

            canvasGo.AddComponent<GraphicRaycaster>();

            UiFactory.CreateBackground(canvasGo.transform, ownerName: nameof(TitleScreen));
            BuildDecoration(canvasGo.transform);

            TitleText = UiFactory.CreateText(
                canvasGo.transform, "TitleText", GameConfig.Ui.GameTitleText,
                GameConfig.Ui.TitleFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.TitleTextAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.TitleTextSize, ownerName: nameof(TitleScreen));

            HintText = UiFactory.CreateText(
                canvasGo.transform, "HintText", GameConfig.Ui.TitleHintText,
                GameConfig.Ui.TitleHintFontSize, GameConfig.Ui.ColorTextSecondary,
                anchor: new Vector2(0.5f, GameConfig.Ui.TitleHintAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.TitleHintSize, ownerName: nameof(TitleScreen));

            RecoveryNoticeText = UiFactory.CreateText(
                canvasGo.transform, "RecoveryNotice", GameConfig.Ui.TitleRecoveryNoticeText,
                GameConfig.Ui.TitleNoticeFontSize, GameConfig.Ui.ColorAlert,
                anchor: new Vector2(0.5f, GameConfig.Ui.TitleNoticeAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.TitleNoticeSize, ownerName: nameof(TitleScreen));
            RecoveryNoticeObject = RecoveryNoticeText.gameObject;
            RecoveryNoticeObject.SetActive(false);
        }

        /// <summary>S-30: panel background behind Title+Hint, a heading ribbon above it, and two mirrored
        /// corner ornaments — built before any text so text draws on top in sibling order (this method is
        /// called right after UiFactory.CreateBackground, before TitleText/HintText/RecoveryNoticeText below).</summary>
        private void BuildDecoration(Transform parent)
        {
            PanelImage = UiFrameKitVisuals.CreateSlicedImage(
                parent, "Panel", _panelSprite,
                anchor: GameConfig.Ui.TitlePanelAnchor, anchoredPos: GameConfig.Ui.TitlePanelAnchoredPos,
                size: GameConfig.Ui.TitlePanelSize);

            RibbonImage = UiFrameKitVisuals.CreateSimpleImage(
                parent, "Ribbon", _ribbonSprite,
                anchor: GameConfig.Ui.TitlePanelAnchor, anchoredPos: GameConfig.Ui.TitleRibbonAnchoredPos,
                size: GameConfig.Ui.TitleRibbonSize);

            var cornerLeft = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerLeft", _cornerSprite,
                anchor: GameConfig.Ui.TitlePanelAnchor, anchoredPos: GameConfig.Ui.TitleCornerAnchoredPos,
                size: GameConfig.Ui.TitleCornerSize);

            Vector2 mirroredPos = new Vector2(-GameConfig.Ui.TitleCornerAnchoredPos.x, GameConfig.Ui.TitleCornerAnchoredPos.y);
            var cornerRight = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerRight", _cornerSprite,
                anchor: GameConfig.Ui.TitlePanelAnchor, anchoredPos: mirroredPos,
                size: GameConfig.Ui.TitleCornerSize);
            if (cornerRight != null)
            {
                cornerRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // mirror the single flourish asset for the opposite corner
            }
            CornerImages = new[] { cornerLeft, cornerRight };
        }

        /// <summary>Toggle the save-corruption recovery notice (Boot→Title 破損復旧プロトコル).</summary>
        public void SetRecoveryNoticeVisible(bool visible)
        {
            if (RecoveryNoticeObject == null)
            {
                Debug.LogError("[Wiring] TitleScreen.SetRecoveryNoticeVisible called before Awake built the canvas");
                return;
            }
            RecoveryNoticeObject.SetActive(visible);
        }
    }
}
