// EnemyView.cs — 敵1体のシーン表現（Marauder/Warbeast 共通・S-04）。
// ロジックは持たず、WaveSpawnController から渡される走行距離を Transform へ反映するだけ
// （gdd「モーション方式」節: 位置補間 + 進行方向への回転追従 + sin波の上下ボブ）。
// MDL-03（Marauder）/ MDL-04（Warbeast）とも取込済み（Integrate・S-19）。GeneratedModelFactory が
// Resources.Load できた場合は実モデルを、未取込/未生成の場合は単色 Capsule プレースホルダへフォールバックする
// （assets-config.md プレースホルダ運用。TowerView/CoreView と同型の契約）。
// S-25: 撃破演出（gdd「モーション方式」節『対象メッシュの非表示化（またはディゾルブ）』）— 撃破確定直後に
// 即 Destroy せず、ルート transform を等方スケールダウンしてから自身で破棄する（TowerView.PlayFireMotion の
// Time.deltaTime 駆動アニメーションと同型パターン）。
using UnityEngine;
using ForgeGame.Systems;

namespace ForgeGame.Components
{
    public sealed class EnemyView : MonoBehaviour
    {
        public int EnemyId { get; private set; }
        public EnemyType Type { get; private set; }

        private float bobPhase;
        private Vector3 baseScale;
        // 負値=非再生中。0以上なら撃破スケールダウン演出の経過秒数（Time.deltaTime で進める）。
        private float defeatElapsedSec = -1f;

        /// <summary>
        /// 撃破演出（スケールダウン）を再生中かどうか（テスト観測用の読み取り専用プロパティ。
        /// S-24 の TowerView.VisualTransform と同型のテストシーム）。破棄直前まで true のまま。
        /// </summary>
        public bool IsPlayingDefeatMotion => defeatElapsedSec >= 0f;

        /// <summary>スポーン直後に1回だけ呼ぶ。見た目（実モデル優先・無ければプレースホルダ）の生成と種別の確定を行う。</summary>
        public void Initialize(int enemyId, EnemyType type)
        {
            EnemyId = enemyId;
            Type = type;
            baseScale = transform.localScale;

            float heightM = type == EnemyType.Marauder
                ? GameConfig.Presentation.MarauderHeightM
                : GameConfig.Presentation.WarbeastHeightM;

            string modelKey = type == EnemyType.Marauder
                ? GameConfig.AssetKeys.ModelMarauder
                : GameConfig.AssetKeys.ModelWarbeast;
            GameObject visual = GeneratedModelFactory.TryCreateGroundedModel(modelKey, transform, $"{type}Model");
            if (visual != null) return;

            PlaceholderFactory.CreateGroundedPrimitive(
                PrimitiveType.Capsule, transform, heightM, GameConfig.Placeholder.EnemyColor, $"{type}PlaceholderVisual");
        }

        /// <summary>
        /// 走行距離(m)から Transform を更新する。位置自体は距離ベース（delta-time 非依存）、
        /// ボブ演出の位相のみ deltaTime で進める（規約2: delta-time 必須）。
        /// </summary>
        public void ApplyProgress(float distanceTraveledM, float deltaTime)
        {
            Vector3 basePosition = WaveSpawnSystem.GetPathPosition(distanceTraveledM);

            bobPhase += GameConfig.Presentation.EnemyBobFrequency * deltaTime;
            float bobOffsetM = Mathf.Sin(bobPhase) * GameConfig.Presentation.EnemyBobAmplitude;
            transform.position = basePosition + new Vector3(0f, bobOffsetM, 0f);

            Vector3 pathDirection = GameConfig.Path.EndPoint - GameConfig.Path.StartPoint;
            if (pathDirection.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(pathDirection.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// 撃破確定直後に1回呼ぶ（WaveSpawnController.TryStartEnemyDefeatMotion 経由。呼び出し元は
        /// Components/BuildSpotController の final-hit 撃破解決）。同フレームでの即消滅ではなく、
        /// GameConfig.Presentation.EnemyDefeatShrinkDurationSec かけてスケールダウンしてから自身を破棄する
        /// （P-03「溶ける実感」— 撃破帰属の集計・撃破報酬タイミングは Systems/ 側で本メソッド呼び出し前に
        /// 確定済みのため、本メソッドは表示専任で副作用を持たない）。
        /// 正規経路（WaveSpawnController.TryStartEnemyDefeatMotion）は辞書除去により one-shot だが、
        /// public API のため再入時に defeatElapsedSec をリセットして演出を延長しないよう再入ガードする
        /// （CR-CODE S-25 iter1 minor #2）。
        /// </summary>
        public void PlayDefeatMotion()
        {
            if (IsPlayingDefeatMotion) return;
            defeatElapsedSec = 0f;
        }

        private void Update()
        {
            if (defeatElapsedSec < 0f) return;

            defeatElapsedSec += Time.deltaTime;
            float duration = GameConfig.Presentation.EnemyDefeatShrinkDurationSec;
            float t = duration > 0f ? Mathf.Clamp01(defeatElapsedSec / duration) : 1f;

            transform.localScale = baseScale * (1f - t);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
