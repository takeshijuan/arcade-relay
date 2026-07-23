// WaveSpawnController.cs — Game シーンの敵進行オーケストレータ（S-04）。
// Systems/WaveSpawnSystem（純粋 C#）を Update() の Time.deltaTime で駆動し、EnemyView の生成/削除・
// Transform 反映、CoreView へのダメージ適用を配線する。ロジック本体（スポーン判定・移動・ゴール到達判定・
// コアHP減算）は Systems/ に委譲する（規約3: Components は薄く）。
using System;
using System.Collections.Generic;
using UnityEngine;
using ForgeGame.Systems;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。CoreView への参照は SerializeField で配線する。</summary>
    public sealed class WaveSpawnController : MonoBehaviour
    {
        [SerializeField] private CoreView coreView;

        private readonly WaveSpawnSystem waveSpawnSystem = new WaveSpawnSystem();
        private readonly List<EnemyGoalReachedEvent> goalEventsBuffer = new List<EnemyGoalReachedEvent>();
        private readonly List<int> waveStartEventsBuffer = new List<int>(); // S-13: ウェーブ開始告知（SFX-05）用
        private readonly Dictionary<int, EnemyView> enemyViews = new Dictionary<int, EnemyView>();
        private bool defeatEventFired;

        /// <summary>コアHP<=0（敗北成立）で1回だけ発火する（S-06 が RunResult 確定・Result 遷移に使う想定）。</summary>
        public event Action OnCoreDefeated;

        /// <summary>テスト/S-06 からの直接参照用（読み取り専用）。</summary>
        public WaveSpawnSystem WaveSystem => waveSpawnSystem;

        /// <summary>テスト用の CoreView 注入。Awake/Start 実行前（非アクティブ状態）に呼ぶこと（規約9）。</summary>
        public void SetCoreViewForTest(CoreView view) => coreView = view;

        private void Start()
        {
            if (coreView == null)
            {
                // 規約12: 配線破損は Start で1回 LogError。CoreView 未注入では敵のゴール到達をコアHPへ反映できない。
                Debug.LogError("[WaveSpawnController] CoreView is not wired; core damage cannot be applied.");
            }
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        /// <summary>
        /// PlayMode テスト用の直接駆動口。Update() と同じ内部処理（StepSimulation）を呼ぶだけで挙動差異は無い
        /// （規約9 のテスト用シーム。実フレーム待ちを避け deltaTime を任意刻みで進められる）。
        /// </summary>
        public void StepForTest(float deltaTime) => StepSimulation(deltaTime);

        private void StepSimulation(float deltaTime)
        {
            if (coreView != null && coreView.IsDefeated)
            {
                FireDefeatEventOnce();
                return; // 敗北成立後は進行を止める
            }

            goalEventsBuffer.Clear();
            waveStartEventsBuffer.Clear();
            waveSpawnSystem.Tick(deltaTime, goalEventsBuffer, waveStartEventsBuffer);

            SyncEnemyViews(deltaTime);

            for (int i = 0; i < waveStartEventsBuffer.Count; i++)
            {
                // Integrate: SFX-05（ウェーブ開始告知）— design/assets.md トリガー「WaveSpawnSystem のウェーブ
                // 予告表示イベント（次ウェーブ開始）」。演出キューの選択は FeedbackCueSystem に委譲する（S-13）。
                // 再生位置は経路始点（敵の湧く場所＝これから来る脅威の方向を示す）。
                FeedbackCue waveStartCue = FeedbackCueSystem.SelectWaveStartCue();
                AudioCuePlayer.PlayOneShot(waveStartCue.AssetKey, GameConfig.Path.StartPoint, pitch: waveStartCue.PitchMultiplier);
            }

            for (int i = 0; i < goalEventsBuffer.Count; i++)
            {
                EnemyGoalReachedEvent evt = goalEventsBuffer[i];
                TryRemoveEnemyView(evt.EnemyId);
                coreView?.ApplyDamage(evt.Type);
            }

            if (coreView != null && coreView.IsDefeated)
            {
                FireDefeatEventOnce();
            }
        }

        private void SyncEnemyViews(float deltaTime)
        {
            IReadOnlyList<EnemyInstance> enemies = waveSpawnSystem.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance enemy = enemies[i];
                if (!enemy.Active) continue;

                if (!enemyViews.TryGetValue(enemy.Id, out EnemyView view))
                {
                    var enemyGo = new GameObject($"Enemy_{enemy.Id}_{enemy.Type}");
                    enemyGo.transform.SetParent(transform, false);
                    view = enemyGo.AddComponent<EnemyView>();
                    view.Initialize(enemy.Id, enemy.Type);
                    enemyViews[enemy.Id] = view;
                }

                view.ApplyProgress(enemy.DistanceTraveledM, deltaTime);
            }
        }

        /// <summary>
        /// enemyId に対応する EnemyView が存在すれば即座に破棄する（ゴール到達時の消滅専用の削除口。
        /// gdd「Marauder」行: ゴール到達は『撃破扱いにしない』ため演出無しの即時消滅のままでよい）。
        /// タワーによる撃破解決（final-hit）は S-25 以降 <see cref="TryStartEnemyDefeatMotion"/> を使う。
        /// </summary>
        public bool TryRemoveEnemyView(int enemyId)
        {
            if (enemyViews.TryGetValue(enemyId, out EnemyView view))
            {
                Destroy(view.gameObject);
                enemyViews.Remove(enemyId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// enemyId に対応する EnemyView が存在すれば撃破演出（スケールダウン等の非表示化）を開始する
        /// （S-25・Components/BuildSpotController の final-hit 撃破解決から呼ぶ削除口）。gdd「モーション方式」節
        /// 『対象メッシュの非表示化（またはディゾルブ）』に対応するため、TryRemoveEnemyView と異なり同フレームでは
        /// GameObject を破棄しない。追跡辞書からは即座に外す（撃破確定後の状態として以降 TryGetEnemyPosition 等の
        /// 対象から外れる。演出完了後の実破棄は EnemyView 自身が Time.deltaTime 駆動で行う）。
        /// </summary>
        public bool TryStartEnemyDefeatMotion(int enemyId)
        {
            if (enemyViews.TryGetValue(enemyId, out EnemyView view))
            {
                enemyViews.Remove(enemyId);
                view.PlayDefeatMotion();
                return true;
            }
            return false;
        }

        /// <summary>
        /// enemyId に対応する EnemyView の現在位置を取得する（Integrate: SFX 再生位置として
        /// Components/BuildSpotController が撃破/被弾イベント時に参照する。View 未存在なら false）。
        /// </summary>
        public bool TryGetEnemyPosition(int enemyId, out Vector3 position)
        {
            if (enemyViews.TryGetValue(enemyId, out EnemyView view))
            {
                position = view.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        private void FireDefeatEventOnce()
        {
            if (defeatEventFired) return;
            defeatEventFired = true;
            OnCoreDefeated?.Invoke();
        }
    }
}
