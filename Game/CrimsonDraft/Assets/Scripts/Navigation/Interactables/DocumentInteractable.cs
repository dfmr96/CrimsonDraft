#nullable enable

using MessagePipe;
using UnityEngine;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data     = null!;
        [SerializeField] private NoteDatabase database = null!;

        private IPublisher<NoteCollectedEvent>? publisher;

        public void Construct(IPublisher<NoteCollectedEvent> notePublisher) => this.publisher = notePublisher;

        public void Interact(InteractionContext context)
        {
            if (this.data == null || string.IsNullOrEmpty(this.data.NoteId)) return;

            this.database.Add(this.data);
            this.publisher?.Publish(new NoteCollectedEvent { NoteId = this.data.NoteId });

            gameObject.SetActive(false);
        }
    }
}
