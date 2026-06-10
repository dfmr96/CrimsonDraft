# Inter-Scene Deck Transition — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the player to move between Deck B and Deck C (separate Unity scenes) via door/stairwell interactables, with door unlock state persisting across scene changes.

**Architecture:** A `DoorStateRegistry` singleton at `GameLifetimeScope` level persists unlock state across `NavigationScope` lifecycles. A `SceneEntryContext` ScriptableObject carries the target spawn point ID from the outgoing scene to the incoming `RoomOrchestrator`. A `FloorTransitionService` (also at `GameLifetimeScope`) orchestrates the full scene swap using the existing `DoorTransition` animation mechanism without modifying `DoorTransitionController`.

**Tech Stack:** VContainer (DI), UniTask (async), Unity SceneManager, existing `RoomTransitionContext` / `DoorTransitionController` infrastructure.

**Spec:** `Design/Plans/superpowers/specs/2026-06-09-inter-scene-transition-design.md`
**GDD:** `Design/GDD/Sistema de Transicion entre Decks.md`

---

## File Map

**New files:**
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IDoorInteractable.cs`
- `Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneEntryContext.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneSpawnPoint.cs`
- `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IFloorTransitionService.cs`
- `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/FloorTransitionService.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SceneDoorInteractable.cs`
- `Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs`
- `Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs`

**Modified files:**
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs` — add `doorId`, update `Construct` signature, add `RestoreFromRegistry`, update registry on unlock
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs` — inject `DoorStateRegistry` + `SceneEntryContext`, pass registry to doors, resolve spawn from entry context
- `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` — cached door arrays, editor button, VContainer registration from cache
- `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs` — register `DoorStateRegistry`, `SceneEntryContext`, move `RoomTransitionContext`, register `FloorTransitionService`
- `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs` — update `MakeDoor`, add registry tests
- `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs` — update `MakeOrchestrator`, add `SceneEntryContext` tests

---

## Task 1: `IDoorInteractable` interface + `DoorStateRegistry` service

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IDoorInteractable.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs`

- [ ] **Step 1: Create `IDoorInteractable`**

```csharp
// IDoorInteractable.cs
#nullable enable

namespace CrimsonDraft.Navigation.Interactables
{
    public interface IDoorInteractable
    {
        string DoorId { get; }
        void   RestoreFromRegistry();
    }
}
```

- [ ] **Step 2: Write failing tests for `DoorStateRegistry`**

```csharp
// DoorStateRegistryTests.cs
#nullable enable

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
        public void LoadState_restoresGivenState()
        {
            var registry = new DoorStateRegistry();
            registry.LoadState(new Dictionary<string, bool> { ["door-x"] = true });
            Assert.IsTrue(registry.IsUnlocked("door-x"));
            Assert.IsFalse(registry.IsUnlocked("door-y"));
        }

        [Test]
        public void GetState_reflectsSetUnlockedCalls()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsTrue(registry.GetState().ContainsKey("door-a"));
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Unity Test Runner → Window → General → Test Runner → EditMode → filter `DoorStateRegistryTests` → Run Selected.
Expected: all 5 fail with type-not-found error.

- [ ] **Step 4: Create `DoorStateRegistry`**

```csharp
// DoorStateRegistry.cs
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class DoorStateRegistry
    {
        private readonly Dictionary<string, bool> state = new();

        [Preserve]
        public DoorStateRegistry() { }

        public bool IsUnlocked(string doorId)
            => this.state.TryGetValue(doorId, out var v) && v;

        public void SetUnlocked(string doorId)
            => this.state[doorId] = true;

        public IReadOnlyDictionary<string, bool> GetState() => this.state;

        public void LoadState(IReadOnlyDictionary<string, bool> saved)
        {
            this.state.Clear();
            foreach (var (k, v) in saved)
                this.state[k] = v;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Unity Test Runner → filter `DoorStateRegistryTests` → Run Selected.
Expected: 5 passed.

- [ ] **Step 6: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IDoorInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/DoorStateRegistry.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/DoorStateRegistryTests.cs"
git commit -m "feat(navigation): add IDoorInteractable interface and DoorStateRegistry"
```

---

## Task 2: Register `DoorStateRegistry` in `GameLifetimeScope`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

- [ ] **Step 1: Add `DoorStateRegistry` registration to `GameLifetimeScope.Configure()`**

In `GameLifetimeScope.cs`, add after the `EncounterContext` registration:

```csharp
// existing:
using CrimsonDraft.Infrastructure.Scenes;
// add:
using CrimsonDraft.Infrastructure;
```

Inside `Configure()`, after `builder.Register<EncounterContext>(...)`:

```csharp
builder.Register<DoorStateRegistry>(Lifetime.Singleton);
```

- [ ] **Step 2: Verify compilation**

Check Unity console for compilation errors. Expected: no errors.

- [ ] **Step 3: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs"
git commit -m "feat(infrastructure): register DoorStateRegistry in GameLifetimeScope"
```

---

## Task 3: Update `RoomDoorInteractable` + `RoomOrchestrator` for `DoorStateRegistry`

`RoomDoorInteractable.Construct()` gains `DoorStateRegistry`; `RoomOrchestrator` injects it and passes it when constructing doors. Registry is updated whenever a door is unlocked.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`

- [ ] **Step 1: Update `RoomDoorInteractableTests` — update `MakeDoor`, add registry tests**

Replace the existing `MakeDoor` helper and add new tests:

```csharp
private static RoomDoorInteractable MakeDoor(
    DoorData          data,
    RoomController    destination,
    GameObject        doorPrefab,
    IRoomOrchestrator orchestrator,
    DoorStateRegistry? registry = null,
    string            doorId   = "test-door")
{
    var go   = new GameObject();
    var door = go.AddComponent<RoomDoorInteractable>();
    var so   = new SerializedObject(door);
    so.FindProperty("data").objectReferenceValue                 = data;
    so.FindProperty("destination").objectReferenceValue          = destination;
    so.FindProperty("doorTransitionPrefab").objectReferenceValue = doorPrefab;
    so.FindProperty("doorId").stringValue                        = doorId;
    so.ApplyModifiedPropertiesWithoutUndo();
    door.Construct(orchestrator, registry ?? new DoorStateRegistry());
    return door;
}
```

Add after the existing tests, before the `FakeOrchestrator` region:

```csharp
[Test]
public void RestoreFromRegistry_whenRegistryHasDoorUnlocked_transitionsImmediatelyDespiteLockedData()
{
    var registry     = new DoorStateRegistry();
    registry.SetUnlocked("door-1");
    var data         = MakeLockedDoor("door_locked");
    var destination  = MakeRoom();
    var prefab       = new GameObject("DoorPrefab");
    var orchestrator = new FakeOrchestrator();
    var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

    door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

    Assert.AreEqual(destination, orchestrator.LastDestination,
        "registry unlock must override locked data flag");

    UnityEngine.Object.DestroyImmediate(door.gameObject);
    UnityEngine.Object.DestroyImmediate(destination.gameObject);
    UnityEngine.Object.DestroyImmediate(prefab);
}

[Test]
public void Interact_whenKeySuccess_updatesRegistry()
{
    var registry     = new DoorStateRegistry();
    var keyData      = MakeKeyItem("key-1", "Key 1");
    var data         = MakeLockedDoor("door_test", keyData);
    var destination  = MakeRoom();
    var prefab       = new GameObject("DoorPrefab");
    var orchestrator = new FakeOrchestrator();
    var dialogue     = new FakeDialogue();
    var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 0) };
    var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

    door.Interact(MakeContext(dialogue, inventory));
    dialogue.LastOnComplete!.Invoke();

    Assert.IsTrue(registry.IsUnlocked("door-1"), "registry must be updated when door is unlocked");

    UnityEngine.Object.DestroyImmediate(door.gameObject);
    UnityEngine.Object.DestroyImmediate(destination.gameObject);
    UnityEngine.Object.DestroyImmediate(prefab);
}
```

Also add `using CrimsonDraft.Infrastructure;` to the test file's using directives.

- [ ] **Step 2: Run tests to verify new ones fail**

Unity Test Runner → filter `RoomDoorInteractableTests` → Run Selected.
Expected: `RestoreFromRegistry_*` and `Interact_whenKeySuccess_updatesRegistry` fail; others pass.

- [ ] **Step 3: Update `RoomDoorInteractable.cs`**

Replace the full file with:

```csharp
#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class RoomDoorInteractable : MonoBehaviour, IInteractable, IDoorInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private string        doorId               = null!;
        [SerializeField] private DoorData       data                = null!;
        [SerializeField] private RoomController destination          = null!;
        [SerializeField] private GameObject     doorTransitionPrefab = null!;

        public string         DoorId      => this.doorId;
        public RoomController? Destination => this.destination;

        private IRoomOrchestrator roomOrchestrator = null!;
        private DoorStateRegistry registry         = null!;
        private bool              unlocked;

        [Inject]
        public void Construct(IRoomOrchestrator roomOrchestrator, DoorStateRegistry registry)
        {
            this.roomOrchestrator = roomOrchestrator;
            this.registry         = registry;
            RestoreFromRegistry();
        }

        public void RestoreFromRegistry()
        {
            this.unlocked = this.registry.IsUnlocked(this.doorId);
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.roomOrchestrator
                    .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                    .Forget();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                case KeyUseResult.AlreadyDepleted:
                    context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                    break;

                case KeyUseResult.Success:
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.registry.SetUnlocked(this.doorId);
                            this.roomOrchestrator
                                .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                                .Forget();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.InventoryService.RemoveItem(outcome.SlotIndex);
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened_depleted",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.registry.SetUnlocked(this.doorId);
                            this.roomOrchestrator
                                .TransitionToRoomAsync(this.destination, this.doorTransitionPrefab)
                                .Forget();
                        });
                    break;
            }
        }
    }
}
```

- [ ] **Step 4: Update `RoomOrchestrator.cs` — inject `DoorStateRegistry`, pass it to doors**

Replace the constructor and `Initialize` method. The constructor adds `DoorStateRegistry`:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomOrchestrator : IRoomOrchestrator, IInitializable
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService                          inputService;
        private readonly PlayerController                       player;
        private readonly RoomTransitionContext                  context;
        private readonly DoorStateRegistry                      doorStateRegistry;
        private readonly IPublisher<RoomTransitionStartedEvent> startedPublisher;
        private readonly IPublisher<RoomTransitionedEvent>      endedPublisher;

        private RoomController? currentRoom;
        private bool            isTransitioning;

        [Preserve]
        public RoomOrchestrator(
            IInputService                          inputService,
            PlayerController                       player,
            RoomTransitionContext                  context,
            DoorStateRegistry                      doorStateRegistry,
            IPublisher<RoomTransitionStartedEvent> startedPublisher,
            IPublisher<RoomTransitionedEvent>      endedPublisher)
        {
            this.inputService      = inputService;
            this.player            = player;
            this.context           = context;
            this.doorStateRegistry = doorStateRegistry;
            this.startedPublisher  = startedPublisher;
            this.endedPublisher    = endedPublisher;
        }

        void IInitializable.Initialize()
        {
            var rooms = Object.FindObjectsOfType<RoomController>(true);

            if (rooms.Length == 0)
            {
                Debug.LogError("[RoomOrchestrator] No RoomController found in scene.");
                return;
            }

            var starting = this.context.StartingRoom;

            foreach (var room in rooms)
                room.Deactivate();

            if (starting == null)
            {
                Debug.LogWarning("[RoomOrchestrator] No starting room set in RoomTransitionContext — using first found.");
                starting = rooms[0];
            }

            starting.Activate();
            this.currentRoom = starting;

            foreach (var door in Object.FindObjectsOfType<RoomDoorInteractable>(true))
            {
                door.Construct(this, this.doorStateRegistry);
            }
        }

        public async UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.startedPublisher.Publish(new RoomTransitionStartedEvent(this.currentRoom!, destination));
            this.inputService.SwitchToDoorTransition();
            AudioListener.pause = true;

            var tcs = new UniTaskCompletionSource();
            this.context.Set(doorPrefab, this.inputService.DoorTransitionSkip, () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();

            this.currentRoom!.Deactivate();
            destination.Activate();

            var spawnPoint     = FindSpawnPoint(destination, this.currentRoom);
            var spawnTransform = spawnPoint != null ? spawnPoint.transform : destination.transform;
            this.player.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
            spawnPoint?.ActivateCamera();

            await tcs.Task;

            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            AudioListener.pause = false;
            this.inputService.SwitchToGameplay();
            this.currentRoom = destination;

            this.endedPublisher.Publish(new RoomTransitionedEvent(this.currentRoom));
            this.isTransitioning = false;
        }

        private static SpawnPoint? FindSpawnPoint(RoomController destination, RoomController fromRoom)
        {
            foreach (var sp in destination.GetComponentsInChildren<SpawnPoint>(includeInactive: true))
            {
                if (sp.FromRoom == fromRoom)
                    return sp;
            }

            Debug.LogWarning($"[RoomOrchestrator] No SpawnPoint for '{fromRoom.name}' in '{destination.name}' — using room root.");
            return null;
        }
    }
}
```

- [ ] **Step 5: Update `RoomOrchestratorInitTests` — update `MakeOrchestrator` helper**

The helper now requires a `DoorStateRegistry` parameter (optional, defaults to a fresh instance):

```csharp
private static RoomOrchestrator MakeOrchestrator(
    PlayerController     player,
    RoomTransitionContext context,
    DoorStateRegistry?   registry = null)
    => new RoomOrchestrator(
        new FakeInputService(),
        player,
        context,
        registry ?? new DoorStateRegistry(),
        new FakePublisher<RoomTransitionStartedEvent>(),
        new FakePublisher<RoomTransitionedEvent>());
```

Add `using CrimsonDraft.Infrastructure;` to the test file.

- [ ] **Step 6: Run all tests**

Unity Test Runner → EditMode → Run All.
Expected: all existing tests pass (existing `RoomOrchestratorInitTests` and `RoomDoorInteractableTests` now pass including new registry tests).

- [ ] **Step 7: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs"
git commit -m "feat(navigation): persist door unlock state via DoorStateRegistry"
```

---

## Task 4: `SceneEntryContext` + `SceneSpawnPoint`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneEntryContext.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneSpawnPoint.cs`

- [ ] **Step 1: Create `SceneEntryContext`**

```csharp
// SceneEntryContext.cs
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Navigation/SceneEntryContext")]
    public sealed class SceneEntryContext : ScriptableObject
    {
        public string? PendingEntryPointId { get; private set; }

        public void SetPendingEntry(string entryPointId)
            => this.PendingEntryPointId = entryPointId;

        public string? Consume()
        {
            var id                     = this.PendingEntryPointId;
            this.PendingEntryPointId   = null;
            return id;
        }
    }
}
```

- [ ] **Step 2: Create `SceneSpawnPoint`**

```csharp
// SceneSpawnPoint.cs
#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string             entryPointId = null!;
        [SerializeField] private RoomController     startingRoom = null!;
        [SerializeField] private CinemachineCamera? camera;

        public string         EntryPointId => this.entryPointId;
        public RoomController StartingRoom => this.startingRoom;

        public void ActivateCamera()
        {
            if (this.camera == null) return;

            var room = GetComponentInParent<RoomController>(includeInactive: true);
            if (room == null) return;

            foreach (var cam in room.GetComponentsInChildren<CinemachineCamera>(includeInactive: true))
                cam.gameObject.SetActive(false);

            this.camera.gameObject.SetActive(true);
        }
    }
}
```

- [ ] **Step 3: Create `SceneEntryContext` asset in Unity**

In Unity Editor: Assets menu → Create → CrimsonDraft → Navigation → SceneEntryContext.
Save as `Game/CrimsonDraft/Assets/Data/Navigation/SceneEntryContext.asset`.
(Create the `Data/Navigation/` folder if it doesn't exist.)

- [ ] **Step 4: Verify compilation**

Check Unity console for compilation errors. Expected: no errors.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneEntryContext.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SceneSpawnPoint.cs"
git add "Game/CrimsonDraft/Assets/Data/Navigation/SceneEntryContext.asset"
git add "Game/CrimsonDraft/Assets/Data/Navigation/SceneEntryContext.asset.meta"
git add "Game/CrimsonDraft/Assets/Data/Navigation/"
git commit -m "feat(navigation): add SceneEntryContext and SceneSpawnPoint"
```

---

## Task 5: Update `RoomOrchestrator` + `GameLifetimeScope` for `SceneEntryContext`

`RoomOrchestrator.Initialize()` checks `SceneEntryContext` for a pending entry point and uses the matching `SceneSpawnPoint`'s starting room instead of the default.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`

- [ ] **Step 1: Write failing tests for `SceneEntryContext` spawn resolution**

Add to `RoomOrchestratorInitTests.cs` (after existing tests):

```csharp
[Test]
public void Initialize_whenSceneEntryContextHasPendingEntry_activatesSpawnPointStartingRoom()
{
    var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
    var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();
    entryContext.SetPendingEntry("entry-1");

    var roomA   = new GameObject("RoomA").AddComponent<RoomController>();
    var roomB   = new GameObject("RoomB").AddComponent<RoomController>();
    var spawnGo = new GameObject("Spawn");
    var spawn   = spawnGo.AddComponent<SceneSpawnPoint>();
    var spawnSo = new SerializedObject(spawn);
    spawnSo.FindProperty("entryPointId").stringValue          = "entry-1";
    spawnSo.FindProperty("startingRoom").objectReferenceValue = roomB;
    spawnSo.ApplyModifiedPropertiesWithoutUndo();

    context.SetStartingRoom(roomA);

    var playerGo = new GameObject("Player");
    var player   = playerGo.AddComponent<PlayerController>();

    try
    {
        var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
        ((IInitializable)orchestrator).Initialize();

        Assert.IsTrue(roomB.gameObject.activeSelf,  "spawn point starting room must be active");
        Assert.IsFalse(roomA.gameObject.activeSelf, "fallback starting room must not be active");
    }
    finally
    {
        Object.DestroyImmediate(roomA.gameObject);
        Object.DestroyImmediate(roomB.gameObject);
        Object.DestroyImmediate(spawnGo);
        Object.DestroyImmediate(playerGo.gameObject);
        Object.DestroyImmediate(context);
        Object.DestroyImmediate(entryContext);
    }
}

[Test]
public void Initialize_whenSceneEntryContextIsEmpty_usesRoomTransitionContextStartingRoom()
{
    var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
    var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();

    var roomA = new GameObject("RoomA").AddComponent<RoomController>();
    var roomB = new GameObject("RoomB").AddComponent<RoomController>();
    context.SetStartingRoom(roomA);

    var playerGo = new GameObject("Player");
    var player   = playerGo.AddComponent<PlayerController>();

    try
    {
        var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
        ((IInitializable)orchestrator).Initialize();

        Assert.IsTrue(roomA.gameObject.activeSelf,  "RoomTransitionContext starting room must be used");
        Assert.IsFalse(roomB.gameObject.activeSelf, "other room must not be active");
    }
    finally
    {
        Object.DestroyImmediate(roomA.gameObject);
        Object.DestroyImmediate(roomB.gameObject);
        Object.DestroyImmediate(playerGo.gameObject);
        Object.DestroyImmediate(context);
        Object.DestroyImmediate(entryContext);
    }
}
```

Update `MakeOrchestrator` to accept `SceneEntryContext`:

```csharp
private static RoomOrchestrator MakeOrchestrator(
    PlayerController      player,
    RoomTransitionContext  context,
    DoorStateRegistry?    registry = null,
    SceneEntryContext?    entry    = null)
    => new RoomOrchestrator(
        new FakeInputService(),
        player,
        context,
        registry ?? new DoorStateRegistry(),
        entry    ?? ScriptableObject.CreateInstance<SceneEntryContext>(),
        new FakePublisher<RoomTransitionStartedEvent>(),
        new FakePublisher<RoomTransitionedEvent>());
```

Add `using CrimsonDraft.Navigation.Rooms;` if not already present.

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → filter `RoomOrchestratorInitTests` → Run Selected.
Expected: the two new tests fail.

- [ ] **Step 3: Update `RoomOrchestrator.cs` — add `SceneEntryContext` to constructor and `Initialize`**

Replace the full file:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomOrchestrator : IRoomOrchestrator, IInitializable
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService                          inputService;
        private readonly PlayerController                       player;
        private readonly RoomTransitionContext                  context;
        private readonly DoorStateRegistry                      doorStateRegistry;
        private readonly SceneEntryContext                      sceneEntryContext;
        private readonly IPublisher<RoomTransitionStartedEvent> startedPublisher;
        private readonly IPublisher<RoomTransitionedEvent>      endedPublisher;

        private RoomController? currentRoom;
        private bool            isTransitioning;

        [Preserve]
        public RoomOrchestrator(
            IInputService                          inputService,
            PlayerController                       player,
            RoomTransitionContext                  context,
            DoorStateRegistry                      doorStateRegistry,
            SceneEntryContext                      sceneEntryContext,
            IPublisher<RoomTransitionStartedEvent> startedPublisher,
            IPublisher<RoomTransitionedEvent>      endedPublisher)
        {
            this.inputService      = inputService;
            this.player            = player;
            this.context           = context;
            this.doorStateRegistry = doorStateRegistry;
            this.sceneEntryContext  = sceneEntryContext;
            this.startedPublisher  = startedPublisher;
            this.endedPublisher    = endedPublisher;
        }

        void IInitializable.Initialize()
        {
            var rooms = Object.FindObjectsOfType<RoomController>(true);

            if (rooms.Length == 0)
            {
                Debug.LogError("[RoomOrchestrator] No RoomController found in scene.");
                return;
            }

            foreach (var room in rooms)
                room.Deactivate();

            var starting = ResolveStartingRoom();

            if (starting == null)
            {
                Debug.LogWarning("[RoomOrchestrator] No starting room resolved — using first found.");
                starting = rooms[0];
            }

            starting.Activate();
            this.currentRoom = starting;

            foreach (var door in Object.FindObjectsOfType<RoomDoorInteractable>(true))
                door.Construct(this, this.doorStateRegistry);
        }

        private RoomController? ResolveStartingRoom()
        {
            var entryId = this.sceneEntryContext.Consume();

            if (entryId != null)
            {
                foreach (var sp in Object.FindObjectsOfType<SceneSpawnPoint>(true))
                {
                    if (sp.EntryPointId != entryId) continue;

                    this.player.transform.SetPositionAndRotation(
                        sp.transform.position, sp.transform.rotation);
                    sp.ActivateCamera();
                    return sp.StartingRoom;
                }

                Debug.LogWarning($"[RoomOrchestrator] No SceneSpawnPoint with entry '{entryId}' — falling back.");
            }

            return this.context.StartingRoom;
        }

        public async UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.startedPublisher.Publish(new RoomTransitionStartedEvent(this.currentRoom!, destination));
            this.inputService.SwitchToDoorTransition();
            AudioListener.pause = true;

            var tcs = new UniTaskCompletionSource();
            this.context.Set(doorPrefab, this.inputService.DoorTransitionSkip, () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();

            this.currentRoom!.Deactivate();
            destination.Activate();

            var spawnPoint     = FindSpawnPoint(destination, this.currentRoom);
            var spawnTransform = spawnPoint != null ? spawnPoint.transform : destination.transform;
            this.player.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
            spawnPoint?.ActivateCamera();

            await tcs.Task;

            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            AudioListener.pause = false;
            this.inputService.SwitchToGameplay();
            this.currentRoom = destination;

            this.endedPublisher.Publish(new RoomTransitionedEvent(this.currentRoom));
            this.isTransitioning = false;
        }

        private static SpawnPoint? FindSpawnPoint(RoomController destination, RoomController fromRoom)
        {
            foreach (var sp in destination.GetComponentsInChildren<SpawnPoint>(includeInactive: true))
            {
                if (sp.FromRoom == fromRoom)
                    return sp;
            }

            Debug.LogWarning($"[RoomOrchestrator] No SpawnPoint for '{fromRoom.name}' in '{destination.name}' — using room root.");
            return null;
        }
    }
}
```

- [ ] **Step 4: Register `SceneEntryContext` in `GameLifetimeScope`**

Add a serialized field and registration in `GameLifetimeScope.cs`:

```csharp
[SerializeField] private SceneEntryContext sceneEntryContext = null!;
```

In `Configure()`:
```csharp
builder.RegisterInstance(this.sceneEntryContext);
```

Add `using CrimsonDraft.Navigation.Rooms;` to `GameLifetimeScope.cs`.

In the Unity Inspector for `GameLifetimeScope` (on the Boot scene's game object): assign the `SceneEntryContext.asset` created in Task 4 to the new field.

- [ ] **Step 5: Move `RoomTransitionContext` registration from `NavigationScope` to `GameLifetimeScope`**

In `GameLifetimeScope.cs`, add serialized field:
```csharp
[SerializeField] private RoomTransitionContext roomTransitionContext = null!;
```

In `Configure()`:
```csharp
builder.RegisterInstance(this.roomTransitionContext);
```

Add `using CrimsonDraft.Navigation.Rooms;` (already added above).

In the Unity Inspector for `GameLifetimeScope`: assign the existing `RoomTransitionContext` asset (the one already in `Resources/`) to the new field.

In `NavigationScope.cs`, remove the `[SerializeField] private RoomTransitionContext roomTransitionContext` field and its `builder.RegisterInstance(this.roomTransitionContext)` call. Replace the `this.roomTransitionContext.SetStartingRoom(this.startingRoom)` line with:

```csharp
var ctx = Parent!.Container.Resolve<RoomTransitionContext>();
ctx.SetStartingRoom(this.startingRoom);
```

Add `using CrimsonDraft.Navigation.Rooms;` to `NavigationScope.cs` if not already present.

- [ ] **Step 6: Run all tests**

Unity Test Runner → EditMode → Run All.
Expected: all tests pass including the two new `SceneEntryContext` tests.

- [ ] **Step 7: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs"
git commit -m "feat(navigation): resolve spawn point from SceneEntryContext on scene load"
```

---

## Task 6: `IFloorTransitionService` + `FloorTransitionService`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IFloorTransitionService.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/FloorTransitionService.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

- [ ] **Step 1: Create `IFloorTransitionService`**

```csharp
// IFloorTransitionService.cs
#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface IFloorTransitionService
    {
        UniTask TransitionToFloorAsync(
            string     fromScene,
            string     toScene,
            string     entryPointId,
            GameObject doorPrefab);
    }
}
```

- [ ] **Step 2: Create `FloorTransitionService`**

```csharp
// FloorTransitionService.cs
#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class FloorTransitionService : IFloorTransitionService
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService        inputService;
        private readonly RoomTransitionContext roomTransitionContext;
        private readonly SceneEntryContext     sceneEntryContext;

        private bool isTransitioning;

        [Preserve]
        public FloorTransitionService(
            IInputService        inputService,
            RoomTransitionContext roomTransitionContext,
            SceneEntryContext     sceneEntryContext)
        {
            this.inputService         = inputService;
            this.roomTransitionContext = roomTransitionContext;
            this.sceneEntryContext     = sceneEntryContext;
        }

        public async UniTask TransitionToFloorAsync(
            string     fromScene,
            string     toScene,
            string     entryPointId,
            GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.inputService.SwitchToDoorTransition();
            this.sceneEntryContext.SetPendingEntry(entryPointId);

            var tcs = new UniTaskCompletionSource();
            this.roomTransitionContext.Set(
                doorPrefab,
                this.inputService.DoorTransitionSkip,
                () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();
            await tcs.Task;

            await SceneManager.UnloadSceneAsync(fromScene).ToUniTask();
            await SceneManager.LoadSceneAsync(toScene, LoadSceneMode.Additive).ToUniTask();
            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            this.inputService.SwitchToGameplay();
            this.isTransitioning = false;
        }
    }
}
```

- [ ] **Step 3: Register `FloorTransitionService` in `GameLifetimeScope`**

In `GameLifetimeScope.Configure()`:

```csharp
builder.Register<FloorTransitionService>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 4: Verify compilation**

Check Unity console. Expected: no errors.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IFloorTransitionService.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/FloorTransitionService.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs"
git commit -m "feat(infrastructure): add FloorTransitionService for inter-scene deck transitions"
```

---

## Task 7: `SceneDoorInteractable` + tests + register in `NavigationScope`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SceneDoorInteractable.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

- [ ] **Step 1: Write failing tests for `SceneDoorInteractable`**

```csharp
// SceneDoorInteractableTests.cs
#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class SceneDoorInteractableTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static SceneDoorInteractable MakeDoor(
            DoorData              data,
            IFloorTransitionService floorService,
            DoorStateRegistry     registry,
            string                doorId = "test-door")
        {
            var go   = new GameObject();
            var door = go.AddComponent<SceneDoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("doorId").stringValue            = doorId;
            so.FindProperty("data").objectReferenceValue     = data;
            so.FindProperty("targetSceneName").stringValue   = "Deck_C";
            so.FindProperty("targetEntryPointId").stringValue = "test-entry";
            so.ApplyModifiedPropertiesWithoutUndo();
            door.Construct(floorService, registry);
            return door;
        }

        private static DoorData MakeUnlockedDoor()
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static DoorData MakeLockedDoor(string yarnNode, KeyItemData? keyItem = null)
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue = true;
            if (keyItem != null)
                so.FindProperty("keyItem").objectReferenceValue = keyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.DialogueReference.nodeName = yarnNode;
            return data;
        }

        private static KeyItemData MakeKeyItem(string id, string displayName)
        {
            var data = ScriptableObject.CreateInstance<KeyItemData>();
            var so   = new SerializedObject(data);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static InteractionContext MakeContext(FakeDialogue dialogue, FakeInventory inventory)
            => new(inventory, null!, dialogue, null!, null!);

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_callsFloorTransitionImmediately()
        {
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeUnlockedDoor(), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

            Assert.IsTrue(fakeService.TransitionCalled, "must call floor transition immediately");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void RestoreFromRegistry_whenRegistryHasDoorUnlocked_transitionsImmediatelyDespiteLockedData()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-1");
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeLockedDoor("door_locked"), fakeService, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

            Assert.IsTrue(fakeService.TransitionCalled, "registry unlock must override locked data flag");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenLockedNoKey_startsDialogue_doesNotTransition()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var door        = MakeDoor(MakeLockedDoor("door_locked"), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, new FakeInventory()));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsFalse(fakeService.TransitionCalled, "must not transition when locked");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeySuccess_startsDialogue_thenTransitionsOnComplete()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 0) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsFalse(fakeService.TransitionCalled, "must not transition before dialogue completes");

            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(fakeService.TransitionCalled, "must transition after dialogue completes");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeySuccess_updatesRegistry()
        {
            var registry    = new DoorStateRegistry();
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 0) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, registry, "door-1");

            door.Interact(MakeContext(dialogue, inventory));
            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(registry.IsUnlocked("door-1"), "registry must be updated on unlock");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, 3) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsTrue(inventory.RemoveItemCalled, "must remove item from inventory when key is depleted");
            Assert.AreEqual(3, inventory.RemovedSlotIndex);

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeFloorService : IFloorTransitionService
        {
            public bool TransitionCalled { get; private set; }

            public UniTask TransitionToFloorAsync(
                string from, string to, string entryId, GameObject doorPrefab)
            {
                this.TransitionCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeDialogue : IDialogueService
        {
            public bool    IsRunning      => false;
            public string? LastNodeName   { get; private set; }
            public Action? LastOnComplete { get; private set; }

            public void StartDialogue(
                string                               nodeName,
                IReadOnlyDictionary<string, object>? variables  = null,
                Action?                              onComplete  = null,
                IReadOnlyDictionary<string, Action>? commands   = null)
            {
                this.LastNodeName   = nodeName;
                this.LastOnComplete = onComplete;
            }
        }

        private sealed class FakeInventory : IInventoryService
        {
            public KeyUseOutcome UseKeyResult    = new(KeyUseResult.NotFound, -1);
            public bool          RemoveItemCalled { get; private set; }
            public int           RemovedSlotIndex { get; private set; } = -1;

            public IReadOnlyList<InventorySlot> Slots                                  => Array.Empty<InventorySlot>();
            public int  SlotCount                                                       => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0)     => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)                   => false;
            public void RemoveItem(int slotIndex) { RemoveItemCalled = true; RemovedSlotIndex = slotIndex; }
            public void MoveItem(int fromSlot, int toSlot)                             { }
            public void EquipWeapon(int slotIndex, int operatorSlot)                   { }
            public void UnequipWeapon(int slotIndex)                                   { }
            public int  GetEquippedWeaponIndex(int operatorSlot)                       => -1;
            public bool CanReload(int slotIndex, int operatorSlot)                     => false;
            public void ReloadOperator(int slotIndex, int operatorSlot)                { }
            public bool TryCombine(int slotA, int slotB)                               => false;
            public KeyUseOutcome TryUseKey(string keyItemId)                           => UseKeyResult;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → filter `SceneDoorInteractableTests` → Run Selected.
Expected: all fail with type-not-found error.

- [ ] **Step 3: Create `SceneDoorInteractable.cs`**

```csharp
// SceneDoorInteractable.cs
#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SceneDoorInteractable : MonoBehaviour, IInteractable, IDoorInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private string     doorId               = null!;
        [SerializeField] private DoorData   data                 = null!;
        [SerializeField] private string     targetSceneName      = null!;
        [SerializeField] private string     targetEntryPointId   = null!;
        [SerializeField] private GameObject doorTransitionPrefab = null!;

        public string DoorId => this.doorId;

        private IFloorTransitionService floorService = null!;
        private DoorStateRegistry       registry     = null!;
        private bool                    unlocked;

        [Inject]
        public void Construct(IFloorTransitionService floorService, DoorStateRegistry registry)
        {
            this.floorService = floorService;
            this.registry     = registry;
            RestoreFromRegistry();
        }

        public void RestoreFromRegistry()
        {
            this.unlocked = this.registry.IsUnlocked(this.doorId);
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                Transition();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                case KeyUseResult.AlreadyDepleted:
                    context.DialogueService.StartDialogue(this.data.DialogueReference.nodeName ?? "");
                    break;

                case KeyUseResult.Success:
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.registry.SetUnlocked(this.doorId);
                            Transition();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.InventoryService.RemoveItem(outcome.SlotIndex);
                    context.DialogueService.StartDialogue(
                        OpenedNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened_depleted",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.registry.SetUnlocked(this.doorId);
                            Transition();
                        });
                    break;
            }
        }

        private void Transition()
        {
            this.floorService
                .TransitionToFloorAsync(
                    gameObject.scene.name,
                    this.targetSceneName,
                    this.targetEntryPointId,
                    this.doorTransitionPrefab)
                .Forget();
        }
    }
}
```

- [ ] **Step 4: Register `SceneDoorInteractable` in `NavigationScope.Configure()`**

In `NavigationScope.cs`, after the `CombatTrigger` and `EnemyNavAgent` registration loops, add:

```csharp
foreach (var door in FindObjectsByType<SceneDoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    builder.RegisterComponent(door);
```

Add `using CrimsonDraft.Navigation.Interactables;` if not already present.

- [ ] **Step 5: Run all tests**

Unity Test Runner → EditMode → Run All.
Expected: all tests pass including the 6 new `SceneDoorInteractableTests`.

- [ ] **Step 6: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/SceneDoorInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs"
git commit -m "feat(navigation): add SceneDoorInteractable for inter-scene floor transitions"
```

---

## Task 8: `NavigationScope` editor cache + VContainer injection migration

Eliminates all runtime `FindObjectsOfType` calls for doors. Requires pressing **"Cache Scene Doors"** button in the `NavigationScope` Inspector after adding any door to the scene.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`

- [ ] **Step 1: Add cached door arrays + editor button to `NavigationScope`**

In `NavigationScope.cs`, add the serialized fields after `startingRoom`:

```csharp
[SerializeField] private RoomDoorInteractable[]  cachedRoomDoors  = System.Array.Empty<RoomDoorInteractable>();
[SerializeField] private SceneDoorInteractable[] cachedSceneDoors = System.Array.Empty<SceneDoorInteractable>();
```

Add the editor-only method inside the class:

```csharp
#if UNITY_EDITOR
[UnityEditor.MenuItem("CONTEXT/NavigationScope/Cache Scene Doors")]
private static void CacheSceneDoorsMenu(UnityEditor.MenuCommand cmd)
    => ((NavigationScope)cmd.context).CacheSceneDoors();

[ContextMenu("Cache Scene Doors")]
private void CacheSceneDoors()
{
    this.cachedRoomDoors  = FindObjectsByType<RoomDoorInteractable>(
        FindObjectsInactive.Include, FindObjectsSortMode.None);
    this.cachedSceneDoors = FindObjectsByType<SceneDoorInteractable>(
        FindObjectsInactive.Include, FindObjectsSortMode.None);
    UnityEditor.EditorUtility.SetDirty(this);
}
#endif
```

- [ ] **Step 2: Switch `NavigationScope.Configure()` to register doors from cache**

In `Configure()`, replace the existing `SceneDoorInteractable` loop with one that uses both cached arrays. Remove any remaining `FindObjectsByType<SceneDoorInteractable>` call and replace with:

```csharp
foreach (var door in this.cachedRoomDoors)
    builder.RegisterComponent(door);
foreach (var door in this.cachedSceneDoors)
    builder.RegisterComponent(door);
```

- [ ] **Step 3: Remove the manual door construction loop from `RoomOrchestrator.Initialize()`**

In `RoomOrchestrator.cs`, remove from `Initialize()`:

```csharp
// DELETE these lines:
foreach (var door in Object.FindObjectsOfType<RoomDoorInteractable>(true))
    door.Construct(this, this.doorStateRegistry);
```

VContainer now calls `Construct()` automatically for all registered `RoomDoorInteractable` components before `Initialize()` runs.

- [ ] **Step 4: Remove `doorStateRegistry` field from `RoomOrchestrator` if no longer used**

After removing the door construction loop, `this.doorStateRegistry` is no longer referenced in `RoomOrchestrator`. Remove it from the constructor and the field:

```csharp
// Remove from constructor parameters:
DoorStateRegistry doorStateRegistry,

// Remove field:
private readonly DoorStateRegistry doorStateRegistry;

// Remove from constructor body:
this.doorStateRegistry = doorStateRegistry;
```

Update `RoomOrchestratorInitTests.MakeOrchestrator` — remove the `DoorStateRegistry` parameter:

```csharp
private static RoomOrchestrator MakeOrchestrator(
    PlayerController      player,
    RoomTransitionContext  context,
    SceneEntryContext?    entry = null)
    => new RoomOrchestrator(
        new FakeInputService(),
        player,
        context,
        entry ?? ScriptableObject.CreateInstance<SceneEntryContext>(),
        new FakePublisher<RoomTransitionStartedEvent>(),
        new FakePublisher<RoomTransitionedEvent>());
```

- [ ] **Step 5: Press "Cache Scene Doors" in the Unity Inspector**

Open the `FIX_Deck_B` scene in Unity. Select the `NavigationScope` GameObject. In the Inspector, right-click the component header (or use the three-dot menu) and choose **"Cache Scene Doors"**. Save the scene.

Repeat for any other navigation scene that has doors.

- [ ] **Step 6: Run all tests**

Unity Test Runner → EditMode → Run All.
Expected: all tests pass.

- [ ] **Step 7: Verify in Play Mode**

Enter Play Mode in `FIX_Deck_B`. Interact with an existing `RoomDoorInteractable`. Confirm the door transition animation plays and the player moves to the correct room.

- [ ] **Step 8: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs"
git add "Game/CrimsonDraft/Assets/Scenes/Production/FIX_Deck_B.unity"
git commit -m "refactor(navigation): cache scene doors at edit time, use VContainer injection"
```

---

## Scene Setup Checklist (after all tasks)

To wire up an inter-scene connection from Deck B to Deck C:

1. **In Deck B scene:** Place a `SceneDoorInteractable` GameObject on the stairwell/door.
   - Set `doorId` to a unique string (e.g. `"deckb_port_stairs_upper"`)
   - Set `targetSceneName` to `"Deck_C"` (exact Unity scene name)
   - Set `targetEntryPointId` to the matching ID (e.g. `"deckb_port_entry"`)
   - Assign `doorTransitionPrefab`
   - Press **"Cache Scene Doors"** on `NavigationScope`, save scene

2. **In Deck C scene:** Place a `SceneSpawnPoint` GameObject at the entry point.
   - Set `entryPointId` to `"deckb_port_entry"` (matches the above)
   - Assign `startingRoom` to the `RoomController` the player should start in
   - Set position/rotation where the player should appear
   - Optionally assign a `CinemachineCamera`

3. **For the return path:** Place a second `SceneDoorInteractable` in Deck C pointing back to Deck B, with a matching `SceneSpawnPoint` in Deck B.
