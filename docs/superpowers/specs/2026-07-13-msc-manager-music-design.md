# MSC Manager Music Design

## Context

Wwise already has a complete music setup under `Interactive Music Hierarchy/Music WK.wwu`: a `MusicSwitchContainer` named `MSC_Manager` (posted via the event `Play_MSC_Manager`), driven by two arguments:

- **`PlayerState`** (State Group) — `None`, `Navigation`, `GameOver`, `Dialogue`, `SafeRoom`, `Combat`, `Menu`.
- **`MarineraSector`** (Switch Group) — `DeckB`, `DeckC`, `Laboratory`, `Doors`.

The container's `EntryList` (the actual Wwise-authored routing table) resolves like this:

| PlayerState | MarineraSector | Playlist |
|---|---|---|
| Combat | (any) | Combat |
| Dialogue | (any) | Dialogue |
| GameOver | (any) | GameOver |
| Menu | (any) | Menu |
| SafeRoom | (any) | SafeRoom |
| Navigation | DeckB | NavigationDeckB |
| Navigation | DeckC | NavigationDeckC |
| Navigation | Laboratory | NavigationLaboratory |
| Navigation | Doors | *(no object — silence)* |

Only when `PlayerState = Navigation` does `MarineraSector` matter. No code touches `PlayerState` or `MarineraSector` today (first usage of both, same situation as `Ambients`/the storm RTPCs before this feature).

Existing infrastructure this design reuses without modification:
- `RoomOrchestrator` already publishes `RoomTransitionStartedEvent` (carrying `Origin`/`Destination` `RoomController`s) at the very start of `TransitionToRoomAsync` — i.e. the instant a door interaction begins a transition — and `RoomTransitionedEvent` (carrying the new `ActiveRoom`) once the transition fully ends, whether the player let the door animation finish naturally or pressed skip (both paths funnel through `DoorTransitionController.OnAnimationComplete` → `NotifyComplete` before `RoomOrchestrator` resumes and publishes). Both events are already registered as MessagePipe brokers in `NavigationScope`.
- The `WeatherAmbienceController` pattern from the previous feature: a scene-global `MonoBehaviour, IInitializable, IDisposable`, posting its event once in `Start()` (not VContainer's Awake-phase `Initialize()`, which races Wwise's SoundBank loading — see that feature's fix), applying State/Switch values *before* posting.

## Goal

Drive the `MSC_Manager` music container correctly as the player moves between rooms and through doors, for the `Navigation`/`SafeRoom` half of `PlayerState` only. `Combat`/`Dialogue`/`Menu`/`GameOver` are out of scope for this pass — they'll be wired later when this connects to `CombatScope`/`DialogueService`/etc.

## Design

Three new pieces, all in `Assets/Scripts/Navigation/Rooms/`.

### `SaveRoomMarker`

Empty tag `MonoBehaviour`, added to the one room GameObject that is the save room. No fields — only its presence is checked, via `GetComponent`.

```csharp
public sealed class SaveRoomMarker : MonoBehaviour { }
```

### `RoomSectorProfile`

Plain data `MonoBehaviour`, optional sibling of `RoomController`, same shape as `RoomWeatherProfile`:

```csharp
public sealed class RoomSectorProfile : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Switch marineraSector = new();
    public AK.Wwise.Switch MarineraSector => this.marineraSector;
}
```

This pass adds it to only a couple of test rooms (e.g. one tagged `DeckB`). Rooms without it fall back to `MusicManagerController`'s `defaultSector`.

### `MusicManagerController`

Scene-global `MonoBehaviour, IInitializable, IDisposable`, registered in `NavigationScope` the same way as `WeatherAmbienceController`:

```csharp
builder.RegisterComponentInHierarchy<MusicManagerController>().AsImplementedInterfaces();
```

```csharp
public sealed class MusicManagerController : MonoBehaviour, IInitializable, IDisposable
{
    [SerializeField] private AK.Wwise.Event  mscEvent        = new(); // Play_MSC_Manager
    [SerializeField] private AK.Wwise.State  navigationState = new(); // PlayerState:Navigation
    [SerializeField] private AK.Wwise.State  safeRoomState   = new(); // PlayerState:SafeRoom
    [SerializeField] private AK.Wwise.Switch doorsSector     = new(); // MarineraSector:Doors
    [SerializeField] private AK.Wwise.Switch defaultSector   = new(); // MarineraSector fallback (e.g. DeckB)

    [Inject] private IRoomOrchestrator                        roomOrchestrator                 = null!;
    [Inject] private ISubscriber<RoomTransitionStartedEvent>  roomTransitionStartedSubscriber  = null!;
    [Inject] private ISubscriber<RoomTransitionedEvent>       roomTransitionedSubscriber       = null!;

    private IDisposable? startedSubscription;
    private IDisposable? transitionedSubscription;

    void IInitializable.Initialize()
    {
        this.startedSubscription      = this.roomTransitionStartedSubscriber.Subscribe(OnRoomTransitionStarted);
        this.transitionedSubscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);
    }

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
```

Notes:
- Subscribing to both events happens in `Initialize()` (Awake-phase) — safe, it's plain C# event wiring with no Wwise dependency, same reasoning as `WeatherAmbienceController`.
- `mscEvent` posts exactly once, in `Start()`, *after* the initial `ApplyRoom` call — same ordering fix learned from the weather feature (set switches/states before the event that reads them starts playing).
- No door-interaction-specific code changes: `RoomTransitionStartedEvent` fires the instant a door interaction kicks off a transition (before the door scene even loads), and `RoomTransitionedEvent` fires once the transition is fully done via either the skip path or the natural-completion path — both already unconditionally cover "entered/exited a door transition" without touching `DoorTransitionController` or `RoomOrchestrator`.
- Once `Play_MSC_Manager` is playing, Wwise's own authored `MusicTransition`/fade rules handle crossfading between playlists as the State/Switch change — no re-posting needed on transitions, mirroring how the storm ambience reacts to `Ambients`/RTPC changes without re-posting `Play_WeatherBC`.
- `PlayerState` is untouched during the door transition itself (only `MarineraSector` flips to `Doors`) — matches the EntryList: `Navigation + Doors` is the only combination involved, and `PlayerState` doesn't need to change for it.

### Wwise Picker assignments (manual, editor-side)

- `MusicManagerController.mscEvent` → `Play_MSC_Manager`
- `MusicManagerController.navigationState` → `PlayerState:Navigation`
- `MusicManagerController.safeRoomState` → `PlayerState:SafeRoom`
- `MusicManagerController.doorsSector` → `MarineraSector:Doors`
- `MusicManagerController.defaultSector` → `MarineraSector:DeckB` (Deck B is the deck currently under test; revisit once more decks are wired)
- `SaveRoomMarker` → added to whichever room GameObject is the actual save room (identify via its `RoomController.RoomId` in the editor)
- `RoomSectorProfile` on the couple of test rooms → `marineraSector` set to the matching switch (e.g. `MarineraSector:DeckB`)

## Out of scope

- `Combat`/`Dialogue`/`Menu`/`GameOver` `PlayerState` wiring (future work tied to `CombatScope`/`DialogueService`).
- Rolling `RoomSectorProfile` out to every room — only a couple of test rooms this pass.
- Any change to `DoorTransitionController`, `RoomOrchestrator`, or `RoomTransitionContext` — the existing `RoomTransitionStartedEvent`/`RoomTransitionedEvent` pair already covers everything this feature needs.
- EditMode tests — same reasoning as `WeatherAmbienceController`: thin Wwise-wiring `MonoBehaviour`s aren't unit-tested in this codebase, no abstraction over `AK.Wwise.*` to fake.
