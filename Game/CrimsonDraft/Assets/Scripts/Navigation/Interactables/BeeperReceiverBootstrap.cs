#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class BeeperReceiverBootstrap : IInitializable
    {
        private readonly BeeperSignalRegistry               registry;
        private readonly ISubscriber<BeeperSignalSentEvent> subscriber;
        private readonly BeeperReceiverInteractable[]       receivers;

        [Preserve]
        public BeeperReceiverBootstrap(
            BeeperSignalRegistry               registry,
            ISubscriber<BeeperSignalSentEvent> subscriber,
            BeeperReceiverInteractable[]       receivers)
        {
            this.registry   = registry;
            this.subscriber = subscriber;
            this.receivers  = receivers;
        }

        void IInitializable.Initialize()
        {
            foreach (var receiver in this.receivers)
                receiver.Construct(this.registry, this.subscriber);
        }
    }
}
