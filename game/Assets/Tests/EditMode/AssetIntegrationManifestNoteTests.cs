// AssetIntegrationManifestNoteTests — CR-CODE s-18 iter1 major指摘#2: S-18 acceptance requires that a
// hero Avatar Generic-degrade is "MANIFEST に注記" (not just logged). This exercises
// Editor/AssetIntegration.BuildManifestDegradationNoteLine / AppendManifestDegradationNote directly.
//
// Why EditMode and not PlayMode: the degrade path lives entirely in ModelImporter/AssetDatabase
// batchmode-import logic (Editor-only assembly — ForgeGame.PlayMode.Tests' asmdef does not reference
// ForgeGame.Editor, only ForgeGame.EditMode.Tests does, mirroring Tests/EditMode/AudioMixerSetupTests.cs'
// own EditMode/PlayMode split for the same reason). It is also not something that can be forced to fail
// deterministically in this project without corrupting the shipping hero FBX (the real Avatar generation
// currently succeeds — see state/active.md S-18 entry), so this test verifies the note-writing logic in
// isolation with an explicit, testable path (mirrors AudioMixerSetup.EnsureMixer's explicit-path
// testability pattern) rather than the full ConfigureHeroModel import pipeline.
using System;
using System.IO;
using ForgeGame.EditorTools;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class AssetIntegrationManifestNoteTests
    {
        private string _tempManifestPath;

        [SetUp]
        public void CreateTempManifestPath()
        {
            _tempManifestPath = Path.Combine(Path.GetTempPath(), "s18-manifest-note-test-" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void DeleteTempManifest()
        {
            if (_tempManifestPath != null && File.Exists(_tempManifestPath))
            {
                File.Delete(_tempManifestPath);
            }
        }

        [Test]
        public void BuildManifestDegradationNoteLine_ProducesSingleLineJsonWithRequiredFields()
        {
            string line = AssetIntegration.BuildManifestDegradationNoteLine(
                "MDL-01", "_generated/models/model-hero.fbx", "deadbeef",
                "Humanoid Avatar generation failed — fell back to Generic.", "S-18",
                new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));

            Assert.IsFalse(line.Contains("\n") || line.Contains("\r"), "MANIFEST.jsonl requires exactly one JSON object per line");
            Assert.IsTrue(line.StartsWith("{") && line.EndsWith("}"), "must be a JSON object");
            StringAssert.Contains("\"asset_id\":\"MDL-01\"", line);
            StringAssert.Contains("\"file\":\"_generated/models/model-hero.fbx\"", line);
            StringAssert.Contains("\"note\":\"integration_degradation\"", line);
            StringAssert.Contains("\"revision_of_sha256\":\"deadbeef\"", line);
            StringAssert.Contains("\"story\":\"S-18\"", line);
            StringAssert.Contains("\"generated_at\":\"2026-07-13T00:00:00Z\"", line);
            StringAssert.Contains("fell back to Generic", line);
        }

        [Test]
        public void BuildManifestDegradationNoteLine_EscapesQuotesAndBackslashesInReason()
        {
            string line = AssetIntegration.BuildManifestDegradationNoteLine(
                "MDL-01", "_generated/models/model-hero.fbx", "deadbeef",
                "reason with \"quotes\" and a \\backslash\\", "S-18", DateTime.UtcNow);

            // Must remain valid single-line JSON (no unescaped quote breaks the object out early).
            StringAssert.Contains("reason with \\\"quotes\\\" and a \\\\backslash\\\\", line);
        }

        [Test]
        public void AppendManifestDegradationNote_AppendsWithoutOverwritingExistingContent()
        {
            File.WriteAllText(_tempManifestPath, "{\"file\":\"_generated/images/img-01-hero-concept.png\",\"pre_existing\":true}" + Environment.NewLine);

            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef",
                "Humanoid Avatar generation failed — fell back to Generic.");

            string[] lines = File.ReadAllLines(_tempManifestPath);
            Assert.AreEqual(2, lines.Length, "append must preserve the pre-existing line and add exactly one new line (rules/assets.md: 既存行の書き換え・削除も禁止・追記のみ)");
            StringAssert.Contains("pre_existing", lines[0]);
            StringAssert.Contains("\"asset_id\":\"MDL-01\"", lines[1]);
            StringAssert.Contains("\"note\":\"integration_degradation\"", lines[1]);
        }

        [Test]
        public void AppendManifestDegradationNote_CreatesFileAndParentDirectoryWhenMissing()
        {
            string nestedPath = Path.Combine(Path.GetTempPath(), "s18-manifest-note-test-dir-" + Guid.NewGuid().ToString("N"), "MANIFEST.jsonl");
            try
            {
                AssetIntegration.AppendManifestDegradationNote(
                    nestedPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "test reason");

                Assert.IsTrue(File.Exists(nestedPath));
                StringAssert.Contains("\"asset_id\":\"MDL-01\"", File.ReadAllText(nestedPath));
            }
            finally
            {
                if (File.Exists(nestedPath))
                {
                    File.Delete(nestedPath);
                }
                string dir = Path.GetDirectoryName(nestedPath);
                if (dir != null && Directory.Exists(dir))
                {
                    Directory.Delete(dir);
                }
            }
        }

        // CR-CODE s-18 iter2 minor指摘#1: re-running IntegrateAll while the same degradation persists
        // (e.g. integrate→QA re-run) must not pile up timestamp-only-different duplicate rows.
        [Test]
        public void AppendManifestDegradationNote_SkipsWhenByteForByteIdenticalNoteAlreadyExists()
        {
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "same reason", "S-18");
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "same reason", "S-18");

            string[] lines = File.ReadAllLines(_tempManifestPath);
            Assert.AreEqual(1, lines.Length, "byte-for-byte identical (asset_id, revision_of_sha256, revision_reason, story) degradation note must be deduped, not appended again");
            StringAssert.Contains("same reason", lines[0]);
        }

        // CR-CODE s-21 iter2 minor指摘#3: a *different* reason recorded against the same unchanged file
        // bytes (e.g. a second, distinct root cause discovered on a later run against the same still-broken
        // raw asset) must not be silently swallowed by the dedupe — the old (asset_id, revision_of_sha256)-only
        // key used to drop this on the floor with no durable record.
        [Test]
        public void AppendManifestDegradationNote_AppendsAgainWhenReasonDiffersEvenIfShaIsSame()
        {
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "first run reason");
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "second run reason (different text, same asset_id+sha)");

            string[] lines = File.ReadAllLines(_tempManifestPath);
            Assert.AreEqual(2, lines.Length, "a genuinely different revision_reason against the same (asset_id, sha256) must still be recorded, not deduped away");
            StringAssert.Contains("first run reason", lines[0]);
            StringAssert.Contains("second run reason", lines[1]);
        }

        // CR-CODE s-21 iter2 minor指摘#3: a different story attributing degradation to the same
        // (asset_id, sha256, reason) must also be recorded, not deduped away by story alone.
        [Test]
        public void AppendManifestDegradationNote_AppendsAgainWhenStoryDiffersEvenIfReasonAndShaAreSame()
        {
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "shared reason text", "S-18");
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "shared reason text", "S-21");

            string[] lines = File.ReadAllLines(_tempManifestPath);
            Assert.AreEqual(2, lines.Length, "a different story against the same (asset_id, sha256, reason) must still be recorded");
        }

        [Test]
        public void AppendManifestDegradationNote_AppendsAgainWhenShaDiffers()
        {
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "reason for revision A");
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "c0ffee", "reason for revision B (hero FBX regenerated)");

            string[] lines = File.ReadAllLines(_tempManifestPath);
            Assert.AreEqual(2, lines.Length, "a genuinely new revision (different sha256) must still be recorded");
        }

        [Test]
        public void ManifestHasDegradationNote_FalseWhenManifestFileDoesNotExist()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), "s18-manifest-note-test-missing-" + Guid.NewGuid().ToString("N") + ".jsonl");
            Assert.IsFalse(AssetIntegration.ManifestHasDegradationNote(missingPath, "MDL-01", "deadbeef", "reason", "S-18"));
        }

        // CR-CODE s-21 iter2 minor指摘#3: the widened dedupe key must actually distinguish a differing
        // reason/story, not just accept whatever is passed — exercised directly against the helper (not just
        // through AppendManifestDegradationNote) so the key composition itself is covered.
        [Test]
        public void ManifestHasDegradationNote_FalseWhenOnlyReasonDiffers()
        {
            AssetIntegration.AppendManifestDegradationNote(
                _tempManifestPath, "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "reason A", "S-18");

            Assert.IsFalse(AssetIntegration.ManifestHasDegradationNote(_tempManifestPath, "MDL-01", "deadbeef", "reason B", "S-18"));
            Assert.IsTrue(AssetIntegration.ManifestHasDegradationNote(_tempManifestPath, "MDL-01", "deadbeef", "reason A", "S-18"));
        }

        [Test]
        public void BuildManifestDegradationNoteLine_FormatsUtcTimestampWithInvariantCulture()
        {
            // CR-CODE s-18 iter2 minor指摘#2: must not depend on the current thread culture.
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA"); // non-Gregorian calendar culture
                string line = AssetIntegration.BuildManifestDegradationNoteLine(
                    "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "reason",
                    "S-18", new DateTime(2026, 7, 13, 9, 30, 0, DateTimeKind.Utc));

                StringAssert.Contains("\"generated_at\":\"2026-07-13T09:30:00Z\"", line);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void BuildManifestDegradationNoteLine_NormalizesUnspecifiedAndLocalKindToUtc()
        {
            // CR-CODE s-21 iter2 minor指摘#5: run under the same non-Gregorian-calendar current-culture
            // stress as BuildManifestDegradationNoteLine_FormatsUtcTimestampWithInvariantCulture, and pin
            // InvariantCulture on the *expected*-value side too — without this, the expected string itself
            // (not just the production code under test) would get culture-formatted digits on such a
            // machine/CI, producing a false-positive failure that has nothing to do with the Local/Unspecified
            // normalization this test is meant to verify.
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA"); // non-Gregorian calendar culture

                string unspecifiedLine = AssetIntegration.BuildManifestDegradationNoteLine(
                    "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "reason", "S-18",
                    new DateTime(2026, 7, 13, 9, 30, 0, DateTimeKind.Unspecified));
                StringAssert.Contains("\"generated_at\":\"2026-07-13T09:30:00Z\"", unspecifiedLine);

                // Note (residual limitation, not fully closed by this fix): this asserts against
                // TimeZoneInfo.Local's *actual* offset via DateTime.ToUniversalTime(), which is the same
                // conversion the production code performs — so it correctly exercises the Kind normalization
                // on any machine. It cannot, however, distinguish "ToUniversalTime() was called" from "it was
                // a no-op" on a CI runner whose system TZ happens to be UTC (Local == Utc there); closing that
                // gap would need injecting a fixed, non-UTC TimeZoneInfo into the production method, which is
                // out of scope for this CR-CODE fix.
                var localTime = new DateTime(2026, 7, 13, 9, 30, 0, DateTimeKind.Local);
                string localLine = AssetIntegration.BuildManifestDegradationNoteLine(
                    "MDL-01", "_generated/models/model-hero.fbx", "deadbeef", "reason", "S-18", localTime);
                string expectedUtc = localTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
                StringAssert.Contains("\"generated_at\":\"" + expectedUtc + "\"", localLine);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void BuildManifestDegradationNoteLine_EscapesControlCharactersInReason()
        {
            string reasonWithControlChar = "reason with a\ttab and a " + (char)0x01 + "control char";
            string line = AssetIntegration.BuildManifestDegradationNoteLine(
                "MDL-01", "_generated/models/model-hero.fbx", "deadbeef",
                reasonWithControlChar, "S-18", DateTime.UtcNow);

            Assert.IsFalse(line.Contains("\t"), "raw tab must not appear unescaped in a JSONL line");
            Assert.IsFalse(line.Contains(((char)0x01).ToString()), "raw 0x01 control byte must not appear unescaped in a JSONL line");
            StringAssert.Contains("reason with a\\ttab and a \\u0001control char", line);
        }

        // CR-CODE s-18 iter2 minor指摘#4: exercise the ConfigureHeroModel wiring itself (path + sha256 +
        // AppendManifestDegradationNote call), not just the two leaf helpers.
        [Test]
        public void RecordHeroAvatarDegradation_AppendsNoteWithComputedShaWhenRawFileExists()
        {
            string rawModelPath = Path.Combine(Path.GetTempPath(), "s18-raw-model-" + Guid.NewGuid().ToString("N") + ".fbx");
            File.WriteAllText(rawModelPath, "fake-fbx-bytes");
            try
            {
                AssetIntegration.RecordHeroAvatarDegradation(_tempManifestPath, rawModelPath);

                string content = File.ReadAllText(_tempManifestPath);
                StringAssert.Contains("\"asset_id\":\"MDL-01\"", content);
                StringAssert.Contains("\"note\":\"integration_degradation\"", content);
                StringAssert.DoesNotContain("unknown-source-file-missing", content);
            }
            finally
            {
                File.Delete(rawModelPath);
            }
        }

        [Test]
        public void RecordHeroAvatarDegradation_UsesSentinelShaWhenRawFileMissing()
        {
            string missingRawModelPath = Path.Combine(Path.GetTempPath(), "s18-raw-model-missing-" + Guid.NewGuid().ToString("N") + ".fbx");

            AssetIntegration.RecordHeroAvatarDegradation(_tempManifestPath, missingRawModelPath);

            string content = File.ReadAllText(_tempManifestPath);
            StringAssert.Contains("\"revision_of_sha256\":\"unknown-source-file-missing\"", content);
        }
    }
}
