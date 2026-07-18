// DashSceneTests — S-07: ダッシュ回避（無敵窓・クールダウン・方向優先順位）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries PlayerController,
// which now also drives Systems/DashSystem) and drives it through InputTestFixture (rule 8: batchmode
// Game View has no focus, so InputAction-level assertions require the fixture's keyboard simulation).
// Frame-polls state transitions (IsDashing / IsDashInvulnerable) rather than fixed waits, mirroring
// AutoAttackSceneTests' technique — this avoids flakiness from real-time wait-duration variance.
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class DashSceneTests : InputTestFixture
    {
        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
        }

        private IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MoveInputHeld_DashMovesApproximatelyDashDistance_InTheInputDirection_AndSetsInvulnFlag()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController (Editor/SceneWiring.WireGame)");
            Assert.IsFalse(player.IsDashing);
            Assert.IsFalse(player.IsDashInvulnerable, "invuln flag must be false before any dash");

            // gdd 操作仕様: 移動+Spaceの同時押しのみ許可 — hold D (right) and dash into it.
            Press(keyboard.dKey);
            yield return null;

            Vector3 posBeforeDash = player.transform.position;

            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => player.IsDashing, 2f);
            Assert.IsTrue(player.IsDashing, "dash must activate on the frame Space is pressed while move input is held");
            Assert.IsTrue(player.IsDashInvulnerable, "invuln flag must be set the instant dash activates (DASH_INVULN_DURATION window)");

            // CR-CODE s-19 major finding (escalated across iterations, resolved here): SFX-02 (ダッシュ発動)
            // trigger->playback was tested via the shared SfxLibrary AudioSource's isPlaying flag — an
            // illusory regression guard, since any other SFX sharing the same source (e.g. an overlapping
            // auto-attack hit) would also satisfy it even if TryActivateDash's SfxLibrary.Instance.Play(...Dash)
            // call were removed. Assert the per-clip trigger counter instead (mirrors HealthSceneTests'
            // identical fix for SFX-03) — it only increments when the Dash clip specifically is played.
            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.Dash, "SFX-02 must be assigned by Editor/AssetIntegration");
            Assert.AreEqual(1, SfxLibrary.Instance.DashTriggerCountForTests,
                "SFX-02 (Dash clip specifically) must have been played exactly once by the frame dash activates");

            yield return WaitUntilOrTimeout(() => !player.IsDashing, 2f);
            Assert.IsFalse(player.IsDashing, "dash must end after DASH_DURATION");

            Vector3 posAfterDash = player.transform.position;
            Release(keyboard.dKey);
            Release(keyboard.spaceKey);

            float distance = Vector3.Distance(posBeforeDash, posAfterDash);
            float expectedDistance = GameConfig.Player.DashSpeed * GameConfig.Player.DashDuration; // ≈4m
            Assert.AreEqual(expectedDistance, distance, 0.6f,
                "dash displacement over DASH_DURATION must be ≈ DASH_SPEED×DASH_DURATION (gdd 数値表 ≈4m)");
            Assert.Greater(posAfterDash.x - posBeforeDash.x, 0f, "dash must move toward the held input direction (+X for D)");
            Assert.AreEqual(posBeforeDash.z, posAfterDash.z, 0.05f, "pure +X input must not introduce Z drift during the dash");

            // The invuln window (0.25s) is shorter than the dash window's tail here only if
            // DASH_INVULN_DURATION < time elapsed since activation; assert it eventually clears.
            yield return WaitUntilOrTimeout(() => !player.IsDashInvulnerable, 2f);
            Assert.IsFalse(player.IsDashInvulnerable, "invuln flag must clear after DASH_INVULN_DURATION");
        }

        [UnityTest]
        public IEnumerator DashPressDuringCooldown_IsIgnored_NoSecondDashOrInvulnRearm()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            Press(keyboard.dKey);
            yield return null;

            // First dash — consumes the cooldown (DASH_COOLDOWN=1.2s, well beyond DASH_DURATION=0.2s
            // and DASH_INVULN_DURATION=0.25s, so both windows will have closed by the time we probe below).
            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => player.IsDashing, 2f);
            Assert.IsTrue(player.IsDashing);
            Release(keyboard.spaceKey);

            yield return WaitUntilOrTimeout(() => !player.IsDashing, 2f);
            Assert.IsFalse(player.IsDashing);
            yield return WaitUntilOrTimeout(() => !player.IsDashInvulnerable, 2f);
            Assert.IsFalse(player.IsDashInvulnerable, "first dash's invuln window must have closed before the second press");

            Vector3 posAfterFirstDash = player.transform.position;

            // Second press, still well inside DASH_COOLDOWN — gdd: クールダウン中は無反応（入力バッファなし）.
            Press(keyboard.spaceKey);
            yield return null;
            yield return null;
            Release(keyboard.spaceKey);

            Assert.IsFalse(player.IsDashing, "a dash press during cooldown must be ignored entirely");
            Assert.IsFalse(player.IsDashInvulnerable, "an ignored dash press must not re-arm the invuln flag");

            // A frame or two of normal (non-dash) movement may have occurred while D was held, but
            // there must be no additional DASH_SPEED-scale burst on top of it.
            Vector3 posAfterSecondPress = player.transform.position;
            float driftFromIgnoredPress = Vector3.Distance(posAfterFirstDash, posAfterSecondPress);
            float maxPlausibleNormalMoveDrift = GameConfig.Player.MoveSpeed * 0.2f; // generous 2-frame bound
            Assert.LessOrEqual(driftFromIgnoredPress, maxPlausibleNormalMoveDrift,
                "an ignored dash press during cooldown must not add a DASH_SPEED-scale burst of movement");

            Release(keyboard.dKey);
        }
    }
}
