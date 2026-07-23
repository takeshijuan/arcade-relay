// TitleScreenPlayModeTests.cs — S-02 acceptance: Title→Menu 遷移（クリック/任意キー）・
// ScreenSpaceCamera Canvas・recovered 通知表示を InputTestFixture の入力擬似発行で検証する。
//
// QA-PLAY 再現バグ修正メモ: 以前は共有 [UnitySetUp] コルーチンでシーンロードを行い、各 [UnityTest]
// 側で AddDevice→Press するパターンだったが、batchmode 実行下では [UnitySetUp]（コルーチン）と
// [UnityTest]（別コルーチン）の境界を跨ぐと、テスト側で追加した Mouse/Keyboard デバイスの状態が
// InputActionMap 側の WasPressedThisFrame() に反映されないことが実測で確認された
// （Mouse.current.leftButton.isPressed は true になるのに click.WasPressedThisFrame() は常に false のまま）。
// ResultScreenPlayModeTests（シーンロードを各 [UnityTest] 内にインライン化しているパターン）は
// 同一の InputReader/Update ポーリング機構を使いながら安定して pass するため、シーンロードと
// デバイス追加/入力擬似発行を同一コルーチン内に収める（インライン化する）方式へ統一した。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ForgeGame.Components;
using ForgeGame.Ui;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture 継承必須（batchmode でのフォーカス握り潰し回避 — docs/conventions.md）。
    public class TitleScreenPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            GameFlow.SetRecovered(false);
            yield break;
        }

        private static IEnumerator LoadTitle(bool recovered = false)
        {
            GameFlow.SetRecovered(recovered);
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TitleCanvas_IsScreenSpaceCamera()
        {
            yield return LoadTitle();

            var titleScreen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(titleScreen, "TitleScreen コンポーネントが Title シーンに存在しない");

            var canvas = titleScreen.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Title の Canvas が見つからない");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.IsNotNull(canvas.worldCamera, "worldCamera が未割当");
        }

        [UnityTest]
        public IEnumerator LeftClick_TransitionsToMenu()
        {
            yield return LoadTitle();

            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator AnyKey_TransitionsToMenu()
        {
            yield return LoadTitle();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator RecoveredFlag_ShowsRecoveryNotice()
        {
            yield return LoadTitle(recovered: true);

            var titleScreen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(titleScreen);
            Assert.IsTrue(titleScreen.RecoveryNoticeVisible, "recovered=true なのに復旧通知が表示されていない");
        }

        [UnityTest]
        public IEnumerator NotRecovered_HidesRecoveryNotice()
        {
            yield return LoadTitle();

            var titleScreen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(titleScreen);
            // CR-CODE iter1 #7: RecoveryNoticeVisible は recoveryNoticeText==null でも false を返すため、
            // 「UI 未構築で失敗」と「意図的に非表示」を区別できない。UI 実在を別途アサートする。
            Assert.IsTrue(titleScreen.RecoveryNoticeTextExists, "RecoveryNoticeText が構築されていない（UI 未構築の疑い）");
            Assert.IsFalse(titleScreen.RecoveryNoticeVisible, "recovered=false なのに復旧通知が表示されている");
        }
    }
}
