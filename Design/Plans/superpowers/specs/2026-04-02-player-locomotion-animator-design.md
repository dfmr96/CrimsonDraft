# Player Locomotion Animator — Design Spec

**Date:** 2026-04-02
**Branch:** feature/migration-2d-to-3d
**Status:** Approved

## Overview

Replace the legacy 2D directional animator (`PlayerAnimator.controller`) with a 3D locomotion system that drives three animation states — Idle, Walk, Run — from a single `Speed` float parameter. Sprint is triggered by a dedicated button.

---

## 1. Animator Controller

**File:** `Assets/Animations/Player/PlayerAnimator.controller`
(replaces the existing 2D directional controller)

### Parameter

| Name | Type | Default |
|------|------|---------|
| `Speed` | Float | 0 |

### State Machine

Single state: `LocomotionBlend` (1D Blend Tree) — this is the default state.

### Blend Tree

| Threshold | State | FBX Source |
|-----------|-------|------------|
| 0.0 | Idle | `HumanoidBase_Overlapping@Breathing Idle.fbx` |
| 0.5 | Walk | `HumanoidBase_Overlapping@Walking.fbx` |
| 1.0 | Run  | `HumanoidBase_Overlapping@Running (1).fbx` |

- Blend type: 1D
- Parameter: `Speed`
- No separate transitions — the blend tree handles all interpolation
- `Speed` is always written discretely (0, 0.5, or 1.0) by `PlayerController`

---

## 2. Sprint Input Action

**Asset:** `Assets/Input/CrimsonDraftControls.inputactions`
**Map:** `Gameplay`

| Property | Value |
|----------|-------|
| Action name | `Sprint` |
| Type | Button (hold) |
| Keyboard binding | `V` |
| Gamepad binding | `ButtonWest` (X on Xbox / Square on PS) |

### IInputService

Add property:
```
InputAction Sprint { get; }
```

### InputService

Bind in constructor alongside existing Gameplay actions:
```
Sprint = this.gameplayMap[nameof(Sprint)];
```

---

## 3. PlayerController Changes

**File:** `Assets/Scripts/Navigation/Player/PlayerController.cs`

### New Serialized Fields

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `animator` | `Animator` | — | `[SerializeField]` |
| `walkSpeed` | `float` | 4f | Renamed from `moveSpeed` |
| `runSpeed` | `float` | 7f | New field |

### Cached Hash

```
private readonly int speedHash = Animator.StringToHash("Speed");
```

Cached once — never use string lookup in FixedUpdate.

### FixedUpdate Logic

```
raw = inputService.Move.ReadValue<Vector2>()

if raw.sqrMagnitude < 0.01:
    rb.linearVelocity = Vector3.zero
    animator.SetFloat(speedHash, 0f)
    return

direction = Gamepad ? raw.normalized : Quantize8Way(raw)
moveDir = new Vector3(direction.x, 0, direction.y)
transform.forward = moveDir

isSprinting = inputService.Sprint.IsPressed()
speed       = isSprinting ? runSpeed : walkSpeed
animSpeed   = isSprinting ? 1.0f    : 0.5f

rb.linearVelocity = moveDir * speed
animator.SetFloat(speedHash, animSpeed)
```

### Standards Compliance

- `speedHash` is `readonly int` — cached once, zero allocation in hot path
- `Sprint.IsPressed()` — direct bool read, no allocation
- No runtime logging (TheOne.Logging not installed in this project)
- `#nullable enable` at file top

---

## Out of Scope

- Blend tree smooth interpolation (Speed is always written discretely)
- Strafing / directional blend (2D blend tree for combat — future feature)
- Footstep audio tied to animation events
- Walk/Run speed configuration via ScriptableObject
