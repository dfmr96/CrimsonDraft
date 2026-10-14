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
    /// owns it (MapRoomData.DoorIds) is shown, so it follows the same discovery rules as rooms,
    /// or when the room of one of its other sides (the excluded far half of a doubled door) is
    /// shown; a door owned by no room is never drawn. Its state is the most advanced one among
    /// its halves (Unlocked over Locked over Unknown). Its Position is the bottom-left corner of
    /// its sprite in map pixels: the owning room's Position plus the door's Transform.Offset, so
    /// a door moves with its room.</summary>
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

        /// <summary>Every door owned by a room, ignoring discovery, all shown in one state (for
        /// the Map Editor).</summary>
        public static IReadOnlyList<MapDoorVisual> Preview(MapData map, DoorMapState state = DoorMapState.Unknown)
            => Build(map, _ => true, _ => state);

        private static IReadOnlyList<MapDoorVisual> Build(
            MapData map,
            Func<MapRoomData, bool> isRoomShown,
            Func<string, DoorMapState> stateOf)
        {
            var shownRoomIds = new HashSet<string>();
            var ownersByDoor = new Dictionary<string, List<MapRoomData>>();

            foreach (var room in map.Rooms)
            {
                if (isRoomShown(room))
                    shownRoomIds.Add(room.RoomId);

                foreach (var doorId in room.DoorIds)
                {
                    if (!ownersByDoor.TryGetValue(doorId, out var owners))
                        ownersByDoor[doorId] = owners = new List<MapRoomData>();
                    owners.Add(room);
                }
            }

            var visuals = new List<MapDoorVisual>();
            foreach (var door in map.Doors)
            {
                if (!ownersByDoor.TryGetValue(door.DoorId, out var owners))
                    continue;

                // Placed on a shown owner when there is one; seen only from its other side, it
                // still sits on its own room (its offset is relative to that room).
                var owner = owners.FirstOrDefault(room => shownRoomIds.Contains(room.RoomId));
                if (owner == null && door.OtherSides.Any(side => shownRoomIds.Contains(side.RoomId)))
                    owner = owners[0];
                if (owner == null)
                    continue;

                // DoorMapState is ordered Unknown < Locked < Unlocked.
                var state = door.OtherSides
                    .Select(side => stateOf(side.DoorId))
                    .Append(stateOf(door.DoorId))
                    .Max();

                visuals.Add(new MapDoorVisual(
                    door.DoorId,
                    owner.RoomId,
                    state,
                    owner.Position + door.Transform.Offset,
                    door.Transform.Rotation,
                    door.Size));
            }
            return visuals;
        }
    }
}
