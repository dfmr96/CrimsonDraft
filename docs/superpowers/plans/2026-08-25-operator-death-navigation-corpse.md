# Operator Death Navigation Corpse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When an operator dies in combat, a persistent corpse (shared operator model, death pose) appears in Navigation at the exact spot the player was standing when that combat happened, survives room re-entry and save/load.

**Architecture:** A new Navigation-side bootstrap (`OperatorCorpseBootstrap`) subscribes to the existing `CombatEndedEvent` and diffs the roster's alive/dead state against a new registry (`OperatorCorpseRegistry`), following the project's existing registry pattern (`EnemyStateRegistry`/`RoomStateRegistry`, bundled in `WorldStateRegistries`, round-tripped through `SaveGameData`/`SaveController`/`SaveGameLoader`). A corpse is spawned as a child GameObject of the `RoomController` it belongs to, so the room's existing `Activate()`/`Deactivate()` (`gameObject.SetActive`) automatically shows/hides it — no separate spawn-on-transition logic needed. Combat scripts are untouched.

**Tech Stack:** C# / Unity, VContainer (DI), MessagePipe (pub/sub), NUnit EditMode tests (plain fakes, no mocking framework).

**Spec:** `docs/superpowers/specs/2026-08-25-operator-death-navigation-corpse-design.md`

## Global Constraints

- `OperatorCorpseRegistry` and the other five world-state registries are registered in **`GameLifetimeScope`** (root, `DontDestroyOnLoad`), not `NavigationScope` — confirmed by reading `Assets/Scripts/Infrastructure/GameLifetimeScope.cs:47-56`. `OperatorCorpseSettings`/`OperatorCorpseSpawner`/`OperatorCorpseBootstrap` are Navigation-only and belong in `NavigationScope`.
- `OperatorCorpseBootstrap` must be registered in `NavigationScope` **after** `SaveGameLoader` (same reasoning already applied to `EnemyBootstrap`): on a loaded save, the registry must reflect previously-recorded deaths before this bootstrap starts listening for new ones.
- Follow existing code style exactly: `#nullable enable` at the top of every file, `this.` prefix on member access, `[Preserve]` on constructors DI resolves, sealed classes, plain C# fakes per test file (no shared mocking framework/helper — every existing test file, e.g. `SaveGameLoaderTests` and `SaveControllerTests`, defines its own private `FakeRoster`/`FakeRoomOrchestrator` rather than sharing one).
- Do **not** add a `Co-Authored-By` trailer to any commit (project convention, `CLAUDE.md`).

---

### Task 1: `OperatorCorpseRegistry`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/OperatorCorpseRegistry.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseRegistryTests.cs`

**Interfaces:**
- Produces: `CrimsonDraft.Infrastructure.OperatorCorpseRegistry` with nested `readonly struct Entry(int slotIndex, string roomId, Vector3 position, Quaternion rotation)` (public `SlotIndex`/`RoomId`/`Position`/`Rotation` properties), and methods `bool IsRecorded(int slotIndex)`, `void Record(int slotIndex, string roomId, Vector3 position, Quaternion rotation)`, `IReadOnlyCollection<Entry> GetAll()`, `void LoadState(IEnumerable<Entry> saved)`, `void ClearAll()`.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseRegistryTests
    {
        [Test]
        public void Record_marksSlotAsRecorded()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(1, "room-a", new Vector3(1f, 0f, 2f), Quaternion.identity);

            Assert.IsTrue(registry.IsRecorded(1));
        }

        [Test]
        public void Record_calledTwiceForSameSlot_keepsFirstEntry()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(1, "room-a", new Vector3(1f, 0f, 0f), Quaternion.identity);
            registry.Record(1, "room-b", new Vector3(9f, 0f, 0f), Quaternion.identity);

            var entry = registry.GetAll().Single(e => e.SlotIndex == 1);
            Assert.AreEqual("room-a", entry.RoomId);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), entry.Position);
        }

        [Test]
        public void LoadState_restoresRecordedSlots()
        {
            var registry = new OperatorCorpseRegistry();
            registry.LoadState(new[]
            {
                new OperatorCorpseRegistry.Entry(2, "room-c", new Vector3(3f, 0f, 4f), Quaternion.identity),
            });

            Assert.IsTrue(registry.IsRecorded(2));
            var entry = registry.GetAll().Single();
            Assert.AreEqual("room-c", entry.RoomId);
        }

        [Test]
        public void ClearAll_removesAllRecordedSlots()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(0, "room-a", Vector3.zero, Quaternion.identity);
            registry.ClearAll();

            Assert.IsFalse(registry.IsRecorded(0));
            Assert.AreEqual(0, registry.GetAll().Count);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Test Runner (Window → General → Test Runner → EditMode), filter to `OperatorCorpseRegistryTests`, or via the UnityMCP `run_tests` tool with `filter: "OperatorCorpseRegistryTests"`.
Expected: FAIL/compile error — `OperatorCorpseRegistry` does not exist yet.

- [ ] **Step 3: Write the implementation**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class OperatorCorpseRegistry
    {
        public readonly struct Entry
        {
            public int        SlotIndex { get; }
            public string     RoomId    { get; }
            public Vector3    Position  { get; }
            public Quaternion Rotation  { get; }

            public Entry(int slotIndex, string roomId, Vector3 position, Quaternion rotation)
            {
                SlotIndex = slotIndex;
                RoomId    = roomId;
                Position  = position;
                Rotation  = rotation;
            }
        }

        private readonly Dictionary<int, Entry> recorded = new();

        [Preserve]
        public OperatorCorpseRegistry() { }

        public bool IsRecorded(int slotIndex) => this.recorded.ContainsKey(slotIndex);

        public void Record(int slotIndex, string roomId, Vector3 position, Quaternion rotation)
        {
            if (this.recorded.ContainsKey(slotIndex)) return;
            this.recorded[slotIndex] = new Entry(slotIndex, roomId, position, rotation);
        }

        public IReadOnlyCollection<Entry> GetAll() => this.recorded.Values;

        public void LoadState(IEnumerable<Entry> saved)
        {
            this.recorded.Clear();
            foreach (var entry in saved)
                this.recorded[entry.SlotIndex] = entry;
        }

        public void ClearAll() => this.recorded.Clear();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same filter as Step 2. Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/OperatorCorpseRegistry.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseRegistryTests.cs
git commit -m "feat(navigation): add OperatorCorpseRegistry for tracking dead-operator corpses"
```

---

### Task 2: Wire `OperatorCorpseRegistry` into `SaveGameData` / `WorldStateRegistries` / `GameLifetimeScope`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs:54`
- Modify (compile fix only, no new assertions yet): `Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs:20`, `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs:111-113,162-164`, `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs:134-136`

**Interfaces:**
- Consumes: `OperatorCorpseRegistry` (Task 1).
- Produces: `WorldStateRegistries.OperatorCorpses` (type `OperatorCorpseRegistry`), `SaveGameData.OperatorCorpseEntry` (fields `int slotIndex`, `string roomId`, `Vector3 position`, `Quaternion rotation`), `SaveGameData.operatorCorpses` (`List<OperatorCorpseEntry>`). `WorldStateRegistries`'s constructor gains a 7th positional parameter `OperatorCorpseRegistry operatorCorpses` (appended last) — every existing call site must be updated.

- [ ] **Step 1: Add `OperatorCorpseEntry` and the `operatorCorpses` list to `SaveGameData`**

In `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs`, add a new serializable entry type after `RoomStateEntry` (currently ends at line 21):

```csharp
[Serializable]
public sealed class OperatorCorpseEntry
{
    public int        slotIndex;
    public string     roomId = "";
    public Vector3    position;
    public Quaternion rotation = Quaternion.identity;
}
```

And add a new list field to `SaveGameData`, alongside `defeatedEnemyIds` (currently line 57):

```csharp
public List<OperatorCorpseEntry> operatorCorpses = new List<OperatorCorpseEntry>();
```

- [ ] **Step 2: Add `OperatorCorpses` to `WorldStateRegistries`**

Replace the full contents of `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs` with:

```csharp
#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    /// <summary>
    /// Bundles the seven cross-scene world-state registries that Save/Load and New-Game-reset
    /// all need together, so consumers don't carry seven separate constructor parameters.
    /// </summary>
    public sealed class WorldStateRegistries
    {
        public readonly DoorStateRegistry      Doors;
        public readonly RoomStateRegistry      Rooms;
        public readonly PickupRegistry         Pickups;
        public readonly NoteRegistry           Notes;
        public readonly KnownMapsRegistry      KnownMaps;
        public readonly EnemyStateRegistry     Enemies;
        public readonly OperatorCorpseRegistry OperatorCorpses;

        [Preserve]
        public WorldStateRegistries(
            DoorStateRegistry      doors,
            RoomStateRegistry      rooms,
            PickupRegistry         pickups,
            NoteRegistry           notes,
            KnownMapsRegistry      knownMaps,
            EnemyStateRegistry     enemies,
            OperatorCorpseRegistry operatorCorpses)
        {
            Doors           = doors;
            Rooms           = rooms;
            Pickups         = pickups;
            Notes           = notes;
            KnownMaps       = knownMaps;
            Enemies         = enemies;
            OperatorCorpses = operatorCorpses;
        }
    }
}
```

(`OperatorCorpseRegistry` resolves without a new `using` — it's declared in `CrimsonDraft.Infrastructure`, the enclosing namespace of `CrimsonDraft.Infrastructure.Save`, exactly like `DoorStateRegistry`/`EnemyStateRegistry` already do in this same file.)

- [ ] **Step 3: Register `OperatorCorpseRegistry` in `GameLifetimeScope`**

In `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`, add one line directly after the existing `EnemyStateRegistry` registration (line 54):

```csharp
            builder.Register<EnemyStateRegistry>(Lifetime.Singleton);
            builder.Register<OperatorCorpseRegistry>(Lifetime.Singleton);
```

- [ ] **Step 4: Fix the four existing `new WorldStateRegistries(...)` call sites so the suite compiles**

In `Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs`, change line 20 from:

```csharp
            var world     = new WorldStateRegistries(doors, rooms, pickups, notes, knownMaps, enemies);
```

to:

```csharp
            var operatorCorpses = new OperatorCorpseRegistry();
            var world     = new WorldStateRegistries(doors, rooms, pickups, notes, knownMaps, enemies, operatorCorpses);
```

In `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs`, both call sites (lines 111-113 and 162-164) currently read:

```csharp
            var world       = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
```

Change each to:

```csharp
            var world       = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry());
```

(keep the local variable name as `world` for the second call site too — it's `world` in both places already).

In `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs`, line 134-136 currently reads:

```csharp
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
```

Change to:

```csharp
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry());
```

- [ ] **Step 5: Run the full EditMode suite to verify everything still compiles and passes**

Run via Unity Test Runner (EditMode, no filter — this is a compile-breaking change across three test files, so run everything) or UnityMCP `run_tests` with no filter.
Expected: PASS, same pass count as before this task (no new tests were added in this task — Task 1's 4 tests plus every pre-existing test, all green).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/SaveGameData.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs
git commit -m "feat(save): wire OperatorCorpseRegistry into WorldStateRegistries and SaveGameData"
```

---

### Task 3: `GameStateResetter` clears `OperatorCorpses` on New Game

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs`

**Interfaces:**
- Consumes: `WorldStateRegistries.OperatorCorpses` (Task 2), `OperatorCorpseRegistry.Record`/`IsRecorded`/`ClearAll` (Task 1).

- [ ] **Step 1: Extend the existing test to assert corpses are cleared**

In `Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs`, in `ResetAll_clearsEveryRegistry`, after the line `enemies.SetDefeated("enemy-a");` (line 29) add:

```csharp
            world.OperatorCorpses.Record(0, "room-a", UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
```

and after `Assert.IsFalse(enemies.IsDefeated("enemy-a"));` (line 41) add:

```csharp
            Assert.IsFalse(world.OperatorCorpses.IsRecorded(0));
```

- [ ] **Step 2: Run the test to verify it fails**

Run via Unity Test Runner, filter `GameStateResetterTests`.
Expected: FAIL on the new `Assert.IsFalse(world.OperatorCorpses.IsRecorded(0))` — `ResetAll()` doesn't clear it yet.

- [ ] **Step 3: Add the clear call to `GameStateResetter.ResetAll()`**

In `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs`, add one line after `this.world.Enemies.ClearAll();` (line 34):

```csharp
            this.world.Enemies.ClearAll();
            this.world.OperatorCorpses.ClearAll();
```

- [ ] **Step 4: Run the test to verify it passes**

Same filter as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Save/GameStateResetter.cs Game/CrimsonDraft/Assets/Tests/EditMode/GameStateResetterTests.cs
git commit -m "feat(save): clear OperatorCorpseRegistry on New Game reset"
```

---

### Task 4: `OperatorCorpseSettings` + `IOperatorCorpseSpawner`/`OperatorCorpseSpawner`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseSettings.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/IOperatorCorpseSpawner.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseSpawner.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseSpawnerTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

**Interfaces:**
- Consumes: `RoomController` (`Assets/Scripts/Navigation/Rooms/RoomController.cs`, has public `Transform` via `MonoBehaviour.transform` and `RoomId`).
- Produces: `CrimsonDraft.Navigation.OperatorCorpseSettings` (ScriptableObject, `GameObject CorpsePrefab { get; }`), `CrimsonDraft.Navigation.IOperatorCorpseSpawner` with `void Spawn(RoomController room, Vector3 position, Quaternion rotation)`, `CrimsonDraft.Navigation.OperatorCorpseSpawner : IOperatorCorpseSpawner` (constructor `OperatorCorpseSpawner(OperatorCorpseSettings settings)`). Later tasks (5, 7) depend on `IOperatorCorpseSpawner`.

- [ ] **Step 1: Write the failing test**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseSpawnerTests
    {
        [Test]
        public void Spawn_instantiatesPrefabAsChildOfRoom_atGivenTransform()
        {
            var prefabSource = new GameObject("DummyCorpseModel");
            var settings     = ScriptableObject.CreateInstance<OperatorCorpseSettings>();
            var so = new SerializedObject(settings);
            so.FindProperty("corpsePrefab").objectReferenceValue = prefabSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var pos = new Vector3(1f, 2f, 3f);
            var rot = Quaternion.Euler(0f, 90f, 0f);

            try
            {
                var spawner = new OperatorCorpseSpawner(settings);
                spawner.Spawn(room, pos, rot);

                Assert.AreEqual(1, room.transform.childCount);
                var spawned = room.transform.GetChild(0);
                Assert.AreEqual(pos, spawned.position);
                Assert.AreEqual(rot, spawned.rotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(prefabSource);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via Unity Test Runner, filter `OperatorCorpseSpawnerTests`.
Expected: FAIL/compile error — `OperatorCorpseSettings`/`OperatorCorpseSpawner` don't exist yet.

- [ ] **Step 3: Write `OperatorCorpseSettings`**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Operator Corpse Settings")]
    public sealed class OperatorCorpseSettings : ScriptableObject
    {
        [SerializeField] private GameObject corpsePrefab = null!;

        public GameObject CorpsePrefab => this.corpsePrefab;
    }
}
```

- [ ] **Step 4: Write `IOperatorCorpseSpawner`**

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public interface IOperatorCorpseSpawner
    {
        void Spawn(RoomController room, Vector3 position, Quaternion rotation);
    }
}
```

- [ ] **Step 5: Write `OperatorCorpseSpawner`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseSpawner : IOperatorCorpseSpawner
    {
        private readonly OperatorCorpseSettings settings;

        [Preserve]
        public OperatorCorpseSpawner(OperatorCorpseSettings settings) => this.settings = settings;

        public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
            => Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Same filter as Step 2. Expected: PASS.

- [ ] **Step 7: Register the new field, ScriptableObject instance, and spawner in `NavigationScope`**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`, add a new serialized field near the other single-asset fields (after `saveSlotListView` at line 41):

```csharp
        [SerializeField] private SaveSlotListView      saveSlotListView     = null!;
        [SerializeField] private OperatorCorpseSettings corpseSettings      = null!;
```

Then, in `Configure`, add the registration directly after the existing `EnemyBootstrap` line (currently line 148: `builder.Register<EnemyBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();`):

```csharp
            builder.Register<EnemyBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.corpseSettings);
            builder.Register<OperatorCorpseSpawner>(Lifetime.Singleton).As<IOperatorCorpseSpawner>();
```

(No new `using` needed — `OperatorCorpseSettings`/`OperatorCorpseSpawner`/`IOperatorCorpseSpawner` are all in `CrimsonDraft.Navigation`, the same namespace `NavigationScope` itself is declared in.)

Note: the `corpseSettings` field will show as a missing reference in the Inspector until Task 8 creates and assigns the actual `OperatorCorpseSettings` asset — this is expected and doesn't break compilation or any EditMode test (nothing resolves this scope's DI container in an EditMode test).

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseSettings.cs Game/CrimsonDraft/Assets/Scripts/Navigation/IOperatorCorpseSpawner.cs Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseSpawner.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseSpawnerTests.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(navigation): add OperatorCorpseSpawner and register it in NavigationScope"
```

---

### Task 5: `OperatorCorpseBootstrap`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseBootstrap.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseBootstrapTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

**Interfaces:**
- Consumes: `IOperatorRoster` (`Count`, indexer returning `OperatorRuntime` with `.IsAlive`), `IRoomOrchestrator.CurrentRoom`, `PlayerController` (`MonoBehaviour`, use `.transform`), `ISubscriber<CombatEndedEvent>` (MessagePipe), `OperatorCorpseRegistry.IsRecorded`/`Record` (Task 1), `IOperatorCorpseSpawner.Spawn` (Task 4).
- Produces: `CrimsonDraft.Navigation.OperatorCorpseBootstrap : IInitializable, IDisposable`.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseBootstrapTests
    {
        private sealed class FakeRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;

            public FakeRoster(int count, params int[] deadSlots)
            {
                this.slots = new OperatorRuntime[count];
                for (int i = 0; i < count; i++)
                {
                    this.slots[i] = new OperatorRuntime(i, null, isPresent: true, maxHp: 100);
                    if (Array.IndexOf(deadSlots, i) >= 0)
                        this.slots[i].ApplyDamage(9999);
                }
            }

            public bool IsInitialized => true;
            public int Count => this.slots.Length;
            public OperatorRuntime this[int slotIndex] => this.slots[slotIndex];

            public IReadOnlyList<int> GetAliveSlots()
            {
                var alive = new List<int>();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) alive.Add(i);
                return alive;
            }

            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => Array.Empty<int>();
            public void RestoreHp(int[] snapshot) { }
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public FakeRoomOrchestrator(RoomController? currentRoom) => this.CurrentRoom = currentRoom;
            public RoomController? CurrentRoom { get; }
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) { }
        }

        private sealed class FakeSpawner : IOperatorCorpseSpawner
        {
            public int SpawnCallCount;
            public RoomController? LastRoom;
            public Vector3 LastPosition;

            public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
            {
                this.SpawnCallCount++;
                this.LastRoom     = room;
                this.LastPosition = position;
            }
        }

        private sealed class FakeSubscriber<T> : ISubscriber<T>
        {
            private IMessageHandler<T>? handler;

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                this.handler = handler;
                return new Subscription(() => this.handler = null);
            }

            public void Publish(T value) => this.handler?.Handle(value);

            private sealed class Subscription : IDisposable
            {
                private readonly Action dispose;
                public Subscription(Action dispose) => this.dispose = dispose;
                public void Dispose() => this.dispose();
            }
        }

        [Test]
        public void OnCombatEnded_recordsAndSpawnsCorpseForNewlyDeadOperator()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();
            var roomSo = new SerializedObject(room);
            roomSo.FindProperty("roomId").stringValue = "room-1";
            roomSo.ApplyModifiedPropertiesWithoutUndo();

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(4f, 0f, 5f);
            var player = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 2, deadSlots: 1);
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = true });

                Assert.IsTrue(registry.IsRecorded(1));
                Assert.IsFalse(registry.IsRecorded(0));
                Assert.AreEqual(1, spawner.SpawnCallCount);
                Assert.AreEqual(room, spawner.LastRoom);
                Assert.AreEqual(new Vector3(4f, 0f, 5f), spawner.LastPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void OnCombatEnded_doesNotRespawnAlreadyRecordedOperator()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1, deadSlots: 0);
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();
            registry.Record(0, "room-1", Vector3.zero, Quaternion.identity);

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = false });

                Assert.AreEqual(0, spawner.SpawnCallCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void OnCombatEnded_ignoresAliveOperators()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1); // no dead slots
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = true });

                Assert.AreEqual(0, spawner.SpawnCallCount);
                Assert.IsFalse(registry.IsRecorded(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Test Runner, filter `OperatorCorpseBootstrapTests`.
Expected: FAIL/compile error — `OperatorCorpseBootstrap` doesn't exist yet.

- [ ] **Step 3: Write the implementation**

```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseBootstrap : IInitializable, IDisposable
    {
        private readonly IOperatorRoster               roster;
        private readonly IRoomOrchestrator              roomOrchestrator;
        private readonly PlayerController               player;
        private readonly ISubscriber<CombatEndedEvent>  combatEndedSubscriber;
        private readonly OperatorCorpseRegistry         registry;
        private readonly IOperatorCorpseSpawner         spawner;

        private IDisposable? subscription;

        [Preserve]
        public OperatorCorpseBootstrap(
            IOperatorRoster              roster,
            IRoomOrchestrator            roomOrchestrator,
            PlayerController             player,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber,
            OperatorCorpseRegistry       registry,
            IOperatorCorpseSpawner       spawner)
        {
            this.roster                = roster;
            this.roomOrchestrator      = roomOrchestrator;
            this.player                = player;
            this.combatEndedSubscriber = combatEndedSubscriber;
            this.registry              = registry;
            this.spawner               = spawner;
        }

        void IInitializable.Initialize()
        {
            this.subscription = this.combatEndedSubscriber.Subscribe(OnCombatEnded);
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            RoomController? room = this.roomOrchestrator.CurrentRoom;
            if (room == null) return;

            for (int i = 0; i < this.roster.Count; i++)
            {
                if (this.roster[i].IsAlive) continue;
                if (this.registry.IsRecorded(i)) continue;

                Vector3    pos = this.player.transform.position;
                Quaternion rot = this.player.transform.rotation;

                this.registry.Record(i, room.RoomId, pos, rot);
                this.spawner.Spawn(room, pos, rot);
            }
        }

        void IDisposable.Dispose()
        {
            this.subscription?.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same filter as Step 2. Expected: PASS (3/3).

- [ ] **Step 5: Register `OperatorCorpseBootstrap` in `NavigationScope`, after `SaveGameLoader`**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`, add one line directly after the `OperatorCorpseSpawner` registration added in Task 4 (which already sits after `EnemyBootstrap`, itself already after `SaveGameLoader` at line 136 — so this satisfies the "after `SaveGameLoader`" ordering constraint):

```csharp
            builder.RegisterInstance(this.corpseSettings);
            builder.Register<OperatorCorpseSpawner>(Lifetime.Singleton).As<IOperatorCorpseSpawner>();
            builder.Register<OperatorCorpseBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseBootstrap.cs Game/CrimsonDraft/Assets/Tests/EditMode/OperatorCorpseBootstrapTests.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(navigation): add OperatorCorpseBootstrap to capture and spawn corpses on combat end"
```

---

### Task 6: `SaveController` writes corpses into `SaveGameData`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs:85-133` (`BuildSaveData`)
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs`

**Interfaces:**
- Consumes: `WorldStateRegistries.OperatorCorpses.GetAll()` (Task 2/1), `SaveGameData.operatorCorpses`/`OperatorCorpseEntry` (Task 2).
- No constructor signature change — `SaveController` already holds `WorldStateRegistries world`.

- [ ] **Step 1: Extend the existing test to assert corpses are written**

In `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs`, in `BuildSaveData_capturesWorldStateAndInventory_andWritesToService`, after `world.Enemies.SetDefeated("enemy-1");` (line 142) add:

```csharp
            world.OperatorCorpses.Record(0, "room-1", new Vector3(9f, 0f, 9f), Quaternion.identity);
```

and after `Assert.AreEqual(1, data.defeatedEnemyIds.Count);` (line 185) add:

```csharp
            Assert.AreEqual(1, data.operatorCorpses.Count);
            Assert.AreEqual(0, data.operatorCorpses[0].slotIndex);
            Assert.AreEqual("room-1", data.operatorCorpses[0].roomId);
            Assert.AreEqual(new Vector3(9f, 0f, 9f), data.operatorCorpses[0].position);
```

- [ ] **Step 2: Run the test to verify it fails**

Run via Unity Test Runner, filter `SaveControllerTests`.
Expected: FAIL on the new `operatorCorpses` assertions — `BuildSaveData` doesn't write them yet (`data.operatorCorpses.Count` is 0).

- [ ] **Step 3: Add the write in `BuildSaveData`**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs`, add this block directly after `data.defeatedEnemyIds.AddRange(this.world.Enemies.GetDefeated());` (line 108):

```csharp
            foreach (var entry in this.world.OperatorCorpses.GetAll())
            {
                data.operatorCorpses.Add(new OperatorCorpseEntry
                {
                    slotIndex = entry.SlotIndex,
                    roomId    = entry.RoomId,
                    position  = entry.Position,
                    rotation  = entry.Rotation,
                });
            }
```

- [ ] **Step 4: Run the test to verify it passes**

Same filter as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/SaveController.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs
git commit -m "feat(save): persist operator corpses in SaveGameData"
```

---

### Task 7: `SaveGameLoader` restores corpses on load

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs`

**Interfaces:**
- Consumes: `IOperatorCorpseSpawner` (Task 4), `OperatorCorpseRegistry.LoadState` (Task 1), `SaveGameData.operatorCorpses` (Task 2), `RoomController.RoomId` (existing).
- `SaveGameLoader`'s constructor gains a new last parameter `IOperatorCorpseSpawner corpseSpawner` — both existing test call sites must be updated.

- [ ] **Step 1: Add a `FakeSpawner` and extend both tests**

In `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs`, add a new private nested class alongside the existing fakes (after `FakeRoomOrchestrator`, currently ending at line 77):

```csharp
        private sealed class FakeSpawner : IOperatorCorpseSpawner
        {
            public int SpawnCallCount;
            public RoomController? LastRoom;

            public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
            {
                this.SpawnCallCount++;
                this.LastRoom = room;
            }
        }
```

In `Initialize_withNoPendingLoad_doesNothing` (line 104-130), change the loader construction (line 119) from:

```csharp
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
```

to:

```csharp
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker(), new FakeSpawner());
```

In `Initialize_withPendingLoad_restoresRegistriesInventoryAndPosition` (line 132-194):

1. Add an `operatorCorpses` entry to the `PendingLoad` `SaveGameData` object initializer — after `defeatedEnemyIds = new List<string> { "enemy-1" },` (line 151) add:

```csharp
                    operatorCorpses   = new List<OperatorCorpseEntry>
                    {
                        new OperatorCorpseEntry { slotIndex = 0, roomId = "room-2", position = new Vector3(1f, 0f, 1f), rotation = Quaternion.identity },
                    },
```

2. After the existing `var playerGo = new GameObject("Player");` / `var player = playerGo.AddComponent<PlayerController>();` lines (165-166), add a room that matches the saved `roomId` and a spawner:

```csharp
            var corpseRoomGo = new GameObject("Room2");
            var corpseRoom   = corpseRoomGo.AddComponent<RoomController>();
            var corpseRoomSo = new SerializedObject(corpseRoom);
            corpseRoomSo.FindProperty("roomId").stringValue = "room-2";
            corpseRoomSo.ApplyModifiedPropertiesWithoutUndo();
            var spawner = new FakeSpawner();
```

3. Change the loader construction (line 170) from:

```csharp
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
```

to:

```csharp
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker(), spawner);
```

4. After `Assert.IsTrue(world.Enemies.IsDefeated("enemy-1"));` (line 178) add:

```csharp
                Assert.IsTrue(world.OperatorCorpses.IsRecorded(0));
                Assert.AreEqual(1, spawner.SpawnCallCount);
                Assert.AreEqual(corpseRoom, spawner.LastRoom);
```

5. In the `finally` block (lines 188-193), add cleanup for the new room:

```csharp
                UnityEngine.Object.DestroyImmediate(corpseRoomGo);
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Test Runner, filter `SaveGameLoaderTests`.
Expected: FAIL/compile error — `SaveGameLoader` doesn't accept an `IOperatorCorpseSpawner` argument yet.

- [ ] **Step 3: Update `SaveGameLoader`**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs`, add a new field and constructor parameter. The constructor becomes:

```csharp
        private readonly ISaveGameService     saveGameService;
        private readonly IInventoryService    inventoryService;
        private readonly IOperatorRoster      roster;
        private readonly IRoomOrchestrator    roomOrchestrator;
        private readonly PlayerController     player;
        private readonly ItemDatabase         itemDatabase;
        private readonly WorldStateRegistries world;
        private readonly PlaytimeTracker      playtimeTracker;
        private readonly IOperatorCorpseSpawner corpseSpawner;

        [Preserve]
        public SaveGameLoader(
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            ItemDatabase         itemDatabase,
            WorldStateRegistries world,
            PlaytimeTracker      playtimeTracker,
            IOperatorCorpseSpawner corpseSpawner)
        {
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.itemDatabase     = itemDatabase;
            this.world            = world;
            this.playtimeTracker  = playtimeTracker;
            this.corpseSpawner    = corpseSpawner;
        }
```

Add the call to `ApplyOperatorCorpses` directly after `this.world.Enemies.LoadState(data.defeatedEnemyIds);` in `Initialize()`:

```csharp
            this.world.Enemies.LoadState(data.defeatedEnemyIds);
            ApplyOperatorCorpses(data);
```

Add the new private method (after `ApplyRooms`, before `ApplyInventory`):

```csharp
        private void ApplyOperatorCorpses(SaveGameData data)
        {
            var entries = new List<OperatorCorpseRegistry.Entry>();
            foreach (var e in data.operatorCorpses)
                entries.Add(new OperatorCorpseRegistry.Entry(e.slotIndex, e.roomId, e.position, e.rotation));
            this.world.OperatorCorpses.LoadState(entries);

            var rooms = UnityEngine.Object.FindObjectsOfType<RoomController>(true);
            foreach (var entry in entries)
            {
                RoomController? room = Array.Find(rooms, r => r.RoomId == entry.RoomId);
                if (room == null)
                {
                    UnityEngine.Debug.LogWarning($"[SaveGameLoader] No room '{entry.RoomId}' for saved operator corpse (slot {entry.SlotIndex}).");
                    continue;
                }
                this.corpseSpawner.Spawn(room, entry.Position, entry.Rotation);
            }
        }
```

(`UnityEngine.Object`/`UnityEngine.Debug` are fully qualified here rather than adding `using UnityEngine;`, because this file already has `using System;` — an unqualified `Object` would be ambiguous between `System.Object` and `UnityEngine.Object` if both namespaces were open.)

- [ ] **Step 4: Run the tests to verify they pass**

Same filter as Step 2. Expected: PASS (2/2).

- [ ] **Step 5: Run the full EditMode suite**

No filter. Expected: PASS, all green (this constructor change only affects the two call sites already updated in Step 1).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/SaveGameLoader.cs Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs
git commit -m "feat(save): restore operator corpses into their rooms on load"
```

---

### Task 8: Corpse prefab + Animator Controller (Unity assets, via UnityMCP)

**Files:**
- Create (Unity assets, not plain-text-editable): `Game/CrimsonDraft/Assets/Animations/OperatorCorpse_Controller.controller`, `Game/CrimsonDraft/Assets/Prefabs/Characters/OperatorNavCorpse.prefab`
- Create (Unity asset): a `OperatorCorpseSettings` instance, e.g. `Game/CrimsonDraft/Assets/Data/OperatorCorpseSettings.asset`
- Modify (scene data): the `NavigationScope` component instance in each Deck scene — assign the `corpseSettings` field added in Task 4.

This task has no automated test (per the spec's Testing section — Unity asset/Animator-Controller work is verified manually in Play Mode, matching the project's existing boundary for scene-dependent MonoBehaviours). Do this task last, after Tasks 1-7 are merged and compiling cleanly, since it depends on the `OperatorCorpseSettings` type existing (Task 4).

- [ ] **Step 1: Locate the shared operator FBX and its unwired death clip**

Using UnityMCP `manage_asset` (or `find_in_file`/`unity_reflect`), confirm `Assets/Prefabs/Characters/Ethan_Combat_FBX.prefab` and its source FBX contain the `Rig|Soldier_Death_27` animation clip (per the design spec and prior project memory — all four `OperatorData` assets share this one battlefield prefab). Note the exact clip asset path for Step 2.

- [ ] **Step 2: Create the minimal Animator Controller**

Using UnityMCP `manage_animation`, create a new Animator Controller at `Assets/Animations/OperatorCorpse_Controller.controller` with:
- No parameters.
- A single state (e.g. named `Dead`) whose motion is the `Rig|Soldier_Death_27` clip located in Step 1, set as the default state.

- [ ] **Step 3: Create the corpse prefab**

Using UnityMCP `manage_gameobject`/`manage_prefabs`:
1. Instantiate the same skinned-mesh model used by `Ethan_Combat_FBX.prefab` (the visible mesh hierarchy only — do not copy `OperatorCombatAudio`, hit-fx marker components, or the combat `Animator Controller`).
2. Add an `Animator` component referencing the controller created in Step 2.
3. Save it as a new prefab at `Assets/Prefabs/Characters/OperatorNavCorpse.prefab`.
4. Use `read_console` to confirm no compile/import errors resulted from the prefab save.

- [ ] **Step 4: Create and wire the `OperatorCorpseSettings` asset**

Using UnityMCP `manage_scriptable_object` (or `manage_asset`):
1. Create an instance of `OperatorCorpseSettings` (the type added in Task 4) at `Assets/Data/OperatorCorpseSettings.asset`.
2. Set its `corpsePrefab` field to the prefab created in Step 3.
3. For each Deck scene's `NavigationScope` component (the `corpseSettings` field added in Task 4, Step 7), assign this asset via `manage_gameobject`/`manage_asset` (set component property), then save the scene.

- [ ] **Step 5: Manual verification in Play Mode**

Per the spec's Testing section, verify by hand (not automatable):
1. Enter Play Mode, trigger a combat encounter, let one operator's HP reach 0 without a party wipe.
2. On returning to Navigation, confirm the corpse appears at the position the player was standing in, holding the death pose.
3. Leave the room (trigger a door transition) and re-enter — confirm the corpse is still there.
4. Save the game, reload that save — confirm the corpse is restored in the correct room at the correct position.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Animations/OperatorCorpse_Controller.controller Game/CrimsonDraft/Assets/Animations/OperatorCorpse_Controller.controller.meta Game/CrimsonDraft/Assets/Prefabs/Characters/OperatorNavCorpse.prefab Game/CrimsonDraft/Assets/Prefabs/Characters/OperatorNavCorpse.prefab.meta Game/CrimsonDraft/Assets/Data/OperatorCorpseSettings.asset Game/CrimsonDraft/Assets/Data/OperatorCorpseSettings.asset.meta
git commit -m "feat(navigation): add operator corpse prefab, death-pose controller, and settings asset"
```

(Scene file changes from wiring the `corpseSettings` field are committed separately per-scene, following however this project already handles scene-file diffs.)
