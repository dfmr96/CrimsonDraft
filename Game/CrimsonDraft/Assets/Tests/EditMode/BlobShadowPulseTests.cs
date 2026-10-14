#nullable enable

using System;
using NUnit.Framework;
using CrimsonDraft.Rendering.Shadows;

namespace CrimsonDraft.Tests
{
    public sealed class BlobShadowPulseTests
    {
        private const float Tolerance = 1e-4f;

        private BlobShadowPulse pulse = null!;

        [SetUp]
        public void SetUp()
        {
            this.pulse = new BlobShadowPulse();
        }

        [Test]
        public void WithoutTrigger_multiplierIsOne()
        {
            Assert.AreEqual(1f, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void RightAfterTrigger_multiplierPeaksAtOnePlusAmplitude()
        {
            this.pulse.Trigger(0.2f, 0.5f);

            Assert.AreEqual(1.2f, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void HalfwayThrough_multiplierEasesOutQuadratically()
        {
            this.pulse.Trigger(0.2f, 0.5f);

            this.pulse.Tick(0.25f);

            Assert.AreEqual(1f + 0.2f * 0.25f, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void AfterDuration_multiplierReturnsToOne()
        {
            this.pulse.Trigger(0.2f, 0.5f);

            this.pulse.Tick(0.6f);

            Assert.AreEqual(1f, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void SmallerTriggerMidPulse_keepsCurrentOffsetInsteadOfSnappingDown()
        {
            this.pulse.Trigger(0.2f, 0.5f);
            this.pulse.Tick(0.1f);
            var beforeRetrigger = this.pulse.ScaleMultiplier;

            this.pulse.Trigger(0.05f, 0.5f);

            Assert.AreEqual(beforeRetrigger, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void LargerTriggerMidPulse_restartsFromNewPeak()
        {
            this.pulse.Trigger(0.1f, 0.5f);
            this.pulse.Tick(0.1f);

            this.pulse.Trigger(0.3f, 0.5f);

            Assert.AreEqual(1.3f, this.pulse.ScaleMultiplier, Tolerance);
        }

        [Test]
        public void NonPositiveDuration_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => this.pulse.Trigger(0.2f, 0f));
        }
    }
}
