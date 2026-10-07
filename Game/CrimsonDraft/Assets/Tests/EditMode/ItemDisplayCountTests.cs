#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class ItemDisplayCountTests
    {
        [Test]
        public void StackableConsumable_showsItsQuantity()
        {
            var tape = new ConsumableItem(Consumable(heal: 0, stackable: true, maxStack: 5, id: "ticker_tape"));
            tape.AddQuantity(2);

            Assert.AreEqual(3, ItemDisplayCount.For(tape));
        }

        [Test]
        public void NonStackableConsumable_showsNoCount()
        {
            var heal = new ConsumableItem(Consumable(stackable: false));

            Assert.IsNull(ItemDisplayCount.For(heal));
        }

        [Test]
        public void AmmoBox_showsItsRounds()
        {
            var box = new AmmoBoxItem(Ammo(defaultQuantity: 30), 12);

            Assert.AreEqual(12, ItemDisplayCount.For(box));
        }
    }
}
