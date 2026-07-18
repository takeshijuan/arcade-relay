// UpgradePurchaseSceneTests — S-12: アップグレード購入インタラクション + Game への反映.
// Loads the wired Menu.unity scene (Editor/SceneWiring.WireMenu → Components/MenuController) and
// drives the Upgrade tab through InputTestFixture (mirrors MenuSceneTests' PressKey pattern). Purchase
// persistence assertions point Components/MenuController.SaveDirectoryOverrideForTests at
// Application.temporaryCachePath (conventions.md §9: persistentDataPath 直使用禁止・実セーブを汚さない),
// mirroring HealthSceneTests' identical seam on Components/HealthComponent.
using System.Collections;
using System.IO;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class UpgradePurchaseSceneTests : InputTestFixture
    {
        private string _tempSaveDir;

        [SetUp]
        public void SetUpSaveOverride()
        {
            _tempSaveDir = Path.Combine(Application.temporaryCachePath, "s12-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);
            MenuController.SaveDirectoryOverrideForTests = _tempSaveDir;
        }

        [TearDown]
        public void TearDownSession()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
            MenuController.SaveDirectoryOverrideForTests = null;
            if (_tempSaveDir != null && Directory.Exists(_tempSaveDir))
            {
                Directory.Delete(_tempSaveDir, true);
            }
        }

        private static SaveData BuildSave(int crystalBalance, int attackLevel = 0, int moveSpeedLevel = 0, int maxHpLevel = 0)
        {
            var save = SaveData.CreateDefault();
            save.crystalBalance = crystalBalance;
            save.upgradeAttackLevel = attackLevel;
            save.upgradeMoveSpeedLevel = moveSpeedLevel;
            save.upgradeMaxHpLevel = maxHpLevel;
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

        /// <summary>Discrete press-then-release of a single key, spread across frames (mirrors
        /// MenuSceneTests.PressKey).</summary>
        private IEnumerator PressKey(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
            yield return null;
        }

        private IEnumerator NavigateToUpgradeTab(Keyboard keyboard)
        {
            // Start(0) -> Stats(1) -> Upgrade(2): 2 presses of E (gdd Q/E タブ循環).
            yield return PressKey(keyboard.eKey);
            yield return PressKey(keyboard.eKey);
        }

        [UnityTest]
        public IEnumerator Purchase_WithSufficientBalance_DecreasesBalance_IncrementsLevel_AndSavesImmediately()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(crystalBalance: 1000));
            yield return NavigateToUpgradeTab(keyboard);

            // Focus starts at index 0 = UPG-01 攻撃力 on tab entry (gdd: タブ切替時はフォーカスをそのタブの先頭項目にリセット).
            int expectedCost = MetaProgression.UpgradeCost(1);
            yield return PressKey(keyboard.enterKey);

            Assert.IsNotNull(SessionHolder.Instance);
            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(1000 - expectedCost, current.crystalBalance, "purchase must debit exactly upgradeCost(level+1) from crystalBalance");
            Assert.AreEqual(1, current.upgradeAttackLevel, "purchase must increment the purchased upgrade's level by 1");

            // gdd: 「即セーブ」— must have landed on disk in the overridden test directory immediately,
            // not merely held in the in-memory SessionHolder.
            var reader = new FileSaveAdapter(_tempSaveDir);
            SaveLoadOutcome outcome = reader.Load();
            Assert.AreEqual(SaveLoadStatus.Ok, outcome.Status);
            Assert.AreEqual(1000 - expectedCost, outcome.Data.crystalBalance);
            Assert.AreEqual(1, outcome.Data.upgradeAttackLevel);
        }

        [UnityTest]
        public IEnumerator Purchase_WithInsufficientBalance_LeavesBalanceAndLevelUnchanged()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            int insufficientBalance = MetaProgression.UpgradeCost(1) - 1;
            yield return LoadMenuWithSave(BuildSave(crystalBalance: insufficientBalance));
            yield return NavigateToUpgradeTab(keyboard);

            yield return PressKey(keyboard.enterKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(insufficientBalance, current.crystalBalance, "insufficient balance must not be debited");
            Assert.AreEqual(0, current.upgradeAttackLevel, "insufficient balance must not purchase (stay at留まる)");

            // CR-CODE S-12 iteration 1, finding m-4(1): a failed purchase must not touch disk at all —
            // guards against a regression where PersistPurchase runs even when TryPurchase.Purchased
            // is false (unnecessary write + save_version stamp on an unchanged save).
            var reader = new FileSaveAdapter(_tempSaveDir);
            Assert.IsFalse(File.Exists(reader.SavePath), "a not-purchased attempt (insufficient balance) must not write a save file");
        }

        [UnityTest]
        public IEnumerator Purchase_AtMaxLevel_DoesNotPurchase_EvenWithAmpleBalance()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(crystalBalance: 1_000_000, attackLevel: GameConfig.Upgrade.AttackLevelMax));
            yield return NavigateToUpgradeTab(keyboard);

            yield return PressKey(keyboard.enterKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(1_000_000, current.crystalBalance, "at max level, balance must not be debited");
            Assert.AreEqual(GameConfig.Upgrade.AttackLevelMax, current.upgradeAttackLevel, "level must not exceed the configured max");

            // CR-CODE S-12 iteration 1, finding m-4(1): same "no disk write on a not-purchased
            // attempt" guard as the insufficient-balance test above, for the max-level path.
            var reader = new FileSaveAdapter(_tempSaveDir);
            Assert.IsFalse(File.Exists(reader.SavePath), "a not-purchased attempt (max level) must not write a save file");
        }

        [UnityTest]
        public IEnumerator Purchase_MoveSpeedUpgrade_ViaUpgradeTabFocusIndex1_IncrementsMoveSpeedOnly()
        {
            // CR-CODE S-12 iteration 1, finding m-4(2): UPG-01 (index 0) and UPG-03 (index 2) are
            // covered by the tests above/below, but UPG-02 (移動速度, focus index 1) never had a UI-
            // driven purchase test — a regression that mapped index 1 to the wrong UpgradeKind (e.g.
            // duplicating Attack) would have slipped through.
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(crystalBalance: 1000));
            yield return NavigateToUpgradeTab(keyboard);

            // Move focus from UPG-01(攻撃力, index 0) to UPG-02(移動速度, index 1): 1x S (gdd W/S 縦フォーカス).
            yield return PressKey(keyboard.sKey);

            int expectedCost = MetaProgression.UpgradeCost(1);
            yield return PressKey(keyboard.enterKey);

            SaveData current = SessionHolder.Instance.Save;
            Assert.AreEqual(1000 - expectedCost, current.crystalBalance, "purchase must debit exactly upgradeCost(level+1) from crystalBalance");
            Assert.AreEqual(1, current.upgradeMoveSpeedLevel, "UPG-02 (移動速度, focus index 1) must have been purchased");
            Assert.AreEqual(0, current.upgradeAttackLevel, "focus index 1 must not purchase UPG-01 (攻撃力)");
            Assert.AreEqual(0, current.upgradeMaxHpLevel, "focus index 1 must not purchase UPG-03 (最大HP)");
        }

        [UnityTest]
        public IEnumerator PurchaseMaxHpUpgrade_ThenLoadGame_PlayerEffectiveMaxHpReflectsPurchase()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(crystalBalance: 1000));
            yield return NavigateToUpgradeTab(keyboard);

            // Move focus from UPG-01(攻撃力, index 0) to UPG-03(最大HP, index 2): 2x S (gdd W/S 縦フォーカス).
            yield return PressKey(keyboard.sKey);
            yield return PressKey(keyboard.sKey);

            yield return PressKey(keyboard.enterKey);

            SaveData purchased = SessionHolder.Instance.Save;
            Assert.AreEqual(1, purchased.upgradeMaxHpLevel, "UPG-03 (最大HP, focus index 2) must have been purchased");

            yield return SceneManager.LoadSceneAsync(GameConfig.Scenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Game scene must be wired with a PlayerController");
            var health = player.GetComponent<HealthComponent>();
            Assert.IsNotNull(health, "Game scene's Player must carry HealthComponent");

            int expectedEffectiveMaxHp = MetaProgression.EffectiveMaxHp(1);
            Assert.Greater(expectedEffectiveMaxHp, GameConfig.Player.MaxHpBase, "sanity: UPG-03 must actually raise max HP above the Lv0 baseline");
            Assert.AreEqual(expectedEffectiveMaxHp, health.EffectiveMaxHp, "Game init must apply MetaProgression.EffectiveMaxHp from the purchased upgradeMaxHpLevel");
        }

        // S-19 音声統合: SFX-06 (アップグレード購入確定) — Editor/AssetIntegration.PatchMenuPurchaseSfx bakes
        // the clip + an AudioSource onto the same MenuController GameObject this file's other tests already
        // load via LoadMenuWithSave. Mirrors AutoAttackSceneTests' isPlaying assertion (S-17) for the
        // "trigger actually plays a clip" precedent.
        [UnityTest]
        public IEnumerator Purchase_WithSufficientBalance_PlaysUpgradePurchaseSfx()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            yield return LoadMenuWithSave(BuildSave(crystalBalance: 1000));
            yield return NavigateToUpgradeTab(keyboard);

            var controller = Object.FindFirstObjectByType<MenuController>();
            Assert.IsNotNull(controller);
            AudioClip clip = UpgradePurchaseSfxField(controller);
            Assert.IsNotNull(clip, "S-19: Editor/AssetIntegration.PatchMenuPurchaseSfx must have baked SFX-06 onto MenuController");

            AudioSource source = controller.GetComponent<AudioSource>();
            Assert.IsNotNull(source, "S-19: Editor/AssetIntegration.PatchMenuPurchaseSfx must have ensured an AudioSource on MenuController's GameObject");
            Assert.IsFalse(source.isPlaying, "sanity: no SFX must be playing before the purchase happens");

            yield return PressKey(keyboard.enterKey);

            Assert.IsTrue(source.isPlaying, "a successful purchase must trigger SFX-06 (アップグレード購入確定) playback");

            // CR-CODE s-19 iteration 1 minor finding: only SfxLibrary's/BgmPlayer's Sfx/Bgm bus routing
            // had a wiring assertion — MenuController's dedicated purchase-SFX AudioSource
            // (RouteSfxSourceToMixerGroup) had none, so a regression that left Menu's SFX-06 off the Sfx
            // bus (and therefore unaffected by the settings-tab sfxVolume slider) would ship silently.
            Assert.IsNotNull(source.outputAudioMixerGroup, "S-19: MenuController's purchase-SFX AudioSource must be routed to an AudioMixerGroup (RouteSfxSourceToMixerGroup)");
            Assert.AreEqual(GameConfig.Audio.MixerSfxGroupName, source.outputAudioMixerGroup.name,
                "S-19: MenuController's purchase-SFX AudioSource must be routed to the Sfx bus so it reflects the settings-tab sfxVolume slider");
        }

        [UnityTest]
        public IEnumerator Purchase_WithInsufficientBalance_DoesNotPlayUpgradePurchaseSfx()
        {
            InputSystem.AddDevice<Keyboard>();
            Keyboard keyboard = Keyboard.current;

            int insufficientBalance = MetaProgression.UpgradeCost(1) - 1;
            yield return LoadMenuWithSave(BuildSave(crystalBalance: insufficientBalance));
            yield return NavigateToUpgradeTab(keyboard);

            var controller = Object.FindFirstObjectByType<MenuController>();
            AudioSource source = controller.GetComponent<AudioSource>();
            Assert.IsNotNull(source);

            yield return PressKey(keyboard.enterKey);

            Assert.IsFalse(source.isPlaying, "a rejected purchase (insufficient balance) must not play SFX-06");
        }

        /// <summary>Reads MenuController's private `_upgradePurchaseSfx` field via reflection (mirrors
        /// MenuSettingsSceneTests.AudioMixerField's identical pattern for `_mixer`).</summary>
        private static AudioClip UpgradePurchaseSfxField(MenuController controller)
        {
            var field = typeof(MenuController).GetField("_upgradePurchaseSfx",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // CR-CODE s-19 iteration 2 minor finding: a reflection-not-found failure ("_upgradePurchaseSfx"
            // renamed/removed) used to collapse into the same null as "field exists but is unwired", so the
            // Purchase_WithSufficientBalance_PlaysUpgradePurchaseSfx caller's null-check misattributed a
            // rename to "PatchMenuPurchaseSfx must have baked SFX-06" and misdirected debugging. Fail loudly
            // and distinctly here instead.
            Assert.IsNotNull(field,
                "MenuController._upgradePurchaseSfx field not found via reflection — renamed? update this test " +
                "and Editor/AssetIntegration.AssignClip's PatchMenuPurchaseSfx caller.");
            return field.GetValue(controller) as AudioClip;
        }
    }
}
