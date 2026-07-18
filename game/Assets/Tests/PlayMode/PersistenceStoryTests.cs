// PersistenceStoryTests — S-01: メタ進行の永続化基盤. Verifies FileSaveAdapter round-trips a
// non-default SaveData across independent instances, and that a corrupted save is retired to a
// save.json.bak.<UTC> file with exactly one [SaveCorruption] error (LogAssert.Expect) while the
// returned SaveData falls back to defaults with Corrupt status (⇒ recovered=true upstream).
// Uses Application.temporaryCachePath per-test directories (never persistentDataPath) and cleans
// up in TearDown (tech-stack-unity.md "セーブ / 永続化" テスト規約).
using System;
using System.IO;
using System.Text.RegularExpressions;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class PersistenceStoryTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Application.temporaryCachePath, "s01-save-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void Save_ThenLoadWithNewAdapterInstance_RoundTripsAllFields()
        {
            var original = new SaveData
            {
                highScore = 4200,
                bestSurvivalTimeSec = 123.5f,
                bestWaveReached = 7,
                totalRunsPlayed = 3,
                totalKillCount = 55,
                totalCrystalsEarned = 210,
                crystalBalance = 88,
                upgradeAttackLevel = 2,
                upgradeMoveSpeedLevel = 1,
                upgradeMaxHpLevel = 3,
                bgmVolume = 0.4f,
                sfxVolume = 0.6f,
            };

            var writer = new FileSaveAdapter(_tempDir);
            writer.Save(original);

            // New instance — must not rely on any in-memory state from `writer`.
            var reader = new FileSaveAdapter(_tempDir);
            SaveLoadOutcome outcome = reader.Load();

            Assert.AreEqual(SaveLoadStatus.Ok, outcome.Status);
            SaveData loaded = outcome.Data;
            Assert.AreEqual(original.save_version, loaded.save_version);
            Assert.AreEqual(original.highScore, loaded.highScore);
            Assert.AreEqual(original.bestSurvivalTimeSec, loaded.bestSurvivalTimeSec);
            Assert.AreEqual(original.bestWaveReached, loaded.bestWaveReached);
            Assert.AreEqual(original.totalRunsPlayed, loaded.totalRunsPlayed);
            Assert.AreEqual(original.totalKillCount, loaded.totalKillCount);
            Assert.AreEqual(original.totalCrystalsEarned, loaded.totalCrystalsEarned);
            Assert.AreEqual(original.crystalBalance, loaded.crystalBalance);
            Assert.AreEqual(original.upgradeAttackLevel, loaded.upgradeAttackLevel);
            Assert.AreEqual(original.upgradeMoveSpeedLevel, loaded.upgradeMoveSpeedLevel);
            Assert.AreEqual(original.upgradeMaxHpLevel, loaded.upgradeMaxHpLevel);
            Assert.AreEqual(original.bgmVolume, loaded.bgmVolume);
            Assert.AreEqual(original.sfxVolume, loaded.sfxVolume);
        }

        [Test]
        public void Load_MissingSaveVersionKey_RetiresBackupLogsOnceAndReturnsDefaultCorrupt()
        {
            var adapter = new FileSaveAdapter(_tempDir);
            // Genuine key omission (acceptance: "save_version 欠落") — no "save_version" key at
            // all. This is only detectable as corruption because MetaTypes.cs's save_version
            // field defaults to the sentinel 0, not GameConfig.Save.CurrentSchemaVersion: if the
            // field initializer were the current version, JsonUtility.FromJson would fill the
            // missing key with that current-version default and this save would silently load as
            // valid instead of tripping MetaSchema.Normalize's `save_version <= 0` check
            // (CR-CODE S-01 finding — fixed by the MetaTypes.cs/FileSaveAdapter.Save sentinel
            // change; previously this test used an explicit 0 as a stand-in for this case).
            File.WriteAllText(adapter.SavePath, "{\"highScore\":999}");

            AssertCorruptionProtocol(adapter);
        }

        [Test]
        public void Load_ExplicitZeroSaveVersion_RetiresBackupLogsOnceAndReturnsDefaultCorrupt()
        {
            var adapter = new FileSaveAdapter(_tempDir);
            // Explicit invalid sentinel value (e.g. a hand-truncated/corrupted save.json), distinct
            // from the genuine key-omission case above — both must trip the same protocol.
            File.WriteAllText(adapter.SavePath, "{\"save_version\":0,\"highScore\":999}");

            AssertCorruptionProtocol(adapter);
        }

        [Test]
        public void Load_UnparsableJson_RetiresBackupLogsOnceAndReturnsDefaultCorrupt()
        {
            var adapter = new FileSaveAdapter(_tempDir);
            File.WriteAllText(adapter.SavePath, "{this is not valid json");

            AssertCorruptionProtocol(adapter);
        }

        [Test]
        public void Load_FutureSaveVersion_RetiresBackupLogsOnceAndReturnsDefaultCorrupt()
        {
            var adapter = new FileSaveAdapter(_tempDir);
            var future = SaveData.CreateDefault();
            future.save_version = GameConfig.Save.CurrentSchemaVersion + 1;
            future.highScore = 12345; // must NOT survive into the returned (defaulted) SaveData
            File.WriteAllText(adapter.SavePath, JsonUtility.ToJson(future));

            AssertCorruptionProtocol(adapter);
        }

        [Test]
        public void Load_SchemaInvalidNegativeHighScore_RetiresBackupLogsOnceAndReturnsDefaultCorrupt()
        {
            var adapter = new FileSaveAdapter(_tempDir);
            // Valid JSON with the CURRENT save_version but a range-invalid field (highScore=-5) —
            // MetaSchema.Normalize's IsSchemaValid check ("schema-invalid" reason,
            // Tests/EditMode/MetaSchemaTests covers the pure layer) must trip the exact same 3-point
            // protocol as parse/version corruption (rules/unity-code.md: スキーマ検証失敗も
            // .bak 退避 + [SaveCorruption] + 既定値再生成の3点セット).
            var invalid = SaveData.CreateDefault();
            invalid.highScore = -5;
            File.WriteAllText(adapter.SavePath, JsonUtility.ToJson(invalid));

            AssertCorruptionProtocol(adapter);
        }

        private static void AssertCorruptionProtocol(FileSaveAdapter adapter)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveCorruption\]"));

            SaveLoadOutcome outcome = adapter.Load();

            Assert.AreEqual(SaveLoadStatus.Corrupt, outcome.Status, "corrupted load must report Corrupt status");
            bool recovered = outcome.Status == SaveLoadStatus.Corrupt;
            Assert.IsTrue(recovered, "Corrupt status is what upstream (BootLoader/SessionHolder) maps to recovered=true");

            // Defaults only — no corrupted-field leakage into the returned SaveData.
            Assert.AreEqual(0, outcome.Data.highScore);
            Assert.AreEqual(GameConfig.Save.CurrentSchemaVersion, outcome.Data.save_version);

            string saveDir = Path.GetDirectoryName(adapter.SavePath);
            string[] backups = Directory.GetFiles(saveDir!, Path.GetFileName(adapter.SavePath) + ".bak.*");
            Assert.AreEqual(1, backups.Length, "expected exactly one save.json.bak.<UTC> file");
        }
    }
}
