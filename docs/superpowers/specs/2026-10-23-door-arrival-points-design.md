# Door Arrival Points: doors own where the player appears

## Problem

A doorway is described twice today. In room D, a `RoomDoorInteractable` with
`destination = S` is the way out to S, and a separate `SpawnPoint` GameObject with
`fromRoom = S` is where the player appears when coming in from S. Both name the same
pair of rooms, are placed by hand and drift apart:

- spawns sit 0.5–2.7 m from their door, some offset sideways (e.g. `Kitchen/DR_187`
  is 1.9 m to the side of its door and faces along the wall);
- some spawns have no door back at all, or a door whose `destination` is wrong
  (`Bathroom B/DR_20`), or point at their own room (`Port_Stairs/SpawnPoint`);
- scene entries repeat the pattern across scenes: a `SceneDoorInteractable` points at a
  `targetEntryPointId`, and in the other scene a `SceneSpawnPoint` with that id and a
  `startingRoom` sits ~5 m away from the scene door of the same room.

## Intent

One source of truth per doorway. The door the player arrives *through* owns the arrival
point, so a spawn can never be missing, mismatched or detached from its door, and the
player always appears with their back to the door at a consistent distance.

## Goals

- Every `RoomDoorInteractable` and `SceneDoorInteractable` owns an **arrival point**: a
  child transform, placed by default in front of the door (centred on the doorway, at a
  fixed distance, facing into the room) and **freely movable by hand** afterwards.
- The arrival camera that `SpawnPoint` / `SceneSpawnPoint` carried today moves to the door.
- Arriving in a room through a door with no matching door back logs a **console error**
  (and falls back as today), so data mistakes are visible instead of silent.
- Room-to-room and scene-to-scene transitions both use the new model.
- `SpawnPoint` and `SceneSpawnPoint` are deleted from the codebase.

## Non-goals

- No change to save/load (`ActivateRoomImmediate` places the player from saved data,
  not from a door).
- No one-way-door feature: a room reached through a door with no way back is treated as
  a data error, per decision.
- No change to door transition visuals, locking or `DoorStateRegistry`.

## Anti-goals

- Teammates must not get broken transitions after pulling (we hit exactly this with the
  camera-zone polygons). Since the old classes are deleted, there is no runtime fallback:
  the migration is run on every scene and prefab and committed **together with** the
  code, so nobody has to run anything locally.
- The migration must not keep the old hand-placed spawn positions: every arrival point
  starts at the default placement (decision), and the designer adjusts what needs it.

## Design

### Runtime

**`DoorArrival` component** (new, `Navigation/Rooms/`), on the same GameObject as a
`RoomDoorInteractable` or `SceneDoorInteractable`:

- `[SerializeField] Transform arrivalPoint` — a child named `ArrivalPoint`.
- `[SerializeField] CinemachineCamera? arrivalCamera` — optional shot to cut to on arrival
  (what `SpawnPoint.camera` did).
- `ActivateCamera(IFixedCameraZoneService)` like the spawn classes have today.

Shared by both door types so the arrival data and its editor tooling live in one place.

**Room transitions.** `IRoomOrchestrator.TransitionToRoomAsync` receives the door being
used (today it only gets the destination room and the transition prefab), because a room
can have several doors to the same neighbour. In destination D, the arrival door is the
`RoomDoorInteractable` whose `destination` is the current room S; with several, the one
**nearest in world space to the door used** (rooms are laid out in real space, so paired
doors are adjacent — e.g. `DR_08` and `DR_08B` are 1.3 m apart). The player is placed at
its `DoorArrival.arrivalPoint` and its camera is activated, then `EnsureLiveCamera` runs
as today.

No door back → `Debug.LogError` naming D, S and the door used, then the current fallback
(room root + `EnsureLiveCamera`).

**Scene transitions.** `SceneDoorInteractable` gains `[SerializeField] string entryPointId`
— the id other scenes use to arrive through this door (the role of
`SceneSpawnPoint.entryPointId`). On scene entry `RoomOrchestrator` looks for the
`SceneDoorInteractable` with `EntryPointId == pending id`, places the player at its
arrival point, starts in the `RoomController` it belongs to (replacing
`SceneSpawnPoint.startingRoom`) and activates its camera. Not found → `LogError` + the
current fallback. `targetEntryPointId` on the origin door is unchanged, so doors in other
scenes need no edits.

**No legacy path.** `SpawnPoint` and `SceneSpawnPoint` are deleted once the migration
has run. An arrival door without `DoorArrival` is a data error: `LogError` + the room
root fallback, like a missing door back.

### Default placement

Same geometry as today's `SpawnPointAligner`:

- door plane from the thinnest horizontal axis of the door's renderer bounds (the door
  pivot and `forward` are not reliable — several doors have the pivot off-centre and
  `forward` along the wall);
- position = doorway centre + interior normal × distance, height at the floor height
  the spawns use today (y = 1.0 above the door base in current data: keep "door bounds
  min y + 1.0"); rotation faces along the interior normal (back to the door);
- distance: one project-wide value, editable, default **1.0 m** (today's
  `SpawnPointAligner.Distance` EditorPref).

**Interior side.** During migration, the side of the old spawn when one exists. Otherwise
(new doors, doors with no spawn) the side facing the centre of the room's `Bounds` child
(falling back to the room's combined renderer bounds).

### Editor tooling

- `DoorArrival` inspector: **Reset Arrival Point** button (default placement), and a
  scene-view gizmo: arrow showing position and facing, in the door colour.
- Camera Zone Viewer: the existing **Align Spawns** button becomes **Reset Arrivals** for
  the selected room (same distance field). `SpawnPointAligner` is replaced by the shared
  placement code.
- Adding `RoomDoorInteractable` or `SceneDoorInteractable` to an object auto-adds
  `DoorArrival` with a placed `ArrivalPoint` (`RequireComponent` + `Reset`).
- `NavigationValidationToolbar`: validates doors instead of spawns — door without
  `DoorArrival`, `arrivalPoint` unset, and room pairs where D is reachable from S but D
  has no door back (the same error the runtime logs). Duplicate scene `entryPointId`s.
- `RoomGraphWindow`: "has spawn point" becomes "has a door back".
- `NavigationHierarchyLabels`: label doors with their arrival instead of spawn objects.

### Migration

One editor command, `CrimsonDraft → Doors → Migrate Spawn Points To Door Arrivals`.
Idempotent: a door that already has `DoorArrival` is not re-placed. It runs while
`SpawnPoint` / `SceneSpawnPoint` still exist; only then are the two scripts deleted, and
the code, the migrated scenes and the prefabs land together.

Order: **room prefabs first** (every prefab under `Assets/Prefabs` that contains a
`RoomController`), **then every scene in `Assets/Scenes/Production`**. Doors are nested
prefab instances (`DR_xx`) inside room prefabs, and the interior side needs the room, so
`DoorArrival` is added on the door instance inside the room prefab (an override of the
room prefab), never on the bare door prefab asset. Scene instances of a room then already
have it and are skipped; rooms that only exist in a scene are converted by the scene pass.
The camera copy (step 2) also runs in the scene pass, so a camera a scene assigned to a
spawn on top of its room prefab is not lost.

For every door:
1. Add `DoorArrival` and an `ArrivalPoint` child at the default placement.
2. Copy the camera from the old spawn it replaces: for a `RoomDoorInteractable` in D with
   destination S, the `SpawnPoint` in D with `fromRoom == S` (nearest to the door if
   several); for a `SceneDoorInteractable`, the `SceneSpawnPoint` of the same room
   (nearest), whose `entryPointId` is also copied to the door.
3. Remove the old `SpawnPoint` / `SceneSpawnPoint` component, and its GameObject when it
   has nothing else on it and no children.

Spawns that match no door (today: `Kitchen/DR_33`, `Bathroom B/DR_20`,
`Port_Stairs/SpawnPoint`) are removed like the rest, and each is listed as an error with
its room, `fromRoom` and position, so the door data can be fixed afterwards (the runtime
error then points at the same rooms).

### Testing

EditMode tests for the pure parts:
- arrival-door selection: matching destination, nearest of several, none → null;
- default placement: thin-axis plane, interior side, distance, facing;
- an arrival door without `DoorArrival` reports an error and falls back to the room root.

Manual: walk every door pair of `Deck_B` and the B↔C stairs after migration.

## Calls made by me (not discussed)

- `DoorArrival` as a separate component shared by both door types, rather than fields
  duplicated on each door class.
- `TransitionToRoomAsync` takes the door used, to disambiguate several doors to the same
  room by proximity.
- Migration run on all scenes/prefabs and committed with the code, instead of a runtime
  fallback, so teammates are never on unmigrated data.
- Unmatched spawns are deleted with the rest and reported, not kept.
- Arrival height = door base + 1.0 m, matching the y every spawn has today.

## Left to the builder

- Gizmo look, exact menu labels, how the shared placement code is split between runtime
  and editor assemblies.
