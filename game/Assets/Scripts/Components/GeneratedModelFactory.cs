// GeneratedModelFactory.cs — 取込済み3Dモデル（MDL-*）を実体化するヘルパー（Components 層・Unity 依存）。
// GameConfig.AssetKeys の文字列キーで Assets/Resources/Generated/models/ 配下の glTFast 取込済み GameObject
// を Resources.Load するため、AssetKeys に無い/未生成（Resources.Load が null）のときは呼び出し側が
// PlaceholderFactory へフォールバックする契約（tech-stack-unity.md「資産の取り扱い」/ 規約10）。
// 取込済み GLB は authoring 時点で GameConfig.Presentation の想定高さ（m）へ Blender でリスケール済み
// （game/_generated/MANIFEST.jsonl bbox_authoring_m）のため、ここでは localScale を追加調整しない
// （追加スケーリングはモデル間の相対サイズ関係を破壊しうるため行わない）。原点位置は生成物ごとに不定なため、
// Renderer 合成 bounds の実測値から接地オフセットのみを補正する（PlaceholderFactory と同じ「親のローカル
// 原点に接地」規約に合わせる）。
using UnityEngine;

namespace ForgeGame.Components
{
    internal static class GeneratedModelFactory
    {
        /// <summary>
        /// resourceKey（GameConfig.AssetKeys の値）で実モデルを Resources.Load→Instantiate し、親のローカル
        /// 原点に接地させて返す。資産が未取込/未生成なら null を返す（呼び出し側は PlaceholderFactory へ
        /// フォールバックすること。CR-CODE 規約10: Renderer は GetComponentInChildren 前提で参照する側を組む）。
        /// </summary>
        internal static GameObject TryCreateGroundedModel(string resourceKey, Transform parent, string name)
        {
            GameObject prefab = Resources.Load<GameObject>(resourceKey);
            if (prefab == null) return null;

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // 取込済みのはずの資産に Renderer が無いのは構造的破損（Integrate 検証（ForgeAssetIntegration）
                // を素通りしたケース）。プレースホルダへフォールバックさせるため破棄して null を返すが、
                // 無言で握り潰さず1回明示ログする（規約12 と同じ思想）。
                Debug.LogError($"[GeneratedModelFactory] \"{name}\" (key={resourceKey}) has no Renderer after instantiation; falling back to placeholder.");
                Object.Destroy(instance);
                return null;
            }

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

            // 親の原点（ワールド座標）に bounds の下端を合わせる（親は非回転・スケール1を前提。
            // Tower/Enemy/Core の View は全て地表面に立つ設置物のため常に成立する）。
            float groundOffsetM = parent.position.y - combined.min.y;
            instance.transform.localPosition += new Vector3(0f, groundOffsetM, 0f);

            return instance;
        }
    }
}
