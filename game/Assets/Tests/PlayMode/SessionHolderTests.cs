// SessionHolderTests — S-01: verifies the DontDestroyOnLoad session holder that Boot populates
// from FileSaveAdapter.Load() (docs/architecture.md §2). Singleton semantics + DontDestroyOnLoad
// placement are what let Title/Menu/Game/Result read the loaded SaveData and recovered flag.
using System.Collections;
using System.Text.RegularExpressions;
using ForgeGame.Components;
using ForgeGame.Systems.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class SessionHolderTests
    {
        [TearDown]
        public void TearDown()
        {
            if (SessionHolder.Instance != null)
            {
                Object.DestroyImmediate(SessionHolder.Instance.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EnsureCreated_HoldsSaveDataAndIsDontDestroyOnLoadSingleton()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 777;
            SessionHolder holder = SessionHolder.EnsureCreated(save, recovered: true);
            yield return null;

            Assert.AreEqual(777, holder.Save.highScore);
            Assert.IsTrue(holder.Recovered);
            Assert.AreEqual("DontDestroyOnLoad", holder.gameObject.scene.name);

            // A second EnsureCreated call (e.g. hypothetical re-entry) must update the SAME
            // instance rather than spawning a duplicate holder.
            var save2 = SaveData.CreateDefault();
            SessionHolder holder2 = SessionHolder.EnsureCreated(save2, recovered: false);

            Assert.AreSame(holder, holder2);
            Assert.IsFalse(holder2.Recovered);
        }

        [UnityTest]
        public IEnumerator EnsureCreated_NullSaveOnExistingInstance_KeepsExistingValueInstead()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 555;
            SessionHolder holder = SessionHolder.EnsureCreated(save, recovered: false);
            yield return null;

            // A caller bug that passes null must NOT clobber a valid in-memory value with
            // CreateDefault() (that would silently erase progress once FileSaveAdapter.Save()
            // persists it next). It must keep the existing value, matching UpdateSave's policy.
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Wiring\] SessionHolder\.EnsureCreated called with null SaveData; keeping existing"));
            SessionHolder holder2 = SessionHolder.EnsureCreated(null, recovered: false);

            Assert.AreSame(holder, holder2);
            Assert.AreEqual(555, holder2.Save.highScore);
            Assert.IsFalse(holder2.Recovered);
        }

        [UnityTest]
        public IEnumerator EnsureCreated_NullSaveWithNoExistingInstance_FallsBackToDefaultsAndForcesRecovered()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Wiring\] SessionHolder\.EnsureCreated called with null SaveData; no existing value"));
            SessionHolder holder = SessionHolder.EnsureCreated(null, recovered: false);
            yield return null;

            Assert.IsNotNull(holder.Save);
            // recovered must be forced true so the Title/Menu recovery notice UI fires even
            // though the caller passed recovered: false.
            Assert.IsTrue(holder.Recovered);
        }

        [UnityTest]
        public IEnumerator Awake_DuplicateSessionHolder_DestroysTheDuplicateAndKeepsTheOriginal()
        {
            var save = SaveData.CreateDefault();
            save.highScore = 42;
            SessionHolder original = SessionHolder.EnsureCreated(save, recovered: false);
            yield return null;

            // A second holder appearing at runtime (e.g. a scene wired with its own SessionHolder on
            // top of Boot's DDOL one) must be destroyed by Awake's duplicate guard, leaving Instance —
            // and its in-memory SaveData — untouched.
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Wiring\] duplicate SessionHolder destroyed"));
            var duplicateGo = new GameObject("SessionHolderDuplicate");
            duplicateGo.AddComponent<SessionHolder>();
            yield return null; // Destroy() only completes at end of frame

            Assert.AreSame(original, SessionHolder.Instance, "Instance must keep pointing at the original holder");
            Assert.AreEqual(42, SessionHolder.Instance.Save.highScore,
                "the original's SaveData must survive the duplicate's Awake untouched");
            Assert.IsTrue(duplicateGo == null,
                "the duplicate GameObject must be destroyed (Unity fake-null) by the frame after its Awake");
        }

        [UnityTest]
        public IEnumerator Awake_NonRootGameObject_ReparentsToRootAndBecomesDontDestroyOnLoadSingleton()
        {
            var parent = new GameObject("SessionHolderTestParent");
            var child = new GameObject(nameof(SessionHolder));
            child.transform.SetParent(parent.transform);

            LogAssert.Expect(LogType.Error, "[Wiring] SessionHolder must be a root GameObject");
            var holder = child.AddComponent<SessionHolder>();
            yield return null;

            Assert.AreSame(holder, SessionHolder.Instance);
            Assert.IsNull(holder.transform.parent);
            Assert.AreEqual("DontDestroyOnLoad", holder.gameObject.scene.name);

            Object.DestroyImmediate(parent);
        }
    }
}
