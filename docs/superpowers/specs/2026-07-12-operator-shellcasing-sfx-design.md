# Operator Shell Casing SFX — Design Spec

**Date:** 2026-07-12
**Status:** Approved
**Scope:** Combat/Audio — playing `Play_ShellCasing` on the operator's shoot animation, shortly after the gunfire sound

---

## Overview

`OperatorCombatAudio` (already implemented) posts `Play_FireGunsSC` via an Animation Event on the `ShootPistolFlexed2` clip. This spec adds a second, independent reaction on the same clip: `Play_ShellCasing`, fired at a later frame (the casing ejects shortly after the shot, not simultaneously with it).

This is a second Animation Event calling a second method — not a second call inside `PlayFireGunSfx()` — because the two sounds happen at different points in the clip's timeline, and Unity's Animation Event model is one function call per specific frame.

---

## `OperatorCombatAudio` changes

```csharp
[SerializeField] private AK.Wwise.Event shellCasingEvent = new();

// Called by Animation Event on the ShootPistolFlexed2 clip, later than the muzzle-flash event.
public void PlayShellCasingSfx() => this.shellCasingEvent.Post(gameObject);
```

No `GunType` switching needed here (unlike `PlayFireGunSfx`) — the spec doesn't call for a per-weapon shell-casing sound variant, just a single event posted on the operator's own GameObject for correct 3D spatialization, matching the simplest existing pattern (`FootstepController`, `EnemyCombatAudio`).

## Content wiring (human, not code)

1. The human assigns `Play_ShellCasing` to the new `shellCasingEvent` field on each of the four operator battlefield prefabs' `OperatorCombatAudio` components, once the event is recognized on the Wwise/Unity side (currently blocked by the same recurring SoundBank-recognition gap seen with prior events — not something this task can resolve).
2. Add a second Animation Event on `ShootPistolFlexed2` calling `PlayShellCasingSfx`, placed at a frame later than the existing `PlayFireGunSfx` event (the casing ejects after the shot fires), via the same `ModelImporter.clipAnimations` technique already used for the prior two SFX features.

## Out of scope

- No `GunType`-based switching for the shell casing sound.
- No change to `BattlefieldView.cs` — this is entirely self-contained within `OperatorCombatAudio` and content wiring, same as `PlayFireGunSfx`.
