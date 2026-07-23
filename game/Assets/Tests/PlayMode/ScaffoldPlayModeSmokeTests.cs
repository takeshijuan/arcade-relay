// ScaffoldPlayModeSmokeTests.cs — PlayMode の配線検証（InputTestFixture 参照の疎通確認・規約8）。
// batchmode で入力を擬似発行するテストの土台。story（コアループ/永続化/シーン遷移）がここに肉付けする。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;

namespace ForgeGame.Tests.PlayMode
{
    // InputTestFixture を継承すると batchmode でも InputAction が入力擬似発行に反応する（規約8）。
    public class ScaffoldPlayModeSmokeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator PlayMode_Harness_IsWired()
        {
            // 仮想マウスを登録して InputTestFixture の疎通を確認する。
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            Assert.IsNotNull(mouse);
            Assert.IsTrue(Application.isPlaying);
        }
    }
}
