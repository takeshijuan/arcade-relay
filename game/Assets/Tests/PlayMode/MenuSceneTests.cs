// MenuSceneTests — S-03: Menu シーン（4タブ・必須4要素の枠）.
// Loads the actual wired Menu.unity scene (Editor/SceneWiring.WireMenu) and drives it through
// InputTestFixture (rule 8: batchmode Game View has no focus, so InputAction-level assertions
// require the fixture's keyboard simulation rather than raw InputSystem.QueueStateEvent).
using System.Collections;
using ForgeGame.Components;
using ForgeGame.Systems.Meta;
using ForgeGame.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class MenuSceneTests : InputTestFixture
    {
        [TearDown]
        public void TearDownSession()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
        }

        private static SaveData BuildPopulatedSave()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 4200;
            save.bestSurvivalTimeSec = 128.5f;
            save.bestWaveReached = 7;
            save.totalRunsPlayed = 12;
            save.totalKillCount = 340;
            save.totalCrystalsEarned = 560;
            save.crystalBalance = 75;
            save.upgradeAttackLevel = 2;
            save.upgradeMoveSpeedLevel = 1;
            save.upgradeMaxHpLevel = 3;
            save.bgmVolume = 0.6f;
            save.sfxVolume = 0.4f;
            return save;
        }

        private IEnumerator LoadMenuWithSave(SaveData save)
        {
            SessionHolder.EnsureCreated(save, recovered: false);
            yield return null; // let Awake run so DontDestroyOnLoad takes effect before the scene load below

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Menu, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Discrete press-then-release of a single key, spread across frames (mirrors the pattern
        /// TitleSceneTests already validated for Submit/Cancel — explicit Press/yield/Release/yield
        /// rather than the fixture's single-call PressAndRelease helper).
        /// </summary>
        private IEnumerator PressKey(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TabBar_HasAllFourRequiredLabels()
        {
            yield return LoadMenuWithSave(BuildPopulatedSave());

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(screen, "Menu scene must be wired with a MenuScreen (Editor/SceneWiring.WireMenu)");
            Assert.AreEqual(4, screen.TabLabelTexts.Length);
            Assert.AreEqual(GameConfig.Ui.MenuTabStartLabel, screen.TabLabelTexts[0].text);
            Assert.AreEqual(GameConfig.Ui.MenuTabStatsLabel, screen.TabLabelTexts[1].text);
            Assert.AreEqual(GameConfig.Ui.MenuTabUpgradeLabel, screen.TabLabelTexts[2].text);
            Assert.AreEqual(GameConfig.Ui.MenuTabSettingsLabel, screen.TabLabelTexts[3].text);
        }

        [UnityTest]
        public IEnumerator StartsOnStartTab_WithOnlyItsPanelActive()
        {
            yield return LoadMenuWithSave(BuildPopulatedSave());

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Start].activeSelf);
            Assert.IsFalse(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Stats].activeSelf);
            Assert.IsFalse(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Upgrade].activeSelf);
            Assert.IsFalse(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Settings].activeSelf);
        }

        [UnityTest]
        public IEnumerator SubmitOnStartTab_TransitionsToGameScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());

            Press(keyboard.enterKey);
            yield return null;
            Release(keyboard.enterKey);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Game, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Cancel_TransitionsToTitleScene()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());

            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;
            yield return null;

            Assert.AreEqual(GameConfig.Scenes.Title, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator TabNext_CyclesForwardAndWrapsPastLastTab()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            // Start(0) -> Stats(1) -> Upgrade(2) -> Settings(3) -> wraps back to Start(0)
            for (int step = 0; step < 3; step++)
            {
                yield return PressKey(keyboard.eKey);
            }
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Settings].activeSelf, "3 presses of E from Start should land on Settings");

            yield return PressKey(keyboard.eKey);
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Start].activeSelf, "E must wrap from the last tab back to Start (gdd: 循環切替)");
        }

        [UnityTest]
        public IEnumerator TabPrev_FromStartTab_WrapsBackwardToSettingsTab()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            yield return PressKey(keyboard.qKey);

            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Settings].activeSelf, "Q from Start must wrap to the last tab (Settings)");
        }

        [UnityTest]
        public IEnumerator FocusMove_ClampsAtLastAndFirstItem_WithinUpgradeTab()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            // Navigate to the Upgrade tab (2 E presses from Start).
            yield return PressKey(keyboard.eKey);
            yield return PressKey(keyboard.eKey);
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Upgrade].activeSelf);

            Color highlight = ExpectedColor(GameConfig.Ui.ColorFocusHighlight);
            Assert.AreEqual(highlight, screen.UpgradeItemTexts[0].color, "focus resets to first item on tab entry");

            // S,S,S -> should clamp at last item (index 2), not wrap.
            for (int i = 0; i < 3; i++)
            {
                yield return PressKey(keyboard.sKey);
            }
            Assert.AreEqual(highlight, screen.UpgradeItemTexts[2].color, "S must clamp at the last item (no wrap)");

            // W,W,W,W -> should clamp at first item (index 0), not wrap below.
            for (int i = 0; i < 4; i++)
            {
                yield return PressKey(keyboard.wKey);
            }
            Assert.AreEqual(highlight, screen.UpgradeItemTexts[0].color, "W must clamp at the first item (no wrap)");
        }

        [UnityTest]
        public IEnumerator StatsTab_ShowsAllRequiredSaveDataFields()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            SaveData save = BuildPopulatedSave();
            yield return LoadMenuWithSave(save);
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            yield return PressKey(keyboard.eKey); // Start -> Stats
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Stats].activeSelf);

            string joined = string.Join("\n", System.Array.ConvertAll(screen.StatsTexts, t => t.text));
            StringAssert.Contains(save.highScore.ToString(), joined);
            StringAssert.Contains(save.bestWaveReached.ToString(), joined);
            StringAssert.Contains(save.totalRunsPlayed.ToString(), joined);
            StringAssert.Contains(save.totalKillCount.ToString(), joined);
            StringAssert.Contains(save.totalCrystalsEarned.ToString(), joined);
            StringAssert.Contains(save.crystalBalance.ToString(), joined);
            StringAssert.Contains("128.5", joined); // bestSurvivalTimeSec
            StringAssert.Contains($"{GameConfig.Ui.MenuUpgradeAttackLabel}Lv{save.upgradeAttackLevel}", joined);
            StringAssert.Contains($"{GameConfig.Ui.MenuUpgradeMoveSpeedLabel}Lv{save.upgradeMoveSpeedLevel}", joined);
            StringAssert.Contains($"{GameConfig.Ui.MenuUpgradeMaxHpLabel}Lv{save.upgradeMaxHpLevel}", joined);
        }

        [UnityTest]
        public IEnumerator SettingsTab_ShowsVolumeItemsAndInstructions()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            SaveData save = BuildPopulatedSave();
            yield return LoadMenuWithSave(save);
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            for (int i = 0; i < 3; i++)
            {
                yield return PressKey(keyboard.eKey); // Start -> Stats -> Upgrade -> Settings
            }
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Settings].activeSelf);

            Assert.AreEqual(2, screen.SettingsItemTexts.Length);
            StringAssert.Contains(GameConfig.Ui.MenuSettingsBgmLabel, screen.SettingsItemTexts[0].text);
            StringAssert.Contains("0.6", screen.SettingsItemTexts[0].text);
            StringAssert.Contains(GameConfig.Ui.MenuSettingsSfxLabel, screen.SettingsItemTexts[1].text);
            StringAssert.Contains("0.4", screen.SettingsItemTexts[1].text);

            Assert.IsNotNull(screen.InstructionsText);
            StringAssert.Contains("WASD", screen.InstructionsText.text);
            StringAssert.Contains("Space", screen.InstructionsText.text);
        }

        [UnityTest]
        public IEnumerator MenuCanvas_UsesScreenSpaceCameraRenderMode()
        {
            yield return LoadMenuWithSave(BuildPopulatedSave());

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(screen);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, screen.Canvas.renderMode);
            Assert.IsNotNull(screen.Canvas.worldCamera, "ScreenSpaceCamera Canvas must have worldCamera assigned (rule 14 — QA RenderTexture capture)");
        }

        [UnityTest]
        public IEnumerator MenuCanvas_HasImg05DecorationWithNonNullSprites()
        {
            yield return LoadMenuWithSave(BuildPopulatedSave());

            var screen = Object.FindFirstObjectByType<MenuScreen>();
            Assert.IsNotNull(screen);
            Assert.IsNotNull(screen.PanelImage, "Menu must have an IMG-05 content panel background (Editor/SceneWiring.WireMenuUiFrameKit)");
            Assert.IsNotNull(screen.PanelImage.sprite, "Menu panel Image must have a non-null IMG-05 sprite");
            Assert.IsNotNull(screen.RibbonImage);
            Assert.IsNotNull(screen.RibbonImage.sprite);
            Assert.IsNotNull(screen.CornerImages);
            Assert.AreEqual(2, screen.CornerImages.Length);
            foreach (var corner in screen.CornerImages)
            {
                Assert.IsNotNull(corner);
                Assert.IsNotNull(corner.sprite, "Menu corner ornament must have a non-null IMG-05 sprite");
            }

            Assert.IsNotNull(screen.TabFrameImages, "Menu must have one IMG-05 selection frame per tab");
            Assert.AreEqual(4, screen.TabFrameImages.Length);
            foreach (var frame in screen.TabFrameImages)
            {
                Assert.IsNotNull(frame);
                Assert.IsNotNull(frame.sprite, "Menu tab frame must have a non-null IMG-05 sprite");
            }
        }

        [UnityTest]
        public IEnumerator TabFrame_ActiveTab_UsesDifferentSpriteThanInactiveTabs()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            // Starts on the Start tab (index 0).
            Sprite activeSprite = screen.TabFrameImages[GameConfig.Ui.MenuTabIndex.Start].sprite;
            Sprite inactiveSprite = screen.TabFrameImages[GameConfig.Ui.MenuTabIndex.Stats].sprite;
            Assert.IsNotNull(activeSprite);
            Assert.IsNotNull(inactiveSprite);
            Assert.AreNotEqual(activeSprite, inactiveSprite,
                "the active tab's selection frame must use a visually distinct sprite (IMG-05 selected) from inactive tabs (unselected)");

            // Switch to Stats (E once) and confirm the frame sprites swap accordingly.
            yield return PressKey(keyboard.eKey);
            Assert.AreEqual(inactiveSprite, screen.TabFrameImages[GameConfig.Ui.MenuTabIndex.Start].sprite,
                "Start's frame must switch to the unselected sprite once it's no longer active");
            Assert.AreEqual(activeSprite, screen.TabFrameImages[GameConfig.Ui.MenuTabIndex.Stats].sprite,
                "Stats' frame must switch to the selected sprite once it becomes active");
        }

        [UnityTest]
        public IEnumerator FocusFrame_FocusedItem_UsesDifferentSpriteThanUnfocusedItems_WithinUpgradeTab()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildPopulatedSave());
            var screen = Object.FindFirstObjectByType<MenuScreen>();

            // Navigate to the Upgrade tab (2 E presses from Start).
            yield return PressKey(keyboard.eKey);
            yield return PressKey(keyboard.eKey);
            Assert.IsTrue(screen.TabPanels[GameConfig.Ui.MenuTabIndex.Upgrade].activeSelf);

            Assert.IsNotNull(screen.UpgradeItemFocusFrames);
            Assert.AreEqual(3, screen.UpgradeItemFocusFrames.Length);
            foreach (var frame in screen.UpgradeItemFocusFrames)
            {
                Assert.IsNotNull(frame);
                Assert.IsNotNull(frame.sprite);
            }

            Sprite focusedSprite = screen.UpgradeItemFocusFrames[0].sprite;
            Sprite unfocusedSprite = screen.UpgradeItemFocusFrames[1].sprite;
            Assert.AreNotEqual(focusedSprite, unfocusedSprite,
                "the focused item's selection frame must use a visually distinct sprite from unfocused items");

            // S once -> focus moves to item 1.
            yield return PressKey(keyboard.sKey);
            Assert.AreEqual(unfocusedSprite, screen.UpgradeItemFocusFrames[0].sprite, "item 0's frame must switch to unselected once focus leaves it");
            Assert.AreEqual(focusedSprite, screen.UpgradeItemFocusFrames[1].sprite, "item 1's frame must switch to selected once it gains focus");
        }

        private static Color ExpectedColor(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color color), $"failed to parse '{hex}'");
            return color;
        }
    }
}
