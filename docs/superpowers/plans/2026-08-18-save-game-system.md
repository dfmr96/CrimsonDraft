# Save Game System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Resident-Evil-classic-style save point interactable that writes the full game state (doors, visited rooms, pickups, notes, known maps, defeated enemies, inventory, operator HP, player position/room/scene) to disk across 20 slots, and a matching Load flow wired into the already-present but disabled "Load Game" button on the main menu.

**Architecture:** Extend the project's existing in-memory registry layer (`DoorStateRegistry`, `RoomStateRegistry`, `PickupRegistry`, `NoteRegistry`, `KnownMapsRegistry`, `EnemyStateRegistry`, plus `InventoryStateRegistry`/`RosterHealthRegistry`) with `ClearAll()`/`LoadState()` where missing, bridge them to disk through a `NavigationScope`-scoped `SaveController` (save) and `SaveGameLoader` (load-on-scene-entry), with a root-singleton `SaveGameService` doing pure JSON I/O. No new third-party dependencies — `JsonUtility` only.

**Tech Stack:** C#, Unity `JsonUtility`, VContainer (DI), NaughtyAttributes (`[Button]`), TextMeshPro/uGUI for the slot-list UI, NUnit EditMode tests with the project's plain-fake pattern.

**Spec:** `docs/superpowers/specs/2026-08-18-save-game-system-design.md`

## Global Constraints

- Free save, no consumable cost (per spec).
- 20 save slots, JSON files at `{Application.persistentDataPath}/Saves/slot_00.json`..`slot_19.json`.
- No new NuGet/UPM dependencies — use `JsonUtility`, not Newtonsoft.
- `#nullable enable` in every new file; serialized fields use `null!`; injected fields start `null?`/are set in `[Inject] Construct(...)`.
- `[Preserve]` on every constructor of a VContainer-registered pure C# class (matches existing registries/services).
- Tests are EditMode only, run via Unity Test Runner or the MCP `run_tests` tool (no CLI test command exists in this repo) — filter by class name.
- Follow the existing `XInteractable` (thin MonoBehaviour) → `XController` (VContainer `IInitializable`/`IDisposable` service) → `XView` (pure presentation MonoBehaviour) pattern used by `ContainerInteractable`/`ContainerController`/`ContainerView`.
- `SaveGameLoader` must be registered in `NavigationScope.Configure()` **after** `RoomOrchestrator` and **before** `DoorBootstrap`/`PickupBootstrap`/`MapPickupBootstrap`/`DocumentPickupBootstrap` — VContainer runs `IInitializable.Initialize()` in registration order, and the registries must hold the loaded state before those bootstraps read them, while `RoomOrchestrator.CurrentRoom` must already be set (to whatever the default starting room is) before `SaveGameLoader` overrides it.

---

### Task 1: Registry `ClearAll()`/`LoadState()` additions + `WorldStateRegistries` bundle

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/PickupRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/NoteRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/EnemyStateRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/RoomStateRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/KnownMapsRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/InventoryStateRegistry.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/RosterHealthRegistry.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/PickupRegistryTests.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/NoteRegistryTests.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyStateRegistryTests.cs`
- Modify test: `Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs`
- Modify test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomStateRegistryTests.cs`
- Modify test: `Game/CrimsonDraft/Assets/Tests/EditMode/KnownMapsRegistryTests.cs`
- Modify test: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryStateRegistryTests.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/RosterHealthRegistryTests.cs`

**Interfaces:**
- Produces: `PickupRegistry.LoadState(IEnumerable<string>)`, `PickupRegistry.ClearAll()`; same shape on `NoteRegistry`, `EnemyStateRegistry` (using its existing `defeated` set — no rename of `GetDefeated()`); `ClearAll()` on `DoorStateRegistry`, `RoomStateRegistry`, `KnownMapsRegistry`, `InventoryStateRegistry`, `RosterHealthRegistry`. `WorldStateRegistries` — public readonly fields `Doors`, `Rooms`, `Pickups`, `Notes`, `KnownMaps`, `Enemies`, constructor taking the six registries.

- [ ] **Step 1: Write failing tests for the new registry methods**

`Game/CrimsonDraft/Assets/Tests/EditMode/PickupRegistryTests.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class PickupRegistryTests
    {
        [Test]
        public void LoadState_marksGivenIdsAsCollected()
        {
            var registry = new PickupRegistry();
            registry.LoadState(new List<string> { "a", "b" });

            Assert.IsTrue(registry.IsCollected("a"));
            Assert.IsTrue(registry.IsCollected("b"));
            Assert.IsFalse(registry.IsCollected("c"));
        }

        [Test]
        public void LoadState_replacesPreviousState()
        {
            var registry = new PickupRegistry();
            registry.SetCollected("old");
            registry.LoadState(new List<string> { "new" });

            Assert.IsFalse(registry.IsCollected("old"));
            Assert.IsTrue(registry.IsCollected("new"));
        }

        [Test]
        public void ClearAll_removesAllCollectedIds()
        {
            var registry = new PickupRegistry();
            registry.SetCollected("a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsCollected("a"));
            Assert.AreEqual(0, registry.CollectedIds.Count);
        }
    }
}
```

`Game/CrimsonDraft/Assets/Tests/EditMode/NoteRegistryTests.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class NoteRegistryTests
    {
        [Test]
        public void LoadState_marksGivenIdsAsCollected()
        {
            var registry = new NoteRegistry();
            registry.LoadState(new List<string> { "note-a" });

            Assert.IsTrue(registry.IsCollected("note-a"));
        }

        [Test]
        public void ClearAll_removesAllCollectedIds()
        {
            var registry = new NoteRegistry();
            registry.SetCollected("note-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsCollected("note-a"));
        }
    }
}
```

`Game/CrimsonDraft/Assets/Tests/EditMode/EnemyStateRegistryTests.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyStateRegistryTests
    {
        [Test]
        public void LoadState_marksGivenKeysAsDefeated()
        {
            var registry = new EnemyStateRegistry();
            registry.LoadState(new List<string> { "enemy-a" });

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
        }

        [Test]
        public void ClearAll_removesAllDefeated()
        {
            var registry = new EnemyStateRegistry();
            registry.SetDefeated("enemy-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsDefeated("enemy-a"));
        }
    }
}
```

Add to `DoorStateRegistryTests.cs`:
```csharp
        [Test]
        public void ClearAll_removesAllDoorState()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            registry.ClearAll();

            Assert.AreEqual(DoorMapState.Unknown, registry.GetMapState("door-a"));
        }
```

Add to `RoomStateRegistryTests.cs`:
```csharp
        [Test]
        public void ClearAll_removesAllRoomState()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("room-a");
            registry.ClearAll();

            Assert.AreEqual(RoomMapState.Unknown, registry.GetState("room-a"));
        }
```

Add to `KnownMapsRegistryTests.cs`:
```csharp
        [Test]
        public void ClearAll_removesAllKnownMaps()
        {
            var registry = new KnownMapsRegistry();
            registry.MarkKnown("map-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsKnown("map-a"));
        }
```

Add to `InventoryStateRegistryTests.cs`:
```csharp
        [Test]
        public void ClearAll_removesSavedState()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(new object());
            registry.ClearAll();

            Assert.IsFalse(registry.HasSavedState);
        }
```

`Game/CrimsonDraft/Assets/Tests/EditMode/RosterHealthRegistryTests.cs`:
```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RosterHealthRegistryTests
    {
        [Test]
        public void Save_thenLoad_returnsSavedArray()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 50, 80 });

            CollectionAssert.AreEqual(new[] { 50, 80 }, registry.Load());
        }

        [Test]
        public void ClearAll_removesSavedState()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 50 });
            registry.ClearAll();

            Assert.IsFalse(registry.HasSavedState);
            Assert.IsNull(registry.Load());
        }
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `PickupRegistryTests`, `NoteRegistryTests`, `EnemyStateRegistryTests`, `DoorStateRegistryTests`, `RoomStateRegistryTests`, `KnownMapsRegistryTests`, `InventoryStateRegistryTests`, `RosterHealthRegistryTests`.
Expected: compile error / FAIL — `LoadState`/`ClearAll` don't exist yet on these types.

- [ ] **Step 3: Add the methods to each registry**

`PickupRegistry.cs` — add inside the class:
```csharp
        public void LoadState(IEnumerable<string> saved)
        {
            this.collected.Clear();
            foreach (var id in saved)
                this.collected.Add(id);
        }

        public void ClearAll() => this.collected.Clear();
```

`NoteRegistry.cs` — same two methods, `saved`/`collected` (already named `collected` in this file too).

`EnemyStateRegistry.cs` — add:
```csharp
        public void LoadState(IEnumerable<string> saved)
        {
            this.defeated.Clear();
            foreach (var key in saved)
                this.defeated.Add(key);
        }

        public void ClearAll() => this.defeated.Clear();
```

`DoorStateRegistry.cs` — add:
```csharp
        public void ClearAll() => this.state.Clear();
```

`RoomStateRegistry.cs` — add:
```csharp
        public void ClearAll() => this.state.Clear();
```

`KnownMapsRegistry.cs` — add:
```csharp
        public void ClearAll() => this.knownMaps.Clear();
```

`InventoryStateRegistry.cs` — add:
```csharp
        public void ClearAll() => this.savedState = null;
```

`RosterHealthRegistry.cs` — add:
```csharp
        public void ClearAll() => this.savedHp = null;
```

- [ ] **Step 4: Create `WorldStateRegistries`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs`:
```csharp
#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    /// <summary>
    /// Bundles the six cross-scene world-state registries that Save/Load and New-Game-reset
    /// all need together, so consumers don't carry six separate constructor parameters.
    /// </summary>
    public sealed class WorldStateRegistries
    {
        public readonly DoorStateRegistry  Doors;
        public readonly RoomStateRegistry  Rooms;
        public readonly PickupRegistry     Pickups;
        public readonly NoteRegistry       Notes;
        public readonly KnownMapsRegistry  KnownMaps;
        public readonly EnemyStateRegistry Enemies;

        [Preserve]
        public WorldStateRegistries(
            DoorStateRegistry  doors,
            RoomStateRegistry  rooms,
            PickupRegistry     pickups,
            NoteRegistry       notes,
            KnownMapsRegistry  knownMaps,
            EnemyStateRegistry enemies)
        {
            Doors     = doors;
            Rooms     = rooms;
            Pickups   = pickups;
            Notes     = notes;
            KnownMaps = knownMaps;
            Enemies   = enemies;
        }
    }
}
```

- [ ] **Step 5: Run the tests again to verify they pass**

Run via Unity Test Runner (or MCP `run_tests`), same filter as Step 2.
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/PickupRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/NoteRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/EnemyStateRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/RoomStateRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/KnownMapsRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/InventoryStateRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/RosterHealthRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs Game/CrimsonDraft/Assets/Tests/EditMode/PickupRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/NoteRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/EnemyStateRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/RoomStateRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/KnownMapsRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/InventoryStateRegistryTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/RosterHealthRegistryTests.cs
git commit -m "feat(save): add ClearAll/LoadState to state registries and WorldStateRegistries bundle"
```

---

### Task 2: `SaveGameData` DTOs and `SaveSlotSummary`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveSlotSummary.cs`

**Interfaces:**
- Consumes: `DoorMapState`, `RoomMapState` (from `CrimsonDraft.Infrastructure`, Task 1's file).
- Produces: `SaveGameData`, `DoorStateEntry`, `RoomStateEntry`, `InventorySlotEntry`, `SaveSlotSummary` — used by every later task (`SaveGameService`, `SaveController`, `SaveGameLoader`).

No dedicated unit test — this is a pure data-holder task; its serialization round-trip is verified by `SaveGameServiceTests` in Task 4.

- [ ] **Step 1: Create the DTO file**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save
{
    [Serializable]
    public sealed class DoorStateEntry
    {
        public string       doorId = "";
        public DoorMapState state;
    }

    [Serializable]
    public sealed class RoomStateEntry
    {
        public string       roomId = "";
        public RoomMapState state;
    }

    [Serializable]
    public sealed class InventorySlotEntry
    {
        public int    slotIndex;
        public string itemId = "";
        public int    slotQuantity;
        public int    ammoBoxQuantity      = -1; // AmmoBoxItem.Quantity; -1 = not an ammo box
        public int    weaponAmmo           = -1; // WeaponItem.CurrentAmmo; -1 = not a weapon
        public int    keyUsesRemaining     = -1; // KeyItem.UsesRemaining; -1 = not a key item
        public bool   isExamined;
        public int    gridCol              = -1;
        public int    gridRow              = -1;
        public int    gridRotation;
        public int    equippedOperatorSlot = -1;
        public int    equippedWeaponSlot   = -1;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public string sceneName    = "";
        public string roomId       = "";
        public string timestampIso = "";
        public float  playtimeSeconds;

        public Vector3    playerPosition;
        public Quaternion playerRotation = Quaternion.identity;

        public List<DoorStateEntry>       doors              = new List<DoorStateEntry>();
        public List<RoomStateEntry>       rooms              = new List<RoomStateEntry>();
        public List<string>               collectedPickupIds = new List<string>();
        public List<string>               readNoteIds        = new List<string>();
        public List<string>               knownMapIds        = new List<string>();
        public List<string>               defeatedEnemyIds   = new List<string>();
        public List<InventorySlotEntry>   inventorySlots     = new List<InventorySlotEntry>();
        public int[]                      operatorHp         = Array.Empty<int>();
    }
}
```

- [ ] **Step 2: Create `SaveSlotSummary`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveSlotSummary.cs`:
```csharp
#nullable enable

using System;

namespace CrimsonDraft.Infrastructure.Save
{
    [Serializable]
    public struct SaveSlotSummary
    {
        public int    slot;
        public bool   isEmpty;
        public string roomId;
        public string timestampIso;
        public float  playtimeSeconds;
    }
}
```

- [ ] **Step 3: Verify the project compiles**

Open Unity, wait for domain reload, check `read_console` (MCP) or the Console window for compile errors.
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveSlotSummary.cs
git commit -m "feat(save): add SaveGameData DTOs and SaveSlotSummary"
```

---

### Task 3: `ItemDatabase`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemDatabase.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ItemDatabaseTests.cs`

**Interfaces:**
- Consumes: `ItemData.ItemId` (`Game/CrimsonDraft/Assets/Scripts/Inventory/ItemData.cs`).
- Produces: `ItemDatabase.TryGetById(string itemId, out ItemData item) : bool` — used by `SaveGameLoader` (Task 11) to rehydrate inventory slots.

- [ ] **Step 1: Write the failing test**

`Game/CrimsonDraft/Assets/Tests/EditMode/ItemDatabaseTests.cs`:
```csharp
#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ItemDatabaseTests
    {
        private static ConsumableData MakeConsumableData(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = "Test Consumable";
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemDatabase MakeDatabase(params ItemData[] items)
        {
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            var so = new SerializedObject(db);
            var arr = so.FindProperty("allItems");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        [Test]
        public void TryGetById_returnsTrueAndItem_whenIdExists()
        {
            var item = MakeConsumableData("herb-green");
            var db   = MakeDatabase(item);

            bool found = db.TryGetById("herb-green", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(item, result);
        }

        [Test]
        public void TryGetById_returnsFalse_whenIdMissing()
        {
            var db = MakeDatabase(MakeConsumableData("herb-green"));

            bool found = db.TryGetById("missing", out _);

            Assert.IsFalse(found);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `ItemDatabaseTests`.
Expected: compile error — `ItemDatabase` doesn't exist yet.

- [ ] **Step 3: Create `ItemDatabase`**

`Game/CrimsonDraft/Assets/Scripts/Inventory/ItemDatabase.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "CrimsonDraft/Inventory/Item Database")]
    public sealed class ItemDatabase : ScriptableObject
    {
        [SerializeField] private ItemData[] allItems = Array.Empty<ItemData>();

        private Dictionary<string, ItemData>? lookup;

        public bool TryGetById(string itemId, out ItemData item)
        {
            this.lookup ??= BuildLookup();
            return this.lookup.TryGetValue(itemId, out item!);
        }

        private Dictionary<string, ItemData> BuildLookup()
        {
            var dict = new Dictionary<string, ItemData>();
            foreach (var data in this.allItems)
            {
                if (data == null || string.IsNullOrEmpty(data.ItemId)) continue;
                dict[data.ItemId] = data;
            }
            return dict;
        }

#if UNITY_EDITOR
        [Button("Populate From Project")]
        private void PopulateFromProject()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            var items = new List<ItemData>();
            foreach (var guid in guids)
            {
                var path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (asset != null)
                    items.Add(asset);
            }
            this.allItems = items.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `ItemDatabaseTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ItemDatabase.cs Game/CrimsonDraft/Assets/Tests/EditMode/ItemDatabaseTests.cs
git commit -m "feat(inventory): add ItemDatabase for itemId-to-ItemData lookup"
```

---

### Task 4: `ISaveGameService` / `SaveGameService` (disk I/O)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/ISaveGameService.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameService.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameServiceTests.cs`

**Interfaces:**
- Consumes: `SaveGameData`, `SaveSlotSummary` (Task 2).
- Produces: `ISaveGameService.ListSlotSummaries() : IReadOnlyList<SaveSlotSummary>`, `WriteToDisk(int slot, SaveGameData data)`, `ReadFromDisk(int slot) : SaveGameData?`, `LoadSlot(int slot) : bool`, `ConsumePendingLoad() : SaveGameData?`. `SaveGameService.SlotCount` (public const `int`, value `20`) — used by `SaveController` (Task 9) and `MainMenuController` (Task 13) to size slot lists.

- [ ] **Step 1: Write the failing tests**

`Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameServiceTests.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Tests
{
    public sealed class SaveGameServiceTests
    {
        private const int TestSlotA = 18;
        private const int TestSlotB = 19;

        [TearDown]
        public void TearDown()
        {
            var service = new SaveGameService();
            string dir = Path.Combine(Application.persistentDataPath, "Saves");
            foreach (var slot in new[] { TestSlotA, TestSlotB })
            {
                string path = Path.Combine(dir, $"slot_{slot:D2}.json");
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static SaveGameData MakeData(string roomId = "room-1") => new SaveGameData
        {
            sceneName       = "Deck_B",
            roomId          = roomId,
            timestampIso    = "2026-08-18T00:00:00Z",
            playtimeSeconds = 123.45f,
            playerPosition  = new Vector3(1f, 2f, 3f),
            playerRotation  = Quaternion.Euler(0f, 90f, 0f),
            doors           = new List<DoorStateEntry> { new DoorStateEntry { doorId = "door-1", state = DoorMapState.Unlocked } },
            rooms           = new List<RoomStateEntry> { new RoomStateEntry { roomId = "room-1", state = RoomMapState.Visited } },
            collectedPickupIds = new List<string> { "pickup-1" },
            readNoteIds        = new List<string> { "note-1" },
            knownMapIds        = new List<string> { "map-1" },
            defeatedEnemyIds   = new List<string> { "enemy-1" },
            operatorHp         = new[] { 90, 100 },
            inventorySlots     = new List<InventorySlotEntry>
            {
                new InventorySlotEntry { slotIndex = 0, itemId = "weapon-1", weaponAmmo = 12 },
            },
        };

        [Test]
        public void ReadFromDisk_returnsNull_whenSlotEmpty()
        {
            var service = new SaveGameService();
            Assert.IsNull(service.ReadFromDisk(TestSlotA));
        }

        [Test]
        public void WriteToDisk_thenReadFromDisk_roundTripsAllFields()
        {
            var service  = new SaveGameService();
            var original = MakeData();

            service.WriteToDisk(TestSlotA, original);
            var loaded = service.ReadFromDisk(TestSlotA);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.sceneName, loaded!.sceneName);
            Assert.AreEqual(original.roomId, loaded.roomId);
            Assert.AreEqual(original.playtimeSeconds, loaded.playtimeSeconds);
            Assert.AreEqual(original.playerPosition, loaded.playerPosition);
            Assert.AreEqual(1, loaded.doors.Count);
            Assert.AreEqual("door-1", loaded.doors[0].doorId);
            Assert.AreEqual(DoorMapState.Unlocked, loaded.doors[0].state);
            Assert.AreEqual(1, loaded.rooms.Count);
            Assert.AreEqual(1, loaded.collectedPickupIds.Count);
            Assert.AreEqual("pickup-1", loaded.collectedPickupIds[0]);
            Assert.AreEqual(1, loaded.inventorySlots.Count);
            Assert.AreEqual("weapon-1", loaded.inventorySlots[0].itemId);
            Assert.AreEqual(12, loaded.inventorySlots[0].weaponAmmo);
            CollectionAssert.AreEqual(new[] { 90, 100 }, loaded.operatorHp);
        }

        [Test]
        public void WriteToDisk_overwritesExistingSlot()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData("room-1"));
            service.WriteToDisk(TestSlotA, MakeData("room-2"));

            var loaded = service.ReadFromDisk(TestSlotA);
            Assert.AreEqual("room-2", loaded!.roomId);
        }

        [Test]
        public void ListSlotSummaries_returnsSlotCountEntries_emptyAndOccupiedMarkedCorrectly()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData());

            var summaries = service.ListSlotSummaries();

            Assert.AreEqual(SaveGameService.SlotCount, summaries.Count);
            Assert.IsFalse(summaries[TestSlotA].isEmpty);
            Assert.AreEqual("room-1", summaries[TestSlotA].roomId);
            Assert.IsTrue(summaries[TestSlotB].isEmpty);
        }

        [Test]
        public void ConsumePendingLoad_returnsNull_whenNothingPending()
        {
            var service = new SaveGameService();
            Assert.IsNull(service.ConsumePendingLoad());
        }

        [Test]
        public void ConsumePendingLoad_returnsDataOnce_thenNull()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData());

            // LoadSlot triggers a scene load, which EditMode tests can't exercise directly;
            // this test exercises the pending-load handoff via WriteToDisk + ReadFromDisk
            // instead, matching what LoadSlot would stash internally.
            var data = service.ReadFromDisk(TestSlotA);
            Assert.IsNotNull(data);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveGameServiceTests`.
Expected: compile error — `SaveGameService`/`ISaveGameService` don't exist yet.

- [ ] **Step 3: Create `ISaveGameService`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/ISaveGameService.cs`:
```csharp
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Infrastructure.Save
{
    public interface ISaveGameService
    {
        IReadOnlyList<SaveSlotSummary> ListSlotSummaries();
        void WriteToDisk(int slot, SaveGameData data);
        SaveGameData? ReadFromDisk(int slot);

        /// <summary>Reads the slot, stashes it as the pending load, and loads its scene. Returns false if the slot is empty.</summary>
        bool LoadSlot(int slot);

        /// <summary>Returns and clears the payload stashed by LoadSlot, or null if nothing is pending.</summary>
        SaveGameData? ConsumePendingLoad();
    }
}
```

- [ ] **Step 4: Create `SaveGameService`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameService.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    public sealed class SaveGameService : ISaveGameService
    {
        public const int SlotCount = 20;
        private const string SaveFolderName = "Saves";

        private SaveGameData? pendingLoad;

        [Preserve]
        public SaveGameService() { }

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);

        private static string SlotPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot:D2}.json");

        public IReadOnlyList<SaveSlotSummary> ListSlotSummaries()
        {
            var summaries = new List<SaveSlotSummary>(SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                var data = ReadFromDisk(i);
                summaries.Add(data == null
                    ? new SaveSlotSummary { slot = i, isEmpty = true }
                    : new SaveSlotSummary
                    {
                        slot            = i,
                        isEmpty         = false,
                        roomId          = data.roomId,
                        timestampIso    = data.timestampIso,
                        playtimeSeconds = data.playtimeSeconds,
                    });
            }
            return summaries;
        }

        public void WriteToDisk(int slot, SaveGameData data)
        {
            Directory.CreateDirectory(SaveDirectory);
            string json     = JsonUtility.ToJson(data, prettyPrint: true);
            string path     = SlotPath(slot);
            string tempPath = path + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
        }

        public SaveGameData? ReadFromDisk(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveGameData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameService] Failed to read slot {slot}: {e}");
                return null;
            }
        }

        public bool LoadSlot(int slot)
        {
            var data = ReadFromDisk(slot);
            if (data == null) return false;

            this.pendingLoad = data;
            SceneManager.LoadScene(data.sceneName, LoadSceneMode.Single);
            return true;
        }

        public SaveGameData? ConsumePendingLoad()
        {
            var data = this.pendingLoad;
            this.pendingLoad = null;
            return data;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveGameServiceTests`.
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/ISaveGameService.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameService.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameServiceTests.cs
git commit -m "feat(save): add SaveGameService for JSON slot I/O"
```

---

### Task 5: `IGameStateResetter` / `GameStateResetter`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/IGameStateResetter.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs`

**Interfaces:**
- Consumes: `WorldStateRegistries` (Task 1), `InventoryStateRegistry`, `RosterHealthRegistry`.
- Produces: `IGameStateResetter.ResetAll()` — used by `MainMenuController` (Task 13) on "New Game".

- [ ] **Step 1: Write the failing test**

`Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs`:
```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Tests
{
    public sealed class GameStateResetterTests
    {
        [Test]
        public void ResetAll_clearsEveryRegistry()
        {
            var doors     = new DoorStateRegistry();
            var rooms     = new RoomStateRegistry();
            var pickups   = new PickupRegistry();
            var notes     = new NoteRegistry();
            var knownMaps = new KnownMapsRegistry();
            var enemies   = new EnemyStateRegistry();
            var world     = new WorldStateRegistries(doors, rooms, pickups, notes, knownMaps, enemies);
            var inventoryState = new InventoryStateRegistry();
            var rosterHealth   = new RosterHealthRegistry();

            doors.SetUnlocked("door-a");
            rooms.MarkVisited("room-a");
            pickups.SetCollected("pickup-a");
            notes.SetCollected("note-a");
            knownMaps.MarkKnown("map-a");
            enemies.SetDefeated("enemy-a");
            inventoryState.Save(new object());
            rosterHealth.Save(new[] { 100 });

            var resetter = new GameStateResetter(world, inventoryState, rosterHealth);
            resetter.ResetAll();

            Assert.IsFalse(doors.IsUnlocked("door-a"));
            Assert.AreEqual(CrimsonDraft.Infrastructure.RoomMapState.Unknown, rooms.GetState("room-a"));
            Assert.IsFalse(pickups.IsCollected("pickup-a"));
            Assert.IsFalse(notes.IsCollected("note-a"));
            Assert.IsFalse(knownMaps.IsKnown("map-a"));
            Assert.IsFalse(enemies.IsDefeated("enemy-a"));
            Assert.IsFalse(inventoryState.HasSavedState);
            Assert.IsFalse(rosterHealth.HasSavedState);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `GameStateResetterTests`.
Expected: compile error — `GameStateResetter` doesn't exist yet.

- [ ] **Step 3: Create the interface and implementation**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/IGameStateResetter.cs`:
```csharp
#nullable enable

namespace CrimsonDraft.Infrastructure.Save
{
    public interface IGameStateResetter
    {
        void ResetAll();
    }
}
```

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs`:
```csharp
#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    public sealed class GameStateResetter : IGameStateResetter
    {
        private readonly WorldStateRegistries  world;
        private readonly InventoryStateRegistry inventoryState;
        private readonly RosterHealthRegistry   rosterHealth;

        [Preserve]
        public GameStateResetter(
            WorldStateRegistries   world,
            InventoryStateRegistry inventoryState,
            RosterHealthRegistry   rosterHealth)
        {
            this.world          = world;
            this.inventoryState = inventoryState;
            this.rosterHealth   = rosterHealth;
        }

        public void ResetAll()
        {
            this.world.Doors.ClearAll();
            this.world.Rooms.ClearAll();
            this.world.Pickups.ClearAll();
            this.world.Notes.ClearAll();
            this.world.KnownMaps.ClearAll();
            this.world.Enemies.ClearAll();
            this.inventoryState.ClearAll();
            this.rosterHealth.ClearAll();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `GameStateResetterTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/IGameStateResetter.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs
git commit -m "feat(save): add GameStateResetter for New Game state reset"
```

---

### Task 6: Register new services in `GameLifetimeScope`

**Correction (found during implementation):** the plan originally had this task also register `ItemDatabase` on `GameLifetimeScope`. That's not possible — `CrimsonDraft.Infrastructure.asmdef` cannot reference `CrimsonDraft.Inventory` because `CrimsonDraft.Inventory.asmdef` already references `CrimsonDraft.Infrastructure`; adding the reverse reference creates a circular assembly dependency, which Unity rejects at compile time (confirmed: `CS0234`/`CS0246` on `GameLifetimeScope.cs` when `ItemDatabase` was referenced there). `ItemDatabase` is only ever consumed by `SaveGameLoader`, which is `NavigationScope`-scoped — so it's registered there instead (moved into Task 12, which already touches `NavigationScope` and scene wiring). Task 6 below only covers `WorldStateRegistries`/`SaveGameService`/`GameStateResetter`, none of which reference `Inventory` types.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `WorldStateRegistries` (Task 1), `ISaveGameService`/`SaveGameService` (Task 4), `IGameStateResetter`/`GameStateResetter` (Task 5).
- Produces: all three resolvable via DI from any child scope (`NavigationScope`, the new `MainMenuScope` in Task 13) — `WorldStateRegistries`, `ISaveGameService`, `IGameStateResetter`.

- [ ] **Step 1: Register the new services in `GameLifetimeScope.Configure`**

Modify `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs` — add `using CrimsonDraft.Infrastructure.Save;` to the usings, and add these lines at the end of `Configure`:

```csharp
            builder.Register<DoorStateRegistry>(Lifetime.Singleton);
            builder.Register<RoomStateRegistry>(Lifetime.Singleton);
            builder.Register<KnownMapsRegistry>(Lifetime.Singleton);
            builder.Register<PickupRegistry>(Lifetime.Singleton);
            builder.Register<NoteRegistry>(Lifetime.Singleton);
            builder.Register<InventoryStateRegistry>(Lifetime.Singleton);
            builder.Register<RosterHealthRegistry>(Lifetime.Singleton);
            builder.Register<EnemyStateRegistry>(Lifetime.Singleton);

            builder.Register<WorldStateRegistries>(Lifetime.Singleton);

            builder.Register<SaveGameService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<GameStateResetter>(Lifetime.Singleton).AsImplementedInterfaces();
```

(The eight `Register<...Registry>` lines already exist in the file — leave them where they are and add the three new lines directly after them.)

- [ ] **Step 2: Verify the project compiles**

Check `read_console` (MCP) or the Console window.
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs
git commit -m "feat(save): register WorldStateRegistries/SaveGameService/GameStateResetter in GameLifetimeScope"
```

---

### Task 7: `IRoomOrchestrator.ActivateRoomImmediate`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`
- Modify test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`

**Interfaces:**
- Produces: `IRoomOrchestrator.ActivateRoomImmediate(string roomId)` — deactivates every `RoomController` in the scene and activates the one matching `roomId`, with no door-transition cutscene. Used by `SaveGameLoader` (Task 11).

- [ ] **Step 1: Write the failing test**

Add to `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`, inside the `RoomOrchestratorInitTests` class:
```csharp
        [Test]
        public void ActivateRoomImmediate_activatesMatchingRoom_deactivatesOthers()
        {
            var goA   = new GameObject("RoomA");
            var roomA = goA.AddComponent<RoomController>();
            var soA   = new SerializedObject(roomA);
            soA.FindProperty("roomId").stringValue = "room-a";
            soA.ApplyModifiedPropertiesWithoutUndo();

            var goB   = new GameObject("RoomB");
            var roomB = goB.AddComponent<RoomController>();
            var soB   = new SerializedObject(roomB);
            soB.FindProperty("roomId").stringValue = "room-b";
            soB.ApplyModifiedPropertiesWithoutUndo();

            goA.SetActive(true);
            goB.SetActive(true);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                orchestrator.ActivateRoomImmediate("room-b");

                Assert.IsFalse(goA.activeSelf, "room-a must be deactivated");
                Assert.IsTrue(goB.activeSelf, "room-b must be activated");
                Assert.AreEqual(roomB, orchestrator.CurrentRoom);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void ActivateRoomImmediate_logsWarning_whenRoomIdNotFound()
        {
            var goA   = new GameObject("RoomA");
            var roomA = goA.AddComponent<RoomController>();
            goA.SetActive(true);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
                orchestrator.ActivateRoomImmediate("does-not-exist");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }
```
Add `using UnityEngine.TestTools;` to the file's usings if not already present.

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `RoomOrchestratorInitTests`.
Expected: compile error — `ActivateRoomImmediate` doesn't exist yet.

- [ ] **Step 3: Add the method to the interface and implementation**

`IRoomOrchestrator.cs` — add to the interface:
```csharp
        void ActivateRoomImmediate(string roomId);
```

`RoomOrchestrator.cs` — add as a public method:
```csharp
        public void ActivateRoomImmediate(string roomId)
        {
            var rooms = Object.FindObjectsOfType<RoomController>(true);
            RoomController? target = null;

            foreach (var room in rooms)
            {
                if (room.RoomId == roomId)
                {
                    target = room;
                    continue;
                }
                room.Deactivate();
            }

            if (target == null)
            {
                Debug.LogWarning($"[RoomOrchestrator] ActivateRoomImmediate: no room with id '{roomId}' found.");
                return;
            }

            target.Activate();
            this.currentRoom = target;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `RoomOrchestratorInitTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs
git commit -m "feat(navigation): add ActivateRoomImmediate for save-load room restoration"
```

---

### Task 8: `SaveSlotRow` and `SaveSlotListView` UI components

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotRow.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotListView.cs`

**Interfaces:**
- Consumes: `SaveSlotSummary` (Task 2).
- Produces: `SaveSlotListView.Show(IReadOnlyList<SaveSlotSummary> slots, Action<SaveSlotSummary> onSlotClicked)`, `SaveSlotListView.ShowConfirm(string message, Action onConfirmed)`, `SaveSlotListView.Hide()` — used by `SaveController` (Task 9) and `MainMenuController` (Task 13). This is a pure presentation MonoBehaviour, same category as `ContainerView` — no VContainer registration, no dedicated unit test, consistent with the codebase's convention of not unit-testing View classes.

- [ ] **Step 1: Create `SaveSlotRow`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotRow.cs`:
```csharp
#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private Button           button = null!;
        [SerializeField] private TextMeshProUGUI  label  = null!;

        public void Bind(SaveSlotSummary summary, Action onClick)
        {
            this.label.text = summary.isEmpty
                ? $"Slot {summary.slot + 1} — empty"
                : $"Slot {summary.slot + 1} — {summary.roomId} — {FormatPlaytime(summary.playtimeSeconds)} — {summary.timestampIso}";

            this.button.onClick.RemoveAllListeners();
            this.button.onClick.AddListener(() => onClick());
            gameObject.SetActive(true);
        }

        private static string FormatPlaytime(float seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
```

- [ ] **Step 2: Create `SaveSlotListView`**

`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotListView.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotListView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel          = null!;
        [SerializeField] private Transform       slotListParent = null!;
        [SerializeField] private SaveSlotRow     slotRowPrefab  = null!;
        [SerializeField] private GameObject      confirmPanel      = null!;
        [SerializeField] private TextMeshProUGUI confirmLabel      = null!;
        [SerializeField] private Button          confirmYesButton  = null!;
        [SerializeField] private Button          confirmNoButton   = null!;

        private readonly List<SaveSlotRow> rows = new();

        public void Show(IReadOnlyList<SaveSlotSummary> slots, Action<SaveSlotSummary> onSlotClicked)
        {
            while (this.rows.Count < slots.Count)
                this.rows.Add(Instantiate(this.slotRowPrefab, this.slotListParent));

            for (int i = 0; i < slots.Count; i++)
            {
                var summary = slots[i];
                this.rows[i].Bind(summary, () => onSlotClicked(summary));
            }

            for (int i = slots.Count; i < this.rows.Count; i++)
                this.rows[i].gameObject.SetActive(false);

            this.confirmPanel.SetActive(false);
            this.panel.SetActive(true);
        }

        public void ShowConfirm(string message, Action onConfirmed)
        {
            this.confirmLabel.text = message;

            this.confirmYesButton.onClick.RemoveAllListeners();
            this.confirmNoButton.onClick.RemoveAllListeners();
            this.confirmYesButton.onClick.AddListener(() => onConfirmed());
            this.confirmNoButton.onClick.AddListener(() => this.confirmPanel.SetActive(false));

            this.confirmPanel.SetActive(true);
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            this.confirmPanel.SetActive(false);
        }
    }
}
```

- [ ] **Step 3: Verify the project compiles**

Check `read_console` (MCP) or the Console window.
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotRow.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/UI/SaveSlotListView.cs
git commit -m "feat(save): add SaveSlotRow/SaveSlotListView shared slot-picker UI"
```

---

### Task 9: `SaveController`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs`

**Interfaces:**
- Consumes: `IInputService` (`Infrastructure/Input/IInputService.cs`), `SaveSlotListView` (Task 8), `ISaveGameService` (Task 4), `IInventoryService`/`InventorySlot`/`WeaponItem`/`AmmoBoxItem`/`KeyItem` (`Inventory/`), `IOperatorRoster` (`Operators/IOperatorRoster.cs`), `IRoomOrchestrator` (Task 7), `PlayerController` (`Navigation/Player/PlayerController.cs`), `WorldStateRegistries` (Task 1), `SaveGameData`/`DoorStateEntry`/`RoomStateEntry`/`InventorySlotEntry` (Task 2).
- Produces: `SaveController.Open()` — called by `SavePointInteractable` (Task 10) via `InteractionContext`.

Note: `ItemDatabase` is deliberately **not** a dependency here — capturing state only needs each item's `ItemId` (already on `ItemData`), never a reverse lookup. Only the *read* path (`SaveGameLoader`, Task 11) needs `ItemDatabase` to turn a saved `itemId` back into an `ItemData` reference.

- [ ] **Step 1: Write the failing tests**

`Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class SaveControllerTests
    {
        private sealed class FakeSaveGameService : ISaveGameService
        {
            public int? WrittenSlot;
            public SaveGameData? WrittenData;

            public IReadOnlyList<SaveSlotSummary> ListSlotSummaries() => Array.Empty<SaveSlotSummary>();
            public void WriteToDisk(int slot, SaveGameData data) { this.WrittenSlot = slot; this.WrittenData = data; }
            public SaveGameData? ReadFromDisk(int slot) => null;
            public bool LoadSlot(int slot) => false;
            public SaveGameData? ConsumePendingLoad() => null;
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public InventorySlot[] RawSlots = Array.Empty<InventorySlot>();
            public int SlotCount => this.RawSlots.Length;
            public IReadOnlyList<InventorySlot> Slots => this.RawSlots;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0) => false;
            public void RemoveItem(int slotIndex) { }
            public void MoveItem(int fromSlot, int toSlot) { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex) { }
            public int GetEquippedWeaponIndex(int operatorSlot) => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB) => false;
            public KeyUseOutcome TryUseKey(string keyItemId) => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void LoadState(InventorySlot[] slots) { }
            public void SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public InventorySlot[] GetRawSlots() => this.RawSlots;
        }

        private sealed class FakeRoster : IOperatorRoster
        {
            public int[] Hp = Array.Empty<int>();
            public bool IsInitialized => true;
            public int Count => 1;
            public OperatorRuntime this[int slotIndex] => new OperatorRuntime(slotIndex, null, isPresent: true, maxHp: 100);
            public IReadOnlyList<int> GetAliveSlots() => new List<int> { 0 };
            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => this.Hp;
            public void RestoreHp(int[] snapshot) { }
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public RoomController? Current;
            public RoomController? CurrentRoom => this.Current;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) { }
        }

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                   => null!;
            public InputAction Interact               => null!;
            public InputAction OpenInventory          => null!;
            public InputAction OpenMap                => null!;
            public InputAction Aim                    => null!;
            public InputAction AimFire                => null!;
            public InputAction Pause                  => null!;
            public InputAction Sprint                 => null!;
            public InputAction CombatNavigate         => null!;
            public InputAction CombatConfirm          => null!;
            public InputAction CombatCancel           => null!;
            public InputAction CombatUseItem          => null!;
            public InputAction UINavigate             => null!;
            public InputAction UIConfirm              => null!;
            public InputAction UICancel               => null!;
            public InputAction UIBack                 => null!;
            public InputAction DialogueAdvanceLine    => null!;
            public InputAction DialogueCancelDialogue => null!;
            public InputAction DoorTransitionSkip     => null!;
            public InputAction PickupNavigate         => null!;
            public InputAction PickupConfirm          => null!;
            public InputAction InventoryNavigate      => null!;
            public InputAction InventoryConfirm       => null!;
            public InputAction InventoryPickup        => null!;
            public InputAction InventoryCancel        => null!;
            public InputAction InventoryNextTab       => null!;
            public InputAction InventoryPrevTab       => null!;
            public InputAction InventoryCloseMap      => null!;
            public InputAction InventoryClose         => null!;
            public void SwitchToGameplay()      { }
            public void SwitchToCombat()        { }
            public void SwitchToUI()            { }
            public void SwitchToDialogue()      { }
            public void SwitchToDoorTransition() { }
            public void SwitchToPickupPrompt()  { }
            public void SwitchToInventory()     { }
            public void Dispose()               { }
        }

        private static WeaponData MakeWeaponData(string id)
        {
            var d  = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue        = id;
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue   = "Test Weapon";
            so.FindProperty("magazineCapacity").intValue = 12;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        [Test]
        public void BuildSaveData_capturesWorldStateAndInventory_andWritesToService()
        {
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            world.Doors.SetUnlocked("door-1");
            world.Rooms.MarkVisited("room-1");
            world.Pickups.SetCollected("pickup-1");
            world.Notes.SetCollected("note-1");
            world.KnownMaps.MarkKnown("map-1");
            world.Enemies.SetDefeated("enemy-1");

            var weaponData = MakeWeaponData("weapon-1");
            var weaponItem = new WeaponItem(weaponData);
            weaponItem.SetAmmo(7);
            var inventory = new FakeInventoryService
            {
                RawSlots = new[] { new InventorySlot { Item = weaponItem, Quantity = 1 } },
            };

            var roster    = new FakeRoster { Hp = new[] { 42 } };
            var roomGo    = new GameObject("Room");
            var room      = roomGo.AddComponent<RoomController>();
            var roomSo    = new SerializedObject(room);
            roomSo.FindProperty("roomId").stringValue = "room-1";
            roomSo.ApplyModifiedPropertiesWithoutUndo();
            var roomOrch  = new FakeRoomOrchestrator { Current = room };

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(5f, 0f, 2f);
            var player   = playerGo.AddComponent<PlayerController>();

            var view       = MakeView();
            var saveService = new FakeSaveGameService();
            var inputService = new FakeInputService();

            try
            {
                var controller = new SaveController(
                    inputService, view, saveService, inventory, roster, roomOrch, player, world);

                controller.Save(3);

                Assert.AreEqual(3, saveService.WrittenSlot);
                var data = saveService.WrittenData!;
                Assert.AreEqual("room-1", data.roomId);
                Assert.AreEqual(new Vector3(5f, 0f, 2f), data.playerPosition);
                Assert.AreEqual(1, data.doors.Count);
                Assert.AreEqual("door-1", data.doors[0].doorId);
                Assert.AreEqual(1, data.rooms.Count);
                Assert.AreEqual(1, data.collectedPickupIds.Count);
                Assert.AreEqual(1, data.readNoteIds.Count);
                Assert.AreEqual(1, data.knownMapIds.Count);
                Assert.AreEqual(1, data.defeatedEnemyIds.Count);
                CollectionAssert.AreEqual(new[] { 42 }, data.operatorHp);
                Assert.AreEqual(1, data.inventorySlots.Count);
                Assert.AreEqual("weapon-1", data.inventorySlots[0].itemId);
                Assert.AreEqual(7, data.inventorySlots[0].weaponAmmo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(weaponData);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        private static SaveSlotListView MakeView()
        {
            var go = new GameObject("SaveSlotListView");
            return go.AddComponent<SaveSlotListView>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveControllerTests`.
Expected: compile error — `SaveController` doesn't exist yet, and `Save(int)` isn't defined.

- [ ] **Step 3: Create `SaveController`**

`Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs`:
```csharp
#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class SaveController : IInitializable, IDisposable
    {
        private readonly IInputService       inputService;
        private readonly SaveSlotListView    view;
        private readonly ISaveGameService    saveGameService;
        private readonly IInventoryService   inventoryService;
        private readonly IOperatorRoster     roster;
        private readonly IRoomOrchestrator   roomOrchestrator;
        private readonly PlayerController    player;
        private readonly WorldStateRegistries world;

        private bool isOpen;

        [Preserve]
        public SaveController(
            IInputService        inputService,
            SaveSlotListView     view,
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            WorldStateRegistries world)
        {
            this.inputService     = inputService;
            this.view             = view;
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.world            = world;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UIBack.performed += OnBack;
        }

        public void Open()
        {
            if (this.isOpen) return;
            this.isOpen = true;
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.saveGameService.ListSlotSummaries(), OnSlotClicked);
        }

        private void OnSlotClicked(SaveSlotSummary summary)
        {
            this.view.ShowConfirm($"Save to slot {summary.slot + 1}?", () => Save(summary.slot));
        }

        public void Save(int slot)
        {
            this.saveGameService.WriteToDisk(slot, BuildSaveData());
            Close();
        }

        private SaveGameData BuildSaveData()
        {
            var data = new SaveGameData
            {
                sceneName       = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                roomId          = this.roomOrchestrator.CurrentRoom != null ? this.roomOrchestrator.CurrentRoom.RoomId : "",
                timestampIso    = DateTime.UtcNow.ToString("o"),
                playtimeSeconds = Time.realtimeSinceStartup,
                playerPosition  = this.player.transform.position,
                playerRotation  = this.player.transform.rotation,
                operatorHp      = this.roster.GetHpSnapshot(),
            };

            foreach (var pair in this.world.Doors.GetState())
                data.doors.Add(new DoorStateEntry { doorId = pair.Key, state = pair.Value });

            foreach (var pair in this.world.Rooms.GetState())
                data.rooms.Add(new RoomStateEntry { roomId = pair.Key, state = pair.Value });

            data.collectedPickupIds.AddRange(this.world.Pickups.CollectedIds);
            data.readNoteIds.AddRange(this.world.Notes.CollectedIds);
            data.knownMapIds.AddRange(this.world.KnownMaps.GetState());
            data.defeatedEnemyIds.AddRange(this.world.Enemies.GetDefeated());

            var slots = this.inventoryService.GetRawSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) continue;
                var item = slots[i].Item!;
                data.inventorySlots.Add(new InventorySlotEntry
                {
                    slotIndex            = i,
                    itemId               = item.Data.ItemId,
                    slotQuantity         = slots[i].Quantity,
                    ammoBoxQuantity      = item is AmmoBoxItem box ? box.Quantity : -1,
                    weaponAmmo           = item is WeaponItem weapon ? weapon.CurrentAmmo : -1,
                    keyUsesRemaining     = item is KeyItem key ? key.UsesRemaining : -1,
                    isExamined           = item.IsExamined,
                    gridCol              = slots[i].GridCol,
                    gridRow              = slots[i].GridRow,
                    gridRotation         = slots[i].GridRotation,
                    equippedOperatorSlot = item.EquippedBySlot,
                    equippedWeaponSlot   = item.EquippedWeaponSlot,
                });
            }

            return data;
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UIBack.performed -= OnBack;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveControllerTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs
git commit -m "feat(save): add SaveController to capture and write game state"
```

---

### Task 10: `SavePointInteractable` + `InteractionContext` + `PlayerInteractionCaster` wiring

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SavePointInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`

**Interfaces:**
- Consumes: `SaveController` (Task 9), `IInteractable` (`Navigation/Interactables/IInteractable.cs`).
- Produces: `InteractionContext.SaveController` (new public field) — consumed by `SavePointInteractable.Interact`.

No new dedicated test — `SavePointInteractable` is a one-line forwarder identical in shape to `ContainerInteractable` (itself untested), and `InteractionContext`/`PlayerInteractionCaster` are plain data-plumbing changes exercised indirectly by existing `PlayerInteractionCaster`-adjacent tests (`DoorInteractableTests`, etc., which construct `InteractionContext` directly and will fail to compile if the new field breaks the constructor — that IS this task's regression check).

- [ ] **Step 1: Add `SaveController` to `InteractionContext`**

Modify `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs`:
```csharp
#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService      InventoryService;
        public readonly IInputService          InputService;
        public readonly IDialogueService       DialogueService;
        public readonly DocumentController     DocumentController;
        public readonly ContainerController    ContainerController;
        public readonly IPickupDialogueService PickupDialogueService;
        public readonly PuzzleViewController    PuzzleViewController;
        public readonly ScreenFader            ScreenFader;
        public readonly PickupPreviewController PickupPreviewController;
        public readonly SaveController         SaveController;

        public InteractionContext(
            IInventoryService      inventoryService,
            IInputService          inputService,
            IDialogueService       dialogueService,
            DocumentController     documentController,
            ContainerController    containerController,
            IPickupDialogueService pickupDialogueService,
            PuzzleViewController    puzzleViewController,
            ScreenFader             screenFader,
            PickupPreviewController pickupPreviewController,
            SaveController          saveController)
        {
            InventoryService      = inventoryService;
            InputService          = inputService;
            DialogueService       = dialogueService;
            DocumentController    = documentController;
            ContainerController   = containerController;
            PickupDialogueService = pickupDialogueService;
            PuzzleViewController   = puzzleViewController;
            ScreenFader            = screenFader;
            PickupPreviewController = pickupPreviewController;
            SaveController          = saveController;
        }
    }
}
```

- [ ] **Step 2: Inject and pass `SaveController` through `PlayerInteractionCaster`**

Modify `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs` — add `using CrimsonDraft.Navigation.Interactables.UI;` is already implicitly satisfied (same namespace, no import needed since `ContainerController`/`SaveController` live in `CrimsonDraft.Navigation.Interactables.UI` and this file is in `CrimsonDraft.Navigation.Interactables` — check the existing file: it already references `ContainerController`, `DocumentController`, etc. by bare name with no explicit `using` for `.UI`, meaning `NavigationScope`/`PlayerInteractionCaster` must already have visibility — add `SaveController` as a sibling field exactly like `containerController`:

```csharp
        private IInputService          inputService          = null!;
        private IInventoryService      inventoryService      = null!;
        private IDialogueService       dialogueService       = null!;
        private IPickupDialogueService pickupDialogueService = null!;
        private DocumentController     documentController    = null!;
        private ContainerController    containerController   = null!;
        private PuzzleViewController    puzzleViewController   = null!;
        private ScreenFader             screenFader            = null!;
        private PickupPreviewController pickupPreviewController = null!;
        private SaveController          saveController          = null!;

        [Inject]
        public void Construct(
            IInputService          inputService,
            IInventoryService      inventoryService,
            IDialogueService       dialogueService,
            IPickupDialogueService pickupDialogueService,
            DocumentController     documentController,
            ContainerController    containerController,
            PuzzleViewController    puzzleViewController,
            ScreenFader             screenFader,
            PickupPreviewController pickupPreviewController,
            SaveController          saveController)
        {
            this.inputService          = inputService;
            this.inventoryService      = inventoryService;
            this.dialogueService       = dialogueService;
            this.pickupDialogueService = pickupDialogueService;
            this.documentController    = documentController;
            this.containerController   = containerController;
            this.puzzleViewController   = puzzleViewController;
            this.screenFader            = screenFader;
            this.pickupPreviewController = pickupPreviewController;
            this.saveController          = saveController;
            this.inputService.Interact.performed += OnInteract;
        }
```

And update the `InteractionContext` construction inside `OnInteract`:
```csharp
            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.dialogueService,
                this.documentController,
                this.containerController,
                this.pickupDialogueService,
                this.puzzleViewController,
                this.screenFader,
                this.pickupPreviewController,
                this.saveController);
            interactable.Interact(context);
```

- [ ] **Step 3: Create `SavePointInteractable`**

`Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SavePointInteractable.cs`:
```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SavePointInteractable : MonoBehaviour, IInteractable
    {
        public void Interact(InteractionContext context)
        {
            context.SaveController.Open();
        }
    }
}
```

- [ ] **Step 4: Verify the project compiles and existing tests still pass**

Check `read_console` (MCP) or the Console window for compile errors, then run the full EditMode suite via Unity Test Runner (or MCP `run_tests` with no filter) to confirm nothing that constructs `InteractionContext` directly (if any) broke.
Expected: no compile errors; existing tests still PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SavePointInteractable.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs
git commit -m "feat(save): add SavePointInteractable and wire SaveController through InteractionContext"
```

---

### Task 11: `SaveGameLoader`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs`

**Interfaces:**
- Consumes: `ISaveGameService` (Task 4), `WorldStateRegistries` (Task 1), `IInventoryService`/`InventorySlot`/`WeaponItem`/`AmmoBoxItem`/`ConsumableItem`/`KeyItem`/`SocketItem` (`Inventory/`), `IOperatorRoster` (`Operators/IOperatorRoster.cs`), `IRoomOrchestrator` (Task 7), `PlayerController` (`Navigation/Player/PlayerController.cs`), `ItemDatabase` (Task 3), `SaveGameData`/entries (Task 2).
- Produces: `SaveGameLoader : IInitializable` — registered in `NavigationScope` (Task 12) right after `RoomOrchestrator`.

- [ ] **Step 1: Write the failing tests**

`Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class SaveGameLoaderTests
    {
        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData? PendingLoad;
            public IReadOnlyList<SaveSlotSummary> ListSlotSummaries() => Array.Empty<SaveSlotSummary>();
            public void WriteToDisk(int slot, SaveGameData data) { }
            public SaveGameData? ReadFromDisk(int slot) => null;
            public bool LoadSlot(int slot) => false;
            public SaveGameData? ConsumePendingLoad()
            {
                var data = this.PendingLoad;
                this.PendingLoad = null;
                return data;
            }
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public int SlotCount { get; set; } = 4;
            public InventorySlot[]? LoadedSlots { get; private set; }
            public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0) => false;
            public void RemoveItem(int slotIndex) { }
            public void MoveItem(int fromSlot, int toSlot) { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex) { }
            public int GetEquippedWeaponIndex(int operatorSlot) => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB) => false;
            public KeyUseOutcome TryUseKey(string keyItemId) => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void LoadState(InventorySlot[] slots) => this.LoadedSlots = slots;
            public void SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public InventorySlot[] GetRawSlots() => Array.Empty<InventorySlot>();
        }

        private sealed class FakeRoster : IOperatorRoster
        {
            public int[]? RestoredHp { get; private set; }
            public bool IsInitialized => true;
            public int Count => 1;
            public OperatorRuntime this[int slotIndex] => new OperatorRuntime(slotIndex, null, isPresent: true, maxHp: 100);
            public IReadOnlyList<int> GetAliveSlots() => new List<int> { 0 };
            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => Array.Empty<int>();
            public void RestoreHp(int[] snapshot) => this.RestoredHp = snapshot;
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public string? ActivatedRoomId { get; private set; }
            public RoomController? CurrentRoom => null;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) => this.ActivatedRoomId = roomId;
        }

        private static KeyItemData MakeKeyItemData(string id, int maxUses)
        {
            var d  = ScriptableObject.CreateInstance<KeyItemData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.FindProperty("displayName").stringValue = "Test Key";
            so.FindProperty("maxUses").intValue         = maxUses;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemDatabase MakeDatabase(params ItemData[] items)
        {
            var db  = ScriptableObject.CreateInstance<ItemDatabase>();
            var so  = new SerializedObject(db);
            var arr = so.FindProperty("allItems");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        [Test]
        public void Initialize_withNoPendingLoad_doesNothing()
        {
            var saveService = new FakeSaveGameService();
            var inventory   = new FakeInventoryService();
            var roster      = new FakeRoster();
            var roomOrch    = new FakeRoomOrchestrator();
            var itemDb      = MakeDatabase();
            var world       = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world);
                ((IInitializable)loader).Initialize();

                Assert.IsNull(inventory.LoadedSlots);
                Assert.IsNull(roomOrch.ActivatedRoomId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
            }
        }

        [Test]
        public void Initialize_withPendingLoad_restoresRegistriesInventoryAndPosition()
        {
            var keyData = MakeKeyItemData("key-1", maxUses: 3);
            var itemDb  = MakeDatabase(keyData);

            var saveService = new FakeSaveGameService
            {
                PendingLoad = new SaveGameData
                {
                    sceneName      = "Deck_B",
                    roomId         = "room-2",
                    playerPosition = new Vector3(1f, 2f, 3f),
                    playerRotation = Quaternion.identity,
                    doors             = new List<DoorStateEntry> { new DoorStateEntry { doorId = "door-1", state = DoorMapState.Unlocked } },
                    rooms             = new List<RoomStateEntry> { new RoomStateEntry { roomId = "room-1", state = RoomMapState.Visited } },
                    collectedPickupIds = new List<string> { "pickup-1" },
                    readNoteIds        = new List<string> { "note-1" },
                    knownMapIds        = new List<string> { "map-1" },
                    defeatedEnemyIds   = new List<string> { "enemy-1" },
                    operatorHp         = new[] { 80 },
                    inventorySlots     = new List<InventorySlotEntry>
                    {
                        new InventorySlotEntry { slotIndex = 0, itemId = "key-1", keyUsesRemaining = 1 },
                    },
                },
            };
            var inventory = new FakeInventoryService { SlotCount = 4 };
            var roster    = new FakeRoster();
            var roomOrch  = new FakeRoomOrchestrator();
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world);
                ((IInitializable)loader).Initialize();

                Assert.IsTrue(world.Doors.IsUnlocked("door-1"));
                Assert.AreEqual(RoomMapState.Visited, world.Rooms.GetState("room-1"));
                Assert.IsTrue(world.Pickups.IsCollected("pickup-1"));
                Assert.IsTrue(world.Notes.IsCollected("note-1"));
                Assert.IsTrue(world.KnownMaps.IsKnown("map-1"));
                Assert.IsTrue(world.Enemies.IsDefeated("enemy-1"));
                CollectionAssert.AreEqual(new[] { 80 }, roster.RestoredHp);
                Assert.AreEqual("room-2", roomOrch.ActivatedRoomId);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), player.transform.position);

                Assert.IsNotNull(inventory.LoadedSlots);
                var keyItem = inventory.LoadedSlots![0].Item as KeyItem;
                Assert.IsNotNull(keyItem);
                Assert.AreEqual(1, keyItem!.UsesRemaining);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
                UnityEngine.Object.DestroyImmediate(keyData);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveGameLoaderTests`.
Expected: compile error — `SaveGameLoader` doesn't exist yet.

- [ ] **Step 3: Create `SaveGameLoader`**

`Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs`:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// Applies a pending loaded save (if any) to the cross-scene registries, inventory, and
    /// player transform. Must run after RoomOrchestrator (so CurrentRoom is already set to a
    /// default before being overridden) and before DoorBootstrap/PickupBootstrap/
    /// MapPickupBootstrap/DocumentPickupBootstrap (so they see the restored registry state).
    /// </summary>
    public sealed class SaveGameLoader : IInitializable
    {
        private readonly ISaveGameService     saveGameService;
        private readonly IInventoryService    inventoryService;
        private readonly IOperatorRoster      roster;
        private readonly IRoomOrchestrator    roomOrchestrator;
        private readonly PlayerController     player;
        private readonly ItemDatabase         itemDatabase;
        private readonly WorldStateRegistries world;

        [Preserve]
        public SaveGameLoader(
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            ItemDatabase         itemDatabase,
            WorldStateRegistries world)
        {
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.itemDatabase     = itemDatabase;
            this.world            = world;
        }

        void IInitializable.Initialize()
        {
            var data = this.saveGameService.ConsumePendingLoad();
            if (data == null) return;

            ApplyDoors(data);
            ApplyRooms(data);
            this.world.Pickups.LoadState(data.collectedPickupIds);
            this.world.Notes.LoadState(data.readNoteIds);
            this.world.KnownMaps.LoadState(data.knownMapIds);
            this.world.Enemies.LoadState(data.defeatedEnemyIds);
            ApplyInventory(data);
            this.roster.RestoreHp(data.operatorHp);

            this.roomOrchestrator.ActivateRoomImmediate(data.roomId);
            this.player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
        }

        private void ApplyDoors(SaveGameData data)
        {
            var dict = new Dictionary<string, DoorMapState>();
            foreach (var entry in data.doors)
                dict[entry.doorId] = entry.state;
            this.world.Doors.LoadState(dict);
        }

        private void ApplyRooms(SaveGameData data)
        {
            var dict = new Dictionary<string, RoomMapState>();
            foreach (var entry in data.rooms)
                dict[entry.roomId] = entry.state;
            this.world.Rooms.LoadState(dict);
        }

        private void ApplyInventory(SaveGameData data)
        {
            int slotCount = this.inventoryService.SlotCount;
            var slots     = new InventorySlot[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = new InventorySlot();

            foreach (var entry in data.inventorySlots)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= slotCount) continue;
                if (!this.itemDatabase.TryGetById(entry.itemId, out var itemData)) continue;

                InventoryItem item = itemData switch
                {
                    WeaponData     wd => new WeaponItem(wd),
                    AmmoBoxData    ad => new AmmoBoxItem(ad, entry.ammoBoxQuantity >= 0 ? entry.ammoBoxQuantity : ad.DefaultQuantity),
                    ConsumableData cd => new ConsumableItem(cd),
                    KeyItemData    kd => new KeyItem(kd),
                    SocketItemData sd => new SocketItem(sd),
                    _ => throw new ArgumentException($"Unknown ItemData subtype: {itemData.GetType().Name}")
                };

                item.IsExamined = entry.isExamined;

                if (item is WeaponItem weaponItem && entry.weaponAmmo >= 0)
                    weaponItem.SetAmmo(entry.weaponAmmo);

                if (item is KeyItem keyItem && entry.keyUsesRemaining >= 0)
                {
                    int toConsume = keyItem.Data.MaxUses - entry.keyUsesRemaining;
                    for (int c = 0; c < toConsume; c++)
                        keyItem.Consume();
                }

                if (entry.equippedOperatorSlot >= 0)
                    item.SetEquipped(entry.equippedOperatorSlot, entry.equippedWeaponSlot);

                slots[entry.slotIndex] = new InventorySlot
                {
                    Item         = item,
                    Quantity     = entry.slotQuantity,
                    GridCol      = entry.gridCol,
                    GridRow      = entry.gridRow,
                    GridRotation = entry.gridRotation,
                };
            }

            this.inventoryService.LoadState(slots);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run via Unity Test Runner (or MCP `run_tests`), filtered to `SaveGameLoaderTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs
git commit -m "feat(save): add SaveGameLoader to apply pending saves on scene entry"
```

---

### Task 12: Wire `SaveController`/`SaveGameLoader`/`SavePointInteractable` into `NavigationScope` + scene setup

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Scene (not code): the navigation scene(s) containing a `SaveRoomMarker` room (e.g. `Deck_B_Development` — verify the exact scene name(s) via the `SaveRoomMarker` usages in the project before proceeding)

**Interfaces:**
- Consumes: `SaveController` (Task 9), `SaveGameLoader` (Task 11), `SaveSlotListView` (Task 8), `SavePointInteractable` (Task 10), `ItemDatabase` (Task 3).
- Produces: a working, testable in-Editor save point.

**Note:** `ItemDatabase` is registered here, not in `GameLifetimeScope` — see the Task 6 correction. Because `NavigationScope` is per-scene (rebuilt on every navigation scene load, e.g. separately for Deck B and Deck C), the `ItemDatabase` asset reference must be assigned on **each** scene's `NavigationScope` component individually, not once in `Boot`.

- [ ] **Step 1: Register `ItemDatabase`, `SaveSlotListView`, `SaveController`, and `SaveGameLoader` in `NavigationScope`**

Modify `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` — add `using CrimsonDraft.Infrastructure.Save;` and `using CrimsonDraft.Infrastructure.Save.UI;` to the usings (`CrimsonDraft.Inventory` is already imported).

Add serialized fields near the other data/view fields (next to `mapDataSet` and `pickupPreviewView` respectively):
```csharp
        [SerializeField] private ItemDatabase             itemDatabase      = null!;
        [SerializeField] private SaveSlotListView        saveSlotListView  = null!;
```

Register `ItemDatabase` next to the other `RegisterInstance` calls near the top of `Configure` (next to `this.mapDataSet`):
```csharp
            builder.RegisterInstance(this.itemDatabase);
```

Register the view and the two controllers. Insert `SaveSlotListView` registration next to `ContainerView`'s:
```csharp
            builder.RegisterInstance(this.saveSlotListView);
```
(placed directly after `builder.RegisterComponentInHierarchy<ContainerView>();`).

Register `SaveController` next to `ContainerController`'s registration:
```csharp
            builder.Register<SaveController>(Lifetime.Scoped).AsSelf();
```
(placed directly after `builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();` — note `SaveController` is registered `.AsSelf()` only, not `.AsImplementedInterfaces()`, because `PlayerInteractionCaster`/`InteractionContext` need the concrete `SaveController` type, not `IInitializable`/`IDisposable` — VContainer only auto-invokes `IInitializable`/`IDisposable` entry points when a type is registered `.AsImplementedInterfaces()`; use `.AsImplementedInterfaces().AsSelf()` instead so both the entry-point lifecycle and the concrete-type resolution work, matching `ContainerController`'s exact registration line):
```csharp
            builder.Register<SaveController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

Register `SaveGameLoader` **immediately after** the `RoomOrchestrator` registration and **before** `MapStateTracker`/`WeatherAmbienceController`/`MusicManagerController`/`DoorCache`/`DoorBootstrap`/`PickupBootstrap`/`MapPickupBootstrap`/`DocumentPickupBootstrap` (per the Global Constraints ordering requirement):
```csharp
            builder.Register<RoomOrchestrator>(Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces();
            builder.Register<SaveGameLoader>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MapStateTracker>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 2: Verify the project compiles**

Check `read_console` (MCP) or the Console window.
Expected: compile errors about the unassigned `saveSlotListView` serialized field only surface at scene-build time in the Editor (missing reference), not at compile time — confirm no C# compile errors.

- [ ] **Step 3a: Create and populate the `ItemDatabase` asset**

In the Unity Editor:
1. Right-click `Assets/Data/` → `Create` → `CrimsonDraft` → `Inventory` → `Item Database`. Name it `ItemDatabase`.
2. Select the new asset, click the "Populate From Project" button in its Inspector (added in Task 3) to auto-fill `allItems` from every `ItemData` asset in the project.
3. In each navigation scene (e.g. `Deck_B_Development`, and any other scene with its own `NavigationScope`), select the `NavigationScope` GameObject and drag the `ItemDatabase` asset into the `Item Database` field added in Step 1. Save each scene.

- [ ] **Step 3b: Build the `SaveSlotListView` prefab/hierarchy in the target scene**

In the Unity Editor, open the scene that has the `NavigationScope` GameObject (e.g. `Deck_B_Development`):
1. Under the scene's HUD/UI Canvas (the same Canvas that hosts `ContainerView`'s panel — inspect the `ContainerView` GameObject in the Hierarchy to find it), create a new child GameObject `SaveSlotListPanel` with:
   - A child `SlotListParent` (empty `RectTransform`, vertical layout) to hold the row instances.
   - A `SaveSlotRow` prefab (create as its own prefab asset under `Assets/Prefabs/UI/SaveSlotRow.prefab`): a `Button` with a child `TextMeshProUGUI` label, and the `SaveSlotRow` component (Task 8) with `button`/`label` wired to them.
   - A child `ConfirmPanel` (initially inactive) containing a `TextMeshProUGUI` label and two `Button`s ("Yes"/"No").
2. Add a `SaveSlotListView` component to `SaveSlotListPanel`, and wire its serialized fields: `panel` → `SaveSlotListPanel` itself, `slotListParent` → `SlotListParent`, `slotRowPrefab` → the `SaveSlotRow` prefab asset, `confirmPanel` → `ConfirmPanel`, `confirmLabel` → its label, `confirmYesButton`/`confirmNoButton` → its two buttons.
3. Set `SaveSlotListPanel` inactive by default (its own `Hide()` call handles this at runtime, but start inactive in the scene so it isn't visible before first `Show()`).
4. Select the scene's `NavigationScope` GameObject and drag `SaveSlotListPanel` into the new `Save Slot List View` field from Step 1.

- [ ] **Step 4: Place the `SavePointInteractable` prop**

1. Create a prefab `Assets/Prefabs/Interactables/SavePoint.prefab` — the visual "typewriter" prop mesh/placeholder, with a trigger `Collider` on the interactable layer (match the layer used by other interactables, e.g. `ContainerInteractable` — check its `GameObject`'s layer in the Editor) and a `SavePointInteractable` component (Task 10).
2. In each scene room already tagged with a `SaveRoomMarker` component (search the Hierarchy for `SaveRoomMarker` or check `Assets/Scripts/Navigation/Rooms/SaveRoomMarker.cs` usages to find which rooms have it), instantiate the `SavePoint` prefab as a child of that room, positioned at a sensible spot.

- [ ] **Step 5: Enter Play mode and manually verify the save point works**

1. Enter Play mode in the scene from Step 3/4.
2. Walk to the save point, interact with it — the slot list should appear.
3. Click an empty slot, confirm — the confirm dialog should appear and then close.
4. Check `Application.persistentDataPath + "/Saves/slot_00.json"` on disk (or via `read_console`/a temporary log) to confirm a JSON file was written with the expected `roomId`/`sceneName`.
5. Report any issues found back before proceeding — this is a manual verification step with no automated substitute (UI interaction).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs Game/CrimsonDraft/Assets/Prefabs/UI/SaveSlotRow.prefab Game/CrimsonDraft/Assets/Prefabs/UI/SaveSlotRow.prefab.meta Game/CrimsonDraft/Assets/Prefabs/Interactables/SavePoint.prefab Game/CrimsonDraft/Assets/Prefabs/Interactables/SavePoint.prefab.meta
git commit -m "feat(save): wire SaveController/SaveGameLoader into NavigationScope and place save point prop"
```
(Adjust the scene file path(s) to also be staged/committed if they were modified — check with `git status`.)

---

### Task 13: `MainMenuScope` + `MainMenuController` Load Game / New Game wiring

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuController.cs`
- Scene (not code): the `MainMenu` scene

**Interfaces:**
- Consumes: `ISaveGameService`/`IGameStateResetter` (Tasks 4, 5), `SaveSlotListView` (Task 8).
- Produces: a working "Load Game" flow and a "New Game" that resets prior session state.

- [ ] **Step 1: Create `MainMenuScope`**

`Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuScope.cs`:
```csharp
#nullable enable

using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MainMenuController>();
        }
    }
}
```

- [ ] **Step 2: Update `MainMenuController` to consume `ISaveGameService`/`IGameStateResetter`**

Modify `Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuController.cs`:
```csharp
#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string newGameSceneName = "Deck_B_Development";

        [SerializeField] private Button newGameButton  = null!;
        [SerializeField] private Button loadGameButton = null!;
        [SerializeField] private Button exitButton     = null!;
        [SerializeField] private SaveSlotListView loadSlotListView = null!;

        private ISaveGameService   saveGameService   = null!;
        private IGameStateResetter gameStateResetter = null!;

        [Inject]
        public void Construct(ISaveGameService saveGameService, IGameStateResetter gameStateResetter)
        {
            this.saveGameService   = saveGameService;
            this.gameStateResetter = gameStateResetter;

            this.newGameButton.onClick.AddListener(OnNewGameClicked);
            this.loadGameButton.onClick.AddListener(OnLoadGameClicked);
            this.exitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnNewGameClicked()
        {
            this.gameStateResetter.ResetAll();
            SceneManager.LoadScene(this.newGameSceneName, LoadSceneMode.Single);
        }

        private void OnLoadGameClicked()
        {
            this.loadSlotListView.Show(this.saveGameService.ListSlotSummaries(), OnLoadSlotClicked);
        }

        private void OnLoadSlotClicked(SaveSlotSummary summary)
        {
            if (summary.isEmpty) return;
            this.loadSlotListView.ShowConfirm(
                $"Load slot {summary.slot + 1}?",
                () => this.saveGameService.LoadSlot(summary.slot));
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

- [ ] **Step 3: Verify the project compiles**

Check `read_console` (MCP) or the Console window.
Expected: no compile errors. (The `loadGameButton.interactable = false` line from the original file is intentionally removed — the button is now fully functional.)

- [ ] **Step 4: Add `MainMenuScope` and the Load slot-list UI to the `MainMenu` scene**

In the Unity Editor, open the `MainMenu` scene:
1. Select the GameObject holding `MainMenuController` (or its parent). Add a `MainMenuScope` component to it, or to a new empty parent GameObject if `MainMenuController` shouldn't own scope lifecycle directly — either works since `RegisterComponentInHierarchy` scans the whole scene.
2. Build a `SaveSlotListPanel` hierarchy identical in structure to the one built for `NavigationScope` in Task 12 Step 3 (reuse the same `SaveSlotRow` prefab asset from `Assets/Prefabs/UI/SaveSlotRow.prefab`), as a child of the Main Menu's Canvas.
3. Add a `SaveSlotListView` component to it and wire its fields the same way as Task 12 Step 3.
4. Select the `MainMenuController` GameObject and drag the new panel into its `Load Slot List View` field.
5. Confirm the `loadGameButton`'s `interactable` checkbox in the Inspector is checked (it may still be unchecked from the old `Awake()`-time disabling — it's fine either way now since the code no longer disables it at runtime, but for a consistent initial state, check it).

- [ ] **Step 5: Enter Play mode and manually verify Load Game / New Game**

1. Enter Play mode from the `MainMenu` scene (or however the project is normally launched — check `Bootstrapper`/build settings for the actual entry scene).
2. Click "Load Game" — the slot list should appear, reflecting whatever slots were written during Task 12 Step 5's manual test.
3. Click the occupied slot, confirm — the scene from that save should load, and the player should appear at the saved position in the saved room (check via the Scene view / Console logs).
4. Return to the Main Menu, click "New Game" — verify a fresh game starts with default starting inventory (not whatever was saved), confirming `ResetAll()` ran.
5. Report any issues found back before proceeding — this is a manual verification step with no automated substitute (scene loading, UI, and player placement can't be exercised from EditMode tests).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuScope.cs Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/MainMenuController.cs
git commit -m "feat(save): wire Load Game and New Game reset into MainMenuController"
```
(Also stage the `MainMenu` scene file if it was modified — check with `git status` and add its path.)

---

## Post-implementation checklist

- [ ] Full EditMode suite passes (Unity Test Runner or MCP `run_tests`, no filter).
- [ ] Manual playtest: save in Deck B, quit and relaunch the app (not just stop Play mode — a real process restart) from the Main Menu, Load Game, confirm doors/pickups/rooms/inventory/operator HP/player position all match what was saved.
- [ ] Manual playtest: New Game after a previous playthrough shows default starting inventory, not leftover state.
