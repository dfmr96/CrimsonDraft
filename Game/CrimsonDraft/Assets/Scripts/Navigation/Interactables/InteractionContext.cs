#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService      InventoryService;
        public readonly IInputService          InputService;
        public readonly IDialogueService       DialogueService;
        public readonly DocumentController     DocumentController;
        public readonly ContainerController    ContainerController;
        public readonly IPickupDialogueService PickupDialogueService;

        public InteractionContext(
            IInventoryService      inventoryService,
            IInputService          inputService,
            IDialogueService       dialogueService,
            DocumentController     documentController,
            ContainerController    containerController,
            IPickupDialogueService pickupDialogueService)
        {
            InventoryService      = inventoryService;
            InputService          = inputService;
            DialogueService       = dialogueService;
            DocumentController    = documentController;
            ContainerController   = containerController;
            PickupDialogueService = pickupDialogueService;
        }
    }
}
