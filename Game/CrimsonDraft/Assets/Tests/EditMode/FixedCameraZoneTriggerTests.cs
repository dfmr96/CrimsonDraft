#nullable enable

using System.Linq;
using System.Reflection;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Interactables;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    public sealed class FixedCameraZoneTriggerTests
    {
        private GameObject root = null!;
        private FixedCameraZoneTrigger trigger = null!;
        private CinemachineCamera a = null!;
        private CinemachineCamera b = null!;

        [SetUp]
        public void SetUp()
        {
            this.root = new GameObject("Camera zone test");
            this.trigger = this.root.AddComponent<FixedCameraZoneTrigger>();
            this.a = CreateCamera("A");
            this.b = CreateCamera("B");
            SetField("zoneCamera", this.a);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.root);

        [Test]
        public void ReferencedCameras_isTheZoneCameraOnly()
            => CollectionAssert.AreEqual(new[] { this.a }, this.trigger.ReferencedCameras.ToArray());

        [Test]
        public void Contains_followsAConcavePolygon()
        {
            SetField("polygon", new[]
            {
                new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 1),
                new Vector2(1, 1), new Vector2(1, 4), new Vector2(0, 4),
            });
            Assert.IsTrue(this.trigger.Contains(new Vector3(0.5f, 1, 3)));
            Assert.IsTrue(this.trigger.Contains(new Vector3(3, 1, 0.5f)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(3, 1, 3)));
        }

        [Test]
        public void Contains_rejectsPositionsOutsideTheHeightRange()
        {
            SetField("minHeight", 0f);
            SetField("maxHeight", 2f);
            Assert.IsTrue(this.trigger.Contains(new Vector3(0, 1, 0)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(0, 3, 0)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(0, -1, 0)));
        }

        [Test]
        public void Contains_measuresInTheParentSpace_notTheZoneObject()
        {
            var room = new GameObject("Room");
            try
            {
                room.transform.SetPositionAndRotation(new Vector3(10, 0, 0), Quaternion.Euler(0, 90, 0));
                room.transform.localScale = new Vector3(2, 1, 1);
                this.root.transform.SetParent(room.transform, false);
                // Moving or turning the zone object itself (the camera) never moves where it activates.
                this.root.transform.localPosition = new Vector3(30, 5, 30);
                this.root.transform.localRotation = Quaternion.Euler(40, 70, 0);

                // The 2x2 square is in room space: room X is stretched to 4 units and turned along -Z.
                Assert.IsTrue(this.trigger.Contains(new Vector3(10.5f, 1, -1.5f)));
                Assert.IsFalse(this.trigger.Contains(new Vector3(12f, 1, 0)));
            }
            finally
            {
                this.root.transform.SetParent(null);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Evaluate_activatesOnEntryAndNotAgainWhileInside()
        {
            var service = new FakeZoneService();
            this.trigger.Construct(service);

            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            this.a.enabled = true;
            this.trigger.Evaluate(new Vector3(0.1f, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);

            this.trigger.Evaluate(new Vector3(50, 1, 0));
            service.Force(this.b);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(2, service.ActivateCount);
        }

        [Test]
        public void Evaluate_ignoresAPlayerOutsideThePolygon()
        {
            var service = new FakeZoneService();
            this.trigger.Construct(service);
            this.trigger.Evaluate(new Vector3(20, 1, 0));
            Assert.AreEqual(0, service.ActivateCount);
        }

        [Test]
        public void SolvedPuzzle_stopsTheZoneFromCuttingToItsCamera()
        {
            var puzzle = this.root.AddComponent<FakePuzzle>();
            SetField("completeWhenPuzzle", puzzle);
            var registry = new PuzzleStateRegistry();
            var service = new FakeZoneService();
            this.trigger.Construct(service, registry);

            Assert.IsFalse(this.trigger.IsCompleted);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            registry.SetSolved(FakePuzzle.Id);
            Assert.IsTrue(this.trigger.IsCompleted);

            // Solved while the player is inside: the camera stays, nothing cuts back or away.
            service.Force(this.b);
            this.trigger.Evaluate(new Vector3(0.1f, 1, 0));
            Assert.AreSame(this.b, service.CurrentZoneCamera);

            // Leaving and coming back no longer fires it either.
            this.trigger.Evaluate(new Vector3(50, 1, 0));
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.b, service.CurrentZoneCamera);
        }

        [Test]
        public void ZoneWithoutAGate_isNeverCompleted()
        {
            this.trigger.Construct(new FakeZoneService(), new PuzzleStateRegistry());
            Assert.IsFalse(this.trigger.IsCompleted);
        }

        private sealed class FakePuzzle : MonoBehaviour, IPuzzle
        {
            public const string Id = "fake-puzzle";
            public string PuzzleId => Id;
            public void BindPuzzleState(PuzzleStateRegistry registry) { }
        }

        private sealed class FakeZoneService : IFixedCameraZoneService
        {
            public CinemachineCamera? CurrentZoneCamera { get; private set; }
            public CinemachineCamera? GameplayZoneCamera => this.CurrentZoneCamera;
            public int ActivateCount { get; private set; }

            public void ActivateZone(CinemachineCamera zoneCamera)
            {
                this.CurrentZoneCamera = zoneCamera;
                this.ActivateCount++;
            }

            public void ActivateInspectionShot(CinemachineCamera inspectCamera) => this.CurrentZoneCamera = inspectCamera;

            public void Force(CinemachineCamera camera) => this.CurrentZoneCamera = camera;
        }

        private CinemachineCamera CreateCamera(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(this.root.transform);
            return obj.AddComponent<CinemachineCamera>();
        }

        private void SetField(string name, object? value) => typeof(FixedCameraZoneTrigger)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this.trigger, value);
    }
}
