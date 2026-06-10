#nullable enable

using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorCache
    {
        public RoomDoorInteractable[]  RoomDoors  { get; }
        public SceneDoorInteractable[] SceneDoors { get; }

        public DoorCache(RoomDoorInteractable[] roomDoors, SceneDoorInteractable[] sceneDoors)
        {
            this.RoomDoors  = roomDoors;
            this.SceneDoors = sceneDoors;
        }
    }
}
