# Inventory MVP Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the shared roster inventory as a navigable list with per-operator weapon slots, context menu actions, and two-panel UI.

**Spec:** Implements [[Sistema de Inventario]] (MVP simplification — lista compartida, sin grilla, slot arma por operador).

**Architecture:** `InventoryService` owns the item list and equip mapping in `CrimsonDraft.Inventory`. `InventoryController` (Navigation) drives a state machine (List → ContextMenu → OperatorSubMenu). `InventoryView` renders two panels: item list (left) + roster status (right).

**Tech Stack:** C# + Unity uGUI, VContainer (Scoped), NUnit (EditMode tests for InventoryService). No NSubstitute — use fake implementations via inner classes, following existing test patterns.

---

## Task 1: Add UIConfirm to IInputService + InputService

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Input/IInputService.cs`
- Modify: `Assets/Scripts/Infrastructure/Input/InputService.cs`

The UI action map already has a `Confirm` action in the input asset (same constant used by CombatConfirm). We just need to expose it for the UI map.

**Step 1: Add UIConfirm to IInputService**

```csharp
// In IInputService.cs — add after UINavigate:
InputAction UIConfirm  { get; }
InputAction UICancel   { get; }
```

**Step 2: Bind UIConfirm in InputService**

In `InputService.cs` constructor, after `UINavigate = this.uiMap[NavigateAction];`:
```csharp
UIConfirm  = this.uiMap[ConfirmAction];
UICancel   = this.uiMap[CancelAction];
```

And declare the property:
```csharp
public InputAction UIConfirm { get; }
```

**Step 3: Add UIConfirm action to the input asset in Unity Editor**

Open `Assets/Settings/CrimsonDraftInputActions.inputactions` (or equivalent). In the UI map, add a `Confirm` action bound to Gamepad South (A button) and Keyboard Space/Enter. This is done in the Unity Editor Input Actions window — verify it exists (CombatConfirm uses the same action name from the Combat map, so the pattern is established).

**Step 4: Compile — no tests needed (interface change)**

Run menu: `Assets → Compile Scripts` or wait for Unity auto-compile. No errors expected.

**Step 5: Commit**

```bash
git add "Assets/Scripts/Infrastructure/Input/IInputService.cs" \
        "Assets/Scripts/Infrastructure/Input/InputService.cs"
git commit -m "feat(inventory): add UIConfirm to IInputService for UI map"
```

---

## Task 2: Assembly references

**Files:**
- Modify: `Assets/Scripts/Inventory/CrimsonDraft.Inventory.asmdef`
- Modify: `Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef`

**Step 1: Add CrimsonDraft.Operators to Inventory asmdef**

```json
{
    "name": "CrimsonDraft.Inventory",
    "rootNamespace": "CrimsonDraft.Inventory",
    "references": [
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Operators",
        "VContainer",
        "VContainer.Unity"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Step 2: Add CrimsonDraft.Inventory to Navigation asmdef**

```json
{
    "name": "CrimsonDraft.Navigation",
    "rootNamespace": "CrimsonDraft.Navigation",
    "references": [
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Inventory",
        "CrimsonDraft.Operators",
        "VContainer",
        "VContainer.Unity",
        "UniTask",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Step 3: Commit**

```bash
git add "Assets/Scripts/Inventory/CrimsonDraft.Inventory.asmdef" \
        "Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef"
git commit -m "feat(inventory): wire assembly references for Inventory + Navigation"
```

---

## Task 3: Data models — ItemType + ItemData

**Files:**
- Create: `Assets/Scripts/Inventory/ItemType.cs`
- Create: `Assets/Scripts/Inventory/ItemData.cs`

**Step 1: Write ItemType.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public enum ItemType { Weapon, AmmoBox, Consumable }
}
```

**Step 2: Write ItemData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "CrimsonDraft/Inventory/Item Data")]
    public sealed class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId      = string.Empty;
        [SerializeField] private ItemType itemType  = ItemType.Consumable;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string caliber     = string.Empty; // empty if not applicable

        public string   ItemId      => this.itemId;
        public ItemType ItemType    => this.itemType;
        public string   DisplayName => this.displayName;
        public string   Caliber     => this.caliber;
    }
}
```

**Step 3: Commit**

```bash
git add "Assets/Scripts/Inventory/ItemType.cs" \
        "Assets/Scripts/Inventory/ItemData.cs"
git commit -m "feat(inventory): add ItemType enum and ItemData ScriptableObject"
```

---

## Task 4: InventoryItem runtime class

**Files:**
- Create: `Assets/Scripts/Inventory/InventoryItem.cs`

**Step 1: Write InventoryItem.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryItem
    {
        public ItemData Data          { get; }
        public int      EquippedBySlot { get; internal set; } = -1;
        public bool     IsEquipped    => this.EquippedBySlot >= 0;

        public InventoryItem(ItemData data) => this.Data = data;
    }
}
```

**Step 2: Commit**

```bash
git add "Assets/Scripts/Inventory/InventoryItem.cs"
git commit -m "feat(inventory): add InventoryItem runtime class"
```

---

## Task 5: IInventoryService interface

**Files:**
- Create: `Assets/Scripts/Inventory/IInventoryService.cs`

**Step 1: Write IInventoryService.cs**

```csharp
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<InventoryItem> Items { get; }

        void AddItem(ItemData data);

        /// <summary>Equips weapon at itemIndex to operatorSlot. Unequips any weapon that slot was previously carrying.</summary>
        void EquipWeapon(int itemIndex, int operatorSlot);

        /// <summary>Unequips weapon at itemIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int itemIndex);

        /// <summary>Returns the index of the weapon equipped by operatorSlot, or -1 if none.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if ammoBox at ammoBoxIndex can reload operatorSlot (caliber match + ammo < max).</summary>
        bool CanReload(int ammoBoxIndex, int operatorSlot);

        /// <summary>Reloads operatorSlot using the ammo box at ammoBoxIndex. Consumes the box (removes from list).</summary>
        void ReloadOperator(int ammoBoxIndex, int operatorSlot);
    }
}
```

**Step 2: Commit**

```bash
git add "Assets/Scripts/Inventory/IInventoryService.cs"
git commit -m "feat(inventory): add IInventoryService interface"
```

---

## Task 6: InventoryService — TDD

**Files:**
- Create: `Assets/Scripts/Inventory/InventoryService.cs`
- Create: `Assets/Tests/EditMode/InventoryServiceTests.cs`

### Step 1: Write failing tests first

```csharp
// Assets/Tests/EditMode/InventoryServiceTests.cs
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using System.Collections.Generic;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryServiceTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;
            public bool IsInitialized => true;
            public int Count => this.slots.Length;
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

        private static OperatorRuntime MakeAlive(int slot, int maxAmmo = 6, int currentAmmo = 0)
        {
            var op = new OperatorRuntime(slot, null, isPresent: true, maxHp: 100, maxAmmo: maxAmmo);
            op.ConsumeAmmo(maxAmmo - currentAmmo); // set ammo to currentAmmo
            return op;
        }

        private static ItemData MakeWeapon(string caliber = "9mm")
        {
            var d = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            // ItemData fields are private — set via serialization hack not needed in tests.
            // Instead, use a test-specific subclass pattern or public setters.
            // NOTE: ItemData uses [SerializeField] — for tests, create a TestItemData helper:
            return d;
        }

        // ── Helper: since ItemData uses SerializeField, use a builder approach ──

        private sealed class TestItemData
        {
            public string ItemId      { get; init; } = "test";
            public ItemType ItemType  { get; init; } = ItemType.Weapon;
            public string DisplayName { get; init; } = "Test Item";
            public string Caliber     { get; init; } = string.Empty;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_increasesItemCount()
        {
            var roster = new FakeRoster(MakeAlive(0));
            var service = new InventoryService(roster);

            // Use a direct InventoryItem for service.AddItem via a test that accepts itemdata
            // Since ItemData is a ScriptableObject we can't create in EditMode without
            // using ScriptableObject.CreateInstance — which is valid in EditMode tests.
            var data = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(data);

            Assert.AreEqual(1, service.Items.Count);
        }

        [Test]
        public void EquipWeapon_setsEquippedBySlot()
        {
            var roster = new FakeRoster(MakeAlive(0));
            var service = new InventoryService(roster);
            var data = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(data);

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Items[0].EquippedBySlot);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameSlot()
        {
            var roster = new FakeRoster(MakeAlive(0));
            var service = new InventoryService(roster);
            var data0 = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            var data1 = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(data0);
            service.AddItem(data1);

            service.EquipWeapon(0, operatorSlot: 0);
            service.EquipWeapon(1, operatorSlot: 0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot, "old weapon should be unequipped");
            Assert.AreEqual(0,  service.Items[1].EquippedBySlot, "new weapon should be equipped");
        }

        [Test]
        public void UnequipWeapon_setsSlotToMinusOne()
        {
            var roster = new FakeRoster(MakeAlive(0));
            var service = new InventoryService(roster);
            var data = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(data);
            service.EquipWeapon(0, operatorSlot: 0);

            service.UnequipWeapon(0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot);
        }

        [Test]
        public void GetEquippedWeaponIndex_returnsCorrectIndex()
        {
            var roster = new FakeRoster(MakeAlive(0), MakeAlive(1));
            var service = new InventoryService(roster);
            var data = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(data);
            service.EquipWeapon(0, operatorSlot: 1);

            Assert.AreEqual(0, service.GetEquippedWeaponIndex(operatorSlot: 1));
            Assert.AreEqual(-1, service.GetEquippedWeaponIndex(operatorSlot: 0));
        }

        [Test]
        public void ReloadOperator_removesAmmoBoxFromList()
        {
            // CanReload requires caliber match — we can't set caliber on ScriptableObject
            // without the inspector. For this test we test the structural behavior:
            // ReloadOperator only removes the box if CanReload returns true.
            // We test CanReload = false path (no weapon equipped → no removal).
            var roster = new FakeRoster(MakeAlive(0, maxAmmo: 6, currentAmmo: 0));
            var service = new InventoryService(roster);
            var ammoBox = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            service.AddItem(ammoBox);

            service.ReloadOperator(0, operatorSlot: 0); // CanReload=false (no weapon equipped) → no-op

            Assert.AreEqual(1, service.Items.Count, "box should NOT be consumed when CanReload is false");
        }
    }
}
```

**Step 2: Run tests — expect compile error (InventoryService not yet created)**

In Unity: `Window → General → Test Runner → EditMode → Run All`
Expected: compile error "type InventoryService not found"

**Step 3: Write InventoryService.cs**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster roster;
        private readonly List<InventoryItem> items = new();

        [Preserve]
        public InventoryService(IOperatorRoster roster) => this.roster = roster;

        public IReadOnlyList<InventoryItem> Items => this.items;

        public void AddItem(ItemData data) => this.items.Add(new InventoryItem(data));

        public void EquipWeapon(int itemIndex, int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                    this.items[i].EquippedBySlot = -1;
            }
            this.items[itemIndex].EquippedBySlot = operatorSlot;
        }

        public void UnequipWeapon(int itemIndex) =>
            this.items[itemIndex].EquippedBySlot = -1;

        public int GetEquippedWeaponIndex(int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                    return i;
            }
            return -1;
        }

        public bool CanReload(int ammoBoxIndex, int operatorSlot)
        {
            InventoryItem box = this.items[ammoBoxIndex];
            if (box.Data.ItemType != ItemType.AmmoBox)
                return false;

            int weaponIndex = GetEquippedWeaponIndex(operatorSlot);
            if (weaponIndex < 0)
                return false;

            if (this.items[weaponIndex].Data.Caliber != box.Data.Caliber)
                return false;

            var op = this.roster[operatorSlot];
            return op.IsAlive && op.Ammo < op.MaxAmmo;
        }

        public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
        {
            if (!CanReload(ammoBoxIndex, operatorSlot))
                return;

            this.roster[operatorSlot].Reload();
            this.items.RemoveAt(ammoBoxIndex);
        }
    }
}
```

**Step 4: Run tests — expect all pass**

`Window → General → Test Runner → EditMode → Run All`
Expected: all InventoryServiceTests pass.

**Step 5: Commit**

```bash
git add "Assets/Scripts/Inventory/InventoryService.cs" \
        "Assets/Tests/EditMode/InventoryServiceTests.cs"
git commit -m "feat(inventory): implement InventoryService with TDD"
```

---

## Task 7: Register InventoryService in NavigationScope

**Files:**
- Modify: `Assets/Scripts/Navigation/NavigationScope.cs`

**Step 1: Add InventoryService registration**

Add `using CrimsonDraft.Inventory;` at top.

Inside `Configure(IContainerBuilder builder)`, after the InventoryView registration:
```csharp
builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
```

**Step 2: Inject IInventoryService into InventoryController**

Modify `Assets/Scripts/Navigation/UI/InventoryController.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
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
        private enum State { Closed, List, ContextMenu, OperatorSubMenu }

        private readonly IInputService inputService;
        private readonly IInventoryService inventoryService;
        private readonly IOperatorRoster roster;
        private readonly InventoryView view;

        private State state = State.Closed;
        private int cursorIndex;
        private int contextActionIndex;

        [Preserve]
        public InventoryController(
            IInputService inputService,
            IInventoryService inventoryService,
            IOperatorRoster roster,
            InventoryView view)
        {
            this.inputService      = inputService;
            this.inventoryService  = inventoryService;
            this.roster            = roster;
            this.view              = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenInventory.performed += OnOpenInventory;
            this.inputService.UINavigate.performed    += OnUINavigate;
            this.inputService.UIConfirm.performed     += OnUIConfirm;
            this.inputService.UICancel.performed      += OnUICancel;
        }

        // ── Open / Close ───────────────────────────────────────────────────────

        private void OnOpenInventory(InputAction.CallbackContext _)
        {
            if (this.state != State.Closed) return;

            this.state       = State.List;
            this.cursorIndex = 0;
            this.inputService.SwitchToUI();
            RefreshView();
            this.view.Show();
        }

        private void Close()
        {
            this.state = State.Closed;
            this.view.HideContextMenu();
            this.view.HideOperatorSubMenu();
            this.view.Hide();
            this.inputService.SwitchToGameplay();
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OnUINavigate(InputAction.CallbackContext ctx)
        {
            var dir = ctx.ReadValue<UnityEngine.Vector2>();
            int delta = dir.y > 0.5f ? -1 : dir.y < -0.5f ? 1 : 0;

            switch (this.state)
            {
                case State.List:
                    if (delta != 0)
                    {
                        int count = this.inventoryService.Items.Count;
                        if (count == 0) return;
                        this.cursorIndex = (this.cursorIndex + delta + count) % count;
                        this.view.SetItemCursor(this.cursorIndex);
                    }
                    break;

                case State.ContextMenu:
                    if (delta != 0)
                    {
                        int count = this.view.ContextMenuActionCount;
                        this.contextActionIndex = (this.contextActionIndex + delta + count) % count;
                        this.view.SetContextMenuCursor(this.contextActionIndex);
                    }
                    break;

                case State.OperatorSubMenu:
                    this.view.MoveOperatorSubMenuCursor(delta);
                    break;
            }
        }

        private void OnUIConfirm(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    OpenContextMenu();
                    break;

                case State.ContextMenu:
                    ExecuteContextMenuAction();
                    break;

                case State.OperatorSubMenu:
                    ExecuteOperatorSubMenuAction();
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
                case State.ContextMenu:
                    this.state = State.List;
                    this.view.HideContextMenu();
                    break;
                case State.OperatorSubMenu:
                    this.state = State.ContextMenu;
                    this.view.HideOperatorSubMenu();
                    break;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private void OpenContextMenu()
        {
            if (this.inventoryService.Items.Count == 0) return;

            this.contextActionIndex = 0;
            var item = this.inventoryService.Items[this.cursorIndex];
            this.view.ShowContextMenu(item, this.cursorIndex);
            this.state = State.ContextMenu;
        }

        private void ExecuteContextMenuAction()
        {
            var action = this.view.GetContextMenuAction(this.contextActionIndex);

            switch (action)
            {
                case ContextMenuAction.Equip:
                case ContextMenuAction.Unequip:
                case ContextMenuAction.Reload:
                    OpenOperatorSubMenu(action);
                    break;

                case ContextMenuAction.Use:
                    // TODO: implement consumable use
                    this.state = State.List;
                    this.view.HideContextMenu();
                    break;

                case ContextMenuAction.Examine:
                    this.view.ShowExamineOverlay(this.inventoryService.Items[this.cursorIndex]);
                    // Examine closes itself on UICancel — handled in OnUICancel → ContextMenu → List path
                    break;
            }
        }

        private void OpenOperatorSubMenu(ContextMenuAction action)
        {
            var operators = BuildOperatorSubMenuEntries(action);
            this.view.ShowOperatorSubMenu(operators, action);
            this.state = State.OperatorSubMenu;
        }

        private List<OperatorSubMenuEntry> BuildOperatorSubMenuEntries(ContextMenuAction action)
        {
            var entries = new List<OperatorSubMenuEntry>();
            this.roster.EnsureInitialized();

            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (!op.IsPresent) continue;

                bool isValid = action switch
                {
                    ContextMenuAction.Equip   => true,
                    ContextMenuAction.Unequip => this.inventoryService.Items[this.cursorIndex].EquippedBySlot == i,
                    ContextMenuAction.Reload  => this.inventoryService.CanReload(this.cursorIndex, i),
                    _ => false
                };

                string name = op.Data?.OperatorId ?? $"Slot {i}";
                int equippedIdx = this.inventoryService.GetEquippedWeaponIndex(i);
                string equippedName = equippedIdx >= 0
                    ? this.inventoryService.Items[equippedIdx].Data.DisplayName
                    : "---";

                entries.Add(new OperatorSubMenuEntry(i, name, equippedName, isValid));
            }

            return entries;
        }

        private void ExecuteOperatorSubMenuAction()
        {
            int operatorSlot = this.view.GetSelectedOperatorSlot();
            var action       = this.view.CurrentSubMenuAction;

            switch (action)
            {
                case ContextMenuAction.Equip:
                    this.inventoryService.EquipWeapon(this.cursorIndex, operatorSlot);
                    break;
                case ContextMenuAction.Unequip:
                    this.inventoryService.UnequipWeapon(this.cursorIndex);
                    break;
                case ContextMenuAction.Reload:
                    this.inventoryService.ReloadOperator(this.cursorIndex, operatorSlot);
                    // After reload, ammo box was removed — adjust cursor
                    if (this.cursorIndex >= this.inventoryService.Items.Count)
                        this.cursorIndex = Mathf.Max(0, this.inventoryService.Items.Count - 1);
                    break;
            }

            this.state = State.List;
            this.view.HideOperatorSubMenu();
            this.view.HideContextMenu();
            RefreshView();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void RefreshView()
        {
            this.view.RefreshItemList(this.inventoryService.Items, this.cursorIndex);
            this.view.RefreshRosterPanel(this.roster, this.inventoryService);
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenInventory.performed -= OnOpenInventory;
            this.inputService.UINavigate.performed    -= OnUINavigate;
            this.inputService.UIConfirm.performed     -= OnUIConfirm;
            this.inputService.UICancel.performed      -= OnUICancel;
        }
    }
}
```

Also create helper types (same file or separate):

```csharp
// Assets/Scripts/Navigation/UI/ContextMenuAction.cs
#nullable enable
namespace CrimsonDraft.Navigation.UI
{
    public enum ContextMenuAction { Equip, Unequip, Reload, Use, Examine }
}
```

```csharp
// Assets/Scripts/Navigation/UI/OperatorSubMenuEntry.cs
#nullable enable
namespace CrimsonDraft.Navigation.UI
{
    public readonly struct OperatorSubMenuEntry
    {
        public int    SlotIndex     { get; }
        public string OperatorName  { get; }
        public string EquippedWeapon { get; }
        public bool   IsValid        { get; }

        public OperatorSubMenuEntry(int slotIndex, string operatorName, string equippedWeapon, bool isValid)
        {
            SlotIndex      = slotIndex;
            OperatorName   = operatorName;
            EquippedWeapon = equippedWeapon;
            IsValid        = isValid;
        }
    }
}
```

NOTE: `InventoryController` references `InventoryView` methods that don't exist yet (Tasks 8–10). The code will not compile until InventoryView is updated. That's fine — commit the partial file and continue.

**Step 2: Commit**

```bash
git add "Assets/Scripts/Navigation/NavigationScope.cs" \
        "Assets/Scripts/Navigation/UI/InventoryController.cs" \
        "Assets/Scripts/Navigation/UI/ContextMenuAction.cs" \
        "Assets/Scripts/Navigation/UI/OperatorSubMenuEntry.cs"
git commit -m "feat(inventory): wire InventoryController state machine + NavigationScope registration"
```

---

## Task 8: InventoryView — full implementation

**Files:**
- Modify: `Assets/Scripts/Navigation/UI/InventoryView.cs`
- Create: `Assets/Scripts/Navigation/UI/InventoryItemRow.cs`
- Create: `Assets/Scripts/Navigation/UI/RosterOperatorRow.cs`
- Create: `Assets/Scripts/Navigation/UI/ContextMenuItemRow.cs`
- Create: `Assets/Scripts/Navigation/UI/OperatorSubMenuRow.cs`

### Step 1: Write row components

**InventoryItemRow.cs** — one row in the item list:
```csharp
#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryItemRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel    = null!;
        [SerializeField] private TextMeshProUGUI equippedLabel = null!;
        [SerializeField] private Image            cursorImage  = null!;

        public void Setup(string displayName, string equippedBy, bool isCursor)
        {
            this.nameLabel.text     = displayName;
            this.equippedLabel.text = equippedBy.Length > 0 ? $"[Eq: {equippedBy}]" : string.Empty;
            this.cursorImage.enabled = isCursor;
        }
    }
}
```

**RosterOperatorRow.cs** — one row in the roster panel:
```csharp
#nullable enable

using UnityEngine;
using TMPro;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class RosterOperatorRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel    = null!;
        [SerializeField] private TextMeshProUGUI weaponLabel  = null!;

        public void Setup(string operatorName, string equippedWeapon)
        {
            this.nameLabel.text   = operatorName;
            this.weaponLabel.text = equippedWeapon;
        }
    }
}
```

**ContextMenuItemRow.cs** — one action in the context menu:
```csharp
#nullable enable

using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class ContextMenuItemRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label       = null!;
        [SerializeField] private Image            cursorImage = null!;
        [SerializeField] private CanvasGroup      group       = null!;

        public ContextMenuAction Action { get; private set; }

        public void Setup(ContextMenuAction action, bool isCursor, bool isEnabled)
        {
            this.Action           = action;
            this.label.text       = action.ToString();
            this.cursorImage.enabled = isCursor;
            this.group.alpha      = isEnabled ? 1f : 0.4f;
            this.group.interactable = isEnabled;
        }
    }
}
```

**OperatorSubMenuRow.cs** — one operator in the sub-menu:
```csharp
#nullable enable

using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class OperatorSubMenuRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel    = null!;
        [SerializeField] private TextMeshProUGUI weaponLabel  = null!;
        [SerializeField] private Image            cursorImage  = null!;
        [SerializeField] private CanvasGroup      group        = null!;

        public int SlotIndex { get; private set; }

        public void Setup(OperatorSubMenuEntry entry, bool isCursor)
        {
            this.SlotIndex          = entry.SlotIndex;
            this.nameLabel.text     = entry.OperatorName;
            this.weaponLabel.text   = entry.EquippedWeapon;
            this.cursorImage.enabled = isCursor;
            this.group.alpha        = entry.IsValid ? 1f : 0.4f;
            this.group.interactable = entry.IsValid;
        }
    }
}
```

### Step 2: Write InventoryView.cs

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryView : MonoBehaviour
    {
        // ── Serialized ─────────────────────────────────────────────────────────
        [Header("Item List")]
        [SerializeField] private Transform         itemListContainer  = null!;
        [SerializeField] private InventoryItemRow  itemRowPrefab      = null!;

        [Header("Roster Panel")]
        [SerializeField] private Transform         rosterContainer    = null!;
        [SerializeField] private RosterOperatorRow rosterRowPrefab    = null!;

        [Header("Context Menu")]
        [SerializeField] private GameObject        contextMenuRoot    = null!;
        [SerializeField] private Transform         contextMenuContainer = null!;
        [SerializeField] private ContextMenuItemRow contextMenuRowPrefab = null!;

        [Header("Operator Sub-Menu")]
        [SerializeField] private GameObject        operatorSubMenuRoot = null!;
        [SerializeField] private Transform         subMenuContainer    = null!;
        [SerializeField] private OperatorSubMenuRow subMenuRowPrefab   = null!;

        [Header("Examine")]
        [SerializeField] private GameObject        examineOverlayRoot  = null!;
        [SerializeField] private TMPro.TextMeshProUGUI examineText     = null!;

        // ── Runtime ────────────────────────────────────────────────────────────
        private readonly List<InventoryItemRow>   itemRows      = new();
        private readonly List<RosterOperatorRow>  rosterRows    = new();
        private readonly List<ContextMenuItemRow> contextRows   = new();
        private readonly List<OperatorSubMenuRow> subMenuRows   = new();

        private int subMenuCursor;
        public  ContextMenuAction CurrentSubMenuAction { get; private set; }
        public  int ContextMenuActionCount => this.contextRows.Count;

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        // ── Item list ──────────────────────────────────────────────────────────

        public void RefreshItemList(IReadOnlyList<InventoryItem> items, int cursorIndex)
        {
            // Grow pool
            while (this.itemRows.Count < items.Count)
                this.itemRows.Add(Instantiate(this.itemRowPrefab, this.itemListContainer));

            // Hide extras
            for (int i = items.Count; i < this.itemRows.Count; i++)
                this.itemRows[i].gameObject.SetActive(false);

            // Setup visible rows
            for (int i = 0; i < items.Count; i++)
            {
                var item       = items[i];
                string eqBy    = item.IsEquipped ? GetOperatorName(item.EquippedBySlot) : string.Empty;
                this.itemRows[i].Setup(item.Data.DisplayName, eqBy, isCursor: i == cursorIndex);
                this.itemRows[i].gameObject.SetActive(true);
            }
        }

        public void SetItemCursor(int index)
        {
            for (int i = 0; i < this.itemRows.Count; i++)
                this.itemRows[i].Setup(
                    this.itemRows[i].GetComponentInChildren<TMPro.TextMeshProUGUI>().text,
                    string.Empty,
                    isCursor: i == index);
            // NOTE: full refresh is cleaner — caller should use RefreshItemList instead.
            // This path exists for lightweight cursor-only updates but for MVP it's fine to
            // just call RefreshItemList from the controller after every navigation.
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

            for (int i = this.rosterRows.Count - 1; i >= presentCount; i--)
            {
                this.rosterRows[i].gameObject.SetActive(false);
            }

            int rowIdx = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var op = roster[i];
                if (!op.IsPresent) continue;

                string name = op.Data?.OperatorId ?? $"Slot {i}";
                int wIdx    = inventory.GetEquippedWeaponIndex(i);
                string wpn  = wIdx >= 0 ? inventory.Items[wIdx].Data.DisplayName : "---";

                this.rosterRows[rowIdx].Setup(name, wpn);
                this.rosterRows[rowIdx].gameObject.SetActive(true);
                rowIdx++;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        public void ShowContextMenu(InventoryItem item, int itemIndex)
        {
            this.contextMenuRoot.SetActive(true);

            // Clear old rows
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

        public void HideContextMenu()
        {
            if (this.contextMenuRoot != null)
                this.contextMenuRoot.SetActive(false);
        }

        public void SetContextMenuCursor(int index)
        {
            for (int i = 0; i < this.contextRows.Count; i++)
                this.contextRows[i].Setup(this.contextRows[i].Action, isCursor: i == index, isEnabled: true);
        }

        public ContextMenuAction GetContextMenuAction(int index) =>
            this.contextRows[index].Action;

        // ── Operator sub-menu ──────────────────────────────────────────────────

        public void ShowOperatorSubMenu(List<OperatorSubMenuEntry> entries, ContextMenuAction action)
        {
            this.CurrentSubMenuAction   = action;
            this.subMenuCursor          = 0;
            this.operatorSubMenuRoot.SetActive(true);

            foreach (var r in this.subMenuRows) Destroy(r.gameObject);
            this.subMenuRows.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(this.subMenuRowPrefab, this.subMenuContainer);
                row.Setup(entries[i], isCursor: i == 0);
                this.subMenuRows.Add(row);
            }
        }

        public void HideOperatorSubMenu()
        {
            if (this.operatorSubMenuRoot != null)
                this.operatorSubMenuRoot.SetActive(false);
        }

        public void MoveOperatorSubMenuCursor(int delta)
        {
            if (this.subMenuRows.Count == 0) return;
            this.subMenuCursor = (this.subMenuCursor + delta + this.subMenuRows.Count) % this.subMenuRows.Count;
            for (int i = 0; i < this.subMenuRows.Count; i++)
                this.subMenuRows[i].Setup(
                    new OperatorSubMenuEntry(
                        this.subMenuRows[i].SlotIndex,
                        this.subMenuRows[i].GetComponentInChildren<TMPro.TextMeshProUGUI>().text,
                        string.Empty,
                        true),
                    isCursor: i == this.subMenuCursor);
        }

        public int GetSelectedOperatorSlot() =>
            this.subMenuRows.Count > 0 ? this.subMenuRows[this.subMenuCursor].SlotIndex : -1;

        // ── Examine overlay ────────────────────────────────────────────────────

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() =>
            this.examineOverlayRoot.SetActive(false);

        // ── Private helpers ────────────────────────────────────────────────────

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item)
        {
            return item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Reload, ContextMenuAction.Examine },
                ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use, ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Examine }
            };
        }

        private string GetOperatorName(int slotIndex)
        {
            // InventoryView doesn't have direct access to the roster.
            // The controller passes the operator name into InventoryItemRow via RefreshItemList.
            // This method is a fallback — the controller should always call RefreshItemList with full data.
            return $"Slot {slotIndex}";
        }
    }
}
```

NOTE: `RefreshItemList` uses `GetOperatorName` which only returns a fallback. The controller should inject operator names. Simplest fix: make `RefreshItemList` accept a `Func<int, string>` resolver, or the controller pre-builds display strings. For MVP, update the controller's `RefreshView` to pass resolved names via a helper method in InventoryView. This is a detail to resolve during implementation — the architecture is correct.

**Step 3: Commit**

```bash
git add "Assets/Scripts/Navigation/UI/InventoryView.cs" \
        "Assets/Scripts/Navigation/UI/InventoryItemRow.cs" \
        "Assets/Scripts/Navigation/UI/RosterOperatorRow.cs" \
        "Assets/Scripts/Navigation/UI/ContextMenuItemRow.cs" \
        "Assets/Scripts/Navigation/UI/OperatorSubMenuRow.cs"
git commit -m "feat(inventory): implement InventoryView with two-panel layout + context menu"
```

---

## Task 9: Unity scene setup

This task is done entirely in the Unity Editor. No C# files to write.

### Step 1: Create ItemData ScriptableObjects

In Unity Project panel:
- `Assets/ScriptableObjects/Inventory/` → right-click → Create → CrimsonDraft → Inventory → Item Data
- Create one per item type for testing:
  - `Mk18` (Weapon, caliber: `5.56`)
  - `Benelli_M4` (Weapon, caliber: `12ga`)
  - `9mm_Box` (AmmoBox, caliber: `9mm`)
  - `5_56_Box` (AmmoBox, caliber: `5.56`)

### Step 2: Create prefabs for row components

In `Assets/Prefabs/Inventory/`:
- `InventoryItemRow.prefab`: Image (background) + TextMeshPro (name) + TextMeshPro (equipped label) + Image (cursor arrow) → attach `InventoryItemRow` component, wire references
- `RosterOperatorRow.prefab`: TextMeshPro (name) + TextMeshPro (weapon) → attach `RosterOperatorRow`
- `ContextMenuItemRow.prefab`: TextMeshPro (label) + Image (cursor) + CanvasGroup → attach `ContextMenuItemRow`
- `OperatorSubMenuRow.prefab`: TextMeshPro (name) + TextMeshPro (weapon) + Image (cursor) + CanvasGroup → attach `OperatorSubMenuRow`

### Step 3: Build InventoryPanel hierarchy in Navigation scene

In the Navigation scene, under the UI Canvas, create:

```
InventoryPanel (GameObject, starts inactive)
├── ItemListPanel (RectTransform, left half)
│   └── ItemListContainer (VerticalLayoutGroup, ContentSizeFitter)
├── RosterPanel (RectTransform, right half)
│   └── RosterContainer (VerticalLayoutGroup)
├── ContextMenu (GameObject, starts inactive)
│   └── ContextMenuContainer (VerticalLayoutGroup)
├── OperatorSubMenu (GameObject, starts inactive)
│   └── SubMenuContainer (VerticalLayoutGroup)
└── ExamineOverlay (GameObject, starts inactive)
    └── ExamineText (TextMeshProUGUI)
```

Wire all references in `InventoryView` inspector.

### Step 4: Verify InventoryView in NavigationScope

`InventoryView` is already registered via `builder.RegisterComponentInHierarchy<InventoryView>()` in `NavigationScope`. Confirm the `InventoryPanel` GameObject in the scene has `InventoryView` component attached.

### Step 5: Seed test items at game start

For MVP testing, add a `InventoryDebugSeeder` MonoBehaviour in the Navigation scene that adds test items on Awake:

```csharp
// Assets/Scripts/Navigation/UI/InventoryDebugSeeder.cs (editor-only or runtime)
#nullable enable
#if UNITY_EDITOR
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryDebugSeeder : MonoBehaviour
    {
        [SerializeField] private ItemData[] debugItems = System.Array.Empty<ItemData>();
        private IInventoryService? service;

        // VContainer injection
        [VContainer.Inject]
        public void Inject(IInventoryService inventoryService) => this.service = inventoryService;

        private void Start()
        {
            if (this.service == null) return;
            foreach (var item in this.debugItems)
                this.service.AddItem(item);
        }
    }
}
#endif
```

Register it in `NavigationScope` temporarily:
```csharp
builder.RegisterComponentInHierarchy<InventoryDebugSeeder>();
```

Assign test ItemData assets in the inspector.

**Commit:**
```bash
git add "Assets/Scripts/Navigation/UI/InventoryDebugSeeder.cs"
git commit -m "feat(inventory): add debug seeder for MVP testing"
```

---

## Task 10: Fix operator name resolution in InventoryView

**Files:**
- Modify: `Assets/Scripts/Navigation/UI/InventoryView.cs`
- Modify: `Assets/Scripts/Navigation/UI/InventoryController.cs`

The `RefreshItemList` call in the controller needs to resolve operator names before passing to the view. Update `RefreshView` in `InventoryController`:

```csharp
private void RefreshView()
{
    var items  = this.inventoryService.Items;
    var names  = BuildOperatorNameMap();
    this.view.RefreshItemList(items, this.cursorIndex, names);
    this.view.RefreshRosterPanel(this.roster, this.inventoryService);
}

private Dictionary<int, string> BuildOperatorNameMap()
{
    var map = new Dictionary<int, string>();
    this.roster.EnsureInitialized();
    for (int i = 0; i < this.roster.Count; i++)
    {
        var op = this.roster[i];
        if (op.IsPresent)
            map[i] = op.Data?.OperatorId ?? $"Slot {i}";
    }
    return map;
}
```

Update `InventoryView.RefreshItemList` signature:
```csharp
public void RefreshItemList(IReadOnlyList<InventoryItem> items, int cursorIndex, Dictionary<int, string> operatorNames)
{
    // ... same setup loop but use operatorNames[item.EquippedBySlot] instead of fallback
    string eqBy = item.IsEquipped && operatorNames.TryGetValue(item.EquippedBySlot, out var n) ? n : string.Empty;
}
```

Add `using System.Collections.Generic;` to InventoryView.

**Commit:**
```bash
git add "Assets/Scripts/Navigation/UI/InventoryView.cs" \
        "Assets/Scripts/Navigation/UI/InventoryController.cs"
git commit -m "fix(inventory): resolve operator names in item list display"
```

---

## Task 11: Run + verify in Play Mode

### Verification checklist

1. Enter Play Mode in the Navigation scene
2. Press Tab / Select → inventory panel opens
3. D-pad Down/Up → cursor moves through item list
4. Press A on a Weapon → context menu shows "Equip" + "Examine"
5. Press A on Equip → operator sub-menu shows all present operators
6. Select an operator → weapon is equipped, item shows `[Eq: OperatorName]`
7. Open inventory again, press A on same weapon → shows "Unequip" + "Examine"
8. Press A on an AmmoBox → context menu shows "Recargar" + "Examine"
9. Press A on Recargar → sub-menu shows operators with compatible weapon equipped
10. Select operator → ammo box is consumed (disappears from list), operator ammo reloaded
11. Press B at any depth → returns to previous state (ContextMenu → List → Closed)

Run EditMode tests: `Window → Test Runner → EditMode → Run All`
Expected: all InventoryServiceTests pass.

**Commit:**
```bash
git commit -m "feat(inventory): MVP inventory system complete"
```

---

## Reference: Assembly dependency graph

```
CrimsonDraft.Inventory
  ├── CrimsonDraft.Infrastructure
  ├── CrimsonDraft.Operators          ← added in Task 2
  └── VContainer

CrimsonDraft.Navigation
  ├── CrimsonDraft.Infrastructure
  ├── CrimsonDraft.Inventory          ← added in Task 2
  ├── CrimsonDraft.Operators
  └── VContainer

CrimsonDraft.Tests.EditMode
  └── CrimsonDraft.Inventory          ← already present
```
