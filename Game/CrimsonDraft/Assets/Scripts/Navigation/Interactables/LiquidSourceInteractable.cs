#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    // Generic liquid dispenser (tank + tap). Two-step flow, same panels as GeneratorInteractable:
    //   1) an examine line through the general DialogueService (same panel as POIs/doors);
    //   2) if the player carries the empty flask, a Yes/No "turn the knob?" prompt through
    //      PickupDialogueService. On "yes" the empty flask is swapped for the filled one.
    // A confirmation line plays once the flask is filled. Without an empty flask only the examine line plays. Not consumable: any later empty flask
    // can be filled again.
    public sealed class LiquidSourceInteractable : MonoBehaviour, IInteractable
    {
        private const string FillCommand = "fill_flask";

        [SerializeField] private DialogueReference examineDialogue = new();
        [SerializeField] private DialogueReference promptDialogue  = new();
        [SerializeField] private DialogueReference filledDialogue  = new();
        [SerializeField] private ItemData          emptyFlask      = null!;
        [SerializeField] private ItemData          filledFlask     = null!;

        public void Interact(InteractionContext context)
        {
            context.DialogueService.StartDialogue(
                this.examineDialogue.nodeName ?? "",
                onComplete: () => StartFillPrompt(context));
        }

        private void StartFillPrompt(InteractionContext context)
        {
            if (!context.InventoryService.HasItem(this.emptyFlask.ItemId)) return;

            bool fill = false;

            // Swap happens from onComplete, not from the command, for the same reason as
            // GeneratorInteractable: the dialogue completes in the same tick the command runs
            // and the input context is restored before onComplete.
            context.PickupDialogueService.StartDialogue(
                this.promptDialogue.nodeName ?? "",
                onComplete: () =>
                {
                    if (!fill) return;
                    if (!context.InventoryService.TryRemove(this.emptyFlask.ItemId)) return;

                    if (!context.InventoryService.TryAdd(this.filledFlask))
                    {
                        // Freed slot was taken meanwhile -- give the empty flask back rather than lose it.
                        context.InventoryService.TryAdd(this.emptyFlask);
                        return;
                    }

                    context.DialogueService.StartDialogue(this.filledDialogue.nodeName ?? "");
                },
                commands: new Dictionary<string, Action>
                {
                    [FillCommand] = () => fill = true
                });
        }
    }
}
