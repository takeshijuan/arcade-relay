// UiFrameKitVisuals — shared factory for the IMG-05 decorative Image GameObjects (9-slice panel
// background / tab-and-focus selection frame / heading ribbon / corner ornament), used identically by
// Ui/TitleScreen, Ui/MenuScreen, Ui/ResultScreen and Ui/GameHud (S-30). All four screens need the exact
// same sprite-driven building blocks, so this is factored out once instead of reimplementing the
// anchor/pivot/Image.Type boilerplate four times (the CreateText/ParseColor/CreateBackground/CreateBar
// counterparts, once duplicated per-file, were later unified into Ui/UiFactory by the ship-review dedup).
//
// Display-only: pure UI construction, no game state, no Systems/ references. Callers pass in the Sprite
// reference (baked into each screen's own [SerializeField] by Editor/SceneWiring — same pattern as
// Ui/MenuScreen._crystalIconSprite for IMG-03) — this class never loads assets itself.
using UnityEngine;
using UnityEngine.UI;

namespace ForgeGame.Ui
{
    internal static class UiFrameKitVisuals
    {
        /// <summary>A 9-sliced (Image.Type.Sliced) decorative Image — used for panel backgrounds and the
        /// tab/focus selection frames (both stretch cleanly via the sprite's authored border, set by
        /// Editor/AssetIntegration.ConfigureUiFrameKitSprites on the panel sprite's spriteBorder — tab
        /// frame sprites currently import without a custom border, in which case uGUI still renders them
        /// as Image.Type.Sliced with a zero border, i.e. equivalent to a plain stretch, which is expected
        /// for frame sprites since they wrap fixed-size tab/row rects rather than arbitrary content).
        /// Returns null (and logs) if <paramref name="sprite"/> is null, mirroring
        /// Ui/MenuScreen.CreateIcon's degrade-gracefully handling of a not-yet-baked sprite.</summary>
        public static Image CreateSlicedImage(
            Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            return CreateImage(parent, name, sprite, Image.Type.Sliced, anchor, anchoredPos, size, preserveAspect: false);
        }

        /// <summary>A non-sliced (Image.Type.Simple) decorative Image with its native aspect preserved —
        /// used for the ribbon/corner ornament accents, which are not meant to stretch.</summary>
        public static Image CreateSimpleImage(
            Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            return CreateImage(parent, name, sprite, Image.Type.Simple, anchor, anchoredPos, size, preserveAspect: true);
        }

        private static Image CreateImage(
            Transform parent, string name, Sprite sprite, Image.Type type,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, bool preserveAspect)
        {
            if (sprite == null)
            {
                Debug.LogWarning($"[Wiring] UiFrameKitVisuals.CreateImage: '{name}' sprite is null (IMG-05 not baked this session) — skipping decorative element.");
                return null;
            }

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
            image.type = type;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }
    }
}
