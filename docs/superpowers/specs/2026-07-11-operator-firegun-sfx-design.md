# Operator Fire-Gun SFX — Design Spec

**Date:** 2026-07-11
**Status:** Approved
**Scope:** Combat/Audio — playing `Play_FireGunsSC` (switched by `GunType`) when the operator's shoot animation fires

---

## Overview

The operator shoot burst feature (already implemented) triggers `Shoot` on the operator's `Operator_Combat_Controller`, playing `ShootPistolFlexed2` once per bullet. This spec adds the `Play_FireGunsSC` Wwise event as a reaction to that same animation, switched by the `GunType` Wwise Switch Group (`Pistols` / `Shotgun` / `REPistols` / `REShotgun`) so the correct gunfire sound plays for whichever weapon the operator currently has equipped.

Unlike the enemy flinch/damage SFX (where the reaction needed no external data — the enemy's own Animator state was enough), the correct `GunType` switch value depends on which weapon the operator is currently carrying, and that data lives in `IOperatorRoster`/`OperatorRuntime` — a pure C# service, not anything already attached to the operator's battlefield prefab. So this feature needs one new piece of plumbing: the operator's battlefield prefab must be told, once, which roster slot it represents, so its own audio component can look up its own current weapon whenever it needs to — self-sufficient after that one-time bind, not re-told on every shot.

---

## `GunType` in code

New enum, `Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs`, matching the existing `Caliber.cs` file's style and namespace:

```csharp
#nullable enable

namespace CrimsonDraft.Operators
{
    public enum GunType
    {
        Pistols,
        Shotgun,
        REPistols,
        REShotgun,
    }
}
```

Values match the Wwise `GunType` Switch Group's four switches exactly (`Pistols`, `Shotgun`, `REPistols`, `REShotgun` — verified against `CrimsonDraft_WwiseProject/Switches/Default Work Unit.wwu`).

### `WeaponData`

Gains a new serialized field, assigned by hand per weapon asset (analogous to the existing `caliber` field):

```csharp
[SerializeField] private GunType gunType = GunType.Pistols;
public GunType GunType => this.gunType;
```

### `IWeaponSlot` / `WeaponItem`

`IWeaponSlot` (`Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`) gains one more property, alongside the existing `Caliber`/`BaseDamage`:

```csharp
GunType GunType { get; }
```

`WeaponItem` implements it the same way it implements `Caliber`:

```csharp
public GunType GunType => this.Data.GunType;
```

---

## `OperatorCombatAudio` component

New file: `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs`, namespace `CrimsonDraft.Audio` (co-located with `FootstepController`/`EnemyCombatAudio`).

```csharp
#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Audio
{
    public sealed class OperatorCombatAudio : MonoBehaviour
    {
        [Serializable]
        public struct GunTypeSwitchEntry
        {
            public GunType         GunType;
            public AK.Wwise.Switch WwiseSwitch;
        }

        [SerializeField] private AK.Wwise.Event      fireGunEvent    = new();
        [SerializeField] private GunTypeSwitchEntry[] gunTypeSwitches = Array.Empty<GunTypeSwitchEntry>();

        private IOperatorRoster? roster;
        private int              slotIndex = -1;

        public void Bind(IOperatorRoster roster, int slotIndex)
        {
            this.roster    = roster;
            this.slotIndex = slotIndex;
        }

        // Called by Animation Event on the ShootPistolFlexed2 clip.
        public void PlayFireGunSfx()
        {
            if (this.roster == null || this.roster.Count <= this.slotIndex || this.slotIndex < 0)
                return;

            var weapon = this.roster[this.slotIndex].ActiveWeapon;
            if (weapon == null)
                return;

            foreach (var entry in this.gunTypeSwitches)
            {
                if (entry.GunType == weapon.GunType)
                {
                    entry.WwiseSwitch.SetValue(gameObject);
                    break;
                }
            }

            this.fireGunEvent.Post(gameObject);
        }
    }
}
```

This mirrors `SurfaceTypeMapping`'s `Entry`/`AK.Wwise.Switch` pattern, inlined directly on the component instead of a separate mapping ScriptableObject — appropriate here since `GunType` is a small, fixed, code-defined enum (4 values), not an open-ended set of designer-created assets like `SurfaceType`.

If no matching entry is configured for the weapon's `GunType`, no switch is set (Wwise falls back to whatever the switch's default/last value is) but the event still posts — consistent with `FootstepController`'s pattern of always posting even if the switch resolution has a fallback path.

---

## `BattlefieldView` changes

`BattlefieldView` currently has zero injected dependencies. It gains:

```csharp
private IOperatorRoster? roster;

[Inject]
public void Construct(IOperatorRoster roster)
{
    this.roster = roster;
}
```

In the operator spawn loop inside `Populate()`, right after caching the operator's `Animator` (the existing `operatorAnimatorBySlot[i] = operatorAnimator;` line), add a one-time bind to the newly spawned prefab's audio component:

```csharp
var operatorAudio = go.GetComponentInChildren<OperatorCombatAudio>();
if (operatorAudio != null && this.roster != null)
    operatorAudio.Bind(this.roster, i);
```

This is the only touch point `BattlefieldView` has with this feature — one reference handoff at spawn time, not a per-shot push. After this, `OperatorCombatAudio` is self-sufficient: it looks up its own current weapon itself whenever its own Animation Event fires, exactly matching the "the prefab itself should know what weapon it has" requirement.

---

## Content wiring (not code — done directly in the Unity Editor)

1. Add `OperatorCombatAudio` component to each operator battlefield prefab that should fire this sound (`Ethan_Combat_FBX.prefab`, `RestPoseMarcusFBX.prefab`, `Lilou_Combat_FBX.prefab`, `Darius_Combat_FBX 1.prefab`), assign `Play_FireGunsSC` to `fireGunEvent`, and configure the four `gunTypeSwitches` entries against the `GunType` Wwise Switch Group's four switches.
2. Assign a `GunType` value on each `WeaponData` asset (the human decides which weapons map to `Pistols`/`Shotgun`/`REPistols`/`REShotgun`).
3. On the `ShootPistolFlexed2` clip (used by `Operator_Combat_Controller.controller`), add a Unity Animation Event calling `OperatorCombatAudio.PlayFireGunSfx()`, placed at the frame that reads as the muzzle-flash/firing moment.

---

## Out of scope

- No change to `AimingState.cs` or `IBattlefieldView.cs` — the burst-timing/flinch-sync logic already built is untouched; this feature only adds the one-time roster bind in `BattlefieldView.Populate()`.
- No handling for weapons with no `GunTypeSwitchEntry` configured beyond "switch not set, event still posts" (see above) — if this proves wrong in practice, it's a follow-up.
- No automated test — same rationale as `EnemyCombatAudio`: a content-adjacent MonoBehaviour with no branching logic beyond a small lookup, consistent with the file's other Animator/Wwise-driven components.
