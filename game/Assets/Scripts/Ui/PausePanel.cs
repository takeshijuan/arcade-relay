// PausePanel.cs — Game シーンの一時停止オーバーレイ（S-17。薄い Component。ロジックは持たない）。
// gdd「操作仕様」Esc行 / 「ゲームフロー」節: Esc は Game シーン中のみ一時停止オーバーレイ（再開/設定/
// タイトルへ戻る）を開閉する（シーン遷移は伴わない一時停止 UI）。本 Component は Game シーンにのみ
// 配置するため、Title/Menu/Result 中の Esc 無反応は「そもそも本コンポーネントが存在しない」ことで
// 構造的に保証される（各シーンの Update() を個別に変更する必要はない）。
// ※ S-17 CR-CODE iter2 #1/#3 で判明した例外: この保証は「PausePanel 自身が Esc に反応しない」ことしか
// 保証しない。Title シーンの Ui/TitleScreen は独自に InputReader.AnyKeyPressedThisFrame（Escape を含む
// `<Keyboard>/anyKey`）を購読しており、Escape を明示的に除外する修正を TitleScreen.Update() 側に別途
// 入れてある（TitleScreen.cs 参照）。他シーンに Esc を購読する Component を新設する場合も同様の考慮が要る。
//
// 開いている間は Time.timeScale = GameConfig.Presentation.PausedTimeScale(=0) にする。
// docs/architecture.md §5 / conventions.md 6節: Systems/Components 層は全て Time.deltaTime 駆動のため、
// timeScale=0 でタワー攻撃・敵移動・タイマーは自動的に停止する——本 Component は timeScale の切替と
// UI 表示/入力のみを担う（表示専任の原則。ゲームロジックを複製・停止処理を自前で書かない）。
//
// timeScale はシーン間で持続するグローバル値のため、「タイトルへ戻る」遷移前・本 Component の
// OnDisable（想定外の破棄経路の保険）の両方で明示的に NormalTimeScale へ戻す。
//
// 「設定」はオーバーレイ内のサブビューとして BGM/SFX 音量スライダー + 操作説明を表示する
// （gdd「ゲームフロー」節 Esc 行・Menu の設定要素と同じ音量調整体験を一時停止中にも提供する設計判断——
// design/gdd.md には設定の中身の明記が無いため、Menu の設定要素と同一の操作性で解釈する）。
// 実効反映・永続化は Ui/MenuScreen（S-16）と同一契約（Persistence/IAudioSettingsStore 経由・
// GameFlow.CurrentAudioSettings のプロセス内キャリー）を踏襲するが、実 BGM/SFX クリップの再生配線
// （S-19）とは独立に、このパネル自身の AudioSource.volume で即時反映を確認できるようにする
// （MenuScreen.cs 該当コメント参照）。
//
// クリック判定は uGUI Button/EventSystem を使わず、InputReader のポインタ座標 + RectTransformUtility の
// 矩形ヒットテストで行う（tech-stack-unity.md 規約4。TitleScreen/MenuScreen/HudPanel と同じ
// 「非破壊入力」パターンを踏襲）。Canvas は ScreenSpaceCamera 固定（規約14）。全数値/色は GameConfig.Ui。
using System;
using UnityEngine;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Input;
using ForgeGame.Persistence;

namespace ForgeGame.Ui
{
    /// <summary>Game シーンに1つだけ置く。Esc トグル・timeScale 切替・UI 生成/入力配線のみを行う。</summary>
    public sealed class PausePanel : MonoBehaviour
    {
        private enum ViewState { Main, Settings }

        private InputReader inputReader;
        private Camera uiCamera;

        private GameObject overlayRoot;
        private GameObject mainView;
        private GameObject settingsView;
        private ViewState currentView = ViewState.Main;

        private RectTransform resumeButtonRect;
        private RectTransform settingsButtonRect;
        private RectTransform titleButtonRect;
        private RectTransform backButtonRect;

        private Slider bgmVolumeSlider;
        private Slider sfxVolumeSlider;
        private Slider draggingSlider;
        private AudioSource bgmAudioSource;
        private AudioSource sfxAudioSource;

        // S-16 と同じ「最初の変更操作まで遅延解決」パターン（Awake 後のテスト注入でも無警告で無視されない）。
        private IAudioSettingsStore audioSettingsStore;

        /// <summary>一時停止オーバーレイが開いているか（テスト/HudPanel の入力抑制判定に使う）。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 直近 Resume() が呼ばれた Time.frameCount（S-17 CR-CODE iter1 #1/#2 fix）。PausePanel と HudPanel
        /// はどちらも DefaultExecutionOrder 未指定で Update() 実行順が不定なため、IsOpen 単体のガードだと
        /// 「PausePanel.Update() が先に走って Resume() 済み（IsOpen==false）になった後で同一フレームの
        /// HudPanel.Update() が同じクリックを再処理してしまう」抜け道が残る。HudPanel はこのフレーム番号も
        /// 合わせて見ることで、Update 実行順に関係なく Resume を発生させたクリックの二重処理を防ぐ。
        /// </summary>
        public int LastResumeFrame { get; private set; } = -1;

        public bool IsSettingsViewOpen => IsOpen && currentView == ViewState.Settings;

        /// <summary>テスト用の読み取り専用状態公開（表示専任の原則: 内部状態そのものは複製しない）。</summary>
        public RectTransform ResumeButtonRect => resumeButtonRect;
        public RectTransform SettingsButtonRect => settingsButtonRect;
        public RectTransform TitleButtonRect => titleButtonRect;
        public RectTransform BackButtonRect => backButtonRect;
        public Slider BgmVolumeSlider => bgmVolumeSlider;
        public Slider SfxVolumeSlider => sfxVolumeSlider;
        public AudioSource BgmAudioSource => bgmAudioSource;
        public AudioSource SfxAudioSource => sfxAudioSource;
        public Camera UiCamera => uiCamera;

        /// <summary>
        /// テスト用の IAudioSettingsStore 注入。MenuScreen.SetAudioSettingsStoreForTest と同じ
        /// 「最初の音量変更操作まで遅延解決」パターンのため Awake 後（シーンロード後）でも安全に呼べる。
        /// </summary>
        public void SetAudioSettingsStoreForTest(IAudioSettingsStore store) => audioSettingsStore = store ??
            throw new ArgumentNullException(nameof(store), "[PausePanel] SetAudioSettingsStoreForTest(null) would " +
                "silently fall back to the default FileAudioSettingsStore (AudioSettingsStores.CreateDefault()) " +
                "on first slider change; pass a real stub.");

        private void Awake()
        {
            inputReader = new InputReader();
            uiCamera = Camera.main;
            BuildUi();
        }

        private void OnEnable() => inputReader?.Enable();

        private void OnDisable()
        {
            inputReader?.Disable();
            // 想定外の破棄/シーン離脱経路で timeScale=0 のまま取り残さない保険（GoToTitle は遷移前に
            // 既に明示的に戻しているため通常経路では冗長だが、二重化しても害はない）。
            if (IsOpen)
            {
                Time.timeScale = GameConfig.Presentation.NormalTimeScale;
            }
        }

        private void Update()
        {
            // Update() 自体は Time.timeScale の影響を受けず毎フレーム呼ばれる（timeScale が変えるのは
            // Time.deltaTime のスケールのみ）。Input System のボタン判定も timeScale と無関係なため、
            // 一時停止中でも Esc トグル自体は正しく検知できる。
            if (inputReader.PausePressedThisFrame)
            {
                if (IsOpen) Resume(); else Open();
                return;
            }

            if (!IsOpen) return;
            HandleInput();
        }

        private void Open()
        {
            IsOpen = true;
            ShowView(ViewState.Main);
            overlayRoot.SetActive(true);
            Time.timeScale = GameConfig.Presentation.PausedTimeScale;
        }

        /// <summary>「再開」。オーバーレイを閉じ通常速度へ戻す（HudPanel/PausePanel 双方から呼べる公開API）。</summary>
        public void Resume()
        {
            IsOpen = false;
            LastResumeFrame = Time.frameCount; // S-17 CR-CODE iter1 #1/#2 fix（上記 LastResumeFrame コメント参照）
            overlayRoot.SetActive(false);
            Time.timeScale = GameConfig.Presentation.NormalTimeScale;
        }

        /// <summary>「タイトルへ戻る」。timeScale はシーン間で持続するグローバル値のため遷移前に必ず戻す。</summary>
        private void GoToTitle()
        {
            IsOpen = false;
            // S-17 CR-CODE iter2 #2/#4(minor) fix: Resume() と同じ同一フレーム二重処理 race
            // （LastResumeFrame コメント参照）が GoToTitle にも及ぶため、Resume() と同じく
            // LastResumeFrame を更新してから閉じる（実害は遷移でシーンごと破棄されるため無いが、
            // 対処漏れを塞いで Resume/GoToTitle の「開いた状態を終える」経路を揃える）。
            LastResumeFrame = Time.frameCount;
            Time.timeScale = GameConfig.Presentation.NormalTimeScale;
            GameFlow.GoToTitle();
        }

        private void HandleInput()
        {
            if (currentView == ViewState.Settings)
            {
                HandleVolumeSliderPointerInput();
            }

            if (!inputReader.ClickPressedThisFrame) return;
            Vector2 pointer = inputReader.PointerScreenPosition;

            if (currentView == ViewState.Main)
            {
                if (IsPointerOverRect(resumeButtonRect, pointer)) { Resume(); return; }
                if (IsPointerOverRect(settingsButtonRect, pointer)) { ShowView(ViewState.Settings); return; }
                if (IsPointerOverRect(titleButtonRect, pointer)) { GoToTitle(); return; }
            }
            else
            {
                if (IsPointerOverRect(backButtonRect, pointer)) { ShowView(ViewState.Main); return; }
            }
        }

        /// <summary>MenuScreen.HandleVolumeSliderPointerInput と同一パターン（CR-CODE iter2 実測済みの
        /// EventSystem 非依存 drag 方式）。押下開始フレームでスライダー矩形上にポインタがあれば以後の
        /// 押下継続中そのスライダーを握り続ける。</summary>
        private void HandleVolumeSliderPointerInput()
        {
            if (!inputReader.ClickHeld)
            {
                draggingSlider = null;
                return;
            }

            Vector2 pointer = inputReader.PointerScreenPosition;

            if (draggingSlider == null)
            {
                if (!inputReader.ClickPressedThisFrame) return;
                if (IsPointerOverRect((RectTransform)bgmVolumeSlider.transform, pointer)) draggingSlider = bgmVolumeSlider;
                else if (IsPointerOverRect((RectTransform)sfxVolumeSlider.transform, pointer)) draggingSlider = sfxVolumeSlider;
                else return;
            }

            ApplyPointerToSlider(draggingSlider, pointer);
        }

        private void ApplyPointerToSlider(Slider slider, Vector2 screenPos)
        {
            var rect = (RectTransform)slider.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCamera, out Vector2 local)) return;

            float ratio = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x);
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(ratio));
        }

        private bool IsPointerOverRect(RectTransform rect, Vector2 screenPos) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, uiCamera);

        private void ShowView(ViewState view)
        {
            currentView = view;
            mainView.SetActive(view == ViewState.Main);
            settingsView.SetActive(view == ViewState.Settings);
        }

        /// <summary>音量スライダー変更ハンドラ（MenuScreen.OnBgmVolumeChanged と同一契約）。
        /// 即時反映してから永続化する（一瞥可読・即時フィードバックの原則）。</summary>
        private void OnBgmVolumeChanged(float value)
        {
            bgmAudioSource.volume = value;
            PersistAudioSettings();
        }

        private void OnSfxVolumeChanged(float value)
        {
            sfxAudioSource.volume = value;
            PersistAudioSettings();
        }

        /// <summary>MenuScreen.PersistAudioSettings と同一契約（このストア種別は contract §6 の3点セット
        /// 対象外——Persistence/IAudioSettingsStore.cs 冒頭コメント参照。保存失敗は1回ログするのみ）。</summary>
        private void PersistAudioSettings()
        {
            var data = new AudioSettingsData { BgmVolume = bgmAudioSource.volume, SfxVolume = sfxAudioSource.volume };
            GameFlow.SetCurrentAudioSettings(data);
            audioSettingsStore ??= AudioSettingsStores.CreateDefault();
            try
            {
                audioSettingsStore.Save(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveFailure] PausePanel failed to persist AudioSettingsData after volume change: {ex}");
            }
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);
            scaler.matchWidthOrHeight = GameConfig.Ui.CanvasScalerMatchWidthOrHeight;
            UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, uiCamera);
            // S-17 CR-CODE iter1 #4: HudCanvas と同一条件（ScreenSpaceCamera・同一カメラ・planeDistance=1）
            // のため描画順を明示しないと未規定になる。一時停止オーバーレイは常に最前面に描画する。
            canvas.sortingOrder = GameConfig.Ui.PauseCanvasSortingOrder;

            overlayRoot = new GameObject("Overlay", typeof(RectTransform));
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            StretchFullScreen((RectTransform)overlayRoot.transform);

            CreateFullScreenPanel(overlayRoot.transform, "Background",
                WithAlpha(GameConfig.Ui.PanelBackground, GameConfig.Ui.PauseOverlayBackgroundAlpha));

            BuildMainView();
            BuildSettingsView();

            overlayRoot.SetActive(false); // 開始時は非表示（Esc で開くまで一時停止しない）
        }

        private void BuildMainView()
        {
            mainView = new GameObject("MainView", typeof(RectTransform));
            mainView.transform.SetParent(overlayRoot.transform, false);
            StretchFullScreen((RectTransform)mainView.transform);

            int row = 0;
            float NextY() => GameConfig.Ui.PauseTopRowAnchorY - (row++ * GameConfig.Ui.PauseRowStepY);

            CreateText(mainView.transform, "PauseHeader", "一時停止",
                GameConfig.Ui.SubtitleFontSize, GameConfig.Ui.AccentTeal, GameConfig.Ui.PauseHeaderAnchorY);

            resumeButtonRect = CreateButton(mainView.transform, "ResumeButton", "再開", NextY());
            settingsButtonRect = CreateButton(mainView.transform, "SettingsButton", "設定", NextY());
            titleButtonRect = CreateButton(mainView.transform, "TitleButton", "タイトルへ戻る", NextY());
        }

        private void BuildSettingsView()
        {
            settingsView = new GameObject("SettingsView", typeof(RectTransform));
            settingsView.transform.SetParent(overlayRoot.transform, false);
            StretchFullScreen((RectTransform)settingsView.transform);
            settingsView.SetActive(false);

            int row = 0;
            float NextY() => GameConfig.Ui.PauseTopRowAnchorY - (row++ * GameConfig.Ui.PauseRowStepY);

            CreateText(settingsView.transform, "SettingsHeader", "設定",
                GameConfig.Ui.SubtitleFontSize, GameConfig.Ui.AccentTeal, GameConfig.Ui.PauseHeaderAnchorY);

            // MenuScreen と同じ「初期値は GameFlow.CurrentAudioSettings、無ければ既定値」契約（表示専任の原則）。
            AudioSettingsData audioSettings = GameFlow.CurrentAudioSettings ?? AudioSettingsData.CreateDefault();

            bgmAudioSource = gameObject.AddComponent<AudioSource>();
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true;
            bgmAudioSource.volume = audioSettings.BgmVolume;

            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.loop = false;
            sfxAudioSource.volume = audioSettings.SfxVolume;

            bgmVolumeSlider = CreateVolumeSlider(settingsView.transform, "BgmVolumeSlider", "BGM音量", NextY(),
                audioSettings.BgmVolume, OnBgmVolumeChanged);
            sfxVolumeSlider = CreateVolumeSlider(settingsView.transform, "SfxVolumeSlider", "SFX音量", NextY(),
                audioSettings.SfxVolume, OnSfxVolumeChanged);

            CreateText(settingsView.transform, "OperationInstructionsText",
                "操作: 左クリック=設置/選択/購入　右クリック=選択解除　Esc=一時停止",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            backButtonRect = CreateButton(settingsView.transform, "BackButton", "戻る", NextY());
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static void CreateFullScreenPanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFullScreen((RectTransform)go.transform);
            go.GetComponent<Image>().color = color;
        }

        private static void StretchFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
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

        private static RectTransform CreateButton(Transform parent, string name, string label, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(GameConfig.Ui.PauseButtonWidth, GameConfig.Ui.PauseButtonHeight);
            go.GetComponent<Image>().color = GameConfig.Ui.AccentTeal;

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
            text.text = label;

            return rect;
        }

        private static Slider CreateVolumeSlider(Transform parent, string name, string label, float anchorY,
            float initialValue, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(GameConfig.Ui.MenuSliderWidth, GameConfig.Ui.MenuSliderHeight);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = GameConfig.Ui.PanelBackground;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = (RectTransform)fillAreaGo.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = GameConfig.Ui.AccentTeal;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue; // リスナー登録前に設定（onValueChanged 未登録のため発火しない）
            slider.onValueChanged.AddListener(onValueChanged);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = new Vector2(GameConfig.Ui.MenuSliderLabelOffsetX, 0f);
            labelRect.sizeDelta = new Vector2(GameConfig.Ui.MenuSliderLabelWidth, GameConfig.Ui.MenuSliderHeight * GameConfig.Ui.TextLineHeightFactor);
            var labelText = labelGo.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = GameConfig.Ui.BodyFontSize;
            labelText.alignment = TextAnchor.MiddleRight;
            labelText.color = GameConfig.Ui.TextPrimary;
            labelText.text = label;

            return slider;
        }
    }
}
