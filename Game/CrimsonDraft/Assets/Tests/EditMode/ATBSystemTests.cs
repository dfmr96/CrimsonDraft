using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class ATBSystemTests
    {
        private static ATBActorConfig Op(int slot, float gps) =>
            new ATBActorConfig(slot, ATBActorKind.Operator, gps);

        private static ATBActorConfig En(int slot, float gps) =>
            new ATBActorConfig(slot, ATBActorKind.Enemy, gps);

        [Test]
        public void Tick_advancesGaugeOfLiveActors()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 0.5f) });
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0.5f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_clampsGaugeAtOne()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(2f, paused: false);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_whenPaused_doesNotAdvanceGauge()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(0.5f, paused: true);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void IsReady_trueWhenGaugeReachesOne()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(1f, paused: false);
            Assert.IsTrue(sys.GetActor(0, ATBActorKind.Operator)!.IsReady);
        }

        [Test]
        public void ResetActor_setsGaugeToZero()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(1f, paused: false);
            sys.ResetActor(0, ATBActorKind.Operator);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void MarkDead_preventsGaugeAdvance()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { En(0, 1f) });
            sys.MarkDead(0, ATBActorKind.Enemy);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }

        [Test]
        public void GetActor_returnsNullForUnknownSlot()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            Assert.IsNull(sys.GetActor(99, ATBActorKind.Operator));
        }

        [Test]
        public void UpdateActorGaugeRate_changesTickBehavior()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { En(0, 0.5f) });
            sys.UpdateActorGaugeRate(0, ATBActorKind.Enemy, 1f);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_doesNotAdvanceDeadActors()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f), En(0, 1f) });
            sys.MarkDead(0, ATBActorKind.Operator);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }

        [Test]
        public void ResetActor_clearsIsAwaitingCommand()
        {
            var sys   = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            ATBActorState actor = sys.GetActor(0, ATBActorKind.Operator)!;
            actor.IsAwaitingCommand = true;
            sys.ResetActor(0, ATBActorKind.Operator);
            Assert.IsFalse(actor.IsAwaitingCommand);
        }

        [Test]
        public void FillGauge_setsGaugeToOne()
        {
            var state = new ATBActorState(new ATBActorConfig(0, ATBActorKind.Operator, 0.1f));
            state.FillGauge();
            Assert.AreEqual(1f, state.Gauge, 0.0001f);
        }

        [Test]
        public void FillGauge_makesActorReady()
        {
            var state = new ATBActorState(new ATBActorConfig(0, ATBActorKind.Operator, 0.1f));
            state.FillGauge();
            Assert.IsTrue(state.IsReady);
        }

        [Test]
        public void FillOperatorGauges_setsAllOperatorsToReady()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[]
            {
                new ATBActorConfig(0, ATBActorKind.Operator, 0.1f),
                new ATBActorConfig(1, ATBActorKind.Operator, 0.2f),
            });
            sys.FillOperatorGauges();
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
            Assert.AreEqual(1f, sys.GetActor(1, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void FillOperatorGauges_doesNotAffectEnemies()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[]
            {
                new ATBActorConfig(0, ATBActorKind.Operator, 0.1f),
                new ATBActorConfig(0, ATBActorKind.Enemy,    0.1f),
            });
            sys.FillOperatorGauges();
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge,    0.0001f);
        }
    }
}
