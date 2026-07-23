// MenuScreen.cs — Menu シーンの表示・入力配線（S-03/S-15。薄い Component。ロジックは持たない）。
// docs/architecture.md §2 / gdd「Menu 必須要素チェック」: Menu は contract §11 の必須4要素を持つ
// アウトゲームハブ。(1) プレイ開始→Game (2) アウトゲーム表示（実績/統計/essence+UPG Lv）
// (3) 設定（音量スライダー+操作説明） (4) タイトルへ戻る→Title。
// 表示専任: essence/実績/統計/UPG Lv は GameFlow.CurrentSaveData（Boot がロードした SaveData の
// プロセス内キャリー）から読むだけで複製しない（UI に状態を持たせない — 役割宣言の鉄則）。
// クリック判定は uGUI Button/EventSystem を使わず、InputReader のポインタ座標 + RectTransformUtility の
// 矩形ヒットテストで行う（tech-stack-unity.md 規約4: 入力は InputReader に集約。EventSystem 未導入の
// プロジェクト規約に合わせ、TitleScreen と同じ「非破壊入力」パターンを踏襲）。
// Canvas は ScreenSpaceCamera 固定（tech-stack-unity.md 規約14）。全数値/色は GameConfig.Ui。
//
// S-15 追記: アウトゲーム表示に ACH-03/04 の現在値/目標値進捗バーと、UPG-01〜03 の essence 購入UIを追加。
// 購入トランザクション（essence消費+Lv加算）の正本は本来 Systems/Meta の純粋 reducer に置くのが
// アーキテクチャ上の理想形だが、購入ロジックの正本ストーリー S-14（gameplay-engineer・build phase）は
// 本コミット時点で未着手のため、Systems/ 層を編集しない（ui-engineer のレーン境界）範囲で本ストーリーの
// acceptance を単独で満たすよう、Ui 層内に閉じた最小実装（TryPurchaseUpgrade）を置く。SaveData.Clone() の
// ような既存公開APIの利用のみで Systems/Meta のファイル自体には触れていない。S-14 着手時に Systems/Meta
// 側へ正本reducerが追加された場合は、本ファイルの TryPurchaseUpgrade をその呼び出しへ置き換えること。
//
// CR-CODE iter1 #1/#3（major/minor・techdebt追跡）: 上記の暫定配置は review-loops.md の
// 「正当理由の明記」経路で CONCERNS 止まり（blocker ではない）と判定済み。stories.yaml S-14 側へ
// 「MenuScreen.TryPurchaseUpgrade を正本 reducer 呼び出しへ置き換える」逆リンクを追加することが推奨
// されているが、S-14 は ui-engineer のレーン境界外（gameplay-engineer 担当ブロック）のため本 iteration
// では stories.yaml を編集しない。state/reviews/s-15.md に技術的負債として明記し、Checkpoint 提示物への
// 蓄積は workflow 側の責務とする。
//
// S-16 追記: 設定パネルの音量スライダーを実際の AudioSource.volume へ実効反映し、
// Persistence/IAudioSettingsStore 経由で保存・復元する（GameFlow.CurrentAudioSettings がプロセス内キャリー。
// GameBootstrap が Boot 起動時にロードして設定する）。実クリップ割当・BGMループ再生配線は S-19 の責務。
//
// S-20 追記: recovered=true（Boot でのセーブ破損復旧）のとき、Title（S-02）と同一文言・スタイルの
// セーブ復旧トーストを Menu にも表示する（Title/Menu の両方で「一度表示」— GameFlow.Recovered は
// Boot ロード時に一度だけ設定される静的フラグで、複製・二重計算せずそのまま読むだけ。Title と同じ
// パターンで常に行スペースを確保し非表示時は空テキストのまま隠す）。ベストクリアタイム未記録（-1）の
// 「--」表示は既存の HighScoreText 行（S-15 実装）が既に満たしているため変更不要。
//
// CR-CODE iter2 #1（major）対応: 本プロジェクトは EventSystem 未導入のため uGUI Slider は素の状態では
// ポインタイベントを一切受け取れず、実プレイヤーがスライダーを操作する手段が無かった（デッドUI）。
// EventSystem を導入せず、既存の「InputReader + RectTransformUtility 矩形ヒットテスト」パターン
// （TitleScreen/MenuScreen 既存ボタンと同型）をスライダーへ拡張する形で解決する:
// InputReader.ClickHeld（押下継続。新規追加 — WasPressedThisFrame とは別の継続検知）でスライダー矩形上の
// クリック/ドラッグを検知し、ポインタのローカルX位置から slider.value を直接設定する
// （HandleVolumeSliderPointerInput/ApplyPointerToSlider 参照）。
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Input;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Ui
{
    /// <summary>
    /// Menu シーンに1つだけ置く。UI 生成・入力購読・シーン遷移の配線のみを行う。
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        // ACH-01〜05（gdd「実績（採用時のみ）」節の固有名詞・順序に一致）。
        private static readonly (string Id, string Label)[] AchievementDefs =
        {
            ("ACH-01", "初勝利"),
            ("ACH-02", "完全防衛"),
            ("ACH-03", "累計撃破"),
            ("ACH-04", "範囲特化"),
            ("ACH-05", "倹約防衛"),
        };

        // UPG-01〜03（gdd「持ち越しアップグレード」節に一致）。
        private static readonly (string Id, string Label)[] UpgradeDefs =
        {
            ("UPG-01", "初期資金"),
            ("UPG-02", "コスト割引"),
            ("UPG-03", "essence獲得率"),
        };

        private enum UpgradeKind { Upg01 = 0, Upg02 = 1, Upg03 = 2 }

        private InputReader inputReader;
        private Camera uiCamera;
        private bool transitioning;

        // S-16 CR-CODE iter2 #1: 現在ポインタ入力で操作中のスライダー（無ければ null）。
        // 矩形内で押下が始まったフレームにのみ握り、ClickHeld が続く間は矩形外に出ても離すまで握り続ける
        // （一般的なスライダー drag の UX。押下解除で自動的に null へ戻る）。
        private Slider draggingSlider;

        // S-15: 購入永続化用ストア。GameBootstrap/RunOutcomeController の SetSaveStoreForTest とは異なり、
        // 本クラスは Awake() 時点で即座に解決せず「最初の購入操作」まで遅延解決するため、Awake 後
        // （シーンロード後）に呼んでも無警告で無視されることはない（購入が発生しない限り消費されない）。
        private ISaveStore saveStore;

        // S-16: 音量設定の永続化ストア。saveStore と同じ「最初の変更操作まで遅延解決」パターン
        // （Awake 後にテスト注入しても無警告で無視されない）。
        private IAudioSettingsStore audioSettingsStore;

        // S-16: スライダー→実効音量の反映先。クリップ割当・実再生配線は S-19（資産統合）の責務。
        // ここでは「スライダー→実 AudioSource.volume」の実効反映と Persistence 経由の保存/復元のみを担う。
        private AudioSource bgmAudioSource;
        private AudioSource sfxAudioSource;

        private RectTransform playButtonRect;
        private RectTransform backToTitleButtonRect;
        private Transform outgamePanel;
        private Transform settingsPanel;
        private Text[] achievementTexts;
        private Slider bgmVolumeSlider;
        private Slider sfxVolumeSlider;
        private Text recoveryNoticeText; // S-20: セーブ復旧トースト（recovered=true 時のみ表示）
        private Text saveFailedNoticeText; // CR-CODE iter1 #1(major)対応: GameFlow.SaveFailed の通知表示

        private Text essenceUpgText;
        private RectTransform[] upgradeBuyButtonRects;
        private Text[] upgradeLevelTexts;
        private Text[] upgradeButtonLabelTexts;
        private Image[] upgradeButtonImages;

        /// <summary>テスト用の読み取り専用状態公開（表示専任の原則: 内部状態そのものは複製しない）。</summary>
        public RectTransform PlayButtonRect => playButtonRect;
        public RectTransform BackToTitleButtonRect => backToTitleButtonRect;
        public Transform OutgamePanel => outgamePanel;
        public Transform SettingsPanel => settingsPanel;
        public Slider BgmVolumeSlider => bgmVolumeSlider;
        public Slider SfxVolumeSlider => sfxVolumeSlider;
        public AudioSource BgmAudioSource => bgmAudioSource;
        public AudioSource SfxAudioSource => sfxAudioSource;
        public int AchievementCount => achievementTexts?.Length ?? 0;
        public Camera UiCamera => uiCamera;

        /// <summary>
        /// テスト用の読み取り専用状態公開（S-20。TitleScreen.RecoveryNoticeVisible と同型）。
        /// recoveryNoticeText==null（UI 未構築）の場合も false を返すため、「UI 未構築」と「意図的な非表示」を
        /// 区別する別プロパティ RecoveryNoticeTextExists と併用する（TitleScreen CR-CODE iter1 #7 と同じ理由）。
        /// </summary>
        public bool RecoveryNoticeVisible => recoveryNoticeText != null && recoveryNoticeText.gameObject.activeSelf;
        public bool RecoveryNoticeTextExists => recoveryNoticeText != null;

        /// <summary>
        /// テスト用の読み取り専用状態公開（CR-CODE iter1 #1。RecoveryNoticeVisible と同型のパターン —
        /// UI未構築/意図的非表示の区別を分けるため Exists を併用する）。
        /// </summary>
        public bool SaveFailedNoticeVisible => saveFailedNoticeText != null && saveFailedNoticeText.gameObject.activeSelf;
        public bool SaveFailedNoticeTextExists => saveFailedNoticeText != null;

        /// <summary>
        /// テスト用の ISaveStore 注入（S-15）。購入操作まで遅延解決するため Awake 後でも安全に呼べる
        /// （GameBootstrap.SetSaveStoreForTest 等の「Start 前限定」契約とは異なる。本クラスは Start を
        /// 持たず、購入が発生しない限りストアを一切参照しない）。
        /// </summary>
        public void SetSaveStoreForTest(ISaveStore store) => saveStore = store ??
            throw new ArgumentNullException(nameof(store), "[MenuScreen] SetSaveStoreForTest(null) would silently " +
                "fall back to the default FileSaveStore (SaveStores.CreateDefault()) on first purchase; pass a real stub.");

        /// <summary>
        /// テスト用の IAudioSettingsStore 注入（S-16）。saveStore と同じ遅延解決パターンのため
        /// Awake 後（シーンロード後）でも安全に呼べる（スライダー操作まで一切参照しない）。
        /// </summary>
        public void SetAudioSettingsStoreForTest(IAudioSettingsStore store) => audioSettingsStore = store ??
            throw new ArgumentNullException(nameof(store), "[MenuScreen] SetAudioSettingsStoreForTest(null) would " +
                "silently fall back to the default FileAudioSettingsStore (AudioSettingsStores.CreateDefault()) " +
                "on first slider change; pass a real stub.");

        private void Awake()
        {
            inputReader = new InputReader();
            uiCamera = Camera.main;
            BuildUi();
        }

        private void OnEnable()
        {
            inputReader?.Enable();
        }

        private void OnDisable()
        {
            inputReader?.Disable();
        }

        private void Update()
        {
            if (transitioning) return;

            // S-16 CR-CODE iter2 #1: 音量スライダーの実プレイヤー入力（クリック/ドラッグ）を毎フレーム処理する。
            // ClickHeld は WasPressedThisFrame と独立のため、下の早期 return（ClickPressedThisFrame 専用）より
            // 前に呼ぶ必要がある。
            HandleVolumeSliderPointerInput();

            if (!inputReader.ClickPressedThisFrame) return;

            Vector2 pointer = inputReader.PointerScreenPosition;
            if (IsPointerOverRect(playButtonRect, pointer))
            {
                transitioning = true;
                GameFlow.GoToGame();
                return;
            }

            if (IsPointerOverRect(backToTitleButtonRect, pointer))
            {
                transitioning = true;
                GameFlow.GoToTitle();
                return;
            }

            // S-15: UPG 購入ボタン（シーン遷移を伴わないため transitioning は立てない）。
            if (upgradeBuyButtonRects != null)
            {
                for (int i = 0; i < upgradeBuyButtonRects.Length; i++)
                {
                    if (IsPointerOverRect(upgradeBuyButtonRects[i], pointer))
                    {
                        HandlePurchaseClick((UpgradeKind)i);
                        return;
                    }
                }
            }
        }

        private bool IsPointerOverRect(RectTransform rect, Vector2 screenPos) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, uiCamera);

        /// <summary>
        /// S-16 CR-CODE iter2 #1（major）対応: EventSystem 非依存でスライダーへのクリック/ドラッグ入力を
        /// 実現する。押下開始フレームでスライダー矩形上にポインタがあれば以後の押下継続中そのスライダーを
        /// 握り続け（矩形外へポインタが出ても離すまで値を更新し続ける — 一般的な drag UX）、押下解除で離す。
        /// </summary>
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
                // 新規に握るのは押下が開始したフレームのみ（矩形外から drag してきた場合は握らない）。
                if (!inputReader.ClickPressedThisFrame) return;
                if (IsPointerOverRect((RectTransform)bgmVolumeSlider.transform, pointer)) draggingSlider = bgmVolumeSlider;
                else if (IsPointerOverRect((RectTransform)sfxVolumeSlider.transform, pointer)) draggingSlider = sfxVolumeSlider;
                else return;
            }

            ApplyPointerToSlider(draggingSlider, pointer);
        }

        /// <summary>ポインタのスライダー矩形内ローカルX位置から slider.value を直接設定する。</summary>
        private void ApplyPointerToSlider(Slider slider, Vector2 screenPos)
        {
            var rect = (RectTransform)slider.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCamera, out Vector2 local)) return;

            float ratio = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x);
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(ratio));
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);
            scaler.matchWidthOrHeight = GameConfig.Ui.CanvasScalerMatchWidthOrHeight;
            UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, uiCamera);

            CreateFullScreenPanel(canvasGo.transform, "Background", GameConfig.Ui.PanelBackground);

            // CR-CODE iter1 #3: null は「Boot 未経由（単体テスト等）」の正当な状態であり Error ではない
            // （Error 昇格は QA-PLAY のエラー0検査/LogAssert を汚す）。ただし製品フローで Boot の
            // SetCurrentSaveData 配線が退行した場合に無音でゼロ表示へ落ちるのを防ぐため Warning で1回記録する。
            SaveData data = GameFlow.CurrentSaveData;
            if (data == null)
            {
                Debug.LogWarning("[MenuScreen] GameFlow.CurrentSaveData is null; rendering defaults (expected only when Menu is entered without going through Boot, e.g. isolated PlayMode tests).");
                data = SaveData.CreateDefault();
            }

            // S-15: UPG購入行の追加でも画面内に収まるよう、既存 MenuRowStepY より詰めた専用ステップに切替える
            // （GameConfig.Ui.MenuRowStepY 自体は変更しない — 共有ファイル規律。本 story はこの新ステップへ
            // 丸ごと移行する）。
            int row = 0;
            float NextY() => GameConfig.Ui.MenuTopAnchorY - (row++ * GameConfig.Ui.MenuRowStepDenseY);

            CreateText(canvasGo.transform, "MenuTitleText", "MENU",
                GameConfig.Ui.SubtitleFontSize, GameConfig.Ui.TextPrimary, NextY());

            // S-20: セーブ復旧トースト（recovered=true 時のみ表示。TitleScreen と同一文言・スタイル）。
            // TitleScreen.BuildUi と同じパターンで常に行スペースを確保し、非表示時は空テキストのまま隠す。
            float recoveryRowY = NextY();
            recoveryNoticeText = CreateText(canvasGo.transform, "RecoveryNoticeText", string.Empty,
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.RecoveryNoticeColor, recoveryRowY);
            bool recovered = GameFlow.Recovered;
            recoveryNoticeText.gameObject.SetActive(recovered);
            if (recovered)
            {
                recoveryNoticeText.text = UiText.SaveCorruptionRecoveredMessage;
                // CR-CODE iter1 #2（minor）対応: Recovered は Boot ロード時に一度だけ設定される静的フラグの
                // ため、消費しないと正準フロー Result→Menu を繰り返すたびに復旧トーストが再表示され
                // 「また破損した」という誤認を招く。正準フロー Boot→Title→Menu は必ず Title を経由してから
                // 最初の Menu に到達するため、Menu 側でのみ消費すれば「Title・Menu 双方で最低1回は表示」
                // という契約（本ファイル冒頭コメント）は崩れない（Title は消費しない設計のまま）。
                GameFlow.SetRecovered(false);
            }

            // CR-CODE iter1 #1（major）対応: GameFlow.SaveFailed（直近 Persistence.Save 失敗）の消費者が
            // 存在せず、保存失敗がプレイヤーから不可視だった指摘への対応。RecoveryNoticeText と同じ行に
            // サブ行として重ね、Menu 内での購入操作による保存失敗も RefreshSaveFailedNotice() で反映する
            // （GameFlow.SaveFailed は「未解決の保存失敗」を表す継続ステータスのため、Recovered と異なり
            // 表示のたびに消費はしない — 次の保存成功で GameFlow.SetSaveFailed(false) されるまで表示し続ける）。
            // CR-CODE iter2 #1/#4（minor・cosmetic）対応: GameConfig.Ui.MenuSaveFailedNoticeOffsetY 参照。
            saveFailedNoticeText = CreateText(canvasGo.transform, "SaveFailedNoticeText", string.Empty,
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.ResultLossColor, recoveryRowY,
                offsetX: 0f, offsetY: GameConfig.Ui.MenuSaveFailedNoticeOffsetY);
            RefreshSaveFailedNotice();

            // ── (1) プレイ開始 ──────────────────────────────────────────
            playButtonRect = CreateButton(canvasGo.transform, "PlayButton", "プレイ開始", NextY());

            // ── (2) アウトゲーム表示: 実績一覧・統計・所持essence・UPG Lv+購入 ──────
            // CR-CODE iter1 #1: AddComponent 生成の RectTransform 既定はゼロサイズ点アンカーのため、
            // 全画面ストレッチにしないと配下の子（フラクショナルアンカー配置）が原点へ収束して重なる。
            var outgameGo = new GameObject("OutgamePanel", typeof(RectTransform));
            outgameGo.transform.SetParent(canvasGo.transform, false);
            outgamePanel = outgameGo.transform;
            StretchFullScreen((RectTransform)outgameGo.transform);

            CreateText(outgameGo.transform, "OutgameHeader", "アウトゲーム表示",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.AccentTeal, NextY());

            CreateText(outgameGo.transform, "StatsText",
                $"総ラン数: {data.totalRunsPlayed}   総勝利数: {data.totalWins}   総撃破数: {data.totalKills}",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            string bestClearTime = UiText.FormatBestClearTime(data.bestClearTimeSec);
            CreateText(outgameGo.transform, "HighScoreText",
                $"ハイスコア: {data.highScore}   ベストクリアタイム: {bestClearTime}",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            // essence 概要（後方互換: "EssenceUpgText" は S-03 の PlayMode テストが "essence: N" /
            // "UPG-01 LvN" の部分文字列一致で検証している正本表示のため、フォーマットは変更しない）。
            float essenceRowY = NextY();
            essenceUpgText = CreateText(outgameGo.transform, "EssenceUpgText", BuildEssenceSummaryText(data),
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, essenceRowY);
            CreateIcon(outgameGo.transform, "EssenceIcon", GameConfig.AssetKeys.IconEssence,
                essenceRowY, GameConfig.Ui.MenuEssenceIconOffsetX, GameConfig.Ui.MenuEssenceIconSize);

            // ── UPG-01〜03: Lv表示 + 購入ボタン（S-15） ──
            upgradeBuyButtonRects = new RectTransform[UpgradeDefs.Length];
            upgradeLevelTexts = new Text[UpgradeDefs.Length];
            upgradeButtonLabelTexts = new Text[UpgradeDefs.Length];
            upgradeButtonImages = new Image[UpgradeDefs.Length];
            for (int i = 0; i < UpgradeDefs.Length; i++)
            {
                float y = NextY();
                (string id, _) = UpgradeDefs[i];
                upgradeLevelTexts[i] = CreateText(outgameGo.transform, $"{id}LevelText", "",
                    GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, y, GameConfig.Ui.MenuUpgRowLabelOffsetX);

                var (buttonRect, buttonLabel, buttonImage) = CreateSmallButton(outgameGo.transform, $"{id}BuyButton",
                    "", y, GameConfig.Ui.MenuUpgRowButtonOffsetX, GameConfig.Ui.MenuUpgButtonWidth, GameConfig.Ui.MenuUpgButtonHeight);
                upgradeBuyButtonRects[i] = buttonRect;
                upgradeButtonLabelTexts[i] = buttonLabel;
                upgradeButtonImages[i] = buttonImage;

                UpdateUpgradeRow(i, data);
            }

            CreateText(outgameGo.transform, "AchievementsHeader", "実績",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.AccentTeal, NextY());

            bool[] unlocked =
            {
                data.achFirstVictory, data.achPerfectDefense, data.achCenturySlayer,
                data.achAoeSpecialist, data.achFrugalArchitect,
            };
            achievementTexts = new Text[AchievementDefs.Length];
            for (int i = 0; i < AchievementDefs.Length; i++)
            {
                (string id, string label) = AchievementDefs[i];
                bool isUnlocked = unlocked[i];
                Color color = isUnlocked ? GameConfig.Ui.RecoveryNoticeColor : WithAlpha(GameConfig.Ui.TextPrimary, GameConfig.Ui.MenuAchievementLockedAlpha);
                string marker = isUnlocked ? "[獲得済]" : "[未獲得]";
                float y = NextY();

                (int current, int target, bool hasProgress) = GetAchievementProgress(id, data);
                string content = (hasProgress && !isUnlocked)
                    ? $"{marker} {id} {label} ({Mathf.Min(current, target)}/{target})"
                    : $"{marker} {id} {label}";

                achievementTexts[i] = CreateText(outgameGo.transform, $"Achievement_{id}", content,
                    GameConfig.Ui.BodyFontSize, color, y);

                if (hasProgress && !isUnlocked)
                {
                    float ratio = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;
                    CreateProgressBar(outgameGo.transform, $"Achievement_{id}_Progress", y, ratio);
                }
            }

            // ── (3) 設定: 音量スライダー(BGM/SFX) + 操作説明 ────────────────
            // CR-CODE iter1 #1: OutgamePanel と同理由で全画面ストレッチが必須。
            var settingsGo = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsGo.transform.SetParent(canvasGo.transform, false);
            settingsPanel = settingsGo.transform;
            StretchFullScreen((RectTransform)settingsGo.transform);

            CreateText(settingsGo.transform, "SettingsHeader", "設定",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.AccentTeal, NextY());

            // S-16: 音量スライダーの実効反映（AudioSource.volume）+ 永続化（Persistence 経由・GameFlow キャリー）。
            // 初期値は GameFlow.CurrentAudioSettings（Boot ロード時/前回変更時に GameBootstrap/MenuScreen が
            // 設定する — GameFlow.cs 参照）を読む。未設定（Boot 非経由の単体テスト等）は既定値へフォールバック
            // する（表示専任の原則。値を複製保持するのではなく、AudioSource.volume 自身が実効状態の正）。
            AudioSettingsData audioSettings = GameFlow.CurrentAudioSettings ?? AudioSettingsData.CreateDefault();

            bgmAudioSource = gameObject.AddComponent<AudioSource>();
            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true; // BGM 想定。実クリップ割当・再生配線は S-19（資産統合）の責務。
            bgmAudioSource.volume = audioSettings.BgmVolume;

            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.loop = false;
            sfxAudioSource.volume = audioSettings.SfxVolume;

            bgmVolumeSlider = CreateVolumeSlider(settingsGo.transform, "BgmVolumeSlider", "BGM音量", NextY(),
                audioSettings.BgmVolume, OnBgmVolumeChanged);
            sfxVolumeSlider = CreateVolumeSlider(settingsGo.transform, "SfxVolumeSlider", "SFX音量", NextY(),
                audioSettings.SfxVolume, OnSfxVolumeChanged);

            CreateText(settingsGo.transform, "OperationInstructionsText",
                "操作: 左クリック=設置/選択/購入　右クリック=選択解除　Esc=一時停止（Gameシーン中）",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            // ── (4) 終了導線: タイトルへ戻る ────────────────────────────
            backToTitleButtonRect = CreateButton(canvasGo.transform, "BackToTitleButton", "タイトルへ戻る", NextY());
        }

        /// <summary>
        /// ACH-03/04 の「現在値/目標値」進捗（gdd「実績」節: 進捗表示要否=要の2件のみ）。
        /// ACH-03（累計撃破）は SaveData.totalKills を素直に使う。ACH-04（範囲特化=単一ラン内AoE撃破数）は
        /// SaveData に永続フィールドが無いため、直近ラン結果（GameFlow.LastRunResult。Result→Menu 遷移で
        /// プロセス内キャリーされる値。Boot経由の初回Menuでは HasRunResult=false→0）を「現在値」の目安として
        /// 表示する（S-15 実装判断: 累計最大値の記録は本ストーリーのスコープ外）。他の実績（ACH-01/02/05）は
        /// gdd 上「進捗表示要否: 不要」のため対象外。
        /// </summary>
        private static (int Current, int Target, bool HasProgress) GetAchievementProgress(string id, SaveData data)
        {
            switch (id)
            {
                case "ACH-03":
                    return (data.totalKills, GameConfig.Meta.CenturySlayerKills, true);
                case "ACH-04":
                    int current = GameFlow.HasRunResult ? GameFlow.LastRunResult.AoeKillCount : 0;
                    return (current, GameConfig.Meta.AoeSpecialistKills, true);
                default:
                    return (0, 0, false);
            }
        }

        /// <summary>essence + UPG-01〜03 Lv の要約行（後方互換フォーマット。S-03 テスト参照）。</summary>
        private static string BuildEssenceSummaryText(SaveData data) =>
            $"essence: {data.essence}   UPG-01 Lv{data.upgStartingGoldLv}   " +
            $"UPG-02 Lv{data.upgTowerDiscountLv}   UPG-03 Lv{data.upgEssenceRateLv}";

        private static int GetUpgradeLevel(SaveData data, UpgradeKind kind)
        {
            // 既存コードベースの慣例（switch式ではなく従来の switch 文）に合わせる。
            switch (kind)
            {
                case UpgradeKind.Upg01: return data.upgStartingGoldLv;
                case UpgradeKind.Upg02: return data.upgTowerDiscountLv;
                case UpgradeKind.Upg03: return data.upgEssenceRateLv;
                default: return 0;
            }
        }

        /// <summary>
        /// UPG購入の純粋計算（クラス冒頭コメント参照: S-14 の Systems/Meta 正本 reducer 着手までの
        /// Ui 層内限定の最小実装）。cost は全Lv共通固定（gdd「持ち越しアップグレード」節）。
        /// </summary>
        private static bool TryPurchaseUpgrade(SaveData prev, UpgradeKind kind, out SaveData next)
        {
            int currentLv = GetUpgradeLevel(prev, kind);
            int cost = GameConfig.Meta.UpgradePurchaseCostPerLevel;
            if (currentLv >= GameConfig.Meta.UpgradeMaxLevel || prev.essence < cost)
            {
                next = prev;
                return false;
            }

            next = prev.Clone();
            next.essence = prev.essence - cost;
            switch (kind)
            {
                case UpgradeKind.Upg01: next.upgStartingGoldLv = currentLv + 1; break;
                case UpgradeKind.Upg02: next.upgTowerDiscountLv = currentLv + 1; break;
                case UpgradeKind.Upg03: next.upgEssenceRateLv = currentLv + 1; break;
            }
            return true;
        }

        private void HandlePurchaseClick(UpgradeKind kind)
        {
            SaveData current = GameFlow.CurrentSaveData ?? SaveData.CreateDefault();
            if (!TryPurchaseUpgrade(current, kind, out SaveData next))
            {
                return; // 資金不足 or Lv上限。ボタン表示は既に不足/上限表示になっている（一瞥可読・非破壊）。
            }

            GameFlow.SetCurrentSaveData(next);
            PersistPurchase(next);
            essenceUpgText.text = BuildEssenceSummaryText(next);
            for (int i = 0; i < UpgradeDefs.Length; i++)
            {
                UpdateUpgradeRow(i, next);
            }
        }

        /// <summary>
        /// GameBootstrap/RunOutcomeController と同じ「黙って初期化しない」原則を踏襲: Save 失敗は1回ログし
        /// プロセス内フラグ(GameFlow.SaveFailed)へ伝播する。CR-CODE iter1 #1 対応: 呼び出し後に
        /// RefreshSaveFailedNotice() で画面表示へも反映する（購入操作中に失敗/回復のどちらが起きても
        /// その場で表示が追随する）。
        /// </summary>
        private void PersistPurchase(SaveData data)
        {
            saveStore ??= SaveStores.CreateDefault();
            try
            {
                saveStore.Save(data);
                GameFlow.SetSaveFailed(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveFailure] MenuScreen failed to persist SaveData after UPG purchase: {ex}");
                GameFlow.SetSaveFailed(true);
            }
            RefreshSaveFailedNotice();
        }

        /// <summary>
        /// GameFlow.SaveFailed の現在値を SaveFailedNoticeText へ反映する（CR-CODE iter1 #1）。
        /// BuildUi() の初期表示、および PersistPurchase() 呼び出し後（購入操作起因の保存成否）の両方から呼ぶ。
        /// </summary>
        private void RefreshSaveFailedNotice()
        {
            if (saveFailedNoticeText == null) return;
            bool saveFailed = GameFlow.SaveFailed;
            saveFailedNoticeText.gameObject.SetActive(saveFailed);
            if (saveFailed)
            {
                saveFailedNoticeText.text = UiText.BuildSaveFailedMessage("進行状況");
            }
        }

        /// <summary>
        /// 音量スライダー変更ハンドラ（S-16）。実際の AudioSource.volume へ即時反映してから永続化する
        /// （一瞥可読・即時フィードバックの原則 — 役割宣言の鉄則。永続化の成否とは独立に音は即変わる）。
        /// </summary>
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

        /// <summary>
        /// PersistPurchase と同じ「黙って初期化しない」原則: 保存失敗は1回ログする。保存に失敗しても
        /// AudioSource.volume 自体は既に反映済みのため今回セッションの実効音量は変わらない — 失われるのは
        /// 次回起動時の復元のみ（contract §6 の3点セットはメタ進行 SaveData 専用のため本設定には適用しない
        /// — Persistence/IAudioSettingsStore.cs 冒頭コメント参照）。
        /// </summary>
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
                Debug.LogError($"[SaveFailure] MenuScreen failed to persist AudioSettingsData after volume change: {ex}");
            }
        }

        private void UpdateUpgradeRow(int index, SaveData data)
        {
            var kind = (UpgradeKind)index;
            (string id, string label) = UpgradeDefs[index];
            int lv = GetUpgradeLevel(data, kind);
            int maxLv = GameConfig.Meta.UpgradeMaxLevel;
            int cost = GameConfig.Meta.UpgradePurchaseCostPerLevel;

            upgradeLevelTexts[index].text = $"{id} {label} Lv{lv}/{maxLv}";

            bool maxed = lv >= maxLv;
            bool affordable = data.essence >= cost;
            upgradeButtonLabelTexts[index].text = maxed ? "Lv上限" : (affordable ? $"購入({cost})" : $"不足({cost})");

            bool purchasable = !maxed && affordable;
            // 既存 S-08 のタワー選択メニュー「資金不足減光」係数を再利用（新規定数を増やさず一貫した見た目にする）。
            Color baseColor = GameConfig.Ui.AccentTeal;
            upgradeButtonImages[index].color = purchasable ? baseColor : WithAlpha(baseColor, GameConfig.Ui.TowerSelectInsufficientAlpha);
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

        /// <summary>
        /// RectTransform を親いっぱいにストレッチする（CR-CODE iter1 #1）。AddComponent 生成の
        /// RectTransform 既定値（ゼロサイズ点アンカー）のままだと、配下にフラクショナルアンカーの
        /// 子を配置した際に全て親原点へ収束して重なるため、グルーピング用パネルは必ずこれを通す。
        /// </summary>
        private static void StretchFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, float anchorY, float offsetX = 0f, float offsetY = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(offsetX, offsetY);
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

        /// <summary>
        /// アイコン画像(S-15)。CR-CODE iter2 #1 で本コメントを実態に合わせて修正: 現状の唯一の呼び出し元
        /// （essenceRowY・IMG-04 IconEssence）は design/assets.md で status=generated、実体も
        /// game/Assets/Resources/Generated/textures/icon-essence.png に取込済み（game/_generated/MANIFEST.jsonl
        /// asset_id=IMG-04 参照）。よって Resources.Load 失敗は「未生成」ではなく、キーのタイポ・.meta 退行・
        /// 取込順序ズレ等の異常系のみを指し、この条件には「正当な到達経路」が存在しない（健全なビルドでは
        /// 一切発火しない）。CR-CODE iter1 #3 の null-CurrentSaveData（Boot 非経由の単体テストという正当な
        /// 到達経路を持つため Warning が妥当）とは事情が異なるため、本箇所は LogError に昇格する
        /// （tech-stack-unity.md 規約12: LogWarning は「縮退が正当なケース」限定。本条件は該当しない）。
        /// IMG-05/06（IconAchievements/IconUpgrades）は design/assets.md 上は既に generated 済みだが、本ファイル
        /// からは未使用（呼び出し元が無い）ため、この分岐の到達可能性には影響しない。将来 IMG-05/06 の
        /// 呼び出し元が配線された際は、本 LogError が退行検知の安全網として機能する（PlayMode テストの
        /// カバレッジ有無に関わらず QA-PLAY 観点1のログエラー0検査で検知される）。
        /// </summary>
        private static void CreateIcon(Transform parent, string name, string resourceKey, float anchorY, float offsetX, float size)
        {
            Sprite sprite = Resources.Load<Sprite>(resourceKey);
            if (sprite == null)
            {
                Debug.LogError($"[MenuScreen] CreateIcon: Resources.Load<Sprite>(\"{resourceKey}\") returned null; " +
                    "skipping icon render. This key is expected to resolve for generated assets (see design/assets.md) — " +
                    "this is a regression (key typo / .meta import / integration order), not an expected planned-asset gap.");
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(offsetX, 0f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
        }

        /// <summary>ACH-03/04 の現在値/目標値 進捗バー(S-15)。Image.Type.Filled の単一Imageで表現する。</summary>
        private static void CreateProgressBar(Transform parent, string name, float anchorY, float fillAmount)
        {
            var bgGo = new GameObject(name + "Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bgRect = (RectTransform)bgGo.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            bgRect.anchorMin = anchor;
            bgRect.anchorMax = anchor;
            bgRect.pivot = new Vector2(0.5f, 1f);
            bgRect.anchoredPosition = new Vector2(0f, GameConfig.Ui.MenuAchievementProgressBarOffsetY);
            bgRect.sizeDelta = new Vector2(GameConfig.Ui.MenuAchievementProgressBarWidth, GameConfig.Ui.MenuAchievementProgressBarHeight);
            bgGo.GetComponent<Image>().color = GameConfig.Ui.PanelBackground;

            var fillGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = fillAmount;
            fillImage.color = GameConfig.Ui.AccentTeal;
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
            rect.sizeDelta = new Vector2(GameConfig.Ui.MenuButtonWidth, GameConfig.Ui.MenuButtonHeight);
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

        /// <summary>
        /// UPG購入ボタン用の小型ボタン(S-15)。既存 CreateButton は Play/タイトルへ戻る専用の固定サイズ・
        /// 中央アンカーのため、幅/高さ/X方向オフセットを取れる別ヘルパとして追加する（既存呼び出し元は無変更）。
        /// </summary>
        private static (RectTransform Rect, Text Label, Image Image) CreateSmallButton(
            Transform parent, string name, string label, float anchorY, float offsetX, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(offsetX, 0f);
            rect.sizeDelta = new Vector2(width, height);
            var image = go.GetComponent<Image>();
            image.color = GameConfig.Ui.AccentTeal;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = GameConfig.Ui.MenuUpgButtonFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GameConfig.Ui.PanelBackground;
            text.text = label;

            return (rect, text, image);
        }

        /// <summary>
        /// 音量スライダー生成（S-16 拡張: initialValue/onValueChanged を追加）。initialValue は
        /// リスナー登録前に設定するため、生成時点では onValueChanged が発火しない（無用な即時保存を防ぐ）。
        /// </summary>
        private static Slider CreateVolumeSlider(Transform parent, string name, string label, float anchorY,
            float initialValue, UnityAction<float> onValueChanged)
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

            // 背景
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = GameConfig.Ui.PanelBackground;

            // Fill Area / Fill
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
