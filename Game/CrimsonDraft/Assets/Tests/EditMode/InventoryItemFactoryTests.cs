#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryItemFactoryTests
    {
        [Test]
        public void Create_mapsEachDataTypeToItsItemType()
        {
            Assert.IsInstanceOf<WeaponItem>(InventoryItemFactory.Create(Weapon()));
            Assert.IsInstanceOf<AmmoBoxItem>(InventoryItemFactory.Create(Ammo()));
            Assert.IsInstanceOf<ConsumableItem>(InventoryItemFactory.Create(Consumable()));
            Assert.IsInstanceOf<KeyItem>(InventoryItemFactory.Create(Key()));
        }

        [Test]
        public void Create_ammo_defaultsToDefaultQuantity()
        {
            Assert.AreEqual(30, InventoryItemFactory.Create(Ammo(defaultQuantity: 30)).Quantity);
            Assert.AreEqual(7,  InventoryItemFactory.Create(Ammo(defaultQuantity: 30), 7).Quantity);
        }

        [Test]
        public void Create_stackableConsumable_usesQuantity_cappedAtMaxStack()
        {
            Assert.AreEqual(3, InventoryItemFactory.Create(Consumable(stackable: true, maxStack: 5), 3).Quantity);
            Assert.AreEqual(5, InventoryItemFactory.Create(Consumable(stackable: true, maxStack: 5), 9).Quantity);
        }

        [Test]
        public void Create_nonStackable_ignoresQuantity()
        {
            Assert.AreEqual(1, InventoryItemFactory.Create(Consumable(), 4).Quantity);
        }

        [Test]
        public void Create_unknownSubtype_throws()
        {
            var plain = ScriptableObject.CreateInstance<ItemData>();
            Assert.Throws<ArgumentException>(() => InventoryItemFactory.Create(plain));
        }

        [Test]
        public void AddQuantity_clampsToMaxStack_andIgnoresNonStackable()
        {
            var ammo = InventoryItemFactory.Create(Ammo(defaultQuantity: 10, maxStack: 12));
            ammo.AddQuantity(5);
            Assert.AreEqual(12, ammo.Quantity);

            var single = InventoryItemFactory.Create(Consumable());
            single.AddQuantity(3);
            Assert.AreEqual(1, single.Quantity);
        }

        [Test]
        public void RestoreUses_setsUsesRemaining_clamped()
        {
            var key = (KeyItem)InventoryItemFactory.Create(Key(maxUses: 3));
            key.RestoreUses(1);
            Assert.AreEqual(1, key.UsesRemaining);
            key.RestoreUses(9);
            Assert.AreEqual(3, key.UsesRemaining);
        }
    }
}
