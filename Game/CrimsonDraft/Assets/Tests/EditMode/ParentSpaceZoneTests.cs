#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Tests
{
    /// <summary>A zone that lives on its camera measures its polygon in the camera's parent (the room),
    /// so moving or turning the camera never moves where it activates.</summary>
    public sealed class ParentSpaceZoneTests
    {
        private GameObject room = null!;
        private FixedCameraZoneTrigger zone = null!;

        [SetUp]
        public void SetUp()
        {
            this.room = new GameObject("Room");
            this.room.transform.position = new Vector3(10f, 0f, 0f);
            var camera = new GameObject("Camera");
            camera.transform.SetParent(this.room.transform, false);
            camera.transform.localPosition = new Vector3(2f, 3f, 0f);
            camera.transform.localRotation = Quaternion.Euler(40f, 30f, 0f);
            this.zone = camera.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(this.zone);
            so.FindProperty("minHeight").floatValue = 0f;
            so.FindProperty("maxHeight").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.room);

        [Test]
        public void Contains_measuresInTheParentNotTheTiltedCamera()
        {
            // Default polygon is a 2x2 square around the parent's origin.
            Assert.IsTrue(this.zone.Contains(new Vector3(10.5f, 1f, 0.5f)));
            Assert.IsFalse(this.zone.Contains(new Vector3(12f, 1f, 0f)), "around the camera is not inside");
        }

        [Test]
        public void MovingOrTurningTheCamera_doesNotMoveTheZone()
        {
            this.zone.transform.localPosition = new Vector3(-5f, 7f, 4f);
            this.zone.transform.localRotation = Quaternion.Euler(60f, -90f, 10f);
            Assert.IsTrue(this.zone.Contains(new Vector3(10.5f, 1f, 0.5f)));
        }

        [Test]
        public void MovingTheRoom_movesTheZone()
        {
            this.room.transform.position = new Vector3(0f, 0f, 20f);
            Assert.IsTrue(this.zone.Contains(new Vector3(0f, 1f, 20.5f)));
            Assert.IsFalse(this.zone.Contains(new Vector3(10.5f, 1f, 0.5f)));
        }

        [Test]
        public void HeightRange_isInParentSpace()
        {
            Assert.IsFalse(this.zone.Contains(new Vector3(10f, 2.5f, 0f)), "above maxHeight of the room");
            Assert.IsFalse(this.zone.Contains(new Vector3(10f, -0.5f, 0f)));
        }

        [Test]
        public void ToWorldAndToLocal_roundTripThroughTheParent()
        {
            var world = this.zone.ToWorld(new Vector2(1f, -1f), 0.5f);
            Assert.That(Vector3.Distance(world, new Vector3(11f, 0.5f, -1f)), Is.LessThan(1e-4f));
            var local = this.zone.ToLocal(world);
            Assert.That(Vector3.Distance(local, new Vector3(1f, 0.5f, -1f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void WithoutParent_usesWorldSpace()
        {
            var loose = new GameObject("Loose camera");
            try
            {
                loose.transform.SetPositionAndRotation(new Vector3(50f, 5f, 0f), Quaternion.Euler(45f, 0f, 0f));
                var z = loose.AddComponent<FixedCameraZoneTrigger>();
                Assert.IsTrue(z.Contains(new Vector3(0.5f, 1f, 0.5f)));
            }
            finally
            {
                Object.DestroyImmediate(loose);
            }
        }
    }
}
