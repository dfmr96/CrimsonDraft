# Room Transition System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current open-room navigation with a prefab-based room system where only one room is active at a time, and door interactions trigger a first-person door animation scene that acts as a diegetic loading screen while the room swap happens invisibly.

**Architecture:** `RoomOrchestrator` (pure C# VContainer service) coordinates the entire transition: it loads the `DoorTransition` scene additively, swaps the active room, teleports the player, awaits a `UniTaskCompletionSource` resolved by `DoorTransitionController` via Animation Event, then unloads the transition scene. Two MessagePipe events (`RoomTransitionStartedEvent`, `RoomTransitionedEvent`) decouple game systems (enemies, ambient audio) from the orchestrator. A `RoomTransitionContext` ScriptableObject bridges the two scenes without DI.

**Tech Stack:** Unity 2022+, C# 10 nullable, VContainer, MessagePipe, UniTask, NUnit (Edit Mode), Unity Animation Events

**Spec:** [`docs/superpowers/specs/2026-05-16-room-transition-design.md`](../specs/2026-05-16-room-transition-design.md)

---

## File Map

| Action | Path |
|---|---|
| **Create** | `Assets/Scripts/Navigation/NavigationEvents.cs` |
| **Create** | `Assets/Scripts/Navigation/Rooms/RoomController.cs` |
| **Create** | `Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs` |
| **Create** | `Assets/Scripts/Navigation/Rooms/RoomTransitionContext.cs` |
| **Create** | `Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs` |
| **Create** | `Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs` |
| **Create** | `Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs` |
| **Create** | `Assets/Tests/EditMode/RoomControllerTests.cs` |
| **Create** | `Assets/Tests/EditMode/RoomOrchestratorInitTests.cs` |
| **Create** | `Assets/Tests/EditMode/RoomDoorInteractableTests.cs` |
| **Modify** | `Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef` |
| **Modify** | `Assets/Scripts/Navigation/NavigationScope.cs` |
| **Unity** | Create `Assets/Resources/RoomTransitionContext.asset` |
| **Unity** | Create `Assets/Scenes/Production/DoorTransition.unity` |
| **Unity** | Add `DoorTransition` to Build Settings |
| **Unity** | Add `RoomController` + `SpawnPoint` to each room in Navigation scene |
| **Unity** | Replace `DoorInteractable` → `RoomDoorInteractable` on room-connecting doors |

All paths are relative to `Game/CrimsonDraft/`.

---

## Task 1 — Add MessagePipe to Navigation Assembly + NavigationEvents.cs

**Files:**
- Modify: `Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef`
- Create: `Assets/Scripts/Navigation/NavigationEvents.cs`

- [ ] **Step 1: Add MessagePipe references to the asmdef**

Open `Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef` and replace its full content:

```json
{
    "name": "CrimsonDraft.Navigation",
    "rootNamespace": "CrimsonDraft.Navigation",
    "references": [
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Inventory",
        "CrimsonDraft.Operators",
        "VContainer",
        "VContainer.Unity",
        "UniTask",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "YarnSpinner.Unity",
        "Unity.Cinemachine",
        "MessagePipe",
        "MessagePipe.VContainer"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create NavigationEvents.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationEvents.cs`:

```csharp
#nullable enable

using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public readonly struct RoomTransitionStartedEvent
    {
        public readonly RoomController Origin;
        public readonly RoomController Destination;

        public RoomTransitionStartedEvent(RoomController origin, RoomController destination)
        {
            Origin      = origin;
            Destination = destination;
        }
    }

    public readonly struct RoomTransitionedEvent
    {
        public readonly RoomController ActiveRoom;

        public RoomTransitionedEvent(RoomController activeRoom)
        {
            ActiveRoom = activeRoom;
        }
    }
}
```

- [ ] **Step 3: Verify no compile errors in Unity**

Switch to Unity Editor. Wait for domain reload. Check **Console** for errors. Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationEvents.cs
git commit -m "feat(room-transition): add MessagePipe refs and navigation event structs"
```

---

## Task 2 — RoomController

**Files:**
- Create: `Assets/Scripts/Navigation/Rooms/RoomController.cs`
- Create: `Assets/Tests/EditMode/RoomControllerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/RoomControllerTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomControllerTests
    {
        [Test]
        public void Activate_makesGameObjectActive()
        {
            var go   = new GameObject();
            go.SetActive(false);
            var room = go.AddComponent<RoomController>();

            room.Activate();

            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Deactivate_makesGameObjectInactive()
        {
            var go   = new GameObject();
            var room = go.AddComponent<RoomController>();

            room.Deactivate();

            Assert.IsFalse(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnPoint_returnsSerializedTransform()
        {
            var go         = new GameObject();
            var room       = go.AddComponent<RoomController>();
            var spawnGo    = new GameObject();

            var so = new SerializedObject(room);
            so.FindProperty("spawnPoint").objectReferenceValue = spawnGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(spawnGo.transform, room.SpawnPoint);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(spawnGo);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile errors (RoomController not defined)**

In Unity: **Window → General → Test Runner → Edit Mode → Run All**.  
Expected: compile failure or `TypeNotFound` for `RoomController`.

- [ ] **Step 3: Create RoomController.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomController.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint = null!;

        public Transform SpawnPoint => this.spawnPoint;

        public void Activate()   => gameObject.SetActive(true);
        public void Deactivate() => gameObject.SetActive(false);
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

In Unity Test Runner: **Edit Mode → Run All**.  
Expected: `RoomControllerTests` — 3 passed.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/RoomControllerTests.cs
git commit -m "feat(room-transition): add RoomController with activate/deactivate and spawn point"
```

---

## Task 3 — RoomTransitionContext (ScriptableObject)

**Files:**
- Create: `Assets/Scripts/Navigation/Rooms/RoomTransitionContext.cs`

- [ ] **Step 1: Create RoomTransitionContext.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomTransitionContext.cs`:

```csharp
#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Navigation/RoomTransitionContext")]
    public sealed class RoomTransitionContext : ScriptableObject
    {
        public GameObject? DoorPrefab { get; private set; }

        private Action? onComplete;

        public void Set(GameObject doorPrefab, Action onComplete)
        {
            this.DoorPrefab   = doorPrefab;
            this.onComplete   = onComplete;
        }

        public void NotifyComplete()
        {
            var callback    = this.onComplete;
            this.onComplete = null;
            this.DoorPrefab = null;
            callback?.Invoke();
        }
    }
}
```

- [ ] **Step 2: Verify compile in Unity**

Switch to Unity. Wait for domain reload. Console must show no errors.

- [ ] **Step 3: Create the Resources asset in Unity**

In Unity Project window:
1. Create folder `Game/CrimsonDraft/Assets/Resources/` if it doesn't exist
2. Right-click inside `Resources/` → **Create → CrimsonDraft → Navigation → RoomTransitionContext**
3. Name the asset exactly `RoomTransitionContext` (the filename becomes the `Resources.Load` key)

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomTransitionContext.cs
git add "Game/CrimsonDraft/Assets/Resources/RoomTransitionContext.asset"
git add "Game/CrimsonDraft/Assets/Resources/RoomTransitionContext.asset.meta"
git add "Game/CrimsonDraft/Assets/Resources.meta"
git commit -m "feat(room-transition): add RoomTransitionContext ScriptableObject and Resources asset"
```

---

## Task 4 — IRoomOrchestrator

**Files:**
- Create: `Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs`

- [ ] **Step 1: Create IRoomOrchestrator.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs`:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public interface IRoomOrchestrator
    {
        UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab);
    }
}
```

- [ ] **Step 2: Verify compile in Unity**

Expected: no errors in Console.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/IRoomOrchestrator.cs
git commit -m "feat(room-transition): add IRoomOrchestrator interface"
```

---

## Task 5 — RoomOrchestrator + Initialize Tests

**Files:**
- Create: `Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`
- Create: `Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs`:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomOrchestratorInitTests
    {
        [Test]
        public void Initialize_withOneActiveRoom_keepsItActive_andInactivesRemainInactive()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(true);
            goA.AddComponent<RoomController>();
            var goB = new GameObject("RoomB"); goB.SetActive(false);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(goA.activeSelf,  "active room must remain active");
                Assert.IsFalse(goB.activeSelf, "inactive room must remain inactive");
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
        public void Initialize_withMultipleActiveRooms_deactivatesAllButFirst()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(true);
            goA.AddComponent<RoomController>();
            var goB = new GameObject("RoomB"); goB.SetActive(true);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                int activeCount = (goA.activeSelf ? 1 : 0) + (goB.activeSelf ? 1 : 0);
                Assert.AreEqual(1, activeCount, "exactly one room must be active after initialize");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static RoomOrchestrator MakeOrchestrator(PlayerController player, RoomTransitionContext context)
            => new RoomOrchestrator(
                new FakeInputService(),
                player,
                context,
                new FakePublisher<RoomTransitionStartedEvent>(),
                new FakePublisher<RoomTransitionedEvent>());

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                  => null!;
            public InputAction Interact              => null!;
            public InputAction OpenInventory         => null!;
            public InputAction OpenMap               => null!;
            public InputAction Aim                   => null!;
            public InputAction Pause                 => null!;
            public InputAction Sprint                => null!;
            public InputAction CombatNavigate        => null!;
            public InputAction CombatConfirm         => null!;
            public InputAction CombatCancel          => null!;
            public InputAction CombatUseItem         => null!;
            public InputAction UINavigate            => null!;
            public InputAction UIConfirm             => null!;
            public InputAction UICancel              => null!;
            public InputAction UIBack                => null!;
            public InputAction DialogueAdvanceLine    => null!;
            public InputAction DialogueCancelDialogue => null!;
            public void SwitchToGameplay() { }
            public void SwitchToCombat()   { }
            public void SwitchToUI()       { }
            public void SwitchToDialogue() { }
            public void Dispose()          { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (RoomOrchestrator not defined)**

In Unity Test Runner: Edit Mode → Run All.  
Expected: compile error for `RoomOrchestrator`.

- [ ] **Step 3: Create RoomOrchestrator.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs`:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomOrchestrator : IRoomOrchestrator, IInitializable
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService                          inputService;
        private readonly PlayerController                       player;
        private readonly RoomTransitionContext                  context;
        private readonly IPublisher<RoomTransitionStartedEvent> startedPublisher;
        private readonly IPublisher<RoomTransitionedEvent>      endedPublisher;

        private RoomController? currentRoom;
        private bool            isTransitioning;

        [Preserve]
        public RoomOrchestrator(
            IInputService                          inputService,
            PlayerController                       player,
            RoomTransitionContext                  context,
            IPublisher<RoomTransitionStartedEvent> startedPublisher,
            IPublisher<RoomTransitionedEvent>      endedPublisher)
        {
            this.inputService      = inputService;
            this.player            = player;
            this.context           = context;
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

            RoomController? active = null;
            foreach (var room in rooms)
            {
                if (room.gameObject.activeSelf)
                {
                    if (active == null)
                        active = room;
                    else
                        room.Deactivate();
                }
            }

            if (active == null)
            {
                Debug.LogError("[RoomOrchestrator] No active RoomController found. Activating first room.");
                active = rooms[0];
                active.Activate();
            }

            this.currentRoom = active;
        }

        public async UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.startedPublisher.Publish(new RoomTransitionStartedEvent(this.currentRoom!, destination));
            this.inputService.SwitchToUI();
            AudioListener.pause = true;

            var tcs = new UniTaskCompletionSource();
            this.context.Set(doorPrefab, () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();

            this.currentRoom!.Deactivate();
            destination.Activate();
            this.player.transform.SetPositionAndRotation(
                destination.SpawnPoint.position,
                destination.SpawnPoint.rotation);

            await tcs.Task;

            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            AudioListener.pause = false;
            this.inputService.SwitchToGameplay();
            this.currentRoom = destination;

            this.endedPublisher.Publish(new RoomTransitionedEvent(this.currentRoom));
            this.isTransitioning = false;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all pass**

In Unity Test Runner: Edit Mode → Run All.  
Expected: `RoomOrchestratorInitTests` — 2 passed.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomOrchestrator.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/RoomOrchestratorInitTests.cs
git commit -m "feat(room-transition): add RoomOrchestrator service with initialize and transition logic"
```

---

## Task 6 — RoomDoorInteractable + Tests

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs`
- Create: `Assets/Tests/EditMode/RoomDoorInteractableTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomDoorInteractableTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static RoomDoorInteractable MakeDoor(
            DoorData data,
            RoomController destination,
            GameObject doorPrefab,
            IRoomOrchestrator orchestrator)
        {
            var go   = new GameObject();
            var door = go.AddComponent<RoomDoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("data").objectReferenceValue                = data;
            so.FindProperty("destination").objectReferenceValue         = destination;
            so.FindProperty("doorTransitionPrefab").objectReferenceValue = doorPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            door.Construct(orchestrator);
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

        private static RoomController MakeRoom()
            => new GameObject("Room").AddComponent<RoomController>();

        private static InteractionContext MakeContext(
            FakeDialogue  dialogue,
            FakeInventory inventory)
            => new(inventory, null!, dialogue, null!, null!);

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_callsTransitionImmediately()
        {
            var data        = MakeUnlockedDoor();
            var destination = MakeRoom();
            var prefab      = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door        = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "should transition to the configured destination");
            Assert.AreEqual(prefab, orchestrator.LastDoorPrefab,
                "should pass the configured door prefab");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenLockedNoKey_startsDialogue_doesNotTransition()
        {
            var data         = MakeLockedDoor("door_locked");
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, new FakeInventory()));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination, "must not transition when locked with no key");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenLockedKeyNotFound_startsDialogue_doesNotTransition()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_locked", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination);

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenKeySuccess_startsDialogue_thenTransitionsOnComplete()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_test", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 0) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsNull(orchestrator.LastDestination, "must not transition before dialogue completes");

            dialogue.LastOnComplete!.Invoke();

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "must transition after dialogue completes");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_test", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, 3) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsTrue(inventory.RemoveItemCalled, "must remove item from inventory when key is depleted");
            Assert.AreEqual(3, inventory.RemovedSlotIndex);

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeOrchestrator : IRoomOrchestrator
        {
            public RoomController? LastDestination { get; private set; }
            public GameObject?     LastDoorPrefab  { get; private set; }

            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
            {
                LastDestination = destination;
                LastDoorPrefab  = doorPrefab;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeDialogue : IDialogueService
        {
            public bool    IsRunning     => false;
            public string? LastNodeName  { get; private set; }
            public Action? LastOnComplete { get; private set; }

            public void StartDialogue(
                string                                nodeName,
                IReadOnlyDictionary<string, object>?  variables  = null,
                Action?                               onComplete  = null,
                IReadOnlyDictionary<string, Action>?  commands   = null)
            {
                LastNodeName   = nodeName;
                LastOnComplete = onComplete;
            }
        }

        private sealed class FakeInventory : IInventoryService
        {
            public KeyUseOutcome UseKeyResult    = new(KeyUseResult.NotFound, -1);
            public bool          RemoveItemCalled { get; private set; }
            public int           RemovedSlotIndex { get; private set; } = -1;

            public IReadOnlyList<InventorySlot> Slots                                   => Array.Empty<InventorySlot>();
            public int  SlotCount                                                        => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0)      => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)                    => false;
            public void RemoveItem(int slotIndex) { RemoveItemCalled = true; RemovedSlotIndex = slotIndex; }
            public void MoveItem(int fromSlot, int toSlot)                              { }
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

- [ ] **Step 2: Run tests — expect compile failure (RoomDoorInteractable not defined)**

In Unity Test Runner: Edit Mode → Run All.  
Expected: compile error for `RoomDoorInteractable`.

- [ ] **Step 3: Create RoomDoorInteractable.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class RoomDoorInteractable : MonoBehaviour, IInteractable
    {
        private const string OpenedNodeName = "door_opened_feedback";

        [SerializeField] private DoorData       data                  = null!;
        [SerializeField] private RoomController destination           = null!;
        [SerializeField] private GameObject     doorTransitionPrefab  = null!;

        private IRoomOrchestrator roomOrchestrator = null!;
        private bool              unlocked;

        [Inject]
        public void Construct(IRoomOrchestrator roomOrchestrator)
        {
            this.roomOrchestrator = roomOrchestrator;
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

- [ ] **Step 4: Run tests — expect all pass**

In Unity Test Runner: Edit Mode → Run All.  
Expected: `RoomDoorInteractableTests` — 5 passed.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/RoomDoorInteractable.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs
git commit -m "feat(room-transition): add RoomDoorInteractable with lock/key logic and room transition"
```

---

## Task 7 — DoorTransitionController

**Files:**
- Create: `Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs`

> No unit test — relies on Unity's Animation Event system, which requires Play Mode. Verified manually in Task 9.

- [ ] **Step 1: Create DoorTransitionController.cs**

Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs`:

```csharp
#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorTransitionController : MonoBehaviour
    {
        [SerializeField] private float animationTimeout = 5f;

        private RoomTransitionContext? context;
        private bool completed;

        private void Start()
        {
            this.context = Resources.Load<RoomTransitionContext>("RoomTransitionContext");

            if (this.context == null)
            {
                Debug.LogError("[DoorTransitionController] RoomTransitionContext not found in Resources.");
                return;
            }

            if (this.context.DoorPrefab == null)
            {
                Debug.LogError("[DoorTransitionController] DoorPrefab is null — calling NotifyComplete immediately.");
                this.context.NotifyComplete();
                return;
            }

            var door = Instantiate(this.context.DoorPrefab, transform);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;

            var animator = door.GetComponent<Animator>();
            if (animator != null)
                animator.Play(0);

            StartCoroutine(TimeoutFallback());
        }

        public void OnAnimationComplete()
        {
            if (this.completed) return;
            this.completed = true;
            this.context?.NotifyComplete();
        }

        private IEnumerator TimeoutFallback()
        {
            yield return new WaitForSeconds(this.animationTimeout);

            if (!this.completed)
            {
                Debug.LogWarning("[DoorTransitionController] Animation timeout — forcing transition complete.");
                OnAnimationComplete();
            }
        }
    }
}
```

- [ ] **Step 2: Verify compile in Unity**

Switch to Unity. Wait for domain reload. Console must show no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs
git commit -m "feat(room-transition): add DoorTransitionController for transition scene"
```

---

## Task 8 — Wire NavigationScope

**Files:**
- Modify: `Assets/Scripts/Navigation/NavigationScope.cs`

- [ ] **Step 1: Update NavigationScope.cs**

Open `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` and replace with:

```csharp
#nullable enable

using MessagePipe;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class NavigationScope : LifetimeScope
    {
        [SerializeField] private StartingLoadout        startingLoadout         = null!;
        [SerializeField] private CombineRecipeLibrary   combineRecipeLibrary    = null!;
        [SerializeField] private RoomTransitionContext  roomTransitionContext    = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InventoryView>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventoryBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<PlaceholderOverlayView>();
            builder.Register<PlaceholderOverlayController>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatTrigger>();
            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.Register<DialogueService>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<ContainerView>();
            builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // ── Room transition ──────────────────────────────────────────────
            builder.RegisterInstance(this.roomTransitionContext);

            var msgOptions = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<RoomTransitionStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<RoomTransitionedEvent>(msgOptions);

            builder.Register<RoomOrchestrator>(Lifetime.Singleton)
                   .AsSelf()
                   .As<IRoomOrchestrator>()
                   .AsImplementedInterfaces();

            foreach (var door in FindObjectsOfType<RoomDoorInteractable>(true))
                builder.RegisterComponent(door);
        }
    }
}
```

- [ ] **Step 2: Verify compile in Unity**

Switch to Unity. Wait for domain reload. Console must show no errors.

- [ ] **Step 3: Assign RoomTransitionContext in Inspector**

In Unity:
1. Select the `NavigationScope` GameObject in the Navigation scene hierarchy
2. In the Inspector, find the new **Room Transition Context** field
3. Drag `Assets/Resources/RoomTransitionContext.asset` into that field
4. Save the scene (**Ctrl+S**)

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(room-transition): wire RoomOrchestrator and RoomDoorInteractable in NavigationScope"
```

---

## Task 9 — Create DoorTransition Scene + Add to Build Settings

> All steps in this task are Unity Editor operations. No code changes.

- [ ] **Step 1: Create the DoorTransition scene**

In Unity:
1. **File → New Scene** → select **Empty** template
2. **File → Save As** → navigate to `Assets/Scenes/Production/` → name it `DoorTransition` → Save

- [ ] **Step 2: Set up the scene hierarchy**

In the `DoorTransition` scene hierarchy:
1. Rename the root to `DoorTransition`
2. **GameObject → Camera** — name it `FP_Camera`
   - Set Position: `(0, 1.6, 0)`, Rotation: `(0, 0, 0)`
   - Remove the **Audio Listener** component (to avoid duplicate AudioListener warning)
   - Set **Field of View**: `75`
3. **GameObject → Create Empty** — name it `DoorSpawner`
   - Set Position: `(0, 1.2, 1.5)`, Rotation: `(0, 180, 0)` — door faces the camera
   - Add the `DoorTransitionController` component to `DoorSpawner`
   - Leave **Animation Timeout** at `5` seconds

- [ ] **Step 3: Save the scene**

**Ctrl+S** to save `DoorTransition.unity`.

- [ ] **Step 4: Add DoorTransition to Build Settings**

1. **File → Build Settings**
2. Click **Add Open Scenes** (with DoorTransition open), or drag `Assets/Scenes/Production/DoorTransition.unity` into the **Scenes In Build** list
3. Verify the scene list order:
   - `Boot` (index 0)
   - `Navigation` (index 1)
   - `Combat` (index 2)
   - `DoorTransition` (index 3)

- [ ] **Step 5: Commit the new scene**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/Production/DoorTransition.unity"
git add "Game/CrimsonDraft/Assets/Scenes/Production/DoorTransition.unity.meta"
git commit -m "feat(room-transition): add DoorTransition scene with FP camera and DoorTransitionController"
```

---

## Task 10 — Wire Rooms in Navigation Scene

> All steps in this task are Unity Editor operations. No code changes.

- [ ] **Step 1: Add RoomController to each room root**

Open the `Navigation.unity` scene. For each room root GameObject:
1. Select the room root
2. **Add Component → RoomController**
3. Create a child empty GameObject named `SpawnPoint` inside the room
4. Position `SpawnPoint` where the player should appear when entering this room (standing position, facing the expected direction)
5. Assign the `SpawnPoint` Transform to the **Spawn Point** field on `RoomController`
6. Ensure only **one** room is active (the starting room) — deactivate all others

- [ ] **Step 2: Replace DoorInteractable with RoomDoorInteractable on room-connecting doors**

For each door that connects two rooms:
1. Select the door GameObject
2. Remove the `DoorInteractable` component (right-click → Remove Component)
3. Add **RoomDoorInteractable** component
4. Assign:
   - **Data**: the existing `DoorData` ScriptableObject asset (same as before)
   - **Destination**: the `RoomController` of the room this door leads to
   - **Door Transition Prefab**: the 3D door model prefab to show in the transition scene (must have an `Animator` — see Step 3 below)
5. Keep `DoorInteractable` on doors that are NOT room-connecting (barred doors, containers, etc.)

- [ ] **Step 3: Prepare the door animation on each door prefab**

For each door prefab used as `doorTransitionPrefab`:
1. Open the prefab
2. Verify it has an `Animator` component with a clip that plays open→close
3. In the animation clip, add an **Animation Event** at the last frame:
   - **Function**: `OnAnimationComplete`
   - **Object**: the `DoorTransitionController` component on the `DoorSpawner` parent
   
   > Since the door is instantiated as a child of `DoorSpawner`, the Animation Event must call `OnAnimationComplete` on the root of the instantiated object. Set the Animation Event to send to the `DoorTransitionController` using **SendMessage** (Unity Animation Events use SendMessage on the GameObject and its ancestors). The event function name `OnAnimationComplete` will propagate up via SendMessage.

   **Correct setup**: in the Animation Event on the clip, set **Function** = `OnAnimationComplete`. Unity's Animation Event system will call this on the Animator's GameObject first, then walk up the hierarchy. Since `DoorTransitionController` is on the parent (`DoorSpawner`), it will receive it.

- [ ] **Step 4: Save the Navigation scene**

**Ctrl+S**.

- [ ] **Step 5: Enter Play Mode and test end-to-end**

1. Open `Navigation.unity` and enter Play Mode
2. Walk the player to a `RoomDoorInteractable` and press the interact button
3. Expected:
   - The door transition scene loads — a static first-person view of the door opening and closing
   - Behind it, the original room deactivates and the destination room activates
   - The player is teleported to the destination room's SpawnPoint
   - When the animation finishes, the transition scene unloads and the destination room is visible
   - The player can move again immediately

- [ ] **Step 6: Commit the scene changes**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity"
git commit -m "feat(room-transition): wire RoomController and RoomDoorInteractable in Navigation scene"
```

---

## Self-Review Checklist

- [x] **Spec — RoomController**: Task 2 ✓
- [x] **Spec — RoomTransitionContext**: Task 3 ✓
- [x] **Spec — IRoomOrchestrator**: Task 4 ✓
- [x] **Spec — RoomOrchestrator with full transition flow**: Task 5 ✓
- [x] **Spec — RoomDoorInteractable with lock/key logic**: Task 6 ✓
- [x] **Spec — DoorTransitionController with fallback timeout**: Task 7 ✓
- [x] **Spec — NavigationScope registration**: Task 8 ✓
- [x] **Spec — DoorTransition scene structure**: Task 9 ✓
- [x] **Spec — Room wiring in Navigation scene**: Task 10 ✓
- [x] **Spec — RoomTransitionStartedEvent + RoomTransitionedEvent**: Task 1 ✓
- [x] **Spec — AudioListener.pause + SwitchToUI/Gameplay**: Task 5 (RoomOrchestrator) ✓
- [x] **Spec — isTransitioning guard**: Task 5 (RoomOrchestrator) ✓
- [x] **Spec — DoorInteractable not removed**: Task 10 step 2 notes ✓
- [x] **No placeholders found**
- [x] **Type consistency**: `RoomTransitionContext`, `RoomController`, `IRoomOrchestrator`, `DoorTransitionController`, `RoomDoorInteractable`, `RoomTransitionStartedEvent`, `RoomTransitionedEvent` — consistent across all tasks ✓
