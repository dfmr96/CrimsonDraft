#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class WorldRestoreTests
    {
        [Test]
        public void IsActive_onlyInsideBegin_andNests()
        {
            Assert.IsFalse(WorldRestore.IsActive);
            using (WorldRestore.Begin())
            {
                Assert.IsTrue(WorldRestore.IsActive);
                using (WorldRestore.Begin()) Assert.IsTrue(WorldRestore.IsActive);
                Assert.IsTrue(WorldRestore.IsActive, "the outer restore is still running");
            }
            Assert.IsFalse(WorldRestore.IsActive);
        }
    }

    /// <summary>On load, a solved safe must simply be open: no slide, and in the right place even though
    /// the restore runs during scene setup, before this component's Awake (Awake never runs in EditMode,
    /// which is exactly that situation).</summary>
    public sealed class SlideOpenOnCallTests
    {
        private GameObject go = null!;
        private SlideOpenOnCall slide = null!;

        [SetUp]
        public void SetUp()
        {
            this.go = new GameObject("Door");
            this.go.transform.localPosition = new Vector3(-0.94f, 1.78f, -0.86f);
            this.go.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            this.slide = this.go.AddComponent<SlideOpenOnCall>();
            var so = new SerializedObject(this.slide);
            so.FindProperty("openLocalOffset").vector3Value = new Vector3(0f, 0f, -0.3f);
            so.FindProperty("openLocalRotationOffset").vector3Value = new Vector3(0f, 90f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.go);

        [Test]
        public void OpenWhileRestoring_snapsToTheOpenPose()
        {
            using (WorldRestore.Begin()) this.slide.Open();

            AssertClose(new Vector3(-0.94f, 1.78f, -1.16f), this.go.transform.localPosition);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 120f, 0f), this.go.transform.localRotation), 0.01f);
        }

        [Test]
        public void SnappedOpen_thenClose_returnsToTheRealClosedPose()
        {
            using (WorldRestore.Begin())
            {
                this.slide.Open();
                this.slide.Close();
            }

            AssertClose(new Vector3(-0.94f, 1.78f, -0.86f), this.go.transform.localPosition);
        }

        [Test]
        public void OpeningTwice_whileRestoring_doesNotOffsetAgain()
        {
            using (WorldRestore.Begin())
            {
                this.slide.Open();
                this.slide.Open();
            }

            AssertClose(new Vector3(-0.94f, 1.78f, -1.16f), this.go.transform.localPosition);
        }

        private static void AssertClose(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 0.0001f, $"expected {expected}, got {actual}");
    }
}
