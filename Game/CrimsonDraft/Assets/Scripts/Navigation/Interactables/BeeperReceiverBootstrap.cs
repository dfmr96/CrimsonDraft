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
        private readonly GateRegistry                       gates;

        [Preserve]
        public BeeperReceiverBootstrap(
            BeeperSignalRegistry               registry,
            ISubscriber<BeeperSignalSentEvent> subscriber,
            BeeperReceiverInteractable[]       receivers,
            IInventoryService                  inventory,
            GateRegistry                       gates)
        {
            this.registry   = registry;
            this.subscriber = subscriber;
            this.receivers  = receivers;
            this.inventory  = inventory;
            this.gates      = gates;
        }

        void IInitializable.Initialize()
        {
            foreach (var receiver in this.receivers)
            {
                receiver.Construct(this.registry, this.subscriber, this.inventory);
                // Registered after SaveGameLoader and PickupBootstrap: the loaded gates are in, and a
                // replayed reveal already knows which items were taken.
                receiver.BindGates(this.gates);
            }
        }
    }
}
