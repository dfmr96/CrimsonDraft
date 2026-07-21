#nullable enable

using MessagePipe;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data = null!;

        private IPublisher<NoteCollectedEvent>? publisher;
        private NoteRegistry?                   noteRegistry;

        public void Construct(IPublisher<NoteCollectedEvent> notePublisher, NoteRegistry registry)
        {
            this.publisher    = notePublisher;
            this.noteRegistry = registry;

            if (this.data != null && registry.IsCollected(this.data.NoteId))
                gameObject.SetActive(false);
        }

        public void Interact(InteractionContext context)
        {
            if (this.data == null || string.IsNullOrEmpty(this.data.NoteId)) return;

            this.noteRegistry?.SetCollected(this.data.NoteId);
            this.publisher?.Publish(new NoteCollectedEvent { NoteId = this.data.NoteId });

            gameObject.SetActive(false);
        }
    }
}
