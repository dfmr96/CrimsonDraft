#nullable enable

using System;
using MessagePipe;
using NUnit.Framework;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class DeadOperatorGearReleaserTests
    {
        private sealed class FakeSubscriber<T> : ISubscriber<T>
        {
            private IMessageHandler<T>? handler;

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                this.handler = handler;
                return new Subscription(() => this.handler = null);
            }

            public void Publish(T value) => this.handler?.Handle(value);

            private sealed class Subscription : IDisposable
            {
                private readonly Action dispose;
                public Subscription(Action dispose) => this.dispose = dispose;
                public void Dispose() => this.dispose();
            }
        }

        [Test]
        public void Initialize_releasesOnce()
        {
            var inventory = new FakeInventoryService();
            var releaser  = new DeadOperatorGearReleaser(inventory, new FakeSubscriber<CombatEndedEvent>());
            ((IInitializable)releaser).Initialize();
            Assert.AreEqual(1, inventory.ReleaseCalls);
        }

        [Test]
        public void CombatEnded_releasesAgain()
        {
            var inventory  = new FakeInventoryService();
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var releaser   = new DeadOperatorGearReleaser(inventory, subscriber);
            ((IInitializable)releaser).Initialize();

            subscriber.Publish(new CombatEndedEvent { Victory = true });

            Assert.AreEqual(2, inventory.ReleaseCalls);
        }

        [Test]
        public void Dispose_stopsListening()
        {
            var inventory  = new FakeInventoryService();
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var releaser   = new DeadOperatorGearReleaser(inventory, subscriber);
            ((IInitializable)releaser).Initialize();
            ((IDisposable)releaser).Dispose();

            subscriber.Publish(new CombatEndedEvent());

            Assert.AreEqual(1, inventory.ReleaseCalls);
        }
    }
}
