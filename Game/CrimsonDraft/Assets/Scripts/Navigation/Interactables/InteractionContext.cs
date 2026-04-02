#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService InventoryService;
        public readonly IInputService     InputService;

        public InteractionContext(IInventoryService inventoryService, IInputService inputService)
        {
            InventoryService = inventoryService;
            InputService     = inputService;
        }
    }
}
