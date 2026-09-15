# Pushable Objects — Design Spec

## Problem

Navigation has no way for the player to move props in the world. Classic Resident Evil titles use pushable objects (bookshelves, crates) both as environmental interaction and as puzzle pieces (blocking a door, reaching a switch, revealing a passage). Crimson Draft has neither the mechanic nor any supporting code.

## Goal

Walking into a tagged prop pushes it along a world axis, at walk speed, for as long as the player keeps moving into it and nothing blocks the way. Works identically under both control schemes (Modern and Classic), requires no dedicated input, and keeps enemies honest — a prop that ends up blocking a corridor also blocks `NavMeshAgent`-driven enemies.

## Architecture

Two new pieces, kept deliberately separate from the existing `IInteractable` raycast+button system, because activation here is contact-based, not action-based:

- **`PushableObject`** (`Assets/Scripts/Navigation/Pushables/PushableObject.cs`) — self-contained unit living on the prop. Owns the pure axis math (static, no Unity dependencies) and the per-step validation/movement (NavMesh + physics obstruction check), mirroring the pattern `PlayerController.ResolveNavMeshDirection` already uses for the player.
- **`PlayerController`** gains a small push-detection step in `FixedUpdate`, inserted between "strategy produced a non-zero direction" and the existing `ResolveNavMeshDirection` call. When a push is active, it fully replaces the normal movement resolution for that frame — `ModernPlayerMovementStrategy`/`ClassicPlayerMovementStrategy` still run (both need to keep ticking regardless, per the existing "always ticked" rule for Modern), but their output direction is only used to test for and drive the push, not to move the player normally.

The two control schemes need no special-casing: both already resolve to a world-space `PlayerMovementResult.Direction` (Modern via the camera-relative basis, Classic via `transform.forward`), and the push axis is derived purely from box/player relative position — not from which scheme produced the input.

## Components

### `PushableObject` (new)

```csharp
[RequireComponent(typeof(Rigidbody))]
public sealed class PushableObject : MonoBehaviour
{
    [SerializeField] private LayerMask obstructionMask;   // walls, other pushables
    [SerializeField] private float     navMeshTolerance = 0.3f;
    [SerializeField] private Vector3   obstructionHalfExtents = new Vector3(0.4f, 0.4f, 0.4f);

    private Rigidbody   rb        = null!;
    private BoxCollider ownCollider = null!;

    private void Awake()
    {
        this.rb          = GetComponent<Rigidbody>();
        this.ownCollider = GetComponent<BoxCollider>();
    }

    // Pure math -- no Unity physics/NavMesh involved, unit-testable with plain Vector3s.
    // Dominant axis wins regardless of contact angle, so a corner hit doesn't produce a
    // diagonal push -- only ever X or Z, never both.
    public static Vector3 ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)
    {
        Vector3 delta = boxPosition - playerPosition;
        return Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
            ? new Vector3(Mathf.Sign(delta.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(delta.z));
    }

    // Validates and, if clear, performs one step. Returns false (no movement) if the next
    // position falls off the NavMesh or overlaps something solid -- same two-layer check
    // PlayerController.ResolveNavMeshDirection already applies to the player.
    public bool TryStep(Vector3 axisDirection, float stepDistance)
    {
        Vector3 next = this.rb.position + axisDirection * stepDistance;

        if (!NavMesh.SamplePosition(next, out _, this.navMeshTolerance, NavMesh.AllAreas))
            return false;

        var hits = Physics.OverlapBox(next, this.obstructionHalfExtents, transform.rotation, this.obstructionMask);
        foreach (var hit in hits)
            if (hit != this.ownCollider) return false;

        this.rb.MovePosition(next);
        return true;
    }
}
```

Also carries a `NavMeshObstacle` component (Inspector-only, no code): Carve enabled, "Carve Only Stationary" **disabled** (it moves), Move Threshold tuned small. Unity recarves the NavMesh automatically as the object moves, so `EnemyNavAgent` — already `NavMeshAgent`-driven — replans around it with no changes on the enemy side.

### `PlayerController` changes

New serialized fields alongside the existing movement/NavMesh ones:

```csharp
[SerializeField] private LayerMask pushableLayer;
[SerializeField] private float     pushProbeDistance = 0.6f;
[SerializeField] private float     pushDotThreshold   = 0.5f; // ~60°, how "into" the box the input must point
```

New animator param: `PushingHash = Animator.StringToHash("Pushing")`, set the same way `ArmedHash`/`WalkHash` already are.

`FixedUpdate`, right after the existing `if (result.Direction == Vector3.zero)` early-return (movement code at [PlayerController.cs:110-115](../../../Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs)), inserts:

```csharp
if (TryResolvePush(result.Direction, out var pushable, out var axis))
{
    bool moved = pushable.TryStep(axis, this.walkSpeed * Time.fixedDeltaTime);
    this.animator.SetBool(PushingHash, true);
    transform.forward = axis;

    this.rb.linearVelocity = moved ? axis * this.walkSpeed : Vector3.zero;
    this.animator.SetTrigger(moved ? WalkHash : IdleHash);
    return;
}
this.animator.SetBool(PushingHash, false);

// ...existing sprint/speed/ResolveNavMeshDirection path, unchanged
```

```csharp
private bool TryResolvePush(Vector3 moveDir, out PushableObject pushable, out Vector3 axis)
{
    pushable = null!;
    axis     = Vector3.zero;

    if (!Physics.Raycast(this.rb.position, moveDir, out var hit, this.pushProbeDistance, this.pushableLayer))
        return false;
    if (!hit.collider.TryGetComponent(out pushable))
        return false;

    axis = PushableObject.ResolveAxis(pushable.transform.position, this.rb.position);
    return Vector3.Dot(moveDir, axis) >= this.pushDotThreshold;
}
```

Sprint is never consulted in this branch — pushing is always `walkSpeed`, matching Classic's existing "backpedal is always walk speed" precedent of a hardcoded speed override independent of the Sprint button.

## Data Flow

1. Player holds a direction; the active strategy (`Modern`/`Classic`) resolves it to a world-space `result.Direction`, exactly as today.
2. `TryResolvePush` raycasts a short distance along that direction. If it hits a `PushableObject` and the object's `ResolveAxis` (computed from relative position, dominant-axis-wins) roughly agrees with the input direction, a push is active this frame.
3. `PushableObject.TryStep` validates the next position (NavMesh + physics obstruction) and moves the prop's `Rigidbody` if clear.
4. The player's own velocity is set to match — `axis * walkSpeed` — moving both together; if the step was blocked, both stop dead this frame instead.
5. `NavMeshObstacle` carving updates the NavMesh as the prop moves, so any `EnemyNavAgent` currently pathing recalculates around the new position on its own, no event needed.
6. Push ends the moment `TryResolvePush` fails — direction changed away from the axis, input released, or `IsAiming` became true (checked earlier in `FixedUpdate`, same priority order as today) — control falls straight back through to the normal `ResolveNavMeshDirection` path next frame.

## Edge Cases

- **Contact near a corner:** `ResolveAxis` always picks the axis with the larger `abs(delta)` component — never a diagonal, regardless of exact contact angle. This was the specific case called out during design and is covered by the axis-resolution unit tests below.
- **Pushing into a wall or another `PushableObject`:** obstruction check (`Physics.OverlapBox` against `obstructionMask`) catches both; `TryStep` returns false, both player and prop stop — no partial movement, no chained pushes.
- **Grazing a prop without pushing it:** the dot-product gate (`pushDotThreshold`) means walking tangentially past a prop's side doesn't move it — it behaves like any other solid collider (Unity's normal physics collision response handles the "don't clip through it" part; this feature only decides whether it also *moves*).
- **Aiming while touching a prop:** `IsAiming` is checked earlier in `FixedUpdate` than the new push branch, so aiming always wins — cancels/prevents a push, consistent with `IsAiming` already short-circuiting all normal movement.
- **Sprint held while pushing:** ignored entirely — push always resolves at `walkSpeed`.
- **Modern vs. Classic:** no scheme-specific code anywhere in the push path; both feed the same `result.Direction` into the same `TryResolvePush`.

## Testing

- `PushableObject.ResolveAxis`: EditMode unit tests, plain `Vector3` inputs, no scene required — cardinal deltas (pure X, pure Z), the near-corner/near-equal-delta case, and sign correctness in all four quadrants.
- `TryStep`, the raycast-based detection in `PlayerController`, and `NavMeshObstacle` carving are physics/NavMesh-dependent MonoBehaviour behavior — same testing boundary the project already draws around `ResolveNavMeshDirection` and `EnemyNavAgent`. Verified manually in Play Mode: push a box down a straight corridor, push it into a corner from an angle and confirm it snaps to one axis, push it into a wall/another box and confirm both player and box stop, and confirm an alert enemy reroutes around a box left blocking its path.
