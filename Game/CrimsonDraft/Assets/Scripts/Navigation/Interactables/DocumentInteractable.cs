#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data = null!;

        public void Interact(InteractionContext context)
        {
            if (string.IsNullOrEmpty(this.data.NoteId)) return;
            context.DialogueService.StartDialogue(this.data.NoteId);
        }
    }
}
