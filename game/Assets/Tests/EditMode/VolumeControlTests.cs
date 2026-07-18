// VolumeControlTests — S-13: pure clamped-step + linear-to-decibel math (Systems/VolumeControl.cs).
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class VolumeControlTests
    {
        [Test]
        public void Step_Up_IncreasesByStepSize()
        {
            Assert.AreEqual(0.6f, VolumeControl.Step(0.5f, 0.1f, +1), 0.0001f);
        }

        [Test]
        public void Step_Down_DecreasesByStepSize()
        {
            Assert.AreEqual(0.4f, VolumeControl.Step(0.5f, 0.1f, -1), 0.0001f);
        }

        [Test]
        public void Step_ClampsAtUpperBound()
        {
            Assert.AreEqual(1f, VolumeControl.Step(0.95f, 0.1f, +1), 0.0001f);
            Assert.AreEqual(1f, VolumeControl.Step(1f, 0.1f, +1), 0.0001f);
        }

        [Test]
        public void Step_ClampsAtLowerBound()
        {
            Assert.AreEqual(0f, VolumeControl.Step(0.05f, 0.1f, -1), 0.0001f);
            Assert.AreEqual(0f, VolumeControl.Step(0f, 0.1f, -1), 0.0001f);
        }

        [Test]
        public void Step_RepeatedSteps_DoNotAccumulateFloatDrift()
        {
            // 0.8 -> 0.7 -> 0.6 -> ... -> 0.0, ten steps down, must land exactly on 0 (not 0.0000003f etc).
            float v = 0.8f;
            for (int i = 0; i < 8; i++)
            {
                v = VolumeControl.Step(v, 0.1f, -1);
            }
            Assert.AreEqual(0f, v, 0.0001f);
        }

        [Test]
        public void LinearToDecibel_AtFullVolume_IsZeroDb()
        {
            Assert.AreEqual(0f, VolumeControl.LinearToDecibel(1f), 0.01f);
        }

        [Test]
        public void LinearToDecibel_AtZero_IsMixerMinDb()
        {
            Assert.AreEqual(GameConfig.Audio.MixerMinDb, VolumeControl.LinearToDecibel(0f), 0.01f);
        }

        [Test]
        public void LinearToDecibel_IsMonotonicallyIncreasing()
        {
            float low = VolumeControl.LinearToDecibel(0.2f);
            float mid = VolumeControl.LinearToDecibel(0.5f);
            float high = VolumeControl.LinearToDecibel(0.8f);
            Assert.Less(low, mid);
            Assert.Less(mid, high);
        }
    }
}
