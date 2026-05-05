# Inventory Slot Grid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat `List<InventoryItem>` with a fixed-size per-operator slot grid (`InventorySlot[]`) as specified in [[Sistema de Inventario]].

**Architecture:** A single `InventorySlot[]` array of size `rosterCount × 4` replaces `List<InventoryItem>`. Slots are indexed globally; `slotIndex / 4` gives the owning operator. Navigation uses a 2D grid cursor (2 rows × `rosterCount * 2` columns). An `InventorySlotCell` MonoBehaviour replaces `InventoryItemRow`. The `OperatorSubMenu` state is removed — Equip/Reload are direct operations using the slot owner.

**Tech Stack:** Unity 3D, VContainer, C# 9, Unity Input System, TextMeshPro, NUnit (EditMode tests)

**Spec:** [Sistema de Inventario.md](../../Sistema de Inventario.md)

---

## Grid Index Layout

The inventory is a **1D array** with size `rosterCount × 4`. The grid is laid out as 2 rows × `rosterCount * 2` columns, where each operator owns a 2×2 block:

```
col:   0    1  |  2    3  |  4    5  | ...
row 0: [0]  [1] | [4]  [5] | [8]  [9] | ...
row 1: [2]  [3] | [6]  [7] | [10][11] | ...
        Op 0        Op 1        Op 2
```

**slotIndex → (col, row):**
```
operatorSlot = slotIndex / 4
posInBlock   = slotIndex % 4
row          = posInBlock / 2
colWithinOp  = posInBlock % 2
globalCol    = operatorSlot * 2 + colWithinOp
```

**Example:** slotIndex=6 → operatorSlot=1, posInBlock=2, row=1, colWithinOp=0, globalCol=2 → (col=2, row=1) ✓

**(col, row) → slotIndex:**
```
operatorSlot = col / 2
colWithinOp  = col % 2
slotIndex    = operatorSlot * 4 + row * 2 + colWithinOp
```

**Example:** (col=2, row=1) → operatorSlot=1, colWithinOp=0 → 1×4 + 1×2 + 0 = 6 ✓

---

## File Map

### New files
| File | Responsibility |
|---|---|
| `Assets/Scripts/Inventory/InventorySlot.cs` | Slot data: `Item?`, `Quantity`, `IsEmpty` |
| `Assets/Scripts/Navigation/UI/InventorySlotCell.cs` | MonoBehaviour — renders one slot cell |

### Modified files
| File | Change |
|---|---|
| `Assets/Scripts/Inventory/ItemData.cs` | Add `virtual bool Stackable` |
| `Assets/Scripts/Inventory/AmmoBoxData.cs` | Override `Stackable → true` |
| `Assets/Scripts/Inventory/IInventoryService.cs` | New API: `Slots`, `SlotCount`, `AddItem` returns `bool`, `MoveItem` |
| `Assets/Scripts/Inventory/InventoryService.cs` | Full rewrite |
| `Assets/Tests/EditMode/InventoryServiceTests.cs` | Full rewrite |
| `Assets/Tests/EditMode/CombatMenuControllerTests.cs` | Update `FakeInventoryService` |
| `Assets/Scripts/Combat/States/CommandPanelState.cs` | Iterate operator's slot range |
| `Assets/Scripts/Combat/States/SubPanelState.cs` | Verify slot index unchanged |
| `Assets/Scripts/Navigation/StartingLoadout.cs` | `StartingItemEntry` gains `operatorSlot` |
| `Assets/Scripts/Navigation/InventoryBootstrap.cs` | Pass `operatorSlot` to `AddItem` |
| `Assets/Scripts/Navigation/Interactables/PickupInteractable.cs` | Handle `bool` from `AddItem` |
| `Assets/Scripts/Navigation/Interactables/DoorInteractable.cs` | Iterate slots in `FindKeyIndex` |
| `Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs` | Pass `operatorSlot: 0` |
| `Assets/Scripts/Navigation/UI/InventoryView.cs` | Slot cells, no list rows, no operator submenu |
| `Assets/Scripts/Navigation/UI/InventoryController.cs` | 2D cursor, Reorder state, no OperatorSubMenu |

### Deleted files
| File | Reason |
|---|---|
| `Assets/Scripts/Navigation/UI/InventoryItemRow.cs` | Replaced by `InventorySlotCell` |
| `Assets/Prefabs/UI/InventoryItemRow.prefab` | Replaced by `InventorySlotCell` prefab |
| `Assets/Scripts/Navigation/UI/OperatorSubMenuRow.cs` | OperatorSubMenu state removed |
| `Assets/Scripts/Navigation/UI/OperatorSubMenuEntry.cs` | Only used by `OperatorSubMenuRow` |

---

## Task 1: Data model — `InventorySlot` + `ItemData.Stackable`

**Files:**
- Create: `Assets/Scripts/Inventory/InventorySlot.cs`
- Modify: `Assets/Scripts/Inventory/ItemData.cs`
- Modify: `Assets/Scripts/Inventory/AmmoBoxData.cs`

- [ ] **Step 1: Create `InventorySlot.cs`**

```csharp
// Assets/Scripts/Inventory/InventorySlot.cs
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventorySlot
    {
        public InventoryItem? Item     { get; internal set; }
        public int            Quantity { get; internal set; }
        public bool           IsEmpty  => this.Item == null;
    }
}
```

- [ ] **Step 2: Add `Stackable` to `ItemData`**

In `Assets/Scripts/Inventory/ItemData.cs`, add after the `displayName` field and property:

```csharp
[SerializeField] private bool stackable = false;

public string   ItemId      => this.itemId;
public ItemType ItemType    => this.itemType;
public string   DisplayName => this.displayName;
public virtual bool Stackable => this.stackable;
```

- [ ] **Step 3: Override `Stackable` in `AmmoBoxData`**

In `Assets/Scripts/Inventory/AmmoBoxData.cs`, add after `DefaultQuantity`:

```csharp
public override bool Stackable => true;
```

- [ ] **Step 4: Verify compile — check Unity console**

Open Unity, wait for compilation. Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Inventory/InventorySlot.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Inventory/InventorySlot.cs.meta" \
        "Game/CrimsonDraft/Assets/Scripts/Inventory/ItemData.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Inventory/AmmoBoxData.cs"
git commit -m "feat(inventory): add InventorySlot type and ItemData.Stackable"
```

---

## Task 2: New `IInventoryService` + `InventoryService`

**Files:**
- Modify: `Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Assets/Scripts/Inventory/InventoryService.cs`

> ⚠️ After this task everything that references `IInventoryService` will fail to compile until Task 3 fixes the callers. Do Tasks 2 and 3 without running Unity between them.

- [ ] **Step 1: Rewrite `IInventoryService.cs`**

```csharp
// Assets/Scripts/Inventory/IInventoryService.cs
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
```

- [ ] **Step 2: Rewrite `InventoryService.cs`**

```csharp
// Assets/Scripts/Inventory/InventoryService.cs
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
```

---

## Task 3: Fix all callers to compile

**Files:**
- Modify: `Assets/Tests/EditMode/CombatMenuControllerTests.cs`
- Modify: `Assets/Scripts/Combat/States/CommandPanelState.cs`
- Modify: `Assets/Scripts/Navigation/StartingLoadout.cs`
- Modify: `Assets/Scripts/Navigation/InventoryBootstrap.cs`
- Modify: `Assets/Scripts/Navigation/Interactables/PickupInteractable.cs`
- Modify: `Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`
- Modify: `Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs`
- Modify: `Assets/Scripts/Navigation/UI/InventoryController.cs` (compile stub)
- Modify: `Assets/Scripts/Navigation/UI/InventoryView.cs` (compile stub)

- [ ] **Step 1: Update `FakeInventoryService` in `CombatMenuControllerTests.cs`**

Replace the entire `FakeInventoryService` inner class (starts at `private sealed class FakeInventoryService`) with:

```csharp
private sealed class FakeInventoryService : IInventoryService
{
    private readonly InventorySlot[]       slots       = new InventorySlot[8]; // 2 operators × 4
    private readonly Dictionary<int, bool> canReloadBy = new();

    public FakeInventoryService()
    {
        for (int i = 0; i < this.slots.Length; i++)
            this.slots[i] = new InventorySlot();
    }

    public IReadOnlyList<InventorySlot> Slots    => this.slots;
    public int                          SlotCount => this.slots.Length;

    public int ReloadCallCount  { get; private set; }
    public int LastSlotIndex    { get; private set; } = -1;
    public int LastOperatorSlot { get; private set; } = -1;

    public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => true;
    public void RemoveItem(int slotIndex)                                   { }
    public void MoveItem(int fromSlot, int toSlot)                         { }
    public void EquipWeapon(int slotIndex, int operatorSlot)               { }
    public void UnequipWeapon(int slotIndex)                               { }
    public int  GetEquippedWeaponIndex(int operatorSlot)                   => -1;

    public bool CanReload(int slotIndex, int operatorSlot)
        => this.canReloadBy.TryGetValue(slotIndex, out bool v) && v;

    public void ReloadOperator(int slotIndex, int operatorSlot)
    {
        this.ReloadCallCount++;
        this.LastSlotIndex    = slotIndex;
        this.LastOperatorSlot = operatorSlot;
    }

    /// <summary>Places an ammo box in the next empty slot. canReload controls CanReload result.</summary>
    public void RegisterBox(AmmoBoxItem box, bool canReload)
    {
        for (int i = 0; i < this.slots.Length; i++)
        {
            if (!this.slots[i].IsEmpty) continue;
            this.slots[i].Item     = box;
            this.slots[i].Quantity = 1;
            this.canReloadBy[i]    = canReload;
            return;
        }
    }
}
```

- [ ] **Step 2: Rename `LastAmmoBoxIndex` → `LastSlotIndex` in test assertions**

Search in `CombatMenuControllerTests.cs` for `LastAmmoBoxIndex` and replace with `LastSlotIndex`. There is one assertion:

```csharp
Assert.AreEqual(0, inv.LastSlotIndex, "correct inventory index");
```

- [ ] **Step 3: Update `CommandPanelState.cs` — iterate operator's slot range**

Find the Reload block and replace the `for` loop over `Items` with:

```csharp
int start = op * 4;
for (int i = start; i < start + 4; i++)
{
    var slot = this.inventory.Slots[i];
    if (this.inventory.CanReload(i, op) && slot.Item is AmmoBoxItem box)
    {
        compatibleIndices.Add(i);
        items.Add(new SubPanelItem($"{box.Data.DisplayName} \u00d7{box.Quantity}"));
    }
}
```

- [ ] **Step 4: Update `StartingLoadout.cs` — add `operatorSlot` to `StartingItemEntry`**

```csharp
[Serializable]
public struct StartingItemEntry
{
    public ItemData item;
    public int      quantity;
    public int      operatorSlot;  // which operator's 4-slot section this item goes to
}
```

- [ ] **Step 5: Update `InventoryBootstrap.cs`**

```csharp
public void Initialize()
{
    if (this.initialized) return;
    this.initialized = true;

    foreach (var entry in this.loadout.Items)
        this.inventory.AddItem(entry.item, entry.operatorSlot, entry.quantity);

    for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
    {
        var weaponData = this.loadout.DefaultWeapons[slot];
        if (weaponData == null) continue;

        this.inventory.AddItem(weaponData, operatorSlot: slot);

        // Find the slot index we just added and equip it
        int start = slot * 4;
        for (int i = start; i < start + 4; i++)
        {
            if (this.inventory.Slots[i].Item?.Data == weaponData
                && this.inventory.Slots[i].Item!.EquippedBySlot < 0)
            {
                this.inventory.EquipWeapon(i, slot);
                break;
            }
        }
    }
}
```

- [ ] **Step 6: Update `PickupInteractable.cs`**

```csharp
// Assets/Scripts/Navigation/Interactables/PickupInteractable.cs
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item = null!;

        public void Interact(InteractionContext context)
        {
            // operatorSlot: 0 — temporary default until active-operator concept is defined
            if (!context.InventoryService.AddItem(this.item, operatorSlot: 0))
            {
                context.PoiController.Open(
                    new[] { $"No space for: {this.item.DisplayName}." });
                return;
            }

            context.PoiController.Open(
                new[] { $"You picked up: {this.item.DisplayName}." },
                onClose: () => gameObject.SetActive(false));
        }
    }
}
```

- [ ] **Step 7: Update `DoorInteractable.cs` — `FindKeyIndex` uses slots**

Replace only the `FindKeyIndex` method:

```csharp
private static int FindKeyIndex(InteractionContext context, string itemId)
{
    var slots = context.InventoryService.Slots;
    for (int i = 0; i < slots.Count; i++)
        if (slots[i].Item?.Data.ItemId == itemId) return i;
    return -1;
}
```

- [ ] **Step 8: Update `ContainerController.cs` — pass `operatorSlot: 0`**

Find the `AddItem` call and change to:

```csharp
this.inventoryService.AddItem(item, operatorSlot: 0);
```

- [ ] **Step 9: Compile-stub `InventoryController.cs`**

Replace the entire file content with a minimal stub that compiles:

```csharp
// Assets/Scripts/Navigation/UI/InventoryController.cs
#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private readonly IInputService     inputService;
        private readonly IInventoryService inventoryService;
        private readonly IOperatorRoster   roster;
        private readonly InventoryView     view;

        [Preserve]
        public InventoryController(
            IInputService     inputService,
            IInventoryService inventoryService,
            IOperatorRoster   roster,
            InventoryView     view)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.view             = view;
        }

        void IInitializable.Initialize() { }
        void IDisposable.Dispose()       { }
    }
}
```

- [ ] **Step 10: Compile-stub `InventoryView.cs`**

Replace entire file with a stub that removes all references to `InventoryItemRow`, `OperatorSubMenuRow`, `OperatorSubMenuEntry`:

```csharp
// Assets/Scripts/Navigation/UI/InventoryView.cs
#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("Context Menu")]
        [SerializeField] private GameObject         contextMenuRoot      = null!;
        [SerializeField] private Transform          contextMenuContainer = null!;
        [SerializeField] private ContextMenuItemRow contextMenuRowPrefab = null!;

        [Header("Examine Overlay")]
        [SerializeField] private GameObject      examineOverlayRoot = null!;
        [SerializeField] private TextMeshProUGUI examineText        = null!;

        private readonly List<ContextMenuItemRow> contextRows = new();
        private int contextActionIndex;

        public int ContextMenuActionCount => this.contextRows.Count;

        public void Show()  => gameObject.SetActive(true);
        public void Hide()  => gameObject.SetActive(false);

        public void RefreshSlots(IReadOnlyList<InventorySlot> slots, int cursorSlot, int liftedSlot = -1) { }
        public void SetOperatorHeaders(string[] names) { }
        public void RefreshRosterPanel(IOperatorRoster roster, IInventoryService inventory)               { }

        public void ShowContextMenu(InventoryItem item, int slotIndex)
        {
            this.contextMenuRoot.SetActive(true);
            this.contextActionIndex = 0;

            foreach (var r in this.contextRows) Destroy(r.gameObject);
            this.contextRows.Clear();

            var actions = GetActionsForItem(item);
            for (int i = 0; i < actions.Count; i++)
            {
                var row = Instantiate(this.contextMenuRowPrefab, this.contextMenuContainer);
                row.Setup(actions[i], isCursor: i == 0, isEnabled: true);
                this.contextRows.Add(row);
            }
        }

        public void HideContextMenu() => this.contextMenuRoot.SetActive(false);

        public void SetContextMenuCursor(int index)
        {
            for (int i = 0; i < this.contextRows.Count; i++)
                this.contextRows[i].Setup(this.contextRows[i].Action, isCursor: i == index, isEnabled: true);
        }

        public ContextMenuAction GetContextMenuAction(int index) => this.contextRows[index].Action;

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() => this.examineOverlayRoot.SetActive(false);

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
            item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Reload,  ContextMenuAction.Examine },
                ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use,     ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Examine }
            };
    }
}
```

- [ ] **Step 11: Verify compile — open Unity, wait for compilation, check console**

Expected: no errors. Old `InventoryServiceTests` may fail — that is expected, they are rewritten in Task 4.

- [ ] **Step 12: Delete deprecated files**

In Unity Project window, delete:
- `Assets/Scripts/Navigation/UI/InventoryItemRow.cs` (and `.meta`)
- `Assets/Scripts/Navigation/UI/OperatorSubMenuRow.cs` (and `.meta`)
- `Assets/Scripts/Navigation/UI/OperatorSubMenuEntry.cs` (and `.meta`)
- `Assets/Prefabs/UI/InventoryItemRow.prefab` (and `.meta`)

Verify compile again after deletion — expected: no errors.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "refactor(inventory): replace flat Items list with InventorySlot[] grid; fix all callers"
```

---

## Task 4: Rewrite `InventoryServiceTests`

**Files:**
- Modify: `Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Rewrite the test file**

```csharp
// Assets/Tests/EditMode/InventoryServiceTests.cs
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
```

- [ ] **Step 2: Run EditMode tests**

**Window → General → Test Runner → EditMode → Run All**

Expected: all `InventoryServiceTests` pass. All `CombatMenuControllerTests` pass.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs"
git commit -m "test(inventory): rewrite InventoryServiceTests for slot-based API"
```

---

## Task 5: `InventorySlotCell` + full `InventoryView`

**Files:**
- Create: `Assets/Scripts/Navigation/UI/InventorySlotCell.cs`
- Modify: `Assets/Scripts/Navigation/UI/InventoryView.cs`

- [ ] **Step 1: Create `InventorySlotCell.cs`**

```csharp
// Assets/Scripts/Navigation/UI/InventorySlotCell.cs
#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventorySlotCell : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel     = null!;
        [SerializeField] private TextMeshProUGUI detailLabel   = null!; // quantity / ammo count
        [SerializeField] private TextMeshProUGUI equippedLabel = null!;
        [SerializeField] private Image           cursorImage   = null!;
        [SerializeField] private Image           liftedImage   = null!; // shown when item is "held" in Reorder

        public void Setup(InventorySlot slot, bool isCursor, bool isLifted)
        {
            if (slot.IsEmpty)
            {
                this.nameLabel.text     = string.Empty;
                this.detailLabel.text   = string.Empty;
                this.equippedLabel.text = string.Empty;
            }
            else
            {
                this.nameLabel.text = slot.Item!.Data.DisplayName;

                if (slot.Item is AmmoBoxItem box)
                    this.detailLabel.text = $"\u00d7{box.Quantity}";
                else if (slot.Quantity > 1)
                    this.detailLabel.text = $"\u00d7{slot.Quantity}";
                else
                    this.detailLabel.text = string.Empty;

                this.equippedLabel.text = slot.Item.IsEquipped ? "[Eq]" : string.Empty;
            }

            this.cursorImage.enabled = isCursor;
            this.liftedImage.enabled = isLifted;
        }
    }
}
```

- [ ] **Step 2: Create `InventorySlotCell` prefab in Unity**

1. In the Navigation scene's inventory canvas, create a UI Panel with:
   - Three `TextMeshPro - Text (UI)` children: name, detail, equipped
   - Two `Image` children: cursor highlight, lifted highlight
2. Add `InventorySlotCell` component. Wire all serialized fields.
3. Drag to `Assets/Prefabs/UI/InventorySlotCell.prefab`.
4. Remove the temporary GameObject from the scene.

- [ ] **Step 3: Replace `InventoryView.cs` with full implementation**

```csharp
// Assets/Scripts/Navigation/UI/InventoryView.cs
#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("Slot Grid")]
        // Length = rosterCount × 4. Order matches slotIndex (op0 slots 0-3, op1 slots 4-7, ...).
        [SerializeField] private InventorySlotCell[] cells           = null!;
        // One label per operator, in operatorSlot order.
        [SerializeField] private TextMeshProUGUI[]   operatorHeaders = null!;

        [Header("Roster Panel")]
        [SerializeField] private Transform         rosterContainer = null!;
        [SerializeField] private RosterOperatorRow rosterRowPrefab = null!;

        [Header("Context Menu")]
        [SerializeField] private GameObject         contextMenuRoot      = null!;
        [SerializeField] private Transform          contextMenuContainer = null!;
        [SerializeField] private ContextMenuItemRow contextMenuRowPrefab = null!;

        [Header("Examine Overlay")]
        [SerializeField] private GameObject      examineOverlayRoot = null!;
        [SerializeField] private TextMeshProUGUI examineText        = null!;

        private readonly List<RosterOperatorRow>  rosterRows  = new();
        private readonly List<ContextMenuItemRow> contextRows = new();

        public int ContextMenuActionCount => this.contextRows.Count;

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show()  => gameObject.SetActive(true);
        public void Hide()  => gameObject.SetActive(false);

        // ── Slot grid ──────────────────────────────────────────────────────────

        public void RefreshSlots(IReadOnlyList<InventorySlot> slots, int cursorSlot, int liftedSlot = -1)
        {
            for (int i = 0; i < this.cells.Length && i < slots.Count; i++)
                this.cells[i].Setup(slots[i], isCursor: i == cursorSlot, isLifted: i == liftedSlot);
        }

        public void SetOperatorHeaders(string[] names)
        {
            for (int i = 0; i < this.operatorHeaders.Length && i < names.Length; i++)
                this.operatorHeaders[i].text = names[i];
        }

        // ── Roster panel ───────────────────────────────────────────────────────

        public void RefreshRosterPanel(IOperatorRoster roster, IInventoryService inventory)
        {
            roster.EnsureInitialized();

            int presentCount = 0;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].IsPresent) presentCount++;

            while (this.rosterRows.Count < presentCount)
                this.rosterRows.Add(Instantiate(this.rosterRowPrefab, this.rosterContainer));

            for (int i = presentCount; i < this.rosterRows.Count; i++)
                this.rosterRows[i].gameObject.SetActive(false);

            int rowIdx = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var op = roster[i];
                if (!op.IsPresent) continue;

                string rawName = op.Data?.DisplayName ?? string.Empty;
                string name    = rawName.Length > 0 ? rawName : $"Slot {i}";
                int    wIdx    = inventory.GetEquippedWeaponIndex(i);
                string wpnName;
                if (wIdx >= 0)
                {
                    string dn     = inventory.Slots[wIdx].Item?.Data.DisplayName ?? "---";
                    var    weapon = op.EquippedWeapon;
                    wpnName = weapon != null ? $"{dn} ({weapon.CurrentAmmo}/{weapon.MaxAmmo})" : dn;
                }
                else
                {
                    wpnName = "---";
                }

                this.rosterRows[rowIdx].Setup(name, wpnName);
                this.rosterRows[rowIdx].gameObject.SetActive(true);
                rowIdx++;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        public void ShowContextMenu(InventoryItem item, int slotIndex)
        {
            this.contextMenuRoot.SetActive(true);

            foreach (var r in this.contextRows) Destroy(r.gameObject);
            this.contextRows.Clear();

            var actions = GetActionsForItem(item);
            for (int i = 0; i < actions.Count; i++)
            {
                var row = Instantiate(this.contextMenuRowPrefab, this.contextMenuContainer);
                row.Setup(actions[i], isCursor: i == 0, isEnabled: true);
                this.contextRows.Add(row);
            }
        }

        public void HideContextMenu() => this.contextMenuRoot.SetActive(false);

        public void SetContextMenuCursor(int index)
        {
            for (int i = 0; i < this.contextRows.Count; i++)
                this.contextRows[i].Setup(this.contextRows[i].Action, isCursor: i == index, isEnabled: true);
        }

        public ContextMenuAction GetContextMenuAction(int index) => this.contextRows[index].Action;

        // ── Examine overlay ────────────────────────────────────────────────────

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() => this.examineOverlayRoot.SetActive(false);

        // ── Private helpers ────────────────────────────────────────────────────

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
            item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Reload,  ContextMenuAction.Examine },
                ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use,     ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Examine }
            };
    }
}
```

- [ ] **Step 4: Verify compile — check Unity console**

Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventorySlotCell.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventorySlotCell.cs.meta" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs" \
        "Game/CrimsonDraft/Assets/Prefabs/UI/InventorySlotCell.prefab" \
        "Game/CrimsonDraft/Assets/Prefabs/UI/InventorySlotCell.prefab.meta"
git commit -m "feat(inventory): InventorySlotCell and full InventoryView slot grid"
```

---

## Task 6: Rewrite `InventoryController`

**Files:**
- Modify: `Assets/Scripts/Navigation/UI/InventoryController.cs`

- [ ] **Step 1: Rewrite `InventoryController.cs`**

```csharp
// Assets/Scripts/Navigation/UI/InventoryController.cs
#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private enum State { Closed, List, Reorder, ContextMenu }

        private readonly IInputService     inputService;
        private readonly IInventoryService inventoryService;
        private readonly IOperatorRoster   roster;
        private readonly InventoryView     view;

        private State state             = State.Closed;
        private int   cursorSlotIndex;
        private int   liftedSlotIndex   = -1;
        private int   contextActionIndex;

        [Preserve]
        public InventoryController(
            IInputService     inputService,
            IInventoryService inventoryService,
            IOperatorRoster   roster,
            InventoryView     view)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.view             = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenInventory.performed += OnOpenInventory;
            this.inputService.UINavigate.performed    += OnUINavigate;
            this.inputService.UIConfirm.performed     += OnUIConfirm;
            this.inputService.UICancel.performed      += OnUICancel;
            this.inputService.UIBack.performed        += OnUIBack;
        }

        // ── Open / Close ───────────────────────────────────────────────────────

        private void OnOpenInventory(InputAction.CallbackContext _)
        {
            if (this.state != State.Closed) return;

            this.state           = State.List;
            this.cursorSlotIndex = 0;
            this.liftedSlotIndex = -1;
            Time.timeScale       = 0f;
            this.inputService.SwitchToUI();
            this.view.SetOperatorHeaders(BuildOperatorHeaders());
            RefreshView();
            this.view.Show();
        }

        private void Close()
        {
            this.state           = State.Closed;
            this.liftedSlotIndex = -1;
            this.view.HideContextMenu();
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OnUINavigate(InputAction.CallbackContext ctx)
        {
            var dir = ctx.ReadValue<Vector2>();
            int dx  = dir.x > 0.5f ? 1 : dir.x < -0.5f ? -1 : 0;
            int dy  = dir.y < -0.5f ? 1 : dir.y > 0.5f ? -1 : 0;

            switch (this.state)
            {
                case State.List:
                case State.Reorder:
                {
                    if (dx == 0 && dy == 0) return;
                    // Total columns = rosterCount * 2. Each operator occupies 2 columns.
                    int totalCols = this.inventoryService.SlotCount / 2;
                    var (col, row) = SlotToColRow(this.cursorSlotIndex);
                    col = Mathf.Clamp(col + dx, 0, totalCols - 1);
                    row = Mathf.Clamp(row + dy, 0, 1);
                    this.cursorSlotIndex = ColRowToSlot(col, row);
                    RefreshView();
                    break;
                }
                case State.ContextMenu:
                {
                    int delta = dy;
                    if (delta == 0) return;
                    int count = this.view.ContextMenuActionCount;
                    if (count == 0) return;
                    this.contextActionIndex = (this.contextActionIndex + delta + count) % count;
                    this.view.SetContextMenuCursor(this.contextActionIndex);
                    break;
                }
            }
        }

        private void OnUIConfirm(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    OpenContextMenuOrIgnore();
                    break;
                case State.Reorder:
                    DropItem();
                    break;
                case State.ContextMenu:
                    ExecuteContextMenuAction();
                    break;
            }
        }

        private void OnUICancel(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    Close();
                    break;
                case State.Reorder:
                    CancelReorder();
                    break;
                case State.ContextMenu:
                    this.state = State.List;
                    this.view.HideContextMenu();
                    RefreshView();
                    break;
            }
        }

        // UIBack = Y button — lifts item for Reorder
        private void OnUIBack(InputAction.CallbackContext _)
        {
            if (this.state != State.List) return;
            if (this.inventoryService.Slots[this.cursorSlotIndex].IsEmpty) return;
            this.liftedSlotIndex = this.cursorSlotIndex;
            this.state           = State.Reorder;
            RefreshView();
        }

        // ── Reorder ────────────────────────────────────────────────────────────

        private void DropItem()
        {
            this.inventoryService.MoveItem(this.liftedSlotIndex, this.cursorSlotIndex);
            this.liftedSlotIndex = -1;
            this.state           = State.List;
            RefreshView();
        }

        private void CancelReorder()
        {
            this.liftedSlotIndex = -1;
            this.state           = State.List;
            RefreshView();
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private void OpenContextMenuOrIgnore()
        {
            var slot = this.inventoryService.Slots[this.cursorSlotIndex];
            if (slot.IsEmpty) return;
            this.contextActionIndex = 0;
            this.view.ShowContextMenu(slot.Item!, this.cursorSlotIndex);
            this.state = State.ContextMenu;
        }

        private void ExecuteContextMenuAction()
        {
            var action  = this.view.GetContextMenuAction(this.contextActionIndex);
            int ownerOp = this.cursorSlotIndex / 4; // operatorSlot derived from slot ownership

            this.state = State.List;
            this.view.HideContextMenu();

            switch (action)
            {
                case ContextMenuAction.Equip:
                    this.inventoryService.EquipWeapon(this.cursorSlotIndex, ownerOp);
                    break;

                case ContextMenuAction.Unequip:
                    this.inventoryService.UnequipWeapon(this.cursorSlotIndex);
                    break;

                case ContextMenuAction.Reload:
                    this.inventoryService.ReloadOperator(this.cursorSlotIndex, ownerOp);
                    break;

                case ContextMenuAction.Use:
                    // TODO: consumable use effects
                    break;

                case ContextMenuAction.Examine:
                    var item = this.inventoryService.Slots[this.cursorSlotIndex].Item;
                    if (item != null) this.view.ShowExamineOverlay(item);
                    return; // don't RefreshView — stay open
            }

            RefreshView();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void RefreshView()
        {
            this.view.RefreshSlots(this.inventoryService.Slots, this.cursorSlotIndex, this.liftedSlotIndex);
            this.view.RefreshRosterPanel(this.roster, this.inventoryService);
        }

        private string[] BuildOperatorHeaders()
        {
            this.roster.EnsureInitialized();
            var headers = new string[this.roster.Count];
            for (int i = 0; i < this.roster.Count; i++)
                headers[i] = this.roster[i].Data?.DisplayName ?? $"Operator {i}";
            return headers;
        }

        // ── Grid index math ────────────────────────────────────────────────────
        //
        // Grid layout: 2 rows × (rosterCount * 2) columns.
        // Each operator owns a 2×2 block. Example with 2 operators:
        //
        //   col:   0    1  |  2    3
        //   row 0: [0]  [1] | [4]  [5]
        //   row 1: [2]  [3] | [6]  [7]
        //           Op 0        Op 1
        //
        // slotIndex → (col, row):
        //   operatorSlot = slotIndex / 4
        //   posInBlock   = slotIndex % 4
        //   row          = posInBlock / 2
        //   colWithinOp  = posInBlock % 2
        //   globalCol    = operatorSlot * 2 + colWithinOp
        //
        // (col, row) → slotIndex:
        //   operatorSlot = col / 2
        //   colWithinOp  = col % 2
        //   slotIndex    = operatorSlot * 4 + row * 2 + colWithinOp

        private static int ColRowToSlot(int col, int row)
        {
            int operatorSlot = col / 2;
            int colWithinOp  = col % 2;
            return operatorSlot * 4 + row * 2 + colWithinOp;
        }

        private static (int col, int row) SlotToColRow(int slotIndex)
        {
            int operatorSlot = slotIndex / 4;
            int posInBlock   = slotIndex % 4;
            int row          = posInBlock / 2;
            int colWithinOp  = posInBlock % 2;
            return (operatorSlot * 2 + colWithinOp, row);
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenInventory.performed -= OnOpenInventory;
            this.inputService.UINavigate.performed    -= OnUINavigate;
            this.inputService.UIConfirm.performed     -= OnUIConfirm;
            this.inputService.UICancel.performed      -= OnUICancel;
            this.inputService.UIBack.performed        -= OnUIBack;
        }
    }
}
```

- [ ] **Step 2: Verify compile — check Unity console**

Expected: no errors.

- [ ] **Step 3: Run all EditMode tests**

**Window → General → Test Runner → EditMode → Run All**

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs"
git commit -m "feat(inventory): rewrite InventoryController with 2D slot grid and Reorder state"
```

---

## Task 7: Scene wiring + play-test

- [ ] **Step 1: Update `StartingLoadout` asset in inspector**

Open `Assets/ScriptableObjects/` and find the `StartingLoadout` asset. For each entry in `Items[]`, set `operatorSlot` to the appropriate operator index (default 0 if single operator or testing).

- [ ] **Step 2: Wire `InventoryView` in Navigation scene**

Select the `InventoryView` GameObject in the Navigation scene:
1. Set `cells` array: drag `InventorySlotCell` GameObjects in slotIndex order. For a 1-operator loadout: 4 cells (slots 0–3). For 2 operators: 8 cells (slots 0–7).
2. Set `operatorHeaders` array: one `TextMeshProUGUI` label per operator.
3. Verify context menu and examine overlay serialized references are still wired.

- [ ] **Step 3: Play-test**

Enter Play Mode and verify:
- [ ] Slot grid appears with correct number of cells
- [ ] Starting items appear in correct operator sections
- [ ] D-pad left/right moves between columns (including across operator block borders)
- [ ] D-pad up/down moves between the 2 rows
- [ ] Cursor does not wrap at grid edges
- [ ] Confirm on empty slot: nothing happens
- [ ] Confirm on item: context menu appears with correct actions for that item type
- [ ] Equip weapon from slot: equips to owning operator, roster panel shows weapon + ammo
- [ ] UIBack (Y) on item: enters Reorder, lifted highlight visible on origin cell
- [ ] Confirm in Reorder on different slot: items swap correctly
- [ ] Cancel in Reorder: item returns to origin, Reorder exits
- [ ] Pick up item (`PickupInteractable`): item appears in op0's section
- [ ] Full op0 inventory + pickup: shows `"No space for: X."`

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/Navigation.unity" \
        "Game/CrimsonDraft/Assets/ScriptableObjects/"
git commit -m "chore(inventory): wire slot grid in Navigation scene; set operatorSlot on StartingLoadout entries"
```
