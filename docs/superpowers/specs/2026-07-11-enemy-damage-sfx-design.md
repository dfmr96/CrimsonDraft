# Enemy Damage SFX — Design Spec

**Date:** 2026-07-11
**Status:** Approved
**Scope:** Combat/Audio — playing `Play_Zombie_Damage` when the enemy's flinch animation reacts to being hit

---

## Overview

The enemy flinch animation feature (already implemented) triggers `Hit1`/`Hit2` on the enemy's `Enemy_Combat_Controller` in sync with each bullet that hits, alternating between the `Armature|Hit_1` and `Armature|Hit_2` clips. This spec adds the `Play_Zombie_Damage` Wwise event as a reaction to that same animation — playing whenever a `Hit_1`/`Hit_2` clip actually plays.

Rejected alternative: giving `BattlefieldView` its own `AK.Wwise.Event` field and posting it directly (mirroring the flinch trigger call). This was rejected because audio for the enemy's own reaction is not `BattlefieldView`'s responsibility — `BattlefieldView` already orchestrates spawn, damage numbers, indicators, and two other actors' Animators; adding a third concern (enemy audio) grows a file that already does a lot, and it would need to reach into the enemy (`GetComponentInChildren`) to post correctly-spatialized audio anyway.

Instead, this follows the same pattern already established by `FootstepController` (`Assets/Scripts/Audio/FootstepController.cs`): a small MonoBehaviour lives on the character prefab, exposes a public method, and that method is wired as a **Unity Animation Event** directly on the animation clip. The enemy's own flinch animation is its reaction to being hit; the sound is part of that reaction, self-contained in the prefab. `BattlefieldView` needs no changes at all — it already triggers `Hit1`/`Hit2`, and everything downstream of that trigger now includes audio without `BattlefieldView` knowing audio exists.

---

## `EnemyCombatAudio` component

New file: `Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs`, namespace `CrimsonDraft.Audio` (co-located with `FootstepController`, which lives in the same namespace despite being player-specific — precedent in this codebase is "audio components live in `Assets/Scripts/Audio/`, grouped by concern, not by feature").

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class EnemyCombatAudio : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Event damageEvent = new();

        // Called by Animation Event on the Hit_1 / Hit_2 clips.
        public void PlayDamageSfx() => this.damageEvent.Post(gameObject);
    }
}
```

This mirrors `FootstepController.OnWalkStep()`/`OnRunStep()` exactly: a public parameterless method, posted on `gameObject` for correct 3D spatialization, called by an Animation Event rather than by any other script. No dependencies, no VContainer `[Inject]`, no MessagePipe — consistent with `FootstepController`, which also has none of these.

---

## Content wiring (not code — done directly in the Unity Editor)

1. Add `EnemyCombatAudio` component to `Game/CrimsonDraft/Assets/Prefabs/Enemies/EnemyCombatModel.prefab` (the shared battlefield prefab referenced by both `Enemy_Grunt.asset` and `Enemy_Heavy.asset`), and assign the `Play_Zombie_Damage` Wwise event to its `damageEvent` field.
2. On the `Armature|Hit_1` and `Armature|Hit_2` animation clips (used by `Enemy_Combat_Controller.controller`), add a Unity Animation Event that calls `EnemyCombatAudio.PlayDamageSfx()`, placed at whatever frame reads as the moment of impact in each clip.

Both clips need their own Animation Event (there are two separate clips, each needs the callback placed individually) — this is a one-time authoring step per clip, not something code can do.

---

## Why this satisfies "the enemy should know it was damaged and react to it"

The enemy's own animation state machine is what "knows" it was hit — that's the entire reason the flinch trigger fires. Wiring the sound as an Animation Event on that same reaction means the audio literally comes from the enemy reacting, not from an external system reaching in and commanding a specific component. `BattlefieldView` (or anything else) never needs to know `EnemyCombatAudio` exists, hold a reference to it, or call it — it only ever talks to the `Animator`, exactly as it already does today.

---

## Out of scope

- No change to `BattlefieldView.cs`, `IBattlefieldView.cs`, or `AimingState.cs` — this feature is entirely new content plus one new, dependency-free component.
- No handling for `Enemy_Grunt` vs. `Enemy_Heavy` having different damage sounds — both currently share `EnemyCombatModel.prefab`, so they'd share the same `EnemyCombatAudio` instance and event. If a future enemy type needs a different damage sound, it gets its own prefab (or its own `EnemyCombatAudio` instance with a different assigned event) — no code changes needed for that either, since the event is a per-instance serialized field, not a shared constant.
- No automated test — this is a content-adjacent MonoBehaviour with no branching logic, consistent with `FootstepController` and `InventorySoundManager`, neither of which have EditMode tests.
