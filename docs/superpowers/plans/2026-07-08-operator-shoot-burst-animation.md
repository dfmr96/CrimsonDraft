# Operator Shoot Burst Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After the player dismisses the aim QTE window in combat, the selected operator's battlefield model plays its "Shoot" animation once per bullet fired, waiting for each clip to actually finish, while the whole combat loop stays paused for the duration.

**Architecture:** `BattlefieldView` gains a slot-indexed `Animator` cache (mirroring its existing enemy-slot tracking) and a new `PlayOperatorShootBurstAsync(slotIndex, shotCount)` method on `IBattlefieldView` that triggers the `Shoot` animation `shotCount` times, waiting on the real clip length between triggers. `AimingState.CloseAimAndReturnToOperatorSelection` becomes an async `UniTaskVoid` that hides the aim UI, awaits the burst, then completes the shot and transitions state — reusing the existing `SetWaitMode` pause instead of adding a new pause mechanism.

**Tech Stack:** C#, UniTask (`Cysharp.Threading.Tasks`), Unity `Animator`, NUnit EditMode tests with hand-written fakes (no mocking framework).

## Global Constraints

- All files use `#nullable enable`.
- No `Co-Authored-By` trailers in commits.
- Tests run via Unity Test Runner (EditMode only) — no CLI test command exists. Verify by describing exact menu path / MCP `run_tests` filter, not by running a shell command.
- Follow existing test pattern: plain C# fakes implementing the same interfaces as production code, no mocking framework.
- `[Preserve]` / `[Inject]` conventions on constructors are unaffected by this plan — no DI wiring changes are needed (no new constructor dependencies).

---

## File Structure

- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs` — add `PlayOperatorShootBurstAsync` to the interface.
- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs` — add the `isPlayingBurst` guard and the async dismiss flow that calls the new method.
- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` — add `operatorAnimatorBySlot`, populate it in `Populate()`, implement `PlayOperatorShootBurstAsync`.
- Modify `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` — extend `FakeBattlefieldView` and `FakeOrchestrator`, add two new tests.

---

### Task 1: Interface + test fakes + failing tests

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `UniTask IBattlefieldView.PlayOperatorShootBurstAsync(int slotIndex, int shotCount)` — later consumed by `AimingState` (Task 2) and implemented for real by `BattlefieldView` (Task 3).
- Produces on `FakeBattlefieldView`: `int BurstCallCount`, `int LastBurstSlotIndex`, `int LastBurstShotCount`, `void HoldNextBurst()`, `void CompletePendingBurst()`.
- Produces on `FakeOrchestrator`: `int NotifyShootCompletedCallCount`.

- [ ] **Step 1: Add the method to `IBattlefieldView`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`, add the using directive and the new method to the interface:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;

namespace CrimsonDraft.Combat
{
    public readonly struct EnemyDamageResult
    {
        public int SlotIndex      { get; }
        public int DamageApplied  { get; }
        public int RemainingHp    { get; }
        public bool IsDead        { get; }

        public EnemyDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead)
        {
            this.SlotIndex     = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp   = remainingHp;
            this.IsDead        = isDead;
        }
    }

    public interface IBattlefieldView
    {
        void Populate(EncounterData encounter);
        void SetOperatorIndicator(int slotIndex);
        void DimOperatorIndicator();
        void PlayEnemyAttackFeedback(int enemySlotIndex);
        void ShowOperatorDamage(int operatorSlotIndex, int damage);
        void SetEnemyTargetIndicator(int slotIndex);
        void HideEnemyTargetIndicator();
        int[] GetOccupiedEnemySlots();
        AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex);
        EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int damage);
        bool HasAliveEnemies();
        UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount);
#if UNITY_EDITOR || DEBUG_COMBAT
        (int Current, int Max, bool IsDead) GetEnemyHpDebug(int slotIndex);
#endif
    }
}
```

- [ ] **Step 2: Add `using Cysharp.Threading.Tasks;` to the test file**

In `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`, add the using directive alongside the existing ones (top of file, after `using System;`):

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
```

- [ ] **Step 3: Extend `FakeBattlefieldView` with burst tracking**

In the same file, inside `private sealed class FakeBattlefieldView : IBattlefieldView`, add these members (place them right after the existing `LastDamageResult` property, around line 610):

```csharp
public int BurstCallCount      { get; private set; }
public int LastBurstSlotIndex  { get; private set; } = -1;
public int LastBurstShotCount  { get; private set; } = -1;
private UniTaskCompletionSource? pendingBurstSource;

public void HoldNextBurst()        => this.pendingBurstSource = new UniTaskCompletionSource();
public void CompletePendingBurst() => this.pendingBurstSource?.TrySetResult();

public UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount)
{
    this.BurstCallCount++;
    this.LastBurstSlotIndex = slotIndex;
    this.LastBurstShotCount = shotCount;
    return this.pendingBurstSource != null ? this.pendingBurstSource.Task : UniTask.CompletedTask;
}
```

- [ ] **Step 4: Extend `FakeOrchestrator` with a call counter**

In the same file, inside `private sealed class FakeOrchestrator : ICombatOrchestrator`, change:

```csharp
public void SetWaitMode(bool paused)       { }
public bool IsOperatorReady(int slotIndex) => true;
public void NotifyShootCompleted()         { }
```

to:

```csharp
public int  NotifyShootCompletedCallCount  { get; private set; }
public void SetWaitMode(bool paused)       { }
public bool IsOperatorReady(int slotIndex) => true;
public void NotifyShootCompleted()         => this.NotifyShootCompletedCallCount++;
```

- [ ] **Step 5: Write the two new failing tests**

Add these tests in the `// ── Aim minigame (no enemies → bypasses TargetSelection) ───────` region (right after `ShotFired_extraConfirm_closesAimAndCommandPanel`, around line 253):

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

[Test]
public void OnConfirm_whileBurstPlaying_ignoresExtraConfirmAndDoesNotTransitionYet()
{
    this.battlefieldView.SetOccupiedSlots(new[] { 1 });
    this.battlefieldView.SetEnemyHp(1, 100);
    this.battlefieldView.HoldNextBurst();
    var c = BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    c.BeginShootConfiguration(0);

    InvokeConfirm(c); // -> TargetSelState
    InvokeConfirm(c); // -> AimingState

    this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

    InvokeConfirm(c); // dismiss -> starts burst, held pending

    Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
    Assert.IsFalse(this.aimView.IsVisible);
    Assert.IsFalse(this.commandPanel.IsVisible);
    Assert.AreEqual(0, this.orchestrator.NotifyShootCompletedCallCount);

    InvokeConfirm(c); // should be ignored while the burst is still playing

    Assert.AreEqual(1, this.battlefieldView.BurstCallCount);
    Assert.AreEqual(0, this.orchestrator.NotifyShootCompletedCallCount);

    this.battlefieldView.CompletePendingBurst();

    Assert.AreEqual(1, this.orchestrator.NotifyShootCompletedCallCount);
}
```

- [ ] **Step 6: Run the tests and confirm they fail for the right reason**

Open Unity Test Runner (Window → General → Test Runner) → EditMode tab, filter by class `CombatMenuControllerTests`, run.

Expected: the whole file now fails to compile (`AimingState` and `BattlefieldView` don't implement `PlayOperatorShootBurstAsync` yet, so `AimingState` doesn't reference it — this is fine, since `AimingState` doesn't need to implement `IBattlefieldView`, only `BattlefieldView` does). Since `BattlefieldView` implements `IBattlefieldView` and doesn't yet have `PlayOperatorShootBurstAsync`, the project fails to compile with a "does not implement interface member" error on `BattlefieldView`. This is expected — Task 3 fixes it. Confirm via `read_console` (MCP) or the Console window that the only error is this missing-member error on `BattlefieldView`, nothing else.

- [ ] **Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "test(combat): add failing coverage for operator shoot burst animation"
```

---

### Task 2: `AimingState` calls the burst and guards re-entrant confirm

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` (temporary stub only — see Step 1)

**Interfaces:**
- Consumes: `IBattlefieldView.PlayOperatorShootBurstAsync(int slotIndex, int shotCount)` (from Task 1).
- Produces: no new public surface — internal behavior change to `AimingState.OnConfirm()` / `CloseAimAndReturnToOperatorSelection`.

- [ ] **Step 1: Add a temporary stub to `BattlefieldView` so the project compiles**

This task is about `AimingState`'s behavior, not the real Animator logic (that's Task 3). To unblock compilation, add a minimal stub to `BattlefieldView.cs` now — Task 3 replaces its body:

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`, add this method to the class (right after `HasAliveEnemies()`, around line 149):

```csharp
public UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount) => UniTask.CompletedTask;
```

Add `using Cysharp.Threading.Tasks;` to the file's using block (after `using DG.Tweening;`):

```csharp
using System;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
```

- [ ] **Step 2: Run tests to verify the new tests now compile and fail on assertions (not compile errors)**

Unity Test Runner → EditMode → `CombatMenuControllerTests`.

Expected: project compiles. `ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorAndShotCount` FAILs with `Assert.AreEqual(1, this.battlefieldView.BurstCallCount)` → actual `0` (AimingState doesn't call it yet). `OnConfirm_whileBurstPlaying_ignoresExtraConfirmAndDoesNotTransitionYet` FAILs the same way. All other existing tests still PASS.

- [ ] **Step 3: Update `AimingState`**

Replace the full contents of `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs` with:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    internal sealed class AimingState : ICombatMenuState
    {
        private readonly CombatMenuController context;
        private readonly ICombatActionMenuView menuView;
        private readonly ICommandPanelView     commandPanel;
        private readonly IBattlefieldView      battlefieldView;
        private readonly IAimView              aimView;
        private readonly IOperatorRoster       roster;

        private bool awaitingDismiss;
        private bool isPlayingBurst;

        internal AimingState(
            CombatMenuController  context,
            ICombatActionMenuView menuView,
            ICommandPanelView     commandPanel,
            IBattlefieldView      battlefieldView,
            IAimView              aimView,
            IOperatorRoster       roster)
        {
            this.context         = context;
            this.menuView        = menuView;
            this.commandPanel    = commandPanel;
            this.battlefieldView = battlefieldView;
            this.aimView         = aimView;
            this.roster          = roster;
        }

        public void Enter()
        {
            this.context.Orchestrator.SetWaitMode(true);
            this.awaitingDismiss = false;
            this.isPlayingBurst  = false;
            this.aimView.OnShotsResolved += HandleShotsResolved;
            this.aimView.Show();
        }

        public void Exit()
        {
            this.context.Orchestrator.SetWaitMode(false);
            this.aimView.OnShotsResolved -= HandleShotsResolved;
        }

        public void OnConfirm()
        {
            if (this.isPlayingBurst) return;

            if (this.awaitingDismiss)
            {
                CloseAimAndReturnToOperatorSelectionAsync().Forget();
                return;
            }
            this.aimView.Confirm();
        }

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            int totalDamage = 0;
            if (shots != null)
            {
                foreach (var shot in shots)
                    totalDamage += Mathf.Max(0, shot.Damage);
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(this.context.CurrentTargetSlot, totalDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
            }

            int op = this.context.SelectedOperator;
            if (this.roster.Count > op)
            {
                var weapon = this.roster[op].ActiveWeapon;
                if (weapon != null)
                    weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);
            }

            this.awaitingDismiss = true;
        }

        private async UniTaskVoid CloseAimAndReturnToOperatorSelectionAsync()
        {
            this.awaitingDismiss = false;
            this.aimView.Hide();
            this.commandPanel.Hide();

            this.isPlayingBurst = true;
            await this.battlefieldView.PlayOperatorShootBurstAsync(this.context.SelectedOperator, this.context.SelectedShotCount);
            this.isPlayingBurst = false;

            this.context.Orchestrator.NotifyShootCompleted();
            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner → EditMode → `CombatMenuControllerTests`.

Expected: all tests PASS, including the two added in Task 1.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(combat): play operator shoot burst after dismissing the aim QTE"
```

---

### Task 3: Real Animator-driven burst in `BattlefieldView`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

**Interfaces:**
- Consumes: nothing new (uses `Animator` from `UnityEngine`, already available).
- Produces: real implementation of `PlayOperatorShootBurstAsync` used by `AimingState` (Task 2, already wired).

- [ ] **Step 1: Add slot-indexed Animator tracking**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`, add a new field next to the existing per-slot dictionaries (after `private EnemyData?[] currentEnemySlots = Array.Empty<EnemyData?>();`, around line 39):

```csharp
private readonly Dictionary<int, Animator> operatorAnimatorBySlot = new();
private static readonly int ShootHash = Animator.StringToHash("Shoot");
```

- [ ] **Step 2: Clear the dictionary on `Populate()` and cache the Animator per operator slot**

In `Populate()`, add `this.operatorAnimatorBySlot.Clear();` next to the other `.Clear()` calls at the top (after `this.enemyRendererBySlot.Clear();`, around line 54):

```csharp
public void Populate(EncounterData encounter)
{
    foreach (var go in this.spawnedSprites)
        Destroy(go);
    this.spawnedSprites.Clear();
    this.enemyStateBySlot.Clear();
    this.enemyGoBySlot.Clear();
    this.enemyRendererBySlot.Clear();
    this.operatorAnimatorBySlot.Clear();
    this.currentEnemySlots = encounter.EnemySlots;
```

Then, in the operator spawn loop (around line 89-107), cache the `Animator` right after the GameObject is created:

```csharp
for (int i = 0; i < encounter.Operators.Length && i < this.playerSlotTransforms.Length; i++)
{
    var op = encounter.Operators[i];
    if (op == null) continue;

    GameObject go;
    if (op.BattlefieldPrefab != null)
    {
        go = Instantiate(op.BattlefieldPrefab, this.playerSlotTransforms[i], false);
    }
    else
    {
        go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.transform.SetParent(this.playerSlotTransforms[i], false);
        go.GetComponent<MeshRenderer>().material.color = Color.blue;
    }
    go.name = $"Operator_{i}";
    this.spawnedSprites.Add(go);

    var operatorAnimator = go.GetComponentInChildren<Animator>();
    if (operatorAnimator != null)
        this.operatorAnimatorBySlot[i] = operatorAnimator;
}
```

- [ ] **Step 3: Replace the stub with the real burst implementation**

Replace the stub added in Task 2 (`public UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount) => UniTask.CompletedTask;`) with:

```csharp
public async UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount)
{
    if (!this.operatorAnimatorBySlot.TryGetValue(slotIndex, out var animator) || animator == null)
        return;

    int count = Mathf.Max(1, shotCount);
    for (int i = 0; i < count; i++)
    {
        animator.SetTrigger(ShootHash);
        await UniTask.NextFrame();
        var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
        float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
        if (duration > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(duration));
    }
}
```

- [ ] **Step 4: Run the full EditMode suite to confirm no regressions**

Unity Test Runner → EditMode → run all tests (or filter by `CombatMenuControllerTests`).

Expected: all tests PASS. This class has no direct automated test for the `Animator`-driving code path itself — consistent with how the existing `PlayEnemyAttackFeedback` and `ShowOperatorDamage` methods in this same file (DOTween-driven visual feedback) are also untested directly, only exercised indirectly through `IBattlefieldView` fakes in `CombatMenuControllerTests`. `BattlefieldView` is a `MonoBehaviour`; its runtime behavior is verified manually in Play Mode in Step 5 below.

- [ ] **Step 5: Manual verification in Play Mode**

The content is already in place, so this is a real verification step, not a deferred one: `OperatorData` assets (`Ethan_Data.asset`, `Marcus_Data.asset`, `Lilou_Data.asset`) already have `battlefieldPrefab` assigned (`Ethan_Combat_FBX.prefab`, `RestPoseMarcusFBX.prefab`, `Lilou_Combat_FBX.prefab`), and those prefabs' `Animator` already uses `Operator_Combat_Controller.controller`, which already has a `Shoot` trigger parameter (`m_Type: 9`) wired to a `ShootPistolFlexed2` state. This matches `ShootHash = Animator.StringToHash("Shoot")` exactly — no further content changes are needed for this to work.

1. Enter Play Mode in a scene with a combat encounter (e.g. `Encounter_Initial`).
2. Select an operator, choose Shoot, pick a shot count > 1, resolve the aim QTE, dismiss it.
3. Confirm the operator's model plays the Shoot animation exactly `shotCount` times back-to-back, and that no enemy attacks or other operators' ATB gauges advance until the burst finishes.
4. If the animation doesn't fire, check `read_console` (MCP) for a warning/error and confirm the trigger name on the `Operator_Combat_Controller` parameter is still exactly `Shoot` (case-sensitive) — a rename there is the most likely way this silently no-ops.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(combat): drive operator Animator during the shoot burst"
```
