// BuildSpotView.cs — ビルドスポット1箇所のシーン表現（S-05）。
// 空き状態のプレースホルダ表示のみを持つ（判定は Systems/BuildSpotSystem）。左クリックでのタワー種別
// 選択メニュー（Ui/TowerSelectPanel）からの入力を受けて実際の設置操作を行うのは
// Components/BuildSpotController.TryPlaceTower（本ストーリー）。
// プレースホルダは薄い板状 Cube（設置面マーカー。design/assets.md に対応する専用 MDL は無く、常にこの
// プレースホルダのまま — assets-config.md プレースホルダ運用）。
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class BuildSpotView : MonoBehaviour
    {
        public int SpotIndex { get; private set; }

        /// <summary>スポーン直後に1回だけ呼ぶ。プレースホルダ見た目の生成とスポート座標の確定を行う。</summary>
        public void Initialize(int spotIndex, Vector3 position)
        {
            SpotIndex = spotIndex;
            transform.position = position;
            PlaceholderFactory.CreateGroundedPrimitive(
                PrimitiveType.Cube, transform, GameConfig.Presentation.BuildSpotPadHeightM,
                GameConfig.Placeholder.BuildSpotColor, "BuildSpotPlaceholderVisual");
        }
    }
}
