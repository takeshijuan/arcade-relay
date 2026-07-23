// TowerActionPanel.cs — 設置済みタワーのアップグレード/売却パネル（S-23）。ロジックは持たない表示+ヒットテスト専任。
// gdd「操作仕様」表: 「左クリック（設置済みタワー）: アップグレード/売却パネルを表示。資金が次Lvコスト以上なら
// アップグレード実行。Lv3到達済みはアップグレード選択肢を非表示」に対応する。CR-CODE S-10/S-11 で継続
// エスカレーションされていた [BLOCKER]（プレイヤー入力から Components/BuildSpotController.TryUpgradeTower/
// TrySellTower へ到達する経路が存在しない — state/reviews/s-10.md, s-11.md 参照）を解消する。
// 開閉・入力ルーティング（クリック/右クリック検出そのもの）は Ui/HudPanel が InputReader 経由で行い、
// このクラスは Open/Close/ヒットテストの API のみを提供する（表示専任: 資金・タワー状態はここに複製せず
// Open() の引数として毎回受け取るだけ — Ui/TowerSelectPanel と同じ設計）。
// クリック判定は uGUI Button/EventSystem を使わず RectTransformUtility の矩形ヒットテストで行う
// （tech-stack-unity.md 規約4。TowerSelectPanel と同じ「非破壊入力」パターンを踏襲）。
// 全数値/色は GameConfig.Ui（マジックナンバー禁止 — 規約1）。既存の TowerSelect* 定数は変更せず、
// 本パネル専用の TowerAction* 定数を新設して使う（story acceptance の制約）。
//
// 現在ダメージ/次Lvダメージ/次Lv実効コストの算出式は Systems/TowerCombatSystem.DamageForLevel・
// Systems/TowerUpgradeSystem.BaseUpgradeCost と同一だが、いずれも private のため（役割宣言: UI は
// Systems 層のロジックを変更しない・gameplay-engineer 領域のファイルを編集しない）、本クラスは
// GameConfig の同じ定数（DamageLv1/2/3・UpgradeLv2Cost/UpgradeLv3Cost）から直接導出する
// （Ui/TowerSelectPanel が GameConfig.BastionCannon.Cost 等を直接読むのと同じパターン。数値の単一情報源
// は GameConfig のまま揺らがないため、参照コード形が2箇所にあっても値のドリフトは起きない）。
// CR-CODE S-23 iter2 #1(minor) 見送り: この式複製自体は iter1 #4 から継続する既知の重複（レーン規律上
// gameplay-engineer 領域の Systems/TowerCombatSystem・Systems/TowerUpgradeSystem を UI レーンから編集
// できないための構造的制約）。指摘は「将来 Systems 側の式が変わった場合に本パネルの表示が黙って
// desync し得る」というリスクで、現時点での値ドリフトは無い（上記のとおり同一 GameConfig 定数参照）。
// 対応案（DamageForLevel/BaseUpgradeCost を Systems 側に public query メソッドとして公開し本クラスの
// 複製を削除）は Systems ファイルの編集を要するため、gameplay-engineer 領域の post-merge follow-up
// story として切り出すことを推奨し、本 iteration では見送る。
// 実効コスト・売却返還額の丸めは Systems/EconomySystem の既存 public static メソッド
// （ComputeEffectiveCost/ComputeSellRefund）をそのまま呼び出す（ロジック変更ではなく既存 API の利用）。
using UnityEngine;
using UnityEngine.UI;
using ForgeGame.Systems;

namespace ForgeGame.Ui
{
    /// <summary>TryHandleClick が返すアクション種別。</summary>
    public enum TowerActionType
    {
        None = 0,
        Upgrade = 1,
        Sell = 2,
    }

    /// <summary>Game シーンの HudPanel が生成する子 GameObject に1つだけ付与する。</summary>
    public sealed class TowerActionPanel : MonoBehaviour
    {
        private struct ButtonRefs
        {
            public RectTransform Rect;
            public Image Background;
            public Text Label;
        }

        private RectTransform panelRect;
        private Text headerText;
        private Text currentInfoText;
        private Text nextInfoText;
        private ButtonRefs upgradeButton;
        private ButtonRefs sellButton;
        private bool upgradeAvailable;

        public bool IsOpen { get; private set; }

        /// <summary>Open() 時に指定されたタワー id。閉じている間は -1。</summary>
        public int OpenTowerId { get; private set; } = -1;

        public RectTransform PanelRect => panelRect;
        public RectTransform UpgradeButtonRect => upgradeButton.Rect;
        public RectTransform SellButtonRect => sellButton.Rect;
        public Text HeaderText => headerText;
        public Text CurrentInfoText => currentInfoText;
        public Text NextInfoText => nextInfoText;
        public Text UpgradeLabel => upgradeButton.Label;
        public Text SellLabel => sellButton.Label;
        public bool IsUpgradeAvailable => upgradeAvailable;

        /// <summary>HudPanel.BuildUi() が AddComponent 直後に1回だけ呼ぶ。UI 生成のみ行い非表示にする。</summary>
        public void Initialize()
        {
            panelRect = (RectTransform)transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(GameConfig.Ui.TowerActionPanelWidth, GameConfig.Ui.TowerActionPanelHeight);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = GameConfig.Ui.PanelBackground;

            headerText = CreateInfoText(transform, "Header", GameConfig.Ui.TowerActionHeaderAnchorY);
            currentInfoText = CreateInfoText(transform, "CurrentInfo", GameConfig.Ui.TowerActionCurrentInfoAnchorY);
            nextInfoText = CreateInfoText(transform, "NextInfo", GameConfig.Ui.TowerActionNextInfoAnchorY);

            upgradeButton = CreateButton(transform, "Button_Upgrade", -GameConfig.Ui.TowerActionButtonOffsetX);
            sellButton = CreateButton(transform, "Button_Sell", GameConfig.Ui.TowerActionButtonOffsetX);

            gameObject.SetActive(false);
        }

        /// <summary>設置済みタワー左クリックで開く。gdd「操作仕様」表の表示内容を全て反映する。</summary>
        public void Open(TowerInstance tower, int currentGold, float discountRate)
        {
            OpenTowerId = tower.Id;
            IsOpen = true;
            gameObject.SetActive(true);
            Refresh(tower, currentGold, discountRate);
        }

        /// <summary>
        /// CR-CODE S-23 iter1 minor fix: Open() 時点の資金スナップショット固定だと、開帳中の撃破報酬による
        /// 資金増加が強化ボタンの非活性表示に反映されない（閉じて開き直すまで復帰しない）。HudPanel.Update()
        /// から開帳中は毎フレーム呼び直して表示を最新の資金/タワー状態に追従させる（表示専任: 受け取った値を
        /// そのまま反映するだけでロジックは持たない）。OpenTowerId/IsOpen/SetActive は変更しない
        /// （開閉状態はここでは触らない — 呼び出し元 Open()/HudPanel が管理する）。
        /// </summary>
        public void Refresh(TowerInstance tower, int currentGold, float discountRate)
        {
            headerText.text = $"{TowerDisplayName(tower.Type)}  Lv {tower.Level}/{GameConfig.Tower.MaxLevel}";
            currentInfoText.text = $"現在ダメージ: {DamageForLevel(tower.Type, tower.Level)}";

            bool isMaxLevel = tower.Level >= GameConfig.Tower.MaxLevel;
            if (isMaxLevel)
            {
                nextInfoText.text = "最大強化済み";
                upgradeAvailable = false;
                upgradeButton.Label.text = "最大強化済み";
            }
            else
            {
                int nextDamage = DamageForLevel(tower.Type, tower.Level + 1);
                int baseCost = BaseUpgradeCost(tower.Type, tower.Level);
                int effectiveCost = EconomySystem.ComputeEffectiveCost(baseCost, discountRate);
                nextInfoText.text = $"次Lv: ダメージ {nextDamage} / 強化費 {effectiveCost}G";

                upgradeAvailable = currentGold >= effectiveCost;
                upgradeButton.Label.text = upgradeAvailable
                    ? $"強化\n{effectiveCost}G"
                    : $"強化\n{effectiveCost}G\n(資金不足)";
            }
            ApplyButtonAlpha(upgradeButton, upgradeAvailable);

            int refund = EconomySystem.ComputeSellRefund(tower.InvestedGold, GameConfig.Build.SellRefundRate);
            sellButton.Label.text = $"売却\n+{refund}G";
            ApplyButtonAlpha(sellButton, true);
        }

        /// <summary>パネル外クリック/右クリックで呼ぶ（選択解除）。</summary>
        public void Close()
        {
            IsOpen = false;
            OpenTowerId = -1;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// screenPos が有効なボタン上ならそのアクション種別を返す。強化ボタンは資金不足/Lv3到達時に
        /// 非活性のためクリックを受け付けない（acceptance「ボタンを事前に非活性表示する」）。
        /// </summary>
        public bool TryHandleClick(Vector2 screenPos, Camera cam, out TowerActionType action)
        {
            if (upgradeAvailable && RectTransformUtility.RectangleContainsScreenPoint(upgradeButton.Rect, screenPos, cam))
            {
                action = TowerActionType.Upgrade;
                return true;
            }
            if (RectTransformUtility.RectangleContainsScreenPoint(sellButton.Rect, screenPos, cam))
            {
                action = TowerActionType.Sell;
                return true;
            }
            action = TowerActionType.None;
            return false;
        }

        public bool IsPointerInsidePanel(Vector2 screenPos, Camera cam) =>
            panelRect != null && RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, cam);

        private static void ApplyButtonAlpha(ButtonRefs button, bool available)
        {
            Color bg = GameConfig.Ui.AccentTeal;
            bg.a = available ? 1f : GameConfig.Ui.TowerSelectInsufficientAlpha;
            button.Background.color = bg;
        }

        private static string TowerDisplayName(TowerType type) =>
            type == TowerType.BastionCannon ? "Bastion Cannon" : "Arc Emitter";

        /// <summary>
        /// Systems/TowerCombatSystem.DamageForLevel と同一式（同じ GameConfig 定数を参照するため値のドリフトは
        /// 無い）。private のため直接呼べず、UI 表示専用にこの読み取りを複製する（クラス冒頭コメント参照）。
        /// </summary>
        private static int DamageForLevel(TowerType type, int level)
        {
            (int lv1, int lv2, int lv3) = type == TowerType.BastionCannon
                ? (GameConfig.BastionCannon.DamageLv1, GameConfig.BastionCannon.DamageLv2, GameConfig.BastionCannon.DamageLv3)
                : (GameConfig.ArcEmitter.DamageLv1, GameConfig.ArcEmitter.DamageLv2, GameConfig.ArcEmitter.DamageLv3);

            switch (level)
            {
                case 1: return lv1;
                case 2: return lv2;
                case 3: return lv3;
                default: throw new System.ArgumentOutOfRangeException(nameof(level), level, "TowerInstance.Level は 1〜3 の範囲である必要がある。");
            }
        }

        /// <summary>
        /// Systems/TowerUpgradeSystem.BaseUpgradeCost と同一式（クラス冒頭コメント参照）。
        /// currentLevel は Open() 呼び出し前に isMaxLevel==false を確認済みのため常に 1〜2。
        /// </summary>
        private static int BaseUpgradeCost(TowerType type, int currentLevel)
        {
            switch (currentLevel)
            {
                case 1:
                    return type == TowerType.BastionCannon
                        ? GameConfig.BastionCannon.UpgradeLv2Cost
                        : GameConfig.ArcEmitter.UpgradeLv2Cost;
                case 2:
                    return type == TowerType.BastionCannon
                        ? GameConfig.BastionCannon.UpgradeLv3Cost
                        : GameConfig.ArcEmitter.UpgradeLv3Cost;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(currentLevel), currentLevel,
                        "currentLevel は 1〜2 の範囲である必要がある（Lv3 は打止め）。");
            }
        }

        private static Text CreateInfoText(Transform parent, string name, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                GameConfig.Ui.TowerActionPanelWidth * GameConfig.Ui.TowerActionInfoWidthFraction,
                GameConfig.Ui.BodyFontSize * GameConfig.Ui.TextLineHeightFactor);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = GameConfig.Ui.BodyFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GameConfig.Ui.TextPrimary;
            return text;
        }

        private static ButtonRefs CreateButton(Transform parent, string name, float xOffset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, GameConfig.Ui.TowerActionButtonAnchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);
            rect.sizeDelta = new Vector2(GameConfig.Ui.TowerActionButtonWidth, GameConfig.Ui.TowerActionButtonHeight);
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
