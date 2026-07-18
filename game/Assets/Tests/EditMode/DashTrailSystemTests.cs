// DashTrailSystemTests — S-31: ダッシュ移動のアフターイメージ（トレイル）VFX (gdd P-01).
// Conventions.md §9: new pure Systems get EditMode coverage. Covers the spawn-cadence threshold and the
// ghost alpha fade-to-zero-at-lifetime math.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class DashTrailSystemTests
    {
        [Test]
        public void ShouldSpawnGhost_TrueOnlyOnceTimerReachesInterval()
        {
            Assert.IsFalse(DashTrailSystem.ShouldSpawnGhost(0.02f, GameConfig.Fx.DashTrailSpawnIntervalS));
            Assert.IsTrue(DashTrailSystem.ShouldSpawnGhost(GameConfig.Fx.DashTrailSpawnIntervalS, GameConfig.Fx.DashTrailSpawnIntervalS));
            Assert.IsTrue(DashTrailSystem.ShouldSpawnGhost(GameConfig.Fx.DashTrailSpawnIntervalS + 0.01f, GameConfig.Fx.DashTrailSpawnIntervalS));
        }

        [Test]
        public void ShouldSpawnGhost_NonPositiveInterval_AlwaysFalse_NoInfiniteCatchUpLoop()
        {
            // CR-CODE S-31 iteration 1 minor finding: Components/DashTrailSpawner.Update's catch-up loop
            // (`while (ShouldSpawnGhost(...)) { timer -= interval; ... }`) would spin forever the instant
            // GameConfig.Fx.DashTrailSpawnIntervalS is misconfigured to <=0, since subtracting a
            // non-positive interval never shrinks the timer back below itself. This wiring-error guard
            // must return false regardless of how large spawnTimer has accumulated.
            Assert.IsFalse(DashTrailSystem.ShouldSpawnGhost(1000f, 0f));
            Assert.IsFalse(DashTrailSystem.ShouldSpawnGhost(1000f, -0.01f));
        }

        [Test]
        public void ComputeGhostAlpha_StartsAtInitialAlpha_AndFadesLinearlyToZero()
        {
            float initialAlpha = GameConfig.Fx.DashTrailGhostAlpha;
            float lifetime = GameConfig.Fx.DashTrailGhostLifetimeS;

            Assert.AreEqual(initialAlpha, DashTrailSystem.ComputeGhostAlpha(0f, lifetime, initialAlpha), 1e-5f,
                "alpha at spawn (elapsed=0) must equal the configured initial alpha");

            float halfway = DashTrailSystem.ComputeGhostAlpha(lifetime * 0.5f, lifetime, initialAlpha);
            Assert.AreEqual(initialAlpha * 0.5f, halfway, 1e-5f, "fade must be linear");

            Assert.AreEqual(0f, DashTrailSystem.ComputeGhostAlpha(lifetime, lifetime, initialAlpha), 1e-5f,
                "alpha must reach exactly 0 the instant elapsed reaches the lifetime");
        }

        [Test]
        public void ComputeGhostAlpha_ClampsPastLifetime_NeverNegative()
        {
            float alpha = DashTrailSystem.ComputeGhostAlpha(
                GameConfig.Fx.DashTrailGhostLifetimeS * 2f, GameConfig.Fx.DashTrailGhostLifetimeS, GameConfig.Fx.DashTrailGhostAlpha);
            Assert.AreEqual(0f, alpha, "alpha must clamp at 0 past the lifetime, never go negative");
        }

        [Test]
        public void ComputeGhostAlpha_NonPositiveLifetime_ReturnsZero_NoDivideByZero()
        {
            Assert.AreEqual(0f, DashTrailSystem.ComputeGhostAlpha(0.1f, 0f, GameConfig.Fx.DashTrailGhostAlpha));
        }

        [Test]
        public void IsGhostExpired_FalseBeforeLifetime_TrueAtOrAfter()
        {
            float lifetime = GameConfig.Fx.DashTrailGhostLifetimeS;
            Assert.IsFalse(DashTrailSystem.IsGhostExpired(lifetime * 0.5f, lifetime));
            Assert.IsTrue(DashTrailSystem.IsGhostExpired(lifetime, lifetime));
            Assert.IsTrue(DashTrailSystem.IsGhostExpired(lifetime * 1.5f, lifetime));
        }
    }
}
