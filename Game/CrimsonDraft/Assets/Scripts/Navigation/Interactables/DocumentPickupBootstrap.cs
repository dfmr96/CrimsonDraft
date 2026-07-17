#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentPickupBootstrap : IInitializable
    {
        private readonly IPublisher<NoteCollectedEvent> publisher;
        private readonly NoteRegistry                   registry;
        private readonly DocumentInteractable[]         documents;

        [Preserve]
        public DocumentPickupBootstrap(
            IPublisher<NoteCollectedEvent> publisher,
            NoteRegistry                   registry,
            DocumentInteractable[]         documents)
        {
            this.publisher = publisher;
            this.registry  = registry;
            this.documents = documents;
        }

        void IInitializable.Initialize()
        {
            foreach (var doc in this.documents)
                doc.Construct(this.publisher, this.registry);
        }
    }
}
