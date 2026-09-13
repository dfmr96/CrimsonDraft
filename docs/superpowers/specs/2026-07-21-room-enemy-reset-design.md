# Room Enemy Position Reset — Design Spec

## Problem

If the player lures an enemy toward a room's door, leaves the room, and re-enters, the enemy is left wherever it last was — sometimes right at the door. Re-entering the room can then trigger combat immediately, before the player has any chance to react.

## Goal

Every enemy in a room has a designer-authored "spawn" position and rotation. Whenever the room becomes active (first entry into the scene, or re-entering through a door transition), every still-alive enemy in that room snaps back to its spawn transform before the player can perceive it, and resumes patrolling from its first waypoint.

## Architecture

Room activation is already a single choke point: `RoomController.Activate()`, called both by `RoomOrchestrator.Initialize()` (initial scene entry) and `RoomOrchestrator.TransitionToRoomAsync()` (door transitions, while the `DoorTransition` cutscene scene covers the screen). This feature hooks into that same method rather than introducing a new lifecycle event.

`RoomController` gains a designer-populated array pairing each `EnemyNavAgent` child with a `Transform` that marks its spawn point. An editor-only button captures that array from the current scene state. At runtime, `Activate()` walks the array and asks each enemy to reset itself; the actual reset logic (position, rotation, patrol index, transient AI flags) lives on `EnemyNavAgent` itself, since it already owns all of that state.

## Components

### `RoomController` (`Assets/Scripts/Navigation/Rooms/RoomController.cs`)

- New serialized type:
  ```csharp
  [Serializable]
  private sealed class EnemySpawnEntry
  {
      public EnemyNavAgent enemy = null!;
      public Transform     spawnPoint = null!;
  }
  ```
- New field: `[SerializeField] private EnemySpawnEntry[] enemySpawns = Array.Empty<EnemySpawnEntry>();`
- New editor-only button (NaughtyAttributes `[Button]`, matching the project's existing "cache children" convention, e.g. `NavigationScope.CacheSceneEnemies`):
  - `[Button("Cache Room Enemies")]` → `CacheRoomEnemies()`, wrapped in `#if UNITY_EDITOR`.
  - Behavior: **destructive recreate**. Destroys every existing spawn-point child `Transform` referenced in `enemySpawns` (via `DestroyImmediate`), then finds all `EnemyNavAgent` via `GetComponentsInChildren<EnemyNavAgent>(includeInactive: true)`, and for each creates a new child `Transform` (parented under the room, named e.g. `"EnemySpawn_<enemy name>"`) positioned/rotated at that enemy's *current* world position/rotation at the time the button is pressed. Rebuilds `enemySpawns` from scratch. Calls `UnityEditor.EditorUtility.SetDirty(this)`.
  - Re-pressing the button after adding a new enemy to the room regenerates *all* spawn points (including ones a designer may have hand-tuned since the last press) — this is intentional per the approved design (simplicity over preserving manual edits).
- `Activate()` changes from:
  ```csharp
  public void Activate() => gameObject.SetActive(true);
  ```
  to also resetting every still-alive cached enemy after activation:
  ```csharp
  public void Activate()
  {
      gameObject.SetActive(true);
      for (int i = 0; i < this.enemySpawns.Length; i++)
      {
          var entry = this.enemySpawns[i];
          if (entry?.enemy == null || entry.spawnPoint == null) continue;
          if (!entry.enemy.gameObject.activeSelf) continue; // defeated or otherwise hidden
          entry.enemy.ResetToSpawn(entry.spawnPoint.position, entry.spawnPoint.rotation);
      }
  }
  ```

### `EnemyNavAgent` (`Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs`)

New public method:

```csharp
public void ResetToSpawn(Vector3 position, Quaternion rotation)
{
    if (!isActiveAndEnabled) return;

    navAgent.Warp(position);
    transform.rotation = rotation;

    combatTriggered = false;
    dialoguePaused  = false;

    path?.ResetIndex();
    TransitionTo(GuardAlertState.Patrol);
}
```

Notes:
- `navAgent.Warp(...)` is required instead of setting `transform.position` directly — it keeps the `NavMeshAgent`'s internal path-planning state in sync with the mesh, which plain transform assignment would desync.
- Clearing `combatTriggered`/`dialoguePaused` is defensive: neither should realistically be `true` on a room re-entry through the normal door-transition path (combat and dialogue both suspend room transitions), but resetting them avoids leaving the agent in a stuck state if some other path leaves them set.
- `TransitionTo(GuardAlertState.Patrol)` already resets `EnemyDetectionSensor` and — since the patrol index was just reset to 0 — points the agent at the first waypoint. No duplicate logic needed.
- If `patrolEnabled` is `false` or the agent has no waypoints, the existing guards inside `TransitionTo`/`UpdatePatrol` already make this a safe no-op for the patrol-specific part; the enemy still gets repositioned and returned to `Patrol` (idle) state.
- Rotation set here can be visually overridden almost immediately once `navAgent.updateRotation` steering kicks in on the walk to waypoint 0 — this matches the engine's existing patrol steering behavior and is not something this feature needs to special-case.

### `EnemyPatrolPath` (`Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs`)

New public method:

```csharp
public void ResetIndex() => index = 0;
```

## Data Flow

1. Designer places an enemy in a room at the position/facing it should have when the room is (re)entered.
2. Designer selects the room's `RoomController` and presses **Cache Room Enemies** in the Inspector.
3. All previously generated spawn-point children are destroyed; one fresh spawn-point `Transform` is created per `EnemyNavAgent` found in the room's hierarchy (including inactive ones), at that enemy's current transform. `enemySpawns` is rebuilt to reference these pairs.
4. At runtime, whenever `RoomController.Activate()` runs — initial room selection at scene load, or arriving via a door transition — every cached, still-active enemy is warped back to its spawn transform and returned to `Patrol` state at waypoint 0.
5. Enemies already marked defeated (`EnemyStateRegistry`-driven `gameObject.SetActive(false)`) are skipped, since their `GameObject.activeSelf` is `false`.

## Edge Cases

- **No enemies in the room:** `enemySpawns` is empty; `Activate()`'s loop is a no-op.
- **Enemy without a patrol path / `patrolEnabled = false`:** reset still repositions it and sets state to `Patrol`; the patrol-specific movement stays inactive exactly as it already does today.
- **Defeated enemy:** skipped via the `activeSelf` check in `Activate()`, matching how `EnemyStateRegistry`/`OnCombatEnded` already hides defeated enemies persistently within the scene session.
- **Manually deleted spawn-point Transform after caching:** `Activate()`'s null-check on `entry.spawnPoint` skips that entry rather than throwing; the room still activates normally. (Re-running **Cache Room Enemies** regenerates it.)
- **First-ever activation of a room (scene load):** resetting to the designer-authored position is idempotent — the enemy is already there, so this is a harmless no-op in practice.

## Testing

- `EnemyPatrolPath.ResetIndex()` gets a small EditMode test (plain `MonoBehaviour` with no external DI, easy to instantiate directly).
- The full integration (`RoomController.Activate()` → `EnemyNavAgent.ResetToSpawn()`) is **not** covered by an automated test: `EnemyNavAgent` requires a real `NavMeshAgent` on a baked NavMesh plus several DI-injected dependencies to run meaningfully, matching the existing project boundary where `MonoBehaviour`s this deeply coupled to `Update()`/Unity systems (e.g. `CombatOrchestrator`) are verified manually instead. Manual Play Mode verification: enter a room, lure an enemy toward the door, leave, re-enter, confirm the enemy is back at its authored spawn position/facing and patrolling from its first waypoint.
- `RoomController`'s new `[Button]` method is editor-only tooling, consistent with every other `[Button]` method in the codebase (none have dedicated tests).
