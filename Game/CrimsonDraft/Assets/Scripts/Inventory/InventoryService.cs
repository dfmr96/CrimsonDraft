#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster roster;
        private InventorySlot[]? slots;

        [Preserve]
        public InventoryService(IOperatorRoster roster) => this.roster = roster;

        // Lazy-init: roster may not be initialized at construction time.
        private InventorySlot[] EnsureSlots()
        {
            if (this.slots != null) return this.slots;
            this.roster.EnsureInitialized();
            this.slots = new InventorySlot[this.roster.Count * 4];
            for (int i = 0; i < this.slots.Length; i++)
                this.slots[i] = new InventorySlot();
            return this.slots;
        }

        public IReadOnlyList<InventorySlot> Slots    => EnsureSlots();
        public int                          SlotCount => EnsureSlots().Length;

        public bool AddItem(ItemData data, int operatorSlot, int quantity = 0)
        {
            var s     = EnsureSlots();
            int start = operatorSlot * 4;

            // Try to stack into existing slot with same item
            if (data.Stackable)
            {
                for (int i = start; i < start + 4; i++)
                {
                    if (s[i].IsEmpty || s[i].Item!.Data.ItemId != data.ItemId) continue;

                    if (s[i].Item is AmmoBoxItem box)
                    {
                        int add = quantity > 0 ? quantity : ((AmmoBoxData)data).DefaultQuantity;
                        box.Quantity += add;
                    }
                    else
                    {
                        s[i].Quantity += quantity > 0 ? quantity : 1;
                    }
                    return true;
                }
            }

            // Place in first empty slot of this operator's block
            for (int i = start; i < start + 4; i++)
            {
                if (!s[i].IsEmpty) continue;

                InventoryItem item = data switch
                {
                    WeaponData     wd => new WeaponItem(wd),
                    AmmoBoxData    ad => new AmmoBoxItem(ad, quantity),
                    ConsumableData cd => new ConsumableItem(cd),
                    _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}")
                };
                s[i].Item     = item;
                s[i].Quantity = 1;
                return true;
            }

            return false; // operator's 4 slots are full
        }

        public void RemoveItem(int slotIndex)
        {
            var s             = EnsureSlots();
            s[slotIndex].Item     = null;
            s[slotIndex].Quantity = 0;
        }

        public void MoveItem(int fromSlot, int toSlot)
        {
            var s    = EnsureSlots();
            var item = s[fromSlot].Item;
            var qty  = s[fromSlot].Quantity;
            s[fromSlot].Item     = s[toSlot].Item;
            s[fromSlot].Quantity = s[toSlot].Quantity;
            s[toSlot].Item       = item;
            s[toSlot].Quantity   = qty;
        }

        public void EquipWeapon(int slotIndex, int operatorSlot)
        {
            var s = EnsureSlots();
            // Unequip any weapon already on this operator
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i].Item?.EquippedBySlot == operatorSlot)
                {
                    s[i].Item!.EquippedBySlot = -1;
                    this.roster[operatorSlot].SetEquippedWeapon(null);
                    break;
                }
            }
            s[slotIndex].Item!.EquippedBySlot = operatorSlot;
            this.roster[operatorSlot].SetEquippedWeapon(s[slotIndex].Item as IWeaponSlot);
        }

        public void UnequipWeapon(int slotIndex)
        {
            var s    = EnsureSlots();
            int slot = s[slotIndex].Item!.EquippedBySlot;
            s[slotIndex].Item!.EquippedBySlot = -1;
            if (slot >= 0)
                this.roster[slot].SetEquippedWeapon(null);
        }

        public int GetEquippedWeaponIndex(int operatorSlot)
        {
            var s = EnsureSlots();
            for (int i = 0; i < s.Length; i++)
                if (s[i].Item?.EquippedBySlot == operatorSlot)
                    return i;
            return -1;
        }

        public bool CanReload(int slotIndex, int operatorSlot)
        {
            var s = EnsureSlots();
            if (s[slotIndex].Item is not AmmoBoxItem box) return false;
            var weapon = this.roster[operatorSlot].EquippedWeapon;
            if (weapon == null) return false;
            if (weapon.Caliber != box.Data.Caliber) return false;
            return this.roster[operatorSlot].IsAlive && weapon.CurrentAmmo < weapon.MaxAmmo;
        }

        public void ReloadOperator(int slotIndex, int operatorSlot)
        {
            if (!CanReload(slotIndex, operatorSlot)) return;
            var s      = EnsureSlots();
            var box    = (AmmoBoxItem)s[slotIndex].Item!;
            var weapon = this.roster[operatorSlot].EquippedWeapon!;
            int needed = weapon.MaxAmmo - weapon.CurrentAmmo;
            int rounds = needed < box.Quantity ? needed : box.Quantity;
            weapon.SetAmmo(weapon.CurrentAmmo + rounds);
            box.Quantity -= rounds;
            if (box.Quantity <= 0)
                RemoveItem(slotIndex);
        }
    }
}
