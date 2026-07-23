// UiCanvasHelper.cs — UI 層の共通ヘルパ。
// tech-stack-unity.md 規約14: 全 UI Canvas は RenderMode.ScreenSpaceCamera 固定（QA の RenderTexture 撮影に写すため）。
// UI ストーリー（Title/Menu/HUD/Result）はこのヘルパで Canvas を構成し、Overlay を使わない。
using UnityEngine;

namespace ForgeGame.Ui
{
    public static class UiCanvasHelper
    {
        /// <summary>
        /// Canvas を ScreenSpaceCamera モードに構成し worldCamera を割り当てる（規約14 準拠）。
        /// PlayMode スモークチェック(Assert.AreEqual(ScreenSpaceCamera, canvas.renderMode)) を満たす。
        /// </summary>
        public static void ConfigureScreenSpaceCamera(Canvas canvas, Camera worldCamera, float planeDistance = 1f)
        {
            // CR-CODE iter1 #5 / iter2 #1: worldCamera==null だと Unity は ScreenSpaceCamera を黙って
            // Overlay 相当に縮退させ、規約14 の目的（QA の RenderTexture 撮影に UI を写す）が静かに壊れる。
            // このヘルパの全呼び出し元は常に Camera.main を渡す設計であり、null は配線バグ以外に発生し得ない
            // （正当な縮退ケースは無い）。QA-PLAY のエラー0検査を素通りさせないよう1回エラー記録する。
            if (worldCamera == null)
            {
                Debug.LogError("[UiCanvasHelper] worldCamera is null; Canvas '" + canvas.name +
                    "' will not render via ScreenSpaceCamera as intended (falls back to Overlay-like behavior). " +
                    "Check that the scene has a Camera tagged MainCamera.");
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = planeDistance;
        }
    }
}
