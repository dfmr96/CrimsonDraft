# Save Game System — Design

Date: 2026-08-18
Status: Approved by user, pending implementation plan

## Summary

Add a Resident-Evil-classic-style save point: an `IInteractable` GameObject
("typewriter") that opens a slot-selection menu, writes the full game state
(doors, rooms visited, pickups, notes, known maps, defeated enemies,
inventory, operator HP, player position/room/scene) to a JSON file on disk,
and can later restore it. Wires up the already-present but disabled
"Load Game" button on the main menu.

Free save (no consumable item cost), multiple slots (20), full save + load
scope.

## Current state (context for implementers)

The project already has an in-memory "registry" layer — DI singletons in
`GameLifetimeScope`, `DontDestroyOnLoad`, surviving scene transitions within
one app run:

- `DoorStateRegistry` — `GetState()`/`LoadState()` present.
- `RoomStateRegistry` — `GetState()`/`LoadState()` present (drives the map UI
  via `MapStateTracker`; doubles as the "visited rooms" record).
- `KnownMapsRegistry` — `LoadState(IEnumerable<string>)` present.
- `PickupRegistry` — collected pickup IDs (`HashSet<string>`). **No
  `LoadState`/`GetState` yet — needs adding.**
- `NoteRegistry` — read note IDs, same shape. **Needs `LoadState`/`GetState`.**
- `EnemyStateRegistry` — defeated enemy IDs. **Needs `LoadState`/`GetState`.**
- `InventoryStateRegistry` — generic `Save(object)`/`Load<T>()` blob, already
  wired to `InventoryService.GetRawSlots()`/`LoadState()` via
  `InventoryBootstrap`.
- `RosterHealthRegistry` — operator HP array, same shape, wired via
  `OperatorRosterBootstrap`.

None of this is written to disk today. There is no save/load code anywhere in
the project. `MainMenuController.loadGameButton` exists but is disabled with
a `// Not implemented yet` comment — this feature fills that gap.

Interactable pattern to follow (seen in `ContainerInteractable` /
`ContainerController` / `ContainerView`): a thin `MonoBehaviour` implementing
`IInteractable` forwards to a VContainer-registered, scope-lifetime
`Controller` (`IInitializable`/`IDisposable`) which owns a `View`.

`PlayerController`, `RoomOrchestrator`, and `InventoryService` all live in
the per-scene `NavigationScope`, not the root `GameLifetimeScope` — a root
singleton cannot constructor-inject them. Any code that needs to read/write
live player position, current room, or live inventory must live in
`NavigationScope`.

## Architecture

Chosen approach: extend the existing registry layer and bridge it to disk
through a scene-scoped controller, rather than a generic reflection- or
interface-plugin-based save system. The set of persisted systems is small
(~8) and fixed; a generic `ISaveParticipant` plugin layer would need
polymorphic JSON (pushing us onto Newtonsoft.Json) for no real benefit here.

```
SavePointInteractable (MonoBehaviour, IInteractable)
    → SaveController.Open()                    [NavigationScope]
        reads: InventoryService, PlayerController, RoomOrchestrator,
               all 8 registries, ItemDatabase
        writes via → ISaveGameService.WriteToDisk(slot, SaveGameData)  [root]

MainMenuController "Load Game"
    → ISaveGameService.ListSlotSummaries()      [root, disk read]
    → ISaveGameService.LoadSlot(slot)           [root]
        reads full SaveGameData from disk, stores as "pending load",
        SceneManager.LoadScene(data.sceneName)

On next NavigationScope bootstrap:
    SaveGameLoader : IInitializable              [NavigationScope, runs FIRST]
        → ISaveGameService.ConsumePendingLoad()
        → registries.LoadState(...), inventoryService.LoadState(...),
          RosterHealthRegistry apply
        → after RoomOrchestrator.Initialize(): roomOrchestrator.ActivateRoomImmediate(roomId)
        → player.transform.SetPositionAndRotation(savedPos, savedRot)
```

## Data model

Files: `{Application.persistentDataPath}/Saves/slot_00.json` .. `slot_19.json`
(20 slots). Plain JSON via `JsonUtility` (no new dependency). Writes go to a
temp file then are renamed over the target, to avoid a corrupt file if the
process dies mid-write.

```csharp
[Serializable]
public sealed class SaveGameData
{
    public string sceneName;
    public string roomId;
    public string timestampIso;
    public float playtimeSeconds;

    public Vector3 playerPosition;
    public Quaternion playerRotation;

    public List<DoorStateEntry> doors;
    public List<RoomStateEntry> rooms;
    public List<string> collectedPickupIds;
    public List<string> readNoteIds;
    public List<string> knownMapIds;
    public List<string> defeatedEnemyIds;
    public List<InventorySlotEntry> inventorySlots;
    public int[] operatorHp;
}
```

`DoorStateEntry`/`RoomStateEntry` are `{ string id; <TheirEnum> state; }`
pairs (`JsonUtility` cannot serialize `Dictionary` directly). `InventorySlotEntry`
carries `itemId` (string) plus the runtime fields needed to rehydrate each
`InventoryItem` subtype: `quantity`, `ammoInMag` (weapons), `usesRemaining`
(key items), `isExamined`, `gridCol`, `gridRow`, `gridRotation`,
`equippedSlot`.

For the slot-list UI, the full file is read and only the metadata fields
(`sceneName`/`roomId`/`timestampIso`/`playtimeSeconds`) are used — these
files are small (a few KB), so a separate lightweight header format is not
worth the complexity.

### New: `ItemDatabase`

`Assets/Scripts/Inventory/ItemDatabase.cs` — a `ScriptableObject` holding
`ItemData[] allItems`, exposing `bool TryGetById(string itemId, out ItemData item)`
backed by a runtime-built dictionary. An editor-only button populates
`allItems` via `AssetDatabase.FindAssets("t:ItemData")` so it doesn't need
manual upkeep. Registered as a singleton in `GameLifetimeScope` via a
serialized field, same as other data assets. This is the missing piece that
lets inventory slots round-trip through a string ID.

## Capture / restore pipeline

**Save** (`SaveController`, `NavigationScope`-scoped, same shape as
`ContainerController`):

1. Injects `InventoryService`, `PlayerController`, `RoomOrchestrator`, the 8
   registries, `ItemDatabase`, `ISaveGameService`, `SaveView`.
2. `Open()` — called from `SavePointInteractable.Interact()` — shows
   `SaveView` populated from `saveGameService.ListSlotSummaries()`.
3. On slot confirm: builds `SaveGameData` from each registry's
   `GetState()`/equivalent, `roomOrchestrator.CurrentRoom.RoomId`,
   `player.transform.position/rotation`, `SceneManager.GetActiveScene().name`.
   Occupied slots prompt an overwrite confirmation first.
4. Calls `saveGameService.WriteToDisk(slot, data)`.

**Load**: `PlayerController`/`RoomOrchestrator`/`InventoryService` don't
exist yet from the Main Menu, so `ISaveGameService` (root singleton) reads
the file, stashes the `SaveGameData` as a pending-load payload, and does
`SceneManager.LoadScene(data.sceneName)`.

`SaveGameLoader : IInitializable` (`NavigationScope`-scoped) must run
**before** `DoorBootstrap`, `PickupBootstrap`, `MapPickupBootstrap`,
`DocumentPickupBootstrap`, and `RoomOrchestrator`'s own initialization,
since those read the registries as soon as they start. On init:

1. `saveGameService.ConsumePendingLoad()` — no-op if nothing pending (normal
   scene entry).
2. If present: `LoadState(...)` on each registry, `inventoryService.LoadState(...)`
   (resolving each `itemId` via `ItemDatabase`), apply operator HP to
   `RosterHealthRegistry`.
3. After `RoomOrchestrator.Initialize()` runs, call the new
   `roomOrchestrator.ActivateRoomImmediate(roomId)` (deactivates all rooms,
   activates the saved one, skips the animated door transition), then
   `player.transform.SetPositionAndRotation(savedPos, savedRot)`, overriding
   whatever default spawn point logic placed the player.

**New game reset**: each registry gets a `ClearAll()`. A new root singleton
`IGameStateResetter.ResetAll()` calls all 8. `MainMenuController.OnNewGameClicked()`
calls this before `SceneManager.LoadScene(newGameSceneName)`, so a fresh game
started within the same process doesn't inherit leftover state from a
previous playthrough.

## Interactable + UI

- `SavePointInteractable` (`Assets/Scripts/Navigation/Interactables/SavePointInteractable.cs`)
  — thin `MonoBehaviour`, `IInteractable`, calls `context.SaveController.Open()`.
  A `SaveController` field is added to `InteractionContext` and wired in
  `PlayerInteractionCaster`, matching the existing pattern for
  `ContainerController`/`DocumentController`. Placed as a scene object
  (typewriter prop) in rooms already tagged with `SaveRoomMarker` (currently
  only used for save-room music).
- `SaveView` — uGUI panel, same visual family as `ContainerView`: a list of
  20 slots, each showing slot number and either `"-- empty --"` or
  `roomId` + formatted timestamp + playtime. Confirming an occupied slot
  prompts an overwrite confirmation.
- **Main Menu → Load Game**: reuses `SaveView` in a read/select mode.
  `MainMenuController.loadGameButton` becomes enabled and opens the same
  view; selecting a slot calls `saveGameService.LoadSlot(slotIndex)`.

## Testing

EditMode tests, following the project's plain-fake pattern
(`FakeOrchestrator`-style):

- `SaveGameService`: write/read round-trip against a temp directory,
  `ListSlotSummaries()` correctness, empty vs occupied slots, corrupt-file
  handling (skip/report, don't throw).
- `ClearAll()` on each of the 8 registries.
- `SaveController`: builds correct `SaveGameData` from fake registries/fake
  `InventoryService`/fake `RoomOrchestrator`/fake `PlayerController`.
- `SaveGameLoader`: applies a `SaveGameData` payload to fakes correctly,
  no-ops when nothing is pending.

## Out of scope

- Save-point animation/cinematic (camera transition like the door system) —
  explicitly deferred; the menu opens directly on interact.
- Ink-ribbon-style consumable cost for saving — explicitly deferred, free
  save for now.
- Save file versioning/migration across game patches.
- Deleting individual save slots from the UI (can be added later; the
  service design doesn't block it).
