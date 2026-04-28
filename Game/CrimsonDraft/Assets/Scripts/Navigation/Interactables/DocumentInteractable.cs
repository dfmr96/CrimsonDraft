#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data = null!;

        public void Interact(InteractionContext context)
        {
            context.DocumentController.Open(this.data.Title, this.data.Pages);
        }
    }
}
