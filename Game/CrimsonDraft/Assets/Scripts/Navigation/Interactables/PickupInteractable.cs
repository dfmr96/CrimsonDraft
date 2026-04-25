#nullable enable

using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData          item              = null!;
        [SerializeField] private DialogueReference dialogueReference = new();

        public void Interact(InteractionContext context)
        {
            var nodeName = this.dialogueReference.nodeName ?? "";

            if (!context.InventoryService.AddItemAuto(this.item))
            {
                context.DialogueService.StartDialogue(
                    nodeName,
                    new Dictionary<string, object>
                    {
                        ["$pickup_result"] = "no_space",
                        ["$item_name"]     = this.item.DisplayName
                    });
                return;
            }

            context.DialogueService.StartDialogue(
                nodeName,
                new Dictionary<string, object>
                {
                    ["$pickup_result"] = "success",
                    ["$item_name"]     = this.item.DisplayName
                },
                onComplete: () => gameObject.SetActive(false));
        }
    }
}
