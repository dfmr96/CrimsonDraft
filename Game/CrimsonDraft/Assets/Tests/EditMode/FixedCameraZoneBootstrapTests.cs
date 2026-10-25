#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Tests
{
    /// <summary>Drives zones through the real bootstrap with a capsule-sized player, the path the
    /// game uses every frame.</summary>
    public sealed class FixedCameraZoneBootstrapTests
    {
        private readonly List<Object> created = new List<Object>();
        private FixedCameraZoneService service = null!;
        private PlayerController player = null!;

        [SetUp]
        public void SetUp()
        {
            this.service = new FixedCameraZoneService();
            var playerGo = Track(new GameObject("Player"));
            this.player = playerGo.AddComponent<PlayerController>();
            var capsule = playerGo.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 1.64f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void AdjacentZones_walkingBackCutsBackToTheFirstCamera()
        {
            var a = MakeCamera("A");
            var b = MakeCamera("B");
            var bootstrap = MakeBootstrap(MakeZone(-4, 0, a), MakeZone(0, 4, b));

            var seen = Walk(bootstrap, 0f, -2f, -0.4f, 0.3f, -0.3f, -3f);

            CollectionAssert.AreEqual(new[] { a, a, b, a, a }, seen);
        }

        [Test]
        public void InspectionShot_isNotReplacedByZones()
        {
            var z1 = MakeCamera("Z1");
            var inspect = MakeCamera("Inspect");
            var bootstrap = MakeBootstrap(MakeZone(-4, 0, z1));
            Walk(bootstrap, -1f, -2f);

            this.service.ActivateInspectionShot(inspect);
            Walk(bootstrap, -1f, -2f, -1.5f);

            Assert.AreSame(inspect, this.service.CurrentZoneCamera);
        }

        private List<CinemachineCamera?> Walk(FixedCameraZoneBootstrap bootstrap, float z, params float[] xs)
        {
            var seen = new List<CinemachineCamera?>();
            foreach (var x in xs)
            {
                this.player.transform.position = new Vector3(x, 1f, z);
                Physics.SyncTransforms();
                ((ITickable)bootstrap).Tick();
                seen.Add(this.service.CurrentZoneCamera);
            }
            return seen;
        }

        private FixedCameraZoneBootstrap MakeBootstrap(params FixedCameraZoneTrigger[] triggers)
        {
            var bootstrap = new FixedCameraZoneBootstrap(triggers, this.service, this.player, new PuzzleStateRegistry());
            ((IInitializable)bootstrap).Initialize();
            return bootstrap;
        }

        private CinemachineCamera MakeCamera(string name) => Track(new GameObject(name)).AddComponent<CinemachineCamera>();

        private FixedCameraZoneTrigger MakeZone(float xMin, float xMax, CinemachineCamera camera)
        {
            var zone = Track(new GameObject("Zone " + camera.name)).AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            var polygon = so.FindProperty("polygon");
            polygon.arraySize = 4;
            polygon.GetArrayElementAtIndex(0).vector2Value = new Vector2(xMin, -2);
            polygon.GetArrayElementAtIndex(1).vector2Value = new Vector2(xMin, 2);
            polygon.GetArrayElementAtIndex(2).vector2Value = new Vector2(xMax, 2);
            polygon.GetArrayElementAtIndex(3).vector2Value = new Vector2(xMax, -2);
            so.FindProperty("minHeight").floatValue = -1f;
            so.FindProperty("maxHeight").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private GameObject Track(GameObject go)
        {
            this.created.Add(go);
            return go;
        }
    }
}
