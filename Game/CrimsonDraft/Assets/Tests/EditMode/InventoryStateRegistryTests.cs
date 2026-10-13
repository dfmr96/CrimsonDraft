#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryStateRegistryTests
    {
        private static List<InventoryItemEntry> OneEntry() =>
            new List<InventoryItemEntry> { new InventoryItemEntry { itemId = "med" } };

        [Test]
        public void HasSavedState_initially_isFalse()
        {
            Assert.IsFalse(new InventoryStateRegistry().HasSavedState);
        }

        [Test]
        public void Load_initially_returnsNull()
        {
            Assert.IsNull(new InventoryStateRegistry().Load());
        }

        [Test]
        public void Save_setsHasSavedState_toTrue()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(OneEntry());
            Assert.IsTrue(registry.HasSavedState);
        }

        [Test]
        public void Load_afterSave_returnsACopyOfTheEntries()
        {
            var registry = new InventoryStateRegistry();
            var entries  = OneEntry();
            registry.Save(entries);
            entries.Clear();

            Assert.AreEqual(1, registry.Load()!.Count);
            Assert.AreEqual("med", registry.Load()![0].itemId);
        }

        [Test]
        public void ClearAll_removesSavedState()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(OneEntry());
            registry.ClearAll();
            Assert.IsFalse(registry.HasSavedState);
        }
    }
}
