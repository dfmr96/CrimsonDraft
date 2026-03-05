using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.UI.HUD;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorEcgMathTests
    {
        [Test]
        public void ClampBpm_clampsToExpectedRange()
        {
            Assert.AreEqual(60, OperatorEcgMath.ClampBpm(40));
            Assert.AreEqual(160, OperatorEcgMath.ClampBpm(200));
        }

        [Test]
        public void ComputeAmplitude_fullHp_returnsExpectedValue()
        {
            var value = OperatorEcgMath.ComputeAmplitude(100f, 1f);
            Assert.AreEqual(42f, value, 0.0001f);
        }

        [Test]
        public void ComputeAmplitude_zeroHp_returnsExpectedValue()
        {
            var value = OperatorEcgMath.ComputeAmplitude(100f, 0f);
            Assert.AreEqual(12f, value, 0.0001f);
        }

        [Test]
        public void ComputeEcgColor_highHp_isGreenDominant()
        {
            var color = OperatorEcgMath.ComputeEcgColor(0.8f);
            Assert.Greater(color.g, color.r);
        }

        [Test]
        public void ComputeEcgColor_lowHp_isRedDominant()
        {
            var color = OperatorEcgMath.ComputeEcgColor(0.2f);
            Assert.Greater(color.r, color.g);
        }

        [Test]
        public void EcgSample_qrsPhase_hasPositiveSpike()
        {
            var sample = OperatorEcgMath.EcgSample(0.16f);
            Assert.Greater(sample, 0.75f);
        }

        [Test]
        public void EcgSample_baselinePhase_isZero()
        {
            var sample = OperatorEcgMath.EcgSample(0.05f);
            Assert.AreEqual(0f, sample, 0.0001f);
        }
    }
}
