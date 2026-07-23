// EnemyHealthSystem.cs — 敵HP・撃破解決（純粋 C#・エンジン非依存。S-05）。
// gdd「敵HP・撃破解決」節（P-03）+ conventions.md「撃破帰属ルールの実装契約」。
// HP減算そのものは敵インスタンスの唯一の所有者である WaveSpawnSystem.ApplyDamage が行う
// （CoreView が CoreDefenseSystem.ApplyDamage を呼ぶのと同じ「所有者が計算結果を反映し、
// System は純粋関数を提供する」パターン）。ここは撃破判定・報酬額算出の純粋関数と、
// 単一ラン内の総撃破数(KillCount)/AoE撃破数(AoeKillCount)の分離集計（インスタンス状態。
// 新規ラン=新規インスタンス生成でリセットされる想定）を提供する。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。
using System;

namespace ForgeGame.Systems
{
    public sealed class EnemyHealthSystem
    {
        /// <summary>単一ラン内の総撃破数（種別問わず）。</summary>
        public int KillCount { get; private set; }

        /// <summary>単一ラン内の Arc Emitter 帰属撃破数（ACH-04 の判定基盤）。</summary>
        public int AoeKillCount { get; private set; }

        /// <summary>ダメージ適用後の新HP（0未満にはならない）。</summary>
        public static int ApplyDamage(int currentHp, int damage) => Math.Max(0, currentHp - damage);

        /// <summary>HPが0以下（撃破）かどうか。</summary>
        public static bool IsDefeated(int hp) => hp <= 0;

        /// <summary>敵種別に応じた撃破報酬（MARAUDER_GOLD_REWARD / WARBEAST_GOLD_REWARD）。</summary>
        public static int GoldReward(EnemyType type) =>
            type == EnemyType.Marauder ? GameConfig.Marauder.GoldReward : GameConfig.Warbeast.GoldReward;

        /// <summary>
        /// 撃破確定時に1回だけ呼ぶ。総撃破数を必ず加算し、final-hit の発生源タワー種別が Arc Emitter の
        /// 場合のみ AoE撃破数へも加算する（conventions.md 撃破帰属ルール。final-hit の決定は
        /// TowerCombatSystem のタワー登録順 + WaveSpawnSystem.ApplyDamage の Active ガードにより保証される）。
        /// </summary>
        public void RecordKill(TowerType sourceTowerType)
        {
            KillCount++;
            if (sourceTowerType == TowerType.ArcEmitter) AoeKillCount++;
        }
    }
}
