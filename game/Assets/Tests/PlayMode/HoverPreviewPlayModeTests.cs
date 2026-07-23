// HoverPreviewPlayModeTests.cs — S-18 acceptance:
// 「ビルドスポット/設置タワーへのマウスホバーでハイライトと射程プレビュー円（該当タワーの RANGE_M 半径）を
//  薄く表示する（入力は非破壊・状態変更なし）。PlayMode テストで、ポインタをスポット上へ移動するとプレビュー
//  表示 GameObject が有効化され、外すと無効化されることを検証できる。」
// GameHudPlayModeTests と同じ「シーンロードと入力擬似発行を同一コルーチンに収める」方式（batchmode の
// [UnitySetUp]/[UnityTest] 境界を跨ぐ既知の不具合を避けるため — docs/conventions.md）で Game シーンを
// ロードし、Ui/HudPanel.FindClickedSpot と同じカメラ投影方式でスポットの画面座標へポインタを動かす。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class HoverPreviewPlayModeTests : InputTestFixture
    {
        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Vector2 ScreenPointOfSpot(int spotIndex, Camera cam) =>
            cam.WorldToScreenPoint(GameConfig.Build.SpotPositions[spotIndex]);

        private static int FindEmptySpot(BuildSpotController build)
        {
            for (int i = 0; i < GameConfig.Build.SpotPositions.Length; i++)
            {
                if (!build.BuildSpots.IsOccupied(i)) return i;
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator HoverOverEmptyBuildSpot_ActivatesPreview_WithoutRangeCircle_AndDoesNotChangeState()
        {
            yield return LoadGame();

            var hover = Object.FindFirstObjectByType<HoverPreviewController>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            Assert.IsNotNull(hover, "HoverPreviewController コンポーネントが Game シーンに存在しない");
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");

            Assert.IsFalse(hover.PreviewGameObject.activeSelf, "初期状態でプレビューが有効になっている");

            int emptySpot = FindEmptySpot(build);
            Assert.GreaterOrEqual(emptySpot, 0, "テスト前提: Game シーン初期状態で空きビルドスポットが無い");
            int goldBefore = build.Economy.Gold;
            bool occupiedBefore = build.BuildSpots.IsOccupied(emptySpot);

            var mouse = InputSystem.AddDevice<Mouse>();
            Move(mouse.position, ScreenPointOfSpot(emptySpot, cam));
            yield return null;
            yield return null;

            Assert.IsTrue(hover.PreviewGameObject.activeSelf, "空きスポットへのホバーでプレビューが有効化されない");
            Assert.AreEqual(emptySpot, hover.HoveredSpotIndex);
            Assert.IsFalse(hover.RangeCircleGameObject.activeSelf, "タワー未設置スポットでは射程プレビュー円は非表示のはず");

            // acceptance: 入力は非破壊・状態変更なし。
            Assert.AreEqual(goldBefore, build.Economy.Gold, "ホバーのみで資金が変化した");
            Assert.AreEqual(occupiedBefore, build.BuildSpots.IsOccupied(emptySpot), "ホバーのみでスポット占有状態が変化した");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MovePointerAwayFromSpot_DeactivatesPreview()
        {
            yield return LoadGame();

            var hover = Object.FindFirstObjectByType<HoverPreviewController>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");

            int emptySpot = FindEmptySpot(build);
            Assert.GreaterOrEqual(emptySpot, 0, "テスト前提: Game シーン初期状態で空きビルドスポットが無い");
            var mouse = InputSystem.AddDevice<Mouse>();
            Move(mouse.position, ScreenPointOfSpot(emptySpot, cam));
            yield return null;
            yield return null;
            Assert.IsTrue(hover.PreviewGameObject.activeSelf, "前提: スポット上でプレビューが有効なこと");

            // 画面隅（どのスポットのピック半径にも入らない位置 — Ui/HudPanel.OutsideClick_ClosesPanel と同じ座標）。
            Move(mouse.position, new Vector2(5f, 5f));
            yield return null;
            yield return null;

            Assert.IsFalse(hover.PreviewGameObject.activeSelf, "スポットから外れてもプレビューが無効化されない");
            Assert.AreEqual(-1, hover.HoveredSpotIndex);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HoverOverPlacedTower_ActivatesPreview_WithRangeCircleSizedToTowerRangeM()
        {
            yield return LoadGame();

            var hover = Object.FindFirstObjectByType<HoverPreviewController>();
            var build = Object.FindFirstObjectByType<BuildSpotController>();
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");

            int spot = FindEmptySpot(build);
            Assert.GreaterOrEqual(spot, 0, "テスト前提: Game シーン初期状態で空きビルドスポットが無い");
            Assert.IsTrue(build.TryPlaceTower(spot, TowerType.BastionCannon).Success, "テスト前提のタワー設置に失敗");

            var mouse = InputSystem.AddDevice<Mouse>();
            Move(mouse.position, ScreenPointOfSpot(spot, cam));
            yield return null;
            yield return null;

            Assert.IsTrue(hover.PreviewGameObject.activeSelf, "設置済みタワーへのホバーでプレビューが有効化されない");
            Assert.IsTrue(hover.RangeCircleGameObject.activeSelf, "設置済みタワーへのホバーで射程プレビュー円が有効化されない");

            float expectedDiameter = GameConfig.BastionCannon.RangeM * 2f;
            Assert.AreEqual(expectedDiameter, hover.RangeCircleGameObject.transform.localScale.x, 1e-4f,
                "射程プレビュー円の直径が Bastion Cannon の RANGE_M と一致しない");
            Assert.AreEqual(expectedDiameter, hover.RangeCircleGameObject.transform.localScale.z, 1e-4f);

            // CR-CODE S-18 iter1 minor指摘: 直径(X/Z)のみの検証では Y の薄板厚が上書きされて既定 Cylinder
            // の高さ(2unit)に戻ってしまう回帰（acceptance「薄く表示」違反）を CI が見逃す。薄板厚
            // （HoverPreviewThicknessM/defaultCylinderHeightUnits と同じ計算）を Y スケールで検証する。
            const float defaultCylinderHeightUnits = 2f;
            float expectedThicknessScale = GameConfig.Presentation.HoverPreviewThicknessM / defaultCylinderHeightUnits;
            Assert.AreEqual(expectedThicknessScale, hover.RangeCircleGameObject.transform.localScale.y, 1e-4f,
                "射程プレビュー円の厚み(Y)が薄板厚のままになっていない（薄い円盤ではなく厚い円柱になっている疑い）");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
