// PlayMode smoke — verifies the InputTestFixture wiring (rule 8: testables + TestFramework ref)
// resolves and that code-built input actions can be enabled. Coreloop + scene-transition +
// persistence PlayMode tests are added by the prototype stories (QA-PLAY).
using System.Collections;
using ForgeGame.InputLayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ForgeGame.Tests.PlayMode
{
    public sealed class ScaffoldPlayModeSmokeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator GameInput_ActionsBuildAndEnableInPlayMode()
        {
            InputSystem.AddDevice<Keyboard>();
            var input = new GameInput();
            input.EnableGameplay();
            input.EnableUi();

            Assert.IsNotNull(input.Move);
            Assert.IsNotNull(input.Dash);
            Assert.IsNotNull(input.Submit);
            Assert.IsTrue(input.Dash.enabled);

            yield return null;

            input.Dispose();
        }
    }
}
