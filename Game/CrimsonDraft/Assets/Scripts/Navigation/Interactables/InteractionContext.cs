#nullable enable

using MessagePipe;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
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
        public readonly IPickupDialogueService PickupDialogueService;
        public readonly PuzzleViewController    PuzzleViewController;
        public readonly ScreenFader            ScreenFader;
        public readonly PickupPreviewController PickupPreviewController;
        public readonly SaveController         SaveController;
        public readonly InspectionController   InspectionController;
        public readonly IPublisher<StorageOpenRequestedEvent> StorageOpenPublisher;

        public InteractionContext(
            IInventoryService      inventoryService,
            IInputService          inputService,
            IDialogueService       dialogueService,
            DocumentController     documentController,
            IPickupDialogueService pickupDialogueService,
            PuzzleViewController    puzzleViewController,
            ScreenFader             screenFader,
            PickupPreviewController pickupPreviewController,
            SaveController          saveController,
            InspectionController    inspectionController,
            IPublisher<StorageOpenRequestedEvent> storageOpenPublisher)
        {
            InventoryService      = inventoryService;
            InputService          = inputService;
            DialogueService       = dialogueService;
            DocumentController    = documentController;
            PickupDialogueService = pickupDialogueService;
            PuzzleViewController   = puzzleViewController;
            ScreenFader            = screenFader;
            PickupPreviewController = pickupPreviewController;
            SaveController          = saveController;
            InspectionController    = inspectionController;
            StorageOpenPublisher    = storageOpenPublisher;
        }
    }
}
