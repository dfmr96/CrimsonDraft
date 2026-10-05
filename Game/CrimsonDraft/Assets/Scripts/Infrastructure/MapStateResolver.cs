#nullable enable

using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Infrastructure
{
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
    }
}
