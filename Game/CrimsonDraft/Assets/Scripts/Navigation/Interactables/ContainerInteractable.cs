#nullable enable

using UnityEngine;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ContainerInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ContainerData data = null!;

        private ContainerController controller = null!;

        [Inject]
        public void Construct(ContainerController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data, context.InventoryService);
        }
    }
}
