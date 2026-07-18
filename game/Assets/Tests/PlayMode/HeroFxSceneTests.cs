// HeroFxSceneTests — S-16: 死亡演出（コード合成）+ 被弾マテリアルフラッシュ.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries HeroFxController
// alongside HealthComponent) and drives the same touching-enemy technique HealthSceneTests/
// GameHudSceneTests already use: a single enemy for a non-lethal hit, and enough SIMULTANEOUSLY-touching
// enemies for a deterministic single-frame lethal hit (mirrors HealthSceneTests.HpReachesZero...'s own
// rationale for avoiding a multi-cooldown-cycle wait). Persistence assertions point
// Components/HealthComponent.SaveDirectoryOverrideForTests at Application.temporaryCachePath
// (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using ForgeGame.Components;
using ForgeGame.Systems;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class HeroFxSceneTests
    {
        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s16-save-test-" + Guid.NewGuid().ToString("N"));
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
                UnityEngine.Object.DestroyImmediate(SessionHolder.Instance.gameObject);
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
            var spawner = UnityEngine.Object.FindFirstObjectByType<WaveSpawner>();
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
        public IEnumerator NonLethalHit_FlashesHeroMaterial_ThenRevertsToBaseColor_WithoutTriggeringDeathFx()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            var heroFx = player.GetComponent<HeroFxController>();
            Assert.IsNotNull(heroFx, "Game scene's Player must carry HeroFxController (Editor/SceneWiring.WireGame)");

            yield return null; // let HeroFxController.Start resolve its renderers/base colors
            Color baseColor = heroFx.CurrentMaterialColor;
            Assert.AreEqual(0f, heroFx.HitFlashIntensity, "no flash before any hit has landed");

            CreateTouchingEnemy(player.transform.position);

            // Wait for the first (non-lethal) contact hit — mirrors HealthSceneTests.ContinuousContact....
            float deadline = Time.realtimeSinceStartup + 5f;
            while (health.CurrentHp == GameConfig.Player.MaxHpBase && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.Less(health.CurrentHp, GameConfig.Player.MaxHpBase,
                "test setup: contact must have reduced HP (a single ENEMY_CONTACT_DAMAGE increment is far from lethal at PLAYER_MAX_HP_BASE)");
            Assert.IsFalse(health.IsDeathSequenceActive, "test setup: a single-enemy contact hit must not be lethal");

            // One more frame guarantees HeroFxController.Update has observed the HP drop regardless of
            // this-frame component execution ordering relative to HealthComponent.
            yield return null;
            Assert.Greater(heroFx.HitFlashIntensity, 0f, "hit flash intensity must be > 0 shortly after a non-lethal hit");
            Assert.AreNotEqual(baseColor, heroFx.CurrentMaterialColor, "hero material color must visibly change during the flash");
            Assert.IsFalse(heroFx.IsDeathFxActive, "a non-lethal hit must never start the death fade/tilt sequence");

            // CR-CODE S-16 iter1 minor finding: AreNotEqual above only proves *some* change happened —
            // it would also pass for a regression that blended toward the wrong constant (e.g. the death
            // dissolve color instead of ColorHitFlash). Recompute the exact expected blend from the same
            // Systems/HeroFxSystem.ComputeHitFlashColor formula HeroFxController itself calls, using the
            // intensity HeroFxController just applied this frame, and require an exact match.
            // CR-CODE S-16 iter2 minor finding: don't ignore the parse's success flag — a false return
            // would silently leave expectedFlashColor at default(Color)=(0,0,0,0), producing a mismatched
            // expected blend whose assertion failure message wouldn't point at the real cause (an invalid
            // GameConfig.Ui.ColorHitFlash constant, the same failure mode HeroFxController.
            // ParseColorOrFallback itself logs+recovers from in production).
            Assert.IsTrue(ColorUtility.TryParseHtmlString(GameConfig.Ui.ColorHitFlash, out Color expectedFlashColor),
                "GameConfig.Ui.ColorHitFlash must be a parseable hex color");
            Color expectedBlend = HeroFxSystem.ComputeHitFlashColor(baseColor, expectedFlashColor, heroFx.HitFlashIntensity);
            AssertColorApproximatelyEqual(expectedBlend, heroFx.CurrentMaterialColor,
                "hero material color must match ComputeHitFlashColor(base, ColorHitFlash, intensity) exactly, not just differ from base");

            // The flash must fully decay back to the base color within HitFlashDuration (well under
            // ENEMY_CONTACT_COOLDOWN=0.5s, so this observes one clean flash-and-revert cycle).
            deadline = Time.realtimeSinceStartup + 2f;
            while (heroFx.HitFlashIntensity > 0f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(0f, heroFx.HitFlashIntensity, "hit flash intensity must decay to exactly 0 within HitFlashDuration");
            Assert.AreEqual(baseColor, heroFx.CurrentMaterialColor, "hero material color must revert exactly to its base color once the flash ends");
        }

        [UnityTest]
        public IEnumerator Death_FadesAndTiltsHeroMaterial_DrivesHudDissolve_ThenTransitionsToResult_AfterFullDeathSequenceDuration()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            var heroFx = player.GetComponent<HeroFxController>();
            Assert.IsNotNull(heroFx);
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud, "Game scene must be wired with a GameHud carrying the death dissolve overlay");

            Animator animator = player.GetComponentInChildren<Animator>();
            RuntimeAnimatorController controllerBeforeDeath = animator != null ? animator.runtimeAnimatorController : null;

            // Deterministic single-frame-lethal contact — same technique as
            // HealthSceneTests.HpReachesZero_TransitionsToResult_AndSavesExactlyOnce.
            int enemiesNeeded = Mathf.CeilToInt((float)GameConfig.Player.MaxHpBase / GameConfig.Enemy.ContactDamage) + 2;
            for (int i = 0; i < enemiesNeeded; i++)
            {
                CreateTouchingEnemy(player.transform.position);
            }

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!health.IsDeathSequenceActive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(health.IsDeathSequenceActive, "test setup: lethal simultaneous contact must start the death sequence");
            float deathStartRealtime = Time.realtimeSinceStartup;

            yield return null; // guarantees HeroFxController has observed IsDeathSequenceActive this frame
            Assert.IsTrue(heroFx.IsDeathFxActive, "HeroFxController must begin its own fade/tilt sequence once IsDeathSequenceActive flips true");
            Assert.AreEqual(0f, heroFx.HitFlashIntensity, "hit flash must never activate once the death sequence has taken over");

            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                Assert.IsFalse(state.IsName("__preview__"),
                    "hero Animator must not be stuck on Unity's auto-generated __preview__ clip during the death fade");
                Assert.AreSame(controllerBeforeDeath, animator.runtimeAnimatorController,
                    "the death sequence must reuse the existing Hero AnimatorController (idle state) — no new clip/controller swap");
            }

            // Mid-sequence sample: fade alpha decreasing and tilt increasing partway through (not an
            // instantaneous jump straight to the end values).
            yield return new WaitForSecondsRealtime(GameConfig.Fx.DeathSequenceDuration * 0.4f);

            // CR-CODE S-16 iter1 minor finding: the strict open-interval assertions this block used to
            // make (alpha/tilt/dissolve all strictly between their start and end values) are flaky under
            // a batchmode frame hitch — DeathSequenceDuration is only 0.5s, so a ~0.3s+ hitch across the
            // preceding yields can let the sequence finish (and even transition to Result, destroying the
            // Player/heroFx and HUD GameObjects this block reads) before this line runs. Guard on the
            // active scene first.
            //
            // CR-CODE S-16 iter2 minor finding: iter1's de-flake weakened every mid-sequence assertion to
            // closed-range bounds (near-tautological against HeroFxSystem's Clamp01-based math) plus "at
            // least one channel moved off its start value" — which would also pass a regression that
            // snaps straight to the terminal fade/tilt/dissolve values the instant death starts. Restore
            // the strict gradualness check, but only when elapsed *measured* real time since death start
            // proves this sample really did land mid-sequence (a hitch large enough to finish the fade
            // would push elapsedSinceDeathStart past DeathSequenceDuration, so falling into the "provably
            // mid-sequence" branch below and still seeing a terminal value is a genuine regression, not a
            // scheduling artifact). Under an actual hitch we fall back to the weaker (but still
            // non-vacuous) "some channel progressed" check, exactly as iter1 de-flaked it.
            float elapsedSinceDeathStart = Time.realtimeSinceStartup - deathStartRealtime;
            if (SceneManager.GetActiveScene().name == GameConfig.Scenes.Game)
            {
                Assert.LessOrEqual(heroFx.DeathFadeAlpha, 1f, "fade alpha must never exceed its starting value of 1");
                Assert.GreaterOrEqual(heroFx.DeathFadeAlpha, 0f, "fade alpha must never go below its terminal value of 0");
                Assert.GreaterOrEqual(heroFx.DeathTiltDeg, 0f, "tilt must never go below its starting value of 0");
                Assert.LessOrEqual(heroFx.DeathTiltDeg, GameConfig.Fx.DeathTiltMaxDeg, "tilt must never exceed DeathTiltMaxDeg");
                Assert.GreaterOrEqual(hud.DeathDissolveOverlay.color.a, 0f, "HUD dissolve overlay alpha must never go below 0");
                Assert.LessOrEqual(hud.DeathDissolveOverlay.color.a, 1f, "HUD dissolve overlay alpha must never exceed 1");

                if (elapsedSinceDeathStart < GameConfig.Fx.DeathSequenceDuration - 0.05f)
                {
                    // Provably mid-sequence — not an instantaneous jump straight to the end values.
                    Assert.Less(heroFx.DeathFadeAlpha, 1f,
                        "fade alpha must have started decreasing from 1 by this point in the sequence (not an instantaneous jump to 0)");
                    Assert.Greater(heroFx.DeathTiltDeg, 0f,
                        "tilt must have started increasing from 0 by this point in the sequence (not an instantaneous jump to max)");
                }
                else
                {
                    bool anyChannelProgressed = heroFx.DeathFadeAlpha < 1f || heroFx.DeathTiltDeg > 0f
                        || hud.DeathDissolveOverlay.color.a > 0f;
                    Assert.IsTrue(anyChannelProgressed,
                        "at least one death fx channel (fade/tilt/dissolve) must have started progressing by the time this sample was taken (even under a hitch that nearly completed the sequence)");
                }
            }

            // Wait for the actual Result transition — Components/HealthComponent's own death timer is the
            // authoritative gate this story's fx are deliberately kept in lockstep with (see
            // Components/HeroFxController's header comment), not read from directly.
            deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != GameConfig.Scenes.Result && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            float deathEndRealtime = Time.realtimeSinceStartup;

            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name,
                "HP<=0 must eventually transition to Result once the death sequence completes");
            Assert.GreaterOrEqual(deathEndRealtime - deathStartRealtime, GameConfig.Fx.DeathSequenceDuration - 0.05f,
                "the Result transition must not happen before (approximately) DeathSequenceDuration has elapsed");
        }

        /// <summary>CR-CODE S-28 iteration 1 major finding fix regression test: Editor/AssetIntegration.
        /// ApplyOutlineToRenderers (S-28) appends the "ForgeGame/Outline" inverse-hull material as an
        /// extra sharedMaterials slot on every hero Renderer, baked into Hero.prefab. That material is
        /// Opaque/ZWrite On/Cull Front with no alpha-blending support at all, so before this fix
        /// HeroFxController's death fade only ever faded slot 0 (the base PBR material) — the outline slot
        /// kept rendering fully opaque, leaving a solid dark-navy silhouette hull on screen instead of the
        /// hero visually disappearing. Machine-verifies the fix strips that slot the instant the fade
        /// starts.</summary>
        [UnityTest]
        public IEnumerator Death_RemovesOutlineMaterialSlot_SoFadedHeroDoesNotLeaveASolidNavyHullBehind()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health);
            var heroFx = player.GetComponent<HeroFxController>();
            Assert.IsNotNull(heroFx);

            yield return null; // let HeroFxController.Start resolve renderers/outline-slot detection

            // Test setup sanity: without an outline slot present before death, this regression test would
            // vacuously pass on a scene where Editor/AssetIntegration hasn't run or the outline material
            // failed to build ([DEGRADED]).
            bool hasOutlineBeforeDeath = player.GetComponentsInChildren<Renderer>()
                .SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && m.shader != null && m.shader.name == GameConfig.Outline.ShaderName);
            Assert.IsTrue(hasOutlineBeforeDeath,
                "test setup: hero must carry the S-28 outline material slot before death " +
                "(Editor/AssetIntegration.ApplyOutlineToRenderers) for this regression test to be meaningful");

            // Deterministic single-frame-lethal contact — same technique as
            // Death_FadesAndTiltsHeroMaterial_....
            int enemiesNeeded = Mathf.CeilToInt((float)GameConfig.Player.MaxHpBase / GameConfig.Enemy.ContactDamage) + 2;
            for (int i = 0; i < enemiesNeeded; i++)
            {
                CreateTouchingEnemy(player.transform.position);
            }

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!health.IsDeathSequenceActive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsTrue(health.IsDeathSequenceActive, "test setup: lethal simultaneous contact must start the death sequence");

            yield return null; // guarantees HeroFxController has observed IsDeathSequenceActive and run BeginDeathFx
            Assert.IsTrue(heroFx.IsDeathFxActive);

            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.materials)
                {
                    bool isOutline = m != null && m.shader != null && m.shader.name == GameConfig.Outline.ShaderName;
                    Assert.IsFalse(isOutline,
                        $"hero Renderer '{r.name}' must not carry the outline material slot while the death " +
                        "fade is active — it is Opaque/ZWrite On/Cull Front and would render as a solid " +
                        "dark-navy hull while the base material fades to transparent");
                }
            }

            // Let the sequence finish so TearDown's scene/session cleanup runs against a settled state.
            deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != GameConfig.Scenes.Result && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GameConfig.Scenes.Result, SceneManager.GetActiveScene().name,
                "test setup: the death sequence must still complete normally with the outline slot stripped");
        }

        /// <summary>Component-wise float tolerance compare (CR-CODE S-16 iter1 minor finding). Unity's
        /// Color has no exposed equality tolerance suitable for cross-checking a value recomputed via the
        /// same formula on the test side vs. the value HeroFxController applied — small float rounding
        /// differences between equivalent Mathf.Lerp call sequences should not fail the test.</summary>
        private static void AssertColorApproximatelyEqual(Color expected, Color actual, string message)
        {
            const float epsilon = 0.01f;
            Assert.Less(Mathf.Abs(expected.r - actual.r), epsilon, message + " (r channel)");
            Assert.Less(Mathf.Abs(expected.g - actual.g), epsilon, message + " (g channel)");
            Assert.Less(Mathf.Abs(expected.b - actual.b), epsilon, message + " (b channel)");
        }
    }
}
