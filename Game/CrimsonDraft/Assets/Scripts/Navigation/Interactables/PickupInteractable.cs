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
            // operatorSlot: 0 — temporary default until active-operator concept is defined
            if (!context.InventoryService.AddItem(this.item, operatorSlot: 0))
            {
                context.PoiController.Open(
                    new[] { $"No space for: {this.item.DisplayName}." });
                return;
            }

            context.PoiController.Open(
                new[] { $"You picked up: {this.item.DisplayName}." },
                onClose: () => gameObject.SetActive(false));
        }
    }
}
