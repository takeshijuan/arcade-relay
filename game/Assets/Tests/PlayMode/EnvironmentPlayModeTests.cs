// EnvironmentPlayModeTests.cs — S-21（環境ビジュアル本仕上げ）の acceptance 検証（PlayMode）。
// (a) カメラ前方向とコア方向の Vector3.Dot>0.2、(b) 地面/経路 Renderer に null/ピンク(InternalErrorShader)無し、
// (c) スクリーンショット証跡を qa/evidence へ保存（平均輝度 0.02 超の機械判定は QA-PLAY 側の magick 検証で行う
// — tech-stack-unity.md「QA-PLAY の実行方法」節）。カメラは移動・ズーム入力を持たない固定俯瞰であることも
// EnvironmentView が Awake で一度だけ構成する契約を Camera.main の実値と GameConfig.Camera の一致で確認する。
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame;

namespace ForgeGame.Tests.PlayMode
{
    public class EnvironmentPlayModeTests
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const float PositionTolerance = 0.01f;
        private const float AngleToleranceDeg = 0.1f;

        private static string EvidenceDir
        {
            get
            {
                string gameDir = Directory.GetParent(Application.dataPath).FullName;
                string repoRoot = Directory.GetParent(gameDir).FullName;
                string dir = Path.Combine(repoRoot, "qa", "evidence");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void CaptureSceneScreenshot(string fileName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");

            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(EvidenceDir, fileName), png);
                Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        private static void AssertRendererSane(string gameObjectName)
        {
            GameObject go = GameObject.Find(gameObjectName);
            Assert.IsNotNull(go, $"'{gameObjectName}' が Game シーンに見つからない");

            Renderer renderer = go.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, $"'{gameObjectName}' に Renderer が無い");

            Material mat = renderer.sharedMaterial;
            Assert.IsNotNull(mat, $"'{gameObjectName}' の material が null");
            Assert.IsNotNull(mat.shader, $"'{gameObjectName}' の material に shader が無い");
            Assert.AreNotEqual("Hidden/InternalErrorShader", mat.shader.name,
                $"'{gameObjectName}' の material がピンク(InternalErrorShader)＝マテリアル欠落");
        }

        [UnityTest]
        public IEnumerator Game_FixedOverheadCamera_FacesCore_GroundAndPathAreSane()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game シーンに Camera.main が無い");

            // EnvironmentView.Awake が GameConfig.Camera の構図をそのまま適用していること
            // （移動・ズーム入力を持たない固定俯瞰 — 実値が config と一致することで担保する）。
            Assert.Less(Vector3.Distance(cam.transform.position, GameConfig.Camera.Position), PositionTolerance,
                "Main Camera の位置が GameConfig.Camera.Position と一致しない");
            Assert.Less(Quaternion.Angle(cam.transform.rotation, Quaternion.Euler(GameConfig.Camera.EulerAngles)), AngleToleranceDeg,
                "Main Camera の回転が GameConfig.Camera.EulerAngles と一致しない");
            Assert.AreEqual(GameConfig.Camera.FieldOfViewDeg, cam.fieldOfView, 0.01f);
            Assert.IsFalse(cam.orthographic);

            // (a) カメラ前方向とコア方向の Dot > 0.2
            Vector3 toCore = (GameConfig.Path.EndPoint - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, toCore);
            Assert.Greater(dot, 0.2f, $"カメラがコア方向を向いていない (dot={dot})");

            // (b) 地面/経路 Renderer に null/ピンク無し
            AssertRendererSane("GroundPlane");
            AssertRendererSane("PathStrip");

            // (c) スクリーンショット証跡（平均輝度の機械判定は QA-PLAY の magick identify で実施）
            CaptureSceneScreenshot("s21-environment-composition.png");
        }

        [UnityTest]
        public IEnumerator Game_TileTextures_LoadOrFallbackAreConsistent()
        {
            // CR-CODE iter1 major #1: EnvironmentView.CreateTileMaterial の未取込フォールバックが
            // Resources.Load 成否と機械的に対応していることを検証する（テクスチャロード成功時に
            // mainTexture が実際に割り当たり Repeat タイリングになっているか／失敗時に単色フォールバック
            // 色と一致しているか）。IMG-01/02 の取込状態に関わらずどちらの分岐でも通る。
            //
            // CR-CODE iter2 minor #2: このテストは実装と同一の Resources.Load・同一キーで分岐するため、
            // 「AssetKeys キー不一致・取込失敗」というタウトロジー的に検出できないクラスの欠陥は依然
            // 目視（LogWarning）頼み。機械検証で閉じられるのは「ロードした tex がそのまま採用されているか
            // （キー取り違えの検知）」と「タイリング表示に必要な mainTextureScale が widthX/widthZ 相当に
            // 設定されているか」の2点で、この2点を追加する。
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            float groundWidthX = GameConfig.Wave.PathLengthM + GameConfig.Environment.GroundMarginM * 2f;
            float groundWidthZ = GameConfig.Environment.GroundWidthZFullM;
            AssertTileMaterialConsistency(
                "GroundPlane", GameConfig.AssetKeys.TileGrass, GameConfig.Placeholder.GroundColor,
                groundWidthX, groundWidthZ);

            float pathWidthX = GameConfig.Wave.PathLengthM;
            float pathWidthZ = GameConfig.Environment.PathStripWidthZM;
            AssertTileMaterialConsistency(
                "PathStrip", GameConfig.AssetKeys.TileDirtPath, GameConfig.Placeholder.PathColor,
                pathWidthX, pathWidthZ);
        }

        private static void AssertTileMaterialConsistency(
            string gameObjectName, string textureKey, Color fallbackColor, float widthX, float widthZ)
        {
            GameObject go = GameObject.Find(gameObjectName);
            Assert.IsNotNull(go, $"'{gameObjectName}' が Game シーンに見つからない");

            Renderer renderer = go.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, $"'{gameObjectName}' に Renderer が無い");
            Material mat = renderer.sharedMaterial;
            Assert.IsNotNull(mat, $"'{gameObjectName}' の material が null");

            Texture2D tex = Resources.Load<Texture2D>(textureKey);
            if (tex != null)
            {
                Assert.IsNotNull(mat.mainTexture,
                    $"'{gameObjectName}' はテクスチャロード成功 (key={textureKey}) にも関わらず mainTexture が null");
                Assert.IsInstanceOf<Texture2D>(mat.mainTexture,
                    $"'{gameObjectName}' の mainTexture が Texture2D ではない");
                Assert.AreEqual(TextureWrapMode.Repeat, ((Texture2D)mat.mainTexture).wrapMode,
                    $"'{gameObjectName}' のテクスチャ wrapMode が Repeat でない（タイリング表示にならない）");
                // CR-CODE iter2 minor #2 (a): キー取り違え検知（ロードした tex がそのまま採用されているか）。
                Assert.AreEqual(tex, mat.mainTexture,
                    $"'{gameObjectName}' の mainTexture がキー '{textureKey}' でロードした Texture2D と異なる（キー取り違えの疑い）");
                // CR-CODE iter2 minor #2 (b): mainTextureScale 未設定（既定1,1）だと1枚引き伸ばしになり
                // acceptance の「タイリング表示」が成立しない。CreateTileMaterial の実際の計算式と一致することを検証する。
                Vector2 expectedScale = new Vector2(
                    widthX / GameConfig.Environment.TileWorldSizeM,
                    widthZ / GameConfig.Environment.TileWorldSizeM);
                Assert.AreEqual(expectedScale.x, mat.mainTextureScale.x, 0.001f,
                    $"'{gameObjectName}' の mainTextureScale.x がタイリング計算値と一致しない（引き伸ばし表示の疑い）");
                Assert.AreEqual(expectedScale.y, mat.mainTextureScale.y, 0.001f,
                    $"'{gameObjectName}' の mainTextureScale.y がタイリング計算値と一致しない（引き伸ばし表示の疑い）");
            }
            else
            {
                Assert.AreEqual(fallbackColor, mat.color,
                    $"'{gameObjectName}' は未取込/未生成 (key={textureKey}) のはずだがフォールバック色が一致しない");
            }
        }

        [UnityTest]
        public IEnumerator Game_Camera_HasNoMovementOrZoomInput()
        {
            // 固定俯瞰カメラは移動・ズーム入力を持たない（gdd 操作仕様）: 数フレーム経過してもカメラ
            // Transform/FOV が Awake 直後から変化しないことを確認する（入力配線が存在しないことの間接検証）。
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Camera cam = Camera.main;
            Vector3 initialPosition = cam.transform.position;
            Quaternion initialRotation = cam.transform.rotation;
            float initialFov = cam.fieldOfView;

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(initialPosition, cam.transform.position);
            Assert.AreEqual(initialRotation, cam.transform.rotation);
            Assert.AreEqual(initialFov, cam.fieldOfView);
        }
    }
}
