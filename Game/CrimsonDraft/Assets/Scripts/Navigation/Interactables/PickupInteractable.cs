#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item        = null!;
        [SerializeField] private string   yarnNodeName = "";

        public void Interact(InteractionContext context)
        {
            if (!context.InventoryService.AddItemAuto(this.item))
            {
                context.DialogueService.StartDialogue(
                    this.yarnNodeName,
                    new Dictionary<string, object>
                    {
                        ["$pickup_result"] = "no_space",
                        ["$item_name"]     = this.item.DisplayName
                    });
                return;
            }

            context.DialogueService.StartDialogue(
                this.yarnNodeName,
                new Dictionary<string, object>
                {
                    ["$pickup_result"] = "success",
                    ["$item_name"]     = this.item.DisplayName
                },
                onComplete: () => gameObject.SetActive(false));
        }
    }
}
