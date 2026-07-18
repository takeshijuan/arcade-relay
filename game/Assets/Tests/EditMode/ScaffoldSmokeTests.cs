// EditMode smoke tests — double as the compile/typecheck canary (tech-stack-unity 検証コマンド).
// Exercise the engine-independent Systems so a compile break in the pure core fails the run.
using ForgeGame;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class ScaffoldSmokeTests
    {
        [Test]
        public void ScoreSystem_ComputesFinalScoreFromGddFormula()
        {
            // 10s survived (10/s) + 2 normal kills (25) + 1 heavy (60) + 3 crystals (5)
            int score = ScoreSystem.ComputeFinalScore(10f, 2, 1, 3);
            Assert.AreEqual(100 + 50 + 60 + 15, score);
        }

        [Test]
        public void ScoreSystem_WaveIsOneBasedFromElapsedSeconds()
        {
            Assert.AreEqual(1, ScoreSystem.CurrentWave(0f));
            Assert.AreEqual(2, ScoreSystem.CurrentWave(GameConfig.Wave.WaveDuration));
        }

        [Test]
        public void MetaProgression_ApplyRunResultUpdatesHighScoreAndBalance()
        {
            var save = SaveData.CreateDefault();
            var run = new RunResult
            {
                FinalScore = 500,
                SurvivalTimeSec = 42f,
                WaveReached = 4,
                NormalKillCount = 7,
                HeavyKillCount = 1,
                CrystalsCollected = 12,
            };
            SaveData next = MetaProgression.ApplyRunResult(save, run);

            Assert.AreEqual(500, next.highScore);
            Assert.AreEqual(4, next.bestWaveReached);
            Assert.AreEqual(12, next.crystalBalance);
            Assert.AreEqual(1, next.totalRunsPlayed);
            Assert.AreEqual(8, next.totalKillCount);
            // Original save is untouched (pure reducer).
            Assert.AreEqual(0, save.highScore);
        }

        [Test]
        public void MetaProgression_PurchaseFailsWhenInsufficientBalance()
        {
            var save = SaveData.CreateDefault(); // balance 0
            var result = MetaProgression.TryPurchase(save, MetaProgression.UpgradeKind.Attack);
            Assert.IsFalse(result.Purchased);
            Assert.AreEqual(0, result.Data.upgradeAttackLevel);
        }

        [Test]
        public void MetaProgression_IsNewHighScore_TrueOnlyWhenRunExceedsPriorHighScore()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 500;

            var higher = new RunResult { FinalScore = 501 };
            var equal = new RunResult { FinalScore = 500 };
            var lower = new RunResult { FinalScore = 499 };

            Assert.IsTrue(MetaProgression.IsNewHighScore(save, higher));
            Assert.IsFalse(MetaProgression.IsNewHighScore(save, equal), "a tie is not an update");
            Assert.IsFalse(MetaProgression.IsNewHighScore(save, lower));
        }

        [Test]
        public void MetaSchema_FutureVersionIsCorrupt()
        {
            var future = SaveData.CreateDefault();
            future.save_version = GameConfig.Save.CurrentSchemaVersion + 1;
            var outcome = MetaSchema.Normalize(future);
            Assert.AreEqual(SaveLoadStatus.Corrupt, outcome.Status);
        }
    }
}
