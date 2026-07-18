// AutoAttackSceneTests — S-06: 自動攻撃（最寄り索敵・瞬間ヒット）+ 敵HP・撃破.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries AutoAttackDriver)
// and manually places enemies (bypassing WaveSpawner, which is disabled per-test to keep the
// AUTO_ATTACK_RANGE/AUTO_ATTACK_INTERVAL timing assertions deterministic — WaveSpawner still spawns
// enemies out on ENEMY_SPAWN_RADIUS which wouldn't interfere anyway, but disabling it removes any
// timing race entirely). Frame-polls (rather than a single fixed wait) to catch each HP-drop/death
// transition on the exact frame it happens, mirroring EnemySpawnSceneTests'
// JustAfterSpawning_EnemyIsPlacedOnTheSpawnRing technique — this avoids flakiness from real-time
// wait-duration variance under Time.timeScale speed-up.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class AutoAttackSceneTests : InputTestFixture
    {
        private string _tempSaveDir;

        // Ship-review AUTO-FIX (CRITICAL): DyingHero_… below kills the player, whose
        // HealthComponent.CompleteRun→FileSaveAdapter.Save used to write to the REAL
        // Application.persistentDataPath because this fixture never set
        // HealthComponent.SaveDirectoryOverrideForTests. Mirrors HealthSceneTests' seam exactly
        // (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない).
        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s06-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            HealthComponent.SaveDirectoryOverrideForTests = _tempSaveDir;
            HealthComponent.SaveInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            EnemyAgent.ActiveEnemies.Clear();
            AutoAttackDriver.AttackIntervalOverrideForTests = null;
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
        public IEnumerator EnemyWithinRange_TakesDamageInBaseIncrements_AndDiesAfterTwoHits()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController+AutoAttackDriver");

            EnemyAgent inRange = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f));
            EnemyAgent outOfRange = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 3f, 0f, 0f));

            Assert.AreEqual(GameConfig.Enemy.HpBase, inRange.CurrentHp);
            Assert.AreEqual(GameConfig.Enemy.HpBase, outOfRange.CurrentHp);

            // --- first tick (~AUTO_ATTACK_INTERVAL = 0.6s) ---
            Time.timeScale = 5f;
            int hpAfterFirstTick = -1;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (inRange == null)
                {
                    Assert.Fail("in-range enemy must not die on the first hit (ENEMY_HP_BASE=40, AUTO_ATTACK_DAMAGE_BASE=20 → 2 hits)");
                }
                if (inRange.CurrentHp < GameConfig.Enemy.HpBase)
                {
                    hpAfterFirstTick = inRange.CurrentHp;
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.AreEqual(GameConfig.Enemy.HpBase - GameConfig.Player.AutoAttackDamageBase, hpAfterFirstTick,
                "HP must drop by exactly one AUTO_ATTACK_DAMAGE_BASE increment per interval tick");
            Assert.AreEqual(GameConfig.Enemy.HpBase, outOfRange.CurrentHp,
                "an enemy outside AUTO_ATTACK_RANGE must never be targeted");

            // --- second tick: must defeat the enemy (2 hits total, gdd TTK≈1.2s) ---
            Time.timeScale = 5f;
            bool died = false;
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (inRange == null)
                {
                    died = true;
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return null;

            Assert.IsTrue(died, "ENEMY_HP_BASE must be defeated in exactly 2 hits of AUTO_ATTACK_DAMAGE_BASE (撃破・消滅)");
            Assert.AreEqual(GameConfig.Enemy.HpBase, outOfRange.CurrentHp,
                "out-of-range enemy must remain fully untouched throughout");

            Object.Destroy(outOfRange.gameObject);
        }

        [UnityTest]
        public IEnumerator HeldMovementAndDashInput_DoesNotAffectAttackTiming()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            EnemyAgent inRange = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f));

            // Hold movement + dash input for the whole window — gdd 操作仕様: 自動攻撃には攻撃ボタンの割当
            // 自体が存在しない。Any other input action firing must not gate/delay the attack tick.
            Press(keyboard.wKey);
            Press(keyboard.spaceKey);

            Time.timeScale = 5f;
            int hpAfterFirstTick = -1;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (inRange == null)
                {
                    Assert.Fail("must not die on the first hit");
                }
                if (inRange.CurrentHp < GameConfig.Enemy.HpBase)
                {
                    hpAfterFirstTick = inRange.CurrentHp;
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Release(keyboard.wKey);
            Release(keyboard.spaceKey);
            yield return null;

            Assert.AreEqual(GameConfig.Enemy.HpBase - GameConfig.Player.AutoAttackDamageBase, hpAfterFirstTick,
                "auto-attack must fire on schedule regardless of held movement/dash input");
        }

        // CR-CODE S-16 iter2 minor finding #2: locks in the iter1 major #2 fix (AutoAttackDriver.Update's
        // PlayerController.IsLocked gate) so a future refactor that removes/weakens it silently
        // reintroduces "attack trigger/SFX fights the Idle death pose". GameConfig.Fx.DeathSequenceDuration
        // (0.5s) is shorter than GameConfig.Player.AutoAttackInterval (0.6s), so a freshly-created driver's
        // real timer would rarely reach a tick inside the death window on its own — that would make a naive
        // "zero attacks during death" assertion vacuously true rather than actually proving the gate does
        // something. AttackIntervalOverrideForTests shortens the interval so several ticks are guaranteed
        // to fall inside the window if the gate were removed.
        [UnityTest]
        public IEnumerator DyingHero_DoesNotFireAutoAttack_DuringDeathFadeSequence()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController+AutoAttackDriver");
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            var driver = player.GetComponent<AutoAttackDriver>();
            Assert.IsNotNull(driver);

            AutoAttackDriver.AttackIntervalOverrideForTests = 0.01f;

            CreateEnemy(player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f));

            // Test-setup sanity check: confirm the shortened interval override actually lets attacks land
            // before death — otherwise a "count never changes" result later would be vacuous.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (driver.AttackCallCountForTests == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.Greater(driver.AttackCallCountForTests, 0,
                "test setup: the shortened AttackIntervalOverrideForTests must let at least one pre-death attack land");

            // Deterministic single-frame-lethal contact — same technique as
            // HealthSceneTests.HpReachesZero_TransitionsToResult_AndSavesExactlyOnce /
            // HeroFxSceneTests.Death_....
            int enemiesNeeded = Mathf.CeilToInt((float)GameConfig.Player.MaxHpBase / GameConfig.Enemy.ContactDamage) + 2;
            for (int i = 0; i < enemiesNeeded; i++)
            {
                CreateEnemy(player.transform.position);
            }

            deadline = Time.realtimeSinceStartup + 5f;
            while (!health.IsDeathSequenceActive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(health.IsDeathSequenceActive, "test setup: lethal simultaneous contact must start the death sequence");

            // Baseline captured only once the death sequence is confirmed active (i.e. after all Update()s
            // for this frame — including any same-frame ordering race between AutoAttackDriver and
            // HealthComponent — have already run), so it already absorbs the accepted one-frame race noted
            // on AutoAttackDriver.Update's IsLocked gate rather than mistaking it for a bug here.
            int attackCountAtDeathStart = driver.AttackCallCountForTests;

            int deathWindowSampleCount = 0;
            float deathWindowDeadline = Time.realtimeSinceStartup + GameConfig.Fx.DeathSequenceDuration + 0.3f;
            while (Time.realtimeSinceStartup < deathWindowDeadline
                && SceneManager.GetActiveScene().name == GameConfig.Scenes.Game)
            {
                deathWindowSampleCount++;
                Assert.AreEqual(attackCountAtDeathStart, driver.AttackCallCountForTests,
                    "AutoAttackDriver must not land any attack while PlayerController.IsLocked (death sequence active)");
                yield return null;
            }
            Assert.Greater(deathWindowSampleCount, 0,
                "test setup: must have observed at least one frame with the death sequence active and the Game scene still loaded (otherwise the death-window assertion above is vacuous)");
        }

        // S-17: 自動攻撃ヒットVFX + 攻撃SFX 同期. Verifies all three landed-hit effects
        // (Components/AutoAttackDriver.TryAttackNearest's VFX-Instantiate/animator-SetTrigger/SFX-Play
        // block) fire together on the same frame a hit lands.
        [UnityTest]
        public IEnumerator AttackLanding_SpawnsHitVfxAtTargetPosition_PlaysSfx_AndTransitionsAnimatorToAttack()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController+AutoAttackDriver");
            var driver = player.GetComponent<AutoAttackDriver>();
            Assert.IsNotNull(driver);
            // CR-CODE S-17 iter2 minor finding fix: HitVfxPrefabForTests is a UnityEngine.Object
            // (GameObject) reference, so use the Unity null-aware `!= null` form (mirrors the `enemy != null`
            // check further below) rather than Assert.IsNotNull's plain CLR reference check — a
            // missing/dangling-GUID serialized reference deserializes to a non-null-CLR-reference "fake null"
            // object, which Assert.IsNotNull would wrongly pass.
            Assert.IsTrue(driver.HitVfxPrefabForTests != null,
                "AutoAttackDriver._hitVfxPrefab must be assigned by Editor/AssetIntegration once IMG-04 is integrated");

            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "hero Animator must be present once Editor/AssetIntegration has integrated ANM-01/02/03");

            AutoAttackDriver.AttackIntervalOverrideForTests = 0.01f;
            // ENEMY_HP_BASE(40) > AUTO_ATTACK_DAMAGE_BASE(20) — survives the first hit, so its position
            // is still available afterwards to compare against the spawned VFX's position.
            EnemyAgent enemy = CreateEnemy(player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f));

            // CR-CODE S-17 iter1 minor finding fix: identify the spawned VFX via a before/after *set*
            // difference rather than "the last element of FindObjectsByType(..., FindObjectsSortMode.None)"
            // — that ordering is explicitly unspecified by Unity, so a future unrelated ParticleSystem in
            // the scene (e.g. an environment VFX) could make vfxAfter[^1] the wrong instance and turn this
            // into a flaky/false-positive position assertion instead of a hard failure.
            ParticleSystem[] vfxBefore = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            var vfxBeforeSet = new HashSet<ParticleSystem>(vfxBefore);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (driver.AttackCallCountForTests == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.Greater(driver.AttackCallCountForTests, 0, "test setup: at least one attack must land within the deadline");

            ParticleSystem[] vfxAfter = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            // CR-CODE S-17 iter2 minor finding fix: this used to also assert the raw
            // vfxAfter.Length > vfxBefore.Length count, which reintroduces exactly the flake the set-diff
            // below exists to avoid (comment above) — if any pre-existing ParticleSystem in vfxBefore
            // despawns (e.g. stopAction=Destroy on a short-lived, unrelated environment VFX) during the
            // attack-wait window, vfxAfter.Length can equal (or be less than) vfxBefore.Length even though
            // the hit VFX really was spawned, turning a passing run into a false failure. Replaced with a
            // new-instance count derived from the same before/after set difference used to identify
            // spawnedVfx below, so there is exactly one source of truth for "was a VFX spawned".
            var newVfxInstances = new List<ParticleSystem>();
            foreach (ParticleSystem ps in vfxAfter)
            {
                if (!vfxBeforeSet.Contains(ps))
                {
                    newVfxInstances.Add(ps);
                }
            }
            Assert.GreaterOrEqual(newVfxInstances.Count, 1,
                "a hit VFX ParticleSystem must be instantiated the frame an attack lands (gdd: ヒット箇所に短命VFXを発生させ単体ヒットでも当たり手応えを補う)");

            ParticleSystem spawnedVfx = newVfxInstances.Count > 0 ? newVfxInstances[0] : null;
            Assert.IsNotNull(spawnedVfx,
                "test setup: the newly-instantiated hit VFX ParticleSystem must be identifiable via before/after set difference");

            // enemy is a UnityEngine.Object subtype (Component) — Assert.IsNotNull uses the CLR reference
            // check and would pass on a Destroy()-ed-but-not-yet-collected object (Unity's `== null`
            // overload is what actually detects "destroyed"), which would turn a real "the single hit
            // killed the enemy" bug into a MissingReferenceException on the Distance call below instead of
            // this intended, clearly-worded assertion (CR-CODE S-17 iter1 minor finding).
            Assert.IsTrue(enemy != null, "test setup: a single hit must not defeat the enemy (needed to compare the VFX spawn position)");
            Assert.Less(Vector3.Distance(spawnedVfx.transform.position, enemy.transform.position), 1f,
                "hit VFX must spawn at (near) the target enemy's position, not the player's or the world origin");

            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.AttackHit, "SFX-01 must be assigned by Editor/AssetIntegration");
            // CR-CODE S-19 iter2 minor finding fix: the shared AudioSource's isPlaying flag is exactly the
            // illusory regression protection SfxLibrary's own S-19 header comment warns about — this test's
            // enemy sits inside AutoAttackRange for the whole window, so any *other* SFX sharing the source
            // (or a stale isPlaying carried over from a previous PlayOneShot) would make this assertion pass
            // even if SFX-01 itself never fired. Assert the per-clip counter (mirrors
            // HealthSceneTests/DashSceneTests/CrystalSceneTests' identical fix) instead — exactly one attack
            // landed by this point (the wait loop above breaks the instant AttackCallCountForTests leaves 0).
            Assert.AreEqual(1, SfxLibrary.Instance.AttackHitTriggerCountForTests,
                "SFX-01 must start playing the same frame the attack lands (gdd: VFXと同期)");

            // Animator.SetTrigger only *requests* the AnyState->Attack transition — the state machine
            // itself evaluates and commits it during the Animator's own per-frame update pass, which runs
            // after this script's Update() within the same frame the trigger was set (mirrors
            // AssetIntegrationSceneTests.HeroVisual_HasValidHumanoidAnimatorAndNoMaterialErrors's own
            // wait before reading GetCurrentAnimatorStateInfo). One extra frame is enough since the
            // AnyState->Attack transition has hasExitTime=false/duration=0 (instant).
            yield return null;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.IsFalse(state.IsName("__preview__"), "Animator must not be stuck on the auto-generated __preview__ clip");
            Assert.IsTrue(state.IsName("Attack"), "Animator must be in the Attack state right after a landed hit");
            // gdd 決定: 「アニメ長が AUTO_ATTACK_INTERVAL を超える場合は再生速度でスケールする」— the state's
            // actual playback duration (length already reflects the authored speed multiplier) must never
            // exceed the interval, however long the underlying ANM-01 clip is.
            Assert.LessOrEqual(state.length, GameConfig.Player.AutoAttackInterval + 0.05f,
                "Attack state's playback duration (clip length / authored AnimatorState.speed) must not exceed AUTO_ATTACK_INTERVAL");
        }
    }
}
