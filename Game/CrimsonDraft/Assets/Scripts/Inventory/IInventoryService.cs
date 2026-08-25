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

        /// <summary>Places an already-constructed item into the first empty slot of operatorSlot's 4-slot
        /// section, preserving its object reference (unlike AddItem, which always constructs a new item).
        /// Returns false if all 4 slots are occupied.</summary>
        bool AddExistingItem(InventoryItem item, int operatorSlot);

        /// <summary>Tries each operator in order until one has space. Returns false only if all operators are full.</summary>
        bool AddItemAuto(ItemData data, int quantity = 0);

        /// <summary>Clears the slot at slotIndex (Item = null, Quantity = 0).</summary>
        void RemoveItem(int slotIndex);

        /// <summary>Clears any slot holding an AmmoBoxItem whose Quantity has reached 0 — a
        /// safety net so a stack that was drained without going through a path that already
        /// removes it (e.g. legacy save data) doesn't linger as a visible 0-quantity item.</summary>
        void PruneEmptyStacks();

        /// <summary>Swaps the full contents of fromSlot and toSlot.</summary>
        void MoveItem(int fromSlot, int toSlot);

        /// <summary>Equips the weapon at slotIndex to operatorSlot.
        /// The target weapon slot (Primary/Secondary) is derived from WeaponData.WeaponSlot.
        /// Only the weapon already occupying that specific slot is replaced; the other slot is untouched.</summary>
        void EquipWeapon(int slotIndex, int operatorSlot);

        /// <summary>Unequips the weapon at slotIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int slotIndex);

        /// <summary>Returns the slot index of the weapon equipped by operatorSlot, or -1.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if the ammo box at slotIndex can reload operatorSlot's weapon.</summary>
        bool CanReload(int slotIndex, int operatorSlot);

        /// <summary>Reloads operatorSlot's weapon using the ammo box at slotIndex. Clears slot if box exhausted.</summary>
        void ReloadOperator(int slotIndex, int operatorSlot);

        /// <summary>Checks for a recipe matching the items in slotA and slotB (symmetric).
        /// If found: removes both items and places the result in the first available slot via AddItemAuto.
        /// Returns false if either slot is empty or no recipe exists. No mutation on false.</summary>
        bool TryCombine(int slotA, int slotB);

        /// <summary>
        /// Finds the first KeyItem with the given itemId, decrements its uses, and returns the outcome.
        /// The key is never auto-removed — caller must call RemoveItem(outcome.SlotIndex) to discard it.
        /// </summary>
        KeyUseOutcome TryUseKey(string keyItemId);

        /// <summary>
        /// Replaces the internal slot array and re-wires equipped weapons to the roster.
        /// Used by InventoryBootstrap to restore saved state across scene transitions.
        /// </summary>
        void LoadState(InventorySlot[] slots);

        /// <summary>Records the 2D visual grid position and rotation of the item at slotIndex for persistence across scenes.</summary>
        void SetSlotPosition(int slotIndex, int col, int row, int rotation);

        /// <summary>Returns the raw slot array for persistence by InventoryBootstrap.</summary>
        InventorySlot[] GetRawSlots();
    }
}
