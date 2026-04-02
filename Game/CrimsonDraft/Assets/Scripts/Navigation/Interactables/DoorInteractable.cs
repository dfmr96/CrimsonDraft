#nullable enable

using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorData   data   = null!;
        [SerializeField] private UnityEvent onOpen = new();

        private PoiController poiController = null!;

        [Inject]
        public void Construct(PoiController poiController)
        {
            this.poiController = poiController;
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked)
            {
                this.onOpen.Invoke();
                return;
            }

            if (this.data.KeyItem == null)
            {
                this.poiController.Open(new[] { "Bloqueada." });
                return;
            }

            var keyItem = this.data.KeyItem;
            bool hasKey = context.InventoryService.Items
                .Any(item => item.Data.ItemId == keyItem.ItemId);

            if (!hasKey)
            {
                this.poiController.Open(new[] { $"Necesitas: {keyItem.DisplayName}." });
                return;
            }

            var itemIndex = context.InventoryService.Items
                .Select((item, i) => (item, i))
                .First(t => t.item.Data.ItemId == keyItem.ItemId).i;

            context.InventoryService.RemoveItem(itemIndex);
            this.onOpen.Invoke();
        }
    }
}
