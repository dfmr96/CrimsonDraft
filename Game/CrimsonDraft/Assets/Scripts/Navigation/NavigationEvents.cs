#nullable enable

using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public readonly struct RoomTransitionStartedEvent
    {
        public readonly RoomController Origin;
        public readonly RoomController Destination;

        public RoomTransitionStartedEvent(RoomController origin, RoomController destination)
        {
            Origin      = origin;
            Destination = destination;
        }
    }

    public readonly struct RoomTransitionedEvent
    {
        public readonly RoomController ActiveRoom;

        public RoomTransitionedEvent(RoomController activeRoom)
        {
            ActiveRoom = activeRoom;
        }
    }

    public readonly struct DialogueActiveChangedEvent
    {
        public readonly bool IsActive;

        public DialogueActiveChangedEvent(bool isActive)
        {
            IsActive = isActive;
        }
    }

    public readonly struct StorageOpenRequestedEvent
    {
    }

    // A world pickup is asking "take it?": the inventory opens and shows where the item would go.
    public readonly struct PickupPreviewStartedEvent
    {
        public readonly ItemData Item;

        public PickupPreviewStartedEvent(ItemData item) => this.Item = item;
    }

    // The question was answered (or abandoned): the inventory it opened closes again.
    public readonly struct PickupPreviewEndedEvent
    {
    }
}
