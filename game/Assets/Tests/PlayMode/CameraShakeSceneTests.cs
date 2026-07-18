// CameraShakeSceneTests — S-23: ダッシュ紙一重回避のカメラシェイク演出 (gdd P-01「紙一重回避」).
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Main Camera carries ArenaCameraRig,
// Player carries PlayerController+HealthComponent) and drives dash activation through InputTestFixture
// (rule 8: batchmode Game View has no focus). Mirrors HealthSceneTests'/DashSceneTests' CreateTouchingEnemy
// + DisableWaveSpawner + frame-poll techniques to keep contact/invuln timing deterministic.
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class CameraShakeSceneTests : InputTestFixture
    {
        [SetUp]
        public void SetUpCounters()
        {
            ArenaCameraRig.TriggerInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            EnemyAgent.ActiveEnemies.Clear();
            ArenaCameraRig.TriggerInvocationCountForTests = 0;
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

        private static void DisableWaveSpawner()
        {
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }
        }

        private static EnemyAgent CreateTouchingEnemy(Vector3 position)
        {
            var go = new GameObject("TestEnemyContact");
            go.transform.position = position;
            EnemyAgent agent = go.AddComponent<EnemyAgent>();
            agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            return agent;
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
        public IEnumerator ContactDuringDashInvulnerability_TriggersSingleShake_ThenRevertsExactlyAfterDuration()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig, "Game scene must be wired with ArenaCameraRig on Main Camera (Editor/SceneWiring.WireGame)");

            yield return null; // let Start()'s fixed-pose assignment settle before sampling the base position
            Vector3 basePos = rig.transform.localPosition;
            Quaternion baseRot = rig.transform.rotation;

            // Touching enemy present before the dash starts, so contact is already true on the very
            // first invulnerable frame.
            CreateTouchingEnemy(player.transform.position);

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);

            yield return WaitUntilOrTimeout(() => player.IsDashInvulnerable, 2f);
            Assert.IsTrue(player.IsDashInvulnerable, "dash must have activated its invuln window");

            bool sawShake = false;
            float pollDeadline = Time.realtimeSinceStartup + GameConfig.Fx.DashNearMissShakeDuration + 1f;
            while (Time.realtimeSinceStartup < pollDeadline)
            {
                if (Vector3.Distance(rig.transform.localPosition, basePos) > 0.01f)
                {
                    sawShake = true;
                    break;
                }
                yield return null;
            }
            Assert.IsTrue(sawShake, "camera localPosition must change from the base fixed position while the near-miss shake is active");
            // acceptance: 「ArenaCameraRig の固定注視方向は変えず」— the shake is a position-only offset;
            // rotation must be untouched even while the shake is actively displacing localPosition
            // (CR-CODE s-23 finding: this was previously unassert­ed, so a future rotational-shake
            // regression would pass all 3 tests unnoticed).
            Assert.AreEqual(baseRot, rig.transform.rotation,
                "camera rotation must remain unchanged while the near-miss shake is displacing localPosition (fixed gaze direction)");

            yield return WaitUntilOrTimeout(
                () => Vector3.Distance(rig.transform.localPosition, basePos) < 1e-4f,
                GameConfig.Fx.DashNearMissShakeDuration + 1f);
            Assert.Less(Vector3.Distance(rig.transform.localPosition, basePos), 1e-4f,
                "camera must strictly revert to the fixed base position once DASH_NEARMISS_SHAKE_DURATION has elapsed");
            Assert.AreEqual(baseRot, rig.transform.rotation,
                "camera rotation must still be unchanged after reverting to the fixed base position");

            Assert.AreEqual(1, ArenaCameraRig.TriggerInvocationCountForTests, "exactly one shake must fire for this dash");

            Release(keyboard.spaceKey);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator NormalDamage_WithoutInvulnerability_NeverTriggersShake()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig);

            yield return null;
            Vector3 basePos = rig.transform.localPosition;

            CreateTouchingEnemy(player.transform.position);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (health.CurrentHp >= GameConfig.Player.MaxHpBase && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.Less(health.CurrentHp, GameConfig.Player.MaxHpBase,
                "contact damage must have actually landed (not invulnerable) for this to be a meaningful negative check");

            Assert.AreEqual(0, ArenaCameraRig.TriggerInvocationCountForTests,
                "normal (non-invulnerable) contact damage must never trigger the near-miss shake");
            Assert.AreEqual(basePos, rig.transform.localPosition,
                "camera must never leave its fixed base position when no shake has fired");
        }

        [UnityTest]
        public IEnumerator MultipleSimultaneousContactsWithinSameDashWindow_TriggersOnlyOneShake()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig);

            yield return null;
            Vector3 basePos = rig.transform.localPosition;

            // Multiple simultaneously-touching enemies so the per-frame contact loop evaluates several
            // "isContacting && invulnerable" hits on the very same frame, and again on every subsequent
            // frame for the whole invuln window (gdd/acceptance: 「同一ダッシュの無敵窓内で複数回の接触判定
            // が起きてもシェイク時間・振幅が多重加算されず単発と同じ挙動になる」).
            CreateTouchingEnemy(player.transform.position);
            CreateTouchingEnemy(player.transform.position);
            CreateTouchingEnemy(player.transform.position);

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);

            yield return WaitUntilOrTimeout(() => player.IsDashInvulnerable, 2f);
            Assert.IsTrue(player.IsDashInvulnerable, "dash must have activated its invuln window");

            // DASH_INVULN_DURATION (0.25s) > DASH_NEARMISS_SHAKE_DURATION (0.15s): stay in contact for
            // the whole window, so a single shake will fully decay back to the base position WHILE
            // contact/invulnerability is still active — the buggy behavior this test guards against is
            // a second shake re-arming once the first one decays out but the window hasn't closed yet.
            yield return WaitUntilOrTimeout(() => !player.IsDashInvulnerable, 2f);
            Assert.IsFalse(player.IsDashInvulnerable, "invuln window must have closed for this window-scoped assertion to be meaningful");

            Assert.AreEqual(1, ArenaCameraRig.TriggerInvocationCountForTests,
                "3 simultaneously-touching enemies across the whole invuln window must still fire only ONE shake per dash");
            Assert.AreEqual(basePos, rig.transform.localPosition,
                "the single shake must have already fully decayed back to base by the time the invuln window closes");

            Release(keyboard.spaceKey);
            Release(keyboard.dKey);
        }
    }
}
