# Item Combination System — Design Spec

**Date:** 2026-04-23  
**Status:** Approved  
**Scope:** Inventory system — predefined item combination mechanic

---

## Overview

Players can combine two inventory items to produce a new item. Combinations follow predefined recipes defined as data assets. Both input items are consumed on success; the result is placed in the first available slot across the roster. If no recipe matches the selected pair, the UI stays in combine mode silently.

---

## Data Layer

### `CombineRecipe` (serializable struct)

| Field    | Type      | Description                        |
|----------|-----------|------------------------------------|
| `inputA` | `ItemData` | ScriptableObject reference — first ingredient  |
| `inputB` | `ItemData` | ScriptableObject reference — second ingredient |
| `output` | `ItemData` | ScriptableObject reference — result item       |

### `CombineRecipeLibrary` (ScriptableObject)

- Contains a `List<CombineRecipe>` (serialized inline — no individual recipe assets)
- Injected into `CombineService` via VContainer at `NavigationScope`
- At initialization, the service builds a symmetric lookup dictionary:
  - Key: `(string minId, string maxId)` where `minId = min(inputA.itemId, inputB.itemId)` and `maxId = max(...)`
  - Value: `ItemData` result
- Recipes are symmetric: A+B and B+A produce the same result regardless of selection order

---

## Service Layer

### `ICombineService`

```
ItemData? TryGetResult(ItemData a, ItemData b)
```

Returns the result `ItemData` if a matching recipe exists; `null` otherwise. The lookup is symmetric — order of `a` and `b` does not matter.

### `CombineService : ICombineService, IInitializable`

- Registered in `NavigationScope` with `.AsSelf().AsImplementedInterfaces()`
- Injected dependency: `CombineRecipeLibrary`
- `Initialize()`: iterates `CombineRecipeLibrary.Recipes`, builds the symmetric dictionary

### `IInventoryService` — new method

```
bool TryCombine(int slotA, int slotB)
```

Implementation in `InventoryService`:

1. Reads `ItemData` from `Slots[slotA].Item.Data` and `Slots[slotB].Item.Data`
2. Calls `ICombineService.TryGetResult(dataA, dataB)`
3. If `null` → returns `false` (no mutation)
4. If result found:
   - `RemoveItem(slotA)`
   - `RemoveItem(slotB)`
   - `AddItemAuto(output)` — places result in first available slot across all operators
   - Returns `true`

---

## UI Layer — `InventoryController`

### New Mode: `CombineMode`

Added to the existing internal state enum alongside `Normal`, `ContextMenu`, and `Reorder`.

Tracks: `int _combineSourceSlot` — the slot index of the first selected item.

### State Transitions

In `CombineMode`, pressing A does **not** open the context menu — it directly attempts the combination.

```
Normal
  └─ A on slot with item → ContextMenu

ContextMenu
  └─ select "Combine" → CombineMode (store _combineSourceSlot, close menu)

CombineMode
  ├─ D-pad → navigate cursor (same as Normal)
  ├─ A on same slot as source → ignored
  ├─ A on empty slot → ignored
  ├─ A on slot with item (any operator's slot):
  │   ├─ TryCombine() == true → Normal (refresh view)
  │   └─ TryCombine() == false → stay in CombineMode (no feedback)
  └─ B (cancel) → Normal
```

**Slot ownership:** Unlike Equip/Reload/Use (which are restricted to the owning operator's slots), Combine can be initiated from any slot and target any slot. The result is placed via `AddItemAuto`, which is not tied to a specific operator.

### Visual Feedback

`InventorySlotCell` receives the current controller mode on each refresh call:

- **Normal cursor color:** existing highlight (white/default)
- **CombineMode cursor color:** distinct color (e.g., yellow/amber) — signals active combine state
- **Source slot highlight:** `_combineSourceSlot` cell renders a secondary distinct color — reminds the player which item they are combining

No text or audio feedback on failed combination attempts (silent rejection per design).

---

## Edge Cases

| Scenario | Behavior |
|---|---|
| A on source slot in CombineMode | Ignored — cannot combine item with itself |
| A on empty slot in CombineMode | Ignored |
| No recipe for selected pair | Stay in CombineMode, no mutation, no feedback |
| Source slot emptied mid-combine | Cannot happen — player has no other actions available in CombineMode |
| Result item is stackable and a matching stack exists | `AddItemAuto` handles stacking via existing logic |
| Both input slots freed, inventory otherwise full | Always has room — two slots freed, one consumed |

---

## Out of Scope

- Runtime recipe unlocking or disabling
- Combination animations or SFX (can be added later)
- Combination outside of the inventory screen
- More than two inputs per recipe
