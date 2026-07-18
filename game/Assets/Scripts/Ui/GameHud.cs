// GameHud — Game シーンの HUD 表示専任コンポーネント (ui-engineer, S-10). Builds the ScreenSpaceCamera
// Canvas + Text/Image UI entirely in code at Awake (engine=unity 方針: uGUI をコード中心に構築), mirroring
// Ui/TitleScreen and Ui/MenuScreen's code-first pattern. Unlike those two screens, this Canvas has NO
// full-screen background panel — the HUD must never cover the play area (role: 「プレイ領域を覆う演出は
// 禁止」). All sizes/colors/text content come from GameConfig.Ui (no magic numbers/hardcoded strings here).
//
// Display-only: every Set* method below only formats and applies values handed in by
// Components/GameHudController, which reads the authoritative state (Components/HealthComponent,
// Components/PlayerController, Components/WaveSpawner, Components/RunStatsTracker) — this component
// never reads game state itself and holds no duplicate copy of it beyond the last values passed in
// (display cache, not a second source of truth; conventions.md role「UI は表示専任・状態は game state が正」).
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    public sealed class GameHud : MonoBehaviour
    {
        /// <summary>S-30: baked by Editor/SceneWiring.WireGameUiFrameKit — same null-degrades-gracefully
        /// pattern as Ui/TitleScreen's IMG-05 fields. HUD only uses the panel sprite (small backgrounds
        /// behind each stat group) — no ribbon/corner here, this is a functional overlay, not a "headline"
        /// screen (class header rule: HUD must never cover the play area).</summary>
        [SerializeField] private Sprite _panelSprite;

        public Canvas Canvas { get; private set; }

        public Text HpText { get; private set; }
        public Image HpBarFill { get; private set; }
        public Text DashText { get; private set; }
        public Image DashBarFill { get; private set; }
        public Text WaveText { get; private set; }
        public Text ScoreText { get; private set; }

        /// <summary>S-30: decorative panel backgrounds behind each corner stat group (HP/Dash/Wave/Score, in
        /// that order) — built before the corresponding text/bar so they draw behind them.</summary>
        public Image[] StatPanelImages { get; private set; }

        /// <summary>Full-screen death dissolve overlay (S-16: gdd 決定「画面全体のディゾルブ/フェードVFX」).
        /// Unlike every other element on this Canvas, this Image DOES cover the play area by design — the
        /// class header's 「プレイ領域を覆う演出は禁止」rule governs live-play feedback; the death sequence
        /// is a distinct post-death moment (input already locked by Components/HealthComponent) where a
        /// full-screen fade is the intended read, not a violation of it. Starts fully transparent
        /// (alpha=0) and is only ever driven upward by Components/GameHudController via SetDeathDissolve.</summary>
        public Image DeathDissolveOverlay { get; private set; }

        // Ship-review AUTO-FIX: the Set* methods below are driven every frame by
        // Components/GameHudController, and every string interpolation/concat in them allocates — a
        // steady per-frame GC load for values that actually change at most a few times per second.
        // Cache the last applied value per method and early-return when unchanged, so Text.text writes
        // (and their string allocations) only happen on real change. int.MinValue sentinels guarantee
        // the first call always applies (the display cache role from the class header is unchanged —
        // these mirror the last values passed in, never a second source of truth).
        private int _lastHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        private int _lastDashDisplayTenths = int.MinValue;
        private int _lastWave = int.MinValue;
        private int _lastScore = int.MinValue;

        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("GameHudCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Wiring] GameHud: no MainCamera-tagged camera in scene; canvas will silently render as Overlay and be invisible to QA RenderTexture capture");
            }
            Canvas.worldCamera = mainCamera;
            Canvas.planeDistance = GameConfig.Ui.CanvasPlaneDistance;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.Ui.ReferenceWidth, GameConfig.Ui.ReferenceHeight);

            canvasGo.AddComponent<GraphicRaycaster>();

            BuildDecoration(canvasGo.transform);

            HpText = UiFactory.CreateText(
                canvasGo.transform, "HpText", $"{GameConfig.Ui.HudHpLabel} 0/0",
                GameConfig.Ui.HudHpFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: GameConfig.Ui.HudHpTextAnchor, anchoredPos: GameConfig.Ui.HudHpTextAnchoredPos,
                size: GameConfig.Ui.HudHpTextSize, ownerName: nameof(GameHud));
            HpBarFill = UiFactory.CreateBar(
                canvasGo.transform, "HpBar", GameConfig.Ui.HudBarBackgroundColor, GameConfig.Ui.HudHpBarFillColor,
                anchor: GameConfig.Ui.HudHpBarAnchor, anchoredPos: GameConfig.Ui.HudHpBarAnchoredPos,
                size: GameConfig.Ui.HudHpBarSize, ownerName: nameof(GameHud));

            DashText = UiFactory.CreateText(
                canvasGo.transform, "DashText", GameConfig.Ui.HudDashReadyText,
                GameConfig.Ui.HudDashFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: GameConfig.Ui.HudDashTextAnchor, anchoredPos: GameConfig.Ui.HudDashTextAnchoredPos,
                size: GameConfig.Ui.HudDashTextSize, ownerName: nameof(GameHud));
            DashBarFill = UiFactory.CreateBar(
                canvasGo.transform, "DashBar", GameConfig.Ui.HudBarBackgroundColor, GameConfig.Ui.HudDashBarFillColor,
                anchor: GameConfig.Ui.HudDashBarAnchor, anchoredPos: GameConfig.Ui.HudDashBarAnchoredPos,
                size: GameConfig.Ui.HudDashBarSize, ownerName: nameof(GameHud));

            WaveText = UiFactory.CreateText(
                canvasGo.transform, "WaveText", GameConfig.Ui.HudWaveLabelPrefix + "1",
                GameConfig.Ui.HudWaveFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: GameConfig.Ui.HudWaveAnchor, anchoredPos: GameConfig.Ui.HudWaveAnchoredPos,
                size: GameConfig.Ui.HudWaveSize, ownerName: nameof(GameHud));

            ScoreText = UiFactory.CreateText(
                canvasGo.transform, "ScoreText", GameConfig.Ui.HudScoreLabelPrefix + "0",
                GameConfig.Ui.HudScoreFontSize, GameConfig.Ui.ColorTextPrimary,
                anchor: GameConfig.Ui.HudScoreAnchor, anchoredPos: GameConfig.Ui.HudScoreAnchoredPos,
                size: GameConfig.Ui.HudScoreSize, ownerName: nameof(GameHud));

            DeathDissolveOverlay = CreateFullScreenOverlay(canvasGo.transform, "DeathDissolveOverlay", GameConfig.Ui.ColorDeathDissolve);
        }

        /// <summary>S-30: small panel backgrounds behind each of the 4 stat groups (HP top-left, Dash
        /// bottom-left, Wave top-center, Score top-right), built before any text/bar so they draw behind
        /// them — tightly sized around the existing groups, never covering the play area.</summary>
        private void BuildDecoration(Transform parent)
        {
            Image hpPanel = UiFrameKitVisuals.CreateSlicedImage(
                parent, "HpPanel", _panelSprite,
                anchor: GameConfig.Ui.HudHpPanelAnchor, anchoredPos: GameConfig.Ui.HudHpPanelAnchoredPos,
                size: GameConfig.Ui.HudHpPanelSize);
            Image dashPanel = UiFrameKitVisuals.CreateSlicedImage(
                parent, "DashPanel", _panelSprite,
                anchor: GameConfig.Ui.HudDashPanelAnchor, anchoredPos: GameConfig.Ui.HudDashPanelAnchoredPos,
                size: GameConfig.Ui.HudDashPanelSize);
            Image wavePanel = UiFrameKitVisuals.CreateSlicedImage(
                parent, "WavePanel", _panelSprite,
                anchor: GameConfig.Ui.HudWavePanelAnchor, anchoredPos: GameConfig.Ui.HudWavePanelAnchoredPos,
                size: GameConfig.Ui.HudWavePanelSize);
            Image scorePanel = UiFrameKitVisuals.CreateSlicedImage(
                parent, "ScorePanel", _panelSprite,
                anchor: GameConfig.Ui.HudScorePanelAnchor, anchoredPos: GameConfig.Ui.HudScorePanelAnchoredPos,
                size: GameConfig.Ui.HudScorePanelSize);
            StatPanelImages = new[] { hpPanel, dashPanel, wavePanel, scorePanel };
        }

        /// <summary>A stretch-anchored (0,0)-(1,1) Image covering the whole Canvas, last in sibling order
        /// so it draws above every other HUD element. Starts fully transparent — S-16's
        /// Components/GameHudController is the only caller that ever raises its alpha.</summary>
        private static Image CreateFullScreenOverlay(Transform parent, string name, string colorHex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            Color color = UiFactory.ParseColor(colorHex, nameof(GameHud));
            color.a = 0f;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Reflects current/effectiveMax HP (gdd: 「現在HP（effectiveMaxHp 基準）」) as both text
        /// and a filled bar. maxHp&lt;=0 is a wiring-error guard (should never happen — EffectiveMaxHp is
        /// always >=1 by construction) that clamps the bar rather than dividing by zero.</summary>
        public void SetHp(int currentHp, int maxHp)
        {
            if (currentHp == _lastHp && maxHp == _lastMaxHp)
            {
                return;
            }
            _lastHp = currentHp;
            _lastMaxHp = maxHp;
            HpText.text = $"{GameConfig.Ui.HudHpLabel} {currentHp}/{maxHp}";
            HpBarFill.fillAmount = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        }

        /// <summary>Reflects dash cooldown remaining (gdd: 「ダッシュクールダウン残」). remainingSec&lt;=0
        /// shows the ready state; cooldownMaxSec&lt;=0 is a wiring-error guard, mirrors SetHp.</summary>
        public void SetDashCooldown(float remainingSec, float cooldownMaxSec)
        {
            // Bar first, every call, from the RAW value: fillAmount is a float setter (no alloc), so
            // quantizing it bought nothing and cost smoothness — DASH_COOLDOWN=1.2s in 0.1s steps was
            // a visibly chunky 12-step fill (adversarial F-2). Only the TEXT keeps the 0.1s dirty
            // check (×10 is the structural inverse of the F1 format's decimal place, not a tunable
            // gdd parameter): sub-0.1s deltas can never change the rendered string, so allocating one
            // per frame was pure GC waste. 0 doubles as the ready-state key.
            DashBarFill.fillAmount = remainingSec <= 0f
                ? 1f
                : (cooldownMaxSec > 0f ? Mathf.Clamp01(1f - remainingSec / cooldownMaxSec) : 0f);
            int displayTenths = remainingSec <= 0f ? 0 : Mathf.CeilToInt(remainingSec * 10f);
            if (displayTenths == _lastDashDisplayTenths)
            {
                return;
            }
            _lastDashDisplayTenths = displayTenths;
            if (remainingSec <= 0f)
            {
                DashText.text = GameConfig.Ui.HudDashReadyText;
                return;
            }
            DashText.text = $"{GameConfig.Ui.HudDashLabel} {remainingSec:F1}s";
        }

        /// <summary>Reflects currentWave (gdd: 「現在ウェーブ番号」).</summary>
        public void SetWave(int wave)
        {
            if (wave == _lastWave)
            {
                return;
            }
            _lastWave = wave;
            WaveText.text = GameConfig.Ui.HudWaveLabelPrefix + wave;
        }

        /// <summary>Applies the wave-switch pulse scale (S-15: gdd 決定「HUDのウェーブ数値表示を0.3秒かけて
        /// 1.0→1.3→1.0倍にスケールさせる」) to WaveText's own RectTransform. Display-only — the caller
        /// (Components/GameHudController) owns the timer and computes the value via
        /// Systems/WavePulseSystem.ComputeScale; this method just applies it uniformly on all three axes
        /// so the number visibly grows/shrinks in place (pivot stays centered — UiFactory.CreateText
        /// already sets pivot=(0.5,0.5)).</summary>
        public void SetWaveScale(float scale)
        {
            WaveText.transform.localScale = Vector3.one * scale;
        }

        /// <summary>Reflects the live current score (gdd: 「現在スコア」= 生存時間・撃破数・回収クリスタル数を
        /// 集計式に適用した値、Systems/ScoreSystem.ComputeFinalScore と同一式).</summary>
        public void SetScore(int score)
        {
            if (score == _lastScore)
            {
                return;
            }
            _lastScore = score;
            ScoreText.text = GameConfig.Ui.HudScoreLabelPrefix + score;
        }

        /// <summary>Reflects the death sequence's screen dissolve progress (S-16: gdd 決定「画面全体の
        /// ディゾルブ/フェードVFX」). alpha is expected in [0,1] (Components/GameHudController derives it
        /// from Systems/HeroFxSystem.ComputeDissolveAlpha, already clamped) — Clamp01 here is a defensive
        /// guard only, not a second source of truth for the value.</summary>
        public void SetDeathDissolve(float alpha)
        {
            Color color = DeathDissolveOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            DeathDissolveOverlay.color = color;
        }
    }
}
