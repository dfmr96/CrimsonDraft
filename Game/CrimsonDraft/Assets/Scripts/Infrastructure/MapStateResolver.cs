#nullable enable

using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Infrastructure
{
    public enum MapRoomVisualState
    {
        Hidden    = 0,
        NotVisited = 1,
        Visited   = 2,
        Completed = 3,
        Current   = 4,
    }

    public static class MapStateResolver
    {
        public static bool IsDeckKnown(
            MapData map,
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps)
        {
            if (knownMaps.IsKnown(map.SceneName))
                return true;

            foreach (var room in map.Rooms)
            {
                if (rooms.GetState(room.RoomId) == RoomMapState.Visited)
                    return true;
            }

            return false;
        }

        public static MapRoomVisualState ResolveRoom(
            bool hasMap,
            RoomMapState roomState,
            bool isCurrentRoom,
            bool isCompleted)
        {
            if (isCurrentRoom)
                return MapRoomVisualState.Current;

            if (roomState == RoomMapState.Visited)
                return isCompleted ? MapRoomVisualState.Completed : MapRoomVisualState.Visited;

            return hasMap ? MapRoomVisualState.NotVisited : MapRoomVisualState.Hidden;
        }
    }
}
