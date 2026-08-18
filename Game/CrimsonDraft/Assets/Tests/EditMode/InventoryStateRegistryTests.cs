#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory; // InventorySlot

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
            Assert.IsNull(registry.Load<InventorySlot[]>());
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
            Assert.AreSame(slots, registry.Load<InventorySlot[]>());
        }

        [Test]
        public void ClearAll_removesSavedState()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(new InventorySlot[4]);
            registry.ClearAll();

            Assert.IsFalse(registry.HasSavedState);
        }
    }
}
