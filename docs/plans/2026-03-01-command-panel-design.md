# Command Panel — Design Document
**Date:** 2026-03-01
**Scope:** `combat-ui`
**Status:** Approved

---

## Overview

When the player enters combat, the bottom strip shows 4 operator columns. The player navigates L/R between operators and presses Submit to open a **CommandPanel** above the selected operator. From there they can select a command, and for inventory-based commands a **SubPanel** stacks above the CommandPanel showing available items.

---

## User Flow

```
Combat loads → OperatorSelection (auto-selects first operator)
    │
    └─ Navigate L/R between operators
    │
    └─ Submit → CommandPanel appears above selected operator
                    │
                    ├─ SHOOT   → (future: open QTEView)
                    ├─ RELOAD  → SubPanel with ammo list
                    ├─ ITEMS   → SubPanel with consumable items
                    └─ DEFEND  → SubPanel with defensive items
                                    │
                                    └─ Select item → (future: apply action)
                                    └─ Cancel → back to CommandPanel

    Cancel (any panel) → one step back in state stack
```

---

## State Machine

`CombatMenuController` owns a private `CombatMenuState` enum:

```
OperatorSelection → CommandPanel → SubPanel
      ▲                  ▲             │
      └──────Cancel───────┴──Cancel────┘
```

```csharp
private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel }
```

---

## Commands (fixed, all operators)

| Command | Action |
|---------|--------|
| SHOOT   | Fires `OnCommandSelected(Shoot)` — controller routes to QTE (not yet implemented) |
| RELOAD  | Opens SubPanel with ammo items from inventory (placeholder for now) |
| ITEMS   | Opens SubPanel with consumable/throwable items (placeholder for now) |
| DEFEND  | Opens SubPanel with defensive items (placeholder for now) |

---

## New Components

### C# — `Assets/Scripts/Combat/UI/`

| File | Type | Responsibility |
|------|------|----------------|
| `CombatCommand.cs` | `enum` | `Shoot, Reload, Items, Defend` |
| `SubPanelItem.cs` | `record` | `string Label` — data for a single sub-panel entry |
| `ICommandPanelView.cs` | interface | `event Action<CombatCommand> OnCommandSelected` · `Show(RectTransform anchor)` · `Hide()` |
| `CommandPanelView.cs` | MonoBehaviour | 4 hardcoded `ActionMenuItem`s, wired in Inspector |
| `ISubPanelView.cs` | interface | `event Action<int> OnItemSelected` · `Show(SubPanelItem[] items, RectTransform anchor)` · `Hide()` |
| `SubPanelView.cs` | MonoBehaviour | Dynamic list of `ActionMenuItem`s (pool of 6 slots max) |

`CombatMenuController.cs` is modified to own the state machine and coordinate both panels.

### Prefabs — `Assets/Prefabs/UI/`

| Prefab | Contents |
|--------|----------|
| `CommandPanel.prefab` | Panel background + 4 `ActionMenuItem` children (SHOOT, RELOAD, ITEMS, DEFEND) |
| `SubPanel.prefab` | Panel background + 6 `ActionMenuItem` slot children (shown/hidden per item count) |

### Scene — `Combat.unity`

Both panels are added as children of **HUDRoot** (sibling to `BottomStrip`), **disabled by default**. No runtime instantiation — Show/Hide only.

`CombatScope` registration:
```csharp
builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
```

---

## Positioning

Both panels live in **HUDRoot**, positioned dynamically on `Show()`.

- **Pivot:** `(0.5, 0)` — center-bottom anchored to operator center
- **CommandPanel X:** center of selected `OperatorOverview` RectTransform (world→local via `InverseTransformPoint`)
- **CommandPanel Y:** top edge of `BottomStrip`
- **SubPanel X:** same as CommandPanel
- **SubPanel Y:** top edge of CommandPanel (exposed via `RectTransform TopAnchor` on `CommandPanelView`)
- **Clamp:** X clamped to keep panel within canvas bounds (0–320px)

Pattern mirrors existing `MoveSelector` logic in `CombatActionMenuView`.

---

## Data Flow

```
CombatActionMenuView
  └─ OnOperatorSelected(index)
        │
        ▼
CombatMenuController
  ├─ records selectedOperatorIndex
  ├─ calls CommandPanelView.Show(operatorAnchor)
  └─ state = CommandPanel

CommandPanelView
  └─ OnCommandSelected(command)
        │
        ▼
CombatMenuController
  ├─ if Shoot → (future signal to QTEView)
  └─ else → SubPanelView.Show(GetItemsFor(command), commandPanelTopAnchor)
             state = SubPanel

SubPanelView
  └─ OnItemSelected(index)
        │
        ▼
CombatMenuController
  └─ (future: apply action to operator)
```

### Placeholder inventory data

Until the inventory system exists, `CombatMenuController` generates static items:

```csharp
private SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
{
    CombatCommand.Reload => new[] { new SubPanelItem("9MM FMJ"), new SubPanelItem("9MM RIP") },
    CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
    CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
    _                    => Array.Empty<SubPanelItem>()
};
```

When the inventory system is implemented, only this method changes.

---

## Input / Navigation

- All panel items are `ActionMenuItem` (extends `Selectable`) — Unity EventSystem handles U/D navigation
- On `Show()`: `EventSystem.current.SetSelectedGameObject(firstItem)`
- On Cancel: controller calls `Hide()` on current panel, restores previous `SetSelectedGameObject`
- Cancel input: `IInputService.CombatCancel` (already wired in `CombatSessionController` — controller needs to intercept before session controller)

---

## What is NOT in scope

- Actual inventory query (placeholder data only)
- QTEView integration (SHOOT fires event, no further action)
- Applying item effects to operator stats
- SubPanel item quantity display
- Disabled state for commands when operator has no valid items
