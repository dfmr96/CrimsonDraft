#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Tests
{
    public sealed class CameraActivationTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void For_noTrigger_isNone()
        {
            var cam = MakeCamera("A");
            var status = CameraActivation.For(cam, new[] { MakeZone(MakeCamera("B")) });
            Assert.AreEqual(CameraActivationState.None, status.State);
            Assert.AreEqual(0, status.Triggers.Count);
        }

        [Test]
        public void For_oneSingleSidedTrigger_isOk()
        {
            var cam = MakeCamera("A");
            var zone = MakeZone(cam);
            var status = CameraActivation.For(cam, new[] { zone, MakeZone(MakeCamera("B")) });
            Assert.AreEqual(CameraActivationState.Ok, status.State);
            CollectionAssert.AreEqual(new[] { zone }, status.Triggers);
        }

        [Test]
        public void For_twoTriggers_isLegacyListingBoth()
        {
            var cam = MakeCamera("A");
            var first = MakeZone(cam);
            var second = MakeZone(cam);
            var status = CameraActivation.For(cam, new[] { first, second });
            Assert.AreEqual(CameraActivationState.Legacy, status.State);
            CollectionAssert.AreEquivalent(new[] { first, second }, status.Triggers);
        }

        [Test]
        public void For_onlyATwoSidedTrigger_isLegacy()
        {
            var cam = MakeCamera("A");
            var zone = MakeZone(cam, MakeCamera("B"));
            var status = CameraActivation.For(cam, new[] { zone });
            Assert.AreEqual(CameraActivationState.Legacy, status.State);
        }

        [Test]
        public void For_twoSidedAndNormalTrigger_isLegacyListingBoth()
        {
            var a = MakeCamera("A");
            var b = MakeCamera("B");
            var twoSided = MakeZone(a, b);
            var normal = MakeZone(a);

            var forA = CameraActivation.For(a, new[] { twoSided, normal });
            Assert.AreEqual(CameraActivationState.Legacy, forA.State);
            CollectionAssert.AreEquivalent(new[] { twoSided, normal }, forA.Triggers);

            var forB = CameraActivation.For(b, new[] { twoSided, normal });
            Assert.AreEqual(CameraActivationState.Legacy, forB.State);
            CollectionAssert.AreEqual(new[] { twoSided }, forB.Triggers);
        }

        private CinemachineCamera MakeCamera(string name)
        {
            var go = new GameObject(name);
            this.created.Add(go);
            return go.AddComponent<CinemachineCamera>();
        }

        private FixedCameraZoneTrigger MakeZone(CinemachineCamera camera, CinemachineCamera? positive = null)
        {
            var go = new GameObject("Zone");
            this.created.Add(go);
            var zone = go.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            if (positive != null)
            {
                so.FindProperty("useTwoSidedSwitch").boolValue = true;
                so.FindProperty("positiveSideCamera").objectReferenceValue = positive;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }
    }
}
