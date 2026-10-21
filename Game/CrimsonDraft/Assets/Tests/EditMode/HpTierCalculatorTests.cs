#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class HpTierCalculatorTests
    {
        private static HpTierConfig MakeConfig(
            float yellowThreshold = 0.75f,
            float orangeThreshold = 0.5f,
            float dangerThreshold = 0.25f)
        {
            var config = ScriptableObject.CreateInstance<HpTierConfig>();
            config.YellowThreshold = yellowThreshold;
            config.OrangeThreshold = orangeThreshold;
            config.DangerThreshold = dangerThreshold;
            return config;
        }

        [Test]
        public void ComputeTier_aboveYellowThreshold_isFull()
        {
            var config = MakeConfig();
            Assert.AreEqual(HpTier.Full, HpTierCalculator.ComputeTier(1f, config));
            Assert.AreEqual(HpTier.Full, HpTierCalculator.ComputeTier(0.76f, config));
            Assert.AreEqual(HpTier.Full, HpTierCalculator.ComputeTier(0.75f, config)); // boundary is inclusive on the Full side
        }

        [Test]
        public void ComputeTier_betweenOrangeAndYellowThresholds_isYellow()
        {
            var config = MakeConfig();
            Assert.AreEqual(HpTier.Yellow, HpTierCalculator.ComputeTier(0.74f, config));
            Assert.AreEqual(HpTier.Yellow, HpTierCalculator.ComputeTier(0.5f, config)); // boundary inclusive on the Yellow side
        }

        [Test]
        public void ComputeTier_betweenDangerAndOrangeThresholds_isOrange()
        {
            var config = MakeConfig();
            Assert.AreEqual(HpTier.Orange, HpTierCalculator.ComputeTier(0.49f, config));
            Assert.AreEqual(HpTier.Orange, HpTierCalculator.ComputeTier(0.25f, config)); // boundary inclusive on the Orange side
        }

        [Test]
        public void ComputeTier_belowDangerThreshold_isDanger()
        {
            var config = MakeConfig();
            Assert.AreEqual(HpTier.Danger, HpTierCalculator.ComputeTier(0.24f, config));
            Assert.AreEqual(HpTier.Danger, HpTierCalculator.ComputeTier(0f, config));
        }

        [Test]
        public void ComputeTier_clampsOutOfRangeRatios()
        {
            var config = MakeConfig();
            Assert.AreEqual(HpTier.Full, HpTierCalculator.ComputeTier(1.5f, config));
            Assert.AreEqual(HpTier.Danger, HpTierCalculator.ComputeTier(-1f, config));
        }

        [Test]
        public void ComputeTier_nullConfig_defaultsToFull()
        {
            Assert.AreEqual(HpTier.Full, HpTierCalculator.ComputeTier(0f, null!));
        }

        [Test]
        public void TierToDanger01_stepsEvenlyFromZeroToOne()
        {
            Assert.AreEqual(0f, HpTierCalculator.TierToDanger01(HpTier.Full), 0.001f);
            Assert.AreEqual(1f / 3f, HpTierCalculator.TierToDanger01(HpTier.Yellow), 0.001f);
            Assert.AreEqual(2f / 3f, HpTierCalculator.TierToDanger01(HpTier.Orange), 0.001f);
            Assert.AreEqual(1f, HpTierCalculator.TierToDanger01(HpTier.Danger), 0.001f);
        }
    }
}
