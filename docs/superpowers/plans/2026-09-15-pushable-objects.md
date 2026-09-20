# Pushable Objects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player push tagged props by walking into them — RE-classic style, axis-locked, no dedicated input, working identically under Modern and Classic control schemes, and visible to enemy NavMesh pathing.

**Architecture:** A new self-contained `PushableObject` MonoBehaviour owns the pure axis math (which world axis a push moves along, resolved from box/player relative position — dominant-delta wins, so contact angle near a corner never produces a diagonal push) and the per-step NavMesh+physics validation/movement. `PlayerController.FixedUpdate` gains a small detection branch that, when active, fully replaces the normal `ResolveNavMeshDirection` movement path for that frame and drives both the player and the box together.

**Tech Stack:** Unity (C#, `#nullable enable`), `UnityEngine.AI.NavMesh`/`NavMeshObstacle`, `UnityEngine.Physics`, NUnit EditMode tests.

**Spec:** [docs/superpowers/specs/2026-09-15-pushable-objects-design.md](../specs/2026-09-15-pushable-objects-design.md)

## Global Constraints

- Push movement always resolves at `walkSpeed` (the player's existing `PlayerController.walkSpeed` field) — Sprint is never consulted, matching Classic's existing "backpedal is always walk speed" precedent.
- `pushDotThreshold = 0.5f` — the minimum `Vector3.Dot(moveDir, axis)` for a contact to count as "pushing into it" rather than grazing past.
- `pushProbeDistance = 0.6f` — raycast distance used to detect a nearby `PushableObject` ahead of the player.
- `PushableObject.navMeshTolerance` defaults to `0.3f`, matching `PlayerController.navMeshTolerance`.
- `PushableObject.obstructionHalfExtents` defaults to `(0.4f, 0.4f, 0.4f)`.
- No new input action, no new `IInteractable` — activation is contact-only, detected every `FixedUpdate`, never through `PlayerInteractionCaster`'s raycast+button path.
- Both control schemes feed the same world-space `PlayerMovementResult.Direction` into the push path — no Modern/Classic branching anywhere in the new code.

---

## File Structure

- **Create** `Game/CrimsonDraft/Assets/Scripts/Navigation/Pushables/PushableObject.cs` — new `CrimsonDraft.Navigation.Pushables` namespace. Owns axis resolution (`ResolveAxis`, static, pure) and per-step movement (`TryStep`).
- **Create** `Game/CrimsonDraft/Assets/Tests/EditMode/PushableObjectTests.cs` — EditMode tests for `ResolveAxis` only (the only pure-math, physics-free piece).
- **Modify** `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs` — new serialized fields, a new animator hash, a new `TryResolvePush` helper, and a new branch in `FixedUpdate`.
- **Unity assets (no plain-text diff):** a new `Pushable` layer, a `Pushing` bool parameter on the player's Animator Controller, and a test prop prefab/scene instance for manual verification.

---

### Task 1: `PushableObject.ResolveAxis` (dominant-axis math)

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Pushables/PushableObject.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/PushableObjectTests.cs`

**Interfaces:**
- Produces: `public static Vector3 PushableObject.ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)` — returns one of `(±1,0,0)` or `(0,0,±1)`, never a diagonal. Consumed by `PlayerController.TryResolvePush` in Task 3 and by `PushableObject.TryStep` internally in Task 2.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Pushables;

namespace CrimsonDraft.Tests
{
    public sealed class PushableObjectTests
    {
        [Test]
        public void ResolveAxis_playerDueSouthOfBox_pushesNorth()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(0f, 0f, 5f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(0f, 0f, 1f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueNorthOfBox_pushesSouth()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(0f, 0f, -5f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(0f, 0f, -1f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueWestOfBox_pushesEast()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(5f, 0f, 0f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueEastOfBox_pushesWest()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(-5f, 0f, 0f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(-1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_cornerContact_xDeltaSlightlyLarger_choosesXAxis_notDiagonal()
        {
            // Player stands just off-center near a corner: |dx|=3 vs |dz|=2.9 -- X wins,
            // and the result must be a pure axis vector, never a diagonal blend of both.
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(3f, 0f, 2.9f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_cornerContact_zDeltaSlightlyLarger_choosesZAxis()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(2.9f, 0f, 3f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(0f, 0f, 1f), axis);
        }

        [Test]
        public void ResolveAxis_exactlyEqualDeltas_choosesXAxis_tieBreakIsDeterministic()
        {
            // Documents the tie-break: ResolveAxis uses >=, so an exact tie always picks X.
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(3f, 0f, 3f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Test Runner (Window → General → Test Runner → EditMode), filtered to `PushableObjectTests`, or the UnityMCP `run_tests` tool with `filter: "PushableObjectTests"`.
Expected: compile error — `PushableObject` does not exist yet. This is the RED state for a statically-typed class that doesn't exist.

- [ ] **Step 3: Create `PushableObject` with the `ResolveAxis` implementation**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Pushables
{
    // Contact-driven push mechanic (RE-classic style): walking into a tagged prop pushes it
    // along a world axis. Activation and per-step movement live in PlayerController.FixedUpdate
    // (see TryResolvePush there) -- this component owns only the prop's own logic: which axis a
    // push moves along, and whether the next step is clear. See
    // docs/superpowers/specs/2026-09-15-pushable-objects-design.md.
    public sealed class PushableObject : MonoBehaviour
    {
        // Pure math -- no Unity physics/NavMesh involved. Dominant axis wins regardless of
        // contact angle, so a corner hit snaps to one axis instead of pushing diagonally.
        public static Vector3 ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)
        {
            Vector3 delta = boxPosition - playerPosition;
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? new Vector3(Mathf.Sign(delta.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(delta.z));
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same Test Runner filter as Step 2.
Expected: all 7 tests in `PushableObjectTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Pushables/PushableObject.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/PushableObjectTests.cs"
git commit -m "feat(navigation): add PushableObject dominant-axis resolution"
```

---

### Task 2: `PushableObject.TryStep` (NavMesh + physics obstruction, movement)

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Pushables/PushableObject.cs`

**Interfaces:**
- Consumes: `Vector3 PushableObject.ResolveAxis(...)` from Task 1 (used internally is not required here — the caller in Task 3 resolves the axis and passes it in).
- Produces: `public bool PushableObject.TryStep(Vector3 axisDirection, float stepDistance)` — attempts one movement step; returns `true` and moves the object if clear, `false` (no movement) if blocked. Consumed by `PlayerController.FixedUpdate` in Task 3.

No automated test for this step: it depends on a baked NavMesh and live physics colliders, which EditMode tests don't have — the same boundary the project already draws around `PlayerController.ResolveNavMeshDirection` (untested) and `EnemyNavAgent`. Verified manually in Task 5.

- [ ] **Step 1: Add the Rigidbody/BoxCollider requirement, serialized fields, and `TryStep`**

Replace the full contents of `PushableObject.cs` with:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace CrimsonDraft.Navigation.Pushables
{
    // Contact-driven push mechanic (RE-classic style): walking into a tagged prop pushes it
    // along a world axis. Activation and per-step movement live in PlayerController.FixedUpdate
    // (see TryResolvePush there) -- this component owns only the prop's own logic: which axis a
    // push moves along, and whether the next step is clear. See
    // docs/superpowers/specs/2026-09-15-pushable-objects-design.md.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PushableObject : MonoBehaviour
    {
        [SerializeField] private LayerMask obstructionMask;          // walls, other pushables
        [SerializeField] private float     navMeshTolerance = 0.3f;
        [SerializeField] private Vector3   obstructionHalfExtents = new Vector3(0.4f, 0.4f, 0.4f);

        private Rigidbody   rb          = null!;
        private BoxCollider ownCollider = null!;

        private void Awake()
        {
            this.rb          = GetComponent<Rigidbody>();
            this.ownCollider = GetComponent<BoxCollider>();
        }

        // Pure math -- no Unity physics/NavMesh involved. Dominant axis wins regardless of
        // contact angle, so a corner hit snaps to one axis instead of pushing diagonally.
        public static Vector3 ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)
        {
            Vector3 delta = boxPosition - playerPosition;
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? new Vector3(Mathf.Sign(delta.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(delta.z));
        }

        // Validates and, if clear, performs one step. Returns false (no movement) if the next
        // position falls off the NavMesh or overlaps something solid -- the same two-layer check
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
}
```

- [ ] **Step 2: Confirm it compiles with no new errors**

Use the UnityMCP `read_console` tool (`action: "get"`, `types: ["error"]`, `filter_text: "PushableObject"`) or Window → General → Console in the Editor. Expected: no compiler errors referencing `PushableObject.cs`.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Pushables/PushableObject.cs"
git commit -m "feat(navigation): add PushableObject.TryStep NavMesh/physics validation"
```

---

### Task 3: `PlayerController` push detection and `FixedUpdate` integration

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs`

**Interfaces:**
- Consumes: `PushableObject.ResolveAxis(Vector3, Vector3)` (Task 1) and `PushableObject.TryStep(Vector3, float)` (Task 2).
- Produces: nothing new consumed by later tasks — this is the last code task.

No automated test for this step, same reasoning as Task 2 (physics raycast + live scene). Verified manually in Task 5.

- [ ] **Step 1: Add the `using` directive**

Modify `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs:11` (after the existing `using CrimsonDraft.Inventory;`):

```csharp
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Pushables;
```

- [ ] **Step 2: Add serialized push-detection fields**

Modify `PlayerController.cs:24` (right after the existing `navMeshTolerance` field, before the `[Header("Health Speed Steps (REmake-based)")]` block):

```csharp
        [SerializeField] private float navMeshTolerance  = 0.3f; // tolerancia horizontal para considerar "en NavMesh"

        [Header("Pushable Objects")]
        [SerializeField] private LayerMask pushableLayer;
        [SerializeField] private float     pushProbeDistance = 0.6f;
        [SerializeField] private float     pushDotThreshold   = 0.5f; // ~60 deg tolerance around the push axis

        [Header("Health Speed Steps (REmake-based)")]
```

- [ ] **Step 3: Add the `Pushing` animator hash**

Modify `PlayerController.cs:37` (after the existing `RunHash` line):

```csharp
        private static readonly int ArmedHash   = Animator.StringToHash("Armed");
        private static readonly int IdleHash    = Animator.StringToHash("Idle");
        private static readonly int WalkHash    = Animator.StringToHash("Walk");
        private static readonly int RunHash     = Animator.StringToHash("Run");
        private static readonly int PushingHash = Animator.StringToHash("Pushing");
```

- [ ] **Step 4: Insert the push branch in `FixedUpdate`, and add `TryResolvePush`**

Modify `PlayerController.cs:110-115` — replace:

```csharp
            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            var isSprinting     = this.inputService.Sprint.IsPressed() && result.AllowSprint;
```

with:

```csharp
            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                this.animator.SetBool(PushingHash, false);
                return;
            }

            if (this.TryResolvePush(result.Direction, out var pushable, out var pushAxis))
            {
                bool moved = pushable.TryStep(pushAxis, this.walkSpeed * Time.fixedDeltaTime);

                transform.forward = pushAxis;
                this.animator.SetBool(PushingHash, true);
                this.animator.SetTrigger(moved ? WalkHash : IdleHash);
                this.rb.linearVelocity = moved ? pushAxis * this.walkSpeed : Vector3.zero;
                return;
            }
            this.animator.SetBool(PushingHash, false);

            var isSprinting     = this.inputService.Sprint.IsPressed() && result.AllowSprint;
```

Then add the new helper method right after `FixedUpdate` closes, before `ResolveNavMeshDirection` (i.e. insert between `PlayerController.cs:132` `}` and `PlayerController.cs:134` `private Vector3 ResolveNavMeshDirection(...)`):

```csharp
        // Contact-only activation: no dedicated input, no IInteractable raycast+button path.
        // The axis comes from PushableObject.ResolveAxis (box/player relative position), not
        // from moveDir -- moveDir only gates *whether* this counts as pushing into it versus
        // grazing past it tangentially.
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

- [ ] **Step 5: Confirm it compiles with no new errors**

Use the UnityMCP `read_console` tool (`action: "get"`, `types: ["error"]`, `filter_text: "PlayerController"`) or the Editor console. Expected: no compiler errors referencing `PlayerController.cs`.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs"
git commit -m "feat(navigation): wire pushable-object detection into PlayerController"
```

---

### Task 4: Unity asset wiring (layer, animator parameter, test prop)

**Files:**
- Unity project settings: `Game/CrimsonDraft/ProjectSettings/TagManager.asset` (new layer)
- Unity asset: the Animator Controller referenced by `Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab`'s `Animator` component (new bool parameter)
- New: a simple pushable test prop, placed in whichever scene will be used for manual verification in Task 5 (e.g. `Deck_B_Development.unity`, already the in-progress dev scene per current git status)

This task is Unity-editor/asset work, not a plain-text diff — do it through the Editor (or the UnityMCP tools) rather than hand-editing YAML.

- [ ] **Step 1: Add a `Pushable` layer**

Edit → Project Settings → Tags and Layers → add `Pushable` in the first empty User Layer slot. (Equivalently, the UnityMCP `manage_editor` tool's tag/layer actions.)

- [ ] **Step 2: Add the `Pushing` bool parameter to the player's Animator Controller**

Open `Player.prefab`, select the GameObject with the `Animator` component, open its `Controller` asset in the Animator window, add a `Bool` parameter named exactly `Pushing` (matches `Animator.StringToHash("Pushing")` from Task 3). No new states/transitions are required — the existing `LocomotionBlend` blend tree keeps driving visuals off `Speed`/the Walk/Idle triggers already set in the new branch; `Pushing` is available for a later animation pass but isn't consumed by any transition yet.

- [ ] **Step 3: Create a test prop and wire it up**

In the manual-verification scene:
1. Create a Cube primitive, scale it to a believable crate size (e.g. `(1, 1, 1)`).
2. Add a `PushableObject` component (this also adds the required `Rigidbody` and `BoxCollider` via `[RequireComponent]`).
3. Set the Rigidbody: gravity on, freeze rotation X/Y/Z (matches the player's own Rigidbody config per the project's Sistema de Movimiento physics table).
4. Add a `NavMeshObstacle` component: enable **Carve**, disable **Carve Only Stationary** (it needs to carve while moving), leave Shape as Box matching the collider.
5. Set the GameObject's layer to `Pushable`.
6. Place it somewhere reachable on the baked NavMesh, ideally near a corner (to exercise the dominant-axis case) and somewhere with a straight corridor (to exercise a full push-and-stop-at-wall case).

- [ ] **Step 4: Point `PlayerController` at the new layer**

Select the `Player.prefab` instance (or the prefab asset) in the Inspector, find the new `Pushable Objects` header added in Task 3, set `Pushable Layer` to the `Pushable` layer created in Step 1. Leave `Push Probe Distance` (0.6) and `Push Dot Threshold` (0.5) at their defaults.

- [ ] **Step 5: Save the scene and commit**

```bash
git add "Game/CrimsonDraft/ProjectSettings/TagManager.asset"
git add -A  # scene/prefab/controller changes from this task -- review `git status` first to confirm scope
git commit -m "chore(navigation): wire Pushable layer, animator param, and test prop"
```

---

### Task 5: Manual end-to-end verification

No code changes. Work through this checklist in Play Mode, in the scene set up in Task 4, testing **both** Modern and Classic control schemes (toggle via Settings → General → Control, per the existing `ControlSchemeService`):

- [ ] Walk straight into the test prop along a clear corridor — it slides ahead of the player at walk speed, player and box move together.
- [ ] Approach the prop near its corner at an angle — confirm it snaps to one axis (X or Z) and never drifts diagonally, regardless of the exact approach angle.
- [ ] Push the prop into a wall (or a second `PushableObject` placed to block it) — confirm both the box and the player stop dead on the blocked frame, no clipping through.
- [ ] Walk past the prop tangentially (input direction roughly perpendicular to the box-to-player axis) — confirm it does **not** move, and the player collides with it normally (doesn't clip through).
- [ ] Hold Sprint while pushing — confirm the push speed is unaffected (still `walkSpeed`).
- [ ] Start aiming while touching the prop — confirm the push is cancelled/prevented (matches `IsAiming` already short-circuiting all movement).
- [ ] Push the prop so it ends up blocking a corridor an alert enemy patrols/chases through (use `Deck_B_Development`'s existing enemy setup) — confirm the `EnemyNavAgent` reroutes around it instead of clipping through or getting stuck.
- [ ] Repeat the straight-corridor and corner-snap checks under **both** Modern and Classic — confirm identical behavior in both (this is the point of not having any scheme-specific code in the push path).

- [ ] **If all checks pass, commit any scene changes made purely for testing (e.g. moved test prop) or revert them if they're not meant to ship:**

```bash
git status  # confirm only intended files changed
git add -A
git commit -m "test(navigation): verify pushable objects end to end"
```

---

## Self-Review Notes

- **Spec coverage:** Problem/Goal → Tasks 1-3. Architecture (separation from `IInteractable`, detection in `PlayerController`) → Task 3. `PushableObject` component (axis math + `TryStep`) → Tasks 1-2. `PlayerController` changes (fields, hash, `TryResolvePush`, `FixedUpdate` branch) → Task 3, code blocks copied verbatim from the spec. `NavMeshObstacle`/carving → Task 4 Step 3. Data Flow steps 1-6 → exercised end-to-end in Task 5. All Edge Cases (corner snap, wall/box block, tangential graze, aiming cancels, sprint ignored, Modern/Classic parity) → each has a corresponding Task 5 checklist line. Testing section (axis unit tests vs. manual physics verification) → Task 1 tests + Task 5 checklist.
- **Placeholder scan:** no TBD/TODO; every step carries real code or a concrete, actionable Editor instruction.
- **Type consistency:** `ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)` and `TryStep(Vector3 axisDirection, float stepDistance)` signatures match exactly between their definition (Tasks 1-2) and their call sites (Task 3's `TryResolvePush`/`FixedUpdate` block). `PushingHash`/`pushableLayer`/`pushProbeDistance`/`pushDotThreshold` names match between declaration (Task 3 Steps 2-3) and use (Task 3 Step 4).
