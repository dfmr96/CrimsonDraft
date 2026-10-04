# Inventory Refactor — Design

Sub-project 1 of 2. Sub-project 2 (RE-style shared item box) gets its own spec
and builds on the container model defined here.

## Problem

The inventory works, but its structure blocks the item box and keeps producing
view/data divergence bugs:

1. **"Grid index == operator index" is hard-coded everywhere.**
   `GridCursor.GetOperatorOf` uses `gridGroup.IndexOf(grid)`; `InventorySceneInit`,
   `InventoryHUDController` and others recompute `slotIndex / slotsPerOperator`
   (27 occurrences). There is no "container" concept, so a storage box has nowhere
   to live.
2. **The UI mutates the domain behind the service's back, in two phases.**
   `GridCursor` moves the view first; `InventoryHUDController.HandleItemPlaced`
   then tries to sync data (`TrySyncItemToOperatorSlot`) and reverts the view on
   failure (`RevertPlacementToSlot`). Equip, stack-merge, reload and consumable
   use are implemented in the HUD, duplicating (and diverging from)
   `InventoryService`. `CombatOrchestrator.cs:606` decrements `slot.Quantity`
   directly.
3. **Occupancy exists twice.** `InventoryGrid.itemGrid[,]` (view) and
   `InventoryService.BuildOccupancy` (data, rebuilt on every add) are separate
   sources of truth. Items may sit at `(-1,-1)` "in transit".
4. **`InventorySceneInit.EnsureSynced` papers over this** by diffing and
   rebuilding views on every open and after the InspectPanel closes.
5. **Duplication.** `ItemData → InventoryItem` construction is copied 4× with
   inconsistent unknown-type handling. Save capture (`SaveController`) and
   restore (`SaveGameLoader`) are separate hand-written loops; key uses are
   restored by calling `Consume()` in a loop. Cross-scene persistence
   (`InventoryStateRegistry`) is a third path that stores live objects as `object`.
   Quantity lives in both `InventorySlot.Quantity` and `AmmoBoxItem.Quantity`.
6. **`GridCursor` is 888 lines** doing input, navigation, held-item handling,
   melee slot, selector visuals and tooltip. Grid traversal is a fixed cyclic
   left/right walk.
7. **Dead code** from an earlier inventory UI and prototype containers.
8. **Test coverage** stops at `InventoryService`; 9 test files each carry their
   own `FakeInventoryService`.

## Goal

- One source of truth: a 2D-matrix `ItemContainer` per operator, owned by
  `InventoryService`. All mutations go through the service.
- Views are a projection of the model: each grid re-renders from its container
  when the container raises `Changed`. `EnsureSynced` is removed, not renamed.
- One persistence path (serializer) shared by scene transitions and disk saves.
- `GridCursor` split; navigation becomes a pure, tested `GridNavigator` with a
  configurable neighbor graph (needed for the box window).
- Dead code removed from code and from scenes/prefabs.

## Non-goals

- **No item box.** No storage container, UI or interactable in this pass
  (sub-project 2). `ContainerKind.Storage` is defined but unused.
- **No combat panel restructure.** `CombatInventoryPanelController` keeps its own
  navigation and state; only its calls are adapted to the new API.
- **No equipped-weapon state unification.** Equipped state still lives on the
  item, the roster and the `PartyPanelView` widget; the HUD keeps updating the
  widget. `PartyPanelView.Refresh()` on open stays.
- **No visual/UX changes.** Layout, feel, SFX and input mapping stay as they are.

## Design

### 1. Domain model (`CrimsonDraft.Inventory`, pure C#)

```csharp
public enum ContainerKind { Operator, Storage }

public readonly struct ContainerId : IEquatable<ContainerId>
{
    public ContainerKind Kind { get; }
    public int Index { get; }
    public static ContainerId Operator(int index);
    public static ContainerId Storage { get; }
}

public sealed class ItemPlacement
{
    public InventoryItem Item { get; }
    public Vector2Int Origin { get; }
    public int Rotation { get; }          // 0 or 1
    public Vector2Int Footprint { get; }  // GridSize, swapped when Rotation == 1
}

public sealed class ItemContainer
{
    private readonly InventoryItem?[,] cells;                          // [col,row]
    private readonly Dictionary<InventoryItem, ItemPlacement> placements;

    public ContainerId Id { get; }
    public int Width { get; }
    public int Height { get; }
    public IEnumerable<ItemPlacement> Placements { get; }
    public event Action? Changed;

    public InventoryItem? GetItemAt(Vector2Int cell);
    public ItemPlacement? GetPlacement(InventoryItem item);
    public bool Contains(InventoryItem item);
    public bool CanPlace(Vector2Int footprint, Vector2Int origin, InventoryItem? ignore = null);
    public bool TryFindFreeCell(Vector2Int footprint, out Vector2Int origin);

    internal void Place(InventoryItem item, Vector2Int origin, int rotation);
    internal void Remove(InventoryItem item);
    internal void Clear();
    internal void NotifyChanged();   // item state changed in place (quantity, ammo, equip)
}
```

**Invariants**
- `cells` and `placements` are only written by `Place`/`Remove`/`Clear`, always
  together. Every item in `placements` occupies exactly its footprint in `cells`.
- Every item owned by the inventory is either placed in exactly one container or
  is the single `Held` item. No `(-1,-1)` state.
- Operator containers are `InventoryConstants.OperatorGridWidth × Height` (4×4).
- Container instances are created once and never replaced; restore clears and
  refills them so subscribers stay attached.
- `Changed` fires once per successful public service operation per affected
  container, and never on a rejected operation.

**Quantity** lives only on the item. `InventoryItem.Quantity` / `AddQuantity`
become real for every stackable type (backed by a protected field, clamped to
`MaxStack`); `AmmoBoxItem` uses the base implementation. `InventorySlot` is
deleted.

**`InventoryItemFactory`** — `static InventoryItem Create(ItemData data, int quantity = 0)`
replaces the four `switch` copies. Unknown `ItemData` subtype throws
`ArgumentException`. `quantity <= 0` means the type's default
(`AmmoBoxData.DefaultQuantity`, otherwise 1).

**`KeyItem.RestoreUses(int usesRemaining)`** (internal) replaces the
`Consume()` loop.

### 2. `IInventoryService`

Index-based API is replaced by an item-reference API.

```csharp
public interface IInventoryService
{
    IReadOnlyList<ItemContainer> OperatorContainers { get; }
    ItemContainer GetContainer(ContainerId id);
    ContainerId? FindContainerOf(InventoryItem item);

    InventoryItem? Held { get; }
    event Action? HeldChanged;

    bool TryAdd(ItemData data, int quantity = 0);                    // operators in order
    bool TryAdd(ItemData data, ContainerId target, int quantity = 0);
    void Remove(InventoryItem item);
    bool TryRemove(string itemId);
    bool HasItem(string itemId);

    bool TryPickUp(InventoryItem item);
    bool TrySplit(InventoryItem stack);
    DropResult TryDrop(ContainerId target, Vector2Int origin, int rotation);
    void CancelHeld();

    bool TryCombine(InventoryItem a, InventoryItem b, out InventoryItem? result);
    bool TryUseConsumable(InventoryItem item, int operatorSlot);
    bool CanReload(InventoryItem ammo, int operatorSlot);
    bool TryReload(InventoryItem ammo, int operatorSlot);
    void Equip(WeaponItem weapon, int operatorSlot);
    void Unequip(WeaponItem weapon);
    KeyUseOutcome TryUseKey(string keyItemId);

    void Restore(IReadOnlyList<InventoryItemEntry> entries, ItemDatabase database);
}

public enum DropResult { Placed, Swapped, Rejected }
```

**Behavior rules** (all preserve today's gameplay):
- `TryAdd(data)` stacks into an existing non-full stack first (any operator, in
  order), then places at the first free cell of the first operator that fits.
  Same per-container rule for `TryAdd(data, target)`.
- `TryPickUp` rejects equipped items. It removes the item from its container and
  records its origin (container, origin, rotation) for cancel.
- `TrySplit` requires a stackable with quantity > 1 and an empty hand. Half
  (rounded down) goes to `Held` as a new item; cancel merges it back into the
  source stack.
- `TryDrop`: out of bounds or overlapping 2+ items → `Rejected`. Empty footprint
  → `Placed`. Exactly one overlapped, non-equipped item → `Swapped` only if that
  item can be guaranteed a return spot (fits at the held item's recorded origin,
  else a free cell in the origin container); the displaced item becomes `Held`
  with that spot as its cancel origin. Otherwise `Rejected`.
- `CancelHeld` returns the held item to its cancel origin (split: merge back).
  Called by the UI on close; the hand is always empty outside the inventory UI.
- `TryCombine` resolves, in order: same stackable → merge quantities up to
  `MaxStack` (source removed if emptied); ammo + weapon of matching caliber →
  reload (ammo removed if emptied); recipe via `ICombineService` → result placed
  at a free cell (preferring the first input's container, considering cells the
  inputs free). Any failure leaves the inventory untouched.
- `TryUseConsumable` heals a living operator and decrements/removes the stack.
  Used by navigation HUD and combat (replaces `CombatOrchestrator`'s direct
  `slot.Quantity--`).
- `Equip`/`Unequip` update item + roster for the weapon's `WeaponSlot`, replacing
  only the weapon in that slot; they raise `Changed` on the affected containers so
  the equipped tint re-renders.

`CombatInventoryPanelController`, `CombatOrchestrator`, `InventoryHUDController`,
`InspectPanel`, `PickupInteractable`, `MapPickupInteractable`, `HotspotReward`,
`ItemSocketInteractable` and the door interactables are updated to this API.

### 3. UI (`CrimsonDraft.UI`)

| Piece | Kind | Responsibility |
|---|---|---|
| `InventoryGrid` | MonoBehaviour | Visual only: serialized `ContainerKind` + index, `GridNeighbors`, `CellToLocal`, `CellSize`, background. Loses `itemGrid`, `CanPlace`, `PlaceItem`, `RemoveItem`, `GetItemAt`, `GetOverlappingItem`, `IsWithinBounds`. |
| `GridNeighbors` | serialized struct on `InventoryGrid` | `left`/`right`/`up`/`down` grid references (null = none). Configured in the prefab to reproduce today's cyclic walk. |
| `InventoryItemView` | MonoBehaviour | Stateless display: `Bind(InventoryItem, ItemPlacement, float cellSize)`; quantity, rotation and equipped tint come from the item. Loses `OwnerGrid`, `GridOrigin`, `Rotate`. |
| `ContainerGridPresenter` | pure C# | One per grid. Subscribes to its container's `Changed` and re-renders all views from `Placements`. If the grid is inactive, marks itself pending and renders on activation. Owns the `item → view` map. |
| `InventoryPresenters` | pure C#, `IInitializable`/`IDisposable` | Builds a presenter per grid in `InventoryGridGroup`; exposes `FindView(item)`, `GetGrid(ContainerId)`. |
| `GridNavigator` | pure C# | State: current grid + cell. `Move(dir)` returns the new position or `ExitUp`. Skips across multi-cell items via the container; crosses grids via `GridNeighbors`, clamping the row to the destination's height. |
| `GridCursor` | MonoBehaviour | Input with auto-repeat, input routing (menu / inspect / tab bar / grid), delegates movement to `GridNavigator` and pick/drop/split to the service, melee pseudo-slot. Target ≤ ~300 lines. |
| `SelectorView` | MonoBehaviour | Selector position, size, sprite, hover scale, tooltip. |
| `HeldItemView` | MonoBehaviour | Single floating view of `service.Held` over the cursor cell; tint shows `container.CanPlace` validity. Listens to `HeldChanged`. |
| `InventoryHUDController` | pure C# | Context menu → service calls; combine-mode UI state; party widget updates for equip. No grid manipulation, no slot lookups. |

`InventoryOpenCloseController.Open` no longer calls `EnsureSynced`; `Close`
calls `service.CancelHeld()` via `GridCursor.CancelAll`. `InspectPanel` no longer
calls `EnsureSynced` after rewards.

`CombatInventoryPanelController` binds a `ContainerGridPresenter` to the active
operator's container and queries the container instead of `InventoryGrid`.

### 4. Persistence

```csharp
[Serializable]
public sealed class InventoryItemEntry
{
    public ContainerKind containerKind;
    public int containerIndex;
    public string itemId = "";
    public int quantity;
    public int col, row, rotation;
    public int weaponAmmo = -1;
    public int keyUsesRemaining = -1;
    public bool isExamined;
    public int equippedOperatorSlot = -1;
    public int equippedWeaponSlot = -1;
}

public static class InventorySerializer
{
    public static List<InventoryItemEntry> Capture(IInventoryService inventory);
    public static List<InventoryItemEntry> FromLegacySlots(IReadOnlyList<InventorySlotEntry> slots);
}
```

- `IInventoryService.Restore(entries, database)` clears all containers, creates
  items through the factory, applies item state, places them, re-wires equipped
  weapons to the roster, and raises `Changed` per container.
- Invalid saved placement (overlap / out of bounds): free cell in the same
  container → free cell in another operator → `Debug.LogError` and drop the item.
  Unknown `itemId`: skipped (same as today).
- **Scene transitions:** `InventoryStateRegistry` becomes typed
  (`Save(IReadOnlyList<InventoryItemEntry>)`, `Load()`, `ClearAll()`).
  `InventoryBootstrap.Dispose` captures; `Initialize` restores. Same code path as
  disk.
- **Disk:** `SaveGameData` gains `List<InventoryItemEntry> inventoryItems`.
  `SaveController` writes it via `Capture`. `SaveGameLoader` restores from it; if
  it is empty and the legacy `inventorySlots` is not, it converts with
  `FromLegacySlots` (operator = `slotIndex / 16`, `-1` positions → free cell at
  restore). `inventorySlots` is read-only from now on.
- `GameStateResetter` unchanged.

### 5. Deletions

**Scenes are not modified in this pass — only prefabs.** Prefab edits are done
through the live Editor, not by hand-editing YAML. Deleting a class that a scene
still references leaves a "Missing Script" component in that scene (harmless at
runtime); those are listed below for the owner to clean up later.

| Delete | Prefab cleanup | Scenes left with a Missing Script (not edited) |
|---|---|---|
| `Navigation/UI/InventoryController.cs` | — | — |
| `Navigation/UI/InventoryView.cs` | — | `Production/Navigation.unity`, `Production/New Room.unity`, `Deck B/DeckB_Port_Stairs.unity`, `Deck B/New Room.unity`, `Test/FIX_Deck_B ShadersTest.unity` |
| `Navigation/UI/OperatorInventoryCard.cs`, `InventorySlotCell.cs`, `InventoryActionButton.cs` | Delete `Prefabs/OperatorInventoryCard.prefab`, `Prefabs/InventorySlotCell.prefab`, `Prefabs/UI/InventoryActionButton.prefab` | — |
| `Editor/BuildInventoryPanel.cs`, `Editor/BuildInventoryActionButton.cs` | — | — |
| `UI/Inventory/Ui_manager.cs`, `PickUpPromptUI.cs` | — | — |
| `UI/Inventory/IItemSpawner.cs`, `InventoryPopulator.cs`, `InventorySceneInit.cs` | Remove `InventoryPopulator` from `UI_Canvas.prefab`; `InventoryScope` registrations updated | — |
| `UI/Inventory/TestPickupItem.cs` | Remove from `UI_Canvas.prefab` | — |
| `Navigation/InventoryDebugPrinter.cs` | — (drop optional registration in `NavigationScope`) | `Production/Deck_B_Development.unity` |
| `Navigation/Interactables/ContainerInteractable.cs`, `UI/ContainerController.cs`, `UI/ContainerView.cs`, `Data/ContainerData.cs` (no `ContainerData` assets exist) | Remove `ContainerView` from `UI_Canvas.prefab`; drop `ContainerController` registration and `InteractionContext.ContainerController` | `ContainerInteractable`: `Production/Navigation.unity`. `ContainerView`: the five scenes listed for `InventoryView` |
| `Inventory/InventorySlot.cs` | — | — |

Before deleting, each class is checked for serialized references from *other*
scene components (e.g. a field typed `ContainerView` on a scope) so that no live
component loses a required reference; if one exists, that field is removed in
code, not in the scene.

Kept: `OperatorTestWidget`, `PartyTestHarness` (roster/test-scene tools, editor
and dev builds only).

## Testing

TDD, EditMode, plain C# fakes (project convention).

- **Shared fake:** the 9 private `FakeInventoryService` classes are replaced by
  one `Tests/EditMode/Fakes/FakeInventoryService.cs` with call recording.
- **New suites:** `ItemContainerTests`, `InventoryItemFactoryTests`,
  `InventorySerializerTests`, `GridNavigatorTests`.
- **Rewritten:** `InventoryServiceTests` against the new API — add/auto/stack/no
  space; pick/drop/swap/cancel incl. cross-container swap with guaranteed return;
  split/cancel; combine (merge, reload, recipe, no-space → untouched); equip and
  roster sync; consumables; keys; `Changed` fired on success only.
- **Adapted:** `SaveControllerTests`, `SaveGameLoaderTests`,
  `InventoryStateRegistryTests`, combat/door/pickup/hotspot tests (call-site
  changes only).
- **Manual Play Mode checklist** (`Deck_B_Development`): open/close; move within
  and across operators; swap and cancel while holding; split ammo; combine
  (merge, reload, recipe); equip; pickup with UI closed; InspectPanel reward with
  UI open; spend ammo in combat and return; save → load; scene transition.
- **Done when:** no compile errors or warnings, full EditMode suite green,
  manual checklist passes.

## Risks

- **Prefab rewiring.** `InventoryGrid` fields change; `UI_Canvas.prefab` and
  `CombatInventoryPanel.prefab` need their grids configured (container binding,
  neighbors). Done via the live Editor and verified in Play Mode.
- **Scope ordering.** Presenters (InventoryScope) subscribe to containers owned
  by the service (NavigationScope). Stable container instances make this
  order-independent.
- **Broad call-site churn.** Many classes move from slot indices to item
  references; compile errors guide the sweep, tests guard behavior.
