// DashTrailSceneTests — S-31: ダッシュ移動のアフターイメージ（トレイル）VFX (gdd P-01「紙一重回避」juice
// ブラッシュアップ). Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries
// DashTrailSpawner alongside PlayerController) and drives it through InputTestFixture (rule 8). Verifies
// (a) ghosts spawn at the configured cadence while dashing, (b) ghosts self-destroy after their
// lifetime, (c) no ghosts spawn outside a dash window, and (d) the pre-existing S-07 dash acceptance
// (distance/invuln/cooldown) has no regression — Systems/DashSystem and Components/PlayerController were
// not touched by this story, so this last point is asserted directly rather than re-deriving the full
// S-07 DashSceneTests suite here.
using System.Collections;
using System.Linq;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class DashTrailSceneTests : InputTestFixture
    {
        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            foreach (DashTrailGhost ghost in Object.FindObjectsByType<DashTrailGhost>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(ghost.gameObject);
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

        private static int LiveGhostCount() =>
            Object.FindObjectsByType<DashTrailGhost>(FindObjectsSortMode.None).Count(g => !g.IsExpired);

        [UnityTest]
        public IEnumerator NoDash_NoGhostsSpawn()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<DashTrailSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a DashTrailSpawner (Editor/SceneWiring.WireGame)");

            // A few idle frames with no dash input at all.
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.AreEqual(0, spawner.GhostsSpawnedForTests, "no ghosts must spawn while the player has never dashed");
            Assert.AreEqual(0, LiveGhostCount());
        }

        [UnityTest]
        public IEnumerator DashActive_SpawnsGhostsAtConfiguredCadence_AndStopsOutsideDash()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            var spawner = Object.FindFirstObjectByType<DashTrailSpawner>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(spawner);

            Press(keyboard.dKey);
            yield return null;

            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => player.IsDashing, 2f);
            Assert.IsTrue(player.IsDashing);

            // At least one ghost must spawn immediately on dash activation.
            Assert.GreaterOrEqual(spawner.GhostsSpawnedForTests, 1, "a ghost must spawn on the frame dash activates");

            yield return WaitUntilOrTimeout(() => !player.IsDashing, 2f);
            Assert.IsFalse(player.IsDashing);
            Release(keyboard.dKey);
            Release(keyboard.spaceKey);

            int ghostsAfterDash = spawner.GhostsSpawnedForTests;

            // DASH_DURATION=0.2s / DashTrailSpawnIntervalS=0.03s ⇒ roughly 5-7 ghosts expected over one
            // dash window (acceptance's own stated density). Assert a generous lower bound only (avoids
            // coupling this test's pass/fail to exact frame-timing variance in batchmode).
            Assert.GreaterOrEqual(ghostsAfterDash, 3,
                "a full DASH_DURATION window at DashTrailSpawnIntervalS cadence must spawn multiple ghosts");

            // CR-CODE S-31 iteration 1 minor finding: the lower bound above doesn't catch a regression
            // where DashTrailSpawnIntervalS is ignored and a ghost spawns every single frame instead — add
            // a generous upper bound derived from the configured cadence (acceptance:
            // 「DashTrailSpawnIntervalS間隔で生成される」, not "every frame"). +3 slack absorbs batchmode
            // frame-timing variance (extra ghost on activation frame + the while-loop's catch-up
            // granularity) without coupling to exact frame counts.
            int maxExpectedGhosts = Mathf.CeilToInt(GameConfig.Player.DashDuration / GameConfig.Fx.DashTrailSpawnIntervalS) + 3;
            Assert.LessOrEqual(ghostsAfterDash, maxExpectedGhosts,
                "ghost cadence must respect DashTrailSpawnIntervalS, not spawn on every frame");

            // Now outside the dash window entirely — hold still for a while and confirm the spawn count
            // does not keep climbing (acceptance: 「ダッシュ発動外ではゴーストが生成されないこと」).
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            Assert.AreEqual(ghostsAfterDash, spawner.GhostsSpawnedForTests,
                "no additional ghosts must spawn once the dash window has ended");
        }

        [UnityTest]
        public IEnumerator Ghost_FadesAndSelfDestructs_AfterLifetime()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => player.IsDashing, 2f);

            // Checked on the very frame dash activation is first observed — the spawner's own
            // isDashing&&!wasDashing branch spawns synchronously on that same frame, so at least one
            // ghost must already exist with an alpha at (or a few Update() ticks below, hence the
            // generous tolerance rather than an exact match) DashTrailGhostAlpha. FindObjectsByType does
            // not guarantee spawn order, so the freshest ghost is whichever currently has the HIGHEST
            // alpha, not index [0].
            DashTrailGhost[] freshGhosts = Object.FindObjectsByType<DashTrailGhost>(FindObjectsSortMode.None);
            Assert.Greater(freshGhosts.Length, 0, "at least one ghost must exist right after dash activation");
            float freshestAlpha = freshGhosts.Max(g => g.CurrentAlpha);
            Assert.AreEqual(GameConfig.Fx.DashTrailGhostAlpha, freshestAlpha, 0.05f,
                "the freshest ghost right after dash activation must start at (or very near) DashTrailGhostAlpha");

            Release(keyboard.dKey);
            Release(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => !player.IsDashing, 2f);

            // CR-CODE S-31 iteration 2 minor finding: the final assertion below only proves ghosts
            // eventually disappear, not that they survive at least "most of" their configured lifetime — a
            // wiring regression that fed the wrong interval into DashTrailGhost.Initialize (e.g.
            // DashTrailSpawnIntervalS instead of DashTrailGhostLifetimeS) would still pass it, since the
            // per-frame alpha check right after dash activation above only samples the very first frame.
            // Sample here, immediately after the dash window ends and well before
            // DashTrailGhostLifetimeS could have elapsed for the last-spawned ghost, and require at least
            // one ghost to still be alive.
            Assert.Greater(LiveGhostCount(), 0,
                "a ghost spawned during the dash must still be alive immediately after the dash window ends " +
                "(well before DashTrailGhostLifetimeS could have elapsed) — catches a lifetime wired to the " +
                "wrong config value causing premature expiry");

            // Wait past DashTrailGhostLifetimeS (plus dash duration, since ghosts keep spawning through
            // the window) and confirm every ghost has self-destroyed (S-31 acceptance: 「各ゴーストが
            // DashTrailGhostLifetimeS経過後に消滅すること」).
            float waitSeconds = GameConfig.Player.DashDuration + GameConfig.Fx.DashTrailGhostLifetimeS + 0.3f;
            yield return WaitUntilOrTimeout(() => LiveGhostCount() == 0, waitSeconds + 2f);
            Assert.AreEqual(0, LiveGhostCount(), "every ghost must have destroyed itself after its lifetime elapsed");
        }

        [UnityTest]
        public IEnumerator DashDistance_InvulnWindow_Cooldown_NoRegressionFromS07()
        {
            // Regression guard for S-07 acceptance (Systems/DashSystem and Components/PlayerController
            // are untouched by this story — this asserts that fact's observable consequence rather than
            // re-deriving the full DashSceneTests suite).
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Assert.IsFalse(player.IsDashing);
            Assert.IsFalse(player.IsDashInvulnerable);

            Press(keyboard.dKey);
            yield return null;
            Vector3 posBeforeDash = player.transform.position;

            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => player.IsDashing, 2f);
            Assert.IsTrue(player.IsDashing);
            Assert.IsTrue(player.IsDashInvulnerable, "invuln flag must still be set the instant dash activates");

            yield return WaitUntilOrTimeout(() => !player.IsDashing, 2f);
            Vector3 posAfterDash = player.transform.position;
            Release(keyboard.dKey);
            Release(keyboard.spaceKey);

            float distance = Vector3.Distance(posBeforeDash, posAfterDash);
            float expectedDistance = GameConfig.Player.DashSpeed * GameConfig.Player.DashDuration;
            Assert.AreEqual(expectedDistance, distance, 0.6f,
                "dash displacement over DASH_DURATION must still be ≈ DASH_SPEED×DASH_DURATION (no S-07 regression)");

            yield return WaitUntilOrTimeout(() => !player.IsDashInvulnerable, 2f);
            Assert.IsFalse(player.IsDashInvulnerable);

            // Immediate re-press must still be ignored during cooldown.
            Press(keyboard.spaceKey);
            yield return null;
            yield return null;
            Release(keyboard.spaceKey);
            Assert.IsFalse(player.IsDashing, "a dash press during cooldown must still be ignored (no S-07 regression)");
        }

        [UnityTest]
        public IEnumerator DashActive_PreIntegrationPlaceholder_GhostsCarryNoGameplayComponents()
        {
            // CR-CODE S-31 iteration 1 blocker regression test: reproduces the wiring state
            // Editor/SceneWiring.WireGame produces before Editor/AssetIntegration.PatchPlayerVisual ever
            // runs (or degrades to via "[DEGRADED] PatchPlayerVisual: Hero prefab not found — leaving
            // primitive-capsule placeholder in place") — DashTrailSpawner.ResolveVisual falls back to the
            // Player root itself in that state. Before the fix, SpawnGhost's Instantiate(_visualRoot.
            // gameObject) cloned the ENTIRE Player GameObject (CapsuleCollider + PlayerController +
            // AutoAttackDriver + HealthComponent + HeroFxController + DashTrailSpawner itself), and
            // PlayerController.Awake() running synchronously inside Instantiate() hijacked the
            // PlayerController.Instance singleton the instant the clone existed.
            //
            // Loads Game.unity first (LoadSceneMode.Single resets the scene deterministically, matching
            // every other test in this file) then replaces the already-integrated Player with a synthetic
            // placeholder-capsule Player wired exactly like Editor/SceneWiring.WireGame's own
            // pre-integration branch (`GameObject.CreatePrimitive(PrimitiveType.Capsule)` +
            // PlayerController/AutoAttackDriver/HealthComponent/HeroFxController/DashTrailSpawner, no
            // HeroVisual child) so DashTrailSpawner.ResolveVisual is forced down the fallback path this
            // test targets and every gameplay component the pre-fix bug would have cloned is present to
            // assert against.
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            GameObject existingPlayer = GameObject.Find("Player");
            Assert.IsNotNull(existingPlayer, "Game scene must have a wired Player (Editor/SceneWiring.WireGame)");
            Object.DestroyImmediate(existingPlayer);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            PlayerController controller = player.AddComponent<PlayerController>();
            player.AddComponent<AutoAttackDriver>();
            player.AddComponent<HealthComponent>();
            player.AddComponent<HeroFxController>();
            DashTrailSpawner spawner = player.AddComponent<DashTrailSpawner>();

            // Let Awake/Start run: PlayerController.Instance is set and DashTrailSpawner.ResolveVisual
            // resolves the fallback (no HeroVisual child exists on this synthetic Player).
            yield return null;
            Assert.AreSame(controller, PlayerController.Instance);

            // Baseline counts before any dash/ghost activity — captured rather than hardcoded to 1,
            // because WaveSpawner (unrelated, still running in this loaded Game scene) may have already
            // spawned an EnemyAgent carrying its own CapsuleCollider by this point. The regression this
            // test guards against is any of these counts INCREASING once ghosts start spawning, not their
            // absolute value.
            int capsuleCollidersBefore = Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Length;
            int autoAttackDriversBefore = Object.FindObjectsByType<AutoAttackDriver>(FindObjectsSortMode.None).Length;
            int healthComponentsBefore = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None).Length;
            int heroFxControllersBefore = Object.FindObjectsByType<HeroFxController>(FindObjectsSortMode.None).Length;

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);
            yield return WaitUntilOrTimeout(() => controller.IsDashing, 2f);
            Assert.IsTrue(controller.IsDashing);

            yield return WaitUntilOrTimeout(() => spawner.GhostsSpawnedForTests >= 2, 2f);
            Assert.GreaterOrEqual(spawner.GhostsSpawnedForTests, 2,
                "the placeholder-visual fallback must still spawn ghosts (S-31 acceptance applies regardless of integration state)");

            // The blocker itself: cloning the Player root for a ghost must never overwrite the
            // PlayerController.Instance singleton or carry any gameplay component/Collider along with it.
            Assert.AreSame(controller, PlayerController.Instance,
                "a ghost must never overwrite the PlayerController.Instance singleton");
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned PlayerController — ghosts must be purely visual");
            Assert.AreEqual(1, Object.FindObjectsByType<DashTrailSpawner>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned DashTrailSpawner (would otherwise recursively spawn further ghosts)");
            Assert.AreEqual(capsuleCollidersBefore, Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned CapsuleCollider — pure visual object only");
            Assert.AreEqual(autoAttackDriversBefore, Object.FindObjectsByType<AutoAttackDriver>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned AutoAttackDriver");
            Assert.AreEqual(healthComponentsBefore, Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned HealthComponent");
            Assert.AreEqual(heroFxControllersBefore, Object.FindObjectsByType<HeroFxController>(FindObjectsSortMode.None).Length,
                "no ghost may carry a cloned HeroFxController");

            Release(keyboard.dKey);
            Release(keyboard.spaceKey);
        }
    }
}
