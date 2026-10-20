#nullable enable

using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Pushables;

namespace CrimsonDraft.Tests
{
    // PushableObject position persistence: back to the home point on every room entry, unless
    // SavePosition() recorded a position for its id.
    public sealed class PushablePersistenceTests
    {
        private GameObject box  = null!;
        private GameObject home = null!;
        private PushableStateRegistry registry = null!;

        private PushableObject Pushable => this.box.GetComponent<PushableObject>();

        [SetUp]
        public void SetUp()
        {
            this.box = new GameObject("Crate", typeof(BoxCollider), typeof(PushableObject));
            this.box.transform.position = new Vector3(1f, 0f, 0f);
            this.home = new GameObject("CrateHome");
            this.home.transform.position = new Vector3(2f, 0f, 3f);
            this.registry = new PushableStateRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.box);
            Object.DestroyImmediate(this.home);
        }

        private void Configure(string id, Transform? homePoint)
        {
            var so = new SerializedObject(Pushable);
            so.FindProperty("pushableId").stringValue          = id;
            so.FindProperty("homePoint").objectReferenceValue  = homePoint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ResetPosition_noSavedPosition_goesToHomePoint()
        {
            Configure("crate-1", this.home.transform);
            Pushable.Construct(this.registry);
            this.box.transform.position = new Vector3(9f, 0f, 9f);

            Pushable.ResetPosition();

            Assert.AreEqual(new Vector3(2f, 0f, 3f), this.box.transform.position);
        }

        [Test]
        public void ResetPosition_withoutHomePoint_goesToStartPosition()
        {
            Configure("crate-1", null);
            Pushable.Construct(this.registry);
            this.box.transform.position = new Vector3(9f, 0f, 9f);

            Pushable.ResetPosition();

            Assert.AreEqual(new Vector3(1f, 0f, 0f), this.box.transform.position);
        }

        [Test]
        public void SavePosition_thenReset_staysAtSavedPosition()
        {
            Configure("crate-1", this.home.transform);
            Pushable.Construct(this.registry);
            this.box.transform.position = new Vector3(5f, 0f, 5f);

            Pushable.SavePosition();
            this.box.transform.position = new Vector3(9f, 0f, 9f);
            Pushable.ResetPosition();

            Assert.AreEqual(new Vector3(5f, 0f, 5f), this.box.transform.position);
        }

        [Test]
        public void Construct_withPositionInRegistry_movesThere()
        {
            Configure("crate-1", this.home.transform);
            this.registry.Record("crate-1", new Vector3(7f, 0f, 1f));

            Pushable.Construct(this.registry);

            Assert.AreEqual(new Vector3(7f, 0f, 1f), this.box.transform.position);
        }

        [Test]
        public void SavePosition_withoutId_recordsNothing()
        {
            Configure("", this.home.transform);
            Pushable.Construct(this.registry);

            LogAssert.Expect(LogType.Warning, new Regex("needs a pushableId"));
            Pushable.SavePosition();

            Assert.AreEqual(0, this.registry.GetState().Count);
        }
    }
}
