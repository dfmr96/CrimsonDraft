# MSC Manager Music Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drive Wwise's `MSC_Manager` music container correctly as the player moves between rooms and through doors — `PlayerState` toggles between `Navigation` (default) and `SafeRoom` (in the designated save room), `MarineraSector` follows the active room's sector (or `Doors` mid-transition). `Combat`/`Dialogue`/`Menu`/`GameOver` are out of scope.

**Architecture:** Three new classes in `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/`: `SaveRoomMarker` (empty tag component), `RoomSectorProfile` (per-room `AK.Wwise.Switch` data, same shape as `RoomWeatherProfile`), and `MusicManagerController` (scene-global `MonoBehaviour, IInitializable, IDisposable`, same pattern as `WeatherAmbienceController` — subscribes to `RoomTransitionStartedEvent`/`RoomTransitionedEvent`, posts `Play_MSC_Manager` once in `Start()`).

**Tech Stack:** C# / Unity, Wwise (`AK.Wwise.Event`, `AK.Wwise.State`, `AK.Wwise.Switch`), VContainer, MessagePipe, UnityMCP tools for compilation/console verification and for wiring Wwise references via `AK.Wwise.BaseType.SetupReference` (the same call the Wwise Picker uses internally — see Task 2).

## Global Constraints

- `#nullable enable` in every file (existing convention).
- `CrimsonDraft.Navigation.asmdef` already references `AK.Wwise.Unity.API.WwiseTypes` — no asmdef edit needed.
- No new EditMode tests — same reasoning as `WeatherAmbienceController`/`FootstepController`/`DoorTransitionController`: no abstraction over `AK.Wwise.*` to fake.
- Do not modify `DoorTransitionController`, `RoomOrchestrator`, or `RoomTransitionContext` — `RoomTransitionStartedEvent`/`RoomTransitionedEvent` already cover everything this feature needs.
- Only wire `RoomSectorProfile` onto a couple of test rooms this pass, and `SaveRoomMarker` onto the one real save room — not a full rollout.
- Git: no `Co-Authored-By` trailers (per CLAUDE.md).

**Wwise GUIDs needed for wiring (from the project's own `.wwu` files):**

| Object | GUID |
|---|---|
| Event `Play_MSC_Manager` | `3CCEE1FE-81FA-415A-BC3D-BE6ED8314C79` |
| StateGroup `PlayerState` | `E9283677-2A10-4DEA-A499-D62316785166` |
| State `PlayerState:Navigation` | `816FB592-0331-450A-AFFA-8D733B19A655` |
| State `PlayerState:SafeRoom` | `8F1695FE-5BF5-430A-8F6F-2A39A6A0F075` |
| SwitchGroup `MarineraSector` | `F4C2CDF7-0A54-4E64-AAD8-4B0BB3B12EF4` |
| Switch `MarineraSector:Doors` | `3EEF49EA-319F-4400-B878-D0821EDC0FB6` |
| Switch `MarineraSector:DeckB` | `22AC347F-1F0B-489C-AD7F-E42D802977A2` |

---

### Task 1: Create SaveRoomMarker, RoomSectorProfile, MusicManagerController, register in NavigationScope

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SaveRoomMarker.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomSectorProfile.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/MusicManagerController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs:115`

**Interfaces:**
- Produces: `RoomSectorProfile.MarineraSector` (`AK.Wwise.Switch`, get-only) — read by `MusicManagerController` via `RoomController.GetComponent<RoomSectorProfile>()`.
- Produces: `SaveRoomMarker` (marker only, no members) — checked via `RoomController.GetComponent<SaveRoomMarker>() != null`.
- Consumes: `IRoomOrchestrator.CurrentRoom`, `RoomTransitionStartedEvent` (`Origin`/`Destination` `RoomController`), `RoomTransitionedEvent.ActiveRoom` — all already exist in `CrimsonDraft.Navigation`/`CrimsonDraft.Navigation.Rooms`.

- [ ] **Step 1: Create SaveRoomMarker.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Empty tag component — its presence on a room's GameObject marks it as the save room.
    // MusicManagerController checks for it via GetComponent.
    public sealed class SaveRoomMarker : MonoBehaviour { }
}
```

- [ ] **Step 2: Create RoomSectorProfile.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Optional sibling of RoomController on rooms that have a MarineraSector assigned.
    // Rooms without this component fall back to MusicManagerController's defaultSector.
    public sealed class RoomSectorProfile : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Switch marineraSector = new();

        public AK.Wwise.Switch MarineraSector => this.marineraSector;
    }
}
```

- [ ] **Step 3: Create MusicManagerController.cs**

```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class MusicManagerController : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private AK.Wwise.Event  mscEvent        = new(); // Play_MSC_Manager
        [SerializeField] private AK.Wwise.State  navigationState = new(); // PlayerState:Navigation
        [SerializeField] private AK.Wwise.State  safeRoomState   = new(); // PlayerState:SafeRoom
        [SerializeField] private AK.Wwise.Switch doorsSector     = new(); // MarineraSector:Doors
        [SerializeField] private AK.Wwise.Switch defaultSector   = new(); // MarineraSector fallback (e.g. DeckB)

        [Inject] private IRoomOrchestrator                 roomOrchestrator                = null!;
        [Inject] private ISubscriber<RoomTransitionStartedEvent> roomTransitionStartedSubscriber = null!;
        [Inject] private ISubscriber<RoomTransitionedEvent>      roomTransitionedSubscriber      = null!;

        private IDisposable? startedSubscription;
        private IDisposable? transitionedSubscription;

        void IInitializable.Initialize()
        {
            this.startedSubscription      = this.roomTransitionStartedSubscriber.Subscribe(OnRoomTransitionStarted);
            this.transitionedSubscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);
        }

        // Deferred to Start() (not here): posting Play_MSC_Manager during VContainer's
        // Awake-phase Initialize() races Wwise's SoundBank load, same issue fixed on
        // WeatherAmbienceController — Wwise silently drops the event if posted too early.
        private void Start()
        {
            ApplyRoom(this.roomOrchestrator.CurrentRoom);
            this.mscEvent.Post(gameObject);
        }

        private void OnRoomTransitionStarted(RoomTransitionStartedEvent e) => this.doorsSector.SetValue(gameObject);

        private void OnRoomTransitioned(RoomTransitionedEvent e) => ApplyRoom(e.ActiveRoom);

        private void ApplyRoom(RoomController? room)
        {
            var sectorProfile = room != null ? room.GetComponent<RoomSectorProfile>() : null;
            if (sectorProfile != null)
                sectorProfile.MarineraSector.SetValue(gameObject);
            else
                this.defaultSector.SetValue(gameObject);

            var isSaveRoom = room != null && room.GetComponent<SaveRoomMarker>() != null;
            if (isSaveRoom)
                this.safeRoomState.SetValue();
            else
                this.navigationState.SetValue();
        }

        void IDisposable.Dispose()
        {
            this.startedSubscription?.Dispose();
            this.transitionedSubscription?.Dispose();
        }
    }
}
```

`RoomTransitionStartedEvent`/`RoomTransitionedEvent` live in the parent `CrimsonDraft.Navigation` namespace — the `using CrimsonDraft.Navigation;` above is required for the same reason it was on `WeatherAmbienceController` (C# doesn't implicitly expose a parent namespace's types to a child namespace).

- [ ] **Step 4: Register in NavigationScope**

Edit `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`, immediately after the `WeatherAmbienceController` line (currently line 115):

```csharp
            builder.RegisterComponentInHierarchy<WeatherAmbienceController>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<MusicManagerController>().AsImplementedInterfaces();
```

- [ ] **Step 5: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` (compile: request, mode: force) to force a domain reload, then `mcp__UnityMCP__read_console` filtered to errors. Expected: no errors referencing `SaveRoomMarker`, `RoomSectorProfile`, `MusicManagerController`, or `NavigationScope`.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/SaveRoomMarker.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomSectorProfile.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/MusicManagerController.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(audio): add SaveRoomMarker, RoomSectorProfile, and MusicManagerController"
```

---

### Task 2: Add MusicManagerController to the Navigation scene and wire its Wwise references

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity`

Follow the exact approach used for `WeatherAmbienceController` (see `docs/superpowers/plans/2026-07-13-balcony-weather-ambience.md` Task 2): wire references via `AK.Wwise.BaseType.SetupReference`/`WwiseGroupValueObjectReference.SetupGroupObjectReference` through `mcp__UnityMCP__execute_code`, rather than hand-authoring GUIDs into the scene YAML — this generates/reuses the same `Assets/Wwise/ScriptableObjects/*` reference assets the Picker would.

- [ ] **Step 1: Load the scene and create the GameObject**

Open `Assets/Scenes/Production/Navigation.unity` (additive load if not already open; `mcp__UnityMCP__manage_scene` action `load`, `path: "Assets/Scenes/Production/Navigation.unity"`, `additive: true`; then `set_active_scene` to `Navigation`). Create an empty GameObject at the root named `MusicManager`, with the `Music Manager Controller` component:

```
mcp__UnityMCP__manage_gameobject action=create name="MusicManager" components_to_add=["CrimsonDraft.Navigation.Rooms.MusicManagerController"]
```

- [ ] **Step 2: Wire the five Wwise references**

Via `mcp__UnityMCP__execute_code`:

```csharp
var go = GameObject.Find("MusicManager");
var controller = go.GetComponent<CrimsonDraft.Navigation.Rooms.MusicManagerController>();
var type = controller.GetType();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

var mscEvent = (AK.Wwise.Event)type.GetField("mscEvent", flags).GetValue(controller);
mscEvent.SetupReference("Play_MSC_Manager", new System.Guid("3CCEE1FE-81FA-415A-BC3D-BE6ED8314C79"));

var navState = (AK.Wwise.State)type.GetField("navigationState", flags).GetValue(controller);
navState.SetupReference("Navigation", new System.Guid("816FB592-0331-450A-AFFA-8D733B19A655"));
navState.WwiseObjectReference.SetupGroupObjectReference("PlayerState", new System.Guid("E9283677-2A10-4DEA-A499-D62316785166"));

var safeState = (AK.Wwise.State)type.GetField("safeRoomState", flags).GetValue(controller);
safeState.SetupReference("SafeRoom", new System.Guid("8F1695FE-5BF5-430A-8F6F-2A39A6A0F075"));
safeState.WwiseObjectReference.SetupGroupObjectReference("PlayerState", new System.Guid("E9283677-2A10-4DEA-A499-D62316785166"));

var doorsSwitch = (AK.Wwise.Switch)type.GetField("doorsSector", flags).GetValue(controller);
doorsSwitch.SetupReference("Doors", new System.Guid("3EEF49EA-319F-4400-B878-D0821EDC0FB6"));
doorsSwitch.WwiseObjectReference.SetupGroupObjectReference("MarineraSector", new System.Guid("F4C2CDF7-0A54-4E64-AAD8-4B0BB3B12EF4"));

var defaultSwitch = (AK.Wwise.Switch)type.GetField("defaultSector", flags).GetValue(controller);
defaultSwitch.SetupReference("DeckB", new System.Guid("22AC347F-1F0B-489C-AD7F-E42D802977A2"));
defaultSwitch.WwiseObjectReference.SetupGroupObjectReference("MarineraSector", new System.Guid("F4C2CDF7-0A54-4E64-AAD8-4B0BB3B12EF4"));

UnityEditor.EditorUtility.SetDirty(controller);
UnityEditor.EditorUtility.SetDirty(controller.gameObject);

return "mscEvent=" + mscEvent.IsValid() + " nav=" + navState.IsValid() + " safe=" + safeState.IsValid() +
       " doors=" + doorsSwitch.IsValid() + " default=" + defaultSwitch.IsValid();
```

Expected: all five report `True`. `AK.Wwise.Switch` is a `BaseGroupType` just like `AK.Wwise.State` — same `SetupReference` + `SetupGroupObjectReference` two-step (see `WeatherAmbienceController`'s `defaultAmbientState` wiring for the State-side precedent; `Switch`'s `WwiseObjectReference` field is typed `WwiseSwitchReference`, also a `WwiseGroupValueObjectReference`).

- [ ] **Step 3: Save and verify**

Save the scene explicitly to its own path (`mcp__UnityMCP__manage_scene` action `save`, `name: "Navigation"`, `path: "Assets/Scenes/Production/Navigation.unity"` — passing `path` explicitly avoids the "Save As to the wrong default location" mistake hit during the weather feature). Confirm via `git diff` that the `MusicManagerController` block in the scene YAML has non-empty `WwiseObjectReference` guids for all five fields (not the default-constructed empty values).

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity
git add Game/CrimsonDraft/Assets/Wwise/ScriptableObjects
git commit -m "content(audio): add MusicManagerController to Navigation scene"
```

(Check `git status` first — the second `git add` should only pick up genuinely new/regenerated Wwise reference assets from this step.)

---

### Task 3: Mark the save room and wire a couple of test rooms with RoomSectorProfile

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Deck_B_Development.unity`

- [ ] **Step 1: Identify the save room**

Use `mcp__UnityMCP__find_gameobjects` (`search_method: by_component`, `search_term: "CrimsonDraft.Navigation.Rooms.RoomController"`, `include_inactive: true`) to list all `RoomController` instances in `Deck_B_Development.unity`, then inspect each candidate's `Room Id` (via the `mcpforunity://scene/gameobject/{id}/component/RoomController` resource, or the Inspector) until you find the one that's the intended save room. If it's not obvious from `RoomId` naming, ask the user which room this is before proceeding — don't guess.

- [ ] **Step 2: Add SaveRoomMarker**

On that room's GameObject (the one holding `RoomController`), add the `Save Room Marker` component (`mcp__UnityMCP__manage_components` action `add`, `component_type: "CrimsonDraft.Navigation.Rooms.SaveRoomMarker"`).

- [ ] **Step 3: Pick one or two test rooms for RoomSectorProfile**

Pick 1-2 rooms already visible in `Deck_B_Development.unity` (e.g. the starting room, `Port_Stairs`, found during the weather feature's verification) to represent Deck B. For each, use the same discovery technique as Step 1 (`find_gameobjects` by `RoomController`, `include_inactive: true`, then `EditorUtility.InstanceIDToObject` on the resolved instance ID — rooms other than the currently-active one are inactive, so `GameObject.Find` alone won't see them) to get the room's GameObject, add the `Room Sector Profile` component to it (`manage_components` action `add`), then wire its `marineraSector` field via `execute_code`:

```csharp
var roomId = 0; // instance ID resolved from find_gameobjects in Step 1's style, for this specific room
var go = (GameObject)UnityEditor.EditorUtility.InstanceIDToObject(roomId);
var profile = go.GetComponent<CrimsonDraft.Navigation.Rooms.RoomSectorProfile>();
var type = profile.GetType();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

var sector = (AK.Wwise.Switch)type.GetField("marineraSector", flags).GetValue(profile);
sector.SetupReference("DeckB", new System.Guid("22AC347F-1F0B-489C-AD7F-E42D802977A2"));
sector.WwiseObjectReference.SetupGroupObjectReference("MarineraSector", new System.Guid("F4C2CDF7-0A54-4E64-AAD8-4B0BB3B12EF4"));

UnityEditor.EditorUtility.SetDirty(profile);
return "sector valid=" + sector.IsValid();
```

Expected: `True`.

- [ ] **Step 4: Save and verify**

Save `Deck_B_Development.unity`. Confirm via `git diff` that only the intended rooms' GameObjects changed (new `SaveRoomMarker` on the save room; new `RoomSectorProfile` with a valid, non-empty `marineraSector` reference on the test room(s)).

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/Deck_B_Development.unity
git add Game/CrimsonDraft/Assets/Wwise/ScriptableObjects
git commit -m "content(audio): mark save room and wire Deck B RoomSectorProfile test rooms"
```

---

### Task 4: End-to-end verification in Play Mode

**Files:** none (verification only)

- [ ] **Step 1: Verify startup state**

Enter Play Mode directly from `Deck_B_Development.unity` (not by manually additively loading `Navigation.unity` — doing so duplicates `EventSystem`/scope content and produces unrelated errors; let the normal `Bootstrapper` flow load `Navigation.unity`, exactly as during the weather feature's verification). Via `execute_code`, reflect into the `MusicManagerController` instance and confirm:
- `Play_MSC_Manager` posted successfully (re-post and check `playingID != 0`).
- `PlayerState` reads `Navigation` unless the starting room has a `SaveRoomMarker`.
- `MarineraSector` reads the starting room's `RoomSectorProfile` value if present, else `DeckB` (the `defaultSector`).

- [ ] **Step 2: Verify the save room**

Move the player into the save room (or simulate via reflection by calling the private `ApplyRoom` method with that room's `RoomController`, same technique used to verify `WeatherAmbienceController`). Confirm `PlayerState` reads `SafeRoom`.

- [ ] **Step 3: Verify door transition sector switch**

Trigger a door transition (interact with a `RoomDoorInteractable`) between two rooms. Confirm via console/Wwise Profiler (or a temporary reflection check timed around the transition):
- The instant the transition starts, `MarineraSector` reads `Doors`.
- Once the transition ends (either skip or natural completion), `MarineraSector` reads the destination room's sector (or `defaultSector` if it has no `RoomSectorProfile`), and `PlayerState` is re-evaluated for the destination (`SafeRoom` if it's the save room, else `Navigation`).

- [ ] **Step 4: Report results**

Summarize pass/fail for all three checks. If any check fails, stop and debug before considering this plan complete — do not report success without having observed the actual State/Switch values directly.
