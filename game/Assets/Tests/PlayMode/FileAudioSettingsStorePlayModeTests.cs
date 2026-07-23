// FileAudioSettingsStorePlayModeTests.cs — S-16 CR-CODE iter1 #3（minor・test-coverage。
// iter2 #4 で番号をs-16.md記載順に修正: 旧#2→#3）対応。
// FileSaveStorePlayModeTests.cs（S-07）と同型の構成で、本番既定ストア FileAudioSettingsStore の
// 実 File I/O 経路を検証する（従来は InMemoryAudioSettingsStore + GameFlow キャリーの
// MenuScreenPlayModeTests のみがカバーしており、production パスが未検証だった非対称を解消する）。
//
// 検証範囲:
// - 保存→新規インスタンスで再ロード→復元一致（.tmp 中間ファイルが残らないアトミック書込）
// - 値域クランプ（Mathf.Clamp01。手動改変ファイル対策）
// - 「パース可能だが不正」な入力（`{}`・必須フィールド欠落・非JSON切詰め）を黙って0.0採用せず
//   Warning 1回 + 既定値へフォールバックすること（CR-CODE iter1 #1 の major 修正の再帰防止）
// - CR-CODE iter2 #3（minor）対応: 上記フィクスチャは全て Load() のプリチェック段
//   （LooksLikeJsonObject/HasRequiredFields/HasNumericFieldTypes）で弾かれ、JsonUtility.FromJson の
//   try/catch 経路（構文的に不正で FromJson 自体が例外を投げるケース）が一度も実行されないという
//   カバレッジの穴があった。プリチェックを全て通過するが末尾カンマで構文的に不正な入力を追加し、
//   catch 経路（Warning「failed to parse」+ 既定値）を検証する
//   （Unity 未起動の並走レーン中の追加のため、JsonUtility が実際に例外を投げるという前提はバッチ検証で
//   最終確認する — static Read/Grep では検証不能）。
// - Save(null) が ArgumentNullException を投げること
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

namespace ForgeGame.Tests.PlayMode
{
    public class FileAudioSettingsStorePlayModeTests
    {
        private string testDir;
        private const string TestFileName = "audio-settings.json";

        [SetUp]
        public void SetUp()
        {
            testDir = Path.Combine(Application.temporaryCachePath, "forgegame-audiosettingstest-" + Guid.NewGuid().ToString("N"));
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

        [Test]
        public void Load_NoFileYet_ReturnsDefault()
        {
            var store = new FileAudioSettingsStore(testDir, TestFileName);

            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.BgmVolume, 0.001f);
            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f);
        }

        [Test]
        public void Save_WritesAtomically_NoTempFileLeftBehind_AndFileExists()
        {
            var store = new FileAudioSettingsStore(testDir, TestFileName);

            store.Save(new AudioSettingsData { BgmVolume = 0.4f, SfxVolume = 0.7f });

            Assert.IsTrue(File.Exists(SavePath), "audio-settings.json が書き込まれていない");
            Assert.IsFalse(File.Exists(SavePath + ".tmp"), ".tmp 中間ファイルが残ってしまっている（アトミック書込が未完了）");
        }

        [Test]
        public void SaveThenLoad_WithNewStoreInstance_RestoresValues()
        {
            var writer = new FileAudioSettingsStore(testDir, TestFileName);
            writer.Save(new AudioSettingsData { BgmVolume = 0.25f, SfxVolume = 0.85f });

            // 新規インスタンスで再ロード（プロセス再起動相当）。
            var reader = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = reader.Load();

            Assert.AreEqual(0.25f, result.BgmVolume, 0.001f);
            Assert.AreEqual(0.85f, result.SfxVolume, 0.001f);
        }

        [Test]
        public void Load_OutOfRangeValues_AreClampedTo01()
        {
            File.WriteAllText(SavePath, "{\"BgmVolume\":5.0,\"SfxVolume\":-2.0}");

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(1f, result.BgmVolume, 0.001f, "手動改変ファイルの範囲外値がクランプされていない(Bgm)");
            Assert.AreEqual(0f, result.SfxVolume, 0.001f, "手動改変ファイルの範囲外値がクランプされていない(Sfx)");
        }

        [Test]
        public void Load_UnparsableJson_LogsWarningOnce_AndReturnsDefault()
        {
            File.WriteAllText(SavePath, "{ this is not valid json at all ][");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[AudioSettingsLoad\]"));

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.BgmVolume, 0.001f);
            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_EmptyJsonObject_DoesNotSilentlyAdoptZeroVolume_LogsWarningAndReturnsDefault()
        {
            // CR-CODE iter1 #1（major。iter2 #4 で番号をs-16.md記載順に修正: 旧#3→#1）の再帰防止テスト:
            // JsonUtility は `{}` を例外なし・非nullで
            // BgmVolume=SfxVolume=0(float既定値) にパースしてしまう既知挙動がある。これを黙って採用すると
            // Warning すら出さずに毎起動ミュートになる。必須フィールドキーの実在チェックで防げていることを検証する。
            File.WriteAllText(SavePath, "{}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[AudioSettingsLoad\].*BgmVolume"));

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.BgmVolume, 0.001f,
                "`{}` が黙って BgmVolume=0（ミュート）として採用されてしまっている");
            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f,
                "`{}` が黙って SfxVolume=0（ミュート）として採用されてしまっている");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_MissingSfxVolumeField_TreatedAsInvalid_LogsWarningAndReturnsDefault()
        {
            // 「パース可能だが不正」: BgmVolume はあるが SfxVolume キーが欠落している。
            File.WriteAllText(SavePath, "{\"BgmVolume\":0.5}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[AudioSettingsLoad\]"));

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f,
                "SfxVolume 欠落ファイルが黙って SfxVolume=0（ミュート）として採用されてしまっている");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Save_Null_ThrowsArgumentNullException()
        {
            var store = new FileAudioSettingsStore(testDir, TestFileName);

            Assert.Throws<ArgumentNullException>(() => store.Save(null));
        }

        [Test]
        public void Load_QuotedNumericFieldValue_TreatedAsInvalid_LogsWarningAndReturnsDefault()
        {
            // CR-CODE iter2 #2（minor）再帰防止テスト: HasRequiredFields はキー実在のみを見るため、
            // 数値をクォートで括った型不一致（手動改変ファイル想定）が素通りし、JsonUtility.FromJson が
            // 例外無く既定値0（ミュート）のまま採用してしまう既知の寛容挙動を検証する。
            File.WriteAllText(SavePath, "{\"BgmVolume\":\"1.0\",\"SfxVolume\":\"0.5\"}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[AudioSettingsLoad\].*non-numeric"));

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.BgmVolume, 0.001f,
                "クォートで括られた数値がクランプ後の型不一致既定値0.0として黙って採用されてしまっている(Bgm)");
            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f,
                "クォートで括られた数値がクランプ後の型不一致既定値0.0として黙って採用されてしまっている(Sfx)");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Load_TrailingCommaSyntaxError_ThrowsDuringParse_LogsWarningAndReturnsDefault()
        {
            // CR-CODE iter2 #3（minor）再帰防止テスト: 既存の不正入力フィクスチャは全て
            // LooksLikeJsonObject/HasRequiredFields/HasNumericFieldTypes のプリチェック段で弾かれ、
            // JsonUtility.FromJson の try/catch 経路（構文エラーで FromJson 自体が例外を投げるケース）が
            // 未検証だった。全プリチェックを通過するが構文的に不正な末尾カンマを与え、catch 経路
            // （Warning「failed to parse」+ 既定値）が実行されることを検証する。
            File.WriteAllText(SavePath, "{\"BgmVolume\":0.5,\"SfxVolume\":0.5,}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[AudioSettingsLoad\] failed to parse"));

            var store = new FileAudioSettingsStore(testDir, TestFileName);
            AudioSettingsData result = store.Load();

            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.BgmVolume, 0.001f);
            Assert.AreEqual(GameConfig.Ui.MenuDefaultVolume, result.SfxVolume, 0.001f);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
