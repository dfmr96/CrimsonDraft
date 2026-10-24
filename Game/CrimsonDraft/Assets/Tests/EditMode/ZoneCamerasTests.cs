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
    public sealed class ZoneCamerasTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void Of_keepsCamerasUnderCamerasAndZoneTargets_dropsFollowAndInspectionCameras()
        {
            var room = Make("Room").AddComponent<RoomController>();
            var cameras = Make("Cameras", room.transform);
            var zoneCam = Make("Zone", cameras.transform).AddComponent<CinemachineCamera>();
            var newCam = Make("New", cameras.transform).AddComponent<CinemachineCamera>();          // no zone yet: still a zone camera
            var inspectCam = Make("Inspect", cameras.transform).AddComponent<CinemachineCamera>(); // used by a puzzle
            var followCam = Make("Follow", room.transform).AddComponent<CinemachineCamera>();      // room root, not under Cameras
            var elsewhere = Make("Elsewhere", room.transform).AddComponent<CinemachineCamera>();   // outside Cameras but a zone targets it

            var zone = Make("ZoneTrigger", cameras.transform).AddComponent<FixedCameraZoneTrigger>();
            Set(zone, "zoneCamera", zoneCam);
            var zone2 = Make("ZoneTrigger2", cameras.transform).AddComponent<FixedCameraZoneTrigger>();
            Set(zone2, "zoneCamera", elsewhere);
            var puzzle = Make("Puzzle", room.transform).AddComponent<FakeInspectable>();
            Set(puzzle, "inspectCamera", inspectCam);

            CollectionAssert.AreEquivalent(new[] { zoneCam, newCam, elsewhere }, ZoneCameras.Of(room));
            Assert.IsFalse(System.Array.IndexOf(ZoneCameras.Of(room), followCam) >= 0);
        }

        [Test]
        public void Of_keepsArrivalCamerasOfDoorsAndSpawns()
        {
            var room = Make("Room").AddComponent<RoomController>();
            var cameras = Make("Cameras", room.transform);
            var doorCam = Make("DoorArrivalCam", cameras.transform).AddComponent<CinemachineCamera>();
            var spawnCam = Make("SpawnCam", cameras.transform).AddComponent<CinemachineCamera>();
            var arrival = Make("Door", room.transform).AddComponent<DoorArrival>();
            Set(arrival, "arrivalCamera", doorCam);
            var spawn = Make("Spawn", room.transform).AddComponent<SpawnPoint>();
            Set(spawn, "camera", spawnCam);

            CollectionAssert.AreEquivalent(new[] { doorCam, spawnCam }, ZoneCameras.Of(room));
        }

        private sealed class FakeInspectable : MonoBehaviour
        {
#pragma warning disable CS0649
            [SerializeField] private CinemachineCamera? inspectCamera;
#pragma warning restore CS0649
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Make(string name, Transform? parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else this.created.Add(go);
            return go;
        }
    }
}
