# Storage Box (RE-style Item Box) — Design

Sub-project 2 of the inventory work. Builds on the container model from
`2026-10-03-inventory-refactor-design.md` (ItemContainer, ContainerId.Storage,
held-item drop/swap, InventorySerializer, GridNavigator, ContainerGridPresenter).

## Goal

A classic Resident Evil item box: every box in the game opens the **same shared
storage**. Interacting with a box opens the existing inventory screen in a
"storage mode" where a 12×4 storage grid appears in a band over the top half of
the four operator cards, and the player can only move items between the box
and the operators. The box contents persist through scene changes and save files.

## Decisions (agreed)

| Topic | Decision |
|---|---|
| Storage model | One shared container (`ContainerId.Storage`) for all boxes |
| Size | 12 columns × 4 rows |
| Placement | Window over the top half of the cards (portraits, weapon/melee slots); the four operator grids stay where they are |
| Allowed actions in storage mode | Move only (pick up, rotate, drop, swap) — on **every** grid. No context menu, no inspect, no tabs, no melee slot |
| Opening | Interacting with a box opens storage mode immediately; closing returns to gameplay |
| Initial contents | Configurable in `StartingLoadout.storageItems` |
| Box items and gameplay | Items in the box are not "carried": they do not satisfy keys, `HasItem`, reloads or combat; pickups never go to the box |

## Non-goals

- Per-box contents (the `ContainerId` design allows adding it later).
- Access to the box from combat or from the normal inventory key.
- Box lid animation, new SFX, final art for the window.
- Scene edits: the box prefab is created; the owner places it in rooms.

## Design

### 1. Domain (`CrimsonDraft.Inventory`)

- `InventoryConstants.StorageGridWidth = 12`, `StorageGridHeight = 4`.
- `InventoryService` creates one `ItemContainer(ContainerId.Storage, 12, 4)`
  together with the operator containers (created once, never replaced).
  `GetContainer(ContainerId.Storage)` returns it.
- Two internal sets:
  - **Carried** = operator containers. Used by `HasItem`, `TryRemove`,
    `TryUseKey`, `HasEquippedWeapon`, `CanReload`, and the automatic
    `TryAdd(data, quantity)`.
  - **All** = operators + storage. Used by `FindContainerOf`, `TryPickUp`,
    `TryDrop`/swap, `CancelHeld`, `Remove`, and `TryAdd(data, ContainerId.Storage, …)`.
- Actions on items located in the storage container are rejected by the service
  (return `false` / no-op, no mutation, no `Changed`): `TryCombine` (either
  input), `TryUseConsumable`, `TryReload`, `Equip`, `CanSplit`/`TrySplit`.
- Transfers use the existing `TryPickUp`/`TryDrop`; swap keeps the
  guaranteed-return rule. Equipped items still cannot be picked up, so they never
  reach the box equipped.

### 2. Persistence

- `InventorySerializer.Capture` includes the storage container's placements
  (`containerKind = Storage`). No save format change (`InventoryItemEntry`
  already carries `containerKind`).
- `Restore`: pass 1 places entries at their saved position in their own
  container (storage entries in storage). Pass 2 fallback order for anything
  left: preferred container → operator containers → storage. Only when nothing
  fits anywhere is the item dropped with `Debug.LogError`.
- Scene transitions use the same serializer, so the box persists automatically.
- `StartingLoadout.storageItems : StartingItemEntry[]` (uses `item` and
  `quantity`; `operatorSlot` ignored). `InventoryBootstrap` adds them to storage
  on a new game only (no saved registry state).

### 3. Navigation (`GridNavigator`, pure, testable)

- New optional constructor input: **rows** — an ordered list (top to bottom)
  of grid rows, each an ordered list (left to right) of `ContainerId`s.
- When a vertical move leaves a grid and the grid belongs to a row with a row
  above/below, the cursor enters the destination row's **first grid at its near
  corner**: bottom-left when moving up (any operator → Storage (0,3)), top-left
  when moving down (Storage → first operator (0,0)).
- With rows present, leaving the top row upward or the bottom row downward is a
  no-op that returns `NavigationExit.None` and keeps the cursor in place
  (storage mode has no tab bar or melee slot to exit to).
- Without rows, behaviour is exactly as today (existing tests unchanged).
- Storage mode rows: `[[Storage], [present operator grids in order]]`.

### 4. UI (`CrimsonDraft.UI`)

- **`StorageWindow`** (MonoBehaviour) on a new GameObject under `Inventory`
  (sibling after `Card_Container`, so it draws above the cards), anchored to the
  top, covering the cards' top half. Contains a frame/background (sprite and
  colour copied from the card border), a title label "STORAGE BOX", and an
  `InventoryGrid` (12×4, `containerKind = Storage`, same cell size as operator
  grids, horizontally centred) inside a `Grid_container` framed with the same
  top/right border sprite as the operator grids (tiled). Inactive by default. Built in
  `UI_Inventry_Root.prefab` through the Editor (source prefab, not UI_Canvas
  overrides, not scenes). API: `InventoryGrid Grid`, `void Show()`, `void Hide()`,
  `bool IsShown`.
- **`InventoryPresenters`**: also creates a presenter for the storage grid when a
  `StorageWindow` exists (inactive grid ⇒ deferred render, existing mechanism).
  `BuildLinks()` keeps today's operator links; new `BuildStorageRows()` returns
  the rows for storage mode.
- **`GridCursor`**: `EnterTransferMode()` / `ExitTransferMode()`. While in
  transfer mode:
  - Confirm picks up the item under the cursor, or drops if holding.
  - Pickup picks up / rotates as today.
  - No context menu, no melee slot, no tab bar; vertical exits are no-ops.
  - Cancel while holding drops (as today); Cancel with an empty hand requests
    close.
  - The navigator is rebuilt with storage rows on enter and with normal links on
    exit; the cursor starts at Storage (0,0).
  - Tooltip (item name) stays.
- **`InventoryOpenCloseController`**: `OpenStorage()` = `Open()` + activate the
  Inventory tab + hide the tab button bar (`Windows`) + `StorageWindow.Show()` +
  `GridCursor.EnterTransferMode()`. `Close()` additionally (when in storage mode)
  exits transfer mode, hides the window and restores the tab bar. Held items are
  returned by the existing `CancelAll → CancelHeld`.

### 5. World → UI

- `StorageOpenRequestedEvent` (empty `readonly struct`) in `NavigationEvents.cs`;
  broker registered in `NavigationScope` with the parent `MessagePipeOptions`.
- `InteractionContext` gains `IPublisher<StorageOpenRequestedEvent>
  StorageOpenPublisher`; `PlayerInteractionCaster` injects and passes it.
- `StorageBoxInteractable` (MonoBehaviour, `IInteractable`):
  `Interact(context) => context.StorageOpenPublisher.Publish(new StorageOpenRequestedEvent())`.
- `InventoryOpenCloseController` subscribes and calls `OpenStorage()`; disposes
  the subscription.
- `Prefabs/Interactables/StorageBox.prefab`: placeholder cube, collider,
  `StorageBoxInteractable`, layer/tag copied from the save-point interactable
  prefab so the interaction caster detects it.

## Testing

TDD, EditMode, shared fakes.

- `InventoryServiceStorageTests`: storage container exists (12×4); automatic
  `TryAdd` never targets storage; `HasItem`/`TryUseKey`/`TryRemove`/
  `HasEquippedWeapon` ignore storage; combine/consume/reload/equip/split rejected
  for storage items without `Changed`; store (operator → storage) and retrieve;
  storage↔operator swap with guaranteed return; cancel returns to storage.
- `InventorySerializerTests` additions: round trip with storage items; storage
  entry falls back to operators; operator entry falls back to storage.
- `InventoryBootstrapTests` additions: new game loads `storageItems` into
  storage; saved state ignores them.
- `GridNavigatorTests` additions: rows — up from any operator enters storage at
  the bottom-left cell; down from storage enters the first operator at top-left;
  edges are no-ops; without rows behaviour unchanged.
- `StorageBoxInteractableTests`: `Interact` publishes the event.
- Play Mode (CLI `eval`): open storage mode via the event, move items both ways,
  close while holding, verify window/tab bar visibility and that the normal
  inventory key does not show the box.
- Manual checklist (owner, real input): open from a placed box, store/retrieve,
  swap, rotate, cancel/close, save → load, scene change, pickups with the box
  full/empty, keys stored in the box don't open doors.

## Risks

- Positional `InteractionContext` construction in tests changes again (one
  added argument).
