#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    // A script reload in the editor round-trips the asset's private strings through Unity's serializer,
    // which turns null into "". An empty pending value must read as nothing pending.
    public sealed class SceneEntryContextTests
    {
        private SceneEntryContext context = null!;

        [SetUp]
        public void SetUp() => this.context = ScriptableObject.CreateInstance<SceneEntryContext>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.context);

        [Test]
        public void EmptyPendingEntry_isNone()
        {
            this.context.SetPendingEntry("");
            Assert.IsNull(this.context.Consume());
        }

        [Test]
        public void EmptyPendingRoom_isNone()
        {
            this.context.SetPendingRoom("");
            Assert.IsNull(this.context.ConsumeRoom());
        }

        [Test]
        public void PendingValues_areReturnedOnce()
        {
            this.context.SetPendingEntry("TR_01");
            this.context.SetPendingRoom("Deck_C/OFFICE");

            Assert.AreEqual("TR_01", this.context.Consume());
            Assert.AreEqual("Deck_C/OFFICE", this.context.ConsumeRoom());
            Assert.IsNull(this.context.Consume());
            Assert.IsNull(this.context.ConsumeRoom());
        }
    }
}
