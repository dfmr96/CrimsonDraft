#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class RoomDoorInteractable : MonoBehaviour, IInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private DoorData       data                 = null!;
        [SerializeField] private RoomController destination          = null!;
        [SerializeField] private GameObject     doorTransitionPrefab = null!;

        private IRoomOrchestrator roomOrchestrator = null!;
        private bool              unlocked;

        [Inject]
        public void Construct(IRoomOrchestrator roomOrchestrator)
        {
            this.roomOrchestrator = roomOrchestrator;
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.roomOrchestrator
                    .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                    .Forget();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                case KeyUseResult.AlreadyDepleted:
                    context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                    break;

                case KeyUseResult.Success:
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.roomOrchestrator
                                .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                                .Forget();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.InventoryService.RemoveItem(outcome.SlotIndex);
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened_depleted",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.roomOrchestrator
                                .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                                .Forget();
                        });
                    break;
            }
        }
    }
}
