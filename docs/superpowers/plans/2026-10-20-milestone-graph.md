# Milestone Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A GraphView editor to draw the progression diagram (milestones, arrows, lanes, notes, frames), plus a runtime that marks each milestone completed when its trigger condition holds, saves it, and answers queries from code.

**Architecture:**
- **Asset:** a `MilestoneGraph` ScriptableObject holds nodes, edges, lanes, notes and frames. Both the editor window and the runtime read it.
- **Change detection:** the existing state registries (pickups, notes, sockets, doors), a new `PuzzleStateRegistry` and the inventory raise `Changed`.
- **Tracking:** a per-scene `MilestoneTracker` re-evaluates pending node triggers (`MilestoneTriggerEvaluator`) through an `IMilestoneWorldState` adapter and completes them in a global, saved `MilestoneRegistry`.
- **Queries:** code asks `IMilestoneService`.

**Tech Stack:** Unity 6000.3, C# (`#nullable enable`), VContainer, `UnityEditor.Experimental.GraphView` + UI Toolkit, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-20-milestone-graph-design.md`

All paths below are relative to `Game/CrimsonDraft/Assets/` unless they start with `docs/`.

## Global Constraints

- **Code style:**
  - Every C# file starts with `#nullable enable`.
  - Serialized fields that Unity fills use `= null!`.
  - Pure C# services get constructor injection, with `[Preserve]` on the constructor.
- **Language:** code, comments, identifiers and UI strings are in English.
- **Commits:**
  - Never add `Co-Authored-By` trailers.
  - Propose each commit to the user before running it.
  - Never stage these unrelated files:
    - `Scenes/Production/Deck_B_Development.unity`
    - `Wwise/Editor/ProjectData/AkWwiseProjectData.asset`
    - Localization `AddressableAssetsData`
    - `TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
- **No runtime scene searches:**
  - Scene objects reach DI through cached serialized arrays on `NavigationScope`, filled by editor buttons and included in **Cache Everything**.
  - `FindObjectsByType` is allowed only inside `#if UNITY_EDITOR` cache buttons and editor windows.
- **Unity edits:** never modify the open scene from code. The user presses the cache buttons and saves scenes themselves.
- **Running tests:**
  - Tests run in Unity: Test Runner window, or the UnityMCP `run_tests` tool with `mode: "EditMode"` and `test_names` / `group_names` filters, polled with `get_test_job`.
  - Before running tests, refresh with UnityMCP `refresh_unity` (`compile: "request"`), then check `read_console` for `error CS`.
- **Arrows never gate anything.** A completed milestone never becomes uncompleted.

## Review Focus

1. **A save made before this feature.** `completedMilestoneIds` / `solvedPuzzleIds` are missing from the JSON. Expect empty lists and no exception. Pinned in Task 4.
2. **The trigger condition already holds when the scene loads**, e.g. an old save already has the Sheep Key. Expect the milestone completed in `Initialize`, with `Completed` raised exactly once. Pinned in Task 8.
3. **A socket whose state array is empty or partially filled.** Expect not completed. Pinned in Task 7.
4. **A trigger with an empty `targetId`, or `ItemHeld` / `NoteRead` with no asset assigned.** Expect never completed and no exception. Pinned in Task 7.
5. **Deleting a node in the editor.** Expect its edges and its frame membership to go with it, so no dangling IDs are left in the asset. Pinned in Task 6.

---

### Task 1: `Changed` events on the existing state registries

**Files:**
- Modify: `Scripts/Infrastructure/PickupRegistry.cs`
- Modify: `Scripts/Infrastructure/NoteRegistry.cs`
- Modify: `Scripts/Infrastructure/DoorStateRegistry.cs`
- Modify: `Scripts/Infrastructure/ItemSocketStateRegistry.cs`
- Test: `Tests/EditMode/RegistryChangedEventTests.cs`

**Interfaces:**
- Produces: `event System.Action? Changed` on `PickupRegistry`, `NoteRegistry`, `DoorStateRegistry` and `ItemSocketStateRegistry`. It is raised only when state really changes. `LoadState` and `ClearAll` never raise it.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RegistryChangedEventTests
    {
        [Test]
        public void Pickup_SetCollected_raisesOnce()
        {
            var registry = new PickupRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetCollected("p1");
            registry.SetCollected("p1");

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Note_SetCollected_raisesOnce()
        {
            var registry = new NoteRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetCollected("n1");
            registry.SetCollected("n1");

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Door_MarkUnlocked_raisesOnlyWhenStateChanges()
        {
            var registry = new DoorStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.MarkLocked("d1");    // Unknown -> Locked
            registry.MarkUnlocked("d1");  // Locked -> Unlocked
            registry.MarkUnlocked("d1");  // no change
            registry.MarkLocked("d1");    // ignored: unlocked doors stay unlocked

            Assert.AreEqual(2, raised);
        }

        [Test]
        public void Socket_SetInserted_raises()
        {
            var registry = new ItemSocketStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetInserted("s1", new[] { true, false });

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void LoadStateAndClearAll_neverRaise()
        {
            var pickups = new PickupRegistry();
            var notes   = new NoteRegistry();
            var doors   = new DoorStateRegistry();
            var sockets = new ItemSocketStateRegistry();
            int raised = 0;
            pickups.Changed += () => raised++;
            notes.Changed   += () => raised++;
            doors.Changed   += () => raised++;
            sockets.Changed += () => raised++;

            pickups.LoadState(new[] { "p1" });
            notes.LoadState(new[] { "n1" });
            doors.LoadState(new System.Collections.Generic.Dictionary<string, DoorMapState> { ["d1"] = DoorMapState.Unlocked });
            sockets.LoadState(new System.Collections.Generic.Dictionary<string, bool[]> { ["s1"] = new[] { true } });
            pickups.ClearAll(); notes.ClearAll(); doors.ClearAll(); sockets.ClearAll();

            Assert.AreEqual(0, raised);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: UnityMCP `refresh_unity` (compile), then `read_console`.
Expected: compile errors `'PickupRegistry' does not contain a definition for 'Changed'` (and the same for the other three).

- [ ] **Step 3: Implement**

`PickupRegistry.cs`: add `using System;` and replace `SetCollected`:

```csharp
        public event Action? Changed;

        public bool IsCollected(string pickupId)  => this.collected.Contains(pickupId);

        public void SetCollected(string pickupId)
        {
            if (this.collected.Add(pickupId)) Changed?.Invoke();
        }
```

`NoteRegistry.cs`: the same change, with `noteId`:

```csharp
        public event Action? Changed;

        public bool IsCollected(string noteId) => this.collected.Contains(noteId);

        public void SetCollected(string noteId)
        {
            if (this.collected.Add(noteId)) Changed?.Invoke();
        }
```

`DoorStateRegistry.cs`: add `using System;`, then replace `MarkLocked` and `MarkUnlocked`:

```csharp
        public event Action? Changed;

        public void MarkLocked(string doorId)
        {
            var current = GetMapState(doorId);
            if (current == DoorMapState.Unlocked || current == DoorMapState.Locked)
                return;

            this.state[doorId] = DoorMapState.Locked;
            Changed?.Invoke();
        }

        public void MarkUnlocked(string doorId)
        {
            if (GetMapState(doorId) == DoorMapState.Unlocked) return;
            this.state[doorId] = DoorMapState.Unlocked;
            Changed?.Invoke();
        }
```

`ItemSocketStateRegistry.cs`: add `using System;`, then:

```csharp
        public event Action? Changed;

        public void SetInserted(string socketId, bool[] slots)
        {
            this.inserted[socketId] = slots;
            Changed?.Invoke();
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `run_tests` with `test_names: ["CrimsonDraft.Tests.RegistryChangedEventTests"]`, then the full EditMode suite.
Expected: 5/5 pass; the full suite stays green.

- [ ] **Step 5: Commit (propose to the user first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/PickupRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/NoteRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/ItemSocketStateRegistry.cs Game/CrimsonDraft/Assets/Tests/EditMode/RegistryChangedEventTests.cs*
git commit -m "feat(infrastructure): raise Changed on pickup, note, door and socket registries"
```

---

### Task 2: `IInventoryService.ContentsChanged`

**Files:**
- Modify: `Scripts/Inventory/IInventoryService.cs`
- Modify: `Scripts/Inventory/InventoryService.cs:191-208`
- Modify: `Tests/EditMode/Fakes/FakeInventoryService.cs`
- Test: `Tests/EditMode/InventoryContentsChangedTests.cs`

**Interfaces:**
- Produces: `event System.Action? ContentsChanged` on `IInventoryService`. It is raised whenever any operator or storage container raises `ItemContainer.Changed`.
- `FakeInventoryService` gets `public void RaiseContentsChanged()` for tests.

- [ ] **Step 1: Write the failing test**

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryContentsChangedTests
    {
        [Test]
        public void TryAdd_raisesContentsChanged()
        {
            var inventory = new InventoryService(FakeRoster.WithOperators(1), FakeCombineService.None, FakeCorpseAccess.None);
            int raised = 0;
            inventory.ContentsChanged += () => raised++;

            Assert.IsTrue(inventory.TryAdd(Consumable(id: "herb")));

            Assert.GreaterOrEqual(raised, 1);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: compile error `'InventoryService' does not contain a definition for 'ContentsChanged'`.

- [ ] **Step 3: Implement**

`IInventoryService.cs`: under `event Action? HeldChanged;` add:

```csharp
        // Raised whenever any operator or storage container's contents change.
        event Action? ContentsChanged;
```

`InventoryService.cs`: add the event and subscribe to each container when it is created:

```csharp
        public event Action? ContentsChanged;

        private void RaiseContentsChanged() => ContentsChanged?.Invoke();

        private ItemContainer[] EnsureContainers()
        {
            if (this.operatorContainers != null) return this.operatorContainers;
            this.roster.EnsureInitialized();
            this.operatorContainers = Enumerable.Range(0, this.roster.Count)
                .Select(i => new ItemContainer(
                    ContainerId.Operator(i),
                    InventoryConstants.OperatorGridWidth,
                    InventoryConstants.OperatorGridHeight))
                .ToArray();
            foreach (var container in this.operatorContainers)
                container.Changed += RaiseContentsChanged;
            return this.operatorContainers;
        }

        private ItemContainer EnsureStorage()
        {
            if (this.storageContainer != null) return this.storageContainer;
            this.storageContainer = new ItemContainer(
                ContainerId.Storage,
                InventoryConstants.StorageGridWidth,
                InventoryConstants.StorageGridHeight);
            this.storageContainer.Changed += RaiseContentsChanged;
            return this.storageContainer;
        }
```

`FakeInventoryService.cs`: next to `HeldChanged` add:

```csharp
        public event Action? ContentsChanged;
        public void RaiseContentsChanged() => ContentsChanged?.Invoke();
```

- [ ] **Step 4: Run tests**

Expected: `InventoryContentsChangedTests` passes; the full suite stays green.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs Game/CrimsonDraft/Assets/Tests/EditMode/Fakes/FakeInventoryService.cs Game/CrimsonDraft/Assets/Tests/EditMode/InventoryContentsChangedTests.cs*
git commit -m "feat(inventory): raise ContentsChanged when any container changes"
```

---

### Task 3: `MilestoneRegistry` and `PuzzleStateRegistry`

**Files:**
- Create: `Scripts/Infrastructure/MilestoneRegistry.cs`
- Create: `Scripts/Infrastructure/PuzzleStateRegistry.cs`
- Test: `Tests/EditMode/MilestoneRegistryTests.cs`
- Test: `Tests/EditMode/PuzzleStateRegistryTests.cs`

**Interfaces:**
- Produces (namespace `CrimsonDraft.Infrastructure`):
  - `MilestoneRegistry`:
    - `bool Complete(string id)`
    - `bool IsCompleted(string id)`
    - `event Action<string>? Completed`
    - `IReadOnlyCollection<string> CompletedIds`
    - `void LoadState(IEnumerable<string>)`
    - `void ClearAll()`
  - `PuzzleStateRegistry`:
    - `void SetSolved(string id)`
    - `bool IsSolved(string id)`
    - `event Action? Changed`
    - `IReadOnlyCollection<string> SolvedIds`
    - `void LoadState(IEnumerable<string>)`
    - `void ClearAll()`
  - Empty or null IDs are ignored by `Complete` and `SetSolved`.

- [ ] **Step 1: Write the failing tests**

`MilestoneRegistryTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneRegistryTests
    {
        [Test]
        public void Complete_firstTimeTrue_secondTimeFalse_raisesOnce()
        {
            var registry = new MilestoneRegistry();
            int raised = 0;
            registry.Completed += _ => raised++;

            Assert.IsTrue(registry.Complete("m1"));
            Assert.IsFalse(registry.Complete("m1"));
            Assert.IsTrue(registry.IsCompleted("m1"));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Complete_emptyId_ignored()
        {
            var registry = new MilestoneRegistry();
            Assert.IsFalse(registry.Complete(""));
            Assert.AreEqual(0, registry.CompletedIds.Count);
        }

        [Test]
        public void LoadState_restoresWithoutRaising()
        {
            var registry = new MilestoneRegistry();
            int raised = 0;
            registry.Completed += _ => raised++;

            registry.LoadState(new[] { "m1", "m2" });

            Assert.IsTrue(registry.IsCompleted("m2"));
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ClearAll_forgetsEverything()
        {
            var registry = new MilestoneRegistry();
            registry.Complete("m1");
            registry.ClearAll();
            Assert.IsFalse(registry.IsCompleted("m1"));
        }
    }
}
```

`PuzzleStateRegistryTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class PuzzleStateRegistryTests
    {
        [Test]
        public void SetSolved_raisesOnce()
        {
            var registry = new PuzzleStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetSolved("music_box");
            registry.SetSolved("music_box");

            Assert.IsTrue(registry.IsSolved("music_box"));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SetSolved_emptyId_ignored()
        {
            var registry = new PuzzleStateRegistry();
            registry.SetSolved("");
            Assert.AreEqual(0, registry.SolvedIds.Count);
        }

        [Test]
        public void LoadStateAndClearAll()
        {
            var registry = new PuzzleStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.LoadState(new[] { "generator" });
            Assert.IsTrue(registry.IsSolved("generator"));

            registry.ClearAll();
            Assert.IsFalse(registry.IsSolved("generator"));
            Assert.AreEqual(0, raised);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors `The type or namespace name 'MilestoneRegistry' could not be found`.

- [ ] **Step 3: Implement**

`MilestoneRegistry.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    // Completed progression milestones (MilestoneGraph node ids). Filled by MilestoneTracker,
    // saved with the game. A milestone never becomes uncompleted.
    public sealed class MilestoneRegistry
    {
        private readonly HashSet<string> completed = new();

        [Preserve]
        public MilestoneRegistry() { }

        public event Action<string>? Completed;

        public IReadOnlyCollection<string> CompletedIds => this.completed;

        public bool IsCompleted(string milestoneId) =>
            !string.IsNullOrEmpty(milestoneId) && this.completed.Contains(milestoneId);

        public bool Complete(string milestoneId)
        {
            if (string.IsNullOrEmpty(milestoneId) || !this.completed.Add(milestoneId)) return false;
            Completed?.Invoke(milestoneId);
            return true;
        }

        public void LoadState(IEnumerable<string> saved)
        {
            this.completed.Clear();
            foreach (var id in saved)
                if (!string.IsNullOrEmpty(id)) this.completed.Add(id);
        }

        public void ClearAll() => this.completed.Clear();
    }
}
```

`PuzzleStateRegistry.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    // One shared "solved" flag per puzzle (IPuzzle.PuzzleId), saved with the game.
    public sealed class PuzzleStateRegistry
    {
        private readonly HashSet<string> solved = new();

        [Preserve]
        public PuzzleStateRegistry() { }

        public event Action? Changed;

        public IReadOnlyCollection<string> SolvedIds => this.solved;

        public bool IsSolved(string puzzleId) =>
            !string.IsNullOrEmpty(puzzleId) && this.solved.Contains(puzzleId);

        public void SetSolved(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId) || !this.solved.Add(puzzleId)) return;
            Changed?.Invoke();
        }

        public void LoadState(IEnumerable<string> saved)
        {
            this.solved.Clear();
            foreach (var id in saved)
                if (!string.IsNullOrEmpty(id)) this.solved.Add(id);
        }

        public void ClearAll() => this.solved.Clear();
    }
}
```

- [ ] **Step 4: Run tests**

Expected: 7/7 pass.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/MilestoneRegistry.cs* Game/CrimsonDraft/Assets/Scripts/Infrastructure/PuzzleStateRegistry.cs* Game/CrimsonDraft/Assets/Tests/EditMode/MilestoneRegistryTests.cs* Game/CrimsonDraft/Assets/Tests/EditMode/PuzzleStateRegistryTests.cs*
git commit -m "feat(infrastructure): add MilestoneRegistry and PuzzleStateRegistry"
```

---

### Task 4: Save, load and reset of both registries

**Files:**
- Modify: `Scripts/Infrastructure/GameLifetimeScope.cs` (registry block, after `PushableStateRegistry`)
- Modify: `Scripts/Infrastructure/Save/WorldStateRegistries.cs`
- Modify: `Scripts/Infrastructure/Save/GameStateResetter.cs`
- Modify: `Scripts/Infrastructure/Save/SaveGameData.cs`
- Modify: `Scripts/Navigation/Interactables/UI/SaveController.cs` (`BuildSaveData`)
- Modify: `Scripts/Navigation/SaveGameLoader.cs` (`Initialize`)
- Modify tests: `Tests/EditMode/SaveControllerTests.cs`, `SaveGameLoaderTests.cs`, `GameStateResetterTests.cs` (every `new WorldStateRegistries(`)

**Interfaces:**
- Consumes: Task 3 registries.
- Produces:
  - `WorldStateRegistries.Milestones` (`MilestoneRegistry`) and `.Puzzles` (`PuzzleStateRegistry`). They are new constructor parameters, appended after `pushables`.
  - `SaveGameData.completedMilestoneIds` and `.solvedPuzzleIds` (`List<string>`).

- [ ] **Step 1: Update the tests first**

In all three test files, change every
`new RadioCallRegistry(), new PushableStateRegistry())`
to
`new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry())`.

This is a mechanical edit; there are 4 call sites, found with `grep -n "new PushableStateRegistry())"`.

Add assertions:
- `GameStateResetterTests`:
  - after `world.Pushables.Record(...)`: `world.Milestones.Complete("m1"); world.Puzzles.SetSolved("p1");`
  - in the asserts: `Assert.IsFalse(world.Milestones.IsCompleted("m1")); Assert.IsFalse(world.Puzzles.IsSolved("p1"));`
- `SaveControllerTests.BuildSaveData_…`:
  - after `world.Pushables.Record(...)`: `world.Milestones.Complete("m1"); world.Puzzles.SetSolved("p1");`
  - asserts: `CollectionAssert.AreEqual(new[] { "m1" }, data.completedMilestoneIds); CollectionAssert.AreEqual(new[] { "p1" }, data.solvedPuzzleIds);`
- `SaveGameLoaderTests.Initialize_withPendingLoad_…`:
  - in the `SaveGameData` initializer: `completedMilestoneIds = new List<string> { "m1" }, solvedPuzzleIds = new List<string> { "p1" },`
  - asserts: `Assert.IsTrue(world.Milestones.IsCompleted("m1")); Assert.IsTrue(world.Puzzles.IsSolved("p1"));`
- New test in `SaveGameLoaderTests` (Review Focus 1, old save). It uses the file's private fakes and `MakeDatabase`:

```csharp
        [Test]
        public void Initialize_withSaveFromBeforeMilestones_loadsEmptyLists()
        {
            var saveService = new FakeSaveGameService
            {
                PendingLoad = JsonUtility.FromJson<SaveGameData>("{\"sceneName\":\"Deck_B\",\"roomId\":\"room-1\"}"),
            };
            var itemDb = MakeDatabase();
            var world  = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(),
                new MilestoneRegistry(), new PuzzleStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, new FakeInventoryService(), new FakeRoster(),
                    new FakeRoomOrchestrator(), player, itemDb, world, new PlaytimeTracker());

                Assert.DoesNotThrow(() => ((IInitializable)loader).Initialize());
                Assert.AreEqual(0, world.Milestones.CompletedIds.Count);
                Assert.AreEqual(0, world.Puzzles.SolvedIds.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
            }
        }
```

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors on the `WorldStateRegistries` constructor and on `completedMilestoneIds`.

- [ ] **Step 3: Implement**

`WorldStateRegistries.cs`:
- add the fields `public readonly MilestoneRegistry Milestones;` and `public readonly PuzzleStateRegistry Puzzles;`;
- append the constructor parameters `MilestoneRegistry milestones, PuzzleStateRegistry puzzles`;
- assign them;
- update the summary count from "ten" to "twelve".

`GameStateResetter.ResetAll`: after `this.world.Pushables.ClearAll();` add:

```csharp
            this.world.Milestones.ClearAll();
            this.world.Puzzles.ClearAll();
```

`SaveGameData.cs`: after `pushables`, add:

```csharp
        public List<string>             completedMilestoneIds = new List<string>();
        public List<string>             solvedPuzzleIds       = new List<string>();
```

`SaveController.BuildSaveData`: after the pushables loop, add:

```csharp
            data.completedMilestoneIds.AddRange(this.world.Milestones.CompletedIds);
            data.solvedPuzzleIds.AddRange(this.world.Puzzles.SolvedIds);
```

`SaveGameLoader.Initialize`: make these the first two lines after the null check, so they run before the inventory restore. That way, `ContentsChanged` raised during the restore already sees the loaded milestones.

```csharp
            this.world.Milestones.LoadState(data.completedMilestoneIds ?? new List<string>());
            this.world.Puzzles.LoadState(data.solvedPuzzleIds ?? new List<string>());
```

`GameLifetimeScope.cs`: after `builder.Register<PushableStateRegistry>(Lifetime.Singleton);` add:

```csharp
            builder.Register<MilestoneRegistry>(Lifetime.Singleton);
            builder.Register<PuzzleStateRegistry>(Lifetime.Singleton);
```

- [ ] **Step 4: Run the full EditMode suite**

Expected: everything passes, including the new old-save test.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs
git commit -m "feat(save): persist completed milestones and solved puzzles"
```

---

### Task 5: `IPuzzle`, `PuzzleBootstrap` and the four puzzles

**Files:**
- Create: `Scripts/Navigation/Interactables/IPuzzle.cs`
- Create: `Scripts/Navigation/Interactables/PuzzleBootstrap.cs`
- Modify: `Scripts/Navigation/Interactables/GeneratorSwitchPanel.cs` (class declaration; solve at `this.registry.SetInserted(SolvedKey, new[] { true });`)
- Modify: `Scripts/Navigation/Interactables/MusicBoxPanel.cs` (class declaration; solve at `this.IsSolved = true;` in `TurnCrankAsync`)
- Modify: `Scripts/Navigation/Interactables/PuzzleInteractable.cs`
- Modify: `Scripts/Navigation/Interactables/BeeperDoorMechanism.cs` (`OnCodeAccepted`)
- Modify: `Scripts/Navigation/NavigationScope.cs` (cached array, registration, cache button, Cache Everything)
- Test: `Tests/EditMode/PuzzleBootstrapTests.cs`

**Interfaces:**
- Consumes: `PuzzleStateRegistry` (Task 3).
- Produces: `CrimsonDraft.Navigation.Interactables.IPuzzle` with `string PuzzleId { get; }` and `void BindPuzzleState(PuzzleStateRegistry registry)`. `PuzzleBootstrap(PuzzleStateRegistry, IPuzzle[])` is an `IInitializable`.

- [ ] **Step 1: Write the failing test**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class PuzzleBootstrapTests
    {
        private sealed class FakePuzzle : IPuzzle
        {
            public string PuzzleId => "fake";
            public PuzzleStateRegistry? Bound;
            public void BindPuzzleState(PuzzleStateRegistry registry) => this.Bound = registry;
        }

        [Test]
        public void Initialize_bindsEveryPuzzle()
        {
            var registry = new PuzzleStateRegistry();
            var a = new FakePuzzle();
            var b = new FakePuzzle();

            ((IInitializable)new PuzzleBootstrap(registry, new IPuzzle[] { a, b })).Initialize();

            Assert.AreSame(registry, a.Bound);
            Assert.AreSame(registry, b.Bound);
        }

        [Test]
        public void PuzzleInteractable_solvingCallsSetSolved()
        {
            var go = new GameObject("Puzzle", typeof(PuzzleInteractable));
            try
            {
                var puzzle = go.GetComponent<PuzzleInteractable>();
                var so = new UnityEditor.SerializedObject(puzzle);
                so.FindProperty("puzzleId").stringValue = "lab_terminal";
                so.ApplyModifiedPropertiesWithoutUndo();
                var registry = new PuzzleStateRegistry();
                ((IPuzzle)puzzle).BindPuzzleState(registry);

                puzzle.MarkSolved();

                Assert.IsTrue(registry.IsSolved("lab_terminal"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: compile errors `IPuzzle` / `PuzzleBootstrap` / `MarkSolved` not found.

- [ ] **Step 3: Implement**

`IPuzzle.cs`:

```csharp
#nullable enable

using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    // A puzzle that reports "solved" to the shared PuzzleStateRegistry, so milestones can
    // trigger on it (MilestoneTriggerKind.PuzzleSolved). Bound once per scene load by
    // PuzzleBootstrap from NavigationScope's cached puzzles.
    public interface IPuzzle
    {
        string PuzzleId { get; }
        void BindPuzzleState(PuzzleStateRegistry registry);
    }
}
```

`PuzzleBootstrap.cs`:

```csharp
#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleBootstrap : IInitializable
    {
        private readonly PuzzleStateRegistry registry;
        private readonly IPuzzle[]           puzzles;

        [Preserve]
        public PuzzleBootstrap(PuzzleStateRegistry registry, IPuzzle[] puzzles)
        {
            this.registry = registry;
            this.puzzles  = puzzles;
        }

        void IInitializable.Initialize()
        {
            foreach (var puzzle in this.puzzles)
                puzzle.BindPuzzleState(this.registry);
        }
    }
}
```

`GeneratorSwitchPanel.cs`:
- add `using CrimsonDraft.Infrastructure;` if it is missing, and append `, IPuzzle` to the class's interface list;
- add the members below;
- right after `this.registry.SetInserted(SolvedKey, new[] { true });`, add `this.puzzleStates?.SetSolved(this.solvedId);`.

```csharp
        private PuzzleStateRegistry? puzzleStates;

        // Same id as its own "#solved" key, so one string names this generator everywhere.
        public string PuzzleId => this.solvedId;
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;
```

`MusicBoxPanel.cs`:
- add `, IPuzzle` and `using CrimsonDraft.Infrastructure;`;
- add the members below;
- right after `this.IsSolved = true;` in `TurnCrankAsync`, add `this.puzzleStates?.SetSolved(this.puzzleId);`.

```csharp
        [SerializeField] private string puzzleId = "music_box";

        private PuzzleStateRegistry? puzzleStates;

        public string PuzzleId => this.puzzleId;
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;
```

`PuzzleInteractable.cs`: implement `IPuzzle`, and expose `MarkSolved` so the callback and the test share one path:

```csharp
    public sealed class PuzzleInteractable : MonoBehaviour, IInteractable, IPuzzle
    {
        [SerializeField] private string                  puzzleId = "";
        [SerializeField] private GameObject              canvasPrefab = null!;
        [SerializeField] private ItemSocketInteractable? requiredSocket;
        [SerializeField] private bool                    triggersDemoEnd;
        [SerializeField, TextArea] private string         demoEndMessage = "This is the end of the demo.\nThanks for playing.";

        private bool isSolved;
        private PuzzleStateRegistry? puzzleStates;

        public string PuzzleId => this.puzzleId;
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;

        public void MarkSolved()
        {
            this.isSolved = true;
            this.puzzleStates?.SetSolved(this.puzzleId);
        }

        public void Interact(InteractionContext context)
        {
            if (this.isSolved) return;

            if (this.requiredSocket != null && !this.requiredSocket.IsActivated)
            {
                this.requiredSocket.Interact(context);
                return;
            }

            context.PuzzleViewController.Open(this.canvasPrefab, onSolved: () =>
            {
                MarkSolved();
                if (this.triggersDemoEnd)
                    context.ScreenFader.ShowEndScreenAsync(this.demoEndMessage).Forget();
            });
        }
    }
```

Add `using CrimsonDraft.Infrastructure;` to its usings.

`BeeperDoorMechanism.cs`:
- add `, IPuzzle` and `using CrimsonDraft.Infrastructure;`;
- add the members below;
- in `OnCodeAccepted`, right after `if (!this.triggered) return;`, add `this.puzzleStates?.SetSolved(this.puzzleId);`.

```csharp
        [SerializeField] private string puzzleId = "beeper_door";

        private PuzzleStateRegistry? puzzleStates;

        public string PuzzleId => this.puzzleId;
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;
```

`NavigationScope.cs`:
- add `using System.Linq;` if it is missing;
- next to `cachedPushables`:

```csharp
        // MonoBehaviour[] because Unity can't serialize interface arrays; holds only IPuzzle components.
        [SerializeField] private MonoBehaviour[]          cachedPuzzles     = System.Array.Empty<MonoBehaviour>();
```

- after the `PushableBootstrap` registration:

```csharp
            builder.RegisterInstance(this.cachedPuzzles.OfType<IPuzzle>().ToArray());
            builder.Register<PuzzleBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
```

- inside `#if UNITY_EDITOR`, after `CacheScenePushables`:

```csharp
        [Button("Cache Scene Puzzles")]
        private void CacheScenePuzzles()
        {
            this.cachedPuzzles = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b is IPuzzle)
                .ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
```

- in `CacheEverything()`, call `CacheScenePuzzles();` after `CacheScenePushables();` and append `{this.cachedPuzzles.Length} puzzles` to the log line.

- [ ] **Step 4: Run tests**

Expected: `PuzzleBootstrapTests` 2/2 pass; the full suite stays green.

- [ ] **Step 5: Manual step for the user** (do not do it from code)

Open each navigation scene, press **Cache Everything** on its `NavigationScope`, check that the console line lists the expected puzzles, and save the scene. Give `PuzzleInteractable` instances a `puzzleId` in the inspector.

- [ ] **Step 6: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IPuzzle.cs* Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PuzzleBootstrap.cs* Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/GeneratorSwitchPanel.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MusicBoxPanel.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PuzzleInteractable.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/BeeperDoorMechanism.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs Game/CrimsonDraft/Assets/Tests/EditMode/PuzzleBootstrapTests.cs*
git commit -m "feat(navigation): report solved puzzles to PuzzleStateRegistry"
```

---

### Task 6: `MilestoneGraph` asset and `MilestoneRef`

**Files:**
- Create: `Scripts/Navigation/Milestones/MilestoneGraph.cs`
- Create: `Scripts/Navigation/Milestones/MilestoneGraphData.cs` (lane, node, edge, note, frame, trigger types)
- Create: `Scripts/Navigation/Milestones/MilestoneRef.cs`
- Test: `Tests/EditMode/MilestoneGraphTests.cs`

**Interfaces:**
- Produces (namespace `CrimsonDraft.Navigation.Milestones`):
  - `MilestoneGraph : ScriptableObject` with:
    - `List<MilestoneLane> Lanes`, `List<MilestoneNode> Nodes`, `List<MilestoneEdge> Edges`, `List<MilestoneNote> Notes`, `List<MilestoneFrame> Frames`
    - `MilestoneNode AddNode(string title, Vector2 position)`
    - `bool RemoveNode(string nodeId)`
    - `MilestoneNode? FindNode(string nodeId)`
    - `MilestoneLane? FindLane(string laneId)`
  - Data classes exactly as in spec §1, with `MilestoneTrigger.item : ItemData?` and `note : DocumentData?`.
  - `MilestoneRef` struct with `MilestoneGraph? graph`, `string nodeId`, `bool IsValid`.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneGraphTests
    {
        private MilestoneGraph graph = null!;

        [SetUp]    public void SetUp()    => this.graph = ScriptableObject.CreateInstance<MilestoneGraph>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.graph);

        [Test]
        public void AddNode_assignsUniqueStableId()
        {
            var a = this.graph.AddNode("Take Sheep Key", Vector2.zero);
            var b = this.graph.AddNode("Use Sheep Key", Vector2.one);

            Assert.IsFalse(string.IsNullOrEmpty(a.id));
            Assert.AreNotEqual(a.id, b.id);

            string before = a.id;
            a.title = "Take Sheep Key in Save Room";
            Assert.AreEqual(before, this.graph.FindNode(before)!.id);
        }

        [Test]
        public void RemoveNode_dropsItsEdgesAndFrameMembership()
        {
            var a = this.graph.AddNode("A", Vector2.zero);
            var b = this.graph.AddNode("B", Vector2.one);
            this.graph.Edges.Add(new MilestoneEdge { fromNodeId = a.id, toNodeId = b.id });
            this.graph.Frames.Add(new MilestoneFrame { id = "f", nodeIds = { a.id, b.id } });

            Assert.IsTrue(this.graph.RemoveNode(a.id));

            Assert.IsNull(this.graph.FindNode(a.id));
            Assert.AreEqual(0, this.graph.Edges.Count);
            CollectionAssert.AreEqual(new[] { b.id }, this.graph.Frames[0].nodeIds);
        }

        [Test]
        public void MilestoneRef_isValidOnlyWithGraphAndId()
        {
            var node = this.graph.AddNode("A", Vector2.zero);
            Assert.IsTrue(new MilestoneRef(this.graph, node.id).IsValid);
            Assert.IsFalse(new MilestoneRef(null, node.id).IsValid);
            Assert.IsFalse(new MilestoneRef(this.graph, "").IsValid);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Expected: compile error `namespace 'CrimsonDraft.Navigation.Milestones' could not be found`.

- [ ] **Step 3: Implement**

`MilestoneGraphData.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum EdgeStyle { Solid, Dashed }

    public enum MilestoneTriggerKind { None, PickupCollected, ItemHeld, SocketCompleted, DoorUnlocked, NoteRead, PuzzleSolved }

    [Serializable]
    public sealed class MilestoneLane
    {
        public string id    = "";
        public string name  = "Lane";
        public Color  color = Color.white;
    }

    [Serializable]
    public sealed class MilestoneTrigger
    {
        public MilestoneTriggerKind kind;
        public string        targetId = "";   // pickupId / socketId / doorId / puzzleId
        public ItemData?     item;            // ItemHeld
        public DocumentData? note;            // NoteRead
    }

    [Serializable]
    public sealed class MilestoneNode
    {
        public string           id       = "";
        public string           title    = "";
        public string           laneId   = "";
        public Vector2          position;
        public MilestoneTrigger trigger  = new();
    }

    // Documentation only: arrows never gate a milestone.
    [Serializable]
    public sealed class MilestoneEdge
    {
        public string    fromNodeId = "";
        public string    toNodeId   = "";
        public EdgeStyle style;
        public string    laneId = "";
        public bool      useCustomColor;
        public Color     customColor = Color.white;
    }

    [Serializable]
    public sealed class MilestoneNote
    {
        public string id   = "";
        public string text = "";
        public Rect   rect = new(0f, 0f, 200f, 120f);
    }

    [Serializable]
    public sealed class MilestoneFrame
    {
        public string       id      = "";
        public string       title   = "";
        public string       laneId  = "";
        public List<string> nodeIds = new();
    }
}
```

`MilestoneGraph.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Milestones
{
    // One progression diagram (an area or act). Edited in Tools > CrimsonDraft > Milestone Graph;
    // read at runtime by MilestoneTracker. Node ids are what saves store -- never reuse or edit them.
    [CreateAssetMenu(menuName = "CrimsonDraft/Milestone Graph", fileName = "Milestones")]
    public sealed class MilestoneGraph : ScriptableObject
    {
        [SerializeField] private List<MilestoneLane>  lanes  = new();
        [SerializeField] private List<MilestoneNode>  nodes  = new();
        [SerializeField] private List<MilestoneEdge>  edges  = new();
        [SerializeField] private List<MilestoneNote>  notes  = new();
        [SerializeField] private List<MilestoneFrame> frames = new();

        public List<MilestoneLane>  Lanes  => this.lanes;
        public List<MilestoneNode>  Nodes  => this.nodes;
        public List<MilestoneEdge>  Edges  => this.edges;
        public List<MilestoneNote>  Notes  => this.notes;
        public List<MilestoneFrame> Frames => this.frames;

        public static string NewId() => Guid.NewGuid().ToString("N");

        public MilestoneNode AddNode(string title, Vector2 position)
        {
            var node = new MilestoneNode { id = NewId(), title = title, position = position };
            this.nodes.Add(node);
            return node;
        }

        public bool RemoveNode(string nodeId)
        {
            if (this.nodes.RemoveAll(n => n.id == nodeId) == 0) return false;
            this.edges.RemoveAll(e => e.fromNodeId == nodeId || e.toNodeId == nodeId);
            foreach (var frame in this.frames)
                frame.nodeIds.Remove(nodeId);
            return true;
        }

        public MilestoneNode? FindNode(string nodeId) => this.nodes.Find(n => n.id == nodeId);

        public MilestoneLane? FindLane(string laneId) => this.lanes.Find(l => l.id == laneId);
    }
}
```

`MilestoneRef.cs`:

```csharp
#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Milestones
{
    // Inspector-friendly pointer to one milestone (graph + node id); MilestoneRefDrawer shows a
    // dropdown of the graph's node titles, so code never hard-codes milestone ids.
    [Serializable]
    public struct MilestoneRef
    {
        public MilestoneGraph? graph;
        public string          nodeId;

        public MilestoneRef(MilestoneGraph? graph, string nodeId)
        {
            this.graph  = graph;
            this.nodeId = nodeId;
        }

        public bool IsValid => this.graph != null && !string.IsNullOrEmpty(this.nodeId);
    }
}
```

- [ ] **Step 4: Run tests**

Expected: `MilestoneGraphTests` 3/3 pass.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Milestones* Game/CrimsonDraft/Assets/Tests/EditMode/MilestoneGraphTests.cs*
git commit -m "feat(navigation): add MilestoneGraph asset and MilestoneRef"
```

---

### Task 7: Trigger evaluation

**Files:**
- Create: `Scripts/Navigation/Milestones/IMilestoneWorldState.cs`
- Create: `Scripts/Navigation/Milestones/MilestoneTriggerEvaluator.cs`
- Test: `Tests/EditMode/MilestoneTriggerEvaluatorTests.cs`

**Interfaces:**
- Consumes: Task 6 data types.
- Produces:
  - `enum MilestoneSource { None, Pickups, Inventory, Sockets, Doors, Notes, Puzzles }`
  - `IMilestoneWorldState : IDisposable` with:
    - `bool IsPickupCollected(string id)`
    - `bool HasItem(string itemId)`
    - `bool IsSocketComplete(string socketId)`
    - `bool IsDoorUnlocked(string doorId)`
    - `bool IsNoteRead(string noteId)`
    - `bool IsPuzzleSolved(string puzzleId)`
    - `event Action<MilestoneSource>? SourceChanged`
  - `static class MilestoneTriggerEvaluator` with:
    - `bool Holds(MilestoneTrigger trigger, IMilestoneWorldState world)`
    - `MilestoneSource SourceOf(MilestoneTriggerKind kind)`

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneTriggerEvaluatorTests
    {
        internal sealed class FakeWorld : IMilestoneWorldState
        {
            public readonly HashSet<string> Pickups = new(), Items = new(), Sockets = new(), Doors = new(), Notes = new(), Puzzles = new();
            public event Action<MilestoneSource>? SourceChanged;
            public void Raise(MilestoneSource source) => SourceChanged?.Invoke(source);
            public bool IsPickupCollected(string id) => this.Pickups.Contains(id);
            public bool HasItem(string itemId)       => this.Items.Contains(itemId);
            public bool IsSocketComplete(string id)  => this.Sockets.Contains(id);
            public bool IsDoorUnlocked(string id)    => this.Doors.Contains(id);
            public bool IsNoteRead(string id)        => this.Notes.Contains(id);
            public bool IsPuzzleSolved(string id)    => this.Puzzles.Contains(id);
            public void Dispose() { }
        }

        private static MilestoneTrigger T(MilestoneTriggerKind kind, string id = "") => new() { kind = kind, targetId = id };

        [Test]
        public void TextIdKinds_followTheirSource()
        {
            var world = new FakeWorld();
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            world.Pickups.Add("p"); world.Sockets.Add("s"); world.Doors.Add("d"); world.Puzzles.Add("z");
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.SocketCompleted, "s"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.DoorUnlocked, "d"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PuzzleSolved, "z"), world));
        }

        [Test]
        public void ItemHeldAndNoteRead_useTheirAssets()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var note = ScriptableObject.CreateInstance<DocumentData>();
            try
            {
                var itemSo = new SerializedObject(item); itemSo.FindProperty("itemId").stringValue = "sheep_key"; itemSo.ApplyModifiedPropertiesWithoutUndo();
                var noteSo = new SerializedObject(note); noteSo.FindProperty("noteId").stringValue = "beeper_file"; noteSo.ApplyModifiedPropertiesWithoutUndo();
                var world = new FakeWorld();
                var held = new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld, item = item };
                var read = new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead, note = note };

                Assert.IsFalse(MilestoneTriggerEvaluator.Holds(held, world));
                world.Items.Add("sheep_key"); world.Notes.Add("beeper_file");
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(held, world));
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(read, world));
            }
            finally { UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(note); }
        }

        // Review Focus 4: empty / unassigned targets never hold and never throw.
        [Test]
        public void MissingTargets_neverHold()
        {
            var world = new FakeWorld();
            world.Pickups.Add("");
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.None), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, ""), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld }, world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead }, world));
        }

        [Test]
        public void SourceOf_mapsEveryKind()
        {
            Assert.AreEqual(MilestoneSource.Pickups,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.PickupCollected));
            Assert.AreEqual(MilestoneSource.Inventory, MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.ItemHeld));
            Assert.AreEqual(MilestoneSource.Sockets,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.SocketCompleted));
            Assert.AreEqual(MilestoneSource.Doors,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.DoorUnlocked));
            Assert.AreEqual(MilestoneSource.Notes,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.NoteRead));
            Assert.AreEqual(MilestoneSource.Puzzles,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.PuzzleSolved));
            Assert.AreEqual(MilestoneSource.None,      MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.None));
        }
    }
}
```

The serialized fields are `ItemData.itemId` (`ItemId => this.itemId`) and `DocumentData.noteId` (`NoteId => this.noteId`); both were verified in the code.

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors for `IMilestoneWorldState`, `MilestoneSource` and `MilestoneTriggerEvaluator`.

- [ ] **Step 3: Implement**

`IMilestoneWorldState.cs`:

```csharp
#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum MilestoneSource { None, Pickups, Inventory, Sockets, Doors, Notes, Puzzles }

    // Read-only view of the game state milestone triggers test against, plus one change signal
    // per source. MilestoneWorldState adapts the real registries; tests use a fake.
    public interface IMilestoneWorldState : IDisposable
    {
        bool IsPickupCollected(string pickupId);
        bool HasItem(string itemId);
        bool IsSocketComplete(string socketId);
        bool IsDoorUnlocked(string doorId);
        bool IsNoteRead(string noteId);
        bool IsPuzzleSolved(string puzzleId);

        event Action<MilestoneSource>? SourceChanged;
    }
}
```

`MilestoneTriggerEvaluator.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Navigation.Milestones
{
    public static class MilestoneTriggerEvaluator
    {
        public static bool Holds(MilestoneTrigger trigger, IMilestoneWorldState world)
        {
            switch (trigger.kind)
            {
                case MilestoneTriggerKind.PickupCollected: return Has(trigger.targetId) && world.IsPickupCollected(trigger.targetId);
                case MilestoneTriggerKind.SocketCompleted: return Has(trigger.targetId) && world.IsSocketComplete(trigger.targetId);
                case MilestoneTriggerKind.DoorUnlocked:    return Has(trigger.targetId) && world.IsDoorUnlocked(trigger.targetId);
                case MilestoneTriggerKind.PuzzleSolved:    return Has(trigger.targetId) && world.IsPuzzleSolved(trigger.targetId);
                case MilestoneTriggerKind.ItemHeld:        return trigger.item != null && Has(trigger.item.ItemId) && world.HasItem(trigger.item.ItemId);
                case MilestoneTriggerKind.NoteRead:        return trigger.note != null && Has(trigger.note.NoteId) && world.IsNoteRead(trigger.note.NoteId);
                default:                                   return false;
            }
        }

        public static MilestoneSource SourceOf(MilestoneTriggerKind kind) => kind switch
        {
            MilestoneTriggerKind.PickupCollected => MilestoneSource.Pickups,
            MilestoneTriggerKind.ItemHeld        => MilestoneSource.Inventory,
            MilestoneTriggerKind.SocketCompleted => MilestoneSource.Sockets,
            MilestoneTriggerKind.DoorUnlocked    => MilestoneSource.Doors,
            MilestoneTriggerKind.NoteRead        => MilestoneSource.Notes,
            MilestoneTriggerKind.PuzzleSolved    => MilestoneSource.Puzzles,
            _                                    => MilestoneSource.None,
        };

        private static bool Has(string? id) => !string.IsNullOrEmpty(id);
    }
}
```

- [ ] **Step 4: Run tests**

Expected: 4/4 pass.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Milestones/IMilestoneWorldState.cs* Game/CrimsonDraft/Assets/Scripts/Navigation/Milestones/MilestoneTriggerEvaluator.cs* Game/CrimsonDraft/Assets/Tests/EditMode/MilestoneTriggerEvaluatorTests.cs*
git commit -m "feat(navigation): evaluate milestone triggers against world state"
```

---

### Task 8: World-state adapter, tracker, service and scope wiring

**Files:**
- Create: `Scripts/Navigation/Milestones/MilestoneWorldState.cs`
- Create: `Scripts/Navigation/Milestones/MilestoneTracker.cs`
- Create: `Scripts/Navigation/Milestones/IMilestoneService.cs`
- Create: `Scripts/Navigation/Milestones/MilestoneService.cs`
- Modify: `Scripts/Navigation/NavigationScope.cs`
- Test: `Tests/EditMode/MilestoneTrackerTests.cs`
- Test: `Tests/EditMode/MilestoneWorldStateTests.cs`

**Interfaces:**
- Consumes:
  - `MilestoneRegistry` (Task 3), `MilestoneGraph` (Task 6) and `MilestoneTriggerEvaluator` / `IMilestoneWorldState` (Task 7);
  - the registries' `Changed` events (Task 1) and `IInventoryService.ContentsChanged` (Task 2);
  - `PuzzleStateRegistry.Changed` (Task 3).
- Produces:
  - `MilestoneTracker(IMilestoneWorldState, MilestoneRegistry, MilestoneGraph[])` : `IInitializable`, `IDisposable`
  - `IMilestoneService` with:
    - `bool IsCompleted(MilestoneRef)`
    - `bool IsCompleted(string)`
    - `event Action<MilestoneRef>? Completed`

- [ ] **Step 1: Write the failing tests**

`MilestoneTrackerTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Milestones;
using FakeWorld = CrimsonDraft.Tests.MilestoneTriggerEvaluatorTests.FakeWorld;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneTrackerTests
    {
        private MilestoneGraph graph = null!;
        private FakeWorld world = null!;
        private MilestoneRegistry registry = null!;
        private MilestoneNode pickupNode = null!;
        private MilestoneNode doorNode = null!;

        [SetUp]
        public void SetUp()
        {
            this.graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.pickupNode = this.graph.AddNode("Take Sheep Key", Vector2.zero);
            this.pickupNode.trigger = new MilestoneTrigger { kind = MilestoneTriggerKind.PickupCollected, targetId = "sheep_key" };
            this.doorNode = this.graph.AddNode("Unlock Lavatory", Vector2.one);
            this.doorNode.trigger = new MilestoneTrigger { kind = MilestoneTriggerKind.DoorUnlocked, targetId = "lavatory" };
            this.graph.AddNode("Unassigned", Vector2.right); // trigger None
            this.world = new FakeWorld();
            this.registry = new MilestoneRegistry();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(this.graph);

        private MilestoneTracker Build()
        {
            var tracker = new MilestoneTracker(this.world, this.registry, new[] { this.graph });
            ((IInitializable)tracker).Initialize();
            return tracker;
        }

        // Review Focus 2: condition already true at load -> completed in Initialize, once.
        [Test]
        public void Initialize_completesWhatAlreadyHolds_once()
        {
            this.world.Pickups.Add("sheep_key");
            int raised = 0;
            this.registry.Completed += _ => raised++;

            Build();

            Assert.IsTrue(this.registry.IsCompleted(this.pickupNode.id));
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SourceChanged_completesMatchingNode()
        {
            Build();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Doors);
            Assert.IsTrue(this.registry.IsCompleted(this.doorNode.id));
        }

        [Test]
        public void OtherSourceChanged_doesNotEvaluateUnrelatedKinds()
        {
            Build();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Pickups);
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
        }

        [Test]
        public void Completed_neverUncompletes()
        {
            this.world.Pickups.Add("sheep_key");
            Build();
            this.world.Pickups.Remove("sheep_key");
            this.world.Raise(MilestoneSource.Pickups);
            Assert.IsTrue(this.registry.IsCompleted(this.pickupNode.id));
        }

        [Test]
        public void Dispose_unsubscribes()
        {
            var tracker = Build();
            ((System.IDisposable)tracker).Dispose();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Doors);
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
        }
    }
}
```

`MilestoneWorldStateTests.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneWorldStateTests
    {
        private PickupRegistry pickups = null!;
        private NoteRegistry notes = null!;
        private ItemSocketStateRegistry sockets = null!;
        private DoorStateRegistry doors = null!;
        private PuzzleStateRegistry puzzles = null!;
        private FakeInventoryService inventory = null!;
        private MilestoneWorldState state = null!;
        private readonly List<MilestoneSource> raised = new();

        [SetUp]
        public void SetUp()
        {
            this.pickups = new PickupRegistry(); this.notes = new NoteRegistry(); this.sockets = new ItemSocketStateRegistry();
            this.doors = new DoorStateRegistry(); this.puzzles = new PuzzleStateRegistry(); this.inventory = new FakeInventoryService();
            this.state = new MilestoneWorldState(this.pickups, this.notes, this.sockets, this.doors, this.puzzles, this.inventory);
            this.raised.Clear();
            this.state.SourceChanged += s => this.raised.Add(s);
        }

        // Review Focus 3: empty / partial sockets are not complete.
        [Test]
        public void IsSocketComplete_requiresEverySlot()
        {
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new bool[0]);
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new[] { true, false });
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new[] { true, true });
            Assert.IsTrue(this.state.IsSocketComplete("s"));
        }

        [Test]
        public void EverySource_forwardsItsChange()
        {
            this.pickups.SetCollected("p");
            this.inventory.RaiseContentsChanged();
            this.sockets.SetInserted("s", new[] { true });
            this.doors.MarkUnlocked("d");
            this.notes.SetCollected("n");
            this.puzzles.SetSolved("z");

            CollectionAssert.AreEqual(new[]
            {
                MilestoneSource.Pickups, MilestoneSource.Inventory, MilestoneSource.Sockets,
                MilestoneSource.Doors, MilestoneSource.Notes, MilestoneSource.Puzzles,
            }, this.raised);
        }

        [Test]
        public void Dispose_stopsForwarding()
        {
            this.state.Dispose();
            this.pickups.SetCollected("p");
            Assert.AreEqual(0, this.raised.Count);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors for `MilestoneTracker` and `MilestoneWorldState`.

- [ ] **Step 3: Implement**

`MilestoneWorldState.cs`:

```csharp
#nullable enable

using System;
using System.Linq;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Milestones
{
    public sealed class MilestoneWorldState : IMilestoneWorldState
    {
        private readonly PickupRegistry          pickups;
        private readonly NoteRegistry            notes;
        private readonly ItemSocketStateRegistry sockets;
        private readonly DoorStateRegistry       doors;
        private readonly PuzzleStateRegistry     puzzles;
        private readonly IInventoryService       inventory;

        public event Action<MilestoneSource>? SourceChanged;

        [Preserve]
        public MilestoneWorldState(
            PickupRegistry pickups, NoteRegistry notes, ItemSocketStateRegistry sockets,
            DoorStateRegistry doors, PuzzleStateRegistry puzzles, IInventoryService inventory)
        {
            this.pickups = pickups; this.notes = notes; this.sockets = sockets;
            this.doors = doors; this.puzzles = puzzles; this.inventory = inventory;

            this.pickups.Changed           += OnPickups;
            this.inventory.ContentsChanged += OnInventory;
            this.sockets.Changed           += OnSockets;
            this.doors.Changed             += OnDoors;
            this.notes.Changed             += OnNotes;
            this.puzzles.Changed           += OnPuzzles;
        }

        public bool IsPickupCollected(string pickupId) => this.pickups.IsCollected(pickupId);
        public bool HasItem(string itemId)             => this.inventory.HasItem(itemId);
        public bool IsDoorUnlocked(string doorId)      => this.doors.IsUnlocked(doorId);
        public bool IsNoteRead(string noteId)          => this.notes.IsCollected(noteId);
        public bool IsPuzzleSolved(string puzzleId)    => this.puzzles.IsSolved(puzzleId);

        public bool IsSocketComplete(string socketId)
        {
            var slots = this.sockets.GetInserted(socketId);
            return slots.Length > 0 && slots.All(s => s);
        }

        private void OnPickups()   => SourceChanged?.Invoke(MilestoneSource.Pickups);
        private void OnInventory() => SourceChanged?.Invoke(MilestoneSource.Inventory);
        private void OnSockets()   => SourceChanged?.Invoke(MilestoneSource.Sockets);
        private void OnDoors()     => SourceChanged?.Invoke(MilestoneSource.Doors);
        private void OnNotes()     => SourceChanged?.Invoke(MilestoneSource.Notes);
        private void OnPuzzles()   => SourceChanged?.Invoke(MilestoneSource.Puzzles);

        public void Dispose()
        {
            this.pickups.Changed           -= OnPickups;
            this.inventory.ContentsChanged -= OnInventory;
            this.sockets.Changed           -= OnSockets;
            this.doors.Changed             -= OnDoors;
            this.notes.Changed             -= OnNotes;
            this.puzzles.Changed           -= OnPuzzles;
        }
    }
}
```

`MilestoneTracker.cs`:

```csharp
#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Milestones
{
    // Completes milestones whose trigger holds: every pending node once on Initialize (registered
    // after SaveGameLoader, so loaded state counts), then only the nodes tied to a source when that
    // source changes. Never un-completes.
    public sealed class MilestoneTracker : IInitializable, IDisposable
    {
        private readonly IMilestoneWorldState world;
        private readonly MilestoneRegistry    registry;
        private readonly MilestoneGraph[]     graphs;

        [Preserve]
        public MilestoneTracker(IMilestoneWorldState world, MilestoneRegistry registry, MilestoneGraph[] graphs)
        {
            this.world    = world;
            this.registry = registry;
            this.graphs   = graphs;
        }

        void IInitializable.Initialize()
        {
            Evaluate(null);
            this.world.SourceChanged += OnSourceChanged;
        }

        private void OnSourceChanged(MilestoneSource source) => Evaluate(source);

        // null = every kind.
        private void Evaluate(MilestoneSource? source)
        {
            foreach (var graph in this.graphs)
            {
                if (graph == null) continue;
                foreach (var node in graph.Nodes)
                {
                    if (this.registry.IsCompleted(node.id)) continue;
                    if (source.HasValue && MilestoneTriggerEvaluator.SourceOf(node.trigger.kind) != source.Value) continue;
                    if (MilestoneTriggerEvaluator.Holds(node.trigger, this.world))
                        this.registry.Complete(node.id);
                }
            }
        }

        void IDisposable.Dispose() => this.world.SourceChanged -= OnSourceChanged;
    }
}
```

`IMilestoneService.cs`:

```csharp
#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    public interface IMilestoneService
    {
        bool IsCompleted(MilestoneRef milestone);
        bool IsCompleted(string milestoneId);
        event Action<MilestoneRef>? Completed;
    }
}
```

`MilestoneService.cs`:

```csharp
#nullable enable

using System;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Milestones
{
    public sealed class MilestoneService : IMilestoneService, IDisposable
    {
        private readonly MilestoneRegistry registry;
        private readonly MilestoneGraph[]  graphs;

        public event Action<MilestoneRef>? Completed;

        [Preserve]
        public MilestoneService(MilestoneRegistry registry, MilestoneGraph[] graphs)
        {
            this.registry = registry;
            this.graphs   = graphs;
            this.registry.Completed += OnCompleted;
        }

        public bool IsCompleted(MilestoneRef milestone) => milestone.IsValid && this.registry.IsCompleted(milestone.nodeId);
        public bool IsCompleted(string milestoneId)    => this.registry.IsCompleted(milestoneId);

        private void OnCompleted(string id)
        {
            foreach (var graph in this.graphs)
                if (graph != null && graph.FindNode(id) != null)
                {
                    Completed?.Invoke(new MilestoneRef(graph, id));
                    return;
                }
        }

        public void Dispose() => this.registry.Completed -= OnCompleted;
    }
}
```

`NavigationScope.cs`:
- add `using CrimsonDraft.Navigation.Milestones;`;
- add the field next to `cachedPuzzles`:

```csharp
        // Assets, not scene objects: filled from the project by the cache button below.
        [SerializeField] private MilestoneGraph[]         cachedMilestoneGraphs = System.Array.Empty<MilestoneGraph>();
```

- after the `PuzzleBootstrap` registration (this is after `SaveGameLoader`, so the tracker initializes after the save is applied):

```csharp
            builder.RegisterInstance(this.cachedMilestoneGraphs);
            builder.Register<MilestoneWorldState>(Lifetime.Singleton).As<IMilestoneWorldState>();
            builder.Register<MilestoneTracker>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MilestoneService>(Lifetime.Singleton).AsImplementedInterfaces();
```

- inside `#if UNITY_EDITOR`:

```csharp
        [Button("Cache Milestone Graphs")]
        private void CacheMilestoneGraphs()
        {
            this.cachedMilestoneGraphs = UnityEditor.AssetDatabase.FindAssets("t:MilestoneGraph")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<MilestoneGraph>)
                .Where(g => g != null)
                .ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
```

- in `CacheEverything()`, call `CacheMilestoneGraphs();` and append `{this.cachedMilestoneGraphs.Length} milestone graphs` to the log line.

- [ ] **Step 4: Run tests**

Expected: `MilestoneTrackerTests` 5/5 and `MilestoneWorldStateTests` 3/3 pass; the full suite stays green.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Milestones Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs Game/CrimsonDraft/Assets/Tests/EditMode/MilestoneTrackerTests.cs* Game/CrimsonDraft/Assets/Tests/EditMode/MilestoneWorldStateTests.cs*
git commit -m "feat(navigation): track milestone completion and expose IMilestoneService"
```

---

### Task 9: Editor window — canvas, nodes, edges, persistence

**Files:**
- Modify: `Scripts/Navigation/Editor/CrimsonDraft.Navigation.Editor.asmdef` (add `"CrimsonDraft.Inventory"` to `references`)
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneGraphWindow.cs`
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneGraphView.cs`
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneNodeView.cs`
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneEdgeView.cs`

**Interfaces:**
- Consumes: `MilestoneGraph` and the data types (Task 6).
- Produces: `MilestoneGraphView(MilestoneGraph graph)`, with `void Load()` (rebuilds the view from the asset) and `void Save()` (writes the view back to the asset with Undo). Task 10 adds notes, frames and lanes to both methods; Task 11 adds the trigger UI to `MilestoneNodeView`.

Persistence model: **the asset is the only source of truth.**
- Every view change schedules `Save()`, which rebuilds `Nodes` positions, `Edges`, `Notes` and `Frames` from the view after `Undo.RecordObject(graph, "Edit Milestone Graph")`, then calls `EditorUtility.SetDirty(graph)`.
- `Undo.undoRedoPerformed` calls `Load()`.

- [ ] **Step 1: Implement the window**

`MilestoneGraphWindow.cs`:

```csharp
#nullable enable

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    public sealed class MilestoneGraphWindow : EditorWindow
    {
        [SerializeField] private MilestoneGraph? graph;
        private MilestoneGraphView? view;

        [MenuItem("Tools/CrimsonDraft/Milestone Graph")]
        public static void Open() => GetWindow<MilestoneGraphWindow>("Milestone Graph");

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not MilestoneGraph asset) return false;
            var window = GetWindow<MilestoneGraphWindow>("Milestone Graph");
            window.SetGraph(asset);
            return true;
        }

        private void CreateGUI() => Rebuild();

        private void OnEnable()  => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo() => this.view?.Load();

        private void SetGraph(MilestoneGraph? asset)
        {
            this.graph = asset;
            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            var field = new ObjectField { objectType = typeof(MilestoneGraph), value = this.graph };
            field.RegisterValueChangedCallback(e => SetGraph(e.newValue as MilestoneGraph));
            toolbar.Add(field);
            toolbar.Add(new ToolbarButton(() => this.view?.FrameAll()) { text = "Frame All" });
            rootVisualElement.Add(toolbar);

            if (this.graph == null)
            {
                rootVisualElement.Add(new HelpBox("Pick or create a Milestone Graph asset (Create > CrimsonDraft > Milestone Graph).", HelpBoxMessageType.Info));
                return;
            }

            this.view = new MilestoneGraphView(this.graph);
            this.view.StretchToParentSize();
            var host = new VisualElement { style = { flexGrow = 1 } };
            host.Add(this.view);
            rootVisualElement.Add(host);
            this.view.Load();
        }
    }
}
```

- [ ] **Step 2: Implement the view, node and edge**

`MilestoneGraphView.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    public sealed class MilestoneGraphView : GraphView
    {
        private readonly MilestoneGraph graph;
        private bool loading;
        private bool saveScheduled;

        public MilestoneGraph Graph => this.graph;

        public MilestoneGraphView(MilestoneGraph graph)
        {
            this.graph = graph;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            this.AddManipulator(new ContextualMenuManipulator(BuildCanvasMenu));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
            ports.Where(p => p.direction != startPort.direction && p.node != startPort.node).ToList();

        private void BuildCanvasMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is not GraphView) return;
            Vector2 local = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Add Milestone", _ =>
            {
                Undo.RecordObject(this.graph, "Add Milestone");
                var node = this.graph.AddNode("New milestone", local);
                EditorUtility.SetDirty(this.graph);
                AddElement(new MilestoneNodeView(node, this));
            });
        }

        public void Load()
        {
            this.loading = true;
            DeleteElements(graphElements.ToList());

            var views = new Dictionary<string, MilestoneNodeView>();
            foreach (var node in this.graph.Nodes)
            {
                var view = new MilestoneNodeView(node, this);
                views[node.id] = view;
                AddElement(view);
            }

            foreach (var edge in this.graph.Edges)
            {
                if (!views.TryGetValue(edge.fromNodeId, out var from) || !views.TryGetValue(edge.toNodeId, out var to)) continue;
                var view = from.Output.ConnectTo<MilestoneEdgeView>(to.Input);
                view.Bind(edge, this);
                AddElement(view);
            }

            this.loading = false;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (this.loading) return change;

            if (change.edgesToCreate != null)
                foreach (var edge in change.edgesToCreate.OfType<MilestoneEdgeView>())
                    edge.Bind(new MilestoneEdge(), this);

            ScheduleSave();
            return change;
        }

        public void ScheduleSave()
        {
            if (this.loading || this.saveScheduled) return;
            this.saveScheduled = true;
            schedule.Execute(() => { this.saveScheduled = false; Save(); });
        }

        // Rebuilds the asset's node positions and edge list from the view.
        public void Save()
        {
            Undo.RecordObject(this.graph, "Edit Milestone Graph");

            var nodeViews = nodes.OfType<MilestoneNodeView>().ToList();
            var alive = new HashSet<string>(nodeViews.Select(v => v.Node.id));
            foreach (var id in this.graph.Nodes.Select(n => n.id).Where(id => !alive.Contains(id)).ToList())
                this.graph.RemoveNode(id);
            foreach (var view in nodeViews)
                view.Node.position = view.GetPosition().position;

            this.graph.Edges.Clear();
            foreach (var edge in edges.OfType<MilestoneEdgeView>())
            {
                if (edge.output?.node is not MilestoneNodeView from || edge.input?.node is not MilestoneNodeView to) continue;
                edge.Data.fromNodeId = from.Node.id;
                edge.Data.toNodeId   = to.Node.id;
                this.graph.Edges.Add(edge.Data);
            }

            EditorUtility.SetDirty(this.graph);
        }

        public Color LaneColor(string laneId) => this.graph.FindLane(laneId)?.color ?? new Color(0.35f, 0.35f, 0.35f);
    }
}
```

`MilestoneNodeView.cs`:

```csharp
#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    public sealed class MilestoneNodeView : Node
    {
        public MilestoneNode Node { get; }
        public Port Input  { get; }
        public Port Output { get; }

        private readonly MilestoneGraphView owner;

        public MilestoneNodeView(MilestoneNode node, MilestoneGraphView owner)
        {
            this.Node  = node;
            this.owner = owner;
            title = node.title;
            SetPosition(new Rect(node.position, new Vector2(220f, 120f)));

            // Port.Create<MilestoneEdgeView> (not InstantiatePort): the port's edge connector builds
            // edges of this type, so edges the user drags are MilestoneEdgeViews and get saved.
            Input  = Port.Create<MilestoneEdgeView>(Orientation.Horizontal, Direction.Input,  Port.Capacity.Multi, typeof(bool));
            Output = Port.Create<MilestoneEdgeView>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Input.portName = ""; Output.portName = "";
            inputContainer.Add(Input);
            outputContainer.Add(Output);

            var titleField = new TextField { value = node.title };
            titleField.RegisterValueChangedCallback(e =>
            {
                this.Node.title = e.newValue;
                title = e.newValue;
                this.owner.ScheduleSave();
            });
            extensionContainer.Add(titleField);

            ApplyLaneColor();
            RefreshExpandedState();
            RefreshPorts();
        }

        public void ApplyLaneColor() =>
            titleContainer.style.backgroundColor = this.owner.LaneColor(this.Node.laneId);

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            this.owner?.ScheduleSave();
        }
    }
}
```

`MilestoneEdgeView.cs`:

```csharp
#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Colour from its lane (or a custom colour); Dashed edges hide the stock line and draw dashes
    // along the same control points. Documentation only.
    public sealed class MilestoneEdgeView : Edge
    {
        public MilestoneEdge Data { get; private set; } = new();
        private MilestoneGraphView? owner;
        private readonly VisualElement dashes = new() { pickingMode = PickingMode.Ignore };

        public MilestoneEdgeView()
        {
            this.dashes.StretchToParentSize();
            this.dashes.generateVisualContent += DrawDashes;
            edgeControl.Add(this.dashes);
        }

        public void Bind(MilestoneEdge data, MilestoneGraphView view)
        {
            this.Data  = data;
            this.owner = view;
            UpdateEdgeControl();
        }

        private Color DataColor =>
            this.Data.useCustomColor || this.owner == null ? this.Data.customColor : this.owner.LaneColor(this.Data.laneId);

        public override bool UpdateEdgeControl()
        {
            bool result = base.UpdateEdgeControl();
            if (!selected)
            {
                var color = this.Data.style == EdgeStyle.Dashed ? Color.clear : DataColor;
                edgeControl.inputColor  = color;
                edgeControl.outputColor = color;
            }
            this.dashes.MarkDirtyRepaint();
            return result;
        }

        private void DrawDashes(MeshGenerationContext ctx)
        {
            if (this.Data.style != EdgeStyle.Dashed || edgeControl.controlPoints == null) return;
            var points = edgeControl.controlPoints;
            var painter = ctx.painter2D;
            painter.strokeColor = selected ? Color.white : DataColor;
            painter.lineWidth = 2f;
            const float dash = 8f, gap = 6f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float length = Vector2.Distance(a, b);
                Vector2 dir = length > 0f ? (b - a) / length : Vector2.zero;
                for (float t = 0f; t < length; t += dash + gap)
                {
                    painter.BeginPath();
                    painter.MoveTo(a + dir * t);
                    painter.LineTo(a + dir * Mathf.Min(t + dash, length));
                    painter.Stroke();
                }
            }
        }
    }
}
```

`CrimsonDraft.Navigation.Editor.asmdef`: change `"references"` to `["CrimsonDraft.Navigation", "CrimsonDraft.Infrastructure", "CrimsonDraft.Inventory"]`.

- [ ] **Step 3: Compile**

Run: `refresh_unity` (compile) + `read_console` filtered on `error CS`.
Expected: no errors. If `Edge.UpdateEdgeControl` is not virtual in this Unity version, move the colour code into an override of `OnCustomStyleResolved`, or into a `schedule.Execute` callback after binding, and note it in the commit.

- [ ] **Step 4: Manual check (user)**

1. Create `Create > CrimsonDraft > Milestone Graph` in `Assets/Data/Milestones/` and double-click it. The window opens.
2. Right-click the canvas, choose **Add Milestone** twice, and drag from the right port of one node to the left port of the other. An edge appears.
3. Rename a node, move both, and close and reopen the window. Names, positions and the edge persist.
4. Press Ctrl+Z. The last change reverts and the view reloads.
5. Delete a node. Its edge disappears, and the asset's `Edges` list (Inspector, Debug mode) has no dangling entry.

- [ ] **Step 5: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/CrimsonDraft.Navigation.Editor.asmdef Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/Milestones*
git commit -m "feat(editor): add Milestone Graph window with nodes and edges"
```

---

### Task 10: Editor — lanes, edge styles, notes and frames

**Files:**
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneGraphWindow.cs` (toolbar)
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneGraphView.cs` (`Load`, `Save`, canvas menu)
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneNodeView.cs` (lane dropdown)
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneEdgeView.cs` (context menu)

**Interfaces:**
- Consumes: Task 9 views.
- Produces: nothing new for other tasks.

- [ ] **Step 1: Lanes**
  - Add a toolbar button **Edit Lanes**. It sets `Selection.activeObject = graph`, so lanes are edited in the asset's Inspector (the `Lanes` list: name, colour), and calls `view.Load()` on window focus (`OnFocus`) so colour changes show.
  - In `MilestoneGraphView.BuildCanvasMenu`, add **Add Lane**. It appends `new MilestoneLane { id = MilestoneGraph.NewId(), name = $"Lane {n}", color = Color.HSVToRGB(Random.value, 0.6f, 0.9f) }` under Undo and reloads.
  - In `MilestoneNodeView`, under the title field, add:

```csharp
            var laneNames = owner.Graph.Lanes.Select(l => l.name).Prepend("(none)").ToList();
            int current = owner.Graph.Lanes.FindIndex(l => l.id == node.laneId) + 1;
            var lane = new PopupField<string>("Lane", laneNames, current);
            lane.RegisterValueChangedCallback(e =>
            {
                int index = laneNames.IndexOf(e.newValue) - 1;
                this.Node.laneId = index >= 0 ? owner.Graph.Lanes[index].id : "";
                ApplyLaneColor();
                owner.ScheduleSave();
            });
            extensionContainer.Add(lane);
```

  (add `using System.Linq;` and `using UnityEditor.UIElements;`).

- [ ] **Step 2: Edge context menu.** In the `MilestoneEdgeView` constructor:

```csharp
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Style/Solid",  _ => SetStyle(EdgeStyle.Solid),  _ => this.Data.style == EdgeStyle.Solid  ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Style/Dashed", _ => SetStyle(EdgeStyle.Dashed), _ => this.Data.style == EdgeStyle.Dashed ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                if (this.owner == null) return;
                evt.menu.AppendAction("Lane/(none)", _ => SetLane(""));
                foreach (var lane in this.owner.Graph.Lanes)
                {
                    var id = lane.id;
                    evt.menu.AppendAction($"Lane/{lane.name}", _ => SetLane(id));
                }
            }));
```

with:

```csharp
        private void SetStyle(EdgeStyle style) { this.Data.style = style; UpdateEdgeControl(); this.owner?.ScheduleSave(); }
        private void SetLane(string laneId)    { this.Data.laneId = laneId; this.Data.useCustomColor = false; UpdateEdgeControl(); this.owner?.ScheduleSave(); }
```

- [ ] **Step 3: Notes (`StickyNote`).**
  - Canvas menu **Add Note** creates a `MilestoneNote { id = NewId(), text = "Note", rect = new Rect(local, new Vector2(200, 120)) }` and calls `Load()`.
  - In `Load()`, for each `graph.Notes`, add the view below:

```csharp
                var sticky = new StickyNote(note.rect.position) { title = "", contents = note.text, userData = note };
                sticky.SetPosition(note.rect);
                sticky.RegisterCallback<StickyNoteChangeEvent>(_ => ScheduleSave());
                AddElement(sticky);
```

  - In `Save()`, rebuild `graph.Notes` from `graphElements.OfType<StickyNote>()`: `(MilestoneNote)userData` with `text = contents` and `rect = GetPosition()`.

- [ ] **Step 4: Frames (`Group`).**
  - Canvas menu **Add Frame around selection** creates a `MilestoneFrame { id = NewId(), title = "Frame", nodeIds = selected node ids }` and calls `Load()`.
  - In `Load()`, for each frame:

```csharp
                var group = new Group { title = frame.title, userData = frame };
                group.style.backgroundColor = LaneColor(frame.laneId) * new Color(1f, 1f, 1f, 0.15f);
                AddElement(group);
                foreach (var id in frame.nodeIds)
                    if (views.TryGetValue(id, out var member)) group.AddElement(member);
```

  - In the constructor, wire `elementsAddedToGroup`, `elementsRemovedFromGroup` and `groupTitleChanged` to `(…) => ScheduleSave()`.
  - In `Save()`, rebuild `graph.Frames` from `graphElements.OfType<Group>()`: `(MilestoneFrame)userData` with `title = group.title` and `nodeIds = group.containedElements.OfType<MilestoneNodeView>().Select(v => v.Node.id).ToList()`.
  - A frame's lane is set from its context menu (`Lane/…`), the same way as edges.

- [ ] **Step 5: Copy/paste nodes.** In the `MilestoneGraphView` constructor (`EditorJsonUtility` keeps the `ItemData` / `DocumentData` references; pasted nodes get new IDs so saves never alias them):

```csharp
            serializeGraphElements = elements => string.Join("\n",
                elements.OfType<MilestoneNodeView>().Select(v => EditorJsonUtility.ToJson(v.Node)));
            canPasteSerializedData = data => !string.IsNullOrEmpty(data);
            unserializeAndPaste = (_, data) =>
            {
                Undo.RecordObject(this.graph, "Paste Milestones");
                foreach (var line in data.Split('\n'))
                {
                    var copy = new MilestoneNode();
                    EditorJsonUtility.FromJsonOverwrite(line, copy);
                    copy.id = MilestoneGraph.NewId();
                    copy.position += new Vector2(30f, 30f);
                    this.graph.Nodes.Add(copy);
                }
                EditorUtility.SetDirty(this.graph);
                Load();
            };
```

- [ ] **Step 6: Compile + manual check (user)**
  1. Add two lanes (cyan, magenta) in the Inspector and assign them to nodes. The headers take the colours.
  2. Right-click an edge and choose **Style/Dashed** and **Lane/magenta**. It is drawn dashed and magenta.
  3. Add a note "Player choice". Add a frame around two selected nodes and rename it.
  4. Close and reopen the window. Everything persists, and Ctrl+Z undoes each step.
  5. Select two nodes, then press Ctrl+C and Ctrl+V. Copies appear offset, keeping their triggers, each with a new ID.

- [ ] **Step 7: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/Milestones
git commit -m "feat(editor): lanes, dashed edges, notes and frames in Milestone Graph"
```

---

### Task 11: Editor — trigger pickers, validation, search and `MilestoneRef` drawer

**Files:**
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneTriggerField.cs`
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneSceneIds.cs`
- Create: `Scripts/Navigation/Editor/Milestones/MilestoneRefDrawer.cs`
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneNodeView.cs` (add trigger field + badge)
- Modify: `Scripts/Navigation/Editor/Milestones/MilestoneGraphWindow.cs` (search field)

**Interfaces:**
- Consumes: Task 6 data, Task 5 `IPuzzle`, and the existing `PickupInteractable.PickupId`, `MapPickupInteractable.PickupId`, `ItemSocketInteractable.SocketId`, `RoomDoorInteractable.DoorId` and `SceneDoorInteractable.DoorId`.
- Produces: `static class MilestoneSceneIds` with `IReadOnlyList<string> For(MilestoneTriggerKind kind)`, which collects the IDs from open scenes.

- [ ] **Step 1: Scene ID lookup**

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Editor-only scan of the open scenes, used by the trigger picker and the validation badge.
    public static class MilestoneSceneIds
    {
        public static IReadOnlyList<string> For(MilestoneTriggerKind kind)
        {
            IEnumerable<string> ids = kind switch
            {
                MilestoneTriggerKind.PickupCollected =>
                    Find<PickupInteractable>().Select(p => p.PickupId).Concat(Find<MapPickupInteractable>().Select(p => p.PickupId)),
                MilestoneTriggerKind.SocketCompleted => Find<ItemSocketInteractable>().Select(s => s.SocketId),
                MilestoneTriggerKind.DoorUnlocked    =>
                    Find<RoomDoorInteractable>().Select(d => d.DoorId).Concat(Find<SceneDoorInteractable>().Select(d => d.DoorId)),
                MilestoneTriggerKind.PuzzleSolved    =>
                    Find<MonoBehaviour>().OfType<IPuzzle>().Select(p => p.PuzzleId),
                _ => Enumerable.Empty<string>(),
            };
            return ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id).ToList();
        }

        private static T[] Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
```

- [ ] **Step 2: Trigger field**

`MilestoneTriggerField.cs`:

```csharp
#nullable enable

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Kind dropdown + the ID picker that kind needs. Calls onChanged after every edit.
    public sealed class MilestoneTriggerField : VisualElement
    {
        private readonly MilestoneTrigger trigger;
        private readonly Action onChanged;
        private readonly VisualElement body = new();

        public MilestoneTriggerField(MilestoneTrigger trigger, Action onChanged)
        {
            this.trigger   = trigger;
            this.onChanged = onChanged;

            var kind = new EnumField("Trigger", trigger.kind);
            kind.RegisterValueChangedCallback(e =>
            {
                this.trigger.kind = (MilestoneTriggerKind)e.newValue;
                RebuildBody();
                this.onChanged();
            });
            Add(kind);
            Add(this.body);
            RebuildBody();
        }

        private void RebuildBody()
        {
            this.body.Clear();
            switch (this.trigger.kind)
            {
                case MilestoneTriggerKind.ItemHeld:
                {
                    var field = new ObjectField("Item") { objectType = typeof(ItemData), value = this.trigger.item };
                    field.RegisterValueChangedCallback(e => { this.trigger.item = e.newValue as ItemData; this.onChanged(); });
                    this.body.Add(field);
                    break;
                }
                case MilestoneTriggerKind.NoteRead:
                {
                    var field = new ObjectField("Note") { objectType = typeof(DocumentData), value = this.trigger.note };
                    field.RegisterValueChangedCallback(e => { this.trigger.note = e.newValue as DocumentData; this.onChanged(); });
                    this.body.Add(field);
                    break;
                }
                case MilestoneTriggerKind.PickupCollected:
                case MilestoneTriggerKind.SocketCompleted:
                case MilestoneTriggerKind.DoorUnlocked:
                case MilestoneTriggerKind.PuzzleSolved:
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var text = new TextField("Id") { value = this.trigger.targetId, style = { flexGrow = 1 } };
                    text.RegisterValueChangedCallback(e => { this.trigger.targetId = e.newValue; this.onChanged(); });
                    var pick = new Button(() => ShowIdMenu(text)) { text = "…" };
                    row.Add(text);
                    row.Add(pick);
                    this.body.Add(row);
                    break;
                }
            }
        }

        private void ShowIdMenu(TextField text)
        {
            var menu = new GenericMenu();
            var ids = MilestoneSceneIds.For(this.trigger.kind);
            if (ids.Count == 0)
                menu.AddDisabledItem(new UnityEngine.GUIContent("No ids in open scenes"));
            foreach (var id in ids)
            {
                var captured = id;
                menu.AddItem(new UnityEngine.GUIContent(captured), captured == this.trigger.targetId, () => text.value = captured);
            }
            menu.ShowAsContext();
        }
    }
}
```

- [ ] **Step 3: Validation badge on `MilestoneNodeView`**

Add these usings: `System.Linq`, `UnityEditor`. Then, in the constructor, after the lane popup:

```csharp
            this.badge = new Label { style = { color = new Color(1f, 0.8f, 0.2f), unityFontStyleAndWeight = FontStyle.Bold } };
            titleContainer.Add(this.badge);
            extensionContainer.Add(new MilestoneTriggerField(node.trigger, () =>
            {
                RefreshBadge();
                owner.ScheduleSave();
            }));
            RefreshBadge();
```

and these members:

```csharp
        private readonly Label badge;

        public void RefreshBadge()
        {
            string problem = FindProblem();
            this.badge.text    = problem.Length > 0 ? "⚠" : "";
            this.badge.tooltip = problem;
        }

        private string FindProblem()
        {
            var t = this.Node.trigger;
            switch (t.kind)
            {
                case MilestoneTriggerKind.None:
                    return "No trigger";
                case MilestoneTriggerKind.ItemHeld when t.item == null:
                case MilestoneTriggerKind.NoteRead when t.note == null:
                    return "No asset assigned";
                case MilestoneTriggerKind.PickupCollected:
                case MilestoneTriggerKind.SocketCompleted:
                case MilestoneTriggerKind.DoorUnlocked:
                case MilestoneTriggerKind.PuzzleSolved:
                    if (!MilestoneSceneIds.For(t.kind).Contains(t.targetId)) return "Id not found in open scenes";
                    break;
            }

            var twin = this.owner.Graph.Nodes.FirstOrDefault(n => n != this.Node && n.trigger.kind == t.kind
                && n.trigger.targetId == t.targetId && n.trigger.item == t.item && n.trigger.note == t.note);
            if (twin != null) return $"Same trigger as '{twin.title}'";

            foreach (var guid in AssetDatabase.FindAssets("t:MilestoneGraph"))
            {
                var other = AssetDatabase.LoadAssetAtPath<MilestoneGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (other != null && other != this.owner.Graph && other.FindNode(this.Node.id) != null)
                    return $"Duplicate id in {other.name}";
            }
            return "";
        }
```

- [ ] **Step 4: Search.** In `MilestoneGraphWindow.Rebuild`, after the Frame All button:

```csharp
            var search = new ToolbarSearchField();
            search.RegisterValueChangedCallback(e =>
            {
                if (this.view == null || string.IsNullOrWhiteSpace(e.newValue)) return;
                var match = this.view.nodes.OfType<MilestoneNodeView>()
                    .FirstOrDefault(n => n.Node.title.IndexOf(e.newValue, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (match == null) return;
                this.view.ClearSelection();
                this.view.AddToSelection(match);
                this.view.FrameSelection();
            });
            toolbar.Add(search);
```

(add `using System.Linq;`).

- [ ] **Step 5: `MilestoneRefDrawer`**

```csharp
#nullable enable

using System.Linq;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    [CustomPropertyDrawer(typeof(MilestoneRef))]
    public sealed class MilestoneRefDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var graphProp = property.FindPropertyRelative("graph");
            var idProp    = property.FindPropertyRelative("nodeId");
            float line = EditorGUIUtility.singleLineHeight;

            EditorGUI.BeginProperty(position, label, property);
            var top = new Rect(position.x, position.y, position.width, line);
            EditorGUI.PropertyField(top, graphProp, label);

            var bottom = new Rect(position.x, position.y + line + EditorGUIUtility.standardVerticalSpacing, position.width, line);
            var graph = graphProp.objectReferenceValue as MilestoneGraph;
            var nodes = graph != null ? graph.Nodes : new System.Collections.Generic.List<MilestoneNode>();
            var titles = nodes.Select(n => n.title).Prepend("(none)").ToArray();
            int current = nodes.FindIndex(n => n.id == idProp.stringValue) + 1;
            int chosen  = EditorGUI.Popup(EditorGUI.IndentedRect(bottom), " ", current, titles);
            if (chosen != current)
                idProp.stringValue = chosen > 0 ? nodes[chosen - 1].id : "";
            EditorGUI.EndProperty();
        }
    }
}
```

- [ ] **Step 6: Compile + manual check (user)**
  1. On a node, set **Pickup Collected** and press "…". The pickups of the open Deck B scene are listed, and choosing one fills the ID with no badge.
  2. Type a wrong ID. The ⚠ badge appears, with the tooltip "Id not found in open scenes".
  3. Give two nodes the same trigger. Both show "Same trigger as …".
  4. Search "Pig". The view frames the "Acquire Pig Key" node.
  5. Add `[SerializeField] MilestoneRef test;` to any MonoBehaviour temporarily. The Inspector shows the graph field plus a title dropdown. Remove the field afterwards.

- [ ] **Step 7: Commit (propose first)**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/Milestones
git commit -m "feat(editor): milestone trigger pickers, validation badges, search and MilestoneRef drawer"
```

---

### Task 12: End-to-end check in Play Mode (user)

- [ ] **Step 1:** In the Deck B graph, add the node "Take Sheep Key in Save Room" with **Pickup Collected** set to the Sheep Key pickup.
- [ ] **Step 2:** On the Deck B `NavigationScope`, press **Cache Everything**. The log lists 1+ milestone graphs and the puzzles. Save the scene.
- [ ] **Step 3:** Enter Play Mode and pick up the Sheep Key. Then, in the Save Manager or the debugger, confirm `MilestoneRegistry.CompletedIds` contains the node ID.
- [ ] **Step 4:** Save at the telegraph, stop, then load that slot. The milestone is still completed, and `Completed` is not raised again.
- [ ] **Step 5:** Run the full EditMode suite one last time. Expected: all green.
