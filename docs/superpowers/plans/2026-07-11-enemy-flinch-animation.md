# Enemy Flinch Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On each bullet of the operator's shoot burst that actually hits (not a `Miss`), the target enemy plays its next flinch clip in strict alternation (`Hit_1`, `Hit_2`, `Hit_1`, ...), fired at the same moment as that bullet's operator shoot trigger.

**Architecture:** `PlayOperatorShootBurstAsync`'s signature changes from `(int slotIndex, int shotCount)` to `(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)`, since the method now needs per-bullet hit/miss data, not just a count. `BattlefieldView` gains a slot-indexed enemy `Animator` cache and a per-enemy `Hit1`/`Hit2` alternation toggle (mirroring the existing operator-slot tracking), and fires the enemy's flinch trigger inside the same per-bullet loop that already drives the operator's `Shoot` trigger — synced by construction, since both fire in the same loop iteration. `AimingState` gains a field to retain the resolved shots array from QTE resolution until the burst plays (it currently discards it after applying damage).

**Tech Stack:** C#, UniTask (`Cysharp.Threading.Tasks`), Unity `Animator`, NUnit EditMode tests with hand-written fakes (no mocking framework).

## Global Constraints

- All files use `#nullable enable`.
- No `Co-Authored-By` trailers in commits.
- Tests run via Unity Test Runner (EditMode only) — no CLI test command exists. Verify by describing exact menu path / MCP `run_tests` filter, not by running a shell command.
- Follow existing test pattern: plain C# fakes implementing the same interfaces as production code, no mocking framework.
- A `Miss` bullet (`ResolvedShot.Zone == ShotZone.Miss`) must never trigger enemy flinch.
- The enemy flinch trigger must not gate the burst's timing — only the operator's own shoot-clip duration paces the loop, exactly as before this feature.
- If the target enemy slot is `-1` (no enemy was targeted) or has no cached `Animator`, flinch is silently skipped — this must never throw or break the operator's burst.

---

## File Structure

- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs` — change `PlayOperatorShootBurstAsync`'s signature.
- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs` — retain `pendingShots`, pass the new arguments.
- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` — add enemy-slot Animator/alternation tracking, drive the `Hit1`/`Hit2` triggers inside the existing per-bullet loop.
- Modify `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` — migrate `FakeBattlefieldView` to the new signature, update/add tests.

---

### Task 1: Interface signature + test fakes + failing tests

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `UniTask IBattlefieldView.PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)` — later consumed by `AimingState` (Task 2) and implemented for real by `BattlefieldView` (Task 3).
- Produces on `FakeBattlefieldView`: `int BurstCallCount` (unchanged), `int LastBurstOperatorSlotIndex` (renamed from `LastBurstSlotIndex`), `int LastBurstEnemySlotIndex` (new), `ResolvedShot[] LastBurstShots` (replaces `int LastBurstShotCount`), `void HoldNextBurst()`/`void CompletePendingBurst()` (unchanged).

- [ ] **Step 1: Change the interface signature**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs:36`, replace:

```csharp
        UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount);
```

with:

```csharp
        UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots);
```

- [ ] **Step 2: Migrate `FakeBattlefieldView`'s tracking fields**

In `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`, replace the block at lines 668-670:

```csharp
            public int BurstCallCount      { get; private set; }
            public int LastBurstSlotIndex  { get; private set; } = -1;
            public int LastBurstShotCount  { get; private set; } = -1;
```

with:

```csharp
            public int BurstCallCount             { get; private set; }
            public int LastBurstOperatorSlotIndex  { get; private set; } = -1;
            public int LastBurstEnemySlotIndex     { get; private set; } = -1;
            public ResolvedShot[] LastBurstShots   { get; private set; } = Array.Empty<ResolvedShot>();
```

- [ ] **Step 3: Migrate the fake's method implementation**

Replace the block at lines 725-731:

```csharp
            public UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount)
            {
                this.BurstCallCount++;
                this.LastBurstSlotIndex = slotIndex;
                this.LastBurstShotCount = shotCount;
                return this.pendingBurstSource != null ? this.pendingBurstSource.Task : UniTask.CompletedTask;
            }
```

with:

```csharp
            public UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)
            {
                this.BurstCallCount++;
                this.LastBurstOperatorSlotIndex = operatorSlotIndex;
                this.LastBurstEnemySlotIndex = enemySlotIndex;
                this.LastBurstShots = shots;
                return this.pendingBurstSource != null ? this.pendingBurstSource.Task : UniTask.CompletedTask;
            }
```

- [ ] **Step 4: Update the existing burst-args test**

Replace the test at lines 256-278 (`ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorAndShotCount`):

```csharp
        [Test]
        public void ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorAndShotCount()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(2);
            c.BeginShootConfiguration(2);

            this.shotCountView.Increment();
            this.shotCountView.Increment(); // Value = 3

            InvokeConfirm(c); // ShotCountSelectionState -> TargetSelState (enemies present)
            InvokeConfirm(c); // TargetSelectionState -> AimingState

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss aim window -> should trigger the burst

            Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(2, this.battlefieldView.LastBurstSlotIndex);
            Assert.AreEqual(3, this.battlefieldView.LastBurstShotCount);
        }
```

with:

```csharp
        [Test]
        public void ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorEnemyAndShots()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(2);
            c.BeginShootConfiguration(2);

            this.shotCountView.Increment();
            this.shotCountView.Increment(); // Value = 3

            InvokeConfirm(c); // ShotCountSelectionState -> TargetSelState (enemies present)
            InvokeConfirm(c); // TargetSelectionState -> AimingState

            var shots = new[]
            {
                new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
                new ResolvedShot(1, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0),
                new ResolvedShot(2, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40),
            };
            this.aimView.FireResolvedShots(shots);

            InvokeConfirm(c); // dismiss aim window -> should trigger the burst

            Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(2, this.battlefieldView.LastBurstOperatorSlotIndex);
            Assert.AreEqual(1, this.battlefieldView.LastBurstEnemySlotIndex);
            Assert.AreEqual(3, this.battlefieldView.LastBurstShots.Length);
            Assert.AreEqual(ShotZone.Head, this.battlefieldView.LastBurstShots[2].Zone);
        }
```

- [ ] **Step 5: Add a test for the no-enemy-target case**

Add this test immediately after the one from Step 4:

```csharp
        [Test]
        public void ShotFired_extraConfirm_noEnemyTarget_passesNegativeOneEnemySlotToBurst()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0); // no occupied enemy slots -> ShotCountSelectionState goes straight to AimingState

            InvokeConfirm(c); // ShotCountSelectionState -> AimingState directly (no enemies)

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            InvokeConfirm(c); // dismiss -> triggers burst

            Assert.AreEqual(-1, this.battlefieldView.LastBurstEnemySlotIndex);
            Assert.AreEqual(1, this.battlefieldView.LastBurstShots.Length);
        }
```

- [ ] **Step 6: Run the tests and confirm they fail for the right reason**

Open Unity Test Runner (Window → General → Test Runner) → EditMode tab, filter by class `CombatMenuControllerTests`, run.

Expected: the project fails to compile — `BattlefieldView` (which implements `IBattlefieldView`) still has the old `PlayOperatorShootBurstAsync(int, int)` signature and no longer matches the interface. This is expected; Task 2 fixes it. Confirm via `read_console` (MCP) or the Console window that the only error is this signature-mismatch error on `BattlefieldView`, nothing else.

- [ ] **Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "test(combat): add failing coverage for enemy flinch burst wiring"
```

---

### Task 2: `AimingState` retains resolved shots and passes the new arguments

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` (signature migration only — see Step 1)

**Interfaces:**
- Consumes: `IBattlefieldView.PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)` (from Task 1).
- Produces: no new public surface — internal behavior change to `AimingState.HandleShotsResolved` / `CloseAimAndReturnToOperatorSelectionAsync`.

- [ ] **Step 1: Migrate `BattlefieldView`'s method signature (no new behavior yet)**

This task is about `AimingState`'s plumbing, not the real flinch logic (that's Task 3). To unblock compilation, update `BattlefieldView.cs:160-186`'s method signature and internal `shotCount` usage now — Task 3 adds the enemy-flinch logic on top of this body without otherwise changing it. Replace:

```csharp
        public async UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(slotIndex, out var animator) || animator == null)
                return;

            // The "Shoot" trigger only has an outgoing transition defined from "AimingIdlePistol"
            // (not the default "IdleUnarmed"), so Aim must be set and the transition into
            // AimingIdlePistol must actually complete before triggering Shoot has any effect.
            animator.SetBool(AimHash, true);
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("AimingIdlePistol"))
                await UniTask.NextFrame();

            int count = Mathf.Max(1, shotCount);
            for (int i = 0; i < count; i++)
            {
                animator.SetTrigger(ShootHash);
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
                    await UniTask.NextFrame();

                var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration));
            }

            animator.SetBool(AimHash, false);
        }
```

with:

```csharp
        public async UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            // The "Shoot" trigger only has an outgoing transition defined from "AimingIdlePistol"
            // (not the default "IdleUnarmed"), so Aim must be set and the transition into
            // AimingIdlePistol must actually complete before triggering Shoot has any effect.
            animator.SetBool(AimHash, true);
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("AimingIdlePistol"))
                await UniTask.NextFrame();

            int count = Mathf.Max(1, shots.Length);
            for (int i = 0; i < count; i++)
            {
                animator.SetTrigger(ShootHash);
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
                    await UniTask.NextFrame();

                var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration));
            }

            animator.SetBool(AimHash, false);
        }
```

(`enemySlotIndex` is intentionally unused in this task's body — Task 3 wires it up. Unused method parameters do not produce a compiler warning in C#.)

- [ ] **Step 2: Run tests to verify they now compile and fail only on the new assertions**

Unity Test Runner → EditMode → `CombatMenuControllerTests`.

Expected: project compiles. `ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorEnemyAndShots` FAILs on `Assert.AreEqual(2, this.battlefieldView.LastBurstOperatorSlotIndex)` (actual `-1`, since nothing calls the burst yet with real data — `AimingState` doesn't pass the enemy slot or persist shots yet). `ShotFired_extraConfirm_noEnemyTarget_passesNegativeOneEnemySlotToBurst` FAILs the same way. All other existing tests still PASS.

- [ ] **Step 3: Add the `pendingShots` field and retain it in `HandleShotsResolved`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`, add the field next to `isPlayingBurst` (after line 19):

```csharp
        private bool awaitingDismiss;
        private bool isPlayingBurst;
        private ResolvedShot[] pendingShots = Array.Empty<ResolvedShot>();
```

Add `using System;` to the file's using block (needed for `Array.Empty<T>()`), after `using Cysharp.Threading.Tasks;`:

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Operators;
```

In `HandleShotsResolved` (starting at line 64), add the retention line right after the null-check block. Replace:

```csharp
        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            int totalDamage = 0;
            if (shots != null)
            {
                foreach (var shot in shots)
                    totalDamage += Mathf.Max(0, shot.Damage);
            }
```

with:

```csharp
        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            this.pendingShots = shots ?? Array.Empty<ResolvedShot>();

            int totalDamage = 0;
            if (shots != null)
            {
                foreach (var shot in shots)
                    totalDamage += Mathf.Max(0, shot.Damage);
            }
```

- [ ] **Step 4: Pass the new arguments at the call site**

In `CloseAimAndReturnToOperatorSelectionAsync` (line 93), replace:

```csharp
            this.isPlayingBurst = true;
            await this.battlefieldView.PlayOperatorShootBurstAsync(this.context.SelectedOperator, this.context.SelectedShotCount);
            this.isPlayingBurst = false;
```

with:

```csharp
            this.isPlayingBurst = true;
            await this.battlefieldView.PlayOperatorShootBurstAsync(
                this.context.SelectedOperator,
                this.context.CurrentTargetSlot,
                this.pendingShots);
            this.isPlayingBurst = false;
```

(`this.context.CurrentTargetSlot` is still valid here — it isn't reset to `-1` until two lines below this call, exactly as it was before this task.)

- [ ] **Step 5: Run tests to verify they pass**

Unity Test Runner → EditMode → `CombatMenuControllerTests`.

Expected: all tests PASS, including the three from Task 1.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(combat): retain resolved shots and pass enemy target into the shoot burst"
```

---

### Task 3: Real enemy flinch driving in `BattlefieldView`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

**Interfaces:**
- Consumes: nothing new (uses `Animator`, already available; content dependency — `Enemy_Combat_Controller.controller`'s `Hit1`/`Hit2` triggers and their `AnyState`/return transitions — already exists and is committed, verified against the live asset).
- Produces: real implementation of the enemy-flinch half of `PlayOperatorShootBurstAsync`, already wired end-to-end from Task 2.

- [ ] **Step 1: Add enemy-slot Animator and alternation tracking**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`, add two fields and two parameter hashes next to the existing per-slot dictionaries (after `private static readonly int AimHash = Animator.StringToHash("Aim");`, around line 43):

```csharp
        private readonly Dictionary<int, Animator> enemyAnimatorBySlot = new();
        private readonly Dictionary<int, bool> enemyHitToggleBySlot = new(); // false = Hit1 next, true = Hit2 next
        private static readonly int Hit1Hash = Animator.StringToHash("Hit1");
        private static readonly int Hit2Hash = Animator.StringToHash("Hit2");
```

- [ ] **Step 2: Clear the new dictionaries in `Populate()` and cache the enemy Animator per slot**

In `Populate()`, add the two `.Clear()` calls next to `this.operatorAnimatorBySlot.Clear();` (line 59):

```csharp
            this.operatorAnimatorBySlot.Clear();
            this.enemyAnimatorBySlot.Clear();
            this.enemyHitToggleBySlot.Clear();
```

Then, in the enemy spawn loop, cache the `Animator` right after `if (mr != null) this.enemyRendererBySlot[i] = mr;` (line 84):

```csharp
                if (mr != null) this.enemyRendererBySlot[i] = mr;
                var enemyAnimator = go.GetComponentInChildren<Animator>();
                if (enemyAnimator != null) this.enemyAnimatorBySlot[i] = enemyAnimator;
```

- [ ] **Step 3: Add the `TriggerEnemyFlinch` helper**

Add this private method right after `PlayOperatorShootBurstAsync` (after the closing brace that currently ends at line 186):

```csharp
        private void TriggerEnemyFlinch(int enemySlotIndex)
        {
            if (enemySlotIndex < 0) return;
            if (!this.enemyAnimatorBySlot.TryGetValue(enemySlotIndex, out var animator) || animator == null) return;

            bool useHit2 = this.enemyHitToggleBySlot.TryGetValue(enemySlotIndex, out var toggle) && toggle;
            animator.SetTrigger(useHit2 ? Hit2Hash : Hit1Hash);
            this.enemyHitToggleBySlot[enemySlotIndex] = !useHit2;
        }
```

- [ ] **Step 4: Call it from inside the per-bullet loop**

In `PlayOperatorShootBurstAsync` (migrated in Task 2), the per-bullet loop currently reads:

```csharp
            int count = Mathf.Max(1, shots.Length);
            for (int i = 0; i < count; i++)
            {
                animator.SetTrigger(ShootHash);
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
                    await UniTask.NextFrame();
```

Add the flinch call right after the operator's own `SetTrigger(ShootHash)`:

```csharp
            int count = Mathf.Max(1, shots.Length);
            for (int i = 0; i < count; i++)
            {
                animator.SetTrigger(ShootHash);

                if (i < shots.Length && shots[i].Zone != ShotZone.Miss)
                    this.TriggerEnemyFlinch(enemySlotIndex);

                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
                    await UniTask.NextFrame();
```

The `i < shots.Length` guard matters because `count` is `Mathf.Max(1, shots.Length)` — if `shots` is ever empty, `count` is still `1` so the operator still visibly fires once, but there is no `shots[0]` to read a hit/miss from, so the flinch call must be skipped for that synthesized iteration.

- [ ] **Step 5: Run the full EditMode suite to confirm no regressions**

Unity Test Runner → EditMode → run all tests (or filter by `CombatMenuControllerTests`).

Expected: all tests PASS except the pre-existing, unrelated set already documented in this codebase (`CombatMenuControllerTests.ShotCount_cancel_returnsToCommandPanel` + 17 `InventoryServiceTests.*`) — these were failing before this feature and are not this feature's responsibility. This class has no direct automated test for the `Animator`-driving code path itself, consistent with how `PlayEnemyAttackFeedback`/`ShowOperatorDamage`/the operator half of `PlayOperatorShootBurstAsync` are also untested directly, only exercised indirectly through `IBattlefieldView` fakes.

- [ ] **Step 6: Manual verification in Play Mode**

The content is already in place and verified: `Enemy_Combat_Controller.controller` has `Hit1`/`Hit2` trigger parameters, `AnyState → Hit_1` / `AnyState → Hit_2` transitions, and `Hit_1 → Idle_1_twitch` / `Hit_2 → Idle_1_twitch` unconditioned exit-time transitions back to the default state.

1. Enter Play Mode in a scene with a combat encounter (e.g. `Combat.unity`).
2. Select an operator, choose Shoot, pick a shot count of 3+, resolve the aim QTE with a mix of hits and at least one miss, dismiss it.
3. Confirm the targeted enemy plays `Hit_1`, then `Hit_2`, then `Hit_1` (strict alternation) for each hit bullet, in sync with that bullet's operator shoot trigger — and does NOT flinch on the miss bullet.
4. Confirm the enemy returns to `Idle_1_twitch` between flinches (not stuck in a `Hit_N` pose).
5. If the flinch doesn't fire, check `read_console` (MCP) for a warning/error and confirm the trigger names on `Enemy_Combat_Controller` are still exactly `Hit1`/`Hit2` (case-sensitive).

- [ ] **Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(combat): cycle enemy flinch animation in sync with each shoot burst hit"
```
