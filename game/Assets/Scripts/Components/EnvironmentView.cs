// EnvironmentView.cs — Game シーンの地形タイル取込・URPライト・固定俯瞰カメラの本配置（S-21）。
// Components 層（Unity依存・薄い配線）。構図・数値は GameConfig.Camera / GameConfig.Lighting /
// GameConfig.Environment（本 story が唯一の所有者）を参照するだけで、判定・分岐ロジックは持たない
// （規約3: Components は薄く。純粋ロジックを要する分岐が無いため Systems/ への追加は不要）。
// IMG-01(TileGrass)/IMG-02(TileDirtPath) が未取込/未生成のあいだは単色マテリアルへフォールバックする
// （GeneratedModelFactory.TryCreateGroundedModel と同じ「未生成→フォールバック」契約）。ただし単色
// フォールバックは成果物とほぼ同見た目のため無言にはせず、フォールバック発生時に1回だけ明示 LogWarning
// する（CR-CODE iter1 major #1。規約12: 正当縮退はヘッダ文書化+LogWarning。配線破損〔Main Camera/
// Directional Light 欠落・シェーダ欠落〕は既存どおり LogError 対象）。
// CR-CODE iter2 major #1 既知リスク: この LogWarning は Unity Test Framework の
// LogAssert.NoUnexpectedReceived()（未 Expect のログ全件で失敗。LogType 不問）と両立しない場合がある。
// IMG-01/02 が未取込のあいだ Game シーンをロードして同 API を呼ぶテスト（例: ui-engineer 所有の
// GameHudPlayModeTests）はこの LogWarning が原因で false-fail する可能性がある。解消は
// (a) IMG-01/02 の Integrate をバッチ検証より前に完了させる、または (b) 該当テストへ
// LogAssert.Expect(LogType.Warning, ...) を追加するのいずれかで、どちらも本 story（S-21・
// EnvironmentView 単体）の担当範囲外（ワークフロー順序 or 他レーン所有テストファイル）のため
// state/reviews/s-21.md に既知リスクとして記録し、レーン合流後のバッチ検証区間の実施者へ委ねる。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。Awake 時に一度だけ地面/経路/カメラ/ライトを構成する。</summary>
    public sealed class EnvironmentView : MonoBehaviour
    {
        private const string DefaultLitShaderName = "Universal Render Pipeline/Lit";

        // Unity 既定 Plane プリミティブの一辺サイズ（メッシュジオメトリの不変値。ゲームパラメータではないため
        // GameConfig ではなくここに定義する — PlaceholderFactory.DefaultPrimitiveHeightUnits と同じ思想）。
        private const float DefaultPrimitivePlaneSizeUnits = 10f;

        // CreateTileMaterial が new Material() で生成したインスタンスを保持し、OnDestroy で解放する
        // （CR-CODE iter1 minor #5: シーンアンロードは GameObject のみ破棄するため無保持だとリークする）。
        private readonly List<Material> createdMaterials = new List<Material>();

        private void Awake()
        {
            ConfigureCamera();
            ConfigureLighting();
            BuildGroundPlane();
            BuildPathStrip();
        }

        private void OnDestroy()
        {
            foreach (Material material in createdMaterials)
            {
                if (material != null) Destroy(material);
            }
            createdMaterials.Clear();
        }

        private void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                // 規約12: 配線破損は1回だけ明示ログ。Main Camera はシーン scaffold が必ず用意する前提のため
                // null は構造的な事故（シーン破損/タグ剥がれ等）。
                Debug.LogError("[EnvironmentView] Camera.main が見つからない。固定俯瞰カメラの構図を適用できない。");
                return;
            }

            cam.transform.position = GameConfig.Camera.Position;
            cam.transform.eulerAngles = GameConfig.Camera.EulerAngles;
            cam.orthographic = false;
            cam.fieldOfView = GameConfig.Camera.FieldOfViewDeg;
        }

        private void ConfigureLighting()
        {
            // CR-CODE iter1 minor #3: FindFirstObjectByType<Light>() は型を絞らないため、後続 story が
            // シーンに Light を追加した場合に取得順不定で無関係の Light へ Directional 設定を書き込みかねない。
            // LightType.Directional で明示的に絞り込む（現状シーンには Directional 1灯のみで挙動は変わらない）。
            Light sun = FindDirectionalLight();
            if (sun == null)
            {
                // 規約12: Directional Light もシーン scaffold が必ず用意する前提。
                Debug.LogError("[EnvironmentView] Directional Light が見つからない。盤面の明るさを設定できない。");
            }
            else
            {
                sun.intensity = GameConfig.Lighting.DirectionalIntensityLux;
                sun.color = GameConfig.Lighting.DirectionalColor;
                sun.transform.eulerAngles = GameConfig.Lighting.DirectionalEulerAngles;
            }

            // URP は RenderSettings の Ambient 設定を Environment Lighting のソースとして参照する。
            // Flat モード = 単色環境光で全方位を底上げし、盤面が暗転しない明るさを保証する。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = GameConfig.Lighting.AmbientColor;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional) return light;
            }
            return null;
        }

        private void BuildGroundPlane()
        {
            float widthX = GameConfig.Wave.PathLengthM + GameConfig.Environment.GroundMarginM * 2f;
            float widthZ = GameConfig.Environment.GroundWidthZFullM;
            CreateTiledPlane(
                "GroundPlane", widthX, widthZ, Vector3.zero,
                GameConfig.AssetKeys.TileGrass, GameConfig.Placeholder.GroundColor);
        }

        private void BuildPathStrip()
        {
            float widthX = GameConfig.Wave.PathLengthM;
            float widthZ = GameConfig.Environment.PathStripWidthZM;
            var position = new Vector3(0f, GameConfig.Environment.PathHeightOffsetM, 0f);
            CreateTiledPlane(
                "PathStrip", widthX, widthZ, position,
                GameConfig.AssetKeys.TileDirtPath, GameConfig.Placeholder.PathColor);
        }

        private GameObject CreateTiledPlane(string name, float widthX, float widthZ, Vector3 localPosition, string textureKey, Color fallbackColor)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(transform, false);
            plane.transform.localPosition = localPosition;
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(
                widthX / DefaultPrimitivePlaneSizeUnits, 1f, widthZ / DefaultPrimitivePlaneSizeUnits);

            Collider planeCollider = plane.GetComponent<Collider>();
            if (planeCollider != null) Destroy(planeCollider);

            // GameObject.CreatePrimitive は常に Renderer(MeshRenderer) を付与するため null チェックで
            // 無ログスキップしない（PlaceholderFactory と同じ思想: 万一欠落した場合は NRE で明確に失敗させる）。
            Material material = CreateTileMaterial(textureKey, fallbackColor, widthX, widthZ);
            if (material != null) createdMaterials.Add(material);
            plane.GetComponent<Renderer>().sharedMaterial = material;

            return plane;
        }

        private static Material CreateTileMaterial(string textureKey, Color fallbackColor, float widthX, float widthZ)
        {
            Shader shader = Shader.Find(DefaultLitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[EnvironmentView] \"{DefaultLitShaderName}\" シェーダが見つからない。地形の見た目が欠落した状態になる可能性がある。");
                return null;
            }

            var material = new Material(shader);
            Texture2D tex = Resources.Load<Texture2D>(textureKey);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                material.mainTexture = tex;
                material.mainTextureScale = new Vector2(
                    widthX / GameConfig.Environment.TileWorldSizeM,
                    widthZ / GameConfig.Environment.TileWorldSizeM);
            }
            else
            {
                // 未取込/未生成（design/assets.md status=planned 等）は想定内の経路のためフォールバックする
                // （色そのものは GeneratedModelFactory.TryCreateGroundedModel と同じ「未生成→フォールバック」
                // 契約）。ただし単色フォールバックは成果物とほぼ同見た目のため、AssetKeys パス不一致・
                // 取込失敗による回復不能な障害を「未生成の想定内縮退」と機械的に区別できるよう、
                // CR-CODE iter1 major #1 対応として1回だけ明示ログする（規約12: 正当縮退はヘッダ文書化+
                // LogWarning）。CR-CODE iter2 major #1: 「Unity Test Framework は Warning でテスト失敗
                // しないため QA エラー0検査と両立」は既定挙動（Error のみ fail）にしか成立せず、
                // tech-stack-unity.md「QA-PLAY の実行方法」観点2が正本手段に指定する
                // LogAssert.NoUnexpectedReceived() は LogType 不問で未 Expect の全ログを fail させるため、
                // Game シーンをロードして同 API を呼ぶ他テストと衝突しうる（クラス冒頭コメント参照）。
                Debug.LogWarning(
                    $"[EnvironmentView] テクスチャ \"{textureKey}\" を Resources.Load できず単色フォールバックを使用する " +
                    "(未生成/未取込なら想定内。AssetKeys のキー不一致や取込失敗の場合はここで検知できないため " +
                    "design/assets.md の status と Assets/Resources/Generated/textures/ の実在を確認すること)。");
                material.color = fallbackColor;
            }

            return material;
        }
    }
}
