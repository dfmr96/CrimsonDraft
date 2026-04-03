#nullable enable

using System.Linq;
using UnityEngine;
using UnityEngine.Events;

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

            var keyItem  = this.data.KeyItem;
            var keyIndex = FindKeyIndex(context, keyItem.ItemId);

            if (keyIndex < 0)
            {
                context.PoiController.Open(new[] { $"You need: {keyItem.DisplayName}." });
                return;
            }

            context.PoiController.Open(
                new[] { $"You used {keyItem.DisplayName} to unlock the door." },
                onClose: () =>
                {
                    context.InventoryService.RemoveItem(keyIndex);
                    this.unlocked = true;
                    this.onOpen.Invoke();
                });
        }

        private static int FindKeyIndex(InteractionContext context, string itemId)
        {
            var items = context.InventoryService.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Data.ItemId == itemId) return i;
            return -1;
        }
    }
}
