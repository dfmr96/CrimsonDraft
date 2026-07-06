#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapDataTests
    {
        [Test]
        public void MapData_defaults_are_empty_and_grid_is_sane()
        {
            var map = ScriptableObject.CreateInstance<MapData>();

            Assert.AreEqual(string.Empty, map.SceneName);
            Assert.AreEqual(string.Empty, map.DisplayName);
            Assert.AreEqual(string.Empty, map.Abbreviation);
            Assert.AreEqual(string.Empty, map.MapItemId);
            Assert.AreEqual(new Vector2Int(25, 25), map.GridSize);
            Assert.AreEqual(1f, map.CellSize);
            Assert.IsEmpty(map.Rooms);
            Assert.IsEmpty(map.Doors);
        }

#if UNITY_EDITOR
        [Test]
        public void EditorSetBakedContent_replacesRoomsAndDoors()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var rooms = new List<MapRoomData> { new() { RoomId = "room-a" } };
            var doors = new List<MapDoorData> { new() { DoorId = "door-a" } };

            map.EditorSetBakedContent(rooms, doors);

            Assert.AreSame(rooms, map.Rooms);
            Assert.AreSame(doors, map.Doors);
        }
#endif
    }
}
