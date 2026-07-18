// WavePulseSceneTests — S-15: ウェーブ切替フィードバック（SFX + HUD パルス）.
// Loads the wired Game.unity scene (Editor/SceneWiring.WireGame → GameHud/GameHudController root) and lets
// WaveSpawner progress naturally toward the Wave1->Wave2 boundary (gdd: currentWave = 1 + floor(elapsedSec
// / WAVE_DURATION)), mirroring GameHudSceneTests.OverTime_WaveNumber_OnHud_Increases's fast-forward
// technique. Unlike that test, this one deliberately slows back down to normal Time.timeScale *before* the
// actual wave-1->2 crossing (leaving a safety margin below WAVE_DURATION during the fast-forward leg) so the
// transition frame itself — and therefore Systems/WavePulseSystem's 0.3s pulse it triggers — plays out at
// real speed and can be sampled mid-pulse without a single accelerated frame's Time.deltaTime blowing
// through the whole pulse duration in one step.
using System.Collections;
using ForgeGame.Components;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class WavePulseSceneTests
    {
        // CR-CODE S-15 iteration 1 (flake-source finding): the transition frame that flips
        // WaveSpawner.CurrentWave can, under batchmode's slower/uneven frame cadence, have a
        // Time.deltaTime that exceeds GameConfig.Fx.WavePulseDuration on its own, which would let
        // Systems/WavePulseSystem's 0.3s pulse complete within that single frame — making the
        // mid-pulse sample below ("scale must be >1f shortly after the wave-increase frame")
        // flaky. Capped only from the moment FastForwardToWave2 drops back to normal Time.timeScale
        // (not during the WaitForSecondsRealtime fast-forward leg itself, where a lower cap would
        // instead *reduce* effective speedup by discarding more real time per slow frame) through
        // the immediate post-transition assertion. Restored in TearDownSession so it never leaks
        // into other tests in the same run.
        //
        // CR-CODE S-15 iteration 2 (findings on the iteration-1 fix): a cap of WavePulseDuration*0.5f
        // (0.15s) still let two consecutive capped frames — the transition frame itself (which both
        // starts the pulse *and* runs UpdateWavePulse the same Update()) plus the test's one extra
        // "yield return null" before the L122 assert — sum to exactly WavePulseDuration (0.15+0.15),
        // at which point UpdateWavePulse's `>=` check snaps the scale back to 1.0 before the assert
        // samples it. Tightened to WavePulseDuration/4f (0.075s) below: 2 capped frames sum to 0.15s,
        // leaving 0.15s (half the pulse) of margin before the >= snap-back, and even 3-4 capped
        // frames in a row stay under the 0.3s duration.
        private float _originalMaximumDeltaTime;
        private bool _maximumDeltaTimeOverridden;

        [TearDown]
        public void TearDownSession()
        {
            Time.timeScale = 1f;
            if (_maximumDeltaTimeOverridden)
            {
                Time.maximumDeltaTime = _originalMaximumDeltaTime;
                _maximumDeltaTimeOverridden = false;
            }
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

        /// <summary>Fast-forwards to WAVE_DURATION minus a safety margin (still safely inside wave 1), then
        /// polls frame-by-frame at normal Time.timeScale until wave 2 is reached. The final approach and
        /// the actual crossing both happen at timeScale=1, so the frame that flips currentWave (and
        /// therefore triggers the pulse/SFX) has an ordinary small Time.deltaTime — additionally hard-capped
        /// below WavePulseDuration for the remainder of the test (see field doc above) so no single frame
        /// can consume the whole pulse.</summary>
        private IEnumerator FastForwardToWave2(WaveSpawner spawner)
        {
            // CR-CODE S-15 iteration 2 (finding on fast-forward-leg overshoot): Time.maximumDeltaTime is
            // a built-in Unity cap that is *always* in effect, not just while this test explicitly sets
            // it — during this leg it is still the project default (ProjectSettings/TimeManager.asset:
            // 0.33333334s). At fastTimeScale=20 a single stalled frame can therefore add up to
            // 20*0.3334 =~6.67s of game time to WaveSpawner's elapsed-time accumulator in one Update().
            // The old 3s margin could not absorb that, letting the Wave1->Wave2 transition (and its
            // pulse) happen *inside* this uncapped leg, where that same giant deltaTime would finish the
            // whole 0.3s pulse in one step — surfacing as the unrelated-looking L122 mid-pulse assert
            // failure below instead of a clearly-labeled setup problem. Raised to 10s: comfortably above
            // the ~6.67s worst-case single-frame jump, so wave 1 always still has margin left when the
            // leg ends, and the explicit assert immediately below turns any residual overshoot into a
            // self-explanatory failure at the point it actually happened.
            const float safetyMarginSec = 10f;
            const float fastTimeScale = 20f;

            Time.timeScale = fastTimeScale;
            yield return new WaitForSecondsRealtime((GameConfig.Wave.WaveDuration - safetyMarginSec) / fastTimeScale);
            Time.timeScale = 1f;

            Assert.AreEqual(1, spawner.CurrentWave,
                "fast-forward leg overshot into wave 2 while Time.maximumDeltaTime was still uncapped " +
                "(a single stalled frame at fastTimeScale can advance game time by fastTimeScale * the " +
                "project's default Time.maximumDeltaTime); this is a setup/timing failure distinct from " +
                "the mid-pulse assertions below — widen safetyMarginSec further if it recurs");

            _originalMaximumDeltaTime = Time.maximumDeltaTime;
            Time.maximumDeltaTime = GameConfig.Fx.WavePulseDuration / 4f;
            _maximumDeltaTimeOverridden = true;

            // CR-CODE S-15 iteration 2 fix: this poll now runs at Time.timeScale=1 (no more
            // fast-forwarding), so closing the remaining safetyMarginSec of wave-1 game time costs
            // roughly safetyMarginSec of real wall-clock time (Time.deltaTime tracks real time 1:1
            // once it is below the WavePulseDuration/4f cap, which any normally-paced frame is). The
            // old fixed 6f deadline assumed a 3s margin; it must scale with safetyMarginSec now that
            // the margin was raised to 10s to survive the fast-forward-leg overshoot fix above.
            float deadline = Time.realtimeSinceStartup + safetyMarginSec + 5f;
            while (spawner.CurrentWave < 2 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WaveIncrease_TriggersWaveStartSfxExactlyOnce_AndPulsesHudWaveScale()
        {
            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud, "Game scene must be wired with a GameHud");
            var controller = Object.FindFirstObjectByType<GameHudController>();
            Assert.IsNotNull(controller, "Game scene must be wired with a GameHudController");
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(spawner, "Game scene must be wired with a WaveSpawner");

            // CR-CODE S-15 iteration 1: WaveStartSfxTriggerCountForTests only counts
            // GameHudController's *decision* to call SfxLibrary.Play, not that the clip actually
            // played (Play() itself no-ops silently on a null Instance/clip — see SfxLibrary.Play).
            // Asserting SfxLibrary is wired with a non-null WaveStart clip here makes this test's
            // later "fires exactly once" assertion self-contained proof of an actual SFX play call,
            // instead of relying on AssetIntegrationSceneTests' separate wiring test to rule out the
            // no-op path.
            Assert.IsNotNull(SfxLibrary.Instance, "Game scene must be wired with a SfxLibrary");
            Assert.IsNotNull(SfxLibrary.Instance.WaveStart,
                "SfxLibrary.WaveStart (SFX-05) must be assigned or the trigger-count assertion below would pass on a silent no-op");

            yield return null;
            Assert.AreEqual(1, spawner.CurrentWave, "test setup: must start at wave 1");
            Assert.AreEqual(0, controller.WaveStartSfxTriggerCountForTests, "no pulse/SFX before any wave increase");
            Assert.AreEqual(1f, hud.WaveText.transform.localScale.x, 1e-3f, "wave text must be at rest (1.0x) before any pulse");

            yield return FastForwardToWave2(spawner);
            Assert.AreEqual(2, spawner.CurrentWave, "test setup: wave must have reached 2 within the deadline");

            // One extra frame guarantees GameHudController.Update has observed the just-happened wave
            // increase this frame regardless of Components execution order within the transition frame.
            yield return null;

            Assert.AreEqual(1, controller.WaveStartSfxTriggerCountForTests,
                "the wave-start SFX/pulse must fire exactly once for the Wave1->Wave2 transition (SfxLibrary.WaveStart" +
                " confirmed non-null above, so this trigger count corresponds to an actual Play() call)");
            Assert.Greater(hud.WaveText.transform.localScale.x, 1f,
                "HUD wave number must be scaled up shortly after the wave-increase frame (rising leg of the 1.0->1.3->1.0 pulse)");
            Assert.LessOrEqual(hud.WaveText.transform.localScale.x, GameConfig.Fx.WavePulseScale + 1e-3f,
                "pulse scale must never exceed the configured peak (WavePulseScale)");

            // Wait out the remainder of the pulse (WavePulseDuration) and confirm it fully reverts.
            float settleDeadline = Time.realtimeSinceStartup + GameConfig.Fx.WavePulseDuration + 1f;
            while (Mathf.Abs(hud.WaveText.transform.localScale.x - 1f) > 1e-3f && Time.realtimeSinceStartup < settleDeadline)
            {
                yield return null;
            }
            Assert.AreEqual(1f, hud.WaveText.transform.localScale.x, 1e-3f,
                "HUD wave number scale must fully revert to 1.0x once WavePulseDuration has elapsed");
            Assert.AreEqual(1, controller.WaveStartSfxTriggerCountForTests,
                "the pulse settling back down must not have re-triggered the SFX/pulse a second time");
        }

        [UnityTest]
        public IEnumerator GameHudCanvas_StillScreenSpaceCamera_WithWaveScaleFeature()
        {
            // Smoke check (rule 14) scoped to this story's own test file: WaveText's RectTransform is a
            // child of the same ScreenSpaceCamera Canvas every other HUD element uses — S-15 does not
            // introduce a second Canvas/render mode.
            yield return LoadGameScene();

            var hud = Object.FindFirstObjectByType<GameHud>();
            Assert.IsNotNull(hud);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, hud.Canvas.renderMode);
            Assert.AreSame(hud.Canvas.transform, hud.WaveText.transform.parent,
                "WaveText must remain a direct child of the same ScreenSpaceCamera Canvas (no separate overlay introduced for the pulse)");
        }
    }
}
