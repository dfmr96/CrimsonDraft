# Room ID references and debug room teleport — design

Date: 2026-10-25 · Branch: `feat/camera-zone-editor`

## Why

Room-to-room doors point at their destination with a direct `RoomController` reference
(`RoomDoorInteractable.destination`). Every room is its own prefab, so that reference can
only exist as a scene override: opening a room prefab on its own shows the destination
empty, and it cannot be seen or edited there. This has been a standing annoyance when
editing room prefabs.

It now also blocks a debug feature: a teleport that can list every room of the game and
take the player to any of them, including rooms on another deck. Nothing outside the loaded
scene can know where a door leads or which rooms exist.

The fix: every room has a game-wide unique string id, and everything that points at a room
(or at a door in another room) stores that id instead of an object reference. Room prefabs
become self-contained.

## Goals

1. `RoomController.roomId` is unique across the whole game and includes the deck:
   `Deck_B/BALCONY_A`, `Deck_C/SAVE_ROOM`.
2. Room doors store their destination as a room id. The inspector offers a picker with
   every room of the project, so ids are never typed by hand. It works inside the room
   prefab (Prefab Mode), not only in the scene.
3. `RoomDoorInteractable.unlocksOnCross` (always a door in another room today: 4 uses, all
   cross-room) stores a room id + door id pair instead of a component reference.
4. Everything that reads the old references uses the ids: crossing a door, the arrival door
   lookup (`DoorArrivalLookup`), navigation validation, the room graph window, hierarchy
   labels, the map baker.
5. A one-shot migration converts Deck_B and Deck_C: new ids on every room, door
   destinations and `unlocksOnCross` written as ids, and every stored copy of an old room id
   updated (see "Where room ids live").
6. Debug teleport (built after the refactor): one F1 debug-menu entry that cycles through
   every room of the game and teleports the player there.

## Non-goals

- `Navigation.unity` is not production: it is not migrated and may be left broken.
- Old save files: state saved under the old room ids (visited rooms, corpses, map) is lost.
  No save migration.
- Duplicate `doorId`s are not fixed here (found: `DR_17` twice in Deck_C — Energy_Room and
  Water_Room; `DR_188` and `DR_188B` once on each deck). Note: `DoorStateRegistry` keys door
  state by `doorId`, so the two Deck_C `DR_17` doors share lock state today. Reported to the
  user as a separate issue.
- `NavigationScope.startingRoom` stays an object reference: it lives in the scene and points
  at a room in the same scene, so it has no prefab problem.
- Scene doors (`SceneDoorInteractable`) already work by strings (target scene + entry id)
  and are unchanged.

## Anti-goals

- Hand-typed ids anywhere in normal editing.
- A room prefab that still shows a missing/empty destination when opened alone.
- The teleport changing room state in a way the real game cannot (it must behave like a real
  entry).

## Room id format

`<Deck>/<ROOM>`: deck is `Deck_B` / `Deck_C` (the production scene name without
`_Development`); the room part is upper case with spaces replaced by `_`.

Normalization of the current ids (user approved "normalize the ids"):

| Deck | Current | New |
|---|---|---|
| B | `HALLWAY B1` | `Deck_B/HALLWAY_B1` |
| B | `HALLWAY B3` | `Deck_B/HALLWAY_B3` |
| B | `INTERROGATION ROOM` | `Deck_B/INTERROGATION_ROOM` |
| B | `STORAGE P` | `Deck_B/STORAGE_P` |
| C | `ART ROOM` | `Deck_C/ART_ROOM` |
| C | `GARBAGE ROOM` | `Deck_C/GARBAGE_ROOM` |
| C | `HALLWAY CONNECTION` | `Deck_C/HALLWAY_CONNECTION` |
| C | `STORAGE ROOM` | `Deck_C/STORAGE_ROOM` |
| C | `SAVEROOM_C` | `Deck_C/SAVE_ROOM` |

Every other id keeps its room part and gains the deck prefix (`CHEFF_ROOM` →
`Deck_B/CHEFF_ROOM`; the typo is kept). Current inventory: 22 rooms in Deck_B, 13 in Deck_C;
`BALCONY_A`, `BALCONY_B` and `HALLWAY_A` exist on both decks, which the prefix resolves.

The id is authored on the room prefab. Some current ids are scene overrides (e.g.
`SAVEROOM_C` in `Deck_C_Development.unity`); after migration the id lives in the prefab and
the scene carries no override for it.

## Where room ids live (all must be migrated)

- `RoomController.roomId` (room prefabs; some scene overrides).
- `MapData` assets (`Data/Map/MapData_DeckB.asset`, `MapData_DeckC.asset`): `RoomId` on
  room entries and on door entries.
- `RoomVisitMutation.triggerRoomId` — `Kitchen_Fridge.prefab` (Deck_B) uses `ENERGY_ROOM`
  (a Deck_C room) → `Deck_C/ENERGY_ROOM`. Its code default `"ENERGY_ROOM"` changes too.
- Runtime/save-only stores (`RoomStateRegistry`, `OperatorCorpseRegistry`, room state in
  saves) just start receiving the new ids; old saves are dropped.
- `DecayVolume.roomId` is an unrelated int (rendering layer) — not a room id, untouched.

The migration must finish with no old-format id left anywhere above; validation (below)
reports any that remain.

## Runtime resolution

- A door's destination id is resolved to the `RoomController` with that id among the rooms
  of the loaded scene. A destination id that is not in the loaded scene is a data error:
  logged, and the door does not transition (same treatment as today's null destination).
- Arrival lookup: "the door of the destination that leads back to the origin" compares
  destination ids with the origin room's id instead of references.
- `unlocksOnCross`: resolved by room id + door id among the loaded scene's doors when the
  door is crossed.

How the lookup is cached or injected is left to the builder (e.g. a room directory built
once per navigation scope).

## Editor

- Room picker for door destinations: a dropdown of every room id in the project, grouped by
  deck, plus "None". The list comes from the room catalog below. Shows a warning when the
  stored id is not in the catalog.
- Door picker for `unlocksOnCross`: room dropdown, then a door dropdown of that room's doors.
- Validation (`Tools/Validate Navigation References`): reports room ids not in `<Deck>/<ROOM>`
  form, duplicate room ids, door destinations / `unlocksOnCross` pointing at unknown ids, and
  map entries with unknown room ids. Existing checks (doors without arrival, missing door
  back) keep working on ids.
- Room graph window, hierarchy labels and map baker read the ids.

## Room catalog

A ScriptableObject listing every room: id, deck, production scene name, and its doors
(door id + whether it has an arrival). Built by an editor button ("Rebuild From Scenes",
like `ItemDatabase`'s "Populate From Project") that scans the production deck scenes, and
rebuilt by the migration. It is the source for the editor pickers and for the teleport in
development builds, where scenes cannot be scanned. Validation flags a catalog that is out
of date with the scenes.

## Debug teleport

- One entry in the F1 debug menu, group `Navigation`, shown as `Teleport: < Deck_B/KITCHEN >`.
  Left/Right cycles the rooms of the catalog (grouped by deck, alphabetical within a deck,
  wrapping); Enter teleports. The debug menu only has Action and Toggle entries today, so a
  third entry kind with a cycling value (Left/Right) is added to the menu.
- Target on the loaded deck: instant, no door animation. The target room is activated and
  the others deactivated (as `RoomOrchestrator.ActivateRoomImmediate` does for save loads),
  the player is placed at the arrival point of the room's first door, the zone camera for
  that position goes live, and `RoomTransitionedEvent` is published so it counts as a real
  visit (map marks it visited).
- Target on another deck: loads that deck's scene the way a stair transition does, and the
  player starts in the target room at the same spot (first door's arrival). This needs the
  scene entry context to carry "start in room X" in addition to today's entry point id.
- A room without doors: the player is placed at the room's origin (data issue, logged).
- Editor and development builds only, like the rest of the debug menu.

## Decided by the user

- Room ids include the deck and are normalized; old saves may be lost.
- `Navigation.unity` is ignored.
- Teleport: all rooms of the game, cycle + Enter in the debug menu, land at the first door's
  arrival, counts as a real visit.

## Calls made by Claude (check these)

- "First door" of a room = the first `RoomDoorInteractable` / `SceneDoorInteractable` with an
  arrival point in hierarchy order. The catalog stores that door so dev builds don't guess.
- Cross-deck teleport plays no door animation: it is a debug shortcut, not a door. A plain
  load (with whatever fade the scene load already has) is enough.
- The teleport is ignored (with a log) while a room/floor transition or combat is running.
- `Port_Stairs` lives in the Deck_B scene, so it becomes `Deck_B/PORT_STAIRS`.
- The catalog asset is referenced from `NavigationScope` so it ships in development builds.
- Ids are compared case-sensitively after migration (they are always generated or picked,
  never typed); `RoomVisitMutation`'s current case-insensitive compare may stay.

## Left to the builder

- Name and placement of the room directory / lookup service and the catalog asset.
- Exact picker UI (PropertyDrawer vs custom inspector).
- How the cycling debug-menu entry renders its value.
- Whether the cross-deck teleport reuses `FloorTransitionService` with a "room" entry or a
  separate path, as long as the player ends in the right room at the first door's arrival.

**Status (2026-10-25):** implemented on `feat/camera-zone-editor` (plan `docs/superpowers/plans/2026-10-25-room-id-references.md`). The catalog stores no "first door": it is computed on arrival, when the target scene is loaded.
