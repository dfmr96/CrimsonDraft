#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
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
            // First visit to the telegraph room: explain it, then carry on into the normal flow.
            var station = GetComponent<TelegraphSaveStation>();
            if (station != null && !station.IntroSeen && !string.IsNullOrEmpty(station.IntroNodeName))
            {
                context.DialogueService.StartDialogue(
                    station.IntroNodeName,
                    onComplete: () =>
                    {
                        station.MarkIntroSeen();

                        // Still inside the runner's completion callback: a follow-up on the same
                        // runner (the missing-item message) is dropped if started from here, so
                        // continue once the input update has finished.
                        DeferredInputAction.Run(() => Proceed(context, station));
                    });
                return;
            }

            Proceed(context, station);
        }

        private void Proceed(InteractionContext context, TelegraphSaveStation? station)
        {
            var item = this.requiredItem;
            if (item == null)
            {
                OpenSave(context, station, onSaved: null);
                return;
            }

            if (!context.InventoryService.HasItem(item.ItemId))
            {
                // The pickup runner is for Yes/No prompts: it shows a plain line and ends at once,
                // without waiting for input. DialogueService waits for the player to read it.
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
                    OpenSave(context, station, onSaved: () => context.InventoryService.TryConsumeOne(item));
                },
                commands: new Dictionary<string, Action>
                {
                    [SendCommand] = () => send = true
                });
        }

        private static void OpenSave(InteractionContext context, TelegraphSaveStation? station, Action? onSaved)
        {
            if (station == null)
            {
                context.SaveController.Open(onSaved);
                return;
            }

            // The panel lives in the room, so the camera cut is ours; Cancel is locked on the
            // inspection because the save panel owns it (back out of Yes/No, then the list).
            var inspection = context.InspectionController;
            inspection.Enter(station.Camera);
            inspection.ExitLocked = true;
            context.SaveController.OpenTelegraph(
                station.View,
                onSaved,
                onClosed: () => DeferredInputAction.Run(inspection.ExitNow));
        }
    }
}
