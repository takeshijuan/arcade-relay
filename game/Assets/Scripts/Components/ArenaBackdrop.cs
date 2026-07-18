// ArenaBackdrop — Game シーンの背景/スカイボックス配線 (gameplay-engineer, S-26: Checkpoint C 修正依頼2
// 「アリーナ外周に key image 寄りの背景/スカイボックス追加」/ design/assets.md IMG-06「Unity Skybox / 背景
// バックドロップ用途」). IMG-06 (game/_generated/images/img-06-arena-backdrop.png) を Skybox/6 Sided の
// 全6面（Front/Back/Left/Right/Up/Down）に同一テクスチャとして割り当てる。IMG-06 は水平方向に模様の無い
// 単純な縦グラデーション（地平のダーク void #12081F → 紫 → シアン → 上方マゼンタ #E62284。design/assets.md
// 起票仕様・低コントラスト/低ディテール）のため、面ごとに同一画像を使っても継ぎ目が視覚的に破綻しない。
// Skybox は Unity のレンダリングパイプラインが常にカメラの全方位・無限遠に描画するため、固定俯瞰カメラ
// （ArenaCameraRig, S-04）がどの画角を向いていても背景の欠落（void の露出）が原理的に発生しない。
//
// スコープを Skybox テクスチャの差し替えに限定し、RenderSettings.ambientMode/ambientIntensity 等の全体
// ライティング調整は S-27（URP ポストプロセス/ライティング）の範囲として本 story では変更しない（既存の
// シーン設定を維持）。
//
// CR-CODE s-26 iteration 1 major指摘#1/#4 fix: 以前はここで Shader.Find("Skybox/6 Sided") を Start() の
// たびに呼び、ランタイムで `new Material(shader)` していた。ビルトインシェーダはビルドに含まれる資産
// （.mat 等）から参照されない限りプレイヤービルドでストリップされ、Shader.Find がスタンドアロンビルドで
// null を返す（Editor 実行のみのテストはシェーダ常在のため検知できない）。修正: Editor/AssetIntegration.
// PatchArenaBackdrop が Skybox マテリアルを .mat 資産（Assets/Generated/Materials/ArenaBackdropSkybox.mat）
// として生成・6面テクスチャを割当・保存し、そのアセット参照をここへ焼き込む（Components/HitVfx の
// マテリアル資産化パターンと同型）。マテリアルがビルドに含まれるシーンの SerializeField から参照される
// ことで、Unity のデフォルトのシェーダストリッピングが自動的にこのシェーダをビルドに含める。結果として
// Start() はランタイムで Material を構築せず（同一アセットインスタンスを毎ロードで再利用するため
// マテリアルリークも同時に解消 — CR-CODE s-26 iteration 1 minor指摘#7）、資産参照を RenderSettings.skybox
// へ代入するだけの薄い配線になる（rule: Components はライフサイクルと配線のみ）。
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class ArenaBackdrop : MonoBehaviour
    {
        [Tooltip("IMG-06 を全6面に焼き込んだ Skybox/6 Sided マテリアル資産. Assigned by Editor/AssetIntegration.PatchArenaBackdrop from GameConfig.AssetKeys.ArenaBackdropTexture (Assets/Generated/Materials/ArenaBackdropSkybox.mat).")]
        [SerializeField] private Material _skyboxMaterial;

        /// <summary>Test-observability: the Skybox material actually applied to RenderSettings.skybox by
        /// the most recent Start() call (mirrors ArenaCameraRig.TriggerInvocationCountForTests /
        /// WaveSpawner.EnemyPrefabForTests — a *ForTests surface PlayMode tests read without needing
        /// reflection). CR-CODE s-26 iteration 1 minor指摘#6 fix: reset to null at the top of every Start()
        /// call (not just left stale from a prior scene load) so this genuinely reads null whenever the
        /// current Start() bailed out on a missing material — matching this doc's own claim.</summary>
        public static Material LastAppliedSkyboxForTests { get; private set; }

        private void Start()
        {
            LastAppliedSkyboxForTests = null;

            if (_skyboxMaterial == null)
            {
                Debug.LogError("[Wiring] ArenaBackdrop._skyboxMaterial is unassigned — Editor/AssetIntegration should have wired the baked ArenaBackdropSkybox material (GameConfig.AssetKeys.ArenaBackdropTexture). Skybox left at scene default.");
                return;
            }

            RenderSettings.skybox = _skyboxMaterial;
            LastAppliedSkyboxForTests = _skyboxMaterial;
        }
    }
}
