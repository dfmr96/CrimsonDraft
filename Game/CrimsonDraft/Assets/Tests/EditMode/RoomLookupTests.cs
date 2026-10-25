#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomLookupTests
    {
        private RoomController a = null!;
        private RoomController b = null!;

        [SetUp]
        public void SetUp()
        {
            this.a = Make("Deck_T/A");
            this.b = Make("Deck_T/B");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.a.gameObject);
            Object.DestroyImmediate(this.b.gameObject);
        }

        [Test]
        public void Find_returnsRoomWithId()
            => Assert.AreSame(this.b, RoomLookup.Find(new[] { this.a, this.b }, "Deck_T/B"));

        [Test]
        public void Find_unknownId_returnsNull()
            => Assert.IsNull(RoomLookup.Find(new[] { this.a, this.b }, "Deck_T/C"));

        [Test]
        public void Find_emptyId_returnsNull()
            => Assert.IsNull(RoomLookup.Find(new[] { this.a, this.b }, ""));

        [Test]
        public void FindLoaded_findsInactiveRoomsOfTheOpenScene()
        {
            this.b.gameObject.SetActive(false);
            Assert.AreSame(this.b, RoomLookup.FindLoaded("Deck_T/B"));
        }

        private static RoomController Make(string id)
        {
            var room = new GameObject(id).AddComponent<RoomController>();
            var so   = new SerializedObject(room);
            so.FindProperty("roomId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }
    }
}
