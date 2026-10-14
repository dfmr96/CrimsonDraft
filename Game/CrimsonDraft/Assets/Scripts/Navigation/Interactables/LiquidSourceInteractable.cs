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

        // The liquid's own name ("Green Liquid"), kept in the item's secondary name.
        private static string LiquidName(ItemData flask)
            => !string.IsNullOrEmpty(flask.SecondaryName) ? flask.SecondaryName : flask.DisplayName;

        private void StartFillPrompt(InteractionContext context)
        {
            if (!context.InventoryService.HasItem(this.emptyFlask.ItemId)) return;

            bool fill = false;

            // Swap happens from onComplete, not from the command, for the same reason as
            // GeneratorInteractable: the dialogue completes in the same tick the command runs
            // and the input context is restored before onComplete.
            context.PickupDialogueService.StartDialogue(
                this.promptDialogue.nodeName ?? "",
                variables: new Dictionary<string, object> { ["$empty_name"] = this.emptyFlask.DisplayName },
                onComplete: () =>
                {
                    if (!fill) return;
                    // Swapped in place (same cell and rotation); on failure the empty flask stays.
                    if (!context.InventoryService.TryReplace(this.emptyFlask.ItemId, this.filledFlask)) return;

                    context.DialogueService.StartDialogue(
                        this.filledDialogue.nodeName ?? "",
                        new Dictionary<string, object> { ["$liquid_name"] = LiquidName(this.filledFlask) });
                },
                commands: new Dictionary<string, Action>
                {
                    [FillCommand] = () => fill = true
                });
        }
    }
}
