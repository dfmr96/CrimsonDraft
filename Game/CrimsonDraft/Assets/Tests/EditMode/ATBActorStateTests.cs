#nullable enable

using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class ATBActorStateTests
    {
        private static ATBActorState MakeState(float gaugePerSecond = 1f, float initialGauge = 0f) =>
            new ATBActorState(new ATBActorConfig(0, ATBActorKind.Operator, gaugePerSecond, initialGauge));

        [Test]
        public void Config_negativeGaugePerSecond_clampsToZero()
        {
            var config = new ATBActorConfig(0, ATBActorKind.Operator, gaugePerSecond: -5f);
            Assert.AreEqual(0f, config.GaugePerSecond);
        }

        [Test]
        public void Config_initialGauge_clampedToZeroOne()
        {
            var config = new ATBActorConfig(0, ATBActorKind.Operator, gaugePerSecond: 1f, initialGauge: 5f);
            Assert.AreEqual(1f, config.InitialGauge);
        }

        [Test]
        public void Tick_advancesGaugeByRateTimesDeltaTime()
        {
            var state = MakeState(gaugePerSecond: 0.5f);
            state.Tick(1f);

            Assert.AreEqual(0.5f, state.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_caps_atOne()
        {
            var state = MakeState(gaugePerSecond: 10f);
            state.Tick(1f);

            Assert.AreEqual(1f, state.Gauge, 0.0001f);
            Assert.IsTrue(state.IsReady);
        }

        [Test]
        public void Tick_whenFrozen_doesNotAdvanceGauge()
        {
            var state = MakeState(gaugePerSecond: 1f);
            state.Freeze();
            state.Tick(1f);

            Assert.AreEqual(0f, state.Gauge);
        }

        [Test]
        public void Tick_whenUnfrozen_resumesAdvancing()
        {
            var state = MakeState(gaugePerSecond: 1f);
            state.Freeze();
            state.Tick(1f);
            state.Unfreeze();
            state.Tick(0.5f);

            Assert.AreEqual(0.5f, state.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_whenDead_doesNotAdvanceGauge()
        {
            var state = MakeState(gaugePerSecond: 1f);
            state.MarkDead();
            state.Tick(1f);

            Assert.AreEqual(0f, state.Gauge);
            Assert.IsTrue(state.IsDead);
        }

        [Test]
        public void FillGauge_setsGaugeToOne_andReady()
        {
            var state = MakeState();
            state.FillGauge();

            Assert.AreEqual(1f, state.Gauge);
            Assert.IsTrue(state.IsReady);
        }

        [Test]
        public void Reset_clearsGaugeAndAwaitingCommand()
        {
            var state = MakeState();
            state.FillGauge();
            state.IsAwaitingCommand = true;

            state.Reset();

            Assert.AreEqual(0f, state.Gauge);
            Assert.IsFalse(state.IsAwaitingCommand);
        }

        [Test]
        public void UpdateGaugePerSecond_negativeRate_clampsToZero()
        {
            var state = MakeState(gaugePerSecond: 1f);
            state.UpdateGaugePerSecond(-3f);
            state.Tick(1f);

            Assert.AreEqual(0f, state.Gauge);
        }

        [Test]
        public void UpdateGaugePerSecond_positiveRate_changesTickBehavior()
        {
            var state = MakeState(gaugePerSecond: 1f);
            state.UpdateGaugePerSecond(2f);
            state.Tick(0.25f);

            Assert.AreEqual(0.5f, state.Gauge, 0.0001f);
        }
    }
}
