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
        public void OffsetCollider_usesVolumeCenterInsteadOfObjectPivot()
        {
            var box = this.root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0, 10);
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(new Vector3(0, 0, 9), this.b));
            Assert.AreSame(this.b, this.trigger.ResolveCameraAt(new Vector3(0, 0, 11), this.a));
            Assert.AreSame(this.a, this.trigger.ResolveCameraAt(new Vector3(0, 0, 10.1f), this.a));
        }

        [Test]
        public void MissingSecondCamera_doesNotSelectAnIncompleteSwitch()
        {
            SetField("positiveSideCamera", null);
            Assert.IsNull(this.trigger.ResolveCameraAt(Vector3.forward, this.a));
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
