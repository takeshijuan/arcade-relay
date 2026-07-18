// CrystalSceneTests — S-09: クリスタル ドロップ・自動回収 + スコア算出.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → RunStatsTracker root) and exercises
// Components/CrystalPickup both directly (via CrystalPickup.SpawnDrop, mirroring AutoAttackSceneTests'
// CreateEnemy technique of bypassing the "natural" trigger for deterministic placement) and through the
// full kill→drop→auto-collect flow driven by Components/AutoAttackDriver (mirrors AutoAttackSceneTests'
// enemy-placement + frame-polling technique). Frame-polls (rather than a single fixed wait) to catch
// each state transition on the exact frame it happens, matching the codebase's existing PlayMode timing
// convention (see AutoAttackSceneTests/HealthSceneTests/EnemySpawnSceneTests headers).
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class CrystalSceneTests
    {
        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            EnemyAgent.ActiveEnemies.Clear();
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

        private static EnemyAgent CreateEnemy(Vector3 position)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            EnemyAgent agent = go.AddComponent<EnemyAgent>();
            agent.Initialize(GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);
            return agent;
        }

        [UnityTest]
        public IEnumerator CrystalWithinPickupRadius_AutoCollects_AndIncrementsRunStats()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats, "Game scene must be wired with a RunStatsTracker (Editor/SceneWiring.WireGame)");
            Assert.AreEqual(0, stats.CrystalsCollected);
            Assert.AreEqual(0, stats.CurrentScore);

            Vector3 nearPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 0.5f, 0f, 0f);
            CrystalPickup.SpawnDrop(nearPosition, 1);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (stats.CrystalsCollected == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(1, stats.CrystalsCollected,
                "a crystal spawned within CRYSTAL_PICKUP_RADIUS must be auto-collected with no pickup input");
            Assert.AreEqual(GameConfig.Score.PerCrystal, stats.CurrentScore,
                "collecting one crystal must increase score by exactly SCORE_PER_CRYSTAL");

            // CR-CODE s-19 major finding (escalated across iterations, resolved here): SFX-04
            // (クリスタル自動回収) trigger->playback was tested via the shared SfxLibrary AudioSource's isPlaying flag —
            // an illusory regression guard for the same reason as HealthSceneTests'/DashSceneTests' identical
            // fix (any other SFX sharing the source would also satisfy it). Assert the per-clip trigger
            // counter instead — it only increments when the CrystalPickup clip specifically is played.
            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.CrystalPickup, "SFX-04 must be assigned by Editor/AssetIntegration");
            Assert.AreEqual(1, SfxLibrary.Instance.CrystalPickupTriggerCountForTests,
                "SFX-04 (CrystalPickup clip specifically) must have been played exactly once by the frame the pickup is auto-collected");

            CrystalPickup[] remaining = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(0, remaining.Length, "the collected crystal must no longer exist in the scene");
        }

        [UnityTest]
        public IEnumerator UncollectedCrystal_DespawnsAfterLifetimeExpires_AndIsNeverCounted()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats);

            Vector3 farPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 5f, 0f, 0f);
            CrystalPickup.SpawnDrop(farPosition, 1);
            CrystalPickup[] spawned = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(1, spawned.Length, "SpawnDrop(pos, 1) must create exactly one crystal");
            CrystalPickup crystal = spawned[0];

            Time.timeScale = 50f; // CRYSTAL_LIFETIME=8s simulated -> ~0.16s real at this scale
            float deadline = Time.realtimeSinceStartup + 5f;
            while (crystal != null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Time.timeScale = 1f;
            yield return null;

            Assert.IsTrue(crystal == null, "an uncollected crystal must despawn once CRYSTAL_LIFETIME elapses");
            Assert.AreEqual(0, stats.CrystalsCollected,
                "a crystal that expired outside CRYSTAL_PICKUP_RADIUS must never be counted as collected");
            Assert.AreEqual(0, stats.CurrentScore, "an expired crystal must not add SCORE_PER_CRYSTAL");
        }

        [UnityTest]
        public IEnumerator KillingEnemyWithinPickupRadius_DropsCrystal_AutoCollects_AndScoreIncreasesByScorePerCrystal()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats, "Game scene must be wired with a RunStatsTracker (Editor/SceneWiring.WireGame)");

            // Close enough to be within both AUTO_ATTACK_RANGE and CRYSTAL_PICKUP_RADIUS at kill time
            // (gdd: 撃破位置にドロップ→回収半径内なら即自動回収).
            Vector3 spawnPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 0.5f, 0f, 0f);
            EnemyAgent enemy = CreateEnemy(spawnPosition);

            Time.timeScale = 5f;
            int scoreAtKill = -1;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (scoreAtKill < 0 && stats.NormalKillCount > 0)
                {
                    // Captured on the exact frame the kill registers, one frame before the newly
                    // spawned crystal's own Start()/Update() can run (mirrors EnemySpawnSceneTests'
                    // JustAfterSpawning_EnemyIsPlacedOnTheSpawnRing "catch the transition frame"
                    // technique) — isolates the kill-only score from the pickup-only score below.
                    scoreAtKill = stats.CurrentScore;
                }
                if (stats.CrystalsCollected > 0)
                {
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;

            // S-32 (撃破インパクト演出): a killing hit no longer destroys the enemy's GameObject
            // synchronously — it defers the actual Destroy() by GameConfig.Fx.EnemyKillPopDurationS while
            // the kill-pop visual plays (Components/EnemyAgent.BeginKillPop/TickKillPop). The kill/score/
            // crystal-drop bookkeeping this test cares about (asserted above/below) is unaffected —
            // acceptance: 「この演出による遅延の影響を受けない（見た目の消滅タイミングのみポップ演出分だけ
            // 遅らせる）」— so only the destruction-timing assertion itself needs to tolerate the pop delay.
            float destroyDeadline = Time.realtimeSinceStartup + GameConfig.Fx.EnemyKillPopDurationS + 2f;
            while (enemy != null && Time.realtimeSinceStartup < destroyDeadline)
            {
                yield return null;
            }
            Assert.IsTrue(enemy == null,
                "enemy must have been destroyed once the S-32 kill-pop's EnemyKillPopDurationS has elapsed");
            Assert.AreEqual(1, stats.NormalKillCount, "the enemy must have been recorded as exactly one normal kill");
            Assert.AreEqual(1, stats.CrystalsCollected, "the dropped crystal must have been auto-collected");
            Assert.AreNotEqual(-1, scoreAtKill, "kill must have registered before the deadline");
            Assert.AreEqual(scoreAtKill + GameConfig.Score.PerCrystal, stats.CurrentScore,
                "collecting the dropped crystal must increase the score by exactly SCORE_PER_CRYSTAL on top of the kill score");

            CrystalPickup[] remaining = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(0, remaining.Length, "the auto-collected crystal must no longer exist in the scene");
        }
    }
}
