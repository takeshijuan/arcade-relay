// WaveSpawnSystem.cs — ウェーブ進行・敵スポーン・経路移動（純粋 C#・エンジン非依存。S-04 + S-12）。
// gdd「ウェーブ進行・敵スポーン」節（P-03）+「難易度曲線」節。経路はタワー配置で変化しない固定直線
// （GameConfig.Path.StartPoint → EndPoint、全長 GameConfig.Wave.PathLengthM — 経路の世界座標は S-04 実装判断。
// GameConfig.Path のコメント参照）。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。Vector3/Mathf は値型として使用可。
// 出現間隔は GameConfig.Wave.SpawnIntervalBase × 現在ウェーブの WaveDef.SpawnIntervalMultiplier
// （gdd 難易度曲線表の「出現間隔◯%」列。S-12）。ウェーブ間の準備フェーズは GameConfig.Wave.WavePrepSec 固定。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Systems
{
    /// <summary>1体の敵インスタンス（純粋値。Transform は持たない — Components/EnemyView が描画を担う）。</summary>
    public struct EnemyInstance
    {
        public int Id;
        public EnemyType Type;
        public float DistanceTraveledM; // 経路始点(0)からの走行距離
        public bool Active;             // false = ゴール到達済み or 撃破済み（いずれも移動/ターゲティング対象外）
        public int Hp;                  // 現在HP（S-05）。0以下で撃破 — ApplyDamage/EnemyHealthSystem.IsDefeated 参照
    }

    /// <summary>WaveSpawnSystem.ApplyDamage の結果（S-05）。</summary>
    public readonly struct EnemyDamageResult
    {
        public readonly bool Found;     // enemyId が生存中の敵として見つかったか
        public readonly bool Defeated;  // このダメージで撃破（HP<=0）に至ったか
        public readonly EnemyType Type;
        public readonly int RemainingHp;

        public EnemyDamageResult(bool found, bool defeated, EnemyType type, int remainingHp)
        {
            Found = found;
            Defeated = defeated;
            Type = type;
            RemainingHp = remainingHp;
        }
    }

    /// <summary>ゴール到達イベント（CoreDefenseSystem の入力）。</summary>
    public readonly struct EnemyGoalReachedEvent
    {
        public readonly int EnemyId;
        public readonly EnemyType Type;

        public EnemyGoalReachedEvent(int enemyId, EnemyType type)
        {
            EnemyId = enemyId;
            Type = type;
        }
    }

    /// <summary>
    /// ウェーブ進行・スポーン・経路移動を管理する純粋インスタンスクラス。
    /// Components/WaveSpawnController が Update() の Time.deltaTime を渡して Tick を駆動する（規約2: delta-time 必須）。
    /// </summary>
    public sealed class WaveSpawnSystem
    {
        private readonly List<EnemyInstance> enemies = new List<EnemyInstance>();
        private int nextEnemyId;

        private int currentWaveIndex; // 0-based。GameConfig.WaveComposition.Waves のインデックス
        private int marauderSpawnedInWave;
        private int warbeastSpawnedInWave;
        private float spawnTimer;
        private float prepTimer;
        private bool inPrepPhase = true; // WAVE 1 の出現前も準備フェーズ扱い
        private int lastAnnouncedWaveIndex = -1; // S-13: ウェーブ開始（準備フェーズ突入）を1ウェーブ1回だけ通知するための既通知インデックス

        /// <summary>
        /// 1-based の現在ウェーブ番号（HUD表示用）。全ウェーブ消化後も最終ウェーブ番号（Waves.Length）に
        /// クランプされたまま返す（Length+1 にはならない）。全消化の判定には CurrentWaveNumber を使わず
        /// AllWavesSpawned を正とすること（CR-CODE S-04 iter1 対応: 旧コメントは Length+1 を返すと誤記していた）。
        /// </summary>
        public int CurrentWaveNumber => Math.Min(currentWaveIndex, GameConfig.WaveComposition.Waves.Length - 1) + 1;

        /// <summary>全ウェーブのスポーンを消化済みか（消化済みでも既存の敵は経路上を移動し続ける）。</summary>
        public bool AllWavesSpawned => currentWaveIndex >= GameConfig.WaveComposition.Waves.Length;

        /// <summary>現在の全敵インスタンス（生存・消滅済み双方を含む。生存判定は Active を見る）。</summary>
        public IReadOnlyList<EnemyInstance> Enemies => enemies;

        /// <summary>生存中の敵数。</summary>
        public int ActiveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].Active) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 1フレーム分（deltaTime 秒）進める。スポーン判定→経路移動の順で処理し、
        /// このフレームでゴールに到達した敵を goalEvents（呼び出し側が用意した空リスト）に積む。
        /// waveStartEventsOut を渡すと、このフレームで新しいウェーブの準備フェーズに突入した場合に
        /// 1-based のウェーブ番号を積む（gdd「ウェーブ進行・敵スポーン」節の「ウェーブ予告表示イベント」。
        /// S-13。省略可 — 省略時は内部の既通知状態のみ更新し呼び出し側には何も返さない）。
        /// </summary>
        public void Tick(float deltaTime, List<EnemyGoalReachedEvent> goalEvents, List<int> waveStartEventsOut = null)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime は 0 以上である必要がある。");
            if (goalEvents == null) throw new ArgumentNullException(nameof(goalEvents));

            AdvanceSpawning(deltaTime, waveStartEventsOut);
            AdvanceMovement(deltaTime, goalEvents);
        }

        /// <summary>経路始点(距離0)〜終点(PathLengthM)の走行距離をワールド座標へ変換する（固定直線経路）。</summary>
        public static Vector3 GetPathPosition(float distanceTraveledM)
        {
            float t = Mathf.Clamp01(distanceTraveledM / GameConfig.Wave.PathLengthM);
            return Vector3.Lerp(GameConfig.Path.StartPoint, GameConfig.Path.EndPoint, t);
        }

        /// <summary>
        /// 生存中の敵へダメージを適用する（HP減算は EnemyHealthSystem.ApplyDamage に委譲。S-05）。
        /// HP<=0 になった場合は Active=false（撃破。以後の移動/ゴール判定/ターゲティング対象外）にする。
        /// 既に非生存(Active=false) or 存在しない enemyId は Found=false を返す — 呼び出し側は同フレーム内で
        /// 同一敵へ複数のダメージ適用イベントが届いた場合の2発目以降をここで自然に無効化できる
        /// （conventions.md 撃破帰属ルール: final-hit のみを撃破に帰属させる仕組みの一部）。
        /// </summary>
        public EnemyDamageResult ApplyDamage(int enemyId, int damage)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance e = enemies[i];
                if (e.Id != enemyId || !e.Active) continue;

                e.Hp = EnemyHealthSystem.ApplyDamage(e.Hp, damage);
                bool defeated = EnemyHealthSystem.IsDefeated(e.Hp);
                if (defeated) e.Active = false;
                enemies[i] = e;
                return new EnemyDamageResult(true, defeated, e.Type, e.Hp);
            }
            return new EnemyDamageResult(false, false, default, 0);
        }

        private void AdvanceSpawning(float deltaTime, List<int> waveStartEventsOut)
        {
            if (AllWavesSpawned) return;

            // S-13: 準備フェーズへ突入した最初の Tick で1回だけウェーブ開始を通知する（WAVE 1 の初回 Tick も含む）。
            // waveStartEventsOut が null（呼び出し側が未対応）でも既通知インデックスは進める — 呼び出し側の
            // 対応有無に内部状態が依存しないようにするため。
            if (inPrepPhase && currentWaveIndex != lastAnnouncedWaveIndex)
            {
                lastAnnouncedWaveIndex = currentWaveIndex;
                waveStartEventsOut?.Add(currentWaveIndex + 1);
            }

            GameConfig.WaveComposition.WaveDef wave = GameConfig.WaveComposition.Waves[currentWaveIndex];
            float intervalSec = GameConfig.Wave.SpawnIntervalBase * wave.SpawnIntervalMultiplier;

            if (inPrepPhase)
            {
                prepTimer += deltaTime;
                if (prepTimer < GameConfig.Wave.WavePrepSec) return;
                inPrepPhase = false;
                prepTimer = 0f;
                spawnTimer = intervalSec; // 準備フェーズ終了直後に1体目を即スポーンさせる
            }
            else
            {
                spawnTimer += deltaTime;
            }

            while (spawnTimer >= intervalSec &&
                   (marauderSpawnedInWave < wave.MarauderCount || warbeastSpawnedInWave < wave.WarbeastCount))
            {
                spawnTimer -= intervalSec;
                SpawnNext(wave);
            }

            if (marauderSpawnedInWave >= wave.MarauderCount && warbeastSpawnedInWave >= wave.WarbeastCount)
            {
                currentWaveIndex++;
                marauderSpawnedInWave = 0;
                warbeastSpawnedInWave = 0;
                inPrepPhase = true;
                prepTimer = 0f;
                spawnTimer = 0f;
            }
        }

        private void SpawnNext(GameConfig.WaveComposition.WaveDef wave)
        {
            // Marauder → Warbeast の順に消化する決定論的な出現順（gdd「難易度曲線」表の構成をそのまま採用）。
            EnemyType type;
            if (marauderSpawnedInWave < wave.MarauderCount)
            {
                type = EnemyType.Marauder;
                marauderSpawnedInWave++;
            }
            else
            {
                type = EnemyType.Warbeast;
                warbeastSpawnedInWave++;
            }

            int hp = type == EnemyType.Marauder ? GameConfig.Marauder.Hp : GameConfig.Warbeast.Hp;
            enemies.Add(new EnemyInstance
            {
                Id = nextEnemyId++,
                Type = type,
                DistanceTraveledM = 0f,
                Active = true,
                Hp = hp,
            });
        }

        private void AdvanceMovement(float deltaTime, List<EnemyGoalReachedEvent> goalEvents)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance e = enemies[i];
                if (!e.Active) continue;

                float speedMps = e.Type == EnemyType.Marauder
                    ? GameConfig.Marauder.SpeedMps
                    : GameConfig.Warbeast.SpeedMps;
                e.DistanceTraveledM += speedMps * deltaTime;

                if (e.DistanceTraveledM >= GameConfig.Wave.PathLengthM)
                {
                    e.DistanceTraveledM = GameConfig.Wave.PathLengthM;
                    e.Active = false;
                    goalEvents.Add(new EnemyGoalReachedEvent(e.Id, e.Type));
                }

                enemies[i] = e;
            }
        }
    }
}
