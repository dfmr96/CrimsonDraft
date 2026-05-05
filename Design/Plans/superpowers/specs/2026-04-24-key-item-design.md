# Key Item System — Design Spec

**Date:** 2026-04-24  
**Status:** Approved  
**Scope:** Inventory system — key items with finite, tracked uses

---

## Overview

Keys are a distinct item category separate from consumables. They are items of world progression — they unlock doors and other interactions but cannot be used or equipped from the inventory directly. A key has a fixed number of uses defined in its data asset; each use decrements the counter. When the last use is consumed, the player is offered a discard prompt. If declined, the depleted key stays in the inventory occupying a slot indefinitely.

---

## Data Layer

### `ItemType` — new value

```
enum ItemType { Weapon, AmmoBox, Consumable, KeyItem }
```

### `KeyItemData : ItemData`

ScriptableObject. `ItemType` is fixed to `KeyItem`.

| Field      | Type  | Description                              |
|------------|-------|------------------------------------------|
| `maxUses`  | `int` | Total uses before depletion — always ≥ 1 |

### `KeyItem : InventoryItem`

Runtime wrapper. Carries mutable use state.

| Member           | Type   | Description                                                     |
|------------------|--------|-----------------------------------------------------------------|
| `UsesRemaining`  | `int`  | Starts at `KeyItemData.maxUses`. Never goes below 0.            |
| `Consume()`      | `bool` | Decrements `UsesRemaining`. Returns `true` if it reached 0.    |

**`Consume()` contract:**
- If `UsesRemaining == 0` on entry: returns `true` without decrementing (already depleted).
- Otherwise: decrements by 1, returns `true` if result is 0, `false` otherwise.

---

## Service Layer

### `KeyUseResult` — enum

```
enum KeyUseResult { Success, DepletedAfterUse, AlreadyDepleted, NotFound }
```

| Value              | Meaning                                                               |
|--------------------|-----------------------------------------------------------------------|
| `Success`          | Use registered; key has remaining uses                                |
| `DepletedAfterUse` | Use registered; key reached 0 uses — caller must show discard prompt |
| `AlreadyDepleted`  | Key found but already at 0 uses; use not registered                  |
| `NotFound`         | No slot contains a `KeyItem` with the given `itemId`                 |

### `KeyUseOutcome` — return struct

```
struct KeyUseOutcome
{
    KeyUseResult Result;
    int SlotIndex;   // ≥ 0 when Result is Success, DepletedAfterUse, or AlreadyDepleted; -1 when NotFound
}
```

The caller needs `SlotIndex` to call `RemoveItem` after a confirmed discard without a second lookup.

### `IInventoryService` — new method

```
KeyUseOutcome TryUseKey(string keyItemId)
```

### `InventoryService` — implementation

1. Find the first slot where `Item` is a `KeyItem` and `Data.ItemId == keyItemId`. Record its `slotIndex`.
2. If not found → return `{ NotFound, -1 }`.
3. If `keyItem.UsesRemaining == 0` → return `{ AlreadyDepleted, slotIndex }`.
4. Call `keyItem.Consume()`:
   - Returns `true` → return `{ DepletedAfterUse, slotIndex }`
   - Returns `false` → return `{ Success, slotIndex }`

The slot is never touched by this method — the key remains in inventory regardless of result.

---

## Use Flow

Keys are used exclusively via world interactions (doors, locks). Not from the inventory screen.

```
Player interacts with locked door
  ├─ Door resolves required keyItemId
  ├─ outcome = inventoryService.TryUseKey(keyItemId)
  │   ├─ NotFound → door locked, no prompt
  │   ├─ AlreadyDepleted → door locked (key exhausted, nothing to do)
  │   ├─ Success → door opens, no prompt
  │   └─ DepletedAfterUse → door opens + show discard prompt:
  │         "Ya no necesitas [DisplayName]. ¿Deseas descartarla?"
  │           ├─ Confirm → inventoryService.RemoveItem(outcome.SlotIndex)
  │           └─ Decline → no action (depleted key stays in slot)
```

---

## Inventory UI

Keys use the standard slot cell. No visual distinction for depleted keys (0 uses).

**Context menu for `KeyItem`:**

| Action     | Available |
|------------|-----------|
| Examinar   | Always    |
| Combinar   | Always    |
| Usar       | Never     |
| Equipar    | Never     |
| Recargar   | Never     |
| Descartar  | Never     |

There is no manual discard option. The discard prompt is the only mechanism for removing a key from inventory.

**Updated item type table (for Sistema de Inventario):**

| Type         | Stackable | Available actions                    |
|--------------|-----------|--------------------------------------|
| Arma         | No        | Equipar / Desequipar, Combinar, Examinar |
| Caja de balas| Sí        | Recargar, Combinar, Examinar         |
| Consumible   | No        | Usar, Combinar, Examinar             |
| **Llave**    | No        | Combinar, Examinar                   |

---

## Edge Cases

| Scenario                                        | Behavior                                                    |
|-------------------------------------------------|-------------------------------------------------------------|
| Key with 0 uses in inventory                    | Occupies slot; context menu shows Examinar / Combinar only  |
| Player uses key on door when already at 0 uses  | `AlreadyDepleted` — door stays locked                       |
| Player declines discard prompt                  | Key stays in inventory at 0 uses, no further action         |
| Depleted key used in a combine recipe           | Combine system does not check uses — combination proceeds if recipe exists |
| Player moves a depleted key to another slot     | `UsesRemaining` travels with the item instance (slot agnostic) |

---

## Out of Scope

- Visual indicator for depleted keys (grayed icon, strikethrough, etc.)
- Key ring or grouping mechanic
- Keys with unlimited uses
- Discard outside of the post-use prompt
