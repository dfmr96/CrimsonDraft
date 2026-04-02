#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService InventoryService;
        public readonly IInputService     InputService;
        public readonly PoiController     PoiController;
        public readonly DocumentController DocumentController;
        public readonly ContainerController ContainerController;

        public InteractionContext(
            IInventoryService   inventoryService,
            IInputService       inputService,
            PoiController       poiController,
            DocumentController  documentController,
            ContainerController containerController)
        {
            InventoryService    = inventoryService;
            InputService        = inputService;
            PoiController       = poiController;
            DocumentController  = documentController;
            ContainerController = containerController;
        }
    }
}
