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
            so.FindProperty("itemId").stringValue          = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex     = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue     = "Test Weapon";
            so.FindProperty("caliber").stringValue         = caliber;
            so.FindProperty("magazineCapacity").intValue   = magazineCapacity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static AmmoBoxData MakeAmmoBoxData(string caliber = "9mm", int defaultQuantity = 30)
        {
            var d  = ScriptableObject.CreateInstance<AmmoBoxData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue          = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex     = (int)ItemType.AmmoBox;
            so.FindProperty("displayName").stringValue     = "Test Box";
            so.FindProperty("caliber").stringValue         = caliber;
            so.FindProperty("defaultQuantity").intValue    = defaultQuantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_weapon_createsWeaponItemWithFullAmmo()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(magazineCapacity: 30));

            Assert.AreEqual(1, service.Items.Count);
            var item = service.Items[0] as WeaponItem;
            Assert.IsNotNull(item);
            Assert.AreEqual(30, item!.CurrentAmmo);
            Assert.AreEqual(30, item.MaxAmmo);
        }

        [Test]
        public void AddItem_ammoBox_usesProvidedQuantity()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData(defaultQuantity: 30), quantity: 99);

            var item = service.Items[0] as AmmoBoxItem;
            Assert.IsNotNull(item);
            Assert.AreEqual(99, item!.Quantity);
        }

        [Test]
        public void AddItem_ammoBox_usesDefaultQuantity_whenZero()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData(defaultQuantity: 30), quantity: 0);

            var item = service.Items[0] as AmmoBoxItem;
            Assert.AreEqual(30, item!.Quantity);
        }

        [Test]
        public void EquipWeapon_setsEquippedBySlotAndUpdatesRoster()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Items[0].EquippedBySlot);
            Assert.IsNotNull(op.EquippedWeapon);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameSlot()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());
            service.AddItem(MakeWeaponData());

            service.EquipWeapon(0, operatorSlot: 0);
            service.EquipWeapon(1, operatorSlot: 0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot, "old weapon unequipped");
            Assert.AreEqual( 0, service.Items[1].EquippedBySlot, "new weapon equipped");
            Assert.AreEqual(service.Items[1] as IWeaponSlot, op.EquippedWeapon);
        }

        [Test]
        public void UnequipWeapon_clearsSlotAndNullsRosterWeapon()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());
            service.EquipWeapon(0, operatorSlot: 0);

            service.UnequipWeapon(0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot);
            Assert.IsNull(op.EquippedWeapon);
        }

        [Test]
        public void WeaponAmmo_persistsWhenTransferredBetweenOperators()
        {
            var op0     = MakeAlive(0);
            var op1     = MakeAlive(1);
            var service = new InventoryService(new FakeRoster(op0, op1));
            service.AddItem(MakeWeaponData(magazineCapacity: 30));

            service.EquipWeapon(0, operatorSlot: 0);
            op0.EquippedWeapon!.SetAmmo(10);
            Assert.AreEqual(10, op0.EquippedWeapon.CurrentAmmo);

            service.UnequipWeapon(0);
            service.EquipWeapon(0, operatorSlot: 1);

            Assert.AreEqual(10, op1.EquippedWeapon!.CurrentAmmo, "ammo stays on weapon item");
        }

        [Test]
        public void CanReload_returnsFalse_whenNoWeaponEquipped()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"));

            Assert.IsFalse(service.CanReload(0, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenCaliberMismatch()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("5.56", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsTrue_whenCaliberMatchAndNotFull()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10);

            Assert.IsTrue(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenWeaponFull()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void ReloadOperator_fillsWeapon_andDeductsFromBox()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm", defaultQuantity: 99), quantity: 99);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10); // 20 rounds spent

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(30, op.EquippedWeapon.CurrentAmmo, "weapon is full");
            Assert.AreEqual(2,  service.Items.Count,           "box still in inventory");
            var box = service.Items[1] as AmmoBoxItem;
            Assert.AreEqual(79, box!.Quantity, "box deducted 20 rounds");
        }

        [Test]
        public void ReloadOperator_removesBox_whenExhausted()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"), quantity: 5);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(0); // empty

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(5, op.EquippedWeapon.CurrentAmmo, "weapon gets 5 rounds");
            Assert.AreEqual(1, service.Items.Count,            "box removed");
        }

        [Test]
        public void ReloadOperator_noOp_whenCannotReload()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"), quantity: 30);

            service.ReloadOperator(0, operatorSlot: 0);

            Assert.AreEqual(1, service.Items.Count, "box not consumed");
        }
    }
}
