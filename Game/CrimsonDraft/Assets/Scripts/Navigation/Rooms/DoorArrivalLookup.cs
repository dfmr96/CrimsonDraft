#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Which door the player arrives through.</summary>
    public static class DoorArrivalLookup
    {
        /// <summary>The door of <paramref name="destination"/> that leads back to <paramref name="origin"/>. With
        /// several (more than one doorway between the same rooms), the one nearest on the floor plane to
        /// <paramref name="departurePosition"/>, the door the player used: rooms sit in real space, so the two
        /// halves of a doorway are next to each other.</summary>
        public static RoomDoorInteractable? FindArrivalDoor(RoomController destination, RoomController origin, Vector3 departurePosition)
        {
            RoomDoorInteractable? best = null;
            float bestDistance = float.MaxValue;
            foreach (var door in destination.GetComponentsInChildren<RoomDoorInteractable>(true))
            {
                if (door.Destination != origin) continue;

                var offset = door.transform.position - departurePosition;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = door;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>The scene door other scenes arrive through with <paramref name="entryPointId"/>; the first one
        /// when several share it.</summary>
        public static SceneDoorInteractable? FindEntryDoor(IEnumerable<SceneDoorInteractable> doors, string entryPointId)
        {
            if (string.IsNullOrEmpty(entryPointId)) return null;
            foreach (var door in doors)
                if (string.Equals(door.EntryPointId, entryPointId, System.StringComparison.Ordinal)) return door;
            return null;
        }
    }
}
