# Head Look at Points of Interest — Design Spec

**Date:** 2026-04-27
**Branch:** feature/head-look-at-poi

## Overview

When the player character is near an item of interest, their head turns smoothly toward it using Unity's built-in Animator IK. Items opt-in via a `Lookable` marker component; the system is completely independent of the existing interaction system.

## Components

### `Lookable` (new)

Marker component placed on any GameObject that should attract the character's gaze.

| Field | Type | Description |
|---|---|---|
| `Offset` | `Vector3` | Local offset from the GameObject's origin to the look target point |
| `Priority` | `int` | Higher value wins when multiple Lookables are in range |
| `LookPosition` | `Vector3` (read-only) | `transform.TransformPoint(Offset)` — world position of the target |

No logic, no dependencies. Pure data + one property.

### `PlayerHeadLookController` (new)

MonoBehaviour added to the `HumanoidBase_Overlapping_TPose` child GameObject (same GameObject as the `Animator` — required for `OnAnimatorIK` to fire).

| Field | Type | Default | Description |
|---|---|---|---|
| `DetectionRadius` | `float` | `3f` | Radius of the overlap sphere around the player |
| `MaxAngle` | `float` | `60f` | Half-angle of the frontal cone; items outside this angle are ignored |
| `WeightSpeed` | `float` | `3f` | Speed at which IK weight blends in and out |
| `DetectionInterval` | `float` | `0.3f` | Seconds between detection passes |
| `LookableLayer` | `LayerMask` | Interactable | Layer mask for the overlap query |

## Detection Flow

Runs on a throttled timer (every `DetectionInterval` seconds, not every frame):

1. `Physics.OverlapSphereNonAlloc()` centered on the player with `DetectionRadius`
2. For each result, check for a `Lookable` component
3. Filter: keep only items where `Vector3.Angle(transform.forward, direction_to_item) < MaxAngle`
4. Select the candidate with the highest `Priority`; ties broken by distance (nearest wins)
5. Store selected `Lookable` as `m_CurrentTarget` (null if none found)

## IK Flow

Runs every frame in `OnAnimatorIK(int layerIndex)`:

- If `m_CurrentTarget != null`: increase `m_Weight` toward `1f` at rate `WeightSpeed * Time.deltaTime`; update `m_LastLookPosition = m_CurrentTarget.LookPosition`
- If `m_CurrentTarget == null`: decrease `m_Weight` toward `0f` at rate `WeightSpeed * Time.deltaTime`
- Call `animator.SetLookAtWeight(m_Weight)`
- Call `animator.SetLookAtPosition(m_LastLookPosition)` when `m_Weight > 0` (uses cached position during fade-out so the head doesn't snap)

## Animator Setup

One change required in `Assets/Animations/Player/PlayerAnimator.controller`:

- Base Layer → enable **IK Pass** (`m_IKPass: 1`)

No new animator parameters or states are needed.

## Integration

- `PlayerHeadLookController` is added to `HumanoidBase_Overlapping_TPose` on the `Player` prefab
- No changes to `PlayerController`, `PlayerInteractionCaster`, or `NavigationScope`
- No VContainer registration — the component is self-contained
- Existing item prefabs are unaffected by default. To make an item lookable, add `Lookable` with an `Offset` and `Priority`

## Out of Scope

- Timeline / cutscene override (can be added later via `SetOverride(Lookable)` if needed)
- Eye IK (separate system)
- Lookable on non-interactable props (supported by design — just add the component)
