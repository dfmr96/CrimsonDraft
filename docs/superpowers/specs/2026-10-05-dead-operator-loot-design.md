# Dead Operator Loot — Design

Builds on the container inventory (`2026-10-03-inventory-refactor-design.md`) and the
storage box (`2026-10-04-storage-box-design.md`): non-carried containers, transfer-mode
cursor behaviour, `GridNavigator` rows.

## Goal

When an operator is KIA, their card is dimmed with a "KIA" label and their items become
unreachable — in navigation and in combat. Only while the player stands inside the
trigger collider of that operator's corpse (navigation) does the card lose the dim and
the player can move items between the dead operator's grid and the living operators'
grids, both ways.

## Decisions (agreed)

| Topic | Decision |
|---|---|
| Proximity | Player inside a trigger collider on the corpse |
| Access outside the collider | None, in navigation and combat |
| Access inside the collider | Move only (pick up, rotate, drop, swap) — no context menu, use, combine, equip, reload, split on the dead operator's items |
| Dead operator's primary weapon | Unequipped on death, stays as a loose item in their grid |
| Card far from corpse | Existing `deadOverlay` dim (enabled) + large "KIA" label |
| Card near corpse | No dim, no label (flat ECG remains) |
| Cursor on a locked grid | Skipped |

## Non-goals

- Reviving operators.
- Loot animation/SFX, final "KIA" art.
- Changes to where corpses spawn (still the player's position at combat end).
- Save format changes.

## Design

### 1. Domain (`CrimsonDraft.Inventory`)

- **Carried containers = containers of living operators.** A dead operator's container
  joins storage in the non-carried set: `HasItem`, `TryRemove`, `TryUseKey`,
  `CanReload`/reload ammo search, `HasEquippedWeapon`, combat item listing and the
  automatic `TryAdd(data, quantity)` ignore it.
- Actions on items in a dead operator's container are always rejected (no mutation, no
  `Changed`): `TryCombine` (either input), `TryUseConsumable`, `TryReload`, `Equip`,
  `CanSplit`/`TrySplit`.
- **`ICorpseAccess`** (`bool CanAccess(int operatorSlot)`) injected into
  `InventoryService`. A container is **accessible** when it is storage, a living
  operator's, or a dead operator's with `CanAccess(slot) == true`.
  `IInventoryService.IsAccessible(ContainerId)` exposes the rule to the UI.
- `TryPickUp` from, and `TryDrop`/swap into, an inaccessible container are rejected
  (no mutation, no `Changed`). Swap keeps the guaranteed-return rule. `CancelHeld`
  returns to the origin even if it became inaccessible (origin is never re-checked).
- `IInventoryService.IsCarried(ContainerId)`: true only for a living operator's container.
- Combat needs no special access source: the combat item panel only shows the acting
  (living) operator's grid, and every action on a dead operator's item is rejected.

### 2. Weapon on death

- `InventoryService.ReleaseDeadOperatorWeapons()`: for each dead operator with an
  equipped primary weapon, clear `WeaponItem` equipped state and
  `OperatorRuntime.SetEquippedWeapon(null, Primary)`; the item stays where it is in the
  grid. Raises `Changed` for affected containers.
- Called by `DeadOperatorGearReleaser` (navigation, registered after the roster
  bootstrap) on `Initialize` and on every `CombatEndedEvent`. Idempotent.
- On a new-scene load `InventoryBootstrap` restores items before `OperatorRosterBootstrap`
  restores HP, so `Restore` can still wire a weapon to an operator that is about to be
  marked dead; `DeadOperatorGearReleaser` (registered after the roster bootstrap) unequips
  it in the same initialization pass. `Restore` skips weapons in a dead operator's grid
  (non-carried).

### 3. World (`CrimsonDraft.Navigation`)

- `IOperatorCorpseSpawner.Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)`;
  `OperatorCorpseBootstrap` passes the slot it already knows.
- `OperatorCorpse` (MonoBehaviour) on the corpse prefab root: trigger collider (size set
  in the prefab), `Initialize(int slot, ICorpseProximity tracker)` called by the spawner.
  `OnTriggerEnter` with the player → `tracker.Enter(slot)`; `OnTriggerExit` and
  `OnDisable` → `tracker.Exit(slot)` (Unity sends no exit when a room is deactivated).
- `CorpseProximityTracker` (pure C#, navigation scope): `Enter(slot)`, `Exit(slot)`,
  implements `ICorpseAccess`. Overlapping corpses give access to all of them.
- Proximity is session state only; on load, a player spawned inside a trigger is
  detected by `OnTriggerEnter`.

### 4. UI (`CrimsonDraft.UI`)

- **Card (`OperatorWidgetView`)**: `Bind` shows `deadOverlay` when
  `!op.IsAlive && !access` (a `bool canLoot` argument from the caller). `Card.prefab`:
  enable the overlay `Image`, add a child `TMP_Text` "KIA", large and centred. Cards are
  rebound every time the inventory (or the combat panel) opens; the inventory pauses the
  game, so access cannot change while it is open.
- **Navigation (`InventoryPresenters`)**: `BuildLinks`, `FirstGrid` and
  `BuildStorageRows` skip inaccessible grids (same mechanism that skips grids without a
  container). `GridNavigator` is unchanged. `GridCursor.ResetCursorToOrigin` rebuilds the
  navigator so links reflect access at every open. In storage mode, entering the operator
  row lands on the first accessible grid.
- **Cursor (`GridCursor`)**: in a non-carried grid (dead operator), Confirm picks up /
  drops (transfer behaviour) instead of opening the context menu; moving up from it never
  enters the melee slot (goes to the tab bar). Rotate and swap work as usual.
  `SelectItemById` ignores inaccessible grids.
- **Storage box**: same rules; while standing in a corpse trigger, items move between
  storage, living operators and the dead operator.
- **Combat panel**: unchanged — it only binds the acting operator's grid, who is alive.

## Save

No format change. Items stay in the dead operator's container
(`InventorySerializer`), corpses persist in `OperatorCorpseRegistry`. Proximity is not
saved.

## Testing

TDD, EditMode, shared fakes (`FakeRoster` gains dead operators; a `FakeCorpseAccess`).

- Service: dead containers ignored by `HasItem`/keys/reload/automatic `TryAdd`; actions on
  dead items rejected without `Changed`; pick up/drop rejected without access, allowed both
  ways with access; swap guaranteed return; cancel returns to an inaccessible origin;
  `IsAccessible` matrix (storage, alive, dead ± access).
- Weapon: `ReleaseDeadOperatorWeapons` unequips and keeps the item in place; `Restore`
  does not equip to a dead operator.
- `CorpseProximityTracker`: enter, exit, two corpses.
- `OperatorCorpseBootstrap`: spawner receives the slot.
- `InventoryPresenters` (EditMode with real `InventoryGrid` objects): links, first grid
  and storage rows skip inaccessible grids.
- `DeadOperatorGearReleaser`: releases on initialize and on combat end.
- Play Mode (CLI `eval`): kill an operator, check overlay + KIA and cursor skip; teleport
  the player into the trigger, transfer both ways; leave the room, locked again.
- Manual (owner): real combat death, loot, save/load next to and away from the corpse.

## Risks

- `IOperatorCorpseSpawner.Spawn` signature change touches its tests/fakes.
- `InventoryService` constructor gains `ICorpseAccess`; every construction site and fake
  changes.
- If the corpse prefab's collider is the same one used for physics, a separate trigger
  child is added instead.
