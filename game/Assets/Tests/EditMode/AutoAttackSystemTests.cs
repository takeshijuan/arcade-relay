// AutoAttackSystemTests — S-06: 自動攻撃（最寄り索敵・瞬間ヒット）+ 敵HP・撃破 (gdd 自動攻撃（照準ゼロ）).
// Conventions.md §9: new pure Systems get EditMode coverage. Also includes a structural check that
// InputLayer/GameInput exposes no Attack action (gdd 操作仕様: 「攻撃ボタンの割当自体が存在しない」).
using System.Collections.Generic;
using System.Reflection;
using ForgeGame.InputLayer;
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class AutoAttackSystemTests
    {
        [Test]
        public void FindNearestIndex_PicksClosestCandidateWithinRange()
        {
            var candidates = new List<Vector3>
            {
                new Vector3(5f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
            };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(1, nearest, "index 1 (distance 2) is nearer than indices 0 (5) and 2 (4)");
        }

        [Test]
        public void FindNearestIndex_IgnoresCandidatesOutsideMaxRange()
        {
            var candidates = new List<Vector3>
            {
                new Vector3(1f, 0f, 0f),  // in range
                new Vector3(50f, 0f, 0f), // far out of range
            };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(0, nearest);
        }

        [Test]
        public void FindNearestIndex_AllCandidatesOutOfRange_ReturnsNoTarget()
        {
            var candidates = new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(-20f, 0f, 0f) };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(AutoAttackSystem.NoTarget, nearest);
        }

        [Test]
        public void FindNearestIndex_EmptyCandidateList_ReturnsNoTarget()
        {
            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, new List<Vector3>(), maxRange: 6f);

            Assert.AreEqual(AutoAttackSystem.NoTarget, nearest);
        }

        [Test]
        public void FindNearestIndex_CandidateExactlyAtMaxRange_IsIncluded()
        {
            var candidates = new List<Vector3> { new Vector3(6f, 0f, 0f) };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(0, nearest, "boundary distance == maxRange must count as in range");
        }

        [Test]
        public void FindNearestIndex_TiedDistances_ResolvesToEarliestIndex()
        {
            var candidates = new List<Vector3> { new Vector3(3f, 0f, 0f), new Vector3(-3f, 0f, 0f) };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(0, nearest, "equal-distance ties must resolve deterministically to the first index");
        }

        [Test]
        public void FindNearestIndex_IgnoresYDifference_MatchesArenaXzPlaneConvention()
        {
            // A candidate 100m "above" origin but 1m away on XZ must still be treated as in-range
            // and nearer than one that is close in Y but far on XZ (アリーナは XZ 平面, conventions §3).
            var candidates = new List<Vector3>
            {
                new Vector3(1f, 100f, 0f),
                new Vector3(0f, 0.01f, 10f),
            };

            int nearest = AutoAttackSystem.FindNearestIndex(Vector3.zero, candidates, maxRange: 6f);

            Assert.AreEqual(0, nearest);
        }

        // ComputeAttackAnimSpeedScale — S-17: gdd 決定「アニメ長が AUTO_ATTACK_INTERVAL を超える場合は
        // 再生速度でスケールする」. Editor/AssetIntegration.BuildHeroController is the sole runtime caller
        // (bakes the result into the generated AnimatorController's Attack state at build time).

        [Test]
        public void ComputeAttackAnimSpeedScale_ClipShorterThanInterval_ReturnsOne()
        {
            float speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(clipLengthSeconds: 0.4f, intervalSeconds: 0.6f);

            Assert.AreEqual(1f, speed, "a clip that already fits within the interval must not be sped up");
        }

        [Test]
        public void ComputeAttackAnimSpeedScale_ClipExactlyEqualToInterval_ReturnsOne()
        {
            float speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(clipLengthSeconds: 0.6f, intervalSeconds: 0.6f);

            Assert.AreEqual(1f, speed);
        }

        [Test]
        public void ComputeAttackAnimSpeedScale_ClipLongerThanInterval_ScalesUpToExactlyFit()
        {
            // 1.2s clip vs a 0.6s interval must play at exactly 2x so the clip's total playback duration
            // (clipLength / speed) equals the interval.
            float speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(clipLengthSeconds: 1.2f, intervalSeconds: 0.6f);

            Assert.AreEqual(2f, speed, 0.0001f);
        }

        [Test]
        public void ComputeAttackAnimSpeedScale_NonPositiveInterval_ReturnsOneRatherThanDivideByZero()
        {
            float speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(clipLengthSeconds: 1.5f, intervalSeconds: 0f);

            Assert.AreEqual(1f, speed, "a non-positive interval is a wiring-error guard, not a real gameplay scenario");
        }

        [Test]
        public void ComputeAttackAnimSpeedScale_NonPositiveClipLength_ReturnsOneRatherThanNegativeSpeed()
        {
            float speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(clipLengthSeconds: 0f, intervalSeconds: 0.6f);

            Assert.AreEqual(1f, speed);
        }

        [Test]
        public void GameInput_ExposesNoAttackAction()
        {
            // Structural check for gdd 操作仕様: 「（自動攻撃）入力なし... 攻撃ボタンの割当自体が存在しない」.
            // AutoAttackDriver must never read an input action for attacking — verify none exists at all.
            FieldInfo[] fields = typeof(GameInput).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                Assert.IsFalse(
                    field.Name.ToLowerInvariant().Contains("attack"),
                    $"GameInput must not expose an Attack-related action (found field '{field.Name}')");
            }
        }
    }
}
