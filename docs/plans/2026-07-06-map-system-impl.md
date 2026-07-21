# Map System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Implements:** [[Sistema de Mapa]] — `Design/GDD/Sistema de Mapa.md` (source of truth).
**Technical design:** `docs/plans/2026-07-06-map-system-design.md`.

**Goal:** Fullscreen per-deck map screen with fog of war (visited rooms + map item), 3-state door markers, derived room completion, scene-authored polygons baked to ScriptableObjects, rendered via ortho camera → RenderTexture.

**Architecture:** Static geometry lives in `MapData` ScriptableObjects baked from scene components on scene save. Dynamic state lives in global registries (`GameLifetimeScope`), pushed by existing gameplay systems at the moment things happen. The map screen reads asset + registries only — never the 3D scene.

**Tech Stack:** Unity, VContainer, MessagePipe, UniTask, NUnit (EditMode), Unity Input System.

## Global Constraints

- All files start with `#nullable enable`.
- Serialized fields use `null!`; injected fields are set in `[Inject] Construct(...)` on MonoBehaviours; pure C# services use constructor injection with `[Preserve]`.
- Never call `RegisterMessagePipe()` in child scopes — resolve `MessagePipeOptions` from parent (`NavigationScope` already does this).
- Tests are EditMode only, plain C# fakes, no mocking framework. Run via Unity Test Runner or MCP `run_tests` with `filter`.
- Cast `IInitializable` explicitly when calling `Initialize()` in tests.
- Commit messages in English, conventional commits, **no Co-Authored-By trailers**.
- IDs (`roomId`, `doorId`, `pickupId`, `itemId`) are the existing project IDs — never invent parallel ID systems.
- Room/door map placement uses map-space transforms decoupled from 3D transforms.
- Registry states are monotonic: `Visited` never reverts; `Locked` never overwrites `Unlocked`.

## File Structure

```
Assets/Scripts/Infrastructure/
  DoorStateRegistry.cs        (MODIFY: bool → DoorMapState tri-state)
  RoomStateRegistry.cs        (NEW: visited rooms)
  KnownMapsRegistry.cs        (NEW: owned deck plans)
  GameLifetimeScope.cs        (MODIFY: register new registries)
  Map/MapData.cs              (NEW: per-deck SO — geometry + IDs)
  Map/MapDataSet.cs           (NEW: ordered list of all decks)
  Map/MapStateResolver.cs     (NEW: pure derived-state logic)
  Map/PolygonTriangulator.cs  (NEW: pure ear-clipping triangulation)
Assets/Scripts/Navigation/
  Rooms/IRoomOrchestrator.cs  (MODIFY: expose CurrentRoom)
  Rooms/RoomOrchestrator.cs   (MODIFY: expose CurrentRoom)
  Interactables/RoomDoorInteractable.cs   (MODIFY: MarkLocked + unlock-on-cross)
  Interactables/SceneDoorInteractable.cs  (MODIFY: same)
  Interactables/PickupInteractable.cs     (MODIFY: expose PickupId)
  Interactables/IDoorInteractable.cs      (MODIFY if needed: DoorId getter)
  Map/MapStateTracker.cs      (NEW: marks rooms visited from events)
  Map/MapPickupInteractable.cs (NEW: pickup that also registers known map)
  Map/MapRoomShape.cs         (NEW: authored polygon on RoomController)
  Map/MapDoorMarker.cs        (NEW: authored door mark on door interactable)
  Map/MapSceneConfig.cs       (NEW: scene → MapData binding)
  Map/MapRenderer.cs          (NEW: mesh gen + ortho cam + RenderTexture)
  UI/MapScreenView.cs         (NEW: canvas view)
  UI/MapScreenController.cs   (NEW: open/close/pan/deck-cycle)
  NavigationScope.cs          (MODIFY: registrations + cached map pickups)
Assets/Scripts/Navigation/Editor/
  MapRoomShapeEditor.cs       (NEW: SceneView polygon handles + trace)
  MapBaker.cs                 (NEW: bake on scene save / play mode)
  MapEditorWindow.cs          (NEW: 2D grid layout window)
Assets/Tests/EditMode/
  DoorStateRegistryTests.cs   (MODIFY)
  RoomDoorInteractableTests.cs (MODIFY: fake + new tests)
  SceneDoorInteractableTests.cs (MODIFY: fake if it implements IRoomOrchestrator)
  RoomStateRegistryTests.cs   (NEW)
  KnownMapsRegistryTests.cs   (NEW)
  MapStateResolverTests.cs    (NEW)
  MapStateTrackerTests.cs     (NEW)
  PolygonTriangulatorTests.cs (NEW)
```

---

### Task 1: Extend DoorStateRegistry to tri-state

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `enum DoorMapState { Unknown = 0, Locked = 1, Unlocked = 2 }` (namespace `CrimsonDraft.Infrastructure`); registry API `bool IsUnlocked(string)`, `void SetUnlocked(string)`, `void MarkLocked(string)`, `DoorMapState GetMapState(string)`, `IReadOnlyDictionary<string, DoorMapState> GetState()`, `void LoadState(IReadOnlyDictionary<string, DoorMapState>)`.

- [ ] **Step 1: Extend the test file with failing tests (keep passing ones, update LoadState test types)**

Replace the full contents of `DoorStateRegistryTests.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class DoorStateRegistryTests
    {
        [Test]
        public void IsUnlocked_whenNeverSet_returnsFalse()
        {
            var registry = new DoorStateRegistry();
            Assert.IsFalse(registry.IsUnlocked("any-door"));
        }

        [Test]
        public void SetUnlocked_thenIsUnlocked_returnsTrue()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsTrue(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void SetUnlocked_doesNotAffectOtherDoors()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsFalse(registry.IsUnlocked("door-b"));
        }

        [Test]
        public void GetMapState_whenNeverSet_returnsUnknown()
        {
            var registry = new DoorStateRegistry();
            Assert.AreEqual(DoorMapState.Unknown, registry.GetMapState("door-a"));
        }

        [Test]
        public void MarkLocked_fromUnknown_setsLocked()
        {
            var registry = new DoorStateRegistry();
            registry.MarkLocked("door-a");
            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-a"));
            Assert.IsFalse(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void MarkLocked_afterUnlocked_doesNotDowngrade()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            registry.MarkLocked("door-a");
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-a"));
            Assert.IsTrue(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void SetUnlocked_afterLocked_upgradesToUnlocked()
        {
            var registry = new DoorStateRegistry();
            registry.MarkLocked("door-a");
            registry.SetUnlocked("door-a");
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-a"));
        }

        [Test]
        public void LoadState_restoresGivenState()
        {
            var registry = new DoorStateRegistry();
            registry.LoadState(new Dictionary<string, DoorMapState>
            {
                ["door-x"] = DoorMapState.Unlocked,
                ["door-y"] = DoorMapState.Locked,
            });
            Assert.IsTrue(registry.IsUnlocked("door-x"));
            Assert.IsFalse(registry.IsUnlocked("door-y"));
            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-y"));
        }

        [Test]
        public void GetState_reflectsSetUnlockedCalls()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsTrue(registry.GetState().ContainsKey("door-a"));
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetState()["door-a"]);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the new ones fail**

Run Unity Test Runner (or MCP `run_tests`) with filter `DoorStateRegistryTests`.
Expected: compile error (`DoorMapState` not defined) — that is the failing state for this step.

- [ ] **Step 3: Implement the tri-state registry**

Replace the full contents of `DoorStateRegistry.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public enum DoorMapState
    {
        Unknown  = 0,
        Locked   = 1,
        Unlocked = 2,
    }

    public sealed class DoorStateRegistry
    {
        private readonly Dictionary<string, DoorMapState> state = new();

        [Preserve]
        public DoorStateRegistry() { }

        public bool IsUnlocked(string doorId)
            => this.state.TryGetValue(doorId, out var v) && v == DoorMapState.Unlocked;

        public void SetUnlocked(string doorId)
            => this.state[doorId] = DoorMapState.Unlocked;

        /// <summary>Records a failed open attempt. Never downgrades an unlocked door.</summary>
        public void MarkLocked(string doorId)
        {
            if (GetMapState(doorId) == DoorMapState.Unknown)
                this.state[doorId] = DoorMapState.Locked;
        }

        public DoorMapState GetMapState(string doorId)
            => this.state.TryGetValue(doorId, out var v) ? v : DoorMapState.Unknown;

        public IReadOnlyDictionary<string, DoorMapState> GetState() => this.state;

        public void LoadState(IReadOnlyDictionary<string, DoorMapState> saved)
        {
            this.state.Clear();
            foreach (var (k, v) in saved)
                this.state[k] = v;
        }
    }
}
```

- [ ] **Step 4: Run all EditMode tests**

Filter: none (full suite) — `SetUnlocked`/`IsUnlocked` keep their signatures so `RoomDoorInteractableTests` and `SceneDoorInteractableTests` must still pass.
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs"
git commit -m "feat(map): extend DoorStateRegistry to tri-state Unknown/Locked/Unlocked"
```

---

### Task 2: Door interactables push map state

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SceneDoorInteractable.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs`

**Interfaces:**
- Consumes: `DoorStateRegistry.MarkLocked(string)` / `SetUnlocked(string)` from Task 1.
- Produces: behavior only (GDD table "Actualización de estados"): crossing an open door → `Unlocked`; failed attempt without key → `Locked`.

- [ ] **Step 1: Add failing tests to `RoomDoorInteractableTests.cs`**

Append inside the class (uses existing helpers `MakeDoor`, `MakeUnlockedDoor`, `MakeLockedDoor`, `MakeContext`, fakes):

```csharp
[Test]
public void Interact_whenNotLocked_marksDoorUnlockedInRegistry()
{
    var registry     = new DoorStateRegistry();
    var data         = MakeUnlockedDoor();
    var destination  = MakeRoom();
    var prefab       = new GameObject("DoorPrefab");
    var orchestrator = new FakeOrchestrator();
    var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

    door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

    Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-1"),
        "crossing an open door must mark it Unlocked on the map");

    UnityEngine.Object.DestroyImmediate(door.gameObject);
    UnityEngine.Object.DestroyImmediate(destination.gameObject);
    UnityEngine.Object.DestroyImmediate(prefab);
}

[Test]
public void Interact_whenLockedNoKey_marksDoorLockedInRegistry()
{
    var registry     = new DoorStateRegistry();
    var data         = MakeLockedDoor("door_locked");
    var destination  = MakeRoom();
    var prefab       = new GameObject("DoorPrefab");
    var orchestrator = new FakeOrchestrator();
    var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

    door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

    Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-1"),
        "a failed attempt without key must mark the door Locked on the map");

    UnityEngine.Object.DestroyImmediate(door.gameObject);
    UnityEngine.Object.DestroyImmediate(destination.gameObject);
    UnityEngine.Object.DestroyImmediate(prefab);
}

[Test]
public void Interact_whenLockedKeyNotFound_marksDoorLockedInRegistry()
{
    var registry     = new DoorStateRegistry();
    var keyData      = MakeKeyItem("key-1", "Key 1");
    var data         = MakeLockedDoor("door_locked", keyData);
    var destination  = MakeRoom();
    var prefab       = new GameObject("DoorPrefab");
    var orchestrator = new FakeOrchestrator();
    var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1) };
    var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

    door.Interact(MakeContext(new FakeDialogue(), inventory));

    Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-1"));

    UnityEngine.Object.DestroyImmediate(door.gameObject);
    UnityEngine.Object.DestroyImmediate(destination.gameObject);
    UnityEngine.Object.DestroyImmediate(prefab);
}
```

- [ ] **Step 2: Run filter `RoomDoorInteractableTests` — expect the 3 new tests FAIL** (registry stays `Unknown`).

- [ ] **Step 3: Implement in `RoomDoorInteractable.Interact`**

Modify the beginning of `Interact` and the no-key / key-not-found branches:

```csharp
public void Interact(InteractionContext context)
{
    if (!this.data.Locked || this.unlocked)
    {
        this.registry.SetUnlocked(this.doorId); // crossing an open door reveals it as Unlocked on the map
        this.roomOrchestrator
            .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
            .Forget();
        return;
    }

    var keyItem = this.data.KeyItem;

    if (keyItem == null)
    {
        this.registry.MarkLocked(this.doorId);
        context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
        return;
    }

    var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

    switch (outcome.Result)
    {
        case KeyUseResult.NotFound:
        case KeyUseResult.AlreadyDepleted:
            this.registry.MarkLocked(this.doorId);
            context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
            break;
        // KeyUseResult.Success and DepletedAfterUse branches: unchanged
        // (they already call this.registry.SetUnlocked(this.doorId) in onComplete)
```

Leave the `Success`/`DepletedAfterUse` branches exactly as they are.

- [ ] **Step 4: Apply the same two changes to `SceneDoorInteractable.Interact`**

Same pattern: in the `!Locked || unlocked` branch add `this.registry.SetUnlocked(this.doorId);` before `Transition();`; in the `keyItem == null` branch and the `NotFound`/`AlreadyDepleted` cases add `this.registry.MarkLocked(this.doorId);` before starting the dialogue.

- [ ] **Step 5: Run filters `RoomDoorInteractableTests` and `SceneDoorInteractableTests`** — expect all PASS.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SceneDoorInteractable.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs"
git commit -m "feat(map): door interactables record Locked/Unlocked map state on interaction"
```

---

### Task 3: RoomStateRegistry and KnownMapsRegistry

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/RoomStateRegistry.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/KnownMapsRegistry.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomStateRegistryTests.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/KnownMapsRegistryTests.cs`

**Interfaces:**
- Produces:
  - `RoomStateRegistry`: `bool IsVisited(string roomId)`, `void MarkVisited(string roomId)`, `IReadOnlyCollection<string> GetState()`, `void LoadState(IEnumerable<string>)`.
  - `KnownMapsRegistry`: `bool IsKnown(string mapId)`, `void SetKnown(string mapId)`, `IReadOnlyCollection<string> GetState()`, `void LoadState(IEnumerable<string>)`. `mapId` is the `MapData.SceneName` (unique per deck).

- [ ] **Step 1: Write failing tests**

`RoomStateRegistryTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RoomStateRegistryTests
    {
        [Test]
        public void IsVisited_whenNeverMarked_returnsFalse()
        {
            var registry = new RoomStateRegistry();
            Assert.IsFalse(registry.IsVisited("room-a"));
        }

        [Test]
        public void MarkVisited_thenIsVisited_returnsTrue()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("room-a");
            Assert.IsTrue(registry.IsVisited("room-a"));
            Assert.IsFalse(registry.IsVisited("room-b"));
        }

        [Test]
        public void MarkVisited_isIdempotent()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("room-a");
            registry.MarkVisited("room-a");
            Assert.AreEqual(1, registry.GetState().Count);
        }

        [Test]
        public void LoadState_restoresGivenRooms()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("stale");
            registry.LoadState(new[] { "room-x" });
            Assert.IsTrue(registry.IsVisited("room-x"));
            Assert.IsFalse(registry.IsVisited("stale"));
        }
    }
}
```

`KnownMapsRegistryTests.cs`: identical shape with `IsKnown`/`SetKnown` and ids `"Deck_B"`, `"Deck_C"`.

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class KnownMapsRegistryTests
    {
        [Test]
        public void IsKnown_whenNeverSet_returnsFalse()
        {
            var registry = new KnownMapsRegistry();
            Assert.IsFalse(registry.IsKnown("Deck_B"));
        }

        [Test]
        public void SetKnown_thenIsKnown_returnsTrue()
        {
            var registry = new KnownMapsRegistry();
            registry.SetKnown("Deck_B");
            Assert.IsTrue(registry.IsKnown("Deck_B"));
            Assert.IsFalse(registry.IsKnown("Deck_C"));
        }

        [Test]
        public void LoadState_restoresGivenMaps()
        {
            var registry = new KnownMapsRegistry();
            registry.SetKnown("stale");
            registry.LoadState(new[] { "Deck_C" });
            Assert.IsTrue(registry.IsKnown("Deck_C"));
            Assert.IsFalse(registry.IsKnown("stale"));
        }
    }
}
```

- [ ] **Step 2: Run filters `RoomStateRegistryTests`, `KnownMapsRegistryTests`** — expect compile failure (types missing).

- [ ] **Step 3: Implement both registries**

`RoomStateRegistry.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>Global, monotonic record of rooms the player has entered. Keyed by RoomController.RoomId.</summary>
    public sealed class RoomStateRegistry
    {
        private readonly HashSet<string> visited = new();

        [Preserve]
        public RoomStateRegistry() { }

        public bool IsVisited(string roomId)  => this.visited.Contains(roomId);
        public void MarkVisited(string roomId) => this.visited.Add(roomId);

        public IReadOnlyCollection<string> GetState() => this.visited;

        public void LoadState(IEnumerable<string> saved)
        {
            this.visited.Clear();
            foreach (var id in saved)
                this.visited.Add(id);
        }
    }
}
```

`KnownMapsRegistry.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>Global record of deck plans the player owns. Keyed by MapData.SceneName.</summary>
    public sealed class KnownMapsRegistry
    {
        private readonly HashSet<string> known = new();

        [Preserve]
        public KnownMapsRegistry() { }

        public bool IsKnown(string mapId)  => this.known.Contains(mapId);
        public void SetKnown(string mapId) => this.known.Add(mapId);

        public IReadOnlyCollection<string> GetState() => this.known;

        public void LoadState(IEnumerable<string> saved)
        {
            this.known.Clear();
            foreach (var id in saved)
                this.known.Add(id);
        }
    }
}
```

- [ ] **Step 4: Register in `GameLifetimeScope.Configure`** after the existing registry lines:

```csharp
builder.Register<RoomStateRegistry>(Lifetime.Singleton);
builder.Register<KnownMapsRegistry>(Lifetime.Singleton);
```

- [ ] **Step 5: Run both filters** — expect PASS.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/RoomStateRegistry.cs" "Game/CrimsonDraft/Assets/Scripts/Infrastructure/KnownMapsRegistry.cs" "Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/RoomStateRegistryTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/KnownMapsRegistryTests.cs"
git commit -m "feat(map): add RoomStateRegistry and KnownMapsRegistry global singletons"
```

---

### Task 4: MapData / MapDataSet ScriptableObjects

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapDataSet.cs`

**Interfaces:**
- Produces (namespace `CrimsonDraft.Infrastructure.Map`):
  - `MapElementTransform { Vector2 Offset; float Rotation; Vector2 Scale; float ZOrder; }` (serializable class, public fields)
  - `MapRoomData { string RoomId; Vector2[] Polygon; MapElementTransform Transform; string[] DoorIds; string[] PickupIds; }`
  - `MapDoorData { string DoorId; MapElementTransform Transform; Vector2 Size; }`
  - `MapData` SO: `string SceneName`, `string DisplayName`, `string Abbreviation`, `string MapItemId`, `Vector2Int GridSize`, `float CellSize`, `IReadOnlyList<MapRoomData> Rooms`, `IReadOnlyList<MapDoorData> Doors`, editor-only `EditorSetBakedContent(List<MapRoomData>, List<MapDoorData>)`.
  - `MapDataSet` SO: `MapData[] Maps`.

- [ ] **Step 1: Implement `MapData.cs`**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    [Serializable]
    public class MapElementTransform
    {
        public Vector2 Offset;
        public float   Rotation;
        public Vector2 Scale = Vector2.one;
        public float   ZOrder;
    }

    [Serializable]
    public class MapRoomData
    {
        public string              RoomId    = "";
        public Vector2[]           Polygon   = Array.Empty<Vector2>();
        public MapElementTransform Transform = new();
        public string[]            DoorIds   = Array.Empty<string>();
        public string[]            PickupIds = Array.Empty<string>();
    }

    [Serializable]
    public class MapDoorData
    {
        public string              DoorId    = "";
        public MapElementTransform Transform = new();
        public Vector2             Size      = new(1f, 0.25f);
    }

    /// <summary>Static per-deck map geometry + IDs. Baked from scene components by MapBaker.
    /// Dynamic state (visited/locked/collected) lives in the global registries.</summary>
    [CreateAssetMenu(menuName = "CrimsonDraft/Map/Map Data")]
    public sealed class MapData : ScriptableObject
    {
        [SerializeField] private string     sceneName    = "";
        [SerializeField] private string     displayName  = "";
        [SerializeField] private string     abbreviation = "";
        [SerializeField] private string     mapItemId    = "";
        [SerializeField] private Vector2Int gridSize     = new(25, 25);
        [SerializeField] private float      cellSize     = 1f;

        [SerializeField] private List<MapRoomData> rooms = new();
        [SerializeField] private List<MapDoorData> doors = new();

        public string     SceneName    => this.sceneName;
        public string     DisplayName  => this.displayName;
        public string     Abbreviation => this.abbreviation;
        public string     MapItemId    => this.mapItemId;
        public Vector2Int GridSize     => this.gridSize;
        public float      CellSize     => this.cellSize;

        public IReadOnlyList<MapRoomData> Rooms => this.rooms;
        public IReadOnlyList<MapDoorData> Doors => this.doors;

#if UNITY_EDITOR
        public void EditorSetBakedContent(List<MapRoomData> bakedRooms, List<MapDoorData> bakedDoors)
        {
            this.rooms = bakedRooms;
            this.doors = bakedDoors;
        }
#endif
    }
}
```

- [ ] **Step 2: Implement `MapDataSet.cs`**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Ordered list of every deck map — feeds the deck selector on the map screen.</summary>
    [CreateAssetMenu(menuName = "CrimsonDraft/Map/Map Data Set")]
    public sealed class MapDataSet : ScriptableObject
    {
        [SerializeField] private MapData[] maps = System.Array.Empty<MapData>();

        public MapData[] Maps => this.maps;
    }
}
```

- [ ] **Step 3: Let Unity compile (`refresh_unity` / focus editor), check console for errors** — expect clean.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map"
git commit -m "feat(map): add MapData and MapDataSet ScriptableObjects"
```

---

### Task 5: MapStateResolver (pure derived-state logic)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapStateResolver.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/MapStateResolverTests.cs`

**Interfaces:**
- Consumes: registries (Tasks 1, 3), `MapData`/`MapRoomData` (Task 4), existing `PickupRegistry`.
- Produces (namespace `CrimsonDraft.Infrastructure.Map`):
  - `enum MapRoomDisplayState { Hidden, NotVisited, Visited, Completed }`
  - `static class MapStateResolver`:
    - `MapRoomDisplayState ResolveRoom(bool visited, bool deckKnown, bool allPickupsCollected)`
    - `MapRoomDisplayState ResolveRoom(MapRoomData room, bool deckKnown, RoomStateRegistry rooms, PickupRegistry pickups)`
    - `bool IsDeckKnown(MapData map, RoomStateRegistry rooms, KnownMapsRegistry knownMaps)`

- [ ] **Step 1: Write failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapStateResolverTests
    {
        // ── pure core ────────────────────────────────────────────────────────

        [Test]
        public void ResolveRoom_unknownRoom_withoutMapItem_isHidden()
            => Assert.AreEqual(MapRoomDisplayState.Hidden,
                MapStateResolver.ResolveRoom(visited: false, deckKnown: false, allPickupsCollected: false));

        [Test]
        public void ResolveRoom_unknownRoom_withMapItem_isNotVisited()
            => Assert.AreEqual(MapRoomDisplayState.NotVisited,
                MapStateResolver.ResolveRoom(visited: false, deckKnown: true, allPickupsCollected: false));

        [Test]
        public void ResolveRoom_visited_withPendingPickups_isVisited()
            => Assert.AreEqual(MapRoomDisplayState.Visited,
                MapStateResolver.ResolveRoom(visited: true, deckKnown: false, allPickupsCollected: false));

        [Test]
        public void ResolveRoom_visited_allPickupsCollected_isCompleted()
            => Assert.AreEqual(MapRoomDisplayState.Completed,
                MapStateResolver.ResolveRoom(visited: true, deckKnown: true, allPickupsCollected: true));

        [Test]
        public void ResolveRoom_notVisited_allPickupsCollected_isNotCompleted()
            => Assert.AreEqual(MapRoomDisplayState.NotVisited,
                MapStateResolver.ResolveRoom(visited: false, deckKnown: true, allPickupsCollected: true),
                "Completed requires Visited — an unvisited room can never show as completed");

        // ── registry-backed overload ─────────────────────────────────────────

        [Test]
        public void ResolveRoom_registryOverload_derivesCompletedFromPickupRegistry()
        {
            var room = new MapRoomData
            {
                RoomId    = "room-a",
                PickupIds = new[] { "p1", "p2" },
            };
            var rooms   = new RoomStateRegistry();
            var pickups = new PickupRegistry();
            rooms.MarkVisited("room-a");
            pickups.SetCollected("p1");

            Assert.AreEqual(MapRoomDisplayState.Visited,
                MapStateResolver.ResolveRoom(room, deckKnown: false, rooms, pickups));

            pickups.SetCollected("p2");

            Assert.AreEqual(MapRoomDisplayState.Completed,
                MapStateResolver.ResolveRoom(room, deckKnown: false, rooms, pickups));
        }

        [Test]
        public void ResolveRoom_roomWithNoPickups_visited_isCompleted()
        {
            var room    = new MapRoomData { RoomId = "room-a" };
            var rooms   = new RoomStateRegistry();
            var pickups = new PickupRegistry();
            rooms.MarkVisited("room-a");

            Assert.AreEqual(MapRoomDisplayState.Completed,
                MapStateResolver.ResolveRoom(room, deckKnown: false, rooms, pickups),
                "a visited room with nothing to collect counts as completed");
        }

        // ── deck known ───────────────────────────────────────────────────────

        [Test]
        public void IsDeckKnown_trueWhenMapItemOwned()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            SetPrivate(map, "sceneName", "Deck_B");
            var knownMaps = new KnownMapsRegistry();
            knownMaps.SetKnown("Deck_B");

            Assert.IsTrue(MapStateResolver.IsDeckKnown(map, new RoomStateRegistry(), knownMaps));
        }

        [Test]
        public void IsDeckKnown_trueWhenAnyRoomVisited()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            SetPrivate(map, "sceneName", "Deck_B");
#if UNITY_EDITOR
            map.EditorSetBakedContent(
                new System.Collections.Generic.List<MapRoomData> { new() { RoomId = "room-a" } },
                new System.Collections.Generic.List<MapDoorData>());
#endif
            var rooms = new RoomStateRegistry();
            rooms.MarkVisited("room-a");

            Assert.IsTrue(MapStateResolver.IsDeckKnown(map, rooms, new KnownMapsRegistry()));
        }

        [Test]
        public void IsDeckKnown_falseWhenNothingKnown()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            Assert.IsFalse(MapStateResolver.IsDeckKnown(map, new RoomStateRegistry(), new KnownMapsRegistry()));
        }

        private static void SetPrivate(Object target, string field, string value)
        {
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

- [ ] **Step 2: Run filter `MapStateResolverTests`** — expect compile failure.

- [ ] **Step 3: Implement**

```csharp
#nullable enable

namespace CrimsonDraft.Infrastructure.Map
{
    public enum MapRoomDisplayState
    {
        Hidden,
        NotVisited,
        Visited,
        Completed,
    }

    /// <summary>Pure derivation of display states from persisted registries.
    /// NotVisited and Completed are derived at draw time — never stored (GDD rule).</summary>
    public static class MapStateResolver
    {
        public static MapRoomDisplayState ResolveRoom(bool visited, bool deckKnown, bool allPickupsCollected)
        {
            if (visited)
                return allPickupsCollected ? MapRoomDisplayState.Completed : MapRoomDisplayState.Visited;

            return deckKnown ? MapRoomDisplayState.NotVisited : MapRoomDisplayState.Hidden;
        }

        public static MapRoomDisplayState ResolveRoom(
            MapRoomData room, bool deckKnown, RoomStateRegistry rooms, PickupRegistry pickups)
        {
            bool visited = rooms.IsVisited(room.RoomId);

            bool allCollected = true;
            foreach (var pickupId in room.PickupIds)
            {
                if (!pickups.IsCollected(pickupId))
                {
                    allCollected = false;
                    break;
                }
            }

            return ResolveRoom(visited, deckKnown, allCollected);
        }

        public static bool IsDeckKnown(MapData map, RoomStateRegistry rooms, KnownMapsRegistry knownMaps)
        {
            if (knownMaps.IsKnown(map.SceneName))
                return true;

            foreach (var room in map.Rooms)
            {
                if (rooms.IsVisited(room.RoomId))
                    return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run filter `MapStateResolverTests`** — expect PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapStateResolver.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/MapStateResolverTests.cs"
git commit -m "feat(map): add MapStateResolver for derived room display states"
```

---

### Task 6: Expose CurrentRoom on IRoomOrchestrator

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs` (FakeOrchestrator)
- Modify: any other test fake implementing `IRoomOrchestrator` (search `: IRoomOrchestrator` under `Assets/Tests`)

**Interfaces:**
- Produces: `RoomController? CurrentRoom { get; }` on `IRoomOrchestrator` — used by `MapStateTracker` (Task 7) and `MapScreenController` (Task 12).

- [ ] **Step 1: Add to the interface**

```csharp
public interface IRoomOrchestrator
{
    RoomController? CurrentRoom { get; }
    UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab);
}
```

- [ ] **Step 2: Implement in `RoomOrchestrator`**

The private field `currentRoom` already exists and is set in `Initialize` and at the end of `TransitionToRoomAsync`. Add the property right below the field declarations:

```csharp
public RoomController? CurrentRoom => this.currentRoom;
```

- [ ] **Step 3: Update every test fake implementing `IRoomOrchestrator`**

In `RoomDoorInteractableTests.FakeOrchestrator` (and equivalents found by searching), add:

```csharp
public RoomController? CurrentRoom { get; set; }
```

- [ ] **Step 4: Run full EditMode suite** — expect all PASS (change is additive).

- [ ] **Step 5: Commit**

```bash
git add -A "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms" "Game/CrimsonDraft/Assets/Tests/EditMode"
git commit -m "feat(map): expose CurrentRoom on IRoomOrchestrator"
```

---

### Task 7: MapStateTracker marks rooms visited

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapStateTracker.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/MapStateTrackerTests.cs`

**Interfaces:**
- Consumes: `IRoomOrchestrator.CurrentRoom` (Task 6), `RoomStateRegistry` (Task 3), `ISubscriber<RoomTransitionedEvent>` (existing).
- Produces: `MapStateTracker : IInitializable, IDisposable` — no public API beyond lifecycle; registered as entry point.

- [ ] **Step 1: Write failing tests**

```csharp
#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class MapStateTrackerTests
    {
        private static RoomController MakeRoom(string roomId)
        {
            var room = new GameObject("Room_" + roomId).AddComponent<RoomController>();
            var so   = new UnityEditor.SerializedObject(room);
            so.FindProperty("roomId").stringValue = roomId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        [Test]
        public void Initialize_marksCurrentRoomVisited()
        {
            var room         = MakeRoom("room-start");
            var registry     = new RoomStateRegistry();
            var orchestrator = new FakeOrchestrator { CurrentRoom = room };
            var subscriber   = new FakeSubscriber();
            var tracker      = new MapStateTracker(orchestrator, subscriber, registry);

            ((IInitializable)tracker).Initialize();

            Assert.IsTrue(registry.IsVisited("room-start"));

            UnityEngine.Object.DestroyImmediate(room.gameObject);
        }

        [Test]
        public void RoomTransitionedEvent_marksNewRoomVisited()
        {
            var start        = MakeRoom("room-start");
            var next         = MakeRoom("room-next");
            var registry     = new RoomStateRegistry();
            var orchestrator = new FakeOrchestrator { CurrentRoom = start };
            var subscriber   = new FakeSubscriber();
            var tracker      = new MapStateTracker(orchestrator, subscriber, registry);

            ((IInitializable)tracker).Initialize();
            subscriber.Publish(new RoomTransitionedEvent(next));

            Assert.IsTrue(registry.IsVisited("room-next"));

            UnityEngine.Object.DestroyImmediate(start.gameObject);
            UnityEngine.Object.DestroyImmediate(next.gameObject);
        }

        [Test]
        public void Initialize_withNoCurrentRoom_doesNotThrow()
        {
            var tracker = new MapStateTracker(new FakeOrchestrator(), new FakeSubscriber(), new RoomStateRegistry());
            Assert.DoesNotThrow(() => ((IInitializable)tracker).Initialize());
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeOrchestrator : IRoomOrchestrator
        {
            public RoomController? CurrentRoom { get; set; }
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
                => UniTask.CompletedTask;
        }

        private sealed class FakeSubscriber : ISubscriber<RoomTransitionedEvent>
        {
            private IMessageHandler<RoomTransitionedEvent>? handler;

            public IDisposable Subscribe(
                IMessageHandler<RoomTransitionedEvent> messageHandler,
                params MessageHandlerFilter<RoomTransitionedEvent>[] filters)
            {
                this.handler = messageHandler;
                return new DummyDisposable();
            }

            public void Publish(RoomTransitionedEvent evt) => this.handler?.Handle(evt);

            private sealed class DummyDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
```

- [ ] **Step 2: Run filter `MapStateTrackerTests`** — expect compile failure.

- [ ] **Step 3: Implement `MapStateTracker.cs`**

```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Listens to room transitions and records visited rooms in the global registry.
    /// The map screen never watches the scene — this tracker pushes state as it happens.</summary>
    public sealed class MapStateTracker : IInitializable, IDisposable
    {
        private readonly IRoomOrchestrator                  orchestrator;
        private readonly ISubscriber<RoomTransitionedEvent> roomTransitioned;
        private readonly RoomStateRegistry                  registry;

        private IDisposable? subscription;

        [Preserve]
        public MapStateTracker(
            IRoomOrchestrator                  orchestrator,
            ISubscriber<RoomTransitionedEvent> roomTransitioned,
            RoomStateRegistry                  registry)
        {
            this.orchestrator     = orchestrator;
            this.roomTransitioned = roomTransitioned;
            this.registry         = registry;
        }

        void IInitializable.Initialize()
        {
            var current = this.orchestrator.CurrentRoom;
            if (current != null && !string.IsNullOrEmpty(current.RoomId))
                this.registry.MarkVisited(current.RoomId);

            this.subscription = this.roomTransitioned.Subscribe(evt =>
            {
                if (!string.IsNullOrEmpty(evt.ActiveRoom.RoomId))
                    this.registry.MarkVisited(evt.ActiveRoom.RoomId);
            });
        }

        public void Dispose() => this.subscription?.Dispose();
    }
}
```

- [ ] **Step 4: Register in `NavigationScope.Configure`**, immediately **after** the `RoomOrchestrator` registration (order matters — `IInitializable`s run in registration order and the tracker reads `CurrentRoom`):

```csharp
builder.Register<MapStateTracker>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 5: Run filter `MapStateTrackerTests`** — expect PASS. (Note: the lambda `Subscribe(Action<T>)` extension requires `MessagePipe` — if the fake's `IMessageHandler` path doesn't match the extension, subscribe with `.Subscribe(evt => ...)` still compiles against `ISubscriber<T>` via GlobalMessagePipe extensions; if not, wrap: implement handler class. Adjust the fake or use `MessageHandlerFilter<T>` overload accordingly until green.)

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapStateTracker.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/MapStateTrackerTests.cs"
git commit -m "feat(map): track visited rooms via RoomTransitionedEvent"
```

---

### Task 8: Map item pickup registers known deck

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PickupInteractable.cs` (expose `PickupId`)
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapPickupInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

**Interfaces:**
- Consumes: `KnownMapsRegistry` (Task 3), `MapData.SceneName` (Task 4), existing `PickupRegistry`, `IPickupDialogueService`, `IInventoryService`.
- Produces: `PickupInteractable.PickupId` public getter (used by MapBaker, Task 10); `MapPickupInteractable : MonoBehaviour, IInteractable` with `Construct(PickupRegistry, KnownMapsRegistry)`.

- [ ] **Step 1: Add getter to `PickupInteractable`** below the serialized fields:

```csharp
public string PickupId => this.pickupId;
```

- [ ] **Step 2: Implement `MapPickupInteractable.cs`** (same dialogue flow as `PickupInteractable`, plus known-map registration):

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>A pickup for a deck plan item: on collection it also marks the deck
    /// as known so its unvisited rooms start drawing on the map (GDD: fog of war).</summary>
    public sealed class MapPickupInteractable : MonoBehaviour, IInteractable
    {
        private const string PromptNode = "pickup_prompt";

        [SerializeField] private string   pickupId = null!;
        [SerializeField] private ItemData item     = null!;
        [SerializeField] private MapData  map      = null!;

        private PickupRegistry    pickupRegistry = null!;
        private KnownMapsRegistry knownMaps      = null!;

        public string PickupId => this.pickupId;

        [Inject]
        public void Construct(PickupRegistry registry, KnownMapsRegistry knownMaps)
        {
            this.pickupRegistry = registry;
            this.knownMaps      = knownMaps;
            if (registry.IsCollected(this.pickupId))
                gameObject.SetActive(false);
        }

        public void Interact(InteractionContext context)
        {
            bool pickupSucceeded = false;
            string itemName = !string.IsNullOrEmpty(this.item.SecondaryName)
                ? this.item.SecondaryName
                : this.item.DisplayName;

            context.PickupDialogueService.StartDialogue(
                PromptNode,
                variables: new Dictionary<string, object>
                {
                    ["$item_name"]      = itemName,
                    ["$pickup_success"] = true,
                },
                onComplete: () =>
                {
                    if (!pickupSucceeded) return;
                    this.pickupRegistry.SetCollected(this.pickupId);
                    this.knownMaps.SetKnown(this.map.SceneName);
                    gameObject.SetActive(false);
                },
                commands: new Dictionary<string, Action>
                {
                    ["try_pickup"] = () =>
                    {
                        pickupSucceeded = context.InventoryService.AddItemAuto(this.item);
                        context.PickupDialogueService.SetVariable("$pickup_success", pickupSucceeded);
                    }
                });
        }
    }
}
```

- [ ] **Step 3: Wire construction in `NavigationScope`**

Add a cached array field + include in the existing cache button + construct in `PickupBootstrap`-style. Concretely:

```csharp
[SerializeField] private CrimsonDraft.Navigation.Map.MapPickupInteractable[] cachedMapPickups
    = System.Array.Empty<CrimsonDraft.Navigation.Map.MapPickupInteractable>();
```

In `Configure`, after `builder.Register<PickupBootstrap>...`:

```csharp
builder.RegisterInstance(this.cachedMapPickups);
builder.Register<MapPickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
```

In the `CacheScenePickups` editor button, also:

```csharp
this.cachedMapPickups = FindObjectsByType<CrimsonDraft.Navigation.Map.MapPickupInteractable>(
    FindObjectsInactive.Include, FindObjectsSortMode.None);
```

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapPickupBootstrap.cs`:

```csharp
#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Map
{
    public sealed class MapPickupBootstrap : IInitializable
    {
        private readonly PickupRegistry           pickupRegistry;
        private readonly KnownMapsRegistry        knownMaps;
        private readonly MapPickupInteractable[]  pickups;

        [Preserve]
        public MapPickupBootstrap(
            PickupRegistry          pickupRegistry,
            KnownMapsRegistry       knownMaps,
            MapPickupInteractable[] pickups)
        {
            this.pickupRegistry = pickupRegistry;
            this.knownMaps      = knownMaps;
            this.pickups        = pickups;
        }

        void IInitializable.Initialize()
        {
            foreach (var pickup in this.pickups)
                pickup.Construct(this.pickupRegistry, this.knownMaps);
        }
    }
}
```

- [ ] **Step 4: Compile check + run full EditMode suite** — expect clean/PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapPickupInteractable.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapPickupBootstrap.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PickupInteractable.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(map): map plan pickup registers known deck"
```

---

### Task 9: Scene authoring components (MapRoomShape, MapDoorMarker, MapSceneConfig)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapRoomShape.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapDoorMarker.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapSceneConfig.cs`
- Modify (only if `DoorId` missing): `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IDoorInteractable.cs`

**Interfaces:**
- Produces (namespace `CrimsonDraft.Navigation.Map`):
  - `MapRoomShape : MonoBehaviour` — `[RequireComponent(typeof(RoomController))]`; `Vector2[] LocalPoints`, `MapElementTransform` fields (`Vector2 MapOffset`, `float MapRotation`, `Vector2 MapScale`, `float ZOrder`); `RoomController Room` getter.
  - `MapDoorMarker : MonoBehaviour` — `Vector2 MapOffset`, `float MapRotation`, `Vector2 Size`; `string? ResolveDoorId()` from sibling `IDoorInteractable`.
  - `MapSceneConfig : MonoBehaviour` — `MapData Map` (the bake target for this scene).

- [ ] **Step 1: Verify `IDoorInteractable` declares `string DoorId { get; }`** — if not, add it (both `RoomDoorInteractable` and `SceneDoorInteractable` already have the property, so adding to the interface is non-breaking).

- [ ] **Step 2: Implement `MapRoomShape.cs`**

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Authored 2D silhouette of a room. Points are in the room's local XZ space,
    /// traced over the real floor in SceneView. Map placement (offset/rotation/scale)
    /// is decoupled from the 3D transform and arranged in the MapEditorWindow.</summary>
    [RequireComponent(typeof(RoomController))]
    public sealed class MapRoomShape : MonoBehaviour
    {
        [SerializeField] private Vector2[] localPoints = System.Array.Empty<Vector2>();

        [Header("Map-space placement")]
        [SerializeField] private Vector2 mapOffset;
        [SerializeField] private float   mapRotation;
        [SerializeField] private Vector2 mapScale = Vector2.one;
        [SerializeField] private float   zOrder;

        public Vector2[] LocalPoints { get => this.localPoints; set => this.localPoints = value; }
        public Vector2   MapOffset   { get => this.mapOffset;   set => this.mapOffset   = value; }
        public float     MapRotation { get => this.mapRotation; set => this.mapRotation = value; }
        public Vector2   MapScale    { get => this.mapScale;    set => this.mapScale    = value; }
        public float     ZOrder      { get => this.zOrder;      set => this.zOrder      = value; }

        public RoomController Room => GetComponent<RoomController>();
    }
}
```

- [ ] **Step 3: Implement `MapDoorMarker.cs`**

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Marks where a door draws on the map. Lives on the same GameObject as the
    /// door interactable — the doorId is always read from it, never typed by hand.</summary>
    public sealed class MapDoorMarker : MonoBehaviour
    {
        [Header("Map-space placement")]
        [SerializeField] private Vector2 mapOffset;
        [SerializeField] private float   mapRotation;
        [SerializeField] private Vector2 size = new(1f, 0.25f);

        public Vector2 MapOffset   { get => this.mapOffset;   set => this.mapOffset   = value; }
        public float   MapRotation { get => this.mapRotation; set => this.mapRotation = value; }
        public Vector2 Size        { get => this.size;        set => this.size        = value; }

        public string? ResolveDoorId()
            => GetComponent<IDoorInteractable>()?.DoorId;
    }
}
```

- [ ] **Step 4: Implement `MapSceneConfig.cs`**

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Scene-level binding: which MapData asset this scene bakes into.
    /// Place one on a root GameObject of each deck scene.</summary>
    public sealed class MapSceneConfig : MonoBehaviour
    {
        [SerializeField] private MapData map = null!;

        public MapData Map => this.map;
    }
}
```

- [ ] **Step 5: Compile check** — expect clean console.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Map" "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IDoorInteractable.cs"
git commit -m "feat(map): scene authoring components for map shapes and door markers"
```

---

### Task 10: MapBaker (bake on scene save)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapBaker.cs`

**Interfaces:**
- Consumes: `MapSceneConfig`, `MapRoomShape`, `MapDoorMarker` (Task 9), `MapData.EditorSetBakedContent` (Task 4), `PickupInteractable.PickupId` / `MapPickupInteractable.PickupId` (Task 8), `RoomDoorInteractable.Destination` (existing).
- Produces: automatic bake on `EditorSceneManager.sceneSaved` and on entering Play Mode; `public static void Bake(MapSceneConfig config)` for the editor window's manual button.

- [ ] **Step 1: Implement `MapBaker.cs`** (editor assembly — lives in the existing `Navigation/Editor` folder):

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Bakes scene map authoring (MapRoomShape/MapDoorMarker) into the scene's
    /// MapData asset on every scene save and on entering Play Mode. There is no manual
    /// export step — the asset can never drift from the scene (GDD: horneado automático).</summary>
    [InitializeOnLoad]
    public static class MapBaker
    {
        static MapBaker()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnSceneSaved(Scene scene) => BakeAllInOpenScenes();

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                BakeAllInOpenScenes();
        }

        private static void BakeAllInOpenScenes()
        {
            foreach (var config in Object.FindObjectsByType<MapSceneConfig>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (config.Map == null)
                {
                    Debug.LogWarning("[MapBaker] MapSceneConfig has no MapData assigned.", config);
                    continue;
                }
                Bake(config);
            }
        }

        public static void Bake(MapSceneConfig config)
        {
            var rooms = new List<MapRoomData>();
            var doors = new List<MapDoorData>();

            var markers = Object.FindObjectsByType<MapDoorMarker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // doorId → marker, and room → linked doorIds (parent room + destination room)
            var doorIdsByRoom = new Dictionary<RoomController, List<string>>();

            foreach (var marker in markers)
            {
                var doorId = marker.ResolveDoorId();
                if (string.IsNullOrEmpty(doorId))
                {
                    Debug.LogWarning("[MapBaker] MapDoorMarker without a door interactable or empty doorId.", marker);
                    continue;
                }

                doors.Add(new MapDoorData
                {
                    DoorId    = doorId!,
                    Size      = marker.Size,
                    Transform = new MapElementTransform
                    {
                        Offset   = marker.MapOffset,
                        Rotation = marker.MapRotation,
                        Scale    = Vector2.one,
                    },
                });

                var parentRoom = marker.GetComponentInParent<RoomController>(true);
                if (parentRoom != null)
                    Link(doorIdsByRoom, parentRoom, doorId!);

                var roomDoor = marker.GetComponent<RoomDoorInteractable>();
                if (roomDoor != null && roomDoor.Destination != null)
                    Link(doorIdsByRoom, roomDoor.Destination, doorId!);
            }

            foreach (var shape in Object.FindObjectsByType<MapRoomShape>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var room = shape.Room;

                if (string.IsNullOrEmpty(room.RoomId))
                {
                    Debug.LogWarning($"[MapBaker] Room '{room.name}' has an empty roomId — skipped.", room);
                    continue;
                }
                if (shape.LocalPoints.Length < 3)
                {
                    Debug.LogWarning($"[MapBaker] Room '{room.name}' shape has fewer than 3 points — skipped.", shape);
                    continue;
                }

                var pickupIds = new List<string>();
                foreach (var pickup in room.GetComponentsInChildren<PickupInteractable>(true))
                {
                    if (!string.IsNullOrEmpty(pickup.PickupId))
                        pickupIds.Add(pickup.PickupId);
                    else
                        Debug.LogWarning($"[MapBaker] Pickup without id in room '{room.name}'.", pickup);
                }
                foreach (var mapPickup in room.GetComponentsInChildren<MapPickupInteractable>(true))
                {
                    if (!string.IsNullOrEmpty(mapPickup.PickupId))
                        pickupIds.Add(mapPickup.PickupId);
                }

                rooms.Add(new MapRoomData
                {
                    RoomId    = room.RoomId,
                    Polygon   = shape.LocalPoints.ToArray(),
                    DoorIds   = doorIdsByRoom.TryGetValue(room, out var ids)
                                    ? ids.ToArray() : System.Array.Empty<string>(),
                    PickupIds = pickupIds.ToArray(),
                    Transform = new MapElementTransform
                    {
                        Offset   = shape.MapOffset,
                        Rotation = shape.MapRotation,
                        Scale    = shape.MapScale,
                        ZOrder   = shape.ZOrder,
                    },
                });
            }

            config.Map.EditorSetBakedContent(rooms, doors);
            EditorUtility.SetDirty(config.Map);
            AssetDatabase.SaveAssetIfDirty(config.Map);
        }

        private static void Link(Dictionary<RoomController, List<string>> map, RoomController room, string doorId)
        {
            if (!map.TryGetValue(room, out var list))
                map[room] = list = new List<string>();
            if (!list.Contains(doorId))
                list.Add(doorId);
        }
    }
}
```

- [ ] **Step 2: Manual verification in the editor**

1. Open `Deck_B_Development` scene.
2. Create `MapData` asset (`Assets → Create → CrimsonDraft → Map → Map Data`), set `sceneName` to the scene name.
3. Add `MapSceneConfig` on a root GameObject; assign the asset.
4. Add `MapRoomShape` to one `RoomController`, give it 4 points in the inspector (e.g. `(-2,-2) (2,-2) (2,2) (-2,2)`).
5. Add `MapDoorMarker` to one door interactable.
6. Save scene → inspect the `MapData` asset: `rooms` has 1 entry with correct `RoomId`, polygon, `DoorIds`, `PickupIds`; `doors` has 1 entry with correct `DoorId`.

Expected: asset populated, warnings only for intentionally missing data.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapBaker.cs"
git commit -m "feat(map): auto-bake scene map authoring into MapData on scene save"
```

---

### Task 11: PolygonTriangulator (pure, tested)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/PolygonTriangulator.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/PolygonTriangulatorTests.cs`

**Interfaces:**
- Produces: `static class PolygonTriangulator` with `static int[] Triangulate(Vector2[] polygon)` — ear-clipping for simple polygons, returns index triples (CW winding for Unity's upward-facing mesh when viewed from +Y). Used by `MapRenderer` (Task 12).

- [ ] **Step 1: Write failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class PolygonTriangulatorTests
    {
        [Test]
        public void Triangulate_quad_returnsTwoTriangles()
        {
            var quad = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            };
            var tris = PolygonTriangulator.Triangulate(quad);
            Assert.AreEqual(6, tris.Length);
        }

        [Test]
        public void Triangulate_triangle_returnsItself()
        {
            var tri  = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
            var tris = PolygonTriangulator.Triangulate(tri);
            Assert.AreEqual(3, tris.Length);
        }

        [Test]
        public void Triangulate_lShape_coversFullArea()
        {
            // L-shaped (concave) polygon, area = 3
            var l = new[]
            {
                new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1),
                new Vector2(1, 1), new Vector2(1, 2), new Vector2(0, 2),
            };
            var tris = PolygonTriangulator.Triangulate(l);
            Assert.AreEqual((l.Length - 2) * 3, tris.Length, "n-gon must produce n-2 triangles");

            float area = 0f;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector2 a = l[tris[i]], b = l[tris[i + 1]], c = l[tris[i + 2]];
                area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }
            Assert.AreEqual(3f, area, 0.001f, "triangles must cover the polygon area exactly");
        }

        [Test]
        public void Triangulate_degenerateInput_returnsEmpty()
        {
            Assert.IsEmpty(PolygonTriangulator.Triangulate(new[] { new Vector2(0, 0), new Vector2(1, 1) }));
            Assert.IsEmpty(PolygonTriangulator.Triangulate(System.Array.Empty<Vector2>()));
        }
    }
}
```

- [ ] **Step 2: Run filter `PolygonTriangulatorTests`** — expect compile failure.

- [ ] **Step 3: Implement ear clipping**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Ear-clipping triangulation for simple (non-self-intersecting) polygons.
    /// Accepts either winding; output indices reference the input array.</summary>
    public static class PolygonTriangulator
    {
        public static int[] Triangulate(Vector2[] polygon)
        {
            int n = polygon.Length;
            if (n < 3)
                return System.Array.Empty<int>();

            var indices = new List<int>(n);
            if (SignedArea(polygon) > 0f)
                for (int i = 0; i < n; i++) indices.Add(i);
            else
                for (int i = n - 1; i >= 0; i--) indices.Add(i);

            var result = new List<int>((n - 2) * 3);
            int guard  = 0;

            while (indices.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];

                    if (!IsEar(polygon, indices, i0, i1, i2))
                        continue;

                    result.Add(i0); result.Add(i1); result.Add(i2);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // degenerate — bail with what we have
            }

            if (indices.Count == 3)
            {
                result.Add(indices[0]); result.Add(indices[1]); result.Add(indices[2]);
            }

            return result.ToArray();
        }

        private static float SignedArea(Vector2[] p)
        {
            float area = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % p.Length];
                area += (b.x - a.x) * (b.y + a.y);
            }
            return area;
        }

        private static bool IsEar(Vector2[] p, List<int> indices, int i0, int i1, int i2)
        {
            Vector2 a = p[i0], b = p[i1], c = p[i2];

            if (Cross(b - a, c - b) <= 0f) // reflex or collinear
                return false;

            foreach (int idx in indices)
            {
                if (idx == i0 || idx == i1 || idx == i2) continue;
                if (PointInTriangle(p[idx], a, b, c))
                    return false;
            }
            return true;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private static bool PointInTriangle(Vector2 pt, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, pt - a);
            float d2 = Cross(c - b, pt - b);
            float d3 = Cross(a - c, pt - c);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }
    }
}
```

- [ ] **Step 4: Run filter `PolygonTriangulatorTests`** — expect PASS. Iterate on winding/ear logic until green.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/PolygonTriangulator.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/PolygonTriangulatorTests.cs"
git commit -m "feat(map): ear-clipping polygon triangulator"
```

---

### Task 12: MapRenderer (meshes + ortho camera + RenderTexture)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapRenderer.cs`

**Interfaces:**
- Consumes: `MapData` (Task 4), `MapStateResolver` (Task 5), `PolygonTriangulator` (Task 11), registries via `Construct`.
- Produces:
  - `void Construct(RoomStateRegistry, KnownMapsRegistry, PickupRegistry, DoorStateRegistry)` ([Inject])
  - `void Generate(MapData map, string? currentRoomId)` — rebuilds all meshes.
  - `RenderTexture? Texture { get; }`
  - `void Pan(Vector2 delta)` — clamped to map bounds.
  - `void SetVisible(bool)` — enables/disables the camera GameObject.

- [ ] **Step 1: Implement `MapRenderer.cs`**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Builds map meshes on a hidden layer and films them with an ortho camera
    /// into a RenderTexture (Horror Engine approach). Reads MapData + registries only —
    /// never the live 3D scene.</summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        [SerializeField] private Camera    mapCamera   = null!;
        [SerializeField] private Transform contentRoot = null!;
        [SerializeField] private int       renderLayer = 30; // dedicated "MapRender" layer

        [Header("Room materials")]
        [SerializeField] private Material roomVisitedMaterial    = null!;
        [SerializeField] private Material roomNotVisitedMaterial = null!;
        [SerializeField] private Material roomCompletedMaterial  = null!;
        [SerializeField] private Material currentRoomMaterial    = null!;
        [SerializeField] private Material wallMaterial           = null!;
        [SerializeField] private float    wallWidth              = 0.12f;

        [Header("Door materials")]
        [SerializeField] private Material doorUnknownMaterial  = null!;
        [SerializeField] private Material doorLockedMaterial   = null!;
        [SerializeField] private Material doorUnlockedMaterial = null!;

        [Header("Current room pulse")]
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseMin   = 0.55f;

        private RoomStateRegistry rooms      = null!;
        private KnownMapsRegistry knownMaps  = null!;
        private PickupRegistry    pickups    = null!;
        private DoorStateRegistry doorStates = null!;

        private RenderTexture? texture;
        private MapData?       currentMap;
        private Renderer?      currentRoomRenderer;

        // Height layers (Y) for draw order under an ortho top-down camera
        private const float RoomHeight   = 0f;
        private const float WallHeight   = 0.2f;
        private const float DoorHeight   = 0.3f;
        private const float CameraHeight = 10f;

        public RenderTexture? Texture => this.texture;

        [Inject]
        public void Construct(
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps,
            PickupRegistry    pickups,
            DoorStateRegistry doorStates)
        {
            this.rooms      = rooms;
            this.knownMaps  = knownMaps;
            this.pickups    = pickups;
            this.doorStates = doorStates;
        }

        private void Awake()
        {
            this.texture = new RenderTexture(Screen.width, Screen.height, 16);
            this.mapCamera.targetTexture = this.texture;
            this.mapCamera.orthographic  = true;
            this.mapCamera.cullingMask   = 1 << this.renderLayer;
            SetVisible(false);
        }

        private void Update()
        {
            if (this.currentRoomRenderer == null) return;
            float t = Mathf.Lerp(this.pulseMin, 1f,
                (Mathf.Sin(Time.unscaledTime * this.pulseSpeed) + 1f) * 0.5f);
            var c = this.currentRoomRenderer.material.color;
            this.currentRoomRenderer.material.color = new Color(c.r, c.g, c.b, t);
        }

        public void SetVisible(bool visible) => this.mapCamera.gameObject.SetActive(visible);

        public void Generate(MapData map, string? currentRoomId)
        {
            this.currentMap          = map;
            this.currentRoomRenderer = null;

            for (int i = this.contentRoot.childCount - 1; i >= 0; i--)
                Destroy(this.contentRoot.GetChild(i).gameObject);

            bool deckKnown = MapStateResolver.IsDeckKnown(map, this.rooms, this.knownMaps);
            var  drawnDoorIds = new HashSet<string>();

            foreach (var room in map.Rooms)
            {
                var state = MapStateResolver.ResolveRoom(room, deckKnown, this.rooms, this.pickups);
                if (state == MapRoomDisplayState.Hidden)
                    continue; // fog of war: unknown room without the deck plan

                bool isCurrent = currentRoomId != null && room.RoomId == currentRoomId;
                var  material  = isCurrent ? this.currentRoomMaterial : state switch
                {
                    MapRoomDisplayState.NotVisited => this.roomNotVisitedMaterial,
                    MapRoomDisplayState.Completed  => this.roomCompletedMaterial,
                    _                              => this.roomVisitedMaterial,
                };

                var roomRenderer = BuildRoomMesh(room, material);
                if (isCurrent)
                    this.currentRoomRenderer = roomRenderer;

                BuildOutline(room);

                foreach (var id in room.DoorIds)
                    drawnDoorIds.Add(id);
            }

            foreach (var door in map.Doors)
            {
                if (!drawnDoorIds.Contains(door.DoorId))
                    continue; // door only draws when a drawn room links it
                BuildDoorMesh(door);
            }

            CenterCamera(map);
        }

        public void Pan(Vector2 delta)
        {
            if (this.currentMap == null) return;
            var pos = this.mapCamera.transform.position + new Vector3(delta.x, 0f, delta.y);

            float halfW = this.currentMap.GridSize.x * this.currentMap.CellSize * 0.5f;
            float halfH = this.currentMap.GridSize.y * this.currentMap.CellSize * 0.5f;
            pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
            pos.z = Mathf.Clamp(pos.z, -halfH, halfH);

            this.mapCamera.transform.position = pos;
        }

        // ── mesh building ────────────────────────────────────────────────────

        private static Matrix4x4 TRS(MapElementTransform t, float height)
            => Matrix4x4.TRS(
                new Vector3(t.Offset.x, height + t.ZOrder * 0.01f, t.Offset.y),
                Quaternion.Euler(0f, -t.Rotation, 0f),
                new Vector3(t.Scale.x, 1f, t.Scale.y));

        private Renderer BuildRoomMesh(MapRoomData room, Material material)
        {
            var tris = PolygonTriangulator.Triangulate(room.Polygon);
            var verts = new Vector3[room.Polygon.Length];
            var trs   = TRS(room.Transform, RoomHeight);
            for (int i = 0; i < verts.Length; i++)
                verts[i] = trs.MultiplyPoint3x4(new Vector3(room.Polygon[i].x, 0f, room.Polygon[i].y));

            return CreateMeshObject($"Room_{room.RoomId}", verts, tris, material);
        }

        private void BuildOutline(MapRoomData room)
        {
            var poly = room.Polygon;
            var trs  = TRS(room.Transform, WallHeight);
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 dir    = (b - a).normalized;
                Vector2 normal = new(-dir.y, dir.x);
                Vector2 half   = normal * (this.wallWidth * 0.5f);

                int baseIdx = verts.Count;
                verts.Add(trs.MultiplyPoint3x4(new Vector3(a.x - half.x, 0f, a.y - half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(a.x + half.x, 0f, a.y + half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(b.x + half.x, 0f, b.y + half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(b.x - half.x, 0f, b.y - half.y)));
                tris.AddRange(new[] { baseIdx, baseIdx + 2, baseIdx + 1, baseIdx, baseIdx + 3, baseIdx + 2 });
            }

            CreateMeshObject($"Walls_{room.RoomId}", verts.ToArray(), tris.ToArray(), this.wallMaterial);
        }

        private void BuildDoorMesh(MapDoorData door)
        {
            var state = this.doorStates.GetMapState(door.DoorId);
            var material = state switch
            {
                DoorMapState.Locked   => this.doorLockedMaterial,
                DoorMapState.Unlocked => this.doorUnlockedMaterial,
                _                     => this.doorUnknownMaterial,
            };

            var trs = TRS(door.Transform, DoorHeight);
            Vector2 h = door.Size * 0.5f;
            var verts = new[]
            {
                trs.MultiplyPoint3x4(new Vector3(-h.x, 0f, -h.y)),
                trs.MultiplyPoint3x4(new Vector3( h.x, 0f, -h.y)),
                trs.MultiplyPoint3x4(new Vector3( h.x, 0f,  h.y)),
                trs.MultiplyPoint3x4(new Vector3(-h.x, 0f,  h.y)),
            };
            CreateMeshObject($"Door_{door.DoorId}", verts, new[] { 0, 2, 1, 0, 3, 2 }, material);
        }

        private Renderer CreateMeshObject(string name, Vector3[] verts, int[] tris, Material material)
        {
            var go = new GameObject(name) { layer = this.renderLayer };
            go.transform.SetParent(this.contentRoot, worldPositionStays: false);

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().mesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material       = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return meshRenderer;
        }

        private void CenterCamera(MapData map)
        {
            this.mapCamera.transform.position = new Vector3(0f, CameraHeight, 0f);
            this.mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            this.mapCamera.orthographicSize   = map.GridSize.y * map.CellSize * 0.5f;
        }
    }
}
```

- [ ] **Step 2: Compile check** — expect clean.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapRenderer.cs"
git commit -m "feat(map): runtime map renderer with ortho camera and RenderTexture"
```

---

### Task 13: Map screen UI (view + controller + scope wiring)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/MapScreenView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/MapScreenController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

**Interfaces:**
- Consumes: `IInputService.OpenMap/UINavigate/UIBack/UIConfirm`, `SwitchToUI()/SwitchToGameplay()` (existing); `MapRenderer` (Task 12); `MapDataSet` (Task 4); `IRoomOrchestrator.CurrentRoom` (Task 6); registries; `MapStateResolver.IsDeckKnown` (Task 5).
- Produces:
  - `MapScreenView : MonoBehaviour` — `void Show(Texture texture, string deckName)`, `void Hide()`, `bool IsVisible`.
  - `MapScreenController : IInitializable, ITickable, IDisposable`.

Provisional input mapping (GDD leaves final input pending): `OpenMap` opens, `UIBack` closes, `UINavigate` pans, `UIConfirm` cycles to the next known deck.

- [ ] **Step 1: Implement `MapScreenView.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class MapScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root     = null!;
        [SerializeField] private RawImage   mapImage = null!;
        [SerializeField] private TMPro.TextMeshProUGUI deckName = null!;

        public bool IsVisible => this.root.activeSelf;

        public void Show(Texture texture, string deckDisplayName)
        {
            this.mapImage.texture = texture;
            this.deckName.text    = deckDisplayName;
            this.root.SetActive(true);
        }

        public void Hide() => this.root.SetActive(false);
    }
}
```

- [ ] **Step 2: Implement `MapScreenController.cs`**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>Fullscreen map screen. Opens on the current deck, pans with UINavigate,
    /// cycles known decks with UIConfirm, closes with UIBack. Pauses navigation like
    /// the inventory does (timeScale 0 + UI action map).</summary>
    public sealed class MapScreenController : IInitializable, ITickable, IDisposable
    {
        private const float PanSpeed = 12f;

        private readonly IInputService     inputService;
        private readonly MapScreenView     view;
        private readonly MapRenderer       renderer;
        private readonly IRoomOrchestrator roomOrchestrator;
        private readonly MapSceneConfig    sceneConfig;
        private readonly MapDataSet        mapSet;
        private readonly RoomStateRegistry rooms;
        private readonly KnownMapsRegistry knownMaps;

        private MapData? shownMap;

        [Preserve]
        public MapScreenController(
            IInputService     inputService,
            MapScreenView     view,
            MapRenderer       renderer,
            IRoomOrchestrator roomOrchestrator,
            MapSceneConfig    sceneConfig,
            MapDataSet        mapSet,
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps)
        {
            this.inputService     = inputService;
            this.view             = view;
            this.renderer         = renderer;
            this.roomOrchestrator = roomOrchestrator;
            this.sceneConfig      = sceneConfig;
            this.mapSet           = mapSet;
            this.rooms            = rooms;
            this.knownMaps        = knownMaps;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenMap.performed   += OnOpenMap;
            this.inputService.UIBack.performed    += OnBack;
            this.inputService.UIConfirm.performed += OnCycleDeck;
        }

        public void Dispose()
        {
            this.inputService.OpenMap.performed   -= OnOpenMap;
            this.inputService.UIBack.performed    -= OnBack;
            this.inputService.UIConfirm.performed -= OnCycleDeck;
        }

        void ITickable.Tick()
        {
            if (!this.view.IsVisible) return;
            var nav = this.inputService.UINavigate.ReadValue<Vector2>();
            if (nav.sqrMagnitude > 0.01f)
                this.renderer.Pan(nav * (PanSpeed * Time.unscaledDeltaTime));
        }

        private void OnOpenMap(InputAction.CallbackContext _)
        {
            if (this.view.IsVisible) return;

            var currentDeck = FindCurrentDeckMap();
            if (currentDeck == null)
            {
                Debug.LogWarning("[MapScreen] No MapData matches the active scene — map not opened.");
                return;
            }

            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            ShowDeck(currentDeck);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.view.IsVisible) return;

            this.view.Hide();
            this.renderer.SetVisible(false);
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.shownMap = null;
        }

        private void OnCycleDeck(InputAction.CallbackContext _)
        {
            if (!this.view.IsVisible || this.shownMap == null) return;

            var known = KnownDecks();
            if (known.Count <= 1) return;

            int idx = known.IndexOf(this.shownMap);
            ShowDeck(known[(idx + 1) % known.Count]);
        }

        private void ShowDeck(MapData map)
        {
            this.shownMap = map;

            // Highlight the player's room only when showing the deck the player is on.
            var currentRoomId = FindCurrentDeckMap() == map
                ? this.roomOrchestrator.CurrentRoom?.RoomId
                : null;

            this.renderer.SetVisible(true);
            this.renderer.Generate(map, currentRoomId);
            this.view.Show(this.renderer.Texture!, map.DisplayName);
        }

        // The scene's MapSceneConfig is the authoritative "which deck am I on" binding —
        // additive scene loading makes SceneManager.GetActiveScene() unreliable here.
        private MapData? FindCurrentDeckMap() => this.sceneConfig.Map;

        private List<MapData> KnownDecks()
        {
            var result = new List<MapData>();
            foreach (var map in this.mapSet.Maps)
            {
                if (MapStateResolver.IsDeckKnown(map, this.rooms, this.knownMaps))
                    result.Add(map);
            }
            return result;
        }
    }
}
```

- [ ] **Step 3: Register in `NavigationScope`**

Add serialized field + registrations:

```csharp
[SerializeField] private CrimsonDraft.Infrastructure.Map.MapDataSet mapDataSet = null!;
```

In `Configure`:

```csharp
builder.RegisterInstance(this.mapDataSet);
builder.RegisterComponentInHierarchy<CrimsonDraft.Navigation.Map.MapSceneConfig>();
builder.RegisterComponentInHierarchy<CrimsonDraft.Navigation.Map.MapRenderer>();
builder.RegisterComponentInHierarchy<MapScreenView>();
builder.Register<MapScreenController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 4: Compile check + run full EditMode suite** — expect clean/PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/MapScreenView.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/UI/MapScreenController.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(map): fullscreen map screen controller and view"
```

---

### Task 14: SceneView polygon editor (trace over real geometry)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapRoomShapeEditor.cs`

**Interfaces:**
- Consumes: `MapRoomShape` (Task 9).
- Produces: custom inspector + SceneView handles; "Trace From Bounds" and "Add Point" buttons.

- [ ] **Step 1: Implement `MapRoomShapeEditor.cs`**

```csharp
#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>SceneView editing for room silhouettes: draggable point handles on the
    /// room's floor plane, plus buttons to seed the polygon from renderer bounds.</summary>
    [CustomEditor(typeof(MapRoomShape))]
    public sealed class MapRoomShapeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var shape = (MapRoomShape)target;

            if (GUILayout.Button("Trace From Bounds"))
            {
                Undo.RecordObject(shape, "Trace Map Shape From Bounds");
                TraceFromBounds(shape);
                EditorUtility.SetDirty(shape);
            }

            if (GUILayout.Button("Add Point"))
            {
                Undo.RecordObject(shape, "Add Map Shape Point");
                var points = new System.Collections.Generic.List<Vector2>(shape.LocalPoints);
                points.Add(points.Count > 0 ? points[^1] + Vector2.right : Vector2.zero);
                shape.LocalPoints = points.ToArray();
                EditorUtility.SetDirty(shape);
            }
        }

        private void OnSceneGUI()
        {
            var shape = (MapRoomShape)target;
            var points = shape.LocalPoints;
            if (points.Length == 0) return;

            var t = shape.transform;

            Handles.color = Color.cyan;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 world = t.TransformPoint(new Vector3(points[i].x, 0f, points[i].y));
                Vector3 next  = t.TransformPoint(new Vector3(
                    points[(i + 1) % points.Length].x, 0f, points[(i + 1) % points.Length].y));

                Handles.DrawLine(world, next, 2f);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    world, HandleUtility.GetHandleSize(world) * 0.08f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shape, "Move Map Shape Point");
                    Vector3 local = t.InverseTransformPoint(moved);
                    points[i] = new Vector2(local.x, local.z);
                    shape.LocalPoints = points;
                    EditorUtility.SetDirty(shape);
                }
            }
        }

        private static void TraceFromBounds(MapRoomShape shape)
        {
            var renderers = shape.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[MapRoomShapeEditor] No renderers under room — cannot trace bounds.", shape);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            var t   = shape.transform;
            var min = t.InverseTransformPoint(bounds.min);
            var max = t.InverseTransformPoint(bounds.max);

            shape.LocalPoints = new[]
            {
                new Vector2(min.x, min.z),
                new Vector2(max.x, min.z),
                new Vector2(max.x, max.z),
                new Vector2(min.x, max.z),
            };
        }
    }
}
```

- [ ] **Step 2: Manual verification**

Select a room with `MapRoomShape` in Deck_B → "Trace From Bounds" seeds a rectangle over the room floor → drag cyan dots in SceneView to trace the real silhouette → save scene → confirm `MapData` asset updates (MapBaker, Task 10).
Expected: handles drag smoothly, Undo works, asset re-bakes on save.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapRoomShapeEditor.cs"
git commit -m "feat(map): SceneView polygon editor with bounds tracing"
```

---

### Task 15: MapEditorWindow (2D grid layout)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapEditorWindow.cs`

**Interfaces:**
- Consumes: `MapRoomShape`, `MapDoorMarker`, `MapSceneConfig` (Task 9), `MapBaker.Bake` (Task 10).
- Produces: `Tools → CrimsonDraft → Map Editor` window: grid, room polygons and door marks drawn in map space; click selects (syncs Unity selection); drag moves `MapOffset`; `R` rotates selection 90°; scale via Inspector; "Bake Now" button.

- [ ] **Step 1: Implement `MapEditorWindow.cs`**

```csharp
#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>2D grid view of the current scene's map layout. Shape comes from SceneView
    /// tracing (MapRoomShapeEditor); this window arranges map-space placement:
    /// drag = move, R = rotate 90°, scale via Inspector. Baking stays automatic on save.</summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const float PixelsPerUnit = 20f;

        private Vector2 pan;
        private float   zoom = 1f;
        private MapRoomShape?  draggingRoom;
        private MapDoorMarker? draggingDoor;

        [MenuItem("Tools/CrimsonDraft/Map Editor")]
        public static void Open()
        {
            var w = GetWindow<MapEditorWindow>("Map Editor");
            w.minSize = new Vector2(500f, 400f);
        }

        private void OnGUI()
        {
            var config = FindFirstObjectByType<MapSceneConfig>();
            if (config == null || config.Map == null)
            {
                EditorGUILayout.HelpBox(
                    "No MapSceneConfig with a MapData asset in the open scene.", MessageType.Info);
                return;
            }

            DrawToolbar(config);
            HandleInput();
            DrawGrid(config);

            foreach (var shape in FindObjectsByType<MapRoomShape>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                DrawRoom(shape);

            foreach (var marker in FindObjectsByType<MapDoorMarker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                DrawDoor(marker);

            Repaint();
        }

        // ── drawing ──────────────────────────────────────────────────────────

        private void DrawToolbar(MapSceneConfig config)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Map: {config.Map.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Bake Now", EditorStyles.toolbarButton))
                MapBaker.Bake(config);
            GUILayout.EndHorizontal();
        }

        private Vector2 MapToScreen(Vector2 mapPos)
            => new Vector2(mapPos.x, -mapPos.y) * (PixelsPerUnit * this.zoom)
               + this.pan + new Vector2(position.width * 0.5f, position.height * 0.5f);

        private Vector2 ScreenToMap(Vector2 screenPos)
        {
            var p = (screenPos - this.pan
                     - new Vector2(position.width * 0.5f, position.height * 0.5f))
                    / (PixelsPerUnit * this.zoom);
            return new Vector2(p.x, -p.y);
        }

        private void DrawGrid(MapSceneConfig config)
        {
            var size = config.Map.GridSize;
            var cell = config.Map.CellSize;
            Handles.BeginGUI();
            Handles.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            for (int x = -size.x / 2; x <= size.x / 2; x++)
            {
                var a = MapToScreen(new Vector2(x * cell, -size.y * 0.5f * cell));
                var b = MapToScreen(new Vector2(x * cell,  size.y * 0.5f * cell));
                Handles.DrawLine(a, b);
            }
            for (int y = -size.y / 2; y <= size.y / 2; y++)
            {
                var a = MapToScreen(new Vector2(-size.x * 0.5f * cell, y * cell));
                var b = MapToScreen(new Vector2( size.x * 0.5f * cell, y * cell));
                Handles.DrawLine(a, b);
            }
            Handles.EndGUI();
        }

        private void DrawRoom(MapRoomShape shape)
        {
            var points = shape.LocalPoints;
            if (points.Length < 3) return;

            bool selected = Selection.activeGameObject == shape.gameObject;
            var rot = Quaternion.Euler(0f, 0f, -shape.MapRotation);

            var screen = new Vector3[points.Length + 1];
            for (int i = 0; i <= points.Length; i++)
            {
                var p = points[i % points.Length];
                var mapPos = (Vector2)(rot * Vector2.Scale(p, shape.MapScale)) + shape.MapOffset;
                screen[i] = MapToScreen(mapPos);
            }

            Handles.BeginGUI();
            Handles.color = selected ? Color.yellow : Color.cyan;
            Handles.DrawPolyLine(screen);
            Handles.EndGUI();

            var label = MapToScreen(shape.MapOffset);
            GUI.Label(new Rect(label.x - 40, label.y - 8, 120, 16),
                shape.Room.RoomId, EditorStyles.miniBoldLabel);
        }

        private void DrawDoor(MapDoorMarker marker)
        {
            bool selected = Selection.activeGameObject == marker.gameObject;
            var center = MapToScreen(marker.MapOffset);
            var size   = marker.Size * PixelsPerUnit * this.zoom;

            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(marker.MapRotation, center);
            EditorGUI.DrawRect(
                new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y),
                selected ? Color.yellow : new Color(0.9f, 0.4f, 0.3f));
            GUI.matrix = oldMatrix;
        }

        // ── interaction ──────────────────────────────────────────────────────

        private void HandleInput()
        {
            var e = Event.current;

            if (e.type == EventType.ScrollWheel)
            {
                this.zoom = Mathf.Clamp(this.zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.2f, 5f);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                this.pan += e.delta;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hit = PickAt(e.mousePosition);
                Selection.activeGameObject = hit;
                this.draggingRoom = hit != null ? hit.GetComponent<MapRoomShape>()  : null;
                this.draggingDoor = hit != null ? hit.GetComponent<MapDoorMarker>() : null;
                if (hit != null) e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                var deltaMap = new Vector2(e.delta.x, -e.delta.y) / (PixelsPerUnit * this.zoom);
                if (this.draggingRoom != null)
                {
                    Undo.RecordObject(this.draggingRoom, "Move Map Room");
                    this.draggingRoom.MapOffset += deltaMap;
                    EditorUtility.SetDirty(this.draggingRoom);
                    e.Use();
                }
                else if (this.draggingDoor != null)
                {
                    Undo.RecordObject(this.draggingDoor, "Move Map Door");
                    this.draggingDoor.MapOffset += deltaMap;
                    EditorUtility.SetDirty(this.draggingDoor);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                this.draggingRoom = null;
                this.draggingDoor = null;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                var go = Selection.activeGameObject;
                var shape  = go != null ? go.GetComponent<MapRoomShape>()  : null;
                var marker = go != null ? go.GetComponent<MapDoorMarker>() : null;
                if (shape != null)
                {
                    Undo.RecordObject(shape, "Rotate Map Room");
                    shape.MapRotation = (shape.MapRotation + 90f) % 360f;
                    EditorUtility.SetDirty(shape);
                    e.Use();
                }
                else if (marker != null)
                {
                    Undo.RecordObject(marker, "Rotate Map Door");
                    marker.MapRotation = (marker.MapRotation + 90f) % 360f;
                    EditorUtility.SetDirty(marker);
                    e.Use();
                }
            }
        }

        private GameObject? PickAt(Vector2 mousePos)
        {
            var mapPos = ScreenToMap(mousePos);

            foreach (var marker in FindObjectsByType<MapDoorMarker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var half = marker.Size * 0.5f;
                var local = mapPos - marker.MapOffset;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y)
                    return marker.gameObject;
            }

            foreach (var shape in FindObjectsByType<MapRoomShape>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Vector2.Distance(mapPos, shape.MapOffset) * PixelsPerUnit * this.zoom < 400f
                    && ContainsPoint(shape, mapPos))
                    return shape.gameObject;
            }
            return null;
        }

        private static bool ContainsPoint(MapRoomShape shape, Vector2 mapPos)
        {
            var rot   = Quaternion.Euler(0f, 0f, shape.MapRotation);
            var local = (Vector2)(rot * (mapPos - shape.MapOffset));
            local = new Vector2(
                shape.MapScale.x != 0 ? local.x / shape.MapScale.x : local.x,
                shape.MapScale.y != 0 ? local.y / shape.MapScale.y : local.y);

            var p = shape.LocalPoints;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > local.y) != (p[j].y > local.y) &&
                    local.x < (p[j].x - p[i].x) * (local.y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
```

- [ ] **Step 2: Manual verification**

Open `Tools → CrimsonDraft → Map Editor` with Deck_B open: grid draws; rooms with shapes appear as cyan outlines with roomId labels; doors as red rectangles; click selects and syncs to Hierarchy; drag moves; `R` rotates; middle-mouse pans; scroll zooms; "Bake Now" updates the asset.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapEditorWindow.cs"
git commit -m "feat(map): 2D grid map layout editor window"
```

---

### Task 16: Scene/asset setup + end-to-end verification

**Files:** scene + assets only (no code).

- [ ] **Step 1: Project setup (manual, in editor)**

1. Add layer **"MapRender"** in Tags & Layers (pick a free slot; set `renderLayer` on the MapRenderer accordingly).
2. Create 7 URP-compatible unlit materials under `Assets/Art/Materials/Map/`: `Map_RoomVisited` (grey-blue), `Map_RoomNotVisited` (dark, low alpha), `Map_RoomCompleted` (desaturated green), `Map_CurrentRoom` (bright, pulsing alpha), `Map_Wall` (near-black), `Map_DoorUnknown` (neutral grey), `Map_DoorLocked` (red), `Map_DoorUnlocked` (green). Transparent surface type where alpha is used.
3. Create `MapRenderer` GameObject in the Navigation scene: child camera (ortho, culling mask = MapRender, no audio listener), child `Content` transform; assign all serialized fields.
4. Add `MapScreenView` under the navigation UI canvas: fullscreen `root` panel with a `RawImage` (map) and TMP text (deck name), disabled by default.
5. Create the `MapDataSet` asset listing all deck `MapData` assets; assign to `NavigationScope.mapDataSet`.
6. Author Deck_B: `MapSceneConfig` + `MapRoomShape` per room (trace) + `MapDoorMarker` per door; arrange in Map Editor; save scene (bakes).
7. Re-run the NavigationScope cache buttons (doors/pickups) so `cachedMapPickups` serializes.

- [ ] **Step 2: End-to-end verification (Play Mode)**

- Open map (`OpenMap` input): current room draws highlighted and pulsing; unvisited rooms hidden.
- Walk through a door → reopen map: new room drawn, crossed door green.
- Try a locked door without key → map shows it red.
- Unlock with key → map shows it green.
- Pick up a `MapPickupInteractable` for the deck → all rooms of the deck draw (unvisited style).
- Collect every pickup in a room → room draws in completed style.
- Cross to another deck and back → states persist (registries are global).
- `UIBack` closes; timeScale restores; gameplay input returns.

- [ ] **Step 3: Run the full EditMode suite once more** — expect all PASS.

- [ ] **Step 4: Commit scene/asset changes**

```bash
git add "Game/CrimsonDraft/Assets/Scenes" "Game/CrimsonDraft/Assets/Art/Materials/Map" "Game/CrimsonDraft/Assets/Data"
git commit -m "feat(map): scene setup, materials and baked map data for Deck B"
```

---

## Deviations / provisional decisions (flagged, not silent)

- **Input mapping** (GDD "Pendiente"): provisional — `OpenMap` opens (action already exists in `IInputService`), `UIBack` closes, `UINavigate` pans, `UIConfirm` cycles decks. Revisit when GDD fixes final input.
- **Deck selector UI**: GDD shows a "selector"; this plan implements deck cycling with a name label. A visual list can layer on later without architectural change.
- **Art direction** (GDD "Pendiente"): placeholder material colors listed in Task 16.
- **Cross-deck door state sharing**: the GDD says an inter-deck door shows "the same state on both decks' maps". Deck doors are unidirectional pairs with independent `doorId`s, so each deck's map shows its own direction's state by default. To honor the GDD literally, level design must assign the **same `doorId` to both direction interactables of one physical door** (the registry is a shared string-keyed store, so this Just Works). Flag this convention in the level-design docs when authoring; no code change needed.
