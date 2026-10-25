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

        private CinemachineCamera MakeCamera(string name)
        {
            var go = new GameObject(name);
            this.created.Add(go);
            return go.AddComponent<CinemachineCamera>();
        }

        private FixedCameraZoneTrigger MakeZone(CinemachineCamera camera)
        {
            var go = new GameObject("Zone");
            this.created.Add(go);
            var zone = go.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }
    }
}
