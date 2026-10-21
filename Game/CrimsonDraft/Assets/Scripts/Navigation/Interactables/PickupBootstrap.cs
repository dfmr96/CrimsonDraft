#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupBootstrap : IInitializable
    {
        private readonly PickupRegistry                  registry;
        private readonly IPublisher<NoteCollectedEvent>  notePublisher;
        private readonly NoteRegistry                    noteRegistry;
        private readonly PickupInteractable[]            pickups;

        [Preserve]
        public PickupBootstrap(
            PickupRegistry                 registry,
            IPublisher<NoteCollectedEvent> notePublisher,
            NoteRegistry                   noteRegistry,
            PickupInteractable[]           pickups)
        {
            this.registry      = registry;
            this.notePublisher = notePublisher;
            this.noteRegistry  = noteRegistry;
            this.pickups       = pickups;
        }

        void IInitializable.Initialize()
        {
            foreach (var pickup in this.pickups)
                pickup.Construct(this.registry, this.notePublisher, this.noteRegistry);
        }
    }
}
