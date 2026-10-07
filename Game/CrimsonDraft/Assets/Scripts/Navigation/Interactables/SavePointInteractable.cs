#nullable enable

using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SavePointInteractable : MonoBehaviour, IInteractable
    {
        // Spent once per save written from this point (the Ticker Tape). Unassigned = free saves.
        [SerializeField] private ItemData?         requiredItem;
        [SerializeField] private DialogueReference missingItemDialogue = new();

        public void Interact(InteractionContext context)
        {
            var item = this.requiredItem;
            if (item == null)
            {
                context.SaveController.Open();
                return;
            }

            if (!context.InventoryService.HasItem(item.ItemId))
            {
                context.DialogueService.StartDialogue(this.missingItemDialogue.nodeName ?? "");
                return;
            }

            context.SaveController.Open(onSaved: () => context.InventoryService.TryConsumeOne(item));
        }
    }
}
