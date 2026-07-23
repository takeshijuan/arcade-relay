// TowerView.cs — 設置済みタワー1基のシーン表現（Bastion Cannon / Arc Emitter 共通・S-05）。
// ロジックは持たない（配置判定は Systems/BuildSpotSystem、攻撃は Systems/TowerCombatSystem）。
// MDL-01/MDL-02 取込済み（Integrate）。GeneratedModelFactory が Resources.Load できた場合は実モデルを、
// 未取込/未生成の場合は種別ごとにシルエットの異なる単色プリミティブへフォールバックする
// （assets-config.md プレースホルダ運用。spire=Bastion Cannon / dome=Arc Emitter のシルエット差別化）。
// S-24: 発射モーション（gdd「モーション方式」節）— 発射時のみ砲身/エミッタ部分（子Transform=visual。
// MDL-01/02 は rig_type:none の単一メッシュ取込のため、砲身/エミッタ専用のサブTransformは持たず、
// TowerView 直下の visual ルート自体を「砲身/エミッタ部分」として扱う実装判断）を対象敵方向へ
// Quaternion.LookRotation で回転させ、発射瞬間に軽いリコイル（スケールのショートパンチ→既定サイズへ復帰）を
// Time.deltaTime 駆動でトリガーする。
using UnityEngine;
using ForgeGame.Systems;

namespace ForgeGame.Components
{
    public sealed class TowerView : MonoBehaviour
    {
        public int TowerId { get; private set; }
        public TowerType Type { get; private set; }

        /// <summary>
        /// 砲身/エミッタ部分として扱う子Transform（テスト/将来ストーリーからの視覚サニティ確認用の読み取り専用参照）。
        /// Initialize 未実行時は null。
        /// </summary>
        public Transform VisualTransform => visual != null ? visual.transform : null;

        private GameObject visual;
        private Vector3 visualBaseScale;
        // 負値=非再生中。0以上ならリコイル演出の経過秒数（Time.deltaTime で進める）。
        private float recoilElapsedSec = -1f;

        // S-24 CR-CODE iter1 minor指摘対応: 発射モーションの向き計算で使う退化方向ベクトル（発射元とターゲットが
        // 同一地点）判定の epsilon。チューニング対象ではないためGameConfigではなくファイル内 const とする
        // （PlaceholderFactory.DefaultPrimitiveHeightUnits の先例に倣う）。sqrMagnitude 比較のため単位は m^2。
        private const float DegenerateDirectionSqrEpsilon = 0.0001f;

        /// <summary>設置確定直後に1回だけ呼ぶ。見た目（実モデル優先・無ければプレースホルダ）の生成と種別/位置の確定を行う。</summary>
        public void Initialize(int towerId, TowerType type, Vector3 position)
        {
            TowerId = towerId;
            Type = type;
            transform.position = position;

            string modelKey = type == TowerType.BastionCannon
                ? GameConfig.AssetKeys.ModelBastionCannon
                : GameConfig.AssetKeys.ModelArcEmitter;
            visual = GeneratedModelFactory.TryCreateGroundedModel(modelKey, transform, $"{type}Model");
            if (visual == null)
            {
                // Bastion Cannon = 尖塔（縦長）、Arc Emitter = ドーム（横広）— シルエット方針（P-03）を
                // プレースホルダ段階から反映する。
                PrimitiveType primitive = type == TowerType.BastionCannon ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
                float heightM = type == TowerType.BastionCannon
                    ? GameConfig.Presentation.BastionCannonHeightM
                    : GameConfig.Presentation.ArcEmitterHeightM;

                visual = PlaceholderFactory.CreateGroundedPrimitive(
                    primitive, transform, heightM, GameConfig.Placeholder.TowerColor, $"{type}PlaceholderVisual");
            }

            visualBaseScale = visual.transform.localScale;
        }

        /// <summary>
        /// 発射瞬間に1回呼ぶ（Components/BuildSpotController が TowerCombatSystem のダメージ適用イベント発行時に
        /// 呼び出す）。visual を targetPosition 方向へ即座に向け、リコイル演出を開始する。visual 未生成
        /// （Initialize 未実行のプログラミングエラー級の異常系。唯一の呼び出し元 BuildSpotController は必ず
        /// Initialize 済みの TowerView のみを保持するため現状は到達不能）は CR-CODE iter1 minor指摘対応として
        /// 規約12（配線破損は1回明示ログ）に揃え、Debug.LogError で表面化してから何もしない。
        /// </summary>
        public void PlayFireMotion(Vector3 targetPosition)
        {
            if (visual == null)
            {
                Debug.LogError("[TowerView] PlayFireMotion called before Initialize(); visual is not set.");
                return;
            }

            // CR-CODE iter2 minor指摘対応: EnemyView.ApplyProgress は敵の見た目 root position に
            // sin波の上下ボブ（GameConfig.Presentation.EnemyBobAmplitude）を加算するため、targetPosition
            // （hitPosition）の y は ±振幅で振動する。ここで水平投影（y=0固定）してから正規化することで、
            // ボブ振幅やその調整・将来の飛行敵導入に関わらず砲身/エミッタの向きが常に水平yawのみで
            // 回転すること（タワーの直立性）を構造的に保証する。
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > DegenerateDirectionSqrEpsilon)
            {
                visual.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            recoilElapsedSec = 0f;
        }

        private void Update()
        {
            // Update() は毎フレーム呼ばれるため、visual==null をここで LogError すると出荷ビルドでも
            // フレーム毎にログを吐く（規約12の「1回」に反する）。visual 未生成時は PlayFireMotion 側で
            // 既に1回表面化しているため recoilElapsedSec は 0 以上に進まず、ここには到達しない
            // （recoilElapsedSec は PlayFireMotion 成功時のみ 0 にセットされる）。
            if (recoilElapsedSec < 0f || visual == null) return;

            recoilElapsedSec += Time.deltaTime;
            float duration = GameConfig.Presentation.TowerFireRecoilDurationSec;
            float t = duration > 0f ? Mathf.Clamp01(recoilElapsedSec / duration) : 1f;

            // 0秒時点で最大パンチ、durationで既定サイズへ線形に復帰する「一瞬伸びて戻る」ショートパンチ。
            float punchFactor = (1f - t) * GameConfig.Presentation.TowerFireRecoilScalePunch;
            visual.transform.localScale = visualBaseScale * (1f + punchFactor);

            if (t >= 1f)
            {
                visual.transform.localScale = visualBaseScale;
                recoilElapsedSec = -1f;
            }
        }
    }
}
