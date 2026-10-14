#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    public readonly struct MapDoorVisual
    {
        public MapDoorVisual(string doorId, string roomId, DoorMapState state, Vector2 position, float rotation, Vector2 size)
        {
            this.DoorId   = doorId;
            this.RoomId   = roomId;
            this.State    = state;
            this.Position = position;
            this.Rotation = rotation;
            this.Size     = size;
        }

        public string       DoorId   { get; }
        public string       RoomId   { get; }
        public DoorMapState State    { get; }
        public Vector2      Position { get; }
        public float        Rotation { get; }
        public Vector2      Size     { get; }

        /// <summary>True on an odd number of quarter turns (90°, 270°): the door is drawn with
        /// the vertical sprite instead of rotating the horizontal one.</summary>
        public bool IsVertical => IsVerticalRotation(this.Rotation);

        public static bool IsVerticalRotation(float rotation)
            => Mathf.Abs(Mathf.RoundToInt(rotation / 90f)) % 2 == 1;
    }

    /// <summary>Which doors a floor draws and in which state. A door shows when any room that
    /// owns it (MapRoomData.DoorIds) is shown, so it follows the same discovery rules as rooms;
    /// a door owned by no room is never drawn. Its Position is the bottom-left corner of its
    /// sprite in map pixels: the owning room's Position plus the door's Transform.Offset, so a
    /// door moves with its room.</summary>
    public static class MapDoorVisuals
    {
        public static IReadOnlyList<MapDoorVisual> Resolve(
            MapData map,
            RoomStateRegistry rooms,
            DoorStateRegistry doors,
            KnownMapsRegistry knownMaps)
        {
            bool hasMapItem = knownMaps.IsKnown(map.SceneName);
            return Build(
                map,
                room => hasMapItem || rooms.GetState(room.RoomId) == RoomMapState.Visited,
                doors.GetMapState);
        }

        public static IReadOnlyList<MapDoorVisual> Preview(MapData map)
            => Build(map, _ => true, _ => DoorMapState.Unknown);

        private static IReadOnlyList<MapDoorVisual> Build(
            MapData map,
            Func<MapRoomData, bool> isRoomShown,
            Func<string, DoorMapState> stateOf)
        {
            var ownerByDoorId = new Dictionary<string, MapRoomData>();

            foreach (var room in map.Rooms.Where(isRoomShown))
            foreach (var doorId in room.DoorIds)
                ownerByDoorId.TryAdd(doorId, room);

            return map.Doors
                .Where(door => ownerByDoorId.ContainsKey(door.DoorId))
                .Select(door =>
                {
                    var owner = ownerByDoorId[door.DoorId];
                    return new MapDoorVisual(
                        door.DoorId,
                        owner.RoomId,
                        stateOf(door.DoorId),
                        owner.Position + door.Transform.Offset,
                        door.Transform.Rotation,
                        door.Size);
                })
                .ToList();
        }
    }
}
