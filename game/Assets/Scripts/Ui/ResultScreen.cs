// ResultScreen.cs — Result シーンの表示・入力配線（S-09。薄い Component。ロジックは持たない）。
// docs/architecture.md §2: Result は今回スコア/勝敗・新規実績・ハイスコア比較を表示し、
// 「もう一度」→Game（新規ラン） / 「メニューへ」→Menu の2導線を持つ。
// S-20 追記: ベストクリアタイム（data.bestClearTimeSec）を表示する。未記録（-1、Menu の HighScoreText 行
// と同じ判定式）は「--」表示にする。ハイスコア更新時の強調表示（isNewHighScore）は S-09 実装済みのため
// 本 story での変更は無い。
// 表示専任: 勝敗・統計・実績は GameFlow（Game→Result 間の RunResult キャリー、および
// Persistence.Save 実施側〔S-06〕が保存直後に更新する GameFlow.CurrentSaveData）から読むだけで、
// UI 側に値を複製・二重計算しない（役割宣言の鉄則）。finalScore の算出は ScoreSystem の純粋関数を
// 再呼び出しするのみ（Systems/ 層のロジックを Ui に移植しない）。
// Canvas は ScreenSpaceCamera 固定（tech-stack-unity.md 規約14）。全数値/色は GameConfig.Ui。
using UnityEngine;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Input;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Ui
{
    /// <summary>
    /// Result シーンに1つだけ置く。UI 生成・入力購読・シーン遷移の配線のみを行う。
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        // ACH-01〜05（gdd「実績（採用時のみ）」節の固有名詞・順序。MenuScreen の一覧と同一の表示語彙）。
        private static readonly (string Id, string Label)[] AchievementDefs =
        {
            ("ACH-01", "初勝利"),
            ("ACH-02", "完全防衛"),
            ("ACH-03", "累計撃破"),
            ("ACH-04", "範囲特化"),
            ("ACH-05", "倹約防衛"),
        };

        private InputReader inputReader;
        private Camera uiCamera;
        private bool transitioning;

        private Transform infoPanel;
        private Text outcomeText;
        private Text scoreText;
        private Text highScoreText;
        private Text bestClearTimeText; // S-20: 未記録(-1)は「--」表示
        private Text saveFailedNoticeText; // CR-CODE iter1 #1(major)対応: GameFlow.SaveFailed の通知表示
        private Text newAchievementsText;
        private RectTransform playAgainButtonRect;
        private RectTransform backToMenuButtonRect;

        /// <summary>テスト用の読み取り専用状態公開（表示専任の原則: 内部状態そのものは複製しない）。</summary>
        public Transform InfoPanel => infoPanel;
        public RectTransform PlayAgainButtonRect => playAgainButtonRect;
        public RectTransform BackToMenuButtonRect => backToMenuButtonRect;
        public Camera UiCamera => uiCamera;

        /// <summary>
        /// テスト用の読み取り専用状態公開（CR-CODE iter1 #1。MenuScreen.SaveFailedNoticeVisible と同型）。
        /// </summary>
        public bool SaveFailedNoticeVisible => saveFailedNoticeText != null && saveFailedNoticeText.gameObject.activeSelf;
        public bool SaveFailedNoticeTextExists => saveFailedNoticeText != null;

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
            if (!inputReader.ClickPressedThisFrame) return;

            Vector2 pointer = inputReader.PointerScreenPosition;
            if (IsPointerOverRect(playAgainButtonRect, pointer))
            {
                transitioning = true;
                // 消費済み RunResult は破棄する（GameFlow.ClearRunResult の呼び出し契機 — GameFlow.cs 参照）。
                // Game シーンは新規ロードされ、CORE_HP_MAX/STARTING_GOLD 等の初期値は各 System が
                // GameConfig から都度読むため、シーン再ロードそのものが新規ランのリセットになる。
                GameFlow.ClearRunResult();
                GameFlow.GoToGame();
                return;
            }

            if (IsPointerOverRect(backToMenuButtonRect, pointer))
            {
                transitioning = true;
                GameFlow.ClearRunResult();
                GameFlow.GoToMenu();
            }
        }

        private bool IsPointerOverRect(RectTransform rect, Vector2 screenPos) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, uiCamera);

        private void BuildUi()
        {
            var canvasGo = new GameObject("ResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);
            scaler.matchWidthOrHeight = GameConfig.Ui.CanvasScalerMatchWidthOrHeight;
            UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, uiCamera);

            CreateFullScreenPanel(canvasGo.transform, "Background", GameConfig.Ui.PanelBackground);

            // RunResult 未キャリー（Result シーン単体ロード等）は Error ではなく Warning で記録し
            // 敗北ランとして安全に既定表示する（MenuScreen の CurrentSaveData==null 分岐と同じ方針 —
            // 製品フローでは GameFlow.GoToResult 経由以外に Result へ来ないため異常時のみ発生）。
            bool hasRunResult = GameFlow.TryGetLastRunResult(out RunResult runResult);
            if (!hasRunResult)
            {
                Debug.LogWarning("[ResultScreen] GameFlow に RunResult が無い状態で Result シーンへ到達した; " +
                    "既定の敗北表示にフォールバックする（想定: Result シーン単体テスト/直接ロード時のみ）。");
                runResult = default;
            }

            SaveData data = GameFlow.CurrentSaveData;
            if (data == null)
            {
                Debug.LogWarning("[ResultScreen] GameFlow.CurrentSaveData is null; rendering defaults (expected only when Result is entered without going through Boot/Game, e.g. isolated PlayMode tests).");
                data = SaveData.CreateDefault();
            }

            // hasRunResult==false のフォールバックは「敗北ラン」として中立表示する必要があり、
            // runResult=default（ClearTimeSec=0）を ScoreSystem.ComputeFinalScore に通すと
            // TimeParSec 由来のボーナスだけで非ゼロスコアが出てしまう（成功に見える偽装）ため、
            // このケースに限りスコアは 0 で固定する（CR-CODE iter1 M指摘対応）。
            int finalScore = hasRunResult ? ScoreSystem.ComputeFinalScore(runResult) : 0;
            // next.highScore = max(prev.highScore, finalScore) という MetaProgression の式より、
            // 「finalScore が現在の highScore と一致」は「このランが highScore を更新/更新に並んだ」ことを
            // 過不足なく意味する（前回値の別キャリーが無くてもここだけで判定できる — GameFlow.CurrentSaveData
            // が Persistence.Save 直後の値であることが前提。GameFlow.cs の SetCurrentSaveData 呼び出し契約参照）。
            bool isNewHighScore = finalScore > 0 && finalScore == data.highScore;

            infoPanel = new GameObject("InfoPanel", typeof(RectTransform)).transform;
            infoPanel.SetParent(canvasGo.transform, false);
            StretchFullScreen((RectTransform)infoPanel);

            int row = 0;
            float NextY() => GameConfig.Ui.ResultTopAnchorY - (row++ * GameConfig.Ui.ResultRowStepY);

            Color outcomeColor = runResult.IsWin ? GameConfig.Ui.ResultWinColor : GameConfig.Ui.ResultLossColor;
            string outcomeLabel = runResult.IsWin ? "勝利" : "敗北";
            outcomeText = CreateText(infoPanel, "OutcomeText", outcomeLabel,
                GameConfig.Ui.ResultOutcomeFontSize, outcomeColor, NextY());

            scoreText = CreateText(infoPanel, "ScoreText", $"スコア: {finalScore}",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            string highScoreLabel = isNewHighScore
                ? $"ハイスコア: {data.highScore}（更新！）"
                : $"ハイスコア: {data.highScore}";
            Color highScoreColor = isNewHighScore ? GameConfig.Ui.ResultHighScoreHighlightColor : GameConfig.Ui.TextPrimary;
            highScoreText = CreateText(infoPanel, "HighScoreText", highScoreLabel,
                GameConfig.Ui.BodyFontSize, highScoreColor, NextY());

            // S-20: ベストクリアタイム。MenuScreen の HighScoreText 行（S-15）と同じ判定式（未記録=-1→「--」。
            // CR-CODE iter1 #3 対応: UiText.FormatBestClearTime に共通化して drift を防ぐ）。
            string bestClearTime = UiText.FormatBestClearTime(data.bestClearTimeSec);
            bestClearTimeText = CreateText(infoPanel, "BestClearTimeText", $"ベストクリアタイム: {bestClearTime}",
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.TextPrimary, NextY());

            string newAchievementsLabel = BuildNewAchievementsLabel(runResult, data);
            newAchievementsText = CreateText(infoPanel, "NewAchievementsText", newAchievementsLabel,
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.AccentTeal, NextY());

            // CR-CODE iter1 #1（major）対応: RunOutcomeController.FinalizeRun が Persistence.Save に失敗した
            // 場合、GameFlow.SaveFailed を読んで通知する（黙って通常の Result 表示にしない — contract §6 の
            // recovered パターンをエラー系にも踏襲）。Result 到達前に一度だけ確定する値のため、Menu の
            // RefreshSaveFailedNotice() のような動的更新は不要（BuildUi 時の1回読みで足りる）。
            saveFailedNoticeText = CreateText(infoPanel, "SaveFailedNoticeText", string.Empty,
                GameConfig.Ui.BodyFontSize, GameConfig.Ui.ResultLossColor, NextY());
            bool saveFailed = GameFlow.SaveFailed;
            saveFailedNoticeText.gameObject.SetActive(saveFailed);
            if (saveFailed)
            {
                saveFailedNoticeText.text = UiText.BuildSaveFailedMessage("今回の結果");
            }

            playAgainButtonRect = CreateButton(canvasGo.transform, "PlayAgainButton", "もう一度", NextY());
            backToMenuButtonRect = CreateButton(canvasGo.transform, "BackToMenuButton", "メニューへ", NextY());
        }

        /// <summary>
        /// 今回解放した新規実績の表示文字列を組む。
        /// gdd の実績条件は「累積値の閾値到達（ACH-01/03）」と「単一ラン内条件（ACH-02/04/05）」の2種。
        /// 累積系は post 保存値から加算前の値を逆算でき「今回初めて条件を満たしたか」を厳密に判定できる。
        /// 単一ラン系は履歴（このランの前から解放済みだったか）を GameFlow が保持しないため、
        /// 「このランの RunResult が条件を満たしたか」を新規解放の近似として採用する
        /// （既に解放済みの実績を再度条件達成した場合も表示される既知の限界。実害は軽微な表示重複のみで、
        /// SaveData 側のフラグは既存どおり単調〔一度trueは不変〕のため実績データそのものは破壊されない）。
        /// </summary>
        private static string BuildNewAchievementsLabel(RunResult r, SaveData post)
        {
            int prevTotalWins = post.totalWins - (r.IsWin ? 1 : 0);
            int prevTotalKills = post.totalKills - r.KillCount;

            // 負値は「post が今回ランを反映した post-save 値ではない」ことの証拠
            // （GameFlow.CurrentSaveData の SetCurrentSaveData 契約 — GameFlow.cs 参照 — の退行）。
            // 検出は部分的（新規解放の誤判定は防げない）で契約責務は S-06 側にあるため、
            // ここでは記録のみ行い表示は既存の（stale な）近似ロジックを続行する（CR-CODE iter1 L指摘対応）。
            if (prevTotalWins < 0 || prevTotalKills < 0)
            {
                Debug.LogWarning("[ResultScreen] carried SaveData appears stale — SetCurrentSaveData contract regression?");
            }

            bool[] newlyUnlocked =
            {
                r.IsWin && prevTotalWins == 0 && post.achFirstVictory,                                   // ACH-01
                post.achPerfectDefense && r.IsWin && r.CoreHpRemaining >= GameConfig.Core.HpMax,          // ACH-02
                post.achCenturySlayer && prevTotalKills < GameConfig.Meta.CenturySlayerKills
                    && post.totalKills >= GameConfig.Meta.CenturySlayerKills,                             // ACH-03
                post.achAoeSpecialist && r.AoeKillCount >= GameConfig.Meta.AoeSpecialistKills,            // ACH-04
                post.achFrugalArchitect && r.IsWin && r.UsedBuildSpots <= GameConfig.Meta.FrugalMaxSpots, // ACH-05
            };

            string labels = string.Empty;
            for (int i = 0; i < AchievementDefs.Length; i++)
            {
                if (!newlyUnlocked[i]) continue;
                (string id, string label) = AchievementDefs[i];
                labels += (labels.Length > 0 ? " / " : string.Empty) + $"{id} {label}";
            }

            return labels.Length > 0 ? $"新規実績: {labels}" : "新規実績: なし";
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
    }
}
