#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ContainerInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ContainerData data = null!;

        public void Interact(InteractionContext context)
        {
            context.ContainerController.Open(this.data, context.InventoryService);
        }
    }
}
