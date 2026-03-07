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

        private static OperatorRuntime MakeAlive(int slot, int maxAmmo = 6, int spentAmmo = 0)
        {
            var op = new OperatorRuntime(slot, null, isPresent: true, maxHp: 100, maxAmmo: maxAmmo);
            op.ConsumeAmmo(spentAmmo);
            return op;
        }

        private static ItemData MakeItem(ItemType type = ItemType.Weapon, string caliber = "")
        {
            var d = ScriptableObject.CreateInstance<ItemData>();
            // Use SerializedObject to set private fields in EditMode tests
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex = (int)type;
            so.FindProperty("displayName").stringValue = type.ToString();
            so.FindProperty("caliber").stringValue     = caliber;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_increasesItemCount()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));

            service.AddItem(MakeItem());

            Assert.AreEqual(1, service.Items.Count);
        }

        [Test]
        public void EquipWeapon_setsEquippedBySlot()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeItem(ItemType.Weapon));

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Items[0].EquippedBySlot);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameSlot()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeItem(ItemType.Weapon));
            service.AddItem(MakeItem(ItemType.Weapon));

            service.EquipWeapon(0, operatorSlot: 0);
            service.EquipWeapon(1, operatorSlot: 0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot, "old weapon should be unequipped");
            Assert.AreEqual(0,  service.Items[1].EquippedBySlot, "new weapon should be equipped");
        }

        [Test]
        public void UnequipWeapon_setsSlotToMinusOne()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeItem(ItemType.Weapon));
            service.EquipWeapon(0, operatorSlot: 0);

            service.UnequipWeapon(0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot);
        }

        [Test]
        public void GetEquippedWeaponIndex_returnsCorrectIndex()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0), MakeAlive(1)));
            service.AddItem(MakeItem(ItemType.Weapon));

            service.EquipWeapon(0, operatorSlot: 1);

            Assert.AreEqual( 0, service.GetEquippedWeaponIndex(operatorSlot: 1));
            Assert.AreEqual(-1, service.GetEquippedWeaponIndex(operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenNoWeaponEquipped()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0, maxAmmo: 6, spentAmmo: 3)));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm"));

            bool result = service.CanReload(0, operatorSlot: 0);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanReload_returnsFalse_whenCaliberMismatch()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0, maxAmmo: 6, spentAmmo: 3)));
            service.AddItem(MakeItem(ItemType.Weapon, caliber: "5.56"));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            bool result = service.CanReload(1, operatorSlot: 0);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanReload_returnsTrue_whenCaliberMatchAndAmmoNotFull()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0, maxAmmo: 6, spentAmmo: 3)));
            service.AddItem(MakeItem(ItemType.Weapon, caliber: "9mm"));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            bool result = service.CanReload(1, operatorSlot: 0);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanReload_returnsFalse_whenAmmoAlreadyFull()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0, maxAmmo: 6, spentAmmo: 0)));
            service.AddItem(MakeItem(ItemType.Weapon, caliber: "9mm"));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            bool result = service.CanReload(1, operatorSlot: 0);

            Assert.IsFalse(result);
        }

        [Test]
        public void ReloadOperator_consumesBox_andRestoresAmmo()
        {
            var op      = MakeAlive(0, maxAmmo: 6, spentAmmo: 4);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeItem(ItemType.Weapon, caliber: "9mm"));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(1, service.Items.Count, "ammo box should be consumed");
            Assert.AreEqual(6, op.Ammo, "operator ammo should be restored to max");
        }

        [Test]
        public void ReloadOperator_noOp_whenCannotReload()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0, maxAmmo: 6, spentAmmo: 3)));
            service.AddItem(MakeItem(ItemType.AmmoBox, caliber: "9mm")); // no weapon equipped

            service.ReloadOperator(0, operatorSlot: 0);

            Assert.AreEqual(1, service.Items.Count, "box should NOT be consumed");
        }
    }
}
