# Wwise Footstep System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** [2026-04-13-wwise-footstep-system-design.md](../specs/2026-04-13-wwise-footstep-system-design.md)

**Goal:** Footstep sounds vary by floor surface type, triggered by Animation Events, using lazy raycast detection only at the moment each footstep fires.

**Architecture:** `FootstepController` on the Player root receives Animation Events from walk/run clips. On each event it runs a single downward raycast (via HorrorEngine's `GroundDetector`), resolves the surface type to a Wwise switch state via `SurfaceTypeMapping`, and posts the Wwise event. No Update/FixedUpdate polling.

**Tech Stack:** Unity 2022+, Wwise Unity Integration, HorrorEngine (default Assembly-CSharp), C# 9

> **Note on assembly:** `FootstepController` holds serialized references to HorrorEngine types (`SurfaceDetector`, `GroundDetector`) which live in Assembly-CSharp (no asmdef). Because Unity asmdef assemblies cannot reference Assembly-CSharp, both new scripts compile into Assembly-CSharp as well — no `.asmdef` file is created for the Audio folder. Wwise assemblies (`AK.Wwise.Unity.API`, `AK.Wwise.Unity.API.WwiseTypes`) are `autoReferenced: true` and are therefore visible from Assembly-CSharp automatically.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `Game/CrimsonDraft/Assets/Scripts/Audio/SurfaceTypeMapping.cs` | ScriptableObject: maps SurfaceType asset → Wwise switch state string |
| Create | `Game/CrimsonDraft/Assets/Scripts/Audio/FootstepController.cs` | MonoBehaviour: Animation Event receiver, lazy detect, Wwise post |
| Modify | `Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab` | Add AkGameObj, GroundDetector, SurfaceDetector, FootstepController |
| Modify | `Game/CrimsonDraft/Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Walking.fbx` | Add Animation Events OnWalkStep |
| Modify | `Game/CrimsonDraft/Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Running (1).fbx` | Add Animation Events OnRunStep |
| Create (Editor) | `Assets/ScriptableObjects/Audio/Surfaces/ST_Metal.asset` | SurfaceType identity asset |
| Create (Editor) | `Assets/ScriptableObjects/Audio/Surfaces/ST_Wood.asset` | SurfaceType identity asset |
| Create (Editor) | `Assets/ScriptableObjects/Audio/Surfaces/ST_Concrete.asset` | SurfaceType identity asset |
| Create (Editor) | `Assets/ScriptableObjects/Audio/Surfaces/ST_Water.asset` | SurfaceType identity asset |
| Create (Editor) | `Assets/ScriptableObjects/Audio/SurfaceTypeMapping.asset` | Mapping asset wired with 4 entries |

---

## Task 1: Floor layer

- [ ] **Open Unity** → `Edit > Project Settings > Tags and Layers`
- [ ] In the **Layers** section, find the first empty slot (User Layer 8 or higher) and type `Floor`
- [ ] Click outside the field to confirm. Note the layer index (e.g., Layer 8)
- [ ] **Commit**

```bash
cd "d:/Proyectos Unity/CrimsonDraft/CrimsonDraft"
git add "Game/CrimsonDraft/ProjectSettings/TagManager.asset"
git commit -m "feat(audio): add Floor physics layer"
```

---

## Task 2: SurfaceType ScriptableObject assets

These are empty marker assets — identity tags used as dictionary keys.

- [ ] In Unity Project window, navigate to `Assets/ScriptableObjects/`. If an `Audio/` folder doesn't exist, create it. Inside `Audio/`, create `Surfaces/`.
- [ ] Right-click `Assets/ScriptableObjects/Audio/Surfaces/` → `Create > Horror Engine > Surfaces > Surface Type`
- [ ] Name it `ST_Metal`
- [ ] Repeat for `ST_Wood`, `ST_Concrete`, `ST_Water`
- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/ScriptableObjects/"
git commit -m "feat(audio): add SurfaceType assets (Metal, Wood, Concrete, Water)"
```

---

## Task 3: SurfaceTypeMapping script

- [ ] Create folder `Game/CrimsonDraft/Assets/Scripts/Audio/` if it doesn't exist
- [ ] Create `SurfaceTypeMapping.cs` with the following content:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace CrimsonDraft.Audio
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Audio/Surface Type Mapping")]
    public sealed class SurfaceTypeMapping : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SurfaceType SurfaceType;
            public string      WwiseSwitchState;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private string  fallbackState = "Metal";

        private Dictionary<SurfaceType, string>? lookup;

        private void OnEnable()
        {
            lookup = new Dictionary<SurfaceType, string>(entries.Length);
            foreach (var e in entries)
            {
                if (e.SurfaceType != null)
                    lookup[e.SurfaceType] = e.WwiseSwitchState;
            }
        }

        public string Resolve(SurfaceType? surface)
        {
            if (surface != null && lookup != null && lookup.TryGetValue(surface, out var state))
                return state;
            return fallbackState;
        }
    }
}
```

- [ ] Verify Unity Console shows no compile errors
- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Audio/SurfaceTypeMapping.cs"
git commit -m "feat(audio): add SurfaceTypeMapping ScriptableObject"
```

---

## Task 4: FootstepController script

- [ ] Create `Game/CrimsonDraft/Assets/Scripts/Audio/FootstepController.cs`:

```csharp
#nullable enable

using AK.Wwise;
using HorrorEngine;
using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class FootstepController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event walkEvent  = new AK.Wwise.Event();
        [SerializeField] private AK.Wwise.Event runEvent   = new AK.Wwise.Event();
        [SerializeField] private string         switchGroup = "SurfaceType";

        [Header("Surface")]
        [SerializeField] private SurfaceTypeMapping mapping    = null!;
        [SerializeField] private SurfaceDetector    surfaceDet = null!;
        [SerializeField] private GroundDetector     groundDet  = null!;

        [Header("Motion Guard")]
        [SerializeField] private Rigidbody rb          = null!;
        [SerializeField] private float     minSpeedSqr = 0.05f;

        // Called by Animation Event on walk clip (left and right foot contacts)
        public void OnWalkStep()
        {
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(walkEvent);
        }

        // Called by Animation Event on run clip (left and right foot contacts)
        public void OnRunStep()
        {
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(runEvent);
        }

        private void DetectAndPost(AK.Wwise.Event wwiseEvent)
        {
            groundDet.Detect(transform.position);
            var state = mapping.Resolve(surfaceDet.CurrentSurface);
            AkSoundEngine.SetSwitch(switchGroup, state, gameObject);
            wwiseEvent.Post(gameObject);
        }
    }
}
```

- [ ] Verify Unity Console shows no compile errors
- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Audio/FootstepController.cs"
git commit -m "feat(audio): add FootstepController with lazy surface detection"
```

---

## Task 5: SurfaceTypeMapping asset

- [ ] Right-click `Assets/ScriptableObjects/Audio/` → `Create > CrimsonDraft > Audio > Surface Type Mapping`
- [ ] Name it `SurfaceTypeMapping`
- [ ] In the Inspector, set **Fallback State** to `Metal`
- [ ] Set **Entries** size to `4`
- [ ] Fill each entry:
  | Index | Surface Type | Wwise Switch State |
  |---|---|---|
  | 0 | ST_Metal | Metal |
  | 1 | ST_Wood | Wood |
  | 2 | ST_Concrete | Concrete |
  | 3 | ST_Water | Water |
- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/ScriptableObjects/Audio/SurfaceTypeMapping.asset"
git add "Game/CrimsonDraft/Assets/ScriptableObjects/Audio/SurfaceTypeMapping.asset.meta"
git commit -m "feat(audio): create SurfaceTypeMapping asset with 4 surface entries"
```

---

## Task 6: Configure Player prefab

- [ ] Open `Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab` in Prefab Mode (double-click)
- [ ] Select the **root** GameObject (the one with `PlayerController`, `Rigidbody`, `CapsuleCollider`)
- [ ] Add component: **AkGameObj** (`Add Component > Wwise > Ak Game Obj`)
  - Leave all settings at default
- [ ] Add component: **GroundDetector** (`Add Component > Horror Engine > Ground Detector` or search)
  - `Offset Up`: `0.1`
  - `Distance`: `0.3`
  - `Ground Check Layer Mask`: set to **Floor** (the layer created in Task 1)
- [ ] Add component: **SurfaceDetector** (`Add Component > search SurfaceDetector`)
  - `Default Surface`: drag `ST_Metal.asset` from `Assets/ScriptableObjects/Audio/Surfaces/`
- [ ] Add component: **FootstepController** (`Add Component > search FootstepController`)
  - `Walk Event`: click the picker, select `Play_Footstep_Walk` from Wwise project *(add after Wwise setup in Task 8)*
  - `Run Event`: click the picker, select `Play_Footstep_Run` *(add after Task 8)*
  - `Switch Group`: `SurfaceType` (already default)
  - `Mapping`: drag `SurfaceTypeMapping.asset`
  - `Surface Det`: drag the `SurfaceDetector` component from this same root GO
  - `Ground Det`: drag the `GroundDetector` component from this same root GO
  - `Rb`: drag the `Rigidbody` component from this same root GO
  - `Min Speed Sqr`: `0.05` (default)
- [ ] Save prefab (`Ctrl+S`)
- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab"
git commit -m "feat(audio): add AkGameObj, GroundDetector, SurfaceDetector, FootstepController to Player prefab"
```

---

## Task 7: Animation Events on FBX clips

Animation Events must be added via the **Model Importer**, not the Animator Controller. The FBX clips are read-only; events are stored in the import settings.

### Walk clip

- [ ] In Project window, select `Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Walking.fbx`
- [ ] In Inspector, click the **Animation** tab
- [ ] Under **Clips**, select the `Walking` clip
- [ ] Scroll down to the **Events** foldout — click the **+** button
- [ ] Set the event:
  - `Time`: scrub the animation preview to find the exact frame where the **left heel** contacts the ground. Convert to normalised time: `frame / totalFrames`. Start with `0.10` and adjust.
  - `Function`: `OnWalkStep`
  - Leave all parameters empty (int=0, float=0, string="", object=None)
- [ ] Click **+** again for the second event:
  - `Time`: right heel contact. Start with `0.60`.
  - `Function`: `OnWalkStep`
- [ ] Click **Apply** at the bottom of the Inspector
- [ ] Enter Play Mode and walk the character — listen for audio sync. If early → increase normalised time. If late → decrease. Adjust until the sound lands exactly at foot impact.

### Run clip

- [ ] Select `Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Running (1).fbx`
- [ ] Animation tab → `Running (1)` clip → Events foldout
- [ ] Add two events:
  - Left heel: `Time` ≈ `0.10`, `Function`: `OnRunStep`
  - Right heel: `Time` ≈ `0.55`, `Function`: `OnRunStep`
- [ ] Click **Apply**
- [ ] Enter Play Mode, sprint, listen and adjust times as needed

- [ ] **Commit** when timing is tuned

```bash
git add "Game/CrimsonDraft/Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Walking.fbx.meta"
git add "Game/CrimsonDraft/Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Running (1).fbx.meta"
git commit -m "feat(audio): add footstep Animation Events to walk and run FBX clips"
```

---

## Task 8: Wwise project setup

This task is performed in the **Wwise Authoring Tool** (separate application), not in Unity.

### Switch Group

- [ ] Open the Wwise project at `Game/CrimsonDraft/CrimsonDraft_WwiseProject/`
- [ ] In Project Explorer → **Game Syncs** → right-click **Switches** → `New Child > Switch Group`
- [ ] Name it `SurfaceType`
- [ ] Right-click `SurfaceType` → `New Child > Switch`:
  - `Metal` — right-click → `Set as Default`
  - `Wood`
  - `Concrete`
  - `Water`

### Events and Sound Objects

For **`Play_Footstep_Walk`**:
- [ ] In Project Explorer → **Actor-Mixer Hierarchy** → right-click → `New Child > Sound SFX`
- [ ] Name it `SFX_Footstep_Walk`
- [ ] Inside `SFX_Footstep_Walk`, create a **Switch Container**:
  - Right-click → `New Child > Switch Container`
  - In Properties, set Switch Group to `SurfaceType`
- [ ] Inside the Switch Container, create four **Random Containers** (one per switch value):
  - Right-click → `New Child > Random Container` → name `Walk_Metal`
  - Repeat for `Walk_Wood`, `Walk_Concrete`, `Walk_Water`
  - Select each random container → in Switch Assignments panel, assign it to the corresponding switch value
- [ ] Import audio files (3–5 WAV clips per surface type) by dragging into each Random Container
- [ ] In Project Explorer → **Events** → right-click → `New Child > Event` → name `Play_Footstep_Walk`
- [ ] Add action: Play → target `SFX_Footstep_Walk`

For **`Play_Footstep_Run`**:
- [ ] Repeat the above structure. Name the sound object `SFX_Footstep_Run`, use different audio files (sharper, faster impacts)
- [ ] Create event `Play_Footstep_Run`

### SoundBank

- [ ] Project Explorer → **SoundBanks** → right-click → `New Child > SoundBank`
- [ ] Name it `SB_Player`
- [ ] Add both events: drag `Play_Footstep_Walk` and `Play_Footstep_Run` into `SB_Player`
- [ ] **Generate SoundBank** (`Project > Generate SoundBanks > Generate All`)
- [ ] Confirm generated files appear in Unity at `Assets/StreamingAssets/Audio/GeneratedSoundBanks/`

### Wire events in Unity

- [ ] Back in Unity, open `Player.prefab`
- [ ] Select root GO → `FootstepController` component
- [ ] `Walk Event`: click picker → select `Play_Footstep_Walk`
- [ ] `Run Event`: click picker → select `Play_Footstep_Run`
- [ ] Save prefab

### AkBank in Navigation scene

- [ ] Open the Navigation scene
- [ ] Create an empty GameObject named `AudioManager`
- [ ] Add component: **AkBank** (`Wwise > Ak Bank`)
- [ ] Set `Bank Name` to `SB_Player`
- [ ] `Load Type`: `Load on Awake` (checked), `Unload on Destroy` (checked)
- [ ] Save scene

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/StreamingAssets/"
git add "Game/CrimsonDraft/Assets/Scenes/"
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab"
git commit -m "feat(audio): Wwise soundbank SB_Player + AkBank in Navigation scene"
```

---

## Task 9: Scene floor setup

- [ ] Open the Navigation scene
- [ ] For each floor collider GameObject in the scene:
  1. In Inspector → **Layer** dropdown → select `Floor`
  2. `Add Component > Horror Engine > Surface` (search "Surface")
  3. Set `Type` to the appropriate `ST_*.asset`
- [ ] Floors with no `Surface` component will default to Metal — acceptable
- [ ] **Save scene**

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/"
git commit -m "feat(audio): assign Floor layer and Surface components to scene floors"
```

---

## Task 10: Verification

Run each check and confirm:

- [ ] **Compile** — Unity Console has zero errors after all scripts are saved
- [ ] **Layer isolation** — Select `Player.prefab` root in Play Mode. In Scene view, enable Gizmos. `GroundDetector` draws a red line downward. Confirm it only points at floor colliders, not walls.
- [ ] **SoundBank loaded** — In Play Mode with Wwise Profiler connected (`Wwise menu > Connect to Application`): navigate to Profiler > SoundBanks tab. Confirm `SB_Player` is listed before any movement.
- [ ] **Metal footstep** — Walk player over a metal floor collider. Profiler Capture log shows `Play_Footstep_Walk` fires twice per walk cycle. Switch Container selected `Metal` branch.
- [ ] **Surface change** — Walk from metal floor to wood floor. Confirm switch state changes to `Wood` before the next Post (check Profiler capture).
- [ ] **Motion guard** — Stand still. Force `Speed` animator param to `0.5` manually via Animator window. Confirm zero events in Profiler (velocity is zero because `PlayerController.FixedUpdate` zeroes it when input is zero).
- [ ] **Fallback** — Remove `Surface` component from one floor temporarily. Walk over it. Confirm `Metal` sound plays, no NullReferenceExceptions in Console. Restore component.
- [ ] **Run footsteps** — Sprint. Confirm `Play_Footstep_Run` fires instead of walk event, timing matches foot contacts.

- [ ] **Commit** if any timing adjustments were made in this step

```bash
git add -A
git commit -m "feat(audio): footstep system verified — all checks pass"
```
