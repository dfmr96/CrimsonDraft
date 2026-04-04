#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryServiceTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;
            public bool IsInitialized => true;
            public int  Count         => this.slots.Length;
            public OperatorRuntime this[int i] => this.slots[i];

            public FakeRoster(params OperatorRuntime[] slots) => this.slots = slots;

            public IReadOnlyList<int> GetAliveSlots()
            {
                var alive = new List<int>();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) alive.Add(i);
                return alive;
            }

            public void EnsureInitialized() { }
        }

        private static OperatorRuntime MakeAlive(int slot) =>
            new OperatorRuntime(slot, null, isPresent: true, maxHp: 100);

        private static WeaponData MakeWeaponData(string caliber = "9mm", int magazineCapacity = 6)
        {
            var d  = ScriptableObject.CreateInstance<WeaponData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue        = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue   = "Test Weapon";
            so.FindProperty("caliber").stringValue       = caliber;
            so.FindProperty("magazineCapacity").intValue = magazineCapacity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static AmmoBoxData MakeAmmoBoxData(string caliber = "9mm", int defaultQuantity = 30)
        {
            var d  = ScriptableObject.CreateInstance<AmmoBoxData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue       = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex  = (int)ItemType.AmmoBox;
            so.FindProperty("displayName").stringValue  = "Test Box";
            so.FindProperty("caliber").stringValue      = caliber;
            so.FindProperty("defaultQuantity").intValue = defaultQuantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // ── AddItem ────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_weapon_placesInFirstEmptySlotOfOperator()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            bool result = service.AddItem(MakeWeaponData(magazineCapacity: 30), operatorSlot: 0);

            Assert.IsTrue(result);
            Assert.IsFalse(service.Slots[0].IsEmpty);
            var item = service.Slots[0].Item as WeaponItem;
            Assert.IsNotNull(item);
            Assert.AreEqual(30, item!.CurrentAmmo);
        }

        [Test]
        public void AddItem_ammoBox_stacksIntoExistingSlot_whenSameItemExists()
        {
            var data    = MakeAmmoBoxData(defaultQuantity: 30);
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(data, operatorSlot: 0, quantity: 30);
            service.AddItem(data, operatorSlot: 0, quantity: 20);

            Assert.IsFalse(service.Slots[0].IsEmpty);
            Assert.IsTrue(service.Slots[1].IsEmpty, "no second slot used — stacked");
            var box = service.Slots[0].Item as AmmoBoxItem;
            Assert.AreEqual(50, box!.Quantity);
        }

        [Test]
        public void AddItem_returnsFalse_whenOperatorSlotsAreFull()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            bool result = service.AddItem(MakeWeaponData(), operatorSlot: 0);
            Assert.IsFalse(result);
        }

        [Test]
        public void AddItem_doesNotSpillToAnotherOperatorsSlots()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0), MakeAlive(1)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            bool result = service.AddItem(MakeWeaponData(), operatorSlot: 0);
            Assert.IsFalse(result, "op0 is full — should not spill");
            Assert.IsTrue(service.Slots[4].IsEmpty, "op1 slot 0 untouched");
        }

        // ── RemoveItem / MoveItem ──────────────────────────────────────────────

        [Test]
        public void RemoveItem_clearsSlot()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            service.RemoveItem(0);

            Assert.IsTrue(service.Slots[0].IsEmpty);
        }

        [Test]
        public void MoveItem_movesItemToEmptySlot()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            var original = service.Slots[0].Item;

            service.MoveItem(0, 2);

            Assert.IsTrue(service.Slots[0].IsEmpty);
            Assert.AreEqual(original, service.Slots[2].Item);
        }

        [Test]
        public void MoveItem_swapsWhenBothOccupied()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            var item0 = service.Slots[0].Item;
            var item1 = service.Slots[1].Item;

            service.MoveItem(0, 1);

            Assert.AreEqual(item1, service.Slots[0].Item);
            Assert.AreEqual(item0, service.Slots[1].Item);
        }

        // ── EquipWeapon / UnequipWeapon ────────────────────────────────────────

        [Test]
        public void EquipWeapon_setsEquippedBySlotAndUpdatesRoster()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Slots[0].Item!.EquippedBySlot);
            Assert.IsNotNull(op.EquippedWeapon);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameOperator()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            service.EquipWeapon(0, operatorSlot: 0);
            service.EquipWeapon(1, operatorSlot: 0);

            Assert.AreEqual(-1, service.Slots[0].Item!.EquippedBySlot, "old weapon unequipped");
            Assert.AreEqual( 0, service.Slots[1].Item!.EquippedBySlot, "new weapon equipped");
            Assert.AreEqual(service.Slots[1].Item as IWeaponSlot, op.EquippedWeapon);
        }

        [Test]
        public void UnequipWeapon_clearsSlotAndNullsRosterWeapon()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);

            service.UnequipWeapon(0);

            Assert.AreEqual(-1, service.Slots[0].Item!.EquippedBySlot);
            Assert.IsNull(op.EquippedWeapon);
        }

        [Test]
        public void WeaponAmmo_persistsWhenMovedToAnotherOperator()
        {
            var op0     = MakeAlive(0);
            var op1     = MakeAlive(1);
            var service = new InventoryService(new FakeRoster(op0, op1));
            service.AddItem(MakeWeaponData(magazineCapacity: 30), operatorSlot: 0);

            service.EquipWeapon(0, operatorSlot: 0);
            op0.EquippedWeapon!.SetAmmo(10);

            service.UnequipWeapon(0);
            service.MoveItem(0, 4); // move to op1's first slot (index 4)
            service.EquipWeapon(4, operatorSlot: 1);

            Assert.AreEqual(10, op1.EquippedWeapon!.CurrentAmmo, "ammo stays on weapon item");
        }

        // ── CanReload / ReloadOperator ─────────────────────────────────────────

        [Test]
        public void CanReload_returnsFalse_whenNoWeaponEquipped()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0);

            Assert.IsFalse(service.CanReload(0, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenCaliberMismatch()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("5.56", 30), operatorSlot: 0);
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsTrue_whenCaliberMatchAndNotFull()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30), operatorSlot: 0);
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10);

            Assert.IsTrue(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void ReloadOperator_fillsWeapon_andDeductsFromBox()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30), operatorSlot: 0);
            service.AddItem(MakeAmmoBoxData("9mm", defaultQuantity: 99), operatorSlot: 0, quantity: 99);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10);

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(30, op.EquippedWeapon.CurrentAmmo, "weapon is full");
            Assert.IsFalse(service.Slots[1].IsEmpty, "box slot still occupied");
            var box = service.Slots[1].Item as AmmoBoxItem;
            Assert.AreEqual(79, box!.Quantity, "box deducted 20 rounds");
        }

        [Test]
        public void ReloadOperator_clearsSlot_whenBoxExhausted()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30), operatorSlot: 0);
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0, quantity: 5);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(0);

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(5, op.EquippedWeapon.CurrentAmmo);
            Assert.IsTrue(service.Slots[1].IsEmpty, "slot cleared after box exhausted");
        }

        // ── GetEquippedWeaponIndex ─────────────────────────────────────────────

        [Test]
        public void GetEquippedWeaponIndex_returnsSlotIndex()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.GetEquippedWeaponIndex(0));
        }

        [Test]
        public void GetEquippedWeaponIndex_returnsNegativeOne_whenNoneEquipped()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            Assert.AreEqual(-1, service.GetEquippedWeaponIndex(0));
        }
    }
}
