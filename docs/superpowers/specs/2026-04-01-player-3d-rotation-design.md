# Player 3D Rotation — Design Spec

**Date:** 2026-04-01
**Status:** Approved

## Problem

`PlayerController` moves the player via `Rigidbody.linearVelocity` in 360° (gamepad) or 8-way quantized (keyboard), but never rotates the GameObject. The 3D model child (`Humanoid`) therefore always faces the same direction regardless of movement.

## Solution

Rotate the root Player transform to face the movement direction each `FixedUpdate`. The model child inherits the rotation from the root with no additional code.

## Hierarchy

```
Player (GameObject)
├── Rigidbody         ← Freeze Rotation: X ✓, Y ✓, Z ✓
├── Collider          ← unchanged
├── PlayerController  ← adds rotation logic
└── Humanoid (child)  ← 3D model, unchanged — inherits rotation from root
```

## Logic

In `PlayerController.FixedUpdate`, after computing `direction`:

```
moveDir = Vector3(direction.x, 0, direction.y)
if moveDir != zero:
    transform.forward = moveDir
rb.linearVelocity = moveDir * moveSpeed
```

Rotation is **instantaneous** (no Slerp). This matches the tactical, snappy feel. Slerp would cause visible sliding without walk animations.

The Rigidbody **must** have rotation constraints frozen (X, Y, Z) so physics doesn't fight the manual `transform.forward` assignment.

## Deletions

| File | Reason |
|------|--------|
| `Scripts/Navigation/Player/FacingDirection.cs` | 4-direction enum leftover from 2D prototype — zero references in codebase |

## Out of Scope

- Walk animations (model has no rig)
- Strafe (aim direction ≠ move direction) — future feature
- Camera changes
