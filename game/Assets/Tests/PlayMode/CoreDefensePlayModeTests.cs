// CoreDefensePlayModeTests.cs — S-04 acceptance:
// 「Game シーンで固定一本道(PATH_LENGTH_M)上を敵(Marauder)が始点からゴールへ delta-time 駆動で等速前進し、
//  ゴール到達で CoreDefenseSystem がコアHPを減算する。コアHP<=0 で敗北イベントが発火する。
//  迎撃なしに全敵をゴール到達させるとコアHP が CORE_HP_MAX から規定量ずつ減り 0 で敗北成立する
//  （NaN座標が無いこと含む）」を検証する。
// WaveSpawnController.enabled=false で MonoBehaviour.Update() の自動駆動を止め、StepForTest（規約9の
// テスト用シーム）だけで決定論的に進める（実フレームの Time.deltaTime との二重進行を避ける）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Systems;

namespace ForgeGame.Tests.PlayMode
{
    public class CoreDefensePlayModeTests
    {
        private GameObject coreGo;
        private GameObject controllerGo;
        private CoreView coreView;
        private WaveSpawnController controller;

        [SetUp]
        public void SetUp()
        {
            coreGo = new GameObject("TestCore");
            coreGo.SetActive(false);
            coreView = coreGo.AddComponent<CoreView>();
            coreGo.SetActive(true); // Awake() でプレースホルダ生成 + CurrentHp=HpMax

            controllerGo = new GameObject("TestWaveSpawnController");
            controllerGo.SetActive(false);
            controller = controllerGo.AddComponent<WaveSpawnController>();
            controller.SetCoreViewForTest(coreView); // 規約9: 非アクティブ生成→注入→アクティブ化
            controllerGo.SetActive(true);
            controller.enabled = false; // Update() の自動駆動を止め、StepForTest のみで進める
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerGo != null) Object.Destroy(controllerGo);
            if (coreGo != null) Object.Destroy(coreGo);
        }

        [UnityTest]
        public IEnumerator UnopposedWaves_DepleteCoreHp_ToExactlyZero_WithNoNaNPositions_AndDefeatEventFires()
        {
            Assert.AreEqual(GameConfig.Core.HpMax, coreView.CurrentHp);

            bool defeatFired = false;
            controller.OnCoreDefeated += () => defeatFired = true;

            const float stepSeconds = 0.5f;
            const float maxSimulatedSeconds = 400f; // 全8ウェーブ分に十分な余裕を持った上限（無限ループ防止）
            float simulated = 0f;

            while (!coreView.IsDefeated && simulated < maxSimulatedSeconds)
            {
                controller.StepForTest(stepSeconds);
                simulated += stepSeconds;

                // NaN座標検査（tech-stack-unity.md QA-PLAY 観点5）: コア + 生存中の敵 View 全て
                AssertNoNaN(coreGo.transform.position, "Core");
                foreach (Transform child in controllerGo.transform)
                {
                    AssertNoNaN(child.position, child.name);
                }

                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds,
                "コアHPが規定時間内に0へ到達しなかった（迎撃なしのはずがコアHPが減っていない可能性）");
            Assert.AreEqual(0, coreView.CurrentHp);
            Assert.IsTrue(coreView.IsDefeated);
            Assert.IsTrue(defeatFired, "コアHP<=0 で敗北イベント(OnCoreDefeated)が発火していない");

            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertNoNaN(Vector3 position, string label)
        {
            Assert.IsFalse(
                float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z),
                $"{label} position contains NaN: {position}");
        }
    }
}
