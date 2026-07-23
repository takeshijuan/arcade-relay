// PlaceholderFactory.cs — 3Dモデル未取込/未生成のあいだ使う単色プリミティブ生成ヘルパー（Components 層・Unity 依存・S-04）。
// MDL-01〜05 は全て Integrate 済み（Resources.Load 経由の実モデル取込・S-19）。各 View は Resources.Load 失敗時
// （想定外の縮退経路）にのみここへフォールバックする（CR-CODE S-19 iter2 minor #5 対応: 旧コメントは
// 「MDL-04 は常にここを使う」としていたが GameConfig.cs の記述と矛盾し、本ファイルのテスト契約
// （AssetIntegrationPlayModeTests.AssertUsesRealModel が Warbeast にも実モデル使用を要求）とも矛盾していた）。
// 規約10（tech-stack-unity.md）: Renderer は GetComponentInChildren<Renderer>() 前提で参照する側を組むこと。
// CR-CODE S-04 iter1 対応: ランタイム生成 Material は色ごとに静的キャッシュして使い回す（敵/コア1体ごとに
// new Material しない）。GameObject.Destroy は sharedMaterial を解放しないため、敵数分アロケートすると
// リスタート毎に累積リークする。色数（実質2色）分だけをアプリケーション寿命で確保し、以後は再利用する。
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Components
{
    internal static class PlaceholderFactory
    {
        // 既定プリミティブのメッシュ形状定数フォールバック（原点中心・高さ2 unit = Capsule/Cylinder の既定値）。
        // 通常は CreateGroundedPrimitive が MeshFilter.sharedMesh.bounds から実測するため使われない
        // （sharedMesh が無い異常系のみのフォールバック）。ゲームパラメータではなくメッシュジオメトリの
        // 不変値のため GameConfig ではなくここに定義する。
        private const float DefaultPrimitiveHeightUnits = 2f;

        // 色 → Material のキャッシュ。プレースホルダの色数は GameConfig.Placeholder で固定（実質数色）のため、
        // 敵/コア/タワーのスポーン数に比例してアロケートせず、初回生成分を使い回す。
        private static readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

        /// <summary>
        /// 底面が親のローカル原点に接地する PrimitiveType のプレースホルダを生成し、指定親へぶら下げる。
        /// 既定プリミティブは原点中心のため、メッシュの実測ローカル高さ（MeshFilter.sharedMesh.bounds.size.y —
        /// Capsule/Cylinder は2 unit、Cube/Sphere は1 unit）を基準に目標高さへ一様スケールし、
        /// Y方向へ半分だけオフセットして接地させる（S-05: Cube/Sphere 系プリミティブにも対応するため
        /// 固定2unit前提の計算から実測ベースへ変更 — 固定値のままだと Cube/Sphere で意図の半分の高さになる）。
        /// </summary>
        internal static GameObject CreateGroundedPrimitive(PrimitiveType type, Transform parent, float heightM, Color color, string name)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);

            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            bool hasValidMeshBounds = meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.bounds.size.y > 0f;
            float localHeightUnits = hasValidMeshBounds ? meshFilter.sharedMesh.bounds.size.y : DefaultPrimitiveHeightUnits;
            if (!hasValidMeshBounds)
            {
                // CR-CODE S-05 iter1 L指摘: sharedMesh 欠落/bounds高さ非正の異常系フォールバックは、本コミットが
                // 修正した「Cube/Sphere で意図の半分の高さ」バグを無言で再現しうる経路のため1回だけ明示ログする。
                Debug.LogWarning($"[PlaceholderFactory] \"{name}\" ({type}) の sharedMesh bounds が取得できず、既定高さ {DefaultPrimitiveHeightUnits} unit にフォールバックした。実測値と異なる可能性がある。");
            }

            float scale = heightM / localHeightUnits;
            go.transform.localScale = new Vector3(scale, scale, scale);
            go.transform.localPosition = new Vector3(0f, heightM * 0.5f, 0f);
            go.transform.localRotation = Quaternion.identity;

            Collider placeholderCollider = go.GetComponent<Collider>();
            if (placeholderCollider != null) Object.Destroy(placeholderCollider);

            // GameObject.CreatePrimitive は常に Renderer(MeshRenderer) を付与するため null チェックで
            // 無ログスキップしない（CR-CODE S-04 iter1 指摘: 到達不能ガードの failure-hiding を除去し、
            // 万一欠落した場合は NRE で明確に失敗させる）。
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(color);

            return go;
        }

        private static Material GetOrCreateMaterial(Color color)
        {
            if (materialCache.TryGetValue(color, out Material cached) && cached != null) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                // 配線破損は1回だけ明示ログ（規約12）。URP Lit が見つからない環境ではプレースホルダが
                // ピンク（InternalErrorShader相当）で表示されうることをここで明示する。
                Debug.LogError("[PlaceholderFactory] \"Universal Render Pipeline/Lit\" シェーダが見つからない。プレースホルダの見た目が欠落した状態になる可能性がある。");
                return null;
            }

            var material = new Material(shader) { color = color };
            materialCache[color] = material;
            return material;
        }
    }
}
