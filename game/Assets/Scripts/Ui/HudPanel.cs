// HudPanel.cs — Game シーンの HUD 表示 + タワー選択メニュー入力配線（S-08。薄い Component。ロジックは持たない）。
// docs/architecture.md §2/§3: Game シーンは資金・コアHP・現在ウェーブをリアルタイム表示し、
// ビルドスポット左クリックでタワー種別選択メニュー（Ui/TowerSelectPanel）を開く。
// 表示専任: 資金は Components/BuildSpotController.Economy（S-05）、コアHPは Components/CoreView.CurrentHp
// （S-04）、ウェーブ番号は Components/WaveSpawnController.WaveSystem.CurrentWaveNumber（S-04）から毎フレーム
// 読むだけで複製しない（UI に状態を持たせない — 役割宣言の鉄則）。設置操作自体は
// Components/BuildSpotController.TryPlaceTower（S-05 が提供する確定操作の受け口。BuildSpotController.cs の
// 設計コメント「メニューの表示・開閉自体は S-08 の責務」に対応）を呼ぶだけで、判定ロジックは持たない。
// クリック判定は uGUI Button/EventSystem を使わず、InputReader のポインタ座標 + RectTransformUtility の
// 矩形ヒットテスト（ビルドスポットの場合は Camera.WorldToScreenPoint 投影 + ピクセル半径判定）で行う
// （tech-stack-unity.md 規約4。TitleScreen/MenuScreen と同じ「非破壊入力」パターンを踏襲）。
// Canvas は ScreenSpaceCamera 固定（tech-stack-unity.md 規約14）。全数値/色は GameConfig.Ui。
using UnityEngine;
using UnityEngine.UI;
using ForgeGame.Components;
using ForgeGame.Input;
using ForgeGame.Systems;

namespace ForgeGame.Ui
{
    /// <summary>Game シーンに1つだけ置く。UI 生成・入力購読・タワー設置確定操作の配線のみを行う。</summary>
    public sealed class HudPanel : MonoBehaviour
    {
        [SerializeField] private BuildSpotController buildSpotController;
        [SerializeField] private CoreView coreView;
        [SerializeField] private WaveSpawnController waveSpawnController;
        // S-17 CR-CODE iter1 #1/#2 fix: Time.timeScale だけでは PausePanel との Update 実行順race を
        // 塞げない（下記 HandleInput のコメント参照）ため、PausePanel.IsOpen / LastResumeFrame を直接参照する。
        [SerializeField] private PausePanel pausePanel;

        private InputReader inputReader;
        private Camera uiCamera;
        private TowerSelectPanel towerSelectPanel;
        // S-23: 設置済みタワー左クリックで開くアップグレード/売却パネル（CR-CODE S-10/S-11 [BLOCKER] 解消）。
        private TowerActionPanel towerActionPanel;
        // S-17 CR-CODE iter2 #5(minor) fix: SetReferencesForTest 経由の注入かどうかを区別するためのフラグ。
        // pausePanel==null を Awake で検知した際、テスト注入経路（意図的な省略）まで誤検知して
        // ログを汚さないための抑制条件に使う（下記 Awake コメント参照）。
        private bool referencesSetForTest;

        private Text goldText;
        private Text coreHpText;
        private Text waveText;

        /// <summary>テスト用の読み取り専用状態公開（表示専任の原則: 内部状態そのものは複製しない）。</summary>
        public Text GoldText => goldText;
        public Text CoreHpText => coreHpText;
        public Text WaveText => waveText;
        public TowerSelectPanel TowerSelect => towerSelectPanel;
        public TowerActionPanel TowerAction => towerActionPanel;
        public Camera UiCamera => uiCamera;

        /// <summary>テスト用の参照注入。Awake() 実行前（非アクティブ状態）に呼ぶこと（規約9）。
        /// pause は既存呼び出し互換のため省略可（省略時は Time.timeScale ガードのみで動作する）。</summary>
        public void SetReferencesForTest(BuildSpotController build, CoreView core, WaveSpawnController wave, PausePanel pause = null)
        {
            buildSpotController = build;
            coreView = core;
            waveSpawnController = wave;
            pausePanel = pause;
            referencesSetForTest = true;
        }

        private void Awake()
        {
            inputReader = new InputReader();
            uiCamera = Camera.main;
            BuildUi();

            // S-17 CR-CODE iter2 #5(minor) fix: pausePanel は本番 Game.unity では常にインスペクタ配線済み
            // （HudPanel/PausePanel 双方 fileID 400000062 で相互配線）。null は「テストが SetReferencesForTest
            // を経由して意図的に省略した」場合を除き配線バグ以外に起こり得ない。以前は
            // `pausePanel != null &&` のガードのみで、配線が将来のシーン編集で欠落しても Time.timeScale
            // ガードのみへ無警告で縮退していた（UiCanvasHelper の Camera.main==null LogError と同種の
            // 「null は配線バグ」先例に合わせる）。
            if (pausePanel == null && !referencesSetForTest)
            {
                Debug.LogError("[HudPanel] pausePanel is not wired (expected Game.unity inspector reference). " +
                    "Falling back to Time.timeScale-only pause guard, which is vulnerable to the same-frame " +
                    "double-processing race described on PausePanel.LastResumeFrame.");
            }
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
            RefreshDisplays();
            HandleInput();
        }

        private void RefreshDisplays()
        {
            if (buildSpotController != null) SetGold(buildSpotController.Economy.Gold);
            if (coreView != null) SetCoreHp(coreView.CurrentHp, GameConfig.Core.HpMax);
            if (waveSpawnController != null)
            {
                SetWave(waveSpawnController.WaveSystem.CurrentWaveNumber, GameConfig.WaveComposition.Waves.Length);
            }

            // CR-CODE S-23 iter1 minor fix: towerActionPanel の強化ボタン活性状態は Open() 時点の資金
            // スナップショット固定だと、開帳中の撃破報酬による資金増加が反映されず「(資金不足)」非活性が
            // 閉じて開き直すまで復帰しなかった。毎フレーム最新の資金/タワー状態で Refresh することで
            // 追従させる（表示専任の原則: Systems 層の現在値をそのまま渡すだけで UI 側に複製しない）。
            if (buildSpotController != null && towerActionPanel != null && towerActionPanel.IsOpen)
            {
                if (TryFindTowerById(towerActionPanel.OpenTowerId, out TowerInstance openTower))
                {
                    towerActionPanel.Refresh(openTower, buildSpotController.Economy.Gold, buildSpotController.TowerDiscountRate);
                }
                else
                {
                    // CR-CODE S-23 iter2 minor fix: 上の Refresh 経路で desync（パネルが保持する
                    // OpenTowerId に対応するタワーが System 側に無い）を検知した場合、以前は無ログで
                    // Refresh をスキップするだけで stale 表示のパネルを開いたままにしていた。クリック経路
                    // （HandleActionPanelOpenInput の TowerNotFound 分岐）と同じ条件・同じ規範（LogError +
                    // 即時 Close で表面化）に揃える。現状は到達不能な防御条件（タワー除去は
                    // TrySellTower のみで、同一コールスタック内で Close() される）だが、将来レーンが
                    // タワー破壊等の除去経路を追加した場合に silent stale UI を防ぐ。
                    Debug.LogError($"[HudPanel] RefreshDisplays: tower {towerActionPanel.OpenTowerId} " +
                        "not found for open action panel (panel/state desync)");
                    towerActionPanel.Close();
                }
            }
        }

        private void HandleInput()
        {
            // S-17: 一時停止中（Time.timeScale==0）は Ui/PausePanel のオーバーレイがクリックを処理する。
            // HudPanel は自前の InputReader/クリック判定を持つため、ガード無しだと同じクリックがオーバーレイ
            // 越しにビルドスポット選択へも同時に反応してしまう（表示専任の原則: 一時停止中は非破壊であるべき）。
            if (Time.timeScale <= GameConfig.Presentation.PausedTimeScale) return;

            // S-17 CR-CODE iter1 #1(major)/#2(minor) fix: 上の Time.timeScale ガードだけでは、PausePanel と
            // HudPanel の Update 実行順が未規定（両者とも DefaultExecutionOrder 無し）なせいで、
            // 「PausePanel.Update() が先に走り Resume() が同一フレーム内で timeScale を 1 に戻した直後、
            // 同じフレームの HudPanel.Update() が同じクリックを再処理してしまう」抜け道が残る
            // （Resume ボタンの矩形と空きビルドスポットの画面座標が重なる場合、オーバーレイ越しに
            // タワー選択パネルが誤って開き得た）。PausePanel.IsOpen（Update 順が逆で Resume 未処理の場合）と
            // LastResumeFrame（Update 順で Resume 済みの場合）の両方を見ることで、実行順に関係なく
            // Resume を発生させたクリックの二重処理を防ぐ。
            if (pausePanel != null && (pausePanel.IsOpen || pausePanel.LastResumeFrame == Time.frameCount)) return;

            // S-23: アップグレード/売却パネルはタワー選択メニューと同時に開かない（gdd 操作仕様: 空き
            // スポットと設置済みスポットは排他）。先に確認することで既存 towerSelectPanel の分岐を崩さない。
            if (towerActionPanel.IsOpen)
            {
                HandleActionPanelOpenInput();
                return;
            }

            if (towerSelectPanel.IsOpen)
            {
                HandlePanelOpenInput();
                return;
            }

            if (!inputReader.ClickPressedThisFrame || buildSpotController == null) return;

            Vector2 pointer = inputReader.PointerScreenPosition;

            // S-23 CR-CODE 後追い fix（バッチ検証区間で発覚。他ストーリー混入回避のため本 story とは別に記録）:
            // 旧実装は「空きスポットを全走査 → 見つからなければ設置済みタワーを全走査」の2段構えで、
            // それぞれが独立に GameConfig.Ui.BuildSpotClickPickRadiusPx（70px）の円内ヒットテストを行っていた。
            // GameConfig.Build.SpotPositions は同一列（同じX）に経路を挟んで2スポット（Z=+3m/-3m）を配置するが、
            // S-21 の固定俯瞰カメラ構図では同一列2スポットの画面距離が約53〜58pxしかなく、70px半径と重なる。
            // そのため「占有済みスポットをクリックしたはず」が、無条件に全空きスポットを先に走査する設計のせいで
            // 隣接する空きスポット（同列の反対側）を誤検出し、意図と異なるパネルが開く実バグがあった
            // （QA-PLAY 実行で TowerActionPanelPlayModeTests 6本が全滅して発覚）。修正: 占有有無を問わず
            // 画面上最近傍の1スポットのみを求め、そのスポットの占有状態で分岐先（TowerSelect/TowerAction）を
            // 決める一本化されたヒットテストに変更（両パネルとも「クリック地点に最も近いスポット」だけを
            // 対象にする直感的な挙動になり、ambiguity が構造的に解消される）。
            int hitSpotIndex = FindClickedSpot(pointer);
            if (hitSpotIndex < 0) return;

            if (buildSpotController.BuildSpots.IsOccupied(hitSpotIndex))
            {
                if (TryFindTowerBySpotIndex(hitSpotIndex, out TowerInstance tower))
                {
                    towerActionPanel.Open(tower, buildSpotController.Economy.Gold, buildSpotController.TowerDiscountRate);
                }
            }
            else
            {
                towerSelectPanel.Open(hitSpotIndex, buildSpotController.Economy.Gold);
            }
        }

        /// <summary>
        /// S-23: towerActionPanel が開いている間の入力処理（HandlePanelOpenInput と対になる towerSelectPanel
        /// 用と同型のパターン）。強化/売却ボタンの確定操作は既存の
        /// Components/BuildSpotController.TryUpgradeTower/TrySellTower を呼ぶだけで判定ロジックは持たない。
        /// </summary>
        private void HandleActionPanelOpenInput()
        {
            if (inputReader.CancelPressedThisFrame)
            {
                towerActionPanel.Close();
                return;
            }

            if (!inputReader.ClickPressedThisFrame) return;

            Vector2 pointer = inputReader.PointerScreenPosition;
            if (towerActionPanel.TryHandleClick(pointer, uiCamera, out TowerActionType action))
            {
                int towerId = towerActionPanel.OpenTowerId;
                if (action == TowerActionType.Upgrade)
                {
                    TowerUpgradeResult result = buildSpotController.TryUpgradeTower(towerId);
                    if (!result.Success)
                    {
                        // CR-CODE S-23 iter1 major fix: TowerUpgradeFailureReason.TowerNotFound は
                        // 「パネルが保持する OpenTowerId に対応するタワーが System 側に存在しない」＝
                        // パネル表示と BuildSpotSystem の状態が desync している回復不能条件で、事前非活性化
                        // では防げない（BuildSpotController の view/state desync ログ群と同種）。
                        // QA-PLAY のエラー0検査を素通りしないよう LogError で表面化する。
                        // AlreadyMaxLevel/InsufficientGold は強化ボタンの事前非活性化（Open()/Refresh() の
                        // isMaxLevel・upgradeAvailable 判定）でブロック済みで、開帳中に資金が減る経路も無い
                        // ため到達不能想定だが、防御的分岐として Warning に留める（TowerSelectPanel の設置
                        // 失敗ログと同型 — 黙殺しない）。
                        if (result.FailureReason == TowerUpgradeFailureReason.TowerNotFound)
                        {
                            Debug.LogError($"[HudPanel] TryUpgradeTower failed: tower {towerId} not found (panel/state desync)");
                        }
                        else
                        {
                            Debug.LogWarning($"[HudPanel] TryUpgradeTower failed unexpectedly after affordability pre-check: {result.FailureReason}");
                        }
                    }
                }
                else if (action == TowerActionType.Sell)
                {
                    TowerSellResult result = buildSpotController.TrySellTower(towerId);
                    if (!result.Success)
                    {
                        // CR-CODE S-23 iter1 major fix: TowerSellFailureReason は None/TowerNotFound の2値
                        // のみ（事前ブロック可能な失敗理由が存在しない）。売却ボタンはクリック可能な限り
                        // buildSpotController 側にタワーが実在するはずなので、ここへ到達するのは常にパネルの
                        // OpenTowerId と System 状態の desync。上の Upgrade 分岐と同じ規範で LogError にする。
                        Debug.LogError($"[HudPanel] TrySellTower failed: tower {towerId} not found (panel/state desync)");
                    }
                }
                towerActionPanel.Close();
                return;
            }

            if (!towerActionPanel.IsPointerInsidePanel(pointer, uiCamera))
            {
                towerActionPanel.Close();
            }
        }

        /// <summary>設置済みタワーを towerActionPanel.Open() へ渡すためのスナップショット取得。</summary>
        private bool TryFindTowerById(int towerId, out TowerInstance tower)
        {
            System.Collections.Generic.IReadOnlyList<TowerInstance> towers = buildSpotController.BuildSpots.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].Id == towerId)
                {
                    tower = towers[i];
                    return true;
                }
            }
            tower = default;
            return false;
        }

        /// <summary>設置済みタワーを SpotIndex から取得する（TryFindTowerById の spot 版）。</summary>
        private bool TryFindTowerBySpotIndex(int spotIndex, out TowerInstance tower)
        {
            System.Collections.Generic.IReadOnlyList<TowerInstance> towers = buildSpotController.BuildSpots.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].SpotIndex == spotIndex)
                {
                    tower = towers[i];
                    return true;
                }
            }
            tower = default;
            return false;
        }

        private void HandlePanelOpenInput()
        {
            if (inputReader.CancelPressedThisFrame)
            {
                towerSelectPanel.Close();
                return;
            }

            if (!inputReader.ClickPressedThisFrame) return;

            Vector2 pointer = inputReader.PointerScreenPosition;
            if (towerSelectPanel.TryHandleClick(pointer, uiCamera, out TowerType type))
            {
                int spotIndex = towerSelectPanel.OpenSpotIndex;
                PlacementResult result = buildSpotController.TryPlaceTower(spotIndex, type);
                if (!result.Success)
                {
                    // 資金不足はボタン側で事前ブロック済み（TowerSelectPanel.Open の affordable 判定）のため、
                    // ここに到達するのはスポットが同フレーム中に占有された等の想定外ケースのみ。
                    // QA-PLAY のエラー0検査を汚さない Warning で記録する（黙殺しない — CR-CODE 規約）。
                    Debug.LogWarning($"[HudPanel] TryPlaceTower failed unexpectedly after affordability pre-check: {result.FailureReason}");
                }
                towerSelectPanel.Close();
                return;
            }

            if (!towerSelectPanel.IsPointerInsidePanel(pointer, uiCamera))
            {
                towerSelectPanel.Close();
            }
        }

        /// <summary>
        /// クリック地点に最も近いビルドスポット（占有有無を問わない）を1つだけ返す。占有状態による分岐は
        /// 呼び出し元（HandleInput）が行う。占有スポットと空きスポットを別々に走査していた旧実装は、
        /// 同一列2スポットの画面距離が pick 半径と重なるケースで隣接スポットを誤検出しうる実バグを持っていた
        /// （HandleInput のコメント参照）。
        /// </summary>
        private int FindClickedSpot(Vector2 pointerScreen)
        {
            if (uiCamera == null) return -1;

            int best = -1;
            float bestDistSq = GameConfig.Ui.BuildSpotClickPickRadiusPx * GameConfig.Ui.BuildSpotClickPickRadiusPx;
            Vector3[] spots = GameConfig.Build.SpotPositions;
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 screenPoint = uiCamera.WorldToScreenPoint(spots[i]);
                if (screenPoint.z <= 0f) continue; // カメラの後方は無視

                float distSq = ((Vector2)screenPoint - pointerScreen).sqrMagnitude;
                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    best = i;
                }
            }
            return best;
        }

        private void SetGold(int gold) => goldText.text = $"資金: {gold}G";

        private void SetCoreHp(int current, int max) => coreHpText.text = $"コアHP: {current}/{max}";

        private void SetWave(int current, int total) => waveText.text = $"ウェーブ: {current}/{total}";

        private void BuildUi()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);
            scaler.matchWidthOrHeight = GameConfig.Ui.CanvasScalerMatchWidthOrHeight;
            UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, uiCamera);

            int row = 0;
            float NextY() => GameConfig.Ui.HudTopAnchorY - (row++ * GameConfig.Ui.HudRowStepY);

            goldText = CreateCornerText(canvasGo.transform, "GoldText", "資金: 0G", NextY());
            coreHpText = CreateCornerText(canvasGo.transform, "CoreHpText", $"コアHP: {GameConfig.Core.HpMax}/{GameConfig.Core.HpMax}", NextY());
            waveText = CreateCornerText(canvasGo.transform, "WaveText", $"ウェーブ: 1/{GameConfig.WaveComposition.Waves.Length}", NextY());

            var panelGo = new GameObject("TowerSelectPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            towerSelectPanel = panelGo.AddComponent<TowerSelectPanel>();
            towerSelectPanel.Initialize();

            var actionPanelGo = new GameObject("TowerActionPanel", typeof(RectTransform));
            actionPanelGo.transform.SetParent(canvasGo.transform, false);
            towerActionPanel = actionPanelGo.AddComponent<TowerActionPanel>();
            towerActionPanel.Initialize();
        }

        private static Text CreateCornerText(Transform parent, string name, string content, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 anchor = new Vector2(GameConfig.Ui.HudMarginXFraction, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                GameConfig.Ui.ReferenceWidth * GameConfig.Ui.HudTextBoxWidthFraction,
                GameConfig.Ui.HudFontSize * GameConfig.Ui.TextLineHeightFactor);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = GameConfig.Ui.HudFontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = GameConfig.Ui.TextPrimary;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
