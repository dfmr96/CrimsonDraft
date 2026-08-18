#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class NoteRegistryTests
    {
        [Test]
        public void LoadState_marksGivenIdsAsCollected()
        {
            var registry = new NoteRegistry();
            registry.LoadState(new List<string> { "note-a" });

            Assert.IsTrue(registry.IsCollected("note-a"));
        }

        [Test]
        public void ClearAll_removesAllCollectedIds()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsCollected("note-a"));
        }
    }
}
