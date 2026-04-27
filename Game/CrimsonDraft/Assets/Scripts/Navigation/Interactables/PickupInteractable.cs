#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        private const string NodeName = "pickup_feedback";

        [SerializeField] private ItemData item = null!;

        public void Interact(InteractionContext context)
        {
            if (!context.InventoryService.AddItemAuto(this.item))
            {
                context.DialogueService.StartDialogue(
                    NodeName,
                    new Dictionary<string, object>
                    {
                        ["$pickup_result"] = "no_space",
                        ["$item_name"]     = this.item.DisplayName
                    });
                return;
            }

            context.DialogueService.StartDialogue(
                NodeName,
                new Dictionary<string, object>
                {
                    ["$pickup_result"] = "success",
                    ["$item_name"]     = this.item.DisplayName
                },
                onComplete: () => gameObject.SetActive(false));
        }
    }
}
