# Balcony Weather Ambience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Post the `Play_WeatherBC` Wwise event once and keep the `Ambients` State plus the `InsideStormForce`/`OutsideStormForce` RTPCs in sync with whichever room the player is in, so the storm ambience is audibly correct on the Balcony room and silent everywhere else (until more rooms get their own profile later).

**Architecture:** Two new classes in `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/`. `RoomWeatherProfile` is a plain-data `MonoBehaviour` — an optional sibling of `RoomController` on any room GameObject that has weather exposure, holding one `AK.Wwise.State` and two float RTPC values. `WeatherAmbienceController` is a scene-global `MonoBehaviour, IInitializable, IDisposable` (registered via VContainer's `RegisterComponentInHierarchy`, same pattern as `NavigationCameraRegistrar`) that posts the ambience event once, subscribes to `RoomTransitionedEvent`, and on every room change looks up a `RoomWeatherProfile` on the active `RoomController` — applying it if found, or a silent default (`Ambients:None`, both RTPCs at `0`) if not.

**Tech Stack:** C# / Unity, Wwise (`AK.Wwise.Event`, `AK.Wwise.State`, `AK.Wwise.RTPC` — first use of the last two in this codebase), VContainer, MessagePipe, UnityMCP tools for compilation/console verification (this project has no CLI test runner — see CLAUDE.md).

## Global Constraints

- `#nullable enable` in every file (existing convention).
- No new EditMode tests: this codebase does not unit-test thin Wwise-wiring MonoBehaviours (`FootstepController`, `DoorTransitionController` have none either) because there's no abstraction over `AK.Wwise.*` to fake. Verification here is compilation + manual Play Mode check.
- `CrimsonDraft.Navigation.asmdef` already references `AK.Wwise.Unity.API.WwiseTypes` — no asmdef edit needed (confirmed by reading the file during design research).
- Only fill in `RoomWeatherProfile` values for the Balcony room this pass — every other room is expected to fall back to the silent default. Do not touch `RoomController`, `RoomOrchestrator`, or the Wwise `Ambients`/`WeatherParameters` objects themselves.
- Git: no `Co-Authored-By` trailers (per CLAUDE.md).

---

### Task 1: Create RoomWeatherProfile and WeatherAmbienceController, register in NavigationScope

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomWeatherProfile.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/WeatherAmbienceController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs:114`

**Interfaces:**
- Produces: `RoomWeatherProfile.AmbientState` (`AK.Wwise.State`, get-only), `RoomWeatherProfile.InsideStormForce` (`float`, get-only), `RoomWeatherProfile.OutsideStormForce` (`float`, get-only) — read by `WeatherAmbienceController` via `RoomController.GetComponent<RoomWeatherProfile>()`.
- Consumes: `IRoomOrchestrator.CurrentRoom` (`RoomController?`, already exists), `RoomTransitionedEvent.ActiveRoom` (`RoomController`, already exists), `RoomController` (already exists, same namespace).

- [ ] **Step 1: Create RoomWeatherProfile.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Optional sibling of RoomController on any room GameObject with weather exposure.
    // Rooms without this component fall back to WeatherAmbienceController's silent default.
    public sealed class RoomWeatherProfile : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.State ambientState = new();
        [SerializeField] private float          insideStormForce;
        [SerializeField] private float          outsideStormForce;

        public AK.Wwise.State AmbientState      => this.ambientState;
        public float          InsideStormForce  => this.insideStormForce;
        public float          OutsideStormForce => this.outsideStormForce;
    }
}
```

- [ ] **Step 2: Create WeatherAmbienceController.cs**

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
    public sealed class WeatherAmbienceController : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private AK.Wwise.Event weatherEvent          = new();
        [SerializeField] private AK.Wwise.RTPC  insideStormForceRtpc  = new();
        [SerializeField] private AK.Wwise.RTPC  outsideStormForceRtpc = new();
        [SerializeField] private AK.Wwise.State defaultAmbientState   = new();

        [Inject] private IRoomOrchestrator                  roomOrchestrator           = null!;
        [Inject] private ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber = null!;

        private IDisposable? subscription;

        void IInitializable.Initialize()
        {
            this.subscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);

            this.weatherEvent.Post(gameObject);
            ApplyRoom(this.roomOrchestrator.CurrentRoom);
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

        void IDisposable.Dispose() => this.subscription?.Dispose();
    }
}
```

`RoomTransitionedEvent` lives in the parent `CrimsonDraft.Navigation` namespace. C# does not implicitly expose a parent namespace's types to a child namespace, so the `using CrimsonDraft.Navigation;` above is required — `IRoomOrchestrator` and `RoomController` need no such import since they're already declared directly in `CrimsonDraft.Navigation.Rooms`, the namespace this file is in.

- [ ] **Step 3: Register in NavigationScope**

Edit `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`, immediately after the existing `MapStateTracker` line (currently line 114):

```csharp
            builder.Register<MapStateTracker>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<WeatherAmbienceController>().AsImplementedInterfaces();
```

- [ ] **Step 4: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` (compile: request, mode: force) to force a domain reload, then `mcp__UnityMCP__read_console` filtered to errors. Expected: no errors referencing `RoomWeatherProfile`, `WeatherAmbienceController`, or `NavigationScope`.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomWeatherProfile.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/WeatherAmbienceController.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(audio): add RoomWeatherProfile and WeatherAmbienceController for storm ambience"
```

---

### Task 2: Add WeatherAmbienceController to the Navigation scene and assign its Wwise Picker fields

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity`

`Navigation.unity` is the scene that hosts `NavigationScope` and `NavigationCameraRegistrar` (confirmed by grep — `Deck_B_Development.unity` holds only room content, no scope). `WeatherAmbienceController` must live in a scene that's loaded whenever `NavigationScope.Configure` runs, so it goes here, not in the level-content scene.

- [ ] **Step 1: Open the scene and create the GameObject**

Open `Assets/Scenes/Production/Navigation.unity`. Create an empty GameObject at the root of the Hierarchy named `WeatherAmbience`, and add the `Weather Ambience Controller` component to it.

- [ ] **Step 2: Assign the Event field**

In the Inspector, click the picker circle next to `Weather Event` and select the event **`Play_WeatherBC`**.

- [ ] **Step 3: Assign the Inside Storm Force RTPC**

Click the picker circle next to `Inside Storm Force Rtpc` and select the game parameter **`InsideStormForce`**.

- [ ] **Step 4: Assign the Outside Storm Force RTPC**

Click the picker circle next to `Outside Storm Force Rtpc` and select the game parameter **`OutsideStormForce`**.

- [ ] **Step 5: Assign the default ambient State**

Click the picker circle next to `Default Ambient State` and select **`Ambients/None`**.

- [ ] **Step 6: Save and verify the serialized values**

Save the scene (`mcp__UnityMCP__manage_scene` save action). Inspect the diff on `Navigation.unity`: the new `WeatherAmbienceController` MonoBehaviour block should show non-empty `weatherEvent`, `insideStormForceRtpc`, `outsideStormForceRtpc`, `defaultAmbientState` entries with real `idInternal`/`groupIdInternal`/`WwiseObjectReference` values, not the default-constructed empty ones (same shape check used for `DoorTransitionController`'s fields — see `docs/superpowers/plans/2026-07-12-door-transition-sfx.md` Task 3, Step 5).

- [ ] **Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity
git add Game/CrimsonDraft/Assets/Wwise/ScriptableObjects
git commit -m "content(audio): add WeatherAmbienceController to Navigation scene"
```

(The second `git add` picks up any new/regenerated Wwise ScriptableObject reference assets created by the picker — run `git status` first to confirm what actually changed before committing.)

---

### Task 3: Add RoomWeatherProfile to the Balcony room and assign its values

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Deck_B_Development.unity`

- [ ] **Step 1: Locate the Balcony room GameObject**

Use `mcp__UnityMCP__find_gameobjects` (or the Hierarchy search) in `Deck_B_Development.unity` to find the `RoomController` whose `Room Id` field is `Balcony` (the scene has many `RoomController` instances — search by component type, then use `mcp__UnityMCP__manage_gameobject`/Inspector to read each candidate's `Room Id` until the Balcony one is found; the Balcony_A room prefab under `Assets/Prefabs/World/Rooms/Deck_B/Balcony_A.prefab` is the geometry for this room and is a useful landmark for narrowing the search).

- [ ] **Step 2: Add the RoomWeatherProfile component**

On that same GameObject (the one holding `RoomController`), add the `Room Weather Profile` component.

- [ ] **Step 3: Assign the ambient State**

Click the picker circle next to `Ambient State` and select **`Ambients/Balcony`**.

- [ ] **Step 4: Set starting RTPC values**

Set `Inside Storm Force` to `0` and `Outside Storm Force` to `100` — Balcony is directly exposed to the storm (no muffling), so the exterior RTPC should read at full and the interior one at zero as a starting point. These are audio-authoring values, not fixed constants: after Task 4's Play Mode check, adjust them in the Inspector while listening until the mix sounds right, and re-save.

- [ ] **Step 5: Save and verify**

Save the scene. Confirm via `git diff` that only the Balcony room's GameObject block changed (new `RoomWeatherProfile` component with the assigned State reference and the two float values) — no unrelated scene churn.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/Deck_B_Development.unity
git add Game/CrimsonDraft/Assets/Wwise/ScriptableObjects
git commit -m "content(audio): add Balcony RoomWeatherProfile (Ambients:Balcony, exterior storm exposure)"
```

---

### Task 4: End-to-end verification in Play Mode

**Files:** none (verification only)

- [ ] **Step 1: Verify the silent default on scene start**

Enter Play Mode with `Navigation.unity` + `Deck_B_Development.unity` loaded, starting in a room other than Balcony (or whatever the configured starting room is). Confirm via the Wwise Profiler (Game Object 3D Viewer / RTPC monitor) or a temporary `Debug.Log` that:
- `Play_WeatherBC` posts exactly once, at startup.
- `Ambients` State reads `None`.
- Both `InsideStormForce` and `OutsideStormForce` read `0`.

- [ ] **Step 2: Verify Balcony applies its profile**

Walk the player into the Balcony room. Confirm:
- `Ambients` State switches to `Balcony`.
- `OutsideStormForce` reads `100` (or whatever value Task 3 Step 4 landed on) and `InsideStormForce` reads `0`.
- The storm/rain ambience is now audible and sounds like direct outdoor exposure, not muffled.

- [ ] **Step 3: Verify leaving Balcony falls back to silence**

Walk the player out of Balcony into any room without a `RoomWeatherProfile`. Confirm:
- `Ambients` State resets to `None`.
- Both RTPCs reset to `0`.
- No second `Play_WeatherBC` post occurs (the event should still show only the one post from Step 1 — it's never re-posted or stopped).

- [ ] **Step 4: Report results**

Summarize pass/fail for all three checks. If any check fails, stop and debug before considering this plan complete — do not report success without having observed the Wwise State/RTPC values (or logs) directly.
