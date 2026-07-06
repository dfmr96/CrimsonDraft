#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class RoomDoorInteractable : MonoBehaviour, IInteractable, IDoorInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private string        doorId               = null!;
        [SerializeField] private DoorData       data                = null!;
        [SerializeField] private RoomController destination          = null!;
        [SerializeField] private GameObject     doorTransitionPrefab = null!;

        public string         DoorId      => this.doorId;
        public RoomController? Destination => this.destination;

        private IRoomOrchestrator roomOrchestrator = null!;
        private DoorStateRegistry registry         = null!;
        private bool              unlocked;

        [Inject]
        public void Construct(IRoomOrchestrator roomOrchestrator, DoorStateRegistry registry)
        {
            this.roomOrchestrator = roomOrchestrator;
            this.registry         = registry;
            RestoreFromRegistry();
        }

        public void RestoreFromRegistry()
        {
            this.unlocked = this.registry.IsUnlocked(this.doorId);
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.registry.MarkUnlocked(this.doorId);
                this.roomOrchestrator
                    .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                    .Forget();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                this.registry.MarkLocked(this.doorId);
                context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                case KeyUseResult.AlreadyDepleted:
                    this.registry.MarkLocked(this.doorId);
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
                            this.registry.MarkUnlocked(this.doorId);
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
                            this.registry.MarkUnlocked(this.doorId);
                            this.roomOrchestrator
                                .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                                .Forget();
                        });
                    break;
            }
        }
    }
}
