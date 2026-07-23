// TitleScreen.cs — Title シーンの表示・入力配線（S-02。薄い Component。ロジックは持たない）。
// docs/architecture.md §2: Title は任意クリック/キー入力で Menu へ遷移し、recovered 時は復旧通知を表示する。
// シーン遷移は GameFlow（S-01）経由のみ（シーン名文字列直書き禁止）。
// Canvas は ScreenSpaceCamera 固定（tech-stack-unity.md 規約14）。全数値/色は GameConfig.Ui / GameConfig.Presentation。
using UnityEngine;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Input;

namespace ForgeGame.Ui
{
    /// <summary>
    /// Title シーンに1つだけ置く。UI 生成・入力購読・シーン遷移の配線のみを行う。
    /// 表示専任: recovered フラグは GameFlow.Recovered（プロセス内共有状態・S-01）から読むだけで複製しない。
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        private InputReader inputReader;
        private Text promptText;
        private Text recoveryNoticeText;
        private float promptBlinkTimer;
        private bool transitioning;

        /// <summary>テスト用の読み取り専用状態公開（内部 Text 参照そのものは渡さない — 表示専任の原則）。</summary>
        public bool RecoveryNoticeVisible => recoveryNoticeText != null && recoveryNoticeText.gameObject.activeSelf;

        /// <summary>
        /// 復旧通知 UI が構築済みかどうか（CR-CODE iter1 #7）。RecoveryNoticeVisible は
        /// recoveryNoticeText==null の場合も false を返すため、「UI 未構築」と「非表示」を区別できない。
        /// 負のテスト（NotRecovered_HidesRecoveryNotice）で UI 構築失敗を見逃さないための別プロパティ。
        /// </summary>
        public bool RecoveryNoticeTextExists => recoveryNoticeText != null;

        private void Awake()
        {
            inputReader = new InputReader();
            BuildUi();
        }

        private void OnEnable()
        {
            // Awake は OnEnable に必ず先行するため素の呼び出しにする（CR-CODE iter1 #6）。
            // null 条件演算子は「入力に一切反応しない Title」をログゼロで成立させてしまうため、
            // 不変条件が破れた場合は NRE で大きく失敗させる。
            inputReader.Enable();
        }

        private void OnDisable()
        {
            inputReader.Disable();
        }

        private void Start()
        {
            bool recovered = GameFlow.Recovered;
            recoveryNoticeText.gameObject.SetActive(recovered);
            if (recovered)
            {
                recoveryNoticeText.text = UiText.SaveCorruptionRecoveredMessage;
            }
        }

        private void Update()
        {
            if (transitioning) return;

            AnimatePrompt();

            // S-17 CR-CODE iter2 #1(blocker)/#3(major) fix: InputReader の AnyKey バインドは
            // `<Keyboard>/anyKey` で Escape も含む（Keyboard.anyKey は物理キーボードの押下有無のみを見る
            // 集約コントロールのため）。修正前は「クリック、または任意のキーで開始」の判定に Escape も
            // 含まれてしまい、Ui/PausePanel（S-17）の「Title 中の Esc は無反応」という acceptance と
            // 「PausePanel が存在しないシーンでは構造的に無反応」という設計コメント（PausePanel.cs 冒頭）を
            // 同時に破っていた。Esc（PausePressedThisFrame）が押されたフレームは AnyKey 側の判定から除外し、
            // Escape 単独押下では遷移しないようにする（クリックや Escape 以外のキーは従来どおり遷移する）。
            bool escapePressed = inputReader.PausePressedThisFrame;
            if (!escapePressed && (inputReader.ClickPressedThisFrame || inputReader.AnyKeyPressedThisFrame))
            {
                transitioning = true;
                GameFlow.GoToMenu();
            }
        }

        private void AnimatePrompt()
        {
            promptBlinkTimer += Time.deltaTime;
            float period = GameConfig.Presentation.TitlePromptBlinkPeriodSec;
            float phase = Mathf.PingPong(promptBlinkTimer, period) / period;
            Color c = promptText.color;
            c.a = Mathf.Lerp(GameConfig.Presentation.TitlePromptMinAlpha, GameConfig.Presentation.TitlePromptMaxAlpha, phase);
            promptText.color = c;
        }

        private void BuildUi()
        {
            // CR-CODE iter1 #5: Camera.main==null の明示エラーは UiCanvasHelper.ConfigureScreenSpaceCamera
            // 側に一元化済み（S-03 レーンの共通修正・全 UI シーン Canvas 呼び出しを一括カバー）。
            // ここで重複ガードを足すと同一条件で LogError が二重発生するため追加しない。
            var uiCamera = Camera.main;

            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);
            scaler.matchWidthOrHeight = GameConfig.Ui.CanvasScalerMatchWidthOrHeight;
            UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, uiCamera);

            CreateFullScreenPanel(canvasGo.transform, "Background", GameConfig.Ui.PanelBackground);

            CreateText(
                canvasGo.transform, "TitleText", "CRYSTAL BASTION",
                GameConfig.Ui.TitleFontSize, GameConfig.Ui.TextPrimary, new Vector2(0.5f, GameConfig.Ui.TitleTextAnchorY));

            recoveryNoticeText = CreateText(
                canvasGo.transform, "RecoveryNoticeText", string.Empty,
                GameConfig.Ui.SubtitleFontSize, GameConfig.Ui.RecoveryNoticeColor, new Vector2(0.5f, GameConfig.Ui.TitleRecoveryNoticeAnchorY));
            recoveryNoticeText.gameObject.SetActive(false);

            promptText = CreateText(
                canvasGo.transform, "PromptText", "クリック、または任意のキーで開始",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.AccentTeal, new Vector2(0.5f, GameConfig.Ui.TitlePromptAnchorY));
        }

        private static void CreateFullScreenPanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                GameConfig.Ui.ReferenceWidth * GameConfig.Ui.TextBoxWidthFraction,
                fontSize * GameConfig.Ui.TextLineHeightFactor);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
