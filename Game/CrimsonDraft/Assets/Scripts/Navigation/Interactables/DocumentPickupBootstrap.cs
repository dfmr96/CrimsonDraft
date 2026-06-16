#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentPickupBootstrap : IInitializable
    {
        private readonly IPublisher<NoteCollectedEvent> publisher;
        private readonly DocumentInteractable[]         documents;

        [Preserve]
        public DocumentPickupBootstrap(IPublisher<NoteCollectedEvent> publisher, DocumentInteractable[] documents)
        {
            this.publisher = publisher;
            this.documents = documents;
        }

        void IInitializable.Initialize()
        {
            foreach (var doc in this.documents)
                doc.Construct(this.publisher);
        }
    }
}
