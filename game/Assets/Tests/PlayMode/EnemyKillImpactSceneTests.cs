// EnemyKillImpactSceneTests — S-32: 撃破インパクト演出（敵消滅ポップ + 小型カメラノッジ）
// (gdd P-02「照準ゼロの自動攻撃」/ P-03「群れ密度の圧力」).
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → Player carries AutoAttackDriver+
// RunStatsTracker, Main Camera carries ArenaCameraRig) and manually places enemies (bypassing
// WaveSpawner, mirroring AutoAttackSceneTests/HeavyEnemySceneTests' technique) to keep the
// confirmed-kill-vs-non-lethal-hit distinction and the pop/nudge timing deterministic.
using System.Collections;
using ForgeGame.Components;
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class EnemyKillImpactSceneTests
    {
        [SetUp]
        public void SetUpCounters()
        {
            ArenaCameraRig.KillNudgeInvocationCountForTests = 0;
        }

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            EnemyAgent.ActiveEnemies.Clear();
            ArenaCameraRig.KillNudgeInvocationCountForTests = 0;
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
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");
            spawner.enabled = false;
        }

        private static EnemyAgent CreateEnemy(Vector3 position, int maxHp, EnemyKind kind = EnemyKind.Swarmer)
        {
            // CreatePrimitive(Cube), not a bare GameObject — mirrors HeavyEnemySceneTests.CreateEnemy:
            // EnemyAgent's kill-pop scales whichever transform carries the Renderer (root, since this test
            // enemy has no "Visual" child), and a bare GameObject would also trip the ApplyHeavyTint
            // Renderer-missing wiring guard for the Heavy sub-test below.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TestEnemy_" + kind;
            go.transform.position = position;
            EnemyAgent agent = go.AddComponent<EnemyAgent>();
            agent.Initialize(GameConfig.Enemy.MoveSpeedBase, maxHp, kind);
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
        public IEnumerator LethalHit_PopsUpThenDisappears_ScoreAndCrystalsAwardedImmediately_CameraNudgeFiresOnce()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController+AutoAttackDriver");
            Assert.IsNotNull(RunStatsTracker.Instance, "Game scene must be wired with a RunStatsTracker");
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig, "Game scene must be wired with ArenaCameraRig on Main Camera");

            yield return null; // let Start()'s fixed-pose assignment settle before sampling the base position
            Vector3 basePos = rig.transform.localPosition;
            // CR-CODE S-32 iteration 1 minor finding: acceptance requires the nudge to be a
            // position-only offset that never changes the rig's fixed look direction — capture the
            // rotation here too so the pop-observation loop below can assert it stays put structurally
            // (ArenaCameraRig.Update() only ever writes localPosition), not just verify it by omission.
            Quaternion baseRot = rig.transform.localRotation;

            int crystalsBefore = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None).Length;

            // maxHp = AUTO_ATTACK_DAMAGE_BASE so a single tick kills it (isolates the pop/nudge timing from
            // the 2-hit TTK covered by AutoAttackSceneTests).
            EnemyAgent enemy = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f),
                GameConfig.Player.AutoAttackDamageBase);

            yield return WaitUntilOrTimeout(() => enemy.IsPopping, 5f);
            Assert.IsTrue(enemy.IsPopping, "a killing hit must start the S-32 pop sequence (非致死ヒットとは区別)");

            // Score/crystal drop (S-06/S-09) must already be committed the instant the kill is confirmed —
            // not delayed by the pop's visible duration (acceptance: 「撃破確定と同時に即座に実行し、この演出
            // による遅延の影響を受けない」).
            Assert.AreEqual(1, RunStatsTracker.Instance.NormalKillCount,
                "kill must be tallied immediately, not after the pop finishes");
            Assert.GreaterOrEqual(RunStatsTracker.Instance.CurrentScore, GameConfig.Score.PerKillNormal);
            int crystalsRightAfterKill = Object.FindObjectsByType<CrystalPickup>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(crystalsBefore + GameConfig.Crystal.DropPerKillNormal, crystalsRightAfterKill,
                "crystal drop must already exist the instant the kill is confirmed, before the pop finishes");

            // Camera nudge fires once, same instant as the kill (S-23 pattern reused).
            Assert.AreEqual(1, ArenaCameraRig.KillNudgeInvocationCountForTests,
                "exactly one camera nudge must fire for this kill");

            // "消滅前に一瞬スケールアップしてから消える" — sample the scale mid-pop and assert it grew past 1.0
            // toward the configured peak before the enemy is actually destroyed.
            bool sawGrownScale = false;
            float popDeadline = Time.realtimeSinceStartup + GameConfig.Fx.EnemyKillPopDurationS + 1f;
            while (enemy != null && Time.realtimeSinceStartup < popDeadline)
            {
                // CR-CODE S-32 iteration 1 minor finding: while the nudge is actively decaying (non-zero
                // offset most likely mid-pop), pin down that it is a pure position offset — rotation must
                // stay bit-for-bit at the Start()-assigned fixed look direction the whole time, not just
                // once it has fully decayed back (the final Assert.Less(basePos) below only proves position
                // reconverges, not that rotation was never touched in between).
                Assert.Less(Quaternion.Angle(rig.transform.localRotation, baseRot), 1e-3f,
                    "the kill-nudge must never rotate the camera rig — only ArenaCameraRig.Update()'s localPosition write may react to it");
                if (enemy.PopVisualLocalScaleForTests.x > 1.0f + 1e-3f)
                {
                    sawGrownScale = true;
                    break;
                }
                yield return null;
            }
            Assert.IsTrue(sawGrownScale, "enemy visual localScale must grow above 1.0 during the pop before disappearing");

            // Once popped, the enemy is already excluded from live gameplay (ActiveEnemies) — confirmed
            // right when growth was observed above, still true here since BeginKillPop removes it synchronously.
            Assert.IsFalse(EnemyAgent.ActiveEnemies.Contains(enemy),
                "a popping enemy must not keep counting toward MAX_CONCURRENT_ENEMIES/targeting/contact");

            yield return WaitUntilOrTimeout(() => enemy == null, GameConfig.Fx.EnemyKillPopDurationS + 2f);
            Assert.IsTrue(enemy == null, "enemy GameObject must be destroyed once EnemyKillPopDurationS has elapsed");

            // Camera nudge (small, brief) must have fully decayed back to the fixed base position by now
            // (EnemyKillCameraNudgeDurationS < EnemyKillPopDurationS + 2s buffer above).
            Assert.Less(Vector3.Distance(rig.transform.localPosition, basePos), 1e-3f,
                "camera must strictly revert to the fixed base position once the kill-nudge duration has elapsed");
        }

        [UnityTest]
        public IEnumerator NonLethalHit_DoesNotTriggerPopOrCameraNudge()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig);

            // ENEMY_HP_BASE > AUTO_ATTACK_DAMAGE_BASE (2 hits to kill — gdd TTK≈1.2s) — the first tick must
            // only damage it, not kill it (被弾でHPが減っただけの非致死ヒット).
            EnemyAgent enemy = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f),
                GameConfig.Enemy.HpBase);

            yield return WaitUntilOrTimeout(() => enemy.CurrentHp < GameConfig.Enemy.HpBase, 5f);
            Assert.Less(enemy.CurrentHp, GameConfig.Enemy.HpBase, "test setup: the first tick must have landed a non-lethal hit");
            Assert.Greater(enemy.CurrentHp, 0, "test setup: the hit must not have been lethal (ENEMY_HP_BASE needs 2 hits)");

            Assert.IsFalse(enemy.IsPopping, "a non-lethal hit must never start the kill-pop sequence");
            Assert.AreEqual(0, ArenaCameraRig.KillNudgeInvocationCountForTests,
                "a non-lethal hit must never fire the enemy-kill camera nudge");
            Assert.IsTrue(EnemyAgent.ActiveEnemies.Contains(enemy),
                "a merely-damaged (still alive) enemy must remain in ActiveEnemies");

            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator HeavyVariant_LethalHit_AlsoPopsAndFiresCameraNudge_NoNewBranch()
        {
            yield return LoadGameScene();
            DisableWaveSpawner();

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(RunStatsTracker.Instance);
            ArenaCameraRig rig = ArenaCameraRig.Instance;
            Assert.IsNotNull(rig);

            // maxHp = AUTO_ATTACK_DAMAGE_BASE so a single tick kills it (mirrors
            // HeavyEnemySceneTests.HeavyVariant_Kill_AwardsHeavyScoreAndDropsHeavyCrystalCount's setup).
            EnemyAgent heavy = CreateEnemy(
                player.transform.position + new Vector3(GameConfig.Player.AutoAttackRange * 0.5f, 0f, 0f),
                GameConfig.Player.AutoAttackDamageBase, EnemyKind.HeavySwarmer);

            yield return WaitUntilOrTimeout(() => heavy.IsPopping, 5f);
            Assert.IsTrue(heavy.IsPopping,
                "gdd 設計判断「新規AI分岐を増やさない」— the heavy variant must reuse the exact same kill-pop path as the normal variant");
            Assert.AreEqual(1, ArenaCameraRig.KillNudgeInvocationCountForTests);
            Assert.AreEqual(1, RunStatsTracker.Instance.HeavyKillCount);

            yield return WaitUntilOrTimeout(() => heavy == null, GameConfig.Fx.EnemyKillPopDurationS + 2f);
            Assert.IsTrue(heavy == null, "heavy variant's GameObject must also be destroyed once the pop completes");
        }

        [Test]
        public void KillNudgeDuration_IsDifferentFromDashNearMissShakeDuration()
        {
            // acceptance: 「カメラノッジの振幅・継続時間がダッシュニアミスシェイク（S-23）と異なる値であること」—
            // amplitude is already covered by EnemyKillPopSystemTests.KillCameraNudgeMagnitude_...; this
            // locks in the duration half of that same requirement.
            Assert.AreNotEqual(GameConfig.Fx.DashNearMissShakeDuration, GameConfig.Fx.EnemyKillCameraNudgeDurationS);
        }
    }
}
