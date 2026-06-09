# Inter-Scene Transition Design
*Deck B → Deck C and back*

Date: 2026-06-09

## Problem

Two independent issues that must be solved together:

1. **No inter-scene door interactable.** `RoomDoorInteractable` references a `RoomController` in the *same scene*. There is no equivalent that transitions to a different Unity scene (e.g. Deck C).

2. **Door unlock state is lost on scene transition.** `bool unlocked` on `RoomDoorInteractable` is runtime memory — it is destroyed when `NavigationScope` unloads. Unlocked doors reset on every scene change.

## Constraints

- Door unlock state must survive scene transitions (in-memory across NavigationScope changes)
- State must be designed for future save system compatibility (serializable)
- Inter-scene transitions must use the same `DoorTransition` animation as intra-scene transitions
- Inter-scene connections are unidirectional per object (two separate objects for bidirectional travel)

## Approach

New `FloorTransitionService` at `GameLifetimeScope` level (survives NavigationScope changes). Handles scene unload/load while reusing the existing `DoorTransition` animation mechanism unchanged.

---

## Components

### 1. `DoorStateRegistry` — state persistence

Pure C# service at `GameLifetimeScope`:

```
IsUnlocked(string doorId) → bool
SetUnlocked(string doorId)
GetState()                 → IReadOnlyDictionary<string, bool>   // future save
LoadState(dict)                                                   // future save
```

Doors have a stable `[SerializeField] string doorId` (e.g. `"deckb_corridor_stairwell"`).

### 2. `IDoorInteractable` — shared door interface

```
string DoorId         { get; }
void   RestoreFromRegistry()   // sets bool unlocked from registry
```

Both `RoomDoorInteractable` and `SceneDoorInteractable` implement this.

### 3. `NavigationScope` — edit-time door cache

```
[SerializeField] RoomDoorInteractable[]  cachedRoomDoors
[SerializeField] SceneDoorInteractable[] cachedSceneDoors

[ContextMenu("Cache Scene Doors")]
CacheSceneDoors()  →  FindObjectsByType for both arrays (Editor only)
```

### 4. `RoomOrchestrator.Initialize()` — zero runtime search

Uses cached arrays from `NavigationScope`. For each door:
1. `door.Construct(this)` — injects `IRoomOrchestrator`
2. `door.RestoreFromRegistry()` — sets `bool unlocked` from `DoorStateRegistry`

On unlock during gameplay: door updates both `this.unlocked = true` and `registry.SetUnlocked(doorId)`.

### 5. `SceneEntryContext` — cross-scene spawn data

ScriptableObject at `GameLifetimeScope`:

```
SetPendingEntry(string entryPointId)
string? Consume()   // returns and clears the pending ID (one-shot)
```

Written by `FloorTransitionService` before loading the destination scene.  
Read by `RoomOrchestrator.Initialize()` after the new scene loads.

### 6. `SceneSpawnPoint` — per-scene entry point marker

MonoBehaviour placed in each scene at every inter-scene entry point:

| Field | Type | Description |
|---|---|---|
| `entryPointId` | `string` | Matches what `SceneEntryContext` carries |
| `startingRoom` | `RoomController` | Room to activate in this scene |
| `camera` | `CinemachineCamera?` | Camera to activate on arrival (optional) |

Transform defines player spawn position/rotation.

### 7. `RoomOrchestrator.Initialize()` — spawn resolution (updated)

```
entryId = SceneEntryContext.Consume()

if entryId != null:
    spawnPoint = find SceneSpawnPoint where entryPointId == entryId
    startingRoom = spawnPoint.startingRoom
    player.SetPositionAndRotation(spawnPoint.transform)
    spawnPoint.ActivateCamera()
else:
    startingRoom = context.StartingRoom   // existing path, unchanged
```

### 8. `IFloorTransitionService` + `FloorTransitionService`

Registered at `GameLifetimeScope`. Injects: `IInputService`, `SceneEntryContext`, `RoomTransitionContext`.

```
UniTask TransitionToFloorAsync(
    string fromScene,
    string toScene,
    string entryPointId,
    GameObject doorPrefab
)
```

Transition flow:
```
1. inputService.SwitchToDoorTransition()
2. sceneEntryContext.SetPendingEntry(entryPointId)
3. roomTransitionContext.Set(doorPrefab, skipAction, onComplete: tcs)
4. SceneManager.LoadSceneAsync("DoorTransition", Additive)
      ↳ DoorTransitionController reads RoomTransitionContext from Resources (unchanged)
5. await tcs                                  // wait for door animation
6. SceneManager.UnloadSceneAsync(fromScene)   // destroy old NavigationScope
7. SceneManager.LoadSceneAsync(toScene, Additive)
      ↳ new NavigationScope initializes
      ↳ RoomOrchestrator.Initialize() consumes SceneEntryContext
8. SceneManager.UnloadSceneAsync("DoorTransition")
9. inputService.SwitchToGameplay()
```

`DoorTransitionController` is not modified — it already reads `RoomTransitionContext` from Resources.

### 9. `SceneDoorInteractable`

MonoBehaviour. Registered in `NavigationScope.Configure()` via `FindObjectsByType` (same pattern as `CombatTrigger`).

Serialized fields:

| Field | Type |
|---|---|
| `doorId` | `string` |
| `data` | `DoorData` |
| `targetSceneName` | `string` |
| `targetEntryPointId` | `string` |
| `doorTransitionPrefab` | `GameObject` |

Injected via `Construct`: `IFloorTransitionService`, `DoorStateRegistry`.

`Interact()` logic is identical to `RoomDoorInteractable` (same locked/key/Yarn flow). On success:
```
floorService.TransitionToFloorAsync(
    gameObject.scene.name, targetSceneName, targetEntryPointId, doorTransitionPrefab)
```
On unlock: `registry.SetUnlocked(doorId)` + `this.unlocked = true`, then transition.

---

## Changed Files Summary

| File | Change |
|---|---|
| `GameLifetimeScope` | Register `DoorStateRegistry`, `SceneEntryContext`, `RoomTransitionContext` (moved), `FloorTransitionService` |
| `NavigationScope` | Remove `RoomTransitionContext` registration; add cached door arrays + `[ContextMenu]`; register `SceneDoorInteractable` components |
| `RoomOrchestrator` | Inject `SceneEntryContext` + `DoorStateRegistry`; use cached arrays; call `RestoreFromRegistry()`; resolve spawn from `SceneEntryContext` |
| `RoomDoorInteractable` | Add `doorId`, implement `IDoorInteractable`, add `RestoreFromRegistry()`, update registry on unlock |

**Unchanged:** `DoorTransitionController`, `RoomTransitionContext`, all Yarn/dialogue logic.
