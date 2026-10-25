#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class DoorArrivalLookupTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private RoomController origin = null!;
        private RoomController destination = null!;
        private RoomController other = null!;

        [SetUp]
        public void SetUp()
        {
            this.origin = MakeRoom("Origin");
            this.destination = MakeRoom("Destination");
            this.other = MakeRoom("Other");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in this.created) if (go != null) Object.DestroyImmediate(go);
            this.created.Clear();
        }

        [Test]
        public void FindArrivalDoor_returnsDoorLeadingBackToOrigin()
        {
            var back = MakeRoomDoor(this.destination, this.origin, new Vector3(0, 0, 0));
            Assert.AreSame(back, DoorArrivalLookup.FindArrivalDoor(this.destination, this.origin, Vector3.zero));
        }

        [Test]
        public void FindArrivalDoor_withTwoDoorsBack_picksNearestToDeparture()
        {
            MakeRoomDoor(this.destination, this.origin, new Vector3(0, 0, 0));
            var far = MakeRoomDoor(this.destination, this.origin, new Vector3(10, 0, 0));
            Assert.AreSame(far, DoorArrivalLookup.FindArrivalDoor(this.destination, this.origin, new Vector3(9, 0, 0)));
        }

        [Test]
        public void FindArrivalDoor_ignoresDoorsToOtherRooms()
        {
            MakeRoomDoor(this.destination, this.other, new Vector3(1, 0, 0));
            var back = MakeRoomDoor(this.destination, this.origin, new Vector3(5, 0, 0));
            Assert.AreSame(back, DoorArrivalLookup.FindArrivalDoor(this.destination, this.origin, new Vector3(1, 0, 0)));
        }

        [Test]
        public void FindArrivalDoor_noDoorBack_returnsNull()
        {
            MakeRoomDoor(this.destination, this.other, Vector3.zero);
            Assert.IsNull(DoorArrivalLookup.FindArrivalDoor(this.destination, this.origin, Vector3.zero));
        }

        [Test]
        public void FindEntryDoor_matchesId()
        {
            var a = MakeSceneDoor("TR_01");
            var b = MakeSceneDoor("TR_02");
            Assert.AreSame(b, DoorArrivalLookup.FindEntryDoor(new[] { a, b }, "TR_02"));
        }

        [Test]
        public void FindEntryDoor_duplicateIds_returnsFirst()
        {
            var a = MakeSceneDoor("TR_01");
            var b = MakeSceneDoor("TR_01");
            Assert.AreSame(a, DoorArrivalLookup.FindEntryDoor(new[] { a, b }, "TR_01"));
        }

        [Test]
        public void FindEntryDoor_emptyOrUnknownId_returnsNull()
        {
            var unset = MakeSceneDoor("");
            var a = MakeSceneDoor("TR_01");
            Assert.IsNull(DoorArrivalLookup.FindEntryDoor(new[] { unset, a }, ""));
            Assert.IsNull(DoorArrivalLookup.FindEntryDoor(new[] { unset, a }, "TR_99"));
        }

        [Test]
        public void ResolveArrival_returnsArrivalOfDoorBack()
        {
            var back = MakeRoomDoor(this.destination, this.origin, Vector3.zero);
            var arrival = back.GetComponent<DoorArrival>();
            arrival.PlaceAtDefault(1f, Vector3.right);

            Assert.AreSame(arrival, RoomOrchestrator.ResolveArrival(this.destination, this.origin, Vector3.zero, out var problem));
            Assert.IsNull(problem);
        }

        [Test]
        public void ResolveArrival_noDoorBack_reportsProblemWithoutLogging()
        {
            Assert.IsNull(RoomOrchestrator.ResolveArrival(this.destination, this.origin, Vector3.zero, out var problem));
            StringAssert.Contains("No door back to 'Origin' in 'Destination'", problem);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ResolveArrival_doorWithoutArrivalPoint_reportsMissingDoorArrival()
        {
            var back = MakeRoomDoor(this.destination, this.origin, Vector3.zero);
            var so = new SerializedObject(back.GetComponent<DoorArrival>());
            so.FindProperty("arrivalPoint").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(RoomOrchestrator.ResolveArrival(this.destination, this.origin, Vector3.zero, out var problem));
            StringAssert.Contains("has no DoorArrival", problem);
        }

        [Test]
        public void AddingADoor_addsItsDoorArrival()
        {
            var door = MakeRoomDoor(this.destination, this.origin, Vector3.zero);
            Assert.IsNotNull(door.GetComponent<DoorArrival>(), "every door is where somebody arrives");
        }

        private RoomController MakeRoom(string name)
        {
            var go = new GameObject(name);
            this.created.Add(go);
            var room = go.AddComponent<RoomController>();
            var so   = new SerializedObject(room);
            so.FindProperty("roomId").stringValue = "Deck_T/" + name.ToUpperInvariant();
            so.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        private RoomDoorInteractable MakeRoomDoor(RoomController room, RoomController target, Vector3 position)
        {
            var go = new GameObject("Door to " + target.name);
            go.transform.SetParent(room.transform, false);
            go.transform.position = position;
            var door = go.AddComponent<RoomDoorInteractable>();
            var so = new SerializedObject(door);
            so.FindProperty("destinationRoomId").stringValue = target.RoomId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private SceneDoorInteractable MakeSceneDoor(string entryPointId)
        {
            var go = new GameObject("SceneDoor " + entryPointId);
            this.created.Add(go);
            var door = go.AddComponent<SceneDoorInteractable>();
            var so = new SerializedObject(door);
            so.FindProperty("entryPointId").stringValue = entryPointId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }
    }
}
