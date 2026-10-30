#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Folds a scene bake into a floor's room list. The bake owns content ids
    /// (pickups, doors); the Map Editor owns layout (sprites, position, rotation), which this
    /// never touches. Rooms no longer in the scene are kept and flagged as orphans. Duplicate
    /// RoomIds (left by the old polygon bake) collapse into one entry, preferring the one that
    /// already has layout. Mutates and returns the existing entries.</summary>
    public static class MapLayoutMerge
    {
        public static List<MapRoomData> Upsert(IReadOnlyList<MapRoomData> existing, IReadOnlyList<MapRoomData> baked)
        {
            var bakedById = new Dictionary<string, MapRoomData>();
            foreach (var room in baked)
                bakedById[room.RoomId] = room;

            var result = new List<MapRoomData>(existing.Count);
            foreach (var room in Collapse(existing))
            {
                if (bakedById.TryGetValue(room.RoomId, out var fresh))
                {
                    room.PickupIds = fresh.PickupIds.ToArray();
                    room.DoorIds   = fresh.DoorIds.ToArray();
                    room.IsOrphan  = false;
                }
                else
                {
                    room.IsOrphan = true;
                }
                result.Add(room);
            }

            var known = new HashSet<string>(existing.Select(room => room.RoomId));
            foreach (var room in baked)
            {
                if (!known.Add(room.RoomId))
                    continue;

                result.Add(new MapRoomData
                {
                    RoomId    = room.RoomId,
                    PickupIds = room.PickupIds.ToArray(),
                    DoorIds   = room.DoorIds.ToArray(),
                });
            }

            return result;
        }

        /// <summary>Folds a scene bake into a floor's door list, in bake order. A door already on
        /// the floor keeps the layout set in the Map Editor (transform, size) and takes the
        /// bake's content (its other sides, stairs kind); a new door takes the MapDoorMarker's placement as
        /// its starting layout. Doors no longer in the scene are dropped.</summary>
        public static List<MapDoorData> UpsertDoors(IReadOnlyList<MapDoorData> existing, IReadOnlyList<MapDoorData> baked)
        {
            var existingById = new Dictionary<string, MapDoorData>();
            foreach (var door in existing)
                existingById.TryAdd(door.DoorId, door);

            return baked
                .Select(door =>
                {
                    if (!existingById.TryGetValue(door.DoorId, out var known))
                        return door;
                    known.OtherSides = door.OtherSides.ToArray();
                    known.Stairs     = door.Stairs;
                    return known;
                })
                .ToList();
        }

        private static IEnumerable<MapRoomData> Collapse(IReadOnlyList<MapRoomData> rooms)
            => rooms
                .GroupBy(room => room.RoomId)
                .Select(group =>
                {
                    var keeper = group.FirstOrDefault(HasLayout) ?? group.First();
                    keeper.PickupIds = group.SelectMany(room => room.PickupIds).Distinct().ToArray();
                    keeper.DoorIds   = group.SelectMany(room => room.DoorIds).Distinct().ToArray();
                    return keeper;
                });

        private static bool HasLayout(MapRoomData room)
            => room.IncompleteSprite != null
               || room.CompleteSprite != null
               || room.Position != UnityEngine.Vector2Int.zero
               || room.QuarterTurns != 0;
    }
}
