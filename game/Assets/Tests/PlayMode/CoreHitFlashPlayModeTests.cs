// CoreHitFlashPlayModeTests.cs — S-26 acceptance:
// 「CoreView.ApplyDamage 時に GameConfig.Presentation.CoreHitFlashSec を使い、コアのマテリアルカラーを
//  一時的に変化させるヒットフラッシュ、および Transform.localScale を一瞬拡大してから既定値へ戻す
//  スケールパルスを実装する。PlayMode テストで、コア被弾時に CoreView のマテリアルカラーまたは
//  localScale が一時的に変化し、CoreHitFlashSec 経過後に既定値へ戻ることを検証できる」を検証する。
// CoreDefensePlayModeTests.cs と同型のフィクスチャ（AddComponent<CoreView>() で Awake() 経由の
// プレースホルダ生成 + CurrentHp 初期化）を流用し、ApplyDamage を直接呼んで演出だけを単体観測する
// （敵の経路踏破を待たない）。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;

namespace ForgeGame.Tests.PlayMode
{
    public class CoreHitFlashPlayModeTests
    {
        private GameObject coreGo;
        private CoreView coreView;

        [SetUp]
        public void SetUp()
        {
            coreGo = new GameObject("TestCore");
            coreGo.SetActive(false);
            coreView = coreGo.AddComponent<CoreView>();
            coreGo.SetActive(true); // Awake() でプレースホルダ生成 + CurrentHp=HpMax + baseScale/baseColor 記録
        }

        [TearDown]
        public void TearDown()
        {
            if (coreGo != null) Object.Destroy(coreGo);
        }

        [UnityTest]
        public IEnumerator ApplyDamage_TriggersHitFlashAndScalePulse_ThenReturnsToDefault_AfterCoreHitFlashSec()
        {
            Color baseColor = coreView.CurrentTintColor;
            Vector3 baseScale = coreGo.transform.localScale;
            float duration = GameConfig.Presentation.CoreHitFlashSec;

            float applyTime = Time.time;
            coreView.ApplyDamage(EnemyType.Marauder);

            // batch-verify 修正メモ（phase:build Polish バッチ検証区間で発覚。tech-stack-unity.md「既知の
            // 落とし穴」参照）: -batchmode の PlayMode 実フレームは vsync 無しで極めて高速に回り（本環境実測
            // 平均 Time.deltaTime ≈ 0.00013秒/フレーム＝約7500fps）、300フレームでは実時間換算 約0.04秒しか
            // 経過せず CoreHitFlashSec(0.15s) に到底届かない。frame-timing 依存を避けるため十分大きい
            // フレーム予算（この環境の実測値からも duration=0.3s 級まで10倍以上のマージンを確保できる値）へ
            // 引き上げる。
            const int maxWaitFrames = 20000;
            bool motionObserved = false;
            for (int i = 0; i < maxWaitFrames; i++)
            {
                yield return null; // CoreView.Update() が実フレームの Time.deltaTime でフラッシュ/パルスを進める
                if (coreView.CurrentTintColor != baseColor || coreGo.transform.localScale != baseScale)
                {
                    motionObserved = true;
                    break;
                }
            }
            Assert.IsTrue(motionObserved, "被弾直後にマテリアルカラーまたは localScale の一時変化が観測されなかった");
            Assert.Greater(coreGo.transform.localScale.x, baseScale.x, "被弾直後はスケールパルスで既定値より拡大していること");

            // CR-CODE S-26 iter1 対応: duration を一切参照せず1フレームで即復帰する誤実装（例: t<1でも
            // 早々に既定値へ戻す実装）を弾くため、CoreHitFlashSec の半分未満では復帰していないことを検証する。
            // CR-CODE S-26 iter2 対応: 「半分経過『直前』を毎フレームサンプルし続ける」ループは、
            // 最初の観測が75ms超遅れた場合やループ判定通過後のフレームスパイクで、実際は誤実装でなくても
            // 偶然 half-duration 超過後の状態しか観測できず spurious fail し得た（フレーク要因）。
            // ここでは「half-duration 到達前に復帰していたら即失敗」という単調な監視に置き換える
            // （どのフレーム間隔でも成立し、frame-timing に依存しない）。
            float halfDuration = duration * 0.5f;
            for (int i = 0; i < maxWaitFrames && Time.time - applyTime < halfDuration; i++)
            {
                bool stillChanged = coreView.CurrentTintColor != baseColor || coreGo.transform.localScale != baseScale;
                Assert.IsTrue(stillChanged,
                    $"CoreHitFlashSec({duration}s) の半分（{halfDuration}s）が経過する前に既定値へ復帰した（即復帰の疑い）");
                yield return null;
            }

            // CoreHitFlashSec 経過後に既定色・既定スケールへ戻ることを検証する。
            // 以降 ApplyDamage は呼ばない（再トリガーで復帰を観測できなくなることを避ける — S-24 と同方針）。
            bool returnedToDefault = false;
            for (int i = 0; i < maxWaitFrames; i++)
            {
                yield return null;
                if (coreView.CurrentTintColor == baseColor && coreGo.transform.localScale == baseScale)
                {
                    returnedToDefault = true;
                    break;
                }
            }
            float elapsedSinceApply = Time.time - applyTime;
            Assert.IsTrue(returnedToDefault, "CoreHitFlashSec 経過後に既定色・既定スケールへ戻らなかった");
            Assert.GreaterOrEqual(elapsedSinceApply, halfDuration,
                $"既定値への復帰が早すぎる（{elapsedSinceApply}s 経過時点。CoreHitFlashSec の半分 {halfDuration}s 以上は経過しているべき）");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
