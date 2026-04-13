# Wwise Footstep System Design

**Date:** 2026-04-13
**Status:** Approved
**Topic:** Footstep audio system integrating Wwise with HorrorEngine surface detection

---

## Problem

CrimsonDraft has no audio system. Footstep sounds must vary by surface type (metal, wood, concrete, water) and play in sync with animation. The detection mechanism must be efficient — surface type only changes when the player crosses a zone boundary, so polling every frame is wasteful.

---

## Decision: Lazy Detection at Footstep Moment

Surface detection runs **only when a footstep event fires**, not on a continuous Update or FixedUpdate poll.

> Rationale: The only consumer of surface type is the footstep sound. The surface only needs to be known at the moment of foot contact. A raycast at that exact moment is both sufficient and minimal.

This replaces the FixedUpdate polling approach previously planned in `iterative-enchanting-bengio.md`.

---

## Approaches Considered

| Approach | Overhead | Complexity | Chosen |
|---|---|---|---|
| A. Lazy raycast at footstep moment | 2 raycasts/walk cycle | Minimal | Yes |
| B. OnCollisionEnter + normal filter | 0 in steady state | Requires normal filtering, fragile at seams | No |
| C. FixedUpdate raycast | 50 raycasts/second | Minimal | No |

---

## Components

### Unity Layer Setup

- **`Floor` layer** — assigned to all floor GameObjects
- `GroundDetector.m_GroundCheckLayerMask` is set to `Floor` exclusively
- Player CapsuleCollider lives in `Player` or `Default` layer — never `Floor`
- This ensures the raycast only hits floor colliders, not walls, ceilings, or the player itself

### Player Prefab — root GameObject

All components added to the same root that holds `PlayerController`:

| Component | Source | Key Settings |
|---|---|---|
| `AkGameObj` | Wwise | default — registers GO with sound engine |
| `GroundDetector` | HorrorEngine | OffsetUp=0.1, Distance=0.3, LayerMask=Floor |
| `SurfaceDetector` | HorrorEngine | DefaultSurface=ST_Metal |
| `FootstepController` | CrimsonDraft.Audio | see below |

`SurfaceDetector.Awake()` self-subscribes to `GroundDetector.OnGroundChanged` via `GetComponentInParent<GroundDetector>()`. No manual wiring needed.

### FootstepController

MonoBehaviour on the Player root. Receives Animation Events from the walk and run clips. On each event:

1. **Motion guard** — if `rb.linearVelocity.sqrMagnitude < threshold`, return immediately. Prevents ghost sounds from stale animation events during deceleration. Threshold default: `0.05` (≈ 0.22 m/s).
2. **Surface detect** — calls `groundDet.Detect(transform.position)`. If the collider under the player changed, `SurfaceDetector.CurrentSurface` is updated via `OnGroundChanged`.
3. **Set Wwise switch** — resolves `CurrentSurface` to a switch state string via `SurfaceTypeMapping`, calls `AkSoundEngine.SetSwitch("SurfaceType", state, gameObject)`.
4. **Post event** — calls `walkEvent.Post(gameObject)` or `runEvent.Post(gameObject)`.

Pseudocode:
```
OnWalkStep():
    if velocity.sqrMagnitude < threshold → return
    groundDet.Detect(position)
    state = mapping.Resolve(surfaceDet.CurrentSurface)
    SetSwitch("SurfaceType", state, gameObject)
    walkEvent.Post(gameObject)

OnRunStep():
    same, using runEvent
```

Fields (serialized):
- `walkEvent: AK.Wwise.Event`
- `runEvent: AK.Wwise.Event`
- `switchGroup: string` = `"SurfaceType"`
- `mapping: SurfaceTypeMapping`
- `surfaceDet: SurfaceDetector`
- `groundDet: GroundDetector`
- `rb: Rigidbody`
- `minSpeedSqr: float` = `0.05`

### SurfaceTypeMapping

ScriptableObject. Maps `SurfaceType` (ScriptableObject identity asset) → Wwise switch state string.

Fields:
- `entries: Entry[]` — each entry: `SurfaceType` asset + `string WwiseSwitchState`
- `fallbackState: string` = `"Metal"`

`Resolve(SurfaceType surface)` — returns matching state string, or `fallbackState` if surface is null or not found.

Asset location: `Assets/ScriptableObjects/Audio/SurfaceTypeMapping.asset`

---

## Surface Assets

### SurfaceType ScriptableObjects

Location: `Assets/ScriptableObjects/Audio/Surfaces/`

| Asset | Wwise switch state |
|---|---|
| `ST_Metal.asset` | `Metal` |
| `ST_Wood.asset` | `Wood` |
| `ST_Concrete.asset` | `Concrete` |
| `ST_Water.asset` | `Water` |

### Scene Floor Setup

Each floor collider GameObject:
- Assigned to the `Floor` layer
- Has a `Surface` MonoBehaviour with its `SurfaceType` assigned
- Floors without a `Surface` component fall back to `Metal` (default)

---

## Animation Events

Added in the Unity **Model Importer > Animation tab** on each FBX. Not in the Animator Controller. Function target is `OnWalkStep` / `OnRunStep` on `FootstepController`.

`SendMessage` propagates the event from the Animator (child GO) to `FootstepController` on the root. No extra wiring needed.

| FBX | Clip | Method | Approx. normalised times |
|---|---|---|---|
| `HumanoidBase_Overlapping@Walking.fbx` | Walking | `OnWalkStep` | ~0.10 (left foot), ~0.60 (right foot) |
| `HumanoidBase_Overlapping@Running (1).fbx` | Running | `OnRunStep` | ~0.10 (left foot), ~0.55 (right foot) |

Times are approximate — scrub animation preview in Importer to find exact heel-contact frames. Adjust until audio lands at moment of foot impact.

---

## Wwise Project

### Switch Group: `SurfaceType`

| Switch Value | Usage | Default |
|---|---|---|
| `Metal` | Ship steel decks | Yes |
| `Wood` | Wood planking, crates | No |
| `Concrete` | Port / dock sections | No |
| `Water` | Flooded compartments | No |

### Events

| Event | Structure |
|---|---|
| `Play_Footstep_Walk` | Sound SFX → Switch Container [SurfaceType] → Random Container (3–5 clips) per switch |
| `Play_Footstep_Run` | Same structure, different audio files |

### SoundBank

`SB_Player` — contains both events. Loaded for the entire Navigation scene lifetime via an `AkBank` component in the scene. Must be loaded before any footstep event fires.

---

## New Code Files

Assembly: `CrimsonDraft.Audio` (`Assets/Scripts/Audio/CrimsonDraft.Audio.asmdef`)
- References: `AK.Wwise.Unity.API`, `AK.Wwise.Unity.API.WwiseTypes`
- `autoReferenced: false`
- HorrorEngine compiles into the default assembly — reachable at runtime via component references, no asmdef reference needed

Files:
- `Assets/Scripts/Audio/CrimsonDraft.Audio.asmdef`
- `Assets/Scripts/Audio/SurfaceTypeMapping.cs`
- `Assets/Scripts/Audio/FootstepController.cs`

---

## Verification

1. **Compile** — no errors; asmdef references valid Wwise assemblies
2. **Wwise Profiler** — walk over metal floor → `Play_Footstep_Walk` fires once per foot contact, Switch Container selects `Metal` branch
3. **Surface change** — walk from metal to wood → switch state changes to `Wood` before next Post
4. **Motion guard** — stand still with Speed animator param > 0 → no events fire (velocity near zero)
5. **Fallback** — walk over floor with no `Surface` component → Metal sound plays, no exceptions in Console
6. **SoundBank** — `SB_Player` visible in Wwise Profiler > Soundbanks before first footstep fires
7. **Layer isolation** — raycast does not hit walls, ceilings, or player (verified via Gizmos in editor)
