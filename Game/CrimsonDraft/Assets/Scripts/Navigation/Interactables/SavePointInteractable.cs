#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SavePointInteractable : MonoBehaviour, IInteractable
    {
        private const string SendCommand = "send_report";

        // Spent once per save written from this point (the Ticker Tape). Unassigned = free saves.
        [SerializeField] private ItemData?         requiredItem;
        [SerializeField] private DialogueReference missingItemDialogue = new();

        // Yes/No asked before the save menu opens when the required item is carried. Its "Yes"
        // option must run <<send_report>>; "No" just ends the dialogue.
        [SerializeField] private DialogueReference promptDialogue = new();

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

            // The command only flags the answer; the menu opens on completion, once the dialogue
            // has handed input back.
            bool send = false;
            context.PickupDialogueService.StartDialogue(
                this.promptDialogue.nodeName ?? "",
                onComplete: () =>
                {
                    if (!send) return;
                    context.SaveController.Open(onSaved: () => context.InventoryService.TryConsumeOne(item));
                },
                commands: new Dictionary<string, Action>
                {
                    [SendCommand] = () => send = true
                });
        }
    }
}
