// EnemySpawnSceneTests — S-05: 敵接近AI + ウェーブスポーン + 難度カーブ.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame) and fast-forwards simulated time
// (Time.timeScale, same technique as GameSceneTests.SustainedInput_NeverLeavesTheArenaRadius) so a
// long in-run window only costs a fraction of a second of real test time. No input is required —
// spawning/approach are fully automatic (gdd: 攻撃ボタンの割当自体が存在しない、敵は常に直線接近).
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class EnemySpawnSceneTests
    {
        private string _tempSaveDir;

        // Ship-review AUTO-FIX (CRITICAL): the long fast-forward windows below can kill the player at
        // high waves, and HealthComponent.CompleteRun→FileSaveAdapter.Save used to write to the REAL
        // Application.persistentDataPath because this fixture never set
        // HealthComponent.SaveDirectoryOverrideForTests. Mirrors HealthSceneTests' seam exactly
        // (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない).
        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s05-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            HealthComponent.SaveDirectoryOverrideForTests = _tempSaveDir;
            HealthComponent.SaveInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            HealthComponent.ContactDamageDisabledForTests = false;
            EnemyAgent.ActiveEnemies.Clear();
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            HealthComponent.SaveDirectoryOverrideForTests = null;
            HealthComponent.SaveInvocationCountForTests = 0;
            if (_tempSaveDir != null && Directory.Exists(_tempSaveDir))
            {
                Directory.Delete(_tempSaveDir, true);
            }
        }

        private IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverTime_EnemiesSpawnOnTheSpawnRing()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner (Editor/SceneWiring.WireGame)");
            Assert.AreEqual(0, EnemyAgent.ActiveEnemies.Count, "no enemies should exist before any time has elapsed");

            // Wave 1's SPAWN_INTERVAL_BASE is 1.5s; fast-forward well past that.
            Time.timeScale = 50f;
            yield return new WaitForSecondsRealtime(0.3f); // ~15 simulated seconds
            Time.timeScale = 1f;
            yield return null;

            Assert.Greater(EnemyAgent.ActiveEnemies.Count, 0, "at least one enemy must have spawned by ~15s of run time");
            foreach (EnemyAgent enemy in EnemyAgent.ActiveEnemies)
            {
                Vector3 pos = enemy.transform.position;
                Assert.IsFalse(float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z));
            }
        }

        [UnityTest]
        public IEnumerator JustAfterSpawning_EnemyIsPlacedOnTheSpawnRing()
        {
            yield return LoadGameScene();

            // Poll frame-by-frame instead of a fixed WaitForSecondsRealtime window: a newly
            // Instantiate()'d EnemyAgent only starts running its own Update() (and therefore
            // EnemyApproachSystem movement) from the *next* frame, so catching the count transition
            // on the very same frame it happens guarantees zero approach movement has been applied
            // yet — this avoids any Time.timeScale-precision race against the approach system.
            Time.timeScale = 20f; // Wave1 SPAWN_INTERVAL_BASE=1.5s sim -> ~0.075s real at this scale
            EnemyAgent spawned = null;
            float deadline = Time.realtimeSinceStartup + 3f; // generous real-time safety cap
            while (spawned == null && Time.realtimeSinceStartup < deadline)
            {
                if (EnemyAgent.ActiveEnemies.Count > 0)
                {
                    spawned = EnemyAgent.ActiveEnemies[0];
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.IsNotNull(spawned, "an enemy must spawn within the real-time safety window");
            float flatDistance = new Vector2(spawned.transform.position.x, spawned.transform.position.z).magnitude;
            Assert.AreEqual(GameConfig.Enemy.SpawnRadius, flatDistance, 1e-2f,
                "enemy must be Instantiated exactly on ENEMY_SPAWN_RADIUS (caught before its first Update)");
        }

        [UnityTest]
        public IEnumerator SpawnedEnemy_ClosesDistanceToThePlayerOverTime()
        {
            yield return LoadGameScene();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");

            // Moderate timeScale (not the aggressive 50x used for the long-window cap test below) so
            // the two checkpoints stay well inside ENEMY_SPAWN_RADIUS/ENEMY_MOVE_SPEED_BASE's ~5.4s
            // time-to-reach-player — this keeps the "distance decreased" comparison meaningful even
            // under WaitForSecondsRealtime's real-time scheduling jitter.
            Time.timeScale = 10f;
            yield return new WaitForSecondsRealtime(0.25f); // >= 2.5 simulated seconds (past the ~1.5s first spawn tick)
            Assert.Greater(EnemyAgent.ActiveEnemies.Count, 0, "an enemy must have spawned to measure approach");

            EnemyAgent enemy = EnemyAgent.ActiveEnemies[0];
            float startDistance = Vector3.Distance(enemy.transform.position, player.transform.position);

            yield return new WaitForSecondsRealtime(0.15f); // +1.5 simulated seconds of further approach
            Time.timeScale = 1f;
            yield return null;

            float endDistance = Vector3.Distance(enemy.transform.position, player.transform.position);
            Assert.Less(endDistance, startDistance, "EnemyApproachSystem must close the distance to the player (直線接近)");
            Assert.IsFalse(float.IsNaN(endDistance));
        }

        [UnityTest]
        public IEnumerator OverALongWindow_ConcurrentEnemyCountNeverExceedsTheCap()
        {
            yield return LoadGameScene();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner);

            // An idle player legitimately dies well inside this ~300-simulated-second window (waves
            // scale up while nobody dodges), which would trip the scene-survival guard below as a
            // false failure. Disable ONLY damage application (contact/spawn pipeline still runs —
            // see the seam's doc) so the guard keeps catching UNEXPECTED scene unloads while the cap
            // is exercised for the whole window. TearDown resets the seam.
            HealthComponent.ContactDamageDisabledForTests = true;

            // Fast-forward through many waves (spawn interval keeps shrinking toward
            // SPAWN_INTERVAL_MIN and spawn count keeps growing — this is where the
            // MAX_CONCURRENT_ENEMIES cap actually gets exercised). 100 is the editor's hard ceiling
            // for Time.timeScale (Unity clamps/errors above it), so use several windows instead of
            // one huge timeScale multiplier to still cover many simulated waves.
            Time.timeScale = 100f;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSecondsRealtime(0.3f); // ~30 simulated seconds per iteration
                // Ship-review AUTO-FIX: if the player dies mid-window, HealthComponent unloads Game and
                // loads Result — WaveSpawner/EnemyAgent are gone and every remaining cap assertion
                // passes vacuously against an empty list. Fail loudly on the death instead so the test
                // only ever passes when the cap was genuinely exercised for the whole window.
                Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name,
                    "the Game scene must survive the whole fast-forward window — a player death here would make the cap assertion below vacuous");
                Assert.LessOrEqual(EnemyAgent.ActiveEnemies.Count, GameConfig.Wave.MaxConcurrentEnemies,
                    "WaveSpawner must never exceed MAX_CONCURRENT_ENEMIES while spawning");
            }
            Time.timeScale = 1f;
            yield return null;

            Assert.LessOrEqual(EnemyAgent.ActiveEnemies.Count, GameConfig.Wave.MaxConcurrentEnemies);
        }
    }
}
