#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class DebugItemCatalogTests
    {
        [Test]
        public void Step_forwardFromStart_returnsFirstItem_thenWraps()
        {
            var a = Consumable(id: "a");
            var b = Key(id: "b");
            var catalog = new DebugItemCatalog(Database(a, b).AllItems);

            Assert.AreSame(a, catalog.Step(1));
            Assert.AreSame(b, catalog.Step(1));
            Assert.AreSame(a, catalog.Step(1));
        }

        [Test]
        public void Step_backwardFromStart_returnsLastItem_thenWraps()
        {
            var a = Consumable(id: "a");
            var b = Key(id: "b");
            var catalog = new DebugItemCatalog(Database(a, b).AllItems);

            Assert.AreSame(b, catalog.Step(-1));
            Assert.AreSame(a, catalog.Step(-1));
            Assert.AreSame(b, catalog.Step(-1));
        }

        [Test]
        public void Reset_startsOverFromTheFirstItem()
        {
            var a = Consumable(id: "a");
            var b = Key(id: "b");
            var catalog = new DebugItemCatalog(Database(a, b).AllItems);
            catalog.Step(1);
            catalog.Step(1);

            catalog.Reset();

            Assert.AreSame(a, catalog.Step(1));
        }

        [Test]
        public void Step_emptyCatalog_returnsNull()
        {
            var catalog = new DebugItemCatalog(Database().AllItems);
            Assert.IsNull(catalog.Step(1));
        }

        [Test]
        public void AllItems_skipsEmptyEntries()
        {
            var a = Consumable(id: "a");
            CollectionAssert.AreEqual(new[] { a }, Database(a, null!).AllItems);
        }

        [Test]
        public void CreateFull_weapon_hasFullMagazine()
        {
            var weapon = (WeaponItem)DebugItemCatalog.CreateFull(Weapon(magazine: 6));
            Assert.AreEqual(6, weapon.CurrentAmmo);
        }

        [Test]
        public void CreateFull_ammoBox_isFilledToMaxStack()
        {
            var ammo = DebugItemCatalog.CreateFull(Ammo(defaultQuantity: 15, maxStack: 60));
            Assert.AreEqual(60, ammo.Quantity);
        }

        [Test]
        public void CreateFull_ammoBoxWithoutMaxStack_usesDefaultQuantity()
        {
            var ammo = DebugItemCatalog.CreateFull(Ammo(defaultQuantity: 15, maxStack: 0));
            Assert.AreEqual(15, ammo.Quantity);
        }

        [Test]
        public void CreateFull_stackableConsumable_isFilledToMaxStack()
        {
            var stack = DebugItemCatalog.CreateFull(Consumable(stackable: true, maxStack: 5));
            Assert.AreEqual(5, stack.Quantity);
        }
    }
}
