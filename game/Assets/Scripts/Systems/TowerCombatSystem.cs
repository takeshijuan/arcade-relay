// TowerCombatSystem.cs — タワー自動攻撃（純粋 C#・エンジン非依存。S-05）。
// gdd「タワー自動攻撃」節（P-02, P-03）+ conventions.md「撃破帰属ルールの実装契約」
// （全ダメージ適用イベントは発生源タワー種別を必須フィールドとして持つ。同フレーム複数ヒットの決定は
//  シーン内タワーコンポーネントの登録順=BuildSpotSystem.Towers のインデックス順=設置順に従う決定論的順序）。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。Vector3/Mathf は値型として使用可。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForgeGame.Systems
{
    /// <summary>タワーのダメージ適用イベント（発生源タワー種別を必須で持つ — conventions.md 撃破帰属ルール）。</summary>
    public readonly struct DamageEvent
    {
        public readonly int EnemyId;
        public readonly int Damage;
        public readonly TowerType SourceTowerType;

        // S-24: 発生源タワーの個体Id（BuildSpotSystem.TowerInstance.Id）。タワー発射モーション
        // （砲身/エミッタの向き+リコイル演出）を「どの TowerView が発射したか」で紐づけるために追加した。
        // 省略時 -1（未指定/呼び出し元不明。既存の3引数コンストラクタ呼び出し — 撃破帰属ロジックのテスト等 —
        // との後方互換のためトレーリング省略可能引数とし、既存呼び出し箇所は変更不要）。
        public readonly int SourceTowerId;

        public DamageEvent(int enemyId, int damage, TowerType sourceTowerType, int sourceTowerId = -1)
        {
            EnemyId = enemyId;
            Damage = damage;
            SourceTowerType = sourceTowerType;
            SourceTowerId = sourceTowerId;
        }
    }

    /// <summary>
    /// タワーの発射/tick間隔管理・ターゲティングを進め、ダメージ適用イベントを発行する。
    /// Bastion Cannon（単体高火力）: 射程内で最も進行度(DistanceTraveledM)が高い1体を単発高ダメージ（規定間隔）。
    /// Arc Emitter（範囲低火力）: 索敵射程内に敵が1体でもいれば tick 間隔ごとに効果半径内の全敵へ低ダメージ。
    /// アップグレード段階（Level）に応じたダメージは GameConfig の各タワー Lv1〜3 値を参照する（S-05 時点は常に Lv1）。
    /// </summary>
    public static class TowerCombatSystem
    {
        public static void Tick(float deltaTime, BuildSpotSystem buildSpots, IReadOnlyList<EnemyInstance> enemies, List<DamageEvent> damageEventsOut)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime は 0 以上である必要がある。");
            if (buildSpots == null) throw new ArgumentNullException(nameof(buildSpots));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            if (damageEventsOut == null) throw new ArgumentNullException(nameof(damageEventsOut));

            IReadOnlyList<TowerInstance> towers = buildSpots.Towers;
            // タワー登録順（=towers のインデックス順=設置順）で処理する。gdd「撃破の帰属ルール」の
            // 決定論的順序（同フレーム複数ヒット時、最初に評価されたイベントを final-hit とする）に一致。
            for (int i = 0; i < towers.Count; i++)
            {
                TowerInstance tower = towers[i];
                float interval = tower.Type == TowerType.BastionCannon
                    ? GameConfig.BastionCannon.FireInterval
                    : GameConfig.ArcEmitter.TickInterval;

                tower.CooldownTimer += deltaTime;
                // ターゲット不在の間は「発射準備完了で待機」までしか蓄積しない（interval で頭打ち）。
                // 上限を設けないと長時間ターゲット不在だったタワーが対象出現時に蓄積分を一気に連射する
                // バースト（gdd に無い挙動）が発生するため、delta-time駆動のまま待機上限をクランプする。
                if (tower.CooldownTimer > interval) tower.CooldownTimer = interval;

                if (tower.CooldownTimer >= interval)
                {
                    bool fired = tower.Type == TowerType.BastionCannon
                        ? TryFireBastionCannon(tower, enemies, damageEventsOut)
                        : TryFireArcEmitter(tower, enemies, damageEventsOut);

                    // 発射できた場合のみ余剰時間を残してタイマーを消費する（delta-time駆動。フレームレート非依存）。
                    // ターゲット不在時はタイマーを待機上限(interval)で保持し、対象出現時に即応させる。
                    if (fired) tower.CooldownTimer -= interval;
                }

                buildSpots.UpdateTower(i, tower);
            }
        }

        private static bool TryFireBastionCannon(TowerInstance tower, IReadOnlyList<EnemyInstance> enemies, List<DamageEvent> outEvents)
        {
            float range = GameConfig.BastionCannon.RangeM;
            int bestIndex = -1;
            float bestDistanceTraveled = -1f;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance e = enemies[i];
                if (!e.Active) continue;
                if (!IsWithinRange(tower.Position, e.DistanceTraveledM, range)) continue;
                if (e.DistanceTraveledM > bestDistanceTraveled)
                {
                    bestDistanceTraveled = e.DistanceTraveledM;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return false;

            int damage = DamageForLevel(GameConfig.BastionCannon.DamageLv1, GameConfig.BastionCannon.DamageLv2, GameConfig.BastionCannon.DamageLv3, tower.Level);
            outEvents.Add(new DamageEvent(enemies[bestIndex].Id, damage, TowerType.BastionCannon, tower.Id));
            return true;
        }

        private static bool TryFireArcEmitter(TowerInstance tower, IReadOnlyList<EnemyInstance> enemies, List<DamageEvent> outEvents)
        {
            float searchRange = GameConfig.ArcEmitter.RangeM;
            float radius = GameConfig.ArcEmitter.RadiusM;

            bool anyInSearchRange = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Active && IsWithinRange(tower.Position, enemies[i].DistanceTraveledM, searchRange))
                {
                    anyInSearchRange = true;
                    break;
                }
            }
            if (!anyInSearchRange) return false;

            int damage = DamageForLevel(GameConfig.ArcEmitter.DamageLv1, GameConfig.ArcEmitter.DamageLv2, GameConfig.ArcEmitter.DamageLv3, tower.Level);
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInstance e = enemies[i];
                if (!e.Active) continue;
                if (!IsWithinRange(tower.Position, e.DistanceTraveledM, radius)) continue;
                outEvents.Add(new DamageEvent(e.Id, damage, TowerType.ArcEmitter, tower.Id));
            }
            return true; // tick消費は索敵射程内に1体でもいれば発生する（gdd 仕様。効果半径内が0体でも tick は消費）
        }

        private static bool IsWithinRange(Vector3 towerPosition, float enemyDistanceTraveledM, float range)
        {
            Vector3 enemyPosition = WaveSpawnSystem.GetPathPosition(enemyDistanceTraveledM);
            return Vector3.Distance(towerPosition, enemyPosition) <= range;
        }

        private static int DamageForLevel(int lv1, int lv2, int lv3, int level)
        {
            // CR-CODE S-05 iter1 M指摘: 不正 Level（0・負値・4以上）を黙って Lv3 として成功させない
            // （既定 TowerInstance の Level=0 や将来の S-10 アップグレードバグが「常に最大ダメージ」に
            //  見え隠蔽されるのを避ける — 失敗時に成功を装う戻り値の禁止）。
            switch (level)
            {
                case 1: return lv1;
                case 2: return lv2;
                case 3: return lv3;
                default: throw new ArgumentOutOfRangeException(nameof(level), level, "TowerInstance.Level は 1〜3 の範囲である必要がある。");
            }
        }
    }
}
