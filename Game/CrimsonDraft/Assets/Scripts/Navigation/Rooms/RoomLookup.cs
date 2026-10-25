#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Finds a room by its game-wide id (<see cref="RoomIds"/>).</summary>
    public static class RoomLookup
    {
        public static RoomController? Find(IEnumerable<RoomController> rooms, string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;
            foreach (var room in rooms)
                if (room != null && string.Equals(room.RoomId, roomId, StringComparison.Ordinal)) return room;
            return null;
        }

        /// <summary>Every room of the loaded scenes, inactive ones included (only one room is active at a time).</summary>
        public static RoomController[] LoadedRooms()
            => UnityEngine.Object.FindObjectsByType<RoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        public static RoomController? FindLoaded(string roomId) => Find(LoadedRooms(), roomId);
    }
}
