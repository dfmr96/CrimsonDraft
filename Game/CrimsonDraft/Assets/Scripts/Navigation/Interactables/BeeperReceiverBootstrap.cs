#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class BeeperReceiverBootstrap : IInitializable
    {
        private readonly BeeperSignalRegistry               registry;
        private readonly ISubscriber<BeeperSignalSentEvent> subscriber;
        private readonly BeeperReceiverInteractable[]       receivers;
        private readonly IInventoryService                  inventory;

        [Preserve]
        public BeeperReceiverBootstrap(
            BeeperSignalRegistry               registry,
            ISubscriber<BeeperSignalSentEvent> subscriber,
            BeeperReceiverInteractable[]       receivers,
            IInventoryService                  inventory)
        {
            this.registry   = registry;
            this.subscriber = subscriber;
            this.receivers  = receivers;
            this.inventory  = inventory;
        }

        void IInitializable.Initialize()
        {
            foreach (var receiver in this.receivers)
                receiver.Construct(this.registry, this.subscriber, this.inventory);
        }
    }
}
