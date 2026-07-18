// HealthSceneTests — S-08: プレイヤーHP・被弾・死亡判定 → Result 遷移.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries HealthComponent) and
// manually places a touching EnemyAgent (bypassing WaveSpawner, mirroring AutoAttackSceneTests'
// technique) to keep ENEMY_CONTACT_COOLDOWN timing assertions deterministic. Frame-polls state
// (rather than a single fixed wait) to catch each HP-drop/death transition on the exact frame it
// happens. Persistence assertions point Components/HealthComponent.SaveDirectoryOverrideForTests at
// Application.temporaryCachePath (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない).
using System.Collections;
using System.IO;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class HealthSceneTests : InputTestFixture
    {
        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s08-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            HealthComponent.SaveDirectoryOverrideForTests = _tempSaveDir;
            HealthComponent.SaveInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
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

        [UnityTest]
        public IEnumerator ContinuousContact_ReducesPlayerHp_ByAtLeastOneContactDamageIncrement()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health, "Game scene's Player must carry HealthComponent (Editor/SceneWiring.WireGame)");
            Assert.AreEqual(GameConfig.Player.MaxHpBase, health.CurrentHp, "fresh run must start at effectiveMaxHp (Lv0 baseline)");

            CreateTouchingEnemy(player.transform.position);

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

            Assert.AreEqual(GameConfig.Player.MaxHpBase - GameConfig.Enemy.ContactDamage, hpAfterFirstTick,
                "HP must drop by exactly one ENEMY_CONTACT_DAMAGE increment per ENEMY_CONTACT_COOLDOWN interval");

            // CR-CODE s-19 major finding (escalated across iterations 1/2/3, resolved here): SFX-03
            // (プレイヤー被弾) trigger->playback was tested via the shared SfxLibrary AudioSource's
            // isPlaying flag, which is an illusory regression guard in this specific test — CreateTouchingEnemy
            // spawns the contact enemy at the player's exact position (distance 0, inside AutoAttackRange),
            // so auto-attack fires SFX-01 on the same shared source throughout the up-to-5s wait above,
            // keeping isPlaying == true regardless of whether HealthComponent actually played SFX-03. Assert
            // the per-clip trigger counter (SfxLibrary.PlayerHitTriggerCountForTests) instead — it only
            // increments when the PlayerHit clip specifically is played (SfxLibrary.IncrementTriggerCountForTests
            // matches by AudioClip reference), so a regression that stops the PlayerHit call site fails this
            // test even while auto-attack's SFX-01 keeps the shared source busy.
            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.PlayerHit, "SFX-03 must be assigned by Editor/AssetIntegration");
            Assert.AreEqual(1, SfxLibrary.Instance.PlayerHitTriggerCountForTests,
                "SFX-03 (PlayerHit clip specifically) must have been played exactly once by the frame the HP drop is observed");
        }

        [UnityTest]
        public IEnumerator ContactDuringDashInvulnerability_NeverReducesPlayerHp()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);

            // Touching enemy present before the dash starts, so contact/cooldown evaluation is
            // already exercised on the very first invulnerable frame (cooldown starts at 0 = ready).
            CreateTouchingEnemy(player.transform.position);

            Press(keyboard.dKey);
            yield return null;
            Press(keyboard.spaceKey);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!player.IsDashInvulnerable && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(player.IsDashInvulnerable, "dash must have activated its invuln window");

            int hpAtInvulnStart = health.CurrentHp;

            deadline = Time.realtimeSinceStartup + 2f;
            while (player.IsDashInvulnerable && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsFalse(player.IsDashInvulnerable, "invuln flag must clear after DASH_INVULN_DURATION");

            Assert.AreEqual(hpAtInvulnStart, health.CurrentHp,
                "continuous contact during the dash invuln window must not reduce HP");

            Release(keyboard.spaceKey);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator HpReachesZero_TransitionsToResult_AndSavesExactlyOnce()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);

            // Spawn enough SIMULTANEOUSLY-touching enemies that a single contact-evaluation frame
            // drops HP from PLAYER_MAX_HP_BASE to <=0 in one shot. gdd 敵・障害物: 「複数同時接触で一気に
            // 脅威化」— each enemy tracks its OWN ENEMY_CONTACT_COOLDOWN (starts elapsed/ready), so N
            // simultaneous contacts each land a hit on the very first frame. This deliberately avoids
            // relying on several real ENEMY_CONTACT_COOLDOWN (0.5s) cycles under Time.timeScale
            // speed-up: Editor batchmode can execute tens of thousands of near-zero-deltaTime frames
            // per real second, so accumulated scaled game time does not track wall-clock 1:1 over a
            // multi-second window — deterministic single-frame lethal contact sidesteps that entirely.
            int enemiesNeeded = Mathf.CeilToInt((float)GameConfig.Player.MaxHpBase / GameConfig.Enemy.ContactDamage) + 2;
            for (int i = 0; i < enemiesNeeded; i++)
            {
                CreateTouchingEnemy(player.transform.position);
            }

            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != GameConfig.Scenes.Result && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name,
                "HP<=0 must eventually lock input, play the death sequence, and load Result");
            Assert.AreEqual(1, HealthComponent.SaveInvocationCountForTests,
                "MetaProgression.ApplyRunResult→FileSaveAdapter.Save must happen exactly once per run");

            // The save must have actually landed on disk in the overridden test directory.
            string savePath = Path.Combine(_tempSaveDir, GameConfig.Save.FileName);
            Assert.IsTrue(File.Exists(savePath), "Save() must write to the overridden test save directory, not the real persistentDataPath");
        }
    }
}
