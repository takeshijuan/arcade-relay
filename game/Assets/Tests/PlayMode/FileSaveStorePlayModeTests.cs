// FileSaveStorePlayModeTests.cs — S-07 acceptance:
// 「Persistence/FileSaveStore が persistentDataPath/save.json へ .tmp 経由のアトミック書込で保存し、
//  新規インスタンスで再ロードすると全 SaveData フィールドが一致する（保存→再起動相当→復元一致）。
//  破損データ（(a)パース不能、(b)ロード可能だがスキーマ不正=save_version 欠落 または必須フィールド型不正
//  を最低1件ずつ）投入時は黙って初期化せず、生データを save.json.bak.<UTC> へ退避し
//  Debug.LogError("[SaveCorruption] ...") を1回だけ出力、既定値を再生成して recovered=true を返す。
//  PlayMode テストは temporaryCachePath の一時ファイルで復元一致と破損復旧（.bak 生成・
//  [SaveCorruption] ログ1回・recovered=true）の両方を検証する」を検証する。
//
// テストは Application.temporaryCachePath 配下の一意ディレクトリのみを使い、実ユーザーの
// persistentDataPath は一切触らない（tech-stack-unity.md「セーブ / 永続化」テスト規約）。
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.PlayMode
{
    public class FileSaveStorePlayModeTests
    {
        private string testDir;
        private const string TestFileName = "save.json";

        [SetUp]
        public void SetUp()
        {
            testDir = Path.Combine(Application.temporaryCachePath, "forgegame-savetest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(testDir) && Directory.Exists(testDir))
            {
                Directory.Delete(testDir, recursive: true);
            }
        }

        private string SavePath => Path.Combine(testDir, TestFileName);

        private static SaveData CreateDistinctSaveData() => new SaveData
        {
            save_version = GameConfig.Save.CurrentVersion,
            highScore = 4321,
            bestClearTimeSec = 187.5f,
            totalRunsPlayed = 7,
            totalWins = 3,
            totalKills = 256,
            essence = 190,
            upgStartingGoldLv = 2,
            upgTowerDiscountLv = 1,
            upgEssenceRateLv = 3,
            achFirstVictory = true,
            achPerfectDefense = false,
            achCenturySlayer = true,
            achAoeSpecialist = false,
            achFrugalArchitect = true,
        };

        private static void AssertSaveDataEqual(SaveData expected, SaveData actual)
        {
            Assert.AreEqual(expected.save_version, actual.save_version);
            Assert.AreEqual(expected.highScore, actual.highScore);
            Assert.AreEqual(expected.bestClearTimeSec, actual.bestClearTimeSec);
            Assert.AreEqual(expected.totalRunsPlayed, actual.totalRunsPlayed);
            Assert.AreEqual(expected.totalWins, actual.totalWins);
            Assert.AreEqual(expected.totalKills, actual.totalKills);
            Assert.AreEqual(expected.essence, actual.essence);
            Assert.AreEqual(expected.upgStartingGoldLv, actual.upgStartingGoldLv);
            Assert.AreEqual(expected.upgTowerDiscountLv, actual.upgTowerDiscountLv);
            Assert.AreEqual(expected.upgEssenceRateLv, actual.upgEssenceRateLv);
            Assert.AreEqual(expected.achFirstVictory, actual.achFirstVictory);
            Assert.AreEqual(expected.achPerfectDefense, actual.achPerfectDefense);
            Assert.AreEqual(expected.achCenturySlayer, actual.achCenturySlayer);
            Assert.AreEqual(expected.achAoeSpecialist, actual.achAoeSpecialist);
            Assert.AreEqual(expected.achFrugalArchitect, actual.achFrugalArchitect);
        }

        [Test]
        public void Load_NoFileYet_ReturnsDefault_NotRecovered()
        {
            var store = new FileSaveStore(testDir, TestFileName);

            LoadResult result = store.Load();

            Assert.IsFalse(result.Recovered);
            AssertSaveDataEqual(SaveData.CreateDefault(), result.Data);
        }

        [Test]
        public void Save_WritesAtomically_NoTempFileLeftBehind_AndFileExists()
        {
            var store = new FileSaveStore(testDir, TestFileName);

            store.Save(CreateDistinctSaveData());

            Assert.IsTrue(File.Exists(SavePath), "save.json が書き込まれていない");
            Assert.IsFalse(File.Exists(SavePath + ".tmp"), ".tmp 中間ファイルが残ってしまっている（アトミック書込が未完了）");
        }

        [Test]
        public void SaveThenLoad_WithNewStoreInstance_RestoresAllFields()
        {
            SaveData original = CreateDistinctSaveData();

            // 「保存」フェーズ: 1つ目のインスタンス。
            var writer = new FileSaveStore(testDir, TestFileName);
            writer.Save(original);

            // 「新規インスタンスで再ロード」フェーズ（プロセス再起動相当）。
            var reader = new FileSaveStore(testDir, TestFileName);
            LoadResult result = reader.Load();

            Assert.IsFalse(result.Recovered, "正常なセーブのロードで recovered=true になってしまっている");
            AssertSaveDataEqual(original, result.Data);
        }

        [Test]
        public void Load_UnparsableJson_TriggersCorruptionProtocol_BackupAndDefaultAndRecovered()
        {
            // (a) パース不能: 構文として壊れた JSON。
            const string garbage = "{ this is not valid json at all ][";
            File.WriteAllText(SavePath, garbage);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveCorruption\] reason=ParseFailed backup="));

            var store = new FileSaveStore(testDir, TestFileName);
            LoadResult result = store.Load();

            Assert.IsTrue(result.Recovered, "パース不能セーブのロードで recovered=true にならない");
            AssertSaveDataEqual(SaveData.CreateDefault(), result.Data);

            string[] backups = Directory.GetFiles(testDir, TestFileName + ".bak.*");
            Assert.AreEqual(1, backups.Length, "破損データの .bak 退避ファイルが1つだけ生成されていない");
            Assert.AreEqual(garbage, File.ReadAllText(backups[0]), ".bak に退避された生データが元の破損データと一致しない");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_SchemaInvalid_MissingSaveVersion_TriggersCorruptionProtocol_BackupAndDefaultAndRecovered()
        {
            // (b) ロード可能だがスキーマ不正: 構文的には妥当な JSON だが save_version キーが欠落している
            // （JsonUtility は未知/欠落フィールドを黙って既定値 0 にする → MetaSchema.Validate が
            //  VersionMissing と判定する）。
            const string missingVersionJson = "{\"highScore\":999,\"essence\":42}";
            File.WriteAllText(SavePath, missingVersionJson);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveCorruption\] reason=VersionMissing backup="));

            var store = new FileSaveStore(testDir, TestFileName);
            LoadResult result = store.Load();

            Assert.IsTrue(result.Recovered, "スキーマ不正セーブのロードで recovered=true にならない");
            AssertSaveDataEqual(SaveData.CreateDefault(), result.Data);

            string[] backups = Directory.GetFiles(testDir, TestFileName + ".bak.*");
            Assert.AreEqual(1, backups.Length, "破損データの .bak 退避ファイルが1つだけ生成されていない");
            Assert.AreEqual(missingVersionJson, File.ReadAllText(backups[0]), ".bak に退避された生データが元の破損データと一致しない");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_SchemaInvalid_NegativeField_TriggersCorruptionProtocol()
        {
            // (b) の別インスタンス: 構文は妥当だが必須フィールドの値域が不正（負のカウンタ＝型として
            // 意味を成さない値）。MetaSchema.Validate が SchemaInvalid と判定する。
            string invalidJson = $"{{\"save_version\":{GameConfig.Save.CurrentVersion},\"essence\":-5}}";
            File.WriteAllText(SavePath, invalidJson);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveCorruption\] reason=SchemaInvalid backup="));

            var store = new FileSaveStore(testDir, TestFileName);
            LoadResult result = store.Load();

            Assert.IsTrue(result.Recovered);
            AssertSaveDataEqual(SaveData.CreateDefault(), result.Data);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_AfterCorruptionRecovery_SavingDefaultThenReloading_RoundTripsCleanly()
        {
            // 破損復旧後、既定値を保存→再ロードしても正常に往復できることを確認する
            // （復旧プロトコルが save.json 自体を壊れたままにしていないか）。
            File.WriteAllText(SavePath, "not json");
            LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveCorruption\]"));

            var store = new FileSaveStore(testDir, TestFileName);
            LoadResult recovered = store.Load();
            Assert.IsTrue(recovered.Recovered);

            store.Save(recovered.Data);

            var reader = new FileSaveStore(testDir, TestFileName);
            LoadResult reloaded = reader.Load();

            Assert.IsFalse(reloaded.Recovered);
            AssertSaveDataEqual(SaveData.CreateDefault(), reloaded.Data);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
