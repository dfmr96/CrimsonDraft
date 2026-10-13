#nullable enable

using NUnit.Framework;
using CrimsonDraft.UI;

namespace CrimsonDraft.Tests
{
    public sealed class AxisStepperTests
    {
        [Test]
        public void Up_returnsMinusOne_down_returnsPlusOne()
        {
            Assert.AreEqual(-1, new AxisStepper().Update(0.9f));
            Assert.AreEqual(+1, new AxisStepper().Update(-0.9f));
        }

        [Test]
        public void Holding_doesNotRepeat()
        {
            var stepper = new AxisStepper();
            stepper.Update(-0.9f);
            Assert.AreEqual(0, stepper.Update(-0.9f));
        }

        [Test]
        public void BelowPress_doesNothing()
        {
            Assert.AreEqual(0, new AxisStepper().Update(0.4f));
        }

        [Test]
        public void Rearms_onlyBelowReleaseThreshold()
        {
            var stepper = new AxisStepper();
            stepper.Update(-0.9f);
            stepper.Update(-0.3f);
            Assert.AreEqual(0, stepper.Update(-0.9f));
            stepper.Update(-0.1f);
            Assert.AreEqual(+1, stepper.Update(-0.9f));
        }

        [Test]
        public void Reset_requiresReleaseBeforeNextStep()
        {
            var stepper = new AxisStepper();
            stepper.Reset();
            Assert.AreEqual(0, stepper.Update(0.9f));
            stepper.Update(0f);
            Assert.AreEqual(-1, stepper.Update(0.9f));
        }
    }
}
