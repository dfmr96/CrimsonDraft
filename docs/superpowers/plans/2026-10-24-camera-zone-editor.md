# Camera Zone Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One activation polygon per camera, entered by the player point, authored in the Camera Zone Viewer with a new plan (top-down) view and per-camera status / fix-up buttons.

**Architecture:** `FixedCameraZoneTrigger` keeps holding the polygon and goes back to point-based entry (revert of `b6c442f8`'s body overlap, keeping its inspection guard). A pure `CameraActivation` resolver says, per camera, whether it has none / one / legacy triggers. The viewer is split so the render-to-texture viewport and the zone overlay + handles are reusable; the plan view is a second, orthographic top-down camera using the same overlay and handles.

**Tech Stack:** Unity 6000.3, C# (`#nullable enable`), Cinemachine 3, URP, NUnit EditMode tests via MCP `run_tests`.

**Spec:** `docs/superpowers/specs/2026-10-24-camera-zone-editor-design.md`

## Global Constraints

- Detection uses the player reference point (`player.transform.position`); no collider/body overlap.
- Keep `FixedCameraZoneBootstrap.Tick`'s guard: no zone evaluation while `CurrentZoneCamera != GameplayZoneCamera`.
- Two-sided switch stays functional at runtime (legacy); the tool never creates it.
- Nothing is written to scenes/prefabs except by explicit tool actions, all undoable (`Undo.*`).
- `#nullable enable`; repo `CLAUDE.md` conventions; no `Co-Authored-By` trailers.
- Paths below are relative to `Game/CrimsonDraft/Assets/` unless they start with `docs/`.

## Review Focus

- Room destroyed / scene reloaded while the viewer is open: no `MissingReferenceException` (it currently breaks `MilestoneGraphEditorTests.Undo_*`). → Task 2.
- Camera referenced by a two-sided zone **and** a normal zone: status is *legacy*, and **Keep this one** on the two-sided one turns it single-sided for that camera only (the other camera loses it). → test in Task 3.
- Camera with no floor under its look direction (looking at the sky / over a void): **Create Trigger** still places a polygon (fallback under the camera). → test in Task 6.
- Plan view on a room with no renderers/colliders (empty `Bounds`): framing falls back to a default size, no NaN ortho size. → test in Task 5.
- Two adjacent single-sided zones A | B: A → B → A cuts back to A (the `b6c442f8` regression). → test in Task 1.

---

### Task 1: Point-based entry again, keep the inspection guard

**Files:**
- Modify: `Scripts/Navigation/CamaraSystem/FixedCameraZoneTrigger.cs` (remove `OverlapsBody`, `Evaluate(Vector3 playerPosition)` back to one parameter)
- Modify: `Scripts/Navigation/CamaraSystem/FixedCameraZoneBootstrap.cs` (`Tick` stops reading the collider; keeps the inspection guard)
- Test: `Tests/EditMode/FixedCameraZoneTriggerTests.cs`, create `Tests/EditMode/FixedCameraZoneBootstrapTests.cs`

**Interfaces:**
- Produces: `public void Evaluate(Vector3 playerPosition)` (unchanged from before `b6c442f8`).

- [ ] **Step 1: Write the failing tests** in a new `Tests/EditMode/FixedCameraZoneBootstrapTests.cs`, driving the real path: a `FixedCameraZoneBootstrap(triggers, new FixedCameraZoneService(), player, new PuzzleStateRegistry())` built with a `PlayerController` GameObject carrying a `CapsuleCollider` (radius 0.5, height 1.64), moved to each position and `((ITickable)bootstrap).Tick()` called (zones are rectangles x∈[-4,0] and x∈[0,4], z∈[-2,2], heights [-1,3], player y = 1):
  - `AdjacentZones_walkingBackCutsBackToTheFirstCamera`: x = -2, -0.4, 0.3, -0.3, -3 → current camera A, A, B, A, A.
  - `ZoneNextToTwoSidedZone_doesNotStickToTheTwoSidedCamera`: two-sided zone x∈[-4,0] (divider along X at z=0, player z=-1, cameras Z1/Z2) next to a normal zone x∈[0,4] (camera Y); x = -2, -0.2, 0.3, 0.7, 3 → Z1, Z1, Y, Y, Y.
  - `InspectionShot_isNotReplacedByZones`: after `ActivateInspectionShot(inspect)` with the player inside a two-sided zone, `Tick()` leaves `CurrentZoneCamera == inspect`.
  - In `FixedCameraZoneTriggerTests` delete `BodyOverlap_keepsEvaluatingPastThePolygonEdgeUntilTheDividerThreshold` and `BodyOverlap_acceptsTheBodyAboveTheFeetButRejectsOtherFloors`; keep `Evaluate_switchRequiresTheDestinationThresholdToBeInsideTheZone` on the point overload.
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.FixedCameraZoneBootstrapTests` — Expected: the two adjacency tests FAIL (camera stays B / Z1, as reproduced against `b6c442f8`); `InspectionShot_isNotReplacedByZones` passes (guard already present).
- [ ] **Step 3: Implement**: remove `OverlapsBody` and the `Bounds?` parameter; `Tick` calls `trigger.Evaluate(position)`; keep the guard line and its comment.
- [ ] **Step 4: Run** the same filter, then the full EditMode suite with the viewer closed — Expected: all pass.
- [ ] **Step 5: Commit** `fix(camera): detect camera zones by the player point again, keep the inspection guard`.

---

### Task 2: Viewer ignores destroyed rooms and cameras

**Files:**
- Modify: `Scripts/Navigation/Editor/CameraZoneViewerWindow.cs` (`RefreshRooms`, `DrawToolbar`, `CurrentRoom`, `CurrentCamera`)

- [ ] **Step 1: Implement**: drop destroyed entries (`r != null` Unity null check) from `rooms` / `cameras` whenever they are read in `OnGUI` (refresh when any is null); `CurrentRoom` / `CurrentCamera` return null for destroyed objects.
- [ ] **Step 2: Verify**: with the Camera Zone Viewer open, run filter `CrimsonDraft.Tests.MilestoneGraphEditorTests` — Expected: 5/5 pass (today 3 fail with `MissingReferenceException` at `CameraZoneViewerWindow.cs:176`).
- [ ] **Step 3: Commit** `fix(editor): camera zone viewer ignores rooms destroyed under it`.

---

### Task 3: Camera activation status

**Files:**
- Create: `Scripts/Navigation/CamaraSystem/CameraActivation.cs`
- Test: `Tests/EditMode/CameraActivationTests.cs`

**Interfaces:**
- Produces (namespace `CrimsonDraft.Navigation.CamaraSystem`):
  - `public enum CameraActivationState { None, Ok, Legacy }`
  - `public readonly struct CameraActivationStatus { public CameraActivationState State; public IReadOnlyList<FixedCameraZoneTrigger> Triggers; }`
  - `public static class CameraActivation { public static CameraActivationStatus For(CinemachineCamera camera, IEnumerable<FixedCameraZoneTrigger> roomTriggers); }` — `Triggers` = every trigger whose `ReferencedCameras` contains the camera; `Ok` = exactly one and it is not two-sided; `None` = zero; otherwise `Legacy`.

- [ ] **Step 1: Write the failing tests**: `For_noTrigger_isNone`, `For_oneSingleSidedTrigger_isOk`, `For_twoTriggers_isLegacyListingBoth`, `For_onlyATwoSidedTrigger_isLegacy`, `For_twoSidedAndNormalTrigger_isLegacyListingBoth` (also for the positive-side camera of the two-sided one: `Legacy` with one trigger).
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.CameraActivationTests` — Expected: compile failure.
- [ ] **Step 3: Implement** `CameraActivation.For`.
- [ ] **Step 4: Run** — Expected: 5 passed.
- [ ] **Step 5: Commit** `feat(camera): resolve each camera's activation trigger status`.

---

### Task 4: Split the viewer into reusable viewport and overlay

**Files:**
- Create: `Scripts/Navigation/Editor/CameraZones/ZoneViewport.cs` — hidden `Camera` + `RenderTexture` + URP data + unlit render (`RenderView`, `CopyLens`, `ReleaseTexture`, `DestroyViewCamera`, `FitToGameAspect` move here).
- Create: `Scripts/Navigation/Editor/CameraZones/ZoneOverlay.cs` — flat zone drawing, player marker, labels and zone handles (`DrawTriggerOverlay`, `OverlayProjection`, `EditHandles` and helpers move here), parameterised by `(RoomController room, Camera view, Rect rect, float planeY, CinemachineCamera? highlight)`.
- Modify: `Scripts/Navigation/Editor/CameraZoneViewerWindow.cs` — keeps toolbar, lists, camera-move mode; uses one `ZoneViewport` + `ZoneOverlay`.

**Interfaces:**
- Produces:
  - `sealed class ZoneViewport : IDisposable { Camera Camera { get; } Texture? Texture { get; } void Render(Vector2Int pixels, Action<Camera> setup, bool unlit); static Rect FitToAspect(Rect area, float aspect); }`
  - `sealed class ZoneOverlay { void Draw(RoomController room, Camera view, Rect rect, float planeY, CinemachineCamera? highlight, Vector3? player); void Edit(RoomController room, Camera view, Rect rect, float planeY, int controlId); }`

- [ ] **Step 1: Move** the code, no behaviour change.
- [ ] **Step 2: Verify**: compile clean; full EditMode suite passes; manually, in `Deck_B_Development` the viewer looks and edits exactly as before (zones, marker, handles, Move Camera, Reset Arrivals, Migrate Doors visibility).
- [ ] **Step 3: Commit** `refactor(editor): split camera zone viewer into viewport and overlay`.

---

### Task 5: Plan view

**Files:**
- Create: `Scripts/Navigation/Editor/CameraZones/PlanViewFraming.cs`
- Modify: `Scripts/Navigation/Editor/CameraZoneViewerWindow.cs` (view mode toolbar `Camera | Plan | Split`, plan input)
- Test: `Tests/EditMode/PlanViewFramingTests.cs`

**Interfaces:**
- Consumes: Task 4 `ZoneViewport`, `ZoneOverlay`.
- Produces: `public static class PlanViewFraming { public const float DefaultHalfSize = 5f; public static (Vector3 position, Quaternion rotation, float orthographicSize) Fit(Bounds room, float aspect, float zoom, Vector2 pan); }` — camera straight down (`rotation = Euler(90,0,0)`), at `room.max.y + 10` over `room.center + (pan.x, 0, pan.y)`; `orthographicSize = max(extents.z, extents.x / aspect) * 1.1 / zoom`; empty or zero bounds → `DefaultHalfSize / zoom`.

- [ ] **Step 1: Write the failing tests**: `Fit_wideRoom_sizesOnWidthOverAspect` (bounds size (20,3,6), aspect 2 → size 5.5), `Fit_tallRoom_sizesOnDepth` (size (4,3,10), aspect 1 → 5.5), `Fit_zoomAndPan_scaleAndOffset` (zoom 2 → half size; pan (1,2) → position x+1, z+2), `Fit_emptyBounds_usesDefaultSize` (size zero → 5), `Fit_looksStraightDown` (rotation * forward == down).
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.PlanViewFramingTests` — Expected: compile failure.
- [ ] **Step 3: Implement** `PlanViewFraming.Fit`; in the window add a second `ZoneViewport` (orthographic, framed with `Fit` over `DoorArrivalPlacement`-style room bounds: room `Bounds` child, else renderers), drawn with the same `ZoneOverlay` (`planeY` as in the camera view, highlight = selected camera) plus the selected camera's position and frustum projected on the floor (four corner rays of its lens intersected with `planeY`, drawn as lines); plan input: MMB drag pans, wheel zooms (0.25–8), LMB edits handles through `ZoneOverlay.Edit`.
- [ ] **Step 4: Run** the filter — Expected: 5 passed; manual: Plan and Split modes show Cheff_Room from above with all zones, the frustum of the selected camera, and vertex drags update both views.
- [ ] **Step 5: Commit** `feat(editor): plan view for the camera zone viewer`.

---

### Task 6: Per-camera status and fix-up actions

**Files:**
- Create: `Scripts/Navigation/Editor/CameraZones/CameraActivationEditing.cs`
- Modify: `Scripts/Navigation/Editor/CameraZoneViewerWindow.cs` (camera list status dot + panel under the list)
- Test: `Tests/EditMode/CameraActivationEditingTests.cs`

**Interfaces:**
- Consumes: Task 3 `CameraActivation.For`.
- Produces: `public static class CameraActivationEditing`
  - `public static FixedCameraZoneTrigger CreateTrigger(CinemachineCamera camera, RoomController room)` — new GameObject `FixedCameraZoneTrigger_<camera name>` under the room's `Cameras` child (created if missing), single-sided, `zoneCamera = camera`, 2×2 m square centred on the floor point under the camera's look ray (`Physics.Raycast` along `camera.transform.forward`, 50 m, ignoring triggers; miss → point under the camera at the room `Bounds` floor, else `camera.y - 2`), `minHeight -0.5`, `maxHeight 3`; registered with `Undo.RegisterCreatedObjectUndo`.
  - `public static void KeepOnly(FixedCameraZoneTrigger keep, CinemachineCamera camera)` — sets `keep.zoneCamera = camera`, `useTwoSidedSwitch = false`, `positiveSideCamera = null` (undoable).
  - `public static void Delete(FixedCameraZoneTrigger trigger)` — `Undo.DestroyObjectImmediate(trigger.gameObject)`.
- Window: list rows show a status dot (green Ok / grey None / orange Legacy); the panel lists the selected camera's triggers with **Keep this one** and **Delete**, or **Create Trigger** when None.

- [ ] **Step 1: Write the failing tests**: `CreateTrigger_placesSquareUnderLookPoint` (camera at (0,5,0) looking down at a plane collider at y=0 → zone at (0,0,0), polygon ±1, `ZoneCamera == camera`, not two-sided), `CreateTrigger_noFloorHit_fallsBackUnderCamera` (no collider → zone at (0, cameraY-2, 0)), `KeepOnly_turnsTwoSidedIntoSingleForThatCamera` (two-sided zone with cameras A/B, `KeepOnly(zone, B)` → `ZoneCamera == B`, not two-sided, `CameraActivation.For(A, ...)` == None).
- [ ] **Step 2: Run** filter `CrimsonDraft.Tests.CameraActivationEditingTests` — Expected: compile failure.
- [ ] **Step 3: Implement** the three operations and the window UI.
- [ ] **Step 4: Run** the filter — Expected: 3 passed; manual: in `Deck_B_Development`, a camera with 2 zones shows orange, **Keep this one** + **Delete** leaves it green; Ctrl+Z restores.
- [ ] **Step 5: Commit** `feat(editor): per-camera activation status and fix-up actions`.

---

### Task 7: Validation of camera activation

**Files:**
- Modify: `Scripts/Navigation/Editor/NavigationValidationToolbar.cs`

**Interfaces:**
- Consumes: Task 3 `CameraActivation.For`.

- [ ] **Step 1: Implement**: per room, for each `CinemachineCamera` referenced by any trigger or child of the room's `Cameras`: one warning line per room summarising `None` / `Legacy` counts with the first camera named (ping target), plus the total of two-sided zones left. No warning for `Ok`.
- [ ] **Step 2: Verify**: compile clean; `Tools/Validate Navigation References` in `Deck_B_Development` reports Legacy cameras (expected: dozens before hand conversion); full EditMode suite passes.
- [ ] **Step 3: Commit** `feat(editor): validate one activation trigger per camera`.
