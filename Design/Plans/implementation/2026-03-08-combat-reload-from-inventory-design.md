# Design: Combat Reload from Inventory

**Date:** 2026-03-08

## Problem

`ReloadCommand.Execute()` is empty. Selecting Reload in combat returns immediately to `OperatorSelectionState` without consuming any inventory ammo.

## Solution

Wire the existing `IInventoryService.ReloadOperator` API into the combat state machine via the SubPanel flow. Approach: direct dependency (add `CrimsonDraft.Inventory` reference to Combat assembly).

## Assembly

Add `"CrimsonDraft.Inventory"` to `CrimsonDraft.Combat.asmdef` references.
No cycle: `Combat → Inventory → Operators ← Combat` (Inventory does not reference Combat).

## State Machine Flow

```
CommandPanel → Reload selected
  → scan IInventoryService.Items for compatible AmmoBoxItem entries
  → build SubPanelItem[] with labels "9MM FMJ ×45"
  → if none: single item "NO AMMO", ReloadAmmoBoxIndices = []
  → store indices in context.ReloadAmmoBoxIndices
  → dim CommandPanel → show SubPanel → SubPanelState

SubPanelState → item selected at index i
  → if i >= ReloadAmmoBoxIndices.Length: no-op ("NO AMMO")
  → inventory.ReloadOperator(ReloadAmmoBoxIndices[i], selectedOperator)
  → menuView.SetOperatorAmmo(op, weapon.CurrentAmmo, weapon.MaxAmmo)
  → subPanel.Hide() → OperatorSelState
```

## Changes

### `CrimsonDraft.Combat.asmdef`
- Add `"CrimsonDraft.Inventory"` to references

### `CombatMenuController`
- Add `IInventoryService inventory` to both constructors
- Add `internal int[] ReloadAmmoBoxIndices { get; set; }` property
- Pass `inventory` + `menuView` to `CommandPanelState` and `SubPanelState` in `Initialize()`

### `CommandPanelState`
- Add `IInventoryService inventory` field
- Replace Reload stub: scan inventory, build SubPanelItem[], store index mapping, show SubPanel

### `SubPanelState`
- Add `IInventoryService inventory`, `IOperatorRoster roster`, `ICombatActionMenuView menuView` fields
- Implement `OnItemSelected`: execute reload, update HUD, transition back

### `ReloadCommand`
- No changes (Command pattern not wired for turn resolution yet)

## Out of Scope
- Multiple ammo types UI differentiation
- Reload animation/feedback
- ReloadCommand execution (future turn resolution)
