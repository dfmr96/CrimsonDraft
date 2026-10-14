#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    // Sink counterpart of LiquidSourceInteractable: examine line first, then -- only if the
    // player carries a filled flask -- a Yes/No "pour it out and clean the flask?" prompt. On
    // "yes" the filled flask is swapped back for the empty one and a confirmation line plays.
    public sealed class LiquidSinkInteractable : MonoBehaviour, IInteractable
    {
        private const string PourCommand = "pour_flask";

        [SerializeField] private DialogueReference examineDialogue = new();
        [SerializeField] private DialogueReference promptDialogue  = new();
        [SerializeField] private DialogueReference pouredDialogue  = new();
        [SerializeField] private ItemData          emptyFlask      = null!;
        [SerializeField] private ItemData[]        filledFlasks    = Array.Empty<ItemData>();

        public void Interact(InteractionContext context)
        {
            context.DialogueService.StartDialogue(
                this.examineDialogue.nodeName ?? "",
                onComplete: () => StartPourPrompt(context));
        }

        private ItemData? FindCarriedFilledFlask(InteractionContext context)
        {
            foreach (var flask in this.filledFlasks)
                if (context.InventoryService.HasItem(flask.ItemId)) return flask;
            return null;
        }

        // The liquid's own name ("Green Liquid"), kept in the item's secondary name.
        private static string LiquidName(ItemData flask)
            => !string.IsNullOrEmpty(flask.SecondaryName) ? flask.SecondaryName : flask.DisplayName;

        private void StartPourPrompt(InteractionContext context)
        {
            if (FindCarriedFilledFlask(context) == null) return;

            bool pour = false;
            string liquidName = LiquidName(FindCarriedFilledFlask(context)!);

            // Swap from onComplete, not the command -- same reasoning as LiquidSourceInteractable.
            context.PickupDialogueService.StartDialogue(
                this.promptDialogue.nodeName ?? "",
                variables: new Dictionary<string, object> { ["$liquid_name"] = liquidName },
                onComplete: () =>
                {
                    if (!pour) return;

                    var filled = FindCarriedFilledFlask(context);
                    if (filled == null || !context.InventoryService.TryRemove(filled.ItemId)) return;

                    if (!context.InventoryService.TryAdd(this.emptyFlask))
                    {
                        // Freed slot was taken meanwhile -- give the filled flask back rather than lose it.
                        context.InventoryService.TryAdd(filled);
                        return;
                    }

                    context.DialogueService.StartDialogue(
                        this.pouredDialogue.nodeName ?? "",
                        new Dictionary<string, object> { ["$empty_name"] = this.emptyFlask.DisplayName });
                },
                commands: new Dictionary<string, Action>
                {
                    [PourCommand] = () => pour = true
                });
        }
    }
}
