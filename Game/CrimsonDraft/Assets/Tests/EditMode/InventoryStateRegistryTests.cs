#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryStateRegistryTests
    {
        [Test]
        public void HasSavedState_initially_isFalse()
        {
            var registry = new InventoryStateRegistry();
            Assert.IsFalse(registry.HasSavedState);
        }

        [Test]
        public void Load_initially_returnsNull()
        {
            var registry = new InventoryStateRegistry();
            Assert.IsNull(registry.Load());
        }

        [Test]
        public void Save_setsHasSavedState_toTrue()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(new InventorySlot[4]);
            Assert.IsTrue(registry.HasSavedState);
        }

        [Test]
        public void Load_afterSave_returnsSameArrayReference()
        {
            var registry = new InventoryStateRegistry();
            var slots = new InventorySlot[4];
            registry.Save(slots);
            Assert.AreSame(slots, registry.Load());
        }
    }
}
