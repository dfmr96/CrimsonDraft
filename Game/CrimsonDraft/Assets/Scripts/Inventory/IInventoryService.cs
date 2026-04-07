#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        /// <summary>
        /// Flat array of rosterCount × 4 slots. Never null.
        /// Grid layout: 2 rows × (rosterCount * 2) columns.
        /// slotIndex / 4 = owning operatorSlot.
        /// See Grid Index Layout in the implementation plan for col/row formulas.
        /// </summary>
        IReadOnlyList<InventorySlot> Slots { get; }
        int SlotCount { get; }

        /// <summary>Adds item to operatorSlot's 4-slot section. Stacks if Stackable and same ItemId exists.
        /// Returns false if all 4 slots are occupied and item cannot stack.</summary>
        bool AddItem(ItemData data, int operatorSlot, int quantity = 0);

        /// <summary>Tries each operator in order until one has space. Returns false only if all operators are full.</summary>
        bool AddItemAuto(ItemData data, int quantity = 0);

        /// <summary>Clears the slot at slotIndex (Item = null, Quantity = 0).</summary>
        void RemoveItem(int slotIndex);

        /// <summary>Swaps the full contents of fromSlot and toSlot.</summary>
        void MoveItem(int fromSlot, int toSlot);

        /// <summary>Equips the weapon at slotIndex to operatorSlot. Unequips any previous weapon on that operator.</summary>
        void EquipWeapon(int slotIndex, int operatorSlot);

        /// <summary>Unequips the weapon at slotIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int slotIndex);

        /// <summary>Returns the slot index of the weapon equipped by operatorSlot, or -1.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if the ammo box at slotIndex can reload operatorSlot's weapon.</summary>
        bool CanReload(int slotIndex, int operatorSlot);

        /// <summary>Reloads operatorSlot's weapon using the ammo box at slotIndex. Clears slot if box exhausted.</summary>
        void ReloadOperator(int slotIndex, int operatorSlot);
    }
}
