# Sprite Map Rework — Design

**Date:** 2026-10-05
**Status:** Implemented on `feature/sprite-map`

## Goal

Replace the polygon/3D-camera map with hand-drawn sprites per room, authored in a new
asset-based editor, shown centred in the MAP tab with vertical input switching floors.

## Decisions (from the user)

- Each room has two sprites: **incomplete** and **complete** (all its pickups collected).
- The player's current room is highlighted by a **tint change**, not a blink.
- Discovery is unchanged: a visited room is drawn; with the floor's **map item**, unvisited
  rooms are drawn too — with their real complete/incomplete sprite. Otherwise invisible.
- A room with no pickups counts as **complete**, even if never visited.
- Sprites are loose pieces at a **shared, fixed pixels-per-unit**, only positioned, with
  rotation limited to **90° steps**.
- **No zoom, no pan.** The map is always centred; **vertical input switches floors**.
- Approach **A**: layout lives in the `MapData` asset, edited without the scene; rendered with uGUI.
- Doors (blue / red / grey, manually positioned) are **out of scope** — a later phase.
- Include a sprite PPU/filter validator inside the editor window.

## Constraints

- No scene edits. Prefab edits only, done through the Editor via the Unity CLI.
- Editor code lives in `Scripts/Navigation/Editor/` (existing editor assembly); runtime code
  never references `UnityEditor` outside `#if UNITY_EDITOR`.
- `#nullable enable`, `[Preserve]` constructors, VContainer, EditMode tests with plain fakes.

## 1. Data model

`MapData` (one asset per floor):

- Keeps: `sceneName`, `displayName`, `abbreviation`, `mapItemId`, `doors` (kept for the door phase).
- Removes: `gridSize`, `cellSize`, `center`.
- `MapRoomData`:

| Field | Type | Owner |
|---|---|---|
| `RoomId` | `string` | bake |
| `IncompleteSprite` | `Sprite?` | editor |
| `CompleteSprite` | `Sprite?` | editor |
| `Position` | `Vector2Int` (map pixels, arbitrary origin) | editor |
| `QuarterTurns` | `int` 0–3 | editor |
| `IsOrphan` | `bool` (room missing from the scene at the last bake) | bake |
| `PickupIds` | `string[]` | bake |
| `DoorIds` | `string[]` | bake |

- Removed from `MapRoomData`: `Polygon`, `Transform`.
- Floor order = order of `MapDataSet.Maps` (first = top floor; ▲ moves towards index 0).

## 2. Visibility rules

Pure function replacing `MapStateResolver.ResolveRoom`:

| Visited | Floor map item owned | Drawn | Sprite |
|---|---|---|---|
| yes | — | yes | complete / incomplete |
| no | yes | yes | complete / incomplete |
| no | no | no | — |

- Complete = every `PickupId` is collected; empty `PickupIds` ⇒ complete. Does not require a visit.
- A room whose resolved sprite is null is not drawn.
- `IsCurrent` only on the floor the player is on (`MapSceneConfig.Map`) and only for
  `IRoomOrchestrator.CurrentRoom.RoomId`.
- Available floors for the selector: `MapStateResolver.IsDeckKnown` (unchanged).

## 3. Bake (scene → asset)

`MapBaker` still runs on scene save and on entering Play Mode, but:

- No longer needs `MapRoomShape`. Collects every `RoomController` in the **same scene as the
  `MapSceneConfig`** (other open scenes are ignored), with `PickupInteractable` ids and `MapDoorMarker` door ids
  under each room (same parent lookup as today).
- **Upsert by `RoomId`** via pure `MapLayoutMerge.Upsert(existing, baked)`:
  - New `RoomId` → appended with empty layout.
  - Existing → only `PickupIds` / `DoorIds` replaced; sprites, position, rotation untouched.
  - Rooms missing from the scene are kept and flagged `IsOrphan = true` (cleared if they come back).
  - Duplicate `RoomId` entries (left by the old polygon bake) collapse into one, keeping the entry that has layout and the union of their ids.
  - Idempotent: baking twice yields an identical asset.
- `RoomController` with empty `RoomId` → `Debug.LogWarning` with the object as context; skipped.
- Writes only the `MapData` asset; never the scene. Doors keep baking into `MapData.doors` as today.

## 4. Editor window — `Tools/CrimsonDraft/Map Editor`

Replaces the current `MapEditorWindow`.

- **Toolbar:** `MapData` selector (every `MapData` asset in the project), preview toggle
  Incomplete / Complete, snap selector 1 / 4 / 8 px.
- **Room list (left):** each room with status ✔ placed · ⚠ missing sprite(s) · ✖ orphan.
  Selecting shows: both sprite fields, `Position` X/Y ints, ⟲ / ⟳ 90° buttons, **Delete** (orphans only).
- **Canvas:**
  - Draws each room sprite at `Position` with `QuarterTurns`; selected room outlined.
  - Click selects; drag moves with snap.
  - Mouse wheel zoom and middle-drag pan — editor only.
  - A room without a sprite is not drawn; assigning its first sprite places it at the visible canvas centre.
  - A cross marks the bounding-box centre (what the game centres on).
- **PPU validator:**
  - A sprite whose `pixelsPerUnit` or `filterMode` differs from the floor's majority is flagged ⚠ in the list.
  - A **Fix** button rewrites that sprite's `TextureImporter` and reimports it.
- **Editor standards:**
  - Window state persists across domain reloads: open map, selection, zoom, pan and snap as `[SerializeField]`; snap also in `EditorPrefs`.
  - All edits are bracketed by `BeginChangeCheck` / `EndChangeCheck` with `Undo.RecordObject(map)`.
  - `SetDirty` is called only when something changed.

Removed:
- `MapRoomShape` from room prefabs;
- `MapRoomShapeEditor`;
- the old `MapEditorWindow`;
- `PolygonTriangulator`.

Kept: `MapDoorMarker`, `MapSceneConfig`.

## 5. Runtime

- **`MapRoomVisuals.Resolve(map, rooms, pickups, knownMaps, currentRoomId)`** (pure): returns
  visible `{RoomId, Sprite, Position, QuarterTurns, IsCurrent}`.
- **`MapLayoutBounds`** (pure): bounding box of the visible visuals, using sprite pixel size and
  swapping width/height on odd `QuarterTurns`; returns the centre.
- **`MapFloors`** (pure):
  - lists the available floors in `MapDataSet` order, plus the player's own floor (appended last) when the set doesn't contain it;
  - the start index is the player's floor, or the first available if it is unknown;
  - `Up` / `Down` clamp at the ends and never wrap.
- **`MapScreenView`** (existing MonoBehaviour, reworked; replaces `MapRenderer` and the `RawImage`):
  - **Rooms:** a content `RectTransform`, with one pooled `Image` per visible room.
    - `sizeDelta = sprite.rect.size` (map unit = sprite pixel; PPU never changes on-screen size);
    - anchored position = `Position − boundsCentre`;
    - Z rotation = `QuarterTurns × 90`.
  - **Fit:** if the bounds exceed the viewport, the content is uniformly scaled down to fit. It is never scaled up past 1.
  - **Current-room tint:** `Color.Lerp(white, currentTint, (sin(t·pulseSpeed)+1)/2)`, using unscaled time.
  - **Header:** floor name with ▲ / ▼ shown only when a floor exists in that direction.
- **`MapTabController`**:
  - On enable, shows the player's floor.
  - The vertical axis of `InventoryNavigate` steps floors once per press: deadzone 0.5, re-arms below 0.2. It is ignored while the tab bar is active.
  - Pan, zoom and Confirm handling are removed.
- **DI:** `NavigationScope` no longer registers `MapRenderer`; `MapScreenView` stays a serialized reference of `MapTabController`.
- **`__GAMEPLAYCORE.prefab`:**
  - removed: the `MapRenderer` object with the map camera, its `CinemachineBrain`, the map vcam and the mesh content root. The room/door material assets stay in the project, now unreferenced;
  - `MapImage` loses its `RawImage`, gains a `RectMask2D` and a `Content` child; `UpArrow`/`DownArrow` labels are added next to `DeckNameText`.

## 6. Migration

`MapData_DeckB` / `MapData_DeckC` keep `RoomId`, `PickupIds`, `DoorIds`; polygon/transform data
is dropped by the field removal. Rooms stay invisible until sprites are assigned in the editor.

## 7. Tests (EditMode)

- **`MapRoomVisuals`:**
  - the visibility table;
  - empty pickups ⇒ complete;
  - one pending pickup ⇒ incomplete;
  - a null sprite is not drawn;
  - `IsCurrent` only on the player's floor.
- **`MapFloors`:**
  - order;
  - unknown floors skipped;
  - start index;
  - clamping at both ends, no wrap.
- **`MapLayoutMerge`:**
  - preserves layout;
  - updates ids;
  - appends new rooms;
  - reports orphans;
  - idempotent.
- **`MapLayoutBounds`:**
  - centre for several rooms;
  - odd `QuarterTurns` swap width and height.
- **`MapTabController` floor stepping:** one step per press and re-arm. Tested via the pure stepping helper if the MonoBehaviour is not practical.

## Out of scope

- Doors on the map: positioning, and the blue/red/grey states.
- Any change to discovery, `KnownMapsRegistry`, `RoomStateRegistry` or `PickupRegistry`.
- Producing the room sprites themselves.
