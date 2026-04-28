# Player 3D Rotation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-04-01-player-3d-rotation-design.md`

**Goal:** Make the Player GameObject rotate to face its movement direction so the 3D model visually walks toward where it's going.

**Architecture:** Add one line to `PlayerController.FixedUpdate` (`transform.forward = moveDir`) and freeze Rigidbody rotation constraints so physics doesn't fight manual rotation. Delete the unused `FacingDirection` enum.

**Tech Stack:** Unity 3D, Rigidbody, InputSystem, VContainer, Unity MCP

---

## Files

| Action | Path | Change |
|--------|------|--------|
| Modify | `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs` | Add `transform.forward = moveDir` in FixedUpdate |
| Delete | `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs` | Unused 2D leftover |
| Delete | `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs.meta` | Unity meta for deleted file |
| MCP | Player GO in Navigation scene | Set Rigidbody Freeze Rotation X, Y, Z |

---

## Task 1: Delete FacingDirection

**Files:**
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs.meta`

- [ ] **Step 1: Confirm zero references**

  ```bash
  grep -r "FacingDirection" Game/CrimsonDraft/Assets/Scripts/
  ```

  Expected output: only `FacingDirection.cs` itself — no other files.

- [ ] **Step 2: Delete both files**

  ```bash
  rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs"
  rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs.meta"
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add -u Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs
  git add -u "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/FacingDirection.cs.meta"
  git commit -m "chore(navigation): delete unused FacingDirection enum (2D leftover)"
  ```

---

## Task 2: Add rotation to PlayerController

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs`

Current `FixedUpdate`:
```csharp
private void FixedUpdate()
{
    var raw = this.inputService.Move.ReadValue<Vector2>();

    if (raw.sqrMagnitude < 0.01f)
    {
        this.rb.linearVelocity = Vector3.zero;
        return;
    }

    var direction = this.lastDevice is Gamepad
        ? raw.normalized
        : Quantize8Way(raw);

    this.rb.linearVelocity = new Vector3(direction.x, 0f, direction.y) * this.moveSpeed;
}
```

- [ ] **Step 1: Add `transform.forward` assignment after computing `direction`**

  Replace `FixedUpdate` with:

  ```csharp
  private void FixedUpdate()
  {
      var raw = this.inputService.Move.ReadValue<Vector2>();

      if (raw.sqrMagnitude < 0.01f)
      {
          this.rb.linearVelocity = Vector3.zero;
          return;
      }

      var direction = this.lastDevice is Gamepad
          ? raw.normalized
          : Quantize8Way(raw);

      var moveDir = new Vector3(direction.x, 0f, direction.y);
      transform.forward = moveDir;
      this.rb.linearVelocity = moveDir * this.moveSpeed;
  }
  ```

  The only changes: `moveDir` local variable extracted (avoids constructing Vector3 twice), `transform.forward = moveDir` added.

- [ ] **Step 2: Verify the file compiles — run Edit Mode tests**

  In Unity: **Window → General → Test Runner → EditMode → Run All**

  Expected: all existing tests pass (no compilation errors, no regressions).

- [ ] **Step 3: Commit**

  ```bash
  git add Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs
  git commit -m "feat(navigation): rotate Player to face movement direction"
  ```

---

## Task 3: Freeze Rigidbody rotation constraints via MCP

**Target:** Player GameObject in the Navigation scene.

Without frozen rotation constraints, `Rigidbody` physics can overwrite the manual `transform.forward` rotation we set in code, causing the player to tip over or spin unpredictably.

- [ ] **Step 1: Find the Player GameObject and read its Rigidbody**

  Use MCP `find_gameobjects` with name "Player" in the Navigation scene. Then use `manage_components` to read the Rigidbody component and verify current constraint values.

- [ ] **Step 2: Set Freeze Rotation X, Y, Z on the Rigidbody**

  Use MCP `manage_components` to set `constraints` on the Rigidbody component of Player:

  - Freeze Position: none (leave as-is)
  - Freeze Rotation: X ✓, Y ✓, Z ✓

  The Unity enum value for `RigidbodyConstraints.FreezeRotation` is `112` (bits: FreezeRotationX=16 + FreezeRotationY=32 + FreezeRotationZ=64). If the Rigidbody already has position constraints, OR the existing value with 112.

- [ ] **Step 3: Save the scene**

  Use MCP `manage_scene` to save the Navigation scene so the constraint change is persisted to `Navigation.unity`.

- [ ] **Step 4: Commit**

  ```bash
  git add Game/CrimsonDraft/Assets/Scenes/Navigation.unity
  git commit -m "feat(navigation): freeze Rigidbody rotation on Player GO"
  ```

---

## Task 4: Verify in Play Mode

- [ ] **Step 1: Enter Play Mode in Navigation scene**

  Open Navigation scene, press Play.

- [ ] **Step 2: Walk in multiple directions**

  Use WASD or joystick to move in at least 4 directions. Verify:
  - Player model rotates to face the direction of movement
  - No spinning or tipping (Rigidbody constraints working)
  - Movement speed unchanged
  - Stopping (releasing input) leaves model facing the last direction (not snapping to default)

- [ ] **Step 3: Keyboard 8-way quantization still correct**

  Press diagonal (e.g. W+D). Player should rotate to exactly 45° NE — not smoothly interpolated.
