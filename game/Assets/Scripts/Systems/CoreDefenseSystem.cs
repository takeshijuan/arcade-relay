// CoreDefenseSystem.cs — コア防衛・敗北判定（純粋 C#・エンジン非依存。S-04）。
// gdd「コア防衛・敗北判定」節（P-01, P-04）。敵のゴール到達イベント（WaveSpawnSystem.EnemyGoalReachedEvent）に
// 応じてコアHPを減算し、0以下で敗北成立を判定する。RunResult 確定・Result 遷移は S-06（勝敗判定）のスコープ。
namespace ForgeGame.Systems
{
    public static class CoreDefenseSystem
    {
        /// <summary>
        /// 敵種別に応じたコアHP減算量（MARAUDER_CORE_DAMAGE / WARBEAST_CORE_DAMAGE）を適用し、
        /// 新しいコアHP（0未満にはならない）を返す。
        /// </summary>
        public static int ApplyDamage(int currentCoreHp, EnemyType attacker)
        {
            int damage = attacker == EnemyType.Marauder
                ? GameConfig.Marauder.CoreDamage
                : GameConfig.Warbeast.CoreDamage;
            int next = currentCoreHp - damage;
            return next < 0 ? 0 : next;
        }

        /// <summary>コアHPが0以下（敗北成立）かどうか。</summary>
        public static bool IsDefeated(int coreHp) => coreHp <= 0;
    }
}
