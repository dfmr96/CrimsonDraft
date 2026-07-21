# Operator Shoot Burst Animation — Design Spec

**Date:** 2026-07-08
**Status:** Approved
**Scope:** Combat — playing the operator's shoot animation, repeated per bullet, after the aim QTE resolves

---

## Overview

Today, resolving the aim QTE (`AimViewController.ResolvePendingShotsAsync` → `OnShotsResolved` → `AimingState.HandleShotsResolved`) only applies damage and decrements ammo. No animation plays on the operator's battlefield model, and `BattlefieldView` does not even track operator GameObjects by slot (only enemies).

This spec adds: once the player dismisses the aim window (confirms out of `WaitingDismiss`), the operator's battlefield model plays its "Shoot" animation once per bullet selected, waiting for each clip to actually finish before firing the next. The whole combat loop pauses for the duration of the burst, reusing the existing `SetWaitMode` mechanism — no new pause mechanism is introduced.

If the operator's spawned model has no `Animator` (e.g. the current placeholder `OperatorCombatModel.prefab`), the burst is a no-op and behavior is unchanged from today.

---

## `BattlefieldView` / `IBattlefieldView`

`BattlefieldView.Populate()` already instantiates each operator's `BattlefieldPrefab` into `playerSlotTransforms[i]`, but only tracks the instances in the flat `spawnedSprites` list (used solely for cleanup). It gains slot-indexed tracking mirroring the existing enemy pattern (`enemyGoBySlot` / `enemyRendererBySlot`):

```csharp
private readonly Dictionary<int, Animator> operatorAnimatorBySlot = new();
```

Populated in the operator spawn loop right after `Instantiate`:

```csharp
var anim = go.GetComponentInChildren<Animator>();
if (anim != null) this.operatorAnimatorBySlot[i] = anim;
```

Cleared alongside the other per-slot dictionaries at the top of `Populate()`.

`IBattlefieldView` gains one method:

```csharp
UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount);
```

### Implementation

```csharp
private static readonly int ShootHash = Animator.StringToHash("Shoot");

public async UniTask PlayOperatorShootBurstAsync(int slotIndex, int shotCount)
{
    if (!this.operatorAnimatorBySlot.TryGetValue(slotIndex, out var animator) || animator == null)
        return;

    int count = Mathf.Max(1, shotCount);
    for (int i = 0; i < count; i++)
    {
        animator.SetTrigger(ShootHash);
        await UniTask.NextFrame(); // let the Animator enter the "Shoot" state this frame
        var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
        float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
        if (duration > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(duration));
    }
}
```

This reads the real duration of whatever clip is assigned to the Animator Controller's "Shoot" state at runtime — not a hand-configured value — satisfying "wait for the clip to actually finish." The `Shoot` trigger name follows the existing hash-cached-parameter convention already used in `PlayerController` (`SpeedHash`, `IsAimingHash`, `ArmedHash`).

**Content dependency (not part of this code change):** the operator Animator Controller needs a "Shoot" state with Loop Time off, and the real rigged prefab needs to be assigned to `OperatorData.battlefieldPrefab` (currently `{fileID: 0}` on all three operator data assets — a pre-existing gap, tracked separately). Until that content exists, `operatorAnimatorBySlot` stays empty for that slot and the method above returns immediately, so combat behavior is unaffected.

---

## `AimingState`

`CloseAimAndReturnToOperatorSelection` becomes an async `UniTaskVoid`, fired with `.Forget()` — the same fire-and-forget pattern already used in `AimViewController.Confirm()` for `ResolvePendingShotsAsync()`. The state transition (and `Orchestrator.NotifyShootCompleted()`) is postponed until the burst finishes:

```csharp
private bool isPlayingBurst;

public void OnConfirm()
{
    if (this.isPlayingBurst) return; // ignore input while the burst is playing

    if (this.awaitingDismiss)
    {
        CloseAimAndReturnToOperatorSelectionAsync().Forget();
        return;
    }
    this.aimView.Confirm();
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
```

### Why this pauses the whole combat loop

`AimingState.Enter()` already calls `Orchestrator.SetWaitMode(true)`; `Exit()` (which only runs when `TransitionTo` actually changes state) is what calls `SetWaitMode(false)`. Since the state transition now waits on the burst `UniTask`, `waitModeActive` stays `true` — pausing ATB ticking for every actor — for the full duration of the burst. No changes to `CombatOrchestrator` or the `animationLockUntil` mechanism are needed; this reuses the pause mechanism that already gates the aim window today.

The `isPlayingBurst` guard prevents `OnConfirm()` from re-entering `CloseAimAndReturnToOperatorSelectionAsync` if the player mashes confirm while the views are hidden and the burst is still playing.

---

## Out of scope

- Authoring the operator Animator Controller, rig, and "Shoot" clip (content work, tracked separately alongside the `battlefieldPrefab` assignment gap).
- Varying animation choice by weapon type or burst pattern shape — this spec always repeats a single "Shoot" trigger `shotCount` times.
- Enemy-side attack animations (enemies currently use a DOTween shake in `PlayEnemyAttackFeedback`, untouched by this change).
