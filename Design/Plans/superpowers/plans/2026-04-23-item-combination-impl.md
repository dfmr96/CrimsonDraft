# Item Combination System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** [docs/superpowers/specs/2026-04-23-item-combination-design.md](../specs/2026-04-23-item-combination-design.md)  
**GDD:** Sistema de Combinación de Ítems

**Goal:** Add predefined-recipe item combination to the inventory — players select two items, the system checks a ScriptableObject recipe library, consumes both, and produces the result.

**Architecture:** Three layers — (1) data: `CombineRecipe` struct + `CombineRecipeLibrary` SO; (2) service: `ICombineService`/`CombineService` with symmetric dict lookup, injected into `InventoryService.TryCombine`; (3) UI: `InventoryController` Combine state machine + visual feedback via `InventoryView`/`InventorySlotCell`.

**Tech Stack:** Unity 2D, C#, VContainer (DI), NUnit (Edit Mode tests), Unity Test Runner

---

## File Map

| Action | File |
|---|---|
| Create | `Assets/Scripts/Inventory/CombineRecipe.cs` |
| Create | `Assets/Scripts/Inventory/CombineRecipeLibrary.cs` |
| Create | `Assets/Scripts/Inventory/ICombineService.cs` |
| Create | `Assets/Scripts/Inventory/CombineService.cs` |
| Modify | `Assets/Scripts/Inventory/IInventoryService.cs` |
| Modify | `Assets/Scripts/Inventory/InventoryService.cs` |
| Modify | `Assets/Scripts/Navigation/NavigationScope.cs` |
| Modify | `Assets/Scripts/Navigation/UI/InventoryController.cs` |
| Modify | `Assets/Scripts/Navigation/UI/InventoryView.cs` |
| Modify | `Assets/Scripts/Navigation/UI/InventorySlotCell.cs` |
| Modify | `Assets/Scripts/Navigation/UI/OperatorInventoryCard.cs` |
| Create | `Assets/Tests/EditMode/CombineServiceTests.cs` |
| Modify | `Assets/Tests/EditMode/InventoryServiceTests.cs` |

All paths are relative to `Game/CrimsonDraft/`.

---

## Task 1: CombineRecipe struct + CombineRecipeLibrary ScriptableObject

**Files:**
- Create: `Assets/Scripts/Inventory/CombineRecipe.cs`
- Create: `Assets/Scripts/Inventory/CombineRecipeLibrary.cs`

These are pure data assets — no tests needed. The recipe library is the designer-facing asset that holds all recipes.

- [ ] **Step 1: Create CombineRecipe.cs**

```csharp
#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [Serializable]
    public struct CombineRecipe
    {
        [SerializeField] private ItemData inputA;
        [SerializeField] private ItemData inputB;
        [SerializeField] private ItemData output;

        public ItemData InputA => this.inputA;
        public ItemData InputB => this.inputB;
        public ItemData Output => this.output;
    }
}
```

- [ ] **Step 2: Create CombineRecipeLibrary.cs**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Combine Recipe Library", fileName = "CombineRecipeLibrary")]
    public sealed class CombineRecipeLibrary : ScriptableObject
    {
        [SerializeField] private List<CombineRecipe> recipes = new();

        public IReadOnlyList<CombineRecipe> Recipes => this.recipes;
    }
}
```

- [ ] **Step 3: Check compilation in Unity**

Open Unity Console and wait for compilation to complete. Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/CombineRecipe.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/CombineRecipeLibrary.cs
git commit -m "feat(inventory): add CombineRecipe struct and CombineRecipeLibrary ScriptableObject"
```

---

## Task 2: ICombineService + CombineService

**Files:**
- Create: `Assets/Scripts/Inventory/ICombineService.cs`
- Create: `Assets/Scripts/Inventory/CombineService.cs`
- Create: `Assets/Tests/EditMode/CombineServiceTests.cs`

- [ ] **Step 1: Create ICombineService.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public interface ICombineService
    {
        /// <summary>Returns the output ItemData if a recipe exists for (a, b). Symmetric — order does not matter. Returns null if no recipe.</summary>
        ItemData? TryGetResult(ItemData a, ItemData b);
    }
}
```

- [ ] **Step 2: Write CombineServiceTests.cs (failing — CombineService does not exist yet)**

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class CombineServiceTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static ConsumableData MakeItem(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static CombineRecipeLibrary MakeLibrary(params (ItemData a, ItemData b, ItemData output)[] recipes)
        {
            var lib  = ScriptableObject.CreateInstance<CombineRecipeLibrary>();
            var so   = new SerializedObject(lib);
            var prop = so.FindProperty("recipes");
            prop.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("inputA").objectReferenceValue = recipes[i].a;
                elem.FindPropertyRelative("inputB").objectReferenceValue = recipes[i].b;
                elem.FindPropertyRelative("output").objectReferenceValue = recipes[i].output;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return lib;
        }

        private static CombineService MakeService(CombineRecipeLibrary lib)
        {
            var svc = new CombineService(lib);
            ((IInitializable)svc).Initialize();
            return svc;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void TryGetResult_returnsOutput_whenRecipeExists()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var docs = MakeItem("documents");
            var svc  = MakeService(MakeLibrary((key, port, docs)));

            var result = svc.TryGetResult(key, port);

            Assert.AreEqual(docs, result);
        }

        [Test]
        public void TryGetResult_isSymmetric_samResultRegardlessOfOrder()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var docs = MakeItem("documents");
            var svc  = MakeService(MakeLibrary((key, port, docs)));

            Assert.AreEqual(docs, svc.TryGetResult(key,  port));
            Assert.AreEqual(docs, svc.TryGetResult(port, key));
        }

        [Test]
        public void TryGetResult_returnsNull_whenNoRecipeExists()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var svc  = MakeService(MakeLibrary()); // empty library

            Assert.IsNull(svc.TryGetResult(key, port));
        }

        [Test]
        public void TryGetResult_returnsNull_whenOnlyOneInputMatches()
        {
            var key     = MakeItem("key");
            var port    = MakeItem("portfolio");
            var other   = MakeItem("other");
            var docs    = MakeItem("documents");
            var svc     = MakeService(MakeLibrary((key, port, docs)));

            Assert.IsNull(svc.TryGetResult(key, other));
        }

        [Test]
        public void TryGetResult_supportsMultipleRecipes()
        {
            var a1 = MakeItem("a1"); var b1 = MakeItem("b1"); var c1 = MakeItem("c1");
            var a2 = MakeItem("a2"); var b2 = MakeItem("b2"); var c2 = MakeItem("c2");
            var svc = MakeService(MakeLibrary((a1, b1, c1), (a2, b2, c2)));

            Assert.AreEqual(c1, svc.TryGetResult(a1, b1));
            Assert.AreEqual(c2, svc.TryGetResult(a2, b2));
        }
    }
}
```

- [ ] **Step 3: Run tests — expect compile error (CombineService not defined)**

Open Unity Test Runner (Window > General > Test Runner > EditMode). All `CombineServiceTests` should fail to compile.

- [ ] **Step 4: Create CombineService.cs**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Inventory
{
    public sealed class CombineService : ICombineService, IInitializable
    {
        private readonly CombineRecipeLibrary                  library;
        private readonly Dictionary<(string, string), ItemData> lookup = new();

        [Preserve]
        public CombineService(CombineRecipeLibrary library) => this.library = library;

        void IInitializable.Initialize()
        {
            this.lookup.Clear();
            foreach (var recipe in this.library.Recipes)
            {
                var key = MakeKey(recipe.InputA.ItemId, recipe.InputB.ItemId);
                this.lookup[key] = recipe.Output;
            }
        }

        public ItemData? TryGetResult(ItemData a, ItemData b)
        {
            var key = MakeKey(a.ItemId, b.ItemId);
            return this.lookup.TryGetValue(key, out var result) ? result : null;
        }

        private static (string, string) MakeKey(string idA, string idB) =>
            string.Compare(idA, idB, StringComparison.Ordinal) <= 0
                ? (idA, idB)
                : (idB, idA);
    }
}
```

- [ ] **Step 5: Run CombineServiceTests — expect all 5 to pass**

Open Unity Test Runner > EditMode. Run `CombineServiceTests`. Expected: 5 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ICombineService.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/CombineService.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombineServiceTests.cs
git commit -m "feat(inventory): add ICombineService and CombineService with symmetric recipe lookup"
```

---

## Task 3: IInventoryService.TryCombine + InventoryService implementation

**Files:**
- Modify: `Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Add TryCombine to IInventoryService.cs**

Add after the `ReloadOperator` method at line 44:

```csharp
        /// <summary>Checks ICombineService for a recipe matching the items in slotA and slotB.
        /// If found: removes both items and places the result in the first available slot.
        /// Returns false if either slot is empty, same slot, or no recipe exists.</summary>
        bool TryCombine(int slotA, int slotB);
```

- [ ] **Step 2: Write TryCombine tests in InventoryServiceTests.cs (failing)**

First add a `NullCombineService` and a `FakeCombineService` private class, and update the `MakeService` helper at the top of the test class. Then replace every `new InventoryService(new FakeRoster(...))` call with `MakeService(...)`.

Add these private classes inside `InventoryServiceTests` (after `FakeRoster`):

```csharp
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
```

Replace all `new InventoryService(new FakeRoster(...))` occurrences with `MakeService(new FakeRoster(...))`. Example:

```csharp
// Before:
var service = new InventoryService(new FakeRoster(MakeAlive(0)));
// After:
var service = MakeService(new FakeRoster(MakeAlive(0)));
```

There are approximately 15 such calls — replace all of them throughout the file.

Then add a `MakeConsumableData` helper after `MakeAmmoBoxData`:

```csharp
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
```

Then add a new test section `// ── TryCombine ─────────────────────────────────────────────────────────────` with these tests:

```csharp
        [Test]
        public void TryCombine_returnsTrue_consumesBothInputs_addsResult()
        {
            var itemA   = MakeConsumableData("key");
            var itemB   = MakeConsumableData("portfolio");
            var output  = MakeConsumableData("documents");
            var combine = new FakeCombineService(itemA, itemB, output);
            var service = MakeService(new FakeRoster(MakeAlive(0)), combine);
            service.AddItem(itemA, operatorSlot: 0);
            service.AddItem(itemB, operatorSlot: 0);

            bool result = service.TryCombine(0, 1);

            Assert.IsTrue(result);
            Assert.IsTrue(service.Slots[0].IsEmpty,  "slotA consumed");
            Assert.IsTrue(service.Slots[1].IsEmpty,  "slotB consumed");
            Assert.IsFalse(service.Slots[2].IsEmpty, "result placed in next free slot");
            Assert.AreEqual(output.ItemId, service.Slots[2].Item!.Data.ItemId);
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

            // Order A+B
            var s1 = MakeService(new FakeRoster(MakeAlive(0)), combine);
            s1.AddItem(itemA, operatorSlot: 0);
            s1.AddItem(itemB, operatorSlot: 0);
            Assert.IsTrue(s1.TryCombine(0, 1), "A+B");

            // Order B+A
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
            var combine = new FakeCombineService(itemA, itemB, output);
            var service = MakeService(new FakeRoster(MakeAlive(0)), combine);
            service.AddItem(itemA, operatorSlot: 0);
            // slot 1 is empty

            bool result = service.TryCombine(0, 1);

            Assert.IsFalse(result);
            Assert.IsFalse(service.Slots[0].IsEmpty, "slotA untouched");
        }

        [Test]
        public void TryCombine_placesResult_inFirstAvailableSlotAcrossOperators()
        {
            // op0 slots 0-1 occupied by inputs, slots 2-3 also occupied — result goes to op1
            var itemA  = MakeConsumableData("key");
            var itemB  = MakeConsumableData("portfolio");
            var filler = MakeConsumableData("filler");
            var output = MakeConsumableData("documents");
            var svc    = MakeService(new FakeRoster(MakeAlive(0), MakeAlive(1)), new FakeCombineService(itemA, itemB, output));
            svc.AddItem(itemA,  operatorSlot: 0); // slot 0
            svc.AddItem(itemB,  operatorSlot: 0); // slot 1
            svc.AddItem(filler, operatorSlot: 0); // slot 2
            svc.AddItem(filler, operatorSlot: 0); // slot 3

            svc.TryCombine(0, 1);

            // slots 0 and 1 freed, slots 2 and 3 still occupied → result in op1 slot 4
            Assert.IsTrue(service: svc, condition: svc.Slots[0].IsEmpty);
            Assert.IsTrue(svc.Slots[1].IsEmpty);
            Assert.IsFalse(svc.Slots[4].IsEmpty, "result placed in op1 slot 0");
            Assert.AreEqual(output.ItemId, svc.Slots[4].Item!.Data.ItemId);
        }
```

Note: the last test has a helper method conflict — fix the `Assert.IsTrue(service: svc, ...)` to `Assert.IsTrue(svc.Slots[0].IsEmpty)`.

Corrected last test:

```csharp
        [Test]
        public void TryCombine_placesResult_inFirstAvailableSlotAcrossOperators()
        {
            var itemA  = MakeConsumableData("key");
            var itemB  = MakeConsumableData("portfolio");
            var filler = MakeConsumableData("filler");
            var output = MakeConsumableData("documents");
            var svc    = MakeService(new FakeRoster(MakeAlive(0), MakeAlive(1)), new FakeCombineService(itemA, itemB, output));
            svc.AddItem(itemA,  operatorSlot: 0); // slot 0
            svc.AddItem(itemB,  operatorSlot: 0); // slot 1
            svc.AddItem(filler, operatorSlot: 0); // slot 2
            svc.AddItem(filler, operatorSlot: 0); // slot 3

            svc.TryCombine(0, 1);

            Assert.IsTrue(svc.Slots[0].IsEmpty);
            Assert.IsTrue(svc.Slots[1].IsEmpty);
            Assert.IsFalse(svc.Slots[4].IsEmpty, "result in op1 slot 0 (index 4)");
            Assert.AreEqual(output.ItemId, svc.Slots[4].Item!.Data.ItemId);
        }
```

- [ ] **Step 3: Run InventoryServiceTests — expect compile error (TryCombine not implemented)**

Run all InventoryServiceTests in Unity Test Runner. Expected: compile error on `TryCombine` call.

- [ ] **Step 4: Update InventoryService.cs — add ICombineService dependency and TryCombine**

Update the constructor and add the new field:

```csharp
        private readonly IOperatorRoster  roster;
        private readonly ICombineService  combineService;
        private InventorySlot[]?          slots;

        [Preserve]
        public InventoryService(IOperatorRoster roster, ICombineService combineService)
        {
            this.roster         = roster;
            this.combineService = combineService;
        }
```

Add `TryCombine` at the end of the class, before the closing `}`:

```csharp
        public bool TryCombine(int slotA, int slotB)
        {
            var s = EnsureSlots();
            if (s[slotA].IsEmpty || s[slotB].IsEmpty) return false;
            var result = this.combineService.TryGetResult(s[slotA].Item!.Data, s[slotB].Item!.Data);
            if (result == null) return false;
            RemoveItem(slotA);
            RemoveItem(slotB);
            AddItemAuto(result);
            return true;
        }
```

- [ ] **Step 5: Run all InventoryServiceTests — expect all to pass**

Run all tests in `InventoryServiceTests` in Unity Test Runner. Expected: all existing tests + 5 new TryCombine tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs
git commit -m "feat(inventory): add TryCombine to IInventoryService and InventoryService"
```

---

## Task 4: InventoryController — Combine state

**Files:**
- Modify: `Assets/Scripts/Navigation/UI/InventoryController.cs`

No unit tests — this is a UI controller that wires input to services. Verify manually in Play Mode.

- [ ] **Step 1: Add State.Combine and combineSourceSlot field**

Locate the `State` enum (line 16) and add `Combine`:

```csharp
        private enum State { Closed, List, Reorder, ContextMenu, Combine }
```

Locate the field declarations (lines 23-26) and add:

```csharp
        private int   combineSourceSlot = -1;
```

Full field block after the change:

```csharp
        private State state              = State.Closed;
        private int   cursorSlotIndex;
        private int   liftedSlotIndex    = -1;
        private int   combineSourceSlot  = -1;
        private int   contextActionIndex;
```

- [ ] **Step 1b: Update OnUINavigate to handle State.Combine**

In `OnUINavigate`, the cursor must move while in Combine mode. Add `State.Combine` to the existing `List`/`Reorder` case:

```csharp
        private void OnUINavigate(InputAction.CallbackContext ctx)
        {
            var dir = ctx.ReadValue<Vector2>();
            int dx  = dir.x > 0.5f ? 1 : dir.x < -0.5f ? -1 : 0;
            int dy  = dir.y < -0.5f ? 1 : dir.y > 0.5f ? -1 : 0;

            switch (this.state)
            {
                case State.List:
                case State.Reorder:
                case State.Combine:
                {
                    if (dx == 0 && dy == 0) return;
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
```

- [ ] **Step 2: Update RefreshView to pass combineSourceSlot**

Replace the existing `RefreshView` method (line 219):

```csharp
        private void RefreshView() =>
            this.view.RefreshSlots(this.inventoryService.Slots, this.cursorSlotIndex, this.liftedSlotIndex, this.combineSourceSlot);
```

- [ ] **Step 3: Update OnUIConfirm to handle State.Combine**

Replace the `OnUIConfirm` method body:

```csharp
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
                case State.Combine:
                    AttemptCombination();
                    break;
            }
        }
```

- [ ] **Step 4: Update OnUICancel to handle State.Combine**

Replace the `OnUICancel` method body:

```csharp
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
                case State.Combine:
                    this.combineSourceSlot = -1;
                    this.state             = State.List;
                    RefreshView();
                    break;
            }
        }
```

- [ ] **Step 5: Update ExecuteContextMenuAction to handle Combine early-exit**

Replace the entire `ExecuteContextMenuAction` method:

```csharp
        private void ExecuteContextMenuAction()
        {
            var action  = this.view.GetContextMenuAction(this.contextActionIndex);
            int ownerOp = this.cursorSlotIndex / 4;

            this.view.HideContextMenu();

            if (action == ContextMenuAction.Combine)
            {
                this.combineSourceSlot = this.cursorSlotIndex;
                this.state             = State.Combine;
                RefreshView();
                return;
            }

            this.state = State.List;

            switch (action)
            {
                case ContextMenuAction.Equip:
                    this.inventoryService.EquipWeapon(this.cursorSlotIndex, ownerOp);
                    break;

                case ContextMenuAction.Unequip:
                    this.inventoryService.UnequipWeapon(this.cursorSlotIndex);
                    break;

                case ContextMenuAction.Use:
                    break;

                case ContextMenuAction.Examine:
                    var item = this.inventoryService.Slots[this.cursorSlotIndex].Item;
                    if (item != null) this.view.ShowExamineOverlay(item);
                    return;
            }

            RefreshView();
        }
```

- [ ] **Step 6: Add AttemptCombination method**

Add after `CancelReorder()`:

```csharp
        private void AttemptCombination()
        {
            if (this.cursorSlotIndex == this.combineSourceSlot) return;
            var slot = this.inventoryService.Slots[this.cursorSlotIndex];
            if (slot.IsEmpty) return;
            if (!this.inventoryService.TryCombine(this.combineSourceSlot, this.cursorSlotIndex)) return;
            this.combineSourceSlot = -1;
            this.state             = State.List;
            RefreshView();
        }
```

- [ ] **Step 7: Check Unity compilation — no errors**

Wait for Unity to compile. Expected: no errors in Console.

- [ ] **Step 8: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs"
git commit -m "feat(inventory): add Combine state to InventoryController"
```

---

## Task 5: Visual feedback — InventoryView + InventorySlotCell + OperatorInventoryCard

**Files:**
- Modify: `Assets/Scripts/Navigation/UI/InventoryView.cs`
- Modify: `Assets/Scripts/Navigation/UI/InventorySlotCell.cs`
- Modify: `Assets/Scripts/Navigation/UI/OperatorInventoryCard.cs`

No unit tests — MonoBehaviour visual components. Verify in Play Mode.

- [ ] **Step 1: Update InventorySlotCell.cs — add isCombineSource parameter and color**

Replace the entire file content:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// Grid position marker for one inventory slot.
    /// Only shows occupied vs empty state — cursor, item info,
    /// and lifted icon are handled externally by InventoryView.
    /// </summary>
    public sealed class InventorySlotCell : MonoBehaviour
    {
        [SerializeField] private Image background = null!;
        [SerializeField] private Image iconImage   = null!;

        [SerializeField] private Color emptyColor        = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color occupiedColor     = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color combineSourceColor = new Color(1f, 0.5f, 0f, 0.6f);

        public RectTransform RectTransform => (RectTransform)transform;

        public void Setup(InventorySlot slot, bool isCombineSource = false)
        {
            Color bgColor = isCombineSource  ? this.combineSourceColor
                          : slot.IsEmpty      ? this.emptyColor
                          :                     this.occupiedColor;
            this.background.color  = bgColor;
            this.iconImage.sprite  = slot.IsEmpty ? null : slot.Item!.Data.Icon;
            this.iconImage.enabled = !slot.IsEmpty && slot.Item!.Data.Icon != null;
        }
    }
}
```

- [ ] **Step 2: Update OperatorInventoryCard.cs — pass combineSourceSlot to cells**

Replace only the `RefreshSlots` method (lines 50-66):

```csharp
        public void RefreshSlots(IReadOnlyList<InventorySlot> allSlots, int combineSourceSlot = -1)
        {
            int start = this.operatorSlotIndex * 4;
            int count = Mathf.Min(4, allSlots.Count - start);
            if (count <= 0) return;

            while (this.cells.Count < count)
                this.cells.Add(Instantiate(this.cellPrefab, this.slotsContainer));

            for (int i = 0; i < this.cells.Count; i++)
                this.cells[i].gameObject.SetActive(i < count);

            for (int i = 0; i < count; i++)
                this.cells[i].Setup(allSlots[start + i], isCombineSource: (start + i) == combineSourceSlot);

            RefreshEquippedWeapon();
        }
```

- [ ] **Step 3: Update InventoryView.cs — add cursor color fields, update RefreshSlots and MoveCursor**

Add two new serialized fields in the Cursor header block (after `cursorIcon`):

```csharp
        [Header("Cursor")]
        [SerializeField] private RectTransform cursorRect        = null!;
        [SerializeField] private Image         cursorIcon        = null!;
        [SerializeField] private Image         cursorHighlight   = null!;
        [SerializeField] private Color         normalCursorColor = Color.white;
        [SerializeField] private Color         combineColor      = new Color(1f, 0.8f, 0f, 1f);
```

Replace `RefreshSlots` method:

```csharp
        public void RefreshSlots(IReadOnlyList<InventorySlot> slots, int cursorSlot, int liftedSlot = -1, int combineSourceSlot = -1)
        {
            bool inCombineMode = combineSourceSlot >= 0;
            foreach (var card in this.cards)
                card.RefreshSlots(slots, combineSourceSlot);

            MoveCursor(cursorSlot, inCombineMode);
            UpdateLiftedIcon(slots, liftedSlot);
            UpdateInfoPanel(slots, cursorSlot);
        }
```

Replace `MoveCursor` method:

```csharp
        private void MoveCursor(int slotIndex, bool combineMode = false)
        {
            var cellRect = GetCellRect(slotIndex);
            if (cellRect == null) return;
            this.cursorRect.position   = cellRect.position;
            this.cursorRect.sizeDelta  = cellRect.sizeDelta;
            this.cursorHighlight.color = combineMode ? this.combineColor : this.normalCursorColor;
        }
```

- [ ] **Step 4: Check Unity compilation — no errors**

Wait for Unity to compile. Expected: no errors in Console.

- [ ] **Step 5: Wire the new cursorHighlight field in Unity Inspector**

In the InventoryView prefab/GameObject, assign the cursor's background Image component to the new `Cursor Highlight` serialized field. This is the Image on the cursor RectTransform that represents the highlight frame.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventorySlotCell.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/OperatorInventoryCard.cs"
git commit -m "feat(inventory): add combine mode visual feedback to inventory cells and cursor"
```

---

## Task 6: NavigationScope registration + Unity asset setup

**Files:**
- Modify: `Assets/Scripts/Navigation/NavigationScope.cs`

- [ ] **Step 1: Add CombineRecipeLibrary field and register CombineService in NavigationScope.cs**

Add the serialized field after `startingLoadout` (line 26):

```csharp
        [SerializeField] private StartingLoadout       startingLoadout       = null!;
        [SerializeField] private CombineRecipeLibrary  combineRecipeLibrary  = null!;
```

In the `Configure` method, add two lines after `builder.RegisterInstance(this.startingLoadout)`:

```csharp
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
```

Also update the `InventoryService` registration — it now requires `ICombineService` which VContainer will inject automatically from the registered `CombineService`. No change to its registration line is needed.

Full `Configure` after changes (relevant lines):

```csharp
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InventoryView>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryController>(Lifetime.Scoped).AsImplementedInterfaces();
            // ... rest unchanged
        }
```

- [ ] **Step 2: Check Unity compilation — no errors**

Wait for compilation. Expected: no errors.

- [ ] **Step 3: Create the CombineRecipeLibrary asset in Unity**

In Unity Editor: right-click in `Assets/Data/` (or create the folder) → Create → CrimsonDraft → Combine Recipe Library. Name it `CombineRecipeLibrary`.

Add the key+portfolio recipe in the Inspector:
- Recipes > + (add element)
- Input A: drag the `Key` ItemData asset
- Input B: drag the `Portfolio` ItemData asset
- Output: drag the target result ItemData asset

- [ ] **Step 4: Assign the asset in NavigationScope**

Select the NavigationScope GameObject in the Navigation scene hierarchy. In the Inspector, assign the `CombineRecipeLibrary` asset to the `Combine Recipe Library` field.

- [ ] **Step 5: Test in Play Mode**

1. Enter Play Mode
2. Open inventory (assigned keybind)
3. Navigate cursor to an item → press A → select Combine
4. Verify cursor changes to yellow/amber color
5. Navigate to a second item with a valid recipe → press A
6. Verify both items disappear and result appears in first free slot
7. Test cancel: open Combine mode → press B → verify returns to normal with no changes
8. Test invalid pair: open Combine mode → navigate to item with no recipe → press A → verify nothing happens and mode stays active

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(inventory): register CombineService and CombineRecipeLibrary in NavigationScope"
```
