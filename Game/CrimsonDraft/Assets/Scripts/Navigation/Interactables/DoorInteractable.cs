#nullable enable

using UnityEngine;
using UnityEngine.Events;
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

            if (this.data.KeyItem == null)
            {
                context.PoiController.Open(new[] { "Locked." });
                return;
            }

            var keyItemData = this.data.KeyItem;
            var outcome     = context.InventoryService.TryUseKey(keyItemData.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                    context.PoiController.Open(new[] { $"You need: {keyItemData.DisplayName}." });
                    break;

                case KeyUseResult.AlreadyDepleted:
                    context.PoiController.Open(new[] { "Locked." });
                    break;

                case KeyUseResult.Success:
                    context.PoiController.Open(
                        new[] { $"Used {keyItemData.DisplayName}." },
                        onClose: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.PoiController.Open(
                        new[] { $"Used {keyItemData.DisplayName}." },
                        onClose: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                            context.InventoryService.RemoveItem(outcome.SlotIndex);
                        });
                    break;
            }
        }
    }
}
