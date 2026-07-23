// TowerSelectPanel.cs — タワー種別選択メニュー（2種。S-08）。ロジックは持たない表示+ヒットテスト専任。
// Components/BuildSpotController.TryPlaceTower（S-05）の設計コメント「左クリック→タワー種別選択メニュー
// からの確定操作は TryPlaceTower を呼ぶ想定（メニューの表示・開閉自体は S-08 の責務）」に対応する。
// 開閉・入力ルーティング（クリック/右クリック検出そのもの）は Ui/HudPanel が InputReader 経由で行い、
// このクラスは Open/Close/ヒットテストの API のみを提供する（表示専任: 資金はここに複製せず Open() の
// currentGold 引数として毎回受け取るだけ）。
// クリック判定は uGUI Button/EventSystem を使わず RectTransformUtility の矩形ヒットテストで行う
// （tech-stack-unity.md 規約4。TitleScreen/MenuScreen と同じ「非破壊入力」パターンを踏襲）。
// 全数値/色は GameConfig.Ui（マジックナンバー禁止 — 規約1）。
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    /// <summary>Game シーンの HudPanel が生成する子 GameObject に1つだけ付与する。</summary>
    public sealed class TowerSelectPanel : MonoBehaviour
    {
        private struct ButtonRefs
        {
            public RectTransform Rect;
            public Image Background;
            public Text Label;
        }

        private RectTransform panelRect;
        private ButtonRefs bastionButton;
        private ButtonRefs arcButton;
        private bool bastionAffordable;
        private bool arcAffordable;

        public bool IsOpen { get; private set; }

        /// <summary>Open() 時に指定されたビルドスポット index。閉じている間は -1。</summary>
        public int OpenSpotIndex { get; private set; } = -1;

        public RectTransform PanelRect => panelRect;
        public RectTransform BastionCannonButtonRect => bastionButton.Rect;
        public RectTransform ArcEmitterButtonRect => arcButton.Rect;
        public Text BastionCannonLabel => bastionButton.Label;
        public Text ArcEmitterLabel => arcButton.Label;
        public bool IsBastionCannonAffordable => bastionAffordable;
        public bool IsArcEmitterAffordable => arcAffordable;

        /// <summary>HudPanel.BuildUi() が AddComponent 直後に1回だけ呼ぶ。UI 生成のみ行い非表示にする。</summary>
        public void Initialize()
        {
            panelRect = (RectTransform)transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(GameConfig.Ui.TowerSelectPanelWidth, GameConfig.Ui.TowerSelectPanelHeight);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = GameConfig.Ui.PanelBackground;

            CreateHeaderText(transform, "タワーを選択", GameConfig.Ui.TowerSelectHeaderAnchorY);

            bastionButton = CreateButton(transform, "Button_BastionCannon", -GameConfig.Ui.TowerSelectButtonOffsetX);
            arcButton = CreateButton(transform, "Button_ArcEmitter", GameConfig.Ui.TowerSelectButtonOffsetX);

            gameObject.SetActive(false);
        }

        /// <summary>ビルドスポット左クリックで開く。資金不足の種別は非活性表示（不足表示）にする。</summary>
        public void Open(int spotIndex, int currentGold)
        {
            OpenSpotIndex = spotIndex;
            IsOpen = true;
            gameObject.SetActive(true);

            bastionAffordable = currentGold >= GameConfig.BastionCannon.Cost;
            arcAffordable = currentGold >= GameConfig.ArcEmitter.Cost;

            ApplyButtonState(bastionButton, "Bastion Cannon", GameConfig.BastionCannon.Cost, bastionAffordable);
            ApplyButtonState(arcButton, "Arc Emitter", GameConfig.ArcEmitter.Cost, arcAffordable);
        }

        /// <summary>パネル外クリック/右クリックで呼ぶ（選択解除）。</summary>
        public void Close()
        {
            IsOpen = false;
            OpenSpotIndex = -1;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// screenPos が選択可能（資金充足）なボタン上ならそのタワー種別を返す。資金不足のボタンは
        /// クリックを受け付けない（矩形内でも false を返す — acceptance「選択不可」）。
        /// </summary>
        public bool TryHandleClick(Vector2 screenPos, Camera cam, out TowerType type)
        {
            if (bastionAffordable && RectTransformUtility.RectangleContainsScreenPoint(bastionButton.Rect, screenPos, cam))
            {
                type = TowerType.BastionCannon;
                return true;
            }
            if (arcAffordable && RectTransformUtility.RectangleContainsScreenPoint(arcButton.Rect, screenPos, cam))
            {
                type = TowerType.ArcEmitter;
                return true;
            }
            type = default;
            return false;
        }

        public bool IsPointerInsidePanel(Vector2 screenPos, Camera cam) =>
            panelRect != null && RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, cam);

        private static void ApplyButtonState(ButtonRefs button, string name, int cost, bool affordable)
        {
            button.Label.text = affordable ? $"{name}\n{cost}G" : $"{name}\n{cost}G\n(資金不足)";
            Color bg = GameConfig.Ui.AccentTeal;
            bg.a = affordable ? 1f : GameConfig.Ui.TowerSelectInsufficientAlpha;
            button.Background.color = bg;
        }

        private static void CreateHeaderText(Transform parent, string content, float anchorY)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                GameConfig.Ui.TowerSelectPanelWidth * GameConfig.Ui.TowerSelectHeaderWidthFraction,
                GameConfig.Ui.BodyFontSize * GameConfig.Ui.TextLineHeightFactor);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = GameConfig.Ui.BodyFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GameConfig.Ui.AccentTeal;
            text.text = content;
        }

        private static ButtonRefs CreateButton(Transform parent, string name, float xOffset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, GameConfig.Ui.TowerSelectButtonAnchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);
            rect.sizeDelta = new Vector2(GameConfig.Ui.TowerSelectButtonWidth, GameConfig.Ui.TowerSelectButtonHeight);
            var background = go.GetComponent<Image>();
            background.color = GameConfig.Ui.AccentTeal;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = GameConfig.Ui.BodyFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GameConfig.Ui.PanelBackground;

            return new ButtonRefs { Rect = rect, Background = background, Label = text };
        }
    }
}
