#nullable enable

using System.Reflection;
using CrimsonDraft.Navigation.CamaraSystem;
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
            this.root = new GameObject("Camera switch test");
            this.trigger = this.root.AddComponent<FixedCameraZoneTrigger>();
            this.a = CreateCamera("A");
            this.b = CreateCamera("B");
            SetField("zoneCamera", this.a);
            SetField("positiveSideCamera", this.b);
            SetField("useTwoSidedSwitch", true);
            SetField("sideMargin", 0.25f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.root);

        [Test]
        public void ReturningWithinMargin_preservesCameraUntilOppositeThreshold()
        {
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(Vector3.forward, this.a));
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(Vector3.back * 0.2f, this.b));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(Vector3.back * 0.3f, this.b));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(Vector3.forward * 0.2f, this.a));
        }

        [Test]
        public void RotatedAndScaledTrigger_usesForwardAndWorldSpaceMargin()
        {
            this.root.transform.SetPositionAndRotation(new Vector3(10, 0, 5), Quaternion.Euler(0, 90, 0));
            this.root.transform.localScale = Vector3.one * 10;
            var origin = this.root.transform.position;
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(origin + Vector3.right * 0.3f, this.a));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(origin - Vector3.right * 0.3f, this.b));
        }

        [Test]
        public void SpawnInsideMargin_selectsCameraWithoutPreviousShot()
        {
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(Vector3.back * 0.1f, null));
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(Vector3.forward * 0.1f, null));
        }

        [Test]
        public void LegacyMode_alwaysSelectsZoneCamera()
        {
            SetField("useTwoSidedSwitch", false);
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(Vector3.forward * 100, this.b));
        }

        [Test]
        public void OffsetDivider_splitsAtTheDividerNotTheObjectPivot()
        {
            SetField("dividerStart", new Vector2(-1f, 10f));
            SetField("dividerEnd", new Vector2(1f, 10f));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(new Vector3(0, 0, 9), this.b));
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(new Vector3(0, 0, 11), this.a));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(new Vector3(0, 0, 10.1f), this.a));
        }

        [Test]
        public void DiagonalDivider_decidesTheSideByDistanceToTheLine()
        {
            // Runs along x = z; positive side is the left of start -> end seen from above.
            SetField("dividerStart", new Vector2(0f, 0f));
            SetField("dividerEnd", new Vector2(10f, 10f));
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(new Vector3(0, 0, 5), this.a));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(new Vector3(5, 0, 0), this.b));
        }

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
        public void Contains_usesTheTransform()
        {
            this.root.transform.SetPositionAndRotation(new Vector3(10, 0, 0), Quaternion.Euler(0, 90, 0));
            this.root.transform.localScale = new Vector3(2, 1, 1);
            // Local polygon is a 2x2 square; local X is stretched to 4 units and turned along -Z.
            Assert.IsTrue(this.trigger.Contains(new Vector3(10.5f, 1, -1.5f)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(12f, 1, 0)));
        }

        [Test]
        public void Evaluate_activatesOnEntryAndNotAgainWhileInsideSingleSided()
        {
            SetField("useTwoSidedSwitch", false);
            var service = new FakeZoneService();
            this.trigger.Construct(service);

            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            this.a.enabled = true;
            this.trigger.Evaluate(new Vector3(0.1f, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);

            this.trigger.Evaluate(new Vector3(50, 1, 0));
            this.service_SwitchAway(service);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(2, service.ActivateCount);
        }

        [Test]
        public void Evaluate_twoSidedFollowsThePlayerAcrossTheDivider()
        {
            var service = new FakeZoneService();
            this.trigger.Construct(service);

            this.trigger.Evaluate(new Vector3(0, 1, -0.8f));
            Assert.AreSame(this.a, service.CurrentZoneCamera);
            this.trigger.Evaluate(new Vector3(0, 1, 0.8f));
            Assert.AreSame(this.b, service.CurrentZoneCamera);
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
        public void MissingSecondCamera_doesNotSelectAnIncompleteSwitch()
        {
            SetField("positiveSideCamera", null);
            Assert.IsNull(this.trigger.ResolveCameraAt(Vector3.forward, this.a));
        }

        private void service_SwitchAway(FakeZoneService service) => service.Force(this.b);

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
