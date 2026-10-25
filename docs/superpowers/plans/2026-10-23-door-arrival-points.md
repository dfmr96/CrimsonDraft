# Door Arrival Points Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Doors own where the player appears (a movable `ArrivalPoint` child placed in front of the door, back to it), replacing and deleting `SpawnPoint` and `SceneSpawnPoint`.

**Architecture:** A `DoorArrival` component on each `RoomDoorInteractable` / `SceneDoorInteractable` holds the arrival transform and optional arrival camera. `RoomOrchestrator` finds the arrival door in the destination room (door back to the origin room, nearest to the door used) or, on scene entry, the scene door with the pending `entryPointId`. A one-shot editor migration converts every room prefab and production scene, then the old classes are deleted.

**Tech Stack:** Unity 6000.3, C# (`#nullable enable`), VContainer, Cinemachine 3, NUnit EditMode tests run through the Unity Test Runner / MCP `run_tests`.

**Spec:** `docs/superpowers/specs/2026-10-23-door-arrival-points-design.md`

> **Status:** Done. Tasks 1–5 and Task 6 Step 1 landed first with a transition period (see the spec); on 2026-10-25 the migration ran over every room prefab and production scene (82 arrivals, 66 cameras copied, 5 unmatched spawns: `Port_Stairs/SpawnPoint`, `Kitchen/DR_33`, `Kitchen/DR_187`, `Kitchen/DR_121`, `Radio/DR_30B`) and Task 7 removed the fallback, `SpawnPoint`, `SceneSpawnPoint`, the aligner, the migration and the **Migrate Doors** button, and restored `RequireComponent(DoorArrival)`.

## Global Constraints

- All files `#nullable enable`; serialized refs `= null!`, optional ones `?`; injected fields set in `[Inject] Construct`.
- Default arrival distance **1.0 m** from the doorway centre; arrival height **door bounds `min.y` + 1.0 m**; facing = interior normal (back to the door).
- Door plane = the thinner horizontal axis (`x` vs `z`) of the door's combined `Renderer` bounds; never the door pivot or `forward`.
- Missing door back / missing entry door / arrival door without `DoorArrival` → `Debug.LogError`, then place the player at the destination room root (room) or use `context.StartingRoom` (scene entry).
- No runtime fallback to `SpawnPoint` / `SceneSpawnPoint`. Work on a branch; **push / merge only after Task 7**, so code and migrated data reach teammates together.
- No `Co-Authored-By` trailers on commits (repo `CLAUDE.md`).
- Run tests with MCP `run_tests` (mode `EditMode`), or the Test Runner filtered by class.

## Review Focus

- Two doors from D lead back to S (several doors between the same rooms): the door nearest to the one used must win, not the first found. → test in Task 2.
- Arrival door exists but has no `DoorArrival` (data not migrated, new door added by hand): error + room root, no exception. → test in Task 3.
- Door with no renderers (bare interactable): placement must not throw and must still face into the room. → test in Task 1.
- Interior hint exactly on the door plane (room `Bounds` centred on the door): pick a deterministic side instead of a zero normal. → test in Task 1.
- Two scene doors with the same `entryPointId`: first found is used and validation reports the duplicate. → test in Task 2 (lookup) and validation in Task 5.

---

## File Structure

- Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorArrival.cs` — component: arrival transform, arrival camera, `ActivateCamera`, editor `Reset` placement.
- Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorArrivalPlacement.cs` — pure static geometry (door bounds, interior hint, default pose).
- Create `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorArrivalLookup.cs` — pure static lookup of the arrival door / entry door.
- Modify `Rooms/IRoomOrchestrator.cs`, `Rooms/RoomOrchestrator.cs`, `Interactables/RoomDoorInteractable.cs`, `Interactables/SceneDoorInteractable.cs`.
- Create `Scripts/Navigation/Editor/DoorArrivalEditor.cs` — inspector button + gizmo; `Scripts/Navigation/Editor/DoorArrivalSettings.cs` — distance EditorPref + "reset room" helper.
- Modify editor tools: `CameraZoneViewerWindow.cs`, `NavigationValidationToolbar.cs`, `RoomGraphWindow.cs`, `NavigationHierarchyLabels.cs`.
- Create (temporary, deleted in Task 7) `Scripts/Navigation/Editor/SpawnPointMigration.cs`.
- Delete in Task 7: `Rooms/SpawnPoint.cs`, `Rooms/SceneSpawnPoint.cs`, `Editor/SpawnPointAligner.cs`, `Editor/SpawnPointMigration.cs` (+ `.meta`).
- Tests (all `Game/CrimsonDraft/Assets/Tests/EditMode/`): create `DoorArrivalPlacementTests.cs`, `DoorArrivalLookupTests.cs`; modify `RoomOrchestratorInitTests.cs` and the five `IRoomOrchestrator` fakes.

All paths below are relative to `Game/CrimsonDraft/Assets/Scripts/Navigation/` unless they start with `Game/` or `Tests/`.

---

### Task 1: `DoorArrival` component and default placement

**Files:**
- Create: `Rooms/DoorArrivalPlacement.cs`, `Rooms/DoorArrival.cs`
- Modify: `Interactables/RoomDoorInteractable.cs`, `Interactables/SceneDoorInteractable.cs` — add `[RequireComponent(typeof(DoorArrival))]` so a new door gets its arrival (and `DoorArrival.Reset` places it) automatically.
- Test: `Tests/EditMode/DoorArrivalPlacementTests.cs`

**Interfaces:**
- Produces:
  - `public static class DoorArrivalPlacement` (namespace `CrimsonDraft.Navigation.Rooms`)
    - `public const float DefaultDistance = 1f;` `public const float ArrivalHeight = 1f;`
    - `public static Bounds DoorBounds(Component door)` — combined `Renderer` bounds of `door` and children; none → `new Bounds(door.transform.position, new Vector3(0.2f, 2f, 1.2f))`.
    - `public static Vector3 InteriorHint(RoomController room)` — centre of the room's `Bounds` child (combined `Collider` then `Renderer` bounds of it); no such child → combined renderer bounds centre of the room; nothing → `room.transform.position`.
    - `public static Pose DefaultPose(Bounds doorBounds, Vector3 interiorHint, float distance)` — normal = `Vector3.right` if `size.x < size.z` else `Vector3.forward`, flipped toward `interiorHint`; hint on the plane (`|dot| < 1e-4`) keeps the unflipped axis. Position = `center + normal * distance` with `y = doorBounds.min.y + ArrivalHeight`; rotation `LookRotation(normal)`.
  - `public sealed class DoorArrival : MonoBehaviour` (namespace `CrimsonDraft.Navigation.Rooms`)
    - `[SerializeField] Transform arrivalPoint = null!;` `[SerializeField] CinemachineCamera? arrivalCamera;`
    - `public Transform ArrivalPoint`, `public CinemachineCamera? ArrivalCamera`
    - `public void ActivateCamera(IFixedCameraZoneService zoneService)` — same as `SpawnPoint.ActivateCamera`.
    - `public void PlaceAtDefault(float distance, Vector3 interiorHint)` — creates the child `"ArrivalPoint"` if `arrivalPoint` is null, then sets its world pose to `DefaultPose(DoorBounds(this), interiorHint, distance)`.
    - `#if UNITY_EDITOR private void Reset()` → `PlaceAtDefault(DefaultDistance, hint)` where hint = `InteriorHint(parent RoomController)` or, without a room, `transform.position + transform.forward`.

- [ ] **Step 1: Write the failing tests** in `DoorArrivalPlacementTests`:

```csharp
[Test] public void DefaultPose_thinXDoor_facesIntoRoomOnX()
{
    var bounds = new Bounds(new Vector3(10, 1.1f, 5), new Vector3(0.3f, 2.2f, 1.2f));
    var pose = DoorArrivalPlacement.DefaultPose(bounds, new Vector3(4, 1, 5), 1f);
    Assert.That(pose.position.x, Is.EqualTo(9f).Within(1e-4));
    Assert.That(pose.position.z, Is.EqualTo(5f).Within(1e-4));
    Assert.That(pose.position.y, Is.EqualTo(1f).Within(1e-4)); // min.y 0 + 1
    Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.left), Is.EqualTo(1f).Within(1e-4));
}
[Test] public void DefaultPose_thinZDoor_usesZAxisAndDistance()  // size (2.2, 2.5, 0.13), hint at z-3, distance 1.5 → z = center.z - 1.5, faces -Z
[Test] public void DefaultPose_hintOnDoorPlane_keepsPositiveAxis() // hint == bounds.center → faces +X for a thin-X door
[Test] public void DoorBounds_withoutRenderers_returnsSlabOnPivot() // empty GameObject at (3,0,4) → center (3,0,4), size (0.2,2,1.2)
[Test] public void PlaceAtDefault_createsArrivalChildOnce()
{
    var door = new GameObject("Door").AddComponent<DoorArrival>();
    door.PlaceAtDefault(1f, door.transform.position + Vector3.right * 5);
    var first = door.ArrivalPoint;
    door.PlaceAtDefault(1f, door.transform.position + Vector3.right * 5);
    Assert.AreSame(first, door.ArrivalPoint);
    Assert.AreEqual(door.transform, first.parent);
    Assert.AreEqual(1, door.transform.childCount);
    Object.DestroyImmediate(door.gameObject);
}
```

- [ ] **Step 2: Run** `run_tests` filter `CrimsonDraft.Tests.DoorArrivalPlacementTests` — Expected: compile failure (`DoorArrivalPlacement` not found).
- [ ] **Step 3: Implement** both files per the Interfaces block.
- [ ] **Step 4: Run** the same filter — Expected: 5 passed.
- [ ] **Step 5: Commit** `feat(doors): add DoorArrival component and default arrival placement`.

---

### Task 2: Arrival door lookup and scene door entry id

**Files:**
- Create: `Rooms/DoorArrivalLookup.cs`
- Modify: `Interactables/SceneDoorInteractable.cs` (add `[SerializeField] private string entryPointId = "";` and `public string EntryPointId => this.entryPointId;`, tooltip "Id other scenes use to arrive through this door (their targetEntryPointId).")
- Test: `Tests/EditMode/DoorArrivalLookupTests.cs`

**Interfaces:**
- Consumes: `RoomDoorInteractable.Destination`, `SceneDoorInteractable.EntryPointId`.
- Produces: `public static class DoorArrivalLookup`
  - `public static RoomDoorInteractable? FindArrivalDoor(RoomController destination, RoomController origin, Vector3 departurePosition)` — among `destination.GetComponentsInChildren<RoomDoorInteractable>(true)` with `Destination == origin`, the one whose `transform.position` is nearest (horizontal XZ) to `departurePosition`; none → null.
  - `public static SceneDoorInteractable? FindEntryDoor(IEnumerable<SceneDoorInteractable> doors, string entryPointId)` — first with `EntryPointId == entryPointId` (ordinal, non-empty); none → null.

- [ ] **Step 1: Write the failing tests**: `FindArrivalDoor_returnsDoorLeadingBackToOrigin`, `FindArrivalDoor_withTwoDoorsBack_picksNearestToDeparture` (doors at x=0 and x=10, departure x=9 → the x=10 door), `FindArrivalDoor_ignoresDoorsToOtherRooms`, `FindArrivalDoor_noDoorBack_returnsNull`, `FindEntryDoor_matchesId`, `FindEntryDoor_duplicateIds_returnsFirst`, `FindEntryDoor_emptyOrUnknownId_returnsNull`. Set `destination` / `entryPointId` through `SerializedObject` like `RoomOrchestratorInitTests` does.
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.DoorArrivalLookupTests` — Expected: compile failure.
- [ ] **Step 3: Implement** `DoorArrivalLookup` and the `SceneDoorInteractable` field.
- [ ] **Step 4: Run** — Expected: 7 passed.
- [ ] **Step 5: Commit** `feat(doors): find arrival door by origin room and scene entry id`.

---

### Task 3: `RoomOrchestrator` places the player through doors

**Files:**
- Modify: `Rooms/IRoomOrchestrator.cs`, `Rooms/RoomOrchestrator.cs` (`ResolveStartingRoom`, `TransitionToRoomAsync`, remove `FindSpawnPoint`), `Interactables/RoomDoorInteractable.cs:187` (`CrossDoor`)
- Modify fakes: `Tests/EditMode/MapStateTrackerTests.cs:49`, `OperatorCorpseBootstrapTests.cs:63`, `RoomDoorInteractableTests.cs:370`, `SaveControllerTests.cs:54`, `SaveGameLoaderTests.cs:60`
- Test: `Tests/EditMode/RoomOrchestratorInitTests.cs`

**Interfaces:**
- Consumes: Task 1 `DoorArrival`, Task 2 `DoorArrivalLookup`.
- Produces: `UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition)` on `IRoomOrchestrator`; `RoomDoorInteractable.CrossDoor` passes `this.transform.position`.
- Behaviour: room transition → `FindArrivalDoor(destination, currentRoom, departurePosition)`; door with `DoorArrival` → player at `ArrivalPoint` pose, `ActivateCamera`; otherwise `LogError` (`"[RoomOrchestrator] No door back to '{origin}' in '{destination}'"` or `"... door '{door}' has no DoorArrival"`) and the room root; `EnsureLiveCamera` as today. Scene entry → `FindEntryDoor(FindObjectsByType<SceneDoorInteractable>(Include, None), entryId)`; found with `DoorArrival` → player at its arrival pose, camera, starting room = `door.GetComponentInParent<RoomController>(true)`; otherwise `LogError` and `context.StartingRoom`.

- [ ] **Step 1: Rewrite the failing test** `Initialize_whenSceneEntryContextHasPendingEntry_activatesSpawnPointStartingRoom` as `Initialize_withPendingEntry_startsInRoomOfEntryDoorAtItsArrival`: a `SceneDoorInteractable` + `DoorArrival` under `roomB` with `entryPointId = "entry-1"`, arrival placed at (5,1,3); assert `roomB` active, `roomA` inactive, player position (5,1,3). Add `Initialize_withPendingEntryButNoDoor_logsErrorAndUsesStartingRoom` (`LogAssert.Expect(LogType.Error, new Regex("entry-1"))`, `roomA` active). Add `Initialize_withEntryDoorWithoutArrival_logsErrorAndUsesStartingRoom`.
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.RoomOrchestratorInitTests` — Expected: new tests fail.
- [ ] **Step 3: Implement** the orchestrator changes, the new interface signature, `CrossDoor`, and update the five fakes to the 3-parameter signature (the `RoomDoorInteractableTests` fake keeps recording `destination`; add `LastDeparture` if it already records arguments).
- [ ] **Step 4: Run** the whole EditMode suite — Expected: all pass (no test references `FindSpawnPoint`).
- [ ] **Step 5: Commit** `refactor(rooms): place the player at the arrival door instead of SpawnPoint`.

---

### Task 4: Editor tooling for arrivals

**Files:**
- Create: `Editor/DoorArrivalSettings.cs`, `Editor/DoorArrivalEditor.cs`
- Modify: `Editor/CameraZoneViewerWindow.cs` (`DrawSpawnAlignControls` → `DrawArrivalControls`)

**Interfaces:**
- Consumes: Task 1.
- Produces: `public static class DoorArrivalSettings` (namespace `CrimsonDraft.Navigation.Editor`)
  - `public static float Distance { get; set; }` — EditorPref `"CrimsonDraft.SpawnDoorDistance"` (same key as `SpawnPointAligner`, so the value carries over), default `DoorArrivalPlacement.DefaultDistance`, min 0.1.
  - `public static int ResetRoom(RoomController room)` — `Undo.RecordObject` on each arrival transform, `PlaceAtDefault(Distance, InteriorHint(room))` for every `DoorArrival` in the room; returns the count; marks the scene dirty.
- `DoorArrivalEditor` (`[CustomEditor(typeof(DoorArrival))]`): default inspector + button **Reset Arrival Point** (undoable); `OnSceneGUI`/`[DrawGizmo]` draws an arrow from the arrival point along its forward and a dotted line to the door.
- Viewer: button **Reset Arrivals** (tooltip "Put every door arrival of this room back in front of its door at the distance on the right.") calls `ResetRoom(CurrentRoom)`; the distance field binds to `DoorArrivalSettings.Distance`.

- [ ] **Step 1: Implement** the three changes (no unit test: editor UI; `ResetRoom` is a loop over the tested `PlaceAtDefault`).
- [ ] **Step 2: Verify**: compile clean (`read_console` errors filter `error CS` → none); in `Deck_B_Development`, pick Cheff_Room in the viewer, press Reset Arrivals with a door that has `DoorArrival` added by hand; arrival lands 1 m inside the room facing away from the door; Ctrl+Z restores.
- [ ] **Step 3: Commit** `feat(editor): reset door arrivals from the inspector and the camera viewer`.

---

### Task 5: Validation, room graph and hierarchy labels use doors

**Files:**
- Modify: `Editor/NavigationValidationToolbar.cs:39-55`, `Editor/RoomGraphWindow.cs:64-107,332`, `Editor/NavigationHierarchyLabels.cs:61`

**Interfaces:**
- Consumes: Tasks 1–2.
- Behaviour:
  - Validation replaces the `SpawnPoint` loop with: every `RoomDoorInteractable` / `SceneDoorInteractable` without `DoorArrival` or with `ArrivalPoint == null` → warning; every room pair where S has a door to D but `DoorArrivalLookup.FindArrivalDoor(D, S, door.position)` is null → warning `"No door back to '{S}' in '{D}'"`; duplicate non-empty `SceneDoorInteractable.EntryPointId` → warning listing the doors.
  - `RoomGraphWindow`: `HasSpawnPoint` renamed `HasDoorBack`, computed with `FindArrivalDoor` instead of spawns; same drawing.
  - Hierarchy labels: the spawn label moves to GameObjects with `DoorArrival` (label `"⇠ arrival"` plus the arrival camera name when set).

- [ ] **Step 1: Implement**.
- [ ] **Step 2: Verify**: compile clean; run `Tools/Validate Navigation References` in `Deck_B_Development` — warnings list doors without `DoorArrival` (expected before Task 6) and the known missing door backs (`Kitchen` from `Exterior_Hallway`, `Bathroom B` from `Hallway_B1`).
- [ ] **Step 3: Commit** `refactor(editor): validate and graph door arrivals instead of spawn points`.

---

### Task 6: Migrate every room prefab and production scene

**Files:**
- Create: `Editor/SpawnPointMigration.cs` (deleted in Task 7)
- Data: room prefabs under `Game/CrimsonDraft/Assets/Prefabs/`, scenes in `Game/CrimsonDraft/Assets/Scenes/Production/`

**Interfaces:**
- Consumes: Tasks 1, 4 (`DoorArrivalSettings.Distance`), old `SpawnPoint` / `SceneSpawnPoint` (still present).
- Produces: menu `CrimsonDraft/Doors/Migrate Spawn Points To Door Arrivals`.
- Algorithm per room (`RoomController`), in this order: **(a)** every prefab under `Assets/Prefabs` whose contents contain a `RoomController` (`LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`), **(b)** every scene in `Assets/Scenes/Production` (`EditorSceneManager.OpenScene` single, `SaveScene`; abort first if the user declines `SaveCurrentModifiedScenesIfUserWantsTo`):
  1. For each `RoomDoorInteractable` and `SceneDoorInteractable` in the room without `DoorArrival`: `AddComponent<DoorArrival>()`, then `PlaceAtDefault(Distance, hint)` with hint = position of the matched old spawn (below) if any, else `InteriorHint(room)`.
  2. Match: room door with destination S → the `SpawnPoint` in the room with `FromRoom == S` nearest to the door; scene door → the `SceneSpawnPoint` whose `StartingRoom == room` nearest to the door. If matched: copy its `camera` to `arrivalCamera` (via `SerializedObject`, field name `camera`), and for scene doors copy `entryPointId`.
  3. Delete every `SpawnPoint` / `SceneSpawnPoint` in the room: the GameObject when it has only `Transform` + the spawn and no children, otherwise just the component. Unmatched ones are logged with `Debug.LogError("[SpawnPointMigration] Unmatched spawn {room}/{name} from {fromRoom} at {position}")` before deletion.
  - Ends with one `Debug.Log` summary: prefabs/scenes touched, doors converted, cameras copied, spawns deleted, unmatched count.

- [ ] **Step 1: Implement** the command.
- [ ] **Step 2: Run it** via `execute_menu_item` `CrimsonDraft/Doors/Migrate Spawn Points To Door Arrivals`; read the console. Expected: no exceptions; unmatched = the 3 known spawns (`Kitchen/DR_33`, `Bathroom B/DR_20`, `Port_Stairs/SpawnPoint`) plus any in `Deck_C`; `SceneSpawnPoint` TR_01–TR_04 matched to scene doors.
- [ ] **Step 3: Verify data**: in each production scene `FindObjectsByType<SpawnPoint>` and `<SceneSpawnPoint>` return 0 (via `execute_code`); `git status` shows the room prefabs and scenes modified; validation (Task 5) reports no doors without `DoorArrival`.
- [ ] **Step 4: Play-test** in `Deck_B_Development`: cross `DR_08`↔`DR_08B` and one stair (TR_01 → Deck_C → back); player appears 1 m inside, back to the door, with the arrival camera.
- [ ] **Step 5: Commit** code + data: `chore(doors): migrate spawn points to door arrivals in all rooms and scenes`.

---

### Task 7: Delete the old spawn system

**Files:**
- Delete: `Rooms/SpawnPoint.cs`, `Rooms/SceneSpawnPoint.cs`, `Editor/SpawnPointAligner.cs`, `Editor/SpawnPointMigration.cs` and their `.meta` files.
- Modify: anything still referencing them (expected: none after Tasks 3–6; `RoomControllerTests.Activate_withMissingSpawnPointReference_doesNotThrow` is about enemy spawns and stays).

- [ ] **Step 1: Delete** the files; `grep -rn "SpawnPoint\b\|SceneSpawnPoint\|SpawnPointAligner" Game/CrimsonDraft/Assets/Scripts Game/CrimsonDraft/Assets/Tests` → only enemy-spawn names (`spawnPoint` field in `RoomController`).
- [ ] **Step 2: Verify**: compile clean; full EditMode suite passes; open each production scene and check the console shows no "missing script" warnings (`execute_code`: count `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` over all objects → 0).
- [ ] **Step 3: Commit** `refactor(rooms): remove SpawnPoint and SceneSpawnPoint`; then push / open the PR for the branch.
