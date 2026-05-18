# Room Transition System — Design Spec

**Date:** 2026-05-16  
**Status:** Approved  
**Scope:** Navigation — room-to-room transition via door interaction

---

## Overview

Each room in Navigation is a self-contained prefab that can be activated or deactivated. Only one room is active at a time — the one where the player currently is. When the player interacts with a `RoomDoorInteractable`, a dedicated transition scene loads additively, showing a first-person door animation that acts as a diegetic loading screen. Behind it, the room swap and player teleport happen invisibly. When the animation ends, the transition scene unloads and the destination room is revealed.

---

## Components

### `RoomController` (MonoBehaviour)

Lives on the root GameObject of each room prefab.

- `[SerializeField] Transform spawnPoint` — position and rotation where the player appears when entering this room
- `Activate()` → `gameObject.SetActive(true)`
- `Deactivate()` → `gameObject.SetActive(false)`

### `RoomTransitionContext` (ScriptableObject)

Bridge between the Navigation scene and the DoorTransition scene. Follows the `EncounterContext` pattern already in the project.

- `GameObject DoorPrefab` — the door model to instantiate in the transition scene
- `void Set(GameObject doorPrefab, Action onComplete)` — called by `RoomOrchestrator` before loading the transition scene
- `void NotifyComplete()` — called by `DoorTransitionController` via Animation Event

Stored at `Resources/RoomTransitionContext` so the transition scene can load it without DI.

### `RoomOrchestrator` (pure C# service)

Registered in `NavigationScope` as `IInitializable` and `IRoomOrchestrator`.

**Injected dependencies:**
- `IInputService`
- `PlayerController`
- `RoomTransitionContext`
- `IPublisher<RoomTransitionStartedEvent>`
- `IPublisher<RoomTransitionedEvent>`

**Initialization (`IInitializable.Initialize()`):**  
Calls `FindObjectsOfType<RoomController>(true)`. The one that is active becomes `currentRoom`. All others are deactivated. If none is active, logs an error. If multiple are active, uses the first and deactivates the rest.

**`TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) : UniTask`:**

```
if (isTransitioning) return
isTransitioning = true

Publish RoomTransitionStartedEvent { Origin = currentRoom, Destination = destination }
inputService.SwitchToUI()
AudioListener.pause = true

// fresh completion source per transition call
var completionSource = new UniTaskCompletionSource()
roomTransitionContext.Set(doorPrefab, onComplete: () => completionSource.TrySetResult())
await SceneManager.LoadSceneAsync("DoorTransition", Additive)

currentRoom.Deactivate()
destination.Activate()
player.transform.SetPositionAndRotation(destination.SpawnPoint.position, destination.SpawnPoint.rotation)

await completionSource.Task           // waits for DoorTransitionController → context.NotifyComplete()

await SceneManager.UnloadSceneAsync("DoorTransition")
AudioListener.pause = false
inputService.SwitchToGameplay()
currentRoom = destination

Publish RoomTransitionedEvent { ActiveRoom = currentRoom }
isTransitioning = false
```

`isTransitioning` is private — never exposed on the interface.

### `IRoomOrchestrator` (interface)

```csharp
public interface IRoomOrchestrator
{
    UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab);
}
```

### `DoorTransitionController` (MonoBehaviour)

Lives in the `DoorTransition` scene.

- `[SerializeField] float animationTimeout = 5f` — fallback duration in case the Animation Event never fires
- On `Start()`:
  - Loads `RoomTransitionContext` via `Resources.Load<RoomTransitionContext>("RoomTransitionContext")`
  - Instantiates `context.DoorPrefab` as a child of the `DoorSpawner` Transform at local position zero, facing the FP camera
  - Gets the `Animator` component from the instantiated door and calls `Play` on the open→close state
  - Starts a fallback timeout coroutine — after `animationTimeout` seconds, if `context.NotifyComplete()` has not been called yet, calls it to prevent a hang
- **Animation Event** at the end of the door clip calls `context.NotifyComplete()`

**Door prefab requirement:** Each door prefab used as `doorTransitionPrefab` must have an `Animator` component with a single clip containing an Animation Event at its last frame that calls `DoorTransitionController.OnAnimationComplete()` on the root GameObject.

### `RoomDoorInteractable` (MonoBehaviour)

Replaces `DoorInteractable` on doors that connect rooms. Keeps the same lock/key logic.

```
[SerializeField] RoomController      destination
[SerializeField] GameObject          doorTransitionPrefab
[SerializeField] DoorData            data
[Inject]         IRoomOrchestrator   roomOrchestrator
```

On `Interact(InteractionContext context)`:
- If locked and no key: runs dialogue (same as `DoorInteractable`)
- If locked and key found: runs dialogue, then on complete → `roomOrchestrator.TransitionToRoomAsync(destination, doorTransitionPrefab)`
- If unlocked: immediately → `roomOrchestrator.TransitionToRoomAsync(destination, doorTransitionPrefab)`

`DoorInteractable` is **not removed** — it remains for non-room doors (decorative, barred, containers).

---

## Events

Defined in `GameEvents.cs` alongside the existing events.

```csharp
public readonly struct RoomTransitionStartedEvent
{
    public readonly RoomController Origin;
    public readonly RoomController Destination;
}

public readonly struct RoomTransitionedEvent
{
    public readonly RoomController ActiveRoom;
}
```

| System | Subscribes to | Reaction |
|---|---|---|
| Enemy AI (future) | `RoomTransitionStartedEvent` | Freeze detection loop |
| Enemy AI (future) | `RoomTransitionedEvent` | Resume detection |
| Ambient audio (future) | `RoomTransitionStartedEvent` | Silence loops |
| Ambient audio (future) | `RoomTransitionedEvent` | Resume loops |

---

## Scenes

### `DoorTransition.unity` (new)

```
DoorTransition (root)
├── FP_Camera          — static first-person camera, no scripts, no AudioListener
└── DoorSpawner        — empty Transform, child of root; DoorTransitionController attached here
```

No VContainer LifetimeScope in this scene. `DoorTransitionController` accesses `RoomTransitionContext` via `Resources.Load`.

### `Navigation.unity` (modified structure)

```
Navigation (root)
├── NavigationScope     — existing LifetimeScope, adds RoomOrchestrator registration
├── Player              — existing
├── Room_A [RoomController]  — active (starting room)
├── Room_B [RoomController]  — inactive
└── ...
```

Each room root has:
- `RoomController` component
- A `SpawnPoint` child Transform
- Room-connecting doors use `RoomDoorInteractable`

### Build Settings — scenes to register

| Scene | Notes |
|---|---|
| Boot | existing |
| Navigation | existing |
| Combat | existing |
| DoorTransition | new — loaded/unloaded additively |

---

## NavigationScope changes

```csharp
[SerializeField] private RoomTransitionContext roomTransitionContext = null!;

// In Configure():
builder.RegisterInstance(this.roomTransitionContext);
builder.Register<RoomOrchestrator>(Lifetime.Singleton)
       .AsSelf()
       .AsImplementedInterfaces();
```

MessagePipe publishers for the two new events must be registered in `GameLifetimeScope` alongside existing brokers.

---

## Edge Cases

| Case | Behavior |
|---|---|
| Double-trigger | `isTransitioning` guard — second `Interact()` is a no-op |
| No room active at start | `RoomOrchestrator.Initialize()` logs error, does nothing |
| Multiple rooms active at start | Uses first found, deactivates rest |
| Animation Event never fires | Fallback timeout in `DoorTransitionController` calls `NotifyComplete()` |
| Wwise audio (future) | If audio migrates to Wwise, add `AkSoundEngine.Suspend(true/false)` alongside `AudioListener.pause` |

---

## What does NOT change

- `CameraService`, `CameraTriggerSwitch` — cameras live inside room prefabs, activate/deactivate with them
- `SceneTransitionService` (combat) — untouched
- `DoorInteractable` — kept for non-room doors
- `NavigationScope` lifetime hierarchy — no new scopes introduced
