# Yarn Spinner Dialogue System — Design Spec
_Date: 2026-04-25_

## Summary

Install Yarn Spinner Unity and introduce an `IDialogueService` that wraps Yarn's `DialogueRunner`. All player-facing text in the Navigation scene moves to `.yarn` files — no strings in C# code. The existing `PoiController` and `PoiDialogView` are deleted; their concerns (time scale, input switching, line progression) move into `DialogueService`. Every interactable data asset gains a `yarnNodeName` field pointing to the Yarn node that handles its dialogue.

---

## Package

- **Install:** `YarnSpinner-Unity` via UPM git URL:
  `https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git`
- **YarnProject asset:** `Assets/Dialogues/Navigation.yarnproject`
  Compiles all `.yarn` files in `Assets/Dialogues/`.
- **Scene setup:** A `DialogueRunner` MonoBehaviour in the Navigation scene references the `Navigation.yarnproject`. Yarn's built-in `LineView` and `OptionsListView` are used as presenters (visual configuration deferred).

---

## IDialogueService

Registered in `NavigationScope`. Wraps the `DialogueRunner`.

```
interface IDialogueService
    bool IsRunning
    void StartDialogue(
        nodeName  : string,
        variables : IReadOnlyDictionary<string, object>?   = null,
        commands  : IReadOnlyDictionary<string, Action>?   = null)
```

**`DialogueService` behavior:**

1. On `StartDialogue`:
   - Populates `InMemoryVariableStorage` with `variables` (cleared from previous run first).
   - Registers each entry in `commands` as a one-time `AddCommandHandler` on the `DialogueRunner`.
   - Sets `Time.timeScale = 0f`.
   - Calls `inputService.SwitchToUI()`.
   - Calls `dialogueRunner.StartDialogue(nodeName)`.

2. On `DialogueRunner.onDialogueComplete`:
   - Restores `Time.timeScale = 1f`.
   - Calls `inputService.SwitchToGameplay()`.
   - Removes all one-time command handlers registered for this run.

---

## InteractionContext

`PoiController` field is replaced by `IDialogueService`:

```
class InteractionContext
    IInventoryService   InventoryService
    IInputService       InputService
    IDialogueService    DialogueService      // replaces PoiController
    DocumentController  DocumentController
    ContainerController ContainerController
```

---

## Deleted Types

| Type | Reason |
|---|---|
| `PoiController` | Logic absorbed by `DialogueService` |
| `PoiDialogView` | Replaced by Yarn's built-in `LineView` + `OptionsListView` |

---

## Data Layer Changes

### PoiData

`string[] lines` is removed. Replaced by a single Yarn node reference:

```
PoiData : ScriptableObject
    string yarnNodeName
```

### DoorData

New field added:

```
DoorData : ScriptableObject
    bool         locked
    KeyItemData? keyItem
    string       yarnNodeName    // new
```

### ItemSocketInteractable

No separate data SO — `yarnNodeName` is serialized directly on the MonoBehaviour:

```
ItemSocketInteractable : MonoBehaviour, IInteractable
    SocketItemData[] requiredItems
    UnityEvent       onActivated
    string           yarnNodeName    // new
```

---

## Interactable Changes

### PoiInteractable

`Open(data.Lines)` → `StartDialogue(data.YarnNodeName)`. No variables or commands needed.

```
Interact(context):
    context.DialogueService.StartDialogue(data.YarnNodeName)
```

### DoorInteractable

The C# logic determines the outcome first, then passes it as a Yarn variable. The Yarn script branches on `$outcome`. If the player has the key, the `<<doorConfirmed>>` command is registered; it consumes the key and fires `onOpen`.

`IInventoryService` gains a new read-only query `HasItem(itemId: string): bool` to check key presence without consuming.

```
Interact(context):
    if !data.Locked or unlocked:
        onOpen.Invoke()
        return

    if data.KeyItem == null:
        context.DialogueService.StartDialogue(data.YarnNodeName,
            variables: { "$key_required": false })
        return

    hasKey = context.InventoryService.HasItem(data.KeyItem.ItemId)

    context.DialogueService.StartDialogue(
        data.YarnNodeName,
        variables: {
            "$key_required": true,
            "$has_key":      hasKey,
            "$key_name":     data.KeyItem.DisplayName
        },
        commands: hasKey ? {
            "doorConfirmed": () =>
                outcome = context.InventoryService.TryUseKey(data.KeyItem.ItemId)
                if outcome.Result is Success or DepletedAfterUse:
                    unlocked = true
                    onOpen.Invoke()
                    if outcome.Result == DepletedAfterUse:
                        context.InventoryService.RemoveItem(outcome.SlotIndex)
        } : null
    )
```

**Yarn variables contract for door nodes:**

| Variable | Type | Meaning |
|---|---|---|
| `$key_required` | bool | Door needs a specific key |
| `$has_key` | bool | Player currently has the key |
| `$key_name` | string | Display name of the required key |

### ItemSocketInteractable — Interact (checklist display)

Passes slot state as variables. No commands.

```
Interact(context):
    if IsActivated:
        context.DialogueService.StartDialogue(yarnNodeName,
            variables: { "$activated": true, "$slots_filled": total, "$slots_total": total })
        return

    context.DialogueService.StartDialogue(yarnNodeName,
        variables: {
            "$activated":    false,
            "$slots_filled": countFilled,
            "$slots_total":  requiredItems.Length
        })
```

### ItemSocketInteractable — TryInsert (inventory "Use")

Called from `InventoryController`. No Yes/No prompt — the player already confirmed via the inventory "Use" action. Shows feedback via Yarn.

```
TryInsert(item, dialogueService):
    // insertion logic unchanged
    // on success:
    dialogueService.StartDialogue(yarnNodeName,
        variables: {
            "$insert_result": "success",
            "$item_name":     item.DisplayName,
            "$slots_filled":  newFilledCount,
            "$slots_total":   requiredItems.Length
        })
    // on fail (wrong item):
    dialogueService.StartDialogue(yarnNodeName,
        variables: {
            "$insert_result": "wrong_item",
            "$item_name":     item.DisplayName
        })
```

`TryInsert` signature changes: replaces `PoiController? poi` parameter with `IDialogueService dialogueService`.

**Yarn variables contract for socket nodes:**

| Variable | Type | Meaning |
|---|---|---|
| `$activated` | bool | Socket is already fully filled |
| `$slots_filled` | number | How many slots are currently filled |
| `$slots_total` | number | Total slots required |
| `$insert_result` | string | `"success"` / `"wrong_item"` (only in TryInsert flow) |
| `$item_name` | string | Display name of the item being inserted (only in TryInsert flow) |

---

## NavigationScope Changes

- Remove `PoiController` and `PoiDialogView` registrations.
- Add `DialogueRunner` (RegisterComponentInHierarchy).
- Add `InMemoryVariableStorage` (RegisterComponentInHierarchy).
- Register `DialogueService` as `IDialogueService` (Scoped, AsImplementedInterfaces).
- `InteractionContext` constructor updated: `PoiController` → `IDialogueService`.

---

## Yarn File Structure

```
Assets/Dialogues/
    Navigation.yarnproject
    poi/
        poi_cargo_hold.yarn
        ...
    doors/
        door_cargo_hold.yarn
        ...
    sockets/
        socket_main_panel.yarn
        ...
```

Each `.yarn` file has one `title:` node matching the `yarnNodeName` value set on the data asset. Localization string tables are generated automatically by the `YarnProject` importer.

---

## Yarn Commands

| Command | Registered by | Fires |
|---|---|---|
| `<<doorConfirmed>>` | `DoorInteractable` per-dialogue | `TryUseKey` + `onOpen.Invoke()` |
| `<<socketItemPlaced>>` | *(reserved for future use)* | — |

Global permanent commands (sounds, VFX) can be registered in `DialogueService.Initialize()` and never cleared.

---

## New Branch

All work happens on a dedicated branch: `feature/yarn-spinner`.
