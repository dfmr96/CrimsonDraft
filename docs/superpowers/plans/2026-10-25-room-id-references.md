# Room ID References and Debug Room Teleport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Doors point at rooms (and at doors in other rooms) by game-wide room id instead of object references, so room prefabs are self-contained; then a debug-menu teleport to any room of the game.

**Architecture:** A `RoomIds` helper defines and normalizes `<Deck>/<ROOM>` ids. `RoomDoorInteractable` stores `destinationRoomId` and a `DoorRef` (room id + door id) for `unlocksOnCross`, resolved at runtime through `RoomLookup` over the loaded scene's rooms. A `RoomCatalog` asset (rebuilt from the production scenes by an editor button) feeds the inspector pickers and the teleport. A one-shot editor migration rewrites Deck_B / Deck_C prefabs, scenes, `MapData` and `RoomVisitMutation`; a follow-up task deletes the legacy fields. The debug menu gains a cycling "choice" entry used by the teleport.

**Tech Stack:** Unity 6, C# (`#nullable enable`), VContainer, MessagePipe, UniTask, NUnit EditMode tests run through UnityMCP `run_tests`.

**Spec:** `docs/superpowers/specs/2026-10-25-room-id-references-design.md`

## Global Constraints

- All files `#nullable enable`; serialized refs `= null!`; `[Preserve]` on DI constructors (CLAUDE.md).
- Room id format `<Deck>/<ROOM>`: deck `Deck_B` / `Deck_C` (scene name without `_Development`), room part upper case, spaces → `_`.
- Normalization table (verbatim from spec): `HALLWAY B1`→`Deck_B/HALLWAY_B1`, `HALLWAY B3`→`Deck_B/HALLWAY_B3`, `INTERROGATION ROOM`→`Deck_B/INTERROGATION_ROOM`, `STORAGE P`→`Deck_B/STORAGE_P`, `ART ROOM`→`Deck_C/ART_ROOM`, `GARBAGE ROOM`→`Deck_C/GARBAGE_ROOM`, `HALLWAY CONNECTION`→`Deck_C/HALLWAY_CONNECTION`, `STORAGE ROOM`→`Deck_C/STORAGE_ROOM`, `SAVEROOM_C`→`Deck_C/SAVE_ROOM`; every other id keeps its room part (`CHEFF_ROOM` typo kept) and gains the deck prefix.
- Production scenes: `Assets/Scenes/Production/Deck_B_Development.unity`, `Deck_C_Development.unity`. `Navigation.unity` is not migrated.
- Ids compared with `StringComparison.Ordinal` after migration.
- Debug-only code under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, like the existing debug menu.
- Tests: EditMode, plain fakes, Unity Test Runner via MCP `run_tests`; the known flaky `ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds` is rerun alone if it fails.
- Commits without `Co-Authored-By` (CLAUDE.md).

## Review Focus

- A door whose `destinationRoomId` is not in the loaded scene (typo, other deck): must log an error and not start a transition, not throw mid-transition — test in Task 2.
- Two rooms of the same deck sharing a legacy id after normalization (e.g. `HALLWAY B1` vs `HALLWAY_B1`): the migration must refuse rather than merge them — test in Task 3 (`Normalize` collision check).
- Teleport to the room the player is already in: must still re-place the player at the first arrival without errors — test in Task 6.
- Teleport target room without any door arrival: player goes to the room origin and an error is logged — test in Task 6.
- Debug choice entry with an empty option list (catalog not built): renders a placeholder and Enter does nothing — test in Task 5.

---

### Task 1: Room ids and the room catalog

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomIds.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomCatalog.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/RoomCatalogEditor.cs`
- Create asset: `Game/CrimsonDraft/Assets/Data/Navigation/RoomCatalog.asset`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomIdsTests.cs`, `RoomCatalogTests.cs`

**Interfaces:**
- Produces: `static class RoomIds` — `string Normalize(string deck, string legacyId)`, `bool IsWellFormed(string id)`, `string? DeckOf(string id)`, `string DeckOfScene(string sceneName)` (`"Deck_B_Development"` → `"Deck_B"`).
- Produces: `sealed class RoomCatalog : ScriptableObject` — `[Serializable] struct Entry { string RoomId; string SceneName; }`, `IReadOnlyList<Entry> Rooms` (sorted: deck order of first appearance, then room id ordinal), `bool TryGetScene(string roomId, out string sceneName)`, `bool Contains(string roomId)`, `void EditorSetRooms(IEnumerable<Entry> rooms)` (editor-only, sorts + dedups).
- Produces: inspector button "Rebuild From Scenes" on `RoomCatalog` that opens each production deck scene (restoring the open scenes afterwards, refusing if one is dirty) and collects every `RoomController.RoomId` with its scene.

- [ ] **Step 1: Write failing tests** `RoomIdsTests`: `Normalize("Deck_B","HALLWAY B1") == "Deck_B/HALLWAY_B1"`; `Normalize("Deck_C","SAVEROOM_C") == "Deck_C/SAVE_ROOM"`; `Normalize("Deck_B","CHEFF_ROOM") == "Deck_B/CHEFF_ROOM"`; `Normalize("Deck_B","Deck_B/KITCHEN") == "Deck_B/KITCHEN"` (idempotent); `IsWellFormed("Deck_B/KITCHEN")` true, false for `"KITCHEN"`, `"Deck_B/HALLWAY B1"`, `"Deck_B/kitchen"`, `""`; `DeckOf("Deck_C/OFFICE") == "Deck_C"`; `DeckOfScene("Deck_C_Development") == "Deck_C"`. `RoomCatalogTests`: `EditorSetRooms` with `Deck_C/B, Deck_B/Z, Deck_B/A, Deck_B/A` → `Rooms` ids exactly `Deck_C/B, Deck_B/A, Deck_B/Z` (decks in order of first appearance, ids ordinal within a deck, duplicate dropped); `TryGetScene("Deck_B/A")` gives its scene; unknown id → false.
- [ ] **Step 2: Run** `run_tests` filter `RoomIdsTests|RoomCatalogTests` — Expected: compile failure.
- [ ] **Step 3: Implement** `RoomIds` (the `SAVEROOM_C`→`SAVE_ROOM` special case is the only rename beyond upper-case + `_`), `RoomCatalog`, `RoomCatalogEditor`; create the asset; press "Rebuild From Scenes" via `execute_code` and check it lists 22 Deck_B + 13 Deck_C rooms (old ids at this point; rebuilt again in Task 3).
- [ ] **Step 4: Run** the same filter — Expected: all pass.
- [ ] **Step 5: Commit** `feat(rooms): room id helper and room catalog`.

---

### Task 2: Doors reference rooms by id at runtime

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomLookup.cs`, `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorRef.cs`
- Modify: `Navigation/Interactables/RoomDoorInteractable.cs`, `Navigation/Rooms/DoorArrivalLookup.cs`, `Navigation/Editor/MapBaker.cs:160`, `Navigation/Editor/NavigationHierarchyLabels.cs:47`, `Navigation/Editor/NavigationValidationToolbar.cs:60-96`, `Navigation/Editor/RoomGraphWindow.cs:87`
- Test: `Tests/EditMode/DoorArrivalLookupTests.cs`, `RoomDoorInteractableTests.cs`, `RoomLookupTests.cs`, and any test setting `"destination"` via `SerializedObject` (`OperatorCorpseBootstrapTests`, `RoomOrchestratorInitTests`, `SaveControllerTests` if they do)

**Interfaces:**
- Consumes: `RoomIds` (Task 1).
- Produces: `static class RoomLookup` — `RoomController? Find(IEnumerable<RoomController> rooms, string roomId)`, `RoomController? FindLoaded(string roomId)` (includes inactive rooms of loaded scenes).
- Produces: `[Serializable] struct DoorRef { string RoomId; string DoorId; bool IsSet; }` and `static RoomDoorInteractable? DoorRef.Resolve(DoorRef r, IEnumerable<RoomController> rooms)`.
- Produces on `RoomDoorInteractable`: `[SerializeField] string destinationRoomId`, `public string DestinationRoomId`, `[SerializeField] DoorRef unlocksOnCrossRef`. The legacy fields `destination` (`RoomController`) and `unlocksOnCross` stay serialized with `[HideInInspector]`, read only by the migration (`internal RoomController? LegacyDestination`, `internal RoomDoorInteractable? LegacyUnlocksOnCross`), removed in Task 4. `Destination` (object) is removed from the public API.
- `DoorArrivalLookup.FindArrivalDoor(destination, origin, departurePosition)` keeps its signature; it now matches `door.DestinationRoomId == origin.RoomId` (Ordinal).

- [ ] **Step 1: Update/write failing tests**: `DoorArrivalLookupTests` set `destinationRoomId` strings (rooms get ids `Deck_T/ORIGIN`, `Deck_T/DESTINATION`, `Deck_T/OTHER`) — existing expectations unchanged. New `RoomLookupTests`: `Find_returnsRoomWithId`, `Find_unknownId_returnsNull`, `Find_emptyId_returnsNull`. New `RoomDoorInteractableTests.Cross_withUnknownDestination_logsErrorAndDoesNotTransition` (`LogAssert.Expect(LogType.Error, new Regex("Deck_T/NOWHERE"))`, fake orchestrator `TransitionCallCount == 0`); `Cross_unlocksDoorReferencedByDoorRef` (door in another room with matching `RoomId`+`DoorId` gets `UnlockFromOtherSide`).
- [ ] **Step 2: Run** filter `DoorArrivalLookupTests|RoomLookupTests|RoomDoorInteractableTests` — Expected: failures/compile errors.
- [ ] **Step 3: Implement**: `CrossDoor` resolves `RoomLookup.FindLoaded(destinationRoomId)` (error + return when null) and `DoorRef.Resolve` for the unlock; editor call sites read `DestinationRoomId` and resolve rooms with `RoomLookup.Find(sceneRooms, id)`; hierarchy label shows the id string.
- [ ] **Step 4: Run** the full EditMode suite — Expected: all pass.
- [ ] **Step 5: Commit** `refactor(doors): door destinations and unlocks by room id`.

---

### Task 3: Migrate production data to the new ids

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/RoomIdMigration.cs` (deleted in Task 4)
- Modify: `Navigation/Interactables/RoomVisitMutation.cs:18` (default `"Deck_C/ENERGY_ROOM"`)
- Data: room prefabs under `Prefabs/World/Rooms/Deck_B|Deck_C` and `Prefabs/World/ProtoWalls/PrefabRooms/Port_Stairs.prefab`, the two deck scenes, `Data/Map/MapData_DeckB.asset`, `MapData_DeckC.asset`, `Data/Navigation/RoomCatalog.asset`
- Test: `Tests/EditMode/RoomIdsTests.cs` (collision helper)

**Interfaces:**
- Consumes: `RoomIds`, `RoomCatalog` (Task 1); legacy accessors and `DoorRef` (Task 2).
- Produces: menu `CrimsonDraft/Rooms/Migrate Room Ids` (+ `(Dry Run)`), and `static IReadOnlyList<string> RoomIds.FindCollisions(IEnumerable<(string deck, string legacyId)> rooms)` returning the new ids produced by more than one room.

Algorithm (per production deck scene, refusing to run if an open scene or prefab stage is dirty, restoring open scenes at the end, like `SpawnPointMigration` did):
1. Map every `RoomController` → new id (`RoomIds.Normalize(DeckOfScene(scene), RoomId)`); abort the whole run if `FindCollisions` is non-empty.
2. Room prefab asset of each room (`PrefabUtility.GetCorrespondingObjectFromSource` root): set `roomId` to the new id; for each door in it, set `destinationRoomId` from the scene instance's `LegacyDestination` (new id of that room) and `unlocksOnCrossRef` from `LegacyUnlocksOnCross` (its room's new id + its `DoorId`); clear both legacy fields. Match prefab door ↔ scene door by transform path under the room root.
3. In the scene: revert the instance overrides of `roomId`, `destination`, `unlocksOnCross`, `destinationRoomId`, `unlocksOnCrossRef` so the prefab values show (`PrefabUtility.RevertPropertyOverride`), then `RemoveUnusedOverrides`, save.
4. `MapData` assets: rewrite `RoomId` on room and door entries through the old→new map of that deck; `RoomVisitMutation.triggerRoomId` in `Kitchen_Fridge.prefab` → `Deck_C/ENERGY_ROOM`.
5. Rebuild the `RoomCatalog`. One summary log: rooms renamed, doors rewritten, unlocks rewritten, map entries rewritten, unresolved legacy references (logged one by one as errors).

- [ ] **Step 1: Write failing test** `FindCollisions_reportsTwoLegacyIdsNormalizingToOne`: input `("Deck_B","HALLWAY B1"),("Deck_B","HALLWAY_B1"),("Deck_C","HALLWAY_B1")` → `["Deck_B/HALLWAY_B1"]`.
- [ ] **Step 2: Run** filter `RoomIdsTests` — Expected: compile failure.
- [ ] **Step 3: Implement** `FindCollisions` and the migration command.
- [ ] **Step 4: Run** the full suite — Expected: pass. Then the dry run via `execute_code`, read the console: no collisions, 0 unresolved except doors that already had no destination (Deck_B 1, Deck_C 3 known).
- [ ] **Step 5: Run the migration**; verify with `execute_code` in each deck scene: every `RoomController.RoomId` passes `IsWellFormed` and the 35 ids are unique; every door's `DestinationRoomId` (where the legacy one was set) resolves with `RoomLookup.Find`; opening `Kitchen.prefab` with `LoadPrefabContents` shows non-empty `destinationRoomId` on its doors; `MapData` room ids all `IsWellFormed`; catalog has 35 entries. `git status`: Boot/Combat/Navigation scenes untouched (revert them if the migration touched them).
- [ ] **Step 6: Play-check** in `Deck_B_Development` via `manage_editor` play: cross `DR_08`↔`DR_08B`; arrival placement unchanged. (If not possible headless, record it as pending for the user.)
- [ ] **Step 7: Commit** code + data `chore(rooms): migrate room ids, door destinations and map data`.

---

### Task 4: Remove legacy references; pickers and validation

**Files:**
- Delete: `Navigation/Editor/RoomIdMigration.cs` (+ `.meta`)
- Modify: `RoomDoorInteractable.cs` (remove legacy fields/accessors)
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomIdAttribute.cs`, `Navigation/Editor/RoomIdDrawer.cs`, `Navigation/Editor/DoorRefDrawer.cs`
- Modify: `Navigation/Editor/NavigationValidationToolbar.cs`
- Test: `Tests/EditMode/RoomIdValidationTests.cs`

**Interfaces:**
- Consumes: `RoomCatalog`, `RoomIds`, `DoorRef` (Tasks 1–2).
- Produces: `[RoomId]` attribute on `destinationRoomId` and `RoomVisitMutation.triggerRoomId`; `RoomIdDrawer` = popup "None" + catalog ids grouped by deck (`Deck_B/…` submenus), warning box when the stored id is not in the catalog; `DoorRefDrawer` = room popup then door popup listing the `DoorId`s of that room's prefab (from the catalog scene's room, or the prefab when the room's prefab is found by `RoomController` root).
- Produces: `static class RoomIdValidation` (runtime-testable, in Navigation.Rooms or Editor) — `IEnumerable<string> Check(IReadOnlyList<RoomController> rooms, IReadOnlyList<RoomDoorInteractable> doors, RoomCatalog catalog)` returning messages for: malformed id, duplicate id, door destination not in catalog, `DoorRef` to unknown room/door, catalog missing a room present in the scene.

- [ ] **Step 1: Write failing tests** `RoomIdValidationTests`: one test per message kind, each asserting exactly one message containing the offending id; clean data → no messages.
- [ ] **Step 2: Run** filter `RoomIdValidationTests` — Expected: compile failure.
- [ ] **Step 3: Implement**; the toolbar logs each message as a warning and counts it; `MapData` room ids not in the catalog are also reported by the toolbar.
- [ ] **Step 4: Run** the full suite — Expected: pass. Compile clean; `Tools/Validate Navigation References` in both decks reports only the known no-destination doors.
- [ ] **Step 5: Verify picker** (manual via `execute_code` is enough: open `Kitchen.prefab` in Prefab Mode with `PrefabStageUtility`/`AssetDatabase.OpenAsset`, confirm the door's `destinationRoomId` is set on the prefab asset — no scene needed).
- [ ] **Step 6: Commit** `refactor(rooms): drop room object references, add room id pickers and validation`; update the spec status line.

---

### Task 5: Cycling choice entry in the debug menu

**Files:**
- Modify: `Infrastructure/DebugMenu/DebugMenuEntry.cs`, `DebugMenuSelection.cs`, `DebugMenuController.cs`
- Test: `Tests/EditMode/DebugMenuTests.cs`

**Interfaces:**
- Produces: `static DebugMenuEntry DebugMenuEntry.Choice(string group, string label, Func<IReadOnlyList<string>> options, Action<string> activate)`; `bool IsChoice`; `string? CurrentOption`; `void Cycle(int delta)` (wraps; keeps the chosen option by value when the list changes, else index 0); `Activate()` invokes `activate(CurrentOption)` and does nothing when there is no option.
- `DebugMenuSelection.CycleCurrent(int delta)`; controller maps Left/Right arrow (and gamepad dpad left/right if the controller already reads a gamepad; it reads `Keyboard.current` today — keyboard only is fine) to `CycleCurrent`.
- `DebugMenuController.Format` renders a choice as `"{label}: < {option} >"`, and `"{label}: (none)"` with no options.

- [ ] **Step 1: Write failing tests**: `Choice_cyclesAndWraps`, `Choice_activateRunsWithCurrentOption`, `Choice_emptyOptions_rendersNoneAndActivateDoesNothing`, `Format_rendersChoiceWithArrows`.
- [ ] **Step 2: Run** filter `DebugMenuTests` — Expected: compile failure.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** filter `DebugMenuTests` then full suite — Expected: pass.
- [ ] **Step 5: Commit** `feat(debug-menu): cycling choice entries`.

---

### Task 6: Debug room teleport

**Files:**
- Modify: `Navigation/Rooms/IRoomOrchestrator.cs`, `RoomOrchestrator.cs`, `SceneEntryContext.cs`, `Infrastructure/Scenes/IFloorTransitionService.cs`, `Navigation/Rooms/FloorTransitionService.cs`, `Navigation/Rooms/DoorArrivalLookup.cs`, `Navigation/NavigationScope.cs` (serialize + `RegisterInstance` the `RoomCatalog`), `Navigation/NavigationDebugKeys.cs`
- Test fakes: every `IRoomOrchestrator` / `IFloorTransitionService` fake in `Tests/EditMode` (RoomDoorInteractableTests, OperatorCorpseBootstrapTests, SaveControllerTests, SaveGameLoaderTests, MapStateTrackerTests, SceneDoorInteractableTests)
- Test: `RoomOrchestratorInitTests.cs`, `DoorArrivalLookupTests.cs`, `NavigationDebugKeysTests.cs`

**Interfaces:**
- Consumes: `RoomCatalog` (Task 1), `RoomLookup` (Task 2), `DebugMenuEntry.Choice` (Task 5).
- Produces: `DoorArrival? DoorArrivalLookup.FindFirstArrival(RoomController room)` — first `DoorArrival` with a non-null `ArrivalPoint` in hierarchy order (`GetComponentsInChildren(true)`).
- Produces: `void IRoomOrchestrator.TeleportToRoom(RoomController room)` — ignored with a warning while transitioning; deactivates the other rooms, activates `room`, places the player at `FindFirstArrival(room)` (or the room transform + `Debug.LogError` naming the room when none), activates that arrival's camera else `EnsureLiveCamera`, sets `CurrentRoom`, publishes `RoomTransitionedEvent`.
- Produces: `SceneEntryContext.SetPendingRoom(string roomId)` / `string? ConsumeRoom()`; `RoomOrchestrator` start-up: a pending room wins over a pending entry point and goes through the same placement as `TeleportToRoom` (no event publish needed beyond what start-up already does).
- Produces: `UniTask IFloorTransitionService.TeleportToFloorAsync(string fromScene, string toScene, string roomId)` — same load/unload/active-scene steps as `TransitionToFloorAsync` but no `DoorTransition` scene; sets the pending room.
- Produces: debug entry `DebugMenuEntry.Choice("Navigation", "Teleport", () => catalog ids, Teleport)` in `NavigationDebugKeys`; `Teleport(id)`: same scene (`RoomCatalog.TryGetScene` equals the active scene) → `TeleportToRoom(RoomLookup.FindLoaded(id))`; other scene → `TeleportToFloorAsync(active, scene, id)`; ignored with a log during combat or a transition. `internal static bool IsSameScene(string activeScene, string targetScene)`.

- [ ] **Step 1: Write failing tests**: `FindFirstArrival_skipsDoorsWithoutArrivalPoint_inHierarchyOrder`; `TeleportToRoom_activatesRoom_placesPlayerAtFirstArrival_andPublishes` (publisher fake count 1, other room inactive); `TeleportToRoom_currentRoom_replacesPlayerWithoutErrors`; `TeleportToRoom_roomWithoutArrival_usesRoomOrigin_andLogsError`; `Initialize_withPendingRoom_startsInThatRoomAtFirstArrival`; `IsSameScene` true/false.
- [ ] **Step 2: Run** filters `RoomOrchestratorInitTests|DoorArrivalLookupTests|NavigationDebugKeysTests` — Expected: compile failure.
- [ ] **Step 3: Implement**, update the fakes (no-op members), assign the catalog on the `NavigationScope` of both deck scenes (`execute_code`, save scenes).
- [ ] **Step 4: Run** the full suite — Expected: pass.
- [ ] **Step 5: Play-check** in `Deck_B_Development`: F1 → Teleport, cycle to `Deck_B/KITCHEN`, Enter → player in Kitchen at its first arrival with a live camera; then `Deck_C/OFFICE` → Deck_C loads, player in Office. Record as pending for the user if play mode can't be driven.
- [ ] **Step 6: Commit** `feat(debug): teleport to any room from the debug menu`; update the spec status line.
