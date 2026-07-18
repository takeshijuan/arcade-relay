// HeavyEnemySceneTests — S-14: ヘヴィスウォーマー変種（任意・見た目/ステータス差分のみ）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame). Most sub-tests manually place a
// heavy-tagged EnemyAgent (bypassing WaveSpawner, mirroring AutoAttackSceneTests/HealthSceneTests'
// technique) to keep HP/contact-damage/kill-score/drop-count assertions deterministic. The final test
// exercises the actual WaveSpawner mixing decision (Systems/HeavyEnemySystem.ShouldSpawnHeavy)
// end-to-end. CR-CODE S-14 iter2 major finding: a fixed RNG seed alone does NOT make that test
// flake-free (the roll *sequence* is fixed but how many rolls land before assertions run isn't) — see
// that test's own comments for how it bounds itself by a guaranteed roll count instead.
using System.Collections;
using System.Collections.Generic;
using ForgeGame.Components;
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class HeavyEnemySceneTests
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
            // CR-CODE S-14 iter2 minor finding: this used to no-op silently on a missing WaveSpawner,
            // the same silent-degradation pattern DisableAutoAttackAndPlayerHealth below was already
            // fixed for in iter1 — a Game.unity wiring regression that drops the WaveSpawner would have
            // let this helper's 3 manual-placement callers pass with zero trace (their assertions only
            // check the manually-placed enemy, never the spawner itself).
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");
            spawner.enabled = false;
        }

        /// <summary>Used only by the spawn-mixture test below: disables the systems that would otherwise
        /// remove enemies from EnemyAgent.ActiveEnemies (auto-attack kills) or end the run (player death
        /// from accumulated contact damage) during a long fast-forward window. Without this, the sampled
        /// population at the end of the window reflects differential survival (Heavy's higher HP means it
        /// survives auto-attack longer) rather than the raw WaveSpawner spawn-time mix this test wants to
        /// observe — mirrors DisableWaveSpawner's "isolate one system" testing technique used elsewhere in
        /// this file/AutoAttackSceneTests/HealthSceneTests.</summary>
        private static void DisableAutoAttackAndPlayerHealth()
        {
            // CR-CODE S-14 iter1 minor finding: this helper's whole purpose is neutralizing the
            // survival-bias sources listed in its own doc comment above (auto-attack kills, player-death
            // ending the run) — a future scene-wiring change silently removing AutoAttackDriver/
            // HealthComponent from Game.unity would un-neutralize that bias with zero trace (the mixing
            // test below would just start flaking without an obvious cause), so assert presence the same
            // way DisableWaveSpawner now also asserts the spawner exists (see that helper above; CR-CODE
            // S-14 iter2 minor finding — no line-number cross-reference here since those rot on the next edit).
            var driver = Object.FindFirstObjectByType<AutoAttackDriver>();
            Assert.IsNotNull(driver, "Game scene must be wired with an AutoAttackDriver");
            driver.enabled = false;
            var health = Object.FindFirstObjectByType<HealthComponent>();
            Assert.IsNotNull(health, "Game scene must be wired with a HealthComponent");
            health.enabled = false;
        }

        private static EnemyAgent CreateEnemy(Vector3 position, EnemyKind kind, float moveSpeed, int maxHp)
        {
            // CR-CODE S-14 iter1 major finding: EnemyAgent.ApplyHeavyTint's "no Renderer" branch is now
            // Debug.LogError (wiring guard, was LogWarning) — a bare `new GameObject` here would make
            // every HeavySwarmer test enemy legitimately trip that guard and fail
            // LogAssert.NoUnexpectedReceived(). Use CreatePrimitive(Cube) instead so the test enemy
            // carries a Renderer the same way every real spawn path (WaveSpawner's placeholder Cube /
            // the MDL-02 prefab) does; the extra MeshFilter/BoxCollider it brings are inert here (contact
            // detection is HealthSystem's XZ radius overlap, not Unity physics — see HealthComponent).
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TestEnemy_" + kind;
            go.transform.position = position;
            EnemyAgent agent = go.AddComponent<EnemyAgent>();
            agent.Initialize(moveSpeed, maxHp, kind);
            return agent;
        }

        [UnityTest]
        public IEnumerator HeavyVariant_Initialize_AppliesHpAndSpeedMultipliers()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            float baseSpeed = GameConfig.Enemy.MoveSpeedBase;
            int baseHp = GameConfig.Enemy.HpBase;

            EnemyAgent heavy = CreateEnemy(
                Vector3.zero, EnemyKind.HeavySwarmer,
                HeavyEnemySystem.AdjustedSpeed(baseSpeed), HeavyEnemySystem.AdjustedHp(baseHp));

            Assert.AreEqual(EnemyKind.HeavySwarmer, heavy.Kind);
            int expectedHp = Mathf.RoundToInt(baseHp * GameConfig.Enemy.HeavyHpMult);
            Assert.AreEqual(expectedHp, heavy.MaxHp, "gdd: 最大HPにHEAVY_ENEMY_HP_MULTを適用");
            Assert.AreEqual(expectedHp, heavy.CurrentHp);
            Assert.AreEqual(baseSpeed * GameConfig.Enemy.HeavySpeedMult, heavy.MoveSpeed, 1e-3f,
                "gdd: HEAVY_ENEMY_SPEED_MULTで減速");
            Assert.Greater(heavy.MaxHp, baseHp);
            Assert.Less(heavy.MoveSpeed, baseSpeed);

            Object.Destroy(heavy.gameObject);
        }

        [UnityTest]
        public IEnumerator HeavyVariant_Contact_AppliesHeavyContactDamageMultiplier()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health, "Game scene's Player must carry HealthComponent");
            Assert.AreEqual(GameConfig.Player.MaxHpBase, health.CurrentHp);

            CreateEnemy(player.transform.position, EnemyKind.HeavySwarmer,
                GameConfig.Enemy.MoveSpeedBase, GameConfig.Enemy.HpBase);

            int hpAfterFirstTick = -1;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (health.CurrentHp < GameConfig.Player.MaxHpBase)
                {
                    hpAfterFirstTick = health.CurrentHp;
                    break;
                }
                yield return null;
            }

            int expectedDamage = HeavyEnemySystem.AdjustedContactDamage(GameConfig.Enemy.ContactDamage);
            Assert.AreEqual(GameConfig.Player.MaxHpBase - expectedDamage, hpAfterFirstTick,
                "heavy variant contact damage must apply HEAVY_ENEMY_CONTACT_DAMAGE_MULT, not the base ENEMY_CONTACT_DAMAGE");
        }

        [UnityTest]
        public IEnumerator HeavyVariant_Kill_AwardsHeavyScoreAndDropsHeavyCrystalCount()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(RunStatsTracker.Instance, "Game scene must be wired with a RunStatsTracker");

            // maxHp = AUTO_ATTACK_DAMAGE_BASE so a single auto-attack tick kills it — this test isolates
            // score/drop bookkeeping from the HP multiplier itself (covered by
            // HeavyVariant_Initialize_AppliesHpAndSpeedMultipliers above).
            EnemyAgent heavy = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f),
                EnemyKind.HeavySwarmer, GameConfig.Enemy.MoveSpeedBase, GameConfig.Player.AutoAttackDamageBase);

            int crystalsBefore = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None).Length;

            bool died = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (heavy == null)
                {
                    died = true;
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(died, "a 1-hit-kill heavy enemy must be defeated by the first auto-attack tick");
            Assert.AreEqual(1, RunStatsTracker.Instance.HeavyKillCount, "kill must be tallied as Heavy");
            Assert.AreEqual(0, RunStatsTracker.Instance.NormalKillCount, "kill must not also be tallied as Normal");
            Assert.GreaterOrEqual(RunStatsTracker.Instance.CurrentScore, GameConfig.Score.PerKillHeavy,
                "gdd: ヘヴィ変種はSCORE_PER_KILL_HEAVYを加点");

            yield return null; // let the just-Instantiate()'d crystal drops register with FindObjectsByType
            int crystalsAfter = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(crystalsBefore + GameConfig.Crystal.DropPerKillHeavy, crystalsAfter,
                "gdd: ヘヴィ変種はCRYSTAL_DROP_PER_KILL_HEAVY個ドロップ（通常のCRYSTAL_DROP_PER_KILL_NORMALではない）");
        }

        [UnityTest]
        public IEnumerator AfterUnlockWave_HeavyVariantsMixIntoAutomaticSpawns_WithPlausibleStatMultipliers()
        {
            // CR-CODE S-14 iter2 major finding: iter1's fix here only corrected this comment's claimed
            // flake probability (0.85^40 ≈ 0.2%, ~1-in-660 runs) without eliminating the flake itself —
            // Random.InitState fixes the roll *sequence* but not how many rolls get consumed before the
            // single fixed-real-time sampling window below closed. This iteration replaces the
            // time-windowed sample with a roll-count-driven loop (via WaveSpawner.SpawnCallCountForTests)
            // that keeps freeing MAX_CONCURRENT_ENEMIES room every frame until a heavy is actually
            // observed, so the assertion is bounded by "how many rolls have we seen" rather than "how
            // much wall-clock time passed" — see the loop below for the residual-probability math.
            Random.InitState(20260713);

            yield return LoadGameScene();
            DisableAutoAttackAndPlayerHealth();

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");

            // Phase 1: fast-forward only until HEAVY_ENEMY_UNLOCK_WAVE (wave 3 begins at 60s of run
            // time), then discard whatever spawned before that point. MAX_CONCURRENT_ENEMIES=40 would
            // otherwise likely already be saturated with pre-unlock (always-Normal) spawns by the time
            // wave 3 starts (~40 spawns accumulate by t=60s at Wave1/2's spawn interval/count) — with
            // AutoAttack/Health disabled above (nothing removes enemies), WaveSpawner would then have no
            // free room left to ever spawn a post-unlock enemy, Heavy or not.
            Time.timeScale = 100f;
            float unlockDeadline = Time.realtimeSinceStartup + 10f;
            while (spawner.CurrentWave < GameConfig.Enemy.HeavyUnlockWave && Time.realtimeSinceStartup < unlockDeadline)
            {
                yield return null;
            }
            Assert.GreaterOrEqual(spawner.CurrentWave, GameConfig.Enemy.HeavyUnlockWave,
                "test window must reach HEAVY_ENEMY_UNLOCK_WAVE for the acceptance's precondition to hold");

            foreach (EnemyAgent preUnlockEnemy in new List<EnemyAgent>(EnemyAgent.ActiveEnemies))
            {
                Object.Destroy(preUnlockEnemy.gameObject);
            }
            yield return null; // let Destroy()/OnDisable() clear them out of ActiveEnemies before sampling

            // Phase 2: instead of fast-forwarding a fixed real-time window and sampling whatever landed
            // in it (iter1's approach — bounded to ~MAX_CONCURRENT_ENEMIES=40 rolls because a saturated
            // ActiveEnemies list stalls WaveSpawner's room check), keep destroying every currently-Normal
            // enemy each frame so room never runs out and WaveSpawner keeps rolling every tick. This is
            // bounded by SpawnCallCountForTests (a real roll count), not by simulated/real seconds
            // elapsed, and stops the instant a Heavy is observed rather than continuing to burn time.
            // requiredRollsBeforeGivingUp=2000 makes the residual "never saw a heavy" flake probability
            // 0.85^2000, which is not just smaller than iter1's ~0.2% but zero in any practically
            // measurable sense — realtimeGiveUpDeadline is a generous safety net that only fires if
            // spawning itself is broken (a real regression, not a statistical flake).
            const int requiredRollsBeforeGivingUp = 2000;
            int rollsAtPhase2Start = spawner.SpawnCallCountForTests;
            bool sawNormal = false;
            EnemyAgent heavyFound = null;
            float realtimeGiveUpDeadline = Time.realtimeSinceStartup + 15f;
            while (heavyFound == null
                && spawner.SpawnCallCountForTests - rollsAtPhase2Start < requiredRollsBeforeGivingUp
                && Time.realtimeSinceStartup < realtimeGiveUpDeadline)
            {
                yield return null;
                foreach (EnemyAgent enemy in EnemyAgent.ActiveEnemies)
                {
                    if (enemy.Kind == EnemyKind.HeavySwarmer)
                    {
                        heavyFound = enemy;
                        break;
                    }
                    sawNormal = true;
                }
                if (heavyFound == null)
                {
                    foreach (EnemyAgent normalEnemy in new List<EnemyAgent>(EnemyAgent.ActiveEnemies))
                    {
                        Object.Destroy(normalEnemy.gameObject);
                    }
                }
            }
            Time.timeScale = 1f;

            int rollsConsumed = spawner.SpawnCallCountForTests - rollsAtPhase2Start;
            Assert.IsTrue(sawNormal, "normal Swarmer must remain present (HEAVY_ENEMY_SPAWN_CHANCE=15% keeps it the majority)");
            Assert.IsNotNull(heavyFound,
                "heavy variant must mix into automatic WaveSpawner spawns after HEAVY_ENEMY_UNLOCK_WAVE " +
                $"(observed {rollsConsumed} spawn rolls before giving up — at HEAVY_ENEMY_SPAWN_CHANCE=15% " +
                "this is not the fixed-seed statistical flake iter1 left in place, treat as a real regression)");
            Assert.Greater(heavyFound.MaxHp, GameConfig.Enemy.HpBase,
                "heavy variant MaxHp must exceed the un-multiplied ENEMY_HP_BASE (HEAVY_ENEMY_HP_MULT>1)");
            Assert.Less(heavyFound.MoveSpeed, GameConfig.Enemy.MoveSpeedBase,
                "heavy variant MoveSpeed must stay below the un-multiplied ENEMY_MOVE_SPEED_BASE (HEAVY_ENEMY_SPEED_MULT<1)");
        }
    }
}
