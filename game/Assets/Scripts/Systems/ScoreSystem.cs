// ScoreSystem — pure C# score math (gdd スコア算出). Engine-independent: no MonoBehaviour,
// no scene API, no I/O. Exercised by EditMode tests as the compile/typecheck canary.
using System;

namespace ForgeGame.Systems
{
    public static class ScoreSystem
    {
        /// <summary>Final score at Result (gdd 数値表 finalScore 擬似コード).</summary>
        public static int ComputeFinalScore(
            float survivalTimeSec, int normalKillCount, int heavyKillCount, int crystalsCollected)
        {
            return (int)Math.Floor(survivalTimeSec * GameConfig.Score.PerSecondSurvived)
                   + normalKillCount * GameConfig.Score.PerKillNormal
                   + heavyKillCount * GameConfig.Score.PerKillHeavy
                   + crystalsCollected * GameConfig.Score.PerCrystal;
        }

        /// <summary>Current wave from elapsed seconds (gdd: 1 + floor(elapsed / WAVE_DURATION)).</summary>
        public static int CurrentWave(float elapsedSec)
        {
            return 1 + (int)Math.Floor(elapsedSec / GameConfig.Wave.WaveDuration);
        }
    }
}
