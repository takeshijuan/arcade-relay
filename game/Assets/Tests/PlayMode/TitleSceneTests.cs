// TitleSceneTests — S-02: Title シーン（決定→Menu / Esc→終了 / 破損復旧通知）.
// Loads the actual wired Title.unity scene (Assets/Scenes/Title.unity, wired by
// Editor/SceneWiring.WireTitle) and drives it through InputTestFixture (rule 8: batchmode Game
// View has no focus, so InputAction-level assertions require the fixture's keyboard simulation
// rather than raw InputSystem.QueueStateEvent).
using System.Collections;
using ForgeGame.Components;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class TitleSceneTests : InputTestFixture
    {
        [TearDown]
        public void TearDownSession()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Submit_TransitionsToMenuScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);

            Press(keyboard.enterKey);
            yield return null;
            Release(keyboard.enterKey);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Submit_Space_AlsoTransitionsToMenuScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Menu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Cancel_RequestsQuit_ExactlyOnce_WithoutActuallyQuittingInEditor()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<TitleController>();
            Assert.IsNotNull(controller, "Title scene must be wired with a TitleController (Editor/SceneWiring.WireTitle)");
            Assert.AreEqual(0, controller.QuitRequestedCount);

            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            // Application.isEditor guards the real Application.Quit() call (only fires in
            // desktop builds), so batchmode PlayMode tests never exit the Editor process — the
            // counter is the acceptance-observable proxy for "quit hook fired".
            Assert.AreEqual(1, controller.QuitRequestedCount);
            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Recovered_True_ShowsRecoveryNoticeOnTitle()
        {
            SessionHolder.EnsureCreated(SaveData.CreateDefault(), recovered: true);
            yield return null; // let Awake run so DontDestroyOnLoad takes effect before the scene load below

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(screen, "Title scene must be wired with a TitleScreen (Editor/SceneWiring.WireTitle)");
            Assert.IsTrue(screen.RecoveryNoticeObject.activeSelf, "recovered=true must show the save-recovery notice on Title");
        }

        [UnityTest]
        public IEnumerator Recovered_False_HidesRecoveryNoticeOnTitle()
        {
            SessionHolder.EnsureCreated(SaveData.CreateDefault(), recovered: false);
            yield return null;

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(screen);
            Assert.IsFalse(screen.RecoveryNoticeObject.activeSelf, "recovered=false must not show the save-recovery notice");
        }

        [UnityTest]
        public IEnumerator TitleCanvas_UsesScreenSpaceCameraRenderMode()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(screen);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, screen.Canvas.renderMode);
            Assert.IsNotNull(screen.Canvas.worldCamera, "ScreenSpaceCamera Canvas must have worldCamera assigned (rule 14 — QA RenderTexture capture)");
        }

        [UnityTest]
        public IEnumerator TitleCanvas_HasImg05DecorationWithNonNullSprites()
        {
            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Title, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(screen);
            Assert.IsNotNull(screen.PanelImage, "Title must have an IMG-05 panel background (Editor/SceneWiring.WireTitleUiFrameKit)");
            Assert.IsNotNull(screen.PanelImage.sprite, "Title panel Image must have a non-null IMG-05 sprite");
            Assert.IsNotNull(screen.RibbonImage, "Title must have an IMG-05 heading ribbon");
            Assert.IsNotNull(screen.RibbonImage.sprite);
            Assert.IsNotNull(screen.CornerImages);
            Assert.AreEqual(2, screen.CornerImages.Length);
            foreach (var corner in screen.CornerImages)
            {
                Assert.IsNotNull(corner, "Title corner ornament Image must be built");
                Assert.IsNotNull(corner.sprite, "Title corner ornament must have a non-null IMG-05 sprite");
            }
        }
    }
}
