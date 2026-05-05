# Item Socket System — Design Spec
_Date: 2026-04-24_

## Summary

An **ItemSocketInteractable** is a world object that requires one or more specific `SocketItem` type items to be inserted via the inventory's "Use" command. When all required items are inserted, it fires a `UnityEvent`.

## New Item Type: SocketItem

- New `ItemType.SocketItem` enum value.
- New `SocketItemData : ItemData` ScriptableObject — no extra fields beyond the base class.
- New `SocketItem : InventoryItem` runtime wrapper — no extra fields.
- Context menu shows `[Use, Combine, Examine]` for `SocketItem`.
- "Use" from inventory fires the insertion flow. "Use" on `Consumable` remains a stub (future healing logic).

## IInteractionCaster Interface

New interface extracted alongside `PlayerInteractionCaster`:

```
interface IInteractionCaster
    bool TryUseItem(ItemData item)
```

`PlayerInteractionCaster` implements it. `TryUseItem` reuses the same raycast parameters (distance, LayerMask) already configured for `OnInteract`. If the hit object has an `ItemSocketInteractable`, calls `socket.TryInsert(item, poiController)` and returns the result.

## ItemSocketInteractable

MonoBehaviour + IInteractable on a world GameObject.

**Serialized fields:**
- `SocketItemData[] requiredItems` — the items the socket needs (can repeat same item).
- `UnityEvent onActivated` — fires once when all items are inserted.

**Runtime state:**
- `bool[] inserted` — tracks which slots are satisfied (parallel to `requiredItems`).
- `bool activated` — prevents re-triggering after completion.

**TryInsert(ItemData item, PoiController poi) : bool**
1. If `activated` → return false (silent).
2. If `item` is not `SocketItemData` → return false (silent).
3. Find first index `i` where `!inserted[i]` and `requiredItems[i].ItemId == item.ItemId`.
4. If found: `inserted[i] = true`, show `"Inserted: {name}."` via poi. If all inserted → `activated = true`, invoke `onActivated`. Return true.
5. If not found: show `"Can't use {name} here."` via poi. Return false.

**Interact(InteractionContext) — normal Interact button (no item)**
- If `activated`: show `"Already activated."` or do nothing.
- Otherwise: show checklist — `"[✓] Keycard"` / `"[ ] Battery"` per slot.

## InventoryController Changes

- Constructor gains `IInteractionCaster` parameter.
- `case ContextMenuAction.Use`:
  - `ItemType.SocketItem` → calls `TryUseItem(data)`. If `true` → `RemoveItem(slotIndex)`, `RefreshView()`.
  - `ItemType.Consumable` → no-op (stub for future).

## Flow Diagram

```
Player opens inventory → selects SocketItem → "Use"
  InventoryController.ExecuteContextMenuAction()
    IInteractionCaster.TryUseItem(itemData)
      Physics.Raycast (same as Interact)
        hit has ItemSocketInteractable?
          NO → return false → nothing happens
          YES → socket.TryInsert(itemData, poi)
            match found?
              YES → inserted[i]=true, poi feedback
                    all inserted? → onActivated.Invoke()
                    return true
              NO  → poi feedback, return false
    if true → inventoryService.RemoveItem(slotIndex)
              RefreshView()
```

## Out of Scope

- Saving socket state across scene loads (runtime-only).
- Inserting items in a required order (any order accepted).
- Quantity requirements per slot (each slot = one item instance).
