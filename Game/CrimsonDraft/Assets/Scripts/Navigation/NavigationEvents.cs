#nullable enable

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
}
