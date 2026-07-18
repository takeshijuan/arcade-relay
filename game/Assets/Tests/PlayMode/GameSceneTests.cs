// GameSceneTests — S-04: プレイヤー移動 + 固定俯瞰カメラ + アリーナ境界.
// Loads the actual wired Game.unity scene (Editor/SceneWiring.WireGame) and drives it through
// InputTestFixture (rule 8: batchmode Game View has no focus, so InputAction-level assertions
// require the fixture's keyboard simulation rather than raw InputSystem.QueueStateEvent).
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class GameSceneTests : InputTestFixture
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

        // Mirrors AutoAttackSceneTests/HealthSceneTests' DisableWaveSpawner — WaveSpawner still spawns
        // enemies during this test's real-time acceleration window otherwise (rule: Components はライフ
        // サイクルと配線のみ; keeping this a pure boundary-clamp test).
        private static void DisableWaveSpawner()
        {
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator RightInput_MovesPlayerToThePositiveXSide()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController (Editor/SceneWiring.WireGame)");
            Vector3 start = player.transform.position;

            Press(keyboard.dKey);
            yield return new WaitForSecondsRealtime(0.2f);
            Release(keyboard.dKey);
            yield return null;

            Vector3 end = player.transform.position;
            Assert.Greater(end.x, start.x, "right (D) input must move the player toward +X");
            Assert.AreEqual(start.z, end.z, 1e-3f, "pure right input must not introduce Z drift");
            Assert.IsFalse(float.IsNaN(end.x) || float.IsNaN(end.y) || float.IsNaN(end.z));
        }

        [UnityTest]
        public IEnumerator SustainedInput_NeverLeavesTheArenaRadius()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();
            // This test only exercises the boundary clamp (Systems/PlayerMovement.ClampToArena) over a
            // long simulated hold — WaveSpawner running unattended for ~25 simulated seconds of
            // undodged straight-line movement can (non-deterministically, depending on exact frame
            // timing) let enemies catch up and kill the player, destroying the scene mid-assertion
            // (observed as a MissingReferenceException on player.transform). Disable it, matching
            // AutoAttackSceneTests/HealthSceneTests' established pattern for isolating a single system.
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            // Speed up simulated time so a long hold (well beyond the ~2s it takes to cross the
            // 12m arena radius at 6 m/s) only costs a fraction of a second of real test time.
            Time.timeScale = 50f;
            Press(keyboard.dKey);
            yield return new WaitForSecondsRealtime(0.5f); // ≈25 simulated seconds of continuous input
            Release(keyboard.dKey);
            Time.timeScale = 1f;
            yield return null;

            Vector3 end = player.transform.position;
            float flatDistance = new Vector2(end.x, end.z).magnitude;
            Assert.LessOrEqual(flatDistance, GameConfig.Player.ArenaRadius + 1e-2f,
                "prolonged input must not push the player outside ARENA_RADIUS (boundary clamp)");
            Assert.IsFalse(float.IsNaN(end.x) || float.IsNaN(end.y) || float.IsNaN(end.z));
        }

        [UnityTest]
        public IEnumerator ArenaCamera_ForwardPointsTowardArenaCenter()
        {
            yield return LoadGameScene();

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Game scene must have a Main Camera wired with ArenaCameraRig");
            Assert.IsNotNull(cam.GetComponent<ArenaCameraRig>(), "Main Camera must carry ArenaCameraRig (Editor/SceneWiring.WireGame)");

            Vector3 toCenter = (Vector3.zero - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, toCenter);

            // S-04 acceptance: カメラ forward が中心方向を向いている（Vector3.Dot > 0.2）
            Assert.Greater(dot, 0.2f);
            Assert.AreEqual(GameConfig.Camera.Fov, cam.fieldOfView, 1e-3f);
        }

        [UnityTest]
        public IEnumerator ArenaCamera_DoesNotFollowThePlayer()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();

            Camera cam = Camera.main;
            Assert.IsNotNull(cam);
            Vector3 camStart = cam.transform.position;
            Quaternion rotStart = cam.transform.rotation;

            Press(keyboard.dKey);
            yield return new WaitForSecondsRealtime(0.2f);
            Release(keyboard.dKey);
            yield return null;

            Assert.AreEqual(camStart, cam.transform.position, "fixed overhead camera must not follow the player (gdd 固定俯瞰カメラ)");
            Assert.AreEqual(rotStart, cam.transform.rotation);
        }
    }
}
