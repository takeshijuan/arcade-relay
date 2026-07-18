// MenuScreen — Menu シーンの表示専任コンポーネント (ui-engineer, S-03). Builds the ScreenSpaceCamera
// Canvas + tab bar (はじめる/統計/アップグレード/設定) + per-tab content rows entirely in code at
// Awake (engine=unity 方針: uGUI をコード中心に構築). All sizes/colors/text content come from
// GameConfig.Ui (no magic numbers/hardcoded strings here).
//
// Display-only: this component renders values handed to it (RenderStats) and highlights whichever
// tab/item Components/MenuController tells it is active/focused (SetActiveTab/SetFocusIndex) — it
// holds no independent copy of SaveData or focus state beyond what was last rendered (display
// cache, not a second source of truth; conventions.md role「UI は表示専任・状態は game state が正」).
//
// Scope note (S-03 = 4タブの「枠」): アップグレード購入の決定操作配線は Components/MenuController が
// S-12 で担う（購入成功時に RenderStats を再呼出しして最新残高/Lv/次コストを反映する）。設定タブのスライダー
// A/D 調整＋即時セーブ（S-13）はまだ無い。ここでは項目の存在・表示・W/S フォーカス移動を扱う。
using ForgeGame.Systems.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    public sealed class MenuScreen : MonoBehaviour
    {
        /// <summary>S-13: baked by Editor/SceneWiring.WireMenuCrystalIcon (SerializedObject assignment,
        /// same pattern as Components/MenuController._mixer). Null is a legitimate degraded state (IMG-03
        /// not yet generated this session) — BuildStatsPanel still builds the icon Image GameObjects but
        /// leaves them transparent (CreateIcon) rather than erroring.</summary>
        [SerializeField] private Sprite _crystalIconSprite;

        /// <summary>S-30: baked by Editor/SceneWiring.WireMenuUiFrameKit — same null-degrades-gracefully
        /// pattern as `_crystalIconSprite` above (Ui/UiFrameKitVisuals skips creating the Image rather than
        /// erroring on a null sprite).</summary>
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _tabSelectedSprite;
        [SerializeField] private Sprite _tabUnselectedSprite;
        [SerializeField] private Sprite _ribbonSprite;
        [SerializeField] private Sprite _cornerSprite;

        public Canvas Canvas { get; private set; }

        /// <summary>4 tab bar labels, in GameConfig.Ui.MenuTabLabels order (Start/Stats/Upgrade/Settings).</summary>
        public Text[] TabLabelTexts { get; private set; }

        /// <summary>4 content panels, one per tab, in the same order as TabLabelTexts.</summary>
        public GameObject[] TabPanels { get; private set; }

        public Text StartItemText { get; private set; }
        public Text[] StatsTexts { get; private set; }
        public Text[] UpgradeItemTexts { get; private set; }
        public Text[] SettingsItemTexts { get; private set; }
        public Text InstructionsText { get; private set; }

        /// <summary>S-30: decorative Images built from IMG-05 — content-area panel background (shared
        /// across all 4 tabs), heading ribbon, two mirrored corner ornaments.</summary>
        public Image PanelImage { get; private set; }
        public Image RibbonImage { get; private set; }
        public Image[] CornerImages { get; private set; }

        /// <summary>S-30: one selection-frame Image per tab bar label (index-aligned with TabLabelTexts),
        /// swapped between UiTabSelectedSprite/UiTabUnselectedSprite by SetActiveTab.</summary>
        public Image[] TabFrameImages { get; private set; }

        /// <summary>S-30: one selection-frame Image per focusable item, index-aligned with the
        /// corresponding *ItemTexts array, swapped between UiTabSelectedSprite/UiTabUnselectedSprite by
        /// SetFocusIndex (Stats has none — it is the one tab with no focusable items, see ItemsForTab).</summary>
        public Image[] StartItemFocusFrames { get; private set; }
        public Image[] UpgradeItemFocusFrames { get; private set; }
        public Image[] SettingsItemFocusFrames { get; private set; }

        /// <summary>S-13: fill bars overlaid under each Settings row's text (parallel to
        /// SettingsItemTexts — index0=BGM/1=SFX), driven 0..1 by RenderStats.</summary>
        public Image[] SettingsBarFills { get; private set; }

        /// <summary>S-13 (conventions.md §8: 「クリスタル残高/コスト表示にはIMG-03アイコンを使用」) — the
        /// two 統計タブ rows that display a crystal count (累計獲得クリスタル・クリスタル残高).</summary>
        public Image StatTotalCrystalsIcon { get; private set; }
        public Image StatCrystalBalanceIcon { get; private set; }

        /// <summary>CR-CODE S-13 iteration 2, finding minor: single source of truth for Stats row order,
        /// set once by BuildStatsPanel. RenderStats looks up each row's index via this array (SetStatRow)
        /// instead of a separately-hardcoded StatsTexts[N] literal, so a future reorder/insert in
        /// BuildStatsPanel's `labels` cannot silently desync the two methods (the failure mode iteration 1
        /// fixed for the crystal icon rows only — this closes the same gap for RenderStats itself).</summary>
        private string[] _statsLabels;

        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Wiring] MenuScreen: no MainCamera-tagged camera in scene; canvas will silently render as Overlay and be invisible to QA RenderTexture capture");
            }
            Canvas.worldCamera = mainCamera;
            Canvas.planeDistance = GameConfig.Ui.CanvasPlaneDistance;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);

            canvasGo.AddComponent<GraphicRaycaster>();

            UiFactory.CreateBackground(canvasGo.transform, ownerName: nameof(MenuScreen));
            BuildDecoration(canvasGo.transform);
            BuildTabBar(canvasGo.transform);
            BuildTabPanels(canvasGo.transform);
        }

        /// <summary>S-30: content-area panel background (shared across all 4 tabs, sits behind whichever
        /// TabPanel is active), a small ribbon accent between the tab bar and panel top edge, and two
        /// mirrored corner flourishes — built before BuildTabBar/BuildTabPanels so everything else draws
        /// on top.</summary>
        private void BuildDecoration(Transform parent)
        {
            PanelImage = UiFrameKitVisuals.CreateSlicedImage(
                parent, "ContentPanel", _panelSprite,
                anchor: GameConfig.Ui.MenuPanelAnchor, anchoredPos: GameConfig.Ui.MenuPanelAnchoredPos,
                size: GameConfig.Ui.MenuPanelSize);

            RibbonImage = UiFrameKitVisuals.CreateSimpleImage(
                parent, "Ribbon", _ribbonSprite,
                anchor: GameConfig.Ui.MenuPanelAnchor, anchoredPos: GameConfig.Ui.MenuRibbonAnchoredPos,
                size: GameConfig.Ui.MenuRibbonSize);

            var cornerLeft = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerLeft", _cornerSprite,
                anchor: GameConfig.Ui.MenuPanelAnchor, anchoredPos: GameConfig.Ui.MenuCornerAnchoredPos,
                size: GameConfig.Ui.MenuCornerSize);

            Vector2 mirroredPos = new Vector2(-GameConfig.Ui.MenuCornerAnchoredPos.x, GameConfig.Ui.MenuCornerAnchoredPos.y);
            var cornerRight = UiFrameKitVisuals.CreateSimpleImage(
                parent, "CornerRight", _cornerSprite,
                anchor: GameConfig.Ui.MenuPanelAnchor, anchoredPos: mirroredPos,
                size: GameConfig.Ui.MenuCornerSize);
            if (cornerRight != null)
            {
                cornerRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            CornerImages = new[] { cornerLeft, cornerRight };
        }

        private void BuildTabBar(Transform parent)
        {
            string[] labels = GameConfig.Ui.MenuTabLabels;
            float[] anchorXs = GameConfig.Ui.MenuTabAnchorXs;
            TabLabelTexts = new Text[labels.Length];
            TabFrameImages = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                Vector2 anchor = new Vector2(anchorXs[i], GameConfig.Ui.MenuTabBarAnchorY);

                // S-30: selection frame behind the label, drawn first so the text renders on top.
                // Defaults to the unselected sprite — SetActiveTab (always called by
                // Components/MenuController right after construction) corrects this immediately.
                TabFrameImages[i] = UiFrameKitVisuals.CreateSlicedImage(
                    parent, $"TabFrame_{labels[i]}", _tabUnselectedSprite,
                    anchor: anchor, anchoredPos: Vector2.zero, size: GameConfig.Ui.MenuTabFrameSize);

                TabLabelTexts[i] = UiFactory.CreateText(
                    parent, $"Tab_{labels[i]}", labels[i],
                    GameConfig.Ui.MenuTabFontSize, GameConfig.Ui.ColorTextSecondary,
                    anchor: anchor, anchoredPos: Vector2.zero,
                    size: GameConfig.Ui.MenuTabLabelSize, ownerName: nameof(MenuScreen));
            }
        }

        private void BuildTabPanels(Transform parent)
        {
            TabPanels = new GameObject[GameConfig.Ui.MenuTabIndex.Count];

            TabPanels[GameConfig.Ui.MenuTabIndex.Start] = BuildStartPanel(parent);
            TabPanels[GameConfig.Ui.MenuTabIndex.Stats] = BuildStatsPanel(parent);
            TabPanels[GameConfig.Ui.MenuTabIndex.Upgrade] = BuildUpgradePanel(parent);
            TabPanels[GameConfig.Ui.MenuTabIndex.Settings] = BuildSettingsPanel(parent);
        }

        private GameObject BuildStartPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "Panel_Start");
            StartItemFocusFrames = new[] { CreateItemFrame(panel.transform, "StartItemFrame", 0) };
            StartItemText = CreateRow(panel.transform, "StartItem", GameConfig.Ui.MenuStartItemLabel, 0);
            return panel;
        }

        /// <summary>S-30: selection-frame Image behind a content row (mirrors CreateRow's own
        /// anchor+anchoredPos math so the frame lines up with the text it wraps). Defaults to the
        /// unselected sprite — SetFocusIndex (always called by Components/MenuController right after
        /// construction) corrects this immediately.</summary>
        private Image CreateItemFrame(Transform parent, string name, int rowIndex)
        {
            var anchoredPos = new Vector2(0f, -rowIndex * GameConfig.Ui.MenuRowSpacingPx);
            return UiFrameKitVisuals.CreateSlicedImage(
                parent, name, _tabUnselectedSprite,
                anchor: new Vector2(0.5f, GameConfig.Ui.MenuContentAnchorY), anchoredPos: anchoredPos,
                size: GameConfig.Ui.MenuItemFrameSize);
        }

        private GameObject BuildStatsPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "Panel_Stats");
            // 8 rows, static labels — values are filled in by RenderStats(SaveData) at runtime.
            // Row order matches gdd「アウトゲーム表示」必須項目の列挙順 (contract §11 / gates.md).
            string[] labels =
            {
                GameConfig.Ui.MenuStatHighScoreLabel,
                GameConfig.Ui.MenuStatBestSurvivalLabel,
                GameConfig.Ui.MenuStatBestWaveLabel,
                GameConfig.Ui.MenuStatTotalRunsLabel,
                GameConfig.Ui.MenuStatTotalKillsLabel,
                GameConfig.Ui.MenuStatTotalCrystalsLabel,
                GameConfig.Ui.MenuStatCrystalBalanceLabel,
                GameConfig.Ui.MenuStatUpgradeLevelsLabel,
            };
            _statsLabels = labels;
            StatsTexts = new Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                StatsTexts[i] = CreateRow(panel.transform, $"Stat_{labels[i]}", labels[i] + ": —", i, GameConfig.Ui.MenuStatsFontSize);
            }

            // S-13 (conventions.md §8): IMG-03 crystal icon next to the two crystal-count rows. CR-CODE
            // S-13 iteration 1, finding minor: row indices were previously hardcoded literals (5/6) that
            // duplicated the `labels` array's ordering above via a comment only — any future reorder/
            // insert in `labels` would silently misalign the icons with zero error (the PlayMode test only
            // asserts the icon sprite is non-null, not row alignment). Derived from `labels` itself instead
            // so the coupling is enforced by the compiler/runtime rather than a comment.
            StatTotalCrystalsIcon = CreateStatCrystalIcon(panel.transform, "Icon_TotalCrystals", labels, GameConfig.Ui.MenuStatTotalCrystalsLabel);
            StatCrystalBalanceIcon = CreateStatCrystalIcon(panel.transform, "Icon_CrystalBalance", labels, GameConfig.Ui.MenuStatCrystalBalanceLabel);
            return panel;
        }

        private Image CreateStatCrystalIcon(Transform parent, string name, string[] labels, string targetLabel)
        {
            int rowIndex = System.Array.IndexOf(labels, targetLabel);
            if (rowIndex < 0)
            {
                Debug.LogError($"[Wiring] MenuScreen.BuildStatsPanel: label '{targetLabel}' not found in Stats labels; skipping crystal icon '{name}'");
                return null;
            }
            var anchoredPos = new Vector2(GameConfig.Ui.MenuStatCrystalIconOffsetX, -rowIndex * GameConfig.Ui.MenuRowSpacingPx);
            return CreateIcon(
                parent, name, _crystalIconSprite,
                anchor: new Vector2(0.5f, GameConfig.Ui.MenuContentAnchorY), anchoredPos: anchoredPos,
                size: GameConfig.Ui.MenuStatCrystalIconSize);
        }

        private GameObject BuildUpgradePanel(Transform parent)
        {
            var panel = CreatePanel(parent, "Panel_Upgrade");
            string[] labels = GameConfig.Ui.MenuUpgradeLabels;
            UpgradeItemTexts = new Text[labels.Length];
            UpgradeItemFocusFrames = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                UpgradeItemFocusFrames[i] = CreateItemFrame(panel.transform, $"UpgradeFrame_{labels[i]}", i);
                UpgradeItemTexts[i] = CreateRow(panel.transform, $"Upgrade_{labels[i]}", labels[i], i);
            }
            return panel;
        }

        private GameObject BuildSettingsPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "Panel_Settings");
            string[] labels = GameConfig.Ui.MenuSettingsLabels;
            SettingsItemTexts = new Text[labels.Length];
            SettingsBarFills = new Image[labels.Length];
            SettingsItemFocusFrames = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                SettingsItemFocusFrames[i] = CreateItemFrame(panel.transform, $"SettingsFrame_{labels[i]}", i);
                SettingsItemTexts[i] = CreateRow(panel.transform, $"Settings_{labels[i]}", labels[i], i);

                // S-13: a visible slider fill bar under the row text (0..1, driven by RenderStats —
                // mirrors Ui/GameHud's HudHpBar/HudDashBar "background + Image.Type.Filled" pattern).
                var barAnchoredPos = new Vector2(0f, -i * GameConfig.Ui.MenuRowSpacingPx + GameConfig.Ui.MenuSettingsBarOffsetY);
                SettingsBarFills[i] = UiFactory.CreateBar(
                    panel.transform, $"SettingsBar_{labels[i]}",
                    GameConfig.Ui.HudBarBackgroundColor, GameConfig.Ui.ColorFocusHighlight,
                    anchor: new Vector2(0.5f, GameConfig.Ui.MenuContentAnchorY), anchoredPos: barAnchoredPos,
                    size: GameConfig.Ui.MenuSettingsBarSize, ownerName: nameof(MenuScreen));
            }

            InstructionsText = UiFactory.CreateText(
                panel.transform, "Instructions", GameConfig.Ui.MenuInstructionsText,
                GameConfig.Ui.MenuInstructionsFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.MenuInstructionsAnchorY), anchoredPos: Vector2.zero,
                size: GameConfig.Ui.MenuInstructionsSize, ownerName: nameof(MenuScreen));
            return panel;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>A stacked content row: anchored at MenuContentAnchorY, offset down by rowIndex * MenuRowSpacingPx.</summary>
        private static Text CreateRow(Transform parent, string name, string content, int rowIndex, int fontSize = -1)
        {
            int size = fontSize >= 0 ? fontSize : GameConfig.Ui.MenuItemFontSize;
            var anchoredPos = new Vector2(0f, -rowIndex * GameConfig.Ui.MenuRowSpacingPx);
            return UiFactory.CreateText(
                parent, name, content, size, GameConfig.Ui.ColorTextPrimary,
                anchor: new Vector2(0.5f, GameConfig.Ui.MenuContentAnchorY), anchoredPos: anchoredPos,
                size: GameConfig.Ui.MenuRowSize, ownerName: nameof(MenuScreen));
        }

        /// <summary>A single Image (IMG-03 crystal icon). <paramref name="sprite"/> may be null (not yet
        /// generated this session — Editor/SceneWiring.WireMenuCrystalIcon degrades gracefully); the Image
        /// is still created but made fully transparent so it never renders a visible placeholder box.</summary>
        private static Image CreateIcon(
            Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            if (sprite == null)
            {
                image.color = new Color(1f, 1f, 1f, 0f); // invisible placeholder — IMG-03 not wired yet
            }
            return image;
        }

        /// <summary>Show the panel for tabIndex, hide the other 3, and highlight the active tab label.</summary>
        public void SetActiveTab(int tabIndex)
        {
            if (TabPanels == null || tabIndex < 0 || tabIndex >= TabPanels.Length)
            {
                Debug.LogError($"[Wiring] MenuScreen.SetActiveTab called with out-of-range tabIndex={tabIndex}");
                return;
            }
            for (int i = 0; i < TabPanels.Length; i++)
            {
                TabPanels[i].SetActive(i == tabIndex);
                TabLabelTexts[i].color = UiFactory.ParseColor(
                    i == tabIndex ? GameConfig.Ui.ColorFocusHighlight : GameConfig.Ui.ColorTextSecondary, nameof(MenuScreen));

                // S-30: swap the IMG-05 selection frame alongside the existing text-color highlight (both
                // are display-only additions layered on the same unchanged tab-switch signal).
                if (TabFrameImages != null && TabFrameImages[i] != null)
                {
                    SetFrameSprite(TabFrameImages[i], i == tabIndex);
                }
            }
        }

        /// <summary>Highlight the focused item (by row index) within the given tab's item list; un-highlight the rest.</summary>
        public void SetFocusIndex(int tabIndex, int focusIndex)
        {
            if (tabIndex < 0 || tabIndex >= GameConfig.Ui.MenuTabIndex.Count)
            {
                Debug.LogError($"[Wiring] MenuScreen.SetFocusIndex called with out-of-range tabIndex={tabIndex}");
                return;
            }

            Text[] items = ItemsForTab(tabIndex);
            if (items == null || items.Length == 0)
            {
                if (tabIndex != GameConfig.Ui.MenuTabIndex.Stats)
                {
                    // Stats is the only tab legitimately without focusable items (gdd: 表示専用).
                    // Any other tab returning null/empty here is a wiring bug (missing item array).
                    Debug.LogError($"[Wiring] MenuScreen.SetFocusIndex: tabIndex={tabIndex} has no focusable items (expected only for Stats)");
                }
                return;
            }

            if (focusIndex < 0 || focusIndex >= items.Length)
            {
                Debug.LogError($"[Wiring] MenuScreen.SetFocusIndex called with out-of-range focusIndex={focusIndex} for tabIndex={tabIndex} (item count={items.Length})");
                return;
            }

            Image[] frames = FramesForTab(tabIndex);
            for (int i = 0; i < items.Length; i++)
            {
                items[i].color = UiFactory.ParseColor(
                    i == focusIndex ? GameConfig.Ui.ColorFocusHighlight : GameConfig.Ui.ColorTextPrimary, nameof(MenuScreen));

                // S-30: swap the IMG-05 selection frame alongside the existing text-color highlight.
                if (frames != null && i < frames.Length && frames[i] != null)
                {
                    SetFrameSprite(frames[i], i == focusIndex);
                }
            }
        }

        /// <summary>CR-CODE S-30 iteration 1, finding minor: swaps `frame`'s sprite to the
        /// selected/unselected IMG-05 frame sprite, but only if the target sprite is non-null. Unlike
        /// Ui/UiFrameKitVisuals.CreateImage (which can simply skip creating the Image when its sprite
        /// arg is null), SetActiveTab/SetFocusIndex mutate an *already-created* Image every time focus
        /// moves — assigning `frame.sprite = null` on a partially-degraded session (e.g. only
        /// _tabSelectedSprite failed to slice while _tabUnselectedSprite succeeded) would make uGUI
        /// render the frame's full Sliced rect as an opaque solid-color box (Image.color, default white)
        /// instead of leaving it blank, which reads as an unstyled flash rather than a graceful
        /// degradation. Skipping the assignment leaves the frame showing whichever sprite it last had
        /// (created with _tabUnselectedSprite — see BuildTabBar/CreateItemFrame) instead.</summary>
        private void SetFrameSprite(Image frame, bool selected)
        {
            Sprite target = selected ? _tabSelectedSprite : _tabUnselectedSprite;
            if (target == null)
            {
                Debug.LogWarning(
                    $"[Wiring] MenuScreen.SetFrameSprite: '{(selected ? nameof(_tabSelectedSprite) : nameof(_tabUnselectedSprite))}' " +
                    $"is null (IMG-05 not baked this session) — leaving '{frame.name}' showing its previous sprite instead of " +
                    "blanking to a solid-color box.");
                return;
            }
            frame.sprite = target;
        }

        private Text[] ItemsForTab(int tabIndex)
        {
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Start) return new[] { StartItemText };
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Upgrade) return UpgradeItemTexts;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Settings) return SettingsItemTexts;
            return null; // Stats タブ: フォーカス項目なし
        }

        /// <summary>S-30: mirrors ItemsForTab's shape/ordering, but for the per-item selection-frame Images
        /// (single source of truth for "which frame array corresponds to which tab" — same rationale as
        /// ItemsForTab itself, kept as a parallel method rather than merging the two so a future change to
        /// one array's construction can't accidentally desync from the other's index order without both
        /// call sites needing to agree on a combined return shape).</summary>
        private Image[] FramesForTab(int tabIndex)
        {
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Start) return StartItemFocusFrames;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Upgrade) return UpgradeItemFocusFrames;
            if (tabIndex == GameConfig.Ui.MenuTabIndex.Settings) return SettingsItemFocusFrames;
            return null; // Stats タブ: フォーカス項目なし
        }

        /// <summary>
        /// Render 統計/アップグレード/設定 タブの現在値を SaveData から反映する（表示専任 — SaveData
        /// の正本は Components/SessionHolder。ここでは受け取った値を文字列化するのみ）。
        /// </summary>
        public void RenderStats(SaveData save)
        {
            if (save == null)
            {
                Debug.LogError("[Wiring] MenuScreen.RenderStats called with null SaveData");
                return;
            }

            SetStatRow(GameConfig.Ui.MenuStatHighScoreLabel, $"{save.highScore}");
            SetStatRow(GameConfig.Ui.MenuStatBestSurvivalLabel, $"{save.bestSurvivalTimeSec:F1} 秒");
            SetStatRow(GameConfig.Ui.MenuStatBestWaveLabel, $"{save.bestWaveReached}");
            SetStatRow(GameConfig.Ui.MenuStatTotalRunsLabel, $"{save.totalRunsPlayed}");
            SetStatRow(GameConfig.Ui.MenuStatTotalKillsLabel, $"{save.totalKillCount}");
            SetStatRow(GameConfig.Ui.MenuStatTotalCrystalsLabel, $"{save.totalCrystalsEarned}");
            SetStatRow(GameConfig.Ui.MenuStatCrystalBalanceLabel, $"{save.crystalBalance}");
            SetStatRow(GameConfig.Ui.MenuStatUpgradeLevelsLabel,
                $"{GameConfig.Ui.MenuUpgradeAttackLabel}Lv{save.upgradeAttackLevel} / " +
                $"{GameConfig.Ui.MenuUpgradeMoveSpeedLabel}Lv{save.upgradeMoveSpeedLevel} / " +
                $"{GameConfig.Ui.MenuUpgradeMaxHpLabel}Lv{save.upgradeMaxHpLevel}");

            RenderUpgradeRow(0, GameConfig.Ui.MenuUpgradeAttackLabel, save.upgradeAttackLevel, GameConfig.Upgrade.AttackLevelMax);
            RenderUpgradeRow(1, GameConfig.Ui.MenuUpgradeMoveSpeedLabel, save.upgradeMoveSpeedLevel, GameConfig.Upgrade.MoveSpeedLevelMax);
            RenderUpgradeRow(2, GameConfig.Ui.MenuUpgradeMaxHpLabel, save.upgradeMaxHpLevel, GameConfig.Upgrade.MaxHpLevelMax);

            SettingsItemTexts[0].text = $"{GameConfig.Ui.MenuSettingsBgmLabel}: {save.bgmVolume:F1}";
            SettingsItemTexts[1].text = $"{GameConfig.Ui.MenuSettingsSfxLabel}: {save.sfxVolume:F1}";
            SettingsBarFills[GameConfig.Ui.MenuSettingsIndex.Bgm].fillAmount = Mathf.Clamp01(save.bgmVolume);
            SettingsBarFills[GameConfig.Ui.MenuSettingsIndex.Sfx].fillAmount = Mathf.Clamp01(save.sfxVolume);
        }

        /// <summary>Writes "<paramref name="label"/>: <paramref name="valueText"/>" into the Stats row whose
        /// position is looked up in `_statsLabels` (the same array BuildStatsPanel used to create StatsTexts
        /// and the crystal icon rows) rather than a hardcoded StatsTexts[N] index — see `_statsLabels` doc
        /// comment / CR-CODE S-13 iteration 2.</summary>
        private void SetStatRow(string label, string valueText)
        {
            int rowIndex = System.Array.IndexOf(_statsLabels, label);
            if (rowIndex < 0)
            {
                Debug.LogError($"[Wiring] MenuScreen.RenderStats: label '{label}' not found in Stats labels; skipping row");
                return;
            }
            StatsTexts[rowIndex].text = $"{label}: {valueText}";
        }

        private void RenderUpgradeRow(int index, string name, int level, int maxLevel)
        {
            string costText = level < maxLevel
                ? MetaProgression.UpgradeCost(level + 1).ToString()
                : GameConfig.Ui.MenuUpgradeMaxLevelText;
            UpgradeItemTexts[index].text = $"{name} Lv{level}/{maxLevel} (次:{costText})";
        }
    }
}
