#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Editor;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomIdValidationTests
    {
        private readonly List<Object> created = new List<Object>();
        private RoomCatalog catalog = null!;

        [SetUp]
        public void SetUp()
        {
            this.catalog = ScriptableObject.CreateInstance<RoomCatalog>();
            this.created.Add(this.catalog);
            this.catalog.EditorSetRooms(new[]
            {
                new RoomCatalog.Entry("Deck_T/A", "Deck_T_Development"),
                new RoomCatalog.Entry("Deck_T/B", "Deck_T_Development"),
                new RoomCatalog.Entry("Deck_U/C", "Deck_U_Development"),
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o is Component c ? c.gameObject : o);
            this.created.Clear();
        }

        [Test]
        public void Check_cleanData_reportsNothing()
        {
            var a = Room("Deck_T/A");
            var b = Room("Deck_T/B");
            Door(a, "DR_1", "Deck_T/B", new DoorRef("Deck_T/B", "DR_2"));
            Door(b, "DR_2", "Deck_T/A");

            CollectionAssert.IsEmpty(Check(a, b));
        }

        [Test]
        public void Check_malformedRoomId()
            => AssertSingle(Check(Room("Deck_T/A"), Room("Deck_T/B"), Room("hallway b1")), "hallway b1");

        [Test]
        public void Check_duplicateRoomId()
            => AssertSingle(Check(Room("Deck_T/A"), Room("Deck_T/B"), Room("Deck_T/B")), "Deck_T/B");

        [Test]
        public void Check_doorDestinationNotInCatalog()
        {
            var a = Room("Deck_T/A");
            Door(a, "DR_1", "Deck_T/NOWHERE");
            AssertSingle(Check(a, Room("Deck_T/B")), "Deck_T/NOWHERE");
        }

        [Test]
        public void Check_doorRefToUnknownRoom()
        {
            var a = Room("Deck_T/A");
            Door(a, "DR_1", "Deck_T/B", new DoorRef("Deck_T/GONE", "DR_9"));
            AssertSingle(Check(a, Room("Deck_T/B")), "Deck_T/GONE");
        }

        [Test]
        public void Check_doorRefToUnknownDoorOfALoadedRoom()
        {
            var a = Room("Deck_T/A");
            Door(a, "DR_1", "Deck_T/B", new DoorRef("Deck_T/B", "DR_9"));
            AssertSingle(Check(a, Room("Deck_T/B")), "DR_9");
        }

        [Test]
        public void Check_roomMissingFromCatalog()
            => AssertSingle(Check(Room("Deck_T/A"), Room("Deck_T/B"), Room("Deck_T/NEW")), "Deck_T/NEW");

        private IReadOnlyList<string> Check(params RoomController[] rooms)
            => RoomIdValidation.Check(rooms, rooms.SelectMany(r => r.GetComponentsInChildren<RoomDoorInteractable>(true)).ToList(), this.catalog).ToList();

        private static void AssertSingle(IReadOnlyList<string> messages, string offending)
        {
            Assert.AreEqual(1, messages.Count, string.Join("\n", messages));
            StringAssert.Contains(offending, messages[0]);
        }

        private RoomController Room(string id)
        {
            var room = new GameObject(id).AddComponent<RoomController>();
            this.created.Add(room);
            Set(room, so => so.FindProperty("roomId").stringValue = id);
            return room;
        }

        private static void Door(RoomController room, string doorId, string destination, DoorRef unlocks = default)
        {
            var go = new GameObject(doorId);
            go.transform.SetParent(room.transform, false);
            var door = go.AddComponent<RoomDoorInteractable>();
            Set(door, so =>
            {
                so.FindProperty("doorId").stringValue                   = doorId;
                so.FindProperty("destinationRoomId").stringValue        = destination;
                so.FindProperty("unlocksOnCrossRef.roomId").stringValue = unlocks.RoomId;
                so.FindProperty("unlocksOnCrossRef.doorId").stringValue = unlocks.DoorId;
            });
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
