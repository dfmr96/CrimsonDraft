# Storage Box Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a shared RE-style item box: a 12×4 storage container that opens in a transfer-only "storage mode" of the existing inventory screen, persisted with the rest of the inventory.

**Architecture:** The domain gains one `ContainerId.Storage` container inside `InventoryService`, excluded from every "carried" query and from item actions. `GridNavigator` learns optional grid *rows* for proportional vertical moves between the storage grid and the operator grids. The UI adds a `StorageWindow` (inactive by default) inside the inventory tab, a transfer mode in `GridCursor`, and `InventoryOpenCloseController.OpenStorage()`, triggered by a `StorageOpenRequestedEvent` published by `StorageBoxInteractable`.

**Tech Stack:** Unity 6000.3 (C# 9), VContainer, MessagePipe, NUnit EditMode, Unity CLI (`unity recompile`, `unity command run_tests`, `unity command eval_file`).

**Spec:** `docs/superpowers/specs/2026-10-04-storage-box-design.md`

## Global Constraints

- Branch `refactor/inventory`. **No per-task commits** (standing user preference from the refactor run): all work is committed once in Task 9. Never stage the user's unrelated changes (`Assets/Data/Map/MapData_DeckB.asset`, `MapData_DeckC.asset`, `Assets/Scenes/Production/Deck_B_Development.unity`, `Assets/Art/UI/Boot*`). No `Co-Authored-By` trailer.
- **No scene edits.** Prefab edits only, through the live Editor (`unity command eval_file`), applied to the **source** prefab (`Prefabs/UI/Inventory/UI_Inventry_Root.prefab` for anything inside the inventory UI; `Prefabs/Core/UI_Canvas.prefab` only for fields of components that live directly in UI_Canvas, i.e. `InventoryOpenCloseController`).
- Every C# file starts with `#nullable enable`; DI constructors carry `[Preserve]`; serialized fields use `= null!` or `?`. C# 9 only.
- Paths are relative to `Game/CrimsonDraft/Assets/` unless they start with `docs/`.
- Storage size: `InventoryConstants.StorageGridWidth = 12`, `StorageGridHeight = 4`.
- After Play Mode sessions, restore `TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` with `git checkout --` if it shows as modified (dynamic atlas side effect), and never leave files under `Assets/Temp`.

### Verification procedure

- **Compile + tests:** `bash .superpowers/sdd/2026-10-04-storage-box/ut.sh [Fully.Qualified.TestClass ...]` (no args = full suite). The script runs `unity recompile` then `unity command run_tests --mode EditMode`, and passes only when every failure is a known pre-existing one (the 5 `CrimsonDraft.Tests.ItemSocketInteractableTests.*` and the flaky `ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds`). Create it in Task 1 Step 0.
- **Editor snippets:** write the snippet to a file and run `unity command eval_file --file <path> --timeout 120 --no-banner --result-only` from `Game/CrimsonDraft`.

## Review Focus

1. **A key stored in the box must not open a door** (`TryUseKey`/`HasItem` ignore storage) — pinned in Task 1 (`TryUseKey_andHasItem_ignoreStorage`).
2. **Closing storage mode while holding an item taken from the box** — it must go back to the box, not to an operator — pinned in Task 1 (`CancelHeld_returnsItemTakenFromStorageToStorage`).
3. **Swap between box and operator where the displaced item can't return** — rejected, nothing moved — pinned in Task 1 (`TryDrop_swapIntoStorageRejected_whenDisplacedCannotReturn`).
4. **Pickups with operators full must not land in the box** — pinned in Task 1 (`TryAdd_auto_neverUsesStorage`).
5. **Normal inventory key after a storage session** — tabs visible, box hidden, cursor in normal mode — verified in Task 8 (Play Mode probe `storage_normal_after.cs`).

---

### Task 1: Storage container in the domain

**Files:**
- Modify: `Scripts/Inventory/InventoryConstants.cs`
- Modify: `Scripts/Inventory/InventoryService.cs`, `InventoryService.Actions.cs`, `InventoryService.Held.cs`
- Modify: `Tests/EditMode/InventoryServiceTests.cs` (replace `GetContainer_storage_throws`)
- Modify: `Tests/EditMode/Fakes/FakeInventoryService.cs`
- Test: `Tests/EditMode/InventoryServiceStorageTests.cs` (partial of `InventoryServiceTests`)

**Interfaces:**
- Produces: `GetContainer(ContainerId.Storage)` returns a 12×4 container; private helpers `ItemContainer EnsureStorage()`, `IEnumerable<ItemContainer> AllContainers()`, `ItemContainer? FindCarriedContainerOf(InventoryItem)`; `FindContainerObjectOf` now searches operators + storage. `FakeInventoryService.Storage` (12×4 container) and `GetContainer(ContainerId.Storage)` support.

- [ ] **Step 0: Create the verification script** `.superpowers/sdd/2026-10-04-storage-box/ut.sh` (repo root relative; the directory is git-ignored):

```bash
#!/usr/bin/env bash
set -uo pipefail
proj="/d/Proyectos Unity/CrimsonDraft/CrimsonDraft/Game/CrimsonDraft"
ws="$(cd "$(dirname "$0")" && pwd)"
cd "$proj"
if ! unity recompile --no-banner > "$ws/last-compile.log" 2>&1; then
  echo "COMPILE FAILED:"; tail -n 40 "$ws/last-compile.log"; exit 1
fi
filters=("$@"); [ ${#filters[@]} -eq 0 ] && filters=("")
rc=0
for f in "${filters[@]}"; do
  args=(--mode EditMode --timeout 900 --result-only); [ -n "$f" ] && args+=(--filter "$f")
  unity command run_tests "${args[@]}" > "$ws/last-tests.json" 2>&1
  python - "$ws/last-tests.json" "${f:-ALL}" <<'PY' || rc=1
import json, sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
try: data = json.loads(raw[raw.index("{"):])
except Exception: print("NO JSON RESULT:\n" + raw[-2000:]); sys.exit(1)
s = data.get("Summary", {})
fails = [r for r in data.get("Results", []) if r.get("Status") == "Failed"]
known = [r for r in fails if r["FullName"].startswith("CrimsonDraft.Tests.ItemSocketInteractableTests.") or r["FullName"] == "CrimsonDraft.Tests.ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds"]
new = [r for r in fails if r not in known]
print(f"[{sys.argv[2]}] total={s.get('Total')} passed={s.get('Passed')} failed={s.get('Failed')} (known-baseline={len(known)}, new={len(new)})")
for r in new: print("  FAIL", r["FullName"], "::", (r.get("Message") or "").strip()[:300])
sys.exit(1 if new or s.get("Total", 0) == 0 else 0)
PY
done
exit $rc
```

Run it with no args. Expected: `[ALL] total=443 … new=0`.

- [ ] **Step 1: Write the failing tests**

In `Tests/EditMode/InventoryServiceTests.cs` replace the test `GetContainer_storage_throws` with:

```csharp
        [Test]
        public void GetContainer_storage_isTwelveByFour()
        {
            var storage = Service().GetContainer(ContainerId.Storage);
            Assert.AreEqual(ContainerId.Storage, storage.Id);
            Assert.AreEqual(12, storage.Width);
            Assert.AreEqual(4, storage.Height);
        }
```

Create `Tests/EditMode/InventoryServiceStorageTests.cs`:

```csharp
#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        private static ItemContainer Box(InventoryService s) => s.GetContainer(ContainerId.Storage);

        private static InventoryItem Stored(InventoryService s, ItemData data, int quantity = 0)
        {
            Assert.IsTrue(s.TryAdd(data, ContainerId.Storage, quantity));
            return Box(s).Placements.Last(p => p.Item.Data == data).Item;
        }

        [Test]
        public void TryAdd_auto_neverUsesStorage()
        {
            var s = Service(operators: 1);
            s.TryAdd(Sized(Consumable(), 4, 4));

            Assert.IsFalse(s.TryAdd(Consumable()));
            Assert.AreEqual(0, Box(s).Count);
        }

        [Test]
        public void TryUseKey_andHasItem_ignoreStorage()
        {
            var s = Service();
            Stored(s, Key(id: "door-key"));

            Assert.IsFalse(s.HasItem("door-key"));
            Assert.AreEqual(KeyUseResult.NotFound, s.TryUseKey("door-key").Result);
            Assert.IsFalse(s.TryRemove("door-key"));
            Assert.AreEqual(1, Box(s).Count);
        }

        [Test]
        public void HasEquippedWeapon_ignoresStorage_andEquipFromStorageIsNoOp()
        {
            var roster = FakeRoster.WithOperators(1);
            var s      = Service(roster);
            var weapon = (WeaponItem)Stored(s, Weapon());

            s.Equip(weapon, 0);

            Assert.IsFalse(weapon.IsEquipped);
            Assert.IsNull(roster[0].PrimaryWeapon);
            Assert.IsFalse(s.HasEquippedWeapon(0));
        }

        [Test]
        public void Actions_onStoredItems_areRejected_withoutChanged()
        {
            var a       = Consumable();
            var b       = Consumable();
            var s       = Service(combine: new FakeCombineService(a, b, Consumable()));
            var storedA = Stored(s, a);
            var carried = AddAt(s, b, 0);
            var ammo    = Stored(s, Ammo(Caliber._9mm, defaultQuantity: 10));
            var weapon  = (WeaponItem)AddAt(s, Weapon(Caliber._9mm, magazine: 6), 0);
            s.Equip(weapon, 0);

            int changes = CountChanges(Box(s), () =>
            {
                Assert.IsFalse(s.TryCombine(storedA, carried, out _));
                Assert.IsFalse(s.TryUseConsumable(storedA, 0));
                Assert.IsFalse(s.CanReload(ammo, 0));
                Assert.IsFalse(s.TryReload(ammo, 0));
                Assert.IsFalse(s.CanSplit(ammo));
                Assert.IsFalse(s.TrySplit(ammo));
            });

            Assert.AreEqual(0, changes);
            Assert.AreEqual(0, weapon.CurrentAmmo);
        }

        [Test]
        public void StoreAndRetrieve_moveItemsBetweenOperatorAndStorage()
        {
            var s    = Service();
            var item = AddAt(s, Consumable(id: "med"), 0);

            int boxChanges = CountChanges(Box(s), () =>
            {
                Assert.IsTrue(s.TryPickUp(item));
                Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Storage, new Vector2Int(5, 2)));
            });

            Assert.AreEqual(1, boxChanges);
            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(item));
            Assert.IsFalse(s.HasItem("med"));

            Assert.IsTrue(s.TryPickUp(item));
            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.IsTrue(s.HasItem("med"));
        }

        [Test]
        public void TryDrop_swapBetweenStorageAndOperator_returnsDisplacedToStorage()
        {
            var s       = Service();
            var fromBox = Stored(s, Consumable());
            var onOp    = AddAt(s, Sized(Consumable(), 2, 1), 0);

            s.TryPickUp(fromBox);
            Assert.AreEqual(DropResult.Swapped, s.TryDrop(ContainerId.Operator(0), Vector2Int.zero));
            Assert.AreSame(onOp, s.Held);

            s.CancelHeld();
            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(onOp));
            Assert.AreEqual(ContainerId.Operator(0), s.FindContainerOf(fromBox));
        }

        [Test]
        public void TryDrop_swapIntoStorageRejected_whenDisplacedCannotReturn()
        {
            var s       = Service(operators: 1);
            var single  = AddAt(s, Consumable(), 0);
            AddAt(s, Sized(Consumable(), 3, 4), 0);
            AddAt(s, Sized(Consumable(), 1, 3), 0);
            var wide    = Stored(s, Sized(Consumable(), 2, 1));
            var wideAt  = Box(s).GetPlacement(wide)!.Origin;

            s.TryPickUp(single);
            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Storage, wideAt));
            Assert.AreSame(single, s.Held);
            Assert.AreEqual(wideAt, Box(s).GetPlacement(wide)!.Origin);
        }

        [Test]
        public void CancelHeld_returnsItemTakenFromStorageToStorage()
        {
            var s    = Service();
            var item = Stored(s, Consumable());
            var at   = Box(s).GetPlacement(item)!.Origin;

            s.TryPickUp(item);
            s.CancelHeld();

            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(item));
            Assert.AreEqual(at, Box(s).GetPlacement(item)!.Origin);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

`ut.sh CrimsonDraft.Tests.InventoryServiceTests`. Expected: test failures (storage container doesn't exist: `GetContainer(Storage)` throws `ArgumentOutOfRangeException`), not compile errors.

- [ ] **Step 3: Implement**

`Scripts/Inventory/InventoryConstants.cs` — add inside the class:

```csharp
        public const int StorageGridWidth  = 12;
        public const int StorageGridHeight = 4;
```

`Scripts/Inventory/InventoryService.cs`:
- add field `private ItemContainer? storageContainer;` under `operatorContainers`;
- in `GetContainer`, before the operator check, add `if (id == ContainerId.Storage) return EnsureStorage();`;
- replace `FindContainerObjectOf` with the two lookups below and add the two helpers:

```csharp
        private ItemContainer EnsureStorage() =>
            this.storageContainer ??= new ItemContainer(
                ContainerId.Storage,
                InventoryConstants.StorageGridWidth,
                InventoryConstants.StorageGridHeight);

        private IEnumerable<ItemContainer> AllContainers() => EnsureContainers().Append(EnsureStorage());

        private ItemContainer? FindContainerObjectOf(InventoryItem item) =>
            AllContainers().FirstOrDefault(c => c.Contains(item));

        private ItemContainer? FindCarriedContainerOf(InventoryItem item) =>
            EnsureContainers().FirstOrDefault(c => c.Contains(item));
```

(`AllPlacements()` keeps using `EnsureContainers()` — carried only.)

`Scripts/Inventory/InventoryService.Actions.cs`:
- `TryCombine`: `var containerA = FindCarriedContainerOf(a);` and `var containerB = FindCarriedContainerOf(b);`
- `TryUseConsumable`: `var container = FindCarriedContainerOf(item);`
- `CanReload`: replace `FindContainerObjectOf(box) == null` with `FindCarriedContainerOf(box) == null`
- `Equip`: first line of the method body: `if (FindCarriedContainerOf(weapon) == null) return;`

`Scripts/Inventory/InventoryService.Held.cs` — in `CanSplit` replace `FindContainerObjectOf(stack) == null` with `FindCarriedContainerOf(stack) == null`.

`Tests/EditMode/Fakes/FakeInventoryService.cs`:
- add field `private readonly ItemContainer storage = new ItemContainer(ContainerId.Storage, 12, 4);` and property `public ItemContainer Storage => this.storage;`
- `GetContainer(ContainerId id) => id == ContainerId.Storage ? this.storage : this.containers.First(c => c.Id == id);`
- `FindContainerOf(item) => this.storage.Contains(item) ? ContainerId.Storage : this.containers.FirstOrDefault(c => c.Contains(item))?.Id;`

- [ ] **Step 4: Run tests**

`ut.sh CrimsonDraft.Tests.InventoryServiceTests` then `ut.sh`. Expected: InventoryServiceTests 51/51; full suite no new failures.

---

### Task 2: Persist the storage container

**Files:**
- Modify: `Scripts/Inventory/InventorySerializer.cs` (`Capture`)
- Modify: `Scripts/Inventory/InventoryService.Persistence.cs`
- Test: `Tests/EditMode/InventorySerializerTests.cs` (add tests)

**Interfaces:**
- Consumes: `GetContainer(ContainerId.Storage)`, `AllContainers()`, `EnsureStorage()` (Task 1).
- Produces: storage placements captured with `containerKind = (int)ContainerKind.Storage, containerIndex = 0`; restore fallback order preferred → operators → storage.

- [ ] **Step 1: Write the failing tests** — add to `InventorySerializerTests`:

```csharp
        private static InventoryItemEntry StorageEntry(string id, int col, int row, int quantity = 0) =>
            new InventoryItemEntry
            {
                containerKind = (int)ContainerKind.Storage,
                itemId        = id,
                col           = col,
                row           = row,
                quantity      = quantity,
            };

        [Test]
        public void CaptureThenRestore_roundTripsStorageItems()
        {
            var ammo   = Ammo(defaultQuantity: 30, id: "ammo");
            var source = Service();
            source.TryAdd(ammo, ContainerId.Storage, 17);
            var stored = source.GetContainer(ContainerId.Storage).Placements.Single();
            source.TryPickUp(stored.Item);
            source.TryDrop(ContainerId.Storage, new Vector2Int(9, 3));

            var entries  = InventorySerializer.Capture(source);
            var restored = Service();
            restored.Restore(entries, Database(ammo));

            var placement = restored.GetContainer(ContainerId.Storage).Placements.Single();
            Assert.AreEqual(new Vector2Int(9, 3), placement.Origin);
            Assert.AreEqual(17, placement.Item.Quantity);
            Assert.AreEqual(0, Op(restored, 0).Count);
        }

        [Test]
        public void Restore_storageEntryWithoutRoom_fallsBackToOperators()
        {
            var huge  = Sized(Consumable(id: "huge"), 12, 4);
            var small = Consumable(id: "small");
            var s     = Service();

            s.Restore(new[] { StorageEntry("huge", 0, 0), StorageEntry("small", 0, 0) }, Database(huge, small));

            Assert.AreEqual(1, s.GetContainer(ContainerId.Storage).Count);
            Assert.AreEqual(1, Op(s, 0).Count);
        }

        [Test]
        public void Restore_operatorEntryWithoutRoom_fallsBackToStorage()
        {
            var big   = Sized(Consumable(id: "big"), 4, 4);
            var small = Consumable(id: "small");
            var s     = Service(FakeRoster.WithOperators(1));

            s.Restore(new[] { Entry("big", 0, 0, 0), Entry("small", 0, 0, 0) }, Database(big, small));

            Assert.AreEqual(1, Op(s, 0).Count);
            Assert.AreEqual(1, s.GetContainer(ContainerId.Storage).Count);
        }
```

Also update the existing `Restore_withNoRoomAnywhere_dropsItemAndLogsError` so that "no room anywhere" still holds now that storage exists: fill storage first by adding `Entry`-independent setup — replace its body with:

```csharp
            var big    = Sized(Consumable(id: "big"), 4, 4);
            var boxful = Sized(Consumable(id: "boxful"), 12, 4);
            var small  = Consumable(id: "small");
            var s      = Service(FakeRoster.WithOperators(1));

            LogAssert.Expect(LogType.Error, new Regex("small"));
            s.Restore(new[] { Entry("big", 0, 0, 0), StorageEntry("boxful", 0, 0), Entry("small", 0, 0, 0) }, Database(big, boxful, small));
            Assert.AreEqual(1, Op(s, 0).Count);
            Assert.AreEqual(1, s.GetContainer(ContainerId.Storage).Count);
```

- [ ] **Step 2: Run tests to verify they fail**

`ut.sh CrimsonDraft.Tests.InventorySerializerTests`. Expected: the three new tests fail (storage not captured / not restored / no storage fallback); `Restore_withNoRoomAnywhere…` may also fail.

- [ ] **Step 3: Implement**

`InventorySerializer.Capture` — replace the `return inventory.OperatorContainers…` expression with:

```csharp
            return inventory.OperatorContainers
                .Append(inventory.GetContainer(ContainerId.Storage))
                .SelectMany(c => c.Placements.Select(p => ToEntry(c.Id, p.Item, p.Origin, p.Rotation)))
                .ToList();
```

`InventoryService.Persistence.cs` — make the placement helpers instance methods that know about storage. Replace from `var containers = EnsureContainers();` through the end of the file's helpers with this version of `Restore` and helpers (keep `WireEquipped` and `ApplyEntryState` unchanged):

```csharp
        public void Restore(IReadOnlyList<InventoryItemEntry> entries, ItemDatabase database)
        {
            if (this.held != null) ClearHeld();
            foreach (var container in AllContainers()) container.Clear();
            for (int op = 0; op < this.roster.Count; op++)
                this.roster[op].SetEquippedWeapon(null, (int)WeaponSlot.Primary);

            var unplaced = new List<(InventoryItem item, InventoryItemEntry entry)>();
            foreach (var entry in entries)
            {
                if (!database.TryGetById(entry.itemId, out var data)) continue;

                var item = InventoryItemFactory.Create(data, entry.quantity);
                ApplyEntryState(item, entry);

                if (TryPlaceAtSavedPosition(item, entry)) WireEquipped(item, entry);
                else unplaced.Add((item, entry));
            }

            foreach (var (item, entry) in unplaced)
            {
                if (!TryPlaceAnywhere(item, entry))
                {
                    Debug.LogError($"Inventory restore: no room for '{entry.itemId}', item dropped.");
                    continue;
                }
                WireEquipped(item, entry);
            }

            NotifyChanged(AllContainers().ToArray());
        }

        private ItemContainer? PreferredContainer(InventoryItemEntry entry)
        {
            if (entry.containerKind == (int)ContainerKind.Storage) return EnsureStorage();
            var operators = EnsureContainers();
            return entry.containerIndex >= 0 && entry.containerIndex < operators.Length ? operators[entry.containerIndex] : null;
        }

        private bool TryPlaceAtSavedPosition(InventoryItem item, InventoryItemEntry entry)
        {
            var preferred = PreferredContainer(entry);
            var origin    = new Vector2Int(entry.col, entry.row);
            var footprint = ItemPlacement.FootprintOf(item.Data.GridSize, entry.rotation);
            if (preferred == null || !preferred.CanPlace(footprint, origin)) return false;

            preferred.Place(item, origin, entry.rotation);
            return true;
        }

        private bool TryPlaceAnywhere(InventoryItem item, InventoryItemEntry entry)
        {
            var preferred  = PreferredContainer(entry);
            var candidates = new List<ItemContainer>();
            if (preferred != null) candidates.Add(preferred);
            candidates.AddRange(EnsureContainers().Where(c => c != preferred));
            if (preferred != EnsureStorage()) candidates.Add(EnsureStorage());

            foreach (var container in candidates)
                for (int rotation = 0; rotation <= 1; rotation++)
                {
                    var footprint = ItemPlacement.FootprintOf(item.Data.GridSize, rotation);
                    if (!container.TryFindFreeCell(footprint, out var origin)) continue;
                    container.Place(item, origin, rotation);
                    return true;
                }

            return false;
        }
```

Delete the old `static` `PreferredContainer(ItemContainer[], …)`, `TryPlaceAtSavedPosition(ItemContainer[], …)` and `TryPlaceAnywhere(ItemContainer[], …)`.

- [ ] **Step 4: Run tests**

`ut.sh CrimsonDraft.Tests.InventorySerializerTests CrimsonDraft.Tests.SaveControllerTests CrimsonDraft.Tests.SaveGameLoaderTests` then `ut.sh`. Expected: serializer 13/13; no new failures.

---

### Task 3: Initial storage contents in `StartingLoadout`

**Files:**
- Modify: `Scripts/Navigation/StartingLoadout.cs`
- Modify: `Scripts/Navigation/InventoryBootstrap.cs`
- Test: `Tests/EditMode/InventoryBootstrapTests.cs` (add tests)

**Interfaces:**
- Produces: `StartingLoadout.StorageItems : StartingItemEntry[]` (serialized `storageItems`).

- [ ] **Step 1: Write the failing tests** — add to `InventoryBootstrapTests` (add `using UnityEditor;`):

```csharp
        private InventoryBootstrap BuildWithStorage(ItemData data, int quantity)
        {
            var loadout = ScriptableObject.CreateInstance<StartingLoadout>();
            var so      = new SerializedObject(loadout);
            var items   = so.FindProperty("storageItems");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = data;
            items.GetArrayElementAtIndex(0).FindPropertyRelative("quantity").intValue         = quantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return new InventoryBootstrap(loadout, this.inventory, this.registry, this.roster, Database(this.med, data));
        }

        [Test]
        public void Initialize_newGame_addsStorageItemsToStorage()
        {
            var ammo = Ammo(defaultQuantity: 30, id: "ammo");
            BuildWithStorage(ammo, 12).Initialize();

            var stored = this.inventory.GetContainer(ContainerId.Storage).Placements.Single();
            Assert.AreSame(ammo, stored.Item.Data);
            Assert.AreEqual(12, stored.Item.Quantity);
        }

        [Test]
        public void Initialize_withSavedState_ignoresStorageItems()
        {
            this.registry.Save(new List<InventoryItemEntry>());
            BuildWithStorage(Ammo(id: "ammo"), 12).Initialize();
            Assert.AreEqual(0, this.inventory.GetContainer(ContainerId.Storage).Count);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

`ut.sh CrimsonDraft.Tests.InventoryBootstrapTests`. Expected: `Initialize_newGame_addsStorageItemsToStorage` fails (NullReferenceException on `FindProperty("storageItems")` or zero placements).

- [ ] **Step 3: Implement**

`StartingLoadout.cs` — add under `defaultMelee`:

```csharp
        [SerializeField] private StartingItemEntry[] storageItems   = Array.Empty<StartingItemEntry>();
```

and under `DefaultMelee`:

```csharp
        public StartingItemEntry[] StorageItems   => this.storageItems;
```

and update the `operatorSlot` field comment in `StartingItemEntry` to: `// which operator receives this item (ignored for storageItems)`.

`InventoryBootstrap.Initialize` — after the loop that adds `this.loadout.Items`, add:

```csharp
            foreach (var entry in this.loadout.StorageItems)
                this.inventory.TryAdd(entry.item, ContainerId.Storage, entry.quantity);
```

- [ ] **Step 4: Run tests**

`ut.sh CrimsonDraft.Tests.InventoryBootstrapTests` then `ut.sh`. Expected: 4/4; no new failures.

---

### Task 4: `GridNavigator` rows

**Files:**
- Modify: `Scripts/Inventory/GridNavigator.cs`
- Test: `Tests/EditMode/GridNavigatorTests.cs` (add tests)

**Interfaces:**
- Produces: `GridNavigator(Func<ContainerId, ItemContainer> resolve, IReadOnlyDictionary<ContainerId, GridLinks> links, ContainerId start, IReadOnlyList<IReadOnlyList<ContainerId>>? rows = null)`. With rows, a vertical exit from a grid that belongs to a row moves proportionally into the adjacent row, or stays in place (returns `NavigationExit.None`) at the outer edges.

- [ ] **Step 1: Write the failing tests** — add to `GridNavigatorTests`:

```csharp
        private static readonly ContainerId Op2 = ContainerId.Operator(2);

        private GridNavigator RowNavigator(ContainerId grid, int col, int row)
        {
            this.containers[Op2]                 = new ItemContainer(Op2, 4, 4);
            this.containers[ContainerId.Storage] = new ItemContainer(ContainerId.Storage, 12, 4);
            this.links[Op0] = new GridLinks(Op2, Op1, null, null);
            this.links[Op1] = new GridLinks(Op0, Op2, null, null);
            this.links[Op2] = new GridLinks(Op1, Op0, null, null);
            var rows = new List<IReadOnlyList<ContainerId>>
            {
                new[] { ContainerId.Storage },
                new[] { Op0, Op1, Op2 },
            };
            var nav = new GridNavigator(id => this.containers[id], this.links, grid, rows);
            nav.Reset(grid, new Vector2Int(col, row));
            return nav;
        }

        [Test]
        public void Rows_upFromOperator_entersStorageAtProportionalColumn()
        {
            var nav = RowNavigator(Op1, col: 2, row: 0);
            Assert.AreEqual(NavigationExit.None, nav.Move(Vector2Int.up, isHolding: false));
            Assert.AreEqual(ContainerId.Storage, nav.Grid);
            Assert.AreEqual(new Vector2Int(6, 3), nav.Cell);
        }

        [Test]
        public void Rows_downFromStorage_entersOperatorAtProportionalColumn()
        {
            var nav = RowNavigator(ContainerId.Storage, col: 10, row: 3);
            nav.Move(Vector2Int.down, isHolding: true);
            Assert.AreEqual(Op2, nav.Grid);
            Assert.AreEqual(new Vector2Int(2, 0), nav.Cell);
        }

        [Test]
        public void Rows_outerEdges_areNoOps()
        {
            var top = RowNavigator(ContainerId.Storage, col: 4, row: 0);
            Assert.AreEqual(NavigationExit.None, top.Move(Vector2Int.up, isHolding: false));
            Assert.AreEqual(ContainerId.Storage, top.Grid);
            Assert.AreEqual(new Vector2Int(4, 0), top.Cell);

            var bottom = RowNavigator(Op0, col: 1, row: 3);
            bottom.Move(Vector2Int.down, isHolding: false);
            Assert.AreEqual(Op0, bottom.Grid);
            Assert.AreEqual(new Vector2Int(1, 3), bottom.Cell);
        }

        [Test]
        public void Rows_horizontalMovesStillFollowLinks()
        {
            var nav = RowNavigator(Op0, col: 3, row: 1);
            nav.Move(Vector2Int.right, isHolding: false);
            Assert.AreEqual(Op1, nav.Grid);
            Assert.AreEqual(new Vector2Int(0, 1), nav.Cell);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

`ut.sh CrimsonDraft.Tests.GridNavigatorTests`. Expected: compile error CS1729 (constructor with 4 arguments).

- [ ] **Step 3: Implement** — in `GridNavigator`:
- add field `private readonly IReadOnlyList<IReadOnlyList<ContainerId>>? rows;`
- extend the constructor with the optional `rows` parameter and assign it;
- in `Move`, replace the vertical block (from `if (next.y < 0)` to the end of the `else if (next.y >= container.Height)` block) with:

```csharp
            if (next.y < 0 || next.y >= container.Height)
            {
                bool up = next.y < 0;
                if (TryLocateRow(this.Grid, out int rowIndex, out int position))
                {
                    int target = up ? rowIndex - 1 : rowIndex + 1;
                    if (target >= 0 && target < this.rows!.Count)
                        EnterRow(target, rowIndex, position, this.Cell.x, container.Width, enterFromBelow: up);
                    return NavigationExit.None;
                }

                if (up)
                {
                    if (gridLinks.Up is { } upLink) { EnterVertical(upLink, next.x, atBottom: true); return NavigationExit.None; }
                    if (!isHolding) return NavigationExit.Up;
                    next.y = container.Height - 1;
                }
                else
                {
                    if (gridLinks.Down is { } downLink) { EnterVertical(downLink, next.x, atBottom: false); return NavigationExit.None; }
                    next.y = 0;
                }
            }
```

- add the helpers:

```csharp
        private bool TryLocateRow(ContainerId grid, out int rowIndex, out int position)
        {
            if (this.rows != null)
                for (rowIndex = 0; rowIndex < this.rows.Count; rowIndex++)
                {
                    position = IndexOf(this.rows[rowIndex], grid);
                    if (position >= 0) return true;
                }

            rowIndex = -1;
            position = -1;
            return false;
        }

        private void EnterRow(int targetRow, int fromRow, int fromPosition, int fromColumn, int fromWidth, bool enterFromBelow)
        {
            float fraction = (fromPosition + (fromColumn + 0.5f) / fromWidth) / this.rows![fromRow].Count;
            var   row      = this.rows[targetRow];
            float scaled   = fraction * row.Count;
            int   gridIdx  = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, row.Count - 1);
            var   target   = this.resolve(row[gridIdx]);
            int   column   = Mathf.Clamp(Mathf.FloorToInt((scaled - gridIdx) * target.Width), 0, target.Width - 1);

            this.Grid = row[gridIdx];
            this.Cell = new Vector2Int(column, enterFromBelow ? target.Height - 1 : 0);
        }

        private static int IndexOf(IReadOnlyList<ContainerId> row, ContainerId grid)
        {
            for (int i = 0; i < row.Count; i++)
                if (row[i] == grid) return i;
            return -1;
        }
```

- [ ] **Step 4: Run tests**

`ut.sh CrimsonDraft.Tests.GridNavigatorTests` then `ut.sh`. Expected: 16/16; no new failures.

---
### Task 5: World → UI event and `StorageBoxInteractable`

**Files:**
- Modify: `Scripts/Navigation/NavigationEvents.cs`
- Modify: `Scripts/Navigation/NavigationScope.cs` (broker registration next to the other `RegisterMessageBroker` calls, ~line 145)
- Modify: `Scripts/Navigation/Interactables/InteractionContext.cs`
- Modify: `Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`
- Create: `Scripts/Navigation/Interactables/StorageBoxInteractable.cs`
- Modify tests: `DoorInteractableTests.cs`, `RoomDoorInteractableTests.cs`, `SceneDoorInteractableTests.cs`, `MapPickupInteractableTests.cs` (add one `null!` argument)
- Test: `Tests/EditMode/StorageBoxInteractableTests.cs`

**Interfaces:**
- Produces: `CrimsonDraft.Navigation.StorageOpenRequestedEvent` (empty `readonly struct`); `InteractionContext.StorageOpenPublisher : IPublisher<StorageOpenRequestedEvent>` (last constructor parameter); `StorageBoxInteractable : MonoBehaviour, IInteractable`.

- [ ] **Step 1: Write the failing test** — `Tests/EditMode/StorageBoxInteractableTests.cs`:

```csharp
#nullable enable

using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class StorageBoxInteractableTests
    {
        private sealed class FakePublisher : IPublisher<StorageOpenRequestedEvent>
        {
            public int Count;
            public void Publish(StorageOpenRequestedEvent message) => this.Count++;
        }

        [Test]
        public void Interact_publishesStorageOpenRequested()
        {
            var go        = new GameObject("StorageBox");
            var box       = go.AddComponent<StorageBoxInteractable>();
            var publisher = new FakePublisher();
            var context   = new InteractionContext(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, publisher);

            box.Interact(context);

            Assert.AreEqual(1, publisher.Count);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`ut.sh CrimsonDraft.Tests.StorageBoxInteractableTests`. Expected: compile errors (`StorageOpenRequestedEvent`, `StorageBoxInteractable` missing).

- [ ] **Step 3: Implement**

`NavigationEvents.cs` — append inside the namespace:

```csharp
    public readonly struct StorageOpenRequestedEvent
    {
    }
```

`NavigationScope.cs` — after `builder.RegisterMessageBroker<DialogueActiveChangedEvent>(msgOptions);` add:

```csharp
            builder.RegisterMessageBroker<StorageOpenRequestedEvent>(msgOptions);
```

`InteractionContext.cs` — add `using MessagePipe;`, a field `public readonly IPublisher<StorageOpenRequestedEvent> StorageOpenPublisher;` after `InspectionController`, a last constructor parameter `IPublisher<StorageOpenRequestedEvent> storageOpenPublisher`, and the assignment `StorageOpenPublisher    = storageOpenPublisher;`.

`PlayerInteractionCaster.cs` — add a field `private IPublisher<StorageOpenRequestedEvent> storageOpenPublisher = null!;`, a `Construct` parameter `IPublisher<StorageOpenRequestedEvent> storageOpenPublisher` placed **before** `ISubscriber<DialogueActiveChangedEvent> dialogueActiveSubscriber`, its assignment, and pass `this.storageOpenPublisher` as the last argument of `new InteractionContext(...)`.

`StorageBoxInteractable.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class StorageBoxInteractable : MonoBehaviour, IInteractable
    {
        public void Interact(InteractionContext context) =>
            context.StorageOpenPublisher.Publish(new StorageOpenRequestedEvent());
    }
}
```

Tests — in each of `DoorInteractableTests.cs`, `RoomDoorInteractableTests.cs`, `SceneDoorInteractableTests.cs`, `MapPickupInteractableTests.cs`, append `, null!` as the last argument of the `new InteractionContext(...)` / `new(...)` call inside `MakeContext`.

- [ ] **Step 4: Run tests**

`ut.sh CrimsonDraft.Tests.StorageBoxInteractableTests CrimsonDraft.Tests.DoorInteractableTests CrimsonDraft.Tests.RoomDoorInteractableTests CrimsonDraft.Tests.SceneDoorInteractableTests CrimsonDraft.Tests.MapPickupInteractableTests` then `ut.sh`. Expected: all green, no new failures.

---

### Task 6: Storage mode in the UI code

UI classes live in `CrimsonDraft.UI.Prototype`, which the test assembly does not reference: verification is compile + full suite here, Play Mode in Task 8.

**Files:**
- Create: `Scripts/UI/Inventory/StorageWindow.cs`
- Modify: `Scripts/UI/Inventory/InventoryGridGroup.cs`
- Modify: `Scripts/UI/Inventory/InventoryPresenters.cs`
- Modify: `Scripts/UI/Inventory/GridCursor.cs`
- Modify: `Scripts/UI/Inventory/InventoryOpenCloseController.cs`

**Interfaces:**
- Consumes: `GridNavigator` rows (Task 4), `ContainerId.Storage` (Task 1), `StorageOpenRequestedEvent` (Task 5).
- Produces: `StorageWindow` (`Grid`, `IsShown`, `Show()`, `Hide()`); `InventoryGridGroup.StorageWindow`; `InventoryPresenters.HasStorage`, `BuildStorageRows()`; `GridCursor.IsTransferMode`, `EnterTransferMode()`, `ExitTransferMode()`; `InventoryOpenCloseController.OpenStorage()`, serialized `tabBarRoot` and `storageWindow`.

- [ ] **Step 1: `StorageWindow.cs`**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    public sealed class StorageWindow : MonoBehaviour
    {
        [SerializeField] private InventoryGrid grid = null!;

        public InventoryGrid Grid    => this.grid;
        public bool          IsShown => this.gameObject.activeSelf;

        public void Show() => this.gameObject.SetActive(true);
        public void Hide() => this.gameObject.SetActive(false);
    }
}
```

- [ ] **Step 2: `InventoryGridGroup`** — add under `itemViewPrefab`:

```csharp
        [SerializeField] private StorageWindow storageWindow;

        public StorageWindow StorageWindow => storageWindow;
```

- [ ] **Step 3: `InventoryPresenters`** — at the end of `Initialize()` add:

```csharp
            var storageWindow = this.gridGroup.StorageWindow;
            if (storageWindow != null)
            {
                var storagePresenter = new ContainerGridPresenter(
                    this.inventory.GetContainer(ContainerId.Storage), storageWindow.Grid, this.gridGroup.ItemViewPrefab);
                storagePresenter.Rendered += () => this.Rendered?.Invoke(ContainerId.Storage);
                this.presenters[ContainerId.Storage] = storagePresenter;
            }
```

and add the members:

```csharp
        public bool HasStorage => this.presenters.ContainsKey(ContainerId.Storage);

        public IReadOnlyList<IReadOnlyList<ContainerId>> BuildStorageRows() =>
            new List<IReadOnlyList<ContainerId>>
            {
                new[] { ContainerId.Storage },
                this.order.ToArray(),
            };
```

(`order` stays operators-only, so `FirstGrid` is unchanged.)

- [ ] **Step 4: `GridCursor`**

Add the field `private bool transferMode;` next to `onMeleeSlot`, and the public members after `CurrentCell`:

```csharp
        public bool IsTransferMode => this.transferMode;

        public void EnterTransferMode()
        {
            if (!this.presenters.HasStorage)
                throw new System.InvalidOperationException("GridCursor: no StorageWindow is wired into the InventoryGridGroup.");

            this.transferMode  = true;
            this.isCombineMode = false;
            this.onMeleeSlot   = false;
            this.navigator     = new GridNavigator(
                this.inventory.GetContainer, this.presenters.BuildLinks(), ContainerId.Storage, this.presenters.BuildStorageRows());
            this.navigator.Reset(ContainerId.Storage, Vector2Int.zero);
            this.selector.SetVisible(true);
            Refresh();
        }

        public void ExitTransferMode()
        {
            this.transferMode = false;
            this.navigator    = null;
        }
```

Extract the non-holding branch of `OnPickup` into a method and call it from `OnPickup`:

```csharp
        private void PickUpAtCursor()
        {
            var container = CurrentPresenter.Container;
            var item      = container.GetItemAt(Navigator.Cell);
            if (item == null) return;

            var origin = container.GetPlacement(item)!.Origin;
            if (!this.inventory.TryPickUp(item))
            {
                this.sfx.PlayCancel(this.gameObject);
                return;
            }

            Navigator.Reset(Navigator.Grid, origin);
            this.sfx.PlayDecide(this.gameObject);
            Refresh();
        }
```

(`OnPickup` keeps its guards and the `IsHoldingItem → RotateHeld` branch, then calls `PickUpAtCursor();`.)

In `OnConfirm`, right after the `inspectPanel` guard, insert:

```csharp
            if (this.transferMode)
            {
                if (this.IsHoldingItem) Drop();
                else                    PickUpAtCursor();
                return;
            }
```

In `TryMove`, change the exit handling to:

```csharp
            if (Navigator.Move(dir, this.IsHoldingItem) == NavigationExit.Up)
            {
                if (this.transferMode) return;
                if (!TryEnterMeleeSlot()) this.tabManager?.EnterTabBar();
                return;
            }
```

At the very top of `TryConsumeCancel`, insert:

```csharp
            if (this.transferMode)
            {
                if (this.IsHoldingItem) Drop();
                else                    RequestClose();
                return true;
            }
```

- [ ] **Step 5: `InventoryOpenCloseController`**

Add `using System;`, `using MessagePipe;`, `using CrimsonDraft.Navigation;` (if not present), and the members:

```csharp
        [SerializeField] private GameObject?    tabBarRoot;
        [SerializeField] private StorageWindow? storageWindow;

        [Inject] private ISubscriber<StorageOpenRequestedEvent> storageOpenSubscriber = null!;

        private IDisposable? storageSubscription;
        private bool         storageMode;
```

In `Initialize()` add `this.storageSubscription = this.storageOpenSubscriber.Subscribe(_ => OpenStorage());`; in `Dispose()` add `this.storageSubscription?.Dispose();`.

Add:

```csharp
        public void OpenStorage()
        {
            if (this.canvasRoot.activeSelf || this.storageWindow == null) return;

            Open();
            this.tabManager.ActivateTab(0);
            if (this.tabBarRoot != null) this.tabBarRoot.SetActive(false);
            this.storageWindow.Show();
            this.cursor.EnterTransferMode();
            this.storageMode = true;
        }
```

In `Close()`, right after `this.cursor.CancelAll();`, add:

```csharp
            if (this.storageMode)
            {
                this.storageMode = false;
                this.cursor.ExitTransferMode();
                if (this.storageWindow != null) this.storageWindow.Hide();
                if (this.tabBarRoot != null) this.tabBarRoot.SetActive(true);
            }
```

- [ ] **Step 6: Compile + full suite**

`ut.sh`. Expected: compiles, no new failures.

---

### Task 7: Prefabs — storage window, wiring, box prefab

**Files (via Editor):**
- `Prefabs/UI/Inventory/UI_Inventry_Root.prefab` — add `StorageWindow`, wire `InventoryGridGroup.storageWindow`
- `Prefabs/Core/UI_Canvas.prefab` — wire `InventoryOpenCloseController.storageWindow` / `tabBarRoot`
- Create `Prefabs/Interactables/StorageBox.prefab`

- [ ] **Step 1: Build the storage window** — snippet `build_storage_window.cs`:

```csharp
const string rootPath = "Assets/Prefabs/UI/Inventory/UI_Inventry_Root.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(rootPath);
try
{
    var inventory = root.transform.Find("Visual/Tabs/INVENTORY/Boundings/Border/Inventory");
    if (inventory.Find("StorageWindow") != null) return "StorageWindow already exists";
    var group   = inventory.GetComponent<CrimsonDraft.UI.InventoryGridGroup>();
    var opGrid  = group.GetGrid(0);
    float cell  = ((UnityEngine.RectTransform)opGrid.transform).rect.width / opGrid.Columns;
    var border  = root.transform.Find("Visual/Tabs/INVENTORY/Boundings/Border").GetComponent<UnityEngine.UI.Image>();
    var invRt   = (UnityEngine.RectTransform)inventory;
    float height = invRt.rect.height * 0.5f;

    var win   = new UnityEngine.GameObject("StorageWindow", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
    var winRt = (UnityEngine.RectTransform)win.transform;
    winRt.SetParent(inventory, false);
    win.transform.SetSiblingIndex(inventory.Find("Card_Container").GetSiblingIndex() + 1);
    winRt.anchorMin = new UnityEngine.Vector2(0f, 1f);
    winRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
    winRt.pivot     = new UnityEngine.Vector2(0.5f, 1f);
    winRt.sizeDelta = new UnityEngine.Vector2(0f, height);
    winRt.anchoredPosition = UnityEngine.Vector2.zero;
    win.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0f, 0f, 0f, 1f);

    var frame   = new UnityEngine.GameObject("Frame", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
    var frameRt = (UnityEngine.RectTransform)frame.transform;
    frameRt.SetParent(winRt, false);
    frameRt.anchorMin = UnityEngine.Vector2.zero; frameRt.anchorMax = UnityEngine.Vector2.one;
    frameRt.offsetMin = UnityEngine.Vector2.zero; frameRt.offsetMax = UnityEngine.Vector2.zero;
    var frameImg = frame.GetComponent<UnityEngine.UI.Image>();
    frameImg.sprite = border.sprite; frameImg.type = border.type; frameImg.color = border.color;
    frameImg.pixelsPerUnitMultiplier = border.pixelsPerUnitMultiplier;

    var fontSource = root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
    var title      = new UnityEngine.GameObject("Title", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
    var titleRt    = (UnityEngine.RectTransform)title.transform;
    titleRt.SetParent(winRt, false);
    titleRt.anchorMin = new UnityEngine.Vector2(0f, 1f); titleRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
    titleRt.pivot     = new UnityEngine.Vector2(0.5f, 1f);
    titleRt.sizeDelta = new UnityEngine.Vector2(0f, cell * 0.6f);
    titleRt.anchoredPosition = new UnityEngine.Vector2(0f, -4f);
    var tmp = title.GetComponent<TMPro.TextMeshProUGUI>();
    tmp.text = "BAÚL"; tmp.alignment = TMPro.TextAlignmentOptions.Center;
    if (fontSource != null) { tmp.font = fontSource.font; tmp.fontSize = fontSource.fontSize; tmp.color = fontSource.color; }

    var gridGo = new UnityEngine.GameObject("StorageGrid", typeof(UnityEngine.RectTransform));
    var gridRt = (UnityEngine.RectTransform)gridGo.transform;
    gridRt.SetParent(winRt, false);
    gridRt.anchorMin = gridRt.anchorMax = new UnityEngine.Vector2(0.5f, 0f);
    gridRt.pivot     = new UnityEngine.Vector2(0.5f, 0f);
    gridRt.sizeDelta = new UnityEngine.Vector2(cell * 12f, cell * 4f);
    gridRt.anchoredPosition = new UnityEngine.Vector2(0f, 6f);
    var storageGrid = gridGo.AddComponent<CrimsonDraft.UI.InventoryGrid>();

    var opGridSo = new UnityEditor.SerializedObject(opGrid);
    var opBg     = opGridSo.FindProperty("gridBackground").objectReferenceValue as UnityEngine.UI.Image;
    UnityEngine.UI.Image? bg = null;
    if (opBg != null)
    {
        var bgGo = new UnityEngine.GameObject("Background", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
        var bgRt = (UnityEngine.RectTransform)bgGo.transform;
        bgRt.SetParent(gridRt, false);
        bgRt.anchorMin = UnityEngine.Vector2.zero; bgRt.anchorMax = UnityEngine.Vector2.one;
        bgRt.offsetMin = UnityEngine.Vector2.zero; bgRt.offsetMax = UnityEngine.Vector2.zero;
        bg = bgGo.GetComponent<UnityEngine.UI.Image>();
        bg.sprite = opBg.sprite; bg.type = opBg.type; bg.color = opBg.color; bg.pixelsPerUnitMultiplier = opBg.pixelsPerUnitMultiplier;
    }

    var gridSo = new UnityEditor.SerializedObject(storageGrid);
    gridSo.FindProperty("columns").intValue             = 12;
    gridSo.FindProperty("rows").intValue                = 4;
    gridSo.FindProperty("containerKind").enumValueIndex = 1;
    gridSo.FindProperty("containerIndex").intValue      = 0;
    gridSo.FindProperty("gridBackground").objectReferenceValue = bg;
    gridSo.ApplyModifiedPropertiesWithoutUndo();

    var window   = win.AddComponent<CrimsonDraft.UI.StorageWindow>();
    var windowSo = new UnityEditor.SerializedObject(window);
    windowSo.FindProperty("grid").objectReferenceValue = storageGrid;
    windowSo.ApplyModifiedPropertiesWithoutUndo();
    win.SetActive(false);

    var groupSo = new UnityEditor.SerializedObject(group);
    groupSo.FindProperty("storageWindow").objectReferenceValue = window;
    groupSo.ApplyModifiedPropertiesWithoutUndo();

    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, rootPath);
    return $"cell={cell} window height={height} grid={cell * 12f}x{cell * 4f} background={(bg != null)} font={(fontSource != null)}";
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
```

Run: `unity command eval_file --file <path> --timeout 120 --no-banner --result-only`. Expected: `success: true` and a result string with `grid` height `≤` window height minus the title.

- [ ] **Step 2: Wire `InventoryOpenCloseController`** — snippet `wire_open_close.cs`:

```csharp
const string canvasPath = "Assets/Prefabs/Core/UI_Canvas.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(canvasPath);
try
{
    var openClose = root.GetComponentInChildren<CrimsonDraft.UI.InventoryOpenCloseController>(true);
    var window    = root.GetComponentInChildren<CrimsonDraft.UI.StorageWindow>(true);
    UnityEngine.GameObject? tabBar = null;
    foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))
        if (t.name == "Windows" && t.GetComponent<UnityEngine.UI.GridLayoutGroup>() != null) { tabBar = t.gameObject; break; }

    var so = new UnityEditor.SerializedObject(openClose);
    so.FindProperty("storageWindow").objectReferenceValue = window;
    so.FindProperty("tabBarRoot").objectReferenceValue    = tabBar;
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, canvasPath);
    return $"window={(window != null)} tabBar={(tabBar != null)}";
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
```

Expected: `window=True tabBar=True`.

- [ ] **Step 3: Create the box prefab** — snippet `create_storage_box.cs`. It copies an existing interactable prefab (so layer, tag and collider match what `PlayerInteractionCaster` detects) without touching any scene:

```csharp
string source = null;
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var go   = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    if (go != null && go.GetComponentInChildren<CrimsonDraft.Navigation.Interactables.SavePointInteractable>(true) != null) { source = path; break; }
}
if (source == null) return "no SavePointInteractable prefab found";

const string target = "Assets/Prefabs/Interactables/StorageBox.prefab";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Prefabs/Interactables"))
    UnityEditor.AssetDatabase.CreateFolder("Assets/Prefabs", "Interactables");
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(target) != null) return "StorageBox.prefab already exists";
if (!UnityEditor.AssetDatabase.CopyAsset(source, target)) return "copy failed from " + source;

var root = UnityEditor.PrefabUtility.LoadPrefabContents(target);
try
{
    root.name = "StorageBox";
    var host = root.GetComponentInChildren<CrimsonDraft.Navigation.Interactables.SavePointInteractable>(true).gameObject;
    foreach (var c in root.GetComponentsInChildren<CrimsonDraft.Navigation.Interactables.SavePointInteractable>(true))
        UnityEngine.Object.DestroyImmediate(c, true);
    host.AddComponent<CrimsonDraft.Navigation.Interactables.StorageBoxInteractable>();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, target);
    return $"source={source} host={host.name} layer={UnityEngine.LayerMask.LayerToName(host.layer)}";
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
```

Expected: `success: true`, with the source prefab path reported. The prefab keeps the save point's model as a placeholder (the owner replaces the model).

- [ ] **Step 4: Verify assets**

```bash
cd "Game/CrimsonDraft/Assets"
git status --short -- Prefabs
```

Expected: ` M Prefabs/Core/UI_Canvas.prefab`, ` M Prefabs/UI/Inventory/UI_Inventry_Root.prefab`, `?? Prefabs/Interactables/…` (StorageBox + folder meta if new). No scene files listed. Then `ut.sh` — no new failures.

---

### Task 8: Play Mode verification

Enter Play Mode with `unity command editor_play` (wait until `editor_status` reports `playing`, then ~8 s for scope build), run each probe with `eval_file`, exit with `editor_stop`, and confirm the active scene is not dirty. Restore the TMP fallback font asset afterwards if modified.

- [ ] **Step 1: Open via the event** — `storage_open.cs`:

```csharp
var scope = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.Navigation.NavigationScope>();
var pub   = (MessagePipe.IPublisher<CrimsonDraft.Navigation.StorageOpenRequestedEvent>)scope.Container.Resolve(typeof(MessagePipe.IPublisher<CrimsonDraft.Navigation.StorageOpenRequestedEvent>));
pub.Publish(new CrimsonDraft.Navigation.StorageOpenRequestedEvent());

var flags  = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var cursor = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.GridCursor>(UnityEngine.FindObjectsInactive.Include);
var nav    = (CrimsonDraft.Inventory.GridNavigator)typeof(CrimsonDraft.UI.GridCursor).GetProperty("Navigator", flags).GetValue(cursor);
var window = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.StorageWindow>(UnityEngine.FindObjectsInactive.Include);
var oc     = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.InventoryOpenCloseController>(UnityEngine.FindObjectsInactive.Include);
var tabBar = (UnityEngine.GameObject)typeof(CrimsonDraft.UI.InventoryOpenCloseController).GetField("tabBarRoot", flags).GetValue(oc);
return $"windowShown={window.IsShown} tabBarActive={tabBar.activeSelf} transfer={cursor.IsTransferMode} grid={nav.Grid} cell={nav.Cell}";
```

Expected: `windowShown=True tabBarActive=False transfer=True grid=Storage[0] cell=(0, 0)`.

- [ ] **Step 2: Store, retrieve, close while holding** — `storage_move.cs` (run while storage mode is open from Step 1):

```csharp
var flags   = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var cursor  = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.GridCursor>(UnityEngine.FindObjectsInactive.Include);
var t       = typeof(CrimsonDraft.UI.GridCursor);
var inv     = (CrimsonDraft.Inventory.IInventoryService)t.GetField("inventory", flags).GetValue(cursor);
var tryMove = t.GetMethod("TryMove", flags);
var confirm = t.GetMethod("OnConfirm", flags);
object ctx  = default(UnityEngine.InputSystem.InputAction.CallbackContext);
var oc      = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.InventoryOpenCloseController>(UnityEngine.FindObjectsInactive.Include);
System.Func<string> state = () =>
{
    var box = inv.GetContainer(CrimsonDraft.Inventory.ContainerId.Storage);
    var op0 = inv.GetContainer(CrimsonDraft.Inventory.ContainerId.Operator(0));
    return $"box={box.Count} op0={op0.Count} held={(inv.Held != null ? inv.Held.Data.ItemId : "none")}";
};
var sb = new System.Text.StringBuilder();
sb.AppendLine("start " + state());
for (int i = 0; i < 4; i++) tryMove.Invoke(cursor, new object[] { UnityEngine.Vector2Int.down });
confirm.Invoke(cursor, new[] { ctx });
sb.AppendLine("picked from op0 " + state());
tryMove.Invoke(cursor, new object[] { UnityEngine.Vector2Int.up });
confirm.Invoke(cursor, new[] { ctx });
sb.AppendLine("dropped in box " + state());
confirm.Invoke(cursor, new[] { ctx });
sb.AppendLine("picked from box " + state());
oc.Close();
sb.AppendLine("closed while holding " + state());
return sb.ToString();
```

Expected lines (cursor goes Storage (0,0) → down ×4 → Operator[0] (0,0) → up → Storage (0,3)): start `box=0 op0=2`; after pick `op0=1 held=<id>`; after drop `box=1 held=none`; after pick from box `box=0 held=<id>`; after close `box=1 held=none` (returned to the box).

- [ ] **Step 3: Normal inventory after a storage session** — `storage_normal_after.cs`:

```csharp
var flags  = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var oc     = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.InventoryOpenCloseController>(UnityEngine.FindObjectsInactive.Include);
var cursor = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.GridCursor>(UnityEngine.FindObjectsInactive.Include);
var window = UnityEngine.Object.FindFirstObjectByType<CrimsonDraft.UI.StorageWindow>(UnityEngine.FindObjectsInactive.Include);
var tabBar = (UnityEngine.GameObject)typeof(CrimsonDraft.UI.InventoryOpenCloseController).GetField("tabBarRoot", flags).GetValue(oc);
oc.Open();
var nav    = (CrimsonDraft.Inventory.GridNavigator)typeof(CrimsonDraft.UI.GridCursor).GetProperty("Navigator", flags).GetValue(cursor);
var result = $"windowShown={window.IsShown} tabBarActive={tabBar.activeSelf} transfer={cursor.IsTransferMode} grid={nav.Grid}";
oc.Close();
return result;
```

Expected: `windowShown=False tabBarActive=True transfer=False grid=Operator[0]`.

- [ ] **Step 4: Console** — `unity command console --level error --tail 20`: no exceptions other than the pre-existing `MusicManagerController.Start` / `WeatherAmbienceController.Start` NullReferenceExceptions.

- [ ] **Step 5: Exit and clean** — `editor_stop`; active scene `dirty=False`; restore the TMP fallback font asset if modified; `Assets/Temp` must not exist.

---

### Task 9: Final verification and commit

- [ ] **Step 1:** `ut.sh` — full suite, no new failures; record the totals.
- [ ] **Step 2:** Clean rebuild warnings (request `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)` via `unity command eval`, then `unity recompile`, then `unity command console --level warning`): no warnings in files touched by this plan.
- [ ] **Step 3: Manual checklist for the owner (real input):**
  - [ ] Place `Prefabs/Interactables/StorageBox.prefab` in a room; interact → storage mode opens.
  - [ ] Navigate up/down between the box and every operator grid; left/right inside the box and across operators.
  - [ ] Store and retrieve items; rotate while holding; swap box↔operator.
  - [ ] Cancel with empty hand closes; close key (Z) and map key (A) close; held item returns where it came from.
  - [ ] Normal inventory key: no box window, tabs work, context menu works.
  - [ ] A key stored in the box does not open its door; pickups with full operators are refused (not sent to the box).
  - [ ] Save → quit → load: box contents restored; scene door transition: box contents kept.
- [ ] **Step 4: Commit** (single commit, explicit paths only):

```bash
cd "<repo root>"
git add -A -- Game/CrimsonDraft/Assets/Scripts Game/CrimsonDraft/Assets/Tests Game/CrimsonDraft/Assets/Prefabs docs/superpowers/specs/2026-10-04-storage-box-design.md docs/superpowers/plans/2026-10-04-storage-box.md
git diff --cached --name-only | grep -vE "^(Game/CrimsonDraft/Assets/(Scripts|Tests|Prefabs)/|docs/superpowers/)" || echo "only intended paths staged"
git commit -m "feat(inventory): add shared RE-style storage box"
```
