// CrystalVfxSceneTests — S-29: クリスタル発光強化 + パーティクル juice（回収フラッシュ含む）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame + Editor/AssetIntegration.IntegrateAll's
// baked CrystalVfxLibrary wiring) and machine-verifies the acceptance from state/stories.yaml S-29:
// (1) a spawned crystal carries an emissive material with no Renderer/material gaps plus an attached
// ambient glow ParticleSystem, (2) auto-collect fires the collect-flash VFX (via
// Components/CrystalVfxLibrary's test-observability counters, same frame as SFX-04 — mirrors
// CrystalSceneTests' SfxLibrary.CrystalPickupTriggerCountForTests pattern), and (3) the glow/collect
// particle parameters actually come from GameConfig.Crystal (not hardcoded in the built prefabs).
using System.Collections;
using ForgeGame.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class CrystalVfxSceneTests
    {
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";

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

        [UnityTest]
        public IEnumerator SpawnDrop_AttachesBoostedEmissiveMaterialAndGlowParticleSystem_WithNoRendererOrMaterialGaps()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            // Far enough that auto-pickup doesn't destroy the crystal before this test inspects it.
            Vector3 farPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 5f, 0f, 0f);
            CrystalPickup.SpawnDrop(farPosition, 1);
            CrystalPickup[] spawned = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(1, spawned.Length, "SpawnDrop(pos, 1) must create exactly one crystal");
            CrystalPickup crystal = spawned[0];

            Renderer renderer = crystal.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "crystal must carry a Renderer");
            Material material = renderer.sharedMaterial;
            Assert.IsNotNull(material, "crystal Renderer must not have a null material slot");
            Assert.AreNotEqual(InternalErrorShaderName, material.shader.name,
                "crystal material fell back to InternalErrorShader (pink) — not URP-compatible");
            Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), "crystal material must have emission enabled (発光マテリアル)");

            Assert.IsTrue(ColorUtility.TryParseHtmlString(GameConfig.Ui.ColorCrystalCyan, out Color baseColor));
            Color expectedEmission = baseColor * GameConfig.Crystal.EmissionIntensity * GameConfig.Crystal.GlowHaloEmissionBoost;
            Color actualEmission = material.GetColor("_EmissionColor");
            const float tolerance = 1e-4f;
            Assert.AreEqual(expectedEmission.r, actualEmission.r, tolerance, "crystal emission R must equal baseColor.r * EmissionIntensity * GlowHaloEmissionBoost (GameConfig-sourced, no magic numbers)");
            Assert.AreEqual(expectedEmission.g, actualEmission.g, tolerance, "crystal emission G must equal baseColor.g * EmissionIntensity * GlowHaloEmissionBoost");
            Assert.AreEqual(expectedEmission.b, actualEmission.b, tolerance, "crystal emission B must equal baseColor.b * EmissionIntensity * GlowHaloEmissionBoost");

            // S-29: ambient glow-halo ParticleSystem, attached as a child by Components/CrystalVfxLibrary.SpawnGlow.
            ParticleSystem glow = crystal.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(glow, "crystal must have an attached glow-halo ParticleSystem (S-29 パーティクル juice)");
            ParticleSystemRenderer glowRenderer = glow.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(glowRenderer);
            Assert.IsNotNull(glowRenderer.sharedMaterial, "crystal glow ParticleSystem must not have a null material slot");
            Assert.AreNotEqual(InternalErrorShaderName, glowRenderer.sharedMaterial.shader.name,
                "crystal glow ParticleSystem material fell back to InternalErrorShader (pink) — not URP-compatible");

            Assert.IsNotNull(CrystalVfxLibrary.Instance, "Game scene must be wired with a CrystalVfxLibrary (Editor/AssetIntegration.PatchCrystalVfxLibrary)");
            Assert.GreaterOrEqual(CrystalVfxLibrary.Instance.GlowVfxSpawnCountForTests, 1,
                "SpawnDrop must have fired CrystalVfxLibrary.SpawnGlow at least once");
        }

        [UnityTest]
        public IEnumerator AutoCollect_FiresCollectVfx_SameFrameAsSfx()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var stats = Object.FindFirstObjectByType<RunStatsTracker>();
            Assert.IsNotNull(stats);
            Assert.IsNotNull(CrystalVfxLibrary.Instance, "Game scene must be wired with a CrystalVfxLibrary");
            int collectCountBefore = CrystalVfxLibrary.Instance.CollectVfxSpawnCountForTests;

            Vector3 nearPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 0.5f, 0f, 0f);
            CrystalPickup.SpawnDrop(nearPosition, 1);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (stats.CrystalsCollected == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(1, stats.CrystalsCollected, "a crystal spawned within CRYSTAL_PICKUP_RADIUS must be auto-collected");
            Assert.AreEqual(collectCountBefore + 1, CrystalVfxLibrary.Instance.CollectVfxSpawnCountForTests,
                "auto-collect must fire exactly one collect-flash VFX (S-29 回収 juice)");
            // Mirrors CrystalSceneTests' regression guard: SFX-04 must have fired on the exact same pickup.
            Assert.IsNotNull(SfxLibrary.Instance);
            Assert.AreEqual(1, SfxLibrary.Instance.CrystalPickupTriggerCountForTests,
                "SFX-04 must have fired exactly once alongside the collect VFX (同一フレーム同期)");
        }

        [UnityTest]
        public IEnumerator CrystalVfxLibrary_BuiltPrefabs_UseGameConfigParameters_NotHardcodedValues()
        {
            yield return LoadGameScene();

            Assert.IsNotNull(CrystalVfxLibrary.Instance, "requires a loaded Game scene with a wired CrystalVfxLibrary");
            GameObject glowPrefab = CrystalVfxLibrary.Instance.GlowVfxPrefab;
            GameObject collectPrefab = CrystalVfxLibrary.Instance.CollectVfxPrefab;
            Assert.IsNotNull(glowPrefab, "CrystalVfxLibrary.GlowVfxPrefab must be assigned by Editor/AssetIntegration");
            Assert.IsNotNull(collectPrefab, "CrystalVfxLibrary.CollectVfxPrefab must be assigned by Editor/AssetIntegration");

            ParticleSystem glowPs = glowPrefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(glowPs);
            Assert.AreEqual(GameConfig.Crystal.GlowParticleLifetime, glowPs.main.startLifetime.constant, 1e-5f,
                "glow ParticleSystem startLifetime must come from GameConfig.Crystal.GlowParticleLifetime");
            Assert.AreEqual(GameConfig.Crystal.GlowParticleStartSize, glowPs.main.startSize.constant, 1e-5f,
                "glow ParticleSystem startSize must come from GameConfig.Crystal.GlowParticleStartSize");
            Assert.AreEqual(GameConfig.Crystal.GlowParticleMaxParticles, glowPs.main.maxParticles,
                "glow ParticleSystem maxParticles must come from GameConfig.Crystal.GlowParticleMaxParticles");
            Assert.AreEqual(GameConfig.Crystal.GlowParticleEmissionRate, glowPs.emission.rateOverTime.constant, 1e-5f,
                "glow ParticleSystem emission rate must come from GameConfig.Crystal.GlowParticleEmissionRate");

            ParticleSystem collectPs = collectPrefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(collectPs);
            Assert.AreEqual(GameConfig.Crystal.CollectVfxDuration, collectPs.main.duration, 1e-5f,
                "collect ParticleSystem duration (回収フラッシュ長) must come from GameConfig.Crystal.CollectVfxDuration");
            Assert.AreEqual(GameConfig.Crystal.CollectVfxParticleLifetime, collectPs.main.startLifetime.constant, 1e-5f,
                "collect ParticleSystem startLifetime must come from GameConfig.Crystal.CollectVfxParticleLifetime");

            var bursts = new ParticleSystem.Burst[collectPs.emission.burstCount];
            collectPs.emission.GetBursts(bursts);
            Assert.AreEqual(1, bursts.Length);
            Assert.AreEqual((float)GameConfig.Crystal.CollectVfxBurstCount, bursts[0].count.constant, 1e-5f,
                "collect ParticleSystem burst count must come from GameConfig.Crystal.CollectVfxBurstCount");
        }

        // CR-CODE S-29 iter2 minor #1: exercises Components/CrystalPickup.LogVfxLibraryMissingOnce, which
        // previously had zero test coverage (the field comment claimed parity with WaveSpawner/SfxLibrary's
        // instance-field one-shot flags, but CrystalPickup's flag is static — this test is what makes that
        // static flag's reset behavior (RuntimeInitializeOnLoadMethod.SubsystemRegistration, see
        // CrystalPickup.ResetStaticStateForDomainReloadDisabled) actually verifiable rather than
        // order-dependent). Destroys the scene-wired CrystalVfxLibrary singleton to force
        // CrystalPickup.SpawnDrop down its Instance-null branch, then asserts (1) the degrade is logged
        // exactly once — a repeat SpawnDrop call in the same session must NOT log again (LogAssert.Expect
        // consumes the single expected occurrence; Unity Test Framework auto-fails on any further unhandled
        // error log, so a regression back to "logs every time" would fail this test) — and (2) the crystal
        // still spawns without the VFX (a documented no-op degrade, not a hard failure).
        [UnityTest]
        public IEnumerator SpawnDrop_LogsMissingLibraryExactlyOnce_AndStillSpawnsCrystal_WhenCrystalVfxLibraryInstanceIsNull()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            Assert.IsNotNull(CrystalVfxLibrary.Instance,
                "Game scene must be wired with a CrystalVfxLibrary before this test can remove it");
            Object.DestroyImmediate(CrystalVfxLibrary.Instance.gameObject);
            Assert.IsNull(CrystalVfxLibrary.Instance,
                "destroying the singleton's GameObject must clear Instance via CrystalVfxLibrary.OnDestroy");

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Vector3 farPosition = player.transform.position + new Vector3(GameConfig.Crystal.PickupRadius * 5f, 0f, 0f);

            LogAssert.Expect(LogType.Error,
                "[Wiring] CrystalVfxLibrary.Instance is null — S-29 crystal glow/collect VFX disabled this session");
            CrystalPickup.SpawnDrop(farPosition, 1);

            // Second spawn under the same missing-library condition: the one-shot flag must suppress a
            // repeat log. The expectation above was already consumed by the first call, so an unexpected
            // repeat here would surface as an unhandled error log and fail this test automatically.
            CrystalPickup.SpawnDrop(farPosition, 1);
            yield return null;

            CrystalPickup[] spawned = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(2, spawned.Length,
                "both crystals must still spawn — missing CrystalVfxLibrary is a documented VFX-less no-op degrade, not a hard failure");
        }
    }
}
