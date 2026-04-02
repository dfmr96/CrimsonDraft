#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item = null!;

        public void Interact(InteractionContext context)
        {
            context.InventoryService.AddItem(this.item);
            gameObject.SetActive(false);
        }
    }
}
