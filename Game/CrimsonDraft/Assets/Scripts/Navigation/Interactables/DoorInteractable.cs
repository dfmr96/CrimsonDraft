#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorData   data   = null!;
        [SerializeField] private UnityEvent onOpen = new();

        private bool unlocked;

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.onOpen.Invoke();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                context.DialogueService.StartDialogue(
                    this.data.DialogueReference.nodeName ?? "",
                    new Dictionary<string, object> { ["$outcome"] = "locked" });
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                    context.DialogueService.StartDialogue(
                        this.data.DialogueReference.nodeName ?? "",
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "needs_key",
                            ["$key_name"] = keyItem.DisplayName
                        });
                    break;

                case KeyUseResult.AlreadyDepleted:
                    context.DialogueService.StartDialogue(
                        this.data.DialogueReference.nodeName ?? "",
                        new Dictionary<string, object> { ["$outcome"] = "locked" });
                    break;

                case KeyUseResult.Success:
                    context.DialogueService.StartDialogue(
                        this.data.DialogueReference.nodeName ?? "",
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.InventoryService.RemoveItem(outcome.SlotIndex);
                    context.DialogueService.StartDialogue(
                        this.data.DialogueReference.nodeName ?? "",
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;
            }
        }
    }
}
