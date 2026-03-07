#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster     roster;
        private readonly List<InventoryItem> items = new();

        [Preserve]
        public InventoryService(IOperatorRoster roster) => this.roster = roster;

        public IReadOnlyList<InventoryItem> Items => this.items;

        public void AddItem(ItemData data, int quantity = 0)
        {
            InventoryItem item = data switch
            {
                WeaponData     wd => new WeaponItem(wd),
                AmmoBoxData    ad => new AmmoBoxItem(ad, quantity),
                ConsumableData cd => new ConsumableItem(cd),
                _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}")
            };
            this.items.Add(item);
        }

        public void EquipWeapon(int itemIndex, int operatorSlot)
        {
            // Unequip any weapon already on this slot
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                {
                    this.items[i].EquippedBySlot = -1;
                    this.roster[operatorSlot].SetEquippedWeapon(null);
                    break;
                }
            }
            this.items[itemIndex].EquippedBySlot = operatorSlot;
            this.roster[operatorSlot].SetEquippedWeapon(this.items[itemIndex] as IWeaponSlot);
        }

        public void UnequipWeapon(int itemIndex)
        {
            int slot = this.items[itemIndex].EquippedBySlot;
            this.items[itemIndex].EquippedBySlot = -1;
            if (slot >= 0)
                this.roster[slot].SetEquippedWeapon(null);
        }

        public int GetEquippedWeaponIndex(int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
                if (this.items[i].EquippedBySlot == operatorSlot)
                    return i;
            return -1;
        }

        public bool CanReload(int ammoBoxIndex, int operatorSlot)
        {
            if (this.items[ammoBoxIndex] is not AmmoBoxItem box)
                return false;

            var weapon = this.roster[operatorSlot].EquippedWeapon;
            if (weapon == null) return false;
            if (weapon.Caliber != box.Data.Caliber) return false;

            return this.roster[operatorSlot].IsAlive && weapon.CurrentAmmo < weapon.MaxAmmo;
        }

        public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
        {
            if (!CanReload(ammoBoxIndex, operatorSlot)) return;

            var box    = (AmmoBoxItem)this.items[ammoBoxIndex];
            var weapon = this.roster[operatorSlot].EquippedWeapon!;

            int needed = weapon.MaxAmmo - weapon.CurrentAmmo;
            int rounds = needed < box.Quantity ? needed : box.Quantity;
            weapon.SetAmmo(weapon.CurrentAmmo + rounds);
            box.Quantity -= rounds;

            if (box.Quantity <= 0)
                this.items.RemoveAt(ammoBoxIndex);
        }
    }
}
