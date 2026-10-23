#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class DoorArrivalPlacementTests
    {
        [Test]
        public void DefaultPose_thinXDoor_facesIntoRoomOnX()
        {
            var bounds = new Bounds(new Vector3(10, 1.1f, 5), new Vector3(0.3f, 2.2f, 1.2f));
            var pose = DoorArrivalPlacement.DefaultPose(bounds, new Vector3(4, 1, 5), 1f);

            Assert.That(pose.position.x, Is.EqualTo(9f).Within(1e-4));
            Assert.That(pose.position.z, Is.EqualTo(5f).Within(1e-4));
            Assert.That(pose.position.y, Is.EqualTo(1f).Within(1e-4)); // min.y 0 + 1
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.left), Is.EqualTo(1f).Within(1e-4));
        }

        [Test]
        public void DefaultPose_thinZDoor_usesZAxisAndDistance()
        {
            var bounds = new Bounds(new Vector3(2, 1.25f, 8), new Vector3(2.2f, 2.5f, 0.13f));
            var pose = DoorArrivalPlacement.DefaultPose(bounds, new Vector3(2, 1, 5), 1.5f);

            Assert.That(pose.position.x, Is.EqualTo(2f).Within(1e-4));
            Assert.That(pose.position.z, Is.EqualTo(6.5f).Within(1e-4));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.back), Is.EqualTo(1f).Within(1e-4));
        }

        [Test]
        public void DefaultPose_hintOnDoorPlane_keepsPositiveAxis()
        {
            var bounds = new Bounds(new Vector3(0, 1, 0), new Vector3(0.3f, 2f, 1.2f));
            var pose = DoorArrivalPlacement.DefaultPose(bounds, bounds.center, 1f);

            Assert.That(pose.position.x, Is.EqualTo(1f).Within(1e-4));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.right), Is.EqualTo(1f).Within(1e-4));
        }

        [Test]
        public void DoorBounds_withoutRenderers_returnsSlabOnPivot()
        {
            var go = new GameObject("Door");
            go.transform.position = new Vector3(3, 0, 4);
            try
            {
                var bounds = DoorArrivalPlacement.DoorBounds(go.transform);
                Assert.AreEqual(new Vector3(3, 0, 4), bounds.center);
                Assert.AreEqual(new Vector3(0.2f, 2f, 1.2f), bounds.size);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BareDoorTurned90_facesIntoRoomAlongZ()
        {
            var go = new GameObject("Door");
            go.transform.SetPositionAndRotation(new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0));
            try
            {
                var bounds = DoorArrivalPlacement.DoorBounds(go.transform);
                var pose = DoorArrivalPlacement.DefaultPose(bounds, new Vector3(0, 1, 4), 1f);

                Assert.That(pose.position.x, Is.EqualTo(0f).Within(1e-4));
                Assert.That(pose.position.z, Is.EqualTo(1f).Within(1e-4));
                Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.forward), Is.EqualTo(1f).Within(1e-4));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlaceAtDefault_createsArrivalChildOnce()
        {
            var door = new GameObject("Door").AddComponent<DoorArrival>();
            try
            {
                door.PlaceAtDefault(1f, door.transform.position + Vector3.right * 5);
                var first = door.ArrivalPoint;
                door.PlaceAtDefault(1f, door.transform.position + Vector3.right * 5);

                Assert.AreSame(first, door.ArrivalPoint);
                Assert.AreEqual(door.transform, first.parent);
                Assert.AreEqual(1, door.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }
    }
}
