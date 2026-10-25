#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>A door in (possibly) another room, by room id + door id: door ids alone are not
    /// unique, and an object reference to another room's prefab can only live in the scene.</summary>
    [Serializable]
    public struct DoorRef
    {
        [SerializeField] private string roomId;
        [SerializeField] private string doorId;

        public DoorRef(string roomId, string doorId)
        {
            this.roomId = roomId;
            this.doorId = doorId;
        }

        public string RoomId => this.roomId ?? "";
        public string DoorId => this.doorId ?? "";
        public bool   IsSet  => !string.IsNullOrEmpty(this.roomId) && !string.IsNullOrEmpty(this.doorId);

        public RoomDoorInteractable? Resolve(IEnumerable<RoomController> rooms)
        {
            if (!IsSet) return null;
            var room = RoomLookup.Find(rooms, RoomId);
            if (room == null) return null;
            foreach (var door in room.GetComponentsInChildren<RoomDoorInteractable>(true))
                if (string.Equals(door.DoorId, DoorId, StringComparison.Ordinal)) return door;
            return null;
        }
    }
}
