#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        private const string PromptNode = "pickup_prompt";

        [SerializeField] private string   pickupId = null!;
        [SerializeField] private ItemData item     = null!;

        // When set, confirming the pickup prompt doesn't add anything to the inventory -- it
        // just marks this note collected and publishes NoteCollectedEvent instead, same as
        // DocumentInteractable. Lets a note use the normal "Pick up X?" prompt/preview instead
        // of the silent instant-collect a DocumentInteractable note gets.
        [SerializeField] private DocumentData? openNoteInstead;

        // Fires once the pickup actually resolves (item added, or note opened via
        // openNoteInstead) -- empty by default, so most pickups pay nothing for it. Used e.g. by
        // BeeperDoorMechanism to react to the room's beeper being collected without
        // PickupInteractable knowing anything about doors.
        [SerializeField] private UnityEvent onPickedUp = new();

        private PickupRegistry                  pickupRegistry = null!;
        private IPublisher<NoteCollectedEvent>? notePublisher;
        private NoteRegistry?                   noteRegistry;

        [Inject]
        public void Construct(PickupRegistry registry, IPublisher<NoteCollectedEvent> notePublisher, NoteRegistry noteRegistry)
        {
            this.pickupRegistry = registry;
            this.notePublisher  = notePublisher;
            this.noteRegistry   = noteRegistry;

            if (registry.IsCollected(this.pickupId))
                gameObject.SetActive(false);
        }

        public string     PickupId   => this.pickupId;
        public UnityEvent OnPickedUp => this.onPickedUp;

        public void Interact(InteractionContext context)
        {
            bool pickupSucceeded = false;
            string itemName = !string.IsNullOrEmpty(this.item.SecondaryName)
                ? this.item.SecondaryName
                : this.item.DisplayName;

            context.PickupPreviewController.Show(this.item);

            context.PickupDialogueService.StartDialogue(
                PromptNode,
                variables: new Dictionary<string, object>
                {
                    ["$item_name"]      = itemName,
                    ["$pickup_success"] = true,
                },
                onComplete: () =>
                {
                    context.PickupPreviewController.Hide();
                    if (!pickupSucceeded) return;

                    this.pickupRegistry.SetCollected(this.pickupId);
                    gameObject.SetActive(false);

                    if (this.openNoteInstead != null)
                    {
                        this.noteRegistry?.SetCollected(this.openNoteInstead.NoteId);
                        this.notePublisher?.Publish(new NoteCollectedEvent { NoteId = this.openNoteInstead.NoteId });
                    }

                    this.onPickedUp.Invoke();
                },
                commands: new Dictionary<string, Action>
                {
                    ["try_pickup"] = () =>
                    {
                        pickupSucceeded = this.openNoteInstead != null
                            || context.InventoryService.AddItemAuto(this.item);
                        context.PickupDialogueService.SetVariable("$pickup_success", pickupSucceeded);
                    }
                });
        }
    }
}
