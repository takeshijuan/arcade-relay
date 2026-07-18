// ArenaCameraMathTests — S-04: pure trig for the fixed overhead camera pose.
// Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class ArenaCameraMathTests
    {
        [Test]
        public void ComputeFixedPose_SitsAtConfiguredHeightAboveTarget()
        {
            ArenaCameraMath.ComputeFixedPose(
                pitchDeg: 65f, height: 14f, lookTarget: Vector3.zero,
                out Vector3 position, out Quaternion _);

            Assert.AreEqual(14f, position.y, 1e-3f);
        }

        [Test]
        public void ComputeFixedPose_RotationForwardPointsAtLookTarget()
        {
            var lookTarget = Vector3.zero;
            ArenaCameraMath.ComputeFixedPose(
                pitchDeg: 65f, height: 14f, lookTarget,
                out Vector3 position, out Quaternion rotation);

            Vector3 forward = rotation * Vector3.forward;
            Vector3 toTarget = (lookTarget - position).normalized;

            // gdd S-04 acceptance: カメラ forward が中心方向を向いている（Vector3.Dot > 0.2）
            Assert.Greater(Vector3.Dot(forward, toTarget), 0.2f);
            // The derivation should aim exactly at the target (near-1 dot), not merely above threshold.
            Assert.Greater(Vector3.Dot(forward, toTarget), 0.99f);
        }

        [Test]
        public void ComputeFixedPose_PitchZero_LooksHorizontally()
        {
            ArenaCameraMath.ComputeFixedPose(
                pitchDeg: 90f, height: 10f, lookTarget: Vector3.zero,
                out Vector3 position, out Quaternion rotation);

            // At 90° pitch (straight down), the camera sits directly above the target.
            Assert.AreEqual(0f, position.x, 1e-3f);
            Assert.AreEqual(0f, position.z, 1e-3f);
            Assert.AreEqual(10f, position.y, 1e-3f);

            Vector3 forward = rotation * Vector3.forward;
            Assert.AreEqual(-1f, forward.y, 1e-3f);
        }

        [Test]
        public void ComputeFixedPose_ChangingLookTarget_TranslatesPoseAccordingly()
        {
            var lookTarget = new Vector3(2f, 0f, 3f);
            ArenaCameraMath.ComputeFixedPose(
                pitchDeg: 65f, height: 14f, lookTarget,
                out Vector3 position, out Quaternion rotation);

            Vector3 forward = rotation * Vector3.forward;
            Vector3 toTarget = (lookTarget - position).normalized;
            Assert.Greater(Vector3.Dot(forward, toTarget), 0.99f);
        }

        // S-22: 固定俯瞰カメラの南側可視性改善 — gdd「南側可視性の再点検」節の手計算
        // (旧値 H=14/P=65°/F=50° → z≈-6.5m、新値 H=18/P=60°/F=55° → z≈-9.6m) を
        // ArenaCameraMath.ComputeSouthVisibilityLimitZ で機械的に再現・検証する。

        [Test]
        public void ComputeSouthVisibilityLimitZ_OldGdd値_MatchesHandComputedMinus6_5m()
        {
            float southLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                pitchDeg: 65f, height: 14f, fovDeg: 50f);

            Assert.AreEqual(-6.528f, southLimit, 0.01f);
        }

        [Test]
        public void ComputeSouthVisibilityLimitZ_CurrentGameConfig値_MatchesHandComputedMinus9_6m()
        {
            float southLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                GameConfig.Camera.PitchDeg, GameConfig.Camera.Height, GameConfig.Camera.Fov);

            // S-22 acceptance: gdd 更新済み初期値（PitchDeg=60/Height=18/Fov=55、いずれもレンジ境界値）
            // の南側可視限界 z≈-9.6m と一致すること。
            Assert.AreEqual(-9.606f, southLimit, 0.01f);
        }

        [Test]
        public void ComputeSouthVisibilityLimitZ_CurrentGameConfig値_ExtendsFartherSouthThanOldValues()
        {
            float oldLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                pitchDeg: 65f, height: 14f, fovDeg: 50f);
            float newLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                GameConfig.Camera.PitchDeg, GameConfig.Camera.Height, GameConfig.Camera.Fov);

            // S-22 acceptance: 南側可視限界が旧値の z≈-6.5m相当から z≈-9.6m相当まで拡大すること
            // (より負に大きい = より南まで見える)。
            Assert.Less(newLimit, oldLimit,
                "south visibility limit must extend farther south (more negative Z) than the pre-S-22 values");
        }

        [Test]
        public void ComputeSouthVisibilityLimitZ_CurrentGameConfig値_CoversFourDirectionEvidenceRingButOldValuesDoNot()
        {
            // ArenaEnvironmentSceneTests.Capture_FourDirectionReadability_Evidence が南側に置く
            // ring point は SpawnRadius*0.6（現在値 13.5m*0.6=8.1m）相当。S-22 の新値ではこの点が
            // 画角内に収まる（南側可視限界の絶対値 >= ringSouthDistance）が、旧値では収まらない
            // （未充足）ことを検証する。GameConfig.Enemy.SpawnRadius から動的算出し、SpawnRadius が
            // 将来チューニングされてもこの回帰ガードが追随するようにする（CR-CODE S-22 minor 指摘対応）。
            float ringSouthDistance = GameConfig.Enemy.SpawnRadius * 0.6f;

            float newLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                GameConfig.Camera.PitchDeg, GameConfig.Camera.Height, GameConfig.Camera.Fov);
            float oldLimit = ArenaCameraMath.ComputeSouthVisibilityLimitZ(
                pitchDeg: 65f, height: 14f, fovDeg: 50f);

            Assert.GreaterOrEqual(Mathf.Abs(newLimit), ringSouthDistance,
                "S-22 current GameConfig camera values must bring the south evidence ring point inside the visible frustum");
            Assert.Less(Mathf.Abs(oldLimit), ringSouthDistance,
                "sanity check: the pre-S-22 camera values must NOT have covered the south evidence ring point (regression guard for the S-20 finding this story fixes)");
        }
    }
}
