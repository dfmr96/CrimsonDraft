#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class CameraZoneGizmoTests
    {
        private GameObject room = null!;
        private GameObject otherRoom = null!;
        private FixedCameraZoneTrigger zone = null!;

        [SetUp]
        public void SetUp()
        {
            this.room = new GameObject("Room");
            this.room.AddComponent<RoomController>();
            this.otherRoom = new GameObject("Other room");
            this.otherRoom.AddComponent<RoomController>();
            var cameras = new GameObject("Cameras");
            cameras.transform.SetParent(this.room.transform, false);
            var go = new GameObject("Zone");
            go.transform.SetParent(cameras.transform, false);
            this.zone = go.AddComponent<FixedCameraZoneTrigger>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.room);
            Object.DestroyImmediate(this.otherRoom);
        }

        [Test]
        public void ShowsWhenItsRoomIsSelected() =>
            Assert.IsTrue(FixedCameraZoneTrigger.ShouldDrawGizmo(this.zone, new[] { this.room }));

        [Test]
        public void ShowsWhileTheZoneItselfIsSelected() =>
            Assert.IsTrue(FixedCameraZoneTrigger.ShouldDrawGizmo(this.zone, new[] { this.zone.gameObject }));

        [Test]
        public void HiddenWhenNothingOrAnotherRoomIsSelected()
        {
            Assert.IsFalse(FixedCameraZoneTrigger.ShouldDrawGizmo(this.zone, new GameObject[0]));
            Assert.IsFalse(FixedCameraZoneTrigger.ShouldDrawGizmo(this.zone, new[] { this.otherRoom }));
        }
    }
}
