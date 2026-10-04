#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        [Test]
        public void TryCombine_sameStackable_mergesUpToMaxStack()
        {
            var s      = Service();
            var data   = Ammo(defaultQuantity: 20, maxStack: 30);
            var source = AddAt(s, data, 0);
            var target = AddAt(s, data, 1);
            target.Quantity = 15;

            Assert.IsTrue(s.TryCombine(source, target, out var result));
            Assert.AreSame(target, result);
            Assert.AreEqual(30, target.Quantity);
            Assert.AreEqual(5, source.Quantity);
        }

        [Test]
        public void TryCombine_mergeEmptyingSource_removesIt()
        {
            var s      = Service();
            var data   = Ammo(defaultQuantity: 5, maxStack: 30);
            var source = AddAt(s, data, 0);
            var target = AddAt(s, data, 1);

            Assert.IsTrue(s.TryCombine(source, target, out _));
            Assert.AreEqual(10, target.Quantity);
            Assert.AreEqual(0, Op(s, 0).Count);
        }

        [Test]
        public void TryCombine_mergeIntoFullStack_fails()
        {
            var s      = Service();
            var data   = Ammo(defaultQuantity: 30, maxStack: 30);
            var source = AddAt(s, data, 0);
            var target = AddAt(s, data, 1);

            Assert.IsFalse(s.TryCombine(source, target, out _));
            Assert.AreEqual(30, source.Quantity);
        }

        [Test]
        public void TryCombine_ammoAndWeapon_reloads_andRemovesEmptiedAmmo()
        {
            var s      = Service();
            var weapon = (WeaponItem)AddAt(s, Weapon(Caliber._9mm, magazine: 6), 0);
            var ammo   = AddAt(s, Ammo(Caliber._9mm, defaultQuantity: 4), 0);

            Assert.IsTrue(s.TryCombine(ammo, weapon, out var result));
            Assert.AreSame(weapon, result);
            Assert.AreEqual(4, weapon.CurrentAmmo);
            Assert.IsFalse(Op(s, 0).Contains(ammo));
        }

        [Test]
        public void TryCombine_ammoAndWeapon_wrongCaliber_fails()
        {
            var s      = Service();
            var weapon = AddAt(s, Weapon(Caliber._9mm), 0);
            var ammo   = AddAt(s, Ammo(Caliber.None), 0);
            Assert.IsFalse(s.TryCombine(ammo, weapon, out _));
        }

        [Test]
        public void TryCombine_recipe_placesResultInFirstInputsContainer()
        {
            var a       = Consumable();
            var b       = Consumable();
            var product = Consumable(id: "product");
            var s       = Service(combine: new FakeCombineService(a, b, product));
            var itemA   = AddAt(s, a, 1);
            var itemB   = AddAt(s, b, 0);

            Assert.IsTrue(s.TryCombine(itemA, itemB, out var result));
            Assert.AreEqual("product", result!.Data.ItemId);
            Assert.IsTrue(Op(s, 1).Contains(result));
            Assert.IsFalse(s.HasItem(a.ItemId));
            Assert.IsFalse(s.HasItem(b.ItemId));
        }

        [Test]
        public void TryCombine_recipeWithoutRoom_restoresInputs()
        {
            var a       = Consumable();
            var b       = Consumable();
            var product = Sized(Consumable(), 4, 4);
            var s       = Service(operators: 1, combine: new FakeCombineService(a, b, product));
            var itemA   = AddAt(s, a, 0);
            var itemB   = AddAt(s, b, 0);
            AddAt(s, Sized(Consumable(), 2, 4), 0);
            var originA = Op(s, 0).GetPlacement(itemA)!.Origin;
            var originB = Op(s, 0).GetPlacement(itemB)!.Origin;

            Assert.IsFalse(s.TryCombine(itemA, itemB, out _));
            Assert.AreEqual(originA, Op(s, 0).GetPlacement(itemA)!.Origin);
            Assert.AreEqual(originB, Op(s, 0).GetPlacement(itemB)!.Origin);
        }

        [Test]
        public void TryCombine_recipeWithEquippedInput_fails()
        {
            var a     = Weapon();
            var b     = Consumable();
            var s     = Service(combine: new FakeCombineService(a, b, Consumable()));
            var itemA = (WeaponItem)AddAt(s, a, 0);
            var itemB = AddAt(s, b, 0);
            s.Equip(itemA, 0);

            Assert.IsFalse(s.TryCombine(itemA, itemB, out _));
        }

        [Test]
        public void TryUseConsumable_healsOperator_andRemovesSingleItem()
        {
            var roster = FakeRoster.WithOperators(1);
            roster[0].ApplyDamage(30);
            var s    = Service(roster);
            var item = AddAt(s, Consumable(heal: 20), 0);

            Assert.IsTrue(s.TryUseConsumable(item, 0));
            Assert.AreEqual(90, roster[0].Hp);
            Assert.AreEqual(0, Op(s, 0).Count);
        }

        [Test]
        public void TryUseConsumable_stack_decrementsQuantity()
        {
            var s = Service();
            Assert.IsTrue(s.TryAdd(Consumable(stackable: true), ContainerId.Operator(0), quantity: 3));
            var item = Only(Op(s, 0));

            s.TryUseConsumable(item, 0);
            Assert.AreEqual(2, item.Quantity);
            Assert.IsTrue(Op(s, 0).Contains(item));
        }

        [Test]
        public void TryUseConsumable_nonConsumable_fails()
        {
            var s = Service();
            Assert.IsFalse(s.TryUseConsumable(AddAt(s, Weapon(), 0), 0));
        }

        [Test]
        public void Equip_setsRosterWeapon_andReplacesPreviousInSameSlot()
        {
            var roster = FakeRoster.WithOperators(1);
            var s      = Service(roster);
            var first  = (WeaponItem)AddAt(s, Weapon(), 0);
            var second = (WeaponItem)AddAt(s, Weapon(), 0);

            s.Equip(first, 0);
            s.Equip(second, 0);

            Assert.AreSame(second, roster[0].PrimaryWeapon);
            Assert.IsTrue(second.IsEquipped);
            Assert.IsFalse(first.IsEquipped);
            Assert.IsTrue(s.HasEquippedWeapon(0));
        }

        [Test]
        public void Unequip_andRemoveEquipped_clearRosterWeapon()
        {
            var roster = FakeRoster.WithOperators(1);
            var s      = Service(roster);
            var weapon = (WeaponItem)AddAt(s, Weapon(), 0);

            s.Equip(weapon, 0);
            s.Unequip(weapon);
            Assert.IsNull(roster[0].PrimaryWeapon);
            Assert.IsFalse(s.HasEquippedWeapon(0));

            s.Equip(weapon, 0);
            s.Remove(weapon);
            Assert.IsNull(roster[0].PrimaryWeapon);
        }

        [Test]
        public void TryReload_usesEquippedWeapon()
        {
            var s      = Service();
            var weapon = (WeaponItem)AddAt(s, Weapon(Caliber._9mm, magazine: 6), 0);
            var ammo   = AddAt(s, Ammo(Caliber._9mm, defaultQuantity: 10), 0);

            Assert.IsFalse(s.CanReload(ammo, 0));
            s.Equip(weapon, 0);
            Assert.IsTrue(s.CanReload(ammo, 0));
            Assert.IsTrue(s.TryReload(ammo, 0));
            Assert.AreEqual(6, weapon.CurrentAmmo);
            Assert.AreEqual(4, ammo.Quantity);
            Assert.IsFalse(s.CanReload(ammo, 0));
        }

        [Test]
        public void TryUseKey_reportsOutcomes_withItemReference()
        {
            var s   = Service();
            var key = (KeyItem)AddAt(s, Key(maxUses: 2, id: "door-key"), 0);

            var first = s.TryUseKey("door-key");
            Assert.AreEqual(KeyUseResult.Success, first.Result);
            Assert.AreSame(key, first.Item);

            Assert.AreEqual(KeyUseResult.DepletedAfterUse, s.TryUseKey("door-key").Result);
            Assert.AreEqual(KeyUseResult.AlreadyDepleted, s.TryUseKey("door-key").Result);
            Assert.AreEqual(KeyUseResult.NotFound, s.TryUseKey("other").Result);
            Assert.IsTrue(s.HasItem("door-key"));
        }
    }
}
