// UiFactory — shared code-first uGUI building blocks (ship-review dedup): the CreateText/ParseColor/
// CreateBackground/CreateBar bodies previously duplicated verbatim across Ui/TitleScreen, Ui/MenuScreen,
// Ui/ResultScreen and Ui/GameHud now live here once (companion to Ui/UiFrameKitVisuals, which already
// factored out the IMG-05 decorative-Image boilerplate the same way — Ui/MenuScreen.CreateBar's old
// "could be extracted later without behavior change" note is that extraction, now done). Signatures are
// the unified superset of the four copies plus an ownerName parameter (callers pass nameof(TheirClass))
// so the wiring-error log lines keep their original per-screen attribution byte for byte.
//
// Display-only: pure UI construction from GameConfig.Ui values handed in by callers — no game state,
// no Systems/ references, never a second source of truth.
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    internal static class UiFactory
    {
        /// <summary>A single anchored Text element (builtin LegacyRuntime font, centered, overflow on
        /// both axes) — the exact body every screen's private CreateText used to carry.</summary>
        public static Text CreateText(
            Transform parent, string name, string content, int fontSize, string colorHex,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, string ownerName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>(GameConfig.Ui.BuiltinFontName);
            text.fontSize = fontSize;
            text.color = ParseColor(colorHex, ownerName);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>GameConfig.Ui hex → Color, with the same white fallback + wiring LogError the four
        /// per-screen copies had — <paramref name="ownerName"/> keeps the message attributing the actual
        /// screen, not this shared helper.</summary>
        public static Color ParseColor(string hex, string ownerName)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
            Debug.LogError($"[Wiring] {ownerName} failed to parse GameConfig.Ui color '{hex}'; falling back to white");
            return Color.white;
        }

        /// <summary>The full-screen ColorBackground Image behind Title/Menu/Result (GameHud deliberately
        /// has none — HUD must never cover the play area, see Ui/GameHud's class header).</summary>
        public static void CreateBackground(Transform parent, string ownerName)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = ParseColor(GameConfig.Ui.ColorBackground, ownerName);
            image.raycastTarget = false;
        }

        /// <summary>A background Image plus a child Image.Type.Filled overlay (horizontal fill from the
        /// left), the returned fill Image being the one callers drive via fillAmount (0..1) — shared by
        /// Ui/GameHud's HP/Dash bars and Ui/MenuScreen's Settings volume bars.</summary>
        public static Image CreateBar(
            Transform parent, string name, string bgColorHex, string fillColorHex,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, string ownerName)
        {
            var bgGo = new GameObject(name + "Bg");
            bgGo.transform.SetParent(parent, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = anchor;
            bgRect.anchorMax = anchor;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = anchoredPos;
            bgRect.sizeDelta = size;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = ParseColor(bgColorHex, ownerName);
            bgImage.raycastTarget = false;

            var fillGo = new GameObject(name + "Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = ParseColor(fillColorHex, ownerName);
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            return fillImage;
        }
    }
}
