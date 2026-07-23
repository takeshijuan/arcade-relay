// EnvironmentConfigTests.cs — S-21（環境ビジュアル本仕上げ）の GameConfig 定数の純粋検証（EditMode）。
// シーンロード不要な数値・幾何の健全性のみをここで確認する。実際のシーン内カメラ/レンダラー検証は
// PlayMode（Assets/Tests/PlayMode/EnvironmentPlayModeTests.cs）で行う（規約8: EditMode/PlayMode 配置分離）。
using NUnit.Framework;
using UnityEngine;
using ForgeGame;

namespace ForgeGame.Tests.EditMode
{
    public class EnvironmentConfigTests
    {
        [Test]
        public void Camera_EulerAngles_LooksTowardCoreDirection()
        {
            // GameConfig.Camera.EulerAngles は X軸回りの俯角のみ（Y/Z回転なし）で構成される固定俯瞰カメラ。
            var rotation = Quaternion.Euler(GameConfig.Camera.EulerAngles);
            Vector3 forward = rotation * Vector3.forward;

            Vector3 toCore = (GameConfig.Path.EndPoint - GameConfig.Camera.Position).normalized;
            float dot = Vector3.Dot(forward, toCore);

            Assert.Greater(dot, 0.2f, $"カメラ前方向がコア方向を向いていない可能性 (dot={dot})");
        }

        [Test]
        public void Camera_Composition_CoversFullPathAndBuildSpots()
        {
            // 画角・高さ・後退量が正の値であること（未設定/符号ミスの検知）。
            Assert.Greater(GameConfig.Camera.FieldOfViewDeg, 0f);
            Assert.Less(GameConfig.Camera.FieldOfViewDeg, 180f);
            Assert.Greater(GameConfig.Camera.HeightM, 0f);
            Assert.Greater(GameConfig.Camera.BackOffsetM, 0f);

            // 距離ベースの粗い視野半幅チェック: 経路全長(PATH_LENGTH_M)の半分が水平画角に収まるだけの
            // 余裕があること（アスペクト比16:9を仮定した保守的な下限チェック）。
            float distance = Mathf.Sqrt(
                GameConfig.Camera.HeightM * GameConfig.Camera.HeightM +
                GameConfig.Camera.BackOffsetM * GameConfig.Camera.BackOffsetM);
            float verticalHalfRad = GameConfig.Camera.FieldOfViewDeg * 0.5f * Mathf.Deg2Rad;
            const float assumedAspect = 16f / 9f;
            float horizontalHalfRad = Mathf.Atan(Mathf.Tan(verticalHalfRad) * assumedAspect);
            float visibleHalfWidthM = distance * Mathf.Tan(horizontalHalfRad);

            float requiredHalfWidthM = GameConfig.Wave.PathLengthM * 0.5f;
            Assert.Greater(visibleHalfWidthM, requiredHalfWidthM,
                $"カメラ画角が PATH_LENGTH_M 全長を収めるには狭すぎる (visible={visibleHalfWidthM}, required={requiredHalfWidthM})");

            // CR-CODE iter1 minor #2: 上記は水平方向の粗い距離チェックのみで、acceptance の
            // 「全 NUM_BUILD_SPOTS・コア(EndPoint) を1画面に収める」を実際には検証していなかった。
            // 実際の Camera の投影行列（WorldToViewportPoint）で全ビルドスポット・コアがビューポート
            // ([0,1]×[0,1]・カメラ前方)に収まることを機械的に検証する。
            var cameraObject = new GameObject("EditModeTestCamera_CoverageCheck");
            try
            {
                Camera cam = cameraObject.AddComponent<Camera>();
                cam.transform.position = GameConfig.Camera.Position;
                cam.transform.eulerAngles = GameConfig.Camera.EulerAngles;
                cam.fieldOfView = GameConfig.Camera.FieldOfViewDeg;
                cam.aspect = assumedAspect;

                // CR-CODE iter2 minor #3: EndPoint（コア）のみの検証ではカメラ位置x・yaw変更時に
                // 経路始点（StartPoint）が画面外に出る回帰を検知できない。対称構図の現状値では
                // 冗長だが、将来のカメラ調整に対する回帰検知として StartPoint も明示的に検証する。
                AssertInViewport(cam, GameConfig.Path.StartPoint, "経路始点(StartPoint)");
                AssertInViewport(cam, GameConfig.Path.EndPoint, "コア(EndPoint)");
                foreach (Vector3 spot in GameConfig.Build.SpotPositions)
                {
                    AssertInViewport(cam, spot, $"ビルドスポット {spot}");
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void AssertInViewport(Camera cam, Vector3 worldPoint, string label)
        {
            Vector3 viewportPoint = cam.WorldToViewportPoint(worldPoint);
            Assert.Greater(viewportPoint.z, 0f, $"{label} がカメラの後方にある (viewport={viewportPoint})");
            Assert.IsTrue(
                viewportPoint.x >= 0f && viewportPoint.x <= 1f && viewportPoint.y >= 0f && viewportPoint.y <= 1f,
                $"{label} がビューポート範囲外で1画面に収まっていない (viewport={viewportPoint})");
        }

        [Test]
        public void Lighting_IsBrightEnoughToAvoidBlackout()
        {
            Assert.Greater(GameConfig.Lighting.DirectionalIntensityLux, 0f);
            float ambientMean = (GameConfig.Lighting.AmbientColor.r + GameConfig.Lighting.AmbientColor.g + GameConfig.Lighting.AmbientColor.b) / 3f;
            Assert.Greater(ambientMean, 0.02f, "環境光が暗すぎる（盤面が暗転する可能性）");
        }

        [Test]
        public void Environment_PathStripNarrowerThanBuildSpotOffset()
        {
            // 経路帯とビルドスポットが重ならないこと（PathStripWidthZM の半分 < SpotOffsetZ）。
            Assert.Less(GameConfig.Environment.PathStripWidthZM * 0.5f, GameConfig.Build.SpotOffsetZ);
        }
    }
}
