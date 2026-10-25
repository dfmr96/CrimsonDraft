#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Room id checks for the loaded scene: malformed or duplicate ids, doors pointing at
    /// rooms or doors that don't exist, and a catalog that is behind the scene.</summary>
    public static class RoomIdValidation
    {
        public static IEnumerable<string> Check(
            IReadOnlyList<RoomController> rooms, IReadOnlyList<RoomDoorInteractable> doors, RoomCatalog catalog)
        {
            foreach (var room in rooms)
            {
                if (!RoomIds.IsWellFormed(room.RoomId))
                    yield return $"Room '{room.name}' has id '{room.RoomId}', not in Deck/ROOM form.";
                else if (!catalog.Contains(room.RoomId))
                    yield return $"Room '{room.RoomId}' is not in the RoomCatalog; press Rebuild From Scenes on it.";
            }

            foreach (var group in rooms.Where(r => RoomIds.IsWellFormed(r.RoomId)).GroupBy(r => r.RoomId, StringComparer.Ordinal))
                if (group.Count() > 1)
                    yield return $"Room id '{group.Key}' is used by {group.Count()} rooms.";

            foreach (var door in doors)
            {
                if (!string.IsNullOrEmpty(door.DestinationRoomId) && !catalog.Contains(door.DestinationRoomId))
                    yield return $"Door '{door.DoorId}' leads to '{door.DestinationRoomId}', which is no room of the game.";

                var unlocks = door.UnlocksOnCross;
                if (!unlocks.IsSet) continue;
                if (!catalog.Contains(unlocks.RoomId))
                    yield return $"Door '{door.DoorId}' unlocks a door in '{unlocks.RoomId}', which is no room of the game.";
                else if (RoomLookup.Find(rooms, unlocks.RoomId) != null && unlocks.Resolve(rooms) == null)
                    yield return $"Door '{door.DoorId}' unlocks door '{unlocks.DoorId}' of '{unlocks.RoomId}', which has no such door.";
            }
        }
    }
}
