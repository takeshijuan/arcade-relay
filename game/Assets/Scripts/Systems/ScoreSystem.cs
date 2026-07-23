// ScoreSystem.cs — スコア算出（純粋 C#・エンジン非依存）。gdd「スコア・進行」節の finalScore 式の正本実装。
// 数値の正本は GameConfig.Score。この関数はランの勝敗いずれでも1回だけ呼ばれる。
using System;

namespace ForgeGame.Systems
{
    public static class ScoreSystem
    {
        /// <summary>
        /// finalScore = round(
        ///   (coreHpRemaining / CORE_HP_MAX) * SCORE_CORE_WEIGHT
        /// + killCount * SCORE_KILL_WEIGHT
        /// + max(0, SCORE_TIME_PAR_SEC - clearTimeSec) * SCORE_TIME_WEIGHT )
        /// coreHpRemaining は敗北時 0（RunResult 側で 0 が入る前提）。
        /// </summary>
        public static int ComputeFinalScore(RunResult r)
        {
            float coreTerm = ((float)r.CoreHpRemaining / GameConfig.Core.HpMax) * GameConfig.Score.CoreWeight;
            float killTerm = r.KillCount * GameConfig.Score.KillWeight;
            float timeTerm = Math.Max(0f, GameConfig.Score.TimeParSec - r.ClearTimeSec) * GameConfig.Score.TimeWeight;
            return (int)Math.Round(coreTerm + killTerm + timeTerm, MidpointRounding.AwayFromZero);
        }
    }
}
