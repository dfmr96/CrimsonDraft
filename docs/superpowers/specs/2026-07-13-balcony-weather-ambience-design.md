# Balcony Weather Ambience Design

## Context

Wwise already has everything needed to blend the storm ambience by room, authored under the "Storm" `BlendContainer` (Game Parameters/WeatherParameters.wwu, States/Default Work Unit.wwu):

- Event `Play_WeatherBC` — a single Play action targeting the "Storm" BlendContainer.
- Game Parameters (RTPC) `InsideStormForce` and `OutsideStormForce`.
- State Group `Ambients` with values `None`, `Bathroom`, `Stairs`, `Hallway`, `Balcony`, `Room`, `Kitchen`.

Every sound inside "Storm" (both the muffled "Inside*" branch and the plain/exterior branch) already has per-`Ambients`-state volume curves authored in Wwise, including `Balcony`. So the mix per room is entirely Wwise's responsibility — the game only needs to post the correct `Ambients` state and the two RTPC values whenever the active room changes.

No code in the project currently uses `AK.Wwise.State` or `AK.Wwise.RTPC`; this is the first usage of both wrapper types.

Existing precedents this design follows:
- `MapStateTracker` (Navigation/MapStateTracker.cs) — pure C# `IInitializable`/`IDisposable` service that subscribes to `RoomTransitionedEvent` and also reads `IRoomOrchestrator.CurrentRoom` directly in `Initialize()`, because the starting room activation in `RoomOrchestrator.Initialize()` does not publish a transition event.
- `NavigationCameraRegistrar` — a `MonoBehaviour, IInitializable` registered via `builder.RegisterComponentInHierarchy<T>().AsImplementedInterfaces()`.
- `DoorTransitionController` / `RoomDoorInteractable` (both in `Navigation/Rooms`) — already post `AK.Wwise.Event`/`AK.Wwise.Switch` directly as `[SerializeField]` fields, and `CrimsonDraft.Navigation.asmdef` already references `AK.Wwise.Unity.API.WwiseTypes`. This feature follows the same placement instead of introducing a new dependency from `CrimsonDraft.Audio` onto `CrimsonDraft.Navigation`.
- `Surface` (Audio/Surface.cs) — a plain data tag-component read by another system (`FootstepController`) via `GetComponent`. `RoomWeatherProfile` follows the same shape.

## Goal

When the player is in the Balcony room, the storm ambience (`Play_WeatherBC`) should audibly reflect that room via the `Ambients` state and the two RTPC values. Other rooms fall back to silence until they get their own profile authored later — this first pass proves the pipeline end-to-end on one room, not the full room roster.

## Design

Two new classes in `Assets/Scripts/Navigation/Rooms/`.

### `RoomWeatherProfile`

Plain data `MonoBehaviour`, optional sibling component next to `RoomController` on any room GameObject that should have weather exposure (Balcony, for this pass).

```csharp
namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomWeatherProfile : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.State ambientState        = new(); // e.g. Ambients:Balcony
        [SerializeField] private float          insideStormForce;
        [SerializeField] private float          outsideStormForce;

        public AK.Wwise.State AmbientState      => this.ambientState;
        public float          InsideStormForce  => this.insideStormForce;
        public float          OutsideStormForce => this.outsideStormForce;
    }
}
```

No logic. Rooms without weather exposure simply don't have this component.

### `WeatherAmbienceController`

Single scene-level `MonoBehaviour, IInitializable`, registered in `NavigationScope`:

```csharp
builder.RegisterComponentInHierarchy<WeatherAmbienceController>().AsImplementedInterfaces();
```

```csharp
namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class WeatherAmbienceController : MonoBehaviour, IInitializable
    {
        [SerializeField] private AK.Wwise.Event weatherEvent          = new(); // Play_WeatherBC
        [SerializeField] private AK.Wwise.RTPC  insideStormForceRtpc  = new(); // InsideStormForce
        [SerializeField] private AK.Wwise.RTPC  outsideStormForceRtpc = new(); // OutsideStormForce
        [SerializeField] private AK.Wwise.State defaultAmbientState   = new(); // Ambients:None

        private IRoomOrchestrator? roomOrchestrator;
        private IDisposable?       subscription;

        [Inject]
        public void Construct(
            IRoomOrchestrator                    roomOrchestrator,
            ISubscriber<RoomTransitionedEvent>   roomTransitionedSubscriber)
        {
            this.roomOrchestrator = roomOrchestrator;
            this.subscription     = roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);
        }

        void IInitializable.Initialize()
        {
            this.weatherEvent.Post(gameObject);
            ApplyRoom(this.roomOrchestrator?.CurrentRoom);
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e) => ApplyRoom(e.ActiveRoom);

        private void ApplyRoom(RoomController? room)
        {
            var profile = room != null ? room.GetComponent<RoomWeatherProfile>() : null;

            if (profile != null)
            {
                profile.AmbientState.SetValue();
                this.insideStormForceRtpc.SetGlobalValue(profile.InsideStormForce);
                this.outsideStormForceRtpc.SetGlobalValue(profile.OutsideStormForce);
            }
            else
            {
                this.defaultAmbientState.SetValue();
                this.insideStormForceRtpc.SetGlobalValue(0f);
                this.outsideStormForceRtpc.SetGlobalValue(0f);
            }
        }

        private void OnDestroy() => this.subscription?.Dispose();
    }
}
```

Notes:
- `weatherEvent` is posted exactly once, in `Initialize()`. It is never stopped or re-posted — the ambience loops for the whole session and the State/RTPC changes are what make rooms without exposure sound silent (via the curves already authored in Wwise) or the Balcony sound present.
- `Initialize()` applies the current room immediately (covering the starting room, which never fires `RoomTransitionedEvent`), then `OnRoomTransitioned` keeps it in sync on every later transition.
- A room with no `RoomWeatherProfile` always resets to `defaultAmbientState` (`Ambients:None`) and both RTPCs at `0` — this is a hard default so that leaving Balcony for an unconfigured room can never leave the storm "stuck" audible.
- `AK.Wwise.RTPC.SetGlobalValue(float)` and `AK.Wwise.State.SetValue()` are both global (no `GameObject` parameter) — matches how these Wwise types work; only `weatherEvent.Post(gameObject)` needs an emitter, which is why this must be a `MonoBehaviour` rather than a pure C# service like `MapStateTracker`.

### Wwise Picker assignments (manual, editor-side)

- `WeatherAmbienceController.weatherEvent` → `Play_WeatherBC`
- `WeatherAmbienceController.insideStormForceRtpc` → `InsideStormForce`
- `WeatherAmbienceController.outsideStormForceRtpc` → `OutsideStormForce`
- `WeatherAmbienceController.defaultAmbientState` → `Ambients:None`
- On the Balcony room GameObject's new `RoomWeatherProfile`: `ambientState` → `Ambients:Balcony`, plus `insideStormForce`/`outsideStormForce` values (exact numbers are an audio-authoring decision made in-editor while listening, not a code concern — start from something audible on the Outside RTPC since Balcony is exposed, and iterate by ear).

## Out of scope

- Filling in `RoomWeatherProfile` for any room other than Balcony.
- Smoothing/transition ramps on the RTPC changes when crossing rooms (instant `SetGlobalValue` for this pass).
- Any EditMode test coverage — consistent with `FootstepController`/`DoorTransitionController`, there's no abstraction over `AK.Wwise.*` to fake, and this class only wires Wwise calls to already-tested room-transition plumbing (`IRoomOrchestrator`, `RoomTransitionedEvent`).
- Changes to `RoomController`, `RoomOrchestrator`, or the `Ambients`/`WeatherParameters` Wwise objects themselves.
