#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class NoteRegistryTests
    {
        [Test]
        public void IsCollected_unknownId_returnsFalse()
        {
            var registry = new NoteRegistry();
            Assert.IsFalse(registry.IsCollected("note-a"));
        }

        [Test]
        public void SetCollected_thenIsCollected_returnsTrue()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");

            Assert.IsTrue(registry.IsCollected("note-a"));
        }

        [Test]
        public void SetCollected_doesNotAffectOtherNotes()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");

            Assert.IsFalse(registry.IsCollected("note-b"));
        }

        [Test]
        public void SetCollected_calledTwice_isIdempotent()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");
            registry.SetCollected("note-a");

            Assert.AreEqual(1, registry.CollectedIds.Count);
        }

        [Test]
        public void CollectedIds_reflectsAllCollectedNotes()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");
            registry.SetCollected("note-b");

            Assert.AreEqual(2, registry.CollectedIds.Count);
            CollectionAssert.Contains(registry.CollectedIds, "note-a");
            CollectionAssert.Contains(registry.CollectedIds, "note-b");
        }
    }
}
