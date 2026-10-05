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
        public void MapData_defaults_are_empty()
        {
            var map = ScriptableObject.CreateInstance<MapData>();

            Assert.AreEqual(string.Empty, map.SceneName);
            Assert.AreEqual(string.Empty, map.DisplayName);
            Assert.AreEqual(string.Empty, map.Abbreviation);
            Assert.AreEqual(string.Empty, map.MapItemId);
            Assert.IsEmpty(map.Rooms);
            Assert.IsEmpty(map.Doors);
        }

        [Test]
        public void MapRoomData_defaults_haveNoLayout()
        {
            var room = new MapRoomData();
            Assert.IsNull(room.IncompleteSprite);
            Assert.IsNull(room.CompleteSprite);
            Assert.AreEqual(Vector2Int.zero, room.Position);
            Assert.AreEqual(0, room.QuarterTurns);
            Assert.IsFalse(room.IsOrphan);
            Assert.IsEmpty(room.PickupIds);
            Assert.IsEmpty(room.DoorIds);
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
