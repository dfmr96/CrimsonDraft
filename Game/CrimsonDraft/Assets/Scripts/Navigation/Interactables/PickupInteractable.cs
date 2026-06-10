#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        private const string PromptNode = "pickup_prompt";

        [SerializeField] private ItemData item = null!;

        public void Interact(InteractionContext context)
        {
            bool pickupSucceeded = false;
            string itemName = !string.IsNullOrEmpty(this.item.SecondaryName)
                ? this.item.SecondaryName
                : this.item.DisplayName;

            context.PickupDialogueService.StartDialogue(
                PromptNode,
                variables: new Dictionary<string, object>
                {
                    ["$item_name"]      = itemName,
                    ["$pickup_success"] = true,
                },
                onComplete: () => { if (pickupSucceeded) gameObject.SetActive(false); },
                commands: new Dictionary<string, Action>
                {
                    ["try_pickup"] = () =>
                    {
                        pickupSucceeded = context.InventoryService.AddItemAuto(this.item);
                        context.PickupDialogueService.SetVariable("$pickup_success", pickupSucceeded);
                    }
                });
        }
    }
}
