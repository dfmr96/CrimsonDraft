#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Editor;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class CameraActivationEditingTests
    {
        // Far from any open scene's geometry, so raycasts only see what the tests build.
        private static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);
        private readonly List<Object> created = new List<Object>();
        private RoomController room = null!;

        [SetUp]
        public void SetUp()
        {
            var go = Track(new GameObject("Room"));
            go.transform.position = Origin;
            this.room = go.AddComponent<RoomController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void CreateTrigger_placesSquareUnderLookPoint()
        {
            var floor = Track(new GameObject("Floor"));
            floor.transform.position = Origin + Vector3.down * 0.05f;
            floor.AddComponent<BoxCollider>().size = new Vector3(20f, 0.1f, 20f);
            var camera = MakeCamera(Origin + new Vector3(2f, 5f, 3f), Quaternion.Euler(90f, 0f, 0f));

            var zone = Track(CameraActivationEditing.CreateTrigger(camera, this.room));

            Assert.That(Vector3.Distance(zone.transform.position, Origin + new Vector3(2f, 0f, 3f)), Is.LessThan(1e-3f));
            Assert.AreSame(camera, zone.ZoneCamera);
            Assert.IsFalse(zone.UseTwoSidedSwitch);
            CollectionAssert.AreEqual(new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) }, zone.Polygon);
            Assert.AreEqual("Cameras", zone.transform.parent.name);
            Assert.AreSame(this.room.transform, zone.transform.parent.parent);
        }

        [Test]
        public void CreateTrigger_lookRayHitsWall_placesZoneOnTheFloorBelow()
        {
            var floor = Track(new GameObject("Floor"));
            floor.transform.position = Origin + Vector3.down * 0.05f;
            floor.AddComponent<BoxCollider>().size = new Vector3(40f, 0.1f, 40f);
            var wall = Track(new GameObject("Wall"));
            wall.transform.position = Origin + new Vector3(0f, 2f, 6f);
            wall.AddComponent<BoxCollider>().size = new Vector3(10f, 4f, 0.2f);
            // Pitched 10° down: the look ray meets the wall (~1.6 m up) before the floor.
            var camera = MakeCamera(Origin + new Vector3(0f, 2.5f, 0f), Quaternion.Euler(10f, 0f, 0f));

            var zone = Track(CameraActivationEditing.CreateTrigger(camera, this.room));

            Assert.That(zone.transform.position.y, Is.EqualTo(Origin.y).Within(1e-3f), "zone must sit on the floor, not on the wall");
            Assert.That(zone.transform.position.z, Is.LessThan(Origin.z + 6f), "in front of the wall");
        }

        [Test]
        public void CreateTrigger_noFloorHit_fallsBackUnderCamera()
        {
            var camera = MakeCamera(Origin + new Vector3(0f, 5f, 0f), Quaternion.Euler(-30f, 0f, 0f)); // looking up
            Physics.SyncTransforms();

            var zone = Track(CameraActivationEditing.CreateTrigger(camera, this.room));

            Assert.That(Vector3.Distance(zone.transform.position, Origin + new Vector3(0f, 3f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void KeepOnly_turnsTwoSidedIntoSingleForThatCamera()
        {
            var a = MakeCamera(Origin, Quaternion.identity);
            var b = MakeCamera(Origin, Quaternion.identity);
            var zone = Track(new GameObject("Zone")).AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = a;
            so.FindProperty("useTwoSidedSwitch").boolValue = true;
            so.FindProperty("positiveSideCamera").objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();

            CameraActivationEditing.KeepOnly(zone, b);

            Assert.AreSame(b, zone.ZoneCamera);
            Assert.IsFalse(zone.UseTwoSidedSwitch);
            Assert.AreEqual(CameraActivationState.None, CameraActivation.For(a, new[] { zone }).State);
            Assert.AreEqual(CameraActivationState.Ok, CameraActivation.For(b, new[] { zone }).State);
        }

        private CinemachineCamera MakeCamera(Vector3 position, Quaternion rotation)
        {
            var go = Track(new GameObject("Camera"));
            go.transform.SetPositionAndRotation(position, rotation);
            return go.AddComponent<CinemachineCamera>();
        }

        private T Track<T>(T obj) where T : Object
        {
            this.created.Add(obj is Component c ? c.gameObject : obj);
            return obj;
        }
    }
}
