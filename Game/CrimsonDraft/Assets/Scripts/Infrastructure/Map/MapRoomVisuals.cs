#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    public readonly struct MapRoomVisual
    {
        public MapRoomVisual(string roomId, Sprite sprite, Vector2Int position, int quarterTurns, bool isCurrent)
        {
            this.RoomId       = roomId;
            this.Sprite       = sprite;
            this.Position     = position;
            this.QuarterTurns = quarterTurns;
            this.IsCurrent    = isCurrent;
        }

        public string     RoomId       { get; }
        public Sprite     Sprite       { get; }
        public Vector2Int Position     { get; }
        public int        QuarterTurns { get; }
        public bool       IsCurrent    { get; }
    }

    /// <summary>Which rooms a floor draws and with which sprite. A room shows when visited or
    /// when the floor's map item is owned; it uses the complete sprite once every pickup in it is
    /// collected (no pickups = complete). Pass currentRoomId only for the player's own floor.</summary>
    public static class MapRoomVisuals
    {
        public static IReadOnlyList<MapRoomVisual> Resolve(
            MapData map,
            RoomStateRegistry rooms,
            PickupRegistry pickups,
            KnownMapsRegistry knownMaps,
            string? currentRoomId)
        {
            bool hasMapItem = knownMaps.IsKnown(map.SceneName);
            var result = new List<MapRoomVisual>();

            foreach (var room in map.Rooms)
            {
                bool visited = rooms.GetState(room.RoomId) == RoomMapState.Visited;
                if (!visited && !hasMapItem)
                    continue;

                var sprite = IsComplete(room, pickups) ? room.CompleteSprite : room.IncompleteSprite;
                if (sprite == null)
                    continue;

                result.Add(new MapRoomVisual(room.RoomId, sprite, room.Position, room.QuarterTurns, room.RoomId == currentRoomId));
            }

            return result;
        }

        public static bool IsComplete(MapRoomData room, PickupRegistry pickups)
            => room.PickupIds.All(pickups.IsCollected);

        public static IReadOnlyList<MapRoomVisual> Preview(MapData map, bool complete)
            => map.Rooms
                .Select(room => (room, sprite: complete ? room.CompleteSprite : room.IncompleteSprite))
                .Where(pair => pair.sprite != null)
                .Select(pair => new MapRoomVisual(pair.room.RoomId, pair.sprite!, pair.room.Position, pair.room.QuarterTurns, false))
                .ToList();
    }
}
