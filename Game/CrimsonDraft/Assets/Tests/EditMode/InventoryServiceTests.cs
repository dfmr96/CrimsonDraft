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

        private sealed class NullCombineService : ICombineService
        {
            public ItemData? TryGetResult(ItemData a, ItemData b) => null;
        }

        private sealed class FakeCombineService : ICombineService
        {
            private readonly ItemData inputA;
            private readonly ItemData inputB;
            private readonly ItemData output;

            public FakeCombineService(ItemData inputA, ItemData inputB, ItemData output)
            {
                this.inputA = inputA;
                this.inputB = inputB;
                this.output = output;
            }

            public ItemData? TryGetResult(ItemData a, ItemData b)
            {
                bool match = (a == this.inputA && b == this.inputB)
                          || (a == this.inputB && b == this.inputA);
                return match ? this.output : null;
            }
        }

        private static InventoryService MakeService(IOperatorRoster roster, ICombineService? combine = null) =>
            new InventoryService(roster, combine ?? new NullCombineService());

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

        private static ConsumableData MakeConsumableData(string? id = null)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id ?? System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = "Test Consumable";
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // ── AddItem ────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_weapon_placesInFirstEmptySlotOfOperator()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
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
            var service = MakeService(new FakeRoster(MakeAlive(0)));
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
            var service = MakeService(new FakeRoster(MakeAlive(0)));
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
            var service = MakeService(new FakeRoster(MakeAlive(0), MakeAlive(1)));
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
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            service.RemoveItem(0);

            Assert.IsTrue(service.Slots[0].IsEmpty);
        }

        [Test]
        public void MoveItem_movesItemToEmptySlot()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            var original = service.Slots[0].Item;

            service.MoveItem(0, 2);

            Assert.IsTrue(service.Slots[0].IsEmpty);
            Assert.AreEqual(original, service.Slots[2].Item);
        }

        [Test]
        public void MoveItem_swapsWhenBothOccupied()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
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
            var service = MakeService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Slots[0].Item!.EquippedBySlot);
            Assert.IsNotNull(op.EquippedWeapon);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameOperator()
        {
            var op      = MakeAlive(0);
            var service = MakeService(new FakeRoster(op));
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
            var service = MakeService(new FakeRoster(op));
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
            var service = MakeService(new FakeRoster(op0, op1));
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
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0);

            Assert.IsFalse(service.CanReload(0, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenCaliberMismatch()
        {
            var op      = MakeAlive(0);
            var service = MakeService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("5.56", 30), operatorSlot: 0);
            service.AddItem(MakeAmmoBoxData("9mm"), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsTrue_whenCaliberMatchAndNotFull()
        {
            var op      = MakeAlive(0);
            var service = MakeService(new FakeRoster(op));
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
            var service = MakeService(new FakeRoster(op));
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
            var service = MakeService(new FakeRoster(op));
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
            var service = MakeService(new FakeRoster(op));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.GetEquippedWeaponIndex(0));
        }

        [Test]
        public void GetEquippedWeaponIndex_returnsNegativeOne_whenNoneEquipped()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            Assert.AreEqual(-1, service.GetEquippedWeaponIndex(0));
        }

        // ── TryCombine ────────────────────────────────────────────────────────

        [Test]
        public void TryCombine_returnsTrue_consumesBothInputs_addsResult()
        {
            var itemA   = MakeConsumableData("key");
            var itemB   = MakeConsumableData("portfolio");
            var output  = MakeConsumableData("documents");
            var service = MakeService(new FakeRoster(MakeAlive(0)), new FakeCombineService(itemA, itemB, output));
            service.AddItem(itemA, operatorSlot: 0);
            service.AddItem(itemB, operatorSlot: 0);

            bool result = service.TryCombine(0, 1);

            // AddItemAuto fills slot 0 first (freed by RemoveItem before result is placed)
            Assert.IsTrue(result);
            Assert.IsFalse(service.Slots[0].IsEmpty,  "result occupies first freed slot (slot 0)");
            Assert.IsTrue(service.Slots[1].IsEmpty,   "slotB consumed and empty");
            Assert.AreEqual(output.ItemId, service.Slots[0].Item!.Data.ItemId);
        }

        [Test]
        public void TryCombine_returnsFalse_whenNoRecipeExists()
        {
            var itemA   = MakeConsumableData("key");
            var itemB   = MakeConsumableData("other");
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            service.AddItem(itemA, operatorSlot: 0);
            service.AddItem(itemB, operatorSlot: 0);

            bool result = service.TryCombine(0, 1);

            Assert.IsFalse(result);
            Assert.IsFalse(service.Slots[0].IsEmpty, "slotA untouched");
            Assert.IsFalse(service.Slots[1].IsEmpty, "slotB untouched");
        }

        [Test]
        public void TryCombine_isSymmetric_worksInBothOrders()
        {
            var itemA   = MakeConsumableData("key");
            var itemB   = MakeConsumableData("portfolio");
            var output  = MakeConsumableData("documents");
            var combine = new FakeCombineService(itemA, itemB, output);

            var s1 = MakeService(new FakeRoster(MakeAlive(0)), combine);
            s1.AddItem(itemA, operatorSlot: 0);
            s1.AddItem(itemB, operatorSlot: 0);
            Assert.IsTrue(s1.TryCombine(0, 1), "A+B");

            var s2 = MakeService(new FakeRoster(MakeAlive(0)), combine);
            s2.AddItem(itemB, operatorSlot: 0);
            s2.AddItem(itemA, operatorSlot: 0);
            Assert.IsTrue(s2.TryCombine(0, 1), "B+A");
        }

        [Test]
        public void TryCombine_returnsFalse_whenEitherSlotIsEmpty()
        {
            var itemA   = MakeConsumableData("key");
            var itemB   = MakeConsumableData("portfolio");
            var output  = MakeConsumableData("documents");
            var service = MakeService(new FakeRoster(MakeAlive(0)), new FakeCombineService(itemA, itemB, output));
            service.AddItem(itemA, operatorSlot: 0);
            // slot 1 is empty

            bool result = service.TryCombine(0, 1);

            Assert.IsFalse(result);
            Assert.IsFalse(service.Slots[0].IsEmpty, "slotA untouched");
        }

        [Test]
        public void TryCombine_placesResult_inFirstAvailableSlotAcrossOperators()
        {
            // op0 fully occupied with fillers — result must spill to op1
            var itemA  = MakeConsumableData("key");
            var itemB  = MakeConsumableData("portfolio");
            var filler = MakeConsumableData("filler");
            var output = MakeConsumableData("documents");
            var svc    = MakeService(new FakeRoster(MakeAlive(0), MakeAlive(1)), new FakeCombineService(itemA, itemB, output));
            svc.AddItem(filler, operatorSlot: 0); // slot 0
            svc.AddItem(filler, operatorSlot: 0); // slot 1
            svc.AddItem(filler, operatorSlot: 0); // slot 2
            svc.AddItem(filler, operatorSlot: 0); // slot 3 — op0 full
            svc.AddItem(itemA,  operatorSlot: 1); // slot 4
            svc.AddItem(itemB,  operatorSlot: 1); // slot 5

            svc.TryCombine(4, 5);

            // op0 full → AddItemAuto falls to op1 → result in slot 4 (first freed slot in op1)
            Assert.IsFalse(svc.Slots[4].IsEmpty, "result in op1 slot 0 (index 4)");
            Assert.AreEqual(output.ItemId, svc.Slots[4].Item!.Data.ItemId);
            Assert.IsTrue(svc.Slots[5].IsEmpty, "slotB consumed");
        }
    }
}
