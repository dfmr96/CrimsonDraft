#nullable enable

using UnityEngine;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data = null!;

        private DocumentController controller = null!;

        [Inject]
        public void Construct(DocumentController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data.Title, this.data.Pages);
        }
    }
}
