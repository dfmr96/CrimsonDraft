#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SceneDoorInteractable : MonoBehaviour, IInteractable, IDoorInteractable, IAnimatedInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private string     doorId               = null!;
        [SerializeField] private DoorData   data                 = null!;
        [SerializeField] private string     targetSceneName      = null!;
        [SerializeField] private string     targetEntryPointId   = null!;
        [SerializeField] private GameObject doorTransitionPrefab = null!;

        public string DoorId => this.doorId;

        // Doors have their own opening/transition animation — the player shouldn't also play
        // a generic Interact animation.
        public InteractionAnimType AnimType => InteractionAnimType.None;

        private IFloorTransitionService floorService = null!;
        private DoorStateRegistry       registry     = null!;
        private bool                    unlocked;

        [Inject]
        public void Construct(IFloorTransitionService floorService, DoorStateRegistry registry)
        {
            this.floorService = floorService;
            this.registry     = registry;
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
                Transition();
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
                            Transition();
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
                            Transition();
                        });
                    break;
            }
        }

        private void Transition()
        {
            this.floorService
                .TransitionToFloorAsync(
                    gameObject.scene.name,
                    this.targetSceneName,
                    this.targetEntryPointId,
                    this.doorTransitionPrefab)
                .Forget();
        }
    }
}
